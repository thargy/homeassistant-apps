ARG BUILD_FROM=ghcr.io/home-assistant/amd64-base-python:3.12-alpine3.19
FROM ${BUILD_FROM}

# Install requirements for Python data manipulation
RUN apk add --no-cache \
    gcc \
    g++ \
    musl-dev \
    linux-headers

# Copy root filesystem
COPY rootfs /

LABEL \
    org.opencontainers.image.title="Vowels" \
    org.opencontainers.image.description="Automated Electrical Energy System" \
    org.opencontainers.image.source="https://github.com/thargy/homeassistant-apps"
