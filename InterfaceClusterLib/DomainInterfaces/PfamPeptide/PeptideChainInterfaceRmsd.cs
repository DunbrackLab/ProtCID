using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PeptideChainInterfaceRmsd : PeptideInterfaceRmsd
    {
        private int numOfInterfaces = 100;

        /// <summary>
        /// 
        /// </summary>
        public void CalculatePeptideChainRmsd()
        {
            rmsdDataFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\pepChainComp");
            if (!Directory.Exists(rmsdDataFileDir))
            {
                Directory.CreateDirectory(rmsdDataFileDir);
            }
            hmmSiteCompTableName = "PfamChainInterfaceHmmSiteComp";
            leastNumOfCommonHmmSites = 3;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptide and chain of pfam interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideChainInterfaceRmsd.txt"), true);
            string[] pfamIds = GetPfamIds();
         //   string[] pfamIds = {"Pkinase", "Pkinase_Tyr"};

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PFAM IDs: " + pfamIds.Length.ToString());
            int pfamCount = 1;
            foreach (string pfamId in pfamIds)
         //   string pfamId = "";
         //   for (int i = pfamIds.Length - 1; i >= 0; i -- )
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString() + ": " + pfamId);
                pfamCount++;

                try
                {
                    CalculateClusterPeptideChainInterfaceRmsd(pfamId, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            rmsdWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateCalculatePeptideChainRmsd(string[] updateEntries)
        {
            rmsdDataFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\pepChainComp");
            if (!Directory.Exists(rmsdDataFileDir))
            {
                Directory.CreateDirectory(rmsdDataFileDir);
            }
            hmmSiteCompTableName = "PfamChainInterfaceHmmSiteComp";
            leastNumOfCommonHmmSites = 3;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptide and chain of pfam interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideChainInterfaceRmsd.txt"), true);
            string[] updatePfamIds = GetPfamIds(updateEntries);
        //    string[] updatePfamIds = { "Cupin_4" };

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PFAM IDs: " + updatePfamIds.Length.ToString());

            int pfamCount = 1;
            foreach (string pfamId in updatePfamIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString() + ": " + pfamId);
                pfamCount++;

                try
                {
                    CalculateClusterPeptideChainInterfaceRmsd(pfamId, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            rmsdWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CalculatePeptideChainInterfaceRmsd(string pfamId, StreamWriter rmsdWriter)
        {
            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");

        /*    if (File.Exists(pfamPymolScriptFile))
            {
                return;
            }*/
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId, leastNumOfCommonHmmSites);
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);

            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();

            Dictionary<string, string[]> pepChainInterfacesHash = GetPepCompChainInterfacesHash(hmmSiteCompTable);

            // get the interface domain def table for both peptide interfaces and the chain interfaces
            List<string> pepInterfaceList = new List<string> (pepChainInterfacesHash.Keys);
            string[] pepInterfaces = new string[pepInterfaceList.Count];
            pepInterfaceList.CopyTo(pepInterfaces);
            DataTable interfaceDomainDefTable = GetDomainInterfaceDefTable (pepInterfaces);
            // try to fit the table format into domain interface format
            DataTable chainInterfaceDomainDefTable = GetChainInterfaceDomainDefTable(hmmSiteCompTable);
            ParseHelper.AddNewTableToExistTable(chainInterfaceDomainDefTable, ref interfaceDomainDefTable);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(pepChainInterfacesHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            foreach (string pepInterface in pepChainInterfacesHash.Keys)
            {
                string[] compChainInterfaces = (string[])pepChainInterfacesHash[pepInterface];
                string[][] divCompInterfaces = DivideCompInterfaces(compChainInterfaces);
                int subGroupCount = 1;
                try
                {
                    foreach (string[] compInterfaces in divCompInterfaces)
                    {
                        CalculateDomainInterfacePeptideRmsd(pfamId, pepInterface, compInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                     interfaceDomainDefTable, domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);

                        RenameCoordAndPymolFiles(pfamId, pepInterface, subGroupCount);
                        subGroupCount++;
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                MoveCoordAndPymolFiles();
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir, "*.cryst*");
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CalculateClusterPeptideChainInterfaceRmsd(string pfamId, StreamWriter rmsdWriter)
        {
            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");

            if (File.Exists(pfamPymolScriptFile))
            {
                return;
            }
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId, leastNumOfCommonHmmSites);
            if (hmmSiteCompTable.Rows.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + ": no data with the number of common hmm sites > " + leastNumOfCommonHmmSites.ToString ());
                ProtCidSettings.logWriter.WriteLine(pfamId + ": no data with the number of common hmm sites > " + leastNumOfCommonHmmSites.ToString());
                ProtCidSettings.logWriter.Flush();
                return;
            }
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);

            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();
            // the representative peptide interfaces for pfam peptide interface clusters
            // one peptide interface for one cluster
         //   Hashtable pepChainInterfacesHash = GetPepCompChainInterfacesHash (pfamId, hmmSiteCompTable);

            Dictionary<string, string[]> pepChainInterfacesHash = GetPepCompChainInterfacesHash(hmmSiteCompTable);

            // get the interface domain def table for both peptide interfaces and the chain interfaces
            List<string> pepInterfaceList = new List<string> (pepChainInterfacesHash.Keys);
            string[] pepInterfaces = new string[pepInterfaceList.Count];
            pepInterfaceList.CopyTo(pepInterfaces);
            DataTable interfaceDomainDefTable = GetDomainInterfaceDefTable(pepInterfaces);
            // try to fit the table format into domain interface format
            DataTable chainInterfaceDomainDefTable = GetChainInterfaceDomainDefTable(hmmSiteCompTable);
            ParseHelper.AddNewTableToExistTable(chainInterfaceDomainDefTable, ref interfaceDomainDefTable);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(pepChainInterfacesHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            foreach (string pepInterface in pepChainInterfacesHash.Keys)
            {
                string[] compChainInterfaces = (string[])pepChainInterfacesHash[pepInterface];
                string[][] divCompInterfaces = DivideCompInterfaces(compChainInterfaces);
                int subGroupCount = 1;
                try
                {
                    foreach (string[] compInterfaces in divCompInterfaces)
                    {
                        CalculateDomainInterfacePeptideRmsd(pfamId, pepInterface, compInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                     interfaceDomainDefTable, domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);

                        RenameCoordAndPymolFiles(pfamId, pepInterface, subGroupCount);
                        subGroupCount++;
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                MoveCoordAndPymolFiles();
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir, "*.cryst*");
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ZipCoordFiles()
        {
            string coordDataDir = @"D:\DbProjectData\pfam\PfamPeptide\pepChainComp";
            string[] coordFiles = Directory.GetFiles(coordDataDir, "*.coord");
            foreach (string coordFile in coordFiles)
            {
                ParseHelper.ZipPdbFile(coordFile);
            }
        }
        /// <summary>
        /// save coord and pymol files
        /// </summary>
        private void MoveCoordAndPymolFiles()
        {
            string[] coordFiles = Directory.GetFiles(ProtCidSettings.tempDir, "*.coord*");
            string destFile = "";
            foreach (string coordFile in coordFiles)
            {
                FileInfo fileInfo = new FileInfo (coordFile);
                destFile = Path.Combine(rmsdDataFileDir, fileInfo.Name);
                File.Move(coordFile, destFile);
            }
            string[] pymolFiles = Directory.GetFiles (ProtCidSettings.tempDir, "*.pml");
            foreach (string pymolFile in pymolFiles)
            {
                FileInfo fileInfo = new FileInfo(pymolFile);
                destFile = Path.Combine(rmsdDataFileDir, fileInfo.Name);
                File.Move(pymolFile, destFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="subGroupCount"></param>
        private void RenameCoordAndPymolFiles(string pfamId, string centerInterface, int subGroupCount)
        {
            string coordFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface + ".coord");
            string newCoordFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface + "_" + subGroupCount.ToString() + ".coord");
            if (File.Exists(newCoordFile))
            {
                File.Delete(newCoordFile);
            }
            File.Move(coordFile, newCoordFile);
            ParseHelper.ZipPdbFile(newCoordFile);

            string pymolFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface + "_pairFit.pml");
            string newPymolFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface +  "_" + subGroupCount.ToString () + "_pairFit.pml");
            if (File.Exists(newPymolFile))
            {
                File.Delete(newPymolFile);
            }
            File.Move(pymolFile, newPymolFile);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="compInterfaces"></param>
        /// <returns></returns>
        private string[][] DivideCompInterfaces(string[] compInterfaces)
        {
            int numOfArrays = (int)Math.Ceiling ((double)compInterfaces.Length / (double)numOfInterfaces);
            string[][] dividedCompInterfaces = new string[numOfArrays][];
            int arrayLength = 0;
            for (int i = 0; i < numOfArrays; i ++ )
            {
                if ((i + 1) * numOfInterfaces > compInterfaces.Length)
                {
                    arrayLength = compInterfaces.Length - i * numOfInterfaces;
                    string[] divCompInterfaces = new string[compInterfaces.Length - i * numOfInterfaces];
                    Array.Copy(compInterfaces, i * numOfInterfaces, divCompInterfaces, 0, arrayLength);
                    dividedCompInterfaces[i] = divCompInterfaces;
                }
                else
                {
                    arrayLength = numOfInterfaces;
                    string[] divCompInterfaces = new string[numOfInterfaces];
                    Array.Copy(compInterfaces, i * numOfInterfaces, divCompInterfaces, 0, arrayLength);
                    dividedCompInterfaces[i] = divCompInterfaces;
                }
            }
            return dividedCompInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetPepCompChainInterfacesHash(DataTable hmmSiteCompTable)
        {
            Dictionary<string, List<string>> pepChainInterfacesListHash = new Dictionary<string,List<string>> ();
            string pepInterface = "";
            string chainInterface = "";
            foreach (DataRow pepChainCompRow in hmmSiteCompTable.Rows)
            {
                pepInterface = pepChainCompRow["PdbID1"].ToString() + "_d" + pepChainCompRow["DomainInterfaceID1"].ToString();
                chainInterface = pepChainCompRow["PdbID2"].ToString() + "_" + pepChainCompRow["DomainInterfaceID2"].ToString();
                if (pepChainInterfacesListHash.ContainsKey (pepInterface))
                {
                    if (!pepChainInterfacesListHash[pepInterface].Contains(chainInterface))
                    {
                        pepChainInterfacesListHash[pepInterface].Add(chainInterface);
                    }
                }
                else
                {
                    List<string> chainInterfaceList = new List<string> ();
                    chainInterfaceList.Add(chainInterface);
                    pepChainInterfacesListHash.Add(pepInterface, chainInterfaceList);
                }
            }
            Dictionary<string, string[]> pepChainInterfacesHash = new Dictionary<string, string[]>();
            foreach (string lsPepInterface in pepChainInterfacesListHash.Keys)
            {
                pepChainInterfacesHash.Add(lsPepInterface, pepChainInterfacesListHash[lsPepInterface].ToArray()); ;
            }
            return pepChainInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetPepCompChainInterfacesHash(string pfamId, DataTable hmmSiteCompTable)
        {
            string[] pfamRepPepInterfaces = GetPfamRepPepInterfaces (pfamId);
            Dictionary<string, List<string>> pepChainInterfaceListHash = new Dictionary<string,List<string>> ();
            string pepInterface = "";
            string chainInterface = "";
            foreach (DataRow pepChainCompRow in hmmSiteCompTable.Rows)
            {
                pepInterface = pepChainCompRow["PdbID1"].ToString() + "_d" + pepChainCompRow["DomainInterfaceID1"].ToString();
                if (! pfamRepPepInterfaces.Contains(pepInterface))
                {
                    continue;
                }
                chainInterface = pepChainCompRow["PdbID2"].ToString() + "_" + pepChainCompRow["DomainInterfaceID2"].ToString();
                if (pepChainInterfaceListHash.ContainsKey(pepInterface))
                {
                    if (!pepChainInterfaceListHash[pepInterface].Contains(chainInterface))
                    {
                        pepChainInterfaceListHash[pepInterface].Add(chainInterface);
                    }
                }
                else
                {
                    List<string> chainInterfaceList = new List<string> ();
                    chainInterfaceList.Add(chainInterface);
                    pepChainInterfaceListHash.Add(pepInterface, chainInterfaceList);
                }
            }
            Dictionary<string, string[]> pepChainInterfacesHash = new Dictionary<string, string[]>();
            foreach (string lsPepInterface in pepChainInterfaceListHash.Keys)
            {
                pepChainInterfacesHash.Add(lsPepInterface, pepChainInterfaceListHash[lsPepInterface].ToArray());
            }
            return pepChainInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideInterfaceId"></param>
        /// <returns></returns>
        private bool IsPeptideChainComprisonDone(string pdbId, int peptideInterfaceId)
        {
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp " + 
                " Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1} AND LocalPepRmsd is not null;", pdbId, peptideInterfaceId);
            DataTable pepChainHmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pepChainHmmSiteCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        #region pfams in rmsd calculations
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamIds()
        {
            string queryString = "Select Distinct PfamID From PfamPepInterfaceClusters;";
          //  string queryString = "Select Distinct PfamID From PafmChainInterfaceHmmSiteComp Where LocalPepRmsd < 0 and NumOfCommonHmmSites >= 3;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                pfamIds[count] = pfamId;
                count++;
            }
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamIds(string[] updateEntries)
        {
            List<string> updatePfamIdList = new List<string> ();
            string queryString = "";
            string pfamId = "";
            foreach (string pdbId in updateEntries)
            {
                //   string queryString = "Select Distinct PfamID From PfamPepInterfaceClusters;";
                queryString = string.Format("Select Distinct PfamID From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
                DataTable entryPepPfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

                foreach (DataRow pfamIdRow in entryPepPfamIdTable.Rows)
                {
                    pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                    if (! updatePfamIdList.Contains(pfamId))
                    {
                        updatePfamIdList.Add(pfamId);
                    }
                }
            }
            updatePfamIdList.Sort();
            return updatePfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string[]> GetPfamClusterRepPepInterfacesHash()
        {
            string queryString = "Select PfamId From PfamPepInterfaceClusters;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            Dictionary<string, string[]> pfamRepPepInterfaceHash = new Dictionary<string,string[]> ();
            
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                string[] clusterRepPepInterfaces = GetPfamRepPepInterfaces(pfamId);
                if (clusterRepPepInterfaces.Length > 0)
                {
                    pfamRepPepInterfaceHash.Add(pfamId, clusterRepPepInterfaces);
                }
            }
            return pfamRepPepInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamRepPepInterfaces(string pfamId)
        {
            List<string> clusterRepPepInterfaceList = new List<string> ();
            DataTable hmmSiteCompTable = ReadHmmSiteCompTable(pfamId);
            Dictionary<int, string[]> clusterPepInterfaceHash = GetPfamClusterRepPepInterfaces(pfamId);
            foreach (int clusterId in clusterPepInterfaceHash.Keys)
            {
                string[] clusterPepInterfaces = clusterPepInterfaceHash[clusterId];
                string repPepInterface = interfaceAlignPymol.GetDomainInterfaceWithMostCommonHmmSites(clusterPepInterfaces, hmmSiteCompTable);
                clusterRepPepInterfaceList.Add(repPepInterface);
            }
            return clusterRepPepInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetPfamClusterRepPepInterfaces(string pfamId)
        {
            string queryString = string.Format("Select ClusterId, PdbID, DomainInterfaceID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable clusterPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, List<string>> clusterPepInterfaceListHash = new Dictionary<int,List<string>> ();
            int clusterId = 0;
            string pepInterface = "";
            foreach (DataRow pepInterfaceRow in clusterPepInterfaceTable.Rows)
            {
                clusterId = Convert.ToInt32(pepInterfaceRow["ClusterID"].ToString ());
                pepInterface = pepInterfaceRow["PdbID"].ToString() + "_d" + pepInterfaceRow["DomainInterfaceID"].ToString();
                if (clusterPepInterfaceListHash.ContainsKey(clusterId))
                {
                    clusterPepInterfaceListHash[clusterId].Add(pepInterface);
                }
                else
                {
                    List<string> pepInterfaceList = new List<string> ();
                    pepInterfaceList.Add(pepInterface);
                    clusterPepInterfaceListHash.Add(clusterId, pepInterfaceList);
                }
            }
            Dictionary<int, string[]> clusterPepInterfaceHash = new Dictionary<int, string[]>();
            foreach (int lsClusterId in clusterPepInterfaceListHash.Keys)
            {
                clusterPepInterfaceHash.Add (lsClusterId, clusterPepInterfaceListHash[lsClusterId].ToArray ());
            }
            return clusterPepInterfaceHash;
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
        #endregion

        #region chain interface def table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceDomainDefTable(DataTable hmmSiteCompTable)
        {
            string pfamId = hmmSiteCompTable.Rows[0]["PfamID"].ToString().TrimEnd();
            Dictionary<string, int[]> entryInterfacesHash = GetEntryChainInterfacesHash(pfamId);
            DataTable chainInterfaceDefTable = GetChainInterfaceDomainDefTable(hmmSiteCompTable, entryInterfacesHash);
            return chainInterfaceDefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private Dictionary<string, int[]> GetEntryChainInterfacesHash(string pfamId )
        {
            Dictionary<string, List<int>> entryChainInterfaceListHash = new Dictionary<string,List<int>> ();
            string queryString = string.Format("Select Distinct PdbID2, DomainInterfaceID2 From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}';", pfamId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow chainInterfaceRow in chainInterfaceTable.Rows)
            {
                pdbId = chainInterfaceRow["PdbID2"].ToString();
                interfaceId = Convert.ToInt32(chainInterfaceRow["DomainInterfaceID2"].ToString ());
                if (entryChainInterfaceListHash.ContainsKey(pdbId))
                {
                    entryChainInterfaceListHash[pdbId].Add(interfaceId);
                }
                else
                {
                    List<int> interfaceIdList = new List<int> ();
                    interfaceIdList.Add(interfaceId);
                    entryChainInterfaceListHash.Add(pdbId, interfaceIdList);
                }
            }
            Dictionary<string, int[]> entryChainInterfacesHash = new Dictionary<string, int[]>();
            foreach (string lsEntry in entryChainInterfaceListHash.Keys)
            {
                entryChainInterfacesHash.Add (lsEntry,entryChainInterfaceListHash[lsEntry].ToArray ());
            }
            return entryChainInterfacesHash;
        }
       
        /// <summary>
        /// need to figure out the multiple pfam domains of a chain
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceDomainDefTable(DataTable hmmSiteCompTable, Dictionary<string, int[]> entryChainInterfacesHash)
        {
            string queryString = "Select First 1 RelSeqID, PdbID, InterfaceID, DomainInterfaceID, " +
                " DomainID1, AsymChain1, ChainDomainID1, DomainID2, AsymChain2, ChainDomainID2 From PfamDomainInterfaces;";
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceDefTable.Clear();
            string chainNo = "";
            string asymChain1 = "";
            string asymChain2 = "";
            long domainId = 0;
            string pfamId = hmmSiteCompTable.Rows[0]["PfamID"].ToString().TrimEnd();
            foreach (string pdbId in entryChainInterfacesHash.Keys )
            {
                DataTable crystInterfaceTable = GetCrystChainInterface (pdbId);
                DataTable chainDomainTable = GetEntryChainPfamTable (pdbId, pfamId);
                int[] interfaceIds = entryChainInterfacesHash[pdbId];
                foreach (int interfaceId in interfaceIds)
                {
                    DataRow hmmSiteCompRow = GetInterfaceHmmSiteCompRow(pdbId, interfaceId, hmmSiteCompTable);
                    if (hmmSiteCompRow == null)
                    {
                        ProtCidSettings.logWriter.WriteLine(pdbId + interfaceId.ToString() + " no hmm site comp rows");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    DataRow crystInterfaceRow = GetCrystChainInterface(pdbId, interfaceId, crystInterfaceTable);
                    if (crystInterfaceRow == null)
                    {
                        ProtCidSettings.logWriter.WriteLine(pdbId + interfaceId.ToString() + " no cryst interface defined");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }

                    asymChain1 = crystInterfaceRow["AsymChain1"].ToString().TrimEnd();
                    asymChain2 = crystInterfaceRow["AsymChain2"].ToString().TrimEnd();
                    domainId = Convert.ToInt64(hmmSiteCompRow["DomainID2"].ToString ());

                    DataRow interfaceDefRow = interfaceDefTable.NewRow();
                    interfaceDefRow["RelSeqID"] = hmmSiteCompRow["RelSeqID2"];
                    interfaceDefRow["PdbID"] = pdbId;
                    interfaceDefRow["InterfaceID"] = interfaceId;
                    interfaceDefRow["DomainInterfaceID"] = interfaceId;
                    chainNo = hmmSiteCompRow["ChainNo"].ToString();
                    if (chainNo == "A")
                    {
                        interfaceDefRow["DomainID1"] = domainId;
                        interfaceDefRow["AsymChain1"] = asymChain1;
                        DataRow[] chainDomainRows1 = GetChainDomainRows(asymChain1, domainId, chainDomainTable);
                        if (chainDomainRows1.Length > 0)
                        {
                            interfaceDefRow["ChainDomainID1"] = chainDomainRows1[0]["ChainDomainID"];
                        }
                        else
                        {
                            interfaceDefRow["ChainDomainId1"] = -1;
                        }

                        DataRow[] chainDomainRows2 = GetChainDomainRows(asymChain2, chainDomainTable);
                        interfaceDefRow["AsymChain2"] = asymChain2;
                        if (chainDomainRows2.Length > 0)
                        {
                            interfaceDefRow["DomainID2"] = chainDomainRows2[0]["DomainID"];
                            interfaceDefRow["ChainDomainID2"] = chainDomainRows2[0]["ChainDomainID"];
                        }
                        else
                        {
                            interfaceDefRow["DomainID2"] = -1;
                            interfaceDefRow["ChainDomainID2"] = -1;
                        }
                    }
                    else
                    {
                        asymChain1 = crystInterfaceRow["AsymChain2"].ToString().TrimEnd();
                        DataRow[] chainDomainRows1 = GetChainDomainRows(asymChain1, chainDomainTable);
                        interfaceDefRow["AsymChain1"] = asymChain1;
                        if (chainDomainRows1.Length > 0)
                        {
                            interfaceDefRow["DomainID1"] = chainDomainRows1[0]["DomainID"];
                            interfaceDefRow["ChainDomainID1"] = chainDomainRows1[0]["ChainDomainID"];
                        }
                        else
                        {
                            interfaceDefRow["DomainID1"] = -1;
                            interfaceDefRow["ChainDomainID1"] = -1;
                        }

                        asymChain2 = crystInterfaceRow["AsymChain1"].ToString().TrimEnd();
                        interfaceDefRow["AsymChain2"] = asymChain2;
                        interfaceDefRow["DomainID2"] = domainId;
                        DataRow[] chainDomainRows2 = GetChainDomainRows(asymChain2, domainId, chainDomainTable);
                        if (chainDomainRows2.Length > 0)
                        {
                            interfaceDefRow["DomainID2"] = hmmSiteCompRow["DomainID2"];
                            interfaceDefRow["ChainDomainID2"] = chainDomainRows2[0]["ChainDomainID"];
                        }
                        else
                        {
                            interfaceDefRow["ChainDomainID2"] = -1;
                        }
                    }
                    interfaceDefTable.Rows.Add(interfaceDefRow);
                }
            }
            return interfaceDefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetCrystChainInterface(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="crystInterfaceTable"></param>
        /// <returns></returns>
        private DataRow GetCrystChainInterface(string pdbId, int interfaceId, DataTable crystInterfaceTable)
        {
            DataRow[] interfaceDefRows = crystInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            if (interfaceDefRows.Length > 0)
            {
                return interfaceDefRows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetEntryChainPfamTable(string pdbId, string pfamId)
        {
            string queryString = string.Format("Select PdbPfamChain.DomainId As DomainID, ChainDomainID, AsymChain " + 
                " From PdbPfam, PdbPfamChain Where PdbPfam.Pfam_ID = '{0}' AND PdbPfam.PdbID = '{1}' AND PdbPfam.PdbID = PdbPfamChain.PdbID " + 
                " AND PdbPfam.DomainID = PdbPfamChain.DomainID;", pfamId, pdbId);
            DataTable chainDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return chainDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private DataRow[] GetChainDomainRows(string asymChain, DataTable chainDomainTable)
        {
            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format("AsymChain = '{0}'", asymChain));
            return chainDomainRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="domainId"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private DataRow[] GetChainDomainRows(string asymChain, long domainId, DataTable chainDomainTable)
        {
            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format ("AsymChain = '{0}' AND DomainID = '{1}'", asymChain, domainId));
            return chainDomainRows;
        }

        /// <summary>
        /// For chains with multiple same pfam domains, choose the one with the maximum number of common hmm sites.
        /// It should be true for most of cases
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="pfamHmmSiteCompTable"></param>
        /// <returns></returns>
        private DataRow GetInterfaceHmmSiteCompRow(string pdbId, int interfaceId, DataTable pfamHmmSiteCompTable)
        {
            DataRow[] hmmSiteCompRows = pfamHmmSiteCompTable.Select(string.Format ("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, interfaceId));
            int numOfCommonHmmSites = 0;
            int maxNumOfCommonHmmSites = 0;
            DataRow hmmSiteRowMax = null;
            foreach (DataRow hmmSiteCompRow in hmmSiteCompRows)
            {
                numOfCommonHmmSites = Convert.ToInt32(hmmSiteCompRow["NumOfCommonHmmSites"].ToString ());
                if (maxNumOfCommonHmmSites < numOfCommonHmmSites)
                {
                    maxNumOfCommonHmmSites = numOfCommonHmmSites;
                    hmmSiteRowMax = hmmSiteCompRow;
                }
            }
            return hmmSiteRowMax;
        }
        #endregion

        #region import rmsd into table
        /// <summary>
        /// 
        /// </summary>
        public void ImportPepChainRmsdIntoDb()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Importing peptide-chain RMSD into database.");

            hmmSiteCompTableName = "PfamChainInterfaceHmmSiteComp";
            string rmsdFile = @"E:\Qifang\DbProjectData\pfam\PfamPeptide\pepChainComp0\PfamPeptideChainInterfaceRmsd.txt";
            StreamReader rmsdReader = new StreamReader(rmsdFile);
            string line = "";
            double[] rmsds = new double[4];
            string[] localAlignInfo = new string[8];
            string pfamId = "";
            while ((line = rmsdReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (IsDataExist(fields[2], fields[3]))
                {
                    continue;
                }
                pfamId = fields[0];
                rmsds[0] = Convert.ToDouble(fields[7]);
                rmsds[1] = Convert.ToDouble(fields[8]);
                rmsds[2] = Convert.ToDouble(fields[9]);
                rmsds[3] = Convert.ToDouble(fields[12]);
                if (fields[15] == "")
                {
                    localAlignInfo[0] = "-";
                }
                else
                {
                    localAlignInfo[0] = fields[15];  // pepAlignment
                }
                if (fields[16] == "")
                {
                    localAlignInfo[1] = "-";
                }
                else
                {
                    localAlignInfo[1] = fields[16];  // chain alignment
                }
                if (fields[17] == "")
                {
                    localAlignInfo[2] = "-1";
                }
                else
                {
                    localAlignInfo[2] = fields[17]; // pep start
                }
                if (fields[18] == "")
                {
                    localAlignInfo[3] = "-1";
                }
                else
                {
                    localAlignInfo[3] = fields[18]; // pep end
                }
                if (fields[19] == "")
                {
                    localAlignInfo[4] = "-1";
                }
                else
                {
                    localAlignInfo[4] = fields[19]; // chain start
                }
                if (fields[20] == "")
                {
                    localAlignInfo[5] = "-1";
                }
                else
                {
                    localAlignInfo[5] = fields[20]; // chain end
                }
                if (fields[21] == "" || fields[21] == "Infinity")
                {
                    localAlignInfo[6] = "-1";
                }
                else
                {
                    localAlignInfo[6] = fields[21]; // score
                }
                if (fields[22] == "" || fields[22] == "Infinity")
                {
                    localAlignInfo[7] = "-1";
                }
                else
                {
                    localAlignInfo[7] = fields[22]; // rmsd
                }
                InsertInterfaceRmsdIntoDb(fields[2], fields[3], fields[5], fields[6], rmsds, localAlignInfo);
            }
            rmsdReader.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterface"></param>
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        private bool IsDataExist(string pepInterface, string chainInterface)
        {
            string pepPdbId = pepInterface.Substring(0, 4);
            int pepInterfaceId = Convert.ToInt32 (pepInterface.Substring(6, pepInterface.Length - 6));
            string chainPdbId = chainInterface.Substring(0, 4);
            int chainInterfaceId = Convert.ToInt32(chainInterface.Substring (5, chainInterface.Length - 5));

            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp " + 
                " Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1} AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3} AND PepRmsd > 0;",
                pepPdbId, pepInterfaceId, chainPdbId, chainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterface1"></param>
        /// <param name="pepInterface2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pepInterface1, string pepInterface2, string domainId2Str, string chainNo, double[] rmsds, string[] localAlignInfo)
        {
            string[] pepInterfaceInfo1 = GetDomainInterfaceInfo(pepInterface1);
            string pdbId1 = pepInterfaceInfo1[0];
            int domainInterfaceId1 = Convert.ToInt32(pepInterfaceInfo1[1]);
            string[] pepInterfaceInfo2 = GetDomainInterfaceInfo(pepInterface2);
            string pdbId2 = pepInterfaceInfo2[0];
            int domainInterfaceId2 = Convert.ToInt32(pepInterfaceInfo2[1]);
            long domainId2 = Convert.ToInt64(domainId2Str);

            InsertInterfaceRmsdIntoDb(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, domainId2, chainNo, rmsds, localAlignInfo);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, 
            long domainId2, string chainNo, double[] rmsds, string[] localAlignInfo)
        {
            string updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, PepRmsd = {3}, InteractPepRmsd = {4}, " + 
                " PepAlignment = '{5}', ChainAlignment = '{6}', " +
               " PepStart = {7}, PepEnd = {8}, ChainStart = {9}, ChainEnd = {10}, Score = {11}, LocalPepRmsd = {12} " +
               " Where PdbID1 = '{13}' AND DomainInterfaceId1 = {14} AND PdbID2 = '{15}' AND DomainInterfaceID2 = {16} AND DomainID2 = {17} AND ChainNO = '{18}';",
               hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], 
               localAlignInfo[0], localAlignInfo[1], localAlignInfo[2], localAlignInfo[3], localAlignInfo[4], localAlignInfo[5],
               localAlignInfo[6], localAlignInfo[7],  
               pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, domainId2, chainNo);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private string GetChainNo(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select NumOfCommonHmmSites, ChainNo From {0} " + 
                 " Where PdbID1 = '{1}' AND DomainInterfaceId1 = {2} AND PdbID2 = '{3}' AND DomainInterfaceID2 = {4};",
                 hmmSiteCompTableName, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainNo = "";
            if (hmmSiteCompTable.Rows.Count > 1)
            {
                int maxNumOfHmmSites = 0;
                int numOfHmmSites = 0;
                foreach (DataRow hmmSiteRow in hmmSiteCompTable.Rows)
                {
                    numOfHmmSites = Convert.ToInt32(hmmSiteRow["NumOfCommonHmmSites"].ToString());
                    if (maxNumOfHmmSites < numOfHmmSites)
                    {
                        maxNumOfHmmSites = numOfHmmSites;
                        chainNo = hmmSiteRow["ChainNo"].ToString();
                    }
                }
            }
            else if (hmmSiteCompTable.Rows.Count == 1)
            {
                chainNo = hmmSiteCompTable.Rows[0]["ChainNO"].ToString();
            }
            return chainNo;
        }
        #endregion

        #region missing interface pairs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void CalculateMissingPeptideChainInterfaceRmsd()
        {
            rmsdDataFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\pepChainComp");
            if (! Directory.Exists (rmsdDataFileDir))
            {
                Directory.CreateDirectory(rmsdDataFileDir); ;
            }
            hmmSiteCompTableName = "PfamChainInterfaceHmmSiteComp";
            string pfamId = "Pkinase";
            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");
            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideChainInterfaceRmsd_missing.txt"), true);

            /*    if (File.Exists(pfamPymolScriptFile))
                {
                    return;
                }*/
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetMissingPfamHmmSiteCompTable (pfamId, leastNumOfCommonHmmSites);
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);

            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string, int[]>();

            Dictionary<string, string[]> pepChainInterfacesHash = GetPepCompChainInterfacesHash(hmmSiteCompTable);

            // get the interface domain def table for both peptide interfaces and the chain interfaces
            List<string> pepInterfaceList = new List<string> (pepChainInterfacesHash.Keys);
            string[] pepInterfaces = new string[pepInterfaceList.Count];
            pepInterfaceList.CopyTo(pepInterfaces);
            DataTable interfaceDomainDefTable = GetDomainInterfaceDefTable(pepInterfaces);
            // try to fit the table format into domain interface format
            DataTable chainInterfaceDomainDefTable = GetChainInterfaceDomainDefTable(hmmSiteCompTable);
            ParseHelper.AddNewTableToExistTable(chainInterfaceDomainDefTable, ref interfaceDomainDefTable);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(pepChainInterfacesHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            foreach (string pepInterface in pepChainInterfacesHash.Keys)
            {
                string[] compChainInterfaces = (string[])pepChainInterfacesHash[pepInterface];
                string[][] divCompInterfaces = DivideCompInterfaces(compChainInterfaces);
                int subGroupCount = 1;
                try
                {
                    foreach (string[] compInterfaces in divCompInterfaces)
                    {
                        CalculateDomainInterfacePeptideRmsd(pfamId, pepInterface, compInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                     interfaceDomainDefTable, domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);

                        RenameCoordAndPymolFiles(pfamId, pepInterface, subGroupCount);
                        subGroupCount++;
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + pepInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                MoveCoordAndPymolFiles();
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir, "*.cryst*");
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetMissingPfamHmmSiteCompTable(string pfamId, int numOfCommonHmmSites)
        {
            //   string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND PepRmsd > -1;", hmmSiteCompTableName, pfamId);
            string queryString = string.Format("Select * From {0} Where pfamID = '{1}' AND NumOfCommonHmmSites >= {2} AND LocalPepRmsd = -1;",
                hmmSiteCompTableName, pfamId, numOfCommonHmmSites);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void CalculateClusterPeptideChainInterfaceRmsd()
        {
            rmsdDataFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\pepChainComp");
            if (!Directory.Exists(rmsdDataFileDir))
            {
                Directory.CreateDirectory(rmsdDataFileDir); ;
            }
            hmmSiteCompTableName = "PfamChainInterfaceHmmSiteComp";
            leastNumOfCommonHmmSites = 3;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptide and chain of pfam interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideChainInterfaceRmsd_debug.txt"), true);

            string pfamId = "PDZ";
            string pepInterface = "2k20_d1";
            string chainInterface = "1qau_2";

            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");

            /*    if (File.Exists(pfamPymolScriptFile))
                {
                    return;
                }*/
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId, pepInterface, chainInterface, leastNumOfCommonHmmSites);
            if (hmmSiteCompTable.Rows.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + ": no data with the number of common hmm sites > " + leastNumOfCommonHmmSites.ToString());
                ProtCidSettings.logWriter.WriteLine(pfamId + ": no data with the number of common hmm sites > " + leastNumOfCommonHmmSites.ToString());
                ProtCidSettings.logWriter.Flush();
                return;
            }
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);

            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string, int[]>();

            Dictionary<string, string[]> pepChainInterfacesHash = GetPepCompChainInterfacesHash(hmmSiteCompTable);

            // get the interface domain def table for both peptide interfaces and the chain interfaces
            List<string> pepInterfaceList = new List<string> (pepChainInterfacesHash.Keys);
            string[] pepInterfaces = new string[pepInterfaceList.Count];
            pepInterfaceList.CopyTo(pepInterfaces);
            DataTable interfaceDomainDefTable = GetDomainInterfaceDefTable(pepInterfaces);
            // try to fit the table format into domain interface format
            DataTable chainInterfaceDomainDefTable = GetChainInterfaceDomainDefTable(hmmSiteCompTable);
            ParseHelper.AddNewTableToExistTable(chainInterfaceDomainDefTable, ref interfaceDomainDefTable);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(pepChainInterfacesHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            string[] compChainInterfaces = (string[])pepChainInterfacesHash[pepInterface];
            try
            {

                CalculateDomainInterfacePeptideRmsd(pfamId, pepInterface, compChainInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                 interfaceDomainDefTable, domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + pepInterface + " " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + " " + pepInterface + " " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetPfamHmmSiteCompTable(string pfamId, string pepInterface, string chainInterface, int numOfCommonHmmSites)
        {
            string pepPdbId = pepInterface.Substring(0, 4);
            int pepDomainInterfaceId = Convert.ToInt32 (pepInterface.Substring(6, pepInterface.Length - 6));
            string chainPdbId = chainInterface.Substring(0, 4);
            int chainInterfaceId = Convert.ToInt32(chainInterface.Substring (5, chainInterface.Length - 5));
            string queryString = string.Format("Select * From {0} Where pfamID = '{1}' AND NumOfCommonHmmSites > {2} AND " + 
                " PdbID1 = '{3}' AND DomainInterfaceID1 = {4} AND PdbID2 = '{5}' AND DomainInterfaceID2 = {6};",
                hmmSiteCompTableName, pfamId, numOfCommonHmmSites, pepPdbId, pepDomainInterfaceId, chainPdbId, chainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        #endregion
    }
}
