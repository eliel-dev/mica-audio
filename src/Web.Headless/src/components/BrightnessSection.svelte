<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import type { DeviceInfo, AppInfo } from '../lib/api';
  import { fetchApps, setBrightness, setApp } from '../lib/api';

  interface Props {
    device: DeviceInfo;
  }

  let { device }: Props = $props();

  let apps: AppInfo[] = $state([]);
  let brightnessValue: number = $state(120);
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  $effect(() => {
    const val = device.brightnessApplied ?? device.brightnessCap ?? 120;
    brightnessValue = Math.max(30, Math.min(160, val));
  });

  function handleBrightnessInput(value: number) {
    brightnessValue = value;
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      void setBrightness(device.deviceId, value);
    }, 500);
  }

  function handleAppChange(appId: string) {
    void setApp(device.deviceId, appId);
  }

  onMount(async () => {
    try {
      apps = await fetchApps();
    } catch {
      /* offline */
    }
  });

  onDestroy(() => {
    if (debounceTimer) clearTimeout(debounceTimer);
  });
</script>

<div class="section">
  <div class="sec-hd">
    <span>Brilho do painel LED</span>
    <span class="sec-hd-val">{brightnessValue}/160</span>
  </div>
  <div class="bright-row">
    <input
      type="range"
      class="win-slider"
      min="30"
      max="160"
      value={brightnessValue}
      oninput={(e) => handleBrightnessInput(parseInt((e.target as HTMLInputElement).value))}
    />
  </div>
  <div class="info-line">
    <span>Brilho atual no painel: <b>{device.brightnessApplied ?? '-'}</b></span>
  </div>
  <div class="info-line">
    <span>Sinal de vida: <b>{device.telemetrySequence != null ? '#' + device.telemetrySequence : '-'}</b></span>
    <span>|</span>
    <span>LED de teste: <b>{device.testLedEnabled ? 'Ativo' : '-'}</b></span>
    <span>|</span>
    <span>Intensidade do LED: <b>{device.testLedDuty ?? '-'}</b></span>
  </div>
</div>

{#if apps.length > 0}
  <div class="section">
    <div class="sec-hd">App ativo</div>
    <select onchange={(e) => handleAppChange((e.target as HTMLSelectElement).value)}>
      {#each apps as appItem (appItem.id)}
        <option value={appItem.id} selected={appItem.id === device.activeAppId}>{appItem.name}</option>
      {/each}
    </select>
  </div>
{/if}

<style>
  .section {
    padding: 16px 24px;
    border-top: 1px solid var(--stroke);
  }

  .sec-hd {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;
    font-size: 16px;
    font-weight: 600;
    color: var(--text-1);
  }

  .sec-hd-val {
    font-weight: 400;
    color: var(--text-2);
    font-size: 14px;
  }

  .bright-row {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-bottom: 12px;
  }

  .info-line {
    display: flex;
    gap: 24px;
    font-size: 14px;
    color: var(--text-2);
    margin-bottom: 6px;
    flex-wrap: wrap;
  }

  .info-line b {
    color: var(--text-1);
    font-weight: 600;
  }
</style>
