using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using System.Collections.Immutable;

namespace RDCore.Web.Endpoints;

// a public procedure of a controller module, the HTTP method annotations that serve it, the parameter its '@FromBody names, if any, the route parameter
// its controller's '@KeptMachine names, if any, and whether it has '@NewMachine.
internal sealed record class RDWebAction(string ModuleName, string? ControllerRoute, MemberDeclarationNode Procedure,
    ImmutableArray<AnnotationTriviaNode> Annotations, string? ExplicitBody, string? KeptMachineKey = null, bool MakesMachine = false)
{
    public ImmutableArray<ParameterDeclarationNode> Parameters { get; } = [.. Procedure.Children.OfType<ParameterDeclarationNode>()];

    // the endpoint one of the action's annotations declares (RDWebEndpoint.FromAnnotation), which throws when it cannot be served.
    public RDWebEndpoint EndpointOf(AnnotationTriviaNode annotation)
        => RDWebEndpoint.FromAnnotation(ModuleName, ControllerRoute, Procedure.Name, [.. Parameters.Select(parameter => parameter.Name)], annotation.Name,
            annotation.GetStringArgument(), ExplicitBody, KeptMachineKey, MakesMachine)!;
}
