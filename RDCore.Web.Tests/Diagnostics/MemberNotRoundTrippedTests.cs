using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.Web.Diagnostics;
using RDCore.Web.Endpoints;
using System.Collections.Immutable;
using ExpressionClassification = RDCore.SDK.Semantics.ExpressionClassification;

namespace RDCore.Web.Tests.Diagnostics;

/// <summary>
/// <c>RDW00003</c> (<see cref="MemberNotRoundTrippedAnalyzer"/>): where an action's body or returned value is an object of a class of the workspace, the
/// members of the classes the host states for it (<see cref="DeclarationFactDto.DeclaredClasses"/>) that do not come back as they went out through JSON. The
/// parses are built as the parser builds them, and the facts as the host states them.
/// </summary>
[TestClass]
public sealed class MemberNotRoundTrippedTests
{
    private static readonly Uri File = new("file:///c:/app/Orders.cls");
    private static readonly Uri PersonClass = new("file:///c:/app/#Person");
    private static readonly Uri TeamClass = new("file:///c:/app/#Team");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last, int start = 0, int end = 40)
        => new() { Uri = File, Range = new SourceRange(first, start, last, end) };

    // a public procedure on lines first to last, its name at column 16, its parameters written on its first line, each at a column of its own.
    private static MemberDeclarationNode Action(string name, MemberKind kind, int first, int last, params string[] parameters)
        => new(Id(), Lines(first, last), [.. parameters.Select((parameter, index) => Parameter(parameter, first, 30 + (10 * index)))], name, kind,
            AccessModifier.Public, NameRange: new SourceRange(first, 16, first, 16 + name.Length));

    private static ParameterDeclarationNode Parameter(string name, int line, int column)
        => new(Id(), Lines(line, line, column, column + name.Length + 12), name, NameRange: new SourceRange(line, column, line, column + name.Length));

    private static SyntaxNode[] Annotated(int line, string name, string? argument = null)
    {
        SyntaxNode[] arguments = argument is null ? [] : [new LiteralExpressionNode(Id(), Lines(line, line), new VBStringValue(argument))];
        return [new CommentTriviaNode(Id(), Lines(line, line), $"'@{name}"), new AnnotationTriviaNode(Id(), Lines(line, line), name, [.. arguments])];
    }

    private static ModuleParseResult Parse(SyntaxNode[] children, params SyntaxNode[][] trivia)
        => new() { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia.SelectMany(nodes => nodes)], LineEnding = "\r\n" };

    private static ClassMemberFact Variable(string name, DeclaredTypeKind kind, string type, Uri? typeClass = null)
        => new(name, ExpressionClassification.Variable, PropertyAccessors.None, 0, kind, false, type) { TypeClass = typeClass };

    private static ClassMemberFact Property(string name, PropertyAccessors accessors, int parameters = 0, Uri? typeClass = null)
        => new(name, ExpressionClassification.Property, accessors, parameters, typeClass is null ? DeclaredTypeKind.Intrinsic : DeclaredTypeKind.Class, false,
            typeClass is null ? "String" : "Team") { TypeClass = typeClass };

    private static ClassMemberFact Method(string name, ExpressionClassification classification, Uri? typeClass = null)
        => new(name, classification, PropertyAccessors.None, 0, typeClass is null ? null : DeclaredTypeKind.Class, false, typeClass is null ? null : "Team")
        {
            TypeClass = typeClass,
        };

    // the round-trip rule (RDW00003): a member is written when it is a variable, or a property whose Get takes no argument.
    // the round-trip rule (RDW00003): a member is given a value when it is a variable, or a property whose Let or Set takes no argument besides its value.
    // Person: Name and Nick round-trip; Badge is a Get with no Let; Slot and Line take an index; Greet and Boss are methods, and Boss names Team.
    private static readonly DeclaredClassFact Person = new(PersonClass, "Person",
    [
        Variable("Name", DeclaredTypeKind.Intrinsic, "String"),
        Property("Nick", PropertyAccessors.Get | PropertyAccessors.Let),
        Property("Badge", PropertyAccessors.Get),
        Property("Slot", PropertyAccessors.Let, parameters: 1),
        Property("Line", PropertyAccessors.Get, parameters: 1),
        Method("Greet", ExpressionClassification.Subroutine),
        Method("Boss", ExpressionClassification.Function, TeamClass),
    ]);

    // Team: Lead is a Person; Tags is a Variant and Extra an Object; Items is an array of Variant.
    private static readonly DeclaredClassFact Team = new(TeamClass, "Team",
    [
        Variable("Name", DeclaredTypeKind.Intrinsic, "String"),
        Property("Lead", PropertyAccessors.Get | PropertyAccessors.Set, typeClass: PersonClass),
        Variable("Tags", DeclaredTypeKind.Variant, "Variant"),
        Variable("Extra", DeclaredTypeKind.Object, "Object"),
        Variable("Items", DeclaredTypeKind.Variant, "Variant") with { IsArray = true },
    ]);

    // the module's facts: a declaration named name, of that kind, declared on that line, with the classes its type names.
    private static ModuleSemanticsDto Semantics(params (string Name, DeclarationKind Kind, int Line, DeclaredClassFact[] Classes)[] declarations)
        => ModuleSemanticsDto.From(new ModuleSemanticModel(File, [], [])
        {
            Declarations =
            [
                .. declarations.Select(declaration => new DeclarationFact(
                    new SemanticId(new UriBuilder(File) { Fragment = $"Orders.{declaration.Name}" }.Uri), declaration.Name, declaration.Kind,
                    AccessModifier.Public, false, new SourceLocation(File, new SourceRange(declaration.Line, 30, declaration.Line, 34)), null)
                {
                    DeclaredClasses = [.. declaration.Classes],
                }),
            ],
        });

    private static AnalyzerFinding[] Findings(ModuleParseResult parse, ModuleSemanticsDto? semantics)
        => [.. new MemberNotRoundTrippedAnalyzer().Analyze(new ModuleAnalysisContext(File, parse, semantics))];

    private static string Text(SourceRange range) => $"{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}";

    // the members a finding names, each as Class.Member: the message's own wording around them is not what this asserts.
    private static string[] Named(AnalyzerFinding finding, params string[] candidates)
        => [.. candidates.Where(candidate => finding.Message.Contains(candidate, StringComparison.Ordinal))];

    private static readonly string[] EveryMember =
    [
        "Person.Name", "Person.Nick", "Person.Badge", "Person.Slot", "Person.Line", "Person.Greet", "Person.Boss",
        "Team.Name", "Team.Lead", "Team.Tags", "Team.Extra", "Team.Items",
    ];

    [TestMethod]
    public void ABodyOfAClass_IsAWarningAtItsParameter_NamingEachMemberThatDoesNotComeBack()
    {
        // 0 '@Controller; 2 '@HttpPost; 3-5 Function Place(order As Team).
        var parse = Parse([Action("Place", MemberKind.Function, 3, 5, "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost));

        var finding = Findings(parse, Semantics(("order", DeclarationKind.Parameter, 3, [Team, Person]))).Single();

        Assert.AreEqual(RDWebDiagnostics.MemberNotRoundTripped, finding.Code);
        Assert.AreEqual(DiagnosticSeverity.Warning, finding.Severity);
        Assert.AreEqual("3:30-3:35", Text(finding.Range), "the name of order");
        CollectionAssert.AreEquivalent(new[] { "Team.Tags", "Team.Extra", "Person.Badge" }, Named(finding, EveryMember));
    }

    [TestMethod]
    public void AnActionThatReturnsAnObjectOfAClass_IsAWarningAtItsName_OnAGetToo()
    {
        // 0 '@Controller; 2 '@HttpGet; 3-5 Function Find() As Person.
        var parse = Parse([Action("Find", MemberKind.Function, 3, 5)], Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpGet));

        var finding = Findings(parse, Semantics(("Find", DeclarationKind.Procedure, 3, [Person, Team]))).Single();

        Assert.AreEqual("3:16-3:20", Text(finding.Range), "the name of Find");
        CollectionAssert.AreEquivalent(new[] { "Person.Badge" }, Named(finding, EveryMember), "Team is reached only through the method Boss");
    }

    [TestMethod]
    public void AClassWhoseMembersAllComeBack_IsNotReported()
    {
        var parse = Parse([Action("Place", MemberKind.Function, 3, 5, "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost));
        var plain = new DeclaredClassFact(PersonClass, "Person",
            [Variable("Name", DeclaredTypeKind.Intrinsic, "String"), Property("Nick", PropertyAccessors.Get | PropertyAccessors.Let)]);

        Assert.IsEmpty(Findings(parse, Semantics(("order", DeclarationKind.Parameter, 3, [plain]))));
    }

    [TestMethod]
    public void AParameterTheBodyDoesNotBind_IsNotReported()
    {
        // 0 '@Controller; 2 '@HttpPost("{lead}"); 3-5 Function Place(lead As Person, order As String): the body binds order, a String.
        var parse = Parse([Action("Place", MemberKind.Function, 3, 5, "lead", "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost, "{lead}"));

        Assert.IsEmpty(Findings(parse, Semantics(("lead", DeclarationKind.Parameter, 3, [Person]))));
    }

    [TestMethod]
    public void WithoutTheHostsFacts_NothingIsReported()
    {
        var parse = Parse([Action("Place", MemberKind.Function, 3, 5, "order")],
            Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpPost));

        Assert.IsEmpty(Findings(parse, null));
    }
}
