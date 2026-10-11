FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MetroByaheAdmin.Api.csproj ./
RUN dotnet restore MetroByaheAdmin.Api.csproj
COPY Controllers/ ./Controllers/
COPY Models/ ./Models/
COPY Program.cs appsettings.json ./
RUN dotnet publish MetroByaheAdmin.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_ENVIRONMENT=Production
ENV GOOGLE_APPLICATION_CREDENTIALS=/etc/secrets/firebase-service-account.json
EXPOSE 10000
CMD ["sh", "-c", "exec dotnet MetroByaheAdmin.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]