using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using CrystalInterfaceLib.Contacts;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;

namespace BuCompLib.BuInterfaces
{
    public class BuDomainInterfaces
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private AsuInfoFinder asuInfoFinder = new AsuInfoFinder();
        #endregion

        #region find the domain-domain interactions from BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryBuInterfacesHash"></param>
        public void GetEntryBuDomainInterfaces(string pdbId, Dictionary<string, InterfaceChains[]> entryBuInterfacesHash)
        {
         //   string queryString = string.Format("Select PdbID, DomainID, Pfam_Acc, Pfam_ID, AsymChain, SeqStartPos, SeqEndPos " +
           //    " From PfamPdb Where PdbID = '{0}';", pdbId);
            string queryString = string.Format("Select PdbID, DomainID, Pfam_Acc, Pfam_ID, EntityID, SeqStart, SeqEnd " +
                " From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainDefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            foreach (string buId in entryBuInterfacesHash.Keys)
            {
                GetEntryBuDomainInterfaces(pdbId, buId, entryBuInterfacesHash[buId], domainDefTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buInterfaces"></param>
        public void GetEntryBuDomainInterfaces(string pdbId, string buId, InterfaceChains[] buInterfaces)
        {
            string queryString = string.Format("Select PdbID, DomainID, Pfam_Acc, Pfam_ID, EntityID, SeqStart, SeqEnd " + 
                " From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainDefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            GetEntryBuDomainInterfaces(pdbId, buId, buInterfaces, domainDefTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="buInterfaces"></param>
        /// <param name="domainDefTable"></param>
        public void GetEntryBuDomainInterfaces(string pdbId, string buId, InterfaceChains[] buInterfaces, DataTable domainDefTable)
        {
            string asymChain1 = "";
            string asymChain2 = "";
            int entityId1 = -1;
            int entityId2 = -1;
            long[] domainIds1 = null;
            long[] domainIds2 = null;
            int maxSeqId1 = -1;
            int maxSeqId2 = -1;
            int minSeqId1 = -1;
            int minSeqId2 = -1;
            string family1 = "";
            string family2 = "";
            int relSeqId = -1;
            char isReversed;
            int domainInterfaceId = 1;
            foreach (InterfaceChains buInterface in buInterfaces)
            {
                GetMaxMinContactSeqIDs(buInterface, ref maxSeqId1, ref minSeqId1, ref maxSeqId2, ref minSeqId2);

                asymChain1 = GetAsymChain(buInterface.firstSymOpString);
                entityId1 = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain1);
                asymChain2 = GetAsymChain(buInterface.secondSymOpString);
                if (asymChain1 == asymChain2)
                {
                    DataRow[] domainDefRows = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId1));
                    domainIds2 = domainIds1 = GetDomainsInChain(domainDefRows);
                    entityId2 = entityId1;
                }
                else
                {
                    entityId2 = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain2);

                    DataRow[] entityDomainDefRows1 = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId1));
                    DataRow[] entityDomainDefRows2 = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId2));
                    domainIds1 = GetDomainsInChain(entityDomainDefRows1);
                    domainIds2 = GetDomainsInChain(entityDomainDefRows2);
                }

                foreach (long domainId1 in domainIds1)
                {
                    DataRow[] domainDefRows1 = domainDefTable.Select(string.Format("EntityID = '{0}' AND DomainID = '{1}'", entityId1, domainId1), "SeqStart ASC");
                    family1 = domainDefRows1[0]["Pfam_Acc"].ToString().TrimEnd();
                    Range[] domainRanges1 = GetDomainRanges(domainDefRows1);
                    if (domainRanges1[0].startPos > maxSeqId1 || domainRanges1[domainRanges1.Length - 1].endPos < minSeqId1)
                    {
                        continue;
                    }
                    foreach (long domainId2 in domainIds2)
                    {
                        DataRow[] domainDefRows2 = domainDefTable.Select(string.Format("EntityID = '{0}' AND DomainID = '{1}'", entityId2, domainId2), "SeqStart ASC");
                        Range[] domainRanges2 = GetDomainRanges(domainDefRows2);
                        family2 = domainDefRows2[0]["Pfam_Acc"].ToString().TrimEnd();
                        if (domainRanges2[0].startPos > maxSeqId2 || domainRanges2[domainRanges2.Length - 1].endPos < minSeqId2)
                        {
                            continue;
                        }

                        if (AreDomainsInteracted(buInterface, domainRanges1, domainRanges2))
                        {
                            relSeqId = GetRelSeqId(family1, family2, out isReversed);
                            DataRow dataRow = BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces].NewRow();
                            dataRow["PdbID"] = pdbId;
                            dataRow["BuID"] = buId;
                            dataRow["InterfaceID"] = buInterface.interfaceId;
                            dataRow["RelSeqID"] = relSeqId;
                            dataRow["DomainInterfaceID"] = domainInterfaceId;
                            domainInterfaceId++;
                            dataRow["DomainID1"] = domainId1;
                            dataRow["DomainID2"] = domainId2;
                            dataRow["IsReversed"] = isReversed;
                            BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces].Rows.Add(dataRow);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <returns></returns>
        private long[] GetDomainsInChain(DataRow[] domainDefRows)
        {
            List<long> domainIdList = new List<long> ();
            long domainId = 0;
            foreach (DataRow domainDefRow in domainDefRows)
            {
                domainId = Convert.ToInt64(domainDefRow["DomainID"].ToString ());
                if (!domainIdList.Contains(domainId))
                {
                    domainIdList.Add(domainId);
                }
            }
            return domainIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <returns></returns>
        private Range[] GetDomainRanges(DataRow[] domainDefRows)
        {
            Range[] domainRanges = new Range[domainDefRows.Length];
            int count = 0;
            foreach (DataRow domainRow in domainDefRows)
            {
                Range range = new Range();
                range.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString ());
                range.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString ());
                domainRanges[count] = range;
                count++;
            }
            return domainRanges;
        }

        #region for analysis of pfam relationship in the pdb biological units
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryBuInterfacesHash"></param>
        public Dictionary<string, List<string>> GetEntryBuDomainInterfaces(string pdbId, Dictionary<string, InterfaceChains[]> entryBuInterfacesHash, StreamWriter dataWriter)
        {
            //   string queryString = string.Format("Select PdbID, DomainID, Pfam_Acc, Pfam_ID, AsymChain, SeqStartPos, SeqEndPos " +
            //    " From PfamPdb Where PdbID = '{0}';", pdbId);
            string queryString = string.Format("Select PdbID, DomainID, Pfam_Acc, Pfam_ID, EntityID, SeqStart, SeqEnd " +
                " From PdbPfam Where PdbID = '{0}' AND (IsWeak = '0' OR IsUpdated = '1' OR Evalue <= 0.00001);", pdbId);
            DataTable domainDefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, List<string>> pfamRelBuHash = new Dictionary<string,List<string>> ();
            foreach (string buId in entryBuInterfacesHash.Keys)
            {
                string[] pfamRelations = GetEntryBuDomainInterfaces(pdbId, buId, entryBuInterfacesHash[buId], domainDefTable, dataWriter);
                foreach (string pfamRelation in pfamRelations)
                {
                    if (pfamRelBuHash.ContainsKey(pfamRelation))
                    {
                        pfamRelBuHash[pfamRelation].Add(buId);
                    }
                    else
                    {
                        List<string> buList = new List<string> ();
                        buList.Add(buId);
                        pfamRelBuHash.Add(pfamRelation, buList);
                    }
                }
            }
            return pfamRelBuHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="buInterfaces"></param>
        /// <param name="domainDefTable"></param>
        public string[] GetEntryBuDomainInterfaces(string pdbId, string buId, InterfaceChains[] buInterfaces, DataTable domainDefTable, StreamWriter dataWriter)
        {
            string asymChain1 = "";
            string asymChain2 = "";
            int entityId1 = -1;
            int entityId2 = -1;
            DataRow[] domainDefRows1 = null;
            DataRow[] domainDefRows2 = null;
            int domainStart1 = -1;
            int domainEnd1 = -1;
            int domainStart2 = -1;
            int domainEnd2 = -1;
            int maxSeqId1 = -1;
            int maxSeqId2 = -1;
            int minSeqId1 = -1;
            int minSeqId2 = -1;
            string family1 = "";
            string family2 = "";
            int domainInterfaceId = 1;
            string dataLine = "";
            List<string> pfamRelList = new List<string> ();
            string pfamRelation = "";
            foreach (InterfaceChains buInterface in buInterfaces)
            {
                GetMaxMinContactSeqIDs(buInterface, ref maxSeqId1, ref minSeqId1, ref maxSeqId2, ref minSeqId2);

                asymChain1 = GetAsymChain(buInterface.firstSymOpString);
                entityId1 = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain1);
                asymChain2 = GetAsymChain(buInterface.secondSymOpString);
                if (asymChain1 == asymChain2)
                {
                    domainDefRows1 = domainDefRows2 =
                        domainDefTable.Select(string.Format("EntityID = '{0}'", entityId1));
                }
                else
                {
                    entityId2 = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain2);

                    domainDefRows1 = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId1));
                    domainDefRows2 = domainDefTable.Select(string.Format("EntityID = '{0}'", entityId2));
                }
                foreach (DataRow domainRow1 in domainDefRows1)
                {
                    domainStart1 = Convert.ToInt32(domainRow1["SeqStart"].ToString());
                    domainEnd1 = Convert.ToInt32(domainRow1["SeqEnd"].ToString());
                    family1 = domainRow1["Pfam_ACC"].ToString().TrimEnd();

                    if (domainStart1 > maxSeqId1 || domainEnd1 < minSeqId1)
                    {
                        continue;
                    }
                    foreach (DataRow domainRow2 in domainDefRows2)
                    {
                        domainStart2 = Convert.ToInt32(domainRow2["SeqStart"].ToString());
                        domainEnd2 = Convert.ToInt32(domainRow2["SeqEnd"].ToString());
                        family2 = domainRow2["Pfam_ACC"].ToString().TrimEnd();
                        if (domainStart2 > maxSeqId2 || domainEnd2 < minSeqId2)
                        {
                            continue;
                        }
                        if (AreDomainsInteracted(buInterface, domainStart1, domainEnd1, domainStart2, domainEnd2))
                        {
                            dataLine = pdbId + "\t" + buId + "\t" +
                                buInterface.interfaceId.ToString() + "\t" + 
                                domainInterfaceId.ToString() + "\t";
                            if (string.Compare(family1, family2) > 0)
                            {
                                pfamRelation = family2 + "_" + family1;
                                dataLine += (buInterface.secondSymOpString + "\t" +
                                        buInterface.firstSymOpString + "\t" +
                                        family2 + "\t" + family1 + "\t" +
                                        domainRow2["DomainID"].ToString() + "\t" +
                                        domainRow1["DomainID"].ToString()) + "\t" +
                                        domainRow2["SeqStart"].ToString () + "-" + domainRow2["SeqEnd"].ToString () + "\t" +
                                        domainRow1["SeqStart"].ToString () + "\t" + domainRow1["SeqEnd"].ToString ();
                            }
                            else
                            {
                                pfamRelation = family1 + "_" + family2;
                                dataLine += (buInterface.firstSymOpString + "\t" +
                                        buInterface.secondSymOpString + "\t" +
                                        family1 + "\t" + family2 + "\t" +
                                        domainRow1["DomainID"].ToString() + "\t" +
                                        domainRow2["DomainID"].ToString()) + "\t" +
                                        domainRow1["SeqStart"].ToString () + "-" + domainRow1["SeqEnd"].ToString () + "\t" +
                                        domainRow2["SeqStart"].ToString () + "-" + domainRow2["SeqEnd"].ToString ();
                            }
                            domainInterfaceId++;
                            dataWriter.WriteLine(dataLine);
                            if (! pfamRelList.Contains(pfamRelation))
                            {
                                pfamRelList.Add(pfamRelation);
                            }
                        }
                    }
                }
            }
            dataWriter.Flush();
            string[] pfamRelations = new string[pfamRelList.Count];
            pfamRelList.CopyTo(pfamRelations);
            return pfamRelations;
        }
        #endregion
        #endregion

        #region check if domain-domain interacted
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterface"></param>
        /// <param name="domainRange1"></param>
        /// <param name="domainRange2"></param>
        /// <returns></returns>
        private bool AreDomainsInteracted(InterfaceChains buInterface, Range[] domainRange1, Range[] domainRange2)
        {
            int numOfResidueContacts = 0;
            int numOfAtomContacts = 0;
            int seqId1 = -1;
            int seqId2 = -1;
            bool hasEnoughResiduePairs = false;
            bool hasContacts = false;
            foreach (string seqString in buInterface.seqDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = ParseHelper.ConvertSeqToInt(seqIds[0]);
                seqId2 = ParseHelper.ConvertSeqToInt(seqIds[1]);
                if (IsSeqIdInDomainRange(seqId1, domainRange1) &&
                    IsSeqIdInDomainRange(seqId2, domainRange2))
                {
                    numOfResidueContacts++;
                    if (numOfResidueContacts >= AppSettings.parameters.contactParams.numOfResidueContacts)
                    {
                        hasEnoughResiduePairs = true;
                        break;
                    }
                }
            }
            foreach (string seqString in buInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = ParseHelper.ConvertSeqToInt(seqIds[0]);
                seqId2 = ParseHelper.ConvertSeqToInt(seqIds[1]);
                if (IsSeqIdInDomainRange (seqId1, domainRange1) &&
                   IsSeqIdInDomainRange (seqId2, domainRange2))
                {
                    numOfAtomContacts++;
                    hasContacts = true;
                    //      break;
                }
            }
            //    if (hasContacts && hasEnoughResiduePairs)
            if ((hasContacts && hasEnoughResiduePairs) ||
                numOfAtomContacts >= AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private bool IsSeqIdInDomainRange(int seqId, Range[] domainRanges)
        {
            foreach (Range domainRange in domainRanges)
            {
                if (seqId <= domainRange.endPos && seqId >= domainRange.startPos)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterface"></param>
        /// <param name="domainStart1"></param>
        /// <param name="domainEnd1"></param>
        /// <param name="domainStart2"></param>
        /// <param name="domainEnd2"></param>
        /// <returns></returns>
        private bool AreDomainsInteracted(InterfaceChains buInterface, int domainStart1, int domainEnd1,
            int domainStart2, int domainEnd2)
        {
            int numOfResidueContacts = 0;
            int numOfAtomContacts = 0;
            int seqId1 = -1;
            int seqId2 = -1;
            bool hasEnoughResiduePairs = false;
            bool hasContacts = false;
            foreach (string seqString in buInterface.seqDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = ParseHelper.ConvertSeqToInt (seqIds[0]);
                seqId2 = ParseHelper.ConvertSeqToInt (seqIds[1]);
                if (seqId1 >= domainStart1 && seqId1 <= domainEnd1 &&
                    seqId2 >= domainStart2 && seqId2 <= domainEnd2)
                {
                    numOfResidueContacts++;
                    if (numOfResidueContacts >= AppSettings.parameters.contactParams.numOfResidueContacts)
                    {
                        hasEnoughResiduePairs = true;
                        break;
                    }
                }
            }
            foreach (string seqString in buInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = ParseHelper.ConvertSeqToInt (seqIds[0]);
                seqId2 = ParseHelper.ConvertSeqToInt (seqIds[1]);
                if (seqId1 >= domainStart1 && seqId1 <= domainEnd1 &&
                   seqId2 >= domainStart2 && seqId2 <= domainEnd2)
                {
                    numOfAtomContacts++;
                    hasContacts = true;
              //      break;
                }
            }
        //    if (hasContacts && hasEnoughResiduePairs)
            if ((hasContacts && hasEnoughResiduePairs) || 
                numOfAtomContacts >= AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterface"></param>
        /// <param name="maxSeq1"></param>
        /// <param name="minSeq1"></param>
        /// <param name="maxSeq2"></param>
        /// <param name="minSeq2"></param>
        private void GetMaxMinContactSeqIDs(InterfaceChains buInterface, ref int maxSeq1, ref int minSeq1, 
            ref int maxSeq2, ref int minSeq2)
        {
            maxSeq1 = -1;
            minSeq1 = 999999;
            maxSeq2 = -1;
            minSeq2 = 999999;
            foreach (string seqString in buInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                int seqId1 = ParseHelper.ConvertSeqToInt(seqIds[0]);
                if (maxSeq1 < seqId1)
                {
                    maxSeq1 = seqId1;
                }
                if (minSeq1 > seqId1)
                {
                    minSeq1 = seqId1;
                }
                int seqId2 = ParseHelper.ConvertSeqToInt(seqIds[1]);
                if (maxSeq2 < seqId2)
                {
                    maxSeq2 = seqId2;
                }
                if (minSeq2 > seqId2)
                {
                    minSeq2 = seqId2;
                }
            }
        }

        #endregion

        #region info from db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpString"></param>
        /// <returns></returns>
        private string GetAsymChain(string chainSymOpString)
        {
            int chainIdx = chainSymOpString.IndexOf("_");
            if (chainIdx > 0)
            {
                return chainSymOpString.Substring(0, chainIdx);
            }
            else
            {
                return chainSymOpString;
            }
        }


        /// <summary>
        /// the domain relation group sequential number
        /// from database
        /// </summary>
        /// <param name="family1"></param>
        /// <param name="family2"></param>
        /// <param name="isReversed"></param>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        public int GetRelSeqId(string family1, string family2, out char isReversed)
        {
            int relSeqId = -1;
            isReversed = '0';
            if (string.Compare(family1, family2) > 0)
            {
                isReversed = '1';
            }
            else
            {
                isReversed = '0';
            }
            string queryString = string.Format("SELECT * FROM PfamRelations " +
                " WHERE FamilyCode1 = '{0}' AND FamilyCode2 = '{1}';",
                family1, family2);
            DataTable familyRelTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (familyRelTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT * FROM PfamRelations " +
                    " WHERE FamilyCode2 = '{0}' AND FamilyCode1 = '{1}';",
                    family1, family2);
                familyRelTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            }
            if (familyRelTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(familyRelTable.Rows[0]["RelSeqID"].ToString());
            }
            else
            {
                queryString = "SELECT Max(RelSeqID) AS MaxRelSeqID FROM PfamRelations;";
                familyRelTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
                if (familyRelTable.Rows[0]["MaxRelSeqID"].ToString() != "")
                {
                    relSeqId = Convert.ToInt32(familyRelTable.Rows[0]["MaxRelSeqID"].ToString()) + 1;
                }
                else
                {
                    relSeqId = 1;
                }
                if (isReversed == '0')
                {
                    queryString = string.Format("INSERT INTO PfamRelations (RelSeqID, FamilyCode1, FamilyCode2) VALUES ({0}, '{1}', '{2}');",
                        relSeqId, family1, family2);
                }
                else
                {
                    queryString = string.Format("INSERT INTO PfamRelations (RelSeqID, FamilyCode1, FamilyCode2) VALUES ({0}, '{1}', '{2}');",
                        relSeqId, family2, family1);
                }
                dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            }
            return relSeqId;
        }
        #endregion
    }
}
