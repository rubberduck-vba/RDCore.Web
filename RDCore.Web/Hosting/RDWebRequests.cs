using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Hosting;
using RDCore.Web.Endpoints;
using RDCore.Web.Machines;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RDCore.Web.Hosting;

// one request to an endpoint: its arguments bound, the procedure run on a machine of the pool or on the kept machine of its key, and its result written as
// the response.
internal static class RDWebRequests
{
    // how many seconds a refused request is asked to wait before it tries again: a refusal is the pool being busy, which a moment can change.
    private const string RetryAfterSeconds = "1";

    public static async Task ServeAsync(HttpContext context, RDWebEndpoint endpoint, RDWebMachinePool pool, RDWebKeptMachines? keptMachines)
    {
        var request = context.Request;
        var arguments = ImmutableDictionary.CreateBuilder<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in endpoint.Parameters)
        {
            if (endpoint.RouteParameters.Contains(parameter))
            {
                if (request.RouteValues.TryGetValue(parameter, out var value) && value is not null)
                {
                    arguments[parameter] = JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
            }
            else if (request.Query.TryGetValue(parameter, out var values) && values.Count > 0)
            {
                arguments[parameter] = JsonValue.Create(values.ToString());
            }
        }

        if (endpoint.BodyParameter is { } body && context.Features.Get<IHttpRequestBodyDetectionFeature>() is { CanHaveBody: true })
        {
            if (!request.HasJsonContentType())
            {
                await Results.Problem(RDWebResources.Request_BodyMustBeJson, statusCode: StatusCodes.Status415UnsupportedMediaType).ExecuteAsync(context);
                return;
            }

            try
            {
                arguments[body] = await JsonNode.ParseAsync(request.Body, cancellationToken: context.RequestAborted);
            }
            catch (JsonException)
            {
                await Results.Problem(RDWebResources.Request_BodyIsNotJson, statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
                return;
            }
        }

        var invocation = new ProgramInvocation(endpoint.ModuleName, endpoint.ProcedureName, arguments.ToImmutable());
        ProgramInvocationResult result;
        if (endpoint.KeptMachineKey is { } keyName)
        {
            // the route matched, so it has the key: the endpoint's route has the parameter.
            var key = Convert.ToString(request.RouteValues[keyName], CultureInfo.InvariantCulture) ?? string.Empty;
            var answer = await keptMachines!.InvokeAsync(key, invocation, endpoint.MakesMachine, context.RequestAborted);
            if (answer.Status is not RDWebKeptMachineStatus.Served)
            {
                if (answer.Status is RDWebKeptMachineStatus.Full)
                {
                    context.Response.Headers.RetryAfter = RetryAfterSeconds;
                }

                await ResponseOf(answer.Status, key).ExecuteAsync(context);
                return;
            }

            result = answer.Result!;
        }
        else
        {
            result = await pool.InvokeAsync(invocation, context.RequestAborted);
        }

        if (result.Outcome is ExecutionOutcome.Refused)
        {
            context.Response.Headers.RetryAfter = RetryAfterSeconds;
        }

        await ResponseOf(endpoint, result).ExecuteAsync(context);
    }

    private static IResult ResponseOf(RDWebEndpoint endpoint, ProgramInvocationResult result) => result switch
    {
        { Outcome: ExecutionOutcome.Completed, HasValue: true } => Results.Text(result.Value?.ToJsonString() ?? "null", "application/json; charset=utf-8"),
        { Outcome: ExecutionOutcome.Completed } => Results.NoContent(),
        { Outcome: ExecutionOutcome.RuntimeError } => Results.Problem(
            detail: result.ErrorMessage,
            statusCode: StatusCodes.Status500InternalServerError,
            title: string.Format(CultureInfo.CurrentUICulture, RDWebResources.Problem_RuntimeError, result.ErrorNumber),
            extensions: new Dictionary<string, object?> { ["errorNumber"] = result.ErrorNumber, ["errorCode"] = result.ErrorCode }),
        { Outcome: ExecutionOutcome.Refused } => Results.Problem(
            detail: result.ErrorMessage,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: RDWebResources.Problem_Refused),
        // an End statement ends a kept machine, which is what the request asked for.
        { Outcome: ExecutionOutcome.Halted } when endpoint.KeptMachineKey is not null => Results.NoContent(),
        { Outcome: ExecutionOutcome.NotFound } => Results.Problem(
            detail: result.ErrorMessage,
            statusCode: StatusCodes.Status500InternalServerError,
            title: string.Format(CultureInfo.CurrentUICulture, RDWebResources.Problem_NotFound, endpoint.ModuleName, endpoint.ProcedureName)),
        _ => Results.Problem(
            detail: result.ErrorMessage,
            statusCode: StatusCodes.Status500InternalServerError,
            title: string.Format(
                CultureInfo.CurrentUICulture, RDWebResources.Problem_DidNotComplete, endpoint.ModuleName, endpoint.ProcedureName, result.Outcome)),
    };

    // a request that reached no kept machine.
    private static IResult ResponseOf(RDWebKeptMachineStatus status, string key) => status switch
    {
        RDWebKeptMachineStatus.NoMachine => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: string.Format(CultureInfo.CurrentUICulture, RDWebResources.Problem_NoMachine, key)),
        RDWebKeptMachineStatus.AlreadyMade => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: string.Format(CultureInfo.CurrentUICulture, RDWebResources.Problem_MachineAlreadyMade, key)),
        _ => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: RDWebResources.Problem_MachinesFull),
    };
}
