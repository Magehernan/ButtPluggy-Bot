FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-amd64 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "ButtPluggy.Bot/ButtPluggy.Bot.csproj"
WORKDIR "/src/ButtPluggy.Bot"
RUN dotnet build "ButtPluggy.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ButtPluggy.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ButtPluggy.Bot.dll"]