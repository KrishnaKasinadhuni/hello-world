import React from 'react';
import { AddressDetails } from '../analytics';
import { X, Wallet, ArrowUpRight, ArrowDownLeft, History } from 'lucide-react';

interface AddressModalProps {
  address: string | null;
  details: AddressDetails | null;
  onClose: () => void;
  onSelectTx: (txId: string) => void;
}

export const AddressModal: React.FC<AddressModalProps> = ({ address, details, onClose, onSelectTx }) => {
  if (!address || !details) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="title-group">
            <div className="title-icon" style={{ background: 'linear-gradient(135deg, var(--accent-cyan), var(--accent-indigo))' }}>
              <Wallet size={20} color="#ffffff" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.25rem' }}>Address Inspector</h2>
              <span className="mono-text" style={{ fontSize: '0.85rem', color: 'var(--accent-cyan)' }}>
                {details.address}
              </span>
            </div>
          </div>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        {/* Address Stats Overview */}
        <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: '1.5rem' }}>
          <div className="stat-card">
            <div className="stat-label">Balance</div>
            <div className="stat-value" style={{ color: 'var(--accent-emerald)', fontSize: '1.5rem' }}>
              {details.balance} coins
            </div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Total Received</div>
            <div className="stat-value" style={{ fontSize: '1.5rem', color: 'var(--accent-cyan)' }}>
              +{details.totalReceivedVolume}
            </div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Total Sent</div>
            <div className="stat-value" style={{ fontSize: '1.5rem', color: 'var(--accent-rose)' }}>
              -{details.totalSentVolume}
            </div>
          </div>
        </div>

        {/* Transaction History List */}
        <div style={{ marginBottom: '1rem' }}>
          <div className="panel-title" style={{ fontSize: '1rem', marginBottom: '0.75rem' }}>
            <History size={16} color="var(--accent-purple)" /> Activity History ({details.transactions.length})
          </div>
          <div className="block-list" style={{ maxHeight: '320px', overflowY: 'auto' }}>
            {details.transactions.length === 0 ? (
              <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem', padding: '1rem', textAlign: 'center' }}>
                No transactions recorded for this address yet.
              </div>
            ) : (
              details.transactions.map((tx) => {
                const isRecipient = tx.recipient === details.address;
                return (
                  <div
                    key={tx.id}
                    className="tx-chip clickable"
                    onClick={() => onSelectTx(tx.id)}
                    style={{ padding: '0.75rem 1rem' }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                      {isRecipient ? (
                        <ArrowDownLeft size={18} color="var(--accent-emerald)" />
                      ) : (
                        <ArrowUpRight size={18} color="var(--accent-rose)" />
                      )}
                      <div>
                        <div style={{ fontSize: '0.85rem' }}>
                          {isRecipient ? (
                            <span>Received from <strong>{tx.sender}</strong></span>
                          ) : (
                            <span>Sent to <strong>{tx.recipient}</strong></span>
                          )}
                        </div>
                        <div className="mono-text" style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          ID: {tx.id.substring(0, 16)}...
                        </div>
                      </div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <span className="mono-text" style={{ fontWeight: 600, color: isRecipient ? 'var(--accent-emerald)' : 'var(--accent-rose)' }}>
                        {isRecipient ? `+${tx.amount}` : `-${tx.amount}`} coins
                      </span>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        {tx.status === 'CONFIRMED' ? `Block #${tx.blockIndex}` : '⏳ Pending'}
                      </div>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', paddingTop: '1rem', borderTop: '1px solid var(--border-color)' }}>
          <button className="btn" style={{ width: 'auto', padding: '0.6rem 1.25rem' }} onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
