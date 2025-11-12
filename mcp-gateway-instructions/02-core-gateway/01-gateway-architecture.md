# 01: Gateway Architecture

## Objective

Design and document the architecture of the MCP Gateway, including system components, data flow, and interaction patterns between the gateway, MCP servers, and clients.

## Prerequisites

- Completed: 01-setup/01-project-structure.md
- Completed: 01-setup/02-dependencies.md
- Completed: 01-setup/03-docker-base-config.md
- Understanding of gateway patterns
- Knowledge of MCP (Model Context Protocol) specification

## Architecture Overview

### System Components

The MCP Gateway consists of the following main components:

1. **API Gateway**: Main entry point for all client requests
2. **Authentication Service**: Handles user authentication and authorization
3. **MCP Server Registry**: Manages registration and discovery of MCP servers
4. **Request Router**: Routes requests to appropriate MCP servers
5. **Connection Manager**: Manages WebSocket and HTTP connections
6. **Security Layer**: Enforces security policies and rate limiting
7. **Audit Logger**: Logs all activities for monitoring and compliance

### Architecture Diagram

```
                    ┌─────────────┐
                    │   Clients   │
                    │  (Browsers, │
                    │   Apps, AI) │
                    └──────┬──────┘
                           │
                           │ HTTPS/WSS
                           │
                    ┌──────▼─────────────────┐
                    │   Nginx Reverse Proxy  │
                    │  (TLS Termination)     │
                    └──────┬─────────────────┘
                           │
                           │ HTTP/WS
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼────────┐  ┌──────▼────────┐  ┌─────▼─────────┐
│  API Gateway   │  │   Auth        │  │  Rate Limiter │
│  (FastAPI)     │  │   Service     │  │  (Redis)      │
└───────┬────────┘  └──────┬────────┘  └─────┬─────────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼────────┐  ┌──────▼────────┐  ┌─────▼─────────┐
│ MCP Registry   │  │   Router      │  │  Connection   │
│  (PostgreSQL)  │  │               │  │  Manager      │
└───────┬────────┘  └──────┬────────┘  └─────┬─────────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼────────┐  ┌──────▼────────┐  ┌─────▼─────────┐
│  MCP Server 1  │  │  MCP Server 2 │  │  MCP Server N │
│  (Isolated)    │  │  (Isolated)   │  │  (Isolated)   │
└────────────────┘  └───────────────┘  └───────────────┘
```

### Data Flow

#### Request Flow

1. **Client Request**: Client sends HTTP/WebSocket request to Nginx
2. **TLS Termination**: Nginx terminates TLS and forwards to gateway
3. **Authentication**: Gateway validates authentication token
4. **Authorization**: Gateway checks user permissions
5. **Rate Limiting**: Gateway checks rate limits
6. **Routing**: Gateway routes request to appropriate MCP server
7. **Connection**: Connection manager establishes/maintains connection
8. **Response**: MCP server responds through gateway back to client
9. **Logging**: All activities are logged for audit

#### MCP Server Registration Flow

1. **Registration Request**: MCP server sends registration request
2. **Validation**: Gateway validates server configuration
3. **Container Creation**: Gateway creates isolated container for server
4. **Health Check**: Gateway performs health check on server
5. **Registry Update**: Server registered in database
6. **Route Configuration**: Routes configured for server endpoints

### Component Details

#### API Gateway

- **Purpose**: Main entry point for all API requests
- **Technology**: FastAPI (Python)
- **Responsibilities**:
  - Request validation
  - Authentication/authorization
  - Request routing
  - Response aggregation
  - Error handling

#### Authentication Service

- **Purpose**: Handle user authentication and token management
- **Technology**: Integrated into gateway (JWT)
- **Responsibilities**:
  - User authentication
  - Token generation and validation
  - Refresh token management
  - Session management

#### MCP Server Registry

- **Purpose**: Manage MCP server registration and metadata
- **Technology**: PostgreSQL database
- **Responsibilities**:
  - Server registration
  - Server discovery
  - Metadata storage
  - Health status tracking

#### Request Router

- **Purpose**: Route requests to appropriate MCP servers
- **Technology**: FastAPI routing with dynamic route registration
- **Responsibilities**:
  - Route matching
  - Load balancing
  - Failover handling
  - Request transformation

#### Connection Manager

- **Purpose**: Manage connections to MCP servers
- **Technology**: HTTP client and WebSocket manager
- **Responsibilities**:
  - Connection pooling
  - Connection health monitoring
  - Reconnection handling
  - Timeout management

#### Security Layer

- **Purpose**: Enforce security policies
- **Technology**: Middleware and Redis
- **Responsibilities**:
  - Rate limiting
  - Request validation
  - Security headers
  - Attack prevention

#### Audit Logger

- **Purpose**: Log all activities for audit and monitoring
- **Technology**: Structured logging to database and files
- **Responsibilities**:
  - Request logging
  - Authentication logging
  - Error logging
  - Performance metrics

### Network Architecture

#### Docker Networks

1. **gateway_network**: Main network for gateway services
   - Gateway
   - Database
   - Redis
   - Nginx

2. **mcp_servers_network**: Isolated network for MCP servers
   - MCP Server containers
   - Isolated from gateway network
   - Controlled access via gateway

#### Network Isolation

- MCP servers run in isolated Docker network
- Gateway acts as proxy between clients and servers
- Servers cannot directly access database or Redis
- Servers can only communicate through gateway

### Security Architecture

#### Authentication Flow

1. Client requests authentication
2. Gateway validates credentials
3. Gateway generates JWT token
4. Client uses token for subsequent requests
5. Gateway validates token on each request

#### Authorization Flow

1. Gateway extracts user from token
2. Gateway checks user permissions
3. Gateway verifies server access rights
4. Gateway allows or denies request

#### Isolation Strategy

1. Each MCP server runs in isolated container
2. Containers have resource limits (CPU, memory)
3. Containers have network restrictions
4. Containers have filesystem restrictions
5. Containers are monitored for security violations

### Data Models

#### MCP Server Model

```python
class MCPServer:
    id: str
    name: str
    description: str
    endpoint: str
    status: str  # active, inactive, error
    container_id: str
    network: str
    config: dict
    created_at: datetime
    updated_at: datetime
    health_status: str
    last_health_check: datetime
```

#### User Model

```python
class User:
    id: str
    username: str
    email: str
    hashed_password: str
    roles: List[str]
    permissions: List[str]
    created_at: datetime
    updated_at: datetime
    last_login: datetime
```

#### Audit Log Model

```python
class AuditLog:
    id: str
    user_id: str
    action: str
    resource: str
    method: str
    path: str
    status_code: int
    ip_address: str
    user_agent: str
    timestamp: datetime
    details: dict
```

### API Endpoints

#### Authentication Endpoints

- `POST /api/auth/login` - User login
- `POST /api/auth/logout` - User logout
- `POST /api/auth/refresh` - Refresh token
- `GET /api/auth/me` - Get current user

#### MCP Server Management Endpoints

- `POST /api/servers` - Register MCP server
- `GET /api/servers` - List MCP servers
- `GET /api/servers/{id}` - Get MCP server details
- `PUT /api/servers/{id}` - Update MCP server
- `DELETE /api/servers/{id}` - Unregister MCP server
- `POST /api/servers/{id}/start` - Start MCP server
- `POST /api/servers/{id}/stop` - Stop MCP server

#### Proxy Endpoints

- `POST /api/proxy/{server_id}/{path}` - Proxy HTTP request
- `WebSocket /api/ws/{server_id}/{path}` - Proxy WebSocket connection

### Scalability Considerations

#### Horizontal Scaling

- Gateway can be scaled horizontally
- Load balancer distributes requests
- Shared database and Redis for state
- Stateless gateway design

#### Vertical Scaling

- Database can be scaled vertically
- Redis can be scaled vertically
- MCP servers can be scaled individually

#### Caching Strategy

- Redis for session caching
- Redis for rate limiting
- Redis for frequently accessed data
- Database for persistent storage

### Monitoring and Observability

#### Metrics

- Request count and latency
- Error rates
- Authentication success/failure rates
- MCP server health status
- Resource utilization

#### Logging

- Structured logging to files
- Audit logs to database
- Error logs with stack traces
- Access logs with request details

#### Health Checks

- Gateway health endpoint
- Database health check
- Redis health check
- MCP server health checks
- Overall system health status

## Implementation Steps

### Step 1: Define Data Models

Create data models for all entities (see Data Models section above).

### Step 2: Design API Structure

Define all API endpoints and their specifications.

### Step 3: Design Database Schema

Create database schema for registry and audit logs.

### Step 4: Design Security Model

Define authentication and authorization mechanisms.

### Step 5: Design Routing Strategy

Define how requests are routed to MCP servers.

### Step 6: Design Connection Management

Define how connections are managed and maintained.

## Testing

### Architecture Validation

1. Review architecture diagram
2. Validate component interactions
3. Check security boundaries
4. Verify scalability design
5. Review monitoring strategy

### Design Review

1. Review data models
2. Review API design
3. Review security design
4. Review network design
5. Review deployment design

## Verification

1. **Architecture Documented**: All components are documented
2. **Data Flow Defined**: Request and registration flows are defined
3. **Security Model**: Security architecture is defined
4. **API Design**: All endpoints are specified
5. **Scalability**: Scalability strategy is defined

## Troubleshooting

### Issue: Architecture too complex

**Solution**: Simplify by combining related components or using simpler patterns.

### Issue: Security concerns

**Solution**: Review security architecture and add additional layers if needed.

### Issue: Scalability issues

**Solution**: Review scalability design and add horizontal scaling capabilities.

## Next Steps

After completing this instruction, proceed to:
- **02-api-server.md**: Implement the main API server
- **03-mcp-server-registry.md**: Implement MCP server registry
- **04-request-routing.md**: Implement request routing

