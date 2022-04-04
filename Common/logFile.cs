using System;
using System.Collections.Generic;
using System.IO;

namespace Common
{
    /// <summary>
    /// Объект для работы с файлом журнала с буфером отложеной записи
    /// Версия от 04.04.2022
    /// </summary>
    public class LogFile
    {
        public long FileMaxSize = 10485760;                       // Максимальный размер файла (в байтах)
        private const string _defaultFileName = "app.log";        // Имя файла журнала по умолчанию
        private const int _tryTimeout = 1000;                     // Время попытки записи в файл
        private string _fileName;                                 // Имя файла журнала с полным путем
        private List<string> _buffer;                             // Буфер отложенной записи
        private bool _busy = false; 
        
        public bool Available                                     // Проверка что журнал доступен для записи
        {
            get { return _fileName != ""; }
        } // Available
        
        public string FileName                                    // Полный путь и имя файла журнала
        {
            get { return _fileName; }
        } // FileName

        /// <summary>
        /// Инициализация объекта с именем файла журнала по умолчанию
        /// </summary>
        public LogFile()
        {
            Init("", "");
        } // LogFile()

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="filename"> имя файла журнала </param>
        public LogFile(string filename)
        {
            Init("", filename);
        } // LogFile(string)

        /// <summary>
        /// Инициализация объекта
        /// Если путь каталога начинается с "\", то это считается подкаталогом в текущем каталоге
        /// </summary>
        /// <param name="dir"> путь каталога файла журнала </param>
        /// <param name="filename"> имя файла журнала </param>
        public LogFile(string dir, string filename)
        {
            Init(dir, filename);
        } // LogFile(string, string)

        /// <summary>
        /// Начальная установка объекта
        /// </summary>
        /// <param name="dir"> путь каталога файла журнала </param>
        /// <param name="filename"> имя файла журнала </param>
        private void Init(string dir, string filename)
        {
            _buffer = new List<string>();
            if (filename == "") filename = _defaultFileName;
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (dir.Length > 0)
                path = (dir.Substring(0, 1) == "\\") ? path + dir.Substring(1) : dir;
            if (path.Substring(path.Length - 1) != "\\") path += "\\";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            _fileName = path + filename;
            StreamWriter file = null;
            try
            {
                file = new StreamWriter(_fileName, false, System.Text.Encoding.Default);
            }
            catch (Exception)
            {
                _fileName = "";
            }
            finally
            {
                file?.Close();
            }
        } // Init(string, string)

        /// <summary>
        /// Непосредстенная запись сообщения в журнал (без отложенной записи)
        /// Если в тексте сообщения первый символ @ - он будет замене на актуальную дату и время
        /// </summary>
        /// <param name="message"> текст сообщения </param>
        public void Write(string message)
        {
            if ((_fileName == "") || (message == "")) return;
            FileInfo fileinfo = new FileInfo(_fileName);
            bool append = (fileinfo?.Length < FileMaxSize);
            if (message.Substring(0, 1) == "@")
                message = DateTime.Now.ToString("HH:mm:ss:ffff dd.MM.yyyy: ") + message.Substring(1);
            DateTime timer = DateTime.Now.AddMilliseconds(_tryTimeout);
            while (DateTime.Now < timer)
            {
                try
                {
                    StreamWriter file = new StreamWriter(_fileName, append, System.Text.Encoding.Default);
                    file.WriteLine(message);
                    file.Close();
                    return;
                }
                catch (Exception) {}
            }
            _fileName = "";
        } // void Write(string)

        /// <summary>
        /// Добавление сообщения в буфер отложенной записи журнала
        /// Если в тексте сообщения первый символ @ - он будет замене на актуальную дату и время
        /// </summary>
        /// <param name="message"> текст сообщения </param>
        public void Add(string message)
        {
            if ((_fileName == "") || (message == "")) return;
            if (message.Substring(0, 1) == "@")
                message = DateTime.Now.ToString("HH:mm:ss:ffff dd.MM.yyyy: ") + message.Substring(1);
            while (_busy) ;
            _busy = true;
            _buffer.Add(message);
            _busy = false;
        } // void Add(string)

        /// <summary>
        /// Cохранение буфера отложенной записи в файл журнала
        /// </summary>
        public void Save()
        {
            if (_fileName == "") return;
            FileInfo fileinfo = new FileInfo(_fileName);
            bool append = fileinfo?.Length < FileMaxSize;
            while (_busy);
            _busy = true;
            DateTime timer = DateTime.Now.AddMilliseconds(_tryTimeout);
            while (DateTime.Now < timer)
            {
                try
                {
                    StreamWriter file = new StreamWriter(_fileName, append, System.Text.Encoding.Default);
                    while (_buffer.Count > 0)
                    {
                        file.WriteLine(_buffer[0]);
                        _buffer.RemoveAt(0);
                    }
                    file.Close();
                    _busy = false;
                    return;
                }
                catch (Exception) { }
            }
            _fileName = "";
            _busy = false;
        } // void Save()

    } // class LogFile
} // namespace Common
