# CAMBIO IMPORTANTE: Usamos 'aspnet' en lugar de 'runtime'
# Esto es necesario porque convertimos el bot en un "Web Service" falso
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# --- NUEVO: Instalamos FFmpeg y librerías de audio ---
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
# Copiamos el archivo de proyecto
COPY ["DiscordBotTato.csproj", "."]
# Restauramos dependencias (Aquí se descarga Discord.Net)
RUN dotnet restore "./DiscordBotTato.csproj"
COPY . .
WORKDIR "/src/."
# Compilamos
RUN dotnet build "./DiscordBotTato.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DiscordBotTato.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DiscordBotTato.dll"]
