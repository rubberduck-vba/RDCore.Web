using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Workspace;
using RDCore.Web.Endpoints;
using System.Collections.Immutable;

namespace RDCore.Web.Diagnostics;

// the actions of the document's controller modules that take a body, each with the parse of its module and the endpoints it is served on that bind one
// (RDWebEndpoint.BodyParameter): of the file's own module, and of each of its blocks'. A GET binds no body, and is never one; nor is a POST whose route
// binds every parameter. An endpoint the host cannot serve is left out (RDWebEndpointException): what is said of an action is of what is served.
internal static class BodyBindings
{
    public static IEnumerable<(ModuleParseResult Parse, RDWebAction Action, ImmutableArray<RDWebEndpoint> Endpoints)> Of(ModuleAnalysisContext context)
        => Served(context)
            .Select(served => (served.Parse, served.Action,
                Endpoints: served.Endpoints.Where(endpoint => endpoint.BodyParameter is not null).ToImmutableArray()))
            .Where(served => served.Endpoints.Length > 0);

    // the actions of the document's controller modules that are served, each with the endpoints it is served on, whether they bind a body or not.
    public static IEnumerable<(ModuleParseResult Parse, RDWebAction Action, ImmutableArray<RDWebEndpoint> Endpoints)> Served(ModuleAnalysisContext context)
    {
        var document = new RDCoreDocument(context.Document, context.ParseResult);
        foreach (var uri in document.ModuleUris)
        {
            if (document.ParseOf(uri) is not { } parse || document.NameOf(uri) is not { } name)
            {
                continue;
            }

            foreach (var action in RDWebEndpointReader.Actions(parse, name))
            {
                var endpoints = action.Annotations.Select(annotation => Served(action, annotation)).OfType<RDWebEndpoint>().ToImmutableArray();
                if (endpoints.Length > 0)
                {
                    yield return (parse, action, endpoints);
                }
            }
        }
    }

    // the parameter of the action of that name, as RD-VBA compares names.
    public static ParameterDeclarationNode ParameterOf(RDWebAction action, string name)
        => action.Parameters.First(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));

    private static RDWebEndpoint? Served(RDWebAction action, AnnotationTriviaNode annotation)
    {
        try
        {
            return action.EndpointOf(annotation);
        }
        catch (RDWebEndpointException)
        {
            return null;
        }
    }
}
