using RDCore.SDK.Platform.Protocol;

namespace RDCore.Web.Machines;

/// <summary>
/// What a request to a kept machine came to (<see cref="RDWebKeptMachines.InvokeAsync"/>).
/// </summary>
public enum RDWebKeptMachineStatus
{
    /// <summary>
    /// The request reached its machine: <see cref="RDWebKeptMachineAnswer.Result"/> is what the machine answered.
    /// </summary>
    Served,

    /// <summary>
    /// No machine is kept for the request's key, and the request does not make one.
    /// </summary>
    NoMachine,

    /// <summary>
    /// The request makes a machine, and one is already kept for its key.
    /// </summary>
    AlreadyMade,

    /// <summary>
    /// The request makes a machine, and <see cref="RDWebKeptMachinesOptions.MaxMachines"/> are kept.
    /// </summary>
    Full,
}

/// <summary>
/// What a request to a kept machine came to, and what the machine answered when the request reached it.
/// </summary>
/// <param name="Status">Whether the request reached its machine, and why not when it did not.</param>
/// <param name="Result">What the machine answered, when <paramref name="Status"/> is <see cref="RDWebKeptMachineStatus.Served"/>; otherwise
/// <see langword="null"/>.</param>
public sealed record class RDWebKeptMachineAnswer(RDWebKeptMachineStatus Status, ProgramInvocationResult? Result = null);
