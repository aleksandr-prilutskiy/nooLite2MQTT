using Common;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;


namespace TrayMonitor
{
//===============================================================================================================
//
// Приложение для контроля и управления сервсом nooLite2MQTT через иконку в системном трее
// Версия от 16.09.2021
//
//===============================================================================================================
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string strServiceNotInstall = "Cервис не установлен";
        private const string strWaitForServiceStop = "Остановка сервиса...";
        private const string strRunService = "Запустить сервис";
        private const string strWaitForServiceRun = "Запуск сервиса...";
        private const string strStopService = "Остановить сервис";
        private const string _iniFileName = "nooLite2MQTT.ini";
        private const string _ServiceName = "nooLite2MQTT";
        private new string Title;                    // Название программы
        private static IniFile IniFile;              // Файл конфигурации программы
        private NotifyIcon MyNotifyIcon;             // Объект - значок в области уведомлений
        private static DispatcherTimer Timer;        // Таймер обработки событий
        private const uint _Timer_Update = 1000;     // Период срабатывания таймера обработки событий
        private bool _exitEnable = false;            // Разрешение завершения программы

//===============================================================================================================
// Name...........:	MainWindow
// Description....:	Инициализация программы
//===============================================================================================================
        public MainWindow()
        {
            Title = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            new Mutex(true, "nooLite2MQTT Tray Monitor", out bool isSingleInstance);
            if (!isSingleInstance)
            {
                System.Windows.MessageBox.Show("Ошибка! Одна копия программы уже запущена.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            InitializeComponent();
            IniFile = new IniFile(_iniFileName);
            Left = IniFile.ReadFloat("TrayMonitor", "Left",
                (float)((SystemParameters.WorkArea.Width - Width) / 2));
            Top = IniFile.ReadFloat("TrayMonitor", "Top",
                (float)((SystemParameters.WorkArea.Height - Height) / 2));
            MyNotifyIcon = new NotifyIcon();
            MyNotifyIcon.Text = Title;
            MyNotifyIcon.MouseClick += TrayMenu_OpenMenu;
        } // MainWindow()

//===============================================================================================================
// Name...........:	MainWindow_Loaded
// Description....:	Действия после загрузки окна программы
//===============================================================================================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string[] portnames = SerialPort.GetPortNames();
            foreach (string portname in portnames)
                setupPortMTRF64.Items.Insert(0, portname);
            setupPortMTRF64.Items.Insert(0, "Авто");
            LoadIniFile();
            Visibility = Visibility.Hidden;
            MyNotifyIcon.Visible = true;
            Timer = new DispatcherTimer();
            Timer.Interval = new TimeSpan(_Timer_Update);
            Timer.Tick += new EventHandler(OnTimer);
            Timer.Start();
        } // MainWindow_Loaded(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	LoadIniFile
// Description....:	Загрузка настроек из ini-файла
//===============================================================================================================
        private void LoadIniFile()
        {
            string port = IniFile.ReadString("nooLite", "Port", "");
            if (port == "") port = "Авто";
            object select = null;
            foreach (string item in setupPortMTRF64.Items)
                if (item == port) select = item;
            if (select != null) setupPortMTRF64.SelectedItem = select;
            else
            {
                setupPortMTRF64.Items.Add(port);
                setupPortMTRF64.SelectedItem = port;
            }
            setupAppAutostart.IsChecked = IsAutoRun();
            setupModeAssist.IsChecked = IniFile.ReadString("Service", "Assist", "") == "On";
            setupModeDebug.IsChecked = IniFile.ReadString("Service", "Mode", "") == "Debug";
            setupMQTT_Host.Text = IniFile.ReadString("MQTT", "Host", "localhost");
            setupMQTT_Port.Text = IniFile.ReadInt("MQTT", "Port", 1883).ToString();
            setupMQTT_User.Text = IniFile.ReadString("MQTT", "User", "");
            setupMQTT_Pass.Password = IniFile.ReadPassword("MQTT", "Password", "");
            Close();
        } // LoadIniFile()

//===============================================================================================================
// Name...........:	OnTimer
// Description....: Обработка событий по таймеру
//===============================================================================================================
        private void OnTimer(object sender, EventArgs e)
        {
            if (IsServiceExist())
            {
                bool run = IsServiceRunning();
                if (run && ((string)TrayItem_Service.Header != strStopService) &&
                    ((string)TrayItem_Service.Header != strWaitForServiceStop))
                {
                    TrayItem_Service.Header = strStopService;
                    TrayItem_Service.Icon = GetIcon("iconStop");
                    MyNotifyIcon.Icon = new Icon(System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/Icon-Green.ico")).Stream);
                }
                else if (!run && ((string)TrayItem_Service.Header != strRunService) &&
                        ((string)TrayItem_Service.Header != strWaitForServiceRun))
                {
                    TrayItem_Service.Header = strRunService;
                    TrayItem_Service.Icon = GetIcon("iconRun");
                    MyNotifyIcon.Icon = new Icon(System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/Icon-Red.ico")).Stream);
                }
                TrayItem_Service.IsEnabled = true;
            }
            else
            {
                if ((string)TrayItem_Service.Header != strServiceNotInstall)
                {
                    TrayItem_Service.Header = strServiceNotInstall;
                    TrayItem_Service.Icon = null;
                    MyNotifyIcon.Icon = new Icon(System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/AppIcon.ico")).Stream);
                    TrayItem_Service.IsEnabled = false;
                }
            }
        } // OnTimer(object, EventArgs)

        private bool IsServiceExist()
        {
            try
            {
                foreach (var service in ServiceController.GetServices())
                    if (service.ServiceName == _ServiceName) return true;
            }
            catch { }
            return false;
        } // IsServiceExist()

//===============================================================================================================
// Name...........:	SaveSetup
// Description....:	Сохранение настроек системы
//===============================================================================================================
        private void SaveSetup(object sender, RoutedEventArgs e)
        {
            string port = (string)setupPortMTRF64.SelectedItem;
            if (port == "Авто") port = "";
            IniFile.WriteString("nooLite", "Port", port);
            if (IsAutoRun())
            {
                if (setupAppAutostart.IsChecked != true)
                    Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\service.exe",
                        "autostart \"" + _ServiceName + "\" " +
                        "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
            }
            else
            {
                if (setupAppAutostart.IsChecked == true)
                    Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\service.exe",
                        "unsetstart \"" + _ServiceName  + "\" " +
                        "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
            }
            IniFile.WriteString("Service", "Assist", setupModeAssist.IsChecked == true ? "On" : "Off");
            IniFile.WriteString("Service", "Mode", setupModeDebug.IsChecked == true ? "Debug" : "");
            IniFile.WriteString("MQTT", "Host", setupMQTT_Host.Text);
            IniFile.WriteString("MQTT", "Port", setupMQTT_Port.Text);
            IniFile.WriteString("MQTT", "User", setupMQTT_User.Text);
            IniFile.WritePassword("MQTT", "Password", setupMQTT_Pass.Password);
            Close();
        } // SaveSetup(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	CancelSetup
// Description....:	Закрыти окна настроек системы без сохранения изменений
//===============================================================================================================
        private void CancelSetup(object sender, RoutedEventArgs e)
        {
            LoadIniFile();
            Close();
        } // CancelSetup(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	MainWindow_Closing
// Description....:	Перехват завершения работы приложения
//===============================================================================================================
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            if (!_exitEnable) e.Cancel = true;
        } // MainWindow_Closing(sender, CancelEventArgs)

//===============================================================================================================
// Name...........:	TrayMenu_OpenMainWindow
// Description....:	Открытие меню в трее
//===============================================================================================================
        private void TrayMenu_OpenMenu(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) TrayMenu.IsOpen = true;
        } // TrayMenu_OpenMenu(sender, MouseEventArgs)

//===============================================================================================================
// Name...........:	TrayMenu_Setup
// Description....:	Открытие окна настроек
//===============================================================================================================
        private void TrayMenu_Setup(object sender, EventArgs e)
        {
            tabGeneral.IsSelected = true;
            Visibility = Visibility.Visible;
        } // TrayMenu_Setup(object, EventArgs)

//===============================================================================================================
// Name...........:	TrayMenu_Control
// Description....:	Открытие программы управления устройствами nooLite
//===============================================================================================================
        private void TrayMenu_Control(object sender, EventArgs e)
        {
            try
            {
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\nooLiteControl.exe");
            }
            catch
            {
                System.Windows.MessageBox.Show("Ошибка:\nФайл nooLiteControl.exe не найден.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        } // TrayMenu_Control(object, EventArgs)

//===============================================================================================================
// Name...........:	TrayMenu_Service
// Description....:	Управление сервисом
//===============================================================================================================
        private void TrayMenu_Service(object sender, EventArgs e)
        {
            if (IsServiceRunning())
            {
                TrayItem_Service.Header = strWaitForServiceStop;
                //Service.Stop();
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\service.exe",
                    "stop \"" + _ServiceName + "\"");
            }
            else
            {
                TrayItem_Service.Header = strWaitForServiceRun;
                //Service.Start();
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\service.exe",
                    "start \"" + _ServiceName + "\"");
            }
            TrayItem_Service.IsEnabled = false;
        } // TrayMenu_Service(sender, MouseEventArgs)

//===============================================================================================================
// Name...........:	TrayMenu_Exit
// Description....:	Завершение работы программы
//===============================================================================================================
        private void TrayMenu_Exit(object sender, EventArgs e)
        {
            //Visibility = Visibility.Hidden;
            if (System.Windows.MessageBox.Show("Вы действительно хотите закрыть программу?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            Timer.Stop();
            MyNotifyIcon.Dispose();
            //Service.Delete();
            IniFile.WriteString("TrayMonitor", "Left", Left.ToString());
            IniFile.WriteString("TrayMonitor", "Top", Top.ToString());
            _exitEnable = true;
            Close();
        } // TrayMenu_Exit(object, EventArgs)

        private bool IsAutoRun()
        {
            bool result = false;
            try
            {
                RegistryKey registry = Registry.LocalMachine.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                if (registry != null)
                    result = (string)registry.GetValue(_ServiceName) ==
                        System.Windows.Forms.Application.ExecutablePath;
                registry.Close();
            }
            catch
            {
                result = false;
            }
            return result;
        } // IsAutoRun()

        private bool IsServiceRunning()            // Возвращает True если служба запущена
        {
            bool result = false;
            try
            {
                ServiceController Service = new ServiceController(_ServiceName);
                result = Service.Status == ServiceControllerStatus.Running;
                Service.Close();
            }
            catch { }
            return result;
        } // IsServiceRunning()

//===============================================================================================================
// Name...........:	GetIcon
// Description....:	Получение пиктограммы из ресурсов по наименованию
// Syntax.........:	GetIcon(resource)
// Parameters.....:	resource    - наименование ресурса пиктограммы
//===============================================================================================================
        public System.Windows.Controls.Image GetIcon(string resource)
        {
            System.Windows.Controls.Image icon = new System.Windows.Controls.Image
            {
                Source = (System.Windows.Media.Imaging.BitmapImage)FindResource(resource)
            };
            return icon;
        } // GetIcon(string)
    } // class MainWindow
}
