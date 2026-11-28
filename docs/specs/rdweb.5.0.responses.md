# 5.0 Responses

The machine answers each request with how the procedure ended, and RDCore.Web writes that as the response. Every outcome other than a returned value or none
answers with a problem details body (RFC 9457, media type `application/problem+json`), as ASP.NET Core writes it
([Problem details](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling#problem-details)).

|Outcome|Status|Body|
|---|---|---|
|The procedure completed and returned a value|`200 OK`|The value, as JSON ([§5.1](#51-returned-values)).|
|The procedure completed and returned none|`204 No Content`|None.|
|A run-time error the procedure did not handle|`500 Internal Server Error`|Problem details with the error's number and code ([§5.2](#52-run-time-errors)).|
|No machine was idle in time, or the machine refused the request|`503 Service Unavailable`|Problem details ([§5.3](#53-other-outcomes)).|
|Any other outcome|`500 Internal Server Error`|Problem details ([§5.3](#53-other-outcomes)).|

> [!NOTE]
> **Not implemented.** No machine runs a procedure in an RDCore runtime session, so no RD-VBA procedure gives these outcomes. They are what RDCore.Web
> answers with a machine's result.

---
## 5.1 Returned Values

### Runtime Semantics

A `Function` or a `Property Get` that completes answers `200 OK`, with the value it returned as the JSON body (media type `application/json`).
[§8.1](rdweb.8.0.json-values.md#81-from-vba-to-json) converts the value. `Empty`, `Null` and `Nothing` are the body `null`.

A `Sub` that completes answers `204 No Content`, with no body.

---
## 5.2 Run-time Errors

### Runtime Semantics

A run-time error that the procedure does not handle answers `500 Internal Server Error` with a problem details body. RDCore.Web sets these members, and
ASP.NET Core adds its own, such as `type`.

|Member|Value|
|---|---|
|`status`|`500`.|
|`title`|`Run-time error 'n'`, where `n` is the error's number, in English or French by the application's UI culture.|
|`detail`|The error's description, as `Err.Description` gives it.|
|`errorNumber`|The error's number, as `Err.Number` gives it.|
|`errorCode`|The error's diagnostic code, such as `VBR00011`: `VBR` for one the runtime raised, `VBA` for one the program raised ([**RD-VBAL §2.6.3** Runtime Errors](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.2.6.3.runtime-errors.html)).|

---
## 5.3 Other Outcomes

### Runtime Semantics

|Outcome|Status|`title`|
|---|---|---|
|No machine was idle in time ([§6.1](rdweb.6.0.machines.md#deadlines)), or the machine refused the request|`503 Service Unavailable`|`The machine refused the request.`|
|The procedure was not found|`500 Internal Server Error`|`There is no procedure Module.Procedure to invoke.`|
|Any other: the program had a syntax error, halted at an `End` statement, was interrupted, or ran code that is not implemented|`500 Internal Server Error`|`The procedure Module.Procedure did not complete (Outcome).`|

The `title` is in English or French by the application's UI culture, and the `detail` is the machine's explanation of the outcome.

A `503` has the header `Retry-After: 1`: the client may send the request again a second later.

---
## Implementation

|Type or member|Role|
|---|---|
|[`MapRDWebEndpoints`](xref:RDCore.Web.Hosting.RDWebEndpointRouteBuilderExtensions.MapRDWebEndpoints*)|Maps each endpoint; a request to it answers as this chapter describes.|
|[`ExecutionOutcome`](xref:RDCore.SDK.Platform.Protocol.ExecutionOutcome)|How the procedure ended, which picks the response.|

---
## Tests

|Rule|Test|
|---|---|
|A value is the JSON body of a `200`|`RDWebEndpointsHostingTests.TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse`|
|A value of `null` is the body `null`|`RDWebEndpointsHostingTests.AProcedureThatReturnsNull_AnswersJsonNull`|
|No value answers `204`|`RDWebEndpointsHostingTests.AProcedureThatReturnsNoValue_AnswersNoContent`|
|A run-time error answers `500` with its number, code and description|`RDWebEndpointsHostingTests.ARunTimeError_AnswersInternalServerError_WithTheErrorsNumberAndCode`|
|A refused request answers `503`, with when to retry|`RDWebEndpointsHostingTests.ARefusedRequest_AnswersServiceUnavailable_WithWhenToRetry`|
|A procedure not found, halted, or not implemented answers `500` with problem details|`RDWebEndpointsHostingTests.AProcedureThatDoesNotComplete_AnswersInternalServerError`|
