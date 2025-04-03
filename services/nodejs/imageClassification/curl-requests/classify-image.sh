#!/bin/bash

# Image classification endpoint
curl -X POST \
  http://localhost:3000/api/classify \
  -H 'Content-Type: multipart/form-data' \
  -F 'image=@/path/to/your/image.jpg' 