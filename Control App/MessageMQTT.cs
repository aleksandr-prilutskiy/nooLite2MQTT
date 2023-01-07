using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace nooLiteControl
{
    /// <summary>
    /// Объект - запись в журнале сообщений MQTT
    /// </summary>
    /// Версия от 06.01.2023
    public class MessageMQTT : INotifyPropertyChanged
    {
        /// <summary>
        /// Наименование топика
        /// </summary>
        public string Topic
        {
            get { return _topic; }
            set
            {
                _topic = value;
                OnPropertyChanged("Topic");
            }
        } // Title
        private string _topic;

        /// <summary>
        /// Содержимое сообщения
        /// </summary>
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                OnPropertyChanged("Content");
            }
        } // Text
        private string _content;

        /// <summary>
        /// Время получения сообщения
        /// </summary>
        public string Time
        {
            get { return _Time.ToString("HH:mm:ss"); }
            set
            {
                if (!DateTime.TryParse(value, out _Time))
                    _Time = DateTime.Now;
                OnPropertyChanged("Time");
            }
        } // Time
        private DateTime _Time;

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
            }
            catch { }
        } // OnPropertyChanged([CallerMemberName]string)
    } // class Message
} // namespace nooLiteControl