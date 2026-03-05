<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import {
    fetchVisualizerState,
    loadVisualizerGifUrl,
    pauseVisualizerGif,
    playVisualizerGif,
    setVisualizerContentMode,
    setVisualizerHub75,
    stopVisualizerGif,
    updateVisualizerSettings,
    type VisualizerSettings,
    type VisualizerState,
  } from '../lib/api';
  import type { WsMessage } from '../lib/ws';

  type Props = {
    realtimeMessage?: WsMessage | null;
    frameSrc?: string;
  };

  let { realtimeMessage = null, frameSrc = '' }: Props = $props();

  let visualizerState = $state(null as VisualizerState | null);
  let settingsOpen = $state(false);
  let loading = $state(false);
  let errorText = $state('');
  let gifUrl = $state('');
  let canvasHost: HTMLDivElement | null = null;
  let canvasEl: HTMLCanvasElement | null = null;
  let renderTimer: ReturnType<typeof setInterval> | null = null;

  const fftOptions = [1024, 2048, 4096, 8192];
  const weightingOptions = [
    { value: 'OFF', label: 'Off' },
    { value: 'A', label: 'A' },
    { value: 'B', label: 'B' },
    { value: 'C', label: 'C' },
    { value: 'D', label: 'D' },
    { value: '468', label: '468' },
  ];
  const scaleOptions = [
    { value: 'Logarithmic', label: 'Logaritmica' },
    { value: 'Mel', label: 'Mel' },
    { value: 'Bark', label: 'Bark' },
  ];
  const frequencyMinOptions = [16, 20, 30, 40, 60, 80, 100, 160, 200, 250, 315, 400, 500, 630, 800, 1000];
  const frequencyMaxOptions = [250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12000, 16000, 20000];

  $effect(() => {
    if (!realtimeMessage || !visualizerState) {
      return;
    }

    if (realtimeMessage.type === 'visualizer-spectrum') {
      visualizerState = {
        ...visualizerState,
        spectrum: {
          bands: realtimeMessage.bands,
          level: realtimeMessage.level,
          timestampUtc: realtimeMessage.timestampUtc,
        },
      };
      return;
    }

    if (realtimeMessage.type === 'visualizer-status') {
      visualizerState = {
        ...visualizerState,
        audioCapturing: realtimeMessage.audioCapturing,
        targetFps: realtimeMessage.targetFps,
        status: realtimeMessage.status,
        contentMode: realtimeMessage.contentMode,
        updatedAtUtc: realtimeMessage.updatedAtUtc,
        settings: {
          ...visualizerState.settings,
          hub75Enabled: realtimeMessage.hub75Enabled,
        },
        gif: {
          ...visualizerState.gif,
          isPlaying: realtimeMessage.gifPlaying,
          frameCount: realtimeMessage.gifFrameCount,
        },
      };
      return;
    }

    if (realtimeMessage.type === 'visualizer-settings-changed') {
      visualizerState = {
        ...visualizerState,
        settings: realtimeMessage.settings,
        updatedAtUtc: realtimeMessage.updatedAtUtc,
      };
    }
  });

  async function refreshState() {
    try {
      visualizerState = await fetchVisualizerState();
      gifUrl = visualizerState.gif.sourceUrl ?? '';
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function patchSettings(settings: Partial<VisualizerSettings>) {
    if (!visualizerState) {
      return;
    }

    loading = true;
    try {
      visualizerState = await updateVisualizerSettings(settings);
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      loading = false;
    }
  }

  async function handleHubToggle() {
    if (!visualizerState) {
      return;
    }

    loading = true;
    try {
      visualizerState = await setVisualizerHub75(!visualizerState.settings.hub75Enabled);
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      loading = false;
    }
  }

  async function handleContentMode(mode: 'audio' | 'gif') {
    loading = true;
    try {
      visualizerState = await setVisualizerContentMode(mode);
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      loading = false;
    }
  }

  async function handleGifLoad() {
    if (!gifUrl.trim()) {
      errorText = 'Informe uma URL GIF valida.';
      return;
    }

    loading = true;
    try {
      visualizerState = await loadVisualizerGifUrl(gifUrl.trim());
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      loading = false;
    }
  }

  async function handleGifPlay() {
    try {
      visualizerState = await playVisualizerGif();
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function handleGifPause() {
    try {
      visualizerState = await pauseVisualizerGif();
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function handleGifStop() {
    try {
      visualizerState = await stopVisualizerGif();
      errorText = '';
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  function resizeCanvasIfNeeded() {
    if (!canvasEl) {
      return;
    }

    const rect = canvasEl.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.floor(rect.width * dpr));
    const height = Math.max(1, Math.floor(rect.height * dpr));

    if (canvasEl.width !== width || canvasEl.height !== height) {
      canvasEl.width = width;
      canvasEl.height = height;
    }
  }

  function renderSpectrum() {
    if (!canvasEl || !visualizerState) {
      return;
    }

    resizeCanvasIfNeeded();
    const ctx = canvasEl.getContext('2d');
    if (!ctx) {
      return;
    }

    const width = canvasEl.width;
    const height = canvasEl.height;
    const centerY = height / 2;
    const bands = visualizerState.spectrum.bands;

    ctx.fillStyle = '#000000';
    ctx.fillRect(0, 0, width, height);

    if (!bands.length) {
      return;
    }

    const barWidth = width / bands.length;
    for (let i = 0; i < bands.length; i += 1) {
      const value = Math.max(0, Math.min(1, bands[i] ?? 0));
      const size = Math.max(1, value * (height * 0.42));
      const x = i * barWidth + (barWidth * 0.2);
      const w = Math.max(1, barWidth * 0.6);
      const hue = (i / Math.max(1, bands.length - 1)) * 300;

      ctx.fillStyle = `hsl(${hue} 100% 55%)`;
      ctx.fillRect(x, centerY - size, w, size * 2);
    }
  }

  async function toggleFullscreen() {
    if (!canvasHost) {
      return;
    }

    if (!document.fullscreenElement) {
      await canvasHost.requestFullscreen();
      return;
    }

    await document.exitFullscreen();
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'F11') {
      event.preventDefault();
      void toggleFullscreen();
    }

    if (event.key === 'Escape' && document.fullscreenElement) {
      event.preventDefault();
      void document.exitFullscreen();
    }
  }

  onMount(async () => {
    await refreshState();
    renderTimer = setInterval(renderSpectrum, 16);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('resize', renderSpectrum);
  });

  onDestroy(() => {
    if (renderTimer) {
      clearInterval(renderTimer);
    }

    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('resize', renderSpectrum);
  });
</script>

<div class="visualizer-page">
  <header class="controls-panel panel">
    <div class="controls-left">
      <button class="tbar-btn" onclick={() => (settingsOpen = !settingsOpen)}>Configuracoes</button>
      <button class="tbar-btn" class:accent={visualizerState?.contentMode === 'audio'} onclick={() => void handleContentMode('audio')}>Audio</button>
      <button class="tbar-btn" class:accent={visualizerState?.contentMode === 'gif'} onclick={() => void handleContentMode('gif')}>GIF</button>
      <label class="hub-toggle">
        <span>Modo HUB75</span>
        <input type="checkbox" checked={Boolean(visualizerState?.settings.hub75Enabled)} disabled={loading} onchange={() => void handleHubToggle()} />
      </label>
    </div>

    <div class="controls-right">
      <span class="status">{visualizerState?.status ?? 'Pronto'}</span>
    </div>
  </header>

  <div class="visualizer-body">
    {#if settingsOpen && visualizerState}
      <aside class="settings-pane panel">
        <h3>Configuracoes</h3>

        <label for="linear-boost-range">Linear Boost</label>
        <input
          id="linear-boost-range"
          class="win-slider"
          type="range"
          min="1"
          max="3"
          step="0.1"
          value={visualizerState.settings.linearBoost}
          oninput={(event) => {
            visualizerState = { ...visualizerState!, settings: { ...visualizerState!.settings, linearBoost: Number((event.target as HTMLInputElement).value) } };
          }}
          onchange={() => void patchSettings({ linearBoost: visualizerState!.settings.linearBoost })}
        />
        <span class="field-value">x{visualizerState.settings.linearBoost.toFixed(1)}</span>

        <label for="bar-count-range">Quantidade de barras</label>
        <input
          id="bar-count-range"
          class="win-slider"
          type="range"
          min="8"
          max="128"
          step="1"
          value={visualizerState.settings.barCount}
          oninput={(event) => {
            visualizerState = { ...visualizerState!, settings: { ...visualizerState!.settings, barCount: Number((event.target as HTMLInputElement).value) } };
          }}
          onchange={() => void patchSettings({ barCount: visualizerState!.settings.barCount })}
        />
        <span class="field-value">{visualizerState.settings.barCount}</span>

        <label for="fft-size-select">FFT Size</label>
        <select
          id="fft-size-select"
          value={String(visualizerState.settings.fftSize)}
          onchange={(event) => void patchSettings({ fftSize: Number((event.target as HTMLSelectElement).value) })}
        >
          {#each fftOptions as item (item)}
            <option value={item}>{item}</option>
          {/each}
        </select>

        <label for="fft-smoothing-range">FFT Smoothing</label>
        <input
          id="fft-smoothing-range"
          class="win-slider"
          type="range"
          min="0"
          max="0.99"
          step="0.01"
          value={visualizerState.settings.fftSmoothing}
          oninput={(event) => {
            visualizerState = { ...visualizerState!, settings: { ...visualizerState!.settings, fftSmoothing: Number((event.target as HTMLInputElement).value) } };
          }}
          onchange={() => void patchSettings({ fftSmoothing: visualizerState!.settings.fftSmoothing })}
        />
        <span class="field-value">{visualizerState.settings.fftSmoothing.toFixed(2)}</span>

        <label for="weighting-filter-select">Weighting Filter</label>
        <select
          id="weighting-filter-select"
          value={visualizerState.settings.weightingFilter}
          onchange={(event) => void patchSettings({ weightingFilter: (event.target as HTMLSelectElement).value })}
        >
          {#each weightingOptions as option (option.value)}
            <option value={option.value}>{option.label}</option>
          {/each}
        </select>

        <label for="frequency-scale-select">Escala de frequencia</label>
        <select
          id="frequency-scale-select"
          value={visualizerState.settings.frequencyScale}
          onchange={(event) => void patchSettings({ frequencyScale: (event.target as HTMLSelectElement).value })}
        >
          {#each scaleOptions as option (option.value)}
            <option value={option.value}>{option.label}</option>
          {/each}
        </select>

        <label for="frequency-min-select">Faixa de frequencia</label>
        <div class="range-grid">
          <select
            id="frequency-min-select"
            value={String(Math.round(visualizerState.settings.frequencyMinHz))}
            onchange={(event) => void patchSettings({ frequencyMinHz: Number((event.target as HTMLSelectElement).value) })}
          >
            {#each frequencyMinOptions as item (item)}
              <option value={item}>{item} Hz</option>
            {/each}
          </select>
          <select
            id="frequency-max-select"
            value={String(Math.round(visualizerState.settings.frequencyMaxHz))}
            onchange={(event) => void patchSettings({ frequencyMaxHz: Number((event.target as HTMLSelectElement).value) })}
          >
            {#each frequencyMaxOptions as item (item)}
              <option value={item}>{item >= 1000 ? `${(item / 1000).toFixed(item % 1000 === 0 ? 0 : 1)} kHz` : `${item} Hz`}</option>
            {/each}
          </select>
        </div>
      </aside>
    {/if}

    <section class="main-column">
      <div class="canvas-panel panel" bind:this={canvasHost}>
        <canvas bind:this={canvasEl}></canvas>
        <button class="fullscreen-btn tbar-btn" onclick={() => void toggleFullscreen()}>Tela cheia</button>
      </div>

      {#if visualizerState?.settings.hub75Enabled || visualizerState?.contentMode === 'gif'}
        <section class="preview-panel panel">
          <div class="preview-title">Simulacao HUB75 local (128x64 nativo)</div>
          <div class="preview-area">
            {#if frameSrc}
              <img src={frameSrc} alt="Preview HUB75" width="512" height="256" />
            {:else}
              <div class="empty">Aguardando frames...</div>
            {/if}
          </div>
        </section>
      {/if}

      <section class="gif-panel panel">
        <div class="gif-title">GIF HUB75</div>
        <div class="gif-row">
          <input
            type="url"
            placeholder="https://exemplo.com/animacao.gif"
            bind:value={gifUrl}
            disabled={loading}
          />
          <button class="tbar-btn" disabled={loading || !gifUrl.trim()} onclick={() => void handleGifLoad()}>Carregar URL</button>
        </div>
        <div class="gif-actions">
          <button class="tbar-btn" disabled={!visualizerState || visualizerState.gif.frameCount === 0} onclick={() => void handleGifPlay()}>Play</button>
          <button class="tbar-btn" disabled={!visualizerState || !visualizerState.gif.isPlaying} onclick={() => void handleGifPause()}>Pause</button>
          <button class="tbar-btn" disabled={!visualizerState || visualizerState.gif.frameCount === 0} onclick={() => void handleGifStop()}>Stop</button>
          <span class="gif-feedback">
            {#if visualizerState?.gif.error}
              Erro: {visualizerState.gif.error}
            {:else if visualizerState}
              Frames: {visualizerState.gif.frameCount} | {visualizerState.gif.isPlaying ? 'reproduzindo' : 'parado'}
            {/if}
          </span>
        </div>
      </section>
    </section>
  </div>

  {#if errorText}
    <div class="error-line">{errorText}</div>
  {/if}
</div>

<style>
  .visualizer-page {
    display: grid;
    grid-template-rows: auto 1fr auto;
    gap: 10px;
    min-height: 0;
    height: 100%;
  }

  .controls-panel {
    padding: 8px 12px;
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    min-height: 68px;
  }

  .controls-left {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }

  .controls-right {
    color: var(--accent);
    font-weight: 600;
    font-size: 14px;
    text-align: right;
  }

  .hub-toggle {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    margin-left: 6px;
    color: var(--text-2);
  }

  .visualizer-body {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 10px;
    min-height: 0;
    position: relative;
  }

  .settings-pane {
    position: absolute;
    right: 0;
    top: 0;
    width: min(360px, calc(100vw - 24px));
    padding: 14px;
    gap: 8px;
    z-index: 5;
    max-height: 100%;
    overflow: auto;
    backdrop-filter: blur(12px);
  }

  .settings-pane h3 {
    margin-bottom: 4px;
    font-size: 17px;
  }

  .settings-pane label {
    color: var(--text-2);
    font-size: 13px;
  }

  .settings-pane .field-value {
    color: var(--text-3);
    font-size: 12px;
    margin-bottom: 8px;
  }

  .range-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
    margin-bottom: 8px;
  }

  .main-column {
    display: grid;
    grid-template-rows: minmax(320px, 1fr) auto auto;
    gap: 10px;
    min-height: 0;
  }

  .canvas-panel {
    position: relative;
    background: #000;
    border-radius: 12px;
    min-height: 320px;
  }

  .canvas-panel canvas {
    width: 100%;
    height: 100%;
    display: block;
    border-radius: 12px;
  }

  .fullscreen-btn {
    position: absolute;
    right: 12px;
    bottom: 12px;
  }

  .preview-panel {
    padding: 10px;
    gap: 8px;
  }

  .preview-title,
  .gif-title {
    color: var(--text-2);
    font-size: 13px;
    font-weight: 600;
  }

  .preview-area {
    border-radius: 8px;
    border: 1px solid var(--stroke);
    background: #000;
    overflow: hidden;
    height: 256px;
  }

  .preview-area img {
    width: 100%;
    height: 100%;
    object-fit: contain;
    image-rendering: pixelated;
  }

  .gif-panel {
    padding: 10px;
    gap: 10px;
  }

  .gif-row {
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 8px;
  }

  .gif-row input {
    background: var(--surface-layer);
    color: var(--text-1);
    border: 1px solid var(--stroke);
    border-radius: 4px;
    padding: 0 10px;
    height: 32px;
    font-family: inherit;
    font-size: 14px;
  }

  .gif-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }

  .gif-feedback {
    color: var(--text-2);
    font-size: 12px;
    margin-left: 4px;
  }

  .error-line {
    color: var(--red);
    font-size: 12px;
    padding: 0 4px;
  }

  .empty {
    height: 100%;
    display: grid;
    place-items: center;
    color: var(--text-3);
    font-size: 13px;
  }

  @media (max-width: 980px) {
    .main-column {
      grid-template-rows: minmax(220px, 1fr) auto auto;
    }

    .preview-area {
      height: 180px;
    }
  }
</style>
