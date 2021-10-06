using System;
using System.Collections.Generic;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Common
{
//===============================================================================================================
//
// Объект для работы с брокером MQTT
// Версия от 19.09.2021
//
//===============================================================================================================
    public class MQTT
    {
        public class Message                         // Структура полученных сообщений
        {
            public string Topic;                     // Имя топика
            public string Data;                      // Передавемые данные
        } // class Message

        public string BrokerAddress = "";            // Адрес хоста брокера MQTT
        public int BrokerPort = 1883;                // Порт брокера MQTT
        public string UserName;                      // Имя пользователя для подключения к брокеру MQTT
        public string Password;                      // Пароль пользователя для подключения к брокеру MQTT
        private static string _clientId;             // Идентификатор клиента, подключаемого к брокеру MQTT
        private readonly LogFile _fileLog = null;    // Ссылка на объект - журнал работы приложения
        private static MqttClient _client;           // Объект для работы с брокером MQTT
        private List<Message> Messages;              // Буфер принятых сообщений
        private bool _busy = false;                  // Признак ожидания ответа на отправленное сообщение

        public delegate void MessageHandler(string topic, string message);
        public MessageHandler OnMessageSend = Skip;  // Обработчик отправленных сообщений
        public MessageHandler OnMessageRead = Skip;  // Обработчик принятых сообщений

        public bool Connected                        // Признак успешного подключения к брокеру MQTT
        {
            get { return _client?.IsConnected == true; }
        } // Connected
        public bool IsMessageReceived                // Проверка наличия сообщений в буфере принятых сообщений
        {
            get { return Messages.Count > 0; }
        } // MessageReceived

//===============================================================================================================
// Name...........:	MQTT
// Description....:	Инициализация объекта
// Syntax.........:	new MQTT()
//===============================================================================================================
        public MQTT()
        {
            Init();
        } // MQTT()

//===============================================================================================================
// Name...........:	MQTT
// Description....:	Инициализация объекта с привязкой файла журнала
// Syntax.........:	new MQTT(fileLog)
// Parameters.....:	fileLog     - ссылка на оъект для работы с файлом журнала
//===============================================================================================================
        public MQTT(LogFile fileLog)
        {
            _fileLog = fileLog;
            Init();
        } // MQTT(LogFile)

//===============================================================================================================
// Name...........:	Init
// Description....:	Начальная установка объекта
// Syntax.........:	Init()
//===============================================================================================================
        private void Init()
        {
            _client = null;
            _clientId = "nooLite2MQTT: " + Guid.NewGuid().ToString("N");
            Messages = new List<Message>();
            //_reconnectTimer = DateTime.Now;
        } // Init()

//===============================================================================================================
// Name...........:	ReadConfig
// Description....:	Чтение настроек доступа к базе данных из файла настроек
// Syntax.........:	ReadConfig(iniFile)
// Parameters.....:	iniFile     - объект-файл конфигурации программы
//===============================================================================================================
        public void ReadConfig(IniFile iniFile)
        {
            BrokerAddress = iniFile.ReadString("MQTT", "Host", "");
            BrokerPort = iniFile.ReadInt("MQTT", "Port", 1883);
            UserName = iniFile.ReadString("MQTT", "User", "");
            Password = iniFile.ReadPassword("MQTT", "Password", "");
        } // ReadConfig(IniFile)

//===============================================================================================================
// Name...........:	Connect
// Description....:	Подключение к брокеру MQTT
// Syntax.........:	Connect()
//===============================================================================================================
        public void Connect()
        {
            if (BrokerAddress == "") return;
            if (_client == null)
            {
                //if (DateTime.Now.Subtract(_reconnectTimer).TotalMilliseconds >= _reconnectTime) return;
                //_reconnectTimer = _reconnectTimer.AddMilliseconds(_reconnectTime);
                try
                {
                    _client = new MqttClient(BrokerAddress, BrokerPort, false, null, null, MqttSslProtocols.None);
                    _client.MqttMsgPublished += MessagePublished;
                    _client.MqttMsgPublishReceived += MessageReceived;
                }
                catch (Exception)
                {
                    _client = null;
                }
            }
            if (_client?.IsConnected == true) return;
            try
            {
                _client?.Connect(_clientId, UserName, Password);
            }
            catch (Exception)
            {
                _client = null;
            }
            if (_client != null)
                _fileLog?.Add("@Установлено подключение к брокеру MQTT: " +
                    BrokerAddress + ":" + BrokerPort.ToString());
            else
                _fileLog?.Add("@Ошибка подключения к брокеру MQTT");
        } // Connect()

//===============================================================================================================
// Name...........:	Subscribe
// Description....:	Подписка на топик
// Syntax.........:	Subscribe(topics)
// Parameters.....:	topics      - имя топика или список топиков через запятую (',')
// Remarks .......:	'#' - для подписки на все топики
//===============================================================================================================
        public void Subscribe(string topics)
        {
            if ((topics != "") && (_client?.IsConnected == true))
                _client.Subscribe(new[] { topics }, new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        } // Subscribe(string)

//===============================================================================================================
// Name...........:	Unsubscribe
// Description....:	Отмена подписки на топик
// Syntax.........:	Subscribe(topics)
// Parameters.....:	topics      - имя топика или список топиков через запятую (',')
//===============================================================================================================
        public void Unsubscribe(string topics)
        {
            if ((topics != "") && (_client?.IsConnected == true))
                _client.Unsubscribe(new[] { topics });
        } // Unsubscribe(string)

//===============================================================================================================
// Name...........:	MessageSend
// Description....:	Публикация сообщения MQTT
// Syntax.........:	MessageSend(topic, message)
// Parameters.....:	topic       - имя топика
//                  message     - строка с текстом сообщения
//===============================================================================================================
        public void MessageSend(string topic, string message)
        {
            MessageSend(topic, message, false);
        } // MessageSend(string, string)

//===============================================================================================================
// Name...........:	MessageSend
// Description....:	Публикация сообщения MQTT
// Syntax.........:	MessageSend(topic, message)
// Parameters.....:	topic       - имя топика
//                  message     - строка с текстом сообщения
//                  retain      - флаг Retain
//===============================================================================================================
        public void MessageSend(string topic, string message, bool retain)
        {
            if (_client?.IsConnected != true) return;
            while (_busy) { }
            _busy = true;
            OnMessageSend(topic, message);
            if (topic == "") return;
            _client?.Publish(topic, Encoding.UTF8.GetBytes(message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, retain);
        } // MessageSend(string, string)

//===============================================================================================================
// Name...........:	MessagePublished
// Description....:	Обработка подтвержения отправки сообщения брокеру
//===============================================================================================================
        private void MessagePublished(object sender, MqttMsgPublishedEventArgs e)
        {
            _busy = false;
        } // MessagePublished(sender, e)

//===============================================================================================================
// Name...........:	MessageReceive
// Description....:	Обработка сообщения по подписке от брокера MQTT (по подписке)
//===============================================================================================================
        private void MessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            Message message = new Message()
            {
                Topic = e.Topic,
                Data = Encoding.UTF8.GetString(e.Message)
            };
            OnMessageRead(message.Topic, message.Data);
            Messages.Add(message);
        } // MessageReceived(sender, e)

//===============================================================================================================
// Name...........:	GetMessage
// Description....:	Получение сообщения из буфера принятых сообщений
// Syntax.........:	GetMessage()
// Return value(s):	Success:    - текст сообщения из буфера, которое было принято раньше других
//                  Failure:    - null
// Remarks .......:	После успешного чтения сообщение удаляется из буфера
//                  Также удаляются все сообщения этого топика
//                  Возвращается значение последненго полученого сообщения этого топика
//===============================================================================================================
        public Message GetMessage()
        {
            if (Messages.Count == 0) return null;
            Message message = new Message()
            {
                Topic = Messages[0].Topic,
                Data = Messages[0].Data
            };
            Messages.RemoveAt(0);
            for (int i = 0; i < Messages.Count; i++)
                while ((i < Messages.Count) && (Messages[i].Topic == message.Topic))
                {
                    message.Data = Messages[i].Data;
                    Messages.RemoveAt(i);
                }
            return message;
        } // GetMessage(string)

//===============================================================================================================
// Name...........:	ClearMessages
// Description....:	Удаление всех сообщений из буфера принятых сообщений
// Syntax.........:	ClearMessages()
//===============================================================================================================
        public void ClearMessages()
        {
            Messages.Clear();
        } // ClearMessages()

//===============================================================================================================
// Name...........:	Skip
// Description....:	Загрушка функции обработки отправки и получения сообщения
//===============================================================================================================
        private static void Skip(string topic, string message) { }

//===============================================================================================================
// Name...........:	Disconnect
// Description....:	Отключение от брокера MQTT
// Syntax.........:	Disconnect()
//===============================================================================================================
        public void Disconnect()
        {
            if (_client?.IsConnected != true) return;
            while (_busy) { }
            _client.Disconnect();
            _client = null;
            Messages.Clear();
        } // Disconnect()
    } // class MQTT
}