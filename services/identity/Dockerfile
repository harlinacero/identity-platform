# Multi-stage build that compiles inside the image (ADR-0005).
# Targets: `service` (the API) and `migrations` (a one-shot bundle run as identity_migrator).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Identity.slnx global.json ./
COPY .config/ .config/
COPY src/ src/
RUN dotnet tool restore \
    && dotnet publish src/Identity.Api/Identity.Api.csproj -c Release -o /out/service \
    && dotnet ef migrations bundle \
        -p src/Identity.Infrastructure -s src/Identity.Infrastructure \
        -o /out/migrations/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=build /out/migrations/ ./
USER $APP_UID
# Shell form: the connection string comes from the environment at run time.
ENTRYPOINT ["sh", "-c", "exec ./efbundle --connection \"$MIGRATOR_CONNECTION_STRING\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS service
WORKDIR /app
COPY --from=build /out/service/ ./
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
