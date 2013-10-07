using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
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
