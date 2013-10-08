using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
	/// <summary>
	/// Object Class definition from Configuration context.
	/// Initiated based on component DB configuration.
	/// </summary>
	public class Class
	{
		#region Variables
		public int ObjectClassID
		{
			get;
			set;
		}
		public string ObjectClass
		{
			get;
			set;
		}
		public string TableContext
		{
			get;
			set;
		}
		public string Filter
		{
			get;
			set;
		}
		public string OtherFilter
		{
			get;
			set;
		}
		public string SearchRoot
		{
			get;
			set;
		}

		public List<Linking> ForwardLinking
		{
			get;
			set;
		}
		public List<Linking> BackwardLinking
		{
			get;
			set;
		}
		#endregion

		#region Constructors
		public Class()
		{
			this.ForwardLinking = new List<Linking>();
			this.BackwardLinking = new List<Linking>();
		}
		#endregion
	}
}
