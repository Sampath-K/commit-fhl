#!/usr/bin/env node
/**
 * generate-icons.js — Creates placeholder PNG icons for Teams app manifest.
 * Run: node appPackage/generate-icons.js
 *
 * Creates:
 *   color.png   — 192×192 solid #0078D4 (Teams blue)
 *   outline.png — 32×32 solid white with blue border
 *
 * Uses only Node.js built-ins (no external deps).
 */
'use strict';
const fs = require('fs');
const path = require('path');

// Minimal PNG writer — enough for solid color images
function writePng(filename, width, height, r, g, b) {
  const zlib = require('zlib');

  function crc32(buf) {
    const table = new Uint32Array(256);
    for (let i = 0; i < 256; i++) {
      let c = i;
      for (let j = 0; j < 8; j++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
      table[i] = c;
    }
    let crc = 0xffffffff;
    for (let i = 0; i < buf.length; i++) crc = table[(crc ^ buf[i]) & 0xff] ^ (crc >>> 8);
    return (crc ^ 0xffffffff) >>> 0;
  }

  function chunk(type, data) {
    const lenBuf = Buffer.alloc(4);
    lenBuf.writeUInt32BE(data.length, 0);
    const typeBuf = Buffer.from(type, 'ascii');
    const combined = Buffer.concat([typeBuf, data]);
    const crcBuf = Buffer.alloc(4);
    crcBuf.writeUInt32BE(crc32(combined), 0);
    return Buffer.concat([lenBuf, typeBuf, data, crcBuf]);
  }

  // IHDR
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;  // bit depth
  ihdr[9] = 2;  // color type: RGB
  ihdr[10] = 0; // compression
  ihdr[11] = 0; // filter
  ihdr[12] = 0; // interlace

  // Raw image data (filter byte 0 per row + RGB pixels)
  const rowSize = 1 + width * 3;
  const raw = Buffer.alloc(height * rowSize);
  for (let y = 0; y < height; y++) {
    const offset = y * rowSize;
    raw[offset] = 0; // filter type: None
    for (let x = 0; x < width; x++) {
      raw[offset + 1 + x * 3 + 0] = r;
      raw[offset + 1 + x * 3 + 1] = g;
      raw[offset + 1 + x * 3 + 2] = b;
    }
  }

  const compressed = zlib.deflateSync(raw);

  const png = Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), // PNG signature
    chunk('IHDR', ihdr),
    chunk('IDAT', compressed),
    chunk('IEND', Buffer.alloc(0)),
  ]);

  fs.writeFileSync(path.join(__dirname, filename), png);
  console.log(`Created ${filename} (${width}×${height})`);
}

// color.png — 192×192 Teams blue (#0078D4 = rgb(0, 120, 212))
writePng('color.png', 192, 192, 0, 120, 212);

// outline.png — 32×32 white (#FFFFFF = rgb(255, 255, 255))
writePng('outline.png', 32, 32, 255, 255, 255);

console.log('Done. Run: cd appPackage && zip commit-fhl.zip manifest.json color.png outline.png');
