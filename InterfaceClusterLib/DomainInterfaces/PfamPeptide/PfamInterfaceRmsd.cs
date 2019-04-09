using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    /// <summary>
    /// Calculate the RMSDs between domain and peptide from domain-domain interface and domain-peptide interfaces
    /// </summary>
    public class PfamInterfaceRmsd : PeptideInterfaceRmsd
    {
        #region member variables
        private double redundantQscoreCutoff = 0.85;
        private double numHmmSitesCutoff = 3;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void CalculateInterfaceDomainPeptideRmsd()
        {
            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideDomainInterfaceRmsd.txt"), true); 

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Calculate peptide and domain RMSD";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate peptide and domain RMSD");
            ProtCidSettings.progressInfo.currentOperationLabel = "Peptide and Domain RMSD";

            string[] pfamIds = GetDomainPepCompPfamIds();
       //     string[] pfamIds = { "Pkinase" };
           
    //        ProtCidSettings.progressInfo.totalOperationNum = pfamIds.Length;
    //        ProtCidSettings.progressInfo.totalStepNum = pfamIds.Length;

            Dictionary<string, string[]> pfamDomainInterfacesCompHash = GetDomainInterfacesToBeCompared(pfamIds);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PfamIDs: " + pfamIds.Length.ToString ());
            int pfamCount = 1;
            foreach (string pfamId in pfamIds)
            {
      //          ProtCidSettings.progressInfo.currentOperationNum++;
       //         ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.progStrQueue.Enqueue (pfamId + " " + pfamCount.ToString ());
                pfamCount++;

                try
                {
                    if (pfamDomainInterfacesCompHash.ContainsKey(pfamId))
                    {
                        string[] domainInterfacesComp = (string[])pfamDomainInterfacesCompHash[pfamId];
                        // remove those don't share common hmm sites. 
                        string[] comHmmSiteDomainInterfaces = GetDomainInterfacesWithCommonHmmSites(pfamId, domainInterfacesComp);
                        CalculateDomainPeptideRmsd(pfamId, comHmmSiteDomainInterfaces, rmsdWriter);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Calculating RMSD between peptide and domain rmsd error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Calculating RMSD between peptide and domain rmsd error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            rmsdWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetDomainPepCompPfamIds()
        {
            string pfamFile = Path.Combine (rmsdDataFileDir, "DomainPepCompPfamIds.txt");
            List<string> pfamIdList = new List<string> ();
            if (File.Exists(pfamFile))
            {
                StreamReader dataReader = new StreamReader(pfamFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    pfamIdList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter pfamIdWriter = new StreamWriter(pfamFile);
                string queryString = string.Format("Select Distinct PfamID From {0};", hmmSiteCompTableName);
                DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                string pfamId = "";
                foreach (DataRow pfamIdRow in pfamIdTable.Rows)
                {
                    pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                    pfamIdList.Add(pfamId);
                    pfamIdWriter.WriteLine(pfamId);
                }
                pfamIdWriter.Close();
            }

            return pfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CalculateDomainPeptideRmsd(string pfamId, string[] domainInterfacesToBeCompared, StreamWriter rmsdWriter)
        {
            string pymolScriptFile = Path.Combine(rmsdDataFileDir, "PepDomainRmsd\\" + pfamId + "_pairFit.pml");
            if (File.Exists(pymolScriptFile))
            {
                return;
            }
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pymolScriptFile);

      //      string[] domainInterfacesToBeCompared = GetDomainInterfacesToBeCompared(pfamId);
      //      string[] peptideInterfaces = GetPfamPepInterfaces(pfamId);
            string[] pfamPeptideInterfaces = null;
            Dictionary<string, string[]> domainPepInterfaceCompHash = GetDomainPeptideInterfaceCompHash(domainInterfacesToBeCompared, out pfamPeptideInterfaces);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = GetTotalInterfacePairs (domainPepInterfaceCompHash );
            ProtCidSettings.progressInfo.totalOperationNum = ProtCidSettings.progressInfo.totalStepNum;

            long[] domainIds = null;
            DataTable hmmSiteCompTable = GetHmmSiteCompTable(pfamId, pfamPeptideInterfaces, domainInterfacesToBeCompared, out domainIds);

            DataTable pfamChainDomainTable = GetPfamChainDomainTable(pfamId, domainIds);

            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();

            foreach (string domainInterface in domainPepInterfaceCompHash.Keys)
            {
      /*          ProtCidSettings.progressInfo.currentFileName = domainInterface;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;
                */
                string[] peptideInterfaces = (string[])domainPepInterfaceCompHash[domainInterface];
                CalculateDomainInterfacePeptideRmsd(pfamId, domainInterface, peptideInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                   domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
                domainInterfaceChainCoordSeqIdsHash.Remove(domainInterface);  // remove the coordinates for the domain interface
            }

            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir);
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
        /// <param name="peptideInterfaces"></param>
        /// <param name="domainInterfaces"></param>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private DataTable GetHmmSiteCompTable(string pfamId, string[] peptideInterfaces, string[] domainInterfaces, out long[] domainIds)
        {
            DataTable hmmSiteCompTable = GetDomainPeptideInterfaceCompTable(pfamId);
            DataTable peptideDomainHmmSiteCompTable = hmmSiteCompTable.Clone();
            string domainInterface = "";
            long domainId = 0;
            List<long> domainIdList = new List<long> ();
            foreach (DataRow hmmSiteCompRow in hmmSiteCompTable.Rows)
            {
                
                domainInterface = hmmSiteCompRow["PdbID2"].ToString() + "_d" + hmmSiteCompRow["DomainInterfaceID2"].ToString();
                if (domainInterfaces.Contains(domainInterface))
                {
                    domainId = Convert.ToInt64(hmmSiteCompRow["DomainID1"].ToString ());
                    if (!domainIdList.Contains(domainId))
                    {
                        domainIdList.Add(domainId);
                    }
                    domainId = Convert.ToInt64(hmmSiteCompRow["DomainID2"].ToString ());
                    if (!domainIdList.Contains(domainId))
                    {
                        domainIdList.Add(domainId);
                    }
                    DataRow dataRow = peptideDomainHmmSiteCompTable.NewRow();
                    dataRow.ItemArray = hmmSiteCompRow.ItemArray;
                    peptideDomainHmmSiteCompTable.Rows.Add(dataRow);
                }
            }
            domainIds = domainIdList.ToArray (); 
            return peptideDomainHmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private DataTable GetPfamChainDomainTable(string pfamId, long[] domainIds)
        {
            DataTable pfamChainDomainTable = interfaceAlignPymol.GetPfamChainDomainTable(pfamId, domainIds);
            if (pfamMultiChainDomainHash.ContainsKey(pfamId))
            {
                interfaceAlignPymol.UpdatePfamMultiChainDomains(pfamChainDomainTable, (long[])pfamMultiChainDomainHash[pfamId]);
            }
            return pfamChainDomainTable;
        }

        #region domain interfaces to be compared 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetDomainPeptideInterfaceCompHash(string[] domainInterfaces, out string[] pfamPeptideInterfaces)
        {
            Dictionary<string, string[]> domainPepInterfaceCompHash = new Dictionary<string, string[]>();
            List<string> pfamPeptideInterfaceList = new List<string> ();
            foreach (string domainInterface in domainInterfaces)
            {
                string[] compPeptideInterfaces = GetPeptideInterfacesWithCommonHmmSites(domainInterface);
                if (compPeptideInterfaces.Length > 0)
                {
                    domainPepInterfaceCompHash.Add(domainInterface, compPeptideInterfaces);
                    pfamPeptideInterfaceList.AddRange(compPeptideInterfaces);
                }
            }
            pfamPeptideInterfaces = pfamPeptideInterfaceList.ToArray();
            return domainPepInterfaceCompHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="peptideInterfaces"></param>
        /// <returns></returns>
        private string[] GetPeptideInterfacesWithCommonHmmSites(string domainInterface)
        {
            string[] domainInterfaceInfo = GetDomainInterfaceInfo(domainInterface);
            string pdbId = domainInterfaceInfo[0];
            int domainInterfaceId = Convert.ToInt32 (domainInterfaceInfo[1]);
            List<string> comHmmSitePeptideInterfaceList = new List<string> ();
            string queryString = string.Format("Select Distinct PdbID1, DomainInterfaceID1 From {0} " + 
                " Where PdbID2 = '{1}' AND DomainInterfaceID2 = {2} AND NumOfCommonHmmSites >= {3};", hmmSiteCompTableName, pdbId, domainInterfaceId, numHmmSitesCutoff);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string peptideInterface = "";
            foreach (DataRow peptideInterfaceRow in peptideInterfaceTable.Rows)
            {
                peptideInterface = peptideInterfaceRow["PdbID1"].ToString () + "_d" + peptideInterfaceRow["DomainInterfaceID1"].ToString ();
                comHmmSitePeptideInterfaceList.Add(peptideInterface);
            }
            return comHmmSitePeptideInterfaceList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private string[] GetDomainInterfacesWithCommonHmmSites(string pfamId, string[] domainInterfaces)
        {
            string queryString = string.Format("Select Distinct PdbID2, DomainInterfaceID2 From {0} " + 
                " Where PfamID = '{1}' AND NumOfCommonHmmSites >= {2};", hmmSiteCompTableName, pfamId, numHmmSitesCutoff);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> comHmmSiteDomainInterfaceList = new List<string> ();
            string domainInterface = "";
            foreach (DataRow domainInterfaceRow in hmmSiteCompTable.Rows)
            {
                domainInterface = domainInterfaceRow["PdbID2"].ToString() + "_d" + domainInterfaceRow["DomainInterfaceID2"].ToString();
                if (domainInterfaces.Contains(domainInterface))
                {
                    comHmmSiteDomainInterfaceList.Add(domainInterface);
                }
            }
            string[] comHmmSitesDomainInterfaces = new string[comHmmSiteDomainInterfaceList.Count];
            comHmmSiteDomainInterfaceList.CopyTo(comHmmSitesDomainInterfaces);
            return comHmmSitesDomainInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetDomainInterfacesToBeCompared(string[] pfamIds)
        {
            string pepCompDomainInterfaceFile = Path.Combine (rmsdDataFileDir, "pepCompDomainInterfaces.txt");           
            Dictionary<string, string[]> pfamDomainInterfaceCompHash = new Dictionary<string, string[]>();
            if (File.Exists(pepCompDomainInterfaceFile))
            {
                Dictionary<string, List<string>> pfamDomainInterfaceCompListHash = new Dictionary<string, List<string>>();
                string pfamId = "";
                StreamReader dataReader = new StreamReader(pepCompDomainInterfaceFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line.IndexOf("#") > -1)
                    {
                        pfamId = line.Substring(1, line.LastIndexOf ("_") - 1);
                        line = dataReader.ReadLine();
                        string[] repDomainInterfaces = line.Split(',');
                        line = dataReader.ReadLine();
                        string[] domainInterfacesNotClusted = line.Split(',');
                        if (pfamDomainInterfaceCompListHash.ContainsKey(pfamId))
                        {
                            pfamDomainInterfaceCompListHash[pfamId].AddRange(repDomainInterfaces);
                            pfamDomainInterfaceCompListHash[pfamId].AddRange(domainInterfacesNotClusted);
                        }
                        else
                        {
                            List<string> domainInterfaceList = new List<string> ();
                            domainInterfaceList.AddRange(repDomainInterfaces);
                            domainInterfaceList.AddRange(domainInterfacesNotClusted);
                            pfamDomainInterfaceCompListHash.Add(pfamId, domainInterfaceList);
                        }
                    }
                }
                dataReader.Close();
                
                foreach (string lsPfamId in pfamDomainInterfaceCompListHash.Keys)
                {
                    pfamDomainInterfaceCompHash.Add(lsPfamId, pfamDomainInterfaceCompListHash[lsPfamId].ToArray());
                }
            }
            else
            {
                StreamWriter domainInterfacesWriter = new StreamWriter(pepCompDomainInterfaceFile);
                foreach (string pfamId in pfamIds)
                {
                    string[] domainInterfacesToBeCompared = WriteDomainInterfacesToBeCompared(pfamId, domainInterfacesWriter);
                    pfamDomainInterfaceCompHash.Add(pfamId, domainInterfacesToBeCompared);
                }
                domainInterfacesWriter.Close();
            }
            return pfamDomainInterfaceCompHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] WriteDomainInterfacesToBeCompared(string pfamId, StreamWriter domainInterfaceWriter)
        {
            int[] relSeqIds = GetDomainRelSeqIDs(pfamId);
            List<string> repDomainInterfaceList = new List<string> ();
            List<string> domainInterfacesListNotClustered = new List<string> ();
            foreach (int relSeqId in relSeqIds)
            {
                domainInterfaceWriter.WriteLine("#" + pfamId + "_" + relSeqId.ToString());
                string[] relRepDomainInterfaces = GetRepresentativeDomainInterfaces(relSeqId);
                string[] domainInterfacesNotClustered = GetDomainInterfacesNotClustered(relSeqId);

                repDomainInterfaceList.AddRange(relRepDomainInterfaces);
                domainInterfacesListNotClustered.AddRange(domainInterfacesNotClustered);

                domainInterfaceWriter.WriteLine(ParseHelper.FormatStringFieldsToString(relRepDomainInterfaces));
                domainInterfaceWriter.WriteLine(ParseHelper.FormatStringFieldsToString(domainInterfacesNotClustered));
                domainInterfaceWriter.Flush();
            }
            List<string> domainInterfacesList = new List<string> ();
            domainInterfacesList.AddRange(repDomainInterfaceList);
            domainInterfacesList.AddRange(domainInterfacesListNotClustered);
            return domainInterfacesList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetDomainInterfacesToBeCompared (string pfamId)
        {
     //       int[] relSeqIds = GetDomainRelSeqIDs(pfamId);
            int[] domainRelSeqIds = GetPepDomainCompRelSeqIDs (pfamId);
            List<string> repDomainInterfaceList = new List<string> ();
            List<string> domainInterfacesListNotClustered = new List<string> ();
            foreach (int relSeqId in domainRelSeqIds)
            {
                string[] relRepDomainInterfaces = GetRepresentativeDomainInterfaces(relSeqId);
                string[] domainInterfacesNotClustered = GetDomainInterfacesNotClustered(relSeqId);
                repDomainInterfaceList.AddRange(relRepDomainInterfaces);
                domainInterfacesListNotClustered.AddRange(domainInterfacesNotClustered);
            }
            List<string> domainInterfacesList = new List<string> ();
            domainInterfacesList.AddRange(repDomainInterfaceList);
            domainInterfacesList.AddRange(domainInterfacesListNotClustered);
            return domainInterfacesList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetDomainRelSeqIDs(string pfamId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainFamilyRelation WHere FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
            DataTable domainRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIds = new int[domainRelSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in domainRelSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPepDomainCompRelSeqIDs (string pfamId)
        {
            string queryString = string.Format ("Select Distinct RelSeqID2 From {0} Where PfamID = '{1}' AND PepComp = '0';", hmmSiteCompTableName, pfamId);
            DataTable domainRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] domainRelSeqIds = new int[domainRelSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in domainRelSeqIdTable.Rows )
            {
                domainRelSeqIds[count] = Convert.ToInt32 (relSeqIdRow["RelSeqID2"].ToString ());

                count ++;
            }
            return domainRelSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetDomainPeptideInterfaceCompTable(string pfamId)
        {
            string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND PepComp = '0';", hmmSiteCompTableName, pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamPepInterfaces(string pfamId)
        {
            int[] relSeqIds = GetDomainRelSeqIDs(pfamId);

            List<string> peptideInterfaceList = new List<string> ();
            string queryString = "";
            foreach (int relSeqId in relSeqIds)
            {
                queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
                DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow peptideInterfaceRow in peptideInterfaceTable.Rows)
                {
                    peptideInterfaceList.Add(peptideInterfaceRow["PdbID"].ToString() + "_d" + peptideInterfaceRow["DomainInterfaceID"].ToString());
                }
            }
            return peptideInterfaceList.ToArray ();
        }
        #endregion

        #region representative interfaces for a relation
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetRepresentativeDomainInterfaces(int relSeqId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamDomainInterfaceCluster Where RelSeqID = {0}", relSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;

            string[] clusterDomainInterfaces = null;
            string clusterRepDomainInterface = "";
            List<string> repDomainInterfaceList = new List<string>();
            List<string> clusterDomainInterfaceList = new List<string>();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                clusterRepDomainInterface = GetRepresentativeDomainInterface(relSeqId, clusterId, out clusterDomainInterfaces);
                repDomainInterfaceList.Add(clusterRepDomainInterface);
                clusterDomainInterfaceList.AddRange(clusterDomainInterfaces);
            }

            return repDomainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetDomainInterfacesNotClustered (int relSeqId)
        {
            string[] relDomainInterfaces = GetRelationDomainInterfaces(relSeqId);
            string[] clusterDomainInterfaces = GetDomainInterfacesInClusters(relSeqId);
      //      ArrayList domainInterfaceListNotClustered = new ArrayList();
            Dictionary<string, List<int>> entryDomainInterfaceNotClusteredHash = new Dictionary<string,List<int>> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string domainInterface in relDomainInterfaces)
            {
                if (Array.IndexOf(clusterDomainInterfaces, domainInterface) > -1)
                {
                    continue;
                }
                string[] domainInterfaceInfo = GetDomainInterfaceInfo(domainInterface);
                pdbId = domainInterfaceInfo[0];
                domainInterfaceId = Convert.ToInt32(domainInterfaceInfo[1]);
                if (entryDomainInterfaceNotClusteredHash.ContainsKey(pdbId))
                {
                    entryDomainInterfaceNotClusteredHash[pdbId].Add(domainInterfaceId);
                }
                else
                {
                    List<int> domainInterfaceIdList = new List<int> ();
                    domainInterfaceIdList.Add(domainInterfaceId);
                    entryDomainInterfaceNotClusteredHash.Add(pdbId, domainInterfaceIdList);
                }
              //  domainInterfaceListNotClustered.Add(domainInterface);
            }
            // remove almost same domain interfaces by checking Qscores in PfamEntryDomainInterfaceComp
            List<string> domainInterfaceList = new List<string> ();
            string nonReduntDomainInterface = "";
            foreach (string entry in entryDomainInterfaceNotClusteredHash.Keys)
            {
                int[] nonReduntDomainInterfaceIds = GetNonRedundantDomainInterfaceIds(entry, entryDomainInterfaceNotClusteredHash[entry].ToArray ());
                foreach (int nonReduntDomainInterfaceId in nonReduntDomainInterfaceIds)
                {
                    nonReduntDomainInterface = entry + "_d" + nonReduntDomainInterfaceId.ToString();
                    domainInterfaceList.Add(nonReduntDomainInterface);
                }
            }

            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetNonRedundantDomainInterfaceIds(string pdbId, int[] domainInterfaceIds)
        {
            // retrieve the redundant domain interfaces for this entry
            string queryString = string.Format("Select * From PfamEntryDomainInterfaceComp " +
                " Where PdbID = '{0}' AND Qscore >= {1} Order By DomainInterfaceID1, DomainInterfaceID2;", pdbId, redundantQscoreCutoff);
            DataTable reduntDomainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> nonReduntDomainInterfaceIdList = new List<int> (domainInterfaceIds);
            int domainInterfaceId1 = 0;
            int domainInterfaceId2 = 0;
            foreach (DataRow domainInterfaceCompRow in reduntDomainInterfaceCompTable.Rows)
            {
                domainInterfaceId1 = Convert.ToInt32(domainInterfaceCompRow["DomainInterfaceID1"].ToString ());
                domainInterfaceId2 = Convert.ToInt32(domainInterfaceCompRow["DomainInterfaceID2"].ToString ());
                // remove the larger number
                if (domainInterfaceId2 < domainInterfaceId1)
                {
                    nonReduntDomainInterfaceIdList.Remove(domainInterfaceId1);
                }
                else 
                {
                    nonReduntDomainInterfaceIdList.Remove(domainInterfaceId2);
                }
            }

            return nonReduntDomainInterfaceIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetDomainInterfacesInClusters(int relSeqId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainClusterInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable clusterDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterDomainInterfaces = new string[clusterDomainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterDomainInterfaceRow in clusterDomainInterfaceTable.Rows)
            {
                clusterDomainInterfaces[count] = clusterDomainInterfaceRow["PdbID"].ToString() + "_d" + clusterDomainInterfaceRow["DomainInterfaceID"].ToString();
                count++;
            }
            return clusterDomainInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationDomainInterfaces(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbId, DomainInterfaceID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relationDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] relDomainInterfaces = new string[relationDomainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceRow in relationDomainInterfaceTable.Rows)
            {
                relDomainInterfaces[count] = domainInterfaceRow["PdbId"].ToString() + "_d" + domainInterfaceRow["DomainInterfaceID"].ToString();
                count++;
            }
            return relDomainInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepresentativeDomainInterface(int relSeqId, int clusterId, out string[] clusterDomainInterfaces)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceCluster Where RelSeqID = {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string> ();
            List<string> interfaceList = new List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_d" + interfaceId.ToString());
            }
            clusterDomainInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(clusterDomainInterfaces);
            double bestQscore = 0;
            double qscore = 0;
            string bestDomainInterface = "";
            foreach (string domainInterface in clusterDomainInterfaces)
            {
                qscore = GetDomainInterfaceQScoreSum(domainInterface, clusterDomainInterfaces);
                if (bestQscore < qscore)
                {
                    bestQscore = qscore;
                    bestDomainInterface = domainInterface;
                }
            }
            return bestDomainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        private double GetDomainInterfaceQScoreSum(string domainInterface, string[] clusterInterfaces)
        {
            string[] domainInterfaceInfo = GetDomainInterfaceInfo(domainInterface);
            string pdbId = domainInterfaceInfo[0];
            int domainInterfaceId = Convert.ToInt32(domainInterfaceInfo[1]);
            DataTable domainInterfaceCompTable = GetDomainInterfaceQscoreTable(pdbId, domainInterfaceId);

            string clusterPdbId = "";
            int clusterDomainInterfaceId = 0;
            double qscore = -1;
            double qscoreSum = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] clusterInterfaceInfo = GetDomainInterfaceInfo(clusterInterface);
                clusterPdbId = clusterInterfaceInfo[0];
                if (clusterPdbId == pdbId)
                {
                    continue;  // only pick up one domain interface for this entry
                }
                clusterDomainInterfaceId = Convert.ToInt32(clusterInterfaceInfo[1]);
                qscore = GetDomainInterfaceCompQscore (pdbId, domainInterfaceId, clusterPdbId, clusterDomainInterfaceId, domainInterfaceCompTable);
                if (qscore > -1)
                {
                    qscoreSum += qscore;
                }
            }
            return qscoreSum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceCompQscore(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, DataTable interfaceCompTable)
        {
            double qscore = -1;
            DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
            if (interfaceCompRows.Length == 0)
            {
                interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1));
            }
            if (interfaceCompRows.Length > 0)
            {
                qscore = Convert.ToDouble(interfaceCompRows[0]["Qscore"]);
            }
            return qscore;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceQscoreTable(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp " +
                " Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1}) OR (PdbID2='{0}' AND DomainInterfaceID2 = {1});", pdbId, domainInterfaceId);
            DataTable interfaceQscoreTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceQscoreTable;
        }
        #endregion
    }
}
