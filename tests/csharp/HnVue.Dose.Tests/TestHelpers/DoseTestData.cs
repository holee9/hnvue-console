namespace HnVue.Dose.Tests.TestHelpers;

/// <summary>
/// Test data factory for dose-related tests.
/// Provides reusable test data following SPEC-DOSE-001 specifications.
/// </summary>
public static class DoseTestData
{
    /// <summary>
    /// Standard test kVp values covering typical X-ray range.
    /// </summary>
    public static readonly decimal[] TypicalKvpValues = { 50m, 60m, 70m, 80m, 100m, 120m, 150m };

    /// <summary>
    /// Standard test mAs values.
    /// </summary>
    public static readonly decimal[] TypicalMasValues = { 1m, 2.5m, 5m, 10m, 20m, 50m, 100m, 200m };

    /// <summary>
    /// Standard SID (Source-to-Image Distance) values in mm.
    /// </summary>
    public static readonly decimal[] TypicalSidValues = { 1000m, 1100m, 1200m, 1500m, 1800m };

    /// <summary>
    /// Standard field sizes (width x height in mm).
    /// </summary>
    public static readonly (decimal Width, decimal Height)[] TypicalFieldSizes =
    [
        (200m, 200m),  // Small field
        (300m, 400m),  // Medium field
        (350m, 430m),  // Standard detector size
        (400m, 400m)   // Large field
    ];

    /// <summary>
    /// Standard filtration materials per DICOM CID 10006.
    /// </summary>
    public static readonly string[] FilterMaterials =
    [
        "AL",  // Aluminum
        "CU",  // Copper
        "MO",  // Molybdenum
        "RH"   // Rhodium
    ];

    /// <summary>
    /// Standard filtration thickness values in mm.
    /// </summary>
    public static readonly decimal[] TypicalFilterThicknesses = { 0m, 1m, 2m, 3m, 5m };

    /// <summary>
    /// Valid DICOM UIDs for testing.
    /// </summary>
    public static class Uids
    {
        public const string StudyInstanceUid = "1.2.3.4.5.100";
        public const string PatientId = "TEST_PATIENT_001";
        public const string AccessionNumber = "ACC_20250228_001";
        public const string IrradiationEventUidFormat = "1.2.3.4.5.700.{0}";
    }

    /// <summary>
    /// DAP calculation reference values for validation.
    /// Based on typical X-ray output coefficients.
    /// </summary>
    public static class DapReference
    {
        /// <summary>
        /// Reference k_factor for typical X-ray tube (kV^-n × mAs^-1 × cm²).
        /// This is a test value; actual calibration must be done per tube.
        /// </summary>
        public const decimal KFactor = 0.0001m;

        /// <summary>
        /// Reference voltage exponent for general radiography.
        /// </summary>
        public const decimal VoltageExponent = 2.5m;

        /// <summary>
        /// Reference calibration coefficient (dimensionless).
        /// </summary>
        public const decimal CalibrationCoefficient = 1.0m;

        /// <summary>
        /// Acceptable tolerance for DAP calculation accuracy (%).
        /// Per SPEC-DOSE-001 NFR-DOSE-03: ±5% of measured DAP.
        /// </summary>
        public const decimal AccuracyTolerancePercent = 5m;
    }

    /// <summary>
    /// DRL (Diagnostic Reference Level) test values per examination type.
    /// Based on national guidelines (factory defaults).
    /// </summary>
    public static class DrlThresholds
    {
        public const decimal ChestPA = 0.25m;      // mGy·cm² -> Gy·cm²: 0.00025 Gy·cm²
        public const decimal ChestLateral = 0.50m; // 0.00050 Gy·cm²
        public const decimal Abdomen = 1.50m;      // 0.00150 Gy·cm²
        public const decimal Pelvis = 2.00m;       // 0.00200 Gy·cm²
        public const decimal Skull = 1.00m;        // 0.00100 Gy·cm²
    }

    /// <summary>
    /// Creates a valid irradiation event UID for testing.
    /// </summary>
    public static string CreateIrradiationEventUid(int index = 0)
    {
        return string.Format(Uids.IrradiationEventUidFormat, index);
    }
}
