# 4.0 Binding

A request's route values, query string values and body become the procedure's arguments, each bound to the parameter of its name. The arguments pass to the
machine as JSON, and [§8.2](rdweb.8.0.json-values.md#82-from-json-to-vba) converts each one to its parameter's type. Binding is simpler than ASP.NET Core's
[model binding](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding): every route and query string value is a string, and a body is the JSON it
holds.

```vb
'@Controller("api/orders")
Option Explicit

'@HttpPost("{id}/lines")
Public Function AddLine(ByVal id As Variant, ByVal line As Variant, Optional ByVal notify As Variant) As Variant
    ' ...
End Function
```

|Request|`id`|`line`|`notify`|
|---|---|---|---|
|`POST /api/orders/7/lines?notify=yes` with `{ "item": "duck", "quantity": 3 }`|`"7"`, from the route|The body's JSON|`"yes"`, from the query string|
|`POST /api/orders/7/lines` with the same body|`"7"`|The body's JSON|No argument|

Argument names match parameters without regard to case, as VBA names do. A parameter that nothing binds gets no argument: the procedure's `Optional`
parameter is missing, or takes its default value ([§8.3](rdweb.8.0.json-values.md#83-arguments)).

---
## 4.1 Route Values

### Syntax

The segments of the request's path that the endpoint's route parameters match ([§3.3](rdweb.3.0.routes.md#33-route-parameters)).

### Runtime Semantics

Each route value binds to the parameter its route parameter names, as a JSON string, decoded from the path: in `/api/orders/42/lines/A%20B`, `id` is
`"42"` and `line` is `"A B"`.

---
## 4.2 Query String Values

### Syntax

The request's query string: `?format=short`.

### Runtime Semantics

A query string value binds to the parameter of its name that the route does not bind, as a JSON string. A query string value that names no parameter
binds nothing.

---
## 4.3 Request Body

### Syntax

The body of a `POST` request, of the JSON media type (`application/json`).

### Static Semantics

Only a `POST` endpoint with a body parameter reads a body: the first parameter its route does not bind ([§2.3](rdweb.2.0.annotations.md#23-httppost)).

### Runtime Semantics

|Request body|Effect|
|---|---|
|Valid JSON, of the JSON media type|The body's JSON binds to the body parameter, as it is: an object, an array, a string, a number, `true`, `false` or `null`.|
|Not valid JSON|`400 Bad Request` with a problem details body, and the procedure does not run.|
|Not of the JSON media type|`415 Unsupported Media Type` with a problem details body, and the procedure does not run.|

The problem details body's `detail` says which of the two it is ("The request body is not valid JSON.", "The request body of this endpoint is JSON
(application/json)."), in English or French, by the application's UI culture.

---
## Implementation

|Type or member|Role|
|---|---|
|[`MapRDWebEndpoints`](xref:RDCore.Web.Hosting.RDWebEndpointRouteBuilderExtensions.MapRDWebEndpoints*)|Maps each endpoint; a request to it binds its arguments as this chapter describes.|
|[`RDWebEndpoint.RouteParameters`](xref:RDCore.Web.Endpoints.RDWebEndpoint.RouteParameters)|The parameters the route binds.|
|[`RDWebEndpoint.BodyParameter`](xref:RDCore.Web.Endpoints.RDWebEndpoint.BodyParameter)|The parameter the body binds.|

---
## Tests

|Rule|Test|
|---|---|
|Route values bind as strings, decoded, without regard to case|`RDWebEndpointsHostingTests.TheRouteValues_BindTheirParameters_AsStrings`|
|A query string value binds the parameter of its name; one that names none binds nothing; a parameter nothing binds gets no argument|`RDWebEndpointsHostingTests.AQueryStringValue_BindsTheParameterOfItsName_AndAParameterNothingBinds_GetsNoArgument`|
|The body binds the body parameter as the JSON it holds|`RDWebEndpointsHostingTests.APostedJsonBody_BindsTheBodyParameter_AsTheJsonItHolds`|
|A body that is not valid JSON answers `400` and runs nothing|`RDWebEndpointsHostingTests.APostedBodyThatIsNotValidJson_AnswersBadRequest_AndRunsNothing`|
|A body that is not JSON answers `415` and runs nothing|`RDWebEndpointsHostingTests.APostedBodyThatIsNotJson_AnswersUnsupportedMediaType_AndRunsNothing`|
