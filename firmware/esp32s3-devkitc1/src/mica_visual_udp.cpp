// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---visual-udp-lan-opt-in
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/ws-protocol-v2.md#udp-visual-v1
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md

#include "mica_visual_udp.h"

#include <Arduino.h>
#include <WiFi.h>
#include <errno.h>
#include <fcntl.h>
#include <lwip/inet.h>
#include <lwip/sockets.h>
#include <mbedtls/md.h>
#include <string.h>
#include <unistd.h>

#include "mica_globals.h"
#include "mica_network.h"

namespace {
constexpr uint8_t kUdpMagic0 = 'M';
constexpr uint8_t kUdpMagic1 = 'I';
constexpr uint8_t kUdpMagic2 = 'C';
constexpr uint8_t kUdpMagic3 = 'A';
constexpr uint8_t kUdpVersion = 1;
constexpr uint8_t kUdpTagSize = 16;
constexpr uint8_t kMaxUdpPacketsPerPoll = 2;

uint16_t readLe16(const uint8_t* value) {
  return static_cast<uint16_t>(value[0]) |
      static_cast<uint16_t>(static_cast<uint16_t>(value[1]) << 8);
}

uint32_t readLe32(const uint8_t* value) {
  return static_cast<uint32_t>(value[0]) |
      (static_cast<uint32_t>(value[1]) << 8) |
      (static_cast<uint32_t>(value[2]) << 16) |
      (static_cast<uint32_t>(value[3]) << 24);
}

bool computeUdpTag(const uint8_t* data, size_t dataLength, uint8_t* tag) {
  const mbedtls_md_info_t* mdInfo = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);
  if (mdInfo == nullptr || gToken.isEmpty()) {
    return false;
  }

  uint8_t fullTag[32] = {};
  const int result = mbedtls_md_hmac(
      mdInfo,
      reinterpret_cast<const unsigned char*>(gToken.c_str()),
      static_cast<size_t>(gToken.length()),
      data,
      dataLength,
      fullTag);
  if (result != 0) {
    return false;
  }

  memcpy(tag, fullTag, kUdpTagSize);
  return true;
}

bool tagsMatchConstantTime(const uint8_t* left, const uint8_t* right) {
  uint8_t diff = 0;
  for (uint8_t index = 0; index < kUdpTagSize; index++) {
    diff |= static_cast<uint8_t>(left[index] ^ right[index]);
  }

  return diff == 0;
}

bool validateUdpDatagram(const uint8_t* datagram, size_t length, uint32_t& sequence, const uint8_t*& payload, size_t& payloadLength) {
  sequence = 0;
  payload = nullptr;
  payloadLength = 0;
  if (datagram == nullptr || length < (kVisualUdpFrameHeaderSize + kVisualUdpFrameTagSize)) {
    registerInvalidStreamFrame("udp_short");
    return false;
  }

  if (datagram[0] != kUdpMagic0
      || datagram[1] != kUdpMagic1
      || datagram[2] != kUdpMagic2
      || datagram[3] != kUdpMagic3
      || datagram[4] != kUdpVersion) {
    registerInvalidStreamFrame("udp_header_invalid");
    return false;
  }

  sequence = readLe32(datagram + 6);
  if ((gVisualUdpHasLastSequence && sequence <= gVisualUdpLastSequence)
      || (gHasStreamLastSequence && sequence <= gStreamLastSequence)) {
    registerInvalidStreamFrame("udp_sequence_old");
    return false;
  }

  payloadLength = readLe16(datagram + 10);
  const size_t expectedLength = kVisualUdpFrameHeaderSize + payloadLength + kVisualUdpFrameTagSize;
  const bool legacyBinsPayload = payloadLength == kStreamFrameSize;
  const bool ownedBinsPayload = payloadLength == kStreamFrameV3Size;
  if ((!legacyBinsPayload && !ownedBinsPayload) || expectedLength != length) {
    registerInvalidStreamFrame("udp_payload_length_invalid");
    return false;
  }

  payload = datagram + kVisualUdpFrameHeaderSize;
  const bool supportedVersion = payload[0] == kStreamVersion || payload[0] == kStreamVersionOwned;
  if (!supportedVersion || payload[1] != kStreamBinsMessageType) {
    registerInvalidStreamFrame("udp_payload_type_invalid");
    return false;
  }

  uint8_t expectedTag[kUdpTagSize] = {};
  if (!computeUdpTag(datagram, kVisualUdpFrameHeaderSize + payloadLength, expectedTag)) {
    registerInvalidStreamFrame("udp_hmac_compute_failed");
    return false;
  }

  const uint8_t* actualTag = datagram + kVisualUdpFrameHeaderSize + payloadLength;
  if (!tagsMatchConstantTime(expectedTag, actualTag)) {
    registerInvalidStreamFrame("udp_hmac_invalid");
    return false;
  }

  return true;
}
}  // namespace

void ensureVisualUdpReceiver() {
  if (gVisualUdpSocket >= 0) {
    return;
  }

  if (WiFi.status() != WL_CONNECTED || gDeviceId.isEmpty() || gToken.isEmpty()) {
    return;
  }

  const int sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_IP);
  if (sock < 0) {
    registerInvalidStreamFrame("udp_socket_failed");
    return;
  }

  int flags = fcntl(sock, F_GETFL, 0);
  if (flags >= 0) {
    (void)fcntl(sock, F_SETFL, flags | O_NONBLOCK);
  }

  sockaddr_in address = {};
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = htonl(INADDR_ANY);
  address.sin_port = htons(kVisualUdpPort);
  if (bind(sock, reinterpret_cast<sockaddr*>(&address), sizeof(address)) < 0) {
    close(sock);
    registerInvalidStreamFrame("udp_bind_failed");
    return;
  }

  gVisualUdpSocket = sock;
  Serial.printf("[udp_visual] listening udp://0.0.0.0:%u mode=bins128\n", kVisualUdpPort);
}

void stopVisualUdpReceiver() {
  if (gVisualUdpSocket < 0) {
    return;
  }

  close(gVisualUdpSocket);
  gVisualUdpSocket = -1;
  gVisualUdpHasLastSequence = false;
}

void processVisualUdpReceiver() {
  if (WiFi.status() != WL_CONNECTED || gDeviceId.isEmpty() || gToken.isEmpty()) {
    stopVisualUdpReceiver();
    return;
  }

  ensureVisualUdpReceiver();
  if (gVisualUdpSocket < 0) {
    return;
  }

  uint8_t datagram[kVisualUdpFrameMaxDatagramSize] = {};
  for (uint8_t packetIndex = 0; packetIndex < kMaxUdpPacketsPerPoll; packetIndex++) {
    sockaddr_in source = {};
    socklen_t sourceLength = sizeof(source);
    const ssize_t received = recvfrom(
        gVisualUdpSocket,
        datagram,
        sizeof(datagram),
        0,
        reinterpret_cast<sockaddr*>(&source),
        &sourceLength);

    if (received < 0) {
      if (errno != EAGAIN && errno != EWOULDBLOCK) {
        registerInvalidStreamFrame("udp_recv_failed");
      }

      return;
    }

    if (received == 0) {
      return;
    }

    uint32_t sequence = 0;
    const uint8_t* payload = nullptr;
    size_t payloadLength = 0;
    if (!validateUdpDatagram(datagram, static_cast<size_t>(received), sequence, payload, payloadLength)) {
      continue;
    }

    gVisualUdpFramesReceived++;
    gVisualUdpLastSequence = sequence;
    gVisualUdpHasLastSequence = true;
    gVisualUdpLastPacketMs = millis();
    if (applyStreamBinaryFrame(payload, payloadLength, true)) {
      gVisualUdpFramesApplied++;
    }
  }
}
