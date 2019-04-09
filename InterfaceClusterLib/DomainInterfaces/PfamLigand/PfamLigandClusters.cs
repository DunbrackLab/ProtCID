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
    public class PfamLigandClusters
    {
        #region member variables
        private DataTable pfamLigandClusterTable = null;
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private string tableName = PfamLigandTableNames.pfamLigandClusterTableName;
        public string[] excludedLigands = {"GOL", "EDO"};
        #endregion

        public PfamLigandClusters ()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
                if (ProtCidSettings.protcidDbConnection == null)
                {
                    ProtCidSettings.protcidDbConnection = new DbConnect();
                    ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                                                        ProtCidSettings.dirSettings.protcidDbPath;
                }

                if (ProtCidSettings.pdbfamDbConnection == null)
                {
                    ProtCidSettings.pdbfamDbConnection = new DbConnect();
                    ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                                                       ProtCidSettings.dirSettings.pdbfamDbPath;
                }
            }
        }

        #region new - cluster by ligands overlapping
        /// <summary>
        /// 
        /// </summary>
        public void ClusterPfamLigands ()
        {
            CreateTables(true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster Ligands in a Pfam");

            string queryString = string.Format("Select Distinct PfamID From {0};", PfamLigandTableNames.pfamLigandComAtomTableName);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = pfamIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamIdTable.Rows.Count;

            string[] clusteredPfamIds = GetPfamsClusteredInDb();

            string pfamId = "";
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;               

                if (Array.BinarySearch (clusteredPfamIds, pfamId) > -1)
                {
                    continue;
                }
                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString() + "  " + pfamId);
                try
                {
                    ClusterPfamLigands(pfamId);
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamsClusteredInDb ()
        {
            string queryString = "Select Distinct PfamID From " + tableName + ";";
            DataTable pfamIdsTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusteredPfamIds = new string[pfamIdsTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdsTable.Rows)
            {
                clusteredPfamIds[count] = pfamIdRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return clusteredPfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void ClusterPfamLigands(string pfamId)
        {
            string queryString = string.Format("Select PfamID, PdbID1, ChainDomainID1, LigandChain1, LigandFileChain1, " +
                " PdbID2, ChainDomainID2, LigandChain2, LigandChain2, " + 
                " DomainRmsd, NumComAtoms1, NumComAtoms2 From {0} Where PfamID = '{1}';", PfamLigandTableNames.pfamLigandComAtomTableName, pfamId);
            DataTable ligandComAtomTable = ProtCidSettings.protcidQuery.Query( queryString);
            ClusterPfamLigands(ligandComAtomTable);
        }
        #endregion

        #region update - cluster by ligands overlapping
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatePfamEntryListDict"></param>
        public void UpdatePfamLigandsClusters (Dictionary<string, List<string>> updatePfamEntryListDict)
        {
            List<string> updatePfamList = updatePfamEntryListDict.Keys.ToList();
            updatePfamList.Sort();

            string[] updatePfams = new string[updatePfamList.Count];
            updatePfamList.CopyTo(updatePfams);
            try
            {
                UpdatePfamLigandsClustersInPfam(updatePfams);
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine("Update ligand clustering error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private List<string> GetUpdatePfams (string[] updateEntries)
        {
            string queryString = "";
            List<string> pfamIdList = new List<string>();
            string pfamId = "";
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                string[] subUpdateEntries = ParseHelper.GetSubArray(updateEntries, i, 300);
                queryString = string.Format("Select Distinct PfamID, PdbID From PfamLigands Where PdbID IN ({0});", ParseHelper.FormatSqlListString (subUpdateEntries));
                DataTable entryPfamTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow entryPfamRow in entryPfamTable.Rows)
                {
                    pfamId = entryPfamRow["PfamID"].ToString().TrimEnd();
                    if (! pfamIdList.Contains (pfamId))
                    {
                        pfamIdList.Add(pfamId);
                    }
                }
            }
            return pfamIdList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatePfams"></param>
        public void UpdatePfamLigandsClustersInPfam (string[] updatePfams)
        {
            CreateTables(true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updatePfams.Length;
            ProtCidSettings.progressInfo.totalStepNum = updatePfams.Length;

            foreach (string pfamId in updatePfams)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentStepNum.ToString () + " " + pfamId);
                try
                {
                    DeletePfamLigandClusters(pfamId);
                    ClusterPfamLigands(pfamId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Update ligand clustering error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeletePfamLigandClusters(string pfamId)
        {
            string deleteString = string.Format("Delete From {0} Where PfamID = '{1}';", tableName, pfamId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion      

        #region cluster by atom overlap, single linkage
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        public void ClusterPfamLigands (DataTable ligandComAtomTable)
        {
   //         string[] clusterCols = { "PfamID", "ClusterID", "PdbID", "ChainDomainID", "LigandChain" };
            List<string> pdbLigandList = GetPfamLigandsInOrder (ligandComAtomTable);
            int[,] connectMatrix = SetConnectMatrix(pdbLigandList, ligandComAtomTable);
            List<List<int>> clusterList = ClusterPfamLigands(connectMatrix);
             List<List<int>> sortedClusterList = SortClustersInLigandNumbers(clusterList);
            int clusterId = 1;
            string[] ligandFields = null;
            string pfamId = ligandComAtomTable.Rows[0]["PfamID"].ToString();
            foreach (List<int> cluster in sortedClusterList)
            {
                foreach (int ligandIndex in cluster)
                {
                    ligandFields = pdbLigandList[ligandIndex].Split('_');
                    DataRow clusterRow = pfamLigandClusterTable.NewRow();
                    clusterRow["PfamID"] = pfamId;
                    clusterRow["ClusterID"] = clusterId;
                    clusterRow["PdbID"] = ligandFields[0];
                    clusterRow["ChainDomainID"] = ligandFields[1];
                    clusterRow["LigandChain"] = ligandFields[2];
                    clusterRow["LigandFileChain"] = ligandFields[3];
                    pfamLigandClusterTable.Rows.Add(clusterRow);
                }
                clusterId++;
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamLigandClusterTable);
            pfamLigandClusterTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterList"></param>
        private List<List<int>> SortClustersInLigandNumbers(List<List<int>> clusterList)
        {
            Dictionary<int, List<List<int>>> ligandNumClusterDic = new Dictionary<int, List<List<int>>>();
            int numOfLigandsInCluster = 0;
            foreach (List<int> cluster in clusterList)
            {
                numOfLigandsInCluster = cluster.Count;
                if (ligandNumClusterDic.ContainsKey (numOfLigandsInCluster))
                {
                    List<List<int>> numClusterList = ligandNumClusterDic[numOfLigandsInCluster];
                    numClusterList.Add(cluster);
                }
                else
                {
                    List<List<int>> numClusterList = new List<List<int>>();
                    numClusterList.Add(cluster);
                    ligandNumClusterDic.Add(numOfLigandsInCluster, numClusterList);
                }
            }
            List<int> numOfLigandsClusterList = new List<int>();
            numOfLigandsClusterList.AddRange(ligandNumClusterDic.Keys);
            numOfLigandsClusterList.Sort();
            List<List<int>> orderedClusterList = new List<List<int>>();
            for (int i = numOfLigandsClusterList.Count - 1; i >= 0; i --)
            {
                List<List<int>> numClusterList = ligandNumClusterDic[numOfLigandsClusterList[i]];
                orderedClusterList.AddRange(numClusterList);
            }
            return orderedClusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectMatrix"></param>
        /// <returns></returns>
        public List<List<int>> ClusterPfamLigands (int[,] connectMatrix)
        {
            List<List<int>> clusterList = new List<List<int>>();
            int numOfLigands = connectMatrix.GetLength(0);
            List<int> cluster = null;
            List<int> addedIndexList = new List<int>();
            for (int i = 0; i < numOfLigands; i++)
            {
                if (addedIndexList.Contains(i))
                {
                    continue;
                }
                cluster = new List<int>();
                cluster.Add(i);
                addedIndexList.Add(i);
                for (int j = i + 1; j < numOfLigands; j ++ )
                {
                    if (addedIndexList.Contains(j))
                    {
                        continue;
                    }
                    if (connectMatrix[i, j] >= 1)
                    {
                        cluster.Add(j);
                        addedIndexList.Add(j);
                    }
                }
                clusterList.Add(cluster);
            }
            return clusterList;
        }

        /// <summary>
        /// can set the matrix to be 1 or 0
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private int[,] SetConnectMatrix (List<string> pdbLigandList, DataTable ligandComAtomTable)
        {            
            int[,] comAtomNumDistMatrix = new int[pdbLigandList.Count, pdbLigandList.Count];
            string pdbId1 = "";
            string ligandChain1 = "";
            string pdbId2 = "";
            string ligandChain2 = "";
            for (int i = 0; i < pdbLigandList.Count; i ++)
            {
                string[] ligandFields1 = pdbLigandList[i].Split('_');
                pdbId1 = ligandFields1[0];
                ligandChain1 = ligandFields1[2];
                comAtomNumDistMatrix[i, i] = 1;
                for (int j = i + 1; j < pdbLigandList.Count; j++)
                {
                    string[] ligandFields2 = pdbLigandList[j].Split('_');
                    pdbId2 = ligandFields2[0];
                    ligandChain2 = ligandFields2[2];                   
                    int[] comAtomNums = GetNumOfComAtoms(pdbId1, ligandChain1, pdbId2, ligandChain2, ligandComAtomTable);
                    comAtomNumDistMatrix[i, j] = comAtomNums[0];
                    comAtomNumDistMatrix[j, i] = comAtomNums[1];
                }
            }
            return comAtomNumDistMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="ligandChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="ligandChain2"></param>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private int[] GetNumOfComAtoms (string pdbId1, string ligandChain1, string pdbId2, string ligandChain2, DataTable ligandComAtomTable)
        {
            int[] comAtomNums = new int[2];
            DataRow[] comAtomRows = ligandComAtomTable.Select(string.Format ("PdbID1 = '{0}' AND LigandChain1 = '{1}' AND PdbID2 = '{2}' AND LigandChain2 = '{3}'", 
                pdbId1, ligandChain1, pdbId2, ligandChain2));
            
            if (comAtomRows.Length > 0)
            {
                comAtomNums[0] = Convert.ToInt32 (comAtomRows[0]["NumComAtoms1"].ToString());
                comAtomNums[1] = Convert.ToInt32(comAtomRows[0]["NumComAtoms2"].ToString ());
            }
            else if (comAtomRows.Length == 0)
            {
                comAtomRows = ligandComAtomTable.Select(string.Format("PdbID1 = '{0}' AND LigandChain1 = '{1}' AND PdbID2 = '{2}' AND LigandChain2 = '{3}'",
                pdbId2, ligandChain2, pdbId1, ligandChain1));
                if (comAtomRows.Length > 0)
                {
                    comAtomNums[0] = Convert.ToInt32(comAtomRows[0]["NumComAtoms2"].ToString());
                    comAtomNums[1] = Convert.ToInt32(comAtomRows[0]["NumComAtoms1"].ToString());
                }
            }
            return comAtomNums;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private List<string> GetPfamLigands (DataTable ligandComAtomTable)
        {
            List<string> pfamLigandList = new List<string>();
            string pdbLigand = "";
            foreach (DataRow ligandPairRow in ligandComAtomTable.Rows)
            {
                pdbLigand = ligandPairRow["PdbID1"].ToString() + ligandPairRow["LigandChain1"].ToString().TrimEnd() +
                    "_" + ligandPairRow["LigandFileChain1"].ToString().TrimEnd();
                if (! pfamLigandList.Contains (pdbLigand))
                {
                    pfamLigandList.Add(pdbLigand);
                }
                pdbLigand = ligandPairRow["PdbID2"].ToString() + ligandPairRow["LigandChain2"].ToString().TrimEnd() +
                    "_" + ligandPairRow["LigandFileChain2"].ToString().TrimEnd();
                if (!pfamLigandList.Contains(pdbLigand))
                {
                    pfamLigandList.Add(pdbLigand);
                }
            }
            return pfamLigandList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private List<string> GetPfamLigandsInOrder (DataTable ligandComAtomTable)
        {
            Dictionary<string, int> ligandNumHash = GetPfamLigandsComNumbers(ligandComAtomTable);
            List<string> pdbLigandsInOrder = OrderPdbLigandsByComNumbers(ligandNumHash);
            return pdbLigandsInOrder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandComAtomTable"></param>
        /// <returns></returns>
        private Dictionary<string, int> GetPfamLigandsComNumbers (DataTable ligandComAtomTable)
        {
            Dictionary<string, int> ligandNumHash = new Dictionary<string, int>();
            string pdbLigand = "";
            foreach (DataRow ligandPairRow in ligandComAtomTable.Rows)
            {
                pdbLigand = ligandPairRow["PdbID1"].ToString() + "_" + ligandPairRow["ChainDomainID1"].ToString ()
                    + "_" + ligandPairRow["LigandChain1"].ToString().TrimEnd() 
                    + "_" + ligandPairRow["LigandFileChain1"].ToString().TrimEnd();
                if (ligandNumHash.ContainsKey (pdbLigand))
                {
                    int numOfLigands = (int)ligandNumHash[pdbLigand];
                    numOfLigands++;
                    ligandNumHash[pdbLigand] = numOfLigands;
                }
                else
                {
                    ligandNumHash.Add(pdbLigand, 1);
                }
            }
            return ligandNumHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandNumHash"></param>
        /// <returns></returns>
        private List<string> OrderPdbLigandsByComNumbers (Dictionary<string, int> ligandNumHash)
        {
            Dictionary<int, List<string>> numLigandListHash = new Dictionary<int, List<string>>();
            int numComLigands = 0;
            foreach (string pdbLigand in ligandNumHash.Keys)
            {
                numComLigands = (int)ligandNumHash[pdbLigand];
                if (numLigandListHash.ContainsKey(numComLigands))
                {
                    List<string> ligandList = (List<string>)numLigandListHash[numComLigands];
                    if (!ligandList.Contains(pdbLigand))
                    {
                        ligandList.Add(pdbLigand);
                    }
                }
                else
                {
                    List<string> ligandList = new List<string>();
                    ligandList.Add(pdbLigand);
                    numLigandListHash.Add(numComLigands, ligandList);
                }
            }

            List<int> numComLigandsList = new List<int>();
            numComLigandsList.AddRange(numLigandListHash.Keys);
            numComLigandsList.Sort ();
            List<string> pdbLigandsInOrder = new List<string>();
            for (int i = numComLigandsList.Count - 1; i >= 0; i -- )
            {
                List<string> ligandList= (List<string>) numLigandListHash[numComLigandsList[i]];
                ligandList.Sort();
                pdbLigandsInOrder.AddRange(ligandList);
            }
            return pdbLigandsInOrder;
        }
        #endregion

        #region cluster tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void CreateTables (bool isUpdate)
        {           
            pfamLigandClusterTable = new DataTable(tableName);
            string[] clusterCols = {"PfamID", "ClusterID", "PdbID", "ChainDomainID", "LigandChain", "LigandFileChain"};
            foreach (string col in clusterCols)
            {
                pfamLigandClusterTable.Columns.Add(new DataColumn (col));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + tableName + " ( " +
                    "PfamID varchar(40) NOT NULL, " +
                    "ClusterID Integer NOT NULL, " +
                    "PdbID char(4) Not Null, " +
                    "ChainDomainID Integer Not Null, " +
                    "LigandChain char(3) Not Null, " +
                    "LigandFileChain char(3) Not Null);";
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
