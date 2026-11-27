using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.Web.Endpoints;

namespace RDCore.Web.Diagnostics;

/// <summary>
/// <c>RDW00001</c> (<see cref="RDWebDiagnostics.ImplicitBodyBinding"/>): the request body of an action binds to the first parameter its route does not bind,
/// because no <c>'@FromBody</c> names one (<see cref="RDWebAnnotations.FromBody"/>).
/// </summary>
/// <remarks>
/// It is reported at that parameter, as information: the binding works, and <c>'@FromBody</c> says which parameter it is. Only an action that takes a body
/// is read (<c>'@HttpPost</c>); a <c>GET</c> never is. The fix writes <c>'@FromBody("&lt;parameter&gt;")</c> on a line of its own above the procedure,
/// under its HTTP method annotations. It is not offered when the action's routes leave its body to more than one parameter.
/// </remarks>
public sealed class ImplicitBodyBindingAnalyzer : IModuleAnalyzer
{
    /// <inheritdoc/>
    public IReadOnlyList<RDCoreDiagnosticCode> Codes => [RDWebDiagnostics.ImplicitBodyBinding];

    /// <inheritdoc/>
    public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
    {
        foreach (var (parse, action, endpoints) in BodyBindings.Of(context).Where(binding => binding.Action.ExplicitBody is null))
        {
            var bodies = endpoints.Select(endpoint => endpoint.BodyParameter).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase).Select(name => BodyBindings.ParameterOf(action, name)).ToArray();
            foreach (var body in bodies)
            {
                var annotation = $"'@{RDWebAnnotations.FromBody}({ExpressionWriter.LiteralOf(new VBStringValue(body.Name))})";
                yield return new AnalyzerFinding(RDWebDiagnostics.ImplicitBodyBinding, body.NameRange ?? body.SourceLocation.Range,
                    DiagnosticSeverity.Information, AnalyzerMessages.Format(RDWebResources.ImplicitBodyBinding_Message, body.Name, action.Procedure.Name))
                {
                    Fixes = bodies.Length > 1 ? null : new DiagnosticFixes
                    {
                        Edits =
                        [
                            new EditFix
                            {
                                Title = AnalyzerMessages.Format(RDWebResources.ImplicitBodyBinding_Fix, body.Name),
                                Edits = [LineEdits.InsertLinesBefore(parse, action.Procedure, annotation)],
                            },
                        ],
                    },
                };
            }
        }
    }
}
