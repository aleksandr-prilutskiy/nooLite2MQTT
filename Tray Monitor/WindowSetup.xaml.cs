using Common;
using System;
using System.Reflection;
using System.Windows;

namespace TrayMonitor
{
    /// <summary>
    /// Логика взаимодействия для окна настроек приложения
    /// </summary>
    /// версия от 02.01.2023
    public partial class WindowSetup : Window
    {
        /// <summary>
        /// Ссылка на объект WindowMain
        /// </summary>
        private WindowMain WindowMain;

        /// <summary>
        /// Признак того, что окно отображается на экране
        /// </summary>
        public static bool IsShow = false;

        /// <summary>
        /// Инициализация окна
        /// </summary>
        public WindowSetup(WindowMain window)
        {
            InitializeComponent();
            WindowMain = window;
            Title = WindowMain.Title + " - Настройки";
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
            LoadIniFile();
            AboutText.Text = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            VersionText.Content = "версия: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
            IsShow = true;
        } // WindowAbout()

        /// <summary>
        /// Загрузка настроек из ini-файла
        /// </summary>
        private void LoadIniFile()
        {
            IniFile iniFile = new IniFile(WindowMain.IniFileName);
            string port = iniFile.ReadString("nooLite", "Port");
            if (string.IsNullOrEmpty(port))
                port = "Авто";
            object select = null;
            foreach (string item in setupPortMTRF64.Items)
                if (item == port)
                    select = item;
            if (select != null)
                setupPortMTRF64.SelectedItem = select;
            else
            {
                setupPortMTRF64.Items.Add(port);
                setupPortMTRF64.SelectedItem = port;
            }
            setupAppAutostart.IsChecked = Admin.IsAutoRun();
            setupModeAssist.IsChecked = iniFile.ReadString("Service", "Assist") == "On";
            setupModeDebug.IsChecked = iniFile.ReadString("Service", "Mode") == "Debug";
            setupMQTT_Host.Text = iniFile.ReadString("MQTT", "Host", "localhost");
            setupMQTT_Port.Text = iniFile.ReadInt("MQTT", "Port", 1883).ToString();
            setupMQTT_User.Text = iniFile.ReadString("MQTT", "User");
            setupMQTT_Pass.Password = iniFile.ReadPassword("MQTT", "Password");
        } // LoadIniFile()

        /// <summary>
        /// Проверка настроек подключения к брокеру MQTT
        /// </summary>
        private void TestMQTTConnect(object sender, RoutedEventArgs e)
        {
            MQTT MQTT = new MQTT();
            MQTT.BrokerAddress = setupMQTT_Host.Text;
            int.TryParse(setupMQTT_Port.Text, out MQTT.BrokerPort);
            MQTT.UserName = setupMQTT_User.Text;
            MQTT.Password = setupMQTT_Pass.Password;
            if (MQTT.Test())
                MessageBox.Show("Успешное подключение к брокеру MQTT", WindowMain.Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Ошибка подключения к брокеру MQTT", WindowMain.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
        } // TestMQTTConnect(object, RoutedEventArgs)

        /// <summary>
        /// Обработка нажатия кнопки 'Сохранить'
        /// </summary>
        private void ButtonClick_Save(object sender, EventArgs e)
        {
            IniFile iniFile = new IniFile(WindowMain.IniFileName);
            iniFile.WriteString("nooLite", "Port", setupPortMTRF64.SelectedItem.ToString());
            iniFile.WriteString("Service", "Assist", (bool)setupModeAssist.IsChecked ? "On" : "Off");
            iniFile.WriteString("Service", "Mode", (bool)setupModeDebug.IsChecked ? "Debug" : string.Empty);
            iniFile.WriteString("MQTT", "Host", setupMQTT_Host.Text);
            if (int.TryParse(setupMQTT_Port.Text, out int port))
                iniFile.WriteString("MQTT", "Port", port.ToString());
            iniFile.WriteString("MQTT", "User", setupMQTT_User.Text);
            iniFile.WritePassword("MQTT", "Password", setupMQTT_Pass.Password);
            if ((bool)setupAppAutostart.IsChecked ^ Admin.IsAutoRun())
            {
                if (Admin.IsAutoRun())
                    Admin.Run(Admin.cmdKeyAutoRunDel);
                else
                    Admin.Run(Admin.cmdKeyAutoRunSet);
            }
            Close();
        } // ButtonClick_Save(object, EventArgs)

        /// <summary>
        /// Обработка нажатия кнопки 'Отмена'
        /// </summary>
        private void ButtonClick_Cancel(object sender, EventArgs e)
        {
            Close();
        } // ButtonClick_Cancel(object, EventArgs)

        /// <summary>
        /// Действия при закрытии окна
        /// </summary>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsShow = false;
        } // WindowClosing(sender, CancelEventArgs)

    } // class WindowSetup
} // namespace TrayMonitor