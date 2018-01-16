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

            timeoutSlider.Value = Properties.Settings.Default.Timeout;
            prefixTextBox.Text = Properties.Settings.Default.Prefix;
            extraParamsTextBox.Text = Properties.Settings.Default.Parameters;
        }

        private void timeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsInitialized == true)
            {
                sliderValueTextBox.Text = timeoutSlider.Value.ToString() + " ms";
            }
        }

        private void okayButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Timeout = (uint)timeoutSlider.Value;
            Properties.Settings.Default.Prefix = prefixTextBox.Text;
            Properties.Settings.Default.Parameters = extraParamsTextBox.Text;
            this.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
