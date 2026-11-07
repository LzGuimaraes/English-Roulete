FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/EnglishRoulette.Api/EnglishRoulette.Api.csproj --locked-mode
RUN dotnet publish src/EnglishRoulette.Api/EnglishRoulette.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false
RUN test -f /out/wwwroot/_framework/blazor.webassembly.js && ! grep -q '#\[' /out/wwwroot/index.html

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/media /data/keys && chown -R app:app /data
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production Media__Root=/data/media DataProtection__KeysPath=/data/keys
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 CMD curl --fail --silent -H "Host: ${AllowedHosts:-localhost}" http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "EnglishRoulette.Api.dll"]
