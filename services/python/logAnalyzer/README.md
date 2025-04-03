# Log Analyzer Service

A Python-based service that analyzes log files to identify error patterns, generate insights, and provide recommendations for resolving issues.

## Features

### 1. Log Analysis
- Parse and analyze log files in various formats
- Identify error patterns and anomalies
- Generate statistical insights
- Support for multiple log formats

### 2. Error Pattern Detection
- Automatic detection of common error patterns
- Group similar error messages
- Calculate error frequencies
- Time-based pattern analysis

### 3. Recommendations
- Generate actionable recommendations
- Identify recurring issues
- Suggest potential solutions
- Time-based insights

### 4. Results Management
- Save analysis results
- Retrieve historical analyses
- Export results in JSON format
- Persistent storage of insights

## Prerequisites

- Python 3.11 or higher
- Docker (optional, for containerized deployment)
- Docker Compose (optional, for containerized deployment)

## Setup

### Local Development

1. Clone the repository
2. Create a virtual environment:
   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   ```
3. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```
4. Create a `.env` file:
   ```
   PORT=8000
   MAX_LOG_SIZE=10485760  # 10MB
   ```

### Docker Deployment

1. Build the image:
   ```bash
   docker build -t log-analyzer-service .
   ```

2. Run with Docker Compose:
   ```bash
   docker-compose up -d
   ```

## API Endpoints

### 1. Health Check
```
GET /health
```
Returns service status and timestamp.

### 2. Analyze Logs
```
POST /api/v1/analyze
Content-Type: multipart/form-data
```
Analyzes a log file and returns:
- Error patterns
- Statistics
- Recommendations
- Analysis results file path (optional)

### 3. Get Analysis Results
```
GET /api/v1/analysis-results
```
Returns list of available analysis results.

## Usage Examples

### Using curl

1. Analyze a log file:
   ```bash
   curl -X POST \
     -F "file=@/path/to/your/logs.log" \
     http://localhost:8000/api/v1/analyze
   ```

2. Get analysis results:
   ```bash
   curl http://localhost:8000/api/v1/analysis-results
   ```

### Using Python

```python
import requests

# Analyze logs
with open('logs.log', 'rb') as f:
    response = requests.post(
        'http://localhost:8000/api/v1/analyze',
        files={'file': f}
    )
    print(response.json())

# Get analysis results
response = requests.get('http://localhost:8000/api/v1/analysis-results')
print(response.json())
```

## Configuration

The service can be configured through environment variables:

- `PORT`: Server port (default: 8000)
- `MAX_LOG_SIZE`: Maximum log file size (default: 10MB)
- `SUPPORTED_LOG_FORMATS`: List of supported log formats
- `ERROR_PATTERNS`: List of error patterns to detect
- `MIN_ERROR_COUNT`: Minimum count for error pattern detection
- `TIME_WINDOW_HOURS`: Time window for analysis
- `SIMILARITY_THRESHOLD`: Threshold for similar error detection
- `ANALYSIS_RESULTS_DIR`: Directory for storing analysis results

## Analysis Features

### Error Pattern Detection
- Identifies common error patterns
- Groups similar error messages
- Calculates error frequencies
- Provides pattern-based recommendations

### Time-based Analysis
- Analyzes error patterns over time
- Identifies peak error periods
- Suggests time-based solutions
- Tracks error trends

### Similarity Analysis
- Uses TF-IDF vectorization
- Calculates cosine similarity
- Groups similar error messages
- Identifies recurring issues

## Development

### Running Tests
```bash
pytest tests/
```

### Code Style
```bash
# Format code
black src/

# Check types
mypy src/

# Lint code
flake8 src/
```

## Docker Commands

### Build and Run
```bash
# Build image
docker build -t log-analyzer-service .

# Run container
docker run -d \
  -p 8000:8000 \
  -v $(pwd)/analysis_results:/app/analysis_results \
  log-analyzer-service
```

### Using Docker Compose
```bash
# Start service
docker-compose up -d

# Stop service
docker-compose down

# View logs
docker-compose logs -f
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

MIT License 