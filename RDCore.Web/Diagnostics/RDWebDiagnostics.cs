using RDCore.SDK.Extensibility.Diagnostics;

namespace RDCore.Web.Diagnostics;

/// <summary>
/// The diagnostics RDCore.Web reports of the endpoints a module declares, under its own prefix (<see cref="Prefix"/>): the RDWeb extension's
/// analyzers report them, and each code has a page under <see cref="HelpBaseUri"/>.
/// </summary>
public static class RDWebDiagnostics
{
    /// <summary>
    /// The prefix of RDCore.Web's diagnostic codes: <c>RDW</c>.
    /// </summary>
    public const string Prefix = "RDW";

    /// <summary>
    /// The address of the pages of RDCore.Web's diagnostics: the page of a code is <c>&lt;HelpBaseUri&gt;/&lt;code&gt;.html</c>, its code in lowercase.
    /// </summary>
    public const string HelpBaseUri = "https://rubberduck-vba.github.io/RDCore.Web/diagnostics";

    /// <summary>
    /// <c>RDW00001</c>: the request body of an action binds to a parameter that no <c>'@FromBody</c> names (<see cref="ImplicitBodyBindingAnalyzer"/>).
    /// </summary>
    public static RDCoreDiagnosticCode ImplicitBodyBinding { get; } = new(Prefix, 1);

    /// <summary>
    /// <c>RDW00002</c>: a parameter of an action that takes a body is bound by neither the route nor the body (<see cref="SeveralBodyParametersAnalyzer"/>).
    /// </summary>
    public static RDCoreDiagnosticCode SeveralBodyParameters { get; } = new(Prefix, 2);
}
