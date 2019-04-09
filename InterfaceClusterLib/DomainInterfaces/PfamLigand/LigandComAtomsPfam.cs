using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.StructureComp;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class LigandComAtomsPfam
    {
        #region member variables
        private string pfamLigandDataDir = @"D:\protcid\pfam\DomainAlign\pdb";
        private string coordDataDir = "";
        public CmdOperations tarOperator = new CmdOperations();
        private CmdOperations pymolLauncher = new CmdOperations();
       private const double atomOverlapCutoff = 0.5;
  //       private const double atomOverlapCutoff = 1.0;
        public RmsdCalculator rmsdCal = new RmsdCalculator();
        private DataTable domainLigandCompTable = null;
        private string tableName = PfamLigandTableNames.pfamLigandComAtomTableName;
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private DataTable ligandNameTable = null;
        private DataTable pfamIdAccTable = null;
        #endregion

        public LigandComAtomsPfam ()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
                if (ProtCidSettings.protcidDbConnection == null)
                {
                    ProtCidSettings.protcidDbConnection = new DbConnect();
                    ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                                                        ProtCidSettings.dirSettings.protcidDbPath;
                }

                if (ProtCidSettings.pdbfamDbConnection == null)
                {
                    ProtCidSettings.pdbfamDbConnection = new DbConnect();
                    ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                                                       ProtCidSettings.dirSettings.pdbfamDbPath;
                }
                pfamLigandDataDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "DomainAlign\\pdb");

                ProtCidSettings.tempDir = @"X:\ligand_temp";
                if (! Directory.Exists (ProtCidSettings.tempDir))
                {
                    Directory.CreateDirectory(ProtCidSettings.tempDir);
                }
            }
            SetPdbLigandNameTable();
            SetPfamIdAccMap();
        }

        #region build a new data table
        /// <summary>
        /// 
        /// </summary>
        public void CalculateOverlapLigandAtomsInPfam()
        {
            bool isUpdate = true;
            CreateTables(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate common ligand atoms  for simple ligand clustering");
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Calculate common ligand atoms  for simple ligand clustering");

            string[] pdbDomainAlignFiles = Directory.GetFiles(pfamLigandDataDir);
            ProtCidSettings.progressInfo.totalOperationNum = pdbDomainAlignFiles.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbDomainAlignFiles.Length;

            //    string domainAlignFile = "";
            //   for (int i = pdbDomainAlignFiles.Length - 1; i >= 0; i--)
            foreach (string domainAlignFile in pdbDomainAlignFiles)
            {
         //       domainAlignFile = pdbDomainAlignFiles[i];
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = domainAlignFile;
                ProtCidSettings.logWriter.WriteLine(domainAlignFile);

                try
                {
                    CalculateOverlapLigandAtomsFromPdb(domainAlignFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(domainAlignFile + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(domainAlignFile + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Ligand common atoms done!");
            ProtCidSettings.logWriter.WriteLine("Ligand common atoms done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.protcidDbConnection.DisconnectFromDatabase();
            ProtCidSettings.pdbfamDbConnection.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAlignFile"></param>
        public void CalculateOverlapLigandAtomsFromPdb(string pfamAlignFile)
        {
            if (!File.Exists(pfamAlignFile))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No pfam domain align file exist. " + pfamAlignFile);
                ProtCidSettings.logWriter.WriteLine("No pfam domain align file exist. " + pfamAlignFile);
                return;
            }
            // retrieve the exist domain files from pdb folder since it contains more domain files than unp and cryst           
            FileInfo fileInfo = new FileInfo(pfamAlignFile);
            coordDataDir = fileInfo.DirectoryName;
            string pfamAcc = fileInfo.Name.Replace("_pdb.tar.gz", "");
            if (IsPfamLigandsCompared (pfamAcc))
            {
                return;
            }
           
            coordDataDir = Path.Combine(ProtCidSettings.tempDir, pfamAcc);
            if (!Directory.Exists(coordDataDir))
            {
                Directory.CreateDirectory(coordDataDir);
                tarOperator.UnTar(pfamAlignFile, coordDataDir);
            }
            if (Directory.Exists (Path.Combine (coordDataDir, pfamAcc + "_pdb")))
            {
                coordDataDir = Path.Combine (coordDataDir, pfamAcc + "_pdb");
            }  

            string pmlScriptFile = Path.Combine(coordDataDir, pfamAcc + "_pairFitDomain.pml");
            string newPmlScriptFile = Path.Combine(coordDataDir, pfamAcc + "_ligandCoord.pml");
 //           string pymolCoordFile = Path.Combine(coordDataDir, pfamAcc + ".coord");
            string pymolCoordDir = Path.Combine(coordDataDir, "coord");
            if (! Directory.Exists (pymolCoordDir))
            {
                Directory.CreateDirectory(pymolCoordDir);
            }
            string coordFileLinuxPath = pymolCoordDir.Replace("\\", "/") + "/";
            string centerDomain = "";

            Dictionary<string, string[]> domainInteractingLigandsHash = null;
            Dictionary<string, Range[]> domainResiRangeHash = null;
            try
            {
                domainInteractingLigandsHash = ReadPfamDomainsLigands(pmlScriptFile, out domainResiRangeHash);         
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " read domain ligand pymol script file errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " read domain ligand pymol script file errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }
            
            string[] pfamDomainsInFileOrder = null;
            try
            {

                pfamDomainsInFileOrder = ChangePmlScriptForLigandDomainsOnly(pmlScriptFile, newPmlScriptFile, coordFileLinuxPath, domainInteractingLigandsHash, out centerDomain);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " modify pymol script file error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " modify pymol script file error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            try
            {
                //               if (!File.Exists(pymolCoordFile))
                pymolLauncher.RunPymol(newPmlScriptFile);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " running PyMol script error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " running PyMol script error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            string centerDomainFile = Path.Combine(coordDataDir, centerDomain + ".pfam");
            string[] domainsWithLigands = pfamDomainsInFileOrder;
            if (! domainInteractingLigandsHash.ContainsKey(centerDomain))
            {
                domainsWithLigands = new string[pfamDomainsInFileOrder.Length - 1];
                Array.Copy(pfamDomainsInFileOrder, 1, domainsWithLigands, 0, domainsWithLigands.Length);
            }
            Dictionary<string, Dictionary<string, AtomInfo[]>> pymolSupCoordHash = null;
            try
            {
                pymolSupCoordHash = ReadDomainLigandCoordinates(pymolCoordDir, domainsWithLigands);
 //               pymolSupCoordHash = ReadDomainLigandCoordinates(pymolCoordDir, centerDomainFile, pfamDomainsInFileOrder, excludeCenterDomain);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " reading coordinates from PyMol output file error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " reading coordinates from PyMol output file error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }  

            string pfamId = GetPfamIdFromPfamAcc(pfamAcc);
            Dictionary<string, string[][]> domainFileAsymChainMatch = GetDomainFileAsymChainMatch(pfamDomainsInFileOrder);
            try
            {
                CountDomainRmsdAndNumOfOverlapLigandAtoms(pfamId, pymolSupCoordHash, domainInteractingLigandsHash, 
                    domainResiRangeHash, domainFileAsymChainMatch);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " counting common atoms of two ligands error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " counting common atoms of two ligands error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            try
            {
                Directory.Delete(coordDataDir, true);
            }
            catch (Exception ex) 
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private bool IsPfamLigandsCompared (string pfamAcc)
        {
            string pfamId = GetPfamIdFromPfamAcc(pfamAcc);
            string queryString = string.Format("Select PfamID From {0} Where PfamID = '{1}'", tableName, pfamId);
            DataTable comLigandTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (comLigandTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UdpateOverlapLigandAtomsInPfam (string[] updateEntries)
        {
            Dictionary<string, string[]> pfamUpdateEntryHash = GetPfamUpdateEntryHash(updateEntries);
            UpdateOverlapLigandAtomsInPfam(pfamUpdateEntryHash);
        }
        /// <summary>
        /// 
        /// </summary>
        public void UpdateOverlapLigandAtomsInPfam(Dictionary<string, string[]> pfamUpdateEntryHash)
        {
            bool isUpdate = true;
            CreateTables(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update common ligand atoms  for simple ligand clustering");
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Update common ligand atoms  for simple ligand clustering");

            List<string> pfamAccList = new List<string> (pfamUpdateEntryHash.Keys);
            pfamAccList.Sort();
            ProtCidSettings.progressInfo.totalOperationNum = pfamAccList.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamAccList.Count;
            string domainAlignFile = "";

            foreach (string pfamAcc in pfamAccList)
            {
                domainAlignFile = Path.Combine(ProtCidSettings.tempDir, pfamAcc + "_pdb" + ".tar.gz");
                string[] updateEntries = pfamUpdateEntryHash[pfamAcc];

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = domainAlignFile;
                ProtCidSettings.logWriter.WriteLine(domainAlignFile);

                try
                {
                    UpdateOverlapLigandAtomsFromPdb(domainAlignFile, updateEntries);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(domainAlignFile + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(domainAlignFile + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Ligand common atoms done!");
            ProtCidSettings.logWriter.WriteLine("Ligand common atoms done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.protcidDbConnection.DisconnectFromDatabase();
            ProtCidSettings.pdbfamDbConnection.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAlignFile"></param>
        /// <param name="updateEntries"></param>
        public void UpdateOverlapLigandAtomsFromPdb(string pfamAlignFile, string[] pfamUpdateEntries)
        {
            if (!File.Exists(pfamAlignFile))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No pfam domain align file exist. " + pfamAlignFile);
                ProtCidSettings.logWriter.WriteLine("No pfam domain align file exist. " + pfamAlignFile);
                return;
            }
            // retrieve the exist domain files from pdb folder since it contains more domain files than unp and cryst           
            FileInfo fileInfo = new FileInfo(pfamAlignFile);
            coordDataDir = fileInfo.DirectoryName;
            string pfamAcc = fileInfo.Name.Replace("_pdb.tar.gz", "");
            coordDataDir = Path.Combine(ProtCidSettings.tempDir, pfamAcc + "_pdb");
            if (!Directory.Exists(coordDataDir))
            {
                tarOperator.UnTar(pfamAlignFile, ProtCidSettings.tempDir);
            }

            string pmlScriptFile = Path.Combine(coordDataDir, pfamAcc + "_pairFitDomain.pml");
            string newPmlScriptFile = Path.Combine(coordDataDir, pfamAcc + "_ligandCoord.pml");
//            string pymolCoordFile = Path.Combine(coordDataDir, pfamAcc + ".coord");
            string pymolCoordDir = Path.Combine(coordDataDir, "coord");
            if (! Directory.Exists (pymolCoordDir))
            {
                Directory.CreateDirectory(pymolCoordDir);
            }
            string coordFileLinuxPath = pymolCoordDir.Replace("\\", "/") + "/";
            string centerDomain = "";

            Dictionary<string, string[]> domainInteractingLigandsHash = null;
            Dictionary<string, Range[]> domainResiRangeHash = null;
            try
            {
                domainInteractingLigandsHash = ReadPfamDomainsLigands(pmlScriptFile, out domainResiRangeHash);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " read domain ligand pymol script file errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " read domain ligand pymol script file errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }
           
            string[] pfamDomainsInFileOrder = null;
            try
            {

                pfamDomainsInFileOrder = ChangePmlScriptForLigandDomainsOnly(pmlScriptFile, newPmlScriptFile, coordFileLinuxPath, domainInteractingLigandsHash, out centerDomain);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " modify pymol script file error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " modify pymol script file error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            try
            {
                pymolLauncher.RunPymol(newPmlScriptFile);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " running PyMol script error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " running PyMol script error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            string centerDomainFile = Path.Combine(coordDataDir, centerDomain + ".pfam");
            string[] domainsWithLigands = pfamDomainsInFileOrder;
            if (! domainInteractingLigandsHash.ContainsKey (centerDomain))
            {
                domainsWithLigands = new string[pfamDomainsInFileOrder.Length - 1];
                Array.Copy(pfamDomainsInFileOrder, 1, domainsWithLigands, 0, domainsWithLigands.Length);
            }
            Dictionary<string, Dictionary<string, AtomInfo[]>> pymolSupCoordHash = null;
            try
            {
                pymolSupCoordHash = ReadDomainLigandCoordinates(pymolCoordDir, domainsWithLigands);
 //               pymolSupCoordHash = ReadDomainLigandCoordinates(pymolCoordFile, centerDomainFile, pfamDomainsInFileOrder, excludeCenterDomain);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " reading coordinates from PyMol output file error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " reading coordinates from PyMol output file error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }
            string pfamId = GetPfamIdFromPfamAcc(pfamAcc);
            Dictionary<string, string[][]> fileAsymChainMatch = GetDomainFileAsymChainMatch(pfamDomainsInFileOrder);
            try
            {
                CountDomainRmsdAndNumOfOverlapLigandAtoms(pfamId, pymolSupCoordHash, domainInteractingLigandsHash,
                    domainResiRangeHash, fileAsymChainMatch, pfamUpdateEntries);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamAcc + " counting common atoms of two ligands error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamAcc + " counting common atoms of two ligands error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            try
            {
                Directory.Delete(coordDataDir, true);
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetPfamUpdateEntryHash (string[] updateEntries)
        {
            Dictionary<string, List<string>> pfamUpdateEntryListHash = new Dictionary<string,List<string>> ();
            string[] subUpdateEntries = null;
            string queryString = "";
            string pfamAcc = "";
            string pdbId = "";
            for (int i = 0; i < updateEntries.Length; i +=200)
            {
                subUpdateEntries = ParseHelper.GetSubArray(updateEntries, i, 200);
                queryString = string.Format("Select Distinct PdbID, Pfam_Acc From PdbPfam Where PdbID IN ({0})", ParseHelper.FormatSqlListString(subUpdateEntries));
                DataTable entryPfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                foreach (DataRow pfamRow in entryPfamAccTable.Rows)
                {
                    pdbId = pfamRow["PdbID"].ToString();
                    pfamAcc = pfamRow["Pfam_Acc"].ToString().TrimEnd();
                    if (pfamUpdateEntryListHash.ContainsKey(pfamAcc))
                    {
                        if (!pfamUpdateEntryListHash[pfamAcc].Contains(pdbId))
                        {
                            pfamUpdateEntryListHash[pfamAcc].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        pfamUpdateEntryListHash.Add(pfamAcc, entryList);
                    }
                }
            }
            Dictionary<string, string[]> pfamUpdateEntryHash = new Dictionary<string, string[]>();
            foreach (string keypfamAcc in pfamUpdateEntryListHash.Keys)
            {
                pfamUpdateEntryHash.Add (keypfamAcc, pfamUpdateEntryHash[keypfamAcc].ToArray ());
            }
            return pfamUpdateEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteUpdateLigandComAtomsData (string pfamID, string[] updateEntries)
        {
            string deleteString = "";
            string[] subUpdateEntries = null;
            for (int i = 0; i < updateEntries.Length; i += 200)
            {
                subUpdateEntries = ParseHelper.GetSubArray(updateEntries, i, 200);
                deleteString = string.Format("Delete From {0} Where PfamID = {1} AND (PdbID1 IN ({2}) OR PdbID2 IN ({2}));", tableName, pfamID, ParseHelper.FormatSqlListString (subUpdateEntries));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void DeletePfamLigandComAtomData(string pfamId, string pdbId1, string pdbId2)
        {
            string deleteString = string.Format("Delete From {0} Where PfamID = '{1}' AND (PdbID1 = '{2}' AND PdbID2 = '{3}');", tableName, pfamId, pdbId1, pdbId2);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion

        #region pymol script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadPfamDomainsWithInteractingLigands(string pmlScriptFile)
        {
            Dictionary<string, List<string>> domainLigandChainsHash = new Dictionary<string,List<string>> ();
            string line = "";
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            bool ligandStart = false;
            string domainChain = "";
            string ligandChain = "";
            int domainLigandAndIndex = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "show spheres, selectLigands")
                {
                    ligandStart = true;
                    continue;
                }
                if (!ligandStart)
                {
                    continue;
                }
                string[] fields = line.Split(",+".ToCharArray());
                foreach (string field in fields)
                {
                    if (field.IndexOf(".pfam") > -1)
                    {
                        domainLigandAndIndex = field.IndexOf("and");
                        domainChain = field.Substring(0, domainLigandAndIndex).Trim();
                        domainLigandAndIndex = domainLigandAndIndex + "and ".Length;
                        ligandChain = field.Substring(domainLigandAndIndex, field.Length - domainLigandAndIndex).Trim();
                        if (domainLigandChainsHash.ContainsKey(domainChain))
                        {
                            domainLigandChainsHash[domainChain].Add(ligandChain);
                        }
                        else
                        {
                            List<string> ligandChainList = new List<string> ();
                            ligandChainList.Add(ligandChain);
                            domainLigandChainsHash.Add(domainChain, ligandChainList);
                        }
                    }
                }
            }
            dataReader.Close();
            return domainLigandChainsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadPfamDomainsLigands(string pmlScriptFile, out Dictionary<string, Range[]> domainResiRangesHash)
        {
            Dictionary<string, string[]> domainLigandChainsHash = new Dictionary<string,string[]> ();
            domainResiRangesHash = new Dictionary<string,Range[]> ();
            string line = "";
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            string ligandChain = "";
            List<string> ligandChainList = null;
            string pfamDomain = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load ") > -1)
                {
                    if (ligandChainList != null && ligandChainList.Count > 0)
                    {
                        domainLigandChainsHash.Add(pfamDomain, ligandChainList.ToArray ());
                    }
                    string[] fields = line.Split();
                    pfamDomain = fields[1].Replace(".pfam", "");
                    ligandChainList = new List<string> ();
                }
                if (line.IndexOf ("spectrum count, rainbow, ") > -1)
                {
                    Range[] domainRanges = GetDomainRanges(line);
                    domainResiRangesHash.Add(pfamDomain, domainRanges);
                }
                if (line == "show spheres, selectLigands")
                {
                    break;
                }
                else if (line.IndexOf("show spheres, ") > -1)
                {
                    string[] fields = line.Split();
                    ligandChain = fields[5];
                    ligandChainList.Add(ligandChain);
                }
            }
            dataReader.Close();
            if (ligandChainList.Count > 0)
            {
                domainLigandChainsHash.Add(pfamDomain, ligandChainList.ToArray ());
            }
            return domainLigandChainsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rangeLine"></param>
        /// <returns></returns>
        private Range[] GetDomainRanges (string rangeLine)
        {
            int resiIndex = rangeLine.IndexOf("resi ") + "resi ".Length;
            string resiRangeString = rangeLine.Substring(resiIndex, rangeLine.Length - resiIndex);
            List<Range> domainRangeList = new List<Range> ();
            string[] rangeFields = resiRangeString.Split('+');
            foreach (string field in rangeFields)
            {
                string[] startEndPoses = field.Split('-');
                Range range = new Range();
                range.startPos = Convert.ToInt32(startEndPoses[0]);
                range.endPos = Convert.ToInt32(startEndPoses[1]);
                domainRangeList.Add(range);
            }

            return domainRangeList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        /// <param name="newPmlScriptFile"></param>
        /// <param name="coordFileLinuxPath"></param>
        /// <returns></returns>
        private string[] ChangePmlScriptForLigandDomainsOnly (string pmlScriptFile, string newPmlScriptFile, string coordFileLinuxPath, 
            Dictionary<string, string[]> domainLigandChainsHash, out string centerDomain)
        {            
            List<string> ligandDomainListInFileOrder = new List<string>  ();
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            StreamWriter dataWriter = new StreamWriter(newPmlScriptFile);
            int numPfam = 0;
            string domainPfam = "";
            bool isLiganDomain = false;
            centerDomain = "";
            string line = dataReader.ReadLine ();
            dataWriter.WriteLine(line); // set ignore_case, 0
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf ("center ") > -1)
                {
                    string[] fields = line.Split();
                    centerDomain = fields[1].Replace(".pfam", "");
                    dataWriter.WriteLine(line);
                }
                if (line.IndexOf ("load ") < 0 && line.IndexOf ("pair_fit ") < 0)
                {
                    continue;
                }
                if (line.IndexOf("load ") > -1)
                {
                    string[] fields = line.Split();
                    domainPfam = fields[1].Replace (".pfam", "");
                    numPfam++;                   
                    isLiganDomain = false;
                    if (domainLigandChainsHash.ContainsKey (domainPfam))
                    {
                        if (!ligandDomainListInFileOrder.Contains(domainPfam))
                        {
                            ligandDomainListInFileOrder.Add(domainPfam);
                        }
                        isLiganDomain = true;
                    }
                }
                if (numPfam <= 1)  // first .pfam file
                {
                    dataWriter.WriteLine(line);
                    if (!ligandDomainListInFileOrder.Contains(domainPfam))
                    {
                        ligandDomainListInFileOrder.Add(domainPfam);
                    }
                }
                else
                {
                    if (isLiganDomain)
                    {
                        dataWriter.WriteLine(line);
                    }
                }
            }
            dataReader.Close();

            // save each pfam (object) to coordinate file
            foreach (string domain in ligandDomainListInFileOrder)
            {
                dataWriter.WriteLine("cmd.save (\"" + coordFileLinuxPath + domain + ".coord\", " + "\"" + domain + ".pfam\")");
            }
     //       dataWriter.WriteLine("cmd.save (\"" + coordFileLinuxPath + "\")");
            dataWriter.WriteLine("quit");
            dataWriter.Close();
       
            return ligandDomainListInFileOrder.ToArray ();
        }
        #endregion

        #region parse pymol coordinates
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolCoordDir"></param>
        /// <param name="domainsWithLigands"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> ReadDomainLigandCoordinates(string pymolCoordDir, string[] domainsWithLigands)
        {
            string pmlCoordFile = "";
            Dictionary<string, Dictionary<string, AtomInfo[]>> domainChainCoordHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();
            Dictionary<string, AtomInfo[]> chainCoordHash = null;
            foreach (string domain in domainsWithLigands)
            {
                pmlCoordFile = Path.Combine(pymolCoordDir, domain + ".coord");
                try
                {
                    chainCoordHash = ReadChainCoordinatesHash(pmlCoordFile);
                    domainChainCoordHash.Add(domain, chainCoordHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(domain + " read pymol coordinates error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(domain + " read pymol coordinates error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            return domainChainCoordHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPmlCoordFile"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> ReadChainCoordinatesHash(string domainPmlCoordFile)
        {
            Dictionary<string, AtomInfo[]> chainCoordHash = new Dictionary<string,AtomInfo[]> ();
            StreamReader coordReader = new StreamReader(domainPmlCoordFile);
            string line = "";
            string currentChainId = "";
            string preChainId = "";
            List<AtomInfo> chainCoordList = new List<AtomInfo> ();
            bool isHetAtomChain = false;
            bool isPreHetAtomChain = false;
            AtomInfo atom = null;
            while ((line = coordReader.ReadLine()) != null)
            {
                if (preChainId != currentChainId && preChainId != "")
                {
                    if (!isPreHetAtomChain && preChainId == "A")
                    {                      
                        chainCoordHash.Add(preChainId, chainCoordList.ToArray ());                       
                    }
                    else
                    {
                        AtomInfo[] ligandAtoms = new AtomInfo[chainCoordList.Count];
                        chainCoordList.CopyTo(ligandAtoms);                       
                        if (chainCoordHash != null)
                        {
                            chainCoordHash.Add(preChainId, ligandAtoms);
                        }                      
                    }
                    preChainId = currentChainId;
                    chainCoordList.Clear();
                    chainCoordList.Add(atom);
                    isPreHetAtomChain = isHetAtomChain;
                }

                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                    currentChainId = atomFields[5];
                    if (preChainId == "")
                    {
                        preChainId = currentChainId;
                    }

                    if (currentChainId == preChainId)
                    {
                        chainCoordList.Add(atom);
                    }
                    isHetAtomChain = false;
                }
                if (line.IndexOf("HETATM") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                    currentChainId = atomFields[5];
                    if (preChainId == "")
                    {
                        preChainId = currentChainId;
                    }

                    if (currentChainId == preChainId)
                    {
                        chainCoordList.Add(atom);
                    }
                    isHetAtomChain = true;
                }
            }
            coordReader.Close();
            if (chainCoordList.Count > 0)
            {
                AtomInfo[] ligandAtoms = new AtomInfo[chainCoordList.Count];
                chainCoordList.CopyTo(ligandAtoms);
                chainCoordHash.Add(currentChainId, ligandAtoms);
            }
            return chainCoordHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolCoordFile"></param>
        /// <param name="centerDomainFile"></param>
        /// <param name="domainInFileOrder"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> ReadDomainLigandCoordinates(string pymolCoordFile, string centerDomainFile, string[] domainInFileOrder, bool excludeCenterDomain)
        {
            StreamWriter logWriter = new StreamWriter("PF0005_line_log.txt");
            StreamReader coordReader = new StreamReader(pymolCoordFile);
            string line = "";
            string currentChainId = "";
            string preChainId = "";
            int domainCount = 0;
            string chainDomain = "";
            Dictionary<string, Dictionary<string, AtomInfo[]>> domainLigandCoordHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
            List<AtomInfo> chainCoordList = new List<AtomInfo> ();
            bool isHetAtomChain = false;
            bool isPreHetAtomChain = false;
            AtomInfo atom = null;
            // the first domain doesn't have interacting ligands
            // only used for center
            // remove the atom lines
            if (excludeCenterDomain)
            {
                int terSeqId = 0;
                int seqId = 0;
       //         int numOfCenterDomainAtomLines = GetNumOfAtomLinesInCenterDomainFile(centerDomainFile);
                while ((line = coordReader.ReadLine()) != null)
                {                    
                    if (line.IndexOf("TER   ") > -1)
                    {
                        string[] terFields = ParseHelper.ParsePdbTerLine(line);
                        terSeqId = ParseHelper.ConvertSeqToInt(terFields[6]);
                        line = coordReader.ReadLine();
                        if (line.IndexOf("ATOM  ") > -1)
                        {
                            string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                            seqId = ParseHelper.ConvertSeqToInt(atomFields[6]);
                            if (seqId < terSeqId)
                            {
                                atom = new AtomInfo();
                                atom.atomId = Convert.ToInt32(atomFields[1]);
                                atom.atomName = atomFields[2];
                                atom.residue = atomFields[4];
                                atom.seqId = atomFields[6];
                                atom.xyz.X = Convert.ToDouble(atomFields[8]);
                                atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                                atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                                currentChainId = atomFields[5];
                                if (preChainId == "")
                                {
                                    preChainId = currentChainId;
                                }

                                if (currentChainId == preChainId)
                                {
                                    chainCoordList.Add(atom);
                                }
                                isHetAtomChain = false;

                                break;
                            }
                        }
                    }
                }
    //            chainDomain = domainInFileOrder[0];
                domainCount = 1;  // exclude the center domain
            }
            while ((line = coordReader.ReadLine()) != null)
            {
                if (preChainId != currentChainId && preChainId != "")
                {
                    if (!isPreHetAtomChain && preChainId == "A")
                    {                     
                        chainDomain = domainInFileOrder[domainCount];

                        logWriter.WriteLine(domainCount.ToString() + " " + chainDomain + " " + line);
                        logWriter.Flush();

                        AtomInfo[] chainAtoms = new AtomInfo[chainCoordList.Count];
                        chainCoordList.CopyTo(chainAtoms);
                        Dictionary<string, AtomInfo[]> chainCoordHash = new Dictionary<string,AtomInfo[]> ();
                        chainCoordHash.Add(preChainId, chainAtoms);
                        domainLigandCoordHash.Add(chainDomain, chainCoordHash);
                        domainCount++;                        
                    }
                    else
                    {
                        AtomInfo[] ligandAtoms = new AtomInfo[chainCoordList.Count];
                        chainCoordList.CopyTo(ligandAtoms);
                        if (domainLigandCoordHash[chainDomain] != null)
                        {
                            domainLigandCoordHash[chainDomain].Add(preChainId, ligandAtoms);
                        }
                        else
                        {
                            ProtCidSettings.logWriter.WriteLine(domainCount.ToString () + ": " + chainDomain + " no domain interacting ligands, center domain?");
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                    preChainId = currentChainId;
                    chainCoordList.Clear();
                    chainCoordList.Add(atom);
                    isPreHetAtomChain = isHetAtomChain;
                }

                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                    currentChainId = atomFields[5];
                    if (preChainId == "")
                    {
                        preChainId = currentChainId;
                    }

                    if (currentChainId == preChainId)
                    {
                        chainCoordList.Add(atom);
                    }
                    isHetAtomChain = false;
                }
                if (line.IndexOf("HETATM") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                    currentChainId = atomFields[5];
                    if (preChainId == "")
                    {
                        preChainId = currentChainId;
                    }

                    if (currentChainId == preChainId)
                    {
                        chainCoordList.Add(atom);
                    }
                    isHetAtomChain = true;
                }
            }
            coordReader.Close();
            if (chainCoordList.Count > 0)
            {
                AtomInfo[] ligandAtoms = new AtomInfo[chainCoordList.Count];
                chainCoordList.CopyTo(ligandAtoms);
                Dictionary<string, AtomInfo[]> chainCoordHash = domainLigandCoordHash[chainDomain];
                chainCoordHash.Add (currentChainId, ligandAtoms);

                logWriter.WriteLine(chainDomain + " ligand chain: " + currentChainId);
            }
            logWriter.Close();

            return domainLigandCoordHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomainFile"></param>
        /// <returns></returns>
        private string[] ReadCenterDomainFile(string centerDomainFile)
        {
            StreamReader atomLineReader = new StreamReader(centerDomainFile);
            List<string> atomLineList = new List<string> ();
            string line = "";
            while ((line = atomLineReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    atomLineList.Add(line);
                }
            }
            atomLineReader.Close();

            return atomLineList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomainFile"></param>
        /// <returns></returns>
        private int GetNumOfAtomLinesInCenterDomainFile(string centerDomainFile)
        {
            StreamReader atomLineReader = new StreamReader(centerDomainFile);
            int numOfAtomLines = 0;
            string line = "";
            while ((line = atomLineReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    numOfAtomLines++;
                }
            }
            atomLineReader.Close();
            return numOfAtomLines;
        }   
        #endregion

        #region domain RMSD and ligand overlap
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolSupCoordHash"></param>
        /// <param name="domainInteractingLigandsHash"></param>
        private void CountDomainRmsdAndNumOfOverlapLigandAtoms (string pfamId, Dictionary<string, Dictionary<string, AtomInfo[]>> pymolSupCoordHash,
            Dictionary<string, string[]> domainInteractingLigandsHash, Dictionary<string, Range[]> domainResiRangeHash, Dictionary<string, string[][]> domainFileAsymChainMatch)
        {          
            List<string> domainList = new List<string> (pymolSupCoordHash.Keys);
            domainList.Sort();
            string[]  domains = new string[domainList.Count];
            domainList.CopyTo (domains);
            double domainRmsd = 0;
            string ligandNameI = "";
            string ligandAsymChainI = "";
            string ligandNameJ = "";
            string ligandAsymChainJ = "";
            string pdbIdI = "";
            string pdbIdJ = "";
            for (int i = 0; i < domains.Length; i++)
            {
                string[][] fileAsymChainMatchI = (string[][])domainFileAsymChainMatch[domains[i]];
                pdbIdI = domains[i].Substring(0, 4);
                Dictionary<string, AtomInfo[]> domainLigandCoordHashI = pymolSupCoordHash[domains[i]];
                if (domainLigandCoordHashI == null)
                {
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[i] + " domainLigandCoordHashI is null");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                string[] interLigandsI = domainInteractingLigandsHash[domains[i]];
                Range[] domainResiRangesI = domainResiRangeHash[domains[i]];
                Coordinate[] domainICoords = GetDomainCoordinates(domainLigandCoordHashI, domainResiRangesI, interLigandsI);               
                for (int j = i + 1; j < domains.Length; j++)
                {
                    string[][] fileAsymChainMatchJ = (string[][])domainFileAsymChainMatch[domains[j]];
                    pdbIdJ = domains[j].Substring(0, 4);
                    Dictionary<string, AtomInfo[]> domainLigandCoordHashJ = pymolSupCoordHash[domains[j]];
                    if (domainLigandCoordHashJ == null)
                    {
                        ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[j] + " domainLigandCoordHashJ is null");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    string[] interLigandsJ = domainInteractingLigandsHash[domains[j]];
                    Range[] domainResiRangesJ = domainResiRangeHash[domains[j]];
                    Coordinate[] domainJCoords = GetDomainCoordinates(domainLigandCoordHashJ, domainResiRangesJ, interLigandsJ);                   
                    domainRmsd = rmsdCal.CalculateMinRmsd(domainICoords, domainJCoords);                   
                    foreach (string ligandFileChainI in interLigandsI)
                    {
                        AtomInfo[] ligandAtomsI = (AtomInfo[]) domainLigandCoordHashI[ligandFileChainI];
                        if (ligandAtomsI == null)
                        {
                            ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[i] + " " + ligandFileChainI + " ligandAtomsI is null");
                            ProtCidSettings.logWriter.Flush();
                            continue;
                        }
                        ligandAsymChainI = GetAsymChainFromFileChain(ligandFileChainI, fileAsymChainMatchI);
                        ligandNameI = GetLigandChainName(pdbIdI, ligandAsymChainI);
                        foreach (string ligandFileChainJ in interLigandsJ)
                        {
                            AtomInfo[] ligandAtomsJ = (AtomInfo[])domainLigandCoordHashJ[ligandFileChainJ];
                            if (ligandAtomsJ == null)
                            {
                                ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[j] + " " + ligandFileChainJ + " ligandAtomsJ is null");
                                ProtCidSettings.logWriter.Flush();
                                continue;
                            }
                            ligandAsymChainJ = GetAsymChainFromFileChain(ligandFileChainJ, fileAsymChainMatchJ);
                            ligandNameJ = GetLigandChainName(pdbIdJ, ligandAsymChainJ);
                            
                            int[] comAtomNums = GetNumOfLigandOverlapAtoms(ligandAtomsI, ligandAtomsJ);
                            if (comAtomNums[0] > 0)
                            {
                                DataRow dataRow = domainLigandCompTable.NewRow();
                                dataRow["PfamID"] = pfamId;
                                dataRow["PdbID1"] = pdbIdI;
                                dataRow["ChainDomainID1"] = domains[i].Substring(4, domains[i].Length - 4);
                                dataRow["LigandChain1"] = ligandAsymChainI;
                                dataRow["LigandFileChain1"] = ligandFileChainI;
                                dataRow["Ligand1"] = ligandNameI;
                                dataRow["PdbID2"] = pdbIdJ;
                                dataRow["ChainDomainID2"] = domains[j].Substring(4, domains[j].Length - 4);
                                dataRow["LigandChain2"] = ligandAsymChainJ;
                                dataRow["LigandFileChain2"] = ligandFileChainJ;
                                dataRow["Ligand2"] = ligandNameJ;
                                dataRow["DomainRmsd"] = domainRmsd;
                                dataRow["NumComAtoms1"] = comAtomNums[0];
                                dataRow["NumComAtoms2"] = comAtomNums[1];
                                domainLigandCompTable.Rows.Add(dataRow);
                            }
                        }
                    }
                }
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, domainLigandCompTable);
                domainLigandCompTable.Clear();
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileChain"></param>
        /// <param name="fileAsymChainMap"></param>
        /// <returns></returns>
        private string GetAsymChainFromFileChain (string fileChain, string[][] fileAsymChainMap)
        {
            for (int i = 0; i < fileAsymChainMap[0].Length; i ++)
            {
                if (fileAsymChainMap[0][i] == fileChain)
                {
                    return fileAsymChainMap[1][i];
                }
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolSupCoordHash"></param>
        /// <param name="domainInteractingLigandsHash"></param>
        private void CountDomainRmsdAndNumOfOverlapLigandAtoms(string pfamId, Dictionary<string, Dictionary<string, AtomInfo[]>> pymolSupCoordHash, 
            Dictionary<string, string[]> domainInteractingLigandsHash, Dictionary<string, Range[]> domainResiRangeHash, Dictionary<string, string[][]> domainFileAsymChainMatch, string[] updateEntries)
        {
            List<string> domainList = new List<string> (pymolSupCoordHash.Keys);
            domainList.Sort();
            string[] domains = new string[domainList.Count];
            domainList.CopyTo(domains);
            double domainRmsd = 0;
            string ligandNameI = "";
            string ligandAsymChainI = "";
            string ligandNameJ = "";
            string ligandAsymChainJ = "";
            string pdbIdI = "";
            string pdbIdJ = "";
            for (int i = 0; i < domains.Length; i++)
            {
                string[][] fileAsymChainsMatchI = (string[][])domainFileAsymChainMatch[domains[i]];
                pdbIdI = domains[i].Substring(0, 4);
                Dictionary<string, AtomInfo[]> domainLigandCoordHashI = pymolSupCoordHash[domains[i]];
                if (domainLigandCoordHashI == null)
                {
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[i] + " domainLigandCoordHashI is null");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                string[] interLigandsI = domainInteractingLigandsHash[domains[i]];
                Range[] domainRangesI = domainResiRangeHash[domains[i]];
                Coordinate[] domainICoords = GetDomainCoordinates(domainLigandCoordHashI, domainRangesI, interLigandsI);
                for (int j = i + 1; j < domains.Length; j++)
                {
                    string[][] fileAsymChainsMatchJ = (string[][])domainFileAsymChainMatch[domains[j]];
                    pdbIdJ = domains[j].Substring(0, 4);
                    if (Array.IndexOf (updateEntries, pdbIdI) < 0 && Array.IndexOf (updateEntries, pdbIdJ) < 0)
                    {
                        continue;
                    }
                    Dictionary<string, AtomInfo[]> domainLigandCoordHashJ = pymolSupCoordHash[domains[j]];
                    if (domainLigandCoordHashJ == null)
                    {
                        ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[j] + " domainLigandCoordHashJ is null");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    string[] interLigandsJ = domainInteractingLigandsHash[domains[j]];
                    Range[] domainRangesJ = domainResiRangeHash[domains[j]];
                    Coordinate[] domainJCoords = GetDomainCoordinates(domainLigandCoordHashJ, domainRangesJ, interLigandsJ);
                    domainRmsd = rmsdCal.CalculateMinRmsd(domainICoords, domainJCoords);
                    foreach (string ligandFileChainI in interLigandsI)
                    {
                        ligandAsymChainI = GetAsymChainFromFileChain (ligandFileChainI, fileAsymChainsMatchI);
                        AtomInfo[] ligandAtomsI = domainLigandCoordHashI[ligandFileChainI];
                        if (ligandAtomsI == null)
                        {
                            ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[i] + " " + ligandFileChainI + " ligandAtomsI is null");
                            ProtCidSettings.logWriter.Flush();
                            continue;
                        }
                        ligandNameI = GetLigandChainName(pdbIdI, ligandAsymChainI);
                        foreach (string ligandFileChainJ in interLigandsJ)
                        {
                            ligandAsymChainJ = GetAsymChainFromFileChain(ligandFileChainJ, fileAsymChainsMatchJ);
                            ligandNameJ = GetLigandChainName(pdbIdJ, ligandAsymChainJ);
                            AtomInfo[] ligandAtomsJ = domainLigandCoordHashI[ligandFileChainJ];
                            if (ligandAtomsJ == null)
                            {
                                ProtCidSettings.logWriter.WriteLine(pfamId + " " + domains[j] + " " + ligandFileChainJ + " ligandAtomsJ is null");
                                ProtCidSettings.logWriter.Flush();
                                continue;
                            }
                            int[] comAtomNums = GetNumOfLigandOverlapAtoms(ligandAtomsI, ligandAtomsJ);
                            if (comAtomNums[0] > 0)
                            {
                                DataRow dataRow = domainLigandCompTable.NewRow();
                                dataRow["PfamID"] = pfamId;
                                dataRow["PdbID1"] = pdbIdI;
                                dataRow["ChainDomainID1"] = domains[i].Substring(4, domains[i].Length - 4);
                                dataRow["LigandChain1"] = ligandAsymChainI;
                                dataRow["LigandFileChain1"] = ligandFileChainI;
                                dataRow["Ligand1"] = ligandNameI;
                                dataRow["PdbID2"] = pdbIdJ;
                                dataRow["ChainDomainID2"] = domains[j].Substring(4, domains[j].Length - 4);
                                dataRow["LigandChain2"] = ligandAsymChainJ;
                                dataRow["LigandFileChain2"] = ligandFileChainJ;
                                dataRow["Ligand2"] = ligandNameJ;
                                dataRow["DomainRmsd"] = domainRmsd;
                                dataRow["NumComAtoms1"] = comAtomNums[0];
                                dataRow["NumComAtoms2"] = comAtomNums[1];
                                domainLigandCompTable.Rows.Add(dataRow);

                            }
                        }
                    }
                }
            }
            // delete the data rows of update entries
            DeleteUpdateLigandComAtomsData(pfamId, updateEntries);
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, domainLigandCompTable);
            domainLigandCompTable.Clear();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandChain"></param>
        /// <returns></returns>
        private string GetLigandChainName (string pdbId, string ligandChain)
        {
            if (ligandNameTable != null)
            {
                DataRow[] nameRows = ligandNameTable.Select(string.Format ("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, ligandChain));
                if (nameRows.Length > 0)
                {
                    return nameRows[0]["Ligand"].ToString().TrimEnd();
                }
            }
            else
            {
                string queryString = string.Format("Select Ligand From PdbLigands Where PdbID = '{0}' AND AsymChain = '{1}';", pdbId, ligandChain);
                DataTable ligandTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (ligandTable.Rows.Count > 0)
                {
                    return ligandTable.Rows[0]["Ligand"].ToString().TrimEnd();
                }
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainLigandCoordHash"></param>
        /// <param name="ligandChains"></param>
        /// <returns></returns>
        private Coordinate[] GetDomainCoordinates(Dictionary<string, AtomInfo[]> domainLigandCoordHash, Range[] domainResiRanges, string[] ligandChains)
        {
            AtomInfo[] domainAtoms = null;           
            List<Coordinate> coordList = new List<Coordinate> ();
            foreach (string chainId in domainLigandCoordHash.Keys)
            {
                if (Array.IndexOf(ligandChains, chainId) < 0)
                {
                    domainAtoms = (AtomInfo[])domainLigandCoordHash[chainId];
                    foreach (AtomInfo atom in domainAtoms)
                    {
                        if (IsAtomInDomainRange(atom, domainResiRanges))
                        {
                            coordList.Add(atom.xyz);
                        }
                    }
                }
            }
            return coordList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="domainResiRanges"></param>
        /// <returns></returns>
        private bool IsAtomInDomainRange (AtomInfo atom, Range[] domainResiRanges)
        {
            int seqId = ParseHelper.ConvertSeqToInt(atom.seqId);
            foreach (Range resiRange in domainResiRanges)
            {
               if (seqId <= resiRange.endPos && seqId >= resiRange.startPos)
               {
                   return true;
               }               
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandAtoms1"></param>
        /// <param name="ligandAtoms2"></param>
        /// <returns></returns>
        private int[] GetNumOfLigandOverlapAtoms(AtomInfo[] ligandAtoms1, AtomInfo[] ligandAtoms2)
        {
            int[] comAtomsNumbers = new int[3];
            List<int> comAtomList1 = new List<int>();
            List<int> comAtomList2 = new List<int>();
            for (int i = 0; i < ligandAtoms1.Length; i ++)
            {
                for (int j = 0; j < ligandAtoms2.Length; j ++)
                {
                    if (AreAtomsOverlap (ligandAtoms1[i], ligandAtoms2[j]))
                    {
                        if (! comAtomList1.Contains (i))
                        {
                            comAtomList1.Add(i);
                        }
                        if (!  comAtomList2.Contains (j))
                        {
                            comAtomList2.Add(j);
                        }
                    }
                }
            }
            int[] comAtomNumbers = new int[2];
            comAtomNumbers[0] = comAtomList1.Count;
            comAtomNumbers[1] = comAtomList2.Count;
            return comAtomNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom1"></param>
        /// <param name="atom2"></param>
        /// <returns></returns>
        private bool AreAtomsOverlap(AtomInfo atom1, AtomInfo atom2)
        {
            double atomDist = Math.Sqrt(Math.Pow(atom1.xyz.X - atom2.xyz.X, 2) + Math.Pow(atom1.xyz.Y - atom2.xyz.Y, 2) + Math.Pow(atom1.xyz.Z - atom2.xyz.Z, 2));
            if (atomDist < atomOverlapCutoff)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region match ligand chains in file and database
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        public Dictionary<string, string[][]> GetDomainFileAsymChainMatch (string[] domains)
        {
            Dictionary<string, string[][]> domainFileAsymChainMatch = new Dictionary<string, string[][]>();
            string domainFile = "";
            foreach (string domain in domains)
            {
                domainFile = Path.Combine(coordDataDir, domain + ".pfam");
                string[][] fileAsymChainMap = GetDomainFileChainAsymChainMatch (domainFile);
                domainFileAsymChainMatch.Add(domain, fileAsymChainMap);
            }
            return domainFileAsymChainMatch;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFile"></param>
        /// <returns></returns>
        public string[][] GetDomainFileChainAsymChainMatch (string domainFile)
        {
            StreamReader dataReader = new StreamReader(domainFile);
            string line = "";
            int chainLineCount = 1;
            string[] asymChains = null;
            string[] fileChains = null;
            string chainLineRemark = "REMARK   2 ";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.Length < chainLineRemark.Length)
                {
                    continue;
                }
                if (line.IndexOf (chainLineRemark) > -1 && line.IndexOf ("AsymChains") > -1)
                {
                    continue;
                }
                if (line.Substring (0, chainLineRemark.Length) == chainLineRemark)
                {
                    if (chainLineCount == 1)
                    {
                        asymChains = line.Substring(chainLineRemark.Length, line.Length - chainLineRemark.Length).Split(',');
                        chainLineCount++;
                        continue;
                    }
                    if (chainLineCount == 2)
                    {
                        fileChains = line.Substring(chainLineRemark.Length, line.Length - chainLineRemark.Length).Split(',');
                    }
                }
                if (chainLineCount == 2)
                {
                    break;
                }
            }
            dataReader.Close();
            string[][] fileAsymChainsMatch = new string[2][];
            fileAsymChainsMatch[0] = fileChains;
            fileAsymChainsMatch[1] = asymChains;
            return fileAsymChainsMatch;
        }
        #endregion

        #region data table definition
        /// <summary>
        /// create table in database and in memory
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private void CreateTables (bool isUpdate)
        {
            string[] cols = { "PfamID", "PdbID1", "ChainDomainID1", "Ligand1", "LigandChain1", "LigandFileChain1",
                                "PdbID2", "ChainDomainID2", "Ligand2", "LigandChain2", "LigandFileChain2",
                                "DomainRmsd", "NumComAtoms1", "NumComAtoms2" };
            domainLigandCompTable = new DataTable(tableName);
            foreach (string col in cols)
            {
                domainLigandCompTable.Columns.Add(new DataColumn(col));
            }
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string dbCreateTableString = "Create Table " + tableName + " ( " +
                    "PfamID Varchar(40) Not Null, " +
                    "PdbID1 char(4) Not Null, " +
                    "ChainDomainID1 Integer Not Null, " +
                    "Ligand1 char(3) Not Null, " +
                    "LigandChain1 char(3) Not Null, " +
                    "LigandFileChain1 char(3) Not Null, " +
                    "PdbID2 char(4) Not Null, " +
                    "ChainDomainID2 Integer Not Null, " +
                    "Ligand2 char(3) Not Null, " +                    
                    "LigandChain2 char(3) Not Null, " +
                    "LigandFileChain2 char(3) Not Null, " +
                    "DomainRmsd Float Not Null, " +
                    "NumComAtoms1 Integer Not Null, " + 
                    "NumComAtoms2 Integer Not Null);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, dbCreateTableString, tableName);
                string indexString = "Create Index PfamLigandComp_pfam on " + tableName + " (PfamID)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComp_pdb on " + tableName + "(PdbID1, PdbID2)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComp_pdb1 on " + tableName + "(PdbID1, ChainDomainID1)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComp_pdb2 on " + tableName + "(PdbID2, ChainDomainID2)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void SetPdbLigandNameTable ()
        {
            string queryString = "Select PdbID, Ligand, AsymChain From PdbLigands;";
            ligandNameTable = ProtCidSettings.protcidQuery.Query( queryString); 
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetPfamIdAccMap ()
        {
            string queryString = "Select Distinct Pfam_ID, Pfam_Acc From PdbPfam;";
            pfamIdAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private string GetPfamIdFromPfamAcc (string pfamAcc)
        {
            string pfamId = "";
            if (pfamIdAccTable != null)
            {
                DataRow[] pfamIdRows = pfamIdAccTable.Select(string.Format ("Pfam_Acc = '{0}'", pfamAcc));
                if (pfamIdRows.Length > 0)
                {
                    pfamId = pfamIdRows[0]["Pfam_ID"].ToString().TrimEnd();
                }
            }
            return pfamId;
        }
        #endregion

        #region for debug and test
        public void FindNullHash(string[] domains, Dictionary<string, Dictionary<string, AtomInfo[]>> domainLigandCoordHash, bool excludeCenterDomain)
        {
            int i = 0;
            if (excludeCenterDomain)
            {
                i = 1;
            }
            for (; i < domains.Length; i++)
            {
                if (domainLigandCoordHash[domains[i]] == null)
                {
                    ProtCidSettings.logWriter.WriteLine(domains[i] + " chainCoordHash is null");
                }
            }
            ProtCidSettings.logWriter.Flush();
        }
        #endregion
    }
}
