using Common;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace TrayMonitor
{
    /// <summary>
    /// Сервис nooLite2MQTT - программа управления сервисом
    /// </summary>
    /// версия от 06.01.2023
    public partial class WindowMain : Window
    {
        /// <summary>
        /// Идентификатор синхронизации для предотвращения повторного запуска приложения
        /// </summary>
        public const string AppMutexName = "nooLite2MQTT_TrayMotitor";

        /// <summary>
        /// Имя файла настроек сервиса
        /// </summary>
        public const string IniFileName = "nooLite2MQTT.ini";

        /// <summary>
        /// Имя файла журнала сервиса
        /// </summary>
        public const string FileNameLog = "nooLite2MQTT.log";

        /// <summary>
        /// Указатель на иконку в трее
        /// </summary>
        public static Hardcodet.Wpf.TaskbarNotification.TaskbarIcon MyNotifyIcon;

        /// <summary>
        /// Иконка в трее по умолчанию
        /// </summary>
        private static Icon iconMain = new Icon(Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/appicon.ico")).Stream);

        /// <summary>
        /// Иконка в трее, когда сервис запущен
        /// </summary>
        private static Icon iconRun = new Icon(Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/tray_Green.ico")).Stream);

        /// <summary>
        /// Иконка в трее, когда сервис не запущен
        /// </summary>
        private static Icon iconStop = new Icon(Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/tray_Red.ico")).Stream);

        /// <summary>
        /// Период срабатывания таймера обработки событий (в миллисекундах)
        /// </summary>
        private const uint TimerInterval = 1000;

        /// <summary>
        /// Таймер для обработки событий
        /// </summary>
        private System.Windows.Threading.DispatcherTimer Timer;

        /// <summary>
        /// Запуск приложения
        /// </summary>
        public WindowMain()
        {
            Title = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            new Mutex(true, AppMutexName, out bool isSingleInstance);
            if (!isSingleInstance)
            {
                MessageBox.Show("Ошибка: Одна копия программы уже запущена", Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.ServiceNotification);
                Application.Current.Shutdown();
            }
            InitializeComponent();
            MyNotifyIcon = NotifyIcon;
            MyNotifyIcon.Icon = iconMain;
            NotifyIconToolTip.Text = Title;
        } // WindowMain()

        /// <summary>
        /// Действия при загрузке программы
        /// </summary>
        private void MainWindow_Loaded(object sender, EventArgs e)
        {
            Visibility = Visibility.Hidden;
            SetServiceState();
            Timer = new System.Windows.Threading.DispatcherTimer();
            Timer.Tick += new EventHandler(OnTimer);
            Timer.Interval = new TimeSpan(TimerInterval);
            Timer.Start();
        } // MainWindow_Loaded(object, EventArgs)

        private void OnTimer(object sender, EventArgs e)
        {
            SetServiceState();
        } // OnTimer(object, EventArgs)

        /// <summary>
        /// Перехват завершения работы приложения с выводом предупреждения
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            e.Cancel = true;
        } // MainWindow_Closing(sender, CancelEventArgs)

        /// <summary>
        /// Завершение работы программы
        /// </summary>
        private void MainWindow_Exit()
        {
            Timer.Stop();
            MyNotifyIcon.Dispose();
            Application.Current.Shutdown();
        } // MainWindow_Exit()

        /// <summary>
        /// Настройка пунктов меню управления сервисом
        /// </summary>
        private void SetServiceState()
        {
            if (Admin.IsServiceExist())
            {
                if (Admin.IsServiceRuning())
                {
                    TrayItem_ServiceStart.Visibility = Visibility.Collapsed;
                    TrayItem_ServiceStop.Visibility = Visibility.Visible;
                    TrayItem_ServiceInstall.Visibility = Visibility.Collapsed;
                    TrayItem_ServiceUninstall.Visibility = Visibility.Collapsed;
                    MyNotifyIcon.Icon = iconRun;
                }
                else
                {
                    TrayItem_ServiceStart.Visibility = Visibility.Visible;
                    TrayItem_ServiceStop.Visibility = Visibility.Collapsed;
                    TrayItem_ServiceInstall.Visibility = Visibility.Collapsed;
                    TrayItem_ServiceUninstall.Visibility = Visibility.Visible;
                    MyNotifyIcon.Icon = iconStop;
                }
            }
            else
            {
                TrayItem_ServiceStart.Visibility = Visibility.Collapsed;
                TrayItem_ServiceStop.Visibility = Visibility.Collapsed;
                TrayItem_ServiceInstall.Visibility = Visibility.Visible;
                TrayItem_ServiceUninstall.Visibility = Visibility.Collapsed;
                MyNotifyIcon.Icon = iconStop;
            }
        } // SetServiceState()

        /// <summary>
        /// Запуск приложения управления устройствами nooLite
        /// </summary>
        private void TrayMenu_Control(object sender, EventArgs e)
        {
            if (Admin.WindowActivateIfExist("Сервис nooLite2MQTT - Управление устройствами"))
                return;
            try
            {
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\nooLiteControl.exe");
            }
            catch
            {
                MessageBox.Show("Ошибка:\nФайл nooLiteControl.exe не найден.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        } // TrayMenu_Control(object, EventArgs)

        /// <summary>
        /// Запуск сервиса
        /// </summary>
        private void TrayMenu_ServiceStart(object sender, EventArgs e)
        {
            Admin.Run(Admin.cmdServiceStart);
            SetServiceState();
        } // TrayMenu_ServiceStart(object, EventArgs)

        /// <summary>
        /// Остановка сервиса
        /// </summary>
        private void TrayMenu_ServiceStop(object sender, EventArgs e)
        {
            Admin.Run(Admin.cmdServiceStop);
            SetServiceState();
        } // TrayMenu_ServiceStop(object, EventArgs)

        /// <summary>
        /// Установка сервиса
        /// </summary>
        private void TrayMenu_ServiceInstall(object sender, EventArgs e)
        {
            if (MessageBox.Show("Сервис 'nooLite2MQTT' не установлен.\n" +
                "Хотите зарегистрировать сервис в системе?",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes,
                MessageBoxOptions.ServiceNotification) == MessageBoxResult.Yes)
            {
                Admin.Run(Admin.cmdServiceInstall);
                SetServiceState();
            }
        } // TrayMenu_ServiceInstall(object, EventArgs)

        /// <summary>
        /// Удаление сервиса
        /// </summary>
        private void TrayMenu_ServiceUninstall(object sender, EventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите удалить сервис из системы?",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes,
                MessageBoxOptions.ServiceNotification) == MessageBoxResult.Yes)
            {
                Admin.Run(Admin.cmdServiceUninstall);
                SetServiceState();
            }
        } // TrayMenu_ServiceUninstall(object, EventArgs)

        /// <summary>
        /// Просмотр журнала сервиса
        /// </summary>
        private void TrayMenu_LogView(object sender, EventArgs e)
        {
            Process.Start("notepad.exe",
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\" + FileNameLog);
        } // TrayMenu_LogView(object, EventArgs)

        /// <summary>
        /// Настройка программы
        /// </summary>
        private void TrayMenu_Setup(object sender, EventArgs e)
        {
            if (!WindowSetup.IsShow)
                new WindowSetup(this).ShowDialog();
        } //TrayMenu_Setup(object, EventArgs)

        /// <summary>
        /// Завершение работы программы
        /// </summary>
        private void TrayMenu_Exit(object sender, EventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите закрыть программу?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                MessageBoxOptions.ServiceNotification) == MessageBoxResult.Yes)
                MainWindow_Exit();
        } // TrayMenu_Exit(object, EventArgs)

        /// <summary>
        /// Функция для отладки
        /// </summary>
        private void Debug(object sender, EventArgs e)
        {
            MessageBox.Show("Отладка", Title, MessageBoxButton.OK, MessageBoxImage.Asterisk,
                MessageBoxResult.OK, MessageBoxOptions.ServiceNotification);
        } // Debug(object, EventArgs)
    } // class WindowMain
} // namespace TrayMonitor