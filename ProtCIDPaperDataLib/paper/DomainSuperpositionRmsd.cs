using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Net;
using InterfaceClusterLib.DomainInterfaces;
using InterfaceClusterLib.PymolScript;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.StructureComp;
using InterfaceClusterLib.PymolScript;
using CrystalInterfaceLib.DomainInterfaces;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;

namespace ProtCIDPaperDataLib.paper
{
    public class DomainSuperpositionRmsd : PaperDataInfo
    {
        private DomainAlignPymolScript domainAlignPymolScript = new DomainAlignPymolScript();
        private CmdOperations pymolLauncher = new CmdOperations();
        private StreamWriter logWriter = new StreamWriter("DomainRmsdLog.txt", true);
        public RmsdCalculator rmsdCal = new RmsdCalculator();
        private DomainAlignPymolScript domainAlignPmlScript = new DomainAlignPymolScript();
        private SeqNumbersMatchData seqNumMatch = new SeqNumbersMatchData ();
        private WebClient downloadClient = new WebClient();
        private string rcsbServerAddress = "https://files.rcsb.org/download/";
        private string rmsdDataDir = "";
        private string coordDataDir = "";

        Dictionary<string, string> chainMutationDict = null;
        Dictionary<string, string> entryLigandsDict = null;
        Dictionary<string, string> entryPdbBaDict = null;
        Dictionary<string, string> entryUnpsDict = null;
        Dictionary<string, string> entryCrystInfoDict = null;


        public void CalculateDomainRegionRmsd()
        {
            rmsdDataDir = Path.Combine(dataDir, "RasRmsd");
            coordDataDir = Path.Combine(rmsdDataDir, "pdb");

            if (!Directory.Exists(rmsdDataDir))
            {
                Directory.CreateDirectory(rmsdDataDir);
            }
            if (!Directory.Exists(coordDataDir))
            {
                Directory.CreateDirectory(coordDataDir);
            }
            //       string pfamId = "Ras";
            string proteinName = "RASH";
            int[] switchOneRange = { 25, 40 };
            int[] switchTwoRange = { 57, 67 };
            int[][] switchOneTwoRanges = new int[2][];
            switchOneTwoRanges[0] = switchOneRange;
            switchOneTwoRanges[1] = switchTwoRange;
            logWriter.WriteLine(DateTime.Today.ToShortDateString());
            logWriter.WriteLine("Calculate RMSD of regions of " + proteinName);
            string centerChain = "5p21A";
            Dictionary<string, string> chainUnpDict = null;
            List<string> chainCoordList = new List<string>();
            Dictionary<string, List<string>> entryRasChainListDict = GetProteinChains(proteinName, out chainUnpDict);
            /*    List<string> rasPdbList = new List<string> (entryRasChainListDict.Keys);
                DownloadPdbFiles(rasPdbList.ToArray());
                string pdbFile = "";
                foreach (string pdbId in rasPdbList)
                {
                    pdbFile = Path.Combine(coordDataDir, pdbId + ".pdb");
                    Dictionary<string, List<string>> chainAtomLineListDict = ReadProteinChainFiles (pdbFile, entryRasChainListDict[pdbId].ToArray ());
                    string[] chainFileNames = WriteChainFiles(pdbId, chainAtomLineListDict);
                    chainCoordList.AddRange(chainFileNames);
                }
                */
            chainCoordList = GetChainCoordFileNames(coordDataDir);
            string[] entries = GetEntryList(chainCoordList.ToArray());
            chainMutationDict = GetChainMutationsDict(chainCoordList.ToArray());
            entryLigandsDict = GetEntryLigandsDict(entries);
            entryPdbBaDict = GetEntryFirstPdbBaDict(entries);
            entryUnpsDict = GetEntryUniprotsDict(entries);
            entryCrystInfoDict = GetEntryCrystInfoDict(entries);
            /*      string pmlScriptFile = Path.Combine(coordDataDir, "RasHAlign.pml");
                  WritePyMolScript(centerChain, coordDataDir, pmlScriptFile);*/

            chainCoordList.Remove(centerChain);

            // RMSD-theseus
            string dataType = "theseus";
            coordDataDir = Path.Combine(rmsdDataDir, dataType);
            string rmsdFile = Path.Combine(rmsdDataDir, proteinName + "_SwitchOneTwo_RMSDs_" + dataType + "_cryst.txt");
            StreamWriter rmsdWriter = new StreamWriter(rmsdFile);
            rmsdWriter.Write("PDB1\tPDB2\tRMSD1\tRMSD2\tMutations\tPdbBA\tUniProts\tLigands\tCrystTemp\tCrystPH\tResolution\tMethod\tSpaceGroup\n");
            string centerChainInfo = GetChainInfo(centerChain);
            rmsdWriter.Write(centerChainInfo + "\n");
            CalculateDomainRegionRmsdFromTheseus(centerChain, chainCoordList.ToArray(), dataType, chainUnpDict, switchOneTwoRanges, rmsdWriter);
            rmsdWriter.Close();

            // RMSD-PyMOL
            /*
                        string rmsdFile = Path.Combine(rmsdDataDir, proteinName + "_SwitchOneTwo_RMSDs.txt");
                        StreamWriter rmsdWriter = new StreamWriter(rmsdFile);
                        rmsdWriter.Write("PDB1\tPDB2\tRMSD1\tRMSD2\n");
              //          CalculateDomainRegionRmsd(centerChain, chainCoordList.ToArray(), chainUnpDict, switchTwoRange, rmsdWriter);
                        CalculateDomainRegionRmsd(centerChain, chainCoordList.ToArray(), chainUnpDict, switchOneTwoRanges, rmsdWriter);
                        rmsdWriter.Close();
              */
        }

        #region RMSD from pymol
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerChain"></param>
        /// <param name="coordDataDir"></param>
        /// <param name="pmlScriptFile"></param>
        public void WritePyMolScript (string centerChain, string coordDataDir, string pmlScriptFile)
        {
            List<string> chainCoordList = GetChainCoordFileNames(coordDataDir);
             
            StreamWriter pmlWriter = new StreamWriter(pmlScriptFile);
            foreach (string chainName in chainCoordList)
            {
                pmlWriter.Write("load " + chainName + ".pdb\n");
            }
            pmlWriter.Write("alignto " + centerChain);
            pmlWriter.Close();
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDataDir"></param>
        /// <returns></returns>
        private List<string> GetChainCoordFileNames (string coordDataDir)
        {
            StreamWriter lsWriter = new StreamWriter(Path.Combine (coordDataDir, "ls-chains_1.txt"));
            string[] chainFiles = Directory.GetFiles(coordDataDir, "*.pdb");
            List<string> chainFileNameList = new List<string>();
            string chainName = "";
            foreach (string chainFile in chainFiles)
            {
                FileInfo fileInfo = new FileInfo(chainFile);
                chainName = fileInfo.Name.Replace (".pdb", "");
                if (chainName.Length > 4)
                {
                    chainFileNameList.Add(chainName);
                    lsWriter.Write(chainName + "\n");
                }
            }
            lsWriter.Close();
            return chainFileNameList;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="compDomains"></param>
        /// <param name="seqRange"></param>
        public void CalculateDomainRegionRmsd(string centerChain, string[] compChains, Dictionary<string, string> chainUnpDict, int[] protSeqRange, StreamWriter rmsdWriter)
        {
            string pymolScriptFile = "";
            string pymolCoordFile = "";

            string[] chains = new string[compChains.Length + 1];
            chains[0] = centerChain;
            Array.Copy(compChains, 0, chains, 1, compChains.Length);
          
            Dictionary<int, int> centerChainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers (centerChain.Substring(0, 4), centerChain.Substring(4, centerChain.Length - 4), chainUnpDict[centerChain]);
            Dictionary<string, Dictionary<int, int>> chainDbPdbSeqMatchDict = new Dictionary<string, Dictionary<int, int>>();
            chainDbPdbSeqMatchDict.Add(centerChain, centerChainDbSeqIdDict);

            List<string> chainListInPmlScript = new List<string>();
            chainListInPmlScript.Add(centerChain);
            foreach (string compChain in compChains)
            {
                try
                {
                    Dictionary<int, int> chainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(compChain.Substring(0, 4), compChain.Substring(4, compChain.Length - 4), chainUnpDict[compChain]);
                    chainDbPdbSeqMatchDict.Add(compChain, chainDbSeqIdDict); 
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }

            Dictionary<string, int[]> chainSeqRangeDict = GetChainSeqRanges(chains, protSeqRange, chainDbPdbSeqMatchDict);

           foreach (string compChain in compChains)
           {
               pymolScriptFile = Path.Combine(coordDataDir, centerChain + "_" + compChain + "_pairFit.pml");
               pymolCoordFile = Path.Combine(coordDataDir, centerChain + "_" + compChain + ".coord");
               try
               {
                   CalculateTwoRegionsRmsd(centerChain, compChain, centerChainDbSeqIdDict, chainDbPdbSeqMatchDict[compChain], chainSeqRangeDict, pymolScriptFile, pymolCoordFile, rmsdWriter);
               }
               catch (Exception ex)
               {
                   logWriter.WriteLine(centerChain + " " + compChain + " error: " + ex.Message);
                   logWriter.Flush();
               }
           }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="compDomains"></param>
        /// <param name="seqRange"></param>
        public void CalculateDomainRegionRmsd(string centerChain, string[] compChains, Dictionary<string, string> chainUnpDict, int[][] protSeqRanges, StreamWriter rmsdWriter)
        {
            string pymolScriptFile = "";
            string pymolCoordFile = "";

            string[] chains = new string[compChains.Length + 1];
            chains[0] = centerChain;
            Array.Copy(compChains, 0, chains, 1, compChains.Length);

            Dictionary<int, int> centerChainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(centerChain.Substring(0, 4), centerChain.Substring(4, centerChain.Length - 4), chainUnpDict[centerChain]);
            Dictionary<string, Dictionary<int, int>> chainDbPdbSeqMatchDict = new Dictionary<string, Dictionary<int, int>>();
            chainDbPdbSeqMatchDict.Add(centerChain, centerChainDbSeqIdDict);

            List<string> chainListInPmlScript = new List<string>();
            chainListInPmlScript.Add(centerChain);
            foreach (string compChain in compChains)
            {
                try
                {
                    Dictionary<int, int> chainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(compChain.Substring(0, 4), compChain.Substring(4, compChain.Length - 4), chainUnpDict[compChain]);
                    chainDbPdbSeqMatchDict.Add(compChain, chainDbSeqIdDict);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }

            Dictionary<string, int[]>[]  chainSeqRangeDicts = new Dictionary<string,int[]>[protSeqRanges.Length];
            for (int i = 0; i < protSeqRanges.Length; i++)
            {
                Dictionary<string, int[]> chainSeqRangeDict = GetChainSeqRanges(chains, protSeqRanges[i], chainDbPdbSeqMatchDict);
                chainSeqRangeDicts[i] = chainSeqRangeDict;
            }

            foreach (string compChain in compChains)
            {
                pymolScriptFile = Path.Combine(coordDataDir, centerChain + "_" + compChain + "_pairFit.pml");
                pymolCoordFile = Path.Combine(coordDataDir, centerChain + "_" + compChain + ".coord");
                try
                {
                    CalculateTwoRegionsRmsd(centerChain, compChain, centerChainDbSeqIdDict, chainDbPdbSeqMatchDict[compChain], chainSeqRangeDicts, pymolScriptFile, pymolCoordFile, rmsdWriter);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(centerChain + " " + compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerChain"></param>
        /// <param name="compChain"></param>
        /// <param name="centerChainDbSeqIdDict"></param>
        /// <param name="chainDbSeqIdDict"></param>
        /// <param name="chainSeqRangeDict"></param>
        /// <param name="pymolScriptFile"></param>
        /// <param name="pymolCoordFile"></param>
        /// <param name="rmsdWriter"></param>
        private void CalculateTwoRegionsRmsd(string centerChain, string compChain, Dictionary<int, int> centerChainDbSeqIdDict, Dictionary<int, int> chainDbSeqIdDict, 
            Dictionary<string, int[]> chainSeqRangeDict, string pymolScriptFile, string pymolCoordFile, StreamWriter rmsdWriter)
        {
            GenerateAlignCoordinateFile(centerChain, compChain, pymolScriptFile, pymolCoordFile);

            string[] twoChains = new string[2];
            twoChains[0] = centerChain;
            twoChains[1] = compChain;
                 
            try
            {
                pymolLauncher.RunPymol(pymolScriptFile);

                CalculateRmsdFromPymolCoordOutput(pymolCoordFile, twoChains, chainSeqRangeDict, rmsdWriter);

      //          File.Delete(pymolScriptFile);
                File.Delete(pymolCoordFile);
            }
            catch (Exception ex)
            {
                logWriter.WriteLine("Calculating rmsd error: " + centerChain + " " + ex.Message);
                logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerChain"></param>
        /// <param name="compChain"></param>
        /// <param name="pymolScriptFile"></param>
        /// <param name="pymolCoordFile"></param>
        private void GenerateAlignCoordinateFile (string centerChain, string compChain, string pymolScriptFile, string pymolCoordFile)
        {
            StreamWriter pairFitScriptWriter = new StreamWriter(pymolScriptFile);

            string centerChainFile = centerChain + ".pdb";
            string scriptLine = "load " + centerChainFile;
            pairFitScriptWriter.WriteLine(scriptLine);

            string compChainFile = compChain + ".pdb";
            //            scriptLine = domainAlignPmlScript.FormatDomainPairFitPymolScript(compChain, centerChain, centerChainDbSeqIdDict, chainDbSeqIdDict);
            //           scriptLine = "align " + compChain + ", " + centerChain;
            scriptLine = "alignto " + centerChain;
            pairFitScriptWriter.WriteLine("load " + compChainFile);
            pairFitScriptWriter.WriteLine(scriptLine);

            pairFitScriptWriter.WriteLine("center " + centerChain);

            string coordFileLinux = pymolCoordFile.Replace("\\", "/");
            pairFitScriptWriter.WriteLine("cmd.save (\"" + coordFileLinux + "\")");
            pairFitScriptWriter.WriteLine("quit");
            pairFitScriptWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerChain"></param>
        /// <param name="compChain"></param>
        /// <param name="centerChainDbSeqIdDict"></param>
        /// <param name="chainDbSeqIdDict"></param>
        /// <param name="chainSeqRangeDict"></param>
        /// <param name="pymolScriptFile"></param>
        /// <param name="pymolCoordFile"></param>
        /// <param name="rmsdWriter"></param>
        private void CalculateTwoRegionsRmsd(string centerChain, string compChain, Dictionary<int, int> centerChainDbSeqIdDict, Dictionary<int, int> chainDbSeqIdDict,
            Dictionary<string, int[]>[] chainSeqRangeDicts, string pymolScriptFile, string pymolCoordFile, StreamWriter rmsdWriter)
        {
           
            GenerateAlignCoordinateFile (centerChain, compChain, pymolScriptFile, pymolCoordFile);
            string[] twoChains = new string[2];
            twoChains[0] = centerChain;
            twoChains[1] = compChain;

            try
            {
                pymolLauncher.RunPymol(pymolScriptFile);

                CalculateRmsdFromPymolCoordOutput(pymolCoordFile, twoChains, chainSeqRangeDicts, rmsdWriter);

                //          File.Delete(pymolScriptFile);
                File.Delete(pymolCoordFile);
            }
            catch (Exception ex)
            {
                logWriter.WriteLine("Calculating rmsd error: " + centerChain + " " + ex.Message);
                logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="dbSeqRange"></param>
        /// <param name="chainDbPdbSeqMatchDict"></param>
        /// <returns></returns>
        private Dictionary<string, int[]> GetChainSeqRanges (string[] chains, int[] dbSeqRange, Dictionary<string, Dictionary<int, int>> chainDbPdbSeqMatchDict)
        {
            Dictionary<string, int[]> chainSeqRangeDict = new Dictionary<string, int[]>();
            foreach (string chain in chains)
            {
                if (chainDbPdbSeqMatchDict.ContainsKey(chain))
                {
                    try
                    {
                        int[] seqRange = GetDomainSeqRangeMatchToProtein(dbSeqRange, chainDbPdbSeqMatchDict[chain]);
                        chainSeqRangeDict.Add(chain, seqRange);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine(chain + " error: " + ex.Message);
                        logWriter.Flush();
                    }
                }
            }
            return chainSeqRangeDict;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="protSeqRange"></param>
        /// <param name="dbPdbSeqMatchDict"></param>
        /// <returns></returns>
        private int[] GetDomainSeqRangeMatchToProtein(int[] protSeqRange, Dictionary<int, int> dbPdbSeqMatchDict)
        {
            int[] pdbSeqRange = new int[2];
            int seqStart = protSeqRange[0];
            while (!dbPdbSeqMatchDict.ContainsKey(seqStart) && seqStart < protSeqRange[1])
            {
                seqStart++;
            }
            if (dbPdbSeqMatchDict.ContainsKey(seqStart))
            {
                pdbSeqRange[0] = dbPdbSeqMatchDict[seqStart];
            }
            int seqEnd = protSeqRange[1];
            while (!dbPdbSeqMatchDict.ContainsKey(seqEnd) && seqEnd > protSeqRange[0])
            {
                seqEnd--;
            }
            if (dbPdbSeqMatchDict.ContainsKey(seqEnd))
            {
                pdbSeqRange[1] = dbPdbSeqMatchDict[seqEnd];
            }
            return pdbSeqRange;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbDBSeqMatchDict"></param>
        /// <returns></returns>
        private Dictionary<int, int> ReversePdbDbSeqMatchDict (Dictionary<int, int> pdbDBSeqMatchDict)
        {
            Dictionary<int, int> dbPdbSeqMatchDict = new Dictionary<int, int>();
            foreach (int pdbSeqId in pdbDBSeqMatchDict.Keys)
            {
                if (!dbPdbSeqMatchDict.ContainsKey(pdbDBSeqMatchDict[pdbSeqId]))
                {
                    dbPdbSeqMatchDict.Add(pdbDBSeqMatchDict[pdbSeqId], pdbSeqId);
                }
            }
            return dbPdbSeqMatchDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="domainRanges"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        private string[] FormatDomainPymolScript(string domainFile, Range[] domainRanges, out string domainResidueRangeString)
        {
            domainResidueRangeString = "";

            string domainPymolChainScript = "";
            string domainPymolScript = "";
            string domainFileName = domainFile + ".pfam";

            string domainRangeString = domainAlignPmlScript.FormatDomainRanges(domainRanges);
            domainResidueRangeString = " and chain A and resi " + domainRangeString;

            domainPymolChainScript = ("load " + domainFileName + "\r\n");
            domainPymolChainScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolChainScript += ("show cartoon, " + domainFileName + " and chain A\r\n");
            domainPymolChainScript += ("color white,  " + domainFileName + " and chain A \r\n");
            domainPymolChainScript += ("spectrum count, rainbow, " + domainFileName + domainResidueRangeString + "\r\n");  // rainbow the domain region

            domainPymolScript = ("load " + domainFileName + "\r\n");
            domainPymolScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolScript += ("show cartoon, " + domainFileName + " and chain A\r\n"); 
            domainPymolScript += ("hide cartoon, " + domainFileName + " and chain A and not resi " + domainRangeString + "\r\n");
            domainPymolScript += ("spectrum count, rainbow, " + domainFileName + domainResidueRangeString + "\r\n");  // rainbow the domain region

            string[] domainPymolScripts = new string[2];
            domainPymolScripts[0] = domainPymolChainScript;
            domainPymolScripts[1] = domainPymolScript;
            return domainPymolScripts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public Dictionary<int, int> MatchChainUnpNumbersToPdbNumbers (string pdbId, string chainId,  string uniprot)
        {
            int entityId = 0;
            string[] pdbSeqNumbers = GetChainPdbNumbers(pdbId, chainId, out entityId);
            string[] unpNumbers = seqNumMatch.ConvertPdbAuthorSeqNumbersToUnpNumbers (pdbId, entityId, uniprot, pdbSeqNumbers);
            Dictionary<int, int> dbPdbNumberMatchDict = new Dictionary<int,int> ();
            int pdbSeqNum = 0;
            int unpSeqNum = 0;
            for (int i = 0; i < pdbSeqNumbers.Length; i ++)
            {
                if (Int32.TryParse (pdbSeqNumbers[i], out pdbSeqNum) && Int32.TryParse (unpNumbers[i], out unpSeqNum))
                {
                    if (unpSeqNum <= 0)
                    {
                        continue;
                    }
                    if (!dbPdbNumberMatchDict.ContainsKey(unpSeqNum))
                    {
                        dbPdbNumberMatchDict.Add(unpSeqNum, pdbSeqNum);
                    }
                }
            }
            return dbPdbNumberMatchDict;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetChainPdbNumbers (string pdbId, string authChain, out int entityId)
        {
            string queryString = string.Format("Select First 1 EntityID, PdbSeqNumbers From AsymUnit Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, authChain);
            DataTable pdbSeqNumTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (pdbSeqNumTable.Rows.Count == 0 && authChain == "A")
            {
                queryString = string.Format("Select First 1 EntityID, PdbSeqNumbers From AsymUnit Where PdbID = '{0}' AND AuthorChain = '_' AND PolymerType = 'polypeptide';", pdbId);
                pdbSeqNumTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string pdbSeqNumString = "";
            entityId = -1;
            if (pdbSeqNumTable.Rows.Count > 0)
            {
                pdbSeqNumString = pdbSeqNumTable.Rows[0]["PdbSeqNumbers"].ToString();
                entityId = Convert.ToInt32 (pdbSeqNumTable.Rows[0]["EntityID"].ToString ());
            }
            return pdbSeqNumString.Split(',');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rmsdPymolScriptFile"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void CalculateRmsdFromPymolCoordOutput(string pymolCoordinateFile, string[] domains, Dictionary<string, int[]> domainSeqRangeDict, StreamWriter dataWriter)
        {
            ChainAtoms[] domainsCoordPml =  ReadCoordinates (pymolCoordinateFile);
            double rmsd = 0;
            double[] interfaceRmsds = new double[4];
            string dataLine = "";
            Coordinate[] centerRegionBb = GetRegionCoordinates(domainsCoordPml[0].BackboneAtoms(), domainSeqRangeDict[domains[0]]);
            for (int i = 1; i < domains.Length; i++)
            {
                // rmsd for the region
                Coordinate[] comRegionBb = GetRegionCoordinates(domainsCoordPml[i].BackboneAtoms(), domainSeqRangeDict[domains[i]]);
                rmsd = rmsdCal.CalculateRmsd(centerRegionBb, comRegionBb);
                dataLine = domains[0] + "\t" + domains[i] + "\t" + rmsd.ToString();          

                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rmsdPymolScriptFile"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void CalculateRmsdFromPymolCoordOutput(string pymolCoordinateFile, string[] domains, Dictionary<string, int[]>[] domainSeqRangeDicts, StreamWriter dataWriter)
        {
            ChainAtoms[] domainsCoordPml = ReadCoordinates(pymolCoordinateFile);
            double rmsd = 0;
            double[] interfaceRmsds = new double[4];
            string dataLine = "";
            for (int i = 1; i < domains.Length; i++)
            {
                dataLine = domains[0] + "\t" + domains[i] + "\t";
                foreach (Dictionary<string, int[]> domainSeqRangeDict in domainSeqRangeDicts)
                {
                    Coordinate[] centerRegionBb = GetRegionCoordinates(domainsCoordPml[0].BackboneAtoms(), domainSeqRangeDict[domains[0]]);
                    // rmsd for the region
                    Coordinate[] comRegionBb = GetRegionCoordinates(domainsCoordPml[i].BackboneAtoms(), domainSeqRangeDict[domains[i]]);
                    rmsd = rmsdCal.CalculateRmsd(centerRegionBb, comRegionBb);
                    dataLine += (rmsd.ToString() + "\t");                   
                }
                dataWriter.WriteLine(dataLine.TrimEnd ('\t'));
                dataWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <returns></returns>
        private ChainAtoms[] ReadCoordinates(string coordinateFile)
        {
            List<ChainAtoms> domainList = new List<ChainAtoms>();
            StreamReader dataReader = new StreamReader(coordinateFile);
            string line = "";
            string chainId = "";
            ChainAtoms oneChain = null;
            string chainEnd = "TER   ";
            int residueSeqId = 0;
            int nextResidueSeqId = 0;
            List<AtomInfo> atomList = new List<AtomInfo>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.Length < chainEnd.Length)
                {
                    continue;
                }

                if (line.Substring(0, chainEnd.Length) == chainEnd)
                {
                    string[] terFields = ParseHelper.ParsePdbTerLine(line);
                    Int32.TryParse(terFields[6], out residueSeqId);

                    line = dataReader.ReadLine(); // next line
                    if (line.IndexOf("ATOM  ") >= 0)
                    {
                        string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                        Int32.TryParse(atomFields[6], out nextResidueSeqId);
                        if (nextResidueSeqId < residueSeqId)
                        {
                            oneChain = new ChainAtoms();
                            oneChain.AsymChain = chainId;
                            oneChain.CartnAtoms = atomList.ToArray();
                            atomList = new List<AtomInfo>();
                            domainList.Add(oneChain);
                        }
                        else
                        {
                            AtomInfo atom = new AtomInfo();
                            atom.atomId = Convert.ToInt32(atomFields[1]);
                            atom.atomName = atomFields[2];
                            atom.residue = atomFields[4];
                            atom.seqId = atomFields[6];
                            atom.xyz.X = Convert.ToDouble(atomFields[8]);
                            atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                            atom.xyz.Z = Convert.ToDouble(atomFields[10]);
                            atomList.Add(atom);
                            chainId = atomFields[5];
                        }
                    }
                    else
                    {
                        oneChain = new ChainAtoms();
                        oneChain.AsymChain = chainId;
                        oneChain.CartnAtoms = atomList.ToArray();
                        atomList = new List<AtomInfo>();
                        domainList.Add(oneChain);
                    }
                    continue;
                }

                if (line.IndexOf("ATOM  ") >= 0)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    AtomInfo atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);
                    atomList.Add(atom);
                    chainId = atomFields[5];

                }
            }
            dataReader.Close();
            if (atomList.Count > 0)
            {
                oneChain = new ChainAtoms();
                oneChain.AsymChain = chainId;
                oneChain.CartnAtoms = atomList.ToArray();
                domainList.Add(oneChain);
            }
            return domainList.ToArray();
        }
        #endregion

        #region RMSD from theseus
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="compDomains"></param>
        /// <param name="seqRange"></param>
        public void CalculateDomainRegionRmsdFromTheseus(string centerChain, string[] compChains, Dictionary<string, string> chainUnpDict, int[] protSeqRange, StreamWriter rmsdWriter)
        {
            string[] chains = new string[compChains.Length + 1];
            chains[0] = centerChain;
            Array.Copy(compChains, 0, chains, 1, compChains.Length);

            Dictionary<int, int> centerChainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(centerChain.Substring(0, 4), centerChain.Substring(4, centerChain.Length - 4), chainUnpDict[centerChain]);
            Dictionary<string, Dictionary<int, int>> chainDbPdbSeqMatchDict = new Dictionary<string, Dictionary<int, int>>();
            chainDbPdbSeqMatchDict.Add(centerChain, centerChainDbSeqIdDict);

            List<string> chainListInPmlScript = new List<string>();
            chainListInPmlScript.Add(centerChain);
            foreach (string compChain in compChains)
            {
                try
                {
                    Dictionary<int, int> chainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(compChain.Substring(0, 4), compChain.Substring(4, compChain.Length - 4), chainUnpDict[compChain]);
                    chainDbPdbSeqMatchDict.Add(compChain, chainDbSeqIdDict);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }

            Dictionary<string, int[]> chainSeqRangeDict = GetChainSeqRanges(chains, protSeqRange, chainDbPdbSeqMatchDict);

            foreach (string compChain in compChains)
            {
               
                try
                {
                    CalculateTwoRegionsRmsd(centerChain, compChain, centerChainDbSeqIdDict, chainDbPdbSeqMatchDict[compChain], chainSeqRangeDict, rmsdWriter);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(centerChain + " " + compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerChain"></param>
        /// <param name="compChain"></param>
        /// <param name="centerChainDbSeqIdDict"></param>
        /// <param name="chainDbSeqIdDict"></param>
        /// <param name="chainSeqRangeDict"></param>
        /// <param name="pymolScriptFile"></param>
        /// <param name="pymolCoordFile"></param>
        /// <param name="rmsdWriter"></param>
        private void CalculateTwoRegionsRmsd(string centerChain, string compChain, Dictionary<int, int> centerChainDbSeqIdDict, Dictionary<int, int> chainDbSeqIdDict,
            Dictionary<string, int[]> chainSeqRangeDict, StreamWriter rmsdWriter)
        {  
            try
            {
                ChainAtoms centerChainCoord = ReadCoordinatesFromTheseusFile(Path.Combine(coordDataDir, "theseus_" + centerChain + ".pdb"));
                ChainAtoms compChainCoord = ReadCoordinatesFromTheseusFile(Path.Combine (coordDataDir, "theseus_" + compChain + ".pdb"));
                CalculateRmsd (centerChain, compChain, centerChainCoord, compChainCoord,  chainSeqRangeDict, rmsdWriter);
            }
            catch (Exception ex)
            {
                logWriter.WriteLine("Calculating rmsd error: " + centerChain + " " + ex.Message);
                logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        private ChainAtoms ReadCoordinatesFromTheseusFile(string coordFile)
        {
            StreamReader pdbReader = new StreamReader(coordFile);
            string line = "";
            string chainId = "";
            List<AtomInfo> atomList = new List<AtomInfo>();
            while ((line = pdbReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") >= 0)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    AtomInfo atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(atomFields[1]);
                    atom.atomName = atomFields[2];
                    atom.residue = atomFields[4];
                    atom.seqId = atomFields[6];
                    atom.xyz.X = Convert.ToDouble(atomFields[8]);
                    atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                    atom.xyz.Z = Convert.ToDouble(atomFields[10]);
                    atomList.Add(atom);
                    chainId = atomFields[5];
                }               
            }
            pdbReader.Close();

            ChainAtoms theChain = new ChainAtoms();
            theChain.AsymChain = chainId;
            theChain.CartnAtoms = atomList.ToArray();
            return theChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rmsdPymolScriptFile"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void CalculateRmsd (string centerStruct, string compStruct, ChainAtoms centerChainCoord, ChainAtoms compChainCoord, Dictionary<string, int[]> domainSeqRangeDict, StreamWriter dataWriter)
        {
            double rmsd = 0;
            double[] interfaceRmsds = new double[4];
            string dataLine = "";
            Coordinate[] centerRegionBb = GetRegionCoordinates(centerChainCoord.BackboneAtoms(), domainSeqRangeDict[centerStruct]);
            // rmsd for the region
            Coordinate[] comRegionBb = GetRegionCoordinates(compChainCoord.BackboneAtoms(), domainSeqRangeDict[compStruct]);
            rmsd = rmsdCal.CalculateRmsd(centerRegionBb, comRegionBb);
            dataLine = centerStruct + "\t" + compStruct + "\t" + rmsd.ToString();

            dataWriter.WriteLine(dataLine);
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="compDomains"></param>
        /// <param name="seqRange"></param>
        public void CalculateDomainRegionRmsdFromTheseus(string centerChain, string[] compChains, string dataType, Dictionary<string, string> chainUnpDict, int[][] protSeqRanges, StreamWriter rmsdWriter)
        {          
            string[] chains = new string[compChains.Length + 1];
            chains[0] = centerChain;
            Array.Copy(compChains, 0, chains, 1, compChains.Length);

            Dictionary<int, int> centerChainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(centerChain.Substring(0, 4), centerChain.Substring(4, centerChain.Length - 4), chainUnpDict[centerChain]);
            Dictionary<string, Dictionary<int, int>> chainDbPdbSeqMatchDict = new Dictionary<string, Dictionary<int, int>>();
            chainDbPdbSeqMatchDict.Add(centerChain, centerChainDbSeqIdDict);

            foreach (string compChain in compChains)
            {
                try
                {
                    // key:unp value:pdb
                    Dictionary<int, int> chainDbSeqIdDict = MatchChainUnpNumbersToPdbNumbers(compChain.Substring(0, 4), compChain.Substring(4, compChain.Length - 4), chainUnpDict[compChain]);
                    chainDbPdbSeqMatchDict.Add(compChain, chainDbSeqIdDict);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }

            Dictionary<string, int[]>[] chainSeqRangeDicts = new Dictionary<string, int[]>[protSeqRanges.Length];
            for (int i = 0; i < protSeqRanges.Length; i++)
            {
                Dictionary<string, int[]> chainSeqRangeDict = GetChainSeqRanges(chains, protSeqRanges[i], chainDbPdbSeqMatchDict);
                chainSeqRangeDicts[i] = chainSeqRangeDict;
            }
            ChainAtoms centerChainCoord = ReadCoordinatesFromTheseusFile(Path.Combine(coordDataDir, dataType +  "_" + centerChain + ".pdb"));
            foreach (string compChain in compChains)
            {
                try
                {
                    ChainAtoms compChainCoord = ReadCoordinatesFromTheseusFile(Path.Combine(coordDataDir, dataType + "_" + compChain + ".pdb"));
                    CalculateRmsd(centerChain, compChain, centerChainCoord, compChainCoord, chainSeqRangeDicts, rmsdWriter);                    
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(centerChain + " " + compChain + " error: " + ex.Message);
                    logWriter.Flush();
                }
            }
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rmsdPymolScriptFile"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <param name="dataWriter"></param>
        private void CalculateRmsd (string centerStruct, string compStruct, ChainAtoms centerChainCoord, ChainAtoms compChainCoord, Dictionary<string, int[]>[] domainSeqRangeDicts, StreamWriter dataWriter)
        {
            double rmsd = 0;
            double[] interfaceRmsds = new double[4];
            string dataLine = centerStruct + "\t" + compStruct ;
            string compChainInfo = GetChainInfo(compStruct);
            string crystInfo = entryCrystInfoDict[compStruct.Substring (0, 4)];
            foreach (Dictionary<string, int[]> domainSeqRangeDict in domainSeqRangeDicts)
            {
                Coordinate[] centerRegionBb = GetRegionCoordinates(centerChainCoord.BackboneAtoms(), domainSeqRangeDict[centerStruct]);
                // rmsd for the region
                Coordinate[] comRegionBb = GetRegionCoordinates(compChainCoord.BackboneAtoms(), domainSeqRangeDict[compStruct]);
                rmsd = rmsdCal.CalculateRmsd(centerRegionBb, comRegionBb);
                dataLine += ("\t" + rmsd.ToString()); 
            }

            dataWriter.WriteLine(dataLine + "\t" + compChainInfo + "\t" + crystInfo);
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="compChain"></param>
        /// <returns></returns>
        private string GetChainInfo (string aChain)
        {
            string chainInfo = "";
            string pdbId = aChain.Substring(0, 4);
            if (chainMutationDict.ContainsKey(aChain))
            {
                chainInfo += (chainMutationDict[aChain] + "\t");
            }
            else
            {
                chainInfo += "-\t";
            }
            if (entryPdbBaDict.ContainsKey (pdbId))
            {
                chainInfo += (entryPdbBaDict[pdbId] + "\t"); 
            }
            else
            {
                chainInfo += "-\t";
            }
            if (entryUnpsDict.ContainsKey (pdbId))
            {
                chainInfo += (entryUnpsDict[pdbId] + "\t");
            }
            else
            {
                chainInfo += "-\t";
            }
            if (entryLigandsDict.ContainsKey(pdbId))
            {
                chainInfo += (entryLigandsDict[pdbId] + "\t");
            }
            else
            {
                chainInfo += "-\t";
            }
            return chainInfo;
        }
        #endregion

        #region region coordinates
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private Coordinate[] GetRegionCoordinates(AtomInfo[] chain, int[] seqRange, string[] atomNames)
        {
            List<Coordinate> coordinateList = new List<Coordinate>();
            int seqId = 0;
            foreach (AtomInfo atom in chain)
            {
                if (atomNames.Contains(atom.atomName))
                {
                    seqId = Convert.ToInt32(atom.seqId);
                    if (seqId <= seqRange[1] && seqId >= seqRange[0])
                    {
                        coordinateList.Add(atom.xyz);
                    }
                }
            }
            return coordinateList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private Coordinate[] GetRegionCoordinates(AtomInfo[] chain, int[] seqRange)
        {
            List<Coordinate> coordinateList = new List<Coordinate>();
            int seqId = 0;
            foreach (AtomInfo atom in chain)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (seqId <= seqRange[1] && seqId >= seqRange[0])
                {
                    coordinateList.Add(atom.xyz);
                }
            }
            return coordinateList.ToArray();
        }
        #endregion

        #region pdb chains coordinate files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="proteinName"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetProteinChains(string proteinName, out Dictionary<string, string> chainUnpDict)
        {
            string queryString = string.Format("Select AsymUnit.PdbID, AsymID, AuthorChain, DbCode From AsymUnit, PdbDbRefSifts " +
                " Where DbCode Like '{0}%' AND AsymUnit.PdbID = PdbDbRefSifts.PdbID AND AsymUnit.EntityID = PdbDbRefSifts.EntityID;", proteinName);
            DataTable proteinChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, List<string>> protEntryChainsDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string chainId = "";
            chainUnpDict = new Dictionary<string, string>();
            foreach (DataRow chainRow in proteinChainTable.Rows)
            {
                pdbId = chainRow["PdbID"].ToString();
                chainId = chainRow["AuthorChain"].ToString().TrimEnd();
                if (chainId == "-" || chainId == "_")
                {
                    chainId = "A";
                }
                if (protEntryChainsDict.ContainsKey (pdbId))
                {
                    protEntryChainsDict[pdbId].Add(chainId);
                }
                else
                {
                    List<string> chainList = new List<string>();
                    chainList.Add(chainId);
                    protEntryChainsDict.Add(pdbId, chainList);
                }
                if (!chainUnpDict.ContainsKey(pdbId + chainId))
                {
                    chainUnpDict.Add(pdbId + chainId, chainRow["DbCode"].ToString().TrimEnd());
                }
            }
            List<string> siftsEntryList = new List<string>(protEntryChainsDict.Keys);
            queryString = string.Format("Select AsymUnit.PdbID, AsymID, AuthorChain, DbCode From AsymUnit, PdbDbRefXml " +
                " Where DbCode Like '{0}%' AND AsymUnit.PdbID = PdbDbRefXml.PdbID AND AsymUnit.EntityID = PdbDbRefXml.EntityID;", proteinName);
            proteinChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow chainRow in proteinChainTable.Rows)
            {
                pdbId = chainRow["PdbID"].ToString();
                chainId = chainRow["AuthorChain"].ToString().TrimEnd();

               if (siftsEntryList.Contains (pdbId))
               {
                   continue;
               }
               if (protEntryChainsDict.ContainsKey(pdbId))
               {
                   protEntryChainsDict[pdbId].Add(chainId);
               }
               else
               {
                   List<string> chainList = new List<string>();
                   chainList.Add(chainId);
                   protEntryChainsDict.Add(pdbId, chainList);
               }
               if (!chainUnpDict.ContainsKey(pdbId + chainId))
               {
                   chainUnpDict.Add(pdbId + chainId, chainRow["DbCode"].ToString().TrimEnd());
               }
            }
            return protEntryChainsDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        public void DownloadPdbFiles (string[] pdbIds)
        {
            string fileUri = "";
            string localFile = "";
            foreach (string pdbId in pdbIds)
            {
                fileUri = rcsbServerAddress + pdbId + ".pdb";
                localFile = Path.Combine(coordDataDir, pdbId + ".pdb");
                if (File.Exists (localFile))
                {
                    continue;
                }
                try
                {
                    downloadClient.DownloadFile(fileUri, localFile);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Download PDB file error: " + pdbId);
                    logWriter.Flush();
                }
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainAtomLineListDict"></param>
        /// <param name="chainPdbDbSeqMatchDict"></param>
        public void WriteChainFileWithDbSeqNumbers (string pdbId, Dictionary<string, List<string>> chainAtomLineListDict, Dictionary<string, Dictionary<string, string>> chainPdbDbSeqMatchDict)
        {
            string chainCoordFile = "";
            string residueSeqId = "";
            string newAtomLine = "";
            foreach (string chainId in chainAtomLineListDict.Keys)
            {
                Dictionary<string, string> seqNumberMatch = chainPdbDbSeqMatchDict[chainId];
                chainCoordFile = Path.Combine(coordDataDir, pdbId + chainId + ".pdb");
                StreamWriter chainWriter = new StreamWriter(chainCoordFile);
                foreach (string atomLine in chainAtomLineListDict[chainId])
                {
                    // residue sequence id
                    residueSeqId = atomLine.Substring(22, 4).Trim();  // 6
                    if (seqNumberMatch.ContainsKey(residueSeqId))
                    {
                        newAtomLine = atomLine.Remove(22, 4);
                        newAtomLine = newAtomLine.Insert(22, seqNumberMatch[residueSeqId]);
                    }
                    else
                    {
                        newAtomLine = atomLine; // should rename the sequence number based on pdb-db sequence number match!!
                    }
                    chainWriter.WriteLine(newAtomLine);
                }
                chainWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainAtomLineListDict"></param>
        public string[] WriteChainFiles (string pdbId, Dictionary<string, List<string>> chainAtomLineListDict)
        {
            string chainCoordFile = "";
            List<string> chainFileNameList = new List<string>();
            foreach (string chainId in chainAtomLineListDict.Keys)
            {
                chainCoordFile = Path.Combine(coordDataDir, pdbId + chainId + ".pdb");
                chainFileNameList.Add(pdbId + chainId);
                StreamWriter chainWriter = new StreamWriter(chainCoordFile);
                foreach (string atomLine in chainAtomLineListDict[chainId])
                {                   
                    chainWriter.WriteLine(atomLine);
                }
                chainWriter.Close();
            }
            return chainFileNameList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbFile"></param>
        /// <param name="authorChains"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> ReadProteinChainFiles(string pdbFile, string[] authorChains)
        {          
            StreamReader pdbReader = new StreamReader(pdbFile);
            string line = "";
            string chainId = "";
            Dictionary<string, List<string>> chainAtomLineListDict = new Dictionary<string,List<string>> ();
            while ((line = pdbReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("ENDMDL") > -1)  // end of the model 1
                {
                    break;
                }
                if (line.IndexOf ("ATOM  ") > -1/* || line.IndexOf ("HETATM") > -1*/)
                {
                    string[] fields = ParseHelper.ParsePdbAtomLine(line);
                    chainId = fields[5];
                    if (chainAtomLineListDict.ContainsKey (chainId))
                    {
                        chainAtomLineListDict[chainId].Add(line);
                    }
                    else
                    {
                        List<string> lineList = new List<string>();
                        lineList.Add(line);
                        chainAtomLineListDict.Add(chainId, lineList);
                    }
                }
                else if (line.IndexOf ("TER   ") > -1)
                {
                    string[] fields = ParseHelper.ParsePdbTerLine(line);
                    chainId = fields[5];
                    if (chainAtomLineListDict.ContainsKey(chainId))
                    {
                        chainAtomLineListDict[chainId].Add(line);
                    }                  
                }
            }
            pdbReader.Close();
            List<string> entryChainList = new List<string>(chainAtomLineListDict.Keys);
            foreach (string entryChain in entryChainList)
            {
                if (! authorChains.Contains (entryChain))
                {
                    chainAtomLineListDict.Remove(entryChain);
                }
            }
            return chainAtomLineListDict;
        }
        #endregion

        #region chain mutations, entry info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetChainMutationsDict (string[] chains)
        {
            Dictionary<string, string> chainMutationsDict = new Dictionary<string, string>();
            foreach (string chain in chains)
            {
                string[] mutations = GetChainMutations(chain.Substring(0, 4), chain.Substring(4, chain.Length - 4));
                if (mutations.Length > 0)
                {
                    chainMutationsDict.Add(chain, ParseHelper.FormatArrayString(mutations, ','));
                }
            }
            return chainMutationsDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domains"></param>
        /// <returns></returns>
        private string[] GetChainMutations(string pdbId, string authorChain)
        {
            string queryString = string.Format("Select DbResidue, Residue, AuthorSeqNum, AuthorChain, Details From PdbDbRefSeqDifXml " +
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND Lower(details) like 'engineered%';", pdbId, authorChain);
            DataTable mutationTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> mutResList = new List<string>();
            string mutRes = "";
            foreach (DataRow mutRow in mutationTable.Rows)
            {
                mutRes = mutRow["DbResidue"].ToString().TrimEnd() + mutRow["AuthorSeqNum"].ToString().TrimEnd() + mutRow["Residue"].ToString().TrimEnd();
                if (!mutResList.Contains(mutRes))
                {
                    mutResList.Add(mutRes);
                }
            }
            return mutResList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <returns></returns>
        private string[] GetEntryList (string[] chains)
        {
            List<string> entryList = new List<string>();
            foreach (string chain in chains)
            {
                if (! entryList.Contains (chain.Substring (0, 4)))
                {
                    entryList.Add(chain.Substring(0, 4));
                }
            }
            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private Dictionary<string,string > GetEntryUniprotsDict (string[] pdbIds)
        {
            Dictionary<string, string> entryUniprotsDict = new Dictionary<string, string>();
            foreach (string pdbId in pdbIds)
            {
                string uniprots = GetEntryUniprots(pdbId);
                entryUniprotsDict.Add(pdbId, uniprots);
            }
            return entryUniprotsDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryUniprots (string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID, DbCode From PdbDbRefSifts Where PdbID = '{0}' AND DbName= 'UNP' Order By RefID;", pdbId);
            DataTable entityUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (entityUnpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct EntityID, DbCode From PdbDbRefXml Where PdbID = '{0}' AND DbName= 'UNP' Order By RefID;", pdbId);
                entityUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string entryUniprots = "";
            Dictionary<int, List<string>> entityUniprotsDict = new Dictionary<int, List<string>>();
            int entityId = 0;
            string uniprot = "";
            List<int> entityList = new List<int>();
            foreach (DataRow entityRow in entityUnpTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                uniprot = entityRow["DbCode"].ToString ().TrimEnd ();
                if (entityUniprotsDict.ContainsKey (entityId))
                {
                    if (! entityUniprotsDict[entityId].Contains (uniprot))
                    {
                        entityUniprotsDict[entityId].Add(uniprot);
                    }
                    else
                    {
                        entityUniprotsDict[entityId].Add(uniprot);
                    }
                }
                else
                {
                    List<string> uniprotList = new List<string>();
                    uniprotList.Add(uniprot);
                    entityUniprotsDict.Add(entityId, uniprotList);

                    entityList.Add(entityId);
                }
            }
            entityList.Sort();
            foreach (int lsEntityId in entityList)
            {
                entryUniprots += (ParseHelper.FormatArrayString(entityUniprotsDict[lsEntityId], '-') + ";");
            }
            entryUniprots = entryUniprots.TrimEnd (';');
            if (entryUniprots == "")
            {
                entryUniprots = "-";
            }
            return entryUniprots;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryFirstPdbBaDict (string[] pdbIds)
        {
            Dictionary<string, string> entryPdbBaDict = new Dictionary<string, string>();
            string pdbBa = "";
            foreach (string pdbId in pdbIds)
            {
                pdbBa = GetFirstPdbBA(pdbId);
                entryPdbBaDict.Add(pdbId, pdbBa);
            }
            return entryPdbBaDict;
        }
        /// <summary>
        /// /
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetFirstPdbBA (string pdbId)
        {
            string queryString = string.Format("Select ABCFormat From PDbBiolUnits Where PdbID = '{0}' AND BuID = '1';", pdbId);
            DataTable baTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (baTable.Rows.Count > 0)
            {
                return baTable.Rows[0]["ABCFormat"].ToString().TrimEnd();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryLigandsDict (string[] pdbIds)
        {
            Dictionary<string, string> entryLigandsDict = new Dictionary<string, string>();
            string ligands = "";
            foreach (string pdbId in pdbIds)
            {
                ligands = GetEntryLigands(pdbId);
                if (ligands != "")
                {
                    entryLigandsDict.Add(pdbId, ligands);
                }
            }
            return entryLigandsDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domains"></param>
        /// <returns></returns>
        private string GetEntryLigands(string pdbId)
        {
            string queryString = string.Format("Select Ligand, count(Distinct AsymChain) As ligandNum From PdbLigands Where PdbID = '{0}' Group By Ligand", pdbId);
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            string entryLigands = "";
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                entryLigands += (ligandRow["Ligand"].ToString().TrimEnd() + "(" + ligandRow["ligandNum"].ToString () + ");");
            }
            return entryLigands.TrimEnd (';');
        }  

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryCrystInfoDict (string[] pdbIds)
        {
            Dictionary<string, string> entryCrystInfoDict = new Dictionary<string, string>();
            string crystInfo = "";
            foreach (string pdbId in pdbIds)
            {
                crystInfo = GetEntryCrystalConditionInfo(pdbId);
                entryCrystInfoDict.Add(pdbId, crystInfo);
            }
            return entryCrystInfoDict;
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryCrystalConditionInfo (string pdbId)
        {
            string queryString = string.Format("Select Method, Resolution, SpaceGroup, CrystTemp, CrystPh From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable entryCrystTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crystInfo = "";
            if (entryCrystTable.Rows.Count > 0)
            {
                crystInfo = entryCrystTable.Rows[0]["CrystTemp"].ToString().TrimEnd() + "\t" +
                    entryCrystTable.Rows[0]["CrystPh"].ToString().TrimEnd() + "\t" +
                    entryCrystTable.Rows[0]["Resolution"].ToString().TrimEnd() + "\t" +
                    entryCrystTable.Rows[0]["Method"].ToString().TrimEnd() + "\t" +
                    entryCrystTable.Rows[0]["SpaceGroup"].ToString().TrimEnd();
            }
            if (crystInfo == "")
            {
                crystInfo = "-\t-\t-\t-\t-";
            }
            return crystInfo;
        }
        #endregion
    }
}
