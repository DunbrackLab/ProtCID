using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Text;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;
using DataCollectorLib.FatcatAlignment;
using InterfaceClusterLib.DataTables;

namespace InterfaceClusterLib.Alignments
{
    public class GroupEntryAlignments
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();  // for regular query
        internal EntryAlignment entryAlignment = new EntryAlignment();
        internal DbInsert dbInsert = new DbInsert();
        internal const int numOfRepEntriesInGroupCutoff = 500;
//        internal DataTable pdbCrcMapTable = null;
        #endregion

        #region initialize        
        /// <summary>
        /// 
        /// </summary>
        private void Initialize()
        {
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbLib.DbConnect();
                if (ProtCidSettings.dirSettings == null)
                {
                    ProtCidSettings.LoadDirSettings();
                }
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.alignmentDbPath;
            }
            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.protcidDbPath);
            }
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                             ProtCidSettings.dirSettings.pdbfamDbPath);
            }                   
        }
        #endregion

        #region Find chain alignments for groups
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainAlignmentsInGroups()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve alignments info in each group.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Alignments info";

            ProtCidSettings.logWriter.WriteLine("Retrieve alignments info in each group.");

            HomoGroupTables.InitializeTables();

            string familyGroupTableName = ProtCidSettings.dataType + "Groups";
            string queryString = string.Format("Select GroupSeqID From {0};", familyGroupTableName);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int groupId = -1;
            ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;

            foreach (DataRow groupRow in groupTable.Rows)
            {
                groupId = Convert.ToInt16(groupRow["GroupSeqID"].ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = groupId.ToString();
                ProtCidSettings.logWriter.WriteLine(groupId.ToString ());      

                // 13299 is (V-set)_(C1_set);(V-set)_(C1_set)
               
                RetrieveChainAlignmentsInGroup(groupId);

                ProtCidSettings.logWriter.Flush();
            }
#if DEBUG
            EntryAlignment.nonAlignedDataWriter.Close();

            FindMissingRepChainPairs();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
   //         ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private bool IsGroupAlignFilled(int groupSeqId)
        {
            string queryString = string.Format("Select * From PfamHomoGroupEntryAlign Where GroupSeqId = {0} AND EntityID1 = -1 AND EntityID2 = -1;", groupSeqId);
            DataTable homoEntryAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (homoEntryAlignTable.Rows.Count > 0)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainAlignmentsInGroups(int[] updateGroups)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve alignments info in each group.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Alignments info";

            ProtCidSettings.logWriter.WriteLine("Retrieve alignments info in each group.");

            HomoGroupTables.InitializeTables();
            ProtCidSettings.progressInfo.totalStepNum = updateGroups.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateGroups.Length;

            foreach (int groupId in updateGroups)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = groupId.ToString();

                ProtCidSettings.logWriter.WriteLine(groupId.ToString ());
                
                RetrieveChainAlignmentsInGroup(groupId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
   //         ProtCidSettings.progressInfo.threadFinished = true;
        }

        #region update
        /// <summary>
        /// update alignments by run fatcat on linux,
        /// parse alignment file and insert data into db
        /// fill in those missing alignments,
        /// added on August 17, 2010
        /// </summary>
        /// <param name="updateGroups"></param>
        public void UpdateChainAlignmentsInGroups(int[] updateGroups)
        {
 //           UpdateMissingChainAlignmentsInGroups(updateGroups);

            // find rep chain pairs without alignments
 //           ProtCidSettings.progressInfo.progStrQueue.Enqueue("Find the rep chain pairs without alignments.");
            string chainPairFile = FindMissingChainAlignments(updateGroups);

            File.Copy(chainPairFile, Path.Combine(ProtCidSettings.dirSettings.fatcatPath, chainPairFile), true);
            DivideNonAlignedChainPairs(Path.Combine(ProtCidSettings.dirSettings.fatcatPath, chainPairFile), 3000);
            // copy the file to Linux server, run fatcat, get the aln file
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Copy the file to Linux server, run fatcat and get the aln file.");          
       //     string alnFile = GetFatcatAlignments(chainPairFile);

            // parse the alignment file, insert data into alignments.fdb
            FatcatAlignmentParser alnFileParser = new FatcatAlignmentParser();
            string[] alnFiles = Directory.GetFiles(Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments"), "*.aln");
            try
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing the alignment file.");
            //    alnFileParser.ParseFatcatAlignments(alnFile, true);
                alnFileParser.ParseFatcatAlignments(alnFiles, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Parse fatcat alignment file error: " + ex.Message);
            }
            
    //        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete missing alignment entry interface comp.");
    //        DeleteMissingAlignEntryInterfacesComp();

            // update missing alignments in the protbud.fdb
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update missing alignments in protbud.fdb");
            UpdateMissingChainAlignmentsInGroups(updateGroups);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedPairFile"></param>
        public void UpdateFatcatAlignmentsFromFile(string nonAlignedChainPairFile)
        {
     /*       string repChainPairFile = GetRepChainPairsToBeAligned(nonAlignedChainPairFile);
        //  string repChainPairFile = GetRepChainPairsToBeAligned();
       //   string repChainPairFile = @"C:\ProjectData\DbProjectData\Fatcat\ChainAlignments\chainAlignFileList\NonAlignedRepEntryPairs.txt";
            DivideNonAlignedChainPairs(repChainPairFile, 10000);*/
            
            // parse the alignment file, insert data into alignments.fdb
            FatcatAlignmentParser alnFileParser = new FatcatAlignmentParser();
            string[] alnFiles = Directory.GetFiles(Path.Combine (ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\alnFiles"), "*.aln");
            try
            {
             //   alnFileParser.ParseFatcatAlignments(alnFile, true);
                alnFileParser.ParseFatcatAlignments(alnFiles, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Parse Fatcat alignment file error: " + ex.Message);
            }
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedPairFile"></param>
        public void UpdateFatcatAlignmentsExistingAlnFiles ()
        {
            FatcatAlignmentParser alnFileParser = new FatcatAlignmentParser();
            string[] alnFiles = Directory.GetFiles(@"D:\DbProjectData\Fatcat\ChainAlignments", "*.aln");
            try
            {
                alnFileParser.ParseFatcatAlignments(alnFiles, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Parse Fatcat alignment file error: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairFile"></param>
        /// <returns></returns>
        public string GetRepChainPairsToBeAligned()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve non-aligned representative chain pairs for FATCAT");
            string filePath = ProtCidSettings.dirSettings.fatcatPath + "\\ChainAlignments";
            string[] nonAlignedFiles = Directory.GetFiles(filePath, "NonAlignedEntryPairs*");
            string repChainPairsFile = Path.Combine(filePath, "NonAlignedRepEntryPairs.txt");
   //         DivideNonAlignedChainPairs(repChainPairsFile, 10000);
            string leftChainPairsFile = Path.Combine(filePath, "NonAlignedRepEntryPairsMore.txt");
            StreamWriter leftChainPairsWriter = new StreamWriter(leftChainPairsFile, true);

            StreamReader dataReader = null;
            StreamWriter dataWriter = null;
            List<string> repChainPairList = new List<string> ();
            if (File.Exists(repChainPairsFile))
            {
                StreamReader repChainPairReader = new StreamReader(repChainPairsFile);
                string existRepChainPair = "";
                while ((existRepChainPair = repChainPairReader.ReadLine()) != null)
                {
                    repChainPairList.Add(existRepChainPair);
                }
                repChainPairReader.Close();

                dataWriter = new StreamWriter(repChainPairsFile, true);
            }
            else
            {
                dataWriter = new StreamWriter(repChainPairsFile);
            }
            repChainPairList.Sort();

            string line = "";
            string repChain1 = "";
            string repChain2 = "";
            Dictionary<string, string> chainRepChainHash = new Dictionary<string,string> ();
            string repChainPair = "";
            int numOfLineReadSofar = 0;
            List<string> leftChainPairList = new List<string> ();
            foreach (string nonAlignedChainPairFile in nonAlignedFiles)
            {
                dataReader = new StreamReader(nonAlignedChainPairFile);
                numOfLineReadSofar = 0;
                while ((line = dataReader.ReadLine()) != null)
                {
                    numOfLineReadSofar++;
 
                    if (line == "")
                    {
                        continue;
                    }
                    string[] fields = ParseHelper.SplitPlus(line, '\t');
                    if (fields.Length != 2)
                    {
                        fields = ParseHelper.SplitPlus(line, ' ');
                    }
                    repChain1 = GetRepAsymChain(fields[0].Substring(0, 4), fields[0].Substring(4, fields[0].Length - 4), ref chainRepChainHash);
                    repChain2 = GetRepAsymChain(fields[1].Substring(0, 4), fields[1].Substring(4, fields[1].Length - 4), ref chainRepChainHash);
                    if (repChain1 == repChain2)
                    {
                        continue;
                    }

                    if (string.Compare(repChain1, repChain2) > 0)
                    {
                        repChainPair = repChain2 + "   " + repChain1;
                    }
                    else
                    {
                        repChainPair = repChain1 + "   " + repChain2;
                    }
                    if (repChainPairList.BinarySearch(repChainPair) < 0)
                    {
                        //   repChainPairList.Add(repChainPair);
                        if (!leftChainPairList.Contains(repChainPair))
                        {
                            leftChainPairList.Add(repChainPair);
                            dataWriter.WriteLine(repChainPair);
                            dataWriter.Flush();
                            leftChainPairsWriter.WriteLine(repChainPair);
                        }
                    }
                }
                dataReader.Close();
            }
            dataWriter.Close();
            leftChainPairsWriter.Close();

            DivideNonAlignedChainPairs(leftChainPairsFile, 500);

            return repChainPairsFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairFile"></param>
        /// <returns></returns>
        public string GetRepChainPairsToBeAligned(string nonAlignedChainPairFile)
        {
            StreamReader dataReader = new StreamReader(nonAlignedChainPairFile);
            string repChainPairsFile = nonAlignedChainPairFile.Replace("NonAlignedEntryPairs", "NonAlignedRepEntryPairs");
            StreamWriter dataWriter = null;
            List<string> repChainPairList = new List<string> ();
            if (File.Exists(repChainPairsFile))
            {
                StreamReader repChainPairReader = new StreamReader(repChainPairsFile);
                string existRepChainPair = "";
                while ((existRepChainPair = repChainPairReader.ReadLine()) != null)
                {
                    repChainPairList.Add(existRepChainPair);
                }
                repChainPairReader.Close();

                dataWriter = new StreamWriter(repChainPairsFile, true);
            }
            else
            {
                dataWriter = new StreamWriter(repChainPairsFile);
            }
            string line = "";
            string repChain1 = "";
            string repChain2 = "";
            Dictionary<string, string> chainRepChainHash = new Dictionary<string,string> ();
            string repChainPair = "";
            //     ArrayList parsedChainPairList = GetParseChainPairList();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                string[] fields = ParseHelper.SplitPlus(line, '\t');
                if (fields.Length != 2)
                {
                    fields = ParseHelper.SplitPlus(line, ' ');
                }
                repChain1 = GetRepAsymChain(fields[0].Substring(0, 4), fields[0].Substring(4, fields[0].Length - 4), ref chainRepChainHash);
                repChain2 = GetRepAsymChain(fields[1].Substring(0, 4), fields[1].Substring(4, fields[1].Length - 4), ref chainRepChainHash);
                if (repChain1 == repChain2)
                {
                    continue;
                }

                if (string.Compare(repChain1, repChain2) > 0)
                {
                    repChainPair = repChain2 + "   " + repChain1;
                }
                else
                {
                    repChainPair = repChain1 + "   " + repChain2;
                }
                if (!repChainPairList.Contains(repChainPair))
                {
                    repChainPairList.Add(repChainPair);
                    dataWriter.WriteLine(repChainPair);
                    dataWriter.Flush();
                }
            }
            dataReader.Close();
            dataWriter.Close();
            return repChainPairsFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPairFile"></param>
        private string GetFatcatAlignments(string chainPairFile)
        {
            CmdOperations linuxOperator = new CmdOperations();
            string chainPairFileName = chainPairFile.Substring(chainPairFile.LastIndexOf ("\\") + 1, 
                chainPairFile.Length - chainPairFile.LastIndexOf ("\\") - 1);
            string linuxChainPairFile = "/home/qifang/Fatcat/FATCAT/FATCATMain/" + chainPairFileName;
            try
            {
                linuxOperator.CopyWindowsDataToLinux(chainPairFile, linuxChainPairFile);
            }
            catch (Exception ex)
            {
                throw new Exception("Copy Windows file to Linux error: " + ex.Message);
            }

            string fatcatCmdFile = "fatcatCmd.txt";
            string alnFile = chainPairFileName.Replace(".txt", ".aln");
            StreamWriter fatcatCmdWriter = new StreamWriter(fatcatCmdFile);
            string cmdLine = "Fatcat/FATCAT/FATCATMain/FATCATQue.pl";
            cmdLine += (" Fatcat/FATCAT/FATCATMain/" + chainPairFileName);
            cmdLine += (" -q > Fatcat/" + alnFile);
            fatcatCmdWriter.WriteLine(cmdLine);
            fatcatCmdWriter.Close();
            try
            {
                linuxOperator.RunPlink(fatcatCmdFile);
            }
            catch (Exception ex)
            {
                throw new Exception("Run Plink error: " + ex.Message);
            }

            string alnFileInWindows = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, alnFile);
            try
            {
                linuxOperator.CopyLinuxDataToWindows("/home/qifang/Fatcat/" + alnFile, alnFileInWindows);
            }
            catch (Exception ex)
            {
                throw new Exception("Copy Linux file to Windows error: " + ex.Message);
            } 
            return alnFileInWindows;
        }
        /// <summary>
        /// for small number of missing chain alignments
        /// </summary>
        public string UpdateMissingChainAlignmentsInGroups(int[] updateGroups)
        {
            HomoGroupTables.InitializeTables();
            Initialize();
  //          entryAlignment.InitializePdbCrcMapTable(pdbCrcMapTable);

            string pdbId1 = "";
            int entityId1 = -1;
            string pdbId2 = "";
            int entityId2 = -1;
            int groupId = -1;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve rep entries alignments info in each group.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Alignments info";

            string tableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            ProtCidSettings.logWriter.WriteLine(tableName);
            ProtCidSettings.logWriter.WriteLine("Retrieve rep entries alignments info in each group.");

            string missingChainPairFile = "MissingAlignPairs.txt";
            StreamWriter missingAlignPairWriter = new StreamWriter(missingChainPairFile, true);

            bool entryAlignExist = false;

            DataTable groupEntryAlignTable = GetMissingEntryAlignTable(updateGroups, tableName);
      //      int antibodyGroupId = 10400;
      //      DataTable groupEntryAlignTable = GetMissingEntryAlignTable(updateGroups, antibodyGroupId, tableName);
            //   int minGroupId = 1450;
            //   DataTable groupEntryAlignTable = GetMissingEntryAlignTable(minGroupId, tableName);

            ProtCidSettings.progressInfo.totalOperationNum = groupEntryAlignTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupEntryAlignTable.Rows.Count;

            foreach (DataRow alignRow in groupEntryAlignTable.Rows)
            {
                pdbId1 = alignRow["PdbID1"].ToString();
                entityId1 = Convert.ToInt32(alignRow["EntityID1"].ToString());
                pdbId2 = alignRow["PdbID2"].ToString();
                entityId2 = Convert.ToInt32(alignRow["EntityID2"].ToString());
                groupId = Convert.ToInt32(alignRow["GroupSeqID"].ToString());

                entryAlignExist = false;

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId1 + "_" + pdbId2;

                try
                {
                    if (entityId1 == -1 || entityId2 == -1)
                    {
                        DataTable alignmentTable = entryAlignment.RetrieveEntryAlignments(pdbId1, pdbId2);
                        if (alignmentTable.Rows.Count == 0)
                        {
                            alignmentTable = entryAlignment.RetrieveEntryHHAlignments(pdbId1, pdbId2);
                        }
                        entryAlignment.AssignAlignmentInfoToTable(groupId, HomoGroupTables.HomoGroupEntryAlign, alignmentTable);
                        if (HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Rows.Count > 0)
                        {
                            DeleteEntryAlignments(pdbId1, pdbId2, tableName);
                            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
                            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
                            entryAlignExist = true;
                        }
                    }
                    else
                    {
                        DataTable alignmentTable = entryAlignment.RetrieveChainAlignment(pdbId1, entityId1, pdbId2, entityId2);
                        if (alignmentTable.Rows.Count == 0)
                        {
                            alignmentTable = entryAlignment.RetrieveChainHHAlignment(pdbId1, entityId1, pdbId2, entityId2);
                        }
                        if (alignmentTable.Rows.Count > 0)
                        {
                            UpdateAlignmentRowInDb(alignRow, alignmentTable, tableName);
                            entryAlignExist = true;
                        }
                    }
                    if (!entryAlignExist)
                    {
                        missingAlignPairWriter.WriteLine(groupId.ToString() + "," + pdbId1 + "," +
                                            entityId1.ToString() + "," + pdbId2 + "," + entityId2.ToString());
                    }                    
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.Message;
                    missingAlignPairWriter.Flush();
                    ProtCidSettings.logWriter.WriteLine(groupId + " " + pdbId1 +  " " + pdbId2 + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    GC.Collect();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            missingAlignPairWriter.Flush();
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
            
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve rep and homo entries alignments info in each group.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Alignments info";

            tableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            ProtCidSettings.logWriter.WriteLine(tableName);
            ProtCidSettings.logWriter.WriteLine("Retrieve rep and homo entries alignments info in each group.");

            DataTable repEntryAlignTable = GetMissingEntryAlignTable(updateGroups, tableName);
            //       DataTable repEntryAlignTable = GetMissingEntryAlignTable(minGroupId, tableName);

            ProtCidSettings.progressInfo.totalOperationNum = repEntryAlignTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = repEntryAlignTable.Rows.Count;

            foreach (DataRow alignRow in repEntryAlignTable.Rows)
            {
                pdbId1 = alignRow["PdbID1"].ToString();
                entityId1 = Convert.ToInt32(alignRow["EntityID1"].ToString());
                pdbId2 = alignRow["PdbID2"].ToString();
                entityId2 = Convert.ToInt32(alignRow["EntityID2"].ToString());
                groupId = Convert.ToInt32(alignRow["GroupSeqID"].ToString());

                entryAlignExist = false;

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId1 + "_" + pdbId2;

                missingAlignPairWriter.WriteLine(groupId.ToString() + "," + pdbId1 + "," +
                   entityId1.ToString() + "," + pdbId2 + "," + entityId2.ToString());
                try
                {
                    if (entityId1 == -1 || entityId2 == -1)
                    {
                        DataTable alignmentTable = entryAlignment.RetrieveEntryAlignments(pdbId1, pdbId2);
                        entryAlignment.AssignAlignmentInfoToTable(groupId, HomoGroupTables.HomoRepEntryAlign, alignmentTable);
                        if (HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Rows.Count > 0)
                        {
                            DeleteEntryAlignments(pdbId1, pdbId2, tableName);
                            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
                            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();
                            entryAlignExist = true;
                        }
                    }
                    else
                    {
                        DataTable alignmentTable = entryAlignment.RetrieveChainAlignment(pdbId1, entityId1, pdbId2, entityId2);
                        if (alignmentTable.Rows.Count > 0)
                        {
                            UpdateAlignmentRowInDb(alignRow, alignmentTable, tableName);
                            entryAlignExist = true;
                        }
                    }
                    if (!entryAlignExist)
                    {
                        missingAlignPairWriter.WriteLine(groupId.ToString() + "," + pdbId1 + "," +
                                            entityId1.ToString() + "," + pdbId2 + "," + entityId2.ToString());
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(groupId + " " + pdbId1 + entityId1 + " " + pdbId2 + entityId2 + ": " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
            missingAlignPairWriter.Close();

            return missingChainPairFile;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetMissingEntryAlignTable(int[] updateGroups, string tableName)
        {
            string queryString = "";
            DataTable entryAlignTable = null;
            if (updateGroups == null)
            {
                queryString = string.Format("Select GroupSeqID, PdbID1, EntityID1, PdbId2, EntityID2 From {0} Where QueryStart = -1 OR EntityId1 = -1 OR EntityID2 = -1;", tableName);
           //    queryString = string.Format("Select GroupSeqID, PdbID1, EntityID1, PdbId2, EntityID2 From {0} Where EntityId1 = -1 or EntityID2 = -1;", tableName);
               entryAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            else
            {
                foreach (int groupId in updateGroups)
                {
                    queryString = string.Format("Select GroupSeqID, PdbID1, EntityID1, PdbId2, EntityID2 From {0} Where GroupSeqID = {1} AND (QueryStart = -1 OR EntityId1 = -1 OR EntityID2 = -1);",
                        tableName, groupId);
                    DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
                    if (entryAlignTable == null)
                    {
                        entryAlignTable = alignTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow alignRow in alignTable.Rows)
                        {
                            DataRow newRow = entryAlignTable.NewRow();
                            newRow.ItemArray = alignRow.ItemArray;
                            entryAlignTable.Rows.Add(newRow);
                        }
                    }
                }
            }
            return entryAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetMissingEntryEntityAlignTable(int[] updateGroups, string tableName)
        {
            string queryString = "";
            DataTable entryAlignTable = null;
            if (updateGroups == null)
            {
                queryString = string.Format("Select GroupSeqID, PdbID1, EntityID1, PdbId2, EntityID2 From {0} Where EntityId1 = -1 or EntityID2 = -1;", tableName);
                entryAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            else
            {
                foreach (int groupId in updateGroups)
                {
                    queryString = string.Format("Select GroupSeqID, PdbID1, EntityID1, PdbId2, EntityID2 From {0} " + 
                        " Where GroupSeqID = {1} AND (EntityId1 = -1 or EntityID2 = -1);",
                        tableName, groupId);
                    DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
                    if (entryAlignTable == null)
                    {
                        entryAlignTable = alignTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow alignRow in alignTable.Rows)
                        {
                            DataRow newRow = entryAlignTable.NewRow();
                            newRow.ItemArray = alignRow.ItemArray;
                            entryAlignTable.Rows.Add(newRow);
                        }
                    }
                }
            }
            return entryAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetMissingEntryAlignTable (int[] updateGroups, int excludeGroupId, string tableName)
        {
            string queryString = "";
            DataTable entryAlignTable = null;
            if (updateGroups == null)
            {
                queryString = string.Format("Select * From {0} Where GroupSeqID <> {1} AND (QueryStart = -1 or EntityId1 = -1 or EntityID2 = -1);", tableName, excludeGroupId);
         //       queryString = string.Format("Select * From {0} Where GroupSeqID <> {1} AND (QueryStart = -1 or EntityId1 = -1 or EntityID2 = -1);", tableName, excludeGroupId);
                entryAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            else
            {
                foreach (int groupId in updateGroups)
                {
                    if (groupId == excludeGroupId)
                    {
                        continue;
                    }
                    queryString = string.Format("Select * From {0} Where GroupSeqID = {1} AND (QueryStart = -1 or EntityId1 = -1 or EntityID2 = -1);", tableName, groupId);
           //         queryString = string.Format("Select * From {0} Where GroupSeqID = {1} AND (EntityId1 = -1 or EntityID2 = -1);", tableName, groupId);
                    DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
                    if (entryAlignTable == null)
                    {
                        entryAlignTable = alignTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow alignRow in alignTable.Rows)
                        {
                            DataRow newRow = entryAlignTable.NewRow();
                            newRow.ItemArray = alignRow.ItemArray;
                            entryAlignTable.Rows.Add(newRow);
                        }
                    }
                }
            }
            return entryAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetMissingEntryAlignTable(int groupId, string tableName)
        {
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1} AND (QueryStart = -1 or EntityId1 = -1 or EntityID2 = -1);",
                tableName, groupId);
            DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
            return alignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbAlignRow"></param>
        /// <param name="alignmentTable"></param>
        /// <param name="tableName"></param>
        private void UpdateAlignmentRowInDb(DataRow dbAlignRow, DataTable alignmentTable, string tableName)
        {
            if (alignmentTable.Rows.Count > 0)
            {
                string updateString = string.Format("Update {0} Set Identity = {1}, " +
                    " QueryStart = {2}, QueryEnd = {3}, HitStart = {4}, HitEnd = {5}, " +
                    " QuerySequence = '{6}', HitSequence = '{7}' " +
                    " Where GroupSeqID = {8} AND PdbID1 = '{9}' AND EntityID1 = {10} AND " +
                    " PdbID2 = '{11}' AND EntityID2 = {12};", tableName,
                    alignmentTable.Rows[0]["Identity"], alignmentTable.Rows[0]["QueryStart"],
                    alignmentTable.Rows[0]["QueryEnd"], alignmentTable.Rows[0]["HitStart"],
                    alignmentTable.Rows[0]["HitEnd"], alignmentTable.Rows[0]["QuerySequence"],
                    alignmentTable.Rows[0]["HitSequence"],
                    dbAlignRow["GroupSeqID"], dbAlignRow["PdbID1"],
                    dbAlignRow["EntityID1"], dbAlignRow["PdbID2"], dbAlignRow["EntityID2"]);
                ProtCidSettings.protcidQuery.Query(updateString);
            }
        }
        #endregion

        #region delete missing align entry interface comp
        private DbUpdate dbDelete = new DbUpdate();
        /// <summary>
        /// when rebuild pfam groups, delete those entry interface comp 
        /// with missing alignments, but with alignments this time
        /// not for update
        /// </summary>
        public void DeleteMissingAlignEntryInterfacesComp()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Delete Interface comp";

            string[] missingAlignEntryPairs = ReadMissingAlignEntryPairs();

            ProtCidSettings.progressInfo.totalOperationNum = missingAlignEntryPairs.Length;
            ProtCidSettings.progressInfo.totalStepNum = missingAlignEntryPairs.Length;
            foreach (string entryPair in missingAlignEntryPairs)
            {
                ProtCidSettings.progressInfo.currentFileName = entryPair;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                string[] entryFields = entryPair.Split(' ');
                try
                {
                    DeleteEntryInterfaceComp(entryFields[0], entryFields[1]);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(entryPair + 
                        ": Delete entry interface comp error: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadMissingAlignEntryPairs()
        {
            string line = "";
            List<string> entryPairList = new List<string> ();

            string missingEntryPairFile = "MissingAlignEntryPairs.txt";

            if (File.Exists(missingEntryPairFile))
            {
                StreamReader entryPairReader = new StreamReader(missingEntryPairFile);
                while ((line = entryPairReader.ReadLine()) != null)
                {
                    entryPairList.Add(line);
                }
                entryPairReader.Close();
            }
            else
            {
                string missingAlignEntryPairsFile = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\NonAlignedEntryPairs.txt");
                StreamWriter entryPairWriter = new StreamWriter(missingEntryPairFile);
                StreamReader dataReader = new StreamReader(missingAlignEntryPairsFile);

                string entryPair = "";
                string pdbId1 = "";
                string asymChain1 = "";
                string pdbId2 = "";
                string asymChain2 = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    pdbId1 = fields[0].Substring(0, 4);
                    asymChain1 = fields[0].Substring(4, fields[0].Length - 4);
                    pdbId2 = fields[1].Substring(0, 4);
                    asymChain2 = fields[1].Substring(4, fields[1].Length - 4);
                    entryPair = pdbId1 + " " + pdbId2;
                    /*      if (IsAlignExistInPfamGroups(fields[0].Substring(0, 4), fields[1].Substring(0, 4)))
                          {
                              continue;
                          }*/

                    if (AreInterfacesCompWithMissingAlignExist(pdbId1, asymChain1, pdbId2, asymChain2))
                    {
                        if (!entryPairList.Contains(entryPair))
                        {
                            entryPairList.Add(entryPair);
                            entryPairWriter.WriteLine(entryPair);
                        }
                    }
                }
                dataReader.Close();
                entryPairWriter.Close();
            }
            string[] missingAlignEntryPairs = new string[entryPairList.Count];
            entryPairList.CopyTo(missingAlignEntryPairs);
            return missingAlignEntryPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private bool IsAlignExistInPfamGroups(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From PfamHomoGroupEntryAlign " +
                " Where ((PdbID1 = '{0}' AND PdbID2 = '{1}') OR " +
                " (PdbID1 = '{1}' AND PdbID2 = '{0}')) AND QueryStart > -1 ;", pdbId1, pdbId2);
            DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (alignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From PfamHomoRepEntryAlign " +
                         " Where ((PdbID1 = '{0}' AND PdbID2 = '{1}') OR " +
                         " (PdbID1 = '{1}' AND PdbID2 = '{0}')) AND QueryStart > -1 ;", pdbId1, pdbId2);
                alignTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            if (alignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void DeleteEntryInterfaceComp(string pdbId1, string pdbId2)
        {
            if (AreEntryInterfaceCompExist(pdbId1, pdbId2))
            {
                string deleteString = string.Format("Delete From DifEntryInterfaceComp Where " +
                    " (PdbID1 = '{0}' AND PdbID2 = '{1}') OR " +
                    " (PdbID1 = '{1}' AND PdbID2 = '{0}');", pdbId1, pdbId2);
                dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);
             //   deleteEntryPairWriter.WriteLine(pdbId1 + " "+ pdbId2);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="asymChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="asymChain2"></param>
        /// <returns></returns>
        private bool AreInterfacesCompWithMissingAlignExist(string pdbId1, string asymChain1,
            string pdbId2, string asymChain2)
        {
            bool interfaceExist1 = AreInterfacesWithAsymChainsExist(pdbId1, asymChain1);
            bool interfaceExist2 = AreInterfacesWithAsymChainsExist(pdbId2, asymChain2);

            if (interfaceExist1 && interfaceExist2)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private bool AreEntryInterfaceCompExist(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " + 
                " Where (PdbID1 = '{0}' AND PdbID2 = '{1}') OR " +
                " (PdbID1 = '{1}' AND PdbID2 = '{0}');", pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (interfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private bool AreInterfacesWithAsymChainsExist(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces " + 
                " Where PdbID = '{0}' AND (AsymChain1 = '{1}' OR AsymChain2 = '{1}');", 
                pdbId, asymChain);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (interfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        internal void DeleteEntryAlignments(string pdbId1, string pdbId2, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID1 = '{1}' AND PdbID2 = '{2}';", tableName, pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query(deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableName"></param>
        internal void DeleteEntryAlignments(int groupId, string pdbId1, string pdbId2, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where GroupSeqId = {1} AND PdbID1 = '{2}' AND PdbID2 = '{3}';", 
                tableName, groupId, pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query(deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        public void RetrieveChainAlignmentsInGroup(int groupId)
        {
            HomoGroupTables.InitializeTables();
            string repAlignTableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            RetrieveRepEntryAlignmentInGroup(repAlignTableName, groupId);

            string repHomoAlignTableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            RetrieveRepHomoEntryAlignmentInGroup(repHomoAlignTableName, groupId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignTableName"></param>
        /// <param name="groupId"></param>
        private void RetrieveRepEntryAlignmentInGroup(string alignTableName, int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID1 From {0}  Where GroupSeqID = {1};", alignTableName, groupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> repPdbList = new List<string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                repPdbList.Add(entryRow["PdbId1"].ToString());
            }
            queryString = string.Format("Select Distinct PdbID2 From {0}  Where GroupSeqID = {1};", alignTableName, groupId);
            entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                if (!repPdbList.Contains(entryRow["PdbID2"].ToString()))
                {
                    repPdbList.Add(entryRow["PdbID2"].ToString());
                }
            }
            string[] repPdbIds = repPdbList.ToArray();
            try
            {
                if (repPdbList.Count > numOfRepEntriesInGroupCutoff)
                {
                    entryAlignment.GetEntryPairAlignmentInBigGroup(repPdbIds, groupId, alignTableName);
                }
                else
                {
                    entryAlignment.GetEntryPairAlignment(repPdbIds, groupId);

                    //  clear the unaligned data between representative entries in a group
                    string deleteString = string.Format("Delete From {0} Where GroupSeqID = '{1}';", alignTableName, groupId);
                    ProtCidSettings.protcidQuery.Query(deleteString);
                    dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
                    HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving alignments between rep entries errors:" + ex.Message +
                    " in group " + groupId.ToString());
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignTableName"></param>
        /// <param name="groupId"></param>
        private void RetrieveRepHomoEntryAlignmentInGroup(string alignTableName, int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID1, PdbID2 From {0} " +
                            " Where GroupSeqID = {1};", alignTableName, groupId);
            DataTable alignEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string repPdbId = "";
            string pdbId2 = "";
            Dictionary<string, List<string>> repHomoEntryHash =  new Dictionary<string, List<string>> ();
            foreach (DataRow alignEntryRow in alignEntryTable.Rows)
            {
                repPdbId = alignEntryRow["PdbID1"].ToString();
                pdbId2 = alignEntryRow["PdbID2"].ToString();
                if (repHomoEntryHash.ContainsKey(repPdbId))
                {
                    List<string> homoPdbList = (List<string>)repHomoEntryHash[repPdbId];
                    if (!homoPdbList.Contains(pdbId2))
                    {
                        homoPdbList.Add(pdbId2);
                    }
                }
                else
                {
                    List<string> homoPdbList = new List<string>();
                    homoPdbList.Add(pdbId2);
                    repHomoEntryHash.Add(repPdbId, homoPdbList);
                }
            }
            try
            {
                foreach (string repPdb in repHomoEntryHash.Keys)
                {
                    List<string> homoPdbList = (List<string>)repHomoEntryHash[repPdb];
                    entryAlignment.RetrieveRepEntryAlignment(repPdb, homoPdbList.ToArray (), groupId);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving alignments between Rep and homo entries errors:" 
                    + ex.Message + " in group " + groupId.ToString ());
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();
                return;
            }
            //  clear the unaligned data between representative entry and its homologous entries
            string deleteString = string.Format("Delete From {0} Where GroupSeqID = '{1}';", alignTableName, groupId);
            ProtCidSettings.protcidQuery.Query(deleteString);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();
        }
        #endregion

        #region Missing chain pairs
        #region all missing chain pairs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignType"></param>
        public void FindMissingChainAlignments()
        {
            Initialize();

            Dictionary<string,string[]> entryRepAuthChainDict = new Dictionary<string,string[]> ();
            StreamWriter dataWriter = new StreamWriter("missingChainPairs.txt");
            string dbTableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            string queryString = string.Format("Select PdbID1, PdbID2 From {0} Where QueryStart = -1;", dbTableName);
            DataTable groupHomoAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            FindMissingChainPairs(groupHomoAlignTable, dataWriter, ref entryRepAuthChainDict);
            dataWriter.Flush();

            dbTableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            queryString = string.Format("Select PdbId1, PdbID2 From {0} Where QueryStart = -1;", dbTableName);
            DataTable groupRepHomoAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
            FindMissingChainPairs(groupRepHomoAlignTable, dataWriter, ref entryRepAuthChainDict);

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignType"></param>
        public string FindMissingChainAlignments(int[] updateGroups)
        {
            Initialize();
            string queryString = "";        

            Dictionary<string, string[]> entryRepAuthChainDict = new Dictionary<string,string[]> ();            
            string dbTableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            List<string> repChainPairList = new List<string> ();
            foreach (int groupId in updateGroups)
            {
                queryString = string.Format("Select PdbID1, PdbID2 From {0} " +
                    " Where GroupSeqID = {1} AND QueryStart = -1;", dbTableName, groupId);
                DataTable groupHomoAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
                FindMissingChainPairs(groupHomoAlignTable, ref repChainPairList, ref entryRepAuthChainDict);
            }

            dbTableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            foreach (int groupId in updateGroups)
            {
                queryString = string.Format("Select PdbId1, PdbID2 From {0}" +
                    " Where GroupSeqID = {1} AND QueryStart = -1;", dbTableName, groupId);
                DataTable groupRepHomoAlignTable = ProtCidSettings.protcidQuery.Query(queryString);
                FindMissingChainPairs(groupRepHomoAlignTable, ref repChainPairList, ref entryRepAuthChainDict);
            }
            string chainPairFileName = "missingChainPairs.txt";
            StreamWriter dataWriter = new StreamWriter(chainPairFileName);
            foreach (string chainPair in repChainPairList)
            {
                dataWriter.WriteLine(chainPair);
            }
            dataWriter.Close();
            return chainPairFileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupHomoAlignTable"></param>
        /// <param name="dataWriter"></param>
        /// <param name="entryRepAuthChainHash"></param>
        /// <param name="alignType"></param>
        private void FindMissingChainPairs(DataTable groupHomoAlignTable, ref List<string> chainPairList,
           ref Dictionary<string, string[]> entryRepAsymChainHash)
        {
            string pdbId1 = "";
            string[] repAsymChains1 = null;
            string pdbId2 = "";
            string[] repAsymChains2 = null;
            string chainPair = "";
            foreach (DataRow dRow in groupHomoAlignTable.Rows)
            {
                pdbId1 = dRow["PdbId1"].ToString();
                repAsymChains1 = GetRepAsymChains(pdbId1, ref entryRepAsymChainHash);
                if (repAsymChains1.Length == 0) // not representative chain
                {
                    continue;
                }
                pdbId2 = dRow["PdbID2"].ToString();
                repAsymChains2 = GetRepAsymChains(pdbId2, ref entryRepAsymChainHash);
                if (repAsymChains2.Length == 0) // not representative chain
                {
                    continue;
                }
                foreach (string repAsymChain1 in repAsymChains1)
                {
                    foreach (string repAsymChain2 in repAsymChains2)
                    {
                        if (repAsymChain1 == repAsymChain2)
                        {
                            continue;
                        }
                        if (string.Compare (repAsymChain1, repAsymChain2) > 0)
                        {
                            chainPair = repAsymChain2 + "   " + repAsymChain1;
                        }
                        else
                        {
                            chainPair = repAsymChain1 + "   " + repAsymChain2;
                        }
                        if (!chainPairList.Contains(chainPair))
                        {
                            chainPairList.Add (chainPair);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupHomoAlignTable"></param>
        /// <param name="dataWriter"></param>
        /// <param name="entryRepAuthChainHash"></param>
        /// <param name="alignType"></param>
        private void FindMissingChainPairs(DataTable groupHomoAlignTable, StreamWriter dataWriter,
           ref Dictionary<string,string[]> entryRepAsymChainDict)
        {
            string pdbId1 = "";
            string[] repAsymChains1 = null;
            string pdbId2 = "";
            string[] repAsymChains2 = null;
            foreach (DataRow dRow in groupHomoAlignTable.Rows)
            {
                pdbId1 = dRow["PdbId1"].ToString();
                repAsymChains1 = GetRepAsymChains(pdbId1, ref entryRepAsymChainDict);
                if (repAsymChains1.Length == 0) // not representative chain
                {
                    continue;
                }
                pdbId2 = dRow["PdbID2"].ToString();
                repAsymChains2 = GetRepAsymChains(pdbId2, ref entryRepAsymChainDict);
                if (repAsymChains2.Length == 0) // not representative chain
                {
                    continue;
                }
                foreach (string repAsymChain1 in repAsymChains1)
                {
                    foreach (string repAsymChain2 in repAsymChains2)
                    {
                        if (repAsymChain1 == repAsymChain2)
                        {
                            continue;
                        }
                        // if it is a good psiblast alignments
                        // no need structure alignments
                 /*      if (IsGoodPsiblastAlignmentExist(pdbId1, repAuthChain1, pdbId2, repAuthChain2))
                        {
                            continue;
                        }
                        // the alignment already exist
                        if (IsChainAlignmentExist(pdbId1, repAuthChain1, pdbId2, repAuthChain2, alignType))
                        {
                            continue;
                        }*/
                        dataWriter.WriteLine(repAsymChain1 + "   " + repAsymChain2);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryRepAuthChainHash"></param>
        /// <returns></returns>
        private string[] GetRepAsymChains(string pdbId, ref Dictionary<string, string[]> entryRepAsymChainDict)
        {
            string[] repAsymChains = null;
            if (entryRepAsymChainDict.ContainsKey(pdbId))
            {
                repAsymChains = (string[])entryRepAsymChainDict[pdbId];
            }
            else
            {
                repAsymChains = GetEntryRepChains(pdbId);
                entryRepAsymChainDict.Add(pdbId, repAsymChains);
            }
            return repAsymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryRepChains(string pdbId)
        {
            string crc = "";
            List<string> repAsymchainList = new List<string>();

            string queryString = string.Format("Select Distinct crc From PdbCrcMap where PdbID = '{0}';", pdbId);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            foreach (DataRow crcRow in crcTable.Rows)
            {
                crc = crcRow["crc"].ToString().TrimEnd();
                queryString = string.Format("Select PdbID, AsymID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (repChainTable.Rows.Count > 0)
                {
                    repAsymchainList.Add(repChainTable.Rows[0]["PdbID"].ToString() + repChainTable.Rows[0]["AsymID"].ToString().TrimEnd());
                }
            }
            if (repAsymchainList.Count < 0)
            {
                string[] entryAsymChains = GetEntryAsymChains(pdbId);
                return entryAsymChains;
            }
            return repAsymchainList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryAsymChains(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit " + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityAsymIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<int> entityList = new List<int> ();
            List<string> entryAsymChainList = new List<string> ();
            int entityId = -1;
            string asymChain = "";
            foreach (DataRow entityRow in entityAsymIdTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString ());
                if (entityList.Contains(entityId))
                {
                    continue;
                }
                entityList.Add(entityId);
                asymChain = entityRow["AsymID"].ToString().TrimEnd();
                entryAsymChainList.Add(pdbId + asymChain);
            }
            string[] entryAsymChains = new string[entryAsymChainList.Count];
            entryAsymChainList.CopyTo(entryAsymChains);
            return entryAsymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="chainRepChainHash"></param>
        /// <returns></returns>
        private string GetRepAsymChain(string pdbId, string asymChain, ref Dictionary<string, string> chainRepChainHash)
        {
            if (chainRepChainHash.ContainsKey(pdbId + asymChain))
            {
                return (string)chainRepChainHash[pdbId + asymChain];
            }
            else
            {
                string repChain = "";
                string crc = "";
                string queryString = string.Format("Select crc From PdbCrcMap Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (crcTable.Rows.Count > 0)
                {
                    crc = crcTable.Rows[0]["crc"].ToString().TrimEnd();
                }
                queryString = string.Format("Select PdbID, AsymID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                if (repChainTable.Rows.Count > 0)
                {
                    repChain = repChainTable.Rows[0]["PdbID"].ToString() + repChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
                }
                else
                {
                    repChain = pdbId + asymChain;
                }
                chainRepChainHash.Add(pdbId + asymChain, repChain);
                return repChain;
            }
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable ReadRedundantChainsTable()
        {
            DataTable reduntChainTable = new DataTable();
            reduntChainTable.Columns.Add(new DataColumn ("PdbID1"));
            reduntChainTable.Columns.Add(new DataColumn("ChainID1"));
            reduntChainTable.Columns.Add(new DataColumn("PdbID2"));
            reduntChainTable.Columns.Add(new DataColumn("ChainID2"));
            StreamReader dataReader = new StreamReader("uniqueEntitiesInfo.txt");
            string line = "";
            string pdbId1 = "";
            string asymChainId1 = "";
            string pdbId2 = "";
            string asymChainId2 = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                string[] fields = line.Split('\t');
                string[] chainInfoFields1 = fields[0].Split (';');
                pdbId1 = chainInfoFields1[0].Substring(0, 4);
                asymChainId1 = chainInfoFields1[1];
                for (int i = 1; i < fields.Length; i++)
                {
                    string[] chainInfoFields2 = fields[i].Split(';');
                    pdbId2 = chainInfoFields2[0].Substring(0, 4);
                    asymChainId2 = chainInfoFields2[1];
                    DataRow dataRow = reduntChainTable.NewRow();
                    dataRow["PdbID1"] = pdbId1;
                    dataRow["ChainId1"] = asymChainId1;
                    dataRow["PdbID2"] = pdbId2;
                    dataRow["ChainID2"] = asymChainId2;
                    reduntChainTable.Rows.Add(dataRow);
                }
            }
            dataReader.Close();
            return reduntChainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="authChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="authChain2"></param>
        /// <param name="alignType"></param>
        /// <returns></returns>
        private bool IsChainAlignmentExist(string pdbId1, string authChain1, string pdbId2, string authChain2,
            string alignType)
        {
            string queryString = string.Format("Select * From {0}Alignments " +
                " Where QueryEntry = '{1}' AND QueryChain = '{2}' " +
                " AND HitEntry = '{3}' AND HitChain = '{4}';",
                alignType, pdbId1, authChain1, pdbId2, authChain2);
            DataTable alignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            if (alignTable.Rows.Count > 0)
            {
                return true;
            }
            queryString = string.Format("Select * From {0}Alignments " +
                " Where QueryEntry = '{1}' AND QueryChain = '{2}' " +
                " AND HitEntry = '{3}' AND HitChain = '{4}';",
                alignType, pdbId2, authChain2, pdbId1, authChain1);
            alignTable = ProtCidSettings.alignmentQuery.Query( queryString);
            if (alignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
   
        #endregion

        #region missing representative chains from the output file from EntryAlignments
        /// <summary>
        /// Find those alignments for representative chains in RedundantPdbChains
        /// The file "NonAlignedEntries.txt" is the output from EntryAlignments
        /// which contains all missing alignments needed, 
        /// but may include some unnecessary alignments
        /// </summary>
        /// <param name="alignType"></param>
        public void FindMissingRepChainAlignments(string alignType)
        {
            string filePath = Path.Combine (ProtCidSettings.dirSettings.fatcatPath, "chainAlignments");
            string missAlignmentFile = Path.Combine(filePath, "NonAlignedEntries.txt");
            StreamReader dataReader = new StreamReader(missAlignmentFile);
            StreamWriter dataWriter = new StreamWriter(Path.Combine(filePath, "Missing" + alignType + "AlignedChainPairs.txt"));
            string line = "";
            Dictionary<string, bool> chainIsRepDict = new Dictionary<string,bool> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] entryChains = line.Split('\t');
                if (IsRepAlignment(entryChains[0], entryChains[1], ref chainIsRepDict))
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// the representative chain pairs for the NonALignedEntries
        /// </summary>
        public void FindMissingRepChainPairs()
        {
#if DEBUG
            EntryAlignment.nonAlignedDataWriter.Close();
#endif
            string missingAllChainPairFile = "NonAlignedEntryPairs.txt";
            string nonChainPairsFile = Path.Combine (ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\NonAlignedChainPairs.txt");
            StreamReader dataReader = new StreamReader(missingAllChainPairFile);
            StreamWriter dataWriter = new StreamWriter(nonChainPairsFile);
            string line = "";
            string repChain1 = "";
            string repChain2 = "";
            Dictionary<string, string> chainRepChainDict = new Dictionary<string,string> ();
            List<string> repChainPairList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                repChain1 = GetRepChain(fields[0].Substring(0, 4), fields[0].Substring(4, fields[0].Length - 4), ref chainRepChainDict);
                repChain2 = GetRepChain(fields[1].Substring(0, 4), fields[1].Substring(4, fields[1].Length - 4), ref chainRepChainDict);
                if (!repChainPairList.Contains(repChain1 + "   " + repChain2))
                {
                    repChainPairList.Add(repChain1 + "   " + repChain2);
                    dataWriter.WriteLine(repChain1 + "   " + repChain2);
                }
            }
            dataReader.Close();
            dataWriter.Close();
        //    string nonChainPairsFile = @"D:\DbProjectData\Fatcat\missingChainPairs.txt";
            int numOfChainPairsCutoff = 2000;
            DivideNonAlignedChainPairs(nonChainPairsFile, numOfChainPairsCutoff);
        }

        /// <summary>
        /// the representative chain pairs for the NonALignedEntries
        /// </summary>
        public void FindMissingEntryPairs()
        {
#if DEBUG
            EntryAlignment.nonAlignedDataWriter.Close();
#endif
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string repChainPairsFile = Path.Combine (ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\NonAlignRepPairs.txt");
            string missingAllChainPairFile = Path.Combine (ProtCidSettings.dirSettings.fatcatPath, "ChainAlignments\\MissingAlignPairs.txt");
            StreamReader dataReader = new StreamReader(missingAllChainPairFile);
            string line = "";
            List<string> repChainPairList = new List<string> ();

            if (File.Exists(repChainPairsFile))
            {
                StreamReader repChainPairReader = new StreamReader(repChainPairsFile);
                while ((line = repChainPairReader.ReadLine()) != null)
                {
                    repChainPairList.Add(line);
                }
                repChainPairReader.Close();
            }
            StreamWriter dataWriter = new StreamWriter(repChainPairsFile, true);
            
            string[] repChains1 = null;
            string[] repChains2 = null;
            Dictionary<string, string[]> entryRepChainDict = new Dictionary<string,string[]> ();
            
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');

                ProtCidSettings.progressInfo.currentFileName = fields[1] + "-" + fields[3];

                repChains1 = GetRepChains(fields[1], ref entryRepChainDict);
                repChains2 = GetRepChains(fields[3], ref entryRepChainDict);

                foreach (string repChain1 in repChains1)
                {
                    foreach (string repChain2 in repChains2)
                    {
                        if (repChain1 == repChain2)
                        {
                            continue;
                        }
                        if (!repChainPairList.Contains(repChain1 + "   " + repChain2))
                        {
                            repChainPairList.Add(repChain1 + "   " + repChain2);
                            dataWriter.WriteLine(repChain1 + "   " + repChain2);
                        }
                    }
                }
            }
            dataReader.Close();
            dataWriter.Close();
            int numOfChainPairsCutoff = 1000;
            DivideNonAlignedChainPairs(repChainPairsFile, numOfChainPairsCutoff);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairsFile"></param>
        /// <param name="numOfChainPairsCutoff"></param>
        public void DivideNonAlignedChainPairs(string nonAlignedChainPairsFile, int numOfChainPairsCutoff)
        {
            StreamReader dataReader = new StreamReader(nonAlignedChainPairsFile);
            FileInfo fileInfo = new FileInfo(nonAlignedChainPairsFile);
            string pairFileName = fileInfo.Name.Replace(".txt", "");
            string lsFileName = Path.Combine(fileInfo.DirectoryName, "fileList.txt");
            StreamWriter lsFileWriter = new StreamWriter(lsFileName);
            string line = "";
            int fileNum = 0;
            int numOfChainPairs = 0;
            string dividedChainPairsFilePrefix = nonAlignedChainPairsFile.Remove(nonAlignedChainPairsFile.IndexOf(".txt"));
            StreamWriter dataWriter = new StreamWriter(dividedChainPairsFilePrefix + "0.txt");
            lsFileWriter.WriteLine(pairFileName + "0.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                numOfChainPairs ++ ;
                if (numOfChainPairs > numOfChainPairsCutoff)
                {
                    dataWriter.Close();
                    fileNum++;
                    dataWriter = new StreamWriter(dividedChainPairsFilePrefix + fileNum.ToString() + ".txt");
                    lsFileWriter.WriteLine(pairFileName + fileNum.ToString() + ".txt");
                    numOfChainPairs = 0;
                }
                dataWriter.WriteLine(line);
            }
            dataReader.Close();
            dataWriter.Close();
            lsFileWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainRepChainHash"></param>
        /// <returns></returns>
        private string GetRepChain(string pdbId, string asymChain, ref Dictionary<string, string> chainRepChainHash)
        {
            if (chainRepChainHash.ContainsKey(pdbId + asymChain))
            {
                return (string)chainRepChainHash[pdbId + asymChain];
            }
            else
            {
                string repChain = "";
                string crc = "";

                string queryString = string.Format("Select crc From PdbCrcMap Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                if (crcTable.Rows.Count > 0)
                {
                    queryString = string.Format("Select PdbID, AsymID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                    DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                    if (repChainTable.Rows.Count > 0)
                    {
                        repChain = repChainTable.Rows[0]["PdbID"].ToString() + repChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
                    }
                }
                if (repChain == "")
                {
                    repChain = pdbId + asymChain;
                }

                chainRepChainHash.Add(pdbId + asymChain, repChain);
                return repChain;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainRepChainHash"></param>
        /// <returns></returns>
        private string[] GetRepChains(string pdbId, ref Dictionary<string, string[]> entryRepChainsDict)
        {
            if (entryRepChainsDict.ContainsKey(pdbId))
            {
                return (string[])entryRepChainsDict[pdbId];
            }
            else
            {
                string crc = "";
                List<string> repChainList = new List<string>();

                string queryString = string.Format("Select distinct crc From PdbCrcMap Where PdbID = '{0}';", pdbId);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                foreach (DataRow crcRow in crcTable.Rows)
                {
                    queryString = string.Format("Select PdbID, AsymID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                    DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                    foreach (DataRow chainRow in repChainTable.Rows)
                    {
                        repChainList.Add(chainRow["PdbID"].ToString() + chainRow["AsymID"].ToString().TrimEnd());
                    }
                }
                string[] repChains = repChainList.ToArray();
                if (repChains.Length == 0)
                {
                    repChains = GetEntryAsymChains(pdbId);
                }
                entryRepChainsDict.Add(pdbId, repChains);

                return repChains;
            }
        }

        /// <summary>
        /// Is the alignment for representative chains
        /// </summary>
        /// <param name="entryChain1"></param>
        /// <param name="entryChain2"></param>
        /// <param name="chainRepHash"></param>
        /// <returns></returns>
        private bool IsRepAlignment(string entryChain1, string entryChain2, ref Dictionary<string, bool> chainIsRepDict)
        {
            bool isRepChain = IsRepChain(entryChain1, ref chainIsRepDict);
            if (!isRepChain)
            {
                return false;
            }
            isRepChain = IsRepChain(entryChain2, ref chainIsRepDict);
            if (!isRepChain)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// is the chain a representative chain in redundantpdbchains
        /// </summary>
        /// <param name="entryChain"></param>
        /// <param name="chainRepHash"></param>
        /// <returns></returns>
        private bool IsRepChain(string entryChain, ref Dictionary<string, bool> chainRepHash)
        {
            bool isRepChain = false;
            if (chainRepHash.ContainsKey(entryChain))
            {
                isRepChain = (bool)chainRepHash[entryChain];
            }
            else
            {
                isRepChain = IsRepChain(entryChain.Substring(0, 4), entryChain.Substring(4, entryChain.Length - 4));
                chainRepHash.Add(entryChain, isRepChain);
            }
            return isRepChain;
        }

        /// <summary>
        /// Is the chain a representative chain in the redundant pdb chains 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <returns></returns>
        private bool IsRepChain(string pdbId, string asymChain)
        {

            string queryString = string.Format("Select IsRep From PdbCrcMap " +
                " Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (repChainTable.Rows.Count > 0)
            {
                if (repChainTable.Rows[0]["IsRep"].ToString() == "1")
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
        #endregion

        #region fill out missing entityid
        /// <summary>
        /// 
        /// </summary>
        public void FillOutMissingEntityIDs()
        {
            StreamWriter dataWriter = new StreamWriter("MissingEntityEntries.txt", true);
            string tableName = "PfamHomoGroupEntryAlign";
            UpdateTableMissingEntityIDs(tableName, dataWriter);
            tableName = "PfamHomoRepEntryAlign";
            UpdateTableMissingEntityIDs(tableName, dataWriter);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="dataWriter"></param>
        private void UpdateTableMissingEntityIDs(string tableName, StreamWriter dataWriter)
        {
            string queryString = "";
            string pdbId = "";
            int entityId = -1;
            queryString = string.Format("Select Distinct PdbID1, EntityID1 From {0} Where EntityID1 = -1;",
                 tableName);
            DataTable entityTable1 = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entityRow in entityTable1.Rows)
            {
                pdbId = entityRow["PdbID1"].ToString();
                entityId = GetEntryEntity(pdbId);
                if (entityId != -1)
                {
                    dataWriter.WriteLine(pdbId);
                    UpdateMissingEntityID(pdbId, entityId, tableName, 1);
                }
            }
            dataWriter.Flush();

            queryString = string.Format("Select Distinct PdbID2, EntityID2 From {0} WHERE EntityID2 = -1;",
                tableName);
            DataTable entityTable2 = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entityRow in entityTable2.Rows)
            {
                pdbId = entityRow["PdbID2"].ToString();
                entityId = GetEntryEntity(pdbId);
                if (entityId != -1)
                {
                    dataWriter.WriteLine(pdbId);
                    UpdateMissingEntityID(pdbId, entityId, tableName, 2);
                }
            }
            dataWriter.Flush();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <param name="?"></param>
        private void UpdateMissingEntityID(string pdbId, int entityId, string tableName, int colNum)
        {
            string updateString = "";
            if (colNum == 1)
            {
                updateString = string.Format("Update {0} Set EntityID1 = {1} Where PdbID1 = '{2}';",
                    tableName, entityId, pdbId);
            }
            else
            {
                updateString = string.Format("Update {0} Set EntityID2 = {1} Where PdbID2 = '{2}';",
                    tableName, entityId, pdbId);
            }
            ProtCidSettings.protcidQuery.Query (updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetEntryEntity(string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", 
                pdbId);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (entityTable.Rows.Count == 1)
            {
                return Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString ());
            }
            return -1;
        }
        #endregion

        #region fill out the entry alignments from the backup database
        public void FilloutEntryAlignments(string[] updateEntries)
        {
            DbConnect backupDbConnect = new DbConnect();
            string backupDbPath = @"F:\Firebird\Xtal\pfamv25\protbud.fdb";
            backupDbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                   backupDbPath;
            backupDbConnect.ConnectToDatabase();

            string tableName = "PfamHomoRepEntryAlign";
     ////       FilloutEntryAlignments(tableName, backupDbConnect, updateEntries);

            tableName = "PfamHomoGroupEntryAlign";
            FilloutEntryAlignments (tableName, backupDbConnect, updateEntries);

            backupDbConnect.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="backupDbConnect"></param>
        public void FilloutEntryAlignments(string tableName, DbConnect backupDbConnect, string[] updateEntries)
        {
            string queryString = string.Format("Select Distinct GroupSeqID, PdbID1, PdbID2 From {0} Where QueryStart = -1;", tableName);
            DataTable entryPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId1 = "";
            string pdbId2 = "";
            int groupId = 0;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryPairTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryPairTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Fill out entry alignments for " + tableName);

            foreach (DataRow entryPairRow in entryPairTable.Rows)
            {
                groupId = Convert.ToInt32(entryPairRow["GroupSeqID"].ToString ());
                pdbId1 = entryPairRow["PdbID1"].ToString();
                pdbId2 = entryPairRow["PdbID2"].ToString();

                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId1 + "-" + pdbId2;

                if (Array.IndexOf(updateEntries, pdbId1) > -1 || Array.IndexOf(updateEntries, pdbId2) > -1)
                {
                    continue;
                }

                DataTable entryAlignTable = GetEntryAlignments(groupId, pdbId1, pdbId2, tableName, backupDbConnect);
                if (entryAlignTable.Rows.Count > 0)
                {
                    DeleteEntryAlignments(groupId, pdbId1, pdbId2, tableName);
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, entryAlignTable);
                    entryAlignTable.Clear();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private DataTable GetEntryAlignments(int groupId, string pdbId1, string pdbId2, string tableName, DbConnect dbConnect)
        {
            string queryString = string.Format("Select * From {0} Where PdbId1 = '{1}' AND PdbID2 = '{2}';",
                tableName, pdbId1, pdbId2);
            DataTable entryAlignTable = dbQuery.Query(dbConnect, queryString);
            entryAlignTable.TableName = tableName;
            DataColumn dCol = new DataColumn("GroupSeqID");
            dCol.DefaultValue = groupId;
            entryAlignTable.Columns.Remove("GroupSeqID");
            entryAlignTable.Columns.Add(dCol);
            entryAlignTable.AcceptChanges();
            return entryAlignTable;
        }
        #endregion

        #region fill out the disorder residues in the FATCAT alignments       
//        private StreamWriter deletedAlignDataWriter = new StreamWriter("DeleteChainAlignments.txt", true);
        public void FillOutDisorderResidues()
        {
            AlignSeqNumConverter seqConverter = new AlignSeqNumConverter();
     //       OutputEntriesAfterSomeDay();
            string parsedEntryFile = "ParsedEntries.txt";
            string[] parsedEntries = null;
            string[] updateEntries = ReadUpdateEntries(parsedEntryFile, out parsedEntries);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Fill out disorder residues in the FATCAT Alignments");

            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            StreamWriter parsedEntryWriter = new StreamWriter(parsedEntryFile, true);
            List<string> parsedEntryList = new List<string> (parsedEntries);

            StreamWriter updateEntryWriter = new StreamWriter("updatedEntries.txt", true);
            int parseStatus = 0;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    parseStatus = FillOutEntryChainAlignments(pdbId, parsedEntryList, seqConverter);
                    parsedEntryWriter.WriteLine(pdbId);
                    parsedEntryWriter.Flush();
                    parsedEntryList.Add(pdbId);
                    if (parseStatus == 1)
                    {
                        updateEntryWriter.WriteLine(pdbId);
                        updateEntryWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update chain alignments for " + pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Update domain alignments for " + pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }

            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
 //           deletedAlignDataWriter.Close();
            parsedEntryWriter.Close();
            updateEntryWriter.Close();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public int FillOutEntryChainAlignments(string pdbId, List<string> parsedEntryList, AlignSeqNumConverter seqConverter)
        {
            string[] asymChainsWithDisorder = GetAsymChainsWithDisorder(pdbId, seqConverter);
            if (asymChainsWithDisorder.Length == 0)
            {
                return 0;
            }
            DataTable alignTable = GetEntryAlignmentTable(pdbId, asymChainsWithDisorder);
            if (alignTable.Rows.Count == 0)
            {
                return 0;
            }

            List<string> deleteChainPairList = new List<string> ();
            string chainPair = "";
            RemoveParsedAlignments(alignTable, parsedEntryList);

//            deletedAlignDataWriter.WriteLine(ParseHelper.FormatDataRows(alignTable.Select()));
//            deletedAlignDataWriter.Flush();

            List<DataRow> notParsedAlignRowList = new List<DataRow> ();
            for (int i = 0; i < alignTable.Rows.Count; i++)
            {
                DataRow alignRow = alignTable.Rows[i];

                chainPair = alignRow["QueryEntry"].ToString() + "_" + alignRow["QueryAsymChain"].ToString().TrimEnd() + "_" +
                       alignRow["HitEntry"].ToString() + "_" + alignRow["HitAsymChain"].ToString().TrimEnd();

                AlignSeqInfo alignInfo1 = new AlignSeqInfo();
                alignInfo1.pdbId = alignRow["QueryEntry"].ToString();
                alignInfo1.asymChainId = alignRow["QueryAsymChain"].ToString().TrimEnd();
                alignInfo1.chainId = alignRow["QueryChain"].ToString().TrimEnd();
                alignInfo1.alignStart = Convert.ToInt32(alignRow["QueryStart"].ToString());
                alignInfo1.alignEnd = Convert.ToInt32(alignRow["QueryEnd"].ToString());
                alignInfo1.alignSequence = alignRow["QuerySequence"].ToString();

                AlignSeqInfo alignInfo2 = new AlignSeqInfo();
                alignInfo2.pdbId = alignRow["HitEntry"].ToString();
                alignInfo2.asymChainId = alignRow["HitAsymChain"].ToString().TrimEnd();
                alignInfo2.chainId = alignRow["HitChain"].ToString().TrimEnd();
                alignInfo2.alignStart = Convert.ToInt32(alignRow["HitStart"].ToString());
                alignInfo2.alignEnd = Convert.ToInt32(alignRow["HitEnd"].ToString());
                alignInfo2.alignSequence = alignRow["HitSequence"].ToString();

                try
                {
                    seqConverter.AddDisorderResiduesToAlignment(ref alignInfo1, ref alignInfo2);

                    alignRow["QuerySequence"] = alignInfo1.alignSequence;
                    alignRow["HitSequence"] = alignInfo2.alignSequence;

                    deleteChainPairList.Add(chainPair);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain alignments " + chainPair + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Update domain alignments " + chainPair + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    notParsedAlignRowList.Add(alignRow);
                    continue;
                }
            }
            foreach (DataRow notParsedRow in notParsedAlignRowList)
            {
                alignTable.Rows.Remove(notParsedRow);
            }
            string[] deleteChainPairs = new string[deleteChainPairList.Count];
            deleteChainPairList.CopyTo(deleteChainPairs);
            DeleteChainAlignments(deleteChainPairs);

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, alignTable);
            if (alignTable.Rows.Count > 0)
            {
                return 1;
            }
            return -1;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetAsymChainsWithDisorder(string pdbId, AlignSeqNumConverter seqConverter)
        {
            string queryString = string.Format("Select AsymID, SequenceInCoord, PolymerType From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable seqInCoordTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string polymerType = "";
            string asymChain = "";
            string seqInCoord = "";
            List<string> asymChainList = new List<string> ();
            foreach (DataRow seqRow in seqInCoordTable.Rows)
            {
                polymerType = seqRow["PolymerType"].ToString().TrimEnd();
                if (polymerType == "polypeptide")
                {
                    asymChain = seqRow["AsymID"].ToString().TrimEnd();
                    seqInCoord = seqRow["SequenceInCoord"].ToString();
                    if (seqConverter.IsSequenceWithDisorderResidues(seqInCoord))
                    {
                        asymChainList.Add(asymChain);
                    }
                }
            }
            string[] asymChainsWithDisorder = asymChainList.ToArray ();
            return asymChainsWithDisorder;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPairs"></param>
        private void DeleteChainAlignments(string[] chainPairs)
        {
            foreach (string chainPair in chainPairs)
            {
                string[] chainFields = chainPair.Split('_');
                DeleteChainAlignment(chainFields[0], chainFields[1], chainFields[2], chainFields[3]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="asymChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="asymChain2"></param>
        private void DeleteChainAlignment(string pdbId1, string asymChain1, string pdbId2, string asymChain2)
        {
            string deleteString = string.Format("Delete From FatcatAlignments Where " + 
                "QueryEntry = '{0}' AND QueryAsymChain = '{1}' AND  " + 
                " HitEntry = '{2}' AND HitAsymChain = '{3}';", pdbId1, asymChain1, pdbId2, asymChain2);
            dbDelete.Delete(ProtCidSettings.alignmentDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignTable"></param>
        /// <param name="parsedEntryList"></param>
        private void RemoveParsedAlignments(DataTable alignTable, List<string> parsedEntryList)
        {
            List<DataRow> removedDataRowList = new List<DataRow> ();
            string queryEntry = "";
            string hitEntry = "";
            foreach (DataRow alignRow in alignTable.Rows)
            {
                queryEntry = alignRow["QueryEntry"].ToString();
                hitEntry = alignRow["HitEntry"].ToString();
                if (parsedEntryList.Contains(queryEntry) || parsedEntryList.Contains(hitEntry))
                {
                    removedDataRowList.Add(alignRow);
                }
            }
            foreach (DataRow alignRow in removedDataRowList)
            {
                alignTable.Rows.Remove(alignRow);
            }
            alignTable.AcceptChanges ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryAlignmentTable(string pdbId, string[] asymChainsWithDisorder)
        {
            string queryString = string.Format("Select * From FATCATAlignments Where " +
                " (QueryEntry = '{0}' AND QueryAsymChain IN ({1})) OR " +
                " (HitEntry = '{0}' AND HitAsymChain IN ({1}));", 
                pdbId, ParseHelper.FormatSqlListString(asymChainsWithDisorder));
            DataTable alignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            alignTable.TableName = "FatcatAlignments";
            return alignTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadUpdateEntries(string parsedEntryFile, out string[] parsedEntries)
        {
            parsedEntries = ReadEntries(parsedEntryFile);
            string entryFile = @"D:\DbProjectData\PDB\newFatcatEntries.txt";
            string[] entries = ReadEntries(entryFile);
            List<string> leftEntryList = new List<string> ();
            foreach (string entry in entries)
            {
                if (Array.IndexOf(parsedEntries, entry) > -1)
                {
                    continue;
                }
                leftEntryList.Add(entry);
            }
            string[] updateEntries = new string[leftEntryList.Count];
            leftEntryList.CopyTo(updateEntries);
            return updateEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadEntries(string entryFile)
        {
            List<string> entryList = new List<string> ();
            if (File.Exists(entryFile))
            {
                StreamReader entryReader = new StreamReader(entryFile);
                string line = "";
                while ((line = entryReader.ReadLine()) != null)
                {
                    entryList.Add(line.Substring(0, 4));
                }
                entryReader.Close();
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        public void OutputEntriesAfterSomeDay ()
        {
            StreamWriter entryWriter = new StreamWriter(@"D:\DbProjectData\PDB\newFatcatEntries.txt", true);
            string queryString = "Select Distinct PdbID From PdbEntry Where ReleaseFileDate >= '01.03.2010';";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (string.Compare(pdbId, "3t07") <= 0)
                {
                    continue;
                }
                if (IsEntryProtein(pdbId))
                {
                    entryWriter.WriteLine(pdbId);
                    entryWriter.Flush();
                }
            }
            entryWriter.Close();
        }

        private bool IsEntryProtein(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (protTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region download and parse crc-crc hh alignments
        public void UpdateCrcHhAlignments ()
        {
            try
            {
                entryAlignment.hhAlignments.UpdataParsingHhrFiles();
      //          entryAlignment.hhAlignments.UpdateMissingHhrFiles();
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update crc-crc HH alignments for error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine("Update crc-crc HH alignments for error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }

        /// <summary>
        /// for debug
        /// </summary>
        public void InsertInsertionFromLogFile ()
        {
            string logFile = "dbInsertErrorLog_crcHH.txt";
            StreamReader dataReader = new StreamReader(logFile);
            string line = "";
            string blockLines = "";
            bool blockBeg = true;
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("EXECUTE BLOCK AS BEGIN") > -1 && line.IndexOf ("END") > -1)
                {
                    blockLines = line.Replace("EXECUTE BLOCK AS BEGIN ", "");
                    blockLines = blockLines.Replace("END", "");
//                    dbInsert.InsertDataIntoDb(ProtCidSettings.alignmentDbConnection, blockLines);
                    string[] insertFields = blockLines.Split(';');
                    foreach (string insertLine in insertFields)
                    {
                        if (insertLine != "" && insertLine != " ")
                        {
                            try
                            {
                                dbInsert.InsertDataIntoDb(ProtCidSettings.alignmentDbConnection, insertLine + ";");
                            }
                            catch (Exception ex)
                            {
                                string errorMsg = ex.Message;
                            }
                        }                        
                    }
                    continue;
                }
                if (line.IndexOf ("EXECUTE BLOCK AS BEGIN") > -1)
                {
                    blockLines = "";
                    blockBeg = true;
                }
                else if (line.IndexOf ("END") > -1)
                {
                    blockBeg = false;
                    blockLines = line.Replace("EXECUTE BLOCK AS BEGIN ", "");
                    string[] insertFields = blockLines.Split(';');
                    foreach (string insertLine in insertFields)
                    {
                        if (insertLine != "" && insertLine != " ")
                        {
                            try
                            {
                                dbInsert.InsertDataIntoDb(ProtCidSettings.alignmentDbConnection, insertLine + ";");
                            }
                            catch (Exception ex)
                            {
                                string errorMsg = ex.Message;
                            }
                        }
                    }
    //                dbInsert.InsertDataIntoDb(ProtCidSettings.alignmentDbConnection, blockLines);                    
                }
                if (blockBeg)
                {
                    blockLines += line;
                }
            }
            dataReader.Close();
        }
        #endregion
    }
}
