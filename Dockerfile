FROM mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
ARG SERVICE
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/${SERVICE}.Api/${SERVICE}.Api.csproj --locked-mode
RUN dotnet publish src/${SERVICE}.Api/${SERVICE}.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS runtime
ARG SERVICE
ENV AZURELAB_SERVICE=${SERVICE}
ENV ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"$AZURELAB_SERVICE.Api.dll\""]
