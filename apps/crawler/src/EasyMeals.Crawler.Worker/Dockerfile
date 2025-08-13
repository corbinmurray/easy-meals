FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/crawler/src/EasyMeals.Crawler/EasyMeals.Crawler.csproj", "EasyMeals.Crawler/"]
RUN dotnet restore "EasyMeals.Crawler/EasyMeals.Crawler.csproj"
COPY apps/crawler/src/EasyMeals.Crawler/ EasyMeals.Crawler/
WORKDIR "/src/EasyMeals.Crawler"
RUN dotnet build "./EasyMeals.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EasyMeals.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyMeals.Crawler.dll"]
