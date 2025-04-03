#!/bin/bash

# Check if image path is provided
if [ -z "$1" ]; then
    echo "Usage: $0 <path_to_image>"
    exit 1
fi

# Check if file exists
if [ ! -f "$1" ]; then
    echo "Error: File $1 does not exist"
    exit 1
fi

# Make the curl request
curl -X POST \
  -H "Content-Type: multipart/form-data" \
  -F "file=@$1" \
  http://localhost:3000/api/similar 