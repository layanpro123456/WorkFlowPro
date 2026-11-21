# Multi-stage Dockerfile for WorkFlowPro Angular 22 Frontend
FROM node:22-alpine AS build
WORKDIR /app

# Install package dependencies with layer caching
COPY package.json package-lock.json ./
RUN npm ci

# Copy application source
COPY . .

# Compile Angular production bundle
RUN npm run build -- --configuration production

# Production static serving with Nginx Alpine
FROM nginx:alpine AS runtime

# Remove default Nginx site configuration
RUN rm -rf /etc/nginx/conf.d/*

# Copy custom Nginx configuration with reverse proxy and security headers
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy compiled SPA bundle
COPY --from=build /app/dist/workflowpro-web/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
