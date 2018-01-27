using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;

namespace MultiInputMacro
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            textBox_ExecutablePath.Text = Properties.Settings.Default.ExecutablePath;

            textBox_ExecuteDirectory.Text = Properties.Settings.Default.ExecutionDirectory;
            textBox_Prefix.Text = Properties.Settings.Default.Prefix;
            textBox_Parameters.Text = Properties.Settings.Default.Parameters;

            slider_Timeout.Value = (int)Properties.Settings.Default.Timeout / 1000;
            slider_MaxInstances.Value = (int)Properties.Settings.Default.MaximumInstances;

            checkBox_NoWindow.IsChecked = Properties.Settings.Default.NoWindowMode;
            checkBox_AutoExec.IsChecked = Properties.Settings.Default.AutoExecute;

            button_Okay.IsEnabled = false;

        }

        private void button_Okay_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ExecutablePath = textBox_ExecutablePath.Text;

            Properties.Settings.Default.ExecutionDirectory = textBox_ExecuteDirectory.Text;
            Properties.Settings.Default.Prefix = textBox_Prefix.Text;
            Properties.Settings.Default.Parameters = textBox_Parameters.Text;

            Properties.Settings.Default.Timeout = (uint)slider_Timeout.Value * 1000;
            Properties.Settings.Default.MaximumInstances = (uint)slider_MaxInstances.Value;

            Properties.Settings.Default.NoWindowMode = (bool)checkBox_NoWindow.IsChecked;
            Properties.Settings.Default.AutoExecute = (bool)checkBox_AutoExec.IsChecked;
            this.Close();
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void textBox_Changed(object sender, TextChangedEventArgs e)
        {
            if (button_Okay != null)
                button_Okay.IsEnabled = true;
        }

        private void slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (button_Okay != null)
                button_Okay.IsEnabled = true;
        }

        private void checkbox_Changed(object sender, RoutedEventArgs e)
        {
            if (button_Okay != null)
                button_Okay.IsEnabled = true;
        }

        private void button_ExecutablePath_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".exe";
            dlg.Filter = "Executable (.exe)|*.exe";
            dlg.Multiselect = false;

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                textBox_ExecutablePath.Text = dlg.FileName;
            }
        }
    }

    public class SliderWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {

            return (double)value - 227;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
