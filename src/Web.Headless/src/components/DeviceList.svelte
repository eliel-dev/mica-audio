<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { fetchDevices, generatePairingCode } from '../lib/api';
  import type { DeviceInfo } from '../lib/api';
  import { buildStatusLine } from '../lib/device-lifecycle';
  import { pushSample } from '../lib/trend-store';

  interface Props {
    selectedDevice: DeviceInfo | null;
    onSelectDevice: (device: DeviceInfo | null) => void;
  }

  let { selectedDevice, onSelectDevice }: Props = $props();

  let devices: DeviceInfo[] = $state([]);
  let pairingCode: string | null = $state(null);
  let pollTimer: ReturnType<typeof setInterval> | null = null;

  async function refreshDevices() {
    try {
      devices = await fetchDevices();
      for (const d of devices) {
        pushSample(d.deviceId, d.loopLoadPercent, d.freeHeapBytes);
      }
      // Update selectedDevice with fresh data
      if (selectedDevice) {
        const updated = devices.find(d => d.deviceId === selectedDevice!.deviceId);
        if (updated) onSelectDevice(updated);
      }
    } catch {
      /* offline */
    }
  }

  async function handleGenPairingCode() {
    const result = await generatePairingCode();
    pairingCode = result.code;
  }

  function selectDevice(device: DeviceInfo) {
    onSelectDevice(device);
  }

  onMount(() => {
    void refreshDevices();
    pollTimer = setInterval(() => void refreshDevices(), 5000);
  });

  onDestroy(() => {
    if (pollTimer) clearInterval(pollTimer);
  });
</script>

<div class="dev-list">
  {#if devices.length === 0}
    <div class="dev-empty">Nenhum dispositivo conectado.</div>
  {:else}
    {#each devices as device (device.deviceId)}
      <div
        class="dev-item"
        class:sel={selectedDevice?.deviceId === device.deviceId}
        onclick={() => selectDevice(device)}
        role="button"
        tabindex="0"
        onkeydown={(e) => { if (e.key === 'Enter') selectDevice(device); }}
      >
        <div class="badge" class:badge-online={device.status === 'online'} class:badge-offline={device.status !== 'online'}>
          {device.status === 'online' ? 'Online' : 'Offline'}
        </div>
        <div class="dev-info">
          <div class="dev-name">{device.name}</div>
          <div class="dev-id">{device.deviceId}</div>
          <div class="dev-meta">{buildStatusLine(device)}</div>
        </div>
      </div>
    {/each}
  {/if}
</div>

<div class="panel-foot">
  <div class="pair-row">
    <div class="pair-dot"></div>
    <span class="pair-label">Pareamento: {pairingCode ?? '-'}</span>
  </div>
  <button class="tbar-btn accent" onclick={handleGenPairingCode}>Gerar codigo</button>
</div>

<style>
  .dev-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
  }

  .dev-empty {
    color: var(--text-3);
    font-size: 13px;
    text-align: center;
    padding: 24px 16px;
  }

  .dev-item {
    display: flex;
    gap: 12px;
    align-items: center;
    padding: 8px;
    border-radius: 5px;
    cursor: pointer;
    transition: background 0.1s;
    border: 1px solid transparent;
    margin-bottom: 2px;
  }

  .dev-item:hover {
    background: var(--hover);
  }

  .dev-item.sel {
    background: var(--hover);
    border: 1px solid var(--stroke);
  }

  .dev-info {
    flex: 1;
    min-width: 0;
  }

  .dev-name {
    font-size: 14px;
    font-weight: 600;
    color: var(--text-1);
    margin-bottom: 2px;
  }

  .dev-id {
    font-size: 12px;
    color: var(--text-3);
    margin-bottom: 2px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .dev-meta {
    font-size: 12px;
    color: var(--text-2);
  }

  .panel-foot {
    padding: 12px 16px;
    border-top: 1px solid var(--stroke);
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    font-size: 12px;
    color: var(--text-2);
  }

  .pair-row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .pair-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--accent);
    flex-shrink: 0;
  }

  .pair-label {
    font-size: 12px;
  }
</style>
