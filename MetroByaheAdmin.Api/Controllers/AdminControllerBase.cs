using System.Text;

using FirebaseAdmin.Auth;

using Google.Cloud.Firestore;

using Microsoft.AspNetCore.Mvc;

namespace MetroByaheAdmin.Api.Controllers;

/// <summary>
/// Shared authorization guard for API actions that mutate or reveal
/// administrative data. A valid Firebase token alone is not sufficient:
/// the matching admin/{uid} document must exist as well.
/// </summary>
public abstract class AdminControllerBase : ControllerBase
{
    protected readonly FirebaseAuth FirebaseAuth;
    protected readonly FirestoreDb Firestore;

    protected AdminControllerBase(
        FirebaseAuth firebaseAuth,
        FirestoreDb firestore)
    {
        FirebaseAuth = firebaseAuth;
        Firestore = firestore;
    }

    protected async Task<AdminAuthorizationResult> RequireAdminAsync()
    {
        var authorizationHeader =
            Request.Headers["Authorization"]
                .ToString();

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return AdminAuthorizationResult.Denied(
                Unauthorized(
                    new
                    {
                        success = false,
                        message = "A Firebase bearer token is required."
                    }));
        }

        var idToken =
            authorizationHeader["Bearer ".Length..]
                .Trim();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return AdminAuthorizationResult.Denied(
                Unauthorized(
                    new
                    {
                        success = false,
                        message = "A Firebase bearer token is required."
                    }));
        }

        FirebaseToken token;

        try
        {
            // Checking token revocation prevents a signed but revoked session
            // from retaining access to protected admin operations.
            token =
                await FirebaseAuth
                    .VerifyIdTokenAsync(
                        idToken,
                        checkRevoked: true);
        }
        catch (FirebaseAuthException)
        {
            return AdminAuthorizationResult.Denied(
                Unauthorized(
                    new
                    {
                        success = false,
                        message = "The Firebase bearer token is invalid, expired, or revoked."
                    }));
        }
        catch (Exception ex)
        {
            return AdminAuthorizationResult.Denied(
                StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "Unable to verify the Firebase bearer token.",
                        error = ex.Message
                    }));
        }

        if (string.IsNullOrWhiteSpace(token.Uid))
        {
            return AdminAuthorizationResult.Denied(
                Unauthorized(
                    new
                    {
                        success = false,
                        message = "The Firebase bearer token does not contain a user ID."
                    }));
        }

        try
        {
            var adminDocument =
                await Firestore
                    .Collection("admin")
                    .Document(token.Uid)
                    .GetSnapshotAsync();

            if (!adminDocument.Exists)
            {
                return AdminAuthorizationResult.Denied(
                    StatusCode(
                        403,
                        new
                        {
                            success = false,
                            message = "An administrator account is required for this action."
                        }));
            }
        }
        catch (Exception ex)
        {
            return AdminAuthorizationResult.Denied(
                StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "Unable to verify the administrator account.",
                        error = ex.Message
                    }));
        }

        return AdminAuthorizationResult.Allowed(token.Uid);
    }

    protected static bool TryValidateDocumentId(
        string? value,
        out string documentId,
        out string validationMessage)
    {
        documentId = value?.Trim() ?? "";
        validationMessage = "";

        if (string.IsNullOrWhiteSpace(documentId))
        {
            validationMessage = "A document ID is required.";
            return false;
        }

        if (documentId is "." or "..")
        {
            validationMessage = "The document ID cannot be '.' or '..'.";
            return false;
        }

        if (documentId.Contains('/') ||
            documentId.Contains('\\') ||
            documentId.Any(char.IsControl))
        {
            validationMessage =
                "The document ID cannot contain slashes or control characters.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(documentId) > 1_500)
        {
            validationMessage = "The document ID is too long.";
            return false;
        }

        return true;
    }
}

public sealed class AdminAuthorizationResult
{
    public string? Uid { get; private init; }

    public IActionResult? FailureResult { get; private init; }

    public bool IsAuthorized =>
        FailureResult == null &&
        !string.IsNullOrWhiteSpace(Uid);

    public static AdminAuthorizationResult Allowed(
        string uid)
    {
        return new AdminAuthorizationResult
        {
            Uid = uid
        };
    }

    public static AdminAuthorizationResult Denied(
        IActionResult failureResult)
    {
        return new AdminAuthorizationResult
        {
            FailureResult = failureResult
        };
    }
}
