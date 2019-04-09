using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PepClusterInterfaceCompress
    {
        #region member variables
        private string domainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
        private CmdOperations tarOperator = new CmdOperations();
        private PfamClusterFilesCompress clusterCompress = new PfamClusterFilesCompress();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();        
        private double rmsdCutoff = 5.0;
        private int alignLengthCutoff = 3;
        private double percentInterfacesCluster = 0.75;
        private double numOfHmmSitesSamePep = 0.95;
        private double percentSimPepInterfaces = 0.50;
        private int numOfSimPepInterfaces = 10;
        private double chainPepAtomDistCutoff = 5.0;
        private PeptideInterfaceCluster pepInterfaceCluster = new PeptideInterfaceCluster();
        #endregion

        #region compress cluster peptide interface files
        /// <summary>
        /// compress peptide interfaces for each cluster
        /// </summary>
        public void CompressClusterPeptideInterfaceFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress peptide interface clusters");

            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PepClusters");
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

            string[] pfamIds = pepInterfaceCluster.GetPepPfamIds();

            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (File.Exists(Path.Combine(clusterFileDir, pfamId + ".tar")))
                {
                    continue;
                }
                CompressClusterPeptideInterfaceFiles(pfamId, clusterFileDir);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("DOne!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        public void UpdateClusterPeptideInterfaceFiles(string[] pfamIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress peptide interface clusters");

            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PepClusters");
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;

            string pfamTarFile = "";
            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                pfamTarFile = Path.Combine(clusterFileDir, pfamId + ".tar");
                if (File.Exists(pfamTarFile))
                {
                    File.Delete(pfamTarFile);
  //                  continue;
                }
                CompressClusterPeptideInterfaceFiles(pfamId, clusterFileDir);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("DOne!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterFileDir"></param>
        public void CompressClusterPeptideInterfaceFiles(string pfamId, string clusterFileDir)
        {
            string queryString = string.Format("Select Distinct ClusterID From {0} Where PfamID = '{1}';", pepInterfaceCluster.tableName, pfamId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;
            string clusterFileName = "";
            List<string> clusterFileList = new List<string> ();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                try
                {
                    clusterFileName = CompressClusterPeptideInterfaceFiles(pfamId, clusterId, clusterFileDir);
                    clusterFileList.Add(clusterFileName);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            tarOperator.RunTar(pfamId + ".tar", clusterFileList.ToArray (), clusterFileDir, false);

            foreach (string clusterFile in clusterFileList)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterFile));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterFileDir"></param>
        private string CompressClusterPeptideInterfaceFiles(string pfamId, int clusterId, string clusterFileDir)
        {
            string[] allClusterDomainInterfaces = pepInterfaceCluster.GetClusterPeptideInterfaces(pfamId, clusterId);
            string[] clusterDomainInterfaces = RemoveSamePeptideInterfaces(pfamId, allClusterDomainInterfaces);
            string[] clusterDomainInterfaceFiles = FormatClusterDomainInterfacesToFileFormat(clusterDomainInterfaces);

            //      string clusterFileName = pfamId + "_" + clusterId.ToString();

            DataTable peptideInterfaceTable = GetClusterPeptideInterfaceDefTable(clusterDomainInterfaces);

            DataTable hmmSiteCompTable = GetPeptideInterfaceHmmSiteCompTable(pfamId);

            string clusterFileName = clusterCompress.CompressClusterPeptideInterfaceFiles(pfamId, clusterId, clusterDomainInterfaceFiles,
                peptideInterfaceTable, hmmSiteCompTable, clusterFileDir);
            return clusterFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceHmmSiteCompTable(string pfamId)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PfamID = '{0}' AND PepRmsd > -1;", pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPeptideInterfaceHmmSiteCompTable(string pfamId)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PfamID = '{0}' AND PepRmsd > -1 AND PepComp = '1';", pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private DataTable GetClusterPeptideInterfaceDefTable(string[] domainInterfaces)
        {
            DataTable domainInterfaceTable = null;
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));
                DataTable thisDomainInterfaceTable = GetPeptideInterfaceDefTable(pdbId, domainInterfaceId);
                ParseHelper.AddNewTableToExistTable(thisDomainInterfaceTable, ref domainInterfaceTable);
            }
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetPeptideInterfaceDefTable(string pdbId, int domainInterfaceId)
        {
            //    string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            // try to fit into the domain interfaces
            string queryString = string.Format("Select RelSeqId, PdbID, InterfaceID, DomainInterfaceID, " +
               " DomainID As DomainID1, AsymChain As AsymChain1, ChainDomainID As ChainDomainID1, " +
               " PepDomainID As DomainID2, PepAsymChain As AsymChain2, PepChainDomainID As ChainDomainID2, NumOfAtomPairs, NumOfResiduePairs " +
               " From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return peptideInterfaceTable;
        }
        #endregion

        #region remove same peptide interfaces in a cluster
        /// <summary>
        /// remove same peptide interfaces for each entry, so that can reduce the number of files in cluster pymol alignments.
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterPeptideInterfaces"></param>
        /// <returns></returns>
        private string[] RemoveSamePeptideInterfaces(string pfamId, string[] clusterPeptideInterfaces)
        {
            Dictionary<string, List<int>> entryInterfaceListHash = new Dictionary<string,List<int>> ();
            string pdbId = "";
            int peptideInterfaceId = 0;
            foreach (string peptideInterface in clusterPeptideInterfaces)
            {
                pdbId = peptideInterface.Substring(0, 4);
                peptideInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
                if (entryInterfaceListHash.ContainsKey(pdbId))
                {
                    entryInterfaceListHash[pdbId].Add(peptideInterfaceId);
                }
                else
                {
                    List<int> interfaceIdList = new List<int> ();
                    interfaceIdList.Add(peptideInterfaceId);
                    entryInterfaceListHash.Add(pdbId, interfaceIdList);
                }
            }
            Dictionary<string, int[]> entryInterfaceHash = new Dictionary<string, int[]>();
            foreach (string lsEntry in entryInterfaceListHash.Keys)
            {
                entryInterfaceHash.Add (lsEntry, entryInterfaceListHash[lsEntry].ToArray ());
            }

            string[] noreduntPeptideInterfaces = RemoveSameEntryPeptideInterfaces(pfamId, entryInterfaceHash);
            return noreduntPeptideInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="entryPeptideInterfacesHash"></param>
        /// <returns></returns>
        private string[] RemoveSameEntryPeptideInterfaces(string pfamId, Dictionary<string, int[]> entryPeptideInterfacesHash)
        {
            List<string> noReduntPeptideInterfaceList = new List<string> ();

            foreach (string pdbId in entryPeptideInterfacesHash.Keys)
            {
                DataTable hmmSiteTable = GetPfamPeptideInterfaceHmmSiteTable(pfamId, pdbId);
                int[] peptideInterfaceIds = entryPeptideInterfacesHash[pdbId];
                int[] noreduntPepInterfaceIds = GetNonRedundantPeptideInterfaceIds(pdbId, peptideInterfaceIds, hmmSiteTable);
                foreach (int peptideInterfaceId in noreduntPepInterfaceIds)
                {
                    noReduntPeptideInterfaceList.Add(pdbId + peptideInterfaceId.ToString());
                }
            }
            return noReduntPeptideInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideInterfaceIds"></param>
        /// <param name="hmmSiteTable"></param>
        /// <returns></returns>
        private int[] GetNonRedundantPeptideInterfaceIds(string pdbId, int[] peptideInterfaceIds, DataTable hmmSiteTable)
        {
            List<int> noreduntPepInterfaceIdList = new List<int> (peptideInterfaceIds);
            for (int i = 0; i < peptideInterfaceIds.Length; i++)
            {
                if (!noreduntPepInterfaceIdList.Contains(peptideInterfaceIds[i]))
                {
                    continue;
                }
                int[] hmmSitesI = GetPeptideInterfaceHmmSites(pdbId, peptideInterfaceIds[i], hmmSiteTable);
                for (int j = i + 1; j < peptideInterfaceIds.Length; j++)
                {
                    if (!noreduntPepInterfaceIdList.Contains(peptideInterfaceIds[j]))
                    {
                        continue;
                    }

                    int[] hmmSitesJ = GetPeptideInterfaceHmmSites(pdbId, peptideInterfaceIds[j], hmmSiteTable);
                    if (ArePeptideInterfacesSame(hmmSitesI, hmmSitesJ))
                    {
                        noreduntPepInterfaceIdList.Remove(peptideInterfaceIds[j]);
                    }
                }
            }
            return noreduntPepInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSitesI"></param>
        /// <param name="hmmSitesJ"></param>
        /// <returns></returns>
        private bool ArePeptideInterfacesSame(int[] hmmSitesI, int[] hmmSitesJ)
        {
            List<int> commonHmmSiteList = new List<int> ();
            foreach (int hmmSiteI in hmmSitesI)
            {
                if (hmmSitesJ.Contains(hmmSiteI))
                {
                    commonHmmSiteList.Add(hmmSiteI);
                }
            }
            int maxHmmSiteLength = hmmSitesI.Length;
            if (maxHmmSiteLength < hmmSitesJ.Length)
            {
                maxHmmSiteLength = hmmSitesJ.Length;
            }
            double comHmmSitePercent = (double)commonHmmSiteList.Count / (double)maxHmmSiteLength;
            if (comHmmSitePercent >= numOfHmmSitesSamePep)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideInterfaceId"></param>
        /// <param name="hmmSiteTable"></param>
        /// <returns></returns>
        private int[] GetPeptideInterfaceHmmSites(string pdbId, int peptideInterfaceId, DataTable hmmSiteTable)
        {
            DataRow[] hmmSiteRows = hmmSiteTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceId = '{1}'", pdbId, peptideInterfaceId));
            List<int> hmmSiteList = new List<int> ();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString());
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }
            return hmmSiteList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetPfamPeptideInterfaceHmmSiteTable(string pfamId, string pdbId)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PfamID = '{0}' AND PdbID = '{1}';", pfamId, pdbId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteTable;
        }
        #endregion

        #region pymol sessions for peptide and domain interfaces

        #region compress peptide interfaces including domain interfaces
        /// <summary>
        /// compress cluster interface files and the similar domain interfaces
        /// </summary>
        public void CompressClusterPeptidDomainInterfacesFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress peptide interface clusters and domain interfaces");

            string pepClusterDomainsInfoFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PeptideClusterDomainInterfacesInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pepClusterDomainsInfoFile);
            dataWriter.WriteLine("PFAMID\tClusterID\tPepPdbID\tPepDomainInterfaceID\tPdbID\tDomainInterfaceID\t" +
                "#CommonHmmSites\tChainRmsd\tInteractChainRmsd\tPepRmsd\tInteractPepRmsd\tRelSeqID\tClusterID\tOtherDomainInterfaces");

            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PepClusters_domain");
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

            //     string[] pfamIds = GetPepPfamIds();
            string[] pfamIds = { "Pkinase" };

            string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (File.Exists(Path.Combine(clusterFileDir, pfamId + ".tar")))
                {
                    continue;
                }
                CompressClusterPeptideInterfaceFiles(pfamId, clusterFileDir, asuInfoTable, dataWriter);
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterFileDir"></param>
        /// <param name="dataWriter"></param>
        public void CompressClusterPeptideInterfaceFiles(string pfamId, string clusterFileDir, DataTable asuInfoTable, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            // get the hmm site comp data for domain interfaces to peptide interfaces
            DataTable pepDomainHmmSiteCompTable = GetPfamPepDomainInterfaceCompTable(pfamId, "PfamInterfaceHmmSiteComp");
            RemoveDomainInterfacesWithPeptides(pepDomainHmmSiteCompTable, asuInfoTable);

            int clusterId = 0;
            string clusterFileName = "";
            List<string> clusterFileList = new List<string> ();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                try
                {
                    clusterFileName = CompressClusterPeptideInterfaceFiles(pfamId, clusterId, pepDomainHmmSiteCompTable, clusterFileDir, dataWriter);
                    clusterFileList.Add(clusterFileName);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            tarOperator.RunTar(pfamId + ".tar", clusterFileList.ToArray (), clusterFileDir, false);

            foreach (string clusterFile in clusterFileList)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterFile));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterFileDir"></param>
        /// <param name="dataWriter"></param>
        /// <returns></returns>
        private string CompressClusterPeptideInterfaceFiles(string pfamId, int clusterId, DataTable pepDomainHmmSiteCompTable, string clusterFileDir, StreamWriter dataWriter)
        {
            string[] clusterPeptideInterfaces = pepInterfaceCluster.GetClusterPeptideInterfaces(pfamId, clusterId);
            string[] addedDomainInterfaces = GetSimilarDomainInterfaces(pfamId, clusterPeptideInterfaces, pepDomainHmmSiteCompTable);
            WriteDomainInterfaceInfoToFile(pfamId, clusterId, addedDomainInterfaces, pepDomainHmmSiteCompTable, dataWriter);
            string[] clusterDomainInterfaces = new string[clusterPeptideInterfaces.Length + addedDomainInterfaces.Length];
            Array.Copy(clusterPeptideInterfaces, 0, clusterDomainInterfaces, 0, clusterPeptideInterfaces.Length);
            Array.Copy(addedDomainInterfaces, 0, clusterDomainInterfaces, clusterPeptideInterfaces.Length, addedDomainInterfaces.Length);

            string[] clusterDomainInterfaceFiles = FormatClusterDomainInterfacesToFileFormat(clusterDomainInterfaces);

            DataTable peptideInterfaceTable = GetClusterPeptideInterfaceDefTable(clusterPeptideInterfaces);
            AddDomainInterfaceDefTable(addedDomainInterfaces, peptideInterfaceTable);

            //      DataTable hmmSiteCompTable = GetPeptideInterfaceHmmSiteCompTable (pfamId);  // not sure what is it. pepDomainHmmSiteCompTable is alrady exist

            string clusterFileName = clusterCompress.CompressClusterPeptideInterfaceFiles(pfamId, clusterId, clusterDomainInterfaceFiles,
                peptideInterfaceTable, pepDomainHmmSiteCompTable, clusterFileDir);
            return clusterFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="addedDomainInterfaces"></param>
        /// <param name="pepDomainHmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void WriteDomainInterfaceInfoToFile(string pfamId, int clusterId, string[] addedDomainInterfaces, DataTable pepDomainHmmSiteCompTable, StreamWriter dataWriter)
        {
            string dataLine = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            string clusterInterfaces = "";
            Dictionary<string, string> clusterInterfaceClusterInfoHash = new Dictionary<string,string> ();
            foreach (string domainInterface in addedDomainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));
                clusterInterfaces = GetDomainInterfaceClusterInfo(pdbId, domainInterfaceId, clusterInterfaceClusterInfoHash);
                DataRow[] pepDomainCompRows = pepDomainHmmSiteCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
                foreach (DataRow pepDomainCompRow in pepDomainCompRows)
                {
                    dataLine = pfamId + "\t" + clusterId.ToString() + "\t" + pepDomainCompRow["PdbID1"].ToString() + "\t" +
                        pepDomainCompRow["DomainInterfaceID1"].ToString() + "\t" + pepDomainCompRow["PdbID2"].ToString() + "\t" +
                        pepDomainCompRow["DomainInterfaceID2"].ToString() + "\t" + pepDomainCompRow["NumOfCommonHmmSites"].ToString() + "\t" +
                        pepDomainCompRow["ChainRmsd"].ToString() + "\t" + pepDomainCompRow["InteractChainRmsd"].ToString() + "\t" +
                        pepDomainCompRow["PepRmsd"].ToString() + "\t" + pepDomainCompRow["InteractPepRmsd"].ToString() + "\t" +
                        clusterInterfaces;
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Flush();
        }
        #endregion

        #region similar domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterPeptideInterfaces"></param>
        /// <returns></returns>
        private string[] GetSimilarDomainInterfaces(string pfamId, string[] clusterPeptideInterfaces, DataTable pepDomainHmmSiteTable)
        {
            string pepPdbId = "";
            int pepDomainInterfaceId = 0;
            string domainInterface = "";
            List<string> domainInterfacesList = new List<string> ();
            foreach (string peptideInterface in clusterPeptideInterfaces)
            {
                pepPdbId = peptideInterface.Substring(0, 4);
                pepDomainInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
                DataRow[] pepDomainRows = pepDomainHmmSiteTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pepPdbId, pepDomainInterfaceId));
                foreach (DataRow domainRow in pepDomainRows)
                {
                    domainInterface = domainRow["PdbID2"].ToString() + domainRow["DomainInterfaceID2"].ToString();
                    if (!domainInterfacesList.Contains(domainInterface))
                    {
                        domainInterfacesList.Add(domainInterface);
                    }
                }
            }

            string[] domainInterfacesToBeClustered = GetSimPeptideDomainInterfaces(domainInterfacesList.ToArray (), pepDomainHmmSiteTable, clusterPeptideInterfaces);
            return domainInterfacesToBeClustered;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterPeptideInterfaces"></param>
        /// <returns></returns>
        private string[] GetSimilarDomainInterfaces(string pfamId, string[] clusterPeptideInterfaces, DataTable pepDomainHmmSiteTable, int[] clusterHmmSites)
        {
            string pepPdbId = "";
            int pepDomainInterfaceId = 0;
            string domainInterface = "";
            List<string> domainInterfacesList = new List<string> ();
            foreach (string peptideInterface in clusterPeptideInterfaces)
            {
                pepPdbId = peptideInterface.Substring(0, 4);
                pepDomainInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
                int[] conservativePeptideSeqIds = GetPeptideInterfacePeptideCoordSeqIds(peptideInterface, clusterHmmSites);

                DataRow[] pepDomainRows = pepDomainHmmSiteTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pepPdbId, pepDomainInterfaceId));
                foreach (DataRow domainRow in pepDomainRows)
                {
                    domainInterface = domainRow["PdbID2"].ToString() + domainRow["DomainInterfaceID2"].ToString();
                    if (ContainConservativeSites(domainRow, conservativePeptideSeqIds))
                    {
                        if (!domainInterfacesList.Contains(domainInterface))
                        {
                            domainInterfacesList.Add(domainInterface);
                        }
                    }
                }
            }
            domainInterfacesList.Sort();
      //      return domainInterfaces;
            string[] domainInterfacesToBeClustered = GetSimPeptideDomainInterfaces(domainInterfacesList.ToArray (), pepDomainHmmSiteTable, clusterPeptideInterfaces);
            return domainInterfacesToBeClustered;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="pepDomainHmmSiteTable"></param>
        /// <returns></returns>
        private string[] GetSimPeptideDomainInterfaces(string[] domainInterfaces, DataTable pepDomainHmmSiteTable, string[] clusterPepInterfaces)
        {
            string domainPdbId = "";
            int domainInterfaceId = 0;
            List<string> simPepDomainInterfaceList = new List<string> ();
            double simPercentage = 0;
            // chain/domain interfaces which share at least 10 hmm sites with at least half of peptide interfaces in the cluster
            foreach (string lsDomainInterface in domainInterfaces)
            {
                domainPdbId = lsDomainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(lsDomainInterface.Substring(4, lsDomainInterface.Length - 4));

                string[] simPepInterfaces = GetSimPeptideInterfaces(domainPdbId, domainInterfaceId, pepDomainHmmSiteTable, clusterPepInterfaces);
                if (simPepInterfaces.Length >= numOfSimPepInterfaces)  // similar to at least numOfSimPepInterfaces peptide interfaces
                {
                    simPepDomainInterfaceList.Add(lsDomainInterface);
                }
                else
                {
                    simPercentage = (double)simPepInterfaces.Length / (double)clusterPepInterfaces.Length;
                    if (simPercentage >= percentSimPepInterfaces) // at least x of the peptide interfaces
                    {
                        simPepDomainInterfaceList.Add(lsDomainInterface);
                    }
                }
            }
            simPepDomainInterfaceList.Sort();
            return simPepDomainInterfaceList.ToArray ();
        }
       
        /// <summary>
        /// /
        /// </summary>
        /// <param name="chainPdbId"></param>
        /// <param name="chainDomainInterfaceId"></param>
        /// <param name="pepDomainHmmSiteTable"></param>
        /// <returns></returns>
        private string[] GetSimPeptideInterfaces(string chainPdbId, int chainDomainInterfaceId, DataTable pepDomainHmmSiteTable, string[] clusterPepInterfaces)
        {
            List<string> simPeptideInterfaceList = new List<string> ();
            string peptideInterface = "";
            DataRow[] hmmSiteCompRows = pepDomainHmmSiteTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", chainPdbId, chainDomainInterfaceId));
            foreach (DataRow hmmSiteCompRow in hmmSiteCompRows)
            {
                peptideInterface = hmmSiteCompRow["PdbID1"].ToString() + hmmSiteCompRow["DomainInterfaceID1"].ToString();
                if (clusterPepInterfaces.Contains(peptideInterface))
                {
                    if (!simPeptideInterfaceList.Contains(peptideInterface))
                    {
                        simPeptideInterfaceList.Add(peptideInterface);
                    }
                }
            }
            return simPeptideInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepChainCompRow"></param>
        /// <param name="peptideSeqIds"></param>
        /// <returns></returns>
        private bool ContainConservativeSites(DataRow pepChainCompRow, int[] peptideSeqIds)
        {
            int pepStart = Convert.ToInt32(pepChainCompRow["PepStart"].ToString());
            int pepEnd = Convert.ToInt32(pepChainCompRow["PepEnd"].ToString());
            List<int> commonSeqIdList = new List<int> ();
            foreach (int pepSeqId in peptideSeqIds)
            {
                if (pepSeqId <= pepEnd && pepSeqId >= pepStart)
                {
                    commonSeqIdList.Add(pepSeqId);
                }
            }
            // share at least 3 hmm sites
            if (commonSeqIdList.Count >= alignLengthCutoff)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepDomainHmmSiteCompTable"></param>
        private void RemoveDomainInterfacesWithPeptides(DataTable pepDomainHmmSiteCompTable, DataTable asuTable)
        {
            List<DataRow> removeDataRowList = new List<DataRow> ();
            string domainPdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow hmmSiteCompRow in pepDomainHmmSiteCompTable.Rows)
            {
                domainPdbId = hmmSiteCompRow["PdbID2"].ToString();
                domainInterfaceId = Convert.ToInt32(hmmSiteCompRow["DomainInterfaceID2"].ToString());
                if (IsDomainInterfacePeptide(domainPdbId, domainInterfaceId, asuTable))
                {
                    removeDataRowList.Add(hmmSiteCompRow);
                }
            }
            foreach (DataRow hmmSiteRow in removeDataRowList)
            {
                pepDomainHmmSiteCompTable.Rows.Remove(hmmSiteRow);
            }
            pepDomainHmmSiteCompTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private bool IsDomainInterfacePeptide(string pdbId, int domainInterfaceId, DataTable asuTable)
        {
            string queryString = string.Format("Select AsymChain1, AsymChain2 From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable asymChainPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            string asymChain1 = asymChainPairTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
            string asymChain2 = asymChainPairTable.Rows[0]["AsymChain2"].ToString().TrimEnd();
            bool isPepChain1 = IsChainPeptide(pdbId, asymChain1, asuTable);
            if (asymChain2 == asymChain1)
            {
                return isPepChain1;
            }
            else
            {
                bool isPepChain2 = IsChainPeptide(pdbId, asymChain2, asuTable);
                if (isPepChain1 || isPepChain2)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private bool IsChainPeptide(string pdbId, string asymChain, DataTable asuTable)
        {
            DataRow[] asymChainRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            if (asymChainRows.Length > 0)
            {
                string sequence = asymChainRows[0]["Sequence"].ToString();
                if (sequence.Length < ProtCidSettings.peptideLengthCutoff)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region domain interface info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfaces"></param>
        /// <returns></returns>
        public string[] FormatClusterDomainInterfacesToFileFormat(string[] clusterDomainInterfaces)
        {
            string[] clusterDomainInterfaceFiles = new string[clusterDomainInterfaces.Length];
            string pdbId = "";
            string domainInterfaceId = "";
            for (int i = 0; i < clusterDomainInterfaces.Length; i++)
            {
                pdbId = clusterDomainInterfaces[i].Substring(0, 4);
                domainInterfaceId = clusterDomainInterfaces[i].Substring(4, clusterDomainInterfaces[i].Length - 4);
                clusterDomainInterfaceFiles[i] = pdbId + "_d" + domainInterfaceId;
            }
            return clusterDomainInterfaceFiles;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamPepDomainInterfaceCompTable(string pfamId, string hmmSiteCompTableName)
        {
            string queryString = string.Format("Select * From {0} " +
                " Where PfamID = '{1}' AND PepComp = '0' AND (NumOfCommonHmmSites >= {2} OR " +
                " (PepRmsd > -1 AND PepRmsd <= {3} AND NumOfCommonHmmSites >= {4}));",
                hmmSiteCompTableName, pfamId, pepInterfaceCluster.numOfComHmmSitesGood, pepInterfaceCluster.rmsdMax, pepInterfaceCluster.numOfComHmmSiteCutoff);
            DataTable goodPepDomainHmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return goodPepDomainHmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfaces"></param>
        /// <returns></returns>
        private void AddDomainInterfaceDefTable(string[] clusterDomainInterfaces, DataTable clusterDomainInterfaceTable)
        {
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string domainInterface in clusterDomainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));
                DataTable domainInterfaceTable = GetDomainInterfaceDefTable(pdbId, domainInterfaceId);
                ParseHelper.AddNewTableToExistTable(domainInterfaceTable, ref clusterDomainInterfaceTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceDefTable(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, InterfaceID, DomainInterfaceID, " +
                " DomainID1, AsymChain1, ChainDomainID1, DomainID2, AsymChain2, ChainDomainID2 " +
                " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataColumn atomPairCol = new DataColumn("NumOfAtomPairs");
            atomPairCol.DefaultValue = -1;
            domainInterfaceTable.Columns.Add(atomPairCol);
            DataColumn residuePairCol = new DataColumn("NumOfResiduePairs");
            residuePairCol.DefaultValue = -1;
            domainInterfaceTable.Columns.Add(residuePairCol);
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetDomainInterfaceClusterInfo(string pdbId, int domainInterfaceId, Dictionary<string, string> interfaceClusterInfoHash)
        {
            string queryString = string.Format("Select RelSeqId, ClusterID From PfamDomainInterfaceCluster Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            int clusterId = -1;
            string clusterInfo = "";
            string clusterInterfaces = "";
            string relSeqIdClusterId = "";
            string relationString = "";
            if (clusterIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(clusterIdTable.Rows[0]["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterIdTable.Rows[0]["ClusterID"].ToString());
                relSeqIdClusterId = relSeqId + "_" + clusterId.ToString();
                if (interfaceClusterInfoHash.ContainsKey(relSeqIdClusterId))
                {
                    clusterInfo =  interfaceClusterInfoHash[relSeqIdClusterId];
                }
                else
                {
                    relationString = GetRelationString(relSeqId);
                    clusterInterfaces = GetClusterInterfaces(relSeqId, clusterId);
                    clusterInfo = relSeqId.ToString() + "\t" + relationString + "\t" + clusterId.ToString() + "\t" + clusterInterfaces;
                    interfaceClusterInfoHash.Add(relSeqIdClusterId, clusterInfo);
                }
            }
            else
            {
                queryString = string.Format("Select RelSeqID From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
                DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
                relSeqIdClusterId = relSeqId.ToString();
                if (interfaceClusterInfoHash.ContainsKey(relSeqIdClusterId))
                {
                    clusterInfo = interfaceClusterInfoHash[relSeqIdClusterId];
                }
                else
                {
                    relationString = GetRelationString(relSeqId);
                    clusterInfo = relSeqId.ToString() + "\t" + relationString;
                    interfaceClusterInfoHash.Add(relSeqIdClusterId, clusterInfo);
                }
            }
            return clusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetRelationString(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable relationStringTable = ProtCidSettings.protcidQuery.Query( queryString);
            string relationString = "";
            if (relationStringTable.Rows.Count > 0)
            {
                relationString = "(" + relationStringTable.Rows[0]["FamilyCode1"].ToString().TrimEnd() + ");(" +
                    relationStringTable.Rows[0]["FamilyCode2"].ToString().TrimEnd() + ")";
            }
            return relationString;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetClusterInterfaces(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string clusterInterfaces = "";
            string clusterInterface = "";
            foreach (DataRow clusterInterfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterface = clusterInterfaceRow["PdbID"].ToString() + "_d" + clusterInterfaceRow["DomainInterfaceID"].ToString();
                clusterInterfaces += (clusterInterface + ",");
            }
            return clusterInterfaces.TrimEnd(',');
        }
        #endregion

        #endregion

        #region pymol sessions for peptide and chain interfaces

        #region compress peptide interfaces including chain interfaces
        private PfamLib.PfamArch.PfamArchitecture pfamArch = new PfamLib.PfamArch.PfamArchitecture();
        /// <summary>
        /// compress cluster interface files and the similar domain interfaces
        /// </summary>
        public void CompressClusterPeptidChainInterfacesFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress peptide interface clusters and domain interfaces");

            string pepClusterDomainsInfoFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PeptideClusterChainInterfacesInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pepClusterDomainsInfoFile);
            dataWriter.WriteLine("PFAMID\tClusterID\tPepPdbID\tPepDomainInterfaceID\tPdbID\tInterfaceID\t" +
                "#CommonHmmSites\tChainRmsd\tInteractChainRmsd\tPepRmsd\tInteractPepRmsd\t" +
                "PepAlignment\tChainAlignment\tPepRange\tChainRange\tScore\tRmsd\tRelSeqID\tClusterID");

            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PepClusters_chain");
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

             string[] pfamIds = pepInterfaceCluster.GetPepPfamIds();
           //  string[] pfamIds = { "Insulin"};

            string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (File.Exists(Path.Combine(clusterFileDir, pfamId + ".tar")))
                {
                    continue;
                }
                CompressClusterPeptideChainInterfaceFiles(pfamId, clusterFileDir, asuInfoTable, dataWriter);
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterFileDir"></param>
        /// <param name="dataWriter"></param>
        public void CompressClusterPeptideChainInterfaceFiles(string pfamId, string clusterFileDir, DataTable asuInfoTable, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            // get the hmm site comp data for domain interfaces to peptide interfaces
     //       DataTable pepDomainHmmSiteCompTable = GetPfamPepChainInterfaceCompTable(pfamId, "PfamChainInterfaceHmmSiteComp");
            DataTable pepDomainHmmSiteCompTable = GetMorePfamPepChainInterfaceCompTable(pfamId, "PfamChainInterfaceHmmSiteComp");
            // remove chain interfaces in which one chain is a peptide
            RemoveChainInterfacesWithPeptides(pepDomainHmmSiteCompTable, asuInfoTable);

            int clusterId = 0;
            string clusterFileName = "";
            List<string> clusterFileList = new List<string> ();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                try
                {
                    clusterFileName = CompressClusterPeptideChainInterfaceFiles(pfamId, clusterId, pepDomainHmmSiteCompTable, clusterFileDir, dataWriter);
                    if (clusterFileName != "")
                    {
                        clusterFileList.Add(clusterFileName);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + clusterId + " Compress domain interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            tarOperator.RunTar(pfamId + ".tar", clusterFileList.ToArray (), clusterFileDir, false);

            foreach (string clusterFile in clusterFileList)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterFile));
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterFileDir"></param>
        /// <param name="dataWriter"></param>
        /// <returns></returns>
        private string CompressClusterPeptideChainInterfaceFiles(string pfamId, int clusterId, DataTable pepChainHmmSiteCompTable,
            string clusterFileDir, StreamWriter dataWriter)
        {
            string[] allClusterPeptideInterfaces = pepInterfaceCluster.GetClusterPeptideInterfaces(pfamId, clusterId);
            string[] clusterPeptideInterfaces = RemoveSamePeptideInterfaces(pfamId, allClusterPeptideInterfaces);

            string[] clusterPepInterfaceFileNames = FormatClusterDomainInterfacesToFileFormat(clusterPeptideInterfaces);
            string clusterRepPepInterface = GetClusterRepPepInterface(pfamId, clusterPepInterfaceFileNames);
            int[] clusterHmmSites = GetClusterCommonHmmSites (clusterPeptideInterfaces );
            //  int[] conservativeSeqIds = GetRepresentativeCoordSeqIds(clusterRepPepInterface, clusterHmmSites);

      //      string[] addedChainInterfaces = GetSimilarDomainInterfaces(pfamId, clusterPeptideInterfaces, pepChainHmmSiteCompTable/*, clusterHmmSites*/);
            string[] addedChainInterfaces = GetSimilarDomainInterfaces(pfamId, clusterPeptideInterfaces, pepChainHmmSiteCompTable, clusterHmmSites);
            WriteChainInterfaceInfoToFile(pfamId, clusterId, addedChainInterfaces, pepChainHmmSiteCompTable, dataWriter);

            string[] addedChainInterfaceFileNames = FormatClusterChainInterfacesToFileFormat(addedChainInterfaces);
            string[] clusterChainInterfaceFiles = new string[addedChainInterfaceFileNames.Length + 1];
            //    string[] clusterRepPepInterfaces = new string[1];
            //   string[] clusterRepInterfaceFileNames = FormatClusterDomainInterfacesToFileFormat(clusterRepPepInterfaces);
            clusterChainInterfaceFiles[0] = clusterRepPepInterface;
            Array.Copy(addedChainInterfaceFileNames, 0, clusterChainInterfaceFiles, 1, addedChainInterfaceFileNames.Length);

            if (clusterChainInterfaceFiles.Length <= 1)
            {
                return "";
            }

            //    string[] clusterChainInterfaceFiles = new string[clusterPepInterfaceFileNames.Length + addedChainInterfaceFileNames.Length];
            //    Array.Copy(clusterPepInterfaceFileNames, 0, clusterChainInterfaceFiles, 0, clusterPepInterfaceFileNames.Length);
            //    Array.Copy(addedChainInterfaceFileNames, 0, clusterChainInterfaceFiles, clusterPepInterfaceFileNames.Length, addedChainInterfaceFileNames.Length);

            //       string[] clusterChainInterfaceFiles = FormatClusterChainInterfacesToFileFormat (clusterChainInterfaces);

            DataTable peptideInterfaceTable = GetClusterPeptideInterfaceDefTable(clusterPeptideInterfaces);
            AddChainInterfaceDefTable(addedChainInterfaces, peptideInterfaceTable, pepChainHmmSiteCompTable);

            //       DataTable hmmSiteCompTable = GetPeptideInterfaceHmmSiteCompTable(pfamId);

            string clusterFileName = clusterCompress.CompressClusterPeptideInterfaceFiles(pfamId, clusterId, clusterChainInterfaceFiles,
                peptideInterfaceTable, clusterFileDir);
            return clusterFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterPeptideInterfaces"></param>
        /// <returns></returns>
        private string GetClusterRepPepInterface(string pfamId, string[] clusterPeptideInterfaces)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PfamID = '{0}' AND PepComp = '1';", pfamId);
            DataTable pfamHmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            string repClusterInterface = clusterCompress.interfacePymolScript.GetDomainInterfaceWithMostCommonHmmSites(clusterPeptideInterfaces, pfamHmmSiteCompTable);
            return repClusterInterface;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="hmmSiteCompTableName"></param>
        /// <returns></returns>
        private DataTable GetPfamPepChainInterfaceCompTable(string pfamId, string hmmSiteCompTableName)
        {
            
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp  " +
                            " Where PfamID = '{0}' AND NumOfCommonHmmSites >= {1} AND LocalPepRmsd <= {2} AND LocalPepRmsd > 0 " + 
                            " AND PepEnd - PepStart + 1 >= {3};",
                            pfamId, pepInterfaceCluster.numOfComHmmSitesGood, rmsdCutoff, alignLengthCutoff);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="hmmSiteCompTableName"></param>
        /// <returns></returns>
        private DataTable GetMorePfamPepChainInterfaceCompTable(string pfamId, string hmmSiteCompTableName)
        {                                  
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp  " +
                           " Where PfamID = '{0}' AND NumOfCommonHmmSites >= {1} AND LocalPepRmsd <= 10.0 AND LocalPepRmsd > 0 " +
                           " AND PepEnd - PepStart + 1 >= 2;",
                           pfamId, pepInterfaceCluster.numOfComHmmSiteCutoff);
            /*   string queryString = string.Format("Select * From PfamchainInterfaceHmmSiteComp " +
                   " Where PfamID = '{0}' AND NumOfCommonHmmSites >= {1};", pfamId, pepInterfaceCluster.numOfComHmmSitesGood);*/
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfaces"></param>
        /// <returns></returns>
        private string[] FormatClusterChainInterfacesToFileFormat(string[] clusterChainInterfaces)
        {
            string[] clusterChainInterfaceFiles = new string[clusterChainInterfaces.Length];
            string pdbId = "";
            string interfaceId = "";
            for (int i = 0; i < clusterChainInterfaces.Length; i++)
            {
                pdbId = clusterChainInterfaces[i].Substring(0, 4);
                interfaceId = clusterChainInterfaces[i].Substring(4, clusterChainInterfaces[i].Length - 4);
                clusterChainInterfaceFiles[i] = pdbId + "_" + interfaceId;
            }
            return clusterChainInterfaceFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfaces"></param>
        /// <returns></returns>
        private void AddChainInterfaceDefTable(string[] clusterChainInterfaces, DataTable clusterDomainInterfaceTable, DataTable pepChainHmmSiteCompTable)
        {
            string pdbId = "";
            int interfaceId = 0;
            foreach (string chainInterface in clusterChainInterfaces)
            {
                pdbId = chainInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(chainInterface.Substring(4, chainInterface.Length - 4));
                DataTable domainInterfaceTable = GetChainInterfaceDomainDefTable(pdbId, interfaceId, pepChainHmmSiteCompTable);
                ParseHelper.AddNewTableToExistTable(domainInterfaceTable, ref clusterDomainInterfaceTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceDomainDefTable(string pdbId, int interfaceId, DataTable pepChainHmmSiteCompTable)
        {
            string queryString = string.Format("Select PdbID, InterfaceID, AsymChain1, AsymChain2" +
                " From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataRow[] hmmSiteCompRows = pepChainHmmSiteCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, interfaceId));
            interfaceDefTable.Columns.Add(new DataColumn("DomainInterfaceID"));
            interfaceDefTable.Columns.Add(new DataColumn("DomainID1"));
            interfaceDefTable.Columns.Add(new DataColumn("ChainDomainID1"));
            interfaceDefTable.Columns.Add(new DataColumn("DomainID2"));
            interfaceDefTable.Columns.Add(new DataColumn("ChainDomainID2"));
            interfaceDefTable.Columns.Add(new DataColumn("RelSeqID"));
            interfaceDefTable.Columns.Add(new DataColumn("NumOfAtomPairs"));
            interfaceDefTable.Columns.Add(new DataColumn("NumOfResiduePairs"));


            DataRow interfaceDefRow = interfaceDefTable.Rows[0];
            interfaceDefRow["DomainInterfaceID"] = interfaceDefRow["InterfaceID"];
            interfaceDefRow["DomainID1"] = -1;
            interfaceDefRow["ChainDomainID1"] = -1;
            interfaceDefRow["DomainID2"] = -1;
            interfaceDefRow["ChainDomainID2"] = -1;
            interfaceDefRow["RelSeqID"] = -1;
            interfaceDefRow["NumOfAtomPairs"] = -1;
            interfaceDefRow["NumOfResiduePairs"] = -1;

            string chainNo = "";
            long domainId = 0;
            string asymChain = "";
            foreach (DataRow hmmSiteCompRow in hmmSiteCompRows)
            {
                chainNo = hmmSiteCompRow["ChainNO"].ToString();
                if (chainNo == "A")
                {
                    interfaceDefRow["RelSeqID"] = hmmSiteCompRow["RelSeqID2"];
                    interfaceDefRow["DomainID1"] = hmmSiteCompRow["DomainID2"];
                    domainId = Convert.ToInt64(hmmSiteCompRow["DomainID2"].ToString());
                    asymChain = interfaceDefRow["AsymChain1"].ToString().TrimEnd();
                    interfaceDefRow["ChainDomainID1"] = GetChainDomainID(pdbId, domainId, asymChain);
                }
                else if (chainNo == "B")
                {
                    interfaceDefRow["RelSeqID"] = hmmSiteCompRow["RelSeqID2"];
                    interfaceDefRow["DomainID2"] = hmmSiteCompRow["DomainID2"];
                    domainId = Convert.ToInt64(hmmSiteCompRow["DomainID2"].ToString());
                    asymChain = interfaceDefRow["AsymChain2"].ToString().TrimEnd();
                    interfaceDefRow["ChainDomainID2"] = GetChainDomainID(pdbId, domainId, asymChain);
                }
            }
            return interfaceDefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private int GetChainDomainID(string pdbId, long domainId, string asymChain)
        {
            string queryString = string.Format("Select ChainDomainID From PdbPfamChain " +
                " Where PdbID = '{0}' AND DomainID = {1} AND AsymChain = '{2}';", pdbId, domainId, asymChain);
            DataTable chainDomainIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (chainDomainIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(chainDomainIdTable.Rows[0]["ChainDomainID"].ToString());
            }
            return -1;
        }
        #endregion

        #region remove chain interfaces with peptides
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepDomainHmmSiteCompTable"></param>
        /// <param name="asumInfoTable"></param>
        private void RemoveChainInterfacesWithPeptides(DataTable pepDomainHmmSiteCompTable, DataTable asuTable)
        {
            List<DataRow> removeDataRowList = new List<DataRow> ();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow hmmSiteCompRow in pepDomainHmmSiteCompTable.Rows)
            {
                pdbId = hmmSiteCompRow["PdbID2"].ToString();
                interfaceId = Convert.ToInt32(hmmSiteCompRow["DomainInterfaceID2"].ToString());
                if (IsChainInterfacePeptide(pdbId, interfaceId, asuTable))
                {
                    removeDataRowList.Add(hmmSiteCompRow);
                }
            }
            foreach (DataRow hmmSiteRow in removeDataRowList)
            {
                pepDomainHmmSiteCompTable.Rows.Remove(hmmSiteRow);
            }
            pepDomainHmmSiteCompTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private bool IsChainInterfacePeptide(string pdbId, int interfaceId, DataTable asuTable)
        {
            string queryString = string.Format("Select AsymChain1, AsymChain2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceId = {1};", pdbId, interfaceId);
            DataTable asymChainPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (asymChainPairTable.Rows.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + interfaceId.ToString () + " no interface definiton!!!" );
                ProtCidSettings.logWriter.WriteLine(pdbId + interfaceId.ToString() + " no interface definiton!!!");
                ProtCidSettings.logWriter.Flush();
                return false;
            }
            string asymChain1 = asymChainPairTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
            string asymChain2 = asymChainPairTable.Rows[0]["AsymChain2"].ToString().TrimEnd();
            bool isPepChain1 = IsChainPeptide(pdbId, asymChain1, asuTable);
            if (asymChain2 == asymChain1)
            {
                return isPepChain1;
            }
            else
            {
                bool isPepChain2 = IsChainPeptide(pdbId, asymChain2, asuTable);
                if (isPepChain1 || isPepChain2)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region write chain interface info to file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <param name="addedDomainInterfaces"></param>
        /// <param name="pepDomainHmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void WriteChainInterfaceInfoToFile(string pfamId, int clusterId, string[] addedChainInterfaces, DataTable pepChainHmmSiteCompTable, StreamWriter dataWriter)
        {
            string dataLine = "";
            string pdbId = "";
            int interfaceId = 0;
            int[] groupClusterInfo = null;
            string groupPfamArch = "";
            Dictionary<string, string> clusterInterfaceClusterInfoHash = new Dictionary<string,string> ();
            foreach (string chainInterface in addedChainInterfaces)
            {
                pdbId = chainInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(chainInterface.Substring(4, chainInterface.Length - 4));
                groupClusterInfo = GetChainInterfaceClusterInfo(pdbId, interfaceId, out groupPfamArch);
                DataRow[] pepChainCompRows = pepChainHmmSiteCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, interfaceId));
                foreach (DataRow pepChainCompRow in pepChainCompRows)
                {
                    dataLine = pfamId + "\t" + clusterId.ToString() + "\t" + pepChainCompRow["PdbID1"].ToString() + "\t" +
                        pepChainCompRow["DomainInterfaceID1"].ToString() + "\t" + pepChainCompRow["PdbID2"].ToString() + "\t" +
                        pepChainCompRow["DomainInterfaceID2"].ToString() + "\t" + pepChainCompRow["NumOfCommonHmmSites"].ToString() + "\t" +
                        pepChainCompRow["ChainRmsd"].ToString() + "\t" + pepChainCompRow["InteractChainRmsd"].ToString() + "\t" +
                        pepChainCompRow["PepRmsd"].ToString() + "\t" + pepChainCompRow["InteractPepRmsd"].ToString() + "\t" +
                        pepChainCompRow["PepAlignment"].ToString() + "\t" + pepChainCompRow["ChainAlignment"].ToString() + "\t" +
                        "[" + pepChainCompRow["PepStart"].ToString() + "-" + pepChainCompRow["PepEnd"].ToString() + "]\t" +
                        "[" + pepChainCompRow["ChainStart"].ToString() + "-" + pepChainCompRow["ChainEnd"].ToString() + "]\t" +
                        pepChainCompRow["Score"].ToString() + "\t" + pepChainCompRow["LocalPepRmsd"].ToString() + "\t" +
                        groupClusterInfo[0].ToString() + "\t" + groupClusterInfo[1].ToString() + "\t" + groupPfamArch;
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int[] GetChainInterfaceClusterInfo(string pdbId, int interfaceId, out string chainPairPfamArch)
        {
            string queryString = string.Format("Select SuperGroupSeqID, ClusterID From PfamSuperInterfaceClusters Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] groupClusterInfo = new int[2];
            chainPairPfamArch = "";
            if (clusterIdTable.Rows.Count > 0)
            {
                groupClusterInfo[0] = Convert.ToInt32(clusterIdTable.Rows[0]["SuperGroupSeqID"].ToString());
                groupClusterInfo[1] = Convert.ToInt32(clusterIdTable.Rows[0]["ClusterID"].ToString());
                chainPairPfamArch = GetChainPfamArchPair(groupClusterInfo[0]);
            }
            else
            {
                chainPairPfamArch = GetChainPfamArchPair(pdbId, interfaceId);
                groupClusterInfo[0] = GetSuperGroupId(chainPairPfamArch);
                groupClusterInfo[1] = -1;
            }
            return groupClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetChainPfamArchPair(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceEntityTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainPairPfamArch = "";
            if (interfaceEntityTable.Rows.Count > 0)
            {
                int entityId1 = Convert.ToInt32(interfaceEntityTable.Rows[0]["EntityID1"].ToString());
                int entityId2 = Convert.ToInt32(interfaceEntityTable.Rows[0]["EntityID2"].ToString());
                string entityPfamArch1 = GetEntityGroupPfamArch(pdbId, entityId1);
                if (entityId1 == entityId2)
                {
                    chainPairPfamArch = entityPfamArch1;
                }
                else
                {
                    string entityPfamArch2 = GetEntityGroupPfamArch(pdbId, entityId2);
                    chainPairPfamArch = entityPfamArch1 + ";" + entityPfamArch2;
                    if (string.Compare(entityPfamArch1, entityPfamArch2) > 0)
                    {
                        chainPairPfamArch = entityPfamArch2 + ";" + entityPfamArch1;
                    }
                }
            }
            return chainPairPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPairPfamArch"></param>
        /// <returns></returns>
        private int GetSuperGroupId(string chainPairPfamArch)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamArch = '{0}';", chainPairPfamArch);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = -1;
            if (groupIdTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32(groupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            return groupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetChainPfamArchPair(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamARch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable chainPfamArchTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainPfamArch = "";
            if (chainPfamArchTable.Rows.Count > 0)
            {
                chainPfamArch = chainPfamArchTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            }
            return chainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntityGroupPfamArch(string pdbId, int entityId)
        {
            string entityPfamArch = pfamArch.GetEntityGroupPfamArch(pdbId, entityId);
            return entityPfamArch;
        }
        #endregion

        #region common hmm sites in a cluster
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRepInterface"></param>
        /// <param name="clusterHmmSeqIds"></param>
        /// <returns></returns>
        private int[] GetPeptideInterfacePeptideCoordSeqIds(string peptideInterface, int[] clusterHmmSeqIds)
        {
            string pdbId = peptideInterface.Substring(0, 4);
            int domainInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
            string queryString = string.Format("Select InterfaceID, SeqID, HmmSeqId From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable seqIdMapTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> seqIdList = new List<int> ();
            int seqId = 0;
            int hmmSeqId = 0;
            int interfaceId = Convert.ToInt32(seqIdMapTable.Rows[0]["InterfaceID"].ToString());
            foreach (DataRow seqIdMapRow in seqIdMapTable.Rows)
            {
                hmmSeqId = Convert.ToInt32(seqIdMapRow["HmmSeqID"].ToString());
                if (clusterHmmSeqIds.Contains(hmmSeqId))
                {
                    seqId = Convert.ToInt32(seqIdMapRow["SeqID"].ToString());
                    if (!seqIdList.Contains(seqId))
                    {
                        seqIdList.Add(seqId);
                    }
                }
            }
            seqIdList.Sort();

            int[] pepSeqIds = GetPeptideCoordSeqIds(pdbId, interfaceId, seqIdList.ToArray ());

            return pepSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainSeqIds"></param>
        /// <returns></returns>
        private int[] GetPeptideCoordSeqIds(string pdbId, int interfaceId, int[] chainSeqIds)
        {
            //     string queryString = string.Format("Select SeqId, PepSeqID From ChainPeptideAtomPairs Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            string queryString = string.Format("Select SeqId, PepSeqID From ChainPeptideAtomPairs Where PdbID = '{0}' AND InterfaceID = {1} AND Distance <= {2};", 
                pdbId, interfaceId, chainPepAtomDistCutoff);
            DataTable pepSeqMapTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            List<int> pepSeqIdList = new List<int> ();
            int seqId = 0;
            int pepSeqId = 0;
            foreach (DataRow pepSeqMapRow in pepSeqMapTable.Rows)
            {
                seqId = Convert.ToInt32(pepSeqMapRow["SeqID"].ToString());
                pepSeqId = Convert.ToInt32(pepSeqMapRow["PepSeqID"].ToString());
                if (chainSeqIds.Contains(seqId))
                {
                    if (!pepSeqIdList.Contains(pepSeqId))
                    {
                        pepSeqIdList.Add(pepSeqId);
                    }
                }
            }
            return pepSeqIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterPeptideInterfaces"></param>
        /// <returns></returns>
        private int[] GetClusterCommonHmmSites(string[] clusterPeptideInterfaces)
        {
            List<int> commonHmmSiteList = new List<int> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            Dictionary<int, int> commonHmmSiteCountHash = new Dictionary<int,int> ();
            foreach (string peptideInterface in clusterPeptideInterfaces)
            {
                pdbId = peptideInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
                int[] hmmSeqIds = GetPeptideHmmSites(pdbId, domainInterfaceId);
                foreach (int hmmSeqId in hmmSeqIds)
                {
                    if (commonHmmSiteCountHash.ContainsKey(hmmSeqId))
                    {
                        int count = (int)commonHmmSiteCountHash[hmmSeqId];
                        count++;
                        commonHmmSiteCountHash[hmmSeqId] = count;
                    }
                    else
                    {
                        commonHmmSiteCountHash.Add(hmmSeqId, 1);
                    }
                }
            }
            double hmmSitePercent = 0;
            foreach (int hmmSeqId in commonHmmSiteCountHash.Keys)
            {
                int count = (int)commonHmmSiteCountHash[hmmSeqId];
                hmmSitePercent = (double)count / (double)clusterPeptideInterfaces.Length;
                if (hmmSitePercent >= percentInterfacesCluster)
                {
                    commonHmmSiteList.Add(hmmSeqId);
                }
            }
            return commonHmmSiteList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepInterfaceId"></param>
        /// <returns></returns>
        private int[] GetPeptideHmmSites(string pdbId, int pepInterfaceId)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, pepInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> hmmSiteList = new List<int> ();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteTable.Rows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString());
                hmmSiteList.Add(hmmSeqId);
            }
            return hmmSiteList.ToArray ();
        }
        #endregion

        #endregion


        #region peptide interface and chain interfaces - for debug
        /// <summary>
        /// 
        /// </summary>
        public void FindChainInterfacesNotIn()
        {
            string chainInfoFile_3ow4 = @"D:\DbProjectData\pfam\PfamPeptide\PepClusters_domain\3ow416_SimChainInterfacesInfo.txt";
            string[] chainInterfaces_3ow4 = GetChainInterfacesInFile(chainInfoFile_3ow4);

            string pepChainClusterInfoFile = @"D:\DbProjectData\pfam\PfamPeptide\PepClusters_domain\PeptideClusterChainInterfacesInfo_pkinase.txt";
            StreamWriter pkinaseChainInterfaceInfoWriter = new StreamWriter(@"D:\DbProjectData\pfam\PfamPeptide\PepClusters_domain\PkinaseChainInterfacesInfo.txt");
            string[] repPepPdbIds = { "2xh5", "3qhr", "2wo6", "1l3r", "3o7l", "4dc2", "2q0n", "2phk", "3cy2" };
            StreamReader dataReader = new StreamReader(pepChainClusterInfoFile);
            string line = "";
            List<string> otherChainInterfaceList = new List<string> ();
            string chainInterface = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields[1] != "2")
                {
                    continue;
                }
                if (repPepPdbIds.Contains(fields[2]))
                {
                    chainInterface = fields[4] + fields[5];
                    if (otherChainInterfaceList.Contains(chainInterface))
                    {
                        continue;
                    }
                    if (chainInterfaces_3ow4.Contains(chainInterface))
                    {
                        continue;
                    }
                    otherChainInterfaceList.Add(chainInterface);
                    pkinaseChainInterfaceInfoWriter.WriteLine(line);
                }

            }
            dataReader.Close();
            pkinaseChainInterfaceInfoWriter.Close();
            /*    StreamWriter chainInterfaceWriter = new StreamWriter(@"D:\DbProjectData\pfam\PfamPeptide\PepClusters_domain\Pkinase2ChainInterfaces.txt");
                chainInterfaceWriter.WriteLine("With 3ow4");
                foreach (string chainInterface_3ow4 in chainInterfaces_3ow4)
                {
                    chainInterfaceWriter.WriteLine(chainInterface_3ow4);
                }
                chainInterfaceWriter.WriteLine("Others");
                foreach (string otherChainInterface in otherChainInterfaceList)
                {
                    chainInterfaceWriter.WriteLine(otherChainInterface);
                }
                chainInterfaceWriter.Close();*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string[] GetChainInterfacesInFile(string fileName)
        {
            List<string> chainInterfaceList = new List<string> ();
            StreamReader dataReader = new StreamReader(fileName);
            string line = "";
            string chainInterface = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                chainInterface = fields[4] + fields[5];
                if (!chainInterfaceList.Contains(chainInterface))
                {
                    chainInterfaceList.Add(chainInterface);
                }
            }
            dataReader.Close();
            return chainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompressPepChainInterfaces()
        {
            string pepPdbId = "3ow4";
            int pepDomainInterfaceId = 16;
            string pfamId = "Pkinase";
            string clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PepClusters_domain");
            CompressPepInterfaceAndChainInterfaces(pfamId, pepPdbId, pepDomainInterfaceId, clusterFileDir);
        }

        public string CompressPepInterfaceAndChainInterfaces(string pfamId, string pepPdbId, int pepDomainInterfaceId, string clusterFileDir)
        {
            DataTable pepChainHmmSiteCompTable = GetHmmSiteCompTable(pfamId, pepPdbId, pepDomainInterfaceId);
            string[] clusterPeptideInterfaces = new string[1];
            clusterPeptideInterfaces[0] = pepPdbId + pepDomainInterfaceId.ToString();
            string[] addedChainInterfaces = GetSimilarDomainInterfaces(pfamId, clusterPeptideInterfaces, pepChainHmmSiteCompTable);

            /*     StreamWriter dataWriter = new StreamWriter(pepPdbId + pepDomainInterfaceId.ToString() + "_SimChainInterfacesInfo.txt");
                 WriteChainInterfaceInfoToFile(pfamId, 2, addedChainInterfaces, pepChainHmmSiteCompTable, dataWriter);
                 dataWriter.Close();
                 */
            string[] pepClusterInterfaceFileNames = FormatClusterDomainInterfacesToFileFormat(clusterPeptideInterfaces);
            string[] addedChainInterfaceFileNames = FormatClusterChainInterfacesToFileFormat(addedChainInterfaces);

            string[] clusterChainInterfaceFiles = new string[pepClusterInterfaceFileNames.Length + addedChainInterfaceFileNames.Length];
            Array.Copy(pepClusterInterfaceFileNames, 0, clusterChainInterfaceFiles, 0, pepClusterInterfaceFileNames.Length);
            Array.Copy(addedChainInterfaceFileNames, 0, clusterChainInterfaceFiles, pepClusterInterfaceFileNames.Length, addedChainInterfaceFileNames.Length);

            //       string[] clusterChainInterfaceFiles = FormatClusterChainInterfacesToFileFormat(clusterChainInterfaces);

            DataTable peptideInterfaceTable = GetClusterPeptideInterfaceDefTable(clusterPeptideInterfaces);
            AddChainInterfaceDefTable(addedChainInterfaces, peptideInterfaceTable, pepChainHmmSiteCompTable);

            //        DataTable hmmSiteCompTable = GetPeptideInterfaceHmmSiteCompTable(pfamId);

            string clusterFileName = clusterCompress.CompressClusterPeptideInterfaceFiles(pfamId, 2, clusterChainInterfaceFiles,
                                            peptideInterfaceTable, clusterFileDir);
            return clusterFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pepPdbId"></param>
        /// <param name="pepDomainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetHmmSiteCompTable(string pfamId, string pepPdbId, int pepDomainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp " +
                " WHere PfamID = '{0}' AND PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND NumOfCommonHmmSites >= {3} AND LocalPepRmsd <= {4};",
                pfamId, pepPdbId, pepDomainInterfaceId, pepInterfaceCluster.numOfComHmmSitesGood, rmsdCutoff);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }
        #endregion

    }
}
