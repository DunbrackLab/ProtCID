using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.PymolScript
{
    public class DomainAlignPymolScript
    {
        private DbQuery dbQuery = new DbQuery();

        #region format domain align pymol script file
       /// <summary>
       /// 
       /// </summary>
       /// <param name="pymolScripFileName"></param>
       /// <param name="coordDomains"></param>
       /// <param name="centerDomain"></param>
       /// <param name="dataDir"></param>
       /// <param name="domainRangesHash"></param>
       /// <param name="pfamDomainTable">the domain defintion table</param>
       /// <param name="domainCoordSeqIdsHash">for pair_fit, which needs exactly same numbers of atoms</param>
        /// <param name="interactingLigandsHash">All interacting chains</param>
       /// <param name="interactingDnaRnaChainsHash">The interacting DNA/RNA chains</param>
        /// <param name="ligandNameDomainChainHash">key: ligand, value: a list of chain-domains</param>
        /// <param name="clusterLigandsHash">ligand clusters</param>
       /// <returns></returns>
        public string[] FormatPymolScriptFile(string pymolScripFileName, string[] coordDomains, string centerDomain, string dataDir, Dictionary<string, Range[]> domainRangesHash,
             DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash, Dictionary<string, string[]> interactingLigandsHash, Dictionary<string, string[]> interactingDnaRnaChainsHash,
            Dictionary<string, string[]> ligandNameDomainChainHash, Dictionary<int, string[]> clusterLigandsHash, string pdbOrUnp)
        {
            string pymolScriptFileChainAlign = Path.Combine(dataDir, pymolScripFileName + "_byChain_" + pdbOrUnp + ".pml");  // display and align the whole chains
            string pymolScriptFilePairFit = Path.Combine(dataDir, pymolScripFileName + "_pairFit_" + pdbOrUnp + ".pml");     // display whole chains and pair_fit domain regions
            string pymolScriptFileDomainAlign = Path.Combine(dataDir, pymolScripFileName + "_byDomain_" + pdbOrUnp + ".pml");  // display and align domain regions only
            string pymolScriptFilePairFileDomain = Path.Combine(dataDir, pymolScripFileName + "_pairFitDomain_" + pdbOrUnp + ".pml");  // display and pair_fit domain regions only
            // display whole chain 
            StreamWriter scriptChainAlignWriter = new StreamWriter(pymolScriptFileChainAlign);
            StreamWriter scriptPairFitWriter = new StreamWriter(pymolScriptFilePairFit);
            // display the domain only
            StreamWriter scriptDomainAlignWriter = new StreamWriter(pymolScriptFileDomainAlign);
            StreamWriter scriptPairFitDomainWriter = new StreamWriter(pymolScriptFilePairFileDomain);

            Dictionary<int, int> centerDomainHmmSeqIdHash = GetSequenceHmmSeqIdHash(centerDomain, pfamDomainTable, domainCoordSeqIdsHash);

            Range[] centerDomainRanges = (Range[])domainRangesHash[centerDomain];
            string centerDomainResidueRangeString = "";
            string[] centerDomainInteractingLigands = new string[0];
            if (interactingLigandsHash.ContainsKey(centerDomain))
            {
                centerDomainInteractingLigands = (string[])interactingLigandsHash[centerDomain];
            }
            string[] centerDomainInteractingDna = new string[0];
            if (interactingDnaRnaChainsHash.ContainsKey(centerDomain))
            {
                centerDomainInteractingDna = (string[])interactingDnaRnaChainsHash[centerDomain];
            }
            string[] centerInterfaceScripLines = FormatDomainPymolScript(centerDomain, centerDomainRanges, centerDomainInteractingLigands, centerDomainInteractingDna, out centerDomainResidueRangeString);

            scriptChainAlignWriter.WriteLine("set ignore_case, 0");
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine("set ignore_case, 0");
            scriptPairFitWriter.WriteLine();

            scriptDomainAlignWriter.WriteLine("set ignore_case, 0");
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine("set ignore_case, 0");
            scriptPairFitDomainWriter.WriteLine();

            scriptChainAlignWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptPairFitWriter.WriteLine();


            scriptDomainAlignWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptPairFitDomainWriter.WriteLine();

            string[] domainInteractingLigands = new string[0];
            string[] domainInteractingDna = new string[0];
            foreach (string coordDomain in coordDomains)
            {
                if (coordDomain == centerDomain)
                {
                    continue;
                }
                domainInteractingLigands = new string[0];
                if (interactingLigandsHash.ContainsKey(coordDomain))
                {
                    domainInteractingLigands = (string[])interactingLigandsHash[coordDomain];
                }
                domainInteractingDna = new string[0];
                if (interactingDnaRnaChainsHash.ContainsKey(coordDomain))
                {
                    domainInteractingDna = (string[])interactingDnaRnaChainsHash[coordDomain];
                }
                
                Range[] domainRanges = domainRangesHash[coordDomain];
                string[] domainAlignScripts = FormatDomainPymolScript(coordDomain, domainRanges, centerDomain, centerDomainResidueRangeString, domainInteractingLigands,
                    domainInteractingDna, centerDomainHmmSeqIdHash, pfamDomainTable, domainCoordSeqIdsHash);

                scriptChainAlignWriter.WriteLine(domainAlignScripts[0]);
                scriptChainAlignWriter.WriteLine();

                scriptPairFitWriter.WriteLine(domainAlignScripts[1]);
                scriptPairFitWriter.WriteLine();

                scriptDomainAlignWriter.WriteLine(domainAlignScripts[2]);
                scriptDomainAlignWriter.WriteLine();

                scriptPairFitDomainWriter.WriteLine(domainAlignScripts[3]);
                scriptPairFitDomainWriter.WriteLine();
            }

            // for ligands and DNA/RNA display
            string seleLigandsString = FormatSelectLigandString(interactingLigandsHash, coordDomains);
            string seleDnaRnaString = FormatSelectDnaRnaString(interactingDnaRnaChainsHash, coordDomains);

            // for ligand clusters
            string seleClusterString = "";
            if (clusterLigandsHash != null)
            {
                seleClusterString = FormatClusterLigandString(clusterLigandsHash, coordDomains);
            }

            string hetScriptLines = "center " + centerDomain + ".pfam\n";
            hetScriptLines += "sele allhet, het\n";   // all ligands/DNA/RNA except protein chains
            hetScriptLines += "color white, allhet\n";
            hetScriptLines += "util.cnc allhet\n"; // color all het atoms by elements, while color carbon atoms by white
            hetScriptLines += "hide spheres, allhet\n";
            if (seleLigandsString != "")
            {
                hetScriptLines += (seleLigandsString + "\n");
                hetScriptLines += "show spheres, selectLigands\n";
            }
            if (seleDnaRnaString != "")
            {
                hetScriptLines += (seleDnaRnaString + "\n");
                hetScriptLines += "show cartoon, selectDnaRna\n";
            }
            if (seleClusterString != "")
            {
                hetScriptLines += (seleClusterString + "\n");
            }
            if (ligandNameDomainChainHash != null)
            {
                // add select for each ligand, so a user can look into a single ligand
                string[] singleLigandSelStrings = FormatSelectSingleLigandString(interactingLigandsHash, ligandNameDomainChainHash, coordDomains);
                foreach (string singleLigandSelString in singleLigandSelStrings)
                {
                    hetScriptLines += (singleLigandSelString + "\n");
                }
                string[] singleDnaRnaSelStrings = FormatSelectSingleLigandString(interactingDnaRnaChainsHash, ligandNameDomainChainHash, coordDomains);
                foreach (string singleDnaRnaSelString in singleDnaRnaSelStrings)
                {
                    hetScriptLines += (singleDnaRnaSelString + "\n");
                }
            }
            if (hetScriptLines != "")
            {
                scriptChainAlignWriter.WriteLine(hetScriptLines);
                scriptPairFitWriter.WriteLine(hetScriptLines);
                scriptDomainAlignWriter.WriteLine(hetScriptLines);
                scriptPairFitDomainWriter.WriteLine(hetScriptLines);
            }
            scriptChainAlignWriter.Close();
            scriptPairFitWriter.Close();
            scriptDomainAlignWriter.Close();
            scriptPairFitDomainWriter.Close();

            string[] scriptFiles = new string[4];
            scriptFiles[0] = pymolScripFileName + "_byChain_" + pdbOrUnp + ".pml";
            scriptFiles[1] = pymolScripFileName + "_pairFit_" + pdbOrUnp + ".pml";
            scriptFiles[2] = pymolScripFileName + "_byDomain_" + pdbOrUnp + ".pml";
            scriptFiles[3] = pymolScripFileName + "_pairFitDomain_" + pdbOrUnp + ".pml";
            return scriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScripFileName"></param>
        /// <param name="coordDomains"></param>
        /// <param name="centerDomain"></param>
        /// <param name="dataDir"></param>
        /// <param name="domainRangesHash"></param>
        /// <param name="pfamDomainTable">the domain defintion table</param>
        /// <param name="domainCoordSeqIdsHash">for pair_fit, which needs exactly same numbers of atoms</param>
        /// <param name="interactingLigandsHash">All interacting chains</param>
        /// <param name="interactingDnaRnaChainsHash">The interacting DNA/RNA chains</param>
        /// <param name="ligandNameDomainChainHash">key: ligand, value: a list of chain-domains</param>
        /// <returns></returns>
        public string[] FormatPymolScriptFile(string pymolScripFileName, string[] coordDomains, string centerDomain,
            string dataDir, Dictionary<string, Range[]> domainRangesHash, DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash,
            Dictionary<string, string[]> interactingLigandsHash, Dictionary<string, string[]> interactingDnaRnaChainsHash, Dictionary<string, string[]> ligandNameDomainChainHash)
        {
            string pymolScriptFileChainAlign = Path.Combine(dataDir, pymolScripFileName + "_byChain.pml");  // display and align the whole chains
            string pymolScriptFilePairFit = Path.Combine(dataDir, pymolScripFileName + "_pairFit.pml");     // display whole chains and pair_fit domain regions
            string pymolScriptFileDomainAlign = Path.Combine(dataDir, pymolScripFileName + "_byDomain.pml");  // display and align domain regions only
            string pymolScriptFilePairFileDomain = Path.Combine(dataDir, pymolScripFileName + "_pairFitDomain.pml");  // display and pair_fit domain regions only
            // display whole chain 
            StreamWriter scriptChainAlignWriter = new StreamWriter(pymolScriptFileChainAlign);
            StreamWriter scriptPairFitWriter = new StreamWriter(pymolScriptFilePairFit);
            // display the domain only
            StreamWriter scriptDomainAlignWriter = new StreamWriter(pymolScriptFileDomainAlign);
            StreamWriter scriptPairFitDomainWriter = new StreamWriter(pymolScriptFilePairFileDomain);

            Dictionary<int, int> centerDomainHmmSeqIdHash = GetSequenceHmmSeqIdHash(centerDomain, pfamDomainTable, domainCoordSeqIdsHash);

            Range[] centerDomainRanges = (Range[])domainRangesHash[centerDomain];
            string centerDomainResidueRangeString = "";
            string[] centerDomainInteractingLigands = new string[0];
            if (interactingLigandsHash.ContainsKey(centerDomain))
            {
                centerDomainInteractingLigands = (string[])interactingLigandsHash[centerDomain];
            }
            string[] centerDomainInteractingDna = new string[0];
            if (interactingDnaRnaChainsHash.ContainsKey(centerDomain))
            {
                centerDomainInteractingDna = (string[])interactingDnaRnaChainsHash[centerDomain];
            }
            string[] centerInterfaceScripLines = FormatDomainPymolScript(centerDomain, centerDomainRanges, centerDomainInteractingLigands, centerDomainInteractingDna, out centerDomainResidueRangeString);

            scriptChainAlignWriter.WriteLine("set ignore_case, 0");
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine("set ignore_case, 0");
            scriptPairFitWriter.WriteLine();

            scriptDomainAlignWriter.WriteLine("set ignore_case, 0");
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine("set ignore_case, 0");
            scriptPairFitDomainWriter.WriteLine();

            scriptChainAlignWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptPairFitWriter.WriteLine();


            scriptDomainAlignWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptPairFitDomainWriter.WriteLine();

            string[] domainInteractingLigands = new string[0];
            string[] domainInteractingDna = new string[0];
            foreach (string coordDomain in coordDomains)
            {
                if (coordDomain == centerDomain)
                {
                    continue;
                }
                domainInteractingLigands = new string[0];
                if (interactingLigandsHash.ContainsKey(coordDomain))
                {
                    domainInteractingLigands = (string[])interactingLigandsHash[coordDomain];
                }
                domainInteractingDna = new string[0];
                if (interactingDnaRnaChainsHash.ContainsKey(coordDomain))
                {
                    domainInteractingDna = (string[])interactingDnaRnaChainsHash[coordDomain];
                }
                Range[] domainRanges = (Range[])domainRangesHash[coordDomain];
                string[] domainAlignScripts = FormatDomainPymolScript(coordDomain, domainRanges, centerDomain, centerDomainResidueRangeString, domainInteractingLigands,
                    domainInteractingDna, centerDomainHmmSeqIdHash, pfamDomainTable, domainCoordSeqIdsHash);

                scriptChainAlignWriter.WriteLine(domainAlignScripts[0]);
                scriptChainAlignWriter.WriteLine();

                scriptPairFitWriter.WriteLine(domainAlignScripts[1]);
                scriptPairFitWriter.WriteLine();

                scriptDomainAlignWriter.WriteLine(domainAlignScripts[2]);
                scriptDomainAlignWriter.WriteLine();

                scriptPairFitDomainWriter.WriteLine(domainAlignScripts[3]);
                scriptPairFitDomainWriter.WriteLine();
            }

            // for ligands and DNA/RNA display
            string seleLigandsString = FormatSelectLigandString(interactingLigandsHash, coordDomains);
            string seleDnaRnaString = FormatSelectDnaRnaString(interactingDnaRnaChainsHash, coordDomains);          

            string hetScriptLines = "center " + centerDomain + ".pfam\n";
            hetScriptLines += "sele allhet, het\n";   // all ligands/DNA/RNA except protein chains
            hetScriptLines += "color white, allhet\n";
            hetScriptLines += "util.cnc allhet\n"; // color all het atoms by elements, while color carbon atoms by white
            hetScriptLines += "hide spheres, allhet\n";
            if (seleLigandsString != "")
            {
                hetScriptLines += (seleLigandsString + "\n");
                hetScriptLines += "show spheres, selectLigands\n";
            }
            if (seleDnaRnaString != "")
            {
                hetScriptLines += (seleDnaRnaString + "\n");
                hetScriptLines += "show cartoon, selectDnaRna\n";
            }            
            if (ligandNameDomainChainHash != null)
            {
                // add select for each ligand, so a user can look into a single ligand
                string[] singleLigandSelStrings = FormatSelectSingleLigandString(interactingLigandsHash, ligandNameDomainChainHash, coordDomains);
                foreach (string singleLigandSelString in singleLigandSelStrings)
                {
                    hetScriptLines += (singleLigandSelString + "\n");
                }
                string[] singleDnaRnaSelStrings = FormatSelectSingleLigandString(interactingDnaRnaChainsHash, ligandNameDomainChainHash, coordDomains);
                foreach (string singleDnaRnaSelString in singleDnaRnaSelStrings)
                {
                    hetScriptLines += (singleDnaRnaSelString + "\n");
                }
            }
            scriptChainAlignWriter.WriteLine(hetScriptLines);
            scriptChainAlignWriter.Close();

            scriptPairFitWriter.WriteLine(hetScriptLines);
            scriptPairFitWriter.Close();

            scriptDomainAlignWriter.WriteLine(hetScriptLines);
            scriptDomainAlignWriter.Close();

            scriptPairFitDomainWriter.WriteLine(hetScriptLines);
            scriptPairFitDomainWriter.Close();

            string[] scriptFiles = new string[4];
            scriptFiles[0] = pymolScripFileName + "_byChain.pml";
            scriptFiles[1] = pymolScripFileName + "_pairFit.pml";
            scriptFiles[2] = pymolScripFileName + "_byDomain.pml";
            scriptFiles[3] = pymolScripFileName + "_pairFitDomain.pml";
            return scriptFiles;
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInteractingLigandsHash"></param>
        /// <returns></returns>
        public string FormatSelectLigandString(Dictionary<string, string[]> domainInteractingLigandsHash, string[] entryDomains)
        {
            if (domainInteractingLigandsHash.Count == 0)
            {
                return "";
            }
            string seleLigandsString = "sele selectLigands, ";
            foreach (string coordDomain in entryDomains)
            {
                if (domainInteractingLigandsHash.ContainsKey(coordDomain))
                {
                    foreach (string ligandChain in domainInteractingLigandsHash[coordDomain])
                    {
                        seleLigandsString += (coordDomain + ".pfam and chain " + ligandChain + " + ");
                    }
                }
            }           
            return seleLigandsString.TrimEnd(" + ".ToCharArray());
        }
         
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterLigandsHash">the ligand clusters</param>
        /// <returns></returns>
        public string FormatClusterLigandString(Dictionary<int, string[]> clusterLigandsHash, string[] entryDomains)
        {
            if (clusterLigandsHash.Count == 0)
            {
                return "";
            }
            string seleClusterString = "";
            string seleThisClusterString = "";
            List<int> clusterIdList = new List<int>(clusterLigandsHash.Keys);
            clusterIdList.Sort();
            string clusterObj = "";
            foreach (int clusterId in clusterIdList)
            {
                clusterObj = "cluster_" + clusterId.ToString();
                seleThisClusterString = "";
                foreach (string ligandChain in clusterLigandsHash[clusterId])
                {
                    string[] domainLigandFields = ligandChain.Split('_');
                    if (entryDomains.Contains(domainLigandFields[0]))
                    {
                        seleThisClusterString += (domainLigandFields[0] + ".pfam and chain " + domainLigandFields[1] + " + ");
                    }
                } 
                if (seleThisClusterString != "")
                {
                    seleThisClusterString = "sele " + clusterObj + ", " + seleThisClusterString;
                    seleClusterString += (seleThisClusterString.TrimEnd(" + ".ToCharArray()) + "\n");
                }
            }
            return seleClusterString.TrimEnd('\n');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInteractingLigandsHash"></param>
        /// <param name="ligandNameDomainChainHash"></param>
        /// <returns></returns>
        public string[] FormatSelectSingleLigandString(Dictionary<string, string[]> domainInteractingLigandsHash, Dictionary<string, string[]> ligandNameDomainChainHash,
            string[] entryDomains)
        {
            List<string> selSingleLigandStringList = new List<string> ();
            List<string> ligandNameList = new List<string> (ligandNameDomainChainHash.Keys);
            ligandNameList.Sort();
            string selLigandString = "";
            foreach (string ligandName in ligandNameList)
            {
                selLigandString = "";
                foreach (string domainChain in ligandNameDomainChainHash[ligandName])
                {
                    string[] fields = domainChain.Split('_');  // domain  + ligand chain id
                    if (entryDomains.Contains(fields[0]))
                    {
                        if (domainInteractingLigandsHash.ContainsKey(fields[0]))
                        {
                            string[] ligandChains = domainInteractingLigandsHash[fields[0]];
                            if (ligandChains.Contains(fields[1]))
                            {
                                selLigandString += (fields[0] + ".pfam and chain " + fields[1] + " + ");
                            }
                        }
                    }
                }
                if (selLigandString != "")
                {
                    selLigandString = "sele " + ligandName + ", " + selLigandString;
                    selSingleLigandStringList.Add(selLigandString.TrimEnd(" + ".ToCharArray()));
                }
            }
            return selSingleLigandStringList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInteractingLigandsHash"></param>
        /// <returns></returns>
        private string FormatSelectDnaRnaString(Dictionary<string, string[]> domainInteractingDnaRnaHash, string[] entryDomains)
        {
            if (domainInteractingDnaRnaHash.Count == 0)
            {
                return "";
            }
            string seleDnaRnaString = "sele selectDnaRna, ";
            foreach (string coordDomain in entryDomains)
            {
                if (domainInteractingDnaRnaHash.ContainsKey (coordDomain))
                {
                    foreach (string dnaRnaChain in domainInteractingDnaRnaHash[coordDomain])
                    {
                        seleDnaRnaString += (coordDomain + ".pfam and chain " + dnaRnaChain + " + ");
                    }
                }
            }
            return seleDnaRnaString.TrimEnd(" + ".ToCharArray());
        }

        /// <summary>
        /// check if there is a chain id in the lower case, so case-sensitive is needed.
        /// </summary>
        /// <param name="chainDomainLigandsHash"></param>
        /// <returns></returns>
        private bool NeedCaseSensitive(Dictionary<string, string[]> chainDomainLigandsHash)
        {
            foreach (string chainDomain in chainDomainLigandsHash.Keys)
            {
                string[] domainInteractingLigands = (string[])chainDomainLigandsHash[chainDomain];
                foreach (string ligand in domainInteractingLigands)
                {
                    if (char.IsLower(ligand[0]))
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
        /// <param name="centerDomain"></param>
        /// <param name="domainRanges"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        private string[] FormatDomainPymolScript(string pfamDomain, Range[] domainRanges, string[] interactingLigands, string[] interactingDnaRna, out string domainResidueRangeString)
        {
            domainResidueRangeString = "";

            string domainPymolChainScript = "";
            string domainPymolScript = "";
            string domainFileName = pfamDomain + ".pfam";

            string domainRangeString = FormatDomainRanges(domainRanges);
            domainResidueRangeString = " and chain A and resi " + domainRangeString;

     //       domainPymolChainScript = ("load " + domainFileName + "\r\n");
            domainPymolChainScript = ("load " + domainFileName + ", format=pdb, object=" + domainFileName + "\r\n");
            domainPymolChainScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolChainScript += ("show cartoon, " + domainFileName + " and chain A\r\n");
            foreach (string dnaRnaChain in interactingDnaRna)
            {
                domainPymolChainScript += ("show cartoon, " + domainFileName + " and chain " + dnaRnaChain + "\r\n");  // for DNA/RNA
            }
            foreach (string ligandChain in interactingLigands)
            {
                domainPymolChainScript += ("show spheres, " + domainFileName + " and chain " + ligandChain + "\r\n"); // for ligands 
            }

            domainPymolChainScript += ("color white,  " + domainFileName + " and chain A \r\n");
            domainPymolChainScript += ("spectrum count, rainbow, " + domainFileName + domainResidueRangeString + "\r\n");  // rainbow the domain region

            domainPymolScript = ("load " + domainFileName + ", format=pdb, object=" + domainFileName + "\r\n");
            domainPymolScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolScript += ("show cartoon, " + domainFileName + " and chain A\r\n");
            foreach (string dnaRnaChain in interactingDnaRna)
            {
                domainPymolScript += ("show cartoon, " + domainFileName + " and chain " + dnaRnaChain + "\r\n"); // for DNA/RNA 
            }
            foreach (string ligand in interactingLigands)  // only show ligands which are interacting with the domain
            {
                domainPymolScript += ("show spheres, " + domainFileName + " and chain " + ligand + "\r\n"); // for ligands 
         //       domainPymolScript += ("color magenta, " + domainFileName + " and Chain " + ligand + "\r\n");  // color ligands to be magenta
            }
            //  interfacePymolChainScript += ("util.cbc\r\n");  // color by chains    
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
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceId"></param>
        public string[] FormatDomainPymolScript(string entryDomain, Range[] domainRanges, string centerDomain, string centerDomainRangeString, string[] domainInteractingLigands,
            string[] domainInteractingDnaChains, Dictionary<int, int> centerDomainHmmSeqIdHash, DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash)
        {
            string domainResidueRangeString = "";
            string[] domainPymolScripts = FormatDomainPymolScript(entryDomain, domainRanges, domainInteractingLigands, domainInteractingDnaChains, out domainResidueRangeString);
            string domainFileName = entryDomain + ".pfam";
            string centerFileName = centerDomain + ".pfam";

            string domainPymolScriptDomain = domainPymolScripts[1] + ("align " + domainFileName + domainResidueRangeString + ", " +
                   centerFileName + centerDomainRangeString);

            string domainPymolScriptChain = domainPymolScripts[0] + ("align " + domainFileName + " and chain A, " + centerFileName + " and chain A");

            string pairAlignLine = FormatDomainPairFitPymolScript (entryDomain, centerDomain, centerDomainHmmSeqIdHash, pfamDomainTable, domainCoordSeqIdsHash);
            string domainPymolScriptPairFitChain = "";
            string domainPymolScriptPairFitDomain = "";
            if (pairAlignLine != "")
            {
                domainPymolScriptPairFitChain = domainPymolScripts[0] + pairAlignLine;
                domainPymolScriptPairFitDomain = domainPymolScripts[1] + pairAlignLine;
            }
            else  // align by domain
            {
            //    domainPymolScriptPairFitChain = domainPymolScripts[0] + ("align " + domainFileName + " and chain A, " + centerFileName + " and chain A");
                // modified on March 14, 2013, before this time, so all *_pairFit.pml are aligned by chain if pairAlignLine is none
                // the *_pairFitDomain.pml is same
                domainPymolScriptPairFitChain = domainPymolScripts[0] + ("align " + domainFileName + domainResidueRangeString + ", " +
                                        centerFileName + centerDomainRangeString);
                domainPymolScriptPairFitDomain = domainPymolScripts[1] + ("align " + domainFileName + domainResidueRangeString + ", " +
                                        centerFileName + centerDomainRangeString);
            }
            string[] domainAlignPymolScripts = new string[4];
            domainAlignPymolScripts[0] = domainPymolScriptChain;
            domainAlignPymolScripts[1] = domainPymolScriptPairFitChain;
            domainAlignPymolScripts[2] = domainPymolScriptDomain;
            domainAlignPymolScripts[3] = domainPymolScriptPairFitDomain;
            return domainAlignPymolScripts;
        }
        #endregion

        #region domain align for other domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScripFileName"></param>
        /// <param name="coordDomains"></param>
        /// <param name="centerDomain"></param>
        /// <param name="dataDir"></param>
        /// <param name="domainRangesHash"></param>
        /// <param name="domainHmmSeqIdHash"></param>
        /// <param name="domainCoordSeqIdsHash"></param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public string[] FormatPymolScriptFile(string pymolScripFileName, string[] coordDomains, string centerDomain,string dataDir,
                          Dictionary<string, Range[]> domainRangesHash, Dictionary<string, Dictionary<int, int>> domainHmmSeqIdHash, string fileExt)
        {
            string pymolScriptFileChainAlign = Path.Combine(dataDir, pymolScripFileName + "_byChain.pml");  // display and align the whole chains
            string pymolScriptFilePairFit = Path.Combine(dataDir, pymolScripFileName + "_pairFit.pml");     // display whole chains and pair_fit domain regions
            string pymolScriptFileDomainAlign = Path.Combine(dataDir, pymolScripFileName + "_byDomain.pml");  // display and align domain regions only
            string pymolScriptFilePairFileDomain = Path.Combine(dataDir, pymolScripFileName + "_pairFitDomain.pml");  // display and pair_fit domain regions only
            // display whole chain 
            StreamWriter scriptChainAlignWriter = new StreamWriter(pymolScriptFileChainAlign);
            StreamWriter scriptPairFitWriter = new StreamWriter(pymolScriptFilePairFit);
            // display the domain only
            StreamWriter scriptDomainAlignWriter = new StreamWriter(pymolScriptFileDomainAlign);
            StreamWriter scriptPairFitDomainWriter = new StreamWriter(pymolScriptFilePairFileDomain);

            Dictionary<int, int> centerDomainHmmSeqIdHash = domainHmmSeqIdHash[centerDomain];

            Range[] centerDomainRanges = domainRangesHash[centerDomain];
            string centerDomainResidueRangeString = "";

            string[] centerInterfaceScripLines = FormatDomainPymolScript(centerDomain + "." + fileExt, centerDomainRanges, out centerDomainResidueRangeString);

            scriptChainAlignWriter.WriteLine("set ignore_case, 0");
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine("set ignore_case, 0");
            scriptPairFitWriter.WriteLine();

            scriptDomainAlignWriter.WriteLine("set ignore_case, 0");
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine("set ignore_case, 0");
            scriptPairFitDomainWriter.WriteLine();

            scriptChainAlignWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptChainAlignWriter.WriteLine();

            scriptPairFitWriter.WriteLine(centerInterfaceScripLines[0]);
            scriptPairFitWriter.WriteLine();


            scriptDomainAlignWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptDomainAlignWriter.WriteLine();

            scriptPairFitDomainWriter.WriteLine(centerInterfaceScripLines[1]);
            scriptPairFitDomainWriter.WriteLine();

            foreach (string coordDomain in coordDomains)
            {
                if (coordDomain == centerDomain)
                {
                    continue;
                }
               
                Range[] domainRanges = domainRangesHash[coordDomain];
                Dictionary<int, int> coordDomainHmmSeqIdHash = domainHmmSeqIdHash[coordDomain];
                string[] domainAlignScripts = FormatDomainPymolScript(coordDomain, domainRanges, centerDomain, centerDomainResidueRangeString,
                    centerDomainHmmSeqIdHash, coordDomainHmmSeqIdHash, fileExt);

                scriptChainAlignWriter.WriteLine(domainAlignScripts[0]);
                scriptChainAlignWriter.WriteLine();

                scriptPairFitWriter.WriteLine(domainAlignScripts[1]);
                scriptPairFitWriter.WriteLine();

                scriptDomainAlignWriter.WriteLine(domainAlignScripts[2]);
                scriptDomainAlignWriter.WriteLine();

                scriptPairFitDomainWriter.WriteLine(domainAlignScripts[3]);
                scriptPairFitDomainWriter.WriteLine();
            }

            string hetScriptLines = "center " + centerDomain + "." + fileExt + "\r\n";

            scriptChainAlignWriter.WriteLine(hetScriptLines);
            scriptChainAlignWriter.Close();

            scriptPairFitWriter.WriteLine(hetScriptLines);
            scriptPairFitWriter.Close();

            scriptDomainAlignWriter.WriteLine(hetScriptLines);
            scriptDomainAlignWriter.Close();

            scriptPairFitDomainWriter.WriteLine(hetScriptLines);
            scriptPairFitDomainWriter.Close();

            string[] scriptFiles = new string[4];
            scriptFiles[0] = pymolScripFileName + "_byChain.pml";
            scriptFiles[1] = pymolScripFileName + "_pairFit.pml";
            scriptFiles[2] = pymolScripFileName + "_byDomain.pml";
            scriptFiles[3] = pymolScripFileName + "_pairFitDomain.pml";
            return scriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="domainRanges"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        private string[] FormatDomainPymolScript(string domainFileName, Range[] domainRanges, out string domainResidueRangeString)
        {
            domainResidueRangeString = "";

            string domainPymolChainScript = "";
            string domainPymolScript = "";
  //          string domainFileName = domainFile + ".pfam";

            string domainRangeString = FormatDomainRanges(domainRanges);
            domainResidueRangeString = " and chain A and resi " + domainRangeString;

            domainPymolChainScript = ("load " + domainFileName + ", format=pdb, object=" + domainFileName + "\r\n");
            domainPymolChainScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolChainScript += ("show cartoon, " + domainFileName + " and chain A\r\n");
            domainPymolChainScript += ("show cartoon, " + domainFileName + " and chain B\r\n");

            domainPymolChainScript += ("color white,  " + domainFileName + " and chain A \r\n");
            domainPymolChainScript += ("spectrum count, rainbow, " + domainFileName + domainResidueRangeString + "\r\n");  // rainbow the domain region
            domainPymolChainScript += ("color magenta, " + domainFileName + " and chain B \r\n");

            domainPymolScript = ("load " + domainFileName + ", format=pdb, object=" + domainFileName + "\r\n");
            domainPymolScript += ("hide lines, " + domainFileName + "\r\n");
            domainPymolScript += ("show cartoon, " + domainFileName + " and chain A\r\n");
            domainPymolScript += ("show cartoon, " + domainFileName + " and chain B\r\n");
           
            //  interfacePymolChainScript += ("util.cbc\r\n");  // color by chains    
            domainPymolScript += ("hide cartoon, " + domainFileName + " and chain A and not resi " + domainRangeString + "\r\n");
            domainPymolScript += ("spectrum count, rainbow, " + domainFileName + domainResidueRangeString + "\r\n");  // rainbow the domain region
            domainPymolScript += ("color magenta, " + domainFileName + " and chain B \r\n");

            string[] domainPymolScripts = new string[2];
            domainPymolScripts[0] = domainPymolChainScript;
            domainPymolScripts[1] = domainPymolScript;
            return domainPymolScripts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceId"></param>
        public string[] FormatDomainPymolScript(string entryDomain, Range[] domainRanges, string centerDomain, string centerDomainRangeString,
                    Dictionary<int, int> centerDomainHmmSeqIdHash, Dictionary<int, int> domainHmmSeqIdHash, string fileExt)
        {
            string domainResidueRangeString = "";
            string domainFileName = entryDomain + "." + fileExt;
            string centerFileName = centerDomain + "." + fileExt;
            string[] domainPymolScripts = FormatDomainPymolScript(domainFileName, domainRanges, out domainResidueRangeString);
            
            string domainPymolScriptDomain = domainPymolScripts[1] + ("align " + domainFileName + domainResidueRangeString + ", " +
                   centerFileName + centerDomainRangeString);

            string domainPymolScriptChain = domainPymolScripts[0] + ("align " + domainFileName + " and chain A, " + centerFileName + " and chain A");

            string pairAlignLine = FormatDomainPairFitPymolScript(domainFileName, centerFileName, centerDomainHmmSeqIdHash, domainHmmSeqIdHash);
            string domainPymolScriptPairFitChain = "";
            string domainPymolScriptPairFitDomain = "";
            if (pairAlignLine != "")
            {
                domainPymolScriptPairFitChain = domainPymolScripts[0] + pairAlignLine;
                domainPymolScriptPairFitDomain = domainPymolScripts[1] + pairAlignLine;
            }
            else  // align by domain
            {
                //    domainPymolScriptPairFitChain = domainPymolScripts[0] + ("align " + domainFileName + " and chain A, " + centerFileName + " and chain A");
                // modified on March 14, 2013, before this time, so all *_pairFit.pml are aligned by chain if pairAlignLine is none
                // the *_pairFitDomain.pml is same
                domainPymolScriptPairFitChain = domainPymolScripts[0] + ("align " + domainFileName + domainResidueRangeString + ", " +
                                        centerFileName + centerDomainRangeString);
                domainPymolScriptPairFitDomain = domainPymolScripts[1] + ("align " + domainFileName + domainResidueRangeString + ", " +
                                        centerFileName + centerDomainRangeString);
            }
            string[] domainAlignPymolScripts = new string[4];
            domainAlignPymolScripts[0] = domainPymolScriptChain;
            domainAlignPymolScripts[1] = domainPymolScriptPairFitChain;
            domainAlignPymolScripts[2] = domainPymolScriptDomain;
            domainAlignPymolScripts[3] = domainPymolScriptPairFitDomain;
            return domainAlignPymolScripts;
        }
        #endregion

        #region pair_fit
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="centerDomain"></param>
        /// <param name="centerDomainHmmSeqIdHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public string FormatDomainPairFitPymolScript(string entryDomain, string centerDomain, Dictionary<int, int> centerDomainHmmSeqIdHash,
            DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash)
        {
            string[] seqIdPairsByHmm = MapDomainSeqIdsByHmmOrder(entryDomain, centerDomainHmmSeqIdHash, pfamDomainTable, domainCoordSeqIdsHash);
            if (seqIdPairsByHmm.Length == 0)
            {
                return "";
            }
            string[] pairFileSeqRegionStrings = FormatSeqIdPairsToRegions(seqIdPairsByHmm);
            //    string pairFitScript = "pair_fit " + entryDomain + ".pfam///" + pairFileSeqRegionStrings[0] + "/CA+N+O, " +
            //        centerDomain + ".pfam///" + pairFileSeqRegionStrings[1] + "/CA+N+O";
            // must put the chain ID here, otherwise pymol will count all the residues with the number for all the chains including heteratoms
            string pairFitScript = "pair_fit " + entryDomain + ".pfam//A/" + pairFileSeqRegionStrings[0] + "/CA, " +
                centerDomain + ".pfam//A/" + pairFileSeqRegionStrings[1] + "/CA";

            return pairFitScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="centerDomain"></param>
        /// <param name="centerDomainHmmSeqIdHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public string FormatDomainPairFitPymolScript(string entryDomainFile, string centerDomainFile, Dictionary<int, int> centerDomainHmmSeqIdHash, Dictionary<int, int> domainHmmSeqIdHash )
        {
            string[] seqIdPairsByHmm = MapDomainSeqIdsByHmmOrder (domainHmmSeqIdHash, centerDomainHmmSeqIdHash);
            if (seqIdPairsByHmm.Length == 0)
            {
                return "";
            }
            string[] pairFileSeqRegionStrings = FormatSeqIdPairsToRegions(seqIdPairsByHmm);
            //    string pairFitScript = "pair_fit " + entryDomain + ".pfam///" + pairFileSeqRegionStrings[0] + "/CA+N+O, " +
            //        centerDomain + ".pfam///" + pairFileSeqRegionStrings[1] + "/CA+N+O";
            // must put the chain ID here, otherwise pymol will count all the residues with the number for all the chains including heteratoms
            string pairFitScript = "pair_fit " + entryDomainFile + "//A/" + pairFileSeqRegionStrings[0] + "/CA, " +
                centerDomainFile + "//A/" + pairFileSeqRegionStrings[1] + "/CA";

            return pairFitScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqIdPairs"></param>
        /// <returns></returns>
        public string[] FormatSeqIdPairsToRegions(string[] seqIdPairs)
        {
            int[] seqIds1 = new int[seqIdPairs.Length];
            int[] seqIds2 = new int[seqIdPairs.Length];
            for (int i = 0; i < seqIdPairs.Length; i++)
            {
                string[] seqIdPair = seqIdPairs[i].Split('_');
                seqIds1[i] = Convert.ToInt32(seqIdPair[0]);
                seqIds2[i] = Convert.ToInt32(seqIdPair[1]);
            }
            int seqCount = 0;
            string seqRegionString1 = "";
            string seqRegionString2 = "";
            int seqStart1 = seqIds1[0];
            int seqStart2 = seqIds2[0];
            int seqEnd1 = seqIds1[0];
            int seqEnd2 = seqIds2[0];
            while (seqCount < seqIdPairs.Length - 1)
            {
                if (seqIds1[seqCount + 1] == seqIds1[seqCount] + 1 &&
                    seqIds2[seqCount + 1] == seqIds2[seqCount] + 1)
                {
                    seqEnd1 = seqIds1[seqCount + 1];
                    seqEnd2 = seqIds2[seqCount + 1];
                }
                else
                {
                    if (seqStart1 == seqEnd1)
                    {
                        seqRegionString1 += (seqStart1.ToString() + "+");
                        seqRegionString2 += (seqStart2.ToString() + "+");
                    }
                    else
                    {
                        seqRegionString1 += (seqStart1.ToString() + "-" + seqEnd1.ToString() + "+");
                        seqRegionString2 += (seqStart2.ToString() + "-" + seqEnd2.ToString() + "+");
                    }
                    seqStart1 = seqIds1[seqCount + 1];
                    seqStart2 = seqIds2[seqCount + 1];
                    seqEnd1 = seqIds1[seqCount + 1];
                    seqEnd2 = seqIds2[seqCount + 1];
                }
                seqCount++;
            }
            // add the last region
            if (seqStart1 == seqEnd1)
            {
                seqRegionString1 += (seqStart1.ToString() + "+");
                seqRegionString2 += (seqStart2.ToString() + "+");
            }
            else
            {
                seqRegionString1 += (seqStart1.ToString() + "-" + seqEnd1.ToString() + "+");
                seqRegionString2 += (seqStart2.ToString() + "-" + seqEnd2.ToString() + "+");
            }

            string[] seqRegionStrings = new string[2];
            seqRegionStrings[0] = seqRegionString1.TrimEnd('+');
            seqRegionStrings[1] = seqRegionString2.TrimEnd('+');
            return seqRegionStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="centerDomain"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public string[] MapDomainSeqIdsByHmmOrder(string domain, Dictionary<int,int> centerDomainHmmSeqIdHash, DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash)
        {
            Dictionary<int, int> domainHmmSeqIdHash = GetSequenceHmmSeqIdHash(domain, pfamDomainTable, domainCoordSeqIdsHash);
            string[] seqIdPairs = MapDomainSeqIdsByHmmOrder(domainHmmSeqIdHash, centerDomainHmmSeqIdHash);
            return seqIdPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="centerDomain"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public string[] MapDomainSeqIdsByHmmOrder(Dictionary<int, int> domainHmmSeqIdHash, Dictionary<int, int> centerDomainHmmSeqIdHash)
        {
            List<int> centerHmmSeqIdList = new List<int> (centerDomainHmmSeqIdHash.Keys);
            centerHmmSeqIdList.Sort();
            List<string> seqIdPairList = new List<string> ();
            string seqIdPair = "";
            foreach (int hmmSeqId in centerHmmSeqIdList)
            {
                if (domainHmmSeqIdHash.ContainsKey(hmmSeqId))
                {
                    seqIdPair = domainHmmSeqIdHash[hmmSeqId].ToString() + "_" + centerDomainHmmSeqIdHash[hmmSeqId].ToString();
                    seqIdPairList.Add(seqIdPair);
                }
            }
            return seqIdPairList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public Dictionary<int, int> GetSequenceHmmSeqIdHash(string entryDomain, DataTable pfamDomainTable, Dictionary<string, int[]> domainCoordSeqIdsHash)
        {
            int[] coordSeqIds = domainCoordSeqIdsHash[entryDomain];
            Dictionary<int, int> hmmSeqIdHash = GetSequenceHmmSeqIdHash(entryDomain, pfamDomainTable, coordSeqIds);
            return hmmSeqIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public Dictionary<int, int> GetSequenceHmmSeqIdHash(string entryDomain, DataTable pfamDomainTable, int[] domainCoordSeqIds)
        {
            string pdbId = entryDomain.Substring(0, 4);
            int chainDomainId = Convert.ToInt32(entryDomain.Substring(4, entryDomain.Length - 4));
            DataRow[] domainRows = pfamDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId), "HmmStart ASC");
            Dictionary<int, int> hmmSeqIdHash = new Dictionary<int,int> ();
            int hmmSeqId = 0;
            int seqId = 0;
            string hmmAlignment = "";
            string seqAlignment = "";
            string asymChain = "";
            foreach (DataRow domainRow in domainRows)
            {
                asymChain = domainRow["AsymChain"].ToString().TrimEnd();
                hmmAlignment = domainRow["HmmAlignment"].ToString();
                seqAlignment = domainRow["QueryAlignment"].ToString();
                hmmSeqId = Convert.ToInt32(domainRow["HmmStart"].ToString());
                seqId = Convert.ToInt32(domainRow["AlignStart"].ToString());
                for (int i = 0; i < hmmAlignment.Length; i++)
                {
                    if (hmmAlignment[i] != '-' && hmmAlignment[i] != '.' &&
                        seqAlignment[i] != '-' && seqAlignment[i] != '.' &&
                        Array.IndexOf(domainCoordSeqIds, seqId) > -1)  // must have coordinates for pair_fit
                    {
                        if (!hmmSeqIdHash.ContainsKey(hmmSeqId))
                        {
                            hmmSeqIdHash.Add(hmmSeqId, seqId);
                        }
                    }
                    if (hmmAlignment[i] != '-' && hmmAlignment[i] != '.')
                    {
                        hmmSeqId++;
                    }
                    if (seqAlignment[i] != '-' && seqAlignment[i] != '.')
                    {
                        seqId++;
                    }
                }
            }
            return hmmSeqIdHash;
        }
        #endregion

        #region multi-chain domains
        /// <summary>
        /// the pfam and multi-chain domainid
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, long[]> GetPfamMultiChainDomainHash()
        {
            Dictionary<string, long[]> multiChainPfamDomainHash = new Dictionary<string, long[]>();
            string pfamId = "";
            string pfamMultiChainDomainFile = "PfamMultiChainDomains.txt";
            if (File.Exists(pfamMultiChainDomainFile))
            {
                StreamReader dataReader = new StreamReader(pfamMultiChainDomainFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(',');
                    pfamId = fields[0];
                    long[] pfamDomains = new long[fields.Length - 1];
                    for (int i = 1; i < fields.Length; i++)
                    {
                        pfamDomains[i - 1] = Convert.ToInt64(fields[i]);
                    }
                    multiChainPfamDomainHash.Add(pfamId, pfamDomains);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "Select Pfam_ID, DomainId, Count(Distinct EntityID) As EntityCount From PdbPfam Group BY Pfam_ID, DomainID;";
                DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                int entityCount = 0;
                long domainId = 0;
                Dictionary<string, List<long>> multiChainPfamDomainListHash = new Dictionary<string, List<long>>();
                foreach (DataRow entityCountRow in domainEntityCountTable.Rows)
                {
                    entityCount = Convert.ToInt32(entityCountRow["EntityCount"].ToString());
                    if (entityCount > 1)
                    {
                        domainId = Convert.ToInt64(entityCountRow["DomainID"].ToString());
                        pfamId = entityCountRow["Pfam_ID"].ToString().TrimEnd();
                        if (multiChainPfamDomainListHash.ContainsKey(pfamId))
                        {
                            multiChainPfamDomainListHash[pfamId].Add(domainId);
                        }
                        else
                        {
                            List<long> domainList = new List<long> ();
                            domainList.Add(domainId);
                            multiChainPfamDomainListHash.Add(pfamId, domainList);
                        }
                    }
                }
                StreamWriter dataWriter = new StreamWriter(pfamMultiChainDomainFile);
                string dataLine = "";
                List<string> pfamIdList = new List<string> (multiChainPfamDomainListHash.Keys);
                foreach (string lsPfamId in pfamIdList)
                {
                    long[] domains = multiChainPfamDomainListHash[lsPfamId].ToArray ();                   
                    multiChainPfamDomainHash[lsPfamId] = domains;
                    dataLine = lsPfamId;
                    foreach (long lsDomainId in domains)
                    {
                        dataLine += ("," + lsDomainId.ToString());
                    }
                    dataWriter.WriteLine(dataLine);
                }
                dataWriter.Close();
            }
            return multiChainPfamDomainHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamChainDomainTable"></param>
        public void UpdatePfamMultiChainDomains(DataTable pfamChainDomainTable, long[] multiChainDomainsInPfam)
        {
            List<string> multiChainDomainIdList = new List<string> ();
            string entryChainDomain = "";
            long domainId = 0;
            foreach (DataRow chainDomainRow in pfamChainDomainTable.Rows)
            {
                entryChainDomain = chainDomainRow["PdbID"].ToString() +
                    chainDomainRow["ChainDomainID"].ToString();
                domainId = Convert.ToInt64(chainDomainRow["DomainID"].ToString());
                if (Array.IndexOf(multiChainDomainsInPfam, domainId) > -1)
                {
                    if (!multiChainDomainIdList.Contains(entryChainDomain))
                    {
                        multiChainDomainIdList.Add(entryChainDomain);
                    }
                }
            }
            string pdbId = "";
            int multiChainDomainId = 0;
            foreach (string chainDomain in multiChainDomainIdList)
            {
                pdbId = chainDomain.Substring(0, 4);
                multiChainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                DataRow[] multiChainDomainRows = pfamChainDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, multiChainDomainId));
                try
                {
                    UpdateMultiChainDomainDefRowsByFileSeqIds(multiChainDomainRows);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(chainDomain + ": Update multi-chain domain by file sequence positions: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(chainDomain + ": Update multi-chain domain by file sequence positions: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
            }
            pfamChainDomainTable.AcceptChanges();
        }


        /// <summary>
        /// use file seq start and file seq end to replace the SeqStart and SeqEnd
        /// and AlignStart and AlignEnd
        /// </summary>
        /// <param name="multiChainDomainRows"></param>
        public void UpdateMultiChainDomainDefRowsByFileSeqIds(DataRow[] multiChainDomainRows)
        {
            string pdbId = multiChainDomainRows[0]["PdbID"].ToString();
            int chainDomainId = Convert.ToInt32(multiChainDomainRows[0]["ChainDomainID"].ToString());
            DataTable fileSeqIdTable = GetDomainFileInfo(pdbId, chainDomainId);
            int seqStart = 0;
            int seqEnd = 0;
            int alignStart = 0;
            int alignEnd = 0;
            int fileSeqStart = 0;
            int fileSeqEnd = 0;
            string asymChain = "";
            for (int i = 0; i < multiChainDomainRows.Length; i++)
            {
                asymChain = multiChainDomainRows[i]["AsymChain"].ToString().TrimEnd();
                seqStart = Convert.ToInt32(multiChainDomainRows[i]["SeqStart"].ToString());
                seqEnd = Convert.ToInt32(multiChainDomainRows[i]["SeqEnd"].ToString());
                alignStart = Convert.ToInt32(multiChainDomainRows[i]["AlignStart"].ToString());
                alignEnd = Convert.ToInt32(multiChainDomainRows[i]["AlignEnd"].ToString());
                DataRow[] fileSeqRows = fileSeqIdTable.Select(string.Format("AsymChain = '{0}' AND SeqStart = '{1}' AND SeqEnd = '{2}'",
                    asymChain, seqStart, seqEnd));
                fileSeqStart = Convert.ToInt32(fileSeqRows[0]["FileStart"].ToString());
                fileSeqEnd = Convert.ToInt32(fileSeqRows[0]["FileEnd"].ToString());
                multiChainDomainRows[i]["AsymChain"] = "A";  // for multi-chain domain
                multiChainDomainRows[i]["SeqStart"] = fileSeqStart;
                multiChainDomainRows[i]["SeqEnd"] = fileSeqEnd;
                multiChainDomainRows[i]["AlignStart"] = fileSeqStart + alignStart - seqStart;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private DataTable GetDomainFileInfo(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select Distinct * From  PdbPfamDomainFileInfo Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable fileSeqIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return fileSeqIdTable;
        }
        #endregion

        #region display domain
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomain"></param>
        /// <param name="domainRanges"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        public string[] DisplayDomainPymolScript(string dataFileName, string chainId, Range[] domainRanges, out string domainResidueRangeString)
        {
            domainResidueRangeString = "";

            domainResidueRangeString = " and chain " + chainId + " and resi " + FormatDomainRanges(domainRanges);

            string domainPymolScriptChain = ("hide lines, " + dataFileName + "\r\n");
            domainPymolScriptChain += ("show cartoon, " + dataFileName + " and chain " + chainId + "\r\n"); 
            domainPymolScriptChain += ("color gray30,  " + dataFileName + " and chain " + chainId + "\r\n");
            domainPymolScriptChain += ("spectrum count, rainbow, " + dataFileName + " and chain " + chainId + domainResidueRangeString + "\r\n");  // rainbow the domain region

            string domainPymolScriptDomain = ("hide lines, " + dataFileName + "\r\n");
            domainPymolScriptDomain += ("show cartoon, " + dataFileName + " and chain " + chainId + domainResidueRangeString + "\r\n");   // only display the domain regions
            domainPymolScriptDomain += ("spectrum count, rainbow, " + dataFileName + " and chain " + chainId + domainResidueRangeString + "\r\n");  // rainbow the domain region

            string[] domainPymolScripts = new string[2];
            domainPymolScripts[0] = domainPymolScriptChain;
            domainPymolScripts[1] = domainPymolScriptDomain;
            return domainPymolScripts;
        }
        #endregion

        #region db info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetPfamChainDomainTable(string pfamId)
        {
            string queryString = string.Format("Select PdbPfam.PdbID, PdbPfam.EntityID, PdbPfam.DomainID, SeqStart, SeqEnd, AlignStart, AlignEnd, " +
                " HmmStart, HmmEnd, QueryAlignment, HmmAlignment, AsymChain, AuthChain, ChainDomainID " +
                " From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND PdbPfam.DomainId = PdbPfamChain.DomainID " +
                " AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        public DataTable GetPfamChainDomainTable(string pfamId, long[] domainIds)
        {
            DataTable pfamDomainTable = GetPfamChainDomainTable(pfamId);
            DataTable pfamChainDomainTable = pfamDomainTable.Clone();
            foreach (long domainId in domainIds)
            {
                DataRow[] domainRows = pfamDomainTable.Select(string.Format ("DomainID = '{0}'", domainId));
                foreach (DataRow domainRow in domainRows)
                {
                    DataRow chainDomainRow = pfamChainDomainTable.NewRow();
                    chainDomainRow.ItemArray = domainRow.ItemArray;
                    pfamChainDomainTable.Rows.Add(chainDomainRow);
                }
            }
            return pfamChainDomainTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="chainDomains"></param>
        /// <returns></returns>
        public DataTable GetPfamChainDomainTable(string pfamId, string[] chainDomains)
        {
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(pfamId);
            DataTable clusterPfamChainDomainTable = pfamChainDomainTable.Clone();
            string pdbId = "";
            int chainDomainId = 0;
            foreach (string chainDomain in chainDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                DataRow[] chainDomainRows = pfamChainDomainTable.Select
                    (string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                foreach (DataRow chainDomainRow in chainDomainRows)
                {
                    DataRow dataRow = clusterPfamChainDomainTable.NewRow();
                    dataRow.ItemArray = chainDomainRow.ItemArray;
                    clusterPfamChainDomainTable.Rows.Add(dataRow);
                }
            }
            return clusterPfamChainDomainTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        public string FormatDomainRanges(Range[] domainRanges)
        {
            string domainRangeString = "";
            foreach (Range domainRange in domainRanges)
            {
                domainRangeString += (domainRange.startPos.ToString() + "-" + domainRange.endPos.ToString());
                domainRangeString += "+";
            }
            domainRangeString = domainRangeString.TrimEnd('+');
            return domainRangeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        public Range[] GetDomainRange(string pdbId, long domainId)
        {
            string queryString = string.Format("Select SeqStart, SeqEnd From PdbPfam " +
                " WHERE PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable rangeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int count = 0;
            Range[] domainRanges = new Range[rangeTable.Rows.Count];
            foreach (DataRow rangeRow in rangeTable.Rows)
            {
                Range domainRange = new Range();
                domainRange.startPos = Convert.ToInt32(rangeRow["SeqStart"].ToString());
                domainRange.endPos = Convert.ToInt32(rangeRow["SeqEnd"].ToString());
                domainRanges[count] = domainRange;
                count++;
            }
            return domainRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainDomains"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        public Dictionary<string, Range[]> GetDomainRangesHash(string[] entryChainDomains, DataTable chainPfamTable)
        {
            string pdbId = "";
            int chainDomainId = 0;
            Dictionary<string, Range[]> entryDomainRangeHash = new Dictionary<string, Range[]>();
            foreach (string entryDomain in entryChainDomains)
            {
                pdbId = entryDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryDomain.Substring(4, entryDomain.Length - 4));

                Range[] domainRanges = GetDomainRanges(pdbId, chainDomainId, chainPfamTable);
                entryDomainRangeHash.Add(entryDomain, domainRanges);
            }
            return entryDomainRangeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainDomains"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        public Range[] GetDomainRanges (string pdbId, int chainDomainId, DataTable chainPfamTable)
        {
            Range[] domainRanges = null;
            DataRow[] domainDefRows = chainPfamTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
            // for multi-chain domain, directly use the domain file where the sequenc numbers are always started at 1.
            if (IsDomainMultiChain(domainDefRows))
            {
                Range fileRange = GetMultiChainDomainFileRange(pdbId, chainDomainId);
                domainRanges = new Range[1];
                domainRanges[0] = fileRange;
            }
            else
            {
                domainRanges = new Range[domainDefRows.Length];
                int count = 0;
                foreach (DataRow domainRow in domainDefRows)
                {
                    Range range = new Range();
                    range.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                    range.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    domainRanges[count] = range;
                    count++;
                }
            }
            return domainRanges;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        public Range GetMultiChainDomainFileRange(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select Distinct FileStart, FileEnd From PdbPfamDomainFileInfo " +
                " Where PdbID = '{0}' AND ChainDomainID = {1} Order By FileStart;",
                 pdbId, chainDomainId);
            DataTable fileSeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Range fileRange = new Range();
            fileRange.startPos = Convert.ToInt32(fileSeqTable.Rows[0]["FileStart"].ToString());
            fileRange.endPos = Convert.ToInt32(fileSeqTable.Rows[fileSeqTable.Rows.Count - 1]["FileEnd"].ToString());
            return fileRange;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <returns></returns>
        public bool IsDomainMultiChain(DataRow[] domainRows)
        {
            List<int> entityList = new List<int> ();
            int entityId = 0;
            foreach (DataRow domainRow in domainRows)
            {
                entityId = Convert.ToInt32(domainRow["EntityID"].ToString());
                if (!entityList.Contains(entityId))
                {
                    entityList.Add(entityId);
                }
            }
            if (entityList.Count > 1)
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}
