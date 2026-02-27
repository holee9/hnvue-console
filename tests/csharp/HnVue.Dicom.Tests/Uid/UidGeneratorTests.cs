using FluentAssertions;
using HnVue.Dicom.Uid;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.Tests.Uid;

public class UidGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public UidGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTooLongUidRoot_ShouldThrowArgumentException()
    {
        // Arrange
        var longRoot = new string('1', 50) + "." + new string('2', 20);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new UidGenerator(longRoot, "DEVICE001"));
        exception.ParamName.Should().Be("orgUidRoot");
    }

    [Fact]
    public void Constructor_WithNullUidRoot_ShouldUseDefaultTestRoot()
    {
        // Arrange & Act
        var generator = new UidGenerator(null, "DEVICE001");

        // Assert - generator should work without throwing
        var uid = generator.GenerateSopInstanceUid();
        uid.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyDeviceSerial_ShouldUseDefault()
    {
        // Arrange & Act
        var generator = new UidGenerator("1.2.3.4.5", "");

        // Assert: generator should still produce valid UIDs when device serial is empty
        var uid = generator.GenerateSopInstanceUid();
        uid.Should().NotBeNullOrEmpty();
        generator.IsValidUid(uid).Should().BeTrue();
    }

    [Fact]
    public void GenerateStudyUid_ShouldReturnValidUid()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act
        var uid = generator.GenerateStudyUid();

        // Assert
        uid.Should().NotBeNullOrEmpty();
        uid.Should().StartWith("1.2.3.4.5");
        generator.IsValidUid(uid).Should().BeTrue();
    }

    [Fact]
    public void GenerateSeriesUid_ShouldReturnValidUid()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act
        var uid = generator.GenerateSeriesUid();

        // Assert
        uid.Should().NotBeNullOrEmpty();
        uid.Should().StartWith("1.2.3.4.5");
        generator.IsValidUid(uid).Should().BeTrue();
    }

    [Fact]
    public void GenerateSopInstanceUid_ShouldReturnValidUid()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act
        var uid = generator.GenerateSopInstanceUid();

        // Assert
        uid.Should().NotBeNullOrEmpty();
        uid.Should().StartWith("1.2.3.4.5");
        generator.IsValidUid(uid).Should().BeTrue();
    }

    [Fact]
    public void GenerateMppsUid_ShouldReturnValidUid()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act
        var uid = generator.GenerateMppsUid();

        // Assert
        uid.Should().NotBeNullOrEmpty();
        uid.Should().StartWith("1.2.3.4.5");
        generator.IsValidUid(uid).Should().BeTrue();
    }

    [Fact]
    public void GenerateMultipleUids_ShouldReturnUniqueValues()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");
        var uids = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            uids.Add(generator.GenerateSopInstanceUid());
        }

        // Assert
        uids.Count.Should().Be(100, "all UIDs should be unique");
    }

    [Fact]
    public void GenerateUid_WhenCalledFromMultipleThreads_ShouldReturnUniqueValues()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");
        var uids = new HashSet<string>();
        var lockObj = new object();
        const int threadCount = 10;
        const int uidsPerThread = 10;

        // Act
        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < uidsPerThread; j++)
                {
                    var uid = generator.GenerateSopInstanceUid();
                    lock (lockObj)
                    {
                        uids.Add(uid);
                    }
                }
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert
        uids.Count.Should().Be(threadCount * uidsPerThread, "all UIDs should be unique across threads");
    }

    [Fact]
    public void IsValidUid_WithValidUid_ShouldReturnTrue()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act & Assert
        generator.IsValidUid("1.2.3.4.5").Should().BeTrue();
        generator.IsValidUid("1.2.840.10008.1.1").Should().BeTrue();
        generator.IsValidUid("2.25.123456789012345678901234567890123456789").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidUid_WithNullOrEmptyUid_ShouldReturnFalse(string? uid)
    {
        // Arrange
        var generator = new UidGenerator();

        // Act
        var result = generator.IsValidUid(uid!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUid_WithTooLongUid_ShouldReturnFalse()
    {
        // Arrange
        var generator = new UidGenerator();
        var longUid = "1.2." + new string('3', 100);

        // Act
        var result = generator.IsValidUid(longUid);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUid_WithLeadingDot_ShouldReturnFalse()
    {
        // Arrange
        var generator = new UidGenerator();

        // Act
        var result = generator.IsValidUid(".1.2.3.4");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUid_WithTrailingDot_ShouldReturnFalse()
    {
        // Arrange
        var generator = new UidGenerator();

        // Act
        var result = generator.IsValidUid("1.2.3.4.");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUid_WithConsecutiveDots_ShouldReturnFalse()
    {
        // Arrange
        var generator = new UidGenerator();

        // Act
        var result = generator.IsValidUid("1.2..3.4");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUid_WithNonDigitCharacters_ShouldReturnFalse()
    {
        // Arrange
        var generator = new UidGenerator();

        // Act & Assert
        generator.IsValidUid("1.2.3.a.4").Should().BeFalse();
        generator.IsValidUid("1.2.3-4.5").Should().BeFalse();
        generator.IsValidUid("1.2.3 4.5").Should().BeFalse();
    }

    [Fact]
    public void GeneratedUid_ShouldNotExceed64Characters()
    {
        // Arrange
        var generator = new UidGenerator("1.2.3.4.5", "DEVICE001");

        // Act
        var uid = generator.GenerateSopInstanceUid();

        // Assert
        uid.Length.Should().BeLessOrEqualTo(64);
    }

    [Fact]
    public void GeneratedUid_ShouldContainDeviceSerial()
    {
        // Arrange: use numeric-only serial since DICOM UIDs must contain only digits and dots
        var generator = new UidGenerator("1.2.3.4.5", "12345");

        // Act
        var uid = generator.GenerateSopInstanceUid();

        // Assert
        uid.Should().Contain("12345");
        generator.IsValidUid(uid).Should().BeTrue();
    }
}
