using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.AuxFuncs;
using PfamLib.PfamArch;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class SeqFastaGenerator
    {
        #region member variables
        public string webFastaFileDir = "";
        public CmdOperations tarOperator = new CmdOperations();
        public PfamArchitecture pfamArch = new PfamArchitecture();
        public DbQuery dbQuery = new DbQuery();
        public StreamWriter logWriter = null;
        public FileCompress fileCompress = new FileCompress();
        #endregion

        #region public interfaces
        /// <summary>
        /// output entity sequences of groups to files 
        /// </summary>
        /// <returns></returns>
        public void WriteSequencesToFastaFiles()
        {
            logWriter = new StreamWriter("SeqWriteLog.txt");
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }

            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\ChainFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }
            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\ChainFasta", "\\webChainFasta");
            if (! Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperInterfaceClusters Where SuperGroupSeqId > 29857";
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = 0;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Seq Files";
            ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;

            foreach (DataRow dRow in groupTable.Rows)
            {
                superGroupId = Convert.ToInt32(dRow["SuperGroupSeqID"].ToString());
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    WriteSuperGroupEntrySequences(superGroupId);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(superGroupId.ToString() + " " + ex.Message);
                    logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Tar Sequence files for each group and its clusters");
            PrintClusterFastaFiles();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate sequence files done!");
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        public void UpdateSeqFastaFiles(int[] superGroupIds)
        {
            logWriter = new StreamWriter("SeqWriteLog.txt");
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = superGroupIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = superGroupIds.Length;
            ProtCidSettings.progressInfo.currentOperationLabel = "Seq Fasta Files";
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\ChainFasta");

   //         UpdateClusterFastaFiles(superGroupIds);

            foreach (int superGroupId in superGroupIds)
            {
                try
                {
                    ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    DeleteObsoleteSeqFiles(superGroupId);

                    WriteSuperGroupEntrySequences(superGroupId);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(superGroupId.ToString() + " " + ex.Message);
                    logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString () + "  " + ex.Message);
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Tar the sequence files of group and clusters for the web server.");
            UpdateClusterFastaFiles(superGroupIds);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update sequence files done!");
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        private void DeleteObsoleteSeqFiles(int superGroupId)
        {
            string groupFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Group" + superGroupId.ToString() + "A.fasta");
            File.Delete(groupFile);
            groupFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Group" + superGroupId.ToString() + "B.fasta");
            File.Delete(groupFile);
            string clusterFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + superGroupId.ToString() + "A.txt");
            File.Delete(clusterFile);
            clusterFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + superGroupId.ToString() + "B.txt");
            File.Delete(clusterFile);
        }
        #endregion

        #region sequences for the super group
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void WriteSuperGroupEntrySequences(int superGroupId)
        {
            string familyString = GetFamilyString(superGroupId);
            if (familyString == "")
            {
                return;
            }
            string[] chainPfamArchs = familyString.Split(';');

            string fileNameA = "Group" + superGroupId.ToString() + "A.fasta";
    /*        if (File.Exists (Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, fileNameA)))
            {
                return;
            }*/
            StreamWriter groupSeqWriterA = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, fileNameA));
            List<string> entityListA = new List<string> ();

            StreamWriter groupSeqWriterB = null;
            List<string> entityListB = null;
            if (chainPfamArchs.Length == 2)
            {
                string fileNameB = "Group" + superGroupId.ToString() + "B.fasta";
                groupSeqWriterB = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, fileNameB));
                entityListB = new List<string> ();
            }

            // string[] repEntriesInSuperGroup = GetCfRepEntriesInSuperGroup(superGroupId);
            string[] entriesInSuperGroup = GetEntriesInSuperGroup(superGroupId);

            DataTable entityInfoTable = GetEntryEntityInfoTable(entriesInSuperGroup);
            DataTable cfTable = GetCrystFormTable(entriesInSuperGroup);
            Dictionary<string, Dictionary<int, string>> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(entriesInSuperGroup);

            foreach (string entry in entriesInSuperGroup)
            {
                DataTable entryEntityTable = GetEntryEntityInfoTable(entry, entityInfoTable);
                if (! entityPfamArchHash.ContainsKey (entry))
                {
                    continue;
                }
                Dictionary<int, string> entryEntityPfamArchHash = entityPfamArchHash[entry];
                string cfString = GetCrystForm(entry, cfTable);

                int[] entities = FindEntrySequencesForPfamArch(entry, chainPfamArchs[0], entryEntityPfamArchHash);
                WriteEntitySequencesToFile(entry, entities, entryEntityTable, cfString, groupSeqWriterA);
                foreach (int entity in entities)
                {
                    entityListA.Add(entry + entity.ToString());
                }
                if (chainPfamArchs.Length == 2)
                {
                    entities = FindEntrySequencesForPfamArch(entry, chainPfamArchs[1], entryEntityPfamArchHash);
                    WriteEntitySequencesToFile(entry, entities, entryEntityTable, cfString, groupSeqWriterB);
                    foreach (int entity in entities)
                    {
                        entityListB.Add(entry + entity.ToString());
                    }
                }
            }
            groupSeqWriterA.Close();
            if (groupSeqWriterB != null)
            {
                groupSeqWriterB.Close();
            }

            WriteClusterSeqInfoToFile(superGroupId, entityListA, "A");
            if (entityListB != null && entityListB.Count > 0)
            {
                WriteClusterSeqInfoToFile(superGroupId, entityListB, "B");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <param name="entryEntityTable"></param>
        /// <param name="cfString"></param>
        /// <param name="groupSeqWriter"></param>
        private void WriteEntitySequencesToFile(string pdbId, int[] entities, DataTable entryEntityTable, string cfString, StreamWriter groupSeqWriter)
        {
            string dataLine = "";
            string[] cfFields = cfString.Split('_');
            string sequence = "";
            foreach (int entity in entities)
            {
                dataLine = ">" + pdbId + entity.ToString();
                DataRow[] sequenceRows = entryEntityTable.Select(string.Format("EntityID = {0}", entity));
                if (sequenceRows.Length > 0)
                {
                    sequence = sequenceRows[0]["Sequence"].ToString().TrimEnd();
                    if (IsValidSequence(sequence))
                    {
                        dataLine += " " + sequenceRows[0]["Name"].ToString().TrimEnd() + " " + cfFields[0] + " " + cfFields[1];
                        groupSeqWriter.WriteLine(dataLine);
                        groupSeqWriter.WriteLine(sequence);
                    }
                }
            }
            groupSeqWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private bool IsValidSequence(string sequence)
        {
            List<char> charList = new List<char> ();
            foreach (char ch in sequence)
            {
                if (!charList.Contains(ch))
                {
                    charList.Add(ch);
                }
            }
            if (charList.Count <= 1)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetFamilyString(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyStringTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (familyStringTable.Rows.Count > 0)
            {
                return familyStringTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="entityList"></param>
        /// <param name="type"></param>
        private void WriteClusterSeqInfoToFile(int superGroupId, List<string> entityList, string type)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;
            StreamWriter clusterSeqInfoWriter = null;
            if (type == "B")
            {
                string clusterFileNameB = "Cluster" + superGroupId.ToString() + "B.txt";
                clusterSeqInfoWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, clusterFileNameB));
            }
            else
            {
                string clusterFileNameA = "Cluster" + superGroupId.ToString() + "A.txt";
                clusterSeqInfoWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, clusterFileNameA));
            }
            clusterSeqInfoWriter.WriteLine("GroupID = " + superGroupId.ToString());
            string dataLine = "";

            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                clusterSeqInfoWriter.WriteLine("ClusterID = " + clusterId.ToString());
                dataLine = "";

                DataTable clusterEntryInfoTable = GetClusterEntryInfoTable(superGroupId, clusterId);
                string[] entriesInCluster = GetClusterEntries(clusterEntryInfoTable);
                foreach (string entryEntity in entityList)
                {
                    string entry = entryEntity.Substring(0, 4);
                    if (Array.IndexOf(entriesInCluster, entry) > -1)
                    {
                        int cfGroupId = GetCfGroupId(entry, clusterEntryInfoTable);
                        dataLine += (entryEntity + "_" + cfGroupId.ToString() + "\t");
                    }
                }
                clusterSeqInfoWriter.WriteLine(dataLine.TrimEnd('\t'));
            }
            clusterSeqInfoWriter.Close();
        }

        private int GetCfGroupId(string pdbId, DataTable clusterEntryInfoTable)
        {
            DataRow[] cfInfoRows = clusterEntryInfoTable.Select(string.Format("PdbID = '{0}'", pdbId));
            return Convert.ToInt32(cfInfoRows[0]["CfGroupID"].ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterEntries(DataTable clusterEntryInfoTable)
        {
            string[] entries = new string[clusterEntryInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in clusterEntryInfoTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterEntryInfoTable(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbId, CfGroupID From PfamSuperClusterEntryInterfaces " +
               " Where SuperGroupSeqID = {0} AND ClusterID = {1};", superGroupId, clusterId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryTable;
        }
        #endregion

        #region entity info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityInfoTable(string pdbId)
        {
            string queryString = string.Format("Select EntityId, AsymID, Sequence, Name, Species From AsymUnit " +
                " Where PdbId = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return entityInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityInfoTable(string pdbId, DataTable entityInfoTable)
        {
            DataRow[] entityRows = entityInfoTable.Select(string.Format("PdbID = '{0}'", pdbId));
            DataTable entryEntityTable = entityInfoTable.Clone();
            foreach (DataRow entityRow in entityRows)
            {
                DataRow newRow = entryEntityTable.NewRow();
                newRow.ItemArray = entityRow.ItemArray;
                entryEntityTable.Rows.Add(newRow);
            }
            return entryEntityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityInfoTable(string[] entries)
        {
            string queryString = "";
            DataTable entityInfoTable = null;
            for (int i = 0; i < entries.Length; i += 300)
            {
                string[] subEntries = ParseHelper.GetSubArray (entries, i, 300);
                queryString = string.Format("Select PdbID, EntityId, AsymID, Sequence, Name, Species From AsymUnit " +
                " Where PdbId IN ({0}) AND PolymerType = 'polypeptide';", ParseHelper.FormatSqlListString (subEntries));
                DataTable subEntityInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subEntityInfoTable, ref entityInfoTable);
            }            
            return entityInfoTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainPfamArch"></param>
        /// <param name="entryEntityPfamArchHash"></param>
        private int[] FindEntrySequencesForPfamArch(string pdbId, string chainPfamArch, Dictionary<int, string> entryEntityPfamArchHash)
        {
            List<int> entityList = new List<int> ();
            foreach (int entityId in entryEntityPfamArchHash.Keys)
            {
                string entityPfamArch = entryEntityPfamArchHash[entityId];
                if (entityPfamArch == chainPfamArch)
                {
                    entityList.Add(entityId);
                }
            }
            return entityList.ToArray ();
        }
        #endregion

        #region cf info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetCrystForm(string pdbId, Dictionary<string, string> entryCfHash)
        {
            string[] cfFields = null;
            if (entryCfHash.ContainsKey(pdbId))
            {
                string cfString = entryCfHash[pdbId];
                cfFields = cfString.Split('_');
            }
            else
            {
                string cfString = GetCrystForm(pdbId);
                entryCfHash.Add(pdbId, cfString);
                cfFields = cfString.Split('_');
            }
            return cfFields;
        }

        private string GetCrystForm(string pdbId)
        {
            string queryString = string.Format("Select SpaceGroup, ASU From PFamHomoSeqInfo WHere PdbID = '{0}';", pdbId);
            DataTable cfTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (cfTable.Rows.Count == 0)
            {
                queryString = string.Format("Select PdbID1 From PfamHomoRepEntryAlign WHere PdbID2 = '{0}';", pdbId);
                DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (repEntryTable.Rows.Count > 0)
                {
                    string repEntry = repEntryTable.Rows[0]["PdbID1"].ToString();
                    queryString = string.Format("Select SpaceGroup, ASU From PfamHomoSeqInfo Where PdbID = '{0}';", repEntry);
                    cfTable = ProtCidSettings.protcidQuery.Query( queryString);
                }
            }
            string cfString = "";
            if (cfTable.Rows.Count > 0)
            {
                cfString = cfTable.Rows[0]["SpaceGroup"].ToString().TrimEnd() + "_" +
                    cfTable.Rows[0]["ASU"].ToString().TrimEnd();
            }
            return cfString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="cfTable"></param>
        /// <returns></returns>
        private string GetCrystForm (string pdbId, DataTable cfTable)
        {
            string cfString = "";
            DataRow[] cfRows = cfTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            if (cfRows.Length > 0)
            {
                cfString = cfRows[0]["SpaceGroup"].ToString().TrimEnd() + "_" +
                    cfRows[0]["ASU"].ToString().TrimEnd();
            }
            return cfString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetCrystFormTable (string[] entries)
        {
            string queryString = "";
            DataTable cfTable = null;
            for (int i = 0; i < entries.Length; i += 300)
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 300);
                queryString = string.Format("Select PdbID, SpaceGroup, ASU From PFamHomoSeqInfo WHere PdbID IN ({0});", ParseHelper.FormatSqlListString (subEntries));
                DataTable subCfTable = ProtCidSettings.protcidQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subCfTable, ref cfTable);
            }

            List<string> homoEntryList = new List<string> (entries);
            string pdbId = "";
            foreach (DataRow cfRow in cfTable.Rows)
            {
                pdbId = cfRow["PdbID"].ToString();
                homoEntryList.Remove(pdbId);
            }
            List<string> leftRepEntryList = new List<string> ();
            string repEntry = "";
            string homoEntry = "";
            if (homoEntryList.Count > 0)
            {
                DataTable repEntryTable = null;
                for (int i = 0; i < homoEntryList.Count; i += 300)
                {
                    List<string> subHomoEntryList = ParseHelper.GetSubList(homoEntryList, i, 300);
                    queryString = string.Format("Select Distinct PdbID1, PdbID2 From PfamHomoRepEntryAlign WHere PdbID2 IN ({0});", ParseHelper.FormatSqlListString (subHomoEntryList.ToArray ()));
                    DataTable subRepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
                    ParseHelper.AddNewTableToExistTable(subRepEntryTable, ref repEntryTable);
                }
                foreach (DataRow repEntryRow in repEntryTable.Rows)
                {
                    repEntry = repEntryTable.Rows[0]["PdbID1"].ToString();
                    if (! entries.Contains (repEntry))
                    {
                        leftRepEntryList.Add(repEntry);
                    }
                }
                if (leftRepEntryList.Count > 0)
                {
                    queryString = string.Format("Select PdbID, SpaceGroup, ASU From PFamHomoSeqInfo WHere PdbID IN ({0});", ParseHelper.FormatSqlListString(leftRepEntryList.ToArray ()));
                    DataTable leftRepCfTable = ProtCidSettings.protcidQuery.Query( queryString);
                    ParseHelper.AddNewTableToExistTable(leftRepCfTable, ref cfTable);
                }
                foreach (DataRow repEntryRow in repEntryTable.Rows)
                {
                    repEntry = repEntryRow["PdbID1"].ToString();
                    homoEntry = repEntryRow["PdbID2"].ToString();
                    DataRow[] cfRows = cfTable.Select(string.Format ("PdbID = '{0}'", repEntry));
                    if (cfRows.Length > 0)
                    {
                        DataRow homoCfRow = cfTable.NewRow();
                        homoCfRow.ItemArray = cfRows[0].ItemArray;
                        homoCfRow["PdbID"] = homoEntry;
                        cfTable.Rows.Add(homoCfRow);
                    }
                }
            }
            return cfTable;
        }
        #endregion

        #region entries in a super group
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetRepEntriesInSuperGroup(int superGroupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo, PfamSuperGroups Where SuperGroupSeqID = {0} AND " +
                " PfamHomoSeqInfo.GroupSeqID = PfamSuperGroups.GroupSeqID;", superGroupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
           
            string[] groupEntries = new string[repEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                groupEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }            
            return groupEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetCfRepEntriesInSuperGroup(int superGroupId)
        {
            string queryString = string.Format("Select CfGroupId, PdbId From PfamNonRedundantCfGroups, PfamSuperGroups " +
                " Where SuperGroupSeqID = {0} AND PfamSuperGroups.GroupSeqID = PfamNonRedundantCfGroups.GroupSeqID;", superGroupId);
            DataTable cfEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> cfEntryList = new List<string> ();
            List<int> cfList = new List<int> ();
            int cfGroupId = 0;
            string entry = "";
            foreach (DataRow cfRow in cfEntryTable.Rows)
            {
                cfGroupId = Convert.ToInt32(cfRow["CfGroupId"].ToString());
                entry = cfRow["PdbID"].ToString();
                if (cfList.Contains(cfGroupId))
                {
                    continue;
                }
                cfList.Add(cfGroupId);
                cfEntryList.Add(entry);
            }
            return cfEntryList.ToArray ();
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetEntriesInSuperGroup(int superGroupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo, PfamSuperGroups Where SuperGroupSeqID = {0} AND " +
                " PfamHomoSeqInfo.GroupSeqID = PfamSuperGroups.GroupSeqID;", superGroupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID2 As PdbID From PfamHomoRepEntryAlign, PfamSuperGroups Where SuperGroupSeqID = {0} AND " +
                " PfamHomoRepEntryAlign.GroupSeqID = PfamSuperGroups.GroupSeqID;", superGroupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> groupEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                groupEntryList.Add (entryRow["PdbID"].ToString());
            }
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
               if (! groupEntryList.Contains (pdbId))
               {
                   groupEntryList.Add(pdbId);
               }
            }
            return groupEntryList.ToArray ();
        }       
        #endregion

        #region clusters fasta files
        #region seq fasta files
        /// <summary>
        /// 
        /// </summary>
        public void PrintClusterFastaFiles()
        {
            string[] allGroupFastaFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, "group*.fasta");
            Dictionary<int, List<string>> groupFastaFilesHash = GetGroupIDsFromFiles(allGroupFastaFiles);
            string groupIdWithType = "";
            string clusterInfoFile = "";
            List<string> fastaFileList = new List<string> ();
            List<int> groupIdList = new List<int> (groupFastaFilesHash.Keys);
            groupIdList.Sort();
            string tarFile = "";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Tar Seq Files";
            ProtCidSettings.progressInfo.totalOperationNum = groupIdList.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupIdList.Count;

            foreach (int chainGroupId in groupIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = chainGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                fastaFileList.Clear();
                foreach (string groupFastaFile in groupFastaFilesHash[chainGroupId])
                {
                    groupIdWithType = GetGroupIdWithTypeFromFileName(groupFastaFile);
                    fastaFileList.Add("Group" + groupIdWithType + ".fasta");
                    try
                    {
                        clusterInfoFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + groupIdWithType + ".txt");
                        Dictionary<string, string> groupEntitySequenceHash = null;
                        Dictionary<string, string> groupEntityAnnotationHash = null;
                        Dictionary<int, string[]> clusterEntityHash = GetClusterEntityHash(clusterInfoFile);
                        ReadEntitySequenceHash(groupFastaFile, out groupEntitySequenceHash, out groupEntityAnnotationHash);
                        foreach (int clusterId in clusterEntityHash.Keys)
                        {
                            string[] clusterEntities = (string[])clusterEntityHash[clusterId];
                            string clusterFastaFile = WriteClusterEntitySequencesToFile(groupIdWithType, clusterId, clusterEntities, groupEntitySequenceHash, groupEntityAnnotationHash);
                            fastaFileList.Add("Cluster" + groupIdWithType + "_" + clusterId.ToString() + ".fasta");
                        }
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Output cluster fasta files errors: " + groupFastaFile + " " + ex.Message);
                        logWriter.Flush();
                    }
                }
                string[] fastaFiles = new string[fastaFileList.Count];
                fastaFileList.CopyTo(fastaFiles);
                string chainGroupName = DownloadableFileName.GetChainGroupTarGzFileName(chainGroupId);
                try
                {
                    string fastaTarFile = "Seq_" + chainGroupName + ".tar.gz";
              /*      if (fastaFiles.Length > 100)
                    {
                        string groupFolder = MoveSeqFastaFilesToGroupFolder(fastaFiles, ProtCidSettings.dirSettings.seqFastaPath, groupId);
                        tarFile = TarFastaFilesOnFolder(groupId, groupFolder);
                    }
                    else
                    {
                        tarFile = TarFastaFiles(groupId, fastaFiles);
                    }*/
                    fastaTarFile = fileCompress.RunTar(fastaTarFile, fastaFiles, ProtCidSettings.dirSettings.seqFastaPath, true);
                    File.Move(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, fastaTarFile), Path.Combine(webFastaFileDir, fastaTarFile));
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Tar and move file error for " + tarFile + "  : " + ex.Message);
                    logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Tar sequence files done!");
        }
        #endregion
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFastaFiles"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetGroupIDsFromFiles(string[] groupFastaFiles)
        {
            Dictionary<int, List<string>> groupFastaFilesHash = new Dictionary<int, List<string>>();
            string groupIdWithType = "";
            foreach (string groupFastaFile in groupFastaFiles)
            {
                groupIdWithType = GetGroupIdWithTypeFromFileName(groupFastaFile);
                int groupId = Convert.ToInt32(groupIdWithType.Substring(0, groupIdWithType.Length - 1));
                if (groupFastaFilesHash.ContainsKey(groupId))
                {
                    groupFastaFilesHash[groupId].Add(groupFastaFile);
                }
                else
                {
                    List<string> groupFastaFileList = new List<string> ();
                    groupFastaFileList.Add(groupFastaFile);
                    groupFastaFilesHash.Add(groupId, groupFastaFileList);
                }
            }
            return groupFastaFilesHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIdWithType"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterEntities"></param>
        /// <param name="groupEntitySequenceHash"></param>
        /// <param name="groupEntityAnnotationHash"></param>
        private string WriteClusterEntitySequencesToFile(string groupIdWithType, int clusterId, string[] clusterEntities, Dictionary<string, string> groupEntitySequenceHash, Dictionary<string, string> groupEntityAnnotationHash)
        {
            string clusterFastaFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + groupIdWithType + "_" + clusterId.ToString() + ".fasta");
            StreamWriter dataWriter = new StreamWriter(clusterFastaFile);
            string sequence = "";
            string annotation = "";
            foreach (string entity in clusterEntities)
            {
                sequence = groupEntitySequenceHash[entity];
                annotation = groupEntityAnnotationHash[entity];
                dataWriter.WriteLine(">" + entity + " " + annotation);
                dataWriter.WriteLine(sequence);
            }
            dataWriter.Close();
            return clusterFastaFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fastaFileName"></param>
        /// <returns></returns>
        private string GetGroupIdWithTypeFromFileName(string fastaFileName)
        {
            int fileIndex = fastaFileName.LastIndexOf("\\");
            string fileName = fastaFileName.Substring(fileIndex + 1, fastaFileName.Length - fileIndex - 1);
            int groupIndex = "group".Length;
            int extIndex = fileName.IndexOf(".fasta");
            string groupIdWithType = fileName.Substring(groupIndex, extIndex - groupIndex);
            return groupIdWithType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInfoFile"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetClusterEntityHash(string clusterInfoFile)
        {
            StreamReader dataReader = new StreamReader(clusterInfoFile);
            string line = "";
            int clusterId = 0;
            Dictionary<int, string[]> clusterEntitiesHash = new Dictionary<int, string[]>();
            int clusterIndex = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                clusterIndex = line.IndexOf("ClusterID = ");
                if (clusterIndex > -1)
                {
                    clusterId = Convert.ToInt32(line.Substring("ClusterID = ".Length, line.Length - "ClusterID = ".Length));
                    line = dataReader.ReadLine();
                    string[] cfEntryEntities = line.Split('\t');
                    List<string> entryEntityList = new List<string>();
                    foreach (string cfEntity in cfEntryEntities)
                    {
                        string[] fields = cfEntity.Split('_');
                        if (!entryEntityList.Contains(fields[0]))
                        {
                            entryEntityList.Add(fields[0]);
                        }
                    }
                    clusterEntitiesHash.Add(clusterId, entryEntityList.ToArray ());
                }
            }
            dataReader.Close();
            return clusterEntitiesHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFastaFile"></param>
        /// <returns></returns>
        private void ReadEntitySequenceHash(string groupFastaFile, out Dictionary<string, string> entitySequenceHash, out Dictionary<string, string> entityAnnotationHash)
        {
            entitySequenceHash = new Dictionary<string, string>();
            entityAnnotationHash = new Dictionary<string,string> ();
            StreamReader dataReader = new StreamReader(groupFastaFile);
            string line = "";
            string entryEntity = "";
            string annotation = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line[0] == '>')
                {
                    int firstSpIndex = line.IndexOf(" ");
                    entryEntity = line.Substring(1, firstSpIndex - 1);
                    annotation = line.Substring(firstSpIndex + 1, line.Length - firstSpIndex - 1);
                    line = dataReader.ReadLine();
                    if (entitySequenceHash.ContainsKey (entryEntity) || entityAnnotationHash.ContainsKey (entryEntity))
                    {
                        logWriter.WriteLine(groupFastaFile + " " + entryEntity + " duplicate in entitySequenceHash");
                        logWriter.Flush();
                        continue;
                    }
                    entitySequenceHash.Add(entryEntity, line);                    
                    entityAnnotationHash.Add(entryEntity, annotation);
                }
            }
            dataReader.Close();
        }

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        public void UpdateClusterFastaFiles(int[] superGroupIds)
        {
            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\ChainFasta", "\\webChainFasta");
            if (!Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }
         //   string[] allGroupFastaFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, "group*.fasta");
            string groupIdWithType = "";
            string clusterInfoFile = "";
            List<string> fastaFileList = new List<string> ();
            string tarFile = "";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = superGroupIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = superGroupIds.Length;
            ProtCidSettings.progressInfo.currentOperationLabel = "Tar Seq Files";

            foreach (int groupId in superGroupIds)
            {
                ProtCidSettings.progressInfo.currentFileName = groupId.ToString();
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;

                fastaFileList.Clear();
                string[] groupFastaFiles = GetGroupSeqFastaFiles(groupId);
                foreach (string groupFastaFile in groupFastaFiles)
                {
                    groupIdWithType = GetGroupIdWithTypeFromFileName(groupFastaFile);
                    fastaFileList.Add("Group" + groupIdWithType + ".fasta");
                    try
                    {
                        clusterInfoFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + groupIdWithType + ".txt");
                        Dictionary<string, string> groupEntitySequenceHash = null;
                        Dictionary<string, string> groupEntityAnnotationHash = null;
                        Dictionary<int, string[]> clusterEntityHash = GetClusterEntityHash(clusterInfoFile);
                        ReadEntitySequenceHash(groupFastaFile, out groupEntitySequenceHash, out groupEntityAnnotationHash);
                        foreach (int clusterId in clusterEntityHash.Keys)
                        {
                            string[] clusterEntities = (string[])clusterEntityHash[clusterId];
                            string clusterFastaFile = WriteClusterEntitySequencesToFile(groupIdWithType, clusterId, clusterEntities, groupEntitySequenceHash, groupEntityAnnotationHash);
                            fastaFileList.Add("Cluster" + groupIdWithType + "_" + clusterId.ToString() + ".fasta");
                        }
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Output cluster fasta files errors: " + groupFastaFile + " " + ex.Message);
                        logWriter.Flush();
                    }
                }
                string[] fastaFiles = new string[fastaFileList.Count];
                fastaFileList.CopyTo(fastaFiles);

                string chainGroupName = DownloadableFileName.GetChainGroupTarGzFileName(groupId);
                try
                {
                    DeleteObsoleteWebFastaFiles(chainGroupName);
              //      tarFile = TarFastaFiles(groupId, fastaFiles);
                    tarFile = "Seq_" + chainGroupName + ".tar.gz";
                    tarFile = fileCompress.RunTar(tarFile, fastaFiles, ProtCidSettings.dirSettings.seqFastaPath, true);
                    File.Move(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, tarFile), Path.Combine(webFastaFileDir, tarFile));
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Tar and move file error for " + tarFile + "  : " + ex.Message);
                    logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Tar sequence files done!");
            ProtCidSettings.logWriter.WriteLine("Tar sequence files done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetGroupSeqFastaFiles(int superGroupId)
        {
            string[] groupSeqFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, "Group" + superGroupId.ToString () + "*.fasta");
            List<string> seqFastaFileList = new List<string> ();
            foreach (string groupSeqFile in groupSeqFiles)
            {
                int groupId = GetGroupIdFromFileName(groupSeqFile);
                if (groupId == superGroupId)
                {
                    seqFastaFileList.Add(groupSeqFile);
                }
            }
            return seqFastaFileList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqFastaFile"></param>
        /// <returns></returns>
        private int GetGroupIdFromFileName(string seqFastaFile)
        {
            string groupIdWithType = GetGroupIdWithTypeFromFileName(seqFastaFile);
            int groupId = Convert.ToInt32(groupIdWithType.Substring (0, groupIdWithType.Length - 1));
            return groupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void DeleteObsoleteWebFastaFiles(string chainGroupName)
        {
            string tarSeqFile = Path.Combine(webFastaFileDir, "Seq_" + chainGroupName + ".tar.gz");
            File.Delete(tarSeqFile);
        }
        #endregion
        #endregion
    }
}
