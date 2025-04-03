require('dotenv').config();

const config = {
    port: process.env.PORT || 3000,
    cohereApiKey: process.env.COHERE_API_KEY,
    uploadDir: 'uploads',
    maxFileSize: 5 * 1024 * 1024, // 5MB
    allowedMimeTypes: ['image/jpeg', 'image/png', 'image/jpg'],
    imageProcessing: {
        maxWidth: 800,
        maxHeight: 800,
        quality: 80
    }
};

// Log configuration (excluding sensitive data)
console.log('Configuration loaded:', {
    ...config,
    cohereApiKey: config.cohereApiKey ? 'API Key is set' : 'API Key is missing'
});

module.exports = config; 