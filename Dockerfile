# Build Stage using Microsoft .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /source

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release --no-restore -o /source/publish

# Minimal Runtime Stage using Microsoft ASP.NET Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build --chown=65532:65532 /source/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_EnableDiagnostics=0
ENV TZ=Etc/UTC
EXPOSE 8080
USER 65532:65532

LABEL org.opencontainers.image.source="https://github.com/Joakim-Byg/tiny-cs-api"
LABEL org.opencontainers.image.description="Tiny C# .NET 8 API microservice"

ENTRYPOINT ["dotnet", "tiny-cs-api.dll"]
