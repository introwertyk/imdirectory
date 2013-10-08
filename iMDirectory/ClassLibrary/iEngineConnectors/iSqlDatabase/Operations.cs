using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace iMDirectory.iEngineConnectors.iSqlDatabase
{
	/// <summary>
	/// Retrieves data from underlying MS SQL DB
	/// Class defines interface between component and data stored under specific data structure.
	/// </summary>
	public class Operations : IDisposable
	{
		#region Constants
		private const string OBJECT_CLASSID_COLUMN = "_iObjectClassID";
		#endregion

		#region Variables
		private bool bDisposed;

		private Sql SqlObject
		{
			get;
			set;
		}

		public int CommandTimeout
		{
			get
			{
				return this.SqlObject.CommandTimeout;
			}
			set
			{
				this.SqlObject.CommandTimeout = value;
			}
		}
		public string ConnectionString
		{
			get
			{
				return this.SqlObject.ConnectionString;
			}
			set
			{
				this.SqlObject.ConnectionString = value;
			}
		}
		public System.Data.SqlClient.SqlConnection sqlConnection
		{
			get
			{
				return this.SqlObject.SqlConnection;
			}
			set
			{
				this.SqlObject.SqlConnection = value;
			}
		}


		#endregion

		#region Constructors
		public Operations(string SqlConnectionString)
		{
			this.bDisposed = false;
			this.SqlObject = new Sql(SqlConnectionString);
		}
		public Operations(string SqlConnectionString, int CommandTimeout)
		{
			this.bDisposed = false;
			this.SqlObject = new Sql(SqlConnectionString, CommandTimeout);
		}
		public Operations(System.Data.SqlClient.SqlConnection sqlConnection)
		{
			this.bDisposed = false;
			this.SqlObject = new Sql(sqlConnection);
		}
		public Operations(System.Data.SqlClient.SqlConnection sqlConnection, int CommandTimeout)
		{
			this.bDisposed = false;
			this.SqlObject = new Sql(sqlConnection, CommandTimeout);
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// From synchronization meta-data retrieves directory servers GUID(s) used to retrieve changes.
		/// Requires Object Class ID from given connector context as synchronization is performed per object class.
		/// </summary>
		public IEnumerable<Dictionary<string, object>> GetOrderedSyncServers(int ObjectClassID)
		{
			return this.SqlObject.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}", "spGetOrderedSyncServers", ObjectClassID));
		}

		/// <summary>
		/// From synchronization meta-data retrieves last stored HighestCommittedUSN.
		/// Requires Object Class ID from given connector context as synchronization is performed per object class.
		/// Requires the last used server GUID.
		/// Data can be retrieved for MS AD(DS) object changes or for linking attribute changes.
		/// </summary>
		public long GetLastStoredUSN(int ObjectClassID, string ServerGUID, bool IsLinkingContext)
		{
			try
			{
				long iHigestCommittedUsn = 0;
				IEnumerator<Dictionary<string, object>> enumRes = this.SqlObject.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}, @ServerGUID='{2}', @IsLinkingContext={3}", "spGetLatestUSN", ObjectClassID, ServerGUID, IsLinkingContext ? 1 : 0)).GetEnumerator();
				if (enumRes.MoveNext())
				{
					object oValue;
					if (enumRes.Current.TryGetValue("UpdateSequenceNumber", out oValue))
					{
						iHigestCommittedUsn = Convert.ToInt64(oValue);
					}
				}
				return iHigestCommittedUsn;
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Stores last HighestCommittedUSN in synchronization meta-data DB.
		/// HighestCommittedUSN should be stored only when synchronization was completed successfully.
		/// </summary>
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

				this.SqlObject.ExecuteNonSqlQuery(sSqlQuery);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Retrieves attributes names for which synchronization is being performed.
		/// Attribute names correspond to table columns names and information is retrieved from DB schema.
		/// Tables are organized per object class and ObjectClassID is required to recognize table name.
		/// </summary>
		public List<string> GetAttributesToLoad(int ObjectClassID)
		{
			try
			{
				List<string> lAttributeList = new List<string>();

				foreach (Dictionary<string, object> dicRes in this.SqlObject.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}", "spGetClassAttributes", ObjectClassID)))
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

		/// <summary>
		/// Writes MS AD(DS) object changes queue into MS SQL DB.
		/// </summary>
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
				while (UpdatesCollection.TryDequeue(out dicDeltas))
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
							this.SqlObject.ExecuteNonSqlQuery(sbSqlQuery.ToString());
							sbSqlQuery = new StringBuilder();
						}
					}
					sbSqlUpdates = new StringBuilder();
					sbSqlInserts = new StringBuilder();
				}
				if (sbSqlQuery.Length > 0 & (iRow % ROW_LIMIT) > 0)
				{
					this.SqlObject.ExecuteNonSqlQuery(sbSqlQuery.ToString());
				}
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}

		/// <summary>
		/// Writes MS AD(DS) linking object changes queue into MS SQL DB.
		/// </summary>
		public void PushLinksDeltas(iEngineConfiguration.Class oClass, ConcurrentQueue<iEngineConnectors.iActiveDirectory.Operations.LinkingUpdate> Deltas)
		{
			try
			{
				const int ROW_LIMIT = 3000;

				StringBuilder sbSqlRequests = new StringBuilder();

				List<iEngineConfiguration.Linking> LinkingDefinitions = oClass.BackwardLinking;

				Dictionary<string, StringBuilder> dicInsertQueryPatterns = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, StringBuilder> dicDeleteQueryPatterns = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);

				foreach (iEngineConfiguration.Linking LinkingDefinition in LinkingDefinitions)
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
								oBckClass.ObjectClassID
								);
						}
						else
						{
							sbSubQuery.AppendFormat("UNION ALL SELECT _ObjectID, _iObjectClassID FROM dbo.[{0}] WHERE [{1}]='{{0}}' AND [_iObjectClassID]={2}",
								oBckClass.TableContext,
								LinkingDefinition.LinkedWith,
								oBckClass.ObjectClassID
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
						LinkingDefinition.LinkingAttributeID,
						oClass.TableContext,
						sbSubQuery,
						oClass.ObjectClassID,
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
					LinkingDefinition.LinkingAttributeID,
					oClass.ObjectClassID,
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

						foreach (KeyValuePair<string, iActiveDirectory.Operations.AttributeValueUpdate.Action> kvUpdate in kvAttributeUpdates.Value.Updates)
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
									SqlObject.ExecuteNonSqlQuery(sbSqlRequests.ToString());
									sbSqlRequests = new StringBuilder();
								}
							}
						}
					}
				}

				if (sbSqlRequests.Length > 0)
				{
					SqlObject.ExecuteNonSqlQuery(sbSqlRequests.ToString());
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Writes MS AD(DS) object changes (deletions) queue into MS SQL DB.
		/// </summary>
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
				while (UpdatesCollection.TryDequeue(out dicDeltas))
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
							SqlObject.ExecuteNonSqlQuery(sbSqlRequests.ToString());
							sbSqlRequests = new StringBuilder();
						}
					}

				}
				if (sbSqlRequests.Length > 0)
				{
					SqlObject.ExecuteNonSqlQuery(sbSqlRequests.ToString());
				}
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
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
}
