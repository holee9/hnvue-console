using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for PatientViewModel.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public class PatientViewModelTests : ViewModelTestBase
{
    private readonly Mock<IPatientService> _mockPatientService;

    public PatientViewModelTests()
    {
        _mockPatientService = CreateMockService<IPatientService>();
    }

    [Fact]
    public void Constructor_Initializes_Collections()
    {
        // Arrange & Act
        var viewModel = new PatientViewModel(_mockPatientService.Object);

        // Assert
        Assert.NotNull(viewModel.Patients);
        Assert.Empty(viewModel.Patients);
        Assert.Null(viewModel.SelectedPatient);
        Assert.False(viewModel.IsLoading);
        Assert.Empty(viewModel.SearchQuery);
    }

    [Fact]
    public async Task SearchCommand_Loads_Patients()
    {
        // Arrange
        var searchResult = new PatientSearchResult
        {
            Patients = new List<Patient>
            {
                CreateTestPatient("PT001"),
                CreateTestPatient("PT002")
            },
            TotalCount = 2
        };

        _mockPatientService
            .Setup(s => s.SearchPatientsAsync(
                It.Is<PatientSearchRequest>(r => r.Query == "Test"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        var viewModel = new PatientViewModel(_mockPatientService.Object);
        viewModel.SearchQuery = "Test";

        // Act
        viewModel.SearchCommand.Execute(null);
        await Task.Delay(100); // Allow async to complete

        // Assert
        Assert.Equal(2, viewModel.Patients.Count);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void SearchCommand_Cannot_Execute_When_Query_Is_Empty()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);
        viewModel.SearchQuery = "";

        // Act & Assert
        Assert.False(viewModel.SearchCommand.CanExecute(null));
    }

    [Fact]
    public void SearchCommand_Cannot_Execute_While_Loading()
    {
        // Arrange
        var tcs = new TaskCompletionSource<PatientSearchResult>();
        _mockPatientService
            .Setup(s => s.SearchPatientsAsync(It.IsAny<PatientSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var viewModel = new PatientViewModel(_mockPatientService.Object);
        viewModel.SearchQuery = "Test";

        // Start search (don't await)
        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        // Assert
        Assert.False(viewModel.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditPatientCommand_Can_Execute_With_Patient()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);
        var patient = CreateTestPatient();

        // Act
        var canExecute = viewModel.EditPatientCommand.CanExecute(patient);

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void EditPatientCommand_Cannot_Execute_Without_Patient()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);

        // Act & Assert
        Assert.False(viewModel.EditPatientCommand.CanExecute(null));
        Assert.False(viewModel.EditPatientCommand.CanExecute("not a patient"));
    }

    [Fact]
    public void SelectPatientCommand_Sets_SelectedPatient()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);
        var patient = CreateTestPatient();

        // Act
        viewModel.SelectPatientCommand.Execute(patient);

        // Assert
        Assert.Equal(patient, viewModel.SelectedPatient);
    }

    [Fact]
    public async Task SearchQuery_Raises_PropertyChanged()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.SearchQuery = "Test");

        // Assert
        Assert.Contains("SearchQuery", changedProperties);
    }

    [Fact]
    public async Task SelectedPatient_Raises_PropertyChanged()
    {
        // Arrange
        var viewModel = new PatientViewModel(_mockPatientService.Object);
        var patient = CreateTestPatient();
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.SelectedPatient = patient);

        // Assert
        Assert.Contains("SelectedPatient", changedProperties);
    }
}
