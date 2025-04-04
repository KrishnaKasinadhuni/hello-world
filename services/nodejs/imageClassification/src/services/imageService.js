const sharp = require('sharp');
const { CohereClient } = require('cohere-ai');
const config = require('../config/config');
const fs = require('fs').promises;
const path = require('path');

// Initialize Cohere client with API key
if (!config.cohereApiKey) {
    throw new Error('COHERE_API_KEY environment variable is not set');
}

const cohereClient = new CohereClient({
    token: config.cohereApiKey
});

class ImageService {
    async processImage(imageBuffer) {
        try {
            // Process image with Sharp
            const processedImage = await sharp(imageBuffer)
                .resize(config.imageProcessing.maxWidth, config.imageProcessing.maxHeight, {
                    fit: 'inside',
                    withoutEnlargement: true
                })
                .jpeg({ quality: config.imageProcessing.quality })
                .toBuffer();

            return processedImage;
        } catch (error) {
            throw new Error(`Image processing failed: ${error.message}`);
        }
    }

    async generateEmbedding(imageBuffer) {
        try {
            // Convert image buffer to base64
            const base64Image = imageBuffer.toString('base64');

            // Generate embedding using Cohere
            const response = await cohereClient.embed({
                texts: [base64Image],
                model: 'multilingual-22-12',
                inputType: 'image'
            });

            // The embeddings should be directly in the response
            return response.embeddings[0];
        } catch (error) {
            console.error('Cohere API Error:', error);
            throw new Error(`Embedding generation failed: ${error.message}`);
        }
    }

    async classifyImage(imageBuffer) {
        try {
            // Process image in multiple steps to reduce size while maintaining recognizability
            const resizedImage = await sharp(imageBuffer)
                // First resize to target dimensions
                .resize(128, 128, {
                    fit: 'inside',
                    withoutEnlargement: true
                })
                // Convert to grayscale to reduce size
                .grayscale()
                // Adjust contrast to maintain important features
                .linear(1.3, -0.1)  // Increase contrast
                // Convert to WebP for better compression
                .webp({ 
                    quality: 35,
                    effort: 6,
                    lossless: false,
                    nearLossless: false,
                    smartSubsample: true,
                    reductionEffort: 6
                })
                .toBuffer();

            // Convert resized image buffer to base64
            const base64Image = resizedImage.toString('base64');

            // Use Cohere's chat API to classify the image
            const response = await cohereClient.chat({
                message: "What is in this image? One brief sentence.",
                model: "command",
                documents: [{
                    text: base64Image,
                    type: "image"
                }]
            });

            // Clean up the response text by removing any base64 data
            const cleanText = response.text.split('\n')[0].trim();
            return cleanText;
        } catch (error) {
            console.error('Cohere API Error:', error);
            throw new Error(`Image classification failed: ${error.message}`);
        }
    }

    async saveImage(imageBuffer, filename) {
        try {
            const uploadDir = path.join(process.cwd(), config.uploadDir);
            await fs.mkdir(uploadDir, { recursive: true });
            
            const filepath = path.join(uploadDir, filename);
            await fs.writeFile(filepath, imageBuffer);
            
            return filepath;
        } catch (error) {
            throw new Error(`Failed to save image: ${error.message}`);
        }
    }

    async findSimilarImages(queryEmbedding) {
        try {
            // Get all files from the uploads directory
            const uploadDir = path.join(process.cwd(), config.uploadDir);
            const files = await fs.readdir(uploadDir);
            
            // Filter for image files and get their embeddings
            const imageFiles = files.filter(file => 
                config.allowedMimeTypes.includes(path.extname(file).toLowerCase())
            );

            const similarImages = [];
            
            // Compare each image's embedding with the query embedding
            for (const file of imageFiles) {
                const filePath = path.join(uploadDir, file);
                const imageBuffer = await fs.readFile(filePath);
                const embedding = await this.generateEmbedding(imageBuffer);
                
                // Calculate cosine similarity
                const similarity = this.cosineSimilarity(queryEmbedding, embedding);
                
                similarImages.push({
                    filename: file,
                    filepath: filePath,
                    similarity: similarity
                });
            }
            
            // Sort by similarity (highest first) and return top 5
            return similarImages
                .sort((a, b) => b.similarity - a.similarity)
                .slice(0, 5);
        } catch (error) {
            throw new Error(`Failed to find similar images: ${error.message}`);
        }
    }

    cosineSimilarity(vec1, vec2) {
        // Calculate cosine similarity between two vectors
        const dotProduct = vec1.reduce((sum, val, i) => sum + val * vec2[i], 0);
        const norm1 = Math.sqrt(vec1.reduce((sum, val) => sum + val * val, 0));
        const norm2 = Math.sqrt(vec2.reduce((sum, val) => sum + val * val, 0));
        return dotProduct / (norm1 * norm2);
    }
}

module.exports = new ImageService(); 