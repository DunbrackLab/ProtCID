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

namespace InterfaceClusterLib.EntryInterfaces
{
    public class ProtCidObsoleteDataRemover  : ObsoleteEntryInfoRemover
    {
        
        /// <summary>
        /// 
        /// </summary>
        public void RemoveObsoleteEntryInfoInProtCID ()
        {
            string[] obsEntries = GetProtCIDEntriesNotInPdb ();
            string[] dbTables = GetDbTables(ProtCidSettings.protcidDbConnection);
            DbUpdate dbDelete = new DbUpdate(ProtCidSettings.protcidDbConnection);
            foreach (string tableName in dbTables)
            {
                foreach (string obsPdbId in obsEntries)
                {
                    DeleteEntryInfoFromTable(obsPdbId, tableName, ProtCidSettings.protcidQuery, dbDelete);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetProtCIDEntriesNotInPdb()
        {
            string entryTableName = "CrystEntryInterfaces";
            string[] currentEntries = GetEntriesInCurrentPdb(ProtCidSettings.pdbfamQuery);

            string[] obsChainEntries = GetObsoleteEntries(ProtCidSettings.protcidQuery, entryTableName, currentEntries);

            entryTableName = "PfamDomainInterfaces";
            string[] obsDomainEntries = GetObsoleteEntries(ProtCidSettings.protcidQuery, entryTableName, currentEntries);

            List<string> obsEntryList = new List<string>();
            obsEntryList.AddRange(obsChainEntries);
            foreach (string obsEntry in obsDomainEntries)
            {
                if (!obsEntryList.Contains(obsEntry))
                {
                    obsEntryList.Add(obsEntry);
                }
            }
            return obsEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetObsoleteChainEntries ()
        {
            StreamWriter obsChainEntryWriter = new StreamWriter("ObsChainEntries.txt", true);
            string[] currentEntries = GetEntriesInCurrentPdb(ProtCidSettings.pdbfamQuery);

            List<string> obsEntryList = new List<string>();
            string pdbId = "";
            string queryString = "Select PdbID From PfamHomoSeqInfo;";
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (Array.BinarySearch (currentEntries, pdbId) < 0)
                {
                    obsEntryList.Add(pdbId);
                    obsChainEntryWriter.WriteLine(pdbId);
                }
            }
            queryString = "Select Distinct PdbID2 As PdbID From PfamHomoRepEntryAlign;";
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (Array.BinarySearch(currentEntries, pdbId) < 0)
                {
                    obsEntryList.Add(pdbId);
                    obsChainEntryWriter.WriteLine(pdbId);
                }
            }
            obsChainEntryWriter.Close();
            return obsEntryList.ToArray();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetObsoleteDomainEntries()
        {
            StreamWriter obsDomainEntryWriter = new StreamWriter("ObsDomainEntries.txt", true);
            string[] currentEntries = GetEntriesInCurrentPdb(ProtCidSettings.pdbfamQuery);

            List<string> obsEntryList = new List<string>();
            string pdbId = "";
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (Array.BinarySearch(currentEntries, pdbId) < 0)
                {
                    obsEntryList.Add(pdbId);
                    obsDomainEntryWriter.WriteLine(pdbId);
                }
            }
            obsDomainEntryWriter.Close();
            return obsEntryList.ToArray();
        }
         
    }
}
