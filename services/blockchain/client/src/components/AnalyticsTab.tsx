import React from 'react';
import { MinerLeaderboardItem, ThroughputMetrics, MempoolAgingMetrics } from '../analytics';
import { Trophy, Zap, Clock, Activity, BarChart2, AlertCircle } from 'lucide-react';

interface AnalyticsTabProps {
  minerLeaderboard: MinerLeaderboardItem[];
  throughput: ThroughputMetrics;
  mempoolAging: MempoolAgingMetrics;
  onSelectAddress: (address: string) => void;
}

export const AnalyticsTab: React.FC<AnalyticsTabProps> = ({
  minerLeaderboard,
  throughput,
  mempoolAging,
  onSelectAddress,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
      {/* 1. Network Performance & Mempool Health Cards */}
      <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        <div className="stat-card">
          <div className="stat-label">
            <Zap size={16} color="var(--accent-cyan)" /> Total Transacted Volume
          </div>
          <div className="stat-value" style={{ color: 'var(--accent-cyan)' }}>
            {throughput.totalVolume.toLocaleString()} coins
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-label">
            <Clock size={16} color="var(--accent-indigo)" /> Avg Block Time
          </div>
          <div className="stat-value">
            {throughput.avgBlockTimeSeconds} sec
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-label">
            <Activity size={16} color="var(--accent-purple)" /> Avg Tx / Block
          </div>
          <div className="stat-value">
            {throughput.avgTxPerBlock} txs
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-label">
            <AlertCircle size={16} color="var(--accent-emerald)" /> Mempool Health
          </div>
          <div
            className="stat-value"
            style={{
              fontSize: '1.25rem',
              color:
                mempoolAging.healthStatus === 'OPTIMAL'
                  ? '#34d399'
                  : mempoolAging.healthStatus === 'MODERATE'
                  ? '#fbbf24'
                  : '#f87171',
            }}
          >
            {mempoolAging.healthStatus}
          </div>
        </div>
      </div>

      {/* 2. Mempool Aging Breakdown */}
      <div className="panel">
        <div className="panel-header">
          <div className="panel-title">
            <Clock size={20} color="var(--accent-purple)" /> Mempool Aging & Queue Metrics
          </div>
        </div>
        <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: 0 }}>
          <div style={{ background: 'rgba(10, 14, 23, 0.5)', padding: '1rem', borderRadius: '12px' }}>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Pending Queue Size</div>
            <div className="mono-text" style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--accent-purple)' }}>
              {mempoolAging.pendingCount} transactions
            </div>
          </div>
          <div style={{ background: 'rgba(10, 14, 23, 0.5)', padding: '1rem', borderRadius: '12px' }}>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Oldest Pending Tx Age</div>
            <div className="mono-text" style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--accent-cyan)' }}>
              {mempoolAging.oldestTxAgeSeconds}s
            </div>
          </div>
          <div style={{ background: 'rgba(10, 14, 23, 0.5)', padding: '1rem', borderRadius: '12px' }}>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Total Pending Volume</div>
            <div className="mono-text" style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--accent-emerald)' }}>
              {mempoolAging.totalPendingVolume} coins
            </div>
          </div>
        </div>
      </div>

      <div className="grid-panels">
        {/* 3. Miner Leaderboard Panel */}
        <div className="panel">
          <div className="panel-header">
            <div className="panel-title">
              <Trophy size={20} color="#f59e0b" /> Miner Proof-of-Work Leaderboard
            </div>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table className="leaderboard-table">
              <thead>
                <tr>
                  <th>Rank</th>
                  <th>Miner Address</th>
                  <th>Blocks Mined</th>
                  <th>Rewards Earned</th>
                  <th>Chain Share</th>
                </tr>
              </thead>
              <tbody>
                {minerLeaderboard.length === 0 ? (
                  <tr>
                    <td colSpan={5} style={{ textTransform: 'none', textAlign: 'center', padding: '1.5rem', color: 'var(--text-muted)' }}>
                      No mined blocks recorded yet.
                    </td>
                  </tr>
                ) : (
                  minerLeaderboard.map((item, index) => (
                    <tr key={item.address}>
                      <td style={{ fontWeight: 700, color: index === 0 ? '#f59e0b' : 'var(--text-primary)' }}>
                        #{index + 1} {index === 0 && '👑'}
                      </td>
                      <td>
                        <span className="mono-text clickable" onClick={() => onSelectAddress(item.address)} style={{ color: 'var(--accent-cyan)' }}>
                          {item.address}
                        </span>
                      </td>
                      <td className="mono-text" style={{ fontWeight: 600 }}>{item.blocksMined}</td>
                      <td className="mono-text" style={{ color: 'var(--accent-emerald)', fontWeight: 600 }}>+{item.rewardsEarned} coins</td>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                          <div className="progress-bar">
                            <div className="progress-fill" style={{ width: `${item.sharePercent}%` }} />
                          </div>
                          <span className="mono-text" style={{ fontSize: '0.8rem' }}>{item.sharePercent}%</span>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* 4. Block Density & Throughput Chart */}
        <div className="panel">
          <div className="panel-header">
            <div className="panel-title">
              <BarChart2 size={20} color="var(--accent-indigo)" /> Block Transaction Density
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            {throughput.blockDensity.length === 0 ? (
              <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem', textAlign: 'center', padding: '1.5rem' }}>
                No block density metrics available.
              </div>
            ) : (
              throughput.blockDensity.slice(-8).map((b) => {
                const maxTx = Math.max(1, ...throughput.blockDensity.map((x) => x.txCount));
                const heightPct = Math.max(12, Number(((b.txCount / maxTx) * 100).toFixed(0)));
                return (
                  <div key={b.index} style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <span className="mono-text" style={{ fontSize: '0.8rem', minWidth: '70px', color: 'var(--accent-cyan)' }}>
                      Block #{b.index}
                    </span>
                    <div style={{ flex: 1, background: 'rgba(10,14,23,0.6)', borderRadius: '6px', height: '24px', overflow: 'hidden', padding: '2px' }}>
                      <div
                        style={{
                          height: '100%',
                          width: `${heightPct}%`,
                          background: 'linear-gradient(90deg, var(--accent-indigo), var(--accent-purple))',
                          borderRadius: '4px',
                          transition: 'width 0.3s ease',
                        }}
                      />
                    </div>
                    <span className="mono-text" style={{ fontSize: '0.8rem', minWidth: '55px', textAlign: 'right' }}>
                      {b.txCount} txs
                    </span>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
