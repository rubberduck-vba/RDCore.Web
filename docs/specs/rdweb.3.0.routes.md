# 3.0 Routes

An endpoint's route is the controller's route, then the endpoint's own. Routes are route templates in ASP.NET Core's syntax, which ASP.NET Core parses and
matches: [Route templates](https://learn.microsoft.com/aspnet/core/fundamentals/routing#route-templates) describes them. A route parameter in braces,
such as `{id}`, names a parameter of the procedure.

```vb
'@Controller("api/orders")
Option Explicit

'@HttpGet("{id}/lines/{line}")
Public Function OrderLine(ByVal id As Variant, ByVal line As Variant) As Variant
    ' ...
End Function
```

`OrderLine` serves `GET /api/orders/{id}/lines/{line}`; the route binds `id` and `line`.

---
## 3.1 Controller Routes

### Syntax

The argument of `'@Controller` ([§2.1](rdweb.2.0.annotations.md#21-controller)), or none.

### Static Semantics

|Case|Controller route|
|---|---|
|An argument|The argument.|
|No argument, a module name that ends with `Controller` with letters before it|The name less those ten letters, in any case: `OrdersController` is `Orders`, `ordersCONTROLLER` is `orders`.|
|No argument, any other module name|The name as written: `Orders` is `Orders`, `Controller` is `Controller`, `ControllerOfThings` is `ControllerOfThings`.|

This is ASP.NET Core's naming of a controller, as `[Route("[controller]")]` gives it
([Routing to controller actions](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing)).

### Runtime Semantics

Routes match without regard to case, so a request for `/orders/all` matches the route `/Orders/all`.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebEndpoint.ConventionalRouteOf`](xref:RDCore.Web.Endpoints.RDWebEndpoint.ConventionalRouteOf*)|The route of a controller whose annotation writes none.|

---
## 3.2 Endpoint Routes

### Syntax

The argument of `'@HttpGet` or `'@HttpPost` ([§2.2](rdweb.2.0.annotations.md#22-httpget), [§2.3](rdweb.2.0.annotations.md#23-httppost)), or none.

### Static Semantics

|Case|Endpoint route|
|---|---|
|No argument|The controller's route.|
|A route that starts with `/` or `~/`|The route, from the root of the application. The controller's route does not prefix it: `'@HttpGet("/health")` is `/health`.|
|Any other route|The controller's route, then the route: `'@HttpGet("{id}")` under `api/orders` is `/api/orders/{id}`.|

The two parts are joined by one `/`, whatever slashes they start or end with: `/api/orders/` and `{id}/` join as `/api/orders/{id}`. Every route is from
the root of the application, and starts with `/`.

A route that is not a valid route template is an error: `FromAnnotation` throws `RDWebEndpointException`, whose message names the route, the module and the
procedure, and gives ASP.NET Core's reason. `{id` is one such route.

### Runtime Semantics

ASP.NET Core routing matches a request's path to the route, without regard to case
([Routing in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/routing)). A path that matches no endpoint's route answers
`404 Not Found`.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebEndpoint.FromAnnotation`](xref:RDCore.Web.Endpoints.RDWebEndpoint.FromAnnotation*)|Joins the routes, and parses the result as a route template.|
|[`RDWebEndpoint.Route`](xref:RDCore.Web.Endpoints.RDWebEndpoint.Route)|The endpoint's route, from the root.|
|[`RDWebEndpointException`](xref:RDCore.Web.Endpoints.RDWebEndpointException)|A route that is not a valid route template.|

---
## 3.3 Route Parameters

### Syntax

A name in braces in a route: `{id}`. [Route templates](https://learn.microsoft.com/aspnet/core/fundamentals/routing#route-templates) gives the
syntax.

### Static Semantics

Each route parameter names a parameter of the procedure, without regard to case: `{ID}` names `Id`. The route binds those parameters; the others are bound
from the query string, or from the body of a `POST` ([§2.3](rdweb.2.0.annotations.md#23-httppost)).

A route parameter that names no parameter of the procedure is an error: `FromAnnotation` throws `RDWebEndpointException`, whose message names the route
parameter, the module and the procedure. `'@HttpGet("{orderId}")` above `Public Function Order(ByVal id As Variant)` is one such route.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebEndpoint.RouteParameters`](xref:RDCore.Web.Endpoints.RDWebEndpoint.RouteParameters)|The parameters the route binds, in the order the procedure declares them.|
|[`RDWebEndpoint.Parameters`](xref:RDCore.Web.Endpoints.RDWebEndpoint.Parameters)|Every parameter of the procedure, in order.|

---
## Tests

|Rule|Test|
|---|---|
|A controller with no route, under its name less `Controller`|`RDWebEndpointTests.AControllerWithNoRoute_IsRoutedUnderItsModuleName_LessItsControllerSuffix`|
|The conventional route of each kind of name|`RDWebEndpointTests.TheConventionalRoute_IsTheModuleName_LessATrailingControllerSuffix_WhenAnythingIsLeft`|
|A request matches a conventional route in any case|`RDWebEndpointsHostingTests.AControllerWithNoRoute_ServesTheRequestsForItsNameLessTheControllerSuffix_InAnyCase`|
|No endpoint route: the controller's|`RDWebEndpointTests.AMemberAnnotationWithNoRoute_ServesTheControllersRoute`|
|A route from the root|`RDWebEndpointTests.ARouteFromTheRoot_IsNotPrefixedByTheControllersRoute`|
|One `/` between the parts|`RDWebEndpointTests.TheSlashesAroundTheRoutes_JoinThemOnce`|
|An invalid route template is an error|`RDWebEndpointTests.ARouteThatIsNoRouteTemplate_IsAnError`|
|Route parameters name parameters, without regard to case|`RDWebEndpointTests.ARouteParameter_BindsTheProcedureParameterOfItsName_WithoutRegardToCase`|
|A route parameter that names no parameter is an error|`RDWebEndpointTests.ARouteParameterThatIsNoParameterOfTheProcedure_IsAnError`|
