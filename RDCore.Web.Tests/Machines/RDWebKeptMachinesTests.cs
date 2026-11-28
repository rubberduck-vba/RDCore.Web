using Microsoft.Extensions.Time.Testing;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using RDCore.SDK.Workspace;
using RDCore.Web.Machines;
using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace RDCore.Web.Tests.Machines;

/// <summary>
/// The machines kept between requests, each for a key (<see cref="RDWebKeptMachines"/>): no reset, one request per machine at a time, the outcomes that end
/// a machine, the idle deadline, the request deadlines and the limit. What runs at once is set by the test (each held machine keeps its invocation until the
/// test releases it), and every deadline runs on a fake clock that only the test moves, so no outcome depends on timing.
/// </summary>
[TestClass]
public sealed class RDWebKeptMachinesTests
{
    private const string Game = "game-1";

    private static readonly ProgramInvocation Invocation = new("GamesController", "Attack", ImmutableDictionary<string, JsonNode?>.Empty);

    private static readonly RDWebKeptMachinesOptions Options = new();

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    private static ScriptedMachine Answering(ProgramInvocationResult result) => new(_ => result);

    private static ScriptedProgram Completing() => new(() => Answering(ProgramInvocationResult.Completed()));

    // a request to the registry, which the test's own hang guard (its token) ends if a broken registry never answers it.
    private Task<RDWebKeptMachineAnswer> InvokeAsync(RDWebKeptMachines kept, string key = Game, bool makesMachine = false)
        => kept.InvokeAsync(key, Invocation, makesMachine, Token).WaitAsync(Token);

    private Task<RDWebKeptMachineAnswer> MakeAsync(RDWebKeptMachines kept, string key = Game) => InvokeAsync(kept, key, makesMachine: true);

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatMakesAMachine_RunsOnIt_AndEveryLaterRequestOfItsKeyRunsOnTheSameMachine_NeverReset()
    {
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options);

        var made = await MakeAsync(kept);
        var later = await InvokeAsync(kept);
        await InvokeAsync(kept);

        Assert.AreEqual(RDWebKeptMachineStatus.Served, made.Status);
        Assert.AreEqual(ExecutionOutcome.Completed, later.Result?.Outcome);
        Assert.HasCount(1, maker.Made);
        Assert.HasCount(3, maker[0].Invocations);
        Assert.AreEqual(0, maker[0].Resets);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task TwoKeys_EachHaveAMachineOfTheirOwn()
    {
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options);

        await MakeAsync(kept, "game-1");
        await MakeAsync(kept, "game-2");
        await InvokeAsync(kept, "game-2");

        Assert.AreEqual(2, kept.Count);
        Assert.HasCount(1, maker[0].Invocations);
        Assert.HasCount(2, maker[1].Invocations);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestForAKeyWithNoMachine_ReachesNone()
    {
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options);

        var answer = await InvokeAsync(kept);

        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, answer.Status);
        Assert.IsNull(answer.Result);
        Assert.IsEmpty(maker.Made);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatMakesAMachineForAKeyThatHasOne_IsNotRun()
    {
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options);
        await MakeAsync(kept);

        var answer = await MakeAsync(kept);

        Assert.AreEqual(RDWebKeptMachineStatus.AlreadyMade, answer.Status);
        Assert.HasCount(1, maker.Made);
        Assert.HasCount(1, maker[0].Invocations);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatWouldMakeOneMachineMoreThanTheMaximum_IsRefused_AndTheKeptMachinesStay()
    {
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options with { MaxMachines = 2 });
        await MakeAsync(kept, "game-1");
        await MakeAsync(kept, "game-2");

        var answer = await MakeAsync(kept, "game-3");

        Assert.AreEqual(RDWebKeptMachineStatus.Full, answer.Status);
        Assert.HasCount(2, maker.Made);
        Assert.AreEqual(RDWebKeptMachineStatus.Served, (await InvokeAsync(kept, "game-1")).Status);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task TwoRequestsOfOneKey_RunOneAtATime_InTheOrderTheyArrive()
    {
        var held = new HeldMachine();
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => held.Machine), Options);

        var first = MakeAsync(kept);
        await held.Entered.Reader.ReadAsync(Token);
        var second = InvokeAsync(kept);

        Assert.HasCount(1, held.Machine.Invocations, "the second request waits for the machine");
        held.Release.SetResult();
        await Task.WhenAll(first, second);

        Assert.HasCount(2, held.Machine.Invocations);
        Assert.AreEqual(1, held.MostAtOnce);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestOfAKeyWhoseMachineIsBeingMade_WaitsForIt_AndRunsOnIt()
    {
        var making = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var machine = Answering(ProgramInvocationResult.Completed());
        await using var kept = new RDWebKeptMachines(new SlowProgram(making.Task, machine), Options);

        var first = MakeAsync(kept);
        var second = InvokeAsync(kept);
        Assert.IsFalse(second.IsCompleted, "the machine of its key is being made");
        making.SetResult();
        await Task.WhenAll(first, second);

        Assert.AreEqual(RDWebKeptMachineStatus.Served, (await second).Status);
        Assert.HasCount(2, machine.Invocations);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(ExecutionOutcome.Completed)]
    [DataRow(ExecutionOutcome.RuntimeError)]
    [DataRow(ExecutionOutcome.NotFound)]
    public async Task AMachineWhoseRequestEndedAsARunEnds_IsKept(ExecutionOutcome outcome)
    {
        var maker = new ScriptedProgram(() => Answering(ResultOf(outcome)));
        await using var kept = new RDWebKeptMachines(maker, Options);

        await MakeAsync(kept);
        var answer = await InvokeAsync(kept);

        Assert.AreEqual(outcome, answer.Result?.Outcome);
        Assert.HasCount(2, maker[0].Invocations);
        Assert.IsFalse(maker[0].IsDisposed);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(ExecutionOutcome.Halted)]
    [DataRow(ExecutionOutcome.Interrupted)]
    [DataRow(ExecutionOutcome.Suspended)]
    [DataRow(ExecutionOutcome.Refused)]
    public async Task AMachineWhoseRequestEndedAnyOtherWay_IsDisposed_AndItsKeyHasNoMachine(ExecutionOutcome outcome)
    {
        var maker = new ScriptedProgram(() => Answering(ResultOf(outcome)));
        await using var kept = new RDWebKeptMachines(maker, Options);

        var answer = await MakeAsync(kept);

        Assert.AreEqual(outcome, answer.Result?.Outcome, "the request is answered as its machine answered it");
        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, (await InvokeAsync(kept)).Status);
        Assert.AreEqual(0, kept.Count);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatWaitsForAMachineThatThenEnds_ReachesNone()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // the request that makes the machine ends it, with an End statement, once the test releases it.
        var machine = new ScriptedMachine(async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return ProgramInvocationResult.Ended(ExecutionOutcome.Halted, "End.");
        });
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => machine), Options);
        var first = MakeAsync(kept);
        await entered.Task.WaitAsync(Token);

        var waiting = InvokeAsync(kept);
        release.SetResult();
        await first;

        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, (await waiting).Status);
        Assert.HasCount(1, machine.Invocations);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachineWhoseInvocationThrows_IsDisposed_AndItsKeyHasNoMachine()
    {
        var calls = 0;
        var maker = new ScriptedProgram(() => new ScriptedMachine(_ =>
            Interlocked.Increment(ref calls) == 2 ? throw new InvalidOperationException("the session failed") : ProgramInvocationResult.Completed()));
        await using var kept = new RDWebKeptMachines(maker, Options);
        await MakeAsync(kept);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => InvokeAsync(kept));

        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, (await InvokeAsync(kept)).Status);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachineThatCannotBeMade_LeavesItsKeyFree()
    {
        var makes = 0;
        var maker = new ScriptedProgram(() =>
            Interlocked.Increment(ref makes) == 1 ? throw new InvalidOperationException("no session") : Answering(ProgramInvocationResult.Completed()));
        await using var kept = new RDWebKeptMachines(maker, Options);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => MakeAsync(kept));

        Assert.AreEqual(0, kept.Count);
        Assert.AreEqual(RDWebKeptMachineStatus.Served, (await MakeAsync(kept)).Status);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachineWithNoRequestForItsIdleTimeout_IsDisposed_AndEachRequestRestartsTheTimeout()
    {
        var time = new FakeTimeProvider();
        var maker = Completing();
        await using var kept = new RDWebKeptMachines(maker, Options, time);
        var almost = Options.IdleTimeout - TimeSpan.FromSeconds(1);
        await MakeAsync(kept);

        time.Advance(almost);
        await InvokeAsync(kept);
        time.Advance(almost);
        Assert.IsFalse(maker[0].IsDisposed, "the second request restarted the idle timeout");

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, (await InvokeAsync(kept)).Status);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachineServingARequest_IsNotIdle()
    {
        var time = new FakeTimeProvider();
        var options = Options with { IdleTimeout = TimeSpan.FromSeconds(10), RequestTimeout = TimeSpan.FromMinutes(1) };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        // the request that makes the machine ends at once; the next one holds the machine until the test releases it.
        var machine = new ScriptedMachine(async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                entered.SetResult();
                await release.Task;
            }

            return ProgramInvocationResult.Completed();
        });
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => machine), options, time);
        await MakeAsync(kept);
        Assert.AreEqual(1, kept.Count, "the machine is kept, for the request that holds it");

        var request = InvokeAsync(kept);
        await entered.Task.WaitAsync(Token);
        time.Advance(options.IdleTimeout * 2);

        Assert.IsFalse(machine.IsDisposed, "its request holds it");
        release.SetResult();
        await request;
        Assert.AreEqual(RDWebKeptMachineStatus.Served, (await InvokeAsync(kept)).Status);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatWaitsForItsMachinePastItsDeadline_IsRefused_AndNeverRuns()
    {
        var time = new FakeTimeProvider();
        var held = new HeldMachine(stopsWhenCancelled: false);
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => held.Machine), Options, time);
        var first = MakeAsync(kept);
        await held.Entered.Reader.ReadAsync(Token);

        var waiting = InvokeAsync(kept);
        time.Advance(Options.RequestTimeout);

        var answer = await waiting;
        Assert.AreEqual(RDWebKeptMachineStatus.Served, answer.Status);
        Assert.AreEqual(ExecutionOutcome.Refused, answer.Result?.Outcome);
        Assert.HasCount(1, held.Machine.Invocations);
        held.Release.SetResult();
        await first;
        Assert.AreEqual(1, kept.Count, "a refused request does not end the machine");
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestCancelledWhileItWaits_DoesNotRun()
    {
        var held = new HeldMachine();
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => held.Machine), Options);
        var first = MakeAsync(kept);
        await held.Entered.Reader.ReadAsync(Token);

        using var cancelled = new CancellationTokenSource();
        var waiting = kept.InvokeAsync(Game, Invocation, makesMachine: false, cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);

        held.Release.SetResult();
        await first;
        await InvokeAsync(kept);
        Assert.HasCount(2, held.Machine.Invocations, "the cancelled request never reached the machine");
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestWhoseMachineDoesNotStopAfterItsDeadline_AnswersInterrupted_AndItsMachineIsDisposedOnceItEnds()
    {
        var time = new FakeTimeProvider();
        var stuck = new HeldMachine(stopsWhenCancelled: false);
        await using var kept = new RDWebKeptMachines(new ScriptedProgram(() => stuck.Machine), Options, time);

        var request = MakeAsync(kept);
        await stuck.Entered.Reader.ReadAsync(Token);
        time.Advance(Options.RequestTimeout + Options.StopTimeout);
        var answer = await request;

        Assert.AreEqual(ExecutionOutcome.Interrupted, answer.Result?.Outcome);
        Assert.AreEqual(RDWebKeptMachineStatus.NoMachine, (await InvokeAsync(kept)).Status);
        Assert.IsFalse(stuck.Machine.IsDisposed, "an abandoned machine is disposed only once it ends");
        stuck.Release.SetResult();
        await stuck.Machine.Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task DisposingTheRegistry_DisposesAFreeMachineNow_AndABusyOneOnceItsRequestEnds()
    {
        var held = new HeldMachine();
        var free = Answering(ProgramInvocationResult.Completed());
        var made = 0;
        var kept = new RDWebKeptMachines(new ScriptedProgram(() => Interlocked.Increment(ref made) == 1 ? free : held.Machine), Options);
        await MakeAsync(kept, "game-1");
        var request = MakeAsync(kept, "game-2");
        await held.Entered.Reader.ReadAsync(Token);

        await kept.DisposeAsync();

        Assert.IsTrue(free.IsDisposed);
        Assert.IsFalse(held.Machine.IsDisposed, "a machine serving a request is disposed once it ends");
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => InvokeAsync(kept, "game-1"));
        held.Release.SetResult();
        await request;
        await held.Machine.Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [DataRow(nameof(RDWebKeptMachinesOptions.MaxMachines))]
    [DataRow(nameof(RDWebKeptMachinesOptions.IdleTimeout))]
    [DataRow(nameof(RDWebKeptMachinesOptions.RequestTimeout))]
    [DataRow(nameof(RDWebKeptMachinesOptions.StopTimeout))]
    public void ARegistryWithALimitThatIsNotPositive_IsRefused(string limit)
    {
        var options = limit switch
        {
            nameof(RDWebKeptMachinesOptions.MaxMachines) => Options with { MaxMachines = 0 },
            nameof(RDWebKeptMachinesOptions.IdleTimeout) => Options with { IdleTimeout = TimeSpan.Zero },
            nameof(RDWebKeptMachinesOptions.RequestTimeout) => Options with { RequestTimeout = TimeSpan.Zero },
            _ => Options with { StopTimeout = TimeSpan.Zero },
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RDWebKeptMachines(Completing(), options));
    }

    private static ProgramInvocationResult ResultOf(ExecutionOutcome outcome) => outcome switch
    {
        ExecutionOutcome.Completed => ProgramInvocationResult.Completed(),
        ExecutionOutcome.RuntimeError => ProgramInvocationResult.RuntimeError(11, "Division by zero", "VBR00011"),
        _ => ProgramInvocationResult.Ended(outcome, "It ended."),
    };

    // a program that makes its one machine once the test lets it.
    private sealed class SlowProgram(Task made, ScriptedMachine machine) : IComposedProgram
    {
        public IReadOnlyList<RDCoreDocument> Documents { get; } = [];

        public async ValueTask<IProgramMachine> CreateMachineAsync(CancellationToken cancellationToken)
        {
            await made.WaitAsync(cancellationToken);
            return machine;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
