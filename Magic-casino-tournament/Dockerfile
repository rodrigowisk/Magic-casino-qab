# Base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Magic-casino-tournament/Magic-casino-tournament.csproj", "Magic-casino-tournament/"]
RUN dotnet restore "Magic-casino-tournament/Magic-casino-tournament.csproj"
COPY . .
WORKDIR "/src/Magic-casino-tournament"
RUN dotnet build "Magic-casino-tournament.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Magic-casino-tournament.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Magic-casino-tournament.dll"]