import serial
import time

def create_set_command(addr, power_on=True, mode='FAN', speed='LOW', temp=24):
    """
    Create SET command for MDV fancoil
    
    mode: 'FAN', 'DRY', 'HEAT', 'COOL', 'AUTO'
    speed: 'LOW', 'MED', 'HIGH', 'AUTO'
    temp: 16-32
    """
    
    # Mode byte (7): power bit + mode
    mode_values = {
        'FAN': 1,
        'DRY': 2,
        'HEAT': 4,
        'COOL': 8,
        'AUTO': 16,
    }
    mode_byte = mode_values.get(mode, 1)
    if power_on:
        mode_byte += 128  # Set power bit
    
    # Speed byte (8)
    speed_values = {
        'HIGH': 1,
        'MED': 2,
        'LOW': 4,
        'AUTO': 128,
    }
    speed_byte = speed_values.get(speed, 4)
    
    # Build command: FE AA C3 ADDR 00 80 00 MODE SPEED TEMP 00 00 00 00 3C CRC 55
    cmd = [0xFE, 0xAA, 0xC3, addr, 0x00, 0x80, 0x00, mode_byte, speed_byte, temp, 0x00, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x55]
    
    # Calculate CRC: 255 - (sum of bytes 1-14 + 85) % 256
    s = 85  # 0x55
    for i in range(1, 15):
        s += cmd[i]
    cmd[15] = (255 - s % 256) & 0xFF
    
    return bytes(cmd)

def send_command(port, cmd, description):
    print(f"\n{description}")
    print(f"TX: {cmd.hex(' ').upper()}")
    
    port.reset_input_buffer()
    port.write(cmd)
    time.sleep(0.3)
    
    response = port.read(32)
    if response:
        print(f"RX: {response.hex(' ').upper()}")
        return response
    else:
        print("No response!")
        return None

# Connect
ser = serial.Serial(port='COM10', baudrate=4800, timeout=0.5)
addr = 48

print("=" * 60)
print("  MDV Command Sender - Address 48")
print("=" * 60)

# Power ON, FAN mode, LOW speed, 24°C
cmd_on = create_set_command(addr, power_on=True, mode='FAN', speed='LOW', temp=24)
send_command(ser, cmd_on, ">>> POWER ON (FAN, LOW, 24°C)")

time.sleep(1)

# Read status
from monitor import create_request
status_cmd = create_request(addr)
send_command(ser, status_cmd, ">>> READ STATUS")

ser.close()
print("\nDone!")
