using System.ServiceProcess;

namespace ServiceApp
{
    static class Program
    {
        /// <summary>
        /// Сервис сопряжения устройств nooLite c протоколом MQTT
        /// Версия от 23.02.2022
        /// Главная точка входа для приложения.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new ServiceMain()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
