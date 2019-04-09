using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Net;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    public class BioAssemblyDataInfo  : PaperDataInfo
    {
        private string biolAssemDataDir = "";
        private int cfgClusterCutoff = 5;
  //      private double surfaceAreaCutoff = 300;
        private int[] excludedChainGroups = { 744, 762 };

        public BioAssemblyDataInfo ()
        {
            biolAssemDataDir = Path.Combine(dataDir, "BiolAssem");
            if (! Directory.Exists (biolAssemDataDir))
            {
                Directory.CreateDirectory(biolAssemDataDir);
            }            
        }

        #region common entries and uniprots
        public void PrintCommonEntriesUniProts()
        {
            int chainGroupId = 737;   // 4HBT
            string[] clusterGroups = { "1-2", "1-4", "1-6" };
            string groupUnpSeqInfoFile = Path.Combine(biolAssemDataDir, chainGroupId + "_clustersCommonEntriesUnpInfo_TwoClusters.txt");
            StreamWriter dataWriter = new StreamWriter(groupUnpSeqInfoFile);
            Dictionary<string, List<string>> clustersCommonPdbPisaNumDict = GetBigChainClustersMultimersContents(chainGroupId, dataWriter);
            int[] clusterIds = null;
            string sequence = "";
            Dictionary<string, List<string>> clustersUnpListDict = new Dictionary<string, List<string>>();
            Dictionary<string, string> unpSeqDict = new Dictionary<string, string>();
            foreach (string clusterList in clustersCommonPdbPisaNumDict.Keys)
            {
                if (! clusterGroups.Contains (clusterList))
                {
                    continue;
                }
                string[] clusterIdFields = clusterList.Split('-');
                clusterIds = new int[clusterIdFields.Length];
                for (int i = 0; i < clusterIdFields.Length; i++)
                {
                    clusterIds[i] = Convert.ToInt32(clusterIdFields[i]);
                }
                Dictionary<string, List<string>> unpEntryListDict = GetEntriesUnpSeqInfo(chainGroupId, clusterIds, clustersCommonPdbPisaNumDict[clusterList].ToArray());
                foreach (string unpCode in unpEntryListDict.Keys)
                {
                    sequence = GetUnpSequence(unpCode, unpSeqDict);
                    dataWriter.WriteLine(clusterList + "\t" + unpCode + "\t" + sequence + "\t" + unpEntryListDict[unpCode].Count + "\t" + ParseHelper.FormatStringFieldsToString(unpEntryListDict[unpCode].ToArray()));
                }
                clustersUnpListDict.Add(clusterList, new List<string>(unpEntryListDict.Keys));
            }
            List<string> clustersList = new List<string>(clustersUnpListDict.Keys);
            clustersList.Sort();
            for (int i = 0; i < clustersList.Count; i++)
            {
                for (int j = i + 1; j < clustersList.Count; j++)
                {
                    List<string> commonUnpList = GetCommonItemList(clustersUnpListDict[clustersList[i]], clustersUnpListDict[clustersList[j]]);
                    dataWriter.Write(clustersList[i] + "\t" + clustersList[j] + "\t" + commonUnpList.Count + "\t" + ParseHelper.FormatStringFieldsToString (commonUnpList.ToArray ()) + "\n");
                }
            }

            foreach (string unpCode in unpSeqDict.Keys)
            {
                dataWriter.Write(">" + unpCode + "\n" + unpSeqDict[unpCode] + "\n");
            }

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        public void PrintAllUniprotsInAGroup (int chainGroupId)
        {            
            string groupSeqFile = Path.Combine(biolAssemDataDir, chainGroupId + "_seq.fasta");
            StreamWriter seqWriter = new StreamWriter(groupSeqFile);
            string queryString = string.Format("Select Distinct UnpCode From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0};", chainGroupId);
            DataTable groupUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            string unpCode = "";
            string sequence = "";
            foreach (DataRow unpRow in groupUnpTable.Rows)
            {
                unpCode = unpRow["UnpCode"].ToString();
                sequence = GetUnpSequence(unpCode);
                seqWriter.Write(">" + unpCode + "\n");
                seqWriter.Write(sequence + "\n");
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        public void PrintAllUniprotsInAGroup(int chainGroupId, int clusterId)
        {
            string groupSeqFile = Path.Combine(biolAssemDataDir, chainGroupId + "_" + clusterId + "_seq.fasta");
            StreamWriter seqWriter = new StreamWriter(groupSeqFile);
            string queryString = string.Format("Select Distinct UnpCode From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1};", chainGroupId, clusterId);
            DataTable groupUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            string unpCode = "";
            string sequence = "";
            foreach (DataRow unpRow in groupUnpTable.Rows)
            {
                unpCode = unpRow["UnpCode"].ToString();
                sequence = GetUnpSequence(unpCode);
                seqWriter.Write(">" + unpCode + "\n");
                seqWriter.Write(sequence + "\n");
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetEntriesUnpSeqInfo (int chainGroupId, int[] clusterIds, string[] entries)
        {
            string queryString = string.Format("Select Distinct PdbID, UnpCode From PfamSuperClusterEntryInterfaces" + 
                " Where SuperGroupSeqID = {0} AND ClusterID IN ({1}) AND PdbID IN ({2});", 
                chainGroupId, ParseHelper.FormatSqlListString (clusterIds), ParseHelper.FormatSqlListString (entries));
            DataTable entryUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpEntryListDict = new Dictionary<string, List<string>>();
            string unpCode = "";
            string pdbId = "";
            foreach (DataRow entryRow in entryUnpTable.Rows)
            {
                unpCode = entryRow["UnpCode"].ToString().TrimEnd();
                pdbId = entryRow["PdbID"].ToString();
                if (unpEntryListDict.ContainsKey (unpCode))
                {
                    unpEntryListDict[unpCode].Add(pdbId);
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add (pdbId);
                    unpEntryListDict.Add(unpCode, entryList);
                }
            }
            return unpEntryListDict; 
        }   
  
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string GetUnpSequence (string unpCode, Dictionary<string, string> unpSeqDict)
        {  
            if (unpSeqDict.ContainsKey (unpCode))
            {
                return unpSeqDict[unpCode];
            }

            string sequence = GetUnpSequence(unpCode);            
            unpSeqDict.Add(unpCode, sequence);
            return sequence;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string GetUnpSequence(string unpCode)
        {           
            string seqTableName = "";
            if (unpCode.ToUpper().IndexOf("_HUMAN") > -1)
            {
                seqTableName = "HumanSeqInfo";
            }
            else
            {
                seqTableName = "UnpSeqInfo";
            }
            string queryString = string.Format("Select Sequence From {0} Where UnpCode = '{1}' AND IsoForm = 0;", seqTableName, unpCode);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            if (seqTable.Rows.Count > 0)
            {
                sequence = seqTable.Rows[0]["Sequence"].ToString();
            }
            else
            {
                sequence = GetUnpSequenceFromWeb(unpCode);
            }            
            return sequence;
        }

        WebClient unpSeqDownloader = new WebClient();
        private string GetUnpSequenceFromWeb (string unpCode )
        {
            string url = "https://www.uniprot.org/uniprot/" + unpCode + ".fasta";
            string seqFile = Path.Combine(biolAssemDataDir, "UnpSeq\\" + unpCode + ".fasta");
            unpSeqDownloader.DownloadFile(url, seqFile);
            string sequence = "";
            if (File.Exists(seqFile))
            {
                StreamReader dataReader = new StreamReader(seqFile);
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    if (line.IndexOf (">") > -1)
                    {
                        continue;
                    }
                    sequence += line;
                }
                dataReader.Close();
            }
            return sequence;
        }
        #endregion

        #region biol chain clusters
        public void CountNumClustersWithCommonEntriesInPdbPisa ()
        {
            string chainGroupsBigClustersFile = Path.Combine(biolAssemDataDir, "ChainGroupsGETwoClustersInfo_cf" + cfgClusterCutoff.ToString() + "_update.txt");
            string groupClusterCom = "";
            Dictionary<string, List<string>> groupComClusterListPdbOrPisaDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> groupComClusterListPdbAndPisaDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader(chainGroupsBigClustersFile);
            string line = "";
            int inPdb = 0;
            int inPisa = 0;
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = ParseHelper.SplitPlus (line, '\t');
                if (fields[6] == "Entry")
                {
                    groupClusterCom = fields[0] + " " + fields[1];
                    inPdb = Convert.ToInt32(fields[4]);
                    inPisa = Convert.ToInt32(fields[5]);
                    if (inPdb > 0 || inPisa > 0)
                    {
                        if (groupComClusterListPdbOrPisaDict.ContainsKey(groupClusterCom))
                        {
                            if (!groupComClusterListPdbOrPisaDict[groupClusterCom].Contains(fields[2]))
                            {
                                groupComClusterListPdbOrPisaDict[groupClusterCom].Add(fields[2]);
                            }
                        }
                        else
                        {
                            List<string> comClusterList = new List<string>();
                            comClusterList.Add(fields[2]);
                            groupComClusterListPdbOrPisaDict.Add(groupClusterCom, comClusterList);
                        }
                    }

                    if (inPdb > 0 && inPisa > 0)
                    {
                        if (groupComClusterListPdbAndPisaDict.ContainsKey(groupClusterCom))
                        {
                            if (! groupComClusterListPdbAndPisaDict[groupClusterCom].Contains(fields[2]))
                            {
                                groupComClusterListPdbAndPisaDict[groupClusterCom].Add(fields[2]);
                            }
                        }
                        else
                        {
                            List<string> comClusterList = new List<string>();
                            comClusterList.Add(fields[2]);
                            groupComClusterListPdbAndPisaDict.Add(groupClusterCom, comClusterList);
                        }
                    }
                }
            }
            dataReader.Close();

            string chainComClustersSumInfoFile = Path.Combine(biolAssemDataDir, "ChainGroupComClustersSumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(chainComClustersSumInfoFile);
            dataWriter.WriteLine("Common Clusters InPDB OR InPISA");
            int numAllComClusters = 0;
            foreach (string lsGroup in groupComClusterListPdbOrPisaDict .Keys)
            {
                dataWriter.WriteLine(lsGroup + "\t" + FormatArrayString(groupComClusterListPdbOrPisaDict[lsGroup].ToArray()) 
                    + "\t" + groupComClusterListPdbOrPisaDict[lsGroup].Count);
                numAllComClusters += groupComClusterListPdbOrPisaDict[lsGroup].Count;
            }
            dataWriter.WriteLine("Total number of common cluster combinations: " + numAllComClusters);
            dataWriter.WriteLine();
            numAllComClusters = 0;
            dataWriter.WriteLine("Common Clusters InPDB AND InPISA");
            foreach (string lsGroup in groupComClusterListPdbAndPisaDict.Keys)
            {
                dataWriter.WriteLine(lsGroup + "\t" + FormatArrayString(groupComClusterListPdbAndPisaDict[lsGroup].ToArray())
                    + "\t" + groupComClusterListPdbAndPisaDict[lsGroup].Count);
                numAllComClusters += groupComClusterListPdbAndPisaDict[lsGroup].Count;
            }
            dataWriter.WriteLine("Total number of common cluster combinations: " + numAllComClusters);
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintSameGroupBiolClusters ()
        {           
            string chainGroupsBigClustersFile = Path.Combine(biolAssemDataDir, "ChainGroupsGETwoClustersInfo_cf" + cfgClusterCutoff.ToString () +  "_update.txt");
            StreamWriter dataWriter = new StreamWriter(chainGroupsBigClustersFile);
            string logFile = Path.Combine(biolAssemDataDir, "GroupBigClustersLog_cf" + cfgClusterCutoff.ToString() + ".txt");
            StreamWriter logWriter = new StreamWriter(logFile);
            logWriter.WriteLine(DateTime.Today.ToShortDateString());
            string queryString = string.Format("Select SuperGroupSeqID, Count(ClusterID) As clusterCount From PfamSuperClusterSumInfo " + 
       //         " Where NumOfCfgCluster >= {0} AND MinSeqIdentity <= 90 AND SurfaceArea >= {1} Group By SuperGroupSeqID;", cfgClusterCutoff, surfaceAreaCutoff);
                    " Where NumOfCfgCluster >= {0} AND MinSeqIdentity <= 90 Group By SuperGroupSeqID;", cfgClusterCutoff);
            DataTable chainGroupBigClusterNumTable = ProtCidSettings.protcidQuery.Query(queryString);
            int numClusters = 0;
            int chainGroupId = 0;
            foreach (DataRow clusterNumRow in chainGroupBigClusterNumTable.Rows)
            {
                numClusters = Convert.ToInt32(clusterNumRow["ClusterCount"].ToString ());
                if (numClusters >= 2)
                {
                    chainGroupId = Convert.ToInt32 (clusterNumRow["SuperGroupSeqID"].ToString ());
                    if (excludedChainGroups.Contains (chainGroupId))
                    {
                        continue;
                    }
                    logWriter.WriteLine(chainGroupId);
                    try
                    {
                        GetBigChainClustersMultimersContents(chainGroupId, dataWriter);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine(chainGroupId + " retrieving big clusters entry info error: " + ex.Message);
                        logWriter.Flush();
                    }
                }
            }
            dataWriter.Close();
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        public Dictionary<string, List<string>> GetBigChainClustersMultimersContents (int chainGroupId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select PfamSuperClusterSumInfo.ClusterID, PdbID, PdbBU, PisaBU From PfamSuperClusterSumInfo, PfamSuperClusterEntryInterfaces " +
                " Where PfamSuperClusterSumInfo.SuperGroupSeqID = {0} AND NumOfCfgCluster > {1} AND MinSeqIdentity <= 90 AND " +
         //       " PfamSuperClusterSumInfo.SurfaceArea > {2} AND " + 
                " PfamSuperClusterSumInfo.SuperGroupSeqID = PfamSuperClusterEntryInterfaces.SuperGroupSeqID AND " +
                " PfamSuperClusterSumInfo.ClusterID = PfamSuperClusterEntryInterfaces.ClusterID;", chainGroupId, cfgClusterCutoff/*, surfaceAreaCutoff*/);
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<int, Dictionary<string, List<string>>> clusterBaPdbListDict = new Dictionary<int, Dictionary<string, List<string>>>();
            Dictionary<int, Dictionary<string, List<string>>> clusterBaPisaListDict = new Dictionary<int, Dictionary<string, List<string>>>();
            Dictionary<string, Dictionary<string, List<string>>> clusterBaCommonPdbListDict = new Dictionary<string, Dictionary<string, List<string>>>();
            Dictionary<string, Dictionary<string, List<string>>> clusterBaCommonPisaListDict = new Dictionary<string, Dictionary<string, List<string>>>();
            Dictionary<string, List<string>> clustersCommonPdbPisaNumDict = new Dictionary<string, List<string>>();

            int clusterId = 0;
            string pdbId = "";
            string pdbBa = "";
            string pisaBa = "";
            List<int> clusterIdList = new List<int>();
            Dictionary<int, List<string>> clusterEntryListDict = new Dictionary<int, List<string>>();
            foreach (DataRow clusterEntryRow in clusterEntryTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterEntryRow["ClusterID"].ToString());
                if (! clusterIdList.Contains (clusterId))
                {
                    clusterIdList.Add(clusterId);
                }
                pdbId = clusterEntryRow["PdbID"].ToString();
                pdbBa = clusterEntryRow["PdbBU"].ToString().TrimEnd();
                pisaBa = clusterEntryRow["PisaBU"].ToString().TrimEnd();
                if (clusterEntryListDict.ContainsKey (clusterId))
                {
                    if (clusterEntryListDict[clusterId].Contains (pdbId))
                    {
                        continue;
                    }
                    else
                    {
                        clusterEntryListDict[clusterId].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    clusterEntryListDict.Add(clusterId, entryList);
                }
                AddEntryBaToDict(clusterId, pdbId, pdbBa, ref clusterBaPdbListDict);
                AddEntryBaToDict(clusterId, pdbId, pisaBa, ref clusterBaPisaListDict);               
            }

            clusterBaCommonPdbListDict = GetBaCommonEntryListDict(clusterIdList, clusterBaPdbListDict);
            if (clusterBaCommonPdbListDict.Count > 1)
            {
                Dictionary<string, Dictionary<string, List<string>>> combClusterBaCommonPdbListDict = GetBaCommonEntryListDict(clusterBaCommonPdbListDict);
                foreach (string combCluster in combClusterBaCommonPdbListDict.Keys)
                {
                    clusterBaCommonPdbListDict.Add(combCluster, combClusterBaCommonPdbListDict[combCluster]);
                }
            }
            clusterBaCommonPisaListDict = GetBaCommonEntryListDict(clusterIdList, clusterBaPisaListDict);
            if (clusterBaCommonPisaListDict.Count > 1)
            {
                Dictionary<string, Dictionary<string, List<string>>> combClusterBaCommonPisaListDict = GetBaCommonEntryListDict(clusterBaCommonPisaListDict);
                foreach (string combCluster in combClusterBaCommonPisaListDict.Keys)
                {
                    clusterBaCommonPisaListDict.Add(combCluster, combClusterBaCommonPisaListDict[combCluster]);
                }
            }

            clustersCommonPdbPisaNumDict = GetCommonEntryListDict(clusterIdList, clusterEntryListDict);
            if (clustersCommonPdbPisaNumDict.Count > 1)
            {
                Dictionary<string, List<string>> moreClusterCommonEntryListDict = GetCommonEntryListDict(clustersCommonPdbPisaNumDict);
                foreach (string combCluster in moreClusterCommonEntryListDict.Keys)
                {
                    clustersCommonPdbPisaNumDict.Add(combCluster, moreClusterCommonEntryListDict[combCluster]);
                }
            }

            string chainGroupName = GetChainGroupName(chainGroupId);
            WriteClusterCommonEntryDictToFile(chainGroupId, chainGroupName, clusterEntryListDict, "Entry", dataWriter);
            WriteClusterCommonEntryDictToFile(chainGroupId, chainGroupName, clustersCommonPdbPisaNumDict, "Entry", dataWriter);
            WriteClusterCommonEntryDictToFile(chainGroupId, chainGroupName, clusterBaCommonPdbListDict, "PdbBA", dataWriter);
            WriteClusterCommonEntryDictToFile(chainGroupId, chainGroupName, clusterBaCommonPisaListDict, "PisaBA", dataWriter);

            return clustersCommonPdbPisaNumDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <returns></returns>
        private string GetChainGroupName (int chainGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups WHere SuperGroupSeqID = {0};", chainGroupId);
            DataTable relPfamArchTable = ProtCidSettings.protcidQuery.Query(queryString);
            string relPfamArch = "";
            if (relPfamArchTable.Rows.Count > 0)
            {
                relPfamArch = relPfamArchTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            }
            return relPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="chainGroupName"></param>
        /// <param name="clusterListBaCommonEntryListDict"></param>
        /// <param name="dataWriter"></param>
        private void WriteClusterCommonEntryDictToFile (int chainGroupId, string chainGroupName, Dictionary<string, Dictionary<string, List<string>>> clusterListBaCommonEntryListDict, string dataType, StreamWriter dataWriter)
        {
            List<string> clusterPairList = new List<string>(clusterListBaCommonEntryListDict.Keys);
            clusterPairList.Sort();
            List<int> clusterIdList = new List<int>();
            List<string> entryInBaList = null;
            foreach (string clusterPair in clusterPairList)
            {
                clusterIdList.Clear();
                string[] clusterFields = clusterPair.Split('-');
                foreach (string clusterField in clusterFields)
                {
                    clusterIdList.Add(Convert.ToInt32(clusterField));
                }

                foreach (string ba in clusterListBaCommonEntryListDict[clusterPair].Keys)
                {
                    if (dataType.ToLower().IndexOf("pdb") > -1)
                    {
                        entryInBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterListBaCommonEntryListDict[clusterPair][ba], "chain", "pdb");
                    }
                    else if (dataType.ToLower().IndexOf("pisa") > -1)
                    {
                        entryInBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterListBaCommonEntryListDict[clusterPair][ba], "chain", "pisa");
                    }
                    clusterListBaCommonEntryListDict[clusterPair][ba].Sort();
                    dataWriter.WriteLine(chainGroupId + "\t" + chainGroupName + "\t" + clusterPair + "\t" + ba + "\t" + clusterListBaCommonEntryListDict[clusterPair][ba].Count + "\t" +
                        entryInBaList.Count + "\t" + dataType + "\t" + ParseHelper.FormatStringFieldsToString(entryInBaList.ToArray()) + "\t" +
                        ParseHelper.FormatStringFieldsToString(clusterListBaCommonEntryListDict[clusterPair][ba].ToArray()));
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="chainGroupName"></param>
        /// <param name="clusterEntryListDict"></param>
        /// <param name="dataWriter"></param>
        private void WriteClusterCommonEntryDictToFile(int chainGroupId, string chainGroupName, Dictionary<string, List<string>> clusterEntryListDict, string dataType , StreamWriter dataWriter)
        {
            List<string> clusterPairList = new List<string>(clusterEntryListDict.Keys);
            clusterPairList.Sort();
            List<int> clusterIdList = new List<int>();
            List<string> entryInPdbBaList = null;
            List<string> entryInPisaBaList = null;
            foreach (string clusterPair in clusterPairList)
            {
                clusterIdList.Clear();
                string[] clusterFields = clusterPair.Split('-');
                foreach (string clusterField in clusterFields)
                {
                    clusterIdList.Add(Convert.ToInt32(clusterField));
                }
                clusterEntryListDict[clusterPair].Sort();
                if (dataType.ToLower ().IndexOf ("pdb") > -1)
                {
                    entryInPdbBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterEntryListDict[clusterPair], "chain", "pdb");
                    dataWriter.WriteLine(chainGroupId + "\t" + chainGroupName + "\t" + clusterPair + "\t" + clusterEntryListDict[clusterPair].Count + "\t" + entryInPdbBaList.Count + "\t" +
                            dataType + "\t" + ParseHelper.FormatStringFieldsToString(entryInPdbBaList.ToArray()) + "\t" +
                            ParseHelper.FormatStringFieldsToString(clusterEntryListDict[clusterPair].ToArray()));
                }
                else if (dataType.ToLower().IndexOf("pisa") > -1)
                {
                    entryInPisaBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterEntryListDict[clusterPair], "chain", "pisa");
                    dataWriter.WriteLine(chainGroupId + "\t" + chainGroupName + "\t" + clusterPair + "\t" + clusterEntryListDict[clusterPair].Count + "\t" + entryInPisaBaList.Count + "\t" +
                            dataType + "\t" + ParseHelper.FormatStringFieldsToString(entryInPisaBaList.ToArray()) + "\t" +
                            ParseHelper.FormatStringFieldsToString(clusterEntryListDict[clusterPair].ToArray()));
                }
                else
                {
                    entryInPdbBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterEntryListDict[clusterPair], "chain", "pdb");
                    entryInPisaBaList = GetEntryListInPdbPisaBAs(chainGroupId, clusterIdList.ToArray(), clusterEntryListDict[clusterPair], "chain", "pisa");

                    dataWriter.WriteLine(chainGroupId + "\t" + chainGroupName + "\t" + clusterPair + "\t" + clusterEntryListDict[clusterPair].Count + "\t" + 
                            entryInPdbBaList.Count + "\t" + entryInPisaBaList.Count + "\t" +
                            dataType + "\t" + ParseHelper.FormatStringFieldsToString(entryInPdbBaList.ToArray()) + "\t" + ParseHelper.FormatStringFieldsToString(entryInPisaBaList.ToArray()) + "\t" +
                            ParseHelper.FormatStringFieldsToString(clusterEntryListDict[clusterPair].ToArray()));
                }
                
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="chainGroupName"></param>
        /// <param name="clusterEntryListDict"></param>
        /// <param name="dataWriter"></param>
        private void WriteClusterCommonEntryDictToFile(int chainGroupId, string chainGroupName, Dictionary<int, List<string>> clusterEntryListDict, string dataType, StreamWriter dataWriter)
        {
            List<int> clusterIdList = new List<int>(clusterEntryListDict.Keys);
            clusterIdList.Sort();
            string clusterInfoString = "";
            foreach (int clusterId in clusterIdList)
            {
                clusterInfoString = GetChainClusterSumInfo(chainGroupId, clusterId);
                clusterEntryListDict[clusterId].Sort();
                dataWriter.WriteLine(chainGroupId + "\t" + chainGroupName + "\t" + clusterId + "\t" + clusterEntryListDict[clusterId].Count + "\t" + dataType + "\t" + 
                    clusterInfoString + "\t" + ParseHelper.FormatStringFieldsToString(clusterEntryListDict[clusterId].ToArray()));
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetChainClusterSumInfo (int chainGroupId, int clusterId)
        {
            string queryString = string.Format("Select NumOfCfgCluster, NumOfEntryCluster, NumOfCfgFamily, NumOfEntryFamily, InPdb, InPisa, SurfaceArea " + 
                " From PfamSuperClusterSumInfo WHere SuperGroupSeqID = {0} AND ClusterID = {1};", chainGroupId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            string clusterInfoString = clusterSumInfoTable.Rows[0]["NumOfCfgCluster"] + "/" + clusterSumInfoTable.Rows[0]["NumOfCfgFamily"] + "\t" +
                clusterSumInfoTable.Rows[0]["NumOfEntryCluster"] + "/" + clusterSumInfoTable.Rows[0]["NumOfEntryFamily"] + "\t" +
                clusterSumInfoTable.Rows[0]["InPDB"] + "\t" + clusterSumInfoTable.Rows[0]["InPisa"] + "\t" + clusterSumInfoTable.Rows[0]["SurfaceArea"];
            return clusterInfoString;
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterIdList"></param>
        /// <param name="clusterBaEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, List<string>>> GetBaCommonEntryListDict (List<int> clusterIdList, Dictionary<int, Dictionary<string, List<string>>> clusterBaEntryListDict)
        {
            string clusterListString = "";
            Dictionary<string, Dictionary<string, List<string>>> clusterBaCommonEntryListDict = new Dictionary<string, Dictionary<string, List<string>>>();
            for (int i = 0; i < clusterIdList.Count; i++)
            {
                Dictionary<string, List<string>> baPdbListDict = clusterBaEntryListDict[clusterIdList[i]];
                foreach (string ba in baPdbListDict.Keys)
                {
                    for (int j = i + 1; j < clusterIdList.Count; j++)
                    {
                        if (clusterBaEntryListDict[clusterIdList[j]].ContainsKey(ba))
                        {
                            clusterListString = clusterIdList[i] + "-" + clusterIdList[j];
                            List<string> commonEntryList = GetCommonItemList(baPdbListDict[ba], clusterBaEntryListDict[clusterIdList[j]][ba]);
                            if (commonEntryList.Count > 0)
                            {
                                if (clusterBaCommonEntryListDict.ContainsKey (clusterListString))
                                {
                                    clusterBaCommonEntryListDict[clusterListString].Add(ba, commonEntryList);
                                }
                                else
                                {
                                    Dictionary<string, List<string>> baCommonEntryListDict = new Dictionary<string, List<string>>();
                                    baCommonEntryListDict.Add(ba, commonEntryList);
                                    clusterBaCommonEntryListDict.Add(clusterListString, baCommonEntryListDict);
                                }
                            }
                        }
                    }
                }
            }
            return clusterBaCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <param name="entryList"></param>
        /// <param name="baType"></param>
        /// <returns></returns>
        private List<string> GetEntryListInPdbPisaBAs(int groupId, int[] clusterIds, List<string> entryList, string interfaceType, string baType)
        {
            List<string> inBaEntryList = new List<string>(entryList);
            string queryString = "";

            foreach (int clusterId in clusterIds)
            {
                if (interfaceType.IndexOf("chain") > -1)
                {
                    queryString = string.Format("Select Distinct PdbID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1} AND In{2} = '1';", groupId, clusterId, baType);
                }
                else
                {
                    queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1} AND In{2} = '1';", groupId, clusterId, baType);
                }
                DataTable inBaClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
                List<string> tempEntryList = new List<string>(inBaEntryList);
                foreach (string pdbId in tempEntryList)
                {
                    DataRow[] entryRows = inBaClusterTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    if (entryRows.Length == 0)
                    {
                        inBaEntryList.Remove(pdbId);
                    }
                }
            }
            inBaEntryList.Sort();
            return inBaEntryList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterIdList"></param>
        /// <param name="clusterBaEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetCommonEntryListDict(List<int> clusterIdList, Dictionary<int, List<string>> clusterEntryListDict)
        {
            string clusterListString = "";
            Dictionary<string, List<string>> clusterCommonEntryListDict = new Dictionary<string, List<string>>();
            for (int i = 0; i < clusterIdList.Count; i++)
            {
                for (int j = i + 1; j < clusterIdList.Count; j++)
                {
                    clusterListString = clusterIdList[i] + "-" + clusterIdList[j];
                    List<string> commonEntryList = GetCommonItemList(clusterEntryListDict[clusterIdList[i]], clusterEntryListDict[clusterIdList[j]]);
                    if (commonEntryList.Count > 0)
                    {
                        clusterCommonEntryListDict.Add(clusterListString, commonEntryList);
                    }
                }
            }
            return clusterCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterPairBaCommonEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, List<string>>> GetBaCommonEntryListDict(Dictionary<string, Dictionary<string, List<string>>> clusterPairBaCommonEntryListDict)
        {
            string clusterListString = "";
            Dictionary<string, Dictionary<string, List<string>>> moreClusterBaCommonEntryListDict = new Dictionary<string, Dictionary<string, List<string>>>();
            List<string> clusterPairList = new List<string>(clusterPairBaCommonEntryListDict.Keys);
            clusterPairList.Sort();
            
            for (int i = 0; i < clusterPairList.Count; i++)
            {
                Dictionary<string, List<string>> baPdbListDict = clusterPairBaCommonEntryListDict[clusterPairList[i]];
                
                foreach (string ba in baPdbListDict.Keys)
                {
                    for (int j = i + 1; j < clusterPairList.Count; j++)
                    {
                        if (clusterPairBaCommonEntryListDict[clusterPairList[j]].ContainsKey(ba))
                        {
                            clusterListString = GetClusterCombListString(clusterPairList[i], clusterPairList[j]);
                            List<string> commonEntryList = GetCommonItemList(baPdbListDict[ba], clusterPairBaCommonEntryListDict[clusterPairList[j]][ba]);
                            if (commonEntryList.Count > 0)
                            {
                                if (moreClusterBaCommonEntryListDict.ContainsKey(clusterListString))
                                {
                                    if (! moreClusterBaCommonEntryListDict[clusterListString].ContainsKey(ba))
                                    {
                                        moreClusterBaCommonEntryListDict[clusterListString].Add(ba, commonEntryList);
                                    }
                                }
                                else
                                {
                                    Dictionary<string, List<string>> baCommonEntryListDict = new Dictionary<string, List<string>>();
                                    baCommonEntryListDict.Add(ba, commonEntryList);
                                    moreClusterBaCommonEntryListDict.Add(clusterListString, baCommonEntryListDict);
                                }
                            }
                        }
                    }
                }
            }
            return moreClusterBaCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterPairBaCommonEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetCommonEntryListDict(Dictionary<string, List<string>> clusterPairCommonEntryListDict)
        {
            string clusterListString = "";
            Dictionary<string, List<string>> moreClusterCommonEntryListDict = new Dictionary<string, List<string>>();
            List<string> clusterPairList = new List<string>(clusterPairCommonEntryListDict.Keys);
            clusterPairList.Sort();

            for (int i = 0; i < clusterPairList.Count; i++)
            {
                for (int j = i + 1; j < clusterPairList.Count; j++)
                {
                    clusterListString = GetClusterCombListString(clusterPairList[i], clusterPairList[j]);
                    List<string> commonEntryList = GetCommonItemList(clusterPairCommonEntryListDict[clusterPairList[i]], clusterPairCommonEntryListDict[clusterPairList[j]]);
                    if (commonEntryList.Count > 0)
                    {
                        if (!moreClusterCommonEntryListDict.ContainsKey(clusterListString))
                        {
                            moreClusterCommonEntryListDict.Add(clusterListString, commonEntryList);
                        }
                    }
                }
            }

            return moreClusterCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterPair1"></param>
        /// <param name="clusterPair2"></param>
        /// <returns></returns>
        private string GetClusterCombListString (string clusterPair1, string clusterPair2)
        {
            List<string> splitClusterList = new List<string>();
            splitClusterList.AddRange(clusterPair1.Split('-'));

            string[] clusterIdFields = clusterPair2.Split('-');
            foreach (string clusterIdField in clusterIdFields)
            {
                if (!splitClusterList.Contains(clusterIdField))
                {
                    splitClusterList.Add(clusterIdField);
                }
            }
            string clusterListString = "";
            splitClusterList.Sort();
            foreach (string clusterField in splitClusterList)
            {
                clusterListString += clusterField + "-";
            }
            clusterListString = clusterListString.TrimEnd('-');
            return clusterListString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="itemList1"></param>
        /// <param name="itemList2"></param>
        /// <returns></returns>
        private List<T> GetCommonItemList<T> (List<T> itemList1, List<T> itemList2)
        {
            List<T> commonItemList = new List<T>();
            foreach (T item in itemList1)
            {
                if (itemList2.Contains (item))
                {
                    commonItemList.Add(item);
                }
            }
            return commonItemList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterId"></param>
        /// <param name="pdbId"></param>
        /// <param name="ba"></param>
        /// <param name="clusterBaEntryListDict"></param>
        private void AddEntryBaToDict (int clusterId, string pdbId, string ba, ref Dictionary<int, Dictionary<string, List<string>>> clusterBaEntryListDict)
        {
            if (clusterBaEntryListDict.ContainsKey(clusterId))
            {
                if (clusterBaEntryListDict[clusterId].ContainsKey(ba))
                {
                    clusterBaEntryListDict[clusterId][ba].Add(pdbId);
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    clusterBaEntryListDict[clusterId].Add(ba, entryList);
                }
            }
            else
            {
                List<string> entryList = new List<string>();
                entryList.Add(pdbId);
                Dictionary<string, List<string>> baEntryListDict = new Dictionary<string, List<string>>();
                baEntryListDict.Add(ba, entryList);
                clusterBaEntryListDict.Add(clusterId, baEntryListDict);
            }
        }
        #endregion

        #region biol domain clusters from different Pfam-Pfam
        private int numOfLoops = 5;
        public void PrintClustersDifPfamRelations ()
        {
   //         string difPfamClustersFile = Path.Combine(biolAssemDataDir, "ClustersInDifPfamRelations_Pfams_" + minNumofPfams + "-" + maxNumOfPfams + ".txt");
            string difPfamClustersFile = Path.Combine(biolAssemDataDir, "ClustersInDifPfamRelations_Pfams_common.txt");
            List<List<string>> connectedPfamsList = new List<List<string>>();
            List<List<int>> connectedRelIdsList = GetConnectedRelationsWithStrongClusters(out connectedPfamsList);
            StreamWriter dataWriter = new StreamWriter(difPfamClustersFile);
            string dataLine = "";
            for (int i = 0; i < connectedRelIdsList.Count;  i ++ )
            {
     /*           if (connectedPfamsList[i].Count > maxNumOfPfams || connectedPfamsList[i].Count < minNumofPfams)
                {
                    continue;
                }*/
                dataLine = "";
                Dictionary<int, string> relPfamPairDict = GetRelationPfamPairs(connectedRelIdsList[i].ToArray());
                foreach (int relSeqId in connectedRelIdsList[i])
                {
                    dataLine += (relSeqId + "(" + relPfamPairDict[relSeqId] + "),");
                }
                dataWriter.WriteLine(dataLine.TrimEnd(','));
       //         dataWriter.WriteLine(FormatArrayString(connectedRelIdsList[i]));
                dataWriter.WriteLine(FormatArrayString(connectedPfamsList[i]));
                dataWriter.Flush();
                Dictionary<string, List<string>> relBigClusterEntryListDict = new Dictionary<string, List<string>> ();
                GetRelationBigClusterDict(connectedRelIdsList[i].ToArray(), relBigClusterEntryListDict);
     /*           foreach (string cluster in relBigClusterEntryListDict.Keys)
                {
                    dataWriter.WriteLine(cluster + "\t" + FormatArrayString(relBigClusterEntryListDict[cluster]));
                }*/
                List<string> commonEntryClustersList = new List<string>();
 //               Dictionary<string, List<string>> relClusterCommonEntryListDict = GetRelationClustersCommonEntryListDict(relBigClusterEntryListDict, out commonEntryClustersList);
                Dictionary<string, List<string>> relClusterCommonEntryListDict = GetConnectedRelationClustersCommonEntryListDict(relBigClusterEntryListDict, out commonEntryClustersList);
                    
                foreach (string clusters in commonEntryClustersList)
                {
                    dataWriter.WriteLine(clusters + "\t" + FormatArrayString(relClusterCommonEntryListDict[clusters]));
                }
                dataWriter.WriteLine();
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetRelationPfamPairs (int[] relSeqIds)
        {
            string queryString = string.Format("Select RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID IN ({0});", 
                ParseHelper.FormatSqlListString (relSeqIds));
            DataTable relPfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<int, string> relPfamPairDict = new Dictionary<int, string>();
            int relSeqId = 0;
            string pfamPair = "";
            foreach (DataRow relRow in relPfamPairTable.Rows)
            {
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                pfamPair = relRow["FamilyCode1"].ToString().TrimEnd();
                if (relRow["FamilyCode1"].ToString () != relRow["FamilyCode2"].ToString ())
                {
                    pfamPair += (";" + relRow["FamilyCode2"].ToString());
                }
                relPfamPairDict.Add(relSeqId, pfamPair);
            }
            return relPfamPairDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relBigClusterEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetRelationClustersCommonEntryListDict(Dictionary<string, List<string>> relBigClusterEntryListDict, out List<string> commonEntryClustersList)
        {
            bool hasCommon = true;
            Dictionary<string, List<string>> relClusterCommonEntryListDict = new Dictionary<string, List<string>> ();
            List<string> newMergedClusterList = new List<string>(relBigClusterEntryListDict.Keys);
            commonEntryClustersList = new List<string>();
            List<string> commonEntryList = null;
            string clusterListString = "";
            int loopCount = 1;
            while (hasCommon && loopCount < numOfLoops)
            {
                List<string> clusterList = new List<string>(newMergedClusterList);
                newMergedClusterList.Clear();
                for (int i = 0; i < clusterList.Count; i++)
                {
                    for (int j = i + 1; j < clusterList.Count; j++)
                    {
                        if (AreClustersSamePfamRelations (clusterList[i], clusterList[j]))
                        {
                            continue;
                        }                        
                        clusterListString = FormatClusterListString(clusterList[i], clusterList[j]);
                        if (relClusterCommonEntryListDict.ContainsKey(clusterListString))
                        {
                            continue;
                        }
                        if (relBigClusterEntryListDict.ContainsKey(clusterList[i]) && relBigClusterEntryListDict.ContainsKey(clusterList[j]))
                        {
                            commonEntryList = GetCommonItemList(relBigClusterEntryListDict[clusterList[i]], relBigClusterEntryListDict[clusterList[j]]);
                        }
                        else
                        {
                            commonEntryList = GetCommonItemList(relClusterCommonEntryListDict[clusterList[i]], relClusterCommonEntryListDict[clusterList[j]]);
                        }
                        if (commonEntryList.Count > 0)
                        {                          
                            relClusterCommonEntryListDict.Add(clusterListString, commonEntryList);
                            newMergedClusterList.Add(clusterListString);
                            commonEntryClustersList.Add(clusterListString);
                        }
                    }
                }
                if (newMergedClusterList.Count <= 1)
                {
                    break;
                }
                loopCount++;
            }
            return relClusterCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relBigClusterEntryListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetConnectedRelationClustersCommonEntryListDict(Dictionary<string, List<string>> relBigClusterEntryListDict, out List<string> commonEntryClustersList)
        {
            commonEntryClustersList = new List<string>();
            Dictionary<string, List<string>> relClusterCommonEntryListDict = new Dictionary<string, List<string>>();
            List<string> clusterList = new List<string>(relBigClusterEntryListDict.Keys);
            List<string> parsedClusterList = new List<string>();
            List<int> parsedRelIdList = new List<int>();
            int relIdI = 0;
            for (int i = 0; i < clusterList.Count; i++)
            {
                if (parsedClusterList.Contains(clusterList[i]))
                {
                    continue;
                }
                string[] clusterFields = clusterList[i].Split('-');
                relIdI = Convert.ToInt32(clusterFields[0]);
                if (parsedRelIdList.Contains(relIdI))
                {
                    continue;
                }
                parsedRelIdList.Add(relIdI);
                List<string> commonEntryList = new List<string>(relBigClusterEntryListDict[clusterList[i]]);
                List<string> commonClusterList = new List<string>();
                commonClusterList.Add(clusterList[i]);
                parsedClusterList.Add(clusterList[i]);
                for (int j = i + 1; j < clusterList.Count; j++)
                {
                    if (parsedClusterList.Contains(clusterList[j]))
                    {
                        continue;
                    }                    
                    List<string> clusterCommonEntryList = GetCommonItemList(commonEntryList, relBigClusterEntryListDict[clusterList[j]]);
                    if (clusterCommonEntryList.Count > 0)
                    {
                        parsedClusterList.Add(clusterList[j]);
                        commonClusterList.Add(clusterList[j]);
                        commonEntryList = clusterCommonEntryList;
                    }                 
                }
                if (commonClusterList.Count > 1)
                {
                    string clusterListString = FormatClusterListString(commonClusterList.ToArray());
                    relClusterCommonEntryListDict.Add(clusterListString, commonEntryList);
                    commonEntryClustersList.Add(clusterListString);
                }
            }
            return relClusterCommonEntryListDict;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="cluster1"></param>
        /// <param name="cluster2"></param>
        /// <returns></returns>
        private string FormatClusterListString (string cluster1, string cluster2)
        {
            string[] clusterFields1 = cluster1.Split('_');
            List<string> clusterList = new List<string>(clusterFields1);
            string[] clusterFields2 = cluster2.Split('_');
            foreach (string cluster in clusterFields2)
            {
                if (! clusterList.Contains (cluster))
                {
                    clusterList.Add(cluster);
                }
            }
            return FormatClusterListString(clusterList.ToArray ());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusters"></param>
        /// <returns></returns>
        private string FormatClusterListString (string[] clusters)
        {
            Dictionary<int, string> relClusterDict = new Dictionary<int, string>();
            int relId = 0;
            foreach (string cluster in clusters)
            {
                string[] fields = cluster.Split('-');
                relId = Convert.ToInt32(fields[0]);
                if (! relClusterDict.ContainsKey (relId))
                {
                    relClusterDict.Add(relId, cluster);
                }
            }
            List<int> relIdList = new List<int>(relClusterDict.Keys);
            relIdList.Sort();
            string clusterListString = "";
            foreach (int lsRelId in relIdList)
            {
                clusterListString += (relClusterDict[lsRelId] + "_");
            }
            return clusterListString.TrimEnd('_');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cluster1"></param>
        /// <param name="cluster2"></param>
        /// <returns></returns>
        private bool AreClustersSamePfamRelations (string cluster1, string cluster2)
        {
            string relIds1 = GetClusterPfamRelSeqList(cluster1);
            string relIds2 = GetClusterPfamRelSeqList(cluster2);
            if (relIds1 == relIds2)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterList"></param>
        /// <returns></returns>
        private string GetClusterPfamRelSeqList (string clusterList)
        {
            string[] clusterFields1 = clusterList.Split("-_".ToCharArray());
            List<int> relIdList = new List<int>();
            int relId = 0;
            for (int i = 0; i < clusterFields1.Length; i += 2)
            {
                relId = Convert.ToInt32(clusterFields1[i]);
                if (! relIdList.Contains(relId))
                {
                    relIdList.Add(relId);
                }
            }
            string relIdListString = "";
            relIdList.Sort();
            foreach (int lsRelId in relIdList)
            {
                relIdListString += (lsRelId.ToString() + "_");
            }
            return relIdListString.TrimEnd('_');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        public void GetRelationBigClusterDict(int[] relSeqIds, Dictionary<string, List<string>> relClusterEntryListDict)
        {
            foreach (int relSeqId in relSeqIds)
            {
                Dictionary<string, List<string>> clusterEntryListDict = GetRelationBigClusterEntries (relSeqId);
                foreach (string cluster in clusterEntryListDict.Keys)
                {
                    relClusterEntryListDict.Add(cluster, clusterEntryListDict[cluster]);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetRelationBigClusterEntries (int relSeqId)
        {
            string queryString = string.Format("Select Distinct PfamDomainClusterInterfaces.ClusterId, PfamDomainClusterInterfaces.PdbID " + 
                " From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo " +
                 " Where PfamDomainClusterSumInfo.RelSeqID = {0} AND NumOfCfgCluster > {1} AND MinSeqIdentity < 90 AND " +
                 " PfamDomainClusterSumInfo.RelSeqID = PfamDomainClusterInterfaces.RelSeqID AND " + 
                 " PfamDomainClusterSumInfo.ClusterID = PfamDomainClusterInterfaces.ClusterID;", relSeqId, cfgClusterCutoff);
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string cluster = "";
            Dictionary<string, List<string>> clusterEntryListDict = new Dictionary<string, List<string>>();
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                cluster = relSeqId + "-" + entryRow["ClusterID"].ToString ();
                if (clusterEntryListDict.ContainsKey (cluster))
                {
                    clusterEntryListDict[cluster].Add(entryRow["PdbID"].ToString());
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(entryRow["PdbID"].ToString());
                    clusterEntryListDict.Add(cluster, entryList);
                }
            }
            return clusterEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterEntryListDict1"></param>
        /// <param name="clusterEntryListDict2"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetCommonEntries (Dictionary<string, List<string>> clusterEntryListDict1, 
            Dictionary<string, List<string>> clusterEntryListDict2)
        {
            string clusterPair = "";
            Dictionary<string, List<string>> difRelClusterCommonEntryListDict = new Dictionary<string, List<string>>();
            foreach (string cluster1 in clusterEntryListDict1.Keys)
            {
                foreach (string cluster2 in clusterEntryListDict2.Keys)
                {
                    clusterPair = cluster1 + "_" + cluster2;
                    List<string> commonEntryList = GetCommonItemList(clusterEntryListDict1[cluster1], clusterEntryListDict2[cluster2]);
                    if (commonEntryList.Count > 0)
                    {
                        difRelClusterCommonEntryListDict.Add(clusterPair, commonEntryList);
                    }
                }
            }
            return difRelClusterCommonEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectedPfamsList"></param>
        /// <returns></returns>
        public List<List<int>>  GetConnectedRelationsWithStrongClusters (out List<List<string>> connectedPfamsList)
        {
            string queryString = string.Format("Select Distinct PfamDomainFamilyRelation.* From PfamDomainClusterSumInfo, PfamDomainFamilyRelation " +
                " Where NumOfCfgCluster > {0} AND MinSeqIdentity < 90 AND PfamDomainClusterSumInfo.RelSeqID = PfamDomainFamilyRelation.RelSeqID;",
                cfgClusterCutoff);
            DataTable pfamRelIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            connectedPfamsList = new List<List<string>>();
            List<List<int>> connectedPfamRelIdsList = new List<List<int>>();
            int relSeqId = 0;
            Dictionary<int, string[]> relationPfamPairsDict = new Dictionary<int, string[]>();
            foreach (DataRow relIdRow in pfamRelIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relIdRow["RelSeqID"].ToString());
                string[] pfamPair = new string[2];
                pfamPair[0] = relIdRow["FamilyCode1"].ToString().TrimEnd();
                pfamPair[1] = relIdRow["FamilyCode2"].ToString().TrimEnd();
                relationPfamPairsDict.Add(relSeqId, pfamPair);
            }

            List<int> relIdList = new List<int>(relationPfamPairsDict.Keys);
            relIdList.Sort();
            List<int> addedRelIdList = new List<int>();
            for (int i = 0; i < relIdList.Count; i++)
            {
                if (addedRelIdList.Contains(relIdList[i]))
                {
                    continue;
                }
                List<int> connectedRelIdList = new List<int>();
                connectedRelIdList.Add(relIdList[i]);
                addedRelIdList.Add(relIdList[i]);
                List<string> connectedPfamList = new List<string>();
                connectedPfamList.Add(relationPfamPairsDict[relIdList[i]][0]);
                if (!connectedPfamList.Contains(relationPfamPairsDict[relIdList[i]][1]))
                {
                    connectedPfamList.Add(relationPfamPairsDict[relIdList[i]][1]);
                }
                for (int j = i + 1; j < relIdList.Count; j++)
                {
                    if (addedRelIdList.Contains(relIdList[j]))
                    {
                        continue;
                    }
                    if (connectedPfamList.Contains(relationPfamPairsDict[relIdList[j]][0]))
                    {
                        if (!connectedPfamList.Contains(relationPfamPairsDict[relIdList[j]][1]))
                        {
                            connectedPfamList.Add(relationPfamPairsDict[relIdList[j]][1]);
                        }
                        addedRelIdList.Add(relIdList[j]);
                        connectedRelIdList.Add(relIdList[j]);
                    }
                    else if (connectedPfamList.Contains(relationPfamPairsDict[relIdList[j]][1]))
                    {
                        if (!connectedPfamList.Contains(relationPfamPairsDict[relIdList[j]][0]))
                        {
                            connectedPfamList.Add(relationPfamPairsDict[relIdList[j]][0]);
                        }
                        addedRelIdList.Add(relIdList[j]);
                        connectedRelIdList.Add(relIdList[j]);
                    }
                }
                connectedPfamRelIdsList.Add(connectedRelIdList);
                connectedPfamsList.Add(connectedPfamList);
            }
            return connectedPfamRelIdsList;
        }
        #endregion


        #region 4HBT human proteins
        public void RetrieveHumanProteins ()
        {
            string pfamUnpFile = Path.Combine(biolAssemDataDir, "4HBT\\PF03061_uniprot.txt");
            string pfamHumanUnpFile = Path.Combine(biolAssemDataDir, "4HBT\\PF03061_HUMAN_seq.txt");
            StreamReader unpAccReader = new StreamReader(pfamUnpFile);
            StreamWriter unpAccWriter = new StreamWriter(pfamHumanUnpFile);
            string line = "";
            string unpAcc = "";
            while ((line = unpAccReader.ReadLine ()) != null)
            {
                if (line.Substring (0, 1) == ">")
                {
                    string[] fields = line.Substring(1, line.Length - 1).Split('/');
                    unpAcc = fields[0];
                    string[] unpSeqInfo = GetHumanUnpCode(unpAcc);
                    if (unpSeqInfo[0] != null && unpSeqInfo[0] != "")
                    {
                        unpAccWriter.Write(">" + unpSeqInfo[0] + "\n");
                        unpAccWriter.Write(unpSeqInfo[1] + "\n");
                    }
         //          unpAccWriter.Write(fields[0] + "\n");
                }
            }
            unpAccReader.Close();
            unpAccWriter.Close();
        }

        private string[] GetHumanUnpCode (string unpAcc)
        {
            string queryString = string.Format("Select Distinct UnpCode, Sequence From HumanSeqInfo Where UnpAccession = '{0}' AND Isoform = 0;", unpAcc);
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] unpSeqInfo = new string[2];
            if (humanUnpTable.Rows.Count > 0)
            {
                unpSeqInfo[0] = humanUnpTable.Rows[0]["UnpCode"].ToString().Trim();
                unpSeqInfo[1] = humanUnpTable.Rows[0]["Sequence"].ToString();
            }
            return unpSeqInfo;
        }

        public void ReformatClustalOmegaPimMatrix ()
        {
            string pimDataDir = Path.Combine (dataDir, @"BiolAssem\4HBT\seq_phylogenetic");
            string allSeqFile = "737_seq.fasta";
            string[] allUnps = ReadClusterUnps(Path.Combine(pimDataDir, allSeqFile));
            string[] clusterSeqFiles = { "4HBT_cluster1-2_seq.txt", "4HBT_cluster1-5_seq.txt", "4HBT_cluster1-6_seq.txt" };
            string[][] clusterUnpsList = new string[4][];
            List<string> leftCluster1UnpList = new List<string>(allUnps);
            for (int i = 0; i < clusterSeqFiles.Length; i ++)
            {
                string[] clusterUnps = ReadClusterUnps(Path.Combine(pimDataDir, clusterSeqFiles[i]));
                clusterUnpsList[i] = clusterUnps;
                foreach (string unp in clusterUnps)
                {
                    leftCluster1UnpList.Remove(unp);
                }
            }
            clusterUnpsList[3] = leftCluster1UnpList.ToArray();
            string[] clusterCombNames = {"cluster1-2", "cluster1-5", "cluster1-6", "cluster1-left"};

            ReadAddedUniprotsPimData(clusterUnpsList, clusterCombNames, pimDataDir);

            string outFile = Path.Combine(pimDataDir, "4hbt_clustalo_pim_inter_intra.txt");
            string[][] intraInterPimData = ReadIntraInterClustersPimData(clusterUnpsList, Path.Combine(pimDataDir, "4hbt_all_clustalo.pim"), clusterCombNames);
            StreamWriter dataWriter = new StreamWriter(outFile);
            dataWriter.Write("Intra " + ParseHelper.FormatArrayString(intraInterPimData[0], ' ') + "\n");
            dataWriter.Write("Inter " + ParseHelper.FormatArrayString(intraInterPimData[1], ' ') + "\n");
            dataWriter.Close();

            string[] pimFiles = { "4hbt_cluster1-2_clustalo.pim", "4hbt_cluster1-5_clustalo.pim", "4hbt_cluster1-6_clustalo.pim", "4hbt_all_clustalo.pim", 
                                "4hbt_all_addedhumanproteins_clustalo.pim", "4hbt_cluster1-6_addedhumanproteins_clustalo.pim"};
            outFile = Path.Combine(pimDataDir, "4hbt_clustalo_pim_inter_intra.txt");
            StreamWriter pimWriter = new StreamWriter(outFile);
            string dataLine = "";
            string colName = "";
            foreach (string pimFile in pimFiles)
            {
                string[] fields = pimFile.Split('_');
                colName = fields[1].Replace ("-", "_");
                if (fields.Length == 4)
                {
                    colName = colName + "_add";
                }
                dataLine = ReadPimFromFile(Path.Combine(pimDataDir, pimFile));
                pimWriter.Write(colName + " " + dataLine + "\n");
            }
            pimWriter.Close();
        }

        public void ReadAddedUniprotsPimData(string[][] clusterUnpsList, string[] clusterCombNames, string pimDataDir)
        {
            string addedAllPimFile = "4hbt_all_addedhumanproteins_clustalo.pim";
            string[] pimUnps = null;
            string[][] pimMatrix = ReadPimMatrixFromFile(Path.Combine (pimDataDir, addedAllPimFile), out pimUnps);
            string[] addedUnps = { "BACHL_HUMAN", "ACO11_HUMAN", "ACO12_HUMAN", "ACOT9_HUMAN" };

            string addedUnpPimFile = "";
            int addedUnpIndex = -1;
            string clustersName = "";
            int clusterCombIndex = 0;
            foreach (string addedUnp in addedUnps)
            {
                addedUnpPimFile = Path.Combine(pimDataDir, addedUnp.Replace("_HUMAN", "") + "_pim.txt");
                StreamWriter dataWriter = new StreamWriter(addedUnpPimFile);
                addedUnpIndex = Array.IndexOf(pimUnps, addedUnp);
                string[] addedUnpPimRow = pimMatrix[addedUnpIndex];
                for (int i = 0; i < addedUnpPimRow.Length; i ++)
                {
                    if (addedUnp == pimUnps[i])
                    {
                        continue;
                    }
                    clusterCombIndex = GetUnpClusterCombID(pimUnps[i], clusterUnpsList);
                    if (clusterCombIndex > -1)
                    {
                        clustersName = clusterCombNames[clusterCombIndex];
                    }
                    dataWriter.Write(pimUnps[i] + "\t" + addedUnpPimRow[i] + "\t" + clustersName + "\n");
                }
                dataWriter.Close();
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterSeqFile"></param>
        /// <returns></returns>
        private string[] ReadClusterUnps (string clusterSeqFile)
        {
            StreamReader dataReader = new StreamReader(clusterSeqFile);
            string line = "";
            List<string> unpList = new List<string> ();
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.Substring (0, 1) == ">")
                {
                    unpList.Add(line.TrimStart('>'));
                }
            }
            dataReader.Close();

            return unpList.ToArray();
        }

        private string[][] ReadIntraInterClustersPimData(string[][] clusterCombUnpList, string allPimFile, string[] clusterCombNames)
        {
            string[] pimUnps = null;
            string[][] pimMatrix = ReadPimMatrixFromFile (allPimFile, out pimUnps);
            string rmatrixFile = allPimFile.Replace ("_clustalo", "_rmatrix");
            WritePimToRMatrixFile(pimMatrix, pimUnps, rmatrixFile);
           
            List<string> intraClusterPimList = new List<string>();
            List<string> interClusterPimList = new List<string>();
            Dictionary<string[], string> intraUnpPairPimDict = new Dictionary<string[], string>();
            Dictionary<string[], string> interUnpPairPimDict = new Dictionary<string[], string>();
            for (int rowNum = 0; rowNum < pimMatrix.Length; rowNum ++)
            {
                for (int colNum = 0; colNum < pimMatrix[rowNum].Length; colNum++)
                {
                    if (rowNum <= colNum)
                    {
                        continue;
                    }
                    if (IsPimIntra (pimUnps[rowNum], pimUnps[colNum], clusterCombUnpList))
                    {
                        intraClusterPimList.Add(pimMatrix[rowNum][colNum]);
                        string[] unpPair = new string[2];
                        unpPair[0] = pimUnps[rowNum];
                        unpPair[1] = pimUnps[colNum];
                        intraUnpPairPimDict.Add(unpPair, pimMatrix[rowNum][colNum]);
                    }
                    else
                    {
                        interClusterPimList.Add(pimMatrix[rowNum][colNum]);
                        string[] unpPair = new string[2];
                        unpPair[0] = pimUnps[rowNum];
                        unpPair[1] = pimUnps[colNum];
                        interUnpPairPimDict.Add(unpPair, pimMatrix[rowNum][colNum]);
                    }
                }
            }
            string unpPairPimFile = allPimFile.Replace("_clustalo", "_unpPair");
            StreamWriter unpPairPimWriter = new StreamWriter(unpPairPimFile);
            unpPairPimWriter.Write("Intra\n");
            string combName = "";
            foreach (string[] unpPair in intraUnpPairPimDict.Keys)
            {
                combName = GetUnpPairClusterIDPair(unpPair[0], unpPair[1], clusterCombUnpList, clusterCombNames);
                unpPairPimWriter.Write(unpPair[0] + " " + unpPair[1] + " " + intraUnpPairPimDict[unpPair] + " " + combName + "\n");
            }
            unpPairPimWriter.Write("\n");
            unpPairPimWriter.Write("Inter\n");
            foreach (string[] unpPair in interUnpPairPimDict.Keys)
            {
                combName = GetUnpPairClusterIDPair(unpPair[0], unpPair[1], clusterCombUnpList, clusterCombNames);
                unpPairPimWriter.Write(unpPair[0] + " " + unpPair[1] + " " + interUnpPairPimDict[unpPair] + " " + combName + "\n");
            }
            unpPairPimWriter.Close();
            string[][] intraInterPimList = new string[2][];
            intraInterPimList[0] = intraClusterPimList.ToArray();
            intraInterPimList[1] = interClusterPimList.ToArray();
            return intraInterPimList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pimMatrix"></param>
        /// <param name="unps"></param>
        /// <param name="rmatrixFile"></param>
        private void WritePimToRMatrixFile (string[][] pimMatrix, string[] unps, string rmatrixFile)
        {
            StreamWriter dataWriter = new StreamWriter(rmatrixFile);
            dataWriter.Write(ParseHelper.FormatArrayString(unps, ' ') + "\n");
            foreach (string[] pimRow in pimMatrix)
            {
                dataWriter.Write(ParseHelper.FormatArrayString (pimRow, ' ') + "\n");
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unp1"></param>
        /// <param name="unp2"></param>
        /// <param name="clusterCombUnpList"></param>
        /// <returns></returns>
        private bool IsPimIntra (string unp1, string unp2, string[][] clusterCombUnpList)
        {
            for (int i = 0; i < clusterCombUnpList.Length; i ++)
            {
                int intra = AreTwoUnpIntra(unp1, unp2, clusterCombUnpList[i]);
                if (intra == 1)
                {
                    return true;
                }
                else if (intra == 0)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unp1"></param>
        /// <param name="unp2"></param>
        /// <param name="clusterCombUnpList"></param>
        /// <param name="clusterCombNames"></param>
        /// <returns></returns>
        private string GetUnpPairClusterIDPair (string unp1, string unp2, string[][] clusterCombUnpList, string[] clusterCombNames)
        {
            int combId1 = GetUnpClusterCombID(unp1, clusterCombUnpList);
            int combId2 = GetUnpClusterCombID(unp2, clusterCombUnpList);

            string combName = "";
            if (combId1 > -1 && combId2 > -1)
            {
                if (combId1 == combId2)
                {
                    combName = clusterCombNames[combId1];
                }
                else
                {
                    combName = clusterCombNames[combId1] + " " + clusterCombNames[combId2];
                }
            }
            return combName;
        }

        private int GetUnpClusterCombID (string unp, string[][] clusterCombUnpList)
        {
            for (int i = 0; i < clusterCombUnpList.Length; i++)
            {
                if (clusterCombUnpList[i].Contains (unp))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unp1"></param>
        /// <param name="unp2"></param>
        /// <param name="unpList"></param>
        /// <returns></returns>
        private int AreTwoUnpIntra (string unp1, string unp2, string[] unpList)
        {
            if (unpList.Contains (unp1) )
            {
                if (unpList.Contains(unp2))
                {
                    return 1;  // both in the list
                }
                else
                {
                    return 0;   // only unp1 in the list
                }
            }
            else if (unpList.Contains (unp2))
            {
                return 0;   // only unp2 in the list
            }
            return -1;        // not in the list    
        }

        private string[][] ReadPimMatrixFromFile (string pimFile, out string[] pimUnps)
        {
            string unp = "";
            StreamReader pimReader = new StreamReader(pimFile);
            string line = "";
            int pimLineNum = 0;
            List<string[]> pimLineList = new List<string[]>();
            List<string> pimUnpList = new List<string>();
            while ((line = pimReader.ReadLine ()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.Substring (0, 1) == "#")
                {
                    continue;
                }
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                pimLineNum = Convert.ToInt32(fields[0].TrimEnd(':'));
                unp = fields[1];
                string[] justPimLine = new string[fields.Length - 2];
                Array.Copy(fields, 2, justPimLine, 0, justPimLine.Length);
                pimLineList.Add(justPimLine);
                pimUnpList.Add(unp);
            }
            pimReader.Close();
            pimUnps = pimUnpList.ToArray();
            return pimLineList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pimFile"></param>
        /// <returns></returns>
        private string ReadPimFromFile (string pimFile)
        {
            string line = "";
            List<string> pimList = new List<string>();
            StreamReader dataReader = new StreamReader(pimFile);
            int pimLineNum = 1;
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.Substring (0, 1) == "#")
                {
                    continue;
                }
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                pimLineNum = Convert.ToInt32(fields[0].TrimEnd (':'));
                for (int i = pimLineNum + 2; i < fields.Length; i ++)
                {
                    pimList.Add(fields[i]);
                }
            }
            dataReader.Close();
            string pimDataLine = ParseHelper.FormatArrayString(pimList, ' ');
            return pimDataLine;
        }
        #endregion
    }
}
