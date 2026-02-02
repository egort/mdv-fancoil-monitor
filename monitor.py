import serial
import time
import msvcrt  # Для Windows - проверка нажатия клавиши

def create_request(addr):
    crc = 129 - addr
    return bytes([0xFE, 0xAA, 0xC0, addr, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, crc, 0x55])

# Расшифровка режимов (байт 9)
MODES = {
    0: "OFF",
    129: "FAN",
    130: "DRY",
    132: "HEAT",
    136: "COOL",
    145: "AUTO",
}

# Расшифровка скорости (байт 10)
SPEEDS = {
    0: "---",
    1: "HIGH",
    2: "MED",
    4: "LOW",
    128: "AUTO?",
    132: "???",
}

def get_mode(b9):
    return MODES.get(b9, f"?({b9})")

def get_speed(b10):
    return SPEEDS.get(b10, f"?({b10})")

def get_swing(b21, b22):
    if b21 == 4 and b22 == 1:
        return "ON"
    elif b21 == 0 and b22 == 0:
        return "OFF"
    else:
        return f"?({b21},{b22})"

print("=" * 90)
print("  MDV Monitor - Адрес 48 | Нажми Q или ESC для выхода")
print("=" * 90)

ser = serial.Serial(port='COM10', baudrate=4800, timeout=0.5)
addr = 48
request = create_request(addr)

last_response = None
count = 0
changed_bytes = set()  # Накапливаем все изменявшиеся байты

try:
    while True:
        ser.reset_input_buffer()
        ser.write(request)
        time.sleep(0.15)
        
        response = ser.read(32)
        count += 1
        
        if response and len(response) >= 10:
            # Показываем только если изменилось
            if response != last_response:
                hex_str = response.hex(' ').upper()
                
                # Подсветка изменений
                if last_response:
                    diff = []
                    for i in range(min(len(response), len(last_response))):
                        if response[i] != last_response[i]:
                            diff.append(i)
                            changed_bytes.add(i)
                    print(f"\n*** ИЗМЕНЕНИЕ в байтах: {diff} ***")
                
                print(f"[{count:4d}] {hex_str}")
                
                # Расшифровка известных байтов
                b9 = response[9]
                b10 = response[10]
                settemp = response[11]
                b21 = response[21] if len(response) > 21 else 0
                b22 = response[22] if len(response) > 22 else 0
                mode = get_mode(b9)
                speed = get_speed(b10)
                swing = get_swing(b21, b22)
                print(f"       Режим={mode:5s} | Скорость={speed:5s} | Уставка={settemp}°C | Свинг={swing}")
                
                # Вывод всех изменявшихся байтов
                if changed_bytes:
                    changed_vals = " | ".join([f"Б{i}={response[i]:3d}" for i in sorted(changed_bytes) if i < len(response)])
                    print(f"       Изменявшиеся: {changed_vals}")
                
                last_response = response
        else:
            if count % 20 == 0:
                print(f"[{count:4d}] нет ответа")
        
        # Проверка нажатия клавиши
        if msvcrt.kbhit():
            key = msvcrt.getch()
            if key in (b'q', b'Q', b'\x1b'):  # q, Q или ESC
                print("\n\nВыход по нажатию клавиши.")
                break
        
        time.sleep(0.3)

except KeyboardInterrupt:
    print("\n\nОстановлено (Ctrl+C).")
finally:
    ser.close()
    print("Порт закрыт.")
