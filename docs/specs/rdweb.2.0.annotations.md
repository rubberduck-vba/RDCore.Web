# 2.0 Annotations

An RDCore.Web application declares its endpoints with three comment annotations. `'@Controller` makes a class module a controller; `'@HttpGet` and
`'@HttpPost` make a procedure of a controller an endpoint. They follow the comment annotation syntax of
[**RD-VBAL §3.0.1.1** Comment Annotations Syntax](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.3.0.1.token-semantics.html#3011-comment-annotations-syntax):
to VBA they are comments, so an annotated module compiles as it is.

> [!NOTE]
> **Not implemented.** Nothing reads the annotations from a module's source. The code that hosts the application passes each annotation's name and argument
> to [`RDWebEndpoint.FromAnnotation`](xref:RDCore.Web.Endpoints.RDWebEndpoint.FromAnnotation*), with the module's name and the procedure's name and
> parameters.

|Annotation|Kind|Written|Argument|
|---|---|---|---|
|`'@Controller`|Module annotation|In the module's declarations section.|The controller's route ([§3.1](rdweb.3.0.routes.md#31-controller-routes)). Optional.|
|`'@HttpGet`|Member annotation|Immediately above the procedure.|The endpoint's route ([§3.2](rdweb.3.0.routes.md#32-endpoint-routes)). Optional.|
|`'@HttpPost`|Member annotation|Immediately above the procedure.|The endpoint's route. Optional.|

Annotation names are compared without regard to case, as RD-VBA names are: `'@httpget` is `'@HttpGet`.

```vb
'@Controller("api/orders")
Option Explicit

'@HttpGet("{id}")
Public Function Order(ByVal id As Variant) As Variant
    ' ...
End Function

'@HttpPost
Public Sub Add(ByVal order As Variant)
    ' ...
End Sub
```

`Order` serves `GET /api/orders/{id}`, and `Add` serves `POST /api/orders`.

---
## 2.1 `'@Controller`

### Syntax

```vb
'@Controller
'@Controller("route")
```

The argument is a string literal: the route that prefixes the routes of the module's endpoints.

### Static Semantics

|Case|Controller route|
|---|---|
|`'@Controller("api/orders")`|`api/orders`.|
|`'@Controller` with no argument|The conventional route: the module's name less a `Controller` suffix ([§3.1](rdweb.3.0.routes.md#31-controller-routes)).|

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.Controller`](xref:RDCore.Web.Endpoints.RDWebAnnotations.Controller)|The annotation's name.|
|[`RDWebEndpoint.FromAnnotation`](xref:RDCore.Web.Endpoints.RDWebEndpoint.FromAnnotation*)|Takes the argument as `controllerRoute`; `null` when the annotation writes none.|

---
## 2.2 `'@HttpGet`

### Syntax

```vb
'@HttpGet
'@HttpGet("route")
```

The argument is a string literal: the endpoint's route, under the controller's.

### Static Semantics

The annotation declares an endpoint for the HTTP `GET` method, served by the procedure immediately below it. Its route is made as
[§3.2](rdweb.3.0.routes.md#32-endpoint-routes) describes. A `GET` endpoint has no body parameter: every parameter is bound from the route or the query
string.

### Runtime Semantics

A `GET` request whose path matches the endpoint's route runs the procedure. A request of another method on that route answers `405 Method Not Allowed`
and runs nothing.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.HttpGet`](xref:RDCore.Web.Endpoints.RDWebAnnotations.HttpGet)|The annotation's name.|
|[`RDWebAnnotations.HttpMethodOf`](xref:RDCore.Web.Endpoints.RDWebAnnotations.HttpMethodOf*)|`GET` for `HttpGet`, without regard to case.|

---
## 2.3 `'@HttpPost`

### Syntax

```vb
'@HttpPost
'@HttpPost("route")
```

The argument is a string literal: the endpoint's route, under the controller's.

### Static Semantics

The annotation declares an endpoint for the HTTP `POST` method, served by the procedure immediately below it. Its route is made as
[§3.2](rdweb.3.0.routes.md#32-endpoint-routes) describes. The first parameter of the procedure that the route does not bind is the body parameter: the
request's body binds to it. A `POST` whose route binds every parameter has no body parameter.

### Runtime Semantics

A `POST` request whose path matches the endpoint's route runs the procedure. A request of another method on that route answers
`405 Method Not Allowed` and runs nothing.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.HttpPost`](xref:RDCore.Web.Endpoints.RDWebAnnotations.HttpPost)|The annotation's name.|
|[`RDWebAnnotations.HttpMethodOf`](xref:RDCore.Web.Endpoints.RDWebAnnotations.HttpMethodOf*)|`POST` for `HttpPost`, without regard to case.|
|[`RDWebEndpoint.BodyParameter`](xref:RDCore.Web.Endpoints.RDWebEndpoint.BodyParameter)|The parameter the body binds to.|

---
## Other annotations

An annotation of any other name declares no endpoint: `FromAnnotation` returns `null` for it, and a procedure with only such annotations is not served.

---
## Tests

|Rule|Test|
|---|---|
|`'@HttpGet` serves `GET`, on the controller's route and its own|`RDWebEndpointTests.AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn`|
|`'@HttpPost` serves `POST`|`RDWebEndpointTests.AnHttpPostAnnotation_ServesTheProcedureForPost`|
|Names are compared without regard to case|`RDWebEndpointTests.AnAnnotationName_IsComparedWithoutRegardToCase`|
|Another annotation declares no endpoint|`RDWebEndpointTests.AnAnnotationOfNoHttpMethod_DeclaresNoEndpoint`|
|The body parameter of a `POST`; none for a `GET`|`RDWebEndpointTests.ThePostBody_BindsTheFirstParameterTheRouteDoesNotBind`, `RDWebEndpointTests.APostWhoseRouteBindsEveryParameter_HasNoBodyParameter`, `RDWebEndpointTests.AGet_HasNoBodyParameter`|
|Another method answers `405` and runs nothing|`RDWebEndpointsHostingTests.AnEndpoint_ServesOnlyItsOwnMethod`|
