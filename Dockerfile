# Stage 1: Build the React Frontend
FROM node:20 AS frontend-build
WORKDIR /app/frontend

COPY src/frontend/package*.json ./
RUN npm ci

COPY src/frontend/ ./
RUN npm run build

# Stage 2: Build the .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

COPY src/backend/backend.csproj ./src/backend/
RUN dotnet restore "src/backend/backend.csproj"

COPY src/backend/ ./src/backend/
WORKDIR "/src/src/backend"
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Ensure Node.js and npm are installed because the backend spawns npx and node for MCP proxies
RUN apt-get update && \
    apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copy the backend binaries
COPY --from=backend-build /app/publish .

# Copy the frontend static build into wwwroot for the backend to serve it
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Ensure the app runs as the default command
ENTRYPOINT ["dotnet", "backend.dll"]
