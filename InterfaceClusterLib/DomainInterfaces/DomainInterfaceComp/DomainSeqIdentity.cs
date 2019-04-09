using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using ProtCidSettingsLib;
using AuxFuncLib;
using DbLib;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainSeqIdentity
    {
        private DbUpdate protcidUpdate = null;
        public DomainSeqIdentity ()
        {
            DomainInterfaceBuilder.InitializeThread();
            protcidUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
        }

        /// <summary>
        /// 
        /// </summary>
        public void FillDomainInterfaceCompSeqIdentities ()
        {
            string queryString = "Select Distinct PdbID1, PdbID2 From PfamDomainInterfaceComp Where PdbId1 = '3j3q' and pdbid2 = '3j3y';";
     //       string queryString = "Select Distinct PdbID1, PdbID2 From PfamDomainInterfaceComp;";
        DataTable entryPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId1 = "";
            string pdbId2 = "";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = entryPairTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryPairTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Fill out  identities in PfamDomainInterfaceComp");
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToString());
            ProtCidSettings.logWriter.WriteLine("Fill out identities in PfamDomainInterfaceComp.");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("This is for entry pairs with number of interface comparison pairs > 2000");
            ProtCidSettings.logWriter.WriteLine("This is for entry pairs with number of interface comparison pairs > 2000");

          foreach (DataRow entryPairRow in entryPairTable.Rows)
            {                
                pdbId1 = entryPairRow["PdbID1"].ToString();
                pdbId2 = entryPairRow["PdbID2"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId1 + " " + pdbId2;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                
                try
                {
                    UpdateDomainInterfaceCompSeqIdentities(pdbId1, pdbId2);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId1 + " " + pdbId2 + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        #region add sequence identity of domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public void UpdateDomainInterfaceCompSeqIdentities(string pdbId1, string pdbId2)
        {
            DataTable domainIdentityTable = GetDomainSeqIdentityTable(pdbId1, pdbId2);
            DataTable domainInterfaceTable1 = GetEntryDomainInterfaceTable(pdbId1);
            DataTable domainInterfaceTable2 = GetEntryDomainInterfaceTable(pdbId2);

            DataTable interfaceCompTable = GetEntryDomainInterfaceCompTable(pdbId1, pdbId2);          

            double identity = 0;
            List<long> interfaceDomainIdList1 = new List<long>();
            List<long> interfaceDomainIdList2 = new List<long>();
            long domainId1 = 0;
            long domainId2 = 0;
            int domainInterfaceId1 = 0;
            int domainInterfaceId2 = 0;
            bool entryReversed = false;
            foreach (DataRow compRow in interfaceCompTable.Rows)
            {
                interfaceDomainIdList1.Clear();
                interfaceDomainIdList2.Clear();
                if (compRow["PdbID1"].ToString () == pdbId1)
                {
                    entryReversed = false;
                    domainInterfaceId1 = Convert.ToInt32(compRow["DomainInterfaceID1"].ToString());
                    domainInterfaceId2 = Convert.ToInt32(compRow["DomainInterfaceID2"].ToString());
                    DataRow[] domainInterfaceRows1 = domainInterfaceTable1.Select(string.Format("DomainInterfaceID = '{0}'", compRow["DomainInterfaceID1"].ToString()));
                    if (domainInterfaceRows1.Length > 0)
                    {
                        domainId1 = Convert.ToInt64(domainInterfaceRows1[0]["DomainID1"].ToString());
                        interfaceDomainIdList1.Add(domainId1);
                        domainId2 = Convert.ToInt64(domainInterfaceRows1[0]["DomainID2"].ToString());
                        if (domainId1 != domainId2)
                        {
                            interfaceDomainIdList1.Add(domainId2);
                        }
                    }
                    DataRow[] domainInterfaceRows2 = domainInterfaceTable2.Select(string.Format("DomainInterfaceID = '{0}'", compRow["DomainInterfaceID2"].ToString()));
                    if (domainInterfaceRows2.Length > 0)
                    {
                        domainId1 = Convert.ToInt64(domainInterfaceRows2[0]["DomainID1"].ToString());
                        interfaceDomainIdList2.Add(domainId1);
                        domainId2 = Convert.ToInt64(domainInterfaceRows2[0]["DomainID2"].ToString());
                        if (domainId1 != domainId2)
                        {
                            interfaceDomainIdList2.Add(domainId2);
                        }                        
                    }
                }
                else if (compRow["PdbID1"].ToString() == pdbId2)
                {
                    entryReversed = true;
                    domainInterfaceId1 = Convert.ToInt32(compRow["DomainInterfaceID2"].ToString());
                    domainInterfaceId2 = Convert.ToInt32(compRow["DomainInterfaceID1"].ToString());
                    DataRow[] domainInterfaceRows1 = domainInterfaceTable1.Select(string.Format("DomainInterfaceID = '{0}'", compRow["DomainInterfaceID2"].ToString()));
                    if (domainInterfaceRows1.Length > 0)
                    {
                        domainId1 = Convert.ToInt64(domainInterfaceRows1[0]["DomainID1"].ToString());
                        interfaceDomainIdList1.Add(domainId1);
                        domainId2 = Convert.ToInt64(domainInterfaceRows1[0]["DomainID2"].ToString());
                        if (domainId1 != domainId2)
                        {
                            interfaceDomainIdList1.Add(domainId2);
                        }
                    }
                    DataRow[] domainInterfaceRows2 = domainInterfaceTable2.Select(string.Format("DomainInterfaceID = '{0}'", compRow["DomainInterfaceID1"].ToString()));
                    if (domainInterfaceRows2.Length > 0)
                    {
                        domainId1 = Convert.ToInt64(domainInterfaceRows2[0]["DomainID1"].ToString());
                        interfaceDomainIdList2.Add(domainId1);
                        domainId2 = Convert.ToInt64(domainInterfaceRows2[0]["DomainID2"].ToString());
                        if (domainId1 != domainId2)
                        {
                            interfaceDomainIdList2.Add(domainId2);
                        }  
                    }
                }
                identity = GetMinDomainSeqIdentity(pdbId1, interfaceDomainIdList1.ToArray(), pdbId2, interfaceDomainIdList2.ToArray(), domainIdentityTable);
                if (entryReversed)
                {
                    UpdateDomainInterfaceCompIdentity(pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1, identity);
                }
                else
                {
                    UpdateDomainInterfaceCompIdentity(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, identity);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="identity"></param>
        private void UpdateDomainInterfaceCompIdentity (string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, double identity)
        {
            string updateString = string.Format("Update PfamDomainInterfaceComp Set Identity = {0} Where PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND " +
                " PdbID2 = '{3}' AND DomainInterfaceID2 = {4};", identity, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            protcidUpdate.Update(updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainIds1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainIds2"></param>
        /// <param name="domainSeqIdentityTable"></param>
        /// <returns></returns>
        private double GetMinDomainSeqIdentity (string pdbId1, long[] domainIds1, string pdbId2, long[] domainIds2, DataTable domainSeqIdentityTable)
        {
            double minSeqIdentity = 100.0;
            double identity = 0;
            foreach (long domainId1 in domainIds1)
            {
                foreach (long domainId2 in domainIds2)
                {
                    DataRow[] domainIdRows = domainSeqIdentityTable.Select(string.Format ("DomainID1 = '{0}' AND DomainID2 = '{1}'", domainId1, domainId2));
                    if (domainIdRows.Length > 0)
                    {
                        identity = Convert.ToDouble(domainIdRows[0]["Identity"].ToString ());
                        if (minSeqIdentity > identity)
                        {
                            minSeqIdentity = identity;
                        }
                    }
                }
            }

            return minSeqIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetEntryDomainInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select RelSeqId, PdbID, DomainInterfaceID, DomainID1, DomainID2 From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable entryDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            return entryDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable GetEntryDomainInterfaceCompTable(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select RelSeqId, PdbId1, DomainInterfaceID1, PdbId2, DomainInterfaceId2  From PfamDomainInterfaceComp " +
                " Where ((PdbID1 = '{0}' AND PdbID2 = '{1}') OR (PdbID1 = '{1}' AND PdbID2 = '{0}')) AND Identity < 0;", pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            return interfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable GetDomainSeqIdentityTable(string pdbId1, string pdbId2)
        {
            DataTable domainIdentityTable = new DataTable();
            domainIdentityTable.Columns.Add(new DataColumn("PdbID1"));
            domainIdentityTable.Columns.Add(new DataColumn("DomainID1"));
            domainIdentityTable.Columns.Add(new DataColumn("PdbID2"));
            domainIdentityTable.Columns.Add(new DataColumn("DomainID2"));
            domainIdentityTable.Columns.Add(new DataColumn("Identity"));
            Dictionary<string, List<long>> entryPfamDomainsDict1 = GetEntryDomainIDsDict(pdbId1);
            Dictionary<string, List<long>> entryPfamDomainsDict2 = GetEntryDomainIDsDict(pdbId2);

            foreach (string pfamAcc in entryPfamDomainsDict1.Keys)
            {
                List<long> entryDomainList1 = entryPfamDomainsDict1[pfamAcc];
                if (entryPfamDomainsDict2.ContainsKey(pfamAcc))
                {
                    List<long> entryDomainList2 = entryPfamDomainsDict2[pfamAcc];
                    RetrieveDomainSeqIdentitiesFromStruct(pdbId1, entryDomainList1.ToArray(), pdbId2, entryDomainList2.ToArray(), domainIdentityTable);
     //               RetrieveDomainSeqIdentitiesFromHmm (pdbId1, entryDomainList1.ToArray(), pdbId2, entryDomainList2.ToArray(), domainIdentityTable);
                }
            }
            return domainIdentityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainIds1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainIds2"></param>
        /// <param name="domainSeqIdentityTable"></param>
        public void RetrieveDomainSeqIdentitiesFromStruct(string pdbId1, long[] domainIds1, string pdbId2, long[] domainIds2, DataTable domainSeqIdentityTable)
        {
            // should be safe to query by "IN", there are not too many domains for a Pfam in an entry
            string queryString = string.Format("Select QueryEntry, QueryDomainID, HitEntry, HitDomainID, Identity From PfamDomainAlignments " +
                " Where (QueryDomainID In ({0}) AND HitDomainID IN ({1})) OR (QueryDomainID IN ({1}) AND HitDomainID IN ({0}));",
                ParseHelper.FormatSqlListString(domainIds1), ParseHelper.FormatSqlListString(domainIds2));
            DataTable identityTable = ProtCidSettings.alignmentQuery.Query(queryString);
            long queryDomainId = 0;
            string queryEntry = "";
            string hitEntry = "";
            long hitDomainId = 0;
            foreach (DataRow alignRow in identityTable.Rows)
            {
                queryEntry = alignRow["QueryEntry"].ToString();
                hitEntry = alignRow["HitEntry"].ToString();
                queryDomainId = Convert.ToInt64(alignRow["QueryDomainID"].ToString());
                hitDomainId = Convert.ToInt64(alignRow["HitDomainID"].ToString());
                if (queryEntry == pdbId2)
                {
                    DataRow identityRow = domainSeqIdentityTable.NewRow();
                    identityRow["PdbID1"] = pdbId2;
                    identityRow["DomainID1"] = hitDomainId;
                    identityRow["PdbID2"] = pdbId1;
                    identityRow["DomainID2"] = queryDomainId;
                    identityRow["Identity"] = alignRow["Identity"];
                    domainSeqIdentityTable.Rows.Add(identityRow);
                }
                else
                {
                    DataRow identityRow = domainSeqIdentityTable.NewRow();
                    identityRow["PdbID1"] = pdbId1;
                    identityRow["DomainID1"] = queryDomainId;
                    identityRow["PdbID2"] = pdbId2;
                    identityRow["DomainID2"] = hitDomainId;
                    identityRow["Identity"] = alignRow["Identity"];
                    domainSeqIdentityTable.Rows.Add(identityRow);
                }
            }
            domainSeqIdentityTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, List<long>> GetEntryDomainIDsDict(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_Acc, DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, List<long>> entryDomainsDict = new Dictionary<string, List<long>>();
            string pfamAcc = "";
            long domainId = 0;
            foreach (DataRow domainRow in domainTable.Rows)
            {
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                pfamAcc = domainRow["Pfam_Acc"].ToString().TrimEnd();
                if (entryDomainsDict.ContainsKey(pfamAcc))
                {
                    entryDomainsDict[pfamAcc].Add(domainId);
                }
                else
                {
                    List<long> domainList = new List<long>();
                    domainList.Add(domainId);
                    entryDomainsDict.Add(pfamAcc, domainList);
                }
            }
            return entryDomainsDict;
        }     
        #endregion

        #region sequence identity based Pfam HMM
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainIds1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainIds2"></param>
        /// <param name="domainSeqIdentityTable"></param>
        public void RetrieveDomainSeqIdentitiesFromHmm (string pdbId1, long[] domainIds1, string pdbId2, long[] domainIds2, DataTable domainSeqIdentityTable)
        {
            double identity = 0;
            foreach (long domainId1 in domainIds1)
            {
                foreach (long domainId2 in domainIds2)
                {
                   identity = GetDomainSeqIdentityOnHMM(domainId1, domainId2);
                   if (identity > -1.0)
                   {
                       DataRow identityRow = domainSeqIdentityTable.NewRow();
                       identityRow["PdbID1"] = pdbId1;
                       identityRow["DomainID1"] = domainId1;
                       identityRow["PdbID2"] = pdbId1;
                       identityRow["DomainID2"] = domainId2;
                       identityRow["Identity"] = identity;
                       domainSeqIdentityTable.Rows.Add(identityRow);
                   }
                }
            }
            domainSeqIdentityTable.AcceptChanges();
        }
        /// <summary>
        /// domains must be in same Pfam
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        public double GetDomainSeqIdentityOnHMM(long domainId1, long domainId2)
        {
            string queryString = string.Format("Select Pfam_ID, DomainID, AlignStart, AlignEnd, HmmStart, HmmEnd, QueryAlignment, HmmAlignment From PdbPfam " +
                " Where DomainID IN ({0}, {1}) Order By DomainID, HmmStart;", domainId1, domainId2);
            DataTable domainHmmAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            double identity = GetDomainSeqIdentityOnHMM(domainId1, domainId2, domainHmmAlignTable);
            return identity;

            /*         int domainLen1 = GetDomainLength (domainRows1);
                     int domainLen2 = GetDomainLength (domainRows2);

                     double identity1 = (double)numOfSameResidues / (double)domainLen1;
                     double identity2 = (double)numOfSameResidues / (double)domainLen2;

                     if (identity1 <= identity2)
                     {
                         return identity2;
                     }
                     else
                     {
                         return identity1;
                     }*/
        }

        /// <summary>
        /// domains must be in same Pfam
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        public double GetDomainSeqIdentityOnHMM(long domainId1, long domainId2, DataTable domainTable)
        {
            DataRow[] domainRows1 = domainTable.Select(string.Format("DomainID = '{0}'", domainId1));
            DataRow[] domainRows2 = domainTable.Select(string.Format("DomainID = '{0}'", domainId2));
            if (domainRows1.Length == 0 || domainRows2.Length == 0)
            {
                return -1.0;
            }
            if (domainRows1[0]["Pfam_ID"].ToString() != domainRows2[0]["Pfam_ID"].ToString())
            {
                return -1.0;
            }
            Dictionary<int, char> hmmSeqResiduesDict1 = GetDomainResiduesAtHmmPosition(domainRows1);
            Dictionary<int, char> hmmSeqResiduesDict2 = GetDomainResiduesAtHmmPosition(domainRows2);
            int numOfSameResidues = GetNumberOfSameResiduesFromHmm(hmmSeqResiduesDict1, hmmSeqResiduesDict2);
            int overlap = GetOverlapHmmLength(hmmSeqResiduesDict1, hmmSeqResiduesDict2);

            return (double)numOfSameResidues / (double)overlap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <returns></returns>
        public Dictionary<int, char> GetDomainResiduesAtHmmPosition(DataRow[] domainRows)
        {
            Dictionary<int, char> hmmSeqResiduesDict = new Dictionary<int, char>();
            int hmmPos = 0;
            int hmmStart = 0;
            string seqAlignment = "";
            string hmmAlignment = "";
            foreach (DataRow domainRow in domainRows)
            {
                seqAlignment = domainRow["QueryAlignment"].ToString();
                hmmAlignment = domainRow["HmmAlignment"].ToString();
                hmmStart = Convert.ToInt32(domainRow["HmmStart"].ToString());
                hmmPos = hmmStart;
                for (int i = 0; i < hmmAlignment.Length; i++)
                {
                    if (hmmAlignment[i] != '-')
                    {
                        if (seqAlignment[i] != '-')
                        {
                            hmmSeqResiduesDict.Add(hmmPos, seqAlignment[i]);
                        }
                        hmmPos++;
                    }
                }
            }
            return hmmSeqResiduesDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSeqResiduesDict1"></param>
        /// <param name="hmmSeqResiduesDict2"></param>
        /// <returns></returns>
        private int GetNumberOfSameResiduesFromHmm(Dictionary<int, char> hmmSeqResiduesDict1, Dictionary<int, char> hmmSeqResiduesDict2)
        {
            int numOfSameResidues = 0;
            foreach (int hmmPos in hmmSeqResiduesDict1.Keys)
            {
                if (hmmSeqResiduesDict2.ContainsKey(hmmPos))
                {
                    if (hmmSeqResiduesDict1[hmmPos] == hmmSeqResiduesDict2[hmmPos])
                    {
                        numOfSameResidues++;
                    }
                }
            }
            return numOfSameResidues;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <returns></returns>
        public int GetDomainLength(DataRow[] domainRows)
        {
            int domainLen = 0;
            foreach (DataRow domainRow in domainRows)
            {
                domainLen += Convert.ToInt32(domainRow["AlignEnd"].ToString()) - Convert.ToInt32(domainRow["AlignStart"].ToString());
            }
            return domainLen;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSeqResiduesDict1"></param>
        /// <param name="hmmSeqResiduesDict2"></param>
        /// <returns></returns>
        private int GetOverlapHmmLength(Dictionary<int, char> hmmSeqResiduesDict1, Dictionary<int, char> hmmSeqResiduesDict2)
        {
            List<int> hmmList1 = new List<int>(hmmSeqResiduesDict1.Keys);
            hmmList1.Sort();
            List<int> hmmList2 = new List<int>(hmmSeqResiduesDict2.Keys);
            hmmList2.Sort();
            int overlap = Math.Min(hmmList1[hmmList1.Count - 1], hmmList2[hmmList2.Count - 1]) - Math.Max(hmmList1[0], hmmList2[0]) + 1;
            return overlap;
        }
        #endregion
       
    }
}
