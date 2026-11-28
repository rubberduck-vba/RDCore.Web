# RDCore.Web
<sup>_Ce document est disponible en [français](index.fr.md)_</sup>

> [!NOTE]
> RDCore.Web is pre-alpha. It maps and serves endpoints, but no RD-VBA procedure runs end to end yet: see [What works](#what-works).

---
## What it is

**RDCore.Web** is the web framework of [RDCore](https://rubberduck-vba.github.io/RDCore/). It serves the procedures of an RD-VBA project's class
modules as HTTP endpoints, through ASP.NET Core. Rubberduck comment annotations declare them: `'@Controller` on the module, `'@HttpGet` and `'@HttpPost` on
its procedures. A procedure's arguments and value pass as JSON, so the frontend can be any app that sends HTTP requests, such as a Node.js or React app.

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

`Hello` is the endpoint `GET /api/hello/{name}`. The annotations are comments to VBA, so the module is still a VBA class module.

---
## What works

|Part|Status|
|---|---|
|Endpoints from `'@Controller`, `'@HttpGet` and `'@HttpPost`: the HTTP method, and the route under the controller's|Works: read from a module's parse.|
|Binding: route values and query string values into the parameters of their names, and a `POST` body into the parameter `'@FromBody` names|Works.|
|Diagnostics: a `POST` that names no body parameter, and each further parameter that neither its route nor its body binds|Works in an RDCore platform that loads the RDWeb extension, with a quick fix that adds `'@FromBody`.|
|Responses: the procedure's value as JSON, no value, a run-time error as a problem details body|Works.|
|A pool of machines, each running one request at a time|Works.|
|Kept machines: a machine for each key of a controller with `'@KeptMachine`, made by `'@NewMachine` and never reset between its requests|Works.|
|JSON values: objects of the workspace's classes, typed arrays and `Collection`, and the `RDJson` library for VBA code|Works in RDCore's runtime. No endpoint reaches it: see the note below.|
|The host: a page that calls an endpoint and shows its answer|Works. The host maps no endpoint.|

> [!NOTE]
> **Not implemented.** No machine runs a procedure in an RDCore runtime session, and the host reads no module. An endpoint's request reaches a machine,
> but no RD-VBA code answers it.

---
## Where to go next

|Page|Read it to|
|---|---|
|[Overview](overview.md)|See how an annotated class module becomes endpoints, and how RDCore.Web relates to ASP.NET Core and to an Excel or Access project.|
|[Getting Started](getting-started.md)|Build and run the host, write a controller module and call it.|
|[RDCore.Web Reference](specs/rdweb.md)|Look up each part's syntax and rules (English only).|
|[API reference](xref:RDCore.Web.Endpoints)|Look up the .NET types that implement them (English only).|
|[RDCore.Web repository](https://github.com/rubberduck-vba/RDCore.Web)|Read the source, its status and how to build it.|

---
[ACCUEIL](index.fr.md) • HOME | [APERÇU](overview.fr.md) • [OVERVIEW](overview.md) | [DÉMARRER](getting-started.fr.md) • [GET STARTED](getting-started.md) | [RDCore](https://rubberduck-vba.github.io/RDCore/) | [SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html) | 🌐 [rubberduckvba.ca](https://rubberduckvba.ca)

---
