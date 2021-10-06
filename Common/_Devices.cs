using Common;
using System;
using System.Collections.Generic;

namespace nooLite2MQTT
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Объект и функции для работы с исполнительными устройствами nooLite
// Версия от 26.09.2021
//
//===============================================================================================================
    public partial class Server
    {
        public class Device                        // Объект - исполнительное устройство nooLite
        {
            public byte Channel { get; set; }      // Номер канала привязки устройства (0-63)
            public byte Type { get; set; }         // Модель устройства
            public byte Version { get; set; }      // Версия прошивки устройства
            public string Addr { get; set; }       // Уникальный адрес устройства (4 байта)
            public bool State { get; set; }        // Состояние: true: вкл; false: выкл
            public byte Bright { get; set; }       // Яркость (0-100%)
        } // class Device
        public static List<Device> Devices;        // Список устройств nooLite

        public static bool _bysy4scan = false;     // Признак запущенного сканирования устройств

//===============================================================================================================
// Name...........:	DeviceSearch
// Description....:	Поиск устройства в списке устройств nooLite
// Syntax.........:	DeviceSearch(channel)
// Parameters.....:	channel     - Номер канала привязки устройства
// Return value(s):	Success:    - Список объектов - исполнительных устройств nooLite
//                  Failure:    - пустой список
//===============================================================================================================
        public static List<Device> DeviceSearch(byte channel)
        {
            List<Device> result = new List<Device>();
            for (int i = 0; i < Devices.Count; i++)
                if (Devices[i].Channel == channel) result.Add(Devices[i]);
            return result;
        } // DeviceSearch(byte)

//===============================================================================================================
// Name...........:	ScanDevices
// Description....:	Сканирование всех подключенных устройств nooLite и чтение их состяний
// Syntax.........:	ScanDevices()
// Remarks .......:	После успешного чтения заполняется список Devices
//===============================================================================================================
        public static void ScanDevices()
        {
            if (!nooLite.Connected || _bysy4scan) return;
            _bysy4scan = true;
            bool scanAll = Devices.Count == 0;
            for (byte i = 0; i < nooLite._сhannelCount; i++)
            {
                List<Device> devices = DeviceSearch(i);
                if (!scanAll && devices.Count == 0) continue;
                if (devices.Count > 0)
                    foreach(Device device in devices)
                        SendCommandToDevice(i, device, (byte)nooLite.Command.ReadState);
                else
                    SendCommandToDevice(i, null, (byte)nooLite.Command.ReadState);
            }
            nooLite_Timer = nooLite_Timer.AddMilliseconds(_nooLite_Update);
            _bysy4scan = false;
        } // ScanDevices()

//===============================================================================================================
// Name...........:	ScanDevicesOnTimer
// Description....:	Опрос состояния всех устройств по таймеру
// Syntax.........:	ScanDevicesOnTimer()
//===============================================================================================================
        private void ScanDevicesOnTimer()
        {
            if (!nooLite.Connected || _bysy4scan) return;
            _bysy4scan = true;
            if (_nooLite_Connect && (DateTime.Now > nooLite_Timer))
            {
                nooLite_Timer = nooLite_Timer.AddMilliseconds(_nooLite_Update);
                ScanDevices();
            }
            _bysy4scan = false;
        } // ScanDevicesOnTimer()

//===============================================================================================================
// Name...........:	SendCommandToDevice
// Description....:	Отправка команды устройству nooLite с обработкой ответа
// Syntax.........:	SendCommandToDevice(device, command, data)
// Parameters.....:	device      - Объект - исполнительное устройство nooLite
//                  command     - Код команды
//                  data        - 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3
// Remarks .......:	После успешного выполнения обновляются данные объекта device
//===============================================================================================================
        public static void SendCommandToDevice(Device device, byte command, uint data = 0)
        {
            if (device == null) return;
            SendCommandToDevice(device.Channel, device, command, data);
        } // SendCommandToDevice(Device, byte, uint)

//===============================================================================================================
// Name...........:	SendCommandToDevice
// Description....:	Отправка команды устройству nooLite с обработкой ответа
// Syntax.........:	SendCommandToDevice(channel, device, command, data)
// Parameters.....:	channel     - Номер канала привязки устройства (0-63)
//                  device      - Объект - исполнительное устройство nooLite
//                  command     - Код команды
//                  data        - 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3
// Remarks .......:	После успешного выполнения обновляются данные объекта device
//                  Если device = null - создается и добавляется в список Devices новый объект
//===============================================================================================================
        private static void SendCommandToDevice(byte channel, Device device, byte command, uint data = 0)
        {
            bool scanMode = device == null;
            List<byte[]> read = nooLite.SendCommand(channel, command, data);
            if (read == null) return;
            foreach (byte[] package in read)
            {
                if (package[(byte)nooLite.Data.Cmd] != (byte)nooLite.Command.SendState) return;
                if (scanMode) device = new Device();
                bool saveState = device.State;
                byte saveBright = device.Bright;
                device.Channel = package[(byte)nooLite.Data.Ch];
                device.Type = package[(byte)nooLite.Data.D0];
                device.Version = package[(byte)nooLite.Data.D1];
                device.State = (package[(byte)nooLite.Data.D2] & 0x01) == 0x01;
                device.Bright = (byte)(100 * package[(byte)nooLite.Data.D3] / 0xFF);
                device.Addr = package[(byte)nooLite.Data.Id0].ToString("X2") +
                              package[(byte)nooLite.Data.Id1].ToString("X2") +
                              package[(byte)nooLite.Data.Id2].ToString("X2") +
                              package[(byte)nooLite.Data.Id3].ToString("X2");
                if (scanMode) Devices.Add(device);
                string topic = "nooLite@" + device.Channel;
                if (scanMode || (device.State != saveState))
                    MQTT.MessageSend(topic, device.State ? "ON" : "OFF", true);
                if (device.State && (device.Bright != saveBright) &&
                    (device.Bright > 0) && (device.Bright < 100))
                    MQTT.MessageSend(topic + "/brig", device.Bright.ToString());
            }
        } // SendCommandToDevice(byte, Device, byte, uint)

//===============================================================================================================
// Name...........:	DeviceBing
// Description....:	Привязка исполнительного устройства nooLite к адаптеру MTRF-64-USB
// Syntax.........:	DeviceBing(channel)
// Parameters.....:	channel     - Номер канала привязки устройства (0-63)
// Remarks .......:	После успешного выполнения...
//===============================================================================================================
        public static void DeviceBing(byte channel)
        {
            if (channel >= nooLite._сhannelCount) return;
            List<byte[]> read = nooLite.SendCommand(channel, (byte)nooLite.Command.Bind);
            if (read == null) return;
            foreach (byte[] package in read)
            {
                if (package[(byte)nooLite.Data.Cmd] != (byte)nooLite.Command.SendState) return;
                Device device = new Device();
                device.Channel = package[(byte)nooLite.Data.Ch];
                device.Type = package[(byte)nooLite.Data.D0];
                device.Version = package[(byte)nooLite.Data.D1];
                device.State = (package[(byte)nooLite.Data.D2] & 0x01) == 0x01;
                device.Bright = (byte)(100 * package[(byte)nooLite.Data.D3] / 0xFF);
                device.Addr = package[(byte)nooLite.Data.Id0].ToString("X2") +
                              package[(byte)nooLite.Data.Id1].ToString("X2") +
                              package[(byte)nooLite.Data.Id2].ToString("X2") +
                              package[(byte)nooLite.Data.Id3].ToString("X2");
                Devices.Add(device);
                string topic = "nooLite@" + device.Channel;
                MQTT.MessageSend(topic, device.State ? "ON" : "OFF", true);
            }
        } // DeviceBing(string)

//===============================================================================================================
// Name...........:	ChannelClear
// Description....:	Отвязка всех исполнительных устройств nooLite от указанного канала адаптера MTRF-64-USB
// Syntax.........:	ChannelClear(channel)
// Parameters.....:	channel     - Номер канала привязки устройства (0-63)
//===============================================================================================================
        public static void ChannelClear(byte channel)
        {
            if (channel >= nooLite._сhannelCount) return;
            byte[] package = nooLite.CreatePackage((byte)nooLite.WorkMode.TxF, channel, 0x00);
            package[(byte)nooLite.Data.Ctr] = 0x05;
            uint crc = 0;
            for (int i = 0; i < (byte)nooLite.Data.Crc; i++) crc += package[i];
            package[(byte)nooLite.Data.Crc] = (byte)(crc & 0xFF);
            nooLite.SendCommand(package, false);
            List<byte[]> read = nooLite.SendCommand(channel, (byte)nooLite.Command.ReadState);
            if (read != null) return;
            for (int i = 0; i < Devices.Count; i++)
                while ((i < Devices.Count) && (Devices[i].Channel == channel))
                    Devices.RemoveAt(i);
        } // ChannelClear(byte)
    } // class Server
}
