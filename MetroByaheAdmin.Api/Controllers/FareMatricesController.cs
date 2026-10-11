using System.Globalization;

using FirebaseAdmin.Auth;

using Google.Cloud.Firestore;

using Microsoft.AspNetCore.Mvc;

using MetroByaheAdmin.Api.Models;

namespace MetroByaheAdmin.Api.Controllers;

[ApiController]
[Route("api/fare-matrices")]
public sealed class FareMatricesController : AdminControllerBase
{
    private const string CollectionName = "fare_matrix";
    private const decimal MaximumFare = 1_000_000m;

    public FareMatricesController(
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

            var fareMatrices =
                snapshot.Documents
                    .Where(document => document.Exists)
                    .Select(ToFareMatrixResponse)
                    .OrderBy(matrix => matrix.Id)
                    .ToList();

            return Ok(
                new
                {
                    success = true,
                    fareMatrices
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to load fare matrices.",
                    error = ex.Message
                });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        FareMatrixRequest? request)
    {
        var authorization =
            await RequireAdminAsync();

        if (!authorization.IsAuthorized)
        {
            return authorization.FailureResult!;
        }

        if (request == null)
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = "A fare matrix payload is required."
                });
        }

        if (!TryValidateDocumentId(
                request.Id,
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

        if (!TryCreateFareMatrix(
                request,
                out var fareMatrix,
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
            var matrixReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await matrixReference
                    .GetSnapshotAsync();

            if (existing.Exists)
            {
                return Conflict(
                    new
                    {
                        success = false,
                        message = "A fare matrix with this document ID already exists.",
                        id = documentId
                    });
            }

            await matrixReference
                .CreateAsync(
                    BuildFareMatrixDocument(fareMatrix));

            fareMatrix.Id = documentId;

            return StatusCode(
                201,
                new
                {
                    success = true,
                    message = "Fare matrix created successfully.",
                    id = documentId,
                    fareMatrix
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to create the fare matrix.",
                    error = ex.Message
                });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody]
        FareMatrixRequest? request)
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

        if (request == null)
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = "A fare matrix payload is required."
                });
        }

        if (!TryCreateFareMatrix(
                request,
                out var fareMatrix,
                out validationMessage))
        {
            return BadRequest(
                new
                {
                    success = false,
                    message = validationMessage
                });
        }

        var targetDocumentId =
            documentId;

        if (!string.IsNullOrWhiteSpace(request.Id) &&
            !TryValidateDocumentId(
                request.Id,
                out targetDocumentId,
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
            var matrixReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await matrixReference
                    .GetSnapshotAsync();

            if (!existing.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message = "Fare matrix not found.",
                        id = documentId
                    });
            }

            var documentData =
                BuildFareMatrixDocument(fareMatrix);

            if (!string.Equals(
                    targetDocumentId,
                    documentId,
                    StringComparison.Ordinal))
            {
                var targetReference =
                    Firestore
                        .Collection(CollectionName)
                        .Document(targetDocumentId);

                var targetExists =
                    await targetReference
                        .GetSnapshotAsync();

                if (targetExists.Exists)
                {
                    return Conflict(
                        new
                        {
                            success = false,
                            message = "A fare matrix with the new document ID already exists.",
                            id = targetDocumentId
                        });
                }

                // Create has a MustNotExist precondition, so this batch cannot
                // overwrite a concurrently-created target matrix. Both writes
                // commit together, keeping a rename atomic.
                var batch =
                    Firestore.StartBatch();

                batch.Create(
                    targetReference,
                    documentData);

                batch.Delete(
                    matrixReference,
                    Precondition.MustExist);

                await batch.CommitAsync();

                fareMatrix.Id = targetDocumentId;

                return Ok(
                    new
                    {
                        success = true,
                        message = "Fare matrix renamed and updated successfully.",
                        id = targetDocumentId,
                        fareMatrix
                    });
            }

            await matrixReference
                .SetAsync(documentData);

            fareMatrix.Id = documentId;

            return Ok(
                new
                {
                    success = true,
                    message = "Fare matrix updated successfully.",
                    id = documentId,
                    fareMatrix
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Failed to update the fare matrix.",
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
            var matrixReference =
                Firestore
                    .Collection(CollectionName)
                    .Document(documentId);

            var existing =
                await matrixReference
                    .GetSnapshotAsync();

            if (!existing.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message = "Fare matrix not found.",
                        id = documentId
                    });
            }

            await matrixReference
                .DeleteAsync();

            return Ok(
                new
                {
                    success = true,
                    message = "Fare matrix deleted successfully.",
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
                    message = "Failed to delete the fare matrix.",
                    error = ex.Message
                });
        }
    }

    private static bool TryCreateFareMatrix(
        FareMatrixRequest request,
        out FareMatrixResponse fareMatrix,
        out string validationMessage)
    {
        fareMatrix = new FareMatrixResponse();
        validationMessage = "";

        var correctedAccommodation =
            request.Accommodation?.Trim() ?? "";

        var legacyAccommodation =
            request.Accomodation?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(correctedAccommodation) &&
            !string.IsNullOrWhiteSpace(legacyAccommodation) &&
            !string.Equals(
                correctedAccommodation,
                legacyAccommodation,
                StringComparison.OrdinalIgnoreCase))
        {
            validationMessage =
                "The accommodation and accomodation values must match when both are provided.";
            return false;
        }

        var accommodation =
            !string.IsNullOrWhiteSpace(correctedAccommodation)
                ? correctedAccommodation
                : legacyAccommodation;

        if (string.IsNullOrWhiteSpace(accommodation) ||
            accommodation.Length > 80 ||
            ContainsControlCharacter(accommodation))
        {
            validationMessage = "A valid accommodation value is required.";
            return false;
        }

        var requestedFares =
            request.Fares ?? new List<FareEntryRequest>();

        if (requestedFares.Count > 1_000)
        {
            validationMessage = "A fare matrix can contain at most 1,000 fare entries.";
            return false;
        }

        fareMatrix.Accommodation = accommodation;

        var pairs =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 0;
             index < requestedFares.Count;
             index++)
        {
            var fare =
                requestedFares[index];

            if (fare == null)
            {
                validationMessage =
                    $"Fare entry {index + 1} is required.";
                return false;
            }

            var origin =
                fare.Origin?.Trim() ?? "";

            var destination =
                fare.Destination?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(origin) ||
                origin.Length > 160 ||
                ContainsControlCharacter(origin))
            {
                validationMessage =
                    $"Fare entry {index + 1} must have a valid origin.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(destination) ||
                destination.Length > 160 ||
                ContainsControlCharacter(destination))
            {
                validationMessage =
                    $"Fare entry {index + 1} must have a valid destination.";
                return false;
            }

            if (!fare.Full.HasValue ||
                !fare.Pagibig.HasValue ||
                !fare.Sp.HasValue ||
                !IsValidFare(fare.Full.Value) ||
                !IsValidFare(fare.Pagibig.Value) ||
                !IsValidFare(fare.Sp.Value))
            {
                validationMessage =
                    $"Fare entry {index + 1} must contain non-negative fare amounts no greater than {MaximumFare:N0}.";
                return false;
            }

            var pairKey =
                $"{origin}\u001f{destination}";

            if (!pairs.Add(pairKey))
            {
                validationMessage =
                    $"Fare entry {index + 1} duplicates an existing origin and destination pair.";
                return false;
            }

            fareMatrix.Fares.Add(
                new FareEntryResponse
                {
                    Origin = origin,
                    Destination = destination,
                    Full = fare.Full.Value,
                    Pagibig = fare.Pagibig.Value,
                    Sp = fare.Sp.Value
                });
        }

        return true;
    }

    private static Dictionary<string, object> BuildFareMatrixDocument(
        FareMatrixResponse fareMatrix)
    {
        var fareMap =
            new Dictionary<string, object>(
                StringComparer.Ordinal);

        foreach (var fare in fareMatrix.Fares)
        {
            if (!fareMap.TryGetValue(
                    fare.Origin,
                    out var destinationValue) ||
                destinationValue is not Dictionary<string, object> destinations)
            {
                destinations =
                    new Dictionary<string, object>(
                        StringComparer.Ordinal);

                fareMap[fare.Origin] =
                    destinations;
            }

            destinations[fare.Destination] =
                new Dictionary<string, object>
                {
                    ["full"] = ToFirestoreNumber(fare.Full),
                    ["pagibig"] = ToFirestoreNumber(fare.Pagibig),
                    ["sp"] = ToFirestoreNumber(fare.Sp)
                };
        }

        // The legacy spelling is what the desktop app currently writes and
        // reads first. GET also supports correctly-spelled existing documents.
        return new Dictionary<string, object>
        {
            ["accomodation"] = fareMatrix.Accommodation,
            ["fares"] = fareMap
        };
    }

    private static FareMatrixResponse ToFareMatrixResponse(
        DocumentSnapshot document)
    {
        var fields =
            document.ToDictionary();

        var fareMatrix =
            new FareMatrixResponse
            {
                Id = document.Id,
                Accommodation = GetTextField(fields, "accomodation")
            };

        if (string.IsNullOrWhiteSpace(fareMatrix.Accommodation))
        {
            fareMatrix.Accommodation =
                GetTextField(fields, "accommodation");
        }

        var fares =
            ToDictionary(
                GetFieldValue(fields, "fares"));

        if (fares == null)
        {
            return fareMatrix;
        }

        foreach (var origin in fares.OrderBy(pair => pair.Key))
        {
            var destinations =
                ToDictionary(origin.Value);

            if (destinations == null)
            {
                continue;
            }

            foreach (var destination in destinations.OrderBy(pair => pair.Key))
            {
                var amounts =
                    ToDictionary(destination.Value);

                if (amounts == null)
                {
                    continue;
                }

                fareMatrix.Fares.Add(
                    new FareEntryResponse
                    {
                        Origin = origin.Key,
                        Destination = destination.Key,
                        Full = GetDecimalField(amounts, "full"),
                        Pagibig = GetDecimalField(amounts, "pagibig"),
                        Sp = GetDecimalField(amounts, "sp")
                    });
            }
        }

        return fareMatrix;
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

    private static decimal GetDecimalField(
        IDictionary<string, object> fields,
        string fieldName)
    {
        var value =
            GetFieldValue(fields, fieldName);

        if (value is decimal decimalValue)
        {
            return decimalValue;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDecimal(
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
            catch (OverflowException)
            {
                return 0;
            }
        }

        return decimal.TryParse(
            value?.ToString(),
            NumberStyles.Number,
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

    private static object ToFirestoreNumber(
        decimal value)
    {
        if (decimal.Truncate(value) == value &&
            value >= long.MinValue &&
            value <= long.MaxValue)
        {
            return decimal.ToInt64(value);
        }

        return (double)value;
    }

    private static bool IsValidFare(
        decimal value)
    {
        return value >= 0 &&
               value <= MaximumFare;
    }

    private static bool ContainsControlCharacter(
        string value)
    {
        return value.Any(char.IsControl);
    }
}
