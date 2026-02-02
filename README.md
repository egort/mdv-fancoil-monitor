# MDV Fancoil Monitor

Utility for monitoring and reverse-engineering MDV fancoil RS485 protocol.

## Protocol

### Serial Port Settings
- Baud rate: 4800
- Data bits: 8
- Parity: None
- Stop bits: 1

### Request Format (17 bytes)
```
FE AA C0 [ADDR] 00 80 00 00 00 00 00 00 00 00 3F [CRC] 55
```
- `FE AA` — header
- `C0` — read command
- `[ADDR]` — device address (0-63)
- `[CRC]` — checksum (129 - ADDR)
- `55` — end of packet

### Response Format (32 bytes)
```
FE AA C0 80 00 [ADDR] 00 XX XX [MODE] [SPEED] [SETTEMP] ... [SWING_H] [SWING_L] ... [CRC]
```

### Response Bytes Decoding

| Byte | Parameter | Values |
|------|-----------|--------|
| 9 | Mode | 0=OFF, 129=FAN, 130=DRY, 132=HEAT, 136=COOL, 145=AUTO |
| 10 | Fan Speed | 1=HIGH, 2=MED, 4=LOW |
| 11 | Set Temp | temperature °C |
| 21-22 | Swing | 4,1=ON / 0,0=OFF |

## Installation

```bash
pip install pyserial
```

## Usage

### Auto-scan (device discovery)
```bash
python scan_auto.py
```

### Real-time Monitoring
```bash
python monitor.py
```
Press **Q**, **ESC** or **Ctrl+C** to exit.

### Interactive Scanner
```bash
python mdv_scanner.py
```

## Configuration

Change `COM10` to your port in files:
- `monitor.py` — line with `serial.Serial(port='COM10', ...)`
- `scan_auto.py` — same

## License

MIT
