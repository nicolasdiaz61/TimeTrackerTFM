# syntax=docker/dockerfile:1

# --- Etapa de compilación: SDK completo ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo lo necesario para restaurar: así la capa de paquetes se reutiliza mientras no cambien los .csproj.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TimeTracker.Domain/TimeTracker.Domain.csproj src/TimeTracker.Domain/
COPY src/TimeTracker.Application/TimeTracker.Application.csproj src/TimeTracker.Application/
COPY src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj src/TimeTracker.Infrastructure/
COPY src/TimeTracker.Api/TimeTracker.Api.csproj src/TimeTracker.Api/
RUN dotnet restore src/TimeTracker.Api/TimeTracker.Api.csproj

COPY src/ src/
# No se regenera docs/api/openapi.json dentro de la imagen: ese contrato lo genera y versiona el build local (V6).
RUN dotnet publish src/TimeTracker.Api/TimeTracker.Api.csproj -c Release -o /app/publish --no-restore \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# --- Etapa final: solo el entorno de ejecución, sin secretos ni herramientas de compilación ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# La configuración (orígenes CORS, etc.) llega por variables de entorno en tiempo de ejecución.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# La imagen oficial define APP_UID (usuario no root).
USER $APP_UID

# Forma shell para expandir el puerto; admite un único puerto. wget viene en BusyBox (Alpine).
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget -q -O /dev/null "http://127.0.0.1:${ASPNETCORE_HTTP_PORTS:-8080}/health" || exit 1

ENTRYPOINT ["dotnet", "TimeTracker.Api.dll"]
