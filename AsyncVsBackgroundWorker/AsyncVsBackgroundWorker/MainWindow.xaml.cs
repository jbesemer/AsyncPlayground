using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AsyncVsBackgroundWorker
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

		public const int Count = 10;
		public const int Delay = 100;

        private async void AsyncTaskButton_Click(object sender, RoutedEventArgs e)
        {
            IProgress<int> progress = new Progress<int>(percentCompleted =>
            {
                AsyncTaskProgressBar.Value = percentCompleted;
            });

            AsyncTaskButton.IsEnabled = false;
			Mouse.OverrideCursor = Cursors.Wait;
            await Task.Run( () =>
            {
                progress.Report(0);
                foreach (var i in Enumerable.Range(1, Count ) )
                {
					// await Task.Delay(Delay);
					Thread.Sleep( Delay );
					progress.Report(i * 100 / Count );
                }
            });
			Mouse.OverrideCursor = null;
			AsyncTaskButton.IsEnabled = true;
        }

        private void BackgroundWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundWorkerButton.IsEnabled = false;
			Mouse.OverrideCursor = Cursors.Wait;
			var backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.DoWork += (s, args) =>
            {
                backgroundWorker.ReportProgress(0);
                foreach (var i in Enumerable.Range(1, Count ) )
                {
                    Thread.Sleep( Delay );
                    backgroundWorker.ReportProgress(i * 100/Count );
                }
            };

            backgroundWorker.ProgressChanged += (s, args) =>
            {
                BackgroundWorkerProgressBar.Value = args.ProgressPercentage;
            };

            backgroundWorker.RunWorkerCompleted += (s, args) =>
            {
				Mouse.OverrideCursor = null;
				BackgroundWorkerButton.IsEnabled = true;
            };

            backgroundWorker.RunWorkerAsync();
        }
    }
}
