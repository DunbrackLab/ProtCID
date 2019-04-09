using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDWebDataLib
{
    public class PfamData
    {
        public PfamData ()
        {
            ProtCidSettings.LoadDirSettings();
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect();
                ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.pdbfamDbPath;
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.protcidDbPath;
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            }
        }
        #region clan info
        /// <summary>
        /// 
        /// </summary>
        public void GenerateClansInPdbTable ()
        {
            DbInsert dbInsert = new DbInsert();
            DataTable pfamClansInPdbTable = CreateTable(false);
            string queryString = "Select Distinct Clan_Acc From PfamInPdb, PfamClanFamily Where PfamInPdb.PfamAcc = PfamClanFamily.Pfam_Acc;";
            DataTable clansTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string clanAcc = "";
            int numPfams = 0;
            int numPfamsPdb = 0;
            int numPfamsPeptide = 0;
            int numPfamsDnaRna = 0;
            int numEntries = 0;
            int numUniProts = 0;
            string[] clanPfamsPdb = null;
            foreach (DataRow clanRow in clansTable.Rows)
            {
                clanAcc = clanRow["Clan_Acc"].ToString().TrimEnd();
                DataRow clanInfoRow = pfamClansInPdbTable.NewRow();
                clanInfoRow["ClanAcc"] = clanAcc;
                numPfams = GetClanNumPfams(clanAcc);
                numPfamsPdb = GetClanNumPfamsInPdb(clanAcc, out clanPfamsPdb);
                numPfamsPeptide = GetNumPfamsWithPeptides(clanPfamsPdb);
                numPfamsDnaRna = GetNumPfamsWithDnaRna(clanPfamsPdb);
                numEntries = GetClanNumEntries(clanAcc);
                numUniProts = GetClanNumUniProtsInPdb(clanAcc);
                clanInfoRow["NumPfams"] = numPfams;
                clanInfoRow["NumPfamsPdb"] = numPfamsPdb;
                clanInfoRow["NumPfamsPeptide"] = numPfamsPeptide;
                clanInfoRow["NumPfamsDnaRna"] = numPfamsDnaRna;
                clanInfoRow["NumEntries"] = numEntries;
                clanInfoRow["NumUniProts"] = numUniProts;
                pfamClansInPdbTable.Rows.Add(clanInfoRow);
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.pdbfamDbConnection, pfamClansInPdbTable);
        }

        /// <summary>
        /// the number of pfams of the input clan  defined by Pfam
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private int GetClanNumPfams (string clanAcc)
        {
            string queryString = string.Format("Select Distinct Pfam_Acc From PfamClanFamily Where Clan_Acc = '{0}';", clanAcc);
            DataTable clanNumPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return clanNumPfamTable.Rows.Count;
        }


        /// <summary>
        /// the number of Pfams of the input clan in PDB
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private int GetClanNumPfamsInPdb (string clanAcc, out string[] clanPfamIDs)
        {
            string queryString = string.Format("Select Distinct PdbPfam.Pfam_Acc, Pfam_ID From PfamClanFamily, PdbPfam " + 
                " Where Clan_Acc = '{0}' AND PdbPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
            DataTable clanPfamPdbTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> clanPfamIdList = new List<string>();
            foreach (DataRow pfamRow in clanPfamPdbTable.Rows)
            {
                clanPfamIdList.Add(pfamRow["Pfam_ID"].ToString().TrimEnd());
            }
            clanPfamIDs = clanPfamIdList.ToArray();
            return clanPfamPdbTable.Rows.Count;
        }

        /// <summary>
        /// the number of Pfams of the input clan in PDB
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private int GetClanNumPfamsInPdb(string clanAcc)
        {
            string queryString = string.Format("Select Distinct PdbPfam.Pfam_Acc From PfamClanFamily, PdbPfam " +
                " Where Clan_Acc = '{0}' AND PdbPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
            DataTable clanPfamPdbTable = ProtCidSettings.pdbfamQuery.Query(queryString);           
            return clanPfamPdbTable.Rows.Count;
        }
        /// <summary>
        /// the Pfams in the input clan
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private string[] GetClanPfamIDs (string clanAcc)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PfamClanFamily, PdbPfam " +
                " Where Clan_Acc = '{0}' AND PdbPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
            DataTable clanPfamPdbTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> clanPfamIdList = new List<string>();
            foreach (DataRow pfamRow in clanPfamPdbTable.Rows)
            {
                clanPfamIdList.Add(pfamRow["Pfam_ID"].ToString().TrimEnd());
            }
            return clanPfamIdList.ToArray();
        }

        /// <summary>
        /// the number of Pfams interacting with peptides in the list of input Pfams
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private int GetNumPfamsWithPeptides (string[] pfamIds)
        {
            string queryString = "";
            DataTable pfamWithPeptidesTable = null;
            for (int i = 0; i < pfamIds.Length; i += 100)
            {
                string[] subPfamIds = ParseHelper.GetSubArray(pfamIds, i, 100);
                queryString = string.Format("Select Distinct PfamID From PfamPeptideInterfaces Where PfamID In ({0});", ParseHelper.FormatSqlListString (subPfamIds));
                DataTable subPeptidePfamsTable = ProtCidSettings.protcidQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subPeptidePfamsTable, ref pfamWithPeptidesTable);
            }

            return pfamWithPeptidesTable.Rows.Count;
        }

        /// <summary>
        /// the number of Pfams interacting with DNA/RNA in the input clan
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private int GetNumPfamsWithDnaRna (string[] pfamIds)
        {
            string queryString = "";
            DataTable pfamWithDnaRnaTable = null;
            for (int i = 0; i < pfamIds.Length; i += 100)
            {
                string[] subPfams = ParseHelper.GetSubArray(pfamIds, i, 100);
                queryString = string.Format("Select Distinct PfamID From PfamDnaRnas Where PfamID IN ({0});", ParseHelper.FormatSqlListString (subPfams));
                DataTable subDnaRnaPfamsTable = ProtCidSettings.protcidQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subDnaRnaPfamsTable, ref pfamWithDnaRnaTable);
            }
            return pfamWithDnaRnaTable.Rows.Count;
        }

        /// <summary>
        /// the number of PDB entries in the input clan
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private int GetClanNumEntries (string clanAcc)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam, PfamClanFamily Where Clan_Acc = '{0}' AND " + 
                " PdbPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
            DataTable clanEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return clanEntryTable.Rows.Count;
        }

        /// <summary>
        /// the uniprots in PDB in the input clan
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private int GetClanNumUniProtsInPdb (string clanAcc)
        {
            string queryString = string.Format("Select Distinct UnpAccession From UnpPfam, PfamClanFamily Where Clan_Acc = '{0}' AND " +
                "UnpPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
            DataTable nonHumanUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            
            queryString = string.Format("Select Distinct UnpAccession From HumanPfam, PfamClanFamily, PdbDbRefSifts " + 
                " Where Clan_Acc = '{0}' AND HumanPfam.Pfam_Acc = PfamClanFamily.Pfam_Acc AND " + 
                " HumanPfam.UnpAccession = PdbDbRefSifts.DbAccession;", clanAcc);
            DataTable humanUnpInPdbTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            return nonHumanUnpTable.Rows.Count + humanUnpInPdbTable.Rows.Count;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private DataTable CreateTable (bool isUpdate)
        {
            string tableName = "PfamClansInPDB";
            string[] clanColumns = {"ClanAcc", "NumPfams", "NumPfamsPdb", "NumPfamsPeptide", "NumPfamsDnaRna", "NumEntries", "NumUniprots"};
            DataTable pfamClansPdbTable = new DataTable(tableName);
            foreach (string col in clanColumns)
            {
                pfamClansPdbTable.Columns.Add(new DataColumn (col));
            }

            if (!isUpdate)
            {
                string createTableString = "Create Table " + tableName + " ( " +
                    " ClanAcc              VARCHAR(10) NOT NULL, " +
                    " NumPfams             Integer NOT NULL, " +
                    " NumPfamsPdb          Integer NOT NULL, " +
                    " NumPfamsPeptide      Integer NOT NULL, " +
                    " NumPfamsDnaRna       Integer NOT NULL, " +
                    " NumEntries           Integer NOT NULL, " +
                    " NumUniProts          Integer NOT NULL " +
                    ");";
                DbCreator dbCreator = new DbCreator();
                dbCreator.CreateTableFromString(ProtCidSettings.pdbfamDbConnection, createTableString, "PfamClansInPDB");
            }
            return pfamClansPdbTable;
        }
        #endregion
    }
}
