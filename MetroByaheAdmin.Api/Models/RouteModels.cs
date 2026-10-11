namespace MetroByaheAdmin.Api.Models;

public sealed class RouteRequest
{
    public string? Id { get; set; }

    public string? RouteName { get; set; }

    public List<RouteStopRequest>? Stops { get; set; }
}

public sealed class RouteStopRequest
{
    public string? Name { get; set; }

    public string? Landmark { get; set; }

    // The existing Firestore schema uses lat/lng rather than
    // latitude/longitude, so the API keeps that contract.
    public double? Lat { get; set; }

    public double? Lng { get; set; }
}

public sealed class RouteResponse
{
    public string Id { get; set; } = "";

    public string RouteName { get; set; } = "";

    public List<RouteStopResponse> Stops { get; set; } = new();
}

public sealed class RouteStopResponse
{
    public string Name { get; set; } = "";

    public string Landmark { get; set; } = "";

    public double Lat { get; set; }

    public double Lng { get; set; }
}
