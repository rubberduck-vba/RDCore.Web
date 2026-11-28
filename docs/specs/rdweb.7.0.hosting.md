# 7.0 Hosting

An RDCore.Web application is an ASP.NET Core application: it maps the endpoints of its controller modules into its routing, and a pool of machines serves
them ([ASP.NET Core fundamentals](https://learn.microsoft.com/aspnet/core/fundamentals/)).

---
## 7.1 Mapping Endpoints

### Syntax

```csharp
// program: the application's composed program; endpoints: those its controller modules declare (§2.0).
await using var pool = await RDWebMachinePool.StartAsync(program, new RDWebMachinePoolOptions(), cancellationToken);
app.MapRDWebEndpoints(endpoints, pool);
```

`MapRDWebEndpoints` returns a builder of the conventions of every endpoint it mapped, such as an authorization policy
([Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)).

### Runtime Semantics

Each endpoint is mapped to its route ([§3.0](rdweb.3.0.routes.md)) and its HTTP method. Its display name is the method, the route and the procedure, such as
`GET api/orders/{id} (OrdersController.Find)`.

A request to an endpoint:

1. binds the procedure's arguments from its route values, query string and body ([§4.0](rdweb.4.0.binding.md));
2. runs the procedure on a machine of the pool ([§6.1](rdweb.6.0.machines.md#61-the-machine-pool));
3. answers with how the procedure ended ([§5.0](rdweb.5.0.responses.md)).

A request to an endpoint's route with another HTTP method answers `405 Method Not Allowed`, and runs nothing.

---
## 7.2 The RDCore.Web Host

### Runtime Semantics

`RDCore.Web.Host` runs one of two ways.

|Started|It is|
|---|---|
|By the RDCore platform, with the pipe the platform names|The RDWeb extension: it reports the `RDW` diagnostics of the documents the language server sends it, each linked to its page ([Diagnostics](../diagnostics/rdw00001.md)).|
|Any other way|An ASP.NET Core application that serves the files of its `wwwroot` folder, `index.html` at the root. It reads its command-line arguments and `appsettings.json` as ASP.NET Core does.|

> [!NOTE]
> **Not implemented.** The host composes no program and maps no endpoint. It serves its static files only.

---
## Implementation

|Type or member|Role|
|---|---|
|[`MapRDWebEndpoints`](xref:RDCore.Web.Hosting.RDWebEndpointRouteBuilderExtensions.MapRDWebEndpoints*)|Maps each endpoint to its route and HTTP method, served by the pool.|
|[`RDWebMachinePool.StartAsync`](xref:RDCore.Web.Machines.RDWebMachinePool.StartAsync*)|Starts the pool the endpoints are served by.|
|[`RDWebEndpointReader`](xref:RDCore.Web.Endpoints.RDWebEndpointReader)|Reads the endpoints a module's annotations declare.|

---
## Tests

|Rule|Test|
|---|---|
|An endpoint serves only its own HTTP method|`RDWebEndpointsHostingTests.AnEndpoint_ServesOnlyItsOwnMethod`|
|Through a pool of one machine, every request is served|`RDWebEndpointsHostingTests.ThroughAPoolOfOneMachine_EveryRequestIsServed`|
|The host serves its page at the root|`RDWebHostApplicationTests.TheRoot_ServesThePage_WhichLoadsItsScript`|
|The host serves the page's script|`RDWebHostApplicationTests.ThePagesScript_IsServed`|
|The extension reports the RDW findings, each linked to its page|`RDWebExtensionTests.TheExtensionsHandler_ReportsRDWebsFindings_EachLinkedToItsPage`|
