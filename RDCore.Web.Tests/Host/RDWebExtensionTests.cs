using Microsoft.Extensions.Logging.Abstractions;
using RDCore.SDK.Client;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.Web.Diagnostics;
using RDCore.Web.Endpoints;
using RDCore.Web.Host;
using System.Reflection;

namespace RDCore.Web.Tests.Host;

/// <summary>
/// RDCore.Web.Host as the platform starts it: the RDWeb extension, which its assembly declares a diagnostics provider of the <c>RDW</c> codes, as
/// <c>rdc describe-ext</c> reads it, and whose handler answers <c>rdcore/diagnostics/document</c> with RDWeb's findings, each linked to its page.
/// </summary>
[TestClass]
public sealed class RDWebExtensionTests
{
    private static readonly Uri File = new("file:///c:/app/Orders.cls");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TheHostsAssembly_DeclaresADiagnosticsProvider_OfTheRDWCodes_AndWhereTheirPagesAre()
    {
        var assembly = typeof(RDWebExtensionApp).Assembly;

        CollectionAssert.Contains(ProvidedCorePlatformCapabilities.Reflect(assembly).ToArray(), nameof(DiagnoseDocument));
        var prefix = assembly.GetCustomAttribute<DiagnosticPrefixAttribute>()!;
        Assert.AreEqual(RDWebDiagnostics.Prefix, prefix.Prefix);
        Assert.AreEqual(RDWebDiagnostics.HelpBaseUri, prefix.HelpBaseUri);
    }

    [TestMethod]
    public async Task TheExtensionsHandler_ReportsRDWebsFindings_EachLinkedToItsPage()
    {
        // 0 '@Controller; 1 '@HttpPost; 2-4 Function Place(order, note).
        SyntaxNode[] Annotated(int line, string name)
            => [new CommentTriviaNode(Id(line), Lines(line, line), $"'@{name}"), new AnnotationTriviaNode(Id(line + 100), Lines(line, line), name, [])];
        var procedure = new MemberDeclarationNode(Id(2), Lines(2, 4),
            [new ParameterDeclarationNode(Id(20), Lines(2, 2), "order"), new ParameterDeclarationNode(Id(21), Lines(2, 2), "note")], "Place",
            MemberKind.Function, AccessModifier.Public);
        var parse = new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(0), Lines(0, 4), [procedure]),
            Trivia = [.. Annotated(0, RDWebAnnotations.Controller), .. Annotated(1, RDWebAnnotations.HttpPost)],
        };
        var handler = new AnalyzerDiagnosticsHandler(RDWebExtensionApp.Diagnostics, RDWebExtensionApp.Analyzers,
            NullLogger<AnalyzerDiagnosticsHandler>.Instance);

        var result = await handler.Handle(new DiagnoseDocumentParams { Json = PlatformJson.Serialize(new DiagnoseDocumentPayload(File, 3, parse)) },
            TestContext.CancellationToken);

        CollectionAssert.AreEquivalent(new[] { RDWebDiagnostics.ImplicitBodyBinding.ToString(), RDWebDiagnostics.SeveralBodyParameters.ToString() },
            result.Diagnostics.Select(diagnostic => diagnostic.Code?.String).ToArray());
        foreach (var diagnostic in result.Diagnostics)
        {
            Assert.AreEqual(new Uri($"{RDWebDiagnostics.HelpBaseUri}/{diagnostic.Code?.String?.ToLowerInvariant()}.html"),
                diagnostic.CodeDescription?.Href);
        }
    }

    private static SyntaxNodeId Id(int key) => new(File.ToString(), [key]);

    private static SourceLocation Lines(int first, int last) => new() { Uri = File, Range = new SourceRange(first, 0, last, 40) };
}
