using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using System.Net;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using InterfaceClusterLib.AuxFuncs;
using PfamLib.Settings;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    public class ComplexStructures : PaperDataInfo
    {
        #region member variables
        private string tempDir = @"X:\xtal_temp";
        private InterfaceReader interfaceReader = new InterfaceReader ();
        private InterfaceWriter interfaceWriter = new InterfaceWriter();
        private InterfaceSeqNumberConverter seqNumberConverter = new InterfaceSeqNumberConverter();
        private SeqNumbersMatchData seqNumMatchData = new SeqNumbersMatchData();
        private double contactOverlap = 0.10;  // 10%?
//        private double unpSeqOverlap = 0.25;
        private int pepLengthCutoff = 50;
        private double surfaceAreaCutoff = 300;
        private string[] excludedPfams = null;
        private string complexDataDir = @"X:\Qifang\Paper\protcid_update\data_v31\Hubs and Complexes\Complexes";
        private string linkedProteinFileDir = "";
        public StreamWriter logWriter = null;

        WebClient fileDownload = new WebClient();
        string unpWebHttp = "https://www.uniprot.org/uniprot/";
        private Dictionary<string, List<string>> geneUnpListDict = new Dictionary<string, List<string>>();
        private Dictionary<string, string> unpGeneListDict = new Dictionary<string, string>();
        private Dictionary<string, int> pfamPairRelIdDict = new Dictionary<string, int>();
        #endregion

        public ComplexStructures ()
        {
            Initialize();
            excludedPfams = GetExcludedPfams();
            linkedProteinFileDir = Path.Combine(complexDataDir, "LinkedHumanProteinsInPdb");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            if (! Directory.Exists (linkedProteinFileDir))
            {
                Directory.CreateDirectory(linkedProteinFileDir);
            }
            string logFile = Path.Combine(complexDataDir, "ComplexPdbStructuresLog.txt");
            logWriter = new StreamWriter(logFile, true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString ());
        }
            
        #region find pdb structructures for uniprot complexes identified from PDB
        /// <summary>
        /// 
        /// </summary>
        public void FindComplexStructureSamples ()
        {
  //          string complexFile = Path.Combine(complexDataDir, "PossibleTrimersInPdb_excludedPfams.txt");
  //          List<string[]> complexList = ReadComplexes(complexFile);
            int numLinkedProteins = 5;
            List<string[]> complexList = ReadInterestingLinkedProteins(numLinkedProteins);

            string complexStructuresFile = Path.Combine(complexDataDir, "PossiblePentemersPdbStructures.txt");
            StreamWriter dataWriter = new StreamWriter(complexStructuresFile);            
            foreach (string[] complexUniprots in complexList)
            {
                logWriter.WriteLine("#" + FormatArrayString(complexUniprots));
                try
                {
                    Dictionary<string[], List<string[]>> unpPairInterfaceListDict = FindComplexStructureSamples(complexUniprots);
                    if (unpPairInterfaceListDict == null || unpPairInterfaceListDict.Count == 0)
                    {
                        logWriter.WriteLine(FormatArrayString(complexUniprots) + " no good structure interfaces");
                        logWriter.Flush();
                    }
                    else
                    {
                        dataWriter.WriteLine("#" + FormatArrayString(complexUniprots));
                        foreach (string[] unpPair in unpPairInterfaceListDict.Keys)
                        {
                            dataWriter.WriteLine(unpPair[0] + " " + unpPair[1] + ": " + FormatStringPairList(unpPairInterfaceListDict[unpPair]));
                        }
                        dataWriter.WriteLine();
                        dataWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(FormatArrayString (complexUniprots) +  " retrieving pdb structures error: " + ex.Message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close();
            logWriter.Close();
        }

        private string FormatStringPairList (List<string[]> interfacePairList)
        {
            string pairListString = "";
            foreach (string[] interfacePair in interfacePairList)
            {
                pairListString += (interfacePair[0] + ";" + interfacePair[1] + " ");
            }
            return pairListString.TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <returns></returns>
        private List<string[]> ReadComplexes (string complexFile)
        {
            List<string[]> complexList = new List<string[]>();

            StreamReader dataReader = new StreamReader(complexFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }

                string[] uniprots = line.TrimStart('#').Split(' ');
                complexList.Add(uniprots);
            }
            dataReader.Close();
            return complexList;

        }
        /// <summary>
        /// the PDB structures of the complex uniprots
        /// </summary>
        /// <param name="complexUniprots"></param>
        public Dictionary<string[], List<string[]>> FindComplexStructureSamples (string[] complexUniprots)
        {
            string queryString = string.Format("Select Distinct UnpID1, UnpID2, PfamDomainInterfaces.PdbID, PfamDomainInterfaces.InterfaceID " +               
                " From UnpPdbDomainInterfaces, PfamDomainInterfaces" +
                " Where UnpID1 In ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2 AND " +
                " UnpPdbDomainInterfaces.PdbID = PfamDomainInterfaces.PdbID AND UnpPdbDomainInterfaces.DomainInterfaceID = PfamDomainInterfaces.DomainInterfaceID;", 
        //        " PfamDomainInterfaces.PdbID = CrystEntryInterfaces.PdbID AND PfamDomainInterfaces.InterfaceID = CrystEntryInterfaces.InterfaceID AND SurfaceArea > {1};",
                ParseHelper.FormatSqlListString(complexUniprots));
            DataTable unpDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            string[] commonUnps = GetCommonUniprots(unpDomainInterfaceTable);
            if (commonUnps.Length == 0)
            {
                return null;
            }
            Dictionary<string, List<string>> unpPairInterfaceListDict =  new Dictionary<string,List<string>> ();
            Dictionary<string, List<string>> unpPairEntryListDict = new Dictionary<string, List<string>>();
            string unpPair = "";
            string chainInterface = "";
            string pdbId = "";
            List<string> unpPairList = new List<string>();
            foreach (DataRow interfaceRow in unpDomainInterfaceTable.Rows)
            {
                unpPair = interfaceRow["UnpID1"].ToString().TrimEnd() + ";" + interfaceRow["UnpID2"].ToString().TrimEnd();
                chainInterface = interfaceRow["PdbID"] + interfaceRow["InterfaceID"].ToString();
                pdbId = interfaceRow["PdbID"].ToString();
                if (unpPairInterfaceListDict.ContainsKey (unpPair))
                {
                    unpPairInterfaceListDict[unpPair].Add(chainInterface);
                }
                else
                {
                    List<string> interfaceList = new List<string>();
                    interfaceList.Add(chainInterface);
                    unpPairInterfaceListDict.Add(unpPair, interfaceList);
                    unpPairList.Add(unpPair);
                }
            }
            unpPairList.Sort();
            string commonUnp = "";
            List<string> parsedEntryPairList = new List<string>();
            Dictionary<string[], List<string[]>> unpPairInterfacePairDict = new Dictionary<string[], List<string[]>>();
            string[] unpPairs = null;
            string[] interfacePairs = null;
            InterfaceChains chainInterface1 = null;
            InterfaceChains chainInterface2 = null;
            bool areStructuresGood = false;
            Dictionary<string, InterfaceChains> chainInterfaceDict = new Dictionary<string,InterfaceChains> ();
            for (int i = 0; i < unpPairList.Count; i ++)
            {
                List<string> chainInterfaceListI = unpPairInterfaceListDict[unpPairList[i]];
                for (int j = i + 1; j < unpPairList.Count; j ++)
                {
                    List<string> chainInterfaceListJ = unpPairInterfaceListDict[unpPairList[j]];
                    commonUnp = GetCommonUnp(unpPairList[i], unpPairList[j]);
                    unpPairs = new string[2];
                    unpPairs[0] = unpPairList[i];
                    unpPairs[1] = unpPairList[j];
                    foreach (string chainInterfaceI in chainInterfaceListI)
                    {
                        try
                        {
                            chainInterface1 = GetChainInterface(chainInterfaceI, chainInterfaceDict);
                        }
                        catch (Exception ex)
                        {
                            logWriter.WriteLine(chainInterfaceI  + " read interface error " + ex.Message);
                            logWriter.Flush();
                            continue;
                        }
                        foreach (string chainInterfaceJ in chainInterfaceListJ)
                        {
                            try
                            {
                                chainInterface2 = GetChainInterface(chainInterfaceJ, chainInterfaceDict);
                            }
                            catch (Exception ex)
                            {
                                logWriter.WriteLine(chainInterfaceJ + " read interface error " + ex.Message);
                                logWriter.Flush();
                                continue;
                            }
                            try
                            {
                                areStructuresGood = AreCommonUniprotInterfacesGood(commonUnp, chainInterface1, chainInterface2);
                            }
                            catch (Exception ex)
                            {
                                logWriter.WriteLine(chainInterfaceI + " " + chainInterfaceJ + " checking qualification error " + ex.Message);
                                logWriter.Flush();
                                continue;
                            }
                            // no interface overlap but with shared uniprot sequences so can be superposed
                            if (areStructuresGood)
                            {
                                interfacePairs = new string[2];
                                interfacePairs[0] = chainInterfaceI;
                                interfacePairs[1] = chainInterfaceJ;
                                if (unpPairInterfacePairDict.ContainsKey(unpPairs))
                                {
                                    unpPairInterfacePairDict[unpPairs].Add(interfacePairs);
                                }
                                else
                                {
                                    List<string[]> interfacePairList = new List<string[]>();
                                    interfacePairList.Add(interfacePairs);
                                    unpPairInterfacePairDict.Add(unpPairs, interfacePairList);
                                }
                            }
                        }
                    }
                }
            }
            return unpPairInterfacePairDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterfaceName"></param>
        /// <param name="chainInterfaceDict"></param>
        /// <returns></returns>
        private InterfaceChains GetChainInterface(string chainInterfaceName, Dictionary<string, InterfaceChains> chainInterfaceDict)
        {
            InterfaceChains chainInterface = null;
            if (chainInterfaceDict.ContainsKey(chainInterfaceName))
            {
                chainInterface = chainInterfaceDict[chainInterfaceName];
            }
            else
            {
                string pdbId = chainInterfaceName.Substring(0, 4);
                int interfaceId = Convert.ToInt32(chainInterfaceName.Substring(4, chainInterfaceName.Length - 4));
                chainInterface = ReadInterfaceFromFile(pdbId, interfaceId);
                chainInterfaceDict.Add(chainInterfaceName, chainInterface);
            }
            return chainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commonUniProt"></param>
        /// <param name="chainInterface1"></param>
        /// <param name="chainInterface2"></param>
        /// <returns></returns>
        public bool AreCommonUniprotInterfacesGood(string commonUniprot, InterfaceChains chainInterface1, InterfaceChains chainInterface2)
        {
            if (chainInterface1 == null || chainInterface2 == null)
            {
                return false;
            }
            int[] entityIds1 = GetUniProtEntities(commonUniprot, chainInterface1.pdbId);
            int[] entityIds2 = GetUniProtEntities(commonUniprot, chainInterface2.pdbId);

            bool areGoodStructures = false;
            bool interfaceOverlap = AreCommonUniprotInteractionOverlap(chainInterface1, chainInterface2, entityIds1, entityIds2);

            // if no interface overlap, then the chain sequences are overlap, so can be superposed?
            if (!interfaceOverlap)
            {
                bool chainOverlap = AreCommonUniprotChainsOverlap(chainInterface1, chainInterface2, entityIds1, entityIds2);
                if (chainOverlap)
                {
                    areGoodStructures = true;
                }
            }
            return areGoodStructures;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commonUniProt"></param>
        /// <param name="chainInterface1"></param>
        /// <param name="chainInterface2"></param>
        /// <returns></returns>
        public bool AreCommonUniprotInterfacesGood (string commonUniprot, string chainInterfaceName1, string chainInterfaceName2)
        {
            string pdbId1 = chainInterfaceName1.Substring(0, 4);
            int interfaceId1 = Convert.ToInt32(chainInterfaceName1.Substring(4, chainInterfaceName1.Length - 4));
            string pdbId2 = chainInterfaceName2.Substring(0, 4);
            int interfaceId2 = Convert.ToInt32(chainInterfaceName2.Substring(4, chainInterfaceName2.Length - 4));

            InterfaceChains chainInterface1 = ReadInterfaceFromFile(pdbId1, interfaceId1);
            InterfaceChains chainInterface2 = ReadInterfaceFromFile(pdbId2, interfaceId2);
            if (chainInterface1 == null || chainInterface2 == null)
            {
                return false;
            }
            int[] entityIds1 = GetUniProtEntities(commonUniprot, pdbId1);
            int[] entityIds2 = GetUniProtEntities(commonUniprot, pdbId2);

            bool areGoodStructures = false;
            bool interfaceOverlap = AreCommonUniprotInteractionOverlap(chainInterface1, chainInterface2, entityIds1, entityIds2);

            // if no interface overlap, then the chain sequences are overlap, so can be superposed?
            if (! interfaceOverlap)  
            {                
                bool  chainOverlap = AreCommonUniprotChainsOverlap(chainInterface1, chainInterface2, entityIds1, entityIds2);
                if (chainOverlap)
                {
                    areGoodStructures = true;
                }
            }
            return areGoodStructures;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commonUniprot"></param>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        public bool AreCommonUniprotInteractionOverlap(InterfaceChains chainInterface1, InterfaceChains chainInterface2, int[] unpEntityIds1, int[] unpEntityIds2)
        {           
            string[] contactResidues1 = null;
            string[] contactResidues2 = null;
            if (unpEntityIds1.Contains(chainInterface1.entityId1))
            {
                contactResidues1 = GetResidueSeqNumbers(chainInterface1.seqDistHash, 0);
            }
            if (unpEntityIds2.Contains(chainInterface2.entityId1))
            {
                contactResidues2 = GetResidueSeqNumbers(chainInterface2.seqDistHash, 0);
            }
            if (unpEntityIds1.Contains(chainInterface1.entityId2))
            {
                contactResidues1 = GetResidueSeqNumbers(chainInterface1.seqDistHash, 1);
            }
            if (unpEntityIds2.Contains(chainInterface2.entityId2))
            {
                contactResidues2 = GetResidueSeqNumbers(chainInterface2.seqDistHash, 1);
            }
            if (AreContactsOverlap (contactResidues1, contactResidues2))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commonUniprot"></param>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        public bool AreCommonUniprotChainsOverlap(InterfaceChains chainInterface1, InterfaceChains chainInterface2, int[] unpEntityIds1, int[] unpEntityIds2)
        {
            string[] residueSeqNumbers1 = null;
            string[] residueSeqNumbers2 = null;
            if (unpEntityIds1.Contains(chainInterface1.entityId1))
            {
                residueSeqNumbers1 = GetChainUnpSeqNumbers(chainInterface1.chain1);
            }
            if (unpEntityIds2.Contains(chainInterface2.entityId1))
            {
                residueSeqNumbers2 = GetChainUnpSeqNumbers(chainInterface2.chain1);
            }
            if (unpEntityIds1.Contains(chainInterface1.entityId2))
            {
                residueSeqNumbers1 = GetChainUnpSeqNumbers(chainInterface1.chain2);
            }
            if (unpEntityIds2.Contains(chainInterface2.entityId2))
            {
                residueSeqNumbers2 = GetChainUnpSeqNumbers(chainInterface2.chain2);
            }
            if (AreContactsOverlap(residueSeqNumbers1, residueSeqNumbers2))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="unpEntities"></param>
        /// <returns></returns>
        private string[] GetChainUnpSeqNumbers(AtomInfo[] chain)
        {
            List<string> chainSeqNumList = new List<string>();
            foreach (AtomInfo atom in chain)
            {
                if (atom.seqId == "-1")
                {
                    continue;
                }
                if (!chainSeqNumList.Contains(atom.seqId))
                {
                    chainSeqNumList.Add(atom.seqId);
                }
            }
            return chainSeqNumList.ToArray();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="contactResidues1"></param>
        /// <param name="contactResidues2"></param>
        /// <returns></returns>
        private bool AreContactsOverlap (string[] contactResidues1, string[] contactResidues2)
        {
            List<string> commonResidueList = new List<string>();
            foreach(string residueSeq in contactResidues1)
            {
                if ( contactResidues2.Contains (residueSeq))
                {
                    commonResidueList.Add(residueSeq);
                }
            }
            double overlap = (double)commonResidueList.Count / (double)Math.Min (contactResidues1.Length, contactResidues2.Length);
            if (overlap > contactOverlap)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqDistHash"></param>
        /// <param name="firstOrSecond"></param>
        /// <returns></returns>
        private string[] GetResidueSeqNumbers (Dictionary<string, double> seqDistHash, int firstOrSecond)
        {
            List<string> residueSeqList = new List<string>();
            if (firstOrSecond == 0)
            {
                foreach (string seqPair in seqDistHash.Keys)
                {
                    string[] seqFields = seqPair.Split('_');
                    if (seqFields[0] == "-1")
                    {
                        continue;
                    }
                    if (! residueSeqList.Contains (seqFields[0]))
                    {
                        residueSeqList.Add(seqFields[0]);
                    }
                }
            }
            else
            {
                foreach (string seqPair in seqDistHash.Keys)
                {
                    string[] seqFields = seqPair.Split('_');
                    if (seqFields[1] == "-1")
                    {
                        continue;
                    }
                    if (!residueSeqList.Contains(seqFields[1]))
                    {
                        residueSeqList.Add(seqFields[1]);
                    }
                }
            }
            return residueSeqList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetUniProtEntities (string uniprot, string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbDbRefSifts Where PdbID = '{0}' AND DbCode = '{1}';", pdbId, uniprot);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] entities = new int[entityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entities[count] = Convert.ToInt32(entityRow["EntityID"].ToString ());
                count++;
            }
            return entities;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        public InterfaceChains ReadInterfaceFromFile (string pdbId, int interfaceId)
        {
            string interfaceFile = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + pdbId.Substring(1, 2) + "\\" + pdbId + "_" + interfaceId + ".cryst.gz");
            string unzippedInterfaceFile = Path.Combine(tempDir, pdbId + "_" + interfaceId + ".cryst");
            if (!File.Exists(unzippedInterfaceFile))
            {
                unzippedInterfaceFile = ParseHelper.UnZipFile(interfaceFile, tempDir);
            }
            InterfaceChains chainInterface = new InterfaceChains ();
            interfaceReader.ReadInterfaceFromFile(unzippedInterfaceFile, ref chainInterface);
            if (chainInterface.surfaceArea < surfaceAreaCutoff && chainInterface.surfaceArea > -1.0)
            {
                return null;
            }
            chainInterface.pdbId = pdbId;
            seqNumberConverter.ConvertInterfaceSeqNumbersToUnpNumbers(chainInterface);
            chainInterface.GetInterfaceResidueDist();
            WriteInterfaceToFile(chainInterface);
 //           File.Delete(unzippedInterfaceFile);
            return chainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        private void WriteInterfaceToFile (InterfaceChains chainInterface)
        {
            string interfaceFileName = Path.Combine (tempDir, chainInterface.pdbId + "_" + chainInterface.interfaceId + "_unp.cryst");
            string remark = "Header " + chainInterface.pdbId + " " + chainInterface.interfaceId + "\n" +
                "REMARK EntityID1 " + chainInterface.entityId1 + " EntityID2" + chainInterface.entityId2 + "\n" +
                "REMARK AsymChain1 " + chainInterface.firstSymOpString + " AsymChain2 " + chainInterface.secondSymOpString + "\n" +
                "REMARK Residue Numbers in UniProt Numbers";
            interfaceWriter.WriteInterfaceToFile(interfaceFileName, remark, chainInterface.chain1, chainInterface.chain2);
        }

        /// <summary>
        /// the PDB structures of the complex uniprots
        /// </summary>
        /// <param name="complexUniprots"></param>
        public Dictionary<string, List<string>> RetrieveComplexStructureInfo(string[] complexUniprots, out Dictionary<string, List<int>> unpPairRelSeqIdListDict)
        {
            string queryString = string.Format("Select Distinct UnpID1, UnpID2, PfamDomainInterfaces.RelSeqID, PfamDomainInterfaces.PdbID, PfamDomainInterfaces.InterfaceID " +
                " From UnpPdbDomainInterfaces, PfamDomainInterfaces" +
                " Where UnpID1 In ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2 AND " +
                " UnpPdbDomainInterfaces.PdbID = PfamDomainInterfaces.PdbID AND UnpPdbDomainInterfaces.DomainInterfaceID = PfamDomainInterfaces.DomainInterfaceID;",
                ParseHelper.FormatSqlListString(complexUniprots));
            DataTable unpDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            Dictionary<string, List<string>> unpPairInterfaceListDict = new Dictionary<string, List<string>>();
            unpPairRelSeqIdListDict = new Dictionary<string, List<int>>();
            string unpPair = "";
            int relSeqId = 0;
            string chainInterface = "";
            string unpId1 = "";
            string unpId2 = "";
            foreach (DataRow unpPairRow in unpDomainInterfaceTable.Rows)
            {
                unpId1 = unpPairRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpPairRow["UnpID2"].ToString().TrimEnd();
                unpPair =  unpId1 + "-" + unpId2;
                if (string.Compare (unpId1, unpId2) > 0)
                {
                    unpPair = unpId2 + "-" + unpId1;
                }                
                relSeqId = Convert.ToInt32(unpPairRow["RelSeqID"].ToString ());
                chainInterface = unpPairRow["PdbID"].ToString() + unpPairRow["InterfaceID"].ToString();
                if (unpPairInterfaceListDict.ContainsKey (unpPair))
                {
                    if (!unpPairInterfaceListDict[unpPair].Contains(chainInterface))
                    {
                        unpPairInterfaceListDict[unpPair].Add(chainInterface);
                    }
                }
                else
                {
                    List<string> chainInterfaceList = new List<string>();
                    chainInterfaceList.Add(chainInterface);
                    unpPairInterfaceListDict.Add(unpPair, chainInterfaceList);
                }

                if (unpPairRelSeqIdListDict.ContainsKey (unpPair))
                {
                    if (! unpPairRelSeqIdListDict[unpPair].Contains (relSeqId))
                    {
                        unpPairRelSeqIdListDict[unpPair].Add(relSeqId);
                    }
                }
                else
                {
                    List<int> relSeqIdList = new List<int>();
                    relSeqIdList.Add(relSeqId);
                    unpPairRelSeqIdListDict.Add(unpPair, relSeqIdList);
                }
            }

            return unpPairInterfaceListDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        private string[][] DividEntriesContainsAllUniprots (string[] complexUniprots)
        {       
            Dictionary<string, List<string>> entryUnpListDict = GetEntryComplexUnpListDict (complexUniprots);
            List<string> entryListWithAllUnps = new List<string>();
            List<string> entryListWithPartialUnps = new List<string>();
            List<string> complexUnpList = new List<string>(complexUniprots);
            foreach (string pdbId in entryUnpListDict.Keys)
            {
                if (DoesFirstContainSecond(entryUnpListDict[pdbId], complexUnpList))
                {
                    entryListWithAllUnps.Add(pdbId);
                }
                else
                {
                    entryListWithPartialUnps.Add(pdbId);
                }
            }
            string[][] entryLists = new string[2][];
            entryLists[0] = entryListWithAllUnps.ToArray();
            entryLists[1] = entryListWithPartialUnps.ToArray();
            return entryLists;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemList1"></param>
        /// <param name="itemList2"></param>
        /// <returns></returns>
        private bool DoesFirstContainSecond (List<string> itemList1, List<string> itemList2)
        {
            foreach (string item in itemList2)
            {
                if (! itemList1.Contains (item))
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpComponentList"></param>
        /// <returns></returns>
        public string FormatConnectUniprotsList(List<List<string>> unpComponentList)
        {
            string connectedUnpListString = "";
            foreach (List<string> connectComponent in unpComponentList)
            {
                connectedUnpListString += (FormatArrayString(connectComponent, ';') + "\t");
            }
            return connectedUnpListString.TrimEnd('\t');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetEntryComplexUnpListDict(string[] complexUniprots)
        {
            string queryString = string.Format("Select Distinct PdbID, DbCode From PdbDbRefSifts  Where DbCode IN ({0});",
                ParseHelper.FormatSqlListString(complexUniprots));
            DataTable entryUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            Dictionary<string, List<string>> entryUnpListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string unpCode = "";
            List<string> entryListInSifts = new List<string>();
            foreach (DataRow unpRow in entryUnpTable.Rows)
            {
                pdbId = unpRow["PdbID"].ToString();
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                if (entryUnpListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpListDict[pdbId].Contains(unpCode))
                    {
                        entryUnpListDict[pdbId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpCode);
                    entryUnpListDict.Add(pdbId, unpList);
                    entryListInSifts.Add(pdbId);
                }
            }

            // add entries from XML files, since SIFTs is not always synchronized with PDB
            queryString = string.Format("Select Distinct PdbID, DbCode From PdbDbRefXml Where DbCode IN ({0});",
                ParseHelper.FormatSqlListString(complexUniprots));
            DataTable entryUnpTableXml = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow unpRow in entryUnpTableXml.Rows)
            {
                pdbId = unpRow["PdbID"].ToString();
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                if (entryListInSifts.Contains(pdbId))
                {
                    continue;
                }
                if (entryUnpListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpListDict[pdbId].Contains(unpCode))
                    {
                        entryUnpListDict[pdbId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpCode);
                    entryUnpListDict.Add(pdbId, unpList);
                }
            }
            return entryUnpListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetEntryComplexUnpRangeListDict(string[] complexUniprots)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbId, DbCode, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                 " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                 " Where DbCode IN ({0}) AND PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId;", 
                 ParseHelper.FormatSqlListString (complexUniprots));
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);        
            Dictionary<string, List<string>> entryUnpRangeListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string unpCodeRange = "";
            List<string> entryListInSifts = new List<string>();
            foreach (DataRow unpRow in unpPdbSeqAlignTable.Rows)
            {
                pdbId = unpRow["PdbID"].ToString();
                unpCodeRange = unpRow["DbCode"].ToString().TrimEnd() + "[" + unpRow["DbAlignBeg"] + "-" + unpRow["DbAlignEnd"] + "]";
                if (entryUnpRangeListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpRangeListDict[pdbId].Contains(unpCodeRange))
                    {
                        entryUnpRangeListDict[pdbId].Add(unpCodeRange);
                    }
                }
                else
                {
                    List<string> unpRangeList = new List<string>();
                    unpRangeList.Add(unpCodeRange);
                    entryUnpRangeListDict.Add(pdbId, unpRangeList);
                    entryListInSifts.Add(pdbId);
                }
            }

            // add entries from XML files, since SIFTs is not always synchronized with PDB
                queryString = string.Format("Select Distinct PdbDbRefXml.PdbId, DbCode, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                    " From PdbDbRefXml, PdbDbRefSeqSifts " +
                    " Where DbCode In ({0}) AND PdbDbRefXml.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqSifts.RefId;", 
                    ParseHelper.FormatSqlListString(complexUniprots));
                DataTable entryUnpAlignTableXml = ProtCidSettings.pdbfamQuery.Query(queryString);
            DataTable entryUnpAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow unpRow in entryUnpAlignTableXml.Rows)
            {
                pdbId = unpRow["PdbID"].ToString();
                unpCodeRange = unpRow["DbCode"].ToString().TrimEnd() + "[" + unpRow["DbAlignBeg"] + "-" + unpRow["DbAlignEnd"] + "]";
                if (entryListInSifts.Contains(pdbId))
                {
                    continue;
                }
                if (entryUnpRangeListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpRangeListDict[pdbId].Contains(unpCodeRange))
                    {
                        entryUnpRangeListDict[pdbId].Add(unpCodeRange);
                    }
                }
                else
                {
                    List<string> unpRangeList = new List<string>();
                    unpRangeList.Add(unpCodeRange);
                    entryUnpRangeListDict.Add(pdbId, unpRangeList);
                }
            }
            return entryUnpRangeListDict;
        }
        #endregion  

        #region complex uniprots from complex portal
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveComplexPortalPdbProtCidData()
        {
            string complexPortalListFile = Path.Combine(complexDataDir, "ComplexesMIF30_list.txt");
            if (geneUnpListDict == null || geneUnpListDict.Count == 0)
            {
                Dictionary<string, string> complexUnpCodeDict = null;
                SetGenesOfUniprotsComplexes(null, out complexUnpCodeDict);
            }
            String lsFile = Path.Combine(complexDataDir, "ComplexSumList_noAll.txt");
            Dictionary<string, string[]> complexUnpListDict = ReadComplexesFromSumListFile(lsFile);
            string complexStructPfamInfoFile = Path.Combine(complexDataDir, "ComplexProtalStructPfamClustersInfo_update.txt");
            StreamWriter dataWriter = new StreamWriter(complexStructPfamInfoFile);
            string[] complexUniprots = null;
            bool[] canComplexBeModeled = null;
            List<string> neededComplexListStruct = new List<string>();
            List<string> neededComplexListHomo = new List<string>();
            foreach (string complexName in complexUnpListDict.Keys)
            {
                complexUniprots = complexUnpListDict[complexName];
                try
                {
                    canComplexBeModeled = RetrieveComplexPdbProtCidInfo(complexName, complexUniprots, dataWriter);
                    if (complexUniprots.Length >= 3)
                    {
                        if (canComplexBeModeled[0])
                        {
                            neededComplexListStruct.Add(complexName);
                        }
                        else if (canComplexBeModeled[1])
                        {
                            neededComplexListHomo.Add(complexName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(complexName + " " + FormatArrayString(complexUniprots, ';') + " : " + ex.Message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close();

            string complexListFile = Path.Combine(complexDataDir, "ListComplexesCanBeModeledStructPfam_update.txt");
            dataWriter = new StreamWriter(complexListFile);
            dataWriter.WriteLine("Complexes >= 3 can be structurally modeled: " + neededComplexListStruct.Count);
            foreach (string complex in neededComplexListStruct)
            {
                dataWriter.WriteLine(complex);
            }
            dataWriter.WriteLine("");
            dataWriter.WriteLine("Complexes >= 3 can be struct/Pfam modeled: " + neededComplexListHomo.Count);
            foreach (string complex in neededComplexListHomo)
            {
                dataWriter.WriteLine(complex);
            }
            dataWriter.Close();

            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveComplexPortalStructuresData()
        {
            string complexPortalListFile = Path.Combine(complexDataDir, "ComplexesMIF30_list.txt");
            Dictionary<string, List<string>> complexUnpListDict = ReadComplexesFromComplexPortal(complexPortalListFile);
            string[] allUniprots = GetAllUniprotsInComplexPortal(complexUnpListDict);
            Dictionary<string, string> complexUnpCodeMapDict = null;
            SetGenesOfUniprotsComplexes(allUniprots, out complexUnpCodeMapDict);
            Dictionary<string, List<string>> updateComplexUnpListDict = UpdateComplexUniprots(complexUnpCodeMapDict, complexUnpListDict);

            string complexPdbStructInfoFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN_test.txt");
            StreamWriter complexStructWriter = new StreamWriter(complexPdbStructInfoFile);
            string[] complexUniprots = null;
            string[] orgComplexUniprots = null;
            foreach (string complexName in updateComplexUnpListDict.Keys)
            {
                complexUniprots = updateComplexUnpListDict[complexName].ToArray();
                orgComplexUniprots = complexUnpListDict[complexName].ToArray ();
                try
                {
                    RetrieveComplexRelatedStructures(complexName, orgComplexUniprots, complexUniprots, complexStructWriter);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(complexName + " " + FormatArrayString(complexUniprots, ';') + " : " + ex.Message);
                    logWriter.Flush();
                }
            }
            logWriter.Close();
            complexStructWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexName"></param>
        /// <param name="complexUniprots"></param>
        /// <param name="structWriter"></param>
        public void RetrieveComplexRelatedStructures (string complexName, string[] orgComplexUniprots, string[] complexUniprots, StreamWriter structWriter)
        {    
            string dataLine = "";
            string[] extendedComplexUnps = null;
            Dictionary<string, string[]> complexSameGnUnpsDict = GetComplexSameGeneUnpsDict(complexUniprots, out extendedComplexUnps);

            Dictionary<string, List<string>> entryInteractUnpListDict = GetEntryInteractUnpListDict(extendedComplexUnps);
            Dictionary<string, List<string>> interactUnpEntryListDict = GetUnpEntryListDict(entryInteractUnpListDict);
            Dictionary<string, List<string>> entryExtendInteractUnpsListDict = null;
            string[] entriesWithAllUniprots = GetEntriesContainAllUniprots(entryInteractUnpListDict, complexSameGnUnpsDict, out entryExtendInteractUnpsListDict);
            dataLine = "#" + complexName + "\tInteraction\t" + entriesWithAllUniprots.Length + "\t" + FormatArrayString(complexUniprots, ';') + "\t" + FormatArrayString(orgComplexUniprots, ';') ;
            structWriter.WriteLine(dataLine);
            structWriter.WriteLine("Structures\tContainAll\tInteractComplexUniprots");
            List<string> entryList = new List<string>(entryExtendInteractUnpsListDict.Keys);
            entryList.Sort();
            foreach (string pdbId in entryList)
            {                
                if (entriesWithAllUniprots.Contains (pdbId))
                {
                    dataLine = pdbId + "\tAll\t" + FormatArrayString(entryExtendInteractUnpsListDict[pdbId], ';');
                }
                else
                {
                    dataLine = pdbId + "\tPart\t" + FormatArrayString(entryExtendInteractUnpsListDict[pdbId], ';');
                }
                structWriter.WriteLine(dataLine);
            }
            foreach (string unps in interactUnpEntryListDict.Keys)
            {
                structWriter.WriteLine(unps + "\t" + FormatArrayString(interactUnpEntryListDict[unps], ';'));
            }
            structWriter.WriteLine();

            Dictionary<string, List<string>> entryUnpListDict = GetEntryComplexUnpListDict(extendedComplexUnps);
            Dictionary<string, List<string>> unpEntryListDict = GetUnpEntryListDict(entryUnpListDict);
            Dictionary<string, List<string>> entryExtendUnpsListDict = null;
            entriesWithAllUniprots = GetEntriesContainAllUniprots(entryUnpListDict, complexSameGnUnpsDict, out entryExtendUnpsListDict);
            dataLine = "#" + complexName + "\tStructure\t" + entriesWithAllUniprots.Length + "\t" + FormatArrayString(complexUniprots, ';') + "\t" + FormatArrayString(orgComplexUniprots, ';'); 
            structWriter.WriteLine(dataLine);
            entryList = new List<string>(entryExtendUnpsListDict.Keys);
            entryList.Sort();
            structWriter.WriteLine("Structures\tContainAll\tComplexUniprots");
            foreach (string pdbId in entryList)
            {
                if (entriesWithAllUniprots.Contains(pdbId))
                {
                    dataLine = pdbId + "\tAll\t" + FormatArrayString(entryExtendUnpsListDict[pdbId], ';');
                }
                else
                {
                    dataLine = pdbId + "\tPart\t" + FormatArrayString(entryExtendUnpsListDict[pdbId], ';');
                }
                structWriter.WriteLine(dataLine);
            }
            foreach (string unps in unpEntryListDict.Keys)
            {
                structWriter.WriteLine(unps + "\t" + FormatArrayString(unpEntryListDict[unps], ';'));
            }
            structWriter.WriteLine();
            structWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryUnpListDict"></param>
        /// <param name="numUniprots"></param>
        /// <returns></returns>
        public string[] GetEntriesContainAllUniprots (Dictionary<string, List<string>> entryUnpListDict,
            Dictionary<string, string[]> complexSameGnUnpsDict, out Dictionary<string, List<string>> entryExtendUnpsListDict)
        {
            entryExtendUnpsListDict = new Dictionary<string, List<string>>();
            List<string> entryAllUnpList = new List<string>();
            bool isComplexUnpContained = false;
            bool IsAllUnpContained = false;
            List<string> extendedUnpList = null;
            foreach (string pdbId in entryUnpListDict.Keys)
            {
                extendedUnpList = new List<string>();
                IsAllUnpContained = true;
                foreach (string complexUnp in complexSameGnUnpsDict.Keys)
                {
                    isComplexUnpContained = false;
                    if (entryUnpListDict[pdbId].Contains (complexUnp))
                    {
                        extendedUnpList.Add(complexUnp);
                        isComplexUnpContained = true;
                    }
                    else 
                    {
                        foreach (string sameGnUnp in complexSameGnUnpsDict[complexUnp])
                        {
                            if (entryUnpListDict[pdbId].Contains (sameGnUnp))
                            {
                                extendedUnpList.Add(sameGnUnp);
                                isComplexUnpContained = true;
                                break;
                            }
                        }
                    }
                    if (! isComplexUnpContained )
                    {
                        IsAllUnpContained = false;
                    }
                }
                entryExtendUnpsListDict.Add(pdbId, extendedUnpList);
                if (IsAllUnpContained)
                {
                    entryAllUnpList.Add(pdbId);
                }                
            }
            return entryAllUnpList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetEntryInteractUnpListDict (string[] complexUniprots)
        {
            string queryString = string.Format("Select Distinct UnpID1, UnpID2, PdbID From UnpPdbDomainInterfaces " +
 //                    " Where UnpID1 In ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2;", ParseHelper.FormatSqlListString(complexUniprots));
                      " Where UnpID1 In ({0}) AND UnpID2 IN ({0});", ParseHelper.FormatSqlListString(complexUniprots));
            DataTable unpPairEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> entryInteractUnpListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string unpCode = "";
            foreach (DataRow unpPairRow in unpPairEntryTable.Rows)
            {
                pdbId = unpPairRow["PdbID"].ToString().TrimEnd();
                unpCode = unpPairRow["UnpID1"].ToString().TrimEnd();
                if (entryInteractUnpListDict.ContainsKey(pdbId))
                {
                    if (!entryInteractUnpListDict[pdbId].Contains(unpCode))
                    {
                        entryInteractUnpListDict[pdbId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> unpCodeList = new List<string>();
                    unpCodeList.Add(unpCode);
                    entryInteractUnpListDict.Add(pdbId, unpCodeList);
                }
                unpCode = unpPairRow["UnpID2"].ToString().TrimEnd();
                if (!entryInteractUnpListDict[pdbId].Contains(unpCode))
                {
                    entryInteractUnpListDict[pdbId].Add(unpCode);
                }
            }

            return entryInteractUnpListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryUnpListDict"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetUnpEntryListDict (Dictionary<string, List<string>> entryUnpListDict)
        {
            string unpString = "";
            Dictionary<string, List<string>> unpEntryListDict = new Dictionary<string, List<string>>();
            foreach (string pdbId in entryUnpListDict.Keys)
            {
                entryUnpListDict[pdbId].Sort();
                unpString = FormatArrayString(entryUnpListDict[pdbId], ';');
                if (unpEntryListDict.ContainsKey (unpString))
                {
                    if (! unpEntryListDict[unpString].Contains (pdbId))
                    {
                        unpEntryListDict[unpString].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    unpEntryListDict.Add(unpString, entryList);
                }
            }
            return unpEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexLsFile"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadComplexesFromComplexPortal(string complexLsFile)
        {
            StreamReader dataReader = new StreamReader(complexLsFile);
            Dictionary<string, List<string>> complexUnpListDict = new Dictionary<string, List<string>>();
            string line = "";
            string complexName = "";
            int xmlIndex = 0;
            List<string> unpList = null;
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                xmlIndex = line.IndexOf (".xml");
                if (xmlIndex > -1)
                {
                    if (complexName != "")
                    {
                        complexUnpListDict.Add(complexName, unpList);
                    }
                    complexName = line.Substring(0, xmlIndex);
                    unpList = new List<string>();
                }
                else
                {
                    string[] unpFields = line.Split('\t');
                    if (unpFields[1] != complexName && unpFields[1].IndexOf ("_") > -1 
                        && unpFields[1].IndexOf ("\'") < 0 && unpFields[1].IndexOf ("(") < 0)
                    {
                        unpList.Add(unpFields[1].ToUpper ());
                    }
                }
            }
            dataReader.Close();
            complexUnpListDict.Add(complexName, unpList);

            return complexUnpListDict;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="complexUnpListDict"></param>
        /// <returns></returns>
        private string[] GetAllUniprotsInComplexPortal (Dictionary<string, List<string>> complexUnpListDict)
        {
            List<string> unpList = new List<string>();
            foreach (string complexName in complexUnpListDict.Keys)
            {
                foreach (string uniprot in complexUnpListDict[complexName])
                {
                    if (! unpList.Contains (uniprot))
                    {
                        unpList.Add(uniprot);
                    }
                }
            }
            return unpList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadComplexesFromSumListFile (string lsFile)
        {
            Dictionary<string, string[]> complexUniprotsDict = new Dictionary<string, string[]>();          
            StreamReader dataReader = new StreamReader(lsFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length == 3)
                {
                    complexUniprotsDict.Add(fields[0], fields[2].Split(';'));
                }
                else if (fields.Length == 2)
                {
                    complexUniprotsDict.Add(fields[0], fields[1].Split(';'));
                }
            }
            dataReader.Close();
            return complexUniprotsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string[]> ReadComplexesFromOutputComplexStructFile ()
        {
            string complexStructFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN.txt");            
            StreamReader dataReader = new StreamReader(complexStructFile);

            string complexSumInfoFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN_sum.txt");
            StreamWriter sumInfoWriter = new StreamWriter(complexSumInfoFile);

            Dictionary<string, string[]> complexUniprotsDict = new Dictionary<string, string[]>();
            Dictionary<string, List<string>> complexAllUnpEntryListDict = new Dictionary<string, List<string>>();
            string line = "";
            string[] complexUniprots = null;
            string complexName = "";
            bool structuresStart = false;
            List<string> entryMixUnpList = null;
            List<string> entrySameUnpList = null;
            List<string> entryHomoUnpList = null;
            List<string> entryPartUnpList = null;

            List<string> sameAllComplexList = new List<string>();
            List<string> mixAllComplexList = new List<string>();
            List<string> homoAllComplexList = new List<string>();
            int numUniprots = 3;
            List<string> neededComplexList = new List<string>();
            List<string> complexAllList = new List<string>();

            string headerLine = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("#") > -1)
                {
                    if (headerLine != "")
                    {
                        if (complexUniprots.Length >= numUniprots)
                        {
                            if (! neededComplexList.Contains (complexName))
                            {
                                neededComplexList.Add(complexName);
                            }
                        }
                        if (entrySameUnpList.Count > 0 || entryMixUnpList.Count > 0 || entryHomoUnpList.Count > 0 || entryPartUnpList.Count > 0)
                        {
                            sumInfoWriter.WriteLine(headerLine);
                            sumInfoWriter.WriteLine("Same\t" + entrySameUnpList.Count + "\t" + FormatArrayString(entrySameUnpList, ';'));
                            sumInfoWriter.WriteLine("Mix\t" + entryMixUnpList.Count + "\t" + FormatArrayString(entryMixUnpList, ';'));
                            sumInfoWriter.WriteLine("Homo\t" + entryHomoUnpList.Count + "\t" + FormatArrayString(entryHomoUnpList, ';'));
                            sumInfoWriter.WriteLine("Part\t" + entryPartUnpList.Count + "\t" + FormatArrayString (entryPartUnpList, ';'));
                            if (!complexUniprotsDict.ContainsKey(complexName) && entrySameUnpList.Count == 0 && entryMixUnpList.Count == 0 && entryHomoUnpList.Count == 0)
                            {
                                complexUniprotsDict.Add(complexName, complexUniprots);
                            }
                            if (entrySameUnpList.Count > 0)
                            {
                                if (! sameAllComplexList.Contains (complexName))
                                {
                                    sameAllComplexList.Add(complexName);
                                }
                                if (!complexAllList.Contains(complexName))
                                {
                                    complexAllList.Add(complexName);
                                }
                            }
                            if (entryMixUnpList.Count > 0 && ! sameAllComplexList.Contains (complexName))
                            {
                                if (! mixAllComplexList.Contains (complexName))
                                {
                                    mixAllComplexList.Add(complexName);
                                }
                                if (!complexAllList.Contains(complexName))
                                {
                                    complexAllList.Add(complexName);
                                }
                            }
                            if (entryHomoUnpList.Count > 0 && ! sameAllComplexList.Contains (complexName) && ! mixAllComplexList.Contains (complexName))
                            {
                                if (! homoAllComplexList.Contains (complexName))
                                {
                                    homoAllComplexList.Add(complexName);
                                }
                                if (!complexAllList.Contains(complexName))
                                {
                                    complexAllList.Add(complexName);
                                }
                            }
                        }
                    }
                    string[] fields = line.Split('\t');
                    headerLine = line;
                    complexName = fields[0].TrimStart('#');
                    complexUniprots = fields[3].Split(';');
                    structuresStart = false;
                }
                else if (line.IndexOf ("Structures\tContainAll") > -1)
                {
                    structuresStart = true;
                    entryMixUnpList = new List<string>();
                    entrySameUnpList = new List<string>();
                    entryHomoUnpList = new List<string>();
                    entryPartUnpList = new List<string>();
                }
                else 
                {
                    string[] fields = line.Split('\t');
                    if (structuresStart && fields.Length == 3 && fields[0].Length == 4)
                    {
                        if (fields[1] == "All")
                        {
                            string[] entryUniprots = fields[2].Split(';');
                            string[] unpSpecies = GetUnpSpecies(complexUniprots);
                            string complexSpecies = unpSpecies[0];
                            string[] entrySpecies = GetUnpSpecies(entryUniprots);
                            if (entrySpecies.Contains(complexSpecies))
                            {
                                if (entrySpecies.Length > 1)
                                {
                                    entryMixUnpList.Add(fields[0]);
                                }
                                else
                                {
                                    entrySameUnpList.Add(fields[0]);
                                }
                            }
                            else
                            {
                                entryHomoUnpList.Add(fields[0]);
                            }
                        }
                        else
                        {
                            entryPartUnpList.Add(fields[0]);
                        }
                    }
                }
            }
            dataReader.Close();
            if (complexUniprots.Length >= numUniprots)
            {
                if (!neededComplexList.Contains(complexName))
                {
                    neededComplexList.Add(complexName);
                }
            }
            if (entrySameUnpList.Count > 0 || entryMixUnpList.Count > 0 || entryHomoUnpList.Count > 0 || entryPartUnpList.Count > 0)
            {
                sumInfoWriter.WriteLine(headerLine);
                sumInfoWriter.WriteLine("Same\t" + entrySameUnpList.Count + "\t" + FormatArrayString(entrySameUnpList, ';'));
                sumInfoWriter.WriteLine("Mix\t" + entryMixUnpList.Count + "\t" + FormatArrayString(entryMixUnpList, ';'));
                sumInfoWriter.WriteLine("Homo\t" + entryHomoUnpList.Count + "\t" + FormatArrayString(entryHomoUnpList, ';'));
                sumInfoWriter.WriteLine("Part\t" + entryPartUnpList.Count + "\t" + FormatArrayString(entryPartUnpList, ';'));
                if (!complexUniprotsDict.ContainsKey(complexName) && entrySameUnpList.Count == 0 && entryMixUnpList.Count == 0 && entryHomoUnpList.Count == 0)
                {
                    complexUniprotsDict.Add(complexName, complexUniprots);
                }

                if (entrySameUnpList.Count > 0)
                {
                    if (!sameAllComplexList.Contains(complexName))
                    {
                        sameAllComplexList.Add(complexName);
                    }
                    if (!complexAllList.Contains(complexName))
                    {
                        complexAllList.Add(complexName);
                    }
                }
                if (entryMixUnpList.Count > 0 && !sameAllComplexList.Contains(complexName))
                {
                    if (!mixAllComplexList.Contains(complexName))
                    {
                        mixAllComplexList.Add(complexName);
                    }
                    if (!complexAllList.Contains(complexName))
                    {
                        complexAllList.Add(complexName);
                    }
                }
                if (entryHomoUnpList.Count > 0 && !sameAllComplexList.Contains(complexName) && !mixAllComplexList.Contains(complexName))
                {
                    if (!homoAllComplexList.Contains(complexName))
                    {
                        homoAllComplexList.Add(complexName);
                    }
                    if (!complexAllList.Contains(complexName))
                    {
                        complexAllList.Add(complexName);
                    }
                }
            }
            sumInfoWriter.Close();
            string complexListFile = Path.Combine(complexDataDir, "ComplexSumList_noAll.txt");
            StreamWriter lsWriter = new StreamWriter(complexListFile);
            foreach (string keyComplexName in complexUniprotsDict.Keys)
            {
                if (! complexAllList.Contains(keyComplexName))
                {
                    lsWriter.WriteLine(keyComplexName + "\t" + complexUniprotsDict[keyComplexName].Length + "\t" + FormatArrayString(complexUniprotsDict[keyComplexName], ';'));
                }
            }
            lsWriter.Close();

            

            string sumInfoFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN_sumData.txt");
            StreamWriter sumWriter = new StreamWriter(sumInfoFile, true);
            sumWriter.WriteLine("#Complexes >= 3: " + neededComplexList.Count);
            sumWriter.WriteLine("Same-All: " + sameAllComplexList.Count);
            sumWriter.WriteLine("Mix-All: " + mixAllComplexList.Count);
            sumWriter.WriteLine("Homo-All: " + homoAllComplexList.Count);
            sumWriter.WriteLine(FormatArrayString(sameAllComplexList, ';'));
            sumWriter.WriteLine(FormatArrayString(mixAllComplexList, ';'));
            sumWriter.WriteLine(FormatArrayString(homoAllComplexList, ';'));
            sumWriter.Close();

            return complexUniprotsDict;
        }

        public void ReadComplexSumInfo ()
        {
            int numUniprots = 3;
            string complexSumInfoFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN_sum.txt");
            List<string> sameAllComplexList = new List<string>();
            List<string> mixAllComplexList = new List<string>();
            List<string> homoAllComplexList = new List<string>();
            string line = "";
            bool isComplexNeeded = false;
            string complexName = "";
            int numEntries = 0;
            List<string> neededComplexList = new List<string>();
            StreamReader dataReader = new StreamReader(complexSumInfoFile);
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (line.IndexOf ("#") > -1)
                {                    
                    complexName = fields[0].TrimStart('#');
                    string[] unpFields = fields[3].Split(';');
                    if (unpFields.Length < numUniprots)
                    {
                        isComplexNeeded = false;
                    }
                    else
                    {
                        isComplexNeeded = true;
                    }
                }
                else
                {
                    if (isComplexNeeded)
                    {
                        if (! neededComplexList.Contains (complexName))
                        {
                            neededComplexList.Add(complexName);
                        }
                        numEntries = Convert.ToInt32(fields[1]);
                        if (fields[0] == "Same" && numEntries > 0)
                        {
                            sameAllComplexList.Add(complexName);
                        }
                        else if (fields[0] == "Mix" && numEntries > 0)
                        {
                            if (!sameAllComplexList.Contains(complexName))
                            {
                                mixAllComplexList.Add(complexName);
                            }
                        }
                        else if (fields[0] == "Homo" && numEntries > 0)
                        {

                            if (!sameAllComplexList.Contains(complexName) && !mixAllComplexList.Contains(complexName))
                            { 
                                homoAllComplexList.Add(complexName);
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            string sumInfoFile = Path.Combine(complexDataDir, "ComplexPortalStructInfo_extendedGN_sumData.txt");
            StreamWriter sumWriter = new StreamWriter(sumInfoFile);
            sumWriter.WriteLine("#Complexes >= 3: " + neededComplexList.Count);
            sumWriter.WriteLine("Same-All: " + sameAllComplexList.Count);
            sumWriter.WriteLine("Mix-All: " + mixAllComplexList.Count);
            sumWriter.WriteLine("Homo-All: " + homoAllComplexList.Count);
            sumWriter.WriteLine( FormatArrayString(sameAllComplexList, ';'));
            sumWriter.WriteLine(FormatArrayString(mixAllComplexList, ';'));
            sumWriter.WriteLine(FormatArrayString(homoAllComplexList, ';'));
            sumWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <param name="structUniprots"></param>
        /// <returns></returns>
        private bool DoesEntryContainHomoUnps (string[] complexUniprots, string[] structUniprots)
        {            
            foreach (string unpCode in structUniprots)
            {
                if (! complexUniprots.Contains (unpCode))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <param name="structUniprots"></param>
        /// <returns></returns>
        private bool DoesEntryContainSameHomoUnps(string[] complexUniprots, string[] structUniprots)
        {
            bool isHomoUnp = false;
            bool isSameUnp = false;
            foreach (string unpCode in structUniprots)
            {
                if (!complexUniprots.Contains(unpCode))
                {
                    isHomoUnp = true;
                }
                else
                {
                    isSameUnp = true;
                }
            }
            if (isHomoUnp && isSameUnp)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="structUniprots"></param>
        /// <returns></returns>
        private string[] GetUnpSpecies (string[] structUniprots)
        {
            List<string> speciesList = new List<string>();
            int speciesIndex = -1;
            string species = "";
            foreach (string unpCode in structUniprots)
            {
                speciesIndex = unpCode.IndexOf("_");
                if (speciesIndex > -1)
                {
                    species = unpCode.Substring(speciesIndex + 1, unpCode.Length - speciesIndex - 1);
                    if (!speciesList.Contains(species))
                    {
                        speciesList.Add(species);
                    }
                }
            }
            return speciesList.ToArray();
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot1"></param>
        /// <param name="uniprot2"></param>
        /// <param name="unpPairInterfaceListDict"></param>
        /// <param name="unpPairPfamClusterListDict"></param>
        /// <param name="relBiggestClusterDict"></param>
        /// <param name="relPfamPairDict"></param>
        /// <param name="unpPairRelSeqIdListDict"></param>
        /// <param name="dataWriter"></param>
        public void PrintUnpPairStructPfamInfo(string uniprot1, string uniprot2, Dictionary<string, List<string>> unpPairInterfaceListDict,
            Dictionary<string, List<string>> unpPairPfamClusterListDict, Dictionary<int, DataRow> relBiggestClusterDict,
            Dictionary<int, string[]> relPfamPairDict, Dictionary<string, List<int>> unpPairRelSeqIdListDict, StreamWriter dataWriter)
        {
            string unpPair = uniprot1 + "-" + uniprot2;
            if (string.Compare (uniprot1, uniprot2) > 0)
            {
                unpPair = uniprot2 + "-" + uniprot1;
            }
            bool structPfamClusterExist = false;
            int relSeqId = 0;
            if (unpPairInterfaceListDict.ContainsKey(unpPair))
            {
                DataRow[] domainClusterRows = GetRelBiggestClusterDict(unpPairRelSeqIdListDict[unpPair].ToArray(), relBiggestClusterDict);
                if (domainClusterRows.Length > 0)
                {
                    structPfamClusterExist = true;
                }
                foreach (DataRow clusterRow in domainClusterRows)
                {
                    relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                    string[] pfamPair = GetRelSeqFamilyCodes(relSeqId, relPfamPairDict);
                    dataWriter.WriteLine(uniprot1 + "\t" + uniprot2 + "\t" + "structcluster\t" + pfamPair[0] + "\t" + pfamPair[1] + "\t" +
                        ParseHelper.FormatDataRow(clusterRow));
                }
                if (domainClusterRows.Length == 0)
                {
                    dataWriter.WriteLine(uniprot1 + "\t" + uniprot2 + "\t" + "struct\t" + FormatArrayString(unpPairInterfaceListDict[unpPair], ';'));
                }
            }
            if (!structPfamClusterExist && unpPairPfamClusterListDict.ContainsKey(unpPair))
            {
                foreach (string clusterLine in unpPairPfamClusterListDict[unpPair])
                {
                    dataWriter.WriteLine(uniprot1 + "\t" + uniprot2 + "\t" + "pfamcluster\t" + clusterLine);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relPfamPairDict"></param>
        /// <returns></returns>
        private string[] GetRelSeqFamilyCodes (int relSeqId, Dictionary<int, string[]> relPfamPairDict)
        {
            string[] pfamPair = new string[2];
            if (relPfamPairDict.ContainsKey (relSeqId))
            {
                pfamPair = relPfamPairDict[relSeqId];
            }
            else
            {
                string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
                if (pfamPairTable.Rows.Count > 0)
                {                    
                    pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                    pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                    relPfamPairDict.Add(relSeqId, pfamPair);                   
                }
            }
            return pfamPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <param name="structConnectUnpPairList"></param>
        /// <param name="pfamConnectUnpPairList"></param>
        /// <param name="notConnectUnps"></param>
        /// <returns></returns>
        public bool IsComplexUniprotsConnected(string[] complexUniprots, List<string> structConnectUnpPairList, 
            List<string> pfamConnectUnpPairList, Dictionary<string, string[]> complexSameGnUnpsDict, out string[] notConnectUnps)
        {
            List<string> noConnectUnpList = new List<string> ();
            bool isComplexConnected = true;
            foreach (string unpCode in complexUniprots)
            {
                if (IsUniprotConnected(unpCode, structConnectUnpPairList, complexSameGnUnpsDict))
                {
                    continue;
                }
                if (IsUniprotConnected(unpCode, pfamConnectUnpPairList, complexSameGnUnpsDict))
                {
                    continue;
                }
                isComplexConnected = false;
                noConnectUnpList.Add(unpCode);
            }
            notConnectUnps = noConnectUnpList.ToArray();
            return isComplexConnected;
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <param name="structConnectUnpPairList"></param>
        /// <param name="pfamConnectUnpPairList"></param>
        /// <param name="notConnectUnps"></param>
        /// <returns></returns>
        public bool IsComplexUniprotsStructConnected(string[] complexUniprots, List<string> structConnectUnpPairList,
            Dictionary<string, string[]> complexSameGnUnpsDict, out string[] notConnectUnps)
        {
            List<string> noConnectUnpList = new List<string>();
            bool isComplexConnected = true;
            List<string> connectedUnpList = new List<string>();
            foreach (string unpCode in complexUniprots)
            {
                if (IsUniprotConnected(unpCode, structConnectUnpPairList, complexSameGnUnpsDict))
                {
                    continue;
                }               
                isComplexConnected = false;
                noConnectUnpList.Add(unpCode);
            }
            notConnectUnps = noConnectUnpList.ToArray();
            return isComplexConnected;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="unpPairList"></param>
        /// <returns></returns>
        private bool IsUniprotConnected(string uniprot, List<string> unpPairList, Dictionary<string, string[]> complexSameGnUnpsDict)
        {
            foreach (string unpPair in unpPairList)
            {
                string[] unpFields = unpPair.Split('-');
                if (unpFields[0] == uniprot || unpFields[1] == uniprot)
                {
                    return true;
                }
                else
                {
                    if (complexSameGnUnpsDict.ContainsKey(uniprot))
                    {
                        foreach (string sameGnUnp in complexSameGnUnpsDict[uniprot])
                        {
                            if (unpFields[0] == sameGnUnp || unpFields[1] == sameGnUnp)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPairInterfaceListDict"></param>
        /// <returns></returns>
        private List<string[]> GetUnpPairsNotConnectedByStructures (Dictionary<string[], List<string>> unpPairInterfaceListDict)
        {
            List<string[]> unpPairStructNotConnectList = new List<string[]>();
            List<string[]> unpPairList = new List<string[]>(unpPairInterfaceListDict.Keys);
            bool unpPairConnected = false;
            for (int i = 0; i < unpPairList.Count; i ++)
            {
                unpPairConnected = false;
                for (int j = i + 1; j < unpPairList.Count; j ++)
                {
                    if (unpPairList[i][0] == unpPairList[j][0] || unpPairList[i][0] == unpPairList[j][1])
                    {
                        unpPairConnected = true;
                    }
                    else if (unpPairList[i][1] == unpPairList[j][0] || unpPairList[i][1] == unpPairList[j][1])
                    {
                        unpPairConnected = true;
                    }                  
                }
                if (! unpPairConnected)
                {
                    unpPairStructNotConnectList.Add(unpPairList[i]);
                }
            }
            return unpPairStructNotConnectList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <param name="relBiggestClusterDict"></param>
        /// <returns></returns>
        private DataRow[] GetRelBiggestClusterDict (int[] relSeqIds, Dictionary<int, DataRow> relBiggestClusterDict)
        {
            List<DataRow> clusterRowList = new List<DataRow>();
            foreach (int relSeqId in relSeqIds)
            {
                if (relBiggestClusterDict.ContainsKey(relSeqId))
                {
                    if (relBiggestClusterDict[relSeqId] != null)
                    {
                        clusterRowList.Add(relBiggestClusterDict[relSeqId]);
                    }
                }
                else
                {
                    DataRow clusterRow = GetBiggestDomainCluster(relSeqId);
                    relBiggestClusterDict.Add(relSeqId, clusterRow);
                    if (clusterRow != null)
                    {
                        clusterRowList.Add(clusterRow);
                    }
                }
            }
            return clusterRowList.ToArray();
        }
        #endregion

        #region same-gene uniprots
        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        public bool[] RetrieveComplexPdbProtCidInfo(string complexName, string[] complexUniprots, StreamWriter dataWriter)
        {
            bool canComplexBeHomoModeled = false;
            bool canComplexBeStructModeled = false;
            bool[] canComplexBeModeled = new bool[2];
            Dictionary<string, List<int>> unpPairRelSeqIdListDict = null;
            string[] extendedComplexUnps = null;
            Dictionary<string, string[]> complexSameGnUnpsDict = GetComplexSameGeneUnpsDict(complexUniprots, out extendedComplexUnps);
            Dictionary<string, string> sameGnUnpComplexUnpDict = ReverseComplexSameGnUniprotsDict(complexSameGnUnpsDict);

            Dictionary<string, List<string>> unpPairInterfaceListDict = RetrieveComplexStructureInfo(extendedComplexUnps, out unpPairRelSeqIdListDict);
            Dictionary<string, List<string>> unpPairPfamClusterListDict = FindUnpPairPfamClusters(extendedComplexUnps);
            //           Dictionary<string, List<string>> unpPairInterfaceListDict = RetrieveComplexStructureInfo(complexUniprots, out unpPairRelSeqIdListDict);         
            //           Dictionary<string, List<string>> unpPairPfamClusterListDict = FindUnpPairPfamClusters(complexUniprots);

            List<string> unpPairStructList = new List<string>(unpPairInterfaceListDict.Keys);
            List<string> unpPairPfamList = new List<string>(unpPairPfamClusterListDict.Keys);
            Dictionary<string, List<string>> complexStructConnectListDict = GetComplexConnectedListDict(complexUniprots, unpPairStructList,
                complexSameGnUnpsDict, sameGnUnpComplexUnpDict);
            Dictionary<string, List<string>> complexPfamConnectListDict = GetComplexConnectedListDict(complexUniprots, unpPairPfamList,
                complexSameGnUnpsDict, sameGnUnpComplexUnpDict);
            List<List<string>> structConnectedComponentList = GetLinkedHumanProteins(complexStructConnectListDict);
            //           string[] notStructConnectUniprots = null;
            //           bool isComplexStructConnected = IsComplexUniprotsStructConnected(complexUniprots, unpPairStructList, complexSameGnUnpsDict, out notStructConnectUniprots);
            dataWriter.WriteLine("Can be structure modeled");
            if (structConnectedComponentList.Count > 1)
            {
                dataWriter.WriteLine("#" + complexName + "\tNo\t#Components=" + structConnectedComponentList.Count + "\t" +
                    FormatArrayString(complexUniprots, ';') + "\t" + FormatConnectUniprotsList(structConnectedComponentList));
            }
            else
            {
                canComplexBeStructModeled = true;
                dataWriter.WriteLine("#" + complexName + "\t0\t" + FormatArrayString(complexUniprots, ';'));
            }
            dataWriter.Flush();
            dataWriter.WriteLine("Can be structure/Pfam modeled");
            List<List<string>> pfamConnectedComponentList = GetLinkedHumanProteins(complexPfamConnectListDict);
            //          bool isComplexConnected = IsComplexUniprotsConnected(complexUniprots, unpPairStructList, unpPairPfamList, complexSameGnUnpsDict, out notConnectUniprots);
            if (pfamConnectedComponentList.Count > 1)
            {
                dataWriter.WriteLine("#" + complexName + "\tNo\t#Components=" + pfamConnectedComponentList.Count + "\t" +
                    FormatArrayString(complexUniprots, ';') + "\t" + FormatConnectUniprotsList(pfamConnectedComponentList));
            }
            else
            {
                canComplexBeHomoModeled = true;
                dataWriter.WriteLine("#" + complexName + "\t0\t" + FormatArrayString(complexUniprots, ';'));
            }
            dataWriter.Flush();
            bool isFirstLine = true;
            foreach (string complexUnp in complexSameGnUnpsDict.Keys)
            {
                if (complexSameGnUnpsDict[complexUnp].Length > 0)
                {
                    if (isFirstLine)
                    {
                        dataWriter.WriteLine("Same Gene Uniprots in PDB");
                        isFirstLine = false;
                    }
                    dataWriter.WriteLine(complexUnp + "\t" + FormatArrayString(complexSameGnUnpsDict[complexUnp], ';'));
                }
            }
            if (unpPairInterfaceListDict.Count == 0 && unpPairPfamClusterListDict.Count == 0)
            {
                dataWriter.WriteLine("No Structural interactions  and Pfam clusters.");
                dataWriter.WriteLine();
                dataWriter.Flush();
                canComplexBeHomoModeled = false;
                canComplexBeStructModeled = false;
                canComplexBeModeled[0] = canComplexBeStructModeled;
                canComplexBeModeled[1] = canComplexBeHomoModeled;
                return canComplexBeModeled;
            }
            Dictionary<int, DataRow> relBiggestClusterDict = new Dictionary<int, DataRow>();
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int, string[]>();
            for (int i = 0; i < complexUniprots.Length; i++)
            {
                string[] sameGnUniprotsI = GetSameGnUniprots(complexUniprots[i], complexSameGnUnpsDict);
                for (int j = i + 1; j < complexUniprots.Length; j++)
                {
                    string[] sameGnUniprotsJ = GetSameGnUniprots(complexUniprots[j], complexSameGnUnpsDict);
                    foreach (string uniprotI in sameGnUniprotsI)
                    {
                        foreach (string uniprotJ in sameGnUniprotsJ)
                        {
                            PrintUnpPairStructPfamInfo(uniprotI, uniprotJ, unpPairInterfaceListDict, unpPairPfamClusterListDict,
                                                relBiggestClusterDict, relPfamPairDict, unpPairRelSeqIdListDict, dataWriter);
                        }
                    }
                }
            }
            dataWriter.WriteLine();
            dataWriter.Flush();

            canComplexBeModeled[0] = canComplexBeStructModeled;
            canComplexBeModeled[1] = canComplexBeHomoModeled;
            return canComplexBeModeled;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPairList"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetComplexConnectedListDict(string[] complexUniprots, List<string> unpPairList,
            Dictionary<string, string[]> complexSameGnUnpsDict, Dictionary<string, string> sameGnUnpComplexUnpDict)
        {
            Dictionary<string, List<string>> unpConnectedListDict = new Dictionary<string, List<string>>();
            string interactUniprot = "";
            string interactComplexUnp = "";
            foreach (string complexUniprot in complexUniprots)
            {
                string[] sameGnUniprots = GetSameGnUniprots(complexUniprot, complexSameGnUnpsDict);
                foreach (string unpPair in unpPairList)
                {
                    interactUniprot = "";
                    interactComplexUnp = "";
                    string[] unpFields = unpPair.Split('-');
                    if (unpFields[0] == complexUniprot || sameGnUniprots.Contains(unpFields[0]))
                    {
                        interactUniprot = unpFields[1];
                    }
                    else if (unpFields[1] == complexUniprot || sameGnUniprots.Contains(unpFields[1]))
                    {
                        interactUniprot = unpFields[0];
                    }

                    if (complexUniprots.Contains(interactUniprot))
                    {
                        interactComplexUnp = interactUniprot;
                    }
                    else
                    {
                        if (sameGnUnpComplexUnpDict.ContainsKey(interactUniprot))
                        {
                            interactComplexUnp = sameGnUnpComplexUnpDict[interactUniprot];
                        }
                    }
                    if (interactComplexUnp != "")
                    {
                        if (unpConnectedListDict.ContainsKey(complexUniprot))
                        {
                            if (!unpConnectedListDict[complexUniprot].Contains(interactComplexUnp))
                            {
                                unpConnectedListDict[complexUniprot].Add(interactComplexUnp);
                            }
                        }
                        else
                        {
                            List<string> connectUnpList = new List<string>();
                            connectUnpList.Add(interactComplexUnp);
                            unpConnectedListDict.Add(complexUniprot, connectUnpList);
                        }

                        if (unpConnectedListDict.ContainsKey(interactComplexUnp))
                        {
                            if (!unpConnectedListDict[interactComplexUnp].Contains(complexUniprot))
                            {
                                unpConnectedListDict[interactComplexUnp].Add(complexUniprot);
                            }
                        }
                        else
                        {
                            List<string> connectUnpList = new List<string>();
                            connectUnpList.Add(complexUniprot);
                            unpConnectedListDict.Add(interactComplexUnp, connectUnpList);
                        }
                    }
                }
                if (!unpConnectedListDict.ContainsKey(complexUniprot))
                {
                    List<string> connectUnpList = new List<string>();
                    unpConnectedListDict.Add(complexUniprot, connectUnpList);
                }
            }
            return unpConnectedListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexSameGnUnpsDict"></param>
        /// <returns></returns>
        public Dictionary<string, string> ReverseComplexSameGnUniprotsDict(Dictionary<string, string[]> complexSameGnUnpsDict)
        {
            Dictionary<string, string> sameGnUnpComplexUnpDict = new Dictionary<string, string>();
            foreach (string complexUnp in complexSameGnUnpsDict.Keys)
            {
                foreach (string sameGnUnp in complexSameGnUnpsDict[complexUnp])
                {
                    if (!sameGnUnpComplexUnpDict.ContainsKey(sameGnUnp))
                    {
                        sameGnUnpComplexUnpDict.Add(sameGnUnp, complexUnp);
                    }
                }
            }
            return sameGnUnpComplexUnpDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprot"></param>
        /// <param name="complexSameGnUniprotsDict"></param>
        /// <returns></returns>
        public string[] GetSameGnUniprots(string complexUniprot, Dictionary<string, string[]> complexSameGnUniprotsDict)
        {
            string[] sameGnUniprots = new string[complexSameGnUniprotsDict[complexUniprot].Length + 1];
            sameGnUniprots[0] = complexUniprot;
            Array.Copy(complexSameGnUniprotsDict[complexUniprot], 0, sameGnUniprots, 1, complexSameGnUniprotsDict[complexUniprot].Length);
            return sameGnUniprots;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetComplexSameGeneUnpsDict(string[] complexUniprots, out string[] extendedComplexUnps)
        {
            List<string> extendedComplexUnpList = new List<string>(complexUniprots);
            Dictionary<string, string[]> complexUnpSameGnUnpsDict = new Dictionary<string, string[]>();
            foreach (string unpCode in complexUniprots)
            {
                string[] sameGnUnpCodes = GetSameGeneUniprotsInPdb(unpCode);
                foreach (string gnUnp in sameGnUnpCodes)
                {
                    if (!extendedComplexUnpList.Contains(gnUnp))
                    {
                        extendedComplexUnpList.Add(gnUnp);
                    }
                }
                List<string> sameGnOtherSpeciesUnpList = new List<string>(sameGnUnpCodes);
                sameGnOtherSpeciesUnpList.Remove(unpCode);
                complexUnpSameGnUnpsDict.Add(unpCode, sameGnOtherSpeciesUnpList.ToArray());
            }
            extendedComplexUnpList.Sort();
            extendedComplexUnps = extendedComplexUnpList.ToArray();
            return complexUnpSameGnUnpsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        public string[] GetSameGeneUniprotsInPdb(string unpCode)
        {
            string[] sameGnUnpCodes = new string[0];
            if (geneUnpListDict == null || geneUnpListDict.Count == 0)
            {
                string queryString = "";
                DataTable unpCodeTable = null;
                if (unpCode.IndexOf("_HUMAN") > -1)
                {
                    queryString = string.Format("Select Distinct UnpSeqInfo.UnpCode From UnpSeqInfo, HumanSeqInfo " +
                        " Where HumanSeqInfo.UnpCode = '{0}' AND HumanSeqInfo.GN = Upper(UnpSeqInfo.GN) AND HumanSeqInfo.GN <> '-';", unpCode);
                    unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                }
                else
                {
                    queryString = string.Format("Select Distinct HumanSeqInfo.UnpCode From UnpSeqInfo, HumanSeqInfo " +
                        " Where UnpSeqInfo.UnpCode = '{0}' AND HumanSeqInfo.GN = Upper(UnpSeqInfo.GN) AND UnpSeqInfo.GN <> '-';", unpCode);
                    unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                    queryString = string.Format("Select Distinct UnpCode From UnpSeqInfo " +
                        " Where GN IN " +
                        " (Select Distinct GN From UnpSeqInfo Where UnpCode = '{0}' AND Isoform = 0 AND GN <> '-') AND UnpCode <> '{0}';", unpCode);
                    DataTable notHuUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                    ParseHelper.AddNewTableToExistTable(notHuUnpTable, ref unpCodeTable);
                }
                sameGnUnpCodes = new string[unpCodeTable.Rows.Count];
                int count = 0;
                foreach (DataRow unpCodeRow in unpCodeTable.Rows)
                {
                    sameGnUnpCodes[count] = unpCodeRow["UnpCode"].ToString().TrimEnd();
                    count++;
                }
            }
            else
            {
                if (unpGeneListDict.ContainsKey(unpCode))
                {
                    string gene = unpGeneListDict[unpCode];
                    if (geneUnpListDict.ContainsKey(gene))
                    {
                        sameGnUnpCodes = geneUnpListDict[gene].ToArray();
                    }
                }
            }
            return sameGnUnpCodes;
        }
        #endregion

        #region check uniprot can be connected by structures
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="pdbChainDbRangeDict"></param>
        /// <returns></returns>
        public List<int[]> GetUniprotStructConnectRangeList (string uniprot, out Dictionary<string, List<int[]>> pdbChainDbRangeDict)
        {
            pdbChainDbRangeDict = new Dictionary<string, List<int[]>>();
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbId, EntityId, AuthorChain, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where DbCode = '{0}' AND PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId;", uniprot);
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpPdbSeqAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct PdbDbRefXml.PdbId, EntityId, AuthorChain, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                    " From PdbDbRefXml, PdbDbRefSeqXml " +
                    " Where DbCode = '{0}' AND PdbDbRefXml.PdbId = PdbDbRefSeqXml.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqXml.RefId;", uniprot);
                unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            int[] dbAlignRange = new int[2];
            string pdbChain = "";
            List<int[]> dbRangeList = new List<int[]>();
            foreach (DataRow seqAlignRow in unpPdbSeqAlignTable.Rows)
            {
                dbAlignRange[0] = Convert.ToInt32(seqAlignRow["DbAlignBeg"].ToString());
                dbAlignRange[1] = Convert.ToInt32(seqAlignRow["DbAlignEnd"].ToString());
                dbRangeList.Add(dbAlignRange);
                pdbChain = seqAlignRow["PdbID"].ToString() + seqAlignRow["AuthorChain"].ToString();
                if (pdbChainDbRangeDict.ContainsKey(pdbChain))
                {
                    pdbChainDbRangeDict[pdbChain].Add(dbAlignRange);
                }
                else
                {
                    List<int[]> rangeList = new List<int[]>();
                    rangeList.Add(dbAlignRange);
                    pdbChainDbRangeDict.Add(pdbChain, rangeList);
                }
            }
            List<int[]> connectRangeList = GetListConnectedRanges(dbRangeList);
            return connectRangeList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="pdbChainDbRangeDict"></param>
        /// <returns></returns>
        public Dictionary<string, List<int[]>> GetUniprotStructConnectRangeList(string[] uniprots)
        {
            Dictionary<string, List<int[]>> unpConnectRangeListDict = new Dictionary<string, List<int[]>>();
            string queryString = string.Format("Select Distinct DbCode, DbAlignBeg, DbAlignEnd" +
                " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where DbCode IN ({0}) AND " +
                " PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId;",
                ParseHelper.FormatSqlListString(uniprots));
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpPdbSeqAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode, DbAlignBeg, DbAlignEnd" +
                    " From PdbDbRefXml, PdbDbRefSeqXml " +
                    " Where DbCode IN ({0}) AND " +
                    " PdbDbRefXml.PdbId = PdbDbRefSeqXml.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqXml.RefId;",
                    ParseHelper.FormatSqlListString(uniprots));
                unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            int[] dbAlignRange = null;
            List<int[]> dbRangeList = new List<int[]>();
            string unpCode = "";
            Dictionary<string, List<int[]>> unpRangeListDict = new Dictionary<string, List<int[]>>();
            foreach (DataRow seqAlignRow in unpPdbSeqAlignTable.Rows)
            {
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                dbAlignRange = new int[2];
                dbAlignRange[0] = Convert.ToInt32(seqAlignRow["DbAlignBeg"].ToString());
                dbAlignRange[1] = Convert.ToInt32(seqAlignRow["DbAlignEnd"].ToString());
                if (unpRangeListDict.ContainsKey(unpCode))
                {
                    unpRangeListDict[unpCode].Add(dbAlignRange);
                }
                else
                {
                    List<int[]> rangeList = new List<int[]>();
                    rangeList.Add(dbAlignRange);
                    unpRangeListDict.Add(unpCode, rangeList);
                }
            }
            foreach (string keyUnpCode in unpRangeListDict.Keys)
            {
                List<int[]> connectRangeList = GetListConnectedRanges(unpRangeListDict[keyUnpCode]);
                unpConnectRangeListDict.Add(keyUnpCode, connectRangeList);
            }
            return unpConnectRangeListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public Dictionary<string, List<int[]>> GetUniprotSeqConnectRangeList(string[] uniprots, string[] pdbIds)
        {
            Dictionary<string, List<int[]>> unpConnectRangeListDict = new Dictionary<string, List<int[]>>();
            string queryString = string.Format("Select Distinct DbCode, DbAlignBeg, DbAlignEnd" +
                " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where DbCode IN ({0}) AND PdbDbRefSifts.PdbId IN ({1}) AND " +
                " PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId;",
                ParseHelper.FormatSqlListString(uniprots), ParseHelper.FormatSqlListString(pdbIds));
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpPdbSeqAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode, DbAlignBeg, DbAlignEnd" +
                    " From PdbDbRefXml, PdbDbRefSeqXml " +
                    " Where DbCode IN ({0}) AND PdbDbRefXml.PdbId IN ({1}) AND " +
                    " PdbDbRefXml.PdbId = PdbDbRefSeqXml.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqXml.RefId;",
                    ParseHelper.FormatSqlListString(uniprots), ParseHelper.FormatSqlListString(pdbIds));
                unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            int[] dbAlignRange = null;
            List<int[]> dbRangeList = new List<int[]>();
            string unpCode = "";
            Dictionary<string, List<int[]>> unpRangeListDict = new Dictionary<string, List<int[]>>();
            foreach (DataRow seqAlignRow in unpPdbSeqAlignTable.Rows)
            {
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                dbAlignRange = new int[2];
                dbAlignRange[0] = Convert.ToInt32(seqAlignRow["DbAlignBeg"].ToString());
                dbAlignRange[1] = Convert.ToInt32(seqAlignRow["DbAlignEnd"].ToString());
                if (unpRangeListDict.ContainsKey(unpCode))
                {
                    unpRangeListDict[unpCode].Add(dbAlignRange);
                }
                else
                {
                    List<int[]> rangeList = new List<int[]>();
                    rangeList.Add(dbAlignRange);
                    unpRangeListDict.Add(unpCode, rangeList);
                }
            }
            foreach (string keyUnpCode in unpRangeListDict.Keys)
            {
                List<int[]> connectRangeList = GetListConnectedRanges(unpRangeListDict[keyUnpCode]);
                unpConnectRangeListDict.Add(keyUnpCode, connectRangeList);
            }
            return unpConnectRangeListDict;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public Dictionary<string, List<int[]>> GetUniprotStructConnectRangeList(string[] uniprots, string[] pdbIds)
        {
            Dictionary<string, List<int[]>> unpConnectRangeListDict = new Dictionary<string, List<int[]>>();
            string queryString = string.Format("Select Distinct DbCode, PdbDbRefSifts.PdbID, PdbDbRefSeqSifts.AuthorChain, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd, SequenceInCoord" +
                " From PdbDbRefSifts, PdbDbRefSeqSifts, AsymUnit " +
                " Where DbCode IN ({0}) AND PdbDbRefSifts.PdbId IN ({1}) AND " +
                " PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId AND " + 
                " PdbDbRefSeqSifts.PdbID = AsymUnit.PdbID AND PdbDbRefSeqSifts.AuthorChain = AsymUnit.AuthorChain AND PolymerType = 'polypeptide';",
                ParseHelper.FormatSqlListString(uniprots), ParseHelper.FormatSqlListString(pdbIds));
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpPdbSeqAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode,  PdbDbRefXml.PdbID, PdbDbRefSeqXml.AuthorChain, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd, SequenceInCoord" +
                    " From PdbDbRefXml, PdbDbRefSeqXml, AsymUnit" +
                    " Where DbCode IN ({0}) AND PdbDbRefXml.PdbId IN ({1}) AND " +
                    " PdbDbRefXml.PdbId = PdbDbRefSeqXml.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqXml.RefId AND" + 
                    " PdbDbRefSeqXml.PdbID = AsymUnit.PdbID AND PdbDbRefSeqXml.AuthorChain = AsymUnit.AuthorChain AND PolymerType = 'polypeptide';",
                    ParseHelper.FormatSqlListString(uniprots), ParseHelper.FormatSqlListString(pdbIds));
                unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            int[] dbAlignRange = null;
            int[] dbCoordRange = null;
            int[] seqAlignRange = null;
            int[] coordRange = null;
            List<int[]> dbRangeList = new List<int[]>();
            string unpCode = "";            
            Dictionary<string, List<int[]>> unpRangeListDict = new Dictionary<string, List<int[]>>();
            foreach (DataRow seqAlignRow in unpPdbSeqAlignTable.Rows)
            {
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                dbAlignRange = new int[2];
                dbAlignRange[0] = Convert.ToInt32(seqAlignRow["DbAlignBeg"].ToString());
                dbAlignRange[1] = Convert.ToInt32(seqAlignRow["DbAlignEnd"].ToString());
                seqAlignRange = new int[2];
                seqAlignRange[0] = Convert.ToInt32(seqAlignRow["SeqAlignBeg"].ToString());
                seqAlignRange[1] = Convert.ToInt32(seqAlignRow["SeqAlignEnd"].ToString());
                coordRange = GetCoordRange(seqAlignRow["SequenceInCoord"].ToString());
                dbCoordRange = GetUnpRangeInCoord(dbAlignRange, seqAlignRange, coordRange);
                if (unpRangeListDict.ContainsKey(unpCode))
                {
                    unpRangeListDict[unpCode].Add(dbCoordRange);
                }
                else
                {
                    List<int[]> rangeList = new List<int[]>();
                    rangeList.Add(dbCoordRange);
                    unpRangeListDict.Add(unpCode, rangeList);
                }
            }
            foreach (string keyUnpCode in unpRangeListDict.Keys)
            {
                List<int[]> connectRangeList = GetListConnectedRanges(unpRangeListDict[keyUnpCode]);
                unpConnectRangeListDict.Add(keyUnpCode, connectRangeList);
            }
            return unpConnectRangeListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authSeqNumLine"></param>
        /// <returns></returns>
        private int[] GetCoordRange (string seqInCoord)
        {           
            int[] coordRange = new int[2];
            int startPos = 0;
            int endPos = 0;
            for (int i = 0; i < seqInCoord.Length; i++)
            {
                if (seqInCoord[i] != '-')
                {
                    startPos = i + 1;
                    break;
                }               
            }

            for (int i = seqInCoord.Length - 1; i >= 0; i--)
            {
                if (seqInCoord[i] != '-')
                {
                    endPos = i + 1;
                    break;
                }
            }
            coordRange[0] = startPos;
            coordRange[1] = endPos;
            return coordRange;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRange"></param>
        /// <param name="authRange"></param>
        /// <param name="authCoordRange"></param>
        /// <returns></returns>
        private int[] GetUnpRangeInCoord (int[] unpRange, int[] authRange, int[] authCoordRange)
        {
            int[] coordUnpRange = new int[2];
            if (authRange[0] < authCoordRange[0])
            {
                coordUnpRange[0] = unpRange[0] + authCoordRange[0] - authRange[0];
            }
            else
            {
                coordUnpRange[0] = unpRange[0];
            }

            if (authRange[1] > authCoordRange[1])
            {
                coordUnpRange[1] = unpRange[1] -  (authRange[1] - authCoordRange[1]);
            }
            else
            {
                coordUnpRange[1] = unpRange[1];
            }
            return coordUnpRange;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots1"></param>
        /// <param name="pdbIds1"></param>
        /// <param name="uniprots2"></param>
        /// <param name="pdbIds2"></param>
        /// <returns></returns>
        public bool CanTwoComplexesConnected (string[] uniprots1, string[] pdbIds1, string[] uniprots2, string[] pdbIds2)
        {
            Dictionary<string, List<int[]>> unpRangeListDict1 = GetUniprotStructConnectRangeList(uniprots1, pdbIds1);
            Dictionary<string, List<int[]>> unpRangeListDict2 = GetUniprotStructConnectRangeList(uniprots2, pdbIds2);
            bool isConnected = AreAnyUniprotsRangesOverlap(unpRangeListDict1, unpRangeListDict2);
            if (! isConnected)
            {
                Dictionary<string, List<int[]>> unpConnectRangeListDict1 = GetUniprotStructConnectRangeList(uniprots1);
                Dictionary<string, List<int[]>> unpConnectRangeListDict2 = GetUniprotStructConnectRangeList(uniprots2);
                isConnected = AreAnyUniprotsRangesOverlap(unpConnectRangeListDict1, unpConnectRangeListDict2);
            }
            return isConnected;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeListDict1"></param>
        /// <param name="unpRangeListDict2"></param>
        /// <returns></returns>
        public bool AreAnyUniprotsRangesOverlap (Dictionary<string, List<int[]>> unpRangeListDict1, Dictionary<string, List<int[]>> unpRangeListDict2)
        {
            bool isOverlap = false;
            foreach (string unp1 in unpRangeListDict1.Keys)
            {
                if (unpRangeListDict2.ContainsKey (unp1))
                {
                    if (AreTwoRangeListsOverlap (unpRangeListDict1[unp1], unpRangeListDict2[unp1]))
                    {
                        isOverlap = true;
                    }
                }
            }
            return isOverlap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rangeList1"></param>
        /// <param name="rangeList2"></param>
        /// <returns></returns>
        private bool AreTwoRangeListsOverlap (List<int[]> rangeList1, List<int[]> rangeList2)
        {
            foreach (int[] range1 in rangeList1)
            {
                foreach (int[] range2 in rangeList2)
                {
                    if (AreTwoRangesOverlap (range1, range2))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, List<int[]>> GetEntryUniprotRanges (string pdbId)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbId, DbCode, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where PdbDbRefSifts.PdbID = '{0}' AND DbName = 'UNP' " +
                " AND PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId;", pdbId);
            DataTable unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpPdbSeqAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct PdbDbRefXml.PdbId, DBCode, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                    " From PdbDbRefXml, PdbDbRefSeqXml " +
                    " Where PdbDbRefXml.PdbID = '{0}' AND DbName = 'UNP'  AND " +
                    " PdbDbRefXml.PdbId = PdbDbRefSeqXml.PdbId AND PdbDbRefXml.RefId = PdbDbRefSeqXml.RefId;", pdbId);
                unpPdbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            Dictionary<string, List<int[]>> unpDbRangeListDict = new Dictionary<string, List<int[]>>();
            string unpCode = "";
            int[] range = new int[2];
            foreach (DataRow seqAlignRow in unpPdbSeqAlignTable.Rows)
            {
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                range = new int[2];
                range[0] = Convert.ToInt32(seqAlignRow["DbAlignBeg"].ToString ());
                range[1] = Convert.ToInt32(seqAlignRow["DbAlignEnd"].ToString ());
                if (unpDbRangeListDict.ContainsKey (unpCode))
                {
                    unpDbRangeListDict[unpCode].Add(range);
                }
                else
                {
                    List<int[]> rangeList = new List<int[]>();
                    rangeList.Add(range);
                    unpDbRangeListDict.Add(unpCode, rangeList);
                }
            }
            return unpDbRangeListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputRangeList"></param>
        /// <returns></returns>
        public List<int[]> GetListConnectedRanges (List<int[]> inputRangeList)
        {
            List<int[]> leftRangeList = new List<int[]>(inputRangeList);
            List<int[]> rangeList = null;
            List<int[]> connectRangeList = new List<int[]>();

            int[] connectRange = null;
            while (leftRangeList.Count > 0)
            {
                connectRange = new int[2];
                connectRange[0] = leftRangeList[0][0];
                connectRange[1] = leftRangeList[0][1];
                leftRangeList.RemoveAt(0);
                rangeList = new List<int[]>(leftRangeList);
                for (int i = 0; i < rangeList.Count; i++)
                {
                    if (AreTwoRangesOverlap(rangeList[i], connectRange))
                    {
                        if (connectRange[0] > rangeList[i][0])
                        {
                            connectRange[0] = rangeList[i][0];
                        }
                        if (connectRange[1] < rangeList[i][1])
                        {
                            connectRange[1] = rangeList[i][1];
                        }
                        leftRangeList.Remove(rangeList[i]);                    
                    }                    
                }
                connectRangeList.Add(connectRange);
            }
            return connectRangeList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="range1"></param>
        /// <param name="range2"></param>
        /// <returns></returns>
        private bool AreTwoRangesOverlap (int[] range1, int[] range2)
        {
            if (range1[0] >= range2[0] && range1[0] <= range2[1])
            {
                return true;
            }
            if (range1[1] >= range2[0] && range1[1] <= range2[1])
            {
                return true;
            }

            if (range2[0] >= range1[0] && range2[0] <= range1[1])
            {
                return true;
            }
            if (range2[1] >= range1[0] && range2[1] <= range1[1])
            {
                return true;
            }
            return false;
        }
        #endregion

        #region find Pfam-Pfam interactions for uniprot complexes
        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        public string[] FindPfamClusters (string[] complexUniprots)
        {
            Dictionary<string, string[]> unpPfamsDict = GetUniprotPfamsDict(complexUniprots);
            Dictionary<string[], string[]> unpPairCommonPfamsDict = FindUnpPairCommonPfams(complexUniprots, unpPfamsDict);
            Dictionary<int, DataRow> relBiggestClusterDict = new Dictionary<int,DataRow> ();
            Dictionary<int, string[]> relPfamPairDict = null;
            Dictionary<string, int> unpPairRelIdDict = new Dictionary<string, int>();
            List<string> dataLineList = new List<string>();
            string dataLine = "";
            int relSeqId = 0;
            for (int i = 0; i < complexUniprots.Length; i++)
            {
                string[] pfamIdsI = unpPfamsDict[complexUniprots[i]];
                for (int j = i + 1; j < complexUniprots.Length; j++)
                {
                    string[] pfamIdsJ = unpPfamsDict[complexUniprots[j]];
                    /*
                      string queryString = string.Format("Select RelSeqID, ClusterID, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, " +
                " NumOfEntryRelation, MinSeqIdentity, SurfaceArea, InPdb, InPisa, InAsu " +
                " From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster DESC;", relSeqId);                    
                     */
                    DataRow[] clusterRows = FindPfamBiggestClusterRows(pfamIdsI, pfamIdsJ, relBiggestClusterDict, out relPfamPairDict);
                    foreach (DataRow clusterRow in clusterRows)
                    {
                        relSeqId = Convert.ToInt32 (clusterRow["RelSeqID"].ToString ());
                        dataLine = complexUniprots[i] + "\t" + complexUniprots[j] + "\t" + relPfamPairDict[relSeqId][0] + "\t" + relPfamPairDict[relSeqId][1] + "\t" +
                            ParseHelper.FormatDataRow (clusterRow);
                        dataLineList.Add(dataLine);
                    }
                }
            }
            return dataLineList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        public Dictionary<string, List<string>> FindUnpPairPfamClusters(string[] complexUniprots)
        {
            Dictionary<string, string[]> unpPfamsDict = GetUniprotPfamsDict(complexUniprots);
    //        Dictionary<string[], string[]> unpPairCommonPfamsDict = FindUnpPairCommonPfams(complexUniprots, unpPfamsDict);
            Dictionary<int, DataRow> relBiggestClusterDict = new Dictionary<int, DataRow>();
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int,string[]> ();
            List<string> dataLineList = new List<string>();
            string dataLine = "";
            int relSeqId = 0;
            Dictionary<string, List<string>> unpPairPfamClusterListDict = new Dictionary<string, List<string>>();
            for (int i = 0; i < complexUniprots.Length; i++)
            {
                string[] pfamIdsI = unpPfamsDict[complexUniprots[i]];
                for (int j = i + 1; j < complexUniprots.Length; j++)
                {
                    dataLineList = new List<string>();
                    string[] pfamIdsJ = unpPfamsDict[complexUniprots[j]];
                    DataRow[] clusterRows = FindPfamBiggestClusterRows(pfamIdsI, pfamIdsJ, relBiggestClusterDict, out relPfamPairDict);
                    foreach (DataRow clusterRow in clusterRows)
                    {
                        relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                        dataLine = relPfamPairDict[relSeqId][0] + "\t" + relPfamPairDict[relSeqId][1] + "\t" + ParseHelper.FormatDataRow(clusterRow);
                        dataLineList.Add(dataLine);
                    }
                    if (dataLineList.Count > 0)
                    {
                        string unpPair =  complexUniprots[i] + "-" + complexUniprots[j];
                        if (string.Compare(complexUniprots[i], complexUniprots[j]) > 0)
                        {
                            unpPair = complexUniprots[j] + "-" + complexUniprots[i];
                        }
                        unpPairPfamClusterListDict.Add(unpPair, dataLineList);
                    }
                }
            }
            return unpPairPfamClusterListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        public Dictionary<string, List<string>> FindUnpPairPfamClusters(Dictionary<string, string[]> unpSameGnUnpsDict)
        {
            List<string> complexUniprotList = new List<string> (unpSameGnUnpsDict.Keys);
            string[] complexUniprots = complexUniprotList.ToArray();
            List<string> extendedUniprotList = new List<string>(complexUniprotList);
            foreach (string uniprot in unpSameGnUnpsDict.Keys)
            {
                foreach (string sameGnUniprot in unpSameGnUnpsDict[uniprot])
                {
                    if (! extendedUniprotList.Contains (sameGnUniprot))
                    {
                        extendedUniprotList.Add(sameGnUniprot);
                    }
                }
            }
            string[] extendedComplexUniprots = extendedUniprotList.ToArray();
            Dictionary<string, string[]> unpPfamsDict = GetUniprotPfamsDict(extendedComplexUniprots);
            //        Dictionary<string[], string[]> unpPairCommonPfamsDict = FindUnpPairCommonPfams(complexUniprots, unpPfamsDict);
            Dictionary<int, DataRow> relBiggestClusterDict = new Dictionary<int, DataRow>();
            Dictionary<int, string[]> relPfamPairDict = null;
            List<string> dataLineList = new List<string>();
            string dataLine = "";
            int relSeqId = 0;
            string uniprotI = "";
            string uniprotJ = "";
            Dictionary<string, List<string>> unpPairPfamClusterListDict = new Dictionary<string, List<string>>();
            for (int i = 0; i < complexUniprots.Length; i++)
            {
                uniprotI = complexUniprots[i];
                string[] pfamIdsI = unpPfamsDict[uniprotI];
                if (pfamIdsI.Length == 0)
                {
                    foreach (string sameGnUnp in unpSameGnUnpsDict[uniprotI])
                    {
                        pfamIdsI = unpPfamsDict[sameGnUnp];
                        if (pfamIdsI.Length > 0)
                        {
                            uniprotI = sameGnUnp;
                            break;
                        }
                    }
                }
                if (pfamIdsI.Length == 0)
                {
                    continue;
                }
                for (int j = i + 1; j < complexUniprots.Length; j++)
                {
                    dataLineList = new List<string>();
                    uniprotJ = complexUniprots[j];
                    string[] pfamIdsJ = unpPfamsDict[uniprotJ];
                    if (pfamIdsJ.Length == 0)
                    {
                        foreach (string sameGnUnp in unpSameGnUnpsDict[uniprotJ])
                        {
                            pfamIdsJ = unpPfamsDict[sameGnUnp];
                            if (pfamIdsJ.Length > 0)
                            {
                                uniprotJ = sameGnUnp;
                                break;
                            }
                        }
                    }
                    if (pfamIdsJ.Length == 0)
                    {
                        continue;
                    }
                    DataRow[] clusterRows = FindPfamBiggestClusterRows(pfamIdsI, pfamIdsJ, relBiggestClusterDict, out relPfamPairDict);
                    foreach (DataRow clusterRow in clusterRows)
                    {
                        relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                        dataLine = relPfamPairDict[relSeqId][0] + "\t" + relPfamPairDict[relSeqId][1] + "\t" + ParseHelper.FormatDataRow(clusterRow);
                        dataLineList.Add(dataLine);
                    }
                    if (dataLineList.Count > 0)
                    {
                        string unpPair = uniprotI + "-" + uniprotJ;
                        if (string.Compare(uniprotI, uniprotJ) > 0)
                        {
                            unpPair = uniprotJ + "-" + uniprotI;
                        }
                        unpPairPfamClusterListDict.Add(unpPair, dataLineList);
                    }
                }
            }
            return unpPairPfamClusterListDict;
        }  
        /// <summary>
        /// common Pfams for unp pairs
        /// </summary>
        /// <param name="complexUniprots"></param>
        /// <param name="unpPfamsDict"></param>
        /// <returns></returns>
        public Dictionary<string[], string[]> FindUnpPairCommonPfams(string[] complexUniprots, Dictionary<string, string[]> unpPfamsDict)
        {          
            Dictionary<string[], string[]> unpPairCommonPfamsDict = new Dictionary<string[], string[]>();
            for (int i = 0; i < complexUniprots.Length; i ++ )
            {
                string[] pfamIdsI = unpPfamsDict[complexUniprots[i]];
                for (int j = i + 1; j < complexUniprots.Length; j ++)
                {
                    string[] pfamIdsJ = unpPfamsDict[complexUniprots[j]];
                    string[] commonPfams = GetCommonPfams(pfamIdsI, pfamIdsJ);
                    if (commonPfams.Length > 0)
                    {
                        string[] unpPair = new string[2];
                        unpPair[0] = complexUniprots[i];
                        unpPair[1] = complexUniprots[j];
                        unpPairCommonPfamsDict.Add(unpPair, commonPfams);
                    }
                }
            }
            return unpPairCommonPfamsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds1"></param>
        /// <param name="pfamIds2"></param>
        /// <returns></returns>
        private string[] GetCommonPfams (string[] pfamIds1, string[] pfamIds2)
        {
            List<string> commonPfamList = new List<string>(pfamIds1);
            foreach (string pfamId in pfamIds1)
            {
                if (! pfamIds2.Contains (pfamId))
                {
                    commonPfamList.Remove(pfamId);
                }
            }
            return commonPfamList.ToArray();
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="pfamIds1"></param>
       /// <param name="pfamIds2"></param>
       /// <param name="relBiggestClusterDict"></param>
       /// <param name="relPfamPairDict"></param>
       /// <returns></returns>
        public DataRow[] FindPfamBiggestClusterRows (string[] pfamIds1, string[] pfamIds2, Dictionary<int, DataRow> relBiggestClusterDict, 
           out Dictionary<int, string[]> relPfamPairDict)
        {
            relPfamPairDict = GetRelationPfamPairDict(pfamIds1, pfamIds2);
            List<DataRow> clusterRowList = new List<DataRow>();
            foreach (int relSeqId in relPfamPairDict.Keys)
            {
                if (relBiggestClusterDict.ContainsKey(relSeqId))
                {
                    if (relBiggestClusterDict[relSeqId] != null)
                    {
                        clusterRowList.Add(relBiggestClusterDict[relSeqId]);
                    }
                }
                else
                {
                    DataRow biggestClusterRow = GetBiggestDomainCluster(relSeqId);
                    if (biggestClusterRow != null)
                    {
                        clusterRowList.Add(biggestClusterRow);
                    }
                    relBiggestClusterDict.Add(relSeqId, biggestClusterRow);
                }
            }
            return clusterRowList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public DataRow GetBiggestDomainCluster(int relSeqId)
        {
            string queryString = string.Format("Select RelSeqID, ClusterID, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, " +
                " NumOfEntryRelation, MinSeqIdentity, SurfaceArea, InPdb, InPisa, InAsu " +
                " From PfamDomainClusterSumInfo Where RelSeqID = {0} Order By NumOfCfgCluster DESC;", relSeqId);
            DataTable domainClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (domainClusterTable.Rows.Count > 0)
            {
                return domainClusterTable.Rows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds1"></param>
        /// <param name="pfamIds2"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetRelationPfamPairDict (string[] pfamIds1, string[] pfamIds2)
        {
            Dictionary<int, string[]> relPfamPairDict = new Dictionary<int, string[]>();
            int relSeqId = 0;
            string pfamPairStr = "";
            foreach (string pfamId1 in pfamIds1)
            {
                foreach (string pfamId2 in pfamIds2)
                {
                    pfamPairStr = pfamId1 + ";" + pfamId2;
                    if (string.Compare (pfamId1, pfamId2) > 0)
                    {
                        pfamPairStr = pfamId2 + ";" + pfamId1;
                    }
                    if (pfamPairRelIdDict.ContainsKey(pfamPairStr))
                    {
                        relSeqId = pfamPairRelIdDict[pfamPairStr];
                    }
                    else
                    {
                        relSeqId = GetRelSeqId(pfamId1, pfamId2);
                        pfamPairRelIdDict.Add(pfamPairStr, relSeqId);
                    }
                    if (relSeqId <= 0)
                    {
                        continue;
                    }
                    if (!relPfamPairDict.ContainsKey(relSeqId))
                    {
                        string[] pfamPair = new string[2];
                        pfamPair[0] = pfamId1;
                        pfamPair[1] = pfamId2;
                        relPfamPairDict.Add(relSeqId, pfamPair);
                    }                   
                }
            }
            return relPfamPairDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUnps"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetUniprotPfamsDict(string[] complexUniprots)
        {
            Dictionary<string, string[]> unpPfamsDict = new Dictionary<string, string[]>();
            foreach (string unp in complexUniprots)
            {
                string[] pfams = GetUniprotPfams(unp);
                unpPfamsDict.Add(unp, pfams);
            }
            return unpPfamsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <returns></returns>
        private string[] GetUniprotPfams (string unpId)
        {
            string queryString = "";
            DataTable pfamIdTable = null;
            if (unpId.IndexOf("_HUMAN") > -1)
            {
                queryString = string.Format("Select Distinct Pfam_ID From HumanPfam Where UnpCode = '{0}';", unpId);
                pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            else
            {
                queryString = string.Format("Select Distinct Pfam_ID From UnpPfam Where UnpCode = '{0}';", unpId);
                pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            List<string> pfamIdList = new List<string>();

            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamIdList.Add(pfamRow["Pfam_ID"].ToString().TrimEnd());
            }
            pfamIdList.Sort();
            return pfamIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetRelSeqId (string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') " + 
                " Or (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }
        #endregion

        #region parse complexes in pdb
        public void ParsePossibleTrimerComplexes()
        {
            List<string> unpPairList = null;
            string trimersInPdbFile = Path.Combine(complexDataDir, "PossibleTrimersInPdb_excludedPfams.txt");
            StreamWriter trimerWriter = new StreamWriter(trimersInPdbFile);
     //       Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteins(out unpPairList);
            Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteinsNoExcludedPfams (out unpPairList);
            
            foreach (string unp in unpInteractingUnpListDict.Keys)
            {
                unpInteractingUnpListDict[unp].Sort();
            }
            List<string> unpList = new List<string>(unpInteractingUnpListDict.Keys);
            unpList.Sort();
            List<string> visitedUniProtGroupList = new List<string>();
            string unpGroupString = "";
            foreach (string startUnp in unpList)
            {
                List<string> interactorList1 = unpInteractingUnpListDict[startUnp];
                foreach (string interactor1 in interactorList1)
                {
                    List<string> interactorList2 = unpInteractingUnpListDict[interactor1];
                    foreach (string interactor2 in interactorList2)
                    {
                        if (interactor2 == startUnp)
                        {
                            continue;
                        }
                        if (unpInteractingUnpListDict[interactor2].BinarySearch(startUnp) >= 0)
                        {
                            continue;
                        }

                        string[] uniprots = new string[3];
                        uniprots[0] = startUnp;
                        uniprots[1] = interactor1;
                        uniprots[2] = interactor2;
                        Array.Sort(uniprots);
                        unpGroupString = FormatUniProts(uniprots);
                        if (visitedUniProtGroupList.Contains(unpGroupString))
                        {
                            continue;
                        }
                        visitedUniProtGroupList.Add(unpGroupString);
                        string[] commonEntries = GetCommonEntriesContainingUniProts(uniprots);
                        if (commonEntries.Length > 0)
                        {
                            continue;
                        }
                        trimerWriter.WriteLine(startUnp + " " + interactor1 + " " + interactor2);
                    }
                }
            }
            trimerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ParsePossibleTetramerComplexes()
        {
            List<string> unpPairList = null;
            string tetramersInPdbFile = Path.Combine(complexDataDir, "PossibleTetramersInPdb_excludedPfams.txt");
            StreamWriter tetramerWriter = new StreamWriter(tetramersInPdbFile);
  //          Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteins(out unpPairList);
            Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteinsNoExcludedPfams(out unpPairList);
            foreach (string unp in unpInteractingUnpListDict.Keys)
            {
                unpInteractingUnpListDict[unp].Sort();
            }
            List<string> unpList = new List<string>(unpInteractingUnpListDict.Keys);
            unpList.Sort();
            List<string> visitedUniProtGroupList = new List<string>();
            string unpGroupString = "";
            foreach (string startUnp in unpList)
            {
                List<string> interactorList1 = unpInteractingUnpListDict[startUnp];
                foreach (string interactor1 in interactorList1)
                {
                    List<string> interactorList2 = unpInteractingUnpListDict[interactor1];
                    foreach (string interactor2 in interactorList2)
                    {
                        if (interactor2 == startUnp)
                        {
                            continue;
                        }
                        if (unpInteractingUnpListDict[interactor2].BinarySearch(startUnp) >= 0)
                        {
                            continue;
                        }
                        List<string> interactorList3 = unpInteractingUnpListDict[interactor2];
                        foreach (string interactor3 in interactorList3)
                        {
                            if (interactor3 == startUnp || interactor3 == interactor1)
                            {
                                continue;
                            }
                            if (unpInteractingUnpListDict[interactor3].BinarySearch(startUnp) >= 0 ||
                                unpInteractingUnpListDict[interactor3].BinarySearch(interactor1) >= 0)
                            {
                                continue;
                            }
                            string[] uniprots = new string[4];
                            uniprots[0] = startUnp;
                            uniprots[1] = interactor1;
                            uniprots[2] = interactor2;
                            uniprots[3] = interactor3;
                            Array.Sort(uniprots);
                            unpGroupString = FormatUniProts(uniprots);
                            if (visitedUniProtGroupList.Contains(unpGroupString))
                            {
                                continue;
                            }
                            visitedUniProtGroupList.Add(unpGroupString);
                            string[] commonEntries = GetCommonEntriesContainingUniProts(uniprots);
                            if (commonEntries.Length > 0)
                            {
                                continue;
                            }
                            tetramerWriter.WriteLine(startUnp + " " + interactor1 + " " + interactor2 + " " + interactor3);
                        }
                    }
                }
            }
            tetramerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ParsePossiblePentamerComplexes()
        {
            List<string> unpPairList = null;
            string pentamersInPdbFile = Path.Combine(complexDataDir, "PossiblePentamersInPdb_excludedPfams.txt");
            StreamWriter pentamerWriter = new StreamWriter(pentamersInPdbFile);
    //        Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteins(out unpPairList);
            Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteinsNoExcludedPfams(out unpPairList);
            foreach (string unp in unpInteractingUnpListDict.Keys)
            {
                unpInteractingUnpListDict[unp].Sort();
            }
            List<string> unpList = new List<string>(unpInteractingUnpListDict.Keys);
            unpList.Sort();
            List<string> visitedUniProtGroupList = new List<string>();
            string unpGroupString = "";
            foreach (string startUnp in unpList)
            {
                List<string> interactorList1 = unpInteractingUnpListDict[startUnp];
                foreach (string interactor1 in interactorList1)
                {
                    List<string> interactorList2 = unpInteractingUnpListDict[interactor1];
                    foreach (string interactor2 in interactorList2)
                    {
                        if (interactor2 == startUnp)
                        {
                            continue;
                        }
                        if (unpInteractingUnpListDict[interactor2].BinarySearch(startUnp) >= 0)
                        {
                            continue;
                        }
                        List<string> interactorList3 = unpInteractingUnpListDict[interactor2];
                        foreach (string interactor3 in interactorList3)
                        {
                            if (interactor3 == startUnp || interactor3 == interactor1)
                            {
                                continue;
                            }
                            if (unpInteractingUnpListDict[interactor3].BinarySearch(startUnp) >= 0 ||
                                unpInteractingUnpListDict[interactor3].BinarySearch(interactor1) >= 0)
                            {
                                continue;
                            }
                            List<string> interactorList4 = unpInteractingUnpListDict[interactor3];
                            foreach (string interactor4 in interactorList4)
                            {
                                if (interactor4 == startUnp || interactor4 == interactor1 || interactor4 == interactor2)
                                {
                                    continue;
                                }
                                if (unpInteractingUnpListDict[interactor4].BinarySearch(startUnp) >= 0 ||
                                    unpInteractingUnpListDict[interactor4].BinarySearch(interactor1) >= 0 ||
                                    unpInteractingUnpListDict[interactor4].BinarySearch(interactor2) >= 0)
                                {
                                    continue;
                                }

                                string[] uniprots = new string[5];
                                uniprots[0] = startUnp;
                                uniprots[1] = interactor1;
                                uniprots[2] = interactor2;
                                uniprots[3] = interactor3;
                                uniprots[4] = interactor4;
                                Array.Sort(uniprots);
                                unpGroupString = FormatUniProts(uniprots);
                                if (visitedUniProtGroupList.Contains(unpGroupString))
                                {
                                    continue;
                                }
                                visitedUniProtGroupList.Add(unpGroupString);
                                string[] commonEntries = GetCommonEntriesContainingUniProts(uniprots);
                                if (commonEntries.Length > 0)
                                {
                                    continue;
                                }
                                pentamerWriter.WriteLine(startUnp + " " + interactor1 + " " + interactor2 + " " + interactor3 + " " + interactor4);
                            }
                        }
                    }
                }
            }
            pentamerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string FormatUniProts(string[] uniprots)
        {
            string uniprotString = "";
            foreach (string unp in uniprots)
            {
                uniprotString += (unp + " ");
            }
            return uniprotString.TrimEnd();
        }
        /// <summary>
        /// this is much faster
        /// </summary>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string[] GetCommonEntriesContainingUniProts(string[] uniprots)
        {
            string queryString = string.Format("Select PdbID, Count(Distinct DbCode) As unpCount From PdbDbRefSifts Where DbCode IN ({0}) Group By PdbID;", ParseHelper.FormatSqlListString(uniprots));
            DataTable entryUnpCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> comEntryList = new List<string>();
            int numUnps = 0;
            foreach (DataRow unpCountRow in entryUnpCountTable.Rows)
            {
                numUnps = Convert.ToInt32(unpCountRow["UnpCount"].ToString());
                if (numUnps == uniprots.Length)
                {
                    comEntryList.Add(unpCountRow["PdbID"].ToString());
                }
            }
            queryString = string.Format("Select PdbID, Count(Distinct DbCode) As unpCount From PdbDbRefXml Where DbCode IN ({0}) Group By PdbID;", ParseHelper.FormatSqlListString(uniprots));
            DataTable entryUnpCountTableXml = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow unpCountRow in entryUnpCountTableXml.Rows)
            {
                pdbId = unpCountRow["PdbID"].ToString();
                numUnps = Convert.ToInt32(unpCountRow["UnpCount"].ToString());
                if (numUnps == uniprots.Length)
                {
                    if (!comEntryList.Contains(pdbId))
                    {
                        comEntryList.Add(pdbId);
                    }
                }
            }
            return comEntryList.ToArray();
        }
        /// <summary>
        /// this is slower
        /// </summary>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string[] GetCommonEntriesContainingUniProts1(string[] uniprots)
        {
            List<string> comEntryList = null;
            List<string> entryList = null;
            foreach (string unp in uniprots)
            {
                string[] unpEntries = GetUnpEntries(unp);
                if (comEntryList == null)
                {
                    comEntryList = new List<string>(unpEntries);
                }
                else
                {
                    entryList = new List<string>(comEntryList);
                    comEntryList.Clear();
                    foreach (string pdbId in unpEntries)
                    {
                        if (entryList.Contains(pdbId))
                        {
                            comEntryList.Add(pdbId);
                        }
                    }
                }
            }
            return comEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        public string[] GetUnpEntries(string unpCode)
        {
            List<string> unpEntryList = new List<string>();
            string queryString = string.Format("Select Distinct PdbID From PdbDbRefSifts Where DbCode = '{0}';", unpCode);
            DataTable unpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpEntryTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct PdbID From PdbDbRefXml Where DbCode = '{0}';", unpCode);
                unpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            foreach (DataRow entryRow in unpEntryTable.Rows)
            {
                unpEntryList.Add(entryRow["PdbID"].ToString());
            }
            return unpEntryList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetLinkedProteins(out List<string> unpPairList)
        {
            string queryString = "Select Distinct UnpId1, UnpId2 From UnpPdbDomainInterfaces " +
                " Where UnpId1 like '%_HUMAN' AND UnpId2 Like '%_HUMAN' AND UnpId1 <> UnpId2 Order By UnpId1, UnpId2;";
            //        string queryString = "Select Distinct UnpId1, UnpId2 From UnpPdbDomainInterfaces Where UnpId1 <> UnpId2;";           
            DataTable unpPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpInteractingUnpListDict = new Dictionary<string, List<string>>();
            string unpId1 = "";
            string unpId2 = "";
            unpPairList = new List<string>();
            foreach (DataRow unpPairRow in unpPairTable.Rows)
            {
                unpId1 = unpPairRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpPairRow["UnpID2"].ToString().TrimEnd();
                if (unpInteractingUnpListDict.ContainsKey(unpId1))
                {
                    if (!unpInteractingUnpListDict[unpId1].Contains(unpId2))
                    {
                        unpInteractingUnpListDict[unpId1].Add(unpId2);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpId2);
                    unpInteractingUnpListDict.Add(unpId1, unpList);
                }

                if (unpInteractingUnpListDict.ContainsKey(unpId2))
                {
                    if (!unpInteractingUnpListDict[unpId2].Contains(unpId1))
                    {
                        unpInteractingUnpListDict[unpId2].Add(unpId1);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpId1);
                    unpInteractingUnpListDict.Add(unpId2, unpList);
                }
                if (string.Compare(unpId1, unpId2) > 0)
                {
                    unpPairList.Add(unpId2 + " " + unpId1);
                }
                else
                {
                    unpPairList.Add(unpId1 + " " + unpId2);
                }
            }
            unpPairList.Sort();
            return unpInteractingUnpListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetLinkedProteinsNoExcludedPfams(out List<string> unpPairList)
        {
            string queryString = "Select Distinct UnpId1, UnpId2 From UnpPdbDomainInterfaces " +
                " Where UnpId1 like '%_HUMAN' AND UnpId2 Like '%_HUMAN' AND UnpId1 <> UnpId2 Order By UnpId1, UnpId2;";
            //        string queryString = "Select Distinct UnpId1, UnpId2 From UnpPdbDomainInterfaces Where UnpId1 <> UnpId2;";           
            DataTable unpPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpInteractingUnpListDict = new Dictionary<string, List<string>>();
            string unpId1 = "";
            string unpId2 = "";
            string[] unpPair = new string[2];
            unpPairList = new List<string>();
            foreach (DataRow unpPairRow in unpPairTable.Rows)
            {
                unpId1 = unpPairRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpPairRow["UnpID2"].ToString().TrimEnd();
                unpPair[0] = unpId1;
                unpPair[1] = unpId2;
                if (IsExcludedComplex(unpPair))
                {
                    continue;
                }
                if (unpInteractingUnpListDict.ContainsKey(unpId1))
                {
                    if (!unpInteractingUnpListDict[unpId1].Contains(unpId2))
                    {
                        unpInteractingUnpListDict[unpId1].Add(unpId2);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpId2);
                    unpInteractingUnpListDict.Add(unpId1, unpList);
                }

                if (unpInteractingUnpListDict.ContainsKey(unpId2))
                {
                    if (!unpInteractingUnpListDict[unpId2].Contains(unpId1))
                    {
                        unpInteractingUnpListDict[unpId2].Add(unpId1);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpId1);
                    unpInteractingUnpListDict.Add(unpId2, unpList);
                }
                if (string.Compare(unpId1, unpId2) > 0)
                {
                    unpPairList.Add(unpId2 + " " + unpId1);
                }
                else
                {
                    unpPairList.Add(unpId1 + " " + unpId2);
                }
            }
            unpPairList.Sort();
            return unpInteractingUnpListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private bool IsExcludedPfamRelation(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable relPfamsTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (relPfamsTable.Rows.Count > 0)
            {
                string pfam1 = relPfamsTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                if (excludedPfams.Contains(pfam1))
                {
                    return true;
                }
                string pfam2 = relPfamsTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                if (excludedPfams.Contains(pfam2))
                {
                    return true;
                }
            }
            return false;
        }


        #endregion

        #region linked proteins
        /// <summary>
        /// 
        /// </summary>
        public void PrintLinkedHumanProteins()
        {
            List<string> unpPairList = null;
            //          Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteins(out unpPairList);
            Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteinsNoExcludedPfams(out unpPairList);
            List<List<string>> linkedProteinsList = GetLinkedHumanProteins(unpInteractingUnpListDict);
            WriteLinkedProteinListToFiles(linkedProteinsList);
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintHubLinkedHumanProteins()
        {
            List<string> unpPairList = null;
            //          Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteins(out unpPairList);
            Dictionary<string, List<string>> unpInteractingUnpListDict = GetLinkedProteinsNoExcludedPfams(out unpPairList);

            List<string> hubUnpList = GetHubProteins();
            List<List<string>> linkedProteinsList = GetLinkedHumanProteins(unpInteractingUnpListDict, hubUnpList.ToArray());
            WriteLinkedProteinListToFiles(linkedProteinsList);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInteractingUnpListDict"></param>
        private void WriteLinkedProteinListToFiles(List<List<string>> linkedProteinsList)
        {
           string linkedProteinsSumFile = Path.Combine(complexDataDir, "LinkedHumanProteinsWithStructuresSumInfo.txt");  
           StreamWriter sumWriter = new StreamWriter(linkedProteinsSumFile);
            string interactingHumanProtFile = Path.Combine(complexDataDir, "LinkedHumanProteinsWithStructures.txt");
            StreamWriter dataWriter = null;

            Dictionary<string, List<string>> entryUnpListDict = null;
            string sumLine = "";
            sumWriter.WriteLine("UniProt\t#LinkedProteins\t#LinkedInteractions\t#InteractionEntries\t#Entries\t#EntryUniProts\t#MergedEntryUniProts\tLinkedProteins");
            foreach (List<string> linkedProteins in linkedProteinsList)
            {                
                interactingHumanProtFile = Path.Combine(linkedProteinFileDir, linkedProteins[0] + "_interactors.txt");
                dataWriter = new StreamWriter(interactingHumanProtFile);
                dataWriter.WriteLine(linkedProteins[0] + " " + linkedProteins.Count);
                dataWriter.WriteLine(FormatArrayString(linkedProteins));
                string[] linkedInteractions = GetInteractionsOfLinkedProteins(linkedProteins.ToArray());
                dataWriter.WriteLine();
                dataWriter.WriteLine("Interactors: " + linkedInteractions.Length);
                foreach (string interaction in linkedInteractions)
                {
                    dataWriter.WriteLine(interaction);
                }
                string[] interactionEntryList = GetEntryListOfUnpPair(linkedProteins.ToArray(), out entryUnpListDict);
                dataWriter.WriteLine();
                dataWriter.WriteLine("Interactions and PDBs");
                foreach (string entry in interactionEntryList)
                {
                    dataWriter.WriteLine(entry);
                }
                List<string> entryList = new List<string>(entryUnpListDict.Keys);
                entryList.Sort();
                dataWriter.WriteLine();
                dataWriter.WriteLine("PDBs and Interactors");
                foreach (string pdbId in entryList)
                {
                    entryUnpListDict[pdbId].Sort();
                    dataWriter.WriteLine(pdbId + " " + FormatArrayString(entryUnpListDict[pdbId]));
                }
                Dictionary<string, List<string>> entryUnpsEntryListDict = null;
                string[] entryUnpsList = GetEntryUnpEntryListDict(entryUnpListDict, out entryUnpsEntryListDict);
                dataWriter.WriteLine();
                foreach (string entryUnps in entryUnpsList)
                {
                    dataWriter.WriteLine(entryUnps);
                }

                string[] mergedEntryUnpsList = MergeEntryUnpsListDict(entryUnpsEntryListDict);
                dataWriter.WriteLine();
                foreach (string entryUnps in mergedEntryUnpsList)
                {
                    dataWriter.WriteLine(entryUnps + " " + FormatArrayString(entryUnpsEntryListDict[entryUnps]));
                }               
                dataWriter.Close();

                sumLine = linkedProteins[0] + "\t" + linkedProteins.Count + "\t" + linkedInteractions.Length + "\t" + interactionEntryList.Length + "\t" +
                    entryList.Count + "\t" + entryUnpsList.Length + "\t" + mergedEntryUnpsList.Length + "\t" + FormatArrayString(linkedProteins);
                sumWriter.WriteLine(sumLine);
                sumWriter.Flush();
            }
            sumWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInteractingUnpListDict"></param>
        /// <returns></returns>
        public List<List<string>> GetLinkedHumanProteins(Dictionary<string, List<string>> unpInteractingUnpListDict)
        {
            List<string> visited = new List<string>();
            List<List<string>> connectedProteinsList = new List<List<string>>();
            foreach (string unp in unpInteractingUnpListDict.Keys)
            {
                if (!visited.Contains(unp))
                {
                    List<string> linkedProteins = GetLinkedHumanProteins(unpInteractingUnpListDict, unp);
                    visited.AddRange(linkedProteins);
                    connectedProteinsList.Add(linkedProteins);
                }
            }
            return connectedProteinsList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInteractingUnpListDict"></param>
        /// <returns></returns>
        public List<List<string>> GetLinkedHumanProteins(Dictionary<string, List<string>> unpInteractingUnpListDict, string[] hubUniprots)
        {
            List<string> visited = new List<string>();
            List<List<string>> connectedProteinsList = new List<List<string>>();
            foreach (string unp in hubUniprots)
            {
                if (!visited.Contains(unp))
                {
                    List<string> linkedProteins = GetLinkedHumanProteins(unpInteractingUnpListDict, unp);
                    visited.AddRange(linkedProteins);
                    connectedProteinsList.Add(linkedProteins);
                }
            }
            return connectedProteinsList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="graph"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public List<string> GetLinkedHumanProteins(Dictionary<string, List<string>> unpInteractingUnpListDict, string start)
        {
            List<string> connectedComponent = new List<string>();

            FindConnectedComponent(unpInteractingUnpListDict, start, connectedComponent);

            return connectedComponent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInteractingUnpListDict"></param>
        /// <param name="vertex"></param>
        /// <param name="visited"></param>
        public void FindConnectedComponent(Dictionary<string, List<string>> unpInteractingUnpListDict, string vertex, List<string> visited)
        {
            if (visited.Contains(vertex))
            {
                return;
            }
            visited.Add(vertex);
            foreach (string neighbor in unpInteractingUnpListDict[vertex])
            {
                if (visited.Contains(neighbor))
                {
                    continue;
                }
                FindConnectedComponent(unpInteractingUnpListDict, neighbor, visited);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private List<string> GetHubProteins()
        {
            string queryString = "Select UnpId1 As HubUnp From (Select UnpId1, Count(Distinct UnpId2) As UnpCount From UnpPdbDomainInterfaces WHere UnpId1 like '%_HUMAN' AND UnpId2 Like '%_HUMAN' AND UnpId1 <> UnpId2 Group By UnpID1) " +
                " Where UnpCount >= 2 Order By UnpCount;";
            DataTable hubUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> hubUnpList = new List<string>();
            foreach (DataRow unpRow in hubUnpTable.Rows)
            {
                hubUnpList.Add(unpRow["HubUnp"].ToString().TrimEnd());
            }
            queryString = "Select UnpId2 As HubUnp From (Select UnpId2, Count(Distinct UnpId1) As UnpCount From UnpPdbDomainInterfaces WHere UnpId1 like '%_HUMAN' AND UnpId2 Like '%_HUMAN' AND UnpId1 <> UnpId2 Group By UnpID2) " +
               " Where UnpCount >= 2 Order By UnpCount;";
            hubUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow unpRow in hubUnpTable.Rows)
            {
                if (!hubUnpList.Contains(unpRow["HubUnp"].ToString().TrimEnd()))
                {
                    hubUnpList.Add(unpRow["HubUnp"].ToString().TrimEnd());
                }
            }
            return hubUnpList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="linkedProteins"></param>
        /// <returns></returns>
        private string[] GetInteractionsOfLinkedProteins(string[] linkedProteins)
        {
            string queryString = string.Format("Select Distinct UnpID1, UnpID2 From UnpPdbDomainInterfaces Where UnpID1 IN ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2 Order By UnpID1;", ParseHelper.FormatSqlListString(linkedProteins));
            DataTable unpPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpLinkedUnpListDict = new Dictionary<string, List<string>>();
            string unpId1 = "";
            string unpId2 = "";
            foreach (DataRow unpPairRow in unpPairTable.Rows)
            {
                unpId1 = unpPairRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpPairRow["UnpID2"].ToString().TrimEnd();
                if (unpLinkedUnpListDict.ContainsKey(unpId1))
                {
                    unpLinkedUnpListDict[unpId1].Add(unpId2);
                }
                else
                {
                    List<string> linkedUnpList = new List<string>();
                    linkedUnpList.Add(unpId2);
                    unpLinkedUnpListDict.Add(unpId1, linkedUnpList);
                }
            }
            List<string> unpInteractionList = new List<string>();
            foreach (string unpId in linkedProteins)
            {
                if (unpLinkedUnpListDict.ContainsKey(unpId))
                {
                    unpLinkedUnpListDict[unpId].Sort();
                    unpInteractionList.Add(unpId + ": " + FormatArrayString(unpLinkedUnpListDict[unpId]));
                }
            }
            return unpInteractionList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpIds2"></param>
        /// <returns></returns>
        private string[] GetEntryListOfUnpPair(string[] unpIds, out Dictionary<string, List<string>> entryUnpListDict)
        {
            string queryString = string.Format("Select Distinct UnpId1, UnpId2, PdbID From UnpPdbDomainInterfaces Where UnpId1 IN ({0}) AND UnpID2 In ({0}) AND UnpID1 <> UnpID2;", ParseHelper.FormatSqlListString(unpIds));
            DataTable unpPairEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string dataLine = "";
            List<string> unpPairLineList = new List<string>();
            bool entryAdded = false;
            Array.Sort(unpIds);
            for (int i = 0; i < unpIds.Length; i++)
            {
                for (int j = i + 1; j < unpIds.Length; j++)
                {
                    entryAdded = false;
                    DataRow[] entryRows = unpPairEntryTable.Select(string.Format("UnpID1 = '{0}' AND UnpID2 = '{1}'", unpIds[i], unpIds[j]));
                    dataLine = unpIds[i] + "," + unpIds[j] + ":";
                    foreach (DataRow entryRow in entryRows)
                    {
                        dataLine += (entryRow["PdbID"].ToString() + ",");
                        entryAdded = true;
                    }

                    entryRows = unpPairEntryTable.Select(string.Format("UnpID1 = '{0}' AND UnpID2 = '{1}'", unpIds[j], unpIds[i]));
                    foreach (DataRow entryRow in entryRows)
                    {
                        dataLine += (entryRow["PdbID"].ToString() + ",");
                        entryAdded = true;
                    }
                    if (entryAdded)
                    {
                        unpPairLineList.Add(dataLine);
                    }
                }
            }
            string pdbId = "";
            string unpId1 = "";
            string unpId2 = "";
            entryUnpListDict = new Dictionary<string, List<string>>();
            foreach (DataRow entryRow in unpPairEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                unpId1 = entryRow["UnpID1"].ToString().TrimEnd();
                unpId2 = entryRow["UnpID2"].ToString().TrimEnd();
                if (entryUnpListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpListDict[pdbId].Contains(unpId1))
                    {
                        entryUnpListDict[pdbId].Add(unpId1);
                    }
                    if (!entryUnpListDict[pdbId].Contains(unpId2))
                    {
                        entryUnpListDict[pdbId].Add(unpId2);
                    }
                }
                else
                {
                    List<string> unpList = new List<string>();
                    unpList.Add(unpId1);
                    unpList.Add(unpId2);
                    entryUnpListDict.Add(pdbId, unpList);
                }
            }
            return unpPairLineList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryUnpListDict"></param>
        /// <returns></returns>
        private string[] GetEntryUnpEntryListDict(Dictionary<string, List<string>> entryUnpListDict, out Dictionary<string, List<string>> entryUnpsEntryListDict)
        {
            string unpListString = "";
            entryUnpsEntryListDict = new Dictionary<string, List<string>>();
            foreach (string pdbId in entryUnpListDict.Keys)
            {
                entryUnpListDict[pdbId].Sort();
                unpListString = FormatArrayString(entryUnpListDict[pdbId].ToArray());
                if (entryUnpsEntryListDict.ContainsKey(unpListString))
                {
                    entryUnpsEntryListDict[unpListString].Add(pdbId);
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    entryUnpsEntryListDict.Add(unpListString, entryList);
                }
            }
            List<string> unpPairList = new List<string>(entryUnpsEntryListDict.Keys);
            unpPairList.Sort();
            List<string> entryUnpEntryList = new List<string>();
            foreach (string unpPairStr in unpPairList)
            {
                entryUnpsEntryListDict[unpPairStr].Sort();
                entryUnpEntryList.Add(unpPairStr + " " + FormatArrayString(entryUnpsEntryListDict[unpPairStr]));
            }
            return entryUnpEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryUnpsEntryListDict"></param>
        /// <returns></returns>
        private string[] MergeEntryUnpsListDict(Dictionary<string, List<string>> entryUnpsEntryListDict)
        {
            List<string> entryUnpsList = new List<string>(entryUnpsEntryListDict.Keys);
            List<string> leftEntryUnpsList = new List<string>(entryUnpsList);
            for (int i = 0; i < entryUnpsList.Count; i++)
            {
                if (!leftEntryUnpsList.Contains(entryUnpsList[i]))
                {
                    continue;
                }
                string[] unpFieldsI = entryUnpsList[i].Split(',');
                for (int j = i + 1; j < entryUnpsList.Count; j++)
                {
                    if (!leftEntryUnpsList.Contains(entryUnpsList[j]))
                    {
                        continue;
                    }
                    string[] unpFieldsJ = entryUnpsList[j].Split(',');
                    if (AreUnpListsContained(unpFieldsI, unpFieldsJ))
                    {
                        if (unpFieldsJ.Length > unpFieldsI.Length)
                        {
                            leftEntryUnpsList.Remove(entryUnpsList[i]);
                        }
                        else
                        {
                            leftEntryUnpsList.Remove(entryUnpsList[j]);
                        }
                    }
                }
            }
            return leftEntryUnpsList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpList1"></param>
        /// <param name="unpList2"></param>
        /// <returns></returns>
        private bool AreUnpListsContained(string[] unpList1, string[] unpList2)
        {
            if (unpList1.Length > unpList2.Length)
            {
                foreach (string unp in unpList2)
                {
                    if (!unpList1.Contains(unp))
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (string unp in unpList1)
                {
                    if (!unpList2.Contains(unp))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interactingHumanProtFile"></param>
        /// <returns></returns>
        private string[] ReadFromHumanProtFile(string interactingHumanProtFile)
        {
            StreamReader dataReader = new StreamReader(interactingHumanProtFile);
            string line = "";
            List<string> humanProtList = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');
                humanProtList.AddRange(fields);
            }
            dataReader.Close();
            return humanProtList.ToArray();
        }
        #endregion

        #region read interesting linked proteins
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<string[]> ReadInterestingLinkedProteins(int numLinkedProteins)
        {
            // 1B15_HUMAN_interactors.txt
            List<string[]> linkedProteinsList = new List<string[]>();
            string[] linkedProteinFiles = Directory.GetFiles (linkedProteinFileDir);
            string[] linkedProteins = null;
            foreach (string proteinFile in linkedProteinFiles)
            {
                if (IsPossibleLinkedComplex (proteinFile, numLinkedProteins, out linkedProteins))
                {
                    linkedProteinsList.Add(linkedProteins);
                }
            }
            return linkedProteinsList;
        }

        /// <summary>
        /// no entries contain all uniprots
        /// so it is a possible linked complex
        /// </summary>
        /// <param name="lnProteinFile"></param>
        /// <returns></returns>
        private bool IsPossibleLinkedComplex (string lnProteinFile, int numInteractorCutoff, out string[] linkedProteins)
        {
            bool isPossibleSample = true;
            StreamReader dataReader = new StreamReader(lnProteinFile);
            string line = dataReader.ReadLine();
            string[] headerFields = line.Split();
            line = dataReader.ReadLine();
            linkedProteins = line.Split(',');
            int numOfInteractors = Convert.ToInt32(headerFields[1]);
            if (numOfInteractors != numInteractorCutoff)
            {
                dataReader.Close();
                return false;
            }
            
            bool entryUniprotsStart = false;
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line == "PDBs and Interactors")
                {
                    entryUniprotsStart = true;
                    continue;
                }
                if (entryUniprotsStart && line == "")
                {
                    entryUniprotsStart = false;
                }
                if (entryUniprotsStart)
                {
                    string[] fields = line.Split();
                    string[] uniprots = fields[1].Split(',');
                    if (uniprots.Length == numInteractorCutoff)
                    {
                        isPossibleSample = false;
                        break;
                    }
                }
            }
            dataReader.Close();
            return isPossibleSample;
        }
        #endregion

        #region add PDB structures to uniprot complexes
        //       private string[] antibodyPfams = {"C1-set", "V-set", "C2-set", "C2-set_2", "V-set_CD47"};
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetExcludedPfams()
        {
            string queryString = "Select Pfam_ID From PfamHmm Where Pfam_ID Like 'Ribosomal%';";
            DataTable ribosomalTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> excludedPfamList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in ribosomalTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                excludedPfamList.Add(pfamId);
            }

            queryString = "Select Pfam_ID From PfamHmm Where Pfam_ID Like 'C%-set%';";
            DataTable csetTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow pfamRow in csetTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                excludedPfamList.Add(pfamId);
            }

            queryString = "Select Pfam_ID From PfamHmm Where Pfam_ID Like 'V%-set%';";
            DataTable vsetTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow pfamRow in vsetTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                excludedPfamList.Add(pfamId);
            }
            excludedPfamList.Sort();
            return excludedPfamList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        public void AddPdbStructuresToComplexes()
        {
            string complexLogFile = Path.Combine(complexDataDir, "ComplexesLog.txt");
            StreamWriter logWriter = new StreamWriter(complexLogFile, true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString());
            string complexListFile = Path.Combine(complexDataDir, "PossibleTrimersInPdb.txt");
            string pdbComplexListFile = Path.Combine(complexDataDir, "PossibleTrimersAndPdb.txt");
            StreamWriter complexStructWriter = new StreamWriter(pdbComplexListFile);
            List<string> complexList = new List<string>();
            StreamReader complexReader = new StreamReader(complexListFile);
            string line = "";
            string unpString = "";
            while ((line = complexReader.ReadLine()) != null)
            {
                string[] complexUnps = line.Split();
                Array.Sort(complexUnps);
                unpString = FormatUniProts(complexUnps);
                if (complexList.Contains(unpString))
                {
                    continue;
                }
                if (IsExcludedComplex(complexUnps))
                {
                    logWriter.WriteLine(line + " antibody complex");
                    continue;
                }
                complexStructWriter.WriteLine("#" + line);
                RetrieveComplexStructures(complexUnps, complexStructWriter);
            }
            complexReader.Close();
            logWriter.Close();
            complexStructWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private bool IsExcludedComplex(string[] uniprots)
        {
            string queryString = string.Format("Select Distinct FamilyCode1, FamilyCode2 " +
                " From UnpPdbDomainInterfaces, PfamDomainFamilyRelation " +
                " Where UnpID1 IN ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2 AND UnpPdbDomainInterfaces.RelSeqID = PfamDomainFamilyRelation.RelSeqID;",
                ParseHelper.FormatSqlListString(uniprots));
            DataTable unpPfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            foreach (DataRow pfamPairRow in unpPfamPairTable.Rows)
            {
                pfamId = pfamPairRow["FamilyCode1"].ToString().TrimEnd();
                if (excludedPfams.Contains(pfamId))
                {
                    return true;
                }
                pfamId = pfamPairRow["FamilyCode2"].ToString().TrimEnd();
                if (excludedPfams.Contains(pfamId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUnps"></param>
        private void RetrieveComplexStructures(string[] complexUnps, StreamWriter structInfoWriter)
        {
            Dictionary<string, Dictionary<string, List<string>>>[] unpPairStructInfoDicts = RetrieveComplexStructures(complexUnps);
            Dictionary<string, Dictionary<string, List<string>>> unpPairPfamPairStructInfoDict = unpPairStructInfoDicts[0];
            Dictionary<string, Dictionary<string, List<string>>> unpPairPfamPairStructListDict = unpPairStructInfoDicts[1];

            List<string> keyUnpPairList = new List<string>(unpPairPfamPairStructInfoDict.Keys);
            keyUnpPairList.Sort();
            string structInfoLine = "";
            foreach (string keyUnpPair in keyUnpPairList)
            {
                List<string> pfamPairList = new List<string>(unpPairPfamPairStructInfoDict[keyUnpPair].Keys);
                pfamPairList.Sort();
                foreach (string keyPfamPair in pfamPairList)
                {
                    structInfoLine = keyUnpPair + "\t" + keyPfamPair + "\t" + unpPairPfamPairStructListDict[keyUnpPair][keyPfamPair].Count + "\t";
                    foreach (string structInfo in unpPairPfamPairStructInfoDict[keyUnpPair][keyPfamPair])
                    {
                        structInfoLine += ("(" + structInfo + ");");
                    }
                    structInfoWriter.WriteLine(structInfoLine.TrimEnd(';'));
                }
            }
            structInfoWriter.WriteLine();
            structInfoWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private bool IsCommonUnpPeptideInStructures(string unpCode)
        {
            string queryString = string.Format("Select AsymUnit.PdbID, AsymUnit.EntityID, Sequence From PdbDbRefSifts, AsymUnit " +
                " Where DbCode = '{0}' AND PdbDbRefSifts.PdbID = AsymUnit.PdbID AND PdbDbRefSifts.EntityID = AsymUnit.EntityID;", unpCode);
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            if (entitySeqTable.Rows.Count > 0)
            {
                sequence = entitySeqTable.Rows[0]["Sequence"].ToString();
            }
            if (sequence.Length < pepLengthCutoff)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <param name="unpPdbDomainInterfaceTable"></param>
        /// <returns></returns>
        private bool IsCommonUnpSamePfam(string unpCode, DataTable unpPdbDomainInterfaceTable)
        {
            List<string> unpPfamList = new List<string>();
            DataRow[] unpInterfaceRows = unpPdbDomainInterfaceTable.Select(string.Format("UnpID1 = '{0}'", unpCode));
            string pfamId = "";
            foreach (DataRow interfaceRow in unpInterfaceRows)
            {
                pfamId = interfaceRow["FamilyCode1"].ToString().TrimEnd();
                if (!unpPfamList.Contains(pfamId))
                {
                    unpPfamList.Add(pfamId);
                }
            }

            unpInterfaceRows = unpPdbDomainInterfaceTable.Select(string.Format("UnpID2 = '{0}'", unpCode));
            foreach (DataRow interfaceRow in unpInterfaceRows)
            {
                pfamId = interfaceRow["FamilyCode2"].ToString().TrimEnd();
                if (!unpPfamList.Contains(pfamId))
                {
                    unpPfamList.Add(pfamId);
                }
            }
            if (unpPfamList.Count == 1)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region format complexes from PyMOL output
        string[] chainLetters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        string complexDir = @"X:\Qifang\Paper\protcid_update\data_v31\Hubs and Complexes\complexes\ahnak-anxa2-S100a10_complex";
        public void FormatComplexesFromPyMolOutput()
        {

            string[] complexFiles = { "ahnak-anxa2-S100a10_complex_trimer1.pdb", "ahnak-anxa2-S100a10_complex2_trimer1.pdb", 
                                    "ahnak-anxa2-S100a10_complex2_trimer2.pdb", "ahnak-anxa2-S100a10_complex2_trimer3.pdb"};

            foreach (string complexFile in complexFiles)
            {
                FormatComplex(complexFile);
            }
        }

        public void FormatComplex(string complexFile)
        {
            string oldComplexFile = Path.Combine(complexDir, complexFile);
            string newCompexFile = Path.Combine(complexDir, complexFile.Replace(".pdb", "_pdb.pdb"));
            StreamReader complexReader = new StreamReader(oldComplexFile);
            StreamWriter complexWriter = new StreamWriter(newCompexFile);
            string line = "";
            string preChainId = "";
            string chainId = "";
            string newChainId = "";
            int chainNum = -1;
            string newAtomLine = "";
            while ((line = complexReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.IndexOf("ANISOU") > -1)
                {
                    continue;
                }
                if (line == "END")
                {
                    complexWriter.Write(line + "\n");
                    continue;
                }
                if (line.Substring(0, 6) == "ATOM  ")
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    chainId = atomFields[5];
                    if (chainId != preChainId)
                    {
                        preChainId = chainId;
                        chainNum++;
                        newChainId = chainLetters[chainNum];
                    }
                    newAtomLine = ReplaceChainID(line, newChainId);
                    complexWriter.Write(newAtomLine + "\n");
                }
                else if (line.Substring(0, 6) == "TER   ")
                {
                    complexWriter.Write(line + "\n");
                }
                else
                {
                    complexWriter.Write(line + "\n");
                }
            }
            complexReader.Close();
            complexWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="newChainId"></param>
        /// <returns></returns>
        private string ReplaceChainID(string atomLine, string newChainId)
        {
            char[] atomLineChars = atomLine.ToCharArray();
            atomLineChars[21] = newChainId[0];
            string newAtomLine = new string(atomLineChars);
            return newAtomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="newChainId"></param>
        /// <param name="newResidueNum"></param>
        /// <returns></returns>
        private string ReplaceChainIDAndResidueSeqNum(string atomLine, string newChainId, string newResidueNum)
        {
            char[] atomLineChars = atomLine.ToCharArray();
            atomLineChars[21] = newChainId[0];
            newResidueNum = newResidueNum.PadLeft(4);
            for (int i = 0; i < newResidueNum.Length; i++)
            {
                atomLineChars[22 + i] = newResidueNum[i];
            }
            string newAtomLine = new string(atomLineChars);
            return newAtomLine;
        }


        public void GetEntriesWithMultipleUniProts()
        {
            string queryString = "Select Distinct PdbID From UnpPdbDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

            }
        }
        #endregion

        #region other functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUnps"></param>
        public Dictionary<string, Dictionary<string, List<string>>>[] RetrieveComplexStructures(string[] complexUnps)
        {
            string queryString = string.Format("Select UnpID1, UnpID2, PfamDomainInterfaces.PdbID, PfamDomainInterfaces.DomainInterfaceID, " +
                " InterfaceID, AsymChain1, AsymChain2, " +
                " PfamDomainFamilyRelation.RelSeqID, FamilyCode1, FamilyCode2 " +
                " From UnpPdbDomainInterfaces, PfamDomainInterfaces, PfamDomainFamilyRelation " +
                " Where UnpID1 In ({0}) AND UnpID2 IN ({0}) AND UnpID1 <> UnpID2 AND " +
                " UnpPdbDomainInterfaces.PdbID = PfamDomainInterfaces.PdbID AND UnpPdbDomainInterfaces.DomainInterfaceID = PfamDomainInterfaces.DomainInterfaceID AND " +
                " UnpPdbDomainInterfaces.RelSeqID = PfamDomainFamilyRelation.RelSeqID;", ParseHelper.FormatSqlListString(complexUnps));
            DataTable unpDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            string[] commonUnps = GetCommonUniprots(unpDomainInterfaceTable);
            string unpPair = "";
            string pfamPair = "";
            string pdbId = "";
            string structInfoLine = "";
            Dictionary<string, Dictionary<string, List<string>>> unpPairPfamPairStructListDict = new Dictionary<string, Dictionary<string, List<string>>>();
            Dictionary<string, Dictionary<string, List<string>>> unpPairPfamPairStructInfoDict = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (DataRow interfaceRow in unpDomainInterfaceTable.Rows)
            {
                unpPair = interfaceRow["UnpID1"].ToString().TrimEnd();
                unpPair += " " + interfaceRow["UnpID2"].ToString().TrimEnd();
                pfamPair = interfaceRow["FamilyCode1"].ToString().TrimEnd();
                pfamPair += " " + interfaceRow["FamilyCode2"].ToString().TrimEnd();
                pdbId = interfaceRow["PdbID"].ToString();
                structInfoLine = interfaceRow["PdbID"].ToString() + " " + interfaceRow["InterfaceID"].ToString() + " " +
                                interfaceRow["DomainInterfaceID"] + " " + interfaceRow["AsymChain1"].ToString().TrimEnd() + " " +
                                interfaceRow["AsymChain2"].ToString().TrimEnd();

                if (unpPairPfamPairStructListDict.ContainsKey(unpPair))
                {
                    if (unpPairPfamPairStructListDict[unpPair].ContainsKey(pfamPair))
                    {
                        if (!unpPairPfamPairStructListDict[unpPair][pfamPair].Contains(pdbId))
                        {
                            unpPairPfamPairStructListDict[unpPair][pfamPair].Add(pdbId);

                            unpPairPfamPairStructInfoDict[unpPair][pfamPair].Add(structInfoLine);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        unpPairPfamPairStructListDict[unpPair].Add(pfamPair, entryList);

                        List<string> structInfoList = new List<string>();
                        structInfoList.Add(structInfoLine);
                        unpPairPfamPairStructInfoDict[unpPair].Add(pfamPair, structInfoList);
                    }

                }
                else
                {
                    Dictionary<string, List<string>> pfamPairStructListDict = new Dictionary<string, List<string>>();
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    pfamPairStructListDict.Add(pfamPair, entryList);
                    unpPairPfamPairStructListDict.Add(unpPair, pfamPairStructListDict);

                    Dictionary<string, List<string>> pfamPairStructInfoListDict = new Dictionary<string, List<string>>();
                    List<string> structInfoList = new List<string>();
                    structInfoList.Add(structInfoLine);
                    pfamPairStructInfoListDict.Add(pfamPair, structInfoList);
                    unpPairPfamPairStructInfoDict.Add(unpPair, pfamPairStructInfoListDict);
                }
            }
            Dictionary<string, Dictionary<string, List<string>>>[] unpPairStructInfoDicts = new Dictionary<string, Dictionary<string, List<string>>>[2];
            unpPairStructInfoDicts[0] = unpPairPfamPairStructListDict;
            unpPairStructInfoDicts[1] = unpPairPfamPairStructInfoDict;
            return unpPairStructInfoDicts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPdbDomainInterfaceTable"></param>
        /// <returns></returns>
        public string[] GetCommonUniprots(DataTable unpPdbDomainInterfaceTable)
        {
            List<string[]> unpPairList = new List<string[]>();
            string unpId1 = "";
            string unpId2 = "";
            foreach (DataRow unpInterfaceRow in unpPdbDomainInterfaceTable.Rows)
            {
                unpId1 = unpInterfaceRow["UnpID1"].ToString().TrimEnd();
                unpId2 = unpInterfaceRow["UnpID2"].ToString().TrimEnd();
                string[] unpPair = new string[2];
                unpPair[0] = unpId1;
                unpPair[1] = unpId2;
                if (ContainsUnpPair(unpPairList, unpPair))
                {
                    continue;
                }
                unpPairList.Add(unpPair);
            }
            string commonUnp = "";
            List<string> commonUnpList = new List<string>();
            for (int i = 0; i < unpPairList.Count; i++)
            {
                for (int j = i + 1; j < unpPairList.Count; j++)
                {
                    commonUnp = GetCommonUnp(unpPairList[i], unpPairList[j]);
                    if (commonUnp == "")
                    {
                        continue;
                    }
                    if (!commonUnpList.Contains(commonUnp))
                    {
                        commonUnpList.Add(commonUnp);
                    }
                }
            }
            return commonUnpList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPairList"></param>
        /// <param name="unpPair"></param>
        /// <returns></returns>
        public bool ContainsUnpPair(List<string[]> unpPairList, string[] unpPair)
        {
            foreach (string[] lsUnpPair in unpPairList)
            {
                if (EqualUnpPairs(lsUnpPair, unpPair))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPair1"></param>
        /// <param name="unpPair2"></param>
        /// <returns></returns>
        public string GetCommonUnp (string unpPair1, string unpPair2)
        {
            string[] unpFields1 = unpPair1.Split(';');
            string[] unpFields2 = unpPair2.Split(';');
            string commonUnp = GetCommonUnp(unpFields1, unpFields2);
            return commonUnp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPair1"></param>
        /// <param name="unpPair2"></param>
        /// <returns></returns>
        public string GetCommonUnp(string[] unpPair1, string[] unpPair2)
        {
            if (unpPair1[0] == unpPair2[0] || unpPair1[0] == unpPair2[1])
            {
                return unpPair1[0];
            }
            else if (unpPair1[1] == unpPair2[0] || unpPair1[1] == unpPair2[0])
            {
                return unpPair1[1];
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPair1"></param>
        /// <param name="unpPair2"></param>
        public bool EqualUnpPairs(string[] unpPair1, string[] unpPair2)
        {
            if ((unpPair1[0] == unpPair2[0] && unpPair1[1] == unpPair2[1]) ||
                    (unpPair1[1] == unpPair2[0] && unpPair1[0] == unpPair2[1]))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public int[] GetUnpEntities(string uniprot, string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbDbRefSifts Where PdbID = '{0}' AND DbCode = '{1}';", pdbId, uniprot);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] entities = new int[entityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entities[count] = Convert.ToInt32(entityRow["EntityID"].ToString());
                count++;
            }
            return entities;
        }
        #endregion

        #region gene data for input complexes
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        public void SetGenesOfUniprotsComplexes (string[] uniprots, out Dictionary<string, string> complexUnpCodeDict)
        {
            string complexUnpGeneSeqFile = Path.Combine(complexDataDir, "ComplexGeneSequenceList.txt");
            string unpCode = "";
            complexUnpCodeDict = new Dictionary<string, string>();
            if (File.Exists(complexUnpGeneSeqFile))
            {
                StreamReader dataReader = new StreamReader(complexUnpGeneSeqFile);
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    string[] fields = line.Split('\t');
                    if (!unpGeneListDict.ContainsKey(fields[3]))
                    {
                        unpGeneListDict.Add(fields[3], fields[1]);
                    }
                    complexUnpCodeDict.Add(fields[0], fields[3]);
                    if (geneUnpListDict.ContainsKey(fields[1]))
                    {
                        geneUnpListDict[fields[1]].Add(fields[3]);
                    }
                    else
                    {
                        List<string> unpList = new List<string>();
                        unpList.Add(fields[3]);
                        geneUnpListDict.Add(fields[1], unpList);
                    }
                }
                dataReader.Close();
  //              AddLeftSameGeneUnpsInPdb();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter(complexUnpGeneSeqFile);

                foreach (string uniprot in uniprots)
                {
                    ParseUnpCode(uniprot, out unpCode);
                    string[] geneSeq = GetUniprotGeneAndSequence(unpCode);
                    dataWriter.WriteLine(uniprot + "\t" + geneSeq[0] + "\t" + geneSeq[1] + "\t" + geneSeq[2] + "\t" + unpCode);
                    complexUnpCodeDict.Add(uniprot, geneSeq[2]);
                    if (! unpGeneListDict.ContainsKey(geneSeq[2]))
                    {
                        unpGeneListDict.Add(geneSeq[2], geneSeq[0]);
                    }
                    if (geneUnpListDict.ContainsKey(geneSeq[0]))
                    {
                        geneUnpListDict[geneSeq[0]].Add(geneSeq[2]);
                    }
                    else
                    {
                        List<string> unpList = new List<string>();
                        unpList.Add(geneSeq[2]);
                        geneUnpListDict.Add(geneSeq[0], unpList);
                    }
                }
                dataWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void AddLeftSameGeneUnpsInPdb ()
        {
            string complexUnpGeneSeqFile = Path.Combine(complexDataDir, "ComplexGeneSequenceList.txt");
            StreamWriter dataWriter = new StreamWriter(complexUnpGeneSeqFile, true);
            List<string> geneList = new List<string>(geneUnpListDict.Keys);
            foreach (string gene in geneList)
            {
                try
                {
                    List<string[]> sameGnUnpInfos = GetGeneUniprots(gene);
                    foreach (string[] unpSeq in sameGnUnpInfos)
                    {
                        if (!geneUnpListDict[gene].Contains(unpSeq[0]))
                        {
                            geneUnpListDict[gene].Add(unpSeq[0]);
                            dataWriter.WriteLine(unpSeq[0] + "\t" + gene + "\t" + unpSeq[1] + "\t" + unpSeq[0] + "\t" + unpSeq[2]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(gene + " retrieve uniprots from protcid data error: " + ex.Message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gene"></param>
        /// <returns></returns>
        private List<string[]> GetGeneUniprots (string gene)
        {
            string queryString = string.Format("Select UnpCode, UnpAccession, sequence From HumanSeqInfo Where Upper(GN) = '{0}' AND Isoform = 0;", gene.ToUpper ());
            DataTable humanUnpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = string.Format("Select UnpCode, UnpAccession, sequence  From UnpSeqInfo Where Upper(GN) = '{0}';", gene.ToUpper());
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<string[]> unpInfoList = new List<string[]>();
            string[] unpSequence = null;
            foreach (DataRow unpRow in humanUnpCodeTable.Rows)
            {
                unpSequence = new string[3];
                unpSequence[0] = unpRow["UnpCode"].ToString ().TrimEnd ();
                unpSequence[1] = unpRow["Sequence"].ToString();
                unpSequence[2] = unpRow["UnpAccession"].ToString().TrimEnd ();
                unpInfoList.Add(unpSequence);
            }

            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                unpSequence = new string[3];
                unpSequence[0] = unpRow["UnpCode"].ToString().TrimEnd();
                unpSequence[1] = unpRow["Sequence"].ToString();
                unpSequence[2] = unpRow["UnpAccession"].ToString().TrimEnd();
                unpInfoList.Add(unpSequence);
            }

            return unpInfoList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUnpCodesDict"></param>
        /// <param name="complexUniprotsDict"></param>
        private Dictionary<string, List<string>> UpdateComplexUniprots(Dictionary<string, string> complexUnpCodeMapDict, Dictionary<string, List<string>> complexUniprotsDict)
        {
            Dictionary<string, List<string>> updateComplexUnpListDict = new Dictionary<string, List<string>>();
            foreach (string complex in complexUniprotsDict.Keys)
            {
                List<string> complexUnpList = new List<string>();
                for (int i = 0; i < complexUniprotsDict[complex].Count; i ++)
                {
                    if (!complexUnpList.Contains(complexUnpCodeMapDict[complexUniprotsDict[complex][i]]))
                    {
                        complexUnpList.Add(complexUnpCodeMapDict[complexUniprotsDict[complex][i]]);
                    }
                }
                updateComplexUnpListDict.Add(complex, complexUnpList);
            }
            return updateComplexUnpListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprot"></param>
        /// <param name="unpAcc"></param>
        /// <returns></returns>
        private bool ParseUnpCode (string uniprot, out string unpCode)
        {
            unpCode = uniprot;
            int accIndex = uniprot.IndexOf ("-PRO_");
            if (accIndex > -1)
            {
                unpCode = uniprot.Substring(0, accIndex);
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpcode"></param>
        /// <returns></returns>
        public string[] GetUniprotGeneAndSequence (string unpcode)
        {
            string[] geneSequence = new string[3];
            string queryString = string.Format("Select UnpCode, GN, Sequence From HumanSeqInfo Where UnpCode = '{0}' OR UnpAccession = '{0}';", unpcode);
            DataTable gnTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (gnTable.Rows.Count == 0)
            {
                queryString = string.Format("Select UnpCode, GN, Sequence From UnpSeqInfo Where UnpCode = '{0}' OR UnpAccession = '{0}';", unpcode);
                gnTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }            
            if (gnTable.Rows.Count > 0)
            {
                geneSequence[0] = gnTable.Rows[0]["GN"].ToString().TrimEnd().ToUpper();
                geneSequence[1] = gnTable.Rows[0]["Sequence"].ToString();
                geneSequence[2] = gnTable.Rows[0]["UnpCode"].ToString().TrimEnd();
            }
            else
            {
                string gene = "";
                string unpId = "";
                string webFile = unpWebHttp + unpcode + ".fasta";
                string unpFile = Path.Combine(tempDir, unpcode + ".fasta");
                if (! File.Exists(unpFile))
                {
                    try
                    {
                        fileDownload.DownloadFile(webFile, unpFile);
                    }
                    catch (Exception ex)
                    {
                        geneSequence[0] = unpcode;
                        geneSequence[1] = "";
                        geneSequence[2] = unpcode;
                        return geneSequence;
                    }
                }
                StreamReader dataReader = new StreamReader(unpFile);
                string line = "";
                int gnIndex = -1;
                string sequence = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line.IndexOf(">") > -1)
                    {
                        string[] fields = line.Split();
                        string[] unpFields = fields[0].Split('|');
                        unpId = unpFields[2];
                        foreach (string field in fields)
                        {
                            gnIndex = field.IndexOf("GN=");
                            if (gnIndex > -1)
                            {
                                gene = field.Remove(0, 3);
                            }
                        }
                    }
                    else
                    {
                        sequence += line;
                    }
                }
                dataReader.Close();

                geneSequence[0] = gene.ToUpper ();
                geneSequence[1] = sequence;
                geneSequence[2] = unpId;
            }
            return geneSequence;
        }
        #endregion
    }
}
