<script lang="ts">
  import type { DeviceInfo } from '../lib/api';
  import DetailHeader from './DetailHeader.svelte';
  import PreviewSection from './PreviewSection.svelte';
  import BrightnessSection from './BrightnessSection.svelte';
  import MetricsTiles from './MetricsTiles.svelte';
  import LoopTrend from './LoopTrend.svelte';
  import EspDashboard from './EspDashboard.svelte';
  import ConnectivityBar from './ConnectivityBar.svelte';

  interface Props {
    device: DeviceInfo;
    onHeartbeat?: (devicesOnline: number, audioCapturing: boolean) => void;
  }

  let { device, onHeartbeat }: Props = $props();
</script>

<DetailHeader {device} />
<PreviewSection {onHeartbeat} />
<BrightnessSection {device} />
<MetricsTiles {device} />
<LoopTrend deviceId={device.deviceId} />
<EspDashboard {device} deviceId={device.deviceId} />
<ConnectivityBar {device} />
