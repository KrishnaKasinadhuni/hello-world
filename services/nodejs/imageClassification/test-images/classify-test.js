const fs = require('fs');
const FormData = require('form-data');
const axios = require('axios');

async function classifyImage(imagePath) {
    try {
        // Create form data
        const formData = new FormData();
        formData.append('file', fs.createReadStream(imagePath));

        // Make request to classify endpoint
        const response = await axios.post('http://localhost:3000/api/classify', formData, {
            headers: {
                ...formData.getHeaders()
            }
        });

        // Log the response
        console.log('Classification Response:', JSON.stringify(response.data, null, 2));
        return response.data;
    } catch (error) {
        console.error('Error:', error.response?.data || error.message);
        throw error;
    }
}

// Get image path from command line argument or use default test image
const imagePath = process.argv[2] || 'test.jpg';

// Run the classification
classifyImage(imagePath)
    .then(result => {
        console.log('\nClassification completed successfully!');
        process.exit(0);
    })
    .catch(error => {
        console.error('\nClassification failed:', error.message);
        process.exit(1);
    }); 