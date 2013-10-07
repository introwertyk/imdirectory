using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
	public class Configuration
	{
		#region Variables
		private Dictionary<int, Connector> dicConnectors;
		private Dictionary<int, Class> dicObjectClasses;
		private Dictionary<int, Linking> dicLinkingAttributes;

		public Dictionary<int, Connector> Connectors
		{
			get
			{
				return this.dicConnectors;
			}
		}
		public Dictionary<int, Class> Classes
		{
			get
			{
				return this.dicObjectClasses;
			}
		}
		public Dictionary<int, Linking> Linking
		{
			get
			{
				return this.dicLinkingAttributes;
			}
		}
		#endregion

		#region Constructors
		public Configuration()
		{
			this.dicConnectors = new Dictionary<int, Connector>();
			this.dicObjectClasses = new Dictionary<int, Class>();
			this.dicLinkingAttributes = new Dictionary<int, Linking>();
		}
		#endregion

		#region Methods
		#endregion
	}
}
