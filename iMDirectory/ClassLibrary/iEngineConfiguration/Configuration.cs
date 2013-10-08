using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
	/// <summary>
	/// Component configuration class.
	/// Initiated based on complete component DB configuration.
	/// </summary>
	public class Configuration
	{
		#region Variables
		public Dictionary<int, Connector> Connectors
		{
			get;
			private set;
		}
		public Dictionary<int, Class> Classes
		{
			get;
			private set;
		}
		public Dictionary<int, Linking> Linking
		{
			get;
			private set;
		}
		#endregion

		#region Constructors
		public Configuration()
		{
			this.Connectors = new Dictionary<int, Connector>();
			this.Classes = new Dictionary<int, Class>();
			this.Linking = new Dictionary<int, Linking>();
		}
		#endregion
	}
}
