using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.BuIO;

namespace ProtCIDPaperDataLib.paper
{
    public class BenchmarkDataInfo : PaperDataInfo
    {
        private string baBenchDir = "";
        private string tempDir = @"D:\temp_dir";
        private string crystInterfaceDir = "";
        private InterfacesComp interfaceComp = new InterfacesComp();
        private InterfaceReader interfaceReader = new InterfaceReader();

        public BenchmarkDataInfo()
        {
            baBenchDir = Path.Combine(dataDir, "BenchmarkData_EPPIC");
            if (! Directory.Exists (tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            crystInterfaceDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
        }

        #region check M/N in benchmark data sets
        public void CheckProtCidClustersInBenchmark ()
        {
            string bioAssemBenchFile = Path.Combine(baBenchDir, "Eppic_BioAssem_benchmark.txt");
            string mnInBioAssemFile = Path.Combine(baBenchDir, "MNClusterInBioAssemBenchmark.txt");
            StreamWriter mnBaWriter = new StreamWriter(mnInBioAssemFile);          

            Dictionary<string, List<string>> mnMonomerListDomainDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> mnDimerOrLargerListDomainDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> mnMonomerListChainDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> mnDimerOrLargerListChainDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader(bioAssemBenchFile);
            string line = "";
            string chainMnString = "";
            string domainMnString = "";
            List<string> chainMnList = new List<string>();
            List<string> domainMnList = new List<string>();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split();
                int[] chainClusterCfNumbers = GetProtCidChainClusterCfNumbers(fields[0]);
                int[] domainClusterCfNumbers = GetProtCidDomainClusterCfNumbers(fields[0]);
                chainMnString = chainClusterCfNumbers[0] + " " + chainClusterCfNumbers[1];
                domainMnString = domainClusterCfNumbers[0] + " " + domainClusterCfNumbers[1];
                mnBaWriter.WriteLine(line + " " + chainMnString + " " + domainMnString);
                if (!chainMnList.Contains(chainMnString))
                {
                    chainMnList.Add(chainMnString);
                }
                if (! domainMnList.Contains (domainMnString))
                {
                    domainMnList.Add(domainMnString);
                }
                if (fields[1] == "A")
                {
                    if (mnMonomerListChainDict.ContainsKey(chainMnString))
                    {
                        if (! mnMonomerListChainDict[chainMnString].Contains(fields[0]))
                        {
                            mnMonomerListChainDict[chainMnString].Add(fields[0]);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add (fields[0]);
                        mnMonomerListChainDict.Add(chainMnString, entryList);
                    }

                    if (mnMonomerListDomainDict.ContainsKey(domainMnString))
                    {
                        if (! mnMonomerListDomainDict[domainMnString].Contains(fields[0]))
                        {
                            mnMonomerListDomainDict[domainMnString].Add(fields[0]);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(fields[0]);
                        mnMonomerListDomainDict.Add(domainMnString, entryList);
                    }
                }
                else
                {
                    if (mnDimerOrLargerListChainDict.ContainsKey(chainMnString))
                    {
                        if (! mnDimerOrLargerListChainDict[chainMnString].Contains(fields[0]))
                        {
                            mnDimerOrLargerListChainDict[chainMnString].Add(fields[0]);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(fields[0]);
                        mnDimerOrLargerListChainDict.Add(chainMnString, entryList);
                    }

                    if (mnDimerOrLargerListDomainDict.ContainsKey(domainMnString))
                    {
                        if (! mnDimerOrLargerListDomainDict[domainMnString].Contains(fields[0]))
                        {
                            mnDimerOrLargerListDomainDict[domainMnString].Add(fields[0]);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(fields[0]);
                        mnDimerOrLargerListDomainDict.Add(domainMnString, entryList);
                    }
                }
            }
            mnBaWriter.Close();

            string dataLine = "";
            string mnInBioAssemSumFile = Path.Combine(baBenchDir, "MNClusterInBAbenchmarkSumInfo_chain.txt");
            StreamWriter chainSumBaWriter = new StreamWriter(mnInBioAssemSumFile);
            foreach (string lsMn in chainMnList)
            {
                dataLine = lsMn + "\t";
                if (mnMonomerListChainDict.ContainsKey(lsMn))
                {
                    dataLine += (mnMonomerListChainDict[lsMn].Count + "\t");
                }
                else
                {
                    dataLine += "1\t";
                }
                if (mnDimerOrLargerListChainDict.ContainsKey(lsMn))
                {
                    dataLine += (mnDimerOrLargerListChainDict[lsMn].Count);
                }
                else
                {
                    dataLine += "1";
                }
                chainSumBaWriter.WriteLine(dataLine);
            }
            chainSumBaWriter.Close();

            string mnInBioAssemDomainSumFile = Path.Combine(baBenchDir, "MNClusterInBAbenchmarkSumInfo_domain.txt");
            StreamWriter domainSumBaWriter = new StreamWriter(mnInBioAssemDomainSumFile);
            foreach (string lsMn in domainMnList)
            {
                dataLine = lsMn + "\t";
                if (mnMonomerListDomainDict.ContainsKey(lsMn))
                {
                    dataLine += (mnMonomerListDomainDict[lsMn].Count + "\t");
                }
                else
                {
                    dataLine += "1\t";
                }
                if (mnDimerOrLargerListDomainDict.ContainsKey(lsMn))
                {
                    dataLine += (mnDimerOrLargerListDomainDict[lsMn].Count);
                }
                else
                {
                    dataLine += "1";
                }
                domainSumBaWriter.WriteLine(dataLine);
            }
            domainSumBaWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetProtCidDomainClusterCfNumbers (string pdbId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation As NumOfCfgGroup From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo " +
                " Where PdbID = '{0}' AND PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND " +
                " PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID AND MinSeqIdentity < 90" + 
                " Order By numOfCfgCluster DESC;", pdbId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
           
            int[] cfNumbers = new int[2];
            if (clusterInfoTable.Rows.Count > 0)
            {
                cfNumbers[0] = Convert.ToInt32(clusterInfoTable.Rows[0]["NumOfCfgCluster"].ToString());
                cfNumbers[1] = Convert.ToInt32 (clusterInfoTable.Rows[0]["NumOfCfgGroup"].ToString());
            }
            else
            {
                Dictionary<int, int> relCfNumDict = GetEntryDomainGroupCfNumDict(pdbId);
                cfNumbers[0] = 1;
                cfNumbers[1] = GetBiggestCfNum(relCfNumDict);
            }
            return cfNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetProtCidChainClusterCfNumbers(string pdbId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgFamily As NumOfCfgGroup From PfamSuperClusterEntryInterfaces, PfamSuperClusterSumInfo " +
                " Where PdbID = '{0}' AND PfamSuperClusterEntryInterfaces.SuperGroupSeqID = PfamSuperClusterSumInfo.SuperGroupSeqID AND " +
                " PfamSuperClusterEntryInterfaces.ClusterID = PfamSuperClusterSumInfo.ClusterID AND MinSeqIdentity < 90" +
                " Order By numOfCfgCluster DESC;", pdbId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);

            int[] cfNumbers = new int[2];
            if (clusterInfoTable.Rows.Count > 0)
            {
                cfNumbers[0] = Convert.ToInt32(clusterInfoTable.Rows[0]["NumOfCfgCluster"].ToString());
                cfNumbers[1] = Convert.ToInt32(clusterInfoTable.Rows[0]["NumOfCfgGroup"].ToString());
            }
            else
            {
                Dictionary<int, int> relCfNumDict = GetEntryChainGroupCfNumDict (pdbId);
                cfNumbers[0] = 1;
                cfNumbers[1] = GetBiggestCfNum(relCfNumDict);
            }
            return cfNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupCfNumDict"></param>
        /// <returns></returns>
        private int GetBiggestCfNum (Dictionary<int, int> groupCfNumDict)
        {
            int maxCfNum = 1;
            foreach (int groupId in groupCfNumDict.Keys)
            {
                if (groupCfNumDict[groupId] > maxCfNum)
                {
                    maxCfNum =  groupCfNumDict[groupId];
                }
            }
            return maxCfNum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetEntryChainGroupCfNumDict (string pdbId)
        {
            Dictionary<int, int> groupCfNumDict = new Dictionary<int, int>();
            string queryString = string.Format("Select Distinct SuperGroupSeqID From PfamSuperGroups, PfamHomoSeqInfo " +
                " Where PdbID = '{0}' AND PfamHomoSeqInfo.GroupSeqID = PfamSuperGroups.GroupSeqID;", pdbId);
            DataTable superGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct SuperGroupSeqID From PfamSuperGroups, PfamHomoRepEntryAlign " +
                " Where PdbID2 = '{0}' AND PfamHomoRepEntryAlign.GroupSeqID = PfamSuperGroups.GroupSeqID;", pdbId);
            DataTable homoSuperGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> superGroupList = new List<int>();
            int chainGroupId = 0;
            foreach (DataRow dataRow in superGroupTable.Rows)
            {
                chainGroupId = Convert.ToInt32(dataRow["SuperGroupSeqID"].ToString ());
                superGroupList.Add(chainGroupId);
            }

            foreach (DataRow dataRow in homoSuperGroupTable.Rows)
            {
                chainGroupId = Convert.ToInt32(dataRow["SuperGroupSeqID"].ToString());
                if (!superGroupList.Contains(chainGroupId))
                {
                    superGroupList.Add(chainGroupId);
                }
            }
            if (superGroupList.Count == 0)
            {
                return groupCfNumDict;
            }

            queryString = string.Format("Select SuperGroupSeqID, PfamNonRedundantCfGroups.GroupSeqID, CfGroupID " +
                " From PfamSuperGroups, PfamNonRedundantCfGroups Where SuperGroupSeqID IN ({0}) AND " + 
                " PfamSuperGroups.GroupSeqID = PfamNonRedundantCfGroups.GroupSeqID;", ParseHelper.FormatSqlListString (superGroupList));
            DataTable chainGroupCfTable = ProtCidSettings.protcidQuery.Query (queryString);
            Dictionary<int, List<string>> chainGroupCfListDict = new Dictionary<int, List<string>>();
            string cfGroup = "";
            foreach (DataRow cfRow in chainGroupCfTable.Rows)
            {
                chainGroupId = Convert.ToInt32(cfRow["SuperGroupSeqID"].ToString());
                cfGroup = cfRow["GroupSeqID"].ToString() + "-" + cfRow["CfGroupID"].ToString();
                if (chainGroupCfListDict.ContainsKey (chainGroupId))
                {
                    if (! chainGroupCfListDict[chainGroupId].Contains (cfGroup))
                    {
                        chainGroupCfListDict[chainGroupId].Add(cfGroup);
                    }
                }
                else
                {
                    List<string> cfList = new List<string>();
                    cfList.Add(cfGroup);
                    chainGroupCfListDict.Add(chainGroupId, cfList);
                }
            }
            
            foreach (int groupId in  chainGroupCfListDict.Keys)
            {
                groupCfNumDict.Add(groupId, chainGroupCfListDict[groupId].Count);
            }
            return groupCfNumDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetEntryDomainGroupCfNumDict (string pdbId)
        {
            Dictionary<int, int> relCfNumDict = new Dictionary<int, int>();

            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> relSeqIdList = new List<int>();
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIdList.Add(Convert.ToInt32 (relSeqIdRow["RelSeqID"].ToString ()));
            }
            if (relSeqIdList.Count ==  0)
            {
                return relCfNumDict;
            }
            queryString = string.Format("Select RelSeqID, GroupSeqID, CfGroupID From PfamDomainInterfaces, PfamNonRedundantCfGroups " + 
                " Where RelSeqID IN ({0}) AND pfamDomainInterfaces.PdbID = PfamNonRedundantCfGroups.PdbID;", ParseHelper.FormatSqlListString (relSeqIdList));
            DataTable relCfTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<int, List<string>> relCfListDict = new Dictionary<int, List<string>>();
            int relSeqId = 0;
            string cfString = "";

            foreach (DataRow cfRow in relCfTable.Rows)
            {
                relSeqId = Convert.ToInt32(cfRow["RelSeqID"].ToString());
                cfString = cfRow["GroupSeqID"].ToString() + "-" + cfRow["CfGroupID"].ToString();
                if (relCfListDict.ContainsKey (relSeqId))
                {
                    if (! relCfListDict[relSeqId].Contains (cfString))
                    {
                        relCfListDict[relSeqId].Add(cfString);
                    }
                }
                else
                {
                    List<string> cfList = new List<string>();
                    cfList.Add(cfString);
                    relCfListDict.Add(relSeqId, cfList);
                }
            }
            
            foreach (int lsRelId in relCfListDict.Keys)
            {
                relCfNumDict.Add(lsRelId, relCfListDict[lsRelId].Count);
            }
            return relCfNumDict;
        }
        #endregion

        #region EPPIC benchmark data
        #region cluster info for eppic interfaces
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveClusterInfoEppicBenchmarkInterfaces ()
        {
            StreamWriter logWriter = new StreamWriter(Path.Combine (baBenchDir, "EppicClusterInfoLog.txt"), true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString ());
            string biolInterfaceListFile = Path.Combine(baBenchDir, "BioInterfacesBenchmarkData.txt");
            string xtalInterfaceListFile = Path.Combine(baBenchDir, "XtalInterfacesBenchmarkData.txt");
            string[] bioInterfaces = ReadEppicInterfaceList (biolInterfaceListFile);
            string[] xtalInterfaces = ReadEppicInterfaceList (xtalInterfaceListFile);
            Dictionary<string, List<int>> entryInterfaceListDict = new Dictionary<string, List<int>>();
            GetEntryInterfaceListDict(bioInterfaces, entryInterfaceListDict);
            GetEntryInterfaceListDict(xtalInterfaces, entryInterfaceListDict);
            StreamWriter interfaceCompWriter = new StreamWriter(Path.Combine (baBenchDir, "EppicCrystInterfaceCompData.txt"), true);
            interfaceCompWriter.WriteLine("EppicInterface\tCrystInterface\tQscore");
            StreamWriter interfaceMatchWriter = new StreamWriter(Path.Combine(baBenchDir, "EppicCrystInterfaceMatch.txt"), true);
            interfaceMatchWriter.WriteLine("EppicInterface\tCrystInterface\tQscore");
            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine (baBenchDir, "EppicBenchmarkInterfacesClusterInfo.txt"), true);
            clusterInfoWriter.WriteLine("EppicInterface\tCrystInterface\tDomainInterface\t" + 
                "M_chain\tN_chain\tNumPDB_cluster_chain\tNumPDB_group_chain\tMinSeqID_chain\t" +
                "M_domain\tN_domain\tNumPDB_cluster_domain\tNumPDB_group_domain\tMinSeqID_domain\tIsBio");
            string crystInterface = "";
            string pdbId = "";
            int crystInterfaceId = 0;
            string chainClusterInfo = "";
            string domainClusterInfo = "";
            string domainInterface = "";
            int totalNum = entryInterfaceListDict.Count;
            int count = 1;
            string[] errorEntries = ReadErrorEntriesFromLog();
            foreach (string eppicEntry in entryInterfaceListDict.Keys)
            {
                Console.WriteLine(count + "/" + totalNum + " " + eppicEntry);
                count++;

                if (Array.BinarySearch (errorEntries, eppicEntry) < 0)
                {
                    continue;
                }
                
                logWriter.WriteLine(count + "/" + totalNum + " " + eppicEntry);
               
                try
                {
                    Dictionary<string, string> eppicCrystInterfaceDict = FindCrystInterfacesForBenchmarkInterfaces(eppicEntry,
                        entryInterfaceListDict[eppicEntry].ToArray(), interfaceCompWriter, interfaceMatchWriter);
                    foreach (string eppicInterface in eppicCrystInterfaceDict.Keys)
                    {
                        crystInterface = eppicCrystInterfaceDict[eppicInterface];
                        pdbId = crystInterface.Substring(0, 4);
                        crystInterfaceId = Convert.ToInt32(crystInterface.Substring(4, crystInterface.Length - 4));
                        chainClusterInfo = GetInterfaceChainClusterInfo(pdbId, crystInterfaceId);
                        domainClusterInfo = GetInterfaceDomainClusterInfo(pdbId, crystInterfaceId, out domainInterface);
                        if (bioInterfaces.Contains(eppicInterface))
                        {
                            clusterInfoWriter.WriteLine(eppicInterface + "\t" + crystInterfaceId + "\t" + domainInterface + "\t" +
                                chainClusterInfo + "\t" + domainClusterInfo + "\t1");
                        }
                        else
                        {
                            clusterInfoWriter.WriteLine(eppicInterface + "\t" + crystInterfaceId + "\t" + domainInterface + "\t" +
                                chainClusterInfo + "\t" + domainClusterInfo + "\t0");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(eppicEntry + " " + ex.Message);
                    logWriter.Flush();
                }
            }
            interfaceCompWriter.Close();
            interfaceMatchWriter.Close();
            clusterInfoWriter.Close();
            logWriter.WriteLine("Done!");
            logWriter.Close();
        }

        private string[] ReadErrorEntriesFromLog ()
        {
            List<string> entryList = new List<string>();
            StreamReader entryReader = new StreamReader(Path.Combine (baBenchDir, "EppicClusterInfoLog_0.txt"));
            string line = "";
            while ((line = entryReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("Read eppic interfaces error: ") > -1)
                {
                    entryList.Add(line.Substring (0, 4));
                }
            }
            entryList.Sort();
            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveClusterInfoEppicInterfaces ()
        {
            StreamWriter logWriter = new StreamWriter(Path.Combine(baBenchDir, "EppicClusterInfoLog.txt"), true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString());

            string biolInterfaceListFile = Path.Combine(baBenchDir, "BioInterfacesBenchmarkData.txt");
            string xtalInterfaceListFile = Path.Combine(baBenchDir, "XtalInterfacesBenchmarkData.txt");
            string[] bioInterfaces = ReadEppicInterfaceList(biolInterfaceListFile);
            string[] xtalInterfaces = ReadEppicInterfaceList(xtalInterfaceListFile);
            Dictionary<string, List<string>> eppicCrystInterfaceListDict = ReadEppicMatchInterfaceListDict();

            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(baBenchDir, "EppicBenchmarkInterfacesClusterInfo_update.txt"), true);
            clusterInfoWriter.WriteLine("EppicInterface\tCrystInterface\tDomainInterface\t" +
                "M_chain\tN_chain\tNumPDB_cluster_chain\tNumPDB_group_chain\tMinSeqID_chain\t" +
                "M_domain\tN_domain\tNumPDB_cluster_domain\tNumPDB_group_domain\tMinSeqID_domain\tIsBio");

            string pdbId = "";
            string chainClusterInfo = "";
            string domainClusterInfo = "";
            string domainInterface = "";
            string chainInterface = "";
            int totalNum = eppicCrystInterfaceListDict.Count;
            int count = 1;
            string[] errorEntries = ReadErrorEntriesFromLog();
            foreach (string eppicInterface in eppicCrystInterfaceListDict.Keys)
            {
                Console.WriteLine(count + "/" + totalNum + " " + eppicInterface);
                count++;

                logWriter.WriteLine(count + "/" + totalNum + " " + eppicInterface);

                try
                {
                    List<string> crystInterfaceList = eppicCrystInterfaceListDict[eppicInterface];
                    List<int> interfaceIdList = new List<int>();
                    foreach (string crystInterface in crystInterfaceList)
                    {
                        interfaceIdList.Add(Convert.ToInt32(crystInterface.Substring(4, crystInterface.Length - 4)));
                    }
                    pdbId = crystInterfaceList[0].Substring(0, 4);
                    //          crystInterfaceId = Convert.ToInt32(crystInterface.Substring(4, crystInterface.Length - 4));
                    chainClusterInfo = GetInterfaceChainClusterInfo(pdbId, interfaceIdList.ToArray(), out chainInterface);
                    domainClusterInfo = GetInterfaceDomainClusterInfo(pdbId, interfaceIdList.ToArray(), out domainInterface);
                    if (bioInterfaces.Contains(eppicInterface))
                    {
                        clusterInfoWriter.WriteLine(eppicInterface + "\t" + chainInterface + "\t" + domainInterface + "\t" +
                            chainClusterInfo + "\t" + domainClusterInfo + "\t1");
                    }
                    else
                    {
                        clusterInfoWriter.WriteLine(eppicInterface + "\t" + chainInterface + "\t" + domainInterface + "\t" +
                            chainClusterInfo + "\t" + domainClusterInfo + "\t0");
                    }

                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(eppicInterface + " " + ex.Message);
                    logWriter.Flush();
                }
            }
            clusterInfoWriter.Close();
            logWriter.WriteLine("Done!");
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadEppicMatchInterfaceListDict ()
        {
            double simQscoreCutoff = 0.89;
            Dictionary<string, List<string>> eppicMatchInterfaceListDict = new Dictionary<string, List<string>>();
            Dictionary<string, string> eppicMaxInterfaceDict = new Dictionary<string, string>();
            Dictionary<string, double> eppicMaxQscoreDict = new Dictionary<string, double>();
            StreamReader dataReader = new StreamReader (Path.Combine (baBenchDir, "EppicCrystInterfaceCompData.txt"));
            string line = dataReader.ReadLine();  // header line
            string eppicInterface = "";
            string crystInterface = "";
            double qscore = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                eppicInterface = fields[0];
                crystInterface = fields[1];
                qscore = Convert.ToDouble(fields[2]);
                if (qscore >= simQscoreCutoff)
                {
                    if (eppicMatchInterfaceListDict.ContainsKey (eppicInterface))
                    {
                        eppicMatchInterfaceListDict[eppicInterface].Add(crystInterface);
                    }
                    else
                    {
                        List<string> crystInterfaceList = new List<string>();
                        crystInterfaceList.Add(crystInterface);
                        eppicMatchInterfaceListDict.Add(eppicInterface, crystInterfaceList);
                    }
                }
                if (eppicMaxQscoreDict.ContainsKey(eppicInterface))
                {
                    if (eppicMaxQscoreDict[eppicInterface] < qscore)
                    {
                        eppicMaxQscoreDict[eppicInterface] = qscore;
                        eppicMaxInterfaceDict[eppicInterface] = crystInterface;
                    }
                }
                else
                {
                    eppicMaxQscoreDict.Add(eppicInterface, qscore);
                    eppicMaxInterfaceDict.Add(eppicInterface, crystInterface);
                }
            }
            dataReader.Close();
            foreach (string lsEppicInterface in eppicMaxInterfaceDict.Keys)
            {
                if (! eppicMatchInterfaceListDict.ContainsKey (lsEppicInterface))
                {
                    List<string> crystInterfaceList = new List<string>();
                    crystInterfaceList.Add(eppicMaxInterfaceDict[lsEppicInterface]);
                    eppicMatchInterfaceListDict.Add(lsEppicInterface, crystInterfaceList);
                }
            }

            return eppicMatchInterfaceListDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceChainClusterInfo (string pdbId, int crystInterfaceId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgFamily, NumOfEntryFamily, MinSeqIdentity " +
                "  From PfamSuperClusterEntryInterfaces, PfamSuperClusterSumInfo Where PdbID = '{0}' AND InterfaceID = {1} AND " +
                " PfamSuperClusterEntryInterfaces.SuperGroupSeqID = PfamSuperClusterSumInfo.SuperGroupSeqID AND " +
                " PfamSuperClusterEntryInterfaces.ClusterID = PfamSuperClusterSumInfo.ClusterID Order By NumOfCfgCluster DESC;", pdbId, crystInterfaceId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string clusterInfoString = "\t \t \t \t";
            if (clusterInfoTable.Rows.Count > 0)
            {
                clusterInfoString = clusterInfoTable.Rows[0]["NumOfCfgCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfCfgFamily"] + "\t" +
                    clusterInfoTable.Rows[0]["NumOfEntryCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfEntryFamily"] + "\t" +
                    clusterInfoTable.Rows[0]["MinSeqIdentity"];
            }
            else
            {
                int[] groupCfEntryNumbers = GetEntryChainGroupCfNumbers (pdbId, crystInterfaceId);
               clusterInfoString = "1\t" + groupCfEntryNumbers[0] + "\t1\t" + groupCfEntryNumbers[1] + "\t100";
            }
            return clusterInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceChainClusterInfo(string pdbId, int[] crystInterfaceIds, out string chainInterface)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgFamily, NumOfEntryFamily, MinSeqIdentity, PdbId, InterfaceID " +
                "  From PfamSuperClusterEntryInterfaces, PfamSuperClusterSumInfo Where PdbID = '{0}' AND InterfaceID IN ({1}) AND " +
                " PfamSuperClusterEntryInterfaces.SuperGroupSeqID = PfamSuperClusterSumInfo.SuperGroupSeqID AND " +
                " PfamSuperClusterEntryInterfaces.ClusterID = PfamSuperClusterSumInfo.ClusterID Order By NumOfCfgCluster DESC;", pdbId, ParseHelper.FormatSqlListString (crystInterfaceIds));
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string clusterInfoString = "\t \t \t \t";
            chainInterface = "";
            if (clusterInfoTable.Rows.Count > 0)
            {
                clusterInfoString = clusterInfoTable.Rows[0]["NumOfCfgCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfCfgFamily"] + "\t" +
                    clusterInfoTable.Rows[0]["NumOfEntryCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfEntryFamily"] + "\t" +
                    clusterInfoTable.Rows[0]["MinSeqIdentity"];
                chainInterface = clusterInfoTable.Rows[0]["PdbID"].ToString () + clusterInfoTable.Rows[0]["InterfaceID"].ToString ();
            }
            else
            {
                int selectedInterfaceId = 0;
                int[] groupCfEntryNumbers = GetEntryChainGroupCfNumbers (pdbId, crystInterfaceIds, out selectedInterfaceId);
                clusterInfoString = "1\t" + groupCfEntryNumbers[0] + "\t1\t" + groupCfEntryNumbers[1] + "\t100";
                chainInterface = pdbId + selectedInterfaceId.ToString();
            }
            return clusterInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryChainGroupCfNumbers (string pdbId, int chainInterfaceId)
        {
            int chainGroupId = GetChainInterfaceSuperGroupId(pdbId, chainInterfaceId);
            int[] groupCfEntryNumbers = new int[2];

            if (chainGroupId < 1)
            {
                return groupCfEntryNumbers;
            }

            string queryString = string.Format("Select SuperGroupSeqID, PfamNonRedundantCfGroups.GroupSeqID, CfGroupID " +
                " From PfamSuperGroups, PfamNonRedundantCfGroups Where SuperGroupSeqID = {0} AND " +
                " PfamSuperGroups.GroupSeqID = PfamNonRedundantCfGroups.GroupSeqID;", chainGroupId);
            DataTable chainGroupCfTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> cfList = new List<string>();
            string cfGroup = "";
            foreach (DataRow cfRow in chainGroupCfTable.Rows)
            {
                chainGroupId = Convert.ToInt32(cfRow["SuperGroupSeqID"].ToString());
                cfGroup = cfRow["GroupSeqID"].ToString() + "-" + cfRow["CfGroupID"].ToString();
                cfList.Add(cfGroup);
            }
            queryString = string.Format("Select SuperGroupSeqID, Count(Distinct PdbID) As EntryCount From PfamSuperGroups, PfamHomoSeqInfo " +
                " Where SuperGroupSeqID = {0} AND PfamHomoSeqInfo.GroupSeqID = PfamSuperGroups.GroupSeqID;", chainGroupId);
            DataTable superGroupEntryNumTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select SuperGroupSeqID, Count(Distinct PdbID2) As EntryCount From PfamSuperGroups, PfamHomoRepEntryAlign " +
                " Where SuperGroupSeqID = {0} AND  PfamHomoRepEntryAlign.GroupSeqID = PfamSuperGroups.GroupSeqID;", chainGroupId);
            DataTable superGroupHomoEntryNumTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupEntryNum = 0;

            groupCfEntryNumbers[0] = cfList.Count;

            DataRow[] groupEntryNumRows = superGroupEntryNumTable.Select(string.Format("SuperGroupSeqID = {0}", chainGroupId));
            groupEntryNum = Convert.ToInt32(groupEntryNumRows[0]["EntryCount"].ToString());
            DataRow[] groupHomoEntryNumRows = superGroupHomoEntryNumTable.Select(string.Format("SuperGroupSeqID = {0}", chainGroupId));
            if (groupHomoEntryNumRows.Length > 0)
            {
                groupEntryNum += Convert.ToInt32(groupHomoEntryNumRows[0]["EntryCount"].ToString());
            }
            groupCfEntryNumbers[1] = groupEntryNum;

            return groupCfEntryNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryChainGroupCfNumbers (string pdbId, int[] chainInterfaceIds, out int selectInterfaceId)
        {
            int[] groupCfEntryNumbers = new int[2];
            selectInterfaceId = 0;
            foreach (int interfaceId in chainInterfaceIds)
            {
                int[] cfEntryNumbers = GetEntryChainGroupCfNumbers (pdbId, interfaceId);
                if (groupCfEntryNumbers[0] < cfEntryNumbers[0])
                {
                    groupCfEntryNumbers[0] = cfEntryNumbers[0];
                    groupCfEntryNumbers[1] = cfEntryNumbers[1];
                    selectInterfaceId = interfaceId;
                }
            }
            
            return groupCfEntryNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int GetChainInterfaceSuperGroupId (string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select Distinct EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable entityTable = ProtCidSettings.protcidQuery.Query(queryString);
            int entityId1 = Convert.ToInt32 (entityTable.Rows[0]["EntityID1"].ToString());
            int entityId2 = Convert.ToInt32(entityTable.Rows[0]["EntityID2"].ToString ());
            string entityPfamArch1 = pfamArch.GetEntityGroupPfamArch(pdbId, entityId1);
            string relChainPfamArch = "(" + entityPfamArch1 + ")";
            string entityPfamArch2 = "";
            if (entityId1 != entityId2)
            {
                entityPfamArch2 = pfamArch.GetEntityGroupPfamArch(pdbId, entityId2);
                relChainPfamArch += (";(" + entityPfamArch2 + ")"); 
            }
            int chainGroupId = 0;
            queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where ChainRelPfamArch = '{0}';", relChainPfamArch);
            DataTable chainGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (chainGroupIdTable.Rows.Count > 0)
            {
                chainGroupId = Convert.ToInt32(chainGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString ());
            }
            return chainGroupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceDomainClusterInfo (string pdbId, int crystInterfaceId, out string domainInterface)
        {
            string clusterInfoString = "\t \t \t \t";
            int[] domainInterfaceIds = GetDomainInterfaceIds(pdbId, crystInterfaceId);
            domainInterface = "";
            if (domainInterfaceIds.Length == 0)
            {
                return clusterInfoString;
            }
            string queryString = string.Format("Select PfamDomainClusterSumInfo.RelSeqID As RelSeqID, PfamDomainClusterSumInfo.ClusterID, " +
                " NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, MinSeqIdentity, PdbID, DomainInterfaceID " +
                "  From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo Where PdbID = '{0}' AND DomainInterfaceID IN ({1}) AND " +
                " PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND " +
                " PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID  Order By NumOfCfgCluster DESC;", 
                pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);          
            
            if (clusterInfoTable.Rows.Count > 0)
            {
                clusterInfoString = clusterInfoTable.Rows[0]["NumOfCfgCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfCfgRelation"] + "\t" +
                    clusterInfoTable.Rows[0]["NumOfEntryCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfEntryRelation"] + "\t" +
                    clusterInfoTable.Rows[0]["MinSeqIdentity"];
                domainInterface = clusterInfoTable.Rows[0]["PdbID"].ToString () + clusterInfoTable.Rows[0]["DomainInterfaceID"].ToString ();
            }
            else
            {
                int maxCfNum = 0;
                int maxEntryNum = 0;
                int maxRelSeqId = 0;
                Dictionary<int, int[]> relCfEntryNumDict = GetInterfaceDomainGroupInfo (pdbId, domainInterfaceIds);
                foreach (int relSeqId in relCfEntryNumDict.Keys)
                {
                    if (maxCfNum < relCfEntryNumDict[relSeqId][0])
                    {
                        maxCfNum = relCfEntryNumDict[relSeqId][0];
                        maxEntryNum = relCfEntryNumDict[relSeqId][1];
                        maxRelSeqId = relSeqId;
                    }
                }
                domainInterface = GetRelDomainInterface(maxRelSeqId, pdbId);
                clusterInfoString = "1\t" + maxCfNum + "\t1\t" + maxEntryNum + "\t100";               
            }
            return clusterInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceDomainClusterInfo(string pdbId, int[] crystInterfaceIds, out string domainInterface)
        {
            string clusterInfoString = "\t \t \t \t";
            int[] domainInterfaceIds = GetDomainInterfaceIds(pdbId, crystInterfaceIds);
            domainInterface = "";
            if (domainInterfaceIds.Length == 0)
            {
                return clusterInfoString;
            }
            string queryString = string.Format("Select PfamDomainClusterSumInfo.RelSeqID As RelSeqID, PfamDomainClusterSumInfo.ClusterID, " +
                " NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, MinSeqIdentity, PdbID, DomainInterfaceID " +
                "  From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo Where PdbID = '{0}' AND DomainInterfaceID IN ({1}) AND " +
                " PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND " +
                " PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID  Order By NumOfCfgCluster DESC;",
                pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);

            if (clusterInfoTable.Rows.Count > 0)
            {
                clusterInfoString = clusterInfoTable.Rows[0]["NumOfCfgCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfCfgRelation"] + "\t" +
                    clusterInfoTable.Rows[0]["NumOfEntryCluster"] + "\t" + clusterInfoTable.Rows[0]["NumOfEntryRelation"] + "\t" +
                    clusterInfoTable.Rows[0]["MinSeqIdentity"];
                domainInterface = clusterInfoTable.Rows[0]["PdbID"].ToString() + clusterInfoTable.Rows[0]["DomainInterfaceID"].ToString();
            }
            else
            {
                int maxCfNum = 0;
                int maxEntryNum = 0;
                int maxRelSeqId = 0;
                Dictionary<int, int[]> relCfEntryNumDict = GetInterfaceDomainGroupInfo(pdbId, domainInterfaceIds);
                foreach (int relSeqId in relCfEntryNumDict.Keys)
                {
                    if (maxCfNum < relCfEntryNumDict[relSeqId][0])
                    {
                        maxCfNum = relCfEntryNumDict[relSeqId][0];
                        maxEntryNum = relCfEntryNumDict[relSeqId][1];
                        maxRelSeqId = relSeqId;
                    }
                }
                domainInterface = GetRelDomainInterface(maxRelSeqId, pdbId);
                clusterInfoString = "1\t" + maxCfNum + "\t1\t" + maxEntryNum + "\t100";
            }
            return clusterInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private Dictionary<int, int[]> GetInterfaceDomainGroupInfo(string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID IN ({1});",
                pdbId, ParseHelper.FormatSqlListString (domainInterfaceIds));
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);

            Dictionary<int, int[]> relCfEntryNumDict = new Dictionary<int, int[]>();

            List<int> relSeqIdList = new List<int>();
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIdList.Add(Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString()));
            }
            if (relSeqIdList.Count == 0)
            {
                return relCfEntryNumDict;
            }
            queryString = string.Format("Select RelSeqID, GroupSeqID, CfGroupID From PfamDomainInterfaces, PfamNonRedundantCfGroups " +
                " Where RelSeqID IN ({0}) AND pfamDomainInterfaces.PdbID = PfamNonRedundantCfGroups.PdbID;", ParseHelper.FormatSqlListString(relSeqIdList));
            DataTable relCfTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<int, List<string>> relCfListDict = new Dictionary<int, List<string>>();
            int relSeqId = 0;
            string cfString = "";

            foreach (DataRow cfRow in relCfTable.Rows)
            {
                relSeqId = Convert.ToInt32(cfRow["RelSeqID"].ToString());
                cfString = cfRow["GroupSeqID"].ToString() + "-" + cfRow["CfGroupID"].ToString();
                if (relCfListDict.ContainsKey(relSeqId))
                {
                    if (!relCfListDict[relSeqId].Contains(cfString))
                    {
                        relCfListDict[relSeqId].Add(cfString);
                    }
                }
                else
                {
                    List<string> cfList = new List<string>();
                    cfList.Add(cfString);
                    relCfListDict.Add(relSeqId, cfList);
                }
            }
            queryString = string.Format("Select RelSeqID, Count(Distinct PdbID) As EntryCount From PfamDomainInterfaces Where RelSeqID IN ({0}) Group By RelSeqID;", 
                ParseHelper.FormatSqlListString(relSeqIdList));
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

            foreach (int lsRelId in relCfListDict.Keys)
            {
                int[] relCfEntryNumbers = new int[2];
                relCfEntryNumbers[0] = relCfListDict[lsRelId].Count;
                DataRow[] relEntryNumRows = relEntryTable.Select (string.Format ("RelSeqID = '{0}'", lsRelId));
                relCfEntryNumbers[1] = Convert.ToInt32 (relEntryNumRows[0]["EntryCount"].ToString ());
                relCfEntryNumDict.Add(lsRelId, relCfEntryNumbers);
            }
            return relCfEntryNumDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetRelDomainInterface (int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string domainInterface = pdbId + domainInterfaceIdTable.Rows[0]["DomainInterfaceID"];
            return domainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <returns></returns>
        private string[] GetInterfaceDomainClusterInfos(string pdbId, int crystInterfaceId)
        {
            int[] domainInterfaceIds = GetDomainInterfaceIds(pdbId, crystInterfaceId);
            if (domainInterfaceIds.Length == 0)
            {
                return null;
            }
            string queryString = string.Format("Select PfamDomainClusterSumInfo.RelSeqID As RelSeqID, PfamDomainClusterSumInfo.ClusterID, " + 
                " NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, MinSeqIdentity " +
                "  From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo Where PdbID = '{0}' AND DomainInterfaceID IN ({1}) AND " + 
                " PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND " + 
                " PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID  Order By RelSeqID ASC, NumOfCfgCluster DESC;", 
         //       " PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID  Order By NumOfCfgCluster DESC;", 
                pdbId, ParseHelper.FormatSqlListString (domainInterfaceIds));
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> clusterInfoList = new List<string>();
            string clusterInfoString = "";
            int relSeqId = 0;
            int preRelSeqId = 0;
            foreach (DataRow clusterRow in clusterInfoTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString ());
                if (preRelSeqId != relSeqId)
                {
                    clusterInfoString = clusterInfoTable.Rows[0]["NumOfCfgCluster"] + "/" + clusterInfoTable.Rows[0]["NumOfCfgRelation"] + " " +
                        clusterInfoTable.Rows[0]["NumOfEntryCluster"] + "/" + clusterInfoTable.Rows[0]["NumOfEntryRelation"] + " " +
                        clusterInfoTable.Rows[0]["MinSeqIdentity"];
                    clusterInfoList.Add(clusterInfoString);
                }
                preRelSeqId = relSeqId;
            }
            return clusterInfoList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIds (string pdbId, int chainInterfaceId)
        {
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, chainInterfaceId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] domainInterfaceIds = new int[domainInterfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceIds[count] = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString ());
                count++;
            }
            return domainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIds(string pdbId, int[] chainInterfaceIds)
        {
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString (chainInterfaceIds));
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] domainInterfaceIds = new int[domainInterfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceIds[count] = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString());
                count++;
            }
            return domainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        private Dictionary<string, string> FindCrystInterfacesForBenchmarkInterfaces (string pdbId, int[] interfaceIds, 
            StreamWriter interfaceCompWriter, StreamWriter interfaceMatchWriter)
        {
            InterfaceChains[] eppicInterfaces = null;
            InterfaceChains[] crystInterfaces = null;
            try
            {
                crystInterfaces = ReadCrystInterfaces(pdbId);
            }
            catch (Exception ex)
            {
                throw new Exception("Read cryst interfaces error: " + ex.Message);
            }

            try
            {
                eppicInterfaces = ReadEppicInterfaces(pdbId, interfaceIds);  
            }
            catch (Exception ex)
            {
                throw new Exception("Read eppic interfaces error: " + ex.Message);                
            }
            
            InterfacePairInfo[] compInfos = null;
            try
            {
                compInfos = interfaceComp.CompareInterfacesBetweenCrystals(eppicInterfaces, crystInterfaces);
            }
            catch (Exception ex)
            {
                throw new Exception("Compare eppic interfaces and cryst interfaces error: " + ex.Message);
            }
            Dictionary<string, string> eppicCrystInterfaceDict = new Dictionary<string, string>();
            Dictionary<string, double> eppicMaxQscoreDict = new Dictionary<string, double>();
            string eppicInterface = "";
            string crystInterface = "";
            foreach (InterfacePairInfo compInfo in compInfos)
            {
                interfaceCompWriter.WriteLine(FormatInterfaceCompInfo(compInfo));
                eppicInterface = compInfo.interfaceInfo1.pdbId + compInfo.interfaceInfo1.interfaceId;
                crystInterface = compInfo.interfaceInfo2.pdbId + compInfo.interfaceInfo2.interfaceId;
                if (eppicMaxQscoreDict.ContainsKey (eppicInterface))
                {
                    if (eppicMaxQscoreDict[eppicInterface] < compInfo.qScore)
                    {
                        eppicMaxQscoreDict[eppicInterface] = compInfo.qScore;
                        eppicCrystInterfaceDict[eppicInterface] = crystInterface;
                    }
                }
                else
                {
                    eppicMaxQscoreDict.Add(eppicInterface, compInfo.qScore);
                    eppicCrystInterfaceDict.Add(eppicInterface, crystInterface);
                }
            }
            interfaceCompWriter.Flush();
            foreach (string lsInterface in eppicCrystInterfaceDict.Keys)
            {
                interfaceMatchWriter.WriteLine(lsInterface + "\t" + eppicCrystInterfaceDict[lsInterface] + "\t" + eppicMaxQscoreDict[lsInterface]);
            }
            interfaceMatchWriter.Flush();
            return eppicCrystInterfaceDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="compInfo"></param>
        /// <returns></returns>
        private string FormatInterfaceCompInfo (InterfacePairInfo compInfo)
        {
            string compInfoString = compInfo.interfaceInfo1.pdbId + compInfo.interfaceInfo1.interfaceId + "\t" +
                compInfo.interfaceInfo2.pdbId + compInfo.interfaceInfo2.interfaceId + "\t" + compInfo.qScore;
            return compInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        /// <returns></returns>
        private InterfaceChains[] ReadEppicInterfaces (string pdbId, int[] interfaceIds)
        {
            string interfaceCifFile = "";
            InterfaceChains[] eppicInterfaces = new InterfaceChains[interfaceIds.Length];
            for (int i = 0; i < interfaceIds.Length; i++)
            {
                interfaceCifFile = Path.Combine(baBenchDir, "BenchmarkInterfaces\\" + pdbId + interfaceIds[i] + ".cif.gz");
                InterfaceChains eppicInterface = ReadInterfaceFromEppicCif(interfaceCifFile);
                eppicInterfaces[i] = eppicInterface;
            }
            return eppicInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private InterfaceChains[] ReadCrystInterfaces(string pdbId)
        {
            string entryCrystInterfaceHashDir = Path.Combine(crystInterfaceDir, pdbId.Substring(1, 2));
            string[] crystInterfaceFiles = Directory.GetFiles(entryCrystInterfaceHashDir, pdbId + "*");
            InterfaceChains[] crystInterfaces = new InterfaceChains[crystInterfaceFiles.Length];
            string tempCrystInterfaceFile = "";
            for (int i = 0; i < crystInterfaceFiles.Length; i++)
            {
                tempCrystInterfaceFile = ParseHelper.UnZipFile(crystInterfaceFiles[i], tempDir);
                InterfaceChains crystInterface = new InterfaceChains();
                string[] remarks = interfaceReader.ReadInterfaceFromFile(tempCrystInterfaceFile, ref crystInterface);
                crystInterface.GetInterfaceResidueDist();
                FileInfo fileInfo = new FileInfo(tempCrystInterfaceFile);
                crystInterface.pdbId = pdbId;
                crystInterfaces[i] = crystInterface;
                File.Delete(tempCrystInterfaceFile);
            }
            return crystInterfaces;
        }

        /*
         * _atom_site.group_PDB
        _atom_site.id                     1
        _atom_site.type_symbol            2
        _atom_site.label_atom_id          3
        _atom_site.label_alt_id
        _atom_site.label_comp_id          5
        _atom_site.label_asym_id          6
        _atom_site.label_entity_id        
        _atom_site.label_seq_id           8
        _atom_site.pdbx_PDB_ins_code
        _atom_site.Cartn_x   10
        _atom_site.Cartn_y   11
        _atom_site.Cartn_z   12
        _atom_site.occupancy
        _atom_site.B_iso_or_equiv
        _atom_site.Cartn_x_esd
        _atom_site.Cartn_y_esd
        _atom_site.Cartn_z_esd
        _atom_site.occupancy_esd
        _atom_site.B_iso_or_equiv_esd
        _atom_site.pdbx_formal_charge
        _atom_site.auth_seq_id
        _atom_site.auth_comp_id
        _atom_site.auth_asym_id
        _atom_site.auth_atom_id
        _atom_site.pdbx_PDB_model_num
         * */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="eppicInterfaceCifFile"></param>
        /// <returns></returns>
        private InterfaceChains ReadInterfaceFromEppicCif (string eppicInterfaceCifFile)
        {
            string interfaceCifFile = eppicInterfaceCifFile;
            bool deleteTempInterfaceFile = false;
            if (eppicInterfaceCifFile.IndexOf (".gz") > -1)
            {
                interfaceCifFile = ParseHelper.UnZipFile(eppicInterfaceCifFile, temp_dir);
                deleteTempInterfaceFile = true;
            }
            StreamReader interfaceReader = new StreamReader(interfaceCifFile);
            string line = "";
            InterfaceChains eppicInterface = new InterfaceChains();
            FileInfo fileInfo = new FileInfo (eppicInterfaceCifFile);
            eppicInterface.pdbId = fileInfo.Name.Substring(0, 4);
            eppicInterface.interfaceId = Convert.ToInt32(fileInfo.Name.Substring (4, fileInfo.Name.IndexOf (".cif") - 4));
            List<string> cifFieldList = new List<string>();
            string field = "";
            string fieldPrefix = "_atom_site.";
            List<AtomInfo> chainAtomList = new List<AtomInfo>();            
            string asymId = "";
            string preAsymId = "";
            while ((line = interfaceReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("_atom_site.") > -1)
                {
                    field = line.Substring(fieldPrefix.Length, line.Length - fieldPrefix.Length);
                    cifFieldList.Add(field);
                }
                if (line.Length > 4 && line.Substring(0, 4) == "ATOM")
                {
                    string[] atomFields = ParseHelper.SplitPlus(line, ' ');
                    asymId = atomFields[6];
                    if (preAsymId != asymId && preAsymId != "")
                    {
                        eppicInterface.chain1 = chainAtomList.ToArray();
                        chainAtomList = new List<AtomInfo>();
                    }

                    AtomInfo atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomType = atomFields[2];
                    atom.atomName = atomFields[3];
                    atom.residue = atomFields[5];
                    atom.seqId = atomFields[8];
                    atom.xyz.X = Convert.ToDouble(atomFields[10]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[11]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[12]);
                    chainAtomList.Add(atom);
                    preAsymId = asymId;
                }
            }
            eppicInterface.chain2 = chainAtomList.ToArray();
            interfaceReader.Close();

            eppicInterface.GetInterfaceResidueDist();

            if (deleteTempInterfaceFile)
            {
                File.Delete(interfaceCifFile);
            }
            return eppicInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="listFile"></param>
        /// <returns></returns>
        private string[] ReadEppicInterfaceList (string listFile)
        {
            List<string> interfaceList = new List<string>();
            StreamReader dataReader = new StreamReader(listFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                interfaceList.Add(line);
            }
            dataReader.Close();
            return interfaceList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eppicInterfaces"></param>
        /// <param name="entryInterfaceListDict"></param>
        private void GetEntryInterfaceListDict (string[] eppicInterfaces, Dictionary<string, List<int>> entryInterfaceListDict)
        {
            if (entryInterfaceListDict == null)
            {
                entryInterfaceListDict = new Dictionary<string, List<int>>();
            }
            string pdbId = "";
            int interfaceId = 0;
            foreach (string benchInterface in eppicInterfaces)
            {
                pdbId = benchInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(benchInterface.Substring (4, benchInterface.Length - 4));
                if (entryInterfaceListDict.ContainsKey (pdbId))
                {
                    entryInterfaceListDict[pdbId].Add(interfaceId);
                }
                else
                {
                    List<int> interfaceIdList = new List<int>();
                    interfaceIdList.Add(interfaceId);
                    entryInterfaceListDict.Add(pdbId, interfaceIdList);
                }
            }           
        }
        #endregion

        #region summary data
        private string[] chainColumns = {"M_chain", "N_chain"};
        private string[] domainColumns = { "M_domain", "N_domain"};
        public void GetClusterInfoSummaryData ()
        {
            string eppicClusterInfoFile = Path.Combine(baBenchDir, "EppicBenchmarkInterfacesClusterInfo_update.txt");          
            StreamReader dataReader = new StreamReader(eppicClusterInfoFile);
            string line = dataReader.ReadLine ();
            string[] headerFields = line.Split('\t');
            int chainMindex = Array.IndexOf (headerFields, chainColumns[0]);
            int chainNindex = Array.IndexOf (headerFields, chainColumns[1]);
            int domainMindex = Array.IndexOf (headerFields, domainColumns[0]);
            int domainNindex = Array.IndexOf (headerFields, domainColumns[1]);
            Dictionary<string, int> chainMNBioInterfaceNumDict = new Dictionary<string, int>();
            Dictionary<string, int> chainMNXtalInterfaceNumDict = new Dictionary<string, int>();
            Dictionary<string, int> domainMNBioInterfaceNumDict = new Dictionary<string, int>();
            Dictionary<string, int> domainMNXtalInterfaceNumDict = new Dictionary<string, int>();

            Dictionary<string, int> chainMNBioInterfaceEntryNumDict = new Dictionary<string, int>();
            Dictionary<string, int> chainMNXtalInterfaceEntryNumDict = new Dictionary<string, int>();
            Dictionary<string, int> domainMNBioInterfaceEntryNumDict = new Dictionary<string, int>();
            Dictionary<string, int> domainMNXtalInterfaceEntryNumDict = new Dictionary<string, int>();
            List<string> entryList = new List<string>();
            string chainMN = "";
            string domainMN = "";
            string pdbId = "";
            string isBio = "1";
            List<string> chainMNlist = new List<string>();
            List<string> domainMNlist = new List<string>();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                pdbId = fields[0].Substring(0, 4);
                isBio = fields[fields.Length - 1];
                #region for bio interface
                if (isBio == "1")  
                {
                    if (fields[chainMindex] != "")
                    {
                        if (fields[chainNindex] == "0")
                        {
                            chainMN = fields[chainMindex] + "\t" + fields[chainMindex];
                        }
                        else
                        {
                            chainMN = fields[chainMindex] + "\t" + fields[chainNindex];
                        }
                        if (!chainMNlist.Contains(chainMN))
                        {
                            chainMNlist.Add(chainMN);
                        }
                        if (chainMNBioInterfaceNumDict.ContainsKey(chainMN))
                        {
                            chainMNBioInterfaceNumDict[chainMN]++;
                        }
                        else
                        {
                            chainMNBioInterfaceNumDict.Add(chainMN, 1);
                        }
                        if (!entryList.Contains(pdbId))
                        {
                            if (chainMNBioInterfaceEntryNumDict.ContainsKey(chainMN))
                            {
                                chainMNBioInterfaceEntryNumDict[chainMN]++;
                            }
                            else
                            {
                                chainMNBioInterfaceEntryNumDict.Add(chainMN, 1);
                            }
                        }
                    }
                    if (fields[domainMindex] != "")
                    {
                        if (fields[chainNindex] == "0")
                        {
                            domainMN = fields[domainMindex] + "\t" + fields[domainMindex];
                        }
                        else
                        {
                            domainMN = fields[domainMindex] + "\t" + fields[domainNindex];
                        }
                        if (!domainMNlist.Contains(domainMN))
                        {
                            domainMNlist.Add(domainMN);
                        }
                        if (domainMNBioInterfaceNumDict.ContainsKey(domainMN))
                        {
                            domainMNBioInterfaceNumDict[domainMN]++;
                        }
                        else
                        {
                            domainMNBioInterfaceNumDict.Add(domainMN, 1);
                        }
                        if (!entryList.Contains(pdbId))
                        {
                            if (domainMNBioInterfaceEntryNumDict.ContainsKey(domainMN))
                            {
                                domainMNBioInterfaceEntryNumDict[domainMN]++;
                            }
                            else
                            {
                                domainMNBioInterfaceEntryNumDict.Add(domainMN, 1);
                            }
                        }
                    }
                }
                #endregion

                #region for xtal interface
                if (isBio == "0")
                {
                    if (fields[chainMindex] != "")
                    {
                        if (fields[chainNindex] == "0")
                        {
                            chainMN = fields[chainMindex] + "\t" + fields[chainMindex];
                        }
                        else
                        {
                            chainMN = fields[chainMindex] + "\t" + fields[chainNindex];
                        }
                        if (! chainMNlist.Contains (chainMN))
                        {
                            chainMNlist.Add(chainMN);
                        }
                        if (chainMNXtalInterfaceNumDict.ContainsKey(chainMN))
                        {
                            chainMNXtalInterfaceNumDict[chainMN]++;
                        }
                        else
                        {
                            chainMNXtalInterfaceNumDict.Add(chainMN, 1);
                        }
                        if (!entryList.Contains(pdbId))
                        {
                            if (chainMNXtalInterfaceEntryNumDict.ContainsKey(chainMN))
                            {
                                chainMNXtalInterfaceEntryNumDict[chainMN]++;
                            }
                            else
                            {
                                chainMNXtalInterfaceEntryNumDict.Add(chainMN, 1);
                            }
                        }
                    }
                    if (fields[domainMindex] != "")
                    {
                        if (fields[chainNindex] == "0")
                        {
                            domainMN = fields[domainMindex] + "\t" + fields[domainMindex];
                        }
                        else
                        {
                            domainMN = fields[domainMindex] + "\t" + fields[domainNindex];
                        }
                        if (!domainMNlist.Contains(domainMN))
                        {
                            domainMNlist.Add(domainMN);
                        }
                        if (domainMNXtalInterfaceNumDict.ContainsKey(domainMN))
                        {
                            domainMNXtalInterfaceNumDict[domainMN]++;
                        }
                        else
                        {
                            domainMNXtalInterfaceNumDict.Add(domainMN, 1);
                        }
                        if (!entryList.Contains(pdbId))
                        {
                            if (domainMNXtalInterfaceEntryNumDict.ContainsKey(domainMN))
                            {
                                domainMNXtalInterfaceEntryNumDict[domainMN]++;
                            }
                            else
                            {
                                domainMNXtalInterfaceEntryNumDict.Add(domainMN, 1);
                            }
                        }
                    }
                }
                #endregion

                if (! entryList.Contains (pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            dataReader.Close();
            string eppicClusterSumInfoFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_chain.txt");
            StreamWriter sumInfoWriter = new StreamWriter(eppicClusterSumInfoFile);
            sumInfoWriter.Write("M\tN\tNumInterfaces\tIsBio\n");
            foreach (string lsChainMN in chainMNlist)
            {
                if (chainMNBioInterfaceNumDict.ContainsKey(lsChainMN))
                {
                    sumInfoWriter.Write(lsChainMN + "\t" + chainMNBioInterfaceNumDict[lsChainMN].ToString() + "\t1\n");
                }
                if (chainMNXtalInterfaceNumDict.ContainsKey (lsChainMN))
                {
                    sumInfoWriter.Write(lsChainMN + "\t" + chainMNXtalInterfaceNumDict[lsChainMN].ToString() + "\t0\n");
                }
            }
            sumInfoWriter.Close();

            eppicClusterSumInfoFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_chain_entry.txt");
            sumInfoWriter = new StreamWriter(eppicClusterSumInfoFile);
            sumInfoWriter.Write("M\tN\tNumInterfaces\tIsBio\n");
            foreach (string lsChainMN in chainMNlist)
            {
                if (chainMNBioInterfaceEntryNumDict.ContainsKey(lsChainMN))
                {
                    sumInfoWriter.Write(lsChainMN + "\t" + chainMNBioInterfaceEntryNumDict[lsChainMN].ToString() + "\t1\n");
                }
                if (chainMNXtalInterfaceEntryNumDict.ContainsKey(lsChainMN))
                {
                    sumInfoWriter.Write(lsChainMN + "\t" + chainMNXtalInterfaceEntryNumDict[lsChainMN].ToString() + "\t0\n");
                }
            }
            sumInfoWriter.Close();

            eppicClusterSumInfoFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_domain.txt");
            sumInfoWriter = new StreamWriter(eppicClusterSumInfoFile);
            sumInfoWriter.Write("M\tN\tNumInterfaces\tIsBio\n");
            foreach (string lsDomainMN in domainMNlist)
            {
                if (domainMNBioInterfaceNumDict.ContainsKey(lsDomainMN))
                {
                    sumInfoWriter.Write(lsDomainMN + "\t" + domainMNBioInterfaceNumDict[lsDomainMN].ToString() + "\t1\n");
                }
                if (domainMNXtalInterfaceNumDict.ContainsKey(lsDomainMN))
                {
                    sumInfoWriter.Write(lsDomainMN + "\t" + domainMNXtalInterfaceNumDict[lsDomainMN].ToString() + "\t0\n");
                }
            }
            sumInfoWriter.Close();

            eppicClusterSumInfoFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_domain_entry.txt");
            sumInfoWriter = new StreamWriter(eppicClusterSumInfoFile);
            sumInfoWriter.Write("M\tN\tNumInterfaces\tIsBio\n");
            foreach (string lsDomainMN in domainMNlist)
            {
                if (domainMNBioInterfaceEntryNumDict.ContainsKey(lsDomainMN))
                {
                    sumInfoWriter.Write(lsDomainMN + "\t" + domainMNBioInterfaceEntryNumDict[lsDomainMN].ToString() + "\t1\n");
                }
                if (domainMNXtalInterfaceEntryNumDict.ContainsKey(lsDomainMN))
                {
                    sumInfoWriter.Write(lsDomainMN + "\t" + domainMNXtalInterfaceEntryNumDict[lsDomainMN].ToString() + "\t0\n");
                }
            }
            sumInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void FormatClusterSumDataToRFiles ()
        {
            int[] NcombCutoffs = {15, 20, 30, 99999};
            string RfilePrefix = "EppicBenchClusterSum";
            string chainClusterSumFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_chain.txt");
            string[] chainClusterRSumFiles = new string[2];
            chainClusterRSumFiles[0] = Path.Combine(baBenchDir, RfilePrefix + "_chain_bio_R.txt");
            chainClusterRSumFiles[1] = Path.Combine(baBenchDir, RfilePrefix + "_chain_xtal_R.txt");
            FormatClusterSumDataToR(chainClusterSumFile, chainClusterRSumFiles, NcombCutoffs);

            string chainClusterSumEntryFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_chain_entry.txt");
            string[] chainClusterRSumEntryFiles = new string[2];
            chainClusterRSumEntryFiles[0] = Path.Combine(baBenchDir, RfilePrefix + "_chain_entry_bio_R.txt");
            chainClusterRSumEntryFiles[1] = Path.Combine(baBenchDir, RfilePrefix + "_chain_entry_xtal_R.txt");
            FormatClusterSumDataToR(chainClusterSumEntryFile, chainClusterRSumEntryFiles, NcombCutoffs);

            string domainClusterSumInfoFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_domain.txt");
            string[] domainClusterRSumFiles = new string[2];
            domainClusterRSumFiles[0] = Path.Combine(baBenchDir, RfilePrefix + "_domain_bio_R.txt");
            domainClusterRSumFiles[1] = Path.Combine(baBenchDir, RfilePrefix + "_domain_xtal_R.txt");
            FormatClusterSumDataToR(domainClusterSumInfoFile, domainClusterRSumFiles, NcombCutoffs);

            string domainClusterSumInfoEntryFile = Path.Combine(baBenchDir, "EppicBenchClusterSumInfo_domain_entry.txt");
            string[] domainClusterRSumEntryFiles = new string[2];
            domainClusterRSumEntryFiles[0] = Path.Combine(baBenchDir, RfilePrefix + "_domain_entry_bio_R.txt");
            domainClusterRSumEntryFiles[1] = Path.Combine(baBenchDir, RfilePrefix + "_domain_entry_xtal_R.txt");
            FormatClusterSumDataToR(domainClusterSumInfoEntryFile, domainClusterRSumEntryFiles, NcombCutoffs);    
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterSumFile"></param>
        /// <param name="Rfiles"></param>
        public void FormatClusterSumDataToR (string clusterSumFile, string[] Rfiles, int[] combCutoffs)
        {
            StreamReader dataReader = new StreamReader(clusterSumFile);
            string line = dataReader.ReadLine();
            Dictionary<int, Dictionary<int, int>> mNBioInterfaceNumDict = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, Dictionary<int, int>> mNXtalInterfaceNumDict = new Dictionary<int, Dictionary<int, int>>();
            int m = 0;
            int n = 0;
            int numInterfaces = 0;
            List<int> bioMlist = new List<int> ();
            List<int> bioNlist = new List<int> ();
            List<int> xtalMlist = new List<int> ();
            List<int> xtalNlist = new List<int> ();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                m = Convert.ToInt32(fields[0]);
                n = Convert.ToInt32(fields[1]);
                numInterfaces = Convert.ToInt32(fields[2]);
                if (fields[fields.Length - 1] == "0")
                {
                   if (mNXtalInterfaceNumDict.ContainsKey (m))
                   {
                       mNXtalInterfaceNumDict[m].Add (n, numInterfaces);
                   }
                   else
                   {
                       Dictionary<int, int> nNumInterfaceDict = new Dictionary<int, int>();
                       nNumInterfaceDict.Add(n, numInterfaces);
                       mNXtalInterfaceNumDict.Add(m, nNumInterfaceDict);
                   }
                    if (! xtalMlist.Contains (m))
                    {
                        xtalMlist.Add (m);
                    }
                    if (! xtalNlist.Contains (n))
                    {
                        xtalNlist.Add (n);
                    }
                }
                else
                {
                    if (mNBioInterfaceNumDict.ContainsKey(m))
                    {
                        mNBioInterfaceNumDict[m].Add(n, numInterfaces);
                    }
                    else
                    {
                        Dictionary<int, int> nNumInterfaceDict = new Dictionary<int, int>();
                        nNumInterfaceDict.Add(n, numInterfaces);
                        mNBioInterfaceNumDict.Add(m, nNumInterfaceDict);
                    }
                    if (! bioMlist.Contains (m))
                    {
                        bioMlist.Add (m);
                    }
                    if (! bioNlist.Contains (n))
                    {
                        bioNlist.Add (n);
                    }
                }
            }
            dataReader.Close();

            bioMlist.Sort ();
            bioNlist.Sort ();
            xtalMlist.Sort ();
            xtalNlist.Sort ();
            string[] outputRfiles = new string[2];
            foreach (int combCutoff in combCutoffs)
            {
                if (combCutoff > 10000)
                {
                    outputRfiles[0] = Rfiles[0];
                    outputRfiles[1] = Rfiles[1];
                }
                else
                {
                    outputRfiles[0] = Rfiles[0].Replace(".txt", "_" + combCutoff.ToString() + ".txt");
                    outputRfiles[1] = Rfiles[1].Replace(".txt", "_" + combCutoff.ToString() + ".txt");
                }
                WriteMNNumInterfacesToRFile(mNBioInterfaceNumDict, bioMlist, bioNlist, combCutoff, outputRfiles[0]);
                WriteMNNumInterfacesToRFile(mNXtalInterfaceNumDict, xtalMlist, xtalNlist, combCutoff, outputRfiles[1]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mNInterfaceNumDict"></param>
        /// <param name="mList"></param>
        /// <param name="nList"></param>
        /// <param name="combCutoff"></param>
        /// <param name="Rfile"></param>
        private void WriteMNNumInterfacesToRFile (Dictionary<int, Dictionary<int, int>> mNInterfaceNumDict, List<int> mList, List<int> nList, 
            int combCutoff, string Rfile)
        {
            string dataLine = "";
            StreamWriter dataWriter = new StreamWriter (Rfile);
            string firstLine = "";
            foreach (int lsN in nList)
            {
                if (lsN > combCutoff)
                {
                    break;
                }
                firstLine += ("\t" + lsN);
            }
            dataWriter.Write(firstLine + "\n");
            Dictionary<int, Dictionary<int, int>> mNcutoffInterfaceNumDict = ReformatDictionaryWithCutoff(mNInterfaceNumDict, combCutoff);
            foreach (int lsM in mList)
            {
                if (lsM > combCutoff )
                {
                    break;
                }
                dataLine = lsM.ToString();
                if (mNcutoffInterfaceNumDict.ContainsKey(lsM))
                {
                    foreach (int lsN in nList)
                    {
                        if (lsN > combCutoff)
                        {
                            break;
                        }
                        if (mNcutoffInterfaceNumDict[lsM].ContainsKey(lsN))
                        {
                            dataLine += ("\t" + mNcutoffInterfaceNumDict[lsM][lsN].ToString());
                        }
                        else
                        {
                            dataLine += "\t0";
                        }
                    }
                }              
                dataWriter.Write(dataLine + "\n");
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mNNumInterfaceDict"></param>
        /// <param name="cutoff"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<int, int>> ReformatDictionaryWithCutoff (Dictionary<int, Dictionary<int, int>> mNNumInterfaceDict, int cutoff)
        {
            Dictionary<int, Dictionary<int, int>> mNCutoffNumInterfaceDict = new Dictionary<int, Dictionary<int, int>>();
            int numInterfaceMCutoff = 0;
            int numInterfaceNCutoff = 0;
            bool isNComb = false;
            bool isMComb = false;
            foreach (int M in mNNumInterfaceDict.Keys)
            {
                isNComb = false;
                numInterfaceNCutoff = 0;
                if (M >= cutoff)
                {
                    foreach (int N in mNNumInterfaceDict[M].Keys)
                    {
                        numInterfaceMCutoff += mNNumInterfaceDict[M][N];
                    }
                    isMComb = true;
                }
                else
                {
                    foreach (int N in mNNumInterfaceDict[M].Keys)
                    {
                        if (N >= cutoff)
                        {
                            numInterfaceNCutoff += mNNumInterfaceDict[M][N];
                            isNComb = true;
                        }
                        else
                        {
                            if (mNCutoffNumInterfaceDict.ContainsKey (M))
                            {
                                mNCutoffNumInterfaceDict[M].Add(N, mNNumInterfaceDict[M][N]);
                            }
                            else
                            {
                                Dictionary<int, int> nNumInterfaceDict = new Dictionary<int, int>();
                                nNumInterfaceDict.Add(N, mNNumInterfaceDict[M][N]);
                                mNCutoffNumInterfaceDict.Add(M, nNumInterfaceDict);
                            }
                        }
                    }
                    if (isNComb)
                    {
                        if (mNCutoffNumInterfaceDict.ContainsKey(M))
                        {
                            mNCutoffNumInterfaceDict[M].Add(cutoff, numInterfaceNCutoff);
                        }
                        else
                        {
                            Dictionary<int, int> nNumInterfaceDict = new Dictionary<int, int>();
                            nNumInterfaceDict.Add(cutoff, numInterfaceNCutoff);
                            mNCutoffNumInterfaceDict.Add(M, nNumInterfaceDict);
                        }
                    }
                }
            }
            if (isMComb)
            {
                Dictionary<int, int> nNumInterfaceDict = new Dictionary<int, int>();
                nNumInterfaceDict.Add(cutoff, numInterfaceMCutoff);
                mNCutoffNumInterfaceDict.Add(cutoff, nNumInterfaceDict);
            }
            return mNCutoffNumInterfaceDict;
        }
        #endregion
        #endregion

        #region debug
        public void AddIsBioColumnToFile()
        {
            string biolInterfaceListFile = Path.Combine(baBenchDir, "BioInterfacesBenchmarkData.txt");
            string xtalInterfaceListFile = Path.Combine(baBenchDir, "XtalInterfacesBenchmarkData.txt");
            string[] bioInterfaces = ReadEppicInterfaceList(biolInterfaceListFile);
            string[] xtalInterfaces = ReadEppicInterfaceList(xtalInterfaceListFile);

            StreamReader clusterInfoReader = new StreamReader(Path.Combine(baBenchDir, "EppicBenchmarkInterfacesClusterInfo.txt"));
            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(baBenchDir, "EppicBenchmarkInterfacesClusterInfo_isbio.txt"));
            string line = clusterInfoReader.ReadLine();
            clusterInfoWriter.WriteLine(line + "\tIsBio");
            while ((line = clusterInfoReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (bioInterfaces.Contains(fields[0]))
                {
                    clusterInfoWriter.WriteLine(line + "\t1");
                }
                else
                {
                    clusterInfoWriter.WriteLine(line + "\t0");
                }
            }
            clusterInfoWriter.Close();
            clusterInfoReader.Close();
        }
        #endregion
    }
}
