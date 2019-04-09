using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using PfamLib.PfamArch;
using AuxFuncLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.DomainInterfaces;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamChainInterfaceHmmSites : PfamHmmSites
    {
        #region member variables
        private PfamArchitecture pfamArch = new PfamArchitecture();
        private string chainInterfaceFileDir = "";
        private double redundantQscoreCutoff = 0.85;
        #endregion

        #region common hmm sites between peptide and chain interfaces
        /// <summary>
        /// 
        /// </summary>
        public void CountPfamPepChainInterfaceHmmSites()
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            bool isUpdate = false;
            InitializeTables(isUpdate);
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites.txt"), true);

            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where CrystalPack = '0';";
            DataTable peptideRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] allRelSeqIds = GetRelSeqIds(peptideRelSeqIdTable);

            int[] relSeqIds = GetLeftPepSeqIds(allRelSeqIds);
          //  string pfamId = "Pkinase";
         //   int[] relSeqIds = GetPfamPepRelSeqIDs(pfamId, allRelSeqIds);
          //  int[] relSeqIds = {912};

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites between peptide and chain interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#Peptide PFAMs: " + relSeqIds.Length.ToString());

            int pepPfamCount = 0;
            foreach (int relSeqId in relSeqIds)
            {
                pepPfamCount++;
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepPfamCount.ToString() + ":  " + relSeqId.ToString());
                try
                {
                    CountPfamPepChainInterfaceHmmSites(relSeqId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " count hmm positions of peptide and chain interfaces errors : " + ex.Message);
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CountPfamPepChainInterfaceHmmSites(int pepRelSeqId)
        {
            string pfamId = GetPfamIdFromPeptideInterface(pepRelSeqId);
            int[] groupSeqIds = GetPfamRelatedChainGroups(pfamId);  // the super groups in which contain pfam arch with pfam
            DataTable pepDomainInterfacesTable = GetPeptideDomainInterfaceTable(pepRelSeqId);

            foreach (int groupSeqId in groupSeqIds)
            {
         //       string[] groupRepChainInterfaces = GetRepChainInterfaces(groupSeqId, pfamId);
                string[] groupChainInterfaces = GetGroupChainInterfaces(groupSeqId);
                ComparePeptideToChainInterfaces(pepRelSeqId, groupSeqId, pfamId, pepDomainInterfacesTable, groupChainInterfaces);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="chainGroupId"></param>
        /// <param name="pfamId"></param>
        /// <param name="pepDomainInterfacesTable"></param>
        /// <param name="chainInterface"></param>
        private void ComparePeptideToChainInterfaces(int pepRelSeqId, int chainGroupId, string pfamId, DataTable pepDomainInterfacesTable, string[] chainInterfaces)
        {
            string[] pepDomainInterfaces = GetDomainInterfaceIds(pepDomainInterfacesTable);
     //       Dictionary<string, int[]> entryInterfaceIdHash = GetChainInterfaceEntries(chainInterfaces);

            ComparePeptideToChainInterfaces(pepRelSeqId, chainGroupId, pfamId, chainInterfaces, pepDomainInterfaces, pepDomainInterfacesTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="chainGroupId"></param>
        /// <param name="pfamId"></param>
        /// <param name="chainInterfaces"></param>
        /// <param name="pepDomainInterfaces"></param>
        /// <param name="pepDomainInterfaceTable"></param>
        private void ComparePeptideToChainInterfaces(int pepRelSeqId, int chainGroupId, string pfamId, string[] chainInterfaces,
            string[] pepDomainInterfaces, DataTable pepDomainInterfacesTable)
        {
            string pepPdbId = "";
            int pepDomainInterfaceId = 0;
            long pepDomainId = 0;  // it is domain id for the domain in domain-peptide interface, not the domain id for the peptide
            int[] pepHmmInteractingSites = null;
            string pepHmmSeqNumbers = "";
            Dictionary<long, int[]>[] interfaceHmmInteractingSitesHashes = null;
            Dictionary<long, int[]> interfaceHmmInteractingSitesHashA = null;
            Dictionary<long, int[]> interfaceHmmInteractingSitesHashB = null;
            string dataLine = "";
            string chainInterface = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = pepDomainInterfaces.Length * chainInterfaces.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pepDomainInterfaces.Length * chainInterfaces.Length;

            Dictionary<string, int[]> entryInterfaceIdHash = GetChainInterfaceEntries(chainInterfaces);

            foreach (string chainPdbId in entryInterfaceIdHash.Keys)
            {
                DataTable pfamDomainTable = GetPfamDomainTable(chainPdbId, pfamId);
                Dictionary<string, Dictionary<long, Range[]>> entityDomainRangeHash = GetEntityPfamRanges(chainPdbId, pfamDomainTable);

                int[] chainInterfaceIds = (int[])entryInterfaceIdHash[chainPdbId];
                foreach (int chainInterfaceId in chainInterfaceIds)
                {
                    try
                    {
                        interfaceHmmInteractingSitesHashes = GetChainInterfaceHmmInteractingSites(chainPdbId, chainInterfaceId, pfamId, entityDomainRangeHash, pfamDomainTable);
                        // [0]: from the chain A in the chain interface
                        // [1]: from the chain B in the chain interface
                        interfaceHmmInteractingSitesHashA = interfaceHmmInteractingSitesHashes[0];
                        interfaceHmmInteractingSitesHashB = interfaceHmmInteractingSitesHashes[1];
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepRelSeqId.ToString() + " " + chainGroupId.ToString() + " " +
                                    chainPdbId + chainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pepRelSeqId.ToString() + " " + chainGroupId.ToString() + " " +
                                    chainPdbId + chainInterfaceId.ToString() + " get interacting hmm sites errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                    if (interfaceHmmInteractingSitesHashes == null)
                    {
                        continue;
                    }
                    chainInterface = chainPdbId + "_" + chainInterfaceId.ToString();
                    foreach (string pepDomainInterface in pepDomainInterfaces)
                    {
                        ProtCidSettings.progressInfo.currentFileName = pepDomainInterface + "_" + chainInterface;
                        ProtCidSettings.progressInfo.currentOperationNum++;
                        ProtCidSettings.progressInfo.currentStepNum++;

                        pepPdbId = pepDomainInterface.Substring(0, 4);
                        pepDomainInterfaceId = Convert.ToInt32(pepDomainInterface.Substring(4, pepDomainInterface.Length - 4));

                        DataRow[] pepDomainInterfaceRows = pepDomainInterfacesTable.Select
                            (string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pepPdbId, pepDomainInterfaceId));

                        pepDomainId = Convert.ToInt64(pepDomainInterfaceRows[0]["DomainID"].ToString());
                        pepHmmInteractingSites = GetPeptideHmmInteractingSites(pepPdbId, pepDomainInterfaceId, out pepHmmSeqNumbers);

                        foreach (long domainId in interfaceHmmInteractingSitesHashA.Keys)
                        {
                            int[] interactingHmmSites = (int[])interfaceHmmInteractingSitesHashA[domainId];
                            if (interactingHmmSites != null && interactingHmmSites.Length > 0)
                            {
                                int numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSites, interactingHmmSites);
                                if (numOfCommonHmmSites > 0)
                                {
                                    DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                                    hmmSiteRow["PfamId"] = pfamId;
                                    hmmSiteRow["RelSeqID1"] = pepRelSeqId;
                                    hmmSiteRow["PdbID1"] = pepPdbId;
                                    hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceId;
                                    hmmSiteRow["DomainID1"] = pepDomainId;
                                    hmmSiteRow["RelSeqID2"] = chainGroupId;
                                    hmmSiteRow["PdbID2"] = chainPdbId;
                                    hmmSiteRow["DomainInterfaceID2"] = chainInterfaceId;
                                    hmmSiteRow["DomainId2"] = domainId;
                                    hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                                    hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSites.Length;
                                    hmmSiteRow["NumOfHmmSites2"] = interactingHmmSites.Length;
                                    hmmSiteRow["ChainNo"] = "A";
                                    hmmSiteRow["ChainRmsd"] = -1;
                                    hmmSiteRow["InteractChainRmsd"] = -1;
                                    hmmSiteRow["PepRmsd"] = -1;
                                    hmmSiteRow["InteractPepRmsd"] = -1;
                                    hmmSiteRow["LocalPepRmsd"] = -1;
                                    hmmSiteRow["PepStart"] = -1;
                                    hmmSiteRow["PepEnd"] = -1;
                                    hmmSiteRow["ChainStart"] = -1;
                                    hmmSiteRow["ChainEnd"] = -1;
                                    hmmSiteRow["PepAlignment"] = "-";
                                    hmmSiteRow["ChainAlignment"] = "-";
                                    hmmSiteRow["Score"] = -1;
                                    pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                                }
                                else
                                {
                                    dataLine = pfamId + "\t" + pepRelSeqId.ToString() + "\t" + pepPdbId + "\t" + pepDomainInterfaceId.ToString() + "\t" +
                                       pepDomainId.ToString() + "\t" + chainGroupId.ToString() + "\t" + chainPdbId + "\t" +
                                       chainInterfaceId.ToString() + "\t" + domainId.ToString() + "\t" +
                                       FormatSeqNumbers(pepHmmInteractingSites) + "\t" + FormatSeqNumbers(interactingHmmSites) + "\t" + numOfCommonHmmSites.ToString();
                                    noCommonHmmSitesWriter.WriteLine(dataLine);
                                }
                            }
                        }
                        foreach (long domainId in interfaceHmmInteractingSitesHashB.Keys)
                        {
                            int[] interactingHmmSites = (int[])interfaceHmmInteractingSitesHashB[domainId];
                            if (interactingHmmSites != null && interactingHmmSites.Length > 0)
                            {
                                int numOfCommonHmmSites = GetCommonHmmInteractingSites(pepHmmInteractingSites, interactingHmmSites);
                                if (numOfCommonHmmSites > 0)
                                {
                                    DataRow hmmSiteRow = pfamInterfaceHmmSiteTable.NewRow();
                                    hmmSiteRow["PfamId"] = pfamId;
                                    hmmSiteRow["RelSeqID1"] = pepRelSeqId;
                                    hmmSiteRow["PdbID1"] = pepPdbId;
                                    hmmSiteRow["DomainInterfaceID1"] = pepDomainInterfaceId;
                                    hmmSiteRow["DomainID1"] = pepDomainId;
                                    hmmSiteRow["RelSeqID2"] = chainGroupId;
                                    hmmSiteRow["PdbID2"] = chainPdbId;
                                    hmmSiteRow["DomainInterfaceID2"] = chainInterfaceId;
                                    hmmSiteRow["DomainId2"] = domainId;
                                    hmmSiteRow["NumOfCommonHmmSites"] = numOfCommonHmmSites;
                                    hmmSiteRow["NumOfHmmSites1"] = pepHmmInteractingSites.Length;
                                    hmmSiteRow["NumOfHmmSites2"] = interactingHmmSites.Length;
                                    hmmSiteRow["ChainNo"] = "B";
                                    hmmSiteRow["ChainRmsd"] = -1;
                                    hmmSiteRow["InteractChainRmsd"] = -1;
                                    hmmSiteRow["PepRmsd"] = -1;
                                    hmmSiteRow["InteractPepRmsd"] = -1;
                                    hmmSiteRow["LocalPepRmsd"] = -1;
                                    hmmSiteRow["LocalPepRmsd"] = -1;
                                    hmmSiteRow["PepStart"] = -1;
                                    hmmSiteRow["PepEnd"] = -1;
                                    hmmSiteRow["ChainStart"] = -1;
                                    hmmSiteRow["ChainEnd"] = -1;
                                    hmmSiteRow["PepAlignment"] = "-";
                                    hmmSiteRow["ChainAlignment"] = "-";
                                    hmmSiteRow["Score"] = -1;
                                    pfamInterfaceHmmSiteTable.Rows.Add(hmmSiteRow);
                                }
                                else
                                {
                                    dataLine = pfamId + "\t" + pepRelSeqId.ToString() + "\t" + pepPdbId + "\t" + pepDomainInterfaceId.ToString() + "\t" +
                                      pepDomainId.ToString() + "\t" + chainGroupId.ToString() + "\t" + chainPdbId + "\t" +
                                      chainInterfaceId.ToString() + "\t" + domainId.ToString() + "\t" +
                                      FormatSeqNumbers(pepHmmInteractingSites) + "\t" + FormatSeqNumbers(interactingHmmSites) + "\t" + numOfCommonHmmSites.ToString();
                                    noCommonHmmSitesWriter.WriteLine(dataLine);
                                }
                            }
                        }
                    }
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamInterfaceHmmSiteTable);
                pfamInterfaceHmmSiteTable.Clear();
                domainHmmInteractingSitesWriter.Flush();
                noCommonHmmSitesWriter.Flush();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamRelatedChainGroups(string pfamId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamArch Like '%({0})%';", pfamId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] groupSeqIds = new int[groupSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow seqIdRow in groupSeqIdTable.Rows)
            {
                groupSeqIds[count] = Convert.ToInt32(seqIdRow["SuperGroupSeqID"].ToString ());
                count++;
            }
            return groupSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamDomainTable(string pdbId, string pfamId)
        {
            string queryString = string.Format("Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, AlignStart, AlignEnd, HmmStart, HmmEnd, QueryAlignment, HmmAlignment " + 
                " From PdbPfam Where Pfam_ID = '{0}' AND PdbID = '{1}';", pfamId, pdbId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaces"></param>
        /// <returns></returns>
        private Dictionary<string, int[]> GetChainInterfaceEntries(string[] chainInterfaces)
        {
            Dictionary<string, List<int>> entryInterfaceIdListHash = new Dictionary<string,List<int>> ();
            string pdbId = "";
            int interfaceId = 0;
            foreach (string chainInterface in chainInterfaces)
            {
                string[] fields = chainInterface.Split('_');
                if (fields.Length == 2)
                {
                    pdbId = fields[0];
                    interfaceId = Convert.ToInt32(fields[1]);
                }
                else
                {
                    pdbId = chainInterface.Substring(0, 4);
                    interfaceId = Convert.ToInt32(chainInterface.Substring (4, chainInterface.Length - 4));
                }
                if (entryInterfaceIdListHash.ContainsKey(pdbId))
                {
                    entryInterfaceIdListHash[pdbId].Add(interfaceId);
                }
                else
                {
                    List<int> interfaceIdList = new List<int> ();
                    interfaceIdList.Add(interfaceId);
                    entryInterfaceIdListHash.Add(pdbId, interfaceIdList);
                }
            }
            Dictionary<string, int[]> entryInterfaceIdHash = new Dictionary<string, int[]>();
            foreach (string lsPdbId in entryInterfaceIdListHash.Keys)
            {
                entryInterfaceIdHash.Add (lsPdbId, entryInterfaceIdListHash[lsPdbId].ToArray());
            }
            return entryInterfaceIdHash;
        }
        #endregion

        #region update peptide and chain comparison
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateCountPfamChainInterfaceHmmSites(string[] updateEntries)
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            bool isUpdate = true;
            InitializeTables(isUpdate);
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites.txt"), true);

            Dictionary<int, List<string>> relUpdateEntryHash = GetPepRelUpdateEntryHash(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites between peptide and chain interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#Peptide PFAMs: " + relUpdateEntryHash.Count.ToString());

            int pepPfamCount = 0;
            List<int> updateRelSeqIdList = new List<int> (relUpdateEntryHash.Keys );
            updateRelSeqIdList.Sort();
            foreach (int relSeqId in updateRelSeqIdList)
            {
                pepPfamCount++;
                string[] relUpdateEntries = relUpdateEntryHash[relSeqId].ToArray();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepPfamCount.ToString() + ":  " + relSeqId.ToString());
                try
                {
                    UpdateCountPfamPepChainInterfaceHmmSites(relSeqId, relUpdateEntries);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " count hmm positions of peptide and chain interfaces errors : " + ex.Message);
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetPepRelUpdateEntryHash(string[] entries)
        {
            Dictionary<int, List<string>> relUpdateEntryHash = new Dictionary<int,List<string>> ();
            foreach (string pdbId in entries)
            {
                int[] entryPepRelSeqIds = GetEntryPepRelSeqIds (pdbId);
                foreach (int relSeqId in entryPepRelSeqIds)
                {
                    if (relUpdateEntryHash.ContainsKey(relSeqId))
                    {
                        relUpdateEntryHash[relSeqId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        relUpdateEntryHash.Add(relSeqId, entryList);
                    }
                }
            }
            return relUpdateEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryPepRelSeqIds (string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamPeptideInterfaces Where PdbID = '{0}' AND CrystalPack = '0';", pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (relSeqIdTable.Rows.Count == 0)
            {
                int[] pepRelSeqIds = GetNoPepEntryPepRelSeqIds(pdbId);
                return pepRelSeqIds;
            }
            else
            {
                int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
                int count = 0;
                foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
                {
                    relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                    count++;
                }
                return relSeqIds;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetNoPepEntryPepRelSeqIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            List<int> relSeqIdList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                queryString = string.Format("Select Distinct RelSeqID From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
                DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (relSeqIdTable.Rows.Count > 0)
                {
                    relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
                    relSeqIdList.Add(relSeqId);
                }
            }

            return relSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void UpdateCountPfamPepChainInterfaceHmmSites(int pepRelSeqId, string[] updateEntries)
        {
            string pfamId = GetPfamIdFromPeptideInterface(pepRelSeqId);
            int[] groupSeqIds = GetPfamRelatedChainGroups(pfamId);  // the super groups in which contain pfam arch with pfam
            DataTable pepDomainInterfacesTable = GetPeptideDomainInterfaceTable(pepRelSeqId);

            foreach (int groupSeqId in groupSeqIds)
            {
                UpdateComparePeptideToChainInterfaces(pepRelSeqId, groupSeqId, pfamId, pepDomainInterfacesTable, updateEntries);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="chainGroupId"></param>
        /// <param name="pfamId"></param>
        /// <param name="pepDomainInterfacesTable"></param>
        /// <param name="chainInterface"></param>
        private void UpdateComparePeptideToChainInterfaces(int pepRelSeqId, int chainGroupId, string pfamId, DataTable pepDomainInterfacesTable, string[] updateEntries)
        {
            string[] groupChainInterfaces = GetGroupChainInterfaces(chainGroupId);
            string[] updateChainInterfaces = GetUpdateInterfaces(groupChainInterfaces, updateEntries);

            string[] pepDomainInterfaces = GetDomainInterfaceIds(pepDomainInterfacesTable);
            string[] updateDomainInterfaces = GetUpdateInterfaces(pepDomainInterfaces, updateEntries);

            // delete data rows containing updated entries
            DeleteObsoleteDataRows(pepRelSeqId, chainGroupId, pfamId, updateEntries);

            // compare all updated chain interfaces to related peptide interfaces
            ComparePeptideToChainInterfaces(pepRelSeqId, chainGroupId, pfamId, updateChainInterfaces, pepDomainInterfaces, pepDomainInterfacesTable);

            // compare non-updated chain interfaces to updated peptide interfaces
            // exclude those chain interfaces which are already compared
            string[] nonUpdateChainInterfaces = GetNonUpdateInterfaces(groupChainInterfaces, updateEntries);
            ComparePeptideToChainInterfaces(pepRelSeqId, chainGroupId, pfamId, nonUpdateChainInterfaces, updateDomainInterfaces, pepDomainInterfacesTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaces"></param>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetUpdateInterfaces(string[] interfaces, string[] updateEntries)
        {
            List<string> updateInterfaceList = new List<string> ();
            foreach (string thisInterface in interfaces)
            {
                if (updateEntries.Contains (thisInterface.Substring (0, 4)))
                {
                    updateInterfaceList.Add (thisInterface );
                }
            }

            return updateInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaces"></param>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetNonUpdateInterfaces(string[] interfaces, string[] updateEntries)
        {
            List<string> nonUpdateInterfaceList = new List<string> ();
            foreach (string thisInterface in interfaces)
            {
                if (! updateEntries.Contains(thisInterface.Substring(0, 4)))
                {
                    nonUpdateInterfaceList.Add(thisInterface);
                }
            }
            return nonUpdateInterfaceList.ToArray ();
        }

        /// <summary>
        /// delete the data rows containing updated entries
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="chainGroupId"></param>
        /// <param name="pfamId"></param>
        /// <param name="updateEntries"></param>
        private void DeleteObsoleteDataRows(int pepRelSeqId, int chainGroupId, string pfamId, string[] updateEntries)
        {
            string deleteString = "";
            foreach (string pdbId in updateEntries)
            {
                deleteString = string.Format("Delete From PfamChainInterfaceHmmSiteComp " + 
                    " Where PfamId = '{0}' AND RelSeqID1 = {1} AND RelSeqID2 = {2} AND (PdbID1 = '{3}' OR PdbID2 = '{3}'); ", 
                    pfamId, pepRelSeqId, chainGroupId, pdbId);
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }
        #endregion

        #region RelSeqIds for the input pfam
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pepRelSeqIDs"></param>
        /// <returns></returns>
        private int[] GetPfamPepRelSeqIDs(string pfamId, int[] pepRelSeqIDs)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            List<int> pepRelSeqIdList = new List<int> ();
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                if (pepRelSeqIDs.Contains(relSeqId))
                {
                    pepRelSeqIdList.Add(relSeqId);
                }
            }
            return pepRelSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqIds"></param>
        /// <returns></returns>
        private int[] GetLeftPepSeqIds(int[] pepRelSeqIds)
        {
            string queryString = "Select Distinct RelSeqID1 From PfamChainInterfaceHmmSiteComp";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> leftRelSeqIdList = new List<int> (pepRelSeqIds);
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID1"].ToString ());
                leftRelSeqIdList.Remove(relSeqId);
            }
            return leftRelSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqIds"></param>
        /// <returns></returns>
        private int[] GetLeftPepSeqIds(int[] pepRelSeqIds, int[] relSeqIdsNotComp)
        {
            string queryString = "Select Distinct RelSeqID1 From PfamChainInterfaceHmmSiteComp";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> leftRelSeqIdList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID1"].ToString());
                if (!relSeqIdsNotComp.Contains(relSeqId))
                {
                    leftRelSeqIdList.Add(relSeqId);
                }
            }
            return leftRelSeqIdList.ToArray ();
        }
        #endregion

        #region pfam hmm sites from chain interface
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="pfamId"></param>
        /// <param name="entityDomainRangeHash"></param>
        /// <param name="domainPfamTable"></param>
        /// <returns></returns>
        private Dictionary<long, int[]>[] GetChainInterfaceHmmInteractingSites(string pdbId, int interfaceId, string pfamId, Dictionary<string, Dictionary<long, Range[]>> entityDomainRangeHash, DataTable domainPfamTable)
        {
            string chainInterfaceFile = Path.Combine(chainInterfaceFileDir, pdbId.Substring(1, 2) + "\\" + pdbId + "_" + interfaceId.ToString() + ".cryst.gz");
            if (!File.Exists(chainInterfaceFile))
            {
                ProtCidSettings.logWriter.WriteLine(pdbId + "_" + interfaceId.ToString() + " chain interface file not exist.");
                return null;
            }
            InterfaceChains chainInterface = null;
            string hashFolder = Path.Combine(chainInterfaceFileDir, pdbId.Substring(1, 2));
            try
            {
                chainInterface = interfaceReader.GetInterfaceFromFile(hashFolder, pdbId, interfaceId, "cryst");
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine(pdbId + "_" + interfaceId.ToString() + " retrieving chain interface file error: " + ex.Message);
                return null;
            }

            if (chainInterface == null)
            {
                ProtCidSettings.logWriter.WriteLine(pdbId + interfaceId.ToString() +
                                    " Can not get the chain interface from the file, most likely cannot get the contacts hash");
                return null;
            }

            int chainNo = 1;
            int[] interactingSeqIds1 = null;
            int[] interactingHmmSeqIds1 = null;
            int[] interactingSeqIds2 = null;
            int[] interactingHmmSeqIds2 = null;
            Dictionary<long, int[]> domainInteractingHmmSitesHash1 = new Dictionary<long,int[]> ();
            Dictionary<long, int[]> domainInteractingHmmSitesHash2 = new Dictionary<long,int[]> ();
            string dataLine = "";
            string entryEntity = pdbId + chainInterface.entityId1.ToString();
            // the chain A contains pfam domain
            if (entityDomainRangeHash.ContainsKey(entryEntity))
            {
                chainNo = 1;
                Dictionary<long, Range[]> domainRangesHash = entityDomainRangeHash[entryEntity];
                foreach (long domainId in domainRangesHash.Keys)
                {
                    Range[] domainRanges = (Range[])domainRangesHash[domainId];
                    interactingSeqIds1 = GetInteractingSeqNumbers(chainInterface, domainRanges, chainNo);
                    interactingHmmSeqIds1 = MapSingleDomainInteractingSeqNumbersToHmm(pdbId, domainId, interactingSeqIds1, domainPfamTable);

                    dataLine = pdbId + "\t" + interfaceId.ToString() + "\t" + domainId.ToString() + "\t" +
                       FormatSeqNumbers(interactingSeqIds1) + "\t" + FormatSeqNumbers(interactingHmmSeqIds1) + "\t" + chainNo.ToString();
                    domainHmmInteractingSitesWriter.WriteLine(dataLine);

                    domainInteractingHmmSitesHash1.Add(domainId, interactingHmmSeqIds1);
                }
            }
            entryEntity = pdbId + chainInterface.entityId2.ToString();
            // chain B contains pfam domain
            if (entityDomainRangeHash.ContainsKey (entryEntity))
            {
                chainNo = 2;
                foreach (long domainId in entityDomainRangeHash[entryEntity].Keys)
                {
                    Range[] domainRanges = entityDomainRangeHash[entryEntity][domainId];
                    interactingSeqIds2 = GetInteractingSeqNumbers(chainInterface, domainRanges, chainNo);
                    interactingHmmSeqIds2 = MapSingleDomainInteractingSeqNumbersToHmm(pdbId, domainId, interactingSeqIds2, domainPfamTable);

                    dataLine = pdbId + "\t" + interfaceId.ToString() + "\t" + domainId.ToString() + "\t" +
                             FormatSeqNumbers(interactingSeqIds2) + "\t" + FormatSeqNumbers(interactingHmmSeqIds2) + "\t" + chainNo.ToString();
                    domainHmmInteractingSitesWriter.WriteLine(dataLine);

                    domainInteractingHmmSitesHash2.Add(domainId, interactingHmmSeqIds2);
                }
            }

            Dictionary<long, int[]>[] domainInteractingHmmSitesHashes = new Dictionary<long, int[]>[2];
            domainInteractingHmmSitesHashes[0] = domainInteractingHmmSitesHash1;
            domainInteractingHmmSitesHashes[1] = domainInteractingHmmSitesHash2;
            return domainInteractingHmmSitesHashes;
          
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRangeHash"></param>
        /// <param name="chainNo"></param>
        /// <returns></returns>
        private int[] GetInteractingSeqNumbers(InterfaceChains chainInterface, Range[] domainRanges, int chainNo)
        {
            Dictionary<string, Contact> contactHash = chainInterface.seqContactHash;
            List<int> interactingSeqIdList = new List<int> ();
            int seqId = 0;
            foreach (string seqPair in contactHash.Keys)
            {
                string[] seqPairFields = seqPair.Split('_');
                if (chainNo == 1)
                {
                    seqId = Convert.ToInt32(seqPairFields[0]);
                    if (IsSeqIdInRanges(seqId, domainRanges))
                    {
                        if (!interactingSeqIdList.Contains(seqId))
                        {
                            interactingSeqIdList.Add(seqId);
                        }
                    }
                }
                else if (chainNo == 2)
                {
                    seqId = Convert.ToInt32(seqPairFields[1]);
                    if (IsSeqIdInRanges(seqId, domainRanges))
                    {
                        if (!interactingSeqIdList.Contains(seqId))
                        {
                            interactingSeqIdList.Add(seqId);
                        }
                    }
                }
            }
            return interactingSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private bool IsSeqIdInRanges(int seqId, Range[] domainRanges)
        {
            foreach (Range range in domainRanges)
            {
                if (seqId <= range.endPos && seqId >= range.startPos)
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
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<long, Range[]>> GetEntityPfamRanges(string pdbId, DataTable pfamDomainTable)
        {
            DataRow[] entryDomainRows = pfamDomainTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            Dictionary<string, Dictionary<long, List<Range>>> entityDomainRangListHash = new Dictionary<string, Dictionary<long, List<Range>>>();
            string entryEntity = "";
            long domainId = 0;
            foreach (DataRow domainRow in entryDomainRows)
            {
                entryEntity = pdbId + domainRow["EntityID"].ToString();
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                Range range = new Range();
                range.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                range.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                if (entityDomainRangListHash.ContainsKey(entryEntity))
                {
                    if (entityDomainRangListHash[entryEntity].ContainsKey(domainId))
                    {
                        entityDomainRangListHash[entryEntity][domainId].Add(range);
                    }
                    else
                    {
                        List<Range> rangeList = new List<Range> ();
                        rangeList.Add(range);
                        entityDomainRangListHash[entryEntity].Add(domainId, rangeList);
                    }
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(range);
                    Dictionary<long, List<Range>> domainRangeHash = new Dictionary<long,List<Range>> ();
                    domainRangeHash.Add(domainId, rangeList);
                    entityDomainRangListHash.Add(entryEntity, domainRangeHash);
                }
            }
            Dictionary<string, Dictionary<long, Range[]>> entityDomainRangeHash = new Dictionary<string, Dictionary<long, Range[]>>();
            foreach (string rangeKey in entityDomainRangListHash.Keys)
            {
                Dictionary<long, Range[]> domainRangeHash = new Dictionary<long, Range[]>();
                foreach (long keyDomainId in entityDomainRangeHash[rangeKey].Keys)
                {                  
                    domainRangeHash.Add (keyDomainId, entityDomainRangeHash[rangeKey][keyDomainId].ToArray ());                    
                }
                entityDomainRangeHash.Add(rangeKey, domainRangeHash);
            }
            return entityDomainRangeHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<long, Range[]>> GetEntityPfamRanges(string pdbId, string pfamId)
        {
            string queryString = string.Format("Select PdbID, EntityID, DomainID, SeqStart, SeqEnd From PdbPfam Where PdbID = '{0}' AND Pfam_ID = '{1}';", pdbId, pfamId);
            DataTable entryEntityPfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, Dictionary<long, Range[]>> entityDomainRangeHash = GetEntityPfamRanges(pdbId, entryEntityPfamDomainTable);
            return entityDomainRangeHash;
        }
        #endregion

        #region representative interfaces for a relation
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetRepChainInterfaces(int superGroupSeqId)
        {
            string[] repClusterInterfaces = GetRepresentativeChainInterfaces(superGroupSeqId);
            string[] chainInterfacesNotInClusters = GetChainInterfacesNotInClusters(superGroupSeqId);
            string[] groupRepChainInterfaces = new string[repClusterInterfaces.Length + chainInterfacesNotInClusters.Length];
            Array.Copy(repClusterInterfaces, 0, groupRepChainInterfaces, 0, groupRepChainInterfaces.Length);
            Array.Copy(chainInterfacesNotInClusters, 0, groupRepChainInterfaces, repClusterInterfaces.Length, chainInterfacesNotInClusters.Length);
            return groupRepChainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetGroupChainInterfaces(int superGroupSeqId)
        {
            string[] groupClusterInterfaces = GetGroupClusterChainInterfaces(superGroupSeqId);
            string[] chainInterfacesNotInClusters = GetChainInterfacesNotInClusters(superGroupSeqId);
            string[] groupChainInterfaces = new string[groupClusterInterfaces.Length + chainInterfacesNotInClusters.Length];
            Array.Copy(groupClusterInterfaces, 0, groupChainInterfaces, 0, groupClusterInterfaces.Length);
            Array.Copy(chainInterfacesNotInClusters, 0, groupChainInterfaces, groupClusterInterfaces.Length, chainInterfacesNotInClusters.Length);
            return groupChainInterfaces;
        }
       
        #region representative interfaces for clusters
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetRepresentativeChainInterfaces(int superGroupSeqId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0}", superGroupSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;

            string[] clusterChainInterfaces = null;
            string clusterRepChainInterface = "";
            List<string> repChainInterfaceList = new List<string> ();
            List<string>  clusterChainInterfaceList = new List<string> ();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                clusterRepChainInterface = GetRepresentativeChainInterface(superGroupSeqId, clusterId, out clusterChainInterfaces);
                repChainInterfaceList.Add(clusterRepChainInterface);
                clusterChainInterfaceList.AddRange(clusterChainInterfaces);
            }
            return repChainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetGroupClusterChainInterfaces(int superGroupSeqId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0}", superGroupSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;

            string[] clusterChainInterfaces = null;
            List<string> clusterChainInterfaceList = new List<string> ();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                clusterChainInterfaces = GetClusterChainInterface(superGroupSeqId, clusterId);
            //    repChainInterfaceList.Add(clusterRepChainInterface);
                clusterChainInterfaceList.AddRange(clusterChainInterfaces);
            }
            return clusterChainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterChainInterface(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0} AND ClusterID = {1}" +
                " Order By PdbID, InterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new  List<string> ();
             List<string>  interfaceList = new  List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_" + interfaceId.ToString());
            }
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepresentativeChainInterface(int relSeqId, int clusterId, out string[] clusterChainInterfaces)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0} AND ClusterID = {1}" + 
                " Order By PdbID, InterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new  List<string> ();
            List<string> interfaceList = new List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_" + interfaceId.ToString());
            }
            clusterChainInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(clusterChainInterfaces);
            double bestQscore = 0;
            double qscore = 0;
            string bestChainInterface = "";
            foreach (string chainInterface in clusterChainInterfaces)
            {
                qscore = GetChainInterfaceQScoreSum(chainInterface, clusterChainInterfaces);
                if (bestQscore < qscore)
                {
                    bestQscore = qscore;
                    bestChainInterface = chainInterface;
                }
            }
            return bestChainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        private double GetChainInterfaceQScoreSum(string chainInterface, string[] clusterInterfaces)
        {
            string[] chainInterfaceInfo = GetChainInterfaceInfo(chainInterface);
            string pdbId = chainInterfaceInfo[0];
            int chainInterfaceId = Convert.ToInt32(chainInterfaceInfo[1]);
            DataTable chainInterfaceCompTable = GetChainInterfaceQscoreTable(pdbId, chainInterfaceId);

            string clusterPdbId = "";
            int clusterChainInterfaceId = 0;
            double qscore = -1;
            double qscoreSum = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] clusterInterfaceInfo = GetChainInterfaceInfo(clusterInterface);
                clusterPdbId = clusterInterfaceInfo[0];
                if (clusterPdbId == pdbId)
                {
                    continue;  // only pick up one domain interface for this entry
                }
                clusterChainInterfaceId = Convert.ToInt32(clusterInterfaceInfo[1]);
                qscore = GetChainInterfaceCompQscore(pdbId,  chainInterfaceId, clusterPdbId, clusterChainInterfaceId, chainInterfaceCompTable);
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
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        private string[] GetChainInterfaceInfo(string chainInterface)
        {
            string chainInterfaceName = chainInterface.Replace(".cryst", "");
            string[] fields = chainInterfaceName.Split('_');
            return fields;
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
        private double GetChainInterfaceCompQscore(string pdbId1, int chainInterfaceId1, string pdbId2, int chainInterfaceId2, DataTable interfaceCompTable)
        {
            double qscore = -1;
            DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND InterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", pdbId1, chainInterfaceId1, pdbId2, chainInterfaceId2));
            if (interfaceCompRows.Length == 0)
            {
                interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND InterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", pdbId2, chainInterfaceId2, pdbId1, chainInterfaceId1));
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
        private DataTable GetChainInterfaceQscoreTable(string pdbId, int chainInterfaceId)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where (PdbID1 = '{0}' AND InterfaceID1 = {1}) OR (PdbID2='{0}' AND InterfaceID2 = {1});", pdbId, chainInterfaceId);
            DataTable interfaceQscoreTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceQscoreTable;
        }
        #endregion

        #region chain interfaces not in clusters
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetChainInterfacesNotInClusters(int superGroupId)
        {
            string[] familyCodes = GetFamilyCodes(superGroupId);
            string[] entriesInSuperGroup = GetSuperGroupEntries(superGroupId);
            List<string> chainInterfacesNotInClusterList = new List<string> ();
            foreach (string pdbId in entriesInSuperGroup)
            {
                int[] interfaceIdsNotInCluster = GetChainInterfacesNotInClusters(superGroupId, pdbId, familyCodes);
                int[] nonReduntInterfaceIdsNotInClusters = GetNonRedundantChainInterfaceIds(pdbId, interfaceIdsNotInCluster);
                foreach (int interfaceId in nonReduntInterfaceIdsNotInClusters)
                {
                    chainInterfacesNotInClusterList.Add(pdbId + "_" + interfaceId.ToString());
                }
            }
            return chainInterfacesNotInClusterList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetChainInterfacesNotInClusters (int superGroupSeqId, string pdbId, string[] familyCodes)
        {
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);

            string queryString = string.Format("Select PdbID, InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            int[] interfaceIdsInClusters = GetChainInterfacesInClusters(superGroupSeqId, pdbId);

            string entityPfamArch1 = "";
            string entityPfamArch2 = "";
            int entityId1 = 0;
            int entityId2 = 0;
            List<int> chainInterfaceIdList = new List<int> ();
            int interfaceId = 0;
            foreach (DataRow chainInterfaceRow in interfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(chainInterfaceRow["InterfaceID"].ToString ());
                if (interfaceIdsInClusters.Contains(interfaceId))
                {
                    continue;
                }
                entityId1 = Convert.ToInt32(chainInterfaceRow["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(chainInterfaceRow["EntityID2"].ToString());
                if (entityPfamArchHash.ContainsKey(entityId1))
                {
                    entityPfamArch1 = entityPfamArchHash[entityId1];
                }
                if (entityId1 == entityId2)
                {
                    entityPfamArch2 = entityPfamArch1;
                }
                else
                {
                    if (entityPfamArchHash.ContainsKey(entityId2))
                    {
                        entityPfamArch2 = entityPfamArchHash[entityId2];
                    }
                }
                if ((entityPfamArch1 == familyCodes[0] && entityPfamArch2 == familyCodes[1]) ||
                    (entityPfamArch1 == familyCodes[1] && entityPfamArch2 == familyCodes[0]))
                {
                    chainInterfaceIdList.Add(Convert.ToInt32(chainInterfaceRow["InterfaceID"].ToString()));
                }
            }
            return chainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetChainInterfacesInClusters(int superGroupSeqId, string pdbId)
        {
            string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperClusterEntryInterfaces " +
              " Where SuperGroupSeqID = {0} AND PdbID = '{1}';", superGroupSeqId, pdbId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] interfaceIdsInClusters = new int[clusterInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                interfaceIdsInClusters[count] = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                count++;
            }
            return interfaceIdsInClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetChainInterfacesInClusters(int superGroupSeqId)
        {
            string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0};", superGroupSeqId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] interfaceIdsInClusters = new string[clusterInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                interfaceIdsInClusters[count] = interfaceRow["PdbID"].ToString () + "_" + interfaceRow["InterfaceID"].ToString();
                count++;
            }
            return interfaceIdsInClusters;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetNonRedundantChainInterfaceIds(string pdbId, int[] chainInterfaceIds)
        {
            // retrieve the redundant domain interfaces for this entry
            string queryString = string.Format("Select * From EntryInterfaceComp " +
                " Where PdbID = '{0}' AND Qscore >= {1} Order By InterfaceID1, InterfaceID2;", pdbId, redundantQscoreCutoff);
            DataTable reduntChainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> nonReduntChainInterfaceIdList = new List<int> (chainInterfaceIds);
            int chainInterfaceId1 = 0;
            int chainInterfaceId2 = 0;
            foreach (DataRow chainInterfaceCompRow in reduntChainInterfaceCompTable.Rows)
            {
                chainInterfaceId1 = Convert.ToInt32(chainInterfaceCompRow["InterfaceID1"].ToString());
                chainInterfaceId2 = Convert.ToInt32(chainInterfaceCompRow["InterfaceID2"].ToString());
                if (chainInterfaceIds.Contains(chainInterfaceId1) && chainInterfaceIds.Contains(chainInterfaceId2))
                {
                    // remove the larger number
                    if (chainInterfaceId2 < chainInterfaceId1)
                    {
                        nonReduntChainInterfaceIdList.Remove(chainInterfaceId1);
                    }
                    else
                    {
                        nonReduntChainInterfaceIdList.Remove(chainInterfaceId2);
                    }
                }
            }
            return nonReduntChainInterfaceIdList.ToArray ();
        }
       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetFamilyCodes(int superGroupId)
        {
            string queryString = string.Format("Select * From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainRelPfamArch = familyCodeTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            string[] familyCodes = new string[2];
            string[] fields = chainRelPfamArch.Split(';');
            if (fields.Length == 1)
            {
                familyCodes[0] = fields[0];
                familyCodes[1] = familyCodes[0];
            }
            else if (fields.Length == 2)
            {
                familyCodes[0] = fields[0];
                familyCodes[1] = fields[1];
            }
            return familyCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetSuperGroupEntries(int superGroupId)
        {
            int[] groupIdsInSuper = GetGroupIdsInSuper(superGroupId);
            List<string> entryList = new List<string> ();
            foreach (int groupId in groupIdsInSuper)
            {
                string[] groupEntries = GetGroupEntries(groupId);
                foreach (string pdbId in groupEntries)
                {
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }

            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupIdsInSuper(int superGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] groupIds = new int[groupIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupIds[count] = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                count++;
            }
            return groupIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetGroupEntries(int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign Where GroupSeqID = {0};", groupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] entries = new string[entryTable.Rows.Count + homoEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                entries[count] = homoEntryRow["PdbID2"].ToString();
                count++;
            }
            return entries;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <returns></returns>
        private bool IsChainInterfaceDerived(string pdbId, int chainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, chainInterfaceId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (peptideInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region initialize table
        private void InitializeTables(bool isUpdate)
        {
            pfamInterfaceHmmSiteTable = new DataTable(interfaceHmmSiteTableName);
            string[] hmmSiteColumns = {"PfamID", "RelSeqID1", "PdbID1", "DomainInterfaceID1", "DomainID1",
                                          "RelSeqID2", "PdbID2", "DomainInterfaceID2", "DomainID2",   // the DomainInterfaceID2 is the chain interface id
                                      "NumOfHmmSites1", "NumOfHmmSites2", "NumOfCommonHmmSites", 
                                      "ChainRmsd", "InteractChainRmsd", "PepRmsd", "InteractPepRmsd", "ChainNo", 
                                      "LocalPepRmsd", "PepStart", "PepEnd", "ChainStart", "ChainEnd", "PepAlignment", "ChainAlignment", "Score"};
            foreach (string col in hmmSiteColumns)
            {
                pfamInterfaceHmmSiteTable.Columns.Add(new DataColumn(col));
            }

            /*      domainInterfaceHmmSiteTable = new DataTable("DomainInterfaceHmmSites");
                  string[] domainHmmSitesColumns = {"PdbID", "RelSeqID", "DomainInterfaceID", "DomainID", "HmmSites", "ChainNO"};
                  foreach (string hmmSiteColumn in domainHmmSitesColumns)
                  {
                      domainInterfaceHmmSiteTable.Columns.Add(new DataColumn (hmmSiteColumn));
                  }
                  */
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
                    " DomainInterfaceID2 INTEGER NOT NULL, " +  // chain interface id
                    " NumOfHmmSites1 INTEGER NOT NULL, " +
                    " NumOfHmmSites2 INTEGER NOT NULL, " +
                    " NumOfCommonHmmSites INTEGER NOT NULL, " + 
                    " ChainRmsd FLOAT, " + 
                    " InteractChainRmsd FLOAT, " + 
                    " PepRmsd FLOAT, " + 
                    " InteractPepRmsd FLOAT, " + 
                    " ChainNO CHAR(1), " +
                    " LocalPepRmsd FLOAT, " + 
                    " PepStart INTEGER, " + 
                    " PepEnd INTEGER, " + 
                    " ChainStart INTEGER, " + 
                    " ChainEnd INTEGER, " + 
                    " PepAlignment VARCHAR(128), " + 
                    " ChainAlignment VARCHAR(128), " + 
                    " Score FLOAT);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, interfaceHmmSiteTableName);

                string createIndexString = "CREATE INDEX PfamChainHmmSites_idx1 ON " + interfaceHmmSiteTableName + "(PdbID1, DomainInterfaceID1);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, interfaceHmmSiteTableName);
                createIndexString = "CREATE INDEX  PfamChainHmmSites_idx2 ON " + interfaceHmmSiteTableName + "(PdbID2, DomainInterfaceID2);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, interfaceHmmSiteTableName);
            }
        }
        #endregion

        #region remove chain interfaces where peptide interfaces are derived
        public void RemovePeptideChainHmmSitesComp()
        {
            string[] pfamIds = {"Pkinase"};
            StreamWriter dataWriter = new StreamWriter("PepChainHmmSiteCompRemoved.txt");
            
            foreach (string pfamId in pfamIds)
            {
                RemovePeptideChainHmmSiteComp(pfamId, dataWriter);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void RemovePeptideChainHmmSiteComp(string pfamId, StreamWriter dataWriter)
        {
            string[] chainInterfacesToBeRemoved = GetSamePepChainInterfaces(pfamId);
            string pdbId = "";
            int interfaceId = 0;
            foreach (string chainInterface in chainInterfacesToBeRemoved)
            {
                pdbId = chainInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(chainInterface.Substring (4, chainInterface.Length - 4));
                WriteHmmSiteCompData(pfamId, pdbId, interfaceId, dataWriter);
                RemoveHmmSiteCompData(pfamId, pdbId, interfaceId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <param name="dataWriter"></param>
        private void WriteHmmSiteCompData(string pfamId, string pdbId, int chainInterfaceId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}' AND PdbID2 = '{1}' AND DomainInterfaceID2 = {2};",
                pfamId, pdbId, chainInterfaceId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataRow[] hmmSiteCompRows = hmmSiteCompTable.Select();
            dataWriter.WriteLine(ParseHelper.FormatDataRows (hmmSiteCompRows));
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        private void RemoveHmmSiteCompData(string pfamId, string pdbId, int chainInterfaceId)
        {
            string deleteString = string.Format("Delete From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}' AND PdbID2 = '{1}' AND DomainInterfaceID2 = {2};",
                pfamId, pdbId, chainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetSamePepChainInterfaces(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID2, DomainInterfaceID2 From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}';", pfamId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int interfaceId = 0;
            List<string> samePepChainInterfaceList = new List<string> ();
            foreach (DataRow chainInterfaceRow in chainInterfaceTable.Rows)
            {
                pdbId = chainInterfaceRow["PdbID2"].ToString();
                interfaceId = Convert.ToInt32(chainInterfaceRow["DomainInterfaceID2"].ToString());

                if (IsChainInterfaceDerived (pdbId, interfaceId))
                {
                    samePepChainInterfaceList.Add(pdbId + interfaceId.ToString());
                }
            }
            return samePepChainInterfaceList.ToArray ();
        }
        #endregion

        #region add those missing interfaces
        /// <summary>
        /// 
        /// </summary>
 /*       public void CountPfamMissingChainInterfaceHmmSites()
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            InitializeTables(true);
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites_pkinaseTyr.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites_pkinaseTyr.txt"), true);

            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces Where CrystalPack = '0';";
            DataTable peptideRelSeqIdTable = dbQuery.Query(queryString);
            int[] allRelSeqIds = GetRelSeqIds(peptideRelSeqIdTable);

            string pfamId = "Pkinase";
            int[] pkinaseRelSeqIds = GetPfamPepRelSeqIDs(pfamId, allRelSeqIds);

            int[] relSeqIds = GetLeftPepSeqIds(allRelSeqIds, pkinaseRelSeqIds);  // all calculations are done for Pkinase

            string noComHmmFile = @"D:\DbProjectData\pfam\PfamPeptide\pepChainComp\ChainInterfacesNoHmmSites.txt";
      //      Hashtable pfamChainInterfacesHash = GetAlreadyParsedChainInterfacesFromFile(noComHmmFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites between peptide and chain interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#Peptide PFAMs: " + relSeqIds.Length.ToString());

            int pepPfamCount = 0;
            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepPfamCount.ToString() + ":  " + relSeqId.ToString());

                try
                {
                    Hashtable pfamChainInterfacesHash = GetAlreadyParsedChainInterfacesFromFile(noComHmmFile, relSeqId);
                    CountPfamMissingChainInterfaceHmmSites(relSeqId, pfamChainInterfacesHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " count hmm positions of peptide and chain interfaces errors : " + ex.Message);
                }
                pepPfamCount++;
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
        }
        */
        /// <summary>
        /// 
        /// </summary>
        public void CountPfamMissingChainInterfaceHmmSites()
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            InitializeTables(true);
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites_missing.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites_missing.txt"), true);

            string[] pfamIds = GetPfamIds();

            string[] pfamIdsDone = {"Pkinase", "Pkinase_Tyr", "V-set"};

       //     string noComHmmFile = @"D:\DbProjectData\pfam\PfamPeptide\ChainInterfacesNoHmmSites.txt";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Count common hmm sites between peptide and chain interfaces");

            int pepPfamCount = 0;
            Dictionary<int, string[]> pfamChainInterfacesHash = new Dictionary<int, string[]> ();
            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId);

                if (pfamIdsDone.Contains(pfamId))
                {
                    continue;
                }

                int[] relSeqIds = GetPfamRelSeqIDs(pfamId);

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("#Peptide PFAM RelSeqIDs: " + relSeqIds.Length.ToString());

                foreach (int relSeqId in relSeqIds)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pepPfamCount.ToString() + ":  " + relSeqId.ToString());
                    pepPfamCount++;
                   
                    try
                    {
                //        CountPfamMissingChainInterfaceHmmSites(relSeqId);
                        CountPfamMissingChainInterfaceHmmSites(relSeqId, pfamChainInterfacesHash);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " count hmm positions of peptide and chain interfaces errors : " + ex.Message);
                    }
                    
                } 
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPfamIds()
        {
            string queryString = "Select Distinct PfamID From PfamChainInterfaceHmmSiteComp;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamIds[count] = pfamIdRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamRelSeqIDs(string pfamId)
        {
            string queryString = string.Format("Select Distinct RelSeqID1 From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID1"].ToString ());
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CountPfamMissingChainInterfaceHmmSites(int relSeqId, Dictionary<int, string[]> pfamParseChainInterfaceHash)
        {
            string pfamId = GetPfamIdFromPeptideInterface(relSeqId);
            int[] groupSeqIds = GetPfamRelatedChainGroups(pfamId);  // the super groups in which contain pfam arch with pfam
            DataTable pepDomainInterfacesTable = GetPeptideDomainInterfaceTable(relSeqId);
            string[] noHmmChainInterfaces = new string[0];
            if (pfamParseChainInterfaceHash.ContainsKey(relSeqId))
            {
                noHmmChainInterfaces = pfamParseChainInterfaceHash[relSeqId];
            }

            foreach (int groupSeqId in groupSeqIds)
            {
                //      string[] groupRepChainInterfaces = GetRepChainInterfaces(groupSeqId, pfamId);
                //     string[] missingChainInterfaces = GetMissingInterfacesNotInClusters(groupSeqId, relSeqId, pfamChainInterfacesHash);
                //      string[] missingChainInterfaces = GetMissingClusterInterfaces(groupSeqId, relSeqId);
                string[] missingChainInterfaces = GetMissingClusterInterfaces(groupSeqId, relSeqId, noHmmChainInterfaces);
                if (missingChainInterfaces.Length > 0)
                {
                    ComparePeptideToChainInterfaces(relSeqId, groupSeqId, pfamId, pepDomainInterfacesTable, missingChainInterfaces);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CountPfamMissingChainInterfaceHmmSites(string pfamId, int groupSeqId)
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            InitializeTables(true);
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites_missing.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites_missing.txt"), true);

            int[] relSeqIds = GetPfamRelSeqIDs(pfamId);
            int relSeqId = 12668;
            int[] groupSeqIds = GetPfamRelatedChainGroups(pfamId);  // the super groups in which contain pfam arch with pfam
            DataTable pepDomainInterfacesTable = GetPeptideDomainInterfaceTable(relSeqId);

            string[] missingChainInterfaces = { "2qvs_1" };

            ComparePeptideToChainInterfaces(relSeqId, groupSeqId, pfamId, pepDomainInterfacesTable, missingChainInterfaces);
        }

     
        /// <summary>
        ///  may be able to remove some of interfaces based on Q score like  greater than 0.90
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetMissingClusterInterfaces(int superGroupId, int relSeqId,  string[] parsedNoHmmChainInterfaces)
        {
            string queryString = string.Format("Select PdbID2 As PdbID, DomainInterfaceID2 As InterfaceId From PfamChainInterfaceHmmSiteComp " +
                " Where RelSeqID1 = {0} AND RelSeqID2 = {1};", relSeqId, superGroupId);
            DataTable dbChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterInterfaces = GetChainInterfacesInClusters(superGroupId);
            List<string> missingInterfaceList = new List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            string chainInterface = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                pdbId = clusterInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(clusterInterface.Substring(5, clusterInterface.Length - 5));
                chainInterface = pdbId + "_" + interfaceId.ToString();
                if (parsedNoHmmChainInterfaces.Contains(chainInterface))
                {
                    continue;
                }
                if (!IsClusterInterfaceParsed(pdbId, interfaceId, dbChainInterfaceTable))
                {
                    if (IsChainInterfaceDerived(pdbId, interfaceId))  // some peptide interfaces are derived from this chain interface, so don't compare it
                    {
                        continue;
                    }
                    missingInterfaceList.Add(clusterInterface);
                }
            }
            return missingInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private bool IsClusterInterfaceParsed(string pdbId, int interfaceId, DataTable hmmSiteCompTable)
        {
            DataRow[] hmmSiteCompRows = hmmSiteCompTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            if (hmmSiteCompRows.Length > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetMissingInterfacesNotInClusters(int superGroupId, int relSeqId, Dictionary<int, string[]> pfamParsedInterfacesHash)
        {
            string[] familyCodes = GetFamilyCodes(superGroupId);
            string[] entriesInSuperGroup = GetSuperGroupEntries(superGroupId);
            List<string> chainInterfacesNotInClusterList = new List<string> ();

            string[] chainInterfacesInDb = GetParsedInterfacesInDb(superGroupId, relSeqId);

            string[] chainInterfacesInFile = null;
            if (pfamParsedInterfacesHash.ContainsKey(relSeqId))
            {
                chainInterfacesInFile = (string[])pfamParsedInterfacesHash[relSeqId];
            }
            else
            {
                chainInterfacesInFile = new string[0];
            }

            string chainInterface = "";
            foreach (string pdbId in entriesInSuperGroup)
            {
                int[] interfaceIdsNotInCluster = GetChainInterfacesNotInClusters(superGroupId, pdbId, familyCodes);
                int[] nonReduntInterfaceIdsNotInClusters = GetNonRedundantChainInterfaceIds(pdbId, interfaceIdsNotInCluster);
                foreach (int interfaceId in nonReduntInterfaceIdsNotInClusters)
                {
                    if (IsChainInterfaceDerived(pdbId, interfaceId))  // if some domain-peptide interfaces are derived from the chain interface, then skip it
                    {
                        continue;
                    }
                    chainInterface = pdbId + "_" + interfaceId.ToString();
                    if (chainInterfacesInDb.Contains(chainInterface))
                    {
                        continue;
                    }
                    if (chainInterfacesInFile.Contains(chainInterface))
                    {
                        continue;
                    }
                    chainInterfacesNotInClusterList.Add(pdbId + "_" + interfaceId.ToString());
                }
            }
            return chainInterfacesNotInClusterList.ToArray ();
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetParsedInterfacesInDb(int groupSeqId, int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID2, DomainInterfaceID2 " + 
                " From PfamChainInterfaceHmmSiteComp Where RelSeqID1 = {0} AND RelSeqID2 = {1};", relSeqId, groupSeqId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] parsedChainInterfaces = new string[chainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in chainInterfaceTable.Rows)
            {
                parsedChainInterfaces[count] = interfaceRow["PdbID2"].ToString() + "_" +
                    interfaceRow["DomainInterfaceID2"].ToString();
                count++;
            }
            return parsedChainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetAlreadyParsedChainInterfacesFromFile (string fileName)
        {
            Dictionary<int, List<string>> pfamChainInterfaceListHash = new Dictionary<int,List<string>> ();
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
        //    string pfamId = "";
            int relSeqId = 0;
            string chainInterface = "";
            while ((line = interfaceReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
            //    pfamId = fields[0];
                relSeqId = Convert.ToInt32(fields[1]);
                chainInterface = fields[6] + "_" + fields[7];
                if (pfamChainInterfaceListHash.ContainsKey(relSeqId))
                {
                    if (!pfamChainInterfaceListHash[relSeqId].Contains(chainInterface))
                    {
                        pfamChainInterfaceListHash[relSeqId].Add(chainInterface);
                    }
                }
                else
                {
                    List<string> interfaceList = new List<string> ();
                    interfaceList.Add(chainInterface);
                    pfamChainInterfaceListHash.Add(relSeqId, interfaceList);
                }
            }
            interfaceReader.Close();
            Dictionary<int, string[]> pfamChainInterfacesHash = new Dictionary<int, string[]>();
            foreach (int lsSeqId in pfamChainInterfaceListHash.Keys)
            {
                pfamChainInterfacesHash.Add(lsSeqId, pfamChainInterfaceListHash[lsSeqId].ToArray());
            }
            return pfamChainInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetAlreadyParsedChainInterfacesFromFile(string fileName, int workingRelSeqId)
        {
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
            string chainInterface = "";
            int relSeqId = 0;
            List<string> interfaceList = new List<string> ();
            bool isRelStart = false;
            while ((line = interfaceReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                //    pfamId = fields[0];
                relSeqId = Convert.ToInt32(fields[1]);
                if (relSeqId == workingRelSeqId)
                {
                    isRelStart = true;

                    chainInterface = fields[6] + "_" + fields[7];
                    interfaceList.Add(chainInterface);
                }
                else if (isRelStart)  // not the same RelSeqID any more, no need to continue
                {
                    break;
                }
            }
            interfaceReader.Close();
            string[] relChainInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(relChainInterfaces);
            Dictionary<int, string[]> relChainInterfacesHash = new Dictionary<int,string[]> ();
            relChainInterfacesHash.Add(workingRelSeqId, relChainInterfaces);
            return relChainInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetAlreadyParsedChainInterfacesFromFile(string fileName, string workingPfamId)
        {
            Dictionary<int, List<string>> pfamChainInterfaceListHash = new Dictionary<int,List<string>> ();
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
            int relSeqId = 0;
            string chainInterface = "";
            string pfamId = "";
            bool isPfamStart = false;
            while ((line = interfaceReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                pfamId = fields[0];
                if (pfamId == workingPfamId)
                {
                    isPfamStart = true;
                    //    pfamId = fields[0];
                    relSeqId = Convert.ToInt32(fields[1]);
                    chainInterface = fields[6] + "_" + fields[7];
                    if (pfamChainInterfaceListHash.ContainsKey(relSeqId))
                    {
                        if (!pfamChainInterfaceListHash[relSeqId].Contains(chainInterface))
                        {
                            pfamChainInterfaceListHash[relSeqId].Add(chainInterface);
                        }
                    }
                    else
                    {
                        List<string> interfaceList = new List<string> ();
                        interfaceList.Add(chainInterface);
                        pfamChainInterfaceListHash.Add(relSeqId, interfaceList);
                    }
                }
                else if (isPfamStart)  // not this pfam any more, no need to continue
                {
                    break;
                }
            }
            interfaceReader.Close();
            Dictionary<int, string[]> pfamChainInterfacesHash = new Dictionary<int, string[]>();
            foreach (int lsSeqId in pfamChainInterfaceListHash.Keys)
            {
                pfamChainInterfacesHash.Add(lsSeqId, pfamChainInterfaceListHash[lsSeqId].ToArray());
            }
            return pfamChainInterfacesHash;
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepRelSeqId"></param>
        /// <param name="chainGroupId"></param>
        /// <param name="pfamId"></param>
        /// <param name="pepDomainInterfacesTable"></param>
        /// <param name="chainInterface"></param>
        public void ComparePeptideToChainInterfaces()
        {
            interfaceHmmSiteTableName = "PfamChainInterfaceHmmSiteComp";
            InitializeTables(true);
            Dictionary<int, Dictionary<int, List<string>>> pepGroupChainInterfaceHash = GetMissingPepChainInterfaceHash();
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainHmmInteractingSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfaceHmmSites_debug.txt"), true);
            noCommonHmmSitesWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide\\ChainInterfacesNoHmmSites_debug.txt"), true);
            string pfamId = "";
            foreach (int pepRelSeqId in pepGroupChainInterfaceHash.Keys)
            {
               
                pfamId = GetPfamIdFromPeptideInterface(pepRelSeqId);
                DataTable pepDomainInterfacesTable = GetPeptideDomainInterfaceTable(pepRelSeqId);
                foreach (int superGroupId in pepGroupChainInterfaceHash[pepRelSeqId].Keys)
                {
                    string[] chainInterfaces = pepGroupChainInterfaceHash[pepRelSeqId][superGroupId].ToArray();
                    ComparePeptideToChainInterfaces(pepRelSeqId, superGroupId, pfamId, pepDomainInterfacesTable, chainInterfaces);
                }
            }
            domainHmmInteractingSitesWriter.Close();
            noCommonHmmSitesWriter.Close();
        }

        private Dictionary<int, Dictionary<int, List<string>>> GetMissingPepChainInterfaceHash()
        {
            Dictionary<int, Dictionary<int, List<string>>> pepChainInterfaceHash = new Dictionary<int,Dictionary<int,List<string>>> ();
            StreamReader dataReader = new StreamReader("pepchainlog.txt");
            string line = "";
            int pepRelSeqId = 0;
            int superGroupId = 0;
            string chainInterface = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Object reference not set to an instance of an object") > -1)
                {
                    string[] fields = line.Split(' ');
                    pepRelSeqId = Convert.ToInt32(fields[0]);
                    superGroupId = Convert.ToInt32(fields[1]);
                    chainInterface = fields[2];
                    if (pepChainInterfaceHash.ContainsKey(pepRelSeqId))
                    {
                        if (pepChainInterfaceHash[pepRelSeqId].ContainsKey(superGroupId))
                        {
                            if (!pepChainInterfaceHash[pepRelSeqId][superGroupId].Contains(chainInterface))
                            {
                                pepChainInterfaceHash[pepRelSeqId][superGroupId].Add(chainInterface);
                            }
                        }
                        else
                        {
                            List<string> chainInterfaceList = new List<string> ();
                            chainInterfaceList.Add(chainInterface);
                            pepChainInterfaceHash[pepRelSeqId].Add(superGroupId, chainInterfaceList);
                        }
                    }
                    else
                    {
                        Dictionary<int, List<string>> groupInterfaceHash = new Dictionary<int,List<string>> ();
                        List<string> chainInterfaceList = new List<string> ();
                        chainInterfaceList.Add(chainInterface);
                        groupInterfaceHash.Add(superGroupId, chainInterfaceList);
                        pepChainInterfaceHash.Add(pepRelSeqId, groupInterfaceHash);
                    }
                }
            }
            dataReader.Close();
            return pepChainInterfaceHash;
        }
        #endregion
    }
}
