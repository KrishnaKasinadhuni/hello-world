# API Test Requests

This directory contains curl requests for testing the Image Classification Service endpoints locally.

## Prerequisites

1. Make sure the service is running locally:
```bash
npm run dev
```

2. Make the curl request scripts executable:
```bash
chmod +x *.sh
```

## Available Endpoints

### 1. Health Check
```bash
./health-check.sh
```
This endpoint checks if the service is running properly.

### 2. Upload Image
```bash
./upload-image.sh
```
This endpoint uploads an image and generates embeddings.
- Replace `/path/to/your/image.jpg` with the actual path to your image file
- The image will be processed and stored in the uploads directory
- Returns the generated embedding and file information

### 3. Classify Image
```bash
./classify-image.sh
```
This endpoint classifies an uploaded image.
- Replace `/path/to/your/image.jpg` with the actual path to your image file
- Returns the classification result

## Example Usage

1. First, check if the service is healthy:
```bash
./health-check.sh
```

2. Upload an image:
```bash
# Edit the script to point to your image
sed -i '' 's|/path/to/your/image.jpg|/actual/path/to/image.jpg|' upload-image.sh
./upload-image.sh
```

3. Classify an image:
```bash
# Edit the script to point to your image
sed -i '' 's|/path/to/your/image.jpg|/actual/path/to/image.jpg|' classify-image.sh
./classify-image.sh
```

## Response Format

### Health Check Response
```json
{
    "status": "ok",
    "timestamp": "2024-03-21T12:00:00.000Z"
}
```

### Upload Response
```json
{
    "success": true,
    "filename": "1234567890.jpg",
    "filepath": "/path/to/uploads/1234567890.jpg",
    "embedding": [...]
}
```

### Classification Response
```json
{
    "success": true,
    "classification": "Sample Classification"
}
``` 