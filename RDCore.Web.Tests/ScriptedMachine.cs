using RDCore.Web.Machines;
using System.Collections.Concurrent;

namespace RDCore.Web.Tests;

// a machine whose answers the test writes: what a session of an RD-VBA program would answer, without the program. It keeps each invocation it was given,
// counts its resets (clean unless the test's Reset says otherwise), and says when it is disposed.
internal sealed class ScriptedMachine(Func<RDWebInvocation, CancellationToken, Task<RDWebInvocationResult>> answer) : IRDWebPooledMachine
{
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _resets;

    public ScriptedMachine(Func<RDWebInvocation, RDWebInvocationResult> answer)
        : this((invocation, _) => Task.FromResult(answer(invocation)))
    {
    }

    public ConcurrentQueue<RDWebInvocation> Invocations { get; } = new();

    public Func<CancellationToken, Task<bool>> Reset { get; init; } = _ => Task.FromResult(true);

    public int Resets => Volatile.Read(ref _resets);

    public bool IsDisposed => _disposed.Task.IsCompleted;

    public Task Disposed => _disposed.Task;

    public Task<RDWebInvocationResult> InvokeAsync(RDWebInvocation invocation, CancellationToken cancellationToken)
    {
        Invocations.Enqueue(invocation);
        return answer(invocation, cancellationToken);
    }

    public Task<bool> ResetAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _resets);
        return Reset(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
