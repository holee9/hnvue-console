using FluentAssertions;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HnVue.Dicom.Tests.Configuration;

public class DicomServiceOptionsTests
{
    [Fact]
    public void DefaultOptions_ShouldHaveSensibleDefaults()
    {
        // Arrange & Act
        var options = new DicomServiceOptions();

        // Assert
        options.CallingAeTitle.Should().Be("HNVUE_CONSOLE");
        options.UidRoot.Should().Be("2.25");
        options.DeviceSerial.Should().Be("HNVUE001");
        options.MinimumLogLevel.Should().Be(LogLevel.Information);
        options.AssociationPool.Should().NotBeNull();
        options.RetryQueue.Should().NotBeNull();
        options.Tls.Should().NotBeNull();
        options.Timeouts.Should().NotBeNull();
        options.StorageDestinations.Should().NotBeNull();
    }

    [Fact]
    public void AssociationPoolOptions_DefaultValues_ShouldBeReasonable()
    {
        // Arrange & Act
        var options = new AssociationPoolOptions();

        // Assert
        options.MaxSize.Should().Be(4);
        options.AcquisitionTimeoutMs.Should().Be(30000);
        options.IdleTimeoutMs.Should().Be(30000);
    }

    [Fact]
    public void RetryQueueOptions_DefaultValues_ShouldProvideResilience()
    {
        // Arrange & Act
        var options = new RetryQueueOptions();

        // Assert
        options.MaxRetryCount.Should().Be(5);
        options.InitialIntervalSeconds.Should().Be(30);
        options.BackoffMultiplier.Should().Be(2.0);
        options.MaxIntervalSeconds.Should().Be(3600);
        options.StoragePath.Should().Be("./data/dicom-queue");
    }

    [Fact]
    public void TlsOptions_DefaultValues_ShouldBeDisabled()
    {
        // Arrange & Act
        var options = new TlsOptions();

        // Assert
        options.Enabled.Should().BeFalse();
        options.MinVersion.Should().Be(TlsVersion.Tls12);
        options.CertificateSource.Should().Be(CertificateSource.WindowsCertificateStore);
        options.ClientCertificateEnabled.Should().BeFalse();
    }

    [Fact]
    public void TimeoutOptions_DefaultValues_ShouldAllowOperationsToComplete()
    {
        // Arrange & Act
        var options = new TimeoutOptions();

        // Assert
        options.AssociationRequestMs.Should().Be(5000);
        options.DimseOperationMs.Should().Be(30000);
        options.SocketReceiveMs.Should().Be(60000);
        options.SocketSendMs.Should().Be(60000);
        options.StorageCommitmentWaitMs.Should().Be(300000);
    }

    [Fact]
    public void DicomDestination_DefaultValues_ShouldNotCauseNullReference()
    {
        // Arrange & Act
        var destination = new DicomDestination();

        // Assert - should not throw
        destination.AeTitle.Should().BeEmpty();
        destination.Host.Should().BeEmpty();
        destination.Port.Should().Be(104);
        destination.TlsEnabled.Should().BeNull();
    }

    [Fact]
    public void StorageDestinations_CanAddMultipleDestinations()
    {
        // Arrange
        var options = new DicomServiceOptions();

        // Act
        options.StorageDestinations.Add(new DicomDestination
        {
            AeTitle = "PACS1",
            Host = "pacs1.hospital.local",
            Port = 104
        });
        options.StorageDestinations.Add(new DicomDestination
        {
            AeTitle = "PACS2",
            Host = "pacs2.hospital.local",
            Port = 104
        });

        // Assert
        options.StorageDestinations.Count.Should().Be(2);
        options.StorageDestinations[0].AeTitle.Should().Be("PACS1");
        options.StorageDestinations[1].AeTitle.Should().Be("PACS2");
    }

    [Theory]
    [InlineData("1.2.840.10008.1.1")]
    [InlineData("2.25")]
    [InlineData("2.25.123456789012345678901234567890123456789")]
    public void UidRoot_WithValidValues_ShouldBeAccepted(string uidRoot)
    {
        // Arrange & Act
        var options = new DicomServiceOptions { UidRoot = uidRoot };

        // Assert
        options.UidRoot.Should().Be(uidRoot);
    }

    [Fact]
    public void CallingAeTitle_CanBeCustomized()
    {
        // Arrange & Act
        var options = new DicomServiceOptions { CallingAeTitle = "CUSTOM_DEVICE" };

        // Assert
        options.CallingAeTitle.Should().Be("CUSTOM_DEVICE");
    }
}
