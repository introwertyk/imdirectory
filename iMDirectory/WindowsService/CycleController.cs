using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

namespace iMDirectory
{
	public partial class CycleController : ServiceBase
	{
		#region variables
		protected EventLog oEventLog;
		private iEngine oEngine;
		#endregion

		#region public methods
		public CycleController()
		{
			InitializeComponent();
			this.oEventLog = new System.Diagnostics.EventLog("Application", ".", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
		}
		#endregion

		#region override methods
		protected override void OnStart(string[] args)
		{
			try
			{
				int iInterval = Convert.ToInt32(ConfigurationManager.AppSettings.Get("interval"));
				this.oTimeController.Interval = 60 * 1000 * iInterval;


				this.oEventLog.WriteEntry(String.Format("Service started with {0} minute(s) interval.", iInterval), EventLogEntryType.Information, 2000);
			}
			catch(Exception eX)
			{
				this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3000);
				this.Stop();
			}
		}

		protected override void OnStop()
		{
			try
			{
				this.oTimeController.Enabled = false;

				this.oEventLog.WriteEntry(String.Format("Service stopped."), EventLogEntryType.Information, 2001);

			}
			catch (Exception eX)
			{
				this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3001);
			}
		}
		#endregion

		#region private methods
		private void OnElapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				this.oTimeController.Enabled = false;
				oEngine = new iEngine();

				this.oEngine.Start();

				this.oTimeController.Enabled = true;
			}
			catch (Exception eX)
			{
				this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3002);
				this.oTimeController.Enabled = true;
			}
		}	
		#endregion
	}
}
