#!/bin/sh
# Default: URL da API no Azure. No Docker Compose, defina API_BASE_URL=http://agripewebapi:8080.
echo "API_BASE_URL=$API_BASE_URL"
echo "Generated config:"
cat /etc/nginx/conf.d/default.conf
export API_BASE_URL="${API_BASE_URL:-https://agripeweb-api.azurewebsites.net}"
envsubst '$API_BASE_URL' < /etc/nginx/conf.d/nginx.conf.template > /etc/nginx/conf.d/default.conf
exec nginx -g "daemon off;"
