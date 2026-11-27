using Microsoft.AspNetCore.Http;

namespace RDCore.Web.Endpoints;

/// <summary>
/// The comment annotations that declare the endpoints of an RD-VBA module: <c>'@Controller</c> on the module, <c>'@HttpGet</c> and <c>'@HttpPost</c> on
/// its procedures.
/// </summary>
/// <remarks>
/// They are RD-VBA comment annotations (<strong>RD-VBAL §3.0.1.1</strong>): to MS-VBA they are comments. <c>'@Controller</c> is a module annotation,
/// written in the module's declarations section; <c>'@HttpGet</c> and <c>'@HttpPost</c> are member annotations, written immediately above the procedure.
/// Each takes one optional string argument, its route template.
/// </remarks>
public static class RDWebAnnotations
{
    /// <summary>
    /// The module annotation that makes a module a controller: <c>'@Controller("orders")</c>. Its argument is the route that prefixes the routes of the
    /// module's endpoints; without one, the module's name less a <c>Controller</c> suffix is (<see cref="RDWebEndpoint.ConventionalRouteOf"/>).
    /// </summary>
    public const string Controller = "Controller";

    /// <summary>
    /// The member annotation that serves a procedure for the HTTP <c>GET</c> method: <c>'@HttpGet("{id}")</c>.
    /// </summary>
    public const string HttpGet = "HttpGet";

    /// <summary>
    /// The member annotation that serves a procedure for the HTTP <c>POST</c> method: <c>'@HttpPost</c>. The request's JSON body binds to the first
    /// parameter of the procedure that its route does not name.
    /// </summary>
    public const string HttpPost = "HttpPost";

    /// <summary>
    /// The HTTP method a member annotation serves its procedure for.
    /// </summary>
    /// <param name="annotationName">The name of the annotation, compared without regard to case, as RD-VBA names are.</param>
    /// <returns><c>GET</c> for <see cref="HttpGet"/>, <c>POST</c> for <see cref="HttpPost"/>; <see langword="null"/> for any other annotation.</returns>
    public static string? HttpMethodOf(string annotationName)
        => string.Equals(annotationName, HttpGet, StringComparison.OrdinalIgnoreCase) ? HttpMethods.Get
            : string.Equals(annotationName, HttpPost, StringComparison.OrdinalIgnoreCase) ? HttpMethods.Post
            : null;
}
