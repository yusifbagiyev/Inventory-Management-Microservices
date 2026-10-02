# Visual Studio runs Debug builds from this stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ApprovalService.API/ApprovalService.API.csproj", "ApprovalService.API/"]
COPY ["ApprovalService.Application/ApprovalService.Application.csproj", "ApprovalService.Application/"]
COPY ["ApprovalService.Domain/ApprovalService.Domain.csproj", "ApprovalService.Domain/"]
COPY ["SharedServices/SharedServices.csproj", "SharedServices/"]
COPY ["ApprovalService.Infrastructure/ApprovalService.Infrastructure.csproj", "ApprovalService.Infrastructure/"]
RUN dotnet restore "./ApprovalService.API/ApprovalService.API.csproj"
COPY . .
WORKDIR "/src/ApprovalService.API"
RUN dotnet build "./ApprovalService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ApprovalService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApprovalService.API.dll"]