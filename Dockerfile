# Build Stage using Wolfi .NET SDK
FROM cgr.dev/chainguard/dotnet-sdk:latest AS build

WORKDIR /source

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release --no-restore -o /source/publish

# Minimal Runtime Stage using Wolfi ASP.NET Runtime
FROM cgr.dev/chainguard/aspnet-runtime:latest

WORKDIR /app
COPY --from=build --chown=65532:65532 /source/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_EnableDiagnostics=0
ENV TZ=Etc/UTC
EXPOSE 8080
USER 65532:65532

ENTRYPOINT ["dotnet", "tiny-cs-api.dll"]
