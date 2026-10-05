FROM node:20-alpine AS client-build
WORKDIR /client
COPY src/MadlanExplorer/client/package.json src/MadlanExplorer/client/package-lock.json ./
RUN npm ci
COPY src/MadlanExplorer/client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=client-build /client/dist/ ./src/MadlanExplorer/wwwroot/
RUN dotnet restore MadlanExplorer.sln

FROM build AS test
RUN dotnet test MadlanExplorer.sln --no-restore

FROM build AS publish
RUN dotnet publish src/MadlanExplorer/MadlanExplorer.csproj --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MadlanExplorer.dll"]
