using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using iCOR3.iSecurityComponent;
using System.Collections;

namespace iMDirectory.iEngineConnectors.iActiveDirectory
{
	/// <summary>
	/// Delivers information about MS AD(DS) configuration.  
	/// </summary>
	public class NativeConfiguration : IDisposable
	{
		#region Constants
		public const string PRIMARY_LDAP_KEY = "objectGuid";
		#endregion

		#region Variables
		private bool bDisposed;

		private Ldap LdapObject
		{
			get;
			set;
		}
		private SchemaAttributes SchemaAttributesObject
		{
			get;
			set;
		}

		public string BaseSearchDn
		{
			get;
			set;
		}
		public string DomainController
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
			private set;
		}
		#endregion

		#region Constructors
		public NativeConfiguration(string ServerFQDN, Credentials SecureCredentials, int Port)
		{
			this.PageSize = 500;
			this.ProtocolVersion = 3;

			this.DomainController = ServerFQDN;
			this.SecureCredentials = SecureCredentials;
			this.Port = Port;
			this.LdapObject = new Ldap(this.DomainController, SecureCredentials, null, this.Port);
			this.SchemaAttributesObject = GetSchemaAttributes();
		}
		public NativeConfiguration(string ServerFQDN, Credentials SecureCredentials) : this(ServerFQDN, SecureCredentials, 389) { }
		#endregion

		#region Public Static Methods
		public static Dictionary<string, object> GetRootDSE(string ServerFQDN, Credentials SecureCredentials, string[] AttributesToLoad)
		{
			try
			{
				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "rootDomainNamingContext", "configurationNamingContext" };
				}

				using (Ldap oLdap = new Ldap(ServerFQDN, SecureCredentials, String.Empty, 389))
				{
					oLdap.SearchScope = SearchScope.Base;
					IEnumerator<Dictionary<string, object>> enumResult = oLdap.RetrieveAttributes(null, AttributesToLoad, false).GetEnumerator();
					if (enumResult.MoveNext())
					{
						return enumResult.Current;
					}
					else
					{
						return null;
					}
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		public static IEnumerable<Dictionary<string, object>> GetForestDomains(string ServerFQDN, Credentials SecureCredentials, string[] AttributesToLoad)
		{
			try
			{
				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "distinguishedName", "nETBIOSName", "dnsRoot" };
				}

				Dictionary<string, object> dicRootDSE = NativeConfiguration.GetRootDSE(
						ServerFQDN,
						SecureCredentials,
						new string[] { "configurationNamingContext" }
						);

				using (Ldap oLdap = new Ldap(ServerFQDN, SecureCredentials, String.Format("CN=Partitions,{0}", ((object[])dicRootDSE["configurationNamingContext"])[0]), 389))
				{
					return oLdap.RetrieveAttributes("(&(systemFlags:1.2.840.113556.1.4.803:=3)(objectClass=crossRef))", AttributesToLoad, false);
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		public static string TrivialTranslateAtribute(DirectoryAttribute daProperty)
		{
			try
			{
				Type tAttribute = daProperty[0].GetType();
				return TrivialTranslateAtribute((object[])daProperty.GetValues(tAttribute));
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}

		public static string TrivialTranslateAtribute(object oProperty)
		{
			try
			{
				return TrivialTranslateAtribute((object[])oProperty);
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}

		public static string TrivialTranslateAtribute(object[] aProperty)
		{
			try
			{
				string sValue = null;
				Type tAttribute = aProperty[0].GetType();

				if (Type.GetTypeCode(tAttribute) == TypeCode.Boolean)
				{
					sValue = String.Format("{0}", aProperty[0]);
				}
				else if (Type.GetTypeCode(tAttribute) == TypeCode.Int32 || Type.GetTypeCode(tAttribute) == TypeCode.Int16)
				{
					sValue = String.Format("{0}", aProperty[0]);
				}
				else if (Type.GetTypeCode(tAttribute) == TypeCode.String)
				{
					foreach (string sVal in aProperty)
					{
						sValue += String.Format("|{0}", sVal);
					}
					sValue = sValue.TrimStart('|');
				}
				return sValue;
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}
		#endregion

		#region Public Instance Methods
		public Dictionary<string, object> RootDSE(string[] AttributesToLoad)
		{
			try
			{
				this.LdapObject.BaseSearchDn = String.Empty;
				this.LdapObject.SearchScope = SearchScope.Base;

				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "rootDomainNamingContext", "configurationNamingContext" };
				}
				else //always retrieve DN
				{
					int iIndex = AttributesToLoad.Length;

					while (iIndex > 0 && !AttributesToLoad[--iIndex].Equals("dnsHostName", StringComparison.OrdinalIgnoreCase)) ;
					if (!AttributesToLoad[iIndex].Equals("dnsHostName", StringComparison.OrdinalIgnoreCase))
					{
						string[] aAttributes = new string[AttributesToLoad.Length + 1];
						aAttributes[0] = "dnsHostName";
						AttributesToLoad.CopyTo(aAttributes, 1);
						AttributesToLoad = aAttributes;
					}
				}

				IEnumerator<Dictionary<string, object>> enumResult = LdapObject.RetrieveAttributes(null, AttributesToLoad, false).GetEnumerator();
				if (enumResult.MoveNext())
				{
					return enumResult.Current;
				}
				else
				{
					return null;
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public Dictionary<string, object> RootDSE()
		{
			return this.RootDSE(null);
		}

		public IEnumerable<Dictionary<string, object>> ForestDomains(string[] AttributesToLoad)
		{
			try
			{
				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "objectGuid", "distinguishedName", "nETBIOSName", "dnsRoot" };
				}
				else //always retrieve DN
				{
					int iIndex = AttributesToLoad.Length;

					while (iIndex > 0 && !AttributesToLoad[--iIndex].Equals("objectGuid", StringComparison.OrdinalIgnoreCase)) ;
					if (!AttributesToLoad[iIndex].Equals("objectGuid", StringComparison.OrdinalIgnoreCase))
					{
						string[] aAttributes = new string[AttributesToLoad.Length + 1];
						aAttributes[0] = "objectGuid";
						AttributesToLoad.CopyTo(aAttributes, 1);
						AttributesToLoad = aAttributes;
					}
				}

				Dictionary<string, object> dicRootDSE = RootDSE(new string[] { "configurationNamingContext" });

				this.LdapObject.BaseSearchDn = String.Format("CN=Partitions,{0}", ((object[])dicRootDSE["configurationNamingContext"])[0]);

				this.LdapObject.SearchScope = SearchScope.Subtree;
				return LdapObject.RetrieveAttributes("(&(systemFlags:1.2.840.113556.1.4.803:=3)(objectClass=crossRef))", AttributesToLoad, false);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public IEnumerable<Dictionary<string, object>> ForestDomains()
		{
			return this.ForestDomains(null);
		}

		public Dictionary<string, object> ServerSite(string[] AttributesToLoad)
		{
			try
			{
				return ServerSite(null, null, AttributesToLoad);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public Dictionary<string, object> ServerSite(string ServerGuid, string[] AttributesToLoad)
		{
			try
			{
				return ServerSite("ObjectGuid", ServerGuid, AttributesToLoad);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public Dictionary<string, object> ServerSite(string AttributeName, string AttributeValue, string[] AttributesToLoad)
		{
			try
			{
				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "objectGuid", "distinguishedName", "name" };
				}
				else //always retrieve DN
				{
					int iIndex = AttributesToLoad.Length;

					while (iIndex > 0 && !AttributesToLoad[--iIndex].Equals("objectGuid", StringComparison.OrdinalIgnoreCase)) ;
					if (!AttributesToLoad[iIndex].Equals("objectGuid", StringComparison.OrdinalIgnoreCase))
					{
						string[] aAttributes = new string[AttributesToLoad.Length + 1];
						aAttributes[0] = "objectGuid";
						AttributesToLoad.CopyTo(aAttributes, 1);
						AttributesToLoad = aAttributes;
					}
				}

				Dictionary<string, object> dicRootDSE = RootDSE(new string[] { "configurationNamingContext", "dnsHostName" });
				if (AttributeName == null || AttributeValue == null)
				{
					AttributeName = "dNSHostName";
					AttributeValue = (string)((object[])dicRootDSE["dnsHostName"])[0];
				}

				this.LdapObject.BaseSearchDn = String.Format("CN=Sites,{0}", ((object[])dicRootDSE["configurationNamingContext"])[0]);

				this.LdapObject.SearchScope = SearchScope.Subtree;
				IEnumerator<Dictionary<string, object>> enumServerResult = LdapObject.RetrieveAttributes(String.Format("(&({0}={1})(objectClass=server))", AttributeName, AttributeValue), new string[] { "distinguishedName", "name" }, false).GetEnumerator();
				if (enumServerResult.MoveNext())
				{
					string sServerName = (string)((object[])enumServerResult.Current["name"])[0];
					string sServerDn = (string)((object[])enumServerResult.Current["distinguishedName"])[0];

					this.LdapObject.BaseSearchDn = sServerDn.Remove(0, String.Format("CN={0},CN=Servers", sServerName).Length + 1);
				}

				LdapObject.SearchScope = SearchScope.Base;
				IEnumerator<Dictionary<string, object>> enumSiteResult = LdapObject.RetrieveAttributes(null, AttributesToLoad, false).GetEnumerator();
				if (enumSiteResult.MoveNext())
				{
					return enumSiteResult.Current;
				}
				else
				{
					return null;
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		public List<Dictionary<string, object>> SiteGlobalCatalogServers(string Site, string[] AttributesToLoad)
		{
			try
			{
				List<Dictionary<string, object>> lResult = new List<Dictionary<string, object>>();
				foreach (Dictionary<string, object> dicRes in ISiteGlobalCatalogServers(Site, AttributesToLoad))
				{
					lResult.Add(dicRes);
				}
				return lResult;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public IEnumerable<Dictionary<string, object>> ISiteGlobalCatalogServers(string Site, string[] AttributesToLoad)
		{
			try
			{
				if (AttributesToLoad == null)
				{
					AttributesToLoad = new string[] { "objectGuid", "dNSHostName", "distinguishedName", "name" };
				}
				else //always retrieve DN
				{
					int iIndex = AttributesToLoad.Length;

					while (iIndex > 0 && !AttributesToLoad[--iIndex].Equals("distinguishedName", StringComparison.OrdinalIgnoreCase)) ;
					if (!AttributesToLoad[iIndex].Equals("distinguishedName", StringComparison.OrdinalIgnoreCase))
					{
						string[] aAttributes = new string[AttributesToLoad.Length + 1];
						aAttributes[0] = "distinguishedName";
						AttributesToLoad.CopyTo(aAttributes, 1);
						AttributesToLoad = aAttributes;
					}
				}

				Dictionary<string, object> dicRootDSE = RootDSE(new string[] { "configurationNamingContext" });

				this.LdapObject.BaseSearchDn = String.Format("CN={0},CN=Sites,{1}", Site, ((object[])dicRootDSE["configurationNamingContext"])[0]);
				this.LdapObject.SearchScope = SearchScope.Subtree;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}

			foreach (Dictionary<string, object> dicServer in LdapObject.RetrieveAttributes("(objectClass=server)", AttributesToLoad, false))
			{
				this.LdapObject.SearchScope = SearchScope.Base;
				this.LdapObject.BaseSearchDn = String.Format("CN=NTDS Settings,{0}", ((object[])dicServer["distinguishedName"])[0]);

				IEnumerator<Dictionary<string, object>> enumServerParams = LdapObject.RetrieveAttributes("(&(options:1.2.840.113556.1.4.803:=1)(objectClass=NTDSDSA))", new string[] { "distinguishedName" }, false).GetEnumerator();
				if (enumServerParams.MoveNext())
				{
					yield return dicServer;
				}
			}
		}

		public SchemaAttributes GetSchemaAttributes()
		{
			return new SchemaAttributes(this.DomainController, this.SecureCredentials);
		}
		#endregion

		#region Private Methods

		#endregion

		#region Classes
		/// <summary>
		/// Class delivers MS AD(DS) attributes definition from AD schema.
		/// Data types defined in AD can be casted to right data types in MS SQL.
		/// </summary>
		public class SchemaAttributes : IDisposable
		{
			#region Variables
			private bool bDisposed;

			private string DomainControler
			{
				get;
				set;
			}

			private Credentials SecureCredentials
			{
				get;
				set;
			}

			public Dictionary<string, AttributeType> AttributeTypes
			{
				get;
				private set;
			}
			#endregion

			#region Constructors
			public SchemaAttributes(string ServerFQDN, Credentials SecureCredentials)
			{
				this.DomainControler = ServerFQDN;
				this.SecureCredentials = SecureCredentials;
				this.AttributeTypes = new Dictionary<string, AttributeType>(StringComparer.OrdinalIgnoreCase);

				this.GetSchemaAttributes();
			}
			#endregion

			#region Private Instance Methods
			/// <summary>
			/// Retrieves MS AD(DS) all attributes definition required for object types casting to corresponding MS SQL DB data type.
			/// </summary>
			private void GetSchemaAttributes()
			{
				try
				{
					string sLdapFilter = @"(&(objectClass=attributeSchema)(oMSyntax=*)(attributeSyntax=*))";

					Dictionary<string, Dictionary<string, AttributeType>> dicAttributeMappings = new Dictionary<string, Dictionary<string, AttributeType>>()
						{
								{"2.5.5.1",		new Dictionary<string, AttributeType>(){{"127",	AttributeType.DNDN}}},
								{"2.5.5.2",		new Dictionary<string, AttributeType>(){{"6",	AttributeType.ObjectIdentifier}}},
								{"2.5.5.3",		new Dictionary<string, AttributeType>(){{"27",	AttributeType.String}}},
								{"2.5.5.4",		new Dictionary<string, AttributeType>(){{"20",	AttributeType.IString}}},
								{"2.5.5.5",		new Dictionary<string, AttributeType>(){{"19",	AttributeType.Printable},
																						{"22",	AttributeType.IA5}}},
								{"2.5.5.6",		new Dictionary<string, AttributeType>(){{"18",	AttributeType.Numeric}}},
								{"2.5.5.7",		new Dictionary<string, AttributeType>(){{"127",	AttributeType.DNBinary}}},
								{"2.5.5.8",		new Dictionary<string, AttributeType>(){{"1",	AttributeType.Boolean}}},
								{"2.5.5.9",		new Dictionary<string, AttributeType>(){{"2",	AttributeType.Integer},
																						{"10",	AttributeType.Enumeration}}},
								{"2.5.5.10",	new Dictionary<string, AttributeType>(){{"4",	AttributeType.Octet}}},
								{"2.5.5.11",	new Dictionary<string, AttributeType>(){{"23",	AttributeType.UTCTime},
																						{"24",	AttributeType.GeneralizedTime}}},
								{"2.5.5.12",	new Dictionary<string, AttributeType>(){{"64",	AttributeType.Unicode}}},
								{"2.5.5.13",	new Dictionary<string, AttributeType>(){{"127",	AttributeType.PresentationAddress}}},
								{"2.5.5.14",	new Dictionary<string, AttributeType>(){{"127",	AttributeType.DNString}}},
								{"2.5.5.15",	new Dictionary<string, AttributeType>(){{"66",	AttributeType.NTSecurityDescriptor}}},
								{"2.5.5.16",	new Dictionary<string, AttributeType>(){{"65",	AttributeType.LargeInteger}}},
								{"2.5.5.17",	new Dictionary<string, AttributeType>(){{"4",	AttributeType.SID}}}
						};

					Dictionary<string, object> dicRootDSE = NativeConfiguration.GetRootDSE(
							this.DomainControler,
							this.SecureCredentials,
							new string[] { "schemaNamingContext" }
							);

					using (Ldap oLdap = new Ldap(this.DomainControler, this.SecureCredentials, String.Format("{0}", ((object[])dicRootDSE["schemaNamingContext"])[0]), 389))
					{
						foreach (Dictionary<string, object> dicRes in oLdap.RetrieveAttributes(sLdapFilter, new string[] { "oMSyntax", "attributeSyntax", "LdapDisplayName" }, false))
						{
							string sAttributeStx = ((object[])dicRes["attributeSyntax"])[0].ToString();
							string sAttributeoMStx = ((object[])dicRes["oMSyntax"])[0].ToString();

							if (dicAttributeMappings.ContainsKey(sAttributeStx) && dicAttributeMappings[sAttributeStx].ContainsKey(sAttributeoMStx))
							{
								AttributeType oAttributeType = dicAttributeMappings[sAttributeStx][sAttributeoMStx];
								string sAttributeName = ((object[])dicRes["LdapDisplayName"])[0].ToString();

								if (this.AttributeTypes.ContainsKey(sAttributeName))
								{
								}
								else
									this.AttributeTypes.Add(sAttributeName, oAttributeType);
							}
						}
					}
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}
			#endregion

			#region Enum
			public enum AttributeType
			{
				String,
				IString,
				ObjectIdentifier,
				DNDN,
				DNBinary,
				DNString,
				Printable,
				IA5,
				Numeric,
				Object,
				Boolean,
				Integer,
				Octet,
				UTCTime,
				GeneralizedTime,
				Enumeration,
				Unicode,
				PresentationAddress,
				NTSecurityDescriptor,
				LargeInteger,
				SID
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
						this.AttributeTypes = null;
					}

					this.bDisposed = true;
				}
			}
			#endregion
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
					if (this.LdapObject != null) this.LdapObject.Dispose();
				}

				this.bDisposed = true;
			}
		}

		#endregion
	}
}
