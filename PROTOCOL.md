# MDV Fancoil RS485 Protocol

## Physical Layer

- **Baud rate:** 4800
- **Data bits:** 8
- **Parity:** None
- **Stop bits:** 1

## Message Format

All messages start with `FE AA` and end with `55`.

### GET Command (Read Status)

Request (17 bytes):
```
FE AA C0 [ADDR] 00 80 00 00 00 00 00 00 00 00 3F [CRC] 55
```

Response (32 bytes):
```
FE AA C0 80 00 [ADDR] 00 [B7] [B8] [MODE] [SPEED] [TEMP] [B12] [B13] FF FF FF 00 00 00 [B20] 00 [B22] 00 00 00 00 00 00 FF FF [CRC]
```

### SET Command (Control)

Request (17 bytes):
```
FE AA C3 [ADDR] 00 80 00 [MODE] [SPEED] [TEMP] 00 00 00 00 3C [CRC] 55
```

Response (32 bytes): Same format as GET response

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
- Example: `0x12` = 18°C, `0x18` = 24°C

### SWING (bytes 21-22 in response)

| B21 | B22 | Meaning |
|-----|-----|---------|
| 0   | 0   | OFF     |
| 4   | 1   | ON      |

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

- **B7 (0xE0):** Unknown, constant?
- **B8 (0x14):** Room temperature? (0x14 = 20)
- **B12-B13 (0x50 0x4E):** Unknown
- **B20:** Changes with power state (0x08 when ON, 0x00 when OFF)

## Equipment Tested

- MDV MKG-300C (2024)
- Address: 48 (0x30)
