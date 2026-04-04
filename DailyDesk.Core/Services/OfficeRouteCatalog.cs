namespace DailyDesk.Services;

public static class OfficeRouteCatalog
{
    public const string ChiefRoute = "chief";
    public const string EngineeringRoute = "engineering";
    public const string SuiteRoute = "suite";
    public const string BusinessRoute = "business";

    public static readonly IReadOnlyList<string> KnownRoutes =
    [
        ChiefRoute,
        EngineeringRoute,
        SuiteRoute,
        BusinessRoute,
    ];

    public static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return ChiefRoute;
        }

        var normalized = route.Trim().ToLowerInvariant();
        return KnownRoutes.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : ChiefRoute;
    }

    public static string ResolveRouteTitle(string route) =>
        NormalizeRoute(route) switch
        {
            ChiefRoute => "Chief of Staff",
            EngineeringRoute => "Engineering Desk",
            SuiteRoute => "Suite Context",
            BusinessRoute => "Business Ops",
            _ => "Desk",
        };

    public static string ResolveRouteDisplayTitle(string route) =>
        NormalizeRoute(route) switch
        {
            BusinessRoute => "Growth Ops",
            _ => ResolveRouteTitle(route),
        };

    public static string ResolvePerspective(string route) =>
        NormalizeRoute(route) switch
        {
            ChiefRoute => "Chief of Staff",
            SuiteRoute => "Chief of Staff",
            BusinessRoute => "Business Strategist",
            _ => "EE Mentor",
        };
}
