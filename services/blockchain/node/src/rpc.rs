use crate::block::Block;
use crate::chain::Blockchain;
use crate::consensus;
use crate::gossip;
use crate::transaction::Transaction;
use axum::{
    extract::{Path, State},
    http::StatusCode,
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use std::sync::{Arc, Mutex};
use tower_http::cors::{Any, CorsLayer};

pub type SharedState = Arc<Mutex<Blockchain>>;

#[derive(Serialize)]
pub struct NodeStatusResponse {
    pub blocks_count: usize,
    pub pending_transactions_count: usize,
    pub peers_count: usize,
    pub difficulty: usize,
    pub mining_reward: u64,
    pub is_valid: bool,
    pub latest_block_hash: String,
}

#[derive(Deserialize)]
pub struct CreateTransactionPayload {
    pub sender: String,
    pub recipient: String,
    pub amount: u64,
    pub signature: Option<String>,
}

#[derive(Deserialize)]
pub struct MinePayload {
    pub miner_address: String,
}

#[derive(Deserialize)]
pub struct RegisterPeerPayload {
    pub peer_url: String,
}

#[derive(Serialize, Deserialize)]
pub struct ApiResponse<T> {
    pub success: bool,
    pub message: String,
    pub data: Option<T>,
}

pub fn create_router(state: SharedState) -> Router {
    let cors = CorsLayer::new()
        .allow_origin(Any)
        .allow_methods(Any)
        .allow_headers(Any);

    Router::new()
        .route("/api/status", get(get_status))
        .route("/api/blocks", get(get_blocks))
        .route("/api/pending", get(get_pending))
        .route("/api/balance/:account", get(get_balance))
        .route("/api/transaction", post(create_transaction))
        .route("/api/mine", post(mine_block))
        .route("/api/peers", get(get_peers))
        .route("/api/peers/register", post(register_peer))
        .route("/api/peers/unregister", post(unregister_peer))
        .route("/api/peers/sync", post(sync_peers))
        .route("/api/peers/broadcast-block", post(receive_broadcast_block))
        .route("/api/peers/broadcast-tx", post(receive_broadcast_tx))
        .layer(cors)
        .with_state(state)
}

async fn get_status(State(state): State<SharedState>) -> impl IntoResponse {
    let chain = state.lock().unwrap();
    let status = NodeStatusResponse {
        blocks_count: chain.chain.len(),
        pending_transactions_count: chain.pending_transactions.len(),
        peers_count: chain.get_peers().len(),
        difficulty: chain.difficulty,
        mining_reward: chain.mining_reward,
        is_valid: chain.is_valid_chain(),
        latest_block_hash: chain.get_latest_block().hash.clone(),
    };
    Json(ApiResponse {
        success: true,
        message: "Node status fetched successfully".to_string(),
        data: Some(status),
    })
}

async fn get_blocks(State(state): State<SharedState>) -> impl IntoResponse {
    let chain = state.lock().unwrap();
    Json(ApiResponse {
        success: true,
        message: "Blocks fetched successfully".to_string(),
        data: Some(chain.chain.clone()),
    })
}

async fn get_pending(State(state): State<SharedState>) -> impl IntoResponse {
    let chain = state.lock().unwrap();
    Json(ApiResponse {
        success: true,
        message: "Pending transactions fetched successfully".to_string(),
        data: Some(chain.pending_transactions.clone()),
    })
}

async fn get_balance(
    Path(account): Path<String>,
    State(state): State<SharedState>,
) -> impl IntoResponse {
    let chain = state.lock().unwrap();
    let balance = chain.get_balance(&account);
    Json(ApiResponse {
        success: true,
        message: format!("Balance for account {}", account),
        data: Some(balance),
    })
}

async fn create_transaction(
    State(state): State<SharedState>,
    Json(payload): Json<CreateTransactionPayload>,
) -> impl IntoResponse {
    let (tx, peers) = {
        let mut chain = state.lock().unwrap();
        let tx = Transaction::new(
            payload.sender,
            payload.recipient,
            payload.amount,
            payload.signature,
        );

        match chain.add_transaction(tx.clone()) {
            Ok(_) => (tx, chain.get_peers()),
            Err(err) => {
                return (
                    StatusCode::BAD_REQUEST,
                    Json(ApiResponse {
                        success: false,
                        message: err,
                        data: None,
                    }),
                );
            }
        }
    };

    // Asynchronously gossip transaction to peers
    tokio::spawn(gossip::broadcast_transaction(peers, tx.clone()));

    (
        StatusCode::CREATED,
        Json(ApiResponse {
            success: true,
            message: "Transaction added to pool and gossiped to network".to_string(),
            data: Some(tx),
        }),
    )
}

async fn mine_block(
    State(state): State<SharedState>,
    Json(payload): Json<MinePayload>,
) -> impl IntoResponse {
    let (block, peers) = {
        let mut chain = state.lock().unwrap();

        match chain.mine_pending_transactions(payload.miner_address) {
            Ok(block) => (block, chain.get_peers()),
            Err(err) => {
                return (
                    StatusCode::BAD_REQUEST,
                    Json(ApiResponse {
                        success: false,
                        message: err,
                        data: None,
                    }),
                );
            }
        }
    };

    // Asynchronously gossip block to peers
    tokio::spawn(gossip::broadcast_block(peers, block.clone()));

    (
        StatusCode::OK,
        Json(ApiResponse {
            success: true,
            message: "Block mined successfully and gossiped to network".to_string(),
            data: Some(block),
        }),
    )
}

async fn get_peers(State(state): State<SharedState>) -> impl IntoResponse {
    let chain = state.lock().unwrap();
    let peers = chain.get_peers();
    Json(ApiResponse {
        success: true,
        message: "Peers list fetched successfully".to_string(),
        data: Some(peers),
    })
}

async fn register_peer(
    State(state): State<SharedState>,
    Json(payload): Json<RegisterPeerPayload>,
) -> impl IntoResponse {
    let mut chain = state.lock().unwrap();
    let added = chain.add_peer(payload.peer_url.clone());
    if added {
        Json(ApiResponse {
            success: true,
            message: format!("Peer registered successfully: {}", payload.peer_url),
            data: Some(chain.get_peers()),
        })
    } else {
        Json(ApiResponse {
            success: false,
            message: format!("Peer already registered or invalid: {}", payload.peer_url),
            data: Some(chain.get_peers()),
        })
    }
}

async fn unregister_peer(
    State(state): State<SharedState>,
    Json(payload): Json<RegisterPeerPayload>,
) -> impl IntoResponse {
    let mut chain = state.lock().unwrap();
    let removed = chain.remove_peer(&payload.peer_url);
    Json(ApiResponse {
        success: removed,
        message: if removed {
            format!("Peer removed: {}", payload.peer_url)
        } else {
            "Peer not found".to_string()
        },
        data: Some(chain.get_peers()),
    })
}

async fn sync_peers(State(state): State<SharedState>) -> impl IntoResponse {
    match consensus::resolve_conflicts(state.clone()).await {
        Ok(replaced) => Json(ApiResponse {
            success: true,
            message: if replaced {
                "Chain updated to longer valid peer chain via Nakamoto consensus"
            } else {
                "Local chain is authoritative and up to date"
            }
            .to_string(),
            data: Some(replaced),
        }),
        Err(err) => Json(ApiResponse {
            success: false,
            message: format!("Sync error: {}", err),
            data: Some(false),
        }),
    }
}

async fn receive_broadcast_block(
    State(state): State<SharedState>,
    Json(block): Json<Block>,
) -> impl IntoResponse {
    let res = {
        let mut chain = state.lock().unwrap();
        chain.receive_block(block)
    };

    match res {
        Ok(added) => Json(ApiResponse {
            success: true,
            message: if added {
                "Broadcast block appended to chain"
            } else {
                "Block already exists in local chain"
            }
            .to_string(),
            data: Some(added),
        }),
        Err(err) => {
            // Trigger sync if fork or missing block
            let state_clone = state.clone();
            tokio::spawn(async move {
                let _ = consensus::resolve_conflicts(state_clone).await;
            });
            Json(ApiResponse {
                success: false,
                message: format!("Block rejected ({}), triggered peer sync", err),
                data: Some(false),
            })
        }
    }
}

async fn receive_broadcast_tx(
    State(state): State<SharedState>,
    Json(tx): Json<Transaction>,
) -> impl IntoResponse {
    let mut chain = state.lock().unwrap();
    match chain.add_transaction(tx) {
        Ok(_) => Json(ApiResponse {
            success: true,
            message: "Broadcast transaction added to pool".to_string(),
            data: Some(true),
        }),
        Err(err) => Json(ApiResponse {
            success: false,
            message: err,
            data: Some(false),
        }),
    }
}
