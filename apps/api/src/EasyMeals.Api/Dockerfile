FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy shared project files first for restore
COPY ["packages/shared/src/EasyMeals.Shared.Data/EasyMeals.Shared.Data.csproj", "packages/shared/src/EasyMeals.Shared.Data/"]

# Copy API project file
COPY ["apps/api/src/EasyMeals.Api/EasyMeals.Api.csproj", "apps/api/src/EasyMeals.Api/"]

# Restore dependencies
RUN dotnet restore "apps/api/src/EasyMeals.Api/EasyMeals.Api.csproj"

# Copy all source files
COPY packages/shared/src/EasyMeals.Shared.Data/ packages/shared/src/EasyMeals.Shared.Data/
COPY apps/api/src/EasyMeals.Api/ apps/api/src/EasyMeals.Api/

# Build the API project
WORKDIR "/src/apps/api/src/EasyMeals.Api"
RUN dotnet build "./EasyMeals.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EasyMeals.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyMeals.Api.dll"]
