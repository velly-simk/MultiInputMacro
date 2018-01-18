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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;

namespace MultiInputMacro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void Action();

        int currentRunningJobs = 0,
            maxRunningJobs = 1;

        private class BOX
        {
            public object item;
            public Process process = new Process();
            public int result = 0;
        }

        public MainWindow()
        {
            InitializeComponent();
            autoExecCheckBox.IsChecked = Properties.Settings.Default.AutoExecute;
            executionProgramBox.Text = Properties.Settings.Default.ExecutionProgram;
        }

        ~MainWindow()
        {
            Properties.Settings.Default.Save();
        }

        private void File_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    if (!selectedListBox.Items.Contains(file))
                    {
                        selectedListBox.Items.Add(file);
                    }
                }

                if (autoExecCheckBox.IsChecked == true)
                {
                    executeButton_Click(sender,e);
                }

            }
        }

        private void Executable_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                executionProgramBox.Text = files[0];
            }
        }

        private void File_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        // make separate preview drag overs for exes and inis

        private void executionProgramButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".exe";
            dlg.Filter = "Executable (.exe)|*.exe";
            dlg.Multiselect = false;

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                executionProgramBox.Text = dlg.FileName;
            }
        }

        private void selectedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Multiselect = true;

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    if (!selectedListBox.Items.Contains(file))
                    {
                        selectedListBox.Items.Add(file);
                    }
                }
            }
        }

        private void worker_doWork(object sender, DoWorkEventArgs e)
        {
            BOX items = e.Argument as BOX;

            int timeout = (int)Properties.Settings.Default.Timeout;
            try
            {
                items.process.Start();

                for (int elapsedTime = 100; !items.process.HasExited && (elapsedTime < timeout); elapsedTime += 100)
                {
                    Thread.Sleep(100);
                }
                if (!items.process.HasExited)
                {
                    items.process.Kill();
                }
                items.result = items.process.ExitCode;
                e.Result = items;
            }
            catch (Exception ex)
            {
                items.result = -1;
                e.Result = items;
            }
        }

        private void worker_complete(object sender, RunWorkerCompletedEventArgs e)
        {
            BOX item = e.Result as BOX;
            if (item.result == 0)
            {
                lock (selectedListBox.Items)
                {
                    selectedListBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    selectedListBox.Items.Remove(item.item)));
                }
            }
            lock (progBar)
            {
                progBar.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                ++progBar.Value));
            }
            Interlocked.Decrement(ref currentRunningJobs);
        }

        private void dispatcher_doWork(object sender, DoWorkEventArgs e)
        {
            Queue<BOX> jobs = e.Argument as Queue<BOX>;

            while (jobs.Count > 0)
            {
                BOX job = jobs.Dequeue();
                while (currentRunningJobs >= maxRunningJobs)
                {
                    Thread.Sleep(200);
                }

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += worker_doWork;
                worker.RunWorkerCompleted += worker_complete;
                Interlocked.Increment(ref currentRunningJobs);
                worker.RunWorkerAsync(job);
            }
        }


        private void executeButton_Click(object sender, RoutedEventArgs e)
        {
            // freeze components which could screw up execution.
            executeButton.IsEnabled = false;
            selectedListBox.AllowDrop = false;
            executionProgramButton.IsEnabled = false;
            selectedFilesButton.IsEnabled = false;
            progBar.Maximum = selectedListBox.Items.Count;
            progBar.Minimum = 0;

            Queue<BOX> jobs = new Queue<BOX>();

            // All jobs have these properties.
            Process general = new Process();
            general.StartInfo.CreateNoWindow = false;
            general.StartInfo.UseShellExecute = false;
            general.StartInfo.FileName = executionProgramBox.Text;

            // enqueue each job
            foreach (object item in selectedListBox.Items)
            {
                BOX job = new BOX();

                job.process = general;

                job.process.StartInfo.Arguments = "";
                job.process.StartInfo.Arguments += Properties.Settings.Default.Parameters + " ";
                job.process.StartInfo.Arguments += Properties.Settings.Default.Prefix + "\"";
                job.process.StartInfo.Arguments += item.ToString() + "\"";

                job.item = item;

                jobs.Enqueue(job);
            }

            // give jobs to workers

            BackgroundWorker dispatcher = new BackgroundWorker();
            dispatcher.DoWork += dispatcher_doWork;
            dispatcher.RunWorkerAsync(jobs);

        }


        /*

if (selectedListBox.Items.Count < 1) return;

int TIMEOUT = (int)Properties.Settings.Default.Timeout; // 5 second timeout, remove hard code
Process myProcess = new Process();

myProcess.StartInfo.CreateNoWindow = true;
myProcess.StartInfo.UseShellExecute = false;

// sets has exited to non null value, prevents an exception later
myProcess.StartInfo.FileName = "cmd.exe";
myProcess.Start();
myProcess.Kill();

myProcess.StartInfo.FileName = executionProgramBox.Text;

try
{
   int i = -1; // index of item being processed
   while (true)
   {
       // wait for completion up to timeout time
       for (int elapsedTime = 100; !myProcess.HasExited && elapsedTime < TIMEOUT; elapsedTime+=100)
       {
           Thread.Sleep(100);
       }
       // if process still has not completed, end it;
       if (!myProcess.HasExited)
       {
           myProcess.Kill();
           ++i; // move to next index
       }
       // only if item has been processed, remove it from list
       if (myProcess.ExitCode == 0)
       {
           selectedListBox.Items.RemoveAt(i);
       }
       else
       {
           ++i; // move to next index
       }
       // if index is out of list range, break out of execution loop
       if (!(i < selectedListBox.Items.Count))
       {
           break;
       }
       // execution arguments
       myProcess.StartInfo.Arguments = "";
       myProcess.StartInfo.Arguments += Properties.Settings.Default.Parameters + " ";
       myProcess.StartInfo.Arguments += Properties.Settings.Default.Prefix + "\"";
       myProcess.StartInfo.Arguments += selectedListBox.Items.GetItemAt(i).ToString() + "\"";
       // "-s \"" + selectedListBox.Items.GetItemAt(i).ToString() + "\"";
       // execute process
       myProcess.Start();
   }
}
catch (Exception ex)
{
   MessageBox.Show(ex.Message);
}

}
*/

        private void autoExecCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoExecute = true;
        }

        private void autoExecCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoExecute = false;
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            Window abc = new SettingsWindow();
            abc.ShowDialog();
        }

        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.ExecutionProgram = executionProgramBox.Text;
        }

        private void progBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (progBar.Value >= progBar.Maximum)
            {
                executeButton.IsEnabled = true;
                selectedListBox.AllowDrop = true;
                executionProgramButton.IsEnabled = true;
                selectedFilesButton.IsEnabled = true;
            }
        }
    }

    public class TextBoxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, 
            object parameter, CultureInfo culture) {
            return (double)value - 152;
        }

        public object ConvertBack(object value, Type targetType, 
            object parameter, CultureInfo culture) {
            return null;
        }
    }

    public class ListBoxHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return (double)value - 53;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class ButtonGridHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return (double)value*3 + 15;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
    public class TopGridHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return (double)value - 20;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }

}
