# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src



# Copy project files and restore
COPY src/ .
RUN dotnet restore Vowels.sln

# Publish JIT binary
RUN dotnet publish Vowels.Daemon/Vowels.Daemon.csproj \
    -c Release \
    -o /app/publish

# Final Stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish/ /app/

# Copy S6-Overlay config
COPY rootfs /

LABEL \
    org.opencontainers.image.title="Vowels" \
    org.opencontainers.image.description="Automated Electrical Energy System (.NET 10)" \
    org.opencontainers.image.source="https://github.com/thargy/homeassistant-apps"

ENTRYPOINT ["dotnet", "/app/Vowels.Daemon.dll"]
