# HnVue Console - Project Memory

## Project Overview
Medical device X-ray GUI console SW (IEC 62304 Class B/C). .NET 8 / C# 12 / WPF.
- Main repo: `/mnt/work/workspace-github/hnvue-console`
- Target framework: `net8.0-windows` (global via Directory.Build.props) - WPF dependency
- Test projects should override to `net8.0` for Linux cross-platform execution

## SPEC Implementation Status (2026-02-28)

### Completed
- SPEC-IPC-001, SPEC-HAL-001, SPEC-IMAGING-001, SPEC-INFRA-001: ✅ 완료
- **SPEC-DICOM-001**: ✅ 완료 (2026-02-28)
  - Storage SCU (C-STORE)
  - Worklist SCU (C-FIND)
  - MPPS SCU (N-CREATE/N-SET)
  - Storage Commitment SCU (N-ACTION/N-EVENT-REPORT)
  - QueryRetrieve SCU (C-FIND/C-MOVE) - Optional
  - TLS Security Support
  - Retry Queue with persistent storage
  - DICOM Conformance Statement
  - Full test coverage (135+ tests passing)

### Pending
- SPEC-DOSE-001: 미시작 (feat/spec-dose-001 worktree ready)
- SPEC-WORKFLOW-001: 미시작 (depends on DOSE)
- SPEC-UI-001: 미시작 (depends on WORKFLOW)
- SPEC-TEST-001: 미시작 (parallel, Python HW simulator needed for WORKFLOW)

## Key Findings

### .gitignore Bug (FIXED)
- Line 223 had `*.cs` (excluded ALL C# files) → fixed to `*.pb.cs`
- Must use `git add -f` for new .cs files in worktrees

### Test Framework
- xUnit + Moq + FluentAssertions
- Test projects need `<TargetFramework>net8.0</TargetFramework>` override in .csproj
- Tests: `dotnet test tests/csharp/HnVue.Dicom.Tests/`

### fo-dicom 5.x API Notes
- `DicomClient(host, port, useTls, callingAe, calledAe)` positional args
- `TransmissionQueue` implements `IAsyncDisposable` → `await using`

## Architecture Notes

### HnVue.Dicom Module (Completed)
- Entry: `DicomServiceFacade` (IDicomServiceFacade)
- SCUs: StorageScu, WorklistScu, MppsScu, StorageCommitScu, QueryRetrieveScu
- IOD builders: DxImageBuilder, CrImageBuilder, RdsrBuilder
- Infrastructure: TransmissionQueue (JSON-file retry), UidGenerator, TlsFactory
- DI: `ServiceCollectionExtensions.AddHnVueDicom()`
- RDSR interface: `IRdsrDataProvider` (HnVue.Dose implements, HnVue.Dicom consumes)

## Git Worktrees
```
main:                  /mnt/work/workspace-github/hnvue-console  [main]
feat/spec-dose-001:    (removed - ready to start when needed)
```

## Recent Commits
```
d8bf162 chore: add .worktrees/ to .gitignore
1e45e8e feat(dicom): complete SPEC-DICOM-001 QueryRetrieve and Conformance Statement
```
