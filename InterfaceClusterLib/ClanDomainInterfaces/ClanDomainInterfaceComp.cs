using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using InterfaceClusterLib.DomainInterfaces;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.ClanDomainInterfaces
{
    public class ClanDomainInterfaceComp : PfamDomainInterfaceComp
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superFamilyId"></param>
        public void CompareDomainInterfacesInSuperFamily(int clanSeqId)
        {

        }

        #region compare domain interfaces within a clan
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <param name="relSeqId1"></param>
        /// <param name="relSeqId2"></param>
        public void CompareRepDomainInterfaces2PfamRelations(int clanSeqId, int relSeqId1, int relSeqId2)
        {
            bool deleteOld = false;
            Dictionary<string, string> pfamClanHash = GetPfamClanHash(relSeqId1, relSeqId2);
            CompareRepDomainInterfaces2PfamRelations(clanSeqId, relSeqId1, relSeqId2, pfamClanHash, deleteOld);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <param name="relSeqId1"></param>
        /// <param name="relSeqId2"></param>
        /// <param name="userPfamClanHash"></param>
        public void CompareRepDomainInterfaces2PfamRelations(int clanSeqId, int relSeqId1, int relSeqId2, Dictionary<string, string> pfamClanHash, bool deletedOld)
        {
            domainAlignType = "struct";  // only have structure alignments
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + clanSeqId.ToString() + "_" + domainAlignType + ".txt", true);
                logWriter = new StreamWriter("DomainInterfaceCompLog" + clanSeqId.ToString() + ".txt", true);

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string[] repEntries1 = GetRelationRepEntries(relSeqId1);
            string[] repEntries2 = GetRelationRepEntries(relSeqId2);
            //   string[] repEntries1 = {"1a06"};
            //   string[] repEntries2 = {"4e7k", "4e7l"};

            Dictionary<string, string[]> compEntryHash = GetEntriesToBeCalculated(clanSeqId, repEntries1, repEntries2, deletedOld);

            ProtCidSettings.progressInfo.totalOperationNum = repEntries1.Length * repEntries2.Length;
            ProtCidSettings.progressInfo.totalStepNum = repEntries1.Length * repEntries2.Length;

            DomainInterfacePairInfo[] pairCompInfos = null;

            foreach (string repEntry1 in compEntryHash.Keys)
            {
                DomainInterface[] domainInterfaces1 =
                            GetEntryUniqueDomainInterfaces(relSeqId1, repEntry1);
                if (domainInterfaces1 == null)
                {
                    noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + repEntry1);
                    continue;
                }
                // change the family codes to clan codes, to retrieve the alignments
                ChangeDomainInterfaceFamilyCodes(domainInterfaces1, pfamClanHash);

                string[] compEntries = (string[]) compEntryHash[repEntry1];

                foreach (string repEntry2 in compEntries)
                {
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;
                    ProtCidSettings.progressInfo.currentFileName = repEntry1 + "_" + repEntry2;
                    pairCompInfos = null;

                    try
                    {
                        DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId2, repEntry2);
                        if (domainInterfaces2 == null)
                        {
                            noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + repEntry2);
                            continue;
                        }
                        ChangeDomainInterfaceFamilyCodes(domainInterfaces2, pfamClanHash);
                        try
                        {
                            pairCompInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                        }
                        catch (Exception ex)
                        {
                            logWriter.WriteLine("Compare Inter-chain domain interfaces for " + repEntry1
                                + " and " + repEntry2 + " errors: " + ex.Message);
                            logWriter.Flush();
                        }
                        if (pairCompInfos == null || pairCompInfos.Length == 0)
                        {
                            noSimDomainInterfacesWriter.WriteLine(repEntry1 + "_" + repEntry2);
                            continue;
                        }
                        try
                        {
                            AssignDomainInterfaceCompTable(clanSeqId, repEntry1, repEntry2, relSeqId1, relSeqId2, pairCompInfos);
                        }
                        catch (Exception ex)
                        {
                            logWriter.WriteLine("Assign comparison of " + repEntry1 + "_" + repEntry2 + " to data table errors:  "
                                         + ex.Message);
                            logWriter.Flush();
                        }
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp]);
                        WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp], compResultWriter);
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].Clear();
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(repEntry1 + "_" + repEntry2 + " error: " + ex.Message);

                        logWriter.WriteLine("Compare " + repEntry1 + "_" + repEntry2 + " domain interfaces error:  "
                                         + ex.Message);
                        logWriter.Flush();
                    }
                }
            }
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                logWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repEntries1"></param>
        /// <param name="repEntries2"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetEntriesToBeCalculated(int clanSeqId, string[] repEntries1, string[] repEntries2, bool deletedOld)
        {
            Dictionary<string, string[]> compEntryHash = new Dictionary<string,string[]> ();
            List<string> compEntryList = new List<string> ();
            foreach (string repEntry1 in repEntries1)
            {
                compEntryList.Clear();   
                foreach (string repEntry2 in repEntries2)
                {
                    if (ExistClanDomainInterfaceCompInDb(clanSeqId, repEntry1, repEntry2) && !deletedOld)
                    {
                        continue;
                    }
                    compEntryList.Add(repEntry2);
                }
                if (compEntryList.Count > 0)
                {
                    string[] compEntries = new string[compEntryList.Count];
                    compEntryList.CopyTo(compEntries);
                    compEntryHash.Add(repEntry1, compEntries);
                }
            }
            return compEntryHash;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="relSeqId"></param>
       /// <param name="pdbId1"></param>
       /// <param name="pdbId2"></param>
       /// <returns></returns>
        private bool ExistClanDomainInterfaceCompInDb(int clanSeqId, string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From {0} Where (ClanSeqID = {1} AND " +
                " ((PdbID1 = '{2}' AND PdbID2 = '{3}')) OR (PdbID1 = '{3}' AND PdbID2 = '{2}'));",
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].TableName, clanSeqId, pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// change the family code in the domain interface to clan code
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="pfamClanIdHash"></param>
        private void ChangeDomainInterfaceFamilyCodes(DomainInterface[] domainInterfaces, Dictionary<string, string> pfamClanIdHash)
        {
            string clanId = "";
            for (int i = 0; i < domainInterfaces.Length; i ++ )
            {
                if (pfamClanIdHash.ContainsKey(domainInterfaces[i].familyCode1))
                {
                    clanId = (string)pfamClanIdHash[domainInterfaces[i].familyCode1];
                    domainInterfaces[i].familyCode1 = clanId;
                }
                if (pfamClanIdHash.ContainsKey(domainInterfaces[i].familyCode2))
                {
                    clanId = (string)pfamClanIdHash[domainInterfaces[i].familyCode2];
                    domainInterfaces[i].familyCode2 = clanId;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId1"></param>
        /// <param name="relSeqId2"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamClanHash(int relSeqId1, int relSeqId2)
        {
            string[] pfamIds1 = GetRelationPfams(relSeqId1);
            string[] pfamIds2 = GetRelationPfams(relSeqId2);
            List<string> pfamIdList = new List<string> (pfamIds1);
            foreach (string pfamId in pfamIds2)
            {
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
            }
            Dictionary<string, string> pfamClanIdHash = new Dictionary<string, string>();
            string clanId = "";
            foreach (string pfamId in pfamIdList)
            {
                clanId = GetPfamClanId(pfamId);
                if (clanId == "")
                {
                    clanId = pfamId;
                }
                pfamClanIdHash.Add(pfamId, clanId);
            }
            return pfamClanIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationPfams(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> pfamCodeList = new List<string> ();
            string pfamId = "";
            foreach (DataRow pfamCodeRow in pfamCodeTable.Rows)
            {
                pfamId = pfamCodeRow["FamilyCode1"].ToString().TrimEnd();
                if (!pfamCodeList.Contains(pfamId))
                {
                    pfamCodeList.Add(pfamId);
                }
                pfamId = pfamCodeRow["FamilyCode2"].ToString().TrimEnd();
                if (!pfamCodeList.Contains(pfamId))
                {
                    pfamCodeList.Add(pfamId);
                }
            }
            return pfamCodeList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamClanId(string pfamId)
        {
            string queryString = string.Format("Select Clan_ID From PfamClanFamily, PfamHmm, PfamClans " + 
                " Where PfamHmm.Pfam_ID = '{0}' AND PfamClanFamily.Pfam_Acc = PfamHmm.Pfam_Acc " + 
                " AND PfamClans.Clan_Acc = PfamclanFamily.Clan_Acc;", pfamId);
            DataTable clanIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string clanId = "";
            if (clanIdTable.Rows.Count > 0)
            {
                clanId = clanIdTable.Rows[0]["Clan_ID"].ToString().TrimEnd();
            }
            return clanId;
        }
        #endregion

        #region insert data into table
        /// <summary>
        /// assign interface pair info into table
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="pairCompInfos"></param>
        private void AssignDomainInterfaceCompTable(int clanSeqId, string pdbId1, string pdbId2, int relSeqId1, int relSeqId2, DomainInterfacePairInfo[] pairCompInfos)
        {
            foreach (DomainInterfacePairInfo pairCompInfo in pairCompInfos)
            {
                if (pairCompInfo != null &&
                   pairCompInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow compRow =
                          DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].NewRow();
                    compRow["ClanSeqID"] = clanSeqId;
                    compRow["RelSeqID1"] = relSeqId1;
                    compRow["PdbID1"] = pdbId1;
                    compRow["PdbID2"] = pdbId2;
                    compRow["RelSeqID2"] = relSeqId2;
                    compRow["DomainInterfaceID1"] = pairCompInfo.interfaceInfo1.domainInterfaceId;
                    compRow["DomainInterfaceID2"] = pairCompInfo.interfaceInfo2.domainInterfaceId;
                    compRow["QScore"] = pairCompInfo.qScore;
                    compRow["Identity"] = pairCompInfo.identity;
                    if (pairCompInfo.isInterface2Reversed)
                    {
                        compRow["IsReversed"] = 1;
                    }
                    else
                    {
                        compRow["IsReversed"] = 0;
                    }
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].Rows.Add(compRow);
                }
            }
        }
        #endregion


        #region for user-defined groups
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRelationHash">key: user group seq id, 
        /// values: user-defined pfam relations, which is the RelSeqID in the db
        /// * user can provide PFAM relations, 
        /// but have to find the RelSeqIDs of the relations for input</param>
        /// <param name="pfamClanHash">Key: PfamId, Value: Clan_ID (can be user-defined)</param>
        public void CompareRepDomainInterfaces2PfamRelations(Dictionary<int, int[]> groupRelationHash, Dictionary<string, string> pfamClanHash)
        {
            bool deletedOld = false;
            domainAlignType = "struct";  // only have structure alignments
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_user.txt", true);
                logWriter = new StreamWriter("DomainInterfaceCompLog_user.txt", true);

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate Q scores between PFAM relations for user-defined groups");

            foreach (int groupSeqId in groupRelationHash.Keys)
            {
                int[] groupRelSeqIds = groupRelationHash[groupSeqId];
                for (int i = 0; i < groupRelSeqIds.Length; i++)
                {
                    for (int j = i + 1; j < groupRelSeqIds.Length; j++)
                    {
                        CompareRepDomainInterfaces2PfamRelations(groupSeqId, groupRelSeqIds[i], groupRelSeqIds[j], pfamClanHash, deletedOld);
                    }
                }
            }
            
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                logWriter.Close();
            }
        }
        #endregion
        // end of function
    }
}
