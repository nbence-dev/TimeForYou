# TimeForYou — Mock WebUntis (Python / FastAPI). Owned by V6 (platform).
#
# This is the container the marker demo depends on (brief §12.4) and, since there is no reachable
# real WebUntis tenant, the system's only backend, permanently — it stands in for nothing.

FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app app
COPY fixtures fixtures

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
