FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/api/src/EasyMeals.Api/EasyMeals.Api.csproj", "EasyMeals.Api/"]
RUN dotnet restore "EasyMeals.Api/EasyMeals.Api.csproj"
COPY apps/api/src/EasyMeals.Api/ EasyMeals.Api/
WORKDIR "/src/EasyMeals.Api"
RUN dotnet build "./EasyMeals.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EasyMeals.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyMeals.Api.dll"]
