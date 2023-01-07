using Microsoft.Win32;
using System.IO;
using System.Reflection;

namespace Common
{
    /// <summary>
    /// Утилита для выполнения действий с правами администратора
    /// </summary>
    /// версия от 18.12.2022
    public class AdminApp
    {
        static void Main(string[] args)
        {
            RegistryKey RegistryKey = Registry.CurrentUser.OpenSubKey(Admin.RegistryPath, true);
            if (args.Length == 0) return;
            switch (args[0])
            {
                case Admin.cmdServiceStart:
                    Admin.ServiceStart();
                    break;
                case Admin.cmdServiceStop:
                    Admin.ServiceStop();
                    break;
                case Admin.cmdServiceInstall:
                    Admin.ServiceInstall();
                    break;
                case Admin.cmdServiceUninstall:
                    Admin.ServiceUninstall();
                    break;
                case Admin.cmdKeyAutoRunSet:
                    RegistryKey.SetValue(Admin.RegistryName, Path.GetDirectoryName(
                        Assembly.GetExecutingAssembly().Location) + "\\" + Admin.TrayMonitorApp);
                    break;
                case Admin.cmdKeyAutoRunDel:
                    RegistryKey.DeleteValue(Admin.RegistryName, false);
                    break;
            }
        } // Main(string[])
    } // class AdminApp
} // namespace Common