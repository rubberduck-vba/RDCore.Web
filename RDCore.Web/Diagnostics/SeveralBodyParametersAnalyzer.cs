using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST.Declarations;

namespace RDCore.Web.Diagnostics;

/// <summary>
/// <c>RDW00002</c> (<see cref="RDWebDiagnostics.SeveralBodyParameters"/>): a parameter of an action that takes a body is bound by neither its route nor
/// its body.
/// </summary>
/// <remarks>
/// A request has one body, which binds one parameter. Each further parameter the route does not bind binds from the query string, as a <c>GET</c>'s do. It
/// is reported at each such parameter, as a warning: a caller that sends it in the body does not reach it. Only an action that takes a body is read
/// (<c>'@HttpPost</c>); a <c>GET</c> never is.
/// </remarks>
public sealed class SeveralBodyParametersAnalyzer : IModuleAnalyzer
{
    /// <inheritdoc/>
    public IReadOnlyList<RDCoreDiagnosticCode> Codes => [RDWebDiagnostics.SeveralBodyParameters];

    /// <inheritdoc/>
    public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
    {
        foreach (var (_, action, endpoints) in BodyBindings.Of(context))
        {
            var reported = new HashSet<ParameterDeclarationNode>(ReferenceEqualityComparer.Instance);
            foreach (var endpoint in endpoints)
            {
                var unbound = action.Parameters.Where(parameter => !IsNamed(endpoint.RouteParameters, parameter.Name)
                    && !string.Equals(parameter.Name, endpoint.BodyParameter, StringComparison.OrdinalIgnoreCase));
                foreach (var parameter in unbound.Where(reported.Add))
                {
                    yield return new AnalyzerFinding(RDWebDiagnostics.SeveralBodyParameters, parameter.NameRange ?? parameter.SourceLocation.Range,
                        DiagnosticSeverity.Warning, AnalyzerMessages.Format(RDWebResources.SeveralBodyParameters_Message, parameter.Name,
                            BodyBindings.ParameterOf(action, endpoint.BodyParameter!).Name, action.Procedure.Name));
                }
            }
        }
    }

    private static bool IsNamed(IEnumerable<string> names, string name) => names.Contains(name, StringComparer.OrdinalIgnoreCase);
}
