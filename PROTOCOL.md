# MDV Fancoil RS485 Protocol

This is the **XYE protocol** used by Midea and clones (MDV, FrigoLine, Mundo Clima, Daikin, etc.) for indoor AC/fancoil units on VRF systems.

**Reference:** https://codeberg.org/xye/xye

## Physical Layer

- **Bus:** RS-485 (X=A, Y=B, E=GND)
- **Baud rate:** 4800
- **Data bits:** 8
- **Parity:** None
- **Stop bits:** 1

The CCM (Central Controller) polls all 64 possible unit IDs. Each unit gets a 130ms time slice (30ms query + 100ms timeout).

## Message Format

All messages start with `FE AA` (or just `AA` in some implementations) and end with `55`.

### GET Command (Read Status) - 0xC0

Request (17 bytes):
```
FE AA C0 [ADDR] 00 80 00 00 00 00 00 00 00 00 3F [CRC] 55
```

Response (32 bytes):
```
FE AA C0 80 00 [ADDR] 00 [B7] [B8] [MODE] [SPEED] [TEMP] [T1] [T2A] [T2B] [T3] [CURRENT] [TIMER_START] [TIMER_STOP] [B19] [MODE_FLAGS] [OPER_FLAGS] [ERR1] [ERR2] [PROT1] [PROT2] [CCM_ERR] 00 00 FF FF [CRC]
```

### SET Command (Control) - 0xC3

Request (17 bytes):
```
FE AA C3 [ADDR] 00 80 00 [MODE] [SPEED] [TEMP] [MODE_FLAGS] [TIMER_START] [TIMER_STOP] 00 3C [CRC] 55
```

Response (32 bytes): Same format as GET response

### LOCK Command - 0xCC

Locks the unit (disables local control).

### UNLOCK Command - 0xCD

Unlocks the unit.

## CRC Calculation

```
CRC = 255 - (sum of bytes[1..14] + 85) % 256
```

Python:
```python
def calc_crc(data):
    return 255 - (sum(data[1:15]) + 85) % 256
```

## Byte Definitions

### Address (ADDR)
- Device address on RS485 bus
- Example: `0x30` = address 48

### MODE Byte (byte 9 in response, byte 7 in SET command)

| Value | Binary     | Meaning |
|-------|------------|---------|
| 0x00  | 0000 0000  | OFF     |
| 0x81  | 1000 0001  | FAN ON  |
| 0x82  | 1000 0010  | DRY ON  |
| 0x84  | 1000 0100  | HEAT ON |
| 0x88  | 1000 1000  | COOL ON |
| 0x90  | 1001 0000  | AUTO ON |

Bit 7 (0x80) = Power ON flag
Bits 0-4 = Mode selection:
- 0x01 = FAN
- 0x02 = DRY
- 0x04 = HEAT
- 0x08 = COOL
- 0x10 = AUTO

### SPEED Byte (byte 10 in response, byte 8 in SET command)

| Value | Meaning |
|-------|---------|
| 0x01  | HIGH    |
| 0x02  | MEDIUM  |
| 0x04  | LOW     |
| 0x80  | AUTO (?) |

### TEMP Byte (byte 11 in response, byte 9 in SET command)

Temperature setpoint in °C (decimal value).
- Example: `0x12` = 18°C, `0x16` = 22°C

### Room Temperature (byte 8 in response)

**MDV MKG-300C:** Direct reading in °C.
- Example: `0x14` = 20°C

Note: XYE docs say this is "capabilities", but on MKG-300C it's room temperature.

### Temperature Sensors (bytes 12-15 in response)

XYE docs say temperature values are encoded as: `value * 0.5 - 48` °C

| Byte | Name | XYE Description | MKG-300C Observed |
|------|------|-----------------|-------------------|
| 12 | T1 | Indoor coil temp | 0x50 (80) - static, possibly `80-60=20°C`? |
| 13 | T2A | ? | 0x4E (78) - static |
| 14 | T2B | ? | 0xFF (invalid) |
| 15 | T3 | ? | 0xFF (invalid) |

**Note:** On MKG-300C bytes 12-13 don't change with temperature setpoint. Need more testing to understand encoding.

### Current (byte 16 in response)

Unit current consumption: 0-99 Amps

### Timer (bytes 17-18 in response, 11-12 in SET)

Timer values are bit-encoded:
- 0x01 = 15 min
- 0x02 = 30 min
- 0x04 = 1 hour
- 0x08 = 2 hours
- 0x10 = 4 hours
- 0x20 = 8 hours
- 0x40 = 16 hours
- 0x80 = invalid/disabled

### MODE_FLAGS Byte (byte 20 in response, byte 10 in SET)

| Bit | Value | Meaning |
|-----|-------|---------|
| 0 | 0x01 | ECO Mode (Sleep) |
| 1 | 0x02 | Aux Heat (Turbo) |
| 2 | 0x04 | SWING |
| 3 | 0x08 | ? |
| 7 | 0x80 | VENT |

Combined: 0x88 = VENT mode

### OPER_FLAGS Byte (byte 21 in response)

| Bit | Value | Meaning |
|-----|-------|---------|
| 2 | 0x04 | Water pump running |
| 7 | 0x80 | Locked |

### Error Bytes (bytes 22-23 in response)

Error code = E + bit position (0-15)

### Protection Bytes (bytes 24-25 in response)

Protection code = P + bit position (0-15)

### CCM Communication Error (byte 26 in response)

Values: 0x00 - 0x02

### SWING (bytes 20, 21 in response - via MODE_FLAGS)

| MODE_FLAGS bit 2 | OPER_FLAGS | Meaning |
|------------------|------------|---------|
| 0 | 0 | OFF |
| 0x04 | ? | ON |

## Example Commands

### Read status from address 48:
```
TX: FE AA C0 30 00 80 00 00 00 00 00 00 00 00 3F 81 55
RX: FE AA C0 80 00 30 00 E0 14 88 04 12 50 4E FF FF FF 00 00 00 08 00 04 00 00 00 00 00 00 FF FF 59
```

### Turn ON (HEAT, LOW speed, 18°C):
```
TX: FE AA C3 30 00 80 00 84 04 12 00 00 00 00 3C B7 55
```

### Turn OFF:
```
TX: FE AA C3 30 00 80 00 00 04 12 00 00 00 00 3C 3B 55
```

### Set temperature to 18°C (COOL mode):
```
TX: FE AA C3 30 00 80 00 88 04 12 00 00 00 00 3C B3 55
```

## Unknown Bytes (to be researched)

- **B7 (0xE0):** Capabilities? (XYE: 0x80 = extended temp 16-32°C, 0x10 = has SWING)

## MKG-300C Response Byte Map

Verified mapping for MDV MKG-300C (2024):

| Byte | Hex | Description | Verified |
|------|-----|-------------|----------|
| 0 | FE | Preamble (extra) | ✅ |
| 1 | AA | Preamble | ✅ |
| 2 | C0 | Response code | ✅ |
| 3 | 80 | To master | ✅ |
| 4 | 00 | Destination | ✅ |
| 5 | 30 | Device address (48) | ✅ |
| 6 | 00 | ? | |
| 7 | E0 | Capabilities? | |
| 8 | 14 | **Room Temp (20°C)** | ✅ |
| 9 | xx | **MODE** | ✅ |
| 10 | xx | **SPEED** | ✅ |
| 11 | xx | **Set Temp (°C)** | ✅ |
| 12 | 50 | T1? (static 80) | ? |
| 13 | 4E | T2A? (static 78) | ? |
| 14 | FF | T2B (invalid) | |
| 15 | FF | T3 (invalid) | |
| 16 | FF | Current (invalid) | |
| 17 | 00 | Timer Start | |
| 18 | 00 | Timer Stop | |
| 19 | 00 | ? | |
| 20 | xx | Mode Flags (SWING) | ✅ |
| 21 | xx | Oper Flags | ✅ |
| 22-25 | 00 | Error/Protect | |
| 26 | 00 | CCM Error | |
| 27-28 | 00 | ? | |
| 29-30 | FF | ? | |
| 31 | xx | CRC | ✅ |

## Equipment Tested

- MDV MKG-300C (2024)
- Address: 48 (0x30)

## Related Projects

- **XYE Protocol Docs:** https://codeberg.org/xye/xye (primary reverse engineering source)
- **ESP32 Midea RS485:** https://github.com/Flachzange/ESP32_Midea_RS485
- **Bunicutz ESP32 Midea:** https://github.com/Bunicutz/ESP32_Midea_RS485

## Differences from XYE Documentation

Observations on MDV MKG-300C (2024) vs XYE docs:

| Parameter | XYE Docs | MKG-300C | Notes |
|-----------|----------|----------|-------|
| Preamble | `AA` | `FE AA` | Extra 0xFE byte |
| Fan LOW | 0x03 | 0x04 | Different encoding |
| Fan AUTO | 0x80 | 0x80 | Same ✅ |
| Byte 8 | Capabilities | Room Temp | Direct °C reading |
| T1-T3 encoding | `val×0.5-48` | Unknown | Bytes 12-13 static on MKG-300C |

## Protocol Variants

The XYE protocol is used by many brands:
- Midea
- MDV (Midea subsidiary)
- FrigoLine
- Mundo Clima
- Daikin (some models)
- And other OEM/rebrand versions
