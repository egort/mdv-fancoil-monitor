#!/usr/bin/env python3
"""Dump all response bytes with indices for protocol analysis."""

import serial
import time

PORT = 'COM10'
BAUD = 4800
ADDR = 0x30

# Build query command
cmd = bytes([0xFE, 0xAA, 0xC0, ADDR, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F])
crc = 255 - (sum(cmd[1:15]) + 85) % 256
cmd = cmd + bytes([crc, 0x55])

ser = serial.Serial(PORT, BAUD, timeout=0.5)
ser.write(cmd)
time.sleep(0.3)
r = ser.read(32)
ser.close()

print(f"TX: {cmd.hex(' ').upper()}")
print(f"RX: {r.hex(' ').upper()}")
print()
print("Byte analysis:")
print("=" * 50)
print(f"{'Idx':>3} | {'Hex':>4} | {'Dec':>3} | XYE interpretation")
print("-" * 50)

xye_names = {
    0: "Preamble (0xFE - extra)",
    1: "Preamble (0xAA)",
    2: "Response code (C0=Query)",
    3: "To master (0x80)",
    4: "Destination",
    5: "Source/Device ID",
    6: "?",
    7: "Capabilities 1?",
    8: "Capabilities 2?",
    9: "MODE (0=OFF, 81=FAN, 82=DRY, 84=HEAT, 88=COOL)",
    10: "FAN (01=Hi, 02=Med, 03/04=Lo, 80=Auto)",
    11: "Set Temp (°C)",
    12: "T1 Temp (raw: val*0.5-48)",
    13: "T2A Temp",
    14: "T2B Temp (FF=invalid)",
    15: "T3 Temp (FF=invalid)",
    16: "Current (FF=invalid)",
    17: "Timer Start",
    18: "Timer Stop",
    19: "?",
    20: "Mode Flags (01=ECO,02=Turbo,04=SWING,88=VENT)",
    21: "Oper Flags (04=pump,80=locked)",
    22: "Error byte 1",
    23: "Error byte 2",
    24: "Protect byte 1",
    25: "Protect byte 2",
    26: "CCM Comm Error",
    27: "?",
    28: "?",
    29: "?",
    30: "?",
    31: "CRC",
}

for i, b in enumerate(r):
    name = xye_names.get(i, "?")
    print(f"{i:3d} | 0x{b:02X} | {b:3d} | {name}")

# Decode temperatures
print()
print("Temperature decoding (if T bytes are valid):")
for idx in [12, 13]:
    val = r[idx] if len(r) > idx else 0
    if val != 0xFF:
        temp = val * 0.5 - 48
        print(f"  Byte {idx}: {val} -> {temp:.1f}°C")
