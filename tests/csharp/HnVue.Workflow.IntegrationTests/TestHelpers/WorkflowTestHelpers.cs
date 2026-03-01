namespace HnVue.Workflow.IntegrationTests.TestHelpers;

using HnVue.Workflow.Study;

/// <summary>
/// Shared test helpers for workflow integration tests.
/// @MX:NOTE: Centralized helper methods for common test operations
/// </summary>
public static class WorkflowTestHelpers
{
    /// <summary>
    /// Creates a test PatientInfo with simplified parameters.
    /// @MX:NOTE: Simplifies PatientInfo creation with test data
    /// </summary>
    public static PatientInfo CreateTestPatientInfo(
        string patientId,
        string patientName,
        int birthYear,
        char sex,
        bool isEmergency = false)
    {
        return new PatientInfo
        {
            PatientID = patientId,
            PatientName = patientName,
            PatientBirthDate = new DateOnly(birthYear, 1, 1),
            PatientSex = sex,
            IsEmergency = isEmergency
        };
    }
}
