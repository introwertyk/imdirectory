﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;

using iCOR3.iSqlDatabase;
using iCOR3.iSecurityComponent;

using iMDirectory.iEngineConnectors.iActiveDirectory;

namespace iMDirectory
{
	/// <summary>
	/// Main iMDirectory class. Initiates global update process.
	/// </summary>
	public class iEngine: IDisposable
	{
		#region Constants
		private const string DELIMITER = "|";
		#endregion

		#region  Variables
		protected EventLog oEventLog;
		private iEngineConfiguration.Configuration Configuration
		{
			get;
			set;
		}
		bool bDisposed;

		#endregion

		#region Constructors
		public iEngine()
		{
			this.bDisposed = false;
			this.oEventLog = new System.Diagnostics.EventLog("Application", ".", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
			this.SetConfiguration();
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Starts synchronization cycle.
		/// </summary>
		public void Start()
		{
			foreach (iEngineConfiguration.Connector oConnector in this.Configuration.Connectors.Values)
			{
				try
				{
					if (oConnector.ParrentConnector == null)
					{
						if (oConnector.Type.Equals("AD-DS", StringComparison.OrdinalIgnoreCase) || oConnector.Type.Equals("Active-Directory", StringComparison.OrdinalIgnoreCase))
						{
							this.ProcessADDS( oConnector, false);;
						}
					}
				}
				catch (Exception eX)
				{
					this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3050);
				}
			}

			foreach (iEngineConfiguration.Connector oConnector in this.Configuration.Connectors.Values)
			{
				try
				{
					if (oConnector.ParrentConnector == null)
					{
						if (oConnector.Type.Equals("AD-DS", StringComparison.OrdinalIgnoreCase) || oConnector.Type.Equals("Active-Directory", StringComparison.OrdinalIgnoreCase))
						{
							this.ProcessADDS(oConnector, true);
						}
					}
				}
				catch (Exception eX)
				{
					this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3060);
				}
			}
		}
		#endregion

		#region Private Methods
		private void ProcessADDS( iEngineConfiguration.Connector oConnector, bool IsLinking)
		{
			try
			{
				//const string INDEXING_ATTRIBUTE="objectGuid";
				if (oConnector.Category.Equals("Forest", StringComparison.OrdinalIgnoreCase))
				{
					using (Credentials oRootCredentials = new Credentials())
					{
						if (oConnector.Configuration.ContainsKey("PASSWORD") && oConnector.Configuration.ContainsKey("USERNAME"))
						{
							oRootCredentials.UserName = iCOR3.iSecurityComponent.Encryption.Decrypt( oConnector.Configuration["USERNAME"].ToString() );
							oRootCredentials.Password = iCOR3.iSecurityComponent.Encryption.Decrypt( oConnector.Configuration["PASSWORD"].ToString() );
						}

						//go through Forest domains and search for dedicated sub-domain connectors
						foreach (Dictionary<string, object> dicDomain in NativeConfiguration.GetForestDomains(oConnector.DomainFQDN, oRootCredentials, new string[] { "dnsRoot" }))
						{
							string sDomainFQDN = (string)((object[])dicDomain["dnsRoot"])[0];

							iEngineConfiguration.Connector oChildConnector;
							if (!oConnector.TryGetChildConnector(sDomainFQDN, out oChildConnector))
							{
								oChildConnector = new iEngineConfiguration.Connector();
							}

							oChildConnector.DomainFQDN = sDomainFQDN;
							oChildConnector = iEngineConfiguration.Connector.MergeConnectors(oConnector, oChildConnector);

							foreach (iEngineConfiguration.Class oObjectClass in oChildConnector.ObjectClasses)
							{
								using (Credentials oCredentials = new Credentials(oRootCredentials.Password, oRootCredentials.Password))
								{
									using (iEngineConnectors.iSqlDatabase.Operations oSqlOperations = new iEngineConnectors.iSqlDatabase.Operations(ConfigurationManager.ConnectionStrings["iMDirectory"].ConnectionString))
									{
										long iLastHighestCommittedUSN;
										List<string> lAttributesToLoad;
										string sDelimiter;

										object oDelim;
										sDelimiter = (oChildConnector.Configuration.TryGetValue("DELIMITER", out oDelim))
											? oDelim.ToString()
											: DELIMITER;

										if (oChildConnector.Configuration.ContainsKey("PASSWORD") && oChildConnector.Configuration.ContainsKey("USERNAME"))
										{
											oCredentials.UserName = iCOR3.iSecurityComponent.Encryption.Decrypt( oChildConnector.Configuration["USERNAME"].ToString() );
											oCredentials.Password = iCOR3.iSecurityComponent.Encryption.Decrypt( oChildConnector.Configuration["PASSWORD"].ToString() );
										}
										object oNearSite = null;
										oChildConnector.Configuration.TryGetValue("NEAR_SITE", out oNearSite);

										Dictionary<string, object> dicServer = this.GetServer(sDomainFQDN, (string)oNearSite, oCredentials, oObjectClass.ObjectClassID);

										//get last USN for give GUID from DB
										iLastHighestCommittedUSN = oSqlOperations.GetLastStoredUSN(oObjectClass.ObjectClassID, dicServer["objectGuid"].ToString(), IsLinking);
										lAttributesToLoad = oSqlOperations.GetAttributesToLoad(oObjectClass.ObjectClassID);

										long iHighestCommittedUSN = Convert.ToInt64(dicServer["highestCommittedUSN"]);

										string sServerFQDN = dicServer["dnsHostName"].ToString();

										if (iHighestCommittedUSN < iLastHighestCommittedUSN)
										{
											this.oEventLog.WriteEntry(String.Format("{0}::Repository data integrity breach. {1} server LatestCommittedUSN ({2}) lower than last locally stored LatestCommittedUSN ({3}).",
																	new StackFrame(0, true).GetMethod().Name,
																	sServerFQDN,
																	iHighestCommittedUSN,
																	iLastHighestCommittedUSN
																	),
																			EventLogEntryType.Error, 3051);
										}

										using (iEngineConnectors.iActiveDirectory.Operations oLdapOperations = new iEngineConnectors.iActiveDirectory.Operations(sServerFQDN, oCredentials, oObjectClass.SearchRoot, oChildConnector.Port))
										{
											if (!IsLinking)
											{
												//retrieve change deltas from AD DS
												ConcurrentQueue<Dictionary<string, string>> fifoDeltas = oLdapOperations.GetDirectoryDelta(iLastHighestCommittedUSN, oObjectClass.Filter, lAttributesToLoad, sDelimiter, false);
												//insert object updates
												oSqlOperations.PushObjectDeltas(oObjectClass.TableContext, oObjectClass.ObjectClassID, fifoDeltas, iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY);

												//process deletions

												//delete linking
												ConcurrentQueue<Dictionary<string, string>> fifoDeltasPhantom = oLdapOperations.GetDirectoryDelta(iLastHighestCommittedUSN, oObjectClass.OtherFilter, lAttributesToLoad, sDelimiter, true);
												oSqlOperations.PushDeletions(oObjectClass, fifoDeltasPhantom, iEngineConnectors.iActiveDirectory.NativeConfiguration.PRIMARY_LDAP_KEY);

												//save current HighestCommittedUsn
												oSqlOperations.SetHighestCommittedUSN(oObjectClass.ObjectClassID, oChildConnector.DomainFQDN, sServerFQDN, dicServer["objectGuid"].ToString(), Convert.ToInt64(dicServer["highestCommittedUSN"]), false);
											}
											else
											{
												if (oObjectClass.BackwardLinking.Count > 0)
												{
													ConcurrentQueue<iEngineConnectors.iActiveDirectory.Operations.LinkingUpdate> fifoDeltas = oLdapOperations.GetDirectoryMetaDataDelta(iLastHighestCommittedUSN, oObjectClass.Filter, new List<string>(oObjectClass.BackwardLinking.Select(a => a.ForwardLink)));
													
													//save current HighestCommittedUsn
													oSqlOperations.PushLinksDeltas(oObjectClass, fifoDeltas);

													oSqlOperations.SetHighestCommittedUSN(oObjectClass.ObjectClassID, oChildConnector.DomainFQDN, sServerFQDN, dicServer["objectGuid"].ToString(), Convert.ToInt64(dicServer["highestCommittedUSN"]), true);
												}
											}
										}
									}
								}
							}
						}
					}
				}
				else if (oConnector.Category.Equals("Domain", StringComparison.OrdinalIgnoreCase))
				{
					;
				}
			}
			catch (Exception eX)
			{
				this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message), EventLogEntryType.Error, 3050);
			}
		}
		private Dictionary<string, object> GetServer(string sDomainFQDN, string sNearSite, Credentials oCredentials, Guid iObjectClassID)
		{
			try
			{
				Dictionary<string, object> dicGlobalCatalog = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

				using (iEngineConnectors.iActiveDirectory.NativeConfiguration oADConfigurationContext = new NativeConfiguration(sDomainFQDN, oCredentials))
				{

					//if no NearSite defined use site of the bind server if no GC get any GC
					if (String.IsNullOrEmpty(sNearSite))
					{
						//rewrite to static methods in new release
						object NearSite;
						if (!oADConfigurationContext.ServerSite(new string[] { "name" }).TryGetValue("name", out NearSite))
						{
							throw new Exception(String.Format("Unable to determine Near Site name for domain: {0}", sDomainFQDN));
						}
						sNearSite = ( (object[])NearSite )[0].ToString();
					}

					using (iEngineConnectors.iSqlDatabase.Operations oSqlOperations = new iEngineConnectors.iSqlDatabase.Operations(ConfigurationManager.ConnectionStrings["iMDirectory"].ConnectionString))
					{
						//get site GC's from domain configuration
						IEnumerator<Dictionary<string, object>> enumGlobalCatalog = oADConfigurationContext.SiteGlobalCatalogServers(sNearSite, new string[] { "objectGuid", "dNSHostName", "distinguishedName", "name" }).GetEnumerator();
						foreach (Dictionary<string, object> dicRes in oSqlOperations.GetOrderedSyncServers(iObjectClassID))
						{
							while (dicGlobalCatalog.Keys.Count == 0 && enumGlobalCatalog.MoveNext())
							{
								//check if sync meta-data exists for found DC
								string sServerGUID = new Guid((byte[])((object[])enumGlobalCatalog.Current["objectGuid"])[0]).ToString();
								if (dicRes["ServerGUID"].ToString().Equals(sServerGUID, StringComparison.OrdinalIgnoreCase))
								{
									//site GC already on the list
									//test the GC ; retrieve highestCommittedUSN from rootDSE
									try
									{
										string sServerFqdn = ((object[])enumGlobalCatalog.Current["dNSHostName"])[0].ToString();

										foreach (KeyValuePair<string, object> kvProperty in iEngineConnectors.iActiveDirectory.NativeConfiguration.GetRootDSE(sServerFqdn, oCredentials, new string[] { "highestCommittedUSN", "dnsHostName", "invocationID" }))
										{
											dicGlobalCatalog.Add(kvProperty.Key, ((object[])kvProperty.Value)[0]);
										}
										dicGlobalCatalog.Add("objectGuid", sServerGUID);
									}
									catch (Exception)
									{
										;
									}
								}
							}
							enumGlobalCatalog.Reset();
						}

						//found no previously synchronized GCs; Select unsynchronized from Site
						if (dicGlobalCatalog.Keys.Count == 0)
						{
							this.oEventLog.WriteEntry(String.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, "No sync meta-data for active servers in near site."), EventLogEntryType.Warning, 4050);

							enumGlobalCatalog.Reset();
							while (dicGlobalCatalog.Keys.Count == 0 && enumGlobalCatalog.MoveNext())
							{
								//test the GC ; retrieve highestCommittedUSN from rootDSE
								try
								{
									string sServerFqdn = ((object[])enumGlobalCatalog.Current["dNSHostName"])[0].ToString();
									string sServerGUID = new Guid((byte[])((object[])enumGlobalCatalog.Current["objectGuid"])[0]).ToString();

									foreach (KeyValuePair<string, object> kvProperty in iEngineConnectors.iActiveDirectory.NativeConfiguration.GetRootDSE(sServerFqdn, oCredentials, new string[] { "highestCommittedUSN", "dnsHostName", "invocationID" }))
									{
										dicGlobalCatalog.Add(kvProperty.Key, ((object[])kvProperty.Value)[0]);
									}
									dicGlobalCatalog.Add("objectGuid", sServerGUID);
								}
								catch (Exception)
								{
									;
								}
							}
						}
					}
					if (dicGlobalCatalog.Keys.Count == 0)
					{
						throw new Exception( String.Format("Unable to determine Global Catalog server for given domain site {0}@{1}", sNearSite, sDomainFQDN) );
					}
					return dicGlobalCatalog;
				}
			}
			catch(Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Loads configuration from underlying DB into iEngineConfiguration library classes instances.
		/// </summary>
		private void SetConfiguration()
		{
			try
			{
				this.Configuration = new iEngineConfiguration.Configuration();

				using (Sql oSql = new Sql(ConfigurationManager.ConnectionStrings["iMDirectory"].ConnectionString))
				{
					//Collect connector definitions
					foreach (Dictionary<string, object> oConnRes in oSql.RetrieveData("EXEC spGetTarget"))
					{
						Guid iConnectorID = new Guid(oConnRes["iConnectorID"].ToString());

						iEngineConfiguration.Connector oConnector;

						if (!this.Configuration.Connectors.TryGetValue(iConnectorID, out oConnector))
						{
							oConnector = new iEngineConfiguration.Connector();
							this.Configuration.Connectors.Add(iConnectorID, oConnector);
						}

						oConnector.ConnectorID = iConnectorID;
						oConnector.DomainFQDN = oConnRes["DomainFQDN"] == DBNull.Value ? String.Empty : oConnRes["DomainFQDN"].ToString();
						oConnector.Type = oConnRes["Type"] == DBNull.Value ? String.Empty : oConnRes["Type"].ToString();
						oConnector.Category = oConnRes["Category"] == DBNull.Value ? String.Empty : oConnRes["Category"].ToString();
						oConnector.Version = oConnRes["Version"] == DBNull.Value ? String.Empty : oConnRes["Version"].ToString();
						if (oConnRes["Port"] != DBNull.Value) oConnector.Port = Convert.ToInt32(oConnRes["Port"]);
						if (oConnRes["ProtocolVersion"] != DBNull.Value) oConnector.ProtocolVersion = Convert.ToInt32(oConnRes["ProtocolVersion"]);
						if (oConnRes["PageSize"] != DBNull.Value) oConnector.PageSize = Convert.ToInt32(oConnRes["PageSize"]);
		
						Dictionary<Guid, Dictionary<string, object>> dicConfiguration = new Dictionary<Guid, Dictionary<string, object>>();

						//Collect connector k/v configuration
						foreach (Dictionary<string, object> oConfRes in oSql.RetrieveData(String.Format("EXEC spGetTargetConfiguration @iConnectorID='{0}'", iConnectorID)))
						{
							string sKey = oConfRes["KeyName"] == DBNull.Value ? String.Empty : oConfRes["KeyName"].ToString();
							object oVal = oConfRes["KeyValue"] == DBNull.Value ? null : oConfRes["KeyValue"];

							oConnector.Configuration.Add(sKey, oVal);
						}

						//Collect classes definition
						foreach (Dictionary<string, object> oClassRes in oSql.RetrieveData(String.Format("EXEC spGetTargetClasses @iConnectorID='{0}'", iConnectorID)))
						{
							Guid iObjectClassID = new Guid (oClassRes["iObjectClassID"].ToString());

							iEngineConfiguration.Class oObjectClass;

							if (!this.Configuration.Classes.TryGetValue(iObjectClassID, out oObjectClass))
							{
								oObjectClass = new iEngineConfiguration.Class();
								this.Configuration.Classes.Add(iObjectClassID, oObjectClass);
								oConnector.ObjectClasses.Add(oObjectClass);
							}

							oObjectClass.ObjectClassID = iObjectClassID;
							oObjectClass.ObjectClass = oClassRes["ObjectClass"].ToString();
							oObjectClass.TableContext = oClassRes["TableContext"].ToString();
							oObjectClass.Filter = oClassRes["Filter"].ToString();
							oObjectClass.OtherFilter = oClassRes["OtherFilter"].ToString();
							oObjectClass.SearchRoot = oClassRes["SearchRoot"] == DBNull.Value
								? null
								: oClassRes["SearchRoot"].ToString();
						}


						//Collect linking configuration
						foreach (Dictionary<string, object> oLinkRes in oSql.RetrieveData(String.Format("EXEC spGetLinks")) )
						{
							Guid iLinkingAttributeID = new Guid(oLinkRes["iLinkingAttributeID"].ToString());

							iEngineConfiguration.Linking oLinking;

							if (!this.Configuration.Linking.TryGetValue(iLinkingAttributeID, out oLinking))
							{
								oLinking = new iEngineConfiguration.Linking();
								this.Configuration.Linking.Add(iLinkingAttributeID, oLinking);
							}

							oLinking.LinkingAttributeID = iLinkingAttributeID;
							oLinking.ForwardLink = oLinkRes["ForwardLink"].ToString();
							oLinking.BackLink = oLinkRes["BackLink"].ToString();
							oLinking.LinkedWith = oLinkRes["LinkedWith"].ToString();
							oLinking.TableContext = oLinkRes["TableContext"].ToString();

							Guid iFwdObjectClassID = new Guid (oLinkRes["iFwdObjectClassID"].ToString());
							Guid iBckObjectClassID = new Guid (oLinkRes["iBckObjectClassID"].ToString());

							iEngineConfiguration.Class oObjectClass;
							if ( this.Configuration.Classes.TryGetValue(iFwdObjectClassID, out oObjectClass) )
							{
								if (!oLinking.ForwardLinkClasses.Contains(oObjectClass))
								{
									oLinking.ForwardLinkClasses.Add(oObjectClass);
								}
							}
							if (this.Configuration.Classes.TryGetValue(iBckObjectClassID, out oObjectClass))
							{
								if (!oLinking.BackLinkClasses.Contains(oObjectClass))
								{
									oLinking.BackLinkClasses.Add(oObjectClass);
								}
							}
						}

						//Collect linking configuration
						foreach (iEngineConfiguration.Class oObjectClass in this.Configuration.Classes.Values)
						{
							foreach (Dictionary<string, object> oLinkRes in oSql.RetrieveData(String.Format("EXEC spGetLinkingAttributesForLinkedClass @iFwdObjectClassID='{0}'", oObjectClass.ObjectClassID)))
							{
								Guid iLinkingAttributeID = new Guid(oLinkRes["iLinkingAttributeID"].ToString());

								iEngineConfiguration.Linking oLinking;
								if (this.Configuration.Linking.TryGetValue(iLinkingAttributeID, out oLinking))
								{
									if ( !oObjectClass.BackwardLinking.Contains(oLinking) )
									{
										oObjectClass.BackwardLinking.Add(oLinking);
									}
								}
							}

							foreach (Dictionary<string, object> oLinkRes in oSql.RetrieveData(String.Format("EXEC spGetLinkingAttributesForLinkedClass @iBckObjectClassID='{0}'", oObjectClass.ObjectClassID)))
							{
								Guid iLinkingAttributeID = new Guid(oLinkRes["iLinkingAttributeID"].ToString());

								iEngineConfiguration.Linking oLinking;
								if (this.Configuration.Linking.TryGetValue(iLinkingAttributeID, out oLinking))
								{
									if (!oObjectClass.ForwardLinking.Contains(oLinking))
									{
										oObjectClass.ForwardLinking.Add(oLinking);
									}
								}
							}
						}
					}

					//create parent/child connector relationships
					foreach (Dictionary<string, object> oConnRes in oSql.RetrieveData("EXEC spGetTarget"))
					{
						Guid iParentConnectorID = oConnRes["iParentConnectorID"] == DBNull.Value ? Guid.Empty : new Guid(oConnRes["iParentConnectorID"].ToString());
						Guid iChildConnectorID = oConnRes["iConnectorID"] == DBNull.Value ? Guid.Empty : new Guid(oConnRes["iConnectorID"].ToString());

						iEngineConfiguration.Connector oParentConnector;
						iEngineConfiguration.Connector oChildConnector;
						if( this.Configuration.Connectors.TryGetValue(iParentConnectorID, out oParentConnector) )
						{
							if (this.Configuration.Connectors.TryGetValue(iChildConnectorID, out oChildConnector))
							{
								oParentConnector.ChildConnectors.Add(oChildConnector);
								oChildConnector.ParrentConnector = oParentConnector;
							}
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

				}

				this.bDisposed = true;
			}
		}
		#endregion
	}
}
