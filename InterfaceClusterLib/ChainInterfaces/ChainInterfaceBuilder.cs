using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using DbLib;
using InterfaceClusterLib.DataTables;
using InterfaceClusterLib.InterfaceImage;
using InterfaceClusterLib.ProtCid;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class ChainInterfaceBuilder
    {
        public static string[] antibodyGroups = { "(C1-set)", "(V-set)", "(V-set)_(C1-set)" };
        private static DbQuery dbQuery = new DbQuery();

        #region build
        public void BuildChainInterfaceClusters(int operationStep)
        {
            ProtCidSettings.dataType = "pfam";

            DbBuilderHelper.Initialize();
            switch (operationStep)
            {
                case 1:
                    try
                    {
                        ChainGroupsClassifier chainGroupClassifier = new ChainGroupsClassifier();
                        chainGroupClassifier.CombineFamilyGroups();
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Classify chain relations error: " +
                            ex.Message);
                        ProtCidSettings.logWriter.WriteLine("Classify chain relations error: " +
                            ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }

                    break;
                //     goto case 2;
            

                case 2:
                    ChainGroupRepEntryComp interGroupRepEntryComp = new ChainGroupRepEntryComp();
                    interGroupRepEntryComp.CompareTwoEntries("5uvz", "2dww");
                     interGroupRepEntryComp.CompareInterGroupRepEntryInterfacesInChainGroups();
               //     interGroupRepEntryComp.PrintMissingAlignInterGroupRepEntries();   
                
                    Alignments.GroupEntryAlignments groupAlign = new Alignments.GroupEntryAlignments();
                    groupAlign.GetRepChainPairsToBeAligned();

       //             interGroupRepEntryComp.CompareMissingEntryPairs();     
                    break;
                //  goto case 3;

                case 3:
                   ChainInterfaceCluster interfaceCluster = new ChainInterfaceCluster();
                     interfaceCluster.ClusterChainGroupInterfaces(); 

                /*    int[] abSuperGroupIds = { 744};
                    interfaceCluster.UpdateSuperGroups(abSuperGroupIds);  */
                    break;
             //       goto case 4;

                case 4:
                    ChainClusterStat superClusterStat = new ChainClusterStat();
//                    superClusterStat.PrintChainClusterTextFilesFromDb ();
                    superClusterStat.PrintSupergroupInterfaceClusters("pfam");
                    //    goto case 5;
                    break;

                case 5:
                    InterfaceFilesReverser interfaceFileReverse = new InterfaceFilesReverser();
                //    interfaceFileReverse.UpdateIsSymmetry();
                   string clusterReverseFileList = interfaceFileReverse.ReverseClusterInterfaceFiles();
#if DEBUG
                    /////////////////////////////////////
                    // the clusterReverseFileList is still a problem, file not exist exception, probably due to change the current directory
                    // but I can not detect where the problem is.
                    // should fix it.
                    ////////////////////////////////////
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("The current working directory: " + System.Environment.CurrentDirectory);                   
                    ProtCidSettings.logWriter.WriteLine("The current working directory: " + System.Environment.CurrentDirectory);
                    FileInfo fileInfo = new FileInfo(clusterReverseFileList);
                    ProtCidSettings.logWriter.WriteLine("The directory of clusterReverseFileList file: " + fileInfo.DirectoryName);
#endif

                    clusterReverseFileList = Path.Combine(ProtCidSettings.applicationStartPath, clusterReverseFileList);
                    ChainClusterCompress clusterFileCompress = new ChainClusterCompress();
                    clusterFileCompress.CompressClusterInterfaceFiles(clusterReverseFileList);
                    // copy the interface files for those not in any clusters
                    // save disk space in the web server
                    clusterFileCompress.RetrieveCrystInterfaceFilesNotInClusters(false);
  
                    InterfaceImageGen imageGen = new InterfaceImageGen();
                    imageGen.GenerateInterfaceImages(); 

                 //   goto case 6;
                  break;

                case 6:
                    SeqFastaGenerator seqFastaGen = new SeqFastaGenerator();
                    seqFastaGen.WriteSequencesToFastaFiles();
                    break;
                //   goto case 7;

                case 7:
                    BiolUnitSumInfo buSumInfo = new BiolUnitSumInfo();
       //             buSumInfo.UpdateBiolUnits();
                    buSumInfo.RetrieveBiolUnits();
                    break;

                default:
                    break;
            }
            DbBuilderHelper.UpdateIndexes("PfamSuper", ProtCidSettings.protcidDbConnection);

            ProtCidSettings.progressInfo.threadFinished = true;
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        public void UpdateChainInterfaceClusters()
        {
            ProtCidSettings.dataType = "pfam";

            DbBuilderHelper.Initialize();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating PFAM SuperGroup Interfaces Data.");

  //          ChainClusterStat clusterStat = new ChainClusterStat();
  //          clusterStat.UpdateMinSeqIdentityInDb();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortTimeString ());
            ProtCidSettings.logWriter.WriteLine("Updating PFAM SuperGroup Interfaces Data.");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update biological units and entrypfamarch data for protcid");

            Dictionary<int, string[]> updatedGroupHash = GetUpdatedGroups();
            Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash = null;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Super Groups.");
//            ChainGroupsClassifier superGroupClassifier = new ChainGroupsClassifier();
//            updateSuperGroupHash = superGroupClassifier.UpdateFamilySuperGroups(updatedGroupHash);

            if (updateSuperGroupHash == null)
            {
                updateSuperGroupHash = new Dictionary<int,Dictionary<int,string[]>> ();
                StreamReader dataReader = new StreamReader("UpdateSuperGroups.txt");
                string line = "";
                int superGroupId = -1;
                Dictionary<int, string[]> updateGroupEntryHash = null;
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line == "")
                    {
                        continue;
                    }
                    if (line.Substring(0, 1) == "#")
                    {
                        if (superGroupId != -1)
                        {
                            updateSuperGroupHash.Add(superGroupId, updateGroupEntryHash);
                        }
                        superGroupId = Convert.ToInt32(line.Substring(1, line.Length - 1));
                        updateGroupEntryHash = new Dictionary<int,string[]> ();
                    }
                    if (line.IndexOf(":") > -1)
                    {
                        string[] fields = line.Split(':');
                        string[] entries = fields[1].Split(',');
                        updateGroupEntryHash.Add(Convert.ToInt32(fields[0]), entries);
                    }
                }
                if (superGroupId > -1)
                {
                    updateSuperGroupHash.Add(superGroupId, updateGroupEntryHash);
                }
                dataReader.Close();
            }
            List<int> updateSuperGroupList = new List<int> (updateSuperGroupHash.Keys);
            updateSuperGroupList.Sort();
            int[] updateSuperGroups = new int[updateSuperGroupList.Count];
            updateSuperGroupList.CopyTo(updateSuperGroups);

            string[] updateEntries = GetUpdateEntries(updateSuperGroupHash);

            // clear the any existing file to make sure the file containing only those from
            // the comparison between groups.
            string nonAlignedPairFile = "NonAlignedEntryPairs.txt";
            if (File.Exists(nonAlignedPairFile))
            {
                // before delete, make a copy
                File.Copy(nonAlignedPairFile, Path.Combine(ProtCidSettings.dirSettings.fatcatPath, nonAlignedPairFile), true);
                File.Delete(nonAlignedPairFile);
            }
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the interface comparisons between groups.");
            ChainGroupRepEntryComp interGroupRepEntryComp = new ChainGroupRepEntryComp();
            interGroupRepEntryComp.UpdateEntryComparisonInSuperGroups(updateSuperGroupHash);
    //        interGroupRepEntryComp.CompareMissingEntryPairs();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating super groups clustering.");
            ChainInterfaceCluster interfaceCluster = new ChainInterfaceCluster();
            interfaceCluster.UpdateSuperGroupClusters (updateSuperGroups);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the summary data.");
            ChainClusterStat superClusterStat = new ChainClusterStat();
            superClusterStat.UpdateSupergroupInterfaceClustersSumInfo (updateSuperGroups, updateEntries, "pfam", true);
//            superClusterStat.UpdateProtCidChainMetaData("pfam", updateEntries, updateSuperGroups);

            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating tar interface files.");
            InterfaceFilesReverser interfaceFileReverse = new InterfaceFilesReverser();
            string updateClusterReverseFileList = interfaceFileReverse.ReverseClusterInterfaceFiles(updateSuperGroupHash);

     //       string updateClusterReverseFileList = "ReverseInterfacesInCluster.txt";
            updateClusterReverseFileList = Path.Combine(ProtCidSettings.applicationStartPath, updateClusterReverseFileList);
            
  //          string updateClusterReverseFileList = "ReverseInterfacesInCluster.txt";
            ChainClusterCompress clusterFileCompress = new ChainClusterCompress();
            clusterFileCompress.CompressGroupClusterInterfaceFiles(updateSuperGroups, updateClusterReverseFileList);
            clusterFileCompress.UpdateCrystInterfaceFilesNotInClusters(updateSuperGroups, false);
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating sequence files.");
            ProtCidSettings.logWriter.WriteLine("Updating sequence files.");
            SeqFastaGenerator seqFastaGen = new SeqFastaGenerator();
            seqFastaGen.UpdateSeqFastaFiles(updateSuperGroups);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update biological units and entrypfamarch data for protcid");
            ProtCidSettings.logWriter.WriteLine("Update biological units and entrypfamarch data for protcid");
            BiolUnitSumInfo buSumInfo = new BiolUnitSumInfo();
            buSumInfo.UpdateBiolUnits(updateEntries);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
   
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating cluster interface images.");
            ProtCidSettings.logWriter.WriteLine("Updating cluster interface images.");
            InterfaceImageGen imageGen = new InterfaceImageGen();
            imageGen.UpdateClusterInterfaceImages(updateSuperGroups);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
         
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating indexes for super groups tables");
            ProtCidSettings.logWriter.WriteLine("Updating indexes for super groups tables");
            DbBuilderHelper.UpdateIndexes("PfamSuper", ProtCidSettings.protcidDbConnection);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateChainInterfaceQscores ()
        {
            ProtCidSettings.dataType = "pfam";

            DbBuilderHelper.Initialize();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Q scores of SuperGroups");

            ChainGroupRepEntryComp interGroupRepEntryComp = new ChainGroupRepEntryComp();
            interGroupRepEntryComp.CompareMissingEntryPairs();
/*
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortTimeString());
            ProtCidSettings.logWriter.WriteLine("Updating Q scores of chain interfaces in  chain groups.");
            ProtCidSettings.logWriter.Flush();

            Hashtable updatedGroupHash = GetUpdatedGroups();
            Hashtable updateSuperGroupHash = null;

            if (updateSuperGroupHash == null)
            {
                updateSuperGroupHash = new Hashtable();
                StreamReader dataReader = new StreamReader("UpdateSuperGroups.txt");
                string line = "";
                int superGroupId = -1;
                Hashtable updateGroupEntryHash = null;
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line == "")
                    {
                        continue;
                    }
                    if (line.Substring(0, 1) == "#")
                    {
                        if (superGroupId != -1)
                        {
                            updateSuperGroupHash.Add(superGroupId, updateGroupEntryHash);
                        }
                        superGroupId = Convert.ToInt32(line.Substring(1, line.Length - 1));
                        updateGroupEntryHash = new Hashtable();
                    }
                    if (line.IndexOf(":") > -1)
                    {
                        string[] fields = line.Split(':');
                        string[] entries = fields[1].Split(',');
                        updateGroupEntryHash.Add(Convert.ToInt32(fields[0]), entries);
                    }
                }
                if (superGroupId > -1)
                {
                    updateSuperGroupHash.Add(superGroupId, updateGroupEntryHash);
                }
                dataReader.Close();
            }
            ArrayList updateSuperGroupList = new ArrayList(updateSuperGroupHash.Keys);
            updateSuperGroupList.Sort();
            int[] updateSuperGroups = new int[updateSuperGroupList.Count];
            updateSuperGroupList.CopyTo(updateSuperGroups);

            // number of calculations in the order: 189798, 2812822, 331348, 287770, 102644, 372940, 124384
            int[] excludedChainGroups = { 7, 744, 762, 1379, 1620, 4116, 4949 };

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the interface comparisons between groups.");
            ChainGroupRepEntryComp interGroupRepEntryComp = new ChainGroupRepEntryComp();
    //        interGroupRepEntryComp.UpdateEntryComparisonInSuperGroups(updateSuperGroupHash);
            int bigChainGroupId = 4949;
            interGroupRepEntryComp.UpdateEntryComparisonInSuperGroup (updateSuperGroupHash, bigChainGroupId);*/
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdatePfamChainClusterFiles()
        {
            ProtCidSettings.dataType = "pfam";

            DbBuilderHelper.Initialize();
            ProtCidSettings.progressInfo.Reset();

 //           int[] updateSuperGroups = GetUpdateClusterChainGroups ();
            int[] updateSuperGroups = {3171, 9125, 13844, 16380, 28272, 29578};
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating super groups clustering.");
            ChainInterfaceCluster interfaceCluster = new ChainInterfaceCluster();
            interfaceCluster.UpdateSuperGroupClusters(updateSuperGroups);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the summary data.");
            ChainClusterStat superClusterStat = new ChainClusterStat();
            superClusterStat.UpdateSupergroupInterfaceClustersSumInfo(updateSuperGroups, "pfam", true);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating tar interface files.");
            InterfaceFilesReverser interfaceFileReverse = new InterfaceFilesReverser();
            string updateClusterReverseFileList = interfaceFileReverse.ReverseClusterInterfaceFiles(updateSuperGroups);

            string updateClusterReverseFileList1 = "ReverseInterfacesInCluster.txt";
            ChainClusterCompress clusterFileCompress = new ChainClusterCompress();
            clusterFileCompress.CompressGroupClusterInterfaceFiles(updateSuperGroups, updateClusterReverseFileList1);
            clusterFileCompress.UpdateCrystInterfaceFilesNotInClusters(false);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating sequence files.");
            SeqFastaGenerator seqFastaGen = new SeqFastaGenerator();
            seqFastaGen.UpdateSeqFastaFiles(updateSuperGroups);

            InterfaceImageGen imageGen = new InterfaceImageGen();
            imageGen.UpdateClusterInterfaceImages(updateSuperGroups);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetUpdateClusterChainGroups()
        {
            List<int> superGroupIdList = new List<int> ();
            string listFileName = "ChainGroupsNoClusters.txt";
            if (File.Exists(listFileName))
            {
                StreamReader dataReader = new StreamReader(listFileName);
                string line = "";
                int numOfCfs = 0;
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    numOfCfs = Convert.ToInt32(fields[2]);
                    if (numOfCfs > 1)
                    {
                        superGroupIdList.Add(Convert.ToInt32(fields[0]));
                    }
                }
                dataReader.Close();
            }
            return superGroupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroupHash"></param>
        /// <returns></returns>
        private string[] GetUpdateEntries(Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash)
        {
            List<string> updateEntryList = new List<string> ();
            foreach (int superGroupId in updateSuperGroupHash.Keys)
            {
                /*    string[] entries = (string[])updateSuperGroupHash[superGroupId];*/
                Dictionary<int, string[]> updateGroupEntryHash = updateSuperGroupHash[superGroupId];
                if (updateGroupEntryHash == null)
                {
                    continue;
                }
                foreach (int groupId in updateGroupEntryHash.Keys)
                {
                    string[] entries = (string[])updateGroupEntryHash[groupId];
                    foreach (string entry in entries)
                    {
                        if (!updateEntryList.Contains(entry))
                        {
                            updateEntryList.Add(entry);
                        }
                    }
                }
            }
            return updateEntryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, string[]> GetUpdatedGroups()
        {
            Dictionary<int, string[]> updateGroupHash = new Dictionary<int,string[]>  ();
            if (File.Exists("updateGroups.txt"))
            {
                StreamReader updateGroupReader = new StreamReader("updateGroups.txt");
                string line = "";
                while ((line = updateGroupReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(' ');
                    List<string> entryList = new List<string> (fields);
                    entryList.RemoveAt(0);
                    updateGroupHash.Add(Convert.ToInt32(fields[0]), entryList.ToArray ());
                }
                updateGroupReader.Close();
            }
            return updateGroupHash;
        }

        #region for a bug due to redundant crystal forms
       
        private int[] GetChainGroupsOfTheGroup(int groupId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID From PfamSuperGroups Where GroupSeqID = {0};", groupId);
            DataTable superGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] superGroupIds = new int[superGroupTable.Rows.Count];
            int count = 0;
            foreach (DataRow superGroupRow in superGroupTable.Rows)
            {
                superGroupIds[count] = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                count++;
            }
            return superGroupIds;
        }

        private Dictionary<int, string[]> GetUpdateChainGroupEntries()
        {
            Dictionary<int, List<string>> updateSupGrouEntryListHash = new Dictionary<int, List<string>>();
            int superGroupId = 0;
            if (File.Exists("UpdateSuperGroupEntries.txt"))
            {
                StreamReader dataReader = new StreamReader("UpdateSuperGroupEntries.txt");
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(',');
                    superGroupId = Convert.ToInt32(fields[0]);
                    List<string> entryList = new List<string> (fields);
                    entryList.RemoveAt(0);
                    updateSupGrouEntryListHash.Add(superGroupId, entryList);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "Select PdbId From PfamEntityPfamArch Where " +
                    " (PfamArchE5 <> SupPfamArchE5) OR (PfamArchE3 <> SupPfamArchE3) OR (PfamArch <> SupPfamArch);";
                DataTable splitEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                string pdbId = "";

                foreach (DataRow entryRow in splitEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    superGroupId = GetChainGroupIdForEntry(pdbId);
                    if (superGroupId > 0)
                    {
                        if (updateSupGrouEntryListHash.ContainsKey(superGroupId))
                        {
                            updateSupGrouEntryListHash[superGroupId].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            updateSupGrouEntryListHash.Add(superGroupId, entryList);
                        }
                    }
                }
                string dataLine = "";
                StreamWriter updateSuperGroupWriter = new StreamWriter("UpdateSuperGroupEntries.txt");
                foreach (int lsSuperGroupId in updateSupGrouEntryListHash.Keys)
                {
                    dataLine = lsSuperGroupId.ToString() + ",";
                    foreach (string entry in updateSupGrouEntryListHash[lsSuperGroupId])
                    {
                        dataLine += (entry + ",");
                    }
                    updateSuperGroupWriter.WriteLine(dataLine);
                }
            }
            Dictionary<int, string[]> updateSupGrouEntriesHash = new Dictionary<int,string[]> ();
            foreach (int lsSuperGroupId in updateSupGrouEntryListHash.Keys)
            {
                string[] updateEntries = updateSupGrouEntriesHash[lsSuperGroupId].ToArray();
                updateSupGrouEntriesHash.Add (lsSuperGroupId, updateEntries);
            }
            return updateSupGrouEntriesHash;
        }

        private int GetChainGroupIdForEntry(string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamHomoSeqInfo Where PdbID = '{0}';", 
                pdbId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query ( queryString);
            int groupId = -1;
            if (groupIdTable.Rows.Count == 0)
            {
                queryString = string.Format("Select GroupSeqID From PfamHomoRepEntryAlign Where PdbID2 = '{0}';",
                 pdbId);
                groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            if (groupIdTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32(groupIdTable.Rows[0]["GroupSeqID"].ToString ());
            }
            if (groupId < 0)
            {
                return -1;
            }
            queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where GroupSeqID = {0};",
                groupId);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (superGroupIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32 (superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            return -1;
        }
        #endregion

        public void DividChainPairsToFiles()
        {
            string chainPairFile = @"D:\DbProjectData\Fatcat\ChainAlignments\NonAlignedRepEntryPairsLeft";
            StreamReader chainPairReader = new StreamReader(chainPairFile + ".txt");
            int fileNum = 0;
            StreamWriter chainPairWriter = new StreamWriter(chainPairFile + fileNum.ToString () + ".txt");
            StreamWriter fileListWriter = new StreamWriter(@"D:\DbProjectData\Fatcat\ChainAlignments\filelist.txt");
            fileListWriter.WriteLine("NonAlignedRepEntryPairsLeft" + fileNum.ToString () + ".txt");
            string line = "";
            int numOfChainPairs = 0;
            int numOfChainPairsInFile = 5000;
            while ((line = chainPairReader.ReadLine()) != null)
            {
                numOfChainPairs++;
                chainPairWriter.WriteLine(line);
                if (numOfChainPairs == numOfChainPairsInFile)
                {
                    chainPairWriter.Close();
                    fileNum++;
                    numOfChainPairs = 0;
                    chainPairWriter = new StreamWriter(chainPairFile + fileNum.ToString () + ".txt");
                    fileListWriter.WriteLine("NonAlignedRepEntryPairsLeft" + fileNum.ToString() + ".txt");
                }
            }
            chainPairReader.Close();
            chainPairWriter.Close();
            fileListWriter.Close();
        }
        #endregion

        #region antibody groups
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        public static bool IsGroupAntibodyGroup(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamARch From PfamSuperGroups Where SuperGroupSeqID = {0};",
                superGroupId);
            DataTable chainRelTable = ProtCidSettings.protcidQuery.Query( queryString);

            string chainRelPfamArch = "";
            if (chainRelTable.Rows.Count > 0)
            {
                chainRelPfamArch = chainRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
                if (antibodyGroups.Contains(chainRelPfamArch))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
