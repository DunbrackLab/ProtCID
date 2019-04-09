using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace BuCompLib.BuInterfaces
{
    public class AsuIntraChainDomainInterfaces
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private ChainContact domainContact = new ChainContact();
        private BuDomainInterfaces chainDomainInterfaces = new BuDomainInterfaces();
        private AsuInfoFinder asuInfoFinder = new AsuInfoFinder();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainAtomsHash"></param>
        public void GetIntraChainDomainInterfaces(string pdbId, Dictionary<string, AtomInfo[]> chainAtomsHash)
        {
            string queryString = string.Format ("Select * From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainDefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            int domainInterfaceId = 1;
            string asymChain = "";
            int entityId = -1;

            List<string> chainSymOpList = new List<string> (chainAtomsHash.Keys);
            chainSymOpList.Sort ();
            foreach (string chainSymOpString in chainSymOpList)
            {
                asymChain = GetAsymChain(chainSymOpString);
                entityId = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain);
                DataRow[] domainDefRows = domainDefTable.Select(string.Format ("EntityID = '{0}'", entityId));
                if (domainDefRows.Length < 2)
                {
                    continue;
                }
                AtomInfo[] chainAtoms = (AtomInfo[])chainAtomsHash[chainSymOpString];

                Dictionary<long, AtomInfo[]> chainDomainAtomsHash = GetDomainAtoms(domainDefRows, chainAtoms);

                // the interacted domain pairs intra-chain
                string[] interactedDomainPairs = GetInteractedDomainPairs (chainDomainAtomsHash);

                // insert domain-domain interfaces into table
                InsertDataIntoTable(pdbId, asymChain, ref domainInterfaceId, interactedDomainPairs, domainDefTable);
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, BuCompTables.intraChainDomainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChainSymOpString"></param>
        /// <returns></returns>
        private string GetAsymChain(string asymChainSymOpString)
        {
            int chainIdx = asymChainSymOpString.IndexOf("_");
            if (chainIdx > -1)
            {
                return asymChainSymOpString.Substring(0, chainIdx);
            }
            else
            {
                return asymChainSymOpString;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <param name="chainAtoms"></param>
        /// <returns></returns>
        private Dictionary<long, AtomInfo[]> GetDomainAtoms(DataRow[] domainDefRows, AtomInfo[] chainAtoms)
        {
            Dictionary<long, AtomInfo[]> domainAtomsHash = new Dictionary<long, AtomInfo[]>();
            int domainStart = -1;
            int domainEnd = -1;
            long domainId = -1;
            foreach (DataRow domainDefRow in domainDefRows)
            {
                domainId = Convert.ToInt64(domainDefRow["DomainID"].ToString ());
                
                domainStart = Convert.ToInt32(domainDefRow["SeqStart"].ToString ());
                domainEnd = Convert.ToInt32(domainDefRow["SeqEnd"].ToString ());

                AtomInfo[] domainAtoms = GetDomainAtoms(chainAtoms, domainStart, domainEnd);
                if (domainAtoms.Length > 0)
                {
                    if (domainAtomsHash.ContainsKey(domainId))
                    {
                        AtomInfo[] domainRegionAtoms = (AtomInfo[])domainAtomsHash [domainId];
                        List<AtomInfo> domainAtomList = new List<AtomInfo> (domainRegionAtoms);
                        domainAtomList.AddRange (domainAtoms);
                        domainAtomsHash[domainId] = domainAtomList.ToArray ();
                    }
                    else
                    {
                        domainAtomsHash.Add(domainId, domainAtoms);
                    }
                }
            }
            return domainAtomsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="domainStart"></param>
        /// <param name="domainEnd"></param>
        /// <returns></returns>
        private AtomInfo[] GetDomainAtoms(AtomInfo[] chainAtoms, int domainStart, int domainEnd)
        {
            List<AtomInfo> domainAtomsList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = ParseHelper.ConvertSeqToInt(atom.seqId);
                if (seqId <= domainEnd && seqId >= domainStart)
                {
                    domainAtomsList.Add(atom);
                }
            }
            return domainAtomsList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainAtomsHash"></param>
        /// <returns></returns>
        private string[] GetInteractedDomainPairs (Dictionary<long, AtomInfo[]> domainAtomsHash)
        {
            List<string> domainPairList = new List<string> ();

            List<long> domainList = new List<long> (domainAtomsHash.Keys);
            for (int i = 0; i < domainList.Count; i++)
            {
                for (int j = i + 1; j < domainList.Count; j++)
                {
                    ChainContactInfo domainInteraction =
                        domainContact.GetDomainContactInfo(domainAtomsHash[domainList[i]], domainAtomsHash[domainList[j]]);
                    if (domainInteraction == null)
                    {
                        continue;
                    }

                    string domainPair = domainList[i].ToString() + "_" + domainList[j].ToString();
                    domainPairList.Add(domainPair);
                }
            }
            return domainPairList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interactedDomainPairs"></param>
        private void InsertDataIntoTable(string pdbId, string asymChain, ref int domainInterfaceId,
            string[] interactedDomainPairs, DataTable domainDefTable)
        {
            int relSeqId = -1;
            char isReversed;

            foreach (string interactedDomainPair in interactedDomainPairs)
            {
                string[] domainIds = interactedDomainPair.Split('_');
                relSeqId = GetRelSeqID(Convert.ToInt64(domainIds[0]), Convert.ToInt64(domainIds[1]), 
                    domainDefTable, out isReversed);
                DataRow dataRow = BuCompTables.intraChainDomainInterfaceTable.NewRow();
                dataRow["PdbID"] = pdbId;
                dataRow["AsymChain"] = asymChain;
                dataRow["DomainInterfaceId"] = domainInterfaceId;
                domainInterfaceId++;
                dataRow["DomainID1"] = domainIds[0];
                dataRow["DomainID2"] = domainIds[1];
                dataRow["RelSeqID"] = relSeqId;
                dataRow["IsReversed"] = isReversed;
                BuCompTables.intraChainDomainInterfaceTable.Rows.Add(dataRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainDefTable"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        private int GetRelSeqID(long domainId1, long domainId2, DataTable domainDefTable, out char isReversed)
        {
            string familyCode1 = GetFamilyCode(domainId1, domainDefTable);
            string familyCode2 = GetFamilyCode(domainId2, domainDefTable);
            isReversed = '0';

            int relSeqId = chainDomainInterfaces.GetRelSeqId(familyCode1, familyCode2, out isReversed);

            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <param name="domainDefTable"></param>
        /// <returns></returns>
        private string GetFamilyCode(long domainId, DataTable domainDefTable)
        {
            DataRow[] domainDefRows = domainDefTable.Select(string.Format ("DomainID = {0}", domainId));
            if (domainDefRows.Length > 0)
            {
                return domainDefRows[0]["Pfam_Acc"].ToString().TrimEnd();
            }
            return "-";
        }

        #region domain-domain interactions in the asu
        private BiolUnitRetriever buRetriever = new BiolUnitRetriever();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainAtomsHash"></param>
        public Dictionary<string, List<string>> GetIntraChainDomainInterfaces(string pdbId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PdbPfam " + 
                " Where PdbID = '{0}' AND (IsWeak = '0' OR IsUpdated = '1' OR Evalue <= 0.00001);", pdbId);
            DataTable domainDefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            Dictionary<string, Dictionary<string, AtomInfo[]>> asuHash = buRetriever.GetAsymUnit(pdbId);
            Dictionary<string, AtomInfo[]> chainAtomsHash = asuHash["0"];
            int domainInterfaceId = 1;
            string asymChain = "";
            int entityId = -1;
            Dictionary<string, List<string>> pfamRelChainHash = new Dictionary<string,List<string>> ();
            List<string> chainSymOpList = new List<string> (chainAtomsHash.Keys);
            chainSymOpList.Sort();
            foreach (string chainSymOpString in chainSymOpList)
            {
                asymChain = GetAsymChain(chainSymOpString);
                entityId = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain);
                DataRow[] domainDefRows = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId));
                if (domainDefRows.Length < 2)
                {
                    continue;
                }
                AtomInfo[] chainAtoms = (AtomInfo[])chainAtomsHash[chainSymOpString];

                Dictionary<long, AtomInfo[]> chainDomainAtomsHash = GetDomainAtoms(domainDefRows, chainAtoms);

                // the interacted domain pairs intra-chain
                string[] interactedDomainPairs = GetInteractedDomainPairs(chainDomainAtomsHash);

                // insert domain-domain interfaces into table
                string[] pfamRelations = InsertDataIntoTable(pdbId, asymChain, ref domainInterfaceId, 
                    interactedDomainPairs, domainDefTable, dataWriter);
                foreach (string pfamRelation in pfamRelations)
                {
                    if (pfamRelChainHash.ContainsKey(pfamRelation))
                    {
                        pfamRelChainHash[pfamRelation].Add(asymChain);
                    }
                    else
                    {
                        List<string> chainList = new List<string> ();
                        chainList.Add(asymChain);
                        pfamRelChainHash.Add(pfamRelation, chainList);
                    }
                }
            }
            dataWriter.Flush();
            return pfamRelChainHash;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="pdbId"></param>
       /// <param name="asymChain"></param>
       /// <param name="domainInterfaceId"></param>
       /// <param name="interactedDomainPairs"></param>
       /// <param name="domainDefTable"></param>
       /// <param name="dataWriter"></param>
        private string[] InsertDataIntoTable(string pdbId, string asymChain, ref int domainInterfaceId,
            string[] interactedDomainPairs, DataTable domainDefTable, StreamWriter dataWriter)
        {
            string familyCode1 = "";
            string familyCode2 = "";
            long domainId1 = 0;
            long domainId2 = 0;
            string dataLine = "";
            List<string> pfamRelList = new List<string> ();
            string pfamRelation = "";
            foreach (string interactedDomainPair in interactedDomainPairs)
            {
                string[] domainIds = interactedDomainPair.Split('_');
                domainId1 = Convert.ToInt64 (domainIds[0]);
                domainId2 = Convert.ToInt64 (domainIds[1]);
                familyCode1 = GetFamilyCode(domainId1, domainDefTable);
                familyCode2 = GetFamilyCode(domainId2, domainDefTable);
                dataLine = pdbId + "\t" + asymChain + "\t" + domainInterfaceId.ToString() + "\t";
                if (string.Compare(familyCode1, familyCode2) > 0)
                {
                    pfamRelation = familyCode2 + "_" + familyCode1;
                    dataLine += (familyCode2 + "\t" + familyCode1 + "\t" +
                                domainIds[1] + "\t" + domainIds[0]);
                }
                else
                {
                    pfamRelation = familyCode1 + "_" + familyCode2;
                    dataLine += (familyCode1 + "\t" + familyCode2 + "\t" +
                                domainIds[0] + "\t" + domainIds[1]);
                }
                dataWriter.WriteLine(dataLine);
                domainInterfaceId++;
                if (!pfamRelList.Contains(pfamRelation))
                {
                    pfamRelList.Add(pfamRelation);
                }
            }
            return pfamRelList.ToArray ();
        }
        #endregion
    }
}
