using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HnVue.Console.ViewModels;
using HnVue.Console.Services;
using HnVue.Console.Services.Adapters;
using HnVue.Console.Rendering;

namespace HnVue.Console.DependencyInjection;

/// <summary>
/// Dependency injection registration for HnVue Console.
/// SPEC-UI-001: DI container configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all HnVue Console services and ViewModels.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void AddHnVueConsole(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Shell ViewModel
        services.AddTransient<ShellViewModel>();

        // Register ViewModels (Transient lifetime)
        services.AddTransient<PatientViewModel>();
        services.AddTransient<PatientRegistrationViewModel>();
        services.AddTransient<PatientEditViewModel>();
        services.AddTransient<WorklistViewModel>();
        services.AddTransient<AcquisitionViewModel>();
        services.AddTransient<AECViewModel>();
        services.AddTransient<ExposureParameterViewModel>();
        services.AddTransient<ProtocolViewModel>();
        services.AddTransient<DoseViewModel>();
        services.AddTransient<ImageReviewViewModel>();
        services.AddTransient<SystemStatusViewModel>();
        services.AddTransient<ConfigurationViewModel>();
        services.AddTransient<AuditLogViewModel>();

        // Register gRPC Service Adapters (Singleton lifetime - shared channel)
        services.AddSingleton<IPatientService, PatientServiceAdapter>();
        services.AddSingleton<IWorklistService, WorklistServiceAdapter>();
        services.AddSingleton<IExposureService, ExposureServiceAdapter>();
        services.AddSingleton<IProtocolService, ProtocolServiceAdapter>();
        services.AddSingleton<IAECService, AECServiceAdapter>();
        services.AddSingleton<IDoseService, DoseServiceAdapter>();
        services.AddSingleton<IImageService, ImageServiceAdapter>();
        services.AddSingleton<IQCService, QCServiceAdapter>();
        services.AddSingleton<ISystemStatusService, SystemStatusServiceAdapter>();
        services.AddSingleton<ISystemConfigService, SystemConfigServiceAdapter>();
        services.AddSingleton<IUserService, UserServiceAdapter>();
        services.AddSingleton<INetworkService, NetworkServiceAdapter>();
        services.AddSingleton<IAuditLogService, AuditLogServiceAdapter>();
        services.AddSingleton<MeasurementOverlayService>();

        // Register Rendering Services
        services.AddSingleton<GrayscaleRenderer>();
        services.AddSingleton<WindowLevelTransform>();

        // Register Configuration
        services.AddSingleton(configuration);
    }
}
