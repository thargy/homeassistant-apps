# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Install build dependencies for Native AOT
RUN apk add --no-cache clang build-base zlib-dev

# Copy project files and restore
COPY src/ .
RUN dotnet restore Vowels.sln

# Publish Native AOT binary
RUN dotnet publish Vowels.Daemon/Vowels.Daemon.csproj \
    -c Release \
    -r linux-musl-x64 \
    --self-contained \
    -p:PublishAot=true \
    -o /app/publish

# Final Stage
FROM alpine:3.19
WORKDIR /app
COPY --from=build /app/publish/Vowels.Daemon /app/vowels

# Copy S6-Overlay config
COPY rootfs /

LABEL \
    org.opencontainers.image.title="Vowels" \
    org.opencontainers.image.description="Automated Electrical Energy System (.NET Native AOT)" \
    org.opencontainers.image.source="https://github.com/thargy/homeassistant-apps"

ENTRYPOINT ["/app/vowels"]
