using Common;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;

namespace nooLiteControl
{
    public partial class MainWindow
    {
        public class Device : INotifyPropertyChanged
        {
            public byte Channel { get; set; }
            public byte Type { get; set; }
            public byte Version { get; set; }

            public string Addr { get; set; }
            public bool State { get; set; }
            public byte Bright { get; set; }
            public string TypeToStr
            {
                get
                {
                    return Type == 1 ? "SLF-1-300"
                         : Type == 5 ? "SUF-1-300"
                         : string.Empty;
                }
            }
            public string VersionToStr

            {
                get { return Version.ToString() + ".0"; }
            }
            public string AddrToStr
            {
                get { return "[" + Addr + "]"; }
            }
            public string StateToStr
            {
                get { return State ? "Вкл" : "Выкл"; }
            }
            public string BrightToStr
            {
                get { return Bright.ToString() + "%"; }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
        public static ObservableCollection<Device> Devices = null;

        private void InitDevices()
        {
            Devices = new ObservableCollection<Device>();
        } // InitDevices()

        public static List<Device> SearchDevice(Channel channel)
        {
            List<Device> devices = new List<Device>();
            if (channel == null) return null;
            foreach (Device device in Devices)
                if (device.Channel == channel.Index)
                    devices.Add(device);
            return devices;
        } // SearchDevice(Channel)

        private static List<Device> SearchDevice(byte channel)
        {
            List<Device> devices = new List<Device>();
            if (channel >= nooLite.ChannelCount) return null;
            foreach (Device device in Devices)
                if (device.Channel == channel)
                    devices.Add(device);
            return devices;
        } // SearchDevice(byte)

        public static ObservableCollection<Device> LoadDevices(int index)
        {
            var newDevices = new ObservableCollection<Device>();
            foreach (Device device in Devices)
                if (device.Channel == index) newDevices.Add(device);
            return newDevices;
        } // LoadDevices(int)
    } // class nooLiteDevice
} // namespace nooLiteControl