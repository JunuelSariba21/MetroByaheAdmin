using Google.Cloud.Firestore;

namespace MetroByaheAdmin.Api.Models;

// =========================================================
// FIRESTORE CONDUCTOR PROFILE
// =========================================================

[FirestoreData]
public class ConductorProfile
{
    [FirestoreProperty]
    public string Name { get; set; } = "";

    [FirestoreProperty]
    public string Username { get; set; } = "";

    [FirestoreProperty]
    public string Phone { get; set; } = "";

    [FirestoreProperty]
    public string Role { get; set; } = "Conductor";

    [FirestoreProperty]
    public string Status { get; set; } = "Available";
}


// =========================================================
// CREATE CONDUCTOR REQUEST
// =========================================================

public class CreateConductorRequest
{
    public string Name { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Status { get; set; } = "Available";
}


// =========================================================
// UPDATE CONDUCTOR REQUEST
// =========================================================

public class UpdateConductorRequest
{
    public string Name { get; set; } = "";

    public string Username { get; set; } = "";

    public string? Password { get; set; }

    public string Phone { get; set; } = "";

    public string Status { get; set; } = "Available";
}