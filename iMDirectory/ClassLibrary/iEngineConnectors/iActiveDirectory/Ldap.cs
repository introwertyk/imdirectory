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

		public string BaseSearchDn
		{
			get;
			set;
		}
		public string[] DomainControllers
		{
			get;
			set;
		}
		public Int32 Port
		{
			get;
			set;
		}
		public Int32 PageSize
		{
			get;
			set;
		}
		public Int32 ProtocolVersion
		{
			get;
			set;
		}
		public System.DirectoryServices.Protocols.SearchScope SearchScope
		{
			get;
			set;
		}
		public Credentials SecureCredentials
		{
			get;
			set;
		}
		#endregion

		#region Constructors
		public Ldap(string ServerFQDN, Credentials oSecureCredentials, string BaseDn, Int32 Port)
		{
			try
			{
				this.bDisposed = false;

				this.PageSize = 500;
				this.ProtocolVersion = 3;
				this.SecureCredentials = oSecureCredentials;

				this.Port = Port;
				this.BaseSearchDn = BaseDn;

				this.DomainControllers = new string[] { ServerFQDN };
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
		/// <summary>
		/// Asynchronously retrieves MS AD(DS) objects and casts attributes into dictionaries of key/value pairs. Where key represents AD attribute name and value (object) corresponds to attribute value.
		/// </summary>
		public IEnumerable<Dictionary<string, object>> RetrieveAttributes(string LdapFilter, string[] AttributesToLoad, bool ShowDeleted)
		{
			using (LdapConnection oLdapConnection = this.OpenLdapConnection(this.DomainControllers[0], this.SecureCredentials))
			{
				SearchResponse dirRes = null;
				SearchRequest srRequest = null;
				PageResultRequestControl rcPageRequest = null;
				PageResultResponseControl rcPageResponse = null;

				string sServerName = oLdapConnection.SessionOptions.HostName;
				string sBaseDn = (this.BaseSearchDn == null)
					? String.Format("DC={0}", sServerName.Substring(sServerName.IndexOf('.') + 1).Replace(".", ",DC="))
					: this.BaseSearchDn;

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
				if (this.PageSize > 0)
				{
					bool bHasCookies = false;
					rcPageRequest = new PageResultRequestControl();
					rcPageRequest.PageSize = this.PageSize;

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

		/// <summary>
		/// Opens new LDAP connection with end-server.
		/// </summary>
		public LdapConnection OpenLdapConnection(string sServerName, Credentials oSecureCredentials)
		{
			try
			{
				LdapDirectoryIdentifier oLdapDirectory = new LdapDirectoryIdentifier(sServerName, this.Port);

				LdapConnection oLdapConnection = new LdapConnection(oLdapDirectory, new NetworkCredential(oSecureCredentials.UserName, oSecureCredentials.Password), AuthType.Basic);
				oLdapConnection.Bind();
				oLdapConnection.Timeout = TimeSpan.FromSeconds(CONN_TIME_OUT);
				oLdapConnection.SessionOptions.TcpKeepAlive = true;
				oLdapConnection.SessionOptions.ProtocolVersion = this.ProtocolVersion;

				oLdapConnection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
				oLdapConnection.AutoBind = false;

				return oLdapConnection;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Opens new LDAP connection with end-server.
		/// </summary>
		public LdapConnection OpenLdapConnection()
		{
			try
			{
				return this.OpenLdapConnection(this.DomainControllers[0], this.SecureCredentials);
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
