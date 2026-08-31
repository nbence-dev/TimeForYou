# TimeForYou — API (ASP.NET Core). Owned by V6 (platform).
#
# NOT WRITTEN YET. Placeholder so the path is fixed before anyone references it.
#
# Build context must be `server/`, not this directory — the publish needs the
# solution and the sibling projects it references (Domain, Data, WebUntis):
#
#   docker build -f TimeForYou.Api/Dockerfile server/
#
# What belongs here:
#   - multi-stage: mcr.microsoft.com/dotnet/sdk:10.0 to build, aspnet:10.0 to run
#   - restore before copying the full source, so layer caching survives edits
#   - non-root user
#   - EXPOSE 8080, and ASPNETCORE_URLS pointing at it
#   - the TFM comes from server/Directory.Build.props — don't restate it here
