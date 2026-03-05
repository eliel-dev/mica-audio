/** Per-device trend/history store for CPU and heap samples */

const MAX_TREND_SAMPLES = 20;
const MAX_HISTORY_SAMPLES = 30;

interface DeviceHistory {
  loopTrend: number[];
  loopHistory: number[];
  heapHistory: number[];
}

const store = new Map<string, DeviceHistory>();

function getOrCreate(deviceId: string): DeviceHistory {
  let entry = store.get(deviceId);
  if (!entry) {
    entry = { loopTrend: [], loopHistory: [], heapHistory: [] };
    store.set(deviceId, entry);
  }
  return entry;
}

export function pushSample(
  deviceId: string,
  loopLoadPercent: number | null,
  freeHeapBytes: number | null,
): void {
  const entry = getOrCreate(deviceId);

  if (loopLoadPercent != null) {
    const clamped = Math.max(0, Math.min(100, loopLoadPercent));
    entry.loopTrend.push(clamped);
    if (entry.loopTrend.length > MAX_TREND_SAMPLES) entry.loopTrend.shift();
    entry.loopHistory.push(clamped);
    if (entry.loopHistory.length > MAX_HISTORY_SAMPLES) entry.loopHistory.shift();
  }

  if (freeHeapBytes != null) {
    entry.heapHistory.push(freeHeapBytes);
    if (entry.heapHistory.length > MAX_HISTORY_SAMPLES) entry.heapHistory.shift();
  }
}

export function getLoopTrend(deviceId: string): number[] {
  return store.get(deviceId)?.loopTrend ?? [];
}

export function getLoopHistory(deviceId: string): number[] {
  return store.get(deviceId)?.loopHistory ?? [];
}

export function getHeapHistory(deviceId: string): number[] {
  return store.get(deviceId)?.heapHistory ?? [];
}
