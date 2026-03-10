# SPEC-UI-002: Implementation Plan

## Overview

AsyncRelayCommand 코드 리뷰에서 발견된 3가지 버그를 수정하는 최소 범위 구현 계획.

## Technical Approach

### Step 1: AsyncRelayCommandBase Modifications

**File**: `src/HnVue.Console/Commands/AsyncRelayCommandBase.cs`

**Changes**:
1. Constructor dispatcher assignment:
   - Before: `_dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;`
   - After: `_dispatcher = dispatcher;` (null coalescing 제거)
   - Reason: XML 문서와 실제 동작 일치

2. `RaiseCanExecuteChanged()` null dispatcher handling:
   - Before: null 분기 없음 (항상 `_dispatcher.CheckAccess()` 호출)
   - After: `if (_dispatcher == null)` 분기로 직접 이벤트 호출
   - Reason: 테스트 환경에서 예외 방지

3. `_disposed` flag 추가:
   - Type: `protected int _disposed;` (Interlocked 지원을 위해 int)
   - `Dispose()`: `Interlocked.Exchange(ref _disposed, 1)` 로 thread-safe 설정
   - `RaiseCanExecuteChanged()`: `Volatile.Read(ref _disposed) == 1` 체크 추가

### Step 2: AsyncRelayCommand Execute() finally block

**File**: `src/HnVue.Console/Commands/AsyncRelayCommand.cs`

**Changes** (both `AsyncRelayCommand` and `AsyncRelayCommand<T>`):
- Before: `EndExecution()` → CTS cleanup
- After: CTS cleanup → `EndExecution()`
- Reason: CTS 정리 전에 `CanExecute`가 true를 반환하는 경쟁 조건 방지

### Step 3: Test Coverage Additions

**File**: `tests/csharp/HnVue.Console.Tests/Commands/AsyncRelayCommandTests.cs`

**New test regions** (8 tests):

1. SPEC-UI-002: Dispatcher Null Handling (3 tests)
   - `NullDispatcher_RaiseCanExecuteChanged_DoesNotThrow`
   - `NullDispatcher_Execute_CompletesSuccessfully`
   - `Generic_NullDispatcher_Execute_PassesParameterCorrectly`

2. SPEC-UI-002: CTS Cleanup Order (2 tests)
   - `Execute_CtsCleanedBeforeStateChange`
   - `Execute_CompleteThenCanExecute_ReturnsTrueImmediately`

3. SPEC-UI-002: Dispose Event Guarding (3 tests)
   - `Dispose_ThenRaiseCanExecuteChanged_NoEventRaised`
   - `Dispose_MultipleTimes_AllSucceed`
   - `Dispose_WhileExecuting_RaisesNoEventsAfterDisposal`

## Dependency Analysis

- No new external dependencies required
- Uses existing: `System.Threading.Interlocked`, `System.Threading.Volatile`
- Test helpers: Existing `ViewModelTestBase`

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Breaking existing dispatcher users | Medium | Medium | Only affects code passing null (already broken per docs) |
| Thread safety regression | Low | High | Added test: `Dispose_WhileExecuting_RaisesNoEventsAfterDisposal` |
| CTS cleanup order regression | Low | Medium | Added test: `Execute_CtsCleanedBeforeStateChange` |

## Out of Scope

The following research.md recommendations are NOT included in this SPEC:
- Race Condition Fix (atomic lock-based CanExecute+Execute check)
- CTS Memory Leak in rapid re-execution (CTS pooling)
- Exception Handling Enhancement (`IsCancellationRequested` property)
- Performance Optimization (Read-Write Lock, ValueTask support)
- Weak Event Pattern

These would require a separate SPEC (e.g., SPEC-UI-003).

## Implementation Order

1. AsyncRelayCommandBase.cs changes (no dependencies)
2. AsyncRelayCommand.cs changes (depends on base class interface)
3. AsyncRelayCommandTests.cs additions (depends on implementation)

## Completion Criteria

- [ ] `dotnet build` succeeds with 0 errors
- [ ] All SPEC-UI-002 tests pass (8 tests)
- [ ] Total test suite passes (all pre-existing tests)
- [ ] Code coverage >= 85% for Commands namespace

---

**Status**: COMPLETE (2026-03-02)
**Implementation**: All 3 requirements implemented and tested.
