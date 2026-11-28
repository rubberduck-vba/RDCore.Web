using Microsoft.AspNetCore.Builder;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using RDCore.Web.Endpoints;
using RDCore.Web.Hosting;
using RDCore.Web.Machines;
using System.Net;

namespace RDCore.Web.Tests.Hosting;

/// <summary>
/// The endpoints of a controller that keeps a machine for each key (<see cref="RDWebKeptMachines"/>), served through Kestrel: the response to a request
/// that reaches its key's machine, and to one that reaches none. The machines' answers are the test's.
/// </summary>
[TestClass]
public sealed class RDWebKeptMachinesHostingTests
{
    private static readonly RDWebMachinePoolOptions OneMachine = new() { MinMachines = 1, MaxMachines = 1 };

    public TestContext TestContext { get; set; } = null!;

    private static RDWebEndpoint Kept(string annotation, string procedure, string route, bool makesMachine = false)
        => RDWebEndpoint.FromAnnotation("GamesController", "api/games/{game}", procedure, [], annotation, route, keptMachineKey: "game",
            makesMachine: makesMachine)!;

    private static readonly RDWebEndpoint NewGame = Kept(RDWebAnnotations.HttpPost, "NewGame", "new", makesMachine: true);

    private static readonly RDWebEndpoint Events = Kept(RDWebAnnotations.HttpGet, "Events", "events");

    // the application, the pool it serves the endpoints that keep no machine from (none here, and its machine is never asked), and the kept machines.
    private sealed class Served(RunningApplication app, RDWebMachinePool pool, RDWebKeptMachines kept, ScriptedProgram program) : IAsyncDisposable
    {
        public HttpClient Client => app.Client;

        public ScriptedProgram Program => program;

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            await kept.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    private async Task<Served> ServeAsync(Func<ProgramInvocation, ProgramInvocationResult> answer, int maxMachines = 10)
    {
        var program = new ScriptedProgram(() => new ScriptedMachine(answer));
        var pool = await RDWebMachinePool.StartAsync(new ScriptedProgram(() => new ScriptedMachine(_ => ProgramInvocationResult.Completed())), OneMachine,
            TestContext.CancellationToken);
        var kept = new RDWebKeptMachines(program, new RDWebKeptMachinesOptions { MaxMachines = maxMachines });
        var app = await RunningApplication.StartAsync(app => app.MapRDWebEndpoints([NewGame, Events], pool, kept), TestContext.CancellationToken);
        return new Served(app, pool, kept, program);
    }

    [TestMethod]
    public async Task ARequestThatMakesItsKeysMachine_RunsOnIt_AndTheKeysLaterRequestsRunOnTheSameMachine()
    {
        await using var served = await ServeAsync(_ => ProgramInvocationResult.Completed());

        using var made = await served.Client.PostAsync("api/games/g1/new", null, TestContext.CancellationToken);
        using var events = await served.Client.GetAsync("api/games/g1/events", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, made.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, events.StatusCode);
        var machine = served.Program.Made.Single();
        CollectionAssert.AreEqual(new[] { "NewGame", "Events" }, machine.Invocations.Select(invocation => invocation.Procedure).ToArray());
    }

    [TestMethod]
    public async Task ARequestOfAKeyWithNoMachine_AnswersNotFound_AndMakesNone()
    {
        await using var served = await ServeAsync(_ => ProgramInvocationResult.Completed());

        using var response = await served.Client.GetAsync("api/games/g1/events", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.IsEmpty(served.Program.Made);
    }

    [TestMethod]
    public async Task ARequestThatMakesAMachineForAKeyThatHasOne_AnswersConflict_AndRunsNothing()
    {
        await using var served = await ServeAsync(_ => ProgramInvocationResult.Completed());
        using var made = await served.Client.PostAsync("api/games/g1/new", null, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, made.StatusCode, "the key's first machine is made");

        using var again = await served.Client.PostAsync("api/games/g1/new", null, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Conflict, again.StatusCode);
        Assert.AreEqual(1, served.Program.Made.Single().Invocations.Count);
    }

    [TestMethod]
    public async Task ARequestThatWouldMakeOneMachineMoreThanTheMaximum_AnswersServiceUnavailable_WithWhenToRetry()
    {
        await using var served = await ServeAsync(_ => ProgramInvocationResult.Completed(), maxMachines: 1);
        using var made = await served.Client.PostAsync("api/games/g1/new", null, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, made.StatusCode, "the one machine allowed is made");

        using var response = await served.Client.PostAsync("api/games/g2/new", null, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsGreaterThan(TimeSpan.Zero, response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero, "the response says how long to wait");
    }

    [TestMethod]
    public async Task AProcedureThatEndsItsMachine_AnswersNoContent_AndItsKeyHasNoMachineAfterwards()
    {
        await using var served = await ServeAsync(invocation => invocation.Procedure == "Events"
            ? ProgramInvocationResult.Ended(ExecutionOutcome.Halted, "End")
            : ProgramInvocationResult.Completed());
        using var made = await served.Client.PostAsync("api/games/g1/new", null, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, made.StatusCode, "the key's machine is made");

        using var ended = await served.Client.GetAsync("api/games/g1/events", TestContext.CancellationToken);
        using var after = await served.Client.GetAsync("api/games/g1/events", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, ended.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, after.StatusCode);
    }

    [TestMethod]
    public async Task EndpointsThatKeepMachines_MappedWithNoKeptMachines_AreAnArgumentException()
    {
        await using var pool = await RDWebMachinePool.StartAsync(new ScriptedProgram(() => new ScriptedMachine(_ => ProgramInvocationResult.Completed())),
            OneMachine, TestContext.CancellationToken);
        await using var app = WebApplication.CreateSlimBuilder().Build();

        Assert.ThrowsExactly<ArgumentException>(() => app.MapRDWebEndpoints([NewGame, Events], pool));
    }
}
