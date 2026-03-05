export interface FrameMessage {
  type: 'frame';
  data: string;
}

export interface HeartbeatMessage {
  type: 'heartbeat';
  devicesOnline: number;
  audioCapturing: boolean;
}

export interface CommandProgressMessage {
  type: 'command-progress';
  deviceId: string;
  commandId?: string;
  stage?: string;
  percent?: number;
  status?: string;
  success?: boolean;
  errorCode?: string;
  message?: string;
}

export interface OnboardingProgressMessage {
  type: 'onboarding-progress';
  stage: string;
  message: string;
  percent: number;
  inProgress: boolean;
  completed: boolean;
  portName?: string;
}

export interface OnboardingResultMessage {
  type: 'onboarding-result';
  success: boolean;
  stage?: string;
  message: string;
  pairCode?: string;
  errorCode?: string;
}

export interface VisualizerSpectrumMessage {
  type: 'visualizer-spectrum';
  bands: number[];
  level: number;
  timestampUtc: string;
}

export interface VisualizerStatusMessage {
  type: 'visualizer-status';
  audioCapturing: boolean;
  targetFps: number;
  status: string;
  contentMode: 'audio' | 'gif';
  hub75Enabled: boolean;
  gifPlaying: boolean;
  gifFrameCount: number;
  updatedAtUtc: string;
}

export interface VisualizerSettingsChangedMessage {
  type: 'visualizer-settings-changed';
  settings: {
    linearBoost: number;
    barCount: number;
    fftSize: number;
    fftSmoothing: number;
    weightingFilter: string;
    frequencyScale: string;
    frequencyMinHz: number;
    frequencyMaxHz: number;
    hub75Enabled: boolean;
    brightness: number;
  };
  updatedAtUtc: string;
}

export type WsMessage =
  | FrameMessage
  | HeartbeatMessage
  | CommandProgressMessage
  | OnboardingProgressMessage
  | OnboardingResultMessage
  | VisualizerSpectrumMessage
  | VisualizerStatusMessage
  | VisualizerSettingsChangedMessage;

type MessageHandler = (message: WsMessage) => void;

export function createPreviewSocket(onMessage: MessageHandler): { close: () => void } {
  let socket: WebSocket | null = null;
  let closed = false;
  let delayMs = 3000;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  const connect = () => {
    if (closed) {
      return;
    }

    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${protocol}//${location.host}/ws/ui/preview`;
    socket = new WebSocket(url);

    socket.onopen = () => {
      delayMs = 3000;
    };

    socket.onmessage = (event) => {
      try {
        const payload = JSON.parse(event.data as string) as WsMessage;
        onMessage(payload);
      } catch {
        // Ignore malformed payloads.
      }
    };

    socket.onclose = () => {
      if (closed) {
        return;
      }

      reconnectTimer = setTimeout(() => {
        delayMs = 3000;
        connect();
      }, delayMs);
    };

    socket.onerror = () => {
      socket?.close();
    };
  };

  connect();

  return {
    close() {
      closed = true;
      if (reconnectTimer) {
        clearTimeout(reconnectTimer);
      }

      socket?.close();
    },
  };
}
