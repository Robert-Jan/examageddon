FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY src/Examageddon.slnx src/
COPY src/Directory.Build.props src/
COPY src/Directory.Packages.props src/
COPY src/Examageddon.Data/Examageddon.Data.csproj src/Examageddon.Data/
COPY src/Examageddon.Services/Examageddon.Services.csproj src/Examageddon.Services/
COPY src/Examageddon.Tests/Examageddon.Tests.csproj src/Examageddon.Tests/
COPY src/Examageddon.Web/Examageddon.Web.csproj src/Examageddon.Web/
RUN dotnet restore src/Examageddon.slnx

COPY src/ src/
RUN dotnet publish src/Examageddon.Web/Examageddon.Web.csproj \
    -c Release \
    -o /publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /publish .

RUN mkdir /data && chown app:app /data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Examageddon.Web.dll"]
