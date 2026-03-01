using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// MVVM compliance validation tests.
/// SPEC-UI-001: FR-UI-00 MVVM architecture requirement.
/// </summary>
public class MvvmComplianceTests
{
    private readonly ITestOutputHelper _output;

    public MvvmComplianceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void All_ViewModels_Are_MVVM_Compliant()
    {
        // Arrange
        var assembly = typeof(ViewModelBase).Assembly;

        // Act
        var report = MvvmComplianceChecker.ScanAssembly(assembly);

        // Output violations for debugging
        if (!report.IsFullyCompliant)
        {
            _output.WriteLine(report.GetSummary());
            foreach (var violation in report.Violations)
            {
                _output.WriteLine(violation);
            }
        }

        // Assert
        Assert.True(report.IsFullyCompliant,
            $"MVVM compliance violations found: {report.GetSummary()}");
    }

    [Fact]
    public void ViewModelBase_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(ViewModelBase);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void ShellViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(ShellViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void PatientViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(PatientViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void WorklistViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(WorklistViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void AcquisitionViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(AcquisitionViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void ImageReviewViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(ImageReviewViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void SystemStatusViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(SystemStatusViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void ConfigurationViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(ConfigurationViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Fact]
    public void AuditLogViewModel_Does_Not_Reference_System_Windows()
    {
        // Arrange
        var viewModelType = typeof(AuditLogViewModel);

        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant);
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(typeof(AECViewModel))]
    [InlineData(typeof(DoseViewModel))]
    [InlineData(typeof(ExposureParameterViewModel))]
    [InlineData(typeof(ProtocolViewModel))]
    [InlineData(typeof(PatientEditViewModel))]
    [InlineData(typeof(PatientRegistrationViewModel))]
    public void ViewModels_Do_Not_Reference_System_Windows(Type viewModelType)
    {
        // Act
        var (isCompliant, violations) = MvvmComplianceChecker.CheckCompliance(viewModelType);

        // Assert
        Assert.True(isCompliant,
            $"{viewModelType.Name} has MVVM violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void All_ViewModels_Have_Correct_Naming_Convention()
    {
        // Arrange
        var assembly = typeof(ViewModelBase).Assembly;
        var viewModelTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ViewModelBase).IsAssignableFrom(t))
            .ToList();

        // Act & Assert
        foreach (var type in viewModelTypes)
        {
            // All ViewModels should end with "ViewModel" except ViewModelBase itself
            if (type != typeof(ViewModelBase))
            {
                Assert.True(type.Name.EndsWith("ViewModel"),
                    $"{type.Name} does not follow ViewModel naming convention");
            }
        }
    }
}
