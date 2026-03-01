using System;
using FluentAssertions;
using HnVue.Dicom.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Common;

/// <summary>
/// Tests for DicomErrorHandler following TDD methodology (RED-GREEN-REFACTOR).
/// SPEC-WORKFLOW-001 TASK-410: DICOM Error Handling
/// </summary>
public class DicomErrorHandlerTests
{
    private readonly Mock<ILogger<DicomErrorHandler>> _loggerMock;
    private readonly Mock<IDicomOperatorNotifier> _notifierMock;

    public DicomErrorHandlerTests()
    {
        _loggerMock = new Mock<ILogger<DicomErrorHandler>>();
        _notifierMock = new Mock<IDicomOperatorNotifier>();
    }

    [Fact]
    public void CategorizeError_ShouldReturnNetworkError_ForNetworkExceptions()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new System.Net.Sockets.SocketException(10054);

        // Act
        var category = handler.CategorizeError(exception);

        // Assert
        category.Should().Be(DicomErrorCategory.Network);
    }

    [Fact]
    public void CategorizeError_ShouldReturnTimeoutError_ForTimeoutExceptions()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new TimeoutException("Operation timed out");

        // Act
        var category = handler.CategorizeError(exception);

        // Assert
        category.Should().Be(DicomErrorCategory.Timeout);
    }

    [Fact]
    public void CategorizeError_ShouldReturnConfigurationError_ForArgumentException()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new ArgumentException("Invalid configuration");

        // Act
        var category = handler.CategorizeError(exception);

        // Assert
        category.Should().Be(DicomErrorCategory.Configuration);
    }

    [Fact]
    public void CategorizeError_ShouldReturnDicomError_ForDicomExceptions()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new HnVue.Dicom.Worklist.DicomWorklistException(0xA700, "DICOM failure");

        // Act
        var category = handler.CategorizeError(exception);

        // Assert
        category.Should().Be(DicomErrorCategory.Dicom);
    }

    [Fact]
    public void CategorizeError_ShouldReturnUnknownError_ForGenericExceptions()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var category = handler.CategorizeError(exception);

        // Assert
        category.Should().Be(DicomErrorCategory.Unknown);
    }

    [Fact]
    public async Task HandleErrorAsync_ShouldPublishNotification_WhenErrorIsCritical()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new DicomException("Critical DICOM error", isCritical: true);

        // Act
        await handler.HandleErrorAsync(exception, "Study001", CancellationToken.None);

        // Assert
        _notifierMock.Verify(
            x => x.NotifyErrorAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleErrorAsync_ShouldLogError_WhenErrorIsNotCritical()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new DicomException("Non-critical error", isCritical: false);

        // Act
        await handler.HandleErrorAsync(exception, "Study001", CancellationToken.None);

        // Assert
        _notifierMock.Verify(
            x => x.NotifyErrorAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ShouldDegradeGracefully_ShouldReturnTrue_ForNetworkErrors()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new System.Net.Sockets.SocketException(10054);

        // Act
        var shouldDegrade = handler.ShouldDegradeGracefully(exception);

        // Assert
        shouldDegrade.Should().BeTrue();
    }

    [Fact]
    public void ShouldDegradeGracefully_ShouldReturnFalse_ForConfigurationErrors()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new ArgumentException("Invalid configuration");

        // Act
        var shouldDegrade = handler.ShouldDegradeGracefully(exception);

        // Assert
        shouldDegrade.Should().BeFalse();
    }

    [Fact]
    public void ShouldDegradeGracefully_ShouldReturnTrue_ForTimeoutErrors()
    {
        // Arrange
        var handler = new DicomErrorHandler(_notifierMock.Object, _loggerMock.Object);
        var exception = new TimeoutException("Operation timed out");

        // Act
        var shouldDegrade = handler.ShouldDegradeGracefully(exception);

        // Assert
        shouldDegrade.Should().BeTrue();
    }
}
