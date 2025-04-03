const FormData = require('form-data');
const fs = require('fs');
const path = require('path');
const axios = require('axios');

async function testSimilaritySearch(imagePath) {
    try {
        // Create form data
        const form = new FormData();
        form.append('file', fs.createReadStream(imagePath));

        // Make request to similarity search endpoint
        const response = await axios.post('http://localhost:3000/api/similar', form, {
            headers: {
                ...form.getHeaders()
            }
        });

        console.log('Similarity Search Results:');
        console.log(JSON.stringify(response.data, null, 2));
    } catch (error) {
        console.error('Error:', error.response?.data || error.message);
    }
}

// Get image path from command line argument
const imagePath = process.argv[2];
if (!imagePath) {
    console.error('Please provide an image path as an argument');
    process.exit(1);
}

testSimilaritySearch(imagePath); 