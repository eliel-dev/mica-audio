<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import DevicesPage from './pages/DevicesPage.svelte';
  import VisualizerPage from './pages/VisualizerPage.svelte';
  import AppsPage from './pages/AppsPage.svelte';
  import { createPreviewSocket, type WsMessage } from './lib/ws';

  type ShellPage = 'visualizer' | 'devices' | 'apps';

  let activePage = $state<ShellPage>('visualizer');
  let realtimeMessage = $state<WsMessage | null>(null);
  let frameSrc = $state('');
  let devicesOnline = $state(0);
  let audioCapturing = $state(false);
  let visualizerStatus = $state('Pronto');
  let wsConnection: { close: () => void } | null = null;

  function handleRealtimeMessage(message: WsMessage) {
    realtimeMessage = message;

    if (message.type === 'frame') {
      frameSrc = `data:image/png;base64,${message.data}`;
      return;
    }

    if (message.type === 'heartbeat') {
      devicesOnline = message.devicesOnline;
      audioCapturing = message.audioCapturing;
      return;
    }

    if (message.type === 'visualizer-status') {
      visualizerStatus = message.status;
    }
  }

  onMount(() => {
    wsConnection = createPreviewSocket(handleRealtimeMessage);
  });

  onDestroy(() => {
    wsConnection?.close();
  });
</script>

<div class="app-shell">
  <aside class="nav-panel panel">
    <div class="brand">MicaAudio</div>
    <nav>
      <button class="nav-item" class:active={activePage === 'visualizer'} onclick={() => (activePage = 'visualizer')}>Visualizador</button>
      <button class="nav-item" class:active={activePage === 'devices'} onclick={() => (activePage = 'devices')}>Dispositivos</button>
      <button class="nav-item" class:active={activePage === 'apps'} onclick={() => (activePage = 'apps')}>Apps</button>
    </nav>
    <div class="nav-footer">Servidor: http://127.0.0.1:5175</div>
  </aside>

  <main class="content-area">
    {#if activePage === 'visualizer'}
      <VisualizerPage {realtimeMessage} {frameSrc} />
    {:else if activePage === 'devices'}
      <DevicesPage {realtimeMessage} {frameSrc} />
    {:else}
      <AppsPage />
    {/if}
  </main>
</div>

<footer class="statusbar">
  <span>Dispositivos: <b>{devicesOnline}</b></span>
  <span>Audio: <b>{audioCapturing ? 'Capturando' : 'Inativo'}</b></span>
  <span>{visualizerStatus}</span>
</footer>

<style>
  .app-shell {
    flex: 1;
    display: grid;
    grid-template-columns: 220px minmax(0, 1fr);
    gap: 10px;
    padding: 10px 10px 8px 10px;
    min-height: 0;
  }

  .nav-panel {
    padding: 10px;
    gap: 10px;
    justify-content: space-between;
  }

  .brand {
    font-size: 24px;
    font-weight: 620;
    padding: 8px;
  }

  nav {
    display: grid;
    gap: 4px;
  }

  .nav-item {
    height: 36px;
    border-radius: 7px;
    background: transparent;
    border: 1px solid transparent;
    color: var(--text-1);
    text-align: left;
    padding: 0 12px;
    cursor: pointer;
    font-family: inherit;
    font-size: 14px;
  }

  .nav-item:hover {
    background: var(--hover);
    border-color: var(--stroke);
  }

  .nav-item.active {
    background: rgba(96, 205, 255, 0.14);
    border-color: rgba(96, 205, 255, 0.34);
    color: var(--accent);
  }

  .nav-footer {
    color: var(--text-3);
    font-size: 12px;
    padding: 8px;
  }

  .content-area {
    min-height: 0;
    height: 100%;
  }

  .statusbar {
    height: 24px;
    display: flex;
    align-items: center;
    gap: 16px;
    padding: 0 12px;
    background: rgba(10, 10, 10, 0.65);
    border-top: 1px solid var(--stroke);
    color: var(--text-3);
    font-size: 11px;
  }

  @media (max-width: 960px) {
    .app-shell {
      grid-template-columns: 1fr;
    }

    .nav-panel {
      padding: 8px;
      min-height: auto;
    }

    nav {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    .nav-footer {
      display: none;
    }
  }
</style>
