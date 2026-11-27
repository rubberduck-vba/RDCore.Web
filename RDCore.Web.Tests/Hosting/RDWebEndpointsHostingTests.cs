using RDCore.SDK.Platform.Protocol;
using RDCore.Web.Endpoints;
using RDCore.Web.Hosting;
using RDCore.Web.Machines;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RDCore.Web.Tests.Hosting;

/// <summary>
/// The endpoints of an RD-VBA application served through Kestrel (<see cref="RDWebEndpointRouteBuilderExtensions.MapRDWebEndpoints"/>): what a request
/// binds into the procedure's arguments, and the response each outcome of the procedure answers. The machine's answers are the test's.
/// </summary>
[TestClass]
public sealed class RDWebEndpointsHostingTests
{
    public TestContext TestContext { get; set; } = null!;

    private static RDWebEndpoint Endpoint(string annotation, string route, params string[] parameters)
        => RDWebEndpoint.FromAnnotation("OrdersController", "api/orders", "Serve", parameters, annotation, route)!;

    private Task<RunningApplication> StartAsync(IRDWebMachine machine, params RDWebEndpoint[] endpoints)
        => RunningApplication.StartAsync(app => app.MapRDWebEndpoints(endpoints, machine), TestContext.CancellationToken);

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse()
    {
        // what a Scripting.Dictionary of a name and a count is as JSON.
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Returned(new JsonObject { ["name"] = "Duck", ["count"] = 42 }));
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "summary"));

        using var response = await app.Client.GetAsync("api/orders/summary", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.CancellationToken);
        Assert.AreEqual("Duck", (string?)body?["name"]);
        Assert.AreEqual(42, (int?)body?["count"]);
        var invocation = machine.Invocations.Single();
        Assert.AreEqual("OrdersController", invocation.ModuleName);
        Assert.AreEqual("Serve", invocation.ProcedureName);
    }

    [TestMethod]
    public async Task AProcedureThatReturnsNull_AnswersJsonNull()
    {
        await using var app = await StartAsync(new ScriptedMachine(_ => RDWebInvocationResult.Returned(null)), Endpoint(RDWebAnnotations.HttpGet, "none"));

        using var response = await app.Client.GetAsync("api/orders/none", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("null", await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AProcedureThatReturnsNoValue_AnswersNoContent()
    {
        await using var app = await StartAsync(new ScriptedMachine(_ => RDWebInvocationResult.Completed()), Endpoint(RDWebAnnotations.HttpPost, "ping"));

        using var response = await app.Client.PostAsync("api/orders/ping", null, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task TheRouteValues_BindTheirParameters_AsStrings()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "{id}/lines/{line}", "id", "line"));

        using var response = await app.Client.GetAsync("api/orders/42/lines/A%20B", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var arguments = machine.Invocations.Single().Arguments;
        Assert.AreEqual(JsonValueKind.String, arguments["id"]!.GetValueKind());
        Assert.AreEqual("42", (string?)arguments["id"]);
        Assert.AreEqual("A B", (string?)arguments["LINE"], "parameters are named without regard to case");
    }

    [TestMethod]
    public async Task AQueryStringValue_BindsTheParameterOfItsName_AndAParameterNothingBinds_GetsNoArgument()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "{id}", "id", "format", "culture"));

        using var response = await app.Client.GetAsync("api/orders/7?format=short&other=1", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var arguments = machine.Invocations.Single().Arguments;
        Assert.AreEqual("short", (string?)arguments["format"]);
        Assert.IsFalse(arguments.ContainsKey("culture"));
        Assert.IsFalse(arguments.ContainsKey("other"), "a query value that names no parameter binds nothing");
    }

    [TestMethod]
    public async Task AQueryStringNameGivenTwice_BindsItsValuesJoinedByCommas()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "tagged", "tag"));

        using var response = await app.Client.GetAsync("api/orders/tagged?tag=a&tag=b", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("a,b", (string?)machine.Invocations.Single().Arguments["tag"]);
    }

    [TestMethod]
    public async Task APostedJsonBody_BindsTheBodyParameter_AsTheJsonItHolds()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Returned(JsonValue.Create(1)));
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpPost, "{id}/lines", "id", "line"));

        var posted = Json("""{ "item": "duck", "quantity": 3, "gift": true }""");
        using var response = await app.Client.PostAsync("api/orders/7/lines", posted, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var arguments = machine.Invocations.Single().Arguments;
        Assert.AreEqual("7", (string?)arguments["id"]);
        var line = arguments["line"]!.AsObject();
        Assert.AreEqual("duck", (string?)line["item"]);
        Assert.AreEqual(3, (int?)line["quantity"]);
        Assert.IsTrue((bool?)line["gift"]);
    }

    [TestMethod]
    public async Task APostedJsonBody_ReplacesAQueryStringValueOfTheSameName()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpPost, "lines", "line"));

        using var response = await app.Client.PostAsync("api/orders/lines?line=queried", Json("""{ "item": "duck" }"""), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var line = machine.Invocations.Single().Arguments["line"];
        Assert.AreEqual(JsonValueKind.Object, line!.GetValueKind(), $"the body binds the parameter, not the query string: {line.ToJsonString()}");
        Assert.AreEqual("duck", (string?)line["item"]);
    }

    [TestMethod]
    public async Task APostWithNoBody_ReadsNothing_AndItsBodyParameterBindsFromTheQueryString()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpPost, "lines", "line"));

        // Content-Length: 0, of a media type that a body would be refused for.
        var nothing = new StringContent(string.Empty, Encoding.UTF8, "text/plain");
        using var response = await app.Client.PostAsync("api/orders/lines?line=queried", nothing, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "no body is read, so its media type is not checked");
        Assert.AreEqual("queried", (string?)machine.Invocations.Single().Arguments["line"]);
    }

    [TestMethod]
    public async Task APostedJsonBody_BindsTheParameterFromBodyNames_AndTheQueryStringStillBindsTheOthers()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        var endpoint = RDWebEndpoint.FromAnnotation("OrdersController", "api/orders", "Serve", ["id", "notify", "line"], RDWebAnnotations.HttpPost,
            "{id}/lines", "line")!;
        await using var app = await StartAsync(machine, endpoint);

        using var response = await app.Client.PostAsync("api/orders/7/lines?notify=yes", Json("""{ "item": "duck" }"""), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var arguments = machine.Invocations.Single().Arguments;
        Assert.AreEqual("duck", (string?)arguments["line"]!["item"]);
        Assert.AreEqual("yes", (string?)arguments["notify"]);
    }

    [TestMethod]
    public async Task APostedBodyThatIsNotValidJson_AnswersBadRequest_AndRunsNothing()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpPost, "lines", "line"));

        using var response = await app.Client.PostAsync("api/orders/lines", Json("{ \"item\": "), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.IsEmpty(machine.Invocations);
    }

    [TestMethod]
    public async Task APostedBodyThatIsNotJson_AnswersUnsupportedMediaType_AndRunsNothing()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpPost, "lines", "line"));

        var text = new StringContent("item=duck", Encoding.UTF8, "text/plain");
        using var response = await app.Client.PostAsync("api/orders/lines", text, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.IsEmpty(machine.Invocations);
    }

    [TestMethod]
    public async Task ARunTimeError_AnswersInternalServerError_WithTheErrorsNumberAndCode()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.RuntimeError(11, "Division by zero", "VBR00011"));
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "ratio"));

        using var response = await app.Client.GetAsync("api/orders/ratio", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.CancellationToken);
        Assert.AreEqual(500, (int?)problem?["status"]);
        Assert.AreEqual(11, (int?)problem?["errorNumber"]);
        Assert.AreEqual("VBR00011", (string?)problem?["errorCode"]);
        Assert.AreEqual("Division by zero", (string?)problem?["detail"]);
    }

    [TestMethod]
    public async Task AMachineThatRefusesTheRequest_AnswersServiceUnavailable()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Ended(ExecutionOutcome.Refused, "A program is running."));
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "busy"));

        using var response = await app.Client.GetAsync("api/orders/busy", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [TestMethod]
    [DataRow(ExecutionOutcome.NotFound)]
    [DataRow(ExecutionOutcome.Halted)]
    [DataRow(ExecutionOutcome.NotImplemented)]
    public async Task AProcedureThatDoesNotComplete_AnswersInternalServerError(ExecutionOutcome outcome)
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Ended(outcome, "It ended."));
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "ended"));

        using var response = await app.Client.GetAsync("api/orders/ended", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task AnEndpoint_ServesOnlyItsOwnMethod()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        await using var app = await StartAsync(machine, Endpoint(RDWebAnnotations.HttpGet, "summary"));

        using var response = await app.Client.PostAsync("api/orders/summary", Json("{}"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.IsEmpty(machine.Invocations);
    }

    [TestMethod]
    public async Task AControllerWithNoRoute_ServesTheRequestsForItsNameLessTheControllerSuffix_InAnyCase()
    {
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Completed());
        var endpoint = RDWebEndpoint.FromAnnotation("ThingsController", null, "All", [], RDWebAnnotations.HttpGet, "all")!;
        await using var app = await StartAsync(machine, endpoint);

        using var response = await app.Client.GetAsync("things/all", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("ThingsController", machine.Invocations.Single().ModuleName);
    }

    [TestMethod]
    public async Task ThroughAPoolOfOneMachine_EveryRequestIsServed()
    {
        var calls = 0;
        var machine = new ScriptedMachine(_ => RDWebInvocationResult.Returned(JsonValue.Create(Interlocked.Increment(ref calls))));
        await using var app = await StartAsync(new RDWebMachinePool([machine]), Endpoint(RDWebAnnotations.HttpGet, "next"));

        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => app.Client.GetStringAsync("api/orders/next", TestContext.CancellationToken)));

        var numbers = Enumerable.Range(1, 8).Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        CollectionAssert.AreEquivalent(numbers, answers);
    }
}
