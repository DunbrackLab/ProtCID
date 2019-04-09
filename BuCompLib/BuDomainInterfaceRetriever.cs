using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using AuxFuncLib;
using CrystalInterfaceLib.Crystal;
using ProtCidSettingsLib;

namespace BuCompLib
{
    public class BuDomainInterfaceRetriever
    {
        public struct DomainInterfaceComponent
        {
            public int chainInterfaceId;
            public string chainSymOpString1;
            public string chainSymOpString2;
            public long domainId1;
            public long domainId2;
            public Range[] domainRange1;
            public Range[] domainRange2;
            public string familyCode1;
            public string familyCode2;
        }

        #region member variables
        private BiolUnitRetriever buRetriever = new BiolUnitRetriever();
        private DbQuery dbQuery = new DbQuery();
        #endregion

        /// <summary>
        /// domain-domain interactions in every BU with the input relation
        /// </summary>
        /// <param name="relSeqId">family-family relation ID</param>
        /// <param name="pdbId"></param>
        /// <returns>every bu and its domain-domain interfaces</returns>
        public Dictionary<string, DomainInterface[]> RetrieveDomainInterfaces(int relSeqId, string pdbId, string buType)
        {
            Dictionary<string, DomainInterface[]> buDomainInterfacesHash = new Dictionary<string, DomainInterface[]> ();

            Dictionary<string, Dictionary<int, DomainInterfaceComponent>> buDomainInterfaceInfoHash = GetDomainInterfaceContents(relSeqId, pdbId, buType);
            string asuBuId = "0";

            if (buDomainInterfaceInfoHash != null && buDomainInterfaceInfoHash.Count > 0)
            {
                List<string> buIdList = new List<string> (buDomainInterfaceInfoHash.Keys);
                string[] relBuIds = new string[buIdList.Count];
                buIdList.CopyTo(relBuIds);

                Dictionary<string, Dictionary<string, AtomInfo[]>> biolUnitsHash = buRetriever.GetEntryBiolUnits(pdbId, relBuIds);
                if (buDomainInterfaceInfoHash.ContainsKey(asuBuId)) // intra-chain domain interfaces
                {
                    Dictionary<string, Dictionary<string, AtomInfo[]>> asuHash = buRetriever.GetAsymUnit(pdbId);
                    if (biolUnitsHash.ContainsKey(asuBuId))
                    {
                        biolUnitsHash[asuBuId] = asuHash[asuBuId];
                    }
                    else
                    {
                        biolUnitsHash.Add(asuBuId, asuHash[asuBuId]);
                    }
                }

                foreach (string buId in buDomainInterfaceInfoHash.Keys)
                {
                    DomainInterface[] buDomainInterfaces = GetDomainInterfaces(buDomainInterfaceInfoHash[buId], biolUnitsHash[buId]);
                    buDomainInterfacesHash.Add(buId, buDomainInterfaces);
                }
            }
            return buDomainInterfacesHash;
        }

        /// <summary>
        /// domain-domain interactions in every BU with the input relation
        /// </summary>
        /// <param name="relSeqId">family-family relation ID</param>
        /// <param name="pdbId"></param>
        /// <returns>every bu and its domain-domain interfaces</returns>
        public Dictionary<string, DomainInterface[]> RetrieveDomainInterfaces(int relSeqId, string pdbId, string[] buIDs)
        {
            Dictionary<string, DomainInterface[]> buDomainInterfacesHash = new Dictionary<string, DomainInterface[]>();

            Dictionary<string, Dictionary<int, DomainInterfaceComponent>> buDomainInterfaceInfoHash = GetDomainInterfaceContents(relSeqId, pdbId, buIDs);
            if (buDomainInterfaceInfoHash.Count > 0)
            {
                List<string> buIdList = new List<string> (buDomainInterfaceInfoHash.Keys);
                string[] relBuIds = new string[buIdList.Count];
                buIdList.CopyTo(relBuIds);

                Dictionary<string, Dictionary<string, AtomInfo[]>> biolUnitsHash = buRetriever.GetEntryBiolUnits(pdbId, relBuIds);

                foreach (string buId in buDomainInterfaceInfoHash.Keys)
                {
                    Dictionary<int, DomainInterfaceComponent> domainInterfacesHash = buDomainInterfaceInfoHash[buId];
                    Dictionary<string, AtomInfo[]> biolUnitHash = biolUnitsHash[buId];
                    DomainInterface[] buDomainInterfaces = GetDomainInterfaces(domainInterfacesHash, biolUnitHash);
                    buDomainInterfacesHash.Add(buId, buDomainInterfaces);
                }
            }
            return buDomainInterfacesHash;
        }

        #region domain interfaces 
        /// <summary>
        /// domain-domain interactions with the relation in the BU
        /// </summary>
        /// <param name="domainInterfacesHash">domain interfaces for the BU</param>
        /// <param name="biolUnitHash">the chains and atoms for the BU</param>
        /// <returns>the domain-domain interfaces in the BU</returns>
        private DomainInterface[] GetDomainInterfaces(Dictionary<int, DomainInterfaceComponent> domainInterfacesHash, Dictionary<string, AtomInfo[]> biolUnitHash)
        {
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();

            foreach (int domainInterfaceId in domainInterfacesHash.Keys)
            {
                DomainInterfaceComponent component = (DomainInterfaceComponent)domainInterfacesHash[domainInterfaceId];
                AtomInfo[] domainAtoms1 = null;
                AtomInfo[] domainAtoms2 = null;
                foreach (string chainSymOpString in biolUnitHash.Keys)
                {
                    if (component.chainSymOpString1 == chainSymOpString)
                    {
                        domainAtoms1 = GetDomainAtoms((AtomInfo[])biolUnitHash[chainSymOpString],
                            component.domainRange1);
                    }
                    if (component.chainSymOpString2 == chainSymOpString)
                    {
                        domainAtoms2 = GetDomainAtoms((AtomInfo[])biolUnitHash[chainSymOpString],
                           component.domainRange2);
                    }
                }
                DomainInterface domainInterface = new DomainInterface();
                domainInterface.domainInterfaceId = domainInterfaceId;
                domainInterface.interfaceId = component.chainInterfaceId;
                domainInterface.chain1 = domainAtoms1;
                domainInterface.chain2 = domainAtoms2;
                domainInterface.firstSymOpString = component.chainSymOpString1;
                domainInterface.secondSymOpString = component.chainSymOpString2;
                domainInterface.domainId1 = component.domainId1;
                domainInterface.domainId2 = component.domainId2;
                domainInterface.familyCode1 = component.familyCode1;
                domainInterface.familyCode2 = component.familyCode2;
                domainInterface.GetInterfaceResidueDist();
                domainInterfaceList.Add(domainInterface);
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// the atoms of the domain
        /// </summary>
        /// <param name="chainAtoms">atomic coordinate for the chain</param>
        /// <param name="domainRange">domain range</param>
        /// <returns>all atomic coordinates for the domain</returns>
        private AtomInfo[] GetDomainAtoms(AtomInfo[] chainAtoms, Range[] domainRange)
        {
            List<AtomInfo> domainAtomList = new List<AtomInfo> ();
            int atomSeqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                atomSeqId = ParseHelper.ConvertSeqToInt(atom.seqId);
                if (IsSeqIdInRange (atomSeqId, domainRange))
                {
                    domainAtomList.Add(atom);
                }
            }
            return domainAtomList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private bool IsSeqIdInRange(int seqId, Range[] ranges)
        {
            foreach (Range range in ranges)
            {
                if (seqId <= range.endPos && seqId >= range.startPos)
                {
                    return true;
                }
            }
            return true;
        }
        #endregion

        #region the contents for domain interfaces
        /// <summary>
        /// inter-chain and intra-chain for the entry
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, DomainInterfaceComponent>> GetDomainInterfaceContents(int relSeqId, string pdbId, string buType)
        {
            string tableName = "";
            string[] difBUs = null;
            if (buType == "asu")
            {
                tableName = buType + "PfamDomainInterfaces";
                difBUs = new string[1];
                difBUs[0] = "1";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
                difBUs = GetEntryDifBUs(pdbId, buType);
            }
            Dictionary<string, Dictionary<int, DomainInterfaceComponent>> buDomainInterfaceHash = GetDomainInterfaceContents(relSeqId, pdbId, difBUs);

            // add intra-chain domain interfaces if exist
            Dictionary<int, DomainInterfaceComponent> intraChainDomainInterfaceHash = GetIntraChainDomainInterfaceContents(relSeqId, pdbId);
            if (intraChainDomainInterfaceHash.Count > 0)
            {
                buDomainInterfaceHash.Add("0", intraChainDomainInterfaceHash);
            }
            return buDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, DomainInterfaceComponent>> GetDomainInterfaceContents(int relSeqId, string pdbId, string[] buIDs)
        {
            if (buIDs.Length == 0)
            {
                return null;
            }
            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }

            // inter-chain domain interfaces
            string queryString = string.Format("Select * From {0} " +
                " Where RelSeqId = {1} AND PdbID = '{2}' AND BuID IN ({3});", 
                tableName, relSeqId, pdbId, ParseHelper.FormatSqlListString (buIDs));
            DataTable domainInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            Dictionary<string, Dictionary<int, DomainInterfaceComponent>> buDomainInterfaceHash = GetDomainInterfaceContents(relSeqId, pdbId, domainInterfaceTable);

            if (buIDs.Contains("0"))  // if need intra-chain
            {
                // add intra-chain domain interfaces if exist
                Dictionary<int, DomainInterfaceComponent> intraChainDomainInterfaceHash = GetIntraChainDomainInterfaceContents(relSeqId, pdbId);
                if (intraChainDomainInterfaceHash.Count > 0)
                {
                    buDomainInterfaceHash.Add("0", intraChainDomainInterfaceHash);
                }
            }
            
            return buDomainInterfaceHash;
        }

      
        /// <summary>
        /// the intra-chain domain interfaces from chains of the ASU
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, DomainInterfaceComponent> GetIntraChainDomainInterfaceContents(int relSeqId, string pdbId)
        {
     //       int asuRelSeqId = ConvertRelSeqID(relSeqId, BuCompBuilder.BuType, "asu");
            string queryString = string.Format("Select * From AsuIntraDomainInterfaces " + 
                " Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            DataTable intraChainDomainInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            Dictionary<int, DomainInterfaceComponent> domainInterfaceHash = new Dictionary<int,DomainInterfaceComponent> ();

            if (intraChainDomainInterfaceTable.Rows.Count > 0)
            {
                string[] familyCodes = GetRelationFamilyCodes(relSeqId);
                int domainInterfaceId = -1;
                int chainInterfaceId = 0;
                string asymChain = "";
                Dictionary<long, Range[]> domainRangeHash = new Dictionary<long,Range[]> ();
                string isReversed = "";

                foreach (DataRow domainInterfaceRow in intraChainDomainInterfaceTable.Rows)
                {
                    domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                    asymChain = domainInterfaceRow["AsymChain"].ToString().TrimEnd();

                    DomainInterfaceComponent domainInterfaceContent = new DomainInterfaceComponent();
                    domainInterfaceContent.chainSymOpString1 = asymChain + "_1_555";
                    domainInterfaceContent.chainSymOpString2 = asymChain + "_1_555";
                    Range[] domainRange1 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString()), ref domainRangeHash);
                    if (domainRange1 == null)
                    {
                        continue;
                    }
                    Range[] domainRange2 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString()), ref domainRangeHash);
                    if (domainRange2 == null)
                    {
                        continue;
                    }
                    domainInterfaceContent.domainRange1 = domainRange1;
                    domainInterfaceContent.domainRange2 = domainRange2;
                    domainInterfaceContent.chainInterfaceId = chainInterfaceId;
                    domainInterfaceContent.domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                    domainInterfaceContent.domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                    isReversed = domainInterfaceRow["IsReversed"].ToString();
                    if (isReversed == "1")
                    {
                        domainInterfaceContent.familyCode1 = familyCodes[1];
                        domainInterfaceContent.familyCode2 = familyCodes[0];
                    }
                    else
                    {
                        domainInterfaceContent.familyCode1 = familyCodes[0];
                        domainInterfaceContent.familyCode2 = familyCodes[1];
                    }
                    domainInterfaceHash.Add(domainInterfaceId, domainInterfaceContent);
                }
            }
            return domainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, DomainInterfaceComponent>> GetDomainInterfaceContents(int relSeqId, string pdbId, DataTable domainInterfaceTable)
        {
            string[] familyCodes = GetRelationFamilyCodes(relSeqId);

            Dictionary<string, Dictionary<int, DomainInterfaceComponent>> buDomainInterfaceHash = new Dictionary<string,Dictionary<int,DomainInterfaceComponent>> ();
            string buId = "";
            int domainInterfaceId = -1;
            int chainInterfaceId = -1;
            Dictionary<long, Range[]> domainRangeHash = new Dictionary<long,Range[]> ();
            string isReversed = "";
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                buId = domainInterfaceRow["BuID"].ToString().TrimEnd();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                chainInterfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                string[] chainSymOpStrings = GetChainInterfaceChainComponent(pdbId, buId, chainInterfaceId);
                if (chainSymOpStrings == null)
                {
                    continue;
                }
                DomainInterfaceComponent domainInterfaceContent = new DomainInterfaceComponent();
                domainInterfaceContent.chainSymOpString1 = chainSymOpStrings[0];
                domainInterfaceContent.chainSymOpString2 = chainSymOpStrings[1];
                Range[] domainRange1 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString()), ref domainRangeHash);
                if (domainRange1 == null)
                {
                    continue;
                }
                Range[] domainRange2 = GetDomainRange(pdbId, Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString()), ref domainRangeHash);
                if (domainRange2 == null)
                {
                    continue;
                }
                domainInterfaceContent.domainRange1 = domainRange1;
                domainInterfaceContent.domainRange2 = domainRange2;
                domainInterfaceContent.chainInterfaceId = chainInterfaceId;
                domainInterfaceContent.domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                domainInterfaceContent.domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                isReversed = domainInterfaceRow["IsReversed"].ToString();
                if (isReversed == "1")
                {
                    domainInterfaceContent.familyCode1 = familyCodes[1];
                    domainInterfaceContent.familyCode2 = familyCodes[0];
                }
                else
                {
                    domainInterfaceContent.familyCode1 = familyCodes[0];
                    domainInterfaceContent.familyCode2 = familyCodes[1];
                }

                if (buDomainInterfaceHash.ContainsKey(buId))
                {
                    buDomainInterfaceHash[buId].Add(domainInterfaceId, domainInterfaceContent);
                }
                else
                {
                    Dictionary<int, DomainInterfaceComponent> domainInterfaceHash = new Dictionary<int,DomainInterfaceComponent> ();
                    domainInterfaceHash.Add(domainInterfaceId, domainInterfaceContent);
                    buDomainInterfaceHash.Add(buId, domainInterfaceHash);
                }
            }
            return buDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string[] GetChainInterfaceChainComponent(string pdbId, string buId, int interfaceId)
        {
            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = "Asu" + "SameInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "BuSameInterfaces";
            }
            string queryString = string.Format("Select * From {0} " + 
                " Where PdbID = '{1}' AND BuID = '{2}' AND InterfaceID = {3};", 
                tableName, pdbId, buId, interfaceId);
            DataTable interfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (interfaceTable.Rows.Count > 0)
            {
                string[] chainSymOpStrings = new string[2];
                if (BuCompBuilder.BuType == "pqs")
                {
                    chainSymOpStrings[0] = interfaceTable.Rows[0]["Chain1"].ToString().TrimEnd();
                    chainSymOpStrings[1] = interfaceTable.Rows[0]["Chain2"].ToString().TrimEnd();
                }
                else
                {
                    chainSymOpStrings[0] = interfaceTable.Rows[0]["Chain1"].ToString().TrimEnd() + "_" +
                        interfaceTable.Rows[0]["SymmetryString1"].ToString().TrimEnd();
                    chainSymOpStrings[1] = interfaceTable.Rows[0]["Chain2"].ToString().TrimEnd() + "_" +
                        interfaceTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
                }
                return chainSymOpStrings;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private Range[] GetDomainRange(string pdbId, long domainId, ref Dictionary<long, Range[]> domainRangeHash)
        {
            if (domainRangeHash.ContainsKey(domainId))
            {
                return domainRangeHash[domainId];
            }
            else
            {
                string queryString = string.Format("Select * From PdbPfam " +
                    " Where PdbID = '{0}' AND DomainID = {1} Order By SeqStart;", pdbId, domainId);
                DataTable domainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

                Range[] domainRanges = new Range[domainTable.Rows.Count];
                int count = 0;
                foreach (DataRow domainRow in domainTable.Rows)
                {
                    Range domainRange = new Range();
                    domainRange.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                    domainRange.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    domainRanges[count] = domainRange;
                    count++;
                }
                domainRangeHash.Add(domainId, domainRanges);
                return domainRanges;
            }
        }
        #endregion

        #region dif BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public string[] GetEntryDifBUs(string pdbId, string buType)
        {
            string tableName = buType + "EntryBuComp";
           
            string[] interfaceBuIDs = GetEntryBUs(pdbId, buType);
            List<string> difBuList = new List<string> (interfaceBuIDs);

            if (interfaceBuIDs.Length > 0)
            {
                string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", tableName, pdbId);
                DataTable entryBuCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);



                foreach (DataRow buCompRow in entryBuCompTable.Rows)
                {
                    if (AreSameBUs(buCompRow))
                    {
                        difBuList.Remove(buCompRow["BuID2"].ToString());
                    }
                }
            }
            string[] difBUs = new string[difBuList.Count];
            difBuList.CopyTo(difBUs);
            return difBUs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public string[] GetEntryBUs(string pdbId, string buType)
        {
            string tableName =buType + "BuInterfaces";
            string queryString = string.Format("Select Distinct BuID From {0} Where PdbID = '{1}';", tableName, pdbId);
            DataTable buIdTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string[] buIDs = new string[buIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow buIdRow in buIdTable.Rows)
            {
                buIDs[count] = buIdRow["BuID"].ToString().TrimEnd();
                count++;
            }
            return buIDs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buCompRow"></param>
        /// <returns></returns>
        private bool AreSameBUs(DataRow buCompRow)
        {
            string sameBUs = buCompRow["SameBUs"].ToString();
            int numOfInterfaces1 = Convert.ToInt32(buCompRow["NumOfInterfaces1"].ToString());
            int numOfInterfaces2 = Convert.ToInt32(buCompRow["NumOfInterfaces2"].ToString());
            string entityFormat1 = buCompRow["EntityFormat1"].ToString().TrimEnd();
            string entityFormat2 = buCompRow["EntityFormat2"].ToString().TrimEnd();
            if (sameBUs == "1" &&
                (numOfInterfaces1 == numOfInterfaces2 || entityFormat1 == entityFormat2))
            {
                return true;
            }
            return false;
        }
        #endregion

        #region relation info
        private int GetRelationSeqIdFromFamilyCodes(string familyCode1, string familyCode2)
        {
            string queryString = string.Format("Select RelSeqID From PfamRelations " +
                " Where FamilyCode1 = '{0}' AND FamilyCode2 = '{1}';", familyCode1, familyCode2);
            DataTable relSeqIdTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (relSeqIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationFamilyCodes(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamRelations Where RelSeqID = {0};", relSeqId);
            DataTable familyCodesTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string[] familyCodes = new string[2];
            familyCodes[0] = familyCodesTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            familyCodes[1] = familyCodesTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return familyCodes;
        }
        #endregion
    }
}
