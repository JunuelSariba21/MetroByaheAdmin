# Deploy MetroByahe Admin API to Render

The API deployment files are ready locally. They must be committed and pushed to your GitHub repository before Render can use them. Deploy only the API; the WPF application stays on Windows.

## Create a Free Web Service

1. In https://dashboard.render.com choose New > Web Service and connect your MetroByaheAdmin GitHub repository. Authorize access if it is private.
2. Select the branch containing the deployment files.
3. Name: metrobyahe-admin-api (or an available name).
4. Region: Singapore if available.
5. Root Directory: MetroByaheAdmin.Api. This is relative to the MetroByaheAdmin Git repository root, not C:\Capstone. If the connected repository instead contains a top-level MetroByaheAdmin folder, use MetroByaheAdmin/MetroByaheAdmin.Api.
6. Language/runtime: Docker. Dockerfile Path: ./Dockerfile. Docker build context: . (the API root). Do not enter a build command or start command; the Dockerfile supplies them.
7. Instance Type: Free. Health Check Path: /.
8. In Environment > Secret Files add firebase-service-account.json and paste the contents of the local API's file directly into Render. Do not put the key in GitHub or chat. Add this before deploying if the creation screen permits it; otherwise add it after creation and redeploy.
9. Set GOOGLE_APPLICATION_CREDENTIALS to /etc/secrets/firebase-service-account.json and ASPNETCORE_ENVIRONMENT to Production. These are also defaults in the Dockerfile. Render supplies PORT; the container binds to that port on 0.0.0.0.
10. Create/deploy the service. Open the exact HTTPS URL shown by Render. The root should return success and 'MetroByahe Admin API is running.'

The Dockerfile copies only API source, configuration, and compiled output. The service-account file is excluded from the Docker build context and read from Render's runtime secret directory.

## Point the desktop app at Render

Replace the content of admin-api-url.txt in the desktop project with the exact Render HTTPS root URL, for example https://metrobyahe-admin-api.onrender.com. Use the actual domain assigned to your service. Do not append /api. The URL file is automatically copied when publishing. METROBYAHE_ADMIN_API_URL overrides the file if set on a user's computer.

From the MetroByaheAdmin project directory:

    dotnet publish MetroByaheAdmin.csproj -c Release -r win-x64 --self-contained true -o artifacts/desktop

Test with an authorized Firebase administrator: read conductors, create/update a route or fare matrix, and check the result. Confirm a request without a bearer token to /api/conductors returns 401. Confirm /api/test/firebase returns 404 in Production.

Share the complete fresh artifacts/desktop folder after verification. Users run MetroByaheAdmin.exe; internet access, an administrator account, and the WebView2 Runtime are required. Users do not run the API or install .NET. Do not distribute Firebase service-account keys or browser/session data from a previously used publish folder.

## Free hosting behavior

Render Free Web Services sleep after 15 minutes without incoming traffic. Waking takes about a minute; the desktop request timeout is 120 seconds to allow for this. If a request still times out, wait and try again. Free services have monthly usage limits and Render recommends them for hobby/demo use rather than production. Without a payment method, reaching certain limits suspends services/builds rather than charging overages. Firestore remains the database; no Render database is needed.

References:
- https://render.com/docs/free
- https://render.com/docs/docker
- https://render.com/docs/docker-secrets
- https://render.com/docs/web-services