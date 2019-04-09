using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPeptideInterfaces : DomainClassifier
    {
        #region member variables
        private DataTable pfamInteractSiteTable = null;
        private DataTable pfamPepInterfaceTable = null;
        private string pfamInteractTableName = "PfamPeptideHmmSites";
        private string pfamPepInterfaceTableName = "PfamPeptideInterfaces";
        private EntryCrystForms entryCf = new EntryCrystForms();
        #endregion

        #region pfam-peptide interfaces
        /// <summary>
        /// 
        /// </summary>
        public void RetrievePfamPeptideInterfaces()
        {
            bool isUpdate = true;
            InitializeTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Pfam-Peptide interactions and HMM sites.");

            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable entryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";

            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    RetrieveEntryPfamPeptideInterfaces (pdbId);

                    dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamInteractSiteTable);
                    pfamInteractSiteTable.Clear();
                    dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamPepInterfaceTable);
                    pfamPepInterfaceTable.Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " : " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " map interacting sequence residue and seqid to hmm: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Retrieving peptide interfaces done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryPeptideInterfaceExist (string pdbId)
        {
            string queryString = string.Format ("Select * From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pepInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns>pfams and its update entries</returns>
        public Dictionary<string, string[]> UpdatePfamPeptideInterfaces(string[] updateEntries)
        {
            bool isUpdate = true;
            InitializeTable(isUpdate);

            Dictionary<string, List<string>> updatePfamEntryListHash = new Dictionary<string,List<string>> ();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Pfam-Peptide interactions and HMM sites.");

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeletePfamPeptideInterfacesData(pdbId);

                    string[] pepInteractPfams = RetrieveEntryPfamPeptideInterfaces(pdbId);
                    if (pepInteractPfams.Length == 0)
                    {
                        continue;
                    }
                    foreach (string pfamId in pepInteractPfams)
                    {
                        if (updatePfamEntryListHash.ContainsKey(pfamId))
                        {
                            updatePfamEntryListHash[pfamId].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            updatePfamEntryListHash.Add(pfamId, entryList);
                        }
                    }                    

                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamInteractSiteTable);
                    pfamInteractSiteTable.Clear();
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamPepInterfaceTable);
                    pfamPepInterfaceTable.Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " : " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " map interacting sequence residue and seqid to hmm: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            StreamWriter dataWriter = new StreamWriter("UpdatePepInteractPfamEntries.txt");
            List<string> updatePfamList = new List<string> (updatePfamEntryListHash.Keys);
            updatePfamList.Sort();
            Dictionary<string, string[]> updatePfamEntryHash = new Dictionary<string, string[]>();
            foreach (string updatePfam in updatePfamList)
            {
                string[] entries = updatePfamEntryListHash[updatePfam].ToArray ();
                updatePfamEntryHash.Add (updatePfam, entries);
                dataWriter.WriteLine(updatePfam + " " + FormatEntryArray (entries));
            }
            dataWriter.Close();
            return updatePfamEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private string FormatEntryArray(string[] entries)
        {
            string entryString = "";
            foreach (string pdbId in entries)
            {
                entryString += (pdbId.ToString() + ",");
            }
            return entryString.TrimEnd(',');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeletePfamPeptideInterfacesData(string pdbId)
        {
            string deleteString = string.Format("Delete From " + pfamPepInterfaceTable.TableName + " Where PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From " + pfamInteractSiteTable.TableName + " Where PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private string[] RetrieveEntryPfamPeptideInterfaces (string pdbId)
        {
            DataTable chainPfamAssignTable = GetChainPfamAssign(pdbId);
            Dictionary<int, Dictionary<string, List<Range>>> domainRangeHash = GetDomainRangeHash(chainPfamAssignTable);
            // used to the interaction between two multi-chain domains
            string[] chainsInMultiChainDomains = GetChainsInMultiChainDomains(chainPfamAssignTable);

            string queryString = string.Format("Select * From ChainPeptideInterfaces " + 
                " Where PdbID = '{0}' AND PepLength < {1};", pdbId, ProtCidSettings.peptideLengthCutoff);
            DataTable chainPepInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format("Select * From ChainPeptideAtomPairs Where PdbID = '{0}';", pdbId);
            DataTable atomPairTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format("Select * From ChainPeptideResiduePairs Where PdbID = '{0}';", pdbId);
            DataTable residuePairTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format ("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            int domainInterfaceId = 0;
            int newDomainInterfaceId = GetMaxEntryDomainInterfaceID (pdbId) + 1;
            string asymChain = "";
            int numOfDomainAtomPairs = 0;
            int numOfDomainResiduePairs = 0;
            long domainId = 0;
            long pepDomainId = 0;
            int pepChainDomainId = 0;
            string pfamId = "";
            string pepPfamId = "";
            int hmmSeqId = 0;
            int relSeqId = 0;
            char isReversed = '0';
            int[] interactingAtomSeqIds = null;
            int[] interactingResidueSeqIds = null;
            string[] interactingAtomResidues = null;
            string[] interactingResidues = null;

            int numOfPepDomainAtomPairs = 0;
            int numOfPepDomainResiduePairs = 0;
            int[] interactingPepAtomSeqIds = null;
            int[] interactingPepResidueSeqIds = null;
            string[] interactingPepAtomResidues = null;
            string[] interactingPepResidues = null;

            string hmmAsymChain = "";
            int hmmChainDomainId = 0;
            DataRow[] hmmChainDomainRows = null;

            List<string> entryPfamList = new List<string> ();
            foreach (int chainDomainId in domainRangeHash.Keys)
            {
                numOfDomainAtomPairs = 0;
                numOfDomainResiduePairs = 0;
                DataRow[] domainAssignRows = chainPfamAssignTable.Select (string.Format ("ChainDomainID = '{0}'", chainDomainId));
                pfamId = domainAssignRows[0]["Pfam_ID"].ToString ().TrimEnd ();
                domainId = Convert.ToInt64 (domainAssignRows[0]["DomainID"].ToString());
                
                Dictionary<string, List<Range>> chainDomainRangeHash = domainRangeHash[chainDomainId];
                List<string> domainChainList = new List<string> (chainDomainRangeHash.Keys );
                domainChainList.Sort ();
                Dictionary<string, List<int>> pepAsymChainInterfaceIdHash = GetDomainChainInterfaceIds(chainPepInterfaceTable, domainChainList.ToArray (), chainsInMultiChainDomains);
                foreach (string pepAsymChain in pepAsymChainInterfaceIdHash.Keys)
                {
                    DataRow pepAsymChainDomainRow = GetChainDomainInfoRow(pepAsymChain, chainPfamAssignTable);
                    if (pepAsymChainDomainRow != null)
                    {
                        pepPfamId = pepAsymChainDomainRow["Pfam_ID"].ToString().TrimEnd();
                        pepChainDomainId = Convert.ToInt32(pepAsymChainDomainRow["ChainDomainID"].ToString());
                        pepDomainId = Convert.ToInt64(pepAsymChainDomainRow["DomainID"].ToString());
                    }
                    else
                    {
                        pepPfamId = "peptide";
                        pepDomainId = -1;
                        pepChainDomainId = -1;
                    }
                    if (pepChainDomainId == chainDomainId)  // don't want interaction within a domain
                    {
                        continue;
                    }
                    relSeqId = GetRelSeqId(pfamId, pepPfamId, out isReversed);
                    domainInterfaceId = GetDomainInterfaceId(pdbId, chainDomainId, pepChainDomainId, chainInterfaceTable);
                    if (domainInterfaceId == -1)
                    {
                        domainInterfaceId = newDomainInterfaceId;
                        newDomainInterfaceId++;
                    }

                    foreach (int chainInterfaceId in pepAsymChainInterfaceIdHash[pepAsymChain])
                    {
                        DataRow[] interfaceRows = chainPepInterfaceTable.Select("InterfaceID = " + chainInterfaceId.ToString());
                        asymChain = interfaceRows[0]["AsymChain"].ToString().TrimEnd();
                        DataRow[] atomPairRows = atomPairTable.Select("InterfaceID = " + chainInterfaceId.ToString(), "SeqID ASC");
                        DataRow[] residuePairRows = residuePairTable.Select("InterfaceID = " + chainInterfaceId.ToString(), "SeqID ASC");
                        hmmAsymChain = asymChain;
                        hmmChainDomainId = chainDomainId;
                        hmmChainDomainRows = domainAssignRows;

                        Range[] domainRanges = chainDomainRangeHash[asymChain].ToArray();
                        numOfDomainAtomPairs = GetNumOfContacts(atomPairRows, domainRanges, out interactingAtomSeqIds, out interactingAtomResidues);
                        numOfDomainResiduePairs = GetNumOfContacts(residuePairRows, domainRanges, out interactingResidueSeqIds, out interactingResidues);

                        if (numOfDomainAtomPairs > 0 && numOfDomainResiduePairs > 0)
                        {
                            DataRow domainPepInterfaceRow = pfamPepInterfaceTable.NewRow();
                            domainPepInterfaceRow["PfamID"] = pfamId;
                            domainPepInterfaceRow["RelSeqID"] = relSeqId;
                            domainPepInterfaceRow["PdbID"] = pdbId;
                            domainPepInterfaceRow["InterfaceID"] = chainInterfaceId;
                            domainPepInterfaceRow["DomainInterfaceID"] = domainInterfaceId;
                            domainPepInterfaceRow["DomainId"] = domainId;
                            domainPepInterfaceRow["AsymChain"] = asymChain;
                            domainPepInterfaceRow["ChainDomainId"] = chainDomainId;
                            domainPepInterfaceRow["PepAsymChain"] = pepAsymChain;
                            if (pepPfamId == "peptide")
                            {
                                domainPepInterfaceRow["PepPfamId"] = "-";
                            }
                            else
                            {
                                domainPepInterfaceRow["PepPfamId"] = pepPfamId;
                            }
                            domainPepInterfaceRow["PepDomainId"] = pepDomainId;
                            domainPepInterfaceRow["PepChainDomainID"] = pepChainDomainId;
                            domainPepInterfaceRow["NumOfAtomPairs"] = numOfDomainAtomPairs;
                            domainPepInterfaceRow["NumOfResiduePairs"] = numOfDomainResiduePairs;
                            domainPepInterfaceRow["SurfaceArea"] = -1;
                            domainPepInterfaceRow["CrystalPack"] = '1';
                            if (!entryPfamList.Contains(pfamId))
                            {
                                entryPfamList.Add(pfamId);
                            }

                            if (pepDomainId == domainId)
                            {
                                numOfPepDomainAtomPairs = GetNumOfPepContacts(atomPairRows, domainRanges, out interactingPepAtomSeqIds, out interactingPepAtomResidues);
                                numOfPepDomainResiduePairs = GetNumOfPepContacts(atomPairRows, domainRanges, out interactingPepResidueSeqIds, out interactingPepResidues);
                                if (numOfDomainAtomPairs < numOfPepDomainAtomPairs ||
                                    (numOfDomainAtomPairs == numOfPepDomainAtomPairs && numOfDomainResiduePairs < numOfPepDomainResiduePairs))
                                {
                                    ReversePfamPeptideInterfaceInfo(domainPepInterfaceRow);
                                    domainPepInterfaceRow["NumOfAtomPairs"] = numOfPepDomainAtomPairs;
                                    domainPepInterfaceRow["NumOfResiduePairs"] = numOfPepDomainResiduePairs;
                                    interactingAtomSeqIds = interactingPepAtomSeqIds;
                                    interactingAtomResidues = interactingPepAtomResidues;
                                    hmmAsymChain = pepAsymChain;
                                    hmmChainDomainId = pepChainDomainId;
                                    hmmChainDomainRows = chainPfamAssignTable.Select(string.Format("ChainDomainID = '{0}'", hmmChainDomainId));
                                }
                            }
                            pfamPepInterfaceTable.Rows.Add(domainPepInterfaceRow);

                             // peptide domain id is same as prot domain id in case of same entity dimer
                            DataRow chainDomainRow = GetDomainChainRow(hmmAsymChain, hmmChainDomainRows);
                            for (int i = 0; i < interactingAtomSeqIds.Length; i ++ )
                            {
                                hmmSeqId = GetHmmSeqId(interactingAtomSeqIds[i], chainDomainRow);
                                if (hmmSeqId > -1)
                                {
                                    DataRow dataRow = pfamInteractSiteTable.NewRow();
                                    dataRow["PdbID"] = pdbId;
                                    dataRow["InterfaceID"] = chainInterfaceId;
                                    dataRow["DomainInterfaceID"] = domainInterfaceId;
                                    dataRow["ChainDomainID"] = hmmChainDomainId;
                                    dataRow["AsymChain"] = hmmAsymChain;
                                    dataRow["Residue"] = interactingAtomResidues[i];
                                    dataRow["SeqID"] = interactingAtomSeqIds[i];
                                    dataRow["PfamID"] = pfamId;
                                    dataRow["HmmSeqID"] = hmmSeqId;
                                    pfamInteractSiteTable.Rows.Add(dataRow);
                                }
                            }  // add data into pfamInteractSiteTable
                        } // if any contacts
                    } // one of chain interface
                } // peptide chain
            } // domain (pfam)
            SetCrystalPackField (pfamPepInterfaceTable);

            return entryPfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPepInterfceRow"></param>
        private void ReversePfamPeptideInterfaceInfo(DataRow domainPepInterfceRow)
        {
            object temp = domainPepInterfceRow["DomainID"];
            domainPepInterfceRow["DomainID"] = domainPepInterfceRow["PepDomainID"];
            domainPepInterfceRow["PepDomainID"] = temp;
            temp = domainPepInterfceRow["AsymChain"];
            domainPepInterfceRow["AsymChain"] = domainPepInterfceRow["PepAsymChain"];
            domainPepInterfceRow["PepAsymChain"] = temp;
            temp = domainPepInterfceRow["ChainDomainID"];
            domainPepInterfceRow["ChainDomainId"] = domainPepInterfceRow["PepChainDomainID"];
            domainPepInterfceRow["PepChainDomainID"] = temp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamAssignTable"></param>
        /// <returns></returns>
        private string[] GetChainsInMultiChainDomains(DataTable chainPfamAssignTable)
        {
            List<string> chainListInMultiChainDomainList = new List<string> ();
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            foreach (DataRow pfamAssignRow in chainPfamAssignTable.Rows)
            {
                chainDomainId = Convert.ToInt32(pfamAssignRow["ChainDomainID"].ToString ());
                if (!chainDomainIdList.Contains(chainDomainId))
                {
                    chainDomainIdList.Add(chainDomainId);
                }
            }
            string multiChainDomainChain = "";
            foreach (int lsChainDomainId in chainDomainIdList)
            {
                DataRow[] chainDomainRows = chainPfamAssignTable.Select(string.Format("ChainDomainID = '{0}'", lsChainDomainId));
                if (chainDomainRows.Length > 1)  // it is a multi-chain domain
                {
                    foreach (DataRow chainDomainRow in chainDomainRows)
                    {
                        multiChainDomainChain = chainDomainRow["AsymChain"].ToString().TrimEnd();
                        chainListInMultiChainDomainList.Add(multiChainDomainChain);
                    }
                }
            }
            return chainListInMultiChainDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainRows"></param>
        /// <returns></returns>
        private DataRow GetDomainChainRow(string asymChain, DataRow[] chainDomainRows)
        {
            string rowAsymChain = "";
            foreach (DataRow chainDomainRow in chainDomainRows)
            {
                rowAsymChain = chainDomainRow["AsymChain"].ToString ().TrimEnd ();
                if (rowAsymChain == asymChain)
                {
                    return chainDomainRow;
                }
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="pepChainDomainId"></param>
        /// <param name="chainInterfaceTable"></param>
        /// <returns></returns>
        private int GetDomainInterfaceId(string pdbId, int chainDomainId, int pepChainDomainId, DataTable chainInterfaceTable)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " + 
                " Where PdbID = '{0}' AND ChainDomainID1 = {1} AND ChainDomainID2 = {2};",
                pdbId, chainDomainId, pepChainDomainId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int interfaceId = 0;
            int domainInterfaceId = -1;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString ());
                if (IsChainInterfaceInAsu(chainInterfaceTable, interfaceId))
                {
                    domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString ());
                    break;
                }
            }
         
            return domainInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaceTable"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsChainInterfaceInAsu(DataTable chainInterfaceTable, int interfaceId)
        {
            DataRow[] interfaceRows = chainInterfaceTable.Select(string.Format ("InterfaceID = '{0}'", interfaceId ));
            if (interfaceRows.Length > 0)
            {
                string symmetryString1 = interfaceRows[0]["SymmetryString1"].ToString().TrimEnd();
                string symmetryString2 = interfaceRows[0]["SymmetryString2"].ToString().TrimEnd();
                if (symmetryString1 == "1_555" && symmetryString2 == "1_555")
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
        /// <returns></returns>
        private int GetMaxEntryDomainInterfaceID(string pdbId)
        {
            string queryString = string.Format("Select Max (DomainInterfaceID) As MaxInterfaceID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable maxInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string maxInterfaceIdString = maxInterfaceIdTable.Rows[0]["MaxInterfaceID"].ToString();
            if (maxInterfaceIdString != null && maxInterfaceIdString != "")
            {
                return Convert.ToInt32(maxInterfaceIdString);
            }
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contactRows"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private int GetNumOfContacts(DataRow[] contactRows, Range[] domainRanges, out int[] interactingSeqIds, out string[] interactingResidues)
        {
            int numOfContacts = 0;
            int seqId = 0;
            List<int> interactingSeqIdList = new List<int> ();
            List<string> interactingResidueList =  new List<string> ();
            foreach (DataRow contactRow in contactRows)
            {
                seqId = Convert.ToInt32(contactRow["SeqID"].ToString ());
                if (IsResidueSeqIdInDomain (seqId, domainRanges))
                {
                    numOfContacts++;
                    if (!interactingSeqIdList.Contains(seqId))
                    {
                        interactingSeqIdList.Add(seqId);
                        interactingResidueList.Add(contactRow["Residue"].ToString());
                    }
                }
            }
            interactingSeqIds = interactingSeqIdList.ToArray (); 
            interactingResidues = interactingResidueList.ToArray ();

            return numOfContacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contactRows"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private int GetNumOfPepContacts(DataRow[] contactRows, Range[] domainRanges, out int[] interactingSeqIds, out string[] interactingResidues)
        {
            int numOfContacts = 0;
            int seqId = 0;
            List<int> interactingSeqIdList = new List<int> ();
            List<string> interactingResidueList = new List<string> ();
            foreach (DataRow contactRow in contactRows)
            {
                seqId = Convert.ToInt32(contactRow["PepSeqID"].ToString());
                if (IsResidueSeqIdInDomain(seqId, domainRanges))
                {
                    numOfContacts++;
                    if (!interactingSeqIdList.Contains(seqId))
                    {
                        interactingSeqIdList.Add(seqId);
                        interactingResidueList.Add(contactRow["PepResidue"].ToString());
                    }
                }
            }
            interactingSeqIds = interactingSeqIdList.ToArray ();
            interactingResidues = interactingResidueList.ToArray ();
            return numOfContacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private DataRow GetChainDomainInfoRow(string asymChain, DataTable chainPfamTable)
        {
            DataRow[] domainRows = chainPfamTable.Select(string.Format ("AsymChain = '{0}'", asymChain));
            if (domainRows.Length > 0)
            {
                return domainRows[0];
            }
            return null;
        }
        #endregion

        #region filter out crystal packing peptide interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPeptideInterfaceTable"></param>
        private void SetCrystalPackField (DataTable domainPeptideInterfaceTable)
        {
            if (domainPeptideInterfaceTable.Rows.Count == 0)
            {
                return;
            }
            string pdbId = domainPeptideInterfaceTable.Rows[0]["PdbID"].ToString();
            int[] leftPeptideInterfaceIds = RemovePeptideInterfacesWithSameEntity(domainPeptideInterfaceTable);
            foreach (int peptideDomainInterfaceId in leftPeptideInterfaceIds)
            {
                DataRow[] peptideDomainInterfaceRows = domainPeptideInterfaceTable.Select(string.Format ("DomainInterfaceId = '{0}'", peptideDomainInterfaceId));
                for (int i = 0; i < peptideDomainInterfaceRows.Length; i ++ )
                {
                    peptideDomainInterfaceRows[i]["CrystalPack"] = '0';
                }
            }
            domainPeptideInterfaceTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RemoveCrystalPackingPeptideInterfaces()
        {
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataTable domainPeptideInterfaceTable = GetPfamPeptideInterfacesTable(pdbId);
           //     DataTable chainPeptideInterfaceTable = GetChainPeptideInterfacesTable(pdbId);

                RemoveCrystalPackingInterfaces(domainPeptideInterfaceTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetPfamPeptideInterfacesTable(string pdbId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return peptideInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainPeptideInterfacesTable(string pdbId)
        {
            string queryString = string.Format("Select * from ChainPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable chainInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            return chainInterfaceTable;
        }
        /* a peptide is not likely to interact chains with same entity id,
         * so only use the interface with the maximum number of contacts.
         * */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPeptideInterfaceTable"></param>
        /// <param name="pfamInteractSiteTable"></param>
        /// <param name="chainPeptideInterfaceTable"></param>
        private void RemoveCrystalPackingInterfaces(DataTable domainPeptideInterfaceTable)
        {
            string pdbId = domainPeptideInterfaceTable.Rows[0]["PdbID"].ToString ();
      //      int[] leftPeptideInterfaceIds = GetBestPeptideInterfacesWithSameEntity(domainPeptideInterfaceTable, chainPeptideInterfaceTable);
            int[] leftPeptideInterfaceIds = RemovePeptideInterfacesWithSameEntity(domainPeptideInterfaceTable);
            foreach (int peptideDomainInterfaceId in leftPeptideInterfaceIds)
            {
                UpdateCrystalPackField(pdbId, peptideDomainInterfaceId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void UpdateCrystalPackField(string pdbId, int domainInterfaceId)
        {
            string updateString = string.Format("Update PfamPeptideInterfaces Set CrystalPack = '0' Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPeptideInterfaceTable"></param>
        /// <param name="chainPeptideInterfaceTable"></param>
        /// <returns></returns>
        private int[] RemovePeptideInterfacesWithSameEntity(DataTable domainPeptideInterfaceTable)
        {
            List<int> domainInterfaceIdList = new List<int> ();
            foreach (DataRow pepInterfaceRow in domainPeptideInterfaceTable.Rows)
            {
                int domainInterfaceId = Convert.ToInt32(pepInterfaceRow["DomainInterfaceID"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }
            string peptideDomain = "";
            int numOfContactsPepInterface = 0;
            int numOfResidueContactsPepInterface = 0;
            Dictionary<string, List<int[]>> peptideDomainContactsHash = new Dictionary<string, List<int[]>>();
            foreach (int domainInterfaceId in domainInterfaceIdList)
            {
                DataRow[] peptideDomainInterfaceRows = domainPeptideInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
                numOfContactsPepInterface = GetNumOfAtomContacts(peptideDomainInterfaceRows);
                numOfResidueContactsPepInterface = GetNumOfResidueContacts (peptideDomainInterfaceRows );
               
                peptideDomain = peptideDomainInterfaceRows[0]["PepAsymChain"].ToString().TrimEnd() + "_" + 
                    peptideDomainInterfaceRows[0]["DomainID"].ToString ();
               
                int[] domainInterfaceIdContacts = new int[3];
                domainInterfaceIdContacts[0] = domainInterfaceId;
                domainInterfaceIdContacts[1] = numOfContactsPepInterface;
                domainInterfaceIdContacts[2] = numOfResidueContactsPepInterface;
                if (peptideDomainContactsHash.ContainsKey(peptideDomain))
                {
                    peptideDomainContactsHash[peptideDomain].Add(domainInterfaceIdContacts);
                }
                else
                {
                    List<int[]> interfaceContactList = new List<int[]> ();
                    interfaceContactList.Add(domainInterfaceIdContacts);
                    peptideDomainContactsHash.Add(peptideDomain, interfaceContactList);
                }
            }

            int[] leftPeptideInterfaceIds = GetPeptideInterfaceIds(peptideDomainContactsHash);

            return leftPeptideInterfaceIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideDomainContactsHash"></param>
        /// <returns></returns>
        private int[] GetPeptideInterfaceIds (Dictionary<string, List<int[]>> peptideDomainContactsHash)
        {
            List<int> leftPeptideInterfaceIdList = new List<int> ();
            int numOfContactsSum = 0;
            double coverage = 0;
            bool peptideInterfaceFound = false;
            foreach (string peptideDomainId in peptideDomainContactsHash.Keys)
            {
                numOfContactsSum = 0;
                peptideInterfaceFound = false;
                foreach (int[] interfaceContacts in peptideDomainContactsHash[peptideDomainId])
                {
                    numOfContactsSum += interfaceContacts[1];
                }
                if (peptideDomainContactsHash[peptideDomainId].Count > 1)  // if a peptide has more than one protein interactions
                {
                    foreach (int[] interfaceContacts in peptideDomainContactsHash[peptideDomainId])
                    {
                        coverage = (double)interfaceContacts[1] / (double)numOfContactsSum;
                        if (coverage >= 0.75)  // and there is one interface take the most of contacts, then only use this one
                        {
                            leftPeptideInterfaceIdList.Add(interfaceContacts[0]);
                            peptideInterfaceFound = true;
                            break;
                        }
                    }
                }
                if (! peptideInterfaceFound)  // otherwise, use cutoff 5 for any atomic cutoff, 10 for Cbeta/Calpha cutoff
                {
                    foreach (int[] interfaceContacts in peptideDomainContactsHash[peptideDomainId])
                    {
                        // cutoff: 5, 10
                        if (interfaceContacts[1] < AppSettings.parameters.contactParams.domainNumOfAtomContacts &&
                            interfaceContacts[2] < AppSettings.parameters.contactParams.numOfResidueContacts)
                        {
                            continue;  // skip those with small number of contacts
                        }
                        leftPeptideInterfaceIdList.Add(interfaceContacts[0]);
                    }
                }
            }
            return leftPeptideInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideDomainContactsHash"></param>
        /// <returns></returns>
        private int[] GetPeptideInterfaceIdsWithMaxContacts (Dictionary<string, List<int[]>> peptideDomainContactsHash)
        {
            int[] maxInterfaceContacts = new int[2];
            List<int> leftPeptideInterfaceIdList = new List<int> ();

            foreach (string peptideDomainId in peptideDomainContactsHash.Keys)
            {
                maxInterfaceContacts[0] = 0;
                maxInterfaceContacts[1] = 0;
                foreach (int[] interfaceContacts in peptideDomainContactsHash[peptideDomainId])
                {
                    if (maxInterfaceContacts[1] < interfaceContacts[1])
                    {
                        maxInterfaceContacts[0] = interfaceContacts[0];
                        maxInterfaceContacts[1] = interfaceContacts[1];
                    }
                }
                leftPeptideInterfaceIdList.Add(maxInterfaceContacts[0]);
            }
            return leftPeptideInterfaceIdList.ToArray ();
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideInterfaceRows"></param>
        /// <returns></returns>
        private int GetNumOfAtomContacts(DataRow[] peptideInterfaceRows)
        {
            int numOfContacts = 0;
            foreach (DataRow peptideRow in peptideInterfaceRows)
            {
                numOfContacts += (Convert.ToInt32(peptideRow["NumOfAtomPairs"].ToString()));
            }
            return numOfContacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideInterfaceRows"></param>
        /// <returns></returns>
        private int GetNumOfResidueContacts(DataRow[] peptideInterfaceRows)
        {
            int numOfContacts = 0;
            foreach (DataRow peptideRow in peptideInterfaceRows)
            {
                numOfContacts += (Convert.ToInt32(peptideRow["NumOfResiduePairs"].ToString()));
            }
            return numOfContacts;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideEntity"></param>
        /// <param name="numOfAtomContacts"></param>
        /// <param name="peptideEntityContactHash"></param>
        /// <returns></returns>
        private bool CanPeptideInterfaceBeAdded(string peptideEntity, int numOfAtomContacts, Dictionary<string, int> peptideEntityContactHash)
        {
            if (peptideEntityContactHash.ContainsKey(peptideEntity))
            {
                int numOfContacts = peptideEntityContactHash[peptideEntity];
                if (numOfAtomContacts <= numOfContacts)
                {
                    return false;
                }
                else
                {
                    peptideEntityContactHash[peptideEntity] = numOfAtomContacts;
                    return true;
                }
            }
            return true;
        }
        #endregion

        #region pfam hmm peptide
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPeptideInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<string, List<int>> GetDomainChainInterfaceIds(DataTable chainPeptideInterfaceTable, string[] domainChains, string[] chainsInMultiChainDomains)
        {
            Dictionary<string, List<int>> pepChainInterfaceIdHash = new Dictionary<string, List<int>>();
            int interfaceId = 0;
            string asymChain = "";
            string pepAsymChain = "";
            foreach (DataRow interfaceRow in chainPeptideInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                asymChain = interfaceRow["AsymChain"].ToString().TrimEnd();
                if (Array.IndexOf(domainChains, asymChain) > -1)
                {
                    pepAsymChain = interfaceRow["PepAsymChain"].ToString().TrimEnd();
                    if (IsPepChainInMultiChainDomain(pepAsymChain, chainsInMultiChainDomains))
                    {
                        continue;
                    }
                    if (pepChainInterfaceIdHash.ContainsKey(pepAsymChain))
                    {
                        pepChainInterfaceIdHash[pepAsymChain].Add(interfaceId);
                    }
                    else
                    {
                        List<int> interfaceIdList = new List<int> ();
                        interfaceIdList.Add(interfaceId);
                        pepChainInterfaceIdHash.Add(pepAsymChain, interfaceIdList);
                    }
                }
            }
            return pepChainInterfaceIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepChain"></param>
        /// <param name="chainsOfMultiChainDomains"></param>
        /// <returns></returns>
        private bool IsPepChainInMultiChainDomain(string pepChain, string[] chainsOfMultiChainDomains)
        {
            if (Array.IndexOf(chainsOfMultiChainDomains, pepChain) > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomPairRows"></param>
        /// <param name="residuePairRows"></param>
        /// <returns></returns>
        private int[] GetInteractingProtResidues(DataRow[] atomPairRows, DataRow[] residuePairRows, out string[] residues)
        {
            List<int> seqIdList = new List<int> ();
            List<string> residueList = new List<string> ();
            int seqId = 0;
            string residue = "";
            foreach (DataRow atomPairRow in atomPairRows)
            {
                seqId = Convert.ToInt32(atomPairRow["SeqID"].ToString ());
                residue = atomPairRow["Residue"].ToString().TrimEnd();
                if (!seqIdList.Contains(seqId))
                {
                    seqIdList.Add(seqId);
                    residueList.Add(residue);
                }
            }
            residues = residueList.ToArray (); 
            int[] seqIds = seqIdList.ToArray (); 
            return seqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private bool IsSeqIdInDomainRanges(int seqId, Range[] domainRanges)
        {
            foreach (Range range in domainRanges)
            {
                if (seqId >= range.startPos && seqId <= range.endPos)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region chain-peptide interface info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetChainPfamAssign(string pdbId)
        {
            string queryString = string.Format("Select PdbPfamChain.*,  " +
                " Pfam_ID, AlignStart, AlignEnd, HmmStart, HmmEnd, QueryAlignment, HmmAlignment" + 
                " From PdbPfam, PdbPfamChain " +
                " Where PdbPfam.PdbID = '{0}' AND PdbPfamChain.PdbID = '{0}' AND " +
                " PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pdbId);
            DataTable pfamAssignTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            return pfamAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<Range>>> GetDomainRangeHash(DataTable chainPfamTable)
        {
            DataRow[] chainPfamRows = chainPfamTable.Select();
            Dictionary<int, Dictionary<string, List<Range>>> domainRangeHash = GetDomainRangeHash(chainPfamRows);
            return domainRangeHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamRows"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<Range>>> GetDomainRangeHash(DataRow[] chainPfamRows)
        {
            Dictionary<int, Dictionary<string, List<Range>>> domainRangeHash = new Dictionary<int, Dictionary<string, List<Range>>>();
            int domainId = 0;
            string asymChain = "";
            foreach (DataRow chainRow in chainPfamRows)
            {
                domainId = Convert.ToInt32(chainRow["ChainDomainID"].ToString ());
                asymChain = chainRow["AsymChain"].ToString().TrimEnd();
                Range domainRange = new Range();
                domainRange.startPos = Convert.ToInt32(chainRow["AlignStart"].ToString ());
                domainRange.endPos = Convert.ToInt32(chainRow["AlignEnd"].ToString ());
                if (domainRangeHash.ContainsKey(domainId))
                {
                    if (domainRangeHash[domainId].ContainsKey(asymChain))
                    {
                        domainRangeHash[domainId][asymChain].Add(domainRange);
                    }
                    else
                    {
                        List<Range> rangeList = new List<Range> ();
                        rangeList.Add(domainRange);
                        domainRangeHash[domainId].Add(asymChain, rangeList);
                    }
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(domainRange);
                    Dictionary<string, List<Range>> chainDomainRangeHash = new Dictionary<string,List<Range>> ();
                    chainDomainRangeHash.Add(asymChain, rangeList);
                    domainRangeHash.Add(domainId, chainDomainRangeHash);
                }
            }
            return domainRangeHash;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <param name="interfaceInfoTable"></param>
        /// <returns></returns>
        private DataRow[] GetDomainChainPeptideInfoRows(DataRow[] domainRows, DataTable interfaceInfoTable, out int[] chainInterfaceIds)
        {
            List<string> asymChainList = new List<string> ();
            foreach (DataRow domainRow in domainRows)
            {
                asymChainList.Add(domainRow["AsymChain"].ToString ().TrimEnd ());
            }
            string[] asymChains = new string[asymChainList.Count];
            asymChainList.CopyTo(asymChains);

            DataRow[] interfaceRows = GetDomainChainPeptideInfoRows(asymChains, interfaceInfoTable, out chainInterfaceIds);

            return interfaceRows;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainEntities"></param>
        /// <param name="interfaceInfoTable"></param>
        /// <returns></returns>
        private DataRow[] GetDomainChainPeptideInfoRows(string[] domainAsymChains, DataTable interfaceInfoTable, out int[] interfaceIds)
        {
            List<DataRow> dataRowList = new List<DataRow> ();
            List<int> interfaceIdList = new List<int> ();
            int interfaceId = 0;
            foreach (string asymChain in domainAsymChains)
            {
                DataRow[] interfaceRows = interfaceInfoTable.Select("AsymChain = " + asymChain);
                foreach (DataRow interfaceRow in interfaceRows)
                {
                    interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                    dataRowList.Add(interfaceRow);
                    interfaceIdList.Add(interfaceId);
                }
            }
            interfaceIds = interfaceIdList.ToArray (); 
            DataRow[] dataRows = dataRowList.ToArray ();
            return dataRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceIds"></param>
        /// <param name="interfaceInfoTable"></param>
        /// <returns></returns>
        private DataRow[] GetInterfaceInfoRows(int[] interfaceIds, DataTable interfaceInfoTable)
        {
            List<DataRow> interfaceInfoRowList = new List<DataRow> ();
            foreach (int interfaceId in interfaceIds)
            {
                DataRow[] interfaceRows = interfaceInfoTable.Select("InterfaceID = " + interfaceId);
                interfaceInfoRowList.AddRange (interfaceRows);
            }
            DataRow[] interfaceInfoRows = interfaceInfoRowList.ToArray ();
            return interfaceInfoRows;
        }
        #endregion

        #region HMM interacting sites
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainAssignRows"></param>
        /// <returns></returns>
        public int GetHmmSeqId(int seqId, DataRow domainAssignRow)
        {
            int start = Convert.ToInt32(domainAssignRow["AlignStart"].ToString());
            int end = Convert.ToInt32(domainAssignRow["AlignEnd"].ToString());
            int hmmSeqId = -1;

            if (seqId >= start && seqId <= end)
            {
                hmmSeqId = MapSequenceSeqIdToHmmSeqId(seqId, domainAssignRow);
            }
            return hmmSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRow"></param>
        /// <returns></returns>
        public int MapSequenceSeqIdToHmmSeqId(int seqId, DataRow domainRow)
        {
            int seqStart = Convert.ToInt32(domainRow["AlignStart"].ToString());
            int seqEnd = Convert.ToInt32(domainRow["AlignEnd"].ToString());

            int hmmStart = Convert.ToInt32(domainRow["HmmStart"].ToString());
            int hmmEnd = Convert.ToInt32(domainRow["HmmEnd"].ToString());

            string seqAlignment = domainRow["QueryAlignment"].ToString().TrimEnd();
            string hmmAlignment = domainRow["HmmAlignment"].ToString().TrimEnd();

            int alignSeqId = seqStart - 1;
            int hmmSeqId = -1;
            int numOfHmmResidues = 0;
            for (int i = 0; i < seqAlignment.Length; i++)
            {
                if (seqAlignment[i] != '.' && seqAlignment[i] != '-')
                {
                    alignSeqId++;

                    if (alignSeqId == seqId)
                    {
                        if (hmmAlignment[i] != '.' && hmmAlignment[i] != '-')
                        {
                            hmmSeqId = hmmStart + numOfHmmResidues;
                        }
                        else
                        {
                            hmmSeqId = -1;
                        }
                        break;
                    }
                }
                if (hmmAlignment[i] != '.' && hmmAlignment[i] != '-')
                {
                    numOfHmmResidues++;
                }
            }
            return hmmSeqId;
        }
        #endregion

        #region intialize tables
        private void InitializeTable(bool isUpdate)
        {
            pfamInteractSiteTable = new DataTable(pfamInteractTableName);
            string[] tableColumns = {"PdbID", "InterfaceID", "DomainInterfaceID", "ChainDomainID", "AsymChain", 
                                     "Residue", "SeqID", "PfamID", "HmmSeqID"};
            foreach (string tableCol in tableColumns)
            {
                pfamInteractSiteTable.Columns.Add(new DataColumn (tableCol));
            }

            pfamPepInterfaceTable = new DataTable(pfamPepInterfaceTableName);
            string[] pfamPepColumns = {"RelSeqID", "PfamID", "PdbID", "InterfaceID", "DomainInterfaceID", 
                                "DomainID", "AsymChain", "ChainDomainID", "PepPfamID",
                                "PepAsymChain", "PepDomainID", "PepChainDomainID", 
                                "NumOfAtomPairs", "NumOfResiduePairs", "SurfaceArea", "CrystalPack"};
            foreach (string pfamPepCol in pfamPepColumns)
            {
                pfamPepInterfaceTable.Columns.Add(new DataColumn (pfamPepCol));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "CREATE TABLE " + pfamInteractTableName + " ( " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " InterfaceID INTEGER NOT NULL, " +
                    " DomainInterfaceID INTEGER NOT NULL, " +
                    " ChainDomainID INTEGER NOT NULL, " +
                    " AsymChain CHAR(3) NOT NULL, " +
                    " Residue CHAR(3) NOT NULL, " +
                    " SeqID INTEGER NOT NULL, " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " HmmSeqID INTEGER NOT NULL );";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pfamInteractTableName);

                string createIndexString = "CREATE INDEX " + pfamInteractTableName + "_idx1 ON " +
                    pfamInteractTableName + "(PdbID, DomainInterfaceID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, pfamInteractTableName);

                createTableString = "CREATE TABLE " + pfamPepInterfaceTableName + " ( " +
                    " RelSeqID INTEGER NOT NULL, " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " InterfaceID INTEGER NOT NULL, " + // refer to ChainPeptideInterfaces in BuComp database
                    " DomainInterfaceID INTEGER NOT NULL, " + // should continue from the pfamdomaininterfaces table for the entry
                    " DomainID INTEGER NOT NULL, " +
                    " AsymChain CHAR(3) NOT NULL, " +
                    " ChainDomainID INTEGER NOT NULL, " +
                    " PepPfamID VARCHAR(40) NOT NULL, " +
                    " PepDomainID INTEGER NOT NULL, " +  // can be -1, no pfam assigned
                    " PepAsymChain CHAR(3) NOT NULL, " +
                    " PepChainDomainID INTEGER NOT NULL, " +
                    " SurfaceArea FLOAT, " +
                    " NumOfAtomPairs INTEGER NOT NULL, " +
                    " NumOfResiduePairs INTEGER NOT NULL, " + 
                    " CrystalPack CHAR(1) " +
                    " );";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pfamPepInterfaceTableName);

                createIndexString = "CREATE INDEX " + pfamPepInterfaceTableName + "_idx1 ON " +
                                    pfamPepInterfaceTableName + "(PdbID, DomainInterfaceID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, pfamPepInterfaceTableName);
            }
        }
        #endregion

        #region hmm sites for homo domain dimer
        /// <summary>
        /// 
        /// </summary>
        public void UpdateHomoDomainPeptideDimers()
        {
            InitializeTable(true);

            string queryString = "Select PdbId, DomainInterfaceId From PfamPeptideInterfaces WHere DomainID = PepDomainID and ChainDomainID <> PepChainDomainID;";
            DataTable homoDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            StreamWriter dataWriter = new StreamWriter("UpdatePfamPeptideInterfaceIds.txt", true);
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow homoInterfaceRow in homoDomainInterfaceTable.Rows)
            {
                pdbId = homoInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(homoInterfaceRow["DomainInterfaceID"].ToString());
                UpdatePeptideDomainInterface(pdbId, domainInterfaceId, dataWriter);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="homoDomainPepInterfaceRow"></param>
        private void UpdatePeptideDomainInterface(string pdbId, int domainInterfaceId, StreamWriter pepInterfaceWriter)
        {
            DataTable domainPepInterfaceTable = GetDomainPeptideInterface(pdbId, domainInterfaceId);
            domainPepInterfaceTable.TableName = pfamPepInterfaceTableName;
         //   DataTable domainPepHmmSiteTable = GetDomainPeptideHmmInfo(pdbId, domainInterfaceId);

            DataTable chainPfamAssignTable = GetChainPfamAssign(pdbId);
            Dictionary<int, Dictionary<string, List<Range>>> domainRangeHash = GetDomainRangeHash(chainPfamAssignTable);

            string queryString = string.Format("Select * From ChainPeptideInterfaces " +
                " Where PdbID = '{0}' AND PepLength < {1};", pdbId, ProtCidSettings.peptideLengthCutoff);
            DataTable chainPepInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format("Select * From ChainPeptideAtomPairs Where PdbID = '{0}';", pdbId);
            DataTable atomPairTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format("Select * From ChainPeptideResiduePairs Where PdbID = '{0}';", pdbId);
            DataTable residuePairTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            int[] interactingPepAtomSeqIds = null;
            string[] interactingPepAtomResidues = null;
             int[] interactingPepResidueSeqIds = null;
            string[] interactingPepResidueResidues = null;

            int chainDomainId = Convert.ToInt32(domainPepInterfaceTable.Rows[0]["ChainDomainID"].ToString());
            int pepChainDomainId = Convert.ToInt32(domainPepInterfaceTable.Rows[0]["PepChainDomainID"].ToString());

            Dictionary<string, List<Range>> domainChainRangeHash = domainRangeHash[pepChainDomainId];
 
            int[] numOfContacts = GetNumOfContacts(domainPepInterfaceTable);
            string pepAsymChain = "";
            int numOfPepDomainAtomPairs = 0;
            int numOfPepDomainResiduePairs = 0;
            int sumOfPepDomainAtomPairs = 0;
            int sumOfPepDomainResiduePairs = 0;
            Dictionary<int, int[]> chainInterfacePepSeqIdHash = new Dictionary<int, int[]>();
            Dictionary<int, string[]> chainInterfacePepResidueHash = new Dictionary<int,string[]> ();
            Dictionary<int, int> chainInterfaceAtomPairNumHash = new Dictionary<int,int> ();
            Dictionary<int, int> chainInterfaceResiduePairNumHash = new Dictionary<int,int> ();
            int chainInterfaceId = 0;
            foreach (DataRow pepInterfaceRow in domainPepInterfaceTable.Rows)
            {
                chainInterfaceId = Convert.ToInt32 (pepInterfaceRow["InterfaceID"].ToString ());
                pepAsymChain = pepInterfaceRow["PepAsymChain"].ToString ().TrimEnd ();
                DataRow[] atomPairRows = GetPeptideDomainInterfaceContactPairs(chainInterfaceId, atomPairTable);
                DataRow[] residuePairRows = GetPeptideDomainInterfaceContactPairs(chainInterfaceId, residuePairTable);
                Range[] domainRanges = domainChainRangeHash[pepAsymChain].ToArray(); 
                numOfPepDomainAtomPairs = GetNumOfPepContacts(atomPairRows, domainRanges, out interactingPepAtomSeqIds, out interactingPepAtomResidues);
                numOfPepDomainResiduePairs = GetNumOfPepContacts(residuePairRows, domainRanges, out interactingPepResidueSeqIds, out interactingPepResidueResidues);
                chainInterfacePepSeqIdHash.Add(chainInterfaceId, interactingPepAtomSeqIds);
                chainInterfacePepResidueHash.Add(chainInterfaceId, interactingPepAtomResidues);
                chainInterfaceAtomPairNumHash.Add(chainInterfaceId, numOfPepDomainAtomPairs);
                chainInterfaceResiduePairNumHash.Add(chainInterfaceId, numOfPepDomainResiduePairs);
                sumOfPepDomainAtomPairs = sumOfPepDomainAtomPairs + numOfPepDomainAtomPairs;
                sumOfPepDomainResiduePairs = sumOfPepDomainResiduePairs + numOfPepDomainResiduePairs;
            }
            string hmmAsymChain = "";
            int hmmSeqId = 0;
            string pfamId = "";
            if (numOfContacts[0] < sumOfPepDomainAtomPairs ||
                (numOfContacts[0] == sumOfPepDomainAtomPairs && numOfContacts[1] < sumOfPepDomainResiduePairs))
            {
                ReversePeptideInterfaceInfo (domainPepInterfaceTable, chainInterfaceAtomPairNumHash, chainInterfaceResiduePairNumHash);

                DataRow[] domainAssignRows = chainPfamAssignTable.Select(string.Format("ChainDomainID = '{0}'", pepChainDomainId));

                foreach (DataRow peptideInterfaceRow in domainPepInterfaceTable.Rows)
                {
                    hmmAsymChain = peptideInterfaceRow["AsymChain"].ToString ().TrimEnd ();
                    chainInterfaceId = Convert.ToInt32(peptideInterfaceRow["InterfaceID"].ToString ());
                    DataRow chainDomainRow = GetDomainChainRow(hmmAsymChain, domainAssignRows);
                    pfamId = chainDomainRow["Pfam_ID"].ToString().TrimEnd();
                    interactingPepAtomSeqIds = (int[])chainInterfacePepSeqIdHash[chainInterfaceId];
                    interactingPepAtomResidues = (string[])chainInterfacePepResidueHash[chainInterfaceId];
                    for (int i = 0; i < interactingPepAtomSeqIds.Length; i++)
                    {
                        hmmSeqId = GetHmmSeqId(interactingPepAtomSeqIds[i], chainDomainRow);
                        if (hmmSeqId > -1)
                        {
                            DataRow dataRow = pfamInteractSiteTable.NewRow();
                            dataRow["PdbID"] = pdbId;
                            dataRow["InterfaceID"] = chainInterfaceId;
                            dataRow["DomainInterfaceID"] = domainInterfaceId;
                            dataRow["ChainDomainID"] = pepChainDomainId;
                            dataRow["AsymChain"] = hmmAsymChain;
                            dataRow["Residue"] = interactingPepAtomResidues[i];
                            dataRow["SeqID"] = interactingPepAtomSeqIds[i];
                            dataRow["PfamID"] = pfamId;
                            dataRow["HmmSeqID"] = hmmSeqId;
                            pfamInteractSiteTable.Rows.Add(dataRow);
                        }
                    }
                }

       //         DeletePfamPeptideInterfaceInfo(pdbId, domainInterfaceId);
       //         dbInsert.InsertDataIntoDBtables(domainPepInterfaceTable);
                domainPepInterfaceTable.Clear();
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamInteractSiteTable);
                pfamInteractSiteTable.Clear();

                pepInterfaceWriter.WriteLine(pdbId + domainInterfaceId.ToString ());
                pepInterfaceWriter.Flush();
            }
         
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPeptideInterfaceTable"></param>
        /// <param name="interfaceAtomPairsHash"></param>
        /// <param name="interfaceResiduePairHash"></param>
        private void ReversePeptideInterfaceInfo(DataTable domainPeptideInterfaceTable, Dictionary<int, int> interfaceAtomPairsHash, Dictionary<int, int> interfaceResiduePairHash)
        {
            int interfaceId = 0;
            for (int i = 0; i < domainPeptideInterfaceTable.Rows.Count; i++)
            {
                ReversePfamPeptideInterfaceInfo(domainPeptideInterfaceTable.Rows[i]);
                interfaceId = Convert.ToInt32(domainPeptideInterfaceTable.Rows[i]["InterfaceId"].ToString ());
                domainPeptideInterfaceTable.Rows[i]["NumOfAtomPairs"] = interfaceAtomPairsHash[interfaceId];
                domainPeptideInterfaceTable.Rows[i]["NumOfResiduePairs"] = interfaceResiduePairHash[interfaceId];
            }
            domainPeptideInterfaceTable.AcceptChanges ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPepInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetNumOfContacts(DataTable domainPepInterfaceTable)
        {
            int numOfAtomPairs = 0;
            int numOfResiduePairs = 0;
            foreach (DataRow interfaceRow in domainPepInterfaceTable.Rows)
            {
                numOfAtomPairs += Convert.ToInt32(interfaceRow["NumOfAtomPairs"].ToString ());
                numOfResiduePairs += Convert.ToInt32(interfaceRow["NumOfResiduePairs"].ToString ());
            }
            int[] numOfContacts = new int[2];
            numOfContacts[0] = numOfAtomPairs;
            numOfContacts[1] = numOfResiduePairs;
            return numOfContacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPeptideInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetChainInterfaceIds(DataTable domainPeptideInterfaceTable)
        {
            List<int> chainInterfaceIdList = new List<int> ();
            int chainInterfaceId = 0;
            foreach (DataRow peptideInterfaceRow in domainPeptideInterfaceTable.Rows)
            {
                chainInterfaceId = Convert.ToInt32(peptideInterfaceRow["InterfaceID"].ToString ());
                chainInterfaceIdList.Add(chainInterfaceId);
            }

            return chainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaceIds"></param>
        /// <param name="atomPairTable"></param>
        /// <returns></returns>
        private DataRow[] GetPeptideDomainInterfaceContactPairs (int[] chainInterfaceIds, DataTable contactPairTable)
        {
            List<DataRow> dataRowList = new List<DataRow> ();
            foreach (int chainInterfaceId in chainInterfaceIds)
            {
                DataRow[] interfaceRows = contactPairTable.Select(string.Format("InterfaceID = '{0}'", chainInterfaceId));
                dataRowList.AddRange(interfaceRows);
            }

            return dataRowList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaceIds"></param>
        /// <param name="atomPairTable"></param>
        /// <returns></returns>
        private DataRow[] GetPeptideDomainInterfaceContactPairs(int chainInterfaceId, DataTable contactPairTable)
        {
            DataRow[] contactPairRows = contactPairTable.Select(string.Format("InterfaceID = '{0}'", chainInterfaceId));
            return contactPairRows;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainPeptideInterface(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable pfamPeptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return pfamPeptideInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainPeptideHmmInfo(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeletePfamPeptideInterfaceInfo(string pdbId, int domainInterfaceId)
        {
            string deleteString = string.Format("Delete From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From PfamPeptideHmmSites Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion

        #region add pfam-peptide meta data
        /// <summary>
        /// the pfam-peptide summary info for protcid web server
        /// </summary>
        public void GetPfamPeptideInPdbMetaData()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Peptide sum info";

            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = "Select First 1 * From IPfamInPdb;";
            DataTable ipfamSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            ipfamSumInfoTable.Clear();
            ipfamSumInfoTable.TableName = "IPfamInPdb";
            DataRow ipfamSumInfoRow = ipfamSumInfoTable.NewRow();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add Pfam-Peptide sum info to table");
            ProtCidSettings.progressInfo.totalOperationNum = pfamIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamIdTable.Rows.Count;
            string pfamId = "";
            int relSeqId = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString();
                relSeqId = GetPeptideRelSeqId (pfamId);
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

                AddPfamPeptideSumInfoToTable(pfamId, relSeqId, ipfamSumInfoRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetPeptideRelSeqId(string pfamId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' AND FamilyCode2 = 'peptide';", pfamId);
            DataTable pepRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pepRelSeqIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(pepRelSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }
            return -1;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatePfamIds"></param>
        public void UpdatePfamPeptideInPdbMetaData(string[] updatePfamIds)
        {
            string queryString = "Select First 1 * From IPfamInPdb;";
            DataTable ipfamSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            ipfamSumInfoTable.Clear();
            ipfamSumInfoTable.TableName = "IPfamInPdb";
            DataRow ipfamSumInfoRow = ipfamSumInfoTable.NewRow();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-Peptide sum info to table");
            int relSeqId = 0;
            foreach (string pfamId in updatePfamIds)
            {
                relSeqId = GetPeptideRelSeqId (pfamId);

                AddPfamPeptideSumInfoToTable(pfamId, relSeqId, ipfamSumInfoRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="ipfamSumInfoRow"></param>
        private void AddPfamPeptideSumInfoToTable(string pfamId, int relSeqId, DataRow ipfamSumInfoRow)
        {
            DeletePfamPepRelSumInfo(pfamId);
            string[] ipfamEntries = GetEntriesIPfamPeptide(pfamId);
            int numEntryIPfamPep = ipfamEntries.Length;
            int numCfsIPfam = entryCf.GetNumberOfCFs(ipfamEntries);
            string[] pepEntries = null;
            int numEntryPfamPep = GetNumOfEntriesWithPfamPeptide(pfamId, ipfamEntries, out pepEntries);
            int numCfs = entryCf.GetNumberOfCFs(pepEntries);
            ipfamSumInfoRow["RelSeqID"] = relSeqId;
            ipfamSumInfoRow["PfamID1"] = pfamId;
            ipfamSumInfoRow["PfamAcc1"] = GetPfamAcc(pfamId);
            ipfamSumInfoRow["PfamID2"] = "peptide";
            ipfamSumInfoRow["PfamAcc2"] = "peptide";
            ipfamSumInfoRow["NumEntriesIPfam"] = numEntryIPfamPep;
            ipfamSumInfoRow["NumEntries"] = numEntryPfamPep;
            ipfamSumInfoRow["NumEntriesSameChain"] = 0;
            ipfamSumInfoRow["NumCfs"] = numCfs;
            ipfamSumInfoRow["NumCfsIPfam"] = numCfsIPfam;
            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, ipfamSumInfoRow);
        }    

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetEntriesIPfamPeptide(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pfamEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }

        /// <summary>
        /// the pfam-peptide summary info for protcid web server
        /// </summary>
        public void GetPfamPeptideRelationsInPdbMetaData()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Peptide sum info";

            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = "Select First 1 * From IPfamInPdb;";
            DataTable ipfamSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            ipfamSumInfoTable.Clear();
            ipfamSumInfoTable.TableName = "IPfamInPdb";
            DataRow ipfamSumInfoRow = ipfamSumInfoTable.NewRow();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add Pfam-Peptide sum info to table");
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

                AddPfamPeptideSumInfoToTable(relSeqId, ipfamSumInfoRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="ipfamSumInfoRow"></param>
        /// <returns></returns>
        private void AddPfamPeptideSumInfoToTable (int relSeqId, DataRow ipfamSumInfoRow)
        {
            string[] pfamPepCodes = GetPfamPeptideCodes(relSeqId);
            string[] pepEntries = null;
            if (pfamPepCodes[1] == "peptide")
            {
                DeletePfamPepRelSumInfo(relSeqId);
                string[] relEntries = GetRelEntriesIPfamPeptide (relSeqId);
                int numEntryIPfamPep = relEntries.Length;
                int numOfCfsIPfam = entryCf.GetNumberOfCFs(relEntries);
                int numEntryPfamPep = GetNumOfEntriesWithPfamPeptide (pfamPepCodes[0], relEntries, out pepEntries);
                int numOfCfs = entryCf.GetNumberOfCFs(pepEntries);
                ipfamSumInfoRow["RelSeqID"] = relSeqId; 
                ipfamSumInfoRow["PfamID1"] = pfamPepCodes[0];
                ipfamSumInfoRow["PfamAcc1"] = GetPfamAcc(pfamPepCodes[0]);
                ipfamSumInfoRow["PfamID2"] = pfamPepCodes[1];
                ipfamSumInfoRow["PfamAcc2"] = "peptide";
                ipfamSumInfoRow["NumEntriesIPfam"] = numEntryIPfamPep;
                ipfamSumInfoRow["NumEntries"] = numEntryPfamPep;
                ipfamSumInfoRow["NumEntriesSameChain"] = 0;
                ipfamSumInfoRow["NumCfs"] = numOfCfsIPfam;
                ipfamSumInfoRow["NumCfsIPfam"] = numOfCfs;
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, ipfamSumInfoRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamAcc(string pfamId)
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
        /// <param name="relSeqId"></param>
        private void DeletePfamPepRelSumInfo(int relSeqId)
        {
            string deleteString = string.Format("Delete From IPfamInPdb Where RelSeqID = {0};", relSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeletePfamPepRelSumInfo(string pfamId)
        {
            string deleteString = string.Format("Delete From IPfamInPdb Where PfamId1 = '{0}' AND PfamId2 = 'peptide';", pfamId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetPfamPeptideCodes (int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamPairs = new string[2];
            if (relationTable.Rows.Count > 0)
            {
                pfamPairs[0] = relationTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                pfamPairs[1] = relationTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            }
            return pfamPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelEntriesIPfamPeptide(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPeptideInterfaces WHere RelSeqID = {0};", relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] relEntries = new string[relEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                relEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return relEntries;
        }

        /// <summary>
        /// the number of structures which contains both protein chain in the Pfam and peptide chains
        /// without checking if there are actually interactions 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetNumOfEntriesWithPfamPeptide(string pfamId, string[] ipfamEntries, out string[] pepEntries)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            List<string> pepEntryList = new List<string>();
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (ipfamEntries.Contains (pdbId))
                {
                    pepEntryList.Add(pdbId);
                    continue;
                }
                string[] pepChains = GetEntryPeptideChains(pdbId);
                if (pepChains.Length > 0)
                {
                    pepEntryList.Add(pdbId);
                }
            }
            pepEntries = pepEntryList.ToArray(); 

            return pepEntryList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPeptideChains(string pdbId)
        {
            string queryString = string.Format("Select AsymID, Sequence From AsymUnit " +
               " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string sequence = "";
            List<string> pepChainList = new List<string> ();
            foreach (DataRow seqInfoRow in entryInfoTable.Rows)
            {
                sequence = seqInfoRow["Sequence"].ToString().TrimEnd();
                if (sequence.Length <= ProtCidSettings.peptideLengthCutoff)
                {
                    pepChainList.Add(seqInfoRow["AsymID"].ToString ());
                }
            }
            return pepChainList.ToArray ();
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void CheckPfamPeptideInterfaces()
        {
            bool isUpdate = true;
            InitializeTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Pfam-Peptide interactions and HMM sites.");

            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable entryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";

            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            StreamWriter entryWriter = new StreamWriter("ErrorPepEntries.txt");

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    RetrieveEntryPfamPeptideInterfaces(pdbId);

                    if (!ArePeptideInterfacesRight(pdbId, pfamPepInterfaceTable))
                    {
                        entryWriter.WriteLine(pdbId);
                        entryWriter.Flush();
/*
                        DeletePfamPeptideInterfacesData(pdbId);
                        dbInsert.InsertDataIntoDBtables(pfamInteractSiteTable);
                        dbInsert.InsertDataIntoDBtables(pfamPepInterfaceTable);*/
                    }
                    pfamInteractSiteTable.Clear();
                    pfamPepInterfaceTable.Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " : " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " map interacting sequence residue and seqid to hmm: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            entryWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <returns></returns>
        private bool ArePeptideInterfacesRight(string pdbId, DataTable pfamPepInterfaceTable)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string tableQueryString = "";
            foreach (DataRow pepInterfaceRow in pepInterfaceTable.Rows)
            {
                tableQueryString = string.Format("RelSeqID = '{0}' AND PdbID = '{1}' AND InterfaceID = '{2}' AND " + 
                    " DomainInterfaceID = '{3}' AND DomainID = '{4}' AND AsymChain = '{5}'", pepInterfaceRow["RelSeqID"].ToString (), 
                    pepInterfaceRow["PdbID"].ToString (), pepInterfaceRow["InterfaceID"].ToString (), 
                    pepInterfaceRow["DomainInterfaceID"].ToString (), pepInterfaceRow["DomainID"].ToString (), 
                    pepInterfaceRow["AsymChain"].ToString ().TrimEnd ());
                DataRow[] newPepInterfaceRows = pfamPepInterfaceTable.Select(tableQueryString);
                if (newPepInterfaceRows.Length == 0)
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns>pfams and its update entries</returns>
        public Dictionary<string, string[]> UpdateMissingPfamPeptideInterfaces()
        {
            bool isUpdate = true;
            InitializeTable(isUpdate);

            Dictionary<string, List<string>> updatePfamEntryListHash = new Dictionary<string,List<string>> ();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Pfam-Peptide interactions and HMM sites.");

            string[] updateEntries = { "3j47", "2m32", "3zpk", "4inm" };

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    string[] pepInteractPfams = RetrieveEntryPfamPeptideInterfaces(pdbId);
                    if (pepInteractPfams.Length == 0)
                    {
                        continue;
                    }
                    foreach (string pfamId in pepInteractPfams)
                    {
                        if (updatePfamEntryListHash.ContainsKey(pfamId))
                        {
                            updatePfamEntryListHash[pfamId].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            updatePfamEntryListHash.Add(pfamId, entryList);
                        }
                    }

                    DeletePfamPeptideInterfacesData(pdbId);

                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamInteractSiteTable);
                    pfamInteractSiteTable.Clear();
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamPepInterfaceTable);
                    pfamPepInterfaceTable.Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " : " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " map interacting sequence residue and seqid to hmm: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            StreamWriter dataWriter = new StreamWriter("UpdatePepInteractPfamEntries_missing.txt", true);
            List<string> updatePfamList = new List<string> (updatePfamEntryListHash.Keys);
            updatePfamList.Sort();
            Dictionary<string, string[]> updatePfamEntryHash = new Dictionary<string, string[]>();
            foreach (string updatePfam in updatePfamList)
            {
                updatePfamEntryHash.Add (updatePfam, updatePfamEntryListHash[updatePfam].ToArray ());
                dataWriter.WriteLine(updatePfam + " " + FormatEntryArray(updatePfamEntryListHash[updatePfam].ToArray ()));
            }
            dataWriter.Close();
            return updatePfamEntryHash;
        }

        private string[] GetMissingEntries()
        {
            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable chainEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> missingEntryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow chainEntryRow in chainEntryTable.Rows)
            {
                pdbId = chainEntryRow["PdbID"].ToString();
                DataRow[] pfamPepRows = pepEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                if (pfamPepRows.Length == 0)
                {
                    missingEntryList.Add(pdbId);
                }
            }
            return missingEntryList.ToArray ();
        }

        public void AddPfamIdsToPfamPepInterfaces()
        {
            string queryString = "Select Distinct PdbID, DomainID From PfamPeptideInterfaces;";
            DataTable domainTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            long domainId = 0;
            string pfamId = "";
            foreach (DataRow domainRow in domainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                pfamId = GetPfamIdFromDomainId(pdbId, domainId);
                AddPfamToTable(pdbId, domainId, pfamId, "PfamID");
            }

            queryString = "Select Distinct PdbId, PepDomainID From PfamPeptideInterfaces Where PepDomainId > -1;";
            domainTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow domainRow in domainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainRow["PepDomainID"].ToString ());
                pfamId = GetPfamIdFromDomainId(pdbId, domainId);
                AddPfamToTable(pdbId, domainId, pfamId, "PepPfamID");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="pfamId"></param>
        private void AddPfamToTable(string pdbId, long domainId, string pfamId, string colName)
        {
            string updateString = string.Format("Update PfamPeptideInterfaces Set {0} = '{1}' WHere PdbID = '{2}' AND DomainID = {3};", 
                colName, pfamId, pdbId, domainId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string GetPfamIdFromDomainId(string pdbId, long domainId)
        {
            string queryString = string.Format("Select Pfam_ID From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamId = "-";
            if (pfamIdTable.Rows.Count > 0)
            {
                pfamId = pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return pfamId;
        }
        /// <summary>
        /// 
        /// </summary>
        public void AddPfamPeptideRelationsIntoRelationTable()
        {
            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                AddPfamPeptideRelationToTable(relSeqId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void AddPfamPeptideRelationToTable(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);
            relationTable.TableName = "PfamDomainFamilyRelation";
            if (relationTable.Rows.Count > 0)
            {
                return;
            }
            queryString = string.Format("Select First 1 DomainID, PepDomainID From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long domainId = Convert.ToInt64(pfamPepInterfaceTable.Rows[0]["DomainID"].ToString());
            long pepDomainId = Convert.ToInt64(pfamPepInterfaceTable.Rows[0]["PepDomainID"].ToString());
            string pfamId = GetPfamId(domainId);
            string pepPfamId = "peptide";
            if (pepDomainId != -1)
            {
                pepPfamId = GetPfamId(pepDomainId);
            }
            DataRow relationRow = relationTable.NewRow();
            relationRow["RelSeqID"] = relSeqId;
            relationRow["FamilyCode1"] = pfamId;
            relationRow["FamilyCode2"] = pepPfamId;
            relationRow["ClanSeqID"] = -1;
            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, relationRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string GetPfamId(long domainId)
        {
            string queryString = string.Format("Select Pfam_ID From PdbPfam Where DomainID = {0};", domainId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (pfamIdTable.Rows.Count > 0)
            {
                return pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePfamPeptideInterfaces()
        {
         //   string[] updateEntries = ReadEntriesToBeUpdated();
            string[] updateEntries = { "1eq8", "1p23", "2xji"};
            UpdatePfamPeptideInterfaces(updateEntries);
        }

        private string[] ReadEntriesToBeUpdated()
        {
            string line = "";
            List<string> entryList = new List<string> ();
            StreamReader dataReader = new StreamReader("EntriesNotInPepTable.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line);
            }
            dataReader.Close();
            return entryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        public void FindEntriesWithNoPeptideInterfaceInfo()
        {
            StreamWriter entryWriter = new StreamWriter("EntriesNotInPepTable.txt");
            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces ;";
            DataTable entryInChainPepTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";
            List<string> pdbIdList = new List<string> ();
            
            foreach (DataRow entryRow in entryInChainPepTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (! AreAllChainInterfacesInPfamPeptide(pdbId))
                {
                    pdbIdList.Add(pdbId);
                    entryWriter.WriteLine(pdbId);
                }
            }
            entryWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool AreAllChainInterfacesInPfamPeptide(string pdbId)
        {
            string queryString = string.Format("Select Distinct PdbID, InterfaceID From ChainPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable chainPepInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            queryString = string.Format("Select Distinct PdbID, InterfaceID From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pfamPeptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int interfaceId = 0;
            foreach (DataRow interfaceRow in chainPepInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                DataRow[] pfamPepInterfaceRows = pfamPeptideInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
                if (pfamPepInterfaceRows.Length == 0)
                {
                    return false;
                }
            }
            return true;
        }

        public void UpdatePfamPeptideInterfaceRelSeqIds ()
        {
            DbConnect updateDbConnect = new DbConnect();
            updateDbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=C:\\Firebird\\Pfam30_Update\\ProtCid.fdb";
            Dictionary<int, string[]> relPfamPairHash = GetRelSeqFamilyPairsHash(updateDbConnect);
            
            int relSeqId = 0;
            char isReversed = '0';
            string updateString = "";
            List<int> relIdList = new List<int> (relPfamPairHash.Keys);
            relIdList.Sort();
            foreach (int relId in relIdList)
            {
                string[] pfamPair = (string[])relPfamPairHash[relId];
                relSeqId = GetRelSeqId(pfamPair[0], pfamPair[1], out isReversed);
                updateString = string.Format("Update PfamPeptideInterfaces Set NewRelSeqID = {0} Where RelSeqID = {1};", relSeqId, relId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
                updateString = string.Format("Update PfamPepInterfaceClusters Set NewRelSeqID = {0} Where RelSeqID = {1};", relSeqId, relId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
            }            
        }

        private Dictionary<int, string[]> GetRelSeqFamilyPairsHash (DbConnect dbConnect)
        {
            string queryString = "Select Distinct RelSeqID From PfamPeptideInterfaces;";
            DataTable pepRelTable = dbQuery.Query(dbConnect, queryString);
            Dictionary<int, string[]> relFamilyPairHash = new Dictionary<int, string[]>();
            int relSeqId = 0;
            foreach (DataRow relRow in pepRelTable.Rows)
            {
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString ());
                string[] pfamPair = GetRelSeqPfamPair(dbConnect, relSeqId);
                relFamilyPairHash.Add(relSeqId, pfamPair);
            }
            return relFamilyPairHash;
        }

        private string[] GetRelSeqPfamPair (DbConnect dbConnect, int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = dbQuery.Query(dbConnect, queryString);
            string[] pfamPair = new string[2];
            if (pfamPairTable.Rows.Count > 0)
            {
                pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            }
            return pfamPair;
        }
        #endregion
    }
}
