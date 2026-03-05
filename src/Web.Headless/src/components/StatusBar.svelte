<script lang="ts">
  interface Props {
    devicesOnline: number;
    audioCapturing: boolean;
  }

  let { devicesOnline, audioCapturing }: Props = $props();
</script>

<footer class="statusbar">
  <span class="sb-item">
    <span class="sb-dot" class:sb-dot--active={devicesOnline > 0}></span>
    Dispositivos: <b>{devicesOnline}</b>
  </span>
  <span class="sb-sep"></span>
  <span class="sb-item">
    <span class="sb-dot" class:sb-dot--active={audioCapturing}></span>
    Audio: <b>{audioCapturing ? 'Capturando' : 'Inativo'}</b>
  </span>
</footer>

<style>
  .statusbar {
    height: 24px;
    background: #191919;
    border-top: 1px solid rgba(255, 255, 255, 0.05);
    display: flex;
    align-items: center;
    padding: 0 14px;
    font-size: 11px;
    color: var(--text-3);
    flex-shrink: 0;
    gap: 12px;
    user-select: none;
  }

  .sb-item {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .sb-item b {
    color: var(--text-2);
    font-weight: 600;
  }

  .sb-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--text-3);
    flex-shrink: 0;
  }

  .sb-dot--active {
    background: var(--green);
  }

  .sb-sep {
    width: 1px;
    height: 12px;
    background: rgba(255, 255, 255, 0.08);
  }
</style>
