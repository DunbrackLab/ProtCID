using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtCidSettingsLib;
using System.Data;

namespace InterfaceClusterLib.AuxFuncs
{
    public class DownloadableFileName
    {
        private const int maxFileNameLength = 164; 
        private const int maxClusterLength = 4;  // maximum cluster id  4-digit

        #region chain group file name
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <returns></returns>
        public static string GetChainGroupTarGzFileName(int chainGroupId)
        {
            string chainGroupFileName = "";
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", chainGroupId);
            DataTable groupNameTable = ProtCidSettings.protcidQuery.Query(queryString);
            // this is the maximum length of chain pfam architecture
            // -2 is for "_" + groupId + "_" + clusterId
            int maxChainArchLength = maxFileNameLength - chainGroupId.ToString().Length - maxClusterLength - 2;
            string newChainPfamArch = "";
            if (groupNameTable.Rows.Count > 0)
            {
                string chainPfamArch = groupNameTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();               
                string[] fields = chainPfamArch.Split(';');
                if (fields.Length == 1)
                {
                    newChainPfamArch = ShortenChainGroupName(chainPfamArch);
                    if (newChainPfamArch.Length < maxChainArchLength)
                    {
                        chainGroupFileName = newChainPfamArch;
                    }
                    else
                    {
                        chainGroupFileName = newChainPfamArch.Substring(0, maxChainArchLength);
                    }
                }
                else if (fields.Length == 2)
                {
                    string newPfamArch1 = ShortenChainGroupName(fields[0]);
                    string newPfamArch2 = ShortenChainGroupName (fields[1]);
                    newChainPfamArch = newPfamArch1 + newPfamArch2;
                    if (newChainPfamArch.Length <= maxChainArchLength)
                    {
                        chainGroupFileName = newChainPfamArch;
                    }
                    else
                    {
                        int halfLength = (int)((double)maxChainArchLength / 2.0);
                        if (newPfamArch1.Length <= halfLength)
                        {
                            newPfamArch2 = GetSubChainPfamArch (newPfamArch2, maxChainArchLength - newPfamArch1.Length);
                            chainGroupFileName = newPfamArch1 + newPfamArch2;
                        }
                        else if (newPfamArch2.Length <= halfLength)
                        {
                            newPfamArch1 = GetSubChainPfamArch(newPfamArch1, maxChainArchLength - newPfamArch2.Length);
                            chainGroupFileName = newPfamArch1 + newPfamArch2;
                        }
                        else
                        {
                            newPfamArch1 = GetSubChainPfamArch(newPfamArch1, halfLength);
                            newPfamArch2 = GetSubChainPfamArch(newPfamArch2, maxChainArchLength - newPfamArch1.Length);
                            chainGroupFileName = newPfamArch1 + newPfamArch2;
                        }
                    }
                }
            }
            return chainGroupFileName + "_" + chainGroupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArch"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetSubChainPfamArch (string pfamArch, int length)
        {
            string[] pfamFields = pfamArch.Split('(');
            string shortPfamArch = "";
            string pfam = "";
            for (int i = 0; i < pfamFields.Length; i++)
            {
                if (pfamFields[i] == "")
                {
                    continue;
                }
                pfam = "(" + pfamFields[i];
                if (shortPfamArch.Length + pfam.Length <= length)
                {
                    shortPfamArch += pfam;
                }
            }
            shortPfamArch += "...";
            return shortPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <returns></returns>
        public static string ShortenChainGroupName(string chainPfamArch)
        {
            string[] pfamFields = chainPfamArch.Split(')');
            string newChainPfamArch = "";
            List<string> pfamList = new List<string>();
            string pfam = "";
            List<int> countList = new List<int>();
            for (int i = 0; i < pfamFields.Length; i++)
            {
                if (pfamFields[i] == "")
                {
                    continue;
                }
                pfam = pfamFields[i].TrimStart('_') + ")";
                if (pfamList.Count == 0)
                {
                    pfamList.Add(pfam);
                    countList.Add(1);
                }
                else
                {
                    if (pfamList[pfamList.Count - 1] != pfam)
                    {
                        pfamList.Add(pfam);
                        countList.Add(1);
                    }
                    else
                    {
                        countList[pfamList.Count - 1]++;
                    }
                }
            }
            for (int i = 0; i < pfamList.Count; i++)
            {
                if (countList[i] > 1)
                {
                    newChainPfamArch += (pfamList[i] + "." + countList[i] + "_");
                }
                else
                {
                    newChainPfamArch += (pfamList[i] + "_");
                }
            }
            return newChainPfamArch.TrimEnd('_');
        }
        #endregion

        #region domain group file name
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public static string GetDomainRelationName(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string fileName = "";
            string pfamId1 = "";
            string pfamId2 = "";
            if (pfamPairTable.Rows.Count > 0)
            {
                pfamId1 = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                if (pfamId1 == pfamId2)
                {
                    fileName = "(" + pfamId1 + ")_" + relSeqId;
                }
                else
                {
                    fileName = "(" + pfamId1 + ")(" + pfamId2 + ")_" + relSeqId;
                }
            }
            return fileName;
        }
        #endregion
    }
}
