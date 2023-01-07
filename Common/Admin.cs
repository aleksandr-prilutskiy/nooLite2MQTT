using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace Common
{
    /// <summary>
    /// Функции для работы с системой от имени администратора
    /// Версия от 06.01.2023
    /// </summary>
    public class Admin
    {
        /// <summary>
        /// Ключ команды запуска сервиса
        /// </summary>
        public const string cmdServiceStart = "81x152xdK3GgHH2IjS63us507K9BGiPy";

        /// <summary>
        /// Ключ команды остановки сервиса
        /// </summary>
        public const string cmdServiceStop = "X50wAU8k5lB2rCr3j96wm7zI78Bn4104";

        /// <summary>
        /// Ключ команды установки сервиса в систему
        /// </summary>
        public const string cmdServiceInstall = "bqW4Na3IzRdP9G2n8F1OQI6r6gmwPWAG";

        /// <summary>
        /// Ключ команды удаления сервиса из системы
        /// </summary>
        public const string cmdServiceUninstall = "FEIs49oKodMqk5Q8r9nh7SDCRCq5cvFm";

        /// <summary>
        /// Ключ команды настройки автозапуска приложения контроля сервиса
        /// </summary>
        public const string cmdKeyAutoRunSet = "4WeymY4250KE7Kqbd6o0aLqeCNt8B0Jb";

        /// <summary>
        /// Ключ команды отключения автозапуска приложения контроля сервиса
        /// </summary>
        public const string cmdKeyAutoRunDel = "Q0ZZNPUF1FvN5BB1pk0FWLpPhqvr45OQ";

        /// <summary>
        /// Путь в системном реестре для настройки автозапуска приложений
        /// </summary>
        public const string RegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        
        /// <summary>
        /// Имя приложения в системном реестре
        /// </summary>
        public const string RegistryName = "nooLite2MQTT_TrayMonitor";

        /// <summary>
        /// Наименование сервиса
        /// </summary>
        public const string ServiceName = "nooLite2MQTT";

        /// <summary>
        /// Описание сервиса
        /// </summary>
        public const string ServiceDisplayName = "Управление устройствами nooLite через протокол MQTT";

        /// <summary>
        /// Имя исполняемого файла программы управления сервисом
        /// </summary>
        public const string TrayMonitorApp = "TrayMonitor.exe";

        /// <summary>
        /// Имя исполняемого файла сервиса
        /// </summary>
        public const string ServiceApp = "nooLite2MQTT.exe";

        /// <summary>
        /// Имя исполняемого файла утилиты для выполнения действий с правами администратора
        /// </summary>
        public const string AdminApp = "admin.exe";

        /// <summary>
        /// Время ожидания запуска и остановки сервиса (в миллисекундах)
        /// </summary>
        public static int Timeout = 60000;

        /// <summary>
        /// Проверка автозапуска приложения при старте Windows
        /// </summary>
        /// <returns> true - если приложение автоматически запускается </returns>
        public static bool IsAutoRun()
        {
            RegistryKey registry = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            return registry.GetValue(RegistryName) != null;
        } // IsAutoRun()

        /// <summary>
        /// Проверка установлен ли Windows-сервис
        /// </summary>
        /// <returns> true - если сервис существует (зарегистрирован в системе) </returns>
        public static bool IsServiceExist()
        {
            try
            {
                foreach (var service in ServiceController.GetServices())
                    if (service.ServiceName == ServiceName)
                        return true;
            }
            catch { }
            return false;
        } // IsServiceExist()

        /// <summary>
        /// Проверка запущен ли Windows-сервис
        /// </summary>
        /// <returns> true - если сервис запущен </returns>
        public static bool IsServiceRuning()
        {
            try
            {
                foreach (var service in ServiceController.GetServices())
                    if (service.ServiceName == ServiceName)
                        return service.Status == ServiceControllerStatus.Running;
            }
            catch { }
            return false;
        } // IsServiceRuning()

        /// <summary>
        /// Установка сервиса
        /// </summary>
        public static void ServiceInstall()
        {
            string filename = AppDomain.CurrentDomain.BaseDirectory + ServiceApp;
            IntPtr scm = OpenSCManager(null, null, 0xF003F);
            if (scm == IntPtr.Zero)
                return;
            try
            {
                IntPtr service = OpenService(scm, ServiceName, 0xF01FF);
                if (service == IntPtr.Zero)
                    CreateService(scm, ServiceName, ServiceDisplayName, 0xF01FF, 0x00000010,
                        0x00000002, 0x00000001, filename, null, IntPtr.Zero, null, null, null);
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        } // ServiceInstall()

        /// <summary>
        /// Удаление сервиса
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        public static void ServiceUninstall()
        {
            IntPtr scm = OpenSCManager(null, null, 0xF003F);
            if (scm == IntPtr.Zero)
                return;
            try
            {
                IntPtr service = OpenService(scm, ServiceName, 0xF01FF);
                if (service != IntPtr.Zero)
                    try
                    {
                        ServiceStop();
                        DeleteService(service);
                    }
                    finally
                    {
                        CloseServiceHandle(service);
                    }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        } // ServiceUninstall()

        /// <summary>
        /// Запуск сервиса
        /// </summary>
        public static void ServiceStart()
        {
            if (IsServiceRuning())
                return;
            IntPtr scm = OpenSCManager(null, null, 0xF003F);
            if (scm == IntPtr.Zero)
                return;
            try
            {
                IntPtr service = OpenService(scm, ServiceName, 0x14);
                if (service != IntPtr.Zero)
                    try
                    {
                        SERVICE_STATUS status = new SERVICE_STATUS();
                        StartService(service, 0, 0);
                        DateTime timer = DateTime.Now.AddMilliseconds(Timeout);
                        while (!IsServiceRuning() && (DateTime.Now < timer))
                            System.Threading.Thread.Sleep(100);
                    }
                    finally
                    {
                        CloseServiceHandle(service);
                    }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        } // ServiceStart()

        /// <summary>
        /// Остановка сервиса
        /// </summary>
        public static void ServiceStop()
        {
            if (!IsServiceRuning())
                return;
            IntPtr scm = OpenSCManager(null, null, 0xF003F);
            if (scm == IntPtr.Zero)
                return;
            try
            {
                IntPtr service = OpenService(scm, ServiceName,0x24);
                if (service != IntPtr.Zero)
                    try
                    {
                        SERVICE_STATUS status = new SERVICE_STATUS();
                        ControlService(service, 0x00000001, status);
                        DateTime timer = DateTime.Now.AddMilliseconds(Timeout);
                        while (IsServiceRuning() && (DateTime.Now < timer))
                            System.Threading.Thread.Sleep(100);
                    }
                    finally
                    {
                        CloseServiceHandle(service);
                    }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        } // ServiceStop()

        /// <summary>
        /// Запустить действие от имени администратора
        /// </summary>
        /// <param name="code"> код действия </param>
        public static void Run(string code)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.Arguments = code;
                process.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + AdminApp;
                process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
                process.WaitForExit();
            }
            catch { }
        } // Run(string)

        /// <summary>
        /// Поиск и активация окна по его заголовку
        /// </summary>
        /// <param name="title"> заголовок окна (Title) </param>
        /// <returns> true - если окно было найдено </returns>
        public static bool WindowActivateIfExist(string title)
        {
            bool exist = false;
            try
            {
                EnumWindows(delegate (IntPtr _hWnd, IntPtr param)
                {
                    int length = GetWindowTextLength(_hWnd);
                    if (length > 0)
                    {
                        StringBuilder buffer = new StringBuilder(length);
                        if ((GetWindowText(_hWnd, buffer, buffer.Capacity + 1) > 0) &&
                            (buffer.ToString() == title))
                        {
                            SetForegroundWindow(_hWnd);
                            exist = true;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return exist;
        } // WindowActivateIfExist(string)

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true,
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName,
            int dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, int dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName,
            string lpDisplayName, int dwDesiredAccess, int dwServiceType,
            int dwStartType, int dwErrorControl, string lpBinaryPathName,
            string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll")]
        private static extern int ControlService(IntPtr hService, int dwControl, SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);

        private class SERVICE_STATUS
        {
            public int dwServiceType = 0;
            public int dwCurrentState = 0;
            public int dwControlsAccepted = 0;
            public int dwWin32ExitCode = 0;
            public int dwServiceSpecificExitCode = 0;
            public int dwCheckPoint = 0;
            public int dwWaitHint = 0;
        } // SERVICE_STATUS

    } // class Admin
} // namespace Common