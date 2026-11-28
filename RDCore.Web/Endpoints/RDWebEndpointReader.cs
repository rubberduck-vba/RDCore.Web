using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using System.Collections.Immutable;

namespace RDCore.Web.Endpoints;

/// <summary>
/// Reads the endpoints a module declares from its annotations: <c>'@Controller</c> in its declarations section, or above the header of its block
/// (<c>Module Name</c>, <c>Class Name</c>) for the module of a block of a file, with the <c>'@KeptMachine</c> that names its machines' key, and
/// <c>'@HttpGet</c> or <c>'@HttpPost</c> immediately above each public <c>Sub</c> or <c>Function</c> that serves one, with the <c>'@FromBody</c> that
/// names its body parameter and the <c>'@NewMachine</c> that makes its key's machine.
/// </summary>
public static class RDWebEndpointReader
{
    /// <summary>
    /// The endpoints a module declares.
    /// </summary>
    /// <param name="parse">
    /// The module's parse; for a file that has blocks, the parse of the one module (<see cref="SDK.Workspace.RDCoreDocument.ParseOf"/>).
    /// </param>
    /// <param name="moduleName">The module's name.</param>
    /// <returns>
    /// One endpoint for each HTTP method annotation of each public procedure, in the order they are written; none for a module that is not a controller. A
    /// <c>Private</c> or <c>Friend</c> procedure serves no endpoint.
    /// </returns>
    /// <exception cref="RDWebEndpointException">
    /// An annotation declares an endpoint that cannot be served (<see cref="RDWebEndpoint.FromAnnotation"/>).
    /// </exception>
    public static ImmutableArray<RDWebEndpoint> Read(ModuleParseResult parse, string moduleName)
        => [.. Actions(parse, moduleName).SelectMany(action => action.Annotations.Select(action.EndpointOf))];

    // each public procedure of a controller module that an HTTP method annotation serves, in the order they are written; none for a module that is not a
    // controller. What the host serves and what the diagnostics say of an action are both read here.
    internal static IEnumerable<RDWebAction> Actions(ModuleParseResult parse, string moduleName)
    {
        if (parse.SyntaxTree is not { } module
            || parse.GetModuleAnnotations().FirstOrDefault(annotation => annotation.IsNamed(RDWebAnnotations.Controller)) is not { } controller)
        {
            yield break;
        }

        var controllerRoute = controller.GetStringArgument();

        // a '@KeptMachine without an argument names an empty key, which no route parameter has: each endpoint of the module refuses it.
        var keptMachineKey = parse.GetModuleAnnotations().FirstOrDefault(annotation => annotation.IsNamed(RDWebAnnotations.KeptMachine)) is { } kept
            ? kept.GetStringArgument() ?? string.Empty
            : null;
        foreach (var procedure in module.Children.OfType<MemberDeclarationNode>().Where(IsPublicProcedure))
        {
            var annotations = parse.GetAnnotations(procedure);
            var methods = annotations.Where(annotation => RDWebAnnotations.HttpMethodOf(annotation.Name) is not null).ToImmutableArray();
            if (methods.Length > 0)
            {
                var body = annotations.FirstOrDefault(annotation => annotation.IsNamed(RDWebAnnotations.FromBody))?.GetStringArgument();
                var makesMachine = annotations.Any(annotation => annotation.IsNamed(RDWebAnnotations.NewMachine));
                yield return new RDWebAction(moduleName, controllerRoute, procedure, methods, body, keptMachineKey, makesMachine);
            }
        }
    }

    private static bool IsPublicProcedure(MemberDeclarationNode member)
        => member.MemberKind is MemberKind.Procedure or MemberKind.Function
            && member.AccessModifier is AccessModifier.Implicit or AccessModifier.Public or AccessModifier.Global;
}
