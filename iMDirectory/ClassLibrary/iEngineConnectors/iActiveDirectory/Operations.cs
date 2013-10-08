using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

using iMDirectory.iSecurityComponent;

namespace iMDirectory.iEngineConnectors.iActiveDirectory
{
	/// <summary>
	/// Retrieves data from MS AD(DS) casted to correct types accepted for MS SQL DB.
	/// Supports deltas updates based on stored last synchronization state.
	/// Most of the operations are processed asynchronously with multithreading.
	/// </summary>
	public class Operations : IDisposable
	{
		#region Constants
		private const int CONN_TIME_OUT = 600; //seconds
		private const int NUMBER_OF_LDAP_CONN = 5;
		private const int MAX_SRV_POOL_THREADS = 4;
		private const int CPU_CORE_NO_PER_SRV = 4;

		private const string SYNC_METADATA_ATTRIBUTE = "msDS-ReplAttributeMetaData";
		private const string SYNC_METADATA_VALUE = "msDS-ReplValueMetaData";
		#endregion

		#region Variables
		private bool bDisposed;

		private Ldap oLdap;
		private NativeConfiguration.SchemaAttributes oSchemaAttributes;
		#endregion

		#region Constructors
		public Operations(string ServerFQDN, Credentials oSecureCredentials, string BaseDn, Int32 Port)
		{
			try
			{
				this.bDisposed = false;
				this.oLdap = new Ldap(ServerFQDN, oSecureCredentials, BaseDn, Port);
				this.oSchemaAttributes = new NativeConfiguration.SchemaAttributes(ServerFQDN, oSecureCredentials);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		public Operations(string ServerFQDN, string UserName, string Password) : this(ServerFQDN, new Credentials(UserName, Password), null, 389) { }
		public Operations(string ServerFQDN, string UserName, string Password, string BaseDn, Int32 Port) : this(ServerFQDN, new Credentials(UserName, Password), BaseDn, Port) { }
		#endregion

		#region Public Instance Methods
		public ConcurrentQueue<Dictionary<string, string>> GetDirectoryDelta(long HighestCommittedUSN, string LdapFilter, List<string> AttributesToLoad, string Delimiter, bool Deleted)
		{
			try
			{
				ConcurrentQueue<Dictionary<string, string>> fifoSearchResultObjects; ;

				const string BASE_ATTRIBUTE = "distinguishedName";

				if (!AttributesToLoad.Contains(SYNC_METADATA_ATTRIBUTE))
				{
					AttributesToLoad.Add(SYNC_METADATA_ATTRIBUTE);
				}

				string sLdapFilter = String.Format(@"(&(!objectCategory=*)(!sAMAccountType=*)({0}))", LdapFilter);

				if (HighestCommittedUSN > 0)
				{
					sLdapFilter = String.Format(@"(&(uSNChanged>={0})({1}))", HighestCommittedUSN, sLdapFilter);
				}

				using (LdapConnectionsProvider oLdapConnections = new LdapConnectionsProvider(this.oLdap.DomainControllers[0], this.oLdap.SecureCredentials, this.oLdap.BaseSearchDn, this.oLdap.Port))
				{
					fifoSearchResultObjects = new ConcurrentQueue<Dictionary<string, string>>();

					if (!Deleted)
					{
						Parallel.ForEach(this.oLdap.RetrieveAttributes(sLdapFilter, new string[] { BASE_ATTRIBUTE }, Deleted), (dicRes) =>
						{
							try
							{
								Dictionary<string, string> dicAttributesCollection = GetObjectDeltasOfAttributes(oLdapConnections.GetRandomLdapConnectionFromPool(),
									((object[])dicRes[BASE_ATTRIBUTE])[0].ToString(),
									AttributesToLoad.ToArray(),
									HighestCommittedUSN,
									Delimiter);

								if (dicAttributesCollection != null && dicAttributesCollection.Count > 0)
									fifoSearchResultObjects.Enqueue(dicAttributesCollection);
							}
							catch (Exception)
							{
								//failed
							}
							finally
							{

							}
						});
					}
					else
					{
						Parallel.ForEach(this.oLdap.RetrieveAttributes(sLdapFilter, new string[] { iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY }, Deleted), (dicRes) =>
						{
							try
							{
								Dictionary<string, string> dicAttributesCollection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
										{{
											iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY,
											new Guid((byte[])((object[])dicRes[iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY])[0]).ToString()
										}};
								fifoSearchResultObjects.Enqueue(dicAttributesCollection);

							}
							catch (Exception)
							{
								//failed
							}
							finally
							{

							}
						});
					}
				}

				return fifoSearchResultObjects;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		public ConcurrentQueue<LinkingUpdate> GetDirectoryMetaDataDelta(long HighestCommittedUSN, string LdapFilter, List<string> AttributesToLoad)
		{
			try
			{
				ConcurrentQueue<LinkingUpdate> fifoSearchResultObjects; ;

				const string BASE_ATTRIBUTE = "distinguishedName";

				string sLdapFilter = String.Format(@"(&(uSNChanged>={0})({1}))", HighestCommittedUSN, LdapFilter);

				using (LdapConnectionsProvider oLdapConnections = new LdapConnectionsProvider(this.oLdap.DomainControllers[0], this.oLdap.SecureCredentials, this.oLdap.BaseSearchDn, this.oLdap.Port))
				{
					fifoSearchResultObjects = new ConcurrentQueue<LinkingUpdate>();

					Parallel.ForEach(this.oLdap.RetrieveAttributes(sLdapFilter, new string[] { BASE_ATTRIBUTE, iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY }, false), (dicRes) =>
					{
						try
						{
							LinkingUpdate oLinkingUpdate = GetObjectDeltasOfValues(oLdapConnections.GetRandomLdapConnectionFromPool(),
								((object[])dicRes[BASE_ATTRIBUTE])[0].ToString(),
								new HashSet<string>(AttributesToLoad),
								HighestCommittedUSN);

							if (oLinkingUpdate.Linking.Count > 0)
							{
								oLinkingUpdate.IndexingValue = new Guid((byte[])((object[])dicRes[iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY])[0]).ToString();
								fifoSearchResultObjects.Enqueue(oLinkingUpdate);
							}
						}
						catch (Exception)
						{
							//failed
						}
						finally
						{

						}
					});
				}

				return fifoSearchResultObjects;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		public string TranslateAttribute(DirectoryAttribute daProperty, string sDelimiter)
		{
			try
			{
				if (daProperty == null)
				{
					return null;
				}
				Type tpAttribute = daProperty[0].GetType();

				NativeConfiguration.SchemaAttributes.AttributeType enAttributeType;
				if (this.oSchemaAttributes.AttributeTypes.TryGetValue(daProperty.Name, out enAttributeType))
				{
					switch (enAttributeType)
					{
						case NativeConfiguration.SchemaAttributes.AttributeType.SID:
							{
								object[] aValues = daProperty.GetValues(Type.GetType("System.Byte[]"));

								return String.Join(sDelimiter, aValues.Select(aValue => (new SecurityIdentifier((byte[])aValue, 0)).ToString()).ToArray());
							};
						case NativeConfiguration.SchemaAttributes.AttributeType.Octet:
							{
								object[] aValues = daProperty.GetValues(Type.GetType("System.Byte[]"));

								return new Guid((byte[])aValues[0]).ToString();
							};
						case NativeConfiguration.SchemaAttributes.AttributeType.LargeInteger:
							{
								Int64 lVal = Convert.ToInt64(daProperty[0]);

								if (lVal > 0)
								{
									return String.Format("{0:yyyy-MM-dd hh:mm:ss}", DateTime.FromFileTime(lVal));
								}
								return null;
							}
						case NativeConfiguration.SchemaAttributes.AttributeType.GeneralizedTime:
							{
								if (daProperty[0] != null)
								{
									return String.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.ParseExact(daProperty[0].ToString(), @"yyyyMMddHHmmss.f'Z'", null));
								}
								return null;
							}
						//change by adding proper definitions
						default:
							{
								object[] aValues = daProperty.GetValues(tpAttribute);
								return String.Join(sDelimiter, (string[])aValues);
							}
							break;
					}
				}
				else
				{
					switch (Type.GetTypeCode(tpAttribute))
					{
						case TypeCode.Boolean:
							{
								object[] aValues = daProperty.GetValues(tpAttribute);
								return String.Join(sDelimiter, (string[])aValues);
							}
						case TypeCode.Int32:
							{
								object[] aValues = daProperty.GetValues(tpAttribute);
								return String.Join(sDelimiter, (string[])aValues);
							}
						case TypeCode.Int16:
							{
								object[] aValues = daProperty.GetValues(tpAttribute);
								return String.Join(sDelimiter, (string[])aValues);
							}
						case TypeCode.String:
							{
								object[] aValues = daProperty.GetValues(tpAttribute);
								return String.Join(sDelimiter, (string[])aValues);
							}
					}
				}

				return null;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}

		}
		#endregion

		#region Private Instance Methods
		private Dictionary<string, string> GetObjectDeltasOfAttributes(LdapConnection oLdapConnection, string BaseDN, string[] aAttributesToLoad, long iHighestCommittedUSN, string sDelimiter)
		{
			const int RETRY_NO = 7;

			SearchResponse dirRes = null;
			SearchRequest srRequest = null;
			SearchResultEntry srEntry = null;

			Dictionary<string, string> dicProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			srRequest = new SearchRequest(
				BaseDN,
				@"(objectClass=*)",
				System.DirectoryServices.Protocols.SearchScope.Base,
				aAttributesToLoad
				);

			ushort iTries = 0;
			bool bRetry = false;
			do
			{
				try
				{
					iTries++;

					dirRes = (SearchResponse)oLdapConnection.SendRequest(srRequest);
					bRetry = false;
				}
				catch (DirectoryOperationException ex)
				{
					if (iTries % RETRY_NO == 0)
					{
						bRetry = false;
						throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
					}
					else
					{
						bRetry = true;
					}
				}
			} while (bRetry);

			string sServerName = oLdapConnection.SessionOptions.HostName;
			string sDomainFqdn = sServerName.Substring(sServerName.IndexOf('.') + 1);

			if (dirRes.Entries.Count > 0)
			{
				HashSet<string> hsUpdatedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				srEntry = dirRes.Entries[0];

				if (srEntry.Attributes.Contains(SYNC_METADATA_ATTRIBUTE))
				{
					foreach (object oValue in srEntry.Attributes[SYNC_METADATA_ATTRIBUTE].GetValues(Type.GetType("System.String")))
					{
						Dictionary<string, string> dicAttributeReplData = GetXMLNodeInnertext(oValue.ToString(), new string[] { "pszAttributeName", "usnLocalChange" }, true);
						if (Convert.ToInt64(dicAttributeReplData["usnLocalChange"]) > iHighestCommittedUSN)
						{
							hsUpdatedAttributes.Add(dicAttributeReplData["pszAttributeName"]);

							hsUpdatedAttributes.Add("whenChanged");
							if (dicAttributeReplData["pszAttributeName"].Equals("Name", StringComparison.OrdinalIgnoreCase))
							{
								hsUpdatedAttributes.Add("DistinguishedName");
								hsUpdatedAttributes.Add("Path");
							}
							else if (dicAttributeReplData["pszAttributeName"].Equals("ntSecurityDescriptor", StringComparison.OrdinalIgnoreCase))
							{
								hsUpdatedAttributes.Add("selective_authentication");
							}
						}
					}

					foreach (string sAttributeName in aAttributesToLoad)
					{
						if (hsUpdatedAttributes.Contains(sAttributeName))
						{
							string sAttributeValue = this.TranslateAttribute(srEntry.Attributes[sAttributeName], sDelimiter);
							dicProperties.Add(sAttributeName, sAttributeValue);
						}
					}
					if (dicProperties.Count > 0)
					{
						string sAttributeValue = this.TranslateAttribute(srEntry.Attributes[iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY], sDelimiter);
						dicProperties.Add(iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY, sAttributeValue);
					}
				}
			}
			return dicProperties;
		}

		private LinkingUpdate GetObjectDeltasOfValues(LdapConnection oLdapConnection, string BaseDN, HashSet<string> hsAttributesToLoad, long iHighestCommittedUSN)
		{
			const ushort RETRY_NO = 7;

			SearchResponse dirRes = null;
			SearchRequest srRequest = null;
			SearchResultEntry srEntry = null;

			LinkingUpdate oLinkingUpdate = new LinkingUpdate(null);

			string sRange = String.Format(@"{0};range={{0}}-{{1}}", SYNC_METADATA_VALUE);

			int iIndex = 0;
			int iStep = 0;

			string sCurrentRange = String.Format(sRange, iIndex, '*');
			bool bMoreData = true;
			while (bMoreData)
			{

				ushort iTries = 0;
				bool bRetry = false;
				do
				{
					try
					{
						iTries++;

						srRequest = new SearchRequest(
								BaseDN,
								@"(objectClass=*)",
								System.DirectoryServices.Protocols.SearchScope.Base,
								new string[] { sCurrentRange }
								);

						dirRes = (SearchResponse)oLdapConnection.SendRequest(srRequest);
						bRetry = false;
					}
					catch (DirectoryOperationException ex)
					{
						if (iTries % RETRY_NO == 0)
						{
							bRetry = false;
							throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
						}
						else
						{
							bRetry = true;
						}
					}
				} while (bRetry);

				if (dirRes.Entries.Count > 0 && dirRes.Entries[0].Attributes.Count > 0)
				{
					srEntry = dirRes.Entries[0];
					foreach (string sAttr in srEntry.Attributes.AttributeNames)
					{
						foreach (object oValue in srEntry.Attributes[sAttr].GetValues(Type.GetType("System.String")))
						{
							Dictionary<string, string> dicAttributeReplData = GetXMLNodeInnertext(oValue.ToString(), new string[] { "pszAttributeName", "pszObjectDn", "ftimeDeleted", "usnLocalChange" }, true);
							if (hsAttributesToLoad.Contains(dicAttributeReplData["pszAttributeName"]))
							{
								string sLocalChangeUSN = null;
								if (dicAttributeReplData.TryGetValue("usnLocalChange", out sLocalChangeUSN))
								{
									if (Convert.ToInt64(dicAttributeReplData["usnLocalChange"]) > iHighestCommittedUSN)
									{
										AttributeValueUpdate oAttributeValueUpdate = new AttributeValueUpdate();

										if (dicAttributeReplData["ftimeDeleted"].Equals("1601-01-01T00:00:00Z", StringComparison.OrdinalIgnoreCase))
										{
											oAttributeValueUpdate.AddEntry(dicAttributeReplData["pszObjectDn"], AttributeValueUpdate.Action.Link);
											oLinkingUpdate.AddEntry(dicAttributeReplData["pszAttributeName"], oAttributeValueUpdate);
										}
										else
										{
											oAttributeValueUpdate.AddEntry(dicAttributeReplData["pszObjectDn"], AttributeValueUpdate.Action.UnLink);
											oLinkingUpdate.AddEntry(dicAttributeReplData["pszAttributeName"], oAttributeValueUpdate);
										}
									}
								}
							}
							iIndex++;
						}

						if (sAttr.IndexOf('*') > 0)
						{
							bMoreData = false;
						}
						else
						{
							iStep = srEntry.Attributes[sAttr].Count;
							sCurrentRange = String.Format(sRange, iIndex, iIndex + iStep);
						}
					}
				}
				else
				{
					bMoreData = false;
				}
			}
			return oLinkingUpdate;
		}
		#endregion

		#region Private Instance Methods
		private Dictionary<string, string> GetXMLNodeInnertext(string sXmlNodes, string[] aNodeNames, bool bOrderedUnique)
		{
			try
			{
				Dictionary<string, string> dicRes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				if (bOrderedUnique)
				{
					int iStart = -1;
					int iFinish = -1;

					foreach (string sNode in aNodeNames)
					{
						if (sXmlNodes.Length > iFinish)
						{
							iStart = sXmlNodes.IndexOf('<' + sNode + '>', iFinish + 1);

							if (iStart >= 0)
							{
								iStart += (sNode.Length + 2);
								iFinish = sXmlNodes.IndexOf("</" + sNode + '>', iStart);

								dicRes.Add(sNode, sXmlNodes.Substring(iStart, iFinish - iStart));
							}
						}
					}
				}
				else
				{
					foreach (string sNode in aNodeNames)
					{
						int iStart = -1;
						int iFinish = -1;

						iStart = sXmlNodes.IndexOf('<' + sNode + '>', 0);

						if (iStart >= 0)
						{
							iStart += (sNode.Length + 2);
							iFinish = sXmlNodes.IndexOf("</" + sNode + '>', iStart);

							dicRes.Add(sNode, sXmlNodes.Substring(iStart, iFinish - iStart));
						}
					}
				}
				return dicRes;
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}
		#endregion

		#region Classes
		/// <summary>
		/// Opens and closes LDAP connection with end system.
		/// Delivers initiated open connections for optimized processing.
		/// Used for multi-threading where more than one LDAP connection shoudl be used to increase number of simultaneous LDAP requests.
		/// </summary>
		private class LdapConnectionsProvider : IDisposable
		{
			#region Variables
			private bool bDisposed;
			private List<LdapConnection> lLdapConnections;

			private Ldap oLdap;
			#endregion

			#region Constructors

			public LdapConnectionsProvider(string ServerFQDN, Credentials oSecureCredentials, string BaseDn, Int32 Port)
			{
				try
				{
					this.bDisposed = false;
					this.oLdap = new Ldap(ServerFQDN, oSecureCredentials, BaseDn, Port);
					this.OpenLdapConnections(NUMBER_OF_LDAP_CONN);
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}
			public LdapConnectionsProvider(string ServerFQDN, string UserName, string Password) : this(ServerFQDN, new Credentials(UserName, Password), null, 389) { }
			public LdapConnectionsProvider(string ServerFQDN, string UserName, string Password, string BaseDn, Int32 Port) : this(ServerFQDN, new Credentials(UserName, Password), BaseDn, Port) { }
			#endregion

			#region Public Instance Methods
			public LdapConnection GetRandomLdapConnectionFromPool()
			{
				return this.lLdapConnections[new Random().Next(0, this.lLdapConnections.Count - 1)];
			}
			#endregion

			#region Private Instance Methods
			private void OpenLdapConnections(int NumbeOfConnections)
			{
				try
				{
					this.CloseAllOpenLdapConnections();

					this.lLdapConnections = new List<LdapConnection>();
					for (int i = 0; i < NumbeOfConnections; i++)
					{
						try
						{
							LdapConnection oLdapConnection = this.oLdap.OpenLdapConnection();
							oLdapConnection.Bind();
							this.lLdapConnections.Add(oLdapConnection);
						}
						catch
						{
							;
						}
					}
					if (!(this.lLdapConnections.Count > 0))
					{
						throw new Exception("Couldn't establish any LDAP connection with servers!");
					}
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}
			private void CloseAllOpenLdapConnections()
			{
				if (this.lLdapConnections != null)
				{
					foreach (LdapConnection ldapConn in this.lLdapConnections)
					{
						ldapConn.Dispose();
					}
					this.lLdapConnections = null;
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
						CloseAllOpenLdapConnections();
					}

					this.bDisposed = true;
				}
			}
			#endregion
		}

		/// <summary>
		/// Class for linking attribute update retrieved from MS AD(DS).
		/// Allows storying linking updates for further processing and MS SQL DB updates.
		/// </summary>
		public class LinkingUpdate
		{
			#region Variables
			private string sIndexingValue;
			private Dictionary<string, AttributeValueUpdate> dicLinking;

			public string IndexingValue
			{
				get
				{
					return this.sIndexingValue;
				}
				set
				{
					this.sIndexingValue = value;
				}
			}

			public Dictionary<string, AttributeValueUpdate> Linking
			{
				get
				{
					return this.dicLinking;
				}
				set
				{
					this.dicLinking = value;
				}
			}
			#endregion

			#region Constructors
			public LinkingUpdate(string IndexingValue)
			{
				this.sIndexingValue = IndexingValue;
				this.dicLinking = new Dictionary<string, AttributeValueUpdate>(StringComparer.OrdinalIgnoreCase);
			}
			#endregion

			#region Public Methods
			public void AddEntry(string AttributeName, AttributeValueUpdate ValueUpdate)
			{
				if (this.Linking.ContainsKey(AttributeName))
				{
					foreach (KeyValuePair<string, Operations.AttributeValueUpdate.Action> kvUpdate in ValueUpdate.Updates)
					{
						this.Linking[AttributeName].AddEntry(kvUpdate.Key, kvUpdate.Value);
					}
				}
				else
				{
					this.Linking.Add(AttributeName, ValueUpdate);
				}
			}
			#endregion
		}

		/// <summary>
		/// Class for attributes update retrieved from MS AD(DS).
		/// Allows storying attribute updates for further processing and MS SQL DB updates.
		/// </summary>
		public class AttributeValueUpdate
		{
			#region Variables
			private Dictionary<string, Action> dicUpdates;

			public Dictionary<string, Action> Updates
			{
				get
				{
					return this.dicUpdates;
				}
				set
				{
					this.dicUpdates = value;
				}
			}
			#endregion

			#region Constructors
			public AttributeValueUpdate()
			{
				this.dicUpdates = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
			}
			#endregion

			#region Public Methods
			public void AddEntry(string Value, Action Action)
			{
				if (this.dicUpdates.ContainsKey(Value))
				{
					this.dicUpdates[Value] = Action;
				}
				else
				{
					this.dicUpdates.Add(Value, Action);
				}
			}
			#endregion

			#region Private Methods
			#endregion

			#region enum
			public enum Action
			{
				Link,
				UnLink
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

				}

				this.bDisposed = true;
			}
		}
		#endregion
	}
}
