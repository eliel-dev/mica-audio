<script lang="ts">
  import type { DeviceInfo } from '../lib/api';
  import { buildStatusLine, buildRegistrationLine, buildPresentation } from '../lib/device-lifecycle';

  interface Props {
    device: DeviceInfo;
  }

  let { device }: Props = $props();

  const presentation = $derived(buildPresentation(device));
  const statusLine = $derived(buildStatusLine(device));
  const registrationLine = $derived(buildRegistrationLine(device));
  const signalLabel = $derived(
    device.lastKnownRssi != null ? `${device.lastKnownRssi} dBm` : '-'
  );
  const appLabel = $derived(
    device.status === 'online'
      ? `App ativo: ${device.activeAppName ?? '-'}`
      : `Ultimo app conhecido: ${device.activeAppName ?? '-'}`
  );
</script>

<div class="d-header">
  <div class="d-title-row">
    <div class="d-left">
      <div class="d-title">{device.name}</div>
      <div class="d-sub">{statusLine}</div>
      <div class="d-sub">{registrationLine}</div>
      <div class="d-sub">{appLabel}</div>
      {#if device.lastKnownIp}
        <div class="d-sub muted">IP: {device.lastKnownIp} | Perfil {device.profile}</div>
      {/if}
    </div>
    <div class="d-right">
      <span class="d-signal">Sinal <b>{signalLabel}</b></span>
      <div class="d-btn-stack">
        <button class="tbar-btn" disabled={!device.testLedAvailable || !presentation.canRunCommands}>
          Testar LED
        </button>
        <button class="tbar-btn danger" disabled={!presentation.canRunCommands}>
          Remover
        </button>
      </div>
    </div>
  </div>
</div>

<style>
  .d-header {
    padding: 24px;
    border-bottom: 1px solid var(--stroke);
  }

  .d-title-row {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 16px;
  }

  .d-left {
    flex: 1;
  }

  .d-title {
    font-size: 28px;
    font-weight: 600;
    line-height: 1.2;
    margin-bottom: 8px;
    color: var(--text-1);
  }

  .d-sub {
    font-size: 14px;
    color: var(--text-2);
    line-height: 1.5;
  }

  .d-sub.muted {
    color: var(--text-3);
  }

  .d-right {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 8px;
    flex-shrink: 0;
  }

  .d-signal {
    font-size: 12px;
    color: var(--text-2);
  }

  .d-signal b {
    color: var(--text-1);
    font-weight: 600;
  }

  .d-btn-stack {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .d-btn-stack .tbar-btn {
    min-width: 132px;
    justify-content: flex-start;
  }

  .danger {
    background: rgba(196, 43, 28, 0.2);
    border-color: rgba(196, 43, 28, 0.4);
    color: var(--red);
  }

  .danger:hover {
    background: rgba(196, 43, 28, 0.3);
  }
</style>
