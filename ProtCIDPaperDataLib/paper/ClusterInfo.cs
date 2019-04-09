using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Threading.Tasks;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.ChainInterfaces;
using InterfaceClusterLib.DomainInterfaces;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.DomainInterfaces;

namespace ProtCIDPaperDataLib.paper
{
    public class ClusterInfo : PaperDataInfo
    {
        private int mCut = 5;
        private double minSeqIdCut = 90.0;


        #region multi-domain chain interfaces and individual domain clusters
        /// <summary>
        /// 
        /// </summary>
        public void OutputMultiDomainChainInterfacesDomainClustersInfo ()
        {
            Dictionary<int, string> multiDomainChainHomoGroupHash = GetMultiDomainChainHomoGroups();
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "MultiDomainChainInterfaces_domainClusters.txt"));
            List<int> chainGroupList = new List<int> (multiDomainChainHomoGroupHash.Keys);
            chainGroupList.Sort();

            string chainPfamArch = "";
            string domainClusterInfo = "";
            string chainClusterInfo = "";
            foreach (int chainGroupId in chainGroupList)
            {
                chainClusterInfo = GetChainClusterInfo(chainGroupId);
                chainPfamArch = (string)multiDomainChainHomoGroupHash[chainGroupId];
                dataWriter.WriteLine(chainGroupId + "\t" + chainPfamArch);
                dataWriter.WriteLine(chainClusterInfo);

                Dictionary<int, string> relDomainClusterInfoHash = GetDomainClusterSumInfo(chainPfamArch);

                string[] pfams = GetPfamsFromChainPfamArch(chainPfamArch);
                foreach (string pfam in pfams)
                {
                    domainClusterInfo = GetDomainClusterInfo(pfam);
                    if (domainClusterInfo != "")
                    {
                        dataWriter.WriteLine(domainClusterInfo);
                    }
                }
                dataWriter.WriteLine();
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        #endregion

        #region multi-domain chains and domain clusters
        ChainInterfaceCluster chainInterfaceCluster = new ChainInterfaceCluster();
        public void SelectMultiChainInterfaces ()
        {
            StreamReader dataReader = new StreamReader (Path.Combine(dataDir, "MultiDomainChainInterfaces_allDclusters.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "SelectedMultiDomainChainInterfaces.txt"));
            string line = "";
            int mcut = 5;
            double minSeqIdCut = 90.0;
            int m_chain = 0;
            double minSeqId_chain = 0;
            int m_domain = 0;
            double minSeqId_domain = 0;
            bool entryNeeded = false;
            string dataLine = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 13)
                {
                    if (entryNeeded)
                    {
                        dataWriter.WriteLine(dataLine);
                        entryNeeded = false;
                    }
                    dataLine = line;
                }
                if (fields.Length > 14)
                {
                    m_chain = Convert.ToInt32(fields[10]);
                    minSeqId_chain = Convert.ToDouble (fields[18]);
                }
                else if (fields.Length < 14)  // no clusters
                {
                    m_chain = -1;
                    minSeqId_chain = 100.0;
                }
                if (fields.Length == 14)
                {
                    m_domain = Convert.ToInt32(fields[2]);
                    minSeqId_domain = Convert.ToDouble(fields[7]);
                    if (m_chain >= mcut && minSeqId_chain < minSeqIdCut)
                    {
                        entryNeeded = false;
                    }
                    else
                    {
                        if (m_domain >= mcut && minSeqId_domain < minSeqIdCut)
                        {
                            entryNeeded = true;
                        }
                    }
                    dataLine += ("\n" + line);
                }
            }
            dataReader.Close();
            dataWriter.Close();
        }
        public void OutputMultiDomainChainInterfacesDomainClusters()
        {
            Dictionary<int, string> multiDomainChainHomoGroupHash = GetMultiDomainChainHomoGroups();
            // the domain clusters containing the chain interface
    //        StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "MultiDomainChainInterfaces_interfaceDomainClusters.txt"));
            // the domain clusters containing the Pfams
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "MultiDomainChainInterfaces_pfamDomainClusters.txt"));
            List<int> chainGroupList =new List<int> (multiDomainChainHomoGroupHash.Keys);
            chainGroupList.Sort();

            string chainPfamArch = "";
/*            string domainClusterInfo = "";
            string chainClusterInfo = "";
            int relSeqId = 0;*/
            foreach (int chainGroupId in chainGroupList)
            {
                 chainPfamArch = (string)multiDomainChainHomoGroupHash[chainGroupId];
                 GetMultiDomainChainInterfaceInfo(chainGroupId, chainPfamArch, dataWriter);
           /*     chainClusterInfo = GetChainClusterInfo(chainGroupId);
               
                dataWriter.WriteLine(chainGroupId + "\t" + chainPfamArch);
                dataWriter.WriteLine(chainClusterInfo);

                string[] pfams = GetPfamsFromChainPfamArch(chainPfamArch);
                for (int i = 0; i < pfams.Length; i ++ )
                {
                    for (int j = i; j < pfams.Length; j ++)
                    {
                        domainClusterInfo = GetDomainClusterInfo(pfams[i], pfams[j], out relSeqId);
                        if (domainClusterInfo != "")
                        {
                            dataWriter.WriteLine(domainClusterInfo);
                        }
                    }
                }*/
           /*         foreach (string pfam in pfams)
                    {
                        //              domainClusterInfo = GetDomainClusterInfo(pfam, chainPfamArch);
                        domainClusterInfo = GetDomainClusterInfo(pfam);
                        if (domainClusterInfo != "")
                        {
                            dataWriter.WriteLine(domainClusterInfo);
                        }
                    }
                dataWriter.WriteLine();
                dataWriter.Flush();*/
            }
            dataWriter.Close();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="chainPfamArch"></param>
        /// <param name="resultWriter"></param>
        public void GetMultiDomainChainInterfaceInfo (int chainGroupId, string chainPfamArch, StreamWriter resultWriter)
        {
            string queryString = string.Format("Select ClusterId, PdbID, InterfaceID, InPdb, InPisa, InAsu, PdbBu, PdbBuID, PisaBu, PisaBuId, PdbPisa, Asu " + 
                " From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0};", chainGroupId);
            DataTable groupClusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            DataTable chainGroupInterfaceTable = new DataTable();
            chainGroupInterfaceTable.Columns.Add(new DataColumn("ClusterID"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn ("PdbID"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn ("InterfaceID"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn("InPdb"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn("PdbBuID"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn("InPisa"));
            chainGroupInterfaceTable.Columns.Add(new DataColumn("PisaBuID"));

            GetMultiDomainChainInterfaces (chainGroupId, chainPfamArch, groupClusterInterfaceTable, chainGroupInterfaceTable);
            queryString = string.Format("Select ClusterId, NumOfCfgCluster, NumOfEntryCluster, NumOfCfgFamily, NumOfEntryFamily, InPdb, InPisa, InAsu, SurfaceArea, MinSeqIdentity " +
                " From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0};", chainGroupId);
            DataTable chainGroupClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            string dataLine = "";
            string interfaceDomainClusterInfo = GetChainGroupBiggestRelatedDomainClusters (chainGroupId, chainPfamArch);
            foreach (DataRow interfaceRow in chainGroupInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
         /*       if (entryList.Contains (pdbId))
                {
                    continue;
                }*/
                entryList.Add(pdbId);
                DataRow[] clusterInfoRows = chainGroupClusterTable.Select(string.Format ("ClusterID = '{0}'", interfaceRow["ClusterID"]));
                if (clusterInfoRows.Length > 0)
                {
                    dataLine = chainGroupId + "\t" + chainPfamArch + "\t" + ParseHelper.FormatDataRow(interfaceRow) + "\t" +
                        ParseHelper.FormatDataRow(clusterInfoRows[0]);
                }
                else
                {
                    dataLine = chainGroupId + "\t" + chainPfamArch + "\t" + ParseHelper.FormatDataRow(interfaceRow);
                }
//                interfaceDomainClusterInfo = GetChainInterfaceDomainClusterInfo(pdbId, interfaceId);
                if (interfaceDomainClusterInfo != "")
                {
                    resultWriter.WriteLine(dataLine);
                    resultWriter.WriteLine(interfaceDomainClusterInfo);
                }
            }
            resultWriter.Flush();
        }

        private string GetChainGroupBiggestRelatedDomainClusters(int chainGroupId, string chainRelPfamArch)
        {
            string[][] pfamPairs = GetPfamPairsFromGroupPfamArch(chainRelPfamArch);
            int[] relSeqIds = GetRelSeqIds(pfamPairs);
            string relationPfamPair = "";
            string domainClusterInfo = "";
            string chainRelDomainClusterInfo = "";
            foreach (int relSeqId in relSeqIds)
            {
                relationPfamPair = GetRelationPfamPairs(relSeqId);
                //        domainClusterInfo = GetBiggestDomainCluster(relSeqId);
                domainClusterInfo =  GetBioDomainClusters(relSeqId, mCut, minSeqIdCut);
         //       domainClusterInfo = GetBiggestRelatedDomainCluster(relSeqId, chainRelPfamArch);
                chainRelDomainClusterInfo += (relSeqId + "\t" + relationPfamPair + "\t" + domainClusterInfo + "\r\n");
            }
            return chainRelDomainClusterInfo;
        }

        private void GetMultiDomainChainInterfaces (int chainGroupId, string chainPfamArch, DataTable clusterInterfaceTable, DataTable chainGroupInterfaceTable)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo " + 
                " Where GroupSeqID IN (Select Distinct GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0});", chainGroupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID2 As PdbID From PfamHomoRepEntryAlign " +
                " Where GroupSeqID IN (Select Distinct GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0});", chainGroupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString ());
            }
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString());
            }
            string buId = "";

            foreach (string pdbId in entryList)
            {
                DataTable pdbbuCompTable = GetCrystBaInterfaceCompTable(pdbId, "pdb");
                DataTable pisabuCompTable = GetCrystBaInterfaceCompTable(pdbId, "pisa");
                int[] chainGroupInterfaceIds = GetEntryChainInterfaces(pdbId, chainPfamArch);
                foreach (int interfaceId in chainGroupInterfaceIds)
                {
                    DataRow[] clusterInterfaceRows = clusterInterfaceTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
                    DataRow newDataRow = chainGroupInterfaceTable.NewRow();
                    if (clusterInterfaceRows.Length > 0)
                    {                        
                        newDataRow["ClusterID"] = clusterInterfaceRows[0]["ClusterID"];
                        newDataRow["PdbID"] = pdbId;
                        newDataRow["InterfaceID"] = interfaceId;
                        newDataRow["InPdb"] = clusterInterfaceRows[0]["InPdb"];
                        newDataRow["InPisa"] = clusterInterfaceRows[0]["InPisa"];
                        newDataRow["PdbBuID"] = clusterInterfaceRows[0]["PdbBuID"]; ;
                        newDataRow["PisaBuID"] = clusterInterfaceRows[0]["PisaBuID"];                        
                    }
                    else
                    {                        
                        newDataRow["ClusterID"] = -1;
                        newDataRow["PdbID"] = pdbId;
                        newDataRow["InterfaceID"] = interfaceId;
                        newDataRow["InPdb"] = '0';
                        newDataRow["InPisa"] = '0';
                        newDataRow["PdbBuID"] = "-";
                        newDataRow["PisaBuID"] = "-";
                        buId = IsInterfaceInBA(pdbId, interfaceId, pdbbuCompTable);
                        if (buId != "-")
                        {
                            newDataRow["InPdb"] = '1';
                            newDataRow["PdbBuID"] = buId;
                        }
                        buId = IsInterfaceInBA(pdbId, interfaceId, pisabuCompTable);
                        if (buId != "-")
                        {
                            newDataRow["InPisa"] = '1';
                            newDataRow["PisaBuID"] = buId;
                        }
                    }                    
                    chainGroupInterfaceTable.Rows.Add(newDataRow);
                }
            }
            RemoveEntriesInBothPdbPisaBAs(entryList, chainGroupInterfaceTable);
        }

        private void RemoveEntriesInBothPdbPisaBAs(List<string> entryList, DataTable chainInterfaceTable)
        {
            foreach (string pdbId in entryList)
            {
                DataRow[] chainInterfaceRows = chainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND InPdb = '1' AND InPisa = '1'", pdbId));
                if (chainInterfaceRows.Length > 0)
                {
                    DataRow[] interfaceRows = chainInterfaceTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    foreach (DataRow interfaceRow in interfaceRows)
                    {
                        chainInterfaceTable.Rows.Remove(interfaceRow);
                    }
                }
            }
            chainInterfaceTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private int[] GetEntryChainInterfaces (string pdbId, string chainGroupPfamArch)
        {
            string[] pfamArchFields = chainInterfaceCluster.SplitEntityFamilyArchString(chainGroupPfamArch);
            int[] chainInterfaceIds = chainInterfaceCluster.GetEntryInterfaces(pdbId, pfamArchFields);
            return chainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="baType"></param>
        /// <returns></returns>
        private string IsInterfaceInBA (string pdbId, int interfaceId, DataTable buCompTable)
        {
            DataRow[] buCompRows = buCompTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));

            if (buCompRows.Length > 0)
            {
                return buCompRows[0]["BuID"].ToString ().TrimEnd ();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="baType"></param>
        /// <returns></returns>
        private DataTable GetCrystBaInterfaceCompTable (string pdbId, string baType)
        {
            string queryString = string.Format("Select PdbID, InterfaceID, BuID, Qscore From cryst{0}buinterfacecomp " +
                " Where PdbID = '{1}' AND Qscore >= 0.45;", baType, pdbId);
            DataTable buCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            return buCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetChainInterfaceDomainClusterInfo (string pdbId, int interfaceId)
        {          
//            string queryString = string.Format("Select Distinct PfamDomainClusterInterfaces.RelSeqID, ClusterID, PfamDomainClusterInterfaces.DomainInterfaceID, InPdb, InPisa, InAsu " + 
            string queryString = string.Format("Select Distinct PfamDomainClusterInterfaces.RelSeqID, ClusterID " + 
                " From PfamDomainInterfaces, PfamDomainClusterInterfaces Where PfamDomainInterfaces.PdbID = '{0}' AND InterfaceID = {1} " +
                " AND PfamDomainInterfaces.RelSeqID = PfamDomainClusterInterfaces.RelSeqID " +
                " AND PfamDomainInterfaces.PdbID = PfamDomainClusterInterfaces.PdbID " + 
                " AND PfamDomainInterfaces.DomainInterfaceID = PfamDomainClusterInterfaces.DomainInterfaceID;", pdbId, interfaceId);
            DataTable domainInterfaceClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            string entryClusterInfo = "";
            if (domainInterfaceClusterTable.Rows.Count >= 2)
            {
                foreach (DataRow dInterfaceRow in domainInterfaceClusterTable.Rows)
                {
                    queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = '{0}' AND ClusterID = {1};",
                        dInterfaceRow["RelSeqID"], dInterfaceRow["ClusterID"]);
                    DataTable clusterSumTable = ProtCidSettings.protcidQuery.Query(queryString);
                    DataRow clusterRow = clusterSumTable.Rows[0];
                    entryClusterInfo += (clusterRow["RelSeqID"].ToString() + "\t" + clusterRow["ClusterID"].ToString() + "\t" + clusterRow["NumOfCfgCluster"].ToString() + "\t" + clusterRow["NumOfCfgRelation"].ToString() + "\t" +
                       clusterRow["NumOfEntryCluster"].ToString() + "\t" + clusterRow["NumOfEntryRelation"].ToString() + "\t" +
                       clusterRow["SurfaceArea"].ToString() + "\t" + clusterRow["MinSeqIdentity"].ToString() + "\t" +
                       clusterRow["InAsu"].ToString() + "\t" + clusterRow["InPDB"].ToString() + "\t" +
                       clusterRow["InPISA"].ToString() + "\t" + clusterRow["NumOfEntryHomo"].ToString() + "\t" +
                       clusterRow["NumOfEntryHetero"].ToString() + "\t" + clusterRow["NumOfEntryIntra"].ToString() + "\n");
                }
            }
            return entryClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetDomainClusterSumInfo(string chainPfamArch)
        {
            string domainClusterInfo = "";
            int relSeqId = 0;
            Dictionary<int, string> relDomainClusterHash = new Dictionary<int,string> ();
            string[] pfams = GetPfamsFromChainPfamArch(chainPfamArch);
            for (int i = 0; i < pfams.Length; i++)
            {
                for (int j = i; j < pfams.Length; j++)
                {
                    domainClusterInfo = GetDomainClusterInfo(pfams[i], pfams[j], out relSeqId);
                    relDomainClusterHash.Add(relSeqId, domainClusterInfo);
                }
            }
            return relDomainClusterHash;
        }      
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        public string GetChainRelatedDomainClusterInfo(string pfam, string chainPfamArch)
        {
            int relSeqId = GetRelationSeqID(pfam, pfam);
            string relClusterInfo = GetBiggestRelatedDomainCluster(relSeqId, chainPfamArch);
            if (relClusterInfo == "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1")
            {
                return "";
            }
            return relSeqId + "\t" + pfam + "\t" + relClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        public string GetDomainClusterInfo(string pfam)
        {
            int relSeqId = GetRelationSeqID(pfam, pfam);
            string relClusterInfo = GetBiggestDomainCluster (relSeqId);
            if (relClusterInfo == "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1")
            {
                return "";
            }
            return relSeqId + "\t" + pfam + "\t" + relClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        public string GetDomainClusterInfo(string pfam1, string pfam2, out int relSeqId)
        {
            relSeqId = GetRelationSeqID(pfam1, pfam2);
            string relClusterInfo = GetBiggestDomainCluster (relSeqId);
            if (relClusterInfo == "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1")
            {
                return "";
            }
            return relSeqId + "\t" + pfam1 + "\t" + pfam2 + "\t" + relClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private string[] GetPfamsFromChainPfamArch(string chainPfamArch)
        {
            string[] pfamFields = chainPfamArch.Split("()".ToCharArray());
            List<string> pfamList = new List<string>();
            string pfam = "";
            foreach (string pfamField in pfamFields)
            {
                if (pfamField == "" || pfamField == "_")
                {
                    continue;
                }
                pfam = pfamField.Trim("()".ToCharArray());
                if (!pfamList.Contains(pfam))
                {
                    pfamList.Add(pfam);
                }
            }
            string[] pfams = new string[pfamList.Count];
            pfamList.Sort();
            pfamList.CopyTo(pfams);
            return pfams;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void OutputMultiDomainChainsClusterInfos()
        {
            Dictionary<string, List<int>> entryMultiChainEntityHash = GetEntryMultiDomainEntityHash();

            int cfgCutoff = 2;
            int seqIdCutoff = 90;

            StreamWriter multiDomainChainDataWriter = new StreamWriter(Path.Combine(dataDir, "MultiDomainChainClusterInfo.txt"));
            StreamWriter chainDomainClusterInfoWriter = new StreamWriter(Path.Combine(dataDir, "MultiDomainChainDomainClusterInfo_noChain_cutoff.txt"));

            List<string> entryList = new List<string> (entryMultiChainEntityHash.Keys);
            List<string> entryNoChainClusterList = new List<string> ();
            List<string> entryNoChainWithDomainList = new List<string>();
            entryList.Sort();
            string entryPfamArch = "";
            foreach (string pdbId in entryList)
            {
                try
                {
                    int[] entityIds = entryMultiChainEntityHash[pdbId].ToArray(); 
                    int[] chainInterfaceIds = GetEntityChainInterfaces(pdbId, entityIds);
                    if (chainInterfaceIds.Length == 0)
                    {
                        continue;
                    }
                    if (chainInterfaceIds.Length > 500)
                    {
                        continue;
                    }
                    //       string[] clusterInfos = GetChainInterfaceClusterInfo(pdbId, chainInterfaceIds);
                    string[] clusterInfos = GetChainInterfaceClusterInfo(pdbId, chainInterfaceIds, cfgCutoff, seqIdCutoff);
                    if (clusterInfos.Length == 0)
                    {
                        entryPfamArch = GetEntryPfamArch(pdbId);
                        int[] domainInterfaceIds = GetDomainInterfaceIds(pdbId, chainInterfaceIds);
                        if (domainInterfaceIds.Length > 0)
                        {
                            string[] domainClusterInfos = GetDomainInterfaceClusterInfos(pdbId, domainInterfaceIds);
                            foreach (string domainClusterInfo in domainClusterInfos)
                            {
                                chainDomainClusterInfoWriter.WriteLine(pdbId + "\t" + entryPfamArch + "\t" + domainClusterInfo);
                            }
                            if (domainClusterInfos.Length > 0)
                            {
                                entryNoChainWithDomainList.Add(pdbId);
                            }
                        }
                        entryNoChainClusterList.Add(pdbId);
                    }
                    else
                    {
                        foreach (string clusterInfo in clusterInfos)
                        {
                            multiDomainChainDataWriter.WriteLine(pdbId + "\t" + clusterInfo);
                        }
                    }
                    chainDomainClusterInfoWriter.Flush();
                    multiDomainChainDataWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": " + ex.Message);
                }
            }
            multiDomainChainDataWriter.Close();
            chainDomainClusterInfoWriter.WriteLine("#Entries with no chain clusters: " + entryNoChainClusterList.Count.ToString());
            chainDomainClusterInfoWriter.WriteLine("#Entries with no chain clusters but with domain clusters (M >= 2 and SeqID <= 90%): " + entryNoChainWithDomainList.Count.ToString());
            chainDomainClusterInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryPfamArch(string pdbId)
        {
            string queryString = string.Format("Select EntryPfamArch From PfamEntryPfamARch WHere PdbID = '{0}';", pdbId);
            DataTable entryPfamArchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string entryPfamArch = "";
            if (entryPfamArchTable.Rows.Count > 0)
            {
                entryPfamArch = entryPfamArchTable.Rows[0]["EntryPfamArch"].ToString().TrimEnd();
            }
            return entryPfamArch;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceIds"></param>
        /// <returns></returns>
        private string[] GetChainInterfaceClusterInfo(string pdbId, int[] chainInterfaceIds)
        {
            string queryString = "";
            int groupId = 0;
            int clusterId = 0;
            Dictionary<int, List<int>> groupClusterIdHash = new Dictionary<int,List<int>> ();
            for (int i = 0; i < chainInterfaceIds.Length; i += 500)
            {
                int[] subChainInterfaceIds = ParseHelper.GetSubArray(chainInterfaceIds, i, 500);
                queryString = string.Format("Select Distinct SuperGroupSeqID, ClusterID From PfamSuperClusterEntryInterfaces " +
                        " WHere PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(subChainInterfaceIds));
                DataTable subClusterIdTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow clusterRow in subClusterIdTable.Rows)
                {
                    groupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());
                    clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                    if (groupClusterIdHash.ContainsKey(groupId))
                    {
                        groupClusterIdHash[groupId].Add(clusterId);
                    }
                    else
                    {
                        List<int> clusterIdList = new List<int> ();
                        clusterIdList.Add(clusterId);
                        groupClusterIdHash.Add(groupId, clusterIdList);
                    }
                }
            }
                     
            List<string> clusterInfoList = new List<string> ();
            foreach (int superGroupId in groupClusterIdHash.Keys)
            {
                int[] clusterIds = groupClusterIdHash[superGroupId].ToArray();
                string[] thisClusterInfos = GetGroupClusterInfo(superGroupId, clusterIds);
                clusterInfoList.AddRange(thisClusterInfos);
            }
            return clusterInfoList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceIds"></param>
        /// <param name="cfgCutoff"></param>
        /// <param name="seqIdCutoff"></param>
        /// <returns></returns>
        private string[] GetChainInterfaceClusterInfo(string pdbId, int[] chainInterfaceIds, int cfgCutoff, int seqIdCutoff)
        {
            string queryString = "";
            int groupId = 0;
            int clusterId = 0;
            Dictionary<int, List<int>> groupClusterIdHash = new Dictionary<int, List<int>>();
            for (int i = 0; i < chainInterfaceIds.Length; i += 500)
            {
                int[] subInterfaceIds = ParseHelper.GetSubArray (chainInterfaceIds, i, 500);
                queryString = string.Format("Select Distinct SuperGroupSeqID, ClusterID From PfamSuperClusterEntryInterfaces " +
                           " WHere PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(subInterfaceIds));
                DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query(queryString);

                foreach (DataRow clusterIdRow in clusterIdTable.Rows)
                {
                    groupId = Convert.ToInt32(clusterIdRow["SuperGroupSeqID"].ToString());
                    clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                    if (groupClusterIdHash.ContainsKey(groupId))
                    {
                        groupClusterIdHash[groupId].Add(clusterId);
                    }
                    else
                    {
                        List<int> clusterIdList = new List<int> ();
                        clusterIdList.Add(clusterId);
                        groupClusterIdHash.Add(groupId, clusterIdList);
                    }
                }
            }
            List<string> clusterInfoList = new List<string> ();
            foreach (int superGroupId in groupClusterIdHash.Keys)
            {
                string[] thisClusterInfos = GetGroupClusterInfo(superGroupId, groupClusterIdHash[superGroupId].ToArray (), cfgCutoff, seqIdCutoff);
                clusterInfoList.AddRange(thisClusterInfos);
            }
            return clusterInfoList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetGroupClusterInfo(int groupId, int[] clusterIds)
        {
            string queryString = string.Format("Select SuperGroupSeqId, ClusterID, InPDB, InPisa, NumOfCfgCluster, NumOfCfgFamily, NumOfEntryCluster, NumOfEntryFamily, MinSeqIdentity, SurfaceArea" +
                " From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0} AND ClusterID IN ({1});", groupId, ParseHelper.FormatSqlListString(clusterIds));
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterInfos = new string[clusterSumInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow sumInfoRow in clusterSumInfoTable.Rows)
            {
                clusterInfos[count] = ParseHelper.FormatDataRow(sumInfoRow);
                count++;
            }
            return clusterInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetGroupClusterInfo(int groupId, int[] clusterIds, int cfgCutoff, int seqIdCutoff)
        {
            string queryString = string.Format("Select SuperGroupSeqId, ClusterID, InPDB, InPisa, NumOfCfgCluster, NumOfCfgFamily, NumOfEntryCluster, NumOfEntryFamily, MinSeqIdentity, SurfaceArea" +
                " From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0} AND ClusterID IN ({1}) AND NumOfCfgCluster >= '{2}' AND MinSeqIdentity <= {3};",
                groupId, ParseHelper.FormatSqlListString(clusterIds), cfgCutoff, seqIdCutoff);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterInfos = new string[clusterSumInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow sumInfoRow in clusterSumInfoTable.Rows)
            {
                clusterInfos[count] = ParseHelper.FormatDataRow(sumInfoRow);
                count++;
            }
            return clusterInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetGroupClusterInfo(int groupId, int[] clusterIds, int cfgCutoff)
        {
            string queryString = string.Format("Select SuperGroupSeqId, ClusterID, InPDB, InPisa, NumOfCfgCluster, NumOfCfgFamily, NumOfEntryCluster, NumOfEntryFamily, MinSeqIdentity, SurfaceArea" +
                " From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0} AND ClusterID IN ({1}) AND NumOfCfgCluster >= '{2}';",
                groupId, ParseHelper.FormatSqlListString(clusterIds), cfgCutoff);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterInfos = new string[clusterSumInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow sumInfoRow in clusterSumInfoTable.Rows)
            {
                clusterInfos[count] = ParseHelper.FormatDataRow(sumInfoRow);
                count++;
            }
            return clusterInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        private int[] GetEntityChainInterfaces(string pdbId, int[] entityIds)
        {
            string queryString = string.Format("Select InterfaceID From CrystEntryInterfaces " +
                " WHere PdbID = '{0}' AND (EntityID1 IN ({1}) OR EntityID2 IN ({1}));", pdbId, ParseHelper.FormatSqlListString(entityIds));
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] chainInterfaceIds = new int[interfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                chainInterfaceIds[count] = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                count++;
            }
            return chainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIds(string pdbId, int[] chainInterfaceIds)
        {
            string queryString = "";
            List<int> domainInterfaceIdList = new List<int>();
            int domainInterfaceId = 0;
            for (int i = 0; i < chainInterfaceIds.Length; i += 500)
            {
                int[] subInterfaceIds = ParseHelper.GetSubArray(chainInterfaceIds, i, 500);
                queryString = string.Format("Select Distinct DomainInterfaceID From PfamDomainInterfaces " +
                     " Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(subInterfaceIds));
                DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow dInterfaceIdRow in domainInterfaceIdTable.Rows)
                {
                    domainInterfaceId = Convert.ToInt32(dInterfaceIdRow["DomainInterfaceID"].ToString ());
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }
            return domainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private string[] GetDomainInterfaceClusterInfos(string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format("Select RelSeqId, ClusterID From PfamDomainClusterInterfaces " +
                " WHere PdbID = '{0}' AND DomainInterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable relClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<int, List<int>> relClusterHash = new Dictionary<int,List<int>> ();
            int relSeqId = 0;
            int clusterId = 0;
            foreach (DataRow clusterRow in relClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                if (relClusterHash.ContainsKey(relSeqId))
                {
                    relClusterHash[relSeqId].Add(clusterId);
                }
                else
                {
                    List<int> clusterIdList = new List<int> ();
                    clusterIdList.Add(clusterId);
                    relClusterHash.Add(relSeqId, clusterIdList);
                }
            }
            List<string> clusterInfoList = new List<string> ();
            string relPfamPair = "";
            foreach (int keyRelSeqId in relClusterHash.Keys)
            {
                relPfamPair = GetRelationPfamPairs(keyRelSeqId);
                if (relPfamPair == "C1-set;V-set" || relPfamPair == "V-set;V-set" || relPfamPair == "C1-set;C1-set")
                {
                    continue;
                }
                string[] thisClusterInfos = GetDomainClusterInfos(keyRelSeqId, relClusterHash[keyRelSeqId].ToArray (), relPfamPair);
                clusterInfoList.AddRange(thisClusterInfos);
            }
            return clusterInfoList.ToArray ();
        }
   
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetDomainClusterInfos(int relSeqId, int[] clusterIds, string relPfamPair)
        {
            string queryString = string.Format("Select RelSeqID, ClusterID, InPdb, InPisa, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, NumOfEntryRelation, MinSeqIdentity " +
                " From PfamDOmainClusterSumInfo Where RelSeqId = {0} AND ClusterID IN ({1}) AND NumOfCfgCluster >= 2 AND MinSeqIdentity < 90;",
                relSeqId, ParseHelper.FormatSqlListString(clusterIds));
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] domainClusterInfos = new string[clusterInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
            {
                domainClusterInfos[count] = relPfamPair + "\t" + ParseHelper.FormatDataRow(clusterInfoRow);
                count++;
            }
            return domainClusterInfos;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relPfamPairHash"></param>
        /// <returns></returns>
        public string GetRelationPfamPairs(int relSeqId)
        {
            string pfamPair = "";
            if (relSeqId <= 0)
            {
                return "";
            }
            if (relPfamPairHash.ContainsKey(relSeqId))
            {
                pfamPair = (string)relPfamPairHash[relSeqId];
            }
            else
            {
                string queryString = string.Format("Select FamilyCode1, FamilyCOde2 From PfamDomainFamilyRelation where RelSeqID = {0};", relSeqId);
                DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
                pfamPair = pfamPairTable.Rows[0]["FamilyCOde1"].ToString().TrimEnd() + ";" + pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                relPfamPairHash.Add(relSeqId, pfamPair);
            }
            return pfamPair;
        }   
        #endregion

        #region domain cluster summary
        /// <summary>
        /// 
        /// </summary>
        public void PrintDomainClusterSumInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "DomainClusterSumInfo.txt"), true);
            DataTable pfamDefTable = GetRelationDefinition();
            string queryString = "Select Distinct RelSeqID, ClusterID From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 2 and MinSeqIdentity < 90;";
            DataTable relClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> relList = new List<int>();
            List<int> difPfamRelList = new List<int>();
            int numOfDifPfamClusters = 0;
            int relSeqId = 0;
            int clusterId = 0;
            string[] clusterEntries = null;
            List<string> uniqueEntryList = new List<string>();
            foreach (DataRow clusterRow in relClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                clusterEntries = GetClusterEntries(relSeqId, clusterId);
                if (!relList.Contains(relSeqId))
                {
                    relList.Add(relSeqId);
                    if (IsRelationDifPfam(relSeqId, pfamDefTable))
                    {
                        difPfamRelList.Add(relSeqId);
                    }
                }
                if (difPfamRelList.Contains(relSeqId))
                {
                    numOfDifPfamClusters++;
                }
                foreach (string pdbId in clusterEntries)
                {
                    if (!uniqueEntryList.Contains(pdbId))
                    {
                        uniqueEntryList.Add(pdbId);
                    }
                }
            }
            dataWriter.WriteLine(DateTime.Today.ToShortDateString());
            dataWriter.WriteLine("Summary of domain clusters with #CF/Cluster >= 2 and minimum sequence identity < 90%");
            dataWriter.WriteLine("# of Pfam-Pfam relations = " + relList.Count.ToString());
            dataWriter.WriteLine("# of Dif-Pfam relations = " + difPfamRelList.Count.ToString());
            dataWriter.WriteLine("# of Pfam-Pfam relation clusters = " + relClusterTable.Rows.Count.ToString());
            dataWriter.WriteLine("# of Dif-Pfam clusters = " + numOfDifPfamClusters.ToString());
            dataWriter.WriteLine("# of entries = " + uniqueEntryList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relationTable"></param>
        /// <returns></returns>
        private bool IsRelationDifPfam(int relSeqId, DataTable relationTable)
        {
            DataRow[] relDefRows = relationTable.Select(string.Format("RelSeqID = '{0}'", relSeqId));
            if (relDefRows.Length > 0)
            {
                if (relDefRows[0]["FamilyCode1"].ToString() != relDefRows[0]["FamilyCode2"].ToString())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetRelationDefinition()
        {
            string queryString = "Select RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable relationTable = ProtCidSettings.protcidQuery.Query(queryString);
            return relationTable;
        }
        #endregion

        #region sequences - chain interface cluster
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        public void PrintChainClusterSequenceFile(int groupId, int clusterId)
        {
            string queryString = string.Format("Select PdbId, InterfaceID From PfamSuperClusterEntryInterfaces " +
                " WHere SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", groupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            DataTable clusterInterfaceSeqTable = GetClusterInterfaceEntitySequences(clusterInterfaceTable);
            StreamWriter seqWriter = new StreamWriter(Path.Combine(dataDir, groupId.ToString() + "_" + clusterId.ToString() + ".seq"));
            foreach (DataRow seqRow in clusterInterfaceSeqTable.Rows)
            {
                seqWriter.WriteLine(">" + seqRow["PdbID"].ToString() + seqRow["InterfaceID"].ToString() + " " +
                    seqRow["EntityID"].ToString() + " " + seqRow["ChainPfamArch"].ToString() + " " + seqRow["UnpCode"].ToString());
                seqWriter.WriteLine(seqRow["Sequence"].ToString());
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        private DataTable GetClusterInterfaceEntitySequences(DataTable clusterInterfaceTable)
        {
            DataTable clusterInterfaceSeqTable = new DataTable();
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("PdbID"));
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("InterfaceID"));
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("EntityID"));
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("Sequence"));
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("ChainPfamArch"));
            clusterInterfaceSeqTable.Columns.Add(new DataColumn("UnpCode"));

            string pdbId = "";
            int interfaceId = 0;
            List<string> addedEntryList = new List<string> ();
            string unpCode = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                if (addedEntryList.Contains(pdbId))
                {
                    continue;
                }
                addedEntryList.Add(pdbId);
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                Dictionary<int, string> entitySeqHash = GetInterfaceEntitySequences(pdbId, interfaceId);
                foreach (int entityId in entitySeqHash.Keys)
                {
                    unpCode = GetEntityUnpCode(pdbId, entityId);
                    DataRow seqRow = clusterInterfaceSeqTable.NewRow();
                    seqRow["PdbID"] = pdbId;
                    seqRow["InterfaceID"] = interfaceId;
                    seqRow["EntityID"] = entityId;
                    seqRow["Sequence"] = (string)entitySeqHash[entityId];
                    seqRow["UNPCOde"] = unpCode;
                    seqRow["ChainPfamArch"] = pfamArch.GetEntityGroupPfamArch(pdbId, entityId);
                    clusterInterfaceSeqTable.Rows.Add(seqRow);
                }
            }
            return clusterInterfaceSeqTable;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetInterfaceEntitySequences(string pdbId, int interfaceId)
        {
            DataTable entitySeqTable = GetEntryEntitySeqTable(pdbId);
            string queryString = string.Format("Select * From CrystEntryInterfaces WHere PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            int entityId1 = 0;
            int entityId2 = 0;
            string sequence = "";
            Dictionary<int, string> entitySeqHash = new Dictionary<int,string> ();
            if (interfaceTable.Rows.Count > 0)
            {
                entityId1 = Convert.ToInt32(interfaceTable.Rows[0]["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceTable.Rows[0]["EntityID2"].ToString());
                sequence = GetEntitySequence(pdbId, entityId1, entitySeqTable);
                entitySeqHash.Add(entityId1, sequence);
                if (entityId1 != entityId2)
                {
                    sequence = GetEntitySequence(pdbId, entityId2, entitySeqTable);
                    entitySeqHash.Add(entityId2, sequence);
                }
            }
            return entitySeqHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="entitySeqTable"></param>
        /// <returns></returns>
        private string GetEntitySequence(string pdbId, int entityId, DataTable entitySeqTable)
        {
            DataRow[] seqRows = entitySeqTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
            string sequence = "";
            if (seqRows.Length > 0)
            {
                sequence = seqRows[0]["Sequence"].ToString().TrimEnd();
            }
            return sequence;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryEntitySeqTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, EntityID, Sequence From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return entitySeqTable;
        }
        #endregion

        #region chain/domain clusters
        public void PrintDomainClusterInfo()
        {
            int relSeqId = 14511;
            int clusterId = 1;
            string queryString = string.Format("Select ChainPfamArch, RelCfGroupId, SpaceGroup, PdbID, SurfaceArea, InPdb, InPisa, InAsu, PdbBU, PisaBU, Asu, UnpCode " +
                " From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1} Order By UnpCode, PdbID;", relSeqId, clusterId);
            DataTable clusterInterfaceInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "Pkinase_TyrClusterInterfaceInfo.txt"));
            foreach (DataRow interfaceInfoRow in clusterInterfaceInfoTable.Rows)
            {
                dataWriter.WriteLine(ParseHelper.FormatDataRow(interfaceInfoRow));
            }
            dataWriter.Close();
        }

        public void PrintDomainClustersChainGroupsSumInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "DomainClustersWithDifChainGroups.txt"));
            dataWriter.WriteLine("RelationString\tHasBiolChainCluster\tRelSeqID\tClusterID\t#CfCluster\t#CfRelation\t#EntryCluster\t#EntryRelation\t" +
                "MinSeqIdentity\tSurfaceArea\tInPdb\tInPisa\tInAsu\tGroupPfamArch\tSuperGroupId\t#CfCluster\t#CfRelation\t" +
                "#EntryCluster\t#EntryRelation\tSurfaceArea\tMinSeqIdentity");
            string queryString = "Select RelSeqID, ClusterID, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, " +
                " NumOfEntryRelation, MinSeqIdentity, SurfaceArea, InPdb, InPisa, InAsu " +
                " From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 5 AND MinSeqIdentity < 90;";
            DataTable biolDomainClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            string[] relPfamPair = null;
            string relationString = "";
            string dataLine = "";
            string groupChainPfamArch = "";
            int superGroupId = 0;
            string chainClusterInfo = "";
            bool relationChainParsed = false;
            bool isBiolChainCluster = false;
            bool hasBiolChainCluster = false;
            List<string> chainClusterInfoList = new List<string>();
            foreach (DataRow domainClusterRow in biolDomainClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(domainClusterRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(domainClusterRow["ClusterID"].ToString());
                DataTable interfaceChainPfamArchTable = GetDomainClusterInterfaceChainPfamArches(relSeqId, clusterId);
                string[] chainPfamArches = GetDomainClusterChainPfamArches(interfaceChainPfamArchTable);
                relPfamPair = GetDomainRelationPfams(relSeqId);
                if (relPfamPair[0] == relPfamPair[1])
                {
                    relationString = "(" + relPfamPair[0] + ")";
                }
                else
                {
                    relationString = "(" + relPfamPair[0] + ");(" + relPfamPair[1] + ")";
                }
                relationChainParsed = false;
                hasBiolChainCluster = false;
                chainClusterInfoList.Clear();
                foreach (string chainPfamArch in chainPfamArches)
                {
                    groupChainPfamArch = chainPfamArch;
                    if (groupChainPfamArch == relationString)
                    {
                        relationChainParsed = true;
                    }
                    superGroupId = GetGroupSeqId(groupChainPfamArch);
                    chainClusterInfo = GetBiggestChainCluster(superGroupId, out isBiolChainCluster);
                    chainClusterInfo = groupChainPfamArch + "\t" + superGroupId.ToString() + "\t" + chainClusterInfo;
                    if (isBiolChainCluster)
                    {
                        hasBiolChainCluster = true;
                    }
                    chainClusterInfoList.Add(chainClusterInfo);
                }
                if (!relationChainParsed)
                {
                    superGroupId = GetGroupSeqId(relationString);
                    chainClusterInfo = GetBiggestChainCluster(superGroupId, out isBiolChainCluster);
                    if (isBiolChainCluster)
                    {
                        hasBiolChainCluster = true;
                    }
                    chainClusterInfo = relationString + "\t" + superGroupId.ToString() + "\t" + chainClusterInfo;
                    chainClusterInfoList.Add(chainClusterInfo);
                }
                foreach (string lsChainClusterInfo in chainClusterInfoList)
                {
                    dataLine = relationString + "\t";
                    if (hasBiolChainCluster)
                    {
                        dataLine = dataLine + "1";
                    }
                    else
                    {
                        dataLine = dataLine + "0";
                    }
                    dataLine = dataLine + "\t" + ParseHelper.FormatDataRow(domainClusterRow) + "\t" + lsChainClusterInfo;
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupChainPfamArch"></param>
        /// <returns></returns>
        private int GetGroupSeqId(string groupChainPfamArch)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups WHere ChainRelPfamArch = '{0}';", groupChainPfamArch);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int superGroupId = 0;
            if (superGroupIdTable.Rows.Count > 0)
            {
                superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            return superGroupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private string FormatGroupChainPfamArch(string chainPfamArch)
        {
            string groupChainPfamArch = "";
            string[] pfamArchFields = chainPfamArch.Split(';');
            if (pfamArchFields.Length == 1)
            {
                groupChainPfamArch = pfamArchFields[0];
            }
            else
            {
                groupChainPfamArch = pfamArchFields[0] + ";" + pfamArchFields[1];
            }
            return groupChainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintBiolDomainClustersChainPfamArchInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "BiolDClustersMultiDomainAccPfams.txt"));
            string queryString = "Select RelSeqID, ClusterID, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, " +
                " NumOfEntryRelation, MinSeqIdentity, SurfaceArea, InPdb, InPisa, InAsu " +
                " From PfamDomainClusterSumInfo Where NumOfCfgRelation >= 5 AND MinSeqIdentity < 90;";
            DataTable biolDomainClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            string[] relPfamPair = null;
            string relationString = "";
            string dataLine = "";
            string accDataLine = "";
            foreach (DataRow domainClusterRow in biolDomainClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(domainClusterRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(domainClusterRow["ClusterID"].ToString());
                DataTable interfaceChainPfamArchTable = GetDomainClusterInterfaceChainPfamArches(relSeqId, clusterId);
                string[] chainPfamArches = GetDomainClusterChainPfamArches(interfaceChainPfamArchTable);
                relPfamPair = GetDomainRelationPfams(relSeqId);
                if (relPfamPair[0] == relPfamPair[1])
                {
                    relationString = "(" + relPfamPair[0] + ")";
                }
                else
                {
                    relationString = "(" + relPfamPair[0] + ");(" + relPfamPair[1] + ")";
                }
                foreach (string chainPfamArch in chainPfamArches)
                {
                    if (chainPfamArch == relationString)
                    {
                        continue;
                    }
                    Dictionary<string, List<string>> completeAccPfamHash = GetCompleteAccPfams(chainPfamArch, relPfamPair, interfaceChainPfamArchTable);
                    dataLine = relationString + "\t" + ParseHelper.FormatDataRow(domainClusterRow) + "\t" + chainPfamArch + "\t";
                    foreach (string accPfam in completeAccPfamHash.Keys)
                    {
                        List<string> entityList = completeAccPfamHash[accPfam];
                        accDataLine = accPfam + ": ";
                        foreach (string entity in entityList)
                        {
                            accDataLine += (entity + ",");
                        }
                        dataLine += (accDataLine.TrimEnd(',') + "\t");
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd('\t'));
                    dataWriter.Flush();
                }
                //    int[] superGroupIds = GetDomainRelatedSuperGroupIds (relPfamPair);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <param name="relPfamPair"></param>
        /// <param name="interfaceChainPfamArchTabl"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetCompleteAccPfams(string chainPfamArch, string[] relPfamPair, DataTable interfaceChainPfamArchTable)
        {
            string[] accPfams = ParseAccessoryPfams(chainPfamArch, relPfamPair);
            string[] chainPfamArchEntries = GetChainPfamArchEntries(chainPfamArch, interfaceChainPfamArchTable);
            int entityId = 0;
            Dictionary<string, List<string>> completeAccPfamEntityHash = new Dictionary<string,List<string>> ();
            foreach (string pdbId in chainPfamArchEntries)
            {
                entityId = GetEntryEntity(pdbId, chainPfamArch);
                foreach (string accPfam in accPfams)
                {
                    if (IsDomainComplete(pdbId, entityId, accPfam))
                    {
                        if (completeAccPfamEntityHash.ContainsKey(accPfam))
                        {
                            completeAccPfamEntityHash[accPfam].Add(pdbId + entityId.ToString());
                        }
                        else
                        {
                            List<string> entityList = new List<string> ();
                            entityList.Add(pdbId + entityId.ToString());
                            completeAccPfamEntityHash.Add(accPfam, entityList);
                        }
                    }
                }
            }
            return completeAccPfamEntityHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private int GetEntryEntity(string pdbId, string chainPfamArch)
        {
            string queryString = string.Format("Select EntityID From PfamEntityPfamArch Where PdbID = '{0}' AND PfamArch = '{1}';", pdbId, chainPfamArch);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int entityId = -1;
            if (entityTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString());
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <param name="chainPfamArchEntryTable"></param>
        /// <returns></returns>
        private string[] GetChainPfamArchEntries(string chainPfamArch, DataTable chainPfamArchEntryTable)
        {
            DataRow[] chainPfamArchRows = chainPfamArchEntryTable.Select(string.Format("ChainPfamArch = '{0}'", chainPfamArch));
            List<string> entryList = new List<string>();
            string pdbId = "";
            foreach (DataRow chainPfamArchRow in chainPfamArchRows)
            {
                pdbId = chainPfamArchRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <param name="relPfams"></param>
        /// <returns></returns>
        private string[] ParseAccessoryPfams(string chainPfamArch, string[] relPfams)
        {
            List<string> accPfamIdList = new List<string>();
            string[] fields = chainPfamArch.Split(';');
            string accPfam = "";
            foreach (string pfamArch in fields)
            {
                string[] pfamArchFields = pfamArch.Split(')');
                foreach (string pfamArchField in pfamArchFields)
                {
                    accPfam = pfamArchField.Trim("_(".ToCharArray());
                    if (accPfam != "")
                    {
                        if (!accPfamIdList.Contains(accPfam))
                        {
                            accPfamIdList.Add(accPfam);
                        }
                    }
                }
            }
            foreach (string relPfam in relPfams)
            {
                accPfamIdList.Remove(relPfam);
            }
            string[] accPfams = new string[accPfamIdList.Count];
            accPfamIdList.CopyTo(accPfams);
            return accPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private bool IsDomainComplete(string pdbId, int entityId, string pfamId)
        {
            string queryString = string.Format("Select DomainID, HmmStart, HmmEnd From PdbPfam Where PdbID = '{0}' AND EntityID = {1} AND Pfam_ID = '{2}';", pdbId, entityId, pfamId);
            DataTable hmmRangeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<long> domainIdList = new List<long>();
            long domainId = 0;
            foreach (DataRow domainRow in hmmRangeTable.Rows)
            {
                domainId = Convert.ToInt64(domainRow["DOmainID"].ToString());
                if (!domainIdList.Contains(domainId))
                {
                    domainIdList.Add(domainId);
                }
            }
            int hmmLength = GetHmmLength(pfamId);
            int alignHmmLength = 0;
            bool isDomainComplete = false;
            double coverage = 0;
            foreach (long lsDomainId in domainIdList)
            {
                DataRow[] domainRows = hmmRangeTable.Select(string.Format("DomainID = '{0}'", lsDomainId));
                alignHmmLength = 0;
                foreach (DataRow domainRow in domainRows)
                {
                    alignHmmLength += (Convert.ToInt32(domainRow["HmmEnd"].ToString()) - Convert.ToInt32(domainRow["HmmStart"].ToString()) + 1);
                }
                coverage = (double)alignHmmLength / (double)hmmLength;
                if (coverage >= 0.80)
                {
                    isDomainComplete = true;
                    break;
                }
            }
            return isDomainComplete;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetHmmLength(string pfamId)
        {
            string queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable hmmLenTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int hmmLength = 0;
            if (hmmLenTable.Rows.Count > 0)
            {
                hmmLength = Convert.ToInt32(hmmLenTable.Rows[0]["ModelLength"].ToString());
            }
            return hmmLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetDomainClusterInterfaceChainPfamArches(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID, ChainPfamArch From PfamDomainClusterInterfaces " +
                " WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable interfaceChainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            return interfaceChainPfamArchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceChainPfamArchTable"></param>
        /// <returns></returns>
        private string[] GetDomainClusterChainPfamArches(DataTable clusterInterfaceChainPfamArchTable)
        {
            List<string> chainPfamArchList = new List<string>();
            string chainPfamArch = "";
            foreach (DataRow interfaceRow in clusterInterfaceChainPfamArchTable.Rows)
            {
                chainPfamArch = interfaceRow["ChainPfamArch"].ToString().TrimEnd();
                if (!chainPfamArchList.Contains(chainPfamArch))
                {
                    chainPfamArchList.Add(chainPfamArch);
                }
            }
            string[] chainPfamArches = new string[chainPfamArchList.Count];
            chainPfamArchList.CopyTo(chainPfamArches);
            return chainPfamArches;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relPfamPair"></param>
        /// <returns></returns>
        private int[] GetDomainRelatedSuperGroupIds(string[] relPfamPair)
        {
            int[] superGroupIds = null;
            if (relPfamPair[0] == relPfamPair[1])
            {
                superGroupIds = GetSuperGroupIds(relPfamPair[0]);
            }
            else
            {
                superGroupIds = GetSuperGroupIds(relPfamPair[0], relPfamPair[1]);
            }
            return superGroupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetSuperGroupIds(string pfamId)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamArch like '%{0}%';", pfamId);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] superGroupIds = new int[superGroupIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow superGroupIdRow in superGroupIdTable.Rows)
            {
                superGroupIds[count] = Convert.ToInt32(superGroupIdRow["SuperGroupSeqID"].ToString());
                count++;
            }
            return superGroupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int[] GetSuperGroupIds(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups Where ChainRelPfamArch like '%{0}%';", pfamId1);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> superGroupIdList = new List<int>();
            int superGroupId = 0;
            string chainPfamArch = "";
            foreach (DataRow superGroupIdRow in superGroupIdTable.Rows)
            {
                chainPfamArch = superGroupIdRow["ChainRelPfamArch"].ToString().TrimEnd();
                if (chainPfamArch.IndexOf(pfamId2) > -1)
                {
                    superGroupId = Convert.ToInt32(superGroupIdRow["SuperGroupSeqID"].ToString());
                    superGroupIdList.Add(superGroupId);
                }
            }
            int[] superGroupIds = new int[superGroupIdList.Count];
            superGroupIdList.CopyTo(superGroupIds);
            return superGroupIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetDomainRelationPfams(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDOmainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pfamPairs = new string[2];
            pfamPairs[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            pfamPairs[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return pfamPairs;
        }
        /// <summary>
        /// 
        /// </summary>
        public void MapChainGroupIdsToDomainRelations()
        {

            DataTable groupMapTable = new DataTable("ChainDomainGroupIdMap");
            groupMapTable.Columns.Add(new DataColumn("SuperGroupSeqID"));
            groupMapTable.Columns.Add(new DataColumn("RelSeqID"));

            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupId = 0;
            string chainPfamArch = "";
            foreach (DataRow groupRow in chainGroupTable.Rows)
            {
                groupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());
                chainPfamArch = groupRow["ChainRelPfamArch"].ToString().TrimEnd();
                string[] pfamIds = GetPfamsFromGroupPfamArch(chainPfamArch);
                int[] relSeqIds = GetRelSeqIds(pfamIds);
                foreach (int relSeqId in relSeqIds)
                {
                    DataRow dataRow = groupMapTable.NewRow();
                    dataRow["SuperGroupSeqID"] = groupId;
                    dataRow["RelSeqID"] = relSeqId;
                    groupMapTable.Rows.Add(dataRow);
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, groupMapTable);
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintChainDomainClusters()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "ChainDomainClusterComp_match.txt"));
            dataWriter.WriteLine("SuperGroup\tNumCfgCluster\tNumCfgGroup\tNumEntryCluster\tNumEntryGroup\tSA\tMinSeqID\t" +
                "Relation\tClusterID\tD_NumCfgCluster\tNumCfgRelation\tD_NumEntryCluster\tNumEntryRelation\tD_SA\tD_MinSeqID\t" +
                "D_InAsu\tD_InPDB\tD_INPISA\tNumOfEntryHomo\tNumOfEntryHetero\tNumOfEntryIntra");
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupId = 0;
            string chainRelPfamArch = "";
            string[][] pfamPairs = null;
            string chainClusterInfo = "";
            string domainClusterInfo = "";
            string relationPfamPair = "";
            bool isBiolCluster = false;
            foreach (DataRow chainGroupRow in chainGroupTable.Rows)
            {
                groupId = Convert.ToInt32(chainGroupRow["SuperGroupSeqID"].ToString());
                chainRelPfamArch = chainGroupRow["ChainRelPFamARch"].ToString().TrimEnd();
                chainClusterInfo = GetBiggestChainCluster(groupId, out isBiolCluster);

                //    pfamIds = GetPfamsFromGroupPfamArch(chainRelPfamArch);
                //     int[] relSeqIds = GetRelSeqIds(pfamIds);
                pfamPairs = GetPfamPairsFromGroupPfamArch(chainRelPfamArch);
                int[] relSeqIds = GetRelSeqIds(pfamPairs);
                foreach (int relSeqId in relSeqIds)
                {
                    relationPfamPair = GetRelationPfamPairs(relSeqId);
                    //        domainClusterInfo = GetBiggestDomainCluster(relSeqId);
                    domainClusterInfo = GetBiggestRelatedDomainCluster(relSeqId, chainRelPfamArch);
                    dataWriter.WriteLine(chainRelPfamArch + "\t" + chainClusterInfo + "\t" + relationPfamPair + "\t" + domainClusterInfo);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void OutputChainDomainCompInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterComp_NoChain_match.txt"));
            string chainDomainClusterCompFile = Path.Combine(dataDir, "ChainDomainClusterComp_match.txt");
            string headerLine = "";
            DataTable chainDomainClusterCompTable = ReadChainDomainClusterCompTable(chainDomainClusterCompFile, out headerLine);
            dataWriter.WriteLine(headerLine);

            int numCfgChainCluster = 0;
            double chainMinSeqId = 0;
            int numCfgDomainCluster = 0;
            double domainMinSeqId = 0;
            string chainRelPfamArch = "";
            List<string> multiDomainChainGroupList = new List<string>();
            List<string> singleDomainChainGroupList = new List<string>();
            List<string> domainRelationList = new List<string>();
            string domainRelation = "";
            int superGroupId = 0;
            string chainClusterInfo = "";
            int sameRelNumOfCfgCluster = 0;
            double sameRelMinSeqId = 0;
            bool isBiolCluster = false;
            foreach (DataRow compRow in chainDomainClusterCompTable.Rows)
            {
                chainRelPfamArch = compRow["SuperGroup"].ToString();
                //     if (chainRelPfamArch == "(V-set)" || chainRelPfamArch == "(C1-set)" || chainRelPfamArch == "(V-set)_(C1-set)")
                if (chainRelPfamArch.IndexOf("(V-set)") > -1 || chainRelPfamArch.IndexOf("(C1-set)") > -1)
                {
                    continue;
                }
                numCfgChainCluster = Convert.ToInt32(compRow["NumCfgCluster"].ToString());
                chainMinSeqId = Convert.ToDouble(compRow["MinSeqID"].ToString());

                numCfgDomainCluster = Convert.ToInt32(compRow["D_NumCfgCluster"].ToString());
                domainMinSeqId = Convert.ToDouble(compRow["D_MinSeqID"].ToString());

                if ((numCfgChainCluster == -1 || numCfgChainCluster == 1 || (numCfgChainCluster >= 2 && chainMinSeqId >= 90)) &&
                    (numCfgDomainCluster >= 2 && domainMinSeqId < 90))
                {
                    domainRelation = compRow["Relation"].ToString();
                    superGroupId = GetSuperGroupSeqId(domainRelation);
                    chainClusterInfo = GetBiggestChainCluster(superGroupId, out isBiolCluster);
                    string[] chainClusterFields = chainClusterInfo.Split('\t');
                    sameRelNumOfCfgCluster = Convert.ToInt32(chainClusterFields[0]);
                    sameRelMinSeqId = Convert.ToDouble(chainClusterFields[5]);
                    if (sameRelNumOfCfgCluster >= 5 && sameRelMinSeqId < 90)
                    {
                        continue;
                    }
                    if (IsGroupMultiDomains(chainRelPfamArch))
                    {
                        if (!multiDomainChainGroupList.Contains(chainRelPfamArch))
                        {
                            multiDomainChainGroupList.Add(chainRelPfamArch);
                        }
                    }
                    else
                    {
                        if (!singleDomainChainGroupList.Contains(chainRelPfamArch))
                        {
                            singleDomainChainGroupList.Add(chainRelPfamArch);
                        }
                    }

                    if (!domainRelationList.Contains(domainRelation))
                    {
                        domainRelationList.Add(domainRelation);
                    }

                    dataWriter.WriteLine(ParseHelper.FormatDataRow(compRow) + "\t" + chainClusterInfo + "\t" + isBiolCluster.ToString());
                }
            }
            dataWriter.WriteLine("#MultiDomain Chain Groups: " + multiDomainChainGroupList.Count.ToString());
            dataWriter.WriteLine("#SingleDomain Chain Groups: " + singleDomainChainGroupList.Count.ToString());
            dataWriter.WriteLine("#DomainRelations: " + domainRelationList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void FormatChainDomainClusterCompInfo()
        {
            string clusterCompFile = Path.Combine(dataDir, "ClusterComp_NoChain_match.txt");
            string domainChainClusterCompFile = Path.Combine(dataDir, "ClusterComp_domainChain.txt");
            string headerLine = "";
            DataTable clusterCompTable = ReadChainDomainClusterCompTable(clusterCompFile, out headerLine);
            List<string> relationList = new List<string>();
            string relationNumCfg = "";
            foreach (DataRow clusterCompRow in clusterCompTable.Rows)
            {
                relationNumCfg = clusterCompRow["Relation"].ToString() + "&" + clusterCompRow["ClusterID"].ToString();
                if (relationNumCfg != "&")
                {
                    if (!relationList.Contains(relationNumCfg))
                    {
                        relationList.Add(relationNumCfg);
                    }
                }
            }
            string dataLine = "";
            string groupPfamArches = "";
            string relationPfamArches = "";
            int numOfChainPfamArches = 0;
            StreamWriter dataWriter = new StreamWriter(domainChainClusterCompFile);
            foreach (string relCluster in relationList)
            {
                string[] fields = relCluster.Split('&');

                DataRow[] clusterCompRows = clusterCompTable.Select(string.Format("Relation = '{0}' AND ClusterID = '{1}'", fields[0], fields[1]));
                groupPfamArches = "";
                foreach (DataRow clusterCompRow in clusterCompRows)
                {
                    groupPfamArches += clusterCompRow["SuperGroup"].ToString() + " || ";
                }
                groupPfamArches = groupPfamArches.TrimEnd(" || ".ToCharArray());
                relationPfamArches = GetDomainClusterChainPfamArches(fields[0], Convert.ToInt32(fields[1]), out numOfChainPfamArches);
                dataLine = clusterCompRows[0]["Relation"].ToString() + "\t" + clusterCompRows[0]["ClusterID"].ToString() + "\t" +
                        clusterCompRows[0]["D_NumCfgCluster"].ToString() + "\t" +
                        clusterCompRows[0]["NumCfgRelation"].ToString() + "\t" + clusterCompRows[0]["D_NumEntryCluster"].ToString() + "\t" +
                        clusterCompRows[0]["NumEntryRelation"].ToString() + "\t" + clusterCompRows[0]["D_SA"].ToString() + "\t" +
                        clusterCompRows[0]["D_MinSeqID"].ToString() + "\t" + clusterCompRows[0]["D_InAsu"].ToString() + "\t" +
                        clusterCompRows[0]["D_InPdb"].ToString() + "\t" + clusterCompRows[0]["D_InPisa"].ToString() + "\t" +
                        clusterCompRows[0]["NumOfEntryHomo"].ToString() + "\t" + clusterCompRows[0]["NumOfEntryHetero"].ToString() + "\t" +
                        clusterCompRows[0]["NumOfEntryIntra"].ToString() + "\t" + clusterCompRows.Length.ToString() + "\t" + groupPfamArches + "\t" +
                        numOfChainPfamArches.ToString() + "\t" + relationPfamArches;
                dataWriter.WriteLine(dataLine);

            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="groupPfamArches"></param>
        /// <returns></returns>
        private string GetDomainClusterChainPfamArches(string relationString, int clusterId, out int numOfChainPfamArches)
        {
            int relSeqId = GetRelSeqId(relationString);
            string queryString = string.Format("Select Distinct ChainPfamArch From PfamDomainClusterInterfaces WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable chainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            string chainPfamArches = "";
            string chainPfamArch = "";
            //      string[] domainClusterPfamArches = new string[chainPfamArchTable.Rows.Count];
            //      int count = 0;
            List<string> chainPfamArchList = new List<string>();
            foreach (DataRow pfamArchRow in chainPfamArchTable.Rows)
            {
                chainPfamArch = pfamArchRow["ChainPfamArch"].ToString().TrimEnd();
                if (!chainPfamArchList.Contains(chainPfamArch))
                {
                    chainPfamArchList.Add(chainPfamArch);
                    chainPfamArches += (chainPfamArch + " || ");
                }
            }
            numOfChainPfamArches = chainPfamArchList.Count;
            return chainPfamArches.TrimEnd(" || ".ToCharArray()); ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationString"></param>
        /// <returns></returns>
        private int GetRelSeqId(string relationString)
        {
            string[] pfamFields = relationString.Split(';');
            string queryString = "";
            if (pfamFields.Length == 1)
            {
                queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' AND FamilyCode2 = '{0}';", pfamFields[0]);
            }
            else
            {
                queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' AND FamilyCode2 = '{1}';", pfamFields[0], pfamFields[1]);
            }
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }
            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="groupPfamArches"></param>
        /// <returns></returns>
        private string GetDomainClusterChainPfamArches(int relSeqId, int clusterId, out int numOfChainPfamArches)
        {
            string queryString = string.Format("Select Distinct ChainPfamArch From PfamDomainClusterInterfaces WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable chainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            string chainPfamArches = "";
            //      string[] domainClusterPfamArches = new string[chainPfamArchTable.Rows.Count];
            //      int count = 0;
            numOfChainPfamArches = chainPfamArchTable.Rows.Count;
            foreach (DataRow pfamArchRow in chainPfamArchTable.Rows)
            {
                chainPfamArches += pfamArchRow["ChainPfamArch"].ToString().TrimEnd() + " || ";
            }
            return chainPfamArches.TrimEnd(" || ".ToCharArray()); ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationString"></param>
        /// <returns></returns>
        private int GetSuperGroupSeqId(string relationString)
        {
            string[] fields = relationString.Split(';');
            string chainPfamArch = "";
            if (fields[0] == fields[1])
            {
                chainPfamArch = "(" + fields[0] + ")";
            }
            else
            {
                chainPfamArch = "(" + fields[0] + ");(" + fields[1] + ")";
            }
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamArch = '{0}';", chainPfamArch);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int superGroupId = 0;
            if (superGroupIdTable.Rows.Count > -0)
            {
                superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            return superGroupId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainRelPfamArch"></param>
        /// <returns></returns>
        private bool IsGroupMultiDomains(string chainRelPfamArch)
        {
            string[] chainPfamArchFields = chainRelPfamArch.Split(';');
            foreach (string chainPfamArch in chainPfamArchFields)
            {
                if (chainPfamArch.IndexOf(")_") > -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomainClusterDompFile"></param>
        /// <returns></returns>
        private DataTable ReadChainDomainClusterCompTable(string chainDomainClusterCompFile, out string headerLine)
        {
            DataTable chainDomainClusterCompTable = new DataTable();
            StreamReader dataReader = new StreamReader(chainDomainClusterCompFile);
            headerLine = dataReader.ReadLine();
            string[] headerFields = headerLine.Split('\t');
            foreach (string hearderField in headerFields)
            {
                chainDomainClusterCompTable.Columns.Add(new DataColumn(hearderField));
            }
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                DataRow dataRow = chainDomainClusterCompTable.NewRow();
                if (fields.Length > headerFields.Length)
                {
                    string[] items = new string[headerFields.Length];
                    Array.Copy(fields, 0, items, 0, items.Length);
                    dataRow.ItemArray = items;
                }
                else
                {
                    dataRow.ItemArray = fields;
                }
                chainDomainClusterCompTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            return chainDomainClusterCompTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string GetBiggestChainCluster(int groupId, out bool isBiolChainCluster)
        {
            string queryString = string.Format("Select * From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0} Order By NumOfCfgCluster DESC;", groupId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string maxClusterInfo = "";
            isBiolChainCluster = false;
            if (clusterSumInfoTable.Rows.Count > 0)
            {
                maxClusterInfo = clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString() + "\t" + clusterSumInfoTable.Rows[0]["NumOfCfgFamily"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["NumOfEntryCluster"].ToString() + "\t" + clusterSumInfoTable.Rows[0]["NumOfEntryFamily"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["SurfaceArea"].ToString() + "\t" + clusterSumInfoTable.Rows[0]["MinSeqIdentity"].ToString();

                if (Convert.ToInt32(clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString()) >= 5 &&
                    Convert.ToDouble(clusterSumInfoTable.Rows[0]["MinSeqIdentity"].ToString()) < 90)
                {
                    isBiolChainCluster = true;
                }

            }
            else
            {
                int[] numOfGroupInfos = GetGroupInfo(groupId);
                maxClusterInfo = "-1\t" + numOfGroupInfos[1].ToString() + "\t-1\t" + numOfGroupInfos[0].ToString() + "\t-1\t-1";
            }
            return maxClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupInfo(int superGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID From PfamSuperGroups WHere SuperGroupSeqID = {0};", superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupSeqId = 0;
            int numOfEntries = 0;
            int groupNumOfEntries = 0;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupSeqId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                groupNumOfEntries = GetGroupEntry(groupSeqId);
                numOfEntries += groupNumOfEntries;
            }
            queryString = string.Format("Select Distinct SuperCfGroupID From PfamSuperCfGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int numOfCfgs = cfGroupTable.Rows.Count;
            int[] numOfGroupInfos = new int[2];
            numOfGroupInfos[0] = numOfEntries;
            numOfGroupInfos[1] = numOfCfgs;
            return numOfGroupInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int GetGroupEntry(int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable groupEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            int numOfEntry = groupEntryTable.Rows.Count;
            queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign WHere GroupSeqID = {0};", groupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            numOfEntry += homoEntryTable.Rows.Count;
            return numOfEntry;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetBiggestRelatedDomainCluster(int relSeqId, string groupPfamArch)
        {
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster DESC;", relSeqId);
            DataTable domainClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            int clusterId = 0;
            string maxClusterInfo = "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1";
            foreach (DataRow clusterRow in domainClusterInfoTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                if (DoesDClusterContainChainPfamArch(relSeqId, clusterId, groupPfamArch))
                {
                    maxClusterInfo = clusterId.ToString() + "\t" + clusterRow["NumOfCfgCluster"].ToString() + "\t" + clusterRow["NumOfCfgRelation"].ToString() + "\t" +
                   clusterRow["NumOfEntryCluster"].ToString() + "\t" + clusterRow["NumOfEntryRelation"].ToString() + "\t" +
                   clusterRow["SurfaceArea"].ToString() + "\t" + clusterRow["MinSeqIdentity"].ToString() + "\t" +
                   clusterRow["InAsu"].ToString() + "\t" + clusterRow["InPDB"].ToString() + "\t" +
                   clusterRow["InPISA"].ToString() + "\t" + clusterRow["NumOfEntryHomo"].ToString() + "\t" +
                   clusterRow["NumOfEntryHetero"].ToString() + "\t" + clusterRow["NumOfEntryIntra"].ToString();
                    break;
                }
            }

            return maxClusterInfo;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetBiggestDomainCluster(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster DESC;", relSeqId);
            DataTable domainClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string maxClusterInfo = "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1";
            if (domainClusterInfoTable.Rows.Count > 0)
            {
                DataRow clusterRow = domainClusterInfoTable.Rows[0];
               maxClusterInfo = clusterRow["ClusterID"].ToString() + "\t" + clusterRow["NumOfCfgCluster"].ToString() + "\t" + clusterRow["NumOfCfgRelation"].ToString() + "\t" +
               clusterRow["NumOfEntryCluster"].ToString() + "\t" + clusterRow["NumOfEntryRelation"].ToString() + "\t" +
               clusterRow["SurfaceArea"].ToString() + "\t" + clusterRow["MinSeqIdentity"].ToString() + "\t" +
               clusterRow["InAsu"].ToString() + "\t" + clusterRow["InPDB"].ToString() + "\t" +
               clusterRow["InPISA"].ToString() + "\t" + clusterRow["NumOfEntryHomo"].ToString() + "\t" +
               clusterRow["NumOfEntryHetero"].ToString() + "\t" + clusterRow["NumOfEntryIntra"].ToString();
            }

            return maxClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetBioDomainClusters (int relSeqId, int mCut, double minSeqIdCut)
        {
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = {0} and NumOfCfgCluster >= {1} and MinSeqIdentity < {2} Order By NumOfCfgCluster DESC;", relSeqId, mCut, minSeqIdCut);
            DataTable domainClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string maxClusterInfo = "";
            if (domainClusterInfoTable.Rows.Count == 0)
            {
                return "-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1\t-1";
            }
            foreach (DataRow clusterRow in domainClusterInfoTable.Rows)
            {
                maxClusterInfo += (clusterRow["ClusterID"].ToString() + "\t" + clusterRow["NumOfCfgCluster"].ToString() + "\t" + clusterRow["NumOfCfgRelation"].ToString() + "\t" +
                clusterRow["NumOfEntryCluster"].ToString() + "\t" + clusterRow["NumOfEntryRelation"].ToString() + "\t" +
                clusterRow["SurfaceArea"].ToString() + "\t" + clusterRow["MinSeqIdentity"].ToString() + "\t" +
                clusterRow["InAsu"].ToString() + "\t" + clusterRow["InPDB"].ToString() + "\t" +
                clusterRow["InPISA"].ToString() + "\t" + clusterRow["NumOfEntryHomo"].ToString() + "\t" +
                clusterRow["NumOfEntryHetero"].ToString() + "\t" + clusterRow["NumOfEntryIntra"].ToString() + "\n");
            }

            return maxClusterInfo.TrimEnd('\n');
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        private bool DoesDClusterContainChainPfamArch(int relSeqId, int clusterId, string chainPfamArch)
        {
            string queryString = string.Format("Select Distinct ChainPfamArch From PfamDomainClusterInterfaces Where RelSeqId = {0} AND ClusterID = {1};",
                relSeqId, clusterId);
            DataTable clusterChainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            string rowChainPfamArch = "";
            string[] chainPfamArchFields = chainPfamArch.Split(';');
            foreach (DataRow chainPfamArchRow in clusterChainPfamArchTable.Rows)
            {
                rowChainPfamArch = chainPfamArchRow["ChainPfamArch"].ToString().TrimEnd();
                string[] pfamArchFields = rowChainPfamArch.Split(';');
                if (ArePfamArchSame(pfamArchFields, chainPfamArchFields))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArchFields1"></param>
        /// <param name="pfamArchFields2"></param>
        /// <returns></returns>
        private bool ArePfamArchSame(string[] pfamArchFields1, string[] pfamArchFields2)
        {
            if (pfamArchFields1.Length != pfamArchFields2.Length)
            {
                return false;
            }

            for (int i = 0; i < pfamArchFields1.Length; i++)
            {
                if (pfamArchFields1[i] == pfamArchFields2[i])
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainRelPfamArch"></param>
        /// <returns></returns>
        private string[] GetPfamsFromGroupPfamArch(string chainRelPfamArch)
        {
            string[] fields = chainRelPfamArch.Split(';');
            List<string> pfamIdList = new List<string>();
            string pfamId = "";
            foreach (string field in fields)
            {
                string[] pfamFields = field.Split(')');
                foreach (string pfamIdField in pfamFields)
                {
                    pfamId = pfamIdField.Trim("(_".ToCharArray());
                    if (pfamId == "")
                    {
                        continue;
                    }
                    if (!pfamIdList.Contains(pfamId))
                    {
                        pfamIdList.Add(pfamId);
                    }
                }
            }
            string[] pfamIds = new string[pfamIdList.Count];
            pfamIdList.CopyTo(pfamIds);
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainRelPfamArch"></param>
        /// <returns></returns>
        private string[][] GetPfamPairsFromGroupPfamArch(string chainRelPfamArch)
        {
            string[] fields = chainRelPfamArch.Split(';');
            List<List<string>> pfamPairList = new List<List<string>>();

            string pfamId = "";
            foreach (string field in fields)
            {
                List<string> pfamIdList = new List<string>();
                string[] pfamFields = field.Split(')');
                foreach (string pfamIdField in pfamFields)
                {
                    pfamId = pfamIdField.Trim("(_".ToCharArray());
                    if (pfamId == "")
                    {
                        continue;
                    }
                    if (!pfamIdList.Contains(pfamId))
                    {
                        pfamIdList.Add(pfamId);
                    }
                }
                pfamPairList.Add(pfamIdList);
            }
            string[][] pfamPairs = new string[pfamPairList.Count][];
            int count = 0;
            foreach (List<string> pfamIdList in pfamPairList)
            {
                pfamPairs[count] = pfamIdList.ToArray ();
                count++;
            }
            return pfamPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private int[] GetRelSeqIds(string[] pfamIds)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation " +
                " WHere FamilyCode1 IN ({0}) AND FamilyCode2 IN ({0});", ParseHelper.FormatSqlListString(pfamIds));
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds1"></param>
        /// <param name="pfamIds2"></param>
        /// <returns></returns>
        private int[] GetRelSeqIds(string[] pfamIds1, string[] pfamIds2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDOmainFamilyRelation " +
                    " WHere (FamilyCode1 IN ({0}) AND FamilyCode2 IN ({1})) OR " +
                    " (FamilyCode1 IN ({1}) AND FamilyCode2 IN ({0}));",
                    ParseHelper.FormatSqlListString(pfamIds1), ParseHelper.FormatSqlListString(pfamIds2));
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamPairs"></param>
        /// <returns></returns>
        private int[] GetRelSeqIds(string[][] pfamPairs)
        {
            int[] relSeqIds = null;
            if (pfamPairs.Length == 1)
            {
                relSeqIds = GetRelSeqIds(pfamPairs[0]);
            }
            else if (pfamPairs.Length == 2)
            {
                relSeqIds = GetRelSeqIds(pfamPairs[0], pfamPairs[1]);
            }
            return relSeqIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        public string GetChainClusterInfo(int superGroupId)
        {
            string queryString = string.Format("Select ClusterID, SurfaceArea, InPDB, InPisa, InAsu, NumOfCfgCluster, NumOfEntryCluster, MinSeqIdentity " +
                " NumOfCfgFamily, NumOfEntryFamily From PfamSuperClusterSumInfo Where SuperGroupSeqId = {0};", superGroupId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string chainClusterInfo = "";
            foreach (DataRow dataRow in clusterInfoTable.Rows)
            {
                chainClusterInfo += (ParseHelper.FormatDataRow(dataRow) + "\r\n");
            }
            return chainClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string GetRelationClusterInfo(int relSeqId, string pfamId)
        {
            string querySTring = string.Format("Select ClusterID, SurfaceArea, InPDB, InPisa, InASu, NumOfCfgCluster, NumOfEntryCluster, MinSeqIdentity From PfamDOmainClusterSumInfo " +
                " Where RelSeqID = {0};", relSeqId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(querySTring);
            string clusterInfoString = "";
            foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
            {
                clusterInfoString += (pfamId + "\t" + ParseHelper.FormatDataRow(clusterInfoRow) + "\r\n");
            }
            return clusterInfoString;
        }
        #endregion

        #region hormone_recep
        private DomainSeqFasta clusterSeq = new DomainSeqFasta();
        public void PrintDomainInterfaceClusterSumInfo()
        {
            int relSeqId = 10348;
            int clusterId = 1;

            string queryString = string.Format("Select UnpCode, Species From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable unpSpeciesTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> unpList = new List<string>();
            List<string> speciesList = new List<string>();

            foreach (DataRow dataRow in unpSpeciesTable.Rows)
            {
                string[] unpFields = dataRow["UnpCode"].ToString().TrimEnd().ToUpper().Split(';');
                foreach (string unpField in unpFields)
                {
                    if (unpField == "" || unpField == "-")
                    {
                        continue;
                    }
                    if (!unpList.Contains(unpField))
                    {
                        unpList.Add(unpField);
                    }
                }
                string[] speciesFields = dataRow["Species"].ToString().TrimEnd().ToUpper().Split(';');
                foreach (string speciesField in speciesFields)
                {
                    if (speciesField == "" || speciesField == "-")
                    {
                        continue;
                    }
                    if (!speciesList.Contains(speciesField))
                    {
                        speciesList.Add(speciesField);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintUnpDomainClusterSequences()
        {
            int relSeqId = 10348;
            int clusterId = 1;

            DataTable relDomainInterfaceTable = clusterSeq.GetRelationDomainInterfaces(relSeqId);
            DataTable clusterDomainInterfaceTable = GetClusterDomainInterfacesWithUnp(relSeqId, clusterId, relDomainInterfaceTable);

            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable familyCodeTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamCode1 = familyCodeTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            string pfamCode2 = familyCodeTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            List<string> clusterDomainListA = new List<string>();
            List<string> clusterDomainListB = null;
            List<string> addedUnpCodeListA = new List<string>();
            List<string> addedUnpCodeListB = null;
            if (pfamCode1 != pfamCode2)
            {
                clusterDomainListB = new List<string>();
                addedUnpCodeListB = new List<string>();
            }
            string entryDomainA = "";
            string entryDomainB = "";
            string pdbId = "";

            string interfaceUnpCode = "";
            string unpCodeA = "";
            string unpCodeB = "";
            List<string> entryList = new List<string>();
            foreach (DataRow domainInterfaceRow in clusterDomainInterfaceTable.Rows)
            {
                interfaceUnpCode = domainInterfaceRow["UnpCode"].ToString().TrimEnd();
                string[] unpCodes = interfaceUnpCode.Split(';');
                if (unpCodes.Length == 1)
                {
                    unpCodeA = unpCodes[0];
                    unpCodeB = unpCodeA;
                }
                else if (unpCodes.Length == 2)
                {
                    unpCodeA = unpCodes[0];
                    unpCodeB = unpCodes[1];
                }

                pdbId = domainInterfaceRow["PdbID"].ToString();
                entryDomainA = pdbId + domainInterfaceRow["DomainID1"].ToString();
                entryDomainB = pdbId + domainInterfaceRow["DomainID2"].ToString();
                if (domainInterfaceRow["IsReversed"].ToString() == "1")
                {
                    string temp = entryDomainA;
                    entryDomainA = entryDomainB;
                    entryDomainB = temp;

                    temp = unpCodeA;
                    unpCodeA = unpCodeB;
                    unpCodeB = temp;
                }
                if (!addedUnpCodeListA.Contains(unpCodeA))
                {
                    if (!clusterDomainListA.Contains(entryDomainA))
                    {
                        clusterDomainListA.Add(entryDomainA);
                    }

                    addedUnpCodeListA.Add(unpCodeA);
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }

                if (clusterDomainListB == null)
                {
                    if (!addedUnpCodeListA.Contains(unpCodeB))
                    {
                        if (!clusterDomainListA.Contains(entryDomainB))
                        {
                            clusterDomainListA.Add(entryDomainB);
                        }

                        addedUnpCodeListA.Add(unpCodeB);
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                        }
                    }
                }
                else
                {
                    if (!addedUnpCodeListB.Contains(unpCodeB))
                    {
                        if (!clusterDomainListB.Contains(entryDomainB))
                        {
                            clusterDomainListB.Add(entryDomainB);
                        }

                        addedUnpCodeListB.Add(unpCodeB);
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                        }
                    }
                }
            }

            string[] pdbIds = new string[entryList.Count];
            entryList.CopyTo(pdbIds);
            DataTable entitySeqTable = GetEntitySeqTable(pdbIds);
            DataTable domainTable = GetDomainTable(pdbIds);

            string[] clusterDomainsA = new string[clusterDomainListA.Count];
            clusterDomainListA.CopyTo(clusterDomainsA);
            string seqFileName = "Cluster" + relSeqId.ToString() + "A_" + clusterId.ToString() + ".fasta";
            string seqFile = Path.Combine(dataDir, seqFileName);
            clusterSeq.WriteDomainSequenceToFile(clusterDomainsA, seqFile, entitySeqTable, domainTable);

            if (clusterDomainListB != null && clusterDomainListB.Count > 0)
            {
                string[] clusterDomainsB = new string[clusterDomainListB.Count];
                clusterDomainListB.CopyTo(clusterDomainsB);
                seqFileName = "Cluster" + relSeqId.ToString() + "B_" + clusterId.ToString() + ".fasta";
                seqFile = Path.Combine(dataDir, seqFileName);
                clusterSeq.WriteDomainSequenceToFile(clusterDomainsB, seqFile, entitySeqTable, domainTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public DataTable GetClusterDomainInterfacesWithUnp(int relSeqId, int clusterId, DataTable relDomainInterfaceTable)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID, UnpCode From PfamDomainClusterInterfaces WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            DataTable domainInterfaceTable = relDomainInterfaceTable.Clone();
            domainInterfaceTable.Columns.Add(new DataColumn("UnpCode"));
            foreach (DataRow interfaceRow in domainInterfaceIdTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                DataRow[] domainInterfaceRows = relDomainInterfaceTable.Select(
                    string.Format("RelSeqID = '{0}' AND PdbID = '{1}' AND DomainInterfaceID = '{2}'", relSeqId, pdbId, domainInterfaceId));

                foreach (DataRow domainInterfaceRow in domainInterfaceRows)
                {
                    DataRow dataRow = domainInterfaceTable.NewRow();
                    object[] newItemArray = new object[domainInterfaceRow.ItemArray.Length + 1];
                    Array.Copy(domainInterfaceRow.ItemArray, newItemArray, domainInterfaceRow.ItemArray.Length);
                    newItemArray[newItemArray.Length - 1] = interfaceRow["UnpCode"];
                    dataRow.ItemArray = newItemArray;
                    domainInterfaceTable.Rows.Add(dataRow);
                }
            }
            return domainInterfaceTable;
        }       

        /// <summary>
        /// 
        /// </summary>
        public void PrintOnePfamUnpSequences()
        {
            dataDir = Path.Combine(dataDir, "domainClusterInfo\\Hormone_recep");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            DataTable hormoneRecepTable = ReadHormoneRecepSubFamilyInfo(Path.Combine(dataDir, "subfamilies_pfamweb.txt"));

            string pfamId = "Hormone_recep";
            int relSeqId = 10348;
            int clusterId = 1;

            string[] clusterEntries = GetClusterEntries(relSeqId, clusterId);
            DataTable entityAsymMapTable = GetEntityAsymMapTable(clusterEntries);
            string[] clusterEntities = GetClusterEntities(relSeqId, clusterId, entityAsymMapTable);

            string queryString = string.Format("Select PdbID, EntityID From PdbPfam WHere Pfam_ID = '{0}';", pfamId);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            int entityId = 0;
            StreamWriter unpDataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_unpCodes.txt"));
            StreamWriter seqDataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_seq.fasta"));
            StreamWriter domainSeqDataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_domainSeq.fasta"));
            StreamWriter chainPfamArchDataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_chainPfamArch.txt"));
            StreamWriter clusterDomainSeqDataWriter = new StreamWriter(Path.Combine(dataDir, "Seq" + relSeqId.ToString() + "_" + clusterId.ToString() + ".fasta"));
            StreamWriter clusterUnpDataWriter = new StreamWriter(Path.Combine(dataDir, "Unp" + relSeqId.ToString() + "_" + clusterId.ToString() + ".txt"));
            StreamWriter clusterChainPfamARchWriter = new StreamWriter(Path.Combine(dataDir, relSeqId.ToString() + "_" + clusterId.ToString() + "ChainPfamArch.txt"));
            string unpCode = "";
            string sequence = "";
            string domainSequence = "";
            string gene = "";
            string clusterPdbId = "";
            Dictionary<string, List<string>> unpCodeEntryHash = new Dictionary<string, List<string>>();
            foreach (string clusterEntity in clusterEntities)
            {
                clusterPdbId = clusterEntity.Substring(0, 4);
                entityId = Convert.ToInt32(clusterEntity.Substring(4, clusterEntity.Length - 4));

                unpCode = GetEntityUnpCode(clusterPdbId, entityId);

                if (unpCode == "" || unpCode == "-")
                {
                    unpCode = clusterPdbId;
                }

                if (unpCodeEntryHash.ContainsKey(unpCode))
                {
                    if (!unpCodeEntryHash[unpCode].Contains(clusterPdbId))
                    {
                        unpCodeEntryHash[unpCode].Add(clusterPdbId);
                    }
                }
                else
                {
                    domainSequence = GetDomainSequence(clusterPdbId, entityId, pfamId);
                    if (domainSequence == "")
                    {
                        continue;
                    }
                    List<string> entryList = new List<string>();
                    entryList.Add(clusterPdbId);
                    unpCodeEntryHash.Add(unpCode, entryList);

                    sequence = GetEntitySequence(clusterPdbId, entityId);
                    seqDataWriter.WriteLine(">" + clusterPdbId + entityId.ToString());
                    seqDataWriter.WriteLine(sequence);


                    domainSeqDataWriter.WriteLine(">" + clusterPdbId + entityId.ToString());
                    domainSeqDataWriter.WriteLine(domainSequence);

                    clusterDomainSeqDataWriter.WriteLine(">" + clusterPdbId + entityId.ToString());
                    clusterDomainSeqDataWriter.WriteLine(domainSequence);

                    string[] chainPfamArches = GetChainPfamArches(clusterPdbId, entityId);
                    clusterChainPfamARchWriter.WriteLine(clusterEntity + "\t" + chainPfamArches[0] + "\t" + chainPfamArches[1] + "\t" + chainPfamArches[2]);

                    chainPfamArchDataWriter.WriteLine(clusterEntity + "\t" + chainPfamArches[0] + "\t" + chainPfamArches[1] + "\t" + chainPfamArches[2]);
                }
            }

            foreach (string keyUnpCode in unpCodeEntryHash.Keys)
            {
                gene = GetUnpSeqGeneName(keyUnpCode);
                string[] unpEntries = unpCodeEntryHash[keyUnpCode].ToArray();
                if (gene == "")
                {
                    gene = "unkown";
                }
                DataRow[] hormoneRecepInfoRows = hormoneRecepTable.Select(string.Format("Gene = '{0}'", gene));
                if (hormoneRecepInfoRows.Length > 0)
                {
                    clusterUnpDataWriter.WriteLine(keyUnpCode + "\t" + gene + "\t" + unpCodeEntryHash[keyUnpCode].Count.ToString()
                        + "\t" + ParseHelper.FormatStringFieldsToString(unpEntries) + "\t" + ParseHelper.FormatDataRow(hormoneRecepInfoRows[0]));
                }
                else
                {
                    clusterUnpDataWriter.WriteLine(keyUnpCode + "\t" + gene + "\t" + unpCodeEntryHash[keyUnpCode].Count.ToString() + "\t" + ParseHelper.FormatStringFieldsToString(unpEntries));
                }
            }
            clusterDomainSeqDataWriter.Close();
            clusterUnpDataWriter.Close();
            clusterChainPfamARchWriter.Close();

            foreach (DataRow entityRow in entityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                if (clusterEntities.Contains(pdbId + entityId.ToString()))
                {
                    continue;
                }
                unpCode = GetEntityUnpCode(pdbId, entityId);
                if (unpCode == "" || unpCode == "-")
                {
                    unpCode = pdbId;
                }
                if (unpCodeEntryHash.ContainsKey(unpCode))
                {
                    if (!unpCodeEntryHash[unpCode].Contains(pdbId))
                    {
                        unpCodeEntryHash[unpCode].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    unpCodeEntryHash.Add(unpCode, entryList);

                    sequence = GetEntitySequence(pdbId, entityId);
                    seqDataWriter.WriteLine(">" + pdbId + entityId.ToString());
                    seqDataWriter.WriteLine(sequence);

                    domainSequence = GetDomainSequence(pdbId, entityId, pfamId);
                    domainSeqDataWriter.WriteLine(">" + pdbId + entityId.ToString());
                    domainSeqDataWriter.WriteLine(domainSequence);

                    string[] chainPfamArches = GetChainPfamArches(pdbId, entityId);
                    chainPfamArchDataWriter.WriteLine(pdbId + entityId.ToString() + "\t" + chainPfamArches[0] + "\t" + chainPfamArches[1] + "\t" + chainPfamArches[2]);
                }
            }
            foreach (string keyUnpCode in unpCodeEntryHash.Keys)
            {
                gene = GetUnpSeqGeneName(keyUnpCode);
                string[] unpEntries = unpCodeEntryHash[keyUnpCode].ToArray();
                if (gene == "")
                {
                    gene = "unkown";
                }
                DataRow[] hormoneRecepInfoRows = hormoneRecepTable.Select(string.Format("Gene = '{0}'", gene));
                if (hormoneRecepInfoRows.Length > 0)
                {
                    unpDataWriter.WriteLine(keyUnpCode + "\t" + gene + "\t" + unpCodeEntryHash[keyUnpCode].Count.ToString() + "\t"
                        + ParseHelper.FormatStringFieldsToString(unpEntries) + "\t" + ParseHelper.FormatDataRow(hormoneRecepInfoRows[0]));
                }
                else
                {
                    unpDataWriter.WriteLine(keyUnpCode + "\t" + gene + "\t" + unpCodeEntryHash[keyUnpCode].Count.ToString() + "\t" + ParseHelper.FormatStringFieldsToString(unpEntries));
                }
            }

            unpDataWriter.Close();
            seqDataWriter.Close();
            domainSeqDataWriter.Close();
            chainPfamArchDataWriter.Close();

            AddEntryBuCompNoCluster(relSeqId, clusterId);
        }       

        /// <summary>
        /// 
        /// </summary>
        public void AddEntryBuCompNoCluster(int relSeqId, int clusterId)
        {
            //    dataDir = Path.Combine(dataDir, "domainClusterInfo\\Hormone_recep");
            string clusterDataFile = Path.Combine(dataDir, "Unp" + relSeqId.ToString() + "_1.txt");
            string updateDataFile = Path.Combine(dataDir, "Unp" + relSeqId.ToString() + "_1_new.txt");
            StreamWriter dataWriter = new StreamWriter(updateDataFile);

            string groupDataFile = Path.Combine(dataDir, "Hormone_recep_unpCodes.txt");
            DataTable hormoneRecepClusterTable = ReadHormoneRecepTextFile(clusterDataFile);
            DataTable hormoneRecepGroupTable = ReadHormoneRecepTextFile(groupDataFile);

            string newHeaderLine = FormatHeaderLine(hormoneRecepClusterTable);
            newHeaderLine += "\tInPdb\tInPisa\tNotInCluster\tEntriesNotInCluster";
            dataWriter.WriteLine(newHeaderLine);

            string[] seqEntries = null;
            int numOfPdb = 0;
            int numOfPisa = 0;
            string unpCode = "";
            foreach (DataRow unpRow in hormoneRecepClusterTable.Rows)
            {
                seqEntries = unpRow["Entries"].ToString().Split(',');
                unpCode = unpRow["UnpCode"].ToString();
                numOfPdb = GetNumOfInBA(relSeqId, clusterId, seqEntries, "pdb");
                numOfPisa = GetNumOfInBA(relSeqId, clusterId, seqEntries, "pisa");
                string[] entriesNotInCluster = GetEntriesNotInCluster(seqEntries, unpCode, hormoneRecepGroupTable);
                dataWriter.WriteLine(ParseHelper.FormatDataRow(unpRow) + "\t" +
                    numOfPdb.ToString() + "\t" + numOfPisa.ToString() + "\t" + entriesNotInCluster.Length.ToString() + "\t" +
                    ParseHelper.FormatStringFieldsToString(entriesNotInCluster));
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        private string FormatHeaderLine(DataTable dataTable)
        {
            string headerLine = "";
            foreach (DataColumn col in dataTable.Columns)
            {
                headerLine += (col.ColumnName + "\t");
            }
            return headerLine.TrimEnd('\t');
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entriesInCluster"></param>
        /// <param name="unpCode"></param>
        /// <param name="groupTable"></param>
        /// <returns></returns>
        private string[] GetEntriesNotInCluster(string[] entriesInCluster, string unpCode, DataTable groupTable)
        {
            DataRow[] unpCodeRows = groupTable.Select(string.Format("UnpCode = '{0}'", unpCode));
            string[] entriesNotInClusters = null;
            if (unpCodeRows.Length > 0)
            {
                string[] unpEntries = unpCodeRows[0]["Entries"].ToString().Split(',');
                List<string> entryNotInClusterList = new List<string>();
                foreach (string pdbId in unpEntries)
                {
                    if (!entriesInCluster.Contains(pdbId))
                    {
                        entryNotInClusterList.Add(pdbId);
                    }
                }

                entriesNotInClusters = new string[entryNotInClusterList.Count];
                entryNotInClusterList.CopyTo(entriesNotInClusters);
            }
            else
            {
                entriesNotInClusters = new string[0];
            }
            return entriesNotInClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="pdbIds"></param>
        /// <param name="pdbOrPisa"></param>
        /// <returns></returns>
        private int GetNumOfInBA(int relSeqId, int clusterId, string[] pdbIds, string pdbOrPisa)
        {
            List<string> inBaList = new List<string>();
            foreach (string pdbId in pdbIds)
            {
                if (IsEntryInBA(relSeqId, clusterId, pdbId, pdbOrPisa))
                {
                    inBaList.Add(pdbId);
                }
            }
            return inBaList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="pdbId"></param>
        /// <param name="pdbOrPisa"></param>
        /// <returns></returns>
        private bool IsEntryInBA(int relSeqId, int clusterId, string pdbId, string pdbOrPisa)
        {
            string queryString = "";
            if (pdbOrPisa == "pdb")
            {
                queryString = string.Format("Select InPdb As InBA From PfamDomainClusterInterfaces Where RelSeqId = {0} AND ClusterID = {1} AND PdbID = '{2}' AND InPDB = '1';",
                    relSeqId, clusterId, pdbId);
            }
            else if (pdbOrPisa == "pisa")
            {
                queryString = string.Format("Select InPISA As InBA From PfamDomainClusterInterfaces Where RelSeqId = {0} AND ClusterID = {1} AND PdbID = '{2}' AND InPISA = '1';",
                    relSeqId, clusterId, pdbId);
            }
            DataTable inBaTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (inBaTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textFile"></param>
        /// <returns></returns>
        private DataTable ReadHormoneRecepTextFile(string textFile)
        {
            StreamReader dataReader = new StreamReader(textFile);
            //  string headerLine = dataReader.ReadLine();
            //  string[] tableColumns = headerLine.Split('\t');
            string[] tableColumns = { "UnpCode", "UnpGene", "NumEntry", "Entries", "SubFamilyNo", "SubFamily", "SubFamilyType", 
                                        "Group", "NRNC", "Abbreviation", "Name", "Gene", "Ligands"};

            DataTable hormoneRecepTable = new DataTable("HormoneRecepTable");
            foreach (string col in tableColumns)
            {
                hormoneRecepTable.Columns.Add(new DataColumn(col));
            }

            string line = "";
            int fieldCount = 0;
            int colCount = tableColumns.Length;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                List<string> itemList = new List<string>(fields);
                fieldCount = fields.Length;
                while (fieldCount < colCount)
                {
                    itemList.Add("-");
                    fieldCount++;
                }
                DataRow dataRow = hormoneRecepTable.NewRow();
                string[] items = new string[itemList.Count];
                itemList.CopyTo(items);
                dataRow.ItemArray = items;
                hormoneRecepTable.Rows.Add(items);
            }
            dataReader.Close();
            return hormoneRecepTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetChainPfamArches(string pdbId, int entityId)
        {
            string queryString = string.Format("Select PfamArch, PfamArchE3, PfamArchE5 From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamArchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] pfamArches = new string[3];
            if (pfamArchTable.Rows.Count > 0)
            {
                pfamArches[0] = pfamArchTable.Rows[0]["PfamArch"].ToString().TrimEnd();
                pfamArches[1] = pfamArchTable.Rows[0]["PfamArchE3"].ToString().TrimEnd();
                pfamArches[2] = pfamArchTable.Rows[0]["PfamArchE5"].ToString().TrimEnd();
            }
            return pfamArches;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="subFamilyFile"></param>
        /// <returns></returns>
        private DataTable ReadHormoneRecepSubFamilyInfo(string subFamilyFile)
        {
            StreamReader dataReader = new StreamReader(subFamilyFile);
            string line = "";
            string headerLine = dataReader.ReadLine();
            string[] headerCols = headerLine.Split('\t');
            DataTable hormoneRecepTable = new DataTable("HormoneRecepSubfamilies");
            foreach (string header in headerCols)
            {
                hormoneRecepTable.Columns.Add(new DataColumn(header.Trim()));
            }
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                DataRow dataRow = hormoneRecepTable.NewRow();
                dataRow.ItemArray = fields;
                hormoneRecepTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            return hormoneRecepTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string GetUnpSeqGeneName(string unpCode)
        {
            string queryString = string.Format("Select PrimaryGene From UnpNameInfo Where UnpCode = '{0}';", unpCode);
            DataTable geneTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string gene = "";
            if (geneTable.Rows.Count > 0)
            {
                gene = geneTable.Rows[0]["PrimaryGene"].ToString().TrimEnd();
            }
            return gene;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string[] GetClusterEntries(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces WHere RelSeqID = {0} AND ClusterID = {1} Order By PdbID;", relSeqId, clusterId);
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

            string[] clusterEntries = new string[clusterEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                clusterEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return clusterEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="entityAsymMapTable"></param>
        /// <returns></returns>
        private string[] GetClusterEntities(int relSeqId, int clusterId, DataTable entityAsymMapTable)
        {
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceId From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> clusterEntityList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in clusterDomainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                int[] entityIds = GetDomainInterfaceEntities(pdbId, domainInterfaceId, entityAsymMapTable);
                foreach (int entityId in entityIds)
                {
                    if (!clusterEntityList.Contains(pdbId + entityId.ToString()))
                    {
                        clusterEntityList.Add(pdbId + entityId.ToString());
                    }
                }
            }
            string[] clusterEntities = new string[clusterEntityList.Count];
            clusterEntityList.CopyTo(clusterEntities);
            return clusterEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="entityAsymMapTable"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceEntities(string pdbId, int domainInterfaceId, DataTable entityAsymMapTable)
        {
            int[] domainInterfaceEntities = null;
            string queryString = string.Format("Select AsymChain1, AsymChain2 From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable asymChainsTable = ProtCidSettings.protcidQuery.Query(queryString);
            string asymChain1 = asymChainsTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
            string asymChain2 = asymChainsTable.Rows[0]["AsymChain2"].ToString().TrimEnd();
            int entityId1 = GetAsymChainEntityId(pdbId, asymChain1, entityAsymMapTable);
            if (asymChain1 != asymChain2)
            {
                int entityId2 = GetAsymChainEntityId(pdbId, asymChain2, entityAsymMapTable);
                domainInterfaceEntities = new int[2];
                domainInterfaceEntities[0] = entityId1;
                domainInterfaceEntities[1] = entityId2;
            }
            else
            {
                domainInterfaceEntities = new int[1];
                domainInterfaceEntities[0] = entityId1;
            }
            return domainInterfaceEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="entityAsymMapTable"></param>
        /// <returns></returns>
        private int GetAsymChainEntityId(string pdbId, string asymChain, DataTable entityAsymMapTable)
        {
            DataRow[] entityAsymChainRows = entityAsymMapTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            if (entityAsymChainRows.Length > 0)
            {
                return Convert.ToInt32(entityAsymChainRows[0]["EntityID"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private DataTable GetEntityAsymMapTable(string[] pdbIds)
        {
            DataTable entityAsymMapTable = null;
            string queryString = "";
            DataTable entryEntityAsymMapTable = null;
            foreach (string pdbId in pdbIds)
            {
                queryString = string.Format("Select PdbID, EntityID, AsymID From AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                entryEntityAsymMapTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(entryEntityAsymMapTable, ref entityAsymMapTable);
            }
            return entityAsymMapTable;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetDomainSequence(string pdbId, int entityId, string pfamId)
        {
            string queryString = string.Format("Select SeqStart, SeqEnd From PdbPfam " +
                " Where PdbID = '{0}' AND EntityID = {1} AND Pfam_ID = '{2}' Order By HmmStart;", pdbId, entityId, pfamId);
            DataTable rangeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<Range> rangeList = new List<Range>();
            foreach (DataRow rangeRow in rangeTable.Rows)
            {
                Range range = new Range();
                range.startPos = Convert.ToInt32(rangeRow["SeqStart"].ToString());
                range.endPos = Convert.ToInt32(rangeRow["SeqEnd"].ToString());
                rangeList.Add(range);
            }
            Range[] domainRanges = new Range[rangeList.Count];
            rangeList.CopyTo(domainRanges);

            string domainSequence = "";
            try
            {
                domainSequence = GetDomainSequence(pdbId, entityId, domainRanges);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + entityId.ToString() + ": " + ex.Message);
            }
            return domainSequence;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entity"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private string GetDomainSequence(string pdbId, int entityId, Range[] domainRanges)
        {
            string entitySequence = GetEntitySequence(pdbId, entityId);
            string domainSequence = "";
            foreach (Range domainRange in domainRanges)
            {
                domainSequence += entitySequence.Substring(domainRange.startPos - 1, domainRange.endPos - domainRange.startPos + 1);
            }
            return domainSequence;
        }

        public void PrintOnePfamLigands()
        {
            string pfamId = "Hormone_recep";
            string queryString = string.Format("Select Ligand, Count(Distinct PdbLigands.PdbID) As EntryCount From PdbLigands, PfamLigands " +
                " Where PdbLigands.PdbID = PfamLigands.PdbID AND PdbLigands.AsymChain = PfamLigands.LigandChain AND" +
                " PfamLigands.PfamId = '{0}' Group By Ligand;", pfamId);
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter(pfamId + "_ligands.txt");
            string ligandName = "";
            string ligand = "";
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                ligand = ligandRow["Ligand"].ToString().TrimEnd();
                ligandName = GetLigandName(ligand);
                dataWriter.WriteLine(ligand + "\t" + ligandRow["EntryCount"].ToString() + "\t" + ligandName);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        /// <returns></returns>
        private string GetLigandName(string ligand)
        {
            string queryString = string.Format("Select First 1 Name From PdbLigands Where Ligand = '{0}';", ligand);
            DataTable nameTable = ProtCidSettings.protcidQuery.Query(queryString);
            string ligandName = "";
            if (nameTable.Rows.Count > 0)
            {
                ligandName = nameTable.Rows[0]["Name"].ToString().TrimEnd();
            }
            return ligandName;
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompareSameDimerChainDomainClusters()
        {
            StreamWriter dataWriter = new StreamWriter("Hormone_RecepEntryNotInRel.txt");
            int relSeqId = 9377;
            int relClusterId = 1;
            int superGroupId = 1856;
            int superClusterId = 1;
            string queryString = string.Format("Select Distinct PdbID From PfamSuperInterfaceClusters " +
                " WHere SuperGroupSeqID = {0} AND ClusterID = {1};", superGroupId, superClusterId);
            DataTable superEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                " WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, relClusterId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

            //    ArrayList notInRelEntryList = new ArrayList();
            string pdbId = "";
            foreach (DataRow entryRow in superEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataRow[] relEntryRows = relEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                if (relEntryRows.Length > 0)
                {
                    continue;
                }
                //    notInRelEntryList.Add(pdbId);
                dataWriter.WriteLine(pdbId);
            }
            dataWriter.Close();
        }
        #endregion

        #region Ras clusters
        public void PrintCommonEntriesInClusters()
        {
            int relSeqId = 15457;
            //     int[] clusterIds = { 2, 3, 6 };
            int[] clusterIds = { 3, 6 };
            string queryString = "";
            Dictionary<int, List<string>> clusterEntryListDict = new Dictionary<int, List<string>>();
            foreach (int clusterId in clusterIds)
            {
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
                DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                List<string> clusterEntryList = new List<string>();
                foreach (DataRow entryRow in clusterEntryTable.Rows)
                {
                    clusterEntryList.Add(entryRow["PdbID"].ToString());
                }
                clusterEntryListDict.Add(clusterId, clusterEntryList);
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "Ras\\CommonEntriesInClusters3_6.txt"));
            foreach (int clusterId in clusterIds)
            {
                dataWriter.WriteLine("RelSeqID = " + relSeqId.ToString() + ", ClusterID = " + clusterId.ToString() + ": " + clusterEntryListDict[clusterId].Count.ToString());
            }
            List<string> commonEntryList = new List<string>(clusterEntryListDict[clusterIds[0]]);
            List<string> removeEntryList = new List<string>();
            for (int i = 1; i < clusterIds.Length; i++)
            {
                removeEntryList.Clear();
                foreach (string pdbId1 in commonEntryList)
                {
                    if (!clusterEntryListDict[clusterIds[i]].Contains(pdbId1))
                    {
                        removeEntryList.Add(pdbId1);
                    }
                }
                foreach (string rmPdbId in removeEntryList)
                {
                    commonEntryList.Remove(rmPdbId);
                }
            }
            dataWriter.WriteLine("# Common Entries: " + commonEntryList.Count.ToString());
            string headerLine = "PdbID\t";
            foreach (int clusterId in clusterIds)
            {
                headerLine += clusterId.ToString() + ":InPdb\t" + clusterId.ToString() + ":InPisa\t";
            }
            headerLine += "UniProt";
            dataWriter.WriteLine(headerLine);
            string interfaceInPdbPisaInfo = "";
            string unpCode = "";
            foreach (string pdbId in commonEntryList)
            {
                interfaceInPdbPisaInfo = GetEntryDomainInterfaceInPdbPisaInfo(relSeqId, clusterIds, pdbId);
                unpCode = GetEntryClusterUniprot(relSeqId, clusterIds[0], pdbId);
                dataWriter.WriteLine(pdbId + "\t" + interfaceInPdbPisaInfo + "\t" + unpCode);
            }
            dataWriter.Close();
        }

        private string GetEntryDomainInterfaceInPdbPisaInfo(int relSeqId, int[] clusterIds, string pdbId)
        {
            string inPdbPisaInfo = "";
            foreach (int clusterId in clusterIds)
            {
                if (IsDomainInterfaceInPdbPisa(relSeqId, clusterId, pdbId, "InPdb"))
                {
                    inPdbPisaInfo += "\t1";
                }
                else
                {
                    inPdbPisaInfo += "\t0";
                }

                if (IsDomainInterfaceInPdbPisa(relSeqId, clusterId, pdbId, "InPisa"))
                {
                    inPdbPisaInfo += "\t1";
                }
                else
                {
                    inPdbPisaInfo += "\t0";
                }
                //       inPdbPisaInfo += ("\t" + GetEntryClusterUniprot(relSeqId, clusterId, pdbId));
            }
            return inPdbPisaInfo;
        }

        private string GetEntryClusterUniprot(int relSeqId, int clusterId, string pdbId)
        {
            string queryString = string.Format("Select UnpCode From PfamDomainClusterInterfaces" +
                 " Where RelSeqID = {0} AND ClusterID = {1} AND PdbID = '{2}';", relSeqId, clusterId, pdbId);
            DataTable unpCodeTable = ProtCidSettings.protcidQuery.Query(queryString);
            return unpCodeTable.Rows[0]["UnpCode"].ToString().TrimEnd();
        }

        private bool IsDomainInterfaceInPdbPisa(int relSeqId, int clusterId, string pdbId, string inBaType)
        {
            string queryString = string.Format("Select PdbId, DomainInterfaceID, InPdb, InPisa From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID = {1} AND PdbID = '{2}' AND {3} = '1';", relSeqId, clusterId, pdbId, inBaType);
            DataTable entryInPdbPisaTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (entryInPdbPisaTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region no-cluster chain interfaces with >=2CFs&SeqID<90% domain interfaces with cluster
        public void PrintDomainInterfacesWithBioClustersChainNo()
        {
            string queryString = "Select RelSeqId, ClusterID From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 2 AND MinSeqIdentity < 90;";
            DataTable domainClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            List<string> noClusterChainInterfaceList = new List<string>();
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "NoClusterChainInterfaces.txt"));
            foreach (DataRow relClusterRow in domainClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(relClusterRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(relClusterRow["ClusterID"].ToString());
                string[] clusterChainInterfaces = GetChainInterfaces(relSeqId, clusterId);
                string[] noClusterChainInterfaces = GetChainInterfacesWithNoClusters(clusterChainInterfaces);
                foreach (string chainInterface in noClusterChainInterfaces)
                {
                    if (!noClusterChainInterfaceList.Contains(chainInterface))
                    {
                        noClusterChainInterfaceList.Add(chainInterface);
                    }
                    dataWriter.WriteLine(chainInterface + "\t" + relSeqId.ToString() + "\t" + clusterId.ToString());
                }
            }
            dataWriter.WriteLine("#Domain Clusters: " + domainClusterTable.Rows.Count.ToString());
            dataWriter.WriteLine("#unique Chain Interfaces: " + noClusterChainInterfaceList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckChainInterfaces()
        {
            Dictionary<string, List<int>> multiDomainEntityHash = GetEntryMultiDomainEntityHash();
            StreamReader dataReader = new StreamReader(Path.Combine(dataDir, "NoClusterChainInterfaces.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "NoClusterChainInterfaces_Info.txt"));
            string line = "";
            List<string> entryList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            string dataLine = "";
            List<string> multiDomainEntryList = new List<string>();
            List<string> singleDomainEntryList = new List<string>();
            List<string> multiDomainInterfaceList = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line[0] == '#')
                {
                    dataWriter.WriteLine(line);
                    continue;
                }
                string[] fields = line.Split('\t');
                pdbId = line.Substring(0, 4);
                string[] interfaceFields = fields[0].Split('_');
                interfaceId = Convert.ToInt32(interfaceFields[1]);
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
                if (IsChainInterfaceMultDomain(pdbId, interfaceId, multiDomainEntityHash))
                {
                    dataLine = line + "\t1";
                    if (!multiDomainEntryList.Contains(pdbId))
                    {
                        multiDomainEntryList.Add(pdbId);
                    }
                    multiDomainInterfaceList.Add(fields[0]);
                }
                else
                {
                    dataLine = line + "\t0";
                    if (!singleDomainEntryList.Contains(pdbId))
                    {
                        singleDomainEntryList.Add(pdbId);
                    }
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.WriteLine("#MultiDomain Interfaces: " + multiDomainInterfaceList.Count.ToString());
            dataWriter.WriteLine("#Entry = " + entryList.Count.ToString());
            dataWriter.WriteLine("#MultiDomain Entries = " + multiDomainEntryList.Count.ToString());
            dataWriter.WriteLine("#SingleDomain Entries = " + singleDomainEntryList.Count.ToString());
            dataWriter.Close();
            dataReader.Close();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, List<int>> GetEntryMultiDomainEntityHash()
        {
            Dictionary<string, List<int>> entryMultiDomainEntityHash = new Dictionary<string, List<int>>();
            string queryString = "Select PdbID, EntityID, Count(Distinct DomainID) As DomainCount From PdbPfam " +
                " WHere IsWeak = '0' OR IsUpdated = '1' OR (Pfam_ID NOT LIKE 'Pfam-B%' AND Evalue <= 0.00001) Group By PdbID, EntityID;";
            DataTable entityDomainCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            int entityId = 0;
            int domainCount = 0;
            foreach (DataRow domainCountRow in entityDomainCountTable.Rows)
            {
                domainCount = Convert.ToInt32(domainCountRow["DomainCount"].ToString());
                if (domainCount > 1)
                {
                    pdbId = domainCountRow["PdbID"].ToString();
                    entityId = Convert.ToInt32(domainCountRow["EntityID"].ToString());
                    if (entryMultiDomainEntityHash.ContainsKey(pdbId))
                    {
                        entryMultiDomainEntityHash[pdbId].Add(entityId);
                    }
                    else
                    {
                        List<int> entityList = new List<int>();
                        entityList.Add(entityId);
                        entryMultiDomainEntityHash.Add(pdbId, entityList);
                    }
                }
            }
            return entryMultiDomainEntityHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, string> GetMultiDomainChainHomoGroups()
        {
            List<int> multiDomainChainHomoGroupList = new List<int>();
            string queryString = "Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            string chainPfamArch = "";
            int chainGroupId = 0;
            Dictionary<int, string> chainGroupHash = new Dictionary<int, string>();
            foreach (DataRow chainGroupRow in chainGroupTable.Rows)
            {
                chainPfamArch = chainGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
                if (chainPfamArch.IndexOf(";") > -1)
                {
                    continue;
                }
                if (chainPfamArch.IndexOf(")_(") > -1)  // multidomain homo dimers
                {
                    chainGroupId = Convert.ToInt32(chainGroupRow["SuperGroupSeqID"].ToString());
                    chainGroupHash.Add(chainGroupId, chainPfamArch);
                }
            }
            return chainGroupHash;
        }

        private bool IsChainInterfaceMultDomain(string pdbId, int interfaceId, Dictionary<string, List<int>> multiDomainEntityHash)
        {
            if (!multiDomainEntityHash.ContainsKey(pdbId))
            {
                return false;
            }
            List<int> entityList = multiDomainEntityHash[pdbId];
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces WHere PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (interfaceDefTable.Rows.Count > 0)
            {
                int entityId1 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID1"].ToString());
                int entityId2 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID2"].ToString());
                if (entityList.Contains(entityId1) || entityList.Contains(entityId2))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetChainInterfaces(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select distinct PfamDomainClusterInterfaces.PdbID, InterfaceID  " +
                " From PfamDomainClusterInterfaces, PfamDomainInterfaces " +
                " Where PfamDomainClusterInterfaces.RelSeqId = {0} AND ClusterID = {1} AND " +
                "PfamDomainClusterInterfaces.PdbID = PfamDomainInterfaces.PdbID AND PfamDOmainClusterInterfaces.DomainInterfaceID = PfamDomainInterfaces.DomainInterfaceID;",
                relSeqId, clusterId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] chainInterfaces = new string[chainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in chainInterfaceTable.Rows)
            {
                chainInterfaces[count] = interfaceRow["PdbID"].ToString() + "_" + interfaceRow["InterfaceID"].ToString();
                count++;
            }
            return chainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaces"></param>
        /// <returns></returns>
        private string[] GetChainInterfacesWithNoClusters(string[] chainInterfaces)
        {
            List<string> noClusterChainInterfaceList = new List<string>();
            foreach (string chainInterface in chainInterfaces)
            {
                string[] fields = chainInterface.Split('_');
                if (!IsChainInterfaceInCluster(fields[0], Convert.ToInt32(fields[1])))
                {
                    noClusterChainInterfaceList.Add(chainInterface);
                }
            }
            string[] noClusterChainInterfaces = new string[noClusterChainInterfaceList.Count];
            noClusterChainInterfaceList.CopyTo(noClusterChainInterfaces);
            return noClusterChainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsChainInterfaceInCluster(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, MinSeqIdentity From PfamSuperClusterEntryInterfaces, PfamSuperClusterSumInfo " +
                " WHere PdbID = '{0}' AND InterfaceID = {1} AND PfamSuperClusterEntryInterfaces.SuperGroupSeqID  = PfamSuperClusterSumInfo.SuperGroupSeqID " +
                " AND PfamSuperClusterEntryInterfaces.ClusterID = PfamSuperClusterSumInfo.ClusterID;", pdbId, interfaceId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            int cfgNum = -1;
            double minSeqId = 100.0;
            if (clusterInfoTable.Rows.Count == 0)
            {
                return false;
            }
            cfgNum = Convert.ToInt32(clusterInfoTable.Rows[0]["NumOfCfgCluster"].ToString());
            minSeqId = Convert.ToDouble(clusterInfoTable.Rows[0]["MinSeqIdentity"].ToString());
            if (cfgNum >= 2 && minSeqId < 90.0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region kinase cluster 1
        private string[] KinaseUnpCodes = { "BRAF_HUMAN", "ITK_HUMAN", "RIPK2_HUMAN", "RAF1_HUMAN", "MLKL_HUMAN", "MLKL_MOUSE", "CSK_HUMAN", "CTR1_ARATH" };
        public void GetSpecificKinasesInfo()
        {
            string resultFile = Path.Combine(dataDir, "Pkinase_Tyr_Cluster1_entryInfo.txt");
            StreamWriter dataWriter = new StreamWriter(resultFile);
            dataWriter.WriteLine("UnpCode\tPdbID\tCrystTemp\tCrystPh\tCrystDetails\tCrystMethod\tInCluster");
            string queryString = "";
            int relSeqId = 14511;
            int clusterId = 1;
            string unpCode = "";
            string dataLine = "";
            string pdbId = "";
            queryString = string.Format("Select Distinct UnpCode From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable unpCodeTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow unpCodeRow in unpCodeTable.Rows)
            {
                unpCode = unpCodeRow["UnpCode"].ToString().TrimEnd();
                string[] unpEntries = GetDbCodeEntries(unpCode);
                DataTable entryCrystCondTable = GetCrystalizationConditions(unpEntries);
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1} AND UnpCode = '{2}';",
                    relSeqId, clusterId, unpCode);
                DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow entryCrystRow in entryCrystCondTable.Rows)
                {
                    pdbId = entryCrystRow["PdbID"].ToString();
                    if (IsEntryInCluster(pdbId, clusterEntryTable))
                    {
                        dataLine = unpCode + "\t" + ParseHelper.FormatDataRow(entryCrystRow) + "\t1";
                    }
                    else
                    {
                        dataLine = unpCode + "\t" + ParseHelper.FormatDataRow(entryCrystRow) + "\t0";
                    }
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        public void AddMutationsOrModifiedResidues()
        {
            string dataFile = Path.Combine(dataDir, "Pkinase_Tyr_Cluster1_entryInfo_1.txt");
            string addModDataFile = Path.Combine(dataDir, "Kinase_cluster1_crystCond_modmut.txt");
            DataTable kinaseModMutTable = ReadModifiedMutationsDataToTable();
            StreamWriter dataWriter = new StreamWriter(addModDataFile);
            StreamReader dataReader = new StreamReader(dataFile);
            string line = dataReader.ReadLine(); // header
            string dataLine = line + "\tNumMod\tNumMut";
            dataWriter.WriteLine(dataLine);
            string unpCode = "";
            string pdbId = "";
            int numMod = 0;
            int numMut = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                unpCode = fields[0];
                pdbId = fields[1];
                numMod = GetNumOfModifiedResidues(unpCode, pdbId, kinaseModMutTable);
                numMut = GetNumOfMutationResidues(unpCode, pdbId, kinaseModMutTable);
                dataLine = line + "\t" + numMod + "\t" + numMut;
                dataWriter.WriteLine(dataLine);
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <param name="pdbId"></param>
        /// <param name="kinaseModMutTable"></param>
        /// <returns></returns>
        private int GetNumOfModifiedResidues(string unpCode, string pdbId, DataTable kinaseModMutTable)
        {
            DataRow[] residueRows = kinaseModMutTable.Select(string.Format("UnpCode = '{0}' AND PdbID = '{1}' AND ModType='modified'", unpCode, pdbId));
            return residueRows.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <param name="pdbId"></param>
        /// <param name="kinaseModMutTable"></param>
        /// <returns></returns>
        private int GetNumOfMutationResidues(string unpCode, string pdbId, DataTable kinaseModMutTable)
        {
            DataRow[] residueRows = kinaseModMutTable.Select(string.Format("UnpCode = '{0}' AND PdbID = '{1}' AND ModType='mutation'", unpCode, pdbId));
            return residueRows.Length;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable ReadModifiedMutationsDataToTable()
        {
            DataTable kinaseModMutTable = new DataTable();
            string[] columns = { "UnpCode", "OrgResidue", "AuthNum", "ModResidue", "ModResidueThree", "PdbID", "AuthorChain", "UnpNum", "UnpAccession", "ModType" };
            foreach (string col in columns)
            {
                kinaseModMutTable.Columns.Add(new DataColumn(col));
            }
            string dataFile = Path.Combine(dataDir, "domainClusterInfo\\modified.txt");
            ReadKinaseModifiedResidues(dataFile, kinaseModMutTable);
            dataFile = Path.Combine(dataDir, "domainClusterInfo\\mutations.txt");
            ReadKinaseModifiedResidues(dataFile, kinaseModMutTable);

            return kinaseModMutTable;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="modResFile"></param>
        /// <param name="kinaseModMutTable"></param>
        private void ReadKinaseModifiedResidues(string modResFile, DataTable kinaseModMutTable)
        {
            StreamReader dataReader = new StreamReader(modResFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                DataRow dataRow = kinaseModMutTable.NewRow();
                dataRow["UnpCode"] = fields[1];
                dataRow["ModType"] = fields[0];
                dataRow["OrgResidue"] = fields[2];
                dataRow["AuthNum"] = fields[3];
                dataRow["ModResidue"] = fields[4];
                dataRow["ModResidueThree"] = fields[5];
                dataRow["PdbID"] = fields[6].Substring(0, 4);
                dataRow["AuthorChain"] = fields[6].Substring(4, fields[6].Length - 4);
                dataRow["UnpNum"] = fields[7];
                dataRow["UnpAccession"] = fields[8];
                kinaseModMutTable.Rows.Add(dataRow);
            }
            dataReader.Close();
        }

        private bool IsEntryInCluster(string pdbId, DataTable clusterEntryTable)
        {
            DataRow[] clusterRows = clusterEntryTable.Select(string.Format("PdbID= '{0}'", pdbId));
            if (clusterRows.Length > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string[] GetDbCodeEntries(string unpCode)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbDbRefSifts Where DbCode = '{0}';", unpCode);
            DataTable kinaseEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] kinaseEntries = new string[kinaseEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in kinaseEntryTable.Rows)
            {
                kinaseEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return kinaseEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private DataTable GetCrystalizationConditions(string[] pdbIds)
        {
            string queryString = string.Format("Select PdbID, CrystTemp, CrystPh, CrystDetails, CrystMethod From PdbEntry Where PdbID IN ({0});", ParseHelper.FormatSqlListString(pdbIds));
            DataTable crystCondTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return crystCondTable;
        }
        #endregion

        #region add ligands and other info to cluster text file
        /// <summary>
        /// 
        /// </summary>
        public void AddLigandsToClusterTextFile()
        {
            int relSeqId = 14511;
            int clusterId = 8;

            string relFamilyString = GetRelationString(relSeqId);
            string clusterInfoTxtFile = Path.Combine(dataDir, relFamilyString + "_" + relSeqId + "_" + clusterId + ".txt");
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            StreamWriter clusterInfoWriter = new StreamWriter(clusterInfoTxtFile);
            string headerLine = "FamilyPair\t";
            foreach (DataColumn dCol in clusterInterfaceTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            headerLine = headerLine + "Ligands";
            clusterInfoWriter.WriteLine(headerLine);
            string dataLine = "";
            string ligands = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                ligands = GetInterfaceLigands(pdbId, domainInterfaceId);
                dataLine = relFamilyString + "\t" + ParseHelper.FormatDataRow(interfaceRow) + "\t" + ligands;
                clusterInfoWriter.WriteLine(dataLine);
            }
            clusterInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetRelationString(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqId = {0};", relSeqId);
            DataTable relStrTable = ProtCidSettings.protcidQuery.Query(queryString);
            string familyCode1 = relStrTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            string familyCode2 = relStrTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            string relString = "";
            if (familyCode1 == familyCode2)
            {
                relString = familyCode1;
            }
            else
            {
                relString = familyCode1 + ";" + familyCode2;
            }

            return relString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceLigands(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select ChainDomainID1, ChainDomainID2 From PfamDomainInterfaces Where PdbId = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable asymChainTable = ProtCidSettings.protcidQuery.Query(queryString);
            int chainDomainId1 = Convert.ToInt32(asymChainTable.Rows[0]["ChainDomainID1"].ToString().TrimEnd());
            int chainDomainId2 = Convert.ToInt32(asymChainTable.Rows[0]["ChainDomainID2"].ToString().TrimEnd());
            int[] dimerChainDomainIds = null;
            if (chainDomainId1 == chainDomainId2)
            {
                dimerChainDomainIds = new int[1];
                dimerChainDomainIds[0] = chainDomainId1;
            }
            else
            {
                dimerChainDomainIds = new int[2];
                dimerChainDomainIds[0] = chainDomainId1;
                dimerChainDomainIds[1] = chainDomainId2;
            }
            queryString = string.Format("Select Distinct Ligand From PdbLigands, PfamLigands Where PfamLigands.PdbID = '{0}' AND ChainDomainID IN ({1}) AND " +
                " PfamLigands.PdbID = PdbLigands.PdbID AND PfamLigands.LigandChain = PdbLigands.AsymChain AND PfamLigands.LigandSeqID = PdbLigands.SeqID;",
                pdbId, ParseHelper.FormatSqlListString(dimerChainDomainIds));
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            string ligands = "";
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                ligands += (ligandRow["Ligand"].ToString().TrimEnd() + ";");
            }
            return ligands.TrimEnd(';');
        }
        #endregion

        #region ACT - table data
        public void PrintDifACTPfamClusters ()
        {
            string actPfamRelFile = Path.Combine(dataDir, "domainClusterInfo\\ACT\\ACT_relationsSumInfo.txt");
            StreamWriter actClusterWriter = new StreamWriter (actPfamRelFile);
            actClusterWriter.WriteLine("Pfams\tClusterID\tUniProt Code\tChain Pfam Arch\t#Entries");
            string queryString = "Select * From PfamDomainFamilyRelation Where FamilyCode1 Like 'ACT%' AND FamilyCode2 Like 'ACT%' AND ClanSeqID = 379 " + 
                " Order By FamilyCode1, FamilyCode2;";
            DataTable actRelationTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId1 = "";
            string pfamId2 = "";
            int relSeqId = 0;
            foreach (DataRow relRow in actRelationTable.Rows)
            {
                pfamId1 = relRow["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = relRow["FamilyCode2"].ToString().TrimEnd();
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString ());
                GetACTClusterUnpChainPfamArchInfo(pfamId1, pfamId2, relSeqId, actClusterWriter);
            }
            actClusterWriter.Close ();
        }

        private void GetACTClusterUnpChainPfamArchInfo (string pfam1, string pfam2, int relSeqId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select RelSeqId, ClusterId, UnpCode, ChainPfamArch, Count(Distinct PdbID) As EntryCount " + 
                " From PfamDomainClusterInterfaces Where RelSeqID = {0} Group By RelSeqId, ClusterId, UnpCode, ChainPfamArch;", relSeqId);
            DataTable unpChainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            string dataLine = "";
            int preClusterId = -1;
            int clusterId = 0;
            foreach (DataRow clusterRow in unpChainPfamArchTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                if (preClusterId != -1 && preClusterId != clusterId)
                {
                    int[] clusterNumbers = GetACTclusterNumbers(relSeqId, preClusterId);
                    dataWriter.WriteLine("\t\t" + clusterNumbers[0] + "\t" + clusterNumbers[1] + "\t" + clusterNumbers[2]);
                }
                dataLine = pfam1 + "-" + pfam2 + "\t" + clusterRow["ClusterID"].ToString() + "\t" +
                    clusterRow["UnpCode"].ToString().TrimEnd() + "\t" + clusterRow["ChainPfamArch"].ToString().TrimEnd() + "\t" +
                    clusterRow["EntryCount"].ToString();
                dataWriter.WriteLine(dataLine);
                preClusterId = clusterId;
            }
            int[] lastClusterNumbers = GetACTclusterNumbers(relSeqId, clusterId);
            dataWriter.WriteLine("\t\t" + lastClusterNumbers[0] + "\t" + lastClusterNumbers[1] + "\t" + lastClusterNumbers[2]);
            dataWriter.WriteLine();
            dataWriter.Flush();
        }

        private int[] GetACTclusterNumbers (int relSeqId, int clusterId)
        {
            int[] clusterNumbers = new int[3];
            string queryString = string.Format("Select Distinct UnpCode From PfamDomainClusterInterfaces" + 
                " Where RelSeqID = {0} AND ClusterId = {1};", relSeqId, clusterId);
            DataTable clusterUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            clusterNumbers[0] = clusterUnpTable.Rows.Count;

            queryString = string.Format("Select Distinct ChainPfamArch From PfamDomainClusterInterfaces" +
                " Where RelSeqID = {0} AND ClusterId = {1};", relSeqId, clusterId);
            DataTable clusterPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            clusterNumbers[1] = clusterPfamArchTable.Rows.Count;

            queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces" +
                " Where RelSeqID = {0} AND ClusterId = {1};", relSeqId, clusterId);
            DataTable clusterPdbTable = ProtCidSettings.protcidQuery.Query(queryString);
            clusterNumbers[2] = clusterPdbTable.Rows.Count;

            return clusterNumbers;
        }
        #endregion

        #region Ras and ErbB proteins
        /// <summary>
        /// 
        /// </summary>
        public void PrintRasAlphaDimerCluster ()
        {
            int relSeqId = 15457;
            int clusterId = 3;
   //         string pfamId = "Ras";
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "Ras_alphaDimer_UniProts_cluster_entries_1.txt"));
            string queryString = string.Format("Select RelCfGroupID, SpaceGroup, PdbID, DomainInterfaceID, SurfaceArea, ChainPfamArch, InPdb, InPisa, InAsu, UnpCode " +
                " From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            Dictionary<string, List<int>> entryInterfaceListDict = new Dictionary<string, List<int>>();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString ());
                if (entryInterfaceListDict.ContainsKey (pdbId))
                {
                    entryInterfaceListDict[pdbId].Add(domainInterfaceId);
                }
                else
                {
                    List<int> interfaceList = new List<int>();
                    interfaceList.Add(domainInterfaceId);
                    entryInterfaceListDict.Add(pdbId, interfaceList);
                }
            }
            string dataLine = "";
            Dictionary<string, int> ligandEntryNumDict = new Dictionary<string, int>();
            foreach (string lsPdb in entryInterfaceListDict.Keys)
            {
                int[] interfaceDomains = GetDomainInterface(lsPdb, entryInterfaceListDict[lsPdb].ToArray());
                string[] mutations = GetDomainMutations(lsPdb, interfaceDomains);
                string[] ligands = GetDomainLigands(lsPdb, interfaceDomains);
                foreach (string ligand in ligands)
                {
                    if (ligandEntryNumDict.ContainsKey (ligand))
                    {
                        ligandEntryNumDict[ligand] = ligandEntryNumDict[ligand] + 1;
                    }
                    else
                    {
                        ligandEntryNumDict.Add(ligand, 1);
                    }
                }
                DataRow[] clusterRows = clusterInterfaceTable.Select(string.Format ("PdbID = '{0}'", lsPdb));
                dataLine = clusterRows[0]["RelCfGroupId"] + "\t" + clusterRows[0]["SpaceGroup"] + "\t" + clusterRows[0]["CrystForm"] + "\t" +
                    clusterRows[0]["PdbID"] + "\t" + clusterRows[0]["SurfaceArea"] + "\t" + clusterRows[0]["ChainPfamArch"] + "\t" +
                    clusterRows[0]["InPdb"] + "\t" + clusterRows[0]["InPisa"] + "\t" + clusterRows[0]["InAsu"] + "\t" +
                    clusterRows[0]["UnpCode"] + "\t" + FormatArrayString(ligands) + "\t" + FormatArrayString(mutations);
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.WriteLine();
            string ligandName  = "";
            foreach (string ligand in ligandEntryNumDict.Keys)
            {
                ligandName = GetLigandName(ligand);
                dataWriter.WriteLine(ligand + " #entries = " + ligandEntryNumDict[ligand] + " " + ligandName);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domains"></param>
        /// <returns></returns>
        private string[] GetDomainMutations (string pdbId, int[] domains)
        {
   /*         string queryString = string.Format("Select PdbModRes.* From PdbModRes, PdbPfamChain Where PdbModRes.PdbID = '{0}' " + 
                " AND ChainDomainID IN ({1}) AND " + 
                " PdbModRes.PdbID = PdbPfamChain.PdbID AND PdbModRes.AsymID = PdbPfamChain.AsymChain;", 
                pdbId, ParseHelper.FormatSqlListString (domains));*/
            string queryString = string.Format("Select DbResidue, Residue, AuthorSeqNum, AuthorChain, Details From PdbDbRefSeqDifXml, PdbPfamChain " +
                " Where PdbPfamChain.PdbID = '{0}' AND ChainDomainID IN ({1}) AND Lower(details) like 'engineered%' AND " +
                " PdbPfamChain.PdbID = PdbDbRefSeqDifXml.PdbID AND PdbPfamChain.AuthChain = PdbDbRefSeqDifXml.AuthorChain;", pdbId, ParseHelper.FormatSqlListString (domains));
            DataTable mutationTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> mutResList = new List<string>();
            string mutRes = "";
            foreach (DataRow mutRow in mutationTable.Rows)
            {
                mutRes = mutRow["AuthorChain"].ToString ().TrimEnd () + ":" + mutRow["DbResidue"].ToString().TrimEnd() + 
                    mutRow["AuthorSeqNum"].ToString().TrimEnd() + mutRow["Residue"].ToString().TrimEnd() + 
                    "(" + mutRow["Details"].ToString ().TrimEnd () + ")";
                if (!mutResList.Contains(mutRes))
                {
                    mutResList.Add(mutRes);
                }
            }
            return mutResList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryMutations(string pdbId)
        {
            string queryString = string.Format("Select DbResidue, Residue, AuthorSeqNum, AuthorChain, Details From PdbDbRefSeqDifXml " +
                " Where PdbID = '{0}' AND Lower(details) like 'engineered%';", pdbId);
            DataTable mutationTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> mutResList = new List<string>();
            string mutRes = "";
            foreach (DataRow mutRow in mutationTable.Rows)
            {
                mutRes = mutRow["AuthorChain"].ToString().TrimEnd() + ":" + mutRow["DbResidue"].ToString().TrimEnd() +
                    mutRow["AuthorSeqNum"].ToString().TrimEnd() + mutRow["Residue"].ToString().TrimEnd();
   //                  + "(" + mutRow["Details"].ToString().TrimEnd() + ")";
                if (!mutResList.Contains(mutRes))
                {
                    mutResList.Add(mutRes);
                }
            }
            return mutResList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domains"></param>
        /// <returns></returns>
        private string[] GetDomainLigands (string pdbId, int[] domains)
        {
            string queryString = string.Format("Select Distinct Ligand From PfamLigands, PdbLigands Where PfamLigands.PdbID = '{0}' AND ChainDomainID IN ({1}) AND " + 
                " PfamLigands.PdbID = PdbLigands.PdbID AND PfamLigands.LigandChain = PdbLigands.AsymChain;", pdbId, ParseHelper.FormatSqlListString (domains));
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] ligands = new string[ligandTable.Rows.Count];
            int count = 0;
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                ligands[count] = ligandRow["Ligand"].ToString().TrimEnd();
                count++;
            }
            return ligands;
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetDomainInterface (string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format ("Select Distinct ChainDomainID1, ChainDomainID2 From PfamDomainInterfaces " + 
                " Where PdbID = '{0}' AND DomainInterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString (domainInterfaceIds));
            DataTable interfaceDomainTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> domainList = new List<int>();
            int domainId = 0;
            foreach (DataRow domainRow in interfaceDomainTable.Rows)
            {
                domainId = Convert.ToInt32(domainRow["ChainDomainID1"].ToString());
                if (! domainList.Contains (domainId))
                {
                    domainList.Add(domainId);
                }
                domainId = Convert.ToInt32(domainRow["ChainDomainID2"].ToString());
                if (!domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
            }
            return domainList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintRasErbbAsymDimerSumInfo() 
        {
           int relSeqId = 14511;
            int clusterId = 7;
            string pfamId = "Pkinase_Tyr";
         /*   int relSeqId = 15457;
            int clusterId = 3;
            string pfamId = "Ras";*/
            string queryString = string.Format ("Select Distinct UnpCode, PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterUnpPdbTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpClusterEntryListDict = new Dictionary<string, List<string>>();
            string unpCode = "";
            string pdbId = "";
            foreach (DataRow entryRow in clusterUnpPdbTable.Rows)
            {
                unpCode = entryRow["UnpCode"].ToString().TrimEnd();
                pdbId = entryRow["PdbID"].ToString().TrimEnd();
                if (unpCode.IndexOf(";") > -1)
                {
                    string[] fields = unpCode.Split(';');
                    foreach (string unpField in fields)
                    {
                        if (unpClusterEntryListDict.ContainsKey(unpField))
                        {
                            unpClusterEntryListDict[unpField].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string>();
                            entryList.Add(pdbId);
                            unpClusterEntryListDict.Add(unpField, entryList);
                        }
                    }
                }
                else
                {
                    if (unpClusterEntryListDict.ContainsKey(unpCode))
                    {
                        unpClusterEntryListDict[unpCode].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        unpClusterEntryListDict.Add(unpCode, entryList);
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_cluster" + clusterId + "_UniProts_entries.txt"));
            dataWriter.WriteLine("UniProt\tPDB\tASU\tPdbBA\tPisaBA\tLigands\tMutations\tInCluster\tCF\tEntryPfamArch");
            int numEntriesOfUnp = 0;
            string dataLine = "";
            string sumDataLines = "";
            string entryPfamArch = "";
            string entryAsu = "";
            string pdbBa = "";
            string pisaBa = "";
            Dictionary<string, Dictionary<string, int>> unpLigandEntryNumInClusterDict = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, Dictionary<string, int>> unpLigandEntryNumNotClusterDict = new Dictionary<string, Dictionary<string, int>>();
   //         string[] phosphateLigands = {"GTP", "GNP", "" };
            foreach (string lsUnpCode in unpClusterEntryListDict.Keys)
            {
                Dictionary<string, string> entryCfDict = GetUnpEntryCfDict(lsUnpCode, pfamId, out numEntriesOfUnp);
                List<string> leftCfList = new List<string>();
                List<string> cfList = new List<string>();
                List<string> clusterCfList = new List<string>();
                List<string> notThisUnpEntryList = new List<string> ();
                foreach (string lsPdb in unpClusterEntryListDict[lsUnpCode])
                {
                    if (! entryCfDict.ContainsKey (lsPdb))
                    {
                        notThisUnpEntryList.Add(lsPdb);
                    }
                }
                foreach (string lsPdb in entryCfDict.Keys)
                {
                    string[] ligands = GetEntryLigands(lsPdb, pfamId);
                    string[] mutations = GetEntryMutations(lsPdb, pfamId);
                    entryPfamArch = GetEntryPfamArch(lsPdb);
                    entryAsu = GetEntryAsu(lsPdb);
                    pdbBa = GetEntryPdbBu(lsPdb);
                    pisaBa = GetEntryPisaBu(lsPdb);
                    dataLine = lsUnpCode + "\t" + lsPdb + "\t" + entryAsu + "\t" + pdbBa + "\t" + pisaBa + "\t" +
                        FormatArrayString(ligands) + "\t" + FormatArrayString(mutations);
                    if (unpClusterEntryListDict[lsUnpCode].Contains(lsPdb))
                    {
                        AddLigandToDict(lsUnpCode, ligands, unpLigandEntryNumInClusterDict);
                        dataLine += "\t1";
                    }
                    else
                    {
                        AddLigandToDict(lsUnpCode, ligands, unpLigandEntryNumNotClusterDict);
                        dataLine += "\t0";
                    }
                    if (entryCfDict.ContainsKey(lsPdb))
                    {
                        if (!unpClusterEntryListDict[lsUnpCode].Contains(lsPdb))
                        {
                            if (!leftCfList.Contains(entryCfDict[lsPdb]))
                            {
                                leftCfList.Add(entryCfDict[lsPdb]);
                            }                            
                        }
                        else
                        {
                            if (! clusterCfList.Contains (entryCfDict[lsPdb]))
                            {
                                clusterCfList.Add(entryCfDict[lsPdb]);
                            }                           
                        }

                        if (!cfList.Contains(entryCfDict[lsPdb]))
                        {
                            cfList.Add(entryCfDict[lsPdb]);
                        }
                        dataLine += ("\t" + entryCfDict[lsPdb]);
                    }
                    else
                    {
                        dataLine += "\t-";
                    }
                    dataWriter.WriteLine(dataLine + "\t" + entryPfamArch);
                }
                sumDataLines += (lsUnpCode + " #Entries=" + numEntriesOfUnp + " #CFs=" + cfList.Count + " #EntriesCluster=" + unpClusterEntryListDict[lsUnpCode].Count +
                        " #CFsCluster=" + clusterCfList.Count + " #EntriesLeft" + (numEntriesOfUnp - unpClusterEntryListDict[lsUnpCode].Count) + " #CFsLeft" + leftCfList.Count + "\r\n");
            }
            dataWriter.WriteLine();
            dataWriter.Write(sumDataLines);
            sumDataLines = "";
            dataWriter.WriteLine("In Cluster");
            foreach (string lsUnp in unpLigandEntryNumInClusterDict.Keys)
            {
                sumDataLines = lsUnp + " ";
                foreach (string ligand in unpLigandEntryNumInClusterDict[lsUnp].Keys)
                {
                    sumDataLines += (ligand + "=" + unpLigandEntryNumInClusterDict[lsUnp][ligand] + " ");
                }
                dataWriter.WriteLine(sumDataLines);
            }
            dataWriter.WriteLine("Not In Cluster");
            foreach (string lsUnp in unpLigandEntryNumNotClusterDict.Keys)
            {
                sumDataLines = lsUnp + " ";
                foreach (string ligand in unpLigandEntryNumNotClusterDict[lsUnp].Keys)
                {
                    sumDataLines += (ligand + "=" + unpLigandEntryNumNotClusterDict[lsUnp][ligand] + " ");
                }
                dataWriter.WriteLine(sumDataLines);
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintBromodomainChainClustersSumInfo()
        {
            int chainGroupId = 8980;
            int clusterId = 10;
            string pfamId = "Bromodomain";
            string queryString = string.Format("Select Distinct UnpCode, PdbID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1};", chainGroupId, clusterId);
            DataTable clusterUnpPdbTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpClusterEntryListDict = new Dictionary<string, List<string>>();
            string unpCode = "";
            string pdbId = "";
            List<string> unpCfList = new List<string>();
            List<string> unpEntryList = new List<string>();
            foreach (DataRow entryRow in clusterUnpPdbTable.Rows)
            {
                unpCode = entryRow["UnpCode"].ToString().TrimEnd();
                pdbId = entryRow["PdbID"].ToString().TrimEnd();
                if (unpCode.IndexOf(";") > -1)
                {
                    string[] fields = unpCode.Split(';');
                    foreach (string unpField in fields)
                    {
                        if (unpClusterEntryListDict.ContainsKey(unpField))
                        {
                            unpClusterEntryListDict[unpField].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string>();
                            entryList.Add(pdbId);
                            unpClusterEntryListDict.Add(unpField, entryList);
                        }
                    }
                }
                else
                {
                    if (unpClusterEntryListDict.ContainsKey(unpCode))
                    {
                        unpClusterEntryListDict[unpCode].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        unpClusterEntryListDict.Add(unpCode, entryList);
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, pfamId + "_chaincluster" + clusterId + "_UniProts_entries.txt"));
            dataWriter.WriteLine("UniProt\tPDB\tASU\tLigands\tMutations\tInCluster\tCF\tEntryPfamArch");
            int numEntriesOfUnp = 0;
            string dataLine = "";
            string sumDataLines = "";
            string entryPfamArch = "";
            string entryAsu = "";
            foreach (string lsUnpCode in unpClusterEntryListDict.Keys)
            {
                Dictionary<string, string> entryCfDict = GetUnpEntryCfDict(lsUnpCode, pfamId, out numEntriesOfUnp);
                List<string> leftCfList = new List<string>();
                List<string> cfList = new List<string>();
                List<string> clusterCfList = new List<string>();
                List<string> notThisUnpEntryList = new List<string>();
                foreach (string lsPdb in unpClusterEntryListDict[lsUnpCode])
                {
                    if (!entryCfDict.ContainsKey(lsPdb))
                    {
                        notThisUnpEntryList.Add(lsPdb);
                    }                   
                }
                foreach (string lsPdb in entryCfDict.Keys)
                {
                    string[] ligands = GetEntryLigands(lsPdb, pfamId);
                    string[] mutations = GetEntryMutations(lsPdb, pfamId);
                    entryPfamArch = GetEntryPfamArch(lsPdb);
                    entryAsu = GetEntryAsu(lsPdb);
                    if (! unpEntryList.Contains(lsPdb))
                    {
                        unpEntryList.Add(lsPdb);
                    }
                    dataLine = lsUnpCode + "\t" + lsPdb + "\t" + entryAsu + "\t" + FormatArrayString(ligands) + "\t" + FormatArrayString(mutations);
                    if (unpClusterEntryListDict[lsUnpCode].Contains(lsPdb))
                    {
                        dataLine += "\t1";
                    }
                    else
                    {
                        dataLine += "\t0";
                    }
                    if (entryCfDict.ContainsKey(lsPdb))
                    {
                        if (!unpClusterEntryListDict[lsUnpCode].Contains(lsPdb))
                        {
                            if (!leftCfList.Contains(entryCfDict[lsPdb]))
                            {
                                leftCfList.Add(entryCfDict[lsPdb]);
                            }
                        }
                        else
                        {
                            if (!clusterCfList.Contains(entryCfDict[lsPdb]))
                            {
                                clusterCfList.Add(entryCfDict[lsPdb]);
                            }
                        }

                        if (!cfList.Contains(entryCfDict[lsPdb]))
                        {
                            cfList.Add(entryCfDict[lsPdb]);
                        }
                        if (! unpCfList.Contains(entryCfDict[lsPdb]))
                        {
                            unpCfList.Add(entryCfDict[lsPdb]);
                        }
                        dataLine += ("\t" + entryCfDict[lsPdb]);
                    }
                    else
                    {
                        dataLine += "\t-";
                    }
                    dataWriter.WriteLine(dataLine + "\t" + entryPfamArch);
                }
                sumDataLines += (lsUnpCode + " #Entries=" + numEntriesOfUnp + " #CFs=" + cfList.Count + " #EntriesCluster=" + unpClusterEntryListDict[lsUnpCode].Count +
                        " #CFsCluster=" + clusterCfList.Count + " #EntriesLeft" + (numEntriesOfUnp - unpClusterEntryListDict[lsUnpCode].Count) + " #CFsLeft" + leftCfList.Count + "\r\n");
            }
            dataWriter.WriteLine();
            dataWriter.WriteLine("#CFs of all Uniprots in cluster = " + unpCfList.Count);
            dataWriter.WriteLine("#entries of all uniprots in cluster = " + unpEntryList.Count);
            dataWriter.Write(sumDataLines);
            
            dataWriter.Close();
        }

        private void AddLigandToDict (string unp, string[] ligands, Dictionary<string, Dictionary<string, int>> unpLigandEntryNumDict)
        {
           if (unpLigandEntryNumDict.ContainsKey (unp))
           {
               foreach (string ligand in ligands)
               {
                   if (unpLigandEntryNumDict[unp].ContainsKey(ligand))
                   {
                       unpLigandEntryNumDict[unp][ligand] = unpLigandEntryNumDict[unp][ligand] + 1;
                   }
                   else
                   {
                       unpLigandEntryNumDict[unp].Add(ligand, 1);
                   }
               }
           }
           else
           {
               Dictionary<string, int> ligandEntryNumDict = new Dictionary<string, int>();
               foreach (string ligand in ligands)
               {
                   ligandEntryNumDict.Add(ligand, 1);
               }
               unpLigandEntryNumDict.Add(unp, ligandEntryNumDict);
           }
        }

        public void GetEGFRsummary ()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "EGFR_clusterSummary.txt"));
            StreamReader dataReader = new StreamReader(Path.Combine(dataDir, "EGFR_clusterUniProts_entries_updated.txt"));
            string line = dataReader.ReadLine (); // header line
            int[] inclusterAsuA = new int[2];
            int[] notClusterAsuA = new int[2];           
            int[] inClusterAsuA2 = new int[3];
            int[] notClusterAsuA2 = new int[3];
            string asu = "";
            int chainBIndex = 0;
            bool inCluster = false;
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 10)
                {
                    continue;
                }
                asu = fields[2];
                if (fields[8] != "")
                {
                    chainBIndex = fields[8].IndexOf("B");
                    if (chainBIndex > -1)
                    {
                        asu = fields[2].Substring(0, chainBIndex);
                    }
                }
                if (fields[5] == "1")
                {
                    inCluster = true;
                }
                else
                {
                    inCluster = false;
                }
                if (asu == "A")
                {
                    if (inCluster)
                    {
                        if (fields[10] == "")
                        {
                            inclusterAsuA[0]++;  // in cluster and active
                        }
                        else
                        {
                            inclusterAsuA[1]++; // in cluster and inactive
                        }
                    }
                    else
                    {
                        if (fields[10] == "")
                        {
                            notClusterAsuA[0]++;  // not in cluster and inactive
                        }
                        else
                        {
                            notClusterAsuA[1]++; // not in cluster and active
                        }
                    }
                }
                else // greater than monomer
                {
                    if (inCluster)
                    {
                        if (fields[10] == "")
                        {
                            inClusterAsuA2[0]++;  // in cluster and active
                        }
                        else if (fields[10] == "inactive")
                        {
                            inClusterAsuA2[1]++; // in cluster and inactive
                        }
                        else
                        {
                            inClusterAsuA2[2]++;
                        }
                    }
                    else
                    {
                        if (fields[10] == "")
                        {
                            notClusterAsuA2[0]++;  // not in cluster and inactive
                        }
                        else if (fields[10] == "active")
                        {
                            notClusterAsuA2[1]++; // not in cluster and active
                        }
                        else
                        {
                            notClusterAsuA2[2]++;
                        }
                    }
                }
            }
            dataReader.Close();
            dataWriter.WriteLine("ASU: A");
            dataWriter.WriteLine("In cluster: active=" + inclusterAsuA[0] + " inactive=" + inclusterAsuA[1]);
            dataWriter.WriteLine("Not in cluster: active=" + notClusterAsuA[1] + " inactive=" + notClusterAsuA[0]);
            dataWriter.WriteLine("ASU: A2");
            dataWriter.WriteLine("In cluster: active=" + inClusterAsuA2[0] + " inactive=" + inClusterAsuA2[1] + " active/inactive=" + inClusterAsuA2[2]);
            dataWriter.WriteLine("Not in cluster: active=" + notClusterAsuA2[1] + " inactive=" + notClusterAsuA2[0] + " active/inactive=" + notClusterAsuA2[2]);
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryLigands(string pdbId, string pfamId)
        {
            string queryString = string.Format("Select Distinct Ligand From PfamLigands, PdbLigands Where PfamLigands.PdbID = '{0}' AND PfamId = '{1}' AND " +
               " PfamLigands.PdbID = PdbLigands.PdbID AND PfamLigands.LigandChain = PdbLigands.AsymChain;", pdbId, pfamId);
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] ligands = new string[ligandTable.Rows.Count];
            int count = 0;
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                ligands[count] = ligandRow["Ligand"].ToString().TrimEnd();
                count++;
            }
            return ligands;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryLigands(string pdbId)
        {
            string queryString = string.Format("Select Distinct Ligand From PdbLigands Where PdbID = '{0}' Order By Ligand;", pdbId);
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] ligands = new string[ligandTable.Rows.Count];
            int count = 0;
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                ligands[count] = ligandRow["Ligand"].ToString().TrimEnd();
                count++;
            }
            return ligands;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryAsu (string pdbId)
        {
            string queryString = string.Format ("Select Asu From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            DataTable asuTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (asuTable.Rows.Count == 0)
            {
                queryString = string.Format("Select AbcFormat As Asu From PdbAsu Where PdbID = '{0}';", pdbId);
                asuTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            if (asuTable.Rows.Count > 0)
            {
                return asuTable.Rows[0]["Asu"].ToString().TrimEnd();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryPdbBu (string pdbId)
        {
            string queryString = string.Format("Select PdbBu From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            DataTable buTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (buTable.Rows.Count == 0)
            {
                queryString = string.Format("Select AbcFormat As PdbBu From PdbBiolUnits Where PdbID = '{0}' and BuID = '1';", pdbId);
                buTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            if (buTable.Rows.Count > 0)
            {
                return buTable.Rows[0]["PdbBu"].ToString().TrimEnd();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryPisaBu(string pdbId)
        {
            string queryString = string.Format("Select PisaBu From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            DataTable buTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (buTable.Rows.Count == 0)
            {
                queryString = string.Format("Select AbcFormat As PisaBu From PisaBiolUnits Where PdbID = '{0}' and BuID = '1';", pdbId);
                buTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            if (buTable.Rows.Count > 0)
            {
                return buTable.Rows[0]["PisaBu"].ToString().TrimEnd();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domains"></param>
        /// <returns></returns>
        private string[] GetEntryMutations(string pdbId, string pfamId)
        {
            string queryString = string.Format("Select DbResidue, Residue, AuthorSeqNum, AuthorChain, Details From PdbDbRefSeqDifXml, PdbPfamChain, PdbPfam" +
                 " Where PdbPfamChain.PdbID = '{0}' AND Lower(details) like 'engineered%' AND Pfam_ID = '{1}' AND " + 
                 " PdbPfamChain.PdbID = PdbPfam.PdbID AND PdbPfamChain.DomainID = PdbPfam.DomainID AND PdbPfamChain.EntityID = PdbPfam.EntityID AND " +
                 " PdbPfamChain.PdbID = PdbDbRefSeqDifXml.PdbID AND PdbPfamChain.AuthChain = PdbDbRefSeqDifXml.AuthorChain;", pdbId, pfamId);
            DataTable mutationTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> mutResList = new List<string>();
            string mutRes = "";
            foreach (DataRow mutRow in mutationTable.Rows)
            {
                mutRes = mutRow["AuthorChain"].ToString ().TrimEnd () + ":" +  mutRow["DbResidue"].ToString().TrimEnd() + 
                    mutRow["AuthorSeqNum"].ToString().TrimEnd() + mutRow["Residue"].ToString().TrimEnd() + 
                    "(" + mutRow["Details"].ToString ().TrimEnd () + ")";
                if (!mutResList.Contains(mutRes))
                {
                    mutResList.Add(mutRes);
                }
            }
            return mutResList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <param name="pfamId"></param>
        /// <param name="numEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetUnpEntryCfDict (string unpCode, string pfamId, out int numEntries)
        {
            string queryString = string.Format ("Select Distinct PdbPfam.PdbID From PdbDbRefSifts, PdbPfam Where DbCode = '{0}' AND Pfam_ID = '{1}' AND " + 
                " PdbPfam.PdbID = PdbDbRefSifts.PdbID AND PdbPfam.EntityID = PdbDbRefSifts.EntityID;", unpCode, pfamId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbPfam.PdbID From PdbDbRefXml, PdbPfam Where DbCode = '{0}' AND Pfam_ID = '{1}' AND " +
                " PdbPfam.PdbID = PdbDbRefXml.PdbID AND PdbPfam.EntityID = PdbDbRefXml.EntityID;", unpCode, pfamId);
            DataTable xmlEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entryList = new List<string>();
            Dictionary<string, string> entryCfDict = new Dictionary<string, string>();
            string pdbId = "";
            string cfGroup = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                entryList.Add(pdbId);
                cfGroup = GetEntryCf(pdbId);
                //       if (cfGroup != "-")
                entryCfDict.Add(pdbId, cfGroup);
            }
            foreach (DataRow entryRow in xmlEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (entryList.Contains (pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                cfGroup = GetEntryCf(pdbId);
                //       if (cfGroup != "-")
                entryCfDict.Add(pdbId, cfGroup);
            }
            numEntries = entryList.Count;
            return entryCfDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryCf (string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID, CfGroupID From PfamNonRedundantCfGroups Where PdbID = '{0}';", pdbId);
            DataTable cfTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (cfTable.Rows.Count== 0)
            {
                queryString = string.Format("Select PfamNonRedundantCfGroups.GroupSeqID, CfGroupID From PfamHomoRepEntryAlign, PfamNonRedundantCfGroups " +
                    " Where PdbID2 = '{0}' AND PfamNonRedundantCfGroups.PdbID = PfamHomoRepEntryAlign.PdbID1;", pdbId);
                cfTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            if (cfTable.Rows.Count > 0)
            {
                return cfTable.Rows[0]["GroupSeqID"].ToString() + "-" + cfTable.Rows[0]["CfGroupID"].ToString();
            }
            return pdbId;
        }

        public void RetreiveRasFromPfamDomainAlignFile ()
        {
            // RASH-GTP/GNP, RASH-GDP
            string rasDomainAlignDataDir = @"X:\Qifang\Paper\protcid_update\data_v31\domainClusterInfo\Ras\Ras_pdb";
            string rasProtein = "RASK";
            string rasPdbPmlFile = Path.Combine(rasDomainAlignDataDir, "Ras_pairFitDomain_unpPfams_pdb.pml");           
            string[] triPhosphates = { "GTP", "GNP" };
            string[] diphosphates = {"GDP"};

            string[] triphPdbIds = RetrieveProteinLigandEntryList(rasProtein, triPhosphates);
            string triphosphatesNewPmlScript = Path.Combine(rasDomainAlignDataDir, rasProtein + "_Triphosphates_pairFitDomain_unpPfams.pml");
            WritePyMolScript(triphPdbIds, rasPdbPmlFile, triphosphatesNewPmlScript);

            string[] diphPdbIds = RetrieveProteinLigandEntryList(rasProtein, diphosphates);
            string diphosphateNewPmlScript = Path.Combine(rasDomainAlignDataDir, rasProtein + "_Diphosphates_pairFitDomain_unpPfams.pml");
            WritePyMolScript(diphPdbIds, rasPdbPmlFile, diphosphateNewPmlScript);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rasProtein"></param>
        /// <param name="boundLigands"></param>
        /// <returns></returns>
        private string[] RetrieveProteinLigandEntryList(string rasProtein, string[] boundLigands)
        {
            string queryString = string.Format ("Select Distinct PdbID From PdbDbRefSifts Where DbCode Like '{0}%';", rasProtein);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<string> pdbIdList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (DoesEntryContainLigands (pdbId, boundLigands))
                {
                    pdbIdList.Add(pdbId);
                }                
            }

            queryString = string.Format ("Select Distinct PdbID From PdbDbRefXml Where DbCode Like '{0}%'", rasProtein);
            entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (pdbIdList.Contains (pdbId))
                {
                    continue;
                }
                if (DoesEntryContainLigands(pdbId, boundLigands))
                {
                    pdbIdList.Add(pdbId);
                }
            }

            return pdbIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligands"></param>
        /// <returns></returns>
        private bool DoesEntryContainLigands (string pdbId, string[] ligands)
        {
            string queryString = string.Format("Select PdbId, ligand From PdbLigands Where PdbID = '{0}' AND Ligand In ({1});", pdbId, ParseHelper.FormatSqlListString (ligands));
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (ligandTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <param name="pymolScript"></param>
        /// <param name="newPmlScript"></param>
        private void WritePyMolScript (string[] pdbIds, string pymolScript, string newPmlScript)
        {
            string line = "";
            string domainDataLines = "";
            string dataLine = "";
            string pdbId = "";
            bool isCenterDomain = true;
            bool isEndLoad = false;
            StreamWriter dataWriter = new StreamWriter (newPmlScript);
            StreamReader dataReader = new StreamReader(pymolScript);
            line = dataReader.ReadLine();

            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line == "")
                {
                    if (domainDataLines != "")
                    {
                        if (isCenterDomain)
                        {
                            dataWriter.Write(domainDataLines);
                            isCenterDomain = false;
                        }
                        else
                        {
                            if (pdbIds.Contains (pdbId))
                            {
                                dataWriter.Write(domainDataLines);
                            }
                        }
                        domainDataLines = "";
                    }
                    continue;
                }
                if (line.IndexOf ("load") > -1)
                {
                    string[] fields = line.Split(" .".ToArray ());
                    pdbId = fields[1].Substring (0, 4);
                    domainDataLines += (line + "\r\n");
                }
                else if (line.IndexOf ("center") > -1)
                {
                    isEndLoad = true;
                    dataWriter.WriteLine(line);
                    line = dataReader.ReadLine();  // sele allhet, het
                    dataWriter.WriteLine(line);
                    line = dataReader.ReadLine();  // color white, allhet
                    dataWriter.WriteLine(line);
                    line = dataReader.ReadLine();   // util.cnc allhet
                    dataWriter.WriteLine(line);
                    line = dataReader.ReadLine();   // hide spheres, allhet
                    dataWriter.WriteLine(line); 
                }
                else if (line.IndexOf ("sele selectLigands") > -1)
                {
                    string[] fields = line.Split(",+".ToCharArray ());
                    dataLine = fields[0] + ", ";
                    for (int i = 1; i < fields.Length; i ++)
                    {
                        pdbId = fields[i].Substring(0, 4);
                        if (pdbIds.Contains (pdbId))
                        {
                            dataLine += (fields[i] + "+");
                        }
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd('+'));
                    line = dataReader.ReadLine();  // show spheres, selectLigands
                    dataWriter.WriteLine(line); 
                }
                else if (line.IndexOf("sele selectDnaRna") > -1)
                {
                    string[] fields = line.Split(",+".ToCharArray());
                    dataLine = fields[0] + ", ";
                    for (int i = 1; i < fields.Length; i++)
                    {
                        pdbId = fields[i].Substring(0, 4);
                        if (pdbIds.Contains(pdbId))
                        {
                            dataLine += (fields[i] + "+");
                        }
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd('+'));
                    line = dataReader.ReadLine();  // show cartoon, selectDnaRna
                    dataWriter.WriteLine(line);
                }
                else if (line.IndexOf("sele cluster_") > -1)
                {
                    continue;   // skip
      /*              string[] fields = line.Split(",+".ToCharArray());
                    dataLine = fields[0] + ", ";
                    for (int i = 1; i < fields.Length; i++)
                    {
                        pdbId = fields[i].Trim ().Substring(0, 4);
                        if (pdbIds.Contains(pdbId))
                        {
                            dataLine += (fields[i] + "+");
                        }
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd('+'));*/
                }
                else if (line.IndexOf("sele ") > -1)
                {
                    string[] fields = line.Split(",+".ToCharArray());
             //       dataLine = fields[0] + ", ";
                    dataLine = "";
                    for (int i = 1; i < fields.Length; i++)
                    {
                        pdbId = fields[i].Trim ().Substring(0, 4);
                        if (pdbIds.Contains(pdbId))
                        {
                            dataLine += (fields[i] + "+");
                        }
                    }
                    if (dataLine != "")
                    {
                        dataWriter.WriteLine(fields[0] + "," + dataLine.TrimEnd('+'));
                    }
                }
                else if (line.IndexOf ("set_name ") > -1)
                {
                    string[] fields = line.Split();
                    pdbId = fields[1].Substring(0, 4);
                    if (pdbIds.Contains (pdbId))
                    {
                        dataWriter.WriteLine(line);
                    }
                }
                else if (line.IndexOf ("order") > -1)
                {
                    string[] fields = line.Split();
                    dataLine = "order";
                    foreach (string field in fields)
                    {
                        string[] items = field.Split('.');
                        pdbId = items[items.Length - 1].Substring(0, 4);
                        if (pdbIds.Contains (pdbId))
                        {
                            dataLine += (" " + field);
                        }
                    }
                    dataWriter.WriteLine(dataLine);
                }
                else
                {
                    if (domainDataLines != "")
                    {
                        domainDataLines += (line + "\r\n");
                    }                   
                }
            }
            if (domainDataLines != "")
            {
                dataWriter.WriteLine(domainDataLines);
            }
            dataReader.Close();
            dataWriter.Close();
        }
        #endregion

        #region For paper supplementary excel file -- Ras, ErbB, Bromodomain-BD2, BRAF-like, 4HBT
        /// <summary>
        /// 
        /// </summary>
        public void PrintInterfaceClustersForPaper()
        {
            string[] pfams = {"Ras", "Pkinase_Tyr", "Bromodomain", "Pkinase_Tyr", "4HBT"};
            string[] Proteins = { "Ras", "ErbB", "Bromodomain_BD2", "BRAF-like", "4HBT" };
            int[] relSeqIds = {15457, 14511, 14511};
            int[] relClusterIds = {3, 7, 1};           
            string clusterDataFile = "";
            string[] columns = { "PDB", "Space Group", "CF", "Chains_symmetry", "SurfaceArea", "ASU", "PDBBA", "PISABA", "InAsu", "InPDB", "InPISA", "UniProts", "Resolution" };

      /*      int relSeqId = 15457;
            int clusterId = 3;
            clusterDataFile = Path.Combine(dataDir, "Ras_alphaDimers_cluster.txt");
            GetRelationDomainClusterEntryInterfaceInfo(relSeqId, clusterId, clusterDataFile);

            relSeqId = 14511;
            clusterId = 7;
            clusterDataFile = Path.Combine(dataDir, "ErbB_AsymDimers_cluster.txt");
            GetRelationDomainClusterEntryInterfaceInfo(relSeqId, clusterId, clusterDataFile);

            relSeqId = 14511;
            clusterId = 1;
            clusterDataFile = Path.Combine(dataDir, "BRAF-like_Dimers_cluster.txt");
            GetRelationDomainClusterEntryInterfaceInfo(relSeqId, clusterId, clusterDataFile);
*/
            int bromoChainGroupId = 8980;  // for Bromodomain-BD2            
            int bromoChainClusterId = 1;
            clusterDataFile = Path.Combine(dataDir, "Bromodomain-BD2_dimers_cluster.txt");
            GetGroupClusterEntryInterfaceInfo(bromoChainGroupId, bromoChainClusterId, clusterDataFile);
            
            int hbtChainGroupId = 737;
            int hbtClusterId = 1;
            clusterDataFile = Path.Combine(dataDir, "4HBT_oligomers_cluster.txt");
            Dictionary<string, List<int>> oligomerClusterListDict = new Dictionary<string, List<int>>();
            List<int> dimerClusterList = new List<int> ();
            dimerClusterList.Add(1);
            oligomerClusterListDict.Add("Dimer", dimerClusterList);

            List<int> tetramerClusterList1 = new List<int>();
            tetramerClusterList1.Add(1);
            tetramerClusterList1.Add(2);
            oligomerClusterListDict.Add("Tetramer1", tetramerClusterList1);

            List<int> tetramerClusterList2 = new List<int>();
            tetramerClusterList2.Add(1);
            tetramerClusterList2.Add(5);
            oligomerClusterListDict.Add("Tetramer2", tetramerClusterList2);


            List<int> hexamerClusterList = new List<int>();
            hexamerClusterList.Add(1);
            hexamerClusterList.Add(6);
            oligomerClusterListDict.Add("Hexamer", dimerClusterList);

            GetHBTChainClusterEntryInterfaceInfo(hbtChainGroupId, hbtClusterId, oligomerClusterListDict, clusterDataFile);
        }

        #region domain cluster
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArch"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public void GetRelationDomainClusterEntryInterfaceInfo(int relSeqId, int clusterId, string dataFile)
        {
            StreamWriter dataWriter = new StreamWriter(dataFile);
            dataWriter.WriteLine("PDB\tSpace Group\tCF\tChains:SymmetryOperators\tSurface Area\tASU\tPDBBA\tPISABA\tInAsu\tInPDB\tInPISA\tUniProts\tResolution\tLigands\tMutations");
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterEntryInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string entry = "";
            foreach (DataRow dataRow in clusterEntryInterfaceTable.Rows)
            {
                entry = dataRow["PdbID"].ToString();
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            entryList.Sort();
            string dataLine = "";
            string authChainsSymOp = "";
            string resolution = "";
            string[] ligands = null;
            string[] mutations = null;
            foreach (string pdbId in entryList)
            {
                resolution = GetEntryResolution(pdbId);
                ligands = GetEntryLigands(pdbId);
                mutations = GetEntryMutations(pdbId);
                DataRow[] entryInterfaceRows = clusterEntryInterfaceTable.Select(string.Format("PdbID = '{0}'", pdbId), "DomainInterfaceID ASC");
               
                authChainsSymOp =  GetDomainInterfaceAuthorChains(pdbId, Convert.ToInt32(entryInterfaceRows[0]["DomainInterfaceID"].ToString()));
                dataLine = pdbId + "\t" + entryInterfaceRows[0]["SpaceGroup"] + "\t" + entryInterfaceRows[0]["RelCfGroupID"].ToString() + "\t" +
                    authChainsSymOp + "\t" +
                    (int)(Convert.ToDouble(entryInterfaceRows[0]["SurfaceArea"].ToString())) + "\t" +
                    entryInterfaceRows[0]["ASU"].ToString() + "\t" + entryInterfaceRows[0]["PdbBU"].ToString() + "\t" + entryInterfaceRows[0]["PisaBU"].ToString() + "\t" +
                    entryInterfaceRows[0]["InAsu"].ToString() + "\t" + entryInterfaceRows[0]["InPdb"].ToString() + "\t" + entryInterfaceRows[0]["InPisa"].ToString() + "\t" +                    
                     entryInterfaceRows[0]["UnpCode"] + "\t" + resolution + "\t" + 
                     ParseHelper.FormatArrayString (ligands, ',') + "\t" + ParseHelper.FormatArrayString (mutations, ';');
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }
        #endregion

        #region chain cluster
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArch"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public void GetGroupClusterEntryInterfaceInfo(int groupSeqId, int clusterId, string dataFile)
        {
            StreamWriter dataWriter = new StreamWriter(dataFile);
            dataWriter.WriteLine("PDB\tSpace Group\tCF\tChains:SymmetryOperators\tSurface Area\tASU\tPDBBA\tPISABA\tInAsu\tInPDB\tInPISA\tUniProts\tResolution\tLigands\tMutations");          
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1};", groupSeqId, clusterId);
            DataTable clusterEntryInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string entry = "";
            foreach (DataRow dataRow in clusterEntryInterfaceTable.Rows)
            {
                entry = dataRow["PdbID"].ToString();
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            entryList.Sort();
            string dataLine = "";
            string authorChains = "";
            string[] ligands = null;
            string[] mutations = null;
            string resolution = "";
            foreach (string pdbId in entryList)
            {
                ligands = GetEntryLigands(pdbId);
                mutations = GetEntryMutations(pdbId);
                resolution = GetEntryResolution (pdbId);

                DataRow[] entryInterfaceRows = clusterEntryInterfaceTable.Select(string.Format("PdbID = '{0}'", pdbId), "InterfaceID ASC");
                authorChains = GetInterfaceAuthorChains(pdbId, Convert.ToInt32(entryInterfaceRows[0]["InterfaceID"].ToString ()));
                dataLine = pdbId + "\t" + entryInterfaceRows[0]["SpaceGroup"] + "\t" + entryInterfaceRows[0]["CfGroupID"].ToString() + "\t" +
                    authorChains + "\t" + 
                    (int)(Convert.ToDouble(entryInterfaceRows[0]["SurfaceArea"].ToString())) + "\t" +
                     entryInterfaceRows[0]["ASU"] + "\t" + entryInterfaceRows[0]["PDBBU"] + "\t" + entryInterfaceRows[0]["PISABU"] + "\t" +
                     entryInterfaceRows[0]["InASU"] + "\t" + entryInterfaceRows[0]["InPdb"] + "\t" + entryInterfaceRows[0]["InPisa"] + "\t" +
                     entryInterfaceRows[0]["UnpCode"] + "\t" + resolution + "\t" +
                     ParseHelper.FormatArrayString (ligands, ',') + "\t" + ParseHelper.FormatArrayString (mutations, ';');
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="dataFile"></param>
        public void GetHBTChainClusterEntryInterfaceInfo (int groupSeqId,  int clusterId, Dictionary<string, List<int>> oligomerClusterListDict, string dataFile)
        {
            StreamWriter dataWriter = new StreamWriter(dataFile);
            dataWriter.WriteLine("PDB\tSpace Group\tCF\tChains:SymmetryOperators\tSurface Area\tASU\tPDBBA\tPISABA\tInAsu\tInPDB\tInPISA\tUniProts\tResolution\tOligomer\tClusters\tLigands\tMutations");
           
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1};", groupSeqId, clusterId);
            DataTable clusterEntryInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            string entry = "";
            foreach (DataRow dataRow in clusterEntryInterfaceTable.Rows)
            {
                entry = dataRow["PdbID"].ToString();
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            entryList.Sort();
            string dataLine = "";
            string authorChains = "";
            string oligomerType = "";
            string entryClusters = "";
            string[] ligands = null;
            string[] mutations = null;
            string resolution = "";
            foreach (string pdbId in entryList)
            {
                ligands = GetEntryLigands(pdbId);
                mutations = GetEntryMutations(pdbId);
                resolution = GetEntryResolution(pdbId);
                DataRow[] entryInterfaceRows = clusterEntryInterfaceTable.Select(string.Format("PdbID = '{0}'", pdbId), "InterfaceID ASC");
                authorChains = GetInterfaceAuthorChains(pdbId, Convert.ToInt32(entryInterfaceRows[0]["InterfaceID"].ToString()));
                oligomerType = GetOligomerType(pdbId, groupSeqId, oligomerClusterListDict);
                entryClusters = GetEntryClusters(pdbId, groupSeqId);
                dataLine = pdbId + "\t" + entryInterfaceRows[0]["SpaceGroup"] + "\t" + entryInterfaceRows[0]["CfGroupID"].ToString() + "\t" +
                    authorChains + "\t" +
                    (int)(Convert.ToDouble(entryInterfaceRows[0]["SurfaceArea"].ToString())) + "\t" +
                     entryInterfaceRows[0]["ASU"] + "\t" + entryInterfaceRows[0]["PDBBU"] + "\t" + entryInterfaceRows[0]["PISABU"] + "\t" +
                     entryInterfaceRows[0]["InASU"] + "\t" + entryInterfaceRows[0]["InPdb"] + "\t" + entryInterfaceRows[0]["InPisa"] + "\t" +
                     entryInterfaceRows[0]["UnpCode"] + "\t" + resolution + "\t" + oligomerType + "\t" + entryClusters + "\t" + 
                     ParseHelper.FormatArrayString (ligands, ',') + "\t" + ParseHelper.FormatArrayString (mutations, ';');
                dataWriter.WriteLine(dataLine); 
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainGroupId"></param>
        /// <returns></returns>
        private string GetOligomerType (string pdbId, int chainGroupId, Dictionary<string, List<int>> oligomerClusterListDict)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} AND PdbID = '{1}';", chainGroupId, pdbId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int clusterId = 0;
            List<int> entryClusterList = new List<int>();
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                entryClusterList.Add(clusterId);
            }
            string entryOligomer = "";
            foreach (string oligomer in oligomerClusterListDict.Keys)
            {
                if (oligomer == "Dimer")
                {
                    continue;
                }
                entryOligomer = oligomer;
                foreach (int oligomerCluster in oligomerClusterListDict[oligomer])
                {
                    if (! entryClusterList.Contains (oligomerCluster))
                    {
                        entryOligomer = "";
                        break;
                    }
                }
                if (entryOligomer != "")
                {
                    break;
                }
            }
            if (entryOligomer == "")
            {
                entryOligomer = "Dimer";
            }
            return entryOligomer;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string GetEntryClusters(string pdbId, int chainGroupId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperClusterEntryInterfaces " +
                " Where SuperGroupSeqID = {0} AND PdbID = '{1}';", chainGroupId, pdbId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int clusterId = 0;
            string clusters = "Cluster";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
               if (clusterId <= 7)
               {
                   clusters += (clusterId + "-");
               }
            }
            return clusters.TrimEnd('-');
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryResolution (string pdbId)
        {
            string queryString = string.Format("Select Method, Resolution From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable resolutionTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string resolution = "";
            if (resolutionTable.Rows.Count > 0)
            {
                resolution = resolutionTable.Rows[0]["Resolution"].ToString();
                double doubleResolution = Convert.ToDouble(resolutionTable.Rows[0]["Resolution"].ToString());
                if (doubleResolution == 0)
                {
                    resolution = resolutionTable.Rows[0]["Method"].ToString().TrimEnd();
                }
            }
            return resolution;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="protcidDbConnect"></param>
        /// <returns></returns>
        public string GetDomainInterfaceAuthorChains(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select InterfaceID, AsymChain1, AsymChain2 From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable interfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int interfaceId = -1;
            if (interfaceIdTable.Rows.Count > 0)
            {
                interfaceId = Convert.ToInt32(interfaceIdTable.Rows[0]["InterfaceID"].ToString());
            }
            string authorChains = "";
            if (interfaceId > 0)
            {
                authorChains = GetInterfaceAuthorChains(pdbId, interfaceId);
            }
            else
            {
                string asymChain = interfaceIdTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
                string authorChain = GetAuthorChain(pdbId, asymChain);
                authorChains = authorChain + "(1_555):" + authorChain + "(1_555)";
            }
            return authorChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetAuthorChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select AuthorChain From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable authChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string authChain = "";
            if (authChainTable.Rows.Count > 0)
            {
                authChain = authChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
            }
            return authChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="protcidDbConnect"></param>
        /// <returns></returns>
        public string GetInterfaceAuthorChains(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string authorChains = "";
            if (entryInterfaceTable.Rows.Count > 0)
            {
                string symmetryString1 = entryInterfaceTable.Rows[0]["SymmetryString1"].ToString().TrimEnd();
                string symmetryString2 = entryInterfaceTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
                string authChain1 = entryInterfaceTable.Rows[0]["AuthChain1"].ToString().TrimEnd();
                string authChain2 = entryInterfaceTable.Rows[0]["AuthChain2"].ToString().TrimEnd();
                authorChains = authChain1 + "(" + symmetryString1 + "):" + authChain2 + "(" + symmetryString2 + ")";
            }
            return authorChains;
        }
        #endregion
    }
}
