# T133: Multi-stage build with Playwright support for dynamic discovery
# Stage 1: Base runtime with Playwright browsers pre-installed (version pinned for compatibility)
ARG PLAYWRIGHT_IMAGE_TAG=v1.55.0-noble
FROM mcr.microsoft.com/playwright/dotnet:${PLAYWRIGHT_IMAGE_TAG} AS base
WORKDIR /app
EXPOSE 8080

# Stage 2: Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY ["apps/recipe-engine/EasyMeals.RecipeEngine.sln", "apps/recipe-engine/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EasyMeals.RecipeEngine.Application.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/EasyMeals.RecipeEngine.Domain.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/EasyMeals.RecipeEngine.Infrastructure.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/"]
COPY ["packages/shared/src/EasyMeals.Shared.Data/EasyMeals.Shared.Data.csproj", "packages/shared/src/EasyMeals.Shared.Data/"]

# Restore dependencies
RUN dotnet restore "apps/recipe-engine/EasyMeals.RecipeEngine.sln"

# Copy entire source code
COPY . .

# Build the solution
WORKDIR "/src/apps/recipe-engine/src/EasyMeals.RecipeEngine"
RUN dotnet build "./EasyMeals.RecipeEngine.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Stage 3: Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EasyMeals.RecipeEngine.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# Stage 4: Final runtime stage with Playwright
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    DOTNET_ROOT=/usr/share/dotnet \
    TZ=UTC

# Install the .NET 8 runtime (base image currently ships with only .NET 6)
RUN curl -sSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends aspnetcore-runtime-8.0 \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd --system recipeengine \
    && useradd --system --gid recipeengine --create-home recipeengine \
    && chown -R recipeengine:recipeengine /app /ms-playwright
USER recipeengine

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet /app/EasyMeals.RecipeEngine.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "EasyMeals.RecipeEngine.dll"]
