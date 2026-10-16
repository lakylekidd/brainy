FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
ARG PROJECT=Brain.Api
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ARG PROJECT=Brain.Api
ENV BRAIN_PROJECT=${PROJECT}
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"${BRAIN_PROJECT}.dll\""]
