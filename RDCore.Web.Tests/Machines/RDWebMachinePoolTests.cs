using Microsoft.Extensions.Time.Testing;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using RDCore.Web.Machines;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace RDCore.Web.Tests.Machines;

/// <summary>
/// The pool of the machines of a program (<see cref="RDWebMachinePool"/>): one request per machine at a time, a reset after each, recycling, deadlines and
/// growth. What runs at once is set by the test (each held machine keeps its invocation until the test releases it), and every deadline runs on a fake clock
/// that only the test moves, so no outcome depends on timing.
/// </summary>
[TestClass]
public sealed class RDWebMachinePoolTests
{
    private static readonly ProgramInvocation Invocation = new("OrdersController", "GetOrder", ImmutableDictionary<string, JsonNode?>.Empty);

    private static readonly RDWebMachinePoolOptions OneMachine = new() { MinMachines = 1, MaxMachines = 1 };

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    private static ScriptedMachine Answering(ProgramInvocationResult result) => new(_ => result);

    // a request to the pool, which the test's own hang guard (its token) ends if a broken pool never answers it.
    private Task<ProgramInvocationResult> InvokeAsync(RDWebMachinePool pool) => pool.InvokeAsync(Invocation, Token).WaitAsync(Token);

    private Task<RDWebMachinePool> StartAsync(ScriptedProgram maker, RDWebMachinePoolOptions options, TimeProvider? time = null)
        => RDWebMachinePool.StartAsync(maker, options, Token, time);

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task APoolOfOneMachine_RunsASecondRequestOnlyOnceTheFirstHasReturned()
    {
        var held = new HeldMachine();
        await using var pool = await StartAsync(new ScriptedProgram(() => held.Machine), OneMachine);

        var first = InvokeAsync(pool);
        await held.Entered.Reader.ReadAsync(Token);
        var second = InvokeAsync(pool);

        Assert.HasCount(1, held.Machine.Invocations, "the second request waits for the machine");
        held.Release.SetResult();
        await Task.WhenAll(first, second);

        Assert.HasCount(2, held.Machine.Invocations);
        Assert.AreEqual(1, held.MostAtOnce);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task APoolOfTwoMachines_RunsTwoRequestsAtOnce_EachOnAMachineOfItsOwn()
    {
        var held = new ConcurrentQueue<HeldMachine>();
        var entered = Channel.CreateUnbounded<bool>();
        await using var pool = await StartAsync(new ScriptedProgram(() =>
        {
            var machine = new HeldMachine(entered: entered);
            held.Enqueue(machine);
            return machine.Machine;
        }), new RDWebMachinePoolOptions { MinMachines = 2, MaxMachines = 2 });

        var first = InvokeAsync(pool);
        var second = InvokeAsync(pool);
        await entered.Reader.ReadAsync(Token);
        await entered.Reader.ReadAsync(Token);

        foreach (var machine in held)
        {
            machine.Release.SetResult();
        }

        await Task.WhenAll(first, second);
        Assert.IsTrue(held.All(machine => machine.MostAtOnce == 1 && machine.Machine.Invocations.Count == 1));
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ThePool_MakesAMachineForARequestThatFindsNoneIdle_UpToItsMaximum()
    {
        var held = new ConcurrentQueue<HeldMachine>();
        var entered = Channel.CreateUnbounded<bool>();
        var maker = new ScriptedProgram(() =>
        {
            var machine = new HeldMachine(entered: entered);
            held.Enqueue(machine);
            return machine.Machine;
        });
        await using var pool = await StartAsync(maker, new RDWebMachinePoolOptions { MinMachines = 1, MaxMachines = 2 });

        var first = InvokeAsync(pool);
        var second = InvokeAsync(pool);
        var third = InvokeAsync(pool);
        await entered.Reader.ReadAsync(Token);
        await entered.Reader.ReadAsync(Token);

        Assert.IsFalse(third.IsCompleted, "the third request waits: the pool holds its maximum");
        Assert.HasCount(2, maker.Made);
        Assert.AreEqual(2, pool.Size);
        held.First().Release.SetResult();
        held.Last().Release.SetResult();
        await Task.WhenAll(first, second, third);
        Assert.AreEqual(3, held.Sum(machine => machine.Machine.Invocations.Count));
        Assert.HasCount(2, maker.Made);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestCancelledWhileItWaits_DoesNotRun_AndTheMachineServesTheNextOne()
    {
        var held = new HeldMachine();
        await using var pool = await StartAsync(new ScriptedProgram(() => held.Machine), OneMachine);
        var first = InvokeAsync(pool);
        await held.Entered.Reader.ReadAsync(Token);

        using var cancelled = new CancellationTokenSource();
        var waiting = pool.InvokeAsync(Invocation, cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);

        held.Release.SetResult();
        await first;
        await InvokeAsync(pool);
        Assert.HasCount(2, held.Machine.Invocations, "the cancelled request never reached the machine");
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AfterEachRequest_TheMachineIsReset_BeforeItServesTheNextOne()
    {
        var log = new ConcurrentQueue<string>();
        var machine = new ScriptedMachine(_ =>
        {
            log.Enqueue("invoke");
            return ProgramInvocationResult.Completed();
        })
        {
            Reset = _ =>
            {
                log.Enqueue("reset");
                return Task.FromResult(true);
            },
        };
        await using var pool = await StartAsync(new ScriptedProgram(() => machine), OneMachine);

        await InvokeAsync(pool);
        await InvokeAsync(pool);

        CollectionAssert.AreEqual(new[] { "invoke", "reset", "invoke" }, log.Take(3).ToArray());
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(ExecutionOutcome.Completed)]
    [DataRow(ExecutionOutcome.RuntimeError)]
    [DataRow(ExecutionOutcome.Halted)]
    [DataRow(ExecutionOutcome.NotFound)]
    public async Task AMachineWhoseRequestEndedNormally_IsResetAndServesTheNextOne(ExecutionOutcome outcome)
    {
        var maker = new ScriptedProgram(() => Answering(ResultOf(outcome)));
        await using var pool = await StartAsync(maker, OneMachine);

        await InvokeAsync(pool);
        await InvokeAsync(pool);

        Assert.HasCount(1, maker.Made);
        Assert.HasCount(2, maker[0].Invocations);
        Assert.IsFalse(maker[0].IsDisposed);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(ExecutionOutcome.Interrupted)]
    [DataRow(ExecutionOutcome.Suspended)]
    [DataRow(ExecutionOutcome.Refused)]
    public async Task AMachineWhoseRequestDidNotEnd_IsRecycled_AndAnotherServesTheNextOne(ExecutionOutcome outcome)
    {
        var maker = new ScriptedProgram(() => Answering(ResultOf(outcome)));
        await using var pool = await StartAsync(maker, OneMachine);

        await InvokeAsync(pool);
        await InvokeAsync(pool);

        // the second request's machine is recycled too, so a third may be made already.
        Assert.IsTrue(maker.Made.Count >= 2, "the second request ran on another machine");
        Assert.HasCount(1, maker[1].Invocations);
        Assert.AreEqual(0, maker[0].Resets, "a recycled machine is not reset");
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachineWhoseInvocationThrows_IsRecycled_AndAnotherServesTheNextOne()
    {
        var calls = 0;
        var maker = new ScriptedProgram(() => new ScriptedMachine(_ =>
            Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("the session failed") : ProgramInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => InvokeAsync(pool));
        var result = await InvokeAsync(pool);

        Assert.AreEqual(ExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(2, maker.Made, "the second request ran on another machine");
        Assert.HasCount(1, maker[1].Invocations);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AMachine_IsRecycledOnceItHasServedRecycleAfterRequests()
    {
        var maker = new ScriptedProgram(() => Answering(ProgramInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine with { RecycleAfter = 2 });

        for (var request = 0; request < 3; request++)
        {
            await InvokeAsync(pool);
        }

        Assert.HasCount(2, maker.Made, "the third request ran on another machine");
        Assert.HasCount(2, maker[0].Invocations);
        Assert.HasCount(1, maker[1].Invocations);
        Assert.AreEqual(1, maker[0].Resets, "reset between its two requests, not after its last");
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AMachineWhoseResetDoesNotComplete_IsRecycled(bool resetThrows)
    {
        var maker = new ScriptedProgram(() => new ScriptedMachine(_ => ProgramInvocationResult.Completed())
        {
            Reset = _ => resetThrows ? Task.FromException<bool>(new InvalidOperationException("Terminate failed")) : Task.FromResult(false),
        });
        await using var pool = await StartAsync(maker, OneMachine);

        await InvokeAsync(pool);
        await InvokeAsync(pool);

        // the second machine's reset fails too, so a third may be made already.
        Assert.IsTrue(maker.Made.Count >= 2, "the second request ran on another machine");
        Assert.HasCount(1, maker[1].Invocations);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task AResetPastItsDeadline_IsCancelled_AndAMachineThatThenDoesNotStop_IsAbandonedAndReplaced()
    {
        var time = new FakeTimeProvider();
        var options = OneMachine;
        var resetting = Channel.CreateUnbounded<CancellationToken>();
        var stuck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resets = 0;
        var maker = new ScriptedProgram(() => new ScriptedMachine(_ => ProgramInvocationResult.Completed())
        {
            Reset = token =>
            {
                if (Interlocked.Increment(ref resets) > 1)
                {
                    return Task.FromResult(true);
                }

                resetting.Writer.TryWrite(token);
                return stuck.Task;
            },
        });
        await using var pool = await StartAsync(maker, options, time);
        try
        {
            await InvokeAsync(pool);
            var resetToken = await resetting.Reader.ReadAsync(Token);
            time.Advance(options.ResetTimeout);
            Assert.IsTrue(resetToken.IsCancellationRequested, "the reset's deadline cancels it");
            time.Advance(options.StopTimeout);
            await InvokeAsync(pool);

            Assert.AreEqual(1, pool.Abandoned);
            Assert.AreEqual(1, pool.Size);
            Assert.HasCount(1, maker[1].Invocations);
            Assert.IsFalse(maker[0].IsDisposed, "an abandoned machine is disposed only once it ends");
        }
        finally
        {
            // a reset still held would hold the pool's disposal.
            stuck.TrySetResult(true);
        }

        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestPastItsDeadline_IsCancelled_AndItsMachineRecycled()
    {
        var time = new FakeTimeProvider();
        var held = new ConcurrentQueue<HeldMachine>();
        var maker = new ScriptedProgram(() =>
        {
            var machine = new HeldMachine();
            held.Enqueue(machine);
            return machine.Machine;
        });
        await using var pool = await StartAsync(maker, OneMachine, time);

        var request = InvokeAsync(pool);
        await held.First().Entered.Reader.ReadAsync(Token);
        time.Advance(OneMachine.RequestTimeout);

        Assert.AreEqual(ExecutionOutcome.Interrupted, (await request).Outcome);
        await maker[0].Disposed.WaitAsync(Token);
        Assert.AreEqual(0, pool.Abandoned, "the machine stopped when it was cancelled");
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestWhoseMachineDoesNotStopAfterItsDeadline_AnswersInterrupted_AndItsMachineIsAbandonedAndReplaced()
    {
        var time = new FakeTimeProvider();
        var stuck = new HeldMachine(stopsWhenCancelled: false);
        var made = 0;
        var maker = new ScriptedProgram(() => Interlocked.Increment(ref made) == 1 ? stuck.Machine : Answering(ProgramInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine, time);

        var request = InvokeAsync(pool);
        await stuck.Entered.Reader.ReadAsync(Token);
        time.Advance(OneMachine.RequestTimeout + OneMachine.StopTimeout);
        var result = await request;
        await InvokeAsync(pool);

        Assert.AreEqual(ExecutionOutcome.Interrupted, result.Outcome);
        Assert.AreEqual(1, pool.Abandoned);
        Assert.HasCount(1, maker[1].Invocations);
        Assert.IsFalse(stuck.Machine.IsDisposed, "an abandoned machine is disposed only once it ends");
        stuck.Release.SetResult();
        await stuck.Machine.Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatWaitsForAMachinePastItsDeadline_IsRefused_AndNeverRuns()
    {
        var time = new FakeTimeProvider();
        var held = new HeldMachine(stopsWhenCancelled: false);
        await using var pool = await StartAsync(new ScriptedProgram(() => held.Machine), OneMachine, time);
        var first = InvokeAsync(pool);
        await held.Entered.Reader.ReadAsync(Token);

        var waiting = InvokeAsync(pool);
        time.Advance(OneMachine.RequestTimeout);

        Assert.AreEqual(ExecutionOutcome.Refused, (await waiting).Outcome);
        Assert.HasCount(1, held.Machine.Invocations);
        held.Release.SetResult();
        await first;
    }

    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task DisposingThePool_DisposesItsIdleMachines_AndOneServingARequestOnceItEnds()
    {
        var held = new HeldMachine();
        var idle = Answering(ProgramInvocationResult.Completed());
        var made = 0;
        var pool = await StartAsync(new ScriptedProgram(() => Interlocked.Increment(ref made) == 1 ? held.Machine : idle),
            new RDWebMachinePoolOptions { MinMachines = 2, MaxMachines = 2 });
        var request = InvokeAsync(pool);
        await held.Entered.Reader.ReadAsync(Token);

        await pool.DisposeAsync();

        Assert.IsTrue(idle.IsDisposed);
        Assert.IsFalse(held.Machine.IsDisposed, "a machine serving a request is disposed once it ends");
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => InvokeAsync(pool));
        held.Release.SetResult();
        await request;
        await held.Machine.Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(-1, 1)]
    [DataRow(2, 1)]
    public async Task APoolOfAnInvalidSize_IsRefused(int min, int max)
        => await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            StartAsync(new ScriptedProgram(() => Answering(ProgramInvocationResult.Completed())),
                new RDWebMachinePoolOptions { MinMachines = min, MaxMachines = max }));

    [TestMethod]
    [DataRow(nameof(RDWebMachinePoolOptions.RecycleAfter))]
    [DataRow(nameof(RDWebMachinePoolOptions.RequestTimeout))]
    [DataRow(nameof(RDWebMachinePoolOptions.StopTimeout))]
    [DataRow(nameof(RDWebMachinePoolOptions.ResetTimeout))]
    public async Task APoolWithALimitThatIsNotPositive_IsRefused(string limit)
    {
        var options = limit switch
        {
            nameof(RDWebMachinePoolOptions.RecycleAfter) => OneMachine with { RecycleAfter = 0 },
            nameof(RDWebMachinePoolOptions.RequestTimeout) => OneMachine with { RequestTimeout = TimeSpan.Zero },
            nameof(RDWebMachinePoolOptions.StopTimeout) => OneMachine with { StopTimeout = TimeSpan.Zero },
            _ => OneMachine with { ResetTimeout = TimeSpan.Zero },
        };

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            StartAsync(new ScriptedProgram(() => Answering(ProgramInvocationResult.Completed())), options));
    }

    private static ProgramInvocationResult ResultOf(ExecutionOutcome outcome) => outcome switch
    {
        ExecutionOutcome.Completed => ProgramInvocationResult.Completed(),
        ExecutionOutcome.RuntimeError => ProgramInvocationResult.RuntimeError(11, "Division by zero", "VBR00011"),
        _ => ProgramInvocationResult.Ended(outcome, "It ended."),
    };
}
