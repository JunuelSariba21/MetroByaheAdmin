using System.Collections;
using System.Globalization;

using FirebaseAdmin.Auth;

using Google.Cloud.Firestore;

using Microsoft.AspNetCore.Mvc;

using MetroByaheAdmin.Api.Models;

namespace MetroByaheAdmin.Api.Controllers;

[ApiController]
[Route("api/routes")]
public sealed class RoutesController : AdminControllerBase
{
    private const string CollectionName = "route_terminals";

    public RoutesController(
        FirebaseAuth firebaseAuth,
        FirestoreDb firestore)
        : base(
            firebaseAuth,
            firestore)
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var authorization =
            await RequireAdminAsync();

        if (!authorization.IsAuthorized)
        {
            return authorization.FailureResult!;
        }

        try
        {
            var snapshot =
                await Firestore
                    .Collection(CollectionName)
                    .GetSnapshotAsync();

            var routes =
                snapshot.Documents
                    .Where(document => document.Exists)
                    .Select(ToRouteResponse)
                    .OrderBy(route => route.RouteName)
                    .ToList();

            return Ok(
                new
                {
                    success = true,
                    routes
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to load routes.",
                    error = ex.Message
                });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        RouteRequest? request)
    {
        var authorization =
            await RequireAdminAsync();

        if (!authorization.IsAuthorized)
        {
            return authorization.FailureResult!;
        }

        if (!TryCreateRoute(
                request,
                out var route,
                out var validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        var requestedId =
            request!.Id;

        var generatedId =
            string.IsNullOrWhiteSpace(requestedId)
                ? CreateDocumentId(route.RouteName)
                : requestedId;

        if (!TryValidateDocumentId(
                generatedId,
                out var documentId,
                out validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        try
        {
            var routeReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await routeReference
                    .GetSnapshotAsync();

            if (existing.Exists)
            {
                return Conflict(
                    new
                    {
                        success = false,
                        message = "A route with this document ID already exists.",
                        id = documentId
                    });
            }

            await routeReference
                .CreateAsync(
                    BuildRouteDocument(route));

            route.Id = documentId;

            return StatusCode(
                201,
                new
                {
                    success = true,
                    message = "Route created successfully.",
                    id = documentId,
                    route
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to create the route.",
                    error = ex.Message
                });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody]
        RouteRequest? request)
    {
        var authorization =
            await RequireAdminAsync();

        if (!authorization.IsAuthorized)
        {
            return authorization.FailureResult!;
        }

        if (!TryValidateDocumentId(
                id,
                out var documentId,
                out var validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        if (!TryCreateRoute(
                request,
                out var route,
                out validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        if (!string.IsNullOrWhiteSpace(request!.Id) &&
            (!TryValidateDocumentId(
                    request.Id,
                    out var requestDocumentId,
                    out validationMessage) ||
             !string.Equals(
                 requestDocumentId,
                 documentId,
                 StringComparison.Ordinal)))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = string.IsNullOrWhiteSpace(validationMessage)
                        ? "The route ID in the request body must match the URL."
                        : validationMessage
                });
        }

        try
        {
            var routeReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await routeReference
                    .GetSnapshotAsync();

            if (!existing.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message = "Route not found.",
                        id = documentId
                    });
            }

            await routeReference
                .SetAsync(
                    BuildRouteDocument(route));

            route.Id = documentId;

            return Ok(
                new
                {
                    success = true,
                    message = "Route updated successfully.",
                    id = documentId,
                    route
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to update the route.",
                    error = ex.Message
                });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id)
    {
        var authorization =
            await RequireAdminAsync();

        if (!authorization.IsAuthorized)
        {
            return authorization.FailureResult!;
        }

        if (!TryValidateDocumentId(
                id,
                out var documentId,
                out var validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        try
        {
            var routeReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await routeReference
                    .GetSnapshotAsync();

            if (!existing.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message = "Route not found.",
                        id = documentId
                    });
            }

            await routeReference
                .DeleteAsync();

            return Ok(
                new
                {
                    success = true,
                    message = "Route deleted successfully.",
                    id = documentId
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to delete the route.",
                    error = ex.Message
                });
        }
    }

    private static bool TryCreateRoute(
        RouteRequest? request,
        out RouteResponse route,
        out string validationMessage)
    {
        route = new RouteResponse();
        validationMessage = "";

        if (request == null)
        {
            validationMessage = "A route payload is required.";
            return false;
        }

        var routeName =
            request.RouteName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(routeName))
        {
            validationMessage = "A route name is required.";
            return false;
        }

        if (routeName.Length > 160 ||
            ContainsControlCharacter(routeName))
        {
            validationMessage = "The route name is invalid.";
            return false;
        }

        if (request.Stops == null ||
            request.Stops.Count == 0)
        {
            validationMessage = "At least one route stop is required.";
            return false;
        }

        if (request.Stops.Count > 250)
        {
            validationMessage = "A route can contain at most 250 stops.";
            return false;
        }

        route.RouteName = routeName;

        for (var index = 0;
             index < request.Stops.Count;
             index++)
        {
            var stop =
                request.Stops[index];

            if (stop == null)
            {
                validationMessage =
                    $"Stop {index + 1} is required.";
                return false;
            }

            var stopName =
                stop.Name?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(stopName) ||
                stopName.Length > 160 ||
                ContainsControlCharacter(stopName))
            {
                validationMessage =
                    $"Stop {index + 1} must have a valid name.";
                return false;
            }

            var landmark =
                stop.Landmark?.Trim() ?? "";

            if (landmark.Length > 160 ||
                ContainsControlCharacter(landmark))
            {
                validationMessage =
                    $"Stop {index + 1} has an invalid landmark.";

                return false;
            }

            if (!stop.Lat.HasValue ||
                !stop.Lng.HasValue ||
                !IsValidCoordinate(
                    stop.Lat.Value,
                    stop.Lng.Value))
            {
                validationMessage =
                    $"Stop {index + 1} must have a valid map location.";
                return false;
            }

            route.Stops.Add(
                new RouteStopResponse
                {
                    Name = stopName,
                    Landmark = landmark,
                    Lat = stop.Lat.Value,
                    Lng = stop.Lng.Value
                });
        }

        return true;
    }

    private static Dictionary<string, object> BuildRouteDocument(
        RouteResponse route)
    {
        var stops =
            new List<object>();

        foreach (var stop in route.Stops)
        {
            // Coordinates are intentionally stored as strings to match the
            // existing Firestore REST writer used by the desktop application.
            var stopFields =
                new Dictionary<string, object>
                {
                    ["name"] = stop.Name,
                    ["lat"] = stop.Lat.ToString(CultureInfo.InvariantCulture),
                    ["lng"] = stop.Lng.ToString(CultureInfo.InvariantCulture)
                };

            if (!string.IsNullOrWhiteSpace(stop.Landmark))
            {
                stopFields["landmark"] = stop.Landmark;
            }

            stops.Add(stopFields);
        }

        return new Dictionary<string, object>
        {
            ["routeName"] = route.RouteName,
            ["stops"] = stops
        };
    }

    private static RouteResponse ToRouteResponse(
        DocumentSnapshot document)
    {
        var fields =
            document.ToDictionary();

        var route =
            new RouteResponse
            {
                Id = document.Id,
                RouteName = GetTextField(fields, "routeName")
            };

        var stopsValue =
            GetFieldValue(fields, "stops");

        if (stopsValue is not IEnumerable stops ||
            stopsValue is string)
        {
            return route;
        }

        foreach (var stopValue in stops)
        {
            var stopFields =
                ToDictionary(stopValue);

            if (stopFields == null)
            {
                continue;
            }

            route.Stops.Add(
                new RouteStopResponse
                {
                    Name = GetTextField(stopFields, "name"),
                    Landmark = GetOptionalLandmark(stopFields),
                    Lat = GetDoubleField(stopFields, "lat"),
                    Lng = GetDoubleField(stopFields, "lng")
                });
        }

        return route;
    }

    private static object? GetFieldValue(
        IDictionary<string, object> fields,
        string fieldName)
    {
        foreach (var pair in fields)
        {
            if (string.Equals(
                    pair.Key,
                    fieldName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string GetTextField(
        IDictionary<string, object> fields,
        string fieldName)
    {
        return GetFieldValue(fields, fieldName)?.ToString() ?? "";
    }

    private static string GetOptionalLandmark(
        IDictionary<string, object> fields)
    {
        var landmark =
            GetTextField(fields, "landmark").Trim();

        return landmark.Length <= 160 &&
               !ContainsControlCharacter(landmark)
            ? landmark
            : "";
    }

    private static double GetDoubleField(
        IDictionary<string, object> fields,
        string fieldName)
    {
        var value =
            GetFieldValue(fields, fieldName);

        if (value is double number)
        {
            return number;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is decimal decimalValue)
        {
            return (double)decimalValue;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(
                    CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                // Fall through to string parsing below.
            }
            catch (InvalidCastException)
            {
                // Fall through to string parsing below.
            }
        }

        return double.TryParse(
            value?.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    private static IDictionary<string, object>? ToDictionary(
        object? value)
    {
        if (value is IDictionary<string, object> dictionary)
        {
            return dictionary;
        }

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
        {
            return new Dictionary<string, object>(
                readOnlyDictionary,
                StringComparer.Ordinal);
        }

        return null;
    }

    private static bool IsValidCoordinate(
        double latitude,
        double longitude)
    {
        return !double.IsNaN(latitude) &&
               !double.IsInfinity(latitude) &&
               !double.IsNaN(longitude) &&
               !double.IsInfinity(longitude) &&
               latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180 &&
               (latitude != 0 || longitude != 0);
    }

    private static bool ContainsControlCharacter(
        string value)
    {
        return value.Any(char.IsControl);
    }

    private static string CreateDocumentId(
        string routeName)
    {
        return routeName
            .Trim()
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\\", "_");
    }
}
