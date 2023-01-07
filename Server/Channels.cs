using Common;
using System.Collections.Generic;
using System.Threading;

namespace nooLite2MQTT
{
    /// <summary>
    /// Сервис сопряжения устройств nooLite c протоколом MQTT
    /// Объект и функции для работы с каналами адаптера MTRF-64-USB
    /// Версия от 06.01.2023
    /// </summary>
    public class Channels
    {
        /// <summary>
        /// Ссылка на объект - журнал работы приложения
        /// </summary>
        private readonly LogFile _fileLog = null;

        /// <summary>
        /// Ссылка на объект для работы с брокером MQTT
        /// </summary>
        private readonly MQTT _MQTT;

        /// <summary>
        /// Ссылка на объект для работы с адаптером nooLite MTRF-64-USB
        /// </summary>
        private readonly nooLite _nooLite;

        /// <summary>
        /// Список каналов привязки устройств nooLite
        /// </summary>
        private List<Channel> _channels;

        //private static DateTime nooLiteScanTimer;    // Таймер опроса состояния устройств nooLite
        //private const uint _nooLite_Update = 60000;  // Пероид таймера опроса состояния устройств nooLite

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="MQTT"> объект для работы с брокером MQTT </param>
        /// <param name="nooLite"> объект для работы с адаптером nooLite MTRF-64-USB </param>
        /// <param name="fileLog"> [необязательный] ссылка на оъект для работы с файлом журнала </param>
        public Channels(MQTT MQTT, nooLite nooLite, LogFile fileLog = null)
        {
            _MQTT = MQTT;
            _nooLite = nooLite;
            _fileLog = fileLog;
            _channels = new List<Channel>();
        } // nooLite(LogFile)

        /// <summary>
        /// Поиск канала по номеру
        /// </summary>
        /// <param name="id"> номер канала </param>
        /// <returns> Соотвествующий канал привязки устройств nooLite или null </returns>
        public Channel Search(byte id)
        {
            foreach (Channel channel in _channels)
                if (channel.Id == id) return channel;
            return null;
        } // Search(byte)

        /// <summary>
        /// Загрузка настроек каналов  nooLite из файла конфигурации
        /// </summary>
        /// <param name="IniFile"> ссылка на объект для работы с файлом конфигурации программы </param>
        public void Load(IniFile IniFile)
        {
            _channels.Clear();
            Channel channel;
            for (byte id = 1; id < nooLite.ChannelCount; id++)
            {
                string section = "Channel#" + id.ToString();
                string type = IniFile.ReadString(section, "Type", "");
                if (type.Length == 0)
                    continue;
                switch (type.ToLower())
                {
                    case "device":
                        channel = new Channel(id);
                        channel.Devices = new List<Device>();
                        _channels.Add(channel);
                        break;
                    case "switch":
                        channel = new Channel(id);
                        channel.Sensors = new List<Sensor>();
                        Sensor sensor = new Sensor()
                        {
                            Type = Sensor.SensorType.Switch,
                            Topic = "",
                            Value = 0,
                            Links = null
                        };
                        string links = IniFile.ReadString(section, "Links", "");
                        if (links.Length > 0)
                        {
                            string[] items = links.Split(',');
                            if (items.Length > 0)
                            {
                                sensor.Links = new byte[items.Length];
                                for (int j = 0; j < items.Length; j++)
                                    byte.TryParse(items[j], out sensor.Links[j]);
                            }
                        }
                        channel.Sensors.Add(sensor);
                        _channels.Add(channel);
                        break;
                    case "door":
                        channel = new Channel(id);
                        channel.Sensors = new List<Sensor>();
                        channel.Sensors.Add(new Sensor()
                        {
                            Type = Sensor.SensorType.Door,
                            Topic = IniFile.ReadString(section, "Topic", ""),
                            Value = 0,
                            Links = null
                        });
                        _channels.Add(channel);
                        break;
                }
            }
        } // LoadSensors()

        /// <summary>
        /// Запуск потока для поддержания подключения к брокеру MQTT
        /// </summary>
        /// <param name="cancelToken"> токен для завершения потока </param>
        public void Start(CancellationToken cancelToken)
        {
            Thread _handler = new Thread(() => Handler(this, cancelToken));
            _handler.Start();
        } // Start(CancellationToken)

        /// <summary>
        /// Обработчик потока
        /// </summary>
        /// <param name="Channels"> список каналов </param>
        /// <param name="cancelToken"> токен для завершения потока </param>
        private static void Handler(Channels Channels, CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                Thread.Sleep(500);
                byte[] buffer = Channels._nooLite.GetMessage();
                if (buffer is null)
                    continue;
                byte command = buffer[(byte)nooLite.Data.Cmd];
                Channel channel = Channels.Search(buffer[(byte)nooLite.Data.Ch]);
                if ((channel is null) || (channel.Sensors is null))
                    continue;
                foreach (Sensor sensor in channel.Sensors)
                    switch (sensor.Type)
                    {
                        case Sensor.SensorType.Door: // Датчик открытия/закрытия двери
                            float value = (command == (byte)nooLite.Command.On) ? 1 : 0;
                            Channels._fileLog.Add("@Sensor in #" + channel.Id.ToString() + " = " + value.ToString());
                            if (value != sensor.Value)
                            {
                                sensor.Value = value;
                                if (sensor.Topic != "")
                                    Channels._MQTT.Publish(sensor.Topic, value.ToString(), true);
                            }
                            break;
                        case Sensor.SensorType.Switch: // Выключатель
                            string state = (command == (byte)nooLite.Command.On) ? "ON" : "OFF";
                            Channels._fileLog?.Add("@Sensor in #" + channel.Id.ToString() + " = " + state);
                            foreach (byte index in sensor.Links)
                            {
                                Channel control = Channels.Search(index);
                                if (control != null) control.SendCommand((nooLite.Command)command);
                            }
                            break;
                    }
            }
        } // Handler()

        /// <summary>
        /// Привязка исполнительного устройства nooLite к каналу адаптера MTRF-64-USB
        /// </summary>
        /// <param name="id"> номер канала </param>
        public void Bind(byte id)
        {
            if (id >= nooLite.ChannelCount)
                return;
            List<byte[]> read = _nooLite.SendCommand(id, nooLite.Command.Bind);
            if (read is null)
                return;
            foreach (byte[] package in read)
            {
                if (package[(byte)nooLite.Data.Cmd] != (byte)nooLite.Command.SendState)
                    return;
                Channel channel = Search(package[(byte)nooLite.Data.Ch]);
                if (channel is null)
                {
                    channel = new Channel(package[(byte)nooLite.Data.Ch]);
                    _channels.Add(channel);
                }
                Device device = new Device()
                {
                    Type = package[(byte)nooLite.Data.D0],
                    Version = package[(byte)nooLite.Data.D1],
                    State = (package[(byte)nooLite.Data.D2] & 0x01) == 0x01,
                    Bright = (byte)(100 * package[(byte)nooLite.Data.D3] / 0xFF),
                    Addr = package[(byte)nooLite.Data.Id0].ToString("X2") +
                           package[(byte)nooLite.Data.Id1].ToString("X2") +
                           package[(byte)nooLite.Data.Id2].ToString("X2") +
                           package[(byte)nooLite.Data.Id3].ToString("X2")
                };
                channel.Devices.Add(device);
            }
        } // Bind(byte)

        /// <summary>
        /// Отвязка всех исполнительных устройств от канала
        /// </summary>
        /// <param name="id"> номер канала </param>
        public void Delete(byte id)
        {
            if (id >= nooLite.ChannelCount)
                return;
            Channel channel = Search(id);
            if (channel is null)
                return;
            byte[] package = _nooLite.CreatePackage((byte)nooLite.WorkMode.TxF, id, 0x00);
            package[(byte)nooLite.Data.Ctr] = 0x05;
            uint crc = 0;
            for (int i = 0; i < (byte)nooLite.Data.Crc; i++)
                crc += package[i];
            package[(byte)nooLite.Data.Crc] = (byte)(crc & 0xFF);
            _nooLite.SendCommand(package, false);
            List<byte[]> read = _nooLite.SendCommand(id, nooLite.Command.ReadState);
            if (read != null)
                return;
            channel.Devices.Clear();
            if (channel.Sensors.Count == 0) _channels.Remove(channel);
        } // Delete(byte)

        /// <summary>
        /// Отправка команды устройствам nooLite, привязанным к заданному каналу и обработка ответа
        /// </summary>
        /// <param name="id"> номер канала привязки устройства (0-63) </param>
        /// <param name="command"> код команды </param>
        /// <param name="data"> [необязательный] 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3 </param>
        public void SendCommand(byte id, nooLite.Command command, uint data = 0)
        {
            List<byte[]> readPackages = _nooLite.SendCommand(id, command, 0x00, data);
            if (readPackages is null)
                return;
            foreach (byte[] package in readPackages)
            {
                //if (ScanMode == ScanModes.Full)
                //{
                //    if ((package[(byte)nooLite.Data.St] == 0xAD) &&
                //        (package[(byte)nooLite.Data.Ch] == nooLite._сhannelCount - 1))
                //        ScanMode = ScanModes.Done;
                //}
                if ((package[(byte)nooLite.Data.St] == 0xAD) &&
                    (package[(byte)nooLite.Data.Ctr] == 0x01))
                {
                    SendCommand(id, command, data);
                    return;
                }
                if (package[(byte)nooLite.Data.Cmd] != (byte)nooLite.Command.SendState)
                    return;
                Channel channel = Search(package[(byte)nooLite.Data.Ch]);
                if (channel is null)
                {
                    channel = new Channel(package[(byte)nooLite.Data.Ch]);
                    _channels.Add(channel);
                }
                string addr = package[(byte)nooLite.Data.Id0].ToString("X2") +
                              package[(byte)nooLite.Data.Id1].ToString("X2") +
                              package[(byte)nooLite.Data.Id2].ToString("X2") +
                              package[(byte)nooLite.Data.Id3].ToString("X2");
                Device device = channel.DeviceSearch(addr);
                if (device is null)
                {
                    device = new Device();
                    channel.Devices.Add(device);
                }
                device.Type = package[(byte)nooLite.Data.D0];
                device.Version = package[(byte)nooLite.Data.D1];
                device.State = (package[(byte)nooLite.Data.D2] & 0x01) == 0x01;
                device.Bright = (byte)(100 * package[(byte)nooLite.Data.D3] / 0xFF);
                device.Addr = addr;
                //if ((ScanMode == ScanModes.Update) && (package[(byte)nooLite.Data.St] == 0xAD) &&
                //    (channel == Channels[Channels.Count - 1]))
                //    ScanMode = ScanModes.Done;
            }
        } // SendCommand(byte, nooLite.Command, [uint])

        /// <summary>
        /// Вывод списка каналов nooLite в файл журнала
        /// </summary>
        //private static void ChannelsToLog()
        //{
        //    if (!_debug) return;
        //    LogFile?.Add("Настроено каналов nooLite: " + Channels.Count.ToString());
        //    for (byte i = 0; i < nooLite._сhannelCount; i++)
        //    {
        //        Channel channel = ChannelSearch(i);
        //        if (channel == null) continue;
        //        LogFile?.Add(" " + channel.Id.ToString("D2") + " = " + (channel.State ? "ON" : "OFF"));
        //        foreach (Device device in channel.Devices)
        //            LogFile.Add("  Device [" + device.Addr + "] = " +
        //                (device.State ? "ON" : "OFF") + " (" + device.Bright + "%)");
        //        foreach (Sensor sensor in channel.Sensors)
        //        {
        //            LogFile.Add("  Sensor #" + sensor.Topic + " = " + sensor.Value);
        //            if (sensor.Links != null)
        //                LogFile.Add("    ->" + string.Join(",", sensor.Links));
        //        }
        //    }
        //} // ChannelsToLog()
    } // class Server
} // namespace nooLite2MQTT