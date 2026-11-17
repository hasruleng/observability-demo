ARG SERVICE_PROJECT

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
ARG SERVICE_PROJECT
COPY NuGet.Config ./
COPY Demo.ServiceDefaults/Demo.ServiceDefaults.csproj Demo.ServiceDefaults/
COPY ${SERVICE_PROJECT}/${SERVICE_PROJECT}.csproj ${SERVICE_PROJECT}/
RUN dotnet restore ${SERVICE_PROJECT}/${SERVICE_PROJECT}.csproj --configfile NuGet.Config
COPY Demo.ServiceDefaults/ Demo.ServiceDefaults/
COPY ${SERVICE_PROJECT}/ ${SERVICE_PROJECT}/
RUN dotnet publish ${SERVICE_PROJECT}/${SERVICE_PROJECT}.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
