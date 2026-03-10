# SPEC-UI-002: Acceptance Criteria

## Overview

모든 수락 기준은 `AsyncRelayCommandTests.cs`의 SPEC-UI-002 region에 해당하는 테스트로 검증됩니다.

---

## AC-1: Dispatcher Null Handling

### AC-1.1: Null Dispatcher Constructor

**Given** `AsyncRelayCommand`가 `dispatcher: null`로 생성될 때
**When** 생성자가 실행되면
**Then** `_dispatcher` 필드는 `null`이어야 한다 (Dispatcher.CurrentDispatcher 사용 금지)

**Test**: `NullDispatcher_RaiseCanExecuteChanged_DoesNotThrow`
```
[Fact] NullDispatcher_RaiseCanExecuteChanged_DoesNotThrow
  Given: command = new AsyncRelayCommand(ct => Task.CompletedTask, dispatcher: null)
  When: command.RaiseCanExecuteChanged()
  Then: No exception thrown
```
**Status**: PASS

### AC-1.2: Null Dispatcher Event Invocation

**Given** `_dispatcher`가 `null`인 커맨드의
**When** `RaiseCanExecuteChanged()`가 호출되면
**Then** 이벤트가 디스패처 마샬링 없이 현재 스레드에서 직접 발생해야 한다

**Test**: `NullDispatcher_Execute_CompletesSuccessfully`
```
[Fact] NullDispatcher_Execute_CompletesSuccessfully
  Given: command with null dispatcher and event subscriber
  When: command.Execute(null)
  Then: CanExecuteChanged event raised, no InvalidOperationException
```
**Status**: PASS

### AC-1.3: Null Dispatcher Generic Command

**Given** `AsyncRelayCommand<T>`가 `dispatcher: null`로 생성될 때
**When** `Execute(parameter)`가 호출되면
**Then** 파라미터가 올바르게 전달되고 예외가 발생하지 않아야 한다

**Test**: `Generic_NullDispatcher_Execute_PassesParameterCorrectly`
```
[Fact] Generic_NullDispatcher_Execute_PassesParameterCorrectly
  Given: command<string> with null dispatcher
  When: command.Execute("test-value")
  Then: execute delegate receives "test-value" parameter, no exception
```
**Status**: PASS

---

## AC-2: CTS Cleanup Order

### AC-2.1: CTS Before EndExecution

**Given** `AsyncRelayCommand.Execute()`가 완료될 때
**When** `finally` 블록이 실행되면
**Then** CTS 정리(`Dispose`)는 `EndExecution()`(IsExecuting = false) **이전**에 발생해야 한다

**Test**: `Execute_CtsCleanedBeforeStateChange`
```
[Fact] Execute_CtsCleanedBeforeStateChange
  Given: command that captures CTS reference during execution
  When: execution completes (finally block runs)
  Then: localCts.IsCancellationRequested == false before EndExecution sets IsExecuting=false
        AND: after completion, command is no longer executing
```
**Status**: PASS

### AC-2.2: CanExecute After CTS Cleanup

**Given** `Execute()`가 완료된 후
**When** `CanExecute()`가 호출되면
**Then** CTS와 실행 상태가 모두 정리된 후에만 `true`를 반환해야 한다

**Test**: `Execute_CompleteThenCanExecute_ReturnsTrueImmediately`
```
[Fact] Execute_CompleteThenCanExecute_ReturnsTrueImmediately
  Given: completed command execution
  When: CanExecute(null) is called immediately after completion
  Then: returns true (both CTS and IsExecuting cleaned up)
```
**Status**: PASS

---

## AC-3: Dispose Event Guarding

### AC-3.1: No Events After Dispose

**Given** `AsyncRelayCommand`이 `Dispose()`된 후
**When** `RaiseCanExecuteChanged()`가 호출되면
**Then** 이벤트가 발생하지 않아야 한다 (BeginInvoke로도 예약되지 않아야 한다)

**Test**: `Dispose_ThenRaiseCanExecuteChanged_NoEventRaised`
```
[Fact] Dispose_ThenRaiseCanExecuteChanged_NoEventRaised
  Given: command.Dispose() called
  When: command.RaiseCanExecuteChanged()
  Then: CanExecuteChanged event NOT raised (eventRaised == false)
```
**Status**: PASS

### AC-3.2: Thread-Safe Dispose Flag

**Given** `Dispose()`가 여러 스레드에서 동시에 호출될 때
**When** 첫 번째 Dispose가 완료되면
**Then** 후속 Dispose 호출은 예외 없이 안전하게 무시되어야 한다

**Test**: `Dispose_MultipleTimes_AllSucceed`
```
[Fact] Dispose_MultipleTimes_AllSucceed
  Given: valid command
  When: command.Dispose() called 3 times
  Then: no exception, no ObjectDisposedException
```
**Status**: PASS

### AC-3.3: Dispose Flag Atomicity

**Given** 커맨드가 실행 중일 때 `Dispose()`가 호출되면
**When** `Dispose()` 이후 CTS 콜백이 실행되면
**Then** `CanExecuteChanged` 이벤트가 발생하지 않아야 한다 (Interlocked 보장)

**Test**: `Dispose_WhileExecuting_RaisesNoEventsAfterDisposal`
```
[Fact] Dispose_WhileExecuting_RaisesNoEventsAfterDisposal
  Given: command executing long-running async operation
  When: command.Dispose() called mid-execution
  Then: no CanExecuteChanged events raised after disposal
        AND: no exceptions thrown
```
**Status**: PASS

---

## Test Coverage Summary

| Requirement | Tests | Status |
|------------|-------|--------|
| AC-1: Dispatcher Null Handling | 3 | PASS |
| AC-2: CTS Cleanup Order | 2 | PASS |
| AC-3: Dispose Event Guarding | 3 | PASS |
| **Total** | **8** | **PASS** |

## Non-Functional Acceptance

| Criteria | Target | Actual | Status |
|---------|--------|--------|--------|
| Test Pass Rate | 100% | 100% (38/38) | PASS |
| LSP Errors | 0 | 0 | PASS |
| LSP Warnings | 0 (code) | 0 (code) | PASS |
| Build Success | Yes | Yes | PASS |

---

**Verified**: 2026-03-10
**Test Run**: `dotnet test --filter "FullyQualifiedName~AsyncRelayCommand"` → 38 passed
