using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Common
{
//===============================================================================================================
//
// Объект для работы с файлом конфигурации программы
// Версия от 03.09.2021
//
//===============================================================================================================
    public class IniFile
    {
        private const string _defaultFileName = "Config.ini";   // Имя файла конфигурации по умолчанию
        private string _fileName = "";                          // Имя файла конфигурации с полным путем
        private string _encryptionKey = "g360Vdoug0Dl8d71";     // Ключ шифрования паролей
        private string _encryptionIV = "jHP0o90czCkRpM3Z";      // Вектор инициализации шифрования паролей

//===============================================================================================================
// Name...........:	IniFile
// Description....:	Инициализация объекта
// Syntax.........:	new IniFile()
//===============================================================================================================
        public IniFile()
        {
            Init("", "");
        } // IniFile()

//===============================================================================================================
// Name...........:	IniFile
// Description....:	Инициализация объекта с привязкой файла журнала
// Syntax.........:	new IniFile(filename)
// Parameters.....:	filename    - имя ini-файла журнала
//===============================================================================================================
        public IniFile(string filename)
        {
            Init("", filename);
        } // IniFile(string)

//===============================================================================================================
// Name...........:	IniFile
// Description....:	Инициализация объекта с привязкой файла журнала
// Syntax.........:	new IniFile(dir, filename)
// Parameters.....:	dir         - путь каталога файла конфигурации программы
//                  filename    - имя файла журнала
// Remarks .......:	Если путь каталога начинается с "\", то это считается подкаталогом в текущем каталоге
//===============================================================================================================
        public IniFile(string dir, string filename)
        {
            Init(dir, filename);
        } // IniFile(string, string)

//===============================================================================================================
// Name...........:	Init
// Description....:	Начальная установка объекта
// Syntax.........:	Init(dir, filename)
// Parameters.....:	dir         - путь каталога файла конфигурации программы
//                  filename    - имя файла журнала
//===============================================================================================================
        private void Init(string dir, string filename)
        {
            if (filename == "") filename = _defaultFileName;
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (dir.Length > 0)
                path = (dir.Substring(0, 1) == "\\") ? path + dir.Substring(1) : dir;
            if (path.Substring(path.Length - 1) != "\\") path += "\\";
            _fileName = AppDomain.CurrentDomain.BaseDirectory + filename;
            if (!File.Exists(_fileName))
            {
                StreamWriter file = new StreamWriter(_fileName);
                file.Close();
            }
            if (!File.Exists(_fileName)) _fileName = "";
        } // Init(string, string)

//===============================================================================================================
// Name...........:	ReadString
// Description....:	Чтение строки из файла конфигурации программы
// Syntax.........:	ReadString(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение по умолчанию
// Return value(s):	Success:    - значение считанного параметра
//                  Failure:    - значение по умолчанию (value)
//===============================================================================================================
        public string ReadString(string section, string key, string value)
        {
            if (_fileName == "") return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, value, temp, bufferSize, _fileName);
            return temp.ToString();
        } // ReadString(string, string, string)

//===============================================================================================================
// Name...........:	ReadInt
// Description....:	Чтение целочисленного значения из файла конфигурации программы
// Syntax.........:	ReadInt(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение по умолчанию 
// Return value(s):	Success:    - значение считанного параметра
//                  Failure:    - значение по умолчанию (value)
//===============================================================================================================
        public int ReadInt(string section, string key, int value)
        {
            if (_fileName == "") return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, "", temp, bufferSize, _fileName);
            if (!int.TryParse(temp.ToString(), out int result)) result = value;
            return result;
        } // ReadInt(string, string, int)

//===============================================================================================================
// Name...........:	ReadFloat
// Description....:	Чтение числа с плавающей запятой из файла конфигурации программы
// Syntax.........:	ReadFloat(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение по умолчанию 
// Return value(s):	Success:    - значение считанного параметра
//                  Failure:    - значение по умолчанию (value)
//===============================================================================================================
        public float ReadFloat(string section, string key, float value)
        {
            if (_fileName == "") return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, "", temp, bufferSize, _fileName);
            if (!float.TryParse(temp.ToString(), out float result)) result = value;
            return result;
        } // ReadFloat(string, string, int)

//===============================================================================================================
// Name...........:	ReadBool
// Description....:	Чтение логического (boolean) значения из файла конфигурации программы
// Syntax.........:	ReadBool(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение по умолчанию 
// Return value(s):	Success:    - значение считанного параметра
//                  Failure:    - значение по умолчанию (value)
//===============================================================================================================
        public bool ReadBool(string section, string key, bool value)
        {
            return ReadString(section, key, value.ToString()) == true.ToString();
        } // ReadBool(string, string, bool)

//===============================================================================================================
// Name...........:	ReadPassword
// Description....:	Чтение зашифрованной строки из файла конфигурации программы
// Syntax.........:	ReadPassword(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение по умолчанию
// Return value(s):	Success:    - значение считанного параметра
//                  Failure:    - значение по умолчанию (value)
//===============================================================================================================
        public string ReadPassword(string section, string key, string value)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(ReadString(section, key, ""));
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider()
                {
                    Key = new SHA256CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(_encryptionKey)),
                    IV = new MD5CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(_encryptionIV))
                };
                return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(bytes, 0, bytes.Length));
            }
            catch
            {
                return value;
            }
        } // ReadString(string, string, string)

//===============================================================================================================
// Name...........:	WriteString
// Description....:	Запись строки в файл конфигурации программы
// Syntax.........:	WriteString(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение параметра
//===============================================================================================================
        public void WriteString(string section, string key, string value)
        {
            if (_fileName == "") return;
            WritePrivateProfileString(section, key, value, _fileName);
        } // WriteString(string, string, string)

//===============================================================================================================
// Name...........:	WriteBool
// Description....:	Запись значения типа boolean в файл конфигурации программы
// Syntax.........:	WriteBool(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение параметра
//===============================================================================================================
        public void WriteBool(string section, string key, bool value)
        {
            if (_fileName == "") return;
            WritePrivateProfileString(section, key, value.ToString(), _fileName);
        } // WriteBool(string, string, bool)

//===============================================================================================================
// Name...........:	WritePassword
// Description....:	Запись строки в файл конфигурации программы с шифрованием
// Syntax.........:	WritePassword(section, key, value)
// Parameters.....:	section     - имя секции в ini-файле
//                  key         - имя параметра в ini-файле
//                  value       - значение параметра
//===============================================================================================================
        public void WritePassword(string section, string key, string value)
        {
            if (_fileName == "") return;
            string strEncrypt = value;
            if (value != "")
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(value);
                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider()
                    {
                        Key = new SHA256CryptoServiceProvider().ComputeHash(
                            Encoding.UTF8.GetBytes(_encryptionKey)),
                        IV = new MD5CryptoServiceProvider().ComputeHash(
                            Encoding.UTF8.GetBytes(_encryptionIV))
                    };
                    strEncrypt = Convert.ToBase64String(
                        aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length));
                }
                catch
                {
                    strEncrypt = value;
                }
            WritePrivateProfileString(section, key, strEncrypt, _fileName);
        } // WriteString(string, string, string)

        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def,
            StringBuilder retVal, int size, string filePath);

        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int WritePrivateProfileString(string section, string key, string str,
            string filePath);

    } // class IniFile
}
