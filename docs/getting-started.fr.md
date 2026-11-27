# Pour démarrer
<sup>_This document is available in [English](getting-started.md)_</sup>

Cette page construit et lance l'hôte de RDCore.Web, écrit un module contrôleur et l'appelle. Elle suppose une ligne de commande, [Git](https://git-scm.com/)
et le [SDK .NET 11](https://dotnet.microsoft.com/download/dotnet/11.0). Elle ne suppose aucune connaissance d'ASP.NET Core : l'[aperçu](overview.fr.md)
explique comment RDCore.Web s'en sert.

> [!NOTE]
> **Non implémenté.** Aucune procédure RD-VBA ne s'exécute de bout en bout. L'hôte sert sa page, mais il n'associe aucun point de terminaison, et aucune
> machine n'exécute de procédure : l'appel à la fin de cette page répond donc `404 Not Found`. [Où ça s'arrête](#où-ça-sarrête) dit ce qui manque.

---
## 1. Construire et tester

```powershell
git clone https://github.com/rubberduck-vba/RDCore.Web.git
cd RDCore.Web
dotnet build RDCore.Web.slnx
dotnet test RDCore.Web.slnx
```

|Projet|Ce que c'est|
|---|---|
|`RDCore.Web`|La bibliothèque du cadriciel.|
|`RDCore.Web.Host`|L'application ASP.NET Core qui sert les pages d'une application.|
|`RDCore.Web.Tests`|Les tests.|

---
## 2. Lancer l'hôte

```powershell
dotnet run --project RDCore.Web.Host
```

L'hôte écrit l'adresse où il écoute, `http://localhost:5000` par défaut;
[Kestrel](https://learn.microsoft.com/fr-fr/aspnet/core/fundamentals/servers/kestrel) explique comment la changer. Ouvrez cette adresse dans un navigateur :
l'hôte sert `index.html` depuis son dossier `wwwroot`, une page avec une case **Name** et un bouton **Call**. Ctrl+C arrête l'hôte.

`RDWebHostApplicationTests.TheRoot_ServesThePage_WhichLoadsItsScript` fixe la page.

---
## 3. Écrire un module contrôleur

La page appelle ce point de terminaison, `GET /api/hello/{name}` :

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

|Ligne|Ce qu'elle déclare|
|---|---|
|`'@Controller("api")`|Le module de classe est un contrôleur. Sa route, `api`, préfixe les routes de ses points de terminaison. Elle va dans la section des déclarations du module.|
|`'@HttpGet("hello/{name}")`|`Hello` sert les requêtes `GET` sur `api/hello/{name}`. Elle va sur la ligne au-dessus de la procédure.|
|`{name}`|Un paramètre de route : il lie le paramètre `name` de la procédure, comme chaîne.|

Un paramètre de route qui ne nomme aucun paramètre de la procédure est une erreur. La [référence de RDCore.Web](specs/rdweb.md), en anglais, donne les
règles de chaque annotation.

`RDWebEndpointTests.AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn` et
`RDWebEndpointsHostingTests.TheRouteValues_BindTheirParameters_AsStrings` fixent le point de terminaison et sa liaison.

---
## 4. L'appeler

Tapez un nom dans la page et choisissez **Call**, ou envoyez la requête depuis une ligne de commande :

```powershell
curl.exe -i http://localhost:5000/api/hello/World
```

Les deux répondent `404 Not Found` : l'hôte n'associe aucun point de terminaison.

Les arguments et la valeur d'une procédure passent en JSON. Quand une machine répond avec la valeur de la procédure, RDCore.Web l'écrit comme corps de la
réponse, avec `200 OK`. `RDWebEndpointsHostingTests.TheValueAProcedureReturns_IsTheJsonBodyOfTheResponse` fixe cette réponse, avec une machine dont le test donne la réponse.

---
## Où ça s'arrête

|Partie manquante|Ce que ça veut dire|
|---|---|
|Une machine qui exécute une procédure|Aucune machine n'exécute une procédure dans une session d'exécution RDCore, alors aucun code RD-VBA ne répond à une requête.|
|Des points de terminaison dans l'hôte|L'hôte n'en associe aucun : il ne sert que sa page.|

---
## Pour la suite

- [Aperçu](overview.fr.md) : comment s'agencent les parties, et leur lien avec ASP.NET Core.
- [Référence de RDCore.Web](specs/rdweb.md) : la syntaxe et les règles de chaque partie (en anglais).
- [Référence de l'API](xref:RDCore.Web.Endpoints) : les types .NET (en anglais).

---
[ACCUEIL](index.fr.md) • [HOME](index.md) | [APERÇU](overview.fr.md) • [OVERVIEW](overview.md) | DÉMARRER • [GET STARTED](getting-started.md) | [RDCore](https://rubberduck-vba.github.io/RDCore/index.fr.html) | [SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html) | 🌐 [rubberduckvba.ca](https://rubberduckvba.ca)

---
