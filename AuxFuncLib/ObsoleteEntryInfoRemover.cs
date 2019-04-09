using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DbLib;

namespace AuxFuncLib
{
    public class ObsoleteEntryInfoRemover
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsPdbId"></param>
        /// <param name="tableName"></param>
        /// <param name="dbDelete"></param>
        public void DeleteEntryInfoFromTable(string obsPdbId, string tableName, DbQuery dbQuery, DbUpdate dbDelete)
        {
            string deleteString = "";
            string queryString = string.Format("Select First 1 * From {0};", tableName);
            DataTable dbTable = dbQuery.Query(queryString);
            if (dbTable.Columns.Contains("PdbID"))
            {
                deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", tableName, obsPdbId);
            }
            else if (dbTable.Columns.Contains("PdbID1"))
            {
                deleteString = string.Format("Delete From {0} Where PdbID1 = '{1}';", tableName, obsPdbId);
            }
            else if (dbTable.Columns.Contains("PdbID2"))
            {
                deleteString = string.Format("Delete From {0} Where PdbID2 = '{1}';", tableName, obsPdbId);
            }
            if (deleteString != "")
            {
                dbDelete.Delete(deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryTableName"></param>
        /// <param name="currentEntries"></param>
        /// <returns></returns>
        public string[] GetObsoleteEntries(DbQuery dbQuery, string entryTableName, string[] currentEntries)
        {
            string queryString = string.Format("Select Distinct PdbID From {0};", entryTableName);
            DataTable chainEntryTable = dbQuery.Query(queryString);
            List<string> obsEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in chainEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (Array.BinarySearch(currentEntries, pdbId) < 0)
                {
                    obsEntryList.Add(pdbId);
                }
            }
            return obsEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetEntriesInCurrentPdb(DbQuery currDbQuery)
        {
            string queryString = "Select Distinct PdbID From PdbEntry Order By PdbID;";
            DataTable entryTable = currDbQuery.Query(queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetDbTables(DbConnect dbConnect)
        {
            System.Data.Odbc.OdbcCommand showTableCommand = dbConnect.CreateCommand();
            showTableCommand.CommandText = @"SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0;";

            DataTable tableNamesTable = new DataTable("Table Names");
            System.Data.Odbc.OdbcDataAdapter adapter = new System.Data.Odbc.OdbcDataAdapter();
            adapter.SelectCommand = showTableCommand;
            adapter.Fill(tableNamesTable);

            List<string> dbTableList = new List<string>();
            foreach (DataRow dRow in tableNamesTable.Rows)
            {
                string tableString = dRow[0].ToString();
                dbTableList.Add(tableString.Trim());
            }

            return dbTableList.ToArray();
        }
    }
}
