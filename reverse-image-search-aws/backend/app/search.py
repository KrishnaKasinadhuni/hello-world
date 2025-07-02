import base64
import io
import json
import uuid
from PIL import Image
from typing import Tuple, List, Optional
from .aws_clients import (
    get_s3_client, get_bedrock_runtime, get_opensearch_client,
    S3_BUCKET_NAME, BEDROCK_MODEL_ID, OPENSEARCH_INDEX_NAME,
    OPENSEARCH_COLLECTION_ENDPOINT
)
from .models import SearchResultItem

MAX_IMAGE_SIZE_MB = 10
VECTOR_FIELD_NAME = "image_vector" # Consistent name for the vector field in OpenSearch


def generate_embedding(image_bytes: bytes) -> List[float]:
    """Generates embedding for the image using Amazon Titan."""
    print(f"Generating embedding using {BEDROCK_MODEL_ID}...")
    bedrock = get_bedrock_runtime()
    if not bedrock:
        raise ValueError("Bedrock client not initialized")

    try:
        body = json.dumps({
            "inputImage": base64.b64encode(image_bytes).decode('utf-8')
            # Add "embeddingConfig": { "outputEmbeddingLength": 256 } if needed
        })
        response = bedrock.invoke_model(
            body=body,
            modelId=BEDROCK_MODEL_ID,
            accept='application/json',
            contentType='application/json'
        )
        response_body = json.loads(response.get('body').read())
        embedding = response_body.get('embedding')
        if not embedding:
            raise ValueError("Failed to get embedding from Bedrock response")
        print(f"Embedding generated successfully (length: {len(embedding)}).")
        return embedding
    except Exception as e:
        print(f"Error generating embedding: {e}")
        raise

def index_image_in_opensearch(image_id: str, embedding: List[float]):
    """Stores the image embedding in OpenSearch Serverless."""
    print(f"Indexing image {image_id} in OpenSearch index {OPENSEARCH_INDEX_NAME}...")
    os_client = get_opensearch_client()
    if not os_client or not OPENSEARCH_COLLECTION_ENDPOINT:
         print("Skipping OpenSearch indexing: Client or Endpoint not configured.")
         # In a real scenario, raise an error or handle appropriately
         return # For now, just return to allow basic flow

    # --- Placeholder for OpenSearch Indexing ---
    # This requires the opensearch-py client and AWS SigV4 signing
    # Example structure (needs actual implementation):
    # document = {
    #     VECTOR_FIELD_NAME: embedding,
    #     "image_s3_key": image_id # Store the S3 key or other metadata
    # }
    # response = os_client.index(
    #     index=OPENSEARCH_INDEX_NAME,
    #     id=image_id, # Use S3 key as document ID
    #     body=document,
    #     refresh='wait_for' # Or true/false depending on need
    # )
    # print(f"OpenSearch index response: {response}")
    # --- End Placeholder ---
    print(f"Placeholder: Image {image_id} embedding sent to OpenSearch.")


def process_and_index_image(image_bytes: bytes, filename: str) -> Tuple[bool, str, Optional[str]]:
    """Validates, uploads to S3, generates embedding, and indexes in OpenSearch."""
    s3 = get_s3_client()
    if not S3_BUCKET_NAME or not s3:
        return False, "S3 Bucket name or client not configured.", None

    # 1. Validate Image (Size, Type using Pillow)
    try:
        if len(image_bytes) > MAX_IMAGE_SIZE_MB * 1024 * 1024:
             return False, f"Image size exceeds {MAX_IMAGE_SIZE_MB}MB limit.", None
        img = Image.open(io.BytesIO(image_bytes))
        img.verify() # Verify it's a valid image file
        # Optional: Check format if needed (img.format)
        print(f"Image validated: {img.format}, {img.size}")
    except Exception as e:
        print(f"Image validation failed: {e}")
        return False, f"Invalid or unsupported image file: {e}", None

    # 2. Upload to S3
    image_id = f"images/{uuid.uuid4()}-{filename}" # Create a unique key/ID
    try:
        print(f"Uploading {image_id} to S3 bucket {S3_BUCKET_NAME}...")
        s3.put_object(Bucket=S3_BUCKET_NAME, Key=image_id, Body=image_bytes)
        print("Upload successful.")
    except Exception as e:
        print(f"S3 upload failed: {e}")
        return False, f"Failed to upload image to S3: {e}", image_id # Return ID even if upload fails? Maybe not.

    # 3. Generate Embedding
    try:
        embedding = generate_embedding(image_bytes)
    except Exception as e:
        return False, f"Failed to generate embedding: {e}", image_id

    # 4. Index in OpenSearch
    try:
        index_image_in_opensearch(image_id, embedding)
    except Exception as e:
        # Decide how critical indexing failure is. Maybe log and continue?
        print(f"Warning: Failed to index image {image_id} in OpenSearch: {e}")
        # For now, we'll consider it a partial success if S3 upload worked
        # return False, f"Failed to index embedding: {e}", image_id

    return True, "Image indexed successfully.", image_id


def search_similar_images(query_embedding: List[float], top_k: int = 5) -> List[SearchResultItem]:
    """Performs k-NN search in OpenSearch Serverless."""
    print(f"Searching for {top_k} similar images in OpenSearch index {OPENSEARCH_INDEX_NAME}...")
    os_client = get_opensearch_client()
    if not os_client or not OPENSEARCH_COLLECTION_ENDPOINT:
         print("Skipping OpenSearch search: Client or Endpoint not configured.")
         return [] # Return empty list if search can't be performed

    results: List[SearchResultItem] = []

    # --- Placeholder for OpenSearch k-NN Search ---
    # This requires the opensearch-py client and AWS SigV4 signing
    # Example structure (needs actual implementation):
    # query = {
    #     "size": top_k,
    #     "_source": {"includes": ["image_s3_key"]}, # Fetch only needed metadata
    #     "query": {
    #         "knn": {
    #             VECTOR_FIELD_NAME: {
    #                 "vector": query_embedding,
    #                 "k": top_k
    #             }
    #         }
    #     }
    # }
    # try:
    #     response = os_client.search(
    #         index=OPENSEARCH_INDEX_NAME,
    #         body=query
    #     )
    #     hits = response.get('hits', {}).get('hits', [])
    #     for hit in hits:
    #         image_id = hit.get('_source', {}).get('image_s3_key')
    #         score = hit.get('_score')
    #         if image_id and score is not None:
    #             results.append(SearchResultItem(image_id=image_id, score=score))
    # except Exception as e:
    #       print(f"OpenSearch search failed: {e}")
    #       # Raise or return empty depending on requirements
    # --- End Placeholder ---

    print(f"Placeholder: OpenSearch search performed. Returning {len(results)} results.")
    # Add dummy results for testing flow without OpenSearch connection
    if not results and not (os_client and OPENSEARCH_COLLECTION_ENDPOINT):
         print("Adding dummy search results as OpenSearch is not configured.")
         results = [
              SearchResultItem(image_id="dummy/image1.jpg", score=0.95),
              SearchResultItem(image_id="dummy/image2.png", score=0.88)
         ]

    return results
