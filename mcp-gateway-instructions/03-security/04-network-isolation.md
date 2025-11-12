# 04: Network Isolation

## Objective

Implement network isolation for MCP servers using Docker networks, ensuring that MCP servers are isolated from the gateway network and can only communicate through the gateway.

## Prerequisites

- Completed: 01-setup/03-docker-base-config.md
- Completed: 02-core-gateway/03-mcp-server-registry.md
- Understanding of Docker networking
- Knowledge of network security principles

## Implementation Steps

### Step 1: Update Docker Compose Network Configuration

#### docker-compose.yml

Update Docker Compose to create isolated networks:

```yaml
version: '3.8'

networks:
  gateway_network:
    driver: bridge
    name: mcp_gateway_network
    internal: false
  mcp_servers_network:
    driver: bridge
    name: mcp_servers_network
    internal: true  # Isolated network, no external access
  gateway_to_servers:
    driver: bridge
    name: gateway_to_servers
    internal: false

services:
  # Gateway services on gateway_network
  postgres:
    networks:
      - gateway_network

  redis:
    networks:
      - gateway_network

  gateway:
    networks:
      - gateway_network
      - gateway_to_servers  # Can access MCP servers

  nginx:
    networks:
      - gateway_network

  # MCP servers will be created dynamically on mcp_servers_network
```

### Step 2: Update Container Manager for Network Isolation

#### gateway/src/registry/container_manager.py

Update container manager to use isolated network:

```python
"""Container manager with network isolation."""
import docker
import logging
from typing import Optional, Dict
from src.config import settings

logger = logging.getLogger(__name__)


class ContainerManager:
    """Manager for Docker containers with network isolation."""

    def __init__(self):
        """Initialize container manager."""
        try:
            self.client = docker.from_env()
            self.mcp_network = settings.MCP_SERVER_NETWORK
            self._ensure_network()
        except Exception as e:
            logger.error(f"Failed to initialize Docker client: {e}")
            raise

    def _ensure_network(self):
        """Ensure MCP servers network exists."""
        try:
            self.client.networks.get(self.mcp_network)
        except docker.errors.NotFound:
            self.client.networks.create(
                self.mcp_network,
                driver="bridge",
                internal=True,  # Isolated network
                check_duplicate=True,
            )
            logger.info(f"Created network: {self.mcp_network}")

    async def start_container(
        self,
        name: str,
        endpoint: str,
        config: Dict,
        network: str = None,
    ) -> str:
        """Start container for MCP server with network isolation."""
        if network is None:
            network = self.mcp_network

        try:
            image = config.get("image", "mcp-server:latest")
            command = config.get("command", [])
            env = config.get("environment", {})
            
            # Container should not expose ports to host
            # Gateway will access via internal network
            ports = {}  # No port mapping to host

            # Create container with resource limits
            container = self.client.containers.run(
                image=image,
                command=command,
                environment=env,
                ports=ports,
                network=network,
                name=f"mcp-server-{name}",
                detach=True,
                restart_policy={"Name": "unless-stopped"},
                mem_limit=config.get("memory", "512m"),
                cpu_period=100000,
                cpu_quota=config.get("cpu_quota", 50000),
                network_disabled=False,
                # Security options
                security_opt=["no-new-privileges:true"],
                cap_drop=["ALL"],
                cap_add=config.get("cap_add", []),
                read_only=config.get("read_only", False),
                tmpfs=config.get("tmpfs", {}),
            )

            logger.info(f"Started container: {container.id} on network: {network}")
            return container.id
        except Exception as e:
            logger.error(f"Error starting container: {e}")
            raise

    async def connect_to_gateway_network(self, container_id: str):
        """Connect container to gateway network for communication."""
        try:
            container = self.client.containers.get(container_id)
            gateway_network = self.client.networks.get("gateway_to_servers")
            gateway_network.connect(container)
            logger.info(f"Connected container {container_id} to gateway network")
        except Exception as e:
            logger.error(f"Error connecting container to gateway network: {e}")
            raise
```

### Step 3: Create Network Security Policies

#### gateway/src/security/network_policy.py

Create network security policies:

```python
"""Network security policies."""
import logging
from typing import List, Dict
from src.config import settings

logger = logging.getLogger(__name__)


class NetworkPolicy:
    """Network security policy."""

    def __init__(self):
        """Initialize network policy."""
        self.allowed_ports = [80, 443, 8000]  # Allowed ports for MCP servers
        self.blocked_ips = []  # Blocked IP addresses
        self.allowed_domains = []  # Allowed domains

    def is_port_allowed(self, port: int) -> bool:
        """Check if port is allowed."""
        return port in self.allowed_ports

    def is_ip_allowed(self, ip: str) -> bool:
        """Check if IP is allowed."""
        return ip not in self.blocked_ips

    def validate_endpoint(self, endpoint: str) -> bool:
        """Validate MCP server endpoint."""
        # Validate endpoint format and security
        if not endpoint.startswith(("http://", "https://")):
            return False
        return True

    def get_network_config(self, server_config: Dict) -> Dict:
        """Get network configuration for server."""
        return {
            "network": settings.MCP_SERVER_NETWORK,
            "internal": True,
            "ports": {},
            "security_opt": ["no-new-privileges:true"],
            "cap_drop": ["ALL"],
        }
```

### Step 4: Update Gateway Router for Network Isolation

#### gateway/src/router/service.py

Update router to use isolated network:

```python
# Update route_request method to use internal network
async def route_request(
    self,
    server_id: str,
    path: str,
    request: Request,
) -> Response:
    """Route HTTP request to MCP server via isolated network."""
    try:
        server = await self.registry.get_server(server_id)
        if not server:
            raise ValueError(f"Server {server_id} not found")

        # Use internal endpoint (container name or internal IP)
        # Gateway can access via gateway_to_servers network
        container_name = f"mcp-server-{server.name}"
        internal_endpoint = f"http://{container_name}:{server.config.get('port', 8000)}"
        
        target_url = f"{internal_endpoint}/{path.lstrip('/')}"
        
        # ... rest of the routing logic ...
```

### Step 5: Create Network Monitoring

#### gateway/src/security/network_monitor.py

Create network monitoring:

```python
"""Network monitoring for security."""
import logging
from typing import Dict, List
import docker

logger = logging.getLogger(__name__)


class NetworkMonitor:
    """Monitor network activity for security."""

    def __init__(self):
        """Initialize network monitor."""
        self.client = docker.from_env()

    def get_network_connections(self, network_name: str) -> List[Dict]:
        """Get network connections."""
        try:
            network = self.client.networks.get(network_name)
            return network.attrs.get("Containers", {})
        except Exception as e:
            logger.error(f"Error getting network connections: {e}")
            return []

    def monitor_network_traffic(self, container_id: str) -> Dict:
        """Monitor network traffic for container."""
        try:
            container = self.client.containers.get(container_id)
            stats = container.stats(stream=False)
            return {
                "network_rx": stats.get("networks", {}).get("eth0", {}).get("rx_bytes", 0),
                "network_tx": stats.get("networks", {}).get("eth0", {}).get("tx_bytes", 0),
            }
        except Exception as e:
            logger.error(f"Error monitoring network traffic: {e}")
            return {}
```

### Step 6: Update Docker Compose for Network Isolation

#### docker-compose.yml

Ensure gateway can access MCP servers network:

```yaml
services:
  gateway:
    networks:
      - gateway_network
      - gateway_to_servers
    extra_hosts:
      - "mcp-server-*:gateway_to_servers"
```

## Testing

### Test Network Isolation

Test network isolation:

```bash
# Create isolated network
docker network create --internal mcp_servers_network

# Start test container on isolated network
docker run -d --name test-server --network mcp_servers_network nginx

# Verify container cannot access internet
docker exec test-server ping -c 1 8.8.8.8
# Should fail

# Verify gateway can access container
docker exec mcp-gateway ping -c 1 test-server
# Should succeed
```

## Verification

1. **Network Created**: Isolated network is created
2. **Containers Isolated**: MCP server containers are isolated
3. **Gateway Access**: Gateway can access MCP servers
4. **No External Access**: MCP servers cannot access external networks
5. **Security Policies**: Network security policies are enforced

## Troubleshooting

### Issue: Containers can't communicate

**Solution**: Check network configuration and ensure containers are on correct network:
```bash
docker network inspect mcp_servers_network
docker network inspect gateway_to_servers
```

### Issue: Gateway can't access MCP servers

**Solution**: Ensure gateway is connected to both networks:
```bash
docker network connect gateway_to_servers mcp-gateway
```

### Issue: Network creation fails

**Solution**: Check Docker network driver and permissions:
```bash
docker network ls
docker network create --driver bridge mcp_servers_network
```

## Next Steps

After completing this instruction, proceed to:
- **05-rate-limiting.md**: Implement rate limiting

