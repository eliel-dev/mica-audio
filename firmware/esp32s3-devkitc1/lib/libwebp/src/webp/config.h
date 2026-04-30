#ifndef WEBP_WEBP_CONFIG_H_
#define WEBP_WEBP_CONFIG_H_

// Minimal libwebp config for the ESP32-S3 / Xtensa Arduino build used by Mica.
// This library is vendored for decode/demux only; optional desktop codecs and
// SIMD backends stay disabled unless the toolchain enables them directly.

#define HAVE_BUILTIN_BSWAP16 1
#define HAVE_BUILTIN_BSWAP32 1
#define HAVE_BUILTIN_BSWAP64 1

#define HAVE_UNISTD_H 1

#endif  // WEBP_WEBP_CONFIG_H_
