using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AuxFuncLib;
using ProtCidSettingsLib;
using DbLib;

namespace BuCompLib
{
    public class BuCompObsoleteDataRemover : ObsoleteEntryInfoRemover
    {
        /// <summary>
        /// 
        /// </summary>
        public void RemoveObsoleteEntryInfoInProtCID()
        {
            string[] obsEntries = GetBuCompEntriesNotInPdb();
            string[] dbTables = GetDbTables(ProtCidSettings.buCompConnection);
            DbUpdate dbDelete = new DbUpdate(ProtCidSettings.buCompConnection);
            foreach (string tableName in dbTables)
            {
                foreach (string obsPdbId in obsEntries)
                {
                    DeleteEntryInfoFromTable(obsPdbId, tableName, ProtCidSettings.buCompQuery, dbDelete);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetBuCompEntriesNotInPdb()
        {
            string entryTableName = "AsuInterfaces";
            string[] currentEntries = GetEntriesInCurrentPdb(ProtCidSettings.pdbfamQuery);

            string[] obsAsuEntries = GetObsoleteEntries(ProtCidSettings.buCompQuery, entryTableName, currentEntries);

            entryTableName = "PdbBuInterfaces";
            string[] obsPdbEntries = GetObsoleteEntries(ProtCidSettings.buCompQuery, entryTableName, currentEntries);

            entryTableName = "PisaBuInterfaces";
            string[] obsPisaEntries = GetObsoleteEntries(ProtCidSettings.buCompQuery, entryTableName, currentEntries);

            List<string> obsEntryList = new List<string>();
            obsEntryList.AddRange(obsAsuEntries);
            foreach (string obsEntry in obsPdbEntries)
            {
                if (!obsEntryList.Contains(obsEntry))
                {
                    obsEntryList.Add(obsEntry);
                }
            }
            foreach (string obsEntry in obsPisaEntries)
            {
                if (!obsEntryList.Contains(obsEntry))
                {
                    obsEntryList.Add(obsEntry);
                }
            }
            return obsEntryList.ToArray();
        }
    }
}
