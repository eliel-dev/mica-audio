<script lang="ts">
  import type { DeviceInfo } from '../lib/api';
  import { formatUptime, formatBytes } from '../lib/format';
  import { getLoopHistory, getHeapHistory } from '../lib/trend-store';

  interface Props {
    device: DeviceInfo;
    deviceId: string;
  }

  let { device, deviceId }: Props = $props();

  const hasData = $derived(device.uptimeSeconds != null);
  const loopHistory = $derived(getLoopHistory(deviceId));
  const heapHistory = $derived(getHeapHistory(deviceId));

  const HEAP_TOTAL = 327680;

  function buildPolyline(samples: number[], width: number, height: number, maxVal: number): string {
    if (samples.length === 0) return '';
    const step = samples.length > 1 ? width / (samples.length - 1) : 0;
    return samples.map((v, i) => {
      const x = i * step;
      const y = height - (Math.min(v, maxVal) / maxVal) * height;
      return `${x},${y}`;
    }).join(' ');
  }

  function buildPolygon(samples: number[], width: number, height: number, maxVal: number): string {
    const line = buildPolyline(samples, width, height, maxVal);
    if (!line) return '';
    const step = samples.length > 1 ? width / (samples.length - 1) : 0;
    const lastX = (samples.length - 1) * step;
    return `0,${height} ${line} ${lastX},${height}`;
  }

  const cpuPercent = $derived(device.loopLoadPercent ?? 0);
  const gaugeOffset = $derived(157 - (157 * cpuPercent) / 100);

  const successRate = $derived(() => {
    const recv = device.streamFramesReceived;
    const applied = device.streamFramesApplied;
    if (recv == null || applied == null || recv === 0) return '-';
    return `${((applied / recv) * 100).toFixed(1)}%`;
  });
</script>

{#if hasData}
  <div class="section">
    <div class="espdash-hd">Status em tempo real</div>

    <!-- Stat cards -->
    <div class="stat-row">
      <div class="stat-card">
        <div class="stat-lbl">Ligado ha</div>
        <div class="stat-val">{formatUptime(device.uptimeSeconds)}</div>
        <div class="stat-sub">desde o ultimo reinicio</div>
      </div>
      <div class="stat-card">
        <div class="stat-lbl">Imagens por segundo</div>
        <div>
          <span class="stat-val">
            {device.uptimeSeconds && device.streamFramesReceived
              ? Math.round(device.streamFramesReceived / device.uptimeSeconds)
              : '-'}
          </span>
          <span class="stat-unit">fps</span>
        </div>
        <div class="stat-sub">enviadas ao painel</div>
      </div>
      <div class="stat-card">
        <div class="stat-lbl">RAM disponivel</div>
        <div>
          <span class="stat-val">{device.freeHeapBytes != null ? Math.round(device.freeHeapBytes / 1024) : '-'}</span>
          <span class="stat-unit">KB</span>
        </div>
        {#if device.freeHeapBytes != null && device.largestHeapBlockBytes != null}
          <div class="stat-sub">
            Fragmentacao: {Math.round((1 - device.largestHeapBlockBytes / device.freeHeapBytes) * 100)}%
          </div>
        {/if}
      </div>
    </div>

    <!-- Line charts -->
    {#if loopHistory.length > 1 || heapHistory.length > 1}
      <div class="chart-row">
        <div class="chart-card">
          <div class="chart-lbl">Uso do processador (historico)</div>
          <svg viewBox="0 0 300 120" preserveAspectRatio="none" class="chart-svg">
            <polygon points={buildPolygon(loopHistory, 300, 120, 100)} fill="rgba(0, 120, 212, 0.15)" />
            <polyline points={buildPolyline(loopHistory, 300, 120, 100)} fill="none" stroke="#0078D4" stroke-width="2" />
          </svg>
        </div>
        <div class="chart-card">
          <div class="chart-lbl">RAM disponivel (historico)</div>
          <svg viewBox="0 0 300 120" preserveAspectRatio="none" class="chart-svg">
            <polygon points={buildPolygon(heapHistory, 300, 120, HEAP_TOTAL)} fill="rgba(78, 201, 78, 0.15)" />
            <polyline points={buildPolyline(heapHistory, 300, 120, HEAP_TOTAL)} fill="none" stroke="#4EC94E" stroke-width="2" />
          </svg>
        </div>
      </div>
    {/if}

    <!-- Gauge + Stream stats -->
    <div class="gauge-row">
      <div class="gauge-card">
        <div class="gauge-lbl">Uso do ESP32</div>
        <svg class="gauge-svg" width="120" height="80" viewBox="0 0 120 80">
          <path d="M15,70 A50,50,0,0,1,105,70" fill="none" stroke="rgba(255,255,255,0.08)" stroke-width="10" stroke-linecap="round"/>
          <path d="M15,70 A50,50,0,0,1,105,70" fill="none" stroke="#0078D4" stroke-width="10" stroke-linecap="round"
                stroke-dasharray="157" stroke-dashoffset={gaugeOffset}/>
          <text x="60" y="72" text-anchor="middle" fill="#F3F3F3" font-size="18" font-weight="300" font-family="Segoe UI, sans-serif">
            {cpuPercent}
          </text>
          <text x="60" y="84" text-anchor="middle" fill="#666" font-size="9" font-family="Segoe UI, sans-serif">%</text>
        </svg>
      </div>

      <div class="stream-card">
        <div class="chart-lbl">Envio de imagens ao painel</div>
        <div class="stream-grid">
          <div>
            <div class="stream-row">
              <span class="stream-key">Imagens enviadas</span>
              <span class="stream-val">{device.streamFramesReceived ?? '-'}</span>
            </div>
            <div class="stream-row">
              <span class="stream-key">Imagens exibidas</span>
              <span class="stream-val good">{device.streamFramesApplied ?? '-'}</span>
            </div>
            <div class="stream-row">
              <span class="stream-key">Taxa de sucesso</span>
              <span class="stream-val">{successRate()}</span>
            </div>
          </div>
          <div class="stream-right">
            <div class="stream-row">
              <span class="stream-key">Pacotes perdidos</span>
              <span class="stream-val" class:warn={device.streamSequenceGapCount != null && device.streamSequenceGapCount > 0}>
                {device.streamSequenceGapCount ?? '-'}
              </span>
            </div>
            <div class="stream-row">
              <span class="stream-key">Imagens com erro</span>
              <span class="stream-val" class:bad={device.streamInvalidFrameCount != null && device.streamInvalidFrameCount > 0}>
                {device.streamInvalidFrameCount ?? '-'}
              </span>
            </div>
            <div class="stream-row">
              <span class="stream-key">Ultima imagem no</span>
              <span class="stream-val">{device.streamLastSequence ?? '-'}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
{/if}

<style>
  .section {
    padding: 24px;
    border-top: 1px solid var(--stroke);
  }

  .espdash-hd {
    font-size: 14px;
    font-weight: 600;
    color: var(--text-1);
    margin-bottom: 16px;
  }

  .stat-row {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px;
  }

  .stat-card {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 16px;
  }

  .stat-lbl {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-2);
    margin-bottom: 8px;
  }

  .stat-val {
    font-size: 24px;
    font-weight: 600;
    line-height: 1;
    color: var(--text-1);
  }

  .stat-unit {
    font-size: 13px;
    color: var(--text-2);
    margin-left: 4px;
  }

  .stat-sub {
    font-size: 12px;
    color: var(--text-3);
    margin-top: 6px;
  }

  .chart-row {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
    margin-top: 16px;
  }

  .chart-card {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 16px;
  }

  .chart-lbl {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-2);
    margin-bottom: 12px;
  }

  .chart-svg {
    width: 100%;
    height: 120px;
    display: block;
  }

  .gauge-row {
    display: grid;
    grid-template-columns: 160px 1fr;
    gap: 12px;
    margin-top: 16px;
  }

  .gauge-card {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 12px 14px;
    display: flex;
    flex-direction: column;
    align-items: center;
  }

  .gauge-lbl {
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.07em;
    text-transform: uppercase;
    color: var(--text-3);
    margin-bottom: 8px;
    align-self: flex-start;
  }

  .gauge-svg {
    overflow: visible;
  }

  .stream-card {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 12px 14px;
  }

  .stream-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0;
  }

  .stream-right {
    padding-left: 12px;
    border-left: 1px solid var(--stroke);
  }

  .stream-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 5px 0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    font-size: 11.5px;
  }

  .stream-row:last-child {
    border-bottom: none;
  }

  .stream-key {
    color: var(--text-2);
  }

  .stream-val {
    font-weight: 600;
    color: var(--text-1);
  }

  .stream-val.good {
    color: var(--green);
  }

  .stream-val.warn {
    color: var(--yellow);
  }

  .stream-val.bad {
    color: var(--red);
  }
</style>
