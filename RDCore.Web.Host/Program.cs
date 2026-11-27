using RDCore.SDK.Client;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Server.Configuration;
using RDCore.Web.Diagnostics;
using RDCore.Web.Host;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RDCore.Web.Tests")]

// marks the RDWeb extension a diagnostics provider: the language server sends it rdcore/diagnostics/document, and `rdc describe-ext` records it.
[assembly: ProvidesCorePlatformClientCapability<DiagnoseDocument>]

// its diagnostics are reported under RDW codes, and each code has a page under RDCore.Web's documentation.
[assembly: DiagnosticPrefix(RDWebDiagnostics.Prefix, HelpBaseUri = RDWebDiagnostics.HelpBaseUri)]

// started by the platform, with the pipe it names, the host is the RDWeb extension; started otherwise, it serves the application.
if (SdkCommandLine.IsServerLaunch(args))
{
    using var extension = new RDWebExtensionHost();
    return await extension.RunAsync(args);
}

RDWebHostApplication.Create(new WebApplicationOptions { Args = args }).Run();
return 0;
