using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using ProtCidSettingsLib;

namespace ProtCIDPaperDataLib.paper
{
    public class PdbPisaBaDataInfo  : PaperDataInfo
    {
        public void PrintMNPdbPisaUnderAnnotation()
        {
            string queryString = "Select RelSeqID, ClusterID, SurfaceArea, InPdb, InPisa, InAsu, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, NumOfEntryRelation, " +
                //      " MinSeqIdentity From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 5 and SurfaceArea >= 300 and MinSeqIdentity < 90;";
                  " MinSeqIdentity From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 5 and MinSeqIdentity < 90;";
            DataTable goodClusterTable = ProtCidSettings.protcidQuery.Query(queryString);

            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "M5Clusters_PdbPisaCoverages.txt"));
            int percent = 0;
            int numOfCluster90 = 0;
            int numOfCluster100 = 0;
            int numOfCluster80 = 0;
            int numOfCluster70 = 0;
            foreach (DataRow clusterRow in goodClusterTable.Rows)
            {
                percent = (int)(Convert.ToDouble(clusterRow["InPdb"].ToString()) / Convert.ToDouble(clusterRow["NumOfEntryCluster"].ToString()) * 100);
                if (percent == 100)
                {
                    numOfCluster100++;
                }
                if (percent >= 90)
                {
                    numOfCluster90++;
                }
                if (percent >= 80)
                {
                    numOfCluster80++;
                }
                if (percent >= 70)
                {
                    numOfCluster70++;
                }
            }
            dataWriter.WriteLine("Total #Clusters: " + goodClusterTable.Rows.Count.ToString() + " with #CFs/cluster >= 5 and minimum sequence identity < 90%");
            dataWriter.WriteLine("#Clusters InPDB == 100%: " + numOfCluster100.ToString());
            dataWriter.WriteLine("#Cluster InPDB >= 90%: " + numOfCluster90.ToString());
            dataWriter.WriteLine("#Cluster InPDB >= 80%: " + numOfCluster80.ToString());
            dataWriter.WriteLine("#Cluster InPDB >= 70%: " + numOfCluster70.ToString());

            numOfCluster90 = 0;
            numOfCluster100 = 0;
            numOfCluster80 = 0;
            numOfCluster70 = 0;
            foreach (DataRow clusterRow in goodClusterTable.Rows)
            {
                percent = (int)(Convert.ToDouble(clusterRow["InPisa"].ToString()) / Convert.ToDouble(clusterRow["NumOfEntryCluster"].ToString()) * 100);
                if (percent == 100)
                {
                    numOfCluster100++;
                }
                if (percent >= 90)
                {
                    numOfCluster90++;
                }
                if (percent >= 80)
                {
                    numOfCluster80++;
                }
                if (percent >= 70)
                {
                    numOfCluster70++;
                }
            }
            dataWriter.WriteLine("Total #Clusters: " + goodClusterTable.Rows.Count.ToString());
            dataWriter.WriteLine("#Clusters InPISA == 100%: " + numOfCluster100.ToString());
            dataWriter.WriteLine("#Cluster InPISA >= 90%: " + numOfCluster90.ToString());
            dataWriter.WriteLine("#Cluster InPISA >= 80%: " + numOfCluster80.ToString());
            dataWriter.WriteLine("#Cluster InPISA >= 70%: " + numOfCluster70.ToString());

            dataWriter.Close();

            /*
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "UnderAnnot_PdbPisaCoverages.txt"));
            int inPdb = 0;
            int inPisa = 0;
            int entryCluster = 0;
            string relationString = "";
            int relSeqId = 0;
            int pdbPercent = 0;
            int pisaPercent = 0;
            foreach (DataRow clusterRow in goodClusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString ());
                relationString = GetRelationPfamPairs(relSeqId);
                inPdb = Convert.ToInt32(clusterRow["InPdb"].ToString ());
                inPisa = Convert.ToInt32(clusterRow["InPisa"].ToString ());
                entryCluster = Convert.ToInt32(clusterRow["NumOfEntryCluster"].ToString ());
                pdbPercent = (int)(((double)inPdb / (double)entryCluster) * 100);
                pisaPercent = (int)(((double)inPisa / (double)entryCluster) * 100);
                dataWriter.WriteLine(relSeqId.ToString () + "\t" + clusterRow["ClusterID"].ToString () + "\t" +
                    relationString + "\t" + clusterRow["NumOfCfgCluster"].ToString () + "\t" + 
                    clusterRow["NumOfEntryCluster"].ToString () + "\t" + clusterRow["NumOfCfgRelation"].ToString () + "\t" +
                    clusterRow["NumOfEntryRelation"].ToString () + "\t" + pdbPercent.ToString () + "\t" + pisaPercent.ToString () + "\t" +
                    (int)Convert.ToDouble (clusterRow["SurfaceArea"].ToString()) + "\t" + clusterRow["MinSeqIdentity"].ToString ());
            }
            dataWriter.Close ();*/
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintMNAndPdbPisaCoverages()
        {
            dataDir = Path.Combine(dataDir, "PdbPisa_M");
            bool isDomain = false;  // chain
            bool needMNratio = true;  // only >= 50%
            PrintMNAndPdbPisaCoverage(isDomain, needMNratio);

            isDomain = true; // domain
            needMNratio = true; // only >= 50%
            PrintMNAndPdbPisaCoverage(isDomain, needMNratio);

            isDomain = false; // chain
            needMNratio = false;  // all
            PrintMNAndPdbPisaCoverage(isDomain, needMNratio);

            isDomain = true; // domain
            needMNratio = false;  // all
            PrintMNAndPdbPisaCoverage(isDomain, needMNratio);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isDomain"></param>
        /// <param name="needMNration"></param>
        public void PrintMNAndPdbPisaCoverage(bool isDomain, bool needMNratio)
        {
            string queryString = "";
            if (isDomain)
            {
                queryString = "Select RelSeqId, ClusterID, NumOfCfgCluster, NumOfCfgRelation, InPdb, InPisa, NumOfEntryCluster, NumOfEntryRelation " +
                   "From PfamDomainClusterSumInfo Where MinSeqIdentity < 90 AND SurfaceArea >= 300 AND NumOfCfgCluster >= 2 Order By RelSeqID, ClusterID;";
            }
            else
            {
                queryString = "Select SuperGroupSeqID As RelSeqID, ClusterID, NumOfCfgCluster, NumOfCfgFamily As NumOfCfgRelation, InPdb, InPisa, " +
                       " NumOfEntryCluster, NumOfEntryFamily As NumOfEntryRelation " +
                      "From PfamSuperClusterSumInfo Where MinSeqIdentity < 90 AND SurfaceArea >= 300 AND NumOfCfgCluster >= 2 Order By SuperGroupSeqID, ClusterID;";
            }
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);


            DataTable[] homoHeteroClusterInfoTables = SplitClusterInfoTableToHomoHeteroTables(clusterInfoTable, isDomain);
            string homoMNCoverageFile = "";
            string homoMNDetailFile = "";
            if (isDomain)
            {
                if (needMNratio)
                {
                    homoMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M50_homoDomain.txt");
                    homoMNDetailFile = Path.Combine(dataDir, "PdbPisaCov_M50_homoDomain_Detail.txt");
                }
                else
                {
                    homoMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M_homoDomain.txt");
                    homoMNDetailFile = Path.Combine(dataDir, "PdbPisaCov_M_homoDomain_Detail.txt");
                }
            }
            else
            {
                if (needMNratio)
                {
                    homoMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M50_homoChain.txt");
                    homoMNDetailFile = Path.Combine(dataDir, "PdbPisaCov_M50_homoChain_Detail.txt");
                }
                else
                {
                    homoMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M_homoChain.txt");
                    homoMNDetailFile = Path.Combine(dataDir, "PdbPisaCov_M_homoChain_Detail.txt");
                }
            }
            WriteMNPdbPisaCoveragesToFile(homoHeteroClusterInfoTables[0], homoMNCoverageFile, homoMNDetailFile, needMNratio, isDomain);


            string heteroMNCoverageFile = "";
            string heteroMNdetailFile = "";
            if (isDomain)
            {
                if (needMNratio)
                {
                    heteroMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M50_heteroDomain.txt");
                    heteroMNdetailFile = Path.Combine(dataDir, "PdbPisaCov_M50_heteroDomain_detail.txt");
                }
                else
                {
                    heteroMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M_heteroDomain.txt");
                    heteroMNdetailFile = Path.Combine(dataDir, "PdbPisaCov_M_heteroDomain_detail.txt");
                }
            }
            else
            {
                if (needMNratio)
                {
                    heteroMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M50_heteroChain.txt");
                    heteroMNdetailFile = Path.Combine(dataDir, "PdbPisaCov_M50_heteroChain_detail.txt");
                }
                else
                {
                    heteroMNCoverageFile = Path.Combine(dataDir, "PdbPisaCov_M_heteroChain.txt");
                    heteroMNdetailFile = Path.Combine(dataDir, "PdbPisaCov_M_heteroChain_detail.txt");
                }
            }
            WriteMNPdbPisaCoveragesToFile(homoHeteroClusterInfoTables[1], heteroMNCoverageFile, heteroMNdetailFile, needMNratio, isDomain);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInfoTable"></param>
        /// <param name="isDomain"></param>
        /// <returns></returns>
        private DataTable[] SplitClusterInfoTableToHomoHeteroTables(DataTable clusterInfoTable, bool isDomain)
        {
            DataTable homoClusterInfoTable = clusterInfoTable.Clone();
            DataTable heteroClusterInfoTable = clusterInfoTable.Clone();
            int relSeqId = 0;
            foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterInfoRow["RelSeqID"].ToString());
                if (IsHomoDimer(relSeqId, isDomain))
                {
                    DataRow newDataRow = homoClusterInfoTable.NewRow();
                    newDataRow.ItemArray = clusterInfoRow.ItemArray;
                    homoClusterInfoTable.Rows.Add(newDataRow);
                }
                else
                {
                    DataRow heteroRow = heteroClusterInfoTable.NewRow();
                    heteroRow.ItemArray = clusterInfoRow.ItemArray;
                    heteroClusterInfoTable.Rows.Add(heteroRow);
                }
            }
            DataTable[] homoHeterClusterInfoTables = new DataTable[2];
            homoHeterClusterInfoTables[0] = homoClusterInfoTable;
            homoHeterClusterInfoTables[1] = heteroClusterInfoTable;
            return homoHeterClusterInfoTables;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInfoTable"></param>
        /// <param name="fileName"></param>
        /// <param name="detailFile"></param>
        /// <param name="needMNratio"></param>
        /// <param name="isDomain"></param>
        private void WriteMNPdbPisaCoveragesToFile(DataTable clusterInfoTable, string fileName, string detailFile, bool needMNratio, bool isDomain)
        {
            Dictionary<int, int> MPdbHash = new Dictionary<int, int>();
            Dictionary<int, int> MPisaHash = new Dictionary<int, int>();
            Dictionary<int, int> MClusterHash = new Dictionary<int, int>();
            Dictionary<int, List<int>> MPdbPercentListHash = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> MPisaPercentListHash = new Dictionary<int, List<int>>();
            Dictionary<int, int> MNoStructPisaHash = new Dictionary<int, int>();
            int M = 0;
            int N = 0;
            int pdbNum = 0;
            int pisaNum = 0;
            int clusterNum = 0;
            double MN = 0;
            int relSeqId = 0;
            int pdbPercent = 0;
            int pisaPercent = 0;
            int structPisaPercent = 0;
            List<int> MList = new List<int>();
            int clusterId = 0;
            int numNoStructPisaEntry = 0;
            List<int> ribosomeHeterGroupList = new List<int>();
            StreamWriter detailWriter = new StreamWriter(detailFile);
            string groupString = "";
            detailWriter.WriteLine("GroupString\tRelSeqId\tClusterId\tM\tN\t#PDB\t#PISA\t#NoStructPisa\t#Cluster\tPDB%\tPISA%");
            foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterInfoRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterInfoRow["ClusterID"].ToString());
                if (ribosomeHeterGroupList.Contains(relSeqId))
                {
                    continue;
                }
                if (IsRibosomeHeteroDimer(relSeqId, isDomain, out groupString))
                {
                    ribosomeHeterGroupList.Add(relSeqId);
                    continue;
                }

                M = Convert.ToInt32(clusterInfoRow["NumOfCfgCluster"].ToString());
                N = Convert.ToInt32(clusterInfoRow["NumOfCfgRelation"].ToString());
                MN = (double)M / (double)N;
                if (needMNratio && MN < 0.50)
                {
                    continue;
                }

                numNoStructPisaEntry = GetNoStructPdbEntries(relSeqId, clusterId, isDomain);
                pdbNum = Convert.ToInt32(clusterInfoRow["InPdb"].ToString());
                pisaNum = Convert.ToInt32(clusterInfoRow["InPisa"].ToString());
                clusterNum = Convert.ToInt32(clusterInfoRow["NumOfEntryCluster"].ToString());
                if (!MList.Contains(M))
                {
                    MList.Add(M);
                }
                if (MPdbHash.ContainsKey(M))
                {
                    int numOfPdbEntry = (int)MPdbHash[M];
                    numOfPdbEntry += pdbNum;
                    MPdbHash[M] = numOfPdbEntry;
                }
                else
                {
                    MPdbHash.Add(M, pdbNum);
                }
                if (MPisaHash.ContainsKey(M))
                {
                    int numOfPisaEntry = (int)MPisaHash[M];
                    numOfPisaEntry += pisaNum;
                    MPisaHash[M] = numOfPisaEntry;
                }
                else
                {
                    MPisaHash.Add(M, pisaNum);
                }
                if (MClusterHash.ContainsKey(M))
                {
                    int numOfClusterEntry = (int)MClusterHash[M];
                    numOfClusterEntry += clusterNum;
                    MClusterHash[M] = numOfClusterEntry;
                }
                else
                {
                    MClusterHash.Add(M, clusterNum);
                }
                if (MNoStructPisaHash.ContainsKey(M))
                {
                    int numNoStuctPisa = (int)MNoStructPisaHash[M];
                    numNoStuctPisa += numNoStructPisaEntry;
                    MNoStructPisaHash[M] = numNoStuctPisa;
                }
                else
                {
                    MNoStructPisaHash.Add(M, numNoStructPisaEntry);
                }

                pdbPercent = (int)(((double)pdbNum / (double)clusterNum) * 100);
                pisaPercent = (int)(((double)pisaNum / (double)clusterNum) * 100);

                if (MPdbPercentListHash.ContainsKey(M))
                {
                    MPdbPercentListHash[M].Add(pdbPercent);
                }
                else
                {
                    List<int> pdbPercentList = new List<int>();
                    pdbPercentList.Add(pdbPercent);
                    MPdbPercentListHash.Add(M, pdbPercentList);
                }

                if (MPisaPercentListHash.ContainsKey(M))
                {
                    MPisaPercentListHash[M].Add(pisaPercent);
                }
                else
                {
                    List<int> pisaPercentList = new List<int>();
                    pisaPercentList.Add(pisaPercent);
                    MPisaPercentListHash.Add(M, pisaPercentList);
                }
                detailWriter.WriteLine(groupString + "\t" + relSeqId.ToString() + "\t" + clusterId.ToString() + "\t" + M.ToString() + "\t" + N.ToString() + "\t" +
                    pdbNum.ToString() + "\t" + pisaNum.ToString() + "\t" + numNoStructPisaEntry.ToString() + "\t" + clusterNum.ToString() + "\t" +
                    pdbPercent.ToString() + "\t" + pisaPercent.ToString());
            }
            MList.Sort();
            detailWriter.Close();
            string dataLine = "";
            StreamWriter dataWriter = new StreamWriter(fileName);
            dataWriter.WriteLine("M\tInPDB\tInPISA\tTotal\tPDBPercent\tPISAPercent_All\tNoStructPISA\tPISAPercent\tPDBPercent_Avg\tPISAPercent_Avg");
            foreach (int lsM in MList)
            {
                dataLine = lsM.ToString() + "\t";
                if (MPdbHash.ContainsKey(lsM))
                {
                    dataLine += MPdbHash[lsM].ToString() + "\t";
                    pdbPercent = (int)((double)(int)MPdbHash[lsM] / (double)(int)MClusterHash[lsM] * 100);
                }
                else
                {
                    dataLine += "0\t";
                    pdbPercent = 0;
                }
                if (MPisaHash.ContainsKey(lsM))
                {
                    dataLine += MPisaHash[lsM].ToString() + "\t";
                    pisaPercent = (int)((double)(int)MPisaHash[lsM] / (double)(int)MClusterHash[lsM] * 100);
                }
                else
                {
                    dataLine += "0\t";
                    pisaPercent = 0;
                }
                if (MClusterHash.ContainsKey(lsM))
                {
                    dataLine += MClusterHash[lsM].ToString();
                }
                else
                {
                    dataLine += "0";
                }
                dataLine += "\t" + pdbPercent.ToString() + "\t" + pisaPercent.ToString() + "\t";
                if (MNoStructPisaHash.ContainsKey(lsM))
                {
                    int structClusterPisaNum = (int)MClusterHash[lsM] - (int)MNoStructPisaHash[lsM];
                    structPisaPercent = (int)((double)(int)MPisaHash[lsM] / (double)structClusterPisaNum * 100);

                    dataLine += (MNoStructPisaHash[lsM].ToString() + "\t" + structPisaPercent.ToString());
                }
                else
                {
                    dataLine += "0\t" + pisaPercent.ToString();
                }

                pdbPercent = (int)GetAverage(MPdbPercentListHash[lsM]);
                pisaPercent = (int)GetAverage(MPisaPercentListHash[lsM]);
                dataLine += "\t" + pdbPercent.ToString() + "\t" + pisaPercent.ToString();

                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        private double GetAverage(List<int> itemList)
        {
            double sum = 0;
            foreach (int item in itemList)
            {
                sum += Convert.ToDouble(item);
            }
            double average = sum / (double)itemList.Count;
            return average;
        }

        private bool IsRibosomeHeteroDimer(int relSeqId, bool isDomain, out string groupString)
        {
            string queryString = "";
            groupString = "";
            if (isDomain)
            {
                queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = '{0}';", relSeqId);
                DataTable familyPairTable = ProtCidSettings.protcidQuery.Query(queryString);
                string familyCode1 = "";
                string familyCode2 = "";
                if (familyPairTable.Rows.Count > 0)
                {
                    familyCode1 = familyPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                    familyCode2 = familyPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                    groupString = familyCode1;
                    if (familyCode1 != familyCode2)
                    {
                        groupString = groupString + ";" + familyCode2;
                    }
                    if (familyCode1.IndexOf("Ribosom") > -1 && familyCode2.IndexOf("Ribosom") > -1)
                    {
                        return true;
                    }
                }
            }
            else
            {
                queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", relSeqId);
                DataTable chainPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
                if (chainPfamArchTable.Rows.Count > 0)
                {
                    string chainPfamArch = chainPfamArchTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
                    groupString = chainPfamArch;
                    string[] fields = chainPfamArch.Split(';');
                    if (fields.Length == 2)
                    {
                        if (fields[0].IndexOf("Ribosom") > -1 && fields[1].IndexOf("Ribosom") > -1)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="isDomain"></param>
        /// <returns></returns>
        private int GetNoStructPdbEntries(int groupId, int clusterId, bool isDomain)
        {
            string queryString = "";
            if (isDomain)
            {
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", groupId, clusterId);
            }
            else
            {
                queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1};", groupId, clusterId);
            }
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            List<string> noStructPisaEntryList = new List<string>();
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!IsPisaBuStatusOk(pdbId))
                {
                    noStructPisaEntryList.Add(pdbId);
                }
            }
            return noStructPisaEntryList.Count;
        }

        private bool IsPisaBuStatusOk(string pdbId)
        {
            string queryString = string.Format("Select Distinct Status From PisaBuStatus Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (buStatTable.Rows.Count > 0)
            {
                string status = buStatTable.Rows[0]["Status"].ToString().TrimEnd().ToUpper();
                if (status == "OK")
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
        /// <param name="isDomain"></param>
        /// <returns></returns>
        private bool IsHomoDimer(int relSeqId, bool isDomain)
        {
            string queryString = "";
            if (isDomain)
            {
                queryString = string.Format("Select * From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable domainRelTable = ProtCidSettings.protcidQuery.Query(queryString);
                string familyCode1 = domainRelTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                string familyCode2 = domainRelTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                if (familyCode1 == familyCode2)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                queryString = string.Format("Select * From PfamSuperGroups Where SuperGroupSeqId = {0};", relSeqId);
                DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                string chainRelPfamArch = chainGroupTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
                if (chainRelPfamArch.IndexOf(";") > -1)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        #region barplot format
        /// <summary>
        /// format numbers data into barplot
        /// </summary>
        public void FormatPdbPisaNumbersBarplot()
        {
            dataDir = Path.Combine(dataDir, "PdbPisa_M");
            string[] dataFiles = Directory.GetFiles(dataDir, "*_M50*.txt");
            foreach (string dataFile in dataFiles)
            {
                if (dataFile.ToLower().IndexOf("_detail.txt") > -1)
                {
                    continue;
                }
                FormatDataToBarplot(dataFile);
            }
        }

        private void FormatDataToBarplot(string dataFile)
        {
            FileInfo fileInfo = new FileInfo(dataFile);
            string barplotFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".txt", "_barplot.txt"));
            StreamWriter dataWriter = new StreamWriter(barplotFile);
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            line = dataReader.ReadLine();
            int mCutoff = 15;
            string dataLine = "";
            int m = 0;
            int inPdb = 0;
            int notInPdb = 0;
            int inPisa = 0;
            int notInPisa = 0;
            int total = 0;
            int noStructPisa = 0;
            List<int> mList = new List<int>();
            Dictionary<int, int[]> mNumberDict = new Dictionary<int, int[]>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                m = Convert.ToInt32(fields[0]);
                if (m < mCutoff)
                {
                    inPdb = Convert.ToInt32(fields[1]);
                    inPisa = Convert.ToInt32(fields[2]);
                    total = Convert.ToInt32(fields[3]);
                    noStructPisa = Convert.ToInt32(fields[6]);
                    inPisa += noStructPisa;
                    notInPdb = total - inPdb;
                    notInPisa = total - inPisa;
                    int[] numbers = new int[4];
                    numbers[0] = inPdb;
                    numbers[1] = notInPdb;
                    numbers[2] = inPisa;
                    numbers[3] = notInPisa;
                    mNumberDict.Add(m, numbers);
                    mList.Add(m);
                }
                else
                {
                    inPdb = Convert.ToInt32(fields[1]);
                    inPisa = Convert.ToInt32(fields[2]);
                    total = Convert.ToInt32(fields[3]);
                    noStructPisa = Convert.ToInt32(fields[6]);
                    inPisa += noStructPisa;
                    notInPdb = total - inPdb;
                    notInPisa = total - inPisa;
                    if (mNumberDict.ContainsKey(mCutoff))
                    {
                        mNumberDict[mCutoff][0] += inPdb;
                        mNumberDict[mCutoff][1] += notInPdb;
                        mNumberDict[mCutoff][2] += inPisa;
                        mNumberDict[mCutoff][3] += notInPisa;
                    }
                    else
                    {
                        int[] numbers = new int[4];
                        numbers[0] = inPdb;
                        numbers[1] = notInPdb;
                        numbers[2] = inPisa;
                        numbers[3] = notInPisa;
                        mNumberDict.Add(mCutoff, numbers);
                        mList.Add(mCutoff);
                    }
                }

            }
            dataReader.Close();
            dataLine = "\t";
            foreach (int lsM in mList)
            {
                dataLine += (lsM.ToString() + "\t");
            }
            dataWriter.WriteLine(dataLine.TrimEnd('\t'));
            string[] categories = { "InPDB", "NotInPDB", "InPISA", "NotInPISA" };
            for (int i = 0; i < categories.Length; i++)
            {
                dataLine = categories[i] + "\t";
                foreach (int lsM in mList)
                {
                    dataLine += (mNumberDict[lsM][i].ToString() + "\t");
                }
                dataWriter.WriteLine(dataLine.TrimEnd('\t'));
            }
            dataWriter.Close();
        }
        #endregion
    }
}
