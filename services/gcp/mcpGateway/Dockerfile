FROM python:3.11-slim

WORKDIR /app

# Prevent Python from writing .pyc files & buffer output
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PORT=8080

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy source code
COPY . .

# Expose target port
EXPOSE 8080

# Run Uvicorn server on PORT
CMD exec uvicorn main:app --host 0.0.0.0 --port ${PORT}
