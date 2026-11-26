using System.Globalization;
using System.Resources;

namespace RDCore.Web;

// the strings of RDWebResources.resx, in the language of the user interface (RDWebResources.fr.resx for French).
internal static class RDWebResources
{
    private static readonly ResourceManager Strings = new("RDCore.Web.RDWebResources", typeof(RDWebResources).Assembly);

    private static string Get(string name) => Strings.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string Endpoint_InvalidRoute => Get(nameof(Endpoint_InvalidRoute));
    public static string Endpoint_RouteParameterIsNoParameter => Get(nameof(Endpoint_RouteParameterIsNoParameter));
    public static string Pool_InvalidSize => Get(nameof(Pool_InvalidSize));
    public static string Pool_InvalidLimit => Get(nameof(Pool_InvalidLimit));
    public static string Pool_NoMachineInTime => Get(nameof(Pool_NoMachineInTime));
    public static string Pool_MachineDidNotStop => Get(nameof(Pool_MachineDidNotStop));
    public static string Request_BodyIsNotJson => Get(nameof(Request_BodyIsNotJson));
    public static string Request_BodyMustBeJson => Get(nameof(Request_BodyMustBeJson));
    public static string Problem_RuntimeError => Get(nameof(Problem_RuntimeError));
    public static string Problem_NotFound => Get(nameof(Problem_NotFound));
    public static string Problem_Refused => Get(nameof(Problem_Refused));
    public static string Problem_DidNotComplete => Get(nameof(Problem_DidNotComplete));
}
