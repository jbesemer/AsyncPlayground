using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// Add a using directive and a reference for System.Net.Http;
using System.Net.Http;

using SharedLibrary;

namespace AsyncTracer
{
	public partial class MainWindow : Window
		, ILocationAndSize
	{
		public MainWindowVM MainWindowVM;
		public SettingsStandard SettingsStandard;

		public MainWindow()
		{
			InitializeComponent();

			DataContext = MainWindowVM = new MainWindowVM( SettingsStandard );

			MainWindowVM.BusyIndicatorChanged += IndicateBusy;
			MainWindowVM.TraceWritten += ResultsWriteline;

			AppDomain.CurrentDomain.UnhandledException
				+= CurrentDomain_UnhandledException;

			System.Threading.Tasks.TaskScheduler.UnobservedTaskException
				+= TaskScheduler_UnobservedTaskException;

			Application.Current.DispatcherUnhandledException
				+= Current_DispatcherUnhandledException;

		}

		#region // Unhandled Exceptions ///////////////////////////////////////

		private void Current_DispatcherUnhandledException( object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e )
		{
			var ex = e.Exception;
			ResultsWriteline( $">>> Current_DispatcherUnhandledException {DecodeException( ex )}" );
		}

		private void TaskScheduler_UnobservedTaskException( object sender, UnobservedTaskExceptionEventArgs e )
		{
			var ex = e.Exception;
			ResultsWriteline( $">>> TaskScheduler_UnobservedTaskException {DecodeException( ex )}" );
		}

		private void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
		{
			var ex = e.ExceptionObject as Exception;
			ResultsWriteline( $">>> CurrentDomain_UnhandledException {DecodeException( ex )}" );
		}

		public string DecodeException( Exception ex )
		{
			StringBuilder sb = new StringBuilder();
			DecodeException( ex, sb );
			return sb.ToString();
		}

		public void DecodeException( Exception ex, StringBuilder sb, string indent="" )
		{
			sb.AppendLine( $"{indent}{ex.GetType().Name} {ex.Message}" );

			if( ex is AggregateException aex )
			{
				indent += "    ";
				foreach( var ex2 in aex.InnerExceptions )
				{
					DecodeException( ex2, sb, indent );
				}
			}
		}

		#endregion

		#region // VM Event Handlers //////////////////////////////////////////

		public void IndicateBusy( bool value )
		{
			Dispatcher.Invoke( () =>
			{
				Mouse.OverrideCursor = value
					? Cursors.Wait
					: Mouse.OverrideCursor = null;
				CommandManager.InvalidateRequerySuggested();
			} );
		}

		public void ResultsWriteline( string message )
		{
			Dispatcher.Invoke( () =>
			{
				resultsTextBox.Text += message;
				resultsTextBox.Text += "\n";
				resultsTextBox.ScrollToEnd();
			} );
		}

		#endregion

		#region // Window Event Handlers //////////////////////////////////////

		private void Window_Initialized( object sender, EventArgs e )
		{
			// load and initialize window here to avoid "two-step" window opening
			SettingsStandard = new SettingsStandard( this );
			SettingsStandard.Load();
		}

		private void Window_Loaded( object sender, RoutedEventArgs e )
		{
		}

		private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
		{
			SettingsStandard.Save();
		}

		private void ClearMenuItem_Click( object sender, RoutedEventArgs e )
		{
			resultsTextBox.Clear();
			progressBar.Value = 0;
		}

		#endregion

		#region // Original Demo //////////////////////////////////////////////

		private async void startButton_Click(object sender, RoutedEventArgs e)
		{
			// The display lines in the example lead you through the control shifts.
			resultsTextBox.Text += "ONE:   Entering startButton_Click.\r\n" +
				"           Calling AccessTheWebAsync.\r\n";

			Task<int> getLengthTask = AccessTheWebAsync();

			resultsTextBox.Text += "\r\nFOUR:  Back in startButton_Click.\r\n" +
				"           Task getLengthTask is started.\r\n" +
				"           About to await getLengthTask -- no caller to return to.\r\n";

			int contentLength = await getLengthTask;

			resultsTextBox.Text += "\r\nSIX:   Back in startButton_Click.\r\n" +
				"           Task getLengthTask is finished.\r\n" +
				"           Result from AccessTheWebAsync is stored in contentLength.\r\n" +
				"           About to display contentLength and exit.\r\n";

			resultsTextBox.Text +=
				String.Format("\r\nLength of the downloaded string: {0}.\r\n", contentLength);
		}


		async Task<int> AccessTheWebAsync()
		{
			resultsTextBox.Text += "\r\nTWO:   Entering AccessTheWebAsync.";

			// Declare an HttpClient object and increase the buffer size. The default
			// buffer size is 65,536.
			HttpClient client =
				new HttpClient() { MaxResponseContentBufferSize = 1000000 };

			resultsTextBox.Text += "\r\n           Calling HttpClient.GetStringAsync.\r\n";

			// GetStringAsync returns a Task<string>. 
			Task<string> getStringTask = client.GetStringAsync("http://msdn.microsoft.com");

			resultsTextBox.Text += "\r\nTHREE: Back in AccessTheWebAsync.\r\n" +
				"           Task getStringTask is started.";

			// AccessTheWebAsync can continue to work until getStringTask is awaited.

			resultsTextBox.Text +=
				"\r\n           About to await getStringTask and return a Task<int> to startButton_Click.\r\n";

			// Retrieve the website contents when task is complete.
			string urlContents = await getStringTask;

			resultsTextBox.Text += "\r\nFIVE:  Back in AccessTheWebAsync." +
				"\r\n           Task getStringTask is complete." +
				"\r\n           Processing the return statement." +
				"\r\n           Exiting from AccessTheWebAsync.\r\n";

			return urlContents.Length;
		}

		// Sample Output:

		// ONE:   Entering startButton_Click.
		//           Calling AccessTheWebAsync.

		// TWO:   Entering AccessTheWebAsync.
		//           Calling HttpClient.GetStringAsync.

		// THREE: Back in AccessTheWebAsync.
		//           Task getStringTask is started.
		//           About to await getStringTask and return a Task<int> to startButton_Click.

		// FOUR:  Back in startButton_Click.
		//           Task getLengthTask is started.
		//           About to await getLengthTask -- no caller to return to.

		// FIVE:  Back in AccessTheWebAsync.
		//           Task getStringTask is complete.
		//           Processing the return statement.
		//           Exiting from AccessTheWebAsync.

		// SIX:   Back in startButton_Click.
		//           Task getLengthTask is finished.
		//           Result from AccessTheWebAsync is stored in contentLength.
		//           About to display contentLength and exit.

		// Length of the downloaded string: 33946.

		#endregion
	}
}
