using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using iMDirectory.iSecurityComponent;

namespace iMDirectory.iEngineConnectors.iActiveDirectory
{
	/// <summary>
	/// Basic Ldap methods class. Custom LDAP (S.DS.P) libraries implementation.
	/// Supports asynchronous directory data retrieval.  
	/// </summary>
	public class Ldap : IDisposable
	{
		#region Constants
		private const int CONN_TIME_OUT = 600; //seconds
		#endregion

		#region Variables
		private bool bDisposed;
		private string sBaseSearchDn = null;
		private string[] aDcServers = null;
		private Int32 iPortNumber;
		private Int32 iPageSize = 500;
		private Int32 iProtocolVersion = 3;
		private Credentials oSecureCredentials;
		private System.DirectoryServices.Protocols.SearchScope enSearchScope = SearchScope.Subtree;

		public string BaseSearchDn
		{
			get
			{
				return this.sBaseSearchDn;
			}
			set
			{
				this.sBaseSearchDn = value;
			}
		}
		public string[] DomainControllers
		{
			get
			{
				return this.aDcServers;
			}
			set
			{
				this.aDcServers = value;
			}
		}
		public Int32 Port
		{
			get
			{
				return this.iPortNumber;
			}
			set
			{
				this.iPortNumber = value;
			}
		}
		public Int32 PageSize
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
		public Int32 ProtocolVersion
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
		public System.DirectoryServices.Protocols.SearchScope SearchScope
		{
			get
			{
				return this.enSearchScope;
			}
			set
			{
				this.enSearchScope = value;
			}
		}
		public Credentials SecureCredentials
		{
			get
			{
				return this.oSecureCredentials;
			}
			set
			{
				this.SecureCredentials = value;
			}
		}
		#endregion

		#region Constructors
		public Ldap(string ServerFQDN, Credentials oSecureCredentials, string BaseDn, Int32 Port)
		{
			try
			{
				this.bDisposed = false;

				this.oSecureCredentials = oSecureCredentials;

				this.iPortNumber = Port;
				this.BaseSearchDn = BaseDn;

				this.aDcServers = new string[] { ServerFQDN };
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public Ldap(string ServerFQDN, string UserName, string Password) : this(ServerFQDN, new Credentials(UserName, Password), null, 389) { }
		public Ldap(string ServerFQDN, string UserName, string Password, string BaseDn, Int32 Port) : this(ServerFQDN, new Credentials(UserName, Password), BaseDn, Port) { }
		#endregion

		#region Public Instance Methods
		public IEnumerable<Dictionary<string, object>> RetrieveAttributes(string LdapFilter, string[] AttributesToLoad, bool ShowDeleted)
		{
			using (LdapConnection oLdapConnection = this.OpenLdapConnection(this.aDcServers[0], this.oSecureCredentials))
			{
				SearchResponse dirRes = null;
				SearchRequest srRequest = null;
				PageResultRequestControl rcPageRequest = null;
				PageResultResponseControl rcPageResponse = null;

				string sServerName = oLdapConnection.SessionOptions.HostName;
				string sBaseDn = (this.sBaseSearchDn == null)
					? String.Format("DC={0}", sServerName.Substring(sServerName.IndexOf('.') + 1).Replace(".", ",DC="))
					: this.sBaseSearchDn;

				srRequest = new SearchRequest(
							sBaseDn,
							LdapFilter,
							this.SearchScope,
							AttributesToLoad
							);

				if (ShowDeleted)
				{
					srRequest.Controls.Add(new ShowDeletedControl());
				}
				//PAGED
				if (this.iPageSize > 0)
				{
					bool bHasCookies = false;
					rcPageRequest = new PageResultRequestControl();
					rcPageRequest.PageSize = this.iPageSize;

					srRequest.Controls.Add(rcPageRequest);

					do
					{
						try
						{
							dirRes = (SearchResponse)oLdapConnection.SendRequest(srRequest);
							DirectoryControl[] dirControls = dirRes.Controls;

							rcPageResponse = (dirControls.Rank > 0 && dirControls.GetLength(0) > 0) ? (PageResultResponseControl)dirRes.Controls.GetValue(0) : (PageResultResponseControl)null;
						}
						catch (Exception eX)
						{
							throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
						}

						if (dirRes.Entries.Count > 1)
						{
							foreach (SearchResultEntry srEntry in dirRes.Entries)
							{
								Dictionary<string, object> dicProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
								foreach (string sAttribute in AttributesToLoad)
								{
									if (srEntry.Attributes.Contains(sAttribute))
									{
										dicProperties.Add(sAttribute, srEntry.Attributes[sAttribute].GetValues(srEntry.Attributes[sAttribute][0].GetType()));
									}
								}
								yield return dicProperties;
							}


							if (rcPageResponse != null && rcPageResponse.Cookie.Length > 0)
							{
								rcPageRequest.Cookie = rcPageResponse.Cookie;
								bHasCookies = true;
							}
							else
							{
								bHasCookies = false;
							}
						}
						else
						{
							foreach (SearchResultEntry srEntry in dirRes.Entries)
							{
								Dictionary<string, object> dicProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
								foreach (string sAttribute in AttributesToLoad)
								{
									if (srEntry.Attributes.Contains(sAttribute))
									{
										dicProperties.Add(sAttribute, srEntry.Attributes[sAttribute].GetValues(srEntry.Attributes[sAttribute][0].GetType()));
									}
								}
								yield return dicProperties;
								bHasCookies = false;
							}
						}
					}
					while (bHasCookies);
				}
				//NOT PAGED
				else
				{
					try
					{
						dirRes = (SearchResponse)oLdapConnection.SendRequest(srRequest);
					}
					catch (Exception eX)
					{
						throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
					}

					if (dirRes.Entries.Count > 0)
					{
						foreach (SearchResultEntry srEntry in dirRes.Entries)
						{
							Dictionary<string, object> dicProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
							foreach (string sAttribute in AttributesToLoad)
							{
								if (srEntry.Attributes.Contains(sAttribute))
								{
									dicProperties.Add(sAttribute, srEntry.Attributes[sAttribute].GetValues(srEntry.Attributes[sAttribute][0].GetType()));
								}
							}
							yield return dicProperties;
						}
					}
				}

				//dispose
				if (dirRes != null) { dirRes = null; }
				if (srRequest != null) { srRequest = null; }
				if (rcPageRequest != null) { rcPageRequest = null; }
				if (rcPageResponse != null) { rcPageResponse = null; }
			}
		}
		public LdapConnection OpenLdapConnection(string sServerName, Credentials oSecureCredentials)
		{
			try
			{
				LdapDirectoryIdentifier oLdapDirectory = new LdapDirectoryIdentifier(sServerName, this.Port);

				LdapConnection oLdapConnection = new LdapConnection(oLdapDirectory, new NetworkCredential(oSecureCredentials.UserName, oSecureCredentials.Password), AuthType.Basic);
				oLdapConnection.Bind();
				oLdapConnection.Timeout = TimeSpan.FromSeconds(CONN_TIME_OUT);
				oLdapConnection.SessionOptions.TcpKeepAlive = true;
				oLdapConnection.SessionOptions.ProtocolVersion = this.iProtocolVersion;

				oLdapConnection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
				oLdapConnection.AutoBind = false;

				return oLdapConnection;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public LdapConnection OpenLdapConnection()
		{
			try
			{
				return this.OpenLdapConnection(this.aDcServers[0], this.oSecureCredentials);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool bDisposing)
		{
			if (!this.bDisposed)
			{
				if (bDisposing)
				{
				}

				this.bDisposed = true;
			}
		}

		#endregion
	}
}
