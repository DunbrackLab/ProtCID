using System;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Summary description for DomainInterfaceTables.
	/// </summary>
	public class DomainInterfaceTables
	{
		public static DataTable[] domainInterfaceTables = new DataTable [6];
		public static int DomainInterfaces = 0;
		public static int DomainInterfaceComp = 1;
		public static int DomainFamilyRel = 2;
   //     public static int DomainRepAlign = 3;
        public static int EntryDomainInterfaceComp = 3;
        public static int DomainClanRel = 4;
        public static int ClanDomainInterfaceComp = 5;

		public DomainInterfaceTables()
		{
		}

		/// <summary>
		/// initialize tables in memory 
		/// </summary>
		public static void InitializeTables ()
		{
			string[] domainInterfaceColumns = {"RelSeqID", "PdbID", "InterfaceID", "DomainInterfaceID", 
				//	 "InterfaceID", "SpaceGroup", "ASU", "DomainID1", "DomainID2", "NumOfSg", "NumOfSg_Identity"};
					"DomainID1", "DomainID2", "AsymChain1", "AsymChain2", "IsReversed", "SurfaceArea"};
						//		"DomainRange1", "DomainRange2", 
                        //        "NumOfSg", "NumOfSg_Identity", 
			domainInterfaceTables[DomainInterfaces] = new DataTable (ProtCidSettings.dataType + "DomainInterfaces");
			foreach (string colName in domainInterfaceColumns)
			{
				domainInterfaceTables[DomainInterfaces].Columns.Add 
					(new DataColumn (colName));
			}

			string[] domainInterfaceCompColumns = {"RelSeqID", "PdbID1", "DomainInterfaceID1", 
								"PdbID2", "DomainInterfaceID2", "QScore", "Identity", "IsReversed"}; 
			domainInterfaceTables[DomainInterfaceComp] = new DataTable (ProtCidSettings.dataType + "DomainInterfaceComp");
			foreach (string colName in domainInterfaceCompColumns)
			{
				domainInterfaceTables[DomainInterfaceComp].Columns.Add 
					(new DataColumn (colName));
			}

			string[] relationSeqColumns = {"RelSeqID", "FamilyCode1", "FamilyCode2", "ClanSeqID"};
			domainInterfaceTables[DomainFamilyRel] = new DataTable (ProtCidSettings.dataType + "DomainFamilyRelation");
			foreach (string colName in relationSeqColumns)
			{
				domainInterfaceTables[DomainFamilyRel].Columns.Add (new DataColumn (colName));
			}

            string[] clanRelationColumns = {"ClanSeqID", "ClanCode1", "ClanCode2"};
            domainInterfaceTables[DomainClanRel] = new DataTable(ProtCidSettings.dataType + "DomainClanRelation");
            foreach (string colName in clanRelationColumns)
            {
                domainInterfaceTables[DomainClanRel].Columns.Add(new DataColumn (colName));
            }

            string[] entryDomainInterfaceCompColumns = {"RelSeqID", "PdbID", "DomainInterfaceID1", 
								"DomainInterfaceID2", "QScore", "IsReversed"};
            domainInterfaceTables[EntryDomainInterfaceComp] = new DataTable(ProtCidSettings.dataType + "EntryDomainInterfaceComp");
            foreach (string colName in entryDomainInterfaceCompColumns)
            {
                domainInterfaceTables[EntryDomainInterfaceComp].Columns.Add
                    (new DataColumn(colName));
            }

            string[] clanDomainInterfaceCompColumns = {"ClanSeqID", "RelSeqID1", "PdbID1", "DomainInterfaceID1", 
                              "RelSeqID2", "PdbID2", "DomainInterfaceID2", "Qscore", "Identity", "IsReversed"};
            domainInterfaceTables[ClanDomainInterfaceComp] = new DataTable("ClanDomainInterfaceComp");
            foreach (string clanCompCol in clanDomainInterfaceCompColumns)
            {
                domainInterfaceTables[ClanDomainInterfaceComp].Columns.Add (new DataColumn (clanCompCol));
            }
		}

		/// <summary>
		/// initialize tables in the database
		/// </summary>
		public static void InitializeDbTables ()
		{
			DbCreator dbCreate = new DbCreator ();
            string tableName = ProtCidSettings.dataType + "DomainInterfaceComp";
            string createTableString = "Create Table " + tableName + 
                "( RelSeqID INTEGER NOT NULL, " +
                " PdbID1 CHAR(4) NOT NULL, " +
                " DomainInterfaceID1 INTEGER NOT NULL, " +
                " PdbID2 CHAR(4) NOT NULL, " +
                " DomainInterfaceID2 INTEGER NOT NULL, " +
                " QScore FLOAT NOT NULL, " + 
                " Identity FLOAT NOT NULL, " + 
                " IsReversed CHAR(1) );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            string createIdxString = "Create INDEX " + tableName + "_idx1 ON " + tableName + "(PdbID1);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx2 ON " + tableName + " (PdbID2);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = ProtCidSettings.dataType + "EntryDomainInterfaceComp";
            createTableString = "Create Table " + tableName +
                "( RelSeqID INTEGER NOT NULL, " +
                " PdbID CHAR(4) NOT NULL, " +
                " DomainInterfaceID1 INTEGER NOT NULL, " +
                " DomainInterfaceID2 INTEGER NOT NULL, " +
                " QScore FLOAT NOT NULL, " + 
                " IsReversed CHAR(1) );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + ProtCidSettings.dataType + "EntryDomInterComp_idx1 ON " + tableName + "(RelSeqID, PdbID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = ProtCidSettings.dataType + "DomainInterfaces";
            createTableString = "Create Table " + tableName +
                "( RelSeqID INTEGER NOT NULL, " +
                " PdbID CHAR(4) NOT NULL, " +
                " InterfaceID INTEGER NOT NULL, " +
                " DomainInterfaceID INTEGER NOT NULL, " +
                " DomainID1 BIGINT NOT NULL, " +
                " AsymChain1 CHAR(3) NOT NULL, " +
                " DomainID2 BIGINT NOT NULL, " +
                " AsymChain2 CHAR(3) NOT NULL, " +
                " SurfaceArea FLOAT, " +
                " ChainDomainID1 INTEGER, " + 
                " ChainDomainID2 INTEGER, " + 
                " IsReversed CHAR(1) NOT NULL);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx1 ON " + tableName + " (PdbID, InterfaceID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx2 ON " + tableName + " (DomainID1);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx3 ON " + tableName + " (DomainID2);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx4 ON " + tableName + " (RelSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = ProtCidSettings.dataType + "DomainFamilyRelation";
            createTableString = "Create Table " + tableName +
                "( RelSeqID INTEGER NOT NULL, " + 
                " FamilyCode1 VARCHAR(40) NOT NULL, " + 
                " FamilyCode2 VARCHAR(40) NOT NULL, " + 
                " ClanSeqID INTEGER NOT NULL);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + ProtCidSettings.dataType + "DomainRel_idx1 ON " + tableName + " (RelSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = ProtCidSettings.dataType + "DomainClanRelation";
            createTableString = "Create Table " + tableName +
                "(ClanSeqID INTEGER NOT NULL, " +
                "ClanCode1 VARCHAR(40) NOT NULL, " +
                "ClanCode2 VARCHAR(40) NOT NULL);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + ProtCidSettings.dataType + "ClanRel_idx1 ON " + tableName + " (ClanSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = "ClanDomainInterfaceComp";
            createTableString = "Create Table " + tableName +
               "( ClanSeqID INTEGER NOT NULL, " +
               " RelSeqID1 INTEGER NOT NULL, " + 
               " PdbID1 CHAR(4) NOT NULL, " +
               " DomainInterfaceID1 INTEGER NOT NULL, " +
               " RelSeqID2 INTEGER NOT NULL, " +
               " PdbID2 CHAR(4) NOT NULL, " +
               " DomainInterfaceID2 INTEGER NOT NULL, " +
               " QScore FLOAT NOT NULL, " +
               " Identity FLOAT NOT NULL, " + 
               " IsReversed CHAR(1) );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx1 ON " + tableName + "(PdbID1);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx2 ON " + tableName + " (PdbID2);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);

            tableName = "PfamDomainCfGroups";
            createTableString = "Create Table " + tableName + "( " +
                " RelSeqID INTEGER NOT NULL, " +
                " RelCfGroupID INTEGER NOT NULL, " +
                " GroupSeqID INTEGER NOT NULL, " +
                " CfGroupID INTEGER NOT NULL);";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = "Create INDEX " + tableName + "_idx1 ON " + tableName + "(RelSeqID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
		} 
	}
}
