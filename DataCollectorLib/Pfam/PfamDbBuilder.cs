using System;
using System.IO;
using System.Data;
using System.Net;
using System.Collections.Generic;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.Pfam
{
	/// <summary>
	/// Summary description for PfamDbBuilder.
	/// </summary>
	public class PfamDbBuilder
	{
        private DbConnect mysqlConnect = new DbConnect ();
		private DbQuery dbQuery = new DbQuery ();
        private string pfamDataDir = @"E:\Qifang\DbProjectData\pfam\Pfam_database";

		public PfamDbBuilder()
		{
		}

		public void BuildPfamDatabase ()
		{
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }

           DownloadPfamFiles();

           string connectString = "Driver={MySQL ODBC 5.1 Driver}; Server=localhost;Database=pfam;UID=root;PWD=learnsql2011;";
           mysqlConnect.ConnectString = connectString;
           mysqlConnect.ConnectToDatabase();

       //     OutputPdbPfamFiles();

			string tempDir = "C:\\temp";
			if (! Directory.Exists (tempDir))
			{
				Directory.CreateDirectory (tempDir);
			}
            string[] sqlFiles = Directory.GetFiles(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfam_database"), "*sql.gz");
			string loadDataString = "";
			string unzippedFile = "";
			string unixFilePath = "";
			string tableName = "";
            string[] createTableStrings = null;
            foreach (string sqlFile in sqlFiles)
            {
                unzippedFile = ParseHelper.UnZipFile(sqlFile, tempDir);

                createTableStrings = ReadCreatTableString(unzippedFile);
                foreach (string createTableString in createTableStrings)
                {
                    dbQuery.Query(mysqlConnect, createTableString);
                }
            }
           
            string[] pfamFiles = Directory.GetFiles(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "pfam_database"), "*txt.gz");
			foreach (string pfamFile in pfamFiles)
			{
				unzippedFile = ParseHelper.UnZipFile (pfamFile, tempDir);
				tableName = GetTableName (pfamFile);
				unixFilePath = unzippedFile.Replace ("\\", "/");
                loadDataString = "Load data local infile '" + unixFilePath + "' into table " + tableName;
				//	" fields enclosed by '\t';";
             //   int fieldIndex = loadDataString.IndexOf("fields enclosed by \'");
               // loadDataString = loadDataString.Insert (fieldIndex + "fields enclosed by \'".Length, "\\");
                dbQuery.Query(mysqlConnect, loadDataString);
			}
			Directory.Delete (tempDir, true);
        
            OutputPdbPfamFiles();
		}

		private string[] ReadCreatTableString (string sqlFile)
		{
			string[] createTableStrings = new string[2];
            string createTableString = "";
			StreamReader dataReader = new StreamReader (sqlFile);
			string line = "";
			while ((line = dataReader.ReadLine ()) != null)
			{
				if (line == "")
				{
					continue;
				}
                // to drop the existing table
                if (line.IndexOf("DROP TABLE") > -1)
                {
                    createTableStrings[0] = line;
                    continue;
                }
				if (line.Substring (0, 2) == "--")
				{
					continue;
				}
				if (line.Substring (0, 2) == "/*")
				{
					continue;
				}
                // ignore all constraints when create tables
                if (line.IndexOf("CONSTRAINT") > -1)
                {
                    createTableString = createTableString.TrimEnd(',');
                    continue;
                }
                // create a new table
                createTableString += line;
			}
			dataReader.Close ();
            createTableStrings[1] = createTableString;
            return createTableStrings;
		}

		private string GetTableName (string pfamFile)
		{
			int extIdx = pfamFile.IndexOf (".txt");
			int fileIdx = pfamFile.LastIndexOf  ("\\");
			string tableName = pfamFile.Substring (fileIdx + 1, extIdx - fileIdx - 1);
			return tableName;
		}

		public void DownloadPfamFiles ()
		{
			WebClient webClient = new WebClient ();
            string pfamServerPath = "ftp://ftp.sanger.ac.uk/pub/databases/Pfam/current_release/database_files/";
            Directory.SetCurrentDirectory(pfamDataDir);	
			string[] pfamFilesToBeDownloaded = {"clans.sql.gz", "clans.txt.gz", "pdb.sql.gz", "pdb.txt.gz", 
			"pdb_pfamA_reg.sql.gz", "pdb_pfamA_reg.txt.gz", "pdb_pfamB_reg.sql.gz", "pdb_pfamB_reg.txt.gz", 
			"pfamA.sql.gz", "pfamA.txt.gz", "pfamB.sql.gz", "pfamB.txt.gz", "pfamseq.sql.gz", "pfamseq.txt.gz", 
            "pdb_residue_data.sql.gz", "pdb_residue_data.txt.gz", "pfamA_reg_full_significant.sql.gz", "pfamA_reg_full_significant.txt.gz"};	
			foreach (string pfamFile in pfamFilesToBeDownloaded)
			{
				webClient.DownloadFile (pfamServerPath + pfamFile, pfamFile);
			}
		}

		public void OutputPdbPfamFiles  ()
		{
            if (!mysqlConnect.IsConnected())
			{
				string connectString = "Driver={MySQL ODBC 5.1 Driver}; Server=localhost;Database=pfam;UID=root;PWD=;";
                mysqlConnect.ConnectString = connectString;
                mysqlConnect.ConnectToDatabase();
			}
/*			string queryString = "Select Pdb_ID, Chain, PfamA_Acc, PfamA_ID, PfamA.Description, " + 
				" PfamSeq_Acc, PfamSeq_ID, Pdb_res_start, pdb_res_end, seq_start, seq_end " + 
				" Into outfile 'E:/DbProjectData/PFam/pdb_pfama.txt' " + 
				" From Pdb, pdb_pfama_reg, pfama, pfamSeq " + 
				" Where Pdb.Auto_pdb = pdb_pfama_reg.auto_pdb AND " + 
				" pdb_pfama_reg.auto_pfama = pfama.auto_pfama AND " + 
				" pdb_pfama_reg.auto_pfamseq = pfamseq.auto_pfamseq;";*/
            // for PFAM24
            string queryString = "Select Pdb_ID, Chain, PfamA_Acc, PfamA_ID, PfamA.Description, " +
                " PfamSeq_Acc, PfamSeq_ID, Pdb_res_start, pdb_res_end, pdb_pfama_reg.seq_start, pdb_pfama_reg.seq_end, " +
                " ali_start, ali_end, model_start, model_end, domain_bits_score, domain_evalue_score, " +
                " sequence_bits_score, sequence_evalue_score" +
                " Into outfile 'E:/Qifang/DbProjectData/PFam/pdb_pfama.txt' " +
                " From pdb_pfama_reg, pfama, pfamSeq, pfama_reg_full_significant " +
                " Where pdb_pfama_reg.auto_pfama = pfama.auto_pfama AND " +
                " pdb_pfama_reg.auto_pfamseq = pfamseq.auto_pfamseq AND " +
                " pdb_pfama_reg.auto_pfama_reg_full = pfama_reg_full_significant.auto_pfama_reg_full;";
            dbQuery.Query(mysqlConnect, queryString);

			queryString = "Select Pdb_ID, Chain, PfamB_Acc, PfamB_ID, " + 
				" PfamSeq_Acc, PfamSeq_ID, Pdb_res_start, pdb_res_end, seq_start, seq_end " + 
				" Into outfile 'E:/Qifang/DbProjectData/PFam/pdb_pfamb.txt' " + 
				" From pdb_pfamb_reg, pfamb, pfamSeq " + 
				" Where pdb_pfamb_reg.auto_pfamb = pfamb.auto_pfamb AND " + 
				" pdb_pfamb_reg.auto_pfamseq = pfamseq.auto_pfamseq;";
            dbQuery.Query(mysqlConnect, queryString);

            mysqlConnect.DisconnectFromDatabase();
		}

        public struct PfamAlignInfo
        {
            public string align_start;
            public string align_end;
            public string model_start;
            public string model_end;
            public string domain_bits_score;
            public string domain_evalue_score;
            public string sequence_bits_score;
            public string sequence_evalue_score;
        }
        public void PrintPfamHmmAlignmentInfo()
        {
            if (!mysqlConnect.IsConnected())
            {
                string connectString = "Driver={MySQL ODBC 5.1 Driver}; Server=localhost;Database=pfam;UID=root;PWD=;";
                mysqlConnect.ConnectString = connectString;
                mysqlConnect.ConnectToDatabase();
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine (pfamDataDir, "PdbSeqHmmAlignInfo.txt"));
            string dataLine = "";
            string queryString = "Select * From pdb_pfamA_reg;";
            DataTable pdbChainDomainTable = dbQuery.Query(mysqlConnect, queryString);
            int auto_pfamseq = 0;
            int auto_pfamA_reg_full = 0;
            int auto_pfama = 0;
            Dictionary<int, string[]> pfamDefHash = new Dictionary<int, string[]>();
            Dictionary<int, string[]> pfamSeqInfoHash = new Dictionary<int, string[]>();
            Dictionary<int, PfamAlignInfo> pfamAlignInfoHash = new Dictionary<int, PfamAlignInfo>();
            foreach (DataRow chainRow in pdbChainDomainTable.Rows)
            {
                auto_pfamseq = Convert.ToInt32(chainRow["auto_pfamseq"].ToString ());
                auto_pfama = Convert.ToInt32(chainRow["auto_pfama"].ToString ());
                auto_pfamA_reg_full = Convert.ToInt32(chainRow["Auto_Pfama_reg_full"].ToString ());
                string[] pfamInfo = GetPfamAFamilyInfo(auto_pfama, ref pfamDefHash);
                string[] pfamSeqInfo = GetPfamSequenceInfo (auto_pfamseq, ref pfamSeqInfoHash);
                PfamAlignInfo pfamAlignInfo = GetPfamAlignInfo(auto_pfamA_reg_full, ref pfamAlignInfoHash);

                dataLine = chainRow["pdb_id"].ToString() + "\t" + chainRow["Chain"].ToString().TrimEnd() + "\t" +
                    pfamInfo[0] + "\t" + pfamInfo[1] + "\t" + pfamInfo[2] + "\t" +
                    pfamSeqInfo[0] + "\t" + pfamSeqInfo[1] + "\t" + chainRow["pdb_res_start"].ToString() + "\t" +
                    chainRow["pdb_res_end"].ToString() + "\t" + chainRow["seq_start"].ToString() + "\t" +
                    chainRow["seq_end"].ToString() + "\t" + pfamAlignInfo.align_start + "\t" + pfamAlignInfo.align_end + "\t" +
                    pfamAlignInfo.model_start + "\t" + pfamAlignInfo.model_end + "\t" + pfamAlignInfo.domain_bits_score + "\t" +
                    pfamAlignInfo.domain_evalue_score + "\t" + pfamAlignInfo.sequence_bits_score + "\t" +
                    pfamAlignInfo.sequence_evalue_score;
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
            mysqlConnect.DisconnectFromDatabase();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="auto_pfamA"></param>
        /// <param name="pfamDefHash"></param>
        /// <returns></returns>
        private string[] GetPfamAFamilyInfo(int auto_pfamA, ref Dictionary<int, string[]> pfamDefHash)
        {
            if (pfamDefHash.ContainsKey(auto_pfamA))
            {
                return pfamDefHash[auto_pfamA];
            }
            string queryString = string.Format("Select PfamA_Acc, PfamA_ID, Description From PfamA Where auto_pfamA = {0};", auto_pfamA);
            DataTable pfamFamilyInfoTable = dbQuery.Query (mysqlConnect, queryString);
            string[] familyInfo = new string[3];
            if (pfamFamilyInfoTable.Rows.Count > 0)
            { 
                familyInfo[0] = pfamFamilyInfoTable.Rows[0]["PfamA_ACC"].ToString().TrimEnd();
                familyInfo[1] = pfamFamilyInfoTable.Rows[0]["PfamA_ID"].ToString().TrimEnd();
                familyInfo[2] = pfamFamilyInfoTable.Rows[0]["Description"].ToString().TrimEnd();
            }
            pfamDefHash.Add(auto_pfamA, familyInfo);
            return familyInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="auto_pfamseq"></param>
        /// <param name="pfamSeqInfoHash"></param>
        /// <returns></returns>
        private string[] GetPfamSequenceInfo(int auto_pfamseq, ref Dictionary<int, string[]> pfamSeqInfoHash)
        {
            if (pfamSeqInfoHash.ContainsKey(auto_pfamseq))
            {
                return pfamSeqInfoHash[auto_pfamseq];
            }
            string queryString = string.Format("Select PfamSeq_ID, PfamSeq_ACC From PfamSeq Where Auto_PfamSeq = {0};", auto_pfamseq);
            DataTable pfamSeqInfoTable = dbQuery.Query(mysqlConnect, queryString);
            string[] pfamSeqInfo = new string[2];
            pfamSeqInfo[0] = pfamSeqInfoTable.Rows[0]["PfamSeq_Acc"].ToString().TrimEnd();
            pfamSeqInfo[1] = pfamSeqInfoTable.Rows[0]["PfamSeq_ID"].ToString().TrimEnd();
            pfamSeqInfoHash.Add(auto_pfamseq, pfamSeqInfo);
            return pfamSeqInfo;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="auto_pfama_reg_full"></param>
        /// <param name="pfamAlignInfoHash"></param>
        /// <returns></returns>
        private PfamAlignInfo GetPfamAlignInfo(int auto_pfama_reg_full, ref Dictionary<int, PfamAlignInfo> pfamAlignInfoHash)
        {
            if (pfamAlignInfoHash.ContainsKey(auto_pfama_reg_full))
            {
                return pfamAlignInfoHash[auto_pfama_reg_full];
            }
            string queryString = string.Format ("Select * From PfamA_Reg_Full_Significant Where Auto_PfamA_Reg_full = {0};", 
                auto_pfama_reg_full);
            DataTable alignInfoTable = dbQuery.Query(mysqlConnect, queryString);
            PfamAlignInfo pfamAlignInfo = new PfamAlignInfo();
            if (alignInfoTable.Rows.Count > 0)
            {
                pfamAlignInfo.align_start = alignInfoTable.Rows[0]["ali_start"].ToString();
                pfamAlignInfo.align_end = alignInfoTable.Rows[0]["ali_end"].ToString();
                pfamAlignInfo.model_start = alignInfoTable.Rows[0]["model_start"].ToString();
                pfamAlignInfo.model_end = alignInfoTable.Rows[0]["model_end"].ToString();
                pfamAlignInfo.domain_bits_score = alignInfoTable.Rows[0]["domain_bits_score"].ToString();
                pfamAlignInfo.domain_evalue_score = alignInfoTable.Rows[0]["domain_evalue_score"].ToString();
                pfamAlignInfo.sequence_bits_score = alignInfoTable.Rows[0]["sequence_bits_score"].ToString();
                pfamAlignInfo.sequence_evalue_score = alignInfoTable.Rows[0]["sequence_evalue_score"].ToString();
            }
            pfamAlignInfoHash.Add(auto_pfama_reg_full, pfamAlignInfo);
            return pfamAlignInfo;
        }
	}
}
