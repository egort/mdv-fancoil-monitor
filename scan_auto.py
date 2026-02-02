import serial
import serial.tools.list_ports
import time

def create_request(addr):
    """Create request for fancoil address"""
    crc = 129 - addr
    return bytes([0xFE, 0xAA, 0xC0, addr, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, crc, 0x55])

def parse_response(data):
    """Parse fancoil response"""
    if len(data) < 10:
        return None
    
    if data[0] != 0xFE or data[1] != 0xAA:
        return None
    
    result = {
        'raw': data.hex(' ').upper(),
        'addr': data[3],
        'power': (data[7] & 0x80) >> 7,
        'mode_raw': data[7] & 0x1F,
        'speed_raw': data[8],
        'set_temp': data[9],
        'room_temp': data[10] if len(data) > 10 else 0,
    }
    
    modes = {0x08: 'Cool', 0x04: 'Heat', 0x02: 'Dry', 0x01: 'Fan', 0x10: 'Auto'}
    result['mode'] = modes.get(result['mode_raw'], f'Unknown({result["mode_raw"]:02X})')
    
    speeds = {0x04: 'Low', 0x02: 'Med', 0x01: 'High', 0x80: 'Auto'}
    result['speed'] = speeds.get(result['speed_raw'], f'Unknown({result["speed_raw"]:02X})')
    
    return result

print("=" * 60)
print("  MDV Fancoil Scanner - Auto-scan")
print("=" * 60)

# Find COM ports
ports = list(serial.tools.list_ports.comports())
print(f"\nPorts found: {len(ports)}")
for p in ports:
    print(f"  - {p.device}: {p.description}")

if not ports:
    print("No COM ports found!")
    exit(1)

# Use first port
port_name = ports[0].device
print(f"\nUsing port: {port_name}")

try:
    ser = serial.Serial(
        port=port_name,
        baudrate=4800,
        bytesize=8,
        parity='N',
        stopbits=1,
        timeout=0.5
    )
    print("Connected!\n")
except Exception as e:
    print(f"Error: {e}")
    exit(1)

print("=" * 60)
print("  Scanning addresses 45-55")
print("=" * 60)

found = []
for addr in range(45, 56):
    request = create_request(addr)
    
    ser.reset_input_buffer()
    ser.write(request)
    time.sleep(0.15)
    
    response = ser.read(32)
    
    if response and len(response) >= 4:
        print(f"\nAddress {addr}: RESPONSE!")
        print(f"  RAW: {response.hex(' ').upper()}")
        parsed = parse_response(response)
        if parsed:
            print(f"  Power:    {'ON' if parsed['power'] else 'OFF'}")
            print(f"  Mode:     {parsed['mode']}")
            print(f"  Speed:    {parsed['speed']}")
            print(f"  SetTemp:  {parsed['set_temp']}°C")
        found.append(addr)
    else:
        print(f"Address {addr}: --")

ser.close()

print("\n" + "=" * 60)
if found:
    print(f"Devices found: {len(found)} at addresses: {found}")
else:
    print("No devices found in range 45-55")
print("=" * 60)
