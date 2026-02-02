import serial
import serial.tools.list_ports
import time

def create_request(addr):
    """Создать запрос для адреса фанкойла"""
    crc = 129 - addr
    return bytes([0xFE, 0xAA, 0xC0, addr, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, crc, 0x55])

def parse_response(data):
    """Разобрать ответ фанкойла"""
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
    
    modes = {0x08: 'Холод', 0x04: 'Тепло', 0x02: 'Осушение', 0x01: 'Вентилятор', 0x10: 'Авто'}
    result['mode'] = modes.get(result['mode_raw'], f'Неизв({result["mode_raw"]:02X})')
    
    speeds = {0x04: 'Низкая', 0x02: 'Средняя', 0x01: 'Высокая', 0x80: 'Авто'}
    result['speed'] = speeds.get(result['speed_raw'], f'Неизв({result["speed_raw"]:02X})')
    
    return result

print("=" * 60)
print("  MDV Fancoil Scanner - Автосканирование")
print("=" * 60)

# Найти COM-порты
ports = list(serial.tools.list_ports.comports())
print(f"\nНайдено портов: {len(ports)}")
for p in ports:
    print(f"  - {p.device}: {p.description}")

if not ports:
    print("COM-порты не найдены!")
    exit(1)

# Берём первый порт
port_name = ports[0].device
print(f"\nИспользую порт: {port_name}")

try:
    ser = serial.Serial(
        port=port_name,
        baudrate=4800,
        bytesize=8,
        parity='N',
        stopbits=1,
        timeout=0.5
    )
    print("Подключено!\n")
except Exception as e:
    print(f"Ошибка: {e}")
    exit(1)

print("=" * 60)
print("  Сканирование адресов 45-55")
print("=" * 60)

found = []
for addr in range(45, 56):
    request = create_request(addr)
    
    ser.reset_input_buffer()
    ser.write(request)
    time.sleep(0.15)
    
    response = ser.read(32)
    
    if response and len(response) >= 4:
        print(f"\nАдрес {addr}: ОТВЕТ!")
        print(f"  RAW: {response.hex(' ').upper()}")
        parsed = parse_response(response)
        if parsed:
            print(f"  Питание:  {'ВКЛ' if parsed['power'] else 'ВЫКЛ'}")
            print(f"  Режим:    {parsed['mode']}")
            print(f"  Скорость: {parsed['speed']}")
            print(f"  Уставка:  {parsed['set_temp']}°C")
        found.append(addr)
    else:
        print(f"Адрес {addr}: --")

ser.close()

print("\n" + "=" * 60)
if found:
    print(f"Найдено устройств: {len(found)} на адресах: {found}")
else:
    print("Устройства не найдены в диапазоне 45-55")
print("=" * 60)
