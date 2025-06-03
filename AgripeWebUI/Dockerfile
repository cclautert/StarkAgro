# Build Angular
FROM node:20-alpine as builder

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build --configuration=production

# Serve com Nginx
FROM nginx:alpine

RUN rm -rf /usr/share/nginx/html/*

# Ajuste o caminho conforme necessário
COPY --from=builder /app/dist/agripe-web-ui/browser /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf

RUN chmod -R 755 /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
