# syntax=docker/dockerfile:1

# Imagen de la Web (Blazor WebAssembly estático) para Railway. El contexto de construcción es la raíz del repositorio:
#   docker build -f deploy/web/Dockerfile -t timetracker-web:local .
# En Railway se selecciona con la variable del servicio RAILWAY_DOCKERFILE_PATH=deploy/web/Dockerfile.

# --- Etapa de compilación: SDK completo ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
SHELL ["/bin/bash", "-o", "pipefail", "-c"]
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TimeTracker.Web/TimeTracker.Web.csproj src/TimeTracker.Web/
RUN dotnet restore src/TimeTracker.Web/TimeTracker.Web.csproj

COPY src/TimeTracker.Web/ src/TimeTracker.Web/
RUN dotnet publish src/TimeTracker.Web/TimeTracker.Web.csproj -c Release -o /app/publish --no-restore

# El publicado trae un <script type="importmap"> con contenido en línea: la CSP lo admite por su hash (sin 'unsafe-inline').
# Falla si no hay importmap o está vacío.
RUN perl -0777 -ne 'm{<script type="importmap">(.+?)</script>}s or die "index.html sin importmap\n"; print $1' /app/publish/wwwroot/index.html \
    | openssl dgst -sha256 -binary | openssl base64 -A > /app/importmap.sha256

# --- Etapa final: nginx sin privilegios (imagen oficial), solo ficheros estáticos ---
FROM nginxinc/nginx-unprivileged:1.30-alpine AS final

ENV PORT=8080
# URL pública de la API (sin barra final); la exige 40-web-config.sh. No tiene valor por defecto a propósito.
ENV API_BASE_URL=

COPY --from=build /app/publish/wwwroot/ /usr/share/nginx/html/
COPY --from=build /app/importmap.sha256 /etc/nginx/importmap.sha256
COPY deploy/web/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --chmod=0755 deploy/web/40-web-config.sh /docker-entrypoint.d/40-web-config.sh

USER root
# El script de arranque escribe appsettings.json y las cabeceras: el usuario sin privilegios necesita poder escribir ahí.
RUN chown nginx:nginx /usr/share/nginx/html /etc/nginx/conf.d
USER nginx

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null "http://127.0.0.1:${PORT}/health" || exit 1
