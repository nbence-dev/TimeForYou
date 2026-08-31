# TimeForYou — Mock WebUntis (Python / FastAPI). Owned by V6 (platform).
#
# NOT WRITTEN YET. Placeholder so the path is fixed before anyone references it.
#
# This is the container the marker demo depends on (brief §12.4) — it stands in
# for the Eduvos WebUntis tenant and mutates timetables on command.
#
# What belongs here:
#   - python:3.12-slim to match docs/dev-setup.md
#   - COPY requirements.txt and pip install BEFORE the source, for layer caching
#   - do NOT copy .venv — add a .dockerignore for it
#   - EXPOSE 8000; uvicorn app.main:app --host 0.0.0.0 --port 8000
#   - fixtures/ must be in the image; the admin page reads them
