using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;

namespace DataCollectorLib.Pisces
{
    public class PiscesDataTables
    {
        #region member variables
        public static DataTable[] piscesDataTables = new DataTable[1];
        public static int RedundantPdbChains = 0;
    //    public static int PsiBlastHit = 1;
    //    public static int CeHit = 2;
        #endregion

        /// <summary>
        /// set the datatable structure
        /// </summary>
        public static void InitializeTables ()
        {
            string[] pdbaanrColumns = { "PdbID1", "ChainID1", "AsymChainID1", "EntityID1", 
                                          "PdbID2", "ChainID2", "AsymChainID2", "EntityID2"};
  /*          string[] psiblastHitColumns = {"QueryEntry", "QueryChain", "HitEntry", "HitChain",  
                                 "QueryLength", "HitLength", "Identity", "AlignmentLength", "E_value", 
                                 "QueryStart", "QueryEnd", "HitStart", "HitEnd", "QuerySequence", "HitSequence", 
                                          "QueryAsymChain", "HitAsymChain"};
            string[] ceHitColumns = {"QueryEntry", "QueryChain",  "HitEntry", "HitChain", 
                                 "QueryLength", "HitLength", "Identity", "AlignmentLength", "E_value", 
                                 "QueryStart", "QueryEnd", "HitStart", "HitEnd", "QuerySequence", "HitSequence", 
                                    "QueryAsymChain", "HitAsymChain"};
          */
            // create data tables
            piscesDataTables[RedundantPdbChains] = new DataTable("RedundantPdbChains");
            foreach (string columnName in pdbaanrColumns)
            {
                piscesDataTables[RedundantPdbChains].Columns.Add(new DataColumn(columnName));
            }
 /*           piscesDataTables[PsiBlastHit] = new DataTable("PsiBlastAlignments");
            foreach (string columnName in psiblastHitColumns)
            {
                piscesDataTables[PsiBlastHit].Columns.Add(new DataColumn(columnName));
            }
            piscesDataTables[CeHit] = new DataTable("CeAlignments");
            foreach (string columnName in ceHitColumns)
            {
                piscesDataTables[CeHit].Columns.Add(new DataColumn(columnName));
            }*/
        }

        /// <summary>
        /// create table structures in the database
        /// </summary>
        public static void InitializeDbTables(DbConnect alignmentDbConnect)
        {
            if (!alignmentDbConnect.IsConnected ())
            {
                alignmentDbConnect.ConnectToDatabase();
            }
            DbCreator dbCreator = new DbCreator();
     /*       string createTableString = "CREATE TABLE PsiBlastAlignments ( " +
                                      "QueryEntry CHAR(4) NOT NULL," +
                                      "QueryChain CHAR(3) NOT NULL," +
                                      "QueryAsymChain CHAR(3)," +
                                      "HitEntry CHAR(4) NOT NULL," +
                                      "HitChain CHAR(3) NOT NULL," +
                                      "HitAsymChain CHAR(3)," +
                                      "QueryLength INTEGER  NOT NULL," +
                                      "HitLength INTEGER  NOT NULL," +
                                      "Identity FLOAT NOT NULL," +
                                      "AlignmentLength INTEGER  NOT NULL," +
                                      "E_value DOUBLE PRECISION NOT NULL," +
                                      "QueryStart INTEGER  NOT NULL," +
                                      "QueryEnd INTEGER  NOT NULL," +
                                      "HitStart INTEGER  NOT NULL," +
                                      "HitEnd INTEGER  NOT NULL," +
                                      "QuerySequence BLOB SUB_TYPE TEXT NOT NULL," +
                                      "HitSequence BLOB SUB_TYPE TEXT NOT NULL);";
            dbCreator.CreateTableFromString(alignmentDbConnect, createTableString, "PsiBlastAlignments");
            string createIdxString = "CREATE INDEX PsiblastAlign_queryindex ON PsiBlastAlignments(QueryEntry, QueryChain);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "PsiBlastAlignments");
            createIdxString = "CREATE INDEX PsiblastAlign_Hitindex ON PsiBlastAlignments(HitEntry, HitChain);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "PsiBlastAlignments");

            createTableString = "CREATE TABLE CeAlignments ( " +
                                      "QueryEntry CHAR(4) NOT NULL," +
                                      "QueryChain CHAR(3) NOT NULL," +
                                      "QueryAsymChain CHAR(3)," +
                                      "HitEntry CHAR(4) NOT NULL," +
                                      "HitChain CHAR(3) NOT NULL," +
                                      "HitAsymChain CHAR(3)," +
                                      "QueryLength INTEGER  NOT NULL," +
                                      "HitLength INTEGER  NOT NULL," +
                                      "Identity FLOAT NOT NULL," +
                                      "AlignmentLength INTEGER  NOT NULL," +
                                      "E_value DOUBLE PRECISION NOT NULL," +
                                      "QueryStart INTEGER  NOT NULL," +
                                      "QueryEnd INTEGER  NOT NULL," +
                                      "HitStart INTEGER  NOT NULL," +
                                      "HitEnd INTEGER  NOT NULL," +
                                      "QuerySequence BLOB SUB_TYPE TEXT NOT NULL," +
                                      "HitSequence BLOB SUB_TYPE TEXT NOT NULL);";
            dbCreator.CreateTableFromString(alignmentDbConnect, createTableString, "CeAlignments");
            createIdxString = "CREATE INDEX CeAlign_queryindex ON CeAlignments(QueryEntry, QueryChain);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "CeAlignments");
            createIdxString = "CREATE INDEX CeAlign_Hitindex ON CeAlignments(HitEntry, HitChain);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "CeAlignments");
            */
            string createTableString = "CREATE TABLE RedundantPdbChains ( " +
                                      "PdbID1 CHAR(4) NOT NULL," +
                                      "ChainID1 CHAR(3) NOT NULL," +
                                      "AsymChainID1 CHAR(3)," +
                                      "EntityID1 INTEGER, " +
                                      "PdbID2 CHAR(4) NOT NULL," +
                                      "ChainID2 CHAR(3) NOT NULL, " + 
                                      "AsymChainID2 CHAR(3), " + 
                                      "EntityID2 INTEGER);";
            dbCreator.CreateTableFromString(alignmentDbConnect, createTableString, "RedundantPdbChains");
            string createIdxString = "CREATE INDEX RedundantPdbChains_indexPdbid1 ON RedundantPdbChains(PdbID1);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "RedundantPdbChains");
            createIdxString = "CREATE INDEX RedundantPdbChains_indexPdbid2 ON RedundantPdbChains(PdbID2);";
            dbCreator.CreateIndex(alignmentDbConnect, createIdxString, "RedundantPdbChains");
        }
    }
}
