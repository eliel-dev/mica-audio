<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { createPreviewSocket } from '../lib/ws';
  import { fetchApps, setBrightness, setApp } from '../lib/api';
  import type { WsMessage } from '../lib/ws';
  import type { AppInfo } from '../lib/api';

  interface Props {
    onHeartbeat?: (devicesOnline: number, audioCapturing: boolean) => void;
  }

  let { onHeartbeat }: Props = $props();

  let frameSrc: string = $state('');
  let socket: { close: () => void } | null = null;
  let apps: AppInfo[] = $state([]);
  let brightnessValue: number = $state(120);
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  function handleMessage(msg: WsMessage) {
    if (msg.type === 'frame') {
      frameSrc = `data:image/png;base64,${msg.data}`;
    } else if (msg.type === 'heartbeat') {
      onHeartbeat?.(msg.devicesOnline, msg.audioCapturing);
    }
  }

  function handleBrightnessInput(value: number) {
    brightnessValue = value;
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      void setBrightness('default', value);
    }, 500);
  }

  function handleAppChange(appId: string) {
    void setApp('default', appId);
  }

  onMount(async () => {
    socket = createPreviewSocket(handleMessage);
    try {
      apps = await fetchApps();
    } catch {
      /* offline */
    }
  });

  onDestroy(() => {
    socket?.close();
    if (debounceTimer) clearTimeout(debounceTimer);
  });
</script>

<!-- Preview HUB75 -->
<div class="section">
  <div class="sec-hd">
    <span>Preview HUB75</span>
    {#if frameSrc}
      <span class="live-dot"></span>
    {/if}
  </div>
  <div class="preview-container">
    {#if frameSrc}
      <img class="preview-image" src={frameSrc} alt="HUB75 128x64 preview" width="512" height="256" />
    {:else}
      <div class="preview-placeholder">Aguardando frames...</div>
    {/if}
  </div>
</div>

<!-- Brightness -->
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
</div>

<!-- App selector -->
{#if apps.length > 0}
  <div class="section">
    <div class="sec-hd">App ativo</div>
    <select onchange={(e) => handleAppChange((e.target as HTMLSelectElement).value)}>
      {#each apps as appItem (appItem.id)}
        <option value={appItem.id}>{appItem.name}</option>
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

  .live-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--green);
    animation: lpulse 1.5s infinite;
  }

  .preview-container {
    position: relative;
    width: 100%;
    aspect-ratio: 2/1;
    border-radius: 8px;
    overflow: hidden;
    background: #000;
    border: 1px solid var(--stroke);
    border-top: 1px solid var(--stroke-top);
  }

  .preview-image {
    width: 100%;
    height: 100%;
    object-fit: contain;
    image-rendering: pixelated;
    display: block;
  }

  .preview-placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    height: 100%;
    color: var(--text-3);
    font-size: 14px;
  }

  .bright-row {
    display: flex;
    align-items: center;
    gap: 16px;
  }
</style>
