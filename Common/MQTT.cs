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
    /// </summary>
    /// Версия от 07.01.2023
    public class MQTT
    {
        /// <summary>
        /// Структура для хранения полученных сообщений
        /// </summary>
        public class Message
        {
            /// <summary>
            /// Имя топика
            /// </summary>
            public string Topic;

            /// <summary>
            /// Передавемые данные
            /// </summary>
            public string Data;
        } // class Message

        /// <summary>
        /// Адрес хоста брокера MQTT
        /// </summary>
        public string BrokerAddress = string.Empty;

        /// <summary>
        /// Порт брокера MQTT
        /// </summary>
        public int BrokerPort = 1883;

        /// <summary>
        /// Имя пользователя для подключения к брокеру MQTT
        /// </summary>
        public string UserName;

        /// <summary>
        /// Пароль пользователя для подключения к брокеру MQTT
        /// </summary>
        public string Password;

        /// <summary>
        /// Список топиков, на которые нужна подписка
        /// </summary>
        public List<string> Topics;

        /// <summary>
        /// Таймаут отправки сообщений брокеру MQTT (в миллисекундах)
        /// </summary>
        public uint Timeout = 1000;

        /// <summary>
        /// Объект-клиент для работы с брокером MQTT
        /// </summary>
        private static MqttClient _client;

        /// <summary>
        /// Идентификатор клиента, подключаемого к брокеру MQTT
        /// </summary>
        private static string _clientId;

        /// <summary>
        /// Период переподключения к брокеру (в миллисекундах)
        /// </summary>
        private const uint _reconnect = 10000;

        /// <summary>
        /// Поток для поддержания подключения к брокеру MQTT
        /// </summary>
        private Thread _thread;

        /// <summary>
        /// Буфер принятых сообщений
        /// </summary>
        private List<Message> _messages;

        /// <summary>
        /// Ссылка на объект - журнал работы приложения
        /// </summary>
        private readonly LogFile _fileLog;

        /// <summary>
        /// Признак ожидания ответа на отправленное сообщение
        /// </summary>
        private bool _busy = false;

        /// <summary>
        /// Результат проверки подключения к брокеру
        /// </summary>
        private bool _testResult = false;

        /// <summary>
        /// Обработчик отправленных сообщений
        /// </summary>
        public MessageHandler OnMessageSend = Skip;

        /// <summary>
        /// Обработчик принятых сообщений
        /// </summary>
        public MessageHandler OnMessageRead = Skip;

        /// <summary>
        /// Обработчик подключения к брокеру MQTT
        /// </summary>
        public ConnectHandler OnConnect = Skip;

        /// <summary>
        /// Обработчик отключения от брокера MQTT
        /// </summary>
        public ConnectHandler OnDisconnect = Skip;

        public delegate void MessageHandler(string topic, string message);
        public delegate void ConnectHandler();

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="fileLog"> [необязательный] ссылка на оъект для работы с файлом журнала </param>
        public MQTT(LogFile fileLog = null)
        {
            _fileLog = fileLog;
            _thread = null;
            _client = null;
            _clientId = "nooLite2MQTT: " + Guid.NewGuid().ToString("N");
            _messages = new List<Message>();
            Topics = new List<string>();
            Topics.Add("#");
        } // MQTT([LogFile])

        /// <summary>
        /// Чтение настроек доступа к базе данных из файла настроек
        /// </summary>
        /// <param name="iniFile"> объект-файл конфигурации программы </param>
        public void ReadConfig(IniFile iniFile)
        {
            BrokerAddress = iniFile.ReadString("MQTT", "Host");
            BrokerPort = iniFile.ReadInt("MQTT", "Port", 1883);
            UserName = iniFile.ReadString("MQTT", "User");
            Password = iniFile.ReadPassword("MQTT", "Password");
        } // ReadConfig(IniFile)

        /// <summary>
        /// Проверка подключения к брокеру MQTT
        /// </summary>
        /// <returns> true - если проверка прошла успешно </returns>
        public bool Test()
        {
            _testResult = false;
            try
            {
                _client = new MqttClient(BrokerAddress, BrokerPort, false, null, null, MqttSslProtocols.None);
                _client.MqttMsgPublishReceived += MessageReceivedTest;
                _client?.Connect(_clientId, UserName, Password);
                Subscribe("#");
                OnConnect();
            }
            catch { }
            DateTime timeout = DateTime.Now.AddSeconds(10);
            while (!_testResult)
            {
                if (DateTime.Now > timeout)
                    break;
                Thread.Sleep(250);
            }
            Disconnect();
            return _testResult;
        } // Test()

        /// <summary>
        /// Запуск потока для поддержания подключения к брокеру MQTT
        /// </summary>
        /// <param name="token"> токен для завершения потока </param>
        public void Start(CancellationToken token)
        {
            _thread = new Thread(() => Handler(this, token));
            _thread.Start();
        } // Start(CancellationToken)

        /// <summary>
        /// Обработчик потока для поддержания подключения к брокеру MQTT
        /// </summary>
        /// <param name="MQTT"> ссылка на объект для работы с брокером MQTT </param>
        /// <param name="token"> токен для завершения потока </param>
        private static void Handler(MQTT MQTT, CancellationToken token)
        {
            DateTime timer = DateTime.Now;
            while (!token.IsCancellationRequested)
            {
                if ((DateTime.Now > timer) && (_client is null))
                    try
                    {
                        _client = new MqttClient(MQTT.BrokerAddress, MQTT.BrokerPort,
                            false, null, null, MqttSslProtocols.None);
                        _client.MqttMsgPublished += MQTT.MessagePublished;
                        _client.MqttMsgPublishReceived += MQTT.MessageReceived;
                        _client?.Connect(_clientId, MQTT.UserName, MQTT.Password);
                        MQTT._fileLog?.Add("@Установлено подключение к брокеру MQTT: " +
                                MQTT.BrokerAddress + ":" + MQTT.BrokerPort.ToString());
                        foreach (string topic in MQTT.Topics)
                            MQTT.Subscribe(topic);
                        MQTT.OnConnect();
                    }
                    catch (Exception exception)
                    {
                        MQTT._fileLog?.Add("@Ошибка MQTT: " + exception.Message);
                        _client = null;
                        timer = DateTime.Now.AddMilliseconds(_reconnect);
                        continue;
                    }
                if ((_client != null) && (!_client.IsConnected))
                {
                    MQTT._fileLog?.Add("@Ошибка MQTT: Потеряно соединение с брокером");
                    MQTT.OnDisconnect();
                    _client = null;
                    timer = DateTime.Now.AddMilliseconds(_reconnect);
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
            if ((!string.IsNullOrEmpty(topics)) &&
                (_client?.IsConnected == true))
                _client.Subscribe(new[] { topics }, new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        } // Subscribe(string)

        /// <summary>
        /// Отмена подписки на топик
        /// </summary>
        /// <param name="topics"> имя топика или список топиков через запятую (',') </param>
        public void Unsubscribe(string topics)
        {
            if ((!string.IsNullOrEmpty(topics)) &&
                (_client?.IsConnected is true))
                _client.Unsubscribe(new[] { topics });
        } // Unsubscribe(string)

        /// <summary>
        /// Публикация сообщения MQTT
        /// </summary>
        /// <param name="topic"> имя топика </param>
        /// <param name="message"> строка с текстом сообщения </param>
        /// <param name="retain"> [необязательный] флаг Retain </param>
        public void Publish(string topic, string message, bool retain = false)
        {
            if ((_client is null) || (_client?.IsConnected != true))
                return;
            BusyWait();
            _busy = true;
            OnMessageSend(topic, message);
            if (string.IsNullOrEmpty(topic))
                return;
            _client?.Publish(topic, Encoding.UTF8.GetBytes(message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, retain);
        } // Publish(string, string, [bool])

        /// <summary>
        /// Обработка подтвержения отправки сообщения брокеру
        /// </summary>
        private void MessagePublished(object sender, MqttMsgPublishedEventArgs e)
        {
            _busy = false;
        } // MessagePublished(object, MqttMsgPublishedEventArgs)

        /// <summary>
        /// Обработка сообщения по подписке от брокера MQTT
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
        } // MessageReceived(object, MqttMsgPublishEventArgs)

        /// <summary>
        /// Тестовая обработка сообщения по подписке от брокера MQTT
        /// </summary>
        private void MessageReceivedTest(object sender, MqttMsgPublishEventArgs e)
        {
            _testResult = true;
        } // MessageReceivedTest(object, MqttMsgPublishEventArgs)

        /// <summary>
        /// Получение сообщения из буфера принятых сообщений
        /// </summary>
        /// После успешного чтения прочитанное сообщение удаляется из буфера
        /// Также удаляются все сообщения этого топика
        /// Возвращается только значение последненго полученого сообщения этого топика
        /// <returns> Текст сообщения из буфера, которое было принято раньше других или null </returns>
        public Message GetMessage()
        {
            if (_messages.Count == 0)
                return null;
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
        } // GetMessage()

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
                    _fileLog?.Add("@Ошибка: превышено время ожидания");
                    _busy = false;
                    return;
                }
                Thread.Sleep(250);
            }
        } // BusyWait()

        /// <summary>
        /// Отключение от брокера MQTT
        /// </summary>
        public void Disconnect()
        {
            if (_client is null)
                return;
            BusyWait();
            DateTime timeout = DateTime.Now.AddSeconds(60);
            if (_thread != null)
                while (_thread.IsAlive)
                {
                    if (DateTime.Now > timeout)
                        break;
                    Thread.Sleep(100);
                }
            _client.Disconnect();
            _client = null;
            _messages.Clear();
            _fileLog?.Add("@Подключение к брокеру MQTT разорвано");
        } // Disconnect()
    } // class MQTT
} // namespace Common