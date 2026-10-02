# Visual Studio runs Debug builds from this stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ProductService.API/ProductService.API.csproj", "ProductService.API/"]
COPY ["ApprovalService.Application/ApprovalService.Application.csproj", "ApprovalService.Application/"]
COPY ["ApprovalService.Domain/ApprovalService.Domain.csproj", "ApprovalService.Domain/"]
COPY ["SharedServices/SharedServices.csproj", "SharedServices/"]
COPY ["IdentityService.Domain/IdentityService.Domain.csproj", "IdentityService.Domain/"]
COPY ["ProductService.Application/ProductService.Application.csproj", "ProductService.Application/"]
COPY ["ProductService.Domain/ProductService.Domain.csproj", "ProductService.Domain/"]
COPY ["ProductService.Infrastructure/ProductService.Infrastructure.csproj", "ProductService.Infrastructure/"]
RUN dotnet restore "./ProductService.API/ProductService.API.csproj"
COPY . .
WORKDIR "/src/ProductService.API"
RUN dotnet build "./ProductService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProductService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


ENTRYPOINT ["dotnet", "ProductService.API.dll"]