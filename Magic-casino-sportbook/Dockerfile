FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Magic-casino-sportbook/Magic-casino-sportbook.csproj", "Magic-casino-sportbook/"]
RUN dotnet restore "Magic-casino-sportbook/Magic-casino-sportbook.csproj"

COPY . .
WORKDIR "/src/Magic-casino-sportbook"
RUN dotnet build "Magic-casino-sportbook.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Magic-casino-sportbook.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Magic-casino-sportbook.dll"]
