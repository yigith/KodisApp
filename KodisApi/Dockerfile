# Build stage: restore against the project file alone first so that the
# (slow) restore layer is reused whenever only source files change.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KodisApi.csproj ./
RUN dotnet restore KodisApi.csproj

COPY . ./
RUN dotnet publish KodisApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

# The base image ships a non-root "app" user; running as root is unnecessary here.
USER app

COPY --from=build --chown=app:app /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "KodisApi.dll"]
