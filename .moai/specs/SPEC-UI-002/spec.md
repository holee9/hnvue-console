# SPEC-UI-002: AsyncRelayCommand Code Review Improvements

## Requirements (EARS Format)

### 1. [SHALL] Dispatcher Null Handling

**The system shall** allow null dispatcher parameter to truly bypass dispatcher marshaling for unit testing.

**Rationale:** Current XML documentation states "pass null to bypass dispatcher requirements for testing" but actual implementation uses `Dispatcher.CurrentDispatcher` when null is passed. This causes tests without a WPF dispatcher to fail.

**Acceptance Criteria:**
- [x] When `dispatcher: null` is passed to constructor, `_dispatcher` field shall be `null`
- [x] `RaiseCanExecuteChanged()` shall invoke event directly when `_dispatcher` is `null`
- [x] Existing tests with `dispatcher: null` shall pass without exceptions

### 2. [SHALL] CTS Cleanup Order

**The system shall** clean up CancellationTokenSource before marking execution as complete.

**Rationale:** Current implementation calls `EndExecution()` before CTS cleanup, creating a window where `IsExecuting` is false but CTS is still valid. This can cause race conditions in rapid re-execution scenarios.

**Acceptance Criteria:**
- [x] In `Execute()` finally block, CTS cleanup shall occur before `EndExecution()`
- [x] `CanExecute` shall return false until both execution state and CTS are cleaned up
- [x] No regression in concurrent execution prevention

### 3. [SHALL] Dispose Event Guarding

**The system shall** prevent event raising after command disposal.

**Rationale:** Fire-and-forget `BeginInvoke` can raise events after command is disposed, potentially causing issues in applications with strong event subscriptions.

**Acceptance Criteria:**
- [x] `RaiseCanExecuteChanged()` shall return without action if command is disposed
- [x] `Dispose()` shall set disposed flag before cleaning up resources
- [x] Disposed flag shall be thread-safe using Interlocked operations

---

## Technical Approach

### Modified Files

1. **AsyncRelayCommandBase.cs**
   - Line 35: Dispatcher assignment logic
   - Line 69-78: `RaiseCanExecuteChanged()` null handling
   - New field: `_disposed` flag
   - `Dispose()`: Add disposed check

2. **AsyncRelayCommand.cs**
   - Line 76-89: `Execute()` finally block order
   - Line 161-174: `Execute<T>()` finally block order

3. **AsyncRelayCommandTests.cs**
   - New tests for dispatcher null behavior
   - New tests for disposed event guarding
   - New tests for CTS cleanup timing

### Implementation Strategy

**Step 1:** AsyncRelayCommandBase modifications
- Change constructor: `_dispatcher = dispatcher;` (remove null coalescing)
- Update `RaiseCanExecuteChanged()`: Add null check branch
- Add `_disposed` field and thread-safe disposal

**Step 2:** Derived class Execute method modifications
- Move CTS cleanup before `EndExecution()` call
- Ensure both `AsyncRelayCommand` and `AsyncRelayCommand<T>` are updated

**Step 3:** Test coverage additions
- Verify null dispatcher behavior works as documented
- Test disposed commands don't raise events
- Test CTS cleanup timing with concurrent operations

---

## Non-Functional Requirements

### Performance
- No performance regression expected (slightly fewer operations in null dispatcher case)

### Compatibility
- **Breaking Change:** Code relying on implicit `Dispatcher.CurrentDispatcher` when passing null will need adjustment
- **Mitigation:** This is bug fix behavior; affected code was already incorrect per XML docs

### Security
- No security implications

---

## Dependencies

- SPEC-UI-001: AsyncRelayCommand original implementation
- .NET 8 WPF Dispatcher API
- xUnit testing framework

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Test Pass Rate | 100% | All existing + new tests pass |
| Code Coverage | 85%+ | coverlet coverage report |
| LSP Errors | 0 | dotnet build output |
| LSP Warnings | 0 | dotnet build output |

---

## References

- CommunityToolkit.Mvvm.AsyncRelayCommand: Reference for dispatcher handling patterns
- Prism.Commands.DelegateCommand: Reference for disposal patterns
- Original code review feedback (user message, truncated)
