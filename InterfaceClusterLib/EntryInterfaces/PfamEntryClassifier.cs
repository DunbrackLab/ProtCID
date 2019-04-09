using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using InterfaceClusterLib.Alignments;
using PfamLib.PfamArch;
using PfamLib;
using PfamLib.Settings;

namespace InterfaceClusterLib.EntryInterfaces
{
	/// <summary>
	/// Summary description for PfamEntryClassifier.
	/// </summary>
    public class PfamEntryClassifier : HomoEntryClassifier
	{ 
		#region member variables		
        private StreamWriter repHomoEntryWriter = new StreamWriter("RepHomoEntries.txt");
        private PfamArchitecture pfamArch = new PfamArchitecture();
		#endregion

        #region classifiy pdb entries based on pfam family components
        /// <summary>
        /// 
        /// </summary>
        public void ClassifyPdbPfamGroups()
        {
            InitializeTables(false);

            Dictionary<string, List<string>> entryPfamArchEntryHash = GetPfamArchEntryHash();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Classify PDB entries.");

            ProtCidSettings.progressInfo.totalOperationNum = entryPfamArchEntryHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryPfamArchEntryHash.Count;

            List<string> entryPfamArchList = new List<string> (entryPfamArchEntryHash.Keys);
            entryPfamArchList.Sort ();
            foreach (string entryPfamArch in entryPfamArchList)
            {
                ProtCidSettings.progressInfo.currentFileName = entryPfamArch;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                string[] groupEntries = entryPfamArchEntryHash[entryPfamArch].ToArray(); 
                AddEntryPfamArchGroups(entryPfamArch, groupEntries);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
       }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, string[]> UpdatePdbPfamGroups()
        {
            InitializeTables(true);
            string queryString = "Select Distinct PdbID From PfamEntityPfamArch Where SupPfamArch <> SupPfamArchE3;";
            DataTable updateEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] updateEntries = new string[updateEntryTable.Rows.Count];
            int count = 0;
            string pdbId = "";
            foreach (DataRow entryRow in updateEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                updateEntries[count] = pdbId;
                count++;
            }

            Dictionary<int, string[]> updateGroupEntryHash = UpdatePdbPfamGroups(updateEntries);
            return updateGroupEntryHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public Dictionary<int, string[]> UpdatePdbPfamGroups (string[] updateEntries)
        {
            InitializeTables(true);

            Dictionary<int, string[]>[] updateGroupEntryHashes = ClassifyUpdateEntriesIntoGroups(updateEntries);
            // for each update group, only the list of update entries
            Dictionary<int, string[]> updateGroupUpdateEntryHash = updateGroupEntryHashes[0];
//            Hashtable updateGroupUpdateEntryHash = null;
            // for each update group, the whole set of entries
            Dictionary<int, string[]> updateGroupEntryHash = updateGroupEntryHashes[1]; 
 //           Hashtable updateGroupEntryHash = ReadUpdateGroupEntriesHash("UpdateGroupEntries.txt");
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add data into data.");
            ProtCidSettings.progressInfo.totalOperationNum = updateGroupEntryHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateGroupEntryHash.Count;

            List<int> updateGroupList = new List<int> (updateGroupEntryHash.Keys); 
            updateGroupList.Sort();
            foreach (int updateGroupId in updateGroupList)
            {
                ProtCidSettings.progressInfo.currentFileName = updateGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (updateGroupId == 2)
                {
                    continue;
                }
            
                groupSeqNum = updateGroupId;
                string[] groupEntries = (string[])updateGroupEntryHash[updateGroupId];
                groupEntries = RemoveDuplicateEntries(groupEntries);  // should not have duplicate, this is just temporary

                UpdateEntryPfamArchGroupTables (updateGroupId, groupEntries);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            return updateGroupUpdateEntryHash;
        }

        /// <summary>
        /// temporarily remove duplicate, just for debug, remove later
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private string[] RemoveDuplicateEntries (string[] entries)
        {
            List<string> uniqueEntryList = new List<string>();
            Array.Sort(entries);
            for (int i = 0; i < entries.Length; i++)
            {
                if (! uniqueEntryList.Contains (entries[i]))
                {
                    uniqueEntryList.Add(entries[i]);
                }
            }
            return uniqueEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, string[]>[] ClassifyUpdateEntriesIntoGroups(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Pfam Groups";
            // delete the old data in the 3 classification tables
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete the old data in the 3 classification tables.");
           int[] groupIdsWithDeletion = DeleteObsoleteGroupInfo(updateEntries);
    //         int[] groupIdsWithDeletion = ReadGroupsWithDeletion();
            // get the left entries for each groups with deleted entries, and delete those empty groups
            int[] deletedGroupIds = null; // those numbers to be reused
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get the left entries in the groups with updated entries, ");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("and delete the empty groups.");
            Dictionary<int, string[]> groupLeftEntryHash = GetGroupLeftEntries(groupIdsWithDeletion, out deletedGroupIds);
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get EntryPfamArch for the entries to be updated.");
            Dictionary<string, List<string>> entryPfamArchEntryHash = GetPfamArchEntryHash(updateEntries);
            int groupSeqId = -1;
            Dictionary<int, string[]>  updateGroupEntryHash = new Dictionary<int,string[]> ();
            Dictionary<int, string[]> updateGroupUpdateEntryHash = new Dictionary<int, string[]>();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Classify the entries into groups.");
            List<string> entryPfamArchList = new List<string> (entryPfamArchEntryHash.Keys);
            entryPfamArchList.Sort();
            List<int> deletedGroupIdList = new List<int> (deletedGroupIds);
            deletedGroupIdList.Sort();
            bool groupExist = true;
            foreach (string entryPfamArch in entryPfamArchList)
            {                
                groupSeqId = GetGroupSeqId(entryPfamArch, deletedGroupIdList, out groupExist);

                List<string> groupUpdateEntryList = entryPfamArchEntryHash[entryPfamArch];
                updateGroupUpdateEntryHash.Add(groupSeqId, groupUpdateEntryList.ToArray ());

                List<string> groupEntryList = new List<string> (groupUpdateEntryList);
                if (groupLeftEntryHash.ContainsKey(groupSeqId))
                {
                    string[] groupLeftEntries = groupLeftEntryHash[groupSeqId];
                    groupEntryList.AddRange(groupLeftEntries);
                    groupLeftEntryHash.Remove(groupSeqId);
                }
                else if (groupExist) // new to the group
                {
                    string[] existGroupEntries = GetGroupEntries(groupSeqId);
                    groupEntryList.AddRange(existGroupEntries);
                }
                updateGroupEntryHash.Add(groupSeqId, groupEntryList.ToArray ());
            }
            
            foreach (int groupWithDeletion in groupLeftEntryHash.Keys)
            {
                string[] groupEntries = (string[])groupLeftEntryHash[groupWithDeletion];
                updateGroupEntryHash.Add(groupWithDeletion, groupEntries);
                // since they are also updated by deleted entries, they needed to be udpated too
                updateGroupUpdateEntryHash.Add(groupWithDeletion, null);
            }

            WriteUpdateGroupsInfo(updateGroupUpdateEntryHash, "UpdateGroups.txt");
            WriteUpdateGroupsInfo(updateGroupEntryHash, "UpdateGroupEntries.txt");

            // write the deleted group ids to file
            StreamWriter deletedGroupWriter = new StreamWriter("DeletedGroups.txt");
            foreach (int deletedGroupId in deletedGroupIds)
            {
                deletedGroupWriter.WriteLine(deletedGroupId);
                deletedGroupWriter.Flush();
                DeletePfamGroupInfo(deletedGroupId);
            }
            deletedGroupWriter.Close();


            Dictionary<int, string[]>[] updateGroupEntryHashes = new Dictionary<int,string[]>[2];
            updateGroupEntryHashes[0] = updateGroupUpdateEntryHash;
            updateGroupEntryHashes[1] = updateGroupEntryHash;

            return updateGroupEntryHashes;
        }

        #region for debug
        /// <summary>
        /// for debug
        /// </summary>
        /// <returns></returns>
        private int[] ReadGroupsWithDeletion(string fileName)
        {
            StreamReader groupsReader = new StreamReader(fileName);
            List<int> groupList = new List<int> ();
            string line = "";
            while ((line = groupsReader.ReadLine()) != null)
            {
                groupList.Add(Convert.ToInt32 (line));
            }
            groupsReader.Close();
            return groupList.ToArray ();
        }

        /// <summary>
        /// for debug
        /// </summary>
        /// <param name="updateGroupHash"></param>
        private void WriteUpdateGroupsInfo(Dictionary<int, string[]> updateGroupHash, string fileName)
        {
            StreamWriter updateGroupWriter = new StreamWriter(fileName);
            string line = "";
            foreach (int updateGroup in updateGroupHash.Keys)
            {
                line = updateGroup.ToString();
                string[] groupUpdateEntries = (string[])updateGroupHash[updateGroup];
                if (groupUpdateEntries != null)
                {
                    foreach (string updateEntry in groupUpdateEntries)
                    {
                        line += " ";
                        line += updateEntry;
                    }
                }
                updateGroupWriter.WriteLine(line);
            }
            updateGroupWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="updateGroupEntryHash"></param>
        /// <returns></returns>
        private string[] GetGroupEntries(int groupId, Dictionary<int, string[]> updateGroupEntryHash)
        {
            if (updateGroupEntryHash.ContainsKey(groupId))
            {
                return updateGroupEntryHash[groupId];
            }
            string[] groupEntries = GetGroupEntries(groupId);
            return groupEntries;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPfamArch"></param>
        /// <returns></returns>
        private int GetGroupSeqId(string entryPfamArch)
        {
            string queryString = string.Format("Select GroupSeqID From {0}Groups Where EntryPfamArch = '{1}';",
                ProtCidSettings.dataType, entryPfamArch);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (groupIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(groupIdTable.Rows[0]["GroupSeqID"].ToString ());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, string[]> ReadUpdateGroupEntriesHash(string fileName)
        {
            Dictionary<int, string[]> updateGroupEntryHash = new Dictionary<int,string[]> ();
            StreamReader dataReader = new StreamReader(fileName);
            string line = "";
            int groupId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                groupId = Convert.ToInt32(fields[0]);
                string[] groupEntries = new string[fields.Length - 1];
                Array.Copy(fields, 1, groupEntries, 0, groupEntries.Length);
                updateGroupEntryHash.Add(groupId, groupEntries);
            }
            dataReader.Close();
            return updateGroupEntryHash;
        }
        #endregion
        #endregion

        #region add update info to db
        /// <summary>
        /// classify pfam group into subgroups based on
        /// number of entities and domain families
        /// </summary>
        /// <param name="entryList"></param>
        private void UpdateEntryPfamArchGroupTables(int groupSeqId, string[] groupEntries)
        {
            AddEntryDataToTable(groupEntries, groupSeqId);

            UpdateHomoGroupRepAlignTables ();

            DeletePfamHomoGroupInfo(groupSeqId);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo]);

            HomoGroupTables.ClearTables();

            entryAlignment.entryEntityFamilyTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void InsertUpdateDataIntoDb(int groupSeqId)
        {
            UpdateAlignmentData(groupSeqId);

            DeletePfamHomoGroupInfo (groupSeqId);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo]);

            HomoGroupTables.ClearTables();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void UpdateAlignmentData(int groupSeqId)
        {
            DataTable[] existAlignTables = GetExistAlignmentData(groupSeqId);

            UpdateGroupAlignments(HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign],
                existAlignTables, HomoGroupTables.HomoGroupEntryAlign);
            UpdateGroupAlignments(HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign],
                existAlignTables, HomoGroupTables.HomoRepEntryAlign);

            DeletePfamGroupAlignInfo(groupSeqId);

            InsertAlignmentData();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignTable"></param>
        /// <param name="existAlignTables"></param>
        private void UpdateGroupAlignments(DataTable alignTable, DataTable[] existAlignTables, int tableNum)
        {
            string pdbId1 = "";
            string pdbId2 = "";
            DataTable updatedAlignTable = alignTable.Clone ();
            foreach (DataRow alignRow in alignTable.Rows)
            {
                pdbId1 = alignRow["PdbID1"].ToString();
                pdbId2 = alignRow["PdbID2"].ToString();
                DataRow[] existRows = GetExistAlignRow(pdbId1, pdbId2, existAlignTables);
                if (existRows != null)
                {
                    foreach (DataRow existRow in existRows)
                    {
                        DataRow newRow = updatedAlignTable.NewRow();
                        newRow.ItemArray = existRow.ItemArray;
                        updatedAlignTable.Rows.Add(newRow);
                    }
                }
                else
                {
                    DataRow newRow = updatedAlignTable.NewRow();
                    newRow.ItemArray = alignRow.ItemArray;
                    updatedAlignTable.Rows.Add(newRow);
                }
            }
            HomoGroupTables.homoGroupTables[tableNum] = updatedAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="entityId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="entityId2"></param>
        /// <param name="existTables"></param>
        /// <returns></returns>
        private DataRow GetExistAlignRow(string pdbId1, int entityId1, string pdbId2, int entityId2,
            DataTable[] existTables)
        {
            string selectString = "";
            foreach (DataTable existTable in existTables)
            {
                selectString = string.Format("PdbID1 = '{0}' AND EntityID1 = '{1}' AND PdbID2 = '{2}' AND EntityID2 = '{3}'", 
                    pdbId1, entityId1, pdbId2, entityId2);
                DataRow[] alignRows = existTable.Select(selectString);
                if (alignRows.Length > 0)
                {
                    return alignRows[0];
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="existTables"></param>
        /// <returns></returns>
        private DataRow[] GetExistAlignRow(string pdbId1, string pdbId2, DataTable[] existTables)
        {
            string selectString = "";
            foreach (DataTable existTable in existTables)
            {
                selectString = string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'", pdbId1, pdbId2);
                DataRow[] alignRows = existTable.Select(selectString);
                if (alignRows.Length > 0)
                {
                    return alignRows;
                }
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private DataTable[] GetExistAlignmentData(int groupSeqId)
        {
            string queryString = string.Format("Select * From {0}HomoRepEntryAlign Where GroupSeqID = {1};", 
                ProtCidSettings.dataType, groupSeqId);
            DataTable repHomoAlignTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From {0}HomoGroupEntryAlign Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            DataTable repEntryAlignTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable[] groupAlignTables = new DataTable[2];
            groupAlignTables[0] = repHomoAlignTable;
            groupAlignTables[1] = repEntryAlignTable;
            return groupAlignTables;
        }

        #endregion

        #region group seq Id
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPfamArch"></param>
        /// <returns></returns>
        public int GetGroupSeqId(string entryPfamArch, List<int> deletedGroupIdList, out bool groupExist)
        {
            groupExist = false;
            string queryString = string.Format("Select GroupSeqID From {0}Groups Where EntryPfamArch = '{1}';",
                ProtCidSettings.dataType, entryPfamArch);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupSeqId = 0;
            if (groupSeqIdTable.Rows.Count > 0)
            {
                groupSeqId = Convert.ToInt32(groupSeqIdTable.Rows[0]["GroupSeqID"].ToString());
                groupExist = true;
            }
            else
            {
                if (deletedGroupIdList.Count > 0)
                {
                    groupSeqId = (int)deletedGroupIdList[0];
                    deletedGroupIdList.RemoveAt(0);
                }
                else
                {
                    groupSeqId = GetMaxGroupId() + 1;
                }
                InsertGroupEntryPfamArchIntoDb(groupSeqId, entryPfamArch);
            }
            return groupSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPfamArch"></param>
        /// <returns></returns>
        public int GetGroupSeqID (string entryPfamArch)
        {
            string queryString = string.Format("Select GroupSeqID From {0}Groups Where EntryPfamArch = '{1}';",
               ProtCidSettings.dataType, entryPfamArch);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupSeqId = -1;
            if (groupSeqIdTable.Rows.Count > 0)
            {
                groupSeqId = Convert.ToInt32(groupSeqIdTable.Rows[0]["GroupSeqID"].ToString());
            }
            return groupSeqId;
        }

        /// <summary>
        /// only for there are data exist in the table
        /// </summary>
        /// <returns></returns>
        private int GetMaxGroupSeqId()
        {
            string queryString = "Select Max(GroupSeqID) As MaxGroupSeqId From {0}Groups;";
            DataTable maxGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int maxGroupSeqId = Convert.ToInt32(maxGroupIdTable.Rows[0]["MaxGroupSeqId"].ToString());
            return maxGroupSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="entryPfamArch"></param>
        private void InsertGroupEntryPfamArchIntoDb(int groupSeqId, string entryPfamArch)
        {
            string insertString = string.Format("Insert Into {0}Groups (GroupSeqID, EntryPfamArch) " +
                "Values ({1}, '{2}');", ProtCidSettings.dataType, groupSeqId, entryPfamArch);
            ProtCidSettings.protcidQuery.Query( insertString);
        }
        #endregion

        #region delete obsolete data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private int[] DeleteObsoleteGroupInfo(string[] updateEntries)
        {
            string groupWithDelFileName = "GroupsWithDeletion.txt";
            int[] groupsWithDeletedEntries = null;
            if (File.Exists(groupWithDelFileName))
            {
                FileInfo fileInfo = new FileInfo(groupWithDelFileName);
                if (DateTime.Compare(fileInfo.LastWriteTime, DateTime.Today) >= 0)
                {
                    groupsWithDeletedEntries = ReadGroupsWithDeletion(groupWithDelFileName);
                }
            }
            else
            {               
                StreamWriter changeGroupsWriter = new StreamWriter(groupWithDelFileName);
                List<int> groupWithDeletedList = new List<int> ();               
                int groupId = 0;
                foreach (string updateEntry in updateEntries)
                {
                    groupId = DeleteObsoleteEntryInfo(updateEntry);
                    if (groupId == -1)
                    {
                        continue;
                    }
                    if (!groupWithDeletedList.Contains(groupId))
                    {
                        groupWithDeletedList.Add(groupId);
                        changeGroupsWriter.WriteLine(groupId);
                    }
                }

                // delete these entries are not in pdb
                DeleteObsoleteEntryInfo(changeGroupsWriter, groupWithDeletedList);

                changeGroupsWriter.Close();
                groupsWithDeletedEntries = new int[groupWithDeletedList.Count];
                groupWithDeletedList.CopyTo(groupsWithDeletedEntries);
            }
            return groupsWithDeletedEntries;
        }   
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="changeGroupsWriter"></param>
        /// <param name="groupWithDeletedList"></param>
        private void DeleteObsoleteEntryInfo (StreamWriter changeGroupsWriter, List<int> groupWithDeletedList)
        {
            ProtCidObsoleteDataRemover obsEntryRetriever = new ProtCidObsoleteDataRemover();
            string[] obsEntries = obsEntryRetriever.GetObsoleteChainEntries ();
            int groupId = 0;
            foreach (string obsEntry in obsEntries)
            {
                groupId = DeleteObsoleteEntryInfo(obsEntry);
                DeleteInterfaceCompData(obsEntry);
                if (groupId == -1)
                {
                    continue;
                }
                if (!groupWithDeletedList.Contains(groupId))
                {
                    groupWithDeletedList.Add(groupId);
                    changeGroupsWriter.WriteLine(groupId);
                }

            }
            changeGroupsWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int DeleteObsoleteEntryInfo(string pdbId)
        {
            int groupSeqId = GetCurrentGroupSeqId(pdbId);
            if (groupSeqId > -1)
            {
                string deleteString = string.Format("Delete From {0}HomoSeqInfo Where PdbId = '{1}';",
                    ProtCidSettings.dataType, pdbId);
                ProtCidSettings.protcidQuery.Query( deleteString);

                deleteString = string.Format("Delete From {0}HomoRepEntryAlign " +
                    " Where PdbId1 = '{1}' OR PdbID2 = '{1}';", ProtCidSettings.dataType, pdbId);
                ProtCidSettings.protcidQuery.Query( deleteString);

                deleteString = string.Format("Delete From {0}HomoGroupEntryAlign " +
                    " Where PdbId1 = '{1}' OR PdbID2 = '{1}';", ProtCidSettings.dataType, pdbId);
                ProtCidSettings.protcidQuery.Query( deleteString);
            }
            return groupSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteInterfaceCompData (string pdbId)
        {
            string deleteString = string.Format("Delete From DifEntryInterfaceComp Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            ProtCidSettings.protcidQuery.Query(deleteString);

            deleteString = string.Format("Delete From EntryInterfaceComp Where PdbID = '{0}';", pdbId);
            ProtCidSettings.protcidQuery.Query(deleteString);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetCurrentGroupSeqId(string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID From {0}HomoSeqInfo Where PdbID = '{1}';",
                ProtCidSettings.dataType, pdbId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupSeqId = -1;
            if (groupIdTable.Rows.Count == 0)
            {
                queryString = string.Format("Select GroupSeqID From {0}HomoRepEntryAlign Where PdbID2 = '{1}';",
                    ProtCidSettings.dataType, pdbId);
                groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            if (groupIdTable.Rows.Count > 0)
            {
                groupSeqId = Convert.ToInt32(groupIdTable.Rows[0]["GroupSeqID"].ToString());
            }
            return groupSeqId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="changedGroups"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetGroupLeftEntries(int[] changedGroups, out int[] deletedGroupIds)
        {
            Dictionary<int, string[]> groupLeftEntriesHash = new Dictionary<int, string[]>();
            List<int> deletedGroupIdList = new List<int> ();
           
            foreach (int changedGroup in changedGroups)
            {
                string[] leftGroupEntries = GetGroupEntries(changedGroup);

                if (leftGroupEntries.Length == 0)
                {
                    DeletePfamGroupsRow(changedGroup);
                    deletedGroupIdList.Add(changedGroup);
                }
                else
                {
                    groupLeftEntriesHash.Add(changedGroup, leftGroupEntries);
                }
            }
            deletedGroupIds = new int[deletedGroupIdList.Count];
            deletedGroupIdList.CopyTo(deletedGroupIds);

            return groupLeftEntriesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void DeletePfamGroupsRow(int groupSeqId)
        {
            string deleteString = string.Format("Delete From {0}Groups Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqIds"></param>
        private void DeletePfamGroupInfos(int[] groupSeqIds)
        {
            foreach (int groupSeqId in groupSeqIds)
            {
                DeletePfamGroupInfo(groupSeqId);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void DeletePfamGroupInfo(int groupSeqId)
        {
            string deleteString = string.Format ("Delete From {0}NonRedundantCfGroups Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From {0}ReduntCrystForms Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From {0}SgInterfaces Where GroupSeqID = {1};", 
                ProtCidSettings.dataType, groupSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From {0}InterfaceClusters Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private string[] GetGroupEntries(int groupSeqId)
        {
            List<string> groupEntryList = new List<string> ();
            string pdbId = "";
            string queryString = string.Format("Select Distinct PdbID From {0}HomoSeqInfo Where GroupSeqID = {1};", 
                ProtCidSettings.dataType, groupSeqId);
            DataTable groupRepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow entryRow in groupRepEntryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID"].ToString ());
            }
            queryString = string.Format("Select Distinct PdbID2 From {0}HomoRepEntryAlign Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                pdbId = homoEntryRow["PdbID2"].ToString();
                if (! groupEntryList.Contains(pdbId))  // should not have same entry, but ?
                {
                    groupEntryList.Add(pdbId);
                }
            }
            return groupEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void DeletePfamHomoGroupInfo(int groupSeqId)
        {
            string deleteString = string.Format("Delete From {0}HomoSeqInfo Where GroupSeqID = {1};", 
                ProtCidSettings.dataType, groupSeqId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void DeletePfamGroupAlignInfo(int groupSeqId)
        {
            string deleteString = string.Format("Delete From {0}HomoRepEntryAlign Where GroupSeqID = {1};",
                ProtCidSettings.dataType, groupSeqId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            deleteString = string.Format("Delete From {0}HomoGroupEntryAlign Where GroupSeqID = {1};", 
                ProtCidSettings.dataType, groupSeqId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        #endregion

        #region entryPfamArch groups
        /// <summary>
        /// classify pfam group into subgroups based on
        /// number of entities and domain families
        /// </summary>
        /// <param name="entryList"></param>
        private void AddEntryPfamArchGroups(string entryPfamArch, string[] groupEntries)
        {            
            AddEntryDataToTable(groupEntries, groupSeqNum);
            RetrieveRepEntries();

            InsertDataIntoDb();

            // add group seqID for pfam family
            DataRow pfamGroupRow = HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups].NewRow();
            pfamGroupRow["GroupSeqID"] = groupSeqNum;
            pfamGroupRow["EntryPfamArch"] = entryPfamArch;
            HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups].Rows.Add(pfamGroupRow);

            groupSeqNum++;

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups].Clear();
            entryAlignment.entryEntityFamilyTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetPfamArchEntryHash()
        {
            string queryString = "Select Distinct PdbID From PfamEntityPfamArch;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pdbIds = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbIds[count] = entryRow["PdbID"].ToString();
                count++;
            }

            Dictionary<string, List<string>> entryPfamArchEntryHash = GetPfamArchEntryHash(pdbIds);

            return entryPfamArchEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetPfamArchEntryHash(string[] pdbIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = pdbIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbIds.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get entry Pfam architecture from db.");

            string entryPfamArch = "";
            Dictionary<string, List<string>> entryPfamArchEntryHash = new Dictionary<string,List<string>> ();
            foreach (string pdbId in pdbIds)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                if (ProtCidSettings.dataType.IndexOf("clan") > -1)
                {
                    entryPfamArch = pfamArch.GetEntryGroupClanArch(pdbId);
                }
                else
                {
                    entryPfamArch = pfamArch.GetEntryGroupPfamArch(pdbId);
                }
                // skip those entries without pfam
                if (entryPfamArch == "")
                {
                    continue;
                }
                if (entryPfamArchEntryHash.ContainsKey(entryPfamArch))
                {
                    if (!entryPfamArchEntryHash[entryPfamArch].Contains(pdbId))
                    {
                        entryPfamArchEntryHash[entryPfamArch].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    entryPfamArchEntryHash.Add(entryPfamArch, entryList);
                }
            }
            return entryPfamArchEntryHash;
        }
        #endregion

        // added in July 15, 2010
        #region fill alignments from the previous data for those not updated entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        private void FillAlignmentsFromPreviousData(int groupId, string[] updateEntries)
        {
            string tableName = GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign];
            FillAlignmentsFromPreviousData(groupId, updateEntries, tableName, ref HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
        
            tableName = GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign];
            FillAlignmentsFromPreviousData(groupId, updateEntries, tableName, ref HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
        }
        /// <summary>
        /// 
        /// </summary>
        private void FillAlignmentsFromPreviousData(int groupId, string[] updateEntries, string tableName, ref DataTable alignmentTable)
        {
            DataTable tempAlignmentTable = alignmentTable.Copy();
            alignmentTable.Clear();

            string pdbId1 = "";
            string pdbId2 = "";
            List<string> parsedEntityPairList = new List<string> ();
            string entityPair = "";
            foreach (DataRow tempRow in tempAlignmentTable.Rows)
            {
                pdbId1 = tempRow["PdbID1"].ToString();
                pdbId2 = tempRow["PdbID2"].ToString();
                if (Array.IndexOf(updateEntries, pdbId1) > -1 ||
                    Array.IndexOf(updateEntries, pdbId2) > -1)
                {
                    DataRow newRow = alignmentTable.NewRow();
                    newRow.ItemArray = tempRow.ItemArray;
                    alignmentTable.Rows.Add(newRow);
                    continue;
                }
                DataTable entryAlignTable = GetAvailableEntryAlignments(groupId, pdbId1, pdbId2, tableName);
                if (entryAlignTable.Rows.Count > 0)
                {
                    foreach (DataRow alignRow in entryAlignTable.Rows)
                    {
                        entityPair = alignRow["PdbID1"].ToString() + alignRow["EntityID1"].ToString()
                            + "_" + alignRow["PdbID2"].ToString() + alignRow["EntityID2"].ToString();
                        if (parsedEntityPairList.Contains(entityPair))
                        {
                            continue;
                        }
                        parsedEntityPairList.Add(entityPair);
                        DataRow newRow = alignmentTable.NewRow();
                        newRow.ItemArray = alignRow.ItemArray;
                        alignmentTable.Rows.Add(newRow);
                    }
                }
                else // copy the align data 
                {
                    DataRow newRow = alignmentTable.NewRow();
                    newRow.ItemArray = tempRow.ItemArray;
                    alignmentTable.Rows.Add(newRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetAvailableEntryAlignments(int groupId, string pdbId1, string pdbId2, string tableName)
        {
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1} AND " + 
                " PdbID1 = '{2}' AND PdbID2 = '{3}';", tableName, groupId, pdbId1, pdbId2);
            DataTable alignmentTable = ProtCidSettings.protcidQuery.Query( queryString);
            return alignmentTable;
        }
        #endregion

        #region add seqranges to table
        public void AddSeqRangesToTable()
        {
            string queryString = "Select Distinct PdbID From PfamEntityPfamArch;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                AddEntryEntityPfamSeqRanges(pdbId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void AddEntryEntityPfamSeqRanges(string pdbId)
        {
            DataTable entryDomainTable = pfamArch.GetPfamDomainDefTable(pdbId);
            Dictionary<int, string> entityPfamSeqRangesHash = pfamArch.GetEntityPfamArchSeqRanges(entryDomainTable);

            DataTable entryDomainTableE3 = pfamArch.GetEntrySubDomainTable(entryDomainTable, PfamLibSettings.goodEvalueCutoff);
            Dictionary<int, string> entityPfamSeqRangesHashE3 = pfamArch.GetEntityPfamArchSeqRanges(entryDomainTableE3);

            DataTable entryDomainTableE5 = pfamArch.GetEntryStrongDomainTable(entryDomainTable);
            Dictionary<int, string> entityPfamSeqRangesHashE5 = pfamArch.GetEntityPfamArchSeqRanges(entryDomainTableE5);

            string pfamSeqRanges = "";
            string pfamSeqRangesE3 = "";
            string pfamSeqRangesE5 = "";
            foreach (int entityId in entityPfamSeqRangesHash.Keys)
            {
                pfamSeqRanges = (string)entityPfamSeqRangesHash[entityId];
                pfamSeqRangesE3 = (string)entityPfamSeqRangesHashE3[entityId];
                pfamSeqRangesE5 = (string)entityPfamSeqRangesHashE5[entityId];

                UpdatePfamSeqRanges(pdbId, entityId, pfamSeqRanges, pfamSeqRangesE3, pfamSeqRangesE5);
            }
        }

        private void UpdatePfamSeqRanges(string pdbId, int entityId, string seqRanges, string seqRangesE3, string seqRangesE5)
        {
            string updateString = string.Format("Update PfamEntityPfamArch Set SeqRanges = '{0}', SeqRangesE3 = '{1}', SeqRangesE5 = '{2}' " +
                " Where PdbID = '{3}' AND EntityID = {4};", seqRanges, seqRangesE3, seqRangesE5, pdbId, entityId);
            dbUpdate.Update(ProtCidSettings.pdbfamDbConnection, updateString);
        }
        #endregion

		#region Initialize tables
		/// <summary>
		/// initialize tables
		/// </summary>
		/// <param name="isUpdate"></param>
		protected void InitializeTables (bool isUpdate)
		{
			// create tables in memory
			HomoGroupTables.InitializeTables ();
			if (! isUpdate)
			{
                HomoGroupTables.InitializeAllGroupDbTables();
                // reuse the alignments data
            //    HomoGroupTables.InitializeGroupTable(); 
           //     HomoGroupTables.AddFlagColumn ();

	    	}
		}
		#endregion

        #region insert data into db, alignments data exist
        /// <summary>
        /// insert data into database
        /// </summary>
        private void InsertDataIntoDb()
        {
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo]);
            InsertAlignmentData();

            HomoGroupTables.ClearTables();
        }

        /// <summary>
        /// insert alignment data 
        /// </summary>
        private void InsertAlignmentData()
        {
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
        }
    
        #endregion

        #region update groups for update entries
        public Dictionary<int, string[]> GetUpdateGroupHashForEntries (string[] updateEntries)
        {
            Dictionary<int, List<string>> updateGroupEntryListHash = new Dictionary<int, List<string>>();
            int groupId = 0;
            
            foreach (string pdbId in updateEntries)
            {
                groupId = GetEntryGroupId(pdbId);
                if (updateGroupEntryListHash.ContainsKey(groupId))
                {
                    updateGroupEntryListHash[groupId].Add(pdbId);
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    updateGroupEntryListHash.Add(groupId, entryList);
                }
            }
            List<int> groupIdList = new List<int> (updateGroupEntryListHash.Keys);
            Dictionary<int, string[]> updateGroupEntryHash = new Dictionary<int, string[]>();
            groupIdList.Sort();
            string dataLine = "";
            StreamWriter dataWriter = new StreamWriter("updateGroups.txt");
            foreach (int lsGroupId in groupIdList)
            {
                updateGroupEntryHash.Add(lsGroupId, updateGroupEntryListHash[lsGroupId].ToArray());
                dataLine = lsGroupId.ToString();
                foreach (string pdbId in updateGroupEntryListHash[lsGroupId])
                {
                    dataLine += (" " + pdbId);
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
            return updateGroupEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetEntryGroupId (string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamHomoSeqInfo where pdbId = '{0}';", pdbId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = -1;
            if (groupIdTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32 (groupIdTable.Rows[0]["GroupSeqID"].ToString ()); 
            }
            return groupId;
        }
        #endregion

        #region add entries to existing groups  -- for debug
        public void AddEntriesToExistingGroups ()
        {
            string entryFile = "EntriesNotInEntryLevelGroups.txt";
            List<string> entryList = new List<string> ();
            StreamReader dataReader = new StreamReader(entryFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                entryList.Add(line);
            }
            dataReader.Close();

        }
        #endregion
    }
}
