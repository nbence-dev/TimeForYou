# TimeForYou — Worker (C# BackgroundService). Owned by V6 (platform).
#
# NOT WRITTEN YET. Placeholder so the path is fixed before anyone references it.
#
# Build context must be `server/` — see the note in TimeForYou.Api/Dockerfile.
#
# What belongs here:
#   - same multi-stage shape as the API, but the runtime image is
#     mcr.microsoft.com/dotnet/runtime:10.0 — this service serves no HTTP
#   - no EXPOSE: it polls outward and talks to Postgres and FCM
#   - reads POLL_INTERVAL_SECONDS, QUIET_HOURS_*, CHANGE_WINDOW_DAYS from env
