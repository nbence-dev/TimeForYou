# TimeForYou — API (ASP.NET Core). Owned by V6 (platform).
#
# Build context must be `server/`, not this directory — the publish needs the
# solution and the sibling projects it references (Domain, Data, WebUntis):
#
#   docker build -f TimeForYou.Api/Dockerfile server/
#
# The TFM comes from server/Directory.Build.props — not restated here.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY TimeForYou.Api/TimeForYou.Api.csproj TimeForYou.Api/
COPY TimeForYou.Domain/TimeForYou.Domain.csproj TimeForYou.Domain/
COPY TimeForYou.Data/TimeForYou.Data.csproj TimeForYou.Data/
COPY TimeForYou.WebUntis/TimeForYou.WebUntis.csproj TimeForYou.WebUntis/
RUN dotnet restore TimeForYou.Api/TimeForYou.Api.csproj

COPY TimeForYou.Api/ TimeForYou.Api/
COPY TimeForYou.Domain/ TimeForYou.Domain/
COPY TimeForYou.Data/ TimeForYou.Data/
COPY TimeForYou.WebUntis/ TimeForYou.WebUntis/
RUN dotnet publish TimeForYou.Api/TimeForYou.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --no-create-home --shell /usr/sbin/nologin appuser
COPY --from=build /app .
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TimeForYou.Api.dll"]
