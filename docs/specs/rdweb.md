# RDCore.Web Reference

> [!NOTE]
> Cette section n'est disponible qu'en anglais.
> _This section is only available in English_.

This reference describes **RDCore.Web**, the web framework of RDCore. An RDCore.Web application serves the procedures of an RD-VBA project's controller
modules as HTTP endpoints, through ASP.NET Core. Each section gives a part's syntax, what makes it valid, what it does when a request arrives, and the types
that implement it. The API reference documents those types.

RD-VBA itself is described by [**RD-VBAL**](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.html), and VBA by
[**MS-VBAL**](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/d5418146-0bd2-45eb-9c7a-fd9502722c74).

> [!NOTE]
> **Not implemented.** No RD-VBA procedure runs end to end. RDCore.Web reads the endpoints from their annotations, binds a request's values and answers
> with what a machine returns, but no machine runs a procedure in an RDCore runtime session.

---
## The site

|Page|Contents|
|---|---|
|[Home](../index.md) (English and French)|What RDCore.Web is, what works, and where to go next.|
|[Overview](../overview.md) (English and French)|How an annotated class module becomes endpoints behind any frontend, and how that relates to ASP.NET Core and to an Excel or Access project. A table maps each annotation to its ASP.NET Core counterpart.|
|[Getting Started](../getting-started.md) (English and French)|Build and run the host, write a controller module, and call it. Where it stops: no RD-VBA procedure runs end to end.|
|This reference (English)|Each part's syntax and rules, and the types that implement it.|
|[API reference](xref:RDCore.Web.Endpoints) (English)|The public types of RDCore.Web, generated from their xmldocs.|

Where a page relies on ASP.NET Core (routing, model binding, problem details, Kestrel, hosting), it links
[Microsoft's ASP.NET Core documentation](https://learn.microsoft.com/aspnet/core/) instead of explaining it.

---
## Table of Contents

A section not yet written is listed without a link.

- 1.0 Applications
- [2.0 Annotations](rdweb.2.0.annotations.md)
  - [2.1 `'@Controller`](rdweb.2.0.annotations.md#21-controller)
  - [2.2 `'@HttpGet`](rdweb.2.0.annotations.md#22-httpget)
  - [2.3 `'@HttpPost`](rdweb.2.0.annotations.md#23-httppost)
  - [2.4 `'@FromBody`](rdweb.2.0.annotations.md#24-frombody)
- [3.0 Routes](rdweb.3.0.routes.md)
  - [3.1 Controller Routes](rdweb.3.0.routes.md#31-controller-routes)
  - [3.2 Endpoint Routes](rdweb.3.0.routes.md#32-endpoint-routes)
  - [3.3 Route Parameters](rdweb.3.0.routes.md#33-route-parameters)
- [4.0 Binding](rdweb.4.0.binding.md)
  - [4.1 Route Values](rdweb.4.0.binding.md#41-route-values)
  - [4.2 Query String Values](rdweb.4.0.binding.md#42-query-string-values)
  - [4.3 Request Body](rdweb.4.0.binding.md#43-request-body)
- [5.0 Responses](rdweb.5.0.responses.md)
  - [5.1 Returned Values](rdweb.5.0.responses.md#51-returned-values)
  - [5.2 Run-time Errors](rdweb.5.0.responses.md#52-run-time-errors)
  - [5.3 Other Outcomes](rdweb.5.0.responses.md#53-other-outcomes)
- 6.0 Machines
  - 6.1 The Machine Pool
- 7.0 Hosting
  - 7.1 Mapping Endpoints
  - 7.2 The RDCore.Web Host
- [8.0 JSON Values](rdweb.8.0.json-values.md)
  - [8.1 From VBA to JSON](rdweb.8.0.json-values.md#81-from-vba-to-json)
  - [8.2 From JSON to VBA](rdweb.8.0.json-values.md#82-from-json-to-vba)
  - [8.3 Arguments](rdweb.8.0.json-values.md#83-arguments)
  - [8.4 Objects](rdweb.8.0.json-values.md#84-objects)
  - [8.5 The RDJson Library](rdweb.8.0.json-values.md#85-the-rdjson-library)

|Section|Covers|
|---|---|
|1.0|What an application is: a request, a machine of the pool, the procedure, and the response.|
|2.0|The comment annotations that declare a controller module and its endpoints.|
|3.0|How an endpoint's route is made from its controller's route and its own, and the parameters a route names.|
|4.0|How a request's route values, query string and body become the procedure's arguments.|
|5.0|The response each outcome of the procedure answers: a value, no value, a run-time error, and any other outcome.|
|6.0|The machines that run the procedures, and the pool that gives each request one of its own.|
|7.0|How an ASP.NET Core application maps the endpoints, and the host that serves the application's pages.|
|8.0|How a procedure's arguments and returned value convert between VBA and JSON, objects of the workspace's classes included, and the `RDJson` library that VBA code converts its own values with.|

---
## Conventions

### Section numbers

Each chapter is one page; its sections are the headings of that page. A section is cited as **§2.1**, and a section of RD-VBAL or MS-VBAL with its prefix,
as **RD-VBAL §3.0.1.1** or **MS-VBAL §5.3.1.11**.

### Pages

|Heading|Contents|
|---|---|
|Syntax|How the part is written in a module, or in a request.|
|Static Semantics|What makes it valid, and what happens when it is not: what the endpoints are made of before any request arrives.|
|Runtime Semantics|What happens when a request arrives.|
|Implementation|The types that implement the part, linked to the API reference.|

### Notes

|Note|Meaning|
|---|---|
|**Not implemented.** …|A limitation: what RDCore.Web does not do, and what happens instead when that is known.|
