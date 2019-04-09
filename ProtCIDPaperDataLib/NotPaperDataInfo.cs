using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    public class NotPaperDataInfo : ClusterInfo
    {
        #region pfam cluster info for casp prediction
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamSequences()
        {
            StreamWriter seqWriter = new StreamWriter("ChainPairs.txt");
            string queryString = "Select Pfam_ID From PfamClanFamily, PfamHmm Where PfamHmm.Pfam_Acc = PfamClanFamily.Pfam_Acc AND Clan_Acc = 'CL0123';";
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pfamInPdbList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                if (IsPfamInPdb(pfamId))
                {
                    pfamInPdbList.Add(pfamId);
                }
            }
            string targetChain = "3rcoA";
            foreach (string clanPfam in pfamInPdbList)
            {
                //      OutputPfamDomainSequences(clanPfam, seqWriter);
                //      OutputPfamAllDomainSequences(clanPfam, seqWriter);
                OutputChainPairs(clanPfam, targetChain, seqWriter);
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="targetChain"></param>
        /// <param name="dataWriter"></param>
        private void OutputChainPairs(string pfamId, string targetChain, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select PdbPfam.PdbID, AsymChain From PdbPfam, PdbPfamChain " +
                " Where PdbPfam.Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID And PdbPfam.EntityID = PdbPfamChain.ENtityID;", pfamId);
            DataTable domainChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> parsedEntryList = new List<string>();
            string entryChain = "";
            foreach (DataRow domainChainRow in domainChainTable.Rows)
            {
                entryChain = domainChainRow["PdbID"].ToString() + domainChainRow["AsymChain"].ToString();
                if (targetChain != entryChain)
                {
                    dataWriter.WriteLine(targetChain + "   " + entryChain);
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void FindPfamClusters()
        {
            StreamWriter dataWriter = new StreamWriter("CL0123PfamClustersInfo_chain.txt");
            dataWriter.WriteLine("PfamID\tClusterID\tSurfaceArea\tInPDB\tInPisa\tInASu\t#CF/Cluster\t#Entry/Cluster\tMinSeqIdentity");
            //         string clanAcc = "CL0123";
            string queryString = "Select Pfam_ID From PfamClanFamily, PfamHmm Where PfamHmm.Pfam_Acc = PfamClanFamily.Pfam_Acc AND Clan_Acc = 'CL0123';";
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pfamInPdbList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                if (IsPfamInPdb(pfamId))
                {
                    pfamInPdbList.Add(pfamId);
                }
            }
            foreach (string clanPfam in pfamInPdbList)
            {
                string clusterInfo = GetDomainClusterInfo(clanPfam);
                //   string clusterInfo = GetChainClusterInfo(clanPfam);
                if (clusterInfo != "")
                {
                    dataWriter.WriteLine(clusterInfo);
                }
            }
            dataWriter.Close();
        }

        private void OutputPfamAllDomainSequences(string pfamId, StreamWriter seqWriter)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID, SeqStart, SeqEnd From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            string pdbId = "";
            int entityId = 0;
            int domainStart = 0;
            int domainEnd = 0;
            string entitySequence = "";
            string domainSequence = "";
            string headerLine = "";
            foreach (DataRow entityRow in domainTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                entitySequence = GetEntitySequence(pdbId, entityId);
                domainStart = Convert.ToInt32(entityRow["SeqStart"].ToString());
                domainEnd = Convert.ToInt32(entityRow["SeqEnd"].ToString());
                try
                {
                    domainSequence = entitySequence.Substring(domainStart - 1, domainEnd - domainStart + 1);
                    headerLine = pdbId + entityId.ToString() + " [" + domainStart.ToString() + "-" + domainEnd.ToString() + "] " + pfamId;
                    seqWriter.WriteLine(">" + headerLine);
                    seqWriter.WriteLine(domainSequence);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(headerLine);
                    ProtCidSettings.logWriter.WriteLine("Write PDBfam to sequence file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
            }
            seqWriter.Flush();
        }

        private void OutputPfamDomainSequences(string pfamId, StreamWriter seqWriter)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID, SeqStart, SeqEnd From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            int relSeqId = GetPfamRelSeqId(pfamId);
            if (relSeqId > -1)
            {
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = 1;", relSeqId);
                DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                string pdbId = "";
                int domainStart = 0;
                int domainEnd = 0;
                string headerLine = "";
                foreach (DataRow entryRow in clusterEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    DataRow[] entityRows = domainTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    if (entityRows.Length > 0)
                    {
                        int entityId = Convert.ToInt32(entityRows[0]["EntityID"].ToString());
                        domainStart = Convert.ToInt32(entityRows[0]["SeqStart"].ToString());
                        domainEnd = Convert.ToInt32(entityRows[0]["SeqENd"].ToString());
                        string entitySequence = GetEntitySequence(pdbId, entityId);
                        try
                        {
                            string domainSequence = entitySequence.Substring(domainStart - 1, domainEnd - domainStart + 1);
                            headerLine = pdbId + entityId.ToString() + " [" + domainStart.ToString() + "-" + domainEnd.ToString() + "] " + pfamId;
                            seqWriter.WriteLine(">" + headerLine);
                            seqWriter.WriteLine(domainSequence);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.logWriter.WriteLine(headerLine);
                            ProtCidSettings.logWriter.WriteLine("Write domain sequence file error: " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                            continue;
                        }

                    }
                }
            }
            seqWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetPfamRelSeqId(string pfamId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDOmainFamilyRelation Where FamilyCOde1 = '{0}' AND FamilyCode2 = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (relSeqIdTable.Rows.Count > 0)
            {
                int relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
                return relSeqId;
            }
            return -1;
        }

        public string GetChainClusterInfo(string pfamId)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups WHere ChainRelPfamArch = '{0}';", "(" + pfamId + ")");
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int superGroupId = 0;
            if (superGroupIdTable.Rows.Count > 0)
            {
                superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
                string chainClusterInfo = GetChainClusterInfo(superGroupId, pfamId);
                return chainClusterInfo;
            }
            return "";
        }

        public string GetChainClusterInfo(int superGroupId, string pfamId)
        {
            string queryString = string.Format("Select ClusterID, SurfaceArea, InPDB, InPisa, InAsu, NumOfCfgCluster, NumOfEntryCluster, MinSeqIdentity " +
                " From PfamSuperClusterSumInfo Where SuperGroupSeqId = {0};", superGroupId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string chainClusterInfo = "";
            foreach (DataRow dataRow in clusterInfoTable.Rows)
            {
                chainClusterInfo += (pfamId + "\t" + ParseHelper.FormatDataRow(dataRow) + "\r\n");
            }
            return chainClusterInfo;
        }       

        private bool IsPfamInPdb(string pfamId)
        {
            string queryString = string.Format("Select distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (domainTable.Rows.Count > 0)
            {
                return true;
            }

            return false;
        }
        #endregion

        #region links_nz for Roland data
        public void FindClusterIDs()
        {
            string pfamId = "ubiquitin";
            int[] relSeqIds = GetPfamRelSeqIDs(pfamId);
            string dataSrcFile = @"C:\Paper\protcid_update\ubiquitins\links_nz";
            string resultFile = @"C:\Paper\protcid_update\ubiquitins\links_nz_protcid";
            StreamWriter dataWriter = new StreamWriter(resultFile);
            StreamReader dataReader = new StreamReader(dataSrcFile);
            string line = "";
            string dataLine = "";
            Dictionary<string, string[][]> entryAsuChainInfoHash = new Dictionary<string, string[][]>();
            string pdbId = "";
            string authChain1 = "";
            string authChain2 = "";
            string clusterInfo = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                dataLine = line;
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                if (fields.Length == 12)
                {
                    pdbId = fields[0].Substring(0, 4).ToLower();
                    authChain1 = fields[3];
                    authChain2 = fields[7];
                    clusterInfo = GetClusterInfo(relSeqIds, pdbId, authChain1, authChain2, entryAsuChainInfoHash);
                    dataLine = dataLine + "   " + clusterInfo;
                }
                else if (line.IndexOf("3DQV.pdb:LINK") > -1)
                {
                    pdbId = fields[0].Substring(0, 4).ToLower();
                    authChain1 = fields[3];
                    authChain2 = fields[7].Substring(0, 1);
                    clusterInfo = GetClusterInfo(relSeqIds, pdbId, authChain1, authChain2, entryAsuChainInfoHash);
                    dataLine = dataLine + "   " + clusterInfo;
                }
                dataWriter.WriteLine(dataLine);
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamRelSeqIDs(string pfamId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
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
        /// <param name="pdbId"></param>
        /// <param name="authChain1"></param>
        /// <param name="authChain2"></param>
        /// <returns></returns>
        private string GetClusterInfo(int[] pfamRelSeqIds, string pdbId, string authChain1, string authChain2, Dictionary<string, string[][]> entryAsymAuthChainHash)
        {
            string[][] asymAuthChains = GetInterfaceAsymChains(pdbId, entryAsymAuthChainHash);
            string asymChain1 = GetAsymChain(authChain1, asymAuthChains);
            string asymChain2 = GetAsymChain(authChain2, asymAuthChains);

            int relSeqId = -1;
            int domainInterfaceId = GetDomainInterfaceId(pfamRelSeqIds, pdbId, asymChain1, asymChain2, out relSeqId);
            string relationString = GetRelationPfamPairs(relSeqId);
            int clusterId = GetClusterID(relSeqId, pdbId, domainInterfaceId);
            return domainInterfaceId.ToString().PadRight(5) + relationString.PadRight(32) + relSeqId.ToString().PadRight(8) + clusterId.ToString();
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private int GetClusterID(int relSeqId, string pdbId, int domainInterfaceId)
        {
            int clusterId = -1;
            string queryString = string.Format("Select RelSeqID, ClusterID From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2};", relSeqId, pdbId, domainInterfaceId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (clusterInfoTable.Rows.Count > 0)
            {
                clusterId = Convert.ToInt32(clusterInfoTable.Rows[0]["ClusterID"].ToString());
            }
            return clusterId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain1"></param>
        /// <param name="asymChain2"></param>
        /// <returns></returns>
        private int GetDomainInterfaceId(int[] relSeqIds, string pdbId, string asymChain1, string asymChain2, out int relSeqId)
        {
            int domainInterfaceId = -1;
            relSeqId = -1;
            int dbRelSeqId = -1;
            string queryString = string.Format("Select RelSeqId, DomainInterfaceID From PfamDOmainInterfaces " +
                " Where PdbID = '{0}' AND ((AsymCHain1 = '{1}' AND AsymCHain2 = '{2}') OR (AsymCHain1 = '{2}' AND AsymCHain2 = '{1}')) " +
                " Order By DomainInterfaceID;", pdbId, asymChain1, asymChain2);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow domainInterfaceRow in domainInterfaceIdTable.Rows)
            {
                dbRelSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString());
                if (Array.IndexOf(relSeqIds, dbRelSeqId) > -1)
                {
                    relSeqId = dbRelSeqId;
                    domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                    break;
                }
            }
            return domainInterfaceId;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="authChain"></param>
        /// <param name="asymAuthChains"></param>
        /// <returns></returns>
        private string GetAsymChain(string authChain, string[][] asymAuthChains)
        {
            string asymChain = "";
            int chainIndex = Array.IndexOf(asymAuthChains[1], authChain);
            if (chainIndex > -1)
            {
                asymChain = asymAuthChains[0][chainIndex];
            }
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryAsuChainInfoHash"></param>
        /// <returns></returns>
        private string[][] GetInterfaceAsymChains(string pdbId, Dictionary<string, string[][]> entryAsuChainInfoHash)
        {
            string[][] asymAuthChains = null;
            if (entryAsuChainInfoHash.ContainsKey(pdbId))
            {
                asymAuthChains = (string[][])entryAsuChainInfoHash[pdbId];
            }
            else
            {
                string queryString = string.Format("Select AsymID, AuthorChain From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable asymAuthChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                asymAuthChains = new string[2][];
                string[] asymChains = new string[asymAuthChainTable.Rows.Count];
                string[] authChains = new string[asymAuthChainTable.Rows.Count];
                int count = 0;
                foreach (DataRow chainPairRow in asymAuthChainTable.Rows)
                {
                    asymChains[count] = chainPairRow["AsymID"].ToString().TrimEnd();
                    authChains[count] = chainPairRow["AuthorChain"].ToString().TrimEnd();
                    count++;
                }
                asymAuthChains[0] = asymChains;
                asymAuthChains[1] = authChains;
                entryAsuChainInfoHash.Add(pdbId, asymAuthChains);
            }
            return asymAuthChains;
        }
        #endregion

        #region Temple work, Pfam: metallophos
        /// <summary>
        /// 
        /// </summary>
        public void PrintLigandInteractingCommonHmmSites()
        {
            string pfamId = "Metallophos";
            string ligand = "PO4";

            string queryString = string.Format("Select PfamLigands.* From pfamligands, pdbligands " +
                " Where pfamid = '{0}' AND pdbligands.ligand = '{1}' AND  pfamligands.pdbid = pdbligands.pdbid" +
                " AND pfamligands.ligandchain = pdbligands.asymchain AND pfamligands.ligandseqid = pdbligands.seqid;", pfamId, ligand);
            DataTable ligandPfamInteractTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<string> chainDomainList = new List<string>();
            string chainDomain = "";
            foreach (DataRow interactRow in ligandPfamInteractTable.Rows)
            {
                chainDomain = interactRow["PdbID"].ToString() + interactRow["ChainDomainID"].ToString();
                if (!chainDomainList.Contains(chainDomain))
                {
                    chainDomainList.Add(chainDomain);
                }
            }
            string pdbId = "";
            int chainDomainId = 0;
            Dictionary<int, int> hmmSiteDomainNumHash = new Dictionary<int, int>();
            foreach (string lsChainDomain in chainDomainList)
            {
                pdbId = lsChainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(lsChainDomain.Substring(4, lsChainDomain.Length - 4));
                int[] hmmSites = GetHmmSites(ligandPfamInteractTable, pdbId, chainDomainId);
                foreach (int hmmSite in hmmSites)
                {
                    if (hmmSiteDomainNumHash.ContainsKey(hmmSite))
                    {
                        int count = (int)hmmSiteDomainNumHash[hmmSite];
                        count++;
                        hmmSiteDomainNumHash[hmmSite] = count;
                    }
                    else
                    {
                        hmmSiteDomainNumHash.Add(hmmSite, 1);
                    }
                }
            }

            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Metallophos_TempleGrant\Metallophos_PO4_StatInfo.txt");
            dataWriter.WriteLine("#Domains " + chainDomainList.Count.ToString());
            List<int> hmmSiteList = new List<int>(hmmSiteDomainNumHash.Keys);
            hmmSiteList.Sort();
            int totalDomains = chainDomainList.Count;
            List<int> commonHmmSiteList = new List<int>();
            foreach (int hmmSite in hmmSiteList)
            {
                int count = (int)hmmSiteDomainNumHash[hmmSite];
                dataWriter.WriteLine(hmmSite + "\t" + hmmSiteDomainNumHash[hmmSite].ToString());
                if ((double)count / (double)totalDomains >= 0.5)
                {
                    commonHmmSiteList.Add(hmmSite);
                }
            }
            dataWriter.Close();
            int[] commonHmmSites = new int[commonHmmSiteList.Count];
            commonHmmSiteList.CopyTo(commonHmmSites);
            //     int[] commonHmmSites = {8, 10, 40, 74, 75, 157, 196, 198 };
            GetSameBindingSitesPeptides(pfamId, commonHmmSites);
            GetSameBindingSitesProteins(pfamId, commonHmmSites);
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintPeptideInteractingCommonHmmSites()
        {
            string pfamId = "Metallophos";

            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PfamID = '{0}';", pfamId);
            DataTable pepPfamInteractTable = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> chainDomainList = new List<string>();
            string chainDomain = "";
            foreach (DataRow interactRow in pepPfamInteractTable.Rows)
            {
                chainDomain = interactRow["PdbID"].ToString() + "_" + interactRow["ChainDomainID"].ToString();
                if (!chainDomainList.Contains(chainDomain))
                {
                    chainDomainList.Add(chainDomain);
                }
            }
            string pdbId = "";
            //      int domainInterfaceId = 0;
            int chainDomainId = 0;
            Dictionary<int, int> hmmSiteDomainNumHash = new Dictionary<int, int>();
            foreach (string lsChainDomain in chainDomainList)
            {
                string[] fields = lsChainDomain.Split('_');
                pdbId = fields[0];
                //    domainInterfaceId = Convert.ToInt32(fields[1]);
                chainDomainId = Convert.ToInt32(fields[1]);
                int[] hmmSites = GetHmmSites(pepPfamInteractTable, pdbId, chainDomainId);
                foreach (int hmmSite in hmmSites)
                {
                    if (hmmSiteDomainNumHash.ContainsKey(hmmSite))
                    {
                        int count = (int)hmmSiteDomainNumHash[hmmSite];
                        count++;
                        hmmSiteDomainNumHash[hmmSite] = count;
                    }
                    else
                    {
                        hmmSiteDomainNumHash.Add(hmmSite, 1);
                    }
                }
            }

            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Metallophos_TempleGrant\Metallophos_peptide_StatInfo_domain.txt");
            dataWriter.WriteLine("#Domains " + chainDomainList.Count.ToString());
            List<int> hmmSiteList = new List<int>(hmmSiteDomainNumHash.Keys);
            hmmSiteList.Sort();
            foreach (int hmmSite in hmmSiteList)
            {
                dataWriter.WriteLine(hmmSite + "\t" + hmmSiteDomainNumHash[hmmSite].ToString());
            }
            dataWriter.Close();

            //       int[] commonHmmSites = {8, 10, 40, 74, 75, 157, 196, 198 };

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteTable"></param>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int[] GetHmmSites(DataTable hmmSiteTable, string pdbId, int chainDomainId)
        {
            DataRow[] hmmSiteRows = hmmSiteTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
            List<int> hmmSiteList = new List<int>();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString());
                if (hmmSeqId == -1)
                {
                    continue;
                }
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }
            int[] hmmSites = new int[hmmSiteList.Count];
            hmmSiteList.CopyTo(hmmSites);
            return hmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteTable"></param>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int[] GetHmmSites(DataTable hmmSiteTable, string pdbId, int domainInterfaceId, int chainDomainId)
        {
            DataRow[] hmmSiteRows = hmmSiteTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}' AND ChainDomainID = '{2}'",
                pdbId, domainInterfaceId, chainDomainId));
            List<int> hmmSiteList = new List<int>();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqID"].ToString());
                if (hmmSeqId == -1)
                {
                    continue;
                }
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }
            int[] hmmSites = new int[hmmSiteList.Count];
            hmmSiteList.CopyTo(hmmSites);
            return hmmSites;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="commonHmmSites"></param>
        private void GetSameBindingSitesProteins(string pfamId, int[] commonHmmSites)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PfamID = '{0}';", pfamId);
            DataTable pepPfamInteractTable = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> chainDomainList = new List<string>();
            string chainDomain = "";
            foreach (DataRow interactRow in pepPfamInteractTable.Rows)
            {
                chainDomain = interactRow["PdbID"].ToString() + "_" + interactRow["InterfaceID"].ToString() + "_" +
                              interactRow["DomainInterfaceID"].ToString() + "_" + interactRow["ChainDomainID"].ToString();
                if (!chainDomainList.Contains(chainDomain))
                {
                    chainDomainList.Add(chainDomain);
                }
            }
            string pdbId = "";
            int domainInterfaceId = 0;
            int interfaceId = 0;
            int chainDomainId = 0;
            List<string> simDomainInterfaceList = new List<string>();
            foreach (string lsChainDomain in chainDomainList)
            {
                string[] fields = lsChainDomain.Split('_');
                pdbId = fields[0];
                interfaceId = Convert.ToInt32(fields[1]);
                domainInterfaceId = Convert.ToInt32(fields[2]);
                chainDomainId = Convert.ToInt32(fields[3]);
                int[] hmmSites = GetHmmSites(pepPfamInteractTable, pdbId, domainInterfaceId, chainDomainId);
                if (hmmSites.Length > 0)
                {
                    int[] protSeqIds = GetProtInteractSeqIds(pepPfamInteractTable, pdbId, domainInterfaceId, chainDomainId);
                    int[] pepSeqIds = GetInteractPeptideSeqIds(pdbId, interfaceId, protSeqIds);
                    string[] simDomainInterfaces = GetSimChainInterfaces(pfamId, pdbId, domainInterfaceId, pepSeqIds);
                    foreach (string domainInterface in simDomainInterfaces)
                    {
                        if (!simDomainInterfaceList.Contains(domainInterface))
                        {
                            simDomainInterfaceList.Add(domainInterface);
                        }
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Metallophos_TempleGrant\Metallophos_pepLigand_domainInterfaceList.txt");
            dataWriter.WriteLine("#Domains " + chainDomainList.Count.ToString());
            simDomainInterfaceList.Sort();
            dataWriter.WriteLine(FormatArrayString(simDomainInterfaceList));
            /*     foreach (string simDomainInterface in simDomainInterfaceList)
                 {
                     dataWriter.WriteLine(simDomainInterface);
                 }*/
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="commonHmmSites"></param>
        private void GetSameBindingSitesPeptides(string pfamId, int[] commonHmmSites)
        {
            string queryString = string.Format("Select * From PfamPeptideHmmSites Where PfamID = '{0}';", pfamId);
            DataTable pepPfamInteractTable = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> chainDomainList = new List<string>();
            string chainDomain = "";
            foreach (DataRow interactRow in pepPfamInteractTable.Rows)
            {
                chainDomain = interactRow["PdbID"].ToString() + "_" + interactRow["DomainInterfaceID"].ToString() + "_" + interactRow["ChainDomainID"].ToString();
                if (!chainDomainList.Contains(chainDomain))
                {
                    chainDomainList.Add(chainDomain);
                }
            }
            string pdbId = "";
            int domainInterfaceId = 0;
            int chainDomainId = 0;
            Dictionary<int, List<string>> hmmSitePepInterfaceHash = new Dictionary<int, List<string>>();
            foreach (string lsChainDomain in chainDomainList)
            {
                string[] fields = lsChainDomain.Split('_');
                pdbId = fields[0];
                domainInterfaceId = Convert.ToInt32(fields[1]);
                chainDomainId = Convert.ToInt32(fields[2]);
                int[] hmmSites = GetHmmSites(pepPfamInteractTable, pdbId, domainInterfaceId, chainDomainId);
                foreach (int hmmSite in hmmSites)
                {
                    if (commonHmmSites.Contains(hmmSite))
                    {
                        if (hmmSitePepInterfaceHash.ContainsKey(hmmSite))
                        {
                            hmmSitePepInterfaceHash[hmmSite].Add(pdbId + domainInterfaceId);
                        }
                        else
                        {
                            List<string> pepInterfaceList = new List<string>();
                            pepInterfaceList.Add(pdbId + domainInterfaceId);
                            hmmSitePepInterfaceHash.Add(hmmSite, pepInterfaceList);
                        }
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Metallophos_TempleGrant\Metallophos_pepligand_StatInfo.txt");
            dataWriter.WriteLine("#Domains " + chainDomainList.Count.ToString());

            foreach (int hmmSite in commonHmmSites)
            {
                if (hmmSitePepInterfaceHash.ContainsKey(hmmSite))
                {
                    List<string> pepInterfaceList = hmmSitePepInterfaceHash[hmmSite];
                    dataWriter.WriteLine(hmmSite.ToString() + "\t" + FormatArrayString(pepInterfaceList) + "\t" + pepInterfaceList.Count.ToString());
                }
            }
            dataWriter.Close();
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteTable"></param>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int[] GetProtInteractSeqIds(DataTable hmmSiteTable, string pdbId, int domainInterfaceId, int chainDomainId)
        {
            DataRow[] hmmSiteRows = hmmSiteTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}' AND ChainDomainID = '{2}'",
                pdbId, domainInterfaceId, chainDomainId));
            List<int> protSeqIdList = new List<int>();
            int seqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                seqId = Convert.ToInt32(hmmSiteRow["SeqID"].ToString());
                if (seqId == -1)
                {
                    continue;
                }
                if (!protSeqIdList.Contains(seqId))
                {
                    protSeqIdList.Add(seqId);
                }
            }
            int[] protSeqIds = new int[protSeqIdList.Count];
            protSeqIdList.CopyTo(protSeqIds);
            return protSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="interactProtSeqIds"></param>
        /// <returns></returns>
        private int[] GetInteractPeptideSeqIds(string pdbId, int interfaceId, int[] interactProtSeqIds)
        {
            List<int> peptideSeqIdList = new List<int>();
            string queryString = string.Format("Select * From ChainPeptideAtomPairs WHere PdbID = '{0}' AND InterfaceID = {1} AND Distance < 5.0;", pdbId, interfaceId);
            DataTable chainPepInterfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
            int pepSeqId = 0;
            int protSeqId = 0;
            foreach (DataRow pepInterfaceRow in chainPepInterfaceTable.Rows)
            {
                protSeqId = Convert.ToInt32(pepInterfaceRow["SeqID"].ToString());
                if (interactProtSeqIds.Contains(protSeqId))
                {
                    pepSeqId = Convert.ToInt32(pepInterfaceRow["PepSeqID"].ToString());
                    if (!peptideSeqIdList.Contains(pepSeqId))
                    {
                        peptideSeqIdList.Add(pepSeqId);
                    }
                }
            }
            return peptideSeqIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <param name="pepDomainInterfaceId"></param>
        /// <param name="interactPepSeqIds"></param>
        /// <returns></returns>
        private string[] GetSimChainInterfaces(string pfamId, string pdbId, int pepDomainInterfaceId, int[] interactPepSeqIds)
        {
            string queryString = string.Format("Select * From PfamChainInterfaceHmmSiteComp Where PfamID = '{0}' AND PdbID1 = '{1}' " +
                " AND DomainInterfaceID1 = {2} AND NumOfCommonHmmSites >= 3 AND LocalPepRmsd < 5 and LocalPepRmsd > 0;", pfamId, pdbId, pepDomainInterfaceId);
            DataTable chainPepCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            int pepStart = 0;
            int pepEnd = 0;
            List<string> domainInterfaceList = new List<string>();
            string domainInterface = "";
            foreach (DataRow chainPepCompRow in chainPepCompTable.Rows)
            {
                pepStart = Convert.ToInt32(chainPepCompRow["PepStart"].ToString());
                pepEnd = Convert.ToInt32(chainPepCompRow["PepEnd"].ToString());
                foreach (int pepSeqId in interactPepSeqIds)
                {
                    if (pepSeqId <= pepEnd && pepSeqId >= pepStart)
                    {
                        domainInterface = chainPepCompRow["PdbID2"].ToString() + chainPepCompRow["DomainInterfaceID2"].ToString();
                        if (!domainInterfaceList.Contains(domainInterface))
                        {
                            domainInterfaceList.Add(domainInterface);
                        }
                    }
                }
            }
            return domainInterfaceList.ToArray();
        }
        #endregion

    }
}
