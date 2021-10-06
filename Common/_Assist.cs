using Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Threading.Tasks;

namespace nooLite2MQTT
{
    public partial class Server
    {
        //private static readonly Action<object> RepeatCommand = (object obj) =>
        //{
        //    if (obj == null) return;
        //    byte[] buffer = (byte[])obj;
        //    byte channel = buffer[(byte)nooLite.Data.Ch];
        //    nooLiteThings.Device device = nooLiteThings.DeviceSearch(channel);
        //    if (device == null) return;
        //    if (device.TryCount == 0) return;
        //    device.TryCount--;
        //    byte command = buffer[(byte)nooLite.Data.Cmd];
        //    if ((command == (byte)nooLite.Command.On) ||
        //        (command == (byte)nooLite.Command.Off))
        //    {
        //        DateTime timer = DateTime.Now.AddMilliseconds(500);
        //        while (DateTime.Now < timer) ;
        //        if (device.State != (command == (byte)nooLite.Command.On))
        //            nooLite.SendCommand(channel, command);
        //    };
        //}; // RepeatCommand(object)
    } // class nooLite2MQTT
}
