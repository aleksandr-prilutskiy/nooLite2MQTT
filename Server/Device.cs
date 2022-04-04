namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - исполнительное устройство nooLite
    /// </summary>
    public class Device
    {
        public byte Type { get; set; }         // Модель устройства
        public byte Version { get; set; }      // Версия прошивки устройства
        public string Addr { get; set; }       // Уникальный (аппаратный) адрес устройства (4 байта)
        public bool State { get; set; }        // Состояние: true: вкл; false: выкл
        public byte Bright { get; set; }       // Яркость (0-100%)
    } // class Device
}
