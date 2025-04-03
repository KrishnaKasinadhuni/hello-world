#!/bin/bash

# Image upload endpoint
curl -X POST \
  http://localhost:3000/api/upload \
  -F 'file=@../test-images/test.jpg' 