<script lang="ts">
  import type { DeviceInfo } from '../lib/api';
  import { computePercent } from '../lib/format';

  interface Props {
    device: DeviceInfo;
  }

  let { device }: Props = $props();

  const HEAP_TOTAL = 327680;
  const PSRAM_TOTAL = 8388608;

  const cpuPercent = $derived(device.loopLoadPercent ?? null);
  const heapPercent = $derived(computePercent(device.freeHeapBytes, HEAP_TOTAL));
  const psramPercent = $derived(computePercent(device.freePsramBytes, PSRAM_TOTAL));
  const hasMetrics = $derived(device.loopLoadPercent != null);

  function cpuColor(pct: number): string {
    if (pct > 70) return '#C42B1C';
    if (pct > 40) return '#E8A000';
    return '#0078D4';
  }
</script>

{#if hasMetrics}
  <div class="section">
    <div class="sec-hd">Metricas</div>
    <div class="metrics-grid">
      <div class="m-cell">
        <div class="m-lbl">Uso do processador</div>
        <div><span class="m-val">{cpuPercent ?? '-'}</span><span class="m-unit">%</span></div>
        <div class="prog">
          <div class="prog-f" style="width: {cpuPercent ?? 0}%; background: {cpuColor(cpuPercent ?? 0)};"></div>
        </div>
      </div>
      <div class="m-cell">
        <div class="m-lbl">Memoria RAM livre</div>
        <div><span class="m-val">{heapPercent ?? '-'}</span><span class="m-unit">%</span></div>
        <div class="prog">
          <div class="prog-f" style="width: {heapPercent ?? 0}%; background: #4EC94E;"></div>
        </div>
        {#if device.freeHeapBytes != null && device.largestHeapBlockBytes != null}
          <div class="m-sub">
            {Math.round(device.freeHeapBytes / 1024)} KB livre | Maior bloco: {Math.round(device.largestHeapBlockBytes / 1024)} KB
          </div>
        {/if}
      </div>
      <div class="m-cell">
        <div class="m-lbl">Memoria extra livre</div>
        <div><span class="m-val">{psramPercent ?? '-'}</span><span class="m-unit">%</span></div>
        <div class="prog">
          <div class="prog-f" style="width: {psramPercent ?? 0}%; background: #9B59B6;"></div>
        </div>
        {#if device.freePsramBytes != null && device.largestPsramBlockBytes != null}
          <div class="m-sub">
            {(device.freePsramBytes / 1048576).toFixed(1)} MB livre | Maior bloco: {(device.largestPsramBlockBytes / 1048576).toFixed(1)} MB
          </div>
        {/if}
      </div>
    </div>
  </div>
{/if}

<style>
  .section {
    padding: 16px 24px;
    border-top: 1px solid var(--stroke);
  }

  .sec-hd {
    margin-bottom: 16px;
    font-size: 16px;
    font-weight: 600;
    color: var(--text-1);
  }

  .metrics-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 16px;
  }

  .m-cell {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 16px;
  }

  .m-lbl {
    font-size: 12px;
    color: var(--text-2);
    font-weight: 600;
    margin-bottom: 8px;
  }

  .m-val {
    font-size: 28px;
    font-weight: 600;
    line-height: 1;
    color: var(--text-1);
  }

  .m-unit {
    font-size: 14px;
    color: var(--text-2);
    margin-left: 4px;
    font-weight: 400;
  }

  .m-sub {
    font-size: 12px;
    color: var(--text-3);
    margin-top: 6px;
  }

  .prog {
    height: 4px;
    border-radius: 2px;
    background: rgba(255, 255, 255, 0.1);
    margin-top: 12px;
    overflow: hidden;
  }

  .prog-f {
    height: 100%;
    border-radius: 2px;
    transition: width 0.5s cubic-bezier(0.2, 0.8, 0.2, 1);
  }
</style>
