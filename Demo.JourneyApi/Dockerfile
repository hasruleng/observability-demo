FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY NuGet.Config ./
COPY Demo.ServiceDefaults/Demo.ServiceDefaults.csproj Demo.ServiceDefaults/
COPY Demo.JourneyApi/Demo.JourneyApi.csproj Demo.JourneyApi/
RUN dotnet restore Demo.JourneyApi/Demo.JourneyApi.csproj --configfile NuGet.Config
COPY Demo.ServiceDefaults/ Demo.ServiceDefaults/
COPY Demo.JourneyApi/ Demo.JourneyApi/
RUN dotnet publish Demo.JourneyApi/Demo.JourneyApi.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.JourneyApi.dll"]
