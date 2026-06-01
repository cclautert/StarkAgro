#!/usr/bin/env node
// Generates favicon.ico (16x16 + 32x32 PNG-in-ICO) for AgripeWeb.
// Brand: water drop, gradient from #38bdf8 (top) to #2563eb (bottom).
// No external deps — uses only Node.js built-ins (zlib + Buffer).

const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

// ── PNG encoder (minimal, raw RGBA) ─────────────────────────────────────────
function encodePng(width, height, rgbaPixels) {
  function crc32(buf) {
    let c = 0xffffffff;
    for (const b of buf) {
      c ^= b;
      for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
    }
    return (c ^ 0xffffffff) >>> 0;
  }
  function chunk(type, data) {
    const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
    const t = Buffer.from(type);
    const crcBuf = Buffer.concat([t, data]);
    const crc = Buffer.alloc(4); crc.writeInt32BE(crc32(crcBuf) | 0);
    return Buffer.concat([len, t, data, crc]);
  }
  // Build raw filtered scanlines (filter type 0 = None per row)
  const scanlines = Buffer.alloc((1 + width * 4) * height);
  for (let y = 0; y < height; y++) {
    scanlines[y * (1 + width * 4)] = 0; // filter
    for (let x = 0; x < width; x++) {
      const src = (y * width + x) * 4;
      const dst = y * (1 + width * 4) + 1 + x * 4;
      scanlines[dst]     = rgbaPixels[src];
      scanlines[dst + 1] = rgbaPixels[src + 1];
      scanlines[dst + 2] = rgbaPixels[src + 2];
      scanlines[dst + 3] = rgbaPixels[src + 3];
    }
  }
  const compressed = zlib.deflateSync(scanlines, { level: 9 });
  const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; ihdr[9] = 6; // 8-bit, RGBA
  return Buffer.concat([sig, chunk('IHDR', ihdr), chunk('IDAT', compressed), chunk('IEND', Buffer.alloc(0))]);
}

// ── Draw water drop ──────────────────────────────────────────────────────────
function drawDrop(size) {
  const px = new Uint8Array(size * size * 4); // RGBA, default transparent

  // The drop occupies roughly [0.2..0.8] horizontally, [0.05..0.95] vertically.
  // Shape: teardrop — circle bottom + pointed tip top.
  const cx = size / 2;
  const cr = size * 0.32;          // circle radius (bottom round part)
  const cy = size * 0.62;          // circle center Y
  const tipX = cx;
  const tipY = size * 0.06;        // top tip Y

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const inCircle = (x - cx) ** 2 + (y - cy) ** 2 <= cr ** 2;

      // Triangle region above circle: check if inside triangle tip→leftTangent→rightTangent
      const leftTangentX  = cx - cr * 0.92;
      const rightTangentX = cx + cr * 0.92;
      const tangentY      = cy - cr * 0.38;
      let inTri = false;
      if (y >= tipY && y <= tangentY) {
        const t = (y - tipY) / (tangentY - tipY);
        const xLeft  = tipX + t * (leftTangentX  - tipX);
        const xRight = tipX + t * (rightTangentX - tipX);
        inTri = x >= xLeft && x <= xRight;
      }

      if (!inCircle && !inTri) continue;

      // Gradient: t = 0 (tip) → 1 (bottom of circle)
      const t = Math.min(1, Math.max(0, (y - tipY) / (size * 0.89)));
      // #38bdf8 → #2563eb
      const r = Math.round(0x38 + t * (0x25 - 0x38));
      const g = Math.round(0xbd + t * (0x63 - 0xbd));
      const b = Math.round(0xf8 + t * (0xeb - 0xf8));

      // Anti-alias: soften edge pixels
      let alpha = 255;
      const dCircle = Math.sqrt((x - cx) ** 2 + (y - cy) ** 2) - cr;
      if (dCircle > -1 && dCircle < 0) alpha = Math.round(255 * (-dCircle));

      const idx = (y * size + x) * 4;
      px[idx]     = r;
      px[idx + 1] = g;
      px[idx + 2] = b;
      px[idx + 3] = alpha;

      // Subtle highlight: upper-left quarter of drop
      if (y < cy && x < cx) {
        const hl = 0.18;
        px[idx]     = Math.min(255, px[idx]     + Math.round(hl * (255 - px[idx])));
        px[idx + 1] = Math.min(255, px[idx + 1] + Math.round(hl * (255 - px[idx + 1])));
        px[idx + 2] = Math.min(255, px[idx + 2] + Math.round(hl * (255 - px[idx + 2])));
      }
    }
  }
  return Buffer.from(px);
}

// ── Assemble ICO (PNG-in-ICO, multi-size) ────────────────────────────────────
const sizes = [16, 32, 48];
const pngs  = sizes.map(s => encodePng(s, s, drawDrop(s)));

// ICO header: 6 bytes  + ICONDIRENTRY × n (16 bytes each)
const headerSize = 6 + sizes.length * 16;
let offset = headerSize;

const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0); // reserved
header.writeUInt16LE(1, 2); // type: ICO
header.writeUInt16LE(sizes.length, 4);

const entries = sizes.map((s, i) => {
  const e = Buffer.alloc(16);
  e[0] = s === 256 ? 0 : s; // width
  e[1] = s === 256 ? 0 : s; // height
  e[2] = 0; // color count
  e[3] = 0; // reserved
  e.writeUInt16LE(1, 4);           // planes
  e.writeUInt16LE(32, 6);          // bit count
  e.writeUInt32LE(pngs[i].length, 8);
  e.writeUInt32LE(offset, 12);
  offset += pngs[i].length;
  return e;
});

const ico = Buffer.concat([header, ...entries, ...pngs]);

const dest = path.join(__dirname, '../AgripeWebUI/public/favicon.ico');
fs.writeFileSync(dest, ico);
console.log(`Written ${ico.length} bytes → ${dest}`);
console.log(`Sizes: ${sizes.map((s, i) => `${s}x${s} (${pngs[i].length}B)`).join(', ')}`);
