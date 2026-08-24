#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BLOCKCHAIN_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "🚀 Starting 2-Node Rust P2P Blockchain Cluster..."
echo "Node A -> http://localhost:3002"
echo "Node B -> http://localhost:3004 (Peer-seeded to Node A)"
echo "Client UI -> http://localhost:3003"
echo "----------------------------------------------------"

# Cleanup background processes on exit
cleanup() {
    echo ""
    echo "🛑 Stopping cluster processes..."
    kill $(jobs -p) 2>/dev/null || true
}
trap cleanup EXIT

# 1. Start Node A
echo "Starting Node A on port 3002..."
(cd "$BLOCKCHAIN_DIR/node" && PORT=3002 cargo run) &
NODE_A_PID=$!
sleep 2

# 2. Start Node B
echo "Starting Node B on port 3004 (peered with Node A)..."
(cd "$BLOCKCHAIN_DIR/node" && PORT=3004 PEERS=http://localhost:3002 cargo run) &
NODE_B_PID=$!
sleep 2

# 3. Start TypeScript 7 Client UI
echo "Starting TypeScript 7 Client Explorer UI on port 3003..."
(cd "$BLOCKCHAIN_DIR/client" && npm run dev) &
CLIENT_PID=$!

wait
