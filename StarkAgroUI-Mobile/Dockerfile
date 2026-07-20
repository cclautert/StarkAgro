# Build Expo web bundle
FROM node:20-alpine AS builder

WORKDIR /app
COPY package*.json ./
RUN npm ci --legacy-peer-deps

COPY . .

ARG EXPO_PUBLIC_API_BASE_URL=/api/v1/
ARG EXPO_PUBLIC_GOOGLE_CLIENT_ID=
ENV EXPO_PUBLIC_API_BASE_URL=$EXPO_PUBLIC_API_BASE_URL
ENV EXPO_PUBLIC_GOOGLE_CLIENT_ID=$EXPO_PUBLIC_GOOGLE_CLIENT_ID

RUN npx expo export --platform web

# Serve with Nginx
FROM nginx:alpine

RUN apk add --no-cache gettext

RUN rm -rf /usr/share/nginx/html/*
COPY --from=builder /app/dist /usr/share/nginx/html

COPY nginx.conf.template /etc/nginx/conf.d/default.conf.template
COPY entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

RUN chown -R nginx:nginx /usr/share/nginx/html && chmod -R 755 /usr/share/nginx/html

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
