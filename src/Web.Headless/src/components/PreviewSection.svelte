<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { createPreviewSocket } from '../lib/ws';
  import type { WsMessage } from '../lib/ws';

  interface Props {
    onHeartbeat?: (devicesOnline: number, audioCapturing: boolean) => void;
  }

  let { onHeartbeat }: Props = $props();

  let frameSrc: string = $state('');
  let socket: { close: () => void } | null = null;

  function handleMessage(msg: WsMessage) {
    if (msg.type === 'frame') {
      frameSrc = `data:image/png;base64,${msg.data}`;
    } else if (msg.type === 'heartbeat') {
      onHeartbeat?.(msg.devicesOnline, msg.audioCapturing);
    }
  }

  onMount(() => {
    socket = createPreviewSocket(handleMessage);
  });

  onDestroy(() => {
    socket?.close();
  });
</script>

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

  .live-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--green);
    animation: lpulse 1.5s infinite;
  }

  .preview-container {
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
</style>
