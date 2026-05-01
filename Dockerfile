FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Bill-App-API/Bill-App-API.csproj Bill-App-API/
COPY Bill-App-Cache/Bill-App-Cache.csproj Bill-App-Cache/
RUN dotnet restore Bill-App-API/Bill-App-API.csproj
COPY Bill-App-API/ Bill-App-API/
COPY Bill-App-Cache/ Bill-App-Cache/
RUN dotnet publish Bill-App-API/Bill-App-API.csproj -c Release --no-restore -o /app/publish/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/wwwroot/avatars
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bill-App-API.dll"]
