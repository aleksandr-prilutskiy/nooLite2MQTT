using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Common
{
    /// <summary>
    /// Объект для работы с файлом конфигурации программы
    /// </summary>
    /// Версия от 01.01.2023
    public class IniFile
    {
        /// <summary>
        /// Имя файла конфигурации по умолчанию
        /// </summary>
        private const string _fileNameDefault = "Config.ini";

        /// <summary>
        /// Имя файла конфигурации с полным путем
        /// </summary>
        private string _fileName = string.Empty;

        /// <summary>
        /// Ключ шифрования паролей
        /// </summary>
        private string _encryptionKey = "g360Vdoug0Dl8d71";

        /// <summary>
        /// Вектор инициализации шифрования паролей
        /// </summary>
        private string _encryptionIV = "jHP0o90czCkRpM3Z";

        /// <summary>
        /// Инициализация объекта с указанием имени файла
        /// </summary>
        /// <param name="filename"> имя файла конфигурации </param>
        public IniFile(string filename = "")
        {
            Init(string.Empty, filename);
        } // IniFile([string])

        /// <summary>
        /// Инициализация объекта с указанием имени файла и каталога
        /// Если путь каталога начинается с "\", то это считается подкаталогом в текущем каталоге
        /// </summary>
        /// <param name="dir"> путь каталога файла конфигурации </param>
        /// <param name="filename"> имя файла конфигурации </param>
        public IniFile(string dir, string filename)
        {
            Init(dir, filename);
        } // IniFile(string, string)

        /// <summary>
        /// Начальная установка объекта
        /// </summary>
        /// <param name="dir"> путь каталога файла конфигурации </param>
        /// <param name="filename"> имя файла конфигурации </param>
        private void Init(string dir, string filename)
        {
            if (string.IsNullOrEmpty(filename))
                filename = _fileNameDefault;
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(dir))
                path = (dir.Substring(0, 1) == "\\") ? path + dir.Substring(1) : dir;
            if (path.Substring(path.Length - 1) != "\\")
                path += "\\";
            _fileName = AppDomain.CurrentDomain.BaseDirectory + filename;
            if (!File.Exists(_fileName))
            {
                StreamWriter file = new StreamWriter(_fileName);
                file.Close();
            }
            if (!File.Exists(_fileName))
                _fileName = string.Empty;
        } // Init(string, string)

        /// <summary>
        /// Чтение строки из файла конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение по умолчанию </param>
        /// <returns> значение считанного параметра или значение по умолчанию </returns>
        public string ReadString(string section, string key, string value = "")
        {
            if (string.IsNullOrEmpty(_fileName))
                return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, value, temp, bufferSize, _fileName);
            return temp.ToString();
        } // ReadString(string, string, string)

        /// <summary>
        /// Чтение целочисленного значения из файла конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение по умолчанию </param>
        /// <returns> значение считанного параметра или значение по умолчанию </returns>
        public int ReadInt(string section, string key, int value = 0)
        {
            if (string.IsNullOrEmpty(_fileName))
                return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, "", temp, bufferSize, _fileName);
            if (!int.TryParse(temp.ToString(), out int result)) result = value;
            return result;
        } // ReadInt(string, string, [int])

        /// <summary>
        /// Чтение числа с плавающей запятой из файла конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение по умолчанию </param>
        /// <returns> значение считанного параметра или значение по умолчанию </returns>
        public float ReadFloat(string section, string key, float value = 0.0f)
        {
            if (string.IsNullOrEmpty(_fileName))
                return value;
            const int bufferSize = 255;
            StringBuilder temp = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, "", temp, bufferSize, _fileName);
            if (!float.TryParse(temp.ToString(), out float result)) result = value;
            return result;
        } // ReadFloat(string, string, [float])

        /// <summary>
        /// Чтение логического (boolean) значения из файла конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение по умолчанию </param>
        /// <returns> значение считанного параметра или значение по умолчанию </returns>
        public bool ReadBool(string section, string key, bool value = false)
        {
            return ReadString(section, key, value.ToString()) == true.ToString();
        } // ReadBool(string, string, [bool])

        /// <summary>
        /// Чтение зашифрованной строки из файла конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение по умолчанию </param>
        /// <returns> значение считанного параметра или значение по умолчанию </returns>
        public string ReadPassword(string section, string key, string value = "")
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
        } // ReadString(string, string, [string])

        /// <summary>
        /// Запись строки в файл конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение параметра </param>
        public void WriteString(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(_fileName))
                return;
            WritePrivateProfileString(section, key, value, _fileName);
        } // WriteString(string, string, string)

        /// <summary>
        /// Запись значения типа boolean в файл конфигурации
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение параметра </param>
        public void WriteBool(string section, string key, bool value)
        {
            if (string.IsNullOrEmpty(_fileName))
                return;
            WritePrivateProfileString(section, key, value.ToString(), _fileName);
        } // WriteBool(string, string, bool)

        /// <summary>
        /// Запись строки в файл конфигурации с шифрованием
        /// </summary>
        /// <param name="section"> имя секции в файле конфигурации </param>
        /// <param name="key"> имя параметра в файле конфигурации </param>
        /// <param name="value"> значение параметра </param>
        public void WritePassword(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(_fileName))
                return;
            string strEncrypt = value;
            if (!string.IsNullOrEmpty(value))
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(value);
                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider()
                    {
                        Key = new SHA256CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(_encryptionKey)),
                        IV = new MD5CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(_encryptionIV))
                    };
                    strEncrypt = Convert.ToBase64String(aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length));
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
        private static extern int WritePrivateProfileString(string section, string key, string str, string filePath);

    } // class IniFile
} // namespace Common