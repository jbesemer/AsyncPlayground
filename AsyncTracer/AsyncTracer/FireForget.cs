using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTracer
{
	public class FireForget
	{
		public event Action<bool> IsRunningChanged;
		protected void OnIsRunningChanged( bool busy )
		{
			IsRunningChanged?.Invoke( busy );
		}

		async void FireAndForget( Task task, Action<Exception> ExceptionLogger = null )
		{
			try
			{
				OnIsRunningChanged( true );
				await task;
			}
			catch( Exception ex )
			{
				ExceptionLogger?.Invoke( ex );
			}
			finally
			{
				OnIsRunningChanged( false );
			}
		}

		void FireAndForget( Action action, Action<Exception> ExceptionLogger = null )
		{
			FireAndForget( new Task( action ), ExceptionLogger );
		}
	}
}
