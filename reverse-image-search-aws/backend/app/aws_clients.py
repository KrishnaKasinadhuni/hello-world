import boto3
import os
from dotenv import load_dotenv

load_dotenv() # Load environment variables from .env file

AWS_REGION = os.getenv("AWS_REGION", "us-east-1")
BEDROCK_MODEL_ID = os.getenv("BEDROCK_MODEL_ID", "amazon.titan-embed-image-v1")
OPENSEARCH_COLLECTION_ENDPOINT = os.getenv("OPENSEARCH_COLLECTION_ENDPOINT")
OPENSEARCH_INDEX_NAME = os.getenv("OPENSEARCH_INDEX_NAME", "reverse-image-index")
S3_BUCKET_NAME = os.getenv("S3_BUCKET_NAME")
REKOGNITION_CONFIDENCE_THRESHOLD = float(os.getenv("REKOGNITION_CONFIDENCE_THRESHOLD", "75.0"))

# Initialize clients (will add session/credentials later)
# For now, basic initialization to avoid errors
s3_client = None
bedrock_runtime = None
opensearch_client = None
rekognition_client = None

def get_s3_client():
    global s3_client
    if s3_client is None:
        print("Initializing S3 client...")
        # Replace with actual initialization using credentials/session
        s3_client = boto3.client('s3', region_name=AWS_REGION)
    return s3_client

def get_bedrock_runtime():
    global bedrock_runtime
    if bedrock_runtime is None:
        print("Initializing Bedrock Runtime client...")
        # Replace with actual initialization
        bedrock_runtime = boto3.client('bedrock-runtime', region_name=AWS_REGION)
    return bedrock_runtime

def get_opensearch_client():
    # Placeholder for OpenSearch client initialization
    # This will require more complex setup (e.g., sigv4 signing)
    global opensearch_client
    if opensearch_client is None:
        print("Initializing OpenSearch client (placeholder)...")
        # Needs proper initialization with endpoint and credentials/sigv4
        pass
    return opensearch_client

def get_rekognition_client():
    global rekognition_client
    if rekognition_client is None:
        print("Initializing Rekognition client...")
        # Replace with actual initialization
        rekognition_client = boto3.client('rekognition', region_name=AWS_REGION)
    return rekognition_client

print(f"AWS Region: {AWS_REGION}")
print(f"Bedrock Model ID: {BEDROCK_MODEL_ID}")
print(f"OpenSearch Endpoint: {OPENSEARCH_COLLECTION_ENDPOINT}")
print(f"OpenSearch Index: {OPENSEARCH_INDEX_NAME}")
print(f"S3 Bucket: {S3_BUCKET_NAME}")
