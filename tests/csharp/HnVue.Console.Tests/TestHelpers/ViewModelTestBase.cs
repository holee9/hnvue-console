using Moq;
using System.ComponentModel;
using System.Linq.Expressions;

namespace HnVue.Console.Tests.TestHelpers;

/// <summary>
/// Base class for ViewModel tests with common setup helpers.
/// SPEC-UI-001: Test infrastructure foundation.
/// </summary>
public abstract class ViewModelTestBase : IDisposable
{
    protected readonly CancellationToken TestCancellationToken = CancellationToken.None;

    /// <summary>
    /// Clean up any test resources.
    /// </summary>
    public virtual void Dispose()
    {
        // Override in derived classes if needed
    }

    /// <summary>
    /// Creates a mock service with standard setup.
    /// </summary>
    protected static Mock<T> CreateMockService<T>() where T : class
    {
        return new Mock<T>(MockBehavior.Strict);
    }

    /// <summary>
    /// Creates a mock service with loose behavior (useful for non-critical dependencies).
    /// </summary>
    protected static Mock<T> CreateLooseMockService<T>() where T : class
    {
        return new Mock<T>(MockBehavior.Loose);
    }

    /// <summary>
    /// Sets up a task-returning method that completes successfully.
    /// </summary>
    protected static void SetupSuccessfulTask<T, TResult>(
        Mock<T> mock,
        Expression<Func<T, Task<TResult>>> expression,
        TResult result) where T : class
    {
        mock.Setup(expression).ReturnsAsync(result);
    }

    /// <summary>
    /// Sets up a task-returning method that completes successfully.
    /// </summary>
    protected static void SetupSuccessfulTask<T>(
        Mock<T> mock,
        Expression<Func<T, Task>> expression) where T : class
    {
        mock.Setup(expression).Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Creates a test patient.
    /// </summary>
    protected static HnVue.Console.Models.Patient CreateTestPatient(string id = "PT001")
    {
        return new HnVue.Console.Models.Patient
        {
            PatientId = id,
            PatientName = "Test Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = HnVue.Console.Models.Sex.Male,
            AccessionNumber = "ACC001"
        };
    }

    /// <summary>
    /// Creates a test worklist item using actual WorklistItem fields.
    /// </summary>
    protected static HnVue.Console.Models.WorklistItem CreateTestWorklistItem(string id = "WL001")
    {
        return new HnVue.Console.Models.WorklistItem
        {
            ProcedureId = id,
            PatientId = "PT001",
            PatientName = "Test Patient",
            AccessionNumber = "ACC001",
            ScheduledProcedureStepDescription = "Chest X-Ray",
            ScheduledDateTime = DateTimeOffset.Now,
            BodyPart = "CHEST",
            Projection = "PA",
            Status = HnVue.Console.Models.WorklistStatus.Scheduled
        };
    }

    /// <summary>
    /// Creates a test protocol preset using actual ProtocolPreset fields.
    /// </summary>
    protected static HnVue.Console.Models.ProtocolPreset CreateTestProtocol(string id = "PROTO001")
    {
        return new HnVue.Console.Models.ProtocolPreset
        {
            ProtocolId = id,
            BodyPartCode = "CHEST",
            ProjectionCode = "PA",
            DefaultExposure = new HnVue.Console.Models.ExposureParameters
            {
                KVp = 120,
                MA = 200,
                ExposureTimeMs = 25,
                SourceImageDistanceCm = 180,
                FocalSpotSize = HnVue.Console.Models.FocalSpotSize.Large
            }
        };
    }

    /// <summary>
    /// Verifies that INotifyPropertyChanged was raised for a specific property.
    /// </summary>
    protected static bool VerifyPropertyChanged(INotifyPropertyChanged viewModel, string propertyName, Action action)
    {
        var propertyChanged = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == propertyName)
                propertyChanged = true;
        };

        action();

        return propertyChanged;
    }

    /// <summary>
    /// Gets all property names that were changed during an action.
    /// </summary>
    protected static IList<string> GetChangedProperties(INotifyPropertyChanged viewModel, Action action)
    {
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (s, e) =>
        {
            changedProperties.Add(e.PropertyName ?? "");
        };

        action();

        return changedProperties;
    }
}
