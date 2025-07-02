#!/bin/bash

# Set AWS region
export AWS_REGION=us-east-2

# Get API key from AWS Secrets Manager
API_KEY=$(aws secretsmanager get-secret-value --secret-id "dev/name" --query "SecretString" --output text)

# check if API_KEY is empty
if [ -z "$API_KEY" ]; then
  echo "API_KEY is empty"
  exit 1
fi

# check if API_KEY exists in the environment
if [ -z "$API_KEY" ]; then
  echo "API_KEY is not set in the environment"
  exit 1
fi
# print API_KEY
echo "API_KEY: $API_KEY"
response=$(curl -X GET 'https://9se90ih4xg.execute-api.us-east-2.amazonaws.com/dev/?hello=world' \
  -H 'content-type: application/json' \
  -H "X-API-KEY: $API_KEY")

# print the response
echo "Response:"
echo "$response"



