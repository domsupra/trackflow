# Multi-stage: build the React dashboard, then the ASP.NET Core API, then serve both
# from the API process (the Vite bundle lands in the API's wwwroot).

FROM node:22-alpine AS dashboard
WORKDIR /app
COPY dashboard/package.json dashboard/package-lock.json ./
RUN npm ci
COPY dashboard/ ./
# Emit to a known absolute dir in the container (local dev uses the config's relative outDir).
RUN npm run build -- --outDir /app/dist

# build the API (csproj-only restore first so its cache layer survives API source edits)
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY global.json TrackFlow.sln ./
COPY src/TrackFlow.Api/TrackFlow.Api.csproj src/TrackFlow.Api/
COPY tests/TrackFlow.Tests/TrackFlow.Tests.csproj tests/TrackFlow.Tests/
RUN dotnet restore
COPY src ./src
COPY tests ./tests
# Always use the fresh dashboard bundle, never a local wwwroot from the build context.
RUN rm -rf src/TrackFlow.Api/wwwroot && cp -r /app/dist src/TrackFlow.Api/wwwroot
RUN dotnet test --nologo -v q
RUN dotnet publish src/TrackFlow.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrackFlow.Api.dll"]
