using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPepInterfaceStatInfo
    {
        private DbQuery dbQuery = new DbQuery();

        #region output Chain-peptide info
        private int numOfAtomPairsCutoff = 5;
        private int numOfResiduePairsCutoff = 10;
        public void PrintPeptideContactsInfo()
        {
            StreamWriter dataWriter = new StreamWriter("PfamDomain\\PfamPeptides\\PeptideContactsInfo_cutoff.txt");
            dataWriter.WriteLine("PdbID\tBuID\tPeptideChain\tPepSymmetryString\tTotalAtomPairs\tMaxAtomPairs\tMinAtomPairs\t" +
                "TotalResiduePairs\tMaxResiduePairs\tMinResiduePairs\tChainMaxAtomPairs\tChainMaxResiduePairs" +
                "\tNumOfProtChains\tPeptideLength\tPeptidePfamArch");
            string queryString = "Select Distinct PdbID, BuID From ChainPeptideInterfaces " +
                " where BuID = '0'  AND PepLength <= 30;";
            DataTable buTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";
            string buId = "";
            Dictionary<string, string> chainPfamArchHash = new Dictionary<string,string> ();
            foreach (DataRow buRow in buTable.Rows)
            {
                chainPfamArchHash.Clear();
                pdbId = buRow["PdbID"].ToString();
                buId = buRow["BuID"].ToString();
                GetPeptideInteractingContacts(pdbId, buId, chainPfamArchHash, dataWriter);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintChainPeptideInteractInfo()
        {
            StreamWriter dataWriter = new StreamWriter("PfamDomain\\PfamPeptides\\chainPeptidePfamRelations_50.txt");
            string queryString = "Select Distinct PdbID, InterfaceID From PfamInteractSites;";
            DataTable chainPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int interfaceId = 0;
            List<string> pdbIdList = new List<string>();
            List<string> pfamRelList = new List<string>();
            List<string> chainPfamList = new List<string>();
            string chainPfam = "";
            List<string> pepPfamList = new List<string>();
            string peptidePfam = "";
            string pfamRel = "";
            int peptideLength = 0;
            foreach (DataRow interfaceRow in chainPepInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                chainPfam = GetChainPfamId(pdbId, interfaceId);
                peptidePfam = GetPeptidePfamId(pdbId, interfaceId, out peptideLength);
                pfamRel = chainPfam + ";" + peptidePfam;
                /*    if (peptideLength > 30)
                      {
                          continue;
                      }*/
                if (chainPfam != "" && peptidePfam != "")
                {
                    if (!pdbIdList.Contains(pdbId))
                    {
                        pdbIdList.Add(pdbId);
                    }
                    if (!chainPfamList.Contains(chainPfam))
                    {
                        chainPfamList.Add(chainPfam);
                    }
                    if (!pepPfamList.Contains(peptidePfam))
                    {
                        pepPfamList.Add(peptidePfam);
                    }
                    if (!pfamRelList.Contains(pfamRel))
                    {
                        pfamRelList.Add(pfamRel);
                    }
                }
                dataWriter.WriteLine(pdbId + "\t" + interfaceId.ToString() + "\t" +
                    chainPfam + "\t" + peptidePfam + "\t" + pfamRel);
            }
            dataWriter.WriteLine("#PDB: " + pdbIdList.Count.ToString());
            dataWriter.WriteLine("#Chain Pfam IDs: " + chainPfamList.Count.ToString());
            dataWriter.WriteLine("#Peptide Pfam IDs: " + pepPfamList.Count.ToString());
            dataWriter.WriteLine("#Chain Peptide Pfam Relations: " + pfamRelList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetChainPfamId(string pdbId, int interfaceId)
        {
            string querystring = string.Format("Select PfamID from PfamInteractSites " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( querystring);
            return pfamIdTable.Rows[0]["PfamID"].ToString().TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetPeptidePfamId(string pdbId, int interfaceId, out int peptideLength)
        {
            peptideLength = 0;
            string querystring = string.Format("Select PepEntityID, PepLength From ChainPeptideInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable pepEntityTable = dbQuery.Query(ProtCidSettings.buCompConnection, querystring);
            int pepEntityId = 0;
            if (pepEntityTable.Rows.Count > 0)
            {
                pepEntityId = Convert.ToInt32(pepEntityTable.Rows[0]["PepEntityID"].ToString());
                peptideLength = Convert.ToInt32(pepEntityTable.Rows[0]["PepLength"].ToString());
                string pfamId = GetPfamId(pdbId, pepEntityId);
                return pfamId;
            }
            return "";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="dataWriter"></param>
        private void GetPeptideInteractingContacts(string pdbId, string buId, Dictionary<string, string> chainPfamArchHash, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From ChainPeptideInterfaces Where PdbID = '{0}' AND BuID = '{1}' AND (NumOfAtomPairs >= {2} OR NumOfResiduePairs >= {3});",
                pdbId, buId, numOfAtomPairsCutoff, numOfResiduePairsCutoff);
            DataTable chainPeptideTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string[] pepChains = GetPeptideChains(chainPeptideTable);
            string[] chainsMaxContacts = null;
            string dataLine = "";
            string chainPfamArch = "";
            foreach (string pepChain in pepChains)
            {
                string[] pepChainFields = pepChain.Split(';');
                chainPfamArch = GetPeptideChainPfamArch(pdbId, pepChainFields[0], chainPfamArchHash);
                DataRow[] pepChainInterfaceRows = chainPeptideTable.Select
                    (string.Format("PepAsymChain = '{0}' AND PepSymmetryString = '{1}'",
                    pepChainFields[0], pepChainFields[1]));
                int[] contacts = GetPeptideContactInfo(pepChainInterfaceRows, out chainsMaxContacts);
                dataLine = pdbId + "\t" + buId + "\t" + pepChainFields[0] + "\t" + pepChainFields[1] + "\t" +
                    contacts[0].ToString() + "\t" + contacts[1].ToString() + "\t" + contacts[2].ToString() + "\t" +
                    contacts[3].ToString() + "\t" + contacts[4].ToString() + "\t" + contacts[5].ToString() + "\t" +
                    chainsMaxContacts[0] + "\t" + chainsMaxContacts[1] + "\t" + contacts[6].ToString() + "\t" +
                    pepChainInterfaceRows[0]["PepLength"].ToString() + "\t" + chainPfamArch;
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainPfamArchHash"></param>
        /// <returns></returns>
        private string GetPeptideChainPfamArch(string pdbId, string asymChain, Dictionary<string, string> chainPfamArchHash)
        {
            string chainPfamArch = "-";
            if (chainPfamArchHash.ContainsKey(asymChain))
            {
                chainPfamArch = chainPfamArchHash[asymChain];
            }
            else
            {
                string queryString = string.Format("Select EntityID From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
                DataTable entityIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                if (entityIdTable.Rows.Count > 0)
                {
                    int entityId = Convert.ToInt32(entityIdTable.Rows[0]["EntityID"].ToString());
                    queryString = string.Format("Select PfamArch From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
                    DataTable pfamArchTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                    if (pfamArchTable.Rows.Count > 0)
                    {
                        chainPfamArch = pfamArchTable.Rows[0]["PfamArch"].ToString().TrimEnd();
                    }
                }

                chainPfamArchHash.Add(asymChain, chainPfamArch);
            }
            return chainPfamArch;
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepChainInterfaceRows"></param>
        /// <returns></returns>
        private int[] GetPeptideContactInfo(DataRow[] pepChainInterfaceRows, out string[] chainsWithMaxContacts)
        {
            int[] numOfContacts = new int[7];
            int totalNumOfAtomPairs = 0;
            int totalNumOfResiduePairs = 0;
            int maxNumOfAtomPairs = 0;
            int minNumOfAtomPairs = 100000;
            int maxNumOfResiduePairs = 0;
            int minNumOfResiduePairs = 100000;
            int numOfAtomPairs = 0;
            int numOfResiduePairs = 0;
            string chainMaxAtomPairs = "";
            string chainMaxResiduePairs = "";
            List<string> protChainList = new List<string>();
            string protChain = "";
            foreach (DataRow pepChainRow in pepChainInterfaceRows)
            {
                numOfAtomPairs = Convert.ToInt32(pepChainRow["NumOfAtomPairs"].ToString());
                numOfResiduePairs = Convert.ToInt32(pepChainRow["NumOfResiduePairs"].ToString());

                protChain = pepChainRow["AsymChain"].ToString().TrimEnd();

                totalNumOfAtomPairs += numOfAtomPairs;
                totalNumOfResiduePairs += numOfResiduePairs;

                if (maxNumOfAtomPairs < numOfAtomPairs)
                {
                    maxNumOfAtomPairs = numOfAtomPairs;
                    chainMaxAtomPairs = pepChainRow["AsymChain"].ToString().TrimEnd();
                }
                if (minNumOfAtomPairs > numOfAtomPairs)
                {
                    minNumOfAtomPairs = numOfAtomPairs;
                }
                if (maxNumOfResiduePairs < numOfResiduePairs)
                {
                    maxNumOfResiduePairs = numOfResiduePairs;
                    chainMaxResiduePairs = pepChainRow["AsymChain"].ToString().TrimEnd();
                }
                if (minNumOfResiduePairs > numOfResiduePairs)
                {
                    minNumOfResiduePairs = numOfResiduePairs;
                }

                if (!protChainList.Contains(protChain))
                {
                    protChainList.Add(protChain);
                }
            }
            chainsWithMaxContacts = new string[2];
            chainsWithMaxContacts[0] = chainMaxAtomPairs;
            chainsWithMaxContacts[1] = chainMaxResiduePairs;

            numOfContacts[0] = totalNumOfAtomPairs;
            numOfContacts[1] = maxNumOfAtomPairs;
            numOfContacts[2] = minNumOfAtomPairs;
            numOfContacts[3] = totalNumOfResiduePairs;
            numOfContacts[4] = maxNumOfResiduePairs;
            numOfContacts[5] = minNumOfResiduePairs;
            numOfContacts[6] = protChainList.Count;
            return numOfContacts;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPeptideInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetPeptideChains(DataTable chainPeptideInterfaceTable)
        {
            List<string> pepChainList = new List<string>();
            string pepChain = "";
            foreach (DataRow chainPepRow in chainPeptideInterfaceTable.Rows)
            {
                pepChain = chainPepRow["PepAsymChain"].ToString().TrimEnd() + ";" +
                    chainPepRow["PepSymmetryString"].ToString().TrimEnd();
                if (!pepChainList.Contains(pepChain))
                {
                    pepChainList.Add(pepChain);
                }
            }
            string[] pepChains = new string[pepChainList.Count];
            pepChainList.CopyTo(pepChains);
            return pepChains;
        }
        #endregion

        #region multi-chain domains for prior knowledge


        public void OutputMultiChainPeptideInterfaces()
        {
            StreamWriter dataWriter = new StreamWriter("MultiChainDomains_peptide.txt");
            string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) AS EntityCount From PdbPfam " +
                "Group By PdbID, DomainID;";
            DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            long domainId = 0;
            int entityCount = 0;
            string pdbId = "";
            List<string> entryList = new List<string>();
            foreach (DataRow domainEntityCountRow in domainEntityCountTable.Rows)
            {
                domainId = Convert.ToInt64(domainEntityCountRow["DomainID"].ToString());
                entityCount = Convert.ToInt32(domainEntityCountRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pdbId = domainEntityCountRow["PdbID"].ToString();
                    if (DoesEntryContainPeptideInterfaces(pdbId))
                    {
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                            dataWriter.WriteLine(pdbId);
                        }
                    }
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool DoesEntryContainPeptideInterfaces(string pdbId)
        {
            string queryString = string.Format("Select * From ChainPeptideInterfaces Where PdbID = '{0}';",
                pdbId);
            DataTable chainPeptideTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (chainPeptideTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void OutputMultiChainDomainInfo()
        {
            StreamWriter dataWriter = new StreamWriter("MultiChainDomains.txt");
            string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) AS EntityCount From PdbPfam " +
                "Group By PdbID, DomainID;";
            DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            long domainId = 0;
            int entityCount = 0;
            string pdbId = "";
            string dataLine = "";
            foreach (DataRow domainEntityCountRow in domainEntityCountTable.Rows)
            {
                domainId = Convert.ToInt64(domainEntityCountRow["DomainID"].ToString());
                entityCount = Convert.ToInt32(domainEntityCountRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pdbId = domainEntityCountRow["PdbID"].ToString();
                    Dictionary<int, List<string>> entityChainHash = GetEntityAsymChainHash(pdbId);
                    int[] domainEntities = GetDomainEntities(domainId);
                    foreach (int domainEntity in domainEntities)
                    {
                        List<string> chainList = entityChainHash[domainEntity];
                        dataLine = pdbId + "\t" + domainId.ToString() + "\t" + entityCount.ToString() + "\t" +
                            domainEntity.ToString() + "\t" + FormatChainList(chainList);
                        if (chainList.Count > 1)
                        {
                            dataLine += "\t1";
                        }
                        else
                        {
                            dataLine += "\t0";
                        }
                        dataWriter.WriteLine(dataLine);
                    }
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamsWithMultiChainAndSingleChain()
        {
            string[] multiChainPfams = GetMultiChainPfamIds();
            StreamWriter dataWriter = new StreamWriter("MultiChainPfams.txt");
            dataWriter.WriteLine("PfamID\t#MultiChainEntries\t#SingleChainEntries\tBestSingleChainStruct");
            string dataLine = "";
            string singleChainBestStruct = "";
            foreach (string pfamId in multiChainPfams)
            {
                int[] entryCounts = DoesPfamHaveSingleChainDomains(pfamId, out singleChainBestStruct);
                dataLine = pfamId + "\t" + entryCounts[0].ToString() + "\t" +
                    entryCounts[1].ToString() + "\t" + singleChainBestStruct;
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMultiChainPfamIds()
        {
            string queryString = "Select PdbID, DomainID, Pfam_ID, Count(Distinct EntityID) AS EntityCount From PdbPfam " +
               "Group By PdbID, DomainID, Pfam_ID;";
            DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int entityCount = 0;
            string pfamId = "";
            List<string> multiChainPfamList = new List<string> ();
            foreach (DataRow domainEntityCountRow in domainEntityCountTable.Rows)
            {
                entityCount = Convert.ToInt32(domainEntityCountRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pfamId = domainEntityCountRow["Pfam_ID"].ToString().TrimEnd();
                    if (!multiChainPfamList.Contains(pfamId))
                    {
                        multiChainPfamList.Add(pfamId);
                    }
                }
            }

            return multiChainPfamList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private int[] DoesPfamHaveSingleChainDomains(string pfamId, out string bestSingleStruct)
        {
            string queryString = string.Format("Select PdbID, DomainID, HmmStart, HmmEnd, Evalue, DomainType " +
                    " From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAssignTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> multiChainEntryList = new List<string>();
            List<string> singleChainEntryList = new List<string>();
            string domainType = "";
            string pdbId = "";
            bestSingleStruct = "";
            string entryDomain = "";
            Dictionary<string, int> domainHmmAlignLengthHash = new Dictionary<string,int> ();
            foreach (DataRow pfamAssignRow in pfamAssignTable.Rows)
            {
                domainType = pfamAssignRow["DomainType"].ToString().TrimEnd();
                pdbId = pfamAssignRow["PdbID"].ToString();
                if (domainType == "c")
                {
                    if (!multiChainEntryList.Contains(pdbId))
                    {
                        multiChainEntryList.Add(pdbId);
                    }
                }
                else
                {
                    int hmmAlignLength = Convert.ToInt32(pfamAssignRow["HmmEnd"].ToString()) -
                        Convert.ToInt32(pfamAssignRow["HmmStart"].ToString()) + 1;
                    entryDomain = pfamAssignRow["PdbID"].ToString() +
                        pfamAssignRow["DomainID"].ToString();
                    if (domainHmmAlignLengthHash.ContainsKey(entryDomain))
                    {
                        int totalHmmAlignLength = (int)domainHmmAlignLengthHash[entryDomain];
                        totalHmmAlignLength += hmmAlignLength;
                        domainHmmAlignLengthHash[entryDomain] = totalHmmAlignLength;
                    }
                    else
                    {
                        domainHmmAlignLengthHash.Add(entryDomain, hmmAlignLength);
                    }
                    if (!singleChainEntryList.Contains(pdbId))
                    {
                        singleChainEntryList.Add(pdbId);
                    }
                }
            }
            int bestHmmAlignLength = 0;
            foreach (string lsEntryDomain in domainHmmAlignLengthHash.Keys)
            {
                int totalHmmAlignLength = (int)domainHmmAlignLengthHash[lsEntryDomain];
                if (bestHmmAlignLength < totalHmmAlignLength)
                {
                    bestHmmAlignLength = totalHmmAlignLength;
                    bestSingleStruct = lsEntryDomain;
                }
            }
            int[] pfamEntryCounts = new int[2];
            pfamEntryCounts[0] = multiChainEntryList.Count;
            pfamEntryCounts[1] = singleChainEntryList.Count;
            return pfamEntryCounts;
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
            int modelLength = -1;
            if (modelLengthTable.Rows.Count > 0)
            {
                modelLength = Convert.ToInt32(modelLengthTable.Rows[0]["ModelLength"].ToString());
            }
            return modelLength;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetEntityAsymChainHash(string pdbId)
        {
            string queryString = string.Format("Select EntityId, AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';",
                pdbId);
            DataTable asymChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            Dictionary<int, List<string>> entityAsymChainHash = new Dictionary<int,List<string>> ();
            int entityId = 0;
            string asymChain = "";
            foreach (DataRow chainRow in asymChainTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString());
                asymChain = chainRow["AsymID"].ToString().TrimEnd();
                if (entityAsymChainHash.ContainsKey(entityId))
                {
                    entityAsymChainHash[entityId].Add(asymChain);
                }
                else
                {
                    List<string> chainList = new List<string> ();
                    chainList.Add(asymChain);
                    entityAsymChainHash.Add(entityId, chainList);
                }
            }
            return entityAsymChainHash;
        }

        private int[] GetDomainEntities(long domainId)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbPfam Where DomainID = {0} Order By EntityID;", domainId);
            DataTable domainEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int[] domainEntities = new int[domainEntityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in domainEntityTable.Rows)
            {
                domainEntities[count] = Convert.ToInt32(entityRow["EntityID"].ToString());
                count++;
            }
            return domainEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainList"></param>
        /// <returns></returns>
        private string FormatChainList(List<string> chainList)
        {
            string chainString = "";
            foreach (string chain in chainList)
            {
                chainString += (chain + ",");
            }
            return chainString.TrimEnd(',');
        }
        #endregion

        #region peptide pfam assignments
        private int PeptideLength = 30;
        public void PrintPfamPeptideInfo()
        {
            StreamWriter dataWriter = new StreamWriter("PfamDomain/PfamPeptides/PeptidePfamInfo_human_30.txt");
            string queryString = "Select Distinct PdbID, EntityID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            queryString = "Select UnpCode, Disorder_Pred From HumanSeqInfo;";
            DataTable disPredTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            int entityId = 0;
            int seqLength = 0;
            string dataLine = "";
            string unpCode = "";
            string pfamId = "";
            List<string> pfamIdList = new List<string>();
            List<string> pfamIdAList = new List<string>();
            List<string> unpCodeList = new List<string>();
            string pfamType = "";
            //     string disPred = "";
            int numOfDisorder = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                pfamId = GetPfamId(pdbId, entityId);
                if (IsEntityPeptide(pdbId, entityId, out seqLength))
                {
                    pfamType = GetPfamType(pfamId);
                    unpCode = GetUnpCode(pdbId, entityId);
                    //     disPred = GetDisorderPrediction(unpCode);
                    numOfDisorder = GetNumberOfDisorderResidues(pdbId, entityId, unpCode, disPredTable);
                    dataLine = pdbId + "\t" + entityId.ToString() + "\t" + pfamId + "\t" +
                                seqLength.ToString() + "\t" + unpCode + "\t" + pfamType + "\t" + numOfDisorder.ToString();
                    dataWriter.WriteLine(dataLine);

                    if (!pfamIdList.Contains(pfamId))
                    {
                        pfamIdList.Add(pfamId);
                    }
                    if (!unpCodeList.Contains(unpCode))
                    {
                        unpCodeList.Add(unpCode);
                    }
                    if (pfamType != "B")
                    {
                        if (!pfamIdAList.Contains(pfamId))
                        {
                            pfamIdAList.Add(pfamId);
                        }
                    }
                }
            }
            dataWriter.WriteLine("#Pfam IDs: " + pfamIdList.Count.ToString());
            dataWriter.WriteLine("#Pfam A IDs: " + pfamIdAList.Count.ToString());
            dataWriter.WriteLine("#Unp codes: " + unpCodeList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string GetDisorderPrediction(string unpCode, DataTable disPredTable)
        {
            //     string querystring = string.Format("Select Disorder_Pred From HumanSeqInfo Where UnpCode = '{0}';", unpCode);
            //     DataTable disPredTable = dbQuery.Query(querystring);
            DataRow[] disPredRows = disPredTable.Select(string.Format("UnpCode = '{0}'", unpCode));
            if (disPredRows.Length > 0)
            {
                return disPredRows[0]["Disorder_Pred"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private int GetNumberOfDisorderResidues(string pdbId, int entityId, string unpCode, DataTable disPredTable)
        {
            Range[] dbRanges = GetEntityUnpRanges(pdbId, entityId, unpCode);
            string disPred = GetDisorderPrediction(unpCode, disPredTable);
            if (disPred == "")
            {
                return -1;
            }
            int numOfDisorder = 0;
            foreach (Range range in dbRanges)
            {
                for (int i = range.startPos - 1; i < range.endPos; i++)
                {
                    if (disPred.Length <= i)
                    {
                        continue;
                    }
                    if (disPred[i] == 'D' || disPred[i] == 'd')
                    {
                        numOfDisorder++;
                    }
                }
            }
            return numOfDisorder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private Range[] GetEntityUnpRanges(string pdbId, int entityId, string unpCode)
        {
            string queryString = string.Format("Select Distinct RefID From PdbDbRefSifts " +
                " Where PdbID = '{0}' AND EntityID = {1} AND DbCode = '{2}';", pdbId, entityId, unpCode);
            DataTable refIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int refId = 0;
            List<Range> rangeList = new List<Range> ();
            foreach (DataRow refIdRow in refIdTable.Rows)
            {
                refId = Convert.ToInt32(refIdRow["RefID"].ToString());
                queryString = string.Format("Select Distinct DbAlignBeg, DbAlignEnd From PdbDbRefSeqSifts " +
                    " Where PdbID = '{0}' AND RefID = {1};", pdbId, refId);
                DataTable dbSeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                foreach (DataRow dbSeqRow in dbSeqTable.Rows)
                {
                    Range range = new Range();
                    range.startPos = Convert.ToInt32(dbSeqRow["DbAlignBeg"].ToString());
                    range.endPos = Convert.ToInt32(dbSeqRow["DbAlignEnd"].ToString());
                    rangeList.Add(range);
                }
            }
            return rangeList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamType(string pfamId)
        {
            if (pfamId.IndexOf("Pfam-B") > -1)
            {
                return "B";
            }
            return "A";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="seqLength"></param>
        /// <returns></returns>
        private bool IsEntityPeptide(string pdbId, int entityId, out int seqLength)
        {
            seqLength = -1;
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (seqTable.Rows.Count > 0)
            {
                seqLength = seqTable.Rows[0]["Sequence"].ToString().Length;
            }
            if (seqLength <= PeptideLength)
            {
                return true;
            }
            return false;
        }

        private string GetUnpCode(string pdbId, int entityId)
        {
            string queryString = string.Format("Select DbCode From PdbDbRefSifts " +
                " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (dbCodeTable.Rows.Count > 0)
            {
                return dbCodeTable.Rows[0]["DbCode"].ToString().TrimEnd();
            }
            return "";
        }

        private string GetPfamId(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Pfam_ID From PdbPfam Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (pfamIdTable.Rows.Count > 0)
            {
                return pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion
    }
}
