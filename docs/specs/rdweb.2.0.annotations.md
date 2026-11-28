# 2.0 Annotations

An RDCore.Web application declares its endpoints with comment annotations. `'@Controller` makes a module a controller; `'@HttpGet` and `'@HttpPost`
make a procedure of a controller an endpoint, and `'@FromBody` names the parameter a `POST` request's body binds to. `'@KeptMachine` keeps a machine
for each key of a controller, and `'@NewMachine` makes it. They follow the comment annotation syntax of
[**RD-VBAL §3.0.1.1** Comment Annotations Syntax](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.3.0.1.token-semantics.html#3011-comment-annotations-syntax):
to VBA they are comments, so an annotated module compiles as it is.

[`RDWebEndpointReader`](xref:RDCore.Web.Endpoints.RDWebEndpointReader) reads a module's endpoints from the annotations of its parse.

> [!NOTE]
> **Not implemented.** The RDCore.Web host reads no module: it maps no endpoint.

|Annotation|Kind|Written|Argument|
|---|---|---|---|
|`'@Controller`|Module annotation|In the module's declarations section, or above the header of a block (`Module Name`, `Class Name`) in a file of blocks.|The controller's route ([§3.1](rdweb.3.0.routes.md#31-controller-routes)). Optional.|
|`'@HttpGet`|Member annotation|Immediately above the procedure.|The endpoint's route ([§3.2](rdweb.3.0.routes.md#32-endpoint-routes)). Optional.|
|`'@HttpPost`|Member annotation|Immediately above the procedure.|The endpoint's route. Optional.|
|`'@FromBody`|Member annotation|Immediately above the procedure, with its HTTP method annotations.|The body parameter's name ([§2.4](#24-frombody)).|
|`'@KeptMachine`|Module annotation|With `'@Controller`.|The route parameter whose value is the machine's key ([§2.5](#25-keptmachine)).|
|`'@NewMachine`|Member annotation|Immediately above the procedure, with its HTTP method annotations.|None ([§2.6](#26-newmachine)).|

Annotation names are compared without regard to case, as RD-VBA names are: `'@httpget` is `'@HttpGet`. A `'@Controller` below the first procedure of a
module is not in its declarations section, and does not make it a controller.

Only a `Public` `Sub` or `Function` serves an endpoint, or one with no access modifier, which is `Public`. A `Private` or `Friend` procedure serves none.

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

A class module with a default instance (`Attribute VB_PredeclaredId = True`,
[**MS-VBAL §5.2.4.1.2** Default Instance Variables Static Semantics](https://learn.microsoft.com/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/189fb41b-cc3a-4999-a6d2-ba89f72d2870))
whose procedures serve endpoints is an error: `Read` throws `RDWebEndpointException`, whose message names the module. A default instance never handles a
request. A controller is a class module whose `VB_PredeclaredId` is `False`, or a standard module.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.Controller`](xref:RDCore.Web.Endpoints.RDWebAnnotations.Controller)|The annotation's name.|
|[`RDWebEndpointReader.Read`](xref:RDCore.Web.Endpoints.RDWebEndpointReader.Read*)|Refuses a controller with a default instance.|
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
[§3.2](rdweb.3.0.routes.md#32-endpoint-routes) describes. The body parameter, which the request's JSON body binds to, is the one `'@FromBody` names
([§2.4](#24-frombody)), else the first parameter of the procedure that the route does not bind. A `POST` whose route binds every parameter, and that has
no `'@FromBody`, has no body parameter.

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
## 2.4 `'@FromBody`

### Syntax

```vb
'@HttpPost("{id}/lines")
'@FromBody("line")
Public Function AddLine(ByVal id As Variant, ByVal notify As Variant, ByVal line As Variant) As Variant
    ' ...
End Function
```

The argument is a string literal: the name of a parameter of the procedure. The annotation names a parameter because a parameter list holds no comment.

### Static Semantics

|Case|Effect|
|---|---|
|A `POST` endpoint|The body binds to the parameter named, matched without regard to case and written as the procedure spells it. The other parameters the route does not bind are bound from the query string.|
|A `GET` endpoint|None: a `GET` has no body, whatever `'@FromBody` names.|
|A `POST` whose name is not a parameter of the procedure|An error: `FromAnnotation` throws `RDWebEndpointException`, whose message names the parameter, the module and the procedure.|
|A `POST` whose name is a parameter the route binds|The same error.|

A `POST` with no `'@FromBody` still binds its body, to the first parameter the route does not bind. Diagnostic
[RDW00001](../diagnostics/rdw00001.md) reports it, with a quick fix that adds the annotation, and [RDW00002](../diagnostics/rdw00002.md) reports each
further parameter that neither the route nor the body binds.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.FromBody`](xref:RDCore.Web.Endpoints.RDWebAnnotations.FromBody)|The annotation's name.|
|[`RDWebEndpoint.FromAnnotation`](xref:RDCore.Web.Endpoints.RDWebEndpoint.FromAnnotation*)|Takes the argument as `bodyParameter`; `null` when the procedure has none.|

---
## 2.5 `'@KeptMachine`

### Syntax

```vb
'@Controller("api/games/{game}")
'@KeptMachine("game")
Option Explicit
```

The argument is a string literal: the name of a parameter of the controller's routes.

### Static Semantics

|Case|Effect|
|---|---|
|Every endpoint of the controller|Keyed by the value of the route parameter named. A request runs on its key's machine ([§6.2](rdweb.6.0.machines.md#62-kept-machines)).|
|The procedure has a parameter of that name|The route value binds to it, as any route value does.|
|The procedure has none|The route value binds nothing: the key need not be a parameter of the procedure.|
|An endpoint whose route has no parameter of that name|An error: `FromAnnotation` throws `RDWebEndpointException`, whose message names the key, the module and the procedure.|

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.KeptMachine`](xref:RDCore.Web.Endpoints.RDWebAnnotations.KeptMachine)|The annotation's name.|
|[`RDWebEndpoint.KeptMachineKey`](xref:RDCore.Web.Endpoints.RDWebEndpoint.KeptMachineKey)|The route parameter named; `null` for an endpoint whose requests run on a machine of the pool.|

---
## 2.6 `'@NewMachine`

### Syntax

```vb
'@HttpPost("new")
'@NewMachine
Public Sub NewGame()
    ' ...
End Sub
```

The annotation takes no argument.

### Static Semantics

A request to the endpoint makes its key's machine, then runs the procedure on it. A request to any other endpoint of the controller runs on the machine its
key has. A `'@NewMachine` in a controller without `'@KeptMachine` is an error: `FromAnnotation` throws `RDWebEndpointException`.

### Implementation

|Type or member|Role|
|---|---|
|[`RDWebAnnotations.NewMachine`](xref:RDCore.Web.Endpoints.RDWebAnnotations.NewMachine)|The annotation's name.|
|[`RDWebEndpoint.MakesMachine`](xref:RDCore.Web.Endpoints.RDWebEndpoint.MakesMachine)|Whether a request makes its key's machine.|

---
## Other annotations

An annotation of any other name declares no endpoint: `FromAnnotation` returns `null` for it, and a procedure with only such annotations is not served.

---
## Implementation

|Type or member|Role|
|---|---|
|[`RDWebEndpointReader.Read`](xref:RDCore.Web.Endpoints.RDWebEndpointReader.Read*)|Reads a module's endpoints from its parse: one for each HTTP method annotation of each public procedure, in the order they are written.|

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
|`'@FromBody` names the body parameter of a `POST`; a `GET` binds none|`RDWebEndpointTests.ThePostBody_BindsTheParameterFromBodyNames_InTheSpellingOfTheProcedure`, `RDWebEndpointTests.AGet_BindsNoBody_WhateverFromBodyNames`, `RDWebEndpointReaderTests.AFromBodyAnnotationOfAProcedure_NamesTheBodyParameterOfItsPost_AndItsGetBindsNone`|
|A `'@FromBody` the `POST` cannot bind is an error|`RDWebEndpointTests.AFromBodyParameterThePostCannotBind_IsAnEndpointException_NamingIt`|
|The body binds the parameter `'@FromBody` names; the query string binds the others|`RDWebEndpointsHostingTests.APostedJsonBody_BindsTheParameterFromBodyNames_AndTheQueryStringStillBindsTheOthers`|
|A module is read: a controller's procedures are its endpoints|`RDWebEndpointReaderTests.AnHttpGetProcedureOfAController_IsAnEndpoint_OnTheControllersRouteAndItsOwn`, `RDWebEndpointReaderTests.AModuleThatIsNoController_DeclaresNoEndpoint`|
|A controller with a default instance is an error; a predeclared class that is not a controller is not|`RDWebEndpointReaderTests.AControllerWithADefaultInstance_IsAnEndpointException_ThatNamesTheModule`, `RDWebEndpointReaderTests.AControllerWhosePredeclaredIdIsFalse_ServesItsEndpoints`, `RDWebEndpointReaderTests.AClassWithADefaultInstanceThatIsNoController_DeclaresNoEndpoint_AndIsNoError`|
|`'@Controller` only in the declarations section, or above a block's header|`RDWebEndpointReaderTests.AControllerAnnotationAfterTheFirstProcedure_DoesNotMakeTheModuleAController`, `RDWebEndpointReaderTests.InAFileOfBlocks_AControllerAnnotationAboveABlocksHeader_MakesThatBlocksModuleAController`|
|Only a public `Sub` or `Function` serves an endpoint|`RDWebEndpointReaderTests.AProcedureThatIsNotPublic_ServesNoEndpoint`, `RDWebEndpointReaderTests.AProcedureWithAnImplicitAccessModifier_IsPublic_AndASubServesAnEndpointToo`|
|One endpoint per HTTP method annotation, in the order written|`RDWebEndpointReaderTests.EachHttpMethodAnnotationOfAProcedure_IsAnEndpoint_InTheOrderWritten_AndOtherAnnotationsAreNone`|
|`'@KeptMachine` keys each endpoint of its controller; `'@NewMachine` marks only its own|`RDWebEndpointReaderTests.AKeptMachineAnnotationOfAController_KeysEachOfItsEndpoints_AndNewMachineMakesTheMachineOnlyWhereItIsWritten`, `RDWebEndpointReaderTests.AControllerWithoutKeptMachine_KeysNoEndpoint`, `RDWebEndpointTests.AnEndpointThatMakesTheMachine_SaysSo`|
|The key need not be a parameter of the procedure, and binds one of its name|`RDWebEndpointTests.TheMachinesKey_NeedNotBeAParameterOfTheProcedure_AndThePostBodyBindsTheFirstParameterAfterIt`, `RDWebEndpointTests.TheMachinesKey_BindsTheProcedureParameterOfItsName_WhenTheProcedureHasOne`, `RDWebEndpointTests.AnotherRouteParameterOfAKeptMachinesEndpoint_StillNamesAParameterOfTheProcedure`|
|A key the route lacks, or `'@NewMachine` without `'@KeptMachine`, is an error|`RDWebEndpointTests.AMachinesKeyThatIsNoParameterOfTheRoute_IsAnEndpointException_NamingIt`, `RDWebEndpointTests.AnEndpointThatMakesAMachine_WithNoKeyToMakeItFor_IsAnEndpointException`, `RDWebEndpointReaderTests.NewMachineInAControllerWithoutKeptMachine_IsAnEndpointException`|
