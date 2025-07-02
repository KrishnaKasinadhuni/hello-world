from pydantic import BaseModel
from typing import List, Optional

class IndexResponse(BaseModel):
    success: bool
    message: str
    image_id: Optional[str] = None
    error: Optional[str] = None

class SearchResultItem(BaseModel):
    image_id: str
    score: float # Similarity score

class SearchResponse(BaseModel):
    success: bool
    results: List[SearchResultItem] = []
    error: Optional[str] = None

class ExtractedObject(BaseModel):
    label: str
    confidence: float
    box: List[float] # [left, top, width, height]

class ExtractResponse(BaseModel):
    success: bool
    objects: List[ExtractedObject] = []
    error: Optional[str] = None
