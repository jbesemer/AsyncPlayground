using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AsyncVsBackgroundWorker
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void AsyncTaskButton_Click(object sender, RoutedEventArgs e)
        {
            IProgress<int> progress = new Progress<int>(percentCompleted =>
            {
                AsyncTaskProgressBar.Value = percentCompleted;
            });

            AsyncTaskButton.IsEnabled = false;
            await Task.Run(async () =>
            {
                progress.Report(0);
                foreach (var i in Enumerable.Range(1, 4))
                {
                    await Task.Delay(1000);
                    progress.Report(i * 25);
                }
            });
            AsyncTaskButton.IsEnabled = true;
        }

        private void BackgroundWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundWorkerButton.IsEnabled = false;
            var backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.DoWork += (s, args) =>
            {
                backgroundWorker.ReportProgress(0);
                foreach (var i in Enumerable.Range(1, 4))
                {
                    Thread.Sleep(1000);
                    backgroundWorker.ReportProgress(i * 25);
                }
            };

            backgroundWorker.ProgressChanged += (s, args) =>
            {
                BackgroundWorkerProgressBar.Value = args.ProgressPercentage;
            };

            backgroundWorker.RunWorkerCompleted += (s, args) =>
            {
                BackgroundWorkerButton.IsEnabled = true;
            };

            backgroundWorker.RunWorkerAsync();
        }
    }
}
