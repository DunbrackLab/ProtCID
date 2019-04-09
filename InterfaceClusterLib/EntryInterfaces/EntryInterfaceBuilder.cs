using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using ProtCidSettingsLib;
using InterfaceClusterLib.stat;
using InterfaceClusterLib.InterfaceComp;
using InterfaceClusterLib.Clustering;
using InterfaceClusterLib.EntryInterfaces;
using InterfaceClusterLib.InterfaceProcess;
using InterfaceClusterLib.AuxFuncs;
using DbLib;

namespace InterfaceClusterLib.EntryInterfaces
{
    /* Q. Xu, May 13, 2016
     * this class is similar to PfamInterfaceBuilder, but modify to fit into new project structure
     * */
    public class EntryInterfaceBuilder
    {      
        /// <summary>
        /// build from current database by adding update/new pdb entries
        /// but rebuild entry-based groups due to new PFAM version
        /// regroup crystal forms
        /// </summary>
        public void BuildEntryInterfaceGroups(int step)
        {
            DbBuilderHelper.Initialize ();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.totalOperationNum = 8;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Building entry-based groups, includes 8 steps: ");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("1.Calculate entry interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("2. Generate interface files, calculate ASA.");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("3. Compare cryst interfaces with PDB/PQS/PISA BU and ASU Interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("4. Classify PDB entries into homologous groups");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("5. Retrieve chain alignments in groups");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("6. Calculate similarity Q scores in an entry group.");
       //     ProtCidSettings.progressInfo.progStrQueue.Enqueue("7. Compare interfaces of entries in entry groups.");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("7. Deal With Redundant Crystal Forms.");

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortTimeString());
            ProtCidSettings.logWriter.WriteLine("Build entry-based group data.");

// update entry-related data, it is not necessary to recompute entry data from scratch
            string[] updateEntries = GetMissBuCompEntries ();
            CrystInterfaceBuilder interfaceBuilder = new CrystInterfaceBuilder();

            switch (step)
            {
                case 1:
                    ProtCidSettings.logWriter.WriteLine("1. Compute interfaces from crystals");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("1. Compute interfaces from crystals");
                    interfaceBuilder.ComputeEntryCrystInterfaces(updateEntries);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    goto case 2;

                case 2:
                    ProtCidSettings.logWriter.WriteLine("2. Generate interface files, calculate ASA and contacts in interfaces");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("2. Generate interface files, calculate ASA and contacts in interfaces");
                    interfaceBuilder.GenerateEntryInterfaceFiles(updateEntries);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;

                case 3:
                    ProtCidSettings.logWriter.WriteLine("3. Compare cryst interfaces with PDB/PISA BU and ASU Interfaces");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("3. Compare cryst interfaces with PDB/PISA BU and ASU Interfaces");
                    // compare cryst interfaces and BU interfaces for each entry
                    EntryCrystBuInterfaceComp crystBuInterfaceComp = new EntryCrystBuInterfaceComp();
                    crystBuInterfaceComp.UpdateEntryCrystBuInterfaces(updateEntries);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
                    DbBuilderHelper.UpdateIndexes("CrystBuInterfaceComp", ProtCidSettings.protcidDbConnection);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;

                case 4:
                    // group entries, rebuild group tables: PfamHomoSeqInfo, PfamHomoGroupEntryAlign, PfamHomoRepEntryAlign
                    ProtCidSettings.logWriter.WriteLine("4. Classify PDB entries into homologous groups.");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("4. Classify PDB entries into homologous groups");
                    PfamEntryClassifier pfamClassifier = new PfamEntryClassifier();
                    pfamClassifier.ClassifyPdbPfamGroups();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
                    DbBuilderHelper.UpdateIndexes("PFAMHOMO", ProtCidSettings.protcidDbConnection);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;
                case 5:
                    // retrieve chain alignments from HH, and fatcat alignments
                    ProtCidSettings.logWriter.WriteLine("5. Retrieve chain alignments in groups");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("5. Retrieve chain alignments in groups");
                    Alignments.GroupEntryAlignments groupAlignment = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
                    groupAlignment.UpdateCrcHhAlignments();
                    groupAlignment.RetrieveChainAlignmentsInGroups();
            //        groupAlignment.GetRepChainPairsToBeAligned();
            //       groupAlignment.GetRepEntryChainAlignFilesFromMissingEntityAlign();
            //        groupAlignment.UpdateFatcatAlignmentsFromFile("");
           //         int[] updateGroups = { 10400 };
           //         groupAlignment.UpdateGroupEntryAlignments (updateGroups);                    
           //         groupAlignment.UpdateMissingChainAlignmentsInGroups (null);        
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
                    DbBuilderHelper.UpdateIndexes("PFAMHOMO", ProtCidSettings.protcidDbConnection);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;
                case 6:
                    // compare entry interfaces in entry groups
                    ProtCidSettings.logWriter.WriteLine("6. Calculate similarity Q scores in a entry-grouped group");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("6. Calculate similarity Q scores in a entry-grouped group");
                    HomoGroupInterfacesFinder.modifyType = "build";
                    HomoGroupInterfacesFinder groupInterfaceFinder = new HomoGroupInterfacesFinder();
                    groupInterfaceFinder.DetectHomoGroupInterfaces(null);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
                    DbBuilderHelper.UpdateIndexes("INTERFACE", ProtCidSettings.protcidDbConnection);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;
                case 7:
                    // group crystal forms based on Q scores of entry interfaces in an entry group        
                    ProtCidSettings.logWriter.WriteLine("7. Detecting redundant crystal forms");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("6. Detecting redundant crystal forms");
                    RedundantCrystForms reduntCrystForm = new RedundantCrystForms();
                    reduntCrystForm.CheckReduntCrystForms();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    // distinct crystal forms
                    ProtCidSettings.logWriter.WriteLine("8. Cluster crystal forms in entry groups");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Non-Redundant Crystal Forms");
                    NonredundantCfGroups nonreduntCfGroups = new NonredundantCfGroups();
                    nonreduntCfGroups.UpdateCfGroupInfo();

                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
                    DbBuilderHelper.UpdateIndexes("REDUN", ProtCidSettings.protcidDbConnection);
                    break;
                default:
                    break;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Build entry-based groups Done!");
            ProtCidSettings.logWriter.WriteLine("Build entry-based groups Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// update entry-related data
        /// it is not necessary to recompute entry data from scratch
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateEntryInterfaceData(string[] updateEntries)
        {
            DbBuilderHelper.Initialize();
            if (updateEntries == null)
            {
  //              string lsFile = @"D:\Qifang\ProjectData\DbProjectData\PDB\newls-pdb_entry.txt";
 //               updateEntries = GetUpdateEntries(lsFile);
                updateEntries = GetUpdateEntries();
            }

            ProtCidSettings.logWriter.WriteLine("1. Compute interfaces from crystals");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("1. Compute interfaces from crystals");
            CrystInterfaceBuilder interfaceBuilder = new CrystInterfaceBuilder();
            interfaceBuilder.ComputeEntryCrystInterfaces(updateEntries);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.progressInfo.currentOperationIndex++;

            ProtCidSettings.logWriter.WriteLine("2. Generate interface files, calculate ASA and contacts in interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("2. Generate interface files, calculate ASA and contacts in interfaces");
            interfaceBuilder.GenerateEntryInterfaceFiles(updateEntries);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.progressInfo.currentOperationIndex++;
            
            ProtCidSettings.logWriter.WriteLine("3. Compare cryst interfaces with PDB/PISA BU and ASU Interfaces");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("3. Compare cryst interfaces with PDB/PISA BU and ASU Interfaces");
            // compare cryst interfaces and BU interfaces for each entry
            EntryCrystBuInterfaceComp crystBuInterfaceComp = new EntryCrystBuInterfaceComp();
            crystBuInterfaceComp.UpdateEntryCrystBuInterfaces(updateEntries);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.logWriter.WriteLine("4. Update crc-crc hh alignments");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("4. Update crc-crc hh alignments");
            Alignments.GroupEntryAlignments groupAlignment = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
            groupAlignment.UpdateCrcHhAlignments();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
            DbBuilderHelper.UpdateIndexes("CrystBuInterfaceComp", ProtCidSettings.protcidDbConnection);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.progressInfo.currentOperationIndex++;
        }

        #region update entry-based groups      
        /// <summary>
        /// update database
        /// </summary>
        public void UpdatePfamInterfaces()
        {
            DbBuilderHelper.Initialize();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating PFAM Interfaces Data.");

            string line = "";
            string[] updateEntries = null;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get the entries to be updated from XML file directory.");
 //           string lsFile = "EntriesNotInEntryLevelGroups.txt";
            updateEntries = GetUpdateEntries();
 //           updateEntries = GetUpdateEntries(lsFile);
            

            if (updateEntries.Length == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No update needed.");
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
                ProtCidSettings.progressInfo.threadFinished = true;
                return;
            }
// Update entry interfaces, 
            // generate interface files, calculate surface area, 
            // then compare crystal interfaces to interfaces in PDB/PISA BAs
//            UpdateEntryInterfaceData(updateEntries);  

 //           updateEntries = GetUpdateEntriesFromPfamUpdate();

            // classify these groups
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update classification of homologous groups");
            PfamEntryClassifier pfamClassifier = new PfamEntryClassifier();
            Dictionary<int, string[]> updateGroupHash = null;
            updateGroupHash = pfamClassifier.UpdatePdbPfamGroups(updateEntries);

            ProtCidSettings.progressInfo.currentOperationIndex++;

            if (updateGroupHash == null)
            {
                updateGroupHash = new Dictionary<int, string[]>();
            }
            // divide into two groups
  /*          int groupIdDivide = 7000;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("This is for groupId >= 7000");
            ProtCidSettings.logWriter.WriteLine("This is for groupid >= 7000.");*/
            if (updateGroupHash.Count == 0)
            {
                int groupId = 0;
                StreamReader updateGroupReader = new StreamReader("updateGroups.txt");
                //		string line = "";
                while ((line = updateGroupReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(' ');
                    groupId = Convert.ToInt32(fields[0]);
                    //           if (groupId >= groupIdDivide)

                    string[] entries = new string[fields.Length - 1];
                    Array.Copy(fields, 1, entries, 0, entries.Length);
                    updateGroupHash.Add(groupId, entries);

                }
                updateGroupReader.Close();
            }

            int[] updateGroups = new int[updateGroupHash.Count];
            updateGroupHash.Keys.CopyTo(updateGroups, 0);
            Array.Sort(updateGroups);

            // update xtal interfaces for these groups
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve alignments in a group");
            Alignments.GroupEntryAlignments groupAlignment = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
            try
            {
                 groupAlignment.UpdateChainAlignmentsInGroups(updateGroups);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Stop the updating alignments and return.");
                ProtCidSettings.logWriter.WriteLine(ex.Message);
                ProtCidSettings.logWriter.WriteLine("Stop the updating alignments and return.");
                ProtCidSettings.logWriter.Flush();
                return;
            }
            ProtCidSettings.progressInfo.currentOperationIndex++;

            // update xtal interfaces for these groups
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Build crystal, detect common interfaces in a group");
            HomoGroupInterfacesFinder.modifyType = "update";
            HomoGroupInterfacesFinder groupInterfaceFinder = new HomoGroupInterfacesFinder();
            try
            {
                groupInterfaceFinder.UpdateHomoGroupInterfaces(updateGroupHash);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Stop the updating group interfaces comparing and return.");
                ProtCidSettings.logWriter.WriteLine(ex.Message);
                ProtCidSettings.logWriter.WriteLine("Stop the updating group interfaces comparing and return.");
                ProtCidSettings.logWriter.Flush();                
                return;
            }
            ProtCidSettings.progressInfo.currentOperationIndex++;          

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("6. Detecting redundant crystal forms");
            RedundantCrystForms reduntCrystForm = new RedundantCrystForms();
            try
            {
                reduntCrystForm.UpdateReduntCrystForms(updateGroups);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Stop the updating redundant cryst forms and return.");
                ProtCidSettings.logWriter.WriteLine(ex.Message);
                ProtCidSettings.logWriter.WriteLine("Stop the updating redundant cryst forms and return.");
                ProtCidSettings.logWriter.Flush(); 
                return;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Non-Redundant Crystal Forms");
            NonredundantCfGroups nonreduntCfGroups = new NonredundantCfGroups();
            try
            {
                nonreduntCfGroups.UpdateCfGroupInfo(updateGroups);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Stop the updating CF groups and return.");
                ProtCidSettings.logWriter.WriteLine(ex.Message);
                ProtCidSettings.logWriter.WriteLine("Stop the updating CF groups and return.");
                ProtCidSettings.logWriter.Flush(); 
                return;
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Recomputing indices.");
            DbBuilderHelper.UpdateIndexes("RedundantCfGroups", ProtCidSettings.protcidDbConnection);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.logWriter.Close();
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            StreamReader dataReader = new StreamReader(Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt"));
            List<string> entryList = new List<string> ();
            string line = "";
            string entry = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                entry = line.Substring(0, 4);
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            dataReader.Close();
            return entryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries(string lsFile)
        {
            StreamReader dataReader = new StreamReader(lsFile);
            List<string> entryList = new List<string>();
            string line = "";
            string entry = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                entry = line.Substring(0, 4);
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            dataReader.Close();
            return entryList.ToArray ();
        }
        #endregion

        #region for debug
        public void AddSymmetryIndexes ()
        {
            DbBuilderHelper.Initialize();
            ChainInterfaces.InterfaceSymmetry interfaceSym = new ChainInterfaces.InterfaceSymmetry();
            interfaceSym.CalculateInterfaceSymmetryJindex();
        }
        private string[] GetMissBuCompEntries ()
        {
            string entryListFile = "EntriesNeededCrystBuComp.txt";
            List<string> updateEntryList = new List<string> ();
            if (File.Exists(entryListFile))
            {
                StreamReader dataReader = new StreamReader(entryListFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    updateEntryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
                DataTable crystEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                string pdbId = "";

                foreach (DataRow entryRow in crystEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    if (!IsCrystBuChainInterfaceCompExist(pdbId))
                    {
                        updateEntryList.Add(pdbId);
                    }
                }
            }
            return updateEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsCrystBuChainInterfaceCompExist (string pdbId)
        {
            string queryString = string.Format("Select * From CrystPdbBuInterfaceComp Where PdbID = '{0}';", pdbId);
            DataTable pdbBuCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select * From CrystPisaBuInterfaceComp Where PdbID = '{0}';", pdbId);
            DataTable pisaBuCompTable = ProtCidSettings.protcidQuery.Query(queryString);

            if (pdbBuCompTable.Rows.Count == 0 && (! IsEntryPdbBuMonomer (pdbId)))
            {
                return false;
            }

            if (pisaBuCompTable.Rows.Count == 0 && (!IsEntryPisaBuMonomer(pdbId)))
            {
                return false;
            }

            return true;
        }

        private bool IsEntryPdbBuMonomer(string pdbId)
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

        private string[] GetMissingSAentries ()
        {
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces Where SurfaceArea < 0;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
//            string[] entries = new string[entryTable.Rows.Count];
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (IsEntryHaveOtherSA (pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
            }
            return entryList.ToArray ();
        }

        private bool IsEntryHaveOtherSA(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}' AND SurfaceArea > 0;", pdbId);
            DataTable SaTable = ProtCidSettings.protcidQuery.Query (queryString);
            if (SaTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntriesFromPfamUpdate ()
        {
            AuxFuncs.UpdateEntriesDifVersionPfams updateEntryDifVersions = new UpdateEntriesDifVersionPfams ();
            string[] updateEntries = updateEntryDifVersions.GetUpdateEntriesDifEntryPfamArch();
            return updateEntries;
        }
        #endregion

    }
}
