using Common;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Timers;

namespace nooLite2MQTT
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Версия от 26.09.2021
//
//===============================================================================================================
   public partial class Server
    {
        private const string _iniFileName = "nooLite2MQTT.ini"; // Имя файла настроек сервиса
        private const string _logFileName = "nooLite2MQTT.log"; // Имя файла журнала
        private const string _topicService = "@nooLite2MQTT";   // Имя топика управления сервисом
        private string TopicPrefix = "nooLite@";     // Префикс MQTT топиков устройств nooLite 
        public static IniFile IniFile;               // Объект для работы с файлом конфигурации программы
        public static LogFile LogFile;               // Объект для работы с файлом журнала
        public static nooLite nooLite;               // Объект для работы с адаптером nooLite MTRF-64-USB
        public static MQTT MQTT;                     // Объект для работы с брокером MQTT
        private static Timer Timer;                  // Таймер обработки событий
        private const uint _Timer_Update = 250;      // Период срабатывания таймера обработки событий
        private static DateTime nooLite_Timer;       // Таймер опроса состояния устройств nooLite
        private const uint _nooLite_Update = 300000; // Пероид таймера опроса состояния устройств nooLite
        private static bool _assist = false;         // Режим контроля срабатывания включения силовых блоков
        private static bool _debug = false;          // Режим отладки (подробные записи в журнал)
        private static bool _busyTimer = false;      // Признак обработки события по таймеру
        private bool _nooLite_Connect = false;       // Признак успешного подключения и настройки nooLite
        private bool _MQTT_Connect = false;          // Признак успешного подключения и настройки MQTT
        public static bool Run = false;              // Признак что сервис запущен

//===============================================================================================================
// Name...........:	Server
// Description....:	Инициализация объекта
// Syntax.........:	new Server()
//===============================================================================================================
        public Server()
        {
            IniFile = new IniFile(_iniFileName);
            LogFile = new LogFile(_logFileName);
            Devices = new List<Device>();
            Sensors = new List<Sensor>();
            nooLite = new nooLite(LogFile)
            {
                OnPackageSend = OnNooLiteMessage,
                OnPackageRead = OnNooLiteMessage,
            };
            MQTT = new MQTT(LogFile)
            {
                OnMessageSend = OnMQTTMessageSend,
                OnMessageRead = OnMQTTMessageResive
            };
            Timer = new Timer()
            {
                Interval = _Timer_Update
            };
            Timer.Elapsed += new ElapsedEventHandler(OnTimer);
            LogFile?.Write("@Сервис загружен");
        } // Server()

//===============================================================================================================
// Name...........:	OnStart()
// Description....:	Действия при запуске сервиса
//===============================================================================================================
        public void OnStart()
        {
            TopicPrefix = IniFile.ReadString("MQTT", "Prefix", TopicPrefix);
            nooLite.NativePort = IniFile.ReadString("nooLite", "Port", "");
            _debug = IniFile.ReadString("Service", "Mode", "") == "Debug";
            _assist = IniFile.ReadString("Service", "Assist", "") == "On";
            MQTT.ReadConfig(IniFile);
            MQTT.Connect();
            nooLite.Connect();
            LoadSensors(IniFile);
            nooLite_Timer = DateTime.Now;
            LogFile?.Add("@Сервис запущен");
            Timer.Start();
            Run = true;
        } // OnStart()

//===============================================================================================================
// Name...........:	OnStop()
// Description....:	Действия при остановке сервиса
//===============================================================================================================
        public void OnStop()
        {
            Run = false;
            _nooLite_Connect = false;
            _MQTT_Connect = false;
            Timer.Stop();
            nooLite.Close();
            MQTT.Disconnect();
            Devices.Clear();
            Sensors.Clear();
            LogFile?.Add("@Сервис остановлен");
            LogFile?.Save();
        } // OnStop()

//===============================================================================================================
// Name...........:	OnMQTTMessageSend
// Description....:	Обработка исходящих сообщений брокеру MQTT
//===============================================================================================================
        private static void OnMQTTMessageSend(string topic, string message)
        {
            if (_debug) LogFile?.Add("@MQTT SEND= topic: " + topic + "; message = '" + message + "'");
        } // OnMQTTMessageSend(string, string)

//===============================================================================================================
// Name...........:	OnMQTTMessageResive
// Description....:	Обработка входящих сообщаний от брокера MQTT
//===============================================================================================================
        private static void OnMQTTMessageResive(string topic, string message)
        {
            if (_debug) LogFile?.Add("@MQTT READ= topic: " + topic + "; message = '" + message + "'");
        } // OnMQTTMessageResive(string, string)

//===============================================================================================================
// Name...........:	OnNooLiteMessage
// Description....:	Обработка исходящих и входящих сообщений адаптера nooLite MTRF-64 USB
//===============================================================================================================
        private static void OnNooLiteMessage(byte[] buffer)
        {
            Debug_CommandInfo(buffer);
        } // OnNooLiteMessage(byte[])

//===============================================================================================================
// Name...........:	OnTimer
// Description....:	Обработка событий по тамеру
//===============================================================================================================
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            if (_busyTimer) return;
            _busyTimer = true;
            CheckConnection_MQTT();
            CheckConnection_nooLite();
            ScanDevicesOnTimer();
            CheckSensors();
            Events_MQTT();
            LogFile?.Save();
            _busyTimer = false;
        } // OnTimer(object, ElapsedEventArgs)

//===============================================================================================================
// Name...........:	CheckConnection_MQTT
// Description....:	Первичная настройка после подключения к брокеру MQTT и обработка потери соединения
// Syntax.........:	CheckConnection_MQTT()
//===============================================================================================================
        private void CheckConnection_MQTT()
        {
            if (MQTT.Connected && !_MQTT_Connect)
            {
                _MQTT_Connect = true;
                MQTT.Subscribe("#");
            }
            else if (!MQTT.Connected && _MQTT_Connect)
            {
                MQTT.Unsubscribe("#");
                _MQTT_Connect = false;
                LogFile?.Add("@Ошибка: Потеряно соединение с брокером MQTT");
            }
        } // CheckConnection_MQTT()

//===============================================================================================================
// Name...........:	CheckConnection_nooLite
// Description....:	Первичная настройка после подключения и обработка оключения адаптера nooLite MTRF-64 USB
// Syntax.........:	CheckConnection_nooLite()
//===============================================================================================================
        private void CheckConnection_nooLite()
        {
            if (!_MQTT_Connect) return;
            if (nooLite.Connected && !_nooLite_Connect)
            {
                _nooLite_Connect = true;
                ScanDevices();
                Debug_DevicesAndSensors();
            }
            else if (!nooLite.Connected && _nooLite_Connect)
            {
                Devices.Clear();
                _nooLite_Connect = false;
                LogFile?.Add("@Ошибка: Потеряно соединение с модулем nooLite MTRF-64-USB");
            }
        } // CheckConnection_nooLite()

//===============================================================================================================
// Name...........:	Events_MQTT
// Description....:	Обработка сообщений, поступивших от брокера MQTT
// Syntax.........:	Events_MQTT()
// Remarks .......:	Специальные команды (топик - сообщение: описание):
//                  @nooLite2MQTT/GetState - [All или номер канала привязки]: Запрос состояния устройств
//                  @nooLite2MQTT/Action - Restart: Перезапуск сервиса
//===============================================================================================================
        private void Events_MQTT()
        {
            while (true)
            {
                MQTT.Message message = MQTT.GetMessage();
                if (message == null) break;
                byte channel = nooLite._сhannelCount;
                string subtopic = "";
                string topic = message.Topic;
                int pos = topic.IndexOf("/");
                if (pos >= 0)
                {
                    subtopic = topic.Substring(pos);
                    topic = topic.Substring(0, pos);
                }
                if (topic == _topicService)
                {
                    switch (subtopic.ToUpper())
                    {
                        case "/GETSTATE":
                            if (message.Data.ToUpper() == "ALL")
                                MQTT.MessageSend(_topicService + "/State",
                                    JsonSerializer.Serialize(Devices));
                            else if (byte.TryParse(message.Data, out channel))
                                MQTT.MessageSend(_topicService + "/State",
                                    JsonSerializer.Serialize(DeviceSearch(channel)));
                            break;
                        case "/ACTION":
                            if (message.Data.ToUpper() == "RESTART")
                            {
                                OnStop();
                                OnStart();
                            }
                            break;
                    }
                    continue;
                }
                if ((topic.Substring(0, TopicPrefix.Length) != TopicPrefix) ||
                    (!byte.TryParse(topic.Substring(TopicPrefix.Length), out channel))) continue;
                List<Device> devices = DeviceSearch(channel);
                if (devices.Count > 0)
                {
                    uint param;
                    switch (subtopic.ToUpper())
                    {
                        case "/BRIG":
                            if (!uint.TryParse(message.Data, out param)) param = 100;
                            if (devices[0].State && (devices[0].Bright != param))
                                SendCommandToDevice(devices[0], (byte)nooLite.Command.SetBrightness,
                                    0xFF * (param > 100 ? 100 : param) / 100 << 24);
                            break;
                        case "/TEMP":
                            uint.TryParse(message.Data, out param);
                            param = (uint)Math.Floor((decimal)param / 5);
                            if ((param > 0) && (param < 256))
                            {
                                param = (param & 0x00FF) << 24;
                                nooLite.SendCommand(channel, (byte)nooLite.Command.TemporaryOn, 0x05, param);
                            }
                            else if ((param > 255) && (param < 65535))
                            {
                                param = ((param & 0x00FF) << 24) | ((param & 0xFF00) << 8);
                                nooLite.SendCommand(channel, (byte)nooLite.Command.TemporaryOn, 0x06, param);
                            }
                            break;
                        default:
                            switch (message.Data.ToUpper())
                            {
                                case "ON":
                                    if (!devices[0].State)
                                        SendCommandToDevice(devices[0], (byte)nooLite.Command.On);
                                    break;
                                case "OFF":
                                    if (devices[0].State)
                                        SendCommandToDevice(devices[0], (byte)nooLite.Command.Off);
                                    break;
                                //case "CLEAR":
                                //    ChannelClear(channel);
                                //    break;
                            }
                            break;
                    }
                }
                //else
                //    switch (message.Data.ToUpper())
                //    {
                //        case "BIND":
                //            DeviceBing(channel);
                //            break;
                //    }
            }
        } // Events_MQTT()
    } // class Server
}
