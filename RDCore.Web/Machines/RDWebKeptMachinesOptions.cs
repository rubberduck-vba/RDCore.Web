namespace RDCore.Web.Machines;

/// <summary>
/// The limits of the <see cref="RDWebKeptMachines"/> of a program.
/// </summary>
public sealed record class RDWebKeptMachinesOptions
{
    /// <summary>
    /// How many machines are kept at most: a request that would make one more is refused. 100 by default.
    /// </summary>
    public int MaxMachines { get; init; } = 100;

    /// <summary>
    /// How long a kept machine is kept with no request, before it is disposed. 20 minutes by default.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// How long a request may take, from its arrival to its answer, its wait for its machine included. 90 seconds by default.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// How long a machine whose request was cancelled has to stop, past <see cref="RequestTimeout"/>, before it is abandoned. 5 seconds by default.
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
