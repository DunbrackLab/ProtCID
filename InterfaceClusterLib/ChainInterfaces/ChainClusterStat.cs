using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using InterfaceClusterLib.Clustering;
using InterfaceClusterLib.Alignments;
using CrystalInterfaceLib.Settings;
using PfamLib.PfamArch;
using InterfaceClusterLib.stat;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class ChainClusterStat : ClusterStat
    {
        #region member variables
        private DataTable superGroupClusterSumTable = new DataTable();
        private string resultDir = "";
        private EntryAlignment entryAlignment = new EntryAlignment();
   //     private string[] antibodyGroups = {"(C1-set)", "(V-set)", "(V-set)_(C1-set)"};
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public void PrintSupergroupInterfaceClusters(string type)
        {
             InitializeTables(type + "Super", false);
        //   InitializeTables(type + "Super", true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo.txt"), true);

            List<int> superGroupList = new List<int> ();
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable superGroupsTable = protcidQuery.Query(queryString);
            int supergroupId = 0;
            string chainRelPfamArch = "";
            
            foreach (DataRow superGroupRow in superGroupsTable.Rows)
            {
                supergroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                chainRelPfamArch = superGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
         /*       if (Array.IndexOf(ChainInterfaceBuilder.antibodyGroups, chainRelPfamArch) > -1) // exclude antibody groups
                {
                    continue;
                }*/
                superGroupList.Add(supergroupId);
            }

            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
            ProtCidSettings.progressInfo.totalOperationNum = superGroupList.Count;
            ProtCidSettings.progressInfo.totalStepNum = superGroupList.Count;
            superGroupList.Sort();
            foreach (int thisSupergroupId in superGroupList)
            {
                sgInterfaceNumHash.Clear();
                superGroupClusterSumTable.Clear();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(thisSupergroupId.ToString ());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = thisSupergroupId.ToString();

                try
                {
                    PrintGroupClusterStatInfo(thisSupergroupId, clusterWriter, clusterSumWriter, type);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(thisSupergroupId.ToString () + " Cluster stat info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine (thisSupergroupId.ToString() + " Cluster stat info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                clusterSumWriter.Flush();
                clusterWriter.Flush();
            }
            clusterWriter.Close();
            clusterSumWriter.Close();

            // post-process the result files.
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Divide the cluster info file into smaller files.");
            DivideChainInterfaceResultOutputFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo.txt"));

            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo.txt"));
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo.txt"));

            // generate meta data for protcid
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate summary info for chain ProtCID");
            GenerateProtCidMetaData(type);

            // PfamChainArchRelation: the pair of chain pfam arch for each entry
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Chain Pfam arch relation for each entry");
            GetChainInterfacePfamArchRel();

            // PfamChainPairInPdb: summary data for each pair of chain pfam arch
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("summary info for each pair of chain pfam archs");
            RetrieveChainArchPairsMetaData();        
#if DEBUG
            logWriter.Close();
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <param name="clusterWriter"></param>
        /// <param name="clusterSumWriter"></param>
        /// <param name="type"></param>
        private void PrintGroupClusterStatInfo(int supergroupId, StreamWriter clusterWriter, StreamWriter clusterSumWriter, string type)
        {
            int[] groupIds = GetGroupSeqIDs(supergroupId);
            if (groupIds.Length == 0)
            {
                return;
            }
            Dictionary<string, string> supergroupFamilyArchChainHash = GetSuperGroupFamilyArchChainHash(supergroupId);
            int totalNumOfEntryInSuperGroup = 0;
            int totalNumOfCfgsInSuperGroup = 0;
            foreach (int groupSeqId in groupIds)
            {               
                try
                {                    
                    DataTable clusterTable = GetSuperGroupClusterInfo(supergroupId, groupSeqId);
                    List<string> pdbList = new List<string> ();
                    foreach (DataRow clusterRow in clusterTable.Rows)
                    {
                        if (!pdbList.Contains(clusterRow["PdbID"].ToString()))
                        {
                            pdbList.Add(clusterRow["PdbID"].ToString());
                        }
                    }
                    // cluster info	
                    DataTable interfaceTable = GetInterfaceTable(groupSeqId, pdbList);
                    DataTable entityInfoTable = GetEntityInfoTable(pdbList);
                    Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);
                    UpdateSuperCfGroupId(supergroupId, groupSeqId, entryCfGroupHash);

                    pdbGroupEntryBuAbcFormatHash.Clear();
                    pisaGroupEntryBuAbcFormatHash.Clear();
                    groupEntryEntityChainNameHash.Clear();

                    GetTotalCfgsEntriesInGroup(groupSeqId, ref totalNumOfCfgsInSuperGroup, ref totalNumOfEntryInSuperGroup, entryCfGroupHash);

                    string[] repEntries = new string[pdbList.Count];
                    pdbList.CopyTo(repEntries);
                    SetGroupEntryEntityChainNameHash(repEntries, supergroupFamilyArchChainHash);
                    string[] homoEntries = GetGroupHomoEntries(groupSeqId, type);
                    SetGroupEntryEntityChainNameHash(homoEntries, supergroupFamilyArchChainHash);

                    FormatGroupClusterInfoIntoTable(groupSeqId, clusterTable, interfaceTable,
                        entityInfoTable, entryCfGroupHash);
                    // interfaces in BUs of homologous entries in a space group
                    FormatRepInterfacesInHomoBUs(groupSeqId, clusterTable, entryCfGroupHash);

                    // interface qscore in a cluster
                    DataTable groupInterfaceCompTable = GetGroupInterfaceCompTable(pdbList);
                    DataTable sgInterfaceCompTable = GetInterfaceOfSgCompTable(pdbList);
                    DataTable groupRepAlignTable = GetGroupRepAlignTable(groupSeqId, pdbList);

                    FormatClusterSumInfoIntoTable(groupSeqId, clusterTable, groupInterfaceCompTable,
                        sgInterfaceCompTable, groupRepAlignTable, entryCfGroupHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine("Retrieving cluster summary info error: " + 
                        supergroupId.ToString () + " " + groupSeqId.ToString () + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            FormatSuperGroupClusterSumInfoTable(supergroupId, totalNumOfCfgsInSuperGroup, totalNumOfEntryInSuperGroup);
            Dictionary<int, string> groupFamilyStringHash = GetGroupFamilyStringHash(groupIds);
            WriteStatDataToFile(supergroupId, clusterWriter, clusterSumWriter, groupFamilyStringHash);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryCfGroupHash"></param>
        private void UpdateSuperCfGroupId(int superGroupId, int groupSeqId, Dictionary<string, int> entryCfGroupHash)
        {
            Dictionary<int, int> cfGroupSuperCfHash = new Dictionary<int,int> ();
            int cfGroupId = -1;
            int superCfGroupId = -1;
            List<string> entryList = new List<string>(entryCfGroupHash.Keys);
            foreach (string entry in entryList)
            {
                cfGroupId = entryCfGroupHash[entry];
                superCfGroupId = GetSuperCfGroupId(superGroupId, groupSeqId, cfGroupId, ref cfGroupSuperCfHash);
                entryCfGroupHash[entry] = superCfGroupId;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="groupId"></param>
        /// <param name="cfGroupId"></param>
        /// <returns></returns>
        private int GetSuperCfGroupId(int superGroupId, int groupId, int cfGroupId, ref Dictionary<int, int> cfGroupSuperCfHash)
        {
            int superCfGroupId = -1;
            if (cfGroupSuperCfHash.ContainsKey(cfGroupId))
            {
                superCfGroupId = (int)cfGroupSuperCfHash[cfGroupId];
            }
            else
            {
                string queryString = string.Format("Select SuperCfGroupId From PfamSuperCfGroups " +
                    " Where SuperGroupSeqID = {0} AND GroupSeqID = {1} AND CfGroupID = {2};",
                    superGroupId, groupId, cfGroupId);
                DataTable superCfGroupIdTable = protcidQuery.Query(queryString);
                if (superCfGroupIdTable.Rows.Count > 0)
                {
                    superCfGroupId = Convert.ToInt32(superCfGroupIdTable.Rows[0]["SuperCfGroupID"].ToString());
                }
                cfGroupSuperCfHash.Add(cfGroupId, superCfGroupId);
            }
            return superCfGroupId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="totalNumOfCfgsInSuperGroup"></param>
        /// <param name="totalNumOfEntryInSuperGroup"></param>
        private void GetTotalCfgsEntriesInGroup(int groupSeqId, ref int totalNumOfCfgsInSuperGroup,
            ref int totalNumOfEntryInSuperGroup, Dictionary<string, int> entryCfGroupHash)
        {
            // get the number of space groups in the family
            string queryString = string.Format("Select distinct PdbID, spaceGroup, ASU From {0} Where GroupSeqID = {1};",
                //        GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupSeqId);
                      GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupSeqId);
            DataTable sgInterfaceTable = protcidQuery.Query(queryString);
            // in case, the entries with common interfaces with othere entries in the other groups, but in the same supergroup
            if (sgInterfaceTable.Rows.Count == 0)
            {
                queryString = string.Format("Select distinct PdbID, spaceGroup, ASU From {0} Where GroupSeqID = {1};",
                   GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupSeqId);
                sgInterfaceTable = protcidQuery.Query(queryString);
            }
            totalNumOfCfgsInSuperGroup += GetNumOfCFGsInFamily(sgInterfaceTable, entryCfGroupHash);
            totalNumOfEntryInSuperGroup += GetNumOfEntriesInFamily(groupSeqId, sgInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="supergroupId"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetSuperGroupFamilyArchChainHash(int supergroupId)
        {
            string supergroupFamilyString = GetSuperFamilyString(supergroupId);
            string[] familyArchFields = supergroupFamilyString.Split(';');
            Dictionary<string, string> supergroupFamilyArchChainHash = new Dictionary<string, string> ();
            for (int i = 0; i < familyArchFields.Length; i++)
            {
                if (!supergroupFamilyArchChainHash.ContainsKey(familyArchFields[i]))
                {
                    supergroupFamilyArchChainHash.Add(familyArchFields[i], chainNames[i].ToString ());
                }
            }
            return supergroupFamilyArchChainHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void FormatSuperGroupClusterSumInfoTable (int superGroupSeqId, int totalNumOfCFGsInSuperGroup, int totalNumOfEntriesInSuperGroup)
        {
            List<int> clusterIdList = new List<int> ();
            foreach (DataRow dataRow in interfaceStatData.clusterDataTable.Rows)
            {
                int clusterId = Convert.ToInt32(dataRow["ClusterId"].ToString());
                if (!clusterIdList.Contains(clusterId))
                {
                    clusterIdList.Add(clusterId);
                }
            }

            int numOfCfgInCluster = 0;
            double minSeqIdentity = 100.0;
            double seqIdentity = 0.0;
            double QminSeqId = 0.0;
            double QminSeqInterGroups = 0;
            double outMaxSeqId = 0.0;
            double maxSeqId = 0.0;
            string interfaceType = "";
            string clusterInterface = ""; // the interface with medium surface area in the cluster
            double clusterInterfaceSurfaceArea = 0;
            superGroupClusterSumTable.Clear();
            foreach (int clusterId in clusterIdList)
            {
                numOfCfgInCluster = 0;
                minSeqIdentity = 100.0;
                QminSeqId = 0.0;
                outMaxSeqId = 0.0;

                DataRow[] clusterRows = interfaceStatData.clusterDataTable.Select
                    (string.Format ("ClusterID = '{0}'", clusterId), "MediumSurfaceArea ASC");
                foreach (DataRow clusterRow in clusterRows)
                {
                    numOfCfgInCluster += Convert.ToInt32 (clusterRow["#CFG/Cluster"].ToString());
                    seqIdentity = Convert.ToDouble(clusterRow["MinSeqIdentity"].ToString ());
                    if (minSeqIdentity > seqIdentity && seqIdentity > -1.0)
                    {
                        minSeqIdentity = seqIdentity;
                        QminSeqId = Convert.ToDouble(clusterRow["Q(MinIdentity)"].ToString ());
                    }
                    maxSeqId = Convert.ToDouble(clusterRow["OutMaxSeqIdentity"].ToString ());
                    if (outMaxSeqId < maxSeqId)
                    {
                        outMaxSeqId = maxSeqId;
                    }
                }
                double minSeqIdentityInterGroups = 
                    GetMinimumSequenceIdentityInChainRelCluster(superGroupSeqId, clusterId, out QminSeqInterGroups);
                if (minSeqIdentity > minSeqIdentityInterGroups)
                {
                    minSeqIdentity = minSeqIdentityInterGroups;
                    QminSeqId = QminSeqInterGroups;
                }
                interfaceType = GetInterfaceTypeInCluster(clusterId);
                DataRow superClusterSumRow = superGroupClusterSumTable.NewRow();
                superClusterSumRow["SuperGroupSeqID"] = superGroupSeqId;
                superClusterSumRow["ClusterID"] = clusterId;
                superClusterSumRow["#CFG/Cluster"] = numOfCfgInCluster;
                superClusterSumRow["#CFG/Family"] = totalNumOfCFGsInSuperGroup;
                superClusterSumRow["#Entry/Family"] = totalNumOfEntriesInSuperGroup;
                superClusterSumRow["MinSeqIdentity"] = minSeqIdentity;
                superClusterSumRow["Q(MinIdentity)"] = QminSeqId;
                superClusterSumRow["OutMaxSeqIdentity"] = outMaxSeqId;
                superClusterSumRow["InterfaceType"] = interfaceType;
                clusterInterface = GetSuperClusterInterface(clusterRows, out clusterInterfaceSurfaceArea);
                superClusterSumRow["ClusterInterface"] = clusterInterface;
                superClusterSumRow["MediumSurfaceArea"] = clusterInterfaceSurfaceArea;
                superGroupClusterSumTable.Rows.Add(superClusterSumRow);
            }
            interfaceStatData.clusterDataTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRows"></param>
        /// <param name="clusterInterfaceArea"></param>
        /// <returns></returns>
        private string GetSuperClusterInterface(DataRow[] clusterRows, out double clusterInterfaceArea)
        {
            int mediumIndex = (int)(clusterRows.Length / 2);
            string clusterInterface = clusterRows[mediumIndex]["ClusterInterface"].ToString();
            clusterInterfaceArea = Convert.ToDouble (clusterRows[mediumIndex]["MediumSurfaceArea"].ToString ());
            return clusterInterface;
        }

        #region minimum sequence identity

        #region for debug
        public void UpdateMinSeqIdentityInDb()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Minimum Sequence Identity in cluster sum info table.");

            string queryString = "Select SuperGroupSeqID, ClusterID, MinSeqIdentity From PfamSuperClusterSumInfo Where MinSeqIdentity <= 10;";
            DataTable clusterTable = protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.totalOperationNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;

            int superGroupId = 0;
            int clusterId = 0;
            double minSeqQscore = 0;
            double minSeqId = 0;
            double orgMinSeqId = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString() + "_" + clusterId.ToString();

                minSeqId = GetMinimumSequenceIdentityInChainRelCluster(superGroupId, clusterId, out minSeqQscore);
                orgMinSeqId = Convert.ToDouble(clusterRow["MinSeqIdentity"].ToString());

                UpdateMinSeqIdentity(superGroupId, clusterId, minSeqId, minSeqQscore);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating the database summary file for the web site.");
            PrintChainDbSumInfo("PfamSuper");
        }


        private void UpdateMinSeqIdentity(int superGroupId, int clusterId, double minSeqId, double minSeqQscore)
        {
            string updateString = string.Format("Update PfamSuperClusterSumInfo " +
                " Set MinSeqIdentity = {0}, Q_MinIdentity = {1} " + 
                " Where SuperGroupSeqID = {2} AND ClusterID = {3};", 
                minSeqId, minSeqQscore, superGroupId, clusterId);
            protcidQuery.Query(updateString);
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private double GetMinimumSequenceIdentityInChainRelCluster(int superGroupId, int clusterId, out double minSeqQscore)
        {
            string queryString = string.Format("Select GroupSeqID, PdbId, InterfaceID From PfamSuperInterfaceClusters " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order by GroupSeqID, PdbID, InterfaceID;", superGroupId, clusterId);
            DataTable clusterEntryTable = protcidQuery.Query(queryString);
            Dictionary<int, List<string>> groupEntryInterfaceHash = new Dictionary<int, List<string>> ();
            int groupId = 0;
            string pdbId = "";
            minSeqQscore = 0;
            double groupMinSeqQscore = 0;
            List<string> addedEntryList = new List<string> ();
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                groupId = Convert.ToInt32(entryRow["GroupSeqID"].ToString ());
                pdbId = entryRow["PdbID"].ToString();
                if (addedEntryList.Contains (pdbId))
                {
                   continue; 
                }
                addedEntryList.Add (pdbId);
                if (groupEntryInterfaceHash.ContainsKey(groupId))
                {
                    groupEntryInterfaceHash[groupId].Add(entryRow["PdbID"].ToString() + entryRow["InterfaceId"].ToString());
                }
                else
                {
                    List<string> interfaceList = new List<string> ();
                    interfaceList.Add(entryRow["PdbID"].ToString() + entryRow["InterfaceId"].ToString());
                    groupEntryInterfaceHash.Add(groupId, interfaceList);
                }
            }
            double minSeqIdentity = GetMinimumSequenceIdentityBetweenGroups(groupEntryInterfaceHash, out minSeqQscore);
            double groupMinSeqIdentity = GetMinimumSequenceIdentityInGroups(groupEntryInterfaceHash, out groupMinSeqQscore);
            if (minSeqIdentity > groupMinSeqIdentity && groupMinSeqIdentity > 0)
            {
                minSeqIdentity = groupMinSeqIdentity;
                minSeqQscore = groupMinSeqQscore;
            }
            return minSeqIdentity;
        }
         /// <summary>
         /// 
         /// </summary>
         /// <param name="groupEntryHash"></param>
         /// <returns></returns>
        private double GetMinimumSequenceIdentityBetweenGroups(Dictionary<int, List<string>> groupEntryInterfaceHash, out double minSeqQscore)
        {
            List<int> groupList = new List<int> (groupEntryInterfaceHash.Keys);
            groupList.Sort();
            int[] groupIds = groupList.ToArray(); 
            string pdbId1 = "";
            int interfaceId1 = 0;
            string pdbId2 = "";
            int interfaceId2 = 0;
            double seqIdentity = 0;
            double minSeqIdentity = 100.0;
            minSeqQscore = 0;
            double qscore = 0;
            for (int i = 0; i < groupIds.Length; i++)
            {
                List<string> entryInterfaceList1 =groupEntryInterfaceHash[groupIds[i]];
                for (int j = i + 1; j < groupIds.Length; j++)
                {
                    List<string> entryInterfaceList2 = groupEntryInterfaceHash[groupIds[j]];
                    foreach (string entryInterface1 in entryInterfaceList1)
                    {
                        pdbId1 = entryInterface1.Substring (0, 4);
                        interfaceId1 = Convert.ToInt32 (entryInterface1.Substring (4, entryInterface1.Length - 4));
                        foreach (string entryInterface2 in entryInterfaceList2)
                        {
                            pdbId2 = entryInterface2.Substring(0, 4);
                            interfaceId2 = Convert.ToInt32(entryInterface2.Substring (4, entryInterface2.Length - 4));
                            seqIdentity = GetInterfaceCompIdentity(pdbId1, interfaceId1, pdbId2, interfaceId2, out qscore);
                            if (minSeqIdentity > seqIdentity && seqIdentity > -1.0)
                            {
                                minSeqIdentity = seqIdentity;
                                minSeqQscore = qscore;
                            }
                        } 
                    }
                }
            }
            return minSeqIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupInterfaces"></param>
        /// <param name="minSeqQscore"></param>
        /// <returns></returns>
        private double GetMinimumSequenceIdentityInGroups(Dictionary<int, List<string>> groupEntryInterfaceHash, out double minSeqQscore)
        {
            List<int> groupList = new List<int> (groupEntryInterfaceHash.Keys);
            groupList.Sort();
            double groupMinSeqIdentity = 0;
            minSeqQscore = 0.0;
            double minSeqIdentity = 100.0;
            double groupMinSeqQscore = 0.0;
            foreach (int groupId in groupList)
            {
                groupMinSeqIdentity = GetMinimumSequenceIdentityInGroups(groupEntryInterfaceHash[groupId].ToArray (), out groupMinSeqQscore);
                if (minSeqIdentity > groupMinSeqIdentity && groupMinSeqIdentity > -1.0)
                {
                    minSeqIdentity = groupMinSeqIdentity;
                    minSeqQscore = groupMinSeqQscore;
                }
            }
            return minSeqIdentity;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupInterfaces"></param>
        /// <param name="minSeqQscore"></param>
        /// <returns></returns>
        private double GetMinimumSequenceIdentityInGroups(string[] groupInterfaces, out double minSeqQscore)
        {          
            string pdbId1 = "";
            int interfaceId1 = 0;
            string pdbId2 = "";
            int interfaceId2 = 0;
            double seqIdentity = 0;
            double minSeqIdentity = 100.0;
            minSeqQscore = 0;
            double qscore = 0;
            for (int i = 0; i < groupInterfaces.Length; i++)
            {
                pdbId1 = groupInterfaces[i].Substring(0, 4);
                interfaceId1 = Convert.ToInt32(groupInterfaces[i].Substring(4, groupInterfaces[i].Length - 4));
                for (int j = i + 1; j < groupInterfaces.Length; j++)
                {
                    pdbId2 = groupInterfaces[j].Substring(0, 4);
                    interfaceId2 = Convert.ToInt32(groupInterfaces[j].Substring(4, groupInterfaces[j].Length - 4));
                    seqIdentity = GetInterfaceCompIdentity(pdbId1, interfaceId1, pdbId2, interfaceId2, out qscore);
                    if (minSeqIdentity > seqIdentity && seqIdentity > -1.0)
                    {
                        minSeqIdentity = seqIdentity;
                        minSeqQscore = qscore;
                    }
                }
            }
            return minSeqIdentity;
        } 

        /// <summary>
        /// recover sequence identity from the interface comparison data
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        private double GetInterfaceCompIdentity(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, out double qscore)
        {
            double seqIdentity = -1.0;
            qscore = -1.0;
            string queryString = string.Format("Select Qscore, Identity From DifEntryInterfaceComp " + 
                " Where (PdbID1 = '{0}' AND InterfaceID1 = {1} AND PdbID2 = '{2}' AND InterfaceID2 = {3}) OR " +
                " (PdbID2 = '{0}' AND InterfaceID2 = {1} AND PdbID1 = '{2}' AND InterfaceID1 = {3});",
                pdbId1, interfaceId1, pdbId2, interfaceId2);
            DataTable qscoreTable = protcidQuery.Query(queryString);
            if (qscoreTable.Rows.Count > 0)
            {
                seqIdentity = Convert.ToDouble(qscoreTable.Rows[0]["Identity"].ToString());
                qscore = Convert.ToDouble(qscoreTable.Rows[0]["Qscore"].ToString());
            }
   //         return seqIdentity * 100.0;
            return seqIdentity;
        }
        #endregion

        #region data print
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterWriter"></param>
        /// <param name="clusterSumWriter"></param>
        /// <param name="groupFamilyStringHash"></param>
        private void WriteStatDataToFile(int superGroupId, StreamWriter clusterWriter, StreamWriter clusterSumWriter, Dictionary<int, string> groupFamilyStringHash)
        {
            if (interfaceStatData.interfaceDataTable.Rows.Count == 0)
            {
                return;
            }
            string superGroupString = GetSuperGroupString(superGroupId);
            string groupFileName = DownloadableFileName.GetChainGroupTarGzFileName(superGroupId);
            string groupClusterFile = Path.Combine(resultDir, groupFileName + ".txt");
            StreamWriter groupClusterWriter = new StreamWriter(groupClusterFile);
            if (headerNames == null)
            {
                headerNames = GetHeaderNames();
                List<string> headerNameList = new List<string> (headerNames);
                headerNameList.Remove("SuperGroupSeqID");
                headerNameList.Remove("ClusterID");
                headerNameList.Insert(0, "ClusterID");
                headerNameList.Insert(0, "SuperGroupSeqID"); // put them before the other columns
                headerNames = new string[headerNameList.Count];
                headerNameList.CopyTo(headerNames);
            }

            // write data to file
            clusterWriter.WriteLine(FormatHeaderString());
            groupClusterWriter.WriteLine(FormatHeaderString());
            List<int> clusterList = new List<int> ();

     //       foreach (DataRow dRow in interfaceStatData.clusterDataTable.Rows)
            foreach (DataRow dRow in superGroupClusterSumTable.Rows)
            {
                clusterList.Add(Convert.ToInt32(dRow["ClusterID"].ToString()));
            }

            clusterList.Sort();
            string dataStream = "";
            string modifiedColName = "";
            foreach (int clusterId in clusterList)
            {
                DataRow sumInfoRow = interfaceStatData.clusterSumInfoTable.NewRow();
                dataStream = "";
                DataRow[] interfaceRows = interfaceStatData.interfaceDataTable.Select(string.Format("ClusterId = '{0}'", clusterId), "GroupSeqID, SpaceGroup, CrystForm, PdbID ASC");
                if (interfaceRows.Length == 0)
                {
                    continue;
                }
                List<string> clusterDistEntryList = new List<string> ();
                List<string> clusterNmrEntryList = new List<string> ();
                string line = "";
                double sumSurfaceArea = 0.0;
                int[] maxPdbBuNums = null;
                int[] maxPisaBuNums = null;
                string maxAsu = "";

                List<string> existAsuEntryList = new List<string>();
                List<string> existPdbEntryList = new List<string>();
                List<string> existPisaEntryList = new List<string>();

                int asaCount = 0;
                int groupId = 0;
                foreach (DataRow interfaceRow in interfaceRows)
                {
                    try
                    {
                        groupId = Convert.ToInt32(interfaceRow["GroupSeqID"].ToString());

                        if (interfaceRow["SpaceGroup"].ToString().Trim() == "NMR")
                        {
                            if (!clusterNmrEntryList.Contains(interfaceRow["PdbID"].ToString()))
                            {
                                clusterNmrEntryList.Add(interfaceRow["PdbID"].ToString());
                            }
                        }
                        line = "";
                        if (!clusterDistEntryList.Contains(interfaceRow["PdbID"].ToString()))
                        {
                            clusterDistEntryList.Add(interfaceRow["PdbID"].ToString());
                        }
                        if (interfaceRow["SurfaceArea"].ToString() != "" && interfaceRow["SurfaceArea"].ToString() != "-1")
                        {
                            sumSurfaceArea += Convert.ToDouble(interfaceRow["SurfaceArea"].ToString());
                            asaCount++;
                        }
               //         GetMaxCopyNumFromAsuBu(interfaceRow["ASU"].ToString(), ref maxAsuNums);
                        if (interfaceRow["ASU"].ToString().Length < maxAsu.Length)
                        {
                            maxAsu = interfaceRow["ASU"].ToString();
                        }
                        GetMaxCopyNumFromAsuBu(interfaceRow["PdbBu"].ToString(), ref maxPdbBuNums);
                        GetMaxCopyNumFromAsuBu(interfaceRow["PisaBu"].ToString(), ref maxPisaBuNums);

                        // entries where the interface exists
                        if (interfaceRow["InASU"].ToString() == "1")
                        {
                            if (!existAsuEntryList.Contains(interfaceRow["PdbID"].ToString()))
                            {
                                existAsuEntryList.Add(interfaceRow["PdbID"].ToString());
                            }
                        }
                        if (interfaceRow["InPdb"].ToString() == "1")
                        {
                            if (!existPdbEntryList.Contains(interfaceRow["PdbID"].ToString()))
                            {
                                existPdbEntryList.Add(interfaceRow["PdbID"].ToString());
                            }
                        }

                        if (interfaceRow["InPisa"].ToString() == "1")
                        {
                            if (!existPisaEntryList.Contains(interfaceRow["PdbID"].ToString()))
                            {
                                existPisaEntryList.Add(interfaceRow["PdbID"].ToString());
                            }
                        }
                        foreach (string colName in headerNames)
                        {
                            if (interfaceStatData.interfaceDataTable.Columns.Contains(colName))
                            {
                                if (colName.ToUpper() == "SUPERGROUPSEQID")
                                {
                                    line += superGroupString;
                                }
                                else if (colName.ToUpper() == "GROUPSEQID")
                                {
                                    line +=  groupFamilyStringHash[groupId];
                                }
                                else
                                {
                                    line += interfaceRow[colName].ToString();
                                }
                                line += "	";
                            }
                            else
                            {
                                line += "";
                                line += "	";
                            }
                        }
                        dataStream += line.TrimEnd('	');
                        dataStream += "\r\n";
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(ParseHelper.FormatDataRow(interfaceRow));
                        ProtCidSettings.logWriter.Flush();
                    }                 
                }

                double avgSurfaceArea = sumSurfaceArea / (double)(asaCount);

                DataRow[] clusterSummaryRows = superGroupClusterSumTable.Select
                    (string.Format("ClusterID = '{0}'", clusterId));

                line = "";
                string sumLine = superGroupString + "	";
                foreach (string colName in headerNames)
                {
                //    if (interfaceStatData.clusterDataTable.Columns.Contains(colName))
                    if (superGroupClusterSumTable.Columns.Contains(colName))
                    {
                        if (colName.ToUpper() == "SUPERGROUPSEQID")
                        {
                            line = clusterSummaryRows[0][colName].ToString() + line;
                        }
                        else
                        {
                            line += clusterSummaryRows[0][colName].ToString();
                        }
                        line += "\t";
                        sumLine += clusterSummaryRows[0][colName].ToString();
                        if (colName.IndexOf("#") > -1 || colName.IndexOf("/") > -1 || colName.IndexOf("(") > -1)
                        {
                            modifiedColName = colName.Replace("#", "NumOf");
                            modifiedColName = modifiedColName.Replace("/", "");
                            modifiedColName = modifiedColName.Replace("(", "_");
                            modifiedColName = modifiedColName.Replace(")", "");
                        }
                        else
                        {
                            modifiedColName = colName;
                        }
                        sumInfoRow[modifiedColName] = clusterSummaryRows[0][colName];
                        sumLine += "	";
                    }
                    else
                    {
                        switch (colName.ToUpper())
                        {
                            case "SURFACEAREA":
                                line += string.Format("{0:0.##}", avgSurfaceArea);
                                line += "	";
                                sumLine += string.Format("{0:0.##}", avgSurfaceArea);
                                sumInfoRow["SurfaceArea"] = avgSurfaceArea;
                                sumLine += "	";
                                break;

                            case "INASU":
                                line += existAsuEntryList.Count;
                                line += "	";
                                sumLine += existAsuEntryList.Count;
                                sumInfoRow["InASU"] = existAsuEntryList.Count;
                                sumLine += "	";
                                break;
                            case "INPDB":
                                line += existPdbEntryList.Count;
                                line += "	";
                                sumLine += existPdbEntryList.Count;
                                sumInfoRow["InPDB"] = existPdbEntryList.Count;
                                sumLine += "	";
                                break;

                            case "INPISA":
                                line += existPisaEntryList.Count;
                                line += "	";
                                sumLine += existPisaEntryList.Count;
                                sumInfoRow["InPISA"] = existPisaEntryList.Count;
                                sumLine += "	";
                                break;

                            case "ASU":
                         /*       line += FormatMaxAsuBuString(maxAsuNums);
                                sumLine += FormatMaxAsuBuString(maxAsuNums);
                                sumInfoRow["MaxASU"] = FormatMaxAsuBuString(maxAsuNums);*/
                                line += maxAsu;
                                sumLine += maxAsu;
                                sumInfoRow["MaxASU"] = maxAsu;
                                line += "	";
                                sumLine += "	";
                                break;

                            case "PDBBU":
                                line += FormatMaxAsuBuString(maxPdbBuNums);
                                sumLine += FormatMaxAsuBuString(maxPdbBuNums);
                                sumInfoRow["MaxPDBBU"] = FormatMaxAsuBuString(maxPdbBuNums);
                                line += "	";
                                sumLine += "	";
                                break;

                            case "PISABU":
                                line += FormatMaxAsuBuString(maxPisaBuNums);
                                sumLine += FormatMaxAsuBuString(maxPisaBuNums);
                                sumInfoRow["MaxPISABU"] = FormatMaxAsuBuString(maxPisaBuNums);
                                line += "	";
                                sumLine += "	";
                                break;

                            case "#ENTRY/CLUSTER":
                                line += clusterDistEntryList.Count;
                                line += "	";
                                sumLine += clusterDistEntryList.Count;
                                sumInfoRow["NumOfEntryCluster"] = clusterDistEntryList.Count;
                                sumLine += "	";
                                break;

                            default:
                                line += "";
                                line += "	";
                                break;
                        }
                    }
                }// finish one cluster
                dataStream = line.TrimEnd('	') + "\r\n" + dataStream;
                clusterWriter.WriteLine(dataStream);
                groupClusterWriter.WriteLine(dataStream);
                // add summary data into line
                // ratio #SG/Cluster and #SG/Family
                sumLine += string.Format("{0:0.###}", Convert.ToDouble(clusterSummaryRows[0]["#CFG/Cluster"].ToString()) /
                    Convert.ToDouble(clusterSummaryRows[0]["#CFG/Family"].ToString()));
                sumLine += "	";

                // ratio #ASU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (double)(existAsuEntryList.Count - clusterNmrEntryList.Count) /
                    (double)(clusterDistEntryList.Count - clusterNmrEntryList.Count));
                sumLine += "	";
                // ratio #PDBBU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (double)(existPdbEntryList.Count - clusterNmrEntryList.Count) /
                    (double)(clusterDistEntryList.Count - clusterNmrEntryList.Count));
                sumLine += "	";
                // ratio #PISABU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (double)(existPisaEntryList.Count) /
                    (double)(clusterDistEntryList.Count - clusterNmrEntryList.Count));
                sumLine += "	";
                // #NMR entries/Cluster
                sumLine += clusterNmrEntryList.Count.ToString();
                sumInfoRow["NumOfNmr"] = clusterNmrEntryList.Count;
                clusterSumWriter.WriteLine(sumLine);

                // add the summary info row into the table
                interfaceStatData.clusterSumInfoTable.Rows.Add(sumInfoRow);
            }
            // insert data into database
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.clusterSumInfoTable);
            AddSuperGroupSeqIDToTable(superGroupId);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.interfaceDataTable);
            // then clear for next group
            interfaceStatData.Clear();
            // output results to a group file for the web server
            groupClusterWriter.Close();
            ParseHelper.ZipPdbFile(groupClusterFile);
        }

        private void WriteClusterQscoresToFile(int superGroupId, int clusterId, StreamWriter groupClusterWriter)
        {
            DataRow[] interfaceRows = interfaceStatData.interfaceDataTable.Select(string.Format("ClusterId = '{0}'", clusterId));
            List<string> interfaceList = new List<string>();
            string entryInterfaceId = "";
            foreach (DataRow interfaceRow in interfaceRows)
            {
                entryInterfaceId = interfaceRow["PdbID"].ToString() + interfaceRow["InterfaceID"].ToString();
                interfaceList.Add(entryInterfaceId);
            }
            interfaceList.Sort();
            string dataLine = "";
            foreach (string entryInterface in interfaceList)
            {
                dataLine += (entryInterface + "\t");
            }
            groupClusterWriter.WriteLine(dataLine.TrimEnd ('\t'));
            double qscore = 0;
            Dictionary<string, double> interfaceQscoreHash = new Dictionary<string,double> ();
            foreach (string entryInterface1 in interfaceList)
            {
                foreach (string entryInterface2 in interfaceList)
                {
                    qscore = GetQscoreBetweenInterfaces(entryInterface1, entryInterface2);
                    interfaceQscoreHash.Add(entryInterface1 + "_" + entryInterface2, qscore);
                }
            }
        }


        private double GetQscoreBetweenInterfaces(string entryInterface1, string entryInterface2)
        {
            string entry1 = entryInterface1.Substring(0, 4);
            string interfaceId1 = entryInterface1.Substring(4, entryInterface1.Length - 4);
            string entry2 = entryInterface2.Substring(0, 4);
            string interfaceId2 = entryInterface2.Substring(4, entryInterface2.Length - 4);
            DataRow[] qscoreRows = interfaceStatData.clusterInterfaceCompTable.Select
                (string.Format ("PdbID1='{0}' AND InterfaceID1='{1}' AND PdbID2='{2}' AND InterfaceID2='{3}'",
                 entry1, interfaceId1, entry2, interfaceId2));
            if (qscoreRows.Length == 0)
            {
                qscoreRows = interfaceStatData.clusterInterfaceCompTable.Select
                    (string.Format("PdbID1='{0}' AND InterfaceID1='{1}' AND PdbID2='{2}' AND InterfaceID2='{3}'",
                    entry2, interfaceId2, entry1, interfaceId1));
            }
            if (qscoreRows.Length > 0)
            {
                return Convert.ToDouble (qscoreRows[0]["Qscore"].ToString ());
            }
            return GetQscoreFromDb(entry1, Convert.ToInt32(interfaceId1), entry2, Convert.ToInt32(interfaceId2));
            
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        private double GetQscoreFromDb(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2)
        {
            string queryString = "";
            if (pdbId1 == pdbId2)
            {
                queryString = string.Format("Select * From EntryInterfaceComp " + 
                    " Where PdbID = '{0}' AND InterfaceID1 = {1} AND InterfaceID2 = {2};",
                    pdbId1, interfaceId1, interfaceId2);
            }
            else
            {
                queryString = string.Format("Select QScore From DifEntryInterfaceComp " + 
                    " Where PdbID1 = '{0}' AND InterfaceID1 = {1} AND " + 
                    " PdbID2 = '{2}' AND InterfaceID2 = {3};", 
                    pdbId1, interfaceId1, pdbId2, interfaceId2);
            }
            DataTable interfaceCompTable = protcidQuery.Query(queryString);
            if (interfaceCompTable.Rows.Count == 0)
            {
                if (pdbId1 == pdbId2)
                {
                    queryString = string.Format("Select * From EntryInterfaceComp " +
                        " Where PdbID = '{0}' AND InterfaceID1 = {1} AND InterfaceID2 = {2};",
                        pdbId1, interfaceId2, interfaceId1);
                }
                else
                {
                    queryString = string.Format("Select QScore From DifEntryInterfaceComp " +
                        " Where PdbID1 = '{0}' AND InterfaceID1 = {1} AND " +
                        " PdbID2 = '{2}' AND InterfaceID2 = {3};",
                        pdbId2, interfaceId2, pdbId1, interfaceId1);
                }
                interfaceCompTable = protcidQuery.Query(queryString);
            }
            if (interfaceCompTable.Rows.Count > 0)
            {
                return Convert.ToDouble(interfaceCompTable.Rows[0]["QScore"].ToString ());
            }
            return -1.0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        private void AddSuperGroupSeqIDToTable(int superGroupSeqId)
        {
            foreach (DataRow clusterInterfaceRow in interfaceStatData.interfaceDataTable.Rows)
            {
                clusterInterfaceRow["SuperGroupSeqID"] = superGroupSeqId;
            }
            interfaceStatData.interfaceDataTable.AcceptChanges();
        }
        /// <summary>
        /// 
        /// </summary>
        private void UpdateClusterSumInfoTableSeqIdColumn()
        {
            DataColumn groupSeqIdCol = interfaceStatData.clusterSumInfoTable.Columns["GroupSeqID"];
            groupSeqIdCol.ColumnName = "SuperGroupSeqID";
            interfaceStatData.clusterSumInfoTable.AcceptChanges();
        }

        private void UpdateSeqIdColumnBack()
        {
            DataColumn groupSeqIdCol = interfaceStatData.clusterSumInfoTable.Columns["SuperGroupSeqID"];
            groupSeqIdCol.ColumnName = "GroupSeqID";
            interfaceStatData.clusterSumInfoTable.AcceptChanges();
        }
        #endregion

        #region supergroup info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIds"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetGroupFamilyStringHash(int[] groupIds)
        {
            Dictionary<int, string> groupFamilyStringHash = new Dictionary<int,string> ();
            string groupFamilyString = "";
            foreach (int GroupSeqID in groupIds)
            {
                groupFamilyString = GetFamilyString(GroupSeqID);
                groupFamilyStringHash.Add(GroupSeqID, groupFamilyString);
            }
            return groupFamilyStringHash;
        }
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <returns></returns>
        private DataTable GetSuperGroupClusterInfo(int superGroupId, int GroupSeqID)
        {
            string queryString = string.Format("Select * From {0} " + 
                " Where SuperGroupSeqID = {1} AND GroupSeqID = {2};",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters], superGroupId, GroupSeqID);
            DataTable groupClusterInfoTable = protcidQuery.Query(queryString);
            return groupClusterInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupSeqIDs(int superGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID  From {0} Where SuperGroupSeqID = {1};",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters], superGroupId);
            DataTable groupIdTable = protcidQuery.Query(queryString);
            List<int> groupList = new List<int> ();
            int groupId = 0;
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());

                groupList.Add(groupId);
            }
            return groupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private bool IsSupergroupDuplicate(string familyString)
        {
            string[] archFields = familyString.Split(';');
            if (archFields.Length == 1)
            {
                return false;
            }
            List<string> distinctFamilyList = new List<string> ();
            foreach (string archField in archFields)
            {
                if (!distinctFamilyList.Contains(archField))
                {
                    distinctFamilyList.Add(archField);
                }
            }
            if (distinctFamilyList.Count == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetSuperFamilyString(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyStringTable = protcidQuery.Query(queryString);
            return familyStringTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetSuperGroupString(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyStringTable = protcidQuery.Query(queryString);
            return familyStringTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
        }
        #endregion

        #region initialize tables
        /// <summary>
        /// 
        /// </summary>
        private void InitializeTables(string type, bool isUpdate)
        {
            interfaceStatData = new InterfaceStatData(type);
            interfaceStatData.interfaceDataTable.Columns.Add(new DataColumn("SuperGroupSeqID"));
            superGroupClusterSumTable = interfaceStatData.clusterDataTable.Clone();
            superGroupClusterSumTable.Columns.Remove("GroupSeqID");
            superGroupClusterSumTable.Columns.Add(new DataColumn("SuperGroupSeqID"));
            interfaceStatData.clusterSumInfoTable.Columns.Add(new DataColumn("SuperGroupSeqID"));
            interfaceStatData.clusterSumInfoTable.Columns.Remove("GroupSeqID");
            // initialize tables in db
            if (!isUpdate)
            {
                interfaceStatData.InitializeSumInfoTablesInDb(type);
            }
        }
        #endregion


        #region update 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroups"></param>
        /// <param name="type"></param>
        /// <param name="sumInfoNeedUpdate"></param>
        public void UpdateSupergroupInterfaceClustersSumInfo (int[] updateSuperGroups, string type, bool sumInfoNeedUpdate)
        {
            InitializeTables(type + "Super", false);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"), true);           

            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
            ProtCidSettings.progressInfo.totalOperationNum = updateSuperGroups.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateSuperGroups.Length;
            Array.Sort (updateSuperGroups);
            foreach (int supergroupId in updateSuperGroups)
            {
                sgInterfaceNumHash.Clear();
                superGroupClusterSumTable.Clear();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupId.ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = supergroupId.ToString();

                try
                {
                    DeleteObsData(supergroupId);
                    PrintGroupClusterStatInfo(supergroupId, clusterWriter, clusterSumWriter, type);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupId.ToString() + " Cluster stat info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(supergroupId.ToString() + " Cluster stat info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                clusterSumWriter.Flush();
                clusterWriter.Flush();
            }
            clusterWriter.Close();
            clusterSumWriter.Close();

            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"));
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"));

            if (sumInfoNeedUpdate)
            {
                UpdateProtCidChainMetaData(type, updateSuperGroups);
            }
#if DEBUG
            logWriter.Close();
#endif
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public void UpdateSupergroupInterfaceClustersSumInfo (int[] updateSuperGroups, string[] updateEntries, string type, bool sumInfoNeedUpdate)
        {
            InitializeTables(type + "Super", true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"), true);

            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
            ProtCidSettings.logWriter.WriteLine("Update cluster stat info");
  //          ProtCidSettings.logWriter.WriteLine("Super Group ID > 20000");
            ProtCidSettings.progressInfo.totalOperationNum = updateSuperGroups.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateSuperGroups.Length;

            foreach (int supergroupId in updateSuperGroups)
            {
                sgInterfaceNumHash.Clear();
                superGroupClusterSumTable.Clear();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = supergroupId.ToString();      
                try
                {
                    DeleteObsData(supergroupId);
                    PrintGroupClusterStatInfo(supergroupId, clusterWriter, clusterSumWriter, type);

                    clusterSumWriter.Flush();
                    clusterWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupId.ToString () + " Cluster Stat Error: " + ex.Message );
                    ProtCidSettings.logWriter.WriteLine(supergroupId.ToString() + " Cluster Stat Error: " + ex.Message);
                }
                ProtCidSettings.logWriter.WriteLine(supergroupId.ToString());
                ProtCidSettings.logWriter.Flush();
            }
            //	DbBuilder.dbConnect.DisconnectFromDatabase ();
            clusterWriter.Close();
            clusterSumWriter.Close();
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"));
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"));

            if (sumInfoNeedUpdate)
            {
                UpdateProtCidChainMetaData(type, updateEntries, updateSuperGroups);
            }
            ProtCidSettings.logWriter.WriteLine("Update cluster stat info done!");
            ProtCidSettings.logWriter.Flush();     
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="updateEntries"></param>
        /// <param name="updateSuperGroups"></param>
        public void UpdateProtCidChainMetaData (string type, string[] updateEntries, int[] updateSuperGroups)
        {
            GenerateProtCidMetaData(type);

            // PfamChainArchRelation: the pair of chain pfam arch for each entry
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Chain Pfam arch relation for each entry");
            UpdateChainInterfacePfamArchRel(updateEntries);

            // PfamChainPairInPdb: summary data for each pair of chain pfam arch
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("summary info for each pair of chain pfam archs");
            UpdateChainArchPairsMetaData(updateSuperGroups);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="updateEntries"></param>
        /// <param name="updateSuperGroups"></param>
        public void UpdateProtCidChainMetaData(string type, int[] updateSuperGroups)
        {
            GenerateProtCidMetaData(type);

            // PfamChainPairInPdb: summary data for each pair of chain pfam arch
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("summary info for each pair of chain pfam archs");
            UpdateChainArchPairsMetaData(updateSuperGroups);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        private void DeleteObsData(int superGroupId)
        {
            string deleteString = string.Format("Delete From {0} Where SuperGroupSeqID = {1};",
                interfaceStatData.clusterSumInfoTable.TableName, superGroupId);
            protcidQuery.Query(deleteString);

            deleteString = string.Format("Delete From {0} Where SuperGroupSeqID = {1};", 
                interfaceStatData.interfaceDataTable.TableName, superGroupId);
            protcidQuery.Query(deleteString);
        }
        #endregion

        #region heterodimer
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public void PrintSpecificSupergroupInterfaceClusters(string type)
        {
            interfaceStatData = new InterfaceStatData(type);
            interfaceStatData.interfaceDataTable.Columns.Add(new DataColumn("SuperGroupSeqID"));

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            string resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_hetero.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_hetero.txt"), true);

            StreamReader dataReader = new StreamReader("DifPfamArchClusterList.txt");
            Dictionary<int, List<int>> superGroupClusterHash = new Dictionary<int,List<int>> ();
            int superGroupId = 0;
            int clusterId = 0;
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                superGroupId = Convert.ToInt32(fields[0]);
                clusterId = Convert.ToInt32(fields[1]);
                if (superGroupClusterHash.ContainsKey(superGroupId))
                {
                    superGroupClusterHash[superGroupId].Add(clusterId);
                }
                else
                {
                    List<int> clusterList = new List<int> ();
                    clusterList.Add(clusterId);
                    superGroupClusterHash.Add(superGroupId, clusterList);
                }
            }
            dataReader.Close();

            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
            ProtCidSettings.progressInfo.totalOperationNum = superGroupClusterHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = superGroupClusterHash.Count;
            List<int> superGroupList = new List<int> (superGroupClusterHash.Keys);
            superGroupList.Sort();
            foreach (int thisSupergroupId in superGroupList)
            {
                sgInterfaceNumHash.Clear();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = thisSupergroupId.ToString();
         //       ArrayList clusterList = (ArrayList)superGroupClusterHash[thisSupergroupId];

       //         PrintGroupClusterStatInfo(thisSupergroupId,  clusterList, clusterWriter, clusterSumWriter, type);
                PrintGroupClusterStatInfo(thisSupergroupId, clusterWriter, clusterSumWriter, type);
                clusterSumWriter.Flush();
                clusterWriter.Flush();
            }
            //	DbBuilder.dbConnect.DisconnectFromDatabase ();
            clusterWriter.Close();
            clusterSumWriter.Close();

 //           ProtCidSettings.progressInfo.progStrQueue.Enqueue("Divide the cluster info file into smaller files.");
  //          DivideChainInterfaceResultOutputFile(type + "SuperChainInterfaceClusterInfo");
#if DEBUG
            logWriter.Close();
#endif
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <param name="clusterWriter"></param>
        /// <param name="clusterSumWriter"></param>
        /// <param name="type"></param>
        private void PrintGroupClusterStatInfo(int supergroupId, List<int> clusterIds, StreamWriter clusterWriter, StreamWriter clusterSumWriter, string type)
        {
            int[] groupIds = GetGroupSeqIDs(supergroupId);
            int totalNumOfEntryInSuperGroup = 0;
            int totalNumOfCfgsInSuperGroup = 0;
            foreach (int groupSeqId in groupIds)
            {
                DataTable clusterTable = GetSuperGroupClusterInfo(supergroupId, groupSeqId, clusterIds);
                List<string> pdbList = new List<string> ();
                Dictionary<int, List<string>> groupEntryHash = new Dictionary<int,List<string>> ();
                foreach (DataRow clusterRow in clusterTable.Rows)
                {
                    if (!pdbList.Contains(clusterRow["PdbID"].ToString()))
                    {
                        pdbList.Add(clusterRow["PdbID"].ToString());
                    }
                }
                if (pdbList.Count == 0)
                {
                    continue;
                }
                // cluster info	
                DataTable interfaceTable = GetInterfaceTable(groupSeqId, pdbList);
                DataTable entityInfoTable = GetEntityInfoTable(pdbList);
                Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);
                UpdateSuperCfGroupId(supergroupId, groupSeqId, entryCfGroupHash);

                // get the number of space group in the family
                string queryString = string.Format("Select distinct PdbID, spaceGroup, ASU From {0} Where GroupSeqID = {1};",
                 //       GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupSeqId);
                    GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupSeqId);
                DataTable sgInterfaceTable = protcidQuery.Query(queryString);
                totalNumOfCfgsInSuperGroup += GetNumOfCFGsInFamily(sgInterfaceTable, entryCfGroupHash);
                totalNumOfEntryInSuperGroup += GetNumOfEntriesInFamily(groupSeqId, sgInterfaceTable);

                pdbGroupEntryBuAbcFormatHash.Clear();
                pisaGroupEntryBuAbcFormatHash.Clear();
                groupEntryEntityChainNameHash.Clear();

                Dictionary<string, string> supergroupFamilyArchChainHash = GetSuperGroupFamilyArchChainHash(supergroupId);
                string[] repEntries = new string[pdbList.Count];
                pdbList.CopyTo(repEntries);
                SetGroupEntryEntityChainNameHash(repEntries, supergroupFamilyArchChainHash);
                string[] homoEntries = GetGroupHomoEntries(groupSeqId, type);
                SetGroupEntryEntityChainNameHash(homoEntries, supergroupFamilyArchChainHash);

                FormatGroupClusterInfoIntoTable(groupSeqId, clusterTable, interfaceTable,
                    entityInfoTable, entryCfGroupHash);
                // interfaces in BUs of homologous entries in a space group
                FormatRepInterfacesInHomoBUs(groupSeqId, clusterTable, entryCfGroupHash);

                // interface qscore in a cluster
                DataTable groupInterfaceCompTable = GetGroupInterfaceCompTable(pdbList);
                DataTable sgInterfaceCompTable = GetInterfaceOfSgCompTable(pdbList);
                DataTable groupRepAlignTable = GetGroupRepAlignTable(groupSeqId, pdbList);

                FormatClusterSumInfoIntoTable(groupSeqId, clusterTable, groupInterfaceCompTable,
                    sgInterfaceCompTable, groupRepAlignTable, entryCfGroupHash);
            }
            FormatSuperGroupClusterSumInfoTable (supergroupId, totalNumOfCfgsInSuperGroup, totalNumOfEntryInSuperGroup);
            Dictionary<int, string> groupFamilyStringHash = GetGroupFamilyStringHash(groupIds);
            WriteStatDataToFile(supergroupId, clusterWriter, clusterSumWriter, groupFamilyStringHash);
        }

        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <returns></returns>
        private DataTable GetSuperGroupClusterInfo(int superGroupId, int GroupSeqID, List<int> clusterIds)
        {
            string queryString = string.Format("Select * From {0} " +
                " Where SuperGroupSeqID = {1} AND GroupSeqID = {2} AND ClusterID IN ({3});",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters], superGroupId, GroupSeqID,
                ParseHelper.FormatSqlListString(clusterIds.ToArray ()));
            DataTable groupClusterInfoTable = protcidQuery.Query(queryString);
            return groupClusterInfoTable;
        }
        #endregion

        #region PFAM In PDB for Browse of Web server
        #region pfam meta data
        public void GenerateProtCidMetaData(string type)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("The Statistical Info");
            PrintFamilyCfgSumInfo();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating the database summary file for the web site.");
            PrintChainDbSumInfo(type + "Super");

            PrintPfamFamilyGroupClusterInfo(type + "Super");
        }

        #region summary info
        /// <summary>
        /// 
        /// </summary>
        public void PrintFamilyCfgSumInfo()
        {
            string queryString = "Select SuperGroupSeqID, Count(SuperCfGroupId) As NumOfCfgs From PfamSuperCfGroups Group By SuperGroupSeqID;";
            DataTable groupCfgTable = protcidQuery.Query(queryString);
            Dictionary<int, int> groupCfgHash = new Dictionary<int,int> ();
            int superGroupId = 0;
            int numOfCfgs = 0;
            foreach (DataRow cfgRow in groupCfgTable.Rows)
            {
                superGroupId = Convert.ToInt32(cfgRow["SuperGroupSeqID"].ToString());
                numOfCfgs = Convert.ToInt32(cfgRow["NumOfCfgs"].ToString());
                if (groupCfgHash.ContainsKey(numOfCfgs))
                {
                    int numOfGroups = groupCfgHash[numOfCfgs];
                    numOfGroups++;
                    groupCfgHash[numOfCfgs] = numOfGroups;
                }
                else
                {
                    groupCfgHash.Add(numOfCfgs, 1);
                }
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine(resultDir, "GroupCfgSumInfo.txt"));
            foreach (int numOfCfg in groupCfgHash.Keys)
            {
                dataWriter.WriteLine(numOfCfg.ToString() + "\t" + groupCfgHash[numOfCfg].ToString());
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintChainDbSumInfo(string type)
        {
            if (interfaceStatData == null)
            {
                interfaceStatData = new InterfaceStatData(type);
            }
            interfaceStatData.InitializeStatInfoTable(type);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster summary info");

            StreamWriter dataWriter = new StreamWriter("ChainInterfaceDbStatInfo.txt");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Group Sum Info.");
            GetGroupSumInfo(dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 2");
            GetClusterSumInfo(2, 101, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 2, SeqID <= 90");
            GetClusterSumInfo(2, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 5, SeqID <= 90");
            GetClusterSumInfo(5, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 10, SeqID <= 90");
            GetClusterSumInfo(10, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 20, SeqID <= 90");
            GetClusterSumInfo(20, 90, dataWriter);

            dataWriter.Close();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.dbStatInfoTable);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataWriter"></param>
        private void GetGroupSumInfo(StreamWriter dataWriter)
        {
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable superGroupTable = protcidQuery.Query(queryString);

            int numOfSingleGroups = 0;
            int numOfDoubleGroups = 0;
            List<string> singleEntryList = new List<string> ();
            List<string> doubleEntryList = new List<string> ();

            int superGroupId = 0;
            string groupString = "";
            foreach (DataRow groupRow in superGroupTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());
                groupString = groupRow["ChainRelPfamArch"].ToString().TrimEnd();
                string[] groupEntries = GetGroupEntries(superGroupId);
                if (IsSinglePfamArchGroup(groupString))
                {
                    numOfSingleGroups++;
                    foreach (string entry in groupEntries)
                    {
                        if (!singleEntryList.Contains(entry))
                        {
                            singleEntryList.Add(entry);
                        }
                    }
                }
                else
                {
                    numOfDoubleGroups++;
                    foreach (string entry in groupEntries)
                    {
                        if (!doubleEntryList.Contains(entry))
                        {
                            doubleEntryList.Add(entry);
                        }
                    }
                }
            }
            dataWriter.WriteLine("# single pfam arch groups: " + numOfSingleGroups.ToString());
            dataWriter.WriteLine("# double pfam arch groups: " + numOfDoubleGroups.ToString());
            int totalNumGroups = numOfSingleGroups + numOfDoubleGroups;
            dataWriter.WriteLine("# both pfam arch groups: " + totalNumGroups.ToString());

            dataWriter.WriteLine("# entries single pfam arch: " + singleEntryList.Count.ToString());
            dataWriter.WriteLine("# entries double pfam arch: " + doubleEntryList.Count.ToString());
            int totalNumEntries = GetDistinctEntryList(singleEntryList, doubleEntryList);
            dataWriter.WriteLine("# total entries both pfam arch: " + totalNumEntries.ToString());

            dataWriter.Flush();

            DataRow statInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            statInfoRow["Category"] = "#Groups";
            statInfoRow["Single"] = numOfSingleGroups;
            statInfoRow["Pair"] = numOfDoubleGroups;
            statInfoRow["Total"] = totalNumGroups;
            interfaceStatData.dbStatInfoTable.Rows.Add(statInfoRow);

            DataRow entryInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            entryInfoRow["Category"] = "#Entries";
            entryInfoRow["Single"] = singleEntryList.Count;
            entryInfoRow["Pair"] = doubleEntryList.Count;
            entryInfoRow["Total"] = totalNumEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(entryInfoRow);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetGroupEntries(int superGroupId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamSuperGroups " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable groupIdTable = protcidQuery.Query(queryString);
            List<int> groupIdList = new List<int> ();
            int groupId = -1;
            foreach (DataRow groupRow in groupIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupRow["GroupSeqID"].ToString());
                groupIdList.Add(groupId);
            }
            queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo " +
                " Where GroupSeqID IN ({0});", ParseHelper.FormatSqlListString(groupIdList.ToArray ()));
            DataTable repEntryTable = protcidQuery.Query(queryString);
            List<string> groupEntryList = new List<string> ();
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID"].ToString());
            }

            queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign " +
                " Where GroupSeqID IN ({0});", ParseHelper.FormatSqlListString(groupIdList.ToArray ()));
            DataTable homoEntryTable = protcidQuery.Query(queryString);
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID2"].ToString());
            }
            return groupEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Mcutoff"></param>
        /// <param name="seqIdCutoff"></param>
        /// <param name="dataWriter"></param>
        private void GetClusterSumInfo(int Mcutoff, int seqIdCutoff, StreamWriter dataWriter)
        {
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable superGroupTable = protcidQuery.Query(queryString);

            int numOfSingleGroups = 0;
            int numOfDoubleGroups = 0;
            int numOfSingleClusters = 0;
            int numOfDoubleClusters = 0;
            List<string> singleEntryList = new List<string> ();
            List<string> doubleEntryList = new List<string>();
            List<string> singlePdbBuEntryList = new List<string>();
            List<string> doublePdbBuEntryList = new List<string>();
            List<string> singlePisaBuEntryList = new List<string>();
            List<string> doublePisaBuEntryList = new List<string>();

            int superGroupId = 0;
            string entryArchString = "";
            bool isSingle = false;
            foreach (DataRow superGroupRow in superGroupTable.Rows)
            {
                entryArchString = superGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());

                isSingle = IsSinglePfamArchGroup(entryArchString);

                int[] clustersMGreater = GetClustersMGreater(superGroupId, Mcutoff, seqIdCutoff);
                string[] entriesInClusters = GetDistinctEntryInClusters(superGroupId, clustersMGreater);
                string[] pdbBuEntriesInClusters = GetDistinctBuEntryWithClusterInterfaces(superGroupId, clustersMGreater, "pdb");
                string[] pisaBuEntriesInClusters = GetDistinctBuEntryWithClusterInterfaces(superGroupId, clustersMGreater, "pisa");

                if (isSingle)
                {
                    foreach (string entryInCluster in entriesInClusters)
                    {
                        if (!singleEntryList.Contains(entryInCluster))
                        {
                            singleEntryList.Add(entryInCluster);
                        }
                    }
                    foreach (string entry in pdbBuEntriesInClusters)
                    {
                        if (!singlePdbBuEntryList.Contains(entry))
                        {
                            singlePdbBuEntryList.Add(entry);
                        }
                    }
                    foreach (string entry in pisaBuEntriesInClusters)
                    {
                        if (!singlePisaBuEntryList.Contains(entry))
                        {
                            singlePisaBuEntryList.Add(entry);
                        }
                    }

                    numOfSingleClusters += clustersMGreater.Length;
                    if (clustersMGreater.Length > 0)
                    {
                        numOfSingleGroups++;
                    }
                }
                else
                {
                    foreach (string entryInCluster in entriesInClusters)
                    {
                        if (!doubleEntryList.Contains(entryInCluster))
                        {
                            doubleEntryList.Add(entryInCluster);
                        }
                    }
                    foreach (string entry in pdbBuEntriesInClusters)
                    {
                        if (!doublePdbBuEntryList.Contains(entry))
                        {
                            doublePdbBuEntryList.Add(entry);
                        }
                    }
                    foreach (string entry in pisaBuEntriesInClusters)
                    {
                        if (!doublePisaBuEntryList.Contains(entry))
                        {
                            doublePisaBuEntryList.Add(entry);
                        }
                    }
                    numOfDoubleClusters += clustersMGreater.Length;
                    if (clustersMGreater.Length > 0)
                    {
                        numOfDoubleGroups++;
                    }
                }
            }

            dataWriter.WriteLine("M = " + Mcutoff.ToString() + "    SeqIdentity = " + seqIdCutoff.ToString());
            dataWriter.WriteLine("# single pfam arch groups M>=" + Mcutoff.ToString() + ": " + numOfSingleGroups.ToString());
            dataWriter.WriteLine("# pair wise pfam arch groups M>=" + Mcutoff.ToString() + ": " + numOfDoubleGroups.ToString());
            int totalNumGroups = numOfSingleGroups + numOfDoubleGroups;
            dataWriter.WriteLine("# both pfam arch groups M>=" + Mcutoff.ToString() + ": " + totalNumGroups.ToString());

            dataWriter.WriteLine("# single pfam arch clusters M>=" + Mcutoff.ToString() + ": " + numOfSingleClusters.ToString());
            dataWriter.WriteLine("# double pfam arch clusters M>=" + Mcutoff.ToString() + ": " + numOfDoubleClusters.ToString());
            int tatalNumClusters = numOfSingleClusters + numOfDoubleClusters;
            dataWriter.WriteLine("# both clusters M>=" + Mcutoff.ToString() + ": " + tatalNumClusters.ToString());

            dataWriter.WriteLine("# distinct entries with single pfam arch M>=" + Mcutoff.ToString() + ": " + singleEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct entries with double pfam arch M>=" + Mcutoff.ToString() + ": " + doubleEntryList.Count.ToString());
            int totalNumEntries = GetDistinctEntryList(singleEntryList, doubleEntryList);
            dataWriter.WriteLine("# distinct entries with both M>=" + Mcutoff.ToString() + ": " + totalNumEntries.ToString());

            dataWriter.WriteLine("# distinct PDB BU entries with single pfam arch M>=" + Mcutoff.ToString() + ": " + singlePdbBuEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct PDB BU entries with double pfam arch M>=" + Mcutoff.ToString() + ": " + doublePdbBuEntryList.Count.ToString());
            int totalPdbBuEntries = GetDistinctEntryList(singlePdbBuEntryList, doublePdbBuEntryList);
            dataWriter.WriteLine("# distinct PDB BU entries with both M>=" + Mcutoff.ToString() + ": " + totalPdbBuEntries.ToString());

            dataWriter.WriteLine("# distinct PISA BU entries with single pfam arch M>=" + Mcutoff.ToString() + ": " + singlePisaBuEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct PISA BU entries with double pfam arch M>=" + Mcutoff.ToString() + ": " + doublePisaBuEntryList.Count.ToString());
            int totalPisaBuEntries = GetDistinctEntryList(singlePisaBuEntryList, doublePisaBuEntryList);
            dataWriter.WriteLine("# distinct PISA BU entries with both M>=" + Mcutoff.ToString() + ": " + totalPisaBuEntries.ToString());

            dataWriter.Flush();

            string mSeqString = " with M>=" + Mcutoff.ToString();
            if (seqIdCutoff < 99)
            {
                mSeqString += (",seqid<" + seqIdCutoff.ToString());
            }
            DataRow groupStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            groupStatInfoRow["Category"] = "#Groups" + mSeqString;
            groupStatInfoRow["Single"] = numOfSingleGroups;
            groupStatInfoRow["Pair"] = numOfDoubleGroups;
            groupStatInfoRow["Total"] = totalNumGroups;
            interfaceStatData.dbStatInfoTable.Rows.Add(groupStatInfoRow);

            DataRow clusterStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            clusterStatInfoRow["Category"] = "#Clusters" + mSeqString;
            clusterStatInfoRow["Single"] = numOfSingleClusters;
            clusterStatInfoRow["Pair"] = numOfDoubleClusters;
            clusterStatInfoRow["Total"] = tatalNumClusters;
            interfaceStatData.dbStatInfoTable.Rows.Add(clusterStatInfoRow);

            DataRow entryStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            entryStatInfoRow["Category"] = "#Entries" + mSeqString;
            entryStatInfoRow["Single"] = singleEntryList.Count;
            entryStatInfoRow["Pair"] = doubleEntryList.Count;
            entryStatInfoRow["Total"] = totalNumEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(entryStatInfoRow);

            DataRow pdbBuStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            pdbBuStatInfoRow["Category"] = "#PDBBU" + mSeqString;
            pdbBuStatInfoRow["Single"] = singlePdbBuEntryList.Count;
            pdbBuStatInfoRow["Pair"] = doublePdbBuEntryList.Count;
            pdbBuStatInfoRow["Total"] = totalPdbBuEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(pdbBuStatInfoRow);

            DataRow pisaBuStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            pisaBuStatInfoRow["Category"] = "#PISABU" + mSeqString;
            pisaBuStatInfoRow["Single"] = singlePisaBuEntryList.Count;
            pisaBuStatInfoRow["Pair"] = doublePisaBuEntryList.Count;
            pisaBuStatInfoRow["Total"] = totalPisaBuEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(pisaBuStatInfoRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="Mcutoff"></param>
        /// <param name="clusterSumInfoTable"></param>
        /// <returns></returns>
        private bool IsGroupMGreater(DataTable clusterSumInfoTable, int Mcutoff)
        {
            int Mcluster = 0;
            foreach (DataRow clusterSumInfoRow in clusterSumInfoTable.Rows)
            {
                Mcluster = Convert.ToInt32(clusterSumInfoRow["NumOfCfgCluster"].ToString());
                if (Mcluster >= Mcutoff)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupClusterSumInfoTable"></param>
        /// <param name="Mcutoff"></param>
        /// <returns></returns>
        private int[] GetClustersMGreater(int superGroupId, int Mcutoff, int seqIdCutoff)
        {
            int Mcluster = 0;
            int seqId = 0;
            List<int> clusterList = new List<int> ();

            string queryString = string.Format("Select * From PfamSuperClusterSumInfo " +
               " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterSumInfoTable = protcidQuery.Query(queryString);

            foreach (DataRow clusterSumInfoRow in clusterSumInfoTable.Rows)
            {
                Mcluster = Convert.ToInt32(clusterSumInfoRow["NumOfCfgCluster"].ToString());
                seqId = (int)(Convert.ToDouble(clusterSumInfoRow["MinSeqIdentity"].ToString()));
                if (Mcluster >= Mcutoff && seqId <= seqIdCutoff)
                {
                    clusterList.Add(Convert.ToInt32(clusterSumInfoRow["ClusterID"].ToString()));
                }
            }
            int[] clusters = new int[clusterList.Count];
            clusterList.CopyTo(clusters);
            return clusters;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupString"></param>
        /// <returns></returns>
        private bool IsSinglePfamArchGroup(string groupString)
        {
            string[] fields = groupString.Split(';');
            if (fields.Length == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetDistinctEntryInClusters(int superGroupId, int[] clusterIds)
        {
            if (clusterIds.Length == 0)
            {
                return new string[0];
            }
            string queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces " +
                " Where SuperGroupSeqID = {0} AND ClusterID in ({1});",
                superGroupId, ParseHelper.FormatSqlListString(clusterIds));
            DataTable entriesInClustersTable = protcidQuery.Query(queryString);
            string[] entriesInClusters = new string[entriesInClustersTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entriesInClustersTable.Rows)
            {
                entriesInClusters[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entriesInClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetDistinctBuEntryWithClusterInterfaces(int superGroupId, int[] clusterIds, string buType)
        {
            if (clusterIds.Length == 0)
            {
                return new string[0];
            }
            string queryString = "";
            if (buType == "pdb")
            {
                queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces " +
                    " Where SuperGroupSeqID = {0} AND ClusterID IN ({1}) AND INPDB = 1;",
                    superGroupId, ParseHelper.FormatSqlListString(clusterIds));
            }
            else
            {
                queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces " +
                    " Where SuperGroupSeqID = {0} AND ClusterID IN ({1}) AND INPISA = 1;",
                    superGroupId, ParseHelper.FormatSqlListString(clusterIds));
            }
            DataTable inBuEntryTable = protcidQuery.Query(queryString);
            string[] buEntries = new string[inBuEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow buEntryRow in inBuEntryTable.Rows)
            {
                buEntries[count] = buEntryRow["PdbID"].ToString();
                count++;
            }
            return buEntries;
        }
        #endregion
        #endregion

        #region chain arch relation meta data
        /// <summary>
        /// 
        /// </summary>
        public void GetChainInterfacePfamArchRel()
        {
            DataTable entryChainArchTable = CreatePfamChainArchRelTable();
            CreatePfamChainArchRelDbTable();

            PfamArchitecture pfamArch = new PfamArchitecture();

            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            DataTable entryTable = protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Chain Pfam Arch Rel";
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash (pdbId);
                GetEntryChainPfamArchInteract(pdbId, entityPfamArchHash, entryChainArchTable);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateChainInterfacePfamArchRel(string[] updateEntries)
        {
            PfamArchitecture pfamArch = new PfamArchitecture();
            DataTable entryChainArchTable = CreatePfamChainArchRelTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Chain PfamArch Rel";
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries )
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DeleteEntryChainArchRel(pdbId, entryChainArchTable.TableName);

                Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);
                GetEntryChainPfamArchInteract(pdbId, entityPfamArchHash, entryChainArchTable);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteEntryChainArchRel(string pdbId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", tableName, pdbId);
            dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityPfamArchHash"></param>
        private void GetEntryChainPfamArchInteract(string pdbId, Dictionary<int, string> entityPfamArchHash, DataTable chainArchTable)
        {
            string queryString = string.Format("Select InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = protcidQuery.Query(queryString);

            Dictionary<string, int> chainArchNumInteractHash = new Dictionary<string,int> ();
            Dictionary<string, int> chainArchNumHeteroHash = new Dictionary<string,int> ();

            int entityId1 = 0;
            int entityId2 = 0;
            string chainArch1 = "";
            string chainArch2 = "";
            string chainArchPair = "";
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
                chainArch1 = "peptide";
                chainArch2 = "peptide";
                if (entityPfamArchHash.ContainsKey(entityId1))
                {
                    chainArch1 = entityPfamArchHash[entityId1];
                }
                if (entityPfamArchHash.ContainsKey(entityId2))
                {
                    chainArch2 = entityPfamArchHash[entityId2];
                }
                if (string.Compare(chainArch1, chainArch2) > 0)
                {
                    string temp = chainArch1;
                    chainArch1 = chainArch2;
                    chainArch2 = temp;
                }
                chainArchPair = chainArch1 + ";" + chainArch2;
                if (chainArchNumInteractHash.ContainsKey(chainArchPair))
                {
                    int numOfInteract = (int)chainArchNumInteractHash[chainArchPair];
                    numOfInteract++;
                    chainArchNumInteractHash[chainArchPair] = numOfInteract;
                }
                else
                {
                    chainArchNumInteractHash.Add(chainArchPair, 1);
                }
                if (entityId1 != entityId2)
                {
                    if (chainArchNumHeteroHash.ContainsKey(chainArchPair))
                    {
                        int numOfInteract = (int)chainArchNumHeteroHash[chainArchPair];
                        numOfInteract++;
                        chainArchNumHeteroHash[chainArchPair] = numOfInteract;
                    }
                    else
                    {
                        chainArchNumHeteroHash.Add(chainArchPair, 1);
                    }
                }
            }
            int superGroupId = 0;
            foreach (string keyChainArchPair in chainArchNumInteractHash.Keys)
            {
                string[] chainArchFields = keyChainArchPair.Split (';');
                superGroupId = GetSuperGroupSeqID(keyChainArchPair);
                DataRow dataRow = chainArchTable.NewRow();
                dataRow["SuperGroupSeqID"] = superGroupId;
                dataRow["PdbID"] = pdbId;
                dataRow["ChainArch1"] = chainArchFields[0];
                dataRow["ChainARch2"] = chainArchFields[1];
                dataRow["NumOfInteractions"] = chainArchNumInteractHash[keyChainArchPair];
                if (chainArchNumHeteroHash.ContainsKey(keyChainArchPair))
                {
                    dataRow["NumOfHetero"] = chainArchNumHeteroHash[keyChainArchPair];
                }
                else
                {
                    dataRow["NumOfHetero"] = 0;
                }
                chainArchTable.Rows.Add(dataRow);
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, chainArchTable);
            chainArchTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainArchPair"></param>
        /// <returns></returns>
        private int GetSuperGroupSeqID(string chainArchPair)
        {
            string[] chainArchFields = chainArchPair.Split(';');
            string chainArchRelSuper = "";
            if (chainArchFields[0] == chainArchFields[1])
            {
                chainArchRelSuper = chainArchFields[0];
            }
            else
            {
                chainArchRelSuper = chainArchFields[0] + ";" + chainArchFields[1];
            }
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamARch = '{0}';", chainArchRelSuper);
            DataTable superGroupIdTable = protcidQuery.Query(queryString);
            int superGroupId = -1;
            if (superGroupIdTable.Rows.Count > 0)
            {
                superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString ());
            }
            return superGroupId;
        }
        /// <summary>
        /// 
        /// </summary>
        private DataTable CreatePfamChainArchRelTable ()
        {
            string[] chainArchColumns = {"SuperGroupSeqID", "PdbID", "ChainArch1", "ChainArch2", "NumOfHetero", "NumOfInteractions"};
            DataTable chainArchRelTable = new DataTable("PfamChainArchRelation");
            foreach (string col in chainArchColumns)
            {
                chainArchRelTable.Columns.Add(new DataColumn (col));
            }
            return chainArchRelTable;
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreatePfamChainArchRelDbTable()
        {
            DbCreator dbCreate = new DbCreator();

            string createTableString = "Create Table PfamChainArchRelation ( " +
                "SuperGroupSeqID Integer NOT NULL, " +
                "PdbID CHAR(4) NOT NULL, " +
                "ChainArch1 VARCHAR(1200)," +
                "ChainArch2 VARCHAR(1200)," +
                "NumOfHetero Integer, " +
                "NumOfInteractions Integer);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, "PfamChainArchRelation");

            string createIndexString = "Create Index EntryChainRel_Idx1 ON PfamChainArchRelation (SuperGroupSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PfamChainArchRelation");

            createIndexString = "Create Index EntryChainRel_Idx2 ON PfamChainArchRelation (PdbID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PfamChainArchRelation");
        }
        #endregion

        #region super group meta info
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainArchPairsMetaData()
        {
            CreatePfamSuperGroupMetaInfoDbTable();

            DataTable chainArchRelTable = CreatePfamSuperGroupMetaInfo();
    //        DataRow metaRow = chainArchRelTable.NewRow();

            string querystring = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable pfamSuperGroupTable = protcidQuery.Query(querystring);
            string chainRelPfamArch = "";
            int superGroupId = 0;
            string chainArch1 = "";
            int numOfEntries = 0;
            int numOfEntriesIChain = 0;
            foreach (DataRow superGroupRow in pfamSuperGroupTable.Rows)
            {
                chainRelPfamArch = superGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
                string[] chainRelPfamArchFields = chainRelPfamArch.Split (';');
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                numOfEntries = GetNumOfEntriesInSuperGroup(superGroupId);
                numOfEntriesIChain = GetNumOfIChainEntriesInSuperGroup(superGroupId);

                // added on June 19, 2014. Should check why those very small number of entries are not in right way.
                // about 15 entries, probably from updating
                if (numOfEntriesIChain > numOfEntries)
                {
                    numOfEntriesIChain = numOfEntries;
                }

                DataRow metaRow = chainArchRelTable.NewRow();
                metaRow["SuperGroupSeqID"] = superGroupId;
                metaRow["ChainArch1"] = chainRelPfamArchFields[0];
                if (chainRelPfamArchFields.Length == 2)
                {
                    metaRow["ChainArch2"] = chainRelPfamArchFields[1];
                }
                else
                {
                    metaRow["ChainArch2"] = chainRelPfamArchFields[0];
                }
                metaRow["NumOfEntries"] = numOfEntries;
                metaRow["NumOfEntriesIChain"] = numOfEntriesIChain;               
            }
            dbInsert.InsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, chainArchRelTable);
            chainArchRelTable.Clear();

            querystring = "Select SuperGroupSeqID, ChainArch1, ChainArch2, Count(Distinct PdbID) As EntryCount From PfamChainArchRelation " + 
                " Where SuperGroupSeqID = -1 Group By SuperGroupSeqID, ChainArch1, ChainArch2;";
            DataTable peptideGroupTable = protcidQuery.Query(querystring);
       //     string chainArch1 = "";
            foreach (DataRow peptideRow in peptideGroupTable.Rows)
            {
                DataRow metaRow = chainArchRelTable.NewRow();
                chainArch1 = peptideRow["ChainArch1"].ToString().TrimEnd();
                metaRow["SuperGroupSeqID"] = peptideRow["SuperGroupSeqID"];
                metaRow["ChainArch1"] = chainArch1;
                metaRow["ChainArch2"] = peptideRow["ChainArch2"];
                string[] peptideEntries = GetChainPeptideInteractEntries(chainArch1, peptideRow["ChainArch2"].ToString().TrimEnd());
                string[] otherPeptideEntries = GetEntriesWithChainArch(chainArch1, peptideEntries);
                metaRow["NumOfEntries"] = peptideEntries.Length + otherPeptideEntries.Length; // should include other entries with peptide in the asymmetric units.
                metaRow["NumOfEntriesIChain"] = peptideRow["EntryCount"];
                chainArchRelTable.Rows.Add(metaRow);
            }
            dbInsert.InsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, chainArchRelTable);
            chainArchRelTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateChainArchPairsMetaData(int[] superGroupSeqIds)
        {
            DataTable chainArchRelTable = CreatePfamSuperGroupMetaInfo();
            DataRow metaRow = chainArchRelTable.NewRow();

            string querystring = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable pfamSuperGroupTable = protcidQuery.Query(querystring);
            string chainRelPfamArch = "";
            int superGroupId = 0;
            string chainArch1 = "";
            int numOfEntries = 0;
            int numOfEntriesIChain = 0;
            foreach (DataRow superGroupRow in pfamSuperGroupTable.Rows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                if (Array.IndexOf(superGroupSeqIds, superGroupId) > -1)
                {
                    DeleteSuperGroupChainPfamArchPairInfo(superGroupId, chainArchRelTable.TableName);

                    chainRelPfamArch = superGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
                    string[] chainRelPfamArchFields = chainRelPfamArch.Split(';');

                    numOfEntries = GetNumOfEntriesInSuperGroup(superGroupId);
                    numOfEntriesIChain = GetNumOfIChainEntriesInSuperGroup(superGroupId);

                    metaRow["SuperGroupSeqID"] = superGroupId;
                    metaRow["ChainArch1"] = chainRelPfamArchFields[0];
                    if (chainRelPfamArchFields.Length == 2)
                    {
                        metaRow["ChainArch2"] = chainRelPfamArchFields[1];
                    }
                    else
                    {
                        metaRow["ChainArch2"] = chainRelPfamArchFields[0];
                    }
                    metaRow["NumOfEntries"] = numOfEntries;
                    metaRow["NumOfEntriesIChain"] = numOfEntriesIChain;
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, metaRow);
                }
            }
            querystring = "Select SuperGroupSeqID, ChainArch1, ChainArch2, Count(Distinct PdbID) As EntryCount From PfamChainArchRelation " +
                " Where SuperGroupSeqID = -1 Group By SuperGroupSeqID, ChainArch1, ChainArch2;";
            DataTable peptideGroupTable = protcidQuery.Query(querystring);

            DeleteSuperGroupChainPfamArchPairInfo(-1, chainArchRelTable.TableName);

            foreach (DataRow peptideRow in peptideGroupTable.Rows)
            {
                chainArch1 = peptideRow["ChainArch1"].ToString().TrimEnd();
                metaRow["SuperGroupSeqID"] = peptideRow["SuperGroupSeqID"];
                metaRow["ChainArch1"] = chainArch1;
                metaRow["ChainArch2"] = peptideRow["ChainArch2"];
                string[] peptideEntries = GetChainPeptideInteractEntries(chainArch1, peptideRow["ChainArch2"].ToString().TrimEnd());
                string[] otherPeptideEntries = GetEntriesWithChainArch(chainArch1, peptideEntries);
                metaRow["NumOfEntries"] = peptideEntries.Length + otherPeptideEntries.Length; // should include other entries with peptide in the asymmetric units.
                metaRow["NumOfEntriesIChain"] = peptideRow["EntryCount"];
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, metaRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="tableName"></param>
        private void DeleteSuperGroupChainPfamArchPairInfo(int superGroupId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} WHere SuperGroupSeqID = {1};", tableName, superGroupId);
            dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainArch1"></param>
        /// <param name="chainArch2"></param>
        /// <returns></returns>
        private string[] GetChainPeptideInteractEntries(string chainArch1, string chainArch2)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamChainARchRelation Where ChainARch1 = '{0}' AND ChainArch2 = '{1}';",
                chainArch1, chainArch2);
            DataTable entryTable = protcidQuery.Query(queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainArch"></param>
        /// <param name="existEntries"></param>
        /// <returns></returns>
        private string[] GetEntriesWithChainArch(string chainArch, string[] existEntries)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamEntityPfamArch " + 
                " Where SupPfamArchE5 = '{0}' OR SupPfamArchE3 = '{0}' OR SupPfamArch = '{0}';", chainArch);
            DataTable entryTable = pdbfamQuery.Query(queryString);
            List<string> leftEntryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (! existEntries.Contains(pdbId))
                {
                    if (IsPeptideExist(pdbId))
                    {
                        leftEntryList.Add(pdbId);
                    }
                }
            }
            return leftEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsPeptideExist(string pdbId)
        {
            string queryString = string.Format("Select EntityID, Sequence From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entitySeqTable = pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct EntityID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamEntityTable = pdbfamQuery.Query(queryString);
            int entityId = 0;
            string sequence = "";
            List<int> parsedEntityList = new List<int> ();
            foreach (DataRow entitySeqRow in entitySeqTable.Rows)
            {
                entityId = Convert.ToInt32(entitySeqRow["EntityID"].ToString());
                if (parsedEntityList.Contains(entityId))
                {
                    continue;
                }
                parsedEntityList.Add(entityId);

                DataRow[] pfamEntityRows = pfamEntityTable.Select(string.Format ("EntityID = '{0}'", entityId));
                if (pfamEntityRows.Length == 0)
                {
                    sequence = entitySeqRow["Sequence"].ToString().TrimEnd();
                    if (sequence.Length <= ProtCidSettings.peptideLengthCutoff)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int GetNumOfIChainEntriesInSuperGroup(int superGroupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamChainArchRelation Where SuperGroupSeqID = {0};", superGroupId);
            DataTable entryTable = protcidQuery.Query(queryString);
            return entryTable.Rows.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        /// <returns></returns>
        private int GetNumOfEntriesInSuperGroup(int superGroupSeqId)
        {
            string[] entriesInSuperGroup = GetEntriesInSuperGroup(superGroupSeqId);
            return entriesInSuperGroup.Length;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        /// <returns></returns>
        private string[] GetEntriesInSuperGroup(int superGroupSeqId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupSeqId);
            DataTable groupIdTable = protcidQuery.Query(queryString);
            int groupId = 0;
            List<string> superGroupEntryList = new List<string> ();
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                string[] groupEntries = GetEntriesInGroup(groupId);
                superGroupEntryList.AddRange(groupEntries);
            }
            return superGroupEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetEntriesInGroup(int groupId)
        {
            string querystring = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable entryTable = protcidQuery.Query(querystring);
            List<string> groupEntryList = new List<string> ();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID"].ToString());
            }
            querystring = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign Where GroupSeqID = {0};", groupId);
            DataTable homoEntryTable = protcidQuery.Query(querystring);
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                groupEntryList.Add(entryRow["PdbID2"].ToString());
            }
            return groupEntryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        private DataTable CreatePfamSuperGroupMetaInfo()
        {
            string[] chainArchRelColumns = { "SuperGroupSeqID", "ChainArch1", "ChainArch2", "NumOfEntries", "NumOfEntriesIChain" };
            DataTable chainArchRelTable = new DataTable("PfamChainPairInPdb");
            foreach (string col in chainArchRelColumns)
            {
                chainArchRelTable.Columns.Add(new DataColumn(col));
            }
            return chainArchRelTable;
        }
        /// <summary>
        /// 
        /// </summary>
        private void CreatePfamSuperGroupMetaInfoDbTable()
        {
            DbCreator dbCreate = new DbCreator();
            string createTableString = "Create Table PfamChainPairInPdb ( " +
                "SuperGroupSeqID Integer NOT NULL, " +
                "ChainArch1 VARCHAR(1200), " +
                "ChainArch2 VARCHAR(1200), " +
                "NumOfEntries Integer, " +
                "NumOfEntriesIChain Integer);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, "PfamChainPairInPdb");

            string createIndexString = "Create Index PfamChainPair_idx1 ON PfamChainPairInPdb (SuperGroupSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PfamChainPairInPdb");

            createIndexString = "Create Index PfamChainPair_idx2 ON PfamChainPairInPdb (ChainArch1, ChainArch2);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PfamChainPairInPdb");
        }
        #endregion
        #endregion

        #region group description and group numbers
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamFamilyGroupClusterInfo(string tableType)
        {
            string queryString = string.Format ("Select Distinct SuperGroupSeqID, ClusterID From {0}ClusterEntryInterfaces;", tableType);
            DataTable clusterTable = protcidQuery.Query(queryString);
            int groupId = -1;
            int clusterId = -1;
            Dictionary<int, List<int>> groupClusterHash = new Dictionary<int,List<int>> ();
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                groupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                if (groupClusterHash.ContainsKey(groupId))
                {
                    groupClusterHash[groupId].Add(clusterId);
                }
                else
                {
                    List<int> clusterList = new List<int> ();
                    clusterList.Add(clusterId);
                    groupClusterHash.Add(groupId, clusterList);
                }
            }
            StreamWriter dataWriter = new StreamWriter("PfamGroupClustersInfo.txt");
            dataWriter.WriteLine("Pfam_Relation\tGroupID\tClusterIDs");
            string groupString = "";
            string dataLine = "";
            List<int> keyGroupIdList = new List<int> (groupClusterHash.Keys);
            keyGroupIdList.Sort();
            foreach (int keyGroupId in keyGroupIdList)
            {
                groupString = GetPfamFamilyInfo(keyGroupId, tableType);
                dataLine = groupString + "\t" + keyGroupId.ToString() + "\t";
                foreach (int lsClusterId in groupClusterHash[keyGroupId])
                {
                    dataLine += (lsClusterId.ToString() + ",");
                }
                dataLine = dataLine.TrimEnd(',');
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string GetPfamFamilyInfo(int groupId, string tableType)
        {
            string queryString = string.Format("Select ChainRelPfamArch From {0}Groups " +
                " Where SuperGroupSeqID = {1};", tableType, groupId);
            DataTable familyStringTable = protcidQuery.Query(queryString);
            return familyStringTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
        }
        #endregion

        #region update cluster interface
        /// <summary>
        /// 
        /// </summary>
        public void UpdateClusterInterfacesWithMediumSa()
        {
            string queryString = "Select Distinct SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo;";
            DataTable clusterSumInfoTable = protcidQuery.Query(queryString);
            int superGroupId = 0;
            int clusterId = 0;
            string clusterInterface = ""; // the interface with the medium surface area in the cluster
            double clusterInterfaceSurfaceArea = 0;
            foreach (DataRow clusterIdRow in clusterSumInfoTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterIdRow["SuperGroupSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                
                clusterInterface = GetClusterRepInterface(superGroupId, clusterId, out clusterInterfaceSurfaceArea);
                UpdateClusterSumInfoClusterInterface(superGroupId, clusterId, clusterInterface, clusterInterfaceSurfaceArea);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRepInterfaceTable"></param>
        /// <param name="mediumSurfaceArea"></param>
        /// <returns></returns>
        private string GetClusterRepInterface(int superGroupId, int clusterId, out double mediumSurfaceArea)
        {
            DataTable clusterRepInterfaceTable = GetClusterRepInterfaceInfoTable(superGroupId, clusterId);
            DataRow[] clusterRows = clusterRepInterfaceTable.Select ("", "SurfaceArea ASC");
            int mediumIndex = (int)(clusterRows.Length / 2);
            string clusterInterface = clusterRows[mediumIndex]["PdbID"].ToString() + "_" +
                clusterRows[mediumIndex]["InterfaceID"].ToString();
            mediumSurfaceArea = Convert.ToDouble(clusterRows[mediumIndex]["SurfaceArea"].ToString ());
            return clusterInterface;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterRepInterfaceInfoTable (int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select PfamSuperClusterEntryInterfaces.* " + 
                " From PfamSuperClusterEntryInterfaces, PfamSuperInterfaceClusters " +
                " Where PfamSuperInterfaceClusters.SuperGroupSeqID = {0} AND " + 
                " PfamSuperInterfaceClusters.ClusterID = {1} AND " + 
                " PfamSuperInterfaceClusters.SuperGroupSeqID = PfamSuperClusterEntryInterfaces.SuperGroupSeqID AND " + 
                " PfamSuperInterfaceClusters.ClusterID = PfamSuperClusterEntryInterfaces.ClusterID AND "  +
                " PfamSuperInterfaceClusters.PdbID = PfamSuperClusterEntryInterfaces.PdbID AND " +
                " PfamSuperInterfaceClusters.InterfaceID = PfamSuperClusterEntryInterfaces.InterfaceID " +
                " Order By PdbId, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = protcidQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            DataTable clusterRepInterfaceTable = clusterInterfaceTable.Clone();
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                if (! entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                    DataRow dataRow = clusterRepInterfaceTable.NewRow();
                    dataRow.ItemArray = interfaceRow.ItemArray;
                    clusterRepInterfaceTable.Rows.Add(dataRow);
                }
            }
            return clusterRepInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterInterface"></param>
        /// <param name="clusterInterfaceSa"></param>
        private void UpdateClusterSumInfoClusterInterface(int superGroupId, int clusterId,
            string clusterInterface, double clusterInterfaceSa)
        {
            string updateString = string.Format("Update PfamSuperClusterSumInfo " + 
                " Set ClusterInterface = '{0}', MediumSurfaceArea = {1} " + 
                " Where SuperGroupSeqID = {2} AND ClusterID = {3};", 
                clusterInterface, clusterInterfaceSa, superGroupId, clusterId);
            protcidQuery.Query(updateString);
        }
        #endregion

        #region for super group pfam arch meta info
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePfamArchMetaInfo()
        {
            // PfamChainArchRelation: the pair of chain pfam arch for each entry
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Chain Pfam arch relation for each entry");
            GetChainInterfacePfamArchRel();

            // PfamChainPairInPdb: summary data for each pair of chain pfam arch
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("summary info for each pair of chain pfam archs");
            RetrieveChainArchPairsMetaData();        
        }
        #endregion

        #region for debug
        public void PrintChainClusterTextFilesFromDb ()
        {
            InitializeTables("PfamSuper", true);
            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, "PfamSuperChainInterfaceClusterInfo.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, "PfamSuperChainInterfaceClusterSumInfo.txt"), true);

            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperClusterSumInfo;";
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int superGroupId = 0;
            Dictionary<int,string> groupFamilyStringHash = new Dictionary<int,string> ();
            foreach (DataRow superGroupRow in superGroupIdTable.Rows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString ());
                PrintGroupChainClusterTextFile(superGroupId, groupFamilyStringHash, clusterWriter, clusterSumWriter);
            }
            clusterWriter.Close();
            clusterSumWriter.Close();
        }

        public void PrintGroupChainClusterTextFile(int superGroupId, Dictionary<int, string> groupFamilyStringHash, StreamWriter clusterWriter, StreamWriter clusterSumWriter)
        {
            string queryString = string.Format ("Select * From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select * From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);

            if (clusterInterfaceTable.Rows.Count == 0)
            {
                return;
            }
            string superGroupString = GetSuperGroupString(superGroupId);
            string groupFileName = DownloadableFileName.GetChainGroupTarGzFileName(superGroupId);
            string groupClusterFile = Path.Combine(resultDir, groupFileName + ".txt");
            StreamWriter groupClusterWriter = new StreamWriter(groupClusterFile);
            if (headerNames == null)
            {
                headerNames = GetHeaderNames();
                List<string> headerNameList = new List<string> (headerNames);
                headerNameList.Remove("SuperGroupSeqID");
                headerNameList.Remove("ClusterID");
                headerNameList.Insert(0, "ClusterID");
                headerNameList.Insert(0, "SuperGroupSeqID"); // put them before the other columns
                headerNames = new string[headerNameList.Count];
                headerNameList.CopyTo(headerNames);
            }

            // write data to file
            clusterWriter.WriteLine(FormatHeaderString());
            groupClusterWriter.WriteLine(FormatHeaderString());
            List<int> clusterList = new List<int> ();

            foreach (DataRow dRow in clusterSumInfoTable.Rows)
            {
                clusterList.Add(Convert.ToInt32(dRow["ClusterID"].ToString()));
            }

            clusterList.Sort();
            string dataStream = "";
            double numOfNmr = 0;
            double numEntryCluster = 0;
            string dbColName = "";
            string line = "";
            int groupId = 0;
            foreach (int clusterId in clusterList)
            {
                dataStream = "";
                DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("ClusterId = '{0}'", clusterId), "GroupSeqID, SpaceGroup, CrystForm, PdbID ASC");
                if (interfaceRows.Length == 0)
                {
                    continue;
                }
                
                foreach (DataRow interfaceRow in interfaceRows)
                {
                    line = "";
                    try
                    {                      
                        foreach (string colName in headerNames)
                        {
                            if (clusterInterfaceTable.Columns.Contains(colName))
                            {
                                if (colName.ToUpper() == "SUPERGROUPSEQID")
                                {
                                    line += superGroupString;
                                }
                                else if (colName.ToUpper() == "GROUPSEQID")
                                {
                                    line +=  groupFamilyStringHash[groupId];
                                }
                                else
                                {
                                    line += interfaceRow[colName].ToString();
                                }
                                line += "	";
                            }
                            else
                            {
                                line += "";
                                line += "	";
                            }
                        }
                        dataStream += line.TrimEnd('	');
                        dataStream += "\r\n";
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(ParseHelper.FormatDataRow(interfaceRow));
                        ProtCidSettings.logWriter.Flush();
                    }                 
                }               

                DataRow[] clusterSummaryRows = clusterSumInfoTable.Select (string.Format("ClusterID = '{0}'", clusterId));

                line = "";
                string sumLine = superGroupString + "	";
                foreach (string colName in headerNames)
                {
                    if (colName.IndexOf("#") > -1 || colName.IndexOf("/") > -1 || colName.IndexOf("(") > -1)
                    {
                        dbColName = colName.Replace("#", "NumOf");
                        dbColName = dbColName.Replace("/", "");
                        dbColName = dbColName.Replace("(", "_");
                        dbColName = dbColName.Replace(")", "");
                    }
                    else
                    {
                        dbColName = colName;
                    }
                //    if (interfaceStatData.clusterDataTable.Columns.Contains(colName))
                    if (clusterSumInfoTable.Columns.Contains(dbColName))
                    {
                        if (colName.ToUpper() == "SUPERGROUPSEQID")
                        {
                            line = clusterSummaryRows[0][dbColName].ToString() + line;
                        }
                        else
                        {
                            line += clusterSummaryRows[0][dbColName].ToString();
                        }
                        line += "\t";
                        sumLine += clusterSummaryRows[0][dbColName].ToString();
                        sumLine += "	";
                    }
                    else
                    {
                        switch (colName.ToUpper())
                        {
                            case "SURFACEAREA":
                                line += string.Format("{0:0.##}", clusterSummaryRows[0]["SurfaceArea"].ToString());
                                line += "	";
                                sumLine += string.Format("{0:0.##}", clusterSummaryRows[0]["SurfaceArea"].ToString());
                                sumLine += "	";
                                break;

                            case "ASU":
                                line += clusterSummaryRows[0]["MaxAsu"].ToString();
                                sumLine += clusterSummaryRows[0]["MaxAsu"].ToString();
                                line += "	";
                                sumLine += "	";
                                break;

                            case "PDBBU":
                                line += clusterSummaryRows[0]["MaxPdbBu"].ToString();
                                sumLine += clusterSummaryRows[0]["MaxPdbBu"].ToString();
                                line += "	";
                                sumLine += "	";
                                break;

                            case "PISABU":
                                line += clusterSummaryRows[0]["MaxPisaBu"].ToString();
                                sumLine += clusterSummaryRows[0]["MaxPdbBu"].ToString();
                                line += "	";
                                sumLine += "	";
                                break;

                            default:
                                line += "";
                                line += "	";
                                break;
                        }
                    }
                }// finish one cluster
                dataStream = line.TrimEnd('	') + "\r\n" + dataStream;
                clusterWriter.WriteLine(dataStream);
                groupClusterWriter.WriteLine(dataStream);
                // add summary data into line
                // ratio #SG/Cluster and #SG/Family
                sumLine += string.Format("{0:0.###}", Convert.ToDouble(clusterSummaryRows[0]["NumOfCfgCluster"].ToString()) /
                    Convert.ToDouble(clusterSummaryRows[0]["NumOfCFGFamily"].ToString()));
                sumLine += "	";

                numOfNmr = Convert.ToDouble(clusterSummaryRows[0]["NumOfNmr"].ToString());
                numEntryCluster = Convert.ToDouble(clusterSummaryRows[0]["NumOfEntryCluster"].ToString ());
                // ratio #ASU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (Convert.ToDouble(clusterSummaryRows[0]["InAsu"].ToString()) -  numOfNmr)/(numEntryCluster - numOfNmr));
                sumLine += "	";
                // ratio #PDBBU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (Convert.ToDouble(clusterSummaryRows[0]["InPdb"].ToString()) - numOfNmr)/(numEntryCluster - numOfNmr));
                sumLine += "	";
                // ratio #PISABU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", Convert.ToDouble(clusterSummaryRows[0]["InPisa"].ToString())/(numEntryCluster - numOfNmr));
                sumLine += "	";
                // #NMR entries/Cluster
                sumLine += numOfNmr.ToString ();
                clusterSumWriter.WriteLine(sumLine);
            }   
            // output results to a group file for the web server
            groupClusterWriter.Close();
            ParseHelper.ZipPdbFile(groupClusterFile);       
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataWriter"></param>
        public void GetGroupSumInfo()
        {
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable superGroupTable = protcidQuery.Query(queryString);

            int numOfSingleGroups = 0;
            int numOfDoubleGroups = 0;
            List<string> singleEntryList = new List<string> ();
            List<string> doubleEntryList = new List<string> ();

            int superGroupId = 0;
            string groupString = "";
            foreach (DataRow groupRow in superGroupTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());
                groupString = groupRow["ChainRelPfamArch"].ToString().TrimEnd();
                string[] groupEntries_All = GetGroupEntries(superGroupId);
                string[] groupEntries = GetEntryWithInterfaces(groupEntries_All);
                if (groupEntries.Length > 0)
                {
                    if (IsSinglePfamArchGroup(groupString))
                    {
                        numOfSingleGroups++;
                        foreach (string entry in groupEntries)
                        {
                            if (!singleEntryList.Contains(entry))
                            {
                                singleEntryList.Add(entry);
                            }
                        }
                    }
                    else
                    {
                        numOfDoubleGroups++;
                        foreach (string entry in groupEntries)
                        {
                            if (!doubleEntryList.Contains(entry))
                            {
                                doubleEntryList.Add(entry);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private string[] GetEntryWithInterfaces(string[] pdbIds)
        {
            List<string> entryWithInterfaceList = new List<string> ();
            foreach (string pdbId in pdbIds)
            {
                if (IsEntryWithInterfaces(pdbId))
                {
                    entryWithInterfaceList.Add(pdbId);
                }
            }
            return entryWithInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryWithInterfaces(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = protcidQuery.Query(queryString);
            if (interfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public void UpdateSupergroupInterfaceClusters()
        {
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperClusterEntryInterfaces Where InterfaceUnit = 'AB';";
            DataTable groupIdTable = protcidQuery.Query(queryString);
            int[] updateSuperGroups = new int[groupIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                updateSuperGroups[count] = Convert.ToInt32(groupIdRow["SuperGroupSeqID"].ToString ());
                count++;
            }

            string type = "pfam";
            InitializeTables(type + "Super", true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"), true);
            StreamWriter clusterSumWriter = new StreamWriter(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"), true);

            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
            ProtCidSettings.progressInfo.totalOperationNum = updateSuperGroups.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateSuperGroups.Length;

            foreach (int supergroupId in updateSuperGroups)
            {
                sgInterfaceNumHash.Clear();
                superGroupClusterSumTable.Clear();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = supergroupId.ToString();

                try
                {
                    DeleteObsData(supergroupId);
                    PrintGroupClusterStatInfo(supergroupId, clusterWriter, clusterSumWriter, type);

                    clusterSumWriter.Flush();
                    clusterWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupId.ToString() + " Cluster Stat Error: " + ex.Message);
#if DEBUG
                    logWriter.WriteLine(supergroupId.ToString() + " Cluster Stat Error: " + ex.Message);
                    logWriter.Flush();
#endif
                }
            }
            //	DbBuilder.dbConnect.DisconnectFromDatabase ();
            clusterWriter.Close();
            clusterSumWriter.Close();
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterInfo_update.txt"));
            ParseHelper.ZipPdbFile(Path.Combine(resultDir, type + "SuperChainInterfaceClusterSumInfo_update.txt"));
        }

        #region update BA comp info
        private string[] ReadInConsistentInPdbPisaInterfaces ()
        {
            List<string> updateBaEntryInterfaceList = new List<string>();
            string baErrorFile = "";
            StreamReader dataReader = new StreamReader(baErrorFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (! updateBaEntryInterfaceList.Contains (fields[1] + fields[2]))
                {
                    updateBaEntryInterfaceList.Add(fields[1] + fields[2]);
                }
            }
            dataReader.Close();
            return updateBaEntryInterfaceList.ToArray ();
        }
  
        public void UpdateBACompInfo()
        {
            // store update groups, for update summary data of groups.
            StreamWriter updateClusterWriter = new StreamWriter("BaUpdatedSuperClustersList.txt");
            string[] updateInterfaceList = ReadInConsistentInPdbPisaInterfaces();
            string pdbId = "";
            int interfaceId = 0;
            string orgInPdb = "";
            string orgInPisa = "";
            string updateString = "";
            string queryString = "";
            List<string> updateChainClusterList = new List<string>();
            string chainCluster = "";
            
            foreach (string entryInterface in updateInterfaceList)
            {
                pdbId = entryInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(entryInterface.Substring(4, entryInterface.Length - 4));
                queryString = string.Format("Select SuperGroupSeqID, ClusterID, PdbID, InterfaceID, InPdb, InPisa, PdbBuID, PdbBu, PisaBuId, PisaBu " +
                    "From PfamSuperClusterEntryInterfaces where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                DataTable interfaceTable = protcidQuery.Query(queryString);
                if (interfaceTable.Rows.Count > 0)
                {
                    DataRow interfaceStatRow = interfaceTable.Rows[0];
                    chainCluster = interfaceStatRow["SuperGroupSeqID"].ToString() + "_" +
                        interfaceStatRow["ClusterID"].ToString ();
                    if (!updateChainClusterList.Contains(chainCluster))
                    {
                        updateChainClusterList.Add(chainCluster);
                        updateClusterWriter.WriteLine(chainCluster.ToString());
                        updateClusterWriter.Flush();
                    }
                    orgInPdb = interfaceStatRow["InPdb"].ToString();
                    orgInPisa = interfaceStatRow["InPisa"].ToString();
                    FormatBuAndCrystBuCompInfo(ref interfaceStatRow);
                    if (orgInPdb != interfaceStatRow["InPdb"].ToString() ||
                        orgInPisa != interfaceStatRow["InPisa"].ToString())
                    {
                        updateString = string.Format("Update PfamSuperClusterEntryInterfaces Set InPdb = '{0}', InPisa = '{1}'," +
                            " PdbBuID = '{2}', PdbBu = '{3}', PisaBuId = '{4}', PisaBu = '{5}' " +
                            " Where PdbID = '{6}' AND InterfaceID = {7};",
                            interfaceStatRow["InPdb"], interfaceStatRow["InPisa"], interfaceStatRow["PdbBuID"],
                            interfaceStatRow["PdbBu"], interfaceStatRow["PisaBuId"], interfaceStatRow["PisaBu"],
                            pdbId, interfaceId);
                        protcidUpdate.Update(updateString);
                    }
                }
            }
            updateClusterWriter.Close();

            UpdateClusterBaSummaryInfo(updateChainClusterList.ToArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateClusters"></param>
        public void UpdateClusterBaSummaryInfo (string[] updateClusters)
        {
            string queryString = "";
            string updateString = "";
            foreach (string updateCluster in updateClusters)
            {
                string[] groupCluster = updateCluster.Split('_');
                queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces " + 
                    "Where SuperGroupSeqID = {0} AND ClusterID = {1} AND InPdb = '1';", 
                    groupCluster[0], groupCluster[1]);
                DataTable inPdbEntryTable = protcidQuery.Query(queryString);

                queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces " +
                    "Where SuperGroupSeqID = {0} AND ClusterID = {1} AND InPisa = '1';",
                    groupCluster[0], groupCluster[1]);
                DataTable inPisaEntryTable = protcidQuery.Query(queryString);

                updateString = string.Format("Update PfamSuperClusterSumInfo Set InPdb = {0}, InPisa = {1} " +
                    "Where SuperGroupSeqID = {2} AND ClusterID = {3};", inPdbEntryTable.Rows.Count, 
                    inPisaEntryTable.Rows.Count, groupCluster[0], groupCluster[1]);
                protcidUpdate.Update(updateString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="inPdb"></param>
        /// <returns></returns>
        private bool IsInterfaceInBA(string pdbId, int interfaceId, bool isPdb, out string buId)
        {
            string queryString = "";
            buId = "-1";
            if (isPdb)
            {
                queryString = string.Format("Select * From CrystPdbBuInterfaceComp Where PdbID = '{0}' AND InterfaceID = {1} Order By Qscore Desc;", pdbId, interfaceId);
            }
            else
            {
                queryString = string.Format("Select * From CrystPisaBuInterfaceComp Where PdbID = '{0}' AND InterfaceID = {1} Order By Qscore Desc;", pdbId, interfaceId);
            }
            DataTable interfaceBACompTable = protcidQuery.Query(queryString);
            double qScore = 0;
            if (interfaceBACompTable.Rows.Count > 0)
            {
                qScore = Convert.ToDouble(interfaceBACompTable.Rows[0]["QScore"].ToString());
                if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                {
                    buId = interfaceBACompTable.Rows[0]["BuID"].ToString();
                    return true;
                }
            }
            return false;
        }
        #endregion
        #endregion

    }
}
