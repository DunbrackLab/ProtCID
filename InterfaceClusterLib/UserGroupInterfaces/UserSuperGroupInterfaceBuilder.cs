using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using DbLib;
using InterfaceClusterLib.ChainInterfaces;


namespace InterfaceClusterLib.UserGroupInterfaces
{
    public class UserSuperGroupInterfaceBuilder
    {
        private DbQuery dbQuery = new DbQuery();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userGroupName"></param>
        /// <param name="userGroupSeqId"></param>
        /// <param name="userEntries"></param>
        public void FindInterfaceClustersInUserGroup(string userGroupName, int userGroupSeqId, string[] userEntries)
        {
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clutering interfaces in the user group");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(userGroupName + "  " + userGroupSeqId.ToString());

            Dictionary<int, string[]> updatedGroupHash = GetUpdatedGroups(userEntries);
            Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash = new Dictionary<int,Dictionary<int,string[]>> ();
            updateSuperGroupHash.Add(userGroupSeqId, updatedGroupHash);
            Dictionary<int, string> superGroupNameHash = new Dictionary<int,string> ();
            superGroupNameHash.Add(userGroupSeqId, userGroupName);

            List<int> updateSuperGroupList = new List<int> (updateSuperGroupHash.Keys);
            int[] updateSuperGroups = new int[updateSuperGroupList.Count];
            updateSuperGroupList.CopyTo(updateSuperGroups);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Super Groups.");
            ChainGroupsClassifier superGroupClassifier = new ChainGroupsClassifier();
            superGroupClassifier.AddChainRelGroupsBasedOnPfam(updateSuperGroupHash, superGroupNameHash);
           
            // clear the any existing file to make sure the file containing only those from
            // the comparison between groups.
            string nonAlignedPairFile = "NonAlignedEntryPairs.txt";
            if (File.Exists(nonAlignedPairFile))
            {
                // before delete, make a copy
                File.Copy(nonAlignedPairFile, Path.Combine(ProtCidSettings.dirSettings.fatcatPath, nonAlignedPairFile));
                File.Delete(nonAlignedPairFile);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the interface comparisons between groups.");
            ChainGroupRepEntryComp interGroupRepEntryComp = new ChainGroupRepEntryComp();
            interGroupRepEntryComp.UpdateEntryComparisonInSuperGroups(updateSuperGroupHash);
          
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating super groups clustering.");
            ChainInterfaceCluster interfaceCluster = new ChainInterfaceCluster();
            interfaceCluster.ClusterUserDefinedGroupInterfaces (updateSuperGroups);
 
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the summary data.");
            ChainClusterStat superClusterStat = new ChainClusterStat();
            superClusterStat.UpdateSupergroupInterfaceClustersSumInfo (updateSuperGroups, userEntries, "pfam", false);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating tar interface files.");
            InterfaceFilesReverser interfaceFileReverse = new InterfaceFilesReverser();
            string updateClusterReverseListFile = interfaceFileReverse.ReverseClusterInterfaceFiles(updateSuperGroupHash);
 
      //      string updateClusterReverseListFile = "ReverseInterfacesInCluster.txt";
            ChainClusterCompress clusterFileCompress = new ChainClusterCompress();
            clusterFileCompress.CompressGroupClusterInterfaceFiles(updateSuperGroups, updateClusterReverseListFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating sequence files.");
            SeqFastaGenerator seqFastaGen = new SeqFastaGenerator();
            seqFastaGen.UpdateSeqFastaFiles(updateSuperGroups);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        #region group ids for user entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userEntries"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetUpdatedGroups(string[] userEntries)
        {
            Dictionary<int, List<string>> groupEntryListHash = new Dictionary<int,List<string>> ();
            int groupId = 0;
            foreach (string userEntry in userEntries)
            {
                groupId = GetPfamArchGroupId(userEntry);
                if (groupId == -1)
                {
                    continue;
                }
                if (groupEntryListHash.ContainsKey(groupId))
                {
                    groupEntryListHash[groupId].Add(userEntry);
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(userEntry);
                    groupEntryListHash.Add(groupId, entryList);
                }
            }
            Dictionary<int, string[]> groupEntryHash = new Dictionary<int, string[]>();
            foreach (int keyGroupId in groupEntryListHash.Keys)
            {
                groupEntryHash.Add(keyGroupId, groupEntryListHash[keyGroupId].ToArray ());
            }
            return groupEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetPfamArchGroupId(string pdbId)
        {
            int groupId = -1;
            string queryString = string.Format("Select GroupSeqID From {0}HomoSeqInfo Where PdbID = '{1}';",
                ProtCidSettings.dataType, pdbId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            if (groupIdTable.Rows.Count == 0)
            {
                queryString = string.Format("Select GroupSeqID From {0}HomoRepEntryAlign Where PdbID2 = '{1}';",
                    ProtCidSettings.dataType, pdbId);
                groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            if (groupIdTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32(groupIdTable.Rows[0]["GroupSeqID"].ToString ());
            }
            return groupId;
        }
        #endregion
    }
}
