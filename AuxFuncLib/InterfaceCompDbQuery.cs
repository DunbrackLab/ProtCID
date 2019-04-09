using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace AuxFuncLib
{
    public class InterfaceCompDbQuery
    {
        private DbQuery dbQuery = new DbQuery();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryList"></param>
        /// <param name="qScoreCutoff"></param>
        /// <returns></returns>
        public DataTable GetDifEntryInterfaceCompTable(List<string> entryList, double qScoreCutoff)
        {
            string[] entries = entryList.ToArray(); 
           DataTable interfaceCompTable = GetDifEntryInterfaceCompTable(entries, qScoreCutoff);
           return interfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        public DataTable GetDifEntryInterfaceCompTable(string[] entries, double qScoreCutoff)
        {
            DataTable interfaceCompTable = null;
            string queryString = "";
            string pdbId1 = "";
            string pdbId2 = "";
            bool isInserted = false;
            foreach (string pdbId in entries)
            {
                queryString = string.Format("Select * From DifEntryInterfaceComp " +
                                " Where PdbID1 = '{0}' OR PdbID2 = '{0}' AND QScore > {1};",
                                pdbId, qScoreCutoff);
                DataTable difEntryInterfaceCompTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
                if (interfaceCompTable == null)
                {
                    interfaceCompTable = difEntryInterfaceCompTable.Clone();
                }
                foreach (DataRow compRow in difEntryInterfaceCompTable.Rows)
                {
                    isInserted = false;
                    pdbId1 = compRow["PdbID1"].ToString();
                    pdbId2 = compRow["PdbID2"].ToString();
                    if (pdbId1 == pdbId)
                    {
                        if (Array.IndexOf (entries, pdbId2) > -1)
                        {
                            isInserted = true;
                        }
                    }
                    else if (pdbId2 == pdbId)
                    {
                        if (Array.IndexOf (entries, pdbId1) > -1)
                        {
                            isInserted = true;
                        }
                    }
                    if (isInserted)
                    {
                        DataRow dataRow = interfaceCompTable.NewRow();
                        dataRow.ItemArray = compRow.ItemArray;
                        interfaceCompTable.Rows.Add(dataRow);
                    }
                }
            }
            return interfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryList"></param>
        /// <param name="qScoreCutoff"></param>
        /// <returns></returns>
        public DataTable GetDifEntryInterfaceCompTable(List<string> entryList)
        {
            string[] entries = entryList.ToArray(); 
            DataTable interfaceCompTable = GetDifEntryInterfaceCompTable(entries);
            return interfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        public DataTable GetDifEntryInterfaceCompTable(string[] entries)
        {
            DataTable interfaceCompTable = null;
            string queryString = "";
            string pdbId1 = "";
            string pdbId2 = "";
            bool isInserted = false;
            foreach (string pdbId in entries)
            {
                queryString = string.Format("Select * From DifEntryInterfaceComp " +
                                " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
                DataTable difEntryInterfaceCompTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
                if (interfaceCompTable == null)
                {
                    interfaceCompTable = difEntryInterfaceCompTable.Clone();
                }
                foreach (DataRow compRow in difEntryInterfaceCompTable.Rows)
                {
                    isInserted = false;
                    pdbId1 = compRow["PdbID1"].ToString();
                    pdbId2 = compRow["PdbID2"].ToString();
                    if (pdbId1 == pdbId)
                    {
                        if (Array.IndexOf(entries, pdbId2) > -1)
                        {
                            isInserted = true;
                        }
                    }
                    else if (pdbId2 == pdbId)
                    {
                        if (Array.IndexOf(entries, pdbId1) > -1)
                        {
                            isInserted = true;
                        }
                    }
                    if (isInserted)
                    {
                        DataRow dataRow = interfaceCompTable.NewRow();
                        dataRow.ItemArray = compRow.ItemArray;
                        interfaceCompTable.Rows.Add(dataRow);
                    }
                }
            }
            return interfaceCompTable;
        } 
    }
}
