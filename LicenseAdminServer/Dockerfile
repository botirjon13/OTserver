FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Railway build context is repository root.
COPY LicenseAdminServer/LicenseAdminServer.csproj ./LicenseAdminServer/
RUN dotnet restore ./LicenseAdminServer/LicenseAdminServer.csproj

COPY LicenseAdminServer/. ./LicenseAdminServer/
RUN dotnet publish ./LicenseAdminServer/LicenseAdminServer.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV APP_DATA_DIR=/data

CMD ["dotnet", "LicenseAdminServer.dll"]
