# AsyncRelayCommand Research Analysis

**SPEC-UI-002: AsyncRelayCommand Code Review and Improvement Plan**

Date: 2026-03-02

## Executive Summary

이 분석은 HnVue Console의 AsyncRelayCommand 구현에 대한 심층 코드 리뷰 결과입니다. 현재 구현은 완전한 취소, 스레드 안전성, UI 스레드 마샬링 등 기본적인 요구사항을 충족하지만, 여러 개선 영역이 발견되었습니다. 특히 CTS 생명주기 관리, 동시성 제어 패턴, 오류 처리 개선이 필요합니다.

## Current Implementation Analysis

### Architecture Overview

```
AsyncRelayCommandBase (abstract)
├── AsyncRelayCommand : ICommand
└── AsyncRelayCommand<T> : ICommand
```

**Key Components:**
- **AsyncRelayCommandBase**: 공통 기능 제공 (IDisposable, CTS 관리, IsExecuting 상태)
- **AsyncRelayCommand/AsyncRelayCommand<T>**: ICommand 구현, 실행 로직 위임

### Threading Analysis

#### Current Patterns
1. **Lock-based Concurrency Control** (AsyncRelayCommandBase.cs:17,45,86,97,133)
   ```csharp
   protected readonly object _stateLock = new();
   lock (_stateLock) { /* state access */ }
   ```
   - 장점: 단순하고 직관적
   - 단점: 성능 병목 가능성, 데드리스크 발생 가능

2. **Dispatcher Integration** (AsyncRelayCommandBase.cs:71-73)
   ```csharp
   if (_dispatcher != null && !_dispatcher.CheckAccess())
   {
       _dispatcher.BeginInvoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
   }
   ```
   - UI 스레드에서만 CanExecuteChanged 이벤트 발생
   - 테스트 환경에서는 null 디스패처 지원

3. **CTS Lifecycle Management**
   - StartNewExecution(): 이전 CTS를 취소하고 새로 생성 (Interlocked 사용)
   - Execute 완료 후: 로컬 CTS 정리 (메모리 누수 방지)

#### Issues Identified

1. **Concurrency Race Condition** (Critical)
   - 위치: AsyncRelayCommand.cs:58-86
   - 문제: `CanExecute` 호출과 `Execute` 실행 사이의 타이밍 문제
   - 영향: 동시 실행 가능성, 상태 불일치

2. **CTS Memory Leak Risk**
   - 위치: AsyncRelayCommand.cs:82-86
   - 현재 구현은 Interlocked.CompareExchange를 사용하지만, 빠른 재실행 시 CTS 누수 가능

3. **Thread Abort Handling**
   - 현재 구현은 Thread.Abort를 처리하지 않음
   - .NET 5+에서는 권장되지 않지만, 이식성을 고려해야 함

### CTS Lifecycle Analysis

#### Current Flow
1. **Execute 호출**
   - StartNewExecution()로 새 CTS 생성 (이전 CTS 취소)
   - IsExecuting = true 설정
   - 로컬 변수에 CTS 캡처

2. **실행 중**
   - 로컬 CTS.Token 전달
   - 예외 발생 시 Error 호출 (OperationCanceledException 제외)

3. **실행 완료**
   - EndExecution() 호출
   - CTS 정리 (CompareExchange로 확인 후 Dispose)

#### Issues
- **Cancellation Race**: Cancel() 호출과 Execute 동시 발생 시 상태 불일치
- **Nested Execution**: 재귀 실행 시 CTS 교체 문제
- **Timeout Support**: 현재는 구현되지 않음

### Event Marshaling Analysis

#### CanExecuteChanged Implementation
- 항상 UI 스레드에서 이벤트 발생
- Dispatcher.CheckAccess()로 교차 스레드 호출 처리
- 성능 문제: BeginInvoke 사용 시 추가 스케줄링 오버헤드

#### RaiseCanExecuteChanged() 호출 패턴
- IsExecuting 변경 시 자동 호출 (line 57, 102)
- 수동 호출 필요성 없음 (MVVM 프레임워크와 자동 통합)

### Cross-Module Interactions

#### ViewModel Integration Patterns
```csharp
// Typical usage pattern from AcquisitionViewModel.cs:63-66
StartPreviewCommand = new AsyncRelayCommand(
    ct => ExecuteStartPreviewAsync(ct),
    () => !IsPreviewActive);
```

**Observed Patterns:**
1. **Command-ViewModel Coupling**: 모든 Command가 ViewModel의 상태에 의존
2. **Binding**: WPF CommandBinding을 통해 UI와 연결
3. **Lifetime**: ViewModel Dispose 시 Command Dispose 필요

#### Dependencies Identified
- ViewModelBase.cs: INotifyPropertyChanged 구현
- Dispatcher.CurrentDispatcher: UI 스레드 인식
- gRPC 통신: 장치 제어와의 연동

## Reference Implementations

### CommunityToolkit.Mvvm Patterns

[Microsoft/CommunityToolkit](https://github.com/CommunityToolkit/CommunityToolkit) 에서 발견된 패턴:

```csharp
// Reference implementation
public class AsyncRelayCommand : IAsyncRelayCommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool> _canExecute;
    private readonly CancellationTokenSource _cts;

    // Key differences:
    // 1. Single CTS instance with Reset() instead of recreate
    // 2. [ObservableProperty] for CanExecute
    // 3. ValueTask support for better performance
}
```

**Best Practices Found:**
1. **CTS Reset**: 재사용 가능한 단일 CTS 인스턴스
2. **WeakEventManager**: 메모리 누수 방지를 위한 약한 참조
3. **ValueTask<T>**: 성능 최적화

### MVVM Light Patterns

MVVM Light의 RelayCommand 구현 참조:
```csharp
public class RelayCommand : ICommand
{
    private readonly WeakReference _canExecuteCallback;
    // WeakReference를 사용한 메모리 관리
}
```

### Prism Patterns

Prism의 DelegateCommand 구현:
```csharp
public class DelegateCommand : ICommand
{
    private readonly SynchronizationContext _synchronizationContext;
    // SynchronizationContext 사용으로 플랫폼 독립성 확보
}
```

## Edge Cases and Potential Issues

### 1. Rapid Execution Pattern
```csharp
// Current implementation may not handle this well
command.Execute(null); // Quick succession
command.Execute(null); //
command.Execute(null); //
```
**Issue**: CTS 교체 과정에서 누수 발생 가능성

### 2. Cancellation During Execution
```csharp
// Cancel() during async execution
command.Cancel();  // Should cancel all pending operations
command.Execute(null); // Should start new execution
```
**Current**: 동작하지만 경쟁 조건 발생 가능

### 3. Exception Propagation
```csharp
try {
    command.Execute(null);
}
catch (Exception) {
    // OperationCanceledException is caught internally
    // Other exceptions bubble up
}
```
**Issue**: 외부에서 OperationCanceledException 구분 불가능

### 4. Test Environment Issues
```csharp
// Test code may create race conditions
var command = new AsyncRelayCommand(ct => Task.Delay(100), null, null, null);
```
**Issue**: null 디스패처 시 스레드 안전성 보장 불분명

### 5. Nested Command Execution
```csharp
// Command가 다른 Command를 실행
await ExecuteAsync(ct);
var nestedCommand = CreateNestedCommand();
nestedCommand.Execute(null);
```
**Issue**: CTS 상태 공유 문제

## Performance Considerations

### Current Bottlenecks
1. **Lock Contention**: `_stateLock` 사용으로 스레드 경합 발생
2. **Dispatcher Overhead**: BeginInvoke 호출 시 스케줄링 비용
3. **CTS Creation/Disposal**: 빈번한 CTS 교체는 GC 부하

### Optimization Opportunities
1. **Read-Write Lock**: IsExecuting 읽기/쓰기 분리
2. **Lazy Initialization**: CTS 지연 생성
3. **Object Pooling**: CTS 재사용

## Missing Test Coverage

### Critical Scenarios (Current Tests: 96% Coverage)
1. **Stress Test**: 동시에 100개 Command 실행
2. **Memory Leak**: 반복 실행 후 메모리 누수 검증
3. **Thread Abort**: 스레드 중단 시 상태 정리
4. **Deep Cancellation**: 취소 중 재취소 요청
5. **Mixed Exception Types**: AggregateException 처리

### Edge Cases Not Tested
1. **Dispatcher 없는 환경** (비 UI 스레드에서 실행)
2. **Weak Event Pattern**: 메모리 누수 검증
3. **ValueTask 지원**: 성능 비교 테스트
4. **Culture Info**: 지역화된 오류 메시지

## Recommendations

### 1. High Priority (Critical)

#### A. Race Condition Fix
```csharp
// Before (AsyncRelayCommand.cs:52-56)
if (!CanExecute(parameter)) return;

// After - Atomic check
lock (_stateLock)
{
    if (_isExecuting || (_canExecute != null && !_canExecute()))
        return;
    _isExecuting = true;
}
```

#### B. CTS Life Cycle Improvements
```csharp
// Implement CTS pool for rapid execution
private readonly SemaphoreSlim _ctsPool = new(1);
private CancellationTokenSource? _pooledCts;
```

#### C. Exception Handling Enhancement
```csharp
// Allow external cancellation detection
public bool IsCancellationRequested =>
    _cancellationTokenSource?.IsCancellationRequested ?? false;
```

### 2. Medium Priority

#### A. Performance Optimization
- Read-Write Lock으로 교체
- ValueTask<T> 지원 추가
- CTS 객체 풀링

#### B. Test Coverage
- 스트레스 테스트 추가
- 메모리 누수 검증
- 비 UI 스레드 테스트

#### C. API Enhancement
- Timeout 매개변수 지원
- Progress<T> 보고 지원
- Weak Event 연결

### 3. Low Priority (Nice to Have)

#### A. Monitoring Support
```csharp
// Add execution metrics
public ExecutionMetrics Metrics { get; } = new();
```

#### B. Debugging Aids
```csharp
[Conditional("DEBUG")]
public void DebugDumpState()
{
    // Log current execution state
}
```

## Implementation Strategy

### Phase 1: Critical Fixes (1-2 days)
1. 경쟁 조건 수정 (Race Condition Fix)
2. CTS 누수 방지 개선
3. 예외 처리 강화

### Phase 2: Performance (2-3 days)
1. 동기화 최적화
2. 메모리 관리 개선
3. 성능 테스트

### Phase 3: Features (3-4 days)
1. 추가 API 기능
2. 모니터링 지원
3. 문서 업데이트

## Conclusion

현재 AsyncRelayCommand 구현은 기본적인 요구사항을 충족하지만, 생산 환경에서 사용하기 위해서는 몇 가지 중요한 개선이 필요합니다. 특히 스레드 안전성과 메모리 관리가 우선순위입니다. 제안된 개선 사항을 단계적으로 구현하면 훨씬 더 안정적이고 성능이 우수한 MVVM async command를 구축할 수 있습니다.

**총 평가**: B- (기능 완성도는 높으나 성능 및 안정성 개선 필요)