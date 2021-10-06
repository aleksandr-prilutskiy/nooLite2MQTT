using Common;

namespace nooLite2MQTT
{
//===============================================================================================================
//
// Сервис сопряжения устройств nooLite c протоколом MQTT
// Функции отладки (добавления отладочной информации в файл журнала)
// Версия от 26.09.2021
//
//===============================================================================================================
    public partial class Server
    {
//===============================================================================================================
// Name...........:	Debug_CommandInfo
// Description....:	Вывод информации о пакете данных nooLite
// Syntax.........:	Debug_CommandInfo([] buffer)
// Parameters.....:	buffer      - пакет данных nooLite
//===============================================================================================================
        private static void Debug_CommandInfo(byte[] buffer)
        {
            if (!_debug || (LogFile == null) || (buffer == null)) return;
            string str = "";
            if (buffer[(byte)nooLite.Data.St] == 0xAB) str += "SEND = ";
            else if (buffer[(byte)nooLite.Data.St] == 0xAD) str += "READ = ";
            else return;
            for (int i = 0; i < buffer.Length; i++) str += buffer[i].ToString("X2") + " ";
            str += "| ch=" + buffer[(byte)nooLite.Data.Ch].ToString("00") + " " +
                (buffer[(byte)nooLite.Data.St] == 0xAB ? "<-" : "->");
            if (buffer[(byte)nooLite.Data.Ctr] == 0x05) str += " mode=Unbind";
            else
                switch (buffer[(byte)nooLite.Data.Cmd])
                {
                    case (byte)nooLite.Command.Off:
                        str += " cmd=Off";
                        break;
                    case (byte)nooLite.Command.BrightDown:
                        str += " cmd=BrightDown";
                        break;
                    case (byte)nooLite.Command.On:
                        str += " cmd=On";
                        break;
                    case (byte)nooLite.Command.BrightUp:
                        str += " cmd=BrightUp";
                        break;
                    case (byte)nooLite.Command.Switch:
                        str += " cmd=Switch";
                        break;
                    case (byte)nooLite.Command.BrightBack:
                        str += " cmd=BrightBack";
                        break;
                    case (byte)nooLite.Command.SetBrightness:
                        str += " cmd=SetBrightness: " +
                            (100 * buffer[(byte)nooLite.Data.D0] / 0xFF).ToString() + "%";
                        break;
                    case (byte)nooLite.Command.LoadPreset:
                        str += " cmd=LoadPreset";
                        break;
                    case (byte)nooLite.Command.SavePreset:
                        str += " cmd=SavePreset";
                        break;
                    case (byte)nooLite.Command.Unbind:
                        str += " cmd=Unbind";
                        break;
                    case (byte)nooLite.Command.BrightStepDown:
                        str += " cmd=BrightStepDown";
                        break;
                    case (byte)nooLite.Command.BrightStepUp:
                        str += " cmd=BrightStepUp";
                        break;
                    case (byte)nooLite.Command.BrightReg:
                        str += " cmd=BrightReg";
                        break;
                    case (byte)nooLite.Command.Bind:
                        str += " cmd=Bind";
                        break;
                    case (byte)nooLite.Command.SwitchMode:
                        str += " cmd=SwitchMode";
                        break;
                    case (byte)nooLite.Command.ReadState:
                        str += " cmd=ReadState";
                        break;
                    case (byte)nooLite.Command.WriteState:
                        str += " cmd=WriteState: " +
                            (buffer[(byte)nooLite.Data.D2] > 0 ? "ON" : "OFF");
                        break;
                    case (byte)nooLite.Command.SendState:
                        str += " cmd=SendState: " +
                            (buffer[(byte)nooLite.Data.D2] > 0 ? "ON" : "OFF") +
                            " (" + (100 * buffer[(byte)nooLite.Data.D3] / 0xFF).ToString() + "%)";
                        break;
                    case (byte)nooLite.Command.Service:
                        str += " cmd=Service";
                        break;
                }
            LogFile?.Add(str);
        } // Debug_CommandInfo([] buffer, string)

//===============================================================================================================
// Name...........:	Debug_DevicesAndSensors
// Description....:	Вывод списка исполнительных устройств и датчиков nooLite
// Syntax.........:	Debug_DevicesAndSensors()
//===============================================================================================================
        private static void Debug_DevicesAndSensors()
        {
            if (!_debug || (LogFile == null)) return;
            LogFile?.Add("Обнаружено устройств nooLite: " + Devices.Count.ToString());
            for (int i = 0; i < nooLite._сhannelCount; i++)
                foreach (Device device in Devices)
                    if (device.Channel == i)
                        LogFile?.Add(" " + device.Channel.ToString("D2") + ": " + device.Addr +
                            " = " + (device.State ? "ON" : "OFF") +
                            " (" + device.Bright.ToString() + "%)");
            LogFile?.Add("Настроено датчиков nooLite: " + Sensors.Count.ToString());
            foreach (Sensor sensor in Sensors)
            {
                if (sensor.Type == (byte)SensorType.Switch)
                {
                    string str = "";
                    if (sensor.Devices != null)
                        foreach (byte device in sensor.Devices)
                            str += "," + device.ToString();
                    if (str.Length > 0) str = str.Substring(1);
                    LogFile?.Add(" " + sensor.Channel.ToString("D2") + ": " + sensor.Topic + " {" + str + "}");
                }
                if (sensor.Type == (byte)SensorType.Door)
                    LogFile?.Add(" " + sensor.Channel.ToString("D2") + ": " + sensor.Topic +
                        " = " + sensor.Value.ToString());
            }
        } // Debug_DevicesAndSensors()
    } // class Server
}
