using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace nooLiteControl
{
//===============================================================================================================
//
// Приложение для управления устройствами nooLite с использованием сервиса nooLite2MQTT
// Версия от 16.09.2021
//
//===============================================================================================================
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string _iniFileName = "nooLite2MQTT.ini";
        private const string _serviceTopic = "@nooLite2MQTT";
        public static string TopicPrefix = "nooLite@";     // Префикс MQTT топиков устройств nooLite 
        public static MQTT MQTT;
        private static IniFile IniFile;
        private new string Title;
        public static List<string> _log;
        private Channel _currentChannel = null;
        public static bool Refresh = false;

//===============================================================================================================
// Name...........:	MainWindow
// Description....:	Инициализация программы
//===============================================================================================================
        public MainWindow()
        {
            Title = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            new Mutex(true, "nooLite2MQTT Control App", out bool isSingleInstance);
            if (!isSingleInstance)
            {
                MessageBox.Show("Ошибка! Одна копия программы уже запущена.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            InitializeComponent();
            _log = new List<string>();
            IniFile = new IniFile(_iniFileName);
            Rect screen = SystemParameters.WorkArea;
            Left = IniFile.ReadFloat("ControlApp", "Left", (float)((screen.Width - Width) / 2));
            Top = IniFile.ReadFloat("ControlApp", "Top", (float)((screen.Height - Height) / 2));
            MQTT = new MQTT();
            MQTT.OnMessageRead = MQTTMessageResive;
            MQTT.ReadConfig(IniFile);
            //MQTT.Start();
            MQTT.Subscribe("#");
        } // MainWindow()

//===============================================================================================================
// Name...........:	MainWindow_Loaded
// Description....:	Действия после загрузки окна программы
//===============================================================================================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitChannels();
            InitDevices();
            treeListСhannels.ItemsSource = Channels;
            MQTT.Publish(_serviceTopic + "/GetState", "All");
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(TimerTick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            dispatcherTimer.Start();
        } // MainWindow_Loaded(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	Button_Setup
// Description....:	Окрытие окна настроек программы
//===============================================================================================================
        private void Button_Setup(object sender, EventArgs e)
        {
            new BindingWindow().ShowDialog();
        } // Button_Setup(object, EventArgs)

//===============================================================================================================
// Name...........:	TimerTick
// Description....:	Обработка задач по тамеру
//===============================================================================================================
        private void TimerTick(object sender, EventArgs e)
        {
            if (Refresh)
            {
                SelectDevice(_currentChannel);
                Refresh = false;
            }
            while (_log.Count > 0)
            {
                logMQTT.Items.Add(_log[0]);
                _log.RemoveAt(0);
                logMQTT.SelectedItem = logMQTT.Items.Count - 1;
            }  
        } // TimerTick(object, EventArgs)

        private static void MQTTMessageResive(string topic, string message)
        {
            if (topic == _serviceTopic + "/State")
            {
                LoadChannels(message);
                Refresh = true;
            }
            else if (Devices != null)
            {
                //string subtopic = "";
                //int pos = topic.IndexOf("/");
                //if (pos >= 0)
                //{
                //    subtopic = topic.Substring(pos);
                //    topic = topic.Substring(0, pos);
                //}
                //foreach (Device device in Devices)
                //    if (topic == TopicPrefix + device.Addr)
                        _log.Add(DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy: ") + topic + " -> " + message);
            }
        } // MQTTMessageResive(string, string)

        private void SelectDevice(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Controls.DataGrid grid = (System.Windows.Controls.DataGrid)sender;
                Channel channel = (Channel)grid.CurrentCell.Item;
                SelectDevice(channel);
            }
            catch { }
        } // SelectDevice(object, EventArgs)

        private void SelectDevice(Channel channel)
        {
            if (channel == null) return;
            gridListDevices.ItemsSource = LoadDevices(channel.Index);
            textDeviceTitle.Text = channel.Title;
            List<Device> devices = SearchDevice(channel);
            if (devices?.Count > 0)
                if (devices[0].State)
                {
                    buttonOn.IsEnabled = false;
                    buttonOff.IsEnabled = true;
                    buttonClear.IsEnabled = true;
                }
                else
                {
                    buttonOn.IsEnabled = true;
                    buttonOff.IsEnabled = false;
                    buttonClear.IsEnabled = true;
                }
            else
            {
                buttonOn.IsEnabled = false;
                buttonOff.IsEnabled = false;
                buttonClear.IsEnabled = false;
            }
            _currentChannel = channel;
        } // SelectDevice(Channel)

        private void Window_Closed(object sender, EventArgs e)
        {
            MQTT.Disconnect();
            IniFile.WriteString("ControlApp", "Left", Left.ToString());
            IniFile.WriteString("ControlApp", "Top", Top.ToString());
        } // Window_Closed(object, EventArgs)


        private void CancelSetup(object sender, RoutedEventArgs e)
        {
        } // CancelSetup(object, RoutedEventArgs)

        private void DeviceOn(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if ((channel == null) || (channel.Count == 0)) return;
            MQTT.Publish(TopicPrefix + channel.Index.ToString(), "ON");
            MQTT.Publish(_serviceTopic + "/GetState", channel.Index.ToString());
            SelectDevice(sender, e);
        } // DeviceOn(object, RoutedEventArgs)

        private void DeviceOff(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if ((channel == null) || (channel.Count == 0)) return;
            MQTT.Publish(TopicPrefix + channel.Index.ToString(), "OFF");
            MQTT.Publish(_serviceTopic + "/GetState", channel.Index.ToString());
            SelectDevice(sender, e);
        } // DeviceOff(object, RoutedEventArgs)

        private void SetTitle(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if (channel == null) return;
            channel.Title = textDeviceTitle.Text;
            IniFile.WriteString("Channel#" + channel.Index.ToString(), "Title", channel.Title);
            SelectDevice(sender, e);
        } // DeviceOff(object, RoutedEventArgs)

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //if ((((System.Windows.Controls.TabControl)sender).SelectedIndex == 0) && (_currentChannel != null))
            //    SelectDevice(_currentChannel);
        }
    } // class MainWindow
}
