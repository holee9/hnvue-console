using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HnVue.Console.DependencyInjection;
using HnVue.Console.Shell;
using HnVue.Console.Services;
using HnVue.Console.ViewModels;

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

        // Detect E2E test mode via environment variable
        var isE2EMode = Environment.GetEnvironmentVariable("HNVUE_E2E_TEST") == "1";

        // Build configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Add environment-specific config
        if (isE2EMode)
        {
            configBuilder.AddJsonFile("appsettings.E2E.json", optional: true, reloadOnChange: true);
        }
        else
        {
            configBuilder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        }

        var configuration = configBuilder.Build();

        // Log E2E mode detection
        var e2eLog = System.Console.Out;
        if (isE2EMode)
        {
            e2eLog.WriteLine("[E2E MODE] Enabled - Loading appsettings.E2E.json");
        }
        else
        {
            e2eLog.WriteLine("[NORMAL MODE] Loading default configuration");
        }

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

        // Show LoginWindow or auto-login depending on mode.
        // HNVUE_E2E_TEST=1 → auto-login unless HNVUE_E2E_SHOW_LOGIN=1 is also set.
        // HNVUE_E2E_SHOW_LOGIN=1 allows E2E login-flow tests to exercise LoginWindow.
        var showLoginInE2E = Environment.GetEnvironmentVariable("HNVUE_E2E_SHOW_LOGIN") == "1";
        if (isE2EMode && !showLoginInE2E)
        {
            // E2E mode: create admin session automatically so existing E2E tests are unaffected.
            AutoLoginForE2E();
        }
        else
        {
            var loginViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow(loginViewModel);
            loginWindow.Show();
        }
    }

    /// <summary>
    /// Creates an admin session and opens MainWindow directly for E2E test runs.
    /// This avoids requiring all existing E2E tests to perform login steps.
    /// </summary>
    private void AutoLoginForE2E()
    {
        var sessionContext = _serviceProvider!.GetRequiredService<ISessionContext>();
        var userService = _serviceProvider!.GetRequiredService<IUserService>();

        // Authenticate synchronously on startup using mock service (no gRPC in E2E mode).
        var result = userService.AuthenticateAsync(
            "System Administrator", "password123", "E2E-WS", CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.Success && result.Session != null)
        {
            sessionContext.SetSession(result.Session);
        }

        new MainWindow().Show();
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
