#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#shadow-retained-de-sessao
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md

#include <Arduino.h>
#include <ArduinoJson.h>

#include "mica_types.h"

const char* clientSessionModeName(ClientSessionMode mode);
const char* clientSessionFallbackStateName();
bool isSessionAwareEnvelope(const ControlCommandEnvelope& envelope);
bool isSessionCommandName(const char* command);
bool doesCommandRequireSessionLock(const char* command);
ClientSessionMode resolveSessionModeHint(const char* command, JsonObjectConst parameters);

void initializeClientSessionRuntime();
void processClientSessionRuntime(unsigned long nowMs);
void expireSessionLeases(unsigned long nowMs);

bool hasActiveClientOwner(unsigned long nowMs);
bool isActiveClientOwner(const String& clientId, uint32_t ownerEpoch, unsigned long nowMs);
uint32_t activeClientOwnerEpoch();

bool isClientLockHeldByAnotherClient(const String& clientId, unsigned long nowMs);
bool isClientLockHeldByClient(const String& clientId, const String& lockToken, unsigned long nowMs);

void adoptActiveClientOwner(const String& clientId, unsigned long nowMs);
void renewActiveClientOwner(unsigned long nowMs);
void setClientSessionMode(ClientSessionMode mode, unsigned long nowMs);
void clearClientDisconnectedFallback(unsigned long nowMs);

bool tryAcquireClientLock(
    const String& clientId,
    const String& token,
    const String& reason,
    uint32_t requestedLeaseMs,
    unsigned long nowMs,
    String& failureCode,
    String& failureMessage);
bool tryReleaseClientLock(
    const String& clientId,
    const String& token,
    unsigned long nowMs,
    String& failureCode,
    String& failureMessage);
bool tryRenewClientLock(const String& clientId, const String& token, unsigned long nowMs);

bool publishClientSessionShadow(unsigned long nowMs, bool force);
bool shouldAcceptOwnedStreamFrame(uint32_t ownerEpoch, bool hasOwnerEpoch, unsigned long nowMs);
void noteAcceptedOwnedStreamFrame(uint32_t ownerEpoch, ClientSessionMode mode, unsigned long nowMs);
