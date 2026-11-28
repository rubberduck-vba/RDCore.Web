using Microsoft.AspNetCore.Http;

namespace RDCore.Web.Endpoints;

/// <summary>
/// The comment annotations that declare the endpoints of an RD-VBA module: <c>'@Controller</c> and <c>'@KeptMachine</c> on the module, <c>'@HttpGet</c>,
/// <c>'@HttpPost</c>, <c>'@FromBody</c> and <c>'@NewMachine</c> on its procedures.
/// </summary>
/// <remarks>
/// They are RD-VBA comment annotations (<strong>RD-VBAL §3.0.1.1</strong>): to MS-VBA they are comments. <c>'@Controller</c> and <c>'@KeptMachine</c> are
/// module annotations, written in the module's declarations section; the others are member annotations, written immediately above the procedure.
/// <c>'@HttpGet</c> and <c>'@HttpPost</c> each take one optional string argument, their route template.
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
    /// The member annotation that serves a procedure for the HTTP <c>POST</c> method: <c>'@HttpPost</c>. The request's JSON body binds to the parameter
    /// <see cref="FromBody"/> names, else to the first parameter of the procedure that its route does not name.
    /// </summary>
    public const string HttpPost = "HttpPost";

    /// <summary>
    /// The member annotation that names the parameter the request's JSON body binds to: <c>'@FromBody("order")</c>, written immediately above the
    /// procedure with its HTTP method annotations. It makes the binding explicit; without it, the body binds to the first parameter the route does not
    /// bind. It names a parameter because a parameter list holds no comment. A <c>GET</c> has no body, and binds none.
    /// </summary>
    public const string FromBody = "FromBody";

    /// <summary>
    /// The module annotation that keeps a machine for each value of a route parameter of a controller: <c>'@KeptMachine("game")</c>, written in the
    /// declarations section with <c>'@Controller</c>. Its argument names the route parameter whose value is the machine's key. A request to an endpoint of
    /// the controller runs on the machine of its key, which is never reset, instead of a machine of the pool (<see cref="Machines.RDWebKeptMachines"/>).
    /// The route parameter binds to the procedure's parameter of its name, when the procedure has one.
    /// </summary>
    public const string KeptMachine = "KeptMachine";

    /// <summary>
    /// The member annotation that makes the machine of a request's key before the procedure runs on it: <c>'@NewMachine</c>, written immediately above
    /// the procedure with its HTTP method annotations, in a controller that has <see cref="KeptMachine"/>. A request to any other endpoint of the
    /// controller runs on the machine its key already has.
    /// </summary>
    public const string NewMachine = "NewMachine";

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
