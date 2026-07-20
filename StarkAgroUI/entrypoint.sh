#!/bin/sh

export API_BASE_URL="${API_BASE_URL:-https://agripeweb-api.azurewebsites.net}"

envsubst '${API_BASE_URL}' \
  < /etc/nginx/conf.d/default.conf.template \
  > /etc/nginx/conf.d/default.conf

exec nginx -g "daemon off;"
