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

		public MainWindowVM( Action<string> resultsWriteline )
		{
			ResultsWriteline = resultsWriteline;
		}

		Action<string> ResultsWriteline;

		#region // Export Simulation //////////////////////////////////////////

		private ICommand _ExportHistogramCommand;
		public ICommand ExportHistogramCommand
		{
			get
			{
				return _ExportHistogramCommand
					?? ( _ExportHistogramCommand
						= new RelayCommand<bool>(
							(wait) => ExportHistorgram( wait ) ) );
			}
		}

		private CancellationTokenSource CancellationTokenSource;

		public void ExportHistorgram( bool waitForCompletion )
		{
			Progress<int> prog = new Progress<int>( SetProgress );
			CancellationTokenSource = new CancellationTokenSource();

			// fire and forget...
			ResultsWriteline( $"Launching task {(waitForCompletion?"Waiting":"Not Waiting")}" );
			Task pretendTask = PretendExportHistogram( prog , CancellationTokenSource.Token );
			ResultsWriteline( "Task is running..." );
			if( waitForCompletion )
			{
				ResultsWriteline( "Waiting for Task to complete..." );
				pretendTask.Wait();
			}
			ResultsWriteline( "UI thread continues..." );
		}

		public void SetProgress( int progress )
		{
			switch( progress )
			{
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

		public Task PretendExportHistogram( IProgress<int> prog, CancellationToken token )
		{
			return Task.Run( () =>
			{
				for( int i = 0; i < 100; i++ )
				{
					if( token.IsCancellationRequested )
					{
						prog.Report( -2 );
						return;
					}
					token.ThrowIfCancellationRequested();

					prog.Report( i );

					// Pretend work
					//Task.Delay( 200 );
					Thread.Sleep( 50 );
				}

				prog.Report( -1 );
			}, token );
		}

#if false
		try
		{
		}
		catch( OperationCanceledException )
		{
			prog.Report( -2 );
		}
#endif

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

		public double _ProgressBarValue;
		public double ProgressBarValue
		{
			get { return _ProgressBarValue; }
			set {
				value = Math.Max( 0, Math.Min( value, 100 ) );
				OnPropertyChanged( ref _ProgressBarValue, value );
			}
		}

		#endregion

		#region // Original Demo //////////////////////////////////////////////

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
