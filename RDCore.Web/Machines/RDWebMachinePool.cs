using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RDCore.Web.Machines;

/// <summary>
/// The machines of an RD-VBA program: each request borrows an idle one, runs on it alone, and returns it. A request that finds none idle waits for one,
/// without holding a thread.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The pool makes <see cref="RDWebMachinePoolOptions.MinMachines"/> machines when it starts, and more when a request finds none idle, up to
/// <see cref="RDWebMachinePoolOptions.MaxMachines"/>. A pool of one machine serves its requests one at a time, in the order they arrive.</item>
/// <item>After each request the pool resets the machine, then makes it idle. It recycles the machine (disposes it, and makes another) after
/// <see cref="RDWebMachinePoolOptions.RecycleAfter"/> requests, after an invocation that was interrupted, did not end, or threw, and after a reset that did
/// not complete.</item>
/// <item>A request that waits for a machine past <see cref="RDWebMachinePoolOptions.RequestTimeout"/> answers <see cref="ExecutionOutcome.Refused"/>. One
/// that runs past it is cancelled. A machine that has not stopped <see cref="RDWebMachinePoolOptions.StopTimeout"/> after its request's deadline, or after
/// its reset's, is abandoned: its request answers <see cref="ExecutionOutcome.Interrupted"/>, the pool makes another machine, and it disposes the abandoned
/// one if it ever ends.</item>
/// </list>
/// </remarks>
public sealed class RDWebMachinePool : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<IProgramMachine>> _create;
    private readonly RDWebMachinePoolOptions _options;
    private readonly TimeProvider _time;
    private readonly Channel<Pooled> _idle = Channel.CreateUnbounded<Pooled>();
    private readonly ConcurrentDictionary<Task, bool> _pending = new();
    private readonly Lock _gate = new();
    private int _size;
    private int _abandoned;
    private bool _disposed;

    private RDWebMachinePool(Func<CancellationToken, Task<IProgramMachine>> create, RDWebMachinePoolOptions options, TimeProvider time)
    {
        _create = create;
        _options = options;
        _time = time;
    }

    // a machine of the pool, and how many requests it served: only the request or the return that holds the machine reads or writes it.
    private sealed class Pooled(IProgramMachine machine)
    {
        public IProgramMachine Machine { get; } = machine;

        public int Served { get; set; }
    }

    /// <summary>
    /// How many machines the pool holds: idle, serving a request, or being made. An abandoned machine is not counted.
    /// </summary>
    public int Size
    {
        get
        {
            lock (_gate)
            {
                return _size;
            }
        }
    }

    /// <summary>
    /// How many machines the pool abandoned because they did not stop.
    /// </summary>
    public int Abandoned => Volatile.Read(ref _abandoned);

    /// <summary>
    /// Starts a pool: it makes its <see cref="RDWebMachinePoolOptions.MinMachines"/> machines, all of them idle.
    /// </summary>
    /// <param name="program">The program whose machines the pool makes, each a session of its own. The pool does not dispose it.</param>
    /// <param name="options">The pool's size and limits.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <param name="timeProvider">The clock of the pool's deadlines; <see cref="TimeProvider.System"/> by default.</param>
    /// <returns>The pool, started.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="RDWebMachinePoolOptions.MaxMachines"/> is less than 1, <see cref="RDWebMachinePoolOptions.MinMachines"/> is negative or more than the
    /// maximum, <see cref="RDWebMachinePoolOptions.RecycleAfter"/> is less than 1, or a timeout is not positive.
    /// </exception>
    public static async Task<RDWebMachinePool> StartAsync(IComposedProgram program, RDWebMachinePoolOptions options,
        CancellationToken cancellationToken, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxMachines < 1 || options.MinMachines < 0 || options.MinMachines > options.MaxMachines)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, RDWebResources.Pool_InvalidSize);
        }

        if (options.RecycleAfter < 1 || options.RequestTimeout <= TimeSpan.Zero || options.StopTimeout <= TimeSpan.Zero
            || options.ResetTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, RDWebResources.Pool_InvalidLimit);
        }

        var pool = new RDWebMachinePool(token => program.CreateMachineAsync(token).AsTask(), options, timeProvider ?? TimeProvider.System);
        try
        {
            for (var made = 0; made < options.MinMachines; made++)
            {
                pool._size++;
                pool._idle.Writer.TryWrite(new Pooled(await program.CreateMachineAsync(cancellationToken).ConfigureAwait(false)));
            }
        }
        catch
        {
            await pool.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return pool;
    }

    /// <summary>
    /// Runs the invocation on an idle machine, once there is one, and returns the machine to the pool when it ends.
    /// </summary>
    /// <param name="invocation">The procedure, and its arguments.</param>
    /// <param name="cancellationToken">Cancels the wait for a machine, and the invocation: the request was aborted.</param>
    /// <returns>
    /// What the machine answered; <see cref="ExecutionOutcome.Refused"/> when no machine was idle before the request's deadline, and
    /// <see cref="ExecutionOutcome.Interrupted"/> when the machine did not stop after it.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before a machine was idle.</exception>
    /// <exception cref="ObjectDisposedException">The pool is disposed.</exception>
    public async Task<ProgramInvocationResult> InvokeAsync(ProgramInvocation invocation, CancellationToken cancellationToken)
    {
        // every deadline of the request is set before anything waits, so that each one runs from the request's arrival.
        using var deadline = new CancellationTokenSource(_options.RequestTimeout, _time);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var answered = new CancellationTokenSource();
        var abandonment = Task.Delay(_options.RequestTimeout + _options.StopTimeout, _time, answered.Token);
        try
        {
            Pooled pooled;
            try
            {
                pooled = await BorrowAsync(request.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return ProgramInvocationResult.Ended(ExecutionOutcome.Refused, RDWebResources.Pool_NoMachineInTime);
            }

            var invoking = InvokeOnAsync(pooled.Machine, invocation, request.Token);
            if (await Task.WhenAny(invoking, abandonment).ConfigureAwait(false) != invoking)
            {
                Abandon(pooled, invoking);
                cancellationToken.ThrowIfCancellationRequested();
                return ProgramInvocationResult.Ended(ExecutionOutcome.Interrupted, RDWebResources.Pool_MachineDidNotStop);
            }

            ProgramInvocationResult result;
            try
            {
                result = await invoking.ConfigureAwait(false);
            }
            catch
            {
                Track(RecycleAsync(pooled));
                throw;
            }

            // an outcome that leaves the session as a run leaves it; any other leaves it stopped part-way, and the machine goes.
            var reusable = result.Outcome is ExecutionOutcome.Completed or ExecutionOutcome.RuntimeError or ExecutionOutcome.Halted
                or ExecutionOutcome.NotFound;
            Track(ReturnAsync(pooled, reusable));
            return result;
        }
        finally
        {
            await answered.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the pool: it takes no more requests, and disposes each machine, now when it is idle, or when it ends its request. The program is its owners to
    /// dispose, once the pool is.
    /// </summary>
    /// <returns>A task that completes when the idle machines and the pool's own work have ended.</returns>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _idle.Writer.TryComplete();
        }

        await Task.WhenAll(_pending.Keys).ConfigureAwait(false);
        while (_idle.Reader.TryRead(out var pooled))
        {
            await DisposeQuietlyAsync(pooled.Machine).ConfigureAwait(false);
        }
    }

    private async Task<Pooled> BorrowAsync(CancellationToken cancellationToken)
    {
        bool grows;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_idle.Reader.TryRead(out var idle))
            {
                return idle;
            }

            grows = _size < _options.MaxMachines;
            if (grows)
            {
                _size++;
            }
        }

        if (grows)
        {
            try
            {
                return new Pooled(await _create(cancellationToken).ConfigureAwait(false));
            }
            catch
            {
                lock (_gate)
                {
                    _size--;
                }

                throw;
            }
        }

        try
        {
            return await _idle.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    // a machine that throws as it is called is a machine whose invocation threw.
    private static async Task<ProgramInvocationResult> InvokeOnAsync(IProgramMachine machine, ProgramInvocation invocation, CancellationToken token)
        => await machine.InvokeAsync(invocation, token).ConfigureAwait(false);

    private static async Task<bool> ResetOnAsync(IProgramMachine machine, CancellationToken token)
        => await machine.ResetAsync(token).ConfigureAwait(false);

    private async Task ReturnAsync(Pooled pooled, bool reusable)
    {
        if (!reusable || ++pooled.Served >= _options.RecycleAfter)
        {
            await RecycleAsync(pooled).ConfigureAwait(false);
            return;
        }

        // the reset's deadline cancels it; one that has not ended the stop timeout after that is abandoned with its machine.
        using var deadline = new CancellationTokenSource(_options.ResetTimeout, _time);
        using var ended = new CancellationTokenSource();
        var abandonment = Task.Delay(_options.ResetTimeout + _options.StopTimeout, _time, ended.Token);
        var resetting = ResetOnAsync(pooled.Machine, deadline.Token);
        var first = await Task.WhenAny(resetting, abandonment).ConfigureAwait(false);
        await ended.CancelAsync().ConfigureAwait(false);
        if (first != resetting)
        {
            Abandon(pooled, resetting);
            return;
        }

        bool clean;
        try
        {
            clean = await resetting.ConfigureAwait(false);
        }
        catch (Exception)
        {
            clean = false;
        }

        if (!clean)
        {
            await RecycleAsync(pooled).ConfigureAwait(false);
        }
        else if (!TryIdle(pooled))
        {
            await DisposeQuietlyAsync(pooled.Machine).ConfigureAwait(false);
        }
    }

    private async Task RecycleAsync(Pooled pooled)
    {
        Track(ReplaceAsync());
        await DisposeQuietlyAsync(pooled.Machine).ConfigureAwait(false);
    }

    // the abandoned machine is not waited for: it may never end. It is disposed if it does.
    private void Abandon(Pooled pooled, Task pending)
    {
        Interlocked.Increment(ref _abandoned);
        _ = DisposeOnceEndedAsync(pooled.Machine, pending);
        Track(ReplaceAsync());
    }

    // the machine that takes the place of one that went: the pool's size is unchanged, unless it cannot be made.
    private async Task ReplaceAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                _size--;
                return;
            }
        }

        try
        {
            var machine = await _create(CancellationToken.None).ConfigureAwait(false);
            if (!TryIdle(new Pooled(machine)))
            {
                await DisposeQuietlyAsync(machine).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // the next request that finds no machine idle makes one.
            lock (_gate)
            {
                _size--;
            }
        }
    }

    private bool TryIdle(Pooled pooled)
    {
        lock (_gate)
        {
            return !_disposed && _idle.Writer.TryWrite(pooled);
        }
    }

    private void Track(Task work)
    {
        _pending.TryAdd(work, true);
        _ = work.ContinueWith(done => _pending.TryRemove(done, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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

    // a machine that fails as it is disposed is gone all the same: nothing of it is used again.
    private static async Task DisposeQuietlyAsync(IProgramMachine machine)
    {
        try
        {
            await machine.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }
}
