using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.MCP.Resources;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Serilog;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.MCP;

public static class SolidWorksPublicHostingExtensions
{
    public static void AddSolidWorksJsonConfiguration(this HostApplicationBuilder host)
    {
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            host.Configuration.AddJsonFile(appsettingsPath, optional: true, reloadOnChange: false);
        }

        var localSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        if (File.Exists(localSettingsPath))
        {
            host.Configuration.AddJsonFile(localSettingsPath, optional: true, reloadOnChange: false);
        }
    }

    public static void AddSolidWorksSerilog(this HostApplicationBuilder host)
    {
        // Enable Serilog self-diagnostics for sink failures
        var selfLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FurniOx", "logs", "furniox-mcp-selflog.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(selfLogPath)!);
        Serilog.Debugging.SelfLog.Enable(
            msg => File.AppendAllText(selfLogPath, $"{DateTime.UtcNow:O} {msg}{Environment.NewLine}"));

        host.Services.AddSerilog((services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .WriteTo.Console(
                    // IMPORTANT: MCP transport uses stdout. All log output MUST go to stderr.
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}",
                    standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                .WriteTo.Async(a => a.File(
                    path: Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "FurniOx", "logs", "furniox-mcp-.log"),
                    rollingInterval: Serilog.RollingInterval.Day,
                    fileSizeLimitBytes: 10_485_760,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext:l}] [PID:{ProcessId}] [T{ThreadId}] {Message:lj}{NewLine}{Exception}"),
                    bufferSize: 10_000);
        });
    }

    public static IServiceCollection AddSolidWorksPublicServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SolidWorksSettings>(configuration.GetSection("SolidWorks"));
        services.AddSingleton(provider =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SolidWorksSettings>>().Value);

        services.AddSingleton<SolidWorksConnection>();
        services.AddSingleton<SolidWorks2023Adapter>();
        services.AddSingleton<ISolidWorksAdapter>(provider => provider.GetRequiredService<SolidWorks2023Adapter>());
        services.AddSingleton<ICircuitBreaker, CircuitBreaker>();
        services.AddSingleton<IStaTaskRunner, StaTaskRunner>();
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        services.AddSingleton<ISmartRouter>(provider => new SmartRouter(
            provider.GetRequiredService<ICircuitBreaker>(),
            provider.GetRequiredService<ISolidWorksAdapter>(),
            provider.GetRequiredService<IStaTaskRunner>(),
            provider.GetRequiredService<IPerformanceMonitor>(),
            provider.GetRequiredService<SolidWorksSettings>(),
            provider.GetRequiredService<ILogger<SmartRouter>>()));

        return services;
    }

    public static IMcpServerBuilder AddSolidWorksPublicMcp(this IServiceCollection services)
    {
        return services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<AssemblyBrowserTools>()
            .WithTools<ConfigurationTools>()
            .WithTools<DocumentTools>()
            .WithTools<ExportTools>()
            .WithTools<FeatureExtrusionTools>()
            .WithTools<FeatureFilletTools>()
            .WithTools<FeatureRevolveTools>()
            .WithTools<FeatureShellTools>()
            .WithTools<SelectionTools>()
            .WithTools<SketchGeometryTools>()
            .WithTools<SketchInspectionTools>()
            .WithTools<SketchParametricTools>()
            .WithTools<SketchSpecializedTools>()
            .WithTools<SortingTools>()
            .WithResourcesFromAssembly(typeof(SolidWorksResources).Assembly);
    }

    public static void ValidatePublicConfiguration(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SolidWorksPublicConfiguration");
        var settings = services.GetRequiredService<SolidWorksSettings>();

        logger.LogInformation("Validating public MCP configuration...");

        if (string.IsNullOrWhiteSpace(settings.GetProgIdVersionHint()))
        {
            logger.LogInformation("No SolidWorks ProgID version hint configured. Using generic ProgID discovery.");
        }
        else
        {
            logger.LogInformation("SolidWorks ProgID version hint: {Version}", settings.GetProgIdVersionHint());
        }

        LogTemplateConfiguration(logger, "Part", settings.PartTemplatePath, settings.GetPartTemplatePath());
        LogTemplateConfiguration(logger, "Assembly", settings.AssemblyTemplatePath, settings.GetAssemblyTemplatePath());
        LogTemplateConfiguration(logger, "Drawing", settings.DrawingTemplatePath, settings.GetDrawingTemplatePath());

        if (settings.CircuitBreaker.FailureThreshold < 1)
        {
            logger.LogWarning("Circuit breaker FailureThreshold is {Threshold}, should be >= 1", settings.CircuitBreaker.FailureThreshold);
        }

        if (settings.CircuitBreaker.ResetTimeoutSeconds < 1)
        {
            logger.LogWarning("Circuit breaker ResetTimeoutSeconds is {Timeout}, should be >= 1", settings.CircuitBreaker.ResetTimeoutSeconds);
        }

        if (settings.ComParameterLimit < 1)
        {
            logger.LogWarning("ComParameterLimit is {Limit}, should be >= 1", settings.ComParameterLimit);
        }

        logger.LogInformation("Public profile enabled: {Enabled}", settings.PublicProfile.Enabled);

        if (!string.IsNullOrWhiteSpace(settings.PublicProfile.Contact.GitHubRepositoryUrl))
        {
            logger.LogInformation("GitHub repository contact configured: {Url}", settings.PublicProfile.Contact.GitHubRepositoryUrl);
        }

        if (!string.IsNullOrWhiteSpace(settings.PublicProfile.Contact.LinkedInUrl))
        {
            logger.LogInformation("LinkedIn contact configured: {Url}", settings.PublicProfile.Contact.LinkedInUrl);
        }

        logger.LogInformation("Public MCP configuration validation complete");
    }

    private static void LogTemplateConfiguration(Microsoft.Extensions.Logging.ILogger logger, string templateType, string? explicitPath, string? fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            logger.LogInformation("{Type} template override configured: {Path}", templateType, explicitPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            logger.LogInformation(
                "{Type} template fallback path configured: {Path}. SolidWorks user preferences will still be checked first at runtime.",
                templateType,
                fallbackPath);
            return;
        }

        logger.LogInformation(
            "{Type} template path is not explicitly configured. SolidWorks user preferences will be used at runtime.",
            templateType);
    }
}
