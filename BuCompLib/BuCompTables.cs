using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace BuCompLib
{
    public class BuCompTables
    {
        public static DataTable[] buCompTables = null;
        public const int BuInterfaces = 0;
        public const int BuSameInterfaces = 1;
        public const int BuDomainInterfaces = 2;
        public const int BuDomainRelation = 3;
        public const int EntryBuInterfaceComp = 4;
        public const int EntryBuComp = 5;

        public static DataTable intraChainDomainInterfaceTable = null;

        public BuCompTables()
        {
        }

        public static void InitializeTables()
        {
            if (BuCompBuilder.BuType == "asu")
            {
                buCompTables = new DataTable[4];
            }
            else
            {
                buCompTables = new DataTable[6];
            }
            string tableName = "";
            // PDB and PQS unique interfaces tables
            string[] uniqueInterfaceColumns = {"PdbID", "BuID", "InterfaceID", "AsymChain1", "AsymChain2", 
									"AuthChain1", "AuthChain2", "EntityID1", "EntityID2", "SurfaceArea", "NumOfCopy"};
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "Interfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "BuInterfaces";
            }
            buCompTables[BuInterfaces] = new DataTable(tableName);
            foreach (string interfaceCol in uniqueInterfaceColumns)
            {
                buCompTables[BuInterfaces].Columns.Add(new DataColumn(interfaceCol));
            }           

            // PDB and PQS BuSameInterfaces
            string[] sameInterfaceColumns = {"PdbID", "BuID", "InterfaceID", "SameInterfaceID", "Chain1", "SymmetryString1", 
												"Chain2", "SymmetryString2", "QScore"};
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "SameInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "BuSameInterfaces";
            }
            buCompTables[BuSameInterfaces] = new DataTable(tableName);
            foreach (string sameInterfaceCol in sameInterfaceColumns)
            {
                buCompTables[BuSameInterfaces].Columns.Add(new DataColumn(sameInterfaceCol));
            }

            if (BuCompBuilder.BuType != "asu")
            {
                string[] buInterfaceCompColumns = { "PdbID", "BuID1", "InterfaceID1", "BuID2", "InterfaceID2", "Qscore" };
                buCompTables[EntryBuInterfaceComp] = new DataTable(BuCompBuilder.BuType + "EntryBuInterfaceComp");
                foreach (string buCompCol in buInterfaceCompColumns)
                {
                    buCompTables[EntryBuInterfaceComp].Columns.Add(new DataColumn(buCompCol));
                }

                string[] buCompColumns = {"PdbID", "BuID1", "BuID2", "EntityFormat1", "EntityFormat2", 
                                         "NumOfInterfaces1", "NumOfInterfaces2", "SameBUs"};
                buCompTables[EntryBuComp] = new DataTable(BuCompBuilder.BuType + "EntryBuComp");
                foreach (string buCompCol in buCompColumns)
                {
                    buCompTables[EntryBuComp].Columns.Add(new DataColumn(buCompCol));
                }
            }
            string[] buDomainInterfaceColumns = {"RelSeqID", "PdbID", "BuID", "InterfaceID", "DomainInterfaceID", 
                                            "DomainID1", "DomainID2", "IsReversed"};
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }
            buCompTables[BuDomainInterfaces] = new DataTable(tableName);
            foreach (string domainInterfaceCol in buDomainInterfaceColumns)
            {
                buCompTables[BuDomainInterfaces].Columns.Add(new DataColumn (domainInterfaceCol));
            }

            string[] buDomainRelationColumns = {"RelSeqID", "FamilyCode1", "FamilyCode2"};
            buCompTables[BuDomainRelation] = new DataTable("PfamRelations");
            foreach (string relCol in buDomainInterfaceColumns)
            {
                buCompTables[BuDomainRelation].Columns.Add(new DataColumn (relCol));
            }

            if (BuCompBuilder.BuType == "asu")
            {
                string[] intraChainDomainColumns = { "RelSeqID", "PdbID", "AsymChain", "DomainInterfaceID", "DomainID1", "DomainID2", "IsReversed" };
                intraChainDomainInterfaceTable = new DataTable("AsuIntraDomainInterfaces");
                foreach (string domainInterfaceCol in intraChainDomainColumns)
                {
                    intraChainDomainInterfaceTable.Columns.Add(new DataColumn(domainInterfaceCol));
                }
            }
        }

        /// <summary>
        /// clear the data in the tables
        /// </summary>
        public static void ClearTables()
        {
            foreach (DataTable dataTable in buCompTables)
            {
                dataTable.Clear();
            }

            if (BuCompBuilder.BuType == "asu")
            {
                intraChainDomainInterfaceTable.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void InitializeDbTables()
        {
            DbCreator dbCreator = new DbCreator();
            string tableName = "";
            string createTableString = "";
            string createIndexString = "";

            foreach (string buType in BuCompBuilder.buTypes)
            {
                if (buType == "asu")
                {
                    tableName = buType + "Interfaces";
                }
                else
                {
                    tableName = buType + "BuInterfaces";
                }

                createTableString = "Create Table " + tableName + "( " +
                                    "PdbID CHAR(4) NOT NULL, " +
                                    "BuID VARCHAR(8) NOT NULL, " +
                                    "InterfaceID INTEGER NOT NULL, " +
                                    "AsymChain1 CHAR(3) NOT NULL, " +
                                    "AsymChain2 CHAR(3) NOT NULL, " +
                                    "AuthChain1 CHAR(3) NOT NULL, " +
                                    "AuthChain2 CHAR(3) NOT NULL, " +
                                    "EntityID1 INTEGER NOT NULL, " +
                                    "EntityID2 INTEGER NOT NULL, " +
                                    "SurfaceArea FLOAT NOT NULL, " +
                                    "NumOfCopy INTEGER NOT NULL, " +
                                    "PRIMARY KEY (PdbID, BuID, InterfaceID))";

                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);

                if (buType == "asu")
                {
                    tableName = buType + "SameInterfaces";
                }
                else
                {
                    tableName = buType + "BuSameInterfaces";
                }
                createTableString = "CREATE TABLE " + tableName + "( " +
                                   "PdbID CHAR(4) NOT NULL, " +
                                   "BuID VARCHAR(8) NOT NULL, " +
                                   "InterfaceID INTEGER NOT NULL, " +
                                   "SameInterfaceID INTEGER NOT NULL, " +
                                   "Chain1 CHAR(3) NOT NULL, " +
                                   "SymmetryString1 VARCHAR(50) NOT NULL, " +
                                   "Chain2 CHAR(3) NOT NULL, " +
                                   "SymmetryString2 VARCHAR(50) NOT NULL, " +
                                   "QScore FLOAT NOT NULL);";
                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
                createIndexString = string.Format("CREATE INDEX {0}_idx1 ON {0} (PdbID, BuID, InterfaceID);", tableName);
                dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);

                if (buType != "asu")
                {
                    tableName = buType + "EntryBuInterfaceComp";
                    createTableString = "CREATE TABLE " + tableName + " ( " +
                        "PdbID CHAR(4) NOT NULL, " +
                        "BuID1 VARCHAR(8) NOT NULL, " +
                        "InterfaceID1 INTEGER NOT NULL, " +
                        "BuID2 VARCHAR(8) NOT NULL, " +
                        "InterfaceID2 INTEGER NOT NULL, " +
                        "QScore FLOAT NOT NULL);";
                    dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
                    createIndexString = string.Format("CREATE INDEX {0}BuInterfaceComp_idx1 " +
                        " ON {0}EntryBuInterfaceComp (PdbID, BuID1, BuID2);", buType);
                    dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);

                    tableName = buType + "EntryBuComp";
                    createTableString = "CREATE TABLE " + tableName + " ( " +
                        "PdbID CHAR(4) NOT NULL, " +
                        "BUID1 VARCHAR(8) NOT NULL, " +
                        "BUID2 VARCHAR(8) NOT NULL, " +
                        "EntityFormat1 VARCHAR(150) NOT NULL, " +
                        "EntityFormat2 VARCHAR(150) NOT NULL, " +
                        "NumOfInterfaces1 INTEGER NOT NULL, " +
                        "NumOfInterfaces2 INTEGER NOT NULL, " +
                        "SameBUs CHAR(1) NOT NULL);";
                    dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
                    createIndexString = string.Format("CREATE INDEX {0}BuComp_Idx1 " +
                        " ON {0}EntryBuComp (PdbID, BuID1, BuID2);", buType);
                    dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void InitializeDomainDbTables()
        {
            DbCreator dbCreator = new DbCreator();
            string tableName = "";
            string createTableString = "";
            string createIndexString = "";        

            #region domain interfaces data
            foreach (string buType in BuCompBuilder.buTypes)
            {
                if (buType == "asu")
                {
                    tableName = buType + "PfamDomainInterfaces";
                }
                else
                {
                    tableName = buType + "PfamBuDomainInterfaces";
                }
                createTableString = "CREATE TABLE " + tableName + " ( " +
                    "RelSeqID INTEGER NOT NULL, " +
                    "PdbID CHAR(4) NOT NULL, " +
                    "BUID VARCHAR(8) NOT NULL, " +
                    "InterfaceID INTEGER NOT NULL, " +
                    "DomainInterfaceID INTEGER NOT NULL, " +
                    "DomainID1 INTEGER NOT NULL, " +
                    "DomainID2 INTEGER NOT NULL, " +
                    "IsReversed CHAR(1) NOT NULL );";
                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
                createIndexString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (PdbID, BuID);", tableName);
                dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);

                if (buType == "asu")
                {
                    tableName = "AsuIntraDomainInterfaces";
                    createTableString = "CREATE TABLE AsuIntraDomainInterfaces ( " +
                        "RelSeqID INTEGER NOT NULL, " +
                        "PdbID CHAR(4) NOT NULL, " +
                        "AsymChain CHAR(3) NOT NULL, " +
                        "DomainInterfaceID INTEGER NOT NULL, " +
                        "DomainID1 INTEGER NOT NULL, " +
                        "DomainID2 INTEGER NOT NULL, " +
                        "IsReversed CHAR(1) NOT NULL);";
                    dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
                    createIndexString = string.Format("CREATE INDEX AsuIntraDInterfaces_Idx1 ON {0} (PdbID, AsymChain);", tableName);
                    dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);
                }
            }

            tableName = "PfamRelations";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                "RelSeqID INTEGER NOT NULL, " +
                "FamilyCode1 VARCHAR(15) NOT NULL, " +
                "FamilyCode2 VARCHAR(15) NOT NULL );";
            dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);
            createIndexString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (RelSeqID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, tableName);
            #endregion
        }
    }
}
