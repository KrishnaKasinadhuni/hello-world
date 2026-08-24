import { Block, Transaction } from './sdk';

export interface MinerLeaderboardItem {
  address: string;
  blocksMined: number;
  rewardsEarned: number;
  sharePercent: number;
  lastMinedTimestamp: number;
}

export interface ThroughputMetrics {
  totalVolume: number;
  totalTransactions: number;
  avgBlockTimeSeconds: number;
  avgTxPerBlock: number;
  blockDensity: { index: number; txCount: number; timestamp: number }[];
}

export interface MempoolAgingMetrics {
  pendingCount: number;
  totalPendingVolume: number;
  oldestTxAgeSeconds: number;
  avgTxAgeSeconds: number;
  healthStatus: 'OPTIMAL' | 'MODERATE' | 'CONGESTED';
}

export interface AddressDetails {
  address: string;
  balance: number;
  sentCount: number;
  receivedCount: number;
  totalSentVolume: number;
  totalReceivedVolume: number;
  transactions: (Transaction & { blockIndex?: number; status: 'CONFIRMED' | 'PENDING' })[];
}

export interface TransactionDetail {
  transaction: Transaction;
  status: 'CONFIRMED' | 'PENDING';
  blockIndex?: number;
  blockHash?: string;
  confirmations: number;
}

export function getMinerLeaderboard(blocks: Block[]): MinerLeaderboardItem[] {
  const minerMap = new Map<string, { blocksMined: number; rewardsEarned: number; lastMinedTimestamp: number }>();
  let totalMinedBlocks = 0;

  for (const block of blocks) {
    // Reward tx is sender = SYSTEM
    const rewardTx = block.transactions.find((tx) => tx.sender === 'SYSTEM');
    if (rewardTx) {
      const miner = rewardTx.recipient;
      totalMinedBlocks++;
      const current = minerMap.get(miner) || { blocksMined: 0, rewardsEarned: 0, lastMinedTimestamp: 0 };
      minerMap.set(miner, {
        blocksMined: current.blocksMined + 1,
        rewardsEarned: current.rewardsEarned + rewardTx.amount,
        lastMinedTimestamp: Math.max(current.lastMinedTimestamp, block.timestamp),
      });
    }
  }

  const leaderboard: MinerLeaderboardItem[] = [];
  minerMap.forEach((val, address) => {
    leaderboard.push({
      address,
      blocksMined: val.blocksMined,
      rewardsEarned: val.rewardsEarned,
      sharePercent: totalMinedBlocks > 0 ? Number(((val.blocksMined / totalMinedBlocks) * 100).toFixed(1)) : 0,
      lastMinedTimestamp: val.lastMinedTimestamp,
    });
  });

  return leaderboard.sort((a, b) => b.blocksMined - a.blocksMined);
}

export function getThroughputMetrics(blocks: Block[]): ThroughputMetrics {
  let totalVolume = 0;
  let totalTransactions = 0;
  const blockTimes: number[] = [];

  const blockDensity = blocks.map((b) => ({
    index: b.index,
    txCount: b.transactions.length,
    timestamp: b.timestamp,
  }));

  for (let i = 0; i < blocks.length; i++) {
    const block = blocks[i];
    for (const tx of block.transactions) {
      totalTransactions++;
      if (tx.sender !== 'SYSTEM') {
        totalVolume += tx.amount;
      }
    }

    if (i > 0) {
      const timeDiff = Math.abs(blocks[i].timestamp - blocks[i - 1].timestamp) / 1000;
      if (timeDiff < 86400) { // filter outliers
        blockTimes.push(timeDiff);
      }
    }
  }

  const avgBlockTimeSeconds = blockTimes.length > 0
    ? Number((blockTimes.reduce((a, b) => a + b, 0) / blockTimes.length).toFixed(1))
    : 0;

  const avgTxPerBlock = blocks.length > 0
    ? Number((totalTransactions / blocks.length).toFixed(1))
    : 0;

  return {
    totalVolume,
    totalTransactions,
    avgBlockTimeSeconds,
    avgTxPerBlock,
    blockDensity,
  };
}

export function getMempoolAgingMetrics(pending: Transaction[]): MempoolAgingMetrics {
  const now = Date.now();
  let totalPendingVolume = 0;
  let oldestAge = 0;
  let totalAgeSum = 0;

  for (const tx of pending) {
    totalPendingVolume += tx.amount;
    const ageSec = Math.max(0, (now - tx.timestamp) / 1000);
    oldestAge = Math.max(oldestAge, ageSec);
    totalAgeSum += ageSec;
  }

  const avgTxAgeSeconds = pending.length > 0 ? Number((totalAgeSum / pending.length).toFixed(1)) : 0;
  const oldestTxAgeSeconds = Number(oldestAge.toFixed(1));

  let healthStatus: 'OPTIMAL' | 'MODERATE' | 'CONGESTED' = 'OPTIMAL';
  if (pending.length > 15 || oldestTxAgeSeconds > 300) {
    healthStatus = 'CONGESTED';
  } else if (pending.length > 5 || oldestTxAgeSeconds > 60) {
    healthStatus = 'MODERATE';
  }

  return {
    pendingCount: pending.length,
    totalPendingVolume,
    oldestTxAgeSeconds,
    avgTxAgeSeconds,
    healthStatus,
  };
}

export function getAddressDetails(address: string, blocks: Block[], pending: Transaction[]): AddressDetails {
  let sentCount = 0;
  let receivedCount = 0;
  let totalSentVolume = 0;
  let totalReceivedVolume = 0;
  const history: (Transaction & { blockIndex?: number; status: 'CONFIRMED' | 'PENDING' })[] = [];

  // Inspect confirmed blocks
  for (const block of blocks) {
    for (const tx of block.transactions) {
      if (tx.sender === address || tx.recipient === address) {
        history.push({ ...tx, blockIndex: block.index, status: 'CONFIRMED' });
        if (tx.sender === address) {
          sentCount++;
          totalSentVolume += tx.amount;
        }
        if (tx.recipient === address) {
          receivedCount++;
          totalReceivedVolume += tx.amount;
        }
      }
    }
  }

  // Inspect pending transactions
  for (const tx of pending) {
    if (tx.sender === address || tx.recipient === address) {
      history.push({ ...tx, status: 'PENDING' });
      if (tx.sender === address) {
        sentCount++;
        totalSentVolume += tx.amount;
      }
      if (tx.recipient === address) {
        receivedCount++;
        totalReceivedVolume += tx.amount;
      }
    }
  }

  const balance = Math.max(0, totalReceivedVolume - totalSentVolume);

  return {
    address,
    balance,
    sentCount,
    receivedCount,
    totalSentVolume,
    totalReceivedVolume,
    transactions: history.reverse(),
  };
}

export function findTransactionById(
  txId: string,
  blocks: Block[],
  pending: Transaction[]
): TransactionDetail | null {
  const latestBlockIndex = blocks.length > 0 ? Math.max(...blocks.map((b) => b.index)) : 0;

  // Check pending
  const pendingTx = pending.find((t) => t.id === txId);
  if (pendingTx) {
    return {
      transaction: pendingTx,
      status: 'PENDING',
      confirmations: 0,
    };
  }

  // Check confirmed blocks
  for (const block of blocks) {
    const found = block.transactions.find((t) => t.id === txId);
    if (found) {
      return {
        transaction: found,
        status: 'CONFIRMED',
        blockIndex: block.index,
        blockHash: block.hash,
        confirmations: latestBlockIndex - block.index + 1,
      };
    }
  }

  return null;
}
