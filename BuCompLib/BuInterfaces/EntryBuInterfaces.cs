using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Data;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.Settings;
using DbLib;
using AuxFuncLib;
using ProgressLib;

namespace BuCompLib.BuInterfaces
{
    public class EntryBuInterfaces
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public DbInsert dbInsert = new DbInsert();
        public DbUpdate dbUpdate = new DbUpdate();
        private InterfacesComp interfaceComp = new InterfacesComp();
        private BuDomainInterfaces buDomainInterfaces = new BuDomainInterfaces();
        private AsuIntraChainDomainInterfaces intraChainDomainInterfaces = null;
        public BiolUnitRetriever buRetriever = new BiolUnitRetriever();
        public const int maxChainNumOfBU = 128;
        #endregion

        public EntryBuInterfaces()
        {
        }

        #region interfaces from entries
        /// <summary>
        /// 
        /// </summary>
        public void GetBuInterfaces()
        {           
            if (BuCompBuilder.logWriter == null)
            {
                BuCompBuilder.logWriter = new StreamWriter("BuCompBuilderLog.txt", true);
                BuCompBuilder.logWriter.WriteLine(DateTime.Today.ToShortDateString());
                BuCompBuilder.logWriter.WriteLine("Retrieve chain interfaces of biological assemblies");
                
            }
            if (BuCompBuilder.BuType == "asu")
            {
                intraChainDomainInterfaces = new AsuIntraChainDomainInterfaces();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve " + BuCompBuilder.BuType + " BU Interfaces.");
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "BU Interfaces";
            
            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            ProtCidSettings.progressInfo.totalOperationNum = protEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = protEntryTable.Rows.Count;

            string pdbId = "";
            
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

      /*         if (IsEntryDataExist(pdbId, BuCompBuilder.BuType))
                {
                    continue;
                }*/

                RetrieveEntryBUInterfaces(pdbId);
            }
            BuCompBuilder.logWriter.WriteLine("Retrieve chain interfaces of biological assemblies done!");
//            BuCompBuilder.logWriter.Close();
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private bool IsEntryDataExist(string pdbId, string buType)
        {
            string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", 
                BuCompTables.buCompTables[BuCompTables.BuInterfaces].TableName, pdbId);
            DataTable buInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (buInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        public void UpdateBuInterfaces(string[] pdbIds)
        {
            if (BuCompBuilder.logWriter == null)
            {
                BuCompBuilder.logWriter = new StreamWriter("BuCompBuilderLog.txt", true);
                BuCompBuilder.logWriter.WriteLine(DateTime.Today.ToShortDateString());
                BuCompBuilder.logWriter.WriteLine("Update chain interfaces of biological assemblies");
            }

            if (BuCompBuilder.BuType == "asu")
            {
                intraChainDomainInterfaces = new AsuIntraChainDomainInterfaces();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update BU Interfaces for " + BuCompBuilder.BuType);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "BU Interfaces";

            ProtCidSettings.progressInfo.totalOperationNum = pdbIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbIds.Length;

            foreach (string pdbId in pdbIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                ClearObsoleteData(pdbId);
                RetrieveEntryBUInterfaces(pdbId);
            }
            BuCompBuilder.logWriter.WriteLine("Update chain interfaces of biological assemblies done!");
//            BuCompBuilder.logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
       //     ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// remove existing data for those updated entries
        /// </summary>
        /// <param name="pdbIds"></param>
        private void ClearObsoleteData(string[] pdbIds)
        {
            string deleteString = "";
            foreach (DataTable buCompTable in BuCompTables.buCompTables)
            {
                if (buCompTable.TableName.IndexOf("PfamRelation") > -1)
                {
                    continue;
                }
                foreach (string pdbId in pdbIds)
                {
                    deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", 
                        buCompTable.TableName, pdbId);
                    dbUpdate.Delete (ProtCidSettings.buCompConnection, deleteString);
                }
            }
        }

        /// <summary>
        /// remove existing data for the updated entry
        /// </summary>
        /// <param name="pdbIds"></param>
        private void ClearObsoleteData(string pdbId)
        {
            string deleteString = "";
            foreach (DataTable buCompTable in BuCompTables.buCompTables)
            {
                if (buCompTable.TableName.IndexOf("PfamRelation") > -1)
                {
                    continue;
                }
                deleteString = string.Format("Delete From {0} Where PdbID = '{1}';",
                    buCompTable.TableName, pdbId);
                dbUpdate.Delete (ProtCidSettings.buCompConnection, deleteString);
            }
        }
        /// <summary>
        /// Retrieve interfaces from BUs of an entry
        /// compare BUs
        /// insert data into db
        /// </summary>
        /// <param name="pdbId"></param>
        private void RetrieveEntryBUInterfaces(string pdbId)
        {
            string[] nonMonomerBUs = null;
            Dictionary<string, Dictionary<int,int>> buEntityContentHash = null;

            if (BuCompBuilder.BuType == "pdb")
            {
                string[] pdbBuIds = GetPdbDefinedBiolUnits(pdbId);

                buEntityContentHash = GetPdbEntryBUEntityContent(pdbId, pdbBuIds);

                nonMonomerBUs = GetNonMonomerBUs(buEntityContentHash);
            }           
            else if (BuCompBuilder.BuType == "pisa")
            {
                buEntityContentHash = GetPisaEntryBUEntityContent(pdbId);

                nonMonomerBUs = GetNonMonomerBUs(buEntityContentHash);
            }
            else
            {
                buEntityContentHash = GetAsuEntityContent(pdbId);
                nonMonomerBUs = new string[1];
                nonMonomerBUs[0] = "0";
            }
            if (nonMonomerBUs.Length == 0)
            {
                BuCompBuilder.logWriter.WriteLine(pdbId + " monomer.");
                return;
            }

            try
            {
                // 1. Retrieve interfaces from all non-monomer BUs, 
                // 2. compare interfaces to get unique interfaces
                Dictionary<string, InterfaceChains[]> buInterfacesHash = GetBuInterfacesInEntry(pdbId, nonMonomerBUs);
                // compare unique interfaces from each BU
                CompareEntryBUs(pdbId, buInterfacesHash, buEntityContentHash);              
          //      buDomainInterfaces.GetEntryBuDomainInterfaces(pdbId, buInterfacesHash);
                
                // insert data into db
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, BuCompTables.buCompTables);
                BuCompTables.ClearTables();
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": " + ex.Message);
                BuCompBuilder.logWriter.WriteLine("Retrieve " + pdbId + " BU interfaces errors: " + ex.Message);
                BuCompTables.ClearTables();
            }
#if DEBUG
            BuCompBuilder.logWriter.WriteLine(pdbId);
            BuCompBuilder.logWriter.Flush();
#endif
        }
        #endregion

        #region non-monomer BUs
        /// <summary>
        /// the biological units defined by PDB
        /// 1. Author_defined
        /// 2. Author_and_Software_defined
        /// 3. if only software_defined, pick the BUs
        /// 4. all other non software_defined
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetPdbDefinedBiolUnits(string pdbId)
        {
            Dictionary<string, string> buOligomerHash = new Dictionary<string,string> ();

            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string details = "";
            string buId = "";
            string oligomer = "";
            foreach (DataRow buStatRow in buStatTable.Rows)
            {
                details = buStatRow["Details"].ToString().TrimEnd();
                buId = buStatRow["BiolUnitID"].ToString();
                oligomer = buStatRow["Oligomeric_Details"].ToString().TrimEnd();
                if (details.IndexOf("author_") > -1)
                {
                    buOligomerHash.Add(buId, oligomer);
                }
                else if (details == "software_defined_assembly")
                {
                    continue;
                }
                else
                {
                    buOligomerHash.Add(buId, oligomer);
                }
            }
            if (buOligomerHash.Count == 0) // only software_defined_assembly
            {
                foreach (DataRow buStatRow in buStatTable.Rows)
                {
                    buId = buStatRow["BiolUnitID"].ToString();
                    oligomer = buStatRow["Oligomeric_Details"].ToString().TrimEnd();
                    buOligomerHash.Add(buId, oligomer);
                }
            }
            // remove monomers
            List<string> buIdList = new List<string> ();
            foreach (string keyBuId in buOligomerHash.Keys)
            {
                if (buOligomerHash[keyBuId] == "monomeric")
                {
                    continue;
                }
                buIdList.Add(keyBuId);
            }

            return buIdList.ToArray ();
        }

        /// <summary>
        /// the biological units defined by PDB
        /// multimer only
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public string[] GetAllPdbDefinedMultimerBiolUnits(string pdbId)
        {
            Dictionary<string, string> buOligomerHash = new Dictionary<string,string> ();

            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string buId = "";
            string oligomer = "";
            foreach (DataRow buStatRow in buStatTable.Rows)
            {
                buId = buStatRow["BiolUnitID"].ToString();
                oligomer = buStatRow["Oligomeric_Details"].ToString().TrimEnd();
                buOligomerHash.Add(buId, oligomer);
            }
            // remove monomers
            List<string> buIdList = new List<string> ();
            foreach (string keyBuId in buOligomerHash.Keys)
            {
                if (buOligomerHash[keyBuId] == "monomeric")
                {
                    continue;
                }
                buIdList.Add(keyBuId);
            }
            return buIdList.ToArray ();
        }
        /// <summary>
        /// non-monomer BUs based on the entity and the number of copies 
        /// </summary>
        /// <param name="buEntityContentHash"></param>
        /// <returns></returns>
        public string[] GetNonMonomerBUs(Dictionary<string, Dictionary<int, int>> buEntityContentHash)
        {
            List<string> nonMonomerBuList = new List<string> ();
            foreach (string buId in buEntityContentHash.Keys)
            {
                if (!IsBuAMonomer(buEntityContentHash[buId]))
                {
                    nonMonomerBuList.Add(buId);
                }
            }
            return nonMonomerBuList.ToArray ();
        }

        /// <summary>
        /// if the BU contains only one entity, it is a monomer
        /// </summary>
        /// <param name="buEntityCountHash"></param>
        /// <returns></returns>
        private bool IsBuAMonomer(Dictionary<int, int> buEntityCountHash)
        {
            List<int> entityIdList = new List<int> (buEntityCountHash.Keys);
            if (buEntityCountHash.Count == 1)
            {
                if (buEntityCountHash[entityIdList[0]] == 1)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region interfaces from each BU of an entry
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public Dictionary<string, InterfaceChains[]> GetBuInterfacesInEntry(string pdbId, string[] nonMonomerBUs)
        {
            Dictionary<string, InterfaceChains[]> buInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnits = buRetriever.GetEntryBiolUnits(pdbId, nonMonomerBUs);
            if (BuCompBuilder.BuType == "asu" )
            {
                // asymmetric unit with buid = "0";
                intraChainDomainInterfaces.GetIntraChainDomainInterfaces(pdbId, entryBiolUnits["0"]);
            }
    //        GetEntityContentForEntryBUs(pdbId, entryBiolUnits);
            foreach (string buId in entryBiolUnits.Keys)
            {
                Dictionary<string, AtomInfo[]> biolUnit = entryBiolUnits[buId];
                if (biolUnit == null || biolUnit.Count == 0)
                {
                    continue;
                }
                // if biolunit is too big, then just calculate the asymmetric unit part, hope cover all possible interfaces
                if (biolUnit.Count > maxChainNumOfBU)  
                {
                    biolUnit = LimitBigBiolUnitsToAsymUnit(biolUnit);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                       ("Total protein chains greater than the maximum(128): " +
                       pdbId + ", " + buId + " with total chain number: " + biolUnit.Count.ToString());

                    BuCompBuilder.logWriter.WriteLine("Total protein chains greater than the maximum(128): " +
                        pdbId + ", " + buId + " with total chain number: " + biolUnit.Count.ToString());
                    BuCompBuilder.logWriter.WriteLine("Parsed the asymmetric unit part");
                }
                InterfaceChains[] uniqueInterfacesInBu = GetUniqueInterfacesInBiolUnit(pdbId, buId, biolUnit);
                if (uniqueInterfacesInBu != null)
                {
                    buInterfacesHash.Add(buId, uniqueInterfacesInBu);
                }
            }
            return buInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="biolUnit"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> LimitBigBiolUnitsToAsymUnit(Dictionary<string, AtomInfo[]> biolUnit)
        {
            Dictionary<string, AtomInfo[]> asuChainInBuHash = new Dictionary<string,AtomInfo[]> ();
            List<string> asuChainList = new List<string>();
            string chainId = "";
            string symOpString = "";
            int chainIndex = 0;
            foreach (string chainAndSymOp in biolUnit.Keys)
            {
                chainIndex = chainAndSymOp.IndexOf("_");
                chainId = chainAndSymOp.Substring(0, chainIndex);
                symOpString = chainAndSymOp.Substring(chainIndex + 1, chainAndSymOp.Length - chainIndex - 1);
                if (symOpString == "1_555")
                {
                    asuChainList.Add(chainAndSymOp);
                }
            }
            foreach (string asuChain in asuChainList)
            {
                asuChainInBuHash.Add(asuChain, biolUnit[asuChain]);
            }
            return asuChainInBuHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public Dictionary<string, InterfaceChains[]> GetBuInterfacesInEntryInDB(string pdbId, string[] nonMonomerBUs)
        {
            Dictionary<string, InterfaceChains[]> buInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnits = buRetriever.GetEntryBiolUnits(pdbId, nonMonomerBUs);
            if (BuCompBuilder.BuType == "asu")
            {
                // asymmetric unit with buid = "0";
                intraChainDomainInterfaces.GetIntraChainDomainInterfaces(pdbId, entryBiolUnits["0"]);
            }
            //        GetEntityContentForEntryBUs(pdbId, entryBiolUnits);
            foreach (string buId in entryBiolUnits.Keys)
            {
                Dictionary<string, AtomInfo[]> biolUnit = entryBiolUnits[buId];
                if (biolUnit == null || biolUnit.Count == 0)
                {
                    continue;
                }
                InterfaceChains[] uniqueInterfacesInBu = GetUniqueBUInterfacesInDB (pdbId, buId, biolUnit);
                if (uniqueInterfacesInBu != null)
                {
                    buInterfacesHash.Add(buId, uniqueInterfacesInBu);
                }
            }
            return buInterfacesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public Dictionary<string, InterfaceChains[]> GetAllBuInterfacesInEntry(string pdbId, string[] nonMonomerBUs)
        {
            Dictionary<string, InterfaceChains[]> buInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnits = buRetriever.GetEntryBiolUnits(pdbId, nonMonomerBUs);
            if (BuCompBuilder.BuType == "asu" && BuCompTables.intraChainDomainInterfaceTable != null)
            {
                // asymmetric unit with buid = "1";
                intraChainDomainInterfaces.GetIntraChainDomainInterfaces(pdbId, entryBiolUnits["0"]);
            }
            //        GetEntityContentForEntryBUs(pdbId, entryBiolUnits);
            foreach (string buId in entryBiolUnits.Keys)
            {
                Dictionary<string, AtomInfo[]> biolUnit = entryBiolUnits[buId];
                if (biolUnit == null || biolUnit.Count == 0)
                {
                    continue;
                }
                InterfaceChains[] interfacesInBu = GetInterfacesInBiolUnit (pdbId, buId, biolUnit);
                if (interfacesInBu != null)
                {
                    buInterfacesHash.Add(buId, interfacesInBu);
                }
            }
            return buInterfacesHash;
        }
        #endregion

        #region interfaces from BU
        /// <summary>
        /// get chain contacts (interfaces) in the biological unit
        /// </summary>
        /// <param name="biolUnit">key: chainid, value: atom list</param>
        /// <returns></returns>
        public InterfaceChains[] GetUniqueBUInterfacesInDB (string pdbId, string buId, Dictionary<string, AtomInfo[]> biolUnit)
        {
            if (biolUnit == null || biolUnit.Count < 2)
            {
                return null;
            }

            string queryString = string.Format("Select * From {0} Where PdbID = '{1}' AND BuID = '{2}' AND InterfaceID = SameInterfaceID;",
                BuCompTables.buCompTables[BuCompTables.BuSameInterfaces].TableName, pdbId, buId);
            DataTable buInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            List<InterfaceChains> interChainsList = new List<InterfaceChains> ();
            string chainSymOp1 = "";
            string chainSymOp2 = "";
            foreach (DataRow buInterfaceRow in buInterfaceTable.Rows)
            {
                chainSymOp1 = buInterfaceRow["Chain1"].ToString().TrimEnd() + "_" + buInterfaceRow["SymmetryString1"].ToString().TrimEnd();
                chainSymOp2 = buInterfaceRow["Chain2"].ToString().TrimEnd() + "_" + buInterfaceRow["SymmetryString2"].ToString().TrimEnd();
                InterfaceChains interfaceChains = new InterfaceChains(chainSymOp1, chainSymOp2);
                // no need to change the tree node data
                // only assign the refereces
                interfaceChains.chain1 = (AtomInfo[])biolUnit[chainSymOp1];
                interfaceChains.chain2 = (AtomInfo[])biolUnit[chainSymOp2];
                interfaceChains.interfaceId = Convert.ToInt32 (buInterfaceRow["InterfaceID"].ToString ());
                interfaceChains.GetInterfaceResidueDist();
                interChainsList.Add(interfaceChains);
            }
            return interChainsList.ToArray ();
        }

        /// <summary>
        /// get chain contacts (interfaces) in the biological unit
        /// </summary>
        /// <param name="biolUnit">key: chainid, value: atom list</param>
        /// <returns></returns>
        public InterfaceChains[] GetUniqueInterfacesInBiolUnit(string pdbId, string buId, Dictionary<string, AtomInfo[]> biolUnit)
        {
            if (biolUnit == null || biolUnit.Count < 2)
            {
                return null;
            }
            // build trees for the biological unit
            Dictionary<string, BVTree> buChainTreesHash = BuildBVtreesForBiolUnit(biolUnit);

            // calculate interfaces
            List<InterfaceChains> interChainsList = new List<InterfaceChains> ();
            List<string> keyList = new List<string> (buChainTreesHash.Keys);
            keyList.Sort();
            int interChainId = 0;
            for (int i = 0; i < keyList.Count - 1; i++)
            {
                for (int j = i + 1; j < keyList.Count; j++)
                {
                    ChainContact chainContact = new ChainContact(keyList[i].ToString(), keyList[j].ToString());
                    ChainContactInfo contactInfo = chainContact.GetChainContactInfo(buChainTreesHash[keyList[i]], buChainTreesHash[keyList[j]]);
                    if (contactInfo != null)
                    {
                        interChainId++;

                        InterfaceChains interfaceChains = new InterfaceChains(keyList[i].ToString(), keyList[j].ToString());
                        // no need to change the tree node data
                        // only assign the refereces
                        interfaceChains.chain1 = ((BVTree)buChainTreesHash[keyList[i]]).Root.AtomList;
                        interfaceChains.chain2 = ((BVTree)buChainTreesHash[keyList[j]]).Root.AtomList;
                        interfaceChains.interfaceId = interChainId;
                        interfaceChains.seqDistHash = contactInfo.GetDistHash();
                        interfaceChains.seqContactHash = contactInfo.GetContactsHash();
                        interChainsList.Add(interfaceChains);
                        //chainContact = null;
                    }
                }
            }
            InterfaceChains[] interChainArray = new InterfaceChains[interChainsList.Count];
            interChainsList.CopyTo(interChainArray);
            InterfacePairInfo[] interfaceCompPairs = interfaceComp.CompareInterfacesWithinCrystal (ref interChainArray);
            InterfaceChains[] uniqueInterfacesInBu = GetTheUniqueInterfaces(interChainArray, interfaceCompPairs);
            InsertDataIntoTables(pdbId, buId, interfaceCompPairs, uniqueInterfacesInBu);
            return uniqueInterfacesInBu;
        }

        /// <summary>
        /// get chain contacts (interfaces) in the biological unit
        /// </summary>
        /// <param name="biolUnit">key: chainid, value: atom list</param>
        /// <returns></returns>
        public InterfaceChains[] GetInterfacesInBiolUnit(string pdbId, string buId, Dictionary<string, AtomInfo[]> biolUnit)
        {
            if (biolUnit == null || biolUnit.Count < 2)
            {
                return null;
            }
            // build trees for the biological unit
            Dictionary<string, BVTree> buChainTreesHash = BuildBVtreesForBiolUnit(biolUnit);

            // calculate interfaces
            List<InterfaceChains> interChainsList = new List<InterfaceChains> ();
            List<string> keyList = new List<string> (buChainTreesHash.Keys);
            keyList.Sort();
            int interChainId = 0;
            for (int i = 0; i < keyList.Count - 1; i++)
            {
                for (int j = i + 1; j < keyList.Count; j++)
                {
                    ChainContact chainContact = new ChainContact(keyList[i].ToString(), keyList[j].ToString());
                    ChainContactInfo contactInfo = chainContact.GetChainContactInfo(buChainTreesHash[keyList[i]], buChainTreesHash[keyList[j]]);
                    if (contactInfo != null)
                    {
                        interChainId++;

                        InterfaceChains interfaceChains = new InterfaceChains(keyList[i].ToString(), keyList[j].ToString());
                        // no need to change the tree node data
                        // only assign the refereces
                        interfaceChains.chain1 = ((BVTree)buChainTreesHash[keyList[i]]).Root.AtomList;
                        interfaceChains.chain2 = ((BVTree)buChainTreesHash[keyList[j]]).Root.AtomList;
                        interfaceChains.interfaceId = interChainId;
                        interfaceChains.seqDistHash = contactInfo.GetDistHash();
                        interfaceChains.seqContactHash = contactInfo.GetContactsHash();
                        interChainsList.Add(interfaceChains);
                        //chainContact = null;
                    }
                }
            }
            return interChainsList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceList"></param>
        /// <param name="compPairs"></param>
        /// <returns></returns>
        private InterfaceChains[] GetTheUniqueInterfaces(InterfaceChains[] interfaceList, InterfacePairInfo[] compPairs)
        {
            List<int> uniqueInterfaceIdList = new List<int> ();
            foreach (InterfaceChains oneInterface in interfaceList)
            {
                uniqueInterfaceIdList.Add (oneInterface.interfaceId);
            }
            int interfaceId1 = -1;
            int interfaceId2 = -1;
            foreach (InterfacePairInfo compPair in compPairs)
            {
                interfaceId1 = compPair.interfaceInfo1.interfaceId;
                interfaceId2 = compPair.interfaceInfo2.interfaceId;
                if (compPair.qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                {
                    uniqueInterfaceIdList.Remove(interfaceId2);
                }
            }
            List<InterfaceChains> uniqueInterfaceList = new List<InterfaceChains> ();
            foreach (InterfaceChains oneInterface in interfaceList)
            {
                if (uniqueInterfaceIdList.Contains(oneInterface.interfaceId))
                {
                    uniqueInterfaceList.Add(oneInterface);
                }
            }
            return uniqueInterfaceList.ToArray ();
        }

        /// <summary>
        /// build BVtrees for chains in a biological unit
        /// </summary>
        /// <param name="biolUnit"></param>
        /// <returns></returns>
        public Dictionary<string, BVTree> BuildBVtreesForBiolUnit(Dictionary<string, AtomInfo[]> biolUnitHash)
        {
           Dictionary<string, BVTree> chainTreesHash = new Dictionary<string,BVTree> ();
            // for each chain in the biological unit
            // build BVtree
            foreach (string chainAndSymOp in biolUnitHash.Keys)
            {
                BVTree chainTree = new BVTree();
                chainTree.BuildBVTree(biolUnitHash[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
                chainTreesHash.Add(chainAndSymOp, chainTree);
            }
            return chainTreesHash;
        }
        #endregion

        #region insert data into tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceCompPairs"></param>
        /// <param name="uniqueInterfaces"></param>
        private void InsertDataIntoTables(string pdbId, string buId, InterfacePairInfo[] interfaceCompPairs, InterfaceChains[] uniqueInterfaces)
        {
            int uniqueInterfaceId = 0;
            string authChain1 = "";
            string authChain2 = "";
            int entityId1 = -1;
            int entityId2 = -1;
            foreach (InterfaceChains theInterface in uniqueInterfaces)
            {
                uniqueInterfaceId++;
                theInterface.interfaceId = uniqueInterfaceId;
                // for itself
                int numOfCopies = 1;
                DataRow interfaceRow = BuCompTables.buCompTables[BuCompTables.BuInterfaces].NewRow();
                interfaceRow["PdbID"] = pdbId;
                interfaceRow["BuID"] = buId;
                interfaceRow["InterfaceID"] = uniqueInterfaceId;

                interfaceRow["AsymChain1"] = theInterface.firstSymOpString.Substring(0, theInterface.firstSymOpString.IndexOf("_"));
                interfaceRow["AsymChain2"] = theInterface.secondSymOpString.Substring(0, theInterface.secondSymOpString.IndexOf("_"));
                FindAuthChainAndEntityIdFromAsymChain(pdbId, interfaceRow["AsymChain1"].ToString(),
                    out authChain1, out entityId1);
                FindAuthChainAndEntityIdFromAsymChain(pdbId, interfaceRow["AsymChain2"].ToString(),
                    out authChain2, out entityId2);
                interfaceRow["AuthChain1"] = authChain1;
                interfaceRow["AuthChain2"] = authChain2;
                interfaceRow["EntityID1"] = entityId1;
                interfaceRow["EntityID2"] = entityId2;


                // add data to same interfaces table
                // add the first unique interface
                DataRow sameInterfaceRow = BuCompTables.buCompTables[BuCompTables.BuSameInterfaces].NewRow();
                sameInterfaceRow["PdbID"] = pdbId;
                sameInterfaceRow["BuID"] = buId;
                sameInterfaceRow["InterfaceID"] = uniqueInterfaceId;
                sameInterfaceRow["SameInterfaceID"] = theInterface.interfaceId;

                string[] chainSymOpStrings = theInterface.firstSymOpString.Split('_');
                sameInterfaceRow["Chain1"] = chainSymOpStrings[0];
                if (chainSymOpStrings.Length == 3)
                {
                    sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                }
                else if (chainSymOpStrings.Length == 2)
                {
                    sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1];
                }
                chainSymOpStrings = theInterface.secondSymOpString.Split('_');
                sameInterfaceRow["Chain2"] = chainSymOpStrings[0];
                if (chainSymOpStrings.Length == 3)
                {
                    sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                }
                else
                {
                    sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1];
                }
                sameInterfaceRow["QScore"] = 1;
                BuCompTables.buCompTables[BuCompTables.BuSameInterfaces].Rows.Add(sameInterfaceRow);

                // add same interfaces
                foreach (InterfacePairInfo pairInfo in interfaceCompPairs)
                {
                    if (pairInfo.qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                    {
                        if (pairInfo.interfaceInfo1 == (InterfaceInfo)theInterface)
                        {
                            numOfCopies++;
                            sameInterfaceRow = BuCompTables.buCompTables[BuCompTables.BuSameInterfaces].NewRow();
                            sameInterfaceRow["PdbID"] = pdbId;
                            sameInterfaceRow["BuID"] = buId;
                            sameInterfaceRow["InterfaceID"] = uniqueInterfaceId;
                            sameInterfaceRow["SameInterfaceID"] = pairInfo.interfaceInfo2.interfaceId;

                            chainSymOpStrings = pairInfo.interfaceInfo2.firstSymOpString.Split('_');
                            sameInterfaceRow["Chain1"] = chainSymOpStrings[0];
                            if (chainSymOpStrings.Length == 3)
                            {
                                sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                            }
                            else if (chainSymOpStrings.Length == 2)
                            {
                                sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1];
                            }
                            chainSymOpStrings = pairInfo.interfaceInfo2.secondSymOpString.Split('_');
                            sameInterfaceRow["Chain2"] = chainSymOpStrings[0];
                            if (chainSymOpStrings.Length == 3)
                            {
                                sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                            }
                            else if (chainSymOpStrings.Length == 2)
                            {
                                sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1];
                            }
                            sameInterfaceRow["QScore"] = pairInfo.qScore;
                            BuCompTables.buCompTables[BuCompTables.BuSameInterfaces].Rows.Add(sameInterfaceRow);
                        }
                    }
                }
                interfaceRow["NumOfCopy"] = numOfCopies;
                // have to compute surface area later
                interfaceRow["SurfaceArea"] = -1.0;
                BuCompTables.buCompTables[BuCompTables.BuInterfaces].Rows.Add(interfaceRow);
            }
        }

        /// <summary>
        /// the author chain and entity id for the input entry and asymmetric chain id
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="authorChain"></param>
        /// <param name="entityId"></param>
        private void FindAuthChainAndEntityIdFromAsymChain(string pdbId, string asymChain,
           out string authorChain, out int entityId)
        {
            authorChain = "-";
            entityId = -1;
            string queryString = string.Format("Select AuthorChain, EntityID From AsymUnit " + 
                " Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable entityAuthChainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (entityAuthChainInfoTable.Rows.Count > 0)
            {
                authorChain = entityAuthChainInfoTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                entityId = Convert.ToInt32 (entityAuthChainInfoTable.Rows[0]["EntityID"].ToString());
            }
        }
        /// <summary>
        /// The asymmetric chain for the input author chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        private string FindAsymChainFromAuthChain(string pdbId, string authorChain)
        {
            string queryString = string.Format("Select AsymID From AsymUnit " + 
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", 
                pdbId, authorChain);
            DataTable asymChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (asymChainTable.Rows.Count > 0)
            {
                return asymChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
            }
            return "-";
        }
        #endregion

        #region compare BUs for an entry
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buInterfacesHash"></param>
        public void CompareEntryBUs(string pdbId, Dictionary<string, InterfaceChains[]> buInterfacesHash, Dictionary<string, Dictionary<int, int>> buEntityContentHash)
        {
            if (buInterfacesHash.Count < 2)
            {
                return;
            }
            List<string> buIdList = new List<string> (buInterfacesHash.Keys);
            buIdList.Sort();
            bool sameBUs = false;
            for (int i = 0; i < buIdList.Count; i++)
            {
                InterfaceChains[] buInterfaces1 = buInterfacesHash[buIdList[i]];
                int[] interfaceIdList1 = GetInterfaceIdList(buInterfaces1);
                for (int j = i + 1; j < buIdList.Count; j++)
                {
                    InterfaceChains[] buInterfaces2 = (InterfaceChains[])buInterfacesHash[buIdList[j]];
                    int[] interfaceIdList2 = GetInterfaceIdList(buInterfaces2);
                    InterfacePairInfo[] compPairInfos = interfaceComp.CompareInterfacesBetweenCrystals(buInterfaces1, buInterfaces2);
                    InsertBuCompDataToTable(pdbId, buIdList[i].ToString(), buIdList[j].ToString(), compPairInfos);
                    sameBUs = AreEntryBUsSame(pdbId, interfaceIdList1, interfaceIdList2, compPairInfos);
                    InsertBuCompDataToTable (pdbId, buIdList[i].ToString (), buIdList[j].ToString (), 
                       interfaceIdList1.Count (), interfaceIdList2.Count (), sameBUs, buEntityContentHash);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterfaces"></param>
        /// <returns></returns>
        private int[] GetInterfaceIdList (InterfaceChains[] buInterfaces)
        {
            int[] interfaceIdList =  new int[buInterfaces.Length];
            for (int i = 0; i < buInterfaces.Length; i++)
            {
                interfaceIdList[i] = buInterfaces[i].interfaceId;
            }
            return interfaceIdList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId1"></param>
        /// <param name="buId2"></param>
        /// <param name="compPairInfos"></param>
        private void InsertBuCompDataToTable(string pdbId, string buId1, string buId2, InterfacePairInfo[] compPairInfos)
        {
            foreach (InterfacePairInfo pairInfo in compPairInfos)
            {
                if (pairInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow dataRow = BuCompTables.buCompTables[BuCompTables.EntryBuInterfaceComp].NewRow();
                    dataRow["PdbID"] = pdbId;
                    dataRow["BuID1"] = buId1;
                    dataRow["BuID2"] = buId2;
                    dataRow["InterfaceID1"] = pairInfo.interfaceInfo1.interfaceId;
                    dataRow["InterfaceID2"] = pairInfo.interfaceInfo2.interfaceId;
                    dataRow["Qscore"] = pairInfo.qScore;
                    BuCompTables.buCompTables[BuCompTables.EntryBuInterfaceComp].Rows.Add(dataRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId1"></param>
        /// <param name="buId2"></param>
        /// <param name="numOfInterfaces1"></param>
        /// <param name="numOfInterfaces2"></param>
        /// <param name="sameBUs"></param>
        private void InsertBuCompDataToTable(string pdbId, string buId1, string buId2,
            int numOfInterfaces1, int numOfInterfaces2, bool sameBUs, Dictionary<string, Dictionary<int, int>>  buEntityContentHash)
        {
            DataRow dataRow = BuCompTables.buCompTables[BuCompTables.EntryBuComp].NewRow();
            dataRow["PdbID"] = pdbId;
            dataRow["BuID1"] = buId1;
            dataRow["BuID2"] = buId2;
            dataRow["EntityFormat1"] = GetEntityFormat (buEntityContentHash[buId1]);
            dataRow["EntityFormat2"] = GetEntityFormat (buEntityContentHash[buId2]);
            dataRow["NumOfInterfaces1"] = numOfInterfaces1;
            dataRow["NumOfInterfaces2"] = numOfInterfaces2;
            if (sameBUs)
            {
                dataRow["SameBUs"] = '1';
            }
            else
            {
                dataRow["SameBUs"] = '0';
            }
            BuCompTables.buCompTables[BuCompTables.EntryBuComp].Rows.Add(dataRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId1"></param>
        /// <param name="buId2"></param>
        /// <param name="compPairInfos"></param>
        /// <returns></returns>
        private bool AreEntryBUsSame(string pdbId, int[] buInterfaceIdList1, int[] buInterfaceIdList2, 
            InterfacePairInfo[] compPairInfos)
        {
            List<int> leftInterfaceIdList1 = new List<int> (buInterfaceIdList1);
            List<int> leftInterfaceIdList2 = new List<int>(buInterfaceIdList2);

            foreach (InterfacePairInfo compPair in compPairInfos)
            {
                if (compPair.qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                {
                    leftInterfaceIdList1.Remove(compPair.interfaceInfo1.interfaceId);
                    leftInterfaceIdList2.Remove(compPair.interfaceInfo2.interfaceId);
                }
            }

            if (leftInterfaceIdList1.Count == 0 && leftInterfaceIdList2.Count == 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Entity content for a BU
        /// <summary>
        /// find the entity-format for BUs which are not monomers
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="nonMonomerBUs"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<int,int>> GetPdbEntryBUEntityContent(string pdbId, string[] nonMonomerBUs)
        {
            if (nonMonomerBUs.Length == 0)
            {
                if (IsEntryNmr(pdbId))
                {
                    return GetAsuEntityContent(pdbId);
                }
            }
            string queryString = string.Format("Select BiolUnit.PdbID, BiolUnit.AsymID, BiolUnitID, NumOfAsymIDs" +
                " From BiolUnit, AsymUnit " +
                " Where BiolUnit.PdbID = '{0}' AND BiolUnit.PdbID = AsymUnit.PdbID " +
                " AND BiolUnit.AsymID = AsymUnit.AsymID " +
                " AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<string, int>> buChainCountHash = new Dictionary<string, Dictionary<string, int>>();
            string asymChain = "";
            int numOfCopies = -1;
            string buId = "";
            foreach (DataRow buChainRow in entryBuTable.Rows)
            {
                buId = buChainRow["BiolUnitID"].ToString().TrimEnd();
                if (Array.IndexOf(nonMonomerBUs, buId) < 0)
                {
                    continue;
                }
                asymChain = buChainRow["AsymID"].ToString().TrimEnd();
                numOfCopies = Convert.ToInt32(buChainRow["NumOfAsymIDs"]);
                if (buChainCountHash.ContainsKey(buId))
                {
                    buChainCountHash[buId].Add(asymChain, numOfCopies);
                }
                else
                {
                    Dictionary<string, int> chainCountHash = new Dictionary<string,int> ();
                    chainCountHash.Add(asymChain, numOfCopies);
                    buChainCountHash.Add(buId, chainCountHash);
                }
            }
           
            Dictionary<string, Dictionary<int, int>> buEntityContentHash = GetEntryBUEntityContent(pdbId, buChainCountHash);
            return buEntityContentHash;
        }


        /// <summary>
        /// find the entity-format for BUs which are not monomers
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<int, int>> GetPdbEntryBUEntityContent(string pdbId)
        {
            string queryString = string.Format("Select BiolUnit.PdbID, BiolUnit.AsymID, BiolUnitID, NumOfAsymIDs" +
                " From BiolUnit, AsymUnit " +
                " Where BiolUnit.PdbID = '{0}' AND BiolUnit.PdbID = AsymUnit.PdbID " +
                " AND BiolUnit.AsymID = AsymUnit.AsymID " +
                " AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<string, int>> buChainCountHash = new Dictionary<string, Dictionary<string, int>>();
            string asymChain = "";
            int numOfCopies = -1;
            string buId = "";
            foreach (DataRow buChainRow in entryBuTable.Rows)
            {
                buId = buChainRow["BiolUnitID"].ToString().TrimEnd();
                asymChain = buChainRow["AsymID"].ToString().TrimEnd();
                numOfCopies = Convert.ToInt32(buChainRow["NumOfAsymIDs"]);
                if (buChainCountHash.ContainsKey(buId))
                {
                    buChainCountHash[buId].Add(asymChain, numOfCopies);
                }
                else
                {
                    Dictionary<string, int> chainCountHash = new Dictionary<string, int>();
                    chainCountHash.Add(asymChain, numOfCopies);
                    buChainCountHash.Add(buId, chainCountHash);
                }
            }
            Dictionary<string, Dictionary<int,int>> buEntityContentHash = GetEntryBUEntityContent(pdbId, buChainCountHash);
            return buEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<int, int>> GetPisaEntryBUEntityContent(string pdbId)
        {
            string queryString = string.Format("Select * From PisaBuMatrix Where PdbID = '{0}';", pdbId);
            DataTable pisaBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            Dictionary<string, Dictionary<string, int>> buChainCountHash = new Dictionary<string, Dictionary<string, int>>();
            string buId = "";
            string asymChain = "";
            foreach (DataRow buRow in pisaBuTable.Rows)
            {
                buId = buRow["AssemblySeqID"].ToString();
                asymChain = buRow["AsymChain"].ToString().TrimEnd();
                if (buChainCountHash.ContainsKey(buId))
                {
                    if (buChainCountHash[buId].ContainsKey(asymChain))
                    {
                        int count = buChainCountHash[buId][asymChain];
                        count++;
                        buChainCountHash[buId][asymChain] = count;
                    }
                    else
                    {
                        buChainCountHash[buId].Add(asymChain, 1);
                    }
                }
                else
                {
                    Dictionary<string, int> chainCountHash = new Dictionary<string,int> ();
                    chainCountHash.Add(asymChain, 1);
                    buChainCountHash.Add(buId, chainCountHash);
                }
            }
            Dictionary<string, Dictionary<int, int>> buEntityContentHash = GetEntryBUEntityContent(pdbId, buChainCountHash);
            return buEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<int, int>> GetAsuEntityContent(string pdbId)
        {
            string queryString = string.Format("Select EntityID From AsymUNit " + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<int, int> entityCountHash = new Dictionary<int,int> ();
            int entityId = -1;
            
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString ());
                if (entityCountHash.ContainsKey(entityId))
                {
                    int count = (int)entityCountHash[entityId];
                    count++;
                    entityCountHash[entityId] = count;
                }
                else
                {
                    entityCountHash.Add(entityId, 1);
                }
            }
            Dictionary<string, Dictionary<int, int>> buEntityCountHash = new Dictionary<string,Dictionary<int,int>> ();
            buEntityCountHash.Add("1", entityCountHash); // fit into the format with BUs
            return buEntityCountHash;
        }
        /// <summary>
        /// the entity format for BUs based on the asymmetric chains and their copies in the BUs
        /// </summary>
        /// <param name="buChainCountHash"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, int>> GetEntryBUEntityContent(string pdbId, Dictionary<string, Dictionary<string, int>> buChainCountHash)
        {
  /*          ArrayList buList = new ArrayList(buChainCountHash.Keys);
            foreach (string thisBuId in buList)
            {
                Hashtable chainCountHash = (Hashtable)buChainCountHash[thisBuId];
                int totalChainCount = 0;
                foreach (string chain in chainCountHash.Keys)
                {
                    totalChainCount += (int)chainCountHash[chain];
                }
                if (totalChainCount > maxChainNumOfBU)
                {
                    buChainCountHash.Remove(thisBuId);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Total protein chains greater than the maximum(128): " +
                        pdbId + ", " + thisBuId + " with total chain number: " + totalChainCount.ToString());

                    BuCompBuilder.logWriter.WriteLine("Total protein chains greater than the maximum(128): " +
                        pdbId + ", " + thisBuId + " with total chain number: " + totalChainCount.ToString());
                    BuCompBuilder.logWriter.WriteLine("Remove this BU");
                    BuCompBuilder.logWriter.Flush();
                }
            }*/

            Dictionary<string, Dictionary<int, int>> buEntityContentHash = new Dictionary<string, Dictionary<int, int>>();
            int entityId = -1;
            foreach (string keyBuId in buChainCountHash.Keys)
            {
                Dictionary<int, int> entityCountHash = new Dictionary<int,int> ();
                foreach (string asymId in buChainCountHash[keyBuId].Keys)
                {
                    entityId = GetEntityFromAsymChain(pdbId, asymId);
                    if (entityId < 1)
                    {
                        continue;
                    }
                    int chainCount = buChainCountHash[keyBuId][asymId];
                    if (entityCountHash.ContainsKey(entityId))
                    {
                        int count = (int)entityCountHash[entityId];
                        count += chainCount;
                        entityCountHash[entityId] = count;
                    }
                    else
                    {
                        entityCountHash.Add(entityId, chainCount);
                    }
                }
                buEntityContentHash.Add(keyBuId, entityCountHash);
            }
            return buEntityContentHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityCountHash"></param>
        /// <returns></returns>
        private string GetEntityFormat(Dictionary<int, int> entityCountHash)
        {
            string entityFormat = "";
            List<int> entityList = new List<int> (entityCountHash.Keys);
            entityList.Sort();
            foreach (int keyEntityId in entityList)
            {
                int count = entityCountHash[keyEntityId];
                entityFormat += ("(" + keyEntityId.ToString() + "." + count.ToString() + ")");
            }
            return entityFormat;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private int GetEntityFromAsymChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit " + 
                " Where PdbID = '{0}' AND AsymID = '{1}' AND POlymerType = 'polypeptide';", pdbId, asymChain);
            DataTable entityTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (entityTable.Rows.Count > 0)
            {
                return Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString ());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryNmr(string pdbId)
        {
            string queryString = string.Format("Select Method From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable entryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (entryTable.Rows.Count > 0)
            {
                string method = entryTable.Rows[0]["Method"].ToString().TrimEnd();
                if (method.IndexOf("NMR") > -1)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region retrieve domain-domain interactions from BAs
        private StreamWriter difDomainEntryWriter = new StreamWriter("DifBuDomainInterfaceEntries.txt", true);
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveBADomainInterfaces()
        {
            if (BuCompBuilder.logWriter == null)
            {
                BuCompBuilder.logWriter = new StreamWriter("BuCompBuilderLog.txt", true);
                BuCompBuilder.logWriter.WriteLine(DateTime.Today.ToShortDateString());
                BuCompBuilder.logWriter.WriteLine("Retrieve domain interactions in biological assemblies");
            }
            
         //   string[] buTypes = { "pisa" };
        //    string pdbId = "";
            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            foreach (string buType in BuCompBuilder.buTypes)
            {
                BuCompBuilder.logWriter.WriteLine(buType);

                BuCompBuilder.BuType = buType;
                BuCompTables.InitializeTables();

                string[] leftEntries = GetEntriesNotInDomainInterfaceDb(protEntryTable);

                if (BuCompBuilder.BuType == "asu")
                {
                    intraChainDomainInterfaces = new AsuIntraChainDomainInterfaces();
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve BU Interfaces.");
                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.currentOperationLabel = "BU Interfaces";

               // ProtCidSettings.progressInfo.totalOperationNum = protEntryTable.Rows.Count;
              //  ProtCidSettings.progressInfo.totalStepNum = protEntryTable.Rows.Count;

                ProtCidSettings.progressInfo.totalOperationNum = leftEntries.Length;
                ProtCidSettings.progressInfo.totalStepNum = leftEntries.Length;

              //  foreach (DataRow entryRow in protEntryTable.Rows)
                foreach (string pdbId in leftEntries)
                {
                //    pdbId = entryRow["PdbID"].ToString();
                    ProtCidSettings.progressInfo.currentFileName = pdbId;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;
                    try
                    {
                        RetrieveEntryBADomainInterfaces(pdbId);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                        BuCompBuilder.logWriter.WriteLine(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                        BuCompBuilder.logWriter.Flush();
                    }
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(buType + " domain-domain interactions Done");
                BuCompBuilder.logWriter.WriteLine(buType + " domain-domain interactions Done!");
            }
            BuCompBuilder.logWriter.WriteLine("Domain interactions of biological assemblies done!");
  //          BuCompBuilder.logWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            difDomainEntryWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateBADomainInterfaces(string[] updateEntries)
        {
#if DEBUG
            if (BuCompBuilder.logWriter == null)
            {
                BuCompBuilder.logWriter = new StreamWriter("BuCompBuilderLog.txt", true);
                BuCompBuilder.logWriter.WriteLine(DateTime.Today.ToShortDateString());
                BuCompBuilder.logWriter.WriteLine("Update domain interfaces of biological assemblies");
            }
#endif
 //           string[] buTypes = { "asu", "pdb", "pisa" };

            foreach (string buType in BuCompBuilder.buTypes)
            {               
                BuCompBuilder.logWriter.WriteLine(buType);

                BuCompBuilder.BuType = buType;
                BuCompTables.InitializeTables();

                if (BuCompBuilder.BuType == "asu")
                {
                    intraChainDomainInterfaces = new AsuIntraChainDomainInterfaces();
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve BU Interfaces.");
                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.currentOperationLabel = "BU Interfaces";

                //        ProtCidSettings.progressInfo.totalOperationNum = protEntryTable.Rows.Count;
                //        ProtCidSettings.progressInfo.totalStepNum = protEntryTable.Rows.Count;

                ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
                ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

                //        foreach (DataRow entryRow in protEntryTable.Rows)
                foreach (string pdbId in updateEntries)
                {
                    //     pdbId = entryRow["PdbID"].ToString();
                    ProtCidSettings.progressInfo.currentFileName = pdbId;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    try
                    {
                        DeleteBuDomainInterfaces(pdbId);
                        RetrieveEntryBADomainInterfaces(pdbId);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                        BuCompBuilder.logWriter.WriteLine(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                        BuCompBuilder.logWriter.Flush();
                    }
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(buType + " domain-domain interactions Done");
            }
            BuCompBuilder.logWriter.WriteLine("Update domain interfaces of biological assemblies done!");
//            BuCompBuilder.logWriter.Close();
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateBADomainInterfaces(string[] updateEntries, string buType)
        {
#if DEBUG
            if (BuCompBuilder.logWriter == null)
            {
                BuCompBuilder.logWriter = new StreamWriter("BuCompBuilderLog.txt", true);
                BuCompBuilder.logWriter.WriteLine(DateTime.Today.ToShortDateString());
                BuCompBuilder.logWriter.WriteLine("Update domain interfaces of biological assemblies");
            }
#endif
            BuCompBuilder.logWriter.WriteLine(buType);

            BuCompBuilder.BuType = buType;
            BuCompTables.InitializeTables();

            if (BuCompBuilder.BuType == "asu")
            {
                intraChainDomainInterfaces = new AsuIntraChainDomainInterfaces();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve " + buType + " Interfaces.");
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "BU Interfaces";

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteBuDomainInterfaces(pdbId);
                    RetrieveEntryBADomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                    BuCompBuilder.logWriter.WriteLine(pdbId + " retrive domain interfaces in " + buType + "error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(buType + " domain-domain interactions Done");
            BuCompBuilder.logWriter.WriteLine(buType + " domain-domain interactions Done");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteBuDomainInterfaces(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} WHere PdbID = '{1}';", BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces].TableName, pdbId);
            dbUpdate.Delete(ProtCidSettings.buCompConnection, deleteString);
        }
        
        /// <summary>
        /// Retrieve interfaces from BUs of an entry
        /// compare BUs
        /// insert data into db
        /// </summary>
        /// <param name="pdbId"></param>
        public void RetrieveEntryBADomainInterfaces(string pdbId)
        {
            string[] nonMonomerBUs = null;
            Dictionary<string, Dictionary<int, int>> buEntityContentHash = null;

            if (BuCompBuilder.BuType == "pdb")
            {
                string[] pdbBuIds = GetPdbDefinedBiolUnits(pdbId);

                buEntityContentHash = GetPdbEntryBUEntityContent(pdbId, pdbBuIds);

                nonMonomerBUs = GetNonMonomerBUs(buEntityContentHash);
            }
            else if (BuCompBuilder.BuType == "pisa")
            {
                buEntityContentHash = GetPisaEntryBUEntityContent(pdbId);

                nonMonomerBUs = GetNonMonomerBUs(buEntityContentHash);
            }
            else
            {
                buEntityContentHash = GetAsuEntityContent(pdbId);
                nonMonomerBUs = new string[1];
                nonMonomerBUs[0] = "0";
            }
            if (nonMonomerBUs.Length == 0)
            {
#if DEBUG
                BuCompBuilder.logWriter.WriteLine(pdbId + " monomer.");
#endif
                return;
            }

            try
            {
                // 1. Retrieve interfaces from all non-monomer BUs, 
                // 2. compare interfaces to get unique interfaces
                Dictionary<string, InterfaceChains[]> buInterfacesHash = GetBuInterfacesInEntryInDB (pdbId, nonMonomerBUs);
                buDomainInterfaces.GetEntryBuDomainInterfaces(pdbId, buInterfacesHash);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces]);

                /*
                if (AreDomainInterfacesSame(pdbId, BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces]))
                {
                    return;
                }
                else
                {
                    difDomainEntryWriter.WriteLine(pdbId);
                    difDomainEntryWriter.Flush();

                    DeleteBuDomainInterfaces(pdbId);
                    // insert data into db
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces]);
                }*/
                BuCompTables.ClearTables();
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": " + ex.Message);
                BuCompBuilder.logWriter.WriteLine("Retrieve " + pdbId + " BU interfaces errors: " + ex.Message);
            }
#if DEBUG
            BuCompBuilder.logWriter.WriteLine(pdbId);
            BuCompBuilder.logWriter.Flush();
#endif
        }

        private bool AreDomainInterfacesSame(string pdbId, DataTable newBuDomainInterfaceTable)
        {
            string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", 
                BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces].TableName, pdbId);
            DataTable orgDomainInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string orgDomainInterfacesString = ParseHelper.FormatDataRows(orgDomainInterfaceTable.Select ());

            string newDomainInterfacesString = ParseHelper.FormatDataRows(newBuDomainInterfaceTable.Select ());
            if (orgDomainInterfacesString == newDomainInterfacesString)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesNotInDomainInterfaceDb(DataTable protEntryTable)
        {
            string queryString = string.Format("Select Distinct PdbID From {0};", BuCompTables.buCompTables[BuCompTables.BuDomainInterfaces]);
            DataTable existEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            List<string> missingEntryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataRow[] existRows = existEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                if (existRows.Length == 0)
                {
                    missingEntryList.Add(pdbId);
                }
            }
            return missingEntryList.ToArray ();
        }
        #endregion
    }
}
