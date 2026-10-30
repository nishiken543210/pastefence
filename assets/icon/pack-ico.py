#!/usr/bin/env python3
"""PNG を 1 つの .ico にまとめる。Python の標準ライブラリだけで動く。

使い方:
    python3 assets/icon/pack-ico.py <PNG のあるフォルダー> <出力する .ico>

.ico の中身は寸法ごとに 2 通りある。
  ・16〜128 … BMP（32bit BGRA ＋ AND マスク）。古い API でも確実に読める。
  ・256     … PNG のまま。BMP にすると 256KB 超になり、無駄に太る。
"""
import struct
import sys
import zlib
from pathlib import Path

# 入れる寸法。256 だけ PNG 圧縮で格納する。
SIZES = [16, 24, 32, 48, 64, 128, 256]
PNG_COMPRESSED = {256}


def decode_png(data: bytes) -> tuple[int, int, bytes]:
    """PNG を (幅, 高さ, RGBA バイト列) にする。Chromium が吐く
    8bit / 非インターレースの PNG だけを相手にする。"""
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("PNG ではない")

    pos, idat, width, height, color_type = 8, bytearray(), 0, 0, 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length  # 長さ(4) + 種類(4) + 本体 + CRC(4)
        if kind == b"IHDR":
            width, height, depth, color_type, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or interlace != 0 or color_type not in (2, 6):
                raise ValueError(f"想定外の PNG: depth={depth} color={color_type} interlace={interlace}")
        elif kind == b"IDAT":
            idat += body
        elif kind == b"IEND":
            break

    channels = 4 if color_type == 6 else 3
    raw = zlib.decompress(bytes(idat))
    stride = width * channels

    # フィルターを解く（PNG 仕様の 0〜4）。
    out = bytearray(height * stride)
    prev = bytearray(stride)
    src = 0
    for y in range(height):
        f = raw[src]
        src += 1
        line = bytearray(raw[src:src + stride])
        src += stride
        if f == 1:    # Sub
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif f == 2:  # Up
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif f == 3:  # Average
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif f == 4:  # Paeth
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = prev[i]
                c = prev[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        elif f != 0:
            raise ValueError(f"未知のフィルター {f}")
        out[y * stride:(y + 1) * stride] = line
        prev = line

    if channels == 3:  # 不透明 PNG はアルファを足す
        rgba = bytearray(width * height * 4)
        for i in range(width * height):
            rgba[i * 4:i * 4 + 3] = out[i * 3:i * 3 + 3]
            rgba[i * 4 + 3] = 0xFF
        out = rgba
    return width, height, bytes(out)


def to_dib(width: int, height: int, rgba: bytes) -> bytes:
    """BITMAPINFOHEADER ＋ BGRA（下から上）＋ AND マスク。"""
    header = struct.pack(
        "<IiiHHIIiiII",
        40,             # biSize
        width,
        height * 2,     # biHeight は XOR と AND の合計を書く決まり
        1,              # biPlanes
        32,             # biBitCount
        0,              # biCompression = BI_RGB
        0, 0, 0, 0, 0,  # biSizeImage 以降は 0 で良い
    )

    xor = bytearray()
    for y in range(height - 1, -1, -1):  # BMP は下の行から並べる
        row = rgba[y * width * 4:(y + 1) * width * 4]
        for x in range(width):
            r, g, b, a = row[x * 4:x * 4 + 4]
            xor += bytes((b, g, r, a))

    # AND マスク: 完全な透明は 1、それ以外は 0。1 行は 4 バイト単位に揃える。
    mask_stride = ((width + 31) // 32) * 4
    mask = bytearray()
    for y in range(height - 1, -1, -1):
        row = bytearray(mask_stride)
        for x in range(width):
            if rgba[(y * width + x) * 4 + 3] == 0:
                row[x >> 3] |= 0x80 >> (x & 7)
        mask += row

    return header + bytes(xor) + bytes(mask)


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    png_dir, ico_path = Path(sys.argv[1]), Path(sys.argv[2])

    images = []
    for size in SIZES:
        data = (png_dir / f"icon-{size}.png").read_bytes()
        width, height, rgba = decode_png(data)
        if (width, height) != (size, size):
            raise SystemExit(f"icon-{size}.png の寸法が {width}x{height} になっている")
        images.append((size, data if size in PNG_COMPRESSED else to_dib(width, height, rgba)))

    offset = 6 + 16 * len(images)
    directory, blobs = bytearray(), bytearray()
    for size, blob in images:
        directory += struct.pack(
            "<BBBBHHII",
            size if size < 256 else 0,  # 256 は 0 と書く決まり
            size if size < 256 else 0,
            0,      # 色数（32bit なので 0）
            0,      # 予約
            1,      # プレーン数
            32,     # ビット数
            len(blob),
            offset,
        )
        blobs += blob
        offset += len(blob)

    ico_path.parent.mkdir(parents=True, exist_ok=True)
    ico_path.write_bytes(struct.pack("<HHH", 0, 1, len(images)) + bytes(directory) + bytes(blobs))
    print(f"{ico_path}  {ico_path.stat().st_size:,} バイト  / {len(images)} 枚")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
