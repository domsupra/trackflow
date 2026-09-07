FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY TrackFlow.sln ./
COPY src/TrackFlow.Api/TrackFlow.Api.csproj src/TrackFlow.Api/
COPY tests/TrackFlow.Tests/TrackFlow.Tests.csproj tests/TrackFlow.Tests/
RUN dotnet restore
COPY . .
RUN dotnet test --nologo -v q
RUN dotnet publish src/TrackFlow.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrackFlow.Api.dll"]
