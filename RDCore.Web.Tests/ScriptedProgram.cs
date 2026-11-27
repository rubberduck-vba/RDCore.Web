using RDCore.SDK.Runtime.Hosting;
using RDCore.SDK.Workspace;
using System.Collections.Concurrent;

namespace RDCore.Web.Tests;

// a program of no documents whose machines the test makes: the machines it made, in the order it made them.
internal sealed class ScriptedProgram(Func<ScriptedMachine> make) : IComposedProgram
{
    public ConcurrentQueue<ScriptedMachine> Made { get; } = new();

    public ScriptedMachine this[int index] => Made.ElementAt(index);

    public IReadOnlyList<RDCoreDocument> Documents { get; } = [];

    public ValueTask<IProgramMachine> CreateMachineAsync(CancellationToken cancellationToken)
    {
        var machine = make();
        Made.Enqueue(machine);
        return ValueTask.FromResult<IProgramMachine>(machine);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
