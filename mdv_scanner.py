import serial
import serial.tools.list_ports
import time

def list_ports():
    """Показать доступные COM-порты"""
    print("\n=== Доступные COM-порты ===")
    ports = list(serial.tools.list_ports.comports())
    if not ports:
        print("Порты не найдены!")
        return None
    for i, p in enumerate(ports):
        print(f"  {i+1}. {p.device} - {p.description}")
    return ports

def create_request(addr):
    """Создать запрос для адреса фанкойла"""
    crc = 129 - addr
    return bytes([0xFE, 0xAA, 0xC0, addr, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, crc, 0x55])

def parse_response(data):
    """Разобрать ответ фанкойла"""
    if len(data) < 17:
        return None
    
    # Проверка заголовка
    if data[0] != 0xFE or data[1] != 0xAA:
        return None
    
    result = {
        'raw': data.hex(' ').upper(),
        'addr': data[3],
        'power': (data[7] & 0x80) >> 7,  # Бит 7 - вкл/выкл
        'mode_raw': data[7] & 0x1F,      # Биты 0-4 - режим
        'speed_raw': data[8],
        'set_temp': data[9],
        'room_temp': data[10] if len(data) > 10 else 0,
    }
    
    # Расшифровка режима
    modes = {0x08: 'Холод', 0x04: 'Тепло', 0x02: 'Осушение', 0x01: 'Вентилятор', 0x10: 'Авто'}
    result['mode'] = modes.get(result['mode_raw'], f'Неизв({result["mode_raw"]:02X})')
    
    # Расшифровка скорости
    speeds = {0x04: 'Низкая', 0x02: 'Средняя', 0x01: 'Высокая', 0x80: 'Авто'}
    result['speed'] = speeds.get(result['speed_raw'], f'Неизв({result["speed_raw"]:02X})')
    
    return result

def scan_range(port, start_addr, end_addr):
    """Сканировать диапазон адресов"""
    print(f"\n=== Сканирование адресов {start_addr}-{end_addr} ===")
    found = []
    
    for addr in range(start_addr, end_addr + 1):
        request = create_request(addr)
        
        port.reset_input_buffer()
        port.write(request)
        time.sleep(0.15)
        
        response = port.read(32)
        
        if response and len(response) >= 4:
            print(f"  Адрес {addr:3d}: ОТВЕТ - {response.hex(' ').upper()}")
            parsed = parse_response(response)
            if parsed:
                print(f"             Питание: {'ВКЛ' if parsed['power'] else 'ВЫКЛ'}, Режим: {parsed['mode']}, Скорость: {parsed['speed']}, Уставка: {parsed['set_temp']}°C")
            found.append(addr)
        else:
            print(f"  Адрес {addr:3d}: нет ответа")
    
    return found

def single_query(port, addr):
    """Один запрос к конкретному адресу"""
    request = create_request(addr)
    print(f"\nЗапрос:  {request.hex(' ').upper()}")
    
    port.reset_input_buffer()
    port.write(request)
    time.sleep(0.2)
    
    response = port.read(32)
    if response:
        print(f"Ответ:   {response.hex(' ').upper()}")
        print(f"Длина:   {len(response)} байт")
        parsed = parse_response(response)
        if parsed:
            print(f"\n--- Расшифровка ---")
            print(f"  Адрес:     {parsed['addr']}")
            print(f"  Питание:   {'ВКЛ' if parsed['power'] else 'ВЫКЛ'}")
            print(f"  Режим:     {parsed['mode']}")
            print(f"  Скорость:  {parsed['speed']}")
            print(f"  Уставка:   {parsed['set_temp']}°C")
    else:
        print("Нет ответа")

def main():
    print("=" * 50)
    print("  MDV Fancoil Scanner")
    print("=" * 50)
    
    # Показать порты
    ports = list_ports()
    if not ports:
        return
    
    # Выбор порта
    print("\nВведите номер порта (или имя, например COM3): ", end="")
    choice = input().strip()
    
    if choice.isdigit():
        port_name = ports[int(choice) - 1].device
    else:
        port_name = choice.upper()
    
    print(f"\nПодключение к {port_name}...")
    
    try:
        ser = serial.Serial(
            port=port_name,
            baudrate=4800,
            bytesize=8,
            parity='N',
            stopbits=1,
            timeout=0.5
        )
        print(f"Подключено к {port_name}")
    except Exception as e:
        print(f"Ошибка подключения: {e}")
        return
    
    while True:
        print("\n--- Меню ---")
        print("1. Сканировать диапазон 45-55")
        print("2. Сканировать диапазон 0-63")
        print("3. Сканировать свой диапазон")
        print("4. Запрос к одному адресу")
        print("5. Выход")
        print("Выбор: ", end="")
        
        choice = input().strip()
        
        if choice == '1':
            scan_range(ser, 45, 55)
        elif choice == '2':
            scan_range(ser, 0, 63)
        elif choice == '3':
            print("Начальный адрес: ", end="")
            start = int(input().strip())
            print("Конечный адрес: ", end="")
            end = int(input().strip())
            scan_range(ser, start, end)
        elif choice == '4':
            print("Адрес фанкойла: ", end="")
            addr = int(input().strip())
            single_query(ser, addr)
        elif choice == '5':
            break
        else:
            print("Неверный выбор")
    
    ser.close()
    print("Отключено.")

if __name__ == '__main__':
    main()
