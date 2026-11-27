# Overview
<sup>_Ce document est disponible en [français](overview.fr.md)_</sup>

This page is for a reader who knows VBA, ASP.NET Core, or both. It says how the parts of an RDCore.Web application fit together, and links Microsoft's
[ASP.NET Core documentation](https://learn.microsoft.com/aspnet/core/introduction-to-aspnet-core) for what ASP.NET Core does. The
[RDCore.Web Reference](specs/rdweb.md) has the rules of each part.

> [!NOTE]
> **Not implemented.** No RD-VBA procedure runs end to end: no machine runs a procedure in an RDCore runtime session, and nothing reads the annotations
> from a module's source. The steps below that come after a machine receives the request describe what RDCore.Web does with a machine's answer.

---
## From a class module to a response

|Step|What happens|
|---|---|
|1. A class module|`'@Controller` makes the module a controller; `'@HttpGet` or `'@HttpPost` above a `Public` procedure makes it an endpoint.|
|2. Endpoints|Each endpoint has an HTTP method and a route: the controller's route, then the procedure's.|
|3. A request|ASP.NET Core routing matches the request to an endpoint. Its route values, query string values and JSON body become the procedure's arguments.|
|4. A machine|The request waits for an idle machine of the application's pool. A machine runs one request at a time.|
|5. The procedure's value|The machine answers with the value the procedure returned, as JSON, or with why it did not complete.|
|6. The response|A value is the JSON body (`200`); no value is `204`; a run-time error is `500` with a problem details body.|

For example, with this controller module:

```vb
'@Controller("api/orders")
Option Explicit

'@HttpGet("{id}")
Public Function Order(ByVal id As Variant, Optional ByVal format As Variant) As Variant
    ' ...
End Function

'@HttpPost("{id}/lines")
Public Function AddLine(ByVal id As Variant, ByVal line As Variant) As Variant
    ' ...
End Function
```

|Request|Procedure|Arguments|
|---|---|---|
|`GET /api/orders/7?format=short`|`Order`|`id` is `"7"`, `format` is `"short"`.|
|`GET /api/orders/7`|`Order`|`id` is `"7"`; `format` gets no argument.|
|`POST /api/orders/7/lines` with `{ "item": "duck", "quantity": 3 }`|`AddLine`|`id` is `"7"`; `line` receives the body's JSON.|

Route values and query string values bind as strings, to the parameter of their name without regard to case. A `POST` body binds to the first parameter
the route does not bind.

---
## Any frontend

The endpoints answer HTTP requests with JSON, so the frontend is any app that sends them: a Node.js or React app, a page served by the host, or another
service. The frontend does not know the endpoints are written in VBA.

---
## RDCore.Web and ASP.NET Core

RDCore.Web is an ASP.NET Core library. It maps the endpoints into ASP.NET Core routing (`MapRDWebEndpoints`), and Kestrel serves them. A .NET developer
hosts the application as any ASP.NET Core application, and adds to the endpoints what ASP.NET Core offers, such as an authorization policy.

The annotations play the part of ASP.NET Core's controller attributes:

|RDCore.Web|ASP.NET Core|Microsoft's documentation|
|---|---|---|
|`'@Controller("api")` on a class module|A controller class with `[Route("api")]`|[`RouteAttribute`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.routeattribute), [Routing to controller actions](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing)|
|`'@Controller` with no argument on `OrdersController`|`[Route("[controller]")]` on `OrdersController`: `Orders`|[Routing to controller actions](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing)|
|`'@HttpGet("{id}")` on a procedure|`[HttpGet("{id}")]` on an action|[`HttpGetAttribute`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.httpgetattribute)|
|`'@HttpPost` on a procedure|`[HttpPost]` on an action|[`HttpPostAttribute`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.httppostattribute)|
|A route such as `hello/{name}`|A route template|[Route templates](https://learn.microsoft.com/aspnet/core/fundamentals/routing#route-templates)|
|Route, query string and body values into parameters|Model binding|[Model binding](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding)|
|A run-time error as `500`|A problem details response|[Problem details](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling#problem-details)|
|The host|An ASP.NET Core application on Kestrel, serving static files|[Kestrel](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel), [Static files](https://learn.microsoft.com/aspnet/core/fundamentals/static-files)|

Route templates follow ASP.NET Core's syntax, since ASP.NET Core parses them. Binding is simpler than model binding: every route and query string value is a
string, and a body is the JSON it holds.

---
## RDCore.Web and an Excel or Access project

A controller is a VBA class module. The annotations are comments, so the module still compiles in the Visual Basic Editor. Adding them to a class module
of an Excel or Access project declares its endpoints.

An RDCore.Web application has no host application: no workbook or database is open. Code that only uses VBA and libraries such as Scripting is the code
a controller holds. Code that uses Excel's or Access's objects (`Range`, `DoCmd`) has none to use.

---
## What the tests pin

|Behaviour|Test|
|---|---|
|An endpoint's method and its route under the controller's|`RDWebEndpointTests.AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn`|
|A controller with no route, under its name less `Controller`|`RDWebEndpointTests.AControllerWithNoRoute_IsRoutedUnderItsModuleName_LessItsControllerSuffix`|
|Route values bind as strings, without regard to case|`RDWebEndpointsHostingTests.TheRouteValues_BindTheirParameters_AsStrings`|
|A query string value binds the parameter of its name|`RDWebEndpointsHostingTests.AQueryStringValue_BindsTheParameterOfItsName_AndAParameterNothingBinds_GetsNoArgument`|
|A `POST` body binds the first parameter the route does not bind|`RDWebEndpointTests.ThePostBody_BindsTheFirstParameterTheRouteDoesNotBind`, `RDWebEndpointsHostingTests.APostedJsonBody_BindsTheBodyParameter_AsTheJsonItHolds`|
|The value is the JSON body; no value is `204`|`RDWebEndpointsHostingTests.TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse`, `RDWebEndpointsHostingTests.AProcedureThatReturnsNoValue_AnswersNoContent`|
|A run-time error is `500` with its number and code|`RDWebEndpointsHostingTests.ARunTimeError_AnswersInternalServerError_WithTheErrorsNumberAndCode`|
|A machine runs one request at a time|`RDWebMachinePoolTests.APoolOfOneMachine_RunsASecondRequestOnlyOnceTheFirstHasReturned`|

---
[ACCUEIL](index.fr.md) • [HOME](index.md) | [APERÇU](overview.fr.md) • OVERVIEW | [DÉMARRER](getting-started.fr.md) • [GET STARTED](getting-started.md) | [RDCore](https://rubberduck-vba.github.io/RDCore/) | [SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html) | 🌐 [rubberduckvba.ca](https://rubberduckvba.ca)

---
