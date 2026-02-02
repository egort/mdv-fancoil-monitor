namespace MDV
{
    using System.IO.Ports;
    using Timer = System.Timers.Timer;
    using System.Text.Json;
    using System.Net;
    using uPLibrary.Networking.M2Mqtt.Messages;
    using uPLibrary.Networking.M2Mqtt;
    using System.Text;
    using static System.Text.Encoding;
    using System.Collections.Generic;
    using System;
    using System.Linq;

    internal class Program
    {
        private static readonly Timer TimerWrite = new(); // Таймер отправки запроса
        private static readonly Timer TimerNoData = new(); // Таймер ожидания ответа
        private static SerialPort port; // Порт RS485
        static int[] enter; // Массыв с адресами фанкойлов
        static string[] Topics; // Массив топиков для подписки
        static string num; // Номер программы
        static Byte[] QOSMQTT; // Массив QOS для подписки на топики
        static readonly Dictionary<int, Byte[]> FansGet = new(); // Массив с данными для отправки запросов
        static readonly Dictionary<int, Byte[]> FansSet = new(); // Массив с данными для уставки 
        static readonly Dictionary<int, Byte[]> Fanscheck = new();
        static readonly Queue<int> SetNum = new(70); // Очередь для отправки уставок
        static Byte[] indata; // Ответ от фанкойла
        static readonly MqttClient client = new(IPAddress.Parse("127.0.0.1")); //   127.0.0.1 192.168.42.1
        static bool keyRepeat = false;
        static bool keySet = false;
        static int RepeatCounter = 0;
        static int IDfan = 0; // ID фанкойла для отправки запроса
        static int t = 0; // Номер фанкойла для отправки запроса
        static int s = 0; // Контрольная сумма
        static int n = 0; // ID фанкойла для уставки данных
        static void Main(string[] args)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            enter = JsonSerializer.Deserialize<int[]>(args[0], options);
            //enter = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
            num = args[2];
            //num = "1";
            Topics = new string[enter.Length * 5];
            QOSMQTT = new Byte[enter.Length * 5];
            for (int i = 0; i < enter.Length; i++)
            {
                FansGet[enter[i]] = new Byte[17] { 254, 170, 192, (Byte)enter[i], 0, 128, 0, 0, 0, 0, 0, 0, 0, 0, 63, (Byte)(129 - enter[i]), 85 };
                FansSet[enter[i]] = new Byte[17] { 254, 170, 195, (Byte)enter[i], 0, 128, 0, 8, 128, 21, 0, 0, 0, 0, 60, 0, 85 };
                // 0 - mode(08) 1 - speed(09) 2 - setTemp(10) 3 - temp(11) 4 - Blinds(21)
                Fanscheck[enter[i]] = new Byte[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                Topics[0 + i * 5] = $"/devices/Fan-{num}_{enter[i]}/controls/Power/on1";
                Topics[1 + i * 5] = $"/devices/Fan-{num}_{enter[i]}/controls/Mode/on1";
                Topics[2 + i * 5] = $"/devices/Fan-{num}_{enter[i]}/controls/Speed/on1";
                Topics[3 + i * 5] = $"/devices/Fan-{num}_{enter[i]}/controls/SetTemp/on1";
                Topics[4 + i * 5] = $"/devices/Fan-{num}_{enter[i]}/controls/Blinds/on1";
            }

            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
            client.Subscribe(Topics, QOSMQTT);

            static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
            {
                //Console.WriteLine($"{e.Topic}, - {Default.GetString(e.Message)}");

                if (e.Topic.Split('/')[4] == "Power")                                    // Включить выключить
                {
                    if (Default.GetString(e.Message) == "1")
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] < 128)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] += 128;
                        }
                    }
                    else if (Default.GetString(e.Message) == "0")
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] -= 128;
                        }
                    }
                }
                else if (e.Topic.Split('/')[4] == "Mode")                   // Режим работы 0 - холод, 1 - тепло, 2 - вент, 3 - сушка, 4 - авто
                {

                    if (Default.GetString(e.Message) == "0") // Режим охлаждение
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 136;
                        }
                        else
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 8;
                        }
                    }
                    else if (Default.GetString(e.Message) == "1") // Режим Обогрев
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 132;
                        }
                        else
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 4;
                        }
                    }
                    else if (Default.GetString(e.Message) == "2") // Режим Осушение
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 130;
                        }
                        else
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 2;
                        }
                    }
                    else if (Default.GetString(e.Message) == "3") // Режим вентилятора
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 129;
                        }
                        else
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 1;
                        }
                    }
                    else if (Default.GetString(e.Message) == "4") // Режим Авто
                    {
                        if (FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] > 127)
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 144;
                        }
                        else
                        {
                            FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][7] = 16;
                        }
                    }
                }
                else if (e.Topic.Split('/')[4] == "Speed")                   // Скорость вентилятора 1, 2, 3, 4 - авто
                {
                    if (Default.GetString(e.Message) == "1")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][8] = 4;
                    }
                    else if (Default.GetString(e.Message) == "2")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][8] = 2;
                    }
                    else if (Default.GetString(e.Message) == "3")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][8] = 1;
                    }
                    else if (Default.GetString(e.Message) == "4")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][8] = 128;
                    }
                }
                else if (e.Topic.Split('/')[4] == "Blinds")                   // Поворот жалюзи
                {
                    if (Default.GetString(e.Message) == "1")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][10] = 4;
                    }
                    else if (Default.GetString(e.Message) == "0")
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][10] = 0;
                    }
                }
                else if (e.Topic.Split('/')[4] == "SetTemp")                   // Уставка температуры 16-32
                {
                    if (int.Parse(Default.GetString(e.Message)) > 15 & int.Parse(Default.GetString(e.Message)) < 32)
                    {
                        FansSet[int.Parse(e.Topic.Split('/')[2].Split('_').Last())][9] = Byte.Parse(Default.GetString(e.Message));
                    }
                }
                if (!SetNum.Contains(int.Parse(e.Topic.Split('/')[2].Split('_').Last()))) // Добавить в очередь
                {
                    SetNum.Enqueue(int.Parse(e.Topic.Split('/')[2].Split('_').Last()));
                }
            }
            port = new SerialPort
            {
                PortName = args[1],//args[1] "COM3"
                BaudRate = 4800,
                DataBits = 8,
                Parity = System.IO.Ports.Parity.None,
                StopBits = System.IO.Ports.StopBits.One,
                ReadTimeout = 100,
                WriteTimeout = 30,
                ReadBufferSize = 32,
                WriteBufferSize = 17
            };
            port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler2);
            port.ErrorReceived += new SerialErrorReceivedEventHandler(Eror);
            port.PinChanged += new SerialPinChangedEventHandler(Stopp);
            try
            {
                port.Open();
                client.Publish($"/devices/sist-{num}/controls/Serial", Encoding.UTF8.GetBytes("Порт открыт"), 0, false);
            }
            catch (Exception e)
            {
                //Console.WriteLine("ERROR: невозможно открыть порт:" + e.ToString());
                client.Publish($"/devices/sist-{num}/controls/Serial", Encoding.UTF8.GetBytes("Невозможно открыть порт"), 0, false);
                return;
            }
            TimerNoData.Interval = 200;
            TimerNoData.Elapsed += NoConect;
            TimerNoData.Elapsed += Writers;
            TimerNoData.AutoReset = false;
            TimerNoData.Enabled = false;

            TimerWrite.Interval = 140;
            TimerWrite.Elapsed += Writers;
            TimerWrite.AutoReset = false;
            TimerWrite.Enabled = true;

            Console.ReadLine();
        }

        private static void Writers(object? sender, System.Timers.ElapsedEventArgs e)
        {
            TimerNoData.Enabled = true;
            if (keyRepeat & RepeatCounter < 2)
            {
                s = 0;
                for (int i = 1; i < 15; i++)
                {
                    s += FansSet[n][i];
                }
                s += 85;
                FansSet[n][15] = (Byte)(255 - s % 256);
                port.Write(FansSet[n], 0, 17);
                //Console.WriteLine($"Повторная уставка {FansSet[n][2]}");
                RepeatCounter += 1;
            }
            else if (SetNum.Count > 0)
            {
                if (RepeatCounter > 0)
                {
                    RepeatCounter = 0;
                }
                if (!keySet)
                {
                    keySet = true;
                }
                s = 85;
                n = SetNum.Dequeue();
                for (int i = 1; i < 15; i++)
                {
                    s += FansSet[n][i];
                    //Console.Write($"{FansSet[n][i]}:");
                }
                //t = FansSet[n][3];
                t = Array.IndexOf(enter, FansSet[n][3] | 0);
                FansSet[n][15] = (Byte)(255 - s % 256);
                port.Write(FansSet[n], 0, 17);
                //Console.WriteLine($"Уставка {FansSet[n][2]}, вент - {FansSet[n][7]}");
            }
            else
            {
                if (keySet)
                {
                    keySet = false;
                }
                port.Write(FansGet[enter[t]], 0, 17);
                //Console.WriteLine($"Отправка запроса {FansGet[enter[t]][2]}");
                IDfan = enter[t];
                //client.Publish($"/devices/sist-{num}/controls/GanGetID", Encoding.UTF8.GetBytes($"{IDfan}"), 0, false);
                if (enter.Length != 1)
                {
                    if (t < enter.Length - 1)
                    {
                        t += 1;
                    }
                    else
                    {
                        t = 0;
                    }
                }
            }
        }

        private static void NoConect(object? sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine("Нет ответа");
            if (keySet)
            {
                keyRepeat = true;
            }
            else
            {
                client.Publish($"/devices/Fan-{num}_{IDfan}/controls/Alarm", Encoding.UTF8.GetBytes("2"), 0, true);
                Fanscheck[IDfan][9] = 2;
                client.Publish($"/devices/Fan-{num}_{IDfan}/controls/Status", Encoding.UTF8.GetBytes("7"), 0, true);
            }
        }
        static void Eror(
                object sender,
                SerialErrorReceivedEventArgs e)
        {
            //Console.Write($"[eror]");
            client.Publish($"/devices/sist-{num}/controls/Error", Encoding.UTF8.GetBytes($"Ошибка - {e.ToString}"), 0, false);
        }

        static void Stopp(
                object sender,
                SerialPinChangedEventArgs e)
        {
            //Console.Write($"[stopp]");
            client.Publish($"/devices/sist-{num}/controls/Error", Encoding.UTF8.GetBytes("Неизвестное событие"), 0, false);
        }

        /// //////////////////////////////////////////////////////////////////////////////////////////////////
        static int s2 = 0;
        static void DataReceivedHandler2(
                        object sender,
                        SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            if (sp.BytesToRead > 30)
            {
                TimerNoData.Enabled = false;
                if (TimerWrite.Enabled == false)
                {
                    TimerWrite.Enabled = true;
                }

                indata = new Byte[32];
                sp.Read(indata, 0, 32);


                s2 = 85;
                if (indata[0] == 254)
                {
                    indata = indata.Skip(1).ToArray();
                    for (int i = 0; i < indata.Length - 1; i++)
                    {
                        s2 += indata[i];
                        //Console.WriteLine($"[{indata[i]}]");
                    }
                }
                else
                {
                    for (int i = 0; i < indata.Length - 2; i++)
                    {
                        s2 += indata[i];
                        //Console.WriteLine($"[{indata[i]}]");
                    }
                }

                if ((int)indata[30] == 255 - s2 % 256)
                {

                    //Console.WriteLine($"ОК {indata[4]}");
                    if (indata[1] == 192)
                    {
                        if (indata[8] != Fanscheck[indata[4]][0])
                        {
                            Fanscheck[indata[4]][0] = indata[8];
                            DataPars(indata);
                        }
                        else if (indata[9] != Fanscheck[indata[4]][1])
                        {
                            Fanscheck[indata[4]][1] = indata[9];
                            DataPars(indata);
                        }
                        else if (indata[10] != Fanscheck[indata[4]][2])
                        {
                            Fanscheck[indata[4]][2] = indata[10];
                            DataPars(indata);
                        }
                        else if (indata[11] != Fanscheck[indata[4]][3])
                        {
                            Fanscheck[indata[4]][3] = indata[11];
                            DataPars(indata);
                        }
                       else if (indata[21] != Fanscheck[indata[4]][4])
                        {
                            Fanscheck[indata[4]][4] = indata[21];
                            DataPars(indata);
                        }
                        else if (indata[22] != Fanscheck[indata[4]][5])
                        {
                            Fanscheck[indata[4]][5] = indata[22];
                            DataPars(indata);
                        }
                        else if (indata[23] != Fanscheck[indata[4]][6])
                        {
                            Fanscheck[indata[4]][6] = indata[23];
                            DataPars(indata);
                        }
                        else if (indata[23] != Fanscheck[indata[4]][6])
                        {
                            Fanscheck[indata[4]][6] = indata[23];
                            DataPars(indata);
                        }
                        else if (Fanscheck[indata[4]][9] == 2)
                        {
                            Fanscheck[indata[4]][9] = 0;
                            DataPars(indata);
                        }
                        //else if (indata[24] != Fanscheck[indata[4]][7])
                        //{
                        //    Fanscheck[indata[4]][7] = indata[24];
                        //    DataPars(indata);
                        //}
                        //else if (indata[25] != Fanscheck[indata[4]][8])
                        //{
                        //    Fanscheck[indata[4]][8] = indata[25];
                        //    DataPars(indata);
                        //}
                    }
                    //else
                    //{
                    //    DataPars(indata);
                    //}
                }
                else
                {
                    if (keySet)
                    {
                        keyRepeat = true;
                    }
                    client.Publish($"/devices/sist-{num}/controls/Error", Encoding.UTF8.GetBytes("Ошибка данных"), 0, false);
                    //Console.WriteLine("Не ОК");
                }
                sp.DiscardInBuffer();
            }
            else
            {
                sp.DiscardInBuffer();
            }
        }
        static void DataPars(Byte[] data)
        {
            if ((data[8] & 128) == 128) // Вкл Выкл
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Power", Encoding.UTF8.GetBytes("1"), 0, true);
                if (FansSet[data[4]][7] < 128)
                {
                    FansSet[data[4]][7] += 128;
                }
                //FansSet[data[4]][7] = (byte)(FansSet[data[4]][7] | 128);
            }
            else
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Power", Encoding.UTF8.GetBytes("0"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] -= 128;
                }
                //FansSet[data[4]][7] = (byte)(FansSet[data[4]][7] & 127);
            }
            if ((data[8] & 16) == 16) // Режим авто
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Mode", Encoding.UTF8.GetBytes("4"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] = 144;
                }
                else
                {
                    FansSet[data[4]][7] = 16;
                }
                //FansSet[data[4]][7] = (byte)((FansSet[data[4]][7] & 128) | 16);
            }
            else if ((data[8] & 8) == 8) // Режим охлаждения
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Mode", Encoding.UTF8.GetBytes("0"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] = 136;
                }
                else
                {
                    FansSet[data[4]][7] = 8;
                }
                //FansSet[data[4]][7] = (byte)((FansSet[data[4]][7] & 128) | 8);
            }
            else if ((data[8] & 4) == 4) // Режим обогрева
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Mode", Encoding.UTF8.GetBytes("1"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] = 132;
                }
                else
                {
                    FansSet[data[4]][7] = 4;
                }
                //FansSet[data[4]][7] = (byte)((FansSet[data[4]][7] & 128) | 4);
            }
            else if ((data[8] & 2) == 2) // Режим осушения
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Mode", Encoding.UTF8.GetBytes("2"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] = 130;
                }
                else
                {
                    FansSet[data[4]][7] = 2;
                }
                //FansSet[data[4]][7] = (byte)((FansSet[data[4]][7] & 128) | 2);
            }
            else if ((data[8] & 1) == 1) // Режим вентилятора
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Mode", Encoding.UTF8.GetBytes("3"), 0, true);
                if (FansSet[data[4]][7] > 127)
                {
                    FansSet[data[4]][7] = 129;
                }
                else
                {
                    FansSet[data[4]][7] = 1;
                }
                //FansSet[data[4]][7] = (byte)((FansSet[data[4]][7] & 128) | 1);
            }

            if ((data[9] & 128) == 128) // Скорость авто
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Speed", Encoding.UTF8.GetBytes("4"), 0, true);
                FansSet[data[4]][8] = 128;
            }
            else if ((data[9] & 4) == 4)  // Скорость 1
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Speed", Encoding.UTF8.GetBytes("1"), 0, true);
                FansSet[data[4]][8] = 4;
            }
            else if ((data[9] & 2) == 2) // Скорость 2
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Speed", Encoding.UTF8.GetBytes("2"), 0, true);
                FansSet[data[4]][8] = 2;
            }
            else if ((data[9] & 1) == 1) // Скорость 3
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Speed", Encoding.UTF8.GetBytes("3"), 0, true);
                FansSet[data[4]][8] = 1;
            }

            client.Publish($"/devices/Fan-{num}_{data[4]}/controls/SetTemp", Encoding.UTF8.GetBytes($"{data[10]}"), 0, true);
            FansSet[data[4]][9] = data[10];
            client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Temp", Encoding.UTF8.GetBytes($"{(data[11] / 2) - 20}"), 0, true);
            if ((data[20] & 4) == 4) // Жалюзи включены
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Blinds", Encoding.UTF8.GetBytes("1"), 0, true);
                //FansSet[data[4]][10] = 4;
            }
            else if ((data[20] & 4) == 0) // Жалюзи выключены
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Blinds", Encoding.UTF8.GetBytes("0"), 0, true);
                //FansSet[data[4]][10] = 0;
            }

            if (data[22] == 0 & data[23] == 0)
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Alarm", Encoding.UTF8.GetBytes("0"), 0, true);
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("0"), 0, true);
                if ((data[8] & 128) == 128)
                {
                    if ((data[8] & 1) == 1) // Режим вентилятора
                    {
                        client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("4"), 0, true);
                    }
                    else if ((data[8] & 8) == 8) // Режим охлаждения
                    {
                        client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("1"), 0, true);
                    }
                    else if ((data[8] & 4) == 4) // Режим обогрева
                    {
                        client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("2"), 0, true);
                    }
                    else if ((data[8] & 2) == 2) // Режим осушения
                    {
                        client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("3"), 0, true);
                    }
                    else if ((data[8] & 16) == 16) // Режим авто
                    {
                        client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("5"), 0, true);
                    }
                }
                else
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("0"), 0, true);
                }
            }
            else
            {
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Status", Encoding.UTF8.GetBytes("6"), 0, true);
                client.Publish($"/devices/Fan-{num}_{data[4]}/controls/Alarm", Encoding.UTF8.GetBytes("1"), 0, true);
                if ((data[22] & 1) == 1)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("1"), 0, true);
                }
                else if ((data[22] & 2) == 2)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("2"), 0, true);
                }
                else if ((data[22] & 4) == 4)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("3"), 0, true);
                }
                else if ((data[22] & 8) == 8)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("4"), 0, true);
                }
                else if ((data[22] & 16) == 16)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("5"), 0, true);
                }
                else if ((data[22] & 32) == 32)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("6"), 0, true);
                }
                else if ((data[22] & 64) == 64)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("7"), 0, true);
                }
                else if ((data[22] & 128) == 128)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("8"), 0, true);
                }
                else if ((data[23] & 1) == 1)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("9"), 0, true);
                }
                else if ((data[23] & 2) == 2)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("10"), 0, true);
                }
                else if ((data[23] & 4) == 4)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("11"), 0, true);
                }
                else if ((data[23] & 8) == 8)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("12"), 0, true);
                }
                else if ((data[23] & 16) == 16)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("13"), 0, true);
                }
                else if ((data[23] & 32) == 32)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("14"), 0, true);
                }
                else if ((data[23] & 64) == 64)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("15"), 0, true);
                }
                else if ((data[23] & 128) == 128)
                {
                    client.Publish($"/devices/Fan-{num}_{data[4]}/controls/AlarmCode", Encoding.UTF8.GetBytes("16"), 0, true);
                }
            }
        }












    }
}
