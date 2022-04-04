using System;
using System.Collections.Generic;
using System.IO.Ports;
//using System.Timers;

using System.Text;
using System.Threading;

namespace Common
{
    /// <summary>
    /// Объект для работы с адаптером nooLite MTRF-64-USB
    /// Версия от 04.04.2022
    /// </summary>
    public class nooLite
    {
        public enum WorkMode : byte // Режим работы адаптера MTRF-64-USB
        {
            Tx      = 0, // Режим nooLite TX
            Rx      = 1, // Режим nooLite RX
            TxF     = 2, // Режим nooLite-F TX
            RxF     = 3, // Режим nooLite-F RX
            Srv     = 4, // Сервисный режим работы с nooLite-F
            Update  = 5  // Режим обновления ПО
        } // enum WorkMode

        public enum Data : byte // Описание структуры отправляемых / получаемых данных адаптера MTRF-64-USB
        {
            St   =  0,   // Стартовый байт(значение всегда tx = 171 / rx = 173)
            Mode =  1,   // Режим работы адаптера
            Ctr  =  2,   // Тип команды
            Res  =  3,   // В tx - зарезервирован, не используется
            Togl =  3,   // В rx - количество оставшихся ответов от адаптера, значение TOGL
            Ch   =  4,   // Адрес канала, ячейки привязки
            Cmd  =  5,   // Код команды
            Fmt  =  6,   // Формат
            D0   =  7,   // Байт данных 0
            D1   =  8,   // Байт данных 1
            D2   =  9,   // Байт данных 2
            D3   = 10,   // Байт данных 3
            Id0  = 11,   // Идентификатор блока, бит 31…24
            Id1  = 12,   // Идентификатор блока, бит 23…16
            Id2  = 13,   // Идентификатор блока, бит 15…8
            Id3  = 14,   // Идентификатор блока, бит 7…0
            Crc  = 15,   // Контрольная сумма(младший байт суммы первых 15 байт)
            Sp   = 16,   // Стоповый байт(значение всегда tx = 172 / rx = 174)
        } // enum Data

        public enum Command : byte // Список команд:
        {
            Off             = 0,    // Выключить нагрузку
            BrightDown      = 1,    // Запускает плавное понижение яркости
            On              = 2,    // Включить нагрузку
            BrightUp        = 3,    // Запускает плавное повышение яркости
            Switch          = 4,    // Включает или выключает нагрузку
            BrightBack      = 5,    // Запускает плавное изменение яркости в обратном направлении
            SetBrightness   = 6,    // Установить заданную в расширении команды яркость
            LoadPreset      = 7,    // Вызвать записанный сценарий
            SavePreset      = 8,    // Записать сценарий в память
            Unbind          = 9,    // Стирание адреса управ. устройства из памяти исполнит.
            StopReg         = 10,   // Прекращает действие команд Bright_Down, Bright_Up, Bright_Back
            BrightStepDown  = 11,   // Понизить яркость на шаг
            BrightStepUp    = 12,   // Повысить яркость на шаг
            BrightReg       = 13,   // Запускает плавное изменение яркости
            Bind            = 15,   // Сообщает исполнительному устройству об активации режима привязки
            RollColour      = 16,   // Запускает плавное изменение цвета в RGB-контроллере по радуге
            SwitchColour    = 17,   // Переключение между стандартными цветами в RGB-контроллере
            SwitchMode      = 18,   // Переключение между режимами RGB-контроллера
            SpeedModeBack   = 19,   // Запускает изменение скорости работы режимов RGB контроллера в обратном направлении
            BatteryLow      = 20,   // У устройства, которое передало данную команду, разрядился элемент питания
            SensTempHumi    = 21,   // Передает данные о температуре, влажности и состоянии элементов
            TemporaryOn     = 25,   // Включить свет на заданное время (в 5-секундных тактах)
            Modes           = 26,   // Установка режимов работы исполнительного устройства
            ReadState       = 128,  // Получение состояния исполнительного устройства
            WriteState      = 129,  // Установка состояния исполнительного устройства
            SendState       = 130,  // Ответ от исполнительного устройства
            Service         = 131,  // Включение сервисного режима на заранее привязанном устройстве
            ClearMemory     = 132,  // Очистка памяти устройства nooLite
        } // enum Command

        private static SerialPort _serial;           // Порт для обмена данными с адаптером MTRF-64-USB
        public string NativePort = "";               // Принудительно указаный порт адаптер MTRF-64-USB
        private string _portMTRF64 = "";             // Название COM-порта, к которому подключен адаптер MTRF-64-USB
        private const uint _timeout = 1000;          // Время ожидания ответа адаптера MTRF-64-USB
        private const uint _reconnect = 30000;        // Период переподключения к адаптеру MTRF-64-USB
        private const byte _modeMtrf64 = (byte)WorkMode.TxF; // Режим работы адаптера MTRF-64-USB
        public const byte _сhannelCount = 64;        // Всего количество каналов адаптера MTRF-64-USB
        private const int _packetSize = 17;          // Размер пакета записи / чтения адаптера MTRF-64-USB
        private Thread _handler = null;              // Поток для поддержания подключения к брокеру MQTT
        private byte[] _buffer;                      // Буфер чтения данных (накопление до полного пакета)
        private byte _bufferPos = 0;                 // Количество данных, загруженных в буфер чтения
        private static List<byte[]> _queuePackages;  // Очередь полученных пакетов, ждущих обработки
        private readonly LogFile _fileLog = null;    // Ссылка на объект - журнал работы приложения
        private bool _busy_send = false;             // Признак того, что адаптер занят (обрабатывает на команду)
        private bool _busy_read = false;             // Признак того, что буфер полученных пакетов занят
        public delegate void PackageHandler(byte[] buffer);
        public PackageHandler OnPackageSend = Skip;  // Обработчик отправленных сообщений
        public PackageHandler OnPackageRead = Skip;  // Обработчик принятых сообщений

        //private static Timer _timer;               // Таймер проверки подключения к адаптеру MTRF-64-USB
        //private bool _busy_timer = false;          // Признак обработки события по таймеру

        public bool Connected                        // Признак успешного подключения к адаптеру MTRF-64-USB
        {
            get { return ((_portMTRF64 != "") && _serial.IsOpen);  } 
        } // Connected

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="fileLog"> ссылка на оъект для работы с файлом журнала </param>
        public nooLite(LogFile fileLog = null)
        {
            _fileLog = fileLog;
            _queuePackages = new List<byte[]>();
            _buffer = new byte[_packetSize];
            _serial = new SerialPort()
            {
                BaudRate = 9600,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                Handshake = Handshake.None,
                ReadTimeout = 100,
                WriteTimeout = 100
            };
            _serial.DataReceived += ReceiveDataHandler;
        } // nooLite(LogFile)

        /// <summary>
        /// Запуск потока для поддержания подключения к адаптеру MTRF-64-USB
        /// </summary>
        public void Start(CancellationToken cancelToken)
        {
            _handler = new Thread(() => Handler(this, cancelToken));
            _handler.Start();
        } // Start()

        /// <summary>
        /// Основной обработчик событий объекта
        /// </summary>
        /// <param name="MQTT"> ссылка на объект для работы с брокером MQTT </param>
        /// <param name="cancelToken"> токен для завершения потока </param>
        private static void Handler(nooLite nooLite, CancellationToken cancelToken)
        {
            DateTime timer = DateTime.Now;
            while (!cancelToken.IsCancellationRequested)
            {
                if ((DateTime.Now >= timer) && (!nooLite.Connected))
                {
                    if (!nooLite.FindPortMTRF())
                        nooLite._fileLog?.Add("@Ошибка: Модуль nooLite MTRF-64-USB не обнаружен");
                    else if (nooLite.Connected)
                        nooLite._fileLog?.Add("@Модуль nooLite MTRF-64-USB подключен к " + nooLite._portMTRF64);
                    else
                        nooLite._fileLog?.Add("@Ошибка: Модуль nooLite MTRF-64-USB не подключен");
                    timer = DateTime.Now.AddMilliseconds(_reconnect);
                }
                Thread.Sleep(500);
            }
        } // Handler(nooLite, CancellationToken)

        /// <summary>
        /// Поиск порта, к которому подключен адаптер nooLite MTRF-64 USB
        /// Если адаптер найден, com-порт остается открытым
        /// </summary>
        /// <returns>
        /// Success: true, в PortMTRF64 - номер COM-порта в виде строки, например: "COM1"
        /// Failure: false, в PortMTRF64 - пустая строка
        /// </returns>
        private bool FindPortMTRF()
        {
            _portMTRF64 = "";
            string[] portnames = SerialPort.GetPortNames();
            if (portnames.Length == 0) return false;
            foreach (string portname in portnames)
            {
                if ((NativePort != "") && (portname != NativePort)) continue;
                _serial.PortName = portname;
                try
                {
                    _serial.Open();
                    if (!_serial.IsOpen) continue;
                    if (SendPackage(CreatePackage((byte)WorkMode.Srv)) &&
                        (SendCommand(0, Command.ReadState) != null))
                    {
                        _portMTRF64 = portname;
                        _serial.DiscardInBuffer();
                        return true;
                    }
                    _serial.Close();
                }
                catch (Exception)
                {
                    if (Connected) _serial.Close();
                }
            }
            return false;
        } // bool FindPortMTRF()

        /// <summary>
        /// Отправка команды на устройство
        /// </summary>
        /// <param name="channel"> номер канала </param>
        /// <param name="command"> код команды </param>
        /// <param name="data"> 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3 </param>
        /// <param name="format"> содержмое поля FMT </param>
        /// <returns>
        /// Success: Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
        /// Failure: null
        /// </returns>
        public List<byte[]> SendCommand(byte channel, Command command, byte format = 0x00, uint data = 0x00000000)
        {
            if ((channel < 0) || (channel >= _сhannelCount)) return null;
            return(SendCommand(CreatePackage(_modeMtrf64, channel, command, format, data)));
        } // SendCommand(byte, Command, byte, uint)

        /// <summary>
        /// Отправка пакета байт на устройство
        /// </summary>
        /// <param name="package"> пакет отпраки данных адаптера MTRF-64 USB </param>
        /// <param name="answer"> если true - ждать ответа на команду </param>
        /// <returns>
        /// Success: Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
        /// Failure: null
        /// </returns>
        public List<byte[]> SendCommand(byte[] package, bool answer = true)
        {
            if (!_serial.IsOpen) return null;
            while (_busy_send) Thread.Sleep(100);
            _busy_send = true;
            byte channel = package[(byte)Data.Ch];
            SendPackage(package);
            if (!answer)
            {
                _busy_send = false;
                return null;
            }
            DateTime timer = DateTime.Now.AddMilliseconds(_timeout);
            List<byte[]> response = new List<byte[]>();
            bool needResponse = true;
            while (needResponse)
            {
                if (DateTime.Now > timer)
                {
                    _busy_send = false;
                    return null;
                }
                while (_busy_read) Thread.Sleep(100);
                _busy_read = true;
                for (int i = 0; i < _queuePackages.Count; i++)
                {
                    package = _queuePackages[i];
                    if (package == null) continue;
                    if (package[(byte)Data.Ch] != channel) continue;
                    if ((package[(byte)Data.Cmd] != (byte)Command.ReadState) &&
                        (package[(byte)Data.Cmd] != (byte)Command.SendState)) continue;
                    if (package[(byte)Data.Togl] == 0) 
                        needResponse = false;
                    response.Add(package);
                    _queuePackages.RemoveAt(i);
                    break;
                }
                _busy_read = false;
            }
            _busy_send = false;
            if (response.Count == 0) return null;
            return response;
        } // SendCommand(byte[], bool)

        /// <summary>
        /// Обработчик событий при получении данных через com-порт
        /// </summary>
        private void ReceiveDataHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            if (!port.IsOpen) return;
            while (port.BytesToRead > 0)
            {
                _buffer[_bufferPos++] = (byte)port.ReadByte();
                if (_bufferPos >= _packetSize)
                {
                    int crc = 0;
                    for (int i = 0; i < (int)Data.Crc; i++) crc += _buffer[i];
                    if ((_buffer[(byte)Data.Crc] == (byte)(crc & 0x00FF)) &&
                        (_buffer[(byte)Data.St] == 173) && (_buffer[(byte)Data.Sp] == 174))
                    {
                        byte[] buffer = new byte[_packetSize];
                        for (int i = 0; i < _packetSize; i++) buffer[i] = _buffer[i];
                        while (_busy_read) Thread.Sleep(100);
                        _busy_read = true;
                        _queuePackages.Add(buffer);
                        _busy_read = false;
                        OnPackageRead(buffer);
                    }
                    _bufferPos = 0;
                }
            }
        } // ReceiveDataHandler(object, SerialDataReceivedEventArgs)

        /// <summary>
        /// Получение сообщения из буфера принятых сообщений
        /// После успешного чтения сообщение удаляется из буфера
        /// </summary>
        /// <returns>
        /// Success: пакет данных из очереди, принятый от адаптера MTRF-64 USB раньше других
        /// Failure: null
        /// </returns>
        public byte[] GetMessage()
        {
            if (_queuePackages.Count == 0) return null;
            while (_busy_read) Thread.Sleep(100);
            _busy_read = true;
            byte[] buffer = new byte[_packetSize];
            for (int i = 0; i < _packetSize; i++)
                buffer[i] = _queuePackages[0][i];
            _queuePackages.RemoveAt(0);
            _busy_read = false;
            return buffer;
        } // GetMessage()

        /// <summary>
        /// Создание пакета отпраки / получения данных адаптера MTRF-64 USB
        /// </summary>
        /// <param name="mode"> режим работы адаптера </param>
        /// <param name="channel"> номер канала </param>
        /// <param name="command"> код команды </param>
        /// <param name="format"> содержимаое поля FMT </param>
        /// <param name="data"> 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3 </param>
        /// <returns> предварительно заполненный массив из _packetSize байт </returns>
        public byte[] CreatePackage(byte mode, byte channel = 0x00, Command command = 0x00,
            byte format =0x00, uint data = 0x00000000)
        {
            byte[] buffer = new byte[_packetSize];
            buffer[(byte)Data.St] = 171;
            buffer[(byte)Data.Mode] = mode;
            for (int i = (byte)Data.Ctr; i < (byte)Data.Sp; i++) buffer[i] = 0;
            if (mode != (byte)WorkMode.Srv)
            {
                buffer[(byte)Data.Ch] = channel;
                buffer[(byte)Data.Cmd] = (byte)command;
                buffer[(byte)Data.Fmt] = format;
                buffer[(byte)Data.D0] = (byte)(data >> 24);
                buffer[(byte)Data.D1] = (byte)(data >> 16);
                buffer[(byte)Data.D2] = (byte)(data >> 8);
                buffer[(byte)Data.D3] = (byte)data;
                uint crc = 0;
                for (int i = 0; i < (byte)Data.Crc; i++) crc += buffer[i];
                buffer[(byte)Data.Crc] = (byte)(crc & 0xFF);
            }
            buffer[(byte)Data.Sp] = 172;
            return buffer;
        } // CreatePackage(byte, byte, Command, byte, uint)

        /// <summary>
        /// Отправка пакета данных адаптеру MTRF-64 USB
        /// </summary>
        /// <param name="buffer"> пакет данных, подготовленный к отправке адаптеру MTRF-64 USB </param>
        /// <returns>
        ///	Success: true - данные успешно отправлены
        /// Failure: flase - во время отправки данных возникла ошибка
        /// </returns>
        private bool SendPackage(byte[] buffer)
        {
            try
            {
                _serial.Write(buffer, 0, buffer.Length);
            }
            catch
            {
                return false;
            }
            OnPackageSend(buffer);
            return true;
        } // SendPackage(byte[])

        /// <summary>
        /// Загрушка функции обработки отправки и получения данных
        /// </summary>
        private static void Skip(byte[] buffer) {}

        /// <summary>
        /// Отключение от адаптера MTRF-64 USB и закрытие com-порта
        /// </summary>
        public void Disconnect()
        {
            if (!Connected) return;
            if (_handler != null)
                while (_handler.IsAlive) Thread.Sleep(100);
            while ((_busy_read) || (_busy_send)) Thread.Sleep(100);
            _serial.Close();
            _portMTRF64 = "";
            _queuePackages.Clear();
        } // void Disconnect

    } // class nooLite
} // namespace Common
