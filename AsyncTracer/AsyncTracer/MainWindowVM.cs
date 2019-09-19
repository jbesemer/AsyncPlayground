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
	public class MainWindowVM : INotifyPropertyChanged
	{
		#region // INotifyPropertyChanged /////////////////////////////////////

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChangedNoTrace( String name )
		{
			PropertyChanged?.Invoke(
					this,
					new PropertyChangedEventArgs( name ) );
		}

		public void OnPropertyChanged( String name )
		{
			TracePropertyChange( "PropertyChanged: {0}", name );
			OnPropertyChangedNoTrace( name );
		}

		protected void OnPropertyChanged( String name, string value )
		{
			TracePropertyChange( "PropertyChanged: {0} -> {1}", name, value );
			OnPropertyChangedNoTrace( name );
		}

		public void OnPropertyChanged( String name, object value )
		{
			OnPropertyChanged( name, ValueToString( value ) );
		}

		// PropertyChanging ///////////////////////////////////////////

		protected bool OnPropertyChanged<T>(
				ref T value,
				T newValue,
				[CallerMemberName] string name = null,
				bool trace = true )
		{
			if( Equals( value, newValue ) )
				return false;       // unchanged

			if( trace )
				TracePropertyChanging( name, value, newValue );

			value = newValue;
			OnPropertyChangedNoTrace( name );

			return true;    // changed
		}

		#endregion

		public MainWindowVM( SettingsStandard settings )
		{
			SettingsStandard = settings;
		}

		#region // Settings and Events ////////////////////////////////////////

		public SettingsStandard SettingsStandard { get; protected set; }

		public event Action<bool> BusyIndicatorChanged;
		protected void OnBusyIndicatorChanged( bool busy )
		{
			BusyIndicatorChanged?.Invoke( busy );
		}

		public event Action<string> TraceWritten;
		protected void ResultsWriteline( string message )
		{
			TraceWritten?.Invoke( message );
		}

		#endregion

		#region // Export Simulation //////////////////////////////////////////

		private ICommand _ExportHistogramCommand;
		public ICommand ExportHistogramCommand
		{
			get
			{
				return _ExportHistogramCommand
					?? ( _ExportHistogramCommand
						= new RelayCommand<bool>(
							( wait ) => ExportHistorgram( wait ),
							( wait ) => !BusyIndicatorIsActive ) );
							//) );
			}
		}

		private CancellationTokenSource CancellationTokenSource;

		public void ExportHistorgram( bool wait )
		{
			// fire/forget; UI continues running while this Task runs on background thread
			Task.Run( () =>
			{
				try
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
				}
				finally
				{
					BusyIndicatorIsActive = false;
					ResultsWriteline( $"Task exiting" );
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
			Progress<int> prog = new Progress<int>( SetProgress );
			CancellationTokenSource = new CancellationTokenSource();
			CancellationToken ct = CancellationTokenSource.Token;
			BusyIndicatorIsActive = true;
			BusyIndicatorText = "Exporting Histogram";

			ResultsWriteline( $"Launching task..." );
			Task exportTask = PretendExportHistogram( prog, ct );
			ResultsWriteline( "Task is running..." );

			// now that it's all on background and exceptions are caught => no reason NOT to wait.
			// furthermore, if you don't wait the busy indicator goes off before task completes.
			if( NoWaitIsChecked )
				return;

			ResultsWriteline( "Waiting for Task to complete..." );
			double factor = TimeoutIsChecked ? 0.25 : 1.25;
			int expected = (int)( ExpectedRuntime_ms * factor );
			var ok = exportTask.Wait( expected, ct );
			ResultsWriteline( $"Task completes...{( ok ? "" : "Timeout" )}" );

			if( !ok )
			{
				// Wait timed-out, not the task itself;
				// Task is still running; so we need to snuff it
				CancellationTokenSource.Cancel();
			}
		}

		public void SetProgress( int progress )
		{
			switch( progress )
			{
			case -2:
				AbortActionIsAllowed = false;
				ResultsWriteline( "Export Canceled" );
				return;
			case -1:
				AbortActionIsAllowed = false;
				ResultsWriteline( "Export Complete" );
				return;
			case 0:
				AbortActionIsAllowed = true;
				ResultsWriteline( "Starting Export" );
				break;
			}

			ProgressBarValue = (double)progress;
		}

		public const int Iterations = 100;
		public const int Delay = 50;
		public const int ExpectedRuntime_ms = Iterations * Delay;
		public const int ThrowThreshold = 40;

		public Task PretendExportHistogram( IProgress<int> prog, CancellationToken token )
		{
			return Task.Run( () =>
			{
				for( int i = 0; i < Iterations; i++ )
				{
					if( token.IsCancellationRequested )
					{
						prog.Report( -2 );
						return;
					}
					token.ThrowIfCancellationRequested();

					if( ThrowIsChecked && i > ThrowThreshold )
					{
						ResultsWriteline( "Throwing IndexOutOfRangeException..." );
						throw new IndexOutOfRangeException( "burp" );
					}

					prog.Report( i );

					// Pretend work
					//Task.Delay( 200 );
					Thread.Sleep( Delay );
				}

				prog.Report( -1 );
			}, token );
		}

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
			set
			{
				if( OnPropertyChanged( ref _BusyIndicatorIsActive, value ) )
				{
					OnBusyIndicatorChanged( value );
					ResultsWriteline( $"BusyIndicatorIsActive -> {value}" );
				}
			}
		}

		public string _BusyIndicatorText;
		public string BusyIndicatorText
		{
			get { return _BusyIndicatorText; }
			set { OnPropertyChanged( ref _BusyIndicatorText, value ); }
		}


		#endregion

		#region // Trace and ValueToString ////////////////////////////////////

		[Conditional( "TRACE" )]    // also turned-on in project Properties.Build
		public static void Trace( string format, params object[] args )
		{
			Debug.WriteLine( string.Format( format, args ) );
		}

		[Conditional( "TRACE_PROPERTY_CHANGE" )]
		public virtual void TracePropertyChange( string format, params object[] args )
		{
#if !SUPPRESS_TRACE_PROPERTY_CHANGE
			Debug.WriteLine( string.Format( format, args ) );
#endif
		}

		[Conditional( "TRACE_PROPERTY_CHANGING" )]
		public virtual void TracePropertyChanging( string name, object value, object newValue )
		{
			Trace( "PropertyChanging {0}: {1} -> {2}",
				name,
				ValueToString( value ),
				ValueToString( newValue ) );
		}

		public string ValueToString( object value )
		{
			return
				( value == null )
					? "[null]"
					: value.ToString();
		}

		#endregion
	}
}
