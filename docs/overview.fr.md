# Aperçu
<sup>_This document is available in [English](overview.md)_</sup>

Cette page s'adresse à qui connaît VBA, ASP.NET Core, ou les deux. Elle explique comment s'agencent les parties d'une application RDCore.Web, et renvoie
à la [documentation d'ASP.NET Core](https://learn.microsoft.com/fr-fr/aspnet/core/introduction-to-aspnet-core) de Microsoft pour ce que fait ASP.NET Core.
La [référence de RDCore.Web](specs/rdweb.md), en anglais, donne les règles de chaque partie.

> [!NOTE]
> **Non implémenté.** Aucune procédure RD-VBA ne s'exécute de bout en bout : aucune machine n'exécute une procédure dans une session d'exécution RDCore,
> et rien ne lit les annotations dans le code source d'un module. Les étapes qui suivent la réception de la requête par une machine décrivent ce que fait
> RDCore.Web de la réponse d'une machine.

---
## D'un module de classe à une réponse

|Étape|Ce qui se passe|
|---|---|
|1. Un module de classe|`'@Controller` fait du module un contrôleur; `'@HttpGet` ou `'@HttpPost` au-dessus d'une procédure `Public` en fait un point de terminaison.|
|2. Les points de terminaison|Chacun a une méthode HTTP et une route : celle du contrôleur, puis celle de la procédure.|
|3. Une requête|Le routage d'ASP.NET Core associe la requête à un point de terminaison. Ses valeurs de route, de chaîne de requête et son corps JSON deviennent les arguments de la procédure.|
|4. Une machine|La requête attend une machine libre du bassin de l'application. Une machine n'exécute qu'une requête à la fois.|
|5. La valeur de la procédure|La machine répond avec la valeur que la procédure a retournée, en JSON, ou avec la raison pour laquelle elle ne s'est pas terminée.|
|6. La réponse|Une valeur est le corps JSON (`200`); aucune valeur donne `204`; une erreur d'exécution donne `500` avec un corps « problem details ».|

Par exemple, avec ce module contrôleur :

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

|Requête|Procédure|Arguments|
|---|---|---|
|`GET /api/orders/7?format=short`|`Order`|`id` vaut `"7"`, `format` vaut `"short"`.|
|`GET /api/orders/7`|`Order`|`id` vaut `"7"`; `format` ne reçoit aucun argument.|
|`POST /api/orders/7/lines` avec `{ "item": "duck", "quantity": 3 }`|`AddLine`|`id` vaut `"7"`; `line` reçoit le JSON du corps.|

Les valeurs de la route et de la chaîne de requête sont liées comme chaînes, au paramètre de leur nom sans égard à la casse. Le corps d'un `POST` est lié
au premier paramètre que la route ne lie pas.

---
## Toute interface

Les points de terminaison répondent en JSON à des requêtes HTTP, alors l'interface est toute application qui en envoie : une application Node.js ou React,
une page servie par l'hôte, ou un autre service. L'interface ne sait pas que les points de terminaison sont écrits en VBA.

---
## RDCore.Web et ASP.NET Core

RDCore.Web est une bibliothèque ASP.NET Core. Il associe les points de terminaison au routage d'ASP.NET Core (`MapRDWebEndpoints`), et Kestrel les sert.
Un développeur .NET héberge l'application comme toute application ASP.NET Core, et ajoute aux points de terminaison ce qu'offre ASP.NET Core, comme une
stratégie d'autorisation.

Les annotations tiennent le rôle des attributs de contrôleur d'ASP.NET Core :

|RDCore.Web|ASP.NET Core|Documentation de Microsoft|
|---|---|---|
|`'@Controller("api")` sur un module de classe|Une classe contrôleur avec `[Route("api")]`|[`RouteAttribute`](https://learn.microsoft.com/fr-fr/dotnet/api/microsoft.aspnetcore.mvc.routeattribute), [Routage vers les actions du contrôleur](https://learn.microsoft.com/fr-fr/aspnet/core/mvc/controllers/routing)|
|`'@Controller` sans argument sur `OrdersController`|`[Route("[controller]")]` sur `OrdersController` : `Orders`|[Routage vers les actions du contrôleur](https://learn.microsoft.com/fr-fr/aspnet/core/mvc/controllers/routing)|
|`'@HttpGet("{id}")` sur une procédure|`[HttpGet("{id}")]` sur une action|[`HttpGetAttribute`](https://learn.microsoft.com/fr-fr/dotnet/api/microsoft.aspnetcore.mvc.httpgetattribute)|
|`'@HttpPost` sur une procédure|`[HttpPost]` sur une action|[`HttpPostAttribute`](https://learn.microsoft.com/fr-fr/dotnet/api/microsoft.aspnetcore.mvc.httppostattribute)|
|Une route comme `hello/{name}`|Un modèle de route|[Modèles de route](https://learn.microsoft.com/fr-fr/aspnet/core/fundamentals/routing#route-templates)|
|Les valeurs de la route, de la chaîne de requête et du corps aux paramètres|La liaison de modèle|[Liaison de modèle](https://learn.microsoft.com/fr-fr/aspnet/core/mvc/models/model-binding)|
|Une erreur d'exécution en `500`|Une réponse « problem details »|[Détails du problème](https://learn.microsoft.com/fr-fr/aspnet/core/fundamentals/error-handling#problem-details)|
|L'hôte|Une application ASP.NET Core sur Kestrel, qui sert des fichiers statiques|[Kestrel](https://learn.microsoft.com/fr-fr/aspnet/core/fundamentals/servers/kestrel), [Fichiers statiques](https://learn.microsoft.com/fr-fr/aspnet/core/fundamentals/static-files)|

Les routes suivent la syntaxe d'ASP.NET Core, puisque c'est ASP.NET Core qui les analyse. La liaison est plus simple que la liaison de modèle : toute valeur
de la route ou de la chaîne de requête est une chaîne, et un corps est le JSON qu'il contient.

---
## RDCore.Web et un projet Excel ou Access

Un contrôleur est un module de classe VBA. Les annotations sont des commentaires, alors le module se compile toujours dans l'éditeur Visual Basic. Les
ajouter à un module de classe d'un projet Excel ou Access déclare ses points de terminaison.

Une application RDCore.Web n'a pas d'application hôte : aucun classeur ni aucune base de données n'est ouvert. Le code qui n'utilise que VBA et des
bibliothèques comme Scripting est le code que contient un contrôleur. Le code qui utilise les objets d'Excel ou d'Access (`Range`, `DoCmd`) n'en a aucun à
utiliser.

---
## Ce que les tests fixent

|Comportement|Test|
|---|---|
|La méthode d'un point de terminaison et sa route sous celle du contrôleur|`RDWebEndpointTests.AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn`|
|Un contrôleur sans route, sous son nom moins `Controller`|`RDWebEndpointTests.AControllerWithNoRoute_IsRoutedUnderItsModuleName_LessItsControllerSuffix`|
|Les valeurs de route sont liées comme chaînes, sans égard à la casse|`RDWebEndpointsHostingTests.TheRouteValues_BindTheirParameters_AsStrings`|
|Une valeur de la chaîne de requête lie le paramètre de son nom|`RDWebEndpointsHostingTests.AQueryStringValue_BindsTheParameterOfItsName_AndAParameterNothingBinds_GetsNoArgument`|
|Le corps d'un `POST` lie le premier paramètre que la route ne lie pas|`RDWebEndpointTests.ThePostBody_BindsTheFirstParameterTheRouteDoesNotBind`, `RDWebEndpointsHostingTests.APostedJsonBody_BindsTheBodyParameter_AsTheJsonItHolds`|
|La valeur est le corps JSON; aucune valeur donne `204`|`RDWebEndpointsHostingTests.TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse`, `RDWebEndpointsHostingTests.AProcedureThatReturnsNoValue_AnswersNoContent`|
|Une erreur d'exécution donne `500` avec son numéro et son code|`RDWebEndpointsHostingTests.ARunTimeError_AnswersInternalServerError_WithTheErrorsNumberAndCode`|
|Une machine n'exécute qu'une requête à la fois|`RDWebMachinePoolTests.APoolOfOneMachine_RunsASecondRequestOnlyOnceTheFirstHasReturned`|

---
[ACCUEIL](index.fr.md) • [HOME](index.md) | APERÇU • [OVERVIEW](overview.md) | [DÉMARRER](getting-started.fr.md) • [GET STARTED](getting-started.md) | [RDCore](https://rubberduck-vba.github.io/RDCore/index.fr.html) | [SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html) | 🌐 [rubberduckvba.ca](https://rubberduckvba.ca)

---
