using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.Clustering;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PeptideInterfaceCluster
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private DbInsert dbInsert = new DbInsert();
        public int numOfComHmmSiteCutoff = 3;  // the minimum number of common hmm sites with which a peptide interface can be added into the group
        public double numOfComHmmSitesGood = 10; // if the number of common hmm sites >=, then add the interface despite of RMSD
        public double rmsdMax = 10.0;  // if RMSD is greater than rmsdMax and numOfComHmmSites < numOfComHmmSitesGood, then the interface cannot be added
        public string tableName = "PfamPepInterfaceClusters";
        #endregion

        #region clustering pfam peptide interfaces
        /// <summary>
        /// cluster peptide interfaces by number of common hmm positions
        /// </summary>
        public void ClusterPeptideInterfaces()
        {
            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
            Clustering.Clustering hcluster = new InterfaceClusterLib.Clustering.Clustering ();
            hcluster.MergeQCutoff = numOfComHmmSiteCutoff;
            hcluster.FirstQCutoff = numOfComHmmSitesGood;

            bool isUpdate = false;
            DataTable peptideInterfaceClusterTable = CreateDataTable(isUpdate);

            string queryString = "Select PdbId, AsymID, EntityID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string[] pfamIds = GetPepPfamIds();
  //          string[] pfamIds = {/* "Flu_M2", */"Hormone_recep", "Peptidase_S9"/*, "Peptidase_S9_N"*/};

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering peptide interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering peptide interfaces");

            StreamWriter dataWriter = null;

            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }
            dataWriter = new StreamWriter(Path.Combine(clusterFileDir, peptideInterfaceClusterTable.TableName + ".txt"), true);
            dataWriter.WriteLine("PfamID\tClusterID\tRelSeqID\tPdbID\tDomainInterfaceID\tProtPfamId\tProtUnpCode\tProtPfamArch\t" +
                "PepPfamID\tPepUnpCode\tProtLength\tPeptideLength\tSurfaceArea\tEntryPfamArch");
            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    ClusterPfamPeptideInterfaces(pfamId, hcluster, peptideInterfaceClusterTable, dataWriter, clusterFileDir, asuTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " clustering peptide interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " clustering peptide interfaces errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        public void UpdatePeptideInterfaceClusters(string[] pfamIds)
        {
            int numOfComHmmSiteCutoff = 3;
            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
            Clustering.Clustering hcluster = new InterfaceClusterLib.Clustering.Clustering();
            hcluster.MergeQCutoff = numOfComHmmSiteCutoff;
            hcluster.FirstQCutoff = 10;

            bool isUpdate = true;
            DataTable peptideInterfaceClusterTable = CreateDataTable(isUpdate);

            string queryString = "Select PdbId, AsymID, EntityID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering peptide interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering peptide interfaces");

            //      int[] numOfCommonHmmSitesCutoffs = {2, 3, 4, 5, 6 };
            StreamWriter dataWriter = null;

            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }
            dataWriter = new StreamWriter(Path.Combine(clusterFileDir, "PfamPepInterfaceClusters" + numOfComHmmSiteCutoff.ToString() + ".txt"));
            dataWriter.WriteLine("PfamID\tClusterID\tRelSeqID\tPdbID\tDomainInterfaceID\tProtPfamId\tProtUnpCode\tProtPfamArch\t" +
                "PepPfamID\tPepUnpCode\tProtLength\tPeptideLength\tEntryPfamArch");
            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeletePfamClusterInfo(pfamId, peptideInterfaceClusterTable.TableName);

                    ClusterPfamPeptideInterfaces(pfamId, hcluster, peptideInterfaceClusterTable, dataWriter, clusterFileDir, asuTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " clustering peptide interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " clustering peptide interfaces errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="hCluster"></param>
        /// <param name="dataWriter"></param>
        private void ClusterPfamPeptideInterfaces(string pfamId, Clustering.Clustering hCluster, DataTable pepInterfaceClusterTable,
            StreamWriter dataWriter, string dataDir, DataTable asuTable)
        {
            //       string pfamAcc = GetPfamAccFromPfamId (pfamId);
            DataTable hmmCompTable = ReadHmmSiteCompTable(pfamId);

            DataTable peptideInterfaceTable = GetPeptideInterfaceTable(pfamId);

            string[] domainInterfaces = GetPfamDomainInterfaces(hmmCompTable, peptideInterfaceTable);

            string[] peptideEntries = GetPeptideEntries(peptideInterfaceTable);
            DataTable pfamAssignTable = GetPfamAssignTable(peptideEntries);
            DataTable unpRefTable = GetUnpRefTable(peptideEntries);
            DataTable entryPfamArchTable = GetEntryPfamArchTable(peptideEntries);
            DataTable chainPeptideInterfaceTable = GetChainPeptideInterfaceTable(peptideEntries);
            DataTable chainPfamArchTable = GetChainPfamArchTable(peptideEntries);

            double[,] comHmmSitesMatrix = CreateDistanceMatrix(domainInterfaces, hmmCompTable, "NumOfCommonHmmSites");
            double[,] rmsdMatrix = CreateRmsdDistanceMatrix (domainInterfaces, hmmCompTable);

            List<List<int>> clusters = hCluster.Cluster(comHmmSitesMatrix, rmsdMatrix, numOfComHmmSitesGood, rmsdMax);
            List<int[]> sortedClusters = SortClustersByNumInterfaces(clusters);

            int clusterId = 1;
            string dataLine = "";
            string domainInterface = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            string entryPfamArch = "";
            int relSeqId = 0;
            double surfaceArea = 0;
            List<string> clusterDomainInterfaceList = new List<string> ();
            int[] chainLengths = null;
            string[] pfamUnpInfos = null;
            foreach (int[] cluster in sortedClusters)
            {
                if (cluster.Length < 2)
                {
                    continue;
                }
                clusterDomainInterfaceList.Clear();
                foreach (int interfaceIndex in cluster)
                {
                    try
                    {
                        domainInterface = domainInterfaces[interfaceIndex];
                        pdbId = domainInterface.Substring(0, 4);
                        entryPfamArch = GetEntryPfamArch(pdbId, entryPfamArchTable);
                        domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));
                        relSeqId = GetDomainInterfaceRelSeqID(pdbId, domainInterfaceId, peptideInterfaceTable);
                        surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, peptideInterfaceTable);
                        if (relSeqId == -1)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + domainInterface + " clustering errors: not defined in the interface table");
                            ProtCidSettings.logWriter.WriteLine(pfamId + " " + domainInterface + " clustering errors: not defined in the interface table");
                            ProtCidSettings.logWriter.Flush();
                            continue;
                        }
                        try
                        {
                            pfamUnpInfos = GetPeptideDomainInterfacePfamUnpInfo(pdbId, domainInterfaceId,
                                peptideInterfaceTable, chainPeptideInterfaceTable, pfamAssignTable, asuTable, unpRefTable, chainPfamArchTable, out chainLengths);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.logWriter.WriteLine(pdbId + " d" + domainInterfaceId + " error: " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                            continue;
                        }
                        dataLine = pfamId + "\t" + clusterId.ToString() + "\t" + relSeqId.ToString() + "\t" + pdbId + "\t" + domainInterfaceId.ToString() + "\t" +
                            pfamUnpInfos[0] + "\t" + pfamUnpInfos[1] + "\t" + pfamUnpInfos[2] + "\t" +
                            pfamUnpInfos[3] + "\t" + pfamUnpInfos[4] + "\t" + chainLengths[0].ToString() + "\t" + chainLengths[1] + "\t" + entryPfamArch;
                        dataWriter.WriteLine(dataLine);
                        DataRow dataRow = pepInterfaceClusterTable.NewRow();
                        dataRow["PfamID"] = pfamId;
                        dataRow["ClusterID"] = clusterId;
                        dataRow["RelSeqID"] = relSeqId;
                        dataRow["PdbID"] = pdbId;
                        dataRow["DomainInterfaceID"] = domainInterfaceId;
                        dataRow["UnpCode"] = pfamUnpInfos[1];
                        dataRow["PfamArch"] = pfamUnpInfos[2];
                        dataRow["PepPfamID"] = pfamUnpInfos[3];
                        dataRow["PepUnpCode"] = pfamUnpInfos[4];
                        dataRow["SeqLength"] = chainLengths[0];
                        dataRow["PepSeqLength"] = chainLengths[1];
                        dataRow["EntryPfamArch"] = entryPfamArch;
                        dataRow["SurfaceArea"] = surfaceArea;
                        pepInterfaceClusterTable.Rows.Add(dataRow);
                        clusterDomainInterfaceList.Add(domainInterface);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + interfaceIndex.ToString () + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pfamId + " " + interfaceIndex.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
                clusterId++;
            }

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pepInterfaceClusterTable);
            pepInterfaceClusterTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterList"></param>
        /// <returns></returns>
        private List<int[]> SortClustersByNumInterfaces (List<List<int>> clusterList)
        {
            Dictionary<int, List<int[]>> numClusterHash = new Dictionary<int,List<int[]>> ();
            int numOfInterfaces = 0;
            foreach (List<int> cluster in clusterList)
            {
                numOfInterfaces = cluster.Count;
                int[] clusterItems = new int[cluster.Count];
                cluster.CopyTo(clusterItems);
                if (numClusterHash.ContainsKey (numOfInterfaces))
                {
                    numClusterHash[numOfInterfaces].Add(clusterItems);
                }
                else
                {
                    List<int[]> thisNumClusterList = new List<int[]> ();
                    thisNumClusterList.Add(clusterItems);
                    numClusterHash.Add(numOfInterfaces, thisNumClusterList);
                }
            }
            List<int> numList = new List<int> (numClusterHash.Keys);
            numList.Sort();
            List<int[]> sortedClusterList = new List<int[]> ();
            for (int i = numList.Count - 1; i >= 0; i --)
            {
                sortedClusterList.AddRange(numClusterHash[numList[i]]);
            }
            return sortedClusterList;
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="hmmCompTable"></param>
        /// <returns></returns>
        private double[,] CreateDistanceMatrix (string[] domainInterfaces, DataTable hmmCompTable, string distColName)
        {
            double[,] distMatrix = new double[domainInterfaces.Length, domainInterfaces.Length];
            string domainInterface1 = "";
            string domainInterface2 = "";
            int interfaceIndex1 = 0;
            int interfaceIndex2 = 0;
            foreach (DataRow hmmCompRow in hmmCompTable.Rows)
            {
                domainInterface1 = hmmCompRow["PdbID1"].ToString() + hmmCompRow["DomainInterfaceID1"].ToString();
                domainInterface2 = hmmCompRow["PdbID2"].ToString() + hmmCompRow["DomainInterfaceID2"].ToString();

                interfaceIndex1 = Array.IndexOf(domainInterfaces, domainInterface1);
                interfaceIndex2 = Array.IndexOf(domainInterfaces, domainInterface2);

                if (interfaceIndex1 > -1 && interfaceIndex2 > -1)
                {
                    distMatrix[interfaceIndex1, interfaceIndex2] = Convert.ToDouble(hmmCompRow[distColName].ToString());
                    distMatrix[interfaceIndex2, interfaceIndex1] = Convert.ToDouble(hmmCompRow[distColName].ToString());
                }
            }

            return distMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="hmmCompTable"></param>
        /// <returns></returns>
        private double[,] CreateRmsdDistanceMatrix(string[] domainInterfaces, DataTable hmmCompTable)
        {
            double[,] rmsdMatrix = new double[domainInterfaces.Length, domainInterfaces.Length];
            string domainInterface1 = "";
            string domainInterface2 = "";
            int interfaceIndex1 = 0;
            int interfaceIndex2 = 0;
            double rmsd = 0;
            double rmsd1 = 0;
            double rmsd2 = 0;
            foreach (DataRow hmmCompRow in hmmCompTable.Rows)
            {
                domainInterface1 = hmmCompRow["PdbID1"].ToString() + hmmCompRow["DomainInterfaceID1"].ToString();
                domainInterface2 = hmmCompRow["PdbID2"].ToString() + hmmCompRow["DomainInterfaceID2"].ToString();

                interfaceIndex1 = Array.IndexOf(domainInterfaces, domainInterface1);
                interfaceIndex2 = Array.IndexOf(domainInterfaces, domainInterface2);

                if (interfaceIndex1 > -1 && interfaceIndex2 > -1)
                {
                    rmsd1 = Convert.ToDouble(hmmCompRow["PepRmsd"].ToString ());
                    rmsd2 = Convert.ToDouble(hmmCompRow["InteractPepRmsd"].ToString ());
                    rmsd = Math.Min(rmsd1, rmsd2);
                    rmsdMatrix[interfaceIndex1, interfaceIndex2] = rmsd;
                    rmsdMatrix[interfaceIndex2, interfaceIndex1] = rmsd;
                }
            }
            return rmsdMatrix;
        }
        #endregion

        #region peptide interfaces info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private int GetDomainInterfaceRelSeqID(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable)
        {
            DataRow[] interfaceRows = peptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (interfaceRows.Length > 0)
            {
                return Convert.ToInt32(interfaceRows[0]["RelSeqID"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable)
        {
            DataRow[] interfaceRows = peptideInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (interfaceRows.Length > 0)
            {
                return Convert.ToDouble(interfaceRows[0]["SurfaceArea"].ToString ());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamAccFromPfamId(string pfamId)
        {
            string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamAcc = "";
            if (pfamAccTable.Rows.Count > 0)
            {
                pfamAcc = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
            }
            return pfamAcc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable ReadHmmSiteCompTable(string pfamId)
        {
            //    string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PfamID = '{0}' AND PepComp = '1';", pfamId);
            string queryString = string.Format("Select PfamID, PdbID1, DomainInterfaceID1, DomainID1, PdbID2, DomainInterfaceID2, DomainID2, " +
                " NumOfCommonHmmSites, PepRmsd, InteractPepRmsd " +
                " From PfamInterfaceHmmSiteComp Where PfamID = '{0}' AND PepComp = '1' AND PepRmsd > -1;", pfamId);
            DataTable hmmCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmCompTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmCompTable"></param>
        /// <returns></returns>
        private string[] GetPfamDomainInterfaces(DataTable hmmCompTable, DataTable pfamPeptideInterfaceTable)
        {
            List<string> domainInterfaceIdList = new List<string> ();
            string domainInterface = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            long domainId = 0;
            foreach (DataRow hmmCompRow in hmmCompTable.Rows)
            {
                pdbId = hmmCompRow["PdbID1"].ToString();
                domainInterfaceId = Convert.ToInt32(hmmCompRow["DomainInterfaceID1"].ToString());
                domainInterface = hmmCompRow["PdbID1"].ToString() + hmmCompRow["DomainInterfaceID1"].ToString();
                domainId = Convert.ToInt64(hmmCompRow["DomainID1"].ToString());

                if (!domainInterfaceIdList.Contains(domainInterface))
                {
                    //  if (!IsDomainInterfaceCrystalPacking(pdbId, domainInterfaceId))
                    if (IsDomainInterfaceValid(pdbId, domainInterfaceId, domainId, pfamPeptideInterfaceTable))
                    {
                        domainInterfaceIdList.Add(domainInterface);
                    }
                }

                pdbId = hmmCompRow["PdbID2"].ToString();
                domainInterfaceId = Convert.ToInt32(hmmCompRow["DomainInterfaceID2"].ToString());
                domainInterface = hmmCompRow["PdbID2"].ToString() + hmmCompRow["DomainInterfaceID2"].ToString();
                domainId = Convert.ToInt64(hmmCompRow["DomainID2"].ToString());

                if (!domainInterfaceIdList.Contains(domainInterface))
                {
                    //      if (!IsDomainInterfaceCrystalPacking(pdbId, domainInterfaceId))
                    if (IsDomainInterfaceValid(pdbId, domainInterfaceId, domainId, pfamPeptideInterfaceTable))
                    {
                        domainInterfaceIdList.Add(domainInterface);
                    }
                }
            }
            return domainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="dbTableName"></param>
        private void DeletePfamClusterInfo(string pfamId, string dbTableName)
        {
            string deleteString = string.Format("Delete From {0} Where PfamID = '{1}';", dbTableName, pfamId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// /
        /// </summary>
        /// <returns></returns>
        public string[] GetPepPfamIds()
        {
            string pepPfamIdFile = "PepPfamIds.txt";
            List<string> pfamIdList = new List<string> ();
            if (File.Exists(pepPfamIdFile))
            {
                string line = "";
                StreamReader dataReader = new StreamReader(pepPfamIdFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    pfamIdList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter(pepPfamIdFile);
                string queryString = "Select Distinct PfamID From PfamPeptideInterfaces Where CrystalPack = '0';";
                DataTable pfamTable = ProtCidSettings.protcidQuery.Query( queryString);
                string pfamId = "";
                foreach (DataRow pfamRow in pfamTable.Rows)
                {
                    pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                    pfamIdList.Add(pfamId);
                    dataWriter.WriteLine(pfamId);
                }
                dataWriter.Close();
            }
            return pfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string[] GetClusterPeptideInterfaces(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID " +
                " From PfamPepInterfaceClusters Where PfamID = '{0}' AND  ClusterID = {1};", pfamId, clusterId);
            DataTable clusterPeptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterPeptideInterfaces = new string[clusterPeptideInterfaceTable.Rows.Count];
            string peptideInterface = "";
            int count = 0;
            foreach (DataRow peptideInterfaceRow in clusterPeptideInterfaceTable.Rows)
            {
                peptideInterface = peptideInterfaceRow["PdbID"].ToString() +
                    peptideInterfaceRow["DomainInterfaceID"].ToString();
                clusterPeptideInterfaces[count] = peptideInterface;
                count++;
            }
            return clusterPeptideInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainId"></param>
        /// <param name="pfamPeptideInterfaceTable"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceValid(string pdbId, int domainInterfaceId, long domainId, DataTable pfamPeptideInterfaceTable)
        {
            DataRow[] domainInterfaceRows = pfamPeptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'",
                pdbId, domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                string crystalPack = domainInterfaceRows[0]["CrystalPack"].ToString();
                if (crystalPack == "0")
                {
                    long chainDomainId = Convert.ToInt64(domainInterfaceRows[0]["DomainID"].ToString());
                    if (chainDomainId == domainId)
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
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceCrystalPacking(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select CrystalPack From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable crystalPackTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (crystalPackTable.Rows.Count > 0)
            {
                string crystalPack = crystalPackTable.Rows[0]["CrystalPack"].ToString();
                if (crystalPack == "1")
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region initialize cluster Tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private DataTable CreateDataTable(bool isUpdate)
        {
            string[] clusterColumns = {"PfamID", "ClusterID", "RelSeqID", "PdbID", "DomainInterfaceID", "UnpCode", "SurfaceArea", 
                          "PfamArch", "PepPfamID", "PepUnpCode", "SeqLength", "PepSeqLength", "EntryPfamArch"};
            DataTable peptideInterfaceClusterTable = new DataTable(tableName);
            foreach (string clusterCol in clusterColumns)
            {
                peptideInterfaceClusterTable.Columns.Add(new DataColumn(clusterCol));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + tableName + " ( " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " ClusterID INTEGER NOT NULL, " +
                    " RelSeqID INTEGER NOT NULL, " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " DomainInterfaceID INTEGER NOT NULL, " +
                    " UnpCode VARCHAR(20) NOT NULL, " +
                    " SurfaceArea FLOAT, " + 
                    " PfamArch VARCHAR(1200) NOT NULL, " +
                    " PepPfamID VARCHAR(40), " +
                    " PepUnpCode VARCHAR(20), " +
                    " SeqLength INTEGER, " +
                    " PepSeqLength INTEGER, " +
                    " EntryPfamArch VARCHAR(1200));";
                dbCreate.CreateTable(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            }
            return peptideInterfaceClusterTable;
        }
        #endregion

        #region pfam and unp info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryPfamArchTable"></param>
        /// <returns></returns>
        private string GetEntryPfamArch(string pdbId, DataTable entryPfamArchTable)
        {
            DataRow[] entryPfamArchRows = entryPfamArchTable.Select(string.Format("PdbID = '{0}'", pdbId));
            string entryPfamArch = "";
            if (entryPfamArchRows.Length > 0)
            {
                entryPfamArch = entryPfamArchRows[0]["EntryPfamArchWeb"].ToString().TrimEnd();
            }
            return entryPfamArch;
        }
        /// <summary>
        /// for peptide interfaces, there is a direction. Protein has PfamID, peptide has pepPfamID
        /// bug fixed on Feb. 27, 2018
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPeptideInterfaceTable(string pfamId)
        {
            int[] pepRelSeqIds = GetPeptideRelSeqIdsForPfamId(pfamId);
            // should not have too many relSeqIds
            // add pfamID = input pfam ID condition on Feb. 27, 2018
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID IN ({0}) AND PfamID = '{1}' AND CrystalPack = '0' ;",
                ParseHelper.FormatSqlListString(pepRelSeqIds), pfamId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return peptideInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetChainPeptideInterfaceTable(string[] entries)
        {
            DataTable chainPeptideInterfaceTable = null;
            string queryString = "";
            for (int i = 0; i < entries.Length; i += 300 )
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 300);
                queryString = string.Format("Select * From ChainPeptideInterfaces Where PdbID In ({0});", 
                    ParseHelper.FormatSqlListString (subEntries));
                DataTable subChainPeptideInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
                ParseHelper.AddNewTableToExistTable(subChainPeptideInterfaceTable, ref chainPeptideInterfaceTable);
            }
            return chainPeptideInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainPeptideInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select * From ChainPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable chainPeptideInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            return chainPeptideInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPeptideRelSeqIdsForPfamId(string pfamId)
        {
            string queryString = string.Format("Select RelSeqId From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR (FamilyCode2 = '{0}');", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            List<int> pepRelSeqIdList = new List<int> ();
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                if (IsRelationPeptide(relSeqId))
                {
                    pepRelSeqIdList.Add(relSeqId);
                }
            }
            return pepRelSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetPeptideEntries(DataTable peptideInterfaceTable)
        {
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow pepInterfaceRow in peptideInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetPfamAssignTable(string[] entries)
        {
            DataTable pfamAssignTable = null;
            foreach (string pdbId in entries)
            {
                DataTable entryPfamAssignTable = GetEntryPfamAssignTable(pdbId);
                ParseHelper.AddNewTableToExistTable(entryPfamAssignTable, ref pfamAssignTable);
            }
            return pfamAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamAssignTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable entryPfamAssignTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return entryPfamAssignTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetUnpRefTable(string[] entries)
        {
            DataTable unpRefTable = null;
            foreach (string pdbId in entries)
            {
                DataTable entryUnpRefTable = GetEntryRefUnpTable(pdbId);
                ParseHelper.AddNewTableToExistTable(entryUnpRefTable, ref unpRefTable);
            }
            return unpRefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryRefUnpTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable unpRefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (unpRefTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From PdbDbRefXml Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
                unpRefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            }
            return unpRefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetChainPfamArchTable(string[] entries)
        {
            DataTable chainPfamArchTable = null;
            foreach (string pdbId in entries)
            {
                DataTable entryChainPfamArchTable = GetChainPfamArchTable(pdbId);
                ParseHelper.AddNewTableToExistTable(entryChainPfamArchTable, ref chainPfamArchTable);
            }
            return chainPfamArchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainPfamArchTable(string pdbId)
        {
            string queryString = string.Format("Select * From PfamEntityPfamArch Where PdbID = '{0}';", pdbId);
            DataTable chainPfamArchTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return chainPfamArchTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamArchTable(string[] entries)
        {
            DataTable pfamArchTable = null;
            foreach (string pdbId in entries)
            {
                DataTable entryPfamArchTable = GetEntryPfamArchTable(pdbId);
                ParseHelper.AddNewTableToExistTable(entryPfamArchTable, ref pfamArchTable);
            }
            return pfamArchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamArchTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, EntryPfamArchWeb From PfamEntryPfamArch Where PdbID = '{0}';", pdbId);
            DataTable pfamArchWebTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamArchWebTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private bool IsRelationPeptide(int relSeqId)
        {
            string queryString = string.Format("Select First 1 * From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (peptideInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string[] GetPeptideDomainInterfacePfamUnpInfo(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable, DataTable chainPeptideInterfaceTable,
            DataTable pfamAssignTable, DataTable asuTable, DataTable unpRefTable, DataTable chainPfamArchTable, out int[] chainLengths)
        {
            DataRow[] peptideInterfaceRows = peptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            long protDomainId = Convert.ToInt64(peptideInterfaceRows[0]["DomainID"].ToString());
            string protAsymChain = peptideInterfaceRows[0]["AsymChain"].ToString().TrimEnd();
            int chainInterfaceId = Convert.ToInt32(peptideInterfaceRows[0]["InterfaceID"].ToString());
            DataRow[] chainPeptideInterfaceRows = chainPeptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceId = '{1}'", pdbId, chainInterfaceId));
            chainLengths = new int[2];
            chainLengths[0] = Convert.ToInt32(chainPeptideInterfaceRows[0]["ChainLength"].ToString());
            chainLengths[1] = Convert.ToInt32(chainPeptideInterfaceRows[0]["PepLength"].ToString());

            string[] pfamUnpInfos = new string[6];

            string[] protPfamUnp = GetChainPfamUnpInfo(pdbId, protAsymChain, protDomainId, pfamAssignTable, asuTable, unpRefTable, chainPfamArchTable);
            pfamUnpInfos[0] = protPfamUnp[0];
            pfamUnpInfos[1] = protPfamUnp[1];
            pfamUnpInfos[2] = protPfamUnp[2];

            long peptideDomainId = Convert.ToInt64(peptideInterfaceRows[0]["PepDomainID"].ToString());
            string peptideAsymChain = peptideInterfaceRows[0]["PepAsymChain"].ToString().TrimEnd();

            string[] peptidePfamUnp = GetChainPfamUnpInfo(pdbId, peptideAsymChain, peptideDomainId, pfamAssignTable, asuTable, unpRefTable, chainPfamArchTable);
            pfamUnpInfos[3] = peptidePfamUnp[0];
            pfamUnpInfos[4] = peptidePfamUnp[1];
            pfamUnpInfos[5] = peptidePfamUnp[2];
            return pfamUnpInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="domainId"></param>
        /// <param name="pfamAssignTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="unpRefTable"></param>
        /// <returns></returns>
        private string[] GetChainPfamUnpInfo(string pdbId, string asymChain, long domainId,
            DataTable pfamAssignTable, DataTable asuTable, DataTable unpRefTable, DataTable chainPfamArchTable)
        {
            string[] pfamUnpCode = new string[3];
            pfamUnpCode[0] = "-";
            if (domainId > -1)
            {
                DataRow[] pfamRows = pfamAssignTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainId));
                if (pfamRows.Length > 0)
                {
                    pfamUnpCode[0] = pfamRows[0]["Pfam_ID"].ToString().TrimEnd();
                }
            }

            DataRow[] chainInfoRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            if (chainInfoRows.Length == 0)
            {
                string originalAsymChain = FormatOriginalAsymChain(asymChain);
                chainInfoRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, originalAsymChain));
            }
            int entityId = -1;
            if (chainInfoRows.Length > 0)
            {
                entityId = Convert.ToInt32(chainInfoRows[0]["EntityID"].ToString());
            }
            DataRow[] dbRefRows = unpRefTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
            if (dbRefRows.Length > 0)
            {
                pfamUnpCode[1] = dbRefRows[0]["DbCode"].ToString().TrimEnd();
            }
            else
            {
                pfamUnpCode[1] = "-";
            }

            DataRow[] chainPfamArchRows = chainPfamArchTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
            if (chainPfamArchRows.Length > 0)
            {
                pfamUnpCode[2] = chainPfamArchRows[0]["PfamArch"].ToString().TrimEnd();
            }
            else
            {
                pfamUnpCode[2] = "-";
            }
            return pfamUnpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string FormatOriginalAsymChain(string asymChain)
        {
            string originalAsymChain = "";
            foreach (char ch in asymChain)
            {
                if (char.IsDigit(ch))
                {
                    break;
                }
                originalAsymChain += ch.ToString();
            }
            return originalAsymChain;
        }
        #endregion
 
        #region print out data: #common hmm sites for cluster
        /// <summary>
        /// 
        /// </summary>
        public void PrintPepInterfaceClusterCommonHmmSites()
        {
            string queryString = "Select Distinct PfamID, ClusterID From PfamPepInterfaceClusters;";
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            int clusterId = 0;
            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
            StreamWriter interfaceHmmSitesWriter = new StreamWriter(Path.Combine(clusterFileDir, "PepClusterCommonHmmSites.txt"));
            //       StreamWriter seqHmmAlignWriter = new StreamWriter  (Path.Combine (clusterFileDir, "PepSeqHmmAlign.txt");
            interfaceHmmSitesWriter.WriteLine("PfamID\tModelLength\tClusterID\tPeptideInterface\tHMMSites\tSeqAlign\tHmmAlign");
            foreach (DataRow pfamClusterRow in pepClusterTable.Rows)
            {
                pfamId = pfamClusterRow["PfamID"].ToString().TrimEnd();
                clusterId = Convert.ToInt32(pfamClusterRow["ClusterID"].ToString());
                GetDomainInterfaceHmmSites(pfamId, clusterId, interfaceHmmSitesWriter);
            }
            interfaceHmmSitesWriter.Close();
            //      seqHmmAlignWriter.Close ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="hmmSitesWriter"></param>
        private void GetDomainInterfaceHmmSites(string pfamId, int clusterId, StreamWriter hmmSitesWriter)
        {
            int modelLength = GetPfamModelLength(pfamId);
            //       char[] emptyPfamModel = InitializePfamHmmSites(modelLength);
            string[] clusterPepInterfaces = GetClusterPeptideInterfaces(pfamId, clusterId);
            string clusterHmmSitesLine = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            string interfaceHmmModel = "";
            string[] seqHmmAlignments = null;
            foreach (string pepInterface in clusterPepInterfaces)
            {
                pdbId = pepInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(pepInterface.Substring(4, pepInterface.Length - 4));
                interfaceHmmModel = GetInteractingHmmSites(pdbId, domainInterfaceId, modelLength);
                seqHmmAlignments = GetHmmAlignedSequences(pdbId, domainInterfaceId, modelLength);
                clusterHmmSitesLine = pfamId + "\t" + modelLength.ToString() + "\t" + clusterId.ToString() +
                    "\t" + pepInterface + "\t" + interfaceHmmModel + "\t" + seqHmmAlignments[0] + "\t" + seqHmmAlignments[1];
                hmmSitesWriter.WriteLine(clusterHmmSitesLine);
            }
            hmmSitesWriter.WriteLine();
            hmmSitesWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="emptyPfamModel"></param>
        /// <returns></returns>
        private string GetInteractingHmmSites(string pdbId, int domainInterfaceId, int modelLength)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            int hmmSeqId = 0;
            char[] interfaceHmmSites = InitializePfamHmmSites(modelLength);
            foreach (DataRow hmmSiteRow in hmmSiteTable.Rows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString());
                if (hmmSeqId < 1 || hmmSeqId > modelLength)
                {
                    continue;
                }
                interfaceHmmSites[hmmSeqId - 1] = '*';
            }
            string interfaceHmmModel = new string(interfaceHmmSites);
            return interfaceHmmModel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="modelLength"></param>
        /// <returns></returns>
        private string[] GetHmmAlignedSequences(string pdbId, int domainInterfaceId, int modelLength)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceId = {1};", pdbId, domainInterfaceId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long domainId = Convert.ToInt64(peptideInterfaceTable.Rows[0]["DomainID"].ToString());
            queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}' AND DomainID = {1} Order By HmmStart;", pdbId, domainId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string domainSequence = "";
            string hmmSequence = "";
            int hmmStart = 0;
            int hmmEnd = 0;
            int fillBefore = 0;
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                hmmStart = Convert.ToInt32(domainRow["HmmStart"].ToString());
                hmmEnd = Convert.ToInt32(domainRow["HmmEnd"].ToString());
                fillBefore = hmmStart - domainSequence.Length - 1;
                for (int i = 1; i <= fillBefore; i++)
                {
                    domainSequence += "-";  // put gaps
                    hmmSequence += "1";
                }
                string[] updateSeqHmmAlignments = RemoveGapsFromHmmAlignment(domainRow["HmmAlignment"].ToString(),
                    domainRow["QueryAlignment"].ToString());
                hmmSequence += updateSeqHmmAlignments[0];
                domainSequence += updateSeqHmmAlignments[1];
            }
            if (hmmEnd < modelLength)
            {
                for (int i = hmmEnd; i < modelLength; i++)
                {
                    domainSequence += "-";
                    hmmSequence += "1";
                }
            }
            string[] seqHmmAlignments = new string[2];
            seqHmmAlignments[0] = domainSequence;
            seqHmmAlignments[1] = hmmSequence;
            return seqHmmAlignments;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqAlignment"></param>
        /// <param name="hmmAlignment"></param>
        /// <returns></returns>
        private string[] RemoveGapsFromHmmAlignment(string hmmAlignment, string seqAlignment)
        {
            List<char> updateHmmAlignList = new List<char> ();
            List<char> updateSeqAlignList = new List<char> ();
            for (int i = 0; i < hmmAlignment.Length; i++)
            {
                if (hmmAlignment[i] == '.' || hmmAlignment[i] == '-')
                {
                    continue;
                }
                updateHmmAlignList.Add(hmmAlignment[i]);
                updateSeqAlignList.Add(seqAlignment[i]);
            }
            char[] updateHmmAlign = updateHmmAlignList.ToArray ();
            string updateHmmAlignment = new string(updateHmmAlign);
            char[] updateSeqAlign = updateSeqAlignList.ToArray (); 
            string updateSeqAlignment = new string(updateSeqAlign);
            string[] updateAlignments = new string[2];
            updateAlignments[0] = updateHmmAlignment;
            updateAlignments[1] = updateSeqAlignment;
            return updateAlignments;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelLength"></param>
        /// <returns></returns>
        private char[] InitializePfamHmmSites(int modelLength)
        {
            char[] pfamModel = new char[modelLength];
            for (int i = 0; i < modelLength; i++)
            {
                pfamModel[i] = ' ';
            }
            return pfamModel;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetPfamModelLength(string pfamId)
        {
            string queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable modelLengthTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int modelLength = 0;
            if (modelLengthTable.Rows.Count > 0)
            {
                modelLength = Convert.ToInt32(modelLengthTable.Rows[0]["ModelLength"].ToString());
            }
            return modelLength;
        }
        /// <summary>
        /// 
        /// </summary>
        public void AddSeqPfamLengthsToTable()
        {
            string dataDir = @"D:\DbProjectData\pfam\PfamPeptide\QAndHmmSites";
            StreamReader dataReader = new StreamReader(Path.Combine(dataDir, "PepQNumOfHmmSites.txt"));

            dataReader.Close();
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetDomainInterfaceHmmSites(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteTable.Rows.Count > 0)
            {
                string interactHmmSites = hmmSiteTable.Rows[0]["InteractHmmSites"].ToString().TrimEnd();
                return interactHmmSites;
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterTable"></param>
        /// <returns></returns>
        private string[] GetClusterDomainInterfaces(DataTable clusterTable)
        {
            string entryDomainInterface = "";
            List<string> domainInterfaceList = new List<string> ();
            foreach (DataRow interfaceRow in clusterTable.Rows)
            {
                entryDomainInterface = interfaceRow["PdbID"].ToString() + interfaceRow["DomainInterfaceID"].ToString();
                if (!domainInterfaceList.Contains(entryDomainInterface))
                {
                    domainInterfaceList.Add(entryDomainInterface);
                }
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetRelationClusterTable(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainInterfaceCluster " +
                " Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterDomainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterDomainInterfaceIdTable;
        }

        /// <summary>
        /// 
        /// </summary>
        public void InsertDomainInterfaceHmmSites()
        {
            string dataDir = @"D:\DbProjectData\pfam\PfamPeptide\HmmSiteCompData";

            DataTable hmmSiteTable = new DataTable("PfamDomainInterfaceHmmSites");
            string[] tableColumns = { "PdbID", "DomainInterfaceID", "DomainID", "InteractSeqIds", "InteractHmmSites", "ChainNo" };

            foreach (string tableCol in tableColumns)
            {
                DataColumn dCol = new DataColumn(tableCol);
                hmmSiteTable.Columns.Add(dCol);
            }

            string[] domainInterfaceHmmSitesFiles = Directory.GetFiles(dataDir, "DomainInterfaceHmmSites*.txt");
            SortFilesInTime(domainInterfaceHmmSitesFiles);
            foreach (string hmmSiteFile in domainInterfaceHmmSitesFiles)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(hmmSiteFile);
                ReadFileContentToTable(hmmSiteFile, hmmSiteTable);
            }

            /*         StreamWriter dataWriter = new StreamWriter (domainInterfaceHmmSitesFile);
                      foreach (DataRow hmmSiteRow in hmmSiteTable.Rows)
                      {
                          dataWriter.WriteLine (ParseHelper.FormatDataRow (hmmSiteRow));
                      }*/
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteFile"></param>
        /// <param name="hmmSiteTable"></param>
        private void ReadFileContentToTable(string hmmSiteFile, DataTable hmmSiteTable/*, ref ArrayList insertedDomainInterfaceList*/)
        {
            StreamReader dataReader = new StreamReader(hmmSiteFile);
            string line = "";
            DataRow dataRow = hmmSiteTable.NewRow();
            string entryDomainInterface = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 6)
                {
                    continue;
                }
                entryDomainInterface = fields[0] + fields[1] + "_" + fields[5];
                /*        if (insertedDomainInterfaceList.Contains(entryDomainInterface))
                        {
                            continue;
                        }
                        insertedDomainInterfaceList.Add(entryDomainInterface);*/
                try
                {
                    if (IsDomainInterfaceHmmSiteExist(fields[0], Convert.ToInt32(fields[1]), Convert.ToInt32(fields[5])))
                    {
                        continue;
                    }
                    dataRow.ItemArray = fields;
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, dataRow);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(hmmSiteFile + "  " + line + "    " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(hmmSiteFile + "  " + line + "    " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            dataReader.Close();
        }

        private bool IsDomainInterfaceHmmSiteExist(string pdbId, int domainInterfaceId, int chainNo)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceHmmSites " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1} AND ChainNO = {2};", pdbId, domainInterfaceId, chainNo);
            DataTable interfaceHmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceHmmSiteTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        private void RemoveDomainInterfaceHmmSiteRows(string pdbId, int domainInterfaceId, int chainNo, DataTable hmmSiteTable)
        {
            DataRow[] hmmSiteRows = hmmSiteTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}' AND ChainNo = '{2}'",
                pdbId, domainInterfaceId, chainNo));
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSiteTable.Rows.Remove(hmmSiteRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="chainNo"></param>
        private void RemoveDomainInterfaceHmmSiteRows(string pdbId, int domainInterfaceId, int chainNo)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaceHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1} AND ChainNo = {2};",
                pdbId, domainInterfaceId, chainNo);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="files"></param>
        private void SortFilesInTime(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                for (int j = i + 1; j < files.Length; j++)
                {
                    FileInfo fileInfoI = new FileInfo(files[i]);
                    FileInfo fileInfoJ = new FileInfo(files[j]);
                    if (DateTime.Compare(fileInfoI.LastWriteTime, fileInfoJ.LastWriteTime) > 0)
                    {
                        string temp = files[i];
                        files[i] = files[j];
                        files[j] = temp;
                    }
                }
            }
        }
        #endregion

        #region debug
        public void UpdatePfamPepClustersUnpCodes()
        {
            string queryString = "Select PdbID, DomainInterfaceID From PfamPepInterfaceClusters Where PepUnpCode = '-';";
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            List<string> entryList = new List<string> ();
            foreach (DataRow domainInterfaceRow in pepInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            int pepInterfaceId = 0;
            string unpCode = "";
            foreach (string lsPdbId in entryList)
            {
                DataRow[] pepInterfaceRows = pepInterfaceTable.Select(string.Format("PdbID = '{0}'", lsPdbId));
                DataTable unpCodeTable = GetEntryUnpCodeTable(lsPdbId);
                DataTable pepInterfaceDefTable = GetEntryPepInterfaceTable(lsPdbId);
                DataTable asuTable = GetEntryAsuTable(lsPdbId);
               
                foreach (DataRow interfaceRow in pepInterfaceRows)
                {
                    pepInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString ());
                    unpCode = GetChainPfamUnpInfo(lsPdbId, pepInterfaceId, pepInterfaceDefTable, asuTable, unpCodeTable, "pep");
                    if (unpCode != "-")
                    {
                        UpdateUnpCodeInfo(lsPdbId, pepInterfaceId, unpCode, "pep");
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryAsuTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, AsymID, EntityID From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return asuTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="unpCode"></param>
        /// <param name="chainOrPep"></param>
        private void UpdateUnpCodeInfo(string pdbId, int domainInterfaceId, string unpCode, string chainOrPep)
        {
            string updateString = "";
            if (chainOrPep == "chain")
            {
                updateString = string.Format("Update PfamPepInterfaceClusters Set UnpCode = '{0}' " +
                    " Where PdbID = '{1}' AND DomainInterfaceID = {2};", unpCode, pdbId, domainInterfaceId);
            }
            else
            {
                updateString = string.Format("Update PfamPepInterfaceClusters Set PepUnpCode = '{0}' " +
                    " Where PdbID = '{1}' AND DomainInterfaceID = {2};", unpCode, pdbId, domainInterfaceId);
            }
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="domainId"></param>
        /// <param name="asuTable"></param>
        /// <param name="unpRefTable"></param>
        /// <returns></returns>
        private string GetChainPfamUnpInfo(string pdbId, int pepInterfaceId, DataTable pepInterfaceDefTable, DataTable asuTable, DataTable unpRefTable, string chainOrPep)
        {
            int entityId = -1;
            string unpCode = "-";
            DataRow[] interfaceDefRows = pepInterfaceDefTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, pepInterfaceId));
            if (interfaceDefRows.Length > 0)
            {
                string asymChain = "";
                if (chainOrPep == "chain")
                {
                    asymChain = interfaceDefRows[0]["AsymChain"].ToString().TrimEnd();
                }
                else if (chainOrPep == "pep")
                {
                    asymChain = interfaceDefRows[0]["PepAsymChain"].ToString().TrimEnd();
                }

                DataRow[] chainInfoRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
                if (chainInfoRows.Length == 0)
                {
                    string originalAsymChain = FormatOriginalAsymChain(asymChain);
                    chainInfoRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, originalAsymChain));
                }

                if (chainInfoRows.Length > 0)
                {
                    entityId = Convert.ToInt32(chainInfoRows[0]["EntityID"].ToString());
                }
                DataRow[] dbRefRows = unpRefTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                if (dbRefRows.Length > 0)
                {
                    unpCode = dbRefRows[0]["DbCode"].ToString().TrimEnd();
                }
            }

            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPepInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces WHere PdbID = '{0}';", pdbId);
            DataTable entryPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryPepInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryUnpCodeTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return unpCodeTable;
        }

        public void FindMissingMatchDomainEntry ()
        {
            StreamWriter dataWriter = new StreamWriter("DomainErrorEntries.txt");
            List<string> entryList =  new List<string> ();
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (! ArePepInterfaceDomainsRight(pdbId))
                {
                    entryList.Add(pdbId);
                    dataWriter.WriteLine(pdbId);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool ArePepInterfaceDomainsRight(string pdbId)
        {
            string queryString = string.Format("Select DomainID, PepDomainID From PfamPeptideInterfaces WHere PdbID = '{0}';", pdbId);
            DataTable pepInterfaceDomainTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select Distinct DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string interfaceDomainId = "";
            foreach (DataRow interfaceDomainRow in pepInterfaceDomainTable.Rows)
            {
                interfaceDomainId = interfaceDomainRow["DomainID"].ToString();
                DataRow[] domainRows = domainTable.Select(string.Format ("DomainID = '{0}'", interfaceDomainId));
                if (domainRows.Length == 0)
                {
                    return false;
                }

                interfaceDomainId = interfaceDomainRow["PepDomainID"].ToString();
                if (interfaceDomainId != "-1")
                {
                    domainRows = domainTable.Select(string.Format("DomainID = '{0}'", interfaceDomainId));
                    if (domainRows.Length == 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion

    }
}
