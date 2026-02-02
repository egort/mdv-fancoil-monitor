import serial
import serial.tools.list_ports
import time

def list_ports():
    """Show available COM ports"""
    print("\n=== Available COM Ports ===")
    ports = list(serial.tools.list_ports.comports())
    if not ports:
        print("No ports found!")
        return None
    for i, p in enumerate(ports):
        print(f"  {i+1}. {p.device} - {p.description}")
    return ports

def create_request(addr):
    """Create request for fancoil address"""
    crc = 129 - addr
    return bytes([0xFE, 0xAA, 0xC0, addr, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, crc, 0x55])

def parse_response(data):
    """Parse fancoil response"""
    if len(data) < 17:
        return None
    
    # Check header
    if data[0] != 0xFE or data[1] != 0xAA:
        return None
    
    result = {
        'raw': data.hex(' ').upper(),
        'addr': data[3],
        'power': (data[7] & 0x80) >> 7,  # Bit 7 - on/off
        'mode_raw': data[7] & 0x1F,      # Bits 0-4 - mode
        'speed_raw': data[8],
        'set_temp': data[9],
        'room_temp': data[10] if len(data) > 10 else 0,
    }
    
    # Mode decoding
    modes = {0x08: 'Cool', 0x04: 'Heat', 0x02: 'Dry', 0x01: 'Fan', 0x10: 'Auto'}
    result['mode'] = modes.get(result['mode_raw'], f'Unknown({result["mode_raw"]:02X})')
    
    # Speed decoding
    speeds = {0x04: 'Low', 0x02: 'Med', 0x01: 'High', 0x80: 'Auto'}
    result['speed'] = speeds.get(result['speed_raw'], f'Unknown({result["speed_raw"]:02X})')
    
    return result

def scan_range(port, start_addr, end_addr):
    """Scan address range"""
    print(f"\n=== Scanning addresses {start_addr}-{end_addr} ===")
    found = []
    
    for addr in range(start_addr, end_addr + 1):
        request = create_request(addr)
        
        port.reset_input_buffer()
        port.write(request)
        time.sleep(0.15)
        
        response = port.read(32)
        
        if response and len(response) >= 4:
            print(f"  Address {addr:3d}: RESPONSE - {response.hex(' ').upper()}")
            parsed = parse_response(response)
            if parsed:
                print(f"             Power: {'ON' if parsed['power'] else 'OFF'}, Mode: {parsed['mode']}, Speed: {parsed['speed']}, SetTemp: {parsed['set_temp']}°C")
            found.append(addr)
        else:
            print(f"  Address {addr:3d}: no response")
    
    return found

def single_query(port, addr):
    """Single query to specific address"""
    request = create_request(addr)
    print(f"\nRequest:  {request.hex(' ').upper()}")
    
    port.reset_input_buffer()
    port.write(request)
    time.sleep(0.2)
    
    response = port.read(32)
    if response:
        print(f"Response: {response.hex(' ').upper()}")
        print(f"Length:   {len(response)} bytes")
        parsed = parse_response(response)
        if parsed:
            print(f"\n--- Decoded ---")
            print(f"  Address:  {parsed['addr']}")
            print(f"  Power:    {'ON' if parsed['power'] else 'OFF'}")
            print(f"  Mode:     {parsed['mode']}")
            print(f"  Speed:    {parsed['speed']}")
            print(f"  SetTemp:  {parsed['set_temp']}°C")
    else:
        print("No response")

def main():
    print("=" * 50)
    print("  MDV Fancoil Scanner")
    print("=" * 50)
    
    # Show ports
    ports = list_ports()
    if not ports:
        return
    
    # Select port
    print("\nEnter port number (or name, e.g. COM3): ", end="")
    choice = input().strip()
    
    if choice.isdigit():
        port_name = ports[int(choice) - 1].device
    else:
        port_name = choice.upper()
    
    print(f"\nConnecting to {port_name}...")
    
    try:
        ser = serial.Serial(
            port=port_name,
            baudrate=4800,
            bytesize=8,
            parity='N',
            stopbits=1,
            timeout=0.5
        )
        print(f"Connected to {port_name}")
    except Exception as e:
        print(f"Connection error: {e}")
        return
    
    while True:
        print("\n--- Menu ---")
        print("1. Scan range 45-55")
        print("2. Scan range 0-63")
        print("3. Scan custom range")
        print("4. Query single address")
        print("5. Exit")
        print("Choice: ", end="")
        
        choice = input().strip()
        
        if choice == '1':
            scan_range(ser, 45, 55)
        elif choice == '2':
            scan_range(ser, 0, 63)
        elif choice == '3':
            print("Start address: ", end="")
            start = int(input().strip())
            print("End address: ", end="")
            end = int(input().strip())
            scan_range(ser, start, end)
        elif choice == '4':
            print("Fancoil address: ", end="")
            addr = int(input().strip())
            single_query(ser, addr)
        elif choice == '5':
            break
        else:
            print("Invalid choice")
    
    ser.close()
    print("Disconnected.")

if __name__ == '__main__':
    main()
