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
    public static string KeptMachines_InvalidLimit => Get(nameof(KeptMachines_InvalidLimit));
    public static string Endpoint_KeptMachineKeyIsNoRouteParameter => Get(nameof(Endpoint_KeptMachineKeyIsNoRouteParameter));
    public static string Endpoint_NewMachineIsNotKept => Get(nameof(Endpoint_NewMachineIsNotKept));
    public static string Endpoint_PredeclaredController => Get(nameof(Endpoint_PredeclaredController));
    public static string Hosting_NoKeptMachines => Get(nameof(Hosting_NoKeptMachines));
    public static string Problem_NoMachine => Get(nameof(Problem_NoMachine));
    public static string Problem_MachineAlreadyMade => Get(nameof(Problem_MachineAlreadyMade));
    public static string Problem_MachinesFull => Get(nameof(Problem_MachinesFull));
    public static string Pool_NoMachineInTime => Get(nameof(Pool_NoMachineInTime));
    public static string Pool_MachineDidNotStop => Get(nameof(Pool_MachineDidNotStop));
    public static string Endpoint_BodyIsNoParameter => Get(nameof(Endpoint_BodyIsNoParameter));
    public static string Endpoint_BodyIsRouteParameter => Get(nameof(Endpoint_BodyIsRouteParameter));
    public static string Request_BodyIsNotJson => Get(nameof(Request_BodyIsNotJson));
    public static string Request_BodyMustBeJson => Get(nameof(Request_BodyMustBeJson));
    public static string Problem_RuntimeError => Get(nameof(Problem_RuntimeError));
    public static string Problem_NotFound => Get(nameof(Problem_NotFound));
    public static string Problem_Refused => Get(nameof(Problem_Refused));
    public static string Problem_DidNotComplete => Get(nameof(Problem_DidNotComplete));
    public static string ImplicitBodyBinding_Message => Get(nameof(ImplicitBodyBinding_Message));
    public static string ImplicitBodyBinding_Fix => Get(nameof(ImplicitBodyBinding_Fix));
    public static string SeveralBodyParameters_Message => Get(nameof(SeveralBodyParameters_Message));
    public static string MemberNotRoundTripped_Message => Get(nameof(MemberNotRoundTripped_Message));
    public static string MemberNotRoundTripped_ReadOnly => Get(nameof(MemberNotRoundTripped_ReadOnly));
    public static string MemberNotRoundTripped_Dictionary => Get(nameof(MemberNotRoundTripped_Dictionary));
}
