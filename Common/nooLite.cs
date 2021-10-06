using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Timers;

namespace Common
{
//===============================================================================================================
//
// Объект для работы с адаптером nooLite MTRF-64-USB
// Версия от 15.09.2021
//
//===============================================================================================================
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
        } // enum Mode

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
        } // enum

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

        private const int _packetSize = 17;          // Размер пакета записи / чтения адаптера MTRF-64-USB
        public const byte _сhannelCount = 64;        // Всего количество каналов адаптера MTRF-64-USB
        private const uint _timeout = 1000;          // Время ожидания ответа адаптера MTRF-64-USB
        private const byte _modeMtrf64 = (byte)WorkMode.TxF; // Режим работы адаптера MTRF-64-USB
        public string NativePort = "";               // Указать порт адаптер MTRF-64-USB (если пусто - искать)

        private readonly LogFile _fileLog = null;    // Ссылка на объект - журнал работы приложения
        private static SerialPort _serial;           // Порт для обмена данными с адаптером MTRF-64-USB
        private string _portMTRF64 = "";             // Название COM-порта, к которому подключен адаптер MTRF-64-USB
        private byte[] _buffer;                      // Буфер чтения данных (накопление до полного пакета)
        private byte _bufferPos = 0;                 // Количество данных, загруженных в буфер чтения
        private static List<byte[]> _queuePackages;  // Очередь полученных пакетов, ждущих обработки
        private Timer ConnectionTimer;               // Таймер проверки соединения с адаптером и переподключения
        public static bool _busy_send = false;       // Признак того, что адаптер занят (обрабатывает на команду)
        public static bool _busy_read = false;       // Признак того, что буфер полученных пакетов занят

        public delegate void PackageHandler(byte[] buffer);
        public PackageHandler OnPackageSend = Skip;  // Обработчик отправленных сообщений
        public PackageHandler OnPackageRead = Skip;  // Обработчик принятых сообщений

        public bool Connected                        // Признак успешного подключения к адаптеру MTRF-64-USB
        {
            get { return _serial.IsOpen;  } 
        } // Connected

//===============================================================================================================
// Name...........:	nooLite
// Description....:	Инициализация объекта
// Syntax.........:	new nooLite()
//===============================================================================================================
        public nooLite()
        {
            Init();
        } // nooLite()

//===============================================================================================================
// Name...........:	nooLite
// Description....:	Инициализация объекта с привязкой файла журнала
// Syntax.........:	new nooLite(fileLog)
// Parameters.....:	fileLog     - Ссылка на оъект для работы с файлом журнала
//===============================================================================================================
        public nooLite(LogFile fileLog)
        {
            _fileLog = fileLog;
            Init();
        } // nooLite(LogFile)

//===============================================================================================================
// Name...........:	Init
// Description....:	Начальная установка объекта
// Syntax.........:	Init()
//===============================================================================================================
        private void Init()
        {
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
            ConnectionTimer = new Timer
            {
                Interval = 10000,
                AutoReset = true
            };
            ConnectionTimer.Elapsed += Reconnect;
        } // Init()

//===============================================================================================================
// Name...........:	Reconnect
// Description....:	Проверка текущего подключения и переподключение к модулю nooLite MTRF-64 USB
// Syntax.........:	Reconnect()
//===============================================================================================================
        private void Reconnect(object source, ElapsedEventArgs e)
        {
            if (!_serial.IsOpen) Connect();
        } // Reconnect(object, ElapsedEventArgs)

//===============================================================================================================
// Name...........:	Connect
// Description....:	Подключение к модулю nooLite MTRF-64 USB
// Syntax.........:	Connect()
//===============================================================================================================
        public void Connect()
        {
            ConnectionTimer.Enabled = false;
            if (!FindPortMTRF())
                _fileLog?.Write("@Ошибка: Модуль nooLite MTRF-64-USB не обнаружен");
            else if (_serial.IsOpen)
                _fileLog?.Write("@Модуль nooLite MTRF-64-USB подключен к " + _portMTRF64);
            else
                _fileLog?.Write("@Ошибка: Модуль nooLite MTRF-64-USB не подключен");
            ConnectionTimer.Enabled = true;
        } // Connect()

//===============================================================================================================
// Name...........:	FindPortMTRF
// Description....:	Поиск порта, к которому подключен адаптер nooLite MTRF-64 USB
// Syntax.........:	FindPortMTRF()
// Return value(s):	Success:    - true, в PortMTRF64 - номер COM-порта в виде строки, например: "COM1"
//                  Failure:    - false, в PortMTRF64 - пустая строка
// Remarks .......:	Если адаптер найден, com-порт остается открытым
//===============================================================================================================
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
                        (SendCommand(0, (byte)Command.ReadState) != null))
                    {
                        _portMTRF64 = portname;
                        _serial.DiscardInBuffer();
                        return true;
                    }
                    _serial.Close();
                }
                catch (Exception)
                {
                    if (_serial.IsOpen) _serial.Close();
                }
            }
            return false;
        } // bool FindPortMTRF()

//===============================================================================================================
// Name...........:	SendCommand
// Description....:	Отправка команды на устройство
// Syntax.........:	SendCommand(channel, command)
// Parameters.....:	channel     - Номер канала
//                  command     - Код команды
// Return value(s):	Success:    - Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
//                  Failure:    - null
//===============================================================================================================
        public List<byte[]> SendCommand(byte channel, byte command)
        {
            return SendCommand(channel, command, 0x00, 0x00000000);
        } // SendCommand(byte, byte)

//===============================================================================================================
// Name...........:	SendCommand
// Description....:	Отправка команды на устройство
// Syntax.........:	SendCommand(channel, command, data)
// Parameters.....:	channel     - Номер канала
//                  command     - Код команды
//                  data        - 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3
// Return value(s):	Success:    - Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
//                  Failure:    - null
//===============================================================================================================
        public List<byte[]> SendCommand(byte channel, byte command, uint data)
        {
            return SendCommand(channel, command, 0x00, data);
        } // SendCommand(byte, byte, uint)

//===============================================================================================================
// Name...........:	SendCommand
// Description....:	Отправка команды на устройство
// Syntax.........:	SendCommand(channel, command, data)
// Parameters.....:	channel     - Номер канала
//                  command     - Код команды
//                  format      - Содержмое поля FMT
//                  data        - 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3
// Return value(s):	Success:    - Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
//                  Failure:    - null
//===============================================================================================================
        public List<byte[]> SendCommand(byte channel, byte command, byte format, uint data)
        {
            if ((channel < 0) || (channel >= _сhannelCount)) return null;
            return(SendCommand(CreatePackage(_modeMtrf64, channel, command, format, data)));
        } // SendCommand(byte, byte, byte, uint)

//===============================================================================================================
// Name...........:	SendCommand
// Description....:	Отправка команды на устройство
// Syntax.........:	SendCommand(package, answer)
// Parameters.....:	package     - Пакет отпраки данных адаптера MTRF-64 USB
//                  answer      - если true - ждать ответа на команду
// Return value(s):	Success:    - Список пакетов данных, принятых от адаптера MTRF-64 USB в ответ на команду
//                  Failure:    - null
//===============================================================================================================
        public List<byte[]> SendCommand(byte[] package, bool answer = true)
        {
            if (!_serial.IsOpen) return null;
            while (_busy_send) { }
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
                while (_busy_read) ;
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

//===============================================================================================================
// Name...........:	ReceiveDataHandler
// Description....:	Обработчик событий при получении данных через com-порт
//===============================================================================================================
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
                        while (_busy_read) ;
                        _busy_read = true;
                        _queuePackages.Add(buffer);
                        _busy_read = false;
                        OnPackageRead(buffer);
                    }
                    _bufferPos = 0;
                }
            }
        } // ReceiveDataHandler(object, SerialDataReceivedEventArgs)

//===============================================================================================================
// Name...........:	GetMessage
// Description....:	Получение сообщения из буфера принятых сообщений
// Syntax.........:	GetMessage()
// Return value(s):	Success:    - пакет данных из очереди, принятый от адаптера MTRF-64 USB раньше других
//                  Failure:    - null
// Remarks .......:	После успешного чтения сообщение удаляется из буфера
//===============================================================================================================
        public byte[] GetMessage()
        {
            if (_queuePackages.Count == 0) return null;
            while (_busy_read) ;
            _busy_read = true;
            byte[] buffer = new byte[_packetSize];
            for (int i = 0; i < _packetSize; i++)
                buffer[i] = _queuePackages[0][i];
            _queuePackages.RemoveAt(0);
            _busy_read = false;
            return buffer;
        } // GetMessage()

//===============================================================================================================
// Name...........:	CreatePackage
// Description....:	Создание пакета отпраки / получения данных адаптера MTRF-64 USB
// Syntax.........:	CreatePackage(mode, channel, command, format, data)
// Parameters.....:	mode        - режим работы адаптера
//                  channel     - номер канала
//                  command     - код команды
//                  format      - содержимаое поля FMT
//                  data        - 32 битное целое, которым будет заполнены поля D0, D1, D2 и D3
// Return value(s):	Предварительно заполненный массив из _packetSize байт
//===============================================================================================================
        public byte[] CreatePackage(byte mode, byte channel = 0x00, byte command = 0x00,
            byte format =0x00, uint data = 0x00000000)
        {
            byte[] buffer = new byte[_packetSize];
            buffer[(byte)Data.St] = 171;
            buffer[(byte)Data.Mode] = mode;
            for (int i = (byte)Data.Ctr; i < (byte)Data.Sp; i++) buffer[i] = 0;
            if (mode != (byte)WorkMode.Srv)
            {
                buffer[(byte)Data.Ch] = channel;
                buffer[(byte)Data.Cmd] = command;
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
        } // CreatePackage(bool, byte, byte, byte, byte, uint)

//===============================================================================================================
// Name...........:	SendPackage
// Description....:	Отправка пакета данных адаптеру MTRF-64 USB
// Syntax.........:	SendPackage(buffer)
// Parameters.....:	buffer      - пакет данных, подготовленный к отправке адаптеру MTRF-64 USB
// Return value(s):	Success:    - true (данные успешно отправдены)
//                  Failure:    - flase (во время отправки данных возникла ошибка)
//===============================================================================================================
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

//===============================================================================================================
// Name...........:	Skip
// Description....:	Загрушка функции обработки отправки и получения данных
//===============================================================================================================
        private static void Skip(byte[] buffer) {}

//===============================================================================================================
// Name...........:	Close
// Description....:	Закрытие com-порта
// Syntax.........:	Close()
//===============================================================================================================
        public void Close()
        {
            if (!_serial.IsOpen) return;
            _serial.Close();
            _portMTRF64 = "";
            _queuePackages.Clear();
        } // void Close
    } // class nooLite
}
