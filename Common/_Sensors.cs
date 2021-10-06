using Common;
using System.Collections.Generic;

namespace nooLite2MQTT
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Объект и функции для работы с датчиками и пультами управления nooLite
// Версия от 22.09.2021
//
//===============================================================================================================
    public partial class Server
    {
        public enum SensorType : byte              // Тип датчика nooLite
        {
            Unknown = 0x00,                        // Неизвестный или не установлен
            Switch  = 0x01,                        // Выключатель
            Door    = 0x02                         // Датчик открытия/закрытия двери
        } // enum SensorType

        public class Sensor                        // Объект - датчик nooLite
        {
            public byte Channel;                   // Номер канала привязки устройства (0-63)
            public byte Type;                      // Тип устройства (SensorType)
            public string Topic;                   // Топик MQTT
            public float Value;                    // Значение датчика
            public byte[] Devices;                 // Массив датчиков (каналы привязки), связанных с датчиком
        } // class Sensor
        public static List<Sensor> Sensors;        // Список датчиков nooLite

//===============================================================================================================
// Name...........:	SensorSearch
// Description....:	Поиск датчика в списке датчиков nooLite
// Syntax.........:	SensorSearch(index)
// Parameters.....:	index       - Номер канала привязки устройства (0-63)
// Return value(s):	Success:    - Объект - датчик nooLite
//                  Failure:    - null
//===============================================================================================================
        public static Sensor SensorSearch(byte index)
        {
            if (Sensors == null) return null;
            for (int i = 0; i < Sensors.Count; i++)
                if (Sensors[i].Channel == index) return Sensors[i];
            return null;
        } // SensorSearch(byte)

//===============================================================================================================
// Name...........:	LoadSensors
// Description....:	Чтение списка датчиков nooLite из файла конфигурации
// Syntax.........:	LoadSensors(iniFile)
// Parameters.....:	iniFile     - объект-файл конфигурации
// Remarks .......:	После успешного чтения заполняется список Sensors
//===============================================================================================================
        public void LoadSensors(IniFile iniFile)
        {
            Sensors.Clear();
            for (int i = 1; true; i++)
            {
                int channel = iniFile.ReadInt("Sensor#" + i.ToString(), "Channel", -1);
                if (channel < 0) break;
                string topic = iniFile.ReadString("Sensor#" + i.ToString(), "Topic", "");
                string type = iniFile.ReadString("Sensor#" + i.ToString(), "Type", "");
                Sensor sensor = new Sensor
                {
                    Channel = (byte)channel,
                    Type = (byte)((type == "Switch") ? SensorType.Switch
                           : (type == "Door") ? SensorType.Door : SensorType.Unknown),
                    Topic = topic,
                    Value = 0,
                    Devices = null
                };
                string[] items = iniFile.ReadString("Sensor#" + i.ToString(), "Devices", "").Split(',');
                if (items.Length > 0)
                {
                    sensor.Devices = new byte[items.Length];
                    for (int j = 0; j < items.Length; j++)
                        byte.TryParse(items[j], out sensor.Devices[j]);
                }
                Sensors.Add(sensor);
            }
        } // LoadSensors(IniFile)

//===============================================================================================================
// Name...........:	CheckSensors
// Description....:	Чтение и обработка поступивших значени датчиков nooLite
// Syntax.........:	CheckSensors()
//===============================================================================================================
        private void CheckSensors()
        {
            if (!_nooLite_Connect) return;
            while (true)
            {
                byte[] buffer = nooLite.GetMessage();
                if (buffer == null) break;
                byte command = buffer[(byte)nooLite.Data.Cmd];
                Sensor sensor = SensorSearch(buffer[(byte)nooLite.Data.Ch]);
                if (sensor != null)
                    switch (sensor.Type)
                    {
                        case (byte)SensorType.Switch: // Выключатель
                            foreach (byte index in sensor.Devices)
                            {
                                List<Device> devices = DeviceSearch(index);
                                if (devices != null)
                                    foreach (Device device in devices)
                                        SendCommandToDevice(device, (byte)nooLite.Command.ReadState);
                            }
                            break;
                        case (byte)SensorType.Door: // Датчик открытия/закрытия двери
                            float value = (command == (byte)nooLite.Command.On) ? 1 : 0;
                            if (value != sensor.Value)
                            {
                                sensor.Value = value;
                                if (sensor.Topic != "")
                                    MQTT.MessageSend(sensor.Topic, value.ToString(), true);
                            }
                            break;
                    }
            }
        } // CheckSensors()
    } // class Server
}
