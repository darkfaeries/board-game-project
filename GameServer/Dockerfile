FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY GameDomain/GameDomain.csproj GameDomain/
COPY GameServer/GameServer.csproj GameServer/
RUN dotnet restore GameServer/GameServer.csproj
COPY GameDomain/ GameDomain/
COPY GameServer/ GameServer/
RUN dotnet publish GameServer/GameServer.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
COPY deploy/backend-healthcheck.sh /usr/local/bin/backend-healthcheck
RUN chmod 755 /usr/local/bin/backend-healthcheck
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
    CMD ["/usr/local/bin/backend-healthcheck"]
ENTRYPOINT ["dotnet", "GameServer.dll"]
