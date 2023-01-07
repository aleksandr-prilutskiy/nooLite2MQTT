using Common;
using System.Collections.Generic;

namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - канал привязки устройств nooLite
    /// </summary>
    /// Версия от 06.01.2023
    public class Channel
    {
        /// <summary>
        /// Номер канала (0-63)
        /// </summary>
        public byte Id { get; set; }

        /// <summary>
        /// Возможность управления яркостью устройств nooLite
        /// </summary>
        public bool CanSetBrightness;

        /// <summary>
        /// Список привязанных устройств
        /// </summary>
        public List<Device> Devices { get; set; }

        /// <summary>
        /// Список датчиков nooLite
        /// </summary>
        public List<Sensor> Sensors;

        /// <summary>
        /// Состояние канала: true: вкл; false: выкл
        /// </summary>
        public bool State
        {
            get
            {
                foreach (Device device in Devices)
                    if (device.State)
                        return true;
                return false;
            }
        } // State

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="id"> номер канала </param>
        public Channel(byte id)
        {
            Id = id;
            CanSetBrightness = false;
            Devices = null;
            Sensors = null;
        } // Channel(byte)

        /// <summary>
        ///	Поиск канала по номеру
        /// </summary>
        /// <param name="addr"> уникальный (аппаратный) адрес устройства в текстовом виде </param>
        /// <returns> Соотвествующий объект - исполнительное устройство nooLite или null </returns>
        public Device DeviceSearch(string addr)
        {
            foreach (Device device in Devices)
                if (device.Addr == addr)
                    return device;
            return null;
        } // DeviceSearch(string)

        /// <summary>
        /// Отправка команды устройствам nooLite, привязанным к каналу
        /// </summary>
        /// <param name="command"> код команды </param>
        /// <param name="data"> [необязательный] 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3 </param>
        public void SendCommand(nooLite.Command command, uint data = 0)
        {
            Server.Channels.SendCommand(Id, command, data);
        } // SendCommand(nooLite.Command, [uint])
    } // class Channel
} // namespace nooLite2MQTT