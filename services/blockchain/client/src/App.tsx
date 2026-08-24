import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { BlockchainNodeClient, Block, NodeStatus, Transaction } from './sdk';
import {
  getMinerLeaderboard,
  getThroughputMetrics,
  getMempoolAgingMetrics,
  getAddressDetails,
  findTransactionById,
} from './analytics';
import { AddressModal } from './components/AddressModal';
import { TransactionModal } from './components/TransactionModal';
import { AnalyticsTab } from './components/AnalyticsTab';
import { PeersTab } from './components/PeersTab';
import {
  Blocks,
  Pickaxe,
  Send,
  ShieldCheck,
  RefreshCw,
  Wallet,
  Activity,
  BarChart3,
  Search,
  LayoutDashboard,
  SearchCode,
  Network,
} from 'lucide-react';

const client = new BlockchainNodeClient();

type TabType = 'OVERVIEW' | 'EXPLORER' | 'ANALYTICS' | 'PEERS';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<TabType>('OVERVIEW');
  const [status, setStatus] = useState<NodeStatus | null>(null);
  const [blocks, setBlocks] = useState<Block[]>([]);
  const [pendingTxs, setPendingTxs] = useState<Transaction[]>([]);
  const [peers, setPeers] = useState<string[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [mining, setMining] = useState<boolean>(false);
  const [syncing, setSyncing] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Form states
  const [sender, setSender] = useState<string>('alice');
  const [recipient, setRecipient] = useState<string>('bob');
  const [amount, setAmount] = useState<number>(50);
  const [minerAddress, setMinerAddress] = useState<string>('charlie');

  // Search state
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Modals state
  const [selectedAddress, setSelectedAddress] = useState<string | null>(null);
  const [selectedTxId, setSelectedTxId] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [statusData, blocksData, pendingData, peersData] = await Promise.all([
        client.getStatus(),
        client.getBlocks(),
        client.getPending(),
        client.getPeers().catch(() => []),
      ]);
      setStatus(statusData);
      setBlocks(blocksData.reverse());
      setPendingTxs(pendingData);
      setPeers(peersData);
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to connect to Rust node RPC';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Analytics computations
  const minerLeaderboard = useMemo(() => getMinerLeaderboard(blocks), [blocks]);
  const throughput = useMemo(() => getThroughputMetrics(blocks), [blocks]);
  const mempoolAging = useMemo(() => getMempoolAgingMetrics(pendingTxs), [pendingTxs]);

  const selectedAddressDetails = useMemo(() => {
    if (!selectedAddress) return null;
    return getAddressDetails(selectedAddress, blocks, pendingTxs);
  }, [selectedAddress, blocks, pendingTxs]);

  const selectedTxDetail = useMemo(() => {
    if (!selectedTxId) return null;
    return findTransactionById(selectedTxId, blocks, pendingTxs);
  }, [selectedTxId, blocks, pendingTxs]);

  const handleSendTransaction = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!sender || !recipient || amount <= 0) {
      setMessage({ text: 'Please fill out all transaction fields with valid values', type: 'error' });
      return;
    }

    try {
      setSubmitting(true);
      await client.sendTransaction(sender, recipient, amount);
      setMessage({ text: `Transaction of ${amount} coins added to pool & gossiped to peers!`, type: 'success' });
      await fetchData();
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Transaction failed';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setSubmitting(false);
    }
  };

  const handleMineBlock = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!minerAddress.trim()) {
      setMessage({ text: 'Miner address cannot be empty', type: 'error' });
      return;
    }

    try {
      setMining(true);
      const minedBlock = await client.mineBlock(minerAddress);
      setMessage({ text: `Successfully mined Block #${minedBlock.index}! Gossiped to ${peers.length} peers`, type: 'success' });
      await fetchData();
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Mining failed';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setMining(false);
    }
  };

  const handleRegisterPeer = async (peerUrl: string) => {
    try {
      const updatedPeers = await client.registerPeer(peerUrl);
      setPeers(updatedPeers);
      setMessage({ text: `Registered peer ${peerUrl} successfully!`, type: 'success' });
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to register peer';
      setMessage({ text: errorMsg, type: 'error' });
    }
  };

  const handleUnregisterPeer = async (peerUrl: string) => {
    try {
      const updatedPeers = await client.unregisterPeer(peerUrl);
      setPeers(updatedPeers);
      setMessage({ text: `Disconnected peer ${peerUrl}`, type: 'success' });
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to unregister peer';
      setMessage({ text: errorMsg, type: 'error' });
    }
  };

  const handleTriggerSync = async () => {
    try {
      setSyncing(true);
      const res = await client.triggerSync();
      setMessage({
        text: res ? 'Consensus sync completed: Chain updated to peer chain!' : 'Consensus sync completed: Local chain is authoritative',
        type: 'success',
      });
      await fetchData();
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Sync failed';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setSyncing(false);
    }
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    const query = searchQuery.trim();
    if (!query) return;

    const tx = findTransactionById(query, blocks, pendingTxs);
    if (tx) {
      setSelectedTxId(query);
      return;
    }

    setSelectedAddress(query);
  };

  return (
    <div className="container">
      {/* Top Header */}
      <header className="header">
        <div className="title-group">
          <div className="title-icon">
            <Blocks size={24} color="#ffffff" />
          </div>
          <div>
            <h1>Rust & TS 7 P2P Blockchain Sandbox</h1>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
              Rust Nakamoto P2P Engine (Axum/Tokio) + TypeScript 7 Client SDK
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <form onSubmit={handleSearch} style={{ display: 'flex', gap: '0.4rem' }}>
            <input
              type="text"
              placeholder="Search address or tx hash..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{ width: '220px', padding: '0.5rem 0.75rem', fontSize: '0.8rem' }}
            />
            <button type="submit" className="btn" style={{ width: 'auto', padding: '0.5rem 0.75rem' }}>
              <Search size={14} />
            </button>
          </form>
          <span className="badge-ts7">TypeScript 7.0</span>
          <button className="btn" style={{ padding: '0.5rem 0.85rem', width: 'auto' }} onClick={fetchData} disabled={loading}>
            <RefreshCw size={16} className={loading ? 'spin' : ''} />
          </button>
        </div>
      </header>

      {/* Tabs Navigation Bar */}
      <nav className="tab-bar">
        <button
          className={`tab-item ${activeTab === 'OVERVIEW' ? 'active' : ''}`}
          onClick={() => setActiveTab('OVERVIEW')}
        >
          <LayoutDashboard size={16} /> Console Overview
        </button>
        <button
          className={`tab-item ${activeTab === 'EXPLORER' ? 'active' : ''}`}
          onClick={() => setActiveTab('EXPLORER')}
        >
          <SearchCode size={16} /> Block Explorer ({blocks.length})
        </button>
        <button
          className={`tab-item ${activeTab === 'ANALYTICS' ? 'active' : ''}`}
          onClick={() => setActiveTab('ANALYTICS')}
        >
          <BarChart3 size={16} /> Analytics & Leaderboard
        </button>
        <button
          className={`tab-item ${activeTab === 'PEERS' ? 'active' : ''}`}
          onClick={() => setActiveTab('PEERS')}
        >
          <Network size={16} /> P2P Network ({peers.length} Peers)
        </button>
      </nav>

      {message && (
        <div className={`alert-toast ${message.type === 'success' ? 'alert-success' : 'alert-error'}`}>
          {message.text}
        </div>
      )}

      {/* TAB 1: OVERVIEW */}
      {activeTab === 'OVERVIEW' && (
        <>
          <div className="grid-stats">
            <div className="stat-card">
              <div className="stat-label">
                <Blocks size={16} color="var(--accent-cyan)" /> Chain Height
              </div>
              <div className="stat-value">{status?.blocks_count ?? 0}</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">
                <Activity size={16} color="var(--accent-purple)" /> Mempool Queue
              </div>
              <div className="stat-value">{status?.pending_transactions_count ?? 0}</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">
                <Network size={16} color="var(--accent-cyan)" /> Connected Peers
              </div>
              <div className="stat-value" style={{ color: 'var(--accent-cyan)' }}>{peers.length}</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">
                <ShieldCheck size={16} color="var(--accent-emerald)" /> Chain Integrity
              </div>
              <div className="stat-value" style={{ fontSize: '1.25rem', color: status?.is_valid ? '#34d399' : '#f87171' }}>
                {status?.is_valid ? 'VALID ✅' : 'INVALID ❌'}
              </div>
            </div>
          </div>

          <div className="grid-panels">
            <div className="panel">
              <div className="panel-header">
                <div className="panel-title">
                  <Send size={18} color="var(--accent-indigo)" /> Broadcast Transaction
                </div>
              </div>
              <form onSubmit={handleSendTransaction}>
                <div className="form-group">
                  <label>Sender Address</label>
                  <input type="text" value={sender} onChange={(e) => setSender(e.target.value)} required />
                </div>
                <div className="form-group">
                  <label>Recipient Address</label>
                  <input type="text" value={recipient} onChange={(e) => setRecipient(e.target.value)} required />
                </div>
                <div className="form-group">
                  <label>Amount (Coins)</label>
                  <input type="number" min="1" value={amount} onChange={(e) => setAmount(Number(e.target.value))} required />
                </div>
                <button type="submit" className="btn" disabled={submitting}>
                  {submitting ? 'Signing & Broadcasting...' : 'Submit Transaction'}
                </button>
              </form>
            </div>

            <div className="panel">
              <div className="panel-header">
                <div className="panel-title">
                  <Pickaxe size={18} color="var(--accent-emerald)" /> Mine Block & Solves
                </div>
              </div>
              <form onSubmit={handleMineBlock}>
                <div className="form-group">
                  <label>Miner Reward Address</label>
                  <input type="text" value={minerAddress} onChange={(e) => setMinerAddress(e.target.value)} required />
                </div>
                <button type="submit" className="btn btn-mine" disabled={mining}>
                  {mining ? 'Solving PoW Nonce...' : `Mine Block (${status?.mining_reward ?? 50} Coin Reward)`}
                </button>
              </form>

              <div style={{ marginTop: '1.5rem', paddingTop: '1rem', borderTop: '1px solid var(--border-color)' }}>
                <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)', marginBottom: '0.5rem' }}>
                  Quick Address Inspector
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem' }}>
                  {['genesis_account', 'alice', 'bob', 'charlie'].map((acc) => (
                    <button
                      key={acc}
                      className="tab-item"
                      style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem', background: 'rgba(255,255,255,0.04)' }}
                      onClick={() => setSelectedAddress(acc)}
                    >
                      <Wallet size={12} color="var(--accent-cyan)" /> {acc}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </>
      )}

      {/* TAB 2: EXPLORER */}
      {activeTab === 'EXPLORER' && (
        <div className="panel">
          <div className="panel-header">
            <div className="panel-title">
              <Blocks size={20} color="var(--accent-cyan)" /> Block Ledger Explorer ({blocks.length} Blocks)
            </div>
          </div>

          <div className="block-list">
            {blocks.map((block) => (
              <div key={block.hash} className="block-card">
                <div className="block-header">
                  <span className="block-number">Block #{block.index}</span>
                  <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                    Nonce: {block.nonce} | {new Date(block.timestamp).toLocaleTimeString()}
                  </span>
                </div>
                <div className="block-hash">Hash: {block.hash}</div>
                <div className="block-hash" style={{ opacity: 0.7 }}>Prev: {block.previous_hash}</div>
                <div>
                  <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginBottom: '0.4rem' }}>
                    Transactions ({block.transactions.length}):
                  </div>
                  {block.transactions.map((tx) => (
                    <div key={tx.id} className="tx-chip" style={{ padding: '0.6rem 0.85rem' }}>
                      <span>
                        <strong className="clickable" onClick={() => setSelectedAddress(tx.sender)}>
                          {tx.sender}
                        </strong>{' '}
                        ➔{' '}
                        <strong className="clickable" onClick={() => setSelectedAddress(tx.recipient)}>
                          {tx.recipient}
                        </strong>
                      </span>
                      <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--accent-emerald)', fontWeight: 600 }}>
                        +{tx.amount} coins
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 3: ANALYTICS & LEADERBOARD */}
      {activeTab === 'ANALYTICS' && (
        <AnalyticsTab
          minerLeaderboard={minerLeaderboard}
          throughput={throughput}
          mempoolAging={mempoolAging}
          onSelectAddress={(addr) => setSelectedAddress(addr)}
        />
      )}

      {/* TAB 4: PEERS & NETWORK MESH */}
      {activeTab === 'PEERS' && (
        <PeersTab
          peers={peers}
          onRegisterPeer={handleRegisterPeer}
          onUnregisterPeer={handleUnregisterPeer}
          onTriggerSync={handleTriggerSync}
          syncing={syncing}
        />
      )}

      {/* Modals */}
      <AddressModal
        address={selectedAddress}
        details={selectedAddressDetails}
        onClose={() => setSelectedAddress(null)}
        onSelectTx={(id) => setSelectedTxId(id)}
      />

      <TransactionModal
        txId={selectedTxId}
        detail={selectedTxDetail}
        onClose={() => setSelectedTxId(null)}
        onSelectAddress={(addr) => setSelectedAddress(addr)}
      />
    </div>
  );
};
