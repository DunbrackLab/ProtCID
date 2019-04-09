using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamLigandClusterHmm
    {
        #region member variables
        private DataTable pfamLigandClusterTable = null;
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private string tableName = PfamLigandTableNames.pfamLigandClusterHmmTableName;     // PfamLigandClustersHmm
 //       private string tableName = PfamLigandTableNames.pfamLigandClusterTableName;  // PfamLigandClusters
        public string[] excludedLigands = { "GOL", "EDO" };

        private const double firstInitialJscoreCutoff = 0.85;  // use to group ligands for initialization to reduce the hierarchical tree depth
        private const double mergeJscoreCutoff = 0.40; // the average jscore used to merge two clusters
        private Clustering.Clustering hCluster = new Clustering.Clustering ();
        #endregion

        public PfamLigandClusterHmm ()
        {
            InitializeHCluster();

            pfamLigandClusterTable = new DataTable(tableName);
            string[] clusterCols = { "PfamID", "ClusterID", "PdbID", "ChainDomainID", "LigandChain" };
            foreach (string col in clusterCols)
            {
                pfamLigandClusterTable.Columns.Add(new DataColumn(col));
            }
        }

        private void InitializeHCluster()
        {
            hCluster.MergeQCutoff = mergeJscoreCutoff;
            hCluster.FirstQCutoff = firstInitialJscoreCutoff;
        }

        #region cluster by sharing Pfam HMM positions
        /// <summary>
        /// 
        /// </summary>
        public void ClusterPfamLigandsByPfamHmm()
        {
            bool isUpdate = true;
            CreateDbTable (isUpdate);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster Ligands in a Pfam");  

            string[] ligandPfams = GetLigandInteractingPfams();
            /* test cases to determine the J score cutoff for clustering
            string[] ligandPfams = { "14-3-3", "1-cysPrx_C", "2-Hacid_dh", "2-Hacid_dh_C", "2-oxoacid_dh", 
                                       "2-ph_phosp", "23S_rRNA_IVP", "2Fe-2S_thioredx", "2OG-FeII_Oxy", "2OG-FeII_Oxy_2"};*/

       //     string[] ligandPfams = { "Globin", "Photo_RC", "Pkinase", "Pkinase_Tyr", "Proteasome", "V-set" };
            ProtCidSettings.progressInfo.totalOperationNum = ligandPfams.Length;
            ProtCidSettings.progressInfo.totalStepNum = ligandPfams.Length;
            
            // run Pkinase
            foreach (string pfamId in ligandPfams)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

        /*        if (string.Compare(pfamId, "V-set") <= 0)
                {
                    continue;
                }
                if (IsPfamLigandsClustered(pfamId))
                {
                    continue;
                }*/

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString() + "  " + pfamId);
                try
                {
                    ClusterPfamLigandsByPfamHmm(pfamId, false);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering ligands done!");
        }

        private bool IsPfamLigandsClustered (string pfamId)
        {
            string queryString = string.Format("Select * From {0} Where PfamId = '{1}';", tableName, pfamId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (clusterTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// show tab
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamLigandPfamHmmTable(string pfamId)
        {
            string queryString = string.Format("Select * From PfamLigands Where PfamID = '{0}';", pfamId);
            DataTable pfamLigandTable = ProtCidSettings.protcidQuery.Query( queryString);
            return pfamLigandTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetLigandInteractingPfams()
        {
    //        string queryString = string.Format ("Select Distinct PfamID From {0};", PfamLigandTableNames.pfamLigandComHmmTableName);
            string queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable ligandPfamsTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[ligandPfamsTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in ligandPfamsTable.Rows)
            {
                pfamIds[count] = pfamRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePfamLigandsByPfamHmm(Dictionary<string, List<string>> updatePfamEntryDict)
        {
            bool isUpdate = true;
            CreateDbTable (isUpdate);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster Ligands in a Pfam");
            ProtCidSettings.logWriter.WriteLine("Update Clustering Ligands in a Pfam");

            List<string> updatePfamList = updatePfamEntryDict.Keys.ToList();
            updatePfamList.Sort();

            ProtCidSettings.progressInfo.totalOperationNum = updatePfamList.Count;
            ProtCidSettings.progressInfo.totalStepNum = updatePfamList.Count;

            foreach (string pfamId in updatePfamList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString() + "  " + pfamId);
                try
                {
                    /*         List<string> updateEntryList = updatePfamEntryDict[pfamId];
                             DeleteObsoleteClusterData(pfamId, updateEntryList);
                             UpdatePfamLigandsClustersHmm(pfamId, updateEntryList);*/

                    DeleteObsoleteClusterData(pfamId);
                    ClusterPfamLigandsByPfamHmm(pfamId, false);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering ligands done!");
            ProtCidSettings.logWriter.WriteLine("Clustering ligands done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeleteObsoleteClusterData (string pfamId, List<string> updateEntryList)
        {
            string deleteString = string.Format("Delete From {0} Where PfamID = '{1}' AND PdbID In ({2});", 
                tableName, pfamId, ParseHelper.FormatSqlListString (updateEntryList.ToArray ()));
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeleteObsoleteClusterData(string pfamId)
        {
            string deleteString = string.Format("Delete From {0} Where PfamID = '{1}';", tableName, pfamId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void UpdatePfamLigandsClustersHmm(string pfamId, List<string> updateEntryList)
        {
            DataTable jscoreTable = GetPfamComHmmTable(pfamId);
            List<string> pdbDomainLigandList = GetPfamChainDomainLigandList (pfamId);
            double[,] jscoreMatrix = SetConnectMatrix(pdbDomainLigandList, jscoreTable);
            List<List<int>> existClusterList = GetExistingClusterList(pfamId, pdbDomainLigandList);
            int[] updateLigandIndexes = GetUpdateLigandIndexes(updateEntryList, pdbDomainLigandList);
            List<int[]> indexClusterList = ClusterPfamLigandsIndexes(jscoreMatrix, updateLigandIndexes, existClusterList);
            int clusterId = 1;
            string[] domainLigandFields = null;
            foreach (int[] cluster in indexClusterList)
            {
                foreach (int ligandIndex in cluster)
                {
                    domainLigandFields = pdbDomainLigandList[ligandIndex].Split('_');
                    DataRow dataRow = pfamLigandClusterTable.NewRow();
                    dataRow["PfamID"] = pfamId;
                    dataRow["ClusterID"] = clusterId;
                    dataRow["PdbID"] = domainLigandFields[0];
                    dataRow["ChainDomainID"] = domainLigandFields[1];
                    dataRow["LigandChain"] = domainLigandFields[2];
                    pfamLigandClusterTable.Rows.Add(dataRow);
                }
                clusterId++;
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamLigandClusterTable);
            pfamLigandClusterTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbDomainLigandList"></param>
        /// <returns></returns>
        private  List<List<int>> GetExistingClusterList (string pfamId, List<string> pdbDomainLigandList)
        {
            string queryString = string.Format("Select Distinct ClusterID From {0} Where PfamID = '{1}';", tableName, pfamId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select Distinct ClusterID, PdbID, ChainDomainID, LigandChain From {0} Where PfamID = '{1}';", tableName, pfamId);
            DataTable ligandClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<List<int>> clusterList = new List<List<int>> ();
            string domainLigand = "";
            int ligandIndex = -1;
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                DataRow[] clusterRows = ligandClusterTable.Select(string.Format ("ClusterID = '{0}'", clusterIdRow["ClusterID"].ToString ()));
                List<int> cluster = new List<int> ();
                foreach (DataRow clusterRow in clusterRows)
                {
                    domainLigand = clusterRow["PdbID"].ToString() + "_" + clusterRow["ChainDomainID"].ToString() + "_" + clusterRow["LigandChain"].ToString().TrimEnd();
                    ligandIndex = pdbDomainLigandList.IndexOf(domainLigand);
                    if (ligandIndex >= 0)
                    {
                        cluster.Add(ligandIndex);
                    }
                }
                cluster.Sort();
                clusterList.Add(cluster);
            }
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntryList"></param>
        /// <param name="pdbDomainLigandList"></param>
        /// <returns></returns>
        private int[] GetUpdateLigandIndexes(List<string> updateEntryList, List<string> pdbDomainLigandList)
        {
            List<int> updateLigandIndexList = new List<int> ();
            foreach (string pdbId in updateEntryList)
            {
                for (int i = 0; i < pdbDomainLigandList.Count; i ++ )
                {
                    if (pdbId == pdbDomainLigandList[i].Substring(0, 4))
                    {
                        updateLigandIndexList.Add(i);
                    }
                }
            }
            return updateLigandIndexList.ToArray (); 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectMatrix"></param>
        /// <returns></returns>
        public List<int[]> ClusterPfamLigandsIndexes(double[,] jscoreMatrix, int[] updateLigandIndexes, List<List<int>> clusterList)
        {
            List<int[]> clusterIndexList = new List<int[]>();
            hCluster.UpdateClusters (updateLigandIndexes, clusterList, jscoreMatrix);
            Dictionary<int, List<int[]>> ligandCountClusterHash = new Dictionary<int,List<int[]>> ();
            int numOfLigands = 0;
            foreach (List<int> cluster in clusterList)
            {
                numOfLigands = cluster.Count;
                if (ligandCountClusterHash.ContainsKey(numOfLigands))
                {
                    ligandCountClusterHash[numOfLigands].Add(cluster.ToArray ());
                }
                else
                {
                    List<int[]> numClusterList = new List<int[]> ();
                    numClusterList.Add(cluster.ToArray ());
                    ligandCountClusterHash.Add(numOfLigands, numClusterList);
                }
            }
            List<int> numOfLigandsList = new List<int> (ligandCountClusterHash.Keys);
            numOfLigandsList.Sort();
            for (int i = numOfLigandsList.Count - 1; i >= 0; i--)
            {
                foreach (int[] cluster in ligandCountClusterHash[numOfLigandsList[i]])
                {
                    clusterIndexList.Add(cluster);
                }
            }
            return clusterIndexList;
        }
        #endregion

        #region cluster by Pfam HMM positions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void ClusterPfamLigandsByPfamHmm(string pfamId, bool deleteOld)
        {
            DataTable jscoreTable = GetPfamComHmmTable(pfamId);
            if (jscoreTable.Rows.Count == 0)
            {
                return;
            }
            bool bigPfam = false;
            List<string> pdbDomainLigandList = GetPfamChainDomainLigandList(pfamId);
            if (pdbDomainLigandList.Count > 1000)
            {
                bigPfam = true;
            }
            double[,] jscoreMatrix = SetConnectMatrix(pdbDomainLigandList, jscoreTable);
            List<int[]> indexClusterList = ClusterPfamLigandsIndexes(jscoreMatrix, bigPfam);
            //  "PfamID", "ClusterID", "PdbID", "ChainDomainID", "LigandChain" 
            int clusterId = 1;
            string[] domainLigandFields = null;
            foreach (int[] cluster in indexClusterList)
            {
                foreach (int ligandIndex in cluster)
                {
                    domainLigandFields = pdbDomainLigandList[ligandIndex].Split ('_');
                    DataRow dataRow = pfamLigandClusterTable.NewRow();
                    dataRow["PfamID"] = pfamId;
                    dataRow["ClusterID"] = clusterId;
                    dataRow["PdbID"] = domainLigandFields[0];
                    dataRow["ChainDomainID"] = domainLigandFields[1];
                    dataRow["LigandChain"] = domainLigandFields[2];
                    pfamLigandClusterTable.Rows.Add(dataRow);
                }
                clusterId++;
            }
            if (deleteOld)
            {
                DeleteObsoleteClusterData(pfamId);
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamLigandClusterTable);
            pfamLigandClusterTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamComHmmTable (string pfamId)
        {
            string queryString = string.Format("Select PdbID1, ChainDomainID1, LigandChain1, PdbID2, ChainDomainID2, LigandChain2, Jscore" + 
                " From {0} Where PfamID = '{1}';", PfamLigandTableNames.pfamLigandComHmmTableName, pfamId);
            DataTable pfamSimScoreTable = ProtCidSettings.protcidQuery.Query( queryString);
            return pfamSimScoreTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamSimScoreTable"></param>
        /// <returns></returns>
        private List<string> GetPfamChainDomainLigandList(string pfamId)
        {
            List<string> chainDomainLigandList = new List<string>();
            string entryDomainLigand = "";
            string queryString = string.Format("Select Distinct PdbID1, ChainDomainID1, LigandChain1 From {0} Where PfamID = '{1}';", PfamLigandTableNames.pfamLigandComHmmTableName, pfamId);
            DataTable domain1Table = ProtCidSettings.protcidQuery.Query(queryString);

            queryString = string.Format("Select Distinct PdbID2, ChainDomainID2, LigandChain2 From {0} Where PfamID = '{1}';", PfamLigandTableNames.pfamLigandComHmmTableName, pfamId);
            DataTable domain2Table = ProtCidSettings.protcidQuery.Query(queryString);

            foreach (DataRow domainRow in domain1Table.Rows)
            {
                entryDomainLigand = domainRow["PdbID1"].ToString() + "_" + domainRow["ChainDomainID1"].ToString() + "_" + domainRow["LigandChain1"].ToString().TrimEnd();
                chainDomainLigandList.Add(entryDomainLigand);
            }
            foreach (DataRow domainRow in domain2Table.Rows)
            {
                entryDomainLigand = domainRow["PdbID2"].ToString() + "_" + domainRow["ChainDomainID2"].ToString() + "_" + domainRow["LigandChain2"].ToString().TrimEnd();
                if (!chainDomainLigandList.Contains(entryDomainLigand))
                {
                    chainDomainLigandList.Add(entryDomainLigand);
                }
            }
            chainDomainLigandList.Sort(); // sorted in alphabet order
            return chainDomainLigandList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamSimScoreTable"></param>
        /// <returns></returns>
        private List<string> GetPfamChainDomainLigandList (DataTable pfamSimScoreTable)
        {
            List<string> chainDomainLigandList = new List<string>();
            string entryDomainLigand = "";
            foreach (DataRow simScoreRow in pfamSimScoreTable.Rows)
            {
                entryDomainLigand = simScoreRow["PdbID1"].ToString() + "_" + simScoreRow["ChainDomainID1"].ToString() + "_" + simScoreRow["LigandChain1"].ToString().TrimEnd();
                if (! chainDomainLigandList.Contains (entryDomainLigand))
                {
                    chainDomainLigandList.Add(entryDomainLigand);
                }
                entryDomainLigand = simScoreRow["PdbID2"].ToString() + "_" + simScoreRow["ChainDomainID2"].ToString() + "_" + simScoreRow["LigandChain2"].ToString().TrimEnd();
                if (!chainDomainLigandList.Contains(entryDomainLigand))
                {
                    chainDomainLigandList.Add(entryDomainLigand);
                }
            }
            chainDomainLigandList.Sort();
            return chainDomainLigandList;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectMatrix"></param>
        /// <returns></returns>
        public List<int[]> ClusterPfamLigandsIndexes (double [,] jscoreMatrix, bool bigCluster)
        {
            List<int[]> clusterIndexList = new List<int[]>();
            List<List<int>> clusterList = null;
            if (bigCluster)
            {
                clusterList = hCluster.ClusterInBig(jscoreMatrix);
            }
            else
            {
                clusterList = hCluster.Cluster(jscoreMatrix);
            }
            Dictionary<int, List<int[]>> ligandCountClusterHash = new Dictionary<int,List<int[]>> ();
            int numOfLigands = 0;
            foreach (List<int> cluster in clusterList)
            {
                numOfLigands = cluster.Count;
                if (ligandCountClusterHash.ContainsKey (numOfLigands))
                {
                    ligandCountClusterHash[numOfLigands].Add(cluster.ToArray ());
                }
                else
                {
                    List<int[]> numClusterList = new List<int[]> ();
                    numClusterList.Add(cluster.ToArray());
                    ligandCountClusterHash.Add(numOfLigands, numClusterList);
                }
            }
            List<int> numOfLigandsList = new List<int> (ligandCountClusterHash.Keys);
            numOfLigandsList.Sort();
            for (int i = numOfLigandsList.Count - 1; i >= 0; i --)
            {
                if ((int)numOfLigandsList[i] > 1)
                {
                    List<int[]> numClusterList = ligandCountClusterHash[numOfLigandsList[i]];
                    foreach (int[] clusterIndexes in numClusterList)
                    {
                        clusterIndexList.Add(clusterIndexes);
                    }
                }
            }
            return clusterIndexList;
        }

        /// <summary>
        /// can set the matrix to be 1 or 0
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private double[,] SetConnectMatrix(List<string> pdbLigandList, DataTable ligandSimScoreTable)
        {
            double[,] ligandJscoreMatrix = new double[pdbLigandList.Count, pdbLigandList.Count];
            
            string pdbLigand1 = "";
            string pdbLigand2 = "";
            int ligandIndex1 = 0;
            int ligandIndex2 = 0;
            double simScore = 0;
            foreach (DataRow jscoreRow in ligandSimScoreTable.Rows)
            {
                simScore = Convert.ToDouble(jscoreRow["Jscore"].ToString());
                pdbLigand1 = jscoreRow["PdbID1"].ToString() + "_" + jscoreRow["ChainDomainID1"].ToString() + "_" + jscoreRow["LigandChain1"].ToString().TrimEnd();
                pdbLigand2 = jscoreRow["PdbID2"].ToString() + "_" + jscoreRow["ChainDomainID2"].ToString() + "_" + jscoreRow["LigandChain2"].ToString().TrimEnd();
                ligandIndex1 = pdbLigandList.BinarySearch (pdbLigand1);
                ligandIndex2 = pdbLigandList.BinarySearch (pdbLigand2);
                ligandJscoreMatrix[ligandIndex1, ligandIndex2] = simScore;
                ligandJscoreMatrix[ligandIndex2, ligandIndex1] = simScore;
            }
  /*          for (int i = 0; i < pdbLigandList.Count; i++)
            {
                string[] ligandFields1 = pdbLigandList[i].Split('_');
                pdbId1 = ligandFields1[0];
                chainDomainId1 = ligandFields1[1];
                ligandChain1 = ligandFields1[2];
                ligandJscoreMatrix [i, i] = 1;
                for (int j = i + 1; j < pdbLigandList.Count; j++)
                {
                    string[] ligandFields2 = pdbLigandList[j].Split('_');
                    pdbId2 = ligandFields2[0];
                    chainDomainId2 = ligandFields2[1];
                    ligandChain2 = ligandFields2[2];
                    simScore = GetLigandJaccardIndex(pdbId1, chainDomainId1, ligandChain1, pdbId2, chainDomainId2, ligandChain2, ligandSimScoreTable);
                    ligandJscoreMatrix[i, j] = simScore;
                    ligandJscoreMatrix[j, i] = simScore;
                }
            }*/
            return ligandJscoreMatrix;
        }

        /// <summary>
        /// Jaccard index
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="chainDomainId1"></param>
        /// <param name="ligandChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="chainDomainId2"></param>
        /// <param name="ligandChain2"></param>
        /// <param name="pfamLigandSimScoreTable"></param>
        /// <returns></returns>
        private double GetLigandJaccardIndex (string pdbId1, string chainDomainId1, string ligandChain1, 
            string pdbId2, string chainDomainId2, string ligandChain2, DataTable pfamLigandSimScoreTable)
        {
            DataRow[] simscoreRows = pfamLigandSimScoreTable.Select(string.Format("PdbID1 = '{0}' AND ChainDomainID1 = '{1}' AND LigandChain1 = '{2}' AND " + 
                            "PdbID2 = '{3}' AND ChainDomainID2 = '{4}' AND LigandChain2 = '{5}'", 
                            pdbId1, chainDomainId1, ligandChain1, pdbId2, chainDomainId2, ligandChain2));
            if (simscoreRows.Length > 0)
            {
                return Convert.ToDouble(simscoreRows[0]["Jscore"].ToString ());
            }
            return 0;
        }
        #endregion

        #region cluster tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void CreateDbTable (bool isUpdate)
        {           
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + tableName + " ( " +
                    "PfamID varchar(40) NOT NULL, " +
                    "ClusterID Integer NOT NULL, " +
                    "PdbID char(4) Not Null, " +
                    "ChainDomainID Integer Not Null, " +
                    "LigandChain char(3) Not Null);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
                string createIndexString = "Create Index " + tableName + "_cluster ON " + tableName + "(PfamID, ClusterID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
                createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PdbID, ChainDomainID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            }
        }
        #endregion
    }
}
