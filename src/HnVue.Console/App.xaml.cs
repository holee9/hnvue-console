using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HnVue.Console.DependencyInjection;

namespace HnVue.Console;

/// <summary>
/// Application entry point with DI bootstrapping.
/// SPEC-UI-001 Shell Infrastructure foundation.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        // Configure services
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddDebug();
            builder.AddConsole();
        });

        // Register HnVue Console services
        services.AddHnVueConsole(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

        _logger.LogInformation("HnVue Console application started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("HnVue Console application shutting down");

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Gets the configured service provider for View resolution.
    /// </summary>
    public static IServiceProvider? ServiceProvider => (Current as App)?._serviceProvider;
}
