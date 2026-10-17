FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
ARG PROJECT=Brain.Api
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ARG PROJECT=Brain.Api
# Exec the apphost directly: /bin/sh can discard environment keys containing hyphenated agent IDs.
RUN ln -s /app/${PROJECT} /app/brain-host
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["/app/brain-host"]
