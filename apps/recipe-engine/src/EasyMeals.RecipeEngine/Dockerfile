FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/recipe-engine/src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj", "apps/recipe-engine/src/EasyMeals.RecipeEngine/"]
RUN dotnet restore "apps/recipe-engine/src/EasyMeals.RecipeEngine/EasyMeals.RecipeEngine.csproj"
COPY . .
WORKDIR "/src/apps/recipe-engine/src/EasyMeals.RecipeEngine"
RUN dotnet build "./EasyMeals.RecipeEngine.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EasyMeals.RecipeEngine.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyMeals.RecipeEngine.dll"]
