using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RDCore.Web.Endpoints;
using RDCore.Web.Machines;

namespace RDCore.Web.Hosting;

/// <summary>
/// Maps the endpoints of an RD-VBA application into ASP.NET Core routing.
/// </summary>
public static class RDWebEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps each endpoint to its route and HTTP method, served by the procedure that declares it.
    /// </summary>
    /// <param name="routes">The application's routes.</param>
    /// <param name="endpoints">The endpoints the application's controller modules declare.</param>
    /// <param name="pool">The machines that run the procedures.</param>
    /// <returns>A builder of the conventions of every endpoint mapped, such as an authorization policy.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A route parameter binds to the procedure's parameter of that name, as a string; a query string value binds to a parameter the route does not
    /// bind, by its name. The request's JSON body binds to <see cref="RDWebEndpoint.BodyParameter"/>.</item>
    /// <item>A value the procedure returns is the response's JSON body (<c>200</c>); a procedure that returns none answers <c>204</c>.</item>
    /// <item>A run-time error the procedure does not handle answers <c>500</c> with a problem details body (RFC 9457) whose <c>errorNumber</c> is the
    /// error's number and whose <c>errorCode</c> is its diagnostic code. A request the pool refuses, because no machine was idle in time or the machine
    /// refused it, answers <c>503</c> with a <c>Retry-After</c> of one second; any other outcome answers <c>500</c>.</item>
    /// <item>A body that is not valid JSON answers <c>400</c>, and one that is not of the JSON media type answers <c>415</c>, before the procedure
    /// runs.</item>
    /// </list>
    /// </remarks>
    public static IEndpointConventionBuilder MapRDWebEndpoints(this IEndpointRouteBuilder routes, IEnumerable<RDWebEndpoint> endpoints,
        RDWebMachinePool pool)
    {
        var group = routes.MapGroup(string.Empty);
        foreach (var endpoint in endpoints)
        {
            group.MapMethods(endpoint.Route, [endpoint.HttpMethod], context => RDWebRequests.ServeAsync(context, endpoint, pool))
                .WithDisplayName($"{endpoint.HttpMethod} {endpoint.Route} ({endpoint.ModuleName}.{endpoint.ProcedureName})");
        }

        return group;
    }
}
