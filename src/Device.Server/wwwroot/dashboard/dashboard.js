// DOCS: docs/wiki/reference/device-observability-dashboard.md
(function () {
  "use strict";

  const BRIGHT_MIN = 30;
  const BRIGHT_MAX = 160;
  const HEAP_TOTAL_BYTES_FALLBACK = 320000;
  const PSRAM_TOTAL_BYTES_FALLBACK = 8000000;
  const HISTORY_POINTS = 30;
  const HEALTH_TONES = ["health-ok", "health-warn", "health-bad"];
  const HOST_BRIDGE_AVAILABLE = !!(window.chrome && window.chrome.webview);
  const healthHistory = new Map();
  const heapHistory = new Map();
  const lastTelemetrySequence = new Map();

  let selectedDeviceId = null;
  let currentDevice = null;
  let activeSocket = null;
  let readyAcknowledged = false;
  let lastCommittedBrightness = null;

  function $(id) {
    return document.getElementById(id);
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function normalizeBrightness(value) {
    const fallback = 120;
    if (typeof value !== "number" || Number.isNaN(value)) {
      return fallback;
    }

    return clamp(Math.round(value), BRIGHT_MIN, BRIGHT_MAX);
  }

  function setBrightnessUi(value) {
    const safe = normalizeBrightness(value);
    const ratio = (safe - BRIGHT_MIN) / (BRIGHT_MAX - BRIGHT_MIN);
    const pct = Math.round(ratio * 100);
    $("bright-label").textContent = String(pct);
    $("slider-bright").value = String(safe);
    $("slider-bright").style.setProperty("--slider-pct", `${pct}%`);
    $("bright-sun").style.transform = `scale(${(0.84 + ratio * 0.22).toFixed(3)})`;
    $("bright-sun").style.opacity = (0.45 + ratio * 0.4).toFixed(3);
  }

  function buildWebSocketUrl(deviceId) {
    const url = new URL(`/ws/device/${encodeURIComponent(deviceId)}`, window.location.origin);
    url.protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
    return url.toString();
  }

  function postHostMessage(message) {
    if (HOST_BRIDGE_AVAILABLE) {
      window.chrome.webview.postMessage(message);
    }
  }

  function applyStandaloneMode() {
    const readonly = !HOST_BRIDGE_AVAILABLE;
    $("action-mode-note").style.display = readonly ? "" : "none";
    document.querySelector(".brightness-card").classList.toggle("readonly", readonly);
    $("btn-led").style.display = readonly ? "none" : "";
    $("btn-rm").style.display = readonly ? "none" : "";
  }

  function triggerDetailAnimation() {
    const host = $("detail-scroll");
    host.classList.remove("animate");
    window.requestAnimationFrame(() => {
      host.classList.add("animate");
    });
  }

  function setWifiIconState(element, wifiState, signalDbm) {
    element.className = "wifi-icon";

    if (wifiState === "connecting") {
      element.classList.add("connecting");
    }
    else if (wifiState === "portal") {
      element.classList.add("portal");
    }
    else if (!wifiState || wifiState === "disconnected" || signalDbm == null) {
      element.classList.add("offline");
      return;
    }

    if (signalDbm <= -70) {
      element.classList.add("rssi-weak");
    }
    else if (signalDbm <= -56) {
      element.classList.add("rssi-medium");
    }
    else {
      element.classList.add("rssi-strong");
    }
  }

  function fmtSignal(signalDbm, online) {
    return online && typeof signalDbm === "number" ? `${signalDbm} dBm` : "-";
  }

  function fmtPercent(value) {
    return typeof value === "number" ? String(clamp(Math.round(value), 0, 100)) : "-";
  }

  function fmtInteger(value) {
    return typeof value === "number" ? String(Math.round(value)) : "-";
  }

  function formatTemperatureCelsius(value) {
    return typeof value === "number" && Number.isFinite(value)
      ? value.toFixed(1)
      : "-";
  }

  function formatCurrentFirmwareVersion(value) {
    return typeof value === "string" && value.trim().length > 0
      ? value.trim()
      : "Firmware atual nao identificado";
  }

  function formatLatestFirmwareVersion(value) {
    return typeof value === "string" && value.trim().length > 0
      ? value.trim()
      : "Sem release oficial";
  }

  function resolveHealthState(percent) {
    const safe = clamp(Math.round(percent), 0, 100);
    if (safe >= 90) {
      return {
        tone: "health-ok",
        label: "Saudavel: loop estavel",
        color: "#4EC94E",
      };
    }

    if (safe >= 75) {
      return {
        tone: "health-warn",
        label: "Atencao: latencia moderada",
        color: "#F6B000",
      };
    }

    return {
      tone: "health-bad",
      label: "Sobrecarregado: latencia elevada",
      color: "#D94B4B",
    };
  }

  function setHealthVisualState(percent) {
    const card = $("m-health-card");
    const bar = $("m-health-bar");
    card.classList.remove(...HEALTH_TONES);
    bar.classList.remove(...HEALTH_TONES);

    if (typeof percent !== "number" || Number.isNaN(percent)) {
      return null;
    }

    const state = resolveHealthState(percent);
    card.classList.add(state.tone);
    bar.classList.add(state.tone);
    return state;
  }

  function computeFragmentationPercent(freeBytes, largestBlockBytes) {
    if (typeof freeBytes !== "number" || typeof largestBlockBytes !== "number" || freeBytes <= 0 || largestBlockBytes < 0) {
      return null;
    }

    return clamp(Math.round((1 - (largestBlockBytes / freeBytes)) * 100), 0, 100);
  }

  function computeFallbackPercent(freeBytes, totalBytes) {
    if (typeof freeBytes !== "number" || Number.isNaN(freeBytes) || typeof totalBytes !== "number" || totalBytes <= 0) {
      return null;
    }

    return clamp(Math.round((freeBytes / totalBytes) * 100), 0, 100);
  }

  function resolveHeapPercent(device) {
    if (typeof device.heapFreePercent === "number") {
      return clamp(Math.round(device.heapFreePercent), 0, 100);
    }

    return computeFallbackPercent(device.heapFreeBytes, HEAP_TOTAL_BYTES_FALLBACK);
  }

  function resolvePsramPercent(device) {
    if (device.psramAvailable === false) {
      return null;
    }

    if (typeof device.psramFreePercent === "number") {
      return clamp(Math.round(device.psramFreePercent), 0, 100);
    }

    return computeFallbackPercent(device.psramFreeBytes, PSRAM_TOTAL_BYTES_FALLBACK);
  }

  function buildHeapSubLabel(device) {
    const fragmentation = typeof device.heapFragmentationPercent === "number"
      ? clamp(Math.round(device.heapFragmentationPercent), 0, 100)
      : computeFragmentationPercent(device.heapFreeBytes, device.heapLargestBlockBytes);
    return fragmentation != null
      ? `Frag. de memoria ${fragmentation}%`
      : "Frag. de memoria -";
  }

  function buildPsramSubLabel(device) {
    if (device.psramAvailable === false) {
      return "PSRAM indisponivel";
    }

    if (typeof device.psramTotalBytes === "number") {
      const mb = Math.round(device.psramTotalBytes / (1024 * 1024));
      return `Base ${mb} MB`;
    }

    if (typeof device.psramFreeBytes === "number") {
      return "Base 8 MB";
    }

    return "PSRAM disponivel";
  }

  function updateProgressBar(id, percent) {
    const safe = typeof percent === "number" ? clamp(Math.round(percent), 0, 100) : 0;
    $(id).style.width = `${safe}%`;
  }

  function getHistory(map, deviceId) {
    if (!map.has(deviceId)) {
      map.set(deviceId, Array(HISTORY_POINTS).fill(null));
    }

    return map.get(deviceId);
  }

  function pushHistoryValue(map, deviceId, value) {
    const history = getHistory(map, deviceId);
    history.push(value);
    while (history.length > HISTORY_POINTS) {
      history.shift();
    }
  }

  function updateHistory(device) {
    if (!device || !device.deviceId) {
      return;
    }

    const telemetryKey = device.deviceId;
    const nextSequence = typeof device.telemetrySequence === "number" ? device.telemetrySequence : null;
    const currentSequence = lastTelemetrySequence.get(telemetryKey);
    if (nextSequence != null && currentSequence === nextSequence) {
      return;
    }

    lastTelemetrySequence.set(telemetryKey, nextSequence);
    pushHistoryValue(
      healthHistory,
      telemetryKey,
      typeof device.loopHealthyPercent === "number"
        ? clamp(Math.round(device.loopHealthyPercent), 0, 100)
        : null);
    pushHistoryValue(
      heapHistory,
      telemetryKey,
      typeof device.heapFreeBytes === "number" ? Math.round(device.heapFreeBytes / 1024) : null);
  }

  function drawHistoryChart(canvas, values, color, maxValue) {
    const context = canvas.getContext("2d");
    const width = canvas.clientWidth || 720;
    const height = canvas.clientHeight || 120;
    const dpr = window.devicePixelRatio || 1;

    canvas.width = Math.max(1, Math.floor(width * dpr));
    canvas.height = Math.max(1, Math.floor(height * dpr));
    context.setTransform(dpr, 0, 0, dpr, 0, 0);
    context.clearRect(0, 0, width, height);

    const left = 32;
    const right = width - 12;
    const top = 10;
    const bottom = height - 20;
    const chartHeight = Math.max(1, bottom - top);
    const chartWidth = Math.max(1, right - left);

    context.strokeStyle = "rgba(255,255,255,0.08)";
    context.lineWidth = 1;
    context.font = "11px Segoe UI";
    context.fillStyle = "rgba(255,255,255,0.32)";

    for (let index = 0; index < 3; index += 1) {
      const ratio = index / 2;
      const y = top + chartHeight * ratio;
      context.beginPath();
      context.moveTo(left, y);
      context.lineTo(right, y);
      context.stroke();

      const labelValue = Math.round(maxValue * (1 - ratio));
      context.fillText(String(labelValue), 4, y + 4);
    }

    const points = values.map((value, index) => ({
      x: left + (chartWidth / (HISTORY_POINTS - 1)) * index,
      y: value == null
        ? null
        : top + chartHeight * (1 - clamp(value / maxValue, 0, 1)),
    }));

    context.beginPath();
    context.moveTo(left, bottom);
    let hasStarted = false;
    for (const point of points) {
      if (point.y == null) {
        continue;
      }

      if (!hasStarted) {
        context.lineTo(point.x, point.y);
        hasStarted = true;
      }
      else {
        context.lineTo(point.x, point.y);
      }
    }
    context.lineTo(right, bottom);
    context.closePath();
    context.fillStyle = `${color}22`;
    context.fill();

    context.beginPath();
    hasStarted = false;
    for (const point of points) {
      if (point.y == null) {
        continue;
      }

      if (!hasStarted) {
        context.moveTo(point.x, point.y);
        hasStarted = true;
      }
      else {
        context.lineTo(point.x, point.y);
      }
    }

    context.strokeStyle = color;
    context.lineWidth = 1.5;
    context.stroke();
  }

  function renderCharts(device) {
    const deviceId = device && device.deviceId ? device.deviceId : selectedDeviceId;
    const healthValues = deviceId ? [...getHistory(healthHistory, deviceId)] : Array(HISTORY_POINTS).fill(null);
    const heapValues = deviceId ? [...getHistory(heapHistory, deviceId)] : Array(HISTORY_POINTS).fill(null);
    const healthState = device && typeof device.loopHealthyPercent === "number"
      ? resolveHealthState(device.loopHealthyPercent)
      : null;

    drawHistoryChart($("chart-loop"), healthValues, healthState ? healthState.color : "#4EC94E", 100);

    const heapMaxKb = (() => {
      if (device && typeof device.heapTotalBytes === "number" && device.heapTotalBytes > 0) {
        return Math.max(64, Math.round(device.heapTotalBytes / 1024));
      }

      const values = heapValues.filter((value) => typeof value === "number");
      return values.length === 0 ? 320 : Math.max(320, ...values);
    })();
    drawHistoryChart($("chart-heap"), heapValues, "#4EC94E", heapMaxKb);
  }

  function resetVisualState() {
    currentDevice = null;
    lastCommittedBrightness = null;
    $("s-rssi").textContent = "-";
    setWifiIconState($("s-rssi-icon"), null, null);
    $("firmware-row").style.display = "none";
    $("firmware-current").textContent = "-";
    $("firmware-latest").textContent = "-";
    $("btn-fw").style.display = "none";
    $("btn-fw").disabled = true;
    $("btn-led").disabled = true;
    $("btn-rm").disabled = true;
    $("slider-bright").disabled = true;
    applyStandaloneMode();
    $("m-ph").textContent = "Selecione um dispositivo para ver a telemetria.";
    $("m-ph").style.display = "";
    $("m-grid").style.display = "none";
    $("espdash-sec").style.display = "none";
    $("m-health").textContent = "-";
    $("m-health-s").textContent = "Saudavel: loop estavel";
    $("m-heap").textContent = "-";
    $("m-psram").textContent = "-";
    $("m-temp").textContent = "-";
    $("m-temp-unit").textContent = "";
    $("m-temp-s").textContent = "Sensor interno indisponivel";
    $("m-heap-s").textContent = "Frag. de memoria -";
    $("m-psram-s").textContent = "PSRAM indisponivel";
    $("s-fps").textContent = "-";
    setHealthVisualState(null);
    updateProgressBar("m-health-bar", 0);
    updateProgressBar("m-heap-bar", 0);
    updateProgressBar("m-psram-bar", 0);
    setBrightnessUi(120);
    renderCharts(null);
  }

  function render(device) {
    currentDevice = device;
    $("s-rssi").textContent = fmtSignal(device.signalDbm, device.online);
    setWifiIconState($("s-rssi-icon"), device.wifiState, device.signalDbm);

    const embeddedMode = HOST_BRIDGE_AVAILABLE;
    const ledEnabled = embeddedMode && !!selectedDeviceId && device.testLedAvailable !== false;
    const firmwareUpdateAvailable = embeddedMode
      && !!selectedDeviceId
      && device.online === true
      && device.firmwareUpdateSupported === true
      && device.firmwareUpdateAvailable === true;
    $("btn-led").style.display = embeddedMode ? "" : "none";
    $("btn-rm").style.display = embeddedMode ? "" : "none";
    $("btn-led").disabled = !ledEnabled;
    $("btn-rm").disabled = !embeddedMode || !selectedDeviceId;
    $("btn-fw").style.display = firmwareUpdateAvailable ? "" : "none";
    $("btn-fw").disabled = !firmwareUpdateAvailable;

    const currentFirmwareKnown = typeof device.firmwareVersion === "string" && device.firmwareVersion.trim().length > 0;
    const latestFirmwareKnown = typeof device.latestFirmwareVersion === "string" && device.latestFirmwareVersion.trim().length > 0;
    $("firmware-row").style.display = selectedDeviceId && (currentFirmwareKnown || latestFirmwareKnown || device.firmwareUpdateSupported === true)
      ? "grid"
      : "none";
    $("firmware-current").textContent = formatCurrentFirmwareVersion(device.firmwareVersion);
    $("firmware-latest").textContent = formatLatestFirmwareVersion(device.latestFirmwareVersion);

    const brightnessValue = normalizeBrightness(device.brightnessRequested ?? device.brightnessApplied ?? 120);
    setBrightnessUi(brightnessValue);
    $("slider-bright").disabled = !embeddedMode || !device.online;
    applyStandaloneMode();

    const healthPercent = typeof device.loopHealthyPercent === "number"
      ? clamp(Math.round(device.loopHealthyPercent), 0, 100)
      : null;
    const heapPercent = resolveHeapPercent(device);
    const psramPercent = resolvePsramPercent(device);
    const chipTemperatureAvailable = typeof device.chipTemperatureCelsius === "number" && Number.isFinite(device.chipTemperatureCelsius);
    const hasMetrics =
      healthPercent != null ||
      heapPercent != null ||
      psramPercent != null ||
      chipTemperatureAvailable;

    if (!hasMetrics) {
      $("m-ph").textContent = device.online
        ? "Online: aguardando primeira telemetria do dispositivo."
        : "Offline: sem telemetria em tempo real no momento.";
      $("m-ph").style.display = "";
      $("m-grid").style.display = "none";
      $("m-health").textContent = "-";
      $("m-health-s").textContent = "Saudavel: loop estavel";
      $("m-heap").textContent = "-";
      $("m-psram").textContent = "-";
      $("m-temp").textContent = "-";
      $("m-temp-unit").textContent = "";
      $("m-temp-s").textContent = "Sensor interno indisponivel";
      $("m-heap-s").textContent = "Frag. de memoria -";
      $("m-psram-s").textContent = "PSRAM indisponivel";
      setHealthVisualState(null);
      updateProgressBar("m-health-bar", 0);
      updateProgressBar("m-heap-bar", 0);
      updateProgressBar("m-psram-bar", 0);
    }
    else {
      $("m-ph").style.display = "none";
      $("m-grid").style.display = "grid";
      $("m-health").textContent = healthPercent != null ? String(healthPercent) : "-";
      $("m-heap").textContent = heapPercent != null ? String(heapPercent) : "-";
      $("m-psram").textContent = psramPercent != null ? String(psramPercent) : "-";
      $("m-temp").textContent = formatTemperatureCelsius(device.chipTemperatureCelsius);
      $("m-temp-unit").textContent = chipTemperatureAvailable ? "\u00B0C" : "";
      $("m-temp-s").textContent = chipTemperatureAvailable
        ? "Sensor interno do ESP32-S3"
        : "Sensor interno indisponivel";
      $("m-heap-s").textContent = buildHeapSubLabel(device);
      $("m-psram-s").textContent = buildPsramSubLabel(device);
      const healthState = setHealthVisualState(healthPercent);
      $("m-health-s").textContent = healthState ? healthState.label : "Saudavel: loop estavel";
      updateProgressBar("m-health-bar", healthPercent);
      updateProgressBar("m-heap-bar", heapPercent);
      updateProgressBar("m-psram-bar", psramPercent);
    }

    $("espdash-sec").style.display = selectedDeviceId ? "block" : "none";
    $("s-fps").textContent = fmtInteger(device.hub75Fps);
    renderCharts(device);
  }

  function closeActiveSocket() {
    if (!activeSocket) {
      return;
    }

    const socket = activeSocket;
    activeSocket = null;
    socket.onopen = null;
    socket.onmessage = null;
    socket.onerror = null;
    socket.onclose = null;
    try {
      socket.close(1000, "selection_changed");
    }
    catch {
      // ignore close races
    }
  }

  function connectSelectedDevice() {
    closeActiveSocket();

    if (!selectedDeviceId) {
      resetVisualState();
      return;
    }

    const socket = new WebSocket(buildWebSocketUrl(selectedDeviceId));
    activeSocket = socket;

    socket.onmessage = (event) => {
      let dto;
      try {
        dto = JSON.parse(event.data);
      }
      catch {
        return;
      }

      updateHistory(dto);
      render(dto);
    };

    socket.onclose = () => {
      if (activeSocket === socket) {
        activeSocket = null;
      }
    };
  }

  function selectDevice(deviceId) {
    const normalized = typeof deviceId === "string" && deviceId.trim().length > 0 ? deviceId.trim() : null;
    if (normalized === selectedDeviceId) {
      return;
    }

    selectedDeviceId = normalized;
    triggerDetailAnimation();
    connectSelectedDevice();
  }

  function commitBrightness() {
    if (!HOST_BRIDGE_AVAILABLE || !selectedDeviceId) {
      return;
    }

    const brightness = normalizeBrightness(Number($("slider-bright").value));
    if (lastCommittedBrightness === brightness) {
      return;
    }

    lastCommittedBrightness = brightness;
    postHostMessage({
      type: "set-brightness",
      deviceId: selectedDeviceId,
      brightness,
    });
  }

  function wireActions() {
    const ledHandler = () => {
      if (!selectedDeviceId || (currentDevice && currentDevice.testLedAvailable === false)) {
        return;
      }

      postHostMessage({ type: "test-led", deviceId: selectedDeviceId });
    };

    const updateFirmwareHandler = () => {
      if (!selectedDeviceId
        || !currentDevice
        || currentDevice.online !== true
        || currentDevice.firmwareUpdateSupported !== true
        || currentDevice.firmwareUpdateAvailable !== true) {
        return;
      }

      postHostMessage({ type: "update-firmware", deviceId: selectedDeviceId });
    };

    const removeDeviceHandler = () => {
      if (!selectedDeviceId) {
        return;
      }

      postHostMessage({ type: "remove-device", deviceId: selectedDeviceId });
    };

    $("btn-fw").addEventListener("click", updateFirmwareHandler);
    $("btn-led").addEventListener("click", ledHandler);
    $("btn-rm").addEventListener("click", removeDeviceHandler);

    $("slider-bright").addEventListener("input", (event) => {
      setBrightnessUi(Number(event.target.value));
    });
    $("slider-bright").addEventListener("change", commitBrightness);
    $("slider-bright").addEventListener("pointerup", commitBrightness);
  }

  function wireHostBridge() {
    if (!HOST_BRIDGE_AVAILABLE) {
      return;
    }

    window.chrome.webview.addEventListener("message", (event) => {
      const message = event.data || {};
      if (message.type === "select-device") {
        selectDevice(message.deviceId);
      }
      else if (message.type === "clear-selection") {
        selectedDeviceId = null;
        closeActiveSocket();
        resetVisualState();
      }
      else if (message.type === "ready-ack") {
        readyAcknowledged = true;
      }
    });

    postHostMessage({ type: "ready" });
  }

  function initStandaloneSelection() {
    const url = new URL(window.location.href);
    const deviceId = url.searchParams.get("deviceId");
    if (deviceId) {
      selectDevice(deviceId);
    }
  }

  window.addEventListener("resize", () => {
    renderCharts(currentDevice);
  });

  document.addEventListener("DOMContentLoaded", () => {
    wireActions();
    wireHostBridge();
    initStandaloneSelection();
    applyStandaloneMode();
    if (!readyAcknowledged && !HOST_BRIDGE_AVAILABLE) {
      readyAcknowledged = true;
    }
    resetVisualState();
  });
})();
