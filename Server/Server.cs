using Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace nooLite2MQTT
{
    /// <summary>
    /// Сервис сопряжения устройств nooLite c протоколом MQTT
    /// Версия от 06.01.2023
    /// </summary>
    public partial class Server
    {
        /// <summary>
        /// Имя файла настроек сервиса
        /// </summary>
        private const string _fileIniName = "nooLite2MQTT.ini";

        /// <summary>
        /// Имя файла журнала сервиса
        /// </summary>
        private const string _fileLogName = "nooLite2MQTT.log";

        /// <summary>
        /// Версия программы
        /// </summary>
        public static string Version = "0.2.2.1";

        /// <summary>
        /// Префикс MQTT топиков устройств nooLite
        /// </summary>
        public static string TopicPrefix = "nooLite/";

        /// <summary>
        /// Объект для работы с файлом журнала
        /// </summary>
        public static LogFile LogFile;

        /// <summary>
        /// Объект для работы с файлом журнала
        /// </summary>
        public static Channels Channels;

        /// <summary>
        /// Объект для работы с адаптером nooLite MTRF-64-USB
        /// </summary>
        public static nooLite nooLite;

        /// <summary>
        /// Объект для работы с брокером MQTT
        /// </summary>
        public static MQTT MQTT;

        /// <summary>
        /// Список обрататываемых топиков
        /// </summary>
        public static List<string> Topics;

        /// <summary>
        /// Режим отладки (подробные записи в журнал)
        /// </summary>
        public static bool Debug = false;

        /// <summary>
        /// Признак что сервис запущен
        /// </summary>
        public static bool Run = false;

        /// <summary>
        /// Токен для завершения потока
        /// </summary>
        public CancellationTokenSource CancelToken;

        /// <summary>
        ///	Инициализация объекта
        /// </summary>
        public Server()
        {
            LogFile = new LogFile(_fileLogName);
            LogFile?.Add("nooLite2MQTT - Сервис сопряжения устройств nooLite c MQTT. Версия: " + Version);
            MQTT = new MQTT(LogFile)
            {
                OnConnect = OnMQTTConnect,
                OnMessageSend = OnMQTTMessageSend,
                OnMessageRead = OnMQTTMessageResive,
            };
            nooLite = new nooLite(LogFile)
            {
                OnPackageSend = nooLiteCommandInfo,
                OnPackageRead = nooLiteCommandInfo,
            };
            Channels = new Channels(MQTT, nooLite, LogFile);
            LogFile?.Add("@Сервис загружен");
        } // Server()

        /// <summary>
        /// Действия при запуске сервиса
        /// </summary>
        public void OnStart()
        {
            if (Run)
                return;
            Run = true;
            LogFile?.Add("@Запуск сервиса");
            IniFile IniFile = new IniFile(_fileIniName);
            TopicPrefix = IniFile.ReadString("MQTT", "Prefix", TopicPrefix);
            nooLite.NativePort = IniFile.ReadString("nooLite", "Port");
            Debug = IniFile.ReadString("Service", "Mode", "") == "Debug";
            Topics = new List<string>();
            Channels.Load(IniFile);
            CancelToken = new CancellationTokenSource();
            new Thread(() => MainThread(CancelToken.Token)).Start();
            MQTT.Topics.Clear();
            MQTT.Topics.Add(TopicPrefix + "#");
            MQTT.ReadConfig(IniFile);
            MQTT.Start(CancelToken.Token);
            nooLite.Start(CancelToken.Token);
            Channels.Start(CancelToken.Token);
            LogFile?.Save();
        } // OnStart()

        /// <summary>
        /// Действия при остановке сервиса
        /// </summary>
        public void OnStop()
        {
            if (!Run)
                return;
            CancelToken.Cancel();
            MQTT.Disconnect();
            nooLite.Disconnect();
            Topics.Clear();
            LogFile?.Add("@Сервис остановлен");
            LogFile?.Save();
            CancelToken.Dispose();
            Run = false;
        } // OnStop()

        /// <summary>
        /// Действия при перезагрузке или выключении ПК
        /// </summary>
        public void OnShutdown()
        {
            OnStop();
        } // OnShutdown()

        /// <summary>
        /// Главный поток обработки событий
        /// </summary>
        /// <param name="cancelToken"> токен остановки потока </param>
        private static void MainThread(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                LogFile?.Save();
                Thread.Sleep(500);
            }
        } // MainThread(CancellationToken)

        /// <summary>
        /// Обработка исходящих сообщений брокеру MQTT
        /// </summary>
        /// <param name="topic"> имя топика </param>
        /// <param name="message"> передавемые данные </param>
        private static void OnMQTTMessageSend(string topic, string message)
        {
            if (Debug)
                LogFile?.Add("@MQTT SEND= topic: " + topic + "; message = '" + message + "'");
        } // OnMQTTMessageSend(string, string)

        /// <summary>
        /// Обработка входящих сообщений от брокера MQTT
        /// </summary>
        /// <param name="topic"> имя топика </param>
        /// <param name="message"> передавемые данные </param>
        private static void OnMQTTMessageResive(string topic, string message)
        {
            if (Debug)
                LogFile?.Add("@MQTT READ= topic: " + topic + "; message = '" + message + "'");
            CheckEventsMQTT();
        } // OnMQTTMessageResive(string, string)

        /// <summary>
        /// Обработка подключения к брокеру MQTT
        /// </summary>
        private static void OnMQTTConnect()
        {
            LogFile?.Add("@MQTT Connected: " + MQTT.BrokerAddress + ":" + MQTT.BrokerPort.ToString());
            Topics.Clear();
        } // OnMQTTConnect()

        /// <summary>
        /// Проверка и обработка сообщений, поступивших от брокера MQTT
        /// Команды управления:
        /// nooLite/ch[номер канала привязки]=>ON               - включить силовой блок
        /// nooLite/ch[номер канала привязки]=>OFF              - выключить силовой блок
        /// nooLite/ch[номер канала привязки]=>SWITCH           - переключить силовой блок (ВКЛ->ВЫКЛ / ВЫКЛ->ВКЛ)
        /// nooLite/ch[номер канала привязки]=>BIND             - привязать силовой блок к каналу
        /// nooLite/ch[номер канала привязки]=>CLEAR            - отвязать все силовые блоки от канала
        /// nooLite/ch[номер канала привязки]/BRIG=>[значение]  - установить яркость силового блока
        /// nooLite/ch[номер канала привязки]/TEMP=>[значение]  - временно включить силовой блок
        /// </summary>
        private static void CheckEventsMQTT()
        {
            while (true)
            {
                byte id;
                MQTT.Message message = MQTT.GetMessage();
                if (message is null)
                    break;
                //if (CheckMQTTServiceTopic(message)) continue;
                if (message.Topic.Substring(0, TopicPrefix.Length) != TopicPrefix)
                    continue;
                bool skip = true;
                foreach (string str in Topics)
                    if (message.Topic == str)
                        skip = false;
                if (skip)
                {
                    Topics.Add(message.Topic);
                    LogFile.Add(" Пропуск: " + message.Topic + "=>" + message.Data);
                    continue;
                }
                LogFile.Add(" Обработка: " + message.Topic + "=>" + message.Data);
                string topic = message.Topic.Substring(TopicPrefix.Length);
                if (topic.Substring(0, 2).ToLower() != "ch")
                    continue;
                string subtopic = "";
                int pos = topic.IndexOf("/");
                if (pos >= 0)
                {
                    subtopic = topic.Substring(pos);
                    topic = topic.Substring(0, pos);
                }
                if (!byte.TryParse(topic.Substring(2), out id))
                    continue;
                if (message.Data.ToUpper() == "BIND")
                {
                    Channels.Bind(id);
                    continue;
                }
                Channel channel = Channels.Search(id);
                if (channel != null)
                    switch (subtopic.ToUpper())
                    {
                        case "/BRIG":
                            if (!uint.TryParse(message.Data, out uint brightness))
                                brightness = 100;
                            brightness = 0xFF * (brightness > 100 ? 100 : brightness) / 100 << 24;
                            channel.SendCommand(nooLite.Command.SetBrightness, brightness);
                            break;
                        case "/TEMP":
                            if (!uint.TryParse(message.Data, out uint param))
                                param = 5;
                            param = (uint)Math.Floor((decimal)param / 5);
                            if ((param > 0) && (param < 256))
                            {
                                param = (param & 0x00FF) << 24;
                                nooLite.SendCommand(channel.Id, nooLite.Command.TemporaryOn, 0x05, param);
                            }
                            else if ((param > 255) && (param < 65535))
                            {
                                param = ((param & 0x00FF) << 24) | ((param & 0xFF00) << 8);
                                nooLite.SendCommand(channel.Id, nooLite.Command.TemporaryOn, 0x06, param);
                            }
                            break;
                        default:
                            switch (message.Data.ToUpper())
                            {
                                case "ON":
                                    channel.SendCommand(nooLite.Command.On);
                                    break;
                                case "OFF":
                                    channel.SendCommand(nooLite.Command.Off);
                                    break;
                                case "SWITCH":
                                    channel.SendCommand(nooLite.Command.Switch);
                                    break;
                                case "CLEAR":
                                    Channels.Delete(id);
                                    break;
                            }
                            break;
                    }
            }
        } // CheckEventsMQTT()

        /// <summary>
        /// Вывод отладочной информации о пакете данных nooLite
        /// При обработке исходящих и входящих сообщений адаптера nooLite MTRF-64 USB
        /// </summary>
        /// <param name="buffer"> пакет данных nooLite </param>
        private static void nooLiteCommandInfo(byte[] buffer)
        {
            if (!Debug || (buffer is null))
                return;
            string str = string.Empty;
            if (buffer[(byte)nooLite.Data.St] == 0xAB)
                str += "SEND = ";
            else if (buffer[(byte)nooLite.Data.St] == 0xAD)
                str += "READ = ";
            else
                return;
            for (int i = 0; i < buffer.Length; i++)
                str += buffer[i].ToString("X2") + " ";
            str += "| ch=" + buffer[(byte)nooLite.Data.Ch].ToString("00") + " " +
                (buffer[(byte)nooLite.Data.St] == 0xAB ? "<-" : "->");
            if (buffer[(byte)nooLite.Data.Ctr] == 0x05)
                str += " mode=Unbind";
            else
                switch (buffer[(byte)nooLite.Data.Cmd])
                {
                    case (byte)nooLite.Command.Off:
                        str += " cmd=Off";
                        break;
                    case (byte)nooLite.Command.BrightDown:
                        str += " cmd=BrightDown";
                        break;
                    case (byte)nooLite.Command.On:
                        str += " cmd=On";
                        break;
                    case (byte)nooLite.Command.BrightUp:
                        str += " cmd=BrightUp";
                        break;
                    case (byte)nooLite.Command.Switch:
                        str += " cmd=Switch";
                        break;
                    case (byte)nooLite.Command.BrightBack:
                        str += " cmd=BrightBack";
                        break;
                    case (byte)nooLite.Command.SetBrightness:
                        str += " cmd=SetBrightness: " +
                            (100 * buffer[(byte)nooLite.Data.D0] / 0xFF).ToString() + "%";
                        break;
                    case (byte)nooLite.Command.LoadPreset:
                        str += " cmd=LoadPreset";
                        break;
                    case (byte)nooLite.Command.SavePreset:
                        str += " cmd=SavePreset";
                        break;
                    case (byte)nooLite.Command.Unbind:
                        str += " cmd=Unbind";
                        break;
                    case (byte)nooLite.Command.BrightStepDown:
                        str += " cmd=BrightStepDown";
                        break;
                    case (byte)nooLite.Command.BrightStepUp:
                        str += " cmd=BrightStepUp";
                        break;
                    case (byte)nooLite.Command.BrightReg:
                        str += " cmd=BrightReg";
                        break;
                    case (byte)nooLite.Command.Bind:
                        str += " cmd=Bind";
                        break;
                    case (byte)nooLite.Command.SwitchMode:
                        str += " cmd=SwitchMode";
                        break;
                    case (byte)nooLite.Command.TemporaryOn:
                        str += " cmd=TemporaryOn";
                        break;
                    case (byte)nooLite.Command.ReadState:
                        str += " cmd=ReadState";
                        break;
                    case (byte)nooLite.Command.WriteState:
                        str += " cmd=WriteState: " +
                            (buffer[(byte)nooLite.Data.D2] > 0 ? "ON" : "OFF");
                        break;
                    case (byte)nooLite.Command.SendState:
                        str += " cmd=SendState: " +
                            (buffer[(byte)nooLite.Data.D2] > 0 ? "ON" : "OFF") +
                            " (" + (100 * buffer[(byte)nooLite.Data.D3] / 0xFF).ToString() + "%)";
                        break;
                    case (byte)nooLite.Command.Service:
                        str += " cmd=Service";
                        break;
                }
            LogFile?.Add("@" + str);
        } // Debug_CommandInfo([] buffer)
    } // class Server
} // namespace nooLite2MQTT