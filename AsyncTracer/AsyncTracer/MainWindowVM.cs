// #define TRACE	// also turned-on in project Properties.Build
// #define SUPPRESS_TRACE_PROPERTY_CHANGE
// #define TRACE_PROPERTY_CHANGE
// #define TRACE_PROPERTY_CHANGING

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.MVVM;

namespace AsyncTracer
{
	public class MainWindowVM : ViewModelBase
	{
		public SettingsStandard SettingsStandard { get; protected set; }

		public MainWindowVM( SettingsStandard settings )
		{
			SettingsStandard = settings;
		}

		#region // Events /////////////////////////////////////////////////////

		public event Action<bool> IsRunningChanged;
		protected void OnIsRunningChanged( bool busy )
		{
			IsRunningChanged?.Invoke( busy );
		}

		public event Action<string> TraceWritten;
		protected void ResultsWriteline( string message )
		{
			TraceWritten?.Invoke( message );
		}

		#endregion

		// Shared Properties //////////////////////////////////////////

		private CancellationTokenSource CancellationTokenSource;

		#region // Fire Forget Command and Associated /////////////////////////

		private ICommand _FireForgetCommand;
		public ICommand FireForgetCommand
		{
			get
			{
				return _FireForgetCommand
					?? ( _FireForgetCommand
						= new RelayCommand(
							() => FireForget(),
							() => !IsRunning ) );
			}
		}

		public void FireForget()
		{
			// these need to be created on UI thread
			Progress<int> progress = new Progress<int>( SetProgress );
			CancellationTokenSource = new CancellationTokenSource();
			CancellationToken token = CancellationTokenSource.Token;

			// start and detach worker thread
			Task.Run( () => FireForgetAsync( progress, token ) );
		}

		public async Task FireForgetAsync( Progress<int> progress, CancellationToken ct )
		{
			try
			{
				await PretendExportHistogram( progress, ct );
			}
			catch( OperationCanceledException )
			{
				SetStatus( "Export Canceled" );
			}
		}

		#endregion

		#region // Export Command and Associated //////////////////////////////

		// lots of knobs and switches for experimentation

		private ICommand _ExportHistogramCommand;
		public ICommand ExportHistogramCommand
		{
			get
			{
				return _ExportHistogramCommand
					?? ( _ExportHistogramCommand
						= new RelayCommand<bool>(
							( wait ) => ExportHistorgram( wait ),
							( wait ) => !IsRunning ) );
			}
		}

		public void ExportHistorgram( bool wait )
		{
			// fire/forget; UI continues running while this Task runs on background thread
			Task.Run( () =>
			{
				if( NoCatchIsChecked )
				{
					ExportHistorgramNoCatch();
					// after about a minute, this exception is raised:
					// TaskScheduler_UnobservedTaskException AggregateException 
					//		A Task's exception(s) were not observed either by Waiting on the Task 
					//		or accessing its Exception property. As a result, the 
					//		unobserved exception was rethrown by the finalizer thread.
					// AggregateException One or more errors occurred.
					//		IndexOutOfRangeException burp

				}
				else
				{
					ExportHistorgramCatch();
				}
			} );
		}

		public void ExportHistorgramCatch()
		{
			try
			{
				ExportHistorgramNoCatch();
			}
			catch( OperationCanceledException )
			{
				ResultsWriteline( $"Task CANCELED" );
			}
			catch( AggregateException aex )
			{
				ResultsWriteline( $"Task AggregateException" );
				foreach( var ex in aex.InnerExceptions )
				{
					ResultsWriteline( $"    Exception: {ex.GetType().Name}: \n\t{ex.Message}" );
				}
			}
			catch( Exception ex )
			{
				ResultsWriteline( $"Task Exception: {ex.GetType().Name}: \n\t{ex.Message}" );
			}
			finally
			{
			}
		}

		public void ExportHistorgramNoCatch()
		{
			Progress<int> progress = new Progress<int>( SetProgress );
			CancellationTokenSource = new CancellationTokenSource();
			CancellationToken token = CancellationTokenSource.Token;

			ResultsWriteline( $"\nLaunching task..." );
			Task exportTask = PretendExportHistogram( progress, token );
			ResultsWriteline( "Task is running..." );

			// now that it's all on background and exceptions are caught => no reason NOT to wait.
			// furthermore, if you don't wait the busy indicator goes off before task completes.
			if( NoWaitIsChecked )
				return;

			ResultsWriteline( "Waiting for Task to complete..." );
			double factor = TimeoutIsChecked ? 0.25 : 1.25;
			int expected = (int)( ExpectedRuntime_ms * factor );
			var ok = exportTask.Wait( expected, token );
			ResultsWriteline( $"Task completes...{( ok ? "" : "Timeout" )}" );

			if( !ok )
			{
				// Wait timed-out, not the task itself;
				// Task is still running; so we need to snuff it
				CancellationTokenSource.Cancel();
			}
		}

		#endregion

		#region // Core Histgogram Exporter ///////////////////////////////////

		public const int Iterations = 100;
		public const int Delay = 50;
		public const int ExpectedRuntime_ms = Iterations * Delay;
		public const int ThrowThreshold = 40;

		public Task PretendExportHistogram( IProgress<int> progress, CancellationToken token )
		{
			return Task.Run( () =>
			{
				IsRunning = true;

				try
				{
					for( int i = 0; i < Iterations; i++ )
					{
						if( token.IsCancellationRequested )
						{
							ExportCanceled();
							return;
						}
						token.ThrowIfCancellationRequested();

						if( ThrowIsChecked && i > ThrowThreshold )
						{
							ResultsWriteline( "Throwing IndexOutOfRangeException..." );
							throw new IndexOutOfRangeException( "burp" );
						}

						progress.Report( i );

						// Pretend work
						//Task.Delay( 200 );
						Thread.Sleep( Delay );
					}
				}
				finally
				{
					IsRunning = false;
				}
			}, token );
		}

		#endregion

		#region // Commands ///////////////////////////////////////////////////

		private ICommand _AbortActionCommand;
		public ICommand AbortActionCommand
		{
			get
			{
				return _AbortActionCommand
					?? ( _AbortActionCommand
						= new RelayCommand(
							() => AbortAction() ) );
			}
		}

		public bool _AbortActionIsAllowed;
		public bool AbortActionIsAllowed
		{
			get { return _AbortActionIsAllowed; }
			set { OnPropertyChanged( ref _AbortActionIsAllowed, value ); }
		}

		public void AbortAction()
		{
			CancellationTokenSource.Cancel();
			SetStatus( "Export Aborted" );
		}

		private ICommand _PingCommand;
		public ICommand PingCommand
		{
			get
			{
				return _PingCommand
					?? ( _PingCommand
						= new RelayCommand(
							() => Ping() ) );
			}
		}

		public void Ping()
		{
			ResultsWriteline( "Ping!" );
		}

		#endregion

		#region // Checkboxes & Indicators ////////////////////////////////////

		public bool _IsRunning;
		public bool IsRunning
		{
			get { return _IsRunning; }
			set
			{
				if( OnPropertyChanged( ref _IsRunning, value ) )
				{
					if( value )
					{
						AbortActionIsAllowed = true;
						BusyIndicatorText = "Exporting Histogram";
						//BusyIndicatorIsActive = true;
						ResultsWriteline( "Starting Export" );
						SetStatus( "Busy Exporting..." );
					}
					else
					{
						AbortActionIsAllowed = false;
						//BusyIndicatorIsActive = false;
						ResultsWriteline( "Export Complete" );
						SetStatus();
					}

					OnIsRunningChanged( value );
				}
			}
		}

		public bool _ThrowIsChecked;
		public bool ThrowIsChecked
		{
			get { return _ThrowIsChecked; }
			set { OnPropertyChanged( ref _ThrowIsChecked, value ); }
		}

		public bool _NoCatchIsChecked;
		public bool NoCatchIsChecked
		{
			get { return _NoCatchIsChecked; }
			set { OnPropertyChanged( ref _NoCatchIsChecked, value ); }
		}

		public bool _NoWaitIsChecked;
		public bool NoWaitIsChecked
		{
			get { return _NoWaitIsChecked; }
			set { OnPropertyChanged( ref _NoWaitIsChecked, value ); }
		}

		public bool _TimeoutIsChecked;
		public bool TimeoutIsChecked
		{
			get { return _TimeoutIsChecked; }
			set { OnPropertyChanged( ref _TimeoutIsChecked, value ); }
		}

		public double _ProgressBarValue;
		public double ProgressBarValue
		{
			get { return _ProgressBarValue; }
			set {
				value = Math.Max( 0, Math.Min( value, 100 ) );
				OnPropertyChanged( ref _ProgressBarValue, value );
			}
		}

		public bool _BusyIndicatorIsActive;
		public bool BusyIndicatorIsActive
		{
			get { return _BusyIndicatorIsActive; }
			set { OnPropertyChanged( ref _BusyIndicatorIsActive, value ); }
		}

		public string _BusyIndicatorText;
		public string BusyIndicatorText
		{
			get { return _BusyIndicatorText; }
			set { OnPropertyChanged( ref _BusyIndicatorText, value ); }
		}

		public string _StatusText;
		public string StatusText
		{
			get { return _StatusText; }
			set { OnPropertyChanged( ref _StatusText, value ); }
		}

		public void SetStatus( string text = "" )
		{
			StatusText = text;
		}

		public void SetProgress( int progress )
		{
			ProgressBarValue = (double)progress;
		}

		public void ExportCanceled()
		{
			ResultsWriteline( "Export Canceled" );
			IsRunning = false;
		}

		#endregion
	}
}
