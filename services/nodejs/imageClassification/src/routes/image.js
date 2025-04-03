const Joi = require('@hapi/joi');
const imageService = require('../services/imageService');
const config = require('../config/config');

const routes = [
    {
        method: 'GET',
        path: '/health',
        handler: async (request, h) => {
            return h.response({
                status: 'ok',
                timestamp: new Date().toISOString()
            });
        }
    },
    {
        method: 'POST',
        path: '/api/upload',
        options: {
            payload: {
                maxBytes: config.maxFileSize,
                output: 'stream',
                parse: true,
                multipart: true,
                allow: 'multipart/form-data'
            }
        },
        handler: async (request, h) => {
            try {
                const data = request.payload;
                if (!data.file) {
                    return h.response({
                        success: false,
                        error: 'No file uploaded'
                    }).code(400);
                }

                // Convert stream to buffer
                const chunks = [];
                for await (const chunk of data.file) {
                    chunks.push(chunk);
                }
                const imageBuffer = Buffer.concat(chunks);
                
                // Process image
                const processedImage = await imageService.processImage(imageBuffer);
                
                // Generate embedding
                const embedding = await imageService.generateEmbedding(processedImage);
                
                // Save image
                const filename = `${Date.now()}.jpg`;
                const filepath = await imageService.saveImage(processedImage, filename);
                
                return h.response({
                    success: true,
                    filename,
                    filepath,
                    embedding
                });
            } catch (error) {
                console.error('Upload error:', error);
                return h.response({
                    success: false,
                    error: error.message
                }).code(500);
            }
        }
    },
    {
        method: 'POST',
        path: '/api/classify',
        options: {
            payload: {
                maxBytes: config.maxFileSize,
                output: 'stream',
                parse: true,
                multipart: true,
                allow: 'multipart/form-data'
            }
        },
        handler: async (request, h) => {
            try {
                const data = request.payload;
                if (!data.file) {
                    return h.response({
                        success: false,
                        error: 'No file uploaded'
                    }).code(400);
                }

                // Convert stream to buffer
                const chunks = [];
                for await (const chunk of data.file) {
                    chunks.push(chunk);
                }
                const imageBuffer = Buffer.concat(chunks);
                
                // Process image
                const processedImage = await imageService.processImage(imageBuffer);
                
                // Classify the image
                const classification = await imageService.classifyImage(processedImage);
                
                return h.response({
                    success: true,
                    classification
                });
            } catch (error) {
                console.error('Classification error:', error);
                return h.response({
                    success: false,
                    error: error.message
                }).code(500);
            }
        }
    }
];

module.exports = routes; 