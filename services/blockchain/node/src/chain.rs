use crate::block::Block;
use crate::peer::PeerManager;
use crate::transaction::Transaction;
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Blockchain {
    pub chain: Vec<Block>,
    pub pending_transactions: Vec<Transaction>,
    pub difficulty: usize,
    pub mining_reward: u64,
    pub peer_manager: PeerManager,
}

impl Blockchain {
    pub fn new(difficulty: usize, mining_reward: u64) -> Self {
        let genesis_block = Block::genesis(difficulty);
        Self {
            chain: vec![genesis_block],
            pending_transactions: Vec::new(),
            difficulty,
            mining_reward,
            peer_manager: PeerManager::new(),
        }
    }

    pub fn get_latest_block(&self) -> &Block {
        self.chain.last().expect("Blockchain should always contain at least genesis block")
    }

    pub fn add_peer(&mut self, url: String) -> bool {
        self.peer_manager.add_peer(url)
    }

    pub fn remove_peer(&mut self, url: &str) -> bool {
        self.peer_manager.remove_peer(url)
    }

    pub fn get_peers(&self) -> Vec<String> {
        self.peer_manager.get_peers()
    }

    pub fn add_transaction(&mut self, tx: Transaction) -> Result<(), String> {
        if !tx.is_valid() {
            return Err("Invalid transaction signature or payload".to_string());
        }

        // Avoid duplicate pending transactions
        if self.pending_transactions.iter().any(|t| t.id == tx.id) {
            return Ok(());
        }

        if tx.sender != "SYSTEM" {
            let sender_balance = self.get_balance(&tx.sender);
            if sender_balance < tx.amount {
                return Err(format!(
                    "Insufficient balance: sender {} has {}, required {}",
                    tx.sender, sender_balance, tx.amount
                ));
            }
        }

        self.pending_transactions.push(tx);
        Ok(())
    }

    pub fn mine_pending_transactions(&mut self, miner_address: String) -> Result<Block, String> {
        if miner_address.trim().is_empty() {
            return Err("Miner address cannot be empty".to_string());
        }

        let mut txs_to_mine = self.pending_transactions.clone();
        let reward_tx = Transaction::new_reward(miner_address, self.mining_reward);
        txs_to_mine.push(reward_tx);

        let latest_block = self.get_latest_block();
        let mut new_block = Block::new(
            latest_block.index + 1,
            txs_to_mine,
            latest_block.hash.clone(),
            self.difficulty,
        );

        new_block.mine_block();

        if !new_block.is_valid(&latest_block.hash) {
            return Err("Mined block validation failed".to_string());
        }

        self.chain.push(new_block.clone());
        self.pending_transactions.clear();

        Ok(new_block)
    }

    pub fn receive_block(&mut self, block: Block) -> Result<bool, String> {
        let latest_block = self.get_latest_block();

        // Check if block already exists
        if self.chain.iter().any(|b| b.hash == block.hash) {
            return Ok(false);
        }

        // Check if block fits directly on current tip
        if block.previous_hash == latest_block.hash && block.index == latest_block.index + 1 {
            if !block.is_valid(&latest_block.hash) {
                return Err("Incoming block failed validation".to_string());
            }

            self.chain.push(block.clone());
            // Filter out transactions in received block from pending pool
            let confirmed_ids: Vec<String> = block.transactions.iter().map(|t| t.id.clone()).collect();
            self.pending_transactions.retain(|t| !confirmed_ids.contains(&t.id));
            return Ok(true);
        }

        Err("Fork detected or block parent hash mismatch".to_string())
    }

    pub fn replace_chain(&mut self, new_chain: Vec<Block>) {
        // Collect all confirmed tx IDs in new chain
        let mut confirmed_txs = std::collections::HashSet::new();
        for b in &new_chain {
            for t in &b.transactions {
                confirmed_txs.insert(t.id.clone());
            }
        }

        self.chain = new_chain;
        // Retain only unconfirmed transactions
        self.pending_transactions.retain(|t| !confirmed_txs.contains(&t.id));
    }

    pub fn get_balance(&self, address: &str) -> u64 {
        let mut balance: i64 = 0;

        for block in &self.chain {
            for tx in &block.transactions {
                if tx.sender == address {
                    balance -= tx.amount as i64;
                }
                if tx.recipient == address {
                    balance += tx.amount as i64;
                }
            }
        }

        if balance < 0 { 0 } else { balance as u64 }
    }

    pub fn is_valid_chain(&self) -> bool {
        if self.chain.is_empty() {
            return false;
        }

        for i in 1..self.chain.len() {
            let current = &self.chain[i];
            let previous = &self.chain[i - 1];

            if !current.is_valid(&previous.hash) {
                return false;
            }
        }

        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_peer_and_chain_replacement() {
        let mut chain1 = Blockchain::new(2, 50);
        let mut chain2 = Blockchain::new(2, 50);

        chain1.add_peer("http://localhost:3004".to_string());
        assert_eq!(chain1.get_peers(), vec!["http://localhost:3004"]);

        let tx = Transaction::new("genesis_account".to_string(), "alice".to_string(), 100, None);
        chain1.add_transaction(tx).unwrap();
        let mined_block = chain1.mine_pending_transactions("charlie".to_string()).unwrap();

        assert_eq!(chain1.chain.len(), 2);
        assert_eq!(chain2.chain.len(), 1);

        chain2.replace_chain(chain1.chain.clone());
        assert_eq!(chain2.chain.len(), 2);
        assert_eq!(chain2.get_latest_block().hash, mined_block.hash);
    }
}
