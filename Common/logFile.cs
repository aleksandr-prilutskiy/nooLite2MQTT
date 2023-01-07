using System;
using System.Collections.Generic;
using System.IO;

namespace Common
{
    /// <summary>
    /// Объект для работы с файлом журнала с буфером отложеной записи
    /// </summary>
    /// Версия от 01.01.2023
    public class LogFile
    {
        /// <summary>
        /// Максимальный размер файла (в байтах)
        /// </summary>
        public long FileMaxSize = 10485760;

        /// <summary>
        /// Имя файла журнала по умолчанию
        /// </summary>
        private const string _fileNameDefault = "app.log";

        /// <summary>
        /// Время ожидания записи в файл (в миллисекундах)
        /// </summary>
        private const int _tryTimeout = 1000;

        /// <summary>
        /// Буфер отложенной записи
        /// </summary>
        private List<string> _buffer;

        /// <summary>
        /// Буфер отложенной записи
        /// </summary>
        private bool _busy = false;

        /// <summary>
        /// Проверка доступа журнала для записи
        /// </summary>
        public bool Available
        {
            get { return !string.IsNullOrEmpty(_fileName); }
        } // Available

        /// <summary>
        /// Полный путь и имя файла журнала
        /// </summary>
        public string FileName
        {
            get { return _fileName; }
        } // FileName
        private string _fileName;

        /// <summary>
        /// Инициализация объекта с именем файла журнала по умолчанию
        /// </summary>
        public LogFile()
        {
            Init(string.Empty, string.Empty);
        } // LogFile()

        /// <summary>
        /// Инициализация объекта
        /// </summary>
        /// <param name="filename"> имя файла журнала </param>
        public LogFile(string filename)
        {
            Init(string.Empty, filename);
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
            if (string.IsNullOrEmpty(filename))
                filename = _fileNameDefault;
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(dir))
                path = (dir.Substring(0, 1) == "\\") ? path + dir.Substring(1) : dir;
            if (path.Substring(path.Length - 1) != "\\")
                path += "\\";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            _fileName = path + filename;
            StreamWriter file = null;
            try
            {
                file = new StreamWriter(_fileName, false, System.Text.Encoding.Default);
            }
            catch (Exception)
            {
                _fileName = string.Empty;
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
            if ((string.IsNullOrEmpty(_fileName)) ||
                (string.IsNullOrEmpty(message)))
                return;
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
            _fileName = string.Empty;
        } // void Write(string)

        /// <summary>
        /// Добавление сообщения в буфер отложенной записи журнала
        /// Если в тексте сообщения первый символ @ - он будет замене на актуальную дату и время
        /// </summary>
        /// <param name="message"> текст сообщения </param>
        public void Add(string message)
        {
            if ((string.IsNullOrEmpty(_fileName)) ||
                (string.IsNullOrEmpty(message)))
                return;
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
            if (string.IsNullOrEmpty(_fileName))
                return;
            FileInfo fileinfo = new FileInfo(_fileName);
            bool append = fileinfo?.Length < FileMaxSize;
            while (_busy) ;
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
            _fileName = string.Empty;
            _busy = false;
        } // void Save()
    } // class LogFile
} // namespace Common