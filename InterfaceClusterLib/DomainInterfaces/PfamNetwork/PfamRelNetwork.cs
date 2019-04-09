using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamNetwork
{
    public class PfamRelNetwork
    {
        private DbQuery dbQuery = new DbQuery();
        private string pfamNetFileDir = Path.Combine (ProtCidSettings.dirSettings.pfamPath, "PfamNetwork");
        private string pfamPairPage = "EntryPfamPair.aspx";  // redirect to the page when a user click a node

        #region pfam network for each pfam
        /// <summary>
        /// 
        /// </summary>
        public void GeneratePfamNetworkGraphmlFiles ()
        {
            if (!Directory.Exists(pfamNetFileDir))
            {
                Directory.CreateDirectory(pfamNetFileDir);
            }
            DataTable relPfamNumEntriesTable = GetRelationNumOfEntries();
            DataTable pfamNumEntriesTable = GetPfamNumOfEntries();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam network graph xml";

            string[] pfamIds = GetPfamsInPdb();
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;

            foreach (string centerPfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = centerPfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    GeneratePfamNetworkGraphml(centerPfamId, relPfamNumEntriesTable, pfamNumEntriesTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(centerPfamId + " generate graphml file error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(centerPfamId + " generate graphml file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            //the pfam interaction network for relation with M>=5 and MinSeqIdentity < 90%
            WritePfamBiolNetwork();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamNetworkGraphmlFiles(string[] updateEntries)
        {
            if (!Directory.Exists(pfamNetFileDir))
            {
                Directory.CreateDirectory(pfamNetFileDir);
            }
            DataTable relPfamNumEntriesTable = GetRelationNumOfEntries();
            DataTable pfamNumEntriesTable = GetPfamNumOfEntries();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam network graph xml";

            string[] pfamIds = GetUpdatePfams(updateEntries);
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;

            foreach (string centerPfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = centerPfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    string pfamNetFile = Path.Combine(pfamNetFileDir, centerPfamId + ".xml");
                    File.Delete(pfamNetFile);

                    GeneratePfamNetworkGraphml(centerPfamId, relPfamNumEntriesTable, pfamNumEntriesTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(centerPfamId + " generate graphml file error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(centerPfamId + " generate graphml file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            //the pfam interaction network for relation with M>=5 and MinSeqIdentity < 90%
            WritePfamBiolNetwork();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPfamId"></param>
        /// <param name="relPfamNumEntriesTable"></param>
        /// <param name="pfamNumEntriesTable"></param>
        public void GeneratePfamNetworkGraphml(string centerPfamId, DataTable relPfamNumEntriesTable, DataTable pfamNumEntriesTable)
        {
            DataRow[] interactingPfamRows = relPfamNumEntriesTable.Select(string.Format("FamilyCode1 = '{0}' OR FamilyCode2 = '{0}'", centerPfamId), "NumOfEntries DESC");
            if (interactingPfamRows.Length == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No interactions for " + centerPfamId);
                return;
            }
            Dictionary<int, List<string>> numEntryRelPfamsHash = new Dictionary<int,List<string>> ();   // the number of entries for the interation, the label of the edge
            DataTable thisPfamNumEntriesTable = pfamNumEntriesTable.Clone();  // the number of entries with the Pfam, the weight of the node
            int relNumOfEntries = 0;
            string relPfamId = "";
            foreach (DataRow relPfamRow in interactingPfamRows)
            {
                relNumOfEntries = Convert.ToInt32(relPfamRow["NumOfEntries"].ToString());
                if (relNumOfEntries == 0)
                {
                    continue;
                }
                relPfamId = relPfamRow["FamilyCode1"].ToString ().TrimEnd ();
                if (relPfamId == centerPfamId)
                {
                    relPfamId = relPfamRow["FamilyCode2"].ToString ().TrimEnd ();
                }
                if (numEntryRelPfamsHash.ContainsKey(relNumOfEntries))
                {
                    numEntryRelPfamsHash[relNumOfEntries].Add(relPfamId);
                }
                else
                {
                    List<string> relPfamList = new List<string> ();
                    relPfamList.Add(relPfamId);
                    numEntryRelPfamsHash.Add(relNumOfEntries, relPfamList);
                }
                DataRow[] pfamNumEntriesRows = pfamNumEntriesTable.Select(string.Format ("Pfam_ID = '{0}'", relPfamId));
                DataRow thisPfamNumEntryRow = thisPfamNumEntriesTable.NewRow();
                if (pfamNumEntriesRows.Length > 0)
                {
                    thisPfamNumEntryRow.ItemArray = pfamNumEntriesRows[0].ItemArray;
                    thisPfamNumEntriesTable.Rows.Add(thisPfamNumEntryRow);
                }
                else
                {
                    thisPfamNumEntryRow["Pfam_ID"] = relPfamId;
                    thisPfamNumEntryRow["NumOfEntries"] = relNumOfEntries;
                    thisPfamNumEntriesTable.Rows.Add(thisPfamNumEntryRow);
                }
            }
            WritePfamnetworkGraphml(centerPfamId, numEntryRelPfamsHash, thisPfamNumEntriesTable);
        }

        /// <summary>
        /// the weight is same as the label of the edge
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="numPfamsHash">key: #entries, value: the list interacting Pfams</param>
        public void WritePfamnetworkGraphml(string pfamId, Dictionary<int, List<string>> numPfamsHash)
        {
            string pfamNetFile = Path.Combine(pfamNetFileDir, pfamId + ".xml");
            StreamWriter dataWriter = new StreamWriter(pfamNetFile);
            dataWriter.WriteLine("<graphml>");
            dataWriter.WriteLine("    <key id=\"label\" for=\"all\" attr.name=\"label\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <key id=\"weight\" for=\"node\" attr.name=\"weight\" attr.type=\"integer\"/>");
            dataWriter.WriteLine("    <key id=\"link\" for=\"node\" attr.name=\"link\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <graph>");

            int nodeId = 1;
            string nodeString = "";
            string edgeString = "";
            int numOfEntries = 0;

            List<int> numList = new List<int> (numPfamsHash.Keys);
            numList.Sort ();
            int centerPfamNum = (int)numList[numList.Count - 1] + 10;

            // nodes
            string centerPfamNodeString = FormatNodeString(nodeId, pfamId, centerPfamNum);
            dataWriter.WriteLine(centerPfamNodeString);

            for (int i = numList.Count - 1; i >= 0; i -- )
            {
                numOfEntries = (int)numList[i];
                List<string> relPfamList = numPfamsHash[numOfEntries];
                relPfamList.Sort();
                foreach (string relPfamId in relPfamList)
                {
                    if (relPfamId == pfamId)
                    {
                        edgeString = FormatEdgeString(1, 1, pfamId, relPfamId, numOfEntries);
                        dataWriter.WriteLine(edgeString);
                        continue;
                    }
                    nodeId++;
                    nodeString = FormatNodeString(nodeId, relPfamId, numOfEntries);
                    dataWriter.WriteLine(nodeString);
                    edgeString = FormatEdgeString(1, nodeId, pfamId, relPfamId, numOfEntries);
                    dataWriter.WriteLine(edgeString);
                }
            }
           
            dataWriter.WriteLine("    </graph>");
            dataWriter.WriteLine("</graphml>");
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPfamId">the center of network</param>
        /// <param name="pfamRelNumEntryHash">key: #entries (interacting), value: a list of interacting Pfams</param>
        /// <param name="pfamNumEntryTable">Pfam and its number of entries containing it</param>
        private void WritePfamnetworkGraphml(string centerPfamId, Dictionary<int, List<string>> pfamRelNumEntryHash, DataTable pfamNumEntryTable)
        {
            string pfamNetFile = Path.Combine(pfamNetFileDir, centerPfamId + ".xml");
            StreamWriter dataWriter = new StreamWriter(pfamNetFile);
            dataWriter.WriteLine("<graphml>");
            dataWriter.WriteLine("    <key id=\"label\" for=\"all\" attr.name=\"label\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <key id=\"weight\" for=\"all\" attr.name=\"weight\" attr.type=\"integer\"/>");
            dataWriter.WriteLine("    <key id=\"link\" for=\"all\" attr.name=\"link\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <graph>");

            int nodeId = 1;
            string nodeString = "";
            string edgeString = "";
            int numOfEntriesRel = 0;
            int numOfEntriesPfam = 0;

            List<int> numList = new List<int> (pfamRelNumEntryHash.Keys);
            numList.Sort();
   //         int centerPfamNum = (int)numList[numList.Count - 1] + 10;
            int centerPfamNum = GetPfamNumOfEntries(centerPfamId, pfamNumEntryTable);

            // nodes
          //  string centerPfamNodeString = FormatNodeString(nodeId, centerPfamId, centerPfamId, centerPfamNum);
            string centerPfamNodeString = FormatNodeString(nodeId, centerPfamId, centerPfamNum);
            dataWriter.WriteLine(centerPfamNodeString);

            string centerEdgeString = FormatCenterEdgeString(centerPfamId, pfamRelNumEntryHash);
            if (centerEdgeString != "")
            {
                dataWriter.WriteLine(centerEdgeString);
            }

            for (int i = numList.Count - 1; i >= 0; i--)
            {
                numOfEntriesRel = (int)numList[i];
                List<string> relPfamList = pfamRelNumEntryHash[numOfEntriesRel];
                relPfamList.Sort();
                foreach (string relPfamId in relPfamList)
                {
                    if (relPfamId == centerPfamId)
                    {
                        continue;
                    }
                    nodeId++;

                    numOfEntriesPfam = GetPfamNumOfEntries(relPfamId, pfamNumEntryTable);
                    nodeString = FormatNodeString(nodeId, relPfamId, numOfEntriesPfam);
                    dataWriter.WriteLine(nodeString);
                    
                    edgeString = FormatEdgeString(1, nodeId, centerPfamId, relPfamId, numOfEntriesRel);
                    dataWriter.WriteLine(edgeString);
                }
            }

            dataWriter.WriteLine("    </graph>");
            dataWriter.WriteLine("</graphml>");
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPfamId"></param>
        /// <param name="numPfamsHash"></param>
        /// <returns></returns>
        public string FormatCenterEdgeString(string centerPfamId, Dictionary<int, List<string>> numPfamsHash)
        {
            int centerPfamNumEntries = GetCenterPfamRelNumEntries(centerPfamId, numPfamsHash);
            if (centerPfamNumEntries == 0)
            {
                return "";
            }
            string centerEdgeString = FormatEdgeString(1, 1, centerPfamId, centerPfamId, centerPfamNumEntries);
            return centerEdgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPfamId"></param>
        /// <param name="numPfamsHash"></param>
        /// <returns></returns>
        private int GetCenterPfamRelNumEntries(string centerPfamId, Dictionary<int, List<string>> numPfamsHash)
        {
            int centerRelNumEntries = 0;
            foreach (int numOfEntries in numPfamsHash.Keys)
            {
                foreach (string pfamId in numPfamsHash[numOfEntries])
                {
                    if (pfamId == centerPfamId)
                    {
                        centerRelNumEntries = numOfEntries;
                        return centerRelNumEntries;
                    }
                }
            }
            return centerRelNumEntries;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamsInPdb()
        {
            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> pfamIdList = new List<string> ();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                pfamIdList.Add(pfamId);
            }
            return pfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetUpdatePfams(string[] updateEntries)
        {
            List<string> updatePfamList = new List<string> ();
            foreach (string pdbId in updateEntries)
            {
                string[] entryPfamIds = GetEntryPfams(pdbId);
                foreach (string pfamId in entryPfamIds)
                {
                    if (!updatePfamList.Contains(pfamId))
                    {
                        updatePfamList.Add(pfamId);
                    }
                }
            }
            List<string> allUpdatePfamList = new List<string> (updatePfamList);
            foreach (string pfamId in updatePfamList)
            {
                string[] interactPfams = GetInteractPfams(pfamId);
                foreach (string interactPfam in interactPfams)
                {
                    if (!allUpdatePfamList.Contains(interactPfam))
                    {
                        allUpdatePfamList.Add(interactPfam);
                    }
                }
            }
            return allUpdatePfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetInteractPfams(string pfamId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> interactPfamIdList = new List<string> ();
            string interactPfamId = "";
            foreach (DataRow pfamPairRow in pfamPairTable.Rows)
            {
                interactPfamId = pfamPairRow["FamilyCode1"].ToString ().TrimEnd ();
                if (interactPfamId != pfamId)
                {
                    if (!interactPfamIdList.Contains(interactPfamId))
                    {
                        interactPfamIdList.Add(interactPfamId);
                    }
                }
            }
            return interactPfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPfams(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] entryPfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                entryPfamIds[count] = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return entryPfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetRelationNumOfEntries()
        {
            string queryString = "Select RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);
            relationTable.Columns.Add(new DataColumn ("NumOfEntries"));
            int relSeqId = 0;
            int relNumOfEntries = 0;
            foreach (DataRow relSeqRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString ());
                relNumOfEntries = GetRelationNumOfEntries(relSeqId);
                relSeqRow["NumOfEntries"] = relNumOfEntries;
            }
            relationTable.AcceptChanges();
            return relationTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetPfamNumOfEntries()
        {
            string queryString = "Select Pfam_ID, Count(Distinct PdbID) As NumOfEntries From PdbPfam Group By Pfam_ID;";
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamEntryTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamNumEntriesTable"></param>
        /// <returns></returns>
        private int GetPfamNumOfEntries(string pfamId, DataTable pfamNumEntriesTable)
        {
            DataRow[] pfamNumEntryRows = pfamNumEntriesTable.Select(string.Format ("Pfam_ID = '{0}'", pfamId));
            int pfamNumOfEntries = 0;
            if (pfamNumEntryRows.Length > 0)
            {
                pfamNumOfEntries = Convert.ToInt32 (pfamNumEntryRows[0]["NumOfEntries"].ToString());
            }
            return pfamNumOfEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int GetRelationNumOfEntries(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (relEntryTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct PdbID From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
                relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            return relEntryTable.Rows.Count;
        }
        #endregion

        #region pfam network -- biological interactions
        /// <summary>
        /// 
        /// </summary>
        public void WritePfamBiolNetwork()
        {
            int numOfCfgBiolCutoff = 2;
            pfamNetFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamBiolNetwork_M" + numOfCfgBiolCutoff.ToString ());
            if (!Directory.Exists(pfamNetFileDir))
            {
                Directory.CreateDirectory(pfamNetFileDir);
            }

            DataTable biolClusterTable = GetPfamRelationsWithBiolClusters(numOfCfgBiolCutoff);
            DataTable pfamNumEntryTable = GetPfamNumOfEntries();
            StreamWriter listFileWriter = new StreamWriter(Path.Combine (pfamNetFileDir, "ls-pfamnet.txt"));

            string[] pfamIdsWithBiolClusters = GetBiolClusterPfams(biolClusterTable);
            
            string[][] pfamClusters = ClusterBiolRelatedPfams(biolClusterTable, pfamIdsWithBiolClusters);
            int biolNetId = 1;
            listFileWriter.WriteLine("#PfamIDs: " + pfamIdsWithBiolClusters.Length.ToString ());
            foreach (string[] pfamCluster in pfamClusters)
            {
                Dictionary<string, int> pfamNumEntryHash = GetPfamNumEntryHash(pfamCluster, pfamNumEntryTable);
                WritePfamBiolNetworkGraphml(biolClusterTable, pfamCluster, pfamNumEntryHash, biolNetId);
                listFileWriter.WriteLine("BiolPfamNetwork" + biolNetId.ToString () + "\t" + FormatArrayString (pfamCluster) + "\t" + pfamCluster.Length.ToString ());
                biolNetId++;
            }
            listFileWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetPfamRelationsWithBiolClusters(int numOfCfgCutoff)
        {
            string queryString = string.Format ("Select RelSeqID, ClusterId, NumOfCfgCluster, NumOfCfgRelation, MinSeqIdentity, NumOfEntryCluster, NumOfEntryRelation " +
                " From PfamDomainClusterSumInfo Where NumOfCfgCluster >= {0} AND MinSeqIdentity < 90 " +
                " Order By RelSeqId ASC, NumOfCfgCluster DESC;", numOfCfgCutoff);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataTable biolClusterTable = clusterTable.Clone();
            biolClusterTable.Columns.Add(new DataColumn("Pfam1"));
            biolClusterTable.Columns.Add(new DataColumn("Pfam2"));
         //   biolClusterTable.Columns.Remove("RelSeqID");
          //  biolClusterTable.Columns.Remove("NumOfCfgCluster");

            List<int> relSeqIdList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                if (relSeqIdList.Contains(relSeqId))
                {
                    continue;
                }
                relSeqIdList.Add(relSeqId);
                string[] pfamPair = GetRelationPfamPair(relSeqId);

                DataRow dataRow = biolClusterTable.NewRow();
                dataRow["Pfam1"] = pfamPair[0];
                dataRow["Pfam2"] = pfamPair[1];
                dataRow["RelSeqId"] = clusterRow["RelSeqID"];
                dataRow["ClusterID"] = clusterRow["ClusterID"];
                dataRow["NumOfCfgCluster"] = clusterRow["NumOfCfgCluster"];
                dataRow["NumOfCfgRelation"] = clusterRow["NumOfCfgRelation"];
                dataRow["NumOfEntryCluster"] = clusterRow["NumOfEntryCluster"];
                dataRow["NumOfEntryRelation"] = clusterRow["NumOfEntryRelation"];
                dataRow["MinSeqIdentity"] = clusterRow["MinSeqIdentity"];
                biolClusterTable.Rows.Add(dataRow);
            }
            return biolClusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="biolClusterTable"></param>
        /// <returns></returns>
        private string[] GetBiolClusterPfams(DataTable biolClusterTable)
        {
            List<string> pfamIdList = new List<string> ();
            string pfamId = "";
            foreach (DataRow clusterRow in biolClusterTable.Rows)
            {
                pfamId = clusterRow["Pfam1"].ToString().TrimEnd();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }

                pfamId = clusterRow["Pfam2"].ToString().TrimEnd();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
            }
            string[] pfamIds = new string[pfamIdList.Count];
            pfamIdList.CopyTo(pfamIds);
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationPfamPair(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamPair = new string[2];
            if (pfamPairTable.Rows.Count > 0)
            {
                pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            }
            else
            {
                pfamPair[0] = "-";
                pfamPair[1] = "-";
            }
            return pfamPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <param name="pfamNumEntryTable"></param>
        /// <returns></returns>
        private Dictionary<string, int> GetPfamNumEntryHash(string[] pfamIds, DataTable pfamNumEntryTable)
        {
            Dictionary<string, int> pfamNumEntryHash = new Dictionary<string,int> ();
            int numOfEntries = 0;
            foreach (string pfamId in pfamIds)
            {
                numOfEntries = GetPfamNumOfEntries(pfamId, pfamNumEntryTable);
                pfamNumEntryHash.Add(pfamId, numOfEntries);
            }
            return pfamNumEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="numPfamsHash"></param>
        private void WritePfamBiolNetworkGraphml(DataTable biolClusterTable, string[] clusterPfams, Dictionary<string, int> pfamNumEntryHash, int numOfCfgClusterCutoff)
        {
            string pfamNetFile = Path.Combine(pfamNetFileDir,  "BiolPfamNetwork" + numOfCfgClusterCutoff.ToString () + ".xml");
            StreamWriter dataWriter = new StreamWriter(pfamNetFile);
            dataWriter.WriteLine("<graphml>");
            dataWriter.WriteLine("    <key id=\"label\" for=\"all\" attr.name=\"label\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <key id=\"weight\" for=\"all\" attr.name=\"weight\" attr.type=\"integer\"/>");
            dataWriter.WriteLine("    <key id=\"link\" for=\"all\" attr.name=\"link\" attr.type=\"string\"/>");
            dataWriter.WriteLine("    <graph>");

            int nodeId = 1;
            string nodeString = "";
            int numOfEntriesPfam = 0;

            foreach (string pfamId in clusterPfams)
            {
                numOfEntriesPfam = pfamNumEntryHash[pfamId];
                nodeString = FormatNodeString(nodeId, pfamId, numOfEntriesPfam);
                dataWriter.WriteLine(nodeString);
                nodeId++;
            }

            string srcPfamId = "";
            string destPfamId = "";
            int srcNodeId = 0;
            int destNodeId = 0;
            string edgeLabel = "";
            string edgeString = "";
            int edgeWeight = 0;
            for (int i = 0; i < clusterPfams.Length; i++)
            {
                srcPfamId = clusterPfams[i];
                srcNodeId = i + 1;
                for (int j = i; j < clusterPfams.Length; j++)
                {
                    destPfamId = clusterPfams[j];
                    destNodeId = j + 1;
                    DataRow[] clusterRows = biolClusterTable.Select(string.Format ("Pfam1 = '{0}' AND Pfam2 = '{1}'", srcPfamId, destPfamId));
                    if (clusterRows.Length == 0)
                    {
                        clusterRows = biolClusterTable.Select(string.Format("Pfam1 = '{0}' AND Pfam2 = '{1}'", destPfamId, srcPfamId));
                    }
                    if (clusterRows.Length > 0)
                    {
                        edgeLabel = clusterRows[0]["NumOfCfgCluster"].ToString() + "/" + clusterRows[0]["NumOfCfgRelation"].ToString()
                            + "(" + GetIntegerPercentage(clusterRows[0]["MinSeqIdentity"].ToString()) + ")";
                        edgeWeight = Convert.ToInt32 (clusterRows[0]["NumOfEntryCluster"].ToString ());
                        edgeString = FormatClusterEdgeString(srcNodeId, destNodeId, srcPfamId, destPfamId, edgeLabel, edgeWeight);
                        dataWriter.WriteLine(edgeString);
                    }
                }
            }

            dataWriter.WriteLine("    </graph>");
            dataWriter.WriteLine("</graphml>");
            dataWriter.Close();
        }

        #region cluster interacting PFAMs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="biolClusterTable"></param>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private string[][] ClusterBiolRelatedPfams(DataTable biolClusterTable, string[] pfamIds)
        {
           List<List<int>> clusterList = InitializePfamClusters(pfamIds, biolClusterTable);
            int[,] distMatrix = InitializeDistanceMatrix(pfamIds, biolClusterTable);
      //      ArrayList clusterList = InitializePfamClusters(pfamIds, distMatrix);
            ClusterBiolRelatedPfams(clusterList, distMatrix);

            string[][] pfamClusters = new string[clusterList.Count][];
            for (int i = 0; i < clusterList.Count; i ++ )
            {
                List<int> clusterIndexList = clusterList[i];
                string[] clusterPfams = new string[clusterIndexList.Count];
                for (int j = 0; j < clusterIndexList.Count; j++)
                {
                    clusterPfams[j] = pfamIds[(int)clusterIndexList[j]];
                }
                pfamClusters[i] = clusterPfams;
            }
            return pfamClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterList"></param>
        /// <param name="distMatrix"></param>
        /// <returns></returns>
        private void ClusterBiolRelatedPfams(List<List<int>> clusterList, int[,] distMatrix)
        {
            int[] mergedTwoClusters = FindMergedTwoClusters(clusterList, distMatrix);
            if (mergedTwoClusters == null)
            {
                return;
            }
            List<int> clusterI = clusterList[mergedTwoClusters[0]];
            List<int> clusterJ = clusterList[mergedTwoClusters[1]];
            clusterI.AddRange(clusterJ);
            clusterList.RemoveAt(mergedTwoClusters[1]);

            ClusterBiolRelatedPfams(clusterList, distMatrix);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterList"></param>
        /// <param name="biolClusterTable"></param>
        /// <returns></returns>
        private int[] FindMergedTwoClusters(List<List<int>> clusterList, int[,] distMatrix)
        {
            int[] mergedTwoClusters = null;
            for (int i = 0; i < clusterList.Count; i++)
            {
                for (int j = i + 1; j < clusterList.Count; j++)
                {
                    if (CanClusterBeMerged(clusterList[i], clusterList[j], distMatrix))
                    {
                        mergedTwoClusters = new int[2];
                        mergedTwoClusters[0] = i;
                        mergedTwoClusters[1] = j;
                        break;
                    }
                }
            }
            return mergedTwoClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cluster1"></param>
        /// <param name="cluster2"></param>
        /// <param name="distMatrix"></param>
        /// <returns></returns>
        private bool CanClusterBeMerged(List<int> cluster1, List<int> cluster2, int[,] distMatrix)
        {
            foreach (int clusterIndex1 in cluster1)
            {
                foreach (int clusterIndex2 in cluster2)
                {
                    if (distMatrix[clusterIndex1, clusterIndex2] == 1)
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
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private List<List<int>> InitializePfamClusters(string[] pfamIds)
        {
            List<List<int>> clusterList = new List<List<int>> ();
            for (int i = 0; i < pfamIds.Length; i ++)
            {
                List<int> clusterItemList =new List<int> ();
                clusterItemList.Add(i);
                clusterList.Add(clusterItemList);
            }
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private List<List<int>> InitializePfamClusters(string[] pfamIds, DataTable biolClusterTable)
        {
            List<List<int>> clusterList = new List<List<int>> ();
            List<string> addedPfamIdList = new List<string> ();
            for (int i = 0; i < pfamIds.Length; i++)
            {
                if (addedPfamIdList.Contains(pfamIds[i]))
                {
                    continue;
                }
                addedPfamIdList.Add(pfamIds[i]);
                List<int> clusterItemList = new List<int> ();
                clusterItemList.Add(i);
                for (int j = i + 1; j < pfamIds.Length; j++)
                {
                    if (addedPfamIdList.Contains(pfamIds[j]))
                    {
                        continue;
                    }
                    if (ArePfamsInteracting(pfamIds[i], pfamIds[j], biolClusterTable))
                    {
                        clusterItemList.Add(j);
                        addedPfamIdList.Add(pfamIds[j]);
                    }
                }
                clusterList.Add(clusterItemList);
            }
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <param name="biolClusterTable"></param>
        /// <returns></returns>
        private int[,] InitializeDistanceMatrix(string[] pfamIds, DataTable biolClusterTable)
        {
            int[,] distMatrix = new int[pfamIds.Length, pfamIds.Length];
            for (int i = 0; i < pfamIds.Length; i++)
            {
                for (int j = i; j < pfamIds.Length; j++)
                {
                    if (ArePfamsInteracting(pfamIds[i], pfamIds[j], biolClusterTable))
                    {
                        distMatrix[i, j] = 1;
                        distMatrix[j, i] = 1;
                    }
                    else
                    {
                        distMatrix[i, j] = 0;
                        distMatrix[i, j] = 0;
                    }
                }
            }
            return distMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <param name="biolClusterTable"></param>
        /// <returns></returns>
        private bool ArePfamsInteracting(string pfamId1, string pfamId2, DataTable biolClusterTable)
        {
            DataRow[] clusterRows = biolClusterTable.Select(string.Format ("Pfam1 = '{0}' AND Pfam2 = '{1}'", pfamId1, pfamId2));
            if (clusterRows.Length == 0)
            {
                clusterRows = biolClusterTable.Select(string.Format("Pfam1 = '{0}' AND Pfam2 = '{1}'", pfamId2, pfamId1));
            }
            if (clusterRows.Length > 0)
            {
                return true;
            }
            return false;
        }
        #endregion


        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent"></param>
        /// <returns></returns>
        private string GetIntegerPercentage(string percent)
        {
            int intPercent = (int)Convert.ToDouble(percent);
            return intPercent.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        private string FormatArrayString(string[] items)
        {
            string itemString = "";
            foreach (string item in items)
            {
                itemString += (item + ",");
            }
            return itemString.TrimEnd(',');
        }
        #endregion

        #region format node and edge string
        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        public string FormatEdgeString(int srcNodeId, int destNodeId, int edgeWeight)
        {
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "         <data key=\"weight\">{2}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeWeight.ToString());
            return edgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        private string FormatEdgeString(int srcNodeId, int destNodeId, string srcPfamId, string destPfamId, int edgeWeight)
        {
            string linkPage = pfamPairPage + "?PfamId1=" + srcPfamId + "&PfamId2=" + destPfamId;
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "         <data key=\"weight\">{2}</data>\r\n" +
                 "         <data key=\"link\">{3}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeWeight.ToString(), linkPage);
            return edgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        public string FormatEdgeString(int srcNodeId, int destNodeId, string srcPfamId, string destPfamId, string edgeLable, int edgeWeight)
        {
            string linkPage = pfamPairPage + "?PfamId1=" + srcPfamId + "&PfamId2=" + destPfamId;
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "         <data key=\"weight\">{3}</data>\r\n" +
                 "         <data key=\"link\">{4}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeLable, edgeWeight.ToString(), linkPage);
            return edgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        private string FormatClusterEdgeString(int srcNodeId, int destNodeId, string srcPfamId, string destPfamId, string edgeLable, int edgeWeight)
        {
            string linkPage = "../Results/ClusterInfo.aspx?PfamId1=" + srcPfamId + "&PfamId2=" + destPfamId;
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "         <data key=\"weight\">{3}</data>\r\n" +
                 "         <data key=\"link\">{4}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeLable, edgeWeight.ToString(), linkPage);
            return edgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="nodeName"></param>
        /// <param name="nodeWeight"></param>
        /// <returns></returns>
        private string FormatNodeString(int nodeId, string pfamId, int nodeWeight)
        {
            string nodeString = "";
            string linkPage = "../Results/EntityPfamArchWithPfam.aspx?PfamId=" + pfamId;
            nodeString = string.Format("    <node id = \"{0}\">\r\n" +
                "         <data key=\"label\">{1}</data>\r\n" +
                "         <data key=\"weight\">{2}</data>\r\n" +
                "         <data key=\"link\">{3}</data>\r\n" +
                "    </node>", nodeId.ToString(), pfamId, nodeWeight.ToString(), linkPage);
            return nodeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        public string FormatEdgeString(int srcNodeId, int destNodeId, string edgeLabel)
        {
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeLabel);
            return edgeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcNodeId"></param>
        /// <param name="destNodeId"></param>
        /// <returns></returns>
        public string FormatEdgeString(int srcNodeId, int destNodeId, string edgeLabel, int edgeWeight)
        {
            string edgeString = string.Format("    <edge source=\"{0}\" target=\"{1}\">\r\n" +
                "         <data key=\"label\">{2}</data>\r\n" +
                "         <data key=\"weight\">{3}</data>\r\n" +
                "    </edge>", srcNodeId.ToString(), destNodeId.ToString(), edgeLabel, edgeWeight.ToString());
            return edgeString;
        }
        #endregion

    }     
}
