using System.Text.Json;

using FirebaseAdmin;
using FirebaseAdmin.Auth;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

var builder =
    WebApplication.CreateBuilder(args);

// =========================================================
// SERVICES
// =========================================================

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "MetroByaheAdmin",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// =========================================================
// SERVICE ACCOUNT FILE
// =========================================================

var configuredCredentialPath =
    Environment.GetEnvironmentVariable(
        "GOOGLE_APPLICATION_CREDENTIALS");

var localCredentialPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "firebase-service-account.json");

var credentialPath =
    !string.IsNullOrWhiteSpace(configuredCredentialPath) &&
    File.Exists(configuredCredentialPath)
        ? configuredCredentialPath
        : localCredentialPath;

if (!File.Exists(credentialPath))
{
    throw new FileNotFoundException(
        "Firebase service account file was not found. " +
        "Set GOOGLE_APPLICATION_CREDENTIALS to a valid file or add " +
        "firebase-service-account.json to the API project directory.",
        credentialPath);
}

// =========================================================
// READ FIREBASE PROJECT ID
// =========================================================

string projectId;

try
{
    var json =
        await File.ReadAllTextAsync(
            credentialPath);

    using var document =
        JsonDocument.Parse(json);

    if (!document.RootElement.TryGetProperty(
            "project_id",
            out var projectIdElement))
    {
        throw new InvalidOperationException(
            "The Firebase service account JSON does not contain project_id.");
    }

    projectId =
        projectIdElement
            .GetString()
            ?? "";

    if (string.IsNullOrWhiteSpace(projectId))
    {
        throw new InvalidOperationException(
            "The Firebase project_id is empty.");
    }
}
catch (JsonException ex)
{
    throw new InvalidOperationException(
        "The Firebase service account JSON is invalid.",
        ex);
}

// =========================================================
// FIREBASE ADMIN SDK
// =========================================================

var serviceAccountCredential =
    CredentialFactory
        .FromFile<ServiceAccountCredential>(
            credentialPath);

var credential =
    GoogleCredential
        .FromServiceAccountCredential(
            serviceAccountCredential);

FirebaseApp firebaseApp;

if (FirebaseApp.DefaultInstance == null)
{
    firebaseApp =
        FirebaseApp.Create(
            new AppOptions
            {
                Credential =
                    credential,

                ProjectId =
                    projectId
            });
}
else
{
    firebaseApp =
        FirebaseApp.DefaultInstance;
}

// =========================================================
// FIREBASE AUTH
// =========================================================

builder.Services.AddSingleton(
    FirebaseAuth.DefaultInstance);

// =========================================================
// FIRESTORE
// =========================================================

var firestoreDb =
    new FirestoreDbBuilder
    {
        ProjectId =
            projectId,

        GoogleCredential =
            credential
    }
    .Build();

builder.Services.AddSingleton(
    firestoreDb);

// =========================================================
// BUILD APPLICATION
// =========================================================

var app =
    builder.Build();

// =========================================================
// CORS
// =========================================================

app.UseCors(
    "MetroByaheAdmin");

// =========================================================
// CONTROLLERS
// =========================================================

app.MapControllers();

// =========================================================
// API HOME
// =========================================================

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                success = true,

                message =
                    "MetroByahe Admin API is running."
            }));

// =========================================================
// FIREBASE CONNECTION TEST
// =========================================================

// Firebase diagnostics run only in local development.
if (app.Environment.IsDevelopment())
{
app.MapGet(
    "/api/test/firebase",
    async () =>
    {
        try
        {
            try
            {
                await FirebaseAuth
                    .DefaultInstance
                    .GetUserByEmailAsync(
                        "firebase-test-connection@invalid.local");
            }
            catch (FirebaseAuthException)
            {
                // The test email does not need to exist.
                // Receiving a Firebase response confirms
                // that Firebase Authentication is reachable.
            }

            return Results.Ok(
                new
                {
                    success = true,

                    message =
                        "Firebase Admin SDK connected.",

                    projectId =
                        projectId
                });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail:
                    ex.ToString());
        }
    });
}

app.Run();
