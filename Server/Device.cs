namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - исполнительное устройство nooLite
    /// </summary>
    /// Версия от 06.01.2023
    public class Device
    {
        /// <summary>
        /// Модель устройства
        /// </summary>
        public byte Type { get; set; }

        /// <summary>
        /// Версия прошивки устройства
        /// </summary>
        public byte Version { get; set; }

        /// <summary>
        /// Уникальный (аппаратный) адрес устройства (4 байта)
        /// </summary>
        public string Addr { get; set; }

        /// <summary>
        /// Состояние: true: вкл; false: выкл
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// Яркость (0-100%)
        /// </summary>
        public byte Bright { get; set; }
    } // class Device
} // namespace nooLite2MQTT