use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Transaction {
    pub id: String,
    pub sender: String,
    pub recipient: String,
    pub amount: u64,
    pub timestamp: i64,
    pub signature: Option<String>,
}

impl Transaction {
    pub fn new(sender: String, recipient: String, amount: u64, signature: Option<String>) -> Self {
        let timestamp = chrono::Utc::now().timestamp_millis();
        let mut tx = Self {
            id: String::new(),
            sender,
            recipient,
            amount,
            timestamp,
            signature,
        };
        tx.id = tx.calculate_hash();
        tx
    }

    pub fn new_reward(recipient: String, amount: u64) -> Self {
        Self::new("SYSTEM".to_string(), recipient, amount, None)
    }

    pub fn calculate_hash(&self) -> String {
        let payload = format!(
            "{}:{}:{}:{}:{}",
            self.sender, self.recipient, self.amount, self.timestamp, self.signature.as_deref().unwrap_or("")
        );
        let mut hasher = Sha256::new();
        hasher.update(payload.as_bytes());
        format!("{:x}", hasher.finalize())
    }

    pub fn is_valid(&self) -> bool {
        if self.amount == 0 {
            return false;
        }
        if self.sender == "SYSTEM" {
            return true;
        }
        if self.sender.trim().is_empty() || self.recipient.trim().is_empty() {
            return false;
        }
        self.id == self.calculate_hash()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_transaction_hash_and_validity() {
        let tx = Transaction::new("alice".to_string(), "bob".to_string(), 50, None);
        assert!(tx.is_valid());
        assert_eq!(tx.id, tx.calculate_hash());
    }

    #[test]
    fn test_zero_amount_invalid() {
        let tx = Transaction::new("alice".to_string(), "bob".to_string(), 0, None);
        assert!(!tx.is_valid());
    }
}
