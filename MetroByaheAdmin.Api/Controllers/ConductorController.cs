using FirebaseAdmin.Auth;

using Google.Cloud.Firestore;

using Microsoft.AspNetCore.Mvc;

using MetroByaheAdmin.Api.Models;

namespace MetroByaheAdmin.Api.Controllers;

[ApiController]
[Route("api/conductors")]
public class ConductorController : AdminControllerBase
{
    private readonly FirebaseAuth _firebaseAuth;
    private readonly FirestoreDb _firestore;

    public ConductorController(
        FirebaseAuth firebaseAuth,
        FirestoreDb firestore) : base(firebaseAuth, firestore)
    {
        _firebaseAuth = firebaseAuth;
        _firestore = firestore;
    }

    // =========================================================
    // FIRESTORE FIELD READER
    // =========================================================
    //
    // Supports both:
    //
    // name     / Name
    // username / Username
    // phone    / Phone
    // role     / Role
    // status   / Status
    //
    // This allows older conductor documents to continue working.
    // =========================================================

    private static string GetField(
        Dictionary<string, object> data,
        string fieldName,
        string defaultValue = "")
    {
        foreach (var pair in data)
        {
            if (string.Equals(
                    pair.Key,
                    fieldName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value?.ToString()
                       ?? defaultValue;
            }
        }

        return defaultValue;
    }

    // =========================================================
    // GET ALL CONDUCTORS
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        try
        {
            var collection =
                _firestore.Collection("conductors");

            var snapshot =
                await collection.GetSnapshotAsync();

            var conductors =
                new List<object>();

            foreach (var document in snapshot.Documents)
            {
                if (!document.Exists)
                {
                    continue;
                }

                var data =
                    document.ToDictionary();

                var name =
                    GetField(
                        data,
                        "name");

                var username =
                    GetField(
                        data,
                        "username");

                var phone =
                    GetField(
                        data,
                        "phone");

                var role =
                    GetField(
                        data,
                        "role",
                        "Conductor");

                var status =
                    GetField(
                        data,
                        "status",
                        "Available");

                conductors.Add(
                    new
                    {
                        id = document.Id,

                        name,

                        username,

                        phone,

                        role,

                        status
                    });
            }

            return Ok(
                new
                {
                    success = true,

                    conductors
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        "Failed to load conductors.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // DEBUG CONDUCTOR
    // =========================================================

    [HttpGet("debug/{uid}")]
    public async Task<IActionResult> DebugConductor(
        string uid)
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        try
        {
            var document =
                await _firestore
                    .Collection("conductors")
                    .Document(uid)
                    .GetSnapshotAsync();

            if (!document.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,

                        message =
                            "Conductor not found."
                    });
            }

            return Ok(
                new
                {
                    success = true,

                    id =
                        document.Id,

                    fields =
                        document.ToDictionary()
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        ex.Message
                });
        }
    }

    // =========================================================
    // GET ONE CONDUCTOR
    // =========================================================

    [HttpGet("{uid}")]
    public async Task<IActionResult> GetById(
        string uid)
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        try
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Conductor UID is required."
                    });
            }

            var document =
                await _firestore
                    .Collection("conductors")
                    .Document(uid)
                    .GetSnapshotAsync();

            if (!document.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,

                        message =
                            "Conductor not found."
                    });
            }

            var data =
                document.ToDictionary();

            var name =
                GetField(
                    data,
                    "name");

            var username =
                GetField(
                    data,
                    "username");

            var phone =
                GetField(
                    data,
                    "phone");

            var role =
                GetField(
                    data,
                    "role",
                    "Conductor");

            var status =
                GetField(
                    data,
                    "status",
                    "Available");

            return Ok(
                new
                {
                    success = true,

                    id =
                        document.Id,

                    name,

                    username,

                    phone,

                    role,

                    status
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        "Failed to load conductor.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // CREATE CONDUCTOR
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        CreateConductorRequest request)
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        string? createdUid = null;

        try
        {
            // -------------------------------------------------
            // VALIDATION
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                    request.Name))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Conductor name is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    request.Username))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Username/email is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    request.Password))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Password is required."
                    });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Password must contain at least 6 characters."
                    });
            }

            // -------------------------------------------------
            // CREATE FIREBASE AUTH USER
            // -------------------------------------------------

            var authArgs =
                new UserRecordArgs
                {
                    Email =
                        request.Username.Trim(),

                    Password =
                        request.Password,

                    DisplayName =
                        request.Name.Trim()
                };

            var user =
                await _firebaseAuth
                    .CreateUserAsync(
                        authArgs);

            createdUid =
                user.Uid;

            // -------------------------------------------------
            // CREATE CONDUCTOR PROFILE
            // -------------------------------------------------

            var conductorProfile =
                new ConductorProfile
                {
                    Name =
                        request.Name.Trim(),

                    Username =
                        request.Username.Trim(),

                    Phone =
                        request.Phone.Trim(),

                    Role =
                        "Conductor",

                    Status =
                        string.IsNullOrWhiteSpace(
                            request.Status)
                            ? "Available"
                            : request.Status.Trim()
                };

            // -------------------------------------------------
            // CREATE USERS PROFILE
            // -------------------------------------------------
            //
            // This is used by the conductor/dispatcher login
            // system.
            //
            // IMPORTANT:
            //
            // Password is NOT stored here.
            // -------------------------------------------------

            var userProfile =
                new Dictionary<string, object>
                {
                    ["active"] =
                        true,

                    ["email"] =
                        request.Username.Trim(),

                    ["name"] =
                        request.Name.Trim(),

                    ["role"] =
                        "conductor"
                };

            try
            {
                // -------------------------------------------------
                // WRITE CONDUCTOR DOCUMENT
                // -------------------------------------------------

                await _firestore
                    .Collection("conductors")
                    .Document(user.Uid)
                    .SetAsync(
                        conductorProfile);

                // -------------------------------------------------
                // WRITE USERS DOCUMENT
                // -------------------------------------------------

                await _firestore
                    .Collection("users")
                    .Document(user.Uid)
                    .SetAsync(
                        userProfile);
            }
            catch
            {
                // -------------------------------------------------
                // ROLLBACK FIREBASE AUTH USER
                // -------------------------------------------------

                try
                {
                    await _firebaseAuth
                        .DeleteUserAsync(
                            user.Uid);
                }
                catch
                {
                    // Ignore rollback error.
                }

                // -------------------------------------------------
                // REMOVE PARTIAL FIRESTORE DOCUMENTS
                // -------------------------------------------------

                try
                {
                    await _firestore
                        .Collection("conductors")
                        .Document(user.Uid)
                        .DeleteAsync();
                }
                catch
                {
                    // Ignore cleanup error.
                }

                try
                {
                    await _firestore
                        .Collection("users")
                        .Document(user.Uid)
                        .DeleteAsync();
                }
                catch
                {
                    // Ignore cleanup error.
                }

                throw;
            }

            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            return Ok(
                new
                {
                    success = true,

                    message =
                        "Conductor created successfully.",

                    id =
                        user.Uid,

                    conductor =
                        new
                        {
                            id =
                                user.Uid,

                            name =
                                conductorProfile.Name,

                            username =
                                conductorProfile.Username,

                            phone =
                                conductorProfile.Phone,

                            role =
                                conductorProfile.Role,

                            status =
                                conductorProfile.Status
                        }
                });
        }
        catch (FirebaseAuthException ex)
        {
            return BadRequest(
                new
                {
                    success = false,

                    message =
                        GetFirebaseAuthErrorMessage(
                            ex)
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        "Failed to create conductor.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // UPDATE CONDUCTOR
    // =========================================================

    [HttpPut("{uid}")]
    public async Task<IActionResult> Update(
        string uid,
        [FromBody]
        UpdateConductorRequest request)
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        try
        {
            // -------------------------------------------------
            // VALIDATE UID
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(uid))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Conductor UID is required."
                    });
            }

            // -------------------------------------------------
            // VALIDATE NAME
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                    request.Name))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Conductor name is required."
                    });
            }

            // -------------------------------------------------
            // VALIDATE USERNAME
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                    request.Username))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Username/email is required."
                    });
            }

            // -------------------------------------------------
            // GET FIRESTORE CONDUCTOR
            // -------------------------------------------------

            var conductorRef =
                _firestore
                    .Collection("conductors")
                    .Document(uid);

            var conductorDocument =
                await conductorRef
                    .GetSnapshotAsync();

            if (!conductorDocument.Exists)
            {
                return NotFound(
                    new
                    {
                        success = false,

                        message =
                            "Conductor Firestore record was not found."
                    });
            }

            var existing =
                conductorDocument
                    .ToDictionary();

            // -------------------------------------------------
            // READ EXISTING VALUES
            // -------------------------------------------------

            var currentName =
                GetField(
                    existing,
                    "name");

            var currentUsername =
                GetField(
                    existing,
                    "username");

            var currentPhone =
                GetField(
                    existing,
                    "phone");

            var currentStatus =
                GetField(
                    existing,
                    "status",
                    "Available");

            // -------------------------------------------------
            // FINAL VALUES
            // -------------------------------------------------

            var newName =
                string.IsNullOrWhiteSpace(
                    request.Name)
                    ? currentName
                    : request.Name.Trim();

            var newUsername =
                string.IsNullOrWhiteSpace(
                    request.Username)
                    ? currentUsername
                    : request.Username.Trim();

            var newPhone =
                string.IsNullOrWhiteSpace(
                    request.Phone)
                    ? currentPhone
                    : request.Phone.Trim();

            var newStatus =
                string.IsNullOrWhiteSpace(
                    request.Status)
                    ? currentStatus
                    : request.Status.Trim();

            // -------------------------------------------------
            // GET FIREBASE AUTH USER
            // -------------------------------------------------

            UserRecord authUser;

            try
            {
                authUser =
                    await _firebaseAuth
                        .GetUserAsync(
                            uid);
            }
            catch (FirebaseAuthException ex)
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "This conductor has a Firestore record, " +
                            "but no matching Firebase Authentication account.",

                        error =
                            ex.Message
                    });
            }

            // -------------------------------------------------
            // PREPARE AUTH UPDATE
            // -------------------------------------------------

            var authArgs =
                new UserRecordArgs
                {
                    Uid =
                        uid,

                    DisplayName =
                        newName
                };

            // -------------------------------------------------
            // UPDATE EMAIL
            // -------------------------------------------------

            if (!string.Equals(
                    authUser.Email,
                    newUsername,
                    StringComparison.OrdinalIgnoreCase))
            {
                authArgs.Email =
                    newUsername;
            }

            // -------------------------------------------------
            // OPTIONAL PASSWORD
            // -------------------------------------------------
            //
            // If empty:
            //     Keep existing password.
            //
            // If supplied:
            //     Change Firebase Auth password.
            //
            // Password is NEVER stored in Firestore.
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                    request.Password))
            {
                if (request.Password.Length < 6)
                {
                    return BadRequest(
                        new
                        {
                            success = false,

                            message =
                                "Password must contain at least 6 characters."
                        });
                }

                authArgs.Password =
                    request.Password;
            }

            // -------------------------------------------------
            // UPDATE FIREBASE AUTH
            // -------------------------------------------------

            try
            {
                await _firebaseAuth
                    .UpdateUserAsync(
                        authArgs);
            }
            catch (FirebaseAuthException ex)
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Firebase Authentication update failed.",

                        error =
                            ex.Message
                    });
            }

            // -------------------------------------------------
            // UPDATE CONDUCTORS COLLECTION
            // -------------------------------------------------

            var conductorProfile =
                new Dictionary<string, object>
                {
                    ["name"] =
                        newName,

                    ["username"] =
                        newUsername,

                    ["phone"] =
                        newPhone,

                    ["role"] =
                        "Conductor",

                    ["status"] =
                        newStatus
                };

            await conductorRef
                .SetAsync(
                    conductorProfile);

            // -------------------------------------------------
            // UPDATE USERS COLLECTION
            // -------------------------------------------------
            //
            // This is what the conductor/dispatcher login
            // checks for the user's role.
            //
            // Password is NOT stored.
            // -------------------------------------------------

            var userRef =
                _firestore
                    .Collection("users")
                    .Document(uid);

            var userProfile =
                new Dictionary<string, object>
                {
                    ["active"] =
                        true,

                    ["email"] =
                        newUsername,

                    ["name"] =
                        newName,

                    ["role"] =
                        "conductor"
                };

            await userRef
                .SetAsync(
                    userProfile);

            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            return Ok(
                new
                {
                    success = true,

                    message =
                        "Conductor updated successfully.",

                    id =
                        uid,

                    conductor =
                        new
                        {
                            id =
                                uid,

                            name =
                                newName,

                            username =
                                newUsername,

                            phone =
                                newPhone,

                            role =
                                "Conductor",

                            status =
                                newStatus
                        }
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        "Failed to update conductor.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // DELETE CONDUCTOR
    // =========================================================

    [HttpDelete("{uid}")]
    public async Task<IActionResult> Delete(
        string uid)
    {
        var authorization = await RequireAdminAsync();
        if (!authorization.IsAuthorized)
            return authorization.FailureResult!;

        try
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            "Conductor UID is required."
                    });
            }

            // -------------------------------------------------
            // DELETE FIREBASE AUTH USER
            // -------------------------------------------------

            try
            {
                await _firebaseAuth
                    .DeleteUserAsync(
                        uid);
            }
            catch (FirebaseAuthException ex)
            {
                // -------------------------------------------------
                // FIREBASE AUTH USER DOES NOT EXIST
                //
                // This supports old Firestore-only conductors
                // such as Romeo and omalay.
                // -------------------------------------------------

                if (ex.AuthErrorCode ==
                    AuthErrorCode.UserNotFound)
                {
                    // Delete conductor document.
                    await _firestore
                        .Collection("conductors")
                        .Document(uid)
                        .DeleteAsync();

                    // Delete users document too, if one exists.
                    await _firestore
                        .Collection("users")
                        .Document(uid)
                        .DeleteAsync();

                    return Ok(
                        new
                        {
                            success = true,

                            message =
                                "Conductor Firestore profile deleted. " +
                                "No Firebase Authentication account existed.",

                            id =
                                uid
                        });
                }

                return BadRequest(
                    new
                    {
                        success = false,

                        message =
                            GetFirebaseAuthErrorMessage(
                                ex)
                    });
            }

            // -------------------------------------------------
            // DELETE CONDUCTOR DOCUMENT
            // -------------------------------------------------

            await _firestore
                .Collection("conductors")
                .Document(uid)
                .DeleteAsync();

            // -------------------------------------------------
            // DELETE USERS DOCUMENT
            // -------------------------------------------------

            await _firestore
                .Collection("users")
                .Document(uid)
                .DeleteAsync();

            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            return Ok(
                new
                {
                    success = true,

                    message =
                        "Conductor deleted successfully.",

                    id =
                        uid
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,

                    message =
                        "Failed to delete conductor.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // FIREBASE AUTH ERROR MESSAGE
    // =========================================================

    private static string
        GetFirebaseAuthErrorMessage(
            FirebaseAuthException ex)
    {
        var message =
            ex.Message ?? "";

        var lowerMessage =
            message.ToLowerInvariant();

        if (lowerMessage.Contains(
                "email already exists"))
        {
            return
                "That email address is already registered.";
        }

        if (lowerMessage.Contains(
                "invalid email"))
        {
            return
                "The username/email address is invalid.";
        }

        if (lowerMessage.Contains(
                "email"))
        {
            return
                "The username/email address is invalid.";
        }

        if (lowerMessage.Contains(
                "password"))
        {
            return
                "The password is invalid or too weak.";
        }

        if (lowerMessage.Contains(
                "user not found"))
        {
            return
                "The Firebase Authentication account was not found.";
        }

        if (lowerMessage.Contains(
                "disabled"))
        {
            return
                "This Firebase Authentication account is disabled.";
        }

        return message;
    }
}