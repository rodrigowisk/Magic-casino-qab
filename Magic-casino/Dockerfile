# Usa a imagem base do .NET 8.0 para rodar o app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
#USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Usa a imagem do SDK do .NET 8.0 para construir o app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Magic-casino/Magic-casino.csproj", "Magic-casino/"]
RUN dotnet restore "./Magic-casino/Magic-casino.csproj"
COPY . .
WORKDIR "/src/Magic-casino"
RUN dotnet build "./Magic-casino.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Magic-casino.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Magic-casino.dll"]