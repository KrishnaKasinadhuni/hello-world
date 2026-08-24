# Rust & TypeScript 7 P2P Blockchain Subproject

A self-contained, multi-node **Proof-of-Work (PoW) Blockchain Engine** written in **Rust (1.93+)** paired with a **TypeScript 7.0+ & React 19 Analytics Explorer**.

---

## 🏗️ Subproject Architecture

```
services/blockchain/
├── node/                             # Rust Axum/Tokio P2P Node Engine (Port 3002 default)
│   ├── src/
│   │   ├── block.rs                  # Block structure & Proof-of-Work SHA-256 mining logic
│   │   ├── transaction.rs            # Transaction structure & cryptographic validation
│   │   ├── chain.rs                  # Ledger state, balance tracking, & pending mempool
│   │   ├── peer.rs                   # Peer discovery & registration manager
│   │   ├── consensus.rs               # Nakamoto longest valid chain conflict resolution
│   │   ├── gossip.rs                  # Asynchronous P2P block & transaction propagation
│   │   ├── rpc.rs                    # Axum REST / JSON-RPC API handlers & CORS rules
│   │   └── main.rs                   # Tokio entry point & SEED peer listener
│   └── Cargo.toml                    # Dependencies (tokio, axum, reqwest, sha2, serde)
├── client/                           # TypeScript 7.0+ & React 19 Web Dashboard (Port 3003 default)
│   ├── src/
│   │   ├── sdk.ts                    # Type-safe TypeScript 7 Client SDK
│   │   ├── analytics.ts              # Throughput stats, miner leaderboard, & mempool aging engine
│   │   ├── components/
│   │   │   ├── AddressModal.tsx      # Interactive Address Inspector popup
│   │   │   ├── TransactionModal.tsx  # Interactive Transaction Inspector popup
│   │   │   ├── AnalyticsTab.tsx      # Throughput charts, miner rankings, & mempool health
│   │   │   └── PeersTab.tsx          # P2P mesh network topology & sync controller
│   │   ├── App.tsx                   # Main Tabbed Explorer Dashboard
│   │   └── index.css                 # Glassmorphism dark mode design system
│   └── package.json                  # Dependencies ("typescript": "^7.0.0", react 19, vite)
└── scripts/
    └── start-cluster.sh              # Local 2-node P2P cluster runner script
```

---

## ⚡ Quick Start

### 1. Build & Run Single Node locally
```bash
# Terminal 1: Run Rust Node (Port 3002)
cd services/blockchain/node
cargo run

# Terminal 2: Run TypeScript 7 Web Dashboard (Port 3003)
cd services/blockchain/client
npm install
npm run dev
```

### 2. Run with Cluster Script (2-Node P2P Cluster + UI)
```bash
./services/blockchain/scripts/start-cluster.sh
```

### 3. Run with Docker Compose (Multi-Container P2P Mesh)
```bash
cd services/blockchain
docker-compose up --build
```
- **Node A**: `http://localhost:3002`
- **Node B**: `http://localhost:3004`
- **Client Explorer UI**: `http://localhost:3003`

---

## 📡 REST & RPC API Reference (Port 3002)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/status` | Chain height, difficulty, mempool size, peer count, integrity state |
| `GET` | `/api/blocks` | Array of all confirmed blocks in ledger |
| `GET` | `/api/pending` | Unconfirmed mempool transactions |
| `GET` | `/api/balance/:account` | Balance query for specific address |
| `POST` | `/api/transaction` | Broadcast & queue new transaction (`{sender, recipient, amount}`) |
| `POST` | `/api/mine` | Mine pending mempool transactions into new block (`{miner_address}`) |
| `GET` | `/api/peers` | List connected P2P peer URLs |
| `POST` | `/api/peers/register` | Register new remote node URL (`{peer_url}`) |
| `POST` | `/api/peers/sync` | Trigger Nakamoto longest valid chain consensus resolution |

---

## 🧪 Testing & Validation

```bash
# Rust Unit Tests
cd services/blockchain/node && cargo test

# TypeScript 7 Strict Typechecks & Build
cd services/blockchain/client && npm test && npm run build
```
