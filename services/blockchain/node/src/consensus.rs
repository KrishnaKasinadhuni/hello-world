use crate::block::Block;
use crate::chain::Blockchain;
use crate::rpc::ApiResponse;
use std::sync::{Arc, Mutex};

pub async fn resolve_conflicts(shared_state: Arc<Mutex<Blockchain>>) -> Result<bool, String> {
    let (peers, local_length) = {
        let chain_guard = shared_state.lock().unwrap();
        (chain_guard.get_peers(), chain_guard.chain.len())
    };

    if peers.is_empty() {
        return Ok(false);
    }

    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(5))
        .build()
        .map_err(|e| e.to_string())?;

    let mut longest_valid_chain: Option<Vec<Block>> = None;
    let mut max_length = local_length;

    for peer_url in peers {
        let endpoint = format!("{}/api/blocks", peer_url.trim_end_matches('/'));
        let response = match client.get(&endpoint).send().await {
            Ok(res) => res,
            Err(_) => continue,
        };

        if !response.status().is_success() {
            continue;
        }

        let api_res: ApiResponse<Vec<Block>> = match response.json().await {
            Ok(data) => data,
            Err(_) => continue,
        };

        if let Some(candidate_chain) = api_res.data {
            if candidate_chain.len() > max_length && is_valid_external_chain(&candidate_chain) {
                max_length = candidate_chain.len();
                longest_valid_chain = Some(candidate_chain);
            }
        }
    }

    if let Some(new_chain) = longest_valid_chain {
        let mut chain_guard = shared_state.lock().unwrap();
        chain_guard.replace_chain(new_chain);
        Ok(true)
    } else {
        Ok(false)
    }
}

pub fn is_valid_external_chain(chain: &[Block]) -> bool {
    if chain.is_empty() {
        return false;
    }

    // Verify genesis block
    if chain[0].index != 0 {
        return false;
    }

    // Verify sequential blocks
    for i in 1..chain.len() {
        let current = &chain[i];
        let previous = &chain[i - 1];

        if current.index != previous.index + 1 {
            return false;
        }

        if !current.is_valid(&previous.hash) {
            return false;
        }
    }

    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_valid_external_chain_verification() {
        let genesis = Block::genesis(2);
        let valid_chain = vec![genesis];
        assert!(is_valid_external_chain(&valid_chain));

        let invalid_chain: Vec<Block> = vec![];
        assert!(!is_valid_external_chain(&invalid_chain));
    }
}
