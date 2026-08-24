import React, { useState } from 'react';
import { Network, Plus, Trash2, RefreshCw, Server, ShieldCheck, Globe } from 'lucide-react';

interface PeersTabProps {
  peers: string[];
  onRegisterPeer: (peerUrl: string) => Promise<void>;
  onUnregisterPeer: (peerUrl: string) => Promise<void>;
  onTriggerSync: () => Promise<void>;
  syncing: boolean;
}

export const PeersTab: React.FC<PeersTabProps> = ({
  peers,
  onRegisterPeer,
  onUnregisterPeer,
  onTriggerSync,
  syncing,
}) => {
  const [newPeerUrl, setNewPeerUrl] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPeerUrl.trim()) return;

    try {
      setSubmitting(true);
      await onRegisterPeer(newPeerUrl);
      setNewPeerUrl('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* P2P Header Banner */}
      <div className="panel" style={{ background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.15), rgba(6, 182, 212, 0.15))' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.85rem' }}>
            <div className="title-icon" style={{ background: 'linear-gradient(135deg, var(--accent-indigo), var(--accent-cyan))' }}>
              <Network size={22} color="#ffffff" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>Peer-to-Peer Network Mesh</h2>
              <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                Nakamoto consensus engine with automatic block propagation & longest-chain resolution
              </p>
            </div>
          </div>

          <button className="btn" style={{ width: 'auto', padding: '0.7rem 1.25rem' }} onClick={onTriggerSync} disabled={syncing}>
            <RefreshCw size={16} className={syncing ? 'spin' : ''} />
            {syncing ? 'Resolving Consensus...' : 'Sync Peer Consensus'}
          </button>
        </div>
      </div>

      <div className="grid-panels">
        {/* Panel 1: Register New Peer Node */}
        <div className="panel">
          <div className="panel-header">
            <div className="panel-title">
              <Plus size={18} color="var(--accent-cyan)" /> Connect New Peer Node
            </div>
          </div>

          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label>Peer Node URL</label>
              <input
                type="text"
                placeholder="e.g. http://localhost:3004"
                value={newPeerUrl}
                onChange={(e) => setNewPeerUrl(e.target.value)}
                required
              />
            </div>

            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: '1rem' }}>
              💡 Enter another running Rust node's HTTP URL (e.g. <code>http://localhost:3004</code>). Both nodes will exchange and validate blocks.
            </div>

            <button type="submit" className="btn" disabled={submitting}>
              {submitting ? 'Connecting Peer...' : 'Register Peer Node'}
            </button>
          </form>
        </div>

        {/* Panel 2: Connected Peer List */}
        <div className="panel">
          <div className="panel-header">
            <div className="panel-title">
              <Globe size={18} color="var(--accent-emerald)" /> Connected Peer Mesh ({peers.length})
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {peers.length === 0 ? (
              <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem', textAlign: 'center', padding: '2rem 1rem' }}>
                No active peers connected. Register a peer URL to form a P2P network!
              </div>
            ) : (
              peers.map((peer) => (
                <div
                  key={peer}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '0.85rem 1rem',
                    background: 'rgba(10, 14, 23, 0.6)',
                    border: '1px solid var(--border-color)',
                    borderRadius: '12px',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <Server size={18} color="var(--accent-cyan)" />
                    <div>
                      <div className="mono-text" style={{ fontSize: '0.9rem', fontWeight: 600, color: 'var(--text-primary)' }}>
                        {peer}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: '#34d399', display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                        <ShieldCheck size={12} /> Active P2P Node
                      </div>
                    </div>
                  </div>

                  <button
                    className="icon-btn"
                    title="Disconnect Peer"
                    onClick={() => onUnregisterPeer(peer)}
                  >
                    <Trash2 size={16} color="var(--accent-rose)" />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
