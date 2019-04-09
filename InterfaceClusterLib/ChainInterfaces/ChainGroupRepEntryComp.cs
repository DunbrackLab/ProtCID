using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.InterfaceComp;
using AuxFuncLib;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class ChainGroupRepEntryComp
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private CrystEntryInterfaceComp entryInterfaceComp = new CrystEntryInterfaceComp ();
        #endregion

        #region public interfaces to compare rep entries in supergroups
        /// <summary>
        /// 
        /// </summary>
        public void CompareInterGroupRepEntryInterfacesInChainGroups()
        {
            // [2, 30]: 11835, [31, 70]: 1971, [71, 100]: 584, [101, 200]: 522, [201, 500]: 1 (supergroupid = 744, #groups=322)
            int minGroupNum = 101;
            int maxGroupNum = 200;
            string queryString = string.Format("Select SuperGroupSeqID, count(*) AS GroupCount  From {0}SuperGroups Group By SuperGroupSeqID;", ProtCidSettings.dataType);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.Reset ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Rep entries inter-group in supergroups [" + 
                minGroupNum.ToString () + "," + maxGroupNum.ToString () + "]");

            ProtCidSettings.logWriter.WriteLine ("Compare Rep entries inter-group in supergroups [" +
                minGroupNum.ToString() + "," + maxGroupNum.ToString() + "]");

       //     int[] newSuperGroups = GetNewSuperGroups();
            int[] supergroupsInRange = GetSuperGroupsInRange(superGroupIdTable, minGroupNum, maxGroupNum);
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue
                ("Total number of supergroups to be compared: " + supergroupsInRange.Length.ToString ());

            int supergroupsCount = 0;
            Array.Sort(supergroupsInRange);
         //   Array.Reverse(supergroupsInRange);
         //   ProtCidSettings.progressInfo.progStrQueue.Enqueue("In reversed order.");
            foreach (int supergroupId in supergroupsInRange)
            {
                supergroupsCount++;
              
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupsCount.ToString() + ": " + supergroupId.ToString());
                ProtCidSettings.logWriter.WriteLine(supergroupsCount.ToString() + ": " + supergroupId.ToString());
                ProtCidSettings.logWriter.Flush();         
              
                CompareRepEntriesInSuperGroup(supergroupId);
            }
            ProtCidSettings.logWriter.WriteLine("Calculate similarity between entry groups in a chain group, Done!");
            ProtCidSettings.logWriter.Flush ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetNewSuperGroups()
        {
            StreamReader dataReader = new StreamReader("NewSuperGroups.txt");
            string line = "";
            List<int> newsuperGroupList = new List<int> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                newsuperGroupList.Add(Convert.ToInt32 (fields[0]));
            }
            dataReader.Close();
            return newsuperGroupList.ToArray ();
        }
        /// <summary>
        /// the list of supergroups with number of groups in the range
        /// </summary>
        /// <param name="superGroupIdTable"></param>
        /// <param name="minGroupNum"></param>
        /// <param name="maxGroupNum"></param>
        /// <returns></returns>
        private int[] GetSuperGroupsInRange (DataTable superGroupIdTable, int minGroupNum, int maxGroupNum)
        {
            List<int> superGroupsInRangeList = new List<int> ();
            int numOfGroups = 0;
            int supergroupId = 0;
            foreach (DataRow superGroupRow in superGroupIdTable.Rows)
            {
                supergroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());

                // skip the antibody group
       /*         if (ChainInterfaceBuilder.IsGroupAntibodyGroup(supergroupId))
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(supergroupId.ToString() + " is an antibody group. Skip it.");
                    ProtCidSettings.ProtCidSettings.logWriter.WriteLine(supergroupId.ToString() + " is an antibody group. Skip it.");
                    continue;
                }*/
              
                numOfGroups = Convert.ToInt32(superGroupRow["GroupCount"].ToString());

                if (numOfGroups >= minGroupNum && numOfGroups <= maxGroupNum)
                {
                    superGroupsInRangeList.Add(supergroupId);
                }
            }
            return superGroupsInRangeList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void CompareRepEntriesInSuperGroup(int superGroupId)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};", 
                ProtCidSettings.dataType, superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string[]> groupRepEntryHash = new Dictionary<int,string[]> ();
            int groupSeqId = -1;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupSeqId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                string[] repEntries = GetGroupRepEntries(groupSeqId);
                groupRepEntryHash.Add(groupSeqId, repEntries);
            }
            int numOfRepEntryPairs = GetTheTotalNumberOfRepEntryPairs(groupRepEntryHash);
            ProtCidSettings.progressInfo.totalOperationNum = numOfRepEntryPairs;
            ProtCidSettings.progressInfo.totalStepNum = numOfRepEntryPairs;

            List<int> groupIdList = new List<int> (groupRepEntryHash.Keys);
            groupIdList.Sort();
            for (int i = 0; i < groupIdList.Count - 1; i++)
            {
                string[] repEntries1 = (string[])groupRepEntryHash[groupIdList[i]];
                for (int j = i + 1; j < groupIdList.Count; j++)
                {
                    string[] repEntries2 = (string[])groupRepEntryHash[groupIdList[j]];
                    CompareRepEntriesInterGroups(repEntries1, repEntries2, false, false);
                } 
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void CompareAllRepEntriesInSuperGroup(int superGroupId)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};",
                ProtCidSettings.dataType, superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
          /*  int[] groupSeqIds = {942, 1193, 1738, 3896, 5096, 6854, 6855, 6857, 6858, 6862};*/
            int groupSeqId = -1;
            List<string> repEntryList = new List<string> ();
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
      //      foreach (int groupSeqId in groupSeqIds)
            {
                //    groupSeqId = Convert.ToInt32(groupIdRow["groupSeqId"].ToString());
                string[] repEntries = GetGroupRepEntries(groupSeqId);
                repEntryList.AddRange(repEntries);
            }
            int numOfRepEntryPairs = (repEntryList.Count * (repEntryList.Count - 1)) / 2;
            ProtCidSettings.progressInfo.totalOperationNum = numOfRepEntryPairs;
            ProtCidSettings.progressInfo.totalStepNum = numOfRepEntryPairs;
            string pdbId = "";
            entryInterfaceComp.nonAlignPairWriter = new StreamWriter("EntryPairsCompLog.txt", true);
            for (int i = 0; i < repEntryList.Count; i++)
            {
                pdbId = (string)repEntryList[i];
                List<string> repEntriesToBeCompList = repEntryList.GetRange(i + 1, repEntryList.Count - i - 1);
                string[] repEntriesToBeComp = new string[repEntriesToBeCompList.Count];
                repEntriesToBeCompList.CopyTo(repEntriesToBeComp);
                entryInterfaceComp.CompareCrystInterfaces(pdbId, repEntriesToBeComp);
            }
            entryInterfaceComp.nonAlignPairWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntries1"></param>
        /// <param name="groupRepEntries2"></param>
        /// <param name="entryInterfaceHash"></param>
        public void CompareRepEntriesInterGroups(string[] groupRepEntries1, string[] groupRepEntries2, bool deleteOld, bool compareNonAlignedPair)
        {
            string errorMsg = "";
            foreach (string repEntry1 in groupRepEntries1)
            {
                foreach (string repEntry2 in groupRepEntries2)
                {
                    ProtCidSettings.progressInfo.currentFileName = repEntry1 + "_" + repEntry2;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    if (repEntry1 == repEntry2)
                    {
                        ProtCidSettings.logWriter.WriteLine(repEntry1 + " repeated in groups.");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    try
                    {
                        errorMsg = entryInterfaceComp.CompareCrystInterfaces(repEntry1, repEntry2, deleteOld, compareNonAlignedPair);
                        if (errorMsg != "")
                        {
                            ProtCidSettings.logWriter.WriteLine("Compare " + repEntry1 + " and " + repEntry2 + " errors: " + errorMsg);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine("Compare " + repEntry1 + " and " + repEntry2 + " Errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntries1"></param>
        /// <param name="groupRepEntries2"></param>
        /// <param name="entryInterfaceHash"></param>
        public void CompareRepEntriesInterGroups(string[] groupRepEntries1, string[] groupRepEntries2, bool deleteOld, bool compareNonAlignedPair, string[] parsedNonAlignedPairs)
        {
            string errorMsg = "";
            string entryPair = "";
            foreach (string repEntry1 in groupRepEntries1)
            {
                foreach (string repEntry2 in groupRepEntries2)
                {
                    ProtCidSettings.progressInfo.currentFileName = repEntry1 + "_" + repEntry2;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    if (repEntry1 == repEntry2)
                    {
                        ProtCidSettings.logWriter.WriteLine(repEntry1 + " repeated in groups.");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    entryPair = repEntry1 + " " + repEntry2;
                    if (Array.BinarySearch (parsedNonAlignedPairs, entryPair) > -1)
                    {
                        continue;
                    }
                    try
                    {
                        errorMsg = entryInterfaceComp.CompareCrystInterfaces(repEntry1, repEntry2, deleteOld, compareNonAlignedPair);
                        if (errorMsg != "")
                        {
                            ProtCidSettings.logWriter.WriteLine("Compare " + repEntry1 + " and " + repEntry2 + " errors: " + errorMsg);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine("Compare " + repEntry1 + " and " + repEntry2 + " Errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetParsedNonAlignedEntryPairs ()
        {
            string nonAlignEntryPairFile = "NonAlignedEntryPairs1.txt";
            StreamReader dataReader = new StreamReader(nonAlignEntryPairFile);
            List<string> entryPairList = new List<string> ();
            string line = "";
            string entryPair = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                entryPair = fields[0].Substring(0, 4) + " " + fields[1].Substring(0, 4);
                if (! entryPairList.Contains (entryPair))
                {
                    entryPairList.Add(entryPair);
                }
            }
            entryPairList.Sort();

            return entryPairList.ToArray ();
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="UpdateGroupEntryHash"></param>
        public void UpdateEntryComparisonInSuperGroups(Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Entry Comparison In SuperGroups.";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Entry Comparison In SuperGroups.");

            int numOfSuperGroups = 0;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Total number of supergroups to be updated: " +
                updateSuperGroupHash.Count.ToString());
            ProtCidSettings.logWriter.WriteLine("Total number of supergroups to be updated: " +
                updateSuperGroupHash.Count.ToString());
            List<int> superGroupIdList = new List<int> (updateSuperGroupHash.Keys);
            superGroupIdList.Sort();

            // 189798, 2812822, 331348, 287770, 102644, 372940, 124384
            int[] excludedChainGroups = {7, 744, 762, 1379, 1620, 4116, 4949};           

            foreach (int superGroupId in superGroupIdList)
            { 
               numOfSuperGroups++;

                if (Array.IndexOf (excludedChainGroups, superGroupId) > -1)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("due to millions of computations, skip " + superGroupId.ToString());
                    ProtCidSettings.logWriter.WriteLine("due to millions of computations, skip " + superGroupId.ToString());
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }              
               ProtCidSettings.progressInfo.progStrQueue.Enqueue(numOfSuperGroups.ToString() + ": " + superGroupId.ToString());
               ProtCidSettings.logWriter.WriteLine(numOfSuperGroups.ToString() + ": " + superGroupId.ToString());
               ProtCidSettings.logWriter.Flush();
               Dictionary<int, string[]> updateGroupHash = updateSuperGroupHash[superGroupId];
               if (updateGroupHash == null || updateGroupHash.Count == 0)
               {
                   continue;
               }
               try
               {
                   CompareRepEntriesInSuperGroup(superGroupId, updateGroupHash);
               }
               catch (Exception ex)
               {
                   ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " errors: " + ex.Message);
                   ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " errors: " + ex.Message);
                   continue;
               }
           }
//            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare the missing entry pairs.");
//            CompareMissingEntryPairs();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.logWriter.WriteLine("Calculate similarity between entry groups in a chain group, Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="UpdateGroupEntryHash"></param>
        public void UpdateEntryComparisonInSuperGroups(Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash, int[] updateSuperGroups)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Entry Comparison In SuperGroups.";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Entry Comparison In SuperGroups.");         

            // 189798, 2812822, 331348, 287770, 102644, 372940, 124384
//            int[] excludedChainGroups = { 7, 744, 762, 1379, 1620, 4116, 4949 };

            foreach (int superGroupId in updateSuperGroups)
            {
                UpdateEntryComparisonInSuperGroup(updateSuperGroupHash, superGroupId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.logWriter.WriteLine("Calculate similarity between entry groups in big chain groups, Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroupHash"></param>
        /// <param name="superGroupId"></param>
        public void UpdateEntryComparisonInSuperGroup (Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash, int superGroupId)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString());
            ProtCidSettings.logWriter.WriteLine(superGroupId.ToString());
            ProtCidSettings.logWriter.Flush();
            Dictionary<int, string[]> updateGroupHash = updateSuperGroupHash[superGroupId];
            if (updateGroupHash == null || updateGroupHash.Count == 0)
            {
                return;
            }
            try
            {
                CompareRepEntriesInSuperGroup(superGroupId, updateGroupHash);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId + " is done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompareMissingEntryPairs()
        {
            //     string[] entryPairsToBeCompared = GetEntryPairListFromFiles ();
            string nonAlignedPairFile = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\NonAlignedEntryPairs.txt");

            /* Copy file to 10.40.16.33 linux machine, run fatcat, copy the alignment file back
           *  then parse the file, insert alignments into fatcatalignments in alignments.fdb database
           **/

   /*         ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run FATCAT on Linux machine. ");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("May take several hours depending on the number of entry pairs to be aligned.");
            try
            {
                //     File.Copy("NonAlignedEntryPairs.txt", nonAlignedPairFile, true);
                Alignments.GroupEntryAlignments entryAlignment = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
                entryAlignment.GetRepChainPairsToBeAligned();
                entryAlignment.UpdateFatcatAlignmentsFromFile(nonAlignedPairFile);  
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine("Run fatcat errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run Fatcat errors: " + ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Do nothing. Returned.");
                return;
            }
            */
            // the list of entry pairs which may have alignments
            //      string[] entryPairsToBeCompared = GetEntryPairListFromFile(nonAlignedPairFile);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get entry pairs for interface computing.");
     //       string[] entryPairsToBeCompared = GetEntryPairListFromFiles();
            string[] entryPairsToBeCompared = GetEntryPairListFromFile(nonAlignedPairFile);
            // compute entry pairs
            entryInterfaceComp.CompareCrystInterfaces(entryPairsToBeCompared);
            //       entryInterfaceComp.CompareCrystInterfacesAfterWeightFixed(entryPairsToBeCompared);
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompareMissingEntryPairs(string[] entryPairsToBeCompared)
        {
            // compute entry pairs
            entryInterfaceComp.CompareCrystInterfaces(entryPairsToBeCompared);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void CompareRepEntriesInSuperGroup(int superGroupId, Dictionary<int, string[]> updateGroupHash)
        {
            // since the comparisons should be deleted in the group building for those updating entries
            // so don't have to delete the Q scores. Modified on August 24, 2010
            bool deleteOld = false;
            bool compareNonAlignedPair = false;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};",
                ProtCidSettings.dataType, superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string[]> groupRepEntryHash = new Dictionary<int,string[]> ();
            int groupSeqId = -1;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupSeqId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                string[] repEntries = GetGroupRepEntries(groupSeqId);
                groupRepEntryHash.Add(groupSeqId, repEntries);
            }
            int numOfRepEntryPairs = GetTheTotalNumberOfRepEntryPairs(groupRepEntryHash, updateGroupHash);
            ProtCidSettings.progressInfo.totalOperationNum = numOfRepEntryPairs;
            ProtCidSettings.progressInfo.totalStepNum = numOfRepEntryPairs;
            ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + ": " + numOfRepEntryPairs.ToString ());

            //       Hashtable entryInterafceHash = new Hashtable ();
            List<int> groupIdList = new List<int> (groupRepEntryHash.Keys);
            groupIdList.Sort();            
            foreach (int updateGroup in updateGroupHash.Keys)
            {
                string[] updateEntries = (string[])updateGroupHash[updateGroup];
                string[] repEntries1 = (string[])groupRepEntryHash[updateGroup];
                string[] updateRepEntries = GetUpdateRepEntries(updateEntries, repEntries1);
                if (updateRepEntries.Length == 0)
                {
                    continue;
                }

                foreach (int groupId in groupRepEntryHash.Keys)
                {
                    if (updateGroup == groupId)
                    {
                        continue;
                    }
                    string[] repEntries2 = (string[])groupRepEntryHash[groupId];

                    try
                    {
                        /*             if (superGroupId == 744)
                                     {
                                         CompareRepEntriesInterGroups(updateRepEntries, repEntries2, deleteOld, compareNonAlignedPair, parsedNonAlignedEntryPairs);
                                     }
                                     else
                                     {*/
                        CompareRepEntriesInterGroups(updateRepEntries, repEntries2, deleteOld, compareNonAlignedPair);
                        //        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() +
                            ": Compare " + updateGroup.ToString() + " and " + groupId.ToString() +
                            " errors: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() +
                            ": Compare " + updateGroup.ToString() + " and " + groupId.ToString() +
                            " errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                } 
            }
            ProtCidSettings.logWriter.WriteLine(superGroupId + " is done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntryHash"></param>
        /// <returns></returns>
        private int GetTheTotalNumberOfRepEntryPairs(int superGroupId, Dictionary<int, string[]> updateGroupHash)
        {
            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};",
                ProtCidSettings.dataType, superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string[]> groupRepEntryHash = new Dictionary<int,string[]> ();
            int groupSeqId = -1;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupSeqId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                string[] repEntries = GetGroupRepEntries(groupSeqId);
                groupRepEntryHash.Add(groupSeqId, repEntries);
            }
            int numOfRepEntryPairs = GetTheTotalNumberOfRepEntryPairs(groupRepEntryHash, updateGroupHash);

            return numOfRepEntryPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntryHash"></param>
        /// <returns></returns>
        private int GetTheTotalNumberOfRepEntryPairs(Dictionary<int, string[]> groupRepEntryHash, Dictionary<int, string[]> updateGroupHash)
        {
            int numOfRepEntryPairs = 0;
            foreach (int updateGroupId in updateGroupHash.Keys)
            {
                string[] updateRepEntries = (string[])groupRepEntryHash[updateGroupId];
                foreach (int groupId in groupRepEntryHash.Keys)
                {
                    if (updateGroupId == groupId)
                    {
                        continue;
                    }
                    string[] repEntries = (string[])groupRepEntryHash[groupId];
                    numOfRepEntryPairs += updateRepEntries.Length * repEntries.Length;
                }
            }
            return numOfRepEntryPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <param name="repEntries"></param>
        /// <returns></returns>
        private string[] GetUpdateRepEntries(string[] updateEntries, string[] repEntries)
        {
            List<string> updateRepEntryList = new List<string> ();
            foreach (string updateEntry in updateEntries)
            {
                if (Array.IndexOf(repEntries, updateEntry) > -1)
                {
                    updateRepEntryList.Add(updateEntry);
                }
            }
            return updateRepEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairFile"></param>
        /// <returns></returns>
        private string[] GetEntryPairListFromFile(string nonAlignedChainPairFile)
        {
            string line = "";
            List<string> entryPairList = new List<string> ();
            if (File.Exists("EntryPairs.txt"))
            {
                StreamReader dataReader = new StreamReader("EntryPairs.txt");
                while ((line = dataReader.ReadLine()) != null)
                {
                    entryPairList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter entryPairWriter = new StreamWriter("EntryPairs.txt");
                StreamReader dataReader = new StreamReader(nonAlignedChainPairFile);
                string entry1 = "";
                string entry2 = "";
                string entryPair = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length != 2)
                    {
                        fields =ParseHelper.SplitPlus (line, ' ');
                    }
                    entry1 = fields[0].Substring(0, 4);
                    entry2 = fields[1].Substring(0, 4);
                    if (entry1 == entry2)
                    {
                        continue;
                    }
                    entryPair = entry1 + "   " + entry2;
                    if (string.Compare(entry1, entry2) > 0)
                    {
                        entryPair = entry2 + "   " + entry1;
                    }
                    if (!entryPairList.Contains(entryPair))
                    {
                        entryPairList.Add(entryPair);
                        entryPairWriter.WriteLine(entryPair);
                    }
                }
                dataReader.Close();
                entryPairWriter.Close();
            }
            return entryPairList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairFile"></param>
        /// <returns></returns>
        private string[] GetEntryPairListFromFiles()
        {
            string line = "";
            string entryFilePath = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\chaincluster");
            string[] entryPairFiles = Directory.GetFiles(entryFilePath, "NonAlignedEntryPairs*");
            StreamWriter entryPairWriter = null;
            string entry1 = "";
            string entry2 = "";
            string entryPair = "";
            List<string> entryPairList = new List<string> ();
            List<string> leftEntryPairList = new List<string> ();
            if (File.Exists("EntryPairs.txt"))
            {
                StreamReader dataReader = new StreamReader("EntryPairs.txt");
                while ((line = dataReader.ReadLine()) != null)
                {
                    entryPairList.Add(line);
                }
                dataReader.Close();
                entryPairWriter = new StreamWriter("EntryPairs.txt", true);
            }
            else
            {
                entryPairWriter = new StreamWriter("EntryPairs.txt");
            }
            entryPairList.Sort();

            int count = 1;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Total number of files: " + entryPairFiles.Length.ToString ());
            foreach (string entryPairFile in entryPairFiles)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(count.ToString () + " : " + entryPairFile);

                StreamReader dataReader = new StreamReader(entryPairFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    entry1 = fields[0].Substring(0, 4);
                    entry2 = fields[1].Substring(0, 4);
                    entryPair = entry1 + "   " + entry2;
                    if (string.Compare(entry1, entry2) > 0)
                    {
                        entryPair = entry2 + "   " + entry1;
                    }
                    if (entryPairList.BinarySearch(entryPair) < 0)
                    {
                        if (!leftEntryPairList.Contains(entryPair))
                        {
                            //   entryPairList.Add(entryPair);
                            leftEntryPairList.Add(entryPair);
                            entryPairWriter.WriteLine(entryPair);
                        }
                    }
                }
                dataReader.Close();
                count++;
            }
            entryPairWriter.Close();

            string[] entryPairs = new string[leftEntryPairList.Count + entryPairList.Count];
            entryPairList.CopyTo(entryPairs);
            leftEntryPairList.CopyTo(entryPairs, entryPairList.Count);   
         /*   leftEntryPairList.Sort();
            string[] entryPairs = new string[leftEntryPairList.Count];
            leftEntryPairList.CopyTo(entryPairs);*/
            return entryPairs;
        }
        #endregion

        #region rep entries of a supergroup
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntryHash"></param>
        /// <returns></returns>
        private int GetTheTotalNumberOfRepEntryPairs(Dictionary<int, string[]> groupRepEntryHash)
        {
            int numOfRepEntryPairs = 0;
            List<int> groupIdList = new List<int> (groupRepEntryHash.Keys);
            for (int i = 0; i < groupIdList.Count - 1; i++)
            {
                string[] repEntries1 = (string[])groupRepEntryHash[groupIdList[i]];
                for (int j = i + 1; j < groupIdList.Count; j++)
                {
                    string[] repEntries2 = (string[])groupRepEntryHash[groupIdList[j]];
                    numOfRepEntryPairs += repEntries1.Length * repEntries2.Length;
                }
            }
            return numOfRepEntryPairs;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <returns></returns>
        private string[] GetGroupRepEntries(int groupSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From {0}HomoSeqInfo " +
                " Where GroupSeqID = {1};", ProtCidSettings.dataType, groupSeqId);
            DataTable groupRepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] groupRepEntries = new string[groupRepEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow repEntryRow in groupRepEntryTable.Rows)
            {
                groupRepEntries[count] = repEntryRow["PdbID"].ToString();
                count++;
            }
            return groupRepEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroupHash"></param>
        public void ProcessMissingAlignRepEntryPairs(Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash)
        {
            StreamWriter dataWriter = new StreamWriter("NonAlignEntryPairs");
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Entry Alignments In SuperGroups.";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Entry Alignments In SuperGroups.");

            List<int> superGroupIdList = new List<int> (updateSuperGroupHash.Keys);
            superGroupIdList.Sort();

            foreach (int superGroupId in superGroupIdList)
            {
       /*         if (ChainInterfaceBuilder.IsGroupAntibodyGroup(superGroupId))
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " is an antibody group. Skip it.");
                    ProtCidSettings.ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " is an antibody group. Skip it.");
                    continue;
                }*/
                ProtCidSettings.logWriter.WriteLine(superGroupId.ToString());
                ProtCidSettings.logWriter.Flush();

                Dictionary<int, string[]> updateGroupHash =  updateSuperGroupHash[superGroupId];
                if (updateGroupHash == null || updateGroupHash.Count == 0)
                {
                    continue;
                }
                try
                {
                    GetMissingAlignRepEntryPairs(superGroupId, updateGroupHash, dataWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + " errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + " errors: " + ex.Message);
                    continue;
                }
            }
            dataWriter.Close();
            CompareMissingEntryPairs();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntryHash"></param>
        /// <returns></returns>
        private void GetMissingAlignRepEntryPairs(int superGroupId, Dictionary<int, string[]> updateGroupHash, StreamWriter dataWriter)
        {
            
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};",
                ProtCidSettings.dataType, superGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string[]> groupRepEntryHash = new Dictionary<int,string[]> ();
            int groupSeqId = -1;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupSeqId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString());
                string[] repEntries = GetGroupRepEntries(groupSeqId);
                groupRepEntryHash.Add(groupSeqId, repEntries);
            }
            List<int> groupIdList = new List<int> (groupRepEntryHash.Keys);
            groupIdList.Sort();
            foreach (int updateGroup in updateGroupHash.Keys)
            {
                string[] updateEntries = (string[])updateGroupHash[updateGroup];
                string[] repEntries1 = (string[])groupRepEntryHash[updateGroup];
                string[] updateRepEntries = GetUpdateRepEntries(updateEntries, repEntries1);
                if (updateRepEntries.Length == 0)
                {
                    continue;
                }

                foreach (int groupId in groupRepEntryHash.Keys)
                {
                    if (updateGroup == groupId)
                    {
                        continue;
                    }
                    string[] repEntries2 = (string[])groupRepEntryHash[groupId];
                    try
                    {
                        foreach (string repEntry1 in updateRepEntries)
                        {
                            foreach (string repEntry2 in repEntries2)
                            {
                                dataWriter.WriteLine(repEntry1 + "   " + repEntry2);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString () + 
                            ": Compare " + updateGroup.ToString () + " and " + groupId.ToString () + 
                            " errors: " + ex.Message);
                    }
                } 
            }
            dataWriter.Flush();
        }
        #endregion

        #region representative entries with missing alignments between entry groups within a chain group
        InterfaceClusterLib.Alignments.EntryAlignment entryAlignments = new Alignments.EntryAlignment();
        /// <summary>
        /// 
        /// </summary>
        public void PrintMissingAlignInterGroupRepEntries ()
        {
            // [2, 30], [31, 70], [71, 100], [101, 500], [501, 1000], [1001, 5000]
            int minGroupNum = 31;
            int maxGroupNum = 70;
            string queryString = string.Format("Select SuperGroupSeqID, count(*) AS GroupCount  From {0}SuperGroups Group By SuperGroupSeqID;", ProtCidSettings.dataType);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Rep entries inter-group in supergroups [" +
                minGroupNum.ToString() + "," + maxGroupNum.ToString() + "]");

            ProtCidSettings.logWriter.WriteLine("Compare Rep entries inter-group in supergroups [" +
                minGroupNum.ToString() + "," + maxGroupNum.ToString() + "]");

            //     int[] newSuperGroups = GetNewSuperGroups();
            int[] supergroupsInRange = GetSuperGroupsInRange(superGroupIdTable, minGroupNum, maxGroupNum);

            int supergroupsCount = 0;
            Array.Sort(supergroupsInRange);
            //   Array.Reverse(supergroupsInRange);
            //   ProtCidSettings.progressInfo.progStrQueue.Enqueue("In reversed order.");
            List<int> superGroupList = new List<int> ();
            foreach (int supergroupId in supergroupsInRange)
            {
                supergroupsCount++;

                superGroupList.Add(supergroupId);
            }
            PrintAlignmentMissingEntryPairs(superGroupList.ToArray ());
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void PrintAlignmentMissingEntryPairs (int[] chainGroupIds)
        {
            string entryAlignPairFile = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "MissingInterGroupAlignEntryPairs.txt");
            StreamWriter entryPairWriter = new StreamWriter(entryAlignPairFile, true);
            entryPairWriter.WriteLine("#");
            bool difEntryCompExist = false;
            foreach (int chainGroupId in chainGroupIds)
            {
                Dictionary<int, string[]> groupRepEntryHash = GetChainGroupRepEntries(chainGroupId);

                List<int> groupIdList = new List<int> (groupRepEntryHash.Keys);
                groupIdList.Sort();
                int[] groupIds = groupIdList.ToArray();
                for (int i = 0; i < groupIds.Length; i++)
                {
                    string[] repEntriesI = groupRepEntryHash[groupIds[i]];
                    for (int j = i + 1; j < groupIds.Length; j++)
                    {
                        string[] repEntriesJ = groupRepEntryHash[groupIds[j]];

                        foreach (string pdbId1 in repEntriesI)
                        {
                            foreach (string pdbId2 in repEntriesJ)
                            {
                                difEntryCompExist = entryInterfaceComp.IsDifEntryCompExist (pdbId1, pdbId2);
                                if (difEntryCompExist)
                                {
                                    ProtCidSettings.logWriter.WriteLine(chainGroupId.ToString () + " " + pdbId1 + " " + pdbId2 + " comp exist.");
                                    continue;
                                }

                                DataTable alignInfoTable = entryAlignments.RetrieveEntryAlignments(pdbId1, pdbId2);

                                if (alignInfoTable.Rows.Count > 0)
                                {
                                    ProtCidSettings.logWriter.WriteLine(chainGroupId.ToString() + " " + pdbId1 + " " + pdbId2 + " alignments exist.");
                                    continue;
                                }

                                entryPairWriter.WriteLine(pdbId1 + "   " + pdbId2);
                            }
                        }                      
                    }
                    entryPairWriter.Flush();
                }
                ProtCidSettings.logWriter.WriteLine(chainGroupId.ToString ());
                ProtCidSettings.logWriter.Flush();
            }
            entryPairWriter.Close();
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetChainGroupRepEntries(int chainGroupId)
        {
            string queryString = string.Format("Select GroupSeqId From {0}SuperGroups Where SuperGroupSeqID = {1};",
                ProtCidSettings.dataType, chainGroupId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string[]> groupRepEntryHash = new Dictionary<int, string[]>();
            int groupId = 0;
            foreach (DataRow groupIdRow in groupSeqIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString ());
                string[] repEntries = GetGroupRepEntries(groupId);
                groupRepEntryHash.Add(groupId, repEntries);
            }

            return groupRepEntryHash;
        }
        #endregion

        #region separate calculating Q scores 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="UpdateGroupEntryHash"></param>
        public void GetTotalInterfaceComparisonChainGroups (Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash)
        {
            string chainGroupCompuationFile = "UpdateChainGroupComputationsInfo.txt";
            StreamWriter dataWriter = new StreamWriter(chainGroupCompuationFile);
            List<int> superGroupIdList = new List<int>(updateSuperGroupHash.Keys);
            superGroupIdList.Sort();
            foreach (int superGroupId in superGroupIdList)
            {                
                Dictionary<int, string[]> updateGroupHash = updateSuperGroupHash[superGroupId];
                if (updateGroupHash == null || updateGroupHash.Count == 0)
                {
                    continue;
                }
                int numOfRepEntryPairs = GetTheTotalNumberOfRepEntryPairs(superGroupId, updateGroupHash);
                dataWriter.WriteLine(superGroupId + " " + numOfRepEntryPairs);
            }
            dataWriter.Close();
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void CompareSpecificEntryPairs(string nonAlignedPairFile)
        {
       //     string nonAlignedPairFile = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\CapriAlignPairs.txt");
            //      File.Copy("NonAlignedEntryPairs.txt", nonAlignedPairFile, true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run FATCAT on Linux machine. ");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("May take several hours depending on the number of entry pairs to be aligned.");
            try
            {
                //     File.Copy("NonAlignedEntryPairs.txt", nonAlignedPairFile, true);
                Alignments.GroupEntryAlignments entryAlignment = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
                entryAlignment.UpdateFatcatAlignmentsFromFile(nonAlignedPairFile);
                //    entryAlignment.GetRepChainPairsToBeAligned ();
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine("Run fatcat errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run Fatcat errors: " + ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Do nothing. Returned.");
                return;
            }
            
            // the list of entry pairs which may have alignments
            string[] entryPairsToBeCompared = GetEntryPairListFromFile(nonAlignedPairFile);
            //    string[] entryPairsToBeCompared = GetEntryPairListFromFiles ();
            // compute entry pairs
            entryInterfaceComp.CompareCrystInterfaces(entryPairsToBeCompared);
        }
        /// <summary>
        /// 
        /// </summary>
        public void CompareRepEntriesFromLogFiles()
        {
            string[] logFiles = {"RepEntriesInterGroupCompLog0.txt"};
                        /*        @"C:\ProtBuDProject\xtal\XtalInterfaceProject\Debug250less400\RepEntriesInterGroupCompLog1.txt", 
                                @"C:\ProtBuDProject\xtal\XtalInterfaceProject\Debug250less400\RepEntriesInterGroupCompLog3.txt", 
                                @"C:\ProtBuDProject\xtal\XtalInterfaceProject\Debug250less400\RepEntriesInterGroupCompLog4.txt"};*/
       
            string pdbId1 = "";
            string pdbId2 = "";
            string entryPairString = "";
            StreamReader dataReader = null;
            string line = "";
            List<string> entryPairList = new List<string> ();
            foreach (string logFile in logFiles)
            {
                dataReader = new StreamReader(logFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line.IndexOf("not aligned pair") > -1)
                    {
                        if (line.IndexOf("Compare") > -1)
                        {
                            int entryPairIndex = line.IndexOf("errors:") + "errors: ".Length;
                            line = line.Substring(entryPairIndex, line.Length - entryPairIndex);
                        }
                        entryPairString = line.Substring(0, 11);
                        if (!entryPairList.Contains(entryPairString))
                        {
                            entryPairList.Add(entryPairString);
                        }
                    }
                }
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = entryPairList.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryPairList.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "Rep Entry Comp";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare interfaces between rep entries in supergroups from log files.");

            bool deleteOld = true;
            bool compareNonAlignedPair = true;
            string errorMsg = "";
            foreach (string entryPair in entryPairList)
            {
                ProtCidSettings.progressInfo.currentFileName = entryPair;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                pdbId1 = entryPair.Substring(0, 4);
                pdbId2 = entryPair.Substring(7, 4);

                errorMsg = entryInterfaceComp.CompareCrystInterfaces(pdbId1, pdbId2, deleteOld, compareNonAlignedPair);

                if (errorMsg != "")
                {
                    ProtCidSettings.logWriter.WriteLine(errorMsg);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
      //      ProtCidSettings.progressInfo.threadFinished = true;
            ProtCidSettings.logWriter.Close();
        }

        public void CompareTwoEntries(string pdbId1, string pdbId2)
        {
            string errorMsg = entryInterfaceComp.CompareTwoEntriesCrystInterfaces (pdbId1, pdbId2, true, false);

        }
        #endregion
    }
}
