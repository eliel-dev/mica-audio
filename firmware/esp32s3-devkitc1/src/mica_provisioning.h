#pragma once

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/guides/setup-new-device.md#passos

void processSerialProvisioning();
void sendSerialHello();
bool startProvisioningPortal(const char* reason);
void enterProvisioningMode(bool clearDeviceCredentials, const char* reason);
