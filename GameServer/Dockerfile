FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY GameServer/GameServer.csproj GameServer/
COPY GameDomain/GameDomain.csproj GameDomain/
COPY GameClient/GameClient.csproj GameClient/
RUN dotnet restore GameServer/GameServer.csproj
COPY GameServer/ GameServer/
COPY GameDomain/ GameDomain/
COPY GameClient/ GameClient/
RUN dotnet publish GameServer/GameServer.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /src/GameDomain/GameFiles/Maps/ /app/GameDomain/GameFiles/Maps/
RUN mkdir -p logs && chown app:app logs
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "GameServer.dll"]
