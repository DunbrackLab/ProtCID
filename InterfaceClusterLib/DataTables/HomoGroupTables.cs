using System;
using System.Data;
using ProtCidSettingsLib;
using DbLib;

namespace InterfaceClusterLib.DataTables
{
	/// <summary>
	/// tables for homologous groups
	/// </summary>
	public class HomoGroupTables
	{
		public static DataTable[] homoGroupTables = null;
		public const int HomoSeqInfo = 0;
		public const int HomoRepEntryAlign = 1;
		public const int HomoGroupEntryAlign = 2;
		public const int FamilyGroups = 3;

		public HomoGroupTables()
		{
		}

        /// <summary>
        /// 
        /// </summary>
		public static void InitializeTables ()
		{
            if (homoGroupTables == null)
            {
                // representative entries in each space group in a scoplogous group
                string[] seqInfoCols = { "GroupSeqID", "PdbID", "SpaceGroup", "ASU", "Method", "Resolution"};
                if (ProtCidSettings.dataType == "scop" || ProtCidSettings.dataType.IndexOf ("pfam") > -1)
                {
                    homoGroupTables = new DataTable[4];
                    homoGroupTables[HomoSeqInfo] = new DataTable(ProtCidSettings.dataType + "HomoSeqInfo");
                    homoGroupTables[HomoSeqInfo].Columns.Add(new DataColumn ("In" + ProtCidSettings.dataType));

                    homoGroupTables[HomoRepEntryAlign] = new DataTable(ProtCidSettings.dataType + "HomoRepEntryAlign");
                    homoGroupTables[HomoGroupEntryAlign] = new DataTable(ProtCidSettings.dataType + "HomoGroupEntryAlign");
                    homoGroupTables[FamilyGroups] = new DataTable(ProtCidSettings.dataType + "Groups");
                }
                else
                {
                    homoGroupTables = new DataTable[3];
                    homoGroupTables[HomoSeqInfo] = new DataTable(ProtCidSettings.dataType + "HomoSeqInfo");
                    homoGroupTables[HomoRepEntryAlign] = new DataTable(ProtCidSettings.dataType + "HomoRepEntryAlign");
                    homoGroupTables[HomoGroupEntryAlign] = new DataTable(ProtCidSettings.dataType + "HomoGroupEntryAlign");
                }
                foreach (string seqInfoCol in seqInfoCols)
                {
                    homoGroupTables[HomoSeqInfo].Columns.Add(new DataColumn(seqInfoCol));
                }        

                // sequence alignment between representative entry and its scoplogous entries
                string[] repAlignCols = {"GroupSeqID", "PdbID1", "EntityID1", "PdbID2", "EntityID2",
										"Identity", "QueryStart", "QueryEnd", "HitStart", "HitEnd", 
										"QuerySequence", "HitSequence"};
                foreach (string repCol in repAlignCols)
                {
                    homoGroupTables[HomoRepEntryAlign].Columns.Add(new DataColumn(repCol));
                }
                // sequence alignment between entries in a group
                string[] groupAlignCols =  {"GroupSeqID", "PdbID1", "EntityID1", "PdbID2", "EntityID2",
										   "Identity", "QueryStart", "QueryEnd", "HitStart", "HitEnd", 
										   "QuerySequence", "HitSequence"};
                foreach (string entryCol in groupAlignCols)
                {
                    homoGroupTables[HomoGroupEntryAlign].Columns.Add(new DataColumn(entryCol));
                }

                if (ProtCidSettings.dataType == "scop" || ProtCidSettings.dataType.IndexOf ("pfam") > -1)
                {
                    string[] familyGroupCols = { "GroupSeqID", "EntryPfamArch" };
                    foreach (string familyCol in familyGroupCols)
                    {
                        homoGroupTables[FamilyGroups].Columns.Add(new DataColumn(familyCol));
                    }
                }
            }
		}
        /// <summary>
        /// 
        /// </summary>
        public static void InitializeAllGroupDbTables()
        {
            DbCreator dbCreator = new DbCreator();
            string createTableString = "";
            string createIndexString = "";
            string tableName = ProtCidSettings.dataType + "HomoSeqInfo";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL, " +
                              "PdbID CHAR(4) NOT NULL, " +
                              "SpaceGroup VARCHAR(30) NOT NULL, " +
                              "ASU BLOB Sub_Type TEXT NOT NULL, " +
                              "Method VARCHAR(100) NOT NULL, " +
                              "Resolution FLOAT NOT NULL, " +
                              "InPfam CHAR NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (GroupSeqID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx2 ON {0} (PdbID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL," +
                              "PdbID1 CHAR(4) NOT NULL," +
                              "EntityID1 INTEGER NOT NULL," +
                              "PdbID2 CHAR(4) NOT NULL," +
                              "EntityID2 INTEGER NOT NULL," +
                              "Identity FLOAT NOT NULL," +
                              "QueryStart INTEGER NOT NULL," +
                              "QueryEnd INTEGER NOT NULL," +
                              "HitStart INTEGER NOT NULL," +
                              "HitEnd INTEGER NOT NULL," +
                              "QuerySequence BLOB Sub_Type TEXT NOT NULL," +
                              "HitSequence BLOB Sub_Type TEXT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (PdbID1);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx2 ON {0} (PdbID2);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL," +
                              "PdbID1 CHAR(4) NOT NULL," +
                              "EntityID1 INTEGER NOT NULL," +
                              "PdbID2 CHAR(4) NOT NULL," +
                              "EntityID2 INTEGER NOT NULL," +
                              "Identity FLOAT NOT NULL," +
                              "QueryStart INTEGER NOT NULL," +
                              "QueryEnd INTEGER NOT NULL," +
                              "HitStart INTEGER NOT NULL," +
                              "HitEnd INTEGER NOT NULL," +
                              "QuerySequence BLOB Sub_Type TEXT NOT NULL," +
                              "HitSequence BLOB Sub_Type TEXT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            string indexName = ProtCidSettings.dataType + "GroupEntryAlign";
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {1} (PdbID1);", indexName, tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx2 ON {1} (PdbID2);", indexName, tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = ProtCidSettings.dataType + "Groups";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL, " +
                              "EntryPfamArch BLOB Sub_Type TEXT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (GroupSeqID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
        }
        /// <summary>
        /// 
        /// </summary>
        public static void InitializeGroupTable()
        {
            DbCreator dbCreator = new DbCreator();
            string createTableString = "";
            string createIndexString = "";
            string tableName = ProtCidSettings.dataType + "HomoSeqInfo";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL, " +
                              "PdbID CHAR(4) NOT NULL, " +
                              "SpaceGroup VARCHAR(30) NOT NULL, " +
                              "ASU VARCHAR(50) NOT NULL, " +
                              "Method VARCHAR(100) NOT NULL, " +
                              "Resolution FLOAT NOT NULL, " +
                              "InPfam CHAR NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (GroupSeqID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx2 ON {0} (PdbID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = ProtCidSettings.dataType + "Groups";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                              "GroupSeqID INTEGER NOT NULL, " +
                              "EntryPfamArch BLOB Sub_Type TEXT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (GroupSeqID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void AddFlagColumn()
        {
            string flagCol = "used";
            homoGroupTables[HomoRepEntryAlign].Columns.Add(new DataColumn (flagCol));
            homoGroupTables[HomoGroupEntryAlign].Columns.Add(new DataColumn (flagCol));

            DbQuery dbAlter = new DbQuery();
            string alterString = string.Format("Alter table {0} Add Used CHAR(1);", homoGroupTables[HomoRepEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, alterString);
            string updateString = string.Format("Update {0} Set Used = '0';", homoGroupTables[HomoRepEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, updateString);

            alterString = string.Format("Alter table {0} Add Used CHAR(1);", homoGroupTables[HomoGroupEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, alterString);
            updateString = string.Format("Update {0} Set Used = '0';", homoGroupTables[HomoGroupEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void RemoveFlagColumn()
        {
            string flagCol = "used";
            homoGroupTables[HomoRepEntryAlign].Columns.Remove(flagCol);
            homoGroupTables[HomoGroupEntryAlign].Columns.Remove(flagCol);

            DbQuery dbAlter = new DbQuery();
            string alterString = string.Format("Alter Table {0} Drop Used;", homoGroupTables[HomoRepEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, alterString);

            alterString = string.Format("Alter Table {0} Drop Used;", homoGroupTables[HomoGroupEntryAlign].TableName);
            dbAlter.Query(ProtCidSettings.protcidDbConnection, alterString);
        }
        /// <summary>
        /// 
        /// </summary>
		public static void InitializeAlignInfoTable ()
		{
			if (ProtCidSettings.dataType == "scop" || ProtCidSettings.dataType.IndexOf ("pfam") > -1)
			{
				homoGroupTables = new DataTable [4];
				homoGroupTables[HomoRepEntryAlign] = new DataTable (ProtCidSettings.dataType +  "HomoRepEntryAlign");
			}
			else
			{
				homoGroupTables = new DataTable [3];
				homoGroupTables[HomoRepEntryAlign] = new DataTable (ProtCidSettings.dataType + "HomoRepEntryAlign");
			}
			// sequence alignment between representative entry and its homologous entries
			string[] repAlignCols = {"GroupSeqID", "PdbID1", "EntityID1", "PdbID2", "EntityID2",
										"Identity", "QueryStart", "QueryEnd", "HitStart", "HitEnd", 
										"QuerySequence", "HitSequence"};			
			foreach (string repCol in repAlignCols)
			{
				homoGroupTables[HomoRepEntryAlign].Columns.Add (new DataColumn(repCol));
			}
		}

        /// <summary>
        /// clear data in all  memory tables
        /// </summary>
		public static void ClearTables ()
		{
			foreach (DataTable dataTable in homoGroupTables)
			{
				dataTable.Clear ();
			}
		}
	}
}
