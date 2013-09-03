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

	public class Connector
	{
		#region Variables
		private int iiConnectorID;
		private string sDomainFQDN;
		private string sType;
		private string sCategory;
		private string sVersion;
		private int iPort;
		private int iProtocolVersion;
		private int iPageSize;
		private List<Class> lObjectClasses;
		private Dictionary<string, object> dicConfiguration;
		private Connector oParrentConnector;
		private List<Connector> lChildConnectors;

		public int iConnectorID
		{
			get
			{
				return this.iiConnectorID;
			}
			set
			{
				this.iiConnectorID = value;
			}
		}
		public string DomainFQDN
		{
			get
			{
				return this.sDomainFQDN;
			}
			set
			{
				this.sDomainFQDN = value;
			}
		}
		public string Type
		{
			get
			{
				return this.sType;
			}
			set
			{
				this.sType = value;
			}
		}
		public string Category
		{
			get
			{
				return this.sCategory;
			}
			set
			{
				this.sCategory = value;
			}
		}
		public string Version
		{
			get
			{
				return this.sVersion;
			}
			set
			{
				this.sVersion = value;
			}
		}
		public int Port
		{
			get
			{
				return this.iPort;
			}
			set
			{
				this.iPort = value;
			}
		}
		public int ProtocolVersion
		{
			get
			{
				return this.iProtocolVersion;
			}
			set
			{
				this.iProtocolVersion = value;
			}
		}
		public int PageSize
		{
			get
			{
				return this.iPageSize;
			}
			set
			{
				this.iPageSize = value;
			}
		}
		public List<Class> ObjectClasses
		{
			get
			{
				return this.lObjectClasses;
			}
		}
		public Dictionary<string, object> Configuration
		{
			get
			{
				return this.dicConfiguration;
			}
		}
		public Connector ParrentConnector
		{
			get
			{
				return this.oParrentConnector;
			}
			set
			{
				this.oParrentConnector = value;
			}
		}
		public List<Connector> ChildConnectors
		{
			get
			{
				return this.lChildConnectors;
			}
			set
			{
				this.lChildConnectors = value;
			}
		}
		#endregion

		#region Constructors
		public Connector()
		{
			this.lObjectClasses = new List<Class>();
			this.dicConfiguration = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			this.lChildConnectors = new List<Connector>();
		}
		#endregion

		#region Methods
		public bool TryGetChildConnector(string DomainFQDN, out Connector oConnector)
		{
			try
			{
				bool bFound = false;
				IEnumerator<Connector> enumConnector = this.lChildConnectors.GetEnumerator();
				while (!bFound && enumConnector.MoveNext() )
				{
					bFound = enumConnector.Current.DomainFQDN.Equals(DomainFQDN, StringComparison.OrdinalIgnoreCase) ;
				}

				oConnector = bFound ? enumConnector.Current : null;

				return bFound;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
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
			catch(Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		#endregion
	}

	public class Class
	{
		#region Variables
		private int iiObjectClassID;
		private string sObjectClass;
		private string sTableContext;
		private string sFilter;
		private string sOtherFilter;
		private string sSearchRoot;
		private List<Linking> lForwardLinking;
		private List<Linking> lBackwardLinking;

		public int iObjectClassID
		{
			get
			{
				return this.iiObjectClassID;
			}
			set
			{
				this.iiObjectClassID = value;
			}
		}
		public string ObjectClass
		{
			get
			{
				return this.sObjectClass;
			}
			set
			{
				this.sObjectClass = value;
			}
		}
		public string TableContext
		{
			get
			{
				return this.sTableContext;
			}
			set
			{
				this.sTableContext = value;
			}
		}
		public string Filter
		{
			get
			{
				return this.sFilter;
			}
			set
			{
				this.sFilter = value;
			}
		}
		public string OtherFilter
		{
			get
			{
				return this.sOtherFilter;
			}
			set
			{
				this.sOtherFilter = value;
			}
		}
		public string SearchRoot
		{
			get
			{
				return this.sSearchRoot;
			}
			set
			{
				this.sSearchRoot = value;
			}
		}

		public List<Linking> ForwardLinking
		{
			get
			{
				return this.lForwardLinking;
			}
			set
			{
				this.lForwardLinking = value;
			}	
		}
		public List<Linking> BackwardLinking
		{
			get
			{
				return this.lBackwardLinking;
			}
			set
			{
				this.lBackwardLinking = value;
			}
		}
		#endregion

		#region Constructors
		public Class()
		{
			this.lForwardLinking = new List<Linking>();
			this.lBackwardLinking = new List<Linking>();
		}
		#endregion

		#region Methods
		#endregion
	}

	public class Linking
	{
		#region Variables
		private int iiLinkingAttributeID;
		private string sForwardLink;
		private string sBackLink;
		private string sLinkedWith;
		private string sTableContext;
		private List<Class> lForwardLinkClasses;
		private List<Class> lBackLinkClasses;

		public int iLinkingAttributeID
		{
			get
			{
				return this.iiLinkingAttributeID;
			}
			set
			{
				this.iiLinkingAttributeID = value;
			}
		}
		public string ForwardLink
		{
			get
			{
				return this.sForwardLink;
			}
			set
			{
				this.sForwardLink = value;
			}
		}
		public string BackLink
		{
			get
			{
				return this.sBackLink;
			}
			set
			{
				this.sBackLink = value;
			}
		}
		public string LinkedWith
		{
			get
			{
				return this.sLinkedWith;
			}
			set
			{
				this.sLinkedWith = value;
			}
		}
		public string TableContext
		{
			get
			{
				return this.sTableContext;
			}
			set
			{
				this.sTableContext = value;
			}
		}
		public List<Class> ForwardLinkClasses
		{
			get
			{
				return this.lForwardLinkClasses;
			}
			set
			{
				this.lForwardLinkClasses = value;
			}
		}
		public List<Class> BackLinkClasses
		{
			get
			{
				return this.lBackLinkClasses;
			}
			set
			{
				this.lBackLinkClasses = value;
			}
		}
		#endregion

		#region Constructors
		public Linking()
		{
			this.ForwardLinkClasses = new List<Class>();
			this.BackLinkClasses = new List<Class>();
		}
		#endregion

		#region Methods
		public void Add(Class ForwardLinkObjectClass)
		{
			this.lForwardLinkClasses.Add(ForwardLinkObjectClass);
		}
		#endregion
	}
}