using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using InterfaceClusterLib.UserGroupInterfaces ;
using InterfaceClusterLib.AuxFuncs;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.Pkinase
{
    public class PkinaseInterfaceClusters
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
       private UserSuperGroupInterfaceBuilder chainRelInterfaceBuilder = new UserSuperGroupInterfaceBuilder ();
        #endregion


        public void ClusterPkinaseInterfaces()
        {
            ProtCidSettings.dataType = "pfam";
            DbBuilderHelper.Initialize();

            string pfamId = "";
            string[] pkinaseEntries = null;
            string groupArchString = "";
            int userGroupSeqId = 111111;

            pfamId = "Pkinase";
            pkinaseEntries = GetPfamEntries(pfamId);
            groupArchString = pfamId;
            userGroupSeqId = 111111;
            chainRelInterfaceBuilder.FindInterfaceClustersInUserGroup(groupArchString, userGroupSeqId, pkinaseEntries); 

            pfamId = "Pkinase_Tyr";
            pkinaseEntries = GetPfamEntries(pfamId);
            groupArchString = pfamId;
            userGroupSeqId = userGroupSeqId + 1;
            chainRelInterfaceBuilder.FindInterfaceClustersInUserGroup(groupArchString, userGroupSeqId, pkinaseEntries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamEntries(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pfamEntries = new string[pfamEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamEntryRow in pfamEntryTable.Rows)
            {
                pfamEntries[count] = pfamEntryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }

        public void PrintNonAlignedPairsFile()
        {
            DbBuilderHelper.Initialize();

            StreamReader dataReader = new StreamReader("RepEntriesInterGroupCompLog.txt");
            StreamWriter dataWriter = new StreamWriter("NonAlignedPairs.txt");
            StreamWriter noInterfaceEntryWriter = new StreamWriter("NoInterfaceEntries.txt");
            string line = "";
            List<string> noInterfaceEntryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("not aligned pair") > -1)
                {
                    string[] fields = line.Split(' ');
                    string[] entityChains1 = GetPolypeptideChains(fields[1]);
                    string[] entityChains2 = GetPolypeptideChains(fields[3]);
                    foreach (string entityChain1 in entityChains1)
                    {
                        foreach (string entityChain2 in entityChains2)
                        {
                            dataWriter.WriteLine(fields[1] + entityChain1 + "\t" + fields[3] + entityChain2);
                        }
                    }
                }
                else 
                {
                    int index = line.IndexOf("no cryst interfaces");
                    if (index > -1)
                    {
                        string pdbId = line.Substring(index - 5, 4);
                        if (! noInterfaceEntryList.Contains(pdbId))
                        {
                            noInterfaceEntryList.Add(pdbId);
                            noInterfaceEntryWriter.WriteLine(pdbId);
                        }
                    }
                }
            }
            dataReader.Close();
            dataWriter.Close();
            noInterfaceEntryWriter.Close();
        }

        private string[] GetPolypeptideChains(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide' " + 
                " Order By AsymID;", pdbId);
            DataTable entityAsymchainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<int> parsedEntityList = new List<int> ();
            List<string> chainList = new List<string> ();
            int entityId = 0;
            string asymChain = "";
            foreach (DataRow chainRow in entityAsymchainTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString ());
                asymChain = chainRow["AsymID"].ToString().TrimEnd();
                if (parsedEntityList.Contains(entityId))
                {
                    continue;
                }
                parsedEntityList.Add(entityId);
                chainList.Add(asymChain);
            }
            string[] entityChains = new string[chainList.Count];
            chainList.CopyTo(entityChains);
            return entityChains;
        }
    }
}
