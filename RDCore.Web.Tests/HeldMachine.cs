using RDCore.SDK.Platform.Protocol;
using System.Threading.Channels;

namespace RDCore.Web.Tests;

// a machine that says when an invocation enters it, and holds it until the test releases it. One that stops when cancelled answers Interrupted; one
// that does not stop keeps holding.
internal sealed class HeldMachine
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
                return ProgramInvocationResult.Completed();
            }
            catch (OperationCanceledException)
            {
                return ProgramInvocationResult.Ended(ExecutionOutcome.Interrupted, "Interrupted.");
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
