using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using System.Collections.Immutable;
using System.Globalization;

namespace RDCore.Web.Endpoints;

/// <summary>
/// An endpoint of an RD-VBA application: the procedure of a controller module that serves an HTTP method on a route.
/// </summary>
public sealed record class RDWebEndpoint
{
    private RDWebEndpoint(string httpMethod, string route, string moduleName, string procedureName, ImmutableArray<string> parameters,
        ImmutableArray<string> routeParameters, string? bodyParameter)
    {
        HttpMethod = httpMethod;
        Route = route;
        ModuleName = moduleName;
        ProcedureName = procedureName;
        Parameters = parameters;
        RouteParameters = routeParameters;
        BodyParameter = bodyParameter;
    }

    /// <summary>
    /// The HTTP method the endpoint serves: <c>GET</c> or <c>POST</c>.
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// The endpoint's route template, from the root of the application: <c>/orders/{id}</c>.
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// The name of the controller module.
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// The name of the procedure that serves the endpoint.
    /// </summary>
    public string ProcedureName { get; }

    /// <summary>
    /// The names of the procedure's parameters, in the order it declares them.
    /// </summary>
    public ImmutableArray<string> Parameters { get; }

    /// <summary>
    /// The parameters the route binds, each by the name of a route parameter, in the order <see cref="Parameters"/> declares them.
    /// </summary>
    public ImmutableArray<string> RouteParameters { get; }

    /// <summary>
    /// The parameter the request's JSON body binds to: for a <c>POST</c>, the first parameter the route does not bind. <see langword="null"/> for a
    /// <c>GET</c>, and for a <c>POST</c> whose parameters the route binds all of.
    /// </summary>
    public string? BodyParameter { get; }

    /// <summary>
    /// The endpoint a member annotation of a controller module declares.
    /// </summary>
    /// <param name="moduleName">The name of the controller module.</param>
    /// <param name="controllerRoute">
    /// The argument of the module's <c>'@Controller</c> annotation; <see langword="null"/> when it writes none, which routes the controller under its
    /// conventional route (<see cref="ConventionalRouteOf"/>).
    /// </param>
    /// <param name="procedureName">The name of the annotated procedure.</param>
    /// <param name="parameters">The names of the procedure's parameters, in the order it declares them.</param>
    /// <param name="annotationName">The name of the member annotation.</param>
    /// <param name="route">
    /// The argument of the member annotation; <see langword="null"/> when it writes none, which serves the procedure on the controller's route. A route that
    /// starts with <c>/</c> or <c>~/</c> is from the root of the application, and the controller's route does not prefix it.
    /// </param>
    /// <returns>The endpoint; <see langword="null"/> when the annotation is not one of an HTTP method (<see cref="RDWebAnnotations.HttpMethodOf"/>).</returns>
    /// <exception cref="RDWebEndpointException">
    /// The route is not a valid route template, or it names a route parameter that is not a parameter of the procedure.
    /// </exception>
    public static RDWebEndpoint? FromAnnotation(string moduleName, string? controllerRoute, string procedureName, IReadOnlyList<string> parameters,
        string annotationName, string? route)
    {
        if (RDWebAnnotations.HttpMethodOf(annotationName) is not { } method)
        {
            return null;
        }

        var template = Combine(controllerRoute ?? ConventionalRouteOf(moduleName), route);
        RoutePattern pattern;
        try
        {
            pattern = RoutePatternFactory.Parse(template);
        }
        catch (RoutePatternException exception)
        {
            throw new RDWebEndpointException(Format(RDWebResources.Endpoint_InvalidRoute, template, moduleName, procedureName, exception.Message), exception);
        }

        var bound = new List<string>();
        foreach (var name in pattern.Parameters.Select(parameter => parameter.Name))
        {
            var parameter = parameters.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new RDWebEndpointException(Format(RDWebResources.Endpoint_RouteParameterIsNoParameter, name, moduleName, procedureName));
            bound.Add(parameter);
        }

        var routeParameters = parameters.Where(bound.Contains).ToImmutableArray();
        var body = method == HttpMethods.Post ? parameters.FirstOrDefault(parameter => !bound.Contains(parameter)) : null;
        return new RDWebEndpoint(method, template, moduleName, procedureName, [.. parameters], routeParameters, body);
    }

    /// <summary>
    /// The route of a controller whose <c>'@Controller</c> annotation writes none: its module's name, less a <c>Controller</c> suffix, as ASP.NET Core
    /// names a controller. <c>ThingsController</c> is routed under <c>Things</c>, which a request for <c>/things</c> matches, since routes match without
    /// regard to case.
    /// </summary>
    /// <param name="moduleName">The name of the controller module.</param>
    /// <returns>
    /// The name less its last ten letters when it ends with <c>Controller</c>, in any case, and has letters before it; the name as written otherwise.
    /// </returns>
    public static string ConventionalRouteOf(string moduleName)
        => moduleName.Length > Suffix.Length && moduleName.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? moduleName[..^Suffix.Length] : moduleName;

    private const string Suffix = "Controller";

    // a route from the root: the controller's, then the member's, unless the member's starts at the root itself.
    private static string Combine(string controllerRoute, string? route)
    {
        if (route is not null && (route.StartsWith('/') || route.StartsWith("~/", StringComparison.Ordinal)))
        {
            return "/" + route.TrimStart('~').Trim('/');
        }

        var parts = new[] { controllerRoute.Trim('/'), route?.Trim('/') ?? string.Empty }.Where(part => part.Length > 0);
        return "/" + string.Join('/', parts);
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
