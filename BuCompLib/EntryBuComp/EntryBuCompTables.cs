using System;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace BuCompLib.EntryBuComp
{
    public class EntryBuCompTables
    {
        #region data tables
        public static DataTable[] entryBuCompTables = new DataTable[2];
        public static int PdbPisaBuComp = 0;
        public static int PdbPisaBuInterfaceComp = 1;
        #endregion

        /// <summary>
        /// initialize tables in memory
        /// </summary>
        public static void InitializeTables ()
        {
            string[] buCompColumns = {"PdbID", "BuID1", "BuID2", "InterfaceNum1", "InterfaceNum2", 
                                     "IsSame"};
            entryBuCompTables[PdbPisaBuComp] = new DataTable("PdbPisaBuComp");
            foreach (string buCompCol in buCompColumns)
            {
                entryBuCompTables[PdbPisaBuComp].Columns.Add(new DataColumn (buCompCol));
            }

            string[] buInterfaceCompColumns = {"PdbID", "BuID1", "BuID2", "InterfaceID1", "InterfaceID2", 
                                              "QScore"};
            entryBuCompTables[PdbPisaBuInterfaceComp] = new DataTable("PdbPisaBuInterfaceComp");
            foreach (string buInterfaceCompCol in buInterfaceCompColumns)
            {
                entryBuCompTables[PdbPisaBuInterfaceComp].Columns.Add(new DataColumn (buInterfaceCompCol));
            }
        }

        /// <summary>
        /// initialize tables in database
        /// </summary>
        public static void InitializeDbTables()
        {
            DbCreator dbCreator = new DbCreator();
            string createTableString = "";
            string createIndexString = "";
            string buCompTableName = "PdbPisaBuComp";
            string buInterfaceCompTableName = "PdbPisaBuInterfaceComp";

            // for BU Comparison

            createTableString = "CREATE TABLE " + buCompTableName + " ( " +
                "PdbID CHAR(4) NOT NULL, " +
                "BuID1 VARCHAR(8) NOT NULL, " +
                "BuID2 VARCHAR(8) NOT NULL, " +
                "InterfaceNum1 INTEGER NOT NULL, " +
                "InterfaceNum2 INTEGER NOT NULL, " +
                "IsSame CHAR(1) NOT NULL);";

            dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, buCompTableName);

            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (PdbID, BuID1, BuID2);", buCompTableName);
            dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, buCompTableName);

            // for BU Interface comparison
            createTableString = "CREATE TABLE " + buInterfaceCompTableName + " ( " +
                  "PdbID CHAR(4) NOT NULL, " +
                  "BuID1 VARCHAR(8) NOT NULL, " +
                  "BuID2 VARCHAR(8) NOT NULL, " +
                  "InterfaceID1 INTEGER NOT NULL, " +
                  "InterfaceID2 INTEGER NOT NULL, " +
                  "QScore FLOAT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, buInterfaceCompTableName);

            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (PdbID, BuID1, BuID2);", buInterfaceCompTableName);
            dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, buInterfaceCompTableName);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void ClearTables()
        {
            foreach (DataTable dataTable in entryBuCompTables)
            {
                dataTable.Clear();
            }
        }
    }
}
