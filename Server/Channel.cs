using Common;
using System.Collections.Generic;

namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - канал привязки устройств nooLite
    /// Версия от 04.04.2022
    /// </summary>
    public class Channel
    {
        public byte Id { get; set; }           // Номер канала (0-63)
        public bool CanSetBrightness;          // Возможность управления яркостью устройств nooLite
        public List<Device> Devices { get; set; } // Список привязанных устройств
        public List<Sensor> Sensors;           // Список датчиков nooLite
        public bool State                      // Состояние канала: true: вкл; false: выкл
        {
            get
            {
                foreach (Device device in Devices)
                    if (device.State) return true;
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
        } // Channel()

        /// <summary>
        ///	Поиск канала по номеру
        /// </summary>
        /// <param name="addr"> уникальный (аппаратный) адрес устройства в текстовом виде </param>
        /// <returns>
        /// Success: соотвествующий объект - исполнительное устройство nooLite
        /// Failure: null
        /// </returns>
        public Device DeviceSearch(string addr)
        {
            foreach (Device device in Devices)
                if (device.Addr == addr) return device;
            return null;
        } // DeviceSearch(string)

        /// <summary>
        /// Отправка команды устройствам nooLite, привязанным к каналу
        /// </summary>
        /// <param name="command"> код команды </param>
        /// <param name="data"> 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3 </param>
        public void SendCommand(nooLite.Command command, uint data = 0)
        {
            Server.Channels.SendCommand(Id, command, data);
        } // SendCommand(nooLite.Command, uint)

    } // class Channel
}
