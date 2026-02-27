# OQ-01: DICOM Organization UID Root Configuration

## Status: OPEN - Required Before Go-Live

## Issue
HnVue.Dicom currently uses DICOM UID root `2.25` (UUID-based test root) as the default
when no organizational root is configured. This is acceptable for development and testing
but MUST be replaced with a registered organizational OID before production deployment.

## Required Action
Obtain a registered DICOM UID root from one of the following sources:
1. **DICOM Standards Committee**: Apply for an organizational root at https://www.dicomstandard.org/
2. **ISO OID Registry**: Register an OID at https://www.oid-info.com/
3. **Enterprise OID**: Use company's existing OID subtree (e.g., 1.2.840.XXXXX)

## Configuration
Once a root is obtained, set it in `appsettings.json`:
```json
{
  "DicomService": {
    "UidRoot": "1.2.840.XXXXX.YOUR_ORG_ROOT",
    "DeviceSerial": "HNVUE_SN_001"
  }
}
```

## Impact Assessment
- Without registered root: UID collisions possible in multi-site PACS environments
- With test root `2.25`: Functionally correct, not suitable for production
- Risk Level: Medium — functional in controlled environments, non-conformant for IHE REM

## Reference
- DICOM PS 3.5 Section 9.1 (UID format requirements)
- DICOM PS 3.5 Annex B (Creating UIDs)
