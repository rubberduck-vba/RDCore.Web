namespace RDCore.Web.Machines;

/// <summary>
/// The size and the limits of a <see cref="RDWebMachinePool"/>.
/// </summary>
public sealed record class RDWebMachinePoolOptions
{
    /// <summary>
    /// How many machines the pool makes when it starts, and keeps once it has made them. 1 by default.
    /// </summary>
    public int MinMachines { get; init; } = 1;

    /// <summary>
    /// How many machines the pool holds at most: a request that finds none idle when the pool holds this many waits for one. The processor count by default.
    /// </summary>
    public int MaxMachines { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// How many requests a machine serves before the pool disposes it and makes another. 1000 by default.
    /// </summary>
    public int RecycleAfter { get; init; } = 1000;

    /// <summary>
    /// How long a request may take, from its arrival to its answer, waiting for a machine included. 90 seconds by default.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// How long a machine whose request was cancelled has to stop, past <see cref="RequestTimeout"/>, before the pool abandons it. 5 seconds by default.
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a machine's reset may take before the pool abandons the machine. 5 seconds by default.
    /// </summary>
    public TimeSpan ResetTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
