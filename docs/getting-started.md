# Getting Started
<sup>_Ce document est disponible en [français](getting-started.fr.md)_</sup>

This page builds and runs the RDCore.Web host, writes a controller module, and calls it. It assumes a command line, [Git](https://git-scm.com/), and the
[.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0). It assumes no ASP.NET Core: the [Overview](overview.md) says how RDCore.Web uses it.

> [!NOTE]
> **Not implemented.** No RD-VBA procedure runs end to end. The host serves its page, but it maps no endpoint, and no machine runs a procedure, so the
> call at the end of this page answers `404 Not Found`. [Where it stops](#where-it-stops) says what is missing.

---
## 1. Build and test

```powershell
git clone https://github.com/rubberduck-vba/RDCore.Web.git
cd RDCore.Web
dotnet build RDCore.Web.slnx
dotnet test RDCore.Web.slnx
```

|Project|What it is|
|---|---|
|`RDCore.Web`|The framework library.|
|`RDCore.Web.Host`|The ASP.NET Core application that serves an application's pages.|
|`RDCore.Web.Tests`|The tests.|

---
## 2. Run the host

```powershell
dotnet run --project RDCore.Web.Host
```

The host writes the address it listens on, `http://localhost:5000` by default; [Kestrel](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel)
explains how to change it. Open that address in a browser: the host serves `index.html` from its `wwwroot` folder, a page with a **Name** box and a
**Call** button. Press Ctrl+C to stop the host.

`RDWebHostApplicationTests.TheRoot_ServesThePage_WhichLoadsItsScript` pins the page.

---
## 3. Write a controller module

The page calls this endpoint, `GET /api/hello/{name}`:

```vb
'@Controller("api")
Option Explicit

'@HttpGet("hello/{name}")
Public Function Hello(ByVal name As Variant) As Variant
    Dim greeting As Object
    Set greeting = CreateObject("Scripting.Dictionary")
    greeting.Add "greeting", "Hello, " & name & "!"
    Set Hello = greeting
End Function
```

|Line|What it declares|
|---|---|
|`'@Controller("api")`|The class module is a controller. Its route, `api`, prefixes the routes of its endpoints. It goes in the module's declarations section.|
|`'@HttpGet("hello/{name}")`|`Hello` serves `GET` requests on `api/hello/{name}`. It goes on the line above the procedure.|
|`{name}`|A route parameter: it binds the procedure's `name` parameter, as a string.|

A route parameter that names no parameter of the procedure is an error. The [RDCore.Web Reference](specs/rdweb.md) has the rules of each annotation.

`RDWebEndpointTests.AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn` and
`RDWebEndpointsHostingTests.TheRouteValues_BindTheirParameters_AsStrings` pin the endpoint and its binding.

---
## 4. Call it

Type a name on the page and select **Call**, or send the request from a command line:

```powershell
curl.exe -i http://localhost:5000/api/hello/World
```

Both answer `404 Not Found`: the host maps no endpoint.

A procedure's arguments and value pass as JSON. When a machine answers with the procedure's value, RDCore.Web writes it as the response's body, with
`200 OK`. `RDWebEndpointsHostingTests.TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse` pins that response, with a machine whose answer the test gives.

---
## Where it stops

|Missing part|What it means|
|---|---|
|A reader of a module's annotations|Endpoints are made from each annotation's name and argument, which the code that hosts the application passes in. Nothing reads them from a module's source.|
|A machine that runs a procedure|No machine runs a procedure in an RDCore runtime session, so no RD-VBA code answers a request.|
|Endpoints in the host|The host maps none: it serves its page only.|

---
## Next

- [Overview](overview.md): how the parts fit together, and how they relate to ASP.NET Core.
- [RDCore.Web Reference](specs/rdweb.md): each part's syntax and rules (English only).
- [API reference](xref:RDCore.Web.Endpoints): the .NET types (English only).

---
[ACCUEIL](index.fr.md) • [HOME](index.md) | [APERÇU](overview.fr.md) • [OVERVIEW](overview.md) | [DÉMARRER](getting-started.fr.md) • GET STARTED | [RDCore](https://rubberduck-vba.github.io/RDCore/) | [SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html) | 🌐 [rubberduckvba.ca](https://rubberduckvba.ca)

---
