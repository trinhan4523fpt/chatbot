"""Service-to-service authentication via a shared internal key."""
from __future__ import annotations

from fastapi import Depends, Header, HTTPException, status

from .config import Settings, get_settings


async def require_internal_key(
    x_internal_key: str | None = Header(default=None, alias="X-Internal-Key"),
    settings: Settings = Depends(get_settings),
) -> None:
    """Reject calls that lack the shared internal key.

    If no key is configured (pure local dev), the check is skipped. In any deployed
    environment INTERNAL_API_KEY must be set and the :8000 port must not be published.
    """
    if not settings.internal_api_key:
        return
    if x_internal_key != settings.internal_api_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing X-Internal-Key",
        )
