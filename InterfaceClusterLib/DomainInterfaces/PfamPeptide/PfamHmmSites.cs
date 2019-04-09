using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Contacts;
using InterfaceClusterLib.InterfaceProcess;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamHmmSites
    {
        #region member variables
        public StreamWriter domainHmmInteractingSitesWriter = null;
        public StreamWriter noCommonHmmSitesWriter = null;
        public DbQuery dbQuery = new DbQuery();
        public DbInsert dbInsert = new DbInsert();
        public DbUpdate dbUpdate = new DbUpdate();
        public static string interfaceHmmSiteTableName = "PfamInterfaceHmmSiteComp";
        public DataTable pfamInterfaceHmmSiteTable = null;
        public InterfaceRetriever interfaceReader = new InterfaceRetriever ();
        private DataTable domainInterfaceHmmSiteTable = null;
        private string domainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
        private PfamPeptideInterfaces pepInterfaces = new PfamPeptideInterfaces();
        private bool withPfam = false;
        #endregion

        #region common hmm sites between domain-peptide and domain-domain interfaces
        public void CountPfamCommonHmmSites()
        {
            InitializeTables(true);
            withPfam = true;

            // save the interacting sequence numbers and the hmm sequence numbers in the file
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine (ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\DomainInterfaceHmmSites_withpfam.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine (ProtCidSettings.dirSettings.pfamPath, "pfamPeptide\\HmmSitesCompNoCommon_withpfam.txt"), true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "HMM interacting sites";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common HMM interacting sites between pfam-peptide and pfam-pfam interfaces");

         //   string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where PepDomainID = -1;";
            string queryString = "";
            if (withPfam)
            {
                queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where PepDomainID > -1;";
            }
            else
            {
                queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where PepDomainID = -1;";
            }
            DataTable pepRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] pepRelSeqIds = GetRelSeqIds(pepRelSeqIdTable);

     //       foreach (DataRow relSeqRow in pepRelSeqIdTable.Rows)
            foreach (int relSeqId in pepRelSeqIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString ());

                try
                {
                    ComparePeptideToDomainInterfaces(relSeqId, pepRelSeqIds);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString () + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString () + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        private void ComparePeptideToDomainInterfaces(int pepRelSeqId, int[] pepRelSeqIds)
        {
       //     string[] pfamPair = GetPfamIds(pepRelSeqId);
       //     string pfamId = pfamPair[0];
            string pfamId = GetPfamIdFromPeptideInterface(pepRelSeqId);
            int[] domainRelSeqIds = GetDomainRelSeqIds(pfamId, pepRelSeqIds);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("# Domain RelSeqIDs: " + domainRelSeqIds.Length.ToString ());
            foreach (int domainRelSeqId in domainRelSeqIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue (pepRelSeqId.ToString() + "_" + domainRelSeqId.ToString ());

                try
                {
                    ComparePeptideToDomainInterfaces(pepRelSeqId, domainRelSeqId, pfamId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + pepRelSeqId.ToString () + " " + domainRelSeqId.ToString () + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Compare " + pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        /// <returns></returns>
        private bool AreRelationsCompared(int pepRelSeqId, int domainRelSeqId)
        {
            string queryString = string.Format("Select First 1 * From PfamInterfaceHmmSiteComp Where RelSeqID1 = {0} AND RelSeqID2 = {1};", pepRelSeqId, domainRelSeqId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteCompTable.Rows.Count == 0)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        private void ComparePeptideToDomainInterfaces(int pepRelSeqId, int domainRelSeqId, string pfamId)
        {
            DataTable pepDomainInterfaceTable = GetPeptideDomainInterfaceTable(pepRelSeqId);
            DataTable domainInterfaceTable = GetRelDomainInterfaceTable(domainRelSeqId);
            ComparePeptideToDomainInterfaces(pepRelSeqId, domainRelSeqId, pfamId, pepDomainInterfaceTable, domainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        /// <param name="pfamId"></param>
        /// <param name="pepDomainInterfaceTable"></param>
        /// <param name="domainInterfaceTable"></param>
        private void ComparePeptideToDomainInterfaces (int pepRelSeqId, int domainRelSeqId, string pfamId,
            DataTable pepDomainInterfaceTable, DataTable domainInterfaceTable)
        {
            string pepPdbId = "";
            int pepDomainInterfaceId = 0;
            long pepDomainId = 0;  // it is domain id for the domain in domain-peptide interface, not the domain id for the peptide
            string domainPdbId = "";
            int domainInterfaceId = 0;
            int[] pepHmmInteractingSites = null;
            string pepHmmSeqNumbers = "";
            int[][] interactingHmmSites = null;
            string dataLine = "";

            string[] domainInterfaces = GetDomainInterfaceIds(domainInterfaceTable);
            string[] pepDomainInterfaces = GetDomainInterfaceIds (pepDomainInterfaceTable);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = pepDomainInterfaces.Length * domainInterfaces.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pepDomainInterfaces.Length * domainInterfaces.Length;

            foreach (string domainInterface in domainInterfaces)
            {
                domainPdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));

                if (Array.IndexOf(pepDomainInterfaces, domainInterface) > -1)
                {
                    continue;
                }

                DataRow[] domainInterfaceRows = domainInterfaceTable.Select
                    (string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", domainPdbId, domainInterfaceId));

                DataTable domainChainPfamTable = pepInterfaces.GetChainPfamAssign(domainPdbId);

                try
                {
                    interactingHmmSites = GetHmmInteractingSites(domainPdbId, domainInterfaceId, pfamId, domainChainPfamTable);
                   
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " " +
                           //     pepPdbId + pepDomainInterfaceId.ToString() + " " +
                                domainPdbId + domainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " " +
                           //     pepPdbId + pepDomainInterfaceId.ToString() + " " +
                                domainPdbId + domainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                if (interactingHmmSites == null)
                {
                    continue;
                }
                foreach (string pepDomainInterface in pepDomainInterfaces)
                {
                    ProtCidSettings.progressInfo.currentFileName = pepDomainInterface + "_" + domainInterface;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    pepPdbId = pepDomainInterface.Substring(0, 4);
                    pepDomainInterfaceId = Convert.ToInt32(pepDomainInterface.Substring(4, pepDomainInterface.Length - 4));

                    DataRow[] pepDomainInterfaceRows = pepDomainInterfaceTable.Select
                        (string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pepPdbId, pepDomainInterfaceId));
                    pepDomainId = Convert.ToInt64(pepDomainInterfaceRows[0]["DomainID"].ToString());
                    pepHmmInteractingSites = GetPeptideHmmInteractingSites(pepPdbId, pepDomainInterfaceId, out pepHmmSeqNumbers);

                   
                    if (interactingHmmSites[0] != null && interactingHmmSites[0].Length > 0)
                    {
                        int numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSites, interactingHmmSites[0]);
                        if (numOfCommonHmmSites > 0)
                        {
                            DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                            hmmSiteRow["PfamId"] = pfamId;
                            hmmSiteRow["RelSeqID1"] = pepRelSeqId;
                            hmmSiteRow["PdbID1"] = pepPdbId;
                            hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceId;
                            hmmSiteRow["DomainID1"] = pepDomainId;
                            hmmSiteRow["RelSeqID2"] = domainRelSeqId;
                            hmmSiteRow["PdbID2"] = domainPdbId;
                            hmmSiteRow["DomainInterfaceID2"] = domainInterfaceId;
                            hmmSiteRow["DomainId2"] = domainInterfaceRows[0]["DomainID1"];
                            hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                            hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSites.Length;
                            hmmSiteRow["NumOfHmmSites2"] = interactingHmmSites[0].Length;
                            pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                        }
                        else
                        {
                            dataLine = pfamId + "\t" + pepRelSeqId.ToString() + "\t" + pepPdbId + "\t" + pepDomainInterfaceId.ToString() + "\t" +
                               pepDomainId.ToString() + "\t" + domainRelSeqId.ToString() + "\t" + domainPdbId + "\t" +
                               domainInterfaceId.ToString() + "\t" + domainInterfaceRows[0]["DomainID1"].ToString() + "\t" +
                               FormatSeqNumbers(pepHmmInteractingSites) + "\t" + FormatSeqNumbers(interactingHmmSites[0]) + "\t" + numOfCommonHmmSites.ToString();
                            noCommonHmmSitesWriter.WriteLine(dataLine);
                        }
                    }
                    if (interactingHmmSites[1] != null && interactingHmmSites[1].Length > 0)
                    {
                        int numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSites, interactingHmmSites[1]);
                        if (numOfCommonHmmSites > 0)
                        {
                            DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                            hmmSiteRow["PfamId"] = pfamId;
                            hmmSiteRow["RelSeqID1"] = pepRelSeqId;
                            hmmSiteRow["PdbID1"] = pepPdbId;
                            hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceId;
                            hmmSiteRow["DomainID1"] = pepDomainId;
                            hmmSiteRow["RelSeqID2"] = domainRelSeqId;
                            hmmSiteRow["PdbID2"] = domainPdbId;
                            hmmSiteRow["DomainInterfaceID2"] = domainInterfaceId;
                            hmmSiteRow["DomainId2"] = domainInterfaceRows[0]["DomainID2"];
                            hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                            hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSites.Length;
                            hmmSiteRow["NumOfHmmSites2"] = interactingHmmSites[1].Length;
                            pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                        }
                        else
                        {
                            dataLine = pfamId + "\t" + pepRelSeqId.ToString() + "\t" + pepPdbId + "\t" + pepDomainInterfaceId.ToString() + "\t" +
                              pepDomainId.ToString() + "\t" + domainRelSeqId.ToString() + "\t" + domainPdbId + "\t" +
                              domainInterfaceId.ToString() + "\t" + domainInterfaceRows[0]["DomainID2"].ToString() + "\t" +
                              FormatSeqNumbers(pepHmmInteractingSites) + "\t" + FormatSeqNumbers(interactingHmmSites[1]) + "\t" + numOfCommonHmmSites.ToString();
                            noCommonHmmSitesWriter.WriteLine(dataLine);
                        }
                    }
                }
                dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamInterfaceHmmSiteTable);
                pfamInterfaceHmmSiteTable.Clear();
                domainHmmInteractingSitesWriter.Flush();
                noCommonHmmSitesWriter.Flush();
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepHmmSeqIds"></param>
        /// <param name="domainHmmSeqIds"></param>
        /// <returns></returns>
        public int GetCommonHmmInteractingSites(int[] pepHmmSeqIds, int[] domainHmmSeqIds)
        {
            int numOfCommonHmmSeqIds = 0;
            foreach (int pepHmmSeqId in pepHmmSeqIds)
            {
                if (Array.IndexOf(domainHmmSeqIds, pepHmmSeqId) > -1)
                {
                    numOfCommonHmmSeqIds++;
                }
            }
            return numOfCommonHmmSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqNumbers"></param>
        /// <returns></returns>
        public string FormatSeqNumbers(int[] seqNumbers)
        {
            string seqNumbersString = "";
            foreach (int seqNumber in seqNumbers)
            {
                seqNumbersString += (seqNumber.ToString() + ",");
            }
            seqNumbersString = seqNumbersString.TrimEnd(',');
            return seqNumbersString;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelDomainInterfaceTable(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private DataTable GetRelDomainInterfaceTable(int relSeqId, string[] domainInterfaces)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataTable selectedDomainInterfaceTable = domainInterfaceTable.Clone();
            string domainInterface = "";
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                domainInterface = interfaceRow["PdbID"].ToString() + interfaceRow["DomainInterfaceID"].ToString();
                if (domainInterfaces.Contains(domainInterface))
                {
                    DataRow newRow = selectedDomainInterfaceTable.NewRow();
                    newRow.ItemArray = interfaceRow.ItemArray;
                    selectedDomainInterfaceTable.Rows.Add(newRow);
                }
            }
            return selectedDomainInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public DataTable GetPeptideDomainInterfaceTable(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID = {0} AND CrystalPack = '0';", relSeqId);
     /*       if (withPfam)
            {
                queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID = {0} AND PepDomainID > -1;", relSeqId);
            }
            else
            {
                queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID = {0} AND PepDomainID = -1;", relSeqId);
            }*/
            DataTable peptideDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return peptideDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetPfamIds(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamPair = new string[2];
            pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return pfamPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string GetPfamIdFromPeptideInterface (int relSeqId)
        {
            string queryString = string.Format("Select First 1 * From PfamPeptideInterfaces WHere RelSeqID = {0};", relSeqId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long domainId = Convert.ToInt64(pepInterfaceTable.Rows[0]["DomainID"].ToString ());
            queryString = string.Format("Select Pfam_ID From PdbPfam Where DomainID = {0};", domainId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pepPfamId = pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            return pepPfamId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamRelSeqIDTable(string pfamId)
        {
            string queryString = string.Format("Select * From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            return relSeqIdTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIdTable"></param>
        /// <returns></returns>
        public int[] GetRelSeqIds(DataTable relSeqIdTable)
        {
            List<int> relSeqIdList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                if (!relSeqIdList.Contains(relSeqId))
                {
                    relSeqIdList.Add(relSeqId);
                }
            }

            return relSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pepRelSeqIds"></param>
        /// <returns></returns>
        private int[] GetDomainRelSeqIds(string pfamId, int[] pepRelSeqIds)
        {
            DataTable relSeqIdTable = GetPfamRelSeqIDTable(pfamId);
            List<int> domainRelSeqIdList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                if (pepRelSeqIds.Contains(relSeqId))
                {
                    continue;
                }
                if (!domainRelSeqIdList.Contains(relSeqId))
                {
                    domainRelSeqIdList.Add(relSeqId);
                }
            }
            return domainRelSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relDomainInterfaceTable"></param>
        /// <returns></returns>
        public string[] GetDomainInterfaceIds(DataTable relDomainInterfaceTable)
        {
            List<string> domainInterfaceList =  new List<string> ();
            string domainInterface = "";
            foreach (DataRow domainInterfaceRow in relDomainInterfaceTable.Rows)
            {
                domainInterface = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainInterfaceID"].ToString();
                if (! domainInterfaceList.Contains (domainInterface))
                {
                    domainInterfaceList.Add (domainInterface);
                }
            }
            return domainInterfaceList.ToArray ();
        }
        #endregion

        #region update
        /// <summary>
        /// /
        /// </summary>
        public void UpdatePfamCommonHmmSites(Dictionary<int, string[]> pepRelUpdateEntryHash)
        {
            InitializeTables(true);

            // save the interacting sequence numbers and the hmm sequence numbers in the file
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\DomainInterfaceHmmSites_update.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfamPeptide\\HmmSitesCompNoCommon_update.txt"), true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "HMM interacting sites";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common HMM interacting sites between pfam-peptide and pfam-pfam interfaces");

            List<int> pepRelSeqIdList = new List<int> (pepRelUpdateEntryHash.Keys);
            pepRelSeqIdList.Sort();
            int[] pepRelSeqIds = new int[pepRelSeqIdList.Count];
            pepRelSeqIdList.CopyTo(pepRelSeqIds);

            foreach (int relSeqId in pepRelSeqIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());

                try
                {
                    string[] updateEntries = pepRelUpdateEntryHash[relSeqId];
                    DeleteHmmSiteCompData(relSeqId, updateEntries);

                    ComparePeptideToDomainInterfaces(relSeqId, updateEntries, pepRelSeqIds);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        private void ComparePeptideToDomainInterfaces(int pepRelSeqId, string[] updateEntries, int[] pepRelSeqIds)
        {
        //    string[] pfamPair = GetPfamIds(pepRelSeqId);
        //    string pfamId = pfamPair[0];
            string pfamId = GetPfamIdFromPeptideInterface (pepRelSeqId);
            int[] domainRelSeqIds = GetDomainRelSeqIds(pfamId, pepRelSeqIds);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("# Domain RelSeqIDs: " + domainRelSeqIds.Length.ToString());
            foreach (int domainRelSeqId in domainRelSeqIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepRelSeqId.ToString() + "_" + domainRelSeqId.ToString());

                try
                {
                    ComparePeptideToDomainInterfaces(pepRelSeqId, domainRelSeqId, pfamId, updateEntries);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Compare " + pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        private void ComparePeptideToDomainInterfaces(int pepRelSeqId, int domainRelSeqId, string pfamId, string[] updateEntries)
        {
            DataTable pepDomainInterfaceTable = GetPeptideDomainInterfaceTable(pepRelSeqId, updateEntries);
            DataTable domainInterfaceTable = GetRelDomainInterfaceTable(domainRelSeqId);
            ComparePeptideToDomainInterfaces(pepRelSeqId, domainRelSeqId, pfamId, pepDomainInterfaceTable, domainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetPeptideDomainInterfaceTable(int relSeqId, string[] updateEntries)
        {
            string queryString = "";
            DataTable pepDomainInterfaceTable = null;
            foreach (string pdbId in updateEntries)
            {
                queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
                DataTable peptideDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (pepDomainInterfaceTable == null)
                {
                    pepDomainInterfaceTable = peptideDomainInterfaceTable.Copy();
                }
                else
                {
                    foreach (DataRow dataRow in peptideDomainInterfaceTable.Rows)
                    {
                        DataRow newRow = pepDomainInterfaceTable.NewRow();
                        newRow.ItemArray = dataRow.ItemArray;
                        pepDomainInterfaceTable.Rows.Add(newRow);
                    }
                }
            }
            return pepDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteHmmSiteCompData(int relSeqId, string[] updateEntries)
        {
            string deleteString = "";
            foreach (string pdbId in updateEntries)
            {
                deleteString = string.Format("Delete From PfamInterfaceHmmSiteComp Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }
        #endregion

        #region PFAM HMM interacting sites for domain interface
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="pfamId"></param>
        /// <param name="domainRelSeqId"></param>
        private void SetDomainInterfaceHmmSitesTable(DataTable domainInterfaceTable, string pfamId, int domainRelSeqId)
        {
            domainInterfaceHmmSiteTable.Clear();
            string[] domainInterfaces = GetDomainInterfaceIds(domainInterfaceTable);
            string pdbId = "";
            int domainInterfaceId = 0;
            int[][] interactingHmmSites = null;
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring (4, domainInterface.Length - 4));
                DataRow[] domainInterfaceRows = domainInterfaceTable.Select
                      (string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));

                DataTable domainChainPfamTable = pepInterfaces.GetChainPfamAssign(pdbId);
                try
                {
                    interactingHmmSites = GetHmmInteractingSites(pdbId, domainInterfaceId, pfamId, domainChainPfamTable);
                    if (interactingHmmSites == null)
                    {
                        continue;
                    }
                    if (interactingHmmSites[0] != null && interactingHmmSites[0].Length > 0)
                    {
                        DataRow hmmSiteRow = domainInterfaceHmmSiteTable.NewRow();
                        hmmSiteRow["PfamId"] = pfamId;
                        hmmSiteRow["RelSeqID"] = pdbId;
                        hmmSiteRow["DomainInterfaceID"] = domainInterfaceId;
                        hmmSiteRow["DomainId"] = domainInterfaceRows[0]["DomainID1"];
                        hmmSiteRow["HmmSites"] = FormatSeqNumbers (interactingHmmSites[0]);
                        hmmSiteRow["ChainNo"] = 1;
                        domainInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                    }
                    if (interactingHmmSites[1] != null && interactingHmmSites[1].Length > 0)
                    {
                        DataRow hmmSiteRow = domainInterfaceHmmSiteTable.NewRow();
                        hmmSiteRow["PfamId"] = pfamId;
                        hmmSiteRow["RelSeqID"] = domainRelSeqId;
                        hmmSiteRow["PdbID"] = pdbId;
                        hmmSiteRow["DomainInterfaceID"] = domainInterfaceId;
                        hmmSiteRow["DomainId"] = domainInterfaceRows[0]["DomainID2"];
                        hmmSiteRow["HmmSites"] = FormatSeqNumbers (interactingHmmSites[1]);
                        hmmSiteRow["ChainNo"] = 2;
                        domainInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(domainRelSeqId.ToString() + " " +
                                pdbId + domainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(domainRelSeqId.ToString() + " " +
                                pdbId + domainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="hmmSeqIds"></param>
        /// <returns></returns>
        public int[] GetPeptideHmmInteractingSites(string pdbId, int domainInterfaceId, out string hmmSeqIds)
        {
            string queryString = string.Format("Select HmmSeqID From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceId = {1} " + 
                                                " Order By HmmSeqID;", pdbId, domainInterfaceId);
            DataTable hmmsiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] hmmSites = new int[hmmsiteTable.Rows.Count];
            int hmmSeqId = 0;
            int count = 0;
            hmmSeqIds = "";
            foreach (DataRow hmmSeqIdRow in hmmsiteTable.Rows)
            {
                hmmSeqId = Convert.ToInt32(hmmSeqIdRow["HmmSeqID"].ToString());
                hmmSites[count] = hmmSeqId;
                hmmSeqIds += (hmmSeqId.ToString() + ",");
                count++;
            }
            hmmSeqIds = hmmSeqIds.TrimEnd(',');
            return hmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        private int[][] GetHmmInteractingSites(string pdbId, int domainInterfaceId, DataTable interfaceHmmSiteTable)
        {
            DataRow[] interfaceHmmSiteRows = interfaceHmmSiteTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            int[][] hmmInteractingSites = new int[2][];
            int chainNo = 0;
            string[] hmmSites = null;
            foreach (DataRow interfaceHmmSiteRow in interfaceHmmSiteRows)
            {
                hmmSites = interfaceHmmSiteRow["HmmSites"].ToString().Split(','); ;
                chainNo = Convert.ToInt32(interfaceHmmSiteRow["ChainNO"].ToString ());

                int[] hmmSeqIds = ConvertStringArrayToIntArray(hmmSites);

                if (chainNo == 1)
                {
                    hmmInteractingSites[0] = hmmSeqIds;
                }
                else if (chainNo == 2)
                {
                    hmmInteractingSites[1] = hmmSeqIds;
                }
            }
            return hmmInteractingSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSites"></param>
        /// <returns></returns>
        private int[] ConvertStringArrayToIntArray(string[] hmmSites)
        {
            int[] hmmSeqIds = new int[hmmSites.Length];
            for (int i = 0; i < hmmSites.Length; i++)
            {
                hmmSeqIds[i] = Convert.ToInt32(hmmSites[i]);
            }
            return hmmSeqIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        private int[][] GetHmmInteractingSites(string pdbId, int domainInterfaceId, string pfamId, DataTable domainChainPfamTable)
        {
            string domainInterfaceFile = Path.Combine(domainInterfaceFileDir, pdbId.Substring (1, 2) + "\\" + pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
            if (! File.Exists(domainInterfaceFile))
            {
                ProtCidSettings.logWriter.WriteLine(pdbId + "_d" + domainInterfaceId.ToString() + " domain interface file not exist.");
                return null;
            }
            DomainInterface domainInterface = null;
            try
            {
                domainInterface = interfaceReader.GetDomainInterfacesFromFiles(domainInterfaceFileDir, pdbId, domainInterfaceId, "cryst");
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine(pdbId + "_d" + domainInterfaceId.ToString() + " retrieving domain interface file error: " + ex.Message);
                return null;
            }
            if (domainInterface == null)
            {
            //    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString () + " Can not get the domain interface from the file");
                ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + 
                                    " Can not get the domain interface from the file, most likely cannot get the contacts hash");
           //     ProtCidSettings.logWriter.Flush();
                return null;
            }
            long[] domainIds = interfaceReader.GetDomainIds(domainInterface.remark);
            bool isMultiChainInterfaceFile = IsMultiChainDomainInterfaceFile(domainIds, domainChainPfamTable);

            string pfamId1 = GetDomainPfamId(domainIds[0], domainChainPfamTable);
            string pfamId2 = GetDomainPfamId(domainIds[1], domainChainPfamTable);
            
            int chainNo = 1;
            int[] interactingSeqIds1 = null;
            int[] interactingHmmSeqIds1 = null;
            int[] interactingSeqIds2 = null;
            int[] interactingHmmSeqIds2 = null;
            string dataLine = "";
            if (pfamId1 == pfamId)
            {
                chainNo = 1;
                interactingSeqIds1 = GetInteractingSeqNumbers(domainInterface, chainNo);
            }
            if (interactingSeqIds1 != null && interactingSeqIds1.Length > 0)
            {
                if (isMultiChainInterfaceFile)
                {
                    interactingHmmSeqIds1 = MapMultiChainDomainInteractingSeqNumbersToHmm(pdbId, domainIds[0], interactingSeqIds1, domainChainPfamTable);
                }
                else
                {
                    interactingHmmSeqIds1 = MapSingleDomainInteractingSeqNumbersToHmm(pdbId, domainIds[0], interactingSeqIds1, domainChainPfamTable);
                }
                dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + domainIds[0].ToString() + "\t" +
                    FormatSeqNumbers(interactingSeqIds1) + "\t" + FormatSeqNumbers(interactingHmmSeqIds1) + "\t" + chainNo.ToString ();
                domainHmmInteractingSitesWriter.WriteLine(dataLine);
            }
            if (pfamId2 == pfamId)
            {
                chainNo = 2;
                interactingSeqIds2 = GetInteractingSeqNumbers(domainInterface, chainNo);
            }
            if (interactingSeqIds2 != null && interactingSeqIds2.Length > 0)
            {
                if (isMultiChainInterfaceFile)
                {
                    interactingHmmSeqIds2 = MapMultiChainDomainInteractingSeqNumbersToHmm(pdbId, domainIds[1], interactingSeqIds2, domainChainPfamTable);
                }
                else
                {
                    interactingHmmSeqIds2 = MapSingleDomainInteractingSeqNumbersToHmm(pdbId, domainIds[1], interactingSeqIds2, domainChainPfamTable);
                }
                dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + domainIds[1].ToString() + "\t" +
                          FormatSeqNumbers(interactingSeqIds2) + "\t" + FormatSeqNumbers(interactingHmmSeqIds2) + "\t" + chainNo.ToString ();
                domainHmmInteractingSitesWriter.WriteLine(dataLine);
            }
            int[][] interfaceHmmInteractingSites = new int[2][];
            interfaceHmmInteractingSites[0] = interactingHmmSeqIds1;
            interfaceHmmInteractingSites[1] = interactingHmmSeqIds2;
            return interfaceHmmInteractingSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="interactingSeqNumbers"></param>
        /// <param name="domainChainPfamTable"></param>
        /// <returns></returns>
        public int[] MapSingleDomainInteractingSeqNumbersToHmm(string pdbId, long domainId, int[] interactingSeqNumbers, DataTable domainChainPfamTable)
        {
            DataRow[] domainRows = domainChainPfamTable.Select(string.Format ("DomainID = '{0}'", domainId));
            List<int> hmmSeqNumberList = new List<int> ();
            int hmmSeqId = 0;
            foreach (int seqId in interactingSeqNumbers)
            {
                hmmSeqId = pepInterfaces.GetHmmSeqId(seqId, domainRows[0]);
                if (hmmSeqId > 0)
                {
                    if (!hmmSeqNumberList.Contains(hmmSeqId))
                    {
                        hmmSeqNumberList.Add(hmmSeqId);
                    }
                }
          //      hmmSeqNumberList.Add(hmmSeqId);
            }
            return hmmSeqNumberList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="interactingSeqNumbers"></param>
        /// <param name="isMultiChainDomainInterface"></param>
        /// <returns></returns>
        private int[] MapMultiChainDomainInteractingSeqNumbersToHmm (string pdbId, long domainId, int[] interactingSeqNumbers, DataTable domainChainPfamTable)
        {
            Dictionary<int, int[]> multiChainSeqNumberHash = MapFileSeqNumbersToXmlSeqNumbers(pdbId, domainId, interactingSeqNumbers);
            List<int> hmmSeqNumberList = new List<int> ();
            int hmmSeqId = 0;
            foreach (int entityId in multiChainSeqNumberHash.Keys)
            {
                int[] seqNumbers = (int[])multiChainSeqNumberHash[entityId];
                DataRow[] chainDomainRows = domainChainPfamTable.Select (string.Format ("DomainID = '{0}' AND EntityID = '{1}'", domainId, entityId));
                foreach (int seqNumber in seqNumbers)
                {
                    hmmSeqId = pepInterfaces.GetHmmSeqId(seqNumber, chainDomainRows[0]);
                    if (hmmSeqId > 0)
                    {
                        if (!hmmSeqNumberList.Contains(hmmSeqId))
                        {
                            hmmSeqNumberList.Add(hmmSeqId);
                        }
                    }
                }
            }
            return hmmSeqNumberList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="chainNo"></param>
        /// <returns></returns>
        private int[] GetInteractingSeqNumbers(DomainInterface domainInterface, int chainNo)
        {
            Dictionary<string, Contact> contactHash = domainInterface.seqContactHash;
            List<int> interactingSeqIdList = new List<int> ();
            int seqId = 0;
            foreach (string seqPair in contactHash.Keys)
            {
                string[] seqPairFields = seqPair.Split('_');
                if (chainNo == 1)
                {
                    seqId = Convert.ToInt32(seqPairFields[0]);
                    if (!interactingSeqIdList.Contains(seqId))
                    {
                        interactingSeqIdList.Add(seqId);
                    }
                }
                else if (chainNo == 2)
                {
                    seqId = Convert.ToInt32(seqPairFields[1]);
                    if (!interactingSeqIdList.Contains(seqId))
                    {
                        interactingSeqIdList.Add(seqId);
                    }
                }
            }
           return interactingSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="fileSeqNumbers"></param>
        /// <returns></returns>
        private Dictionary<int, int[]> MapFileSeqNumbersToXmlSeqNumbers(string pdbId, long domainId, int[] fileSeqNumbers)
        {
            string queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND DomainID = {1} Order By ChainDomainID;", pdbId, domainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int chainDomainId = Convert.ToInt32(domainFileInfoTable.Rows[0]["ChainDomainID"].ToString ());
            DataRow[] chainDomainFileInfoRows = domainFileInfoTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId));
            int fileStart = 0;
            int fileEnd = 0;
            int seqStart = 0;
            int seqNumber = 0;
            int seqDif = 0;
            Dictionary<int, int[]> entityXmlSeqNumberHash = new Dictionary<int,int[]> ();
            int entityId = 0;
            foreach (DataRow domainFileInfoRow in chainDomainFileInfoRows)
            {
                fileStart = Convert.ToInt32(domainFileInfoRow["FileStart"].ToString());
                fileEnd = Convert.ToInt32(domainFileInfoRow["FileEnd"].ToString());

                entityId = Convert.ToInt32(domainFileInfoRow["EntityID"].ToString());
                seqStart = Convert.ToInt32(domainFileInfoRow["SeqStart"].ToString());
                seqDif = fileStart - seqStart;
                List<int> xmlSeqNumberList = new List<int> ();
                for (int i = 0; i < fileSeqNumbers.Length; i++)
                {
                    if (fileSeqNumbers[i] <= fileEnd && fileSeqNumbers[i] >= fileStart)
                    {
                        seqNumber = fileSeqNumbers[i] + seqDif;
                        xmlSeqNumberList.Add(seqNumber);
                    }
                }
                if (xmlSeqNumberList.Count > 0)
                {
                    if (entityXmlSeqNumberHash.ContainsKey(entityId))
                    {
                        int[] thisXmlSeqNumbers = entityXmlSeqNumberHash[entityId];
                        List<int> entityXmlSeqNumberList = new List<int> (thisXmlSeqNumbers);
                        entityXmlSeqNumberList.AddRange(xmlSeqNumberList);

                        int[] xmlSeqNumbers = new int[entityXmlSeqNumberList.Count];
                        entityXmlSeqNumberList.CopyTo(xmlSeqNumbers);

                        entityXmlSeqNumberHash[entityId] = xmlSeqNumbers;
                    }
                    else
                    {
                        int[] xmlSeqNumbers = new int[xmlSeqNumberList.Count];
                        xmlSeqNumberList.CopyTo(xmlSeqNumbers);
                        entityXmlSeqNumberHash.Add(entityId, xmlSeqNumbers);
                    }
                }
            }
            return entityXmlSeqNumberHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <param name="domainPfamTable"></param>
        /// <returns></returns>
        private bool IsDomainMultiChain(long domainId, DataTable domainChainPfamTable)
        {
            DataRow[] domainRows = domainChainPfamTable.Select(string.Format("DomainID = '{0}'", domainId));
            int entityId = 0;
            List<int> entityList = new List<int> ();
            foreach (DataRow domainRow in domainRows)
            {
                entityId = Convert.ToInt32(domainRow["EntityId"].ToString ());
                if (!entityList.Contains(entityId))
                {
                    entityList.Add(entityId);
                }
            }
            if (entityList.Count > 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIds"></param>
        /// <param name="domainPfamTable"></param>
        /// <returns></returns>
        private bool IsMultiChainDomainInterfaceFile(long[] domainIds, DataTable domainChainPfamTable)
        {
            foreach (long domainId in domainIds)
            {
                bool isMultiChainDomain = IsDomainMultiChain(domainId, domainChainPfamTable);
                if (isMultiChainDomain)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string GetDomainPfamId(long domainId, DataTable pfamTable)
        {
            DataRow[] domainRows = pfamTable.Select(string.Format("DomainID = '{0}'", domainId));
            string pfamId = "";
            if (domainRows.Length > 0)
            {
                pfamId = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return pfamId;
        }
        #endregion

        #region initialize table
        private void InitializeTables(bool isUpdate)
        {
            pfamInterfaceHmmSiteTable = new DataTable(interfaceHmmSiteTableName);
            string[] hmmSiteColumns = {"PfamID", "RelSeqID1", "PdbID1", "DomainInterfaceID1", "DomainID1",
                                          "RelSeqID2", "PdbID2", "DomainInterfaceID2", "DomainID2",  
                                      "NumOfHmmSites1", "NumOfHmmSites2", "NumOfCommonHmmSites", "PepComp"};
            foreach (string col in hmmSiteColumns)
            {
                pfamInterfaceHmmSiteTable.Columns.Add(new DataColumn(col));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "CREATE TABLE " + interfaceHmmSiteTableName + " ( " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " RelSeqID1 INTEGER NOT NULL, " +
                    " PdbID1 CHAR(4) NOT NULL, " +
                    " DomainInterfaceID1 INTEGER NOT NULL, " +
                    " DomainID1 BIGINT NOT NULL, " +
                    " RelSeqID2 INTEGER NOT NULL, " +
                    " PdbID2 CHAR(4) NOT NULL, " +
                    " DomainID2 BIGINT NOT NULL, " +
                    " DomainInterfaceID2 INTEGER NOT NULL, " +
                    " NumOfHmmSites1 INTEGER NOT NULL, " +
                    " NumOfHmmSites2 INTEGER NOT NULL, " +
                    " NumOfCommonHmmSites INTEGER NOT NULL, " + 
                    " ChainRmsd FLOAT, " +
                    " InteractChainRmsd FLOAT, " +
                    " PepRmsd FLOAT, " +
                    " InteractPepRmsd FLOAT, " +
               //     " ChainNO CHAR, " +   // when compare chain/domain to peptide, if the interface is a homodimer, then need to record if the chain is A or B
                    " LocalPepRmsd FLOAT, " +
                    " PepStart INTEGER, " +
                    " PepEnd INTEGER, " +
                    " ChainStart INTEGER, " +
                    " ChainEnd INTEGER, " +
                    " PepAlignment VARCHAR(128), " +
                    " ChainAlignment VARCHAR(128), " +
                    " SCORE FLOAT, " +
                    " PepComp CHAR(1) );";  // two compared interfaces are peptide interfaces or not 
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, interfaceHmmSiteTableName);

                string createIndexString = "CREATE INDEX " + interfaceHmmSiteTableName + "_idx1 ON " +
                    interfaceHmmSiteTableName + "(PdbID1, DomainInterfaceID1);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, interfaceHmmSiteTableName);
                createIndexString = "CREATE INDEX " + interfaceHmmSiteTableName + "_idx2 ON " +
                    interfaceHmmSiteTableName + "(PdbID2, DomainInterfaceID2);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, interfaceHmmSiteTableName);
            }
        }
        #endregion

        #region #common hmm sites in peptide interfaces
        /// <summary>
        /// 
        /// </summary>
        public void CountPeptideInterfaceHmmSites()
        {
            InitializeTables(false);

            // save the interacting sequence numbers and the hmm sequence numbers in the file
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PeptideInterfaceHmmSites.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfamPeptide\\HmmSitesCompNoCommon_pep.txt"), true);

 //           Hashtable pepPfamRelSeqIdHash = GetPepPfamIdRelSeqIdHash();
            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
       //     ProtCidSettings.progressInfo.totalOperationNum = pepPfamRelSeqIdHash.Count;
       //     ProtCidSettings.progressInfo.totalStepNum = pepPfamRelSeqIdHash.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites in peptide interfaces. #PfamIDs = " + pepPfamTable.Rows.Count.ToString());
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count #common hmm sites in peptide interfaces");
            string pepPdbIdI = "";
            int pepDomainInterfaceIdI = 0;
            string pepPdbIdJ = "";
            int pepDomainInterfaceIdJ = 0;
            int[] pepHmmInteractingSitesI = null;
            int[] pepHmmInteractingSitesJ = null;
            string pepHmmSeqNumbersI = "";
            string pepHmmSeqNumbersJ = "";
            int numOfCommonHmmSites = 0;
            string dataLine = "";
            int pfamCount = 1;
            string pepPfamId = "";
            foreach (DataRow pepPfamRow in pepPfamTable.Rows)
            {
                pepPfamId = pepPfamRow["PfamID"].ToString().TrimEnd();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString () + ": " + pepPfamId);
                if (IsPfamPeptideCompared(pepPfamId))
                {
                    continue;
                }
                
                DataTable peptideInterfaceTable = GetRelationPeptideInterfacesTable(pepPfamId);
                string[] peptideInterfaces = GetPeptideInterfaces(peptideInterfaceTable);

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalStepNum = (peptideInterfaces.Length + 1) * peptideInterfaces.Length / 2;
                ProtCidSettings.progressInfo.totalOperationNum  = (peptideInterfaces.Length + 1) * peptideInterfaces.Length / 2;

                for (int i = 0; i < peptideInterfaces.Length; i++)
                {
                    pepPdbIdI = peptideInterfaces[i].Substring(0, 4);
                    pepDomainInterfaceIdI = Convert.ToInt32(peptideInterfaces[i].Substring (4, peptideInterfaces[i].Length - 4));
                    pepHmmInteractingSitesI = GetPeptideHmmInteractingSites(pepPdbIdI, pepDomainInterfaceIdI, out pepHmmSeqNumbersI);
                    DataRow pepInterfaceRowI = GetPeptideInterfaceRow(pepPdbIdI, pepDomainInterfaceIdI, peptideInterfaceTable);

                    for (int j = i + 1; j < peptideInterfaces.Length; j++)
                    {
                        pepPdbIdJ = peptideInterfaces[j].Substring (0, 4);
                        pepDomainInterfaceIdJ = Convert.ToInt32 (peptideInterfaces[j].Substring (4, peptideInterfaces[j].Length - 4));

                        ProtCidSettings.progressInfo.currentOperationNum++;
                        ProtCidSettings.progressInfo.currentStepNum++;
                        ProtCidSettings.progressInfo.currentFileName = peptideInterfaces[i] + "_" + peptideInterfaces[j];

                        if (ArePeptideInterfaceHmmCompExist (pepPdbIdI, pepDomainInterfaceIdI, pepPdbIdJ, pepDomainInterfaceIdJ))
                        {
                            continue;
                        }
                        
                        pepHmmInteractingSitesJ = GetPeptideHmmInteractingSites(pepPdbIdJ, pepDomainInterfaceIdJ, out pepHmmSeqNumbersJ);
                        DataRow pepInterfaceRowJ = GetPeptideInterfaceRow(pepPdbIdJ, pepDomainInterfaceIdJ, peptideInterfaceTable);

                        numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSitesI, pepHmmInteractingSitesJ);

                        if (numOfCommonHmmSites > 0)
                        {
                            DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                            hmmSiteRow["PfamId"] = pepPfamId;
                            hmmSiteRow["RelSeqID1"] = pepInterfaceRowI["RelSeqID"];
                            hmmSiteRow["PdbID1"] = pepPdbIdI;
                            hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceIdI;
                            hmmSiteRow["DomainID1"] = pepInterfaceRowI["DomainID"];
                            hmmSiteRow["RelSeqID2"] = pepInterfaceRowJ["RelSeqID"];
                            hmmSiteRow["PdbID2"] = pepPdbIdJ;
                            hmmSiteRow["DomainInterfaceID2"] = pepDomainInterfaceIdJ;
                            hmmSiteRow["DomainId2"] = pepInterfaceRowJ["DomainID"];
                            hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                            hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSitesI.Length;
                            hmmSiteRow["NumOfHmmSites2"] = pepHmmInteractingSitesJ.Length;
                            hmmSiteRow["PepComp"] = "1";
                            pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                        }
                        else
                        {
                            dataLine = pepPfamId + "\t" + pepInterfaceRowI["RelSeqID"].ToString() + "\t" + pepPdbIdI + "\t" + pepDomainInterfaceIdI.ToString() + "\t" +
                               pepInterfaceRowI["DomainID"].ToString() + "\t" +
                               pepInterfaceRowJ["RelSeqID"].ToString() + "\t" + pepPdbIdJ + "\t" +
                               pepDomainInterfaceIdJ.ToString() + "\t" + pepInterfaceRowJ["DomainID"].ToString() + "\t" +
                               FormatSeqNumbers(pepHmmInteractingSitesI) + "\t" + FormatSeqNumbers(pepHmmInteractingSitesJ);
                            noCommonHmmSitesWriter.WriteLine(dataLine);
                        }
                    }
                }
                dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamInterfaceHmmSiteTable);
                pfamInterfaceHmmSiteTable.Clear();

                noCommonHmmSitesWriter.Flush();

                pfamCount++;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamId"></param>
        /// <returns></returns>
        private bool IsPfamPeptideCompared(string pepPfamId)
        {
            string queryString = string.Format("Select * From " + interfaceHmmSiteTableName + " Where PfamID = '{0}';", pepPfamId);
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
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private bool ArePeptideInterfaceHmmCompExist(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp " + 
                " Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1} AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3};", 
                pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
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
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private DataRow GetPeptideInterfaceRow(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable)
        {
            DataRow[] interfaceRows = peptideInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            return interfaceRows[0];
        }
       
        /// <summary>
        /// /
        /// </summary>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetPeptideInterfaces(DataTable peptideInterfaceTable)
        {
            List<string> pepInterfaceList = new List<string> ();
            string pepInterface = "";
            foreach (DataRow pepInterfaceRow in peptideInterfaceTable.Rows)
            {
                pepInterface = pepInterfaceRow["PdbID"].ToString() + pepInterfaceRow["DomainInterfaceID"].ToString();
                pepInterfaceList.Add(pepInterface); 
            }
            return pepInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private DataTable GetRelationPeptideInterfacesTable (string pfamId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable relPeptideInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            return relPeptideInterfaceTable;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetPepRelSeqIds()
        {
            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] pepRelSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                pepRelSeqIds[count] = relSeqId;
                count++;
            }
            return pepRelSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, int[]> GetPepPfamIdRelSeqIdHash ()
        {
            Dictionary<string, List<int>> pepPfamRelSeqIdListHash = new Dictionary<string,List<int>> ();
            int[] pepRelSeqIds = GetPepRelSeqIds();
            string pepPfamId = "";
            foreach (int relSeqId in pepRelSeqIds)
            {
                pepPfamId = GetPfamIdFromPeptideInterface (relSeqId);
                if (pepPfamRelSeqIdListHash.ContainsKey(pepPfamId))
                {
                    pepPfamRelSeqIdListHash[pepPfamId].Add(relSeqId);
                }
                else
                {
                    List<int> relSeqIdList = new List<int> ();
                    relSeqIdList.Add(relSeqId);
                    pepPfamRelSeqIdListHash.Add(pepPfamId, relSeqIdList);
                }
            }
            Dictionary<string, int[]> pepPfamRelSeqIdHash = new Dictionary<string, int[]>();
            foreach (string pfamId in pepPfamRelSeqIdListHash.Keys )
            {
                pepPfamRelSeqIdHash.Add (pfamId, pepPfamRelSeqIdListHash[pfamId].ToArray ());
            }
            return pepPfamRelSeqIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadMultiChainPfamIds()
        {
            StreamReader dataReader = new StreamReader("PfamMultiChainDomains.txt");
            string line = "";
            List<string> multiChainPfamIdList = new List<string> ();
            string pfamId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');
                pfamId = fields[0];
                multiChainPfamIdList.Add(pfamId);
            }
            dataReader.Close();

            return multiChainPfamIdList.ToArray ();
        }

        #region update common hmm sites of peptide interfaces
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePeptideInterfaceHmmSites(string[] updateEntries)
        {
            InitializeTables(true);

            // save the interacting sequence numbers and the hmm sequence numbers in the file
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PeptideInterfaceHmmSites_update.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfamPeptide\\HmmSitesCompNoCommon_pep_update.txt"), true);

//            Hashtable pepPfamRelSeqIdHash = GetPepPfamIdRelSeqIdHash(updateEntries);
            string[] pepPfamIds = GetPepPfamIds(updateEntries);
            // delete hmm site comp rows for the update entries, 
            // this must happen before comparing the peptide and domain interfaces
            DeleteObsoleteHmmSiteCompRows(updateEntries);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites in peptide interfaces. #PfamIDs = " + pepPfamIds.Length);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count #common hmm sites in peptide interfaces");
            string pepPdbIdI = "";
            int pepDomainInterfaceIdI = 0;
            string pepPdbIdJ = "";
            int pepDomainInterfaceIdJ = 0;
            int[] pepHmmInteractingSitesI = null;
            int[] pepHmmInteractingSitesJ = null;
            string pepHmmSeqNumbersI = "";
            string pepHmmSeqNumbersJ = "";
            int numOfCommonHmmSites = 0;
            string dataLine = "";
            int pfamCount = 1;
            foreach (string pepPfamId in pepPfamIds )
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString() + ": " + pepPfamId);
              
                DataTable peptideInterfaceTable = GetRelationPeptideInterfacesTable(pepPfamId);
                string[] peptideInterfaces = GetPeptideInterfaces(peptideInterfaceTable);

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalStepNum = (peptideInterfaces.Length + 1) * peptideInterfaces.Length / 2;
                ProtCidSettings.progressInfo.totalOperationNum = (peptideInterfaces.Length + 1) * peptideInterfaces.Length / 2;

                for (int i = 0; i < peptideInterfaces.Length; i++)
                {
                    pepPdbIdI = peptideInterfaces[i].Substring(0, 4);
                    pepDomainInterfaceIdI = Convert.ToInt32(peptideInterfaces[i].Substring(4, peptideInterfaces[i].Length - 4));
                    pepHmmInteractingSitesI = GetPeptideHmmInteractingSites(pepPdbIdI, pepDomainInterfaceIdI, out pepHmmSeqNumbersI);
                    DataRow pepInterfaceRowI = GetPeptideInterfaceRow(pepPdbIdI, pepDomainInterfaceIdI, peptideInterfaceTable);

                    for (int j = i + 1; j < peptideInterfaces.Length; j++)
                    {
                        pepPdbIdJ = peptideInterfaces[j].Substring(0, 4);
                        // one of pdbid must be in the update list
                        if (! updateEntries.Contains(pepPdbIdI) && !updateEntries.Contains(pepPdbIdJ))
                        {
                            continue;
                        }
                        pepDomainInterfaceIdJ = Convert.ToInt32(peptideInterfaces[j].Substring(4, peptideInterfaces[j].Length - 4));

                        ProtCidSettings.progressInfo.currentOperationNum++;
                        ProtCidSettings.progressInfo.currentStepNum++;
                        ProtCidSettings.progressInfo.currentFileName = peptideInterfaces[i] + "_" + peptideInterfaces[j];

                        pepHmmInteractingSitesJ = GetPeptideHmmInteractingSites(pepPdbIdJ, pepDomainInterfaceIdJ, out pepHmmSeqNumbersJ);
                        DataRow pepInterfaceRowJ = GetPeptideInterfaceRow(pepPdbIdJ, pepDomainInterfaceIdJ, peptideInterfaceTable);

                        numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSitesI, pepHmmInteractingSitesJ);

                        if (numOfCommonHmmSites > 0)
                        {
                            DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                            hmmSiteRow["PfamId"] = pepPfamId;
                            hmmSiteRow["RelSeqID1"] = pepInterfaceRowI["RelSeqID"];
                            hmmSiteRow["PdbID1"] = pepPdbIdI;
                            hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceIdI;
                            hmmSiteRow["DomainID1"] = pepInterfaceRowI["DomainID"];
                            hmmSiteRow["RelSeqID2"] = pepInterfaceRowJ["RelSeqID"];
                            hmmSiteRow["PdbID2"] = pepPdbIdJ;
                            hmmSiteRow["DomainInterfaceID2"] = pepDomainInterfaceIdJ;
                            hmmSiteRow["DomainId2"] = pepInterfaceRowJ["DomainID"];
                            hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                            hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSitesI.Length;
                            hmmSiteRow["NumOfHmmSites2"] = pepHmmInteractingSitesJ.Length;
                            hmmSiteRow["PepComp"] = "1";
                            pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                        }
                        else
                        {
                            dataLine = pepPfamId + "\t" + pepInterfaceRowI["RelSeqID"].ToString() + "\t" + pepPdbIdI + "\t" + pepDomainInterfaceIdI.ToString() + "\t" +
                               pepInterfaceRowI["DomainID"].ToString() + "\t" +
                               pepInterfaceRowJ["RelSeqID"].ToString() + "\t" + pepPdbIdJ + "\t" +
                               pepDomainInterfaceIdJ.ToString() + "\t" + pepInterfaceRowJ["DomainID"].ToString() + "\t" +
                               FormatSeqNumbers(pepHmmInteractingSitesI) + "\t" + FormatSeqNumbers(pepHmmInteractingSitesJ);
                            noCommonHmmSitesWriter.WriteLine(dataLine);
                        }
                    }
                }
                dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamInterfaceHmmSiteTable);
                pfamInterfaceHmmSiteTable.Clear();

                noCommonHmmSitesWriter.Flush();

                pfamCount++;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetPepPfamIds (string[] updateEntries)
        {
            string queryString = "";
            List<string> pepPfamIdList = new List<string>();
            string pfamId = "";
            for (int i = 0; i < updateEntries.Length; i += 500)
            {
                string[] subEntries = ParseHelper.GetSubArray (updateEntries, i, 500);
                queryString = string.Format("Select Distinct PfamID From PfamPeptideInterfaces " +
                    " Where PdbID IN ({0});", ParseHelper.FormatSqlListString (subEntries));
                DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow pfamIdRow in pfamIdTable.Rows)
                {
                    pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                    if (! pepPfamIdList.Contains (pfamId))
                    {
                        pepPfamIdList.Add(pfamId);
                    }
                }
            }
            return pepPfamIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, int[]> GetPepPfamIdRelSeqIdHash(string[] updateEntries)
        {
            Dictionary<string, List<int>> updatePfamRelSeqIdListHash = new Dictionary<string,List<int>> ();
            foreach (string pdbId in updateEntries)
            {
                GetPfamRelSeqIds(pdbId, updatePfamRelSeqIdListHash);
            }
            Dictionary<string, int[]> updatePfamRelSeqIdHash = new Dictionary<string, int[]>();
            foreach (string pfamId in updatePfamRelSeqIdListHash.Keys)
            {
                updatePfamRelSeqIdHash.Add (pfamId, updatePfamRelSeqIdListHash[pfamId].ToArray ());
            }
            return updatePfamRelSeqIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="updatePfamRelSeqIdHash"></param>
        private void GetPfamRelSeqIds(string pdbId, Dictionary<string, List<int>> updatePfamRelSeqIdHash)
        {
            string queryString = string.Format("Select RelSeqId, PfamId From PfamPeptideInterfaces WHere PdbID = '{0}';", pdbId);
            DataTable relSeqIdPfamTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            string pfamId = "";
            foreach (DataRow dataRow in relSeqIdPfamTable.Rows)
            {
                relSeqId = Convert.ToInt32(dataRow["RelSeqID"].ToString());
                pfamId = dataRow["PfamId"].ToString().TrimEnd();
                if (updatePfamRelSeqIdHash.ContainsKey(pfamId))
                {
                    if (!updatePfamRelSeqIdHash[pfamId].Contains(relSeqId))
                    {
                        updatePfamRelSeqIdHash[pfamId].Add(relSeqId);
                    }
                }
                else
                {
                    List<int> relSeqIdList = new List<int> ();
                    relSeqIdList.Add(relSeqId);
                    updatePfamRelSeqIdHash.Add(pfamId, relSeqIdList);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsoleteHmmSiteCompRows(string[] updateEntries)
        {
            foreach (string pdbId in updateEntries)
            {
                DeleteObsoleteHmmSiteCompRows(pdbId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsoleteHmmSiteCompRows(string pdbId)
        {
            string deleteString = string.Format("Delete From PfamInterfaceHmmSiteComp WHere PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion
        #endregion


        #region print data out
        public void SeparateDataToPfams ()
        {
            string dataDir = @"D:\DbProjectData\pfam\PfamPeptide";
            StreamReader dataReader = new StreamReader(Path.Combine (dataDir, "PeptideInterfaceHmmSiteComp.txt"));
            Dictionary<string, List<string>>  pfamDataLineHash = new Dictionary<string,List<string>> ();
            StreamWriter dataWriter = null;
            string line = "";
            string pfamId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split ('\t');
                pfamId = fields[0];
                if (pfamDataLineHash.ContainsKey(pfamId))
                {
                    pfamDataLineHash[pfamId].Add(line);
                }
                else
                {
                    List<string> dataLineList = new List<string> ();
                    dataLineList.Add(line);
                    pfamDataLineHash.Add(pfamId, dataLineList);
                }
            }
            dataReader.Close ();

            foreach (string lsPfamId in pfamDataLineHash.Keys)
            {
                dataWriter = new StreamWriter(Path.Combine (dataDir, "HmmSiteComp_" + lsPfamId + ".txt"));
                foreach (string dataLine in pfamDataLineHash[lsPfamId])
                {
                    dataWriter.WriteLine (dataLine);
                }
                dataWriter.Close ();
            }
        }

        public void PrintPeptideInterfaceHmmCompData()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\PeptideInterfaceHmmSiteComp.txt"), true);
            string queryString = "Select Distinct PdbID, DomainInterfaceID From PfamPeptideInterfaces Where PdbID >= '1kj7';";
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow pepInterfaceRow in pepInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(pepInterfaceRow["DomainInterfaceID"].ToString());

                PrintPeptideInterfaceHmmCompInfo(pdbId, domainInterfaceId, pepInterfaceTable, dataWriter);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void PrintPeptideInterfaceHmmCompInfo(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp WHere PdbID1 = '{0}' AND DomainInterfaceId1 = {1};", pdbId, domainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId2 = "";
            int domainInterfaceId2 = 0;
            foreach (DataRow hmmSiteRow in hmmSiteCompTable.Rows)
            {
                pdbId2 = hmmSiteRow["PdbID2"].ToString();
                domainInterfaceId2 = Convert.ToInt32(hmmSiteRow["DomainInterfaceID2"].ToString());
                if (IsDomainInterfacePeptide(pdbId2, domainInterfaceId2, peptideInterfaceTable))
                {
                    UpdateIsPepComp(pdbId, domainInterfaceId, pdbId2, domainInterfaceId2);
                    dataWriter.WriteLine(ParseHelper.FormatDataRow(hmmSiteRow));
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private bool IsDomainInterfacePeptide(string pdbId, int domainInterfaceId, DataTable peptideInterfaceTable)
        {
            DataRow[] interfaceRows = peptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (interfaceRows.Length > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        private void UpdateIsPepComp(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string updateString = string.Format("Update PfamInterfaceHmmSiteComp  Set pepComp = '1' Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1} " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3};", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion

        #region Q scores and #CommonHmmSites
        public void PrintBestHmmSitesComp()
        {
            StreamWriter dataWriter = new StreamWriter(@"D:\DbProjectData\pfam\PfamPeptide\BestHmmSiteComps.txt");
            string queryString = "Select Distinct PdbId, DomainInterfaceID From PfamPeptideInterfaces Where CrystalPack = '0';";
            DataTable pepDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow pepDomainInterfaceRow in pepDomainInterfaceTable.Rows)
            {
                pdbId = pepDomainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(pepDomainInterfaceRow["DomainInterfaceID"].ToString ());

                DataRow bestHmmCompRow = GetBestHmmSitesCompRow(pdbId, domainInterfaceId);
                dataWriter.WriteLine(ParseHelper.FormatDataRow(bestHmmCompRow));
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataRow GetBestHmmSitesCompRow(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1};", pdbId, domainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            int maxNumOfCommonHmmSites = 0;
            int numOfHmmSites = 0;
            DataRow bestHmmCompRow = null;
            foreach (DataRow hmmSiteCompRow in hmmSiteCompTable.Rows)
            {
                numOfHmmSites = Convert.ToInt32(hmmSiteCompRow["NumOfCommonHmmSites"].ToString ());
                if (maxNumOfCommonHmmSites < numOfHmmSites)
                {
                    maxNumOfCommonHmmSites = numOfHmmSites;
                    bestHmmCompRow = hmmSiteCompRow;
                }
            }
            return bestHmmCompRow;
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintQAndNumOfHmmSites()
        {
            StreamWriter dataWriter = new StreamWriter(@"D:\DbProjectData\pfam\PfamPeptide\PepQNumOfHmmSites.txt");
            string queryString = "Select Distinct PdbId, DomainInterfaceID From PfamPeptideInterfaces Where PepDomainId > -1;";
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow pepInterfaceRow in pepInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(pepInterfaceRow["DomainInterfaceID"].ToString ());

                GetQscoresNumOfHmmSites(pdbId, domainInterfaceId, dataWriter);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPdbId"></param>
        /// <param name="pepDomainInterfaceId"></param>
        /// <returns></returns>
        private void GetQscoresNumOfHmmSites(string pepPdbId, int pepDomainInterfaceId, StreamWriter dataWriter)
        {
            DataTable domainInterfaceCompTable = GetDomainInterfaceCompTable(pepPdbId, pepDomainInterfaceId);

            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1};", pepPdbId, pepDomainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            string domainPdbId = "";
            int domainInterfaceId = 0;
            double qscore = -1;
            string dataLine = "";
            foreach (DataRow hmmCompRow in hmmSiteCompTable.Rows)
            {
                domainPdbId = hmmCompRow["PdbID2"].ToString();
                domainInterfaceId = Convert.ToInt32(hmmCompRow["DomainInterfaceID2"].ToString ());

                qscore = GetQscore(domainPdbId, domainInterfaceId, domainInterfaceCompTable);
                if (qscore > -1)
                {
                    dataLine = pepPdbId + "\t" + pepDomainInterfaceId.ToString() + "\t"  +
                             domainPdbId + "\t" + domainInterfaceId.ToString() + "\t" +
                             qscore.ToString() + "\t" + hmmCompRow["NumOfCommonHmmSites"].ToString() + "\t" +
                             hmmCompRow["NumOfHmmSites1"].ToString () + "\t" + 
                             hmmCompRow["NumOfHmmSites2"].ToString ();
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceCompTable"></param>
        /// <returns></returns>
        private double GetQscore(string pdbId, int domainInterfaceId, DataTable domainInterfaceCompTable)
        {
            DataRow[] qscoreRows = domainInterfaceCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pdbId, domainInterfaceId));
            if (qscoreRows.Length == 0)
            {
                qscoreRows = domainInterfaceCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
            }
            if (qscoreRows.Length > 0)
            {
                return Convert.ToDouble(qscoreRows[0]["Qscore"].ToString ());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceCompTable(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1}) OR " + 
                " (PdbID2 = '{0}' AND DomainInterfaceID2 = {1});", pdbId, domainInterfaceId);
            DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceCompTable;
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void CountMissingPeptideInterfaceHmmSites()
        {
            InitializeTables(true);

            string queryString = "Select PfamId, RelSeqID1, PdbID1, DomainInterfaceID1, RelSeqID2, PdbID2, DomainInterfaceID2" +
                " From PfamInterfaceHmmSiteComp WHere CHainRmsd = -1 AND NumOfCommonHmmSites < 2;";
            DataTable hmmCompEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            string pdbId1 = "";
            int domainInterfaceId1 = 0;
            string pdbId2 = "";
            int domainInterfaceId2 = 0;

            int[] pepHmmInteractingSites1 = null;
            int[] pepHmmInteractingSites2 = null;
            string pepHmmSeqNumbers1 = "";
            string pepHmmSeqNumbers2 = "";
            int numOfCommonHmmSites = 0;
            int relSeqId1 = 0;
            int relSeqId2 = 0;

            string pfamId = "";
            foreach (DataRow interfacePairRow in hmmCompEntryTable.Rows)
            {
                relSeqId1 = Convert.ToInt32(interfacePairRow["RelSeqId1"].ToString ());
                pdbId1 = interfacePairRow["PdbID1"].ToString();
                domainInterfaceId1 = Convert.ToInt32(interfacePairRow["DomainInterfaceID1"].ToString());

                pepHmmInteractingSites1 = GetPeptideHmmInteractingSites(pdbId1, domainInterfaceId1, out pepHmmSeqNumbers1);

                relSeqId2 = Convert.ToInt32(interfacePairRow["RelSeqId2"].ToString());
                pdbId2 = interfacePairRow["PdbID2"].ToString();
                domainInterfaceId2 = Convert.ToInt32(interfacePairRow["DomainInterfaceID2"].ToString());

                pepHmmInteractingSites2 = GetPeptideHmmInteractingSites(pdbId2, domainInterfaceId2, out pepHmmSeqNumbers2);

                numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSites1, pepHmmInteractingSites2);

                if (numOfCommonHmmSites > 0)
                {
                    UpdateHmmSiteCompRow(pfamId, relSeqId1, pdbId1, domainInterfaceId1, relSeqId2, pdbId2, domainInterfaceId2,
                        pepHmmInteractingSites1.Length, pepHmmInteractingSites2.Length, numOfCommonHmmSites);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="relSeqId1"></param>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="relSeqId2"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="numOfHmmSites1"></param>
        /// <param name="numOfHmmSites2"></param>
        /// <param name="numOfComHmmSites"></param>
        private void UpdateHmmSiteCompRow(string pfamId, int relSeqId1, string pdbId1, int domainInterfaceId1, int relSeqId2, string pdbId2, int domainInterfaceId2,
            int numOfHmmSites1, int numOfHmmSites2, int numOfComHmmSites)
        {
            string updateString = string.Format("Update PfamInterfaceHmmSiteComp Set NumOfHmmSites1 = {0}, NumOfHmmSites2 = {1}, NumOfCommonHmmSites = {2} " +
                " Where PfamID = '{3}' AND RelSeqID1 = {4} AND PdbID1 = '{5}' AND DomainInterfaceID1 = {6} AND " +
                " RelSeqID2 = {7} AND PdbID2 = '{8}' AND DomainInterfaceID2 = {9};", 
                numOfHmmSites1, numOfHmmSites2, numOfComHmmSites, pfamId, relSeqId1, pdbId1, domainInterfaceId1, relSeqId2, pdbId2, domainInterfaceId2);
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
        private bool ArePeptideInterfaceHmmCompExistOnRmsd(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select * From PfamInterfaceHmmSiteComp " +
                " Where PdbID1 = '{0}' AND DomainInterfaceID1 = {1} AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3} AND ChainRmsd = -1;",
                pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
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
        public void UpdateMultiChainPfamPeptideInterfaces ()
        {
            string[] pfamIdsToBeUpdated = ReadMultiChainPfamIds();
         //   string[] pfamIdsToBeUpdated = GetPfamIdsToBeUpdated();
      //      string[] pfamIdsToBeUpdated = { "Insulin" };
    //        UpdatePeptideInterfaceClusters(pfamIdsToBeUpdated);
     //       UpdateClusterPeptideInterfaceFiles(pfamIdsToBeUpdated);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamIdsToBeUpdated()
        {
            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where ChainDomainID = PepChainDomainID;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            List<string> pfamIdToBeUpdateList = new List<string> ();
            string pfamId = "";
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                pfamId = pfamIdTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                if (!pfamIdToBeUpdateList.Contains(pfamId))
                {
                    pfamIdToBeUpdateList.Add(pfamId);
                }
            }
            return pfamIdToBeUpdateList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        public void CheckPeptideInterfacesNotInAsu()
        {
            StreamWriter dataWriter = new StreamWriter("PeptideInterfaceNotInAsu_0.txt");
            string queryString = "Select Distinct PdbID From PdbBuGen;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            string dataLine = "";
            List<string> entryList = new List<string> ();
            int numOfBus = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
     
                string[] peptideAsymChains = GetPeptideAsymChains(pdbId, asuTable);
                if (peptideAsymChains.Length > 0)
                {
                    dataLine = CheckPeptideChainsInSameSymOpBUs(pdbId, peptideAsymChains, ref numOfBus);
                    if (dataLine != "")
                    {
                        dataWriter.WriteLine(dataLine);
                        dataWriter.WriteLine();
                        dataWriter.Flush();

                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                        }
                    }
                }
            }
            dataWriter.WriteLine("#Entries = " + entryList.Count.ToString ());
            dataWriter.WriteLine("#BUs = " + numOfBus.ToString ());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private string CheckPeptideChainsInSameSymOpBUs(string pdbId, string[] peptideAsymChains, ref int numOfBus)
        {
            string queryString = string.Format("Select PdbId, AsymID, BiolUnitID, SymOpNum, SymmetryString From PdbBuGen Where PdbID = '{0}';", pdbId);
            DataTable buGenTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> biolUnitIdList = new List<string> ();
            foreach (DataRow buGenRow in buGenTable.Rows)
            {
                string buId = buGenRow["BiolUnitID"].ToString().TrimEnd();
                if (!biolUnitIdList.Contains(buId))
                {
                    biolUnitIdList.Add(buId);
                }
            }
            string asymChain = "";
            string symOp = "";
            List<string> peptideSymOpList = new List<string>();
            List<string> chainSymOpList = new List<string>();
            string peptideSymOpInfoLine = "";
            foreach (string buId in biolUnitIdList)
            {
                peptideSymOpList.Clear();
                chainSymOpList.Clear();
                DataRow[] buGenRows = buGenTable.Select(string.Format ("BiolUnitID = '{0}'", buId));
                foreach (DataRow buGenRow in buGenRows)
                {
                    asymChain = buGenRow["AsymID"].ToString().TrimEnd ();
                    symOp = buGenRow["SymmetryString"].ToString().TrimEnd();
                    if (peptideAsymChains.Contains(asymChain))
                    {
                        if (!peptideSymOpList.Contains(symOp))
                        {
                            peptideSymOpList.Add(symOp);
                        }
                    }
                    else
                    {
                        if (!chainSymOpList.Contains(symOp))
                        {
                            chainSymOpList.Add(symOp);
                        }
                    }
                }
                if (chainSymOpList.Count > 0 && peptideSymOpList.Count > 0)
                {
                    /*      if (peptideSymOpList.Count > 1)  // more than 1 symmetry operators for the peptides 
                          {
                              peptideSymOpInfoLine += (pdbId + "\t" + buId + "\r\n");
                          }
                          else */
                    if (peptideSymOpList.Count != chainSymOpList.Count)
                    {
                        peptideSymOpInfoLine += (pdbId + "\t" + buId + "\r\n");
                        numOfBus++;
                    }
                    else
                    {
                        foreach (string lsSymOp in peptideSymOpList)
                        {
                            if (!chainSymOpList.Contains(lsSymOp))
                            {
                                peptideSymOpInfoLine += (pdbId + "\t" + buId + "\r\n");
                                numOfBus++;
                                break;
                            }
                        }
                    }
                }
            }
            if (peptideSymOpInfoLine != "")
            {
                peptideSymOpInfoLine = pdbId + "\t" + FormatAsymChains(peptideAsymChains) + "\r\n" +
                    peptideSymOpInfoLine;
            }
            return peptideSymOpInfoLine;
        }

        private string FormatAsymChains(string[] asymChains)
        {
            string asymChainString = "";
            foreach (string asymChain in asymChains)
            {
                asymChainString += (asymChain + ",");
            }
            return asymChainString.TrimEnd(',');
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private string[] GetPeptideAsymChains(string pdbId, DataTable asuTable)
        {
            DataRow[] seqRows = asuTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            List<string> peptideAsymChainList = new List<string> ();
            string asymChain = "";
            string sequence = "";
            foreach (DataRow seqRow in seqRows)
            {
                sequence = seqRow["Sequence"].ToString().TrimEnd();
                if (sequence.Length <= 30)
                {
                    asymChain = seqRow["AsymID"].ToString().TrimEnd();
                    peptideAsymChainList.Add(asymChain);
                }
            }

            return peptideAsymChainList.ToArray ();
        }
        public void CountPfamCommonHmmSitesForLogFile ()
        {
            InitializeTables(true);

            // save the interacting sequence numbers and the hmm sequence numbers in the file
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\DomainInterfaceHmmSites_log.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfamPeptide\\HmmSitesCompNoCommon_log.txt"), true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "HMM interacting sites";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common HMM interacting sites between pfam-peptide and pfam-pfam interfaces");

     //       Hashtable pepDomainRelInterfaceHash = ReadPepDomainRelationInterfacesFromFile();
            Dictionary<int, Dictionary<int, List<string>>> pepDomainRelInterfaceHash = new Dictionary<int, Dictionary<int, List<string>>>();
            Dictionary<int, List<string>> relDomainInterfaceHash = new Dictionary<int,List<string>> ();
            List<string> domainInterfaceList1 = new List<string> ();
            domainInterfaceList1.Add("4a9232");
            relDomainInterfaceHash.Add(6057, domainInterfaceList1);
            pepDomainRelInterfaceHash.Add(6974, relDomainInterfaceHash);

            string pfamId = "";
            foreach (int pepRelSeqId in pepDomainRelInterfaceHash.Keys)
            {
         //       relPfamPair = GetPfamIds(pepRelSeqId);
         //       pfamId = relPfamPair[0];
                pfamId = GetPfamIdFromPeptideInterface(pepRelSeqId);
                foreach (int domainRelSeqId in pepDomainRelInterfaceHash[pepRelSeqId].Keys)
                {
                    string[] domainInterfaces = pepDomainRelInterfaceHash[pepRelSeqId][domainRelSeqId].ToArray(); 
                    try
                    {
                        ComparePeptideToDomainInterfacesForDebug(pepRelSeqId, domainRelSeqId, pfamId, domainInterfaces);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepRelSeqId.ToString () + " " + domainRelSeqId.ToString () + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pepRelSeqId.ToString() + " " + domainRelSeqId.ToString() + " error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        private void ComparePeptideToDomainInterfacesForDebug (int pepRelSeqId, int domainRelSeqId, string pfamId, string[] relDomainInterfaces)
        {
            DataTable pepDomainInterfaceTable = GetPeptideDomainInterfaceTable(pepRelSeqId);
            DataTable domainInterfaceTable = GetRelDomainInterfaceTable(domainRelSeqId, relDomainInterfaces);
            ComparePeptideToDomainInterfaces(pepRelSeqId, domainRelSeqId, pfamId, pepDomainInterfaceTable, domainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, Dictionary<int, List<string>>> ReadPepDomainRelationInterfacesFromFile()
        {
            Dictionary<int, Dictionary<int, List<string>>> pepDomainRelInterfaceHash = new Dictionary<int,Dictionary<int,List<string>>> ();
            string line = "";
            int pepRelSeqId = 0;
            int domainRelSeqId = 0;
            StreamReader dataReader = new StreamReader("InterfaceDbLog_debug0.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("get interacting hmm sites errors") > -1)
                {
                    string[] fields = line.Split(' ');
                    pepRelSeqId = Convert.ToInt32(fields[0]);
                    domainRelSeqId = Convert.ToInt32(fields[1]);
                    if (pepDomainRelInterfaceHash.ContainsKey(pepRelSeqId))
                    {
                        if (pepDomainRelInterfaceHash[pepRelSeqId].ContainsKey(domainRelSeqId))
                        {
                            if (!pepDomainRelInterfaceHash[pepRelSeqId][domainRelSeqId].Contains(fields[3]))
                            {
                                pepDomainRelInterfaceHash[pepRelSeqId][domainRelSeqId].Add(fields[3]);
                            }
                        }
                        else
                        {
                            List<string> interfaceList = new List<string> ();
                            interfaceList.Add(fields[3]);
                            pepDomainRelInterfaceHash[pepRelSeqId].Add(domainRelSeqId, interfaceList);
                        }
                    }
                    else
                    {
                        Dictionary<int, List<string>> domainRelInterfaceHash = new Dictionary<int,List<string>> ();
                        List<string> interfaceList = new List<string> ();
                        interfaceList.Add(fields[3]);
                        domainRelInterfaceHash.Add(domainRelSeqId, interfaceList);
                        pepDomainRelInterfaceHash.Add(pepRelSeqId, domainRelInterfaceHash);
                    }
                }
            }
            dataReader.Close();
            return pepDomainRelInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearDuplicateRecords()
        {
            string queryString = "Select Distinct RelSeqID1, RelSeqID2 From PfamInterfaceHmmSiteComp;";
            DataTable relationPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            int pepRelSeqId = 0;
            int domainRelSeqId = 0;
            foreach (DataRow relPairRow in relationPairTable.Rows)
            {
                pepRelSeqId = Convert.ToInt32(relPairRow["RelSeqID1"].ToString ());
                domainRelSeqId = Convert.ToInt32(relPairRow["RelSeqID2"].ToString ());
                ClearPepDomainInterfaceCompRows(pepRelSeqId, domainRelSeqId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        /// <returns></returns>
        private void ClearPepDomainInterfaceCompRows(int pepRelSeqId, int domainRelSeqId)
        {
            string queryString = string.Format("Select Distinct * From PfamInterfaceHmmSiteComp Where RelSeqId1 = {0} AND RelSeqID2 = {1};", pepRelSeqId, domainRelSeqId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            hmmSiteCompTable.TableName = "PfamInterfaceHmmSiteComp";
            DeleteRelationPairData(pepRelSeqId, domainRelSeqId);
            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, hmmSiteCompTable);
            hmmSiteCompTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="domainRelSeqId"></param>
        private void DeleteRelationPairData(int pepRelSeqId, int domainRelSeqId)
        {
            string deleteString = string.Format("Delete From PfamInterfaceHmmSiteComp Where RelSeqID1 = {0} AND RelSeqID2 = {1};", pepRelSeqId, domainRelSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        public void DeleteHmmSiteCompWithPfam()
        {
            string queryString = "Select Distinct PdbId, DomainInterfaceId From PfamPeptideInterfaces Where PepDomainId > -1;";
            DataTable pfamPeptideInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in pfamPeptideInterfaceIdTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString ());
                DeleteHmmSiteComp(pdbId, domainInterfaceId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeleteHmmSiteComp(string pdbId, int domainInterfaceId)
        {
            string deleteString = string.Format("Delete From PfamInterfaceHmmSiteComp Where PdbId1 = '{0}' AND DomainInterfaceId1 = {1};", pdbId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion

    }
}
