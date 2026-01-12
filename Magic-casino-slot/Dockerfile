# ================= BASE =================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ================= BUILD =================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# copia SOMENTE o csproj correto
COPY ["Magic-casino-slot/Magic-casino-slot.csproj", "Magic-casino-slot/"]
RUN dotnet restore "Magic-casino-slot/Magic-casino-slot.csproj"

# copia o resto do código
COPY . .
WORKDIR "/src/Magic-casino-slot"
RUN dotnet build "Magic-casino-slot.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ================= PUBLISH =================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Magic-casino-slot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ================= FINAL =================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Magic-casino-slot.dll"]
