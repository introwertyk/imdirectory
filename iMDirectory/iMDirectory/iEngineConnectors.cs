using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

using iMDirectory.iSecurityComponent;
using System.Collections;

namespace iMDirectory.iEngineConnectors
{
	namespace iSqlDatabase
	{
		public class Sql : IDisposable
		{
			#region Variables
			private bool bDisposed;

			private int iCommandTimeout = 300;
			private string sConnectionString;
			private System.Data.SqlClient.SqlConnection oSqlConnection;


			public int CommandTimeout
			{
				get
				{
					return this.iCommandTimeout;
				}
				set
				{
					this.iCommandTimeout = value;
				}
			}
			public string ConnectionString
			{
				get
				{
					return this.sConnectionString;
				}
				set
				{
					this.sConnectionString = value;
				}
			}
			public System.Data.SqlClient.SqlConnection sqlConnection
			{
				get
				{
					return this.oSqlConnection;
				}
				set
				{
					this.oSqlConnection = value;
				}
			}
		

			#endregion

			#region Constructors
			public Sql(string SqlConnectionString)
			{
				this.bDisposed = false;
				this.sConnectionString = SqlConnectionString;
			}
			public Sql(string SqlConnectionString, int CommandTimeout)
			{
				this.bDisposed = false;
				this.iCommandTimeout = CommandTimeout;
				this.sConnectionString = SqlConnectionString;
			}
			public Sql(System.Data.SqlClient.SqlConnection sqlConnection)
			{
				this.bDisposed = false;
				this.sqlConnection = sqlConnection;
				if (this.sqlConnection.State != ConnectionState.Open) this.sqlConnection.Open();
			}
			public Sql(System.Data.SqlClient.SqlConnection sqlConnection, int CommandTimeout)
			{
				this.bDisposed = false;
				this.iCommandTimeout = CommandTimeout;
				this.sqlConnection = sqlConnection;
				if (this.sqlConnection.State != ConnectionState.Open) this.sqlConnection.Open();
			}
			#endregion

			#region Public Methods
			public IEnumerable<Dictionary<string, object>> RetrieveData(string SqlQuery)
			{
				using (System.Data.SqlClient.SqlConnection sqlConnection = new System.Data.SqlClient.SqlConnection(this.sConnectionString))
				{
					try
					{
						sqlConnection.Open();
					}
					catch (Exception eX)
					{
						throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
					}

					using (System.Data.SqlClient.SqlCommand sqlCommand = new System.Data.SqlClient.SqlCommand(SqlQuery, sqlConnection))
					{
						sqlCommand.CommandTimeout = this.CommandTimeout;
						using (System.Data.SqlClient.SqlDataReader sqlReader = sqlCommand.ExecuteReader())
						{
							while (sqlReader.Read())
							{
								Dictionary<string, object> dicRecord = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
								for (int i = 0; i < sqlReader.FieldCount; i++)
								{
									string sKey = sqlReader.GetName(i);

									if (!dicRecord.ContainsKey(sKey))
									{
										dicRecord.Add(sKey, sqlReader.GetValue(i));
									}
								}
								yield return dicRecord;
							}
						}
					}
				}
			}
			public void ExecuteNonSqlQuery(string SqlQuery)
			{
				try
				{
					if (this.sqlConnection != null)
					{
						using (System.Data.SqlClient.SqlCommand sqlCommand = new System.Data.SqlClient.SqlCommand(SqlQuery, this.sqlConnection))
						{
							sqlCommand.CommandTimeout = this.CommandTimeout;
							sqlCommand.ExecuteNonQuery();
						}
					}
					else
					{
						using (System.Data.SqlClient.SqlConnection sqlConnection = new System.Data.SqlClient.SqlConnection(this.ConnectionString))
						{
							try
							{
								sqlConnection.Open();
							}
							catch (Exception eX)
							{
								throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
							}
							using (System.Data.SqlClient.SqlCommand sqlCommand = new System.Data.SqlClient.SqlCommand(SqlQuery, sqlConnection))
							{
								sqlCommand.CommandTimeout = this.CommandTimeout;
								sqlCommand.ExecuteNonQuery();
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
						if (this.sqlConnection != null)
						{
							this.sqlConnection.Dispose();
						}
					}

					this.bDisposed = true;
				}
			}
			#endregion
		}

		public class Operations : IDisposable
		{
			#region Constants
			private const string OBJECT_CLASSID_COLUMN="_iObjectClassID";
			#endregion

			#region Variables
			private bool bDisposed;

			private Sql oSql;

			public int CommandTimeout
			{
				get
				{
					return this.oSql.CommandTimeout;
				}
				set
				{
					this.oSql.CommandTimeout = value;
				}
			}
			public string ConnectionString
			{
				get
				{
					return this.oSql.ConnectionString;
				}
				set
				{
					this.oSql.ConnectionString = value;
				}
			}
			public System.Data.SqlClient.SqlConnection sqlConnection
			{
				get
				{
					return this.oSql.sqlConnection;
				}
				set
				{
					this.oSql.sqlConnection = value;
				}
			}
		

			#endregion

			#region Constructors
			public Operations(string SqlConnectionString)
			{
				this.bDisposed = false;
				this.oSql = new Sql(SqlConnectionString);
			}
			public Operations(string SqlConnectionString, int CommandTimeout)
			{
				this.bDisposed = false;
				this.oSql = new Sql(SqlConnectionString, CommandTimeout);
			}
			public Operations(System.Data.SqlClient.SqlConnection sqlConnection)
			{
				this.bDisposed = false;
				this.oSql = new Sql(sqlConnection);
			}
			public Operations(System.Data.SqlClient.SqlConnection sqlConnection, int CommandTimeout)
			{
				this.bDisposed = false;
				this.oSql = new Sql(sqlConnection, CommandTimeout);
			}
			#endregion

			#region Public Methods
			public IEnumerable<Dictionary<string, object>> GetOrderedSyncServers(int ObjectClassID)
			{
				return this.oSql.RetrieveData( String.Format("EXEC {0} @iObjectClassID={1}", "spGetOrderedSyncServers", ObjectClassID) );
			}

			public long GetLastStoredUSN(int ObjectClassID, string ServerGUID, bool IsLinkingContext)
			{
				try
				{
					long iHigestCommittedUsn = 0;
					IEnumerator<Dictionary<string, object>> enumRes = this.oSql.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}, @ServerGUID='{2}', @IsLinkingContext={3}", "spGetLatestUSN", ObjectClassID, ServerGUID, IsLinkingContext ? 1 : 0)).GetEnumerator();
					if( enumRes.MoveNext() )
					{
						object oValue;
						if( enumRes.Current.TryGetValue("UpdateSequenceNumber", out oValue) )
						{
							iHigestCommittedUsn = Convert.ToInt64(oValue);
						}
					}
					return iHigestCommittedUsn;
				}
				catch(Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}

			public void SetHighestCommittedUSN(int ObjectClassID, string DomainFqdn, string ServerFqdn, string ServerGUID, long UpdateSequenceNumber, bool IsLinkingContext)
			{
				try
				{
					string sSqlQuery = String.Format("EXEC {0} @iObjectClassID={1}, @DomainFQDN='{2}', @ServerFQDN='{3}', @ServerGUID='{4}', @UpdateSequenceNumber='{5}', @IsLinkingContext={6}",
							"spSetLatestUSN",
							ObjectClassID,
							DomainFqdn,
							ServerFqdn,
							ServerGUID,
							UpdateSequenceNumber,
							IsLinkingContext ? 1 : 0
							);

					this.oSql.ExecuteNonSqlQuery(sSqlQuery);
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}

			public List<string> GetAttributesToLoad(int ObjectClassID)
			{
				try
				{
					List<string> lAttributeList = new List<string>();

					foreach (Dictionary<string, object> dicRes in this.oSql.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}", "spGetClassAttributes", ObjectClassID)) )
					{
						lAttributeList.Add(dicRes["Attribute"].ToString());
					}

					return lAttributeList;
				}
				catch (Exception ex)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
				}
			}

			public void PushObjectDeltas(string TableName, int ObjectClassID, ConcurrentQueue<Dictionary<string, string>> UpdatesCollection, string IndexingAttributeName)
			{
				try
				{
					const int ROW_LIMIT = 10;

					StringBuilder sbSqlQuery = new StringBuilder();
					StringBuilder sbSqlUpdates = new StringBuilder();
					StringBuilder sbSqlInserts = new StringBuilder();

					string sSqlQueryBase = String.Format(@"
UPDATE [{0}] {{0}}
WHERE [{1}]='{{1}}'
IF @@ROWCOUNT=0
INSERT INTO [{0}]
([{{2}}],[{2}])
VALUES ({{3}},{3})",
				 TableName,
				 IndexingAttributeName,
				 OBJECT_CLASSID_COLUMN,
				 ObjectClassID);

					int iRow = 0;

					Dictionary<string, string> dicDeltas;
					while ( UpdatesCollection.TryDequeue(out dicDeltas) )
					{
						string sIndexingAttributeValue = dicDeltas[IndexingAttributeName];

						IEnumerator<KeyValuePair<string, string>> enUpdate = dicDeltas.GetEnumerator();

						if (enUpdate.MoveNext())
						{
							if (String.IsNullOrEmpty(enUpdate.Current.Value))
							{
								if (!enUpdate.Current.Key.Equals(IndexingAttributeName, StringComparison.OrdinalIgnoreCase))
								{
									sbSqlUpdates.AppendFormat(@"SET [{0}]=NULL", enUpdate.Current.Key);
								}
								sbSqlInserts.Append("NULL");
							}
							else
							{
								string sValue = enUpdate.Current.Value.Replace("'", "''");
								if (!enUpdate.Current.Key.Equals(IndexingAttributeName, StringComparison.OrdinalIgnoreCase))
								{
									sbSqlUpdates.AppendFormat(@"SET [{0}]='{1}'", enUpdate.Current.Key, sValue);
								}
								sbSqlInserts.AppendFormat(@"'{0}'", sValue);
							}

							while (enUpdate.MoveNext())
							{
								if (String.IsNullOrEmpty(enUpdate.Current.Value))
								{
									if (!enUpdate.Current.Key.Equals(IndexingAttributeName, StringComparison.OrdinalIgnoreCase))
									{
										sbSqlUpdates.AppendFormat(@",[{0}]=NULL", enUpdate.Current.Key);
									}
									sbSqlInserts.Append(",NULL");
								}
								else
								{
									string sValue = enUpdate.Current.Value.Replace("'", "''");
									if (!enUpdate.Current.Key.Equals(IndexingAttributeName, StringComparison.OrdinalIgnoreCase))
									{
										sbSqlUpdates.AppendFormat(@",[{0}]='{1}'", enUpdate.Current.Key, sValue);
									}
									sbSqlInserts.AppendFormat(@",'{0}'", sValue);
								}
							}
						}

						sbSqlQuery.AppendFormat(sSqlQueryBase, sbSqlUpdates, sIndexingAttributeValue, String.Join("],[", dicDeltas.Keys), sbSqlInserts);
						iRow++;

						if ((iRow % ROW_LIMIT) == 0)
						{
							if (sbSqlQuery.Length > 0)
							{
								this.oSql.ExecuteNonSqlQuery(sbSqlQuery.ToString());
								sbSqlQuery = new StringBuilder();
							}
						}
						sbSqlUpdates = new StringBuilder();
						sbSqlInserts = new StringBuilder();
					}
					if (sbSqlQuery.Length > 0 & (iRow % ROW_LIMIT) > 0)
					{
						this.oSql.ExecuteNonSqlQuery(sbSqlQuery.ToString());
					}
				}
				catch (Exception ex)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
				}
			}

			public void PushLinksDeltas(iEngineConfiguration.Class oClass, ConcurrentQueue<iEngineConnectors.iActiveDirectory.Operations.LinkingUpdate> Deltas)
			{
				try
				{
					const int ROW_LIMIT = 3000;

					StringBuilder sbSqlRequests = new StringBuilder();

					List<iEngineConfiguration.Linking> LinkingDefinitions = oClass.BackwardLinking;

					Dictionary<string, StringBuilder> dicInsertQueryPatterns = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
					Dictionary<string, StringBuilder> dicDeleteQueryPatterns = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);

					foreach(iEngineConfiguration.Linking LinkingDefinition in LinkingDefinitions)
					{
						StringBuilder sbSqlInsertBase = new StringBuilder();
						StringBuilder sbSqlDeleteBase = new StringBuilder();
						StringBuilder sbSubQuery = new StringBuilder();
						
						foreach (iEngineConfiguration.Class oBckClass in LinkingDefinition.BackLinkClasses)
						{
							if (sbSubQuery.Length == 0)
							{
								sbSubQuery.AppendFormat("SELECT _ObjectID, _iObjectClassID FROM dbo.[{0}] WHERE [{1}]='{{0}}' AND [_iObjectClassID]={2}",
									oBckClass.TableContext,
									LinkingDefinition.LinkedWith,
									oBckClass.iObjectClassID
									);
							}
							else
							{
								sbSubQuery.AppendFormat("UNION ALL SELECT _ObjectID, _iObjectClassID FROM dbo.[{0}] WHERE [{1}]='{{0}}' AND [_iObjectClassID]={2}",
									oBckClass.TableContext,
									LinkingDefinition.LinkedWith,
									oBckClass.iObjectClassID
									);
							}
						}

						sbSqlInsertBase.AppendFormat(@"
SET ROWCOUNT 1
INSERT INTO [{0}]
([iLinkingAttributeID], [iFwdObjectClassID], [iFwdObjectID], [iBckObjectClassID], [iBckObjectID])
SELECT '{1}',FWD._iObjectClassID,FWD._ObjectID,BCK._iObjectClassID,BCK._ObjectID
FROM [{2}] FWD, ({3}) BCK
WHERE (FWD._iObjectClassID={4}) AND (FWD.{5}='{{1}}');",
							LinkingDefinition.TableContext,
							LinkingDefinition.iLinkingAttributeID,
							oClass.TableContext,
							sbSubQuery,
							oClass.iObjectClassID,
							iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY
							);

						dicInsertQueryPatterns.Add(LinkingDefinition.ForwardLink, sbSqlInsertBase);


						sbSqlDeleteBase.AppendFormat(@"
SET ROWCOUNT 1
DELETE LNK FROM [{0}] LNK
JOIN [{1}] FWD
ON (FWD._ObjectID=LNK.iFwdObjectID) AND (FWD._iObjectClassID=LNK.iFwdObjectClassID)
JOIN ({2}) BCK
ON (BCK._ObjectID=LNK.iBckObjectID) AND (BCK._iObjectClassID=LNK.iBckObjectClassID)
WHERE iLinkingAttributeID={3} AND (FWD._iObjectClassID={4}) AND (FWD.{5}='{{1}}');",
						LinkingDefinition.TableContext,
						oClass.TableContext,
						sbSubQuery,
						LinkingDefinition.iLinkingAttributeID,
						oClass.iObjectClassID,
						iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY
						);

						dicDeleteQueryPatterns.Add(LinkingDefinition.ForwardLink, sbSqlDeleteBase);
					}

					int iRowCount = 0;


					iActiveDirectory.Operations.LinkingUpdate oLinkingUpdate;
					while (Deltas.TryDequeue(out oLinkingUpdate))
					{
						foreach (KeyValuePair<string, iActiveDirectory.Operations.AttributeValueUpdate> kvAttributeUpdates in oLinkingUpdate.Linking)
						{
							string sIPattern = dicInsertQueryPatterns[kvAttributeUpdates.Key].ToString();
							string sDPattern = dicDeleteQueryPatterns[kvAttributeUpdates.Key].ToString();

							foreach(KeyValuePair<string, iActiveDirectory.Operations.AttributeValueUpdate.Action> kvUpdate in kvAttributeUpdates.Value.Updates)
							{
								if (kvUpdate.Value == iActiveDirectory.Operations.AttributeValueUpdate.Action.Link)
								{
									sbSqlRequests.AppendFormat(sIPattern, kvUpdate.Key, oLinkingUpdate.IndexingValue);
								}
								else
								{
									sbSqlRequests.AppendFormat(sDPattern, kvUpdate.Key, oLinkingUpdate.IndexingValue);
								}
								iRowCount++;

								if ((iRowCount % ROW_LIMIT) == 0)
								{
									if (sbSqlRequests.Length > 0)
									{
										oSql.ExecuteNonSqlQuery(sbSqlRequests.ToString());
										sbSqlRequests = new StringBuilder();
									}
								}
							}
						}
					}

					if (sbSqlRequests.Length > 0)
					{
						oSql.ExecuteNonSqlQuery(sbSqlRequests.ToString());
					}
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}

			public void PushDeletions(iEngineConfiguration.Class oClass, ConcurrentQueue<Dictionary<string, string>> UpdatesCollection, string IndexingAttributeName)
			{
				try
				{
					const int ROW_LIMIT = 3000;
					HashSet<string> hsLinkingTables = new HashSet<string>();

					foreach (iEngineConfiguration.Linking oLinking in oClass.BackwardLinking)
					{
						if (!hsLinkingTables.Contains(oLinking.TableContext))
						{
							hsLinkingTables.Add(oLinking.TableContext);
						}
					}
					foreach (iEngineConfiguration.Linking oLinking in oClass.ForwardLinking)
					{
						if (!hsLinkingTables.Contains(oLinking.TableContext))
						{
							hsLinkingTables.Add(oLinking.TableContext);
						}
					}

					int iRowCount = 0;

					StringBuilder sbSqlRequests = new StringBuilder();
					Dictionary<string, string> dicDeltas;
					while ( UpdatesCollection.TryDequeue(out dicDeltas) )
					{
						sbSqlRequests.Append("BEGIN TRANSACTION;");
						foreach (string sLinkingTable in hsLinkingTables)
						{
							sbSqlRequests.AppendFormat(@"
DELETE LNK FROM {0} LNK
JOIN {1} OBJ ON (OBJ._ObjectID=LNK.iBckObjectID AND OBJ._iObjectClassID=LNK.iBckObjectClassID) OR (OBJ._ObjectID=LNK.iFwdObjectID AND OBJ._iObjectClassID=LNK.iFwdObjectClassID)
WHERE [{2}]='{3}';",
								sLinkingTable,
								oClass.TableContext,
								IndexingAttributeName,
								dicDeltas[IndexingAttributeName].Replace("'", "''"));
						}
						sbSqlRequests.AppendFormat(@"
DELETE FROM {0} WHERE [{1}]='{2}';COMMIT TRANSACTION;",
								oClass.TableContext,
								IndexingAttributeName,
								dicDeltas[IndexingAttributeName].Replace("'", "''"));
							
						iRowCount++;

						if ((iRowCount % ROW_LIMIT) == 0)
						{
							if (sbSqlRequests.Length > 0)
							{
								oSql.ExecuteNonSqlQuery(sbSqlRequests.ToString());
								sbSqlRequests = new StringBuilder();
							}
						}

					}
					if (sbSqlRequests.Length > 0)
					{
						oSql.ExecuteNonSqlQuery(sbSqlRequests.ToString());
					}
				}
				catch (Exception ex)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
				}
			}
			#endregion

			#region Private Methods
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
						if (this.sqlConnection != null)
						{
							this.sqlConnection.Dispose();
						}
					}

					this.bDisposed = true;
				}
			}
			#endregion
		}
	}

	namespace iActiveDirectory
	{
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
					if (this.iPageSize > 0 )
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

								rcPageResponse = (dirControls.Rank > 0 && dirControls.GetLength(0) > 0 ) ? (PageResultResponseControl)dirRes.Controls.GetValue(0) : (PageResultResponseControl) null;
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

			#region Private Instance Methods
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
					ConcurrentQueue<Dictionary<string, string>> fifoSearchResultObjects;;

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
								if ( hsAttributesToLoad.Contains(dicAttributeReplData["pszAttributeName"]) )
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
								iStart = sXmlNodes.IndexOf('<'+sNode+'>', iFinish + 1);

								if (iStart >= 0)
								{
									iStart += (sNode.Length + 2);
									iFinish = sXmlNodes.IndexOf("</"+sNode+'>', iStart);

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

							iStart = sXmlNodes.IndexOf('<'+sNode+'>', 0);

							if (iStart >= 0)
							{
								iStart += (sNode.Length + 2);
								iFinish = sXmlNodes.IndexOf("</"+sNode+'>', iStart);

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
					if(this.Linking.ContainsKey(AttributeName) )
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

				#region Private Methods
				#endregion
			}

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

		public class NativeConfiguration : IDisposable
		{
			#region Constants
			public const string PRIMARY_LDAP_KEY = "objectGuid";
			#endregion

			#region Variables
			private bool bDisposed;

			private string sBaseSearchDn = null;
			private string sServerFQDN = null;
			private Int32 iPortNumber = 389;
			private Int32 iPageSize = 500;
			private Int32 iProtocolVersion = 3;
			private Credentials oSecureCredentials;
			private System.DirectoryServices.Protocols.SearchScope enSearchScope = SearchScope.Subtree;
			private Ldap oLdap;
			private SchemaAttributes oSchemaAttributes;

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
			public string DomainController
			{
				get
				{
					return this.sServerFQDN;
				}
				set
				{
					this.sServerFQDN = value;
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
				set
				{
					this.SecureCredentials = value;
				}
			}
			#endregion

			#region Constructors
			public NativeConfiguration(string ServerFQDN, Credentials SecureCredentials, int Port)
			{
				this.sServerFQDN = ServerFQDN;
				this.oSecureCredentials = SecureCredentials;
				this.iPortNumber = Port;
				this.oLdap = new Ldap(this.sServerFQDN, SecureCredentials, null, this.iPortNumber);
				this.oSchemaAttributes = GetSchemaAttributes();
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

					using (Ldap oLdap = new Ldap(ServerFQDN, SecureCredentials, String.Format("CN=Partitions,{0}", ( (object[])dicRootDSE["configurationNamingContext"])[0] ), 389))
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
					return TrivialTranslateAtribute( (object[])daProperty.GetValues(tAttribute) );
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
					this.oLdap.BaseSearchDn = String.Empty;
					this.oLdap.SearchScope = SearchScope.Base;

					if(AttributesToLoad == null)
					{
						AttributesToLoad = new string[] {"rootDomainNamingContext", "configurationNamingContext"};
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

					Dictionary<string, object> dicRootDSE = RootDSE( new string[] { "configurationNamingContext" } );

					this.oLdap.BaseSearchDn = String.Format("CN=Partitions,{0}", ((object[])dicRootDSE["configurationNamingContext"])[0]);

					this.oLdap.SearchScope = SearchScope.Subtree;
					return oLdap.RetrieveAttributes("(&(systemFlags:1.2.840.113556.1.4.803:=3)(objectClass=crossRef))", AttributesToLoad, false);
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
						AttributesToLoad = new string[] { "objectGuid", "distinguishedName", "name"};
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
					if(AttributeName == null || AttributeValue == null)
					{
						AttributeName="dNSHostName";
						AttributeValue=(string)((object[])dicRootDSE["dnsHostName"])[0];
					}

					this.oLdap.BaseSearchDn = String.Format("CN=Sites,{0}", ((object[])dicRootDSE["configurationNamingContext"])[0]);

					this.oLdap.SearchScope = SearchScope.Subtree;
					IEnumerator<Dictionary<string, object>> enumServerResult = oLdap.RetrieveAttributes(String.Format("(&({0}={1})(objectClass=server))", AttributeName, AttributeValue), new string[] { "distinguishedName", "name" }, false).GetEnumerator();
					if (enumServerResult.MoveNext())
					{
						string sServerName = (string)((object[])enumServerResult.Current["name"])[0];
						string sServerDn = (string)((object[])enumServerResult.Current["distinguishedName"])[0];

						this.oLdap.BaseSearchDn = sServerDn.Remove(0, String.Format("CN={0},CN=Servers", sServerName).Length + 1);
					}

					oLdap.SearchScope = SearchScope.Base;
					IEnumerator<Dictionary<string, object>> enumSiteResult = oLdap.RetrieveAttributes(null, AttributesToLoad, false).GetEnumerator();
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
					foreach( Dictionary<string, object> dicRes in ISiteGlobalCatalogServers(Site, AttributesToLoad) )
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
							string[] aAttributes = new string[AttributesToLoad.Length+1];
							aAttributes[0] = "distinguishedName";
							AttributesToLoad.CopyTo(aAttributes, 1);
							AttributesToLoad = aAttributes;
						}
					}

					Dictionary<string, object> dicRootDSE = RootDSE(new string[] { "configurationNamingContext" });

					this.oLdap.BaseSearchDn = String.Format("CN={0},CN=Sites,{1}", Site, ((object[])dicRootDSE["configurationNamingContext"])[0]);
					this.oLdap.SearchScope = SearchScope.Subtree;
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}

				foreach( Dictionary<string, object> dicServer in oLdap.RetrieveAttributes("(objectClass=server)", AttributesToLoad, false) )
				{
					this.oLdap.SearchScope = SearchScope.Base;
					this.oLdap.BaseSearchDn = String.Format("CN=NTDS Settings,{0}", ((object[])dicServer["distinguishedName"])[0]);

					IEnumerator<Dictionary<string, object>> enumServerParams = oLdap.RetrieveAttributes("(&(options:1.2.840.113556.1.4.803:=1)(objectClass=NTDSDSA))", new string[] { "distinguishedName" }, false).GetEnumerator();
					if (enumServerParams.MoveNext())
					{
						yield return dicServer;
					}
				}
			}

			public SchemaAttributes GetSchemaAttributes()
			{
				return new SchemaAttributes(this.sServerFQDN, this.oSecureCredentials);
			}
			#endregion

			#region Private Methods

			#endregion

			#region Classes
			public class SchemaAttributes : IDisposable
			{
				#region Variables
				private bool bDisposed;
				private Dictionary<string, AttributeType> dicAttributeTypes = new Dictionary<string, AttributeType>(StringComparer.OrdinalIgnoreCase);

				string sServerFQDN;

				Credentials oSecureCredentials;

				public Dictionary<string, AttributeType> AttributeTypes
				{
					get
					{
						return this.dicAttributeTypes;
					}
				}
				#endregion

				#region Constructors
				public SchemaAttributes(string ServerFQDN, Credentials SecureCredentials)
				{
					this.sServerFQDN = ServerFQDN;
					this.oSecureCredentials = SecureCredentials;

					this.GetSchemaAttributes();
				}
				#endregion

				#region Private Instance Methods
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
								this.sServerFQDN,
								this.oSecureCredentials,
								new string[] { "schemaNamingContext" }
								);

						using (Ldap oLdap = new Ldap(this.sServerFQDN, this.oSecureCredentials, String.Format("{0}", ((object[])dicRootDSE["schemaNamingContext"])[0]), 389))
						{
							foreach ( Dictionary<string, object> dicRes in oLdap.RetrieveAttributes(sLdapFilter, new string[] { "oMSyntax", "attributeSyntax", "LdapDisplayName" }, false))
							{
								string sAttributeStx = ((object[])dicRes["attributeSyntax"])[0].ToString();
								string sAttributeoMStx = ((object[])dicRes["oMSyntax"])[0].ToString();

								if (dicAttributeMappings.ContainsKey(sAttributeStx) && dicAttributeMappings[sAttributeStx].ContainsKey(sAttributeoMStx))
								{
									AttributeType oAttributeType = dicAttributeMappings[sAttributeStx][sAttributeoMStx];
									string sAttributeName = ((object[])dicRes["LdapDisplayName"])[0].ToString();

									if (this.dicAttributeTypes.ContainsKey(sAttributeName))
									{
									}
									else
									this.dicAttributeTypes.Add(sAttributeName, oAttributeType);
								}
							}
						}
					}
					catch(Exception eX)
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
							this.dicAttributeTypes = null;
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
						if (this.oLdap != null) this.oLdap.Dispose();
					}

					this.bDisposed = true;
				}
			}

			#endregion
		}
	}
}
