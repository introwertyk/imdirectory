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
		public Dictionary<Guid, Connector> Connectors
		{
			get;
			private set;
		}
		public Dictionary<Guid, Class> Classes
		{
			get;
			private set;
		}
		public Dictionary<Guid, Linking> Linking
		{
			get;
			private set;
		}
		#endregion

		#region Constructors
		public Configuration()
		{
			this.Connectors = new Dictionary<Guid, Connector>();
			this.Classes = new Dictionary<Guid, Class>();
			this.Linking = new Dictionary<Guid, Linking>();
		}
		#endregion
	}
}
