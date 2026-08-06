import os
from typing import Optional, Dict, Any, List
from fastapi import Request, HTTPException, Security, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from google.oauth2 import id_token
from google.auth.transport import requests as google_requests

security = HTTPBearer(auto_error=False)

DISABLE_AUTH = os.getenv("DISABLE_AUTH", "false").lower() in ("true", "1", "yes")
GOOGLE_CLIENT_ID = os.getenv("GOOGLE_CLIENT_ID", None)
GCP_PROJECT_ID = os.getenv("GCP_PROJECT", os.getenv("GOOGLE_CLOUD_PROJECT", "precise-works-456015-h9"))
SECRET_NAME = os.getenv("SECRET_NAME", "mcp-gateway-allowed-emails")
DEFAULT_ALLOWED_EMAIL = os.getenv("ALLOWED_EMAILS", "krishna.kasinadhuni@gmail.com")

def _get_allowed_emails() -> List[str]:
    """
    Dynamically fetches allowed user emails from GCP Secret Manager or environment config.
    Returns a lowercased list of authorized emails.
    """
    # 1. Try reading secret from GCP Secret Manager
    try:
        from google.cloud import secretmanager
        client = secretmanager.SecretManagerServiceClient()
        secret_path = f"projects/{GCP_PROJECT_ID}/secrets/{SECRET_NAME}/versions/latest"
        response = client.access_secret_version(request={"name": secret_path})
        secret_value = response.payload.data.decode("UTF-8").strip()
        if secret_value:
            return [e.strip().lower() for e in secret_value.split(",") if e.strip()]
    except Exception:
        pass

    # 2. Fallback to ALLOWED_EMAILS environment variable or default
    return [e.strip().lower() for e in DEFAULT_ALLOWED_EMAIL.split(",") if e.strip()]


async def verify_oauth_token(
    request: Request,
    credentials: Optional[HTTPAuthorizationCredentials] = Security(security)
) -> Dict[str, Any]:
    """
    Verifies Google OAuth 2.0 / OIDC ID Token from Bearer header or 'token' query param,
    and enforces dynamic GCP Secret Manager email whitelisting.
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

        # Enforce Email Whitelist (GCP Secret Manager / Environment)
        user_email = id_info.get("email", "").lower()
        allowed_emails = _get_allowed_emails()

        if allowed_emails and user_email not in allowed_emails:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail=f"Access denied. '{user_email}' is not authorized to access this MCP Gateway."
            )

        return id_info
    except HTTPException:
        raise
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
