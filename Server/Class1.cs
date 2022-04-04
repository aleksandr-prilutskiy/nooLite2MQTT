using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nooLite2MQTT
{
    public partial class Server
    {
        //private const string _topicService = "Service";       // Имя топика управления сервисом
        //private static bool _assist = false;                  // Режим контроля срабатывания включения силовых блоков
        //public static Thread Thread;

        //_assist = IniFile.ReadString("Service", "Assist", "") == "On";

        /// <summary>
        /// Обработка сообщений с топиками управления сервисом
        /// Специальные команды (топик - сообщение: описание):
        /// nooLite/Service - GetState: Запрос состояния устройств
        /// nooLite/Service - GetState - [All или номер канала привязки]: Запрос состояния устройств
        /// nooLite/Service - Restart: Перезапуск сервиса
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        //private bool CheckMQTTServiceTopic(MQTT.Message message)
        //{
        //    if (message.Topic != TopicPrefix + _topicService) return false;
        //    switch (message.Data.ToLower())
        //    {
        //        //case "/GETSTATE":
        //        //    if (message.Data.ToUpper() == "ALL")
        //        //    {
        //        //        string jsonstr = "[";
        //        //        foreach (Channel channel in Channels)
        //        //            if (channel.Devices.Count > 0)
        //        //                jsonstr += (jsonstr.Length > 1 ? "," : "") +
        //        //                    JsonSerializer.Serialize(channel);
        //        //        MQTT.MessageSend(_topicService + "/State", jsonstr + "]");
        //        //    }
        //        //    else if (byte.TryParse(message.Data, out byte id))
        //        //        MQTT.MessageSend(_topicService + "/State",
        //        //            JsonSerializer.Serialize(ChannelSearch(id)));
        //        //    break;
        //        case "restart":
        //            LogFile.Add("@Перезапуск сервиса");
        //            OnStop();
        //            OnStart();
        //            return true;
        //    }
        //    return false;
        //} // CheckMQTTServiceTopic(MQTT.Message)

        /// <summary>
        /// Ожидание подключени к адаптеру nooLite MTRF-64 USB и настройки после подклчения
        /// </summary>
        //private static void Init_nooLite()
        //{
        //nooLite.Connect();
        //while (!nooLite.Connected)
        //{
        //    if (!Run) return;
        //    Thread.Sleep(500);
        //}
        //Channels.Clear();
        //ScanChannels();
        //LoadSensors(IniFile);
        //ChannelsToLog();
        //LogFile?.Save();
        //nooLiteScanTimer = DateTime.Now.AddMilliseconds(_nooLite_Update);
        //} // Init_nooLite()

        /// <summary>
        /// Проверка и обработка оключения адаптера nooLite MTRF-64 USB
        /// </summary>
        //private static void CheckConnection()
        //{
        //    return;
        //    //if (nooLite_TryConnect.Status == TaskStatus.Running) return;
        //    //if (nooLite.Connected) return;
        //    //LogFile?.Add("@Ошибка: Потеряно соединение с модулем nooLite MTRF-64-USB");
        //    //new Task(Init_nooLite).Start();
        //} // CheckConnection()

    }
}
