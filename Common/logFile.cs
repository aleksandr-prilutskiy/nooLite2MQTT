using System;
using System.Collections.Generic;
using System.IO;

namespace Common
{
//===============================================================================================================
//
// Объект для работы с файлом журнала с буфером отложеной записи
// Версия от 15.08.2021
//
//===============================================================================================================
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

//===============================================================================================================
// Name...........:	LogFile
// Description....:	Инициализация объекта с именем файла журнала по умолчанию
// Syntax.........:	new LogFile()
//===============================================================================================================
        public LogFile()
        {
            Init("", "");
        } // LogFile()

//===============================================================================================================
// Name...........:	LogFile
// Description....:	Инициализация объекта
// Syntax.........:	new LogFile(filename)
// Parameters.....:	filename    - имя файла журнала
//===============================================================================================================
        public LogFile(string filename)
        {
            Init("", filename);
        } // LogFile(string)

//===============================================================================================================
// Name...........:	LogFile
// Description....:	Инициализация объекта
// Syntax.........:	new LogFile(dir, filename)
// Parameters.....:	dir         - путь каталога файла журнала
//                  filename    - имя файла журнала
// Remarks .......:	Если путь каталога начинается с "\", то это считается подкаталогом в текущем каталоге
//===============================================================================================================
        public LogFile(string dir, string filename)
        {
            Init(dir, filename);
        } // LogFile(string)

//===============================================================================================================
// Name...........:	Init
// Description....:	Начальная установка объекта
// Syntax.........:	Init(dir, filename)
// Parameters.....:	dir         - путь каталога файла журнала
//                  filename    - имя файла журнала
//===============================================================================================================
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

//===============================================================================================================
// Name...........:	Write
// Description....:	Непосредстенная запись сообщения в журнал (без отложенной записи)
// Syntax.........:	Write(message)
// Parameters.....:	message     - текст сообщения
// Remarks .......:	Если в тексте сообщения первый символ @ - он будет замене на актуальную дату и время
//===============================================================================================================
        public void Write(string message)
        {
            if ((_fileName == "") || (message == "")) return;
            FileInfo fileinfo = new FileInfo(_fileName);
            bool append = (fileinfo?.Length < FileMaxSize);
            if (message.Substring(0, 1) == "@")
                message = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy: ") + message.Substring(1);
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

//===============================================================================================================
// Name...........:	Add
// Description....:	Добавление сообщения в буфер отложенной записи журнала
// Syntax.........:	Add(message)
// Parameters.....:	message     - текст сообщения
// Remarks .......:	Если в тексте сообщения первый символ @ - он будет замене на актуальную дату и время
//===============================================================================================================
        public void Add(string message)
        {
            if ((_fileName == "") || (message == "")) return;
            if (message.Substring(0, 1) == "@")
                message = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy: ") + message.Substring(1);
            while (_busy) ;
            _busy = true;
            _buffer.Add(message);
            _busy = false;
        } // void Add(string)

//===============================================================================================================
// Name...........:	Save
// Description....:	Cохранение буфера отложенной записи в файл журнала
// Syntax.........:	Save()
//===============================================================================================================
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
                    foreach (string record in _buffer)
                        file.WriteLine(record);
                    file.Close();
                    _buffer.Clear();
                    _busy = false;
                    return;
                }
                catch (Exception) { }
            }
            _fileName = "";
            _busy = false;
        } // void Save()
    } // class LogFile
}
