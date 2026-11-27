# RDCore.Web
<sup>_Ce document est disponible en [français](./README.fr.md)_</sup>

![VIVAT CUCUMIS](./assets/vivat-cucumis-stonecore.png)

## Before we begin.
> 👋 Hi! New here? _Rubberduck_ was always an open-source initiative. **RDCore honors it with an Open-Core formula**.  
> <small>See [rubberduckvba.ca](https://rubberduckvba.ca) for more information.</small>

This repository is for **RDCore.Web**, the web framework of [RDCore](https://github.com/rubberduck-vba/RDCore), under **⚖️GPLv3 or later**. RDCore.Web serves the class modules of an RD-VBA project through ASP.NET Core. Rubberduck annotations such as `'@Controller`, `'@HttpGet` and `'@HttpPost` describe the endpoints, and the frontend can be any Node.js app.

The RD-VBA language model comes from the [RDCore.SDK](https://github.com/rubberduck-vba/RDCore.SDK) package. What you build with RDCore.Web is yours (see [NOTICE.md](NOTICE.md)). The documentation is on the [RDCore.Web site](https://rubberduck-vba.github.io/RDCore.Web/).

## Project Status
RDCore.Web maps the endpoints of an application and serves them through ASP.NET Core. It reads the endpoints from the annotations in a module's source. It does not run RD-VBA code: no machine of the pool runs a procedure in an RDCore runtime session.

| Part | Status |
|---|---|
| Endpoints from the `'@Controller`, `'@HttpGet` and `'@HttpPost` annotations: the HTTP method, the route under the controller's, and a check that each route parameter names a parameter of the procedure | Works, from each annotation's name and argument. |
| The machine pool: each request runs alone on one machine, and a request that finds no idle machine waits for one without holding a thread | Works. The pool grows from its minimum to its maximum, resets each machine after each request, and recycles a machine after a set number of requests or one that did not end. A request past its deadline is canceled, and a machine that does not stop is replaced. A pool of one machine serves its requests one at a time. |
| Binding: a route value or a query string value into the procedure's parameter of that name, as a string; the JSON body of a `POST` into the parameter `'@FromBody` names, else the first parameter its route does not bind | Works. |
| Diagnostics of the body binding: [RDW00001](docs/diagnostics/rdw00001.md) where no `'@FromBody` names the body's parameter, with a quick fix that adds it, and [RDW00002](docs/diagnostics/rdw00002.md) at each further parameter of a `POST` that its route does not bind | Works, in an RDCore platform that loads the RDWeb extension. |
| Responses: the procedure's value as JSON (`200`), no value (`204`), a run-time error as `500` with a problem details body (RFC 9457) that holds the error's number and code | Works. |
| The host: a page in `wwwroot` that calls an endpoint and shows its answer | Works. The host maps no endpoint. |

The annotations of a controller module:

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

`Hello` is the endpoint `GET /api/hello/{name}`, and its route binds `name`. Running it, and answering with its `Dictionary` as a JSON object, take a machine that runs RD-VBA code, which RDCore.Web does not have.

## The solution

| Project | What it is |
|---|---|
| `RDCore.Web` | The framework library, and the repository's NuGet package. It builds on RDCore.SDK and ASP.NET Core. |
| `RDCore.Web.Host` | The ASP.NET Core host that serves an application. Started by an RDCore platform, it is the RDWeb extension, which reports the `RDW` diagnostics; `PublishExtension.ps1` publishes it into a platform's `Extensions/RDCore.Web/` folder. |
| `RDCore.Web.Tests` | The tests (MSTest). |

## Building from source

With the [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0), from the repository root:

```powershell
dotnet build RDCore.Web.slnx
dotnet test RDCore.Web.slnx
```

The RDCore.SDK package comes from nuget.org, at the version that `Directory.Build.props` sets (`RDCoreSdkVersion`). A build on a package that is not published passes its version and its feed, as `-p:RDCoreSdkVersion` and `-p:RestoreConfigFile`.

## License
RDCore.Web is licensed under the GNU General Public License, version 3 or any later version: see [LICENSE-GPLv3.md](LICENSE-GPLv3.md). The license file reproduces the official text, in English. A French version of the license is available on request. [NOTICE.md](NOTICE.md) covers the applications built with RDCore.Web and the RDCore.SDK package.

---
### See also
- [The RDCore.Web documentation](https://rubberduck-vba.github.io/RDCore.Web/)
- [RDCore](https://github.com/rubberduck-vba/RDCore)
- [RDCore.SDK](https://github.com/rubberduck-vba/RDCore.SDK)
- [RDCore.VSCode](https://github.com/rubberduck-vba/RDCore.VSCode)

<hr/>
<p align='left' style='margin-left: 32px;'>
<a href='https://rubberduck-vba.github.io/RDCore/index.fr.html'>ACCUEIL</a> • <a href='https://rubberduck-vba.github.io/RDCore/index.html'>HOME</a>  | ℹ️ <a href='https://rubberduck-vba.github.io/RDCore/introduction.fr.html'>BIENVENUE</a> • <a href='https://rubberduck-vba.github.io/RDCore/introduction.html'>WELCOME</a>  | 🧩 <a href='https://rubberduck-vba.github.io/RDCore/getting-started.fr.html'>BÂTISSONS</a> • <a href='https://rubberduck-vba.github.io/RDCore/getting-started.html'>BUILD</a>  | <a href='https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.html'><strong>RD-VBAL</strong></a>  |  <a href='https://rubberduck-vba.github.io/RDCore/api/RDCore.SDK.Model.Errors.VBCompileErrorId.html'>SDK</a>  | 🌐 <a href='https://rubberduckvba.ca'>rubberduckvba.ca</a>
</p>
<hr/>
<p align='center'><img alt='Logo™ 9562-7303 Québec inc.' src='./assets/ducky.png' style='width:200px; align:center;' /></p>
<h6 align='center'>V I V A T ❤️ C U C U M I S ™</h6>
<p align='center' style='font-size:8pt;'>
<small>© Copyright <strong>9562-7303 Québec inc.</strong> (2026)<br/><em>Seul, &quot;Rubberduck&quot; est utilisé pour fins de référence au projet open-source legacy <strong>utilisé publiquement ainsi depuis 2015</strong> et sans lien ni affiliation avec tout tiers détenteur d'une marque semblable dans quelque juridiction que ce soit.<br/>&quot;Rubberduck VBA&quot;, &quot;RDCore&quot; et &quot;VIVAT CUCUMIS&quot; sont des marques de commerce revendiquées par 9562-7303 Québec inc. (en attente); Toutes les marques appartiennent à leur détenteur respectif.<br/>RDCore n'est pas un produit de Microsoft et n'est pas affilié à Microsoft, ni directement, ni indirectement.<br/><br/>If used alone, <em>&quot;Rubberduck&quot; is used as a reference to the legacy open-source project <strong>the same way it has been used publicly since 2015</strong> and without any links or affiliation with any third-party trademark holders of a similar trademark in any jurdisdiction.<br/>&quot;Rubberduck VBA&quot;, &quot;RDCore&quot; and &quot;VIVAT CUCUMIS&quot; are trademarks claimed by 9562-7303 Québec inc. (pending). All trademarks belong to their respective owners.<br/>RDCore is not a Microsoft product and is not affiliated with Microsoft, directly or indirectly.</small>
</p>
