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
        ImmutableArray<string> routeParameters, string? bodyParameter, string? keptMachineKey, bool makesMachine)
    {
        HttpMethod = httpMethod;
        Route = route;
        ModuleName = moduleName;
        ProcedureName = procedureName;
        Parameters = parameters;
        RouteParameters = routeParameters;
        BodyParameter = bodyParameter;
        KeptMachineKey = keptMachineKey;
        MakesMachine = makesMachine;
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
    /// The parameter the request's JSON body binds to: for a <c>POST</c>, the one <c>'@FromBody</c> names, else the first parameter the route does not
    /// bind. <see langword="null"/> for a <c>GET</c>, and for a <c>POST</c> whose parameters the route binds all of.
    /// </summary>
    public string? BodyParameter { get; }

    /// <summary>
    /// The route parameter whose value is the key of the machine a request runs on, as the controller's <c>'@KeptMachine</c> names it;
    /// <see langword="null"/> for an endpoint whose requests run on a machine of the pool.
    /// </summary>
    public string? KeptMachineKey { get; }

    /// <summary>
    /// Whether a request to the endpoint makes the machine of its key before the procedure runs (<c>'@NewMachine</c>).
    /// </summary>
    public bool MakesMachine { get; }

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
    /// <param name="bodyParameter">
    /// The argument of the procedure's <c>'@FromBody</c> annotation: the parameter the body of a <c>POST</c> binds to, matched in any case, as VBA
    /// matches names. <see langword="null"/> when the procedure has none, which binds the body to the first parameter the route does not bind. A
    /// <c>GET</c> has no body, and binds none.
    /// </param>
    /// <param name="keptMachineKey">
    /// The argument of the controller's <c>'@KeptMachine</c> annotation: the route parameter whose value is the key of the machine a request runs on,
    /// matched in any case. It need not be a parameter of the procedure. <see langword="null"/> for a controller without one, whose requests run on a
    /// machine of the pool.
    /// </param>
    /// <param name="makesMachine">Whether the procedure has a <c>'@NewMachine</c> annotation: a request makes the machine of its key.</param>
    /// <returns>The endpoint; <see langword="null"/> when the annotation is not one of an HTTP method (<see cref="RDWebAnnotations.HttpMethodOf"/>).</returns>
    /// <exception cref="RDWebEndpointException">
    /// The route is not a valid route template, or it names a route parameter that is neither a parameter of the procedure nor the machine's key; for a
    /// <c>POST</c>, <paramref name="bodyParameter"/> is not a parameter of the procedure, or is one the route binds; <paramref name="keptMachineKey"/> is
    /// not a parameter of the route; or <paramref name="makesMachine"/> is <see langword="true"/> and <paramref name="keptMachineKey"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static RDWebEndpoint? FromAnnotation(string moduleName, string? controllerRoute, string procedureName, IReadOnlyList<string> parameters,
        string annotationName, string? route, string? bodyParameter = null, string? keptMachineKey = null, bool makesMachine = false)
    {
        if (RDWebAnnotations.HttpMethodOf(annotationName) is not { } method)
        {
            return null;
        }

        if (makesMachine && keptMachineKey is null)
        {
            throw new RDWebEndpointException(
                string.Format(CultureInfo.CurrentUICulture, RDWebResources.Endpoint_NewMachineIsNotKept, moduleName, procedureName));
        }

        var template = Combine(controllerRoute ?? ConventionalRouteOf(moduleName), route);
        RoutePattern pattern;
        try
        {
            pattern = RoutePatternFactory.Parse(template);
        }
        catch (RoutePatternException exception)
        {
            throw new RDWebEndpointException(
                string.Format(CultureInfo.CurrentUICulture, RDWebResources.Endpoint_InvalidRoute, template, moduleName, procedureName, exception.Message),
                exception);
        }

        var bound = new List<string>();
        foreach (var name in pattern.Parameters.Select(parameter => parameter.Name))
        {
            var parameter = parameters.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
            if (parameter is not null)
            {
                bound.Add(parameter);
            }
            else if (!IsMachineKey(name))
            {
                throw new RDWebEndpointException(
                    string.Format(CultureInfo.CurrentUICulture, RDWebResources.Endpoint_RouteParameterIsNoParameter, name, moduleName, procedureName));
            }
        }

        if (keptMachineKey is not null && !pattern.Parameters.Any(parameter => IsMachineKey(parameter.Name)))
        {
            throw new RDWebEndpointException(string.Format(
                CultureInfo.CurrentUICulture, RDWebResources.Endpoint_KeptMachineKeyIsNoRouteParameter, keptMachineKey, moduleName, procedureName));
        }

        var routeParameters = parameters.Where(bound.Contains).ToImmutableArray();
        var body = method != HttpMethods.Post ? null
            : bodyParameter is null ? parameters.FirstOrDefault(parameter => !bound.Contains(parameter))
            : ExplicitBody(bodyParameter);
        return new RDWebEndpoint(method, template, moduleName, procedureName, [.. parameters], routeParameters, body, keptMachineKey, makesMachine);

        // the machine's key names a machine, and binds to a parameter only where the procedure declares one.
        bool IsMachineKey(string name) => keptMachineKey is not null && string.Equals(name, keptMachineKey, StringComparison.OrdinalIgnoreCase);

        string ExplicitBody(string name)
        {
            var parameter = parameters.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new RDWebEndpointException(
                    string.Format(CultureInfo.CurrentUICulture, RDWebResources.Endpoint_BodyIsNoParameter, name, moduleName, procedureName));
            return bound.Contains(parameter)
                ? throw new RDWebEndpointException(
                    string.Format(CultureInfo.CurrentUICulture, RDWebResources.Endpoint_BodyIsRouteParameter, name, moduleName, procedureName))
                : parameter;
        }
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
}
