using Microsoft.Extensions.Time.Testing;
using RDCore.SDK.Platform.Protocol;
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
    private static readonly RDWebInvocation Invocation = new("OrdersController", "GetOrder", ImmutableDictionary<string, JsonNode?>.Empty);

    private static readonly RDWebMachinePoolOptions OneMachine = new() { MinMachines = 1, MaxMachines = 1 };

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    // the machines a pool makes, in the order it makes them.
    private sealed class Maker(Func<ScriptedMachine> make)
    {
        public ConcurrentQueue<ScriptedMachine> Made { get; } = new();

        public ScriptedMachine this[int index] => Made.ElementAt(index);

        public Task<IRDWebPooledMachine> CreateAsync(CancellationToken cancellationToken)
        {
            var machine = make();
            Made.Enqueue(machine);
            return Task.FromResult<IRDWebPooledMachine>(machine);
        }
    }

    // a machine that says when an invocation enters it, and holds it until the test releases it. One that stops when cancelled answers Interrupted; one
    // that does not stop keeps holding.
    private sealed class HeldMachine
    {
        private int _running;

        public HeldMachine(bool stopsWhenCancelled = true, Channel<bool>? entered = null)
        {
            Entered = entered ?? Channel.CreateUnbounded<bool>();
            Machine = new ScriptedMachine(async (_, token) =>
            {
                MostAtOnce = Math.Max(MostAtOnce, Interlocked.Increment(ref _running));
                Entered.Writer.TryWrite(true);
                try
                {
                    await (stopsWhenCancelled ? Release.Task.WaitAsync(token) : Release.Task);
                    return RDWebInvocationResult.Completed();
                }
                catch (OperationCanceledException)
                {
                    return RDWebInvocationResult.Ended(ExecutionOutcome.Interrupted, "Interrupted.");
                }
                finally
                {
                    Interlocked.Decrement(ref _running);
                }
            });
        }

        public ScriptedMachine Machine { get; }

        // says when an invocation enters the machine: a channel of its own, or one that several machines share.
        public Channel<bool> Entered { get; }

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MostAtOnce { get; private set; }
    }

    private static ScriptedMachine Answering(RDWebInvocationResult result) => new(_ => result);

    private Task<RDWebMachinePool> StartAsync(Maker maker, RDWebMachinePoolOptions options, TimeProvider? time = null)
        => RDWebMachinePool.StartAsync(maker.CreateAsync, options, Token, time);

    [TestMethod]
    public async Task APoolOfOneMachine_RunsASecondRequestOnlyOnceTheFirstHasReturned()
    {
        var held = new HeldMachine();
        await using var pool = await StartAsync(new Maker(() => held.Machine), OneMachine);

        var first = pool.InvokeAsync(Invocation, Token);
        await held.Entered.Reader.ReadAsync(Token);
        var second = pool.InvokeAsync(Invocation, Token);

        Assert.HasCount(1, held.Machine.Invocations, "the second request waits for the machine");
        held.Release.SetResult();
        await Task.WhenAll(first, second);

        Assert.HasCount(2, held.Machine.Invocations);
        Assert.AreEqual(1, held.MostAtOnce);
    }

    [TestMethod]
    public async Task APoolOfTwoMachines_RunsTwoRequestsAtOnce_EachOnAMachineOfItsOwn()
    {
        var held = new ConcurrentQueue<HeldMachine>();
        var entered = Channel.CreateUnbounded<bool>();
        await using var pool = await StartAsync(new Maker(() =>
        {
            var machine = new HeldMachine(entered: entered);
            held.Enqueue(machine);
            return machine.Machine;
        }), new RDWebMachinePoolOptions { MinMachines = 2, MaxMachines = 2 });

        var first = pool.InvokeAsync(Invocation, Token);
        var second = pool.InvokeAsync(Invocation, Token);
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
    public async Task ThePool_MakesAMachineForARequestThatFindsNoneIdle_UpToItsMaximum()
    {
        var held = new ConcurrentQueue<HeldMachine>();
        var entered = Channel.CreateUnbounded<bool>();
        var maker = new Maker(() =>
        {
            var machine = new HeldMachine(entered: entered);
            held.Enqueue(machine);
            return machine.Machine;
        });
        await using var pool = await StartAsync(maker, new RDWebMachinePoolOptions { MinMachines = 1, MaxMachines = 2 });

        var first = pool.InvokeAsync(Invocation, Token);
        var second = pool.InvokeAsync(Invocation, Token);
        var third = pool.InvokeAsync(Invocation, Token);
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
    public async Task ARequestCancelledWhileItWaits_DoesNotRun_AndTheMachineServesTheNextOne()
    {
        var held = new HeldMachine();
        await using var pool = await StartAsync(new Maker(() => held.Machine), OneMachine);
        var first = pool.InvokeAsync(Invocation, Token);
        await held.Entered.Reader.ReadAsync(Token);

        using var cancelled = new CancellationTokenSource();
        var waiting = pool.InvokeAsync(Invocation, cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);

        held.Release.SetResult();
        await first;
        await pool.InvokeAsync(Invocation, Token);
        Assert.HasCount(2, held.Machine.Invocations, "the cancelled request never reached the machine");
    }

    [TestMethod]
    public async Task AfterEachRequest_TheMachineIsReset_BeforeItServesTheNextOne()
    {
        var log = new ConcurrentQueue<string>();
        var machine = new ScriptedMachine(_ =>
        {
            log.Enqueue("invoke");
            return RDWebInvocationResult.Completed();
        })
        {
            Reset = _ =>
            {
                log.Enqueue("reset");
                return Task.FromResult(true);
            },
        };
        await using var pool = await StartAsync(new Maker(() => machine), OneMachine);

        await pool.InvokeAsync(Invocation, Token);
        await pool.InvokeAsync(Invocation, Token);

        CollectionAssert.AreEqual(new[] { "invoke", "reset", "invoke" }, log.Take(3).ToArray());
    }

    [TestMethod]
    [DataRow(ExecutionOutcome.Completed)]
    [DataRow(ExecutionOutcome.RuntimeError)]
    [DataRow(ExecutionOutcome.Halted)]
    [DataRow(ExecutionOutcome.NotFound)]
    public async Task AMachineWhoseRequestEndedNormally_IsResetAndServesTheNextOne(ExecutionOutcome outcome)
    {
        var maker = new Maker(() => Answering(ResultOf(outcome)));
        await using var pool = await StartAsync(maker, OneMachine);

        await pool.InvokeAsync(Invocation, Token);
        await pool.InvokeAsync(Invocation, Token);

        Assert.HasCount(1, maker.Made);
        Assert.HasCount(2, maker[0].Invocations);
        Assert.IsFalse(maker[0].IsDisposed);
    }

    [TestMethod]
    [DataRow(ExecutionOutcome.Interrupted)]
    [DataRow(ExecutionOutcome.Suspended)]
    [DataRow(ExecutionOutcome.Refused)]
    public async Task AMachineWhoseRequestDidNotEnd_IsRecycled_AndAnotherServesTheNextOne(ExecutionOutcome outcome)
    {
        var maker = new Maker(() => Answering(ResultOf(outcome)));
        await using var pool = await StartAsync(maker, OneMachine);

        await pool.InvokeAsync(Invocation, Token);
        await pool.InvokeAsync(Invocation, Token);

        // the second request's machine is recycled too, so a third may be made already.
        Assert.IsTrue(maker.Made.Count >= 2, "the second request ran on another machine");
        Assert.HasCount(1, maker[1].Invocations);
        Assert.AreEqual(0, maker[0].Resets, "a recycled machine is not reset");
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    public async Task AMachineWhoseInvocationThrows_IsRecycled_AndAnotherServesTheNextOne()
    {
        var calls = 0;
        var maker = new Maker(() => new ScriptedMachine(_ =>
            Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("the session failed") : RDWebInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pool.InvokeAsync(Invocation, Token));
        var result = await pool.InvokeAsync(Invocation, Token);

        Assert.AreEqual(ExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(2, maker.Made, "the second request ran on another machine");
        Assert.HasCount(1, maker[1].Invocations);
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    public async Task AMachine_IsRecycledOnceItHasServedRecycleAfterRequests()
    {
        var maker = new Maker(() => Answering(RDWebInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine with { RecycleAfter = 2 });

        for (var request = 0; request < 3; request++)
        {
            await pool.InvokeAsync(Invocation, Token);
        }

        Assert.HasCount(2, maker.Made, "the third request ran on another machine");
        Assert.HasCount(2, maker[0].Invocations);
        Assert.HasCount(1, maker[1].Invocations);
        Assert.AreEqual(1, maker[0].Resets, "reset between its two requests, not after its last");
        await maker[0].Disposed.WaitAsync(Token);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AMachineWhoseResetDoesNotComplete_IsRecycled(bool resetThrows)
    {
        var maker = new Maker(() => new ScriptedMachine(_ => RDWebInvocationResult.Completed())
        {
            Reset = _ => resetThrows ? Task.FromException<bool>(new InvalidOperationException("Terminate failed")) : Task.FromResult(false),
        });
        await using var pool = await StartAsync(maker, OneMachine);

        await pool.InvokeAsync(Invocation, Token);
        await pool.InvokeAsync(Invocation, Token);

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
        var maker = new Maker(() => new ScriptedMachine(_ => RDWebInvocationResult.Completed())
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
            await pool.InvokeAsync(Invocation, Token);
            var resetToken = await resetting.Reader.ReadAsync(Token);
            time.Advance(options.ResetTimeout);
            Assert.IsTrue(resetToken.IsCancellationRequested, "the reset's deadline cancels it");
            time.Advance(options.StopTimeout);
            await pool.InvokeAsync(Invocation, Token);

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
    public async Task ARequestPastItsDeadline_IsCancelled_AndItsMachineRecycled()
    {
        var time = new FakeTimeProvider();
        var held = new ConcurrentQueue<HeldMachine>();
        var maker = new Maker(() =>
        {
            var machine = new HeldMachine();
            held.Enqueue(machine);
            return machine.Machine;
        });
        await using var pool = await StartAsync(maker, OneMachine, time);

        var request = pool.InvokeAsync(Invocation, Token);
        await held.First().Entered.Reader.ReadAsync(Token);
        time.Advance(OneMachine.RequestTimeout);

        Assert.AreEqual(ExecutionOutcome.Interrupted, (await request).Outcome);
        await maker[0].Disposed.WaitAsync(Token);
        Assert.AreEqual(0, pool.Abandoned, "the machine stopped when it was cancelled");
    }

    [TestMethod]
    public async Task ARequestWhoseMachineDoesNotStopAfterItsDeadline_AnswersInterrupted_AndItsMachineIsAbandonedAndReplaced()
    {
        var time = new FakeTimeProvider();
        var stuck = new HeldMachine(stopsWhenCancelled: false);
        var made = 0;
        var maker = new Maker(() => Interlocked.Increment(ref made) == 1 ? stuck.Machine : Answering(RDWebInvocationResult.Completed()));
        await using var pool = await StartAsync(maker, OneMachine, time);

        var request = pool.InvokeAsync(Invocation, Token);
        await stuck.Entered.Reader.ReadAsync(Token);
        time.Advance(OneMachine.RequestTimeout + OneMachine.StopTimeout);
        var result = await request;
        await pool.InvokeAsync(Invocation, Token);

        Assert.AreEqual(ExecutionOutcome.Interrupted, result.Outcome);
        Assert.AreEqual(1, pool.Abandoned);
        Assert.HasCount(1, maker[1].Invocations);
        Assert.IsFalse(stuck.Machine.IsDisposed, "an abandoned machine is disposed only once it ends");
        stuck.Release.SetResult();
        await stuck.Machine.Disposed.WaitAsync(Token);
    }

    [TestMethod]
    public async Task ARequestThatWaitsForAMachinePastItsDeadline_IsRefused_AndNeverRuns()
    {
        var time = new FakeTimeProvider();
        var held = new HeldMachine(stopsWhenCancelled: false);
        await using var pool = await StartAsync(new Maker(() => held.Machine), OneMachine, time);
        var first = pool.InvokeAsync(Invocation, Token);
        await held.Entered.Reader.ReadAsync(Token);

        var waiting = pool.InvokeAsync(Invocation, Token);
        time.Advance(OneMachine.RequestTimeout);

        Assert.AreEqual(ExecutionOutcome.Refused, (await waiting).Outcome);
        Assert.HasCount(1, held.Machine.Invocations);
        held.Release.SetResult();
        await first;
    }

    [TestMethod]
    public async Task DisposingThePool_DisposesItsIdleMachines_AndOneServingARequestOnceItEnds()
    {
        var held = new HeldMachine();
        var idle = Answering(RDWebInvocationResult.Completed());
        var made = 0;
        var pool = await StartAsync(new Maker(() => Interlocked.Increment(ref made) == 1 ? held.Machine : idle),
            new RDWebMachinePoolOptions { MinMachines = 2, MaxMachines = 2 });
        var request = pool.InvokeAsync(Invocation, Token);
        await held.Entered.Reader.ReadAsync(Token);

        await pool.DisposeAsync();

        Assert.IsTrue(idle.IsDisposed);
        Assert.IsFalse(held.Machine.IsDisposed, "a machine serving a request is disposed once it ends");
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => pool.InvokeAsync(Invocation, Token));
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
            StartAsync(new Maker(() => Answering(RDWebInvocationResult.Completed())), new RDWebMachinePoolOptions { MinMachines = min, MaxMachines = max }));

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

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => StartAsync(new Maker(() => Answering(RDWebInvocationResult.Completed())), options));
    }

    private static RDWebInvocationResult ResultOf(ExecutionOutcome outcome) => outcome switch
    {
        ExecutionOutcome.Completed => RDWebInvocationResult.Completed(),
        ExecutionOutcome.RuntimeError => RDWebInvocationResult.RuntimeError(11, "Division by zero", "VBR00011"),
        _ => RDWebInvocationResult.Ended(outcome, "It ended."),
    };
}
