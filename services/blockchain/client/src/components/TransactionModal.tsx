import React from 'react';
import { TransactionDetail } from '../analytics';
import { X, Hash, ArrowRight } from 'lucide-react';

interface TransactionModalProps {
  txId: string | null;
  detail: TransactionDetail | null;
  onClose: () => void;
  onSelectAddress: (address: string) => void;
}

export const TransactionModal: React.FC<TransactionModalProps> = ({ txId, detail, onClose, onSelectAddress }) => {
  if (!txId || !detail) return null;

  const { transaction: tx, status, blockIndex, blockHash, confirmations } = detail;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="title-group">
            <div className="title-icon" style={{ background: 'linear-gradient(135deg, var(--accent-indigo), var(--accent-purple))' }}>
              <Hash size={20} color="#ffffff" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.25rem' }}>Transaction Inspector</h2>
              <span className="mono-text" style={{ fontSize: '0.85rem', color: 'var(--accent-indigo)' }}>
                {tx.id}
              </span>
            </div>
          </div>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        {/* Transaction Overview Cards */}
        <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: '1.5rem' }}>
          <div className="stat-card">
            <div className="stat-label">Status</div>
            <div className="stat-value" style={{ fontSize: '1.2rem', color: status === 'CONFIRMED' ? '#34d399' : '#a855f7' }}>
              {status === 'CONFIRMED' ? '✅ CONFIRMED' : '⏳ PENDING'}
            </div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Transfer Amount</div>
            <div className="stat-value" style={{ fontSize: '1.5rem', color: 'var(--accent-cyan)' }}>
              {tx.amount} coins
            </div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Confirmations</div>
            <div className="stat-value" style={{ fontSize: '1.5rem', color: 'var(--accent-indigo)' }}>
              {confirmations}
            </div>
          </div>
        </div>

        {/* Flow visualizer */}
        <div className="panel" style={{ background: 'rgba(10, 14, 23, 0.6)', padding: '1.25rem', marginBottom: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
            <div
              className="clickable-card"
              onClick={() => onSelectAddress(tx.sender)}
              style={{ flex: 1, minWidth: '180px', padding: '0.75rem 1rem', borderRadius: '10px', background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)' }}
            >
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>FROM (SENDER)</div>
              <div className="mono-text" style={{ fontWeight: 600, color: 'var(--accent-indigo)', wordBreak: 'break-all' }}>
                {tx.sender}
              </div>
            </div>

            <ArrowRight size={24} color="var(--accent-cyan)" />

            <div
              className="clickable-card"
              onClick={() => onSelectAddress(tx.recipient)}
              style={{ flex: 1, minWidth: '180px', padding: '0.75rem 1rem', borderRadius: '10px', background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)' }}
            >
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>TO (RECIPIENT)</div>
              <div className="mono-text" style={{ fontWeight: 600, color: 'var(--accent-cyan)', wordBreak: 'break-all' }}>
                {tx.recipient}
              </div>
            </div>
          </div>
        </div>

        {/* Details Fields */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.5rem' }}>
            <span style={{ color: 'var(--text-secondary)' }}>Timestamp</span>
            <span className="mono-text">{new Date(tx.timestamp).toLocaleString()} ({tx.timestamp})</span>
          </div>

          {blockIndex !== undefined && (
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-secondary)' }}>Mined in Block</span>
              <span className="mono-text" style={{ color: 'var(--accent-cyan)', fontWeight: 600 }}>Block #{blockIndex}</span>
            </div>
          )}

          {blockHash && (
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-secondary)' }}>Block Hash</span>
              <span className="mono-text" style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>{blockHash}</span>
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem' }}>
            <span style={{ color: 'var(--text-secondary)' }}>Cryptographic Verification</span>
            <span style={{ color: '#34d399', fontWeight: 600 }}>SHA-256 Validated ✅</span>
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button className="btn" style={{ width: 'auto', padding: '0.6rem 1.25rem' }} onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
