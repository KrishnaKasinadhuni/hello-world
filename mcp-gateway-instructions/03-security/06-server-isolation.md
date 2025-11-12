# 06: Server Isolation

## Objective

Implement server isolation for MCP servers using Docker container isolation, resource limits, security constraints, and sandboxing to ensure MCP servers cannot affect other servers or the gateway.

## Prerequisites

- Completed: 02-core-gateway/03-mcp-server-registry.md
- Completed: 03-security/04-network-isolation.md
- Understanding of Docker container security
- Knowledge of Linux capabilities and namespaces

## Implementation Steps

### Step 1: Create Container Security Configuration

#### gateway/src/security/container_security.py

Create container security configuration:

```python
"""Container security configuration."""
import logging
from typing import Dict, List, Optional
from src.config import settings

logger = logging.getLogger(__name__)


class ContainerSecurity:
    """Container security configuration."""

    def __init__(self):
        """Initialize container security."""
        self.default_memory_limit = "512m"
        self.default_cpu_limit = 0.5
        self.default_cpu_quota = 50000
        self.default_cpu_period = 100000

    def get_security_config(self, server_config: Dict) -> Dict:
        """Get security configuration for container."""
        return {
            "memory": server_config.get("memory", self.default_memory_limit),
            "cpu_quota": server_config.get("cpu_quota", self.default_cpu_quota),
            "cpu_period": server_config.get("cpu_period", self.default_cpu_period),
            "security_opt": ["no-new-privileges:true"],
            "cap_drop": ["ALL"],
            "cap_add": server_config.get("cap_add", []),
            "read_only": server_config.get("read_only", True),
            "tmpfs": server_config.get("tmpfs", {"/tmp": "rw,noexec,nosuid"}),
            "user": server_config.get("user", "1000:1000"),
            "ulimits": [
                docker.types.Ulimit(name="nofile", soft=1024, hard=2048),
                docker.types.Ulimit(name="nproc", soft=64, hard؟=128),
            ],
            "pids_limit": server_config.get("pids_limit", 100),
            "shm_size": server_config.get("shm_size", "64m"),
        }

    def validate_security_config(self, config: Dict) -> bool:
        """Validate security configuration."""
        # Check memory limit
        memory = config.get("memory", self.default_memory_limit)
        if not isinstance(memory, str) or not memory.endswith(("m", "g")):
            return False

        # Check CPU limit
        cpu_quota = config.get("cpu_quota", self.default_cpu_quota)
        if not isinstance(cpu_quota, int) or cpu_quota < 1000:
            return False

        # Check capabilities
        cap_add = config.get("cap_add", [])
        if not isinstance(cap_add, list):
            return False

        return True
```

### Step 2: Update Container Manager with Security

#### gateway/src/registry/container_manager.py

Update container manager with security constraints:

```python
# Update start_container method
async def start_container(
    self,
    name: str,
    endpoint: str,
    config: Dict,
    network: str = None,
) -> str:
    """Start container with security constraints."""
    from src.security.container_security import ContainerSecurity
    
    security = ContainerSecurity()
    security_config = security.get_security_config(config)

    try:
        container = self.client.containers.run(
            image=config.get("image", "mcp-server:latest"),
            command=config.get("command", []),
            environment=config.get("environment", {}),
            network=network or self.mcp_network,
            name=f"mcp-server-{name}",
            detach=True,
            restart_policy={"Name": "unless-stopped"},
            # Resource limits
            mem_limit=security_config["memory"],
            cpu_period=security_config["cpu_period"],
            cpu_quota=security_config["cpu_quota"],
            # Security options
            security_opt=security_config["security_opt"],
            cap_drop=security_config["cap_drop"],
            cap_add=security_config["cap_add"],
            read_only=security_config["read_only"],
            tmpfs=security_config["tmpfs"],
            user=security_config["user"],
            ulimits=security_config["ulimits"],
            pids_limit=security_config["pids_limit"],
            shm_size=security_config["shm_size"],
            # Network isolation
            network_disabled=False,
            # Logging
            log_config={
                "type": "json-file",
                "config": {
                    "max-size": "10m",
                    "max-file": "3",
                },
            },
        )

        logger.info(f"Started secure container: {container.id}")
        return container.id
    except Exception as e:
        logger.error(f"Error starting container: {e}")
        raise
```

### Step 3: Create Resource Monitor

#### gateway/src/security/resource_monitor.py

Create resource monitor for containers:

```python
"""Resource monitor for containers."""
import logging
import docker
from typing import Dict, Optional
from src.config import settings

logger = logging.getLogger(__name__)


class ResourceMonitor:
    """Monitor container resources."""

    def __init__(self):
        """Initialize resource monitor."""
        self.client = docker.from_env()
        self.memory_threshold = 0.9  # 90% memory usage
        self.cpu_threshold = 0.9  # 90% CPU usage

    async def monitor_container(self, container_id: str) -> Dict:
        """Monitor container resources."""
        try:
            container = self.client.containers.get(container_id)
            stats = container.stats(stream=False)

            # Calculate memory usage
            memory_usage = stats["memory_stats"].get("usage", 0)
            memory_limit = stats["memory_stats"].get("limit", 0)
            memory_percent = (memory_usage / memory_limit) * 100 if memory_limit > 0 else 0

            # Calculate CPU usage
            cpu_delta = stats["cpu_stats"]["cpu_usage"]["total_usage"] - \
                       stats["precpu_stats"]["cpu_usage"]["total_usage"]
            system_delta = stats["cpu_stats"]["system_cpu_usage"] - \
                          stats["precpu_stats"]["system_cpu_usage"]
            cpu_percent = (cpu_delta / system_delta) * 100 if system_delta > 0 else 0

            # Check thresholds
            memory_alert = memory_percent > (self.memory_threshold * 100)
            cpu_alert = cpu_percent > (self.cpu_threshold * 100)

            return {
                "container_id": container_id,
                "memory_usage": memory_usage,
                "memory_limit": memory_limit,
                "memory_percent": memory_percent,
                "cpu_percent": cpu_percent,
                "memory_alert": memory_alert,
                "cpu_alert": cpu_alert,
            }
        except Exception as e:
            logger.error(f"Error monitoring container: {e}")
            return {}

    async def check_resource_limits(self, container_id: str) -> bool:
        """Check if container is within resource limits."""
        stats = await self.monitor_container(container_id)
        return not (stats.get("memory_alert") or stats.get("cpu_alert"))

    async def get_container_logs(self, container_id: str, lines: int = 100) -> str:
        """Get container logs."""
        try:
            container = self.client.containers.get(container_id)
            return container.logs(tail=lines).decode("utf-8")
        except Exception as e:
            logger.error(f"Error getting container logs: {e}")
            return ""
```

### Step 4: Create Security Policy Enforcement

#### gateway/src/security/policy_enforcer.py

Create security policy enforcer:

```python
"""Security policy enforcer."""
import logging
from typing import Dict, List
from src.config import settings

logger = logging.getLogger(__name__)


class PolicyEnforcer:
    """Enforce security policies."""

    def __init__(self):
        """Initialize policy enforcer."""
        self.blocked_ports = [22, 23, 135, 139, 445]  # Blocked ports
        self.allowed_ports = [80, 443, 8000, 8080]  # Allowed ports
        self.blocked_commands = ["rm", "shutdown", "reboot"]  # Blocked commands

    def validate_container_config(self, config: Dict) -> tuple[bool, str]:
        """Validate container configuration against security policies."""
        # Check ports
        ports = config.get("ports", {})
        for port in ports.keys():
            if int(port) in self.blocked_ports:
                return False, f"Port {port} is blocked"

        # Check commands
        command = config.get("command", [])
        for cmd in command:
            if any(blocked in cmd for blocked in self.blocked_commands):
                return False, f"Command contains blocked command: {cmd}"

        # Check image
        image = config.get("image", "")
        if not image or image == "latest":
            return False, "Image must be specified with version tag"

        return True, ""

    def get_security_policy(self) -> Dict:
        """Get security policy."""
        return {
            "blocked_ports": self.blocked_ports,
            "allowed_ports": self.allowed_ports,
            "blocked_commands": self.blocked_commands,
            "max_memory": settings.MCP_SERVER_MAX_MEMORY,
            "max_cpu": settings.MCP_SERVER_MAX_CPU,
        }
```

### Step 5: Update Registry Service with Security

#### gateway/src/registry/service.py

Update registry service to enforce security:

```python
# Add to imports
from src.security.policy_enforcer import PolicyEnforcer
from src.security.container_security import ContainerSecurity

# Update register_server method
async def register_server(
    self,
    name: str,
    description: str,
    endpoint: str,
    config: dict,
    network: str = "mcp_servers",
) -> MCPServer:
    """Register a new MCP server with security validation."""
    # Validate security policy
    policy_enforcer = PolicyEnforcer()
    is_valid, error_message = policy_enforcer.validate_container_config(config)
    if not is_valid:
        raise ValueError(f"Security policy violation: {error_message}")

    # Validate security configuration
    security = ContainerSecurity()
    if not security.validate_security_config(config):
        raise ValueError("Invalid security configuration")

    # ... rest of registration logic ...
```

### Step 6: Create Container Health Checker

#### gateway/src/security/container_health.py

Create container health checker:

```python
"""Container health checker."""
import logging
import docker
from typing import Dict
from src.config import settings

logger = logging.getLogger(__name__)


class ContainerHealthChecker:
    """Check container health and security."""

    def __init__(self):
        """Initialize container health checker."""
        self.client = docker.from_env()

    async def check_container_health(self, container_id: str) -> Dict:
        """Check container health."""
        try:
            container = self.client.containers.get(container_id)
            health = container.attrs.get("State", {}).get("Health", {})
            
            return {
                "status": container.status,
                "health_status": health.get("Status", "unknown"),
                "health_failing_streak": health.get("FailingStreak", 0),
                "restart_count": container.attrs.get("RestartCount", 0),
            }
        except Exception as e:
            logger.error(f"Error checking container health: {e}")
            return {"status": "unknown", "error": str(e)}

    async def check_security_violations(self, container_id: str) -> List[str]:
        """Check for security violations."""
        violations = []
        
        try:
            container = self.client.containers.get(container_id)
            attrs = container.attrs

            # Check capabilities
            cap_add = attrs.get("HostConfig", {}).get("CapAdd", [])
            if cap_add and "ALL" not in cap_add:
                # Check for dangerous capabilities
                dangerous_caps = ["SYS_ADMIN", "NET_ADMIN", "SYS_MODULE"]
                if any(cap in cap_add for cap in dangerous_caps):
                    violations.append("Dangerous capabilities enabled")

            # Check privileged mode
            if attrs.get("HostConfig", {}).get("Privileged", False):
                violations.append("Container is running in privileged mode")

            # Check read-only filesystem
            if not attrs.get("HostConfig", {}).get("ReadonlyRootfs", False):
                violations.append("Container filesystem is not read-only")

        except Exception as e:
            logger.error(f"Error checking security violations: {e}")
            violations.append(f"Error checking security: {e}")

        return violations
```

## Testing

### Test Server Isolation

Test server isolation:

```bash
# Start a test container with security constraints
docker run -d --name test-server \
  --memory="512m" \
  --cpus="0.5" \
  --security-opt no-new-privileges:true \
  --cap-drop ALL \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid \
  nginx

# Check container security
docker inspect test-server | jq '.[0].HostConfig'

# Test resource limits
docker stats test-server

# Check security violations
python -c "
from src.security.container_health import ContainerHealthChecker
import asyncio

async def test():
    checker = ContainerHealthChecker()
    violations = await checker.check_security_violations('test-server')
    print(violations)

asyncio.run(test())
"
```

## Verification

1. **Security Config**: Security configuration is applied
2. **Resource Limits**: Resource limits are enforced
3. **Capabilities**: Capabilities are restricted
4. **Filesystem**: Filesystem is read-only
5. **Network Isolation**: Network isolation is enforced
6. **Policy Enforcement**: Security policies are enforced

## Troubleshooting

### Issue: Container won't start with security constraints

**Solution**: Check if container image supports read-only filesystem:
```bash
docker run --read-only --tmpfs /tmp nginx
```

### Issue: Resource limits too strict

**Solution**: Adjust resource limits in configuration:
```python
# In container_security.py
self.default_memory_limit = "1g"
self.default_cpu_limit = 1.0
```

### Issue: Security violations detected

**Solution**: Review container configuration and fix security issues:
```bash
docker inspect container_id | jq '.[0].HostConfig'
```

## Next Steps

After completing this instruction, proceed to:
- **07-audit-logging.md**: Implement audit logging

