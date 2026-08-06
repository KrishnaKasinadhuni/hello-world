import os
from typing import Optional, Dict, Any
from fastapi import Request, HTTPException, Security, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from google.oauth2 import id_token
from google.auth.transport import requests as google_requests

security = HTTPBearer(auto_error=False)

DISABLE_AUTH = os.getenv("DISABLE_AUTH", "false").lower() in ("true", "1", "yes")
GOOGLE_CLIENT_ID = os.getenv("GOOGLE_CLIENT_ID", None)

async def verify_oauth_token(
    request: Request,
    credentials: Optional[HTTPAuthorizationCredentials] = Security(security)
) -> Dict[str, Any]:
    """
    Verifies Google OAuth 2.0 / OIDC ID Token from Bearer header or 'token' query param.
    """
    if DISABLE_AUTH:
        return {"email": "dev-user@example.com", "sub": "dev-user-id", "name": "Local Dev User"}

    token: Optional[str] = None

    # Check Authorization: Bearer <token>
    if credentials and credentials.credentials:
        token = credentials.credentials
    # Fallback for SSE / EventSource query parameter
    elif "token" in request.query_params:
        token = request.query_params["token"]

    if not token:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing Authorization bearer token or 'token' query parameter.",
            headers={"WWW-Authenticate": "Bearer"},
        )

    try:
        # Verify Google OIDC ID Token signature & audience
        request_adapter = google_requests.Request()
        id_info = id_token.verify_oauth2_token(
            token,
            request_adapter,
            audience=GOOGLE_CLIENT_ID
        )

        # Check issuer
        if id_info.get("iss") not in ["accounts.google.com", "https://accounts.google.com"]:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid token issuer.",
            )

        return id_info
    except ValueError as e:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=f"Invalid OAuth token: {str(e)}",
            headers={"WWW-Authenticate": "Bearer"},
        )
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Token verification error: {str(e)}",
        )
