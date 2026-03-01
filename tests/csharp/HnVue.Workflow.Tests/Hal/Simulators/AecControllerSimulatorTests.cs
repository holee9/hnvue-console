namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using Xunit;

/// <summary>
/// Unit tests for AecControllerSimulator.
/// SPEC-WORKFLOW-001 TASK-404: IAecController Simulator implementation
/// </summary>
public class AecControllerSimulatorTests
{
    /// <summary>
    /// Test that simulator initializes in NotConfigured state.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SetsStateToNotConfigured()
    {
        // Arrange
        var simulator = new AecControllerSimulator();

        // Act
        await simulator.InitializeAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.NotConfigured);
        status.IsReady.Should().BeFalse();
        status.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Test that SetAecParametersAsync transitions to Ready state.
    /// </summary>
    [Fact]
    public async Task SetAecParametersAsync_WhenValidTransitionsToReady()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Assert
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.Ready);
        status.IsReady.Should().BeTrue();
        status.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Test that SetAecParametersAsync validates chamber selection (1-3).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task SetAecParametersAsync_InvalidChamberThrows(int invalidChamber)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = invalidChamber,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => simulator.SetAecParametersAsync(parameters, CancellationToken.None));
    }

    /// <summary>
    /// Test that valid chamber selections are accepted.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SetAecParametersAsync_ValidChambersAreAccepted(int chamber)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = chamber,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act & Assert - Should not throw
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.Ready);
    }

    /// <summary>
    /// Test that density index range is validated.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task SetAecParametersAsync_InvalidDensityIndexThrows(int invalidDensity)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = invalidDensity,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => simulator.SetAecParametersAsync(parameters, CancellationToken.None));
    }

    /// <summary>
    /// Test that valid density index values are accepted.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SetAecParametersAsync_ValidDensityIndexAccepted(int density)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = density,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act & Assert - Should not throw
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.Ready);
    }

    /// <summary>
    /// Test that body part thickness is validated.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]  // Max thickness is 500mm
    public async Task SetAecParametersAsync_InvalidThicknessThrows(int invalidThickness)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = invalidThickness,
            KvPriority = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => simulator.SetAecParametersAsync(parameters, CancellationToken.None));
    }

    /// <summary>
    /// Test that GetRecommendedParamsAsync returns reasonable parameters.
    /// </summary>
    [Theory]
    [InlineData(100)]  // Thin body part
    [InlineData(200)]  // Medium body part
    [InlineData(300)]  // Thick body part
    public async Task GetRecommendedParamsAsync_ReturnsValidParameters(int thickness)
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var parameters = await simulator.GetRecommendedParamsAsync(thickness, CancellationToken.None);

        // Assert - Parameters should be within clinical ranges
        parameters.Kv.Should().BeGreaterOrEqualTo(40);
        parameters.Kv.Should().BeLessOrEqualTo(150);
        parameters.Ma.Should().BeGreaterOrEqualTo(10);
        parameters.Ma.Should().BeLessOrEqualTo(500);
        parameters.Ms.Should().BeGreaterOrEqualTo(10);
        parameters.Ms.Should().BeLessOrEqualTo(3000);
    }

    /// <summary>
    /// Test that thicker body parts get higher mAs recommendations.
    /// </summary>
    [Fact]
    public async Task GetRecommendedParamsAsync_ThickerBodyPartGetsHigherMas()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var thinParams = await simulator.GetRecommendedParamsAsync(100, CancellationToken.None);
        var thickParams = await simulator.GetRecommendedParamsAsync(300, CancellationToken.None);

        // Assert - Thicker body part should require higher exposure
        var thinMas = thinParams.Kv * thinParams.Ma * thinParams.Ms / 1000.0;
        var thickMas = thickParams.Kv * thickParams.Ma * thickParams.Ms / 1000.0;
        thickMas.Should().BeGreaterThan(thinMas);
    }

    /// <summary>
    /// Test that GetAecReadinessAsync returns NotReady when not configured.
    /// </summary>
    [Fact]
    public async Task GetAecReadinessAsync_WhenNotConfiguredReturnsNotReady()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);

        // Assert
        status.State.Should().Be(AecState.NotConfigured);
        status.IsReady.Should().BeFalse();
    }

    /// <summary>
    /// Test that GetAecReadinessAsync returns Ready after configuration.
    /// </summary>
    [Fact]
    public async Task GetAecReadinessAsync_WhenConfiguredReturnsReady()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Act
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);

        // Assert
        status.State.Should().Be(AecState.Ready);
        status.IsReady.Should().BeTrue();
    }

    /// <summary>
    /// Test that simulator can be reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ResetsToNotConfiguredState()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Act
        await simulator.ResetAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.NotConfigured);
        status.IsReady.Should().BeFalse();
    }

    /// <summary>
    /// Test that AEC can be disabled.
    /// </summary>
    [Fact]
    public async Task SetAecParametersAsync_CanDisableAec()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = false,  // AEC disabled
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Assert
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        // When AEC is disabled, system should still be ready
        status.State.Should().Be(AecState.Ready);
    }

    /// <summary>
    /// Test that kV priority mode is supported.
    /// </summary>
    [Fact]
    public async Task SetAecParametersAsync_SupportsKvPriorityMode()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = true
        };

        // Act
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Assert
        var status = await simulator.GetAecReadinessAsync(CancellationToken.None);
        status.State.Should().Be(AecState.Ready);
    }

    /// <summary>
    /// Test that last configured parameters can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetLastConfiguredParameters_ReturnsConfiguredValues()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 2,
            DensityIndex = 1,
            BodyPartThickness = 250,
            KvPriority = true
        };
        await simulator.SetAecParametersAsync(parameters, CancellationToken.None);

        // Act
        var lastParams = simulator.GetLastConfiguredParameters();

        // Assert
        lastParams.Should().BeEquivalentTo(parameters);
    }

    /// <summary>
    /// Test that cancellation token works.
    /// </summary>
    [Fact]
    public async Task SetAecParametersAsync_CancellationTokenCancelsOperation()
    {
        // Arrange
        var simulator = new AecControllerSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => simulator.SetAecParametersAsync(parameters, cts.Token));
    }
}
