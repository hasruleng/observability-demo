FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY NuGet.Config ./
COPY Demo.ServiceDefaults/Demo.ServiceDefaults.csproj Demo.ServiceDefaults/
COPY Demo.Dependency/Demo.Dependency.csproj Demo.Dependency/
RUN dotnet restore Demo.Dependency/Demo.Dependency.csproj --configfile NuGet.Config
COPY Demo.ServiceDefaults/ Demo.ServiceDefaults/
COPY Demo.Dependency/ Demo.Dependency/
RUN dotnet publish Demo.Dependency/Demo.Dependency.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.Dependency.dll"]
