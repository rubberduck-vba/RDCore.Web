namespace RDCore.Web.Machines;

/// <summary>
/// A machine that a <see cref="RDWebMachinePool"/> owns: it serves one invocation at a time, is reset to a clean program state between two invocations, and
/// is disposed when the pool recycles it.
/// </summary>
public interface IRDWebPooledMachine : IRDWebMachine, IAsyncDisposable
{
    /// <summary>
    /// Resets the machine to a clean program state: its module-level variables, objects (with their <c>Class_Terminate</c>), open files and error state.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reset: it ran past the pool's reset deadline.</param>
    /// <returns>
    /// <see langword="true"/> when the machine is clean and can serve another invocation; <see langword="false"/> when the reset did not complete, and the
    /// pool recycles the machine.
    /// </returns>
    Task<bool> ResetAsync(CancellationToken cancellationToken);
}
