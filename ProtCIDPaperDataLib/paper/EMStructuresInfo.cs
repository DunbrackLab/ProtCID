using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Threading.Tasks;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    public class EMStructuresInfo :  ComplexStructures
    {
        #region member variables
        private string emDataDir = "";
        private Dictionary<string, List<string>> parsedUnpComplexEntryListDict = new Dictionary<string, List<string>>();
        private string[] emStructuresPdb = null;

        /// <summary>
        /// 
        /// </summary>
        public EMStructuresInfo ()
        {
            emDataDir = Path.Combine(dataDir, "EMstructures");
            if (!Directory.Exists(emDataDir))
            {
                Directory.CreateDirectory(emDataDir);
            }

            string queryString = "Select PdbID From PdbEntry Where Method like 'ELECTRON %';";
            DataTable EMentryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            emStructuresPdb = new string[EMentryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in EMentryTable.Rows)
            {
                emStructuresPdb[count] = entryRow["PdbID"].ToString();
                count++;
            }
            Array.Sort(emStructuresPdb);
        }
        #endregion

        #region EM complexes and Xray structures
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void AddStructuresPfamsClustersInfo ()
        {
            string emStructuresInterfaceDetailsFile = Path.Combine(emDataDir, "EMstructuresInterfacesStructDetails_all.txt");
            string emStructuresSumFile = Path.Combine(emDataDir, "EMstructuresSumInfo_all.txt");            
            StreamWriter interfaceDataWriter = new StreamWriter(emStructuresInterfaceDetailsFile);
            interfaceDataWriter.WriteLine("RelSeqID\tPfamID1\tPfamID2\tUnpID1\tUnpID2\tPdbID\tDomainInterfaceID\tAsymChain1\tAsymChain2\tBiggestClusterOfEntry\tBiggestCluster");
            StreamWriter sumDataWriter = new StreamWriter(emStructuresSumFile);
            sumDataWriter.WriteLine("PdbID\tResolution\t#HumanUnps\t#Uniprots\tHumanUniprots\tUniProts");
            
            string queryString = "Select PdbID, Resolution From PdbEntry Where Method like 'ELECTRON %';";
            DataTable EMentryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            Dictionary<string, List<string>> entryUnpListDict = null;
            string[][] complexUnpsHumanUnps = null;
            foreach (DataRow entryRow in EMentryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                if (HasEMStructureXtalStructuresOnlyPartialUniProts(pdbId, out entryUnpListDict, out complexUnpsHumanUnps))
                {
                    GetEMStructureUnpPfamInterfaceClusters(pdbId, interfaceDataWriter);
                    sumDataWriter.WriteLine(pdbId + "\t" + entryRow["Resolution"] + "\t" + 
                        complexUnpsHumanUnps[1].Length + "\t" + complexUnpsHumanUnps[0].Length + "\t" +
                        GetComplexUnpString (complexUnpsHumanUnps[1]) + "\t" + GetComplexUnpString(complexUnpsHumanUnps[0]) + "\t" + 
                        FormatXtalStructuresUnps(entryUnpListDict));
                    sumDataWriter.Flush();
                }                
            }
            interfaceDataWriter.Close();
            sumDataWriter.Close();

            string emStructUnpComplexFile = Path.Combine(emDataDir, "EMstructuresUnpComplexes_all.txt");
            StreamWriter unpComplexWriter = new StreamWriter(emStructUnpComplexFile);
            unpComplexWriter.WriteLine("UniProts\tPDB List");
            List<string> unpComplexList = new List<string>(parsedUnpComplexEntryListDict.Keys);
            unpComplexList.Sort ();
            foreach (string complex in unpComplexList)
            {
                parsedUnpComplexEntryListDict[complex].Sort ();
                unpComplexWriter.WriteLine(complex + "\t"  + ParseHelper.FormatStringFieldsToString (parsedUnpComplexEntryListDict[complex].ToArray ()));
            }
            unpComplexWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveEmStructuresData()
        {
            string emComplexListFile = Path.Combine(emDataDir, "EMstructuresUnpComplexes_all.txt");
            Dictionary<string[], string[]> emComplexEntryListDict = ReadEMcomplexEntryListDict(emComplexListFile);         

            string complexPdbStructInfoFile = Path.Combine(emDataDir, "emStructuresXtalStructInfo_unpRange_struct.txt");
            StreamWriter complexStructWriter = new StreamWriter(complexPdbStructInfoFile);
            foreach (string[] complexUniprots in emComplexEntryListDict.Keys)
            {
                try
                {
                    RetrieveComplexRelatedStructures(complexUniprots, emComplexEntryListDict[complexUniprots], emStructuresPdb, complexStructWriter);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(FormatArrayString(complexUniprots, ';') + "\t" + FormatArrayString (emComplexEntryListDict[complexUniprots], ';') + " : " + ex.Message);
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
        public void RetrieveComplexRelatedStructures( string[] complexUniprots, string[] complexEmStructures, string[] pdbEmStructures, StreamWriter structWriter)
        {
            string dataLine = "";
            string[] extendedComplexUnps = null;            
            Dictionary<string, string[]> complexSameGnUnpsDict = GetComplexSameGeneUnpsDict(complexUniprots, out extendedComplexUnps);
            Dictionary<string, string> sameGnComplexUnpDict = ReverseComplexSameGnUniprotsDict(complexSameGnUnpsDict);
            string[] entriesWithAllUniprots = null;
            List<string> unpEntryList = null;
            List<string> entryList = null;
    /*        Dictionary<string, List<string>> entryInteractUnpListDict = GetEntryInteractUnpListDict(extendedComplexUnps);
            unpEntryList = new List<string>(entryInteractUnpListDict.Keys);
            foreach (string pdbId in unpEntryList)
            {
                if (Array.BinarySearch(pdbEmStructures, pdbId) > -1) // remove all EM structures
                {
                    entryInteractUnpListDict.Remove(pdbId);
                }
            }
            Dictionary<string, List<string>> interactUnpEntryListDict = GetUnpEntryListDict(entryInteractUnpListDict);
            Dictionary<string, List<string>> entryExtendInteractUnpsListDict = null;
            string[] entriesWithAllUniprots = GetEntriesContainAllUniprots(entryInteractUnpListDict, complexSameGnUnpsDict, out entryExtendInteractUnpsListDict);
            dataLine = "#" + FormatArrayString(complexEmStructures, ';') + "\tInteraction\t" + entriesWithAllUniprots.Length + "\t" + FormatArrayString(complexUniprots, ';');
            structWriter.WriteLine(dataLine);
            structWriter.WriteLine("Structures\tContainAll\tInteractComplexUniprots");
            List<string> entryList = new List<string>(entryExtendInteractUnpsListDict.Keys);
            entryList.Sort();
            foreach (string pdbId in entryList)
            {
                if (entriesWithAllUniprots.Contains(pdbId))
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
            */
            Dictionary<string, List<string>> entryUnpListDict = GetEntryComplexUnpListDict(extendedComplexUnps);
            Dictionary<string, List<string>> entryUnpRangeListDict = GetEntryComplexUnpRangeListDict(extendedComplexUnps);
            unpEntryList = new List<string>(entryUnpListDict.Keys);
            foreach (string pdbId in unpEntryList)
            {
                if (Array.BinarySearch(pdbEmStructures, pdbId) > -1) // remove all EM structures
                {
                    entryUnpListDict.Remove(pdbId);
                    entryUnpRangeListDict.Remove(pdbId);
                }
            }
            Dictionary<string, List<string>> unpEntryListDict = GetUnpEntryListDict(entryUnpListDict);
            Dictionary<string, List<string>> entryExtendUnpsListDict = null;
            entriesWithAllUniprots = GetEntriesContainAllUniprots(entryUnpListDict, complexSameGnUnpsDict, out entryExtendUnpsListDict);
            dataLine = "#" + FormatArrayString(complexEmStructures, ';') + "\tStructure\t" + entriesWithAllUniprots.Length + "\t" + FormatArrayString(complexUniprots, ';');
            structWriter.WriteLine(dataLine);
            entryList = new List<string>(entryExtendUnpsListDict.Keys);
            entryList.Sort();
            string[] orgComplexUnps = null;
            bool sameComplexUnps = true;
            structWriter.WriteLine("Structures\tContainAll\tComplexUniprots");
            foreach (string pdbId in entryList)
            {
                orgComplexUnps = GetComplexUniProts(entryExtendUnpsListDict[pdbId].ToArray(), sameGnComplexUnpDict, out sameComplexUnps);
                if (entriesWithAllUniprots.Contains(pdbId))
                {
                    dataLine = pdbId + "\tAll\t" + FormatArrayString(entryExtendUnpsListDict[pdbId], ';');
                    if (entryUnpRangeListDict.ContainsKey (pdbId))
                    {
                        dataLine = dataLine + "\t" + FormatArrayString(entryUnpRangeListDict[pdbId], ';');
                    }
                    if (!sameComplexUnps)
                    {
                        dataLine += ("\t" + FormatArrayString(orgComplexUnps));
                    }
                }
                else
                {
                    dataLine = pdbId + "\tPart\t" + FormatArrayString(entryExtendUnpsListDict[pdbId], ';');
                    if (entryUnpRangeListDict.ContainsKey(pdbId))
                    {
                        dataLine += ("\t" + FormatArrayString(entryUnpRangeListDict[pdbId], ';'));
                    }
                    if (!sameComplexUnps)
                    {
                        dataLine += ("\t" + FormatArrayString (orgComplexUnps));
                    }
                }
                structWriter.WriteLine(dataLine);
            }
            foreach (string unps in unpEntryListDict.Keys)
            {
                string[] uniprots = unps.Split (';');
                orgComplexUnps = GetComplexUniProts(uniprots, sameGnComplexUnpDict, out sameComplexUnps);
                Dictionary<string, List<int[]>> unpConnectRangeListDict = GetUniprotStructConnectRangeList(uniprots, unpEntryListDict[unps].ToArray ());
                dataLine = FormatUnpRangeListDict(unpConnectRangeListDict, uniprots);
                if (! sameComplexUnps)
                {
                    dataLine += ("\t" + FormatArrayString(orgComplexUnps));
                }
                dataLine += ("\t" + FormatArrayString(unpEntryListDict[unps], ';'));
                structWriter.WriteLine(dataLine);
            }
            structWriter.WriteLine();
            structWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <param name="sameGnUnpComplexUnpDict"></param>
        /// <returns></returns>
        private string[] GetComplexUniProts (string[] uniprots, Dictionary<string, string> sameGnUnpComplexUnpDict, out bool sameComplexUnps)
        {
            string[] complexUnps = new string[uniprots.Length];
            int count = 0;
            sameComplexUnps = true;
            foreach (string unp in uniprots)
            {
                if (sameGnUnpComplexUnpDict.ContainsKey (unp))
                {
                    complexUnps[count] = sameGnUnpComplexUnpDict[unp];
                    sameComplexUnps = false;
                }
                else
                {
                    complexUnps[count] = unp;
                }
                count++;
            }
            return complexUnps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeListDict"></param>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string FormatUnpRangeListDict (Dictionary<string, List<int[]>> unpRangeListDict, string[] uniprots)
        {
            string unpRangeString = "";
            foreach (string unp in uniprots)
            {
                if (unpRangeListDict.ContainsKey (unp))
                {
                    unpRangeString += (unp + FormatRangeList(unpRangeListDict[unp]) + ";");
                }
                else
                {
                    unpRangeString += (unp + ";");
                }
            }
            return unpRangeString.TrimEnd (';');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeListDict"></param>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string FormatUnpRangeListDict(Dictionary<string, List<int[]>> unpRangeListDict)
        {
            string unpRangeString = "";
            foreach (string unp in unpRangeListDict.Keys)
            {
                unpRangeString += (unp + FormatRangeList(unpRangeListDict[unp]) + ";");
            }
            return unpRangeString.TrimEnd(';');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rangeList"></param>
        /// <returns></returns>
        private string FormatRangeList (List<int[]> rangeList)
        {
            string rangeString = "";
            foreach (int[] range in rangeList)
            {
                rangeString += ("[" + range[0] + "-" + range[1] + "]");
            }
            return rangeString;
        }
        #endregion

        #region EM complexes to be modeled
        /// <summary>
        /// 
        /// </summary>
        public void CheckEMcomplexesToBeXtalStructModeled ()
        {
            string emStructXtalFile = Path.Combine(emDataDir, "emStructuresXtalStructInfo_unpRange_struct.txt");
            string emStructXtalModelFile = Path.Combine(emDataDir, "emStructuresXtalModeled_struct.txt");
            StreamWriter dataWriter = new StreamWriter(emStructXtalModelFile);
            StreamReader emXtalStructReader = new StreamReader(emStructXtalFile);
            string line = "";
            string[] emStructures = null;
            string[] complexUniprots = null;
            Dictionary<string, List<int[]>> unpsRangeListDict = new Dictionary<string, List<int[]>>();
            Dictionary<string, string[]> unpsEntriesDict = new Dictionary<string, string[]>();
            string[] uniprots = null;
            string[] unpEntries = null;
            List<Dictionary<string, List<int[]>>> unpRangeDictList = new List<Dictionary<string, List<int[]>>>();
            Dictionary<string, string> sameGnComplexUnpDict = new Dictionary<string,string> ();
            List<int> connectIndexList = null;
            string headerLine = "";
            List<string> dataLineList = new List<string>();
            bool xtalCoverEm = false;
            while ((line = emXtalStructReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (line == "")
                {
                    continue;
                }
                if (line.Substring (0, 1) == "#")
                {
                    if (unpRangeDictList.Count >  0)
                    {
                        if (complexUniprots.Length >= 3)
                        {
                            if (xtalCoverEm)
                            {
                                dataWriter.WriteLine(headerLine + "\txtal all");
                                foreach (string dataLine in dataLineList)
                                {
                                    dataWriter.WriteLine(dataLine);
                                }
                                dataWriter.WriteLine();
                            }
                            else if (IsComplexConnected(unpRangeDictList, complexUniprots, sameGnComplexUnpDict, out connectIndexList))
                            {
                                dataWriter.WriteLine(headerLine + "\txtal connected");
                                foreach (string dataLine in dataLineList)
                                {
                                    dataWriter.WriteLine(dataLine);
                                }
                                dataWriter.WriteLine();
                                dataWriter.WriteLine("#Connected Uniprots");
                                foreach (int index in connectIndexList)
                                {
                                    dataWriter.WriteLine(FormatUnpRangeListDict(unpRangeDictList[index]));
                                }
                                dataWriter.WriteLine();
                                dataWriter.WriteLine();
                            }
                        }
                    }
                    unpRangeDictList.Clear();
                    unpsEntriesDict.Clear();
                    sameGnComplexUnpDict.Clear ();
                    headerLine = line;
                    dataLineList.Clear();
                    xtalCoverEm = false;

                    emStructures = fields[0].Trim('#').Split (';');
                    complexUniprots = fields[3].Split(';');
                    line = emXtalStructReader.ReadLine();  // skip Structures	ContainAll	ComplexUniprots
                }
                else if (fields[0].Length == 4)
                {
                    if (fields[1] == "All")
                    {
                        xtalCoverEm = true;
                    }
                    continue;
                }
                else
                {
                    Dictionary<string, List<int[]>> unpRangeListDict = GetUniprotFromRangeFormat(fields[0], out uniprots);
                    unpRangeDictList.Add(unpRangeListDict);
                    dataLineList.Add(line);
                    if (fields.Length == 2)
                    {
                        unpEntries = fields[1].Split(';');
                    }
                    else if (fields.Length == 3)
                    {
                        string[] orgUniprots = fields[1].Split(";,".ToCharArray ());
                        unpEntries = fields[2].Split(';');
                       for (int i = 0; i < uniprots.Length; i ++)
                       {
                           if (! sameGnComplexUnpDict.ContainsKey (uniprots[i]))
                           {
                               sameGnComplexUnpDict.Add (uniprots[i], orgUniprots[i]);
                           }
                       }
                    }
                    unpsEntriesDict.Add(FormatArrayString(uniprots, ';'), unpEntries);                   
                }
            }

            if (complexUniprots.Length >= 3)
            {
                if (xtalCoverEm)
                {
                    dataWriter.WriteLine(headerLine + " xtal all");
                    foreach (string dataLine in dataLineList)
                    {
                        dataWriter.WriteLine(dataLine);
                    }
                }
                else if (IsComplexConnected(unpRangeDictList, complexUniprots, sameGnComplexUnpDict, out connectIndexList))
                {
                    dataWriter.WriteLine(headerLine + " xtal connected");
                    foreach (string dataLine in dataLineList)
                    {
                        dataWriter.WriteLine(dataLine);
                    }
                    foreach (int index in connectIndexList)
                    {
                        dataWriter.WriteLine(FormatUnpRangeListDict(unpRangeDictList[index]));
                    }
                }
            }

            emXtalStructReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeDictList"></param>
        /// <param name="complexUniprots"></param>
        /// <param name="sameGnComplexUnpDict"></param>
        /// <param name="connectIndexList"></param>
        /// <returns></returns>
        private bool IsComplexConnected (List<Dictionary<string, List<int[]>>> unpRangeDictList, string[] complexUniprots,
            Dictionary<string, string> sameGnComplexUnpDict, out List<int> connectIndexList)
        {
            List<List<int>> connectIndexListList = GetConnectIndexListList(unpRangeDictList);
            bool isConnected = false;
            connectIndexList = new List<int>();
            foreach (List<int> indexList in connectIndexListList)
            {
                isConnected = DoesConnectUniProtsCoverComplex(indexList, unpRangeDictList, complexUniprots, sameGnComplexUnpDict);
                if (isConnected)
                {
                    connectIndexList = indexList;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexList"></param>
        /// <param name="unpRangeDictList"></param>
        /// <returns></returns>
        private bool DoesConnectUniProtsCoverComplex (List<int> indexList, List<Dictionary<string, List<int[]>>> unpRangeDictList, 
            string[] complexUniprots, Dictionary<string, string> sameGnComplexUnpDict)
        {
            List<string> connectUnpList = new List<string>();
            string complexUnp = "";
            foreach (int index in indexList)
            {
                foreach (string unp in unpRangeDictList[index].Keys)
                {
                    complexUnp = unp;
                    if (sameGnComplexUnpDict.ContainsKey (unp))
                    {
                        complexUnp = sameGnComplexUnpDict[unp];
                    }
                    if (! connectUnpList.Contains (complexUnp))
                    {
                        connectUnpList.Add(complexUnp);
                    }
                }
            }
            if (connectUnpList.Count == complexUniprots.Length)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeDictList"></param>
        /// <returns></returns>
        private List<List<int>> GetConnectIndexListList (List<Dictionary<string, List<int[]>>> unpRangeDictList)
        {
            List<List<int>> connectIndexListList = new List<List<int>>();
            List<int> connectIndexList = null;
            List<int> leftIndexList = new List<int>();
            for (int i = 0; i < unpRangeDictList.Count; i++)
            {
                leftIndexList.Add(i);
            }
            for (int i = 0; i < unpRangeDictList.Count; i ++)
            {
                if (unpRangeDictList[i].Count < 2)
                {
                    continue;
                }
                if (! leftIndexList.Contains (i))
                {
                    continue;
                }
                connectIndexList = new List<int>();
                connectIndexList.Add(i);
                leftIndexList.Remove(i);
                for (int j = i + 1; j < unpRangeDictList.Count; j ++)
                {
                    if (unpRangeDictList[j].Count < 2)
                    {
                        continue;
                    }
                    if (!leftIndexList.Contains(j))
                    {
                        continue;
                    }
                    if (AreAnyUniprotsRangesOverlap (unpRangeDictList[i], unpRangeDictList[j]))
                    {
                        connectIndexList.Add(j);
                        leftIndexList.Remove(j);
                    }
                }
                connectIndexListList.Add(connectIndexList);
            }

            return connectIndexListList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRangeFormat"></param>
        /// <returns></returns>
        private Dictionary<string, List<int[]>> GetUniprotFromRangeFormat (string unpRangeFormat, out string[] uniprots)
        {
            string[] unpRangeFields = unpRangeFormat.Split(';');
            string unpCode = "";
            List<int[]> rangeList = null;
            List<string> unpList = new List<string>();
            Dictionary<string, List<int[]>> unpRangeListDict = new Dictionary<string, List<int[]>>();
            foreach (string unpRangeField in unpRangeFields)
            {
                string[] fields = unpRangeField.Split('[');
                rangeList = new List<int[]>();
                unpCode = fields[0];
                unpList.Add(unpCode);
                for (int i = 1; i < fields.Length; i ++)
                {
                    int[] range = new int[2];
                    string[] rangeFields = fields[i].TrimEnd(']').Split('-');
                    range[0] = Convert.ToInt32(rangeFields[0]);
                    range[1] = Convert.ToInt32(rangeFields[1]);
                    rangeList.Add(range);
                }
                unpRangeListDict.Add(unpCode, rangeList);
            }
            uniprots = unpList.ToArray();
            return unpRangeListDict;
        }
        /// <summary>
        /// To do list on Jan. 31, 2019
        /// 1. check whether any x-ray crystals containing all UniProts
        /// 2. Check whether same UniProts are actually overlap in structures
        /// 2.1 overlap of two structures containing interactions
        /// 2.2 whether any structures can connect the fragements of the Uniprot in two structures
        /// </summary>
        public void RetrieveEMcomplexesToBeModeled ()
        {
            logWriter.WriteLine("Retrieving structures and pfam info for EM complexes.");
            string emComplexListFile = Path.Combine(emDataDir, "EMstructuresUnpComplexes_all.txt");
            Dictionary<string[], string[]> emComplexEntryListDict = ReadEMcomplexEntryListDict(emComplexListFile);
            string emComplexXtalStructPfamInfoFile = Path.Combine (emDataDir, "EMcomplexXtalPfamInfo_update_3.txt");
            StreamWriter dataWriter = new StreamWriter (emComplexXtalStructPfamInfoFile);

            Dictionary<string[], string> complexStructConnectDict = ReadComplexStructModelInfoDict();

            Dictionary<string[], string[]> emComplexesStructModeledDict = new Dictionary<string[], string[]>();
            Dictionary<string[], string[]> emComplexesStructPfamModeledDict = new Dictionary<string[], string[]>();
            int numEmComplexes3 = 0;
            int numEmComplexesStruct3 = 0;
            int numEmComplexesStructConnect3 = 0;
            int numEmComplexesStructAll3 = 0;
            int numEmComplexesStructPfam3 = 0;
            int numEmComplexes = 0;
            int numEmComplexesStruct = 0;
            int numEmComplexesStructPfam = 0;
            foreach (string[] emUniprots in emComplexEntryListDict.Keys)
            {
                if (emUniprots.Length >= 3)
                {
                    numEmComplexes3 ++;
                }
                numEmComplexes++;
                try
                {
                    bool[] canComplexBeModeled = RetrieveEMComplexPdbProtCidInfo(emUniprots, emComplexEntryListDict[emUniprots], dataWriter);
                    if (canComplexBeModeled[0])
                    {
                        emComplexesStructModeledDict.Add(emUniprots, emComplexEntryListDict[emUniprots]);
                        if (emUniprots.Length >= 3)
                        {
                            numEmComplexesStruct3 ++;
                            if (complexStructConnectDict.ContainsKey (emUniprots))
                            {
                               if (complexStructConnectDict[emUniprots] == "xtal all")
                               {
                                   numEmComplexesStructAll3++;
                               }
                               else
                               {
                                   numEmComplexesStructConnect3++;
                               }
                            }
                        }
                        numEmComplexesStruct++;
                    }
                    else if (canComplexBeModeled[1])
                    {
                        emComplexesStructPfamModeledDict.Add(emUniprots, emComplexEntryListDict[emUniprots]);
                        if (emUniprots.Length >= 3)
                        {
                            numEmComplexesStructPfam3 ++;
                        }
                        numEmComplexesStructPfam++;
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Retrieving Xtal structures and Pfam Info of EM complex error: " + ex.Message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close ();

            string emComplexStructPfamListFile = Path.Combine(emDataDir, "EMcomplexXtalPfamModeledList_update_2.txt");
            dataWriter = new StreamWriter(emComplexStructPfamListFile);
            dataWriter.WriteLine("#Complexes with N>=3: " + numEmComplexes3);
            dataWriter.WriteLine("#Complexes with N>=3 and structures modeled: " + numEmComplexesStruct3);
            dataWriter.WriteLine("#Complexes with N>=3 and structures connection modeled: " + numEmComplexesStructConnect3);
            dataWriter.WriteLine("#Complexes with N>=3 and structures all modeled: " + numEmComplexesStructAll3);
            dataWriter.WriteLine("#Complexes with N>=3 and struct/Pfam modeled: " + numEmComplexesStructPfam3);
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Complexes: " + numEmComplexes);
            dataWriter.WriteLine("#Complexes can be structures modeled: " + numEmComplexesStruct);
            dataWriter.WriteLine("#Complexes can be struct/Pfam modeled: " + numEmComplexesStructPfam);
            dataWriter.WriteLine();
            dataWriter.WriteLine("All complexes can be structure modeled: " + emComplexesStructModeledDict.Count);
            foreach (string[] emUniprots in emComplexesStructModeledDict.Keys)
            {
                dataWriter.WriteLine(FormatArrayString (emUniprots, ';') + "\t" + FormatArrayString (emComplexesStructModeledDict[emUniprots], ';'));
            }
            dataWriter.WriteLine();
            dataWriter.WriteLine("All complexes can be structure/Pfam modeled: " + emComplexesStructPfamModeledDict.Count);
            foreach (string[] emUniprots in emComplexesStructPfamModeledDict.Keys)
            {
                dataWriter.WriteLine(FormatArrayString(emUniprots, ';') + "\t" + FormatArrayString(emComplexesStructPfamModeledDict[emUniprots], ';'));
            }
            dataWriter.Close();
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string[], string> ReadComplexStructModelInfoDict ()
        {
            string complexStructModelFile = Path.Combine(emDataDir, "emStructuresXtalModeled_struct.txt");
            StreamReader dataReader = new StreamReader(complexStructModelFile);
            string line = "";
            Dictionary<string[], string> complexXtalConnectDict = new Dictionary<string[], string>(new StringArrayEqualityComparer());
            string[] complexUniprots = null;
            string xtalConnect = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("#") > -1)
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length >= 4)
                    {
                        string[] unpStructFields = fields[3].Split(' ');
                        complexUniprots = unpStructFields[0].Split(';');
                        xtalConnect = unpStructFields[1] + " " + unpStructFields[2];
                        complexXtalConnectDict.Add(complexUniprots, xtalConnect);
                    }
                }
            }
            dataReader.Close();
            return complexXtalConnectDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="emComplexListFile"></param>
        /// <returns></returns>
        private Dictionary<string[], string[]> ReadEMcomplexEntryListDict(string emComplexListFile)
        {
            string line = "";
            Dictionary<string[], string[]> emComplexEntryListDict = new Dictionary<string[], string[]>();
            StreamReader dataReader = new StreamReader(emComplexListFile);
            while ((line= dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split  ('\t');
                emComplexEntryListDict.Add(fields[0].Split (','), fields[1].Split(','));
            }
            dataReader.Close();
            return emComplexEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUniprots"></param>
        public bool[] RetrieveEMComplexPdbProtCidInfo(string[] complexUniprots, string[] emEntries, StreamWriter dataWriter)
        {
            bool canComplexBeHomoModeled = false;
            bool canComplexBeStructModeled = false;
            bool[] canComplexBeModeled = new bool[2];
            Dictionary<string, List<int>> unpPairRelSeqIdListDict = null;
            string[] extendedComplexUnps = null;
            Dictionary<string, string[]> complexSameGnUnpsDict = GetComplexSameGeneUnpsDict(complexUniprots, out extendedComplexUnps);
            Dictionary<string, string> sameGnUnpComplexUnpDict = ReverseComplexSameGnUniprotsDict(complexSameGnUnpsDict);

            Dictionary<string, List<string>> allUnpPairInterfaceListDict = RetrieveComplexStructureInfo(extendedComplexUnps, out unpPairRelSeqIdListDict);
            Dictionary<string, List<string>> unpPairInterfaceListDict = RemoveUnpPairInterfaceDictEmStructures(allUnpPairInterfaceListDict, emStructuresPdb);

            Dictionary<string, List<string>> unpPairPfamClusterListDict = FindUnpPairPfamClusters(complexSameGnUnpsDict);
            //           Dictionary<string, List<string>> unpPairInterfaceListDict = RetrieveComplexStructureInfo(complexUniprots, out unpPairRelSeqIdListDict);         
            //           Dictionary<string, List<string>> unpPairPfamClusterListDict = FindUnpPairPfamClusters(complexUniprots);

            List<string> unpPairStructList = new List<string>(unpPairInterfaceListDict.Keys);
            List<string> unpPairPfamList = new List<string>(unpPairPfamClusterListDict.Keys);

            Dictionary<string, List<string>> complexStructConnectListDict = GetComplexConnectedListDict(complexUniprots, unpPairStructList,
                complexSameGnUnpsDict, sameGnUnpComplexUnpDict);
            Dictionary<string, List<string>> complexPfamConnectListDict = GetComplexConnectedListDict(complexUniprots, unpPairPfamList,
                complexSameGnUnpsDict, sameGnUnpComplexUnpDict);

            List<List<string>> structConnectedComponentList = GetLinkedHumanProteins(complexStructConnectListDict);
    /*        string[] notConnectUniprots = null;
            string[] notStructConnectUniprots = null;
            bool isComplexStructConnected = IsComplexUniprotsStructConnected(complexUniprots, unpPairStructList, complexSameGnUnpsDict, out notStructConnectUniprots);*/
            dataWriter.WriteLine("#" + FormatArrayString(complexUniprots, ';') + "\t" + FormatArrayString(emEntries, ';'));
            
            if (structConnectedComponentList.Count > 1)
            {
                dataWriter.WriteLine("Can not be structure modeled");
                dataWriter.WriteLine("#\t#Structure Connected Components=" + structConnectedComponentList.Count + "\t" + FormatConnectUniprotsList(structConnectedComponentList));
            }
            else
            {
                canComplexBeStructModeled = true;
                dataWriter.WriteLine("#\tComplex is connected by structures");
            }
            dataWriter.Flush();           
 //          bool isComplexConnected = IsComplexUniprotsConnected(complexUniprots, unpPairStructList, unpPairPfamList, complexSameGnUnpsDict, out notConnectUniprots);
            List<List<string>> pfamConnectedComponentList = GetLinkedHumanProteins(complexPfamConnectListDict);
            if (pfamConnectedComponentList.Count > 1)
            {
                dataWriter.WriteLine("Can not be structure/Pfam modeled");
                dataWriter.WriteLine("#\t#Pfam/Structure Connected Components=" + pfamConnectedComponentList.Count + "\t" + FormatConnectUniprotsList(pfamConnectedComponentList));
            }
            else
            {
                canComplexBeHomoModeled = true;
                dataWriter.WriteLine("#\tComplex is connected by structures/Pfams");
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
        /// <param name="unpPairInterfaceListDict"></param>
        /// <param name="emEntries"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> RemoveUnpPairInterfaceDictEmStructures(Dictionary<string, List<string>> unpPairInterfaceListDict, string[] emEntries)
        {
            Dictionary<string, List<string>> noEmUnpPairInterfaceListDict = new Dictionary<string, List<string>>();
            foreach (string unpPair in unpPairInterfaceListDict.Keys)
            {
                List<string> noEmInterfaceList = new List<string>(unpPairInterfaceListDict[unpPair]);
                foreach (string chainInterface in unpPairInterfaceListDict[unpPair])
                {
                    if (emEntries.Contains (chainInterface.Substring (0, 4)))
                    {
                        noEmInterfaceList.Remove(chainInterface);
                    }
                }
                if (noEmInterfaceList.Count > 0)
                {
                    noEmUnpPairInterfaceListDict.Add(unpPair, noEmInterfaceList);
                }
            }
            return noEmUnpPairInterfaceListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryUnpListDict"></param>
        /// <returns></returns>
        private string FormatXtalStructuresUnps (Dictionary<string, List<string>> entryUnpListDict)
        {
            string entryUnpListString = "";
            List<string> entryList = new List<string>(entryUnpListDict.Keys);
            entryList.Sort();
            foreach (string pdbId in entryList)
            {
                entryUnpListString += (pdbId + ":" + ParseHelper.FormatSqlListString (entryUnpListDict[pdbId]) + " ");
            }
            return entryUnpListString.TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool HasEMStructureXtalStructuresOnlyPartialUniProts (string pdbId, out Dictionary<string, List<string>> entryUnpListDict,
            out string[][] entryUnpsHumanUnps)
        {
            entryUnpListDict = new Dictionary<string, List<string>>();
            entryUnpsHumanUnps = GetEntryUniProts(pdbId);
            string[] entryUnps = entryUnpsHumanUnps[0];
            string[] entryHumanUnps = entryUnpsHumanUnps[1];
            string entryComplexUnpString = GetComplexUnpString(entryUnps);

            if (parsedUnpComplexEntryListDict.ContainsKey(entryComplexUnpString))
            {
                parsedUnpComplexEntryListDict[entryComplexUnpString].Add(pdbId);
                return false;
            }
            else
            {
                List<string> entryList = new List<string>();
                entryList.Add(pdbId);
                parsedUnpComplexEntryListDict.Add(entryComplexUnpString, entryList);
            }
            if (entryUnps.Length < 3)
            {
                return false;
            }
            else 
            {
                entryUnpListDict = GetXtalEntryUniprotDict (entryUnps);
                if (DoStructuresContainsAllUniprots (entryUnpListDict, entryUnps))
                {
                    return false;
                }
            }           
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexUnps"></param>
        /// <returns></returns>
        private string GetComplexUnpString(string[] complexUnps)
        {
            Array.Sort(complexUnps);
            string complexUnpsString = ParseHelper.FormatStringFieldsToString(complexUnps);
            return complexUnpsString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void GetEMStructureUnpPfamInterfaceClusters (string pdbId, StreamWriter dataWriter)
        {
            DataTable entryUnpPfamInterfaceTable = RetrieveEMUnpPfamInterfaces(pdbId);
            Dictionary<int, string[]> relationClustersDict = new Dictionary<int, string[]>();
            int relSeqId = 0;
            string[] clusterLines = null;
            foreach (DataRow interfaceRow in entryUnpPfamInterfaceTable.Rows)
            {
                relSeqId = Convert.ToInt32(interfaceRow["RelSeqID"].ToString ());
                if (relationClustersDict.ContainsKey(relSeqId))
                {
                    clusterLines = relationClustersDict[relSeqId];
                }
                else
                {
                    DataRow biggestEntryClusterRow = GetBiggestDomainClusterInEntry(pdbId, relSeqId);
                    string beggestEntryClusterLine = FormatClusterRow(biggestEntryClusterRow);
                    DataRow biggestClusterRow = GetBiggestDomainCluster(relSeqId);
                    string biggestClusterLine = FormatClusterRow(biggestClusterRow);
                    clusterLines = new string[2];
                    clusterLines[0] = beggestEntryClusterLine;
                    clusterLines[1] = biggestClusterLine;
                    relationClustersDict.Add(relSeqId, clusterLines);
                }
                dataWriter.WriteLine(ParseHelper.FormatDataRow(interfaceRow) + "\t" + clusterLines[0] + "\t" + clusterLines[1]);
            }
            dataWriter.Flush ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataRow GetBiggestDomainClusterInEntry (string pdbId, int relSeqId)
        {
            string queryString = string.Format ("Select PfamDomainClusterSumInfo.RelSeqID, PfamDomainClusterSumInfo.ClusterID, NumOfCfgCluster, NumOfCfgRelation, NumOfEntryCluster, " +
                " NumOfEntryRelation, MinSeqIdentity, PfamDomainClusterSumInfo.SurfaceArea, PfamDomainClusterSumInfo.InPdb, PfamDomainClusterSumInfo.InPisa, PfamDomainClusterSumInfo.InAsu " +
                " From PfamDomainClusterSumInfo, PfamDomainClusterInterfaces Where PfamDomainClusterInterfaces.RelSeqID = {0} AND PdbID = '{1}' AND " +
                " PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID " + 
                " Order By NumOfCfgCluster DESC;", relSeqId, pdbId);
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
        /// <param name="clusterRow"></param>
        /// <returns></returns>
        private string FormatClusterRow (DataRow clusterRow)
        {
            string clusterLine = "-";
            if (clusterRow != null)
            {
                clusterLine = clusterRow["ClusterID"] + " " + clusterRow["NumOfCfgCluster"] + "/" + clusterRow["NumOfCfgRelation"] + " " +
                    clusterRow["NumOfEntryCluster"] + "/" + clusterRow["NumOfEntryRelation"] + " " + clusterRow["MinSeqIdentity"] + " " + clusterRow["SurfaceArea"] + " " +
                    clusterRow["InPDB"] + "/" + clusterRow["InPISA"] + "/" + clusterRow["InASU"];
            }

            return clusterLine;
        }

        
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable RetrieveEMUnpPfamInterfaces (string pdbId)
        {
            string queryString = string.Format("Select PfamDomainInterfaces.RelSeqID, FamilyCode1 as PfamID1, FamilyCode2 as PfamID2, UnpID1, UnpID2, " +
                " PfamDomainInterfaces.PdbID, PfamDomainInterfaces.DomainInterfaceID, AsymChain1, AsymChain2 " + 
                " From UnpPdbDomainInterfaces, PfamDomainInterfaces, PfamDomainFamilyRelation " +
                " WHere UnpPdbDomainInterfaces.PdbID = '{0}' AND UnpPdbDomainInterfaces.PdbID = PfamDomainInterfaces.PdbID AND " + 
                " UnpPdbDomainInterfaces.DomainInterfaceID = PfamDomainInterfaces.DomainInterfaceID AND UnpPdbDomainInterfaces.RelSeqID = PfamDomainFamilyRelation.RelSeqID;", pdbId);
            DataTable entryUnpPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            return entryUnpPfamTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[][] GetEntryUniProts (string pdbId)
        {
            string queryString = string.Format("Select Distinct DbCode  From PdbDBRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode  From PdbDBRefXml Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
                unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string[] uniprots = new string[unpTable.Rows.Count];
            List<string> humanUniprotList = new List<string>();
            int count = 0;
            string unpCode = "";
            foreach (DataRow unpRow in unpTable.Rows)
            {
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                uniprots[count] = unpCode;
                count++;
                if (unpCode.IndexOf ("_HUMAN") > -1)
                {
                    humanUniprotList.Add(unpCode);
                }
            }
            string[][] entryUniprots = new string[2][];
            entryUniprots[0] = uniprots;
            entryUniprots[1] = humanUniprotList.ToArray();
            return entryUniprots;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetXtalEntryUniprotDict(string[] uniprots)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbID, DbCode From PdbDbRefSifts, PdbEntry Where DbCode IN ({0}) " +
               " AND (Method Like 'X-RAY%' OR Method Like '% NMR') AND PdbDbRefSifts.PdbID = PdbEntry.PdbID);",
               ParseHelper.FormatSqlListString(uniprots));          
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
                    if (!entryUnpListDict.ContainsKey(pdbId))
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
            queryString = string.Format("Select Distinct PdbDbRefXml.PdbID, DbCode From PdbDbRefXml, PdbEntry Where DbCode IN ({0}) " +
               " AND (Method Like 'X-RAY%' OR Method Like '% NMR') AND PdbDbRefXml.PdbID = PdbEntry.PdbID);",
                ParseHelper.FormatSqlListString(uniprots));
            DataTable entryUnpTableXml = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow unpRow in entryUnpTableXml.Rows)
            {
                pdbId = unpRow["PdbID"].ToString();
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                if (entryListInSifts.Contains (pdbId))
                {
                    continue;
                }
                if (! entryUnpListDict.ContainsKey(pdbId))
                {
                    if (!entryUnpListDict.ContainsKey(pdbId))
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
        /// <param name="entryUnpListDict"></param>
        /// <param name="complexUniprots"></param>
        /// <returns></returns>
        private bool DoStructuresContainsAllUniprots (Dictionary<string, List<string>> entryUnpListDict, string[] complexUniprots)
        {
            foreach (string pdbId in entryUnpListDict.Keys)
            {
                if (entryUnpListDict[pdbId].Count == complexUniprots.Length )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryChainUnpDict(string pdbId)
        {
            string queryString = string.Format("Select AsymID, DbCode  From PdbDBRefSifts, AsymUnit " +
                " Where PdbDBRefSifts.PdbID = '{0}' AND DbName = 'UNP' AND PdbDbRefSifts.PdbID = AsymUnit.PdbID AND " +
                " PdbDbRefSifts.EntityID = AsymUnit.EntityID Order By AsymID, RefID;", pdbId);
            DataTable chainUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (chainUnpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select AsymID, DbCode  From PdbDBRefXml, AsymUnit " +
                " Where PdbDBRefXml.PdbID = '{0}' AND DbName = 'UNP' AND PdbDbRefXml.PdbID = AsymUnit.PdbID AND " +
                " PdbDbRefXml.EntityID = AsymUnit.EntityID Order By AsymID, RefID;", pdbId);
                chainUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            Dictionary<string, string> chainUnpDict = new Dictionary<string, string>();
            string asymChain = "";
            string unpCode = "";
            foreach (DataRow unpRow in chainUnpTable.Rows)
            {
                asymChain = unpRow["AsymID"].ToString().TrimEnd();
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                if (chainUnpDict.ContainsKey(asymChain))
                {
                    chainUnpDict[asymChain] = chainUnpDict[asymChain] + "-" + unpCode;
                }
                else
                {
                    chainUnpDict.Add(asymChain, unpCode);
                }
            }
            return chainUnpDict;
        }
        #endregion

        #region EM complexes in PDB
        /// <summary>
        /// 
        /// </summary>
        public void PrintEMStructuresComplexInfo ()
        {
            string EMentryStructInfoFile = Path.Combine(emDataDir, "EMentryStructuresInfo.txt");
            StreamWriter dataWriter = new StreamWriter(EMentryStructInfoFile);
            dataWriter.WriteLine("PdbID\t#Uniprots\t#HumanUniprots\t#Pfams\tResolution\tASU BiolAssemblies\tEntity:UniProt\tEntity:PfamArch");

            string queryString = "Select PdbID From PdbEntry Where Method like 'ELECTRON %';";
            DataTable EMentryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            string dataLine = "";
            string baLine = "";
            string unpLine = "";
            string pfamLine = "";
            int numUnps = 0;
            int numHumanUnps = 0;
            int numPfams = 0;
            string resolution = "";
            foreach (DataRow entryRow in EMentryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (! DoesEntryContainProteins (pdbId))
                {
                    continue;
                }
                baLine = GetBiolAssemblies(pdbId);
                unpLine = GetEMentryUniprots(pdbId,out numUnps, out numHumanUnps);
                pfamLine = GetEMentryPfams(pdbId);
                numPfams = GetEMentryNumPfams(pdbId);
                resolution = GetEntryResolution(pdbId);
                dataLine = pdbId + "\t" + numUnps + "\t" + numHumanUnps + "\t" + numPfams + "\t"  + 
                    resolution + "\t" + baLine + "\t" +unpLine + "\t" + pfamLine;
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool DoesEntryContainProteins (string pdbId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (protChainTable.Rows.Count > 0)
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
        private string GetEntryResolution (string pdbId)
        {
            string queryString = string.Format("Select Resolution From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable resolutionTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (resolutionTable.Rows.Count > 0)
            {
                return resolutionTable.Rows[0]["Resolution"].ToString();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetBiolAssemblies (string pdbId)
        {
            string queryString = string.Format("Select ASU_ABC, PDBBU_ABC, PISABU_ABC From ProtBudBiolAssemblies Where PdbID = '{0}';", pdbId);
            DataTable baTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (baTable.Rows.Count == 0)
            {
                return "";
            }
            string biolAssemblies = baTable.Rows[0]["ASU_ABC"].ToString ().TrimEnd ();
            string pdbBAs = "";
            string pisaBAs = "";
            foreach (DataRow baRow in baTable.Rows)
            {
                pdbBAs += (baRow["PDBBU_ABC"].ToString().TrimEnd() + ";");
                pisaBAs += (baRow["PISABU_ABC"].ToString().TrimEnd() + ";");
            }
            biolAssemblies += (" " + pdbBAs + " " + pisaBAs);
            return biolAssemblies;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEMentryUniprots (string pdbId, out int numOfUNPs, out int numOfHumanUNPs)
        {
            string queryString = string.Format("Select Distinct EntityID, DbCode  From PdbDBRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct EntityID, DbCode  From PdbDBRefXml Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
                unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string entryUnps = "";
            List<string> unpList = new List<string>();
            List<string> humanUnpList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in unpTable.Rows)
            {
                unpCode = unpRow["DbCode"].ToString().TrimEnd().ToUpper ();
                entryUnps += (unpRow["EntityID"].ToString() + ":" + unpCode + ";");
                if (! unpList.Contains (unpCode))
                {
                    unpList.Add(unpCode);
                }
                if (unpCode.IndexOf ("_HUMAN") > -1 && ! humanUnpList.Contains (unpCode))
                {
                    humanUnpList.Add(unpCode);
                }
            }
            numOfUNPs = unpList.Count;
            numOfHumanUNPs = humanUnpList.Count;
            return entryUnps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEMentryPfams(string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID, PfamArch  From PfamEntityPfamArch Where PdbID = '{0}';", pdbId);
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
           
            string entryPfams = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                entryPfams += (pfamRow["EntityID"].ToString() + ":" + pfamRow["PfamArch"].ToString().TrimEnd() + ";");
            }
            return entryPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetEMentryNumPfams (string pdbId)
        {
            string[] entryPfams = GetEntryPfams(pdbId);
            return entryPfams.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPfams(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] pfamIds = new string[pfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamIds[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }
        #endregion

        #region add EM resolution
        private DbUpdate pdbfamUpdate = null;
        /// <summary>
        /// 
        /// </summary>
        public void AddMissingEMresolution()
        {
            pdbfamUpdate = new DbUpdate(ProtCidSettings.pdbfamDbConnection);
            Dictionary<string, double> entryResolutionDict = GetEntryResolutionDictFromPdbResolutionFile ();
            string queryString = "Select PdbID From PdbEntry Where Method Like 'ELECTRON %' AND Resolution = 0;";
            DataTable missingResolutionEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            double resolution = 0;
            foreach (DataRow entryRow in missingResolutionEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (entryResolutionDict.ContainsKey (pdbId))
                {
                    resolution = entryResolutionDict[pdbId];
                    if (resolution > -1)
                    {
                        UpdateResolution(pdbId, resolution);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, double> GetEntryResolutionDictFromPdbResolutionFile ()
        {
            Dictionary<string, double> entryResolutionDict = new Dictionary<string, double>();
            string resolutionFile = @"D:\Qifang\ProjectData\DbProjectData\PDB\resolu.idx";
            StreamReader dataReader = new StreamReader(resolutionFile);
            string line = "";
            string pdbId = "";
            double resolution = -1;
            string resolutionField = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("\t;\t") > -1)
                {
                    pdbId = line.Substring(0, 4).ToLower ();
                    resolutionField = line.Substring(7, line.Length - 7);
                    resolution = -1;
                    if (Double.TryParse (resolutionField, out resolution))
                    {
                        if (!entryResolutionDict.ContainsKey(pdbId))
                        {
                            entryResolutionDict.Add(pdbId, resolution);
                        }
                    }
                }
            }
            dataReader.Close();

            return entryResolutionDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="resolution"></param>
        private void UpdateResolution (string pdbId, double resolution)
        {
            string updateString = string.Format("Update PdbEntry Set Resolution = {0} Where PdbID = '{1}';", resolution, pdbId);
            pdbfamUpdate.Update(updateString);
        }
        #endregion
    }
}
