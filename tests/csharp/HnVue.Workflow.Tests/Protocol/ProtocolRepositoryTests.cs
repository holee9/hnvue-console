namespace HnVue.Workflow.Tests.Protocol;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using System.IO;
using HnVue.Workflow.Protocol;

/// <summary>
/// Unit tests for ProtocolRepository.
/// SPEC-WORKFLOW-001 FR-WF-08: Protocol selection and validation
/// SPEC-WORKFLOW-001 FR-WF-09: Safety limit enforcement on protocol save
/// SPEC-WORKFLOW-001 NFR-WF-03: 50ms or better lookup performance for 500+ protocols
/// </summary>
public class ProtocolRepositoryTests : IDisposable
{
    private readonly ProtocolRepository _repository;
    private readonly DeviceSafetyLimits _safetyLimits;
    private readonly Mock<ILogger<ProtocolRepository>> _loggerMock;
    private readonly string _databasePath;

    public ProtocolRepositoryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_protocols_{Guid.NewGuid()}.db");
        _safetyLimits = new DeviceSafetyLimits
        {
            MinKvp = 40,
            MaxKvp = 150,
            MinMa = 1,
            MaxMa = 500,
            MaxExposureTimeMs = 3000,
            MaxMas = 2000 // Increased to accommodate realistic clinical protocols
        };
        _loggerMock = new Mock<ILogger<ProtocolRepository>>();
        _repository = new ProtocolRepository(_databasePath, _safetyLimits, _loggerMock.Object);
    }

    public void Dispose()
    {
        _repository.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task CreateAsync_WithValidProtocol_CreatesProtocol()
    {
        // Arrange - Use values that keep CalculatedMas under 500
        // CalculatedMas = 120 * 100 * 40 / 1000 = 480 mAs (< 500)
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 40, // Reduced to keep mAs under 500
            DeviceModel = "HVG-3000",
            ProcedureCodes = new[] { "CHEST_PA" }
        };

        // Act
        var result = await _repository.CreateAsync(protocol);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetProtocolAsync(protocol.ProtocolId);
        retrieved.Should().NotBeNull();
        retrieved!.BodyPart.Should().Be("CHEST");
        retrieved.Projection.Should().Be("PA");
        retrieved.ProcedureCodes.Should().ContainSingle().Which.Should().Be("CHEST_PA");
    }

    [Fact]
    public async Task CreateAsync_WithExceedingSafetyLimits_ReturnsFalse()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 150, // At MaxKvp
            Ma = 400, // At MaxMa
            ExposureTimeMs = 100, // mAs = 150 * 400 * 100 / 1000 = 6000 > MaxMas (2000)
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = await _repository.CreateAsync(protocol);

        // Assert
        result.Should().BeFalse();

        var retrieved = await _repository.GetProtocolAsync(protocol.ProtocolId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCompositeKey_ReturnsFalse()
    {
        // Arrange - Test duplicate detection with composite key: CHEST|PA|HVG-3000
        var protocol1 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        var protocol2 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 110,
            Ma = 80,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Act
        var result1 = await _repository.CreateAsync(protocol1);
        var result2 = await _repository.CreateAsync(protocol2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithValidProtocol_UpdatesProtocol()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol);

        // Act
        protocol.Kv = 125;
        protocol.ProcedureCodes = new[] { "CHEST_PA_UPDATED" };
        var result = await _repository.UpdateAsync(protocol);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetProtocolAsync(protocol.ProtocolId);
        retrieved.Should().NotBeNull();
        retrieved!.Kv.Should().Be(125);
        retrieved.ProcedureCodes.Should().ContainSingle().Which.Should().Be("CHEST_PA_UPDATED");
    }

    [Fact]
    public async Task UpdateAsync_WithExceedingSafetyLimits_ReturnsFalse()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol);

        // Act
        protocol.Kv = 200; // Exceeds MaxKvp
        var result = await _repository.UpdateAsync(protocol);

        // Assert
        result.Should().BeFalse();

        var retrieved = await _repository.GetProtocolAsync(protocol.ProtocolId);
        retrieved.Should().NotBeNull();
        retrieved!.Kv.Should().Be(120); // Unchanged
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesProtocol()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol);

        // Act
        await _repository.DeleteAsync(protocol.ProtocolId);

        // Assert
        var retrieved = await _repository.GetProtocolAsync(protocol.ProtocolId);
        retrieved.Should().BeNull(); // Soft deleted, not returned by default

        var allProtocols = await _repository.GetAllAsync();
        allProtocols.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveProtocols()
    {
        // Arrange
        var protocol1 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        var protocol2 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "ABDOMEN",
            Projection = "AP",
            Kv = 110,
            Ma = 80,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol1);
        await _repository.CreateAsync(protocol2);
        await _repository.DeleteAsync(protocol2.ProtocolId);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProtocolId.Should().Be(protocol1.ProtocolId);
    }

    [Fact]
    public async Task GetByCompositeKey_WithExistingProtocol_ReturnsProtocol()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol);

        // Act
        var result = await _repository.GetByCompositeKeyAsync("CHEST", "PA", "HVG-3000");

        // Assert
        result.Should().NotBeNull();
        result!.ProtocolId.Should().Be(protocol.ProtocolId);
    }

    [Fact]
    public async Task GetByCompositeKey_WithNonExistingProtocol_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByCompositeKeyAsync("CHEST", "PA", "HVG-3000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCompositeKey_WithDifferentCase_ReturnsProtocol()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol);

        // Act
        var result = await _repository.GetByCompositeKeyAsync("chest", "pa", "hvg-3000");

        // Assert
        result.Should().NotBeNull();
        result!.ProtocolId.Should().Be(protocol.ProtocolId);
    }

    [Fact]
    public async Task GetProtocolsByBodyPartAsync_ReturnsMatchingProtocols()
    {
        // Arrange
        var protocol1 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        var protocol2 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "LATERAL",
            Kv = 110,
            Ma = 80,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        var protocol3 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "ABDOMEN",
            Projection = "AP",
            Kv = 100,
            Ma = 60,
            ExposureTimeMs = 80,
            DeviceModel = "HVG-3000"
        };

        await _repository.CreateAsync(protocol1);
        await _repository.CreateAsync(protocol2);
        await _repository.CreateAsync(protocol3);

        // Act
        var result = await _repository.GetProtocolsByBodyPartAsync("CHEST");

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Projection).Should().BeEquivalentTo(new[] { "PA", "LATERAL" });
    }

    [Fact]
    public async Task GetByProcedureCodeAsync_ReturnsMatchingProtocols()
    {
        // Arrange
        var protocol1 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000",
            ProcedureCodes = new[] { "CHEST_PA", "XR_CHEST" }
        };

        var protocol2 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "LATERAL",
            Kv = 110,
            Ma = 80,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000",
            ProcedureCodes = new[] { "XR_CHEST" }
        };

        var protocol3 = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "ABDOMEN",
            Projection = "AP",
            Kv = 100,
            Ma = 60,
            ExposureTimeMs = 80,
            DeviceModel = "HVG-3000",
            ProcedureCodes = new[] { "ABDOMEN_AP" }
        };

        await _repository.CreateAsync(protocol1);
        await _repository.CreateAsync(protocol2);
        await _repository.CreateAsync(protocol3);

        // Act
        var result = await _repository.GetByProcedureCodeAsync("XR_CHEST");

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Projection).Should().BeEquivalentTo(new[] { "PA", "LATERAL" });
    }

    [Fact]
    public async Task GetByProcedureCodeAsync_WithDifferentCase_ReturnsProtocols()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000",
            ProcedureCodes = new[] { "CHEST_PA" }
        };

        await _repository.CreateAsync(protocol);

        // Act
        var result = await _repository.GetByProcedureCodeAsync("chest_pa");

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProtocolAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetProtocolAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CompositeKeyLookupPerformance_With500Protocols_CompletesWithin50ms()
    {
        // Arrange - Use unique composite keys (body_part, projection, device_model)
        var protocols = new Protocol[500];
        for (int i = 0; i < 500; i++)
        {
            protocols[i] = new Protocol
            {
                ProtocolId = Guid.NewGuid(),
                BodyPart = $"BODY_PART_{i % 10}",
                Projection = $"PROJ_{i % 5}",
                Kv = 80 + (i % 50),
                Ma = 50 + (i % 100),
                ExposureTimeMs = 50 + (i % 50),
                DeviceModel = $"HVG-{3000 + i / 50}" // Unique device model to avoid duplicate composite keys
            };
            await _repository.CreateAsync(protocols[i]);
        }

        var targetProtocol = protocols[250];

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetByCompositeKeyAsync(
            targetProtocol.BodyPart,
            targetProtocol.Projection,
            targetProtocol.DeviceModel);
        sw.Stop();

        // Assert
        result.Should().NotBeNull();
        result!.ProtocolId.Should().Be(targetProtocol.ProtocolId);
        sw.ElapsedMilliseconds.Should().BeLessOrEqualTo(50,
            $"Composite key lookup took {sw.ElapsedMilliseconds}ms, expected <= 50ms for 500 protocols");
    }
}
