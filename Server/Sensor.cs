namespace nooLite2MQTT
{
    /// <summary>
    /// Объект - датчик nooLite
    /// </summary>
    /// Версия от 06.01.2023
    public class Sensor
    {
        /// <summary>
        /// Типы датчиков nooLite
        /// </summary>
        public enum SensorType : byte
        {
            Unknown = 0x00,                        // Неизвестный или не установлен
            Switch = 0x01,                         // Выключатель
            Door = 0x02                            // Датчик открытия/закрытия двери
        } // enum SensorType

        /// <summary>
        /// Тип устройства
        /// </summary>
        public SensorType Type;

        /// <summary>
        /// Топик MQTT
        /// </summary>
        public string Topic;

        /// <summary>
        /// Значение датчика
        /// </summary>
        public float Value;

        /// <summary>
        /// Массив каналов привязки, связанных с датчиком
        /// </summary>
        public byte[] Links;
    } // class Sensor
} // namespace nooLite2MQTT