using System;
using System.Data;
using DbLib;

namespace DataCollectorLib.FatcatAlignment
{
	/// <summary>
	/// Summary description for FatcatTables.
	/// </summary>
	public class FatcatTables
	{
		public static DataTable fatcatAlignTable = null;
	//	public static string tableName = "FatCatAlignments";
		public FatcatTables()
		{
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbTableName"></param>
        public static void InitializeTables(string dbTableName)
        {
            fatcatAlignTable = new DataTable(dbTableName);
            string[] alignCols = null;
            if (dbTableName.ToLower().IndexOf("pfam") > -1)
            {
               string[] fatcatCols = {"QueryEntry", "QueryDomainID", "QueryLength", "HitEntry", "HitDomainID", "HitLength",
								"QueryEntity", "HitEntity", "QueryDomainStart", "HitDomainStart",  
								"Align_Len", "E_Value", "Identity", "Score", "Gaps", "Similarity",
								"AlignmentLength", "QuerySequence", "HitSequence", "QueryStart", "QueryEnd", 
								"HitStart", "HitEnd"};
               alignCols = fatcatCols;
            }
            else
            {
                string[] fatcatCols = {"QueryEntry", "QueryChain", "QueryLength", 
								"HitEntry", "HitChain", "HitLength", 
								"Align_Len", "E_Value", "Identity", "Score", "Gaps", "Similarity",
								"AlignmentLength", "QuerySequence", "HitSequence", "QueryStart", "QueryEnd", 
								"HitStart", "HitEnd", "QueryAsymChain", "HitAsymChain"};
                alignCols = fatcatCols;
            }
            foreach (string col in alignCols)
            {
                fatcatAlignTable.Columns.Add(new DataColumn(col));
            }
            if (dbTableName.ToLower().IndexOf("pfamdomainalignments") > -1)
            {
                fatcatAlignTable.Columns.Add(new DataColumn ("QuerySeqNumbers"));
                fatcatAlignTable.Columns.Add(new DataColumn ("HitSeqNumbers"));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbConnect"></param>
        /// <param name="dbTableName"></param>
        public static void InitializeDbTable(DbConnect dbConnect, string dbTableName)
        {
            string createTableString = "";
            string createIndexString = "";
            DbCreator dbCreator = new DbCreator();
            string pfamAlignType = "";
            if (dbTableName.ToLower().IndexOf("pfam") > -1)
            {
                if (dbTableName.ToLower().IndexOf("pfamweak") > -1)
                {
                    pfamAlignType = "PfamDomainWeak";
                    if (dbTableName.ToLower().IndexOf("rigid") > -1)
                    {
                        pfamAlignType = "PfamDomainWeakRigid";
                    }
                }
                else
                {
                    pfamAlignType = "PfamDomain";
                    if (dbTableName.ToLower().IndexOf("rigid") > -1)
                    {
                        pfamAlignType = "PfamDomainRigid";
                    }
                }
                createTableString = string.Format("CREATE TABLE {0} ( " +
                    " QueryEntry CHAR(4) NOT NULL, QueryEntity INTEGER NOT NULL, " + 
                    " QueryDomainID BIGINT NOT NULL, " +
                    " QueryDomainStart INTEGER NOT NULL, QueryLength INTEGER NOT NULL, " +
                    " HitEntry CHAR(4) NOT NULL, HitEntity INTEGER NOT NULL, " +
                    " HitDOmainID BIGINT NOT NULL, " +
                    " HitDomainStart INTEGER NOT NULL, HitLength INTEGER NOT NULL, " +
                    " Align_Len INTEGER NOT NULL, Score FLOAT NOT NULL, Gaps INTEGER NOT NULL, " +
                    " E_Value DOUBLE PRECISION NOT NULL, Identity FLOAT NOT NULL, Similarity FLOAT NOT NULL, " +
                    " AlignmentLength INTEGER NOT NULL, " +
                    " QuerySequence BLOB Sub_Type TEXT NOT NULL, HitSequence BLOB Sub_Type TEXT NOT NULL, ", 
                dbTableName);
                if (dbTableName.ToLower ().IndexOf ("weak") < 0)
                {
                    createTableString = createTableString + 
                        " QuerySeqNumbers BLOB Sub_Type TEXT NOT NULL, HitSeqNumbers BLOB Sub_Type TEXT NOT NULL, ";
                }
                createTableString = createTableString + 
                    " QueryStart INTEGER NOT NULL, QueryEnd INTEGER NOT NULL, " +
                    " HitStart INTEGER NOT NULL, HitEnd INTEGER NOT NULL);";

                dbCreator.CreateTableFromString(dbConnect, createTableString, dbTableName);

                createIndexString = string.Format("CREATE INDEX {0}_Idx1 ON {1} (QueryEntry, QueryEntity);", pfamAlignType, dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);

                createIndexString = string.Format("CREATE INDEX {0}_Idx2 ON {1} (HitEntry, HitEntity);", pfamAlignType, dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);

                createIndexString = string.Format("CREATE INDEX {0}_Idx3 ON {1} (QueryEntry, QueryDomainID);", pfamAlignType, dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);

                createIndexString = string.Format("CREATE INDEX {0}_Idx4 ON {1} (HitEntry, HitDomainID);", pfamAlignType, dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);
            }
            else
            {
                createTableString = string.Format("CREATE TABLE {0} ( " +
                    " QueryEntry CHAR(4) NOT NULL, QueryChain CHAR(3) NOT NULL, QueryLength INTEGER NOT NULL, " +
                    " HitEntry CHAR(4) NOT NULL, HitChain CHAR(3) NOT NULL, HitLength INTEGER NOT NULL, " +
                    " Align_Len INTEGER NOT NULL, Score FLOAT NOT NULL, Gaps INTEGER NOT NULL, " +
                    " E_Value DOUBLE PRECISION NOT NULL, Identity FLOAT NOT NULL, Similarity FLOAT NOT NULL, " +
                    " AlignmentLength INTEGER NOT NULL, " +
                    " QuerySequence BLOB Sub_Type TEXT NOT NULL, HitSequence BLOB Sub_Type TEXT NOT NULL, " +
                    " QueryStart INTEGER NOT NULL, QueryEnd INTEGER NOT NULL, " +
                    " HitStart INTEGER NOT NULL, HitEnd INTEGER NOT NULL, " + 
                    " QueryAsymChain CHAR(3), HitAsymChain CHAR(3));",
                    dbTableName);

                dbCreator.CreateTableFromString(dbConnect, createTableString, dbTableName);

                createIndexString = string.Format("CREATE INDEX FatcatAlign_Idx1 ON {0} (QueryEntry, QueryChain);", dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);

                createIndexString = string.Format("CREATE INDEX FatcatAlign_Idx2 ON {0} (HitEntry, HitChain);", dbTableName);
                dbCreator.CreateIndex(dbConnect, createIndexString, dbTableName);
            }
        }
	}
}
