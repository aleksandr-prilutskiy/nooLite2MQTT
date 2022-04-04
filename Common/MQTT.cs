using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Common
{
    /// <summary>
    /// Объект для работы с брокером MQTT с использованием буфера принятых сообщений
    /// Версия от 04.04.2022
    /// </summary>
    public class MQTT
    {
        /// <summary>
        /// Структура для хранения полученных сообщений
        /// </summary>
        public class Message
        {
            public string Topic;                     // Имя топика
            public string Data;                      // Передавемые данные
        } // class Message

        public string BrokerAddress = "";            // Адрес хоста брокера MQTT
        public int BrokerPort = 1883;                // Порт брокера MQTT
        public string UserName;                      // Имя пользователя для подключения к брокеру MQTT
        public string Password;                      // Пароль пользователя для подключения к брокеру MQTT
        private const uint Reconnect = 10000;        // Период переподключения к брокеру
        public uint Timeout = 1000;                  // Таймаут отправки сообщений брокеру MQTT
        private static MqttClient _client;           // Объект для работы с брокером MQTT
        private static string _clientId;             // Идентификатор клиента, подключаемого к брокеру MQTT
        private Thread _handler = null;              // Поток для поддержания подключения к брокеру MQTT
        public List<string> Topics;                  // Список топиков, но которые нужна подписка
        private List<Message> _messages;             // Буфер принятых сообщений
        private readonly LogFile _fileLog = null;    // Ссылка на объект - журнал работы приложения
        private bool _busy = false;                  // Признак ожидания ответа на отправленное сообщение
        public delegate void MessageHandler(string topic, string message);
        public MessageHandler OnMessageSend = Skip;  // Обработчик отправленных сообщений
        public MessageHandler OnMessageRead = Skip;  // Обработчик принятых сообщений
        public delegate void ConnectHandler();
        public ConnectHandler OnConnect = Skip;      // Обработчик подключения к брокеру MQTT
        public ConnectHandler OnDisconnect = Skip;   // Обработчик отключения от брокера MQTT

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="fileLog"> ссылка на оъект для работы с файлом журнала </param>
        public MQTT(LogFile fileLog = null)
        {
            _fileLog = fileLog;
            _client = null;
            _clientId = "nooLite2MQTT: " + Guid.NewGuid().ToString("N");
            _messages = new List<Message>();
            Topics = new List<string>();
            Topics.Add("#");
        } // MQTT(LogFile)

        /// <summary>
        /// Чтение настроек доступа к базе данных из файла настроек
        /// </summary>
        /// <param name="iniFile"> объект-файл конфигурации программы </param>
        public void ReadConfig(IniFile iniFile)
        {
            BrokerAddress = iniFile.ReadString("MQTT", "Host", "");
            BrokerPort = iniFile.ReadInt("MQTT", "Port", 1883);
            UserName = iniFile.ReadString("MQTT", "User", "");
            Password = iniFile.ReadPassword("MQTT", "Password", "");
        } // ReadConfig(IniFile)

        /// <summary>
        /// Запуск потока для поддержания подключения к брокеру MQTT
        /// </summary>
        public void Start(CancellationToken cancelToken)
        {
            _handler = new Thread(() => Handler(this, cancelToken));
            _handler.Start();
        } // Start()

        /// <summary>
        /// Обработчик потока для поддержания подключения к брокеру MQTT
        /// </summary>
        /// <param name="MQTT"> ссылка на объект для работы с брокером MQTT </param>
        /// <param name="cancelToken"> токен для завершения потока </param>
        private static void Handler(MQTT MQTT, CancellationToken cancelToken)
        {
            DateTime timer = DateTime.Now;
            while (!cancelToken.IsCancellationRequested)
            {
                if ((DateTime.Now >= timer) && (_client == null))
                    try
                    {
                        _client = new MqttClient(MQTT.BrokerAddress, MQTT.BrokerPort,
                            false, null, null, MqttSslProtocols.None);
                        _client.MqttMsgPublished += MQTT.MessagePublished;
                        _client.MqttMsgPublishReceived += MQTT.MessageReceived;
                        _client?.Connect(_clientId, MQTT.UserName, MQTT.Password);
                        MQTT._fileLog?.Add("@Установлено подключение к брокеру MQTT: " +
                                MQTT.BrokerAddress + ":" + MQTT.BrokerPort.ToString());
                        foreach (string topic in MQTT.Topics) MQTT.Subscribe(topic);
                        MQTT.OnConnect();
                    }
                    catch (Exception exception)
                    {
                        MQTT._fileLog?.Add("@Ошибка MQTT: " + exception.Message);
                        _client = null;
                        timer = DateTime.Now.AddMilliseconds(Reconnect);
                        continue;
                    }
                if (!_client.IsConnected)
                {
                    MQTT._fileLog?.Add("@Ошибка MQTT: Потеряно соединение с брокером");
                    MQTT.OnDisconnect();
                    _client = null;
                    timer = DateTime.Now.AddMilliseconds(Reconnect);
                    continue;
                }
                Thread.Sleep(500);
            }
        } // Handler(MQTT, CancellationToken)

        /// <summary>
        /// Подписка на топик
        /// '#' - для подписки на все топики
        /// </summary>
        /// <param name="topics"> имя топика или список топиков через запятую (',') </param>
        public void Subscribe(string topics)
        {
            if ((topics != "") && (_client?.IsConnected == true))
                _client.Subscribe(new[] { topics }, new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        } // Subscribe(string)

        /// <summary>
        /// Отмена подписки на топик
        /// </summary>
        /// <param name="topics"> имя топика или список топиков через запятую (',') </param>
        public void Unsubscribe(string topics)
        {
            if ((topics != "") && (_client?.IsConnected == true))
                _client.Unsubscribe(new[] { topics });
        } // Unsubscribe(string)

        /// <summary>
        /// Публикация сообщения MQTT
        /// </summary>
        /// <param name="topic"> имя топика </param>
        /// <param name="message"> строка с текстом сообщения </param>
        /// <param name="retain"> флаг Retain </param>
        public void Publish(string topic, string message, bool retain = false)
        {
            if ((_client is null) || (_client?.IsConnected != true)) return;
            BusyWait();
            _busy = true;
            OnMessageSend(topic, message);
            if (topic == "") return;
            _client?.Publish(topic, Encoding.UTF8.GetBytes(message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, retain);
        } // Publish(string, string)

        /// <summary>
        /// Обработка подтвержения отправки сообщения брокеру
        /// </summary>
        private void MessagePublished(object sender, MqttMsgPublishedEventArgs e)
        {
            _busy = false;
        } // MessagePublished(sender, e)

        /// <summary>
        /// Обработка сообщения по подписке от брокера MQTT (по подписке)
        /// </summary>
        private void MessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            Message message = new Message()
            {
                Topic = e.Topic,
                Data = Encoding.UTF8.GetString(e.Message)
            };
            _messages.Add(message);
            OnMessageRead(message.Topic, message.Data);
        } // MessageReceived(sender, e)

        /// <summary>
        /// Получение сообщения из буфера принятых сообщений
        /// После успешного чтения прочитанное сообщение удаляется из буфера
        /// Также удаляются все сообщения этого топика
        /// Возвращается только значение последненго полученого сообщения этого топика
        /// </summary>
        /// <returns>
        /// Success: текст сообщения из буфера, которое было принято раньше других
        /// Failure: null - если сообщений нет
        /// </returns>
        public Message GetMessage()
        {
            if (_messages.Count == 0) return null;
            Message message = new Message()
            {
                Topic = _messages[0].Topic,
                Data = _messages[0].Data
            };
            _messages.RemoveAt(0);
            for (int i = 0; i < _messages.Count; i++)
                while ((i < _messages.Count) && (_messages[i].Topic == message.Topic))
                {
                    message.Data = _messages[i].Data;
                    _messages.RemoveAt(i);
                }
            return message;
        } // GetMessage(string)

        /// <summary>
        /// Удаление всех сообщений из буфера принятых сообщений
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
        } // ClearMessages()

        /// <summary>
        /// Загрушка функций обработки отправки и получения сообщения, подключения к брокеру
        /// </summary>
        /// <param name="topic"> имя топика </param>
        /// <param name="message"> строка с текстом сообщения </param>
        private static void Skip(string topic, string message) { }

        /// <summary>
        /// Загрушка функций обработки отправки и получения сообщения, подключения к брокеру
        /// </summary>
        private static void Skip() { }

        /// <summary>
        /// Ожидание обработки операции
        /// </summary>
        private void BusyWait()
        {
            DateTime timer = DateTime.Now.AddMilliseconds(Timeout);
            while (_busy)
            {
                if (DateTime.Now > timer)
                {
                    _fileLog?.Add("@Ошибка: превешение времени ожидания");
                    _busy = false;
                    return;
                }
                Thread.Sleep(100);
            }
        } // BusyWait()

        /// <summary>
        /// Отключение от брокера MQTT
        /// </summary>
        public void Disconnect()
        {
            if (_client is null) return;
            BusyWait();
            if (_handler != null)
                while (_handler.IsAlive) Thread.Sleep(100);
            _client.Disconnect();
            _client = null;
            _messages.Clear();
            _fileLog?.Add("@Подключение к брокеру MQTT разорвано");
        } // Disconnect()

    } // class MQTT
} // namespace Common