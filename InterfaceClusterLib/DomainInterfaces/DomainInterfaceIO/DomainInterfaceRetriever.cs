using System;
using System.Data;
using System.Collections.Generic;
using System.IO;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using InterfaceClusterLib.InterfaceProcess;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Summary description for DomainInterfaceRetriever.
	/// </summary>
	public class DomainInterfaceRetriever : InterfaceRetriever
	{
		#region member variables
		private DbQuery dbQuery = new DbQuery ();
        private DomainAtomsReader domainReader = new DomainAtomsReader();
		#endregion

		public DomainInterfaceRetriever()
		{
        }

        #region domain interfaces from files
        /// <summary>
        /// directly read the domain interfaces from the files
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public DomainInterface[] GetDomainInterfacesFromFiles (string pdbId, int relSeqId, string type)
        {
            int[] domainInterfaceIds = GetEntryDomainInterfaceIds(pdbId, relSeqId, type);
            DomainInterface[] domainInterfaces = ReadDomainInterfacesFromFiles(pdbId, domainInterfaceIds);
            return domainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIds(string pdbId, int relSeqId, string type)
        {
            string queryString = string.Format("Select * From {0}DomainInterfaces " +
                " Where RelSeqID = {1} AND PdbID = '{2}';", ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> domainInterfaceIdList = new List<int> ();

            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }
            int[] domainInterfaceIds = new int[domainInterfaceIdList.Count];
            domainInterfaceIdList.CopyTo(domainInterfaceIds);
            return domainInterfaceIds;
        }
        #endregion

        #region inter-chain interfaces from chain interface files
        /// <summary>
        /// retrieve domain interfaces from crystal chain interfaces
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="familyCode"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public DomainInterface[] GetInterChainDomainInterfaces(string pdbId, int relSeqId, int[] domainInterfaceIds, string type)
        {
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();
            if (domainInterfaceIds.Length > 0)
            {
                string queryString = string.Format("Select * From {0}DomainInterfaces " +
                    " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID > 0;", ProtCidSettings.dataType, relSeqId, pdbId);
                DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                Dictionary<int, List<int>> domainChainInterfaceIdHash = GetDomainChainInterfaceHash(domainInterfaceTable);

                queryString = string.Format("Select * From {0}DomainFamilyRelation Where RelSeqId = {1};",
                    ProtCidSettings.dataType, relSeqId);
                DataTable domainSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                string pfamCode1 = domainSeqIdTable.Rows[0]["FamilyCode1"].ToString().Trim();
                string pfamCode2 = domainSeqIdTable.Rows[0]["FamilyCode2"].ToString().Trim();

                int[] chainInterfaceIds = GetInterChainInterfaceIDs(relSeqId, pdbId);

                InterfaceChains[] chainInterfaces = null;
                if (chainInterfaceIds.Length > 0)
                {
                    chainInterfaces = GetCrystInterfaces(pdbId, chainInterfaceIds, type);
                }

                DomainInterface domainInterface = null;
                foreach (int domainInterfaceId in domainChainInterfaceIdHash.Keys)
                {
                    if (Array.IndexOf(domainInterfaceIds, domainInterfaceId) > -1)
                    {
                        DataRow[] domainInterfaceRows = domainInterfaceTable.Select("DomainInterfaceID = " + domainInterfaceId);
                        List<int> chainInterfaceIdList = domainChainInterfaceIdHash[domainInterfaceId];
                        if (chainInterfaceIdList.Count == 1)  // inter-chain 
                        {
                            domainInterface = GetInterChainDomainInterface(domainInterfaceRows[0], pfamCode1, pfamCode2, chainInterfaces);
                            if (domainInterface != null && domainInterface.seqDistHash.Count > 0)
                            {
                                domainInterfaceList.Add(domainInterface);
                            }
                        }
                    }
                }
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public DomainInterface[] GetInterChainDomainInterfaces(string pdbId, int relSeqId, string type)
        {
            string queryString = string.Format("Select * From {0}DomainInterfaces " +
                " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID > 0;", ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select * From {0}DomainFamilyRelation Where RelSeqId = {1};",
                ProtCidSettings.dataType, relSeqId);
            DataTable domainSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string family1 = domainSeqIdTable.Rows[0]["FamilyCode1"].ToString().Trim();
            string family2 = domainSeqIdTable.Rows[0]["FamilyCode2"].ToString().Trim();

            int[] chainInterfaceIds = GetInterChainInterfaceIDs(relSeqId, pdbId);

            InterfaceChains[] chainInterfaces = GetCrystInterfaces(pdbId, chainInterfaceIds, type);

            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();

            Range[] domainRanges1 = null;
            Range[] domainRanges2 = null;
            int chainInterfaceId = -1;
            int domainInterfaceId = -1;
            string interfacePfam1 = "";
            string interfacePfam2 = "";
            bool isReversedChains = false;
            foreach (DataRow domainRow in domainInterfaceTable.Rows)
            {
                isReversedChains = false;
                DomainInterface domainInterface = new DomainInterface();
                domainInterfaceId = Convert.ToInt32(domainRow["DomainInterfaceID"].ToString());
                domainRanges1 = GetDomainRange(pdbId, Convert.ToInt64(domainRow["DomainID1"].ToString()), out interfacePfam1);

                domainRanges2 = GetDomainRange(pdbId, Convert.ToInt64(domainRow["DomainID2"].ToString()), out interfacePfam2);
                chainInterfaceId = Convert.ToInt32(domainRow["InterfaceID"].ToString());

                InterfaceChains chainInterface = GetChainInterface(chainInterfaces, chainInterfaceId);
                if (chainInterface == null)
                {
                    continue;
                }
                // domain interface chains are in the different order of the corresponding chain interface chains
                if (domainRow["IsReversed"].ToString() == "1")
                {
                    isReversedChains = true;
                    domainInterface = GetDomainInterface(chainInterface, domainRanges2, domainRanges1, isReversedChains);
                }
                else
                {
                    domainInterface = GetDomainInterface(chainInterface, domainRanges1, domainRanges2);
                }
                
                domainInterface.domainInterfaceId = domainInterfaceId;
                domainInterface.pdbId = pdbId;
                domainInterface.interfaceId = chainInterfaceId;
                domainInterface.domainId1 = Convert.ToInt64(domainRow["DomainID1"].ToString());
                domainInterface.domainId2 = Convert.ToInt64(domainRow["DomainID2"].ToString());
                domainInterface.familyCode1 = interfacePfam1;
                domainInterface.familyCode2 = interfacePfam2;

                if (family2 != family1 && interfacePfam1 == family2 && interfacePfam2 == family1)
                {
                    domainInterface.Reverse();
                }
                domainInterfaceList.Add(domainInterface);
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRow"></param>
        /// <param name="pfamCode1"></param>
        /// <param name="pfamCode2"></param>
        /// <param name="chainInterfaces"></param>
        /// <returns></returns>
        private DomainInterface GetInterChainDomainInterface(DataRow domainInterfaceRow, string pfamCode1, string pfamCode2, InterfaceChains[] chainInterfaces)
        {
            string pdbId = domainInterfaceRow["PdbID"].ToString();
            DomainInterface domainInterface = new DomainInterface();
            int domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
            string interfacePfam1 = "";
            string interfacePfam2 = "";
            Range[] domainRanges1 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString()), out interfacePfam1);

            Range[] domainRanges2 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString()), out interfacePfam2);
            int chainInterfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
            if (chainInterfaceId > 0)  // inter-chain domain interface
            {
                InterfaceChains chainInterface = GetChainInterface(chainInterfaces, chainInterfaceId);
                if (chainInterface != null)
                {
                    domainInterface = GetDomainInterface(chainInterface, domainRanges1, domainRanges2);
                }
            }
            if (domainInterface != null)
            {
          /*      if (domainInterfaceRow["IsReversed"].ToString() == "1")
                {
                    domainInterface.familyCode1 = pfamCode2;
                    domainInterface.familyCode2 = pfamCode1;
                }
                else
                {
                    domainInterface.familyCode1 = pfamCode1;
                    domainInterface.familyCode2 = pfamCode2;
                }*/
                domainInterface.domainInterfaceId = domainInterfaceId;
                domainInterface.pdbId = pdbId;
                domainInterface.interfaceId = chainInterfaceId;
                domainInterface.domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                domainInterface.domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                domainInterface.familyCode1 = interfacePfam1;
                domainInterface.familyCode2 = interfacePfam2;

                // if the domain interface chain order is different from the pfam ids defined for the relation
                // then reverse the interface chains 
                if (interfacePfam1 == pfamCode2 && interfacePfam2 == pfamCode1)
                {
                    domainInterface.Reverse();
                }
            }
            return domainInterface;
        }

        /// <summary>
        /// domain interaction of the interface
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRange1"></param>
        /// <param name="domainRange2"></param>
        /// <returns></returns>
        private DomainInterface GetDomainInterface(InterfaceChains chainInterface, string domainRangeString1, string domainRangeString2)
        {
            DomainInterface domainInterface = new DomainInterface(chainInterface);
            if (domainRangeString1 != "-" && domainRangeString2 != "-")
            {
                Range[] domainRanges1 = GetDomainRanges(domainRangeString1);
                domainInterface.chain1 = GetDomainAtoms(domainInterface.chain1, domainRanges1);

                Range[] domainRanges2 = GetDomainRanges(domainRangeString2);
                domainInterface.chain2 = GetDomainAtoms(domainInterface.chain2, domainRanges2);

                domainInterface.seqDistHash =
                    GetResDistHash(domainInterface.seqDistHash, domainRanges1, domainRanges2);
            }
            return domainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<int, List<int>> GetDomainChainInterfaceHash(DataTable domainInterfaceTable)
        {
            Dictionary<int, List<int>> domainChainInterfaceIdHash = new Dictionary<int,List<int>> ();
            int domainInterfaceId = 0;
            int chainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                chainInterfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                if (domainChainInterfaceIdHash.ContainsKey(domainInterfaceId))
                {
                    domainChainInterfaceIdHash[domainInterfaceId].Add(chainInterfaceId);
                }
                else
                {
                    List<int> chainInterfaceIdList = new List<int> ();
                    chainInterfaceIdList.Add(chainInterfaceId);
                    domainChainInterfaceIdHash.Add(domainInterfaceId, chainInterfaceIdList);
                }
            }
            return domainChainInterfaceIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetInterChainInterfaceIDs(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select Distinct InterfaceID From {0}DomainInterfaces" +
                " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID <> 0;",
                ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable interChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] interfaceIds = new int[interChainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceIdRow in interChainInterfaceTable.Rows)
            {
                interfaceIds[count] = Convert.ToInt32(interfaceIdRow["InterfaceID"].ToString());
                count++;
            }
            return interfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetInterChainInterfaceIDs(int relSeqId, string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format("Select Distinct InterfaceID From {0}DomainInterfaces" +
                " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID <> 0 AND DomainInterfaceID IN ({3});",
                ProtCidSettings.dataType, relSeqId, pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable interChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] interfaceIds = new int[interChainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceIdRow in interChainInterfaceTable.Rows)
            {
                interfaceIds[count] = Convert.ToInt32(interfaceIdRow["InterfaceID"].ToString());
                count++;
            }
            return interfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaces"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        public InterfaceChains GetChainInterface(InterfaceChains[] chainInterfaces, int interfaceId)
        {
            foreach (InterfaceChains chainInterface in chainInterfaces)
            {
                if (chainInterface.interfaceId == interfaceId)
                {
                    return chainInterface;
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainStart1"></param>
        /// <param name="domainEnd1"></param>
        /// <param name="domainStart2"></param>
        /// <param name="domainEnd2"></param>
        /// <returns></returns>
        public DomainInterface GetInterChainDomainInterface(InterfaceChains chainInterface,
            int domainStart1, int domainEnd1, int domainStart2, int domainEnd2)
        {
            Range[] domainRanges1 = FormatDomainRange(domainStart1, domainEnd1);
            Range[] domainRanges2 = FormatDomainRange(domainStart2, domainEnd2);
            return GetDomainInterface(chainInterface, domainRanges1, domainRanges2);
        }

        /// <summary>
        /// domain interaction of the interface
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRange1"></param>
        /// <param name="domainRange2"></param>
        /// <returns></returns>
        public DomainInterface GetDomainInterface(InterfaceChains chainInterface, Range[] domainRanges1, Range[] domainRanges2)
        {
            DomainInterface domainInterface = new DomainInterface(chainInterface);

            domainInterface.chain1 = GetDomainAtoms(chainInterface.chain1, domainRanges1);
            domainInterface.chain2 = GetDomainAtoms(chainInterface.chain2, domainRanges2);
            domainInterface.seqDistHash =
                GetResDistHash(chainInterface.seqDistHash, domainRanges1, domainRanges2);

            return domainInterface;
        }
        /// <summary>
        /// domain interaction of the interface
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRange1"></param>
        /// <param name="domainRange2"></param>
        /// <returns></returns>
        public DomainInterface GetDomainInterface(InterfaceChains chainInterface, Range[] domainRanges1, Range[] domainRanges2, bool isReversedChains)
        {
            DomainInterface domainInterface = new DomainInterface(chainInterface);
            if (isReversedChains)
            {
                domainInterface.chain2 = GetDomainAtoms(chainInterface.chain1, domainRanges1);
                domainInterface.chain1 = GetDomainAtoms(chainInterface.chain2, domainRanges2);
                domainInterface.ResetSeqResidueHash();
            }
            else
            {
                domainInterface.chain1 = GetDomainAtoms(chainInterface.chain1, domainRanges1);
                domainInterface.chain2 = GetDomainAtoms(chainInterface.chain2, domainRanges2);
                domainInterface.seqDistHash =
                    GetResDistHash(chainInterface.seqDistHash, domainRanges1, domainRanges2);
            }

            return domainInterface;
        }
        #endregion

        #region intra-chain domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="intraChainDomainInterfaceIds"></param>
        /// <returns></returns>
        public DomainInterface[] GetIntraChainDomainInterfaces(string pdbId, int[] intraChainDomainInterfaceIds)
        {
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();
            if (intraChainDomainInterfaceIds.Length > 0)
            {
                string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID = 0;", pdbId);
                DataTable entryIntraChainDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

                queryString = string.Format("Select DomainID, SeqStart, SeqEnd From PdbPfam Where PdbID = '{0}';", pdbId);
                DataTable pdbpfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);

                string[] asymChains = GetAsymChainsForIntraDomainInterfaces(entryIntraChainDomainInterfaceTable, intraChainDomainInterfaceIds);
                Dictionary<string, ChainAtoms> chainAtomsHash = domainReader.ReadAtomsOfChains(pdbId, asymChains);

                foreach (int domainInterfaceId in intraChainDomainInterfaceIds)
                {
                    DomainInterface domainInterface = ReadIntraDomainInterfaceFromChain(domainInterfaceId, entryIntraChainDomainInterfaceTable, pdbpfamTable, chainAtomsHash);
                    domainInterfaceList.Add(domainInterface);
                }
            }
            DomainInterface[] domainInterfaces = new DomainInterface[domainInterfaceList.Count];
            domainInterfaceList.CopyTo(domainInterfaces);
            return domainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="pdbpfamTable"></param>
        /// <param name="chainAtomsHash"></param>
        /// <returns></returns>
        private DomainInterface ReadIntraDomainInterfaceFromChain(int domainInterfaceId, DataTable domainInterfaceTable, 
            DataTable pdbpfamTable, Dictionary<string, ChainAtoms> chainAtomsHash)
        {
            DomainInterface domainInterface = new DomainInterface();
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                long domainId1 = Convert.ToInt64(domainInterfaceRows[0]["DomainID1"].ToString());
                long domainId2 = Convert.ToInt64(domainInterfaceRows[0]["DomainID2"].ToString());
                string asymChain = domainInterfaceRows[0]["AsymChain1"].ToString().TrimEnd();
                if (chainAtomsHash.ContainsKey(asymChain))
                {
                    ChainAtoms chain = chainAtomsHash[asymChain];

                    Range[] domainRanges1 = GetDomainRange(pdbpfamTable, domainId1);
                    Range[] domainRanges2 = GetDomainRange(pdbpfamTable, domainId2);

                    domainInterface.domainInterfaceId = domainInterfaceId;
                    domainInterface.firstSymOpString = asymChain + "_1_555";
                    domainInterface.secondSymOpString = asymChain + "_1_555";
                    domainInterface.domainId1 = domainId1;
                    domainInterface.domainId2 = domainId2;
                    domainInterface.chain1 = GetDomainAtoms(chain.CartnAtoms, domainRanges1);
                    domainInterface.chain2 = GetDomainAtoms(chain.CartnAtoms, domainRanges2);

                    domainInterface.GetInterfaceResidueDist();
                }
            }
            return domainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbpfamTable"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private Range[] GetDomainRange(DataTable pdbpfamTable, long domainId)
        {
            DataRow[] domainRows = pdbpfamTable.Select(string.Format("DomainID = '{0}'", domainId));
            List<Range> rangeList = new List<Range> ();
            foreach (DataRow domainRow in domainRows)
            {
                Range range = new Range();
                range.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                range.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                rangeList.Add(range);
            }
            return rangeList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomainInterfaceTable"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private string[] GetAsymChainsForIntraDomainInterfaces(DataTable entryDomainInterfaceTable, int[] domainInterfaceIds)
        {
            List<string> asymChainList = new List<string> ();
            string asymChain = "";
            foreach (int intraChainDomainInterfaceId in domainInterfaceIds)
            {
                DataRow[] domainInterfaceRows = entryDomainInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", intraChainDomainInterfaceId));
                if (domainInterfaceRows.Length > 0)
                {
                    asymChain = domainInterfaceRows[0]["AsymChain1"].ToString().TrimEnd();
                    if (!asymChainList.Contains(asymChain))
                    {
                        asymChainList.Add(asymChain);
                    }
                }
            }
            return asymChainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetIntraChainDomainInterfaceIDs(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID From {0}DomainInterfaces" +
                " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID = 0;",
                ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable intraChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] domainInterfaceIds = new int[intraChainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceIdRow in intraChainInterfaceTable.Rows)
            {
                domainInterfaceIds[count] = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString());
                count++;
            }
            return domainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetIntraChainDomainInterfaceIDs(int relSeqId, string pdbId, int[] domainInterfaceIds,
            out string[] asymChains)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID, AsymChain1 From {0}DomainInterfaces" +
                " Where RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID = 0 AND DomainInterfaceID IN ({3});",
                ProtCidSettings.dataType, relSeqId, pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable intraChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] intraDomainInterfaceIds = new int[intraChainInterfaceTable.Rows.Count];
            int count = 0;
            List<string> asymChainList = new List<string> ();
            string asymChain = "";
            foreach (DataRow domainInterfaceIdRow in intraChainInterfaceTable.Rows)
            {
                intraDomainInterfaceIds[count] = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString());
                count++;
                asymChain = domainInterfaceIdRow["AsymChain1"].ToString().TrimEnd();
                if (!asymChainList.Contains(asymChain))
                {
                    asymChainList.Add(asymChain);
                }
            }
            asymChains = new string[asymChainList.Count];
            asymChainList.CopyTo(asymChains);
            return intraDomainInterfaceIds;
        }
        #endregion

        #region read domain interfaces from files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DomainInterface[] ReadDomainInterfacesFromFiles(string pdbId)
        {
            string searchMode = pdbId + "*.cryst.gz";
            string filePath = ProtCidSettings.dirSettings.interfaceFilePath + "\\" + ProtCidSettings.dataType + "domain";
            DomainInterface[] domainInterfaces = GetDomainInterfacesFromFiles(filePath, pdbId, "cryst");
            return domainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        public DomainInterface[] ReadDomainInterfacesFromFiles(string pdbId, int[] domainInterfaceIds)
        {
            string searchMode = pdbId + "*.cryst.gz";
            string filePath = ProtCidSettings.dirSettings.interfaceFilePath + "\\" + ProtCidSettings.dataType + "domain";
            DomainInterface[] domainInterfaces = GetDomainInterfacesFromFiles(filePath, pdbId, domainInterfaceIds, "cryst");
            return domainInterfaces;
        }
        #endregion

        #region domain Info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainStart"></param>
        /// <param name="domainEnd"></param>
        /// <returns></returns>
        private Range[] FormatDomainRange(int domainStart, int domainEnd)
        {
            Range[] domainRanges = new Range[1];
            domainRanges[0] = new Range();
            domainRanges[0].startPos = domainStart;
            domainRanges[0].endPos = domainEnd;
            return domainRanges;
        }
        /// <summary>
        /// format the range string into domain ranges
        /// </summary>
        /// <param name="domainRangeString"></param>
        /// <returns></returns>
        private Range[] GetDomainRanges(string domainRangeString)
        {
            string[] rangeStrings = domainRangeString.Split(';');
            Range[] ranges = new Range[rangeStrings.Length];
            for (int i = 0; i < rangeStrings.Length; i++)
            {
                string[] rangePosFields = rangeStrings[i].Split('-');
                ranges[i].startPos = Convert.ToInt32(rangePosFields[0]);
                ranges[i].endPos = Convert.ToInt32(rangePosFields[1]);
            }
            return ranges;
        }

        /// <summary>
        /// get the domain ranges from the database
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private Range[] GetDomainRange(string pdbId, Int64 domainId, out string pfamId)
        {
            List<Range> domainRangeList = new List<Range> ();
            DataTable domainTable = null;
            string queryString = "";
            if (ProtCidSettings.dataType == "pfam")
            {
                queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}' AND DomainID = {1};",
                    pdbId, domainId);
            }
            else if (ProtCidSettings.dataType == "scop")
            {
                queryString = string.Format("Select * From ScopDomainPos Where SunID = {0};", domainId);
            }
            domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            pfamId = "";
            if (domainTable.Rows.Count > 0)
            {
                pfamId = domainTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            foreach (DataRow domainRow in domainTable.Rows)
            {
                Range domainRange = new Range();
                domainRange.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRange.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                domainRangeList.Add(domainRange);
            }
            Range[] domainRanges = new Range[domainRangeList.Count];
            domainRangeList.CopyTo(domainRanges);
            return domainRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private Dictionary<int, Range[]> GetMultiChainDomainRange(string pdbId, Int64 domainId)
        {
            Dictionary<int, List<Range>> domainRangeListHash = new Dictionary<int, List<Range>>();
            DataTable domainTable = null;
            string queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);         
            domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            int entityId = 0;
            foreach (DataRow domainRow in domainTable.Rows)
            {
                entityId = Convert.ToInt32(domainRow["EntityID"].ToString());
                Range domainRange = new Range();
                domainRange.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRange.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                if (domainRangeListHash.ContainsKey(entityId))
                {
                    domainRangeListHash[entityId].Add(domainRange);
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(domainRange);
                    domainRangeListHash.Add(entityId, rangeList);
                }
            }
            Dictionary<int, Range[]> domainRangeHash = new Dictionary<int, Range[]>();
            foreach (int keyEntityId in domainRangeListHash.Keys)
            {                
                domainRangeHash.Add (keyEntityId, domainRangeListHash[keyEntityId].ToArray ());
            }
            return domainRangeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceRows"></param>
        /// <returns></returns>
        private Dictionary<string, Range[]>[] GetMultiChainDomainRange(DataRow[] domainInterfaceRows)
        {
            string pdbId = domainInterfaceRows[0]["PdbID"].ToString();
            Dictionary<int, Range[]>[] domainRangeHashes = new Dictionary<int, Range[]>[2];
            long domainId1 = Convert.ToInt64(domainInterfaceRows[0]["DomainID1"].ToString());
            string asymChain1 = "";
            List<string> asymChainList1 = new List<string> ();
            long domainId2 = Convert.ToInt64(domainInterfaceRows[0]["DomainID2"].ToString());
            string asymChain2 = "";
            List<string> asymChainList2 = new List<string> ();
            foreach (DataRow domainInterfaceRow in domainInterfaceRows)
            {
                asymChain1 = domainInterfaceRow["AsymChain1"].ToString().TrimEnd();
                if (!asymChainList1.Contains(asymChain1))
                {
                    asymChainList1.Add(asymChain1);
                }
                asymChain2 = domainInterfaceRow["AsymChain2"].ToString().TrimEnd();
                if (!asymChainList2.Contains(asymChain2))
                {
                    asymChainList2.Add(asymChain2);
                }
            }
            string[] asymChains1 = new string[asymChainList1.Count];
            asymChainList1.CopyTo(asymChains1);
            string[] asymChains2 = new string[asymChainList2.Count];
            asymChainList2.CopyTo(asymChains2);
            Dictionary<string, Range[]> domainChainRangeHash1 = GetMultiChainDomainRange(pdbId, domainId1, asymChains1);
            Dictionary<string, Range[]> domainChainRangeHash2 = GetMultiChainDomainRange(pdbId, domainId2, asymChains2);

            Dictionary<string, Range[]>[] domainInterfaceRangeHashes = new Dictionary<string, Range[]>[2];
            domainInterfaceRangeHashes[0] = domainChainRangeHash1;
            domainInterfaceRangeHashes[1] = domainChainRangeHash2;
            return domainInterfaceRangeHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        private Dictionary<string, Range[]> GetMultiChainDomainRange(string pdbId, long domainId, string[] asymChains)
        {
            string queryString = string.Format("Select PdbPfamChain.*, SeqStart, SeqEnd " +
                " From PdbPfam, PdbPfamChain Where PdbPfamChain.PdbID = '{0}' AND PdbPfamChain.DomainID = {1} AND " +
                " PdbPfamChain.AsymChain In ({2}) AND PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamChain.EntityID;",
                pdbId, domainId, ParseHelper.FormatSqlListString(asymChains));
            DataTable domainChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, List<Range>> chainDomainRangeListHash = new Dictionary<string,List<Range>> ();
            string asymChain = "";
            foreach (DataRow domainRangeRow in domainChainTable.Rows)
            {
                asymChain = domainRangeRow["AsymChain"].ToString().TrimEnd();
                Range range = new Range();
                range.startPos = Convert.ToInt32(domainRangeRow["SeqStart"].ToString());
                range.endPos = Convert.ToInt32(domainRangeRow["SeqEnd"].ToString());
                if (chainDomainRangeListHash.ContainsKey(asymChain))
                {
                    chainDomainRangeListHash[asymChain].Add(range);
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(range);
                    chainDomainRangeListHash.Add(asymChain, rangeList);
                }
            }
            Dictionary<string, Range[]> chainDomainRangeHash = new Dictionary<string, Range[]>();
            foreach (string keyAsymChain in chainDomainRangeListHash.Keys)
            {
                chainDomainRangeHash.Add (keyAsymChain, chainDomainRangeListHash[keyAsymChain].ToArray ());
            }
            return chainDomainRangeHash;
        }

        /// <summary>
        /// the domain in the chain
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="startPos"></param>
        /// <param name="endPos"></param>
        /// <returns></returns>
        private AtomInfo[] GetDomainAtoms(AtomInfo[] chain, int startPos, int endPos)
        {
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chain)
            {
                seqId = ParseHelper.ConvertSeqToInt(atom.seqId);
                if (seqId <= endPos && seqId >= startPos)
                {
                    atomList.Add(atom);
                }
            }
            AtomInfo[] domain = new AtomInfo[atomList.Count];
            atomList.CopyTo(domain);
            return domain;
        }

        /// <summary>
        /// the domain in the chain
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="startPos"></param>
        /// <param name="endPos"></param>
        /// <returns></returns>
        private AtomInfo[] GetDomainAtoms(AtomInfo[] chain, Range[] ranges)
        {
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chain)
            {
                seqId = ParseHelper.ConvertSeqToInt(atom.seqId);
                foreach (Range range in ranges)
                {
                    if (seqId <= range.endPos && seqId >= range.startPos)
                    {
                        atomList.Add(atom);
                        break;
                    }
                }
            }
            AtomInfo[] domain = new AtomInfo[atomList.Count];
            atomList.CopyTo(domain);
            return domain;
        }

        /// <summary>
        /// Hashtable for residue-residue distances
        /// </summary>
        /// <param name="residueDistHash"></param>
        /// <param name="startPos1"></param>
        /// <param name="endPos1"></param>
        /// <param name="startPos2"></param>
        /// <param name="endPos2"></param>
        /// <returns></returns>
        private Dictionary<string, double> GetResDistHash(Dictionary<string, double> residueDistHash,  int startPos1, int endPos1, int startPos2, int endPos2)
        {
            Dictionary<string, double> domainResDistHash = new Dictionary<string, double>();
            foreach (string seqString in residueDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                if (Convert.ToInt32(seqIds[0]) <= endPos1 && Convert.ToInt32(seqIds[0]) >= startPos1 &&
                    Convert.ToInt32(seqIds[1]) <= endPos2 && Convert.ToInt32(seqIds[1]) >= startPos2)
                {
                    domainResDistHash.Add(seqString, residueDistHash[seqString]);
                }
            }
            return domainResDistHash;
        }

        /// <summary>
        /// Hashtable for residue-residue distances
        /// </summary>
        /// <param name="residueDistHash"></param>
        /// <param name="startPos1"></param>
        /// <param name="endPos1"></param>
        /// <param name="startPos2"></param>
        /// <param name="endPos2"></param>
        /// <returns></returns>
        private Dictionary<string, double> GetResDistHash(Dictionary<string, double> residueDistHash, Range[] domainRanges, int chainNum)
        {
            Dictionary<string, double> domainResDistHash = new Dictionary<string, double>();
            int seqNum = 0;
            foreach (string seqString in residueDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                if (chainNum == 1)
                {
                    seqNum = Convert.ToInt32(seqIds[0]);
                }
                else if (chainNum == 2)
                {
                    seqNum = Convert.ToInt32(seqIds[1]);
                }
                foreach (Range range in domainRanges)
                {
                    if (seqNum <= range.endPos && seqNum >= range.startPos)
                    {
                        domainResDistHash.Add(seqString, residueDistHash[seqString]);
                    }
                }
            }
            return domainResDistHash;
        }

        /// <summary>
        /// residue-residue contacts for the domain interface
        /// </summary>
        /// <param name="residueDistHash"></param>
        /// <param name="domainRanges1"></param>
        /// <param name="domainRanges2"></param>
        /// <returns></returns>
        private Dictionary<string, double> GetResDistHash(Dictionary<string, double> residueDistHash, Range[] domainRanges1, Range[] domainRanges2)
        {
            Dictionary<string, double> domainResDistHash = new Dictionary<string, double>();
            int seqNum1 = 0;
            int seqNum2 = 0;
            bool inRange1 = false;
            bool inRange2 = false;
            foreach (string seqString in residueDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqNum1 = Convert.ToInt32(seqIds[0]);
                seqNum2 = Convert.ToInt32(seqIds[1]);
                inRange1 = false;
                inRange2 = false;
                foreach (Range range1 in domainRanges1)
                {
                    if (seqNum1 <= range1.endPos && seqNum1 >= range1.startPos)
                    {
                        inRange1 = true;
                        break;
                    }
                }
                foreach (Range range2 in domainRanges2)
                {
                    if (seqNum2 <= range2.endPos && seqNum2 >= range2.startPos)
                    {
                        inRange2 = true;
                        break;
                    }
                }
                // this residue-residue contact located in the domain interface
                if (inRange1 && inRange2)
                {
                    domainResDistHash.Add(seqString, residueDistHash[seqString]);
                }
            }
            return domainResDistHash;
        }
        #endregion
    }
}
