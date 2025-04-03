# AI Image Classification Service

This service provides AI-powered image processing capabilities using Node.js, Hapi server, and Cohere's AI models. It can process images, generate embeddings, perform image classification, and find similar images.

## Features

### 1. Image Upload and Processing
- Upload images through a REST API endpoint
- Automatic image processing and optimization
- Support for JPEG, PNG, and JPG formats
- Configurable image size and quality settings

### 2. Image Classification
- AI-powered image classification using Cohere's models
- Returns a brief description of the image content
- Optimized image processing for accurate classification
- Handles various image sizes and formats

### 3. Similar Image Search
- Find similar images based on visual similarity
- Uses Cohere's embedding models for image comparison
- Returns top 5 most similar images with similarity scores
- Supports batch processing of multiple images

### 4. Health Monitoring
- Health check endpoint for service monitoring
- Timestamp-based status reporting
- Easy integration with monitoring systems

## Prerequisites

- Node.js (v18.17.0 or higher)
- Cohere API key
- Docker (optional, for containerized deployment)

## Setup

1. Clone the repository
2. Install dependencies:
   ```bash
   npm install
   ```
3. Create a `.env` file in the root directory with your Cohere API key:
   ```
   COHERE_API_KEY=your_api_key_here
   PORT=3000
   ```

## Running the Service

### Development Mode
```bash
npm run dev
```

### Production Mode
```bash
npm start
```

## API Endpoints

### 1. Health Check
```
GET /health
```
Returns service status and timestamp.

### 2. Upload Image
```
POST /api/upload
Content-Type: multipart/form-data
```
Uploads an image and returns:
- Processed image filename
- File path
- Generated embedding

### 3. Classify Image
```
POST /api/classify
Content-Type: multipart/form-data
```
Classifies an uploaded image and returns:
- Classification result (text description)

### 4. Similar Image Search
```
POST /api/similar
Content-Type: multipart/form-data
```
Finds similar images and returns:
- List of similar images (top 5)
- Similarity scores
- File paths

## Testing

### Using Test Scripts
1. Upload test:
   ```bash
   node test-images/upload-test.js /path/to/image.jpg
   ```

2. Classification test:
   ```bash
   node test-images/classify-test.js /path/to/image.jpg
   ```

3. Similarity search test:
   ```bash
   node test-images/similar-test.js /path/to/image.jpg
   ```

### Using Curl Scripts
1. Upload:
   ```bash
   ./curl-requests/upload-image.sh /path/to/image.jpg
   ```

2. Classify:
   ```bash
   ./curl-requests/classify-image.sh /path/to/image.jpg
   ```

3. Similarity search:
   ```bash
   ./curl-requests/similar-search.sh /path/to/image.jpg
   ```

## Configuration

The service can be configured through environment variables:

- `COHERE_API_KEY`: Your Cohere API key
- `PORT`: Server port (default: 3000)
- `MAX_FILE_SIZE`: Maximum file size (default: 5MB)
- `UPLOAD_DIR`: Directory for storing uploaded images

## Error Handling

The service includes comprehensive error handling for:
- Invalid file uploads
- Processing errors
- API failures
- File system errors

## Security

- File size limits
- File type validation
- Secure file storage
- Environment variable protection

## Performance Considerations

- Image optimization for processing
- Efficient embedding generation
- Optimized similarity search
- Caching support (planned)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

MIT License 