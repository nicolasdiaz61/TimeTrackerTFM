#!/bin/sh
# Se ejecuta al arrancar el contenedor (antes de nginx). Con API_BASE_URL genera:
#  - wwwroot/appsettings.json con ApiBaseUrl (la Web lee la URL de la API de ahí), y
#  - las cabeceras de seguridad, con la CSP abierta solo al origen de la API.
# Sin código ni URLs reales en el repositorio: el valor llega por variable de entorno.
set -eu

html=/usr/share/nginx/html

fail() {
    echo "40-web-config.sh: $1" >&2
    exit 1
}

[ -n "${API_BASE_URL:-}" ] || fail "defina API_BASE_URL (URL absoluta http/https de la API)."

# Sin barra final y solo caracteres seguros: el valor acaba dentro de un JSON y de una cabecera HTTP.
api_url="${API_BASE_URL%/}"
case "$api_url" in
    http://* | https://*) ;;
    *) fail "API_BASE_URL debe empezar por http:// o https://." ;;
esac
if printf '%s' "$api_url" | grep -Eq '[^A-Za-z0-9:/._-]'; then
    fail "API_BASE_URL contiene caracteres no permitidos."
fi

# Origen (esquema + host + puerto) para connect-src.
api_origin="$(printf '%s' "$api_url" | sed -E 's|^(https?://[^/]+).*$|\1|')"
importmap_hash="$(cat /etc/nginx/importmap.sha256)"

# La publicación incluye versiones precomprimidas (.gz, .br) del appsettings.json original: se retiran para
# que nadie sirva la antigua con gzip_static. Tampoco se publica el de desarrollo (apunta a localhost).
rm -f "$html"/appsettings.json "$html"/appsettings.json.gz "$html"/appsettings.json.br \
    "$html"/appsettings.Development.json "$html"/appsettings.Development.json.gz "$html"/appsettings.Development.json.br
printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$api_url" > "$html/appsettings.json"

# 'wasm-unsafe-eval' lo exige Blazor WebAssembly para compilar el WebAssembly; el importmap en línea se admite por hash.
csp="default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-${importmap_hash}'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self' ${api_origin}; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'"

cat > /etc/nginx/conf.d/security-headers.inc <<HEADERS
add_header Content-Security-Policy "${csp}" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Referrer-Policy "no-referrer" always;
add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;
add_header Cross-Origin-Opener-Policy "same-origin" always;
add_header Strict-Transport-Security "max-age=31536000" always;
HEADERS

echo "40-web-config.sh: configuración de la Web generada para el origen de la API ${api_origin}."
