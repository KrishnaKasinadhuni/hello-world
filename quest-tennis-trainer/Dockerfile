# syntax=docker/dockerfile:1.7
# ==============================================================================
# Hardened Multi-Stage Dockerfile for Quest 3 Tennis Trainer Physics Sandbox
# - Stage 1 (build): Minimal Alpine SDK with deterministic restore & compilation
# - Stage 2 (sandbox): Microsoft Hardened Ubuntu Chiseled (distroless, no shell,
#   no package manager, non-root UID 1654 $APP_UID) executing NUnitLite runner
# ==============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

WORKDIR /src

# 1. Copy project manifest first to cache NuGet layer independently of C# edits
COPY QuestTennisTrainer.Tests.csproj ./
RUN dotnet restore ./QuestTennisTrainer.Tests.csproj

# 2. Copy C# physics, training modes, XR haptics, and NUnit test sources
COPY Assets/ ./Assets/

# 3. Compile with Release optimizations and publish self-contained test assembly
RUN dotnet publish ./QuestTennisTrainer.Tests.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ==============================================================================
# Stage 2: Hardened Distroless Runtime (Ubuntu Noble Chiseled)
# - Zero shell (/bin/sh removed), zero package manager, minimal attack surface
# - Runs strictly as unprivileged non-root user ($APP_UID = 1654)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled AS sandbox

WORKDIR /app
COPY --from=build /app/publish ./

# Enforce non-root execution provided by Chiseled base image
USER $APP_UID

ENTRYPOINT ["dotnet", "/app/QuestTennisTrainer.Tests.dll", "--noresult"]

