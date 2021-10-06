using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace Service_Monitor
{
    public partial class SetupWindow : Window
    {
//===============================================================================================================
// Name...........:	SetupWindow
// Description....:	Инициализация окна
//===============================================================================================================
        public SetupWindow()
        {
            InitializeComponent();
            Window window = Application.Current.Windows[0];
            Left = window.Left + (window.Width - Width) / 2;
            Top = window.Top + (window.Height - Height) / 2;
        } // SetupWindow()

//===============================================================================================================
// Name...........:	SetupWindow_Loaded
// Description....:	Действия после загрузки окна настроек программы
//===============================================================================================================
        private void SetupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            setupLogFilename.Text = MainWindow.IniFile.ReadString("Log", "File", "");
            setupMQTTAddr.Text = MainWindow.IniFile.ReadString("MQTT", "Host", MainWindow.MQTT.BrokerAddress);
            setupMQTTPort.Text = MainWindow.IniFile.ReadInt("MQTT", "Port", MainWindow.MQTT.BrokerPort).ToString();
            setupMQTTUser.Text = MainWindow.IniFile.ReadString("MQTT", "User", MainWindow.MQTT.UserName);
            setupMQTTPass.Password = MainWindow.IniFile.ReadPassword("MQTT", "Password", MainWindow.MQTT.Password);
        } // SetupWindow_Loaded(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	LogFileSearch
// Description....:	Поиск файла журнала
//===============================================================================================================
        private void LogFileSearch(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Поиск файла журнала...",
                Filter = "Файл журнала|*.log"
            };
            if (dialog.ShowDialog() == false) return;
            if (!File.Exists(dialog.FileName)) return;
            setupLogFilename.Text = dialog.FileName;
        } // CancelSetup(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	SaveSetup
// Description....:	Сохранение настроек программы
//===============================================================================================================
        private void SaveSetup(object sender, RoutedEventArgs e)
        {
            MainWindow.LogFileName = setupLogFilename.Text;
            if (!int.TryParse(setupMQTTPort.Text, out int portMQTT)) portMQTT = 1883;
            MainWindow.IniFile.WriteString("Log", "File", setupLogFilename.Text);
            MainWindow.IniFile.WriteString("MQTT", "Host", setupMQTTAddr.Text);
            MainWindow.IniFile.WriteString("MQTT", "Port", portMQTT.ToString());
            MainWindow.IniFile.WriteString("MQTT", "User", setupMQTTUser.Text);
            MainWindow.IniFile.WritePassword("MQTT", "Password", setupMQTTPass.Password);
            Close();
        } // SaveSetup(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	CancelSetup
// Description....:	Закрыти окна настроек программы без сохранения изменений
//===============================================================================================================
        private void CancelSetup(object sender, RoutedEventArgs e)
        {
            Close();
        } // CancelSetup(object, RoutedEventArgs)
    } // class SetupWindow
}
