using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Workspace;
using RDCore.Web.Endpoints;

namespace RDCore.Web.Tests.Endpoints;

/// <summary>
/// The endpoints a module's annotations declare (<see cref="RDWebEndpointReader"/>). The parses are built as the parser builds them: each annotation inside
/// the comment line that holds it, each parameter a child of its procedure.
/// </summary>
[TestClass]
public sealed class RDWebEndpointReaderTests
{
    private static readonly Uri File = new("file:///c:/app/Greetings.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last) => new() { Uri = File, Range = new SourceRange(first, 0, last, 40) };

    private static MemberDeclarationNode Procedure(string name, int first, int last, MemberKind kind = MemberKind.Function,
        AccessModifier access = AccessModifier.Public, params string[] parameters)
        => new(Id(), Lines(first, last), [.. parameters.Select(Parameter)], name, kind, access);

    private static ParameterDeclarationNode Parameter(string name) => new(Id(), Lines(0, 0), name);

    // a comment line that holds one annotation, as the parser keeps it: the comment, then the annotation inside its range.
    private static SyntaxNode[] Annotated(int line, string name, string? route = null)
    {
        SyntaxNode[] arguments = route is null ? [] : [new LiteralExpressionNode(Id(), Lines(line, line), new VBStringValue(route))];
        return [new CommentTriviaNode(Id(), Lines(line, line), $"'@{name}"), new AnnotationTriviaNode(Id(), Lines(line, line), name, [.. arguments])];
    }

    private static ModuleParseResult Parse(SyntaxNode[] children, params SyntaxNode[][] trivia)
        => new() { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia.SelectMany(nodes => nodes)] };

    [TestMethod]
    public void AnHttpGetProcedureOfAController_IsAnEndpoint_OnTheControllersRouteAndItsOwn()
    {
        var parse = Parse([Procedure("Hello", 3, 6, parameters: "name")],
            Annotated(0, RDWebAnnotations.Controller, "api"), Annotated(2, RDWebAnnotations.HttpGet, "hello/{name}"));

        var endpoint = RDWebEndpointReader.Read(parse, "Greetings").Single();

        Assert.AreEqual("GET", endpoint.HttpMethod);
        Assert.AreEqual("/api/hello/{name}", endpoint.Route);
        Assert.AreEqual("Greetings", endpoint.ModuleName);
        Assert.AreEqual("Hello", endpoint.ProcedureName);
        CollectionAssert.AreEqual(new[] { "name" }, endpoint.RouteParameters);
    }

    [TestMethod]
    public void AControllerWithNoRoute_RoutesItsEndpointsUnderTheModulesName()
    {
        var parse = Parse([Procedure("Hello", 3, 6)], Annotated(0, RDWebAnnotations.Controller), Annotated(2, RDWebAnnotations.HttpGet, "hello"));

        Assert.AreEqual("/Greetings/hello", RDWebEndpointReader.Read(parse, "Greetings").Single().Route);
    }

    [TestMethod]
    public void AModuleThatIsNoController_DeclaresNoEndpoint()
    {
        var parse = Parse([Procedure("Hello", 3, 6)], Annotated(2, RDWebAnnotations.HttpGet, "hello"));

        Assert.IsEmpty(RDWebEndpointReader.Read(parse, "Greetings"));
    }

    [TestMethod]
    public void AControllerAnnotationAfterTheFirstProcedure_DoesNotMakeTheModuleAController()
    {
        var parse = Parse([Procedure("Hello", 1, 3), Procedure("Goodbye", 6, 8)],
            Annotated(0, RDWebAnnotations.HttpGet, "hello"),
            Annotated(4, RDWebAnnotations.Controller, "api"),
            Annotated(5, RDWebAnnotations.HttpGet, "goodbye"));

        Assert.IsEmpty(RDWebEndpointReader.Read(parse, "Greetings"));
    }

    [TestMethod]
    [DataRow(AccessModifier.Private)]
    [DataRow(AccessModifier.Friend)]
    public void AProcedureThatIsNotPublic_ServesNoEndpoint(AccessModifier access)
    {
        var parse = Parse([Procedure("Hello", 3, 6, access: access)],
            Annotated(0, RDWebAnnotations.Controller, "api"), Annotated(2, RDWebAnnotations.HttpGet, "hello"));

        Assert.IsEmpty(RDWebEndpointReader.Read(parse, "Greetings"));
    }

    [TestMethod]
    public void AProcedureWithAnImplicitAccessModifier_IsPublic_AndASubServesAnEndpointToo()
    {
        var parse = Parse([Procedure("Ping", 3, 5, MemberKind.Procedure, AccessModifier.Implicit)],
            Annotated(0, RDWebAnnotations.Controller, "api"), Annotated(2, RDWebAnnotations.HttpPost, "ping"));

        Assert.AreEqual("POST", RDWebEndpointReader.Read(parse, "Greetings").Single().HttpMethod);
    }

    [TestMethod]
    public void EachHttpMethodAnnotationOfAProcedure_IsAnEndpoint_InTheOrderWritten_AndOtherAnnotationsAreNone()
    {
        var parse = Parse([Procedure("Hello", 5, 8, parameters: "name")],
            Annotated(0, RDWebAnnotations.Controller, "api"),
            Annotated(2, RDWebAnnotations.HttpGet, "hello/{name}"),
            Annotated(3, "Description", "Greets."),
            Annotated(4, RDWebAnnotations.HttpPost, "hello"));

        var endpoints = RDWebEndpointReader.Read(parse, "Greetings");

        var routes = endpoints.Select(endpoint => $"{endpoint.HttpMethod} {endpoint.Route}").ToArray();
        CollectionAssert.AreEqual(new[] { "GET /api/hello/{name}", "POST /api/hello" }, routes);
        Assert.AreEqual("name", endpoints[1].BodyParameter);
    }

    [TestMethod]
    public void AFromBodyAnnotationOfAProcedure_NamesTheBodyParameterOfItsPost_AndItsGetBindsNone()
    {
        var parse = Parse([Procedure("Save", 5, 8, parameters: ["id", "notify", "order"])],
            Annotated(0, RDWebAnnotations.Controller, "api"),
            Annotated(2, RDWebAnnotations.HttpGet, "orders/{id}"),
            Annotated(3, RDWebAnnotations.HttpPost, "orders/{id}"),
            Annotated(4, RDWebAnnotations.FromBody, "order"));

        var endpoints = RDWebEndpointReader.Read(parse, "Orders");

        Assert.IsNull(endpoints[0].BodyParameter);
        Assert.AreEqual("order", endpoints[1].BodyParameter, "without '@FromBody, the body binds notify, the first parameter the route does not bind");
    }

    [TestMethod]
    public void InAFileOfBlocks_AControllerAnnotationAboveABlocksHeader_MakesThatBlocksModuleAController()
    {
        // 0-1 above Greetings' header; 2 the header; 3 above Hello; 4-6 Hello; 7 the end. 8 Other's header; 9 above Bye; 10-11 Bye; 12 the end.
        BlockModule Block(string name, int header, int end, params SyntaxNode[] children)
            => new(name, ModuleType.StdModule, RDCoreDocument.BlockUri(File, name), new SourceRange(header, 0, end, 10),
                new ModuleNode(Id(), Lines(header + 1, end), [.. children]));
        BlockBoundaryTriviaNode Boundary(int line, bool isEnd) => new(Id(), Lines(line, line), FileBlockKind.Module, isEnd, "Module");

        var greetings = Block("Greetings", 2, 7, Procedure("Hello", 4, 6));
        var other = Block("Other", 8, 12, Procedure("Bye", 10, 11));
        var parse = new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), Lines(0, 12), []),
            Trivia =
            [
                .. Annotated(0, RDWebAnnotations.Controller, "api"), .. Annotated(1, RDWebAnnotations.HttpGet, "above"), Boundary(2, false),
                .. Annotated(3, RDWebAnnotations.HttpGet, "hello"), Boundary(7, true),
                Boundary(8, false), .. Annotated(9, RDWebAnnotations.HttpGet, "bye"), Boundary(12, true),
            ],
            BlockModules = [greetings, other],
        };
        var document = new RDCoreDocument(File, parse);

        var endpoint = RDWebEndpointReader.Read(document.ParseOf(greetings.Uri)!, "Greetings").Single();

        Assert.AreEqual("GET /api/hello", $"{endpoint.HttpMethod} {endpoint.Route}", "an annotation above the header is no member's");
        Assert.IsEmpty(RDWebEndpointReader.Read(document.ParseOf(other.Uri)!, "Other"), "the controller is the block's below it only");
        Assert.IsEmpty(RDWebEndpointReader.Read(document.ParseOf(File)!, "Greetings"));
    }
}
