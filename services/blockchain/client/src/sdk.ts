export interface Transaction {
  id: string;
  sender: string;
  recipient: string;
  amount: number;
  timestamp: number;
  signature?: string | null;
}

export interface Block {
  index: number;
  timestamp: number;
  transactions: Transaction[];
  previous_hash: string;
  hash: string;
  nonce: number;
  difficulty: number;
}

export interface NodeStatus {
  blocks_count: number;
  pending_transactions_count: number;
  peers_count: number;
  difficulty: number;
  mining_reward: number;
  is_valid: boolean;
  latest_block_hash: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data?: T;
}

export class BlockchainNodeClient {
  private baseUrl: string;

  constructor(baseUrl: string = '/api') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  private async request<T>(endpoint: string, options?: RequestInit): Promise<T> {
    const res = await fetch(`${this.baseUrl}${endpoint}`, {
      headers: {
        'Content-Type': 'application/json',
      },
      ...options,
    });

    const body: ApiResponse<T> = await res.json();
    if (!res.ok || !body.success) {
      throw new Error(body.message || `RPC Request failed with status ${res.status}`);
    }

    return body.data as T;
  }

  async getStatus(): Promise<NodeStatus> {
    return this.request<NodeStatus>('/status');
  }

  async getBlocks(): Promise<Block[]> {
    return this.request<Block[]>('/blocks');
  }

  async getPending(): Promise<Transaction[]> {
    return this.request<Transaction[]>('/pending');
  }

  async getBalance(account: string): Promise<number> {
    return this.request<number>(`/balance/${encodeURIComponent(account)}`);
  }

  async sendTransaction(sender: string, recipient: string, amount: number): Promise<Transaction> {
    return this.request<Transaction>('/transaction', {
      method: 'POST',
      body: JSON.stringify({ sender, recipient, amount }),
    });
  }

  async mineBlock(minerAddress: string): Promise<Block> {
    return this.request<Block>('/mine', {
      method: 'POST',
      body: JSON.stringify({ miner_address: minerAddress }),
    });
  }

  async getPeers(): Promise<string[]> {
    return this.request<string[]>('/peers');
  }

  async registerPeer(peerUrl: string): Promise<string[]> {
    return this.request<string[]>('/peers/register', {
      method: 'POST',
      body: JSON.stringify({ peer_url: peerUrl }),
    });
  }

  async unregisterPeer(peerUrl: string): Promise<string[]> {
    return this.request<string[]>('/peers/unregister', {
      method: 'POST',
      body: JSON.stringify({ peer_url: peerUrl }),
    });
  }

  async triggerSync(): Promise<boolean> {
    return this.request<boolean>('/peers/sync', {
      method: 'POST',
    });
  }
}
