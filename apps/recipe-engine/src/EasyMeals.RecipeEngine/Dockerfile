# T133: Multi-stage build with Playwright support for dynamic discovery
# Stage 1: Base runtime with Playwright browsers pre-installed
FROM mcr.microsoft.com/playwright/dotnet:v1.40.0-focal AS base
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
    TZ=UTC

# Create non-root user for security
RUN groupadd -r recipeengine && useradd -r -g recipeengine recipeengine
RUN chown -R recipeengine:recipeengine /app
USER recipeengine

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet /app/EasyMeals.RecipeEngine.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "EasyMeals.RecipeEngine.dll"]
