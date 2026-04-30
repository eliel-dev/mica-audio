Import("env")

from pathlib import Path
import re


# DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---buffer-ws-para-frame-128x64

PATCH_MARKER = "MICA_WEBSOCKETS_MAX_DATA_SIZE_PATCH"
TARGET_HEADER = Path(env.subst("$PROJECT_LIBDEPS_DIR")) / env.subst("$PIOENV") / "WebSockets" / "src" / "WebSockets.h"
DEFINE_PATTERN = re.compile(r"^#define WEBSOCKETS_MAX_DATA_SIZE (.+)$", re.MULTILINE)


def patch_websockets_header(*_args, **_kwargs):
    if not TARGET_HEADER.exists():
        raise RuntimeError(f"WebSockets header nao encontrado para patch: {TARGET_HEADER}")

    original = TARGET_HEADER.read_text(encoding="utf-8")
    if PATCH_MARKER in original:
        print(f"[mica][ws-patch] header ja ajustado: {TARGET_HEADER}")
        return

    newline = "\r\n" if "\r\n" in original else "\n"

    def replace(match):
        expression = match.group(1)
        return (
            "#ifndef WEBSOCKETS_MAX_DATA_SIZE"
            + newline
            + f"#define WEBSOCKETS_MAX_DATA_SIZE {expression}"
            + newline
            + f"#endif // {PATCH_MARKER}"
        )

    patched, count = DEFINE_PATTERN.subn(replace, original)
    if count == 0:
        raise RuntimeError("Assinatura esperada de WEBSOCKETS_MAX_DATA_SIZE nao encontrada em WebSockets.h.")

    with TARGET_HEADER.open("w", encoding="utf-8", newline="") as stream:
        stream.write(patched)
    print(f"[mica][ws-patch] header ajustado ({count} ocorrencias): {TARGET_HEADER}")

patch_websockets_header()
