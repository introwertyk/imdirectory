using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
	/// <summary>
	/// Connector configuration class. Defines end-systems, connection details and connector dependences on other connectors.
	/// Initiated based on connectors DB configuration.
	/// </summary>
	public class Connector
	{
		#region Variables
		public Guid ConnectorID
		{
			get;
			set;
		}
		public string DomainFQDN
		{
			get;
			set;
		}
		public string Type
		{
			get;
			set;
		}
		public string Category
		{
			get;
			set;
		}
		public string Version
		{
			get;
			set;
		}
		public int Port
		{
			get;
			set;
		}
		public int ProtocolVersion
		{
			get;
			set;
		}
		public int PageSize
		{
			get;
			set;
		}
		public List<Class> ObjectClasses
		{
			get;
			private set;
		}
		public Dictionary<string, object> Configuration
		{
			get;
			private set;
		}
		public Connector ParrentConnector
		{
			get;
			set;
		}
		public List<Connector> ChildConnectors
		{
			get;
			set;
		}
		#endregion

		#region Constructors
		public Connector()
		{
			this.ObjectClasses = new List<Class>();
			this.Configuration = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			this.ChildConnectors = new List<Connector>();
		}
		#endregion

		#region Methods
		/// <summary>
		/// Method tries to retireve child connector.
		/// Child connector is a definition of a connector that is part of parent connector. It represents MS AD(DS) Domain to Forest relationship.
		/// If parent connector covers child connector configuration, child connector configuration can be still overwritten.
		/// </summary>
		public bool TryGetChildConnector(string DomainFQDN, out Connector oConnector)
		{
			try
			{
				bool bFound = false;
				IEnumerator<Connector> enumConnector = this.ChildConnectors.GetEnumerator();
				while (!bFound && enumConnector.MoveNext())
				{
					bFound = enumConnector.Current.DomainFQDN.Equals(DomainFQDN, StringComparison.OrdinalIgnoreCase);
				}

				oConnector = bFound ? enumConnector.Current : null;

				return bFound;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Method merges configuration of child connector with parent connector.
		/// Implements inheritace of generic parent configuration to child configuration.
		/// Child connector is a definition of a connector that is part of parent connector. It represents MS AD(DS) Domain to Forest relationship.
		/// If parent connector covers child connector configuration, child connector configuration can be still overwritten.
		/// </summary>
		public static Connector MergeConnectors(Connector Parent, Connector Child)
		{
			try
			{
				Connector oMerged = new Connector();

				oMerged.DomainFQDN = String.IsNullOrEmpty(Child.DomainFQDN)
									? Parent.DomainFQDN
									: Child.DomainFQDN;

				//inherit connector properties from Parent (full Merge)
				if (String.IsNullOrEmpty(Child.Type))
				{
					oMerged.Type = Parent.Type;
					oMerged.Category = Parent.Category;
					oMerged.Version = Parent.Version;
					oMerged.Port = Parent.Port;
					oMerged.ProtocolVersion = Parent.ProtocolVersion;
					oMerged.PageSize = Parent.PageSize;
				}
				else
				{
					oMerged.Type = Child.Type;
					oMerged.Category = Child.Category;
					oMerged.Version = Child.Version;
					oMerged.Port = Child.Port;
					oMerged.ProtocolVersion = Child.ProtocolVersion;
					oMerged.PageSize = Child.PageSize;
				}

				//inherit ObjectClasses from Parent (no merge)
				if (Child.ObjectClasses.Count > 0)
				{
					oMerged.ObjectClasses.AddRange(Child.ObjectClasses);
				}
				else
				{
					oMerged.ObjectClasses.AddRange(Parent.ObjectClasses);
				}

				//inherit configuration from Parent (full merge)
				foreach (KeyValuePair<string, object> kvConfig in Child.Configuration)
				{
					oMerged.Configuration.Add(kvConfig.Key, kvConfig.Value);
				}

				foreach (KeyValuePair<string, object> kvConfig in Parent.Configuration)
				{
					if (!oMerged.Configuration.ContainsKey(kvConfig.Key))
					{
						oMerged.Configuration.Add(kvConfig.Key, kvConfig.Value);
					}
				}

				//inherits parents/children only from Child object
				if (Child.ChildConnectors.Count > 0)
				{
					oMerged.ChildConnectors.AddRange(Child.ChildConnectors);
				}
				if (Child.ParrentConnector != null)
				{
					oMerged.ParrentConnector = Child.ParrentConnector;
				}

				return oMerged;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		#endregion
	}
}
