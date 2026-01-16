# CAMBIO: Usamos 'aspnet' en lugar de 'runtime'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# --- Instalamos FFmpeg y librerías de audio (Ya lo tenías bien) ---
USER root
RUN apt-get update && apt-get install -y ffmpeg libsodium23 libopus0
USER app
# -----------------------------------------------------
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DiscordBotTato.csproj", "."]

# Restauramos dependencias
RUN dotnet restore "./DiscordBotTato.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./DiscordBotTato.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DiscordBotTato.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Sin esto, el bot no tiene permiso para crear "ranking.json"
USER root
RUN chown -R app:app /app
USER app

ENTRYPOINT ["dotnet", "DiscordBotTato.dll"]