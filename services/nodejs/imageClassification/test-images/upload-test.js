const axios = require('axios');
const fs = require('fs');
const path = require('path');
const FormData = require('form-data');

const filePath = path.join(__dirname, 'test.jpg');
const form = new FormData();
form.append('file', fs.createReadStream(filePath));

axios.post('http://localhost:3000/api/upload', form, {
    headers: {
        ...form.getHeaders(),
        'Content-Type': 'multipart/form-data'
    }
})
.then(response => {
    console.log('Response:', response.data);
})
.catch(error => {
    console.error('Error:', error.response ? error.response.data : error.message);
}); 