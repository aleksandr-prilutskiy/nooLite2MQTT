using Common;
using nooLite2MQTT;
using System;
using System.IO;

namespace ConsoleApp
{
    /// <summary>
    /// Сервис сопряжения устройств nooLite c протоколом MQTT
    /// Программа для отладки сервиса в режиме консольного приложения
    /// Версия от 23.02.2022
    /// </summary>
    class App
    {
        static void Main()
        {
            Console.WriteLine("Отладка сервиса");
            Console.Write("Запуск....");
            Server service = new Server();
            service.OnStart();
            Console.WriteLine("ОК");
            Console.WriteLine("S - Остановка сервиса");
            Console.WriteLine("R - Запуск сервиса");
            Console.WriteLine("L - Просморт журнала");
            Console.WriteLine("T - Просморт таблицы утройств");
            Console.WriteLine("Q - Выход");
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey();
                Console.WriteLine();
                if ((key.KeyChar == 'Q') || (key.KeyChar == 'q')) break;
                if ((key.KeyChar == 'R') || (key.KeyChar == 'r'))
                {
                    service.OnStart();
                    Console.WriteLine("Сервис запущен");
                }
                if ((key.KeyChar == 'S') || (key.KeyChar == 's'))
                {
                    service.OnStop();
                    Console.WriteLine("Сервис остановлен");
                }
                if ((key.KeyChar == 'L') || (key.KeyChar == 'l'))
                    try
                    {
                        using (FileStream fstream = File.OpenRead(Server.LogFile.FileName))
                        {
                            Console.WriteLine("Файл: " + Server.LogFile.FileName);
                            byte[] array = new byte[fstream.Length];
                            fstream.Read(array, 0, array.Length);
                            string textFromFile = System.Text.Encoding.Default.GetString(array);
                            Console.WriteLine(textFromFile);
                        }
                    }
                    catch { }
                if ((key.KeyChar == 'T') || (key.KeyChar == 't')) 
                {
                    Console.WriteLine("Channels:");
                    for (byte i = 0; i < nooLite._сhannelCount; i++)
                    {
                        Channel channel = Server.Channels.Search(i);
                        if (channel is null) continue;
                        Console.WriteLine(" " + channel.Id.ToString("D2"));
                        if (channel.Devices != null)
                            foreach (Device device in channel?.Devices)
                                Console.WriteLine("  Device [" + device.Addr + "] = " +
                                    (device.State ? "ON" : "OFF") + " (" + device.Bright + "%)");
                        if (channel.Sensors != null)
                            foreach (Sensor sensor in channel.Sensors)
                            {
                                Console.WriteLine("  Sensor #" + sensor.Topic + " = " + sensor.Value);
                                if (sensor.Links != null)
                                    Console.WriteLine("  Links: " + string.Join(",", sensor.Links));
                            }
                    }
                    Console.WriteLine("\nTopics:");
                    foreach (string topic in Server.Topics)
                        Console.WriteLine(" " + topic);
                }
            }
            service.OnStop();
        } // Main()
    } // class App
}
