use crate::block::Block;
use crate::transaction::Transaction;

pub async fn broadcast_block(peers: Vec<String>, block: Block) {
    if peers.is_empty() {
        return;
    }

    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(3))
        .build();

    let client = match client {
        Ok(c) => c,
        Err(_) => return,
    };

    for peer_url in peers {
        let endpoint = format!("{}/api/peers/broadcast-block", peer_url.trim_end_matches('/'));
        let _ = client.post(&endpoint).json(&block).send().await;
    }
}

pub async fn broadcast_transaction(peers: Vec<String>, tx: Transaction) {
    if peers.is_empty() {
        return;
    }

    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(3))
        .build();

    let client = match client {
        Ok(c) => c,
        Err(_) => return,
    };

    for peer_url in peers {
        let endpoint = format!("{}/api/peers/broadcast-tx", peer_url.trim_end_matches('/'));
        let _ = client.post(&endpoint).json(&tx).send().await;
    }
}
