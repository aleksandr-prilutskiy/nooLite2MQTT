using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Controls;

namespace nooLiteControl
{
    /// <summary>
    /// Приложение для управления устройствами nooLite с использованием сервиса nooLite2MQTT
    /// </summary>
    /// Версия от 02.01.2023
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Имя файла настроек сервиса
        /// </summary>
        public const string IniFileName = "nooLite2MQTT.ini";

        private static IniFile IniFile;
        private const string _serviceTopic = "@nooLite2MQTT";
        public static string TopicPrefix = "nooLite@";     // Префикс MQTT топиков устройств nooLite 
        public static MQTT MQTT;
        private new string Title;
        public static List<string> _log;
        private Channel _currentChannel = null;
        public static bool Refresh = false;

        public static System.Windows.Controls.ItemCollection TopisItems;
        public static System.Windows.Controls.DataGrid log;

        /// <summary>
        /// Список отображаемых сообщений MQTT
        /// </summary>
        public static ObservableCollection<MessageMQTT> MessagesMQTT { get; set; }

        /// <summary>
        /// Токен для завершения потока
        /// </summary>
        public CancellationTokenSource CancelToken;

        /// <summary>
        /// Объект для синхронизации доступа к статическим объектам из разных потоков
        /// </summary>
        public static SynchronizationContext SynchronizationContext;

        /// <summary>
        /// Инициализация программы
        /// </summary>
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
            textAbout.Text = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description +
                "\nверсия: " + Assembly.GetEntryAssembly().GetName().Version.ToString();
            IniFile = new IniFile(IniFileName);
            Rect screen = SystemParameters.WorkArea;
            Left = IniFile.ReadFloat("ControlApp", "Left", (float)((screen.Width - Width) / 2));
            Top = IniFile.ReadFloat("ControlApp", "Top", (float)((screen.Height - Height) / 2));
            CancelToken = new CancellationTokenSource();
            SynchronizationContext = SynchronizationContext.Current;
            MessagesMQTT = new ObservableCollection<MessageMQTT>();
            MQTT = new MQTT();
        } // MainWindow()

        /// <summary>
        /// Действия после загрузки окна программы
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitChannels();
            InitDevices();
            treeListСhannels.ItemsSource = Channels;
            MQTT.ReadConfig(IniFile);
            MQTT.OnMessageRead = MQTTMessageResive;
            MQTT.Start(CancelToken.Token);
            MQTT.Subscribe("#");
            MQTT.Publish(_serviceTopic + "/GetState", "All");
            //DispatcherTimer dispatcherTimer = new DispatcherTimer();
            //dispatcherTimer.Tick += new EventHandler(TimerTick);
            //dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            //dispatcherTimer.Start();
            log = logMQTT;
            logMQTT.ItemsSource = MessagesMQTT;
            TopisItems = sendTopic.Items;
            sendContent.Items.Add("ON");
            sendContent.Items.Add("OFF");
        } // MainWindow_Loaded(object, RoutedEventArgs)

        /// <summary>
        /// Обработка задач по тамеру
        /// </summary>
        //private void TimerTick(object sender, EventArgs e)
        //{
        //if (Refresh)
        //{
        //    SelectDevice(_currentChannel);
        //    Refresh = false;
        //}
        //while (_log.Count > 0)
        //{
        //    logMQTT.Items.Add(_log[0]);
        //    _log.RemoveAt(0);
        //    logMQTT.SelectedItem = logMQTT.Items.Count - 1;
        //}
        //} // TimerTick(object, EventArgs)

        /// <summary>
        /// Обработка закрытия окна программы
        /// </summary>
        private void Window_Closed(object sender, EventArgs e)
        {
            CancelToken.Cancel();
            CancelToken.Dispose();
            MQTT.Disconnect();
            IniFile.WriteString("ControlApp", "Left", Left.ToString());
            IniFile.WriteString("ControlApp", "Top", Top.ToString());
        } // Window_Closed(object, EventArgs)

        /// <summary>
        /// Обработка события при получении MQTT сообщения
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="message"></param>
        private static void MQTTMessageResive(string topic, string message)
        {
            SynchronizationContext.Send(x => AddMessagesMQTT(topic, message), null);
        } // MQTTMessageResive(string, string)

        /// <summary>
        /// Добавление сообщения в журнал MQTT
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="message"></param>
        private static void AddMessagesMQTT(string topic, string message)
        {

            MessageMQTT data = new MessageMQTT()
            {
                Topic = topic,
                Content = message,
                Time = DateTime.Now.ToString(),
            };
            MessagesMQTT.Add(data);
            while (MessagesMQTT.Count > 500)
                MessagesMQTT.RemoveAt(0);
            log.ScrollIntoView(data);
            foreach (string item in TopisItems)
                if (item == topic)
                    return;
            TopisItems.Add(topic);
        } // AddMessagesMQTT(string, string)

        /// <summary>
        /// Отправка сообщения брокеру MQTT
        /// </summary>
        private void PublishMQTT(object sender, RoutedEventArgs e)
        {
            MQTT.Publish(sendTopic.Text, sendContent.Text);
        } // PublishMQTT(object, RoutedEventArgs)

        /// <summary>
        /// 
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((TabControl)sender).SelectedIndex == 1)
                if (MessagesMQTT.Count > 0)
                    logMQTT.ScrollIntoView(MessagesMQTT[0]);
        } // TabControl_SelectionChanged(object, SelectionChangedEventArgs)
















        /// <summary>
        /// Окрытие окна настроек программы
        /// </summary>
        private void Button_Setup(object sender, EventArgs e)
        {
            new BindingWindow().ShowDialog();
        } // Button_Setup(object, EventArgs)

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
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

        /// <summary>
        /// 
        /// </summary>
        private void CancelSetup(object sender, RoutedEventArgs e)
        {
        } // CancelSetup(object, RoutedEventArgs)

        /// <summary>
        /// 
        /// </summary>
        private void DeviceOn(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if ((channel == null) || (channel.Count == 0)) return;
            MQTT.Publish(TopicPrefix + channel.Index.ToString(), "ON");
            MQTT.Publish(_serviceTopic + "/GetState", channel.Index.ToString());
            SelectDevice(sender, e);
        } // DeviceOn(object, RoutedEventArgs)

        /// <summary>
        /// 
        /// </summary>
        private void DeviceOff(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if ((channel == null) || (channel.Count == 0)) return;
            MQTT.Publish(TopicPrefix + channel.Index.ToString(), "OFF");
            MQTT.Publish(_serviceTopic + "/GetState", channel.Index.ToString());
            SelectDevice(sender, e);
        } // DeviceOff(object, RoutedEventArgs)

        /// <summary>
        /// 
        /// </summary>
        private void SetTitle(object sender, RoutedEventArgs e)
        {
            Channel channel = CurrentChannel();
            if (channel == null) return;
            channel.Title = textDeviceTitle.Text;
            IniFile.WriteString("Channel#" + channel.Index.ToString(), "Title", channel.Title);
            SelectDevice(sender, e);
        } // DeviceOff(object, RoutedEventArgs)

    } // class MainWindow
}
