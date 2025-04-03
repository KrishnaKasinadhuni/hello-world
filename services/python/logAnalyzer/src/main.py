from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from typing import Optional
import uvicorn
from pathlib import Path
import os
from datetime import datetime

from config.config import settings
from services.log_analyzer import LogAnalyzer

app = FastAPI(
    title=settings.PROJECT_NAME,
    openapi_url=f"{settings.API_V1_STR}/openapi.json"
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize log analyzer
log_analyzer = LogAnalyzer(settings.ERROR_PATTERNS)

@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat()
    }

@app.post(f"{settings.API_V1_STR}/analyze")
async def analyze_logs(
    file: UploadFile = File(...),
    save_results: bool = True
):
    """
    Analyze log file and provide insights.
    
    Args:
        file: The log file to analyze
        save_results: Whether to save analysis results to file
    
    Returns:
        Analysis results including error patterns and recommendations
    """
    # Validate file size
    file_size = 0
    content = ""
    for chunk in file.stream():
        file_size += len(chunk)
        content += chunk.decode('utf-8')
        if file_size > settings.MAX_LOG_SIZE:
            raise HTTPException(
                status_code=400,
                detail="File size exceeds maximum allowed size"
            )
    
    # Validate file extension
    file_extension = Path(file.filename).suffix.lower()
    if file_extension not in settings.SUPPORTED_LOG_FORMATS:
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported file format. Supported formats: {settings.SUPPORTED_LOG_FORMATS}"
        )
    
    try:
        # Parse and analyze logs
        df = log_analyzer.parse_log_file(content)
        results = log_analyzer.analyze_errors(df)
        
        # Save results if requested
        if save_results:
            output_file = log_analyzer.save_analysis_results(
                results,
                settings.ANALYSIS_RESULTS_DIR
            )
            results['output_file'] = output_file
        
        return results
        
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Error analyzing logs: {str(e)}"
        )

@app.get(f"{settings.API_V1_STR}/analysis-results")
async def get_analysis_results():
    """Get list of available analysis results."""
    results_dir = Path(settings.ANALYSIS_RESULTS_DIR)
    if not results_dir.exists():
        return []
    
    results = []
    for file in results_dir.glob("analysis_*.json"):
        results.append({
            "filename": file.name,
            "created_at": datetime.fromtimestamp(file.stat().st_mtime).isoformat(),
            "path": str(file)
        })
    
    return results

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=True
    ) 