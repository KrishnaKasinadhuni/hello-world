from pydantic_settings import BaseSettings
from typing import List
import os
from dotenv import load_dotenv

load_dotenv()

class Settings(BaseSettings):
    # API Settings
    API_V1_STR: str = "/api/v1"
    PROJECT_NAME: str = "Log Analyzer Service"
    
    # Server Settings
    HOST: str = "0.0.0.0"
    PORT: int = 8000
    
    # Log Analysis Settings
    MAX_LOG_SIZE: int = 10 * 1024 * 1024  # 10MB
    SUPPORTED_LOG_FORMATS: List[str] = [".log", ".txt"]
    ERROR_PATTERNS: List[str] = [
        r"ERROR",
        r"FATAL",
        r"EXCEPTION",
        r"FAILED",
        r"CRITICAL",
        r"WARNING"
    ]
    
    # Analysis Settings
    MIN_ERROR_COUNT: int = 3
    TIME_WINDOW_HOURS: int = 24
    SIMILARITY_THRESHOLD: float = 0.8
    
    # Storage Settings
    ANALYSIS_RESULTS_DIR: str = "analysis_results"
    
    class Config:
        case_sensitive = True
        env_file = ".env"

settings = Settings() 