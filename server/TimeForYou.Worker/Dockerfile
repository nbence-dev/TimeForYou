# TimeForYou — Worker (C# BackgroundService). Owned by V6 (platform).
#
# Build context must be `server/` — see the note in TimeForYou.Api/Dockerfile:
#
#   docker build -f TimeForYou.Worker/Dockerfile server/
#
# Same multi-stage shape as the API, but the runtime image is the plain
# `dotnet/runtime` — this service serves no HTTP, it polls outward and talks
# to Postgres and FCM.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY TimeForYou.Worker/TimeForYou.Worker.csproj TimeForYou.Worker/
COPY TimeForYou.Domain/TimeForYou.Domain.csproj TimeForYou.Domain/
COPY TimeForYou.Data/TimeForYou.Data.csproj TimeForYou.Data/
COPY TimeForYou.WebUntis/TimeForYou.WebUntis.csproj TimeForYou.WebUntis/
RUN dotnet restore TimeForYou.Worker/TimeForYou.Worker.csproj

COPY TimeForYou.Worker/ TimeForYou.Worker/
COPY TimeForYou.Domain/ TimeForYou.Domain/
COPY TimeForYou.Data/ TimeForYou.Data/
COPY TimeForYou.WebUntis/ TimeForYou.WebUntis/
RUN dotnet publish TimeForYou.Worker/TimeForYou.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app .
USER appuser

ENTRYPOINT ["dotnet", "TimeForYou.Worker.dll"]
