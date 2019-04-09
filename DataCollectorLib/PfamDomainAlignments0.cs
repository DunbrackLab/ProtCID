using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using DataCollectorLib.FatcatAlignment;
using XtalLib.Settings;
using XtalLib.DomainInterfaces;
using ProgressLib;
using AuxFuncLib;

namespace DataCollectorLib.Pfam
{
    public class PfamDomainAlignments : FatcatAlignmentParser
    {
        private string pfamDbTable = "";
        public string[] alignTableNames = new string[2];

        public PfamDomainAlignments()
        {
            dbAlignTableName = "PfamDomainAlignments";
            pfamDbTable = "PdbPfam";
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
        }
        #region domain alignments in XML sequential numbers
        /// <summary>
        /// 
        /// </summary>
        public void ParsePfamAlignFiles(bool isUpdate, string domainType, string pfamOrClan)
        {
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
            }
            string alignFileDir = Path.Combine (AppSettings.dirSettings.fatcatPath, "PfamDomainAlignments");
            if (pfamOrClan.IndexOf("weak") > -1)
            {
                alignFileDir = Path.Combine(alignFileDir, "weakDomainAlignments");
                alignTableNames[1] = "PfamWeakDomainAlignmentsRigid";
                alignTableNames[0] = "PfamWeakDomainAlignments";
                dbAlignTableName = "PfamWeakDomainAlignments";
            }
            else
            {
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

            AppSettings.progressInfo.Reset();
            AppSettings.progressInfo.progStrQueue.Enqueue("Parsing Pfam Alignments files: ");
            logWriter = new StreamWriter("PfamAlignmentsLog.txt");

            string dataPath = "";
            foreach (string alignTableName in alignTableNames)
            {
            /*    if (!isUpdate)
                {
                    FatcatTables.InitializeDbTable(AppSettings.alignmentDbConnection, alignTableName);
                }*/
                FatcatTables.fatcatAlignTable.TableName = alignTableName;
                dbAlignTableName = alignTableName;


                if (alignTableName.IndexOf("Rigid") > -1)
                {
                    dataPath = Path.Combine(alignFileDir, pfamOrClan.Substring (0, 4) + "StructAlign\\rigid");
                }
                else
                {
                    dataPath = Path.Combine(alignFileDir, pfamOrClan.Substring (0, 4) + "StructAlign\\flexible");
                }
                string[] alignFiles = System.IO.Directory.GetFiles(dataPath, "*.aln");

                AppSettings.progressInfo.ResetCurrentProgressInfo();
                AppSettings.progressInfo.progStrQueue.Enqueue(dbAlignTableName);
                AppSettings.progressInfo.totalOperationNum = alignFiles.Length;
                AppSettings.progressInfo.totalStepNum = alignFiles.Length;
                AppSettings.progressInfo.currentOperationLabel = "Parse Pfam Align Files";

                foreach (string alignFile in alignFiles)
                {
                    AppSettings.progressInfo.currentFileName = alignFile;
                    AppSettings.progressInfo.currentOperationNum++;
                    AppSettings.progressInfo.currentStepNum++;

                    logWriter.WriteLine(alignFile);
                    ParsePfamFatcatAlignmentFile(alignFile, isUpdate);
                    logWriter.Flush();
                    GC.Collect();
                }
            }
            AppSettings.alignmentDbConnection.DisconnectFromDatabase();

            logWriter.Close();

            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
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

            Initialize(isUpdate);

            AppSettings.progressInfo.Reset();
            AppSettings.progressInfo.progStrQueue.Enqueue("Parsing Pfam Alignments files: ");
            logWriter = new StreamWriter("PfamAlignmentsLog.txt", true);

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.totalOperationNum = alignFiles.Length;
            AppSettings.progressInfo.totalStepNum = alignFiles.Length;
            AppSettings.progressInfo.currentOperationLabel = "Parse Pfam Align Files";

            foreach (string alignFile in alignFiles)
            {
                AppSettings.progressInfo.currentFileName = alignFile;
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;

                logWriter.WriteLine(alignFile);
                ParsePfamFatcatAlignmentFile(alignFile, isUpdate);
                logWriter.Flush();
                GC.Collect();
            }
            AppSettings.alignmentDbConnection.DisconnectFromDatabase();

            logWriter.Close();
            AppSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// parse one fatcat alignment output file
        /// insert data into database
        /// </summary>
        /// <param name="alignFile"></param>
        public void ParsePfamFatcatAlignmentFile(string alignFile, bool isUpdate)
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
            AlignSeqInfo alignInfo1 = new AlignSeqInfo();
            AlignSeqInfo alignInfo2 = new AlignSeqInfo();
            DataRow dataRow = FatcatTables.fatcatAlignTable.NewRow();
            string dataLine = "";
            // the asymchain and startpos for this domain
            Hashtable domainChainInfoHash = new Hashtable();

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
                        alignInfo2.pdbId = fields[4].Substring(0, 4);
                        alignInfo2.asymChainId = domainInfo2[2];
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
                        if (alignInfo1.alignStart < 0)
                        {
                            FindStartEndPosition(dataRow["QueryEntry"].ToString(),
                                Convert.ToInt32 (dataRow["QueryEntity"].ToString()),
                                Convert.ToInt16(dataRow["QueryDomainStart"].ToString()), ref alignInfo1);
                        }
                        alignInfo2.alignStart = alignStart2;
                        alignInfo2.alignEnd = alignEnd2;
                        alignInfo2.alignSequence = alignSequence2;
                        if (alignInfo2.alignStart < 0)
                        {
                            FindStartEndPosition(dataRow["HitEntry"].ToString(),
                                Convert.ToInt32 (dataRow["HitEntity"].ToString()),
                                Convert.ToInt16(dataRow["HitDomainStart"].ToString()), ref alignInfo2);
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
                        dbInsert.InsertDataIntoDb(AppSettings.alignmentDbConnection, dataRow);
                        alignSequence1 = "";
                        alignSequence2 = "";
                        dataLine = "";
                    }
                    if (line.IndexOf("#Time used") > -1)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(ex.Message);
                    logWriter.WriteLine(line);
                    logWriter.WriteLine(dataLine);
                    logWriter.Flush();
                    dataLine = "";
                }
            }
            dataReader.Close();
        }
        #endregion

        #region domain alignments in domain sequential numbers in HMM order
        /// <summary>
        /// sequential numbers in a domain file is continuous, 
        /// but the pdb sequential numbers may not be continuous
        /// </summary>
        public void ParsePfamDomainAlignments()
        {
            //        alignTableNames[0] = "PfamDomainAlignments";
            //        alignTableNames[1] = "PfamDomainAlignmentsRigid";

            bool isUpdate = false;

            pfamDbTable = "PdbPfam";

            Initialize(isUpdate);

            AppSettings.progressInfo.Reset();
            AppSettings.progressInfo.progStrQueue.Enqueue("Parsing Pfam domain Alignments files: ");
            logWriter = new StreamWriter("PfamAlignmentsLog.txt");

            string dataPath = @"D:\DbProjectData\pfam\EPCutoffStructFiles\EPStructResults";

            FatcatTables.fatcatAlignTable.TableName = "PfamDomainAlignmentsRigid";

            if (!isUpdate)
            {
                FatcatTables.InitializeDbTable(AppSettings.alignmentDbConnection, FatcatTables.fatcatAlignTable.TableName);
            }
            
            string[] alignFiles = System.IO.Directory.GetFiles(dataPath, "*.aln");

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.progStrQueue.Enqueue(dbAlignTableName);
            AppSettings.progressInfo.totalOperationNum = alignFiles.Length;
            AppSettings.progressInfo.totalStepNum = alignFiles.Length;
            AppSettings.progressInfo.currentOperationLabel = "Parse Pfam Align Files";

            foreach (string alignFile in alignFiles)
            {
                AppSettings.progressInfo.currentFileName = alignFile;
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;

                logWriter.WriteLine(alignFile);
                ParsePfamFatcatAlignmentFile(alignFile, isUpdate);
                try
                {
                    MoveParsedAlignFileToParsedFolder(alignFile);
                }
                catch (Exception ex)
                {
                    AppSettings.progressInfo.progStrQueue.Enqueue("Move " + alignFile + " error: " + ex.Message);
                    logWriter.WriteLine("Move " + alignFile + " error: " + ex.Message);
                }
                logWriter.Flush();
                GC.Collect();
            }
            AppSettings.alignmentDbConnection.DisconnectFromDatabase();

            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignFile"></param>
        private void MoveParsedAlignFileToParsedFolder(string alignFile)
        {
            FileInfo fileInfo = new FileInfo(alignFile);
            string destFilePath = Path.Combine(fileInfo.DirectoryName, "parsed");
            string destFileName = Path.Combine(destFilePath, fileInfo.Name);
            File.Move(alignFile, destFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignRow"></param>
        private void AddQueryHitSeqNumbers(DataRow alignRow)
        {
            string queryPdbId = alignRow["QueryEntry"].ToString();
            long queryDomainId = Convert.ToInt64(alignRow["QueryDomainID"].ToString ());

            int queryFileSeqStart = Convert.ToInt32(alignRow["QueryStart"].ToString ());
            string queryAlignment = alignRow["QuerySequence"].ToString();
            string querySeqNumbers = GetSeqNumbers(queryPdbId, queryDomainId, queryFileSeqStart, queryAlignment);

            string hitPdbId = alignRow["HitEntry"].ToString();
            long hitDomainId = Convert.ToInt64(alignRow["hitDomainID"].ToString ());
            int hitFileSeqStart = Convert.ToInt32(alignRow["HitStart"]);
            string hitAlignment = alignRow["HitSequence"].ToString();
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
        private string GetSeqNumbers(string pdbId, long domainId, int fileSeqStart, string alignment)
        {
            int[] fileSeqNumbers = GetFileSeqNumbers(alignment, fileSeqStart);

            string queryString = string.Format("Select * From PdbPfamDomainFileInfo " +
                " Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable fileSeqInfoTable = dbQuery.Query(queryString);
            Range[] fileRegions = new Range[fileSeqInfoTable.Rows.Count];
            Range[] seqRegions = new Range[fileSeqInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow seqInfoRow in fileSeqInfoTable.Rows)
            {
                Range seqRegion = new Range();
                seqRegion.startPos = Convert.ToInt32(seqInfoRow["SeqStart"].ToString ());
                seqRegion.endPos = Convert.ToInt32(seqInfoRow["SeqEnd"].ToString ());
                seqRegions[count] = seqRegion;

                Range fileRegion = new Range();
                fileRegion.startPos = Convert.ToInt32(seqInfoRow["FileStart"].ToString ());
                fileRegion.endPos = Convert.ToInt32(seqInfoRow["FileEnd"].ToString ());
                fileRegions[count] = fileRegion;
                count++;
            }
            string seqNumbers = "";
            int seqNumber = 0;
            for (int i = 0; i < fileSeqNumbers.Length; i ++ )
            {
                if (fileSeqNumbers[i] != -1)
                {
                    seqNumber = GetSeqNumber(fileSeqNumbers[i], fileRegions, seqRegions);
                    seqNumbers += (seqNumber.ToString() + ",");
                }
                else
                {
                    seqNumbers += "-,";
                }
            }
            return seqNumbers.TrimEnd(',');
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
            ArrayList fileSeqNumberList = new ArrayList ();
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
            int[] fileSeqNumbers = new int[fileSeqNumberList.Count];
            fileSeqNumberList.CopyTo(fileSeqNumbers);
            return fileSeqNumbers;
        }
        #endregion

        #region deal with domain alignments
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
                " Where QueryEntry = '{1}' AND QueryDomainID = {2} AND " +
                " HitEntry = '{3}' AND HitDomainID = {4};",
                dbAlignTableName, pdbId1, domainId1, pdbId2, domainId2);
            dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
        }

        // delete all the record about this weak domains
        private void DeletePfamAlignment(string pdbId, long domainId)
        {
            string deleteString = string.Format("Delete From {0} Where HitEntry = '{1}' AND HitDomainID = {2};",
                dbAlignTableName, pdbId, domainId);
            dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
        }

        /// <summary>
        /// the asymmetric chain name from the pfam domain name
        /// which has the format 1xxxChainStartPos.pfam
        /// chain name may have more than one characters
        /// <param name="domainFile"></param>
        /// <returns></returns>
        private string[] ParseDomainName(string domainFile, ref Hashtable domainChainInfoHash)
        {
            string[] domainNameInfo = new string[4];
            domainNameInfo[0] = domainFile.Substring(0, 4); // pdb entry code
            int exeIdx = domainFile.IndexOf(".pfam");
            domainNameInfo[1] = domainFile.Substring (4, exeIdx - 4); // domain ID
            string[] chainInfos = GetDomainChainInfo (domainNameInfo[0], Convert.ToInt64 (domainNameInfo[1]), 
                ref domainChainInfoHash);
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
            Hashtable coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, alignInfo.alignStart);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence, alignInfo.alignStart - 1);
            if (alignStartIdx > -1)
            {
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = (int)coordSeqIdSeqIdHash[alignEndIdx + 1];
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
            Hashtable coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, alignInfo.alignStart);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence);
            if (alignStartIdx > -1)
            {
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = (int)coordSeqIdSeqIdHash[alignEndIdx];
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
        private void FindStartEndPosition(string pdbId, int entityID, int startPos, ref AlignSeqInfo alignInfo)
        {
            string noGapAlignSequence = GetNonGapAlignedString(alignInfo.alignSequence);
            string sequenceInCoord = GetChainSequenceInCoordinates(pdbId, entityID);
            string noGapCoordSeq = "";
            Hashtable coordSeqIdSeqIdHash = GetCoordSeqToSeqHash(sequenceInCoord, out noGapCoordSeq, startPos);
            int alignStartIdx = noGapCoordSeq.IndexOf(noGapAlignSequence);
            if (alignStartIdx > -1)
            {
                alignInfo.alignStart = (int)coordSeqIdSeqIdHash[alignStartIdx + 1];
                int alignEndIdx = alignStartIdx + noGapAlignSequence.Length;
                // get the XML sequential id
                int alignEnd = (int)coordSeqIdSeqIdHash[alignEndIdx];
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
            DataTable seqInCoordTable = dbQuery.Query(queryString);
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
        private Hashtable GetCoordSeqToSeqHash(string seqInCoord, out string noGapCoordSeq, int startPos)
        {
            Hashtable seqCoordSeqHash = new Hashtable();
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
        private string[] GetDomainChainInfo(string pdbId, Int64 domainId, ref Hashtable domainChainInfoHash)
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
                DataTable domainInfoTable = dbQuery.Query(queryString);

                if (domainInfoTable.Rows.Count == 0)
                {
                    queryString = string.Format("Select EntityID, AlignStart From {0} " +
                            " Where PdbID = '{1}' AND DomainID = {2};", pfamDbTable + "Weak", pdbId, domainId);
                    domainInfoTable = dbQuery.Query(queryString);
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
        #endregion

        #region delete obsolete alignments
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
                    dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
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
                    dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
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
                dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);

                if (isRigid)
                {
                    deleteString = string.Format("Delete From {0} WHERE (QueryEntry = '{1}' AND QueryDomainID = {2}) " +
                    " OR (HitEntry = '{1}' AND HitDomainID = {2});", alignTableName + "Rigid", pdbId, domainId);
                    dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
                }
            }
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void AddPfamDomainIDs()
        {
            if (AppSettings.alignmentDbConnection == null)
            {
                AppSettings.alignmentDbConnection = new DbConnect();
            }
            if (AppSettings.alignmentDbConnection.ConnectString == "")
            {
                if (AppSettings.dirSettings == null)
                {
                    AppSettings.LoadDirSettings();
                }
                AppSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.alignmentDbPath;
            }
            AppSettings.alignmentDbConnection.ConnectToDatabase();

            if (DbBuilder.dbConnect.ConnectString == "")
            {
                DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;
            }
            string entry = "";
            int entityId = -1;
            int domainStart = -1;
            Int64 domainId = -1;

            string queryString = string.Format ("Select PdbID, EntityID, AlignStart, DomainID From {0} " +
                " Where IsSame = '1';", pfamDbTable);
            DataTable pfamDomainTable = dbQuery.Query(queryString);
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                entry = domainRow["PdbID"].ToString();
                entityId = Convert.ToInt32 (domainRow["EntityID"].ToString().TrimEnd());
                domainStart = Convert.ToInt32(domainRow["AlignStart"].ToString ());
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());

                UpdatePfamDomainIDInAlignmentsTable(entry, entityId, domainStart, domainId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="domainStart"></param>
        /// <param name="domainId"></param>
        private void UpdatePfamDomainIDInAlignmentsTable(string pdbId, int entityId, int domainStart, Int64 domainId)
        {
            string updateString = string.Format("Update {0} Set QueryDomainID = {1} " +
                    " Where QueryEntry = '{2}' AND QueryEntity = '{3}' AND QueryDomainStart = {4};",
                    dbAlignTableName, domainId, pdbId, entityId, domainStart);
            dbQuery.Query(AppSettings.alignmentDbConnection, updateString);

            updateString = string.Format("Update {0} Set HitDomainID = {1} " +
                " Where HitEntry = '{2}' AND HitEntity = '{3}' AND HitDomainStart = {4};",
                dbAlignTableName, domainId, pdbId, entityId, domainStart);
            dbQuery.Query(AppSettings.alignmentDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="domainStart"></param>
        /// <param name="domainId"></param>
        private void UpdateHitPfamDomainIDInAlignmentsTable(string pdbId, int entityId, int domainStart, Int64 domainId)
        {
            string updateString = string.Format("Update {0} Set HitDomainID = {1} " +
                " Where HitEntry = '{2}' AND HitEntity = '{3}' AND HitDomainStart = {4};",
                dbAlignTableName, domainId, pdbId, entityId, domainStart);
            dbQuery.Query(AppSettings.alignmentDbConnection, updateString);
        }
        #endregion
    }
}
