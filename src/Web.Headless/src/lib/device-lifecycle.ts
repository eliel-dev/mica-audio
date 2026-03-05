/** Device lifecycle status labels — port of DeviceLifecyclePolicy from WinUI */

import type { DeviceInfo } from './api';

export interface DevicePresentation {
  primaryState: string;
  secondaryState: string;
  lastSeenLabel: string;
  canRunCommands: boolean;
}

function computeLastSeenLabel(device: DeviceInfo): string {
  if (device.status === 'online') return 'agora';
  if (!device.lastSeenUtc) return 'desconhecido';

  const lastSeen = new Date(device.lastSeenUtc).getTime();
  const now = Date.now();
  const diffMs = now - lastSeen;
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 1) return 'agora';
  if (diffMinutes < 60) return `ha ${diffMinutes}m`;
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `ha ${diffHours}h`;
  const diffDays = Math.floor(diffHours / 24);
  return `ha ${diffDays}d`;
}

export function buildPresentation(device: DeviceInfo): DevicePresentation {
  if (device.status === 'pairing') {
    return {
      primaryState: 'Registrado',
      secondaryState: 'Aguardando provisionamento',
      lastSeenLabel: 'Ultimo contato: desconhecido',
      canRunCommands: false,
    };
  }

  if (device.isRegistered && device.firstSeenUtc == null) {
    return {
      primaryState: 'Registrado',
      secondaryState: 'Nunca conectado',
      lastSeenLabel: 'Ultimo contato: desconhecido',
      canRunCommands: false,
    };
  }

  const lastSeenLabel = `Ultimo contato: ${computeLastSeenLabel(device)}`;

  if (device.status === 'online') {
    return {
      primaryState: 'Online',
      secondaryState: 'Configurado',
      lastSeenLabel,
      canRunCommands: true,
    };
  }

  // Offline
  const secondaryState = device.configState === 'driftsuspected'
    ? 'Configuracao incerta'
    : 'Configurado';

  return {
    primaryState: 'Offline',
    secondaryState,
    lastSeenLabel,
    canRunCommands: false,
  };
}

export function buildStatusLine(device: DeviceInfo): string {
  const p = buildPresentation(device);
  const profile = device.profile || '-';
  return `${p.primaryState} | ${p.secondaryState} | ${p.lastSeenLabel} | Perfil ${profile}`;
}

export function buildRegistrationLine(device: DeviceInfo): string {
  if (!device.isRegistered) return 'Vinculo: nao registrado';
  if (device.status === 'online') return 'Vinculo: configurado e em comunicacao';
  if (device.status === 'pairing') return 'Vinculo: aguardando provisionamento';
  return 'Vinculo: configurado';
}
