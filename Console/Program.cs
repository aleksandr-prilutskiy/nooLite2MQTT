using Common;
using nooLite2MQTT;
using System;
using System.IO;

namespace ConsoleApp
{
    /// <summary>
    /// Сервис сопряжения устройств nooLite c протоколом MQTT
    /// Программа для отладки сервиса в режиме консольного приложения
    /// </summary>
    /// Версия от 06.01.2023
    class App
    {

        /// <summary>
        /// Перехват необработанных исключений
        /// </summary>
        public static void UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;
            Console.WriteLine("Исключение: " + exception.Message);
        } // UnhandledException(object, UnhandledExceptionEventArgs)

        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);

            Console.WriteLine("Отладка сервиса");
            Console.Write("Запуск....");
            Server service = new Server();
            service.OnStart();
            Console.WriteLine("ОК");
            Console.WriteLine("S - Остановка сервиса");
            Console.WriteLine("R - Запуск сервиса");
            Console.WriteLine("L - Просморт журнала");
            Console.WriteLine("T - Просморт таблицы утройств");
            Console.WriteLine("D - Отладка");
            Console.WriteLine("Q - Выход");
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey();
                Console.WriteLine();
                if ((key.KeyChar == 'Q') || (key.KeyChar == 'q'))
                {
                    service.OnStop();
                    break;
                }
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
                    for (byte i = 0; i < nooLite.ChannelCount; i++)
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
                if ((key.KeyChar == 'D') || (key.KeyChar == 'd'))
                {
                    Console.WriteLine("Debug...");
                    throw new Exception("2");
                    int i = 0;
                    var x = 4 / i;
                }
            }
            service.OnStop();
        } // Main()
    } // class App
} // namespace ConsoleApp