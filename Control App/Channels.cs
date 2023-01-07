using Common;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace nooLiteControl
{
    public partial class MainWindow
    {
        public class Channel : INotifyPropertyChanged
        {
            public int Index;
            public string Title;

            public int Count
            {
                set
                {
                    _count = value;
                    OnPropertyChanged("Count");
                    OnPropertyChanged("CountToStr");
                }
                get { return _count; }
            } // Count
            private int _count;

            public string CountToStr
            {
                get { return "Устройств: " + _count.ToString(); }
            } // CountToStr

            public string _Index
            {
                get { return (Index + 1).ToString(); }
            }
            public string _Count
            {
                get { return "Устройств: " + Count.ToString(); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
            public void OnPropertyChanged([CallerMemberName] string prop = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
            } // OnPropertyChanged([CallerMemberName]string)
        }
        public static ObservableCollection<Channel> Channels = null;

        private static void InitChannels()
        {
            Channels = new ObservableCollection<Channel>();
            for (int i = 0; i < nooLite.ChannelCount; i++)
                Channels.Add(new Channel()
                {
                    Index = i,
                    Title = IniFile.ReadString("Channel#" + i.ToString(), "Title", ""),
                    Count = 0
                });
        } // InitChannels()

        private static void LoadChannels(string json)
        {
            foreach (Channel channel in Channels) channel.Count = 0;
            foreach (Device record in JsonSerializer.Deserialize<List<Device>>(json))
            {
                List<Device> devices = SearchDevice(record.Channel);
                if (devices.Count == 0)
                    Devices.Add(new Device()
                    {
                        Channel = record.Channel,
                        Type = record.Type,
                        Version = record.Version,
                        Addr = record.Addr,
                        State = record.State,
                        Bright = record.Bright
                    });
                else
                    foreach (Device device in devices)
                    {
                        if (device.Addr == record.Addr)
                        {
                            device.State = record.State;
                            device.Bright = record.Bright;
                        }
                    }
            }
            foreach (Channel channel in Channels)
                foreach (Device device in Devices)
                    if (channel.Index == device.Channel)
                        channel.Count++;
        } // LoadChannels(string)

        public Channel CurrentChannel()
        {
            var channel = (Channel)treeListСhannels.SelectedItem;
            if (channel == null) return null;
            return channel;
        } // CurrentChannel()
    } // class nooLiteData
}
