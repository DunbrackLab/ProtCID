using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Threading.Tasks;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;

namespace BugFixingLib
{
    public class MissingData
    {
        public MissingData()
        {
            Initialize();
        }

        #region initialize
        private void Initialize()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
                //          ProtCidSettings.protcidDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=X:\\Firebird\\Pfam30\\protcid.fdb");
            }
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
            }
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath);
            }

            ProtCidSettings.buCompConnection = new DbConnect();
            ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.baInterfaceDbPath;

            ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            ProtCidSettings.alignmentQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);
        }
        #endregion

        #region missing pfam assignments
        private Dictionary<string, int> pfamModelLengthDict = new Dictionary<string, int>();
        public void PrintEntriesMissingStrongHits()
        {
            StreamWriter missingPfamEntryWriter = new StreamWriter("MissingPfamEntries.txt");
            string queryString = "Select Distinct PdbID From PdbPfam;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                string[] missingEntities = GetEntryMissingPfamDomains(pdbId);
                foreach (string missingEntity in missingEntities)
                {
                    missingPfamEntryWriter.WriteLine(missingEntity);
                }
                if (missingEntities.Length > 0)
                {
                    missingPfamEntryWriter.Flush();
                }
            }
            missingPfamEntryWriter.Close();
        }

        private string[] GetEntryMissingPfamDomains(string pdbId)
        {
            string queryString = "";
            List<string> missingEntityList = new List<string>();
            Dictionary<int, string> entityCrcDict = GetEntryCrc(pdbId);
            foreach (int entityId in entityCrcDict.Keys)
            {
                queryString = string.Format("Select Crc, HmmModel, I_Evalue, HmmFrom, HmmTo, AliFrom, AliTo, EnvFrom, EnvTo From PdbPfamHmmAlignments " +
                    " Where Crc = '{0}' AND I_Evalue < 0.00001 ;", entityCrcDict[entityId]);
                DataTable pfamHitTable = ProtCidSettings.alignmentQuery.Query(queryString);

                queryString = string.Format("Select PdbId, EntityID, Pfam_ID, Evalue, AlignStart, AlignEnd, HmmStart, HmmEnd From PdbPfam " +
                    " Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
                DataTable pfamAssignTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                if (IsPfamHitMissing(pfamHitTable, pfamAssignTable))
                {
                    missingEntityList.Add(pdbId + entityId.ToString() + " " + entityCrcDict[entityId]);
                }
            }
            return missingEntityList.ToArray();
        }
        private bool IsPfamHitMissing(DataTable pfamHitTable, DataTable pfamAssignTable)
        {
            bool hitExist = false;
            foreach (DataRow pfamHitRow in pfamHitTable.Rows)
            {
                if (!IsHitStrong(pfamHitRow))
                {
                    continue;
                }
                hitExist = false;
                int[] hitSeqRanges = new int[2];
                hitSeqRanges[0] = Convert.ToInt32(pfamHitRow["AliFrom"].ToString());
                hitSeqRanges[1] = Convert.ToInt32(pfamHitRow["AliTo"].ToString());
                int[] hitHmmRanges = new int[2];
                hitHmmRanges[0] = Convert.ToInt32(pfamHitRow["HmmFrom"].ToString());
                hitHmmRanges[1] = Convert.ToInt32(pfamHitRow["HmmTo"].ToString());
                foreach (DataRow assignRow in pfamAssignTable.Rows)
                {
                    //          if (pfamHitRow["HmmModel"].ToString ().TrimEnd () == assignRow["Pfam_ID"].ToString ().TrimEnd ())
                    //           {
                    int[] assignSeqRanges = new int[2];
                    assignSeqRanges[0] = Convert.ToInt32(assignRow["AlignStart"].ToString());
                    assignSeqRanges[1] = Convert.ToInt32(assignRow["AlignEnd"].ToString());
                    int[] assignHmmRanges = new int[2];
                    assignHmmRanges[0] = Convert.ToInt32(assignRow["HmmStart"].ToString());
                    assignHmmRanges[1] = Convert.ToInt32(assignRow["HmmEnd"].ToString());

                    //             if (AreRangesOverlap(hitSeqRanges, assignSeqRanges) && AreRangesOverlap(hitHmmRanges, assignHmmRanges))
                    if (IsHitRangesOverlap(hitSeqRanges, assignSeqRanges))
                    {
                        hitExist = true;
                        break;
                    }
                    //        }
                }
                if (!hitExist)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsHitRangesOverlap(int[] hitRange, int[] assignRange)
        {
            int maxStart = Math.Max(hitRange[0], assignRange[0]);
            int minEnd = Math.Min(hitRange[1], assignRange[1]);
            double coverage1 = (double)(minEnd - maxStart + 1) / (double)(hitRange[1] - hitRange[0] + 1);
            //         double coverage2 = (double)(minEnd - maxStart + 1) / (double)(assignRange[1] - assignRange[0] + 1);
            if (coverage1 > 0.25)
            {
                return true;
            }
            return false;
        }
        private Dictionary<int, string> GetEntryCrc(string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID, Crc From PdbCrcMap Where PdbID = '{0}';", pdbId);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<int, string> entityCrcDict = new Dictionary<int, string>();
            int entityId = 0;
            foreach (DataRow crcRow in crcTable.Rows)
            {
                entityId = Convert.ToInt32(crcRow["EntityID"].ToString());
                entityCrcDict.Add(entityId, crcRow["Crc"].ToString().TrimEnd());
            }
            return entityCrcDict;
        }

        private int GetModelLength(string pfamId)
        {
            if (pfamModelLengthDict.ContainsKey(pfamId))
            {
                return pfamModelLengthDict[pfamId];
            }
            else
            {
                string queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_ID = '{0}';", pfamId);
                DataTable hmmLengthTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (hmmLengthTable.Rows.Count > 0)
                {
                    return Convert.ToInt32(hmmLengthTable.Rows[0]["ModelLength"].ToString());
                }
            }
            return -1;
        }

        private bool IsHitStrong(DataRow hitRow)
        {
            string pfamId = hitRow["HmmModel"].ToString().TrimEnd();
            int modelLength = GetModelLength(pfamId);
            int hitHmmLength = Convert.ToInt32(hitRow["HmmTo"].ToString()) - Convert.ToInt32(hitRow["HmmFrom"].ToString()) + 1;
            double coverage = (double)hitHmmLength / (double)modelLength;
            if (coverage > 0.75)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region missing crc hh alignments
        public void GetMissingHHCrcPair()
        {
            string localCrcPairLsFile = @"D:\Pfam\HH\crcpairs_local.txt";
            string linuxCrcPairLsFile = @"D:\Pfam\HH\hhglobal_crcpairs.txt";
            Dictionary<string, List<string>> mysqlCrcPairListDict = ReadMySqlCrcPairListDict(linuxCrcPairLsFile);
            Dictionary<string, List<string>> fbCrcPairListDict = ReadFbCrcPairListDict(localCrcPairLsFile);
            StreamWriter missingCrcPairWriter = new StreamWriter(@"D:\Pfam\HH\missingCrcPairs.txt");
            StreamWriter newCrcPairWriter = new StreamWriter(@"D:\Pfam\HH\newCrcPairs.txt");
            foreach (string query in mysqlCrcPairListDict.Keys)
            {
                if (fbCrcPairListDict.ContainsKey(query))
                {
                    fbCrcPairListDict[query].Sort();
                    foreach (string lsCrc in mysqlCrcPairListDict[query])
                    {
                        if (fbCrcPairListDict[query].BinarySearch(lsCrc) < 0)
                        {
                            missingCrcPairWriter.Write(query + "\t" + lsCrc + "\n");
                        }
                    }
                }
                else
                {
                    foreach (string lsCrc in mysqlCrcPairListDict[query])
                    {
                        newCrcPairWriter.Write(query + "\t" + lsCrc + "\n");
                    }
                }
            }
            missingCrcPairWriter.Close();
            newCrcPairWriter.Close();
        }

        public void GetMissingHHCrcPairsInFbDb()
        {
            Dictionary<string, List<string>> missingCrcPairListDict = ReadMySqlCrcPairListDict(@"D:\Pfam\HH\neededCrcPairs.txt");
            //        Dictionary<string, List<string>> newCrcPairListDict = ReadMySqlCrcPairListDict(@"D:\Pfam\HH\newCrcPairs.txt");
            StreamWriter crcPairWriter = new StreamWriter(@"D:\Pfam\HH\neededRepCrcList.txt");
            Dictionary<string, bool> crcInDbDict = new Dictionary<string, bool>();
            foreach (string keyCrc in missingCrcPairListDict.Keys)
            {
                crcPairWriter.Write(keyCrc + "," + ParseHelper.FormatStringFieldsToString(missingCrcPairListDict[keyCrc].ToArray()) + "\n");
                /*         if (!IsCrcInDb(keyCrc, crcInDbDict))
                         {
                             continue;
                         }
                         foreach (string lsCrc in missingCrcPairListDict[keyCrc])
                         {
                             if (IsCrcInDb(lsCrc, crcInDbDict))
                             {
                                 if (!IsHHAlignmentExist(keyCrc, lsCrc))
                                 {
                                     crcPairWriter.Write(keyCrc + "\t" + lsCrc + "\n");
                                 }
                             }
                         }*/
            }
            crcPairWriter.Close();
        }


        double doubleLimit = 1E-307;
        public void InsertCrcHHAlignments()
        {
            DbInsert hhInsert = new DbInsert(ProtCidSettings.alignmentDbConnection);
            string queryString = "Select First 1 * From PdbCrcHHAlignments;";
            DataTable hhalignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            hhalignTable.Clear();
            hhalignTable.TableName = "PdbCrcHHAlignments";
            int numOfLines = 0;
            string[] hhAlignFiles = Directory.GetFiles(@"D:\Pfam\HH\pdb\missingHhCrcAlign", "*.txt");
            foreach (string hhAlignFile in hhAlignFiles)
            {
                StreamReader alignReader = new StreamReader(hhAlignFile);
                string line = "";
                double pvalue = 0;
                while ((line = alignReader.ReadLine()) != null)
                {
                    numOfLines++;
                    string[] fields = line.Split('\t');
                    string[] items = new string[hhalignTable.Columns.Count];
                    Array.Copy(fields, 0, items, 0, items.Length);
                    DataRow hhAlignRow = hhalignTable.NewRow();
                    hhAlignRow.ItemArray = items;
                    pvalue = Convert.ToDouble(hhAlignRow["Evalue"].ToString());
                    if (pvalue < doubleLimit)
                    {
                        hhAlignRow["Evalue"] = 0;
                    }
                    pvalue = Convert.ToDouble(hhAlignRow["Pvalue"].ToString());
                    if (pvalue < doubleLimit)
                    {
                        hhAlignRow["Pvalue"] = 0;
                    }
                    hhalignTable.Rows.Add(hhAlignRow);

                    hhInsert.InsertDataIntoDBtables(hhalignTable);
                    hhalignTable.Clear();
                }
                alignReader.Close();
            }
        }

        public void InsertLogLines()
        {
            DbInsert hhInsert = new DbInsert(ProtCidSettings.alignmentDbConnection);
            StreamReader dataReader = new StreamReader("dbInsertErrorLog0.txt");
            string line = "";
            double pvalue = 0;
            double evalue = 0;
            string insertLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("INSERT ") > -1)
                {
                    insertLine = "";
                    string[] fields = line.Split();

                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (i == 26)
                        {
                            pvalue = Convert.ToDouble(fields[26].Trim("',".ToCharArray()));
                            if (pvalue < doubleLimit)
                            {
                                insertLine += "0, ";
                                continue;
                            }
                        }
                        if (i == 33)
                        {
                            evalue = Convert.ToDouble(fields[33].Trim("',".ToCharArray()));
                            if (evalue < doubleLimit)
                            {
                                insertLine += "0, ";
                                continue;
                            }
                        }
                        insertLine += (fields[i] + " ");
                    }
                    insertLine.TrimEnd(' ');
                    hhInsert.InsertDataIntoDb(insertLine);
                }
            }
            dataReader.Close();
        }

        private bool IsHHAlignmentExist(string crc1, string crc2)
        {
            string queryString = string.Format("Select first 1  query, hit From PdbCrcHhAlignments Where (query = '{0}' AND hit = '{1}') OR (query = '{1}' AND hit = '{0}');", crc1, crc2);
            DataTable hhAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            if (hhAlignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        private bool IsCrcInDb(string crc, Dictionary<string, bool> crcInDbDict)
        {
            if (crcInDbDict.ContainsKey(crc))
            {
                return crcInDbDict[crc];
            }
            bool inDb = false;
            string queryString = string.Format("Select First 2 * From PdbCrcMap Where crc = '{0}';", crc);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (crcTable.Rows.Count > 0)
            {
                inDb = true;
            }
            crcInDbDict.Add(crc, inDb);
            return inDb;
        }

        private Dictionary<string, List<string>> ReadMySqlCrcPairListDict(string linuxCrcPairLsFile)
        {
            Dictionary<string, List<string>> CrcPairListDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader(linuxCrcPairLsFile);
            string line = "";
            int numOfLines = 0;
            string keyCrc = "";
            string valueCrc = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                numOfLines++;
                string[] fields = line.Split('\t');
                if (numOfLines <= 117690 + 18673)
                {
                    continue;
                }
                if (string.Compare(fields[0], fields[1]) > 0)
                {
                    keyCrc = fields[1];
                    valueCrc = fields[0];
                }
                else
                {
                    keyCrc = fields[0];
                    valueCrc = fields[1];
                }
                if (CrcPairListDict.ContainsKey(keyCrc))
                {
                    CrcPairListDict[keyCrc].Add(valueCrc);
                }
                else
                {
                    List<string> crcList = new List<string>();
                    crcList.Add(valueCrc);
                    CrcPairListDict.Add(keyCrc, crcList);
                }
            }
            dataReader.Close();
            return CrcPairListDict;
        }

        private Dictionary<string, List<string>> ReadFbCrcPairListDict(string firebirdCrcPairLsFile)
        {
            Dictionary<string, List<string>> CrcPairListDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader(firebirdCrcPairLsFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.Substring(0, 1) == "=")
                {
                    continue;
                }
                if (line.Substring(0, 5) == "QUERY")
                {
                    continue;
                }
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                if (CrcPairListDict.ContainsKey(fields[0]))
                {
                    CrcPairListDict[fields[0]].Add(fields[1]);
                }
                else
                {
                    List<string> crcList = new List<string>();
                    crcList.Add(fields[1]);
                    CrcPairListDict.Add(fields[0], crcList);
                }
            }
            dataReader.Close();
            return CrcPairListDict;
        }
        #endregion

        #region missing chain interfaces
        public void GetEntriesMissingBAchainNowAdded()
        {
            string[] EntriesWithMissingChainInBA = GetEntriesMissingChainInterfaces();
            List<string> entryListCrystBAComp = new List<string>();
            StreamWriter entryWriter = new StreamWriter("EntriesNeededCrystBuComp.txt");
            string queryString = "";
            foreach (string pdbId in EntriesWithMissingChainInBA)
            {
                queryString = string.Format("Select * From PdbBuInterfaces Where PdbID = '{0}';", pdbId);
                DataTable buInterfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
                if (buInterfaceTable.Rows.Count > 0)
                {
                    entryListCrystBAComp.Add(pdbId);
                    entryWriter.WriteLine(pdbId);
                }
            }
            entryWriter.Close();
        }

        public string[] GetEntriesMissingChainInterfaces()
        {
            List<string> missingEntryList = new List<string>();
            if (File.Exists("EntriesMissingChainInterfaces.txt"))
            {
                StreamReader dataReader = new StreamReader("EntriesMissingChainInterfaces.txt");
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    missingEntryList.Add(line);
                }
                dataReader.Close();
            }
            {
                string queryString = "Select Distinct PdbId From PdbBuStat Where oligomeric_details <> 'monomeric'";
                DataTable buEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                queryString = "Select Distinct PdbID From PdbBuInterfaces;";
                DataTable buInterfaceEntryTable = ProtCidSettings.buCompQuery.Query(queryString);

                StreamWriter entryWriter = new StreamWriter("EntriesMissingChainInterfaces.txt");
                string pdbId = "";
                foreach (DataRow entryRow in buEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    DataRow[] interfaceRows = buInterfaceEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    if (interfaceRows.Length > 0)
                    {
                        continue;
                    }
                    missingEntryList.Add(pdbId);
                    entryWriter.WriteLine(pdbId);
                }
                entryWriter.Close();
            }
            return missingEntryList.ToArray();
        }

        public void DeleteEntriesWithLigandsNotInPfam()
        {
            string queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable ligandPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            string pdbId = "";
            StreamWriter delPfamEntryWriter = new StreamWriter("DeletedEntriesInPfamLigands.txt");
            foreach (DataRow pfamRow in ligandPfamTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
                DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                queryString = string.Format("Select Distinct PdbID From PfamLigands Where PfamID = '{0}';", pfamId);
                DataTable ligandEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow entryRow in ligandEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    DataRow[] pfamEntryRows = pfamEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    if (pfamEntryRows.Length > 0)
                    {
                        continue;
                    }
                    delPfamEntryWriter.WriteLine(pfamId + " " + pdbId);
                }
            }
            delPfamEntryWriter.Close();
        }

        /// <summary>
        /// after delete, should update two summary tables: PFAMLIGANDSPAIRSUMINFO, PFAMLIGANDSSUMINFO 
        /// </summary>
        public void DeletePfamLigandsData()
        {
            StreamReader dataReader = new StreamReader("DeletedEntriesInPfamLigands.txt");
            string line = "";
            string[] tableNames = { "PFAMLIGANDCLUSTERSHMM", "PFAMLIGANDCOMHMMS", "PFAMLIGANDS" };
            string deleteString = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                deleteString = string.Format("Delete From PfamLigands Where PfamID = '{0}' AND PdbID = '{1}';", fields[0], fields[1]);
                ProtCidSettings.protcidQuery.Query(deleteString);
                deleteString = string.Format("Delete From PFAMLIGANDCLUSTERSHMM Where PfamID = '{0}' AND PdbID = '{1}';", fields[0], fields[1]);
                ProtCidSettings.protcidQuery.Query(deleteString);
                deleteString = string.Format("Delete From PFAMLIGANDCOMHMMS Where PfamID = '{0}' AND (PdbID1 = '{1}' OR PdbID2 = '{1}');", fields[0], fields[1]);
                ProtCidSettings.protcidQuery.Query(deleteString);
                deleteString = string.Format("Delete From PFAMLIGANDCOMAtomS Where PfamID = '{0}' AND (PdbID1 = '{1}' OR PdbID2 = '{1}');", fields[0], fields[1]);
                ProtCidSettings.protcidQuery.Query(deleteString);
            }
            dataReader.Close();
        }

        public void GetEntriesWithMismatchDomainIDs()
        {
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            StreamWriter dataWriter = new StreamWriter("EntryListStillMismatchDomainIDs.txt");
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                string[] notmatchedDomainIds = GetEntryMisMatchDomains(pdbId);
                dataWriter.WriteLine(pdbId + " " + ParseHelper.FormatStringFieldsToString(notmatchedDomainIds));
            }
            dataWriter.Close();
        }

        private string[] GetEntryMisMatchDomains(string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct DomainID1 As DomainID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceDomainId1Table = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct DomainID2 As DomainID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceDomainId2Table = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pfamDomainList = new List<string>();
            string domainId = "";
            foreach (DataRow domainIdRow in domainIdTable.Rows)
            {
                pfamDomainList.Add(domainIdRow["DomainID"].ToString());
            }
            List<string> notMatchDomainList = new List<string>();
            foreach (DataRow domainIdRow in interfaceDomainId1Table.Rows)
            {
                domainId = domainIdRow["DomainID"].ToString();
                if (!pfamDomainList.Contains(domainId))
                {
                    notMatchDomainList.Add(domainId);
                    //          return false;
                }
            }

            foreach (DataRow domainIdRow in interfaceDomainId2Table.Rows)
            {
                domainId = domainIdRow["DomainID"].ToString();
                if (!pfamDomainList.Contains(domainId))
                {
                    notMatchDomainList.Add(domainId);
                    //        return false;
                }
            }
            return notMatchDomainList.ToArray();
        }
        #endregion

        #region missing entries in entry-level classification
        public void FindMissingEntriesEntryLevel()
        {
            string queryString = "Select Distinct PdbID From PfamHomoSeqInfo;";
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct PdbID2 From PfamHomoRepEntryAlign;";
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString());
            }
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                entryList.Add(entryRow["PdbID2"].ToString());
            }
            entryList.Sort();
            queryString = "Select Distinct PdbID From PfamEntryPfamArch Where EntryPfamArch <> \'\';";
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            StreamWriter entryWriter = new StreamWriter("EntriesNotInEntryLevelGroups_1.txt");
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                if (entryList.BinarySearch(entryRow["PdbID"].ToString()) < 0)
                {
                    entryWriter.WriteLine(entryRow["PdbID"].ToString());
                }
            }
            entryWriter.Close();
        }
        #endregion

        #region entries with messed domain ids in domain interfaces
        public void GetDomainInterfacesEntriesMessyDomainIds  ()
        {
            string queryString = "Select Distinct PdbID, DomainID1 From PfamDomainInterfaces;";
            DataTable domainIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> domainIdList1 = new List<string>();
            foreach (DataRow domainRow in domainIdTable.Rows)
            {
                domainIdList1.Add(domainRow["PdbID"].ToString() + domainRow["DomainID1"].ToString());
            }
            domainIdList1.Sort();
            queryString = "Select Distinct PdbID, DomainID2 From PfamDomainInterfaces;";
            domainIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string entryDomain = "";
            List<string> interfaceDomainIdList = new List<string>(domainIdList1);
            foreach (DataRow domainRow in domainIdTable.Rows)
            {
                entryDomain = domainRow["PdbID"].ToString() + domainRow["DomainID2"].ToString();
                if (domainIdList1.BinarySearch(entryDomain) < 0)
                {
                    interfaceDomainIdList.Add(entryDomain);
                }
            }
            queryString = "Select Distinct PdbID, DomainID From PdbPfam;";
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pfamDomainIdList = new List<string>();
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                pfamDomainIdList.Add(domainRow["PdbID"].ToString() + domainRow["DomainID"].ToString());
            }
            pfamDomainIdList.Sort();
            List<string> messyEntryDomainList = new List<string> ();
            foreach (string lsDomain in interfaceDomainIdList)
            {
                if (pfamDomainIdList.BinarySearch (lsDomain) < 0)
                {
                    messyEntryDomainList.Add(lsDomain);
                }
            }
            string[] domainInterfaces = null;
            StreamWriter entryWriter = new StreamWriter("MessyDomainInterfaces_0.txt");
            string pdbId = "";
            long domainId = 0;
            foreach (string badDomain in messyEntryDomainList)
            {
                pdbId = badDomain.Substring(0, 4);
                domainId = Convert.ToInt64(badDomain.Substring (4, badDomain.Length - 4));
                domainInterfaces = GetDomainInterfaceEntry(pdbId, domainId);
                entryWriter.WriteLine(badDomain + " " + ParseHelper.FormatSqlListString(domainInterfaces));              
            }
            entryWriter.Close();
        }

        private string[] GetDomainInterfaceEntry (string pdbId, long domainId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceId From PfamDomainInterfaces " + 
                " Where PdbID = '{0}' AND (DomainID1 = {1} OR DomainID2 = {1});", pdbId, domainId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] domainInterfaces = new string[interfaceTable.Rows.Count];
            int count = 0;           
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                domainInterfaces[count] = pdbId + interfaceRow["DomainInterfaceID"];
                count++;
            }
            return domainInterfaces;
        }
        #endregion

    }
}
