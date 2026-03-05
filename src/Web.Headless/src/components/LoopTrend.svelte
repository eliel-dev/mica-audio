<script lang="ts">
  import { getLoopTrend } from '../lib/trend-store';

  interface Props {
    deviceId: string;
  }

  let { deviceId }: Props = $props();

  const samples = $derived(getLoopTrend(deviceId));
</script>

{#if samples.length > 0}
  <div class="section">
    <div class="trend-sub">Historico de uso do processador</div>
    <div class="trend-box">
      <div class="trend-bars">
        {#each samples as value, i (i)}
          <div class="t-bar" style="height: {Math.max(2, value)}%;"></div>
        {/each}
      </div>
    </div>
  </div>
{/if}

<style>
  .section {
    padding: 16px 24px;
    border-top: 1px solid var(--stroke);
  }

  .trend-sub {
    font-size: 14px;
    color: var(--text-2);
    margin-bottom: 12px;
    font-weight: 600;
  }

  .trend-box {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
    border-radius: 8px;
    padding: 12px 16px;
    min-height: 80px;
  }

  .trend-bars {
    display: flex;
    align-items: flex-end;
    gap: 3px;
    height: 50px;
  }

  .t-bar {
    flex: 1;
    border-radius: 2px 2px 0 0;
    min-height: 2px;
    transition: height 0.4s ease;
    background: var(--accent);
    opacity: 0.8;
  }

  .t-bar:hover {
    opacity: 1;
  }
</style>
