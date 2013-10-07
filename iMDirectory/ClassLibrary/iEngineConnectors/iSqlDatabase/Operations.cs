﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace iMDirectory.iEngineConnectors.iSqlDatabase
{
	public class Operations : IDisposable
	{
		#region Constants
		private const string OBJECT_CLASSID_COLUMN = "_iObjectClassID";
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
			return this.oSql.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}", "spGetOrderedSyncServers", ObjectClassID));
		}

		public long GetLastStoredUSN(int ObjectClassID, string ServerGUID, bool IsLinkingContext)
		{
			try
			{
				long iHigestCommittedUsn = 0;
				IEnumerator<Dictionary<string, object>> enumRes = this.oSql.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}, @ServerGUID='{2}', @IsLinkingContext={3}", "spGetLatestUSN", ObjectClassID, ServerGUID, IsLinkingContext ? 1 : 0)).GetEnumerator();
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

				foreach (Dictionary<string, object> dicRes in this.oSql.RetrieveData(String.Format("EXEC {0} @iObjectClassID={1}", "spGetClassAttributes", ObjectClassID)))
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