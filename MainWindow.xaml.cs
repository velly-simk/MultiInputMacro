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
using System.Windows.Media.Animation;

namespace MultiInputMacro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int currentRunningJobs = 0;
        private delegate void Action();
        


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

        // make separate preview drag overs for exes and inis


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
            Queue<BOX> jobs = e.Argument as Queue<BOX>;

            while (jobs.Count > 0)
            {
                BOX job = jobs.Dequeue();
                while (currentRunningJobs >= (int)Properties.Settings.Default.MaximumInstances)
                {
                    Thread.Sleep(100);
                }

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += worker_doWork;
                worker.RunWorkerCompleted += worker_complete;
                Interlocked.Increment(ref currentRunningJobs);
                worker.RunWorkerAsync(job);
                Thread.Sleep(100); // ensures multiple instances are not started at the same time up to the ms resolution
            }
        }


        private void executeButton_Click(object sender, RoutedEventArgs e)
        {
            // freeze components which could screw up execution.
            button_Execute.IsEnabled = false;
            listbox_SelectedItems.AllowDrop = false;
            button_SelectFiles.IsEnabled = false;
            button_Settings.IsEnabled = false;
            progBar.Maximum = listbox_SelectedItems.Items.Count;
            swapProgBarColors(ref progBar);
            toggleStatusInfoVisibility();
            progBar.Value = progBar.Minimum = 0;
            progBar.Visibility = Visibility.Visible;


            Queue<BOX> jobs = new Queue<BOX>();

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

                job.item = item;

                jobs.Enqueue(job);
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
                listbox_SelectedItems.AllowDrop = true;
                button_SelectFiles.IsEnabled = true;
                button_Settings.IsEnabled = true;
                swapProgBarColors(ref progBar);
                toggleStatusInfoVisibility();
                progBar.Value = 0;

            }
        }
    }

}
