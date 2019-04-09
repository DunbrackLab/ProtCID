using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using DbLib;
using InterfaceClusterLib.DataTables;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.Clustering
{
	/// <summary>
	/// Summary description for NonredundantCfGroups.
	/// </summary>
	public class NonredundantCfGroups
	{
		private DbQuery dbQuery = new DbQuery ();
		private double simPercent = 0.7;
		private double leftAsaCutoff = 500.0;
        private double mergeIdentity = 70.0;

		public NonredundantCfGroups()
		{
			//
			// TODO: Add constructor logic here
			//
		}	
		#region nonredundant cf groups in db
		/// <summary>
		/// insert redundant crystal forms summary info to db table
		/// </summary>
		public void UpdateCfGroupInfo ()
		{
			InitializeDbTable ();
			DataTable cfGroupTable = InitializeTable ();

            Dictionary<int, Dictionary<int, List<string>>> familyReduntCfHash = GetRedundantCFs();
	/*		string queryString = string.Format ("Select Distinct GroupSeqID From {0};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces]);*/
            string queryString = string.Format("Select Distinct GroupSeqID From {0};", 
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo]);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;
			ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;
			ProtCidSettings.progressInfo.currentOperationLabel = "Update CF Info.";

			int groupId = 0;
			foreach (DataRow dRow in groupTable.Rows)
			{
				groupId = Convert.ToInt32 (dRow["GroupSeqID"].ToString ());
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = groupId.ToString ();

				GetNonReduntCfGroupsInFamily (groupId, familyReduntCfHash[groupId], ref cfGroupTable);
			}
			DbInsert dbInsert = new DbInsert ();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, cfGroupTable);
	//		DbBuilder.dbConnect.DisconnectFromDatabase ();
		}

		/// <summary>
		/// update nonredundant cf group table
		/// </summary>
		public void UpdateCfGroupInfo (int[] updateGroups)
		{
			DataTable cfGroupTable = InitializeTable ();
			
			Dictionary<int, Dictionary<int, List<string>>> familyReduntCfHash = GetRedundantCFs (updateGroups);
			foreach (int groupId in updateGroups)
			{
				GetNonReduntCfGroupsInFamily (groupId, familyReduntCfHash[groupId], ref cfGroupTable);
			}
			DeleteObsData (updateGroups);
			DbInsert dbInsert = new DbInsert ();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, cfGroupTable);
	//		DbBuilder.dbConnect.DisconnectFromDatabase ();
		}
		#endregion

		#region nonredundant cf groups in db
		/// <summary>
		/// 
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="reduntCfHash"></param>
		/// <param name="cfGroupTable"></param>
		private void GetNonReduntCfGroupsInFamily (int groupId, Dictionary<int, List<string>> reduntCfHash, ref DataTable cfGroupTable)
		{
			int cfGroupId = 0;
			string queryString = string.Format ("Select Distinct SpaceGroup, ASU, PdbID " + 
				" From {0} Where GroupSeqID = {1};", 
		//		GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
			List<string> groupEntryList = new List<string> ();
			string spaceGroup = "";
			string asu = "";
			List<string> nmrEntryList = new List<string> ();
			foreach (DataRow entryRow in entryTable.Rows)
			{
                spaceGroup = entryRow["SpaceGroup"].ToString ().TrimEnd ();
             //   abbrevCrystMethod = ParseHelper.GetNonXrayAbbrevMethod(spaceGroup);
				if (spaceGroup.IndexOf ("NMR") > -1)
          //      if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevCrystMethod) > -1)
				{
					if (! nmrEntryList.Contains (entryRow["PdbID"].ToString ()))
					{
						nmrEntryList.Add (entryRow["PdbID"].ToString ());
					}
				}
				else
				{
					groupEntryList.Add (entryRow["PdbID"].ToString ());
				}
			}
			if (reduntCfHash != null)
			{
				List<int> reduntCfGroups = new List<int> (reduntCfHash.Keys);
				reduntCfGroups.Sort ();
				foreach (int reduntCfId in reduntCfGroups)
				{
					cfGroupId ++;
                    foreach (string entry in reduntCfHash[reduntCfId])
					{
						groupEntryList.Remove (entry);
					}
                    foreach (string sameCfEntry in reduntCfHash[reduntCfId])
					{
						GetEntryCfString (sameCfEntry, entryTable, ref spaceGroup, ref asu);
						DataRow newRow = cfGroupTable.NewRow ();
						newRow["GroupSeqID"] = groupId;
						newRow["CfGroupID"] = cfGroupId;
						newRow["PdbID"] = sameCfEntry;
						newRow["SpaceGroup"] = spaceGroup;
						newRow["ASU"] = asu;
						cfGroupTable.Rows.Add (newRow);
					}
				
				}
			}
			foreach (string leftEntry in groupEntryList)
			{
				cfGroupId ++;
				GetEntryCfString (leftEntry, entryTable, ref spaceGroup, ref asu);
				DataRow newRow = cfGroupTable.NewRow ();
				newRow["GroupSeqID"] = groupId;
				newRow["CfGroupID"] = cfGroupId;
				newRow["PdbID"] = leftEntry;
				newRow["SpaceGroup"] = spaceGroup;
				newRow["ASU"] = asu;
				cfGroupTable.Rows.Add (newRow);
			}
            Dictionary<string, List<string>> nmrGroupHash = GetNmrEntriesGroups(nmrEntryList, entryTable);
			foreach (string cfString in nmrGroupHash.Keys)
			{
				cfGroupId ++;
				foreach (string nmrEntry in nmrGroupHash[cfString])
				{
					GetEntryCfString (nmrEntry, entryTable, ref spaceGroup, ref asu);
					DataRow newRow = cfGroupTable.NewRow ();
					newRow["GroupSeqID"] = groupId;
					newRow["CfGroupID"] = cfGroupId;
					newRow["PdbID"] = nmrEntry;
					newRow["SpaceGroup"] = spaceGroup;
					newRow["ASU"] = asu;
					cfGroupTable.Rows.Add (newRow);
				}
			}
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="cfInfoTable"></param>
        /// <param name="spaceGroup"></param>
        /// <param name="asu"></param>
		private void GetEntryCfString (string pdbId, DataTable cfInfoTable, ref string spaceGroup, ref string asu)
		{
			spaceGroup = "";
			asu = "";
			DataRow[] cfStringRows = cfInfoTable.Select (string.Format ("PdbID = '{0}'", pdbId));
			spaceGroup = cfStringRows[0]["SpaceGroup"].ToString ().TrimEnd ();
			asu = cfStringRows[0]["ASU"].ToString ().TrimEnd ();
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nmrEntryList"></param>
        /// <param name="cfInfoTable"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetNmrEntriesGroups(List<string> nmrEntryList, DataTable cfInfoTable)
		{
			Dictionary<string, List<string>> nmrGroupHash = new Dictionary<string,List<string>> ();
			string spaceGroup = "";
			string asu = "";
			string realAsu = "";
			foreach (string entry in nmrEntryList)
			{
				GetEntryCfString (entry, cfInfoTable, ref spaceGroup, ref asu);
				realAsu = GetTrueAsu (asu);
				if (nmrGroupHash.ContainsKey (spaceGroup + "_" + realAsu))
				{
                    nmrGroupHash[spaceGroup + "_" + realAsu].Add(entry);
				}
				else
				{
					List<string> cfNmrEntryList = new List<string> ();
					cfNmrEntryList.Add (entry);
					nmrGroupHash.Add (spaceGroup + "_" + realAsu, cfNmrEntryList) ;
				}
			}
			return nmrGroupHash;
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuString"></param>
        /// <returns></returns>
		private string GetTrueAsu (string asuString)
		{
			string asu = asuString;
			int parenthesisIdx = asuString.IndexOf ("(");
			if (parenthesisIdx > -1)
			{
				asu = asuString.Substring (0, parenthesisIdx);
			}
			return asu;
		}
		#endregion

		#region update Cf group Info for each group
        public Dictionary<int, Dictionary<int, List<string>>> GetRedundantCFs()
		{
            Dictionary<int, Dictionary<int, List<string>>> familyReduntCfHash = new Dictionary<int, Dictionary<int, List<string>>>();

			string queryString = string.Format ("Select Distinct GroupSeqID From {0};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms]);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
			int groupId = 0;
			foreach (DataRow groupRow in groupTable.Rows)
			{
				groupId = Convert.ToInt32 (groupRow["GroupSeqID"].ToString ());
				queryString = string.Format ("Select * From {0} WHERE GroupSeqID = {1};", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms], groupId);
                DataTable reduntCfTable = ProtCidSettings.protcidQuery.Query( queryString);
				Dictionary<int, List<string>> reduntCfGroupHash = GetReduntCrystFormEntryHash (reduntCfTable);
				familyReduntCfHash.Add (groupId, reduntCfGroupHash);
			}
			return familyReduntCfHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
        /// <returns></returns>
        public Dictionary<int, Dictionary<int, List<string>>> GetRedundantCFs(int[] groups)
		{
            Dictionary<int, Dictionary<int, List<string>>> familyReduntCfHash = new Dictionary<int, Dictionary<int, List<string>>>();
			string queryString = "";

			foreach (int groupId in groups)
			{
				queryString = string.Format ("Select * From {0} WHERE GroupSeqID = {1};", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.ReduntCrystForms], groupId);
                DataTable reduntCfTable = ProtCidSettings.protcidQuery.Query( queryString);
				Dictionary<int, List<string>> reduntCfGroupHash = GetReduntCrystFormEntryHash (reduntCfTable);
				familyReduntCfHash.Add (groupId, reduntCfGroupHash);
			}
			return familyReduntCfHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupReduntCrystFormTable"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetReduntCrystFormEntryHash(DataTable groupReduntCrystFormTable)
		{
			Dictionary<string, List<string>> groupReduntCfHash = GetReduntCfsOfCf (groupReduntCrystFormTable);
			Dictionary<int, List<string>> reduntCfGroupHash = new Dictionary<int,List<string>>  ();
			while (groupReduntCfHash.Count > 0)
			{
				string maxCfPdbId = GetCfWithMaxNumOfReduntCFs (groupReduntCfHash);
				List<string> reduntCfList = GetRedundantCFList (maxCfPdbId, ref groupReduntCfHash);
				if (reduntCfList.Count < 2)
				{
					continue;
				}
				reduntCfGroupHash.Add (reduntCfGroupHash.Count + 1, reduntCfList);
			}
			return reduntCfGroupHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupReduntCrystFormTable"></param>
        /// <returns></returns>
		private Dictionary<string, List<string>> GetReduntCfsOfCf (DataTable groupReduntCrystFormTable)
		{
			Dictionary<string, List<string>> reduntCfGroupHash = new Dictionary<string,List<string>> ();
			string cf1 = "";
			string cf2 = "";
			double simPercent1 = 0.0;
			double simPercent2 = 0.0;
			double leftMaxAsa1 = 0.0;
			double leftMaxAsa2 = 0.0;
			string spaceGroup1 = "";
			string spaceGroup2 = "";
            double identity = 0.0;
			bool isRedunt = false;
			foreach (DataRow reduntCfRow in groupReduntCrystFormTable.Rows)
			{
				isRedunt = false;
				simPercent1 = Convert.ToDouble (reduntCfRow["NumOfSimInterfaces1"].ToString ()) / 
					Convert.ToDouble (reduntCfRow["NumOfSgInterfaces1"].ToString ());
				simPercent2 = Convert.ToDouble (reduntCfRow["NumOfSimInterfaces2"].ToString ()) / 
					Convert.ToDouble (reduntCfRow["NumOfSgInterfaces2"].ToString ());
				leftMaxAsa1 = Convert.ToDouble (reduntCfRow["LeftMaxAsa1"].ToString ());
				leftMaxAsa2 = Convert.ToDouble (reduntCfRow["LeftMaxAsa2"].ToString ());
				spaceGroup1 = reduntCfRow["SpaceGroup1"].ToString ().Trim ();
				spaceGroup2 = reduntCfRow["SpaceGroup2"].ToString ().Trim ();

                identity = Convert.ToDouble(reduntCfRow["Identity"].ToString ());

                if (identity < mergeIdentity)
                {
                    isRedunt = false;
                }
				else if ((int)simPercent1 == 1 && (int)simPercent2 == 1)
				{
					isRedunt = true;
				}
				else if (((int)simPercent1 == 1 && simPercent2 >= 0.5) ||
					((int)simPercent2 == 1 && simPercent1 >= 0.5))
				{
					if (spaceGroup1 == spaceGroup2 || (leftMaxAsa1 < leftAsaCutoff && leftMaxAsa2 < leftAsaCutoff))
					{
						isRedunt = true;
					}
				}
				else if (simPercent1 >= simPercent && simPercent2 >= simPercent)
				{
					if (leftMaxAsa1 < leftAsaCutoff && leftMaxAsa2 < leftAsaCutoff)
					{
						isRedunt = true;
					}
				}
				/*		if (((int)simPercent1 == 1 && simPercent2 >= simPercent)
							|| ((int)simPercent2 == 1 && simPercent1 >= simPercent))*/
				if (isRedunt)
				{
					cf1 = reduntCfRow["PdbID1"].ToString ();
					cf2 = reduntCfRow["PdbID2"].ToString ();
					if (reduntCfGroupHash.ContainsKey (cf1))
					{
                        if (!reduntCfGroupHash[cf1].Contains(cf2))
						{
                            reduntCfGroupHash[cf1].Add(cf2);
						}
					}
					else
					{
						List<string> reduntCfList = new List<string> ();
						reduntCfList.Add (cf2);
						reduntCfGroupHash.Add (cf1, reduntCfList);
					}

					if (reduntCfGroupHash.ContainsKey (cf2))
					{
                        if (!reduntCfGroupHash[cf2].Contains(cf1))
						{
                            reduntCfGroupHash[cf2].Add(cf1);
						}
					}
					else
					{
						List<string> reduntCfList = new List<string> ();
						reduntCfList.Add (cf1);
						reduntCfGroupHash.Add (cf2, reduntCfList);
					}
				}
			}
			return reduntCfGroupHash;
		}
		
		private string GetCfWithMaxNumOfReduntCFs (Dictionary<string, List<string>> reduntCfGroupHash)
		{
			string maxCfString = "";
			int maxNumOfCFs = 0;

			foreach (string cf in reduntCfGroupHash.Keys)
			{
                if (maxNumOfCFs < reduntCfGroupHash[cf].Count)
				{
                    maxNumOfCFs = reduntCfGroupHash[cf].Count;
					maxCfString = cf;
				}
			}
			return maxCfString;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxCfPdbId"></param>
        /// <param name="groupReduntCfHash"></param>
        /// <returns></returns>
		private List<string> GetRedundantCFList (string maxCfPdbId, ref Dictionary<string, List<string>> groupReduntCfHash)
		{
			List<string> reduntCfList = groupReduntCfHash[maxCfPdbId];
			string[] sortedRedundantCFs = SortRedundantCFs (reduntCfList, groupReduntCfHash);
			
			List<string> reduntGroupCfList = new List<string> ();
			reduntGroupCfList.Add (maxCfPdbId);
			if (sortedRedundantCFs != null)
			{
				reduntGroupCfList.Add (sortedRedundantCFs[0]);
				bool canBeAdded = false;
				for (int i = 1; i < sortedRedundantCFs.Length; i ++)
				{
					canBeAdded = true;
                    foreach (string cf in groupReduntCfHash[sortedRedundantCFs[i]])
					{
                        if (!groupReduntCfHash[sortedRedundantCFs[i]].Contains(cf))
						{
							canBeAdded = false;
							break;
						}
					}
					if (canBeAdded)
					{
						reduntGroupCfList.Add (sortedRedundantCFs[i]);
					}
				}
			}
			foreach (string cf in reduntGroupCfList)
			{
				groupReduntCfHash.Remove (cf);
			}
			return reduntGroupCfList;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cfList"></param>
        /// <param name="reduntCfGroupHash"></param>
        /// <returns></returns>
		private string[] SortRedundantCFs (List<string> cfList, Dictionary<string, List<string>> reduntCfGroupHash)
		{
			List<string> numOfRedundantCfStrings = new List<string> ();
			for (int i = 0; i < cfList.Count; i ++)
			{
				if (reduntCfGroupHash.ContainsKey (cfList[i].ToString ()))
				{
					List<string> reduntCfList = reduntCfGroupHash[cfList[i].ToString ()];
					numOfRedundantCfStrings.Add (reduntCfList.Count.ToString ().PadLeft (3, '0') + "_" + cfList[i].ToString ());
				}
			}
			
			if (numOfRedundantCfStrings.Count > 0)
			{
				numOfRedundantCfStrings.Sort ();
				string[] sortedCfList = new string [numOfRedundantCfStrings.Count];
				int count = numOfRedundantCfStrings.Count - 1;
				foreach (string numCfString in numOfRedundantCfStrings)
				{
					if (numCfString == null)
					{
						continue;
					}
					string[] fields = numCfString.Split ('_');
					sortedCfList[count] = fields[1];
					count --;
				}
				return sortedCfList;
			}
			return null;
		}
		#endregion

		#region initialize tables
		private void InitializeDbTable ()
		{
			DbCreator dbCreat = new DbCreator ();
			string createTableString = string.Format ("CREATE TABLE {0} ( " + 
				" GroupSeqID INTEGER NOT NULL, " +
				" CfGroupID INTEGER NOT NULL, " + 
				" PdbID CHAR(4) NOT NULL, " + 
				" SpaceGroup VARCHAR(30) NOT NULL, " + 
				" ASU VARCHAR(255) NOT NULL);", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);
            dbCreat.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);

            string indexString = string.Format("Create Index {0}_pdb on {0} (PdbID);", 
                GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);
            dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, 
                GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);

            indexString = string.Format("Create Index {0}_groupcf on {0} (GroupSeqID, CfGroupID);",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);
            dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, indexString,
                GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);
		}

		private DataTable InitializeTable ()
		{
			DataTable reduntCfGroupTable = new DataTable 
				(GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups]);
			string[] cols = {"GroupSeqID", "CfGroupID", "PdbID", "SpaceGroup", "ASU"};
			foreach (string col in cols)
			{
				reduntCfGroupTable.Columns.Add (new DataColumn (col));
			}
			return reduntCfGroupTable;
		}

		private void DeleteObsData (int[] updateGroups)
		{
			string deleteString = "";
			foreach (int groupId in updateGroups)
			{
				deleteString = string.Format ("Delete From {0} Where GroupSeqID = {1};", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups], groupId);
                ProtCidSettings.protcidQuery.Query( deleteString);
			}
		}
		#endregion

        #region for debug
        /// <summary>
        /// add the cfgroupid for those entries are not in the pfamgroups 
        /// temporary added for the domain interface data, should discard later
        /// </summary>
        public void UpdateCfGroupInfoForDebug ()
        {
            DbInsert dbInsert = new DbInsert();

            DataTable cfGroupTable = InitializeTable();
            Dictionary<int, string[]> updateGroupEntryHash = ReadUpdateEntries();
            List<int> groupIdList = new List<int> (updateGroupEntryHash.Keys);
            groupIdList.Sort();
            foreach (int groupId in groupIdList)
            {
                string[] updateEntries = updateGroupEntryHash[groupId];
                GetNonReduntCfGroupsInFamilyForDebug(groupId, updateEntries, ref cfGroupTable);

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, cfGroupTable);
                cfGroupTable.Clear();
            }
            //		DbBuilder.dbConnect.DisconnectFromDatabase ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reduntCfHash"></param>
        /// <param name="cfGroupTable"></param>
        private void GetNonReduntCfGroupsInFamilyForDebug (int groupId, string[] updateEntries, ref DataTable cfGroupTable)
        {
            int cfGroupId = GetGroupMaxCfGroupId (groupId);
            string queryString = string.Format("Select Distinct SpaceGroup, ASU, PdbID " +
                " From {0} Where GroupSeqID = {1} AND PdbID IN ({2});",
                //		GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupId, ParseHelper.FormatSqlListString (updateEntries));
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> groupEntryList = new List<string> ();
            string spaceGroup = "";
            string asu = "";
            List<string> nmrEntryList = new List<string> ();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID"].ToString());
            }

            Dictionary<string, List<string>> nmrGroupHash = GetNmrEntriesGroups(groupEntryList, entryTable);
            foreach (string cfString in nmrGroupHash.Keys)
            {
                cfGroupId++;
                foreach (string nmrEntry in nmrGroupHash[cfString])
                {
                    GetEntryCfString(nmrEntry, entryTable, ref spaceGroup, ref asu);
                    DataRow newRow = cfGroupTable.NewRow();
                    newRow["GroupSeqID"] = groupId;
                    newRow["CfGroupID"] = cfGroupId;
                    newRow["PdbID"] = nmrEntry;
                    newRow["SpaceGroup"] = spaceGroup;
                    newRow["ASU"] = asu;
                    cfGroupTable.Rows.Add(newRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int GetGroupMaxCfGroupId(int groupId)
        {
            string queryString = string.Format("Select Max(CfGroupID) As MaxCfGroupId From PfamNonRedundantCfGroups Where GroupSeqID = {0};", groupId);
            DataTable maxCfGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int maxCfGroupId = 1;
            string maxGroupString = maxCfGroupIdTable.Rows[0]["MaxCfGroupId"].ToString();
            if (maxGroupString != null && maxGroupString != "")
            {
                maxCfGroupId = Convert.ToInt32(maxGroupString);
            }
            return maxCfGroupId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, string[]> ReadUpdateEntries()
        {
            StreamReader dataReader = new StreamReader("UpdateGroups.txt");
            Dictionary<int, string[]> groupUpdateEntryHash = new Dictionary<int,string[]> ();
            string line = "";
            int groupId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                groupId = Convert.ToInt32(fields[0]);
                string[] updateEntries = new string[fields.Length - 1];
                Array.Copy(fields, 1, updateEntries, 0, updateEntries.Length);
                groupUpdateEntryHash.Add(groupId, updateEntries);
            }
            dataReader.Close();
            return groupUpdateEntryHash;
        }
        #endregion
    }
}
