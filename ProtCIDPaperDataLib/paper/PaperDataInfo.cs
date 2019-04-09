using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;
using PfamLib.PfamArch;
using PfamLib.Settings;
using AuxFuncLib;

using CrystalInterfaceLib.Crystal;

namespace ProtCIDPaperDataLib.paper
{
    public class PaperDataInfo
    {
        public DbInsert dbInsert = new DbInsert();
        public DbUpdate dbUpdate = null;
        public string dataDir = @"X:\Qifang\Paper\protcid_update\data_v31";
        public PfamArchitecture pfamArch = new PfamArchitecture();
        public Dictionary<int, string> relPfamPairHash = new Dictionary<int, string>();
        public string temp_dir = @"D:\xtal_temp";

        public PaperDataInfo()
        {
            Initialize();
        }

        #region initialize
        public void Initialize()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
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

            ProtCidSettings.buCompConnection = new DbConnect();
            ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.baInterfaceDbPath;

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            dbUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
            PfamLibSettings.pdbfamDbQuery = ProtCidSettings.pdbfamQuery;
        }
        #endregion       

        #region pfams with no interacting
        public void PrintPfamsWithInteraction()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "InterPfamsSumInfo.txt"));
            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            dataWriter.WriteLine("# Pfams in PDB: " + pdbPfamTable.Rows.Count.ToString());

            queryString = "Select * From PfamDomainFamilyRelation;";
            DataTable pfamRelTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pfamAList = new List<string>();
            List<string> samePfamAList = new List<string>();
            List<string> difPfamAList = new List<string>();
            string pfamId1 = "";
            string pfamId2 = "";
            foreach (DataRow pfamRelRow in pfamRelTable.Rows)
            {
                pfamId1 = pfamRelRow["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = pfamRelRow["FamilyCode2"].ToString().TrimEnd();
                if (!pfamAList.Contains(pfamId1))
                {
                    pfamAList.Add(pfamId1);
                }
                if (!pfamAList.Contains(pfamId2))
                {
                    pfamAList.Add(pfamId2);
                }
                if (pfamId1 == pfamId2)
                {
                    if (!samePfamAList.Contains(pfamId1))
                    {
                        samePfamAList.Add(pfamId1);
                    }
                }
                else
                {
                    if (!difPfamAList.Contains(pfamId1))
                    {
                        difPfamAList.Add(pfamId1);
                    }
                    if (!difPfamAList.Contains(pfamId2))
                    {
                        difPfamAList.Add(pfamId2);
                    }
                }
            }
            List<string> bothPfamAList = new List<string>();
            foreach (string pfamA in samePfamAList)
            {
                if (difPfamAList.Contains(pfamA))
                {
                    bothPfamAList.Add(pfamA);
                }
            }
            dataWriter.WriteLine("#Pfams with interactions: " + pfamAList.Count.ToString());
            dataWriter.WriteLine("#Pfams interacting with same Pfams: " + samePfamAList.Count.ToString());
            dataWriter.WriteLine("#Pfams interacting with diffferent Pfams: " + difPfamAList.Count.ToString());
            dataWriter.WriteLine("#Pfams interacting with same and different Pfams: " + bothPfamAList.Count.ToString());
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamsWithNoInteracting()
        {
            string queryString = "Select Pfam_ID, count(distinct PdbID) As EntryCount From PdbPfam Group  By Pfam_ID;";
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "NoInteractingPfamIds.txt"));
            dataWriter.WriteLine("PfamID\tNumEntries\tNumMonomerNMR\tDomainLength");
            string pfamId = "";
            int numOfMonomerNmr = 0;
            int hmmLength = 0;
            foreach (DataRow pfamRow in pfamEntryTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                if (!IsPfamWithInteraction(pfamId, false))
                {
                    hmmLength = GetPfamHmmLength(pfamId);
                    numOfMonomerNmr = GetMonomerNmrEntriesInPfam(pfamId);
                    dataWriter.WriteLine(pfamId + "\t" + pfamRow["EntryCount"].ToString() + "\t" + numOfMonomerNmr.ToString() + "\t" + hmmLength.ToString());
                }
            }

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetMonomerNmrEntriesInPfam(string pfamId)
        {
            string[] pdbIds = GetPfamEntries(pfamId);
            int numOfMonomerNmr = GetMonomerNmrStructures(pdbIds);
            return numOfMonomerNmr;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetPfamHmmLength(string pfamId)
        {
            string queryString = string.Format("Select ModelLength From PfamHmm Where PFam_ID = '{0}';", pfamId);
            DataTable hmmLenTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (hmmLenTable.Rows.Count > 0)
            {
                return Convert.ToInt32(hmmLenTable.Rows[0]["ModelLength"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private int GetMonomerNmrStructures(string[] pdbIds)
        {
            List<string> monomerNmrEntryList = new List<string>();
            foreach (string pdbId in pdbIds)
            {
                if (IsEntryNmr(pdbId))
                {
                    if (IsEntryMonomer(pdbId))
                    {
                        monomerNmrEntryList.Add(pdbId);
                    }
                }
            }
            return monomerNmrEntryList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public bool IsEntryNmr(string pdbId)
        {
            string queryString = string.Format("Select Method From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable methodTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string method = methodTable.Rows[0]["Method"].ToString().TrimEnd().ToUpper();
            if (method.IndexOf("NMR") > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public bool IsEntryMonomer(string pdbId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable pepChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (pepChainTable.Rows.Count == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamEntries(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] pfamEntries = new string[pfamEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pfamEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="withPfamB"></param>
        /// <returns></returns>
        private bool IsPfamWithInteraction(string pfamId, bool withPfamB)
        {
            string queryString = "";
            if (withPfamB)
            {
                queryString = string.Format("Select * From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", pfamId);
            }
            else
            {
                queryString = string.Format("Select * From PfamDomainFamilyRelation Where (FamilyCode1 = '{0}' AND FamilyCode2 NOT Like 'Pfam-B%') OR " +
                    "(FamilyCode1 Not Like 'Pfam-B%' AND FamilyCode2 = '{0}');", pfamId);
            }

            DataTable pfamRelTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (pfamRelTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion
        
        #region rewrite fasta file by filtering out redundant sequences
        public void PrintUniqueSequences()
        {
            string seqDataDir = @"X:\Qifang\Paper\protcid_update\data_v31\PfamPeptide\PepClusters\Pkinase\peptideSequences\SeqPkinase_Tyr";
            string[] fastaFiles = Directory.GetFiles(seqDataDir, "*.fasta");
            foreach (string fastaFile in fastaFiles)
            {
                PrintUniqueSequences(fastaFile);
            }
        }

        public void PrintUniqueSequences(string seqFastaFile)
        {
            Dictionary<string, List<string>> seqChainDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader(seqFastaFile);
            string line = "";
            string sequence = "";
            string seqName = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf(">") > -1)
                {
                    string[] fields = line.Split(' ');
                    seqName = fields[0].Substring(1, fields[0].Length - 1);
                    continue;
                }
                sequence = line;
                if (seqChainDict.ContainsKey(sequence))
                {
                    seqChainDict[sequence].Add(seqName);
                }
                else
                {
                    List<string> chainList = new List<string>();
                    chainList.Add(seqName);
                    seqChainDict.Add(sequence, chainList);
                }
            }
            dataReader.Close();
            FileInfo fileInfo = new FileInfo(seqFastaFile);
            string newFastaFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".fasta", "_unique.fasta"));
            StreamWriter dataWriter = new StreamWriter(newFastaFile);
            string headerLine = "";
            foreach (string lsSequence in seqChainDict.Keys)
            {
                List<string> seqNameList = seqChainDict[lsSequence];
                if (seqNameList.Count == 1)
                {
                    headerLine = ">" + seqNameList[0];
                }
                else
                {
                    headerLine = ">" + seqNameList[0] + " | ";
                }
                for (int i = 1; i < seqNameList.Count; i++)
                {
                    headerLine += seqNameList[i] + " ";
                }
                dataWriter.WriteLine(headerLine.TrimEnd());
                dataWriter.WriteLine(lsSequence);
            }
            dataWriter.Close();
        }
        #endregion

        #region lysine residue distances
        private string[] histoneTypes = { "H3", "H4", "H2A", "H2B" };
        public void PrintLysineDistances()
        {
            if (!Directory.Exists(temp_dir))
            {
                Directory.CreateDirectory(temp_dir);
            }
            string histoneLogFile = Path.Combine(dataDir, "histoneLog.txt");
            StreamWriter logWriter = new StreamWriter(histoneLogFile);
            string[] histonePfams = { "Histone", "CBFD_NFYB_HMF" };
            string[] dnaHistoneEntries = GetHistoneDnaEntryList();
            string histoneLysineDistFile = Path.Combine(dataDir, "LysineDistancesInHistoneAndDNAentries.txt");
            StreamWriter distanceWriter = new StreamWriter(histoneLysineDistFile);
            distanceWriter.WriteLine("PdbID\tHistone\tChain1\tChain2\tSeqID1\tAuthSeqID1\tResidue1\tSeqID2\tAuthSeqID2\tResidue2\tDistance");
            foreach (string pdbId in dnaHistoneEntries)
            {
                CalculateLysineDistanceInHistoneStructure(pdbId, distanceWriter, logWriter);
            }
            distanceWriter.Close();
            logWriter.Close();
        }

        public void CalculateLysineDistanceInHistoneStructure(string pdbId, StreamWriter dataWriter, StreamWriter logWriter)
        {
            Dictionary<string, Dictionary<string, List<int>>> chainLysineNumDict = GetEntryLysineResidues(pdbId);
            foreach (string histoneType in histoneTypes)
            {
                if (!chainLysineNumDict.ContainsKey(histoneType)) // does not contain all four types of histones
                {
                    logWriter.WriteLine(pdbId + " does not contain " + histoneType);
                    logWriter.Flush();
                    return;
                }
            }
            ChainAtoms[] chainCoords = ReadEntryCoordinates(pdbId);
            List<string> chainList = new List<string>();
            string atomDistLines = "";
            foreach (string histoneType in histoneTypes)
            {
                chainList.Clear();
                Dictionary<string, List<AtomInfo>> chainLysineCoordListDict = new Dictionary<string, List<AtomInfo>>();
                foreach (string chainId in chainLysineNumDict[histoneType].Keys)
                {
                    List<AtomInfo> lysineCoordList = new List<AtomInfo>();
                    foreach (ChainAtoms chain in chainCoords)
                    {
                        if (chain.AsymChain == chainId)
                        {
                            chainList.Add(chainId);
                            foreach (int seqNum in chainLysineNumDict[histoneType][chainId])
                            {
                                foreach (AtomInfo alphaAtom in chain.CalphaAtoms())
                                {
                                    if (alphaAtom.seqId == seqNum.ToString())
                                    {
                                        lysineCoordList.Add(alphaAtom);
                                    }
                                }
                            }
                        }
                    }
                    chainLysineCoordListDict.Add(chainId, lysineCoordList);
                }
                for (int i = 0; i < chainList.Count; i++)
                {
                    for (int j = i + 1; j < chainList.Count; j++)
                    {
                        atomDistLines = CalculateLysineCalphaDistances(pdbId, histoneType, chainList[i], chainList[j],
                            chainLysineCoordListDict[chainList[i]].ToArray(),
                            chainLysineCoordListDict[chainList[j]].ToArray());
                        dataWriter.Write(atomDistLines);
                    }
                }
            } // end of histone type
            dataWriter.Flush();
        }

        private string CalculateLysineCalphaDistances(string pdbId, string histoneType, string chainId1, string chainId2, AtomInfo[] atoms1, AtomInfo[] atoms2)
        {
            string atomDistanceLines = "";
            double distance = 0;
            foreach (AtomInfo atom1 in atoms1)
            {
                foreach (AtomInfo atom2 in atoms2)
                {
                    distance = atom1 - atom2;
                    atomDistanceLines += (pdbId + "\t" + histoneType + "\t" + chainId1 + "\t" + chainId2 + "\t" +
                        atom1.seqId + "\t" + atom1.authSeqId + "\t" + atom1.residue + "\t" +
                        atom2.seqId + "\t" + atom2.authSeqId + "\t" + atom2.residue + "\t" + distance.ToString() + "\n");
                }
            }
            return atomDistanceLines;
        }

        private ChainAtoms[] ReadEntryCoordinates(string pdbId)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, temp_dir);
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            return chains;
        }

        private Dictionary<string, Dictionary<string, List<int>>> GetEntryLysineResidues(string pdbId)
        {
            string queryString = string.Format("Select AsymID, Name, Sequence From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable chainSeqNumTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, Dictionary<string, List<int>>> chainLysineNumberDict =
                new Dictionary<string, Dictionary<string, List<int>>>();
            string name = "";
            string asymChain = "";
            string seqName = "";
            string sequence = "";
            foreach (DataRow seqRow in chainSeqNumTable.Rows)
            {
                asymChain = seqRow["AsymID"].ToString().TrimEnd();
                seqName = seqRow["Name"].ToString();
                if (seqName.IndexOf("H3") > -1)
                {
                    name = "H3";
                }
                else if (seqName.IndexOf("H4") > -1)
                {
                    name = "H4";
                }
                else if (seqName.IndexOf("H2A") > -1)
                {
                    name = "H2A";
                }
                else if (seqName.IndexOf("H2B") > -1)
                {
                    name = "H2B";
                }
                sequence = seqRow["Sequence"].ToString();
                //         string[] authSeqNumbers = seqRow["AuthSeqNumber"].ToString ().Split (',');
                List<int> lysineNumList = new List<int>();
                for (int i = 0; i < sequence.Length; i++)
                {
                    /*         if (sequence[i] == 'K' && authSeqNumbers[i] != "")
                             {
                                 lysineNumList.Add(authSeqNumbers[i]);
                             }*/
                    if (sequence[i] == 'K')
                    {
                        lysineNumList.Add(i + 1);
                    }
                }
                if (chainLysineNumberDict.ContainsKey(name))
                {
                    chainLysineNumberDict[name].Add(asymChain, lysineNumList);
                }
                else
                {
                    Dictionary<string, List<int>> chainLysineListDict = new Dictionary<string, List<int>>();
                    chainLysineListDict.Add(asymChain, lysineNumList);
                    chainLysineNumberDict.Add(name, chainLysineListDict);
                }
            }
            return chainLysineNumberDict;
        }

        public string[] GetHistoneDnaEntryList()
        {
            List<string> dnaHistoneEntryList = new List<string>();
            string histoneDnaEntryListFile = Path.Combine(dataDir, "HistoneDnaEntryList.txt");
            if (File.Exists(histoneDnaEntryListFile))
            {
                StreamReader dataReader = new StreamReader(histoneDnaEntryListFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    dnaHistoneEntryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "select *  from (select pdbid, count(distinct entityid) as entitycount from pdbpfam where pfam_id in ('Histone', 'CBFD_NFYB_HMF') group by pdbid) where entitycount = 4;";
                DataTable histoneEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                queryString = "select distinct pdbid from pfamdnarnas where pfamid in ('Histone', 'CBFD_NFYB_HMF');";
                DataTable histoneDnaEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

                string pdbId = "";
                foreach (DataRow dataRow in histoneDnaEntryTable.Rows)
                {
                    pdbId = dataRow["PdbID"].ToString();
                    DataRow[] histoneEntryRows = histoneEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    if (histoneEntryRows.Length > 0)
                    {
                        dnaHistoneEntryList.Add(pdbId);
                    }
                }
                StreamWriter dataWriter = new StreamWriter(histoneDnaEntryListFile);
                foreach (string entry in dnaHistoneEntryList)
                {
                    dataWriter.WriteLine(entry);
                }
                dataWriter.Close();
            }
            return dnaHistoneEntryList.ToArray();
        }
        #endregion

        #region pdb info query
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public DataTable GetEntitySeqTable(string[] pdbIds)
        {
            DataTable entitySeqTable = null;
            string queryString = "";
            foreach (string pdbId in pdbIds)
            {
                queryString = string.Format("Select PdbID, EntityID, AsymID, Sequence From AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable entryEntitySeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(entryEntitySeqTable, ref entitySeqTable);
            }
            return entitySeqTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public string[] GetEntityAsymChains(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable asymIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            string[] asymChains = new string[asymIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow chainRow in asymIdTable.Rows)
            {
                asymChains[count] = chainRow["AsymID"].ToString().TrimEnd();
                count++;
            }
            return asymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public string GetEntitySequence(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit WHere PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            if (seqTable.Rows.Count > 0)
            {
                sequence = seqTable.Rows[0]["Sequence"].ToString();
            }
            return sequence;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public string GetEntityUnpCode(string pdbId, int entityId)
        {
            string queryString = string.Format("Select DbCode As UnpID From PdbDbRefSifts Where PdbID = '{0}' AND ENtityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            if (dbCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select DbCode As UnpID From PdbDbRefXml Where PdbID = '{0}' AND ENtityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                dbCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            if (dbCodeTable.Rows.Count > 0)
            {
                unpCode = dbCodeTable.Rows[0]["UnpID"].ToString().TrimEnd();
            }
            return unpCode;
        }
        #endregion

        #region domain query       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public DataTable GetDomainTable(string[] pdbIds)
        {
            DataTable domainTable = null;
            string queryString = "";
            foreach (string pdbId in pdbIds)
            {
                queryString = string.Format("Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, Pfam_ID, Pfam_Acc From PdbPfam WHere PdbID = '{0}';", pdbId);
                DataTable entryDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(entryDomainTable, ref domainTable);
            }
            return domainTable;
        }
        #endregion

        #region pfam/clan query functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetPfamUnpCodes(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbPfamChain.PdbID, ChainDomainID From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND " +
                   "PdbPfam.PdbID = PdbPfamChain.PdbID AND PdbPfam.EntityID = PdbPfamChain.EntityID AND PdbPfam.DomainID = PdbPfamChain.DomainID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            int chainDomainId = 0;
            string unpCode = "";
            List<string> pfamUnpList = new List<string>();
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                chainDomainId = Convert.ToInt32(domainRow["ChainDomainID"].ToString());
                unpCode = GetDomainUnpCode(pdbId, chainDomainId);
                if (unpCode != "")
                {
                    if (!pfamUnpList.Contains(unpCode))
                    {
                        pfamUnpList.Add(unpCode);
                    }
                }
                else
                {
                    if (!pfamUnpList.Contains(pdbId))
                    {
                        pfamUnpList.Add(pdbId);
                    }
                }
            }
            return pfamUnpList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <returns></returns>
        public bool IsUnpHuman(string unpId)
        {
            if (unpId.ToUpper().IndexOf("_HUMAN") > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string GetPfamClanID(string pfamId)
        {
            string queryString = string.Format("Select Clan_ID From PfamClanFamily, PfamClans, PfamHmm Where PfamHmm.Pfam_ID = '{0}' AND " +
                "PfamHmm.Pfam_Acc = PfamClanFamily.Pfam_Acc AND PfamClanFamily.Clan_Acc = PfamClans.Clan_Acc;", pfamId);
            DataTable clanIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string clanId = "-";
            if (clanIdTable.Rows.Count > 0)
            {
                clanId = clanIdTable.Rows[0]["Clan_ID"].ToString().TrimEnd();
            }
            return clanId;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetPfamHumanSeqs(string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpCode From HumanPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamHumanSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] humanUnpSeqs = new string[pfamHumanSeqTable.Rows.Count];
            int count = 0;
            foreach (DataRow seqRow in pfamHumanSeqTable.Rows)
            {
                humanUnpSeqs[count] = seqRow["UnpCode"].ToString().TrimEnd();
                count++;
            }
            return humanUnpSeqs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetPfamHumanUnpSeqsInPdb(string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpID From UnpPdbfam, PdbPfam Where Pfam_ID = {0} " +
                " AND UnpPdbfam.PdbID = PdbPfam.PdbID AND UnpPdbfam.DomainID = PdbPfam.DomainID;", pfamId);
            DataTable pdbUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> humanUnpList = new List<string>();
            string unpId = "";
            foreach (DataRow unpRow in pdbUnpTable.Rows)
            {
                unpId = unpRow["UnpCode"].ToString().TrimEnd();
                if (IsUnpHuman (unpId))
                {
                    humanUnpList.Add(unpId);
                }
            }
            return humanUnpList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string[] GetPfamUnpSeqsInPdb (string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpID From UnpPdbfam, PdbPfam Where Pfam_ID = {0} " +
                " AND UnpPdbfam.PdbID = PdbPfam.PdbID AND UnpPdbfam.DomainID = PdbPfam.DomainID;", pfamId);
            DataTable pdbUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] unpCodes = new string[pdbUnpTable.Rows.Count];
            int count = 0;
            foreach (DataRow unpRow in pdbUnpTable.Rows)
            {
                unpCodes[count] = unpRow["UnpCode"].ToString().TrimEnd();
                count++;
            }
            return unpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public int GetNumEntriesOfPfam(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return entryTable.Rows.Count;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        public int GetRelationSeqID(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " +
            "(FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }
            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanId"></param>
        /// <returns></returns>
        public string[] GetClanPfams(string clanId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PfamHmm, PfamClans, PfamClanFamily Where Clan_ID = '{0}' " +
                " AND PfamClans.Clan_ACC = PfamClanFamily.Clan_ACC AND PfamClanFamily.Pfam_Acc = PfamHmm.Pfam_Acc;", clanId);
            DataTable clanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] clanPfams = new string[clanPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in clanPfamTable.Rows)
            {
                clanPfams[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return clanPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanPfams"></param>
        /// <returns></returns>
        public string[] GetClanPfamsInPdb(string[] clanPfams)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where Pfam_ID IN ({0});", ParseHelper.FormatSqlListString(clanPfams));
            DataTable clanPdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] clanPdbPfams = new string[clanPdbPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in clanPdbPfamTable.Rows)
            {
                clanPdbPfams[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return clanPdbPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanId"></param>
        /// <returns></returns>
        public string[] GetClanHumanPfams(string clanId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PfamClans, PfamClanFamily, HumanPfam Where Clan_ID = '{0}' " +
                " AND PfamClans.Clan_ACC = PfamClanFamily.Clan_ACC AND PfamClanFamily.Pfam_Acc = HumanPfam.Pfam_Acc;", clanId);
            DataTable clanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] clanHumanPfams = new string[clanPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in clanPfamTable.Rows)
            {
                clanHumanPfams[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return clanHumanPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanId"></param>
        /// <returns></returns>
        public string[] GetClanHumanPfamsInPdb(string clanId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PfamClans, PfamClanFamily, PdbPfam, UnpPdbfam Where Clan_ID = '{0}' " +
                " AND PfamClans.Clan_ACC = PfamClanFamily.Clan_ACC AND PfamClanFamily.Pfam_Acc = PdbPfam.Pfam_Acc AND " +
                " UnpPdbfam.PdbID = PdbPfam.PdbID AND UnpPdbfam.DomainID = PdbPfam.DomainID AND UnpID Like '%_HUMAN';", clanId);
            DataTable clanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] clanHumanPfams = new string[clanPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in clanPfamTable.Rows)
            {
                clanHumanPfams[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return clanHumanPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanPfams"></param>
        /// <returns></returns>
        public string[][] GetClanUnpHumanUnpsInPdb(string[] clanPfams)
        {
            string[][] clanUnpHumanUnps = new string[2][];
            List<string> clanUnpList = new List<string>();
            List<string> clanHumanUnpList = new List<string>();
            if (clanPfams.Length > 0)
            {
                string queryString = string.Format("Select Distinct UnpID From UnpPdbfam, PdbPfam Where Pfam_ID IN ({0}) " +
                    " AND UnpPdbfam.PdbID = PdbPfam.PdbID AND UnpPdbfam.DomainID = PdbPfam.DomainID;", ParseHelper.FormatSqlListString(clanPfams));
                DataTable pdbUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                string unpId = "";
                foreach (DataRow unpRow in pdbUnpTable.Rows)
                {
                    unpId = unpRow["UnpID"].ToString().TrimEnd();
                    if (IsUnpHuman(unpId))
                    {
                        clanHumanUnpList.Add(unpId);
                    }
                    clanUnpList.Add(unpId);
                }

                clanUnpHumanUnps[0] = clanUnpList.ToArray(); // #unp in pdb
                clanUnpHumanUnps[1] = clanHumanUnpList.ToArray();  // #human unp in pdb
            }
            return clanUnpHumanUnps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpIds"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetUnpDomainListDict(string[] unpIds)
        {
            DataTable unpDomainTable = null;
            for (int i = 0; i < unpIds.Length; i += 300)
            {
                string[] subUnpIds = ParseHelper.GetSubArray(unpIds, i, 300);
                string queryString = string.Format("Select Distinct UnpID, PdbID, DomainID From UnpPdbfam Where UnpID IN ({0});", ParseHelper.FormatSqlListString(subUnpIds));
                DataTable thisUnpDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(thisUnpDomainTable, ref unpDomainTable);
            }
            Dictionary<string, List<string>> unpDomainDict = new Dictionary<string, List<string>>();
            string unpId = "";
            string domain = "";
            foreach (DataRow domainRow in unpDomainTable.Rows)
            {
                unpId = domainRow["UnpID"].ToString().TrimEnd();
                domain = domainRow["PdbID"].ToString() + domainRow["DomainID"].ToString();
                if (unpDomainDict.ContainsKey(unpId))
                {
                    if (!unpDomainDict[unpId].Contains(domain))
                    {
                        unpDomainDict[unpId].Add(domain);
                    }
                }
                else
                {
                    List<string> domainList = new List<string>();
                    domainList.Add(domain);
                    unpDomainDict.Add(unpId, domainList);
                }
            }
            return unpDomainDict;
        }
        #endregion

        #region chain domain query functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public string GetChainSequence(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (seqTable.Rows.Count > 0)
            {
                return seqTable.Rows[0]["Sequence"].ToString();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="polymerType"></param>
        /// <returns></returns>
        public string GetChainSequence(string pdbId, string asymChain, out string polymerType)
        {
            string queryString = string.Format("Select Sequence, PolymerType From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            polymerType = "";
            if (seqTable.Rows.Count > 0)
            {
                polymerType = seqTable.Rows[0]["PolymerType"].ToString().TrimEnd();
                return seqTable.Rows[0]["Sequence"].ToString();
            }
            return "";
        }

        /// <summary>
        /// suppose one domain has one uniprot code
        /// </summary>
        /// <param name="chainDomain"></param>
        /// <returns></returns>
        public string GetDomainUnpCode(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select dbcode, asymid, seqalignbeg, seqalignend, dbalignbeg, dbalignend, seqstart, seqend From PdbPfamChain, PdbPfam, PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where PdbPfamChain.pdbid = '{0}' and chaindomainid = {1} " +
                " and PdbPfamChain.pdbid = PdbPfam.pdbid and PdbPfamChain.domainid = PdbPfam.domainid and PdbPfamChain.entityid = PdbPfam.entityid " +
                "  and PdbPfamChain.pdbid = PdbDbRefSifts.pdbid and PdbPfam.entityid = PdbDbRefSifts.entityid and PdbDbRefSifts.pdbid = PdbDbRefSeqSifts.pdbid " +
                " and PdbDbRefSifts.refid = PdbDbRefSeqSifts.refid and PdbDbRefSeqSifts.asymid = PdbPfamChain.asymChain; ", pdbId, chainDomainId);
            DataTable domainUnpSeqRegionTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            int seqDbBeg = 0;
            int seqDbEnd = 0;
            int seqDomainStart = 0;
            int seqDomainEnd = 0;
            foreach (DataRow unpSeqRow in domainUnpSeqRegionTable.Rows)
            {
                seqDbBeg = Convert.ToInt32(unpSeqRow["SeqAlignBeg"].ToString());
                seqDbEnd = Convert.ToInt32(unpSeqRow["SeqAlignEnd"].ToString());
                seqDomainStart = Convert.ToInt32(unpSeqRow["SeqStart"].ToString());
                seqDomainEnd = Convert.ToInt32(unpSeqRow["SeqEnd"].ToString());
                if (IsOverlap(seqDbBeg, seqDbEnd, seqDomainStart, seqDomainEnd))
                {
                    unpCode = unpSeqRow["DbCode"].ToString().TrimEnd();
                    break;
                }
            }
            return unpCode;
        }
        #endregion

        #region helper functions      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqDbStart"></param>
        /// <param name="seqDbEnd"></param>
        /// <param name="seqDomainStart"></param>
        /// <param name="seqDomainEnd"></param>
        /// <returns></returns>
        public bool IsOverlap(int seqDbStart, int seqDbEnd, int seqDomainStart, int seqDomainEnd)
        {
            int maxStart = Math.Max(seqDbStart, seqDomainStart);
            int minEnd = Math.Min(seqDbEnd, seqDomainEnd);

            int overlap = minEnd - maxStart;
            double coverage = (double)overlap / (double)(seqDomainEnd - seqDomainStart + 1);
            if (coverage >= 0.5)
            {
                return true;
            }
            return false;
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="itemList"></param>
        /// <param name="toBeAddedItemList"></param>
        public void AddSecondListToFirst<T> (List<T> itemList, List<T> toBeAddedItemList )
        {
            if (itemList == null)
            {
                itemList = new List<T>(toBeAddedItemList);
                return;
            }
            if (toBeAddedItemList == null)
            {
                return;
            }
            foreach (T item in toBeAddedItemList)
            {
                if (! itemList.Contains (item))
                {
                    itemList.Add(item);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <returns></returns>
        public string FormatArrayString<T>(List<T> dataList)
        {
            if (dataList == null)
            {
                return "";
            }
            string arrayString = "";
            foreach (T item in dataList)
            {
                arrayString += (item.ToString() + ",");
            }
            arrayString = arrayString.TrimEnd(',');
            return arrayString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <returns></returns>
        public string FormatArrayString<T>(List<T> dataList, char sep)
        {
            if (dataList == null)
            {
                return "";
            }
            string arrayString = "";
            foreach (T item in dataList)
            {
                arrayString += (item.ToString() + sep);
            }
            arrayString = arrayString.TrimEnd(sep);
            return arrayString;
        }      

        /// <summary>
        /// /
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public string FormatArrayString<T>(T[] itemList)
        {
            if (itemList == null)
            {
                return "";
            }
            string arrayString = "";
            foreach (T item in itemList)
            {
                arrayString += (item.ToString() + ",");
            }
            arrayString = arrayString.TrimEnd(',');
            return arrayString;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public string FormatArrayString<T>(T[] itemList, char sep)
        {
            if (itemList == null)
            {
                return "";
            }
            string arrayString = "";
            if (itemList.Length == 0)
            {
                return "-";
            }
            foreach (T item in itemList)
            {
                arrayString += (item.ToString() + sep);
            }
            arrayString = arrayString.TrimEnd(sep);
            return arrayString;
        }      
        #endregion
    }

}
