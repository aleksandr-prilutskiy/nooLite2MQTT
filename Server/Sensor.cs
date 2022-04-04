namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - датчик nooLite
    /// </summary>
    public class Sensor
    {
        public enum SensorType : byte              // Типы датчиков nooLite
        {
            Unknown = 0x00,                        // Неизвестный или не установлен
            Switch = 0x01,                         // Выключатель
            Door = 0x02                            // Датчик открытия/закрытия двери
        } // enum SensorType

        public SensorType Type;                    // Тип устройства
        public string Topic;                       // Топик MQTT
        public float Value;                        // Значение датчика
        public byte[] Links;                       // Массив каналов привязки, связанных с датчиком
    } // class Sensor
}
