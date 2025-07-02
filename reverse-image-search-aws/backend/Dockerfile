# Use an official Python runtime as a parent image
FROM python:3.11-slim

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE 1
ENV PYTHONUNBUFFERED 1

# Set the working directory in the container
WORKDIR /code

# Install system dependencies (if any - e.g., for Pillow)
# RUN apt-get update && apt-get install -y --no-install-recommends some-package && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
# Copy only requirements to leverage Docker cache
COPY requirements.txt /code/
RUN pip install --no-cache-dir --upgrade pip
RUN pip install --no-cache-dir -r requirements.txt

# Copy the rest of the application code
COPY . /code/

# Expose the port the app runs on
EXPOSE 8000

# Define the command to run the application
# Use the host 0.0.0.0 to accept connections from outside the container
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"] 