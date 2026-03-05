<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import {
    fetchApps,
    fetchDeviceDetails,
    fetchDeviceLogs,
    fetchDevices,
    fetchOnboardingPorts,
    fetchOnboardingStatus,
    generatePairingCode,
    removeDevice,
    setApp,
    setBrightness,
    startOnboarding,
    testLed,
    type AppInfo,
    type CommandStatus,
    type DeviceDetails,
    type DeviceInfo,
    type DeviceLogs,
    type OnboardingPort,
    type OnboardingStatus,
  } from '../lib/api';
  import type { WsMessage } from '../lib/ws';

  type Props = {
    realtimeMessage?: WsMessage | null;
    frameSrc?: string;
  };

  let { realtimeMessage = null, frameSrc = '' }: Props = $props();

  let devices = $state<DeviceInfo[]>([]);
  let apps = $state<AppInfo[]>([]);
  let selectedDeviceId = $state<string | null>(null);
  let details = $state<DeviceDetails | null>(null);
  let logs = $state<DeviceLogs | null>(null);
  let commandState = $state<CommandStatus>({
    inProgress: false,
    percent: 0,
    status: 'Comandos: pronto',
    stage: null,
    deviceId: null,
    commandId: null,
    errorCode: null,
    updatedAtUtc: new Date().toISOString(),
  });
  let pairingFooterText = $state('Pareamento: -');
  let pairingFooterTone = $state<'info' | 'success' | 'warning' | 'error'>('info');
  let errorText = $state('');
  let busyAction = $state(false);

  let brightnessDraft = $state(160);
  let brightnessDirty = $state(false);

  let onboardingOpen = $state(false);
  let onboardingPorts = $state<OnboardingPort[]>([]);
  let onboardingPortName = $state('');
  let onboardingStatus = $state<OnboardingStatus>({
    inProgress: false,
    stage: 'idle',
    message: 'Aguardando onboarding.',
    percent: 0,
    portName: null,
    completed: false,
    success: false,
    errorCode: null,
    pairCode: null,
    updatedAtUtc: new Date().toISOString(),
  });

  let devicesTimer: ReturnType<typeof setInterval> | null = null;
  let detailsTimer: ReturnType<typeof setInterval> | null = null;
  let logsTimer: ReturnType<typeof setInterval> | null = null;
  let onboardingTimer: ReturnType<typeof setInterval> | null = null;

  const selectedDevice = $derived(
    selectedDeviceId ? (devices.find((item) => item.deviceId === selectedDeviceId) ?? null) : null,
  );
  const canRunDeviceCommands = $derived(Boolean(details?.canRunCommands) && !busyAction);

  $effect(() => {
    if (!details) {
      return;
    }

    if (!brightnessDirty) {
      brightnessDraft = details.brightnessValue;
    }
  });

  $effect(() => {
    if (realtimeMessage) {
      handleWsMessage(realtimeMessage);
    }
  });

  function setPairingFooter(message: string, tone: 'info' | 'success' | 'warning' | 'error' = 'info') {
    pairingFooterText = message;
    pairingFooterTone = tone;
  }

  function handleWsMessage(message: WsMessage) {
    if (message.type === 'frame') {
      frameSrc = `data:image/png;base64,${message.data}`;
      return;
    }

    if (message.type === 'heartbeat') {
      return;
    }

    if (message.type === 'command-progress') {
      commandState = {
        inProgress: (message.percent ?? 0) < 100 && !message.success,
        percent: message.percent ?? commandState.percent,
        status: message.status ?? commandState.status,
        stage: message.stage ?? null,
        deviceId: message.deviceId ?? null,
        commandId: message.commandId ?? null,
        errorCode: message.errorCode ?? null,
        updatedAtUtc: new Date().toISOString(),
      };

      if (selectedDeviceId && message.deviceId === selectedDeviceId) {
        void refreshDetails();
      }

      return;
    }

    if (message.type === 'onboarding-progress') {
      onboardingStatus = {
        ...onboardingStatus,
        stage: message.stage,
        message: message.message,
        percent: message.percent,
        inProgress: message.inProgress,
        completed: message.completed,
        portName: message.portName ?? onboardingStatus.portName,
        updatedAtUtc: new Date().toISOString(),
      };
      return;
    }

    if (message.type === 'onboarding-result') {
      onboardingStatus = {
        ...onboardingStatus,
        inProgress: false,
        completed: true,
        success: message.success,
        stage: message.stage ?? onboardingStatus.stage,
        message: message.message,
        errorCode: message.errorCode ?? null,
        pairCode: message.pairCode ?? onboardingStatus.pairCode,
        updatedAtUtc: new Date().toISOString(),
      };

      if (message.success && message.pairCode) {
        setPairingFooter(`Pareamento: ${message.pairCode}`, 'success');
      } else if (!message.success) {
        setPairingFooter(`Pareamento: erro (${message.errorCode ?? 'onboarding'})`, 'error');
      }
    }
  }

  async function refreshDevices() {
    try {
      const next = await fetchDevices();
      devices = next;
      const firstDevice = next.at(0) ?? null;

      if (selectedDeviceId && !next.some((device) => device.deviceId === selectedDeviceId)) {
        selectedDeviceId = firstDevice ? firstDevice.deviceId : null;
      }

      if (!selectedDeviceId && firstDevice) {
        selectedDeviceId = firstDevice.deviceId;
      }

      if (selectedDeviceId) {
        void refreshDetails();
        void refreshLogs();
      } else {
        details = null;
        logs = null;
      }
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function refreshDetails() {
    if (!selectedDeviceId) {
      details = null;
      return;
    }

    try {
      details = await fetchDeviceDetails(selectedDeviceId);
      commandState = details.command;
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function refreshLogs() {
    if (!selectedDeviceId) {
      logs = null;
      return;
    }

    try {
      logs = await fetchDeviceLogs(selectedDeviceId);
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function refreshOnboardingPorts() {
    try {
      onboardingPorts = await fetchOnboardingPorts();
      const firstPort = onboardingPorts.at(0) ?? null;
      if (!onboardingPortName && firstPort) {
        onboardingPortName = firstPort.portName;
      }
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  async function refreshOnboardingStatus() {
    try {
      onboardingStatus = await fetchOnboardingStatus();
      if (onboardingStatus.success && onboardingStatus.pairCode) {
        setPairingFooter(`Pareamento: ${onboardingStatus.pairCode}`, 'success');
      }
    } catch {
      // No-op while onboarding is idle.
    }
  }

  async function handleGeneratePairingCode() {
    try {
      const pairing = await generatePairingCode();
      setPairingFooter(`Pareamento: ${pairing.code}`, 'success');
    } catch (error) {
      setPairingFooter('Pareamento: falha ao gerar codigo', 'error');
      errorText = (error as Error).message;
    }
  }

  async function handleChangeApp(appId: string) {
    if (!selectedDeviceId) {
      return;
    }

    busyAction = true;
    try {
      await setApp(selectedDeviceId, appId);
      await refreshDetails();
      await refreshDevices();
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      busyAction = false;
    }
  }

  function handleBrightnessInput(value: number) {
    brightnessDraft = value;
    brightnessDirty = true;
  }

  async function commitBrightness() {
    if (!selectedDeviceId || !details || !brightnessDirty) {
      return;
    }

    busyAction = true;
    try {
      await setBrightness(selectedDeviceId, brightnessDraft);
      brightnessDirty = false;
      await refreshDetails();
      await refreshDevices();
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      busyAction = false;
    }
  }

  async function handleTestLed() {
    if (!selectedDeviceId) {
      return;
    }

    busyAction = true;
    try {
      await testLed(selectedDeviceId);
      await refreshDetails();
      await refreshLogs();
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      busyAction = false;
    }
  }

  async function handleRemoveDevice() {
    if (!selectedDeviceId || !selectedDevice) {
      return;
    }

    const warning = selectedDevice.status === 'online'
      ? 'O dispositivo esta online e sera revogado/reiniciado antes da remocao local. Confirmar?'
      : 'Remover dispositivo do registro local?';

    if (!confirm(warning)) {
      return;
    }

    busyAction = true;
    try {
      const result = await removeDevice(selectedDeviceId);
      setPairingFooter(result.message, result.revokeSucceeded || !result.wasOnline ? 'success' : 'warning');
      await refreshDevices();
      await refreshDetails();
      await refreshLogs();
    } catch (error) {
      errorText = (error as Error).message;
    } finally {
      busyAction = false;
    }
  }

  async function openOnboarding() {
    onboardingOpen = true;
    await refreshOnboardingPorts();
    await refreshOnboardingStatus();
  }

  function closeOnboarding() {
    if (onboardingStatus.inProgress) {
      return;
    }

    onboardingOpen = false;
  }

  async function handleStartOnboarding() {
    if (!onboardingPortName) {
      errorText = 'Selecione uma porta COM valida.';
      return;
    }

    try {
      const response = await startOnboarding(onboardingPortName);
      onboardingStatus = response.snapshot;
    } catch (error) {
      errorText = (error as Error).message;
    }
  }

  onMount(async () => {
    try {
      apps = await fetchApps();
    } catch {
      apps = [];
    }

    await refreshDevices();
    await refreshOnboardingStatus();

    devicesTimer = setInterval(() => void refreshDevices(), 5000);
    detailsTimer = setInterval(() => void refreshDetails(), 2000);
    logsTimer = setInterval(() => void refreshLogs(), 3000);
    onboardingTimer = setInterval(() => {
      if (onboardingOpen || onboardingStatus.inProgress) {
        void refreshOnboardingStatus();
      }
    }, 1000);
  });

  onDestroy(() => {
    if (devicesTimer) clearInterval(devicesTimer);
    if (detailsTimer) clearInterval(detailsTimer);
    if (logsTimer) clearInterval(logsTimer);
    if (onboardingTimer) clearInterval(onboardingTimer);
  });
</script>

<div class="devices-layout">
  {#if errorText}
    <div class="inline-error">{errorText}</div>
  {/if}
  <section class="panel list-panel">
    <div class="list-header">Dispositivos</div>
    <div class="device-list">
      {#if devices.length === 0}
        <div class="empty">Nenhum dispositivo conectado.</div>
      {:else}
        {#each devices as device (device.deviceId)}
          <button
            class="device-row"
            class:selected={selectedDeviceId === device.deviceId}
            onclick={() => {
              selectedDeviceId = device.deviceId;
              void refreshDetails();
              void refreshLogs();
            }}
          >
            <span class="badge" class:badge-online={device.status === 'online'} class:badge-offline={device.status !== 'online'}>
              {device.status === 'online' ? 'Online' : 'Offline'}
            </span>
            <span class="row-main">
              <span class="row-name">{device.name}</span>
              <span class="row-id">{device.deviceId}</span>
              <span class="row-meta">{device.statusLine}</span>
            </span>
          </button>
        {/each}
      {/if}
    </div>

    <div class="list-actions">
      <button class="tbar-btn accent" onclick={openOnboarding}>Novo dispositivo</button>
    </div>

    <div class="pair-footer">
      <span
        class="pair-dot"
        class:pair-info={pairingFooterTone === 'info'}
        class:pair-success={pairingFooterTone === 'success'}
        class:pair-warning={pairingFooterTone === 'warning'}
        class:pair-error={pairingFooterTone === 'error'}
      ></span>
      <span class="pair-text">{pairingFooterText}</span>
      <button class="tbar-btn" onclick={handleGeneratePairingCode}>Gerar codigo</button>
    </div>
  </section>

  <section class="panel detail-panel">
    {#if details?.exists}
      <header class="detail-header">
        <div class="detail-left">
          <h1>{details.name}</h1>
          <div class="detail-line">{details.subtitle}</div>
          <div class="detail-line">{details.registrationLine}</div>
          <div class="detail-line">{details.appLabel}</div>
          <div class="detail-line muted">{details.serverLine}</div>
        </div>
        <div class="detail-right">
          <div class="signal">{details.signalLabel}</div>
          <button class="tbar-btn" disabled={!canRunDeviceCommands || !details.testLedAvailable} onclick={handleTestLed}>Testar LED</button>
          <button class="tbar-btn danger" disabled={!details.canRemove || busyAction} onclick={handleRemoveDevice}>Remover</button>
        </div>
      </header>

      <section class="command-status">
        <div class="ring" class:active={commandState.inProgress}></div>
        <div class="command-label">{commandState.status}</div>
        <div class="command-percent">{commandState.percent}%</div>
      </section>

      <section class="section">
        <div class="section-header">
          <span>Preview HUB75</span>
          {#if frameSrc}
            <span class="live-dot"></span>
          {/if}
        </div>
        <div class="preview">
          {#if frameSrc}
            <img src={frameSrc} alt="Preview HUB75" width="512" height="256" />
          {:else}
            <div class="empty">Aguardando frames...</div>
          {/if}
        </div>
      </section>

      <section class="section">
        <div class="section-header">
          <span>Brilho do painel LED</span>
          <span>{brightnessDraft}/{details.brightnessMax}</span>
        </div>
        <div class="brightness-row">
          <input
            class="win-slider"
            type="range"
            min={details.brightnessMin}
            max={details.brightnessMax}
            value={brightnessDraft}
            oninput={(event) => handleBrightnessInput(Number((event.target as HTMLInputElement).value))}
            onpointerup={commitBrightness}
            onblur={commitBrightness}
            onchange={commitBrightness}
          />
        </div>
        <div class="detail-line">{details.brightnessStatusLabel}</div>
        <div class="detail-line">{details.heartbeatLabel}</div>
        <div class="app-row">
          <label for="active-app-select">App ativo</label>
          <select
            id="active-app-select"
            value={selectedDevice?.activeAppId ?? ''}
            disabled={!canRunDeviceCommands}
            onchange={(event) => handleChangeApp((event.target as HTMLSelectElement).value)}
          >
            {#each apps as app (app.id)}
              <option value={app.id}>{app.name}</option>
            {/each}
          </select>
        </div>
      </section>

      <section class="section">
        <div class="section-header"><span>Metricas</span></div>
        <div class="metrics-grid">
          <div class="tile">
            <div class="tile-title">Uso do processador</div>
            <div class="tile-value">{details.metrics.loopLoadPercent ?? '-'}%</div>
            <div class="bar"><span style={`width:${Math.round(details.metrics.loopLoadProgress * 100)}%`}></span></div>
          </div>
          <div class="tile">
            <div class="tile-title">Memoria RAM livre</div>
            <div class="tile-value">{details.metrics.heapLabel}</div>
          </div>
          <div class="tile">
            <div class="tile-title">Memoria extra livre</div>
            <div class="tile-value">{details.metrics.psramLabel}</div>
          </div>
        </div>
      </section>

      <section class="section">
        <div class="section-header"><span>{details.loopTrendCaption}</span></div>
        {#if details.loopTrendSeries.length > 0}
          <div class="trend">
            {#each details.loopTrendSeries as sample, index (index)}
              <span class="trend-bar" style={`height:${Math.max(3, sample)}%`}></span>
            {/each}
          </div>
        {:else}
          <div class="empty">{details.loopTrendPlaceholder}</div>
        {/if}
      </section>

      <section class="section">
        <div class="section-header"><span>ESP Dash</span></div>
        <div class="esp-grid">
          <div class="tile"><div class="tile-title">Ligado ha</div><div class="tile-value">{details.espDash.uptimeValue}</div></div>
          <div class="tile"><div class="tile-title">Imagens por segundo</div><div class="tile-value">{details.espDash.fpsValue}</div></div>
          <div class="tile"><div class="tile-title">RAM disponivel</div><div class="tile-value">{details.espDash.heapValue}</div><div class="tile-sub">{details.espDash.heapSubValue}</div></div>
          <div class="tile"><div class="tile-title">Uso do ESP32</div><div class="tile-value">{details.espDash.cpuPercent}%</div></div>
        </div>
        <div class="stream-table">
          <div><span>Imagens enviadas</span><b>{details.espDash.streamFramesReceived}</b></div>
          <div><span>Imagens exibidas</span><b>{details.espDash.streamFramesApplied}</b></div>
          <div><span>Taxa de sucesso</span><b>{details.espDash.successRate}</b></div>
          <div><span>Pacotes perdidos</span><b>{details.espDash.streamGapCount}</b></div>
          <div><span>Imagens com erro</span><b>{details.espDash.streamInvalidCount}</b></div>
          <div><span>Ultima imagem no</span><b>{details.espDash.streamLastSequence}</b></div>
        </div>
      </section>

      <section class="section connectivity">
        <span>{details.connectivity.wifiStateLabel}</span>
        <span>{details.connectivity.portalStateLabel}</span>
        <span>{details.connectivity.uptimeLabel}</span>
        <span>{details.connectivity.lastEventLabel}</span>
        <span>{details.connectivity.auxLedLabel}</span>
      </section>

      <section class="section logs">
        <div class="section-header"><span>Historico de eventos</span></div>
        {#if logs && logs.deviceEntries.length > 0}
          <pre>{logs.deviceEntries.join('\n')}</pre>
        {:else}
          <div class="empty">{details.deviceLogsPlaceholder}</div>
        {/if}
      </section>
    {:else}
      <div class="empty center">Selecione um dispositivo para ver os detalhes.</div>
    {/if}
  </section>
</div>

{#if onboardingOpen}
  <div class="wizard-overlay">
    <div class="wizard-card">
      <header>
        <h3>Novo dispositivo</h3>
        <button class="tbar-btn" disabled={onboardingStatus.inProgress} onclick={closeOnboarding}>Fechar</button>
      </header>
      <div class="wizard-steps">
        <span class="step active"></span>
        <span class="step" class:active={onboardingStatus.percent > 50}></span>
      </div>
      <div class="wizard-body">
        <label for="onboarding-port-select">Dispositivo USB (Porta COM)</label>
        <div class="wizard-row">
          <select id="onboarding-port-select" bind:value={onboardingPortName} disabled={onboardingStatus.inProgress}>
            {#each onboardingPorts as port (port.portName)}
              <option value={port.portName}>{port.label}</option>
            {/each}
          </select>
          <button class="tbar-btn" disabled={onboardingStatus.inProgress} onclick={() => void refreshOnboardingPorts()}>Atualizar portas</button>
        </div>
        <div class="wizard-note">Selecione a porta COM para gravar o firmware. O Wi-Fi sera configurado no AP do ESP32 apos o flash.</div>
        <div class="wizard-status">{onboardingStatus.message}</div>
        <div class="wizard-progress">
          <div class="progress-track"><span style={`width:${onboardingStatus.percent}%`}></span></div>
          <span>{onboardingStatus.percent}%</span>
        </div>
      </div>
      <footer>
        <button class="tbar-btn accent" disabled={onboardingStatus.inProgress} onclick={handleStartOnboarding}>
          {onboardingStatus.inProgress ? 'Processando...' : 'Concluir'}
        </button>
      </footer>
    </div>
  </div>
{/if}

<style>
  .devices-layout {
    display: grid;
    grid-template-columns: 340px minmax(0, 1fr);
    gap: 8px;
    height: 100%;
    min-height: 0;
  }

  .list-panel {
    display: grid;
    grid-template-rows: auto 1fr auto auto;
  }

  .list-header {
    padding: 16px;
    border-bottom: 1px solid var(--stroke);
    font-weight: 600;
    font-size: 16px;
  }

  .device-list {
    overflow: auto;
    padding: 8px;
  }

  .device-row {
    width: 100%;
    display: flex;
    gap: 10px;
    border: 1px solid transparent;
    border-radius: 6px;
    background: transparent;
    color: inherit;
    text-align: left;
    padding: 8px;
    margin-bottom: 4px;
    cursor: pointer;
  }

  .device-row:hover,
  .device-row.selected {
    background: var(--hover);
    border-color: var(--stroke);
  }

  .badge {
    width: 62px;
    height: 40px;
    border-radius: 4px;
    font-size: 11px;
    font-weight: 700;
    display: grid;
    place-items: center;
    flex-shrink: 0;
  }

  .badge-online {
    background: rgba(108, 203, 95, 0.15);
    color: var(--green);
  }

  .badge-offline {
    background: rgba(255, 255, 255, 0.08);
    color: var(--text-3);
  }

  .row-main {
    display: grid;
    min-width: 0;
  }

  .row-name {
    font-weight: 600;
  }

  .row-id {
    font-size: 12px;
    color: var(--text-3);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .row-meta {
    font-size: 12px;
    color: var(--text-2);
  }

  .list-actions {
    padding: 10px;
    border-top: 1px solid var(--stroke);
  }

  .pair-footer {
    padding: 10px;
    border-top: 1px solid var(--stroke);
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    gap: 8px;
  }

  .pair-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--accent);
  }

  .pair-info {
    background: var(--accent);
  }

  .pair-success {
    background: var(--green);
  }

  .pair-warning {
    background: var(--yellow);
  }

  .pair-error {
    background: var(--red);
  }

  .pair-text {
    font-size: 12px;
    color: var(--text-2);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .detail-panel {
    overflow: auto;
  }

  .detail-header {
    padding: 24px;
    border-bottom: 1px solid var(--stroke);
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 16px;
  }

  .detail-header h1 {
    font-size: 28px;
    margin: 0 0 8px 0;
  }

  .detail-line {
    color: var(--text-2);
    margin-bottom: 4px;
  }

  .detail-line.muted {
    color: var(--text-3);
  }

  .detail-right {
    display: grid;
    gap: 8px;
    align-content: start;
  }

  .signal {
    font-size: 12px;
    color: var(--text-2);
    justify-self: end;
  }

  .command-status {
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 12px;
    align-items: center;
    padding: 14px 24px;
    border-bottom: 1px solid var(--stroke);
  }

  .ring {
    width: 16px;
    height: 16px;
    border-radius: 50%;
    border: 2px solid var(--text-3);
  }

  .ring.active {
    border-color: var(--accent);
    border-top-color: transparent;
    animation: spin 1s linear infinite;
  }

  .command-label {
    color: var(--text-2);
  }

  .command-percent {
    color: var(--text-3);
  }

  .section {
    padding: 16px 24px;
    border-top: 1px solid var(--stroke);
  }

  .section-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
    font-size: 16px;
    font-weight: 600;
  }

  .preview {
    border: 1px solid var(--stroke);
    border-radius: 8px;
    background: #000;
    aspect-ratio: 2 / 1;
    overflow: hidden;
  }

  .preview img {
    width: 100%;
    height: 100%;
    object-fit: contain;
    image-rendering: pixelated;
  }

  .live-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--green);
    animation: pulse 1.5s infinite;
  }

  .brightness-row {
    margin-bottom: 8px;
  }

  .app-row {
    margin-top: 10px;
    display: grid;
    gap: 8px;
    max-width: 320px;
  }

  .metrics-grid,
  .esp-grid {
    display: grid;
    gap: 12px;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .tile {
    background: var(--surface-layer);
    border: 1px solid var(--stroke);
    border-radius: 8px;
    padding: 12px;
  }

  .tile-title {
    font-size: 12px;
    color: var(--text-3);
    margin-bottom: 8px;
  }

  .tile-value {
    font-size: 14px;
    color: var(--text-1);
    font-weight: 600;
  }

  .tile-sub {
    margin-top: 4px;
    font-size: 12px;
    color: var(--text-3);
  }

  .bar {
    height: 4px;
    background: rgba(255, 255, 255, 0.08);
    border-radius: 3px;
    margin-top: 8px;
  }

  .bar > span {
    display: block;
    height: 100%;
    background: var(--accent);
    border-radius: 3px;
  }

  .trend {
    display: flex;
    height: 60px;
    align-items: flex-end;
    gap: 3px;
  }

  .trend-bar {
    flex: 1;
    background: var(--accent);
    opacity: 0.85;
    border-radius: 2px 2px 0 0;
  }

  .stream-table {
    margin-top: 12px;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
  }

  .stream-table div {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    color: var(--text-2);
    font-size: 12px;
  }

  .stream-table b {
    color: var(--text-1);
  }

  .connectivity {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    color: var(--text-2);
    font-size: 12px;
  }

  .logs pre {
    background: rgba(0, 0, 0, 0.2);
    border: 1px solid var(--stroke);
    border-radius: 8px;
    padding: 12px;
    max-height: 260px;
    overflow: auto;
    white-space: pre-wrap;
    font-family: 'Cascadia Code', Consolas, monospace;
    font-size: 12px;
  }

  .empty {
    color: var(--text-3);
    font-size: 13px;
  }

  .empty.center {
    display: grid;
    place-items: center;
    height: 100%;
  }

  .inline-error {
    position: fixed;
    right: 14px;
    bottom: 10px;
    background: rgba(196, 43, 28, 0.18);
    border: 1px solid rgba(196, 43, 28, 0.45);
    color: var(--red);
    border-radius: 6px;
    padding: 6px 10px;
    font-size: 12px;
    z-index: 8;
  }

  .wizard-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    backdrop-filter: blur(4px);
    display: grid;
    place-items: center;
    z-index: 20;
  }

  .wizard-card {
    width: min(560px, calc(100vw - 28px));
    border-radius: 10px;
    background: rgba(30, 33, 38, 0.95);
    border: 1px solid var(--stroke);
    overflow: hidden;
  }

  .wizard-card header,
  .wizard-card footer {
    padding: 12px 16px;
    border-bottom: 1px solid var(--stroke);
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .wizard-card footer {
    border-bottom: none;
    border-top: 1px solid var(--stroke);
    justify-content: flex-end;
  }

  .wizard-card h3 {
    margin: 0;
    font-size: 16px;
  }

  .wizard-steps {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
    padding: 12px 16px 0 16px;
  }

  .wizard-steps .step {
    height: 4px;
    border-radius: 2px;
    background: rgba(255, 255, 255, 0.2);
  }

  .wizard-steps .step.active {
    background: var(--accent);
  }

  .wizard-body {
    padding: 12px 16px;
    display: grid;
    gap: 10px;
  }

  .wizard-row {
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 8px;
  }

  .wizard-note {
    color: var(--text-3);
    font-size: 12px;
  }

  .wizard-status {
    color: var(--text-2);
    font-size: 12px;
  }

  .wizard-progress {
    display: grid;
    grid-template-columns: 1fr auto;
    align-items: center;
    gap: 8px;
    font-size: 12px;
  }

  .progress-track {
    height: 8px;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.14);
    overflow: hidden;
  }

  .progress-track > span {
    display: block;
    height: 100%;
    background: var(--accent);
  }

  .danger {
    background: rgba(196, 43, 28, 0.25);
    border-color: rgba(196, 43, 28, 0.4);
    color: var(--red);
  }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.35; }
  }

  @keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
  }

  @media (max-width: 960px) {
    .devices-layout {
      grid-template-columns: 1fr;
      height: 100%;
    }
  }
</style>
