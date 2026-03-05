export interface DeviceInfo {
  deviceId: string;
  name: string;
  profile: string;
  status: string;
  isRegistered: boolean;
  lastSeenUtc: string;
  firstSeenUtc: string | null;
  lastTelemetryUtc: string | null;
  lastAuthUtc: string | null;
  configState: string;
  lastKnownIp: string | null;
  lastKnownRssi: number | null;
  uptimeSeconds: number | null;
  loopLoadPercent: number | null;
  freeHeapBytes: number | null;
  largestHeapBlockBytes: number | null;
  psramAvailable: boolean | null;
  freePsramBytes: number | null;
  largestPsramBlockBytes: number | null;
  wifiConnected: boolean | null;
  wifiState: string | null;
  provisioningPortalActive: boolean | null;
  auxLedAvailable: boolean | null;
  testLedAvailable: boolean | null;
  lastWifiEvent: string | null;
  streamLastSequence: number | null;
  streamFramesReceived: number | null;
  streamFramesApplied: number | null;
  streamSequenceGapCount: number | null;
  streamInvalidFrameCount: number | null;
  firmwareVersion: string | null;
  telemetrySequence: number | null;
  brightnessCap: number | null;
  brightnessRequested: number | null;
  brightnessApplied: number | null;
  testLedEnabled: boolean | null;
  testLedDuty: number | null;
  activeAppId: string | null;
  activeAppName: string | null;
  boardModel: string | null;
  panelType: string | null;
  statusLine: string;
  registrationLine: string;
  appLabel: string;
  signalLabel: string;
  lifecyclePrimaryState: string;
  lifecycleSecondaryState: string;
  lifecycleLastSeenLabel: string;
  canRunCommands: boolean;
}

export interface AppInfo {
  id: string;
  name: string;
}

export interface HealthInfo {
  status: string;
  audioCapturing: boolean;
  devicesOnline: number;
  commandInProgress: boolean;
  timestampUtc: string;
}

export interface PairingCodeResult {
  code: string;
  expiresAtUtc: string;
}

export interface CommandStatus {
  inProgress: boolean;
  percent: number;
  status: string;
  stage: string | null;
  deviceId: string | null;
  commandId: string | null;
  errorCode: string | null;
  updatedAtUtc: string;
}

export interface DeviceMetrics {
  statusLabel: string;
  uptimeLabel: string;
  heapLabel: string;
  psramLabel: string;
  networkLabel: string;
  loopLoadPercent: number | null;
  loopLoadProgress: number;
  heapFragmentationProgress: number | null;
  psramFragmentationProgress: number | null;
  hasMetrics: boolean;
  isOfflineSnapshot: boolean;
  isPsramAvailable: boolean;
  placeholderMessage: string;
}

export interface DeviceConnectivity {
  wifiStateLabel: string;
  portalStateLabel: string;
  uptimeLabel: string;
  lastEventLabel: string;
  auxLedLabel: string;
}

export interface DeviceEspDash {
  uptimeValue: string;
  fpsValue: string;
  heapValue: string;
  heapSubValue: string;
  cpuPercent: number;
  successRate: string;
  streamFramesReceived: number;
  streamFramesApplied: number;
  streamGapCount: number;
  streamInvalidCount: number;
  streamLastSequence: number;
  loopSeries: number[];
  heapSeries: number[];
}

export interface DeviceDetails {
  deviceId: string;
  exists: boolean;
  name: string;
  subtitle: string;
  registrationLine: string;
  appLabel: string;
  serverLine: string;
  signalLabel: string;
  canRunCommands: boolean;
  canRemove: boolean;
  testLedAvailable: boolean;
  brightnessMin: number;
  brightnessMax: number;
  brightnessValue: number;
  brightnessValueLabel: string;
  brightnessStatusLabel: string;
  heartbeatLabel: string;
  loopTrendSeries: number[];
  loopTrendCaption: string;
  loopTrendPlaceholder: string;
  metrics: DeviceMetrics;
  espDash: DeviceEspDash;
  connectivity: DeviceConnectivity;
  command: CommandStatus;
  deviceLogCount: number;
  deviceLogsPlaceholder: string;
}

export interface DeviceLogs {
  deviceId: string;
  deviceEntries: string[];
  globalEntries: string[];
  placeholder: string;
}

export interface CommandResponse {
  deviceId: string;
  accepted: boolean;
  completed: boolean;
  success: boolean;
  commandId: string | null;
  stage: string | null;
  errorCode: string | null;
  message: string | null;
}

export interface RemoveDeviceResponse {
  deviceId: string;
  removed: boolean;
  wasOnline: boolean;
  revokeAttempted: boolean;
  revokeSucceeded: boolean;
  revokeError: string | null;
  message: string;
}

export interface OnboardingPort {
  portName: string;
  displayName: string;
  pnpDeviceId: string | null;
  vidPid: string | null;
  isPreferredDevice: boolean;
  priorityRank: number;
  label: string;
}

export interface OnboardingStatus {
  inProgress: boolean;
  stage: string;
  message: string;
  percent: number;
  portName: string | null;
  completed: boolean;
  success: boolean;
  errorCode: string | null;
  pairCode: string | null;
  updatedAtUtc: string;
}

export interface OnboardingStartResponse {
  accepted: boolean;
  status: string;
  message: string | null;
  snapshot: OnboardingStatus;
}

export interface VisualizerSettings {
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
}

export interface VisualizerGifState {
  sourceUrl: string | null;
  scaleMode: string;
  isPlaying: boolean;
  frameCount: number;
  error: string | null;
}

export interface VisualizerSpectrum {
  bands: number[];
  level: number;
  timestampUtc: string;
}

export interface VisualizerState {
  audioCapturing: boolean;
  targetFps: number;
  status: string;
  contentMode: 'audio' | 'gif';
  settings: VisualizerSettings;
  gif: VisualizerGifState;
  spectrum: VisualizerSpectrum;
  updatedAtUtc: string;
}

const BASE = '';

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let body = '';
    try {
      body = await response.text();
    } catch {
      body = '';
    }

    throw new Error(body || `HTTP ${response.status}: ${response.statusText}`);
  }

  return (await response.json()) as T;
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  return readJson<T>(await fetch(`${BASE}${input}`, init));
}

export function fetchDevices(): Promise<DeviceInfo[]> {
  return request<DeviceInfo[]>('/api/ui/devices');
}

export function fetchDeviceDetails(deviceId: string): Promise<DeviceDetails> {
  return request<DeviceDetails>(`/api/ui/devices/${encodeURIComponent(deviceId)}/details`);
}

export function fetchDeviceLogs(deviceId: string): Promise<DeviceLogs> {
  return request<DeviceLogs>(`/api/ui/devices/${encodeURIComponent(deviceId)}/logs`);
}

export function fetchApps(): Promise<AppInfo[]> {
  return request<AppInfo[]>('/api/ui/apps');
}

export function fetchHealth(): Promise<HealthInfo> {
  return request<HealthInfo>('/api/ui/health');
}

export function setBrightness(deviceId: string, value: number): Promise<CommandResponse> {
  return request<CommandResponse>(`/api/ui/devices/${encodeURIComponent(deviceId)}/brightness`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ value }),
  });
}

export function setApp(deviceId: string, appId: string): Promise<CommandResponse> {
  return request<CommandResponse>(`/api/ui/devices/${encodeURIComponent(deviceId)}/app`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ appId }),
  });
}

export function testLed(deviceId: string): Promise<CommandResponse> {
  return request<CommandResponse>(`/api/ui/devices/${encodeURIComponent(deviceId)}/test-led`, {
    method: 'POST',
  });
}

export function removeDevice(deviceId: string): Promise<RemoveDeviceResponse> {
  return request<RemoveDeviceResponse>(`/api/ui/devices/${encodeURIComponent(deviceId)}`, {
    method: 'DELETE',
  });
}

export function generatePairingCode(): Promise<PairingCodeResult> {
  return request<PairingCodeResult>('/api/ui/pairing-code', { method: 'POST' });
}

export function fetchOnboardingPorts(): Promise<OnboardingPort[]> {
  return request<OnboardingPort[]>('/api/ui/onboarding/ports');
}

export function startOnboarding(portName: string): Promise<OnboardingStartResponse> {
  return request<OnboardingStartResponse>('/api/ui/onboarding/start', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ portName }),
  });
}

export function fetchOnboardingStatus(): Promise<OnboardingStatus> {
  return request<OnboardingStatus>('/api/ui/onboarding/status');
}

export function fetchVisualizerState(): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/state');
}

export function updateVisualizerSettings(settings: Partial<VisualizerSettings>): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  });
}

export function setVisualizerHub75(enabled: boolean): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/hub75', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ enabled }),
  });
}

export function setVisualizerContentMode(mode: 'audio' | 'gif'): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/content-mode', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ mode }),
  });
}

export function loadVisualizerGifUrl(url: string): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/gif/url', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  });
}

export function playVisualizerGif(): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/gif/play', { method: 'POST' });
}

export function pauseVisualizerGif(): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/gif/pause', { method: 'POST' });
}

export function stopVisualizerGif(): Promise<VisualizerState> {
  return request<VisualizerState>('/api/ui/visualizer/gif/stop', { method: 'POST' });
}
