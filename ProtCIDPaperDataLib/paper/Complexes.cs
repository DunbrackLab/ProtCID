using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace ProtCIDPaperDataLib.paper
{
    public class Complexes : PaperDataInfo
    {
        public Complexes ()
        {
            Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainClustersSharedEntries ()
        {
            StreamWriter chainClustersWriter = new StreamWriter(Path.Combine (dataDir, "ChainClustersForComplexes.txt"));
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperClusterEntryInterfaces;";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int chainGroupId = 0;
            foreach (DataRow groupRow in chainGroupTable.Rows)
            {
                chainGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString ());
            }
        }

    }
}
