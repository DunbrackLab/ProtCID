using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using AuxFuncLib;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.Crystal;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using InterfaceClusterLib.PymolScript;
using InterfaceClusterLib.InterfaceProcess;
using DbLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    /// <summary>
    /// Calculate RMSD between peptides in domain-peptide interfaces
    /// </summary>
    public class PeptideInterfaceRmsd
    {
        #region member variables
        private CmdOperations untarOperator = new CmdOperations();
        private CmdOperations pymolLauncher = new CmdOperations();
        public PfamClusterFilesCompress fileCompress = new PfamClusterFilesCompress();
        public RmsdCalculator rmsdCal = new RmsdCalculator();
        public StructAlign structAlign = new StructAlign();
        public string atomName = "CA";
        public DbQuery dbQuery = new DbQuery();
        public InterfaceRetriever domainInterfaceReader = new InterfaceRetriever();
        public string rmsdDataFileDir = "";
        public InterfaceAlignPymolScript interfaceAlignPymol = new InterfaceAlignPymolScript();
        public Dictionary<string, long[]>  pfamMultiChainDomainHash = null;
        public DbUpdate dbUpdate = new DbUpdate();
        public string hmmSiteCompTableName = "";
        public int leastNumOfCommonHmmSites = 3;
        public DataTable rmsdTable = null;
        #endregion

        #region constructors
        public PeptideInterfaceRmsd()
        {
            rmsdDataFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
            pfamMultiChainDomainHash = interfaceAlignPymol.GetPfamMultiChainDomainHash();
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            hmmSiteCompTableName = PfamHmmSites.interfaceHmmSiteTableName;
            rmsdTable = new DataTable(hmmSiteCompTableName);
        }
        #endregion

        #region rmsd of peptide interfaces aligned by pair_fit of hmm positions
        /// <summary>
        /// 
        /// </summary>
        public void CalculateDomainInterfacePeptideRmsd()
        {
            ProtCidSettings.tempDir = @"X:\xtal_temp_pep";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptides in domain interfaces after pair_fit by HMM positions");
            ProtCidSettings.logWriter.WriteLine ("Calculate rmsd between peptides in domain interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine (rmsdDataFileDir, "PfamPeptideInterfaceRmsd.txt"), true);
            string queryString = string.Format("Select Distinct PfamID From {0} Where NumOfCommonHmmSites >= 3 and PepComp = '1' AND (ChainRmsd = -1 Or ChainRmsd is null);", hmmSiteCompTableName);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PFAM IDs: " + pfamIdTable.Rows.Count.ToString ());

            string pfamId = "";
            int pfamCount = 1;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString () + ": " + pfamId);
                pfamCount++;

                try
                {
                    CalculatePfamDomainInterfacePeptideRmsd(pfamId, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            rmsdWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            ProtCidSettings.logWriter.WriteLine("RMSD done!");
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CalculatePfamDomainInterfacePeptideRmsd(string pfamId, StreamWriter rmsdWriter)
        {
            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");

            if (File.Exists(pfamPymolScriptFile))
            {
                return;
            }
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId);
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);
            Dictionary<string, string[]> domainInterfacePairHash = GetDomainInterfacePairHash(hmmSiteCompTable);
            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(domainInterfacePairHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;
            
            foreach (string centerInterface in domainInterfacePairHash.Keys)
            {
                string[] compDomainInterfaces = domainInterfacePairHash[centerInterface];

                try
                {
                    CalculateDomainInterfacePeptideRmsd(pfamId, centerInterface, compDomainInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                    domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir);
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="compDomainInterfaces"></param>
        /// <param name="domainInterfaceChainCoordSeqIdsHash"></param>
        /// <param name="pfamPymolPairFitWriter"></param>
        public void CalculateDomainInterfacePeptideRmsd(string pfamId, string centerInterface, string[] compDomainInterfaces, DataTable pfamChainDomainTable, 
           DataTable hmmSiteCompTable, Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash, StreamWriter pfamPymolPairFitWriter, StreamWriter rmsdWriter)
        {
            string[] domainInterfaces = new string[compDomainInterfaces.Length + 1];
            domainInterfaces[0] = centerInterface;
            Array.Copy(compDomainInterfaces, 0, domainInterfaces, 1, compDomainInterfaces.Length);
            DataTable domainInterfaceTable = GetDomainInterfaceDefTable(domainInterfaces);

            CalculateDomainInterfacePeptideRmsd(pfamId, centerInterface, compDomainInterfaces, pfamChainDomainTable,
                                hmmSiteCompTable, domainInterfaceTable, domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="centerInterface"></param>
        /// <param name="compDomainInterfaces"></param>
        /// <param name="pfamChainDomainTable"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="domainInterfaceChainCoordSeqIdsHash"></param>
        /// <param name="pfamPymolPairFitWriter"></param>
        /// <param name="rmsdWriter"></param>
        public void CalculateDomainInterfacePeptideRmsd(string pfamId, string centerInterface, string[] compDomainInterfaces, DataTable pfamChainDomainTable,
           DataTable hmmSiteCompTable, DataTable domainInterfaceTable, Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash, StreamWriter pfamPymolPairFitWriter, StreamWriter rmsdWriter)
        {
            string scriptLine = "";

            string pymolScriptFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface + "_" + compDomainInterfaces[0] + "_pairFit.pml");
            string pymolCoordFile = Path.Combine(ProtCidSettings.tempDir, pfamId + "_" + centerInterface + "_" + compDomainInterfaces[0] + ".coord");

            string[] domainInterfaces = new string[compDomainInterfaces.Length + 1];
            domainInterfaces[0] = centerInterface;
            Array.Copy(compDomainInterfaces, 0, domainInterfaces, 1, compDomainInterfaces.Length);            

            ReadDomainInterfaceChainCoordSeqIds(centerInterface, domainInterfaceChainCoordSeqIdsHash, false);

            StreamWriter pairFitScriptWriter = null;
            try
            {
                pairFitScriptWriter = new StreamWriter(pymolScriptFile);
            }
            catch (Exception ex)
            {
          //      pairFitScriptWriter.Close();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Open PyMol Script File error: " + pymolScriptFile + " " + ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Close it, and return");
                ProtCidSettings.logWriter.WriteLine("Open PyMol Script File error: " + pymolScriptFile + " " + ex.Message);
                ProtCidSettings.logWriter.WriteLine("Close it " + pymolScriptFile + " and return.");
                ProtCidSettings.logWriter.Flush();
                return;
            }
            scriptLine = "load " + centerInterface + ".cryst, format=pdb, object=" + centerInterface;
            pairFitScriptWriter.WriteLine(scriptLine);
            pfamPymolPairFitWriter.WriteLine(scriptLine);

            bool needReversed = false;

            foreach (string compDomainInterface in compDomainInterfaces)
            {
                ProtCidSettings.progressInfo.currentFileName = centerInterface + ";" + compDomainInterface;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                needReversed = IsCompInterfaceNeedReversed(centerInterface, compDomainInterface, hmmSiteCompTable);
                ReadDomainInterfaceChainCoordSeqIds(compDomainInterface, domainInterfaceChainCoordSeqIdsHash, needReversed );

                scriptLine = GetPairFitPeptideInterfacePymolScript(compDomainInterface, centerInterface,
                        pfamChainDomainTable, domainInterfaceTable, domainInterfaceChainCoordSeqIdsHash);

                pairFitScriptWriter.WriteLine(scriptLine);
                pfamPymolPairFitWriter.WriteLine(scriptLine);
            }

            pairFitScriptWriter.WriteLine("center " + centerInterface + ".cryst");
            pfamPymolPairFitWriter.WriteLine("center " + centerInterface + ".cryst");

            string coordFileLinux = pymolCoordFile.Replace("\\", "/");
            pairFitScriptWriter.WriteLine("cmd.save (\"" + coordFileLinux + "\")");
            pairFitScriptWriter.WriteLine("quit");
            pairFitScriptWriter.Close();

            pfamPymolPairFitWriter.WriteLine("cmd.save (\"" + coordFileLinux + "\")");
            pfamPymolPairFitWriter.WriteLine();
            pfamPymolPairFitWriter.Flush();

            try
            {
                pymolLauncher.RunPymol(pymolScriptFile);
              
                CalculateRmsdFromPymolCoordOutput(pfamId, -1, pymolScriptFile, domainInterfaces, pymolCoordFile, hmmSiteCompTable, rmsdWriter);
                
                File.Delete(pymolScriptFile);
                File.Delete(pymolCoordFile);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating rmsd error: " + pfamId + " " + centerInterface + " " + ex.Message);
                ProtCidSettings.logWriter.WriteLine("Calculating rmsd error: " + pfamId + " " + centerInterface + " " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }                  
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerInterface"></param>
        /// <param name="compInterface"></param>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private bool IsCompInterfaceNeedReversed(string centerInterface, string compInterface, DataTable hmmSiteCompTable)
        {
            string[] centerInterfaceInfo = GetDomainInterfaceInfo(centerInterface);
            string[] compInterfaceInfo = GetDomainInterfaceInfo(compInterface);
            DataRow[] hmmSiteCompRows = hmmSiteCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' AND " +
                " PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", centerInterfaceInfo[0], centerInterfaceInfo[1],
                compInterfaceInfo[0], compInterfaceInfo[1]));
            if (hmmSiteCompRows.Length > 0)
            {
                if (hmmSiteCompTable.Columns.Contains("ChainNo"))
                {
                    string chainNo = hmmSiteCompRows[0]["ChainNo"].ToString();
                    if (chainNo == "B")
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
        /// <param name="centerInterface"></param>
        /// <param name="compInterface"></param>
        /// <param name="pfamChainDomainTable"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="domainInterfaceChainCoordSeqIdHash"></param>
        private string GetPairFitPeptideInterfacePymolScript (string compInterface, string centerInterface, DataTable pfamChainDomainTable, 
            DataTable domainInterfaceTable, Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash)
        {
        //    string pymolScriptLine = "load " + centerInterface + "\r\n";
            string pymolScriptLine = "load " + compInterface + ".cryst, format=pdb, object=" + compInterface + "\r\n";
            centerInterface = centerInterface + ".cryst";

            bool isCenterReversed = false;
            Dictionary <int, int>[] centerHmmSeqIdsHashes = interfaceAlignPymol.GetDomainInterfaceSequenceHmmSeqIdHash(centerInterface, pfamChainDomainTable,
                domainInterfaceTable, domainInterfaceChainCoordSeqIdHash, out isCenterReversed);

            string pairFitLine = interfaceAlignPymol.FormatDomainInterfacePairFitPymolScript(compInterface, centerInterface, centerHmmSeqIdsHashes[0],
                    pfamChainDomainTable, domainInterfaceTable, domainInterfaceChainCoordSeqIdHash); 

            pymolScriptLine += pairFitLine;

            return pymolScriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        public DataTable GetDomainInterfaceDefTable(string[] domainInterfaces)
        {
            DataTable domainInterfaceDefTable = null;
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = interfaceAlignPymol.GetDomainInterfaceID(domainInterface);
                DataTable peptideInterfaceTable = GetDomainInterfaceDefTable (pdbId, domainInterfaceId);
                ParseHelper.AddNewTableToExistTable(peptideInterfaceTable, ref domainInterfaceDefTable);
            }
            return domainInterfaceDefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceDefTable(string pdbId, int domainInterfaceId)
        {
            // try to fit into the domain interfaces
            string queryString = string.Format("Select RelSeqId, PdbID, InterfaceID, DomainInterfaceID, " +
               " DomainID As DomainID1, AsymChain As AsymChain1, ChainDomainID As ChainDomainID1, " +
          //     " PepDomainID As DomainID2, PepAsymChain As AsymChain2, PepChainDomainID As ChainDomainID2, NumOfAtomPairs, NumOfResiduePairs " +
               " PepDomainID As DomainID2, PepAsymChain As AsymChain2, PepChainDomainID As ChainDomainID2" +
               " From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (domainInterfaceTable.Rows.Count == 0)
            {
                queryString = string.Format("Select RelSeqID, PdbID, InterfaceID, DomainInterfaceID, " + 
                    " DomainID1, AsymChain1, ChainDomainID1, DomainID2, AsymChain2, ChainDomainID2 " + 
                    " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
                domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            return domainInterfaceTable;
        }


       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="domainInterfaceChainCoordHash"></param>
        /// <returns></returns>
        public void ReadDomainInterfaceChainCoordSeqIds(string domainInterface, Dictionary<string, int[]> domainInterfaceChainCoordHash, bool needReversed)
        {
            if (! domainInterfaceChainCoordHash.ContainsKey(domainInterface + "_A"))
            {
                string hashFolder = "";
                if (domainInterface.IndexOf("_d") > -1)
                {
                    hashFolder = Path.Combine(fileCompress.domainInterfaceFileDir, domainInterface.Substring(1, 2));
                }
                else
                {
                    hashFolder = Path.Combine(fileCompress.chainInterfaceFileDir, domainInterface.Substring(1, 2));
                }
                string gzInterfaceFile = Path.Combine(hashFolder, domainInterface + ".cryst.gz");
                string interfaceFile = Path.Combine (ProtCidSettings.tempDir, domainInterface + ".cryst");
                if (! File.Exists(interfaceFile))
                {
                    interfaceFile = ParseHelper.UnZipFile(gzInterfaceFile, ProtCidSettings.tempDir);
                }
                if (needReversed)
                {
                    fileCompress.ReverseChainInterfaceFile(interfaceFile);
                }
                int[][] chainCoordsSeqIds = fileCompress.ReadInterfaceChainCalphaCoordinates(interfaceFile);
                domainInterfaceChainCoordHash.Add(domainInterface + "_A", chainCoordsSeqIds[0]);
                domainInterfaceChainCoordHash.Add(domainInterface + "_B", chainCoordsSeqIds[1]);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        public DataTable GetPfamChainDomainTable (DataTable hmmSiteCompTable)
        {
            string pfamId = hmmSiteCompTable.Rows[0]["PfamID"].ToString().TrimEnd();
            long[] domainIds = GetAlignDomainsOfDomainInterfaces(hmmSiteCompTable);
            DataTable pfamChainDomainTable = interfaceAlignPymol.GetPfamChainDomainTable(pfamId, domainIds);
            if (pfamMultiChainDomainHash.ContainsKey(pfamId))
            {
                interfaceAlignPymol.UpdatePfamMultiChainDomains(pfamChainDomainTable, (long[])pfamMultiChainDomainHash[pfamId]);
            }
            return pfamChainDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteTable"></param>
        /// <returns></returns>
        private long[] GetAlignDomainsOfDomainInterfaces(DataTable hmmSiteTable)
        {
            List<long> alignDomainList = new List<long> ();
            long domainId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteTable.Rows)
            {
                domainId = Convert.ToInt64(hmmSiteRow["DomainID1"].ToString());
                if (!alignDomainList.Contains(domainId))
                {
                    alignDomainList.Add(domainId);
                }
                domainId = Convert.ToInt64(hmmSiteRow["DomainID2"].ToString ());
                if (!alignDomainList.Contains(domainId))
                {
                    alignDomainList.Add(domainId);
                }
            }
            return alignDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetDomainInterfacePairHash(DataTable hmmSiteCompTable)
        {
            Dictionary<string, List<string>> domainInterfacePairListHash = new Dictionary<string, List<string>>();
            string domainInterface1 = "";
            string domainInterface2 = "";
            foreach (DataRow hmmSiteCompRow in hmmSiteCompTable.Rows)
            {
                domainInterface1 = hmmSiteCompRow["PdbID1"].ToString() + "_d" + hmmSiteCompRow["DomainInterfaceID1"].ToString();
                domainInterface2 = hmmSiteCompRow["PdbID2"].ToString() + "_d" + hmmSiteCompRow["DomainInterfaceID2"].ToString();
                if (domainInterface2 == domainInterface1)
                {
                    continue;
                }
                if (domainInterfacePairListHash.ContainsKey(domainInterface1))
                {
                    if (!domainInterfacePairListHash[domainInterface1].Contains(domainInterface2))
                    {
                        domainInterfacePairListHash[domainInterface1].Add(domainInterface2);
                    }
                }
                else
                {
                    List<string> interfacePairList = new List<string> ();
                    interfacePairList.Add(domainInterface2);
                    domainInterfacePairListHash.Add(domainInterface1, interfacePairList);
                }
            }
            List<string> domainInterfaceList = new List<string>(domainInterfacePairListHash.Keys);
            Dictionary<string, string[]> domainInterfacePairHash = new Dictionary<string, string[]>();
            foreach (string domainInterface in domainInterfaceList)
            {
                domainInterfacePairHash[domainInterface] = domainInterfacePairListHash[domainInterface].ToArray ();
            }
            return domainInterfacePairHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfacePairHash"></param>
        /// <returns></returns>
        public int GetTotalInterfacePairs(Dictionary<string, string[]> interfacePairHash)
        {
            int totalInterfacePairs = 0;
            foreach (string centerInterface in interfacePairHash.Keys)
            {
                totalInterfacePairs += interfacePairHash[centerInterface].Length;
            }
            return totalInterfacePairs;
        }
        #endregion

        #region update peptide Rmsd
        /// <summary>
        /// No need to delete the RMSD data, since when updating the common hmm sites, the corresponding data rows are updated. 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateDomainInterfacePeptideRmsd(Dictionary<string, string[]> updatePfamEntryDict)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptides in domain interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideInterfaceRmsd_update.txt"), true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PFAM IDs: " + updatePfamEntryDict.Count.ToString());
            int pfamCount = 1;
            foreach (string pfamId in updatePfamEntryDict.Keys)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString() + ": " + pfamId);
                
                pfamCount++;
                if (pfamId != "Peptidase_C14")
                {
                    continue;
                }
                try
                {
                    string[] pfamUpdateEntries = updatePfamEntryDict[pfamId];
                    CalculatePfamDomainInterfacePeptideRmsd(pfamId, pfamUpdateEntries, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                ProtCidSettings.logWriter.WriteLine(pfamCount.ToString() + ": " + pfamId);
                ProtCidSettings.logWriter.Flush();
            }
            rmsdWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CalculatePfamDomainInterfacePeptideRmsd(string pfamId, string[] updateEntries, StreamWriter rmsdWriter)
        {           
            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId);
            if (hmmSiteCompTable.Rows.Count == 0)
            {
                ProtCidSettings.logWriter.WriteLine(pfamId + " no hmm sites comp data in " + hmmSiteCompTableName);
                ProtCidSettings.logWriter.Flush();
                return;
            }

            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");
            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);
            //     DataTable domainInterfaceTable = GetDomainInterfaceTable(pfamId);
            Dictionary<string, string[]> domainInterfacePairHash = GetDomainInterfacePairHash(hmmSiteCompTable);
            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(domainInterfacePairHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            string centerPdbId = "";
            string[] compDomainInterfaces = null;
            foreach (string centerInterface in domainInterfacePairHash.Keys)
            {
                centerPdbId = centerInterface.Substring(0, 4);
                if (updateEntries.Contains(centerPdbId))
                {
                    compDomainInterfaces = (string[])domainInterfacePairHash[centerInterface];
                }
                else
                {
                    compDomainInterfaces = GetCompDomainInterfaces((string[])domainInterfacePairHash[centerInterface], updateEntries);
                }
                if (compDomainInterfaces.Length == 0)
                {
                    continue;
                }
                  
                try
                {
                    CalculateDomainInterfacePeptideRmsd(pfamId, centerInterface, compDomainInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                    domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir);
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetCompDomainInterfaces(string[] domainInterfaces, string[] updateEntries)
        {
            List<string> compDomainInterfaceList = new List<string> ();
            string pdbId = "";
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                if (updateEntries.Contains(pdbId))
                {
                    compDomainInterfaceList.Add(domainInterface);
                }
            }
            return compDomainInterfaceList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetUpdatePfamIds(string[] updateEntries)
        {
            Dictionary <string, List<string>> updatePfamEntryListHash = new Dictionary<string,List<string>> ();
            foreach (string pdbId in updateEntries)
            {
                string[] entryPfamIds = GetEntryPfams(pdbId);
                foreach (string pfamId in entryPfamIds)
                {
                    if (updatePfamEntryListHash.ContainsKey(pfamId))
                    {
                        updatePfamEntryListHash[pfamId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        updatePfamEntryListHash.Add(pfamId, entryList);
                    }
                }
            }
            Dictionary<string, string[]> updatePfamEntryHash = new Dictionary<string, string[]>();
            foreach (string pfamId in updatePfamEntryListHash.Keys)
            {
                updatePfamEntryHash.Add (pfamId, updatePfamEntryListHash[pfamId].ToArray ());
            }
            return updatePfamEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPfams(string pdbId)
        {
            string queryString = string.Format("Select Distinct PfamID From {0} WHere PdbID = '{1}' AND NumOfCommonHmmSites >= 3 and PepComp = '1';",
                hmmSiteCompTableName, pdbId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] entryPfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                entryPfamIds[count] = pfamIdRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return entryPfamIds;
        }
        #endregion

        #region rmsd of peptide and domain from existing pymol script
        /// <summary>
        /// 
        /// </summary>
        public void CalculateRmsdFromPymolScriptFiles()
        {
            if (! Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            string pymolScriptDir = Path.Combine (rmsdDataFileDir, "PepDomainRmsd");
            if (! Directory.Exists (pymolScriptDir))
            {
                Directory.CreateDirectory(pymolScriptDir);
            }
            string dataStorageDir = Path.Combine (rmsdDataFileDir, "rmsdTemp");
            if (!Directory.Exists(dataStorageDir))
            {
                Directory.CreateDirectory(dataStorageDir);
            }
          //  string[] pymolScriptFiles = Directory.GetFiles(pymolScriptDir, "*.pml");
            string[] pymolScriptFiles = { @"D:\DbProjectData\pfam\PfamPeptide\PepDomainRmsd\Pkinase_pairFit.pml" };
            string workingPymolScriptFile = "";
            StreamWriter rmsdWriter = new StreamWriter(Path.Combine (rmsdDataFileDir,  "PepDomainRmsd_newCal.txt"), true);
            ProtCidSettings.progressInfo.currentOperationLabel = "Calculate Pep Domain RMSD";
            foreach (string pymolScriptFile in pymolScriptFiles)
            {
                FileInfo fileInfo = new FileInfo(pymolScriptFile);
                workingPymolScriptFile = Path.Combine(ProtCidSettings.tempDir, fileInfo.Name);
                File.Copy(pymolScriptFile, workingPymolScriptFile, true);

                CalculateRmsdFromPymolScriptFile(workingPymolScriptFile, rmsdWriter, dataStorageDir);
            }
            rmsdWriter.Close ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScript"></param>
        public void CalculateRmsdFromPymolScriptFile(string pymolScript, StreamWriter rmsdWriter, string dataStorageDir)
        {
            FileInfo fileInfo = new FileInfo(pymolScript);
            string dataDir = fileInfo.DirectoryName;
            string pfamId = fileInfo.Name.Replace("_pairFit.pml", "");

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId);

         //   string[] interfacePymolScriptFiles = SeparatePymolScriptFile(pymolScript);
            string[] interfacePymolScriptFiles = Directory.GetFiles(dataDir, "*.pml");

            ProtCidSettings.progressInfo.totalOperationNum = interfacePymolScriptFiles.Length;
            ProtCidSettings.progressInfo.totalStepNum = interfacePymolScriptFiles.Length;

            string pymolCoordFile = "";
            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId);
            foreach (string interfacePymolScriptFile in interfacePymolScriptFiles)
            {
                fileInfo = new FileInfo(interfacePymolScriptFile);
                ProtCidSettings.progressInfo.currentFileName = fileInfo.Name;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (interfacePymolScriptFile == pymolScript)
                {
                    continue;
                }
                pymolCoordFile = interfacePymolScriptFile.Replace(".pml", ".coord");
                string[] alignedInterfaces = GetAlignedInterfaces(interfacePymolScriptFile);
                if (!File.Exists(pymolCoordFile))
                {
                    CopyInterfaceFilesToWorkingDir(alignedInterfaces, dataDir);
                    pymolLauncher.RunPymol(interfacePymolScriptFile);
                    if (File.Exists(pymolCoordFile))
                    {
                        CalculateDifRmsdFromPymolCoordOutput(pfamId, interfacePymolScriptFile, alignedInterfaces, pymolCoordFile, hmmSiteCompTable, rmsdWriter);
                    }
                }
            }
            // delete the interface files, save the pymol script and coord files 
            string[] crystFiles = Directory.GetFiles(dataDir, "*.cryst");
            foreach (string crystFile in crystFiles)
            {
                File.Delete(crystFile);
            }
            string[] pymolFiles = Directory.GetFiles(dataDir, "*.pml");
            string destFile = "";
            foreach (string pymolFile in pymolFiles)
            {
                fileInfo = new FileInfo(pymolFile);
                destFile = Path.Combine(dataStorageDir, fileInfo.Name);
                File.Move(pymolFile, destFile);
            }
            string[] coordFiles = Directory.GetFiles(dataDir, "*.coord");
            foreach (string coordFile in coordFiles)
            {
                fileInfo = new FileInfo(coordFile);
                destFile = Path.Combine(dataStorageDir, fileInfo.Name);
                File.Move (coordFile, destFile);
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
        private void CalculateDifRmsdFromPymolCoordOutput (string pfamId, string rmsdPymolScriptFile, string[] clusterInterfaces, 
            string coordinateFile, DataTable hmmSiteCompTable, StreamWriter dataWriter)
        {
            Dictionary<string, Range[][]> interfacePairAlignedRangesHash = ReadInterfacePairAlignedRangeHash(rmsdPymolScriptFile);

            //      Hashtable interfaceCoordinateHash = ReadInterfaceCoordinates(clusterInterfaces, coordinateFile);
            DomainInterface[] peptideInterfaces = ReadDomainInterfaces(clusterInterfaces, coordinateFile);
            //      Coordinate[][] centerInterfaceCoordinates = (Coordinate[][])interfaceCoordinateHash[clusterInterfaces[0]];
            DomainInterface centerDomainInterface = peptideInterfaces[0];
            Range[][] alignedRanges = null;
            Range[] centerRanges = null;
            Range[] compRanges = null;
            Range[] rmsdAlignedRanges = null;
            Range centerPepRange = GetPeptideInteractingRange(centerDomainInterface);
            Range[] centerPepRanges = new Range[1];
            centerPepRanges[0] = centerPepRange;
            Range compPepRange = null;
            int numOfComHmmSites = 0;
            double rmsd = 0;
            double[] selectRmsds = null;
            int[] numOfResiduePairs = null;
            double[] interfaceRmsds = new double[4];
            string dataLine = "";
            for (int i = 1; i < clusterInterfaces.Length; i++)
            {
                alignedRanges = null;
                if (interfacePairAlignedRangesHash.ContainsKey(clusterInterfaces[i]))
                {
                    alignedRanges = (Range[][])interfacePairAlignedRangesHash[clusterInterfaces[i]];
                }
                dataLine = pfamId + "\t" + clusterInterfaces[0] + "\t" + clusterInterfaces[i] + "\t";

                DomainInterface compDomainInterface = peptideInterfaces[i];
                if (alignedRanges == null)
                {
                    centerRanges = null;
                    compRanges = null;
                }
                else
                {
                    compRanges = alignedRanges[0];
                    centerRanges = alignedRanges[1];
                }
                // the number of common hmm sites
                numOfComHmmSites = GetNumOfCommonHmmSites(centerDomainInterface.pdbId, centerDomainInterface.domainInterfaceId,
                    compDomainInterface.pdbId, compDomainInterface.domainInterfaceId, hmmSiteCompTable);
                dataLine = dataLine + numOfComHmmSites.ToString() + '\t';

                // rmsd for the whole domains
                Coordinate[] centerChainCoords = GetAlignedChainCoordinates(centerDomainInterface.chain1, null, atomName);
                Coordinate[] compChainCoords = GetAlignedChainCoordinates(compDomainInterface.chain1, null, atomName);
                rmsd = rmsdCal.CalculateMinRmsd(centerChainCoords, compChainCoords);
                dataLine = dataLine + rmsd.ToString() + "\t";
                interfaceRmsds[0] = rmsd;
                // rmsd for the aligned regions of domains 
                Coordinate[] alignedCenterCoords = GetAlignedChainCoordinates(centerDomainInterface.chain1, centerRanges, atomName);
                Coordinate[] alignedCompCoords = GetAlignedChainCoordinates(compDomainInterface.chain1, compRanges, atomName);
                rmsd = rmsdCal.CalculateRmsd(alignedCenterCoords, alignedCompCoords);
                dataLine = dataLine + rmsd.ToString() + "\t";
                interfaceRmsds[1] = rmsd;

                // rmsd for the entire peptides by simple linear square fit
                rmsd = rmsdCal.CalculateLinearMinRmsd(centerDomainInterface.chain2, compDomainInterface.chain2, atomName, out rmsdAlignedRanges);
                dataLine = dataLine + rmsd.ToString() + "\t" + FormatRange(rmsdAlignedRanges[0]) + "\t" + FormatRange(rmsdAlignedRanges[1]) + "\t";
                interfaceRmsds[2] = rmsd;

                compPepRange = GetPeptideInteractingRange(compDomainInterface);
                Range[] compPepRanges = new Range[1];
                compPepRanges[0] = compPepRange;
                Coordinate[] alignedCenterPepCoords = GetAlignedChainCoordinates(centerDomainInterface.chain2, centerPepRanges, atomName);
                Coordinate[] alignedCompPepCoords = GetAlignedChainCoordinates(compDomainInterface.chain2, compPepRanges, atomName);
                rmsd = rmsdCal.CalculateLinearMinRmsd(alignedCenterPepCoords, alignedCompPepCoords, out rmsdAlignedRanges);
                dataLine = dataLine + rmsd.ToString() + "\t" + FormatRange(centerPepRange) + "\t" + FormatRange(compPepRange);
                interfaceRmsds[3] = rmsd;

                Coordinate[] centerPepCoords = GetAlignedChainCoordinates(centerDomainInterface.chain2, null, atomName);
                Coordinate[] compPepCoords = GetAlignedChainCoordinates(compDomainInterface.chain2, null, atomName);
                selectRmsds = rmsdCal.CalculateRmsd(centerPepCoords, compPepCoords, out numOfResiduePairs);
                dataLine = dataLine + "\t" + selectRmsds[0].ToString() + "\t" + selectRmsds[1] + "\t" + numOfResiduePairs[0].ToString() + "\t" + numOfResiduePairs[1].ToString (); 

                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
        //        InsertInterfaceRmsdIntoDb(clusterInterfaces[0], clusterInterfaces[i], interfaceRmsds);
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScript"></param>
        /// <returns></returns>
        private string[] SeparatePymolScriptFile(string pymolScript)
        {
            StreamReader dataReader = new StreamReader(pymolScript);
            FileInfo fileInfo = new FileInfo(pymolScript);
            StreamWriter dataWriter = null;
            string interfacePymolScriptFile = "";
            string line = "";
            string scriptLine = "";
            string centerInterface = "";
            List<string> pymolFileList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                scriptLine += (line + "\r\n");
                if (line.IndexOf("cmd.save") > -1)
                {
                    dataWriter = new StreamWriter(interfacePymolScriptFile);
                    dataWriter.WriteLine(scriptLine);
                    dataWriter.WriteLine("quit");
                    dataWriter.Close();
                    scriptLine = "";
                }
                else if (line.IndexOf ("center") > -1)
                {
                    centerInterface = line.Replace ("center", "");
                    centerInterface = centerInterface.Replace (".cryst", "").Trim ();
                    interfacePymolScriptFile = fileInfo.Name.Replace("pairFit", centerInterface);
                    interfacePymolScriptFile = Path.Combine(fileInfo.DirectoryName, interfacePymolScriptFile);
                    pymolFileList.Add(interfacePymolScriptFile);
                }
            }
            dataReader.Close();
            if (scriptLine != "")
            {
                dataWriter = new StreamWriter(interfacePymolScriptFile);
                dataWriter.WriteLine(scriptLine);
                dataWriter.WriteLine("quit");
                dataWriter.Close();
            }
            return pymolFileList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfacePymolScript"></param>
        /// <returns></returns>
        private string[] GetAlignedInterfaces(string interfacePymolScript)
        {
            List<string> interfaceList = new List<string> ();
            StreamReader dataReader = new StreamReader(interfacePymolScript);
            string line = "";
            string interfaceName = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    interfaceName = line.Substring("load".Length, line.Length - "load".Length).Trim();
                    interfaceName = interfaceName.Replace(".cryst", "");
                    interfaceList.Add(interfaceName);
                }
            }
            dataReader.Close();
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFiles"></param>
        /// <param name="destDir"></param>
        private void CopyInterfaceFilesToWorkingDir(string[] interfaceFiles, string destDir)
        {
            string hashFolder = "";
            string gzInterfaceFile = "";
            foreach (string interfaceFile in interfaceFiles)
            {
                hashFolder = Path.Combine(fileCompress.domainInterfaceFileDir, interfaceFile.Substring(1, 2));
                gzInterfaceFile = Path.Combine(hashFolder, interfaceFile + ".cryst.gz");
                ParseHelper.UnZipFile(gzInterfaceFile, destDir);
            }
        }
        #endregion

        #region insert rmsd into db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterface1"></param>
        /// <param name="pepInterface2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pepInterface1, string pepInterface2, double[] rmsds)
        {
            string[] pepInterfaceInfo1 = GetDomainInterfaceInfo(pepInterface1);
            string pdbId1 = pepInterfaceInfo1[0];
            int domainInterfaceId1 = Convert.ToInt32(pepInterfaceInfo1[1]);
            string[] pepInterfaceInfo2 = GetDomainInterfaceInfo(pepInterface2);
            string pdbId2 = pepInterfaceInfo2[0];
            int domainInterfaceId2 = Convert.ToInt32(pepInterfaceInfo2[1]);

            InsertInterfaceRmsdIntoDb(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, rmsds);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, double[] rmsds)
        {
            string updateString = "";
            if (IsUpdateReversed(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2))
            {
                updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, PepRmsd = {3}, InteractPepRmsd = {4}" + 
                   " Where PdbID1 = '{5}' AND DomainInterfaceId1 = {6} AND PdbID2 = '{7}' AND DomainInterfaceID2 = {8};",
                   hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1);
            }
            else
            {
                updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, PepRmsd = {3}, InteractPepRmsd = {4}" +
                  " Where PdbID1 = '{5}' AND DomainInterfaceId1 = {6} AND PdbID2 = '{7}' AND DomainInterfaceID2 = {8};",
                  hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            }
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        /// <summary>
        /// sit
        /// </summary>
        /// <param name="pepInterface1"></param>
        /// <param name="pepInterface2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pepInterface1, string pepInterface2, double[] rmsds, StructAlignOutput alignOut, string chainId)
        {
            string[] pepInterfaceInfo1 = GetDomainInterfaceInfo(pepInterface1);
            string pdbId1 = pepInterfaceInfo1[0];
            int domainInterfaceId1 = Convert.ToInt32(pepInterfaceInfo1[1]);
            string[] pepInterfaceInfo2 = GetDomainInterfaceInfo(pepInterface2);
            string pdbId2 = pepInterfaceInfo2[0];
            int domainInterfaceId2 = Convert.ToInt32(pepInterfaceInfo2[1]);

            InsertInterfaceRmsdIntoDb(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, chainId, rmsds, alignOut);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="rmsds"></param>
        public void InsertInterfaceRmsdIntoDb(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, string chainId, double[] rmsds, StructAlignOutput alignOut)
        {
            string updateString = "";
            /*      if (IsUpdateReversed(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2))
                  {
                      updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, " +
                         " PepRmsd = {3}, InteractPepRmsd = {4}, LocalPepRmsd = {5}, PepStart = {6}, PepEnd = {7}, " +
                         " ChainStart = {8}, ChainEnd = {9}, PepAlignment = '{10}', ChainAlignment = '{11}', Score = {12}" +
                         " Where PdbID1 = '{13}' AND DomainInterfaceId1 = {14} AND PdbID2 = '{15}' AND DomainInterfaceID2 = {16} AND ChainNo = '{17}';",
                         hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], alignOut.rmsd, alignOut.startPos1, alignOut.endPos1,
                         alignOut.startPos2, alignOut.endPos2, alignOut.alignment1, alignOut.alignment2, alignOut.alignScore,
                         pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1, chainId);
                  }
                  else
                  {
                       updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, " +
                         " PepRmsd = {3}, InteractPepRmsd = {4}, LocalPepRmsd = {5}, PepStart = {6}, PepEnd = {7}, " +
                         " ChainStart = {8}, ChainEnd = {9}, PepAlignment = '{10}', ChainAlignment = '{11}', Score = {12}" +
                         " Where PdbID1 = '{13}' AND DomainInterfaceId1 = {14} AND PdbID2 = '{15}' AND DomainInterfaceID2 = {16} AND ChainNo = '{17}';",
                         hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], alignOut.rmsd, alignOut.startPos2, alignOut.endPos2,
                         alignOut.startPos1, alignOut.endPos1, alignOut.alignment2, alignOut.alignment1, alignOut.alignScore,
                         pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, chainId);
                  }*/

            if (IsUpdateReversed(pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2))
            {
                if (alignOut == null)
                {
                    updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, PepRmsd = {3}, InteractPepRmsd = {4} " + 
                        " Where PdbID1 = '{5}' AND DomainInterfaceId1 = {6} AND PdbID2 = '{7}' AND DomainInterfaceID2 = {8} AND ChainNo = '{9}';",
                       hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1, chainId);
                }
                else
                {
                    updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, " +
                       " PepRmsd = {3}, InteractPepRmsd = {4}, LocalPepRmsd = {5}, PepStart = {6}, PepEnd = {7}, " +
                       " ChainStart = {8}, ChainEnd = {9}, PepAlignment = '{10}', ChainAlignment = '{11}', Score = {12}" +
                       " Where PdbID1 = '{13}' AND DomainInterfaceId1 = {14} AND PdbID2 = '{15}' AND DomainInterfaceID2 = {16} AND ChainNo = '{17}';",
                       hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], alignOut.rmsd, alignOut.startPos2, alignOut.endPos2,
                       alignOut.startPos1, alignOut.endPos1, alignOut.alignment2, alignOut.alignment1, alignOut.alignScore,
                       pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1, chainId);

                }
            }
            else
            {
                if (alignOut == null)
                {
                    updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, PepRmsd = {3}, InteractPepRmsd = {4} " +
                        " Where PdbID1 = '{5}' AND DomainInterfaceId1 = {6} AND PdbID2 = '{7}' AND DomainInterfaceID2 = {8} AND ChainNo = '{9}';",
                       hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, chainId);
                }
                else
                {
                    updateString = string.Format("Update {0} Set ChainRmsd = {1}, InteractChainRmsd = {2}, " +
                      " PepRmsd = {3}, InteractPepRmsd = {4}, LocalPepRmsd = {5}, PepStart = {6}, PepEnd = {7}, " +
                      " ChainStart = {8}, ChainEnd = {9}, PepAlignment = '{10}', ChainAlignment = '{11}', Score = {12}" +
                      " Where PdbID1 = '{13}' AND DomainInterfaceId1 = {14} AND PdbID2 = '{15}' AND DomainInterfaceID2 = {16} AND ChainNo = '{17}';",
                      hmmSiteCompTableName, rmsds[0], rmsds[1], rmsds[2], rmsds[3], alignOut.rmsd, alignOut.startPos1, alignOut.endPos1,
                      alignOut.startPos2, alignOut.endPos2, alignOut.alignment1, alignOut.alignment2, alignOut.alignScore,
                      pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, chainId);
                }
            }
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private bool IsUpdateReversed (string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select * From  {0} " +
                " Where PdbID1 = '{1}' AND DomainInterfaceId1 = {2} AND PdbID2 = '{3}' AND DomainInterfaceID2 = {4};",
                hmmSiteCompTableName, pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1);
            DataTable pepInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pepInterfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region rmsd of peptide clusters aligned by pair_fit of hmm positions
        /// <summary>
        /// 
        /// </summary>
        public void CalculateClusterInterfaceRmsd()
        {
            string pepClusterTarFileDir = Path.Combine(rmsdDataFileDir, "PepClusters");
            if (!Directory.Exists(pepClusterTarFileDir))
            {
                Directory.CreateDirectory(pepClusterTarFileDir); ;
            }
            string tempDir = @"C:\temp";
            if (! Directory.Exists (tempDir))
            {
                Directory.CreateDirectory (tempDir);
            }
            StreamWriter dataWriter = new StreamWriter(Path.Combine (pepClusterTarFileDir, "PeptideRmsd.txt"), true);
            dataWriter.WriteLine("PFAMID\tClusterID\tCenterInterface\tCompInterface\tNumOfComHmmSites\tChainRmsd\tAlignChainRmsd\tPeptideRmsd\t" + 
                "CenterPepAlignRange\tCompPepAlignRange\tAlignPepRmsd");
            string[] clusterTarFiles = Directory.GetFiles(pepClusterTarFileDir, "*.tar");
       /*     string[] clusterTarFiles = { @"D:\DbProjectData\pfam\PfamPeptide\PepClusters\Hormone_recep.tar", 
                                       @"D:\DbProjectData\pfam\PfamPeptide\PepClusters\MHC_I.tar", 
                                       @"D:\DbProjectData\pfam\PfamPeptide\PepClusters\V-set.tar"};*/
            foreach (string clusterTarFile in clusterTarFiles)
            {
                try
                {
                    CalculateClusterInterfaceRmsd(clusterTarFile, tempDir, dataWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterTarFile + " Calculating RMSD error: " + ex.Message);
                }
                finally
                {
                    string[] files = Directory.GetFiles(tempDir);
                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }
                }
            }
            dataWriter.Close();
            try
            {
                Directory.Delete(tempDir);
            }
            catch {}
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceTarFile"></param>
        public void CalculateClusterInterfaceRmsd(string interfaceTarFile, string unTarFileDir, StreamWriter dataWriter)
        {
            untarOperator.UnTar(interfaceTarFile, unTarFileDir);
            string[] clusterTarFiles = Directory.GetFiles(unTarFileDir, "*.tar.gz");
            string clusterPymolScriptFile = "";
            string rmsdPymolScriptFile = "";
            string coordinateFile = "";
            string[] clusterInterfaces = null;
            string oldUnTarFileDir = unTarFileDir;
            bool isClusterSubFolder = false;
            string pfamId = GetPfamIdFromPfamTarFile(interfaceTarFile);
            DataTable hmmSiteCompTable = GetPfamHmmSiteCompTable(pfamId);
            int clusterId = 0;
            
            foreach (string clusterTarFile in clusterTarFiles)
            {
                isClusterSubFolder = false;
                FileInfo fileInfo = new FileInfo(clusterTarFile);
                clusterId = GetClusterIdFromClusterTarFile(fileInfo);
                untarOperator.UnTar(clusterTarFile, unTarFileDir);
                clusterPymolScriptFile = Path.Combine(unTarFileDir, fileInfo.Name.Replace(".tar.gz", "_pairFit.pml"));
                if (!File.Exists(clusterPymolScriptFile))
                {
                    string clusterUnTarFileDir = Path.Combine(unTarFileDir, fileInfo.Name.Replace(".tar.gz", ""));
                    if (Directory.Exists(clusterUnTarFileDir))
                    {
                        unTarFileDir = clusterUnTarFileDir;
                        isClusterSubFolder = true;
                    }
                }
                clusterPymolScriptFile = Path.Combine(unTarFileDir, fileInfo.Name.Replace(".tar.gz", "_pairFit.pml"));
                rmsdPymolScriptFile = AddCoordinateSaveToPymolScript(clusterPymolScriptFile, out coordinateFile, out clusterInterfaces);
                pymolLauncher.RunPymol(rmsdPymolScriptFile);

                CalculateRmsdFromPymolCoordOutput(pfamId, clusterId, rmsdPymolScriptFile, clusterInterfaces, coordinateFile, hmmSiteCompTable, dataWriter);

                ClearInterfaceFiles(unTarFileDir);
                if (isClusterSubFolder)
                {
                    unTarFileDir = oldUnTarFileDir;
                }
            }
            dataWriter.Flush();
            // clear all the cluster tar.gz files
            string[] targzFiles = Directory.GetFiles(unTarFileDir, "*.tar.gz");
            foreach (string targzFile in targzFiles)
            {
                File.Delete(targzFile);
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
        private void CalculateRmsdFromPymolCoordOutput(string pfamId, int clusterId, string rmsdPymolScriptFile, string[] clusterInterfaces, 
            string coordinateFile, DataTable hmmSiteCompTable, StreamWriter dataWriter)
        {
            Dictionary<string, Range[][]> interfacePairAlignedRangesHash = ReadInterfacePairAlignedRangeHash(rmsdPymolScriptFile);

            //      Hashtable interfaceCoordinateHash = ReadInterfaceCoordinates(clusterInterfaces, coordinateFile);
            DomainInterface[] peptideInterfaces = ReadDomainInterfaces(clusterInterfaces, coordinateFile);
            //      Coordinate[][] centerInterfaceCoordinates = (Coordinate[][])interfaceCoordinateHash[clusterInterfaces[0]];
            DomainInterface centerDomainInterface = peptideInterfaces[0];
            Range[][] alignedRanges = null;
            Range[] centerRanges = null;
            Range[] compRanges = null;
            Range[] rmsdAlignedRanges = null;
            Range centerPepRange = GetPeptideInteractingRange(centerDomainInterface);
            Range[] centerPepRanges = new Range[1];
            centerPepRanges[0] = centerPepRange;
            Range compPepRange = null;
        //    int numOfComHmmSites = 0;
            string hmmSiteCompInfo = "";
            double rmsd = 0;
            double[] interfaceRmsds = new double[4];
            StructAlignOutput localAlignOut = null; 
            string dataLine = "";
            for (int i = 1; i < clusterInterfaces.Length; i++)
            {
               
                alignedRanges = null;
                if (interfacePairAlignedRangesHash.ContainsKey(clusterInterfaces[i]))
                {
                    alignedRanges = (Range[][])interfacePairAlignedRangesHash[clusterInterfaces[i]];
                }
                dataLine = pfamId + "\t" + clusterId.ToString() + "\t" +
                   clusterInterfaces[0] + "\t" + clusterInterfaces[i] + "\t";

                DomainInterface compDomainInterface = peptideInterfaces[i];
                if (alignedRanges == null)
                {
                    centerRanges = null;
                    compRanges = null;
                }
                else
                {
                    compRanges = alignedRanges[0];
                    centerRanges = alignedRanges[1];
                }
                // the number of common hmm sites
          //      numOfComHmmSites = GetNumOfCommonHmmSites(centerDomainInterface.pdbId, centerDomainInterface.domainInterfaceId,
                hmmSiteCompInfo = GetHmmSiteCompInfo(centerDomainInterface.pdbId, centerDomainInterface.domainInterfaceId,
                    compDomainInterface.pdbId, compDomainInterface.domainInterfaceId, hmmSiteCompTable);
              //  dataLine = dataLine + numOfComHmmSites.ToString() + '\t';

                dataLine = dataLine + hmmSiteCompInfo + '\t';

                // rmsd for the whole domains
                Coordinate[] centerChainCoords = GetAlignedChainCoordinates(centerDomainInterface.chain1, null, atomName);
                Coordinate[] compChainCoords = GetAlignedChainCoordinates(compDomainInterface.chain1, null, atomName);
                rmsd = rmsdCal.CalculateMinRmsd(centerChainCoords, compChainCoords);
                dataLine = dataLine + rmsd.ToString() + "\t";
                interfaceRmsds[0] = rmsd;
                // rmsd for the aligned regions of domains 
                Coordinate[] alignedCenterCoords = GetAlignedChainCoordinates(centerDomainInterface.chain1, centerRanges, atomName);
                Coordinate[] alignedCompCoords = GetAlignedChainCoordinates(compDomainInterface.chain1, compRanges, atomName);
                rmsd = rmsdCal.CalculateRmsd(alignedCenterCoords, alignedCompCoords);
                dataLine = dataLine + rmsd.ToString() + "\t";
                interfaceRmsds[1] = rmsd;

                // rmsd for the entire peptides by simple linear square fit
                rmsd = rmsdCal.CalculateLinearMinRmsd(centerDomainInterface.chain2, compDomainInterface.chain2, atomName, out rmsdAlignedRanges);
                dataLine = dataLine + rmsd.ToString() + "\t" + FormatRange(rmsdAlignedRanges[0]) + "\t" + FormatRange(rmsdAlignedRanges[1]) + "\t";
                interfaceRmsds[2] = rmsd;

                compPepRange = GetPeptideInteractingRange(compDomainInterface);
                Range[] compPepRanges = new Range[1];
                compPepRanges[0] = compPepRange;
                Coordinate[] alignedCenterPepCoords = GetAlignedChainCoordinates(centerDomainInterface.chain2, centerPepRanges, atomName);
                Coordinate[] alignedCompPepCoords = GetAlignedChainCoordinates(compDomainInterface.chain2, compPepRanges, atomName);
                rmsd = rmsdCal.CalculateLinearMinRmsd(alignedCenterPepCoords, alignedCompPepCoords, out rmsdAlignedRanges);
                dataLine = dataLine + rmsd.ToString() + "\t" + FormatRange(compPepRange) + "\t" + FormatRange(centerPepRange);
                interfaceRmsds[3] = rmsd;

                if (hmmSiteCompTable.Columns.Contains("ChainNO"))
                {
                    try
                    {
                        AtomInfo[] centerCalphaAtoms = GetAlignedCalphaAtoms(centerDomainInterface.chain2);
                        AtomInfo[] compCalphaAtoms = GetAlignedCalphaAtoms(compDomainInterface.chain2);
                        localAlignOut = structAlign.AlignTwoSetsAtoms(centerCalphaAtoms, compCalphaAtoms, "local");
                        dataLine = dataLine + "\t" + FormatStructAlignOutPut(localAlignOut);

                        StructAlignOutput globalAlignOut = structAlign.AlignTwoSetsAtoms(centerCalphaAtoms, compCalphaAtoms, "global");
                        dataLine = dataLine + "\t" + FormatStructAlignOutPut(globalAlignOut);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterInterfaces[0] + " " + clusterInterfaces[i] + "Structure alignment error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pfamId + " " + clusterInterfaces[0] + " " + clusterInterfaces[i] + "Structure alignment error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                    string[] hmmCompInfoFields = hmmSiteCompInfo.Split('\t');
                    string chainId = hmmCompInfoFields[hmmCompInfoFields.Length - 1];
                    InsertInterfaceRmsdIntoDb (clusterInterfaces[0], clusterInterfaces[i], interfaceRmsds, localAlignOut, chainId);
                }
                else
                {
                    InsertInterfaceRmsdIntoDb(clusterInterfaces[0], clusterInterfaces[i], interfaceRmsds);
                }

                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignOut"></param>
        /// <returns></returns>
        private string FormatStructAlignOutPut(StructAlignOutput alignOut)
        {
            string structAlignOut = "";

            if (alignOut != null)
            {
                structAlignOut = alignOut.alignment1 + "\t" + alignOut.alignment2 + "\t" +
                    alignOut.startPos1.ToString() + "\t" + alignOut.endPos1.ToString() + "\t" +
                    alignOut.startPos2.ToString() + "\t" + alignOut.endPos2.ToString() + "\t" +
                    alignOut.alignScore.ToString() + "\t" + alignOut.rmsd.ToString();
            }
            else
            {
                structAlignOut = "\t\t\t\t\t\t\t";
            }
            return structAlignOut;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        private string FormatRange(Range range)
        {
            string rangeString = "[" + range.startPos.ToString() + "-" + range.endPos.ToString() + "]";
            return rangeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupTarFile"></param>
        /// <returns></returns>
        private string GetPfamIdFromPfamTarFile(string groupTarFile)
        {
            FileInfo fileInfo = new FileInfo(groupTarFile);
            string pfamId = fileInfo.Name.Replace(".tar", ""); ;
            return pfamId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetPfamHmmSiteCompTable(string pfamId)
        {
           string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND (PepRmsd is NULL OR PepRmsd = -1);", hmmSiteCompTableName, pfamId);
       //      string queryString = string.Format("Select * From {0} Where pfamID = '{1}';", hmmSiteCompTableName, pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetPfamHmmSiteCompTable(string pfamId, int numOfCommonHmmSites)
        {
            //   string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND (PepRmsd is NULL OR PepRmsd = -1);", hmmSiteCompTableName, pfamId);
            // may need include the numOfCommonHmmSites
            string queryString = string.Format("Select * From {0} Where pfamID = '{1}' AND NumOfCommonHmmSites >= {2} and (PepRmsd = -1 OR PepRmsd is NULL);", 
                hmmSiteCompTableName, pfamId, numOfCommonHmmSites);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataTable bestHmmSiteCompTable = GetPfamHmmSiteCompTable(hmmSiteCompTable);
            return bestHmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private DataTable GetPfamHmmSiteCompTable(DataTable hmmSiteCompTable)
        {
            DataTable bestHmmSiteCompTable = hmmSiteCompTable.Clone();

            string[] interfacePairs = GetInterfacePairs(hmmSiteCompTable);
            string pdbId1 = "";
            int domainInterfaceId1 = 0;
            string pdbId2 = "";
            int domainInterfaceId2 = 0;
            foreach (string interfacePair in interfacePairs)
            {
                string[] fields = interfacePair.Split('_');
                pdbId1 = fields[0].Substring(0, 4);
                domainInterfaceId1 = Convert.ToInt32(fields[0].Substring(4, fields[0].Length - 4));
                pdbId2 = fields[1].Substring(0, 4);
                domainInterfaceId2 = Convert.ToInt32(fields[1].Substring(4, fields[1].Length - 4));
                DataRow[] hmmSiteCompRows = hmmSiteCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " + 
                    "AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
                DataRow bestHmmSiteRow = GetBestHmmSiteCompRow(hmmSiteCompRows);
                DataRow newHmmSiteCompRow = bestHmmSiteCompTable.NewRow();
                newHmmSiteCompRow.ItemArray = bestHmmSiteRow.ItemArray;
                bestHmmSiteCompTable.Rows.Add(newHmmSiteCompRow);
            }
            return bestHmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompRows"></param>
        /// <returns></returns>
        private DataRow GetBestHmmSiteCompRow(DataRow[] hmmSiteCompRows)
        {
            int maxNumOfComHmmSites = 0;
            int numOfComHmmSites = 0;
            int numOfHmmSites2 = 0;
            int maxNumOfHmmSites2 = 0;
            DataRow bestHmmSiteCompRow = null;
            foreach (DataRow hmmSiteCompRow in hmmSiteCompRows)
            {
                numOfComHmmSites = Convert.ToInt32(hmmSiteCompRow["NumOfCommonHmmSites"].ToString ());
                if (maxNumOfComHmmSites < numOfComHmmSites)
                {
                    bestHmmSiteCompRow = hmmSiteCompRow;
                    maxNumOfComHmmSites = numOfComHmmSites;
                }
                else if (maxNumOfComHmmSites == numOfComHmmSites)
                {
                    numOfHmmSites2 = Convert.ToInt32(hmmSiteCompRow["NumOfHmmSites2"].ToString ());
                    maxNumOfHmmSites2 = Convert.ToInt32(bestHmmSiteCompRow["NumOfHmmSites2"].ToString ());
                    if (maxNumOfHmmSites2 < numOfHmmSites2)
                    {
                        bestHmmSiteCompRow = hmmSiteCompRow;
                    }
                }
            }
            return bestHmmSiteCompRow;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSiteCompTable"></param>
        /// <returns></returns>
        private string[] GetInterfacePairs(DataTable hmmSiteCompTable)
        {
            List<string> interfacePairList = new List<string> ();
            string interfacePair = "";
            foreach (DataRow compRow in hmmSiteCompTable.Rows)
            {
                interfacePair = compRow["PdbId1"].ToString() + compRow["DomainInterfaceID1"].ToString() + "_" +
                    compRow["PdbID2"].ToString() + compRow["DomainInterfaceID2"].ToString();
                if (!interfacePairList.Contains(interfacePair))
                {
                    interfacePairList.Add(interfacePair);
                }
            }
            return interfacePairList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamPepDomainHmmSiteCompTable(string pfamId)
        {
            string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND PepComp = '1' AND PepRmsd > -1;", hmmSiteCompTableName, pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterTarFileInfo"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetClusterIdFromClusterTarFile(FileInfo clusterTarFileInfo)
        {
            string clusterTarFileName = clusterTarFileInfo.Name;
            string clusterFileName = clusterTarFileName.Replace(".tar.gz", "");
            int lastDashIndex = clusterTarFileInfo.Name.LastIndexOf("_");
            int clusterId = Convert.ToInt32(clusterFileName.Substring (lastDashIndex + 1, clusterFileName.Length - lastDashIndex - 1));
            return clusterId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScript"></param>
        /// <returns></returns>
        private string[] GetClusterInterfacesFromPymolScript(string pymolScript)
        {
            List<string> interfaceList = new List<string> ();
            StreamReader dataReader = new StreamReader(pymolScript);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    string[] fields = line.Split(' ');
                    interfaceList.Add(fields[1]);
                }
            }
            dataReader.Close();
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <returns></returns>
        private Dictionary<string, Coordinate[][]> ReadInterfaceCoordinates (string[] clusterInterfaces, string coordinateFile)
        {
            StreamReader dataReader = new StreamReader(coordinateFile);
            string line = "";
            int interfaceCount = 0;
            string currentChainId = "";
            string preChainId = "";
            Dictionary<string, Coordinate[][]> interfaceCoordinateHash = new Dictionary<string,Coordinate[][]> ();
            List<Coordinate> chainACoordList = new List<Coordinate> ();
            List<Coordinate> chainBCoordList = new List<Coordinate> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") < 0)
                {
                    continue;
                }

                string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                if (atomFields[2] == atomName)
                {
                    Coordinate xyz = new Coordinate();
                    currentChainId = atomFields[5];
                    xyz.X = Convert.ToDouble(atomFields[8]);
                    xyz.Y = Convert.ToDouble(atomFields[9]);
                    xyz.Z = Convert.ToDouble(atomFields[10]);
                    if (preChainId != "" && currentChainId != preChainId && currentChainId == "A" && preChainId == "B")
                    {
                        Coordinate[][] interfaceCoordinates = new Coordinate[2][];
                        interfaceCoordinates[0] = chainACoordList.ToArray ();
                        interfaceCoordinates[1] = chainBCoordList.ToArray ();
                        interfaceCoordinateHash.Add(clusterInterfaces[interfaceCount], interfaceCoordinates);
                        chainACoordList.Clear();
                        chainBCoordList.Clear();

                        interfaceCount++;
                    }
                    if (currentChainId == "A")
                    {
                        chainACoordList.Add(xyz);
                    }
                    else
                    {
                        chainBCoordList.Add(xyz);
                    }
                    preChainId = currentChainId;
                }
            }
            dataReader.Close();

            // add the coordinates for the last interface
            Coordinate[][] lastInterfaceCoordinates = new Coordinate[2][];
            lastInterfaceCoordinates[0] = chainACoordList.ToArray ();
            lastInterfaceCoordinates[1] = chainBCoordList.ToArray ();
            interfaceCoordinateHash.Add(clusterInterfaces[interfaceCount], lastInterfaceCoordinates);

            return interfaceCoordinateHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="coordinateFile"></param>
        /// <returns></returns>
        private DomainInterface[] ReadDomainInterfaces(string[] clusterInterfaces, string coordinateFile)
        {
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();
            StreamReader dataReader = new StreamReader(coordinateFile);
            string line = "";
            int interfaceCount = 0;
            string currentChainId = "";
            string preChainId = "";
            List<AtomInfo> chainAtomAList = new List<AtomInfo> ();
            List<AtomInfo> chainAtomBList = new List<AtomInfo> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") < 0)
                {
                    continue;
                }
                string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                AtomInfo atom = new AtomInfo();
                atom.atomId = Convert.ToInt32(atomFields[1]);
                atom.atomName = atomFields[2];
                atom.residue = atomFields[4];
                atom.seqId = atomFields[6];
                atom.xyz.X = Convert.ToDouble(atomFields[8]);
                atom.xyz.Y = Convert.ToDouble(atomFields[9]);
                atom.xyz.Z = Convert.ToDouble(atomFields[10]);

                currentChainId = atomFields[5];

                if (preChainId != "" && currentChainId != preChainId && currentChainId == "A" && preChainId == "B")
                {
                    DomainInterface peptideInterface = GetDomainInterface(clusterInterfaces[interfaceCount], chainAtomAList, chainAtomBList);

                    domainInterfaceList.Add(peptideInterface);

                    chainAtomAList.Clear();
                    chainAtomBList.Clear();

                    interfaceCount++;
                }
                if (currentChainId == "A")
                {
                    chainAtomAList.Add(atom);
                }
                else
                {
                    chainAtomBList.Add(atom);
                }
                preChainId = currentChainId;
            }
            dataReader.Close();

            DomainInterface lastPeptideInterface = GetDomainInterface(clusterInterfaces[interfaceCount], chainAtomAList, chainAtomBList);
            domainInterfaceList.Add(lastPeptideInterface);
            DomainInterface[] domainInterfaces = new DomainInterface[domainInterfaceList.Count];
            domainInterfaceList.CopyTo(domainInterfaces);

            return domainInterfaces;
        }

        /// <summary>
        /// Read domain interface from the pymol output
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="chainAtomAList"></param>
        /// <param name="chainAtomBList"></param>
        /// <returns></returns>
        private DomainInterface GetDomainInterface(string domainInterface, List<AtomInfo> chainAtomAList, List<AtomInfo> chainAtomBList)
        {
            DomainInterface peptideInterface = new DomainInterface();
            peptideInterface.chain1 = chainAtomAList.ToArray ();
            peptideInterface.chain2 = chainAtomBList.ToArray ();

            string[] domainInterfaceInfo = GetDomainInterfaceInfo(domainInterface);

            peptideInterface.pdbId = domainInterfaceInfo[0];
            peptideInterface.domainInterfaceId = Convert.ToInt32(domainInterfaceInfo[1]);

            peptideInterface.GetInterfaceResidueDist();

            return peptideInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingFileDir"></param>
        private void ClearInterfaceFiles(string workingFileDir)
        {
        /*    string[] allDirFiles = Directory.GetFiles(workingFileDir);
            foreach (string dirFile in allDirFiles)
            {
                File.Delete(dirFile);
            }*/
            string[] interfaceFiles = Directory.GetFiles(workingFileDir, "*.cryst");
            foreach (string interfaceFile in interfaceFiles)
            {
                File.Delete(interfaceFile);
            }
            string[] pmlFiles = Directory.GetFiles(workingFileDir, "*.pml");
            foreach (string pmlFile in pmlFiles)
            {
                File.Delete(pmlFile);
            }

            string[] coordFiles = Directory.GetFiles(workingFileDir, "*.coord");
            foreach (string coordFile in coordFiles)
            {
                File.Delete(coordFile);
            }
        }

        /// <summary> 
        /// 
        /// </summary>
        /// <param name="clusterPymolScript"></param>
        /// <returns></returns>
        private string AddCoordinateSaveToPymolScript(string clusterPymolScript, out string coordinateFile, out string[] clusterInterfaces)
        {
            FileInfo fileInfo = new FileInfo (clusterPymolScript );
            string clusterPymolScriptRootName = GetClusterFileRootName(clusterPymolScript);
            coordinateFile = Path.Combine (fileInfo.DirectoryName, clusterPymolScriptRootName + ".coord");
            StreamReader dataReader = new StreamReader(clusterPymolScript);
            string rmsdPymolScript = Path.Combine(fileInfo.DirectoryName, clusterPymolScriptRootName + "_coord.pml");
            StreamWriter dataWriter = new StreamWriter(rmsdPymolScript);
            string line = "";
            List<string> clusterInterfaceList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    string[] fields = line.Split(' ');
                    clusterInterfaceList.Add(fields[1]);
                    dataWriter.WriteLine(line);
                }
                else if (line.IndexOf("pair_fit") > -1  || line.IndexOf ("align") > -1)
                {
                    dataWriter.WriteLine(line);
                }
                else if (line.IndexOf("center") > -1)
                {
                    dataWriter.WriteLine(line);
                }
            }
            string coordFileLinux = coordinateFile.Replace("\\", "/");
            dataWriter.WriteLine("cmd.save (\"" + coordFileLinux + "\")");
            dataWriter.WriteLine ("quit");

            dataReader.Close();
            dataWriter.Close();

            clusterInterfaces = new string[clusterInterfaceList.Count];
            clusterInterfaceList.CopyTo(clusterInterfaces);

            return rmsdPymolScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <returns></returns>
        private Dictionary<string, Range[][]> ReadInterfacePairAlignedRangeHash(string pymolScriptFile)
        {
            Dictionary<string, Range[][]> interfacePairAlignedRangeHash = new Dictionary<string, Range[][]>();

            StreamReader dataReader = new StreamReader(pymolScriptFile);
            string line = "";
            string compDomainInterface = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("pair_fit") > -1 )
                {
                    compDomainInterface = ReadCrystInterfaceName(line);
                    compDomainInterface = compDomainInterface.Replace(".cryst", "");
                    if (interfacePairAlignedRangeHash.ContainsKey(compDomainInterface))
                    {
                        continue;
                    }
                    Range[][] alignedRanges = GetPairAlignedRanges(line);
                    interfacePairAlignedRangeHash.Add(compDomainInterface, alignedRanges);
                }
                else if (line.IndexOf("align") > -1)  // aligned the whole chain/domain
                {
                    compDomainInterface = ReadCrystInterfaceName(line);
                    compDomainInterface = compDomainInterface.Replace(".cryst", "");
                    interfacePairAlignedRangeHash.Add(compDomainInterface, null);
                }
            }
            dataReader.Close(); 
            return interfacePairAlignedRangeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignLine"></param>
        /// <returns></returns>
        private string ReadCrystInterfaceName(string alignLine)
        {
            string[] fields = alignLine.Split(' ');
            string compInterfaceField = fields[1];
            int crystIndex = compInterfaceField.IndexOf(".cryst");
            string compInterface = compInterfaceField.Substring(0, crystIndex + ".cryst".Length);
            return compInterface;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pairFileLine"></param>
        /// <returns></returns>
        private Range[][] GetPairAlignedRanges(string pairFitLine)
        {
            string[] fields = pairFitLine.Split(' ');
            string compRangeLine = fields[1].TrimEnd(',');
            string[] compRangeFields = ParseHelper.SplitPlus(compRangeLine, '/');
            Range[] compAlignedRanges = GetAlignedRanges(compRangeFields[2]);

            string refRangeLine = fields[2];
            string[] refRangeFields = ParseHelper.SplitPlus(refRangeLine, '/');
            Range[] refAlignedRanges = GetAlignedRanges(refRangeFields[2]);

            Range[][] alignedRanges = new Range[2][];
            alignedRanges[0] = compAlignedRanges;
            alignedRanges[1] = refAlignedRanges;
            return alignedRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rangeLine"></param>
        /// <returns></returns>
        private Range[] GetAlignedRanges(string rangeLine)
        {
            string[] rangeFields = rangeLine.Split('+');
            Range[] alignedRanges = new Range[rangeFields.Length];
            for (int i = 0; i < rangeFields.Length; i++)
            {
                string[] regionFields = rangeFields[i].Split('-');
                Range alignedRange = new Range();
                if (regionFields.Length == 2)
                {
                    alignedRange.startPos = Convert.ToInt32(regionFields[0]);
                    alignedRange.endPos = Convert.ToInt32(regionFields[1]);
                }
                else
                {
                    alignedRange.startPos = Convert.ToInt32(regionFields[0]);
                    alignedRange.endPos = Convert.ToInt32(regionFields[0]);
                }
                alignedRanges[i] = alignedRange;
            }
            return alignedRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterFile"></param>
        /// <returns></returns>
        private string GetClusterFileRootName(string clusterFile)
        {
            int lastDashIndex = clusterFile.LastIndexOf('_');
            return clusterFile.Substring(0, lastDashIndex);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        private Range GetPeptideInteractingRange (DomainInterface domainInterface)
        {
            Range pepRange = new Range ();
            pepRange.startPos = -1;
            pepRange.endPos = -1;
            int pepSeqId = 0;
            foreach (string seqPair in domainInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqPair.Split('_');
                pepSeqId = Convert.ToInt32 (seqIds[1]);
                if (pepRange.startPos == -1 || pepRange.startPos > pepSeqId)
                {
                    pepRange.startPos = pepSeqId;
                }
                if (pepRange.endPos == -1 || pepRange.endPos < pepSeqId)
                {
                    pepRange.endPos = pepSeqId;
                }
            }
            return pepRange;
        }
        #endregion

        #region get coordinates
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private AtomInfo[] GetAlignedCalphaAtoms (AtomInfo[] chain)
        {
            List<AtomInfo> calphaAtomList = new List<AtomInfo> ();
            foreach (AtomInfo atom in chain)
            {
                if (atom.atomName == "CA")
                {
                    calphaAtomList.Add(atom);
                }
            }
            return calphaAtomList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private Coordinate[] GetAlignedChainCoordinates(AtomInfo[] chain, Range[] alignedRanges)
        {
            List<Coordinate> coordinateList = new List<Coordinate> ();
            if (alignedRanges == null)
            {
                foreach (AtomInfo atom in chain)
                {
                    coordinateList.Add(atom.xyz);
                }
            }
            else
            {
                int seqId = 0;
                foreach (AtomInfo atom in chain)
                {
                    seqId = Convert.ToInt32(atom.seqId);
                    if (IsResidueAligned(seqId, alignedRanges))
                    {
                        coordinateList.Add(atom.xyz);
                    }
                }
            }
            return coordinateList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private Coordinate[] GetAlignedChainCoordinates(AtomInfo[] chain, Range[] alignedRanges, string atomName)
        {
            List<Coordinate> coordinateList = new List<Coordinate> ();
            if (alignedRanges == null)
            {
                foreach (AtomInfo atom in chain)
                {
                    if (atom.atomName == atomName)
                    {
                        coordinateList.Add(atom.xyz);
                    }
                }
            }
            else
            {
                int seqId = 0;
                foreach (AtomInfo atom in chain)
                {
                    if (atom.atomName == atomName)
                    {
                        seqId = Convert.ToInt32(atom.seqId);
                        if (IsResidueAligned(seqId, alignedRanges))
                        {
                            coordinateList.Add(atom.xyz);
                        }
                    }
                }
            }
            return coordinateList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="alignedRanges"></param>
        /// <returns></returns>
        private bool IsResidueAligned(int seqId, Range[] alignedRanges)
        {
            foreach (Range alignRange in alignedRanges)
            {
                if (seqId >= alignRange.startPos && seqId <= alignRange.endPos)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region number of common hmm sites
        /// <summary>
        /// 
        /// </summary>
        public void AddNumOfCommonHmmSites()
        {
            string pepClusterTarFileDir = @"D:\DbProjectData\pfam\PfamPeptide\clustering";

            StreamReader dataReader = new StreamReader(Path.Combine(pepClusterTarFileDir, "PeptideRmsd_maxCom_1.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepClusterTarFileDir, "PeptideRmsd_maxCom.txt"));
            string line = dataReader.ReadLine ();
            dataWriter.WriteLine (line);
            string centerPdbId = "";
            int centerDomainInterfaceId = 0;
            string compPdbId = "";
            int compDomainInterfaceId = 0;
            int numOfComHmmSites = 0;
            string dataLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                string[] centerDomainInterfaceInfo = GetDomainInterfaceInfo(fields[2]);
                string[] compDomainInterfaceInfo = GetDomainInterfaceInfo(fields[3]);
                centerPdbId = centerDomainInterfaceInfo[0];
                centerDomainInterfaceId = Convert.ToInt32(centerDomainInterfaceInfo[1]);
                compPdbId = compDomainInterfaceInfo[0];
                compDomainInterfaceId = Convert.ToInt32(compDomainInterfaceInfo[1]);
                numOfComHmmSites = GetNumOfCommonHmmSites(centerPdbId, centerDomainInterfaceId, compPdbId, compDomainInterfaceId);
                dataLine = "";
                for (int i = 0; i < fields.Length - 1; i ++)
                {
                    dataLine += (fields[i] + "\t");
                }
                dataLine = dataLine + numOfComHmmSites.ToString();
                dataWriter.WriteLine(dataLine);
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerInterface"></param>
        /// <param name="compInterface"></param>
        /// <param name="pfamHmmSiteCompTable"></param>
        /// <returns></returns>
        private int GetNumOfCommonHmmSites(string centerInterface, string compInterface, DataTable pfamHmmSiteCompTable)
        {
            string[] centerInterfaceInfo = GetDomainInterfaceInfo(centerInterface);
            string centerPdbId = centerInterfaceInfo[0];
            int centerDomainInterfaceId = Convert.ToInt32(centerInterfaceInfo[1]);
            string[] compInterfaceInfo = GetDomainInterfaceInfo(compInterface);
            string compPdbId = compInterfaceInfo[0];
            int compDomainInterfaceId = Convert.ToInt32(compInterfaceInfo[1]);

            int numOfComHmmSites = GetNumOfCommonHmmSites(centerPdbId, centerDomainInterfaceId, compPdbId, compDomainInterfaceId, pfamHmmSiteCompTable);
            return numOfComHmmSites;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private int GetNumOfCommonHmmSites(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select NumOfCommonHmmSites From {0} " +
                "WHere (PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND PdbID2 = '{3}' AND DomainInterfaceID2 = {4}) OR " +
                " (PdbID1 = '{3}' AND DomainInterfaceID1 = {4} AND PdbID2 = '{1}' AND DomainInterfaceID2 = {2});",
                hmmSiteCompTableName, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable comHmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);
            int numOfComHmmSites = 0;
            if (comHmmSiteTable.Rows.Count > 0)
            {
                numOfComHmmSites = Convert.ToInt32(comHmmSiteTable.Rows[0]["NumOfCommonHmmSites"].ToString());
            }
            return numOfComHmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private int GetNumOfCommonHmmSites(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, DataTable pfamHmmSiteCompTable)
        {
            DataRow[] hmmSiteCompRows = pfamHmmSiteCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceId1 = '{1}' " + 
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
            int numOfComHmmSites = 0;
            if (hmmSiteCompRows.Length == 0)
            {
                hmmSiteCompRows = pfamHmmSiteCompTable.Select(string.Format("PdbID1 = '{2}' AND DomainInterfaceId1 = '{3}' " +
                " AND PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
            }
            if (hmmSiteCompRows.Length > 0)
            {
                numOfComHmmSites = Convert.ToInt32(hmmSiteCompRows[0]["NumOfCommonHmmSites"].ToString());
            }
            return numOfComHmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private string GetHmmSiteCompInfo (string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2, DataTable pfamHmmSiteCompTable)
        {
            DataRow[] hmmSiteCompRows = pfamHmmSiteCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceId1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
            if (hmmSiteCompRows.Length == 0)
            {
                hmmSiteCompRows = pfamHmmSiteCompTable.Select(string.Format("PdbID1 = '{2}' AND DomainInterfaceId1 = '{3}' " +
                " AND PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2));
            }
            string hmmSiteCompInfo = "";
            if (hmmSiteCompRows.Length > 0)
            {
                hmmSiteCompInfo = hmmSiteCompRows[0]["NumOfCommonHmmSites"].ToString() + "\t" +
                    hmmSiteCompRows[0]["DomainID2"].ToString();
            }
            else
            {
                hmmSiteCompInfo = "-1\t-1";
            }
            if (pfamHmmSiteCompTable.Columns.Contains("ChainNo"))
            {
                if (hmmSiteCompRows.Length > 0)
                {
                    hmmSiteCompInfo = hmmSiteCompInfo + "\t" + hmmSiteCompRows[0]["ChainNo"].ToString();
                }
                else
                {
                    hmmSiteCompInfo = hmmSiteCompInfo + "\t-";
                }
            }
            return hmmSiteCompInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        public string[] GetDomainInterfaceInfo(string domainInterface)
        {
            domainInterface = domainInterface.Replace(".cryst", "");
            string[] fields = domainInterface.Split('_');
            string[] domainInterfaceInfo = new string[2];
            if (fields.Length == 1)
            {
                domainInterfaceInfo[0] = domainInterface.Substring(0, 4);
                domainInterfaceInfo[1] = domainInterface.Substring (4, domainInterface.Length - 4);
            }
            else
            {
                domainInterfaceInfo[0] = fields[0];
                domainInterfaceInfo[1] = fields[1].Replace("d", ""); // remove d before the domain interface id if exist
            }
            return domainInterfaceInfo;
        }
        #endregion

        #region print RMSD and #Common HMM sites
        public void PrintRmsdAndNumCommonHmmSites ()
        {
            Dictionary<string, int> pfamModelLengthHash = new Dictionary<string,int> ();
            string rmsdHmmSiteFile = Path.Combine(rmsdDataFileDir, "rmsdComHmmSites.txt");
            StreamWriter dataWriter = new StreamWriter(rmsdHmmSiteFile);
            dataWriter.WriteLine("PfamID\tPdbID1\tDomainInterfaceID1\tPdbID2\tDomainInterfaceID2\tNumComHmmSites\tChainRmsd\t" +
                "InteractChainRmsd\tPepRmsd\tInteractPepRmsd\tModelLength");
            string queryString = string.Format ("Select PfamID, PdbID1, DomainInterfaceId1, PdbID2, DomainInterfaceID2, " +
                " NumOfCommonHmmSites, ChainRmsd, InteractChainRmsd, PepRmsd, InteractPepRmsd " +
                " From {0} Where PepComp = '1' and InteractPepRmsd > -1 Order By PfamID;", hmmSiteCompTableName);
            DataTable rmsdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            int modelLength = 0;
            foreach (DataRow rmsdRow in rmsdTable.Rows)
            {
                pfamId = rmsdRow["PfamID"].ToString().Trim();
                modelLength = GetModelLength(pfamId, pfamModelLengthHash);
                dataWriter.WriteLine(ParseHelper.FormatDataRow (rmsdRow) + "\t" + modelLength.ToString ());
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamModelLengthHash"></param>
        /// <returns></returns>
        private int GetModelLength(string pfamId, Dictionary<string, int> pfamModelLengthHash)
        {
            if (pfamModelLengthHash.ContainsKey(pfamId))
            {
                return (int)pfamModelLengthHash[pfamId];
            }
            else
            {
                string queryString = string.Format("Select ModelLength From PfamHMM Where Pfam_ID = '{0}';", pfamId);
                DataTable pfamLengthTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                int modelLength = -1;
                if (pfamLengthTable.Rows.Count > 0)
                {
                    modelLength = Convert.ToInt32(pfamLengthTable.Rows[0]["ModelLength"].ToString ());
                }
                pfamModelLengthHash.Add(pfamId, modelLength);
                return modelLength;
            }
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void CalculateMissingPfamDomainInterfacePeptideRmsd()
        {
            ProtCidSettings.tempDir = @"X:\xtal_temp_pep";
            if (! Directory.Exists (ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.logWriter.WriteLine("Update missing peptide RMSD.");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideInterfaceRmsd_left.txt"), true);
            string pfamId = "Insulin";
            string[] bigPfams = { "Hormone_recep", "MHC_I", "Peptidase_C14", "Trypsin", "V-set", "WD40" };

            string queryString = string.Format("Select Distinct PfamId From {0} WHere ChainRmsd = -1 OR ChainRmsd is null;", hmmSiteCompTableName);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();

      /*          if (pfamId != bigPfams[5])  
                {
                    continue;
                }*/

                if (rmsdWriter == null)
                {
                    rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideInterfaceRmsd_" + pfamId + ".txt"), true);
                    ProtCidSettings.tempDir = Path.Combine(ProtCidSettings.tempDir, pfamId);
                    if (! Directory.Exists (ProtCidSettings.tempDir))
                    {
                        Directory.CreateDirectory(ProtCidSettings.tempDir);
                    }
                }

                ProtCidSettings.logWriter.WriteLine(pfamId);
//                string[] parsedCenterInterfaces = GetPfamCenterInterfaceInTempDir(pfamId);

                string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");
                StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

                queryString = string.Format("Select * From {0} Where pfamID = '{1}' AND (ChainRmsd = -1 OR ChainRmsd is null);", hmmSiteCompTableName, pfamId);
                DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);

                DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);
                //     DataTable domainInterfaceTable = GetDomainInterfaceTable(pfamId);
                Dictionary<string, string[]> domainInterfacePairHash = GetDomainInterfacePairHash(hmmSiteCompTable);
                Dictionary<string,  int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string,int[]> ();

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(domainInterfacePairHash);
                ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

                string centerPdbId = "";
                string[] compDomainInterfaces = null;
                foreach (string centerInterface in domainInterfacePairHash.Keys)
                {
       /*             if (Array.IndexOf (parsedCenterInterfaces, centerInterface) > -1)
                    {
                        continue;
                    }*/
                    centerPdbId = centerInterface.Substring(0, 4);
                    compDomainInterfaces = (string[])domainInterfacePairHash[centerInterface];
                    ProtCidSettings.logWriter.WriteLine(centerPdbId + " #CompDomainInterfaces=" + compDomainInterfaces.Length.ToString());

                    if (compDomainInterfaces.Length == 0)
                    {
                        continue;
                    }

                    foreach (string compDomainInterface in compDomainInterfaces)
                    {
                        string[] domainInterfacesToBeComp = new string[1];
                        domainInterfacesToBeComp[0] = compDomainInterface;
                        try
                        {
                            CalculateDomainInterfacePeptideRmsd(pfamId, centerInterface, domainInterfacesToBeComp, pfamChainDomainTable, hmmSiteCompTable,
                                                            domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + centerInterface + " " + ex.Message);
                            ProtCidSettings.logWriter.WriteLine(pfamId + " " + centerInterface + " " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                }
                pfamPymolPairFitWriter.WriteLine("quit");
                pfamPymolPairFitWriter.Close();
                ProtCidSettings.logWriter.WriteLine("Update missing peptide RMSD Done!");
                ProtCidSettings.logWriter.Flush();
            }
            rmsdWriter.Flush();


            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.tempDir + " Delete temporary directory error:  " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamCenterInterfaceInTempDir(string pfamId)
        {
            string[] pfamCoordFiles = Directory.GetFiles(ProtCidSettings.tempDir, pfamId + "*.coord");
            List<string> parsedCenterInterfaceList = new List<string> ();           
            string domainFileName = "";
            foreach (string coordFile in pfamCoordFiles)
            {
                FileInfo fileInfo = new FileInfo(coordFile);
                domainFileName = fileInfo.Name.Replace(pfamId + "_", "");
                domainFileName = domainFileName.Replace(".coord", "");
                parsedCenterInterfaceList.Add(domainFileName);
            }

            string[] pfamPmlFiles = Directory.GetFiles(ProtCidSettings.tempDir, pfamId + "*.pml");
            foreach (string pmlFile in pfamPmlFiles)
            {
                FileInfo fileInfo = new FileInfo(pmlFile);
                domainFileName = fileInfo.Name.Replace(pfamId + "_", "");
                domainFileName = domainFileName.Replace("_pairFit.pml", "");
                if (! parsedCenterInterfaceList.Contains(domainFileName))
                {
                    parsedCenterInterfaceList.Add(domainFileName);
                }
            }
            return parsedCenterInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateMissingDomainInterfacePeptideRmsd()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate rmsd between peptides in domain interfaces after pair_fit by HMM positions");

            StreamWriter rmsdWriter = new StreamWriter(Path.Combine(rmsdDataFileDir, "PfamPeptideInterfaceRmsd.txt"), true);
            string[] leftPfamIds = GetUpdatePfamIds();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#PFAM IDs: " + leftPfamIds.Length.ToString());

            int pfamCount = 1;
      //      foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            foreach (string pfamId in leftPfamIds) 
            {
           //     pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamCount.ToString() + ": " + pfamId);
                pfamCount++;
                try
                {
                    UpdatePfamDomainInterfacePeptideRmsd (pfamId, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            rmsdWriter.Close();

            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        public void ResetRmsdToNull ()
        {
            Dictionary<string, List<string>> pfamDomainInterfaceHash = ReadPfamDomainInterfaceFromLog ();
            string pdbId = "";
            int domainInterfaceId = 0;
            List<string> pfamList = new List<string> (pfamDomainInterfaceHash.Keys);
            pfamList.Sort();
            foreach (string pfamId in pfamList)
            {
                foreach (string domainInterface in pfamDomainInterfaceHash[pfamId])
                {
                    string[] domainInterfaceInfo = GetDomainInterfaceInfo(domainInterface);
                    pdbId = domainInterfaceInfo[0];
                    domainInterfaceId = Convert.ToInt32 (domainInterfaceInfo[1]);
                    ResetRmsdToNull(pdbId, domainInterfaceId);
                }
            }
        }

        private void ResetRmsdToNull(string pdbId, int domainInterfaceId)
        {
            string updateString = string.Format("Update {0} Set ChainRmsd = null " + 
                " Where PdbID1 = '{1}' AND DomainInterfaceId1 = {2};", hmmSiteCompTableName, pdbId, domainInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        private Dictionary<string, List<string>> ReadPfamDomainInterfaceFromLog ()
        {
            string logFile = @"D:\DbProjectData\pfam\PfamPeptide\PepPairWiseCoords\coordErrorLog.txt";
            StreamReader dataReader = new StreamReader (logFile);
            Dictionary<string, List<string>> pfamDomainInterfaceHash = new Dictionary<string, List<string>>();
            string line = "";
            string pfamId = "";
            string domainInterface = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split(' ');
                pfamId = fields[3];
                domainInterface = fields[4];
                if (pfamDomainInterfaceHash.ContainsKey(pfamId))
                {
                    pfamDomainInterfaceHash[pfamId].Add(domainInterface);
                }
                else
                {
                    List<string> interfaceList = new List<string> ();
                    interfaceList.Add(domainInterface);
                    pfamDomainInterfaceHash.Add(pfamId, interfaceList);
                }
            }
            dataReader.Close ();
            return pfamDomainInterfaceHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdatePfamIds()
        {
            string updatePfamIdFile = @"D:\DbProjectData\pfam\PfamPeptide\PepPairWiseCoords\rmsdErrorPfamIds.txt";
            List<string> pfamIdList = new List<string> ();
            if (File.Exists(updatePfamIdFile))
            {
                string line = "";
                StreamReader dataReader = new StreamReader (updatePfamIdFile);
                while ((line = dataReader.ReadLine ()) != null)
                {
                    pfamIdList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = string.Format("Select Distinct PfamID From {0} Where NumOfCommonHmmSites >= 3 and PepComp = '1' AND ChainRmsd < 0;", hmmSiteCompTableName);
                DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                StreamWriter dataWriter = new StreamWriter(updatePfamIdFile);
                string pfamId = "";
                foreach (DataRow pfamIdRow in pfamIdTable.Rows)
                {
                    pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                    dataWriter.WriteLine(pfamId);
                }
                dataWriter.Close();
            }
            string[] pfamIds = new string[pfamIdList.Count];
            pfamIdList.CopyTo(pfamIds);
            return pfamIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void UpdatePfamDomainInterfacePeptideRmsd(string pfamId, StreamWriter rmsdWriter)
        {
            string pfamPymolScriptFile = Path.Combine(rmsdDataFileDir, pfamId + "_pairFit.pml");

            StreamWriter pfamPymolPairFitWriter = new StreamWriter(pfamPymolScriptFile);

            DataTable hmmSiteCompTable = GetMissingPfamHmmSiteCompTable (pfamId);
            DataTable pfamChainDomainTable = GetPfamChainDomainTable(hmmSiteCompTable);
            //     DataTable domainInterfaceTable = GetDomainInterfaceTable(pfamId);
            Dictionary<string, string[]> domainInterfacePairHash = GetDomainInterfacePairHash(hmmSiteCompTable);
     //       Hashtable domainInterfacePairHash = RemoveExistDomainInterfacePairs(pfamDomainInterfacePairHash);
            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdsHash = new Dictionary<string, int[]>();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = GetTotalInterfacePairs(domainInterfacePairHash);
            ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.totalOperationNum;

            foreach (string centerInterface in domainInterfacePairHash.Keys)
            {
                string[] compDomainInterfaces = (string[])domainInterfacePairHash[centerInterface];

                try
                {
                    CalculateDomainInterfacePeptideRmsd(pfamId, centerInterface, compDomainInterfaces, pfamChainDomainTable, hmmSiteCompTable,
                                                    domainInterfaceChainCoordSeqIdsHash, pfamPymolPairFitWriter, rmsdWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + centerInterface + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            pfamPymolPairFitWriter.WriteLine("quit");
            pfamPymolPairFitWriter.Close();

            rmsdWriter.Flush();

            try
            {
                string[] tempFiles = Directory.GetFiles(ProtCidSettings.tempDir);
                foreach (string tempFile in tempFiles)
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetMissingPfamHmmSiteCompTable(string pfamId)
        {
            string queryString = string.Format("Select * From {0} Where PfamID = '{1}' AND PepComp = '1' AND ChainRmsd is null;", hmmSiteCompTableName, pfamId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return hmmSiteCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfacePairHash"></param>
        /// <returns></returns>
        private Dictionary <string, string[]> RemoveExistDomainInterfacePairs(Dictionary<string, string[]> domainInterfacePairHash)
        {
            Dictionary<string, string[]> leftDomainInterfacePairHash = new Dictionary<string, string[]>();
            foreach (string centerInterface in domainInterfacePairHash.Keys)
            {
                string[] nullRmsdCompInterfaces = GetNullRmsdInterfaces(centerInterface);
                if (nullRmsdCompInterfaces.Length > 0)
                {
                    leftDomainInterfacePairHash.Add(centerInterface, nullRmsdCompInterfaces);
                }
            }
            return leftDomainInterfacePairHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerInterface"></param>
        /// <returns></returns>
        private string[] GetNullRmsdInterfaces(string centerInterface)
        {
            string[] centerInterfaceInfo = GetDomainInterfaceInfo(centerInterface);
            string pdbId = centerInterfaceInfo[0];
            int domainInterfaceId = Convert.ToInt32(centerInterfaceInfo[1]);

            string queryString = string.Format("Select Distinct PdbId2, DomainInterfaceID2 From {0} " +
               " Where PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND PepComp = '1' AND ChainRmsd is null;", 
               hmmSiteCompTableName,  pdbId, domainInterfaceId);
            DataTable nullRmsdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string compInterface = "";
            List<string> leftCompInterfaceList = new List<string> ();
            foreach (DataRow compInterfaceRow in nullRmsdTable.Rows)
            {
                compInterface = compInterfaceRow["PdbID2"].ToString() + "_d" + compInterfaceRow["DomainInterfaceID2"].ToString();
                leftCompInterfaceList.Add(compInterface);
            }
            string[] leftCompInterfaces = new string[leftCompInterfaceList.Count];
            leftCompInterfaceList.CopyTo(leftCompInterfaces);
            return leftCompInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerInterface"></param>
        /// <param name="compInterface"></param>
        /// <returns></returns>
        private bool IsNullRmsdExist(string centerInterface)
        {
            string[] domainInterfaceInfo = GetDomainInterfaceInfo(centerInterface);
            string pdbId = domainInterfaceInfo[0];
            int domainInterfaceId = Convert.ToInt32(domainInterfaceInfo[1]);
            string queryString = string.Format("Select ChainRmsd From {0} Where PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND ChainRmsd is null;", 
                hmmSiteCompTableName, pdbId, domainInterfaceId);
            DataTable nullRmsdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (nullRmsdTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        public void TestRmsd()
        {
            string coordinateFile = @"D:\DbProjectData\pfam\PfamPeptide\PepClusters\SH3_1\SH3_1_1\SH3_1_1.coord";
            string pymolScriptFile = @"D:\DbProjectData\pfam\PfamPeptide\PepClusters\SH3_1\SH3_1_1\SH3_1_1_pairFit.pml";
            string[] clusterInterfaces = GetClusterInterfacesFromPymolScript(pymolScriptFile);
            Dictionary<string, Coordinate[][]> interfaceCoordinateHash = ReadInterfaceCoordinates(clusterInterfaces, coordinateFile);
            Coordinate[][] centerInterfaceCoordinates = interfaceCoordinateHash[clusterInterfaces[0]];
            StreamWriter rmsdTestWriter = new StreamWriter(@"D:\DbProjectData\pfam\PfamPeptide\PepClusters\SH3_1\SH3_1_1\rmsdText.txt");
            double rmsd = 0;
            string dataLine = "";
            for (int i = 1; i < clusterInterfaces.Length; i++)
            {
                Coordinate[][] interfaceCoordinates = (Coordinate[][])interfaceCoordinateHash[clusterInterfaces[i]];
                rmsd = rmsdCal.CalculateMinRmsd(centerInterfaceCoordinates[0], interfaceCoordinates[0]);
                dataLine = clusterInterfaces[0] + "\t" + clusterInterfaces[i] + "\t" + rmsd.ToString() + "\t";
                rmsd = rmsdCal.CalculateMinRmsd(centerInterfaceCoordinates[1], interfaceCoordinates[1]);
                dataLine += rmsd.ToString();
                rmsdTestWriter.WriteLine(dataLine);
            }
            rmsdTestWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintChainInterfaceFilesChanged()
        {
            StreamWriter lsInterfaceWriter = new StreamWriter("InterfaceListChangedFiles.txt");
            string interfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\cryst";
            string[] hashFolders = Directory.GetDirectories(interfaceFileDir);
            DateTime dt = new DateTime(2013, 6, 10);
            foreach (string hashFolder in hashFolders)
            {
                DateTime lastWritingTime = Directory.GetLastWriteTime(hashFolder);
                if (DateTime.Compare(lastWritingTime, dt) > 0)
                {
                    string[] interfaceFiles = Directory.GetFiles(hashFolder);
                    foreach (string interfaceFile in interfaceFiles)
                    {
                        FileInfo fileInfo = new FileInfo(interfaceFile);
                        DateTime fileLastWritingTime = fileInfo.LastWriteTime;
                        if (DateTime.Compare(fileLastWritingTime, dt) > 0)
                        {

                            lsInterfaceWriter.WriteLine(fileInfo.Name);
                        }
                    }
                }
            }
            lsInterfaceWriter.Close();
        }

        public void PrintRmsds()
        {
         //   string queryString = "Select PfamID,  InteractPepRmsd, LocalPepRmsd, Score, NumOfCommonHmmSites From PfamChainInterfaceHmmSiteComp " +
            string queryString = "Select Distinct PdbID2, DomainInterfaceID2, DomainID2 From PfamChainInterfaceHmmSiteComp " +
                " Where LocalPepRmsd is not null and LocalPepRmsd > -1 Order By PfamID;";
            DataTable rmsdChainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            StreamWriter dataWriter = new StreamWriter("PfamPepChainRmsds.txt");
            dataWriter.WriteLine("PfamID\tInteractPepRmsd\tLocalPepRmsd\tScore\tNumOfCommonHmmSites");
            string pdbId = "";
            int domainInterfaceId = 0;
            long domainId =  0;
            foreach (DataRow chainInterfaceRow in rmsdChainInterfaceTable.Rows)
            {
                pdbId = chainInterfaceRow["PdbID2"].ToString();
                domainInterfaceId = Convert.ToInt32(chainInterfaceRow["DomainInterfaceID2"].ToString ());
                domainId = Convert.ToInt64(chainInterfaceRow["DomainID2"].ToString ());
                DataRow rmsdRow = GetBestRmsdDataRow(pdbId, domainInterfaceId, domainId);
                dataWriter.WriteLine(ParseHelper.FormatDataRow(rmsdRow));
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataRow GetBestRmsdDataRow(string pdbId, int domainInterfaceId, long domainId)
        {
            string queryString = string.Format("Select PfamID,  InteractPepRmsd, LocalPepRmsd, Score, NumOfCommonHmmSites From PfamChainInterfaceHmmSiteComp " +
                " Where PdbID2 = '{0}' AND DomainInterfaceID2 = {1} AND DomainID2 = {2} AND LocalPepRmsd is not null and LocalPepRmsd > -1;", pdbId, domainInterfaceId, domainId);
            DataTable hmmSiteCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataRow bestRow = null;
            int maxNumOfHmmSites = 0;
            int numOfHmmSites = 0;
            foreach (DataRow hmmSiteCompRow in hmmSiteCompTable.Rows)
            {
                numOfHmmSites = Convert.ToInt32(hmmSiteCompRow["NumOfCommonHmmSites"].ToString ());
                if (maxNumOfHmmSites < numOfHmmSites)
                {
                    maxNumOfHmmSites = numOfHmmSites;
                    bestRow = hmmSiteCompRow;
                }
            }
            return bestRow;
        }
        #endregion
    }
}
