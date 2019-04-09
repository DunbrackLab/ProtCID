using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.Clustering
{
	/// <summary>
	/// Summary description for RedunCrystForms.
	/// </summary>
	public class RedundantCrystForms
	{
		#region member variables
		private double simPercent = 0.70;

		private double simQScore = 0.10;
		private double asaCutoff = 100.0;
		private DbQuery dbQuery = new DbQuery ();
		private DbInsert dbInsert = new DbInsert ();
		public DataTable reduntCrystFormTable = null;
		private const double minVal = 999999.0;
		#endregion

		public RedundantCrystForms()
		{
            if (AppSettings.abbrevNonXrayCrystMethods == null)
            {
                AppSettings.LoadCrystMethods();
            }
		}

		#region check redundant crystal forms
		/// <summary>
		/// check if there are same crystal forms for all groups
		/// </summary>
		public void CheckReduntCrystForms ()
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Redundant crystal forms.";
			
			InitializeTables ();
			InitializeDbTables ();

			string queryString = string.Format ("Select Distinct GroupSeqID From {0};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo]);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
			int groupId = 0;

			ProtCidSettings.progressInfo.totalStepNum = groupIdTable.Rows.Count;
			ProtCidSettings.progressInfo.totalOperationNum = groupIdTable.Rows.Count;
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Detecting reduntdant crystal forms.");

			foreach (DataRow groupRow in groupIdTable.Rows)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;				
				groupId = Convert.ToInt32 (groupRow["GroupSeqID"].ToString ());
				ProtCidSettings.progressInfo.currentFileName = groupId.ToString ();

				CheckGroupReduntCrystForms (groupId);
				if (reduntCrystFormTable.Rows.Count > 0)
				{
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, reduntCrystFormTable);
					reduntCrystFormTable.Clear ();
				}
			}
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done!");
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
		public void UpdateReduntCrystForms (int[] groups)
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Detecting redundant crystal forms.";
	
			ProtCidSettings.progressInfo.totalStepNum = groups.Length;
			ProtCidSettings.progressInfo.totalOperationNum = groups.Length;
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Detecting reduntdant crystal forms.");

			InitializeTables ();

            StreamWriter groupWriter = new StreamWriter("CfUpdatedGroups.txt");
            
			foreach (int groupId in groups)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;				
				ProtCidSettings.progressInfo.currentFileName = groupId.ToString ();

				CheckGroupReduntCrystForms (groupId);

                DeleteObsData(groupId);

                if (reduntCrystFormTable.Rows.Count > 0)
                {
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, reduntCrystFormTable);
                    reduntCrystFormTable.Clear();
                    groupWriter.WriteLine(groupId.ToString ());
                }
			}
            groupWriter.Close();
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done!");
		}

		/// <summary>
		/// check there are same crystal forms in the group
		/// </summary>
		/// <param name="groupId"></param>
		/// <returns>redundant crystal forms</returns>
		public Dictionary<int, List<string>> CheckGroupReduntCrystForms (int groupId)
		{
			string pdbId1 = "";
			string pdbId2 = "";
            Dictionary<string, int> entryInterfaceNumHash = new Dictionary<string,int> ();
            Dictionary<string, List<int>> asaInterfaceListHash = new Dictionary<string,List<int>> ();
			string spaceGroup1 = "";
			string spaceGroup2 = "";
			string asu1 = "";
			string asu2 = "";
			bool inReduntGroup = false;

			InitializeTables ();

			string queryString = string.Format ("Select Distinct GroupSeqID, PdbID, SpaceGroup, ASU From {0} WHERE GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupId);
            DataTable groupEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

			entryInterfaceNumHash.Clear ();
			asaInterfaceListHash.Clear ();

			Dictionary<int, List<string>> reduntCrystFormGroupHash = new Dictionary<int,List<string>> ();
            string abbrevCrystMethod1 = "";
            string abbrevCrystMethod2 = "";
			
			for (int i = 0; i < groupEntryTable.Rows.Count - 1; i ++)
			{
				pdbId1 = groupEntryTable.Rows[i]["PdbID"].ToString ();
				spaceGroup1 = groupEntryTable.Rows[i]["SpaceGroup"].ToString ().Trim ();
				asu1 = groupEntryTable.Rows[i]["ASU"].ToString ().Trim ();
                abbrevCrystMethod1 = ParseHelper.GetNonXrayAbbrevMethod(spaceGroup1);
		//		if (spaceGroup1 == "NMR")
                if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevCrystMethod1) > -1)
				{
					continue;
				}

				for (int j = i + 1; j < groupEntryTable.Rows.Count; j ++)
				{
					spaceGroup2 = groupEntryTable.Rows[j]["SpaceGroup"].ToString ().Trim ();
					pdbId2 = groupEntryTable.Rows[j]["PdbID"].ToString ();
					asu2 = groupEntryTable.Rows[j]["ASU"].ToString ().Trim ();
                    abbrevCrystMethod2 = ParseHelper.GetNonXrayAbbrevMethod(spaceGroup2);
				//	if (spaceGroup2 == "NMR")
                    if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevCrystMethod2) > -1)
					{
						continue;
					}
					
					DataRow dataRow = reduntCrystFormTable.NewRow ();
					dataRow["GroupSeqID"] = groupId;
					dataRow["SpaceGroup1"] = spaceGroup1;
					dataRow["ASU1"] = asu1;
					dataRow["PdbID1"] = pdbId1;
					if (AreSameCrystForms (pdbId1, pdbId2, ref entryInterfaceNumHash, ref asaInterfaceListHash, ref dataRow))
					{
						dataRow["SpaceGroup2"] = spaceGroup2;
						dataRow["ASU2"] = asu2;
						dataRow["PdbID2"] = pdbId2;
						reduntCrystFormTable.Rows.Add (dataRow);
						inReduntGroup = false;
						foreach (int reduntGroupId in reduntCrystFormGroupHash.Keys)
						{
                            if (reduntCrystFormGroupHash[reduntGroupId].Contains(pdbId1))
							{
                                reduntCrystFormGroupHash[reduntGroupId].Add(pdbId2);
								inReduntGroup = true;
							}
                            if (reduntCrystFormGroupHash[reduntGroupId].Contains(pdbId2))
							{
                                reduntCrystFormGroupHash[reduntGroupId].Add(pdbId1);
								inReduntGroup = true;
							}
						}
						if (! inReduntGroup)
						{
							List<string> reduntCrystFormList = new List<string>  ();
							reduntCrystFormList.Add (pdbId1);
							reduntCrystFormList.Add (pdbId2);
							reduntCrystFormGroupHash.Add (reduntCrystFormGroupHash.Count + 1, reduntCrystFormList);
						}
					}
				}
			}
			return reduntCrystFormGroupHash;
		}
		#endregion

		#region check same crystal forms
		/// <summary>
		/// Are these two crystal forms same or not
		/// Same Crystal forms: same number of interfaces, same number of similar interfaces
		/// </summary>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <param name="entryInterfaceNumHash"></param>
		/// <returns></returns>
		private bool AreSameCrystForms (string pdbId1, string pdbId2, 
			ref Dictionary<string, int> entryInterfaceNumHash, ref Dictionary<string, List<int>> asaInterfaceListHash, ref DataRow dataRow)
		{
			int numOfInterfaces1 = GetNumOfInterfaces (pdbId1, ref entryInterfaceNumHash);
			int numOfInterfaces2 = GetNumOfInterfaces (pdbId2, ref entryInterfaceNumHash);
			
			DataTable interfaceCompTable = GetInterfaceCompTable (pdbId1, pdbId2);
			if (interfaceCompTable.Rows.Count == 0)
			{
				return false;
			}
			if (interfaceCompTable.Rows.Count == 0)
			{
				return false;
			}
			int simInterfaceNum1 = -1;
			int simInterfaceNum2 = -1;

			double[] scores = GetSimInterfaceNums (interfaceCompTable, out simInterfaceNum1, out simInterfaceNum2, ref asaInterfaceListHash);
		
			int sgInterfaceNum1 = asaInterfaceListHash[pdbId1].Count;
			int sgInterfaceNum2 = asaInterfaceListHash[pdbId2].Count;

			double simPercent1 = (double)simInterfaceNum1 / (double)sgInterfaceNum1;
			double simPercent2 = (double)simInterfaceNum2 / (double)sgInterfaceNum2;

			if ((int)simPercent1 == 1 || (int) simPercent2 == 1 ||
				(simPercent1 >= simPercent && simPercent2 >= simPercent))
			{		
				dataRow["NumOfInterfaces1"] = numOfInterfaces1;				
				dataRow["NumOfSgInterfaces1"] = sgInterfaceNum1;				
				dataRow["NumOfSimInterfaces1"] = simInterfaceNum1;
				dataRow["NumOfInterfaces2"] = numOfInterfaces2;
				dataRow["NumOfSgInterfaces2"] = sgInterfaceNum2;
				dataRow["NumOfSimInterfaces2"] = simInterfaceNum2;
				dataRow["MaxQ"] = scores[0];
				dataRow["MinQ"] = scores[1];
				dataRow["LeftMaxQ"] = scores[2];
				dataRow["LeftMinQ"] = scores[3];
				dataRow["LeftMaxAsa1"] = scores[4];
				dataRow["LeftMaxAsa2"] = scores[5];
				double identity = GetIdentity (pdbId1, pdbId2);
				dataRow["Identity"] = identity;
				return true;
			}
			return false;
		}

		private DataTable GetInterfaceCompTable (string pdbId1, string pdbId2)
		{
			string queryString = string.Format ("SELECT * FROM DifEntryInterfaceComp " + 
				" WHERE PdbID1 = '{0}' AND PdbID2 = '{1}';", pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (interfaceCompTable.Rows.Count == 0)
			{
				queryString = string.Format ("SELECT * FROM DifEntryInterfaceComp " + 
					" WHERE PdbID2 = '{0}' AND PdbID1 = '{1}';", pdbId1, pdbId2);
                DataTable tempCompTable = ProtCidSettings.protcidQuery.Query( queryString);
				foreach (DataRow tempRow in tempCompTable.Rows)
				{
					DataRow newRow = interfaceCompTable.NewRow ();
					newRow.ItemArray = tempRow.ItemArray;
					newRow["PdbID1"] = tempRow["PdbID2"];
					newRow["InterfaceID1"] = tempRow["InterfaceID2"];
					newRow["PdbID2"] = tempRow["PdbID1"];
					newRow["InterfaceID2"] = tempRow["InterfaceID1"];
					interfaceCompTable.Rows.Add (newRow);
				}
			}
			return interfaceCompTable;
		}

		/// <summary>
		/// number of similar interfaces
		/// </summary>
		/// <param name="interfaceCompTable"></param>
		/// <param name="simInterfaceNum1"></param>
		/// <param name="simInterfaceNum2"></param>
		private double[] GetSimInterfaceNums (DataTable interfaceCompTable, 
			out int simInterfaceNum1, out int simInterfaceNum2, ref Dictionary<string, List<int>> asaInterfaceListHash)
		{
            List<int> simInterfaceList1 = new List<int>();
            List<int> simInterfaceList2 = new List<int>();

			double maxQ = -1.0;
			double minQ = minVal;
			double leftMaxQ = -1.0;
			double leftMinQ = minVal;
			double leftMaxAsa1 = -1.0;
			double leftMaxAsa2 = -1.0;
			double qScore = 0.0;

			string pdbId1 = interfaceCompTable.Rows[0]["PdbID1"].ToString ();
			string pdbId2 = interfaceCompTable.Rows[0]["PdbID2"].ToString ();
			List<int> totalInterfaceList1 = GetAsaInterfaceList (pdbId1, ref asaInterfaceListHash);
			List<int> totalInterfaceList2 = GetAsaInterfaceList (pdbId2, ref asaInterfaceListHash);

			List<int> origInterfaceList1 = new List<int> (totalInterfaceList1);
			List<int> origInterfaceList2 = new List<int> (totalInterfaceList2);

			foreach (DataRow dRow in interfaceCompTable.Rows)
			{
				qScore = Convert.ToDouble (dRow["QScore"].ToString ());
				if (qScore >= simQScore)
				{
					if (! origInterfaceList1.Contains (Convert.ToInt32 (dRow["InterfaceID1"].ToString ())) &&
						! origInterfaceList2.Contains (Convert.ToInt32 (dRow["InterfaceID2"].ToString ())))
					{
						continue;
					}
					if (origInterfaceList1.Contains (Convert.ToInt32 (dRow["InterfaceID1"].ToString ())))
					{
						if (! simInterfaceList1.Contains (Convert.ToInt32 (dRow["InterfaceID1"].ToString ())))
						{
							simInterfaceList1.Add (Convert.ToInt32 (dRow["InterfaceID1"].ToString ()));
							totalInterfaceList1.Remove (Convert.ToInt32 (dRow["InterfaceID1"].ToString ()));
						}
					}
					if (origInterfaceList2.Contains (Convert.ToInt32 (dRow["InterfaceID2"].ToString ())))
					{
						if (! simInterfaceList2.Contains (Convert.ToInt32 (dRow["InterfaceID2"].ToString ())))
						{
							simInterfaceList2.Add (Convert.ToInt32 (dRow["InterfaceID2"].ToString ()));
							totalInterfaceList2.Remove (Convert.ToInt32 (dRow["InterfaceID2"].ToString ()));
						}
					}
					if (maxQ < qScore)
					{
						maxQ = qScore;
					}
					if (minQ > qScore)
					{
						minQ = qScore;
					}
				}
			}

			if (totalInterfaceList1.Count == 0 && totalInterfaceList2.Count == 0)
			{
				leftMaxQ = -1;
				leftMinQ = -1;
				leftMaxAsa1 = -1.0;
				leftMaxAsa2 = -1.0;
			}
			bool skipMinQ = false;
			if (totalInterfaceList1.Count > 0)
			{
				foreach (int interfaceId1 in totalInterfaceList1)
				{
					DataRow[] qscoreRows = interfaceCompTable.Select (string.Format ("InterfaceID1 = {0}", interfaceId1));
					GetLeftMaxMinQScores (qscoreRows, ref leftMaxQ, ref leftMinQ, ref skipMinQ);
				}
				leftMaxAsa1 = GetEntryInterfaceMaxAsa (pdbId1, totalInterfaceList1);
			}

			if (totalInterfaceList2.Count > 0)
			{
				foreach (int interfaceId2 in totalInterfaceList2)
				{
					DataRow[] qscoreRows = interfaceCompTable.Select (string.Format ("InterfaceID2 = {0}", interfaceId2));
					GetLeftMaxMinQScores (qscoreRows, ref leftMaxQ, ref leftMinQ, ref skipMinQ);
				}
				leftMaxAsa2 = GetEntryInterfaceMaxAsa (pdbId2, totalInterfaceList2);
			}
		
			simInterfaceNum1 = simInterfaceList1.Count;
			simInterfaceNum2 = simInterfaceList2.Count;

			double[] scores = new double [6];
			scores[0] = maxQ;
			scores[1] = minQ;
			scores[2] = leftMaxQ;
			if (leftMinQ == minVal)
			{
				scores[3] = -1.0;
			}
			else
			{
				scores[3] = leftMinQ;
			}
			scores[4] = leftMaxAsa1;
			scores[5] = leftMaxAsa2;
			return scores;
		}
		/// <summary>
		/// the number of interfaces of the entry
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="entryInterfaceNumHash"></param>
		/// <returns></returns>
		private int GetNumOfInterfaces (string pdbId, ref Dictionary<string, int> entryInterfaceNumHash)
		{
			int numOfInterfaces = -1;
			if (entryInterfaceNumHash.ContainsKey (pdbId))
			{
				numOfInterfaces = entryInterfaceNumHash[pdbId];
			}
			else
			{
				string queryString = string.Format ("Select Distinct InterfaceID " + 
					" FROM CrystEntryInterfaces WHERE PdbId = '{0}';", pdbId);
                DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
				numOfInterfaces = interfaceTable.Rows.Count;
				entryInterfaceNumHash.Add (pdbId, numOfInterfaces);
			}
			return numOfInterfaces;
		}

		private List<int> GetSgInterfaceList (string pdbId, ref Dictionary<string, List<int>> sgInterfaceListHash)
		{			
			if (sgInterfaceListHash.ContainsKey (pdbId))
			{
                return sgInterfaceListHash[pdbId];
			}
            List<int> sgInterfaceList = new List<int>();
			string queryString = string.Format ("Select Distinct InterfaceID From {0}" + 
				" Where PdbID = '{1}' AND SurfaceArea >= {2};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], pdbId, asaCutoff);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
			
			foreach (DataRow interfaceRow in interfaceTable.Rows)
			{
				sgInterfaceList.Add (Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()));		
			}
			sgInterfaceListHash.Add (pdbId, sgInterfaceList);
			return sgInterfaceList;
		}

		private List<int> GetAsaInterfaceList (string pdbId, ref Dictionary<string, List<int>> asaInterfaceListHash)
		{			
			if (asaInterfaceListHash.ContainsKey (pdbId))
			{
                return asaInterfaceListHash[pdbId];
			}

			string queryString = string.Format ("Select Distinct InterfaceID From CrystEntryInterfaces" + 
				" Where PdbID = '{0}' AND SurfaceArea >= {1};", pdbId, asaCutoff);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
			List<int> asaInterfaceList = new List<int>  ();
			foreach (DataRow interfaceRow in interfaceTable.Rows)
			{
				asaInterfaceList.Add (Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()));		
			}
			asaInterfaceListHash.Add (pdbId, asaInterfaceList);
			return asaInterfaceList;
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
		private double GetIdentity (string pdbId1, string pdbId2)
		{
			string queryString = string.Format ("Select * FROM {0} " + 
				" Where (PdbID1 = '{1}' AND PdbID2 = '{2}') OR " + 
				" (PdbID1 = '{2}' AND PdbID2 = '{1}');", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign], pdbId1, pdbId2);
            DataTable identityTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, double> entityMaxIdentityHash1 = new Dictionary<int, double>();
            Dictionary<int, double> entityMaxIdentityHash2 = new Dictionary<int,double> ();
            int entityId1 = -1;
            int entityId2 = -1;
            double identity = 0.0;
            foreach (DataRow identityRow in identityTable.Rows)
            {
                entityId1 = Convert.ToInt32(identityRow["EntityID1"].ToString ());
                entityId2 = Convert.ToInt32(identityRow["EntityID2"].ToString ());
                identity = Convert.ToDouble(identityRow["Identity"].ToString ());
                if (entityMaxIdentityHash1.ContainsKey (entityId1))
                {
                    double maxIdentity = (double)entityMaxIdentityHash1[entityId1];
                    if (maxIdentity < identity)
                    {
                        entityMaxIdentityHash1[entityId1] = identity;
                    }
                }
                else
                {
                    entityMaxIdentityHash1.Add(entityId1, identity);
                }
                if (entityMaxIdentityHash2.ContainsKey (entityId2))
                {
                    double maxIdentity = (double)entityMaxIdentityHash2[entityId2];
                    if (maxIdentity < identity)
                    {
                        entityMaxIdentityHash2[entityId2] = identity;
                    }
                }
                else
                {
                    entityMaxIdentityHash2.Add(entityId2, identity);
                }
            }
            double minIdentity = 100.0;
            foreach (int entityId in entityMaxIdentityHash1.Keys)
            {
                identity = (double)entityMaxIdentityHash1[entityId];
                if (minIdentity > identity)
                {
                    minIdentity = identity;
                }
            }
            foreach (int entityId in entityMaxIdentityHash2.Keys)
            {
                identity = (double)entityMaxIdentityHash2[entityId];
                if (minIdentity > identity)
                {
                    minIdentity = identity;
                }
            }
            if (entityMaxIdentityHash1.Count == 0 && entityMaxIdentityHash2.Count == 0)
            {
                minIdentity = -1;
            }
            return minIdentity;
	/*		if (identityTable.Rows.Count > 0)
			{
				return Convert.ToDouble (identityTable.Rows[0]["Identity"].ToString ());
			}
			else
			{
				return -1;
			}*/
		}

		private void GetLeftMaxMinQScores (DataRow[] qscoreRows, ref double leftMaxQ, ref double leftMinQ, ref bool skipMinQ)
		{
			double qScore = 0.0;
			if (qscoreRows.Length == 0)
			{
				leftMinQ = minVal;
				skipMinQ = true;
			}
			foreach (DataRow qscoreRow in qscoreRows)
			{
				qScore = Convert.ToDouble (qscoreRow["QScore"].ToString ());
				if (leftMaxQ < qScore)
				{
					leftMaxQ = qScore;
				}
				if (! skipMinQ)
				{
					if (leftMinQ > qScore)
					{
						leftMinQ = qScore;
					}
				}
			}
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceList"></param>
        /// <returns></returns>
		private double GetEntryInterfaceMaxAsa (string pdbId, List<int> interfaceList)
		{
	//		string queryString = string.Format ("Select PdbID, InterfaceID, SurfaceArea From CrystEntryInterfaces " + 
	//			" Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString (interfaceList));
            string queryString = string.Format("Select PdbId, InterfaceID, SurfaceArea From CrystEntryInterfaces " + 
                " Where PdbID = '{0}';", pdbId);
            DataTable interfaceAsaTable = ProtCidSettings.protcidQuery.Query( queryString);
			double maxAsa = -1.0;
			double asa = 0.0;
            int interfaceId = -1;
			foreach (DataRow asaRow in interfaceAsaTable.Rows)
			{
                interfaceId = Convert.ToInt32(asaRow["InterfaceID"].ToString ());
                if (! interfaceList.Contains(interfaceId))
                {
                    continue;
                }
				asa = Convert.ToDouble (asaRow["SurfaceArea"].ToString ());
				if (maxAsa < asa)
				{
					maxAsa = asa;
				}
			}
			return maxAsa;
		}
		#endregion

		#region initialize 
		private void InitializeTables ()
		{
			reduntCrystFormTable = 
				new DataTable (GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
			string[] colNames = {"GroupSeqID", "SpaceGroup1", "ASU1", "PdbID1", "NumOfInterfaces1", 
									"NumOfSgInterfaces1", "NumOfSimInterfaces1",
									"SpaceGroup2", "ASU2", "PdbID2", "NumOfInterfaces2", 
									"NumOfSgInterfaces2", "NumOfSimInterfaces2", 
									"MaxQ", "MinQ", "LeftMaxQ", "LeftMinQ", "Identity", "LeftMaxAsa1", "LeftMaxAsa2"};
			foreach (string colName in colNames)
			{
				reduntCrystFormTable.Columns.Add (new DataColumn (colName));
			}
		}

		private void InitializeDbTables ()
		{
			DbCreator dbCreate = new DbCreator ();
			string createTableString = string.Format ("CREATE Table {0} ( " + 
				" GroupSeqID INTEGER NOT NULL, " + 
				" SpaceGroup1 VARCHAR(30) NOT NULL, ASU1 VARCHAR(255) NOT NULL, PDBID1 CHAR(4) NOT NULL, " + 
				" NumOfInterfaces1 INTEGER NOT NULL, " + 
				" NumOfSgInterfaces1 INTEGER NOT NULL, NumOfSimInterfaces1 INTEGER NOT NULL, " + 
				" SpaceGroup2 VARCHAR(30) NOT NULL, ASU2 VARCHAR(255) NOT NULL, PDBID2 CHAR(4) NOT NULL, " + 
				" NumOfInterfaces2 INTEGER NOT NULL, " + 
				" NumOfSgInterfaces2 INTEGER NOT NULL, NumOfSimInterfaces2 INTEGER NOT NULL, " +
				" MaxQ FLOAT NOT NULL, MinQ FLOAT NOT NULL, " + 
				" LeftMaxQ FLOAT NOT NULL, LeftMinQ FLOAT NOT NULL, Identity FLOAT NOT NULL, " + 
				" LeftMaxAsa1 FLOAT NOT NULL, LeftMaxAsa2 FLOAT NOT NULL);", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
			string indexString = string.Format ("CREATE INDEX {0}_Idx1 ON {0} (PdbID1);", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
			indexString = string.Format ("CREATE INDEX {0}_Idx2 ON {0} (PdbID2);", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
		}
		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        private void DeleteObsData(int groupId)
        {
            string deleteString = string.Format("Delete From {0} Where GroupSeqID = {1};",
                    GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms],
                    groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
		#endregion
	}
}
