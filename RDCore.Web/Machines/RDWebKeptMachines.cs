using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using System.Collections.Concurrent;

namespace RDCore.Web.Machines;

/// <summary>
/// The machines of an RD-VBA program that are kept between requests, each for a key: a request with that key runs on that machine, which is never reset,
/// so its module-level variables, its objects and the rest of its state are what the requests before it left.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A request that makes a machine makes it for its key, then runs on it; one whose key has a machine already is not run. Any other request runs on
/// the machine of its key, once its machine is free: one invocation at a time per machine, in the order they arrive. A request waits without holding a
/// thread.</item>
/// <item>A request that ends with <see cref="ExecutionOutcome.Completed"/>, <see cref="ExecutionOutcome.RuntimeError"/> or
/// <see cref="ExecutionOutcome.NotFound"/> leaves its machine kept. Any other outcome, an invocation that throws, and a machine that is abandoned end it:
/// the machine is disposed, and its key has no machine afterwards.</item>
/// <item>A machine that has had no request for <see cref="RDWebKeptMachinesOptions.IdleTimeout"/> is disposed.</item>
/// <item>A request that waits for its machine past <see cref="RDWebKeptMachinesOptions.RequestTimeout"/> answers <see cref="ExecutionOutcome.Refused"/>.
/// One that runs past it is cancelled, and a machine that has not stopped <see cref="RDWebKeptMachinesOptions.StopTimeout"/> later is abandoned: its
/// request answers <see cref="ExecutionOutcome.Interrupted"/>, and the machine is disposed if it ever ends.</item>
/// </list>
/// </remarks>
public sealed class RDWebKeptMachines : IAsyncDisposable
{
    private readonly IComposedProgram _program;
    private readonly RDWebKeptMachinesOptions _options;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Kept> _kept = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Task, bool> _pending = new();
    private readonly Lock _gate = new();
    private bool _disposed;

    /// <summary>
    /// Makes the registry of the kept machines of a program, with no machine kept.
    /// </summary>
    /// <param name="program">The program whose machines are kept, each a session of its own. The registry does not dispose it.</param>
    /// <param name="options">The registry's limits.</param>
    /// <param name="timeProvider">The clock of the deadlines; <see cref="TimeProvider.System"/> by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="RDWebKeptMachinesOptions.MaxMachines"/> is less than 1, or a timeout is not positive.
    /// </exception>
    public RDWebKeptMachines(IComposedProgram program, RDWebKeptMachinesOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxMachines < 1 || options.IdleTimeout <= TimeSpan.Zero || options.RequestTimeout <= TimeSpan.Zero
            || options.StopTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, RDWebResources.KeptMachines_InvalidLimit);
        }

        _program = program;
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    // a kept machine: its key, the machine once it is made, and the turn that one request at a time holds. Gone is set, under the registry's gate, when the
    // machine leaves the registry; a request that then gets the turn finds no machine.
    private sealed class Kept(string key, bool taken)
    {
        public string Key { get; } = key;

        public IProgramMachine? Machine { get; set; }

        public SemaphoreSlim Turn { get; } = new(taken ? 0 : 1, 1);

        public ITimer? Idle { get; set; }

        public bool Gone { get; set; }
    }

    /// <summary>
    /// How many machines are kept: made, or being made.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _kept.Count;
            }
        }
    }

    /// <summary>
    /// Runs the invocation on the machine kept for the key, once it is free; a request that makes a machine makes it first.
    /// </summary>
    /// <param name="key">The key of the machine, as the request names it.</param>
    /// <param name="invocation">The procedure, and its arguments.</param>
    /// <param name="makesMachine">Whether the request makes the machine of its key.</param>
    /// <param name="cancellationToken">Cancels the wait for the machine, and the invocation: the request was aborted.</param>
    /// <returns>
    /// <see cref="RDWebKeptMachineStatus.Served"/> with what the machine answered: <see cref="ExecutionOutcome.Refused"/> when the machine was not free before
    /// the request's deadline, and <see cref="ExecutionOutcome.Interrupted"/> when it did not stop after it. Otherwise why the request reached no machine.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before the machine was free.</exception>
    /// <exception cref="ObjectDisposedException">The registry is disposed.</exception>
    public async Task<RDWebKeptMachineAnswer> InvokeAsync(string key, ProgramInvocation invocation, bool makesMachine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        // every deadline of the request is set before anything waits, so that each one runs from the request's arrival.
        using var deadline = new CancellationTokenSource(_options.RequestTimeout, _time);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var answered = new CancellationTokenSource();
        var abandonment = Task.Delay(_options.RequestTimeout + _options.StopTimeout, _time, answered.Token);

        Kept? kept;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (makesMachine)
            {
                if (_kept.ContainsKey(key))
                {
                    return new RDWebKeptMachineAnswer(RDWebKeptMachineStatus.AlreadyMade);
                }

                if (_kept.Count >= _options.MaxMachines)
                {
                    return new RDWebKeptMachineAnswer(RDWebKeptMachineStatus.Full);
                }

                // the request that makes the machine holds its turn from the start: a request for the same key waits until it is made.
                kept = new Kept(key, taken: true);
                _kept.Add(key, kept);
            }
            else if (!_kept.TryGetValue(key, out kept))
            {
                return new RDWebKeptMachineAnswer(RDWebKeptMachineStatus.NoMachine);
            }
        }

        var holds = makesMachine;
        try
        {
            if (makesMachine)
            {
                try
                {
                    kept.Machine = await _program.CreateMachineAsync(request.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    Remove(kept);
                    return Served(ProgramInvocationResult.Ended(ExecutionOutcome.Refused, RDWebResources.Pool_NoMachineInTime));
                }
                catch
                {
                    Remove(kept);
                    throw;
                }

                kept.Idle = _time.CreateTimer(state => OnIdle((Kept)state!), kept, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            else
            {
                try
                {
                    await kept.Turn.WaitAsync(request.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return Served(ProgramInvocationResult.Ended(ExecutionOutcome.Refused, RDWebResources.Pool_NoMachineInTime));
                }

                holds = true;
                if (IsGone(kept))
                {
                    return new RDWebKeptMachineAnswer(RDWebKeptMachineStatus.NoMachine);
                }

                kept.Idle?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            var machine = kept.Machine!;
            var invoking = InvokeOnAsync(machine, invocation, request.Token);
            if (await Task.WhenAny(invoking, abandonment).ConfigureAwait(false) != invoking)
            {
                Remove(kept);
                _ = DisposeOnceEndedAsync(machine, invoking);
                cancellationToken.ThrowIfCancellationRequested();
                return Served(ProgramInvocationResult.Ended(ExecutionOutcome.Interrupted, RDWebResources.Pool_MachineDidNotStop));
            }

            ProgramInvocationResult result;
            try
            {
                result = await invoking.ConfigureAwait(false);
            }
            catch
            {
                Remove(kept);
                Track(DisposeQuietlyAsync(machine));
                throw;
            }

            // an outcome that leaves the session as a run leaves it keeps the machine; any other leaves it stopped part-way, or ended, and the machine goes.
            if (result.Outcome is not (ExecutionOutcome.Completed or ExecutionOutcome.RuntimeError or ExecutionOutcome.NotFound))
            {
                Remove(kept);
                Track(DisposeQuietlyAsync(machine));
            }

            return Served(result);
        }
        finally
        {
            await answered.CancelAsync().ConfigureAwait(false);
            if (holds)
            {
                if (!IsGone(kept))
                {
                    kept.Idle?.Change(_options.IdleTimeout, Timeout.InfiniteTimeSpan);
                }

                kept.Turn.Release();
            }
        }
    }

    /// <summary>
    /// Disposes the registry: it takes no more requests, and disposes each machine, now when it is free, or when its request ends. The registry does not
    /// dispose the program: its owner does, once the registry is disposed.
    /// </summary>
    /// <returns>A task that completes when the free machines and the registry's own work have ended.</returns>
    public async ValueTask DisposeAsync()
    {
        Kept[] all;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            all = [.. _kept.Values];
            _kept.Clear();
            foreach (var kept in all)
            {
                kept.Gone = true;
            }
        }

        foreach (var kept in all)
        {
            kept.Idle?.Dispose();
            if (await kept.Turn.WaitAsync(0).ConfigureAwait(false))
            {
                await DisposeQuietlyAsync(kept.Machine).ConfigureAwait(false);
                kept.Turn.Release();
            }
            else
            {
                // a request holds the machine: it is disposed once that request lets go of it, and the registry does not wait for that.
                _ = DisposeWhenFreeAsync(kept);
            }
        }

        await Task.WhenAll(_pending.Keys).ConfigureAwait(false);
    }

    private static RDWebKeptMachineAnswer Served(ProgramInvocationResult result) => new(RDWebKeptMachineStatus.Served, result);

    // a machine that throws as it is called is a machine whose invocation threw.
    private static async Task<ProgramInvocationResult> InvokeOnAsync(IProgramMachine machine, ProgramInvocation invocation, CancellationToken token)
        => await machine.InvokeAsync(invocation, token).ConfigureAwait(false);

    private bool IsGone(Kept kept)
    {
        lock (_gate)
        {
            return kept.Gone;
        }
    }

    private void Remove(Kept kept)
    {
        lock (_gate)
        {
            kept.Gone = true;
            if (_kept.TryGetValue(kept.Key, out var current) && ReferenceEquals(current, kept))
            {
                _kept.Remove(kept.Key);
            }
        }

        kept.Idle?.Dispose();
    }

    // the idle deadline of a machine that no request holds: a request that holds it restarts the deadline when it ends.
    private void OnIdle(Kept kept)
    {
        if (!kept.Turn.Wait(0))
        {
            return;
        }

        var expires = !IsGone(kept);
        if (expires)
        {
            Remove(kept);
        }

        kept.Turn.Release();
        if (expires)
        {
            Track(DisposeQuietlyAsync(kept.Machine));
        }
    }

    private void Track(Task work)
    {
        _pending.TryAdd(work, true);
        _ = work.ContinueWith(done => _pending.TryRemove(done, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task DisposeWhenFreeAsync(Kept kept)
    {
        await kept.Turn.WaitAsync().ConfigureAwait(false);

        // a machine made after the registry was disposed has an idle timer made after it too.
        kept.Idle?.Dispose();
        await DisposeQuietlyAsync(kept.Machine).ConfigureAwait(false);
        kept.Turn.Release();
    }

    private static async Task DisposeOnceEndedAsync(IProgramMachine machine, Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // how the abandoned machine's work ended does not matter: the machine goes either way.
        }

        await DisposeQuietlyAsync(machine).ConfigureAwait(false);
    }

    // a machine that fails as it is disposed is gone all the same: nothing of it is used again. A machine still being made has nothing to dispose.
    private static async Task DisposeQuietlyAsync(IProgramMachine? machine)
    {
        if (machine is null)
        {
            return;
        }

        try
        {
            await machine.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }
}
