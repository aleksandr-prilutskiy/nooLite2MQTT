using nooLite2MQTT;
using System;
using System.IO;

namespace ConsoleApp
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Программа для отладки сервиса в режиме консольного приложения
// Версия от 22.09.2021
//
//===============================================================================================================
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
                if (((key.KeyChar == 'R') || (key.KeyChar == 'r')) && (!Server.Run))
                {
                    service.OnStart();
                    Console.WriteLine("Сервис запущен.");
                }
                if (((key.KeyChar == 'S') || (key.KeyChar == 's')) && (Server.Run))
                {
                    service.OnStop();
                    Console.WriteLine("Сервис остановлен.");
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
                    Console.WriteLine("Devices:");
                    foreach (Server.Device device in Server.Devices)
                        Console.WriteLine(" " + device.Channel.ToString("D2") + ": " + device.Addr +
                            " = " + (device.State ? "ON" : "OFF") + " (" + device.Bright + "%)");
                    Console.WriteLine("Sensors:");
                    foreach (Server.Sensor sensor in Server.Sensors)
                        Console.WriteLine(" " + sensor.Channel.ToString("D2") + ": " + 
                            (sensor.Topic.Length > 0 ? sensor.Topic : "        ") +
                            " = " + sensor.Value.ToString());
                }
            }
            service.OnStop();
        } // Main()
    } // class App
}
