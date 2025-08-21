FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for restore
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/EasyMeals.RecipeEngine.Domain.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EasyMeals.RecipeEngine.Application.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/EasyMeals.RecipeEngine.Infrastructure.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/"]
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker/EasyMeals.RecipeEngine.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker/"]
COPY ["apps/shared/EasyMeals.Data/EasyMeals.Data.csproj", "apps/shared/EasyMeals.Data/"]

# Restore dependencies
RUN dotnet restore "apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker/EasyMeals.RecipeEngine.csproj"

# Copy source code
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/
COPY apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker/ apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker/
COPY apps/shared/EasyMeals.Data/ apps/shared/EasyMeals.Data/

# Build the application
WORKDIR "/src/apps/recipe-engine/src/EasyMeals.RecipeEngine.Worker"
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
