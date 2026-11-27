using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using System.Collections.Immutable;

namespace RDCore.Web.Endpoints;

// a public procedure of a controller module, the HTTP method annotations that serve it, and the parameter its '@FromBody names, if any.
internal sealed record class RDWebAction(string ModuleName, string? ControllerRoute, MemberDeclarationNode Procedure,
    ImmutableArray<AnnotationTriviaNode> Annotations, string? ExplicitBody)
{
    public ImmutableArray<ParameterDeclarationNode> Parameters { get; } = [.. Procedure.Children.OfType<ParameterDeclarationNode>()];

    // the endpoint one of the action's annotations declares (RDWebEndpoint.FromAnnotation), which throws when it cannot be served.
    public RDWebEndpoint EndpointOf(AnnotationTriviaNode annotation)
        => RDWebEndpoint.FromAnnotation(ModuleName, ControllerRoute, Procedure.Name, [.. Parameters.Select(parameter => parameter.Name)], annotation.Name,
            annotation.GetStringArgument(), ExplicitBody)!;
}
