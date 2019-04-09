using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;


namespace ProtCIDPaperDataLib.paper
{
    public class HumanUnpDataInfo : PaperDataInfo
    {
        private string unpDataDir = "";
        public HumanUnpDataInfo ()
        {
            unpDataDir = Path.Combine(dataDir, "UnpClusterInfo");
        }
        #region human protein interactions
        public void CheckHumanProtInteractions()
        {
            string dataFile = Path.Combine(unpDataDir, "humanProt\\HumanProt_multDomains_clusterInfo.txt");
            StreamWriter dataWriter = new StreamWriter(dataFile);
            string queryString = "Select UnpCode, Isoform, Count(Distinct Pfam_ID) As DomainCount From HumanPfam where IsWeak = '0' Group By UnpCode, Isoform;";
            DataTable unpDomainCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            int isoform = 0;
            int domainCount = 0;
            List<string> humanEntryList = new List<string>();
            List<string> pfamRelationList = new List<string>();
            string clusterInfo = "";
            foreach (DataRow countRow in unpDomainCountTable.Rows)
            {
                domainCount = Convert.ToInt32(countRow["DomainCount"].ToString());
                if (domainCount >= 2)
                {
                    unpCode = countRow["UnpCode"].ToString().TrimEnd();
                    isoform = Convert.ToInt32(countRow["Isoform"].ToString());
                    string[] pfamIds = GetHumanProteinPfams(unpCode, isoform);
                    for (int i = 0; i < pfamIds.Length; i++)
                    {
                        for (int j = i + 1; j < pfamIds.Length; j++)
                        {
                            if (!ArePfamsInOneChain(pfamIds[i], pfamIds[j]))
                            {
                                clusterInfo = GetClusterInfo(pfamIds[i], pfamIds[j]);
                                if (clusterInfo != "")
                                {
                                    dataWriter.WriteLine(unpCode + "\t" + isoform.ToString() + "\t" + pfamIds[i] + "\t" + pfamIds[j] + "\t" +
                                        clusterInfo);
                                    if (!humanEntryList.Contains(unpCode))
                                    {
                                        humanEntryList.Add(unpCode);
                                    }
                                    if (!pfamRelationList.Contains(pfamIds[i] + "\t" + pfamIds[j]))
                                    {
                                        pfamRelationList.Add(pfamIds[i] + "\t" + pfamIds[j]);
                                    }
                                }
                            }
                        }
                    }
                }
                dataWriter.Flush();
            }
            dataWriter.WriteLine("#Human Entries: " + humanEntryList.Count.ToString());
            dataWriter.WriteLine("#Pfam Relations: " + pfamRelationList.Count.ToString() + "\t" + FormatArrayString(pfamRelationList));
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string[] GetHumanProteinPfams(string unpCode, int isoform)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From HumanPfam WHere UnpCode = '{0}' AND Isoform = {1} AND IsWeak = '0';", unpCode, isoform);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] protPfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                protPfamIds[count] = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return protPfamIds;
        }

        private string GetClusterInfo(string pfamId1, string pfamId2)
        {
            int relSeqId = GetRelationSeqID(pfamId1, pfamId2);
            if (relSeqId == -1)
            {
                return "";
            }
            string clusterInfo = GetProtCidClusterInfo(relSeqId);
            return clusterInfo;
        }

        private bool ArePfamsInOneChain(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = '{0}' AND IsWeak = '0';", pfamId1);
            DataTable entityTable1 = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = '{0}' AND IsWeak = '0';", pfamId2);
            DataTable entityTable2 = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entityList = new List<string>();
            foreach (DataRow entityRow in entityTable1.Rows)
            {
                entityList.Add(entityRow["PdbID"].ToString() + entityRow["EntityID"].ToString());
            }
            foreach (DataRow entityRow in entityTable2.Rows)
            {
                if (entityList.Contains(entityRow["PdbID"].ToString() + entityRow["EntityID"].ToString()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetConfidentHumanPPIs()
        {
            string dataFile = Path.Combine(unpDataDir, "humanProt\\humanProt_domainClusters.txt");
            string newDataFile = Path.Combine(unpDataDir, "humanProt\\humanProt_MN.txt");
            StreamReader dataReader = new StreamReader(dataFile);
            StreamWriter dataWriter = new StreamWriter(newDataFile);
            string line = "";
            string headerLine = dataReader.ReadLine();
            List<string> pfamIdList = new List<string>();
            List<string> entryList = new List<string>();
            List<string> relEntryList = new List<string>();
            int MCut = 5;
            int seqIdCut = 90;
            double surfaceAreaCut = 300;
            int m = 0;
            int seqId = 0;
            double surfaceArea = 0;
            int numOfRelations = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                m = Convert.ToInt32(fields[2]);
                seqId = Convert.ToInt32(fields[10]);
                surfaceArea = Convert.ToDouble(fields[6]);
                if (m >= MCut && seqId <= seqIdCut && surfaceArea >= surfaceAreaCut)
                {
                    relEntryList.Clear();
                    string[] humanEntries1 = fields[11].Split(',');
                    foreach (string humanUnp in humanEntries1)
                    {
                        if (!relEntryList.Contains(humanUnp))
                        {
                            relEntryList.Add(humanUnp);
                        }
                        if (!entryList.Contains(humanUnp))
                        {
                            entryList.Add(humanUnp);
                        }
                    }

                    string[] humanEntries2 = fields[12].Split(',');
                    foreach (string humanUnp in humanEntries2)
                    {
                        if (!relEntryList.Contains(humanUnp))
                        {
                            relEntryList.Add(humanUnp);
                        }
                        if (!entryList.Contains(humanUnp))
                        {
                            entryList.Add(humanUnp);
                        }
                    }
                    dataWriter.WriteLine(fields[0] + "\t" + fields[1] + "\t" + fields[2] + "\t" + fields[3] + "\t" +
                        fields[4] + "\t" + fields[5] + "\t" + fields[6] + "\t" + fields[7] + "\t" + fields[8] + "\t" +
                        fields[9] + "\t" + fields[10] + "\t" + relEntryList.Count.ToString());

                    numOfRelations++;
                }
            }
            dataReader.Close();
            dataWriter.WriteLine("Total #Entries: " + entryList.Count.ToString());
            dataWriter.WriteLine("Totoal #Relations: " + numOfRelations.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetProtCidClusterInfo(int relSeqId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, SurfaceArea, InPdb, InPisa, InAsu, MinSeqIdentity " +
                " From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster;", relSeqId);
            DataTable relClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string relMaxClusterInfo = "";
            if (relClusterInfoTable.Rows.Count > 0)
            {
                relMaxClusterInfo = ParseHelper.FormatDataRow(relClusterInfoTable.Rows[0]);
            }
            return relMaxClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="humanPfamTable"></param>
        /// <returns></returns>
        private string[] GetHumanProteinsWithPfam(string pfamId, DataTable humanPfamTable)
        {
            DataRow[] humanProtRows = humanPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
            string[] pfamHumanEntries = new string[humanProtRows.Length];
            int count = 0;
            foreach (DataRow pfamRow in humanProtRows)
            {
                pfamHumanEntries[count] = pfamRow["UnpCode"].ToString().TrimEnd();
                count++;
            }
            return pfamHumanEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetPfamsInHumanProteins()
        {
            string queryString = "Select Distinct Pfam_ID From HumanPfam;";
            DataTable humanPfamsTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            string[] humanPfamIds = new string[humanPfamsTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in humanPfamsTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                humanPfamIds[count] = pfamId;
                count++;
            }
            return humanPfamIds;
        }
        #endregion

        #region uniprot proteins
        #region summary info
        public void GetProteinsInPdbSumInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(unpDataDir, "ProteinsSumInfoInProtcid.txt"));
            int[] unpInPdbNumbers = GetNumbersOfProteinsInPdb();
            dataWriter.WriteLine("#UniProts in PDB = " + unpInPdbNumbers[0]);
            dataWriter.WriteLine("#Human UniProts in PDB = " + unpInPdbNumbers[1]);
            dataWriter.Flush();

            int[] unpInPfamsNumbers = GetNumberOfProteinsWithPfams();
            dataWriter.WriteLine("#UniProts with Pfams = " + unpInPfamsNumbers[0]);
            dataWriter.WriteLine("#Human UniProts with Pfams = " + unpInPfamsNumbers[1]);
            dataWriter.Flush();

            int[] pfamNumbers = PrintProteinPfams();
            dataWriter.WriteLine("#Pfams of UniProts = " + pfamNumbers[0]);
            dataWriter.WriteLine("#Pfams of human UniProts = " + pfamNumbers[1]);
            dataWriter.WriteLine("#Pfams of UniProts not in PDBfam = " + pfamNumbers[2]);
            dataWriter.Flush();

            int[] unpInPdbfamNumbers = PrintProteinsWithPfamsInPdb();
            dataWriter.WriteLine("#Uniprots in PDBfam = " + unpInPdbfamNumbers[0]);
            dataWriter.WriteLine("#Human UniProts in PDBfam = " + unpInPdbfamNumbers[1]);

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetNumbersOfProteinsInPdb()
        {
            string queryString = "Select count(distinct DbCode) As UnpCount From PdbDbRefSifts Where DbName = 'UNP';";
            DataTable unpInPdbCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = "Select count(distinct DbCode) As UnpCount From PdbDbRefSifts Where DbName = 'UNP' AND DbCode Like '%_HUMAN';";
            DataTable humanUnpInPdbCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] unpNumbers = new int[2];
            unpNumbers[0] = Convert.ToInt32(unpInPdbCountTable.Rows[0]["UnpCount"].ToString());
            unpNumbers[1] = Convert.ToInt32(humanUnpInPdbCountTable.Rows[0]["UnpCount"].ToString());
            return unpNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int[] GetNumberOfProteinsWithPfams()
        {
            //         string queryString = "Select Distinct DbCode As UnpCode From PdbDbRefSifts Where DbName = 'UNP';";
            string queryString = "Select Distinct UnpCode From UnpPfam;";
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpList = new List<string>();
            List<string> humanUnpList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                unpCode = unpRow["UnpCode"].ToString().TrimEnd();
                unpList.Add(unpCode);
                if (unpCode.IndexOf("_HUMAN") > -1)
                {
                    humanUnpList.Add(unpCode);
                }
            }

            queryString = "Select Distinct UnpCode From HumanPfam;";
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            foreach (DataRow hunpRow in humanUnpTable.Rows)
            {
                unpCode = hunpRow["UnpCode"].ToString().TrimEnd();
                if (!unpList.Contains(unpCode))
                {
                    unpList.Add(unpCode);
                }
                if (!humanUnpList.Contains(unpCode))
                {
                    humanUnpList.Add(unpCode);
                }
            }
            int[] unpNumbers = new int[2];
            unpNumbers[0] = unpList.Count;
            unpNumbers[1] = humanUnpList.Count;
            return unpNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int[] PrintProteinPfams()
        {
            string queryString = "Select Distinct Pfam_ID From UnpPfam;";
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<string> pfamList = new List<string>();
            List<string> humanPfamList = new List<string>();
            List<string> nopdbPfamList = new List<string>();
            string pfamId = "";
            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                pfamId = unpRow["Pfam_ID"].ToString().TrimEnd();
                pfamList.Add(pfamId);
                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length == 0)
                {
                    nopdbPfamList.Add(pfamId);
                }
            }

            queryString = "Select Distinct Pfam_ID From HumanPfam;";
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            foreach (DataRow hunpRow in humanUnpTable.Rows)
            {
                pfamId = hunpRow["Pfam_ID"].ToString().TrimEnd();
                if (!pfamList.Contains(pfamId))
                {
                    pfamList.Add(pfamId);
                }
                if (!humanPfamList.Contains(pfamId))
                {
                    humanPfamList.Add(pfamId);
                }

                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length == 0)
                {
                    if (!nopdbPfamList.Contains(pfamId))
                    {
                        nopdbPfamList.Add(pfamId);
                    }
                }
            }
            int[] pfamNumbers = new int[3];
            pfamNumbers[0] = pfamList.Count;
            pfamNumbers[1] = humanPfamList.Count;
            pfamNumbers[2] = nopdbPfamList.Count;
            return pfamNumbers;
        }

        public int[] PrintProteinsWithPfamsInPdb()
        {
            string queryString = "Select Distinct UnpCode, Pfam_ID From UnpPfam;";
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pdbPfamTable = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> unpList = new List<string>();
            List<string> humanUnpList = new List<string>();
            string pfamId = "";
            string unpCode = "";
            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                pfamId = unpRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = unpRow["UnpCode"].ToString().TrimEnd();
                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length > 0)
                {
                    if (!unpList.Contains(unpCode))
                    {
                        unpList.Add(unpCode);
                    }
                }
            }

            queryString = "Select Distinct UnpCode, Pfam_ID From HumanPfam;";
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            foreach (DataRow hunpRow in humanUnpTable.Rows)
            {
                pfamId = hunpRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = hunpRow["UnpCode"].ToString().TrimEnd();

                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length > 0)
                {
                    if (!humanUnpList.Contains(unpCode))
                    {
                        humanUnpList.Add(unpCode);
                    }
                    if (!unpList.Contains(unpCode))
                    {
                        unpList.Add(unpCode);
                    }
                }
            }
            int[] unpNumbersPdbfam = new int[2];
            unpNumbersPdbfam[0] = unpList.Count;
            unpNumbersPdbfam[1] = humanUnpList.Count;
            return unpNumbersPdbfam;
        }
        #endregion

        #region uniprot protein interactions
        public void PrintUnpProteinPfamInteractions()
        {
            string dataFile = Path.Combine(unpDataDir, "unpPfamInteractions.txt");
            StreamWriter dataWriter = new StreamWriter(dataFile);
            string clusterInfo = "";
            int M = 0;
       /*     Dictionary<string, List<string>> pfamUnpListDict = GetPfamsInPdbUnpListDict();
            List<string> pfamList = new List<string>(pfamUnpListDict.Keys);
            pfamList.Sort();
            
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int, string[]>();
            
            for (int i = 0; i < pfamList.Count; i++)
            {
                Dictionary<int, string[]> relPfamPairHash = GetInteractingPfamRelSeqIdList(pfamList[i], ProtCidSettings.protcidQuery, pfamList);
                foreach (int lsRelSeqId in relPfamPairHash.Keys)
                {
                    if (!relPfamPairDict.ContainsKey(lsRelSeqId))
                    {
                        relPfamPairDict.Add(lsRelSeqId, relPfamPairHash[lsRelSeqId]);
                    }
                }
            }*/
            Dictionary<int, string[]> relPfamPairDict = GetUnpInteractionRelPfamPairDict( );
            foreach (int lsRelSeqId in relPfamPairDict.Keys)
            {
                string[] pfamPair = relPfamPairDict[lsRelSeqId];
                string[] unpPairs = GetRelUnpPairList (lsRelSeqId);
                clusterInfo = GetBiggestClusterInfo(lsRelSeqId, ProtCidSettings.protcidQuery, out M);
                dataWriter.WriteLine(pfamPair[0] + "\t" + pfamPair[1] + "\t" + 
                    ParseHelper.FormatArrayString (unpPairs, ' ') + "\t" + clusterInfo);
                //           if (clusterInfo != "")
         /*       dataWriter.WriteLine(pfamPair[0] + "\t" + pfamPair[1] + "\t" +
                    ParseHelper.FormatStringFieldsToString(unpList1.ToArray()) + "\t" +
                    ParseHelper.FormatStringFieldsToString(unpList2.ToArray()) + "\t" + clusterInfo);*/
            }
            dataWriter.Close();
        }

        private Dictionary<int, string[]> GetUnpInteractionRelPfamPairDict ()
        {
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int, string[]>();
            string queryString = "Select Distinct RelSeqID From UnpPdbDomainInterfaces;";
            DataTable unpRelSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            foreach (DataRow relIdRow in unpRelSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relIdRow["RelSeqID"].ToString());
                string[] pfamPair = GetPfamPair(relSeqId);
                relPfamPairDict.Add(relSeqId, pfamPair);
            }
            return relPfamPairDict;
        }

        private string[] GetRelUnpPairList (int relSeqId)
        {
            string queryString = string.Format("Select Distinct UnpId1, UnpID2 From UnpPdbDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable unpPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> unpPairList = new List<string>();
            string unpPair = "";
            string unpId1 = "";
            string unpId2 = "";
            foreach (DataRow unpPairRow in unpPairTable.Rows)
            {
                unpId1 = unpPairRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpPairRow["UnpID2"].ToString().TrimEnd();
                unpPair = unpId1 + ";" + unpId2;
                if (string.Compare (unpId1, unpId2) > 0)
                {
                    unpPair = unpId2 + ";" + unpId1;
                }
                unpPairList.Add(unpPair);
            }
            return unpPairList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetPfamPair (int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pfamPair = new string[2];
            pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return pfamPair;
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintUnpInteractionSumInfoDifMs()
        {
            int[] Mcuts = { 1, 2, 3, 5, 10, 20 };
            string sumDataFile = Path.Combine(unpDataDir, "unpPfamInterSumInfo_minseq90_1.txt");
            StreamWriter dataWriter = new StreamWriter(sumDataFile, true);
            foreach (int mCut in Mcuts)
            {
                PrintUnpInteractionSumInfoDifM(mCut, dataWriter);
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="MCutoff"></param>
        /// <param name="sumWriter"></param>
        public void PrintUnpInteractionSumInfoDifM(int MCutoff, StreamWriter sumWriter)
        {
            string dataFile = Path.Combine(unpDataDir, "unpPfamInteractions.txt");
            List<int> pfamRelList = new List<int>();
            int relSeqId = 0;
            int clusterCount = 0;
            List<string> unpList = new List<string>();
            List<string[]> unpPairList = new List<string[]>();
            List<string> pdbList = new List<string>();
            int M = 0;
            int seqId = 0;
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            int mIndex = 9;
            int seqIndex = 19;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length > mIndex)
                {
                    M = Convert.ToInt32(fields[mIndex]);
                    seqId = Convert.ToInt32(fields[seqIndex]);
                    if (M >= MCutoff && seqId <= 90)
                    {
                        relSeqId = Convert.ToInt32(fields[3]);
                        int relClusterCount = GetRelationClusters(relSeqId, MCutoff, 90);
                        clusterCount += relClusterCount;
                        if (!pfamRelList.Contains(relSeqId))
                        {
                            pfamRelList.Add(relSeqId);
                        }
                        string[] unpFields = fields[2].Split("; ".ToCharArray ());
                        foreach (string unp in unpFields)
                        {
                            if (!unpList.Contains(unp))
                            {
                                unpList.Add(unp);
                            }
                        }

             /*           string[] unpFields2 = fields[3].Split(',');
                        foreach (string unp in unpFields2)
                        {
                            if (!unpList.Contains(unp))
                            {
                                unpList.Add(unp);
                            }
                        }*/
                        string[] relEntries = GetNumEntriesInRelation(relSeqId);
                        foreach (string pdbId in relEntries)
                        {
                            if (!pdbList.Contains(pdbId))
                            {
                                pdbList.Add(pdbId);
                            }
                        }

                    }
                }
            }
            dataReader.Close();
            sumWriter.WriteLine("M=" + MCutoff);
            sumWriter.WriteLine("#Relations = " + pfamRelList.Count);
            sumWriter.WriteLine("#Clusters = " + clusterCount);
            sumWriter.WriteLine("#Uniprots = " + unpList.Count);
            sumWriter.WriteLine("#entries = " + pdbList.Count);
            sumWriter.Flush();
        }

        private int GetRelationClusters(int relSeqId, int mCut, int seqCut)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamDomainClusterSumInfo " +
                " Where RelSeqID = {0} AND NumOfCfgCluster >= {1} AND MinSeqIdentity < {2};", relSeqId, mCut, seqCut);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            return clusterTable.Rows.Count;
        }

        public void PrintUnpInteractionSumInfoDifM(int MCutoff)
        {
            string dataFile = Path.Combine(unpDataDir, "unpPfamInteractions.txt");
            string sumDataFile = Path.Combine(unpDataDir, "unpPfamInterSumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(sumDataFile);

            List<int> pfamRelList = new List<int>();
            int relSeqId = 0;
            List<string> unpList = new List<string>();
            List<string[]> unpPairList = new List<string[]>();
            List<string> pdbList = new List<string>();
            int M = 0;
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length > 10)
                {
                    M = Convert.ToInt32(fields[10]);
                    if (M >= MCutoff)
                    {
                        dataWriter.WriteLine(line);
                        relSeqId = Convert.ToInt32(fields[4]);
                        if (!pfamRelList.Contains(relSeqId))
                        {
                            pfamRelList.Add(relSeqId);
                        }
                        string[] unpFields1 = fields[2].Split(',');
                        foreach (string unp in unpFields1)
                        {
                            if (!unpList.Contains(unp))
                            {
                                unpList.Add(unp);
                            }
                        }

                        string[] unpFields2 = fields[3].Split(',');
                        foreach (string unp in unpFields2)
                        {
                            if (!unpList.Contains(unp))
                            {
                                unpList.Add(unp);
                            }
                        }
                        string[] relEntries = GetNumEntriesInRelation(relSeqId);
                        foreach (string pdbId in relEntries)
                        {
                            if (!pdbList.Contains(pdbId))
                            {
                                pdbList.Add(pdbId);
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            dataWriter.WriteLine("#Relations = " + pfamRelList.Count);
            dataWriter.WriteLine("#Uniprots = " + unpList.Count);
            dataWriter.WriteLine("#entries = " + pdbList.Count);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetNumEntriesInRelation(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] relEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                relEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return relEntries;
        }

        public void PrintUnpInteractionSumInfo()
        {
            string dataFile = Path.Combine(unpDataDir, "unpPfamInteractions.txt");
            string sumDataFile = Path.Combine(unpDataDir, "unpPfamInterSumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(sumDataFile);

            int relCount = 0;
            List<string> unpList = new List<string>();
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            string pfamId = "";
            Dictionary<string, List<string>> pfamUnpListDict = new Dictionary<string, List<string>>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relCount++;
                pfamId = fields[0];
                string[] unpFields1 = fields[2].Split(',');
                foreach (string unp in unpFields1)
                {
                    if (!unpList.Contains(unp))
                    {
                        unpList.Add(unp);
                    }
                    if (pfamUnpListDict.ContainsKey (pfamId))
                    {
                        if (!pfamUnpListDict[pfamId].Contains (unp))
                        {
                            pfamUnpListDict[pfamId].Add(unp);
                        }
                    }
                    else
                    {
                        List<string> pfamUnpList = new List<string>();
                        pfamUnpList.Add(unp);
                        pfamUnpListDict.Add(pfamId, pfamUnpList);
                    }
                }

                pfamId = fields[1];
                string[] unpFields2 = fields[3].Split(',');
                foreach (string unp in unpFields2)
                {
                    if (!unpList.Contains(unp))
                    {
                        unpList.Add(unp);
                    }

                    if (pfamUnpListDict.ContainsKey(pfamId))
                    {
                        if (!pfamUnpListDict[pfamId].Contains(unp))
                        {
                            pfamUnpListDict[pfamId].Add(unp);
                        }
                    }
                    else
                    {
                        List<string> pfamUnpList = new List<string>();
                        pfamUnpList.Add(unp);
                        pfamUnpListDict.Add(pfamId, pfamUnpList);
                    }
                }
            }
            dataReader.Close();
            string[] unpPfamEntries = GetUnpPdbInPfam(pfamUnpListDict);

            dataWriter.WriteLine("#Relations = " + relCount);
            dataWriter.WriteLine("#Uniprots = " + unpList.Count);
            dataWriter.WriteLine("#Entries = " + unpPfamEntries.Length);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamUnpListDict"></param>
        /// <returns></returns>
        private string[] GetUnpPdbInPfam (Dictionary<string, List<string>> pfamUnpListDict)
        {
            List<string> unpEntryList = new List<string>();
            foreach (string pfamId in pfamUnpListDict.Keys)
            {
                string[] pfamEntries = GetUnpPdbInPfam(pfamId, pfamUnpListDict[pfamId].ToArray());
                foreach (string pdbId in pfamEntries)
                {
                    if (! unpEntryList.Contains (pdbId))
                    {
                        unpEntryList.Add(pdbId);
                    }
                }
            }
            return unpEntryList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="unpId"></param>
        /// <returns></returns>
        private string[] GetUnpPdbInPfam (string pfamId, string[] unpIds)
        {
            string queryString = "";
            DataTable unpPfamEntryTable = null;
            for (int i = 0; i < unpIds.Length; i += 300)
            {
                string[] subUnpIds = ParseHelper.GetSubArray(unpIds, i, 300);
                queryString = string.Format("Select Distinct UnpPdbfam.PdbID From PdbPfam, UnpPdbfam Where Pfam_ID = '{0}' AND UnpPdbfam.UnpId IN ({1})" +
                        " AND PdbPfam.PdbID = UnpPdbfam.PdbID AND PdbPfam.DomainID = UnpPdbfam.DomainID;", pfamId, ParseHelper.FormatSqlListString(subUnpIds));
                DataTable subUnpPfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpPfamEntryTable, ref unpPfamEntryTable);
            }
            List<string> pfamEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in unpPfamEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!pfamEntryList.Contains(pdbId))
                {
                    pfamEntryList.Add(pdbId);
                }
            }
            return pfamEntryList.ToArray ();
        }

        public void PrintUnpInteractionSumInfo(StreamWriter sumWriter)
        {
            string dataFile = Path.Combine(unpDataDir, "unpPfamInteractions.txt");

            int relCount = 0;
            int relSeqId = -1;
            List<string> unpList = new List<string>();
            List<string> pdbList = new List<string>();
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relCount++;
                string[] unpFields1 = fields[2].Split(',');
                foreach (string unp in unpFields1)
                {
                    if (!unpList.Contains(unp))
                    {
                        unpList.Add(unp);
                    }
                }

                string[] unpFields2 = fields[3].Split(',');
                foreach (string unp in unpFields2)
                {
                    if (!unpList.Contains(unp))
                    {
                        unpList.Add(unp);
                    }
                }
                if (fields.Length > 4)
                {
                    if (Int32.TryParse(fields[4], out relSeqId))
                    {
                        string[] relEntries = GetNumEntriesInRelation(relSeqId);
                        foreach (string pdbId in relEntries)
                        {
                            if (!pdbList.Contains(pdbId))
                            {
                                pdbList.Add(pdbId);
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            sumWriter.WriteLine("#Relations = " + relCount);
            sumWriter.WriteLine("#Uniprots = " + unpList.Count);
            sumWriter.WriteLine("#Entries=" + pdbList.Count);
            sumWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam1"></param>
        /// <param name="pfam2"></param>
        /// <param name="relQuery"></param>
        /// <returns></returns>
        private int GetRelSeqID(string pfam1, string pfam2, DbQuery relQuery)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation" +
                " Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}')" +
                "OR (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfam1, pfam2);
            DataTable relTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (relTable.Rows.Count > 0)
            {
                return Convert.ToInt32(relTable.Rows[0]["RelSeqID"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam"></param>
        /// <param name="relQuery"></param>
        /// <returns></returns>
        private string[] GetInteractingPfams(string pfam, DbQuery relQuery, List<string> pfamList, out List<int> relSeqIdList)
        {
            string queryString = string.Format("Select Distinct RelSeqId, FamilyCode2 As PfamId From PfamDomainFamilyRelation" +
               " Where FamilyCode1 = '{0}';", pfam);
            DataTable relTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> interPfamList = new List<string>();
            relSeqIdList = new List<int>();
            string pfamId = "";
            int relSeqId = 0;
            foreach (DataRow relRow in relTable.Rows)
            {
                pfamId = relRow["PfamId"].ToString().TrimEnd();
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                if (pfamList.Contains(pfamId))
                {
                    interPfamList.Add(pfamId);
                }
                relSeqIdList.Add(relSeqId);
            }
            queryString = string.Format("Select Distinct RelSeqId, FamilyCode1 As PfamId From PfamDomainFamilyRelation" +
               " Where FamilyCode2 = '{0}';", pfam);
            relTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow relRow in relTable.Rows)
            {
                pfamId = relRow["PfamId"].ToString().TrimEnd();
                if (pfamList.Contains(pfamId))
                {
                    if (!interPfamList.Contains(pfamId))
                    {
                        interPfamList.Add(pfamId);
                    }
                }
                if (!relSeqIdList.Contains(relSeqId))
                {
                    relSeqIdList.Add(relSeqId);
                }
            }
            return interPfamList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfam"></param>
        /// <param name="relQuery"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetInteractingPfamRelSeqIdList(string pfam, DbQuery relQuery, List<string> pfamList)
        {
            string queryString = string.Format("Select Distinct RelSeqId, FamilyCode2 As PfamId From PfamDomainFamilyRelation" +
               " Where FamilyCode1 = '{0}';", pfam);
            DataTable relTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> relSeqIdList = new List<int>();
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int, string[]>();
            string pfamId = "";
            int relSeqId = 0;
            foreach (DataRow relRow in relTable.Rows)
            {
                pfamId = relRow["PfamId"].ToString().TrimEnd();
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                if (pfamList.Contains(pfamId))
                {
                    string[] pfamPair = new string[2];
                    pfamPair[0] = pfam;
                    pfamPair[1] = pfamId;

                    relPfamPairDict.Add(relSeqId, pfamPair);
                }
            }
            queryString = string.Format("Select Distinct RelSeqId, FamilyCode1 As PfamId From PfamDomainFamilyRelation" +
               " Where FamilyCode2 = '{0}';", pfam);
            relTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow relRow in relTable.Rows)
            {
                pfamId = relRow["PfamId"].ToString().TrimEnd();
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                if (pfamList.Contains(pfamId))
                {
                    string[] pfamPair = new string[2];
                    pfamPair[0] = pfamId;
                    pfamPair[1] = pfam;
                    if (!relPfamPairDict.ContainsKey(relSeqId))
                    {
                        relPfamPairDict.Add(relSeqId, pfamPair);
                    }
                }
            }
            return relPfamPairDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relQuery"></param>
        /// <param name="M"></param>
        /// <returns></returns>
        private string GetBiggestClusterInfo(int relSeqId, DbQuery relQuery, out int M)
        {
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster;", relSeqId);
            DataTable domainClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            M = 0;
            if (domainClusterInfoTable.Rows.Count > 0)
            {
                M = Convert.ToInt32(domainClusterInfoTable.Rows[0]["NumOfCfgCluster"].ToString());
                return ParseHelper.FormatDataRow(domainClusterInfoTable.Rows[0]);
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetPfamsInPdbUnpListDict()
        {
            Dictionary<string, List<string>> pfamUnpListDict = new Dictionary<string, List<string>>();
            string queryString = "Select Distinct UnpCode, Pfam_ID From UnpPfam;";
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            string pfamId = "";
            string unpCode = "";
            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                pfamId = unpRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = unpRow["UnpCode"].ToString().TrimEnd();
                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length > 0)
                {
                    if (pfamUnpListDict.ContainsKey(pfamId))
                    {
                        List<string> unpList = pfamUnpListDict[pfamId];
                        if (!unpList.Contains(unpCode))
                        {
                            unpList.Add(unpCode);
                        }
                    }
                    else
                    {
                        List<string> unpList = new List<string>();
                        unpList.Add(unpCode);
                        pfamUnpListDict.Add(pfamId, unpList);
                    }
                }
            }

            queryString = "Select Distinct UnpCode, Pfam_ID From HumanPfam;";
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            foreach (DataRow hunpRow in humanUnpTable.Rows)
            {
                pfamId = hunpRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = hunpRow["UnpCode"].ToString().TrimEnd();

                DataRow[] dataRows = pdbPfamTable.Select(string.Format("Pfam_ID = '{0}'", pfamId));
                if (dataRows.Length > 0)
                {
                    if (pfamUnpListDict.ContainsKey(pfamId))
                    {
                        List<string> unpList = pfamUnpListDict[pfamId];
                        if (!unpList.Contains(unpCode))
                        {
                            unpList.Add(unpCode);
                        }
                    }
                    else
                    {
                        List<string> unpList = new List<string>();
                        unpList.Add(unpCode);
                        pfamUnpListDict.Add(pfamId, unpList);
                    }
                }
            }
            return pfamUnpListDict;
        }
        #endregion
        #endregion

        #region human protein pfam info
        public void PrintHumanProteinPfamInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(unpDataDir, "HumanPfamInfos.txt"), true);
            string queryString = "";
            DataTable pdbPfamTable = null;
            queryString = "Select Distinct Pfam_ID From PdbPfam;";
            pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            PrintHumanProteinPfamInfo(pdbPfamTable, dataWriter, false, false);

            queryString = "Select Distinct Pfam_ID From PdbPfam Where Pfam_ID Not Like 'Pfam-B%';";
            pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            PrintHumanProteinPfamInfo(pdbPfamTable, dataWriter, false, true);


            queryString = "Select Distinct Pfam_ID From PdbPfam Where Pfam_ID Not Like 'Pfam-B%' AND (IsWeak = '0' OR IsUpdated = '1');";
            pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            PrintHumanProteinPfamInfo(pdbPfamTable, dataWriter, true, true);

            dataWriter.Close();
        }

        public void PrintHumanProteinsInPdb()
        {
            List<string> humanSeqList = new List<string>();
            List<string> entryList = new List<string>();
            string queryString = "Select Distinct DbCode From PdbDbRefSifts Where DbCode Like '%_HUMAN'";
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow dbCodeRow in dbCodeTable.Rows)
            {
                humanSeqList.Add(dbCodeRow["DbCode"].ToString().TrimEnd());
            }

            queryString = "Select Distinct PdbID From PdbDbRefSifts Where DbCode Like '%_HUMAN'";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString());
            }

            queryString = "Select Distinct DbCode From PdbDbRefXml Where DbCode Like '%_HUMAN'";
            dbCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow dbCodeRow in dbCodeTable.Rows)
            {
                if (!humanSeqList.Contains(dbCodeRow["DbCode"].ToString().TrimEnd()))
                {
                    humanSeqList.Add(dbCodeRow["DbCode"].ToString().TrimEnd());
                }
            }

            queryString = "Select Distinct PdbID From PdbDbRefXml Where DbCode Like '%_HUMAN'";
            entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                if (!entryList.Contains(entryRow["PdbID"].ToString()))
                {
                    entryList.Add(entryRow["PdbID"].ToString());
                }
            }

        }

        private void PrintHumanProteinPfamInfo(DataTable pdbPfamTable, StreamWriter dataWriter, bool isStrong, bool isPfamA)
        {
            string pfamId = "";
            List<string> unpCodeList = new List<string>();
            List<string> unpSeqList = new List<string>();
            List<string> pfamIdList = new List<string>();
            string[][] humanSeqs = null;
            foreach (DataRow pfamIdRow in pdbPfamTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_Id"].ToString();

                humanSeqs = GetHumanProtsWithPfam(pfamId, true);

                if (humanSeqs[0].Length > 0)
                {
                    pfamIdList.Add(pfamId);
                }
                foreach (string unpCode in humanSeqs[0])
                {
                    if (!unpCodeList.Contains(unpCode))
                    {
                        unpCodeList.Add(unpCode);
                    }
                }
                foreach (string unpSeq in humanSeqs[1])
                {
                    if (!unpSeqList.Contains(unpSeq))
                    {
                        unpSeqList.Add(unpSeq);
                    }
                }
            }
            if (isPfamA)
            {
                if (isStrong)
                {
                    dataWriter.WriteLine("Pfam A only, and strong hits only.");
                }
                else
                {
                    dataWriter.WriteLine("Pfam A only");
                }
            }
            else
            {
                dataWriter.WriteLine("Total");
            }
            dataWriter.WriteLine("#PfamId in PDB: " + pdbPfamTable.Rows.Count.ToString());
            dataWriter.WriteLine("#Human Proteins: " + unpCodeList.Count.ToString());
            dataWriter.WriteLine("#Human isoform proteins: " + unpSeqList.Count.ToString());
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[][] GetHumanProtsWithPfam(string pfamId, bool isStrong)
        {
            string queryString = "";
            if (isStrong)
            {
                queryString = string.Format("Select Distinct UnpCode, Isoform From HumanPfam Where Pfam_ID = '{0}' and IsWeak = '0';", pfamId);
            }
            else
            {
                queryString = string.Format("Select Distinct UnpCode, Isoform From HumanPfam Where Pfam_ID = '{0}';", pfamId);
            }
            DataTable unpSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            string unpCodeIsoform = "";
            List<string> unpCodeList = new List<string>();
            List<string> unpSeqList = new List<string>();
            foreach (DataRow unpCodeRow in unpSeqTable.Rows)
            {
                unpCode = unpCodeRow["UnpCode"].ToString().TrimEnd();
                unpCodeIsoform = unpCode + "_" + unpCodeRow["Isoform"].ToString();
                if (!unpCodeList.Contains(unpCode))
                {
                    unpCodeList.Add(unpCode);
                }
                unpSeqList.Add(unpCodeIsoform);
            }
            string[][] humanSeqsWithPfam = new string[2][];
            humanSeqsWithPfam[0] = new string[unpCodeList.Count];
            unpCodeList.CopyTo(humanSeqsWithPfam[0]);
            humanSeqsWithPfam[1] = new string[unpSeqList.Count];
            unpSeqList.CopyTo(humanSeqsWithPfam[1]);
            return humanSeqsWithPfam;
        }
        #endregion

        #region fill out the uniprot ids
        public void FillOutUniprotIDs ()
        {
            string queryString = "Select Distinct UnpAccession From HumanPfam Where UnpAccession <> '' AND UnpCode = '';";
            DataTable missingIdUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpAccession = "";
            string unpCode = "";
            foreach (DataRow unpRow in missingIdUnpTable.Rows)
            {
                unpAccession = unpRow["UnpAccession"].ToString().TrimEnd();
                unpCode = GetUnpCode(unpAccession);
                if (unpCode != "")
                {
                    UpdateUnpCode(unpAccession, unpCode);
                }
            }
        }

        private string GetUnpCode (string unpAccession)
        {
            string queryString = string.Format("Select UnpCode From HumanSeqInfo Where UnpAccession = '{0}';", unpAccession);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpCodeTable.Rows.Count > 0)
            {
               return unpCodeTable.Rows[0]["UnpCode"].ToString().TrimEnd();
            }
            return "";
        }

        private void UpdateUnpCode (string unpAccession, string unpCode)
        {
            string updateString = string.Format("Update HumanPfam Set UnpCode = '{0}' Where UnpAccession = '{1}' AND UnpCode = '';", unpCode, unpAccession);
            dbUpdate.Update(ProtCidSettings.pdbfamDbConnection, updateString);
        }
        #endregion
    }
}
