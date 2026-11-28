using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.Web.Endpoints;

namespace RDCore.Web.Diagnostics;

/// <summary>
/// <c>RDW00003</c> (<see cref="RDWebDiagnostics.MemberNotRoundTripped"/>): a member of the class that an action's body or its returned value is an object of
/// does not come back as it went out through JSON.
/// </summary>
/// <remarks>
/// <para>
/// The body's parameter, or the <c>Function</c> of an action, is declared as a class of the workspace: the host states that class and
/// the classes its members name (<see cref="DeclarationFact.DeclaredClasses"/>). A JSON object of one of them has its <c>Public</c> variables and its
/// properties that a <c>Get</c> reads without arguments, and its keys give values to its variables and to its properties that a <c>Let</c> or a <c>Set</c>
/// gives a value without arguments. A member that takes part in either is reported when the object a client sends back is not the one it was sent:
/// </para>
/// <list type="bullet">
/// <item>a <c>Get</c> with no <c>Let</c> or <c>Set</c>: it is written out, and its key is never read back;</item>
/// <item>a member declared <c>As Object</c> or <c>As Variant</c>: an object it holds is read back as a <c>Dictionary</c>.</item>
/// </list>
/// <para>
/// It is reported once at the body's parameter, and once at the name of a <c>Function</c> that returns an object, naming each such member with its class. The
/// classes are those reached from the declared one through the members that take part. Without the host's facts of the module, nothing is reported.
/// </para>
/// </remarks>
public sealed class MemberNotRoundTrippedAnalyzer : IModuleAnalyzer
{
    /// <inheritdoc/>
    public IReadOnlyList<RDCoreDiagnosticCode> Codes => [RDWebDiagnostics.MemberNotRoundTripped];

    /// <inheritdoc/>
    public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
    {
        if (context.Semantics is not { } semantics)
        {
            yield break;
        }

        foreach (var (_, action, endpoints) in BodyBindings.Served(context))
        {
            var bodies = endpoints.Select(endpoint => endpoint.BodyParameter).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in bodies.Select(body => BodyBindings.ParameterOf(action, body)))
            {
                var fact = semantics.Declarations.FirstOrDefault(declaration => declaration.Kind is DeclarationKind.Parameter
                    && IsNamed(declaration, parameter.Name) && Contains(action.Procedure.SourceLocation.Range, declaration.Location.Range.Start));
                if (FindingOf(fact, parameter.Name, parameter.NameRange ?? parameter.SourceLocation.Range) is { } finding)
                {
                    yield return finding;
                }
            }

            // a procedure of the module is found by its name, which is its own in the module.
            if (action.Procedure.MemberKind is MemberKind.Function)
            {
                var fact = semantics.Declarations.FirstOrDefault(declaration => declaration.Kind is DeclarationKind.Procedure
                    && IsNamed(declaration, action.Procedure.Name));
                if (FindingOf(fact, action.Procedure.Name, action.Procedure.NameRange ?? action.Procedure.SourceLocation.Range) is { } finding)
                {
                    yield return finding;
                }
            }
        }
    }

    private static bool IsNamed(DeclarationFactDto declaration, string name) => string.Equals(declaration.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(SourceRange range, SourcePosition position)
        => (position.Line > range.Start.Line || position.Line == range.Start.Line && position.Character >= range.Start.Character)
            && (position.Line < range.End.Line || position.Line == range.End.Line && position.Character <= range.End.Character);

    // the round-trip rule (RDW00003): a member is written when it is a variable, or a property whose Get takes no argument.
    private static bool IsWritten(ClassMemberFact member)
        => member.Classification is ExpressionClassification.Variable || member.Accessors.HasFlag(PropertyAccessors.Get) && member.Parameters == 0;

    // the round-trip rule (RDW00003): a member is given a value when it is a variable, or a property whose Let or Set takes no argument besides its value.
    private static bool IsGivenAValue(ClassMemberFact member)
        => member.Classification is ExpressionClassification.Variable
            || (member.Accessors & (PropertyAccessors.Let | PropertyAccessors.Set)) != 0 && member.Parameters == 0;

    private static AnalyzerFinding? FindingOf(DeclarationFactDto? declaration, string name, SourceRange range)
    {
        if (declaration?.DeclaredClasses is not { Length: > 0 } classes)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var declared in Reached(classes))
        {
            foreach (var member in declared.Members.Where(member => IsWritten(member) || IsGivenAValue(member)))
            {
                var qualified = $"{declared.Name}.{member.Name}";
                if (IsWritten(member) && !IsGivenAValue(member))
                {
                    parts.Add(AnalyzerMessages.Format(RDWebResources.MemberNotRoundTripped_ReadOnly, qualified));
                }

                if (member is { TypeKind: DeclaredTypeKind.Object or DeclaredTypeKind.Variant, IsArray: false })
                {
                    parts.Add(AnalyzerMessages.Format(RDWebResources.MemberNotRoundTripped_Dictionary, qualified));
                }
            }
        }

        return parts.Count == 0
            ? null
            : new AnalyzerFinding(RDWebDiagnostics.MemberNotRoundTripped, range, DiagnosticSeverity.Warning,
                AnalyzerMessages.Format(RDWebResources.MemberNotRoundTripped_Message, name, string.Join("; ", parts)));
    }

    // the declared class, then each class a member that takes part names, in the order they are reached.
    private static IEnumerable<DeclaredClassFact> Reached(IReadOnlyList<DeclaredClassFact> classes)
    {
        var byAddress = classes.ToDictionary(declared => declared.Class.AbsoluteUri, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { classes[0].Class.AbsoluteUri };
        var pending = new Queue<DeclaredClassFact>([classes[0]]);
        while (pending.TryDequeue(out var declared))
        {
            yield return declared;
            foreach (var member in declared.Members.Where(member => IsWritten(member) || IsGivenAValue(member)))
            {
                if (member.TypeClass is { } address && seen.Add(address.AbsoluteUri) && byAddress.TryGetValue(address.AbsoluteUri, out var next))
                {
                    pending.Enqueue(next);
                }
            }
        }
    }
}
