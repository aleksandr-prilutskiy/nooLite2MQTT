using Common;
using Microsoft.Win32;
using System;
using System.Text;
using System.Windows;
using System.Reflection;
using System.IO;

namespace Service_Monitor
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Программа для отладки сервиса в через файл журнала и протокол MQTT
// Версия от 22.09.2021
//
//===============================================================================================================
    public partial class MainWindow : Window
    {
        public static IniFile IniFile;
        public static MQTT MQTT;
        public static string LogFileName = "";
        private static int LogFileSize = 0;
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;
        private bool Run = false;
        private bool _busy = false;
        private bool _state = false;

//===============================================================================================================
// Name...........:	SetupWindow
// Description....:	Инициализация программы
//===============================================================================================================
        public MainWindow()
        {
            InitializeComponent();
            IniFile = new IniFile();
            MQTT = new MQTT();
            Left = IniFile.ReadFloat("Window", "Left", 0);
            Top = IniFile.ReadFloat("Window", "Top", 0);
            Width = IniFile.ReadFloat("Window", "Width", 1200);
            Height = IniFile.ReadFloat("Window", "Height", 600);
        } // MainWindow()

//===============================================================================================================
// Name...........:	MainWindow_Loaded
// Description....:	Действия после загрузки окна программы
//===============================================================================================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Title = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            MQTT.ReadConfig(IniFile);
            //MQTT.Start();
            MQTT.Subscribe("#");
            LogFileName = IniFile.ReadString("Log", "File", "");
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(TimerTick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            dispatcherTimer.Start();
            sendMQTTTopic.Items.Add("@nooLite2MQTT");
            sendMQTTTopic.Items.Add("@nooLite2MQTT/GetState");
            sendMQTTTopic.Items.Add("@nooLite2MQTT/Action");
            sendMQTTText.Items.Add("ON");
            sendMQTTText.Items.Add("OFF");
            sendMQTTText.Items.Add("BIND");
            sendMQTTText.Items.Add("CLEAR");
            sendMQTTText.Items.Add("All");
            sendMQTTText.Items.Add("Restart");
            statusFileName.Text = LogFileName;
            Button_PlayStop(sender, null);
        } // MainWindow_Loaded(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	Button_Setup
// Description....:	Окрытие окна настроек программы
//===============================================================================================================
        private void Button_Setup(object sender, EventArgs e)
        {
            new SetupWindow().ShowDialog();
        } // Button_Setup(object, EventArgs)

//===============================================================================================================
// Name...........:	Button_PlayStop
// Description....:	Остановка и запуск чтения log-файла 
//===============================================================================================================
        private void Button_PlayStop(object sender, EventArgs e)
        {
            Run = !Run;
            if (Run && ((LogFileName == "") || !File.Exists(LogFileName)))
                Run = false;
            if (_state == Run) return;
            _state = Run;
            //if (Run)
            //    imagePlayStop.Source = GetIcon("iconPause").Source;
            //else
            //    imagePlayStop.Source = GetIcon("iconPlay").Source;
        } // Button_PlayStop(object, EventArgs)

//===============================================================================================================
// Name...........:	MQTTMessageSend
// Description....: Отправка настраиваемого сообщения MQTT
//===============================================================================================================
        private void MQTTMessageSend(object sender, RoutedEventArgs e)
        {
            if ((sendMQTTTopic.Text == "") || (sendMQTTText.Text == "")) return;
            //MQTT.MessageSend(sendMQTTTopic.Text, sendMQTTText.Text);
        } // MQTTMessageResiveSend(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	TimerTick
// Description....:	Обработка событий по тамеру
//========================================================A=======================================================
        private void TimerTick(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            if (statusFileName.Text != LogFileName) statusFileName.Text = LogFileName;
            while (true)
            {
                MQTT.Message newMessage = MQTT.GetMessage();
                if (newMessage == null) break;
                textMQTT.Text += "MQTT topic: " + newMessage.Topic + "; " +
                    "message = '" + newMessage.Data + "'\n";
                textMQTT.ScrollToEnd();
                if (sendMQTTTopic.Items.IndexOf(newMessage.Topic) < 0)
                    sendMQTTTopic.Items.Add(newMessage.Topic);
            }
            if (Run)
            {
                FileInfo file = new FileInfo(LogFileName);
                if (LogFileSize != file.Length)
                {
                    string text = "";
                    try
                    {
                        FileStream fileRead = File.OpenRead(LogFileName);
                        byte[] array = new byte[fileRead.Length];
                        fileRead.Read(array, 0, array.Length);
                        text += Encoding.Default.GetString(array);
                        fileRead.Close();
                    }
                    catch { }
                    textLogFile.Text = text;
                    textLogFile.ScrollToEnd();
                    LogFileSize = (int)file.Length;
                }
            }
            _busy = false;
        } // TimerTick(object, EventArgs)

//===============================================================================================================
// Name...........:	Window_Closing
// Description....:	Перехват завершения работы приложения с выводом предупреждения
//===============================================================================================================
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
            MQTT.Disconnect();
            IniFile.WriteString("Window", "Left", Left.ToString());
            IniFile.WriteString("Window", "Top", Top.ToString());
            IniFile.WriteString("Window", "Width", Width.ToString());
            IniFile.WriteString("Window", "Height", Height.ToString());
        } // Window_Closing(sender, CancelEventArgs)

//===============================================================================================================
// Name...........:	MainMenu_Profile_Save
// Description....:	Сохранение профиля в файл
// Remarks........: Главное меню: Профиль -> Сохранить в файл...
//===============================================================================================================
        private void MainMenu_Profile_Save(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Сохранение профиля в файл...",
                Filter = "Файл cfg|*.cfg",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
            };
            if (dialog.ShowDialog() == false) return;
            int pos = dialog.FileName.LastIndexOf("\\");
            if (pos < 0) return;
            string dir = dialog.FileName.Substring(0, pos);
            string filename = dialog.FileName.Substring(pos + 1);
            StreamWriter file = null;
            try
            {
                file = new StreamWriter(dialog.FileName, false, System.Text.Encoding.Default);
            }
            catch (Exception)
            {
                return;
            }
            finally
            {
                file?.Close();
            }
            IniFile newFile = new IniFile(dir, filename);
            newFile.WriteString("Log", "File", LogFileName);
            newFile.WriteString("MQTT", "Host", MQTT.BrokerAddress);
            newFile.WriteString("MQTT", "Port", MQTT.BrokerPort.ToString());
            newFile.WriteString("MQTT", "User", MQTT.UserName);
            newFile.WritePassword("MQTT", "Password", MQTT.Password);
        } // MainMenu_Profile_Save(object, EventArgs)

//===============================================================================================================
// Name...........:	MainMenu_Profile_Load
// Description....:	Чтение профиля из файла
// Remarks........: Главное меню: Профиль -> Загрузить из файла...
//===============================================================================================================
        private void MainMenu_Profile_Load(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Чтение профиля из файла...",
                Filter = "Файл cfg|*.cfg",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
            };
            if (dialog.ShowDialog() == false) return;
            int pos = dialog.FileName.LastIndexOf("\\");
            if (pos < 0) return;
            string dir = dialog.FileName.Substring(0, pos);
            string filename = dialog.FileName.Substring(pos + 1);
            IniFile newFile = new IniFile(dir, filename);
            LogFileName = newFile.ReadString("Log", "File", LogFileName);
            MQTT.BrokerAddress = newFile.ReadString("MQTT", "Host", MQTT.BrokerAddress);
            MQTT.BrokerPort = newFile.ReadInt("MQTT", "Port", MQTT.BrokerPort);
            MQTT.UserName = newFile.ReadString("MQTT", "User", MQTT.UserName);
            MQTT.Password = newFile.ReadPassword("MQTT", "Password", MQTT.Password);
            IniFile.WriteString("Log", "File", LogFileName);
            IniFile.WriteString("MQTT", "Host", MQTT.BrokerAddress);
            IniFile.WriteString("MQTT", "Port", MQTT.BrokerPort.ToString());
            IniFile.WriteString("MQTT", "User", MQTT.UserName);
            IniFile.WritePassword("MQTT", "Password", MQTT.Password);
        } // MainMenu_Profile_Load(object, EventArgs)

//===============================================================================================================
// Name...........:	MainMenu_Exit
// Description....:	Завершение работы программы
// Remarks........: Главное меню: Профиль -> Выход
//===============================================================================================================
        private void MainMenu_Exit(object sender, EventArgs e)
        {
            Window_Closing(sender, null);
            Close();
        } // MainMenu_Exit(object, EventArgs)
    } // class MainWindow
}
