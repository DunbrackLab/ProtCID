using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using BuCompLib.BuInterfaces;
using BuCompLib.EntryBuComp;
using BuCompLib.BamDb;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.Settings;

namespace BuCompLib
{
    /// <summary>
    /// BU means BA
    /// it was biological unit, now, the name is changed into biological assembly
    /// </summary>
    public class BuCompBuilder
    {
        #region member variables
        public static string BuType = "";
        public static StreamWriter logWriter = null;
        private DbQuery dbQuery = new DbQuery();
        public static string[] buTypes = { "asu", "pdb", "pisa" };
  //      public static string[] buTypes = {"pdb"};
        #endregion

        public BuCompBuilder ()
        {
            Initialize();
        }

        #region retrieve chain-chain and domain-domain interfaces of each BU
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        public void FindInterfacesInBUs(bool isUpdate)
        {           
            ProtCidSettings.buCompConnection.ConnectToDatabase();
            EntryBuInterfaces buInterfaces = new EntryBuInterfaces();
            logWriter = new StreamWriter("BuCompBuilderLog.txt", true);

 //           BuCompTables.InitializeTables();

            if (!isUpdate)
            {
   //             BuCompTables.InitializeDbTables ();
            }

            if (!isUpdate)
            {
                buInterfaces.GetBuInterfaces();
            }
            else
            {
                string[] updateEntries = GetUpdateEntries();
                string[] updateBuTypes = {"asu", "pdb", "pisa"};
                //          string[] updateEntries = null;
                //       string[] updateEntries = GetEntriesMissingChainInterfaces();
                //         string[] updateEntries = { "3mtj" };
                foreach (string updateBuType in buTypes)
                //       foreach (string updateBuType in updateBuTypes)
                {
                    //        updateEntries = GetEntriesWithMissingInterfaces(updateBuType);
                    //          logWriter.WriteLine(updateBuType);
                    BuType = updateBuType;
                    BuCompTables.InitializeTables();
                    buInterfaces.UpdateBuInterfaces(updateEntries);
                }
            }
            ProtCidSettings.buCompConnection.DisconnectFromDatabase();

            logWriter.Close();
            logWriter = null;
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        #endregion

        #region retrieve domain-domain interfaces in BAs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        public void FindDomainInterfacesInBAs(bool isUpdate)
        {
            ProtCidSettings.buCompConnection.ConnectToDatabase();

            if (!isUpdate)
            {
    //            BuCompTables.InitializeDomainDbTables ();  // should be really careful about drop the tables
            }
            EntryBuInterfaces buInterfaces = new EntryBuInterfaces();
            if (isUpdate)
            {
      /*          buType = "pisa";
                dataType = "domain";
                string[] updateEntries = GetEntriesWithMissingInterfaces (buType, dataType);           
                buInterfaces.UpdateBADomainInterfaces(updateEntries, buType);*/

                string[] updateEntries = GetUpdateEntries();
    //             string[] updateEntries = GetEntriesMissingChainInterfaces ();
   //            string[] updateEntries = GetRibosomalEntries();
                buInterfaces.UpdateBADomainInterfaces(updateEntries);
            }
            else
            {
                buInterfaces.RetrieveBADomainInterfaces ();
            }
            
            ProtCidSettings.buCompConnection.DisconnectFromDatabase();

            logWriter.Close();
            logWriter = null;
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetSplitCombDomainEntries()
        {
            string queryString = "Select Distinct PdbID From PdbPfam Where DomainType = 's' or DomainType = 'c'";
            DataTable splitEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in splitEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                entryList.Add(pdbId);
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        #endregion

        #region compare domain interfaces in BAs of a relation
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        public void CompareDomainInterfacesInBAs()
        {
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath;
                ProtCidSettings.alignmentDbConnection.ConnectToDatabase();
            }

            ProtCidSettings.buCompConnection.ConnectToDatabase();

            HomoBuComp.BuDomainInterfaceComp domainInterfaceComp = new BuCompLib.HomoBuComp.BuDomainInterfaceComp();
            domainInterfaceComp.CompareBuDomainInterfaces();
            //   domainInterfaceComp.CompareBuDomainInterfaces(31);

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            logWriter.Close();
            logWriter = null;
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateCompareDomainInterfacesInBUs()
        {
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath;
                ProtCidSettings.alignmentDbConnection.ConnectToDatabase();
            }

            ProtCidSettings.buCompConnection.ConnectToDatabase();

            HomoBuComp.BuDomainInterfaceComp domainInterfaceComp = new BuCompLib.HomoBuComp.BuDomainInterfaceComp();

            string[] updateEntries = GetUpdateEntries();
  //          string[] updateBuTypes = { "pdb", "pisa", "asu" };
            foreach (string updateBuType in buTypes)
            {
                BuType = updateBuType;
                domainInterfaceComp.UpdateCompareBuDomainInterfaces(updateEntries);
            }

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            logWriter.Close();
            logWriter = null;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        #endregion

        #region compare BUs of an entry
        /// <summary>
        /// 
        /// </summary>
        public void CompareEntryBiolAssemblies()
        {
            ProtCidSettings.buCompConnection.ConnectToDatabase();

            EntryBuComp.EntryBuComp buComp = new BuCompLib.EntryBuComp.EntryBuComp();
            buComp.CompareEntryBUs();

            // print bu comp info to text files
            // not necessary to print to text file
            // directly use the table in bu comp
            PrintBuCompInfoToFiles();

            // build BAM database
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Build ProtBudBiolAssemblies table ");
            ProtBudBiolAssemblies protbudBa = new ProtBudBiolAssemblies();
            protbudBa.RetrieveProtBudBiolAssemblies ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Build BAM database from Pdbfam");
            BamDbUpdate bamDbUpdate = new BamDbUpdate();
            bamDbUpdate.BuildBamDatabase ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Build BAM db done!");

            logWriter.Close();
            logWriter = null;

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            ProtCidSettings.progressInfo.threadFinished = true;
        }    

        /// <summary>
        /// 
        /// </summary>
        public void UpdateComparingEntryBiolAssembliess()
        {
            ProtCidSettings.buCompConnection.ConnectToDatabase();

            string[] updateEntries = GetUpdateEntries();
 //           string[] updateEntries = GetEntriesMissingChainInterfaces();
            EntryBuComp.EntryBuComp buComp = new BuCompLib.EntryBuComp.EntryBuComp();
            buComp.UpdateComparingEntryBUs(updateEntries);
            
            // not necessary to print data to text file,
            // directly use the table in bucomp database
            PrintUpdatedBuCompInfoToFiles(updateEntries);

            // update BAM database
            ProtBudBiolAssemblies protbudBa = new ProtBudBiolAssemblies();
            protbudBa.UpdateProtBudBiolAssemblies(updateEntries);

            BamDbUpdate bamDbUpdate = new BamDbUpdate();
            bamDbUpdate.UpdateBamDatabase(updateEntries);

            logWriter.Close();
            logWriter = null;

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// Output bucomp data to files
        /// </summary>
        public void PrintBuCompInfoToFiles()
        {
            string buCompFile = Path.Combine(ProtCidSettings.dirSettings.piscesPath, "bucomp");
            if (!Directory.Exists(ProtCidSettings.dirSettings.piscesPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.piscesPath);
            }
            StreamWriter buCompWriter = new StreamWriter(buCompFile);
            string queryString = "Select * From PdbPisaBuComp;";
            DataTable buCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string line = "";
            foreach (DataRow buCompRow in buCompTable.Rows)
            {
                line = "";
                foreach (object item in buCompRow.ItemArray)
                {
                    line += (item.ToString() + " ");
                }
                buCompWriter.WriteLine(line.TrimEnd());
            }
            buCompWriter.Close();

            string buInterfaceCompFile = Path.Combine(ProtCidSettings.dirSettings.piscesPath, "buinterfacecomp");
            StreamWriter buInterfaceCompWriter = new StreamWriter(buInterfaceCompFile);
            queryString = "Select * From PdbPisaBuInterfaceComp;";
            DataTable buInterfaceCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            foreach (DataRow buInterfaceCompRow in buInterfaceCompTable.Rows)
            {
                line = "";
                foreach (object item in buInterfaceCompRow.ItemArray)
                {
                    line += (item.ToString() + " ");
                }
                buInterfaceCompWriter.WriteLine(line.TrimEnd());
            }
            buInterfaceCompWriter.Close();
            ParseHelper.ZipPdbFile(buCompFile);
            ParseHelper.ZipPdbFile(buInterfaceCompFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatedEntries"></param>
        public void PrintUpdatedBuCompInfoToFiles(string[] updatedEntries)
        {
            string buCompFile = Path.Combine(ProtCidSettings.dirSettings.piscesPath, "newbucomp");
            if (!Directory.Exists(ProtCidSettings.dirSettings.piscesPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.piscesPath);
            }
            StreamWriter buCompWriter = new StreamWriter(buCompFile);
            string queryString = "";
            string line = "";
            foreach (string updateEntry in updatedEntries)
            {
                queryString = string.Format("Select * From PdbPisaBuComp Where PdbID = '{0}';", updateEntry);
                DataTable buCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

                foreach (DataRow buCompRow in buCompTable.Rows)
                {
                    line = "";
                    foreach (object item in buCompRow.ItemArray)
                    {
                        line += (item.ToString() + " ");
                    }
                    buCompWriter.WriteLine(line.TrimEnd());
                }
            }
            buCompWriter.Close();

            string buInterfaceCompFile = Path.Combine(ProtCidSettings.dirSettings.piscesPath, "newbuinterfacecomp");
            StreamWriter buInterfaceCompWriter = new StreamWriter(buInterfaceCompFile);
            foreach (string entry in updatedEntries)
            {
                queryString = string.Format("Select * From PdbPisaBuInterfaceComp Where PdbID = '{0}';", entry);
                DataTable buInterfaceCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
                foreach (DataRow buInterfaceCompRow in buInterfaceCompTable.Rows)
                {
                    line = "";
                    foreach (object item in buInterfaceCompRow.ItemArray)
                    {
                        line += (item.ToString() + " ");
                    }
                    buInterfaceCompWriter.WriteLine(line.TrimEnd());
                }
            }
            buInterfaceCompWriter.Close();
            ParseHelper.ZipPdbFile(buCompFile);
            ParseHelper.ZipPdbFile(buInterfaceCompFile);
        }

        #endregion

        #region group BUs based on PFAM contents
        /// <summary>
        /// 
        /// </summary>
        public void GroupBUsOnPfam()
        {
            ProtCidSettings.buCompConnection.ConnectToDatabase();
            HomoBuComp.PfamBuClassifier buClassifier = new BuCompLib.HomoBuComp.PfamBuClassifier();
            //   buClassifier.FindMissingPfamBuEntries();
            buClassifier.GetBuDomainContents();

            logWriter.Close();
            logWriter = null;

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        public void UpdateGroupingBUsOnPfam()
        {
            string[] updateEntries = GetUpdateEntries();
       //     string[] updateEntries = GetUpdatePisaEntries();
//            string[] updateEntries = GetUpdateNmrEntries();

            ProtCidSettings.buCompConnection.ConnectToDatabase();
            string[] updateBuTypes = { "pdb", "pisa"};
       //     string[] updateBuTypes = { "pisa" };
            HomoBuComp.PfamBuClassifier buClassifier = new BuCompLib.HomoBuComp.PfamBuClassifier();
            //   buClassifier.FindMissingPfamBuEntries();
            foreach (string updateBuType in updateBuTypes)
            {
                BuType = updateBuType;
                buClassifier.UpdateBuDomainContents(updateEntries);
            }

            logWriter.Close();
            logWriter = null;

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
        }
        #endregion

        #region chain-peptide interaction
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainPeptideInterfacesInBAs()
        {
            BuType = "pdb";

            ProtCidSettings.buCompConnection.ConnectToDatabase();

            PfamInteract.ChainPeptideInterfaces chainPeptideInterface = new BuCompLib.PfamInteract.ChainPeptideInterfaces();
            chainPeptideInterface.RetrieveChainPeptideInterfaces();

            PfamInteract.ChainDnaRna chainDnaRnaInteract = new BuCompLib.PfamInteract.ChainDnaRna();
            chainDnaRnaInteract.RetrieveProtDnaRnaInteractions();

            PfamInteract.ChainLigands chainLigandInteract = new BuCompLib.PfamInteract.ChainLigands();
            chainLigandInteract.RetrieveChainLigandInteractions();                     

            logWriter.Close();
            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateChainPeptideInterfacesInBAs()
        {
            BuType = "pdb";

            ProtCidSettings.buCompConnection.ConnectToDatabase();

            string[] updateEntries = GetUpdateEntries();
    //       string[] updateEntries = GetMissingPeptideEntries();
  //           string[] updateEntries = GetMissingLigandEntries();

            PfamInteract.ChainPeptideInterfaces chainPeptideInterface = new BuCompLib.PfamInteract.ChainPeptideInterfaces();
            chainPeptideInterface.UpdateChainPeptideInterfaces(updateEntries);

            PfamInteract.ChainDnaRna chainDnaRna = new BuCompLib.PfamInteract.ChainDnaRna();
            chainDnaRna.UpdateProtDnaRnaInteractions(updateEntries);

            updateEntries = GetUpdateEntries(@"D:\Qifang\ProjectData\DbProjectData\PDB\updateEntries_ligandsDif.txt");
            PfamInteract.ChainLigands chainLigand = new BuCompLib.PfamInteract.ChainLigands();
            chainLigand.UpdateChainLigandInteractions (updateEntries);
                       
            logWriter.Close();
            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
        }

//        private string[] GetMissingDnaRnaEntries ()
        private string[] GetMissingLigandEntries ()
        {
 //           string queryString = "Select Distinct PdbID From ChainDnaRnas;";
            string queryString = "Select Distinct PdbID From ChainLigands;";
            DataTable entryTable = ProtCidSettings.buCompQuery.Query(queryString);
  //          queryString = "Select Distinct PdbID From AsymUnit Where polymertype in ('polydeoxyribonucleotide', 'polyribonucleotide');";
            queryString = "Select Distinct PdbID From PdbLigands Where Ligand not in ('DNA', 'RNA');";
            DataTable asuEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
  //          DataTable asuEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<string> missEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in  asuEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataRow[] dnaRows = entryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                if (dnaRows.Length > 0)
                {
                    continue;
                }
                if (IsEntryProtein (pdbId))
                {
                    missEntryList.Add(pdbId);
                }
            }

            return missEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryProtein (string pdbId)
        {
            string queryString = string.Format("Select PdbID, AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (protChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
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
            if (Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.dataType = "pfam";

            // connect to Pdbfam database
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
                ProtCidSettings.protcidQuery = new DbQuery (ProtCidSettings.protcidDbConnection);
            }

            // used to connect to BuComp Database
            if (ProtCidSettings.buCompConnection == null)
            {
                ProtCidSettings.buCompConnection = new DbConnect();
                ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                   ProtCidSettings.dirSettings.baInterfaceDbPath;
                ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            }

            

            if (logWriter == null)
            {
                logWriter = new StreamWriter("BuCompLibLog.txt", true);
                logWriter.WriteLine(DateTime.Today.ToShortDateString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            string newlsFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt");
            //         string newlsFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb_pfamUpdate.txt");
            StreamReader dataReader = new StreamReader(newlsFile);
            List<string> entryList = new List<string> ();
            string line = "";
            string pdbId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                pdbId = line.Substring(0, 4).ToLower();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            dataReader.Close();
            string[] pdbIds = new string[entryList.Count];
            entryList.CopyTo(pdbIds);
            return pdbIds;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries(string lsFile)
        {
            StreamReader dataReader = new StreamReader(lsFile);
            List<string> entryList = new List<string> ();
            string line = "";
            string pdbId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                pdbId = line.Substring(0, 4).ToLower();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            dataReader.Close();
            string[] pdbIds = new string[entryList.Count];
            entryList.CopyTo(pdbIds);
            return pdbIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetMissingPeptideEntries()
        {
            string entryFile = "MissingPepEntries.txt";
            List<string> missingPepEntryList = new List<string>();

            string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, List<string>> entryChainListDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> entryPepChainListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string sequence = "";
            foreach (DataRow entryRow in asuEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                sequence = entryRow["Sequence"].ToString();
                if (entryChainListDict.ContainsKey (pdbId))
                {
                    entryChainListDict[pdbId].Add(entryRow["AsymID"].ToString ().TrimEnd ());
                }
                else
                {
                    List<string> chainList = new List<string>();
                    chainList.Add(entryRow["AsymID"].ToString().TrimEnd());
                    entryChainListDict.Add(pdbId, chainList);
                }
                if (sequence.Length < 50)
                {
                    if (entryPepChainListDict.ContainsKey(pdbId))
                    {
                        entryPepChainListDict[pdbId].Add(entryRow["AsymID"].ToString().TrimEnd());
                    }
                    else
                    {
                        List<string> pepChainList = new List<string>();
                        pepChainList.Add(entryRow["AsymID"].ToString().TrimEnd());
                        entryPepChainListDict.Add(pdbId, pepChainList);
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter(entryFile);

            foreach (string pepPdbId in entryPepChainListDict.Keys)
            {
                if (!IsPeptideInterfaceExistOrValide(pepPdbId, entryChainListDict[pepPdbId]))
                {
                    missingPepEntryList.Add(pepPdbId);
                    dataWriter.WriteLine(pepPdbId);
                }
            }
            dataWriter.Close();

            return missingPepEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool HasEntryPeptides(string pdbId, out List<string> protChainList)
        {
            string queryString = string.Format("Select AsymID, AuthorChain, EntityID, Sequence From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string sequence = "";
            int seqLength = 0;
            protChainList = new List<string>();
            foreach (DataRow infoRow in entryInfoTable.Rows)
            {
                protChainList.Add(infoRow["AsymID"].ToString().TrimEnd());
                sequence = infoRow["Sequence"].ToString().TrimEnd();
                seqLength = sequence.Length;

                if (seqLength <= 50)
                {
                    return true;
                }

            }
            return false;
        }

        private bool IsPeptideInterfaceExistOrValide(string pdbId, List<string> asuAsymChainList)
        {
            string querystring = string.Format("Select InterfaceID, AsymChain, PepAsymChain From ChainPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pepInterfaceTable = ProtCidSettings.buCompQuery.Query(querystring);
            if (pepInterfaceTable.Rows.Count == 0)
            {
                return false;
            }
            string chainId = "";
            foreach (DataRow pepRow in pepInterfaceTable.Rows)
            {
                chainId = pepRow["AsymChain"].ToString().TrimEnd();
                if (!asuAsymChainList.Contains(chainId))
                {
                    return false;
                }
            }
            return true;
        }

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void UpdateMissingEntryBiolAssemblies ()
        {
            ProtCidSettings.buCompConnection.ConnectToDatabase();

            string[] updateEntries = GetEntriesMissingProtBudBiolAssem ();
 /*           string[] updateEntries = GetEntriesMissingChainInterfaces();
            EntryBuComp.EntryBuComp buComp = new BuCompLib.EntryBuComp.EntryBuComp();
            buComp.UpdateComparingEntryBUs(updateEntries);

            // not necessary to print data to text file,
            // directly use the table in bucomp database
            PrintUpdatedBuCompInfoToFiles(updateEntries);

            // update BAM database protbudbiolassemblies on Jan. 3, 2019
            ProtBudBiolAssemblies protbudBa = new ProtBudBiolAssemblies();
            protbudBa.UpdateProtBudBiolAssemblies(updateEntries);
*/
            BamDbUpdate bamDbUpdate = new BamDbUpdate();
            bamDbUpdate.UpdateBamDatabase(updateEntries);

            logWriter.Close();
            logWriter = null;

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        private string[] tableNames = { "PDBPFAMBUDOMAININTERFACES", /*"ASUPFAMDOMAININTERFACES", "ASUINTRADOMAININTERFACES", */"PISAPFAMBUDOMAININTERFACES" };
        private string[] chainTableNames = {"PdbBuInterfaces", "PisaBuInterfaces"};

        private string[] GetEntriesMissingProtBudBiolAssem ()
        {
            List<string> missingBaEntryList = new List<string>();

            string queryString = "Select Distinct PdbID From ProtBudBiolAssemblies;";
            DataTable baEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
             List<string> baEntryList = new List<string>();
            foreach (DataRow entryRow in baEntryTable.Rows)
            {
                baEntryList.Add(entryRow["PdbID"].ToString());
            }
            baEntryList.Sort();

            queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            
            string pdbId = "";
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
               if (baEntryList.BinarySearch (pdbId) < 0)
               {
                   missingBaEntryList.Add (pdbId);
               }
            }
            return missingBaEntryList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesMissingChainInterfaces ()
        {
            List<string> missingEntryList = new List<string>();
            if (File.Exists("EntriesMissingChainInterfaces.txt"))
            {
                StreamReader dataReader = new StreamReader("EntriesMissingChainInterfaces.txt");
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    missingEntryList.Add(line);
                }
                dataReader.Close();
            }
            else
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
            return missingEntryList.ToArray ();
        }
        /// <summary>
        /// for ribosomal structures
        /// </summary>
        /// <returns></returns>
        private string[] GetRibosomalEntries ()
        {
            string queryString = "Select Distinct PdbID From PdbPfam Where Pfam_ID like 'Ribosom%';";
            StreamWriter entryWriter = new StreamWriter("UpdateChainRibosomeEntries.txt");
            DataTable ribosomEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> ribosomeEntryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in ribosomEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
  //              if (IsEntryAsuBAInterfacesExist(pdbId))
                if (IsEntryAsuBAChainInterfacesExist (pdbId))
                {
                    continue;
                }

                ribosomeEntryList.Add(pdbId);
                entryWriter.WriteLine(pdbId);
            }
            entryWriter.Close();
            return ribosomeEntryList.ToArray ();
        }

        private bool IsEntryAsuBAChainInterfacesExist(string pdbId)
        {
            string queryString = "";
            foreach (string tableName in chainTableNames)
            {
                queryString = string.Format("Select PdbID From {0} Where PdbID = '{1}';", tableName, pdbId);
                DataTable interfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
                if (interfaceTable.Rows.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        
        public string[] CombineUpdateEntries ()
        {
            string[] entryFiles = {/*"EntriesWithoutBuInterfaces_asu.txt",*/ "EntriesWithoutBuInterfaces_pdb.txt", "EntriesWithoutBuInterfaces_pisa.txt"};
            List<string> entryList = new List<string> ();
            StreamWriter leftEntryWriter = new StreamWriter("LeftChainBAinterfacesEntries.txt");
            foreach (string entryFile in entryFiles)
            {
                StreamReader entryReader = new StreamReader(entryFile);
                string line = "";
                while ((line = entryReader.ReadLine ()) != null)
                {
                    if (IsEntryAsuBAInterfacesExist(line))
                    {
                        if (!entryList.Contains(line))
                        {
                            entryList.Add(line);
                            leftEntryWriter.WriteLine(line);
                        }
                    }
                }
                entryReader.Close();
            }
            leftEntryWriter.Close();
            return entryList.ToArray ();
        }

        private bool IsEntryAsuBAInterfacesExist (string pdbId)
        {
            string queryString = "";
            foreach (string tableName in tableNames)
            {
                queryString = string.Format("Select PdbID From {0} Where PdbID = '{1}';", tableName, pdbId);
                DataTable interfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
                if (interfaceTable.Rows.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingEntriesFromLogFile()
        {
      //      string logFile = @"BuInterfaceRetrieverLog0.txt";
            string logFile = @"PdbBuCompBuilderLog.txt";
            StreamReader dataReader = new StreamReader(logFile);
            string line = "";
            List<string> missingEntryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
            //    if (line.IndexOf("Item has already been added. Key in dictionary: ") > -1)
                if (line.IndexOf("Length cannot be less than zero.") > -1)
                {
                    string[] fields = line.Split(' ');
                    missingEntryList.Add(fields[1]);
                }
            }
            dataReader.Close();
            string[] missingEntries = new string[missingEntryList.Count];
            missingEntryList.CopyTo(missingEntries);
            return missingEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string[] GetEntriesWithMissingInterfaces(string type, string domainOrChain)
        {
            List<string> entryList = new List<string> ();
            string fileName = "";
            if (domainOrChain == "chain")
            {
                fileName = "EntriesWithoutBuInterfaces_" + type + ".txt";
            }
            else
            {
                fileName = "EntriesWithoutBuDomainInterfaces_" + type + ".txt";
            }
            if (File.Exists(fileName))
            {
                StreamReader dataReader = new StreamReader(fileName);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    entryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                string pdbId = "";
                StreamWriter dataWriter = new StreamWriter(fileName);
                string queryString = "";
                string tableName = "";
                if (type == "asu")
                {
                    if (domainOrChain == "chain")
                    {
                        tableName = "AsuInterfaces";
                    }
                    else
                    {
                        tableName = "AsuPfamDomainInterfaces";
                    }
                }
                else
                {
                    if (domainOrChain == "chain")
                    {
                        tableName = type + "BuInterfaces";
                    }
                    else
                    {
                        tableName = type + "PfamBuDomainInterfaces";
                    }
                }
                if (domainOrChain == "chain")
                {
                    queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
                }
                else
                {
                    queryString = "Select Distinct PdbID From PdbPfam;";
                }
                DataTable protEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                foreach (DataRow entryRow in protEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    if (type == "asu")
                    {
                        if (domainOrChain == "chain")
                        {
                            if (IsEntryAsuMonomer(pdbId))
                            {
                                continue;
                            }
                        }
                    }
                    else if (type == "pdb")
                    {
                        if (IsEntryPdbBuMonomer (pdbId))
                        {
                            continue;
                        }
                    }
                    else if (type == "pisa")
                    {
                        if (IsEntryPisaBuMonomer (pdbId))
                        {
                            continue;
                        }
                    }
                    if (!IsEntryBuInterfaceExist(pdbId, tableName))
                    {
                        entryList.Add(pdbId);
                        dataWriter.WriteLine(pdbId);
                    }
                }
                dataWriter.Close();
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        private bool IsEntryAsuMonomer (string pdbId)
        {
            string queryString = string.Format ("Select AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (protEntryTable.Rows.Count > 1)
            {
                return false;
            }
            return true;
        }
        
        private bool IsEntryPdbBuMonomer (string pdbId)
        {
            string queryString = string.Format("Select BiolUnitID From PdbBuStat Where PdbID = '{0}' AND " + 
                " Details like 'author_%' AND Oligomeric_details <> 'monomeric';", pdbId);
            DataTable buIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (buIdTable.Rows.Count > 0)
            {
                return false;
            }
            queryString = string.Format("Select BiolUnitID From PdbBuStat Where PdbID = '{0}' AND " +
                " Oligomeric_details <> 'monomeric';", pdbId);
            buIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (buIdTable.Rows.Count > 0)
            {
                return false;
            }
            return true;
        }

        private bool IsEntryPisaBuMonomer(string pdbId)
        {
            string queryString = string.Format("Select AssemblySeqID From PisaAssembly Where PdbID = '{0}' AND Formula_ABC <> 'A';", pdbId);
            DataTable buIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (buIdTable.Rows.Count > 0)
            {
                return false;
            }
           
            return true;
        }

        private bool IsEntryBuInterfaceExist(string pdbId, string tableName)
        {  
           string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", tableName, pdbId);
            DataTable buInterfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
            if (buInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            if (tableName.ToUpper() == "ASUPFAMDOMAININTERFACES")
            {
                queryString = string.Format("Select * From AsuIntraDomainInterfaces Where PdbID = '{0}';", pdbId);
                buInterfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
                if (buInterfaceTable.Rows.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntriesWithAuthSoftware()
        {
            string queryString = "Select PdbID, count(distinct biolunitid) as buCount From PdbBuStat" + 
                " Group By PdbID;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            int buCount = 0;
            string[] softwareBuIds = null;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                buCount = Convert.ToInt32(entryRow["BuCount"].ToString ());
                if (buCount == 1)
                {
                    continue;
                }
                if (IsEntryAuthSoftwareMixed(pdbId, out softwareBuIds))
                {
                    if (AreSoftwareBuInPdbBuInterfaces(pdbId, softwareBuIds))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryAuthSoftwareMixed(string pdbId, out string[] softwareBuIds)
        {
            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            bool hasAuthor = false;
            bool hasSoftware = false;
            List<string> softwareBuList = new List<string> ();
            string details = "";
            foreach (DataRow buStatRow in buStatTable.Rows)
            {
                details = buStatRow["Details"].ToString().TrimEnd().ToLower ();
                if (details.IndexOf("author_") > -1)
                {
                    hasAuthor = true;
                }
                else if (details.IndexOf("software_") > -1)
                {
                    hasSoftware = true;
                    softwareBuList.Add(buStatRow["BiolUnitID"].ToString ().TrimEnd ());
                }
            }
            softwareBuIds = new string[softwareBuList.Count];
            softwareBuList.CopyTo(softwareBuIds);
            if (hasAuthor && hasSoftware)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="softwareBuIds"></param>
        /// <returns></returns>
        private bool AreSoftwareBuInPdbBuInterfaces(string pdbId, string[] softwareBuIds)
        {
            string queryString = string.Format("Select * From PdbBuInterfaces " + 
                " Where PdbID = '{0}' AND BUID IN ({1});", pdbId, ParseHelper.FormatSqlListString (softwareBuIds));
            DataTable interfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
            if (interfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
  /*      private string[] GetUpdatePisaEntries()
        {
            StreamReader dataReader = new StreamReader("WrongAsymChainPisaEntriex.txt");
            string line = "";
            ArrayList entryList = new ArrayList();
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (!entryList.Contains(line.Substring(0, 4)))
                {
                    entryList.Add(line.Substring(0, 4));
                }
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }*/

        private string[] GetUpdatePisaEntries()
        {
            StreamReader entryReader = new StreamReader("PisaEntryNotFound.txt");
            string line = "";
            List<string> updatePisaEntryList = new List<string> ();
            while ((line = entryReader.ReadLine()) != null)
            {
                if (IsEntryNotFound(line))
                {
                    continue;
                }
                if (line == "2zzq")
                {
                    continue;
                }
                updatePisaEntryList.Add(line);
            }
            entryReader.Close();
            string queryString = string.Format("Select Distinct PdbID from PisaBuMatrix;");
            string[] updatePisaEntries = new string[updatePisaEntryList.Count];
            updatePisaEntryList.CopyTo(updatePisaEntries);
            return updatePisaEntries;
        }

        private bool IsEntryNotFound(string pdbId)
        {
            string queryString = string.Format("Select * From PisaBuStatus Where PdbID = '{0}';", pdbId);
            DataTable buStatusTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string buStatus = buStatusTable.Rows[0]["Status"].ToString().TrimEnd();
            if (buStatus == "Entry not found")
            {
                return true;
            }
            return false;
        }

        private string[] GetPisaEntriesWithMessedAsymChains()
        {
            List<string> entryList = new List<string> ();
       /*     StreamReader dataReader = new StreamReader("PisaEntriesWithMessyAsymChains.txt");
            
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line);
            }
            dataReader.Close();
*/
            StreamWriter dataWriter = new StreamWriter("PisaEntriesWithMessyAsymChains.txt");
            string queryString = "Select Distinct PdbID From PisaBuMatrix Where AsymChain = '-';";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query (queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entryList.Add (entryRow["PdbID"].ToString ());
                dataWriter.WriteLine(entryRow["PdbID"].ToString ());
            }
            dataWriter.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateNmrEntries()
        {
            string queryString = "Select PdbID From PdbEntry Where Method like '%NMR%';";
            DataTable nmrEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> nmrEntryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow nmrEntryRow in nmrEntryTable.Rows)
            {
                pdbId = nmrEntryRow["PdbID"].ToString();
                if (IsEntryBuInterfaceExist(pdbId))
                {
                    continue;
                }
                if (IsNmrEntryMonomer(pdbId))
                {
                    continue;
                }
                nmrEntryList.Add(pdbId);
            }
            string[] nmrEntries = new string[nmrEntryList.Count];
            nmrEntryList.CopyTo(nmrEntries);
            return nmrEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryBuInterfaceExist(string pdbId)
        {
            string queryString = string.Format("Select * From PdbBuInterfaces Where PdbId = '{0}';", pdbId);
            DataTable buInterfaceTable = ProtCidSettings.buCompQuery.Query(queryString);
            if (buInterfaceTable.Rows.Count > 0)
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
        private bool IsNmrEntryMonomer(string pdbId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit " + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable chainTable = ProtCidSettings.pdbfamQuery.Query (queryString);
            if (chainTable.Rows.Count > 1)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissedEntries()
        {
            List<string> entryList = new List<string> ();
            string line = "";
            StreamReader dataReader = new StreamReader("EntriesWithoutBuInterfaces_pdb.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line);
            }
            dataReader.Close();
            dataReader = new StreamReader("EntriesWithoutBuInterfaces_pisa.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                if (! entryList.Contains(line))
                {
                    entryList.Add(line);
                }
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        #endregion
    }
}
