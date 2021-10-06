using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace nooLiteControl
{
    public partial class BindingWindow : Window
    {
//===============================================================================================================
// Name...........:	BindingWindow
// Description....:	Инициализация окна
//===============================================================================================================
        public BindingWindow()
        {
            InitializeComponent();
            Window window = Application.Current.Windows[0];
            Left = window.Left + (window.Width - Width) / 2;
            Top = window.Top + (window.Height - Height) / 2;
        } // BindingWindow()

//===============================================================================================================
// Name...........:	Confirm
// Description....:	Подтверждение привязки устройства
//===============================================================================================================
        private void Confirm(object sender, RoutedEventArgs e)
        {
            Close();
        } // Confirm(object, RoutedEventArgs)

//===============================================================================================================
// Name...........:	Cancel
// Description....:	Отмена привязки устройства
//===============================================================================================================
        private void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        } // Cancel(object, RoutedEventArgs)
    } // class BindingWindow
}
