import os
import io
from fastapi import FastAPI, File, UploadFile, HTTPException, Form
from fastapi.responses import JSONResponse
from dotenv import load_dotenv
from typing import List, Optional

# Load environment variables early
load_dotenv()

from app.models import IndexResponse, SearchResponse, ExtractResponse # noqa E402
from app.search import process_and_index_image, generate_embedding, search_similar_images # noqa E402
# Import object extraction function when implemented
# from app.object_detection import extract_objects_rekognition # noqa E402

app = FastAPI(
    title="Reverse Image Search API",
    description="API for indexing and searching images using AWS Bedrock and OpenSearch.",
    version="0.1.0",
)

# Load host and port from environment variables
BACKEND_HOST = os.getenv("BACKEND_HOST", "0.0.0.0")
# Ensure port is an integer
try:
    BACKEND_PORT = int(os.getenv("BACKEND_PORT", "8000"))
except ValueError:
    print("Warning: Invalid BACKEND_PORT in .env, using default 8000.")
    BACKEND_PORT = 8000


@app.get("/", tags=["Health"])
async def read_root():
    """Health check endpoint."""
    return {"status": "ok", "message": "Reverse Image Search API is running."}


@app.post("/index", response_model=IndexResponse, tags=["Indexing"])
async def index_image_endpoint(file: UploadFile = File(..., description="Image file to index")):
    """
    Indexes an uploaded image.
    - Uploads to S3.
    - Generates embedding using Bedrock Titan.
    - Stores embedding in OpenSearch Serverless.
    """
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Invalid file type. Please upload an image.")

    try:
        image_bytes = await file.read()
        success, message, image_id = process_and_index_image(image_bytes, file.filename or "uploaded_image")

        if success:
            return IndexResponse(success=True, message=message, image_id=image_id)
        else:
            # Use 500 for indexing failures, could refine based on error type
            return JSONResponse(
                status_code=500,
                content=IndexResponse(success=False, message="Indexing failed", error=message).model_dump(exclude_none=True)
            )
    except Exception as e:
        print(f"Error during indexing endpoint: {e}")
        raise HTTPException(status_code=500, detail=f"An unexpected error occurred during indexing: {e}")
    finally:
        await file.close()


@app.post("/search", response_model=SearchResponse, tags=["Search"])
async def search_endpoint(
    file: Optional[UploadFile] = File(None, description="Query image file (required if embedding not provided)."),
    embedding: Optional[str] = Form(None, description="Base64 encoded query embedding (alternative to file)."),
    top_k: int = Form(5, description="Number of similar images to return.")
    ):
    """
    Searches for similar images using a query image or a pre-computed embedding.
    """
    query_embedding: List[float]

    if file:
        if not file.content_type.startswith("image/"):
             raise HTTPException(status_code=400, detail="Invalid file type for query image.")
        try:
            image_bytes = await file.read()
            # TODO: Add validation for query image size if necessary
            query_embedding = generate_embedding(image_bytes)
        except Exception as e:
            print(f"Error generating embedding for query image: {e}")
            raise HTTPException(status_code=500, detail=f"Failed to process query image: {e}")
        finally:
            await file.close()
    elif embedding:
         # TODO: Add proper decoding and validation for provided embedding
         print("Received pre-computed embedding (validation pending).")
         # query_embedding = decode_and_validate_embedding(embedding) # Implement this
         raise HTTPException(status_code=501, detail="Using pre-computed embeddings is not yet implemented.")
    else:
        raise HTTPException(status_code=400, detail="Either a query image file or an embedding must be provided.")

    try:
        results = search_similar_images(query_embedding, top_k=top_k)
        return SearchResponse(success=True, results=results)
    except Exception as e:
        print(f"Error during search: {e}")
        return JSONResponse(
             status_code=500,
             content=SearchResponse(success=False, error=f"Search failed: {e}").model_dump(exclude_none=True)
        )


# Optional: Add Rekognition object detection endpoint later
@app.post("/extract", response_model=ExtractResponse, tags=["Object Detection"])
async def extract_objects_endpoint(file: UploadFile = File(..., description="Image file to extract objects from.")):
     """
     (Placeholder) Detects objects in an image using Rekognition.
     """
     if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Invalid file type. Please upload an image.")

     # image_bytes = await file.read()
     # objects = extract_objects_rekognition(image_bytes) # Implement this
     # await file.close()
     # return ExtractResponse(success=True, objects=objects)
     await file.close()
     raise HTTPException(status_code=501, detail="Object extraction endpoint not yet implemented.")


# --- Main Execution ---
if __name__ == "__main__":
    import uvicorn
    print(f"Starting Uvicorn server on {BACKEND_HOST}:{BACKEND_PORT}...")
    uvicorn.run(
        "main:app", 
        host=BACKEND_HOST, 
        port=BACKEND_PORT, 
        reload=True,
        reload_excludes=["venv", ".vscode", ".git", "__pycache__"]
    )
