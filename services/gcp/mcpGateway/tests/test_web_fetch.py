import pytest
from unittest.mock import patch, MagicMock
from tools.web_fetch import fetch_web_markdown

@pytest.mark.asyncio
async def test_fetch_web_markdown_success():
    sample_html = """
    <!DOCTYPE html>
    <html>
      <head><title>Test MCP Page</title></head>
      <body>
        <nav>Navigation Menu</nav>
        <h1>Welcome to MCP Gateway</h1>
        <p>This is a high-speed HTML to Markdown converter.</p>
        <script>console.log("ignore me");</script>
      </body>
    </html>
    """
    
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.text = sample_html
    mock_response.raise_for_status = MagicMock()

    with patch("httpx.AsyncClient.get", return_value=mock_response):
        result = await fetch_web_markdown("https://example.com/test", max_chars=500)

    assert result["status_code"] == 200
    assert result["title"] == "Test MCP Page"
    assert "Welcome to MCP Gateway" in result["markdown"]
    assert "Navigation Menu" not in result["markdown"]  # Nav tag stripped
    assert "console.log" not in result["markdown"]     # Script tag stripped
    assert result["truncated"] is False


@pytest.mark.asyncio
async def test_fetch_web_markdown_truncation():
    sample_html = "<html><head><title>Long Page</title></head><body>" + "<p>Word </p>" * 100 + "</body></html>"
    
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.text = sample_html
    mock_response.raise_for_status = MagicMock()

    with patch("httpx.AsyncClient.get", return_value=mock_response):
        result = await fetch_web_markdown("https://example.com/long", max_chars=50)

    assert result["truncated"] is True
    assert "...[Truncated due to length]" in result["markdown"]


@pytest.mark.asyncio
async def test_fetch_web_markdown_error_handling():
    with patch("httpx.AsyncClient.get", side_effect=Exception("Connection refused")):
        result = await fetch_web_markdown("https://invalid-domain-does-not-exist.com")

    assert result["status"] == "failed"
    assert "error" in result
    assert "Connection refused" in result["error"]
