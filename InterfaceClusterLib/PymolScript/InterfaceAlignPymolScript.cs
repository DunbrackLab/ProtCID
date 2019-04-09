using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using ProtCidSettingsLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace InterfaceClusterLib.PymolScript
{
    public class InterfaceAlignPymolScript : DomainAlignPymolScript
    {
        #region member variable
        private DbQuery dbQuery = new DbQuery();
        #endregion

        #region chain interface pymol script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterFileDir"></param>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string[] FormatChainInterfacePymolScriptFiles(string clusterFileDir, int groupId, int clusterId, string[] clusterInterfaces)
        {
            //    string firstExistInterface = GetTheFirstExistInterfaceFile(clusterFileDir, clusterInterfaces);
            string centerInterface = GetTheCenterInterface(groupId, clusterId);

            string pymolScriptFileChain = Path.Combine(clusterFileDir, groupId.ToString() + "_" + clusterId.ToString() + "_bychain.pml");
            StreamWriter scriptWriter = new StreamWriter(pymolScriptFileChain);
            string scriptLine = "";
            string centerInterfaceScripLine = GetChainInterfacePymolScript(centerInterface);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                scriptLine = GetAlignedInterfacePymolScriptByChain(clusterInterface, centerInterface);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();

            string pymolScriptFile = Path.Combine(clusterFileDir, groupId.ToString() + "_" + clusterId.ToString() + ".pml");
            scriptWriter = new StreamWriter(pymolScriptFile);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                scriptLine = GetAlignedInterfacePymolScript(clusterInterface, centerInterface);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();
            string[] pymolScriptFiles = new string[2];
            pymolScriptFiles[0] = groupId.ToString() + "_" + clusterId.ToString() + ".pml";
            pymolScriptFiles[1] = groupId.ToString() + "_" + clusterId.ToString() + "_bychain.pml";
            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFileName"></param>
        /// <param name="clusterFileDir"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string[] FormatChainInterfacePymolScriptFiles(string pmlScriptFileName, string clusterFileDir, string[] clusterInterfaces)
        {
            //    string firstExistInterface = GetTheFirstExistInterfaceFile(clusterFileDir, clusterInterfaces);
            string centerInterface = clusterInterfaces[0];

            string pymolScriptFileChain = Path.Combine(clusterFileDir, pmlScriptFileName + "_bychain.pml");
            StreamWriter scriptWriter = new StreamWriter(pymolScriptFileChain);
            string scriptLine = "";
            string centerInterfaceScripLine = GetChainInterfacePymolScript(centerInterface);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                scriptLine = GetAlignedInterfacePymolScriptByChain(clusterInterface, centerInterface);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();

            string pymolScriptFile = Path.Combine(clusterFileDir, pmlScriptFileName + ".pml");
            scriptWriter = new StreamWriter(pymolScriptFile);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                scriptLine = GetAlignedInterfacePymolScript(clusterInterface, centerInterface);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();
            string[] pymolScriptFiles = new string[2];
            pymolScriptFiles[0] = pmlScriptFileName + ".pml";
            pymolScriptFiles[1] = pmlScriptFileName + "_bychain.pml";
            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstInterface"></param>
        /// <returns></returns>
        private string GetChainInterfacePymolScript(string chainInterface)
        {
            string scriptLine = "load " + chainInterface + ", format=pdb, object=" + chainInterface +  "\r\n";
            scriptLine += "hide lines, " + chainInterface + "\r\n";
            scriptLine += "show cartoon, " + chainInterface + "\r\n";
            scriptLine += "spectrum count, rainbow, " + chainInterface + " and chain A\r\n";
            scriptLine += "spectrum count, rainbow, " + chainInterface + " and chain B\r\n";
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        private string GetAlignedInterfacePymolScriptByChain(string clusterInterface, string centerInterface)
        {
            string scriptLine = GetChainInterfacePymolScript (clusterInterface);
            scriptLine += "align " + clusterInterface + " and chain A, " + centerInterface + " and chain A";
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        private string GetAlignedInterfacePymolScript(string clusterInterface, string centerInterface)
        {
            string scriptLine = GetChainInterfacePymolScript(clusterInterface);
            scriptLine += "align " + clusterInterface + ", " + centerInterface;
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string GetTheFirstExistInterfaceFile(string clusterFileDir, string[] clusterInterfaces)
        {
            string clusterInterfaceFile = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                clusterInterfaceFile = Path.Combine(clusterFileDir, clusterInterface);
                if (File.Exists(clusterInterfaceFile))
                {
                    return clusterInterface;
                }
            }
            return "";
        }

        /// <summary>
        /// The first interface in alphabet order
        /// </summary>
        /// <returns></returns>
        private string GetTheCenterInterface(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select First 1 * From PfamSuperInterfaceClusters " +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbId, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string centerInterface = clusterInterfaceTable.Rows[0]["PdbID"].ToString() + "_" +
                clusterInterfaceTable.Rows[0]["InterfaceID"].ToString() + ".cryst";
            return centerInterface;
        }
        #endregion

        #region domain interface pymol script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        public string[] FormatDomainInterfacePymolScript(string pdbId, int domainInterfaceId, string domainInterface, out string domainResidueRangeString)
        {
            domainResidueRangeString = "";
            bool isReversed = false;
            Range[][] interfaceDomainRanges = GetInterfaceDomainRanges(pdbId, domainInterfaceId, out isReversed);

            string domainInterfaceFile = domainInterface + ".cryst";

            string domainResidueRangeStringA = " and chain A and resi " + FormatDomainRanges(interfaceDomainRanges[0]);
            string domainResidueRangeStringB = " and chain B and resi " + FormatDomainRanges(interfaceDomainRanges[1]);

            string[] pymolScriptData = FormatDomainInterfacePymolScriptLines(domainInterfaceFile, domainResidueRangeStringA, domainResidueRangeStringB);

            if (isReversed)
            {
                domainResidueRangeString = domainResidueRangeStringB;
            }
            else
            {
                domainResidueRangeString = domainResidueRangeStringA;
            }
            return pymolScriptData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        public string[] FormatDomainInterfacePymolScript(string pdbId, int domainInterfaceId, string domainInterface, bool isReversed,
            out string domainResidueRangeString)
        {
            domainResidueRangeString = "";
            bool isPfamReversed = false;
            Range[][] interfaceDomainRanges = GetInterfaceDomainRanges(pdbId, domainInterfaceId, out isPfamReversed);

            string domainInterfaceFile = domainInterface + ".cryst";

            string domainResidueRangeStringA = " and chain A and resi " + FormatDomainRanges(interfaceDomainRanges[0]);
            string domainResidueRangeStringB = " and chain B and resi " + FormatDomainRanges(interfaceDomainRanges[1]);

           string[] domainInterfaceScriptLines = FormatDomainInterfacePymolScriptLines(domainInterfaceFile, domainResidueRangeStringA, domainResidueRangeStringB);

            if (isReversed)
            {
                domainResidueRangeString = domainResidueRangeStringB;
            }
            else
            {
                domainResidueRangeString = domainResidueRangeStringA;
            }

            return domainInterfaceScriptLines;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="domainResidueRangeString"></param>
        /// <returns></returns>
        public string[] FormatMultiChainDomainInterfacePymolScript(string pdbId, int domainInterfaceId, string domainInterface, bool isReversed,
            out string domainResidueRangeString)
        {
            domainResidueRangeString = "";
            bool isPfamReversed = false;
            Range[][] interfaceDomainRanges = GetMultiChainInterfaceDomainRanges(pdbId, domainInterfaceId, out isPfamReversed);

            string domainInterfaceFile = domainInterface + ".cryst";

            string domainResidueRangeStringA = " and chain A and resi " + FormatDomainRanges(interfaceDomainRanges[0]);
            string domainResidueRangeStringB = " and chain B and resi " + FormatDomainRanges(interfaceDomainRanges[1]);

            string[] domainInterfaceScriptLines = FormatDomainInterfacePymolScriptLines(domainInterfaceFile, domainResidueRangeStringA, domainResidueRangeStringB);

            if (isReversed)
            {
                domainResidueRangeString = domainResidueRangeStringB;
            }
            else
            {
                domainResidueRangeString = domainResidueRangeStringA;
            }

            return domainInterfaceScriptLines;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceFile"></param>
        /// <param name="domainResidueRangeStringA"></param>
        /// <param name="domainResidueRangeStringB"></param>
        /// <returns></returns>
        private string[] FormatDomainInterfacePymolScriptLines(string domainInterfaceFile, string domainResidueRangeStringA, string domainResidueRangeStringB)
        {
            string[] domainAlignScriptLines = new string[2];
            string interfacePymolChainScript = ("load " + domainInterfaceFile + ", format=pdb, object=" + domainInterfaceFile + "\r\n");
            interfacePymolChainScript += ("hide lines, " + domainInterfaceFile + "\r\n");
            interfacePymolChainScript += ("show cartoon, " + domainInterfaceFile + "\r\n");
            interfacePymolChainScript += ("color gray10,  " + domainInterfaceFile + "\r\n");
            interfacePymolChainScript += ("spectrum count, rainbow, " + domainInterfaceFile + domainResidueRangeStringA + "\r\n");
            interfacePymolChainScript += ("spectrum count, rainbow, " + domainInterfaceFile + domainResidueRangeStringB + "\r\n");

            string interfacePymolDomainScript = ("load " + domainInterfaceFile + ", format=pdb, object=" + domainInterfaceFile + "\r\n");
            interfacePymolDomainScript += ("hide lines, " + domainInterfaceFile + "\r\n");
            interfacePymolDomainScript += ("show cartoon, " + domainInterfaceFile + domainResidueRangeStringA + "\r\n");
            interfacePymolDomainScript += ("show cartoon,  " + domainInterfaceFile + domainResidueRangeStringB + "\r\n");
            interfacePymolDomainScript += ("spectrum count, rainbow, " + domainInterfaceFile + domainResidueRangeStringA + "\r\n");
            interfacePymolDomainScript += ("spectrum count, rainbow, " + domainInterfaceFile + domainResidueRangeStringB + "\r\n");

            domainAlignScriptLines[0] = interfacePymolChainScript;
            domainAlignScriptLines[1] = interfacePymolDomainScript;
            return domainAlignScriptLines;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceId"></param>
        public string[] FormatDomainInterfacePymolScript(string pdbId, int domainInterfaceId, int interfaceId,
            string centerInterface, string centerDomainRangeString, bool isReversed)
        {
            string centerInterfaceFile = centerInterface + ".cryst";
            string domainInterface = "";
            if (interfaceId != 0)
            {
                domainInterface = pdbId + "_" + interfaceId.ToString();
            }
            else
            {
                domainInterface = pdbId + "_d" + domainInterfaceId.ToString();
            }
            string interfaceFileName = domainInterface + ".cryst";
            string domainResidueRangeString = "";

            string[] pymolScriptData = FormatDomainInterfacePymolScript(pdbId, domainInterfaceId, domainInterface, isReversed, out domainResidueRangeString);

            pymolScriptData[0] = pymolScriptData[0] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
                centerInterfaceFile + centerDomainRangeString);
            pymolScriptData[1] = pymolScriptData[1] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
               centerInterfaceFile + centerDomainRangeString);

            return pymolScriptData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceId"></param>
        public string[] FormatDomainInterfacePymolScript(string pdbId, int domainInterfaceId, string centerInterface, string centerDomainRangeString, bool isReversed)
        {
            string centerInterfaceFile = centerInterface + ".cryst";
            string domainInterface = pdbId + "_d" + domainInterfaceId.ToString();
            string interfaceFileName = domainInterface + ".cryst";
            string domainResidueRangeString = "";

            string[] pymolScriptData = FormatDomainInterfacePymolScript(pdbId, domainInterfaceId, domainInterface, isReversed, out domainResidueRangeString);

            pymolScriptData[0] = pymolScriptData[0] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
                centerInterfaceFile + centerDomainRangeString);
            pymolScriptData[1] = pymolScriptData[1] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
               centerInterfaceFile + centerDomainRangeString);

            return pymolScriptData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceId"></param>
        public string[] FormatMultiChainDomainInterfacePymolScript (string pdbId, int domainInterfaceId, 
                            string centerInterface, string centerDomainRangeString, bool isReversed)
        {
            string centerInterfaceFile = centerInterface + ".cryst";
            string domainInterface = pdbId + "_d" + domainInterfaceId.ToString();

            string interfaceFileName = domainInterface + ".cryst";
            string domainResidueRangeString = "";

            string[] pymolScriptData = FormatMultiChainDomainInterfacePymolScript(pdbId, domainInterfaceId, domainInterface, isReversed, out domainResidueRangeString);

            pymolScriptData[0] = pymolScriptData[0] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
                centerInterfaceFile + centerDomainRangeString);
            pymolScriptData[1] = pymolScriptData[1] + ("align " + interfaceFileName + domainResidueRangeString + ", " +
               centerInterfaceFile + centerDomainRangeString);

            return pymolScriptData;
        }
        #endregion

        #region peptide interface pymol script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterFileDir"></param>
        /// <param name="pymolScripFileName"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string[] FormatPeptideInterfacePymolScriptFiles(string[] clusterInterfaces, DataTable domainInterfaceTable, DataTable pfamDomainTable,
                                    DataTable hmmSiteCompTable, Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash, string pymolScripFileName, string clusterFileDir)
        {
     //      string centerInterface = GetTheFirstExistInterfaceFile(clusterFileDir, clusterInterfaces);
     //       string centerInterface = GetPepInterfaceWithMaxContacts(clusterInterfaces, domainInterfaceTable);
            string centerInterface = GetDomainInterfaceWithMostCommonHmmSites(clusterInterfaces, hmmSiteCompTable);

            string[] pymolScriptFiles = FormatPeptideInterfacePymolScriptFiles(centerInterface, clusterInterfaces, domainInterfaceTable, pfamDomainTable,
                          domainInterfaceChainCoordSeqIdHash, pymolScripFileName, clusterFileDir);

            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterFileDir"></param>
        /// <param name="pymolScripFileName"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string[] FormatPeptideInterfacePymolScriptFiles(string[] clusterInterfaces, DataTable domainInterfaceTable, DataTable pfamDomainTable,
                                   Dictionary<string,  int[]> domainInterfaceChainCoordSeqIdHash, string pymolScripFileName, string clusterFileDir)
        {
            string centerInterface = GetTheFirstExistInterfaceFile(clusterFileDir, clusterInterfaces);
            //       string centerInterface = GetPepInterfaceWithMaxContacts(clusterInterfaces, domainInterfaceTable);

            string[] pymolScriptFiles = FormatPeptideInterfacePymolScriptFiles(centerInterface, clusterInterfaces, domainInterfaceTable, pfamDomainTable,
                          domainInterfaceChainCoordSeqIdHash, pymolScripFileName, clusterFileDir);

            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="domainInterfaceChainCoordSeqIdHash"></param>
        /// <param name="pymolScripFileName"></param>
        /// <param name="clusterFileDir"></param>
        /// <returns></returns>
        public string[] FormatPeptideInterfacePymolScriptFiles(string centerInterface, string[] clusterInterfaces, DataTable domainInterfaceTable, DataTable pfamDomainTable,
                                    Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash, string pymolScripFileName, string clusterFileDir)
        {
            string pymolScriptFileChain = Path.Combine(clusterFileDir, pymolScripFileName + "_bychain.pml");
            StreamWriter scriptWriter = new StreamWriter(pymolScriptFileChain);
            string scriptLine = "";
            string centerInterfaceScripLine = GetPeptideInterfacePymolScript(centerInterface);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            bool isDomainInterface = false;
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                // check if the cluster interface is a domain interface, that is, the chain length > 30
                // if yes, then display the chain in white, not magenta
                isDomainInterface = IsDomainInterface(clusterInterface, domainInterfaceTable);
                scriptLine = GetAlignedPeptideInterfacePymolScriptByChain(clusterInterface, centerInterface, isDomainInterface);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();

            string pymolScriptFile = Path.Combine(clusterFileDir, pymolScripFileName + "_pairFit.pml");
            scriptWriter = new StreamWriter(pymolScriptFile);
            scriptWriter.WriteLine(centerInterfaceScripLine);
            scriptWriter.WriteLine();
            bool isCenterReversed = false;
            Dictionary<int, int>[] centerHmmSeqIdsHashes = GetDomainInterfaceSequenceHmmSeqIdHash(centerInterface, pfamDomainTable,
                domainInterfaceTable, domainInterfaceChainCoordSeqIdHash, out isCenterReversed);
            /*     if (isCenterReversed)
                 {
                     ReverseCenterDomainInterfaceInfo(centerInterface, pfamDomainTable);  // reversed domain interface defintion and domain interface file
                 }*/
            foreach (string clusterInterface in clusterInterfaces)
            {
                if (clusterInterface == centerInterface)
                {
                    continue;
                }
                scriptLine = GetPairFitPeptideInterfacePymolScript(clusterInterface, centerInterface, centerHmmSeqIdsHashes[0],
                    pfamDomainTable, domainInterfaceTable, domainInterfaceChainCoordSeqIdHash);
                scriptWriter.WriteLine(scriptLine);
                scriptWriter.WriteLine();
            }
            scriptWriter.WriteLine("center " + centerInterface);
            scriptWriter.Close();
            string[] pymolScriptFiles = new string[2];
            pymolScriptFiles[0] = pymolScripFileName + "_pairFit.pml";
            pymolScriptFiles[1] = pymolScripFileName + "_bychain.pml";
            return pymolScriptFiles;
        }
 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        private string GetAlignedPeptideInterfacePymolScript(string clusterInterface, string centerInterface, bool isDomainInterface)
        {
            string scriptLine = "";
            if (isDomainInterface)
            {
                scriptLine = GetDomainInterfacePepCompPymolScript(clusterInterface);
            }
            else
            {
                scriptLine = GetPeptideInterfacePymolScript(clusterInterface);
            }
            scriptLine += "align " + clusterInterface + ", " + centerInterface;
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        public string GetPairFitPeptideInterfacePymolScript(string clusterInterface, string centerInterface, 
            Dictionary<int,  int> centerHmmSeqIdsHash, DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash)
        {
            bool isDomainInterface = IsDomainInterface(clusterInterface, domainInterfaceTable);
            string scriptLine = "";
            if (isDomainInterface)
            {
                scriptLine = GetDomainInterfacePepCompPymolScript(clusterInterface);
            }
            else
            {
                scriptLine = GetPeptideInterfacePymolScript(clusterInterface);
            }
            scriptLine += FormatDomainInterfacePairFitPymolScript(clusterInterface, centerInterface, centerHmmSeqIdsHash,
                    pfamDomainTable, domainInterfaceTable, domainInterfaceChainCoordSeqIdHash);
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstInterface"></param>
        /// <returns></returns>
        private string GetPeptideInterfacePymolScript(string peptideInterface)
        {
            string scriptLine = "load " + peptideInterface + ", format=pdb, object=" + peptideInterface + "\r\n";
            scriptLine += "hide lines, " + peptideInterface + "\r\n";
            scriptLine += "show cartoon, " + peptideInterface + "\r\n";
            scriptLine += "spectrum count, rainbow, " + peptideInterface + " and chain A\r\n";
            scriptLine += "color magenta, " + peptideInterface + " and chain B\r\n";
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        private string GetDomainInterfacePepCompPymolScript(string domainInterface)
        {
            string scriptLine = "load " + domainInterface + ", format=pdb, object=" + domainInterface + "\r\n";
            scriptLine += "hide lines, " + domainInterface + "\r\n";
            scriptLine += "show cartoon, " + domainInterface + "\r\n";
            scriptLine += "spectrum count, rainbow, " + domainInterface + " and chain A\r\n";
            scriptLine += "color white, " + domainInterface + " and chain B\r\n";
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="domainInterfaceDefTable"></param>
        /// <returns></returns>
        private bool IsDomainInterface(string domainInterface, DataTable domainInterfaceDefTable)
        {
            string pdbId = domainInterface.Substring(0, 4);
            int domainInterfaceId = GetDomainInterfaceID (domainInterface);
            DataRow[] domainRows = domainInterfaceDefTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (domainRows.Length > 0)
            {
                int numOfAtomPairs = Convert.ToInt32 (domainRows[0]["NumOfAtomPairs"].ToString());
                if (numOfAtomPairs == -1)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        private string GetAlignedPeptideInterfacePymolScriptByChain(string clusterInterface, string centerInterface, bool isDomainInterface)
        {
            string scriptLine = "";
            if (isDomainInterface)
            {
                scriptLine = GetDomainInterfacePepCompPymolScript (clusterInterface);
            }
            else
            {
                scriptLine = GetPeptideInterfacePymolScript(clusterInterface);
            }
            scriptLine += "align " + clusterInterface + " and chain A, " + centerInterface + " and chain A";
            return scriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private string GetPepInterfaceWithMaxContacts (string[] clusterInterfaces, DataTable peptideInterfaceTable)
        {
            int maxNumOfContacts = 0;
            int numOfContacts = 0;
            int maxResiduePairs = 0;
            int numOfResiduePairs = 0;
            string repPeptideInterface = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string peptideInterface in clusterInterfaces)
            {
                pdbId = peptideInterface.Substring(0, 4);
         //       domainInterfaceId = Convert.ToInt32(peptideInterface.Substring(4, peptideInterface.Length - 4));
                domainInterfaceId = GetDomainInterfaceID(peptideInterface);
                DataRow[] peptideInterfaceRows = peptideInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
                numOfContacts = Convert.ToInt32(peptideInterfaceRows[0]["NumOfAtomPairs"].ToString());
                numOfResiduePairs = Convert.ToInt32 (peptideInterfaceRows[0]["NumOfResiduePairs"].ToString ());
                if (maxNumOfContacts < numOfContacts)
                {
                    maxNumOfContacts = numOfContacts;
                    maxResiduePairs = numOfResiduePairs;
                    repPeptideInterface = peptideInterface;
                }
                else if (maxNumOfContacts == numOfContacts)
                {
                    if (maxResiduePairs < numOfResiduePairs)
                    {
                        maxResiduePairs = numOfResiduePairs;
                        repPeptideInterface = peptideInterface;
                    }
                }
            }
            return repPeptideInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="pfamHmmSiteCompTable"></param>
        /// <returns></returns>
        public string GetDomainInterfaceWithMostCommonHmmSites(string[] clusterInterfaces, DataTable pfamHmmSiteCompTable)
        {
            double maxAverageHmmSites = 0;
            double averageHmmSites = 0;
            string clusterInterfaceWithMaxHmmSites = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                averageHmmSites = GetAverageCommonHmmSites(clusterInterface, clusterInterfaces, pfamHmmSiteCompTable);
                if (maxAverageHmmSites < averageHmmSites)
                {
                    maxAverageHmmSites = averageHmmSites;
                    clusterInterfaceWithMaxHmmSites = clusterInterface;
                }
            }
            if (clusterInterfaceWithMaxHmmSites == "")
            {
                clusterInterfaceWithMaxHmmSites = clusterInterfaces[0];
            }
            return clusterInterfaceWithMaxHmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="pfamHmmSiteCompTable"></param>
        /// <returns></returns>
        private double GetAverageCommonHmmSites(string clusterInterface, string[] clusterInterfaces, DataTable pfamHmmSiteCompTable)
        {
            int totalNumOfCommonHmmSites = 0;
            int numOfCompInterfaces = 0;
            string pdbId = clusterInterface.Substring(0, 4);
            int domainInterfaceId = GetDomainInterfaceID(clusterInterface);
            DataRow[] interfaceHmmCompRows2 = pfamHmmSiteCompTable.Select
                (string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pdbId, domainInterfaceId));
            string compInterface = "";
            foreach (DataRow hmmCompRow in interfaceHmmCompRows2)
            {
                if (clusterInterface.IndexOf(".cryst") > -1)
                {
                    compInterface = hmmCompRow["PdbID2"].ToString() + "_d" + hmmCompRow["DomainInterfaceID2"].ToString() + ".cryst";
                }
                else
                {
                    compInterface = hmmCompRow["PdbID2"].ToString() + "_d" + hmmCompRow["DomainInterfaceID2"].ToString();
                }
                if (Array.IndexOf(clusterInterfaces, compInterface) > -1)
                {
                    totalNumOfCommonHmmSites += Convert.ToInt32(hmmCompRow["NumOfCommonHmmSites"].ToString ());
                    numOfCompInterfaces++;
                }
            }

            DataRow[] interfaceHmmCompRows1 = pfamHmmSiteCompTable.Select
               (string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
            foreach (DataRow hmmCompRow in interfaceHmmCompRows1)
            {
                if (clusterInterface.IndexOf(".cryst") > -1)
                {
                    compInterface = hmmCompRow["PdbID1"].ToString() + "_d" + hmmCompRow["DomainInterfaceID1"].ToString() + ".cryst";
                }
                else
                {
                    compInterface = hmmCompRow["PdbID1"].ToString() + "_d" + hmmCompRow["DomainInterfaceID1"].ToString();
                }
                if (Array.IndexOf(clusterInterfaces, compInterface) > -1)
                {
                    totalNumOfCommonHmmSites += Convert.ToInt32(hmmCompRow["NumOfCommonHmmSites"].ToString());
                    numOfCompInterfaces++;
                }
            }
            double aveNumOfCommonHmmSites = (double)totalNumOfCommonHmmSites / (double)numOfCompInterfaces;
            return aveNumOfCommonHmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        public int GetDomainInterfaceID(string domainInterface)
        {
            int exeIndex = domainInterface.IndexOf(".");
            string domainInterfaceIdString = domainInterface;
            if (exeIndex > -1)
            {
                domainInterfaceIdString = domainInterface.Substring(0, exeIndex);
            }
            string[] fields = domainInterfaceIdString.Split('_');  // domainInterface = pdbId + "_d" + id
            int domainInterfaceId = 0;
            if (fields.Length == 1)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceIdString.Substring (4, domainInterfaceIdString.Length - 4));
            }
            else
            {
                if (fields[1].IndexOf("d") > -1)
                {
                    domainInterfaceId = Convert.ToInt32(fields[1].Substring(1, fields[1].Length - 1));
                }
                else
                {
                    domainInterfaceId = Convert.ToInt32(fields[1]);
                }
            }
            return domainInterfaceId;
        }
        #endregion

        #region interface domain pair_fit
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="centerInterface"></param>
        /// <param name="centerDomainInterfaceHmmSeqIdHashes"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="interfaceChainSeqIdHash"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        public string FormatDomainInterfacePairFitPymolScript(string domainInterface, string centerInterface, Dictionary<int, int> centerDomainInterfaceHmmSeqIdHash,
            DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdHash)
        {
            domainInterface = domainInterface.Replace(".cryst", "");
            string[] seqIdPairsByHmm = MapDomainInterfacesByHmmOrder(domainInterface,
                centerDomainInterfaceHmmSeqIdHash, pfamDomainTable, domainInterfaceTable, interfaceChainCoordSeqIdHash);
            string pairFitScript = "";
            if (seqIdPairsByHmm.Length > 0)
            {
                string[] pairFitSeqRegionStrings = FormatSeqIdPairsToRegions(seqIdPairsByHmm);

                pairFitScript = "pair_fit " + domainInterface + ".cryst//A/" + pairFitSeqRegionStrings[0] + "/CA, " +
                    centerInterface + "//A/" + pairFitSeqRegionStrings[1] + "/CA";
            }
            else
            {
                pairFitScript = "align " + domainInterface + ".cryst and chain A, " + centerInterface + " and chain A";
            }

            return pairFitScript;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="centerInterface"></param>
        /// <param name="centerDomainInterfaceHmmSeqIdHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="interfaceChainCoordSeqIdHash"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        public string FormatDomainInterfacePairFitPymolScript(string domainInterface, string centerInterface, Dictionary<int, int> centerDomainInterfaceHmmSeqIdHash,
            DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdHash, bool isReversed)
        {
            domainInterface = domainInterface.Replace(".cryst", "");
            string[] seqIdPairsByHmm = MapDomainInterfacesByHmmOrder(domainInterface,
                centerDomainInterfaceHmmSeqIdHash, pfamDomainTable, domainInterfaceTable, interfaceChainCoordSeqIdHash);
            string pairFitScript = "";
            if (seqIdPairsByHmm.Length > 0)
            {
                string[] pairFitSeqRegionStrings = FormatSeqIdPairsToRegions(seqIdPairsByHmm);

                pairFitScript = "pair_fit " + domainInterface + ".cryst//A/" + pairFitSeqRegionStrings[0] + "/CA, " +
                    centerInterface + "//A/" + pairFitSeqRegionStrings[1] + "/CA";
            }
            else
            {
                pairFitScript = "align " + domainInterface + ".cryst and chain A, " + centerInterface + " and chain A";
            }

            return pairFitScript;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="centerInterface"></param>
        /// <param name="centerDomainInterfaceHmmSeqIdHashes"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="interfaceChainSeqIdHash"></param>
        /// <param name="isReversed"></param>
        /// <returns></returns>
        private string FormatDomainInterfacePairFitPymolScript(string domainInterface, string centerInterface, Dictionary<int, int>[] centerDomainInterfaceHmmSeqIdHashes,
            DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdHash, int domainNo, out bool isReversed)
        {
            isReversed = false;
            string pairFitScript = "";
            if (domainNo > 0)
            {
                string[] seqIdPairsByHmm = MapDomainInterfacesByHmmOrder(domainInterface, centerDomainInterfaceHmmSeqIdHashes,
                                    pfamDomainTable, domainInterfaceTable, interfaceChainCoordSeqIdHash, domainNo, out isReversed);
                if (seqIdPairsByHmm.Length > 0)
                {
                    return "";
                }
                string[] pairFileSeqRegionStrings = FormatSeqIdPairsToRegions(seqIdPairsByHmm);

                if (domainNo == 1)
                {
                    if (isReversed)
                    {
                        pairFitScript = "pair_fit " + domainInterface + ".cryst//B/" + pairFileSeqRegionStrings[0] + "/CA, " +
                            centerInterface + ".pfam//A/" + pairFileSeqRegionStrings[1] + "/CA";
                    }
                    else
                    {
                        pairFitScript = "pair_fit " + domainInterface + ".cryst//A/" + pairFileSeqRegionStrings[0] + "/CA, " +
                            centerInterface + ".pfam//A/" + pairFileSeqRegionStrings[1] + "/CA";
                    }
                }
                if (domainNo == 2)
                {
                    if (isReversed)
                    {
                        pairFitScript = "pair_fit " + domainInterface + ".cryst//A/" + pairFileSeqRegionStrings[0] + "/CA, " +
                            centerInterface + ".pfam//B/" + pairFileSeqRegionStrings[1] + "/CA";
                    }
                    else
                    {
                        pairFitScript = "pair_fit " + domainInterface + ".cryst//B/" + pairFileSeqRegionStrings[0] + "/CA, " +
                            centerInterface + ".pfam//B/" + pairFileSeqRegionStrings[1] + "/CA";
                    }
                }
            }
            else  // pair_fit both chains
            {
                string[] seqIdPairsByHmm1 = MapDomainInterfacesByHmmOrder(domainInterface, centerDomainInterfaceHmmSeqIdHashes, pfamDomainTable,
                    domainInterfaceTable, interfaceChainCoordSeqIdHash, 1, out isReversed);
                string[] pairFileSeqRegionStrings1 = FormatSeqIdPairsToRegions(seqIdPairsByHmm1);

                string[] seqIdPairsByHmm2 = MapDomainInterfacesByHmmOrder(domainInterface, centerDomainInterfaceHmmSeqIdHashes, pfamDomainTable,
                    domainInterfaceTable, interfaceChainCoordSeqIdHash, 2, out isReversed);
                string[] pairFileSeqRegionStrings2 = FormatSeqIdPairsToRegions(seqIdPairsByHmm2);

                if (isReversed)
                {
                    pairFitScript = "pair_fit " + domainInterface + ".cryst//B/" + pairFileSeqRegionStrings1[0] + "/CA, " +
                            centerInterface + ".pfam//A/" + pairFileSeqRegionStrings1[1] + "/CA";
                    pairFitScript += ("\r\npair_fit " + domainInterface + ".cryst//A/" + pairFileSeqRegionStrings2[0] + "/CA, " +
                            centerInterface + ".pfam//B/" + pairFileSeqRegionStrings2[1] + "/CA");
                }
                else
                {
                    pairFitScript = "pair_fit " + domainInterface + ".cryst//A/" + pairFileSeqRegionStrings1[0] + "/CA, " +
                            centerInterface + ".pfam//A/" + pairFileSeqRegionStrings1[1] + "/CA";
                    pairFitScript += ("\r\npair_fit " + domainInterface + ".cryst//B/" + pairFileSeqRegionStrings2[0] + "/CA, " +
                            centerInterface + ".pfam//B/" + pairFileSeqRegionStrings2[1] + "/CA");
                }
            }
            return pairFitScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="centerDomainInterfaceHmmSeqIdHashes"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="interfaceChainSeqNoHash"></param>
        /// <param name="alignPair"></param>
        /// <returns></returns>
        private string[] MapDomainInterfacesByHmmOrder(string domainInterface, Dictionary<int, int> centerDomainInterfaceHmmSeqIdHash,
            DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdsHash)
        {
            bool isReversed = false;
            Dictionary<int, int>[] domainInterfaceHmmSeqIdHashes = GetDomainInterfaceSequenceHmmSeqIdHash(domainInterface, pfamDomainTable,
                domainInterfaceTable, interfaceChainCoordSeqIdsHash, out isReversed);

            string[] seqIdPairs = MapDomainSeqIdsByHmmOrder(domainInterfaceHmmSeqIdHashes[0], centerDomainInterfaceHmmSeqIdHash);

            return seqIdPairs;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="centerDomainInterfaceHmmSeqIdHashes"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="interfaceChainSeqNoHash"></param>
        /// <param name="alignPair"></param>
        /// <returns></returns>
        private string[] MapDomainInterfacesByHmmOrder(string domainInterface, Dictionary<int, int>[] centerDomainInterfaceHmmSeqIdHashes,
            DataTable pfamDomainTable, DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdHash, int domainNo, out bool isReversed)
        {
            isReversed = false;
            Dictionary<int, int>[] domainInterfaceHmmSeqIdHashes = GetDomainInterfaceSequenceHmmSeqIdHash (domainInterface, pfamDomainTable,
                domainInterfaceTable, interfaceChainCoordSeqIdHash, out isReversed);
            string[] seqIdPairs = null;
            if (domainNo == 1)
            {
                if (isReversed)
                {
                    seqIdPairs = MapDomainSeqIdsByHmmOrder(domainInterfaceHmmSeqIdHashes[1], centerDomainInterfaceHmmSeqIdHashes[0]);
                }
                else
                {
                    seqIdPairs = MapDomainSeqIdsByHmmOrder(domainInterfaceHmmSeqIdHashes[0], centerDomainInterfaceHmmSeqIdHashes[0]);
                }
            }
            else if (domainNo == 2)
            {
                if (isReversed)
                {
                    seqIdPairs = MapDomainSeqIdsByHmmOrder (domainInterfaceHmmSeqIdHashes[0], centerDomainInterfaceHmmSeqIdHashes[1]);
                }
                else
                {
                    seqIdPairs = MapDomainSeqIdsByHmmOrder(domainInterfaceHmmSeqIdHashes[1], centerDomainInterfaceHmmSeqIdHashes[1]);
                }
            }
            return seqIdPairs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="pfamChainDomainTable">PFAM chain domain table which is chain-based</param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="interfaceChainCoordSeqIdsHash">the list of seq ids in coordinates for both chains of an interface,
        /// key: domainInterface_A(B) (domainInterface with no .cryst), value: int[] coordinate seq ids</param>
        /// <returns></returns>
        public Dictionary<int, int>[] GetDomainInterfaceSequenceHmmSeqIdHash(string domainInterface, DataTable pfamChainDomainTable, 
            DataTable domainInterfaceTable, Dictionary<string, int[]> interfaceChainCoordSeqIdsHash, out bool isReversed)
        {
            isReversed = false;
            domainInterface = domainInterface.Replace(".cryst", "");
            string pdbId = domainInterface.Substring(0, 4);
            int domainInterfaceId = GetDomainInterfaceIdFromFileName(domainInterface);
            int[] chainDomainIdPair = GetDomainInterfaceChainDomainPair(pdbId, domainInterfaceId, domainInterfaceTable, out isReversed);
            string entryDomain = pdbId + chainDomainIdPair[0];
            int[] coordSeqIds = interfaceChainCoordSeqIdsHash[domainInterface + "_A"];
            Dictionary<int, int> domainHmmSeqIdHash1 = GetSequenceHmmSeqIdHash(entryDomain, pfamChainDomainTable, coordSeqIds);

            Dictionary<int, int> domainHmmSeqIdHash2 = null;
            if (chainDomainIdPair[1] == chainDomainIdPair[0])
            {
                domainHmmSeqIdHash2 = domainHmmSeqIdHash1;
            }
            else
            {
                entryDomain = pdbId + chainDomainIdPair[1];
                coordSeqIds = (int[])interfaceChainCoordSeqIdsHash[domainInterface + "_B"];
                domainHmmSeqIdHash2 = GetSequenceHmmSeqIdHash(entryDomain, pfamChainDomainTable, coordSeqIds);
            }
            Dictionary<int, int>[] domainHmmSeqIdHashes = new Dictionary<int, int>[2];
            domainHmmSeqIdHashes[0] = domainHmmSeqIdHash1;
            domainHmmSeqIdHashes[1] = domainHmmSeqIdHash2;
            return domainHmmSeqIdHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceFileName"></param>
        /// <returns></returns>
        private int GetDomainInterfaceIdFromFileName(string domainInterfaceFileName)
        {
            domainInterfaceFileName = domainInterfaceFileName.Replace(".cryst", "");
            string[] fields = domainInterfaceFileName.Split('_');
            int domainInterfaceId = 0;
            if (domainInterfaceFileName.IndexOf("_d") > -1)
            {
                domainInterfaceId = Convert.ToInt32(fields[1].Substring(1, fields[1].Length - 1));
            }
            else
            {
                if (fields.Length == 3)
                {
                    domainInterfaceId = Convert.ToInt32(fields[2]);
                }
                else
                {
                    domainInterfaceId = Convert.ToInt32(fields[1]);
                }
            }
            return domainInterfaceId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="isReversed">is the chain domain ids is in the PFAM alphabet order</param>
        /// <returns></returns>
        private int[] GetDomainInterfaceChainDomainPair(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable, out bool isReversed)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            isReversed = false;
            if (domainInterfaceTable.Columns.Contains("IsReversed"))
            {
                if (domainInterfaceRows[0]["IsReversed"].ToString() == "1")
                {
                    isReversed = true;
                }
            }
            int[] domainPair = new int[2];
            domainPair[0] = Convert.ToInt32(domainInterfaceRows[0]["ChainDomainID1"].ToString());
            domainPair[1] = Convert.ToInt32(domainInterfaceRows[0]["ChainDomainID2"].ToString());
            return domainPair;
        }
     
        #endregion

        #region interface domain ranges
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        public Range[][] GetMultiChainInterfaceDomainRanges(string pdbId, int domainInterfaceId, out bool isReversed)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int chainDomainId1 = Convert.ToInt32(domainInterfaceTable.Rows[0]["ChainDomainID1"].ToString());
            int chainDomainId2 = Convert.ToInt32(domainInterfaceTable.Rows[0]["ChainDomainId2"].ToString());
            Range domainRange1 = GetMultiChainDomainFileRange(pdbId, chainDomainId1);
            Range domainRange2 = null;
            if (chainDomainId1 == chainDomainId2)
            {
                domainRange2 = domainRange1;
            }
            else
            {
                domainRange2 = GetMultiChainDomainFileRange(pdbId, chainDomainId2);
            }
            if (domainInterfaceTable.Rows[0]["IsReversed"].ToString() == "1")
            {
                isReversed = true;
            }
            else
            {
                isReversed = false;
            }
            Range[] domainRanges1 = new Range[1];
            domainRanges1[0] = domainRange1;
            Range[] domainRanges2 = new Range[1];
            domainRanges2[0] = domainRange2;

            Range[][] interfaceDomainRanges = new Range[2][];
            interfaceDomainRanges[0] = domainRanges1;
            interfaceDomainRanges[1] = domainRanges2;
            return interfaceDomainRanges;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        public Range[][] GetInterfaceDomainRanges(string pdbId, int domainInterfaceId, out bool isReversed)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long domainId1 = Convert.ToInt64(domainInterfaceTable.Rows[0]["DomainID1"].ToString());
            long domainId2 = Convert.ToInt64(domainInterfaceTable.Rows[0]["DomainID2"].ToString());
            Range[] domainRanges1 = GetDomainRange(pdbId, domainId1);
            Range[] domainRanges2 = null;
            if (domainId1 == domainId2)
            {
                domainRanges2 = domainRanges1;
            }
            else
            {
                domainRanges2 = GetDomainRange(pdbId, domainId2);
            }
            if (domainInterfaceTable.Rows[0]["IsReversed"].ToString() == "1")
            {
                isReversed = true;
            }
            else
            {
                isReversed = false;
            }
            Range[][] interfaceDomainRanges = new Range[2][];
            interfaceDomainRanges[0] = domainRanges1;
            interfaceDomainRanges[1] = domainRanges2;
            return interfaceDomainRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        public Range[][] GetInterfaceDomainRanges(string pdbId, int domainInterfaceId, out int[] interfaceIds)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long domainId1 = Convert.ToInt64(domainInterfaceTable.Rows[0]["DomainID1"].ToString());
            long domainId2 = Convert.ToInt64(domainInterfaceTable.Rows[0]["DomainID2"].ToString());
            Range[] domainRanges1 = GetDomainRange(pdbId, domainId1);
            Range[] domainRanges2 = null;
            if (domainId1 == domainId2)
            {
                domainRanges2 = domainRanges1;
            }
            else
            {
                domainRanges2 = GetDomainRange(pdbId, domainId2);
            }
            List<int> interfaceIdList = new List<int> ();
            int interfaceId = 0;
            foreach (DataRow interfaceDefRow in domainInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(domainInterfaceTable.Rows[0]["InterfaceID"].ToString());
                interfaceIdList.Add(interfaceId);
            }
            interfaceIds = new int[interfaceIdList.Count];
            interfaceIdList.CopyTo(interfaceIds);

            Range[][] interfaceDomainRanges = new Range[2][];
            interfaceDomainRanges[0] = domainRanges1;
            interfaceDomainRanges[1] = domainRanges2;
            return interfaceDomainRanges;
        }
        #endregion
    }
}
