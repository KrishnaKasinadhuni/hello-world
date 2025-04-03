# AI Image Classification Service

This service provides image classification capabilities using Node.js, Hapi server, and Cohere's AI models. It can process images, generate embeddings, and perform image classification and similarity search.

## Features

- Image upload and processing
- Image embedding generation using Cohere
- Image classification
- Similar image search
- Vector database storage using Cohere

## Prerequisites

- Node.js (v14 or higher)
- Cohere API key

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

Development mode:
```bash
npm run dev
```

Production mode:
```bash
npm start
```

## API Endpoints

### POST /api/upload
Upload an image for classification

### POST /api/classify
Classify an uploaded image

### GET /api/similar
Find similar images based on a reference image

## Environment Variables

- `COHERE_API_KEY`: Your Cohere API key
- `PORT`: Server port (default: 3000) 