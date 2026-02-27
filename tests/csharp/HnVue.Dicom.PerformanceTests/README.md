# HnVue.Dicom.PerformanceTests

Performance benchmarks for HnVue DICOM Communication Services.

## Purpose

Validates SPEC-DICOM-001 Non-Functional Requirements:
- **NFR-PERF-01**: C-STORE 50MB DX image within 10 seconds (100 Mbit/s)
- **NFR-PERF-02**: Worklist C-FIND query within 3 seconds
- **NFR-PERF-03**: MPPS N-CREATE within 2 seconds

## Prerequisites

- .NET 8.0 SDK
- (Optional) Running DICOM SCP instances for full integration benchmarks

## Running Benchmarks

### All Benchmarks
```bash
cd tests/csharp/HnVue.Dicom.PerformanceTests
dotnet run -c Release
```

### Specific Benchmark Suite
```bash
# C-STORE only
dotnet run -c Release -- --filter Cstore

# Worklist only
dotnet run -c Release -- --filter Worklist

# MPPS only
dotnet run -c Release -- --filter Mpps
```

## Benchmark Details

### CstorePerformanceTests
- `TranscodeInMemory_ImplicitToJpeg2000Lossless`: Codec performance (no SCP required)
- `TranscodeInMemory_ImplicitToExplicitVR`: VR transcoding (no SCP required)
- `CreateLargeDicomFile_50MB`: File creation overhead (no SCP required)
- `StoreAsync_ImplicitVRLittleEndian`: C-STORE with baseline syntax (requires SCP on localhost:11112)
- `StoreAsync_ExplicitVRLittleEndian`: C-STORE with preferred syntax (requires SCP on localhost:11112)

### WorklistPerformanceTests
- `CreateWorklistQuery`: Query object construction (no SCP required)
- `CreateDateRange_Today`: Date range creation (no SCP required)
- `CreateDateRange_Custom`: Custom date range creation (no SCP required)
- `QueryAsync_FullWorklist`: Full C-FIND query (requires MWL SCP on localhost:11114)
- `QueryAsync_PatientIdFilter`: Filtered C-FIND query (requires MWL SCP on localhost:11114)

### MppsPerformanceTests
- `CreateMppsData`: MPPS data object construction (no SCP required)
- `GenerateExposureData_10Images`: Exposure data generation (no SCP required)
- `CreateProcedureStepAsync`: N-CREATE MPPS (requires MPPS SCP on localhost:11116)
- `PrepareCompletedMppsData`: Completed MPPS data preparation (no SCP required)

## Output

Benchmark results are saved to `BenchmarkDotNet.Artifacts/results/`:
- CSV reports for statistical analysis
- HTML reports for visualization
- Console output with mean, std dev, percentiles

## Performance Targets

| Test | Target | Spec Reference |
|------|--------|----------------|
| C-STORE 50MB | <= 10s | NFR-PERF-01 |
| C-FIND 50 items | <= 3s | NFR-PERF-02 |
| N-CREATE MPPS | <= 2s | NFR-PERF-03 |

## Running with Real DICOM SCPs

For full end-to-end benchmarking, start the following DICOM SCPs:

### Orthanc (for C-STORE)
```bash
docker run -d -p 11112:4242 orthancteam/orthanc:latest
```

### Modality Worklist SCP
Orthanc does not include a built-in MWL SCP. Use a dedicated MWL SCP implementation or configure a PACS with MWL support.

### MPPS SCP
Configure a dedicated MPPS SCP or PACS with MPPS support.

## Troubleshooting

### Port conflicts
```
Error: port 11112 is already allocated
```
**Solution**: Stop other services using ports 11112, 11114, 11116 or modify the port bindings in the benchmark code

### SCP not running
Benchmarks that require a running SCP will gracefully handle connection failures and report the SCU implementation performance only.

### BenchmarkDotNet artifacts not found
Results are in `BenchmarkDotNet.Artifacts/` relative to the build output directory, not the source directory.
