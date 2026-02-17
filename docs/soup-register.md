# SOUP Register - Software of Unknown Provenance

**Maintenance Per**: IEC 62304 Section 8.1.2
**Last Updated**: 2026-02-18
**Version**: 0.1.0

## Purpose

This register tracks all third-party software components (SOUP - Software of Unknown Provenance) integrated into the HnVue system. Each entry includes risk classification, justification, and verification status per IEC 62304 requirements.

---

## Register Entries

### C++ SOUP Components

| Library | Version | Manufacturer/Source | Risk Class | Safety Analysis | Verification | Date Added |
|---------|---------|---------------------|------------|-----------------|--------------|------------|
| gRPC | 1.68.x | https://github.com/grpc/grpc | Low | IPC transport only; no safety impact | Not Verified | 2026-02-18 |
| Protocol Buffers | 25.x | https://github.com/protocolbuffers/protobuf | Low | Data serialization; no safety impact | Not Verified | 2026-02-18 |
| spdlog | 1.13.x | https://github.com/gabime/spdlog | Low | Logging only; no safety impact | Not Verified | 2026-02-18 |
| fmt | 10.x | https://github.com/fmtlib/fmt | Low | String formatting only; no safety impact | Not Verified | 2026-02-18 |
| Google Test | 1.14.x | https://github.com/google/googletest | None | Test-only, not shipped | N/A | 2026-02-18 |

### C# SOUP Components

| Library | Version | Manufacturer/Source | Risk Class | Safety Analysis | Verification | Date Added |
|---------|---------|---------------------|------------|-----------------|--------------|------------|
| fo-dicom | 5.1.x | https://github.com/fo-dicom/fo-dicom | Medium | DICOM protocol stack; safety-critical data path | Not Verified | 2026-02-18 |
| Prism.Core | 9.x | https://prismlibrary.com/ | Low | MVVM framework only; no safety impact | Not Verified | 2026-02-18 |
| Prism.Wpf | 9.x | https://prismlibrary.com/ | Low | MVVM framework only; no safety impact | Not Verified | 2026-02-18 |
| Microsoft.Extensions.Logging | 8.x | Microsoft | Low | Logging only; no safety impact | Not Verified | 2026-02-18 |
| Microsoft.Extensions.DI | 8.x | Microsoft | Low | DI container only; no safety impact | Not Verified | 2026-02-18 |
| xUnit | 2.7.x | https://xunit.net/ | None | Test-only, not shipped | N/A | 2026-02-18 |

### Infrastructure SOUP

| Component | Version | Manufacturer/Source | Risk Class | Safety Analysis | Verification | Date Added |
|-----------|---------|---------------------|------------|-----------------|--------------|------------|
| Orthanc (Docker) | 24.x | https://www.orthanc-server.com/ | None | Test-environment only, not shipped | N/A | 2026-02-18 |

---

## Risk Classification Criteria

- **None**: Test-only code, not shipped in production
- **Low**: No direct safety impact, failure does not affect patient safety
- **Medium**: Potential indirect safety impact, requires analysis
- **High**: Direct safety impact, requires rigorous verification and validation

---

## Change Control

All SOUP additions, updates, or removals must:

1. Be documented in this register
2. Undergo risk classification
3. For Risk Class B/C: Complete safety analysis per IEC 62304
4. Be linked to the corresponding pull request
5. Update the `vcpkg.json` or `Directory.Packages.props` files

---

## References

- IEC 62304:2006(R) Medical device software - Software life cycle processes
- IEC 62304 Clause 8.1.2: SOUP items
- ISO 13485:2016 Medical devices - Quality management systems

---

*Maintained by: abyz-lab <hnabyz2023@gmail.com>*
