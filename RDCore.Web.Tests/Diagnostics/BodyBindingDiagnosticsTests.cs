using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Workspace;
using RDCore.Web.Diagnostics;
using RDCore.Web.Endpoints;

namespace RDCore.Web.Tests.Diagnostics;

/// <summary>
/// What RDWeb's analyzers say of the body binding of a controller's actions: <c>RDW00001</c> where no <c>'@FromBody</c> names the parameter the body binds
/// (<see cref="ImplicitBodyBindingAnalyzer"/>), and <c>RDW00002</c> at each further parameter the route does not bind
/// (<see cref="SeveralBodyParametersAnalyzer"/>). The parses are built as the parser builds them: each annotation inside the comment line that holds it, each
/// parameter a child of its procedure, on the procedure's first line.
/// </summary>
[TestClass]
public sealed class BodyBindingDiagnosticsTests
{
    private static readonly Uri File = new("file:///c:/app/Orders.cls");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last, int start = 0, int end = 40)
        => new() { Uri = File, Range = new SourceRange(first, start, last, end) };

    // a public Function on lines first to last, its parameters written on its first line, each at a column of its own.
    private static MemberDeclarationNode Action(string name, int first, int last, params string[] parameters)
        => new(Id(), Lines(first, last), [.. parameters.Select((parameter, index) => Parameter(parameter, first, 20 + (10 * index)))], name,
            MemberKind.Function, AccessModifier.Public);

    private static ParameterDeclarationNode Parameter(string name, int line, int column)
        => new(Id(), Lines(line, line, column, column + name.Length + 12), name, NameRange: new SourceRange(line, column, line, column + name.Length));

    // a comment line that holds one annotation, as the parser keeps it: the comment, then the annotation inside its range.
    private static SyntaxNode[] Annotated(int line, string name, string? argument = null)
    {
        SyntaxNode[] arguments = argument is null ? [] : [new LiteralExpressionNode(Id(), Lines(line, line), new VBStringValue(argument))];
        return [new CommentTriviaNode(Id(), Lines(line, line), $"'@{name}"), new AnnotationTriviaNode(Id(), Lines(line, line), name, [.. arguments])];
    }

    private static ModuleParseResult Parse(SyntaxNode[] children, params SyntaxNode[][] trivia)
        => new() { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia.SelectMany(nodes => nodes)], LineEnding = "\r\n" };

    private static AnalyzerFinding[] Findings(IModuleAnalyzer analyzer, ModuleParseResult parse)
        => [.. analyzer.Analyze(new ModuleAnalysisContext(File, parse, null))];

    private static string Text(SourceRange range) => $"{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}";

    [TestMethod]
    public void TheActionsOfTheFilesOwnModule_AreOfTheModuleItsVBNameNames_NotOfItsFile()
    {
        // 0 Attribute VB_Name = "Purchases" in Orders.cls; 1 '@Controller; 2 '@HttpPost; 3-5 Function Place(order).
        var vbName = new AttributeDirectiveNode(Id(), Lines(0, 0), Tokens.VB_Name, "\"Purchases\"");
        var parse = Parse([vbName, Action("Place", 3, 5, "order")], Annotated(1, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost));

        var (_, action, _) = BodyBindings.Of(new ModuleAnalysisContext(File, parse, null)).Single();

        Assert.AreEqual("Purchases", action.ModuleName);
    }

    [TestMethod]
    public void APostWhoseBodyNoFromBodyNames_IsReportedAtTheParameterItBinds_WithAFixThatNamesIt()
    {
        // 0 '@Controller; 2 '@HttpPost("{id}"); 3-5 Function Place(id, order).
        var parse = Parse([Action("Place", 3, 5, "id", "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost, "{id}"));

        var finding = Findings(new ImplicitBodyBindingAnalyzer(), parse).Single();

        Assert.AreEqual(RDWebDiagnostics.ImplicitBodyBinding, finding.Code);
        Assert.AreEqual(DiagnosticSeverity.Information, finding.Severity);
        Assert.AreEqual("3:30-3:35", Text(finding.Range), "the name of order, the parameter the body binds");
        var edit = finding.Fixes!.Edits.Single().Edits.Single();
        Assert.AreEqual("'@FromBody(\"order\")\r\n", edit.NewText);
        Assert.AreEqual("3:0-3:0", $"{edit.Range.Start.Line}:{edit.Range.Start.Character}-{edit.Range.End.Line}:{edit.Range.End.Character}",
            "on a line of its own, above the procedure, under its '@HttpPost");
    }

    [TestMethod]
    public void APostWhoseBodyFromBodyNames_AndAGet_AreNotReported()
    {
        // 0 '@Controller; 2-3 '@HttpPost, '@FromBody("order"); 4-6 Place(id, order). 8 '@HttpGet; 9-11 Find(id, name).
        var parse = Parse([Action("Place", 4, 6, "id", "order"), Action("Find", 9, 11, "id", "name")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost), Annotated(3, RDWebAnnotations.FromBody, "order"),
            Annotated(8, RDWebAnnotations.HttpGet));

        Assert.IsEmpty(Findings(new ImplicitBodyBindingAnalyzer(), parse));
    }

    [TestMethod]
    public void AnActionWhoseRoutesLeaveItsBodyToTwoParameters_IsReportedAtEach_WithNoFix()
    {
        // 0 '@Controller; 2 '@HttpPost("{id}"), which leaves the body to order; 3 '@HttpPost, which leaves it to id; 4-6 Place(id, order).
        var parse = Parse([Action("Place", 4, 6, "id", "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost, "{id}"), Annotated(3, RDWebAnnotations.HttpPost));

        var findings = Findings(new ImplicitBodyBindingAnalyzer(), parse);

        CollectionAssert.AreEquivalent(new[] { "4:30-4:35", "4:20-4:22" }, findings.Select(finding => Text(finding.Range)).ToArray());
        Assert.IsTrue(findings.All(finding => finding.Fixes is null), "naming one would leave the other route with its body in its route");
    }

    [TestMethod]
    public void EachFurtherParameterOfAPost_ThatTheRouteDoesNotBind_IsAWarning_AndTheBodysParameterIsNot()
    {
        // 0 '@Controller; 2 '@HttpPost("{id}"); 3-5 Place(id, order, note): the route binds id, the body order, and note is left to the query string.
        var parse = Parse([Action("Place", 3, 5, "id", "order", "note")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost, "{id}"));

        var finding = Findings(new SeveralBodyParametersAnalyzer(), parse).Single();

        Assert.AreEqual(RDWebDiagnostics.SeveralBodyParameters, finding.Code);
        Assert.AreEqual(DiagnosticSeverity.Warning, finding.Severity);
        Assert.AreEqual("3:40-3:44", Text(finding.Range), "the name of note");
    }

    [TestMethod]
    public void TheParameterFromBodyNames_IsTheBodys_AndTheFirstUnboundOneIsTheFurtherOne()
    {
        // 0 '@Controller; 2-3 '@HttpPost, '@FromBody("note"); 4-6 Place(order, note).
        var parse = Parse([Action("Place", 4, 6, "order", "note")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost), Annotated(3, RDWebAnnotations.FromBody, "note"));

        Assert.AreEqual("4:20-4:25", Text(Findings(new SeveralBodyParametersAnalyzer(), parse).Single().Range), "the name of order");
    }

    [TestMethod]
    public void AGetsParameters_BindFromTheQueryString_AndAreNotReported()
    {
        // 0 '@Controller; 2 '@HttpGet; 3-5 Find(id, name, page).
        var parse = Parse([Action("Find", 3, 5, "id", "name", "page")], Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpGet));

        Assert.IsEmpty(Findings(new SeveralBodyParametersAnalyzer(), parse));
        Assert.IsEmpty(Findings(new ImplicitBodyBindingAnalyzer(), parse));
    }

    [TestMethod]
    public void AnActionTheHostCannotServe_IsNotReported_AndTheModulesOthersAre()
    {
        // 0 '@Controller; 2 '@HttpPost("{missing}"), whose route names no parameter; 3-5 Broken(order). 7 '@HttpPost; 8-10 Place(order).
        var parse = Parse([Action("Broken", 3, 5, "order"), Action("Place", 8, 10, "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost, "{missing}"), Annotated(7, RDWebAnnotations.HttpPost));

        Assert.AreEqual("8:20-8:25", Text(Findings(new ImplicitBodyBindingAnalyzer(), parse).Single().Range));
    }

    [TestMethod]
    public void AnActionOfABlocksController_IsReported_InTheFile()
    {
        // 0 '@Controller above the header; 1 Class Orders; 2 '@HttpPost; 3-5 Place(order); 6 End Class. The file's own module has no controller.
        var block = new BlockModule("Orders", ModuleType.ClassModule, RDCoreDocument.BlockUri(File, "Orders"), new SourceRange(1, 0, 6, 9),
            new ModuleNode(Id(), Lines(2, 5), [Action("Place", 3, 5, "order")]));
        var parse = new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), Lines(0, 6), []),
            Trivia =
            [
                .. Annotated(0, RDWebAnnotations.Controller),
                new BlockBoundaryTriviaNode(Id(), Lines(1, 1), FileBlockKind.Class, false, "Class Orders"),
                .. Annotated(2, RDWebAnnotations.HttpPost),
                new BlockBoundaryTriviaNode(Id(), Lines(6, 6), FileBlockKind.Class, true, "End Class"),
            ],
            BlockModules = [block],
        };

        Assert.AreEqual("3:20-3:25", Text(Findings(new ImplicitBodyBindingAnalyzer(), parse).Single().Range));
    }
}
