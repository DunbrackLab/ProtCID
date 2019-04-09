using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.AuxFuncs
{
    public class EntryCrystForms
    {
        private string[] homoEntries = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputEntries"></param>
        /// <returns></returns>
        public int GetNumberOfCFs(string[] inputEntries)
        {
            string queryString = "";
            List<string> cfGroupIdList = new List<string>();
            string cfGroup = "";
            for (int i = 0; i < inputEntries.Length; i += 300)
            {
                string[] subEntries = ParseHelper.GetSubArray(inputEntries, i, 300);
                queryString = string.Format("Select GroupSeqID, CfGroupId, PdbID From PfamNonRedundantCfGroups Where PdbID IN ({0});",
                    ParseHelper.FormatSqlListString(subEntries));
                DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow cfGroupRow in cfGroupTable.Rows)
                {
                    cfGroup = cfGroupRow["GroupSeqID"].ToString() + "_" + cfGroupRow["CfGroupID"].ToString();
                    if (!cfGroupIdList.Contains(cfGroup))
                    {
                        cfGroupIdList.Add(cfGroup);
                    }
                }
            }
            return cfGroupIdList.Count;
        }

 /*       /// <summary>
        /// 
        /// </summary>
        /// <param name="inputEntries"></param>
        /// <returns></returns>
        public int GetNumberOfCFs(string[] inputEntries)
        {
            string queryString = "";
            List<string> cfGroupIdList = new List<string>();
            List<string> entryNoCfList = new List<string> ();
            entryNoCfList.AddRange(inputEntries);
            string cfGroup = "";
            string pdbId = "";
            for (int i = 0; i < inputEntries.Length; i += 300)
            {
                string[] subEntries = ParseHelper.GetSubArray(inputEntries, i, 300);
                queryString = string.Format("Select GroupSeqID, CfGroupId, PdbID From PfamNonRedundantCfGroups Where PdbID IN ({0});",
                    ParseHelper.FormatSqlListString(subEntries));
                DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow cfGroupRow in cfGroupTable.Rows)
                {
                    cfGroup = cfGroupRow["GroupSeqID"].ToString() + "_" + cfGroupRow["CfGroupID"].ToString();
                    if (!cfGroupIdList.Contains(cfGroup))
                    {
                        cfGroupIdList.Add(cfGroup);
                    }
                    pdbId = cfGroupRow["PdbID"].ToString();
                    entryNoCfList.Remove(pdbId);
                }
            }
            List<string> updateEntryList = RemoveSameCfsByPfamHomoRepEntryAlign(entryNoCfList);
            return cfGroupIdList.Count + updateEntryList.Count;
        }
        */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputEntries"></param>
        public List<string> RemoveSameCfsByPfamHomoRepEntryAlign(List<string> inputEntryList)
        {
            if (homoEntries == null)
            {
                homoEntries = GetHomoEntries();
            }
            List<string> updateEntryList = new List<string>(inputEntryList);
            foreach (string pdbId in inputEntryList)
            {
                if (Array.BinarySearch(homoEntries, pdbId) > -1)
                {
                    updateEntryList.Remove(pdbId);
                }
            }
            return updateEntryList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetHomoEntries()
        {
            string queryString = "Select Distinct PdbID2 From PfamHomoRepEntryAlign;";
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> homoEntryList = new List<string>();
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                homoEntryList.Add (entryRow["PdbID2"].ToString());
            }
            homoEntryList.Sort();
            return homoEntryList.ToArray ();
        }
    }
}
