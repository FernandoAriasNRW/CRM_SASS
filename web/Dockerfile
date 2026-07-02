# Stage 1: Build the Angular application
FROM node:22-alpine AS build
WORKDIR /app

# Copy package.json and lock files
COPY package.json package-lock.json* pnpm-lock.yaml* ./

# Install dependencies (using npm for simplicity as package-lock.json exists)
RUN npm ci || npm install

# Copy the rest of the application code
COPY . .

# Build the Angular application for production
# If 'ng build' creates a dist folder with a subfolder (like 'dist/web/browser'), 
# we'll copy the contents of that folder.
RUN npm run build

# Stage 2: Serve the application using Nginx
FROM nginx:alpine

# Remove default nginx website
RUN rm -rf /usr/share/nginx/html/*

# Copy the custom nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the built Angular app from the build stage. 
# Depending on Angular v17+, the output path is often 'dist/web/browser'
COPY --from=build /app/dist/web/browser /usr/share/nginx/html

# Expose port 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
