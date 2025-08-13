FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for restore
COPY ["apps/crawler/src/EasyMeals.Crawler.Domain/EasyMeals.Crawler.Domain.csproj", "apps/crawler/src/EasyMeals.Crawler.Domain/"]
COPY ["apps/crawler/src/EasyMeals.Crawler.Application/EasyMeals.Crawler.Application.csproj", "apps/crawler/src/EasyMeals.Crawler.Application/"]
COPY ["apps/crawler/src/EasyMeals.Crawler.Infrastructure/EasyMeals.Crawler.Infrastructure.csproj", "apps/crawler/src/EasyMeals.Crawler.Infrastructure/"]
COPY ["apps/crawler/src/EasyMeals.Crawler.Worker/EasyMeals.Crawler.csproj", "apps/crawler/src/EasyMeals.Crawler.Worker/"]
COPY ["apps/shared/EasyMeals.Data/EasyMeals.Data.csproj", "apps/shared/EasyMeals.Data/"]

# Restore dependencies
RUN dotnet restore "apps/crawler/src/EasyMeals.Crawler.Worker/EasyMeals.Crawler.csproj"

# Copy source code
COPY apps/crawler/src/EasyMeals.Crawler.Domain/ apps/crawler/src/EasyMeals.Crawler.Domain/
COPY apps/crawler/src/EasyMeals.Crawler.Application/ apps/crawler/src/EasyMeals.Crawler.Application/
COPY apps/crawler/src/EasyMeals.Crawler.Infrastructure/ apps/crawler/src/EasyMeals.Crawler.Infrastructure/
COPY apps/crawler/src/EasyMeals.Crawler.Worker/ apps/crawler/src/EasyMeals.Crawler.Worker/
COPY apps/shared/EasyMeals.Data/ apps/shared/EasyMeals.Data/

# Build the application
WORKDIR "/src/apps/crawler/src/EasyMeals.Crawler.Worker"
RUN dotnet build "EasyMeals.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EasyMeals.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "EasyMeals.Crawler.dll"]
