namespace HnVue.Workflow.Tests.StateMachine;

using System;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for WorkflowState enum and related functionality.
/// Tests state enumeration values and state transitions as defined in SPEC-WORKFLOW-001 Section 2.2.
/// </summary>
public class WorkflowStateTests
{
    [Fact]
    public void State_Idle_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.Idle;

        // Assert
        state.Should().Be(WorkflowState.Idle);
        state.ToString().Should().Be("Idle");
    }

    [Fact]
    public void State_WorklistSync_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.WorklistSync;

        // Assert
        state.Should().Be(WorkflowState.WorklistSync);
        state.ToString().Should().Be("WorklistSync");
    }

    [Fact]
    public void State_PatientSelect_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.PatientSelect;

        // Assert
        state.Should().Be(WorkflowState.PatientSelect);
        state.ToString().Should().Be("PatientSelect");
    }

    [Fact]
    public void State_ProtocolSelect_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.ProtocolSelect;

        // Assert
        state.Should().Be(WorkflowState.ProtocolSelect);
        state.ToString().Should().Be("ProtocolSelect");
    }

    [Fact]
    public void State_PositionAndPreview_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.PositionAndPreview;

        // Assert
        state.Should().Be(WorkflowState.PositionAndPreview);
        state.ToString().Should().Be("PositionAndPreview");
    }

    [Fact]
    public void State_ExposureTrigger_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.ExposureTrigger;

        // Assert
        state.Should().Be(WorkflowState.ExposureTrigger);
        state.ToString().Should().Be("ExposureTrigger");
    }

    [Fact]
    public void State_QcReview_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.QcReview;

        // Assert
        state.Should().Be(WorkflowState.QcReview);
        state.ToString().Should().Be("QcReview");
    }

    [Fact]
    public void State_MppsComplete_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.MppsComplete;

        // Assert
        state.Should().Be(WorkflowState.MppsComplete);
        state.ToString().Should().Be("MppsComplete");
    }

    [Fact]
    public void State_PacsExport_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.PacsExport;

        // Assert
        state.Should().Be(WorkflowState.PacsExport);
        state.ToString().Should().Be("PacsExport");
    }

    [Fact]
    public void State_RejectRetake_ShouldHaveExpectedValue()
    {
        // Arrange & Act
        var state = WorkflowState.RejectRetake;

        // Assert
        state.Should().Be(WorkflowState.RejectRetake);
        state.ToString().Should().Be("RejectRetake");
    }

    [Fact]
    public void AllStates_ShouldBeDistinct()
    {
        // Arrange
        var allStates = new[]
        {
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            WorkflowState.MppsComplete,
            WorkflowState.PacsExport,
            WorkflowState.RejectRetake
        };

        // Act & Assert
        allStates.Distinct().Count().Should().Be(10, "all workflow states should be distinct");
    }
}
