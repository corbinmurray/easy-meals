FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy shared project files first
COPY ["packages/shared/src/EasyMeals.Shared.Data/EasyMeals.Shared.Data.csproj", "packages/shared/src/EasyMeals.Shared.Data/"]

# Copy project files for restore
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/EasyMeals.RecipeEngine.Domain.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EasyMeals.RecipeEngine.Application.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/EasyMeals.RecipeEngine.Infrastructure.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine/"]

# Restore dependencies
RUN dotnet restore "apps/recipe-engine/src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj"

# Copy source code
COPY packages/shared/src/EasyMeals.Shared.Data/ packages/shared/src/EasyMeals.Shared.Data/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine/ apps/recipe-engine/src/EasyMeals.RecipeEngine/

# Build the application
WORKDIR "/src/apps/recipe-engine/src/EasyMeals.RecipeEngine"
RUN dotnet build "EasyMeals.RecipeEngine.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EasyMeals.RecipeEngine.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "EasyMeals.RecipeEngine.dll"]
