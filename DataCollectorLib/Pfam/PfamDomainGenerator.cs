using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Data;
using System.Xml.Serialization;
using DbLib;
using XtalLib.Crystal;
using XtalLib.Settings;
using AuxFuncLib;

namespace DataCollectorLib.Pfam
{
    public class PfamDomainGenerator
    {
        #region member variables
      //  private string coordXmlDir = @"C:\DbProjectData\CoordXml";
        private string pfamDomainFileDir = "";
    //    private string tempDir = @"C:\pfam_temp";
        private StreamWriter logWriter = new StreamWriter ("PfamDomainFileGenLog.txt", true);
        private string pfamDomainTableName = "PdbPfam";
        private DataTable domainFileInfoTable = null;

        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        public bool isUpdate = false;
        private string fileChain = "A";
        #endregion

        
        /// <summary>
        /// 
        /// </summary>
        public PfamDomainGenerator()
        {
            if (DbBuilder.dbConnect.ConnectString == "")
            {
                if (AppSettings.dirSettings == null)
                {
                    AppSettings.LoadDirSettings();
                }
                DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;
                DbBuilder.dbConnect.ConnectToDatabase();
            }
            pfamDomainFileDir = Path.Combine(AppSettings.dirSettings.pfamPath, "DomainFiles");
        }
        /// <summary>
        /// 
        /// </summary>
        public void GeneratPfamDomainFiles()
        {
            isUpdate = true;
            InitializeTables(isUpdate);
            pfamDomainFileDir = Path.Combine(AppSettings.dirSettings.pfamPath, "DomainFiles");
            if (! Directory.Exists(pfamDomainFileDir))
            {
                Directory.CreateDirectory(pfamDomainFileDir);
            }
         //   logWriter = new StreamWriter("pfamDomainGeneratorLog.txt", true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString ());
            if (! Directory.Exists(AppSettings.tempDir))
            {
                Directory.CreateDirectory(AppSettings.tempDir);
            }

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.currentOperationLabel = "Generate Pfam Domain Files";
            AppSettings.progressInfo.progStrQueue.Enqueue("Generate Pfam domain files.");

            StreamWriter domainFileWriter = new StreamWriter(Path.Combine(pfamDomainFileDir, "pfamDomainLs.txt"), true);
            string queryString = "Select Distinct PdbId From " + pfamDomainTableName + " ;";
            DataTable pfamEntryTable = dbQuery.Query(queryString);

            AppSettings.progressInfo.totalOperationNum = pfamEntryTable.Rows.Count;
            AppSettings.progressInfo.totalStepNum = pfamEntryTable.Rows.Count;

            string pdbId = "";
            string fileNameLine = "";
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    string[] entryDomainFiles = GenerateEntryPfamDomainFiles(pdbId);
                    if (entryDomainFiles != null)
                    {
                        fileNameLine = "";
                        foreach (string domainFile in entryDomainFiles)
                        {
                            int fileIndex = domainFile.LastIndexOf("\\");
                            fileNameLine += (domainFile.Substring(fileIndex + 1, domainFile.Length - fileIndex - 1) + ",");
                        }
                        domainFileWriter.WriteLine(fileNameLine.TrimEnd(','));
                        domainFileWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    AppSettings.progressInfo.progStrQueue.Enqueue("Generate " + pdbId + " domain files errors: " + ex.Message);
                }
            }
            domainFileWriter.Close();
            logWriter.Close();
            DbBuilder.dbConnect.DisconnectFromDatabase();

            try
            {
                Directory.Delete(AppSettings.tempDir, true);
            }
            catch { }

            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        #region update interface
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePfamDomainFiles()
        {
            isUpdate = true;
            InitializeTables(isUpdate);

       //     string[] updateEntries = ReadUpdateEntries();
       //     string[] updateEntries = GetMissingEntries();
            string[] updateEntries = GetUpdatedWeakDomainEntries();

            if (!Directory.Exists(AppSettings.tempDir))
            {
                Directory.CreateDirectory(AppSettings.tempDir);
            }

            StreamWriter dataWriter = new StreamWriter(Path.Combine(pfamDomainFileDir, "newls-pfam.txt"));
            string fileNameLine = "";
            AppSettings.progressInfo.Reset();
            AppSettings.progressInfo.currentOperationLabel = "Generate PFAM domain files";
            AppSettings.progressInfo.totalOperationNum = updateEntries.Length ;
            AppSettings.progressInfo.totalStepNum = updateEntries.Length;
            AppSettings.progressInfo.progStrQueue.Enqueue("Generate PFAM domain files."); 
            
            // automatically copy the updated files into a new folder
            // so that it is easy to copy to other machines
            string updateFileDir = Path.Combine(pfamDomainFileDir, "updateFileDir");
            if (Directory.Exists(updateFileDir))
            {
                Directory.Delete(updateFileDir, true);
            }
            Directory.CreateDirectory(updateFileDir);
            string hashDir = "";
            string domainFileName = "";

            foreach (string entry in updateEntries)
            {
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = entry;
                // delete the obsolete domain files
                DeleteObsDomainFiles(entry);

                string[] entryDomainFiles = GenerateEntryPfamDomainFiles(entry);
                if (entryDomainFiles != null)
                {
                    fileNameLine = "";
                    hashDir = Path.Combine(updateFileDir, entry.Substring(1, 2));
                    if (! Directory.Exists(hashDir))
                    {
                        Directory.CreateDirectory(hashDir);
                    }
                    foreach (string domainFile in entryDomainFiles)
                    {
                        int fileIndex = domainFile.LastIndexOf("\\");
                        domainFileName = domainFile.Substring(fileIndex + 1, domainFile.Length - fileIndex - 1);
                        fileNameLine += (domainFileName + ",");
                        File.Copy(domainFile, Path.Combine(hashDir, domainFileName), true);
                    }
                    dataWriter.WriteLine(fileNameLine.TrimEnd(','));
                    dataWriter.Flush();
                }
            }
            dataWriter.Close();
            DbBuilder.dbConnect.DisconnectFromDatabase();

            try
            {
                Directory.Delete(AppSettings.tempDir, true);
            }
            catch { }

            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// delete the domain files for this input entry
        /// </summary>
        /// <param name="entry"></param>
        private void DeleteObsDomainFiles(string entry)
        {
            string hashDir = Path.Combine (pfamDomainFileDir, entry.Substring(1, 2));
            string[] domainFiles = Directory.GetFiles(hashDir, "entry*");
            foreach (string domainFile in domainFiles)
            {
                File.Delete(domainFile);
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public string[] GenerateEntryPfamDomainFiles(string pdbId)
        {
            DataTable entryPfamTable = GetEntryPfamDomainTable(pdbId);            
            if (entryPfamTable.Rows.Count == 0) // no pfam domain definition for the entry
            {
                return null;
            }
            ArrayList entryDomainInfoList = new ArrayList();
            ArrayList domainList = new ArrayList();
            foreach (DataRow domainRow in entryPfamTable.Rows)
            {
                string domainId = domainRow["DomainID"].ToString ();
                if (!domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
            }
            string domainFile = "";
            Hashtable entityChainInfoHash = new Hashtable ();
            foreach (string domainId in domainList)
            {
                domainFile = Path.Combine(pfamDomainFileDir, pdbId.Substring (1, 2) + "\\" + 
                    pdbId + domainId + ".pfam.gz");
                if (File.Exists(domainFile))
                {
                    continue;
                }
                DataRow[] domainRows = entryPfamTable.Select(string.Format ("DomainID = '{0}'", domainId), "HmmStart ASC");
                PfamDomainInfo domainInfo = new PfamDomainInfo();
                domainInfo.pdbId = pdbId;
                domainInfo.domainId = domainId;
                domainInfo.pfamAcc = domainRows[0]["Pfam_ACC"].ToString().TrimEnd();
                domainInfo.pfamId = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
                DomainSegmentInfo[] segmentInfos = new DomainSegmentInfo[domainRows.Length];
                int count = 0;
                foreach (DataRow domainRow in domainRows)
                {
                    DomainSegmentInfo segmentInfo = new DomainSegmentInfo();
                    segmentInfo.entityId = Convert.ToInt32(domainRow["EntityId"].ToString());
                    string[] chainInfos = GetAsymAuthorChainWithMaxCoord(pdbId, segmentInfo.entityId, ref entityChainInfoHash);
                    segmentInfo.asymChain = chainInfos[0];
                    segmentInfo.authChain = chainInfos[1];
                    segmentInfo.seqStart = Convert.ToInt32(domainRow["SeqStart"].ToString());
                    segmentInfo.seqEnd = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    segmentInfo.hmmStart = Convert.ToInt32(domainRow["HmmStart"].ToString());
                    segmentInfo.hmmEnd = Convert.ToInt32(domainRow["HmmEnd"].ToString());
                    segmentInfo.alignStart = Convert.ToInt32(domainRow["AlignStart"].ToString());
                    segmentInfo.alignEnd = Convert.ToInt32(domainRow["AlignEnd"].ToString());
                    segmentInfos[count] = segmentInfo;
                    count++;
                }
                domainInfo.segmentInfos = segmentInfos;
                entryDomainInfoList.Add(domainInfo);
            }
            PfamDomainInfo[] entryDomainInfos = new PfamDomainInfo[entryDomainInfoList.Count];
            entryDomainInfoList.CopyTo(entryDomainInfos);
            string[] domainFiles = GenerateEntryPfamDomainFiles(pdbId, entryDomainInfos);
            return domainFiles;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamDomainTable(string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID, SeqStart, SeqEnd, " + 
                "AlignStart, AlignEnd, HmmStart, HmmEnd, Pfam_ACC, Pfam_ID, DomainID " +
                " From {0} Where PdbID = '{1}';", pfamDomainTableName, pdbId);
            DataTable entryPfamTable = dbQuery.Query(queryString);
            queryString = string.Format("Select Distinct EntityID, SeqStart, SeqEnd, " +
                "AlignStart, AlignEnd, HmmStart, HmmEnd, Pfam_ACC, Pfam_ID, DomainID " +
                " From {0} Where PdbID = '{1}' AND IsUpdated = '1';", pfamDomainTableName + "weak", pdbId);
            DataTable updateWeakDomainTable = dbQuery.Query(queryString);
            ArrayList domainList = new ArrayList();
            foreach (DataRow domainRow in entryPfamTable.Rows)
            {
                long domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                if (!domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
            }
            foreach (long domainId in domainList)
            {
                DataRow[] updateDomainRows = updateWeakDomainTable.Select(string.Format ("domainId = {0}", domainId));
                if (updateDomainRows.Length > 0)
                {
                    DataRow[] orgDomainRows = entryPfamTable.Select(string.Format ("DomainID = {0}", domainId));
                    foreach (DataRow orgDomainRow in orgDomainRows)
                    {
                        entryPfamTable.Rows.Remove(orgDomainRow);
                    }
                    foreach (DataRow updateDomainRow in updateDomainRows)
                    {
                        DataRow newRow = entryPfamTable.NewRow();
                        newRow.ItemArray = updateDomainRow.ItemArray;
                        entryPfamTable.Rows.Add(newRow);
                    }
                }
            }
            return entryPfamTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetAsymAuthorChainWithMaxCoord(string pdbId, int entityId, ref Hashtable entityChainInfoHash)
        {
            if (entityChainInfoHash.ContainsKey(entityId))
            {
                return (string[]) entityChainInfoHash[entityId];
            }
            string queryString = string.Format("Select AsymID, AuthorChain, SequenceInCoord From AsymUnit " + 
                " Where PdbId = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable chainTable = dbQuery.Query(queryString);
            string bestAsymChain = "";
            string bestAuthChain = "";
            string bestSeqInCoord = "";
            int bestNumOfCoord = 0;
            int numOfCoord = 0;
            string seqInCoord = "";
            foreach (DataRow chainRow in chainTable.Rows)
            {
                seqInCoord = chainRow["SequenceInCoord"].ToString().TrimEnd();
                numOfCoord = GetNumOfCoordinates(seqInCoord);
                if (bestSeqInCoord == "")
                {
                    bestSeqInCoord = seqInCoord;
                    bestNumOfCoord = numOfCoord;
                    bestAsymChain = chainRow["AsymID"].ToString().TrimEnd();
                    bestAuthChain = chainRow["AuthorChain"].ToString().TrimEnd();
                }
                else
                {
                    if (bestNumOfCoord < numOfCoord)
                    {
                        bestSeqInCoord = seqInCoord;
                        bestNumOfCoord = numOfCoord;
                        bestAsymChain = chainRow["AsymID"].ToString().TrimEnd();
                        bestAuthChain = chainRow["AuthorChain"].ToString().TrimEnd();
                    }
                }
            }
            string[] bestChains = new string[2];
            bestChains[0] = bestAsymChain;
            bestChains[1] = bestAuthChain;
            entityChainInfoHash.Add(entityId, bestChains);
            return bestChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequenceInCoord"></param>
        /// <returns></returns>
        private int GetNumOfCoordinates(string sequenceInCoord)
        {
            int numOfCoord = 0;
            foreach (char ch in sequenceInCoord)
            {
                if (ch != '-')
                {
                    numOfCoord++;
                }
            }
            return numOfCoord;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyDomainList"></param>
        private string[] GenerateEntryPfamDomainFiles(string pdbId, PfamDomainInfo[] entryDomainInfos)
        {
            string gzXmlFile = Path.Combine(AppSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            if (! File.Exists(gzXmlFile))
            {
                logWriter.WriteLine("File not exist: " + gzXmlFile);
                
return null;
            }
            string coordXmlFile = ParseHelper.UnZipFile(gzXmlFile, AppSettings.tempDir);

            // read data from crystal xml file
            EntryCrystal entryInfo;
            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
                FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
                entryInfo = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
                xmlFileStream.Close();
            }
            catch (Exception ex)
            {
                logWriter.WriteLine("Read coordinate files errors: " + ex.Message);
                return null;
            }

            ArrayList domainFileList = new ArrayList ();
            string domainFile = "";
            foreach (PfamDomainInfo domainInfo in entryDomainInfos)
            {
                // for each domain
                for (int i = 0; i < domainInfo.segmentInfos.Length; i ++)
                {
                    foreach (ChainAtoms chain in entryInfo.atomCat.ChainAtomList)
                    {
                        if (chain.AsymChain == domainInfo.segmentInfos[i].asymChain)
                        {
                            AtomInfo[]  domainSegmentAtoms = GetDomainSegmentAtoms(domainInfo.segmentInfos[i], chain);
                            domainInfo.segmentInfos[i].atoms = domainSegmentAtoms;
                            break;
                        }
                    }
                    string[] segmentResidues = GetDomainSegmentThreeLetterResidues
                        (domainInfo.segmentInfos[i], entryInfo.entityCat.EntityInfoList);
                    domainInfo.segmentInfos[i].threeLetterResidues = segmentResidues;
                }
                domainFile = WritePfamDomainToFile (pdbId, domainInfo);
                if (domainFile != "")
                {
                    domainFileList.Add(domainFile);
                }
            }
            File.Delete(coordXmlFile);

            string[] domainFiles = new string[domainFileList.Count];
            domainFileList.CopyTo(domainFiles);
            return domainFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInfo"></param>
        /// <param name="chainInfo"></param>
        /// <returns></returns>
        private string WritePfamDomainToFile (string pdbId, PfamDomainInfo domainInfo)
        {
            string remarkString = "HEADER   " + pdbId + "\r\n";
            remarkString += "REMARK   1   PFAM    " + domainInfo.domainId + "\r\n";
            remarkString += ("REMARK   2   PFAM ACC: " + domainInfo.pfamAcc + ", " +
               " PFAM ID: " + domainInfo.pfamId + "\r\n");

            AtomInfo[] domainAtoms = ChangeDomainAtomsSeqAtomIds(domainInfo);
            if (domainAtoms.Length == 0)
            {
                logWriter.WriteLine(pdbId + domainInfo.domainId + " no coordinates.");
                logWriter.Flush();
                return "";
            }

            string domainInfoRemark = FormatDomainSegmentInfoRemark(domainInfo.segmentInfos);
            remarkString += domainInfoRemark;

            string seqResRecord = FormatDomainSeqResRecord(domainInfo.segmentInfos);
            remarkString += (seqResRecord + "\r\n");

            string hashDir = pdbId.Substring(1, 2);
            if (! Directory.Exists (Path.Combine(pfamDomainFileDir, hashDir)))
            {
                Directory.CreateDirectory(Path.Combine(pfamDomainFileDir, hashDir));
            }

            InsertDataIntoDbTable(domainInfo);

            string fileName = Path.Combine(pfamDomainFileDir, hashDir + "\\" + pdbId + domainInfo.domainId + ".pfam");
            WriteAtomsToFile(fileName, fileChain, remarkString, domainAtoms);
            ParseHelper.ZipPdbFile(fileName);
            return fileName + ".gz";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="segmentInfos"></param>
        /// <returns></returns>
        private string FormatDomainSegmentInfoRemark(DomainSegmentInfo[] segmentInfos)
        {
            string remark = "";
            foreach (DomainSegmentInfo segmentInfo in segmentInfos)
            {
                remark += "REMARK   3   Entity ID: " + segmentInfo.entityId.ToString() +
                    ", Asymmetric Chain: " + segmentInfo.asymChain +
                    ", Author Chain: " + segmentInfo.authChain +
                    ", Sequence start: " + segmentInfo.seqStart.ToString() +
                    ", Sequence end: " + segmentInfo.seqEnd.ToString() +
                    ", Align start: " + segmentInfo.alignStart.ToString() +
                    ", Align end: " + segmentInfo.alignEnd.ToString() +
                    ", Hmm start: " + segmentInfo.hmmStart.ToString() +
                    ", Hmm end: " + segmentInfo.hmmEnd.ToString() + 
                    ", File start: " + segmentInfo.fileStart.ToString () + 
                    ", File end: " + segmentInfo.fileEnd.ToString () +
                    "\r\n";
            }
            return remark;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="segmentInfos"></param>
        /// <returns></returns>
        private string FormatDomainSeqResRecord(DomainSegmentInfo[] segmentInfos)
        {
            ArrayList residueList = new ArrayList();
            foreach (DomainSegmentInfo segInfo in segmentInfos)
            {
                residueList.AddRange(segInfo.threeLetterResidues);
            }
            string[] domainResidues = new string[residueList.Count];
            residueList.CopyTo(domainResidues);
            string seqResRecord = ParseHelper.FormatChainSeqResRecords(fileChain, domainResidues);
            return seqResRecord;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainEntityInfo"></param>
        /// <param name="chainInfo"></param>
        /// <returns></returns>
        private AtomInfo[] GetDomainSegmentAtoms(DomainSegmentInfo domainEntityInfo, ChainAtoms chainInfo)
        {
            ArrayList domainAtomList = new ArrayList();
            int seqId = -1;
            foreach (AtomInfo atom in chainInfo.CartnAtoms)
            {
                seqId = Convert.ToInt16(atom.seqId);
                if (seqId >= domainEntityInfo.seqStart && seqId <= domainEntityInfo.seqEnd)
                {
               //     AddAtomToList(atom, domainAtomList);
                    domainAtomList.Add(atom);
                }
            }
            AtomInfo[] domainAtoms = new AtomInfo[domainAtomList.Count];
            domainAtomList.CopyTo(domainAtoms);
            return domainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSegInfo"></param>
        /// <param name="entryEntityInfos"></param>
        /// <returns></returns>
        private string[] GetDomainSegmentThreeLetterResidues(DomainSegmentInfo domainSegInfo, EntityInfo[] entryEntityInfos)
        {
            foreach (EntityInfo entityInfo in entryEntityInfos)
            {
                if (entityInfo.entityId == domainSegInfo.entityId)
                {
                    string[] residues = entityInfo.threeLetterSeq.TrimEnd(' ').Split(' ');
                    string[] segmentResidues = new string[domainSegInfo.seqEnd - domainSegInfo.seqStart + 1];
                    try
                    {
                        Array.Copy(residues, domainSegInfo.seqStart - 1, segmentResidues, 0,
                            domainSegInfo.seqEnd - domainSegInfo.seqStart + 1);
                        return segmentResidues;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Get Segment three letter sequences error: " + ex.Message);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInfo"></param>
        private AtomInfo[] ChangeDomainAtomsSeqAtomIds(PfamDomainInfo domainInfo)
        {
            int atomSeqNo = 1;
            int residueSeqNo = 1;
            int atomSeqId = 0;
            int preAtomSeqId = 0;
            ArrayList domainAtomList = new ArrayList();
            foreach (DomainSegmentInfo segInfo in domainInfo.segmentInfos)
            {
                atomSeqId = 0;
                preAtomSeqId = 0;

                segInfo.fileStart = residueSeqNo;
                segInfo.fileEnd = residueSeqNo + segInfo.seqEnd - segInfo.seqStart;
                for (int i = 0; i < segInfo.atoms.Length; i ++)
                {
                    segInfo.atoms[i].atomId = atomSeqNo;
                    atomSeqNo++;
                    if (atomSeqNo > 99999) // the maximum atom seq id is 5-digit
                    {
                        atomSeqNo = 1;
                    }
                    atomSeqId = Convert.ToInt32 (segInfo.atoms[i].seqId);
                    if (preAtomSeqId == 0)
                    {
                        preAtomSeqId = atomSeqId;
                    }
                    if (preAtomSeqId == atomSeqId)
                    {
                        segInfo.atoms[i].seqId = residueSeqNo.ToString();
                    }
                    else
                    {
                        residueSeqNo = residueSeqNo + atomSeqId - preAtomSeqId;
                        segInfo.atoms[i].seqId = residueSeqNo.ToString ();
                        preAtomSeqId = atomSeqId;
                    }
                }
                domainAtomList.AddRange(segInfo.atoms);
                residueSeqNo = segInfo.fileEnd + 1;
            }
            AtomInfo[] domainAtoms = new AtomInfo[domainAtomList.Count];
            domainAtomList.CopyTo(domainAtoms);
            return domainAtoms;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="domainAtomList"></param>
        private void AddAtomToList(AtomInfo atom, ArrayList domainAtomList)
        {
            int i = -1;
            for (i = domainAtomList.Count - 1; i >= 0; i--)
            {
                if (atom.atomId > ((AtomInfo)domainAtomList[i]).atomId)
                {
                    break;
                }
            }
            if (i == domainAtomList.Count - 1)
            {
                domainAtomList.Add(atom);
            }
            else
            {
                domainAtomList.Insert(i + 1, atom);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="remarkString"></param>
        /// <param name="atoms"></param>
        private void WriteAtomsToFile(string fileName, string fileChain, string remarkString, AtomInfo[] atoms)
        {
            StreamWriter dataWriter = new StreamWriter(fileName);
            dataWriter.WriteLine(remarkString);
            string atomLine = "";
            AtomInfo lastAtom = null;
            foreach (AtomInfo atom in atoms)
            {
                atomLine = FormatAtomLine(fileChain, atom);
                dataWriter.WriteLine(atomLine);
                lastAtom = atom;
            }
            string line = "TER   ";
            int atomId = lastAtom.atomId + 1;
            line += atomId.ToString().PadLeft(5, ' ');
            line += "      ";
            line += lastAtom.residue.PadLeft(3, ' ');
            line += " ";
            line += fileChain;
            line += lastAtom.seqId.PadLeft(4, ' ');
            dataWriter.WriteLine(line);
            dataWriter.WriteLine("END");
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <returns></returns>
        private string FormatAtomLine(string chainId, AtomInfo atom)
        {
            string line = "ATOM  ";
            line += atom.atomId.ToString ().PadLeft(5, ' ');
            line += " ";
            string atomName = atom.atomName;
            if (atomName != "" && atomName.Length < 4)
            {
                atomName = " " + atomName;
            }
            line += atomName.PadRight(4, ' ');
            line += " ";
            line += atom.residue.PadLeft(3, ' ');
            line += " ";
            line += chainId;
            line += atom.seqId.PadLeft(4, ' ');
            line += "    ";
            line += FormatDoubleString(atom.xyz.X, 4, 3);
            line += FormatDoubleString(atom.xyz.Y, 4, 3);
            line += FormatDoubleString(atom.xyz.Z, 4, 3);
            line += "  1.00";
            line += "  0.00";
            line += "           ";
            line += atom.atomType;

            return line;
        }

        /// <summary>
        /// format a double into a string 
        /// (8.3) (1234.123)
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private string FormatDoubleString(double val, int numPre, int numPost)
        {
            string valStr = val.ToString();
            int dotIndex = valStr.IndexOf(".");
            if (dotIndex == -1)
            {
                // return the int part, plus ".0  "
                valStr = valStr.PadLeft(numPre, ' ');
                valStr += ".";
                int i = 0;
                while (i < numPost)
                {
                    valStr += "0";
                    i++;
                }
                return valStr;
            }
            string intPartStr = valStr.Substring(0, dotIndex).PadLeft(numPre, ' ');
            int subStrLen = valStr.Length - dotIndex - 1;
            if (subStrLen > numPost)
            {
                subStrLen = numPost;
            }
            string fractStr = valStr.Substring(dotIndex + 1, subStrLen).PadRight(3, '0');
            return intPartStr + "." + fractStr;
        }

        #region read update files 
        public void ReadUpdateFiles()
        {
            string[] hashDirs = Directory.GetDirectories(pfamDomainFileDir);
            string updateFileDir = Path.Combine(pfamDomainFileDir, "updateFileDir");
            if (!Directory.Exists(updateFileDir))
            {
                Directory.CreateDirectory(updateFileDir);
            }

            foreach (string hashDir in hashDirs)
            {
                DateTime writenTime = Directory.GetLastWriteTime(hashDir);
                if (writenTime.ToShortDateString () == DateTime.Today.ToShortDateString ())
                {
                    string updateHashDir = hashDir.Replace(pfamDomainFileDir, updateFileDir);
                    if (!Directory.Exists(updateHashDir))
                    {
                        Directory.CreateDirectory(updateHashDir);
                    }
                    string[] files = Directory.GetFiles(hashDir);
                    foreach (string file in files)
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime.ToShortDateString () == DateTime.Today.ToShortDateString ())
                        {
                            File.Copy(file, file.Replace(pfamDomainFileDir, updateFileDir));
                        }
                    }
                }
            }
        }
        #endregion

        #region read update entries
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdatedWeakDomainEntries()
        {
            string queryString = "Select Distinct PdbID From PdbPfamWeak;";
            DataTable weakDomainEntryTable = dbQuery.Query(queryString);
            string[] updateEntries = new string[weakDomainEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in weakDomainEntryTable.Rows)
            {
                updateEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return updateEntries;
        }
        private string[] GetMissingEntries()
        {
            string queryString = "Select Distinct PdbID From PfamPdb;";
            DataTable entryTable = dbQuery.Query(queryString);
            ArrayList missingEntryList = new ArrayList();
            string pdbId = "";
            StreamWriter dataWriter = new StreamWriter("missingPfamDomainInterfaceFileEntries.txt");
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                if (! AreEntryDomainFilesExist (pdbId))
                {
                   missingEntryList.Add (pdbId);
                   dataWriter.WriteLine(pdbId);
                }
            }
           
            dataWriter.Close();
            string[] missingEntries = new string[missingEntryList.Count];
            missingEntryList.CopyTo(missingEntries);
            return missingEntries;
        }

        private bool AreEntryDomainFilesExist(string pdbId)
        {
            string fileDir = Path.Combine(pfamDomainFileDir, "DomainFiles");
            fileDir = Path.Combine(fileDir, pdbId.Substring(1, 2));
            string[] domainFiles = Directory.GetFiles (fileDir, pdbId + "*");
            if (domainFiles.Length > 0)
            {
                return true;
            }
            return false;
        }

        private string[] ReadUpdateEntries()
        {
            string line = "";
            ArrayList updateEntryList = new ArrayList();
            StreamReader dataReader = new StreamReader(Path.Combine (AppSettings.dirSettings.xmlPath, "newls-pdb.txt"));
          //  StreamReader dataReader = new StreamReader("EntryNotInPfam.txt");
           
            while ((line = dataReader.ReadLine()) != null)
            {
                updateEntryList.Add(line.Substring(0, 4));
            }
     /*       StreamReader dataReader = new StreamReader("EntryNotInPfam.txt");
            string entry = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("already exists") > -1)
                {
                    continue;
                }
                string[] fields = line.Split(' ');
                entry = fields[1].Substring(0, 4);
                if (updateEntryList.Contains (entry))
                {
                    continue;
                }
                if (IsEntryExist(entry))
                {
                    updateEntryList.Insert(0, entry);
                }
            }*/
            dataReader.Close();
            StreamWriter dataWriter = new StreamWriter("UpdateEntries.txt");
            foreach (string pdbId in updateEntryList)
            {
                dataWriter.WriteLine(pdbId);
            }
            dataWriter.Close();
            string[] updateEntries = new string[updateEntryList.Count ];
            updateEntryList.CopyTo(updateEntries);
            return updateEntries;
        }

        private bool IsEntryExist(string pdbId)
        {
            string queryString = string.Format("Select * From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable entryTable = dbQuery.Query(queryString);
            if (entryTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region initialize tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void InitializeTables (bool isUpdate)
        {
            string tableName = "PdbPfamDomainFileInfo";
            string[] tableCols = {"PdbID", "DomainID", "EntityID", "AsymChain", "SeqStart", "SeqEnd", "FileStart", "FileEnd"};
            domainFileInfoTable = new DataTable(tableName);
            foreach (string col in tableCols)
            {
                domainFileInfoTable.Columns.Add(new DataColumn (col));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "CREATE TABLE " + tableName + " ( " +
                    "PdbID CHAR(4) NOT NULL, " +
                    "DomainID BIGINT NOT NULL, " +
                    "EntityID INTEGER NOT NULL, " +
                    "AsymChain CHAR(3) NOT NULL, " +
                    "SeqStart INTEGER NOT NULL, " +
                    "SeqEnd INTEGER NOT NULL, " +
                    "FileStart INTEGER NOT NULL, " +
                    "FileEnd INTEGER NOT NULL );";
                dbCreate.CreateTableFromString(createTableString, tableName);
                string createIndexString = "CREATE INDEX " + tableName + "_idx1 ON " + tableName + "(PdbID);";
                dbCreate.CreateIndex(createIndexString, tableName);
                createIndexString = "CREATE INDEX " + tableName + "_idx2 ON " + tableName + "(DomainID);";
                dbCreate.CreateIndex(createIndexString, tableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInfo"></param>
        private void InsertDataIntoDbTable(PfamDomainInfo domainInfo)
        {
            if (isUpdate)
            {
                string deleteString = string.Format("Delete From {0}  Where PdbID = '{1}' AND DomainID = {2};", 
                    domainFileInfoTable.TableName, domainInfo.pdbId, domainInfo.domainId);
                dbQuery.Query(deleteString);
            }
            foreach (DomainSegmentInfo segInfo in domainInfo.segmentInfos)
            {
                DataRow fileInfoRow = domainFileInfoTable.NewRow();
                fileInfoRow["PdbId"] = domainInfo.pdbId;
                fileInfoRow["DomainId"] = domainInfo.domainId;
                fileInfoRow["EntityID"] = segInfo.entityId;
                fileInfoRow["AsymChain"] = segInfo.asymChain;
                fileInfoRow["SeqStart"] = segInfo.seqStart;
                fileInfoRow["SeqEnd"] = segInfo.seqEnd;
                fileInfoRow["FileStart"] = segInfo.fileStart;
                fileInfoRow["FileEnd"] = segInfo.fileEnd;
                domainFileInfoTable.Rows.Add(fileInfoRow);
            }
            dbInsert.InsertDataIntoDBtables(domainFileInfoTable);
            domainFileInfoTable.Clear();
        }
        #endregion
    }
}
