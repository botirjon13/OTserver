FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LicenseAdminServer.csproj ./
RUN dotnet restore LicenseAdminServer.csproj

COPY . ./
RUN dotnet publish LicenseAdminServer.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV APP_DATA_DIR=/data

CMD ["dotnet", "LicenseAdminServer.dll"]
