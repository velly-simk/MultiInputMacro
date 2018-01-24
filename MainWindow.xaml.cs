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
        int currentRunningJobs = 0;
        private delegate void Action();
        bool cancelState = false;


        private class BOX
        {
            public object item;
            public Process process = new Process();
            public int result = 0;
        }

        public MainWindow()
        {
            InitializeComponent();
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
                    if (!listbox_SelectedItems.Items.Contains(file))
                    {
                        listbox_SelectedItems.Items.Add(file);
                    }
                }

                if (Properties.Settings.Default.AutoExecute)
                {
                    executeButton_Click(sender,e);
                }

            }
        }

        private void File_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
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
                    if (!listbox_SelectedItems.Items.Contains(file))
                    {
                        listbox_SelectedItems.Items.Add(file);
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

                for (int elapsedTime = 250; !cancelState && !items.process.HasExited && (timeout == 0 ? true : (elapsedTime < timeout)); elapsedTime += 250)
                {
                    items.process.WaitForExit(250);
                }
                if (!items.process.HasExited)
                {
                    items.process.Kill();
                }

                items.result = items.process.ExitCode;
                e.Result = items;
            }
            catch
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
                lock (listbox_SelectedItems.Items)
                {
                    listbox_SelectedItems.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    listbox_SelectedItems.Items.Remove(item.item)));
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
            List<BOX> jobs = e.Argument as List<BOX>;
            int processed = 0;
            foreach (BOX job in jobs)
            {
                if (cancelState == true)
                {
                    progBar.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        progBar.Value += jobs.Count - processed; // adds to value jobs which loop has not gone over
                    }));
                    break;
                }
                while (currentRunningJobs >= (int)Properties.Settings.Default.MaximumInstances)
                {
                    Thread.Sleep(250);
                }

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += worker_doWork;
                worker.RunWorkerCompleted += worker_complete;
                Interlocked.Increment(ref currentRunningJobs);
                worker.RunWorkerAsync(job);
                Thread.Sleep(100); // ensures multiple instances are not started at the same time up to 100 ms resolution
                ++processed;
            }
        }

        private void executeButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = false;

            if (Properties.Settings.Default.MaximumInstances > 1)
            {
                MessageBoxResult dlg = MessageBox.Show(string.Format("Executing with {0} max instances, are you sure?", Properties.Settings.Default.MaximumInstances),
                    "Multiple Instances warning.", MessageBoxButton.YesNo);
                if (dlg == MessageBoxResult.Yes)
                    confirmed = true;
            }
            else confirmed = true;

            if (!confirmed) return;

            // ui state changes
            cancelState = false;
            button_Execute.IsEnabled = false;
            button_Execute.Visibility = Visibility.Hidden;
            button_Cancel.IsEnabled = true;
            button_Cancel.Visibility = Visibility.Visible;
            button_SelectFiles.IsEnabled = false;
            button_Settings.IsEnabled = false;
            button_ClearList.IsEnabled = false;
            button_DeleteItem.IsEnabled = false;
            listbox_SelectedItems.AllowDrop = false;
            progBar.Maximum = listbox_SelectedItems.Items.Count;
            swapProgBarColors(ref progBar);
            toggleStatusInfoVisibility();
            progBar.Value = progBar.Minimum = 0;
            progBar.Visibility = Visibility.Visible;

            List<BOX> jobs = new List<BOX>();

            // enqueue each job
            foreach (object item in listbox_SelectedItems.Items)
            {
                BOX job = new BOX();

                job.process = new Process();
                job.process.StartInfo.CreateNoWindow = Properties.Settings.Default.NoWindowMode;
                job.process.StartInfo.UseShellExecute = false;
                job.process.StartInfo.FileName = Properties.Settings.Default.ExecutablePath;

                job.process.StartInfo.Arguments = "";
                job.process.StartInfo.Arguments += Properties.Settings.Default.Parameters + " ";
                job.process.StartInfo.Arguments += Properties.Settings.Default.Prefix + "\"";
                job.process.StartInfo.Arguments += item.ToString() + "\"";

                job.process.StartInfo.WorkingDirectory = Properties.Settings.Default.ExecutionDirectory;

                job.item = item;

                jobs.Add(job);
            }

            // give jobs to workers

            BackgroundWorker dispatcher = new BackgroundWorker();
            dispatcher.DoWork += dispatcher_doWork;
            dispatcher.RunWorkerAsync(jobs);

        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            Window abc = new SettingsWindow();
            abc.ShowDialog();
        }

        private void swapProgBarColors (ref ProgressBar a)
        {
            Brush tmp = a.Background;
            a.Background = a.Foreground;
            a.Foreground = tmp;
        }

        private void toggleStatusInfoVisibility()
        {
            if (statusText.Visibility == Visibility.Hidden)
            {
                statusText.Visibility = Visibility.Visible;
                statusDigit1.Visibility = Visibility.Visible;
                statusSlash.Visibility = Visibility.Visible;
                statusDigit2.Visibility = Visibility.Visible;
            }
            else
            {
                statusText.Visibility = Visibility.Hidden;
                statusDigit1.Visibility = Visibility.Hidden;
                statusSlash.Visibility = Visibility.Hidden;
                statusDigit2.Visibility = Visibility.Hidden;
            }
        }

        private void progBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (progBar.Value >= progBar.Maximum)
            {
                button_Execute.IsEnabled = true;
                button_Execute.Visibility = Visibility.Visible;
                button_Cancel.IsEnabled = false;
                button_Cancel.Visibility = Visibility.Hidden;
                button_SelectFiles.IsEnabled = true;
                button_Settings.IsEnabled = true;
                button_ClearList.IsEnabled = true;
                button_DeleteItem.IsEnabled = true;
                listbox_SelectedItems.AllowDrop = true;
                swapProgBarColors(ref progBar);
                toggleStatusInfoVisibility();
                progBar.Value = 0;

            }
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            cancelState = true;
            button_Cancel.IsEnabled = false;
        }

        private void button_ClearList_Click(object sender, RoutedEventArgs e)
        {
            listbox_SelectedItems.Items.Clear();
        }

        private void button_DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            Queue<object> tmp = new Queue<object>();
            foreach (object item in listbox_SelectedItems.SelectedItems)
            {
                tmp.Enqueue(item);
            }
            while (tmp.Count > 0)
            {
                object item = tmp.Dequeue();
                listbox_SelectedItems.Items.RemoveAt(listbox_SelectedItems.Items.IndexOf(item));
            }
        }
    }

}
