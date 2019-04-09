using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class ClanDomainInterfaceComp : PfamDomainInterfaceComp
    {

        /// <summary>
        /// 
        /// </summary>
        public void CompareClanDomainInterfaces()
        {
            int minNumRepEntries = 0;
            int maxNumRepEntries = 100;
             if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_" + minNumRepEntries.ToString () + 
                    "less" + maxNumRepEntries.ToString () + ".txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog_" + minNumRepEntries.ToString() +
                    "less" + maxNumRepEntries.ToString() + ".txt");
            }

            int[] clanSeqIds = GetClanSeqIDs();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + ProtCidSettings.dataType + " domain interfaces in clans.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Comp";

            foreach (int clanSeqId in clanSeqIds)
            {
                CompareDomainInterfacesInClan(clanSeqId);
            }

            DomainInterfaceBuilder.nonAlignDomainsWriter.Close();
            nonAlignDomainWriter.Close();
            noSimDomainInterfacesWriter.Close();
            logWriter.Close();
            compResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        public void CompareDomainInterfacesInClan(int clanSeqId)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("ClanInterfaceCompResults" + clanSeqId.ToString() + ".txt");
                logWriter = new StreamWriter("ClanDomainInterfaceCompLog" + clanSeqId.ToString() + ".txt");

                resultNeedClosed = true;
            }

            int[] clanRelSeqIds = GetRelationSeqIdsInClan(clanSeqId);
            if (clanRelSeqIds.Length < 2)
            {
                return;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(clanSeqId.ToString ());

            Dictionary<int, string[]> relationRepEntryHash = GetRelationRepEntryHash(clanRelSeqIds);

            int numOfEntryPairs = GetClanEntryPairsCount(relationRepEntryHash);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = numOfEntryPairs;
            ProtCidSettings.progressInfo.totalStepNum = numOfEntryPairs;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(clanSeqId.ToString ());

            for (int i = 0; i < clanRelSeqIds.Length; i++)
            {
                string[] repEntries1 = (string[])relationRepEntryHash[clanRelSeqIds[i]];
                for (int j = i + 1; j < clanRelSeqIds.Length; j++)
                {
                    string[] repEntries2 = (string[])relationRepEntryHash[clanRelSeqIds[j]];
                    CompareDomainInterfaceInterRelations(clanSeqId, clanRelSeqIds[i], repEntries1,
                        clanRelSeqIds[j], repEntries2);
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
        /// <param name="relSeqId1"></param>
        /// <param name="repEntries1"></param>
        /// <param name="relSeqId2"></param>
        /// <param name="repEntries2"></param>
        private void CompareDomainInterfaceInterRelations(int clanSeqId, int relSeqId1, string[] repEntries1, 
            int relSeqId2, string[] repEntries2)
        {
            foreach (string repEntry1 in repEntries1)
            {
                DomainInterface[] domainInterfaces1 = GetEntryUniqueDomainInterfacesFromDb (relSeqId1, repEntry1);
                if (domainInterfaces1 == null)
                {
                    logWriter.WriteLine(relSeqId1.ToString () + " " + repEntry1 + " no domain interfaces.");
                    logWriter.Flush();
                    continue;
                }
                foreach (string repEntry2 in repEntries2)
                {
                    ProtCidSettings.progressInfo.currentFileName = repEntry1 + "_" + repEntry2;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfacesFromDb (relSeqId2, repEntry2);
                    if (domainInterfaces2 == null)
                    {
                        logWriter.WriteLine(relSeqId2.ToString() + " " + repEntry2 + " no domain interfaces.");
                        logWriter.Flush();
                        continue;
                    }
                    DomainInterfacePairInfo[] compInfos = 
                        CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);

                    AssignDomainInterfaceCompTable(clanSeqId, relSeqId1, repEntry1, relSeqId2, repEntry2,
                        compInfos);
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp]);
            WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp],
                compResultWriter);
            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetRelationRepEntryHash(int[] relSeqIds)
        {
            Dictionary<int, string[]> relationRepEntryHash = new Dictionary<int,string[]> ();
            foreach (int relSeqId in relSeqIds)
            {
                string[] repEntries = GetRelationRepEntries(relSeqId);
                relationRepEntryHash.Add(relSeqId, repEntries);
            }
            return relationRepEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <returns></returns>
        private int[] GetRelationSeqIdsInClan(int clanSeqId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation " + 
                "Where ClanSeqID = {0};", clanSeqId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetClanSeqIDs()
        {
            string queryString = "Select Distinct ClanSeqID From PfamDomainFamilyRelation Order By ClanSeqID;";
            DataTable clanSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] clanSeqIds = new int[clanSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow clanSeqIdRow in clanSeqIdTable.Rows)
            {
                clanSeqIds[count] = Convert.ToInt32(clanSeqIdRow["ClanSeqID"].ToString ());
                count++;
            }
            return clanSeqIds;
        }

        /// <summary>
        /// assign interface pair info into table
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="pairCompInfos"></param>
        private void AssignDomainInterfaceCompTable(int clanSeqId,  int relSeqId1, string pdbId1, 
            int relSeqId2, string pdbId2, DomainInterfacePairInfo[] pairCompInfos)
        {
            foreach (DomainInterfacePairInfo pairCompInfo in pairCompInfos)
            {
                if (pairCompInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow compRow =
                          DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].NewRow();
                    compRow["ClanSeqID"] = clanSeqId;
                    compRow["RelSeqID1"] = relSeqId1;
                    compRow["RelSeqID2"] = relSeqId2;
                    compRow["PdbID1"] = pdbId1;
                    compRow["PdbID2"] = pdbId2;
                    compRow["DomainInterfaceID1"] = pairCompInfo.interfaceInfo1.domainInterfaceId;
                    compRow["DomainInterfaceID2"] = pairCompInfo.interfaceInfo2.domainInterfaceId;
                    compRow["QScore"] = pairCompInfo.qScore;
                    compRow["Identity"] = pairCompInfo.identity;
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.ClanDomainInterfaceComp].Rows.Add(compRow);
                }
            }
        }

        /// <summary>
        /// how many entry pairs in the clan to be compared
        /// </summary>
        /// <param name="relationRepEntryHash"></param>
        /// <returns></returns>
        private int GetClanEntryPairsCount(Dictionary<int, string[]> relationRepEntryHash)
        {
            int numOfEntryPairs = 0;
            List<int> relSeqIdList = new List<int> (relationRepEntryHash.Keys);
            for (int i = 0; i < relSeqIdList.Count; i++)
            {
                string[] repEntries1 = relationRepEntryHash[relSeqIdList[i]];
                for (int j = i + 1; j < relSeqIdList.Count; j++)
                {
                    string[] repEntries2 = relationRepEntryHash[relSeqIdList[j]];
                    numOfEntryPairs += repEntries1.Length * repEntries2.Length;
                }
            }
            return numOfEntryPairs;
        }
    }
}
