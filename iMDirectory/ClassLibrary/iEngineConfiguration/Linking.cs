using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
	/// <summary>
	/// Linking configuration class. Defines references between different object classes for linking attribute reporting.
	/// Initiated based on linking DB configuration.
	/// </summary>
	public class Linking
	{
		#region Variables
		public int LinkingAttributeID
		{
			get;
			set;
		}
		public string ForwardLink
		{
			get;
			set;
		}
		public string BackLink
		{
			get;
			set;
		}
		public string LinkedWith
		{
			get;
			set;
		}
		public string TableContext
		{
			get;
			set;
		}
		public List<Class> ForwardLinkClasses
		{
			get;
			set;
		}
		public List<Class> BackLinkClasses
		{
			get;
			set;
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
		/// <summary>
		/// Adds Object Class reference from forward-linked object class to back-linked object class.
		/// Builds object class linking relationship.
		/// </summary>
		public void Add(Class ForwardLinkObjectClass)
		{
			this.ForwardLinkClasses.Add(ForwardLinkObjectClass);
		}
		#endregion
	}
}
