const sharp = require('sharp');
const path = require('path');

// Create a simple 100x100 white image
sharp({
    create: {
        width: 100,
        height: 100,
        channels: 4,
        background: { r: 255, g: 255, b: 255, alpha: 1 }
    }
})
.jpeg()
.toFile(path.join(__dirname, 'test.jpg'))
.then(() => {
    console.log('Test image created successfully!');
})
.catch(err => {
    console.error('Error creating test image:', err);
}); 