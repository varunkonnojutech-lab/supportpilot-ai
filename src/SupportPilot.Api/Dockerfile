# Build context is the REPO ROOT (see docker-compose.yml) so the restore
# can see the solution layout.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/SupportPilot.Api/SupportPilot.Api.csproj src/SupportPilot.Api/
RUN dotnet restore src/SupportPilot.Api/SupportPilot.Api.csproj

COPY src/ src/
RUN dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is only here for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# SQLite lives here; docker-compose mounts a volume on /app/data.
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/supportpilot.db"

COPY --from=build /app/publish .

# Run as the non-root `app` user that ships with the .NET 8 images.
# The data directory is created (and owned by app) in the image so a
# fresh named volume mounted on /app/data starts out writable.
RUN mkdir -p /app/data && chown -R app:app /app
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "SupportPilot.Api.dll"]
