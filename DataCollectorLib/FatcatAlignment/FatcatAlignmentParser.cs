using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;


namespace DataCollectorLib.FatcatAlignment
{
	/// <summary>
	/// Parse FATCAT alignment output
	/// insert data into database
	/// </summary>
	public class FatcatAlignmentParser
	{
		#region member variables
		public DbQuery dbQuery = new DbQuery ();
		public DbInsert dbInsert = new DbInsert ();
        public DbUpdate dbUpdate = new DbUpdate();
        public AlignSeqNumConverter seqConverter = new AlignSeqNumConverter();

        public string dbAlignTableName = "";
        public DbConnect alignmentDbConnection = null;
		public StreamWriter logWriter = null;

		#endregion

        public FatcatAlignmentParser()
        {
            dbAlignTableName = "FatcatAlignments";
            alignmentDbConnection = ProtCidSettings.alignmentDbConnection;
        }

		public FatcatAlignmentParser(DbConnect dbConnect)
		{
           alignmentDbConnection = dbConnect;
           dbAlignTableName = "FatcatAlignments";
	    }

        public FatcatAlignmentParser(DbConnect dbConnect, string dbTableName)
        {
            alignmentDbConnection = dbConnect;
            this.dbAlignTableName = dbTableName;
        }

	    /// <summary>
	    /// 
	    /// </summary>
	    /// <param name="alignFiles"></param>
	    /// <param name="isUpdate"></param>
        public void ParseFatcatAlignments (bool isUpdate)
		{
            Initialize(isUpdate);

            string alignmentFileDir = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments");
            string[] alignFiles = System.IO.Directory.GetFiles(alignmentFileDir, "*.aln");

            string parsedDir = Path.Combine(alignmentFileDir, "parsed_" + DateTime.Today.ToShortDateString());
            if (!Directory.Exists(parsedDir))
            {
                Directory.CreateDirectory(parsedDir);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing FATCAT alignments files.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Fatcat alignments";
            ProtCidSettings.progressInfo.totalOperationNum = alignFiles.Length;
            ProtCidSettings.progressInfo.totalStepNum = alignFiles.Length;

			foreach (string alignFile in alignFiles)
			{
                ProtCidSettings.progressInfo.currentFileName = alignFile;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
				
                try
                {
                    ParseFatcatAlignmentFile(alignFile);

                    FileInfo thisFileInfo = new FileInfo(alignFile);
                    File.Move(alignFile, Path.Combine(parsedDir, thisFileInfo.Name));
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(alignFile + ": " + ex.Message);
                    logWriter.Flush();
                }
                finally
                {
                    GC.Collect();
                }
			}
            alignmentDbConnection.DisconnectFromDatabase();
            logWriter.WriteLine("Parsing done!");
			logWriter.Close ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
		}
		/// <summary>
		/// parse fatcat alignment file
		/// </summary>
		/// <param name="alignFile"></param>
		public void ParseFatcatAlignments (string alignFile, bool isUpdate)
		{
            dbAlignTableName = "FatCatAlignments";
            Initialize(isUpdate);

			ParseFatcatAlignmentFile (alignFile);

            alignmentDbConnection.DisconnectFromDatabase();
            logWriter.Close();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignFiles"></param>
        /// <param name="isUpdate"></param>
        public void ParseFatcatAlignments(string[] alignFiles, bool isUpdate)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            if (alignFiles == null || alignFiles.Length == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("There is no input alignment files: length = 0");
                ProtCidSettings.logWriter.WriteLine("There is no input alignment files: length = 0");
                ProtCidSettings.logWriter.Flush();
                return;
            }
            
            ProtCidSettings.progressInfo.totalOperationNum = alignFiles.Length;
            ProtCidSettings.progressInfo.totalStepNum = alignFiles.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing FATCAT chain alignments");
            ProtCidSettings.progressInfo.currentOperationLabel = "Parsing FATCAT alignments";

            dbAlignTableName = "FatCatAlignments";

            Initialize(isUpdate);

            FileInfo fileInfo  = new FileInfo (alignFiles[0]);
            string parsedDir = Path.Combine(fileInfo.DirectoryName, "parsed_" + DateTime.Today.ToShortDateString());
            if (!Directory.Exists(parsedDir))
            {
                Directory.CreateDirectory(parsedDir);
            }
            

            foreach (string alnFile in alignFiles)
            {
                ProtCidSettings.progressInfo.currentFileName = alnFile;
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(alnFile);
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                try
                {
                    ParseFatcatAlignmentFile(alnFile);

                    FileInfo thisFileInfo = new FileInfo(alnFile);
                    File.Move(alnFile, Path.Combine(parsedDir, thisFileInfo.Name));
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(alnFile + ": " + ex.Message);
                    logWriter.Flush();
                }
                finally
                {
                    GC.Collect();
                }
            }
            alignmentDbConnection.DisconnectFromDatabase();
            logWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
          
		/// <summary>
		/// parse one fatcat alignment output file
		/// insert data into database
		/// </summary>
		/// <param name="alignFile"></param>
		public void ParseFatcatAlignmentFile (string alignFile)
		{
            /* modified on April 5, 2010, change the input files of FATCAT from Guoli's regular files
                     into my regular file with XMl sequential numbers and asymID
                     instead of PDB sequence numbers.
                     so no sequence nubmers conversion needed 
            */ 
            if (logWriter == null)
            {
                logWriter = new StreamWriter("fatcatAlignmentsLog.txt", true);
            }
            logWriter.WriteLine(alignFile);
			StreamReader dataReader = new StreamReader (alignFile);
			string line = "";
			int scoreIdx = -1;
			int alignLenIdx = -1;
			int gapIdx = -1;
			int gapEndIdx = -1;
			string alignSequence1 = "";
			string alignSequence2 = "";
			int alignStart1 = -1;
			int alignEnd1 = -1;
			int alignStart2 = -1;
			int alignEnd2 = -1;
			string[] fields = null;
			bool chain1Started = false;
			bool chain2Started = false;
			AlignSeqInfo alignInfo1 = new AlignSeqInfo ();
			AlignSeqInfo alignInfo2 = new AlignSeqInfo ();
			DataRow dataRow = null;
            Dictionary<string, string> entryAuthChainHash = new Dictionary<string,string> ();
            string dataLine = "";
			while ((line = dataReader.ReadLine ()) != null)
			{
				if (line == "")
				{
					continue;
				}
				
				if (line.IndexOf ("Align") > -1 && 
					line.Substring (0, "Align".Length) == "Align")
				{
                    // the fatcat format: Align 3v2dX.pdb 70 with 4garZ.pdb 58
					fields = ParseHelper.SplitPlus (line, ' ');

                    // get the pdbid and chain id from the fileName
                    string[] entryChainFields1 = GetEntryChainFields(fields[1]);
                    string[] entryChainFields2 = GetEntryChainFields(fields[4]);

                    dataRow = FatcatTables.fatcatAlignTable.NewRow();

					dataRow["QueryEntry"] = entryChainFields1[0];
					dataRow["QueryLength"] = fields[2];
					dataRow["HitEntry"] = entryChainFields2[0];
					dataRow["HitLength"] = fields[5];

					alignInfo1.pdbId = entryChainFields1[0];
			        alignInfo1.asymChainId = entryChainFields1[1];
                    alignInfo1.chainId = GetAuthorChainFromAsymID(alignInfo1.pdbId, alignInfo1.asymChainId, ref entryAuthChainHash);
                    dataRow["QueryChain"] = alignInfo1.chainId;
                    dataRow["QueryAsymChain"] = alignInfo1.asymChainId;

					alignInfo2.pdbId = entryChainFields2[0];
		            alignInfo2.asymChainId = entryChainFields2[1];
                    alignInfo2.chainId = GetAuthorChainFromAsymID (alignInfo2.pdbId, alignInfo2.asymChainId, ref entryAuthChainHash);
                    dataRow["HitAsymChain"] = alignInfo2.asymChainId;
                    dataRow["HitChain"] = alignInfo2.chainId;

                    alignSequence1 = "";
					alignSequence2 = "";
					chain1Started = true;
					chain2Started = true;
                    dataLine = "";
				}
                dataLine += (line + "\r\n");
				scoreIdx = line.IndexOf ("Score");
				if (scoreIdx > -1)
				{
					// from opt-equ, equivalent positions
					alignLenIdx = line.IndexOf ("align-len");
					gapIdx = line.IndexOf ("gaps");
					gapEndIdx = line.LastIndexOf ("(");
					dataRow["Score"] = line.Substring (scoreIdx + "Score".Length + 1, alignLenIdx - scoreIdx - "Score".Length - 1);
					dataRow["Align_Len"] = line.Substring (alignLenIdx + "align-len".Length + 1, 
						gapIdx - alignLenIdx - "align-len".Length - 2);
					dataRow["Gaps"] = line.Substring (gapIdx + "gaps".Length + 1, gapEndIdx - gapIdx - "gaps".Length - 2);
				}
				if (line.IndexOf ("P-value") > -1)
				{
					fields = ParseHelper.SplitPlus (line, ' ');
					dataRow["E_Value"] = Convert.ToDouble (fields[1]);
					dataRow["Identity"] = fields[5].TrimEnd ('%');
					dataRow["Similarity"] = fields[7].TrimEnd ('%');
				}
				if (line.IndexOf ("Chain 1: ") > -1)
				{
					fields = ParseHelper.SplitPlus (line, ' ');
					if (chain1Started)
					{
						alignStart1 = ConvertSeqToInt (fields[2]);
						chain1Started = false;
					}
					alignSequence1 += fields[3];
					alignEnd1 = ConvertSeqToInt (fields[2]) + GetNonGapAlignedString (fields[3]).Length - 1;
				}
				if (line.IndexOf ("Chain 2:") > -1)
				{
                    line = line.Replace(':', ' ');
					fields = ParseHelper.SplitPlus (line, ' ');
					if (chain2Started)
					{
						alignStart2 = ConvertSeqToInt (fields[2]);
						chain2Started = false;
					}
					
					alignSequence2 += fields[3];
					alignEnd2 = ConvertSeqToInt (fields[2]) + GetNonGapAlignedString (fields[3]).Length - 1;
				}
				if (line.IndexOf ("Note:") > -1)
				{
					alignInfo1.alignStart = alignStart1;
					alignInfo1.alignEnd = alignEnd1;
					alignInfo1.alignSequence = alignSequence1;
					alignInfo2.alignStart = alignStart2;
					alignInfo2.alignEnd = alignEnd2;
					alignInfo2.alignSequence = alignSequence2;  
                /*  if (IsAlignmentInDb(alignInfo1.pdbId, alignInfo1.chainId, alignInfo2.pdbId, alignInfo2.chainId))
                    {
                        continue;
                    }
					*/
                    // Convert aligned sequences to xml sequences
                    // add these residues with no-coordinate and no -Calpha to the alignment
                    // modified on August 31, 2012
                    try
                    {
                        seqConverter.AddDisorderResiduesToAlignment(ref alignInfo1, ref alignInfo2);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine(alignInfo1.pdbId + alignInfo1.asymChainId + " " +
                               alignInfo2.pdbId + alignInfo2.asymChainId  + " filling out disorder residues failed: " + ex.Message);
                        logWriter.Flush();
                    }

					dataRow["AlignmentLength"] = GetAlignmentLength (alignSequence1, alignSequence2);
					dataRow["QuerySequence"] = alignInfo1.alignSequence;
                    dataRow["HitSequence"] = alignInfo2.alignSequence;
                    // modified on April 10, 2010. Since input files for FATCAT use XML sequential numbers
                    dataRow["QueryStart"] = alignInfo1.alignStart;
                    dataRow["QueryEnd"] = alignInfo1.alignEnd;
                    dataRow["HitStart"] = alignInfo2.alignStart;
                    dataRow["HitEnd"] = alignInfo2.alignEnd;
      //              DeleteDbData(alignInfo1.pdbId, alignInfo1.chainId, alignInfo2.pdbId, alignInfo2.chainId);

                    FatcatTables.fatcatAlignTable.Rows.Add(dataRow);
       /*             try
                    {
                        dbInsert.InsertDataIntoDb(alignmentDbConnection, dataRow);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine(alignFile + ": error " + ex.Message + "\r\n" + ParseHelper.FormatDataRow (dataRow) + " ");
                        logWriter.Flush();
                    }*/
				}
			}
			dataReader.Close ();
            try
            {
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, FatcatTables.fatcatAlignTable);
                FatcatTables.fatcatAlignTable.Clear();
                // "too many open handles to database", try to close the handles before leave by commit or rollback
                dbUpdate.Update(ProtCidSettings.alignmentDbConnection, "Commit;");
            }
            catch (Exception ex)
            {
                logWriter.WriteLine(alignFile + ": error " + ex.Message + "\r\n" + ParseHelper.FormatDataRow(dataRow) + " ");
                logWriter.Flush();
            }            
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainFileName"></param>
        /// <returns></returns>
        private string[] GetEntryChainFields(string chainFileName)
        {
            string[] fields = chainFileName.Split('.');
            string pdbId = fields[0].Substring(0, 4);
            string chainId = fields[0].Substring(4, fields[0].Length - 4);
            string[] entryChainFields = new string[2];
            entryChainFields[0] = pdbId;
            entryChainFields[1] = chainId;
            return entryChainFields;
        }
		/// <summary>
		/// alignment length
		/// </summary>
		/// <param name="alignSequence1"></param>
		/// <param name="alignSequence2"></param>
		/// <returns></returns>
		public int GetAlignmentLength (string alignSequence1, string alignSequence2)
		{
			int alignLength = 0;
			for (int i = 0; i < alignSequence1.Length; i ++)
			{
				if (alignSequence1[i] != '-' && alignSequence2[i] != '-')
				{
					alignLength ++;
				}
			}
			return alignLength;
		}

		/// <summary>
		/// check if the alignment is in database
		/// </summary>
		/// <param name="queryEntry"></param>
		/// <param name="queryChain"></param>
		/// <param name="hitEntry"></param>
		/// <param name="hitChain"></param>
		/// <returns></returns>
		private bool IsAlignmentInDb (string queryEntry, string queryChain, string hitEntry, string hitChain)
		{
			string queryString = string.Format ("Select * From FatCatAlignments " + 
				" Where QueryEntry = '{0}' AND QueryChain = '{1}' AND " + 
				" HitEntry = '{2}' AND HitChain = '{3}';", 
				queryEntry, queryChain, hitEntry, hitChain);
			DataTable alignTable = dbQuery.Query (alignmentDbConnection, queryString);
            if (alignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From FatCatAlignments " +
                " Where QueryEntry = '{0}' AND QueryChain = '{1}' AND " +
                " HitEntry = '{2}' AND HitChain = '{3}';",
                hitEntry, hitChain, queryEntry, queryChain);
                alignTable = dbQuery.Query(alignmentDbConnection, queryString);
            }
			if (alignTable.Rows.Count > 0)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// delete the alignment from database
		/// </summary>
		/// <param name="queryEntry"></param>
		/// <param name="queryChain"></param>
		/// <param name="hitEntry"></param>
		/// <param name="hitChain"></param>
		private void DeleteDbData (string queryEntry, string queryChain, string hitEntry, string hitChain)
		{
			string deleteString = string.Format ("Delete From FatCatAlignments " + 
				" Where (QueryEntry = '{0}' AND QueryChain = '{1}' AND " +
                " HitEntry = '{2}' AND HitChain = '{3}') " +
                " OR (QueryEntry = '{2}' AND QueryChain = '{3}' AND " +
                " HitEntry = '{0}' AND HitChain = '{1}');", 
				queryEntry, queryChain, hitEntry, hitChain);
			dbUpdate.Delete (alignmentDbConnection, deleteString);


      /*      deleteString = string.Format("Delete From FatCatAlignments " +
                " Where QueryEntry = '{0}' AND QueryChain = '{1}' AND " +
                " HitEntry = '{2}' AND HitChain = '{3}';",
                hitEntry, hitChain, queryEntry, queryChain);
            dbUpdate.Delete (alignmentDbConnection, deleteString);*/
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="alignedSeq"></param>
		/// <returns></returns>
		public string GetNonGapAlignedString (string alignedSeq)
		{
			string nonGapString = "";
			foreach (char ch in alignedSeq)
			{
				if (ch == '-' || ch == '.')
				{
					continue;
				}
				nonGapString += ch.ToString ();
			}
			return nonGapString;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqString"></param>
        /// <returns></returns>
		public int ConvertSeqToInt (string seqString)
		{
			string digitString = "";
			foreach (char ch in seqString)
			{
				if (Char.IsDigit (ch))
				{
					digitString += ch.ToString ();
				}
			}
            if (digitString == "")
            {
                return -1;
            }
			return Convert.ToInt32 (digitString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="entryAsymChainHash"></param>
        /// <returns></returns>
        private string GetAsymChainID(string pdbId, string authChain, ref Dictionary<string, string> entryAsymChainHash)
        {
            string asymChain = "-";
            if (entryAsymChainHash.ContainsKey(pdbId + "_" + authChain))
            {
                asymChain = entryAsymChainHash[pdbId + "_" + authChain];
            }
            else
            {
                string queryString = string.Format("Select AsymID From AsymUnit Where " + 
                    " PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", 
                    pdbId, authChain);
                DataTable asymChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

                if (asymChainTable.Rows.Count == 0 && authChain == "A")
                {
                    queryString = string.Format("Select AsymID From AsymUnit Where " +
                    " PdbID = '{0}' AND AuthorChain = '_' AND PolymerType = 'polypeptide';", pdbId);

                    asymChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                }
                if (asymChainTable.Rows.Count > 0)
                {
                    asymChain = asymChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
                }
               
                entryAsymChainHash.Add(pdbId + "_" + authChain, asymChain);
            }
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymId"></param>
        /// <returns></returns>
        private string GetAuthorChainFromAsymID(string pdbId, string asymId, ref Dictionary<string, string> entryAuthChainHash)
        {
            string authChain = "";
            if (entryAuthChainHash.ContainsKey(pdbId + asymId))
            {
                authChain = entryAuthChainHash[pdbId + asymId];
            }
            else
            {
                if (seqConverter.asuSeqInfoTable != null)
                {
                    DataRow[] authorChainRows = seqConverter.asuSeqInfoTable.Select(string.Format ("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymId));
                    if (authorChainRows.Length > 0)
                    {
                        authChain = authorChainRows[0]["AuthorChain"].ToString().TrimEnd();
                    }
                    else
                    {
                        authChain = "A";
                    }
                }
                else
                {
                    string queryString = string.Format("Select AuthorChain From AsymUnit " +
                        " Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymId);
                    DataTable authChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                    if (authChainTable.Rows.Count > 0)
                    {
                        authChain = authChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                    }
                    else
                    {
                        authChain = "A";
                    }
                }               
                entryAuthChainHash.Add(pdbId + asymId, authChain);
            }
            if (authChain == "_")
            {
                authChain = "A";
            }
            return authChain;
        }
        #region initialize 
        /// <summary>
        /// 
        /// </summary>
        public void Initialize (bool isUpdate)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            // for connecting to ProtBuD database, converting sequence numbers
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.pdbfamDbPath);
            }

            if (alignmentDbConnection == null)
            {
                alignmentDbConnection = new DbConnect();
                alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.alignmentDbPath;
            }
            alignmentDbConnection.ConnectToDatabase();

            FatcatTables.InitializeTables(dbAlignTableName);
  /*          if (! isUpdate) // intend to comment out, to prevent drop the tables
            {
                FatcatTables.InitializeDbTable(alignmentDbConnection, dbAlignTableName); // create new fatcat alignments table in the db
                FatcatTables.InitializeDbTable(alignmentDbConnection, dbAlignTableName + "Rigid");
            }*/

            if (logWriter == null)
            {
                logWriter = new StreamWriter("FatcatAlignmentsParsingLog.txt", true);
                logWriter.WriteLine(DateTime.Today.ToShortDateString());
            }
        }

        #endregion
	}
}
