using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Uid;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Storage;
using HnVue.Dicom.Tls;
using HnVue.Dicom.Worklist;
using HnVue.Dicom.Mpps;
using HnVue.Dicom.StorageCommit;
using HnVue.Dicom.Facade;

namespace HnVue.Dicom.DependencyInjection;

/// <summary>
/// Extension methods for configuring HnVue DICOM services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds HnVue DICOM services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing DICOM settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHnVueDicom(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        // Bind configuration (fo-dicom 4.x: use Bind() to avoid ConfigurationExtensions dependency)
        services.Configure<DicomServiceOptions>(options => configuration.Bind(options));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DicomServiceOptions>>().Value);

        // Register core services
        services.AddSingleton<IUidGenerator>(sp => {
            var opts = sp.GetRequiredService<DicomServiceOptions>();
            return new UidGenerator(opts.UidRoot, opts.DeviceSerial);
        });
        services.AddSingleton<IAssociationManager, AssociationManager>();

        // Register TLS factory
        services.AddSingleton<ITlsFactory, DicomTlsFactory>();

        // Register queue services
        services.AddSingleton<ITransmissionQueue, TransmissionQueue>();

        // Register SCU services
        services.AddSingleton<IStorageScu, StorageScu>();
        services.AddSingleton<IWorklistScu, WorklistScu>();
        services.AddSingleton<IMppsScu, MppsScu>();
        services.AddSingleton<IStorageCommitScu, StorageCommitScu>();

        // Register facade
        services.AddSingleton<IDicomServiceFacade, DicomServiceFacade>();

        return services;
    }

    /// <summary>
    /// Adds HnVue DICOM services with pre-configured options.
    /// Useful for testing or scenarios without IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure DICOM options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHnVueDicom(
        this IServiceCollection services,
        Action<DicomServiceOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DicomServiceOptions>>().Value);

        // Register core services
        services.AddSingleton<IUidGenerator>(sp => {
            var opts = sp.GetRequiredService<DicomServiceOptions>();
            return new UidGenerator(opts.UidRoot, opts.DeviceSerial);
        });
        services.AddSingleton<IAssociationManager, AssociationManager>();

        // Register TLS factory
        services.AddSingleton<ITlsFactory, DicomTlsFactory>();

        // Register queue services
        services.AddSingleton<ITransmissionQueue, TransmissionQueue>();

        // Register SCU services
        services.AddSingleton<IStorageScu, StorageScu>();
        services.AddSingleton<IWorklistScu, WorklistScu>();
        services.AddSingleton<IMppsScu, MppsScu>();
        services.AddSingleton<IStorageCommitScu, StorageCommitScu>();

        // Register facade
        services.AddSingleton<IDicomServiceFacade, DicomServiceFacade>();

        return services;
    }
}
