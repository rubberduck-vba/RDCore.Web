namespace RDCore.Web.Machines;

/// <summary>
/// A machine: one runtime session of an RD-VBA program, which serves one request at a time.
/// </summary>
/// <remarks>
/// A machine runs one invocation at a time, from a clean program state: its module-level variables, objects, open files and error state are reset between
/// two invocations (<see cref="IRDWebPooledMachine.ResetAsync"/>), so that nothing of one request reaches the next. Concurrency comes from several machines
/// (<see cref="RDWebMachinePool"/>), never from two requests in one machine.
/// </remarks>
public interface IRDWebMachine
{
    /// <summary>
    /// Runs one procedure of the program with the request's arguments.
    /// </summary>
    /// <param name="invocation">The procedure, and its arguments as JSON values.</param>
    /// <param name="cancellationToken">Cancels the invocation: the request was aborted.</param>
    /// <returns>The value the procedure returned, as JSON, or how it failed. A run-time error of the program is a result, never an exception.</returns>
    Task<RDWebInvocationResult> InvokeAsync(RDWebInvocation invocation, CancellationToken cancellationToken);
}
