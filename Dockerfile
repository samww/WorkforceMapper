# Stage 1: build frontend
FROM node:22-slim AS frontend-build
WORKDIR /src
COPY workforcemapper.client/package*.json ./workforcemapper.client/
RUN cd workforcemapper.client && npm ci
COPY workforcemapper.client/ ./workforcemapper.client/
RUN mkdir -p WorkforceMapper.Server/wwwroot && cd workforcemapper.client && npm run build

# Stage 2: build server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY WorkforceMapper.Server/WorkforceMapper.Server.csproj ./WorkforceMapper.Server/
COPY workforcemapper.client/workforcemapper.client.esproj ./workforcemapper.client/
COPY workforcemapper.client/package.json ./workforcemapper.client/
RUN dotnet restore ./WorkforceMapper.Server/WorkforceMapper.Server.csproj
COPY WorkforceMapper.Server/ ./WorkforceMapper.Server/
COPY data/ ./data/
COPY --from=frontend-build /src/WorkforceMapper.Server/wwwroot ./WorkforceMapper.Server/wwwroot/
RUN printf '#!/bin/sh\nexit 0\n' > /usr/local/bin/npm && chmod +x /usr/local/bin/npm
RUN dotnet publish ./WorkforceMapper.Server/WorkforceMapper.Server.csproj \
    -c Release -o /app/publish --no-restore -p:ShouldRunNpmInstall=false

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "WorkforceMapper.Server.dll"]
