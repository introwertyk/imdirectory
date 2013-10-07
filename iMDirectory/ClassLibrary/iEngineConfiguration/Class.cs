using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iMDirectory.iEngineConfiguration
{
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
}
