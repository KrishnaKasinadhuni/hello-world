mod block;
mod chain;
mod consensus;
mod gossip;
mod peer;
mod rpc;
mod transaction;

use chain::Blockchain;
use rpc::{create_router, SharedState};
use std::env;
use std::sync::{Arc, Mutex};

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt::init();

    let difficulty: usize = env::var("CHAIN_DIFFICULTY")
        .unwrap_or_else(|_| "2".to_string())
        .parse()
        .unwrap_or(2);

    let mining_reward: u64 = env::var("MINING_REWARD")
        .unwrap_or_else(|_| "50".to_string())
        .parse()
        .unwrap_or(50);

    let port: u16 = env::var("PORT")
        .unwrap_or_else(|_| "3002".to_string())
        .parse()
        .unwrap_or(3002);

    let mut chain = Blockchain::new(difficulty, mining_reward);

    // Initial peer registration from PEERS environment variable
    if let Ok(peers_env) = env::var("PEERS") {
        for peer_url in peers_env.split(',') {
            let trimmed = peer_url.trim();
            if !trimmed.is_empty() {
                chain.add_peer(trimmed.to_string());
                println!("🌐 Registered initial seed peer: {}", trimmed);
            }
        }
    }

    let shared_state: SharedState = Arc::new(Mutex::new(chain));

    let app = create_router(shared_state);

    let addr = format!("0.0.0.0:{}", port);
    println!("🚀 Rust P2P Blockchain Node running on http://{}", addr);

    let listener = tokio::net::TcpListener::bind(&addr)
        .await
        .expect("Failed to bind TCP listener");

    axum::serve(listener, app)
        .await
        .expect("Failed to start Axum server");
}
