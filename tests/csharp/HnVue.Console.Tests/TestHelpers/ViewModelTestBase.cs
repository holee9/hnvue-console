using Moq;
using Moq.Language;
using Moq.Language.Flow;
using System.ComponentModel;

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
    protected static ISetup<T, Task<TResult>> SetupSuccessfulTask<T, TResult>(
        this Mock<T> mock,
        Expression<Func<T, Task<TResult>>> expression,
        TResult result) where T : class
    {
        return mock.Setup(expression).Returns(Task.FromResult(result));
    }

    /// <summary>
    /// Sets up a task-returning method that completes successfully.
    /// </summary>
    protected static ISetup<T, Task> SetupSuccessfulTask<T>(
        this Mock<T> mock,
        Expression<Func<T, Task>> expression) where T : class
    {
        return mock.Setup(expression).Returns(Task.CompletedTask);
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
            DateOfBirth = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Sex = "M",
            AccessionNumber = "ACC001"
        };
    }

    /// <summary>
    /// Creates a test worklist item.
    /// </summary>
    protected static HnVue.Console.Models.WorklistItem CreateTestWorklistItem(string id = "WL001")
    {
        return new HnVue.Console.Models.WorklistItem
        {
            StudyId = id,
            PatientId = "PT001",
            PatientName = "Test Patient",
            ProcedureName = "Chest X-Ray",
            ScheduledTime = DateTimeOffset.Now,
            Priority = HnVue.Console.Models.StudyPriority.Routine,
            Status = HnVue.Console.Models.WorklistStatus.Pending
        };
    }

    /// <summary>
    /// Creates a test protocol.
    /// </summary>
    protected static HnVue.Console.Models.Protocol CreateTestProtocol(string id = "PROTO001")
    {
        return new HnVue.Console.Models.Protocol
        {
            ProtocolId = id,
            ProtocolName = "Chest PA",
            BodyPart = "CHEST",
            ViewPosition = "PA",
            Kv = 120,
            Mas = 5.0m,
            FocalSpotSize = HnVue.Console.Models.FocalSpotSize.Large,
            GridRequired = true
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
