using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.ProtInterfaces;
using InterfaceClusterLib.DataTables;
using InterfaceClusterLib.InterfaceComp;
using DbLib;
using AuxFuncLib;
using BuCompLib.BuInterfaces;

namespace InterfaceClusterLib.InterfaceProcess
{
    public class CrystInterfaceRetriever
    {
        #region member variables
        public DbInsert dbInsert = new DbInsert();
        public DbQuery dbQuery = new DbQuery();
        public DbUpdate dbUpdate = new DbUpdate();
        public AsuInterfaces asuInterfacesNonCryst = null;
        public Alignments.HomoEntryAlignInfo homoEntryAlignInfo = new Alignments.HomoEntryAlignInfo();
        public CrystEntryInterfaceComp crystInterfaceComp = new CrystEntryInterfaceComp();
        public InterfaceSymmetryIndex symIndex = new InterfaceSymmetryIndex();
        #endregion

        #region detect interfaces in crystal structures
        /// <summary>
        /// find unique interfaces in a crystal
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        public void FindUniqueInterfaces(string[] pdbIdList)
        {
            CrystInterfaceTables.InitializeTables();
            bool isUpdate = true;
            if (pdbIdList == null)
            {
           //     pdbIdList = ReadProtPdbEntries();
                pdbIdList = GetMissingInterfaceEntries();
          //      pdbIdList = GetFullListOfProtPdbEntries();
         //       CrystInterfaceTables.InitializeDbTables();
            }

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";
            ProtCidSettings.progressInfo.totalStepNum = pdbIdList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbIdList.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

            foreach (string pdbId in pdbIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    FindUniqueInterfaces(pdbId, isUpdate);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Retrieve cryst interfaces errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Retrieve cryst interfaces errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public InterfaceChains[] FindUniqueInterfaces(string pdbId, bool isUpdate)
        {
            string spaceGroup = GetEntrySpaceGroup(pdbId);

            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            if (! Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            InterfaceChains[] entryInterfaces = null;
            AsuInterfaces asuInterfacesNonCryst = new AsuInterfaces();

            try
            {
                if (spaceGroup == "NMR")
                {
                    entryInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(coordXmlFile);
                    if (entryInterfaces == null)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("NMR entry " + pdbId + " is a monomer.");
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("NMR entry " + pdbId + " is a monomer.");
                        ProtCidSettings.logWriter.Flush();
#endif
                    }
                }
                else
                {
                    ContactInCrystal contactInCrystal = new ContactInCrystal();
                    int numOfChainsInUnitCell = contactInCrystal.FindInteractChains(coordXmlFile);

                    if (numOfChainsInUnitCell > 64)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
                        ProtCidSettings.logWriter.Flush();
#endif
                    }

                    if (contactInCrystal.InterfaceChainsList == null)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("No interfaces exist in entry: " + pdbId);
                    }
                    entryInterfaces = contactInCrystal.InterfaceChainsList;
                }
                AssignEntryInterfacesToTable(entryInterfaces, pdbId, spaceGroup);
                AssignEntryInterfaceCompToTable(pdbId, entryInterfaces);

                if (isUpdate)
                {
                    DeleteObsData(pdbId);
                }

                dbInsert.InsertDataIntoDBtables
                    (ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                dbInsert.InsertDataIntoDBtables
                    (ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp]);


                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear();
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Clear();
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
#endif
            }
            finally
            {
                if (File.Exists(coordXmlFile))
                {
                    File.Delete(coordXmlFile);
                }
            }
            return entryInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsData(string pdbId)
        {
            string deleteString = string.Format("Delete From CrystEntryInterfaces Where PdbID = '{0}'", pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
            deleteString = string.Format("Delete From EntryInterfaceComp Where PdbID = '{0}';", pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
            deleteString = string.Format("Delete From DifEntryInterfaceComp Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            string interfaceFileHashFolder = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + pdbId.Substring(1, 2));
            string[] interfaceFiles = Directory.GetFiles(interfaceFileHashFolder, pdbId + ".*");
            foreach (string interfaceFile in interfaceFiles)
            {
                File.Delete(interfaceFile);
            }
        }
        /// <summary>
        /// find unique interfaces in a crystal
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        public void FindUniqueInterfacesFromAsu(string[] pdbIdList)
        {
            CrystInterfaceTables.InitializeTables();

#if DEBUG
            StreamWriter interfaceDataWriter = new StreamWriter("CrystEntryInterfaces.txt", true);
            StreamWriter entryInterfaceCompDataWriter = new StreamWriter("EntryInterfaceComp.txt", true);
            StreamWriter logWriter = new StreamWriter("EntryInterfaceRetrievalLog.txt", true);
#endif
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";
            ProtCidSettings.progressInfo.totalStepNum = pdbIdList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbIdList.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

        //    Hashtable entrySgHash = GetEntrySgAsu(new ArrayList(pdbIdList));
            InterfaceChains[] entryInterfaces = null;
            AsuInterfaces asuInterfacesNonCryst = new AsuInterfaces();
            string spaceGroup = "";
            foreach (string pdbId in pdbIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                spaceGroup = GetEntrySpaceGroup(pdbId);

                string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
                string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

                try
                {
                    entryInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(coordXmlFile);
                    if (entryInterfaces == null)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Entry " + pdbId + " is a monomer.");
#if DEBUG
                        logWriter.WriteLine("Entry " + pdbId + " is a monomer.");
                        logWriter.Flush();
#endif
                        continue;
                    }
                    AssignEntryInterfacesToTable(entryInterfaces, pdbId, spaceGroup);
                    AssignEntryInterfaceCompToTable(pdbId, entryInterfaces);

                    dbInsert.InsertDataIntoDBtables
                        (ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                    dbInsert.InsertDataIntoDBtables
                        (ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp]);
#if DEBUG
                    WriteDataToFiles(interfaceDataWriter, entryInterfaceCompDataWriter);
#endif

                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear();
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
#if DEBUG
                    logWriter.WriteLine("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
                    logWriter.Flush();
#endif
                }
                finally
                {
                    if (File.Exists(coordXmlFile))
                    {
                        File.Delete(coordXmlFile);
                    }
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }

#if DEBUG
            interfaceDataWriter.Close();
            entryInterfaceCompDataWriter.Close();
            logWriter.Close();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadProtPdbEntries()
        {
            StreamReader dataReader = new StreamReader("ProtPdbEntries.txt");
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line);
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        private string[] GetMissingInterfaceEntries()
        {
            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            List<string> entryList = new List<string> ();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["pdbId"].ToString();
                if (! IsEntryInterfaceExist(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        private bool IsEntryInterfaceExist(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// check the representative interfaces exist in the NMR group
        /// </summary>
        /// <param name="noCrystFileList"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public InterfaceChains[] GetNmrEntryInterfaces(string[] nmrPdbList)
        {
            asuInterfacesNonCryst = new AsuInterfaces();
            Dictionary<string, InterfaceChains[]> sgEntryInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            CrystInterfaceTables.InitializeTables();

            foreach (string pdbId in nmrPdbList)
            {
                string noCrystFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
                // unzip xml file
                if (!File.Exists(noCrystFile))
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(string.Format("{0} file not exist. ", noCrystFile));
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine(string.Format("{0} file not exist. ", noCrystFile));
#endif
                    continue;
                }
                try
                {
                    InterfaceChains[] asuInterfaces = null;
                    asuInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(noCrystFile);
                    AddEntityIDsToInterfaces(pdbId, ref asuInterfaces);

                    if (asuInterfaces != null && asuInterfaces.Length > 0)
                    {
                        sgEntryInterfacesHash.Add(pdbId, asuInterfaces);
                        AssignEntryInterfacesToTable(asuInterfaces, pdbId, "NMR");
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine(ex.Message);
#endif
                }
            }
            return null;
        }

        /// <summary>
        /// find unique interfaces in a crystal
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        public void FindUniqueInterfaces(string repEntry, string[] pdbIdList)
        {
            CrystInterfaceTables.InitializeTables();
            asuInterfacesNonCryst = new AsuInterfaces();

            List<string> entryList = new List<string> (pdbIdList);
            entryList.Insert(0, repEntry);

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";
            ProtCidSettings.progressInfo.totalStepNum = entryList.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryList.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

            Dictionary<string, InterfaceChains[]> entryInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            Dictionary<string, string> entrySgHash = GetEntrySgAsu(entryList);
            foreach (string pdbId in entryList)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                InterfaceChains[] entryInterfaces = null;
                string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
                string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

                try
                {
                    if ((string)entrySgHash[pdbId] == "NMR")
                    {
                        entryInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(coordXmlFile);
                    }
                    else
                    {
                        ContactInCrystal contactInCrystal = new ContactInCrystal();

                        int numOfChainsInUnitCell = contactInCrystal.FindInteractChains(coordXmlFile);

                        if (numOfChainsInUnitCell > 64)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
                        }

                        if (contactInCrystal.InterfaceChainsList == null)
                        {
                            continue;
                        }
                        entryInterfaces = contactInCrystal.InterfaceChainsList;
                    }
                    AssignEntryInterfacesToTable(entryInterfaces, pdbId, (string)entrySgHash[pdbId]);
                    entryInterfacesHash.Add(pdbId, entryInterfaces);

                    string deleteString = string.Format("Delete From CrystEntryInterfaces Where PdbID = '{0}'", pdbId);
                    ProtCidSettings.protcidQuery.Query( deleteString);

                    dbInsert.InsertDataIntoDBtables
                        (ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear();
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().IndexOf("no protein chains") < -1 &&
                        ex.Message.ToLower().IndexOf("no symmetry operators") < -1)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": " + ex.Message);
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine(pdbId + ": " + ex.Message);
#endif
                    }
                }
                finally
                {
                    if (File.Exists(coordXmlFile))
                    {
                        File.Delete(coordXmlFile);
                    }
                }
            }
            Directory.Delete(ProtCidSettings.tempDir, true);
            // compare interfaces between representative entry and its homology entries
            // save data into db
            crystInterfaceComp.CompareRepHomoEntryInterfacesInGroup (repEntry, pdbIdList, entryInterfacesHash);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryList"></param>
        /// <returns></returns>
        protected Dictionary<string, string> GetEntrySgAsu(List<string> entryList)
        {
            Dictionary<string, string> entrySgAsuHash = new Dictionary<string,string> ();
            string spaceGroup = "";
            foreach (string entry in entryList)
            {
                spaceGroup = GetEntrySpaceGroup(entry);
                entrySgAsuHash.Add(entry, spaceGroup);
            }
            return entrySgAsuHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntrySpaceGroup(string pdbId)
        {
            string queryString = string.Format("Select SpaceGroup, Method From PdbEntry WHere PdbID = '{0}';", pdbId);
            DataTable sgTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string spaceGroup = sgTable.Rows[0]["SpaceGroup"].ToString().TrimEnd();
            string method = sgTable.Rows[0]["Method"].ToString().TrimEnd();
            if (method.IndexOf("NMR") > -1)
            {
                spaceGroup = "NMR";
            }
            return spaceGroup;
        }
        #endregion

        #region unique interface in an entry
        /// <summary>
        /// find unique interfaces in a crystal
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        public void FindUniqueInterfaces(string xmlFile, string sg_asu, ref InterfaceChains[] uniqueEntryinterfaces)
        {
            string pdbId = xmlFile.Substring(xmlFile.LastIndexOf("\\") + 1, 4);
            string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir); ;
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable crystInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string> interfaceDefHash = new Dictionary<int,string> ();
            Dictionary<int, string> interfaceEntityInfoHash = new Dictionary<int,string> ();
            if (crystInterfaceTable.Rows.Count > 0)
            {
                // build cryst interfaces based on database info
                foreach (DataRow dRow in crystInterfaceTable.Rows)
                {
                    string symOpString1 = dRow["AsymChain1"].ToString().Trim() + "_" +
                        dRow["SymmetryString1"].ToString().Trim();
                    string symOpString2 = dRow["AsymChain2"].ToString().Trim() + "_" +
                        dRow["SymmetryString2"].ToString().Trim();
                    if (!interfaceDefHash.ContainsKey(Convert.ToInt32(dRow["InterfaceId"].ToString())))
                    {
                        interfaceDefHash.Add(Convert.ToInt32(dRow["InterfaceId"].ToString()),
                            symOpString1 + ";" + symOpString2);
                        interfaceEntityInfoHash.Add(Convert.ToInt32(dRow["InterfaceId"].ToString()),
                            dRow["EntityID1"].ToString() + "_" + dRow["EntityID2"].ToString());
                    }
                }
            }

            try
            {
                ContactInCrystal contactInCrystal = new ContactInCrystal();
                if (interfaceDefHash.Count > 0)  // interfaces defined in db
                {
                    bool interfaceExist = true;
                    contactInCrystal.FindInteractChains(coordXmlFile, interfaceDefHash, interfaceEntityInfoHash, out interfaceExist);
                    if (interfaceExist)
                    {
                        uniqueEntryinterfaces = contactInCrystal.InterfaceChainsList;
                        foreach (InterfaceChains interfaceChains in uniqueEntryinterfaces)
                        {
                            DataRow[] interfaceDefRows = crystInterfaceTable.Select
                                (string.Format("InterfaceId = '{0}'", interfaceChains.interfaceId));
                            interfaceChains.entityId1 = Convert.ToInt32(interfaceDefRows[0]["EntityID1"].ToString());
                            interfaceChains.entityId2 = Convert.ToInt32(interfaceDefRows[0]["EntityID2"].ToString());
                        }              
                    }                    
                }
                else   // no interface definition in db, retrieve interfaces from coordinate file
                {
                    int numOfChainsInUnitCell = contactInCrystal.FindInteractChains(coordXmlFile);

                    if (numOfChainsInUnitCell > 64)
                    {
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
#endif
                    }

                    if (contactInCrystal.InterfaceChainsList != null)
                    {
                        uniqueEntryinterfaces = contactInCrystal.InterfaceChainsList;
                        AssignEntryInterfacesToTable(uniqueEntryinterfaces, pdbId, sg_asu);
                        AssignEntryInterfaceCompToTable(pdbId, uniqueEntryinterfaces);
                    }
                }
               
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().IndexOf("no protein chains") < -1 &&
                    ex.Message.ToLower().IndexOf("no symmetry operators") < -1)
                {
                    throw new Exception(pdbId + ": " + ex);
                }
            }
            finally
            {
                if (File.Exists(coordXmlFile))
                {
                    File.Delete(coordXmlFile);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceList"></param>
        protected void AddEntityIDsToInterfaces(string pdbId, ref InterfaceChains[] interfaceList)
        {
            if (interfaceList == null)
            {
                return;
            }
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, int> chainEntityHash = new Dictionary<string,int> ();
            foreach (DataRow dRow in entityTable.Rows)
            {
                chainEntityHash.Add(dRow["AsymID"].ToString().Trim(), Convert.ToInt32(dRow["EntityID"].ToString()));
            }
            foreach (InterfaceChains oneInterface in interfaceList)
            {
                oneInterface.entityId1 = (int)chainEntityHash[GetAsymChainFromSymOpString(oneInterface.firstSymOpString)];
                oneInterface.entityId2 = (int)chainEntityHash[GetAsymChainFromSymOpString(oneInterface.secondSymOpString)];
            }
        }

        protected string GetAsymChainFromSymOpString(string symOpString)
        {
            string[] fields = symOpString.Split('_');
            return fields[0];
        }
        #endregion 

        #region interface comparison between interfaces of an entry
        /// <summary>
        /// pairwise comparing two interfaces in an entry
        /// add data to table
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="sg_asu">spacegroup + asu</param>
        /// <param name="pdbId"></param>
        /// <param name="repEntryInterfaces">interfaces of the representative entry</param>
        protected void CompareEntryInterfaces(string pdbId, InterfaceChains[] repEntryInterfaces)
        {
            string queryString = string.Format("SELECT * FROM EntryInterfaceComp WHERE PdbID = '{0}';", pdbId);
            DataTable entryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (entryInterfaceCompTable.Rows.Count > 0)
            {
                return;
            }
            AssignEntryInterfaceCompToTable(pdbId, repEntryInterfaces);
        }
      
        /// <summary>
        /// pairwise comparing two interfaces in a representative entry
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="spaceGroup"></param>
        /// <param name="pdbId"></param>
        /// <param name="interfacesInSg"></param>
        protected void AssignEntryInterfaceCompToTable(string pdbId, InterfaceChains[] interfacesInSg)
        {
            if (interfacesInSg == null)
            {
                return;
            }
            InterfacesComp interfaceComp = new InterfacesComp();
            InterfacePairInfo[] interfacePairsInfo = interfaceComp.CompareInterfacesWithinCrystal(ref interfacesInSg);
            foreach (InterfacePairInfo pairInfo in interfacePairsInfo)
            {
                if (pairInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow interfaceCompRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].NewRow();
                    interfaceCompRow["PdbID"] = pdbId;
                    interfaceCompRow["InterfaceID1"] = pairInfo.interfaceInfo1.interfaceId;
                    interfaceCompRow["InterfaceID2"] = pairInfo.interfaceInfo2.interfaceId;
                    interfaceCompRow["QScore"] = pairInfo.qScore;
                    if (pairInfo.isInterface2Reversed)
                    {
                        interfaceCompRow["IsReversed"] = 1;
                    }
                    else
                    {
                        interfaceCompRow["IsReversed"] = 0;
                    }
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Rows.Add(interfaceCompRow);
                }
            }
        }
        #endregion

        #region insert entry interfaces into table
        /// <summary>
        /// assign interfaces of an entry into the datatable
        /// </summary>
        /// <param name="interChains">interfaces in an entry</param></param>
        /// <param name="pdbId"></param>
        protected void AssignEntryInterfacesToTable(InterfaceChains[] interfaceChains, string pdbId, string sg_asu)
        {
            if (interfaceChains == null)
            {
                return;
            }
            string queryString = string.Format("SELECT AsymID, AuthorChain, EntityID FROM AsymUnit " +
                "WHERE AsymUnit.PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable chainInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] sgAsu = sg_asu.Split('_');
            SymOperator symOp = new SymOperator();
            string fullSymString = "";
            int entityId = -1;
            string authorChain = "";
   //         bool isSymmetry = false;
            double symmetryJindex = -1.0;
            foreach (InterfaceChains thisInterface in interfaceChains)
            {
                // interative chains info
                DataRow interfaceRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].NewRow();
                interfaceRow["PDBID"] = pdbId;
                interfaceRow["InterfaceID"] = thisInterface.interfaceId;
                string symOpStr1 = thisInterface.firstSymOpString;
                string asymChain1 = symOpStr1.Substring(0, symOpStr1.IndexOf("_"));
                string symString1 = symOpStr1.Substring(symOpStr1.IndexOf("_") + 1, symOpStr1.Length - symOpStr1.IndexOf("_") - 1);
                interfaceRow["AsymChain1"] = asymChain1;
                //	DataRow[] chainRows = chainInfoTable.Select (string.Format ("AsymID = '{0}'", asymChain1));
                GetAuthEntityInfoForAsymChain(asymChain1, chainInfoTable, out entityId, out authorChain);
                interfaceRow["AuthChain1"] = authorChain;
                interfaceRow["EntityID1"] = entityId;
                thisInterface.entityId1 = entityId;
                interfaceRow["SymmetryString1"] = symString1;
                string symOpStr2 = thisInterface.secondSymOpString;
                string asymChain2 = symOpStr2.Substring(0, symOpStr2.IndexOf("_"));
                string symString2 = symOpStr2.Substring(symOpStr2.IndexOf("_") + 1, symOpStr2.Length - symOpStr2.IndexOf("_") - 1);
                interfaceRow["AsymChain2"] = asymChain2;
                if (asymChain1 != asymChain2)
                {
                    GetAuthEntityInfoForAsymChain(asymChain2, chainInfoTable, out entityId, out authorChain);
                }
                interfaceRow["AuthChain2"] = authorChain;
                interfaceRow["EntityID2"] = entityId;
                thisInterface.entityId2 = entityId;
                interfaceRow["SymmetryString2"] = symString2;
                // get full symmetry string from symmetry operators
                if (symString1 == "1_555")
                {
                    interfaceRow["FullSymmetryString1"] = "X,Y,Z";
                }
                else
                {
                    try
                    {
                        fullSymString = symOp.ConvertSymOpStringToFull(sgAsu[0], symString1);
                    }
                    catch
                    {
                        fullSymString = "-";
                    }
                    interfaceRow["FullSymmetryString1"] = fullSymString;
                }
                if (symString2 == "1_555")
                {
                    interfaceRow["FullSymmetryString2"] = "X,Y,Z";
                }
                else
                {
                    try
                    {
                        fullSymString = symOp.ConvertSymOpStringToFull(sgAsu[0], symString2);
                    }
                    catch
                    {
                        fullSymString = "-";
                    }
                    interfaceRow["FullSymmetryString2"] = fullSymString;
                    //	interfaceRow["FullSymmetryString2"] = symOp.ConvertSymOpStringToFull(sgAsu[0], symString2);
                }
                interfaceRow["SurfaceArea"] = -1.0;
       /*         isSymmetry = IsInterfaceSymmetry(thisInterface);
                if (isSymmetry )
                {
                    interfaceRow["IsSymmetry"] = "1";
                }
                else
                {
                    interfaceRow["IsSymmetry"] = "0";
                }*/
                symmetryJindex = symIndex.CalculateInterfaceSymmetry(thisInterface);
                interfaceRow["SymmetryIndex"] = symmetryJindex;
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Rows.Add(interfaceRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainInfoTable"></param>
        /// <param name="entityId"></param>
        /// <param name="authorChain"></param>
        private void GetAuthEntityInfoForAsymChain(string asymChain, DataTable chainInfoTable,
            out int entityId, out string authorChain)
        {
            entityId = -1;
            authorChain = "-";
            DataRow[] chainRows = chainInfoTable.Select(string.Format("AsymID = '{0}'", asymChain));
            if (chainRows.Length > 0)
            {
                entityId = Convert.ToInt32(chainRows[0]["EntityID"].ToString());
                authorChain = chainRows[0]["AuthorChain"].ToString().TrimEnd();
            }
            else
            {
                string origAsymChain = "";
                if (IsAsymChainNcsGenerated(asymChain, out origAsymChain))
                {
                    chainRows = chainInfoTable.Select(string.Format("AsymID = '{0}'", origAsymChain));
                    if (chainRows.Length > 0)
                    {
                        entityId = Convert.ToInt32(chainRows[0]["EntityID"].ToString());
                        authorChain = chainRows[0]["AuthorChain"].ToString().TrimEnd();
                    }
                }
            }
        }

        /// <summary>
        /// if an asymmetric chain id contains letters + number,
        /// it is generated from PDB NCS
        /// </summary>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private bool IsAsymChainNcsGenerated(string asymChain, out string origAsymChain)
        {
            int digitIdx = -1;
            origAsymChain = "";
            for (int i = 0; i < asymChain.Length; i++)
            {
                if (char.IsDigit(asymChain[i]))
                {
                    digitIdx = i;
                    break;
                }
            }
            if (digitIdx == -1)
            {
                return false;
            }
            origAsymChain = asymChain.Substring(0, digitIdx);
            string digitPart = asymChain.Substring(digitIdx, asymChain.Length - digitIdx);

            foreach (char ch in digitPart)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region write data into text file
        public void WriteDataToFiles(StreamWriter interfaceDataWriter, StreamWriter entryInterfaceCompWriter)
        {
            string dataLine = "";
            foreach (DataRow dataRow in CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Rows)
            {
                dataLine = "";
                foreach (object item in dataRow.ItemArray)
                {
                    dataLine += (item.ToString() + "\t");
                }
                interfaceDataWriter.WriteLine(dataLine.TrimEnd ('\t'));
            }
            interfaceDataWriter.Flush();

            foreach (DataRow dataRow in CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Rows)
            {
                dataLine = "";
                foreach (object item in dataRow.ItemArray)
                {
                    dataLine += (item.ToString() + "\t");
                }
                entryInterfaceCompWriter.WriteLine(dataLine);
            }
            entryInterfaceCompWriter.Flush();
        }
        #endregion

        #region detect interfaces for Multi-Chain domain in PFAM
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        public InterfaceChains[] FindUniquePfamInterfaces(string pdbId, bool isUpdate)
        {
            string spaceGroup = GetEntrySpaceGroup(pdbId);

            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            InterfaceChains[] entryInterfaces = null;
            AsuInterfaces asuInterfacesNonCryst = new AsuInterfaces();

            try
            {
                if (spaceGroup == "NMR")
                {
                    entryInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(coordXmlFile);
                    if (entryInterfaces == null)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("NMR entry " + pdbId + " is a monomer.");
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("NMR entry " + pdbId + " is a monomer.");
                        ProtCidSettings.logWriter.Flush();
#endif
                    }
                }
                else
                {
                    ContactInCrystal contactInCrystal = new ContactInCrystal();
                    int numOfChainsInUnitCell = contactInCrystal.FindInteractChains(coordXmlFile);

                    if (numOfChainsInUnitCell > 64)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("The number of chains of " + pdbId + " in a unit cell: " + numOfChainsInUnitCell.ToString());
                        ProtCidSettings.logWriter.Flush();
#endif
                    }

                    if (contactInCrystal.InterfaceChainsList == null)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("No interfaces exist in entry: " + pdbId);
                    }
                    entryInterfaces = contactInCrystal.InterfaceChainsList;
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine("Retrieving interfaces from " + pdbId + " errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
#endif
            }
            finally
            {
                if (File.Exists(coordXmlFile))
                {
                    File.Delete(coordXmlFile);
                }
            }
            return entryInterfaces;
        }
        #endregion
    }
}
