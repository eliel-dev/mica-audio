<script lang="ts">
  import type { DeviceInfo } from '../lib/api';
  import { formatUptime } from '../lib/format';

  interface Props {
    device: DeviceInfo;
  }

  let { device }: Props = $props();

  const hasData = $derived(device.wifiState != null || device.uptimeSeconds != null);
</script>

{#if hasData}
  <div class="conn-section">
    <div class="conn-bar">
      <span class="conn-item">
        Rede Wi-Fi: <b>{device.wifiState ?? '-'}</b>
        {#if device.lastKnownRssi != null}
          ({device.lastKnownRssi} dBm)
        {/if}
      </span>
      <span class="conn-item">
        Modo configuracao: <b>{device.provisioningPortalActive ? 'Ativo' : '-'}</b>
      </span>
      <span class="conn-item">
        Ligado ha: <b>{formatUptime(device.uptimeSeconds)}</b>
      </span>
      <span class="conn-item">
        Ultimo evento de rede: <b>{device.lastWifiEvent ?? '-'}</b>
      </span>
      <span class="conn-item">
        LED auxiliar: <b>{device.auxLedAvailable ? 'Disponivel' : 'Indisponivel'}</b>
      </span>
    </div>
  </div>
{/if}

<style>
  .conn-section {
    border-top: 1px solid var(--stroke);
  }

  .conn-bar {
    display: flex;
    flex-wrap: wrap;
    gap: 20px;
    padding: 10px 24px;
    font-size: 12px;
    color: var(--text-2);
  }

  .conn-item b {
    color: var(--text-1);
    font-weight: 600;
  }
</style>
