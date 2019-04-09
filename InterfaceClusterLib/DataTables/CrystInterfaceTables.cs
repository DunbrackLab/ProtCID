using System;
using System.Data;
using ProtCidSettingsLib;
using DbLib;

namespace InterfaceClusterLib.DataTables
{
	/// <summary>
	/// Summary description for CrystInterfaceTables.
	/// </summary>
	public class CrystInterfaceTables
	{
		public static DataTable[] crystInterfaceTables = null;
		public const int CrystEntryInterfaces = 0;
		public const int SgInterfaces = 1;		
		public const int SgInterfaceResidues = 2;
		public const int SgInterfaceContacts = 3;
		public const int DifEntryInterfaceComp = 4;
		public const int EntryInterfaceComp = 5;		
	
		public CrystInterfaceTables()
		{
		}

		/// <summary>
		/// initialize data tables
		/// </summary>
		public static void InitializeTables ()
		{
            if (crystInterfaceTables == null)
            {
                crystInterfaceTables = new DataTable[6];
                // for unique interfaces in a space group
                string[] interfaceCols = { "GroupSeqId", "SpaceGroup", "ASU", "PDBID", "InterfaceID", "NumOfSg", "NumOfSg_Identity", "SurfaceArea" };
                crystInterfaceTables[SgInterfaces] = new DataTable(ProtCidSettings.dataType + "SgInterfaces");

                foreach (string interfaceCol in interfaceCols)
                {
                    crystInterfaceTables[SgInterfaces].Columns.Add(new DataColumn(interfaceCol));
                }

                // residue for interfaces in a space group
                string[] residueCols = { "PdbID", "InterfaceID", "Residue1", "SeqID1", "Residue2", "SeqID2", "Distance" };
                crystInterfaceTables[SgInterfaceResidues] = new DataTable("SgInterfaceResidues");
                foreach (string residueCol in residueCols)
                {
                    crystInterfaceTables[SgInterfaceResidues].Columns.Add(new DataColumn(residueCol));
                }
                // contacts of an interface of a space group
                string[] contactCols = { "PdbID", "InterfaceID", "Residue1", "SeqID1", "Residue2", "SeqID2", "Distance" };
                crystInterfaceTables[SgInterfaceContacts] = new DataTable("SgInterfaceContacts");
                foreach (string contactCol in contactCols)
                {
                    crystInterfaceTables[SgInterfaceContacts].Columns.Add(new DataColumn(contactCol));
                }

                // comparison between unique interfaces in two space groups of a homologous family
                string[] groupColNames = { "PdbID1", "InterfaceID1", "PdbID2", "InterfaceID2", "QScore", "Identity", "IsReversed"};
                crystInterfaceTables[DifEntryInterfaceComp] = new DataTable("DifEntryInterfaceComp");
                foreach (string groupCol in groupColNames)
                {
                    crystInterfaceTables[DifEntryInterfaceComp].Columns.Add(new DataColumn(groupCol));
                }
                // comparison between unique interfaces within one space groups
                string[] sgInterfaceColNames = { "PdbID", "InterfaceID1", "InterfaceID2", "QScore", "IsReversed"};
                crystInterfaceTables[EntryInterfaceComp] = new DataTable("EntryInterfaceComp");
                foreach (string groupCol in sgInterfaceColNames)
                {
                    crystInterfaceTables[EntryInterfaceComp].Columns.Add(new DataColumn(groupCol));
                }
                // crystal interfaces for each entry in a space group
                string[] entryInterfaceCols = {"PDBID", "InterfaceID", "AsymChain1", "AsymChain2", 
								"AuthChain1", "AuthChain2", "EntityID1", "EntityID2",
								"SymmetryString1", "SymmetryString2", "FullSymmetryString1", 
								"FullSymmetryString2", "SurfaceArea"/*, "IsSymmetry"*/, "SymmetryIndex"};
                // IsSymmetry added on Feb. 6, 2017, whether an interface with same pfam architecture is in symmetry
                crystInterfaceTables[CrystEntryInterfaces] = new DataTable("CrystEntryInterfaces");
                foreach (string interfaceCol in entryInterfaceCols)
                {
                    crystInterfaceTables[CrystEntryInterfaces].Columns.Add(new DataColumn(interfaceCol));
                }
            }
		}

		/// <summary>
		/// clear the data in the data tables
		/// </summary>
		public static void ClearTables ()
		{
			foreach (DataTable dataTable in crystInterfaceTables)
			{
				dataTable.Clear ();
			}
		}

        public static void  InitializeDbTables ()
        {
            string createTableString = "";
            string createIndexString = "";
            string tableName = "";
            DbCreator dbCreator = new DbCreator();
            tableName = "CRYSTENTRYINTERFACES";
            createTableString = "CREATE TABLE CRYSTENTRYINTERFACES ( " +
                            "PDBID                CHAR(4) NOT NULL," +
                            "INTERFACEID          INTEGER NOT NULL," +
                            "ASYMCHAIN1           CHAR(4) NOT NULL," +
                            "AUTHCHAIN1           CHAR(3) NOT NULL," +
                            "ENTITYID1            INTEGER NOT NULL," +
                            "SYMMETRYSTRING1      VARCHAR(15) NOT NULL," +
                            "FULLSYMMETRYSTRING1  VARCHAR(30) NOT NULL," +
                            "ASYMCHAIN2           CHAR(4) NOT NULL," +
                            "AUTHCHAIN2           CHAR(3) NOT NULL," +
                            "ENTITYID2            INTEGER NOT NULL," +
                            "SYMMETRYSTRING2      VARCHAR(15) NOT NULL," +
                            "FULLSYMMETRYSTRING2  VARCHAR(30) NOT NULL," +
                            "SURFACEAREA          FLOAT DEFAULT -1 NOT NULL" +
                            "SymmetryIndex        FLOAT DEFAULT 0);";   // added on August 16, 2018 whether the interface with same pfam architecture is in symmetry
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = "CREATE INDEX CRYSTENTRYINTERFACES_IDX1 ON CRYSTENTRYINTERFACES (PDBID, INTERFACEID);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = "DIFENTRYINTERFACECOMP";
            createTableString = "CREATE TABLE DIFENTRYINTERFACECOMP (" +
                                "PDBID1           CHAR(4) NOT NULL," +
                                "INTERFACEID1     INTEGER NOT NULL," +
                                "PDBID2           CHAR(4) NOT NULL," +
                                "INTERFACEID2     INTEGER NOT NULL," +
                                "QSCORE           FLOAT NOT NULL," +
                                "IDENTITY         FLOAT NOT NULL, " +
                                "ISREVERSED       INTEGER NOT NULL );";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = "CREATE INDEX DIFENTRYINTERFACECOMP_IDX1 ON DIFENTRYINTERFACECOMP (PDBID1, INTERFACEID1);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            createIndexString = "CREATE INDEX DIFENTRYINTERFACECOMP_IDX2 ON DIFENTRYINTERFACECOMP (PDBID2, INTERFACEID2);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = "ENTRYINTERFACECOMP";
            createTableString = "CREATE TABLE ENTRYINTERFACECOMP (" +
                                "PDBID         CHAR(4) NOT NULL," +
                                "INTERFACEID1  INTEGER NOT NULL," +
                                "INTERFACEID2  INTEGER NOT NULL," +
                                "QSCORE        FLOAT NOT NULL," +
                                "ISREVERSED    INTEGER NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = "CREATE INDEX ENTRYINTERFACECOMP_IDX1 ON ENTRYINTERFACECOMP (PDBID);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            /*
             * Not maintain since Pfam30 since November 2016, due to the size of tables
             * */
/*
            tableName = "SGINTERFACECONTACTS";
            createTableString = "CREATE TABLE SGINTERFACECONTACTS (" +
                                "PDBID        CHAR(4) NOT NULL," +
                                "INTERFACEID  INTEGER NOT NULL," +
                                "RESIDUE1     CHAR(3) NOT NULL," +
                                "SEQID1       CHAR(5) NOT NULL," +
                                "RESIDUE2     CHAR(3) NOT NULL," +
                                "SEQID2       CHAR(5) NOT NULL," +
                                "DISTANCE     FLOAT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = "CREATE INDEX SGINTERFACECONTACTS_IDX1 ON SGINTERFACECONTACTS (PDBID, INTERFACEID);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);

            tableName = "SGINTERFACERESIDUES";
            createTableString = "CREATE TABLE SGINTERFACERESIDUES (" +
                                "PDBID        CHAR(4) NOT NULL," +
                                "INTERFACEID  INTEGER NOT NULL," +
                                "RESIDUE1     CHAR(3) NOT NULL," +
                                "SEQID1       CHAR(5) NOT NULL," +
                                "RESIDUE2     CHAR(3) NOT NULL," +
                                "SEQID2       CHAR(5) NOT NULL," +
                                "DISTANCE     FLOAT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIndexString = "CREATE INDEX SGINTERFACERESIDUES_IDX1 ON SGINTERFACERESIDUES (PDBID, INTERFACEID);";
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);*/
        }
	}
}
