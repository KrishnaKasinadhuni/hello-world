#!/bin/bash

# Health check endpoint
curl -X GET \
  http://localhost:3000/health \
  -H 'Content-Type: application/json' 