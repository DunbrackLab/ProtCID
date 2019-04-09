using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainInterfaceStatInfo
    {
        DbQuery dbQuery = new DbQuery();

        #region domain cluster info
        public void GetDomainClusterInfo()
        {
            StreamWriter dataWriter = new StreamWriter("DomainInterfaceClusterInfo.txt", true);
            GetIntraChainDomainClusterInfo(dataWriter);
            GetInterChainDomainClusterInfo(dataWriter);
            dataWriter.Close();
        }

        public void FindClustersOnlyInIntraMulti()
        {
            StreamWriter dataWriter = new StreamWriter("RelationsOnlyIntraMulti1.txt");
            Dictionary<int, int[]>[] relClusterIdsHashes = ReadClusterIdsHashes();
            int numOfRelationsIntraOnly = 0;
            int numOfClustersIntraOnly = 0;
            int numOfRelationsMultiOnly = 0;
            int numOfClustersMultiOnly = 0;
            dataWriter.WriteLine("Intra-Chain");
            string[] familyCodes = null;
            foreach (int relSeqId in relClusterIdsHashes[0].Keys)
            {
                int[] clusterIds = relClusterIdsHashes[0][relSeqId];
                if (!relClusterIdsHashes[2].ContainsKey(relSeqId) && !relClusterIdsHashes[1].ContainsKey(relSeqId))
                {
                    numOfRelationsIntraOnly++;
                    numOfClustersIntraOnly += clusterIds.Length;
                    familyCodes = GetDomainRelation(relSeqId);
                    dataWriter.WriteLine(relSeqId.ToString() + "\t" + familyCodes[0] + "\t" + familyCodes[1]);
                }
                else
                {
                    if (relClusterIdsHashes[2].ContainsKey(relSeqId))
                    {
                        int[] singleClusterIds = relClusterIdsHashes[2][relSeqId];
                        clusterIds = GetClusterIdsNotIn(clusterIds, singleClusterIds);
                    }
                    if (relClusterIdsHashes[1].ContainsKey(relSeqId))
                    {
                        int[] multiClusterIds = relClusterIdsHashes[1][relSeqId];
                        clusterIds = GetClusterIdsNotIn(clusterIds, multiClusterIds);
                    }
                    numOfClustersIntraOnly += clusterIds.Length;
                }
            }
            dataWriter.WriteLine("Multi-Chain");
            foreach (int relSeqId in relClusterIdsHashes[1].Keys)
            {
                int[] clusterIds = relClusterIdsHashes[1][relSeqId];
                if (!relClusterIdsHashes[2].ContainsKey(relSeqId) && !relClusterIdsHashes[0].ContainsKey(relSeqId))
                {
                    numOfRelationsMultiOnly++;
                    numOfClustersMultiOnly += clusterIds.Length;
                    familyCodes = GetDomainRelation(relSeqId);
                    dataWriter.WriteLine(relSeqId.ToString() + "\t" + familyCodes[0] + "\t" + familyCodes[1]);
                }
                else
                {
                    if (relClusterIdsHashes[2].ContainsKey(relSeqId))
                    {
                        int[] singleClusterIds = relClusterIdsHashes[2][relSeqId];
                        clusterIds = GetClusterIdsNotIn(clusterIds, singleClusterIds);
                    }
                    if (relClusterIdsHashes[0].ContainsKey(relSeqId))
                    {
                        int[] intraClusterIds = relClusterIdsHashes[0][relSeqId];
                        clusterIds = GetClusterIdsNotIn(clusterIds, intraClusterIds);
                    }
                    numOfClustersMultiOnly += clusterIds.Length;
                }
            }
            dataWriter.Close();
        }

        private int[] GetClusterIdsNotIn(int[] clusterIds1, int[] clusterIds2)
        {
            List<int> clusterIdList = new List<int> ();
            foreach (int clusterId in clusterIds1)
            {
                if (clusterIds2.Contains(clusterId))
                {
                    continue;
                }
                clusterIdList.Add(clusterId);
            }
            int[] notInClusterIds = new int[clusterIdList.Count];
            clusterIdList.CopyTo(notInClusterIds);
            return notInClusterIds;
        }

        private string[] GetDomainRelation(int relSeqId)
        {
            string querystring = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable familyCodesTable = ProtCidSettings.protcidQuery.Query( querystring);
            string[] familyCodes = new string[2];
            familyCodes[0] = familyCodesTable.Rows[0]["FamilyCOde1"].ToString().TrimEnd();
            familyCodes[1] = familyCodesTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return familyCodes;
        }
        private Dictionary<int, int[]>[] ReadClusterIdsHashes()
        {
            StreamReader dataReader = new StreamReader("DomainInterfaceClusterInfo0.txt");
            string line = "";
            bool isIntra = false;
            bool isMulti = false;
            bool isSingle = false;
            Dictionary<int, int[]> intraClusterHash = new Dictionary<int,int[]> ();
            Dictionary<int, int[]> multiClusterHash = new Dictionary<int, int[]>();
            Dictionary<int, int[]> singleClusterHash = new Dictionary<int, int[]>();
            int relSeqId = 0;
            int[] clusterIds = null;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Intra-chain") > -1)
                {
                    isIntra = true;
                    continue;
                }
                if (line.IndexOf("Multi-domains") > -1)
                {
                    isIntra = false;
                    isMulti = true;
                    continue;
                }
                if (line.IndexOf("Single-domains") > -1)
                {
                    isIntra = false;
                    isMulti = false;
                    isSingle = true;
                    continue;
                }
                if (line == "" || line.Substring(0, 1) == "#")
                {
                    continue;
                }
                string[] fields = line.Split('\t');
                relSeqId = Convert.ToInt32(fields[0]);
                clusterIds = new int[fields.Length - 1];
                for (int i = 1; i < fields.Length; i++)
                {
                    clusterIds[i - 1] = Convert.ToInt32(fields[i]);
                }
                if (isIntra)
                {
                    intraClusterHash.Add(relSeqId, clusterIds);
                }
                if (isMulti)
                {
                    multiClusterHash.Add(relSeqId, clusterIds);
                }
                if (isSingle)
                {
                    singleClusterHash.Add(relSeqId, clusterIds);
                }
            }
            dataReader.Close();
            Dictionary<int, int[]>[] relClustersHashes = new Dictionary<int, int[]>[3];
            relClustersHashes[0] = intraClusterHash;
            relClustersHashes[1] = multiClusterHash;
            relClustersHashes[2] = singleClusterHash;
            return relClustersHashes;
        }


        public void GetIntraChainDomainClusterInfo(StreamWriter dataWriter)
        {
            string queryString = "Select RelSeqID, PdbID, DomainInterfaceID From PfamDomainInterfaces Where InterfaceID = 0;";
            DataTable intraDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = "Select Distinct RelSeqID From PfamDomainInterfaces Where InterfaceID = 0;";
            DataTable intraChainRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            Dictionary<int, int[]> relationClusterIdsHash = new Dictionary<int,int[]> ();
            int numOfClusters = 0;
            int numOfRelations = 0;
            int numOfDomainInterfacesInCluster = 0;
            int numOfDomainInterfaces = 0;
            string[] entriesInClusters = null;
            List<string> entryInClusterList = new List<string> ();
            foreach (DataRow relSeqIdRow in intraChainRelSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                DataRow[] relationDomainInterfaceRows = intraDomainInterfaceTable.Select(string.Format("RelSeqID = '{0}'", relSeqId));
                int[] relationClusterIds = GetClustersWithInDomainInterfaces(relSeqId, relationDomainInterfaceRows,
                    out numOfDomainInterfaces, out entriesInClusters);
                if (relationClusterIds.Length > 0)
                {
                    relationClusterIdsHash.Add(relSeqId, relationClusterIds);
                    numOfRelations++;
                    numOfClusters += relationClusterIds.Length;
                    numOfDomainInterfacesInCluster += numOfDomainInterfaces;
                    foreach (string pdbId in entriesInClusters)
                    {
                        if (!entryInClusterList.Contains(pdbId))
                        {
                            entryInClusterList.Add(pdbId);
                        }
                    }
                }
            }
            dataWriter.WriteLine("Intra-chain");
            dataWriter.WriteLine("#Domain Interfaces in clusters: " + numOfDomainInterfacesInCluster.ToString());
            dataWriter.WriteLine("#Entries in clusters: " + entryInClusterList.Count.ToString());
            dataWriter.WriteLine("#relations with clusters: " + numOfRelations.ToString());
            dataWriter.WriteLine("#Clusters: " + numOfClusters.ToString());
            WriteHashInfoToFile(relationClusterIdsHash, dataWriter);
        }

        public void GetInterChainDomainClusterInfo(StreamWriter dataWriter)
        {
            Dictionary<int, int[]> singleRelationClusterHash = new Dictionary<int,int[]> ();
            Dictionary<int, int[]> multiRelationClusterHash = new Dictionary<int,int[]> ();
            string queryString = "Select Distinct RelSeqID From PfamDomainInterfaces Where InterfaceID > 0;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            int numOfSingleRelSeqIdsAll = 0;
            int numOfSingleInterfacesAll = 0;
            List<string> singleEntryListAll = new List<string>();
            int numOfSingleRelSeqIds = 0;
            int numOfSingleInterfaces = 0;
            List<string> singleEntryList = new List<string>();
            int numOfSingleClusters = 0;

            int numOfMultiRelSeqIdsAll = 0;
            int numOfMultiInterfacesAll = 0;
            List<string> multEntryListAll = new List<string>();
            int numOfMultiRelSeqIds = 0;
            int numOfMultiInterfaces = 0;
            List<string> multEntryList = new List<string>();
            int numOfMultiClusters = 0;

            string pdbId = "";
            int numOfDomainInterfacesInCluster = 0;
            string[] entriesInClusters = null;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                DataRow[][] domainInterfaceRows = GetSingleMultiDomainInterfaces(relSeqId);

                numOfMultiInterfacesAll += domainInterfaceRows[0].Length;

                foreach (DataRow multiDomainInterfaceRow in domainInterfaceRows[0])
                {
                    pdbId = multiDomainInterfaceRow["PdbID"].ToString();
                    if (!multEntryListAll.Contains(pdbId))
                    {
                        multEntryListAll.Add(pdbId);
                    }
                }
                if (domainInterfaceRows[0].Length > 0)
                {
                    numOfMultiRelSeqIdsAll++;
                }

                int[] multiClusterIds = GetClustersWithInDomainInterfaces(relSeqId, domainInterfaceRows[0],
                    out numOfDomainInterfacesInCluster, out entriesInClusters);
                if (multiClusterIds.Length > 0)
                {
                    multiRelationClusterHash.Add(relSeqId, multiClusterIds);
                    numOfMultiInterfaces += numOfDomainInterfacesInCluster;
                    foreach (string multiPdbId in entriesInClusters)
                    {
                        if (!multEntryList.Contains(multiPdbId))
                        {
                            multEntryList.Add(multiPdbId);
                        }
                    }
                    numOfMultiRelSeqIds++;
                    numOfMultiClusters += multiClusterIds.Length;
                }

                numOfSingleInterfacesAll += domainInterfaceRows[1].Length;
                if (domainInterfaceRows[1].Length > 0)
                {
                    numOfSingleRelSeqIdsAll++;
                }
                foreach (DataRow domainInterfaceRow in domainInterfaceRows[1])
                {
                    pdbId = domainInterfaceRow["PdbID"].ToString();
                    if (!singleEntryListAll.Contains(pdbId))
                    {
                        singleEntryListAll.Add(pdbId);
                    }
                }
                int[] singleClusterIds = GetClustersWithInDomainInterfaces(relSeqId, domainInterfaceRows[1],
                   out numOfDomainInterfacesInCluster, out entriesInClusters);
                if (singleClusterIds.Length > 0)
                {
                    numOfSingleInterfaces += numOfDomainInterfacesInCluster;
                    numOfSingleRelSeqIds++;
                    foreach (string singlePdbId in entriesInClusters)
                    {
                        if (!singleEntryList.Contains(singlePdbId))
                        {
                            singleEntryList.Add(singlePdbId);
                        }
                    }
                    singleRelationClusterHash.Add(relSeqId, singleClusterIds);
                    numOfSingleClusters += singleClusterIds.Length;
                }


            }
            dataWriter.WriteLine("Multi-domains");
            dataWriter.WriteLine("#multi-domain interfaces: " + numOfMultiInterfacesAll.ToString());
            dataWriter.WriteLine("#multi-domain entries: " + multEntryListAll.Count.ToString());
            dataWriter.WriteLine("#multi-domain relations: " + numOfMultiRelSeqIdsAll.ToString());
            dataWriter.WriteLine("#multi-domain interfaces in clusters: " + numOfMultiInterfaces.ToString());
            dataWriter.WriteLine("#multi-domain entries in clusters: " + multEntryList.Count.ToString());
            dataWriter.WriteLine("#multi-domain relations in clusters: " + numOfMultiRelSeqIds.ToString());
            dataWriter.WriteLine("#multi-domain clusters: " + numOfMultiClusters.ToString());
            WriteHashInfoToFile(multiRelationClusterHash, dataWriter);
            dataWriter.WriteLine("Single-domains");
            dataWriter.WriteLine("#single-domain interfaces: " + numOfSingleInterfacesAll.ToString());
            dataWriter.WriteLine("#single-domain entries: " + singleEntryListAll.Count.ToString());
            dataWriter.WriteLine("#single-domain relations: " + numOfSingleRelSeqIdsAll.ToString());
            dataWriter.WriteLine("#single-domain interfaces in clusters: " + numOfSingleInterfaces.ToString());
            dataWriter.WriteLine("#single-domain entries in clusters: " + singleEntryList.Count.ToString());
            dataWriter.WriteLine("#single-domain relations in clusters: " + numOfSingleRelSeqIds.ToString());
            dataWriter.WriteLine("#single-domain clusters: " + numOfSingleClusters.ToString());
            WriteHashInfoToFile(singleRelationClusterHash, dataWriter);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationClusterInfoHash"></param>
        /// <param name="dataWriter"></param>
        private void WriteHashInfoToFile(Dictionary<int, int[]> relationClusterInfoHash, StreamWriter dataWriter)
        {
            List<int> relSeqIdList = new List<int> (relationClusterInfoHash.Keys);
            relSeqIdList.Sort();
            string dataLine = "";
            foreach (int relSeqId in relSeqIdList)
            {
                int[] clusterIds = (int[])relationClusterInfoHash[relSeqId];
                dataLine = relSeqId.ToString();
                foreach (int clusterId in clusterIds)
                {
                    dataLine += ("\t" + clusterId.ToString());
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.WriteLine();
            dataWriter.Flush();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataRow[][] GetSingleMultiDomainInterfaces(int relSeqId)
        {
            string querystring = string.Format("Select PdbId, InterfaceId, Count(Distinct DomainInterfaceId) As DomainInterfaceCount " +
                " From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND InterfaceID > 0 Group By PdbId, InterfaceId;", relSeqId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( querystring);
            int domainInterfaceCount = 0;
            List<DataRow> multiDomainInterfaceList = new List<DataRow> ();
            List<DataRow> singleDomainInterfaceList = new List<DataRow>();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                domainInterfaceCount = Convert.ToInt32(interfaceRow["DomainInterfaceCount"].ToString());
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                DataRow[] domainInterfaceRows = GetInterfaceDomainInterfaces(pdbId, interfaceId, relSeqId);
                if (domainInterfaceCount > 1)
                {
                    multiDomainInterfaceList.AddRange(domainInterfaceRows);
                }
                else
                {
                    singleDomainInterfaceList.AddRange(domainInterfaceRows);
                }
            }
            DataRow[][] singleMultiDomainInterfaceRows = new DataRow[2][];
            singleMultiDomainInterfaceRows[0] = new DataRow[multiDomainInterfaceList.Count];
            multiDomainInterfaceList.CopyTo(singleMultiDomainInterfaceRows[0]);
            singleMultiDomainInterfaceRows[1] = new DataRow[singleDomainInterfaceList.Count];
            singleDomainInterfaceList.CopyTo(singleMultiDomainInterfaceRows[1]);
            return singleMultiDomainInterfaceRows;
        }

        private DataRow[] GetInterfaceDomainInterfaces(string pdbId, int interfaceId, int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0} AND PdbID = '{1}' AND InterfaceID = {2};",
                relSeqId, pdbId, interfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable.Select();
        }

        private int[] GetClustersWithInDomainInterfaces(int relSeqId, DataRow[] domainInterfaceRows,
            out int numOfDomainInterfacesInCluster, out string[] entriesInCluster)
        {
            List<int> clusterIdList = new List<int> ();
            int clusterId = -1;
            string pdbId = "";
            int domainInterfaceId = 0;
            numOfDomainInterfacesInCluster = 0;
            List<string> entryInClusterList = new List<string> ();
            foreach (DataRow domainInterfaceRow in domainInterfaceRows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                clusterId = GetClusterIdFromInDomainInterface(pdbId, domainInterfaceId, relSeqId);
                if (clusterId > -1)
                {
                    if (!clusterIdList.Contains(clusterId))
                    {
                        clusterIdList.Add(clusterId);
                    }
                    numOfDomainInterfacesInCluster++;
                    if (!entryInClusterList.Contains(pdbId))
                    {
                        entryInClusterList.Add(pdbId);
                    }
                }
            }
            entriesInCluster = new string[entryInClusterList.Count];
            entryInClusterList.CopyTo(entriesInCluster);

            return clusterIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int GetClusterIdFromInDomainInterface(string pdbId, int domainInterfaceId, int relSeqId)
        {
            string queryString = string.Format("Select ClusterID From PfamDomainClusterInterfaces Where RelSeqId = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2};",
                relSeqId, pdbId, domainInterfaceId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = -1;
            if (clusterInterfaceTable.Rows.Count > 0)
            {
                clusterId = Convert.ToInt32(clusterInterfaceTable.Rows[0]["ClusterID"].ToString());
            }
            return clusterId;
        }
        #endregion

        #region Pfam-peptide interface data
        public void PrintPepLigandHmmSites()
        {
            string pfamId = "cNMP_binding";
            string ligand = "CMP";

            string queryString = string.Format("Select Distinct PdbID, Ligand, AsymChain, SeqID From PdbLigands WHere Ligand = '{0}';", ligand);
            DataTable ligandChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string pdbId = "";
            string asymChain = "";
            int seqId = 0;
            Dictionary<int, List<string>> hmmEntryHash = new Dictionary<int,List<string>> ();
            List<string> hmmEntryList = new List<string> ();
            Dictionary<int, double> hmmSumDistHash = new Dictionary<int,double> ();
            Dictionary<int, int> hmmCountHash = new Dictionary<int,int> ();
            foreach (DataRow ligandChainRow in ligandChainTable.Rows)
            {
                pdbId = ligandChainRow["PdbID"].ToString();
                asymChain = ligandChainRow["AsymChain"].ToString ().TrimEnd ();
                seqId = Convert.ToInt32(ligandChainRow["SeqID"].ToString ());

                if (!hmmEntryList.Contains(pdbId))
                {
                    hmmEntryList.Add(pdbId);
                }

          //      int[] interactingHmmSites = GetLigandInteractingHmmSites(pfamId, pdbId, asymChain, seqId);
                Dictionary<int, double> hmmDistHash = GetLigandInteractingHmmSites(pfamId, pdbId, asymChain, seqId);

                foreach (int hmmSite in hmmDistHash.Keys)
                {
                    if (hmmEntryHash.ContainsKey(hmmSite))
                    {
                        if (!hmmEntryHash[hmmSite].Contains(pdbId))
                        {
                            hmmEntryHash[hmmSite].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        hmmEntryHash.Add(hmmSite, entryList);
                    }
                    if (hmmSumDistHash.ContainsKey(hmmSite))
                    {
                        double sumDist = (double)hmmSumDistHash[hmmSite];
                        sumDist += (double)hmmDistHash[hmmSite];
                        hmmSumDistHash[hmmSite] = sumDist;

                        int count = (int)hmmCountHash[hmmSite];
                        count++;
                        hmmCountHash[hmmSite] = count;
                    }
                    else
                    {
                        hmmSumDistHash.Add(hmmSite, hmmDistHash[hmmSite]);
                        hmmCountHash.Add(hmmSite, 1);
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(ligand + "HmmSites.txt");
            List<int> hmmSiteList = new List<int> (hmmEntryHash.Keys);
            hmmSiteList.Sort();
            foreach (int hmmSite in hmmSiteList)
            {
                List<string> entryList =  hmmEntryHash[hmmSite];
                double hmmSumDist = (double)hmmSumDistHash[hmmSite];
                int hmmCount = (int)hmmCountHash[hmmSite];
                double avgDist = hmmSumDist / (double)hmmCount;
                dataWriter.WriteLine(hmmSite + "\t" + entryList.Count.ToString () + "\t" + FormatArrayString (entryList) + "\t" + avgDist.ToString ());
            }
            dataWriter.WriteLine("Total Pfam-ligand entries: " + hmmEntryList.Count.ToString ());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandChain"></param>
        /// <param name="seqId"></param>
        /// <returns></returns>
        private Dictionary<int, double> GetLigandInteractingHmmSites(string pfamId, string pdbId, string ligandChain, int ligandSeqId)
        {
            string queryString = string.Format("Select * From PfamLigands Where PfamId = '{0}' AND PdbID = '{1}' AND LigandChain = '{2}' AND LigandSeqID = {3};", 
                pfamId, pdbId, ligandChain, ligandSeqId);
            DataTable ligandHmmSiteTable = ProtCidSettings.pdbfamQuery.Query( queryString);
     //       ArrayList hmmSeqIdList = new ArrayList();
            Dictionary<int, double> hmmDistHash = new Dictionary<int,double> ();
            int hmmSeqId = 0;
            string protChain = "";
            int protSeqId = 0;
            double minDistance = 0;
            foreach (DataRow hmmSiteRow in ligandHmmSiteTable.Rows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString ());
             /*   if (!hmmSeqIdList.Contains(hmmSeqId))
                {
                    hmmSeqIdList.Add(hmmSeqId);
                }*/
                protChain = hmmSiteRow["AsymCHain"].ToString().TrimEnd();
                protSeqId = Convert.ToInt32(hmmSiteRow["SeqID"].ToString ());

                minDistance = GetMinimumAtomDistance(pdbId, ligandChain, ligandSeqId, protChain, protSeqId);
                if (hmmDistHash.ContainsKey(hmmSeqId))
                {
                    double hmmDist = (double)hmmDistHash[hmmSeqId];
                    if (hmmDist > minDistance)
                    {
                        hmmDistHash[hmmSeqId] = minDistance;
                    }
                }
                else
                {
                    hmmDistHash.Add(hmmSeqId, minDistance);
                }
            }
            return hmmDistHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandChain"></param>
        /// <param name="ligandSeqId"></param>
        /// <param name="protChain"></param>
        /// <param name="protSeqId"></param>
        /// <returns></returns>
        private double GetMinimumAtomDistance(string pdbId, string ligandChain, int ligandSeqId, string protChain, int protSeqId)
        {
            string queryString = string.Format("Select Distance From ChainLigands Where PdbID = '{0}' AND ChainAsymID = '{1}' AND ChainSeqID = {2} AND " +
                " AsymID = '{3}' AND SeqID = {4};", pdbId, protChain, protSeqId, ligandChain, ligandSeqId);
            DataTable distanceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            double minDistance = 10.0;
            double distance = 0;
            foreach (DataRow distRow in distanceTable.Rows)
            {
                distance = Convert.ToDouble(distRow["Distance"].ToString ());
                if (minDistance > distance)
                {
                    minDistance = distance;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintPepInteractingHmmSites()
        {
            string pfamId = "SH2";
            string protResidue = "ARG";
            int clusterId = 1;
            string[] pepResidues = { "PTH", "PTR", "PM3", "AY0", "1PA", "FTY", "TYR"};
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\protcid_update\data\" + pfamId + clusterId.ToString () + "_PepHmmMapping_Arg.txt");
            dataWriter.WriteLine("PdbID\tDomainInterfaceID\tInterfaceID\tPepResidue\tResidues\tSeqIDs\tHmmSites\tUnpSeqIDs\tDistances");

            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamPepInterfaceClusters " + 
                " Where PfamID = '{0}' AND ClusterId = {1};", pfamId, clusterId);
            DataTable clusterDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            Dictionary<int, List<string>> hmmSiteNumEntryHash = new Dictionary<int,List<string>> ();
            Dictionary<int, List<string>> hmmSitePepResidueHash = new Dictionary<int,List<string>>  ();
            Dictionary<int, double> hmmSitePepResidueSumDistHash = new Dictionary<int,double> ();
            Dictionary<int, int> hmmSitePepResidueCountHash = new Dictionary<int,int> ();
            Dictionary<int, double> hmmSitePepResidueMinDistHash = new Dictionary<int,double>  ();
            List<string> entryList = new List<string> ();
            List<string> noHmmSiteEntryList = new List<string> ();
            foreach (DataRow dInterfaceRow in clusterDomainInterfaceTable.Rows)
            {
                pdbId = dInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(dInterfaceRow["DomainInterfaceID"].ToString ());

                DataTable chainPepAtomPairsTable = GetChainPepAtomPairsTable(pdbId);

         /*       MatchPepResiduesToHmmSites (pdbId, domainInterfaceId, pepResidues, chainPepAtomPairsTable, dataWriter, 
                    ref hmmSiteNumEntryHash, ref hmmSitePepResidueHash, ref hmmSitePepResidueSumDistHash, ref hmmSitePepResidueCountHash, 
                    ref hmmSitePepResidueMinDistHash);*/
                MatchPepResiduesToHmmSites(pdbId, domainInterfaceId, pepResidues, protResidue, chainPepAtomPairsTable, dataWriter,
                    ref hmmSiteNumEntryHash, ref hmmSitePepResidueHash, ref hmmSitePepResidueSumDistHash, ref hmmSitePepResidueCountHash,
                    ref hmmSitePepResidueMinDistHash);
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            string dataLine = "";
            foreach (int hmmSite in hmmSiteNumEntryHash.Keys)
            {
                List<string> hmmEntryList = hmmSiteNumEntryHash[hmmSite];
                noHmmSiteEntryList.Clear();
                if (hmmEntryList.Count > 50)
                {
                    foreach (string lsPdbId in entryList)
                    {
                        if (! hmmEntryList.Contains(lsPdbId))
                        {
                            noHmmSiteEntryList.Add(lsPdbId);
                        }
                    }
                }
                dataLine = hmmSite.ToString() + "\t" + FormatArrayString(hmmEntryList) + "\t" + hmmEntryList.Count.ToString() + "\t" + FormatArrayString(noHmmSiteEntryList); ;
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.WriteLine(FormatArrayString (entryList) + "\t" + entryList.Count.ToString ());

            double sumDist = 0;
            int count = 0;
            double avgDist = 0;
            double minDist = 0;
            foreach (int hmmSite in hmmSitePepResidueHash.Keys)
            {
                List<string> pepResidueList = hmmSitePepResidueHash[hmmSite];
                pepResidueList.Sort ();
                sumDist = (double)hmmSitePepResidueSumDistHash[hmmSite];
                count = (int)hmmSitePepResidueCountHash[hmmSite];
                avgDist = sumDist / (double)count; 
                minDist = (double)hmmSitePepResidueMinDistHash[hmmSite];
                dataLine = hmmSite.ToString() + "\t" + FormatArrayString(pepResidueList) + "\t" + 
                    pepResidueList.Count.ToString() + "\t" + avgDist.ToString () + "\t" + minDist.ToString ();
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void MatchPepResiduesToHmmSites(string pdbId, int domainInterfaceId, string[] pepResidues, DataTable chainPepAtomPairsTable,
            StreamWriter dataWriter, ref Dictionary<int, List<string>> hmmSiteNumEntryHash, ref Dictionary<int, List<string>> hmmSitePepResidueHash,
            ref Dictionary<int, double> hmmSitePepResidueSumDistHash, ref Dictionary<int,double> hmmSitePepResidueCountHash, ref Dictionary<int, double> hmmSitePepResidueMinDistHash)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbId = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteTable.Rows.Count == 0)
            {
                return;
            }
            int interfaceId = Convert.ToInt32(hmmSiteTable.Rows[0]["InterfaceID"].ToString());
            string residue = "";
            int seqId = 0;
            string dataLine = "";
            double distance = 0;
            List<int> hmmSiteList = new List<int> ();
            List<int> seqIdList = new List<int> ();
            List<string> residueList = new List<string> ();
            List<double> distanceList = new List<double> ();
            foreach (string pepResidue in pepResidues)
            {
                hmmSiteList.Clear();
                seqIdList.Clear();
                distanceList.Clear();
                residueList.Clear();
                DataRow[] dataRows = chainPepAtomPairsTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}' AND PepResidue = '{2}'",
                    pdbId, interfaceId, pepResidue));
                foreach (DataRow dataRow in dataRows)
                {
                    residue = dataRow["Residue"].ToString().TrimEnd();
                    seqId = Convert.ToInt32(dataRow["SeqID"].ToString());
                    distance = Convert.ToDouble(dataRow["Distance"].ToString());

                    int[] interactingHmmSites = GetHmmSiteSeqIds(domainInterfaceId, seqId, hmmSiteTable);  
                    foreach (int hmmSite in interactingHmmSites)
                    {
                        if (hmmSiteNumEntryHash.ContainsKey(hmmSite))
                        {
                            if (!hmmSiteNumEntryHash[hmmSite].Contains(pdbId))
                            {
                                hmmSiteNumEntryHash[hmmSite].Add(pdbId);
                            }
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            hmmSiteNumEntryHash.Add(hmmSite, entryList);
                        }
                        if (hmmSitePepResidueHash.ContainsKey(hmmSite))
                        {
                            if (!hmmSitePepResidueHash[hmmSite].Contains(pepResidue))
                            {
                                hmmSitePepResidueHash[hmmSite].Add(pepResidue);
                            }
                            double sumDist = (double)hmmSitePepResidueSumDistHash[hmmSite];
                            sumDist += distance;
                            hmmSitePepResidueSumDistHash[hmmSite] = sumDist;

                            int count = (int)hmmSitePepResidueCountHash[hmmSite];
                            count++;
                            hmmSitePepResidueCountHash[hmmSite] = count;

                            double minDist = (double)hmmSitePepResidueMinDistHash[hmmSite];
                            if (minDist > distance)
                            {
                                hmmSitePepResidueMinDistHash[hmmSite] = distance;
                            }
                        }
                        else
                        {
                            List<string> pepResidueList = new List<string> ();
                            pepResidueList.Add(pepResidue);
                            hmmSitePepResidueHash.Add(hmmSite, pepResidueList);

                            hmmSitePepResidueSumDistHash.Add(hmmSite, distance);
                            hmmSitePepResidueCountHash.Add(hmmSite, 1);

                            hmmSitePepResidueMinDistHash.Add(hmmSite, distance);
                        }
                      //  if (!hmmSiteList.Contains(hmmSite))
                      //  {
                            hmmSiteList.Add(hmmSite);
                      //  }
                        seqIdList.Add(seqId);
                        distanceList.Add(distance);
                        residueList.Add(residue);
                    }
                }
                if (hmmSiteList.Count > 0)
                {
                    hmmSiteList.Sort();
                    seqIdList.Sort();
                    int[] seqIds = new int[seqIdList.Count];
                    seqIdList.CopyTo(seqIds);
                    int[] unpSeqIds = GetUnpSequenceIDs(pdbId, domainInterfaceId, seqIds);
                    dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + interfaceId.ToString() + "\t" + pepResidue + "\t" +
                            FormatArrayString (residueList) + "\t" + FormatArrayString(seqIdList) + "\t" + FormatArrayString(hmmSiteList) + "\t" + 
                            FormatArrayString(new List<int> (unpSeqIds)) + "\t" + FormatArrayString(distanceList);

                    dataWriter.WriteLine(dataLine);
                }
            }
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void MatchPepResiduesToHmmSites(string pdbId, int domainInterfaceId, string[] pepResidues, string protResidue, DataTable chainPepAtomPairsTable, 
            StreamWriter dataWriter, ref Dictionary<int, List<string>> hmmSiteNumEntryHash, ref Dictionary<int, List<string>> hmmSitePepResidueHash, 
            ref Dictionary<int, double> hmmSitePepResidueSumDistHash, ref Dictionary<int, int> hmmSitePepResidueCountHash, ref Dictionary<int, double> hmmSitePepResidueMinDistHash)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbId = '{0}' AND DomainInterfaceID = {1} AND Residue = '{2}' ;", 
                pdbId, domainInterfaceId, protResidue);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteTable.Rows.Count == 0)
            {
                return;
            }
            int interfaceId = Convert.ToInt32 (hmmSiteTable.Rows[0]["InterfaceID"].ToString());
            string residue = "";
            int seqId = 0;
            string dataLine = "";
            double distance = 0;
            List<int> hmmSiteList = new List<int> ();
            List<int> seqIdList = new List<int> ();
            List<double> distanceList = new List<double> ();
            foreach (string pepResidue in pepResidues)
            {
                hmmSiteList.Clear();
                seqIdList.Clear();
                distanceList.Clear();
                DataRow[] dataRows = chainPepAtomPairsTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}' AND PepResidue = '{2}' AND Residue = '{3}'", 
                    pdbId, interfaceId, pepResidue, protResidue));
                foreach (DataRow dataRow in dataRows)
                {
                    residue = dataRow["Residue"].ToString().TrimEnd ();
                    seqId = Convert.ToInt32(dataRow["SeqID"].ToString ());
                    distance = Convert.ToDouble(dataRow["Distance"].ToString ());

                    int[] interactingHmmSites = GetHmmSiteSeqIds(domainInterfaceId, seqId, hmmSiteTable);
                    foreach (int hmmSite in interactingHmmSites)
                    {
                        if (hmmSiteNumEntryHash.ContainsKey(hmmSite))
                        {
                            if (!hmmSiteNumEntryHash[hmmSite].Contains(pdbId))
                            {
                                hmmSiteNumEntryHash[hmmSite].Add(pdbId);
                            }
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            hmmSiteNumEntryHash.Add(hmmSite, entryList);
                        }
                        if (hmmSitePepResidueHash.ContainsKey(hmmSite))
                        {
                            if (!hmmSitePepResidueHash[hmmSite].Contains(pepResidue))
                            {
                                hmmSitePepResidueHash[hmmSite].Add(pepResidue);
                            }
                            double sumDist = (double)hmmSitePepResidueSumDistHash[hmmSite];
                            sumDist += distance;
                            hmmSitePepResidueSumDistHash[hmmSite] = sumDist;

                            int count = (int)hmmSitePepResidueCountHash[hmmSite];
                            count++;
                            hmmSitePepResidueCountHash[hmmSite] = count;

                            double minDist = (double)hmmSitePepResidueMinDistHash[hmmSite];
                            if (minDist > distance)
                            {
                                hmmSitePepResidueMinDistHash[hmmSite] = distance;
                            }
                        }
                        else
                        {
                            List<string> pepResidueList = new List<string> ();
                            pepResidueList.Add(pepResidue);
                            hmmSitePepResidueHash.Add(hmmSite, pepResidueList);

                            hmmSitePepResidueSumDistHash.Add(hmmSite, distance);
                            hmmSitePepResidueCountHash.Add(hmmSite, 1);

                            hmmSitePepResidueMinDistHash.Add(hmmSite, distance);
                        }
                        if (!hmmSiteList.Contains(hmmSite))
                        {
                            hmmSiteList.Add(hmmSite);
                        }
                        seqIdList.Add(seqId);
                        distanceList.Add(distance);
                    }
                }
                if (hmmSiteList.Count > 0)
                {
                    hmmSiteList.Sort();
                    seqIdList.Sort();
                    int[] seqIds = new int[seqIdList.Count];
                    seqIdList.CopyTo(seqIds);
                    int[] unpSeqIds = GetUnpSequenceIDs(pdbId, domainInterfaceId, seqIds);
                    dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + interfaceId.ToString() + "\t" + pepResidue + "\t" +
                            residue + "\t" + FormatArrayString(seqIdList) + "\t" + FormatArrayString(hmmSiteList) + "\t" + FormatArrayString(new List<int>(unpSeqIds))
                            + "\t" + FormatArrayString(distanceList);
                     
                    dataWriter.WriteLine(dataLine);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void MatchMinPepResiduesToHmmSites(string pdbId, int domainInterfaceId, string[] pepResidues, DataTable chainPepAtomPairsTable,
                    StreamWriter dataWriter, ref Dictionary<int, List<string>> hmmSiteNumEntryHash, ref Dictionary<int, List<string>> hmmSitePepResidueHash,
                    ref Dictionary<int, double> hmmSitePepResidueSumDistHash, ref Dictionary<int, int> hmmSitePepResidueCountHash, ref Dictionary<int, double> hmmSitePepResidueMinDistHash)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PdbId = '{0}' AND DomainInterfaceID = {1} ;", pdbId, domainInterfaceId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (hmmSiteTable.Rows.Count == 0)
            {
                return;
            }
            int interfaceId = Convert.ToInt32(hmmSiteTable.Rows[0]["InterfaceID"].ToString());
            string residue = "";
            int seqId = 0;
            string dataLine = "";
            double distance = 0;
            List<int> hmmSiteList = new List<int> ();
            List<int> seqIdList = new List<int> ();
            List<double> distanceList = new List<double> ();
            double minDistance = 1000.0;
            string minResidue = "";
            int minSeqId = -1;
            foreach (string pepResidue in pepResidues)
            {
                hmmSiteList.Clear();
                seqIdList.Clear();
                distanceList.Clear();
                minDistance = 1000;
                minResidue = "";
                minSeqId = -1;
                DataRow[] dataRows = chainPepAtomPairsTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}' AND PepResidue = '{2}'", pdbId, interfaceId, pepResidue));
                foreach (DataRow dataRow in dataRows)
                {
                    residue = dataRow["Residue"].ToString().TrimEnd();
                    seqId = Convert.ToInt32(dataRow["SeqID"].ToString());
                    distance = Convert.ToDouble(dataRow["Distance"].ToString());
                    if (minDistance > distance)
                    {
                        minDistance = distance;
                        minResidue = residue;
                        minSeqId = seqId;
                    }  
                }
                int[] interactingHmmSites = GetHmmSiteSeqIds(domainInterfaceId, minSeqId, hmmSiteTable);
                foreach (int hmmSite in interactingHmmSites)
                {
                    if (hmmSiteNumEntryHash.ContainsKey(hmmSite))
                    {
                        if (!hmmSiteNumEntryHash[hmmSite].Contains(pdbId))
                        {
                            hmmSiteNumEntryHash[hmmSite].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        hmmSiteNumEntryHash.Add(hmmSite, entryList);
                    }
                    if (hmmSitePepResidueHash.ContainsKey(hmmSite))
                    {
                        if (!hmmSitePepResidueHash[hmmSite].Contains(pepResidue))
                        {
                            hmmSitePepResidueHash[hmmSite].Add(pepResidue);
                        }
                        double sumDist = (double)hmmSitePepResidueSumDistHash[hmmSite];
                        sumDist += minDistance;
                        hmmSitePepResidueSumDistHash[hmmSite] = sumDist;

                        int count = (int)hmmSitePepResidueCountHash[hmmSite];
                        count++;
                        hmmSitePepResidueCountHash[hmmSite] = count;

                        double minDist = (double)hmmSitePepResidueMinDistHash[hmmSite];
                        if (minDist > minDistance)
                        {
                            hmmSitePepResidueMinDistHash[hmmSite] = minDistance;
                        }
                    }
                    else
                    {
                        List<string> pepResidueList = new List<string> ();
                        pepResidueList.Add(pepResidue);
                        hmmSitePepResidueHash.Add(hmmSite, pepResidueList);

                        hmmSitePepResidueSumDistHash.Add(hmmSite, minDistance);
                        hmmSitePepResidueCountHash.Add(hmmSite, 1);

                        hmmSitePepResidueMinDistHash.Add(hmmSite, minDistance);
                    }
                    if (!hmmSiteList.Contains(hmmSite))
                    {
                        hmmSiteList.Add(hmmSite);
                    }
                    seqIdList.Add(minSeqId);
                    distanceList.Add(minDistance);
                }

                if (hmmSiteList.Count > 0)
                {
                    hmmSiteList.Sort();
                    seqIdList.Sort();
                    int[] seqIds = new int[seqIdList.Count];
                    seqIdList.CopyTo(seqIds);
                    int[] unpSeqIds = GetUnpSequenceIDs(pdbId, domainInterfaceId, seqIds);
                    dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + interfaceId.ToString() + "\t" + pepResidue + "\t" +
                            minResidue + "\t" + FormatArrayString(seqIdList) + "\t" + FormatArrayString(hmmSiteList) + "\t" + FormatArrayString(new List<int>(unpSeqIds)) + 
                            "\t" + FormatArrayString (distanceList);

                    dataWriter.WriteLine(dataLine);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepInterfaceId"></param>
        /// <param name="seqIds"></param>
        /// <returns></returns>
        private int[] GetUnpSequenceIDs(string pdbId, int pepInterfaceId, int[] seqIds)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceId = {1};", pdbId, pepInterfaceId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string asymChain = pepInterfaceTable.Rows[0]["AsymChain"].ToString().TrimEnd();
            queryString = string.Format("Select SeqNumbers, DbSeqNumbers From PdbDbRefSeqAlignSifts Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqNumberTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int[] dbSeqIds = new int[seqIds.Length];
            string seqNumberString = "";
            string dbSeqNumberString = "";
            int seqIndex = 0;
            foreach (DataRow seqNumRow in seqNumberTable.Rows)
            {
                seqNumberString = seqNumRow["SeqNumbers"].ToString().TrimEnd();
                dbSeqNumberString = seqNumRow["DbSeqNumbers"].ToString().TrimEnd();
                string[] seqNumbers = seqNumberString.Split(',');
                string[] dbSeqNumbers = dbSeqNumberString.Split(',');
                for (int i = 0; i < seqIds.Length; i++)
                {
                    seqIndex = Array.IndexOf(seqNumbers, seqIds[i].ToString());
                    if (seqIndex > -1)
                    {
                        dbSeqIds[i] = Convert.ToInt32(dbSeqNumbers[seqIndex]);
                    }
                }
            }
            return dbSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <returns></returns>
        private string FormatArrayString<T>(List<T> dataList)
        {
            string arrayString = "";
            foreach (T item in dataList)
            {
                arrayString += (item.ToString() + ",");
            }
            arrayString = arrayString.TrimEnd(',');
            return arrayString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceId"></param>
        /// <param name="residueSeqId"></param>
        /// <param name="hmmSiteTable"></param>
        /// <returns></returns>
        private int[] GetHmmSiteSeqIds (int domainInterfaceId, int residueSeqId, DataTable hmmSiteTable)
        {
            string queryString = string.Format("DomainInterfaceId = '{0}' AND SeqID = '{1}'", domainInterfaceId, residueSeqId);
            DataRow[] hmmSiteRows = hmmSiteTable.Select(queryString);
            List<int> hmmSiteList = new List<int> ();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString ());
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }
            int[] hmmSiteSeqIds = new int[hmmSiteList.Count];
            hmmSiteList.CopyTo(hmmSiteSeqIds);
            return hmmSiteSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainPepAtomPairsTable  (string pdbId)
        {
            string queryString = string.Format("Select * From ChainPeptideAtomPairs Where PdbID = '{0}' AND Distance <= 5.0 Order BY InterfaceID, SeqID;", pdbId);
            DataTable pepInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            return pepInterfaceTable;
        }
        #endregion

        #region for paper
        public void PrintPfamDomainRelationInfo()
        {
            string pfamId = "Pkinase";
            int relSeqId = GetPfamDomainRelSeqId(pfamId);
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqId = {0};", relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\protcid_update\data\" + pfamId + "_sumInfo.txt");
            string pdbId = "";
            int domainInterfaceId = 0;
            string interfacePfamArch = "";
            Dictionary<string, List<string>> interfacePfamArchEntryHash = new Dictionary<string,List<string>> ();
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString ());
                interfacePfamArch = GetDomainInterfaceChainPfamArch(relSeqId, pdbId, domainInterfaceId, domainInterfaceTable);
                if (interfacePfamArch.IndexOf(";") > -1)
                {
                    dataWriter.WriteLine(pdbId + "\t" + domainInterfaceId.ToString() + "\t" + interfacePfamArch + "\thetero");
                }
                else
                {
                    dataWriter.WriteLine(pdbId + "\t" + domainInterfaceId.ToString() + "\t" + interfacePfamArch + "\thomo");
                }
                if (interfacePfamArchEntryHash.ContainsKey(interfacePfamArch))
                {
                    if (!interfacePfamArchEntryHash[interfacePfamArch].Contains(pdbId))
                    {
                        interfacePfamArchEntryHash[interfacePfamArch].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    interfacePfamArchEntryHash.Add(interfacePfamArch, entryList);
                }
            }
            foreach (string lsInterfacePfamArch in interfacePfamArchEntryHash.Keys)
            {
                dataWriter.WriteLine(lsInterfacePfamArch + "\t" + interfacePfamArchEntryHash[lsInterfacePfamArch].Count.ToString() + "\t" + FormatArrayString(interfacePfamArchEntryHash[lsInterfacePfamArch]));
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private string GetDomainInterfaceChainPfamArch(int relSeqId, string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            string queryString = string.Format("Select ChainPfamArch From PfamDomainClusterInterfaces Where RelSeqId = {0} AND PdbID = '{1}' AND DomainInterfaceId = {2};",
                relSeqId, pdbId, domainInterfaceId);
            DataTable chainPfamArchTable = ProtCidSettings.protcidQuery.Query( queryString);
            string interfacePfamArch = "";
            if (chainPfamArchTable.Rows.Count > 0)
            {
                interfacePfamArch = chainPfamArchTable.Rows[0]["ChainPfamArch"].ToString().TrimEnd();
            }
            else
            {
                interfacePfamArch = GetDomainInterfaceChainPfamArch(pdbId, domainInterfaceId, domainInterfaceTable);
            }
            return interfacePfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private string GetDomainInterfaceChainPfamArch(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            DataRow[] interfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            string chainPfamArch = "";
            string interfaceChainPfamArch = "";
            int entityId1 = 0;
            int entityId2 = 0;
            foreach (DataRow interfaceRow in interfaceRows)
            {
                chainDomainId = Convert.ToInt32(interfaceRow["ChainDomainId1"].ToString ());
                chainDomainIdList.Add(chainDomainId);
                entityId1 = GetChainDomainEntityId (pdbId, chainDomainId);
                chainPfamArch = GetEntityPfamArch(pdbId, entityId1);
                interfaceChainPfamArch = chainPfamArch;

                chainDomainId = Convert.ToInt32(interfaceRow["ChainDomainId2"].ToString ());
                entityId2 = GetChainDomainEntityId(pdbId, chainDomainId);

                if (entityId1 != entityId2)
                {
                    chainPfamArch = GetEntityPfamArch(pdbId, entityId2);
                    interfaceChainPfamArch = interfaceChainPfamArch + ";" + chainPfamArch;
                }
            }
            return interfaceChainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int GetChainDomainEntityId(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select EntityID From PdbPfamChain WHere PdbID = '{0}' AND ChainDomainId = {1} Order By EntityID;", pdbId, chainDomainId);
            DataTable entityIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int entityId = Convert.ToInt32(entityIdTable.Rows[0]["EntityId"].ToString());
            return entityId;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntityPfamArch(string pdbId, int entityId)
        {
            string queryString = string.Format("Select SupPfamArch, SupPfamArchE3, SupPfamArchE5 From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamArchTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamArch = "";
            foreach (DataRow pfamArchRow in pfamArchTable.Rows)
            {
                pfamArch = pfamArchRow["SupPfamArchE5"].ToString().TrimEnd();
                if (pfamArch == "-" || pfamArch == "")
                {
                    pfamArch = pfamArchRow["SupPfamArchE3"].ToString().TrimEnd();
                }
                if (pfamArch == "-" || pfamArch == "")
                {
                    pfamArch = pfamArchRow["SupPfamArch"].ToString().TrimEnd();
                }
            }
            return pfamArch;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetPfamDomainRelSeqId(string pfamId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation WHere FamilyCode1 = '{0}' AND FamilyCode2 = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }
        /// <summary>
        /// 
        /// </summary>
        public void GetTheInterfaceClusterInfoUpdate()
        {
            StreamWriter dataWriter = new StreamWriter("EntryDomainInterfacesUpdate5.txt");
            Dictionary<string, List<long>> entryDomainHash = GetEntryDomainHash();
            string dataLine = "";
            Dictionary<int, List<string>> relationDomainInterfaceHash = new Dictionary<int,List<string>> ();
            foreach (string pdbId in entryDomainHash.Keys)
            {
                foreach (long domainId in entryDomainHash[pdbId])
                {
                    dataLine = GetDomainInterfaceInfo(pdbId, domainId, ref relationDomainInterfaceHash);
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
            dataWriter = new StreamWriter("RelationUpdateDomainInterfaces5.txt");
            foreach (int relSeqId in relationDomainInterfaceHash.Keys)
            {
                dataLine = relSeqId.ToString ();
                foreach (string domainInterface in relationDomainInterfaceHash[relSeqId])
                {
                    dataLine += ("\t" + domainInterface);
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string GetDomainInterfaceInfo(string pdbId, long domainId, ref Dictionary<int, List<string>> relationDomainInterfaceHash)
        {
            string domainInterfaceInfo = "";
            string queryString = string.Format("Select RelSeqID, DomainInterfaceID, SurfaceArea From PfamDomainInterfaces Where PdbID = '{0}' AND (DomainID1 = {1} OR DomainID2 = {1});",
                pdbId, domainId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            string domainInterfaceId = "";
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                relSeqId  = Convert.ToInt32 (domainInterfaceIdRow["RelSeqID"].ToString ());
                domainInterfaceId = domainInterfaceIdRow["DomainInterfaceID"].ToString ();
                domainInterfaceInfo += (pdbId + "\t" + domainId.ToString () + "\t" + domainInterfaceId + "\t" + relSeqId + "\t" + 
                    domainInterfaceIdRow["SurfaceArea"].ToString ()  + "\r\n");
                if (relationDomainInterfaceHash.ContainsKey(relSeqId))
                {
                    relationDomainInterfaceHash[relSeqId].Add(pdbId + "_d" + domainInterfaceId);
                }
                else
                {
                    List<string> domainInterfaceList = new List<string> ();
                    domainInterfaceList.Add(pdbId + "_d" + domainInterfaceId);
                    relationDomainInterfaceHash.Add(relSeqId, domainInterfaceList);
                }
            }
            return domainInterfaceInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<long>> GetEntryDomainHash()
        {
            int difCutoff = 5;
            StreamReader dataReader = new StreamReader("DomainsSeqRangesChanged.txt");
            Dictionary<string, List<long>> entryDomainHash = new Dictionary<string,List<long>> ();
            string line = "";
            long domainId = 0;
            string pdbId = "";
            int orgSeq = 0;
            int newSeq = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                fields = line.Split('\t');
                pdbId = fields[0];
                domainId = Convert.ToInt64(fields[2]);
                orgSeq = Convert.ToInt32(fields[3]);
                newSeq = Convert.ToInt32(fields[5]);
                if (newSeq - orgSeq >= difCutoff)
                {
                    if (entryDomainHash.ContainsKey(pdbId))
                    {
                        if (!entryDomainHash[pdbId].Contains(domainId))
                        {
                            entryDomainHash[pdbId].Add(domainId);
                        }
                    }
                    else
                    {
                        List<long> domainList = new List<long> ();
                        domainList.Add(domainId);
                        entryDomainHash.Add(pdbId, domainList);
                    }
                }
            }
            dataReader.Close();
            dataReader = new StreamReader("WrongMultiChainDomains.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                pdbId = line.Substring(0, 4);
                domainId = Convert.ToInt64(line.Substring(4, line.Length - 4));
                if (entryDomainHash.ContainsKey(pdbId))
                {
                    if (!entryDomainHash[pdbId].Contains(domainId))
                    {
                        entryDomainHash[pdbId].Add(domainId);
                    }
                }
                else
                {
                    List<long> domainList = new List<long> ();
                    domainList.Add(domainId);
                    entryDomainHash.Add(pdbId, domainList);
                }
            }
            dataReader.Close();
            return entryDomainHash;
        }
        #endregion

        #region for pfam-peptide
        public void GetPfamPepInterfaceClusterInfo()
        {
            DataTable pepSeqTable = GetPeptideSequenceTable();
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\protcid_update\data\PepPfamInfo.txt");
            string queryString = "Select Distinct PfamId From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query( queryString);
            dataWriter.WriteLine("#Pfams in Pfam-Peptide: " + pepPfamTable.Rows.Count.ToString());
            queryString = "Select Distinct PfamId, ClusterId From PfamPepInterfaceClusters;";
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            dataWriter.WriteLine("#Clusters: " + pepClusterTable.Rows.Count.ToString());
            queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepPfamEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            dataWriter.WriteLine("#Entry: " + pepPfamEntryTable.Rows.Count.ToString());
            int numOfPeptides = GetNumOfPeptides(pepSeqTable);
            dataWriter.WriteLine("#Peptides: " + numOfPeptides.ToString());

            queryString = "Select PfamId, ClusterId, NumEntries, MinSeqIdentity From PfamPepClusterSumInfo Where NumEntries >= 2;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> pfamId2List = new List<string> ();
            List<string> clusterId2List = new List<string>();
            List<string> pepSeq2List = new List<string>();
            List<string> entry2List = new List<string>();
            List<string> pfamId2Seq90List = new List<string>();
            List<string> clusterId2Seq90List = new List<string>();
            List<string> pepSeq2Seq90List = new List<string>();
            List<string> entry2Seq90List = new List<string>();
            string pfamId = "";
            string cluster = "";
            int clusterId = 0;
            int numOfEntry = 0;
            double minSeqId = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                numOfEntry = Convert.ToInt32(clusterRow["NumEntries"].ToString());
                minSeqId = Convert.ToDouble(clusterRow["MinSeqIdentity"].ToString());
                if (numOfEntry >= 2)
                {
                    pfamId = clusterRow["PfamID"].ToString().TrimEnd();

                    cluster = pfamId + "_" + clusterRow["ClusterID"].ToString();

                    clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                    string[] pepSequences = GetPeptideSequences(pfamId, clusterId, pepSeqTable);

                    string[] clusterEntries = GetClusterEntries(pfamId, clusterId);

                    if (!pfamId2List.Contains(pfamId))
                    {
                        pfamId2List.Add(pfamId);
                    }
                    if (!clusterId2List.Contains(cluster))
                    {
                        clusterId2List.Add(cluster);
                    }
                    foreach (string pepSeq in pepSequences)
                    {
                        if (!pepSeq2List.Contains(pepSeq))
                        {
                            pepSeq2List.Add(pepSeq);
                        }
                    }
                    foreach (string pdbId in clusterEntries)
                    {
                        if (!entry2List.Contains(pdbId))
                        {
                            entry2List.Add(pdbId);
                        }
                    }

                    if (minSeqId <= 90)
                    {
                        if (!pfamId2Seq90List.Contains(pfamId))
                        {
                            pfamId2Seq90List.Add(pfamId);
                        }
                        if (!clusterId2Seq90List.Contains(cluster))
                        {
                            clusterId2Seq90List.Add(cluster);
                        }
                        foreach (string pepSeq in pepSequences)
                        {
                            if (!pepSeq2Seq90List.Contains(pepSeq))
                            {
                                pepSeq2Seq90List.Add(pepSeq);
                            }
                        }
                        foreach (string pdbId in clusterEntries)
                        {
                            if (!entry2Seq90List.Contains(pdbId))
                            {
                                entry2Seq90List.Add(pdbId);
                            }
                        }
                    }
                }
            }
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Pfams(#Entry >= 2): " + pfamId2List.Count.ToString());
            dataWriter.WriteLine("#Clusters (#Entry >= 2): " + clusterId2List.Count.ToString());
            dataWriter.WriteLine("#Entry(#Entry >= 2): " + entry2List.Count.ToString());
            dataWriter.WriteLine("#Peptides(#Entry >= 2): " + pepSeq2List.Count.ToString());
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Pfams(#Entry >= 2, MinSeq <= 90): " + pfamId2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Clusters (#Entry >= 2, MinSeq <= 90): " + clusterId2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Entry(#Entry >= 2, MinSeq <= 90): " + entry2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Peptides(#Entry >= 2, MinSeq <= 90): " + pepSeq2Seq90List.Count.ToString());

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterEntries(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterEntries = new string[entryTable.Rows.Count];
            int count = 0;
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                clusterEntries[count] = pdbId;
                count++;
            }
            return clusterEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetPeptideSequences(string pfamId, int clusterId, DataTable pepSeqTable)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> pepSeqList = new List<string> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            string pepChain = "";
            string pepSeq = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());

                pepChain = GetPeptideAsymChain(pdbId, domainInterfaceId);
                DataRow[] pepSeqRows = pepSeqTable.Select(string.Format("PdbID = '{0}' AND PepAsymChain = '{1}'", pdbId, pepChain));
                if (pepSeqRows.Length > 0)
                {
                    pepSeq = pepSeqRows[0]["Sequence"].ToString();
                    if (!pepSeqList.Contains(pepSeq))
                    {
                        pepSeqList.Add(pepSeq);
                    }
                }
            }
            return pepSeqList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetPeptideAsymChain(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select PepAsymChain From PfamPeptideInterfaces Where PdbID = '{0}' AND DOmainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pepChainTable.Rows.Count > 0)
            {
                return pepChainTable.Rows[0]["PepAsymChain"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepSeqTable"></param>
        /// <returns></returns>
        private int GetNumOfPeptides(DataTable pepSeqTable)
        {
            List<string> pepSeqList = new List<string> ();
            string sequence = "";
            foreach (DataRow pepSeqRow in pepSeqTable.Rows)
            {
                sequence = pepSeqRow["Sequence"].ToString();
                if (!pepSeqList.Contains(sequence))
                {
                    pepSeqList.Add(sequence);
                }
            }
            return pepSeqList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetPeptideSequenceTable()
        {
            string queryString = "Select Distinct PdbID, PepAsymChain From PfamPeptideInterfaces;";
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataTable pepChainSeqTable = pepChainTable.Clone();
            pepChainSeqTable.Columns.Add(new DataColumn("Sequence"));
            string pdbId = "";
            string pepAsymChain = "";
            string sequence = "";
            foreach (DataRow pepChainRow in pepChainTable.Rows)
            {
                pdbId = pepChainRow["PdbID"].ToString();
                pepAsymChain = pepChainRow["PepAsymChain"].ToString().TrimEnd();
                sequence = GetChainSequence(pdbId, pepAsymChain);
                DataRow pepSeqRow = pepChainSeqTable.NewRow();
                pepSeqRow["PdbID"] = pdbId;
                pepSeqRow["PepAsymChain"] = pepAsymChain;
                pepSeqRow["Sequence"] = sequence;
                pepChainSeqTable.Rows.Add(pepSeqRow);
            }
            return pepChainSeqTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetChainSequence(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (seqTable.Rows.Count > 0)
            {
                return seqTable.Rows[0]["Sequence"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion

        #region for debug
        public void GetGenAsymChainDomainInterfaces()
        {
            StreamWriter dataWriter = new StreamWriter("DomainInterfacesByGenChains.txt");
            string querystring = "Select * From PfamDomainInterfaces WHere ChainDomainID1 = 0 OR ChainDomainID2 = 0;";
            DataTable genDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( querystring);
            string pdbId = "";
            int domainInterfaceId = 0;
            string clusterInfo = "";
            string homoClusterInfo = "";
            string dataLine = "";
            foreach (DataRow domainInterfaceRow in genDomainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString ());
                clusterInfo = GetClusterInfo(pdbId, domainInterfaceId);
                homoClusterInfo = GetClusterInfoByHomo(pdbId, domainInterfaceId);
                dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t" + clusterInfo + "\t" +
                    homoClusterInfo;
                dataWriter.WriteLine(dataLine);
                
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetClusterInfo(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * from PfamDomainInterfaceCluster Where PdbID = '{0}' AND DomainInterfaceID = {1};", 
                pdbId,domainInterfaceId);
            DataTable interfaceClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            string clusterInfo = "";
            if (interfaceClusterTable.Rows.Count > 0)
            {
                clusterInfo = interfaceClusterTable.Rows[0]["RelSeqID"].ToString() + "_" + interfaceClusterTable.Rows[0]["ClusterID"].ToString();
            }
            return clusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetClusterInfoByHomo (string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * from PfamDomainClusterInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable interfaceClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            string clusterInfo = "";
            if (interfaceClusterTable.Rows.Count > 0)
            {
                clusterInfo = interfaceClusterTable.Rows[0]["RelSeqID"].ToString() + "_" + interfaceClusterTable.Rows[0]["ClusterID"].ToString();
            }
            return clusterInfo;
        }
        #endregion
    }
}
