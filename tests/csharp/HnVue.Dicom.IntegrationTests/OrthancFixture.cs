using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Xunit;

namespace HnVue.Dicom.IntegrationTests;

/// <summary>
/// Orthanc DICOM SCP fixture for integration tests.
/// Manages Orthanc Docker container lifecycle using Testcontainers for .NET.
/// Orthanc image: jodogne/orthanc:24.1.2
/// Exposed ports: 4242 (DICOM), 8042 (HTTP API)
/// Default credentials: orthanc/orthanc
/// </summary>
public sealed class OrthancFixture : IAsyncLifetime
{
    private const string OrthancImage = "jodogne/orthanc:24.1.2";
    private const string DefaultUsername = "orthanc";
    private const string DefaultPassword = "orthanc";
    private const int DicomPort = 4242;
    private const int HttpPort = 8042;

    private readonly IContainer _container;

    /// <summary>
    /// Gets the mapped DICOM port on the host.
    /// </summary>
    public int HostDicomPort { get; private set; }

    /// <summary>
    /// Gets the mapped HTTP port on the host.
    /// </summary>
    public int HostHttpPort { get; private set; }

    /// <summary>
    /// Gets the host address (localhost).
    /// </summary>
    public string HostAddress { get; } = "127.0.0.1";

    /// <summary>
    /// Gets the DICOM endpoint host:port for testing.
    /// </summary>
    public string DicomedEndpoint => $"{HostAddress}:{HostDicomPort}";

    /// <summary>
    /// Gets the HTTP API base URL for testing.
    /// </summary>
    public string HttpApiBaseUrl => $"http://{HostAddress}:{HostHttpPort}";

    /// <summary>
    /// Initializes a new instance of the <see cref="OrthancFixture"/> class.
    /// </summary>
    public OrthancFixture()
    {
        _container = new ContainerBuilder()
            .WithImage(OrthancImage)
            .WithName($"orthanc-it-{Guid.NewGuid()}")
            .WithPortBinding(DicomPort, true)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(DicomPort))
            .Build();
    }

    /// <summary>
    /// Initializes the Orthanc container asynchronously.
    /// Called by xUnit before the first test in the collection.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        HostDicomPort = _container.GetMappedPublicPort(DicomPort);
        HostHttpPort = _container.GetMappedPublicPort(HttpPort);

        // Wait for Orthanc to be fully ready
        await WaitForOrthancReadyAsync();
    }

    /// <summary>
    /// Disposes the Orthanc container asynchronously.
    /// Called by xUnit after all tests in the collection complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Waits for Orthanc HTTP API to be responsive.
    /// </summary>
    private async Task WaitForOrthancReadyAsync()
    {
        var maxAttempts = 30;
        var delay = TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                using var client = CreateHttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{HttpApiBaseUrl}/statistics");
                var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{DefaultUsername}:{DefaultPassword}"));
                request.Headers.Add("Authorization", $"Basic {authHeader}");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore connection errors and retry
            }

            await Task.Delay(delay);
        }

        throw new InvalidOperationException("Orthanc did not become ready within the timeout period.");
    }

    /// <summary>
    /// Queries Orthanc HTTP API for DICOM instances.
    /// </summary>
    /// <returns>List of DICOM instance IDs stored in Orthanc.</returns>
    public async Task<List<string>> GetInstancesAsync()
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"{HttpApiBaseUrl}/instances");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var instances = JsonSerializer.Deserialize<List<string>>(content);

        return instances ?? new List<string>();
    }

    /// <summary>
    /// Gets detailed information about a DICOM instance from Orthanc.
    /// </summary>
    /// <param name="instanceId">The Orthanc instance ID.</param>
    /// <returns>Instance information as JSON.</returns>
    public async Task<JsonDocument> GetInstanceInfoAsync(string instanceId)
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"{HttpApiBaseUrl}/instances/{instanceId}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Gets DICOM tags for an instance from Orthanc.
    /// </summary>
    /// <param name="instanceId">The Orthanc instance ID.</param>
    /// <returns>DICOM tags as JSON document.</returns>
    public async Task<JsonDocument> GetInstanceTagsAsync(string instanceId)
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"{HttpApiBaseUrl}/instances/{instanceId}/tags?simplify");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Deletes all instances from Orthanc.
    /// Useful for test isolation.
    /// </summary>
    public async Task DeleteAllInstancesAsync()
    {
        var instances = await GetInstancesAsync();
        using var client = CreateHttpClient();

        foreach (var instanceId in instances)
        {
            await client.DeleteAsync($"{HttpApiBaseUrl}/instances/{instanceId}");
        }
    }

    /// <summary>
    /// Finds a free TCP port on the local host.
    /// Used for dynamic port allocation when needed.
    /// </summary>
    /// <returns>A free port number.</returns>
    public static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Creates an HTTP client configured for Orthanc API authentication.
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var authHeader = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{DefaultUsername}:{DefaultPassword}"));
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {authHeader}");
        return client;
    }

    /// <summary>
    /// Creates an Orthanc modality (worklist/MPPS/storage commit) configuration.
    /// </summary>
    /// <param name="aeTitle">The AE title for the modality.</param>
    /// <param name="host">The host address.</param>
    /// <param name="port">The port number.</param>
    public async Task CreateModalityAsync(string aeTitle, string host, int port)
    {
        using var client = CreateHttpClient();
        var payload = JsonSerializer.Serialize(new
        {
            Aet = aeTitle,
            Host = host,
            Port = port
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"{HttpApiBaseUrl}/modalities/{aeTitle}", content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Queries Orthanc for worklist entries.
    /// </summary>
    /// <returns>Worklist entries as JSON document.</returns>
    public async Task<JsonDocument> QueryWorklistAsync()
    {
        using var client = CreateHttpClient();
        var response = await client.PostAsync($"{HttpApiBaseUrl}/modalities/ORTHANC/query-worklist", null);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Gets Orthanc statistics.
    /// </summary>
    /// <returns>Orthanc statistics as JSON document.</returns>
    public async Task<JsonDocument> GetStatisticsAsync()
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"{HttpApiBaseUrl}/statistics");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }
}

/// <summary>
/// Collection fixture for Orthanc - shared across all tests in the collection.
/// Tests must use [Collection("Orthanc")] attribute to share this fixture.
/// </summary>
[CollectionDefinition("Orthanc")]
public class OrthancCollection : ICollectionFixture<OrthancFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and the ICollectionFixture<> interface.
}
