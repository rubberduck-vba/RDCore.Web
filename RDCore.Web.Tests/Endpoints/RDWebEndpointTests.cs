using RDCore.Web.Endpoints;

namespace RDCore.Web.Tests.Endpoints;

/// <summary>
/// The endpoint a member annotation of a controller module declares (<see cref="RDWebEndpoint.FromAnnotation"/>): its method, its route from the root, and
/// the parameters its route and its body bind.
/// </summary>
[TestClass]
public sealed class RDWebEndpointTests
{
    private static RDWebEndpoint Declare(string annotation, string? route, string? controllerRoute = "api/orders", params string[] parameters)
        => RDWebEndpoint.FromAnnotation("OrdersController", controllerRoute, "GetOrder", parameters, annotation, route)
            ?? throw new AssertFailedException($"'@{annotation} declares no endpoint");

    [TestMethod]
    public void AnHttpGetAnnotation_ServesTheProcedureForGet_OnTheControllersRouteAndItsOwn()
    {
        var endpoint = Declare(RDWebAnnotations.HttpGet, "{id}", parameters: "id");

        Assert.AreEqual("GET", endpoint.HttpMethod);
        Assert.AreEqual("/api/orders/{id}", endpoint.Route);
        Assert.AreEqual("OrdersController", endpoint.ModuleName);
        Assert.AreEqual("GetOrder", endpoint.ProcedureName);
    }

    [TestMethod]
    public void AnHttpPostAnnotation_ServesTheProcedureForPost()
        => Assert.AreEqual("POST", Declare(RDWebAnnotations.HttpPost, null).HttpMethod);

    [TestMethod]
    public void AnAnnotationName_IsComparedWithoutRegardToCase()
        => Assert.AreEqual("GET", Declare("httpget", null).HttpMethod);

    [TestMethod]
    public void AnAnnotationOfNoHttpMethod_DeclaresNoEndpoint()
        => Assert.IsNull(RDWebEndpoint.FromAnnotation("OrdersController", "api/orders", "GetOrder", [], "Description", "x"));

    [TestMethod]
    public void AControllerWithNoRoute_IsRoutedUnderItsModuleName_LessItsControllerSuffix()
        => Assert.AreEqual("/Orders/all", Declare(RDWebAnnotations.HttpGet, "all", controllerRoute: null).Route);

    [TestMethod]
    [DataRow("ThingsController", "Things")]
    [DataRow("thingsCONTROLLER", "things")]
    [DataRow("Things", "Things")]
    [DataRow("Controller", "Controller")]
    [DataRow("ControllerOfThings", "ControllerOfThings")]
    public void TheConventionalRoute_IsTheModuleName_LessATrailingControllerSuffix_WhenAnythingIsLeft(string moduleName, string expected)
        => Assert.AreEqual(expected, RDWebEndpoint.ConventionalRouteOf(moduleName));

    [TestMethod]
    public void AMemberAnnotationWithNoRoute_ServesTheControllersRoute()
        => Assert.AreEqual("/api/orders", Declare(RDWebAnnotations.HttpGet, null).Route);

    [TestMethod]
    [DataRow("/health")]
    [DataRow("~/health")]
    public void ARouteFromTheRoot_IsNotPrefixedByTheControllersRoute(string route)
        => Assert.AreEqual("/health", Declare(RDWebAnnotations.HttpGet, route).Route);

    [TestMethod]
    public void TheSlashesAroundTheRoutes_JoinThemOnce()
        => Assert.AreEqual("/api/orders/{id}", Declare(RDWebAnnotations.HttpGet, "{id}/", "/api/orders/", "id").Route);

    [TestMethod]
    public void ARouteParameter_BindsTheProcedureParameterOfItsName_WithoutRegardToCase()
    {
        var endpoint = Declare(RDWebAnnotations.HttpGet, "{ID}/lines/{line}", parameters: ["Id", "Line", "Format"]);

        CollectionAssert.AreEqual(new[] { "Id", "Line" }, endpoint.RouteParameters);
        CollectionAssert.AreEqual(new[] { "Id", "Line", "Format" }, endpoint.Parameters);
    }

    [TestMethod]
    public void ARouteParameterThatIsNoParameterOfTheProcedure_IsAnError()
    {
        var error = Assert.ThrowsExactly<RDWebEndpointException>(() => Declare(RDWebAnnotations.HttpGet, "{orderId}", parameters: "id"));

        StringAssert.Contains(error.Message, "orderId");
    }

    [TestMethod]
    public void ARouteThatIsNoRouteTemplate_IsAnError()
        => Assert.ThrowsExactly<RDWebEndpointException>(() => Declare(RDWebAnnotations.HttpGet, "{id", parameters: "id"));

    [TestMethod]
    public void ThePostBody_BindsTheFirstParameterTheRouteDoesNotBind()
        => Assert.AreEqual("order", Declare(RDWebAnnotations.HttpPost, "{id}", parameters: ["id", "order", "notify"]).BodyParameter);

    [TestMethod]
    public void APostWhoseRouteBindsEveryParameter_HasNoBodyParameter()
        => Assert.IsNull(Declare(RDWebAnnotations.HttpPost, "{id}", parameters: "id").BodyParameter);

    [TestMethod]
    public void AGet_HasNoBodyParameter()
        => Assert.IsNull(Declare(RDWebAnnotations.HttpGet, "{id}", parameters: ["id", "order"]).BodyParameter);
}
