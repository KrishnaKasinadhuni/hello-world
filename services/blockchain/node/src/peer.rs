use serde::{Deserialize, Serialize};
use std::collections::HashSet;

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct PeerManager {
    pub peers: HashSet<String>,
}

impl PeerManager {
    pub fn new() -> Self {
        Self {
            peers: HashSet::new(),
        }
    }

    pub fn add_peer(&mut self, url: String) -> bool {
        let trimmed = url.trim();
        if trimmed.is_empty() {
            return false;
        }
        // Normalize URL without trailing slash
        let mut normalized = trimmed.trim_end_matches('/').to_string();
        if !normalized.starts_with("http://") && !normalized.starts_with("https://") {
            normalized = format!("http://{}", normalized);
        }
        self.peers.insert(normalized)
    }

    pub fn remove_peer(&mut self, url: &str) -> bool {
        let normalized = url.trim().trim_end_matches('/');
        self.peers.remove(normalized)
    }

    pub fn get_peers(&self) -> Vec<String> {
        let mut list: Vec<String> = self.peers.iter().cloned().collect();
        list.sort();
        list
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_peer_registration_and_normalization() {
        let mut pm = PeerManager::new();
        assert!(pm.add_peer("http://localhost:3004/".to_string()));
        assert!(!pm.add_peer("http://localhost:3004".to_string())); // duplicate
        assert_eq!(pm.get_peers(), vec!["http://localhost:3004"]);
    }
}
