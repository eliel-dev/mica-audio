// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#shadow-retained-de-sessao
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md

#include "mica_session.h"

#include "mica_globals.h"
#include "mica_network.h"

namespace {

constexpr const char* kVisualizerAppId = "visualizer-hub75";
constexpr const char* kPanelsAppId = "panels-hub75";

bool isExpired(unsigned long nowMs, unsigned long expiresAtMs) {
  return expiresAtMs != 0 && static_cast<unsigned long>(nowMs - expiresAtMs) < (1UL << 31);
}

uint32_t remainingLeaseMs(unsigned long nowMs, unsigned long expiresAtMs) {
  if (expiresAtMs == 0 || isExpired(nowMs, expiresAtMs)) {
    return 0;
  }

  return static_cast<uint32_t>(expiresAtMs - nowMs);
}

void markSessionMutation(unsigned long nowMs) {
  gSessionShadowState.shadowVersion++;
  gSessionShadowState.lastMutationAtMs = nowMs;
  gSessionShadowState.dirty = true;
}

void clearActiveOwnerLeaseInternal() {
  gSessionShadowState.activeClientId = "";
  gSessionShadowState.activeOwnerExpiresAtMs = 0;
}

void clearLockLeaseInternal() {
  gSessionShadowState.lock.held = false;
  gSessionShadowState.lock.clientId = "";
  gSessionShadowState.lock.token = "";
  gSessionShadowState.lock.reason = "";
  gSessionShadowState.lock.acquiredAtMs = 0;
  gSessionShadowState.lock.lastHeartbeatAtMs = 0;
  gSessionShadowState.lock.expiresAtMs = 0;
}

String normalizedAppId(JsonObjectConst parameters) {
  String appId = parameters["appId"] | "";
  appId.trim();
  appId.toLowerCase();
  return appId;
}

}  // namespace

const char* clientSessionModeName(ClientSessionMode mode) {
  switch (mode) {
    case ClientSessionMode::Visualizer:
      return "visualizer";
    case ClientSessionMode::Panels:
      return "panels";
    case ClientSessionMode::ClockFallback:
      return "clock_fallback";
    case ClientSessionMode::Idle:
    default:
      return "idle";
  }
}

const char* clientSessionFallbackStateName() {
  return gClientDisconnectedFallbackActive ? "client_disconnected" : "none";
}

bool isSessionAwareEnvelope(const ControlCommandEnvelope& envelope) {
  return envelope.clientId.length() > 0;
}

bool isSessionCommandName(const char* command) {
  return command != nullptr
      && (strcmp(command, "session_heartbeat") == 0
          || strcmp(command, "session_lock_acquire") == 0
          || strcmp(command, "session_lock_release") == 0);
}

bool doesCommandRequireSessionLock(const char* command) {
  return command != nullptr
      && (strcmp(command, "enter_provisioning") == 0
          || strcmp(command, "revoke_and_restart") == 0
          || strcmp(command, "update_firmware") == 0
          || strcmp(command, "install_app") == 0
          || strcmp(command, "set_app_config") == 0);
}

ClientSessionMode resolveSessionModeHint(const char* command, JsonObjectConst parameters) {
  if (command == nullptr || command[0] == '\0') {
    return gSessionShadowState.mode;
  }

  if (strcmp(command, "queue_panels_batch") == 0) {
    return ClientSessionMode::Panels;
  }

  if (strcmp(command, "activate_app") == 0
      || strcmp(command, "install_app") == 0
      || strcmp(command, "set_app_config") == 0) {
    const String appId = normalizedAppId(parameters);
    if (appId == kVisualizerAppId) {
      return ClientSessionMode::Visualizer;
    }

    if (appId == kPanelsAppId) {
      return ClientSessionMode::Panels;
    }

    return ClientSessionMode::Idle;
  }

  if (gSessionShadowState.mode == ClientSessionMode::Visualizer
      || gSessionShadowState.mode == ClientSessionMode::Panels) {
    return gSessionShadowState.mode;
  }

  return ClientSessionMode::Idle;
}

void initializeClientSessionRuntime() {
  gSessionShadowState = {};
  gSessionShadowState.mode = ClientSessionMode::ClockFallback;
  gSessionShadowState.dirty = true;
  gClientDisconnectedFallbackActive = false;
  gSessionLastLeaseTickMs = 0;
}

void processClientSessionRuntime(unsigned long nowMs) {
  if (gSessionLastLeaseTickMs != 0 && (nowMs - gSessionLastLeaseTickMs) < kSessionLeaseTickMs) {
    if (gSessionShadowState.dirty) {
      (void)publishClientSessionShadow(nowMs, false);
    }
    return;
  }

  gSessionLastLeaseTickMs = nowMs;
  expireSessionLeases(nowMs);
  if (gSessionShadowState.dirty) {
    (void)publishClientSessionShadow(nowMs, false);
  }
}

void expireSessionLeases(unsigned long nowMs) {
  bool changed = false;

  if (gSessionShadowState.lock.held && isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)) {
    clearLockLeaseInternal();
    changed = true;
  }

  if (hasActiveClientOwner(nowMs) && isExpired(nowMs, gSessionShadowState.activeOwnerExpiresAtMs)) {
    clearActiveOwnerLeaseInternal();
    clearLockLeaseInternal();
    gSessionShadowState.mode = ClientSessionMode::ClockFallback;
    gClientDisconnectedFallbackActive = true;
    changed = true;
  }

  if (changed) {
    markSessionMutation(nowMs);
  }
}

bool hasActiveClientOwner(unsigned long nowMs) {
  return gSessionShadowState.activeClientId.length() > 0
      && !isExpired(nowMs, gSessionShadowState.activeOwnerExpiresAtMs);
}

bool isActiveClientOwner(const String& clientId, uint32_t ownerEpoch, unsigned long nowMs) {
  return hasActiveClientOwner(nowMs)
      && gSessionShadowState.activeClientId.equals(clientId)
      && gSessionShadowState.activeOwnerEpoch == ownerEpoch;
}

uint32_t activeClientOwnerEpoch() {
  return gSessionShadowState.activeOwnerEpoch;
}

bool isClientLockHeldByAnotherClient(const String& clientId, unsigned long nowMs) {
  return gSessionShadowState.lock.held
      && !isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)
      && !gSessionShadowState.lock.clientId.equals(clientId);
}

bool isClientLockHeldByClient(const String& clientId, const String& lockToken, unsigned long nowMs) {
  return gSessionShadowState.lock.held
      && !isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)
      && gSessionShadowState.lock.clientId.equals(clientId)
      && gSessionShadowState.lock.token.equals(lockToken);
}

void adoptActiveClientOwner(const String& clientId, unsigned long nowMs) {
  gSessionShadowState.activeClientId = clientId;
  gSessionShadowState.activeOwnerEpoch++;
  if (gSessionShadowState.activeOwnerEpoch == 0) {
    gSessionShadowState.activeOwnerEpoch = 1;
  }

  gSessionShadowState.activeOwnerExpiresAtMs = nowMs + kSessionOwnerExpiryMs;
  gClientDisconnectedFallbackActive = false;
  markSessionMutation(nowMs);
}

void renewActiveClientOwner(unsigned long nowMs) {
  if (!hasActiveClientOwner(nowMs)) {
    return;
  }

  gSessionShadowState.activeOwnerExpiresAtMs = nowMs + kSessionOwnerExpiryMs;
  gClientDisconnectedFallbackActive = false;
  markSessionMutation(nowMs);
}

void setClientSessionMode(ClientSessionMode mode, unsigned long nowMs) {
  if (gSessionShadowState.mode == mode) {
    return;
  }

  gSessionShadowState.mode = mode;
  if (mode != ClientSessionMode::ClockFallback) {
    gClientDisconnectedFallbackActive = false;
  }
  markSessionMutation(nowMs);
}

void clearClientDisconnectedFallback(unsigned long nowMs) {
  if (!gClientDisconnectedFallbackActive) {
    return;
  }

  gClientDisconnectedFallbackActive = false;
  if (gSessionShadowState.mode == ClientSessionMode::ClockFallback) {
    gSessionShadowState.mode = ClientSessionMode::Idle;
  }
  markSessionMutation(nowMs);
}

bool tryAcquireClientLock(
    const String& clientId,
    const String& token,
    const String& reason,
    uint32_t requestedLeaseMs,
    unsigned long nowMs,
    String& failureCode,
    String& failureMessage) {
  failureCode = "";
  failureMessage = "";

  if (clientId.length() == 0) {
    failureCode = "missing_client_id";
    failureMessage = "clientId ausente para lock.";
    return false;
  }

  if (token.length() == 0) {
    failureCode = "lock_token_required";
    failureMessage = "lockToken obrigatorio para adquirir lock.";
    return false;
  }

  const uint32_t leaseMs = requestedLeaseMs == 0
      ? static_cast<uint32_t>(kSessionLockLeaseDefaultMs)
      : (requestedLeaseMs > kSessionLockLeaseMaxMs
            ? static_cast<uint32_t>(kSessionLockLeaseMaxMs)
            : requestedLeaseMs);

  if (gSessionShadowState.lock.held && !isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)) {
    if (!gSessionShadowState.lock.clientId.equals(clientId)) {
      failureCode = "locked_by_other_client";
      failureMessage = "Outro cliente detem o lock ativo.";
      return false;
    }

    if (!gSessionShadowState.lock.token.equals(token)) {
      failureCode = "lock_token_mismatch";
      failureMessage = "lockToken nao corresponde ao lock atual.";
      return false;
    }
  }

  gSessionShadowState.lock.held = true;
  gSessionShadowState.lock.clientId = clientId;
  gSessionShadowState.lock.token = token;
  gSessionShadowState.lock.reason = reason;
  gSessionShadowState.lock.acquiredAtMs = nowMs;
  gSessionShadowState.lock.lastHeartbeatAtMs = nowMs;
  gSessionShadowState.lock.expiresAtMs = nowMs + leaseMs;
  markSessionMutation(nowMs);
  return true;
}

bool tryReleaseClientLock(
    const String& clientId,
    const String& token,
    unsigned long nowMs,
    String& failureCode,
    String& failureMessage) {
  failureCode = "";
  failureMessage = "";

  if (!gSessionShadowState.lock.held || isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)) {
    clearLockLeaseInternal();
    failureCode = "lock_not_held";
    failureMessage = "Nenhum lock ativo para liberar.";
    return false;
  }

  if (!gSessionShadowState.lock.clientId.equals(clientId)) {
    failureCode = "locked_by_other_client";
    failureMessage = "Outro cliente detem o lock ativo.";
    return false;
  }

  if (!gSessionShadowState.lock.token.equals(token)) {
    failureCode = "lock_token_mismatch";
    failureMessage = "lockToken nao corresponde ao lock atual.";
    return false;
  }

  clearLockLeaseInternal();
  markSessionMutation(nowMs);
  return true;
}

bool tryRenewClientLock(const String& clientId, const String& token, unsigned long nowMs) {
  if (!isClientLockHeldByClient(clientId, token, nowMs)) {
    return false;
  }

  gSessionShadowState.lock.lastHeartbeatAtMs = nowMs;
  gSessionShadowState.lock.expiresAtMs = nowMs + kSessionLockLeaseDefaultMs;
  markSessionMutation(nowMs);
  return true;
}

bool publishClientSessionShadow(unsigned long nowMs, bool force) {
  if ((!force && !gSessionShadowState.dirty) || gDeviceId.isEmpty()) {
    return false;
  }

  if (!gMqtt.connected()) {
    return false;
  }

  JsonDocument shadow;
  shadow["deviceId"] = gDeviceId;
  shadow["shadowVersion"] = gSessionShadowState.shadowVersion;
  shadow["mode"] = clientSessionModeName(gSessionShadowState.mode);
  if (hasActiveClientOwner(nowMs)) {
    shadow["activeClientId"] = gSessionShadowState.activeClientId;
    shadow["activeOwnerEpoch"] = gSessionShadowState.activeOwnerEpoch;
    shadow["ownerLeaseRemainingMs"] = remainingLeaseMs(nowMs, gSessionShadowState.activeOwnerExpiresAtMs);
  }
  shadow["lockHeld"] = gSessionShadowState.lock.held && !isExpired(nowMs, gSessionShadowState.lock.expiresAtMs);
  if (gSessionShadowState.lock.held && !isExpired(nowMs, gSessionShadowState.lock.expiresAtMs)) {
    shadow["lockClientId"] = gSessionShadowState.lock.clientId;
    shadow["lockReason"] = gSessionShadowState.lock.reason;
    shadow["lockLeaseRemainingMs"] = remainingLeaseMs(nowMs, gSessionShadowState.lock.expiresAtMs);
  }
  if (gActiveAppId.length() > 0) {
    shadow["activeAppId"] = gActiveAppId;
  }
  shadow["fallbackState"] = clientSessionFallbackStateName();

  const bool published = publishMqttDocument(buildDeviceMqttTopic("shadow"), shadow, true);
  if (published) {
    gSessionShadowState.dirty = false;
  }

  return published;
}

bool shouldAcceptOwnedStreamFrame(uint32_t ownerEpoch, bool hasOwnerEpoch, unsigned long nowMs) {
  if (!hasActiveClientOwner(nowMs)) {
    return true;
  }

  return hasOwnerEpoch && ownerEpoch == gSessionShadowState.activeOwnerEpoch;
}

void noteAcceptedOwnedStreamFrame(uint32_t ownerEpoch, ClientSessionMode mode, unsigned long nowMs) {
  if (!hasActiveClientOwner(nowMs) || ownerEpoch != gSessionShadowState.activeOwnerEpoch) {
    return;
  }

  if (gClientDisconnectedFallbackActive) {
    clearClientDisconnectedFallback(nowMs);
  }

  if (gSessionShadowState.mode != mode) {
    setClientSessionMode(mode, nowMs);
  }
}
