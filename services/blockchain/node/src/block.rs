use crate::transaction::Transaction;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Block {
    pub index: u64,
    pub timestamp: i64,
    pub transactions: Vec<Transaction>,
    pub previous_hash: String,
    pub hash: String,
    pub nonce: u64,
    pub difficulty: usize,
}

impl Block {
    pub fn new(index: u64, transactions: Vec<Transaction>, previous_hash: String, difficulty: usize) -> Self {
        let timestamp = chrono::Utc::now().timestamp_millis();
        let mut block = Self {
            index,
            timestamp,
            transactions,
            previous_hash,
            hash: String::new(),
            nonce: 0,
            difficulty,
        };
        block.hash = block.calculate_hash();
        block
    }

    pub fn genesis(difficulty: usize) -> Self {
        let genesis_tx = Transaction::new_reward("genesis_account".to_string(), 1_000_000);
        let mut block = Self::new(0, vec![genesis_tx], "0".repeat(64), difficulty);
        block.mine_block();
        block
    }

    pub fn calculate_hash(&self) -> String {
        let tx_hashes: String = self.transactions.iter().map(|tx| tx.id.as_str()).collect();
        let payload = format!(
            "{}:{}:{}:{}:{}",
            self.index, self.timestamp, tx_hashes, self.previous_hash, self.nonce
        );
        let mut hasher = Sha256::new();
        hasher.update(payload.as_bytes());
        format!("{:x}", hasher.finalize())
    }

    pub fn mine_block(&mut self) {
        let target_prefix = "0".repeat(self.difficulty);
        while !self.hash.starts_with(&target_prefix) {
            self.nonce += 1;
            self.hash = self.calculate_hash();
        }
    }

    pub fn is_valid(&self, expected_prev_hash: &str) -> bool {
        if self.previous_hash != expected_prev_hash {
            return false;
        }
        if self.hash != self.calculate_hash() {
            return false;
        }
        let target_prefix = "0".repeat(self.difficulty);
        if !self.hash.starts_with(&target_prefix) {
            return false;
        }
        self.transactions.iter().all(|tx| tx.is_valid())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_genesis_block_mining() {
        let genesis = Block::genesis(2);
        assert_eq!(genesis.index, 0);
        assert!(genesis.hash.starts_with("00"));
        assert!(genesis.is_valid("0".repeat(64).as_str()));
    }
}
