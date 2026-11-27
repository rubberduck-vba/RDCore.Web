using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using RDCore.SDK.Client;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.Diagnostics;
using RDCore.SDK.Server;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Server.Services;
using RDCore.SDK.Server.Services.States;
using RDCore.Web.Diagnostics;
using System.Reflection;

namespace RDCore.Web.Host;

// RDCore.Web.Host as the platform starts it (SdkCommandLine.IsServerLaunch): the RDWeb extension, over the pipe the platform names.
internal sealed class RDWebExtensionHost() : RDCorePlatformServerHost<RDWebExtensionApp>();

// the RDWeb extension: a diagnostics provider of the RDW codes, which runs RDWeb's analyzers on each document the language server pushes down.
internal sealed class RDWebExtensionApp(
    IOptions<SdkAppOptions> options,
    IServerStateProvider serverStateProvider,
    IHealthCheckService<RDWebExtensionApp> healthCheckService,
    ILanguageServerProtocolTransportLayer transportLayer,
    ILogger<RDWebExtensionApp> logger)
    : RDCoreServerApp(options, serverStateProvider, healthCheckService, transportLayer, logger)
{
    public override CoreServerComponent PlatformComponent => CoreServerComponent.Extension;

    // the factory links each RDW code to its page: the prefix and the pages' address are the ones the assembly declares, which its manifest records.
    public static DiagnosticFactory Diagnostics { get; } = new()
    {
        DeclaredPrefix = typeof(RDWebExtensionApp).Assembly.GetCustomAttribute<DiagnosticPrefixAttribute>(),
    };

    // every analyzer of the extension, which the handler runs on each document.
    public static IReadOnlyList<IModuleAnalyzer> Analyzers { get; } = [new ImplicitBodyBindingAnalyzer(), new SeveralBodyParametersAnalyzer()];

    protected override void ConfigureHandlers(IRDCoreLSPHandlerConfigurationBuilder builder) => builder.WithHandler<AnalyzerDiagnosticsHandler>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICoreDiagnosticsFactory>(Diagnostics);
        foreach (var analyzer in Analyzers)
        {
            services.AddSingleton(analyzer);
        }
    }

    protected override void Dispose(bool disposing)
    {
    }

    protected override void RegisterServerCapabilities(ILanguageServer server, ClientCapabilities clientCapabilities)
    {
    }
}
