using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.ChainInterfaces
{
    /// <summary>
    /// combine groups into supergroups if they share same pfam domain architecture
    /// </summary>
    public class ChainGroupsClassifier
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();
        private DataTable supergroupTable = null;
        private DataTable superCfGroupTable = null;
        #endregion

        #region chain relation groups
        /// <summary>
        /// 
        /// </summary>
        public void CombineFamilyGroups()
        {
            Initialize(false);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Combine Family Groups";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Combine family groups.");

            ProtCidSettings.logWriter.WriteLine("Combine family groups.");
            
            string queryString = string.Format ("Select * From {0}Groups Order By EntryPfamArch;", ProtCidSettings.dataType);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;
            ProtCidSettings.logWriter.WriteLine("# of entry groups = " + groupTable.Rows.Count.ToString ());
           
            string familyString = "";
            int GroupSeqID = -1;
            int superGroupId = 1;
            List<string> parsedSuperGroupList = new List<string> ();
            string[] supergroupArchGroups = null;
            foreach (DataRow groupRow in groupTable.Rows)
            {
                familyString = groupRow["EntryPfamArch"].ToString().TrimEnd();                

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = familyString;

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString () + " " + familyString);

                GroupSeqID = Convert.ToInt32(groupRow["GroupSeqID"].ToString ());
                // find new relations at each step
                supergroupArchGroups = GetSuperGroupArchStrings (familyString, parsedSuperGroupList);

                foreach (string supergroupArchString in supergroupArchGroups)
                {
                    string[] entityFamilyFields = supergroupArchString.Split(';');
                    int[] subSetGroups = GetSuperSetFamilyGroups(entityFamilyFields);
                    if (subSetGroups.Length > 0)
                    {
                        foreach (int subsetGroup in subSetGroups)
                        {
                            DataRow supergroupRow = supergroupTable.NewRow();
                            supergroupRow["SuperGroupSeqID"] = superGroupId;
                            supergroupRow["ChainRelPfamArch"] = supergroupArchString;
                            supergroupRow["GroupSeqID"] = subsetGroup;
                            supergroupTable.Rows.Add(supergroupRow);
                        }
                        AddCfGroupIDToSuperGroup(superGroupId, subSetGroups);

                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, supergroupTable);
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, superCfGroupTable);
                        supergroupTable.Clear();
                        superCfGroupTable.Clear();

                        superGroupId++;
                    }
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Classify chain relations Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateCfGroupTable ()
        {
            Initialize(true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Combine Family Groups";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Combine family groups.");

            string queryString = "Select Distinct SuperGroupSeqId From PfamSuperGroups;";
            DataTable superGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.totalOperationNum = superGroupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = superGroupTable.Rows.Count;
  
            int superGroupId = 1;
            foreach (DataRow groupRow in superGroupTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();

                queryString = string.Format("Select Distinct GroupSeqID From PfamSuperGroups Where SuperGroupSeqId = {0};", superGroupId);
                DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                int[] groupIds = new int[groupIdTable.Rows.Count];
                int count = 0;
                foreach (DataRow groupIdRow in groupIdTable.Rows)
                {
                    groupIds[count] = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                    count++;
                }

                AddCfGroupIDToSuperGroup(superGroupId, groupIds);

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, superCfGroupTable);
                superCfGroupTable.Clear();

            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Update chain groups done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyString"></param>
        /// <returns></returns>
        private string GetSuperGroupArchString(string familyString)
        {
            string[] fields = familyString.Split(';');
            List<string> archFieldList = new List<string> ();
            foreach (string field in fields)
            {
                if (! archFieldList.Contains(field))
                {
                    archFieldList.Add(field);
                }
            }
            string supergroupString = "";
            archFieldList.Sort();
            foreach (string field in archFieldList)
            {
                supergroupString += field;
                supergroupString += ";";
            }
            return supergroupString.TrimEnd(';');
        }

        /// <summary>
        /// single and pairwise pfam-arch groups
        /// </summary>
        /// <param name="familyString"></param>
        /// <returns></returns>
        private string[] GetSuperGroupArchStrings(string familyString, List<string> parsedSuperGroupList)
        {
            string[] fields = familyString.Split(';');
            List<string> archFieldList = new List<string> ();
            List<string> superGroupStringList = new List<string> ();
            string pairwisePfamArchString = "";
            foreach (string field in fields)
            {
                if (!archFieldList.Contains(field))
                {
                    archFieldList.Add(field);
                }
                if (parsedSuperGroupList.Contains(field))
                {
                    continue;
                }
                parsedSuperGroupList.Add(field);
                // for homo
                superGroupStringList.Add(field);
            }
            archFieldList.Sort();
           
            // for pairwise hetero
            for (int i = 0; i < archFieldList.Count; i++)
            {
                for (int j = i + 1; j < archFieldList.Count; j++)
                {
                    pairwisePfamArchString = archFieldList[i] + ";" + archFieldList[j];
                    if (parsedSuperGroupList.Contains(pairwisePfamArchString))
                    {
                        continue;
                    }
                    superGroupStringList.Add(pairwisePfamArchString);
                    parsedSuperGroupList.Add(pairwisePfamArchString);
                }
            }
            return superGroupStringList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityFamilyStrings"></param>
        /// <returns></returns>
        private int[] GetSuperSetFamilyGroups(string[] entityFamilyStrings)
        {
            List<int> superSetGroupList = new List<int> ();
            foreach (string entityFamilyString in entityFamilyStrings)
            {
                int[] relatedGroups = GetEntityGroupSupersets(entityFamilyString);
                if (superSetGroupList.Count == 0)
                {
                    superSetGroupList.AddRange(relatedGroups);
                }
                else
                {
                    List<int> tempSuperSetGroupList = new List<int> (superSetGroupList);
                    superSetGroupList.Clear();
                    foreach (int relatedGroup in relatedGroups)
                    {
                        if (tempSuperSetGroupList.Contains(relatedGroup))
                        {
                            superSetGroupList.Add(relatedGroup);
                        }
                    }
                }
            }
            return superSetGroupList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityArch"></param>
        /// <returns></returns>
        private int[] GetEntityGroupSupersets(string entityArch)
        {
            string queryString = string.Format("Select * From {0}Groups Where EntryPfamArch Like '%{1}%';", ProtCidSettings.dataType, entityArch);
            DataTable relationGroupTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<int> relatedGroupList = new List<int> ();
            bool canBeAdded = false;
            foreach (DataRow groupRow in relationGroupTable.Rows)
            {
                canBeAdded = false;
                string[] groupFields = groupRow["EntryPfamArch"].ToString().TrimEnd().Split(';') ;
                foreach (string groupField in groupFields)
                {
                    if (groupField == entityArch)
                    {
                        canBeAdded = true;
                        break;
                    }
                }
                if (canBeAdded)
                {
                    relatedGroupList.Add(Convert.ToInt32(groupRow["GroupSeqID"].ToString()));
                }
            }
            return relatedGroupList.ToArray ();
        }
        #endregion

        #region create data table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void Initialize(bool isUpdate)
        {
            CreateMemoryTableStructure ();
            if (!isUpdate)
            {
                CreateDbTable();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void CreateMemoryTableStructure()
        {
            supergroupTable = new DataTable(ProtCidSettings.dataType + "SuperGroups");
            string[] supergroupColumns = {"SuperGroupSeqID", "ChainRelPfamArch", "GroupSeqID"};
            foreach (string superGroupCol in supergroupColumns)
            {
                supergroupTable.Columns.Add(new DataColumn (superGroupCol));
            }

            superCfGroupTable = new DataTable(ProtCidSettings.dataType + "SuperCfGroups");
            string[] superCfgroupColumns = {"SuperGroupSeqID", "SuperCfGroupID", "GroupSeqID", "CfGroupID"};
            foreach (string cfGroupCol in superCfgroupColumns)
            {
                superCfGroupTable.Columns.Add(new DataColumn (cfGroupCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreateDbTable()
        {
            DbCreator dbCreate = new DbCreator();
            string tableName = "";
            string createTableString = "";
            string createIndexString = "";

            tableName = ProtCidSettings.dataType + "SuperGroups";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                " SuperGroupSeqID INTEGER NOT NULL, " +
                " ChainRelPfamArch VARCHAR(800) NOT NULL, " +
                " GroupSeqID INTEGER NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (SuperGroupSeqID);", tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_Idx2 ON {0} (ChainRelPfamArch);", tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = ProtCidSettings.dataType + "SuperCfGroups";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                " SuperGroupSeqID INTEGER NOT NULL, " +
                " SuperCfGroupID INTEGER NOT NULL, " +
                " GroupSeqID INTEGER NOT NULL, " +
                " CfGroupID INTEGER NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (SuperGroupSeqID, SuperCfGroupID);", tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_Idx2 ON {0} (GroupSeqID, CfGroupID);", tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
        }
        #endregion

        #region CFGroupID for the superGroup
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="superSetGroups"></param>
        public void AddCfGroupIDToSuperGroup(int superGroupId, int[] subSetGroups)
        {
            Array.Sort(subSetGroups);
            int superCfGroupId = 1;
            DataTable groupCfGroupTable = GetCfGroupIDs (subSetGroups);
            foreach (DataRow cfGroupRow in groupCfGroupTable.Rows)
            {
                DataRow superCfgRow = superCfGroupTable.NewRow();
                superCfgRow["SuperGroupSeqID"] = superGroupId;
                superCfgRow["SuperCfGroupID"] = superCfGroupId;
                superCfgRow["GroupSeqID"] = cfGroupRow["GroupSeqID"];
                superCfgRow["CfGroupID"] = cfGroupRow["CfGroupID"];
                superCfGroupTable.Rows.Add(superCfgRow);
                superCfGroupId++;
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int[] GetCfGroupIDs(int groupId)
        {
            string queryString = string.Format("Select Distinct CfGroupID From PfamNonRedundantCfGroups " +
                " Where GroupSeqID = {0} ORDER BY CfGroupID;", groupId);
            DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] cfGroups = new int[cfGroupTable.Rows.Count];
            int count = 0;
            foreach (DataRow cfGroupRow in cfGroupTable.Rows)
            {
                cfGroups[count] = Convert.ToInt32(cfGroupRow["CfGroupID"].ToString());
                count++;
            }
            return cfGroups;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private DataTable GetCfGroupIDs(int[] groupIds)
        {
            string queryString = "";
            DataTable cfGroupTable = null;           
            for (int i = 0; i < groupIds.Length; i += 300)
            {
                int[] subGroupIds = ParseHelper.GetSubArray (groupIds, i, 300);
                queryString = string.Format("Select Distinct GroupSeqID, CfGroupID From PfamNonRedundantCfGroups " +
                     " Where GroupSeqID in ({0}) ORDER BY GroupSeqID, CfGroupID;", ParseHelper.FormatSqlListString(groupIds));
                DataTable subCfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subCfGroupTable, ref cfGroupTable);
            }
            return cfGroupTable;
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetSuperGroupCfGroups()
        {
            Initialize(true);
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperGroups;";
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = -1;
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupIdRow["SuperGroupSeqID"].ToString());
                int[] groupIds = GetGroupIDsInSuperGroup(superGroupId);

                AddCfGroupIDToSuperGroup(superGroupId, groupIds);

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, superCfGroupTable);

                superCfGroupTable.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        public void UpdateSuperGroupCfGroups(int[] superGroupIds)
        {
            Initialize(true);
            foreach (int superGroupId in superGroupIds)
            {
                superCfGroupTable.Clear();

                int[] groupIds = GetGroupIDsInSuperGroup(superGroupId);
                if (groupIds.Length > 0)
                {
                    AddCfGroupIDToSuperGroup(superGroupId, groupIds);

                    DeleteSuperCfGroup(superGroupId);

                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, superCfGroupTable);
                }
            }
            superCfGroupTable.Clear();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupIDsInSuperGroup(int superGroupId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0};",
                superGroupId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] groupIds = new int[groupIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupIds[count] = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                count++;
            }
            return groupIds;
        }

        private void DeleteSuperCfGroup(int superGroupId)
        {
            string deleteString = string.Format("Delete From PfamSuperCfGroups Where SuperGroupSeqID = {0};", superGroupId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion

        #region updated super groupIDs
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, Dictionary<int, string[]>> UpdateFamilySuperGroups(Dictionary<int, string[]> updateGroupsHash)
        {
            Initialize(true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Combine Family Groups";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update superfamily groups.");

            // get those super groups which have groups already deleted.
            int[] relatedSuperGroupIds = GetSuperGroupsWithDeletedGroups();

            FindSuperGroupsForUpdateGroups(updateGroupsHash);

            int[] dbSuperGroupIds = UpdateSuperGroupIds();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("The update super groups containing update groups");
            Dictionary<int, Dictionary<int, string[]>> updateSuperGroupsHash = GetUpdateSuperGroups(dbSuperGroupIds, updateGroupsHash);

#if DEBUG
            StreamWriter updateSuperGroupWriter = new StreamWriter("UpdateSuperGroups.txt");
            string dataLine = "";
            foreach (int keySuperGroupId in updateSuperGroupsHash.Keys)
            {
                updateSuperGroupWriter.WriteLine("#" + keySuperGroupId.ToString());
                foreach (int groupId in updateSuperGroupsHash[keySuperGroupId].Keys)
                {
                    dataLine = groupId.ToString() + ":";
                    string[] groupEntries = updateSuperGroupsHash[keySuperGroupId][groupId];
                    foreach (string entry in groupEntries)
                    {
                        dataLine += (entry + ",");
                    }
                    updateSuperGroupWriter.WriteLine(dataLine.TrimEnd(','));
                }
            }
            updateSuperGroupWriter.Flush();
#endif
            List<int> otherUpdateSuperGroupList = new List<int> ();
            int[] updateGroups = new int[updateGroupsHash.Count];
            int count = 0;
            foreach (int groupId in updateGroupsHash.Keys)
            {
                updateGroups[count] = groupId;
                count++;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clear super groups in the db");
            int[] changedSuperGroupIds = ClearSuperGroupDataInDb(updateGroups);
            foreach (int changedSuperGroupId in changedSuperGroupIds)
            {
                if (!updateSuperGroupsHash.ContainsKey(changedSuperGroupId))
                {
                    updateSuperGroupsHash.Add(changedSuperGroupId, null);
                    otherUpdateSuperGroupList.Add(changedSuperGroupId);
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete super groups data");
            DeleteSuperGroups(dbSuperGroupIds);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Inserting updated super groups data into db.");
            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, supergroupTable);
            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, superCfGroupTable);

            foreach (int relatedSuperGroup in relatedSuperGroupIds)
            {
                if (!updateSuperGroupsHash.ContainsKey(relatedSuperGroup))
                {
                    updateSuperGroupsHash.Add(relatedSuperGroup, null);
                    if (!otherUpdateSuperGroupList.Contains(relatedSuperGroup))
                    {
                        otherUpdateSuperGroupList.Add(relatedSuperGroup);
                    }
                }
            }
            // update cf groups info for those super groups not in updateSuperGroupsHash
            int[] otherUpdateSuperGroups = new int[otherUpdateSuperGroupList.Count];
            otherUpdateSuperGroupList.CopyTo (otherUpdateSuperGroups);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clear empty super groups and delete all related super group data");
            int[] emptySuperGroups = ClearEmptySuperGroupsData(otherUpdateSuperGroups);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update cf groups for the other super groups");
            UpdateSuperGroupCfGroups(otherUpdateSuperGroups);

            // write those super groups to be updated while excluding the those empty ones
#if DEBUG
            foreach (int otherSuperGroupId in otherUpdateSuperGroups)
            {
                if (Array.IndexOf(emptySuperGroups, otherSuperGroupId) < 0)
                {
                    updateSuperGroupWriter.WriteLine("#" + otherSuperGroupId.ToString());
                }
            }
            updateSuperGroupWriter.Close();
#endif

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clear super groups int interface clusters.");
            ClearEmptySuperGroupsData();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done");

            return updateSuperGroupsHash;
        }
        /// <summary>
        /// the super groups for the updated groups
        /// </summary>
        /// <param name="updateGroupHash"></param>
        /// <returns></returns>
        private void FindSuperGroupsForUpdateGroups(Dictionary<int, string[]> updateGroupHash)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updateGroupHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateGroupHash.Count;
            string familyString = "";
            int superGroupId = 1;
            List<string> parsedSuperGroupList = new List<string> ();
            string[] supergroupArchGroups = null;
            List<int> groupIdList = new List<int> (updateGroupHash.Keys);
            groupIdList.Sort();

            DataTable groupFamilyStringTable = GetGroupFamilyString(groupIdList.ToArray ());
            foreach (int groupId in groupIdList)
            {
                familyString = GetGroupFamilyString(groupId, groupFamilyStringTable);

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = familyString;

                if (familyString == "")
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("No group definition exist: " + groupId.ToString ());
                }

                supergroupArchGroups = GetSuperGroupArchStrings(familyString, parsedSuperGroupList);

                foreach (string supergroupArchString in supergroupArchGroups)
                {
                    string[] entityFamilyFields = supergroupArchString.Split(';');
                    int[] superSetGroups = GetSuperSetFamilyGroups(entityFamilyFields);
                    if (superSetGroups.Length > 0)
                    {
                        foreach (int supersetGroup in superSetGroups)
                        {
                            DataRow supergroupRow = supergroupTable.NewRow();
                            supergroupRow["SuperGroupSeqID"] = superGroupId;
                            supergroupRow["ChainRelPfamArch"] = supergroupArchString;
                            supergroupRow["GroupSeqID"] = supersetGroup;
                            supergroupTable.Rows.Add(supergroupRow);
                        }
                        AddCfGroupIDToSuperGroup(superGroupId, superSetGroups);

                        superGroupId++;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string GetGroupFamilyString(int groupId, DataTable groupFamilyStringTable)
        {
            DataRow[] groupFamilyStringRows = groupFamilyStringTable.Select(string.Format("GroupSeqID = '{0}'", groupId));
            if (groupFamilyStringRows.Length > 0)
            {
                return groupFamilyStringRows[0]["EntryPfamArch"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private DataTable GetGroupFamilyString(int[] groupIds)
        {
            DataTable familyStringTable = null;
            string queryString = "";
            for (int i = 0; i < groupIds.Length; i += 300)
            {
                int[] subGroupIds = ParseHelper.GetSubArray(groupIds, i, 300);
                queryString = string.Format("Select GroupSeqID, EntryPfamArch From PfamGroups Where GroupSeqID IN ({0});", ParseHelper.FormatSqlListString(subGroupIds));
                DataTable subFamilyStringTable = ProtCidSettings.protcidQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subFamilyStringTable, ref familyStringTable);
            }
            return familyStringTable;
        }

        #region update super group ids
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] UpdateSuperGroupIds ()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Super Group IDs");

            string superGroupString = "";
            int tempSuperGroupId = 0;
            int newDbSuperGroupId = -1;
            Dictionary<int, List<DataRow>> origDataRowHash = new Dictionary<int,List<DataRow>> (); // index the data rows by the temporary super group id
            Dictionary<int, string> superGroupStringHash = new Dictionary<int,string> ();
            foreach (DataRow superGroupRow in supergroupTable.Rows)
            {
                tempSuperGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                if (origDataRowHash.ContainsKey(tempSuperGroupId))
                {
                    origDataRowHash[tempSuperGroupId].Add(superGroupRow);
                }
                else
                {
                    List<DataRow> dataRowList = new List<DataRow> ();
                    dataRowList.Add(superGroupRow);
                    origDataRowHash.Add(tempSuperGroupId, dataRowList);

                    superGroupString = superGroupRow["ChainRelPfamArch"].ToString();
                    superGroupStringHash.Add(tempSuperGroupId, superGroupString);
                }
            }
            int[] dbSuperGroupIds = new int[origDataRowHash.Count];
            ProtCidSettings.progressInfo.totalOperationNum = origDataRowHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = origDataRowHash.Count;

            List<int> updateSuperGroupIdList = new List<int> (origDataRowHash.Keys);
            updateSuperGroupIdList.Sort();
            int[] updateSuperGroupIds = updateSuperGroupIdList.ToArray(); 
            // index the cf group rows by the temporary super group ids
            Dictionary<int, DataRow[]> superCfGroupHash = GetUpdateCfGroupRowHash(updateSuperGroupIds);
            int count = 0;
            foreach (int updateSuperGroupId in updateSuperGroupIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = updateSuperGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                int dbSuperGroupId = GetSuperGroupId((string)superGroupStringHash[updateSuperGroupId], ref newDbSuperGroupId);
                DataRow[] updateDataRows = origDataRowHash[updateSuperGroupId].ToArray (); 
                // update the data rows with temporary supergroup id
                UpdateSuperGroupTables(dbSuperGroupId, updateDataRows, superCfGroupHash[updateSuperGroupId]);
                dbSuperGroupIds[count] = dbSuperGroupId;
                count++;
            }
            supergroupTable.AcceptChanges();
            superCfGroupTable.AcceptChanges();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            return dbSuperGroupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        /// <returns></returns>
        private Dictionary<int, DataRow[]> GetUpdateCfGroupRowHash(int[] superGroupIds)
        {
            Dictionary<int, DataRow[]> superCfGroupHash = new Dictionary<int,DataRow[]> ();
            foreach (int superGroupId in superGroupIds)
            {
                DataRow[] cfGroupRows = superCfGroupTable.Select(string.Format ("SuperGroupSeqID = '{0}'", superGroupId));
                superCfGroupHash.Add(superGroupId, cfGroupRows);
            }
            return superCfGroupHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupString"></param>
        /// <returns></returns>
        private int GetSuperGroupId(string superGroupString, ref int newSuperGroupId)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups " +
                " Where ChainRelPfamArch = '{0}';", superGroupString);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (superGroupIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            if (newSuperGroupId == -1)
            {
                newSuperGroupId = GetMaxSuperGroupSeqID();
            }
            int superGroupId = newSuperGroupId + 1;
            newSuperGroupId++;

            return superGroupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="origSuperGroupString"></param>
        /// <param name="dbSuperGroupId"></param>
        private void UpdateSuperGroupTables(int dbSuperGroupId, DataRow[] groupRows, DataRow[] cfGroupRows)
        {
            foreach (DataRow groupRow in groupRows)
            {
                groupRow["SuperGroupSeqID"] = dbSuperGroupId;
            }

            foreach (DataRow cfGroupRow in cfGroupRows)
            {
                cfGroupRow["SuperGroupSeqID"] = dbSuperGroupId;
            }
        }
        #endregion

        #region clear database
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        private int[] ClearSuperGroupDataInDb(int[] updateGroups)
        {
            List<int> changedSuperGroupIdList = new List<int> ();
            foreach (int groupId in updateGroups)
            {
                int[] origSuperGroupIds = GetSuperGroupIdsInDb(groupId);
                int[] newSuperGroupIds = GetSuperGroupIdsInTable(groupId);
                int[] changeSuperGroupIds = GetOrigSuperGroupIdsNotNew(origSuperGroupIds, newSuperGroupIds);
                if (changeSuperGroupIds.Length > 0)
                {
                    foreach (int changeSuperGroupId in changeSuperGroupIds)
                    {
                        ChangeSuperGroup(changeSuperGroupId, groupId);
                        if (!changedSuperGroupIdList.Contains(changeSuperGroupId))
                        {
                            changedSuperGroupIdList.Add(changeSuperGroupId);
                        }
                    }
                }
            }
            return changedSuperGroupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int[] GetSuperGroupIdsInDb (int groupId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID From PfamSuperGroups " + 
                " Where GroupSeqID = {0};", groupId);
            DataTable superGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] superGroupIds = new int[superGroupTable.Rows.Count];
            int count = 0;
            int superGroupId = 0;
            foreach (DataRow superGroupRow in superGroupTable.Rows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString ());
                superGroupIds[count] = superGroupId;
                count++;
            }
            return superGroupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int[] GetSuperGroupIdsInTable(int groupId)
        {
            DataRow[] superGroupRows = supergroupTable.Select(string.Format ("GroupSeqID = '{0}'", groupId));
            int[] tableSuperGroupIds = new int[superGroupRows.Length];
            int count = 0;
            int superGroupId = 0;
            foreach (DataRow superGroupRow in superGroupRows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString ());
                tableSuperGroupIds[count] = superGroupId;
                count++;
            }
            return tableSuperGroupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="origSuperGroupIds"></param>
        /// <param name="newSuperGroupIds"></param>
        /// <returns></returns>
        private int[] GetOrigSuperGroupIdsNotNew(int[] origSuperGroupIds, int[] newSuperGroupIds)
        {
            List<int> obsoleteSuperGroupList = new List<int> ();
            foreach (int origSuperGroupId in origSuperGroupIds)
            {
                if (Array.IndexOf(newSuperGroupIds, origSuperGroupId) > -1)
                {
                    continue;
                }
                obsoleteSuperGroupList.Add(origSuperGroupId);
            }
            return obsoleteSuperGroupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="groupId"></param>
        private void ChangeSuperGroup(int superGroupId, int groupId)
        {
            string deleteString = string.Format("Delete From PfamSuperGroups " +
                " Where SuperGroupSeqId = {0} AND GroupSeqID = {1};", superGroupId, groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            if (IsSuperGroupEmpty(superGroupId))
            {
                DeleteSuperGroup (superGroupId); ;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private bool IsSuperGroupEmpty(int superGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID From PfamSuperGroups " + 
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (groupIdTable.Rows.Count > 0)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        private void DeleteSuperGroup(int superGroupId)
        {
            string deleteString = string.Format("Delete From PfamSuperGroups " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString); 

            deleteString = string.Format("Delete From PfamSuperCfGroups " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        private void DeleteSuperGroups(int[] superGroupIds)
        {
            string deleteString = "";
            for (int i = 0; i < superGroupIds.Length; i += 300)
            {
                int[] subSuperGroupIds = ParseHelper.GetSubArray(superGroupIds, i, 300);
                deleteString = string.Format("Delete From PfamSuperGroups " +
                   " Where SuperGroupSeqID IN ({0});", ParseHelper.FormatSqlListString (subSuperGroupIds));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

                deleteString = string.Format("Delete From PfamSuperCfGroups " +
                    " Where SuperGroupSeqID IN ({0});", ParseHelper.FormatSqlListString(subSuperGroupIds));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
       /*     foreach (int superGroupId in superGroupIds)
            {
                DeleteSuperGroup(superGroupId);
            }*/
        }
       
        #endregion

        #region updated super groups
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbUpdateSuperGroupIds"></param>
        /// <param name="updateGroupsHash"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<int,string[]>> GetUpdateSuperGroups(int[] dbUpdateSuperGroupIds, Dictionary<int, string[]> updateGroupsHash)
        {
            Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash = new Dictionary<int,Dictionary<int,string[]>> ();
            foreach (int superGroupId in dbUpdateSuperGroupIds)
            {
                int[] groupIds = GetGroupIdInSuperGroup(superGroupId);
                Dictionary<int, string[]> groupUpdateEntryHash = new Dictionary<int,string[]> ();
                foreach (int groupId in groupIds)
                {
                    if (updateGroupsHash.ContainsKey(groupId))
                    {
                        groupUpdateEntryHash.Add(groupId, (string[])updateGroupsHash[groupId]);
                    }
                }
                updateSuperGroupHash.Add(superGroupId, groupUpdateEntryHash);
            }
            return updateSuperGroupHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupIdInSuperGroup(int superGroupId)
        {
            DataRow[] groupIdRows = supergroupTable.Select(string.Format ("SuperGroupSeqID = '{0}'", superGroupId));
            int[] groupIds = new int[groupIdRows.Length];
            int count = 0;
            foreach (DataRow groupIdRow in groupIdRows)
            {
                groupIds[count] = Convert.ToInt32(groupIdRow["GrouPSeqID"].ToString ());
                count++;
            }
            return groupIds;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int GetMaxSuperGroupSeqID()
        {
            string queryString = "Select Max(SuperGroupSeqID) As MaxGroupId From PfamSuperGroups;";
            DataTable maxGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int maxGroupId = 0;
            if (maxGroupIdTable.Rows.Count > 0)
            {
                maxGroupId = Convert.ToInt32(maxGroupIdTable.Rows[0]["MaxGroupID"].ToString());
            }
            return maxGroupId;
        }
        #endregion

        #region super groups with deleted groups
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetSuperGroupsWithDeletedGroups()
        {
            string changedSuperGroupFile = "ChangedSuperGroups.txt";
            List<int> relatedSuperGroupList = new List<int> ();
            if (File.Exists(changedSuperGroupFile))
            {
                string line = "";
                int superGroupId = 0;
                StreamReader dataReader = new StreamReader(changedSuperGroupFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    superGroupId = Convert.ToInt32(line);
                    if (!relatedSuperGroupList.Contains(superGroupId))
                    {
                        relatedSuperGroupList.Add(superGroupId);
                    }
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter changedSuperGroupsWriter = new StreamWriter(changedSuperGroupFile, true);
                int[] deletedGroups = ReadDeletedGroups();
                DataTable superGroupIdsTabel = GetSuperGroupIdForGroup(deletedGroups);
                foreach (int deletedGroup in deletedGroups)
                {
                    int[] superGroups = GetSuperGroupIdForGroup(deletedGroup, superGroupIdsTabel);
                    foreach (int superGroup in superGroups)
                    {
                        if (!relatedSuperGroupList.Contains(superGroup))
                        {
                            relatedSuperGroupList.Add(superGroup);
                            changedSuperGroupsWriter.WriteLine(superGroup);
                            changedSuperGroupsWriter.Flush();
                        }
                    }
                    //delete the group info from the super groups
            //        DeleteRelatedSuperGroups(deletedGroup);
                }
                changedSuperGroupsWriter.Close();
                DeleteRelatedSuperGroups(deletedGroups);
                // keep a copy in case
                File.Copy("ChangedSuperGroups.txt", "ChangedSuperGroups_copy.txt");
            }

            int[] relatedSuperGroups = new int[relatedSuperGroupList.Count];
            relatedSuperGroupList.CopyTo(relatedSuperGroups);
            return relatedSuperGroups;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        private void DeleteRelatedSuperGroups(int groupId)
        {
            string deleteString = string.Format("Delete From PfamSuperGroups Where GroupSeqID = {0};", groupId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From PfamSuperCfGroups Where GroupSeqID = {0};", groupId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        private void DeleteRelatedSuperGroups(int[] groupIds)
        {
            string deleteString = "";
            for (int i = 0; i < groupIds.Length; i += 300)
            {
                int[] subGroupIds = ParseHelper.GetSubArray(groupIds, i, 300);
                deleteString = string.Format("Delete From PfamSuperGroups Where GroupSeqID IN ({0});", ParseHelper.FormatSqlListString(subGroupIds));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

                deleteString = string.Format("Delete From PfamSuperCfGroups Where GroupSeqID IN ({0});", ParseHelper.FormatSqlListString(subGroupIds));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] ReadDeletedGroups()
        {
            StreamReader dataReader = new StreamReader("DeletedGroups.txt");
            List<int> groupIdList = new List<int> ();
            int groupId = 0;
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                groupId = Convert.ToInt32(line);
                groupIdList.Add(groupId);
            }
            dataReader.Close();
            return groupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int[] GetSuperGroupIdForGroup(int groupId, DataTable superGroupIdTable)
        {
            DataRow[] superGroupRows = superGroupIdTable.Select(string.Format ("GroupSeqID = '{0}'", groupId));
            List<int> superGroupList = new List<int> ();
            int superGroupId = 0;
            foreach (DataRow superGroupRow in superGroupRows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString ());
                superGroupList.Add(superGroupId);
            }
            return superGroupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private DataTable GetSuperGroupIdForGroup(int[] groupIds)
        {
            string queryString = "";
            DataTable superGroupIdTable = null;
            for (int i = 0; i < groupIds.Length; i += 300 )
            {
                int[] subGroupIds = ParseHelper.GetSubArray(groupIds, i, 300);
                queryString = string.Format("Select SuperGroupSeqID, GroupSeqID From PfamSuperGroups Where GroupSeqID In ({0});", ParseHelper.FormatSqlListString (subGroupIds));
                DataTable subSuperGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subSuperGroupIdTable, ref superGroupIdTable);
            }
            return superGroupIdTable;
        }
        #endregion

        #region update supergroupseqid
          string[] tableNames = {"PfamSuperGroups", "PfamSuperCfGroups", "PfamSuperInterfaceClusters" };
         public void UpdateSuperGroupSeqIdInOrder()
         {
             string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups Order By ChainRelPfamArch;";
             DataTable superGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
             int newSuperGroupSeqId = 0;
             int superGroupSeqId = 0;
             foreach (DataRow superGroupRow in superGroupTable.Rows)
             {
                 newSuperGroupSeqId++;
                 superGroupSeqId = Convert.ToInt32 (superGroupRow["SuperGroupSeqID"].ToString ());
                 
                 UpdateSuperGroupRow(superGroupSeqId, newSuperGroupSeqId);
             }
         }

         private void UpdateSuperGroupRow(int orgSuperGroupId, int newSuperGroupId)
         {
             string updateString = "";
             foreach (string tableName in tableNames)
             {
                 updateString = string.Format("Update {0} Set NewSuperGroupSeqID = {1} " + 
                     " Where SuperGroupSeqID = {2};", tableName, newSuperGroupId, orgSuperGroupId);
                 ProtCidSettings.protcidQuery.Query( updateString);
             }
         }
        #endregion

        #region add the super group for the user input 
        /// <summary>
        /// chain-chain relation groups based on one PFAM not by the Chain PFAM architecture
        /// </summary>
        /// <param name="userSuperUpdateGroupHash"></param>
        /// <param name="userGroupName"></param>
         public void AddChainRelGroupsBasedOnPfam (Dictionary<int, Dictionary<int, string[]>> chainRelGroupEntriesHash, Dictionary<int, string> chainRelGroupNameHash)
         {
             CreateMemoryTableStructure();

             ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
           
             ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add the user defined group.");

             List<int> chainRelGroupList = new List<int> (chainRelGroupEntriesHash.Keys);
             chainRelGroupList.Sort();
             int[] subsetGroups = null;
             foreach (int chainRelGroupId in chainRelGroupList)
             {
                 Dictionary<int, string[]> subGroupEntryHash = chainRelGroupEntriesHash[chainRelGroupId];
                 List<int> subsetList = new List<int> (subGroupEntryHash.Keys);
                 subsetList.Sort ();
                 foreach (int subsetGroup in subsetList)
                 {
                     DataRow supergroupRow = supergroupTable.NewRow();
                     supergroupRow["SuperGroupSeqID"] = chainRelGroupId;
                     supergroupRow["ChainRelPfamArch"] = (string)chainRelGroupNameHash[chainRelGroupId];
                     supergroupRow["GroupSeqID"] = subsetGroup;
                     supergroupTable.Rows.Add(supergroupRow);
                 }
                 subsetGroups = new int[subsetList.Count];
                 subsetList.CopyTo(subsetGroups);
                 AddCfGroupIDToSuperGroup(chainRelGroupId, subsetGroups);

                 // delete the old data if there is any
                 DeleteSuperGroup(chainRelGroupId);

                 dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, supergroupTable);
                 dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, superCfGroupTable);
                 supergroupTable.Clear();
                 superCfGroupTable.Clear();
             }
             ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
         }
        #endregion

        #region clear empty super groups 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatedSuperGroupIds"></param>
        /// <returns></returns>
         public int[] ClearEmptySuperGroupsData(int[] updatedSuperGroupIds)
         {
             List<int> emptySuperGroupList = new List<int> ();
             foreach (int superGroupId in updatedSuperGroupIds)
             {
                 if (IsSuperGroupEmpty(superGroupId))
                 {
                     emptySuperGroupList.Add(superGroupId);
                     DeleteSuperCfGroup (superGroupId);
                     DeleteClusterInfo(superGroupId);
                 }
             }
             return emptySuperGroupList.ToArray ();
         }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
         public void ClearEmptySuperGroupsData()
         {
             StreamWriter emptySuperGroupIdWriter = new StreamWriter("HomoSeq\\EmptySuperGroups.txt");
             string queryString = "Select Distinct SuperGroupSeqID From PfamSuperInterfaceClusters;";
             DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
             int superGroupId = 0;
             foreach (DataRow superGroupIdRow in superGroupIdTable.Rows)
             {
                 superGroupId = Convert.ToInt32(superGroupIdRow["SuperGroupSeqID"].ToString ());
                 if (IsSuperGroupEmpty(superGroupId))
                 {
                     DeleteClusterInfo(superGroupId);
                     emptySuperGroupIdWriter.WriteLine(superGroupId.ToString ());
                 }
             }
             emptySuperGroupIdWriter.Close();
         }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
         private void DeleteClusterInfo(int superGroupId)
         {
             string deleteString = string.Format("Delete From PfamSuperInterfaceClusters " + 
                 " Where SuperGroupSeqID = {0};", superGroupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

             deleteString = string.Format("Delete From PfamSuperClusterSumInfo " + 
                 " Where SuperGroupSeqID = {0};", superGroupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

             deleteString = string.Format("Delete From PfamSuperClusterEntryInterfaces " + 
                 " Where SuperGrouPSeqID = {0};", superGroupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
         }

         public void ClearNotExistGroups()
         {
             string queryString = "Select Distinct GroupSeqID From PfamSuperGroups;";
             DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
             List<int> notExistGroupList = new List<int> ();
             int groupId = 0;
             foreach (DataRow groupRow in groupIdTable.Rows)
             {
                 groupId = Convert.ToInt32(groupRow["GroupSeqID"].ToString ());
                 if (!IsGroupExist(groupId))
                 {
                     notExistGroupList.Add(groupId);
                 }
             }
             foreach (int notExistGroupId in notExistGroupList)
             {
                 DeleteNotExistGroupId(notExistGroupId);
             }
         }

         private bool IsGroupExist(int groupId)
         {
             string queryString = string.Format("Select * From PfamGroups Where GroupSeqID = {0};", groupId);
             DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
             if (groupTable.Rows.Count > 0)
             {
                 return true;
             }
             return false;
         }

         private void DeleteNotExistGroupId(int groupId)
         {
             string deleteString = string.Format("Delete From  PfamSuperGroups Where GroupSeqID = {0};", groupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

             deleteString = string.Format("Delete From PfamSuperCfGroups Where GroupSeqID = {0};", groupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

             deleteString = string.Format("Delete From PfamSuperInterfaceClusters Where GroupSeqID = {0};", groupId);
             dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
         }
        #endregion
    }
}
