using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using DataCollectorLib.FatcatAlignment;
using CrystalInterfaceLib.DomainInterfaces;
using ProgressLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.Pfam
{
    public class PfamDomainAlignments : FatcatAlignmentParser
    {
        #region member variables
        private string pfamDbTable = "";
        private Dictionary<string, long[]> entryMultiDomainHash = new Dictionary<string,long[]> ();
        public string[] alignTableNames = new string[2];

        public PfamDomainAlignments()
        {
            dbAlignTableName = "PfamDomainAlignments";
            pfamDbTable = "PdbPfam";
            entryMultiDomainHash = GetEntryMultiChainDomainIds();            
        }

        public PfamDomainAlignments(string domainType)
        {
            dbAlignTableName = "PfamDomainAlignments";
            if (domainType == "")
            {
                pfamDbTable = "PdbPfam";
            }
            else
            {
                pfamDbTable = "PfamPdb" + domainType;
            }
            entryMultiDomainHash = GetEntryMultiChainDomainIds();
        }

        public PfamDomainAlignments(string domainType, string dbTableName)
        {
            dbAlignTableName = dbTableName;
            if (domainType == "")
            {
                pfamDbTable = "PdbPfam";
            }
            else
            {
                pfamDbTable = "PfamPdb" + domainType;
            }
            entryMultiDomainHash = GetEntryMultiChainDomainIds();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignDbConnect"></param>
        public PfamDomainAlignments (DbConnect alignDbConnect)
        {
            dbAlignTableName = "PfamDomainAlignments";
            pfamDbTable = "PdbPfam";
            entryMultiDomainHash = GetEntryMultiChainDomainIds();

            alignmentDbConnection = alignDbConnect;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, long[]> GetEntryMultiChainDomainIds()
        {
            string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) As EntityCount From PdbPfam Group By PdbID, DomainID;";
            DataTable domainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string pdbId = "";
            long domainId = 0;
            int entityCount = 0;
            Dictionary<string, List<long>> entryMultiChainDomainListHash = new Dictionary<string,List<long>> ();
            foreach (DataRow domainRow in domainTable.Rows)
            {
                entityCount = Convert.ToInt32(domainRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pdbId = domainRow["PdbID"].ToString();
                    domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                    if (entryMultiChainDomainListHash.ContainsKey(pdbId))
                    {
                        entryMultiChainDomainListHash[pdbId].Add(domainId);
                    }
                    else
                    {
                        List<long> multiChainDomainList = new List<long> ();
                        multiChainDomainList.Add(domainId);
                        entryMultiChainDomainListHash.Add(pdbId, multiChainDomainList);
                    }

                }
            }
            Dictionary<string, long[]> entryMultiChainDomainHash = new Dictionary<string, long[]>();
            foreach (string lsEntry in entryMultiChainDomainListHash.Keys)
            {
                entryMultiChainDomainHash.Add (lsEntry, entryMultiChainDomainListHash[lsEntry].ToArray ());
            }
            return entryMultiChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        public void SetFatcatTableName (string tableName)
        {
            FatcatTables.fatcatAlignTable.TableName = tableName;
        }
        #endregion

        #region domain alignments in XML sequential numbers
        /// <summary>
        /// 
        /// </summary>
        public void ParsePfamAlignFiles(bool isUpdate, string domainType, string pfamOrClan)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            string alignFileDir = Path.Combine (ProtCidSettings.dirSettings.pfamPath, "FatcatAlign");
            
            if (pfamOrClan.IndexOf("hhweak") > -1)
            {
                alignFileDir = Path.Combine(alignFileDir, "weakDomains");
                alignTableNames[1] = "PfamHHWeakDomainAlignmentsRigid";
                alignTableNames[0] = "PfamHHWeakDomainAlignments";
                dbAlignTableName = "PfamWeakDomainAlignments";
            }
            else if (pfamOrClan.IndexOf("weak") > -1)
            {
                alignFileDir = Path.Combine(alignFileDir, "weakDomains\\");
                alignTableNames[1] = "PfamWeakDomainAlignmentsRigid";
                alignTableNames[0] = "PfamWeakDomainAlignments";
                dbAlignTableName = "PfamWeakDomainAlignments";
            }
            else
            {
                alignFileDir = Path.Combine(alignFileDir, "PfamDomains\\" + pfamOrClan.Substring(0, 4) + "StructAlign");
                alignTableNames[0] = "PfamDomainAlignments";
                alignTableNames[1] = "PfamDomainAlignmentsRigid";
                dbAlignTableName = "PfamDomainAlignments";
            }

            if (domainType == "")
            {
                pfamDbTable = "PdbPfam";
            }
            else
            {
                pfamDbTable = "PfamPdb" + domainType;
            }

            Initialize(isUpdate);

            logWriter.WriteLine ("Parsing Pfam Alignments files: ");

            string dataPath = "";
            foreach (string alignTableName in alignTableNames)
            {
                FatcatTables.fatcatAlignTable.TableName = alignTableName;
                dbAlignTableName = alignTableName;

                if (alignTableName.IndexOf("Rigid") > -1)
                {
                    dataPath = Path.Combine(alignFileDir, "rigid");
                }
                else
                {
                    dataPath = Path.Combine(alignFileDir, "flexible");
                }
                string[] alignFiles = System.IO.Directory.GetFiles(dataPath, "*.aln");

                logWriter.WriteLine (dbAlignTableName);
                logWriter.WriteLine (alignFiles.Length.ToString ());
                int fileCount = 1;
                foreach (string alignFile in alignFiles)
                {
                    logWriter.WriteLine (fileCount.ToString () + ": " + alignFile);

                    ParsePfamFatcatAlignmentFile(alignFile, isUpdate, logWriter);
                    try
                    {
                        MoveParsedAlignFileToParsedFolder(alignFile);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine ("Move " + alignFile + " error: " + ex.Message);
                        logWriter.Flush();
                    }                   
                    GC.Collect();
                    fileCount++;
                }
            }
            alignmentDbConnection.DisconnectFromDatabase();
            logWriter.WriteLine("Parsing done!");
            logWriter.Close();
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="alignFiles"></param>
       /// <param name="isUpdate"></param>
       /// <param name="domainType"></param>
        public void ParsePfamAlignFiles(string[] alignFiles, string domainType, bool isUpdate)
        {
            if (domainType == "")
            {
                pfamDbTable = "PdbPfam";
            }
            else
            {
                pfamDbTable = "PfamPdb" + domainType;
            }
         
            logWriter.WriteLine ("Parsing Pfam Alignments files: ");

            logWriter.WriteLine("#files to be parsed: " + alignFiles.Length.ToString()); ;
            int fileCount = 1;
            foreach (string alignFile in alignFiles)
            {
                logWriter.WriteLine (fileCount.ToString () + ": " + alignFile);
  
                ParsePfamFatcatAlignmentFile(alignFile, isUpdate, logWriter);

                logWriter.Flush();
                GC.Collect();

                fileCount ++;
            }
            alignmentDbConnection.DisconnectFromDatabase();
            logWriter.WriteLine("parsing done!");
            logWriter.Close();
        }

        /// <summary>
        /// parse one fatcat alignment output file
        /// insert data into database
        /// </summary>
        /// <param name="alignFile"></param>
        public void ParsePfamFatcatAlignmentFile(string alignFile, bool isUpdate, StreamWriter logWriter)
        {
            StreamReader dataReader = new StreamReader(alignFile);
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
            DomainAlignSeqInfo alignInfo1 = new DomainAlignSeqInfo ();
            DomainAlignSeqInfo alignInfo2 = new DomainAlignSeqInfo ();
            DataRow dataRow = null;
            string dataLine = "";
            // the asymchain and startpos for this domain
            Dictionary<long, string[]> domainChainInfoHash = new Dictionary<long,string[]> ();
            int rowCount = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }

                try
                {
                    dataLine += (line + "\r\n");
                    if (line.IndexOf("Align") > -1 &&
                        line.Substring(0, "Align".Length) == "Align")
                    {
                        fields = ParseHelper.SplitPlus(line, ' ');
                        // domain 1
                        string[] domainInfo1 = ParseDomainName(fields[1], ref domainChainInfoHash);

                        rowCount++;
                        dataRow = FatcatTables.fatcatAlignTable.NewRow();
                        dataRow["QueryEntry"] = domainInfo1[0];
                        dataRow["QueryDomainID"] = domainInfo1[1];
                        dataRow["QueryEntity"] = domainInfo1[2];
                        dataRow["QueryDomainStart"] = domainInfo1[3];
                        dataRow["QueryLength"] = fields[2];
                        // domain 2
                        string[] domainInfo2 = ParseDomainName(fields[4], ref domainChainInfoHash);
                        dataRow["HitEntry"] = domainInfo2[0];
                        dataRow["HitDomainID"] = domainInfo2[1];
                        dataRow["HitEntity"] = domainInfo2[2];
                        dataRow["HitDomainStart"] = domainInfo2[3];
                        dataRow["HitLength"] = fields[5];
                        alignInfo1.pdbId = fields[1].Substring(0, 4);
                        alignInfo1.asymChainId = domainInfo1[2];
                        alignInfo1.domainId = Convert.ToInt64 (domainInfo1[1]);
                        alignInfo2.pdbId = fields[4].Substring(0, 4);
                        alignInfo2.asymChainId = domainInfo2[2];
                        alignInfo2.domainId = Convert.ToInt64 (domainInfo2[1]);
                        alignSequence1 = "";
                        alignSequence2 = "";
                        chain1Started = true;
                        chain2Started = true;
                    }
                    scoreIdx = line.IndexOf("Score");
                    if (scoreIdx > -1)
                    {
                        // from opt-equ, equivalent positions
                        //	dataRow["AlignmentLength"] = 
                        alignLenIdx = line.IndexOf("align-len");
                        gapIdx = line.IndexOf("gaps");
                        gapEndIdx = line.LastIndexOf("(");
                        dataRow["Score"] = line.Substring(scoreIdx + "Score".Length + 1, alignLenIdx - scoreIdx - "Score".Length - 1);
                        dataRow["Align_Len"] = line.Substring(alignLenIdx + "align-len".Length + 1,
                            gapIdx - alignLenIdx - "align-len".Length - 2);
                        dataRow["Gaps"] = line.Substring(gapIdx + "gaps".Length + 1, gapEndIdx - gapIdx - "gaps".Length - 2);
                    }
                    if (line.IndexOf("P-value") > -1)
                    {
                        fields = ParseHelper.SplitPlus(line, ' ');
                        dataRow["E_Value"] = Convert.ToDouble(fields[1]);
                        dataRow["Identity"] = fields[5].TrimEnd('%');
                        dataRow["Similarity"] = fields[7].TrimEnd('%');
                    }
                    if (line.IndexOf("Chain 1:") > -1)
                    {
                        // contain alignStart and aligned sequence
                        fields = ParseChainAlignSeqLine(line);
                        if (chain1Started)
                        {
                            alignStart1 = ConvertSeqToInt(fields[0]);
                            chain1Started = false;
                        }
                        alignSequence1 += fields[1];
                        alignEnd1 = ConvertSeqToInt(fields[0]) + GetNonGapAlignedString(fields[1]).Length - 1;
                    }
                    if (line.IndexOf("Chain 2:") > -1)
                    {
                        fields = ParseChainAlignSeqLine(line);
                        if (chain2Started)
                        {
                            alignStart2 = ConvertSeqToInt(fields[0]);
                            chain2Started = false;
                        }

                        alignSequence2 += fields[1];
                        alignEnd2 = ConvertSeqToInt(fields[0]) + GetNonGapAlignedString(fields[1]).Length - 1;
                    }
                    if (line.IndexOf("Note:") > -1)
                    {
                        if (alignSequence1 == "")
                        {
                            continue;
                        }
                        alignInfo1.alignStart = alignStart1;
                        alignInfo1.alignEnd = alignEnd1;
                        alignInfo1.alignSequence = alignSequence1;
                    /*    if (alignInfo1.alignStart < 0)
                        {
                            FindStartEndPosition(dataRow["QueryEntry"].ToString(),
                                Convert.ToInt32 (dataRow["QueryEntity"].ToString()),
                                Convert.ToInt16(dataRow["QueryDomainStart"].ToString()), ref alignInfo1);
                        }*/
                        alignInfo2.alignStart = alignStart2;
                        alignInfo2.alignEnd = alignEnd2;
                        alignInfo2.alignSequence = alignSequence2;
                    /*    if (alignInfo2.alignStart < 0)
                        {
                            FindStartEndPosition(dataRow["HitEntry"].ToString(),
                                Convert.ToInt32 (dataRow["HitEntity"].ToString()),
                                Convert.ToInt16(dataRow["HitDomainStart"].ToString()), ref alignInfo2);
                        }*/
                        if (dbAlignTableName.IndexOf("WeakDomain") < 0)
                        {
                            try
                            {
                                seqConverter.AddDisorderResiduesToDomainAlignment(ref alignInfo1, ref alignInfo2);
                            }
                            catch (Exception ex)
                            {
                                logWriter.WriteLine (alignInfo1.pdbId + alignInfo1.domainId + " " +
                                    alignInfo2.pdbId + alignInfo2.domainId + " filling out disorder residues failed: " + ex.Message + "\n");
                            }
                        }
                        dataRow["AlignmentLength"] = GetAlignmentLength(alignSequence1, alignSequence2);
                        dataRow["QuerySequence"] = alignInfo1.alignSequence;
                        dataRow["HitSequence"] = alignInfo2.alignSequence;
                        dataRow["QueryStart"] = alignInfo1.alignStart;
                        dataRow["QueryEnd"] = alignInfo1.alignEnd;
                        dataRow["HitStart"] = alignInfo2.alignStart;
                        dataRow["HitEnd"] = alignInfo2.alignEnd;
                        // delete the previous data
                        if (isUpdate)
                        {
                            DeletePfamAlignment(dataRow["QueryEntry"].ToString(), Convert.ToInt64(dataRow["QueryDomainID"].ToString()),
                                dataRow["HitEntry"].ToString(), Convert.ToInt64(dataRow["HitDomainID"].ToString()));
                        }
                        
                        if (FatcatTables.fatcatAlignTable.Columns.Contains("QuerySeqNumbers"))
                        {
                            AddQueryHitSeqNumbers(dataRow);
                        }
                        FatcatTables.fatcatAlignTable.Rows.Add(dataRow);
                        alignSequence1 = "";
                        alignSequence2 = "";
                        dataLine = "";
                        if (rowCount == 10000)  // to prevent big memory usage
                        {
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
                            rowCount = 0;
                        }
                    }
                    if (line.IndexOf("#Time used") > -1)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine (ex.Message);
                    logWriter.WriteLine (line);
                    logWriter.WriteLine(dataLine);

                    dataLine = "";
                }
            }
            dataReader.Close();

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
        #endregion

        #region domain alignments file seqIds <--> XML seqids
        /// <summary>
        /// This can be simplified. 
        /// The residues in the domain interface can be numbered as the domain file from 1 to N. 
        /// Since now I use the domain interfaces for Q scores,
        /// should directly use the residue numbers in the domain file
        /// </summary>
        /// <param name="alignRow"></param>
        public void AddQueryHitSeqNumbers(DataRow alignRow)
        {
            string queryPdbId = alignRow["QueryEntry"].ToString();
            long queryDomainId = Convert.ToInt64(alignRow["QueryDomainID"].ToString ());

            int queryFileSeqStart = Convert.ToInt32(alignRow["QueryStart"].ToString ());
            string queryAlignment = alignRow["QuerySequence"].ToString();
            string nonGapQueryAlignment = GetNonGapAlignedString(queryAlignment);
            alignRow["QueryEnd"] = queryFileSeqStart + nonGapQueryAlignment.Length - 1;
            string querySeqNumbers = GetSeqNumbers(queryPdbId, queryDomainId, queryFileSeqStart, queryAlignment);

            string hitPdbId = alignRow["HitEntry"].ToString();
            long hitDomainId = Convert.ToInt64(alignRow["hitDomainID"].ToString ());
            int hitFileSeqStart = Convert.ToInt32(alignRow["HitStart"]);
            string hitAlignment = alignRow["HitSequence"].ToString();
            string nonGapHitAligment = GetNonGapAlignedString(hitAlignment);
            alignRow["HitEnd"] = hitFileSeqStart + nonGapHitAligment.Length - 1;
            string hitSeqNumbers = GetSeqNumbers(hitPdbId, hitDomainId, hitFileSeqStart, hitAlignment);

            alignRow["QuerySeqNumbers"] = querySeqNumbers;
            alignRow["HitSeqNumbers"] = hitSeqNumbers;

      //      alignRow.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="fileSeqStart"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public string GetSeqNumbers(string pdbId, long domainId, int fileSeqStart, string alignment)
        {
            int[] fileSeqNumbers = GetFileSeqNumbers(alignment, fileSeqStart);
            string seqNumbers = "";

            if (!IsMultiChainDomain(pdbId, domainId))
            {
                string queryString = string.Format("Select * From PdbPfamDomainFileInfo " +
                    " Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
                DataTable fileSeqInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                if (AreDomainFileRegionsSame(fileSeqInfoTable))
                {
                    seqNumbers = ConvertSeqNumbersToString(fileSeqNumbers);
                }
                else
                {
                    seqNumbers = ConvertFileSeqIdsToXml(fileSeqNumbers, fileSeqInfoTable);
                }
            }
            else  // if it is multi-chain domain, then use the sequence numbers in the file
            {
                seqNumbers = ConvertSeqNumbersToString(fileSeqNumbers);
            }
            return seqNumbers.TrimEnd(',');
        }

        /// <summary>
        /// format seq numbers into a string
        /// </summary>
        /// <param name="fileSeqNumbers"></param>
        /// <returns></returns>
        private string ConvertSeqNumbersToString(int[] fileSeqNumbers)
        {
            string seqNumbers = "";
            for (int i = 0; i < fileSeqNumbers.Length; i++)
            {
                if (fileSeqNumbers[i] != -1)
                {
                    seqNumbers += (fileSeqNumbers[i].ToString() + ",");
                }
                else
                {
                    seqNumbers += "-,";
                }
            }
            return seqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFileInfoTable"></param>
        /// <returns></returns>
        private bool AreDomainFileRegionsSame(DataTable domainFileInfoTable)
        {
            if (domainFileInfoTable.Rows.Count == 1)
            {
                int seqStart = Convert.ToInt32(domainFileInfoTable.Rows[0]["SeqStart"].ToString ());
                int fileStart = Convert.ToInt32(domainFileInfoTable.Rows[0]["FileStart"].ToString ());
                if (seqStart == fileStart)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileSeqNumbers"></param>
        /// <param name="fileSeqInfoTable"></param>
        /// <returns></returns>
        private string ConvertFileSeqIdsToXml(int[] fileSeqNumbers, DataTable fileSeqInfoTable)
        {
            Range[] fileRegions = new Range[fileSeqInfoTable.Rows.Count];
            Range[] seqRegions = new Range[fileSeqInfoTable.Rows.Count];
            int count = 0;
            int seqNumber = 0;
            string seqNumbers = "";
            foreach (DataRow seqInfoRow in fileSeqInfoTable.Rows)
            {
                Range seqRegion = new Range();
                seqRegion.startPos = Convert.ToInt32(seqInfoRow["SeqStart"].ToString());
                seqRegion.endPos = Convert.ToInt32(seqInfoRow["SeqEnd"].ToString());
                seqRegions[count] = seqRegion;

                Range fileRegion = new Range();
                fileRegion.startPos = Convert.ToInt32(seqInfoRow["FileStart"].ToString());
                fileRegion.endPos = Convert.ToInt32(seqInfoRow["FileEnd"].ToString());
                fileRegions[count] = fileRegion;
                count++;
            }

            for (int i = 0; i < fileSeqNumbers.Length; i++)
            {
                if (fileSeqNumbers[i] != -1)
                {
                    seqNumber = GetSeqNumber(fileSeqNumbers[i], fileRegions, seqRegions); // the sequence number in the XML file
                    seqNumbers += (seqNumber.ToString() + ",");
                }
                else
                {
                    seqNumbers += "-,";
                }
            }
            return seqNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private bool IsMultiChainDomain(string pdbId, long domainId)
        {
            if (entryMultiDomainHash.ContainsKey(pdbId))
            {
                long[] multiChainDomainIds = (long[])entryMultiDomainHash[pdbId];
                if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileSeqNumber"></param>
        /// <param name="fileRegions"></param>
        /// <param name="seqRegions"></param>
        /// <returns></returns>
        private int GetSeqNumber(int fileSeqNumber, Range[] fileRegions, Range[] seqRegions)
        {
            int seqNumber = -1;
            for (int i = 0; i < fileRegions.Length; i++)
            {
                if (fileSeqNumber <= fileRegions[i].endPos && fileSeqNumber >= fileRegions[i].startPos)
                {
                    seqNumber = GetSeqNumber(fileSeqNumber, fileRegions[i], seqRegions[i]);
                    break;
                }
            }
            return seqNumber;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileSeqNumber"></param>
        /// <param name="fileRegion"></param>
        /// <param name="seqRange"></param>
        /// <returns></returns>
        private int GetSeqNumber(int fileSeqNumber, Range fileRegion, Range seqRange)
        {
            int seqNumber = fileSeqNumber + seqRange.startPos - fileRegion.startPos;
            return seqNumber;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignment"></param>
        /// <param name="fileSeqStart"></param>
        /// <param name="fileSeqEnd"></param>
        /// <returns></returns>
        private int[] GetFileSeqNumbers(string alignment, int fileSeqStart)
        {
            List<int> fileSeqNumberList = new List<int> ();
            int residueCount = 0;
            for (int i = 0; i < alignment.Length; i++)
            {
                if (alignment[i] == '-')
                {
                    fileSeqNumberList.Add(-1);
                }
                else
                {
                    fileSeqNumberList.Add (fileSeqStart + residueCount);
                    residueCount++;
                }
            }
            return fileSeqNumberList.ToArray ();
        }
        #endregion

        #region deal with domain alignments
       
        /// <summary>
        /// the asymmetric chain name from the pfam domain name
        /// which has the format 1xxxChainStartPos.pfam
        /// chain name may have more than one characters
        /// <param name="domainFile"></param>
        /// <returns></returns>
        private string[] ParseDomainName(string domainFile, ref Dictionary<long, string[]> domainChainInfoHash)
        {
            string[] domainNameInfo = new string[4];
            domainNameInfo[0] = domainFile.Substring(0, 4); // pdb entry code
            int exeIdx = domainFile.IndexOf(".pfam");
            domainNameInfo[1] = domainFile.Substring (4, exeIdx - 4); // domain ID
            string[] chainInfos = GetDomainChainInfo (domainNameInfo[0], Convert.ToInt64 (domainNameInfo[1]), ref domainChainInfoHash);
            domainNameInfo[2] =  chainInfos[0];// entity id
            domainNameInfo[3] = chainInfos[1]; // the start position of the domain
            return domainNameInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="alignInfo"></param>
        private void FindEndPosition(string pdbId, int entityId, ref AlignSeqInfo alignInfo)
        {
            string noGapAlignSequence = GetNonGapAlignedString(alignInfo.alignSequence);
            string sequenceInCoord = GetChainSequenceInCoordinates(pdbId, entityId);
            string noGapCoordSeq = "";
            Dictionary<int, int> coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, alignInfo.alignStart);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence, alignInfo.alignStart - 1);
            if (alignStartIdx > -1)
            {
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = coordSeqIdSeqIdHash[alignEndIdx + 1];
                alignInfo.alignEnd = alignEnd;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignInfo"></param>
        private void FindEndPosition(ref AlignSeqInfo alignInfo)
        {
            string noGapAlignSequence = GetNonGapAlignedString(alignInfo.alignSequence);
            string sequenceInCoord = GetChainSequenceInCoordinates(alignInfo.pdbId, Convert.ToInt32 (alignInfo.asymChainId));
            string noGapCoordSeq = "";
            Dictionary<int, int> coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, alignInfo.alignStart);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence);
            if (alignStartIdx > -1)
            {
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = coordSeqIdSeqIdHash[alignEndIdx];
                alignInfo.alignEnd = alignEnd;
            }
        }

        /// <summary>
        /// the aligned seqeuence only for those with coordinates
        /// Fatcat don't provide the end position which is in the PDB file
        /// have to find the real start and end positions in XML residue sequential number
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="startPos"></param>
        /// <param name="alignInfo"></param>
        private void FindStartEndPosition(string pdbId, int entityID, int startPos, ref DomainAlignSeqInfo alignInfo)
        {
            string noGapAlignSequence = GetNonGapAlignedString(alignInfo.alignSequence);
            string sequenceInCoord = GetChainSequenceInCoordinates(pdbId, entityID);
            string noGapCoordSeq = "";
            Dictionary<int, int> coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, startPos);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence);
            if (alignStartIdx > -1)
            {
                alignInfo.alignStart = coordSeqIdSeqIdHash[alignStartIdx + 1];
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = coordSeqIdSeqIdHash[alignEndIdx];
                alignInfo.alignEnd = alignEnd;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetChainSequenceInCoordinates(string pdbId, int entityId)
        {
            string queryString = string.Format("Select SequenceInCoord From AsymUnit " +
                " WHere PdbID = '{0}' AND EntityID = {1} AND PolymerType = 'polypeptide';", pdbId, entityId );
            DataTable seqInCoordTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (seqInCoordTable.Rows.Count > 0)
            {
                return seqInCoordTable.Rows[0]["SequenceInCoord"].ToString().TrimEnd();
            }
            else
            {
                return "-";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqInCoord"></param>
        /// <param name="noGapCoordSeq"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetCoordSeqToSeqHash(string seqInCoord, out string noGapCoordSeq, int startPos)
        {
            Dictionary<int, int> seqCoordSeqHash = new Dictionary<int,int> ();
            noGapCoordSeq = "";
            int coordSeqId = 0;
            for (int i = startPos - 1; i < seqInCoord.Length; i++)
            {
                if (seqInCoord[i] == '-')
                {
                    continue;
                }
                coordSeqId++;
                // the sequential id for each residue with coordinates
                // only those residues can be aligned
                seqCoordSeqHash.Add(coordSeqId, i + 1);
                noGapCoordSeq += seqInCoord[i].ToString();
            }
            return seqCoordSeqHash;
        }

        /// <summary>
        /// the asymmetric chain and the start pos for this domain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="domainChainInfoHash"></param>
        /// <returns></returns>
        private string[] GetDomainChainInfo(string pdbId, Int64 domainId, ref Dictionary<long, string[]> domainChainInfoHash)
        {
            string[] asymChainInfos = new string[2];
            if (domainChainInfoHash.ContainsKey(domainId))
            {
                asymChainInfos = (string[])domainChainInfoHash[domainId];
            }
            else
            {
                string queryString = string.Format("Select EntityID, AlignStart From {0} " +
                    " Where PdbID = '{1}' AND DomainID = {2};", pfamDbTable, pdbId, domainId);
                DataTable domainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

                if (domainInfoTable.Rows.Count == 0)
                {
                    queryString = string.Format("Select EntityID, AlignStart From {0} " +
                            " Where PdbID = '{1}' AND DomainID = {2};", pfamDbTable + "Weak", pdbId, domainId);
                    domainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                }

                if (domainInfoTable.Rows.Count > 0)
                {
                    asymChainInfos[0] = domainInfoTable.Rows[0]["EntityID"].ToString();
                    asymChainInfos[1] = domainInfoTable.Rows[0]["AlignStart"].ToString();
                }
                else
                {
                    asymChainInfos[0] = "-1";
                    asymChainInfos[1] = "-1";
                }
                domainChainInfoHash.Add(domainId, asymChainInfos);
            }
            return asymChainInfos;
        } 

        /// <summary>
        /// Format: 
        /// Chain 1: 1052 LLEAYLLVKWRMCEAREPSVDLRLPLCAGIDPLNSDPFLKMVSVGPMLQSTRKYFAQTLFMAKTVSGLDV
        ///               or
        /// Chain 2:A1052 LLEAYLLVKWRMCEAREPSVDLRLPLCAGIDPLNSDPFLKMVSVGPMLQSTRKYFAQTLFMAKTVSGLDV
        /// </summary>
        /// <param name="dataLine"></param>
        /// <returns></returns>
        private string[] ParseChainAlignSeqLine(string dataLine)
        {
            int chainIdx = dataLine.IndexOf(":");
            string seqNumString = dataLine.Substring(chainIdx + 1, dataLine.Length - chainIdx - 1);
            string[] seqNumAlignSeq = ParseHelper.SplitPlus(seqNumString, ' ');
            return seqNumAlignSeq;
        }

        /// <summary>
        /// the query and hit PDBIDs are in the alphabet order
        /// </summary>
        /// <param name="dataRow"></param>
        /// <returns></returns>
        private bool IsRowNeededToBeReversed(DataRow dataRow)
        {
            string queryEntry = dataRow["QueryEntry"].ToString();
            string hitEntry = dataRow["HitEntry"].ToString();
            // the alignment need to be reversed
            if (string.Compare(queryEntry, hitEntry) > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignRow"></param>
        private void ReverseAlignRow(DataRow alignRow)
        {
            List<string> queryColumnList = new List<string> ();
            string upperColName = "";
            foreach (DataColumn alignCol in alignRow.Table.Columns)
            {
                upperColName = alignCol.ColumnName.ToUpper();
                if (upperColName.Length > 5 && upperColName.Substring(0, 5) == "QUERY")
                {
                    queryColumnList.Add(upperColName);
                }
            }
            string hitCol = "";
            foreach (string queryCol in queryColumnList)
            {
                hitCol = queryCol.Replace("QUERY", "HIT");
                object temp = alignRow[queryCol];
                alignRow[queryCol] = alignRow[hitCol];
                alignRow[hitCol] = temp;
            }
        }
        #endregion

        #region delete alignments
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainId2"></param>
        private void DeletePfamAlignment(string pdbId1, long domainId1, string pdbId2, long domainId2)
        {
            string deleteString = string.Format("Delete From {0} " +
                " Where (QueryEntry = '{1}' AND QueryDomainID = {2} AND " +
                " HitEntry = '{3}' AND HitDomainID = {4}) OR " +
                " (HitEntry = '{1}' AND HitDomainID = {2} AND " +
                " QueryEntry = '{3}' AND QueryDomainID = {4});",
                dbAlignTableName, pdbId1, domainId1, pdbId2, domainId2);
            dbUpdate.Delete(alignmentDbConnection, deleteString);
        }

        // delete all the record about this weak domains
        private void DeletePfamAlignment(string pdbId, long domainId)
        {
            string deleteString = string.Format("Delete From {0} Where HitEntry = '{1}' AND HitDomainID = {2};",
                dbAlignTableName, pdbId, domainId);
            dbUpdate.Delete(alignmentDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        public void DeleteObsoleteDomainAlignments(long[] domainIds)
        {
            string deleteString = "";
            foreach (string alignTable in alignTableNames)
            {
                foreach (long domainId in domainIds)
                {
                    deleteString = string.Format("Delete From {0} WHERE QueryDomainID = {1} OR HitDomainID = {1};",
                        alignTable, domainId);
                    dbUpdate.Delete (alignmentDbConnection, deleteString);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomainIds"></param>
        public void DeleteObsoleteDomainAlignments(string[] entryDomainIds)
        {
            string deleteString = "";
            string pdbId = "";
            long domainId = 0;
            foreach (string alignTable in alignTableNames)
            {
                foreach (string entryDomainId in entryDomainIds)
                {
                    pdbId = entryDomainId.Substring(0, 4);
                    domainId = Convert.ToInt64(entryDomainId.Substring (4, entryDomainId.Length - 4));
                    deleteString = string.Format("Delete From {0} WHERE (QueryEntry = '{1}' AND QueryDomainID = {2}) " + 
                        " OR (HitEntry = '{1}' AND HitDomainID = {2});", alignTable, pdbId, domainId);
                    dbUpdate.Delete(alignmentDbConnection, deleteString);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomainIds"></param>
        public void DeleteObsoleteDomainAlignments(string[] entryDomainIds, string alignTableName, bool isRigid)
        {
            string deleteString = "";
            string pdbId = "";
            long domainId = 0;
            foreach (string entryDomainId in entryDomainIds)
            {
                pdbId = entryDomainId.Substring(0, 4);
                domainId = Convert.ToInt64(entryDomainId.Substring(4, entryDomainId.Length - 4));
                deleteString = string.Format("Delete From {0} WHERE (QueryEntry = '{1}' AND QueryDomainID = {2}) " +
                    " OR (HitEntry = '{1}' AND HitDomainID = {2});", alignTableName, pdbId, domainId);
                dbUpdate.Delete (alignmentDbConnection, deleteString);

                if (isRigid)
                {
                    deleteString = string.Format("Delete From {0} WHERE (QueryEntry = '{1}' AND QueryDomainID = {2}) " +
                    " OR (HitEntry = '{1}' AND HitDomainID = {2});", alignTableName + "Rigid", pdbId, domainId);
                    dbUpdate.Delete (alignmentDbConnection, deleteString);
                }
            }
        }
        #endregion

        #region move files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignFile"></param>
        public void MoveParsedAlignFileToParsedFolder(string alignFile)
        {
            FileInfo fileInfo = new FileInfo(alignFile);
            string parsedDir = "parsed" + DateTime.Today.Month.ToString().PadLeft(2, '0')
                + DateTime.Today.Day.ToString().PadLeft(2, '0') + DateTime.Today.Year.ToString();
            string destFilePath = Path.Combine(fileInfo.DirectoryName, parsedDir);
            if (!Directory.Exists(destFilePath))
            {
                Directory.CreateDirectory(destFilePath);
            }
            string destFileName = Path.Combine(destFilePath, fileInfo.Name);
            File.Move(alignFile, destFileName);
        }
        #endregion

        #region debug
        #region filling out the disorder residues in the FATCAT domain alignments
        private StreamWriter deletedAlignDataWriter = null;
        public void UpdateDomainAlignmentsWithDisorder()
        {
            alignTableNames[0] = "PfamDomainAlignments";
            alignTableNames[1] = "PfamDomainAlignmentsRigid";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            deletedAlignDataWriter = new StreamWriter("DeletedDomainAlignments.txt", true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(alignTableNames[0]);
            UpdateDomainAlignmentsWithDisorder(alignTableNames[0]);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            
            deletedAlignDataWriter = new StreamWriter("DeletedDomainAlignmentsRigid.txt", true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(alignTableNames[1]);
            UpdateDomainAlignmentsWithDisorder(alignTableNames[1]);
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainAlignmentsWithDisorder(string alignTableName)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            int numOfDisorderDomains = 0;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Read parsed domain list.");
            bool isRigid = false;
            if (alignTableName.ToLower().IndexOf("rigid") > -1)
            {
                isRigid = true;
            }
            string parsedPfamFile = "ParsedPfams.txt";
            if (isRigid)
            {
                parsedPfamFile = "ParsedPfamsRigid.txt";
            }
            string parsedDomainFile = "ParsedDomains.txt";
            if (isRigid)
            {
                parsedDomainFile = "ParsedDomainsRigid.txt";
            }
            Dictionary<string, string[]> parsedDomainHash = new Dictionary<string, string[]>();
            Dictionary<string, string[]> pfamDisorderDomainHash = ReadDisorderDomains(out numOfDisorderDomains, parsedPfamFile, 
                parsedDomainFile, out parsedDomainHash);

            StreamWriter pfamWriter =  new StreamWriter(parsedPfamFile, true);
            StreamWriter parsedDomainsWriter = new StreamWriter(parsedDomainFile, true);
          
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = numOfDisorderDomains;
            ProtCidSettings.progressInfo.totalStepNum = numOfDisorderDomains;
            ProtCidSettings.progressInfo.currentOperationLabel = "Disorder Alignments";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain alignments with disorder regions. ");

            List<string> pfamList = new List<string> (pfamDisorderDomainHash.Keys);
            pfamList.Sort();
            List<string> pfamParsedDomainList = new List<string> ();
            foreach (string pfamId in pfamList)
            {
                string[] domains = (string[])pfamDisorderDomainHash[pfamId];
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId);
                parsedDomainsWriter.WriteLine("#" + pfamId);
                pfamParsedDomainList.Clear();
                if (parsedDomainHash.ContainsKey(pfamId))
                {
                    string[] pfamParsedDomains = (string[])parsedDomainHash[pfamId];
                    pfamParsedDomainList.AddRange(pfamParsedDomains);
                }
                UpdateDomainAlignments(domains, alignTableName, parsedDomainsWriter, pfamParsedDomainList);
                pfamWriter.WriteLine(pfamId);
                pfamWriter.Flush();
            }
            deletedAlignDataWriter.Close();
            pfamWriter.Close();
            parsedDomainsWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <param name="alignTableName"></param>
        public void UpdateDomainAlignments(string[] domains, string alignTableName, StreamWriter parsedDomainsWriter, List<string> parsedDomainList)
        {
         //   ArrayList parsedDomainPairList = new ArrayList();
            string pdbId = "";
            long domainId = 0;
         //   ArrayList parsedDomainList = new ArrayList();
            foreach (string domain in domains)
            {
                ProtCidSettings.progressInfo.currentFileName = domain;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                pdbId = domain.Substring(0, 4);
                domainId = Convert.ToInt64(domain.Substring(4, domain.Length - 4));
                DataTable domainAlignTable = GetDomainAlignTable(pdbId, domainId, alignTableName);
                RemoveParsedDomainPairs(domainAlignTable, parsedDomainList);
                deletedAlignDataWriter.WriteLine(alignTableName);
                deletedAlignDataWriter.WriteLine(ParseHelper.FormatDataRows(domainAlignTable.Select()));
                deletedAlignDataWriter.Flush();
                try
                {
                //    UpdateDomainAlignments(domainAlignTable, ref parsedDomainPairList, alignTableName);
                    UpdateDomainAlignments(domainAlignTable, alignTableName);

                    parsedDomainsWriter.WriteLine(domain);
                    parsedDomainsWriter.Flush();
                    parsedDomainList.Add(domain);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain alignments for " + domain + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Update domain alignments for " + domain + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// removed those already parsed alignments
        /// </summary>
        /// <param name="alignTable"></param>
        /// <param name="parsedDomainPairList"></param>
        private void RemoveParsedDomainPairs(DataTable alignTable, List<string> parsedDomainList)
        {
            string queryDomain = "";
            string hitDomain = "";
            List<DataRow> removedDataRowList = new List<DataRow> ();
            foreach (DataRow alignRow in alignTable.Rows)
            {
                queryDomain = alignRow["QueryEntry"].ToString() + alignRow["QueryDomainID"].ToString();
                hitDomain = alignRow["HitEntry"].ToString() + alignRow["HitDomainID"].ToString();
                if (parsedDomainList.Contains(queryDomain) || parsedDomainList.Contains (hitDomain))
                {
                    removedDataRowList.Add(alignRow);
                }
            }
            foreach (DataRow removedDataRow in removedDataRowList)
            {
                alignTable.Rows.Remove(removedDataRow);
            }
            alignTable.AcceptChanges();
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainAlignTable"></param>
        /// <param name="parsedDomainPairList"></param>
        /// <param name="alignTableName"></param>
  //      private void UpdateDomainAlignments(DataTable domainAlignTable, ref ArrayList parsedDomainPairList, string alignTableName)
        private void UpdateDomainAlignments(DataTable domainAlignTable, string alignTableName)
        {
            string domainPair = "";
            List<string> domainPairToDeleteList = new List<string> ();
            List<DataRow> notParsedAlignRowList = new List<DataRow> ();
            for (int i = 0; i < domainAlignTable.Rows.Count; i++)
            {
                DataRow alignRow = domainAlignTable.Rows[i];

                domainPair = GetDomainPair(alignRow);
           /*     if (parsedDomainPairList.Contains(domainPair))
                {
                    notParsedAlignRowList.Add(alignRow);
                    continue;
                }
                parsedDomainPairList.Add(domainPair);
               */
                try
                {
                    DomainAlignSeqInfo[] alignInfos = GetDomainAlignInfos(alignRow);
                    seqConverter.AddDisorderResiduesToDomainAlignment(ref alignInfos[0], ref alignInfos[1]);
                    alignRow["QuerySequence"] = alignInfos[0].alignSequence;
                    alignRow["HitSequence"] = alignInfos[1].alignSequence;
                    AddQueryHitSeqNumbers(alignRow);

                    domainPairToDeleteList.Add(domainPair);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain alignments " + domainPair + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Update domain alignments " + domainPair + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    notParsedAlignRowList.Add(alignRow);
                    continue;
                }
            }
            
            foreach (DataRow notParsedAlignRow in notParsedAlignRowList)
            {
                domainAlignTable.Rows.Remove(notParsedAlignRow);
            }
            string[] domainPairsToBeDeleted = new string[domainPairToDeleteList.Count];
            domainPairToDeleteList.CopyTo(domainPairsToBeDeleted);
            DeleteDomainAlignments(domainPairsToBeDeleted, alignTableName);

            dbInsert.InsertDataIntoDBtables(alignmentDbConnection, domainAlignTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignRow"></param>
        /// <returns></returns>
        private string GetDomainPair(DataRow alignRow)
        {
            string entryDomain1 = alignRow["QueryEntry"].ToString() + alignRow["QueryDomainID"].ToString();
            string entryDomain2 = alignRow["HitEntry"].ToString() + alignRow["HitDomainID"].ToString();

            string domainPair = entryDomain1 + "_" + entryDomain2;
            if (string.Compare(entryDomain1, entryDomain2) > 0)
            {
                domainPair = entryDomain2 + "_" + entryDomain1;
            }
            return domainPair;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignRow"></param>
        /// <returns></returns>
        private DomainAlignSeqInfo[] GetDomainAlignInfos(DataRow alignRow)
        {
            DomainAlignSeqInfo alignInfo1 = new DomainAlignSeqInfo();
            alignInfo1.pdbId = alignRow["QueryEntry"].ToString();
            alignInfo1.domainId = Convert.ToInt64(alignRow["QueryDomainID"].ToString ());
            alignInfo1.alignSequence = alignRow["QuerySequence"].ToString();
            alignInfo1.alignStart = Convert.ToInt32(alignRow["QueryStart"].ToString ());
            alignInfo1.alignEnd = Convert.ToInt32(alignRow["QueryEnd"].ToString ());


            DomainAlignSeqInfo alignInfo2 = new DomainAlignSeqInfo();
            alignInfo2.pdbId = alignRow["HitEntry"].ToString();
            alignInfo2.domainId = Convert.ToInt64(alignRow["HitDomainID"].ToString());
            alignInfo2.alignSequence = alignRow["HitSequence"].ToString();
            alignInfo2.alignStart = Convert.ToInt32(alignRow["HitStart"].ToString());
            alignInfo2.alignEnd = Convert.ToInt32(alignRow["HitEnd"].ToString());

            DomainAlignSeqInfo[] alignInfos = new DomainAlignSeqInfo[2];
            alignInfos[0] = alignInfo1;
            alignInfos[1] = alignInfo2;
            return alignInfos;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="alignTableName"></param>
        /// <returns></returns>
        private DataTable GetDomainAlignTable(string pdbId, long domainId, string alignTableName)
        {
            string queryString = string.Format("Select * From {0} Where (QueryEntry = '{1}' AND QueryDomainID = {2}) OR " +
                " (HitEntry = '{1}' AND HitDomainID = {2});", alignTableName, pdbId, domainId);
            DataTable alignTable = dbQuery.Query(alignmentDbConnection, queryString);
            alignTable.TableName = alignTableName;
            return alignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadDisorderDomains(out int numOfDisorderDomains, string parsedPfamFile,
            string parsedDomainFile, out Dictionary<string, string[]> parsedDomainHash)
        {
            string[] parsedPfams = ReadParsedPfams(parsedPfamFile);
            parsedDomainHash = ReadParsedDomains(parsedDomainFile);
            Dictionary<string, string[]> pfamDisorderDomainHash = new Dictionary<string,string[]>  ();
            string domainFile = @"D:\DbProjectData\pfam\DomainAlign\DomainsWithDisorder.txt";
            StreamReader dataReader = new StreamReader(domainFile);
            string line = "";
            string pfamId = "";
            numOfDisorderDomains = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                pfamId = line;
                line = dataReader.ReadLine ();
                string[] domains = line.Split (',');
                if (Array.IndexOf(parsedPfams, pfamId) > -1)
                {
                    continue;
                }
                if (parsedDomainHash.ContainsKey(pfamId))
                {
                    string[] parsedDomains = (string[])parsedDomainHash[pfamId];
                    List<string> leftDomainsList = new List<string> (domains);
                    foreach (string parsedDomain in parsedDomains)
                    {
                        leftDomainsList.Remove(parsedDomain);
                    }
                    domains = new string[leftDomainsList.Count];
                    leftDomainsList.CopyTo(domains);
                }

                pfamDisorderDomainHash.Add (pfamId, domains);
                numOfDisorderDomains += domains.Length;
            }
            dataReader.Close();

            return pfamDisorderDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadParsedPfams(string parsedPfamFile)
        {
            List<string> pfamList = new List<string> ();
            if (File.Exists(parsedPfamFile))
            {
                StreamReader pfamReader = new StreamReader(parsedPfamFile);
                string line = "";

                while ((line = pfamReader.ReadLine()) != null)
                {
                    pfamList.Add(line);
                }
                pfamReader.Close();
            }
            return pfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadParsedDomains(string parsedDomainFile)
        {
            Dictionary<string, string[]> parsedDomainsHash = new Dictionary<string,string[]> ();
            if (File.Exists(parsedDomainFile))
            {
                string pfamId = "";
                List<string> domainList = new List<string> ();
                StreamReader domainReader = new StreamReader(parsedDomainFile);
                string line = "";
                while ((line = domainReader.ReadLine()) != null)
                {
                    if (line == "")
                    {
                        continue;
                    }
                    if (line[0] == '#')
                    {
                        if (pfamId != "")
                        {
                            if (parsedDomainsHash.ContainsKey(pfamId))
                            {
                                string[] existDomains = (string[])parsedDomainsHash[pfamId];
                                List<string> allDomainsList = new List<string> (existDomains);
                                allDomainsList.AddRange(domainList);
                                parsedDomainsHash[pfamId] = allDomainsList.ToArray ();
                            }
                            else
                            {
                                parsedDomainsHash.Add(pfamId, domainList.ToArray ());
                            }
                            domainList = new List<string> ();
                        }
                        pfamId = line.Substring(1, line.Length - 1);
                    }
                    domainList.Add(line);
                }
                domainReader.Close();
                if (domainList.Count > 0)
                {
                    if (parsedDomainsHash.ContainsKey(pfamId))
                    {
                        string[] existDomains = (string[])parsedDomainsHash[pfamId];
                        List<string> allDomainsList = new List<string> (existDomains);
                        allDomainsList.AddRange(domainList);                      
                        parsedDomainsHash[pfamId] = allDomainsList.ToArray ();
                    }
                    else
                    {
                        parsedDomainsHash.Add(pfamId, domainList.ToArray ());
                    }
                }
            }
            return parsedDomainsHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPairs"></param>
        /// <param name="alignTableName"></param>
        private void DeleteDomainAlignments(string[] domainPairs, string alignTableName)
        {
            string pdbId1 = "";
            long domainId1 = 0;
            string pdbId2 = "";
            long domainId2 = 0;
            foreach (string domainPair in domainPairs)
            {
                string[] fields = domainPair.Split('_');
                pdbId1 = fields[0].Substring(0, 4);
                domainId1 = Convert.ToInt64(fields[0].Substring (4, fields[0].Length - 4));
                pdbId2 = fields[1].Substring(0, 4);
                domainId2 = Convert.ToInt64(fields[1].Substring (4, fields[1].Length - 4));
                DeleteDomainAlignment(pdbId1, domainId1, pdbId2, domainId2, alignTableName);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainId2"></param>
        /// <param name="alignTableName"></param>
        private void DeleteDomainAlignment(string pdbId1, long domainId1, string pdbId2, long domainId2, string alignTableName)
        {
            string deleteString = string.Format("Delete From {0} Where QueryEntry = '{1}' AND QueryDomainID = {2} AND " +
                " HitEntry = '{3}' AND HitDomainID = {4};", alignTableName, pdbId1, domainId1, pdbId2, domainId2);
            dbUpdate.Delete(alignmentDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainPairAlignments()
        {
            alignTableNames[0] = "PfamDomainAlignments";
            alignTableNames[1] = "PfamDomainAlignmentsRigid";

            string domain1 = "2qki10236911";
            string domain2 = "3tkv10236919";
            
            string[] domainPairs = ReadDomainPairs();
            foreach (string domainPair in domainPairs)
            {
                string[] domainPairFields = domainPair.Split(',');
                domain1 = domainPairFields[0];
                domain2 = domainPairFields[1];
                DataRow updateAlignRow = UpdateDomainPairAlignments(domain1, domain2, alignTableNames[0]);
                DeleteDomainAlignment(domain1.Substring(0, 4), Convert.ToInt64(domain1.Substring(4, domain1.Length - 4)),
                    domain2.Substring(0, 4), Convert.ToInt64(domain2.Substring(4, domain2.Length - 4)), alignTableNames[0]);
                dbInsert.InsertDataIntoDb(alignmentDbConnection, updateAlignRow);

                DataRow updateRigidAlignRow = UpdateDomainPairAlignments(domain1, domain2, alignTableNames[1]);
                DeleteDomainAlignment(domain1.Substring(0, 4), Convert.ToInt64(domain1.Substring(4, domain1.Length - 4)),
                    domain2.Substring(0, 4), Convert.ToInt64(domain2.Substring(4, domain2.Length - 4)), alignTableNames[1]);
                dbInsert.InsertDataIntoDb(alignmentDbConnection, updateAlignRow);
            }
        }

    

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadDomainPairs()
        {
            StreamReader domainPairReader = new StreamReader(@"D:\DbProjectData\Fatcat\PfamDomainAlignments\ErrorDomainPairs.txt");
            string line = "";
            List<string> domainPairList = new List<string> ();
            while ((line = domainPairReader.ReadLine()) != null)
            {
                domainPairList.Add(line);
            }
            domainPairReader.Close();
            return domainPairList.ToArray ();
        }

        public void WriteStructDomainPairs()
        {
            string queryString = "Select QueryEntry, QueryEntity, QueryDomainID, HitEntry, HitEntity, HitDomainID From PfamDomainALignments " +
                " Where (QueryEntry = '4a6y' AND QueryDomainID = 1076543915 AND QueryEntity = 2) OR " + 
                " (HitEntry = '4a6y' AND HitDomainID = 1076543915 AND HitEntity = 2);";
            DataTable domainAlignTable = dbQuery.Query(alignmentDbConnection, queryString);
            StreamWriter domainPairWriter = new StreamWriter("WrongDomainPairs.txt");
            foreach (DataRow alignRow in domainAlignTable.Rows)
            {
                domainPairWriter.WriteLine(alignRow["QueryEntry"].ToString () + alignRow["QueryDomainID"].ToString () + "," +
                    alignRow["HitEntry"].ToString () + alignRow["HitDomainID"].ToString ());
            }
            domainPairWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain1"></param>
        /// <param name="domain2"></param>
        /// <param name="tableName"></param>
        public DataRow UpdateDomainPairAlignments(string domain1, string domain2, string tableName)
        {
            string pdbId1 = domain1.Substring(0, 4);
            long domainId1 = Convert.ToInt64(domain1.Substring(4, domain1.Length - 4));
            string pdbId2 = domain2.Substring(0, 4);
            long domainId2 = Convert.ToInt64(domain2.Substring(4, domain2.Length - 4));
            DataTable domainAlignTable = GetDomainAlignment(pdbId1, domainId1, pdbId2, domainId2, tableName);
            DataRow alignRow = domainAlignTable.Rows[0];
            DomainAlignSeqInfo[] alignInfos = GetDomainAlignInfos(alignRow);
            seqConverter.AddDisorderResiduesToDomainAlignment(ref alignInfos[0], ref alignInfos[1]);
            alignRow["QuerySequence"] = alignInfos[0].alignSequence;
            alignRow["HitSequence"] = alignInfos[1].alignSequence;
            AddQueryHitSeqNumbers(alignRow);
            return alignRow;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainId2"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetDomainAlignment(string pdbId1, long domainId1, string pdbId2, long domainId2, string tableName)
        {
            string queryString = string.Format("Select * From {0} Where (QueryEntry = '{1}' AND QueryDomainID = {2} AND " +
                " HitEntry = '{3}' AND HitDomainID = {4}) OR (QueryEntry = '{3}' AND QueryDomainID = {4} AND " +
                " HitEntry = '{1}' AND HitDomainID = {2});", tableName, pdbId1, domainId1, pdbId2, domainId2);
            DataTable domainAlignTable = dbQuery.Query(alignmentDbConnection, queryString);
            domainAlignTable.TableName = tableName;
            return domainAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RecoverDomainAlignments()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Read parsed domain pairs.");
            List<string>[] insertedDomainPairLists = ReadInsertedDomainPairList("InsertedDomainPairs.txt");
            List<string> insertedDomainPairList = insertedDomainPairLists[0];
            List<string> insertedDomainPairRigidList = insertedDomainPairLists[1];
            StreamWriter insertedDomainPairsWriter = new StreamWriter("InsertedDomainPairs.txt", true);
            string[] domainAligmentFiles = Directory.GetFiles(@"D:\DbProjectData\Fatcat\PfamDomainAlignments", "*.txt");
            string queryString = "Select First 1 * From PfamDomainAlignments;";
            DataTable alignTable = dbQuery.Query(alignmentDbConnection, queryString);
            alignTable.Clear();
            foreach (string alignFile in domainAligmentFiles)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(alignFile);
                InputAlignmentsToDb(alignFile, insertedDomainPairList, insertedDomainPairRigidList,
                    alignTable, insertedDomainPairsWriter);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            insertedDomainPairsWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignFile"></param>
        /// <param name="insertedDomainPairList"></param>
        /// <param name="alignTable"></param>
        /// <param name="domainPairWriter"></param>
        private void InputAlignmentsToDb(string alignFile, List<string> insertedDomainPairList, List<string> insertedDomainPairRigidList, 
            DataTable alignTable, StreamWriter domainPairWriter)
        {
            string tableName = "";
            string domainPair = "";
            StreamReader alignReader = new StreamReader(alignFile);
            string line = "";
            while ((line = alignReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length == 1)
                {
                    tableName = fields[0];
                    alignTable.TableName = tableName;
                }
                else
                {
                    DataRow alignRow = alignTable.NewRow();
                    if (IsDomainExist(fields[0], Convert.ToInt64(fields[2])) &&
                        IsDomainExist(fields[5], Convert.ToInt64(fields[7])))
                    {
                        alignRow.ItemArray = fields;
                        domainPair = GetDomainPair(alignRow);
                        if (tableName.IndexOf("Rigid") > -1)
                        {
                            if (insertedDomainPairRigidList.Contains(domainPair))
                            {
                                continue;
                            }
                            insertedDomainPairRigidList.Add(domainPair);
                            domainPairWriter.WriteLine("Rigid:" + domainPair);
                            domainPairWriter.Flush();
                        }
                        else
                        {
                            if (insertedDomainPairList.Contains(domainPair))
                            {
                                continue;
                            }
                            insertedDomainPairList.Add(domainPair);
                            domainPairWriter.WriteLine(domainPair);
                            domainPairWriter.Flush();
                        }
                        ProtCidSettings.progressInfo.currentFileName = domainPair;
                        DeleteDomainAlignment(fields[0], Convert.ToInt64 (fields[2]), fields[5], Convert.ToInt64 (fields[7]), tableName);
                        dbInsert.InsertDataIntoDb(alignmentDbConnection, alignRow);
                    }
                }
            }
            alignReader.Close(); 
        }

        private List<string>[] ReadInsertedDomainPairList(string insertDomainPairFile)
        {
            List<string> domainPairList = new List<string>();
            List<string> domainPairRigidList = new List<string>();
            if (File.Exists(insertDomainPairFile))
            {
                StreamReader domainPairReader = new StreamReader(insertDomainPairFile);
                string line = "";
                while ((line = domainPairReader.ReadLine()) != null)
                {
                    if (line.IndexOf("Rigid") > -1)
                    {
                        string[] fields = line.Split(':');
                        //     if (!domainPairRigidList.Contains(fields[1]))
                        domainPairRigidList.Add(fields[1]);
                    }
                    else
                    {
                        domainPairList.Add(line);
                    }
                }
                domainPairReader.Close();
            }
            List<string>[] domainPairLists = new List<string>[2];
            domainPairLists[0] = domainPairList;
            domainPairLists[1] = domainPairRigidList;
            return domainPairLists;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private bool IsDomainExist(string pdbId, long domainId)
        {
            string queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable pfamAssignTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (pfamAssignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        public void UpdateMultiChainDomainSeqNumbers()
        {

        }

        public void OutputMultiChainDomainALignments()
        {
      /*      StreamReader dataReader = new StreamReader(@"F:\Firebird\Xtal\Alignments\ErrorDomainPairs.txt");
            string line = "";
            
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');
                ArrayList hitDomainList = new ArrayList(fields);
                hitDomainList.RemoveAt(0);
                domainAlignHash.Add(fields[0], hitDomainList);
            }
            dataReader.Close();
            */
            string queryDomain = "";
            string hitDomain = "";
            Dictionary<string, List<string>> domainAlignHash = new Dictionary<string,List<string>> ();
           
            foreach (string pdbId in entryMultiDomainHash.Keys)
            {
                long[] multiChainDomains = (long[])entryMultiDomainHash[pdbId];
                foreach (long multiChainDomain in multiChainDomains)
                {
                    DataTable domainPairTable = GetMultiChainDomainAlignments(pdbId, multiChainDomain);
                    foreach (DataRow domainPairRow in domainPairTable.Rows)
                    {
                        queryDomain = domainPairRow["QueryEntry"].ToString() + domainPairRow["QueryDomainID"].ToString();
                        hitDomain = domainPairRow["HitEntry"].ToString() + domainPairRow["HitDomainID"].ToString();
                        if (domainAlignHash.ContainsKey(queryDomain))
                        {
                            if (!domainAlignHash[queryDomain].Contains(hitDomain))
                            {
                                domainAlignHash[queryDomain].Add(hitDomain);
                            }
                        }
                        else
                        {
                            List<string> domainList = new List<string> ();
                            domainList.Add(hitDomain);
                            domainAlignHash.Add(queryDomain, domainList);
                        }
                    }
                }
            }
            string updateOrgDomainPairsFile = @"F:\Firebird\Xtal\Alignments\DomainPairs_multiChain.txt";
            StreamWriter dataWriter = new StreamWriter(updateOrgDomainPairsFile);
            string dataLine = "";
            foreach (string lsQueryDomain in domainAlignHash.Keys)
            {
                dataLine = lsQueryDomain;
                foreach (string lsHitDomain in domainAlignHash[lsQueryDomain])
                {
                    dataLine += ("," + lsHitDomain);
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        private DataTable GetMultiChainDomainAlignments(string pdbId, long multiChainDomainId)
        {
            string queryString = string.Format("Select QueryEntry, QueryDomainID, HitEntry, HitDomainID From PfamDomainAlignments " +
                " Where (QueryEntry = '{0}' AND QueryDomainId = {1}) OR (HitEntry = '{0}' AND HitDomainID = {1});", pdbId, multiChainDomainId);
            DataTable domainPairTable = dbQuery.Query(alignmentDbConnection, queryString);
            return domainPairTable;
        }
        /// <summary>
        /// 
        /// </summary>
        public void OutputErrorStartDomainAlignments()
        {
       /*     string queryString = "Select QueryEntry, QueryDomainID, HitEntry, HitDomainID From PfamDomainAlignments Where QueryStart <= 0 Or HitStart <= 0;";
            DataTable errorDomainAlignTable = dbQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter("ErrorStartDomainAlignments.txt");
            string dataLine = "";
            foreach (DataRow domainAlignRow in errorDomainAlignTable.Rows)
            {
                dataLine = domainAlignRow["QueryEntry"].ToString() + domainAlignRow["QueryDomainID"].ToString() + "\t" +
                    domainAlignRow["HitEntry"].ToString() + domainAlignRow["HitDomainID"].ToString();
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
            */
            Dictionary<string, List<string>> queryDomainHash = new Dictionary<string,List<string>> ();
            StreamReader dataReader = new StreamReader(@"F:\Firebird\Xtal\Alignments\errorstartdomainalignments.txt");
            string line = "";
            string queryDomain = "";
            string hitDomain = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.IndexOf ("QUERYENTRY") > -1)
                {
                    continue;
                }
                if (line.IndexOf ("==========") > -1)
                {
                    continue;
                }

                string[] fields = ParseHelper.SplitPlus(line, ' ');
                queryDomain = fields[0] + fields[1];
                hitDomain = fields[2] + fields[3];
                if (queryDomainHash.ContainsKey(queryDomain))
                {
                    queryDomainHash[queryDomain].Add(hitDomain);
                }
                else
                {
                    List<string> hitDomainList = new List<string> ();
                    hitDomainList.Add(hitDomain);
                    queryDomainHash.Add(queryDomain, hitDomainList);
                }
            }
            dataReader.Close();

            StreamWriter dataWriter = new StreamWriter(@"F:\Firebird\Xtal\Alignments\ErrorDomainPairs.txt");
            string dataLine = "";
            foreach (string lsQueryDomain in queryDomainHash.Keys)
            {
                dataLine = lsQueryDomain;
                foreach (string lsHitDomain in queryDomainHash[lsQueryDomain])
                {
                    dataLine += ("," + lsHitDomain); 
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void InitializeTables (bool isUpdate)
        {  
            FatcatTables.InitializeTables(dbAlignTableName);
            if (! isUpdate)
            {
                FatcatTables.InitializeDbTable(alignmentDbConnection, alignTableNames[0]);
                FatcatTables.InitializeDbTable(alignmentDbConnection, alignTableNames[1]);
            }
        }
    }
}
