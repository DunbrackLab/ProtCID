using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using AuxFuncLib;
using ProtCidSettingsLib;
using DbLib;

namespace DataCollectorLib.HHAlignment
{
    /// <summary>
    /// this class is to parse a hhr file
    /// where a query is a hmm file, and database is an hmm database
    /// e.g. pdb sequence hmm to pdb hmm database, from pisces
    /// </summary>
    public class HhrFileParser
    {
        #region member variables
        public double doubleLimit = 1E-307;  // the firebird database cannot accept the higher precision double 
        private string hhAlignTableName = "PdbCrcHHAlignments";  // hh alignment for pairwise pdb sequences
        private DataTable hhAlignTable = null;
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();

        public string HHAlignTableName
        {
            get
            {
                return hhAlignTableName;
            }
            set
            {
                hhAlignTableName = value;
            }
        }
        #endregion

        /// <summary>
        /// build hh alignment table 
        /// </summary>
        /// <param name="hhrListFile">the list of sequence codes, like uniprot code, pdb id, crc code</param>
        /// <param name="hhrFileDir">the directory where hhr files are</param>
        public void BuildHhAlignments (string hhrListFile, string hhrFileDir)
        {
            if (hhrListFile == "")
            {
                BuildHhAlignments(hhrFileDir);
                return;
            }
            bool isUpdate = false;
            Initialize(isUpdate);

            StreamReader lsReader = new StreamReader(hhrListFile);
            string seqCode = "";
            string hhrFile = "";
            bool deleteHhrFile = false;
            while ((seqCode = lsReader.ReadLine()) != null)
            {
                deleteHhrFile = false;
                hhrFile = Path.Combine(hhrFileDir, seqCode + ".hhr");
                if (! File.Exists (hhrFile))
                {
                    hhrFile = Path.Combine(hhrFileDir, seqCode + ".hhr.gz");
                    hhrFile = ParseHelper.UnZipFile(hhrFile, ProtCidSettings.tempDir);
                    deleteHhrFile = true;
                }
                try
                {
                    ParseHhrFile(hhrFile, hhAlignTable);
                    dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.alignmentDbConnection, hhAlignTable);
           //         InsertHhAlignDataToDb(hhAlignTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(seqCode + " Parsing hhr file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                hhAlignTable.Clear();

                if (deleteHhrFile)
                {
                    File.Delete(hhrFile);
                }
            }
            lsReader.Close();
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// build hh alignment table from the all files in the input directory
        /// </summary>
        /// <param name="hhrFileDir">the directory where hhr files are</param>
        public void BuildHhAlignments(string hhrFileDir)
        {
            bool isUpdate = false;
            Initialize(isUpdate);          

            string[] hhrFiles = Directory.GetFiles(hhrFileDir, "*.hhr*");

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing hhr files");
            ProtCidSettings.progressInfo.totalOperationNum = hhrFiles.Length;
            ProtCidSettings.progressInfo.totalStepNum = hhrFiles.Length;

            foreach (string hhrFile in hhrFiles)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(hhrFile);
                    ProtCidSettings.progressInfo.currentFileName = fileInfo.Name;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;
                    ProtCidSettings.progressInfo.currentOperationLabel = fileInfo.Name;
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(fileInfo.Name);

                    ParseHhrFile(hhrFile, hhAlignTable);
             //       InsertHhAlignDataToDb(hhAlignTable);
                    dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.alignmentDbConnection, hhAlignTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(hhrFile + " Parsing hhr file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(hhrFile + " Parsing hhr file error: " + ex.Message);
                }
                hhAlignTable.Clear();
            }
            ProtCidSettings.logWriter.Flush();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parseing done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateListFile"></param>
        /// <param name="obsListFile"></param>
        /// <param name="hhrFileDir"></param>
        public void UpdateHhAlignments (string updateListFile, string hhrFileDir)
        {
            bool isUpdate = true;
            Initialize(isUpdate);

            StreamReader lsReader = new StreamReader(updateListFile);
            string seqCode = "";
            string hhrFile = "";
            bool deleteHhrFile = false;
            while ((seqCode = lsReader.ReadLine()) != null)
            {
                deleteHhrFile = false;
                hhrFile = Path.Combine(hhrFileDir, seqCode + ".hhr");
                if (!File.Exists(hhrFile))
                {
                    hhrFile = Path.Combine(hhrFileDir, seqCode + ".hhr.gz");
                    hhrFile = ParseHelper.UnZipFile(hhrFile, ProtCidSettings.tempDir);
                    deleteHhrFile = true;
                }
                try
                {
                    DeleteUpdateHhAlignData(seqCode);

                    ParseHhrFile(hhrFile, hhAlignTable);
            //        InsertHhAlignDataToDb (hhAlignTable);
                    dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.alignmentDbConnection, hhAlignTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(seqCode + " Parsing hhr file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                hhAlignTable.Clear();

                if (deleteHhrFile)
                {
                    File.Delete(hhrFile);
                }
            }
            lsReader.Close();
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// delete these sequences not in the database any more
        /// </summary>
        /// <param name="obsListFile"></param>
        public void DeleteObsoleteHhAlignData (string obsListFile)
        {
            StreamReader dataReader = new StreamReader(obsListFile);
            string seqCode = "";
            while ((seqCode = dataReader.ReadLine ()) != null)
            {
                DeleteUpdateHhAlignData(seqCode);
            }
            dataReader.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqCode"></param>
        private void DeleteUpdateHhAlignData (string seqCode)
        {
            string deleteString = string.Format("Delete From {0} Where Query = '{1}' OR Hit = '{1}';", hhAlignTableName, seqCode);
            dbUpdate.Delete(ProtCidSettings.alignmentDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void Initialize (bool isUpdate)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
                ProtCidSettings.alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.alignmentDbPath);
            }
            hhAlignTable = CreateHHAlignmentTable(isUpdate, hhAlignTableName);
        }

        /// <summary>
        /// 
        /// </summary>
        private void InsertHhAlignDataToDb (DataTable hhAlignmentTable)
        {
            int rowCutoff = 100;
            int rowCount = 0;
            DataTable subHhAlignTable = hhAlignmentTable.Clone();
            subHhAlignTable.TableName = hhAlignmentTable.TableName;
            for (int i = 0; i < hhAlignmentTable.Rows.Count; i++)
            {
                if (rowCount == rowCutoff)
                {
                    try
                    {
                        dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, subHhAlignTable);
                    }
                    catch
                    {
                        dbInsert.InsertDataIntoDBtables (ProtCidSettings.alignmentDbConnection, subHhAlignTable);
                    }
                    subHhAlignTable.Clear();
                    rowCount = 0;
                }
                DataRow newDataRow = subHhAlignTable.NewRow();
                newDataRow.ItemArray = hhAlignmentTable.Rows[i].ItemArray;
                subHhAlignTable.Rows.Add(newDataRow);
                rowCount++;
            }
            if (subHhAlignTable.Rows.Count > 0)
            {
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, subHhAlignTable);
            }
        }

        #region parse hhr file
        /// <summary>
        /// parse a hhr file, searching a hmm on an hmm database
        /// </summary>
        /// <param name="hhrFile"></param>
        public void ParseHhrFile(string hhrFile, DataTable hhAlignTable)
        {
            bool deleteTempHhr = false;
            string unzipHhrFile = "";
            FileInfo fileInfo = new FileInfo(hhrFile);           

            if (hhrFile.IndexOf(".gz") > -1)
            {
                unzipHhrFile = Path.Combine (ProtCidSettings.tempDir, fileInfo.Name.Replace (".gz", ""));
                if (File.Exists(unzipHhrFile))
                {
                    hhrFile = unzipHhrFile;
                }
                else
                {
                    hhrFile = ParseHelper.UnZipFile(hhrFile, ProtCidSettings.tempDir);
                }
                deleteTempHhr = true;
            }
            fileInfo = new FileInfo(hhrFile);
            string seqName = fileInfo.Name.Replace(".hhr", "");  
          
            StreamReader dataReader = new StreamReader(hhrFile);
            string line = "";
            bool isSumInfoStart = false;
            string sumLine = "No Hit                             Prob E-value P-value  Score    SS Cols Query HMM  Template HMM";
            string alignmentChar = ">";
            int hitNo = 1;
            DataRow hitRow = null;
            string queryAlignment = "";
            string queryConsensus = "";
            int queryLength = 0;
            string hitAlignment = "";
            string hitConsensus = "";
            string queryDssp = "";
            string hitDssp = "";
            string confidence = "";
            bool isAlignStart = false;
            string match = "";
  //          string matchLine = "";
            int matchColumns = 0;
            string matchColumnsString = "";
            string seqSummaryLine = dataReader.ReadLine(); // summary info about the query sequence
            string[] seqSumFields = ParseHelper.SplitPlus(seqSummaryLine, ' ');
            double prob = 0;
            double evalue = 0;
            string hhAlignTableName = hhAlignTable.TableName.ToLower();

            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    if (isSumInfoStart)
                    {
                        isSumInfoStart = false;
                    }
                    continue;
                }
                if (line.IndexOf("Match_columns") > -1)
                {
                    matchColumnsString = line.Substring("Match_columns".Length,
                        line.Length - "Match_columns".Length).Trim();
                    matchColumns = Convert.ToInt32(matchColumnsString);
                }
                if (line.IndexOf("No_of_seqs    ") > -1)
                {
                    string[] lineFields = ParseHelper.SplitPlus(line, ' ');
                    queryLength = Convert.ToInt32 (lineFields[lineFields.Length - 1]);
                }
                if (line.IndexOf(sumLine) > -1)
                {
                    isSumInfoStart = true;
                    continue;
                }
                if (isSumInfoStart)
                {
                    string[] sumInfoFields = SplitHhrSumInfoLine(line);
                    if (sumInfoFields != null && sumInfoFields.Length == 13)
                    {
                        AddHhAlignSumInfoToTable(seqName, queryLength, sumInfoFields, hhAlignTable);
                        continue;
                    }
                }

                if (line.Substring(0, 1) == alignmentChar)
                {
                    isAlignStart = false;
                    if (queryAlignment != "")
                    {
                        hitRow["QueryAlignment"] = queryAlignment;                      
                        hitRow["HitAlignment"] = hitAlignment;
                        /*
                        hitRow["QueryConsensus"] = queryConsensus;
                        hitRow["HitConsensus"] = hitConsensus;
                        hitRow["QueryDssp"] = queryDssp;
                        hitRow["HitDssp"] = hitDssp;
                        hitRow["Confidence"] = confidence;
                        hitRow["Match"] = match; */

                        queryAlignment = "";
                        queryConsensus = "";
                        hitAlignment = "";
                        hitConsensus = "";
                        queryDssp = "";
                        hitDssp = "";
                        confidence = "";
                    }
                    DataRow[] hitRows = hhAlignTable.Select(string.Format("HitNo = '{0}'", hitNo));
                    hitRow = hitRows[0];
                    isSumInfoStart = false;
                    string[] hitInfoFields = line.Split(' '); // fields[0]: pfam info
                    // hhsearch changed its format for pfam database, which is a real pain
                    hitRow["Hit"] = hitInfoFields[0].TrimStart('>');
                    // the probab evalue line
                    line = dataReader.ReadLine();
                    string[] alignSumInfoFields = ParseHelper.SplitPlus(line, ' ');
                    foreach (string alignSumInfoField in alignSumInfoFields)
                    {
                        string[] equationFields = alignSumInfoField.Split('=');
                        switch (equationFields[0].ToLower())
                        {
                            case "probab":
                                prob = Convert.ToDouble(equationFields[1]);
                                if (prob < doubleLimit)
                                {
                                    prob = 0;
                                }
                                //   hitRow["Prob"] = equationFields[1];
                                hitRow["Prob"] = prob;
                                break;

                            case "e-value":
                                evalue = Convert.ToDouble(equationFields[1]);
                                if (evalue < doubleLimit)
                                {
                                    evalue = 0;
                                }
                                hitRow["Evalue"] = evalue;
                                break;

                            case "score":
                                hitRow["Score"] = equationFields[1];
                                break;

                            case "aligned_cols":
                                hitRow["AlignLength"] = equationFields[1];
                                break;

                            case "identities":
                                hitRow["Identity"] = equationFields[1].TrimEnd('%');
                                break;

                            case "similarity":
                                hitRow["Similarity"] = equationFields[1];
                                break;

                            case "sum_probs":
                                hitRow["Sum_Probs"] = equationFields[1];
                                break;

                            default:
                                break;
                        }
                    }
                    hitNo++;
                    continue;
                }
                string[] alignFields = ParseHelper.SplitPlus(line, ' ');
                if (alignFields.Length == 0)
                {
                    continue;
                }
                // match, hope all of three characters are available
                // the match line can be empty
                if (alignFields[0] != "Q" && isAlignStart &&
                    (line.IndexOf("+") > -1 || line.IndexOf("|") > -1 || line.IndexOf(".") > -1))
                {
                    match += line.Substring(22, line.Length - 22);
                    continue;
                }

                if (alignFields[0] == "Q")
                {
                    isAlignStart = true;
                    //      if (alignFields[1] == seqSumFields[1])
                    if (seqSumFields[1].IndexOf(alignFields[1]) > -1)
                    {
                        // no query sequence aligned, add an empty line of match
               /*         if (IsAlignmentEmpty(alignFields[3]))
                        {
                            matchLine = GetEmptyMatchLine(alignFields[3].Length);

                            match += matchLine;
                        }*/
                        queryAlignment += alignFields[3];
                    }
                    else if (alignFields[1].ToLower() == "consensus")
                    {
                        queryConsensus += alignFields[3];
                    }
                    else if (alignFields[1].ToLower() == "ss_dssp")
                    {
                        queryDssp += alignFields[2];
                    }
                    continue;
                }
                else if (alignFields[0] == "T")
                {
                    switch (alignFields[1].ToLower())
                    {
                        case "consensus":
                            hitConsensus += alignFields[3];
                            // no model sequence aligned, add an empty line of match
                /*            if (IsAlignmentEmpty(alignFields[3]))
                            {
                                matchLine = GetEmptyMatchLine(alignFields[3].Length);
                                match += matchLine;
                            }*/
                            break;

                        case "ss_dssp":
                            hitDssp += alignFields[2];
                            break;

                        default:
                            hitAlignment += alignFields[3];
                            break;
                    }
                    continue;
                }
                else if (alignFields[0].ToLower() == "confidence")
                {
                    confidence += line.Substring(22, line.Length - 22);
                }
            }
            if (hitRow != null)
            {                
                hitRow["QueryAlignment"] = queryAlignment;                
                hitRow["HitAlignment"] = hitAlignment;
                /*
                hitRow["QueryConsensus"] = queryConsensus;
                hitRow["HitConsensus"] = hitConsensus;
                hitRow["QueryDssp"] = queryDssp;
                hitRow["HitDssp"] = hitDssp;
                hitRow["Confidence"] = confidence;
                hitRow["Match"] = match;*/
            }

            dataReader.Close();

            if (deleteTempHhr)
            {
                File.Delete(hhrFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="consensusAlignment"></param>
        /// <returns></returns>
        public bool IsAlignmentEmpty(string alignment)
        {
            foreach (char ch in alignment)
            {
                if (ch != '-')
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignLength"></param>
        /// <returns></returns>
        public string GetEmptyMatchLine(int alignLength)
        {
            string matchLine = "";
            for (int i = 0; i < alignLength; i++)
            {
                matchLine += " ";
            }
            return matchLine;
        }
        /// <summary>
        ///     
        /// </summary>
        /// <param name="sumInfoLine"></param>
        /// <returns></returns>
        public string[] SplitHhrSumInfoLine(string sumInfoLine)
        {
            // this assumes that the line is seperated by space
            string[] fields = ParseHelper.SplitPlus(sumInfoLine, ' ');
            // the description field may contain space
            string statSubLine = sumInfoLine.Substring(34, sumInfoLine.Length - 34).TrimStart();
            string[] statFields = statSubLine.Split(" ()".ToCharArray());

            List<string> sumInfoItemList = new List<string> ();
            // No: 0
            sumInfoItemList.Add(fields[0]);
            // Hit: 1
            sumInfoItemList.Add(fields[1]);
            // skip description field, which is fields[2]
            foreach (string statField in statFields)
            {
                if (statField == "")
                {
                    continue;
                }
                if (statField.IndexOf ("E-") > -1)
                {
                    sumInfoItemList.Add(statField);
                }
                else if (statField.IndexOf ("-") > -1 && statField[0] != '-')
                {
                    string[] rangeFields = statField.Split('-');
                    sumInfoItemList.AddRange(rangeFields);
                }
                else
                {
                    sumInfoItemList.Add(statField);
                }
            }           

            string[] sumInfoFields = new string[sumInfoItemList.Count];
            sumInfoItemList.CopyTo(sumInfoFields);
            return sumInfoFields;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sumInfoFields"></param>
        /// <param name="hhAlignTable"></param>
        public void AddHhAlignSumInfoToTable(string seqName, int queryLength, string[] sumInfoFields, DataTable hhAlignTable)
        {
            DataRow hhAlignRow = hhAlignTable.NewRow();          
            hhAlignRow["HitNo"] = sumInfoFields[0];
            hhAlignRow["Query"] = seqName;
            hhAlignRow["Hit"] = sumInfoFields[1];
            hhAlignRow["Prob"] = sumInfoFields[2];
            hhAlignRow["QueryLength"] = queryLength;
            double evalue = Convert.ToDouble(sumInfoFields[3]);
            if (evalue <= doubleLimit)
            {
                evalue = 0;
            }
            hhAlignRow["Evalue"] = evalue;
            double pvalue = Convert.ToDouble(sumInfoFields[4]);
            if (pvalue <= doubleLimit)
            {
                pvalue = 0;
            }
            hhAlignRow["Pvalue"] = pvalue;
            hhAlignRow["Score"] = sumInfoFields[5];
            hhAlignRow["AlignLength"] = sumInfoFields[7];
            hhAlignRow["QueryStart"] = sumInfoFields[8];
            hhAlignRow["QueryEnd"] = sumInfoFields[9];
            hhAlignRow["HitStart"] = sumInfoFields[10];
            hhAlignRow["HitEnd"] = sumInfoFields[11];
            hhAlignRow["HitLength"] = sumInfoFields[12];
            hhAlignTable.Rows.Add(hhAlignRow);
        }
        #endregion

        #region create tables in memory and db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        public DataTable CreateHHAlignmentTable(bool isUpdate, string tableName)
        {
            string[] hhrAlignColumns = {"HitNo", "Query", "Hit", "QueryLength", "HitLength", "AlignLength", 
                                         "Identity", "Evalue", "PValue", "Score",   "Similarity", "Prob", 
                                        "Sum_Probs","QueryStart", "QueryEnd", "HitStart", "HitEnd", 
                                        "QueryAlignment", "HitAlignment"/*, "QueryConsensus", "HitConsensus",
                                        "QueryDssp", "HitDssp", "Confidence", "Match"*/};
            DataTable pfamHHalignTable = new DataTable(tableName);
            foreach (string hhCol in hhrAlignColumns)
            {
                pfamHHalignTable.Columns.Add(new DataColumn (hhCol));
            }
          
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createIndexString = "";
                string createTableString = "CREATE TABLE " + tableName + " ( " +
                       "HitNO INTEGER NOT NULL, " +
                       "Query VARCHAR(32) NOT NULL," +
                       "Hit VARCHAR(32) NOT NULL, " +
                       "QueryLength INTEGER, " +
                       "HitLength INTEGER, " +
                       "AlignLength INTEGER, " +
                       "Identity FLOAT, " +
                       "EValue  DOUBLE PRECISION, " +
                       "PValue DOUBLE PRECISION, " +
                       "Score DOUBLE PRECISION, " +
                       "Similarity FLOAT, " +
                       "Prob FLOAT, " +
                       "Sum_Probs FLOAT, " +
                       "QueryStart INTEGER, " + // for the input sequence
                       "QueryEnd INTEGER, " +
                       "HitStart INTEGER, " +
                       "HitEnd INTEGER, " +
                       "QueryAlignment BLOB Sub_Type 1, " +
                       "HitAlignment BLOB Sub_Type 1);";
                  /*     "QueryConsensus BLOB Sub_Type 1, " +
                       "HitConsensus BLOB Sub_Type 1, " +
                       "QueryDssp BLOB Sub_Type 1, " +
                       "HitDssp BLOB Sub_Type 1, " +
                       "Confidence BLOB Sub_Type 1, " + 
                       "Match BLOB Sub_Type 1);";   */
                dbCreate.CreateTableFromString(ProtCidSettings.alignmentDbConnection, createTableString, tableName);
                createIndexString = "CREATE Index " + tableName + "_query ON " + tableName + "(Query)";
                dbCreate.CreateIndex(ProtCidSettings.alignmentDbConnection, createIndexString, tableName);
                createIndexString = "CREATE Index " + tableName + "_hit ON " + tableName + "(Hit)";
            }
            return pfamHHalignTable;
        }
        #endregion
    }
}
