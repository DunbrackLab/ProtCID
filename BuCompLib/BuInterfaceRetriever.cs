using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using DbLib;
using ProtCidSettingsLib;

namespace BuCompLib
{
    public class BuInterfaceRetriever
    {
        private DbQuery dbQuery = new DbQuery();
        private BiolUnitRetriever buRetriever = new BiolUnitRetriever();

        #region retrieve interfaces from BUs of the entry
        /// <summary>
        /// interfaces in each BU of the entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        public Dictionary<string, InterfaceChains[]> GetEntryBuInterfaces(string pdbId, string buType)
        {
            string queryString = string.Format("Select Distinct BuID From {0}BuSameInterfaces " +
                " Where PdbID = '{1}';", buType, pdbId);
            DataTable buTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string[] buIDs = new string[buTable.Rows.Count];
            int count = 0;
            foreach (DataRow buRow in buTable.Rows)
            {
                buIDs[count] = buRow["BuID"].ToString().TrimEnd();
                count++;
            }
            return GetEntryBuInterfaces (pdbId, buIDs, buType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIDs"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        public Dictionary<string, InterfaceChains[]> GetEntryBuInterfaces(string pdbId, string[] buIDs, string buType)
        {
            string queryString = "";
            Dictionary<string, InterfaceChains[]> entryBuInterfaceHash = new Dictionary<string,InterfaceChains[]> ();

            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnitHash = GetEntryBiolUnits(pdbId, buIDs, buType);
            if (entryBiolUnitHash == null || entryBiolUnitHash.Count == 0)
            {
                return entryBuInterfaceHash;
            }       

            foreach (string buId in buIDs)
            {
                queryString = string.Format("Select * From {0}BuSameInterfaces " +
                    " Where PdbID = '{1}' AND BuID = '{2}' AND InterfaceID = SameInterfaceID;",
                    buType, pdbId, buId);
                DataTable buInterfaceDefTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
                InterfaceChains[] buInterfaces = GetEntryBiolUnitInterfaces(buInterfaceDefTable, entryBiolUnitHash[buId], buType);
                entryBuInterfaceHash.Add(buId, buInterfaces);
            }
            return entryBuInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterfaceDefTable"></param>
        /// <param name="biolUnitHash"></param>
        /// <returns></returns>
        private InterfaceChains[] GetEntryBiolUnitInterfaces(DataTable buInterfaceDefTable, Dictionary<string, AtomInfo[]> biolUnitHash, string buType)
        {
            string chainSymOpString1 = "";
            string chainSymOpString2 = "";
            List<InterfaceChains> buInterfaceList = new List<InterfaceChains> ();
           
            foreach (DataRow interfaceDefRow in buInterfaceDefTable.Rows)
            {
                if (buType == "pqs") // use PQS chains
                {
                    chainSymOpString1 = interfaceDefRow["Chain1"].ToString().TrimEnd();
                    chainSymOpString2 = interfaceDefRow["Chain2"].ToString().TrimEnd();
                }
                else
                {
                    chainSymOpString1 = interfaceDefRow["Chain1"].ToString().TrimEnd() + "_" +
                        interfaceDefRow["SymmetryString1"].ToString().TrimEnd();
                    chainSymOpString2 = interfaceDefRow["Chain2"].ToString().TrimEnd() + "_" +
                        interfaceDefRow["SymmetryString2"].ToString().TrimEnd();
                }
                if (biolUnitHash.ContainsKey(chainSymOpString1) && biolUnitHash.ContainsKey(chainSymOpString2))
                {
                    InterfaceChains buInterface = new InterfaceChains();
                    buInterface.firstSymOpString = chainSymOpString1;
                    buInterface.secondSymOpString = chainSymOpString2;
                    buInterface.interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    buInterface.pdbId = interfaceDefRow["PdbID"].ToString();
                    buInterface.chain1 = (AtomInfo[])biolUnitHash[chainSymOpString1];
                    buInterface.chain2 = (AtomInfo[])biolUnitHash[chainSymOpString2];
                    buInterface.GetInterfaceResidueDist();
                    buInterfaceList.Add(buInterface);
                }
            }
            return buInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIDs"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> GetEntryBiolUnits(string pdbId, string[] buIDs, string buType)
        {
            BuCompBuilder.BuType = buType;
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnitHash = buRetriever.GetEntryBiolUnits(pdbId, buIDs);
            return entryBiolUnitHash;
        }
        #endregion
    }
}
