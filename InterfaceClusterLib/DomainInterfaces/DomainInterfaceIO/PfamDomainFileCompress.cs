using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Xml.Serialization;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using InterfaceClusterLib.PymolScript;
using DbLib;
using AuxFuncLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.BuIO;
using InterfaceClusterLib.AuxFuncs;
using PfamLib.DomainDef;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class PfamDomainFileCompress
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public string pfamDomainPymolFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "DomainAlign");
 //       public string pfamDomainPymolFileDir = @"D:\protcid_update31Fromv30\pfam\DomainAlign";
        public string pfamDomainFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "domainFiles");
        public string pfamDnaRnaDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamDnaRna");
        public FileCompress fileCompress = new FileCompress();
        public DomainAlignPymolScript domainAlignPymolScript = new DomainAlignPymolScript();
        public CmdOperations tarOperator = new CmdOperations();
        private string[] bestStructTypes = {"pdb", "unp", "cryst"};
        public BuWriter buWriter = new BuWriter();
        private DataTable pdbLigandTable = null;
        private string generalInstructFile = "";
        private PdbfamOutput pfamOut = new PdbfamOutput();
        private Dictionary<string, List<string>> domainLigandChainListDict = null;
        private PymolScriptUpdater pmlScriptUpdate = new PymolScriptUpdater();
        private ChainDomainUnpPfamArch objUnpPfamArch = new ChainDomainUnpPfamArch();
        #endregion

        public PfamDomainFileCompress ()
        {
             if (! Directory.Exists (Path.Combine (pfamDomainPymolFileDir, "pdb")))
             {
                 Directory.CreateDirectory(Path.Combine (pfamDomainPymolFileDir, "pdb"));
             }
             if (! Directory.Exists(Path.Combine(pfamDomainPymolFileDir, "unp")))
             {
                 Directory.CreateDirectory(Path.Combine(pfamDomainPymolFileDir, "unp"));
             }
             if (!Directory.Exists(Path.Combine(pfamDomainPymolFileDir, "cryst")))
             {
                 Directory.CreateDirectory(Path.Combine(pfamDomainPymolFileDir, "cryst"));
             }
             generalInstructFile = Path.Combine(pfamDomainPymolFileDir, "HowToUsePfamLigandsData.txt");
             domainLigandChainListDict = new Dictionary<string, List<string>>();
        }

        #region compile domain files for each PFAM
        /// <summary>
        /// 
        /// </summary>
        public void CompressPfamDomainFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress domain align in pymol");
            ProtCidSettings.logWriter.WriteLine("Compress domain align in pymol");

            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            DataTable asuTable = GetAsuSeqInfoTable();

            queryString = "Select * From PdbLigands;";
            pdbLigandTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = pfamTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamTable.Rows.Count;

            foreach (string bestStructType in bestStructTypes)
            {
                if (!Directory.Exists(Path.Combine(pfamDomainPymolFileDir, bestStructType)))
                {
                    Directory.CreateDirectory(Path.Combine (pfamDomainPymolFileDir, bestStructType));
                }
            }

            string pfamId = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                ProtCidSettings.progressInfo.currentOperationLabel = pfamId;

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                ProtCidSettings.logWriter.WriteLine(pfamId);

                try
                {
                    CompilePfamDomainWithLigandFiles(pfamId, asuTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        public void CompilePfamDomainWithLigandFiles(string pfamId, DataTable asuTable)
        {
            // use pfam ID as compressed file name on May 3, 2017
            string groupFileName = pfamId;
            string pdbPfamDomainPymolFileDir = Path.Combine(pfamDomainPymolFileDir, "pdb");
            string domainAlignFile = Path.Combine(pdbPfamDomainPymolFileDir, groupFileName + "_pdb.tar.gz");
        /*    if (File.Exists(domainAlignFile))
            {
                return;
            }*/

            CompressPfamDomainFiles(pfamId, asuTable, groupFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        /// <param name="groupFileName"></param>
        private void CompressPfamDomainFiles(string pfamId, DataTable asuTable, string groupFileName)
        {
            string queryString = string.Format("Select PdbPfam.PdbID, PdbPfam.EntityID, PdbPfam.DomainID, SeqStart, SeqEnd, AlignStart, AlignEnd, " +
               " HmmStart, HmmEnd, QueryAlignment, HmmAlignment, AsymChain, AuthChain, ChainDomainID " +
               " From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND PdbPfam.DomainId = PdbPfamChain.DomainID " +
               " AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = string.Format("Select Distinct PfamID, ClusterID, PdbID, ChainDomainID, LigandChain From PfamLigandClustersHmm Where PfamID = '{0}';", pfamId);
            DataTable ligandClusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            if (pfamDomainTable.Rows.Count == 0)
            {
                return;
            }
//            queryString = string.Format("Select Distinct PdbID, ChainDomainID From PfamLigands WHere PfamID = '{0}';", pfamId);
            DataTable pfamLigandsTable = GetPfamLigandsTable(pfamId);

            string entryDomainBestCoordInPfam = "";
            domainLigandChainListDict.Clear();  // record the best domains which interact with ligands (most contacts and most coordinates)
            //      Hashtable pfamBestStructChainDomainHash = GetPfamBestStructChainDomainHash(pfamDomainTable, asuTable, out entryDomainBestCoordInPfam, bestStructType);
            /* Modified on April 5-10, 2018
             * before, use the domain (chain-based) which has best coordinates for each PDB entry and uniprot
             * after, Add all domains which are interacting different ligands for each PDB entry or Uniprot
             * for entries and uniprots without ligands, still just add the domains with best coordinates
             */
            Dictionary<string, string[]>[] pfamBestStructDomainDicts = GetPfamBestStructChainDomainDicts (pfamDomainTable, asuTable, pfamLigandsTable, out entryDomainBestCoordInPfam);
            Dictionary<string, string[]> pdbPfamBestStructDomainDict = pfamBestStructDomainDicts[0];
            Dictionary<string, string[]> unpPfamBestStructDomainDict = pfamBestStructDomainDicts[1];
            Dictionary<string, string[]> crystPfamBestStructDomainDict = pfamBestStructDomainDicts[2];

            string[] pfamEntryChainDomains = GetPfamEntryChainDomains(pdbPfamBestStructDomainDict);
            string[] pfamChainDomains = AddPfamEntryChainDomains(unpPfamBestStructDomainDict, pfamEntryChainDomains);
            Dictionary<string, Range[]> chainDomainRangesHash = domainAlignPymolScript.GetDomainRangesHash(pfamChainDomains, pfamDomainTable);
            Dictionary<string, string[]> domainFileChainMapHash = new Dictionary<string, string[]> (); // the map between chain Ids and asymmtric chain ids in the domain file
            Dictionary<string, int[]> domainCoordSeqIdsHash = new Dictionary<string, int[]>();

            string[] domainCoords = null;
            try
            {
                domainCoords = WritePfamDomainWithLigandFiles(pfamChainDomains, pfamDomainTable, domainCoordSeqIdsHash, domainFileChainMapHash);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " write domain coordinate files errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + " write domain coordinate files errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            if (domainCoords == null || domainCoords.Length == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " no domain files. something wrong.");
                ProtCidSettings.logWriter.WriteLine(pfamId + " no domain files. something wrong.");
                return;
            }
            // remove those domains which don't have coordinates somehow. 
            DataTable ligandClusterNeedTable = GetPfamClusterLigandsNeedAlign(ligandClusterTable, domainCoords);
            
            try
            {
                // generate compressed domain alignments for pdb, unp, cryst at the same time, modifies on Nov. 27, 2013
        /*        CompressDomainCoordFiles(groupFileName, domainCoords, entryDomainBestCoordInPfam, pfamBestStructDomainHashes,
                    pfamDomainPymolFileDir, chainDomainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainFileChainMapHash, false);*/
                CompressDomainCoordFiles(groupFileName, domainCoords, entryDomainBestCoordInPfam, pfamBestStructDomainDicts, ligandClusterNeedTable,
                    pfamDomainPymolFileDir, chainDomainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainFileChainMapHash, false);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamLigandsTable (string pfamId)
        {
            string queryString = string.Format("Select Pfamligands.PdbId, ChainDomainId, LigandChain, Ligand, UnpCode, count(*) As ContactCount" +
             " From PdbLigands, PfamLigands Where PfamId = '{0}' and PfamLigands.pdbid = PdbLigands.pdbid and " +
             " PfamLigands.ligandchain = PdbLigands.asymchain and PfamLigands.ligandseqid = PdbLigands.seqid " +
             " Group By Pfamligands.PdbId, ChainDomainid, LigandChain, Ligand, UnpCode;", pfamId);
            DataTable pfamLigandsTable = ProtCidSettings.protcidQuery.Query(queryString);

            queryString = string.Format("Select PfamDnaRnas.PdbId, ChainDomainId, DnaRnaChain As LigandChain, Ligand,  count(*) As ContactCount" +
             " From PdbLigands, PfamDnaRnas Where PfamId = '{0}' AND PfamDnaRnas.pdbid = PdbLigands.PdbId AND " +
             " PfamDnaRnas.DnaRnaChain = PdbLigands.AsymChain " +
             " Group By PfamDnaRnas.Pdbid, ChainDomainid, LigandChain, Ligand;", pfamId);
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query(queryString);
            string unpCode = "";
            string pdbId = "";
            int chainDomainId = 0;
            foreach (DataRow dnaRnaRow in pfamDnaRnaTable.Rows)
            {
                pdbId = dnaRnaRow["PdbID"].ToString();
                chainDomainId = Convert.ToInt32(dnaRnaRow["ChainDomainID"].ToString ());
                unpCode = GetDomainUnpCode(pdbId, chainDomainId, pfamLigandsTable);
                DataRow dataRow = pfamLigandsTable.NewRow();
                dataRow["PdbID"] = dnaRnaRow["PdbID"];
                dataRow["ChainDomainID"] = dnaRnaRow["ChainDomainID"];
                dataRow["LigandChain"] = dnaRnaRow["LigandChain"];
                dataRow["Ligand"] = dnaRnaRow["Ligand"];                               
                dataRow["UnpCode"] = unpCode;
                dataRow["ContactCount"] = dnaRnaRow["ContactCount"];
                pfamLigandsTable.Rows.Add(dataRow);
            }
            return pfamLigandsTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <returns></returns>
        private string GetDomainUnpCode (string pdbId, int chainDomainId, DataTable pfamLigandsTable)
        {
            string unpCode = "";
            DataRow[] ligandsRows = pfamLigandsTable.Select(string.Format ("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
            if (ligandsRows.Length > 0)
            {
                unpCode = ligandsRows[0]["UnpCode"].ToString();
            }
            else
            {
                unpCode = objUnpPfamArch.GetDomainUnpCode(pdbId, chainDomainId);
            }
            return unpCode;
        }

        /// <summary>
        /// remove those coordDomains which are not in the clusters
        /// </summary>
        /// <param name="pfamLigandClusterTable"></param>
        /// <param name="coordDomains"></param>
        private DataTable GetPfamClusterLigandsNeedAlign (DataTable pfamLigandClusterTable, string[] coordDomains)
        {
            string pdbId = "";
            string chainDomainId = "";
            DataTable ligandClusterNeedAlignTable = pfamLigandClusterTable.Clone();
            foreach (string chainDomain in  coordDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = chainDomain.Substring(4, chainDomain.Length - 4);
                DataRow[] clusterInfoRows = pfamLigandClusterTable.Select(string.Format ("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                foreach (DataRow clusterInfoRow in clusterInfoRows)
                {
                    DataRow dataRow = ligandClusterNeedAlignTable.NewRow();
                    dataRow.ItemArray = clusterInfoRow.ItemArray;
                    ligandClusterNeedAlignTable.Rows.Add(dataRow);
                }
            }
            return ligandClusterNeedAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="pfamLigandChainDomains"></param>must be sorted<param>
        /// <param name="entryDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string>[] GetPfamBestStructChainDomainHash(DataTable pfamDomainTable, DataTable asuTable, DataTable pfamLigandsTable, out string entryDomainBestCoordInPfam)
        {
            entryDomainBestCoordInPfam = "";
            // the best structure of a pdb structure
            Dictionary<string, string> pfamBestStructChainDomainHash = GetPfamBestStructChainDomainIdHash(pfamDomainTable, pfamLigandsTable, asuTable, out entryDomainBestCoordInPfam);
            string[] bestEntryChainDomainIds = GetPfamEntryChainDomains(pfamBestStructChainDomainHash);

            string[][] splitLigandEntryChainDomainIds = SplitEntryChainDomainIdsWithLigands(bestEntryChainDomainIds, pfamLigandsTable);
            Dictionary<string, string> unpPfamBestStructChainDomainHash = GetPfamUnpBestStructChainDomainIdHash(pfamDomainTable, asuTable, splitLigandEntryChainDomainIds[0]);
            Dictionary<string, string> noLigandUnpChainDomainHash = GetPfamUnpBestStructChainDomainIdHash(pfamDomainTable, asuTable, splitLigandEntryChainDomainIds[1]);
            MergeSecondDictToFirstDict (unpPfamBestStructChainDomainHash, noLigandUnpChainDomainHash);

            Dictionary<string, string> crystPfamBestStructChainDomainHash = GetPfamCrystBestStructChainDomainIdHash(pfamDomainTable, asuTable, splitLigandEntryChainDomainIds[0]);
            Dictionary<string, string> noLigandCrystChainDomainHash = GetPfamCrystBestStructChainDomainIdHash(pfamDomainTable, asuTable, splitLigandEntryChainDomainIds[1]);
            MergeSecondDictToFirstDict (crystPfamBestStructChainDomainHash, noLigandCrystChainDomainHash);

            Dictionary<string, string>[] pfambestStructChainDomainHashes = new Dictionary<string, string>[3];
            pfambestStructChainDomainHashes[0] = pfamBestStructChainDomainHash;
            pfambestStructChainDomainHashes[1] = unpPfamBestStructChainDomainHash;
            pfambestStructChainDomainHashes[2] = crystPfamBestStructChainDomainHash;

            return pfambestStructChainDomainHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="pfamLigandChainDomains"></param>must be sorted<param>
        /// <param name="entryDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string[]>[] GetPfamBestStructChainDomainDicts (DataTable pfamDomainTable, DataTable asuTable, DataTable pfamLigandsTable, out string entryDomainBestCoordInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pfamDomainTable);
            string[][] splitChainDomainIds = SplitEntryChainDomainIdsWithLigands(entryChainDomainIds, pfamLigandsTable);
            string[] ligandDomains = splitChainDomainIds[0];
            string[] noLigandDomains = splitChainDomainIds[1];

            entryDomainBestCoordInPfam = "";
            // the best structure of a pdb structure
            string[][] entryDomainsLigandsOrNot = new string[2][];
            Dictionary<string, string[]> pfamEntryBestDomainDict = GetPfamBestStructChainDomainsDict (pfamDomainTable, pfamLigandsTable, asuTable, noLigandDomains, out entryDomainBestCoordInPfam, out entryDomainsLigandsOrNot);

            Dictionary<string, string[]> unpPfamBestStructChainDomainDict = GetLigandBestDomainOnContactsAndCoordsDict(pfamLigandsTable, pfamDomainTable, asuTable, out entryDomainBestCoordInPfam, "unp");
            List<string> unpCodeListWithLigands = new List<string>(unpPfamBestStructChainDomainDict.Keys);
            Dictionary<string, string> noLigandUnpChainDomainDict = GetPfamUnpBestStructChainDomainIdHash(pfamDomainTable, asuTable, noLigandDomains, unpCodeListWithLigands.ToArray ());
            MergeSecondDictToFirstDict(unpPfamBestStructChainDomainDict, noLigandUnpChainDomainDict);

            Dictionary<string, string[]> crystPfamBestStructChainDomainDict = GetPfamCrystBestStructChainDomainIdHash(pfamDomainTable, pfamLigandsTable, asuTable, entryDomainsLigandsOrNot[0]);
            Dictionary<string, string> noLigandCrystChainDomainDict = GetPfamCrystBestStructChainDomainIdHash(pfamDomainTable, asuTable, entryDomainsLigandsOrNot[1]);
            MergeSecondDictToFirstDict(crystPfamBestStructChainDomainDict, noLigandCrystChainDomainDict);

            // add ligand asymchain id for each ligand in the ligand list 
            AddLigandChainIdsToDict(pfamLigandsTable);

            Dictionary<string, string[]>[] pfambestStructChainDomainHashes = new Dictionary<string, string[]>[3];
            pfambestStructChainDomainHashes[0] = pfamEntryBestDomainDict;
            pfambestStructChainDomainHashes[1] = unpPfamBestStructChainDomainDict;
            pfambestStructChainDomainHashes[2] = crystPfamBestStructChainDomainDict;

            return pfambestStructChainDomainHashes;
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamLigandsTable"></param>
        private void AddLigandChainIdsToDict (DataTable pfamLigandsTable)
        {   
            string ligandChainId = "";
            List<string> domainList = new List<string>(domainLigandChainListDict.Keys);

            foreach (string entryDomain in domainList)
            {
                List<string> ligandNameChainIdList = new List<string>();
                foreach (string ligandName in domainLigandChainListDict[entryDomain])
                {
                    ligandChainId = GetLigandChainId(entryDomain, ligandName, pfamLigandsTable);
                    ligandNameChainIdList.Add(ligandName + ";" + ligandChainId);
                }
                domainLigandChainListDict[entryDomain] = ligandNameChainIdList;
            }
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamBestStructChainDomainHash"></param>
        /// <returns></returns>
        private string[] WritePfamDomainWithLigandFiles(Dictionary<string, string> pfamBestStructChainDomainDict, DataTable pfamDomainTable, Dictionary<string, int[]> fileCoordSeqIdHash, 
            Dictionary<string, string[]> chainDomainChainMapHash)
        {
            int chainDomainId = 0;
            string entryBestStructChainDomain = "";
            string chainDomainFile = "";
            List<string> chainDomainFileList = new List<string>();
            string pdbId = "";
            foreach (string structKey in pfamBestStructChainDomainDict.Keys)
            {
                entryBestStructChainDomain = pfamBestStructChainDomainDict[structKey];

                ProtCidSettings.progressInfo.currentFileName = entryBestStructChainDomain;

                pdbId = entryBestStructChainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryBestStructChainDomain.Substring(4, entryBestStructChainDomain.Length - 4));
                DataRow[] chainDomainRows = pfamDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));

                try
                {
                    chainDomainFile = WriteDomainChainWithLigandFile(chainDomainRows, pfamDomainPymolFileDir, ref fileCoordSeqIdHash, ref chainDomainChainMapHash);
                    chainDomainFileList.Add(chainDomainFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chainDomainId.ToString() + " Write domain file error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + chainDomainId.ToString() + " Write domain file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            chainDomainFileList.Sort();
            string[] chainDomainFiles = new string[chainDomainFileList.Count];
            chainDomainFileList.CopyTo(chainDomainFiles);
            return chainDomainFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamBestStructChainDomainHash"></param>
        /// <returns></returns>
        private string[] WritePfamDomainWithLigandFiles(string[] entryDomains, DataTable pfamDomainTable, Dictionary<string, int[]> fileCoordSeqIdHash,
            Dictionary<string, string[]> chainDomainChainMapHash)
        {
            int chainDomainId = 0;
            string chainDomainFile = "";
            List<string> chainDomainFileList = new List<string>();
            string pdbId = "";
            foreach (string entryBestStructChainDomain in entryDomains)
            {
                ProtCidSettings.progressInfo.currentFileName = entryBestStructChainDomain;              

                pdbId = entryBestStructChainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryBestStructChainDomain.Substring(4, entryBestStructChainDomain.Length - 4));
                DataRow[] chainDomainRows = pfamDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                try
                {
                    chainDomainFile = WriteDomainChainWithLigandFile(chainDomainRows, pfamDomainPymolFileDir, ref fileCoordSeqIdHash, ref chainDomainChainMapHash);
                    chainDomainFileList.Add(chainDomainFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chainDomainId.ToString() + " Write domain file error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + chainDomainId.ToString() + " Write domain file error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            chainDomainFileList.Sort();
            string[] chainDomainFiles = new string[chainDomainFileList.Count];
            chainDomainFileList.CopyTo(chainDomainFiles);
            return chainDomainFiles;
        }

         /// <summary>
         /// 
         /// </summary>
         /// <param name="chainDomains"></param>
         /// <param name="domainFileChainMapHash"></param>
         /// <param name="domainInteractingDnaRnaHash"></param>
         /// <returns></returns>
        public Dictionary<string, string[]>[] GetChainDomainInteractingLigandsHash(string[] chainDomains, Dictionary<string, string[]> domainFileChainMapHash)
        {
            Dictionary<string, string[]> domainInteractingLigandsHash = new Dictionary<string, string[]> ();
            Dictionary<string, string[]> domainInteractingDnaRnaHash = new Dictionary<string, string[]>();
            Dictionary<string, List<string>> ligandNameDomainChainListHash = new Dictionary<string, List<string>>();
            string pdbId = "";
            int chainDomainId = 0;
            string ligandName = "";
            foreach (string chainDomain in chainDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(chainDomain.Substring (4, chainDomain.Length - 4));
                string[][] interactingLigandChains = GetDomainInteractingLigandChainIds(chainDomain);
       //         string[][] interactingLigandChains = GetDomainInteractingLigands(pdbId, chainDomainId);  // get ligand chains 
                string[] interactingLigands = interactingLigandChains[0];
  //              string[] interactingDnaRnaChains = interactingLigandChains[1];
                string[] interactingDnaRnaChains = GetDomainInteractingDnaRnaChains(pdbId, chainDomainId);
               
                string[] fileChainMap = (string[])domainFileChainMapHash[chainDomain];
                // match the ligand chains to the chain ids in the file
                // use file chain ids for the pymol sessions.
                if (interactingLigands.Length > 0)
                {
                    string[] ligandFileChains = MapAsymmetricChainsToFileChains(interactingLigands, fileChainMap);
                    domainInteractingLigandsHash.Add(chainDomain, ligandFileChains);

                    for (int i = 0; i < ligandFileChains.Length; i++)
                    {
                        ligandName = GetLigandName(pdbId, interactingLigands[i], pdbLigandTable);
                        if (ligandNameDomainChainListHash.ContainsKey(ligandName))
                        {
                            ligandNameDomainChainListHash[ligandName].Add(chainDomain + "_" + ligandFileChains[i]);
                        }
                        else
                        {
                            List<string> ligandChainList = new List<string> ();
                            ligandChainList.Add(chainDomain + "_" + ligandFileChains[i]);
                            ligandNameDomainChainListHash.Add(ligandName, ligandChainList);
                        }
                    }
                }

                if (interactingDnaRnaChains.Length > 0)
                {
                    string[] fileDnaRnaChains = MapAsymmetricChainsToFileChains(interactingDnaRnaChains, fileChainMap);
                    domainInteractingDnaRnaHash.Add(chainDomain, fileDnaRnaChains);

                    for (int i = 0; i < fileDnaRnaChains.Length; i++)
                    {
                        ligandName = GetLigandName(pdbId, interactingDnaRnaChains[i], pdbLigandTable);
                        if (ligandNameDomainChainListHash.ContainsKey(ligandName))
                        {
                            ligandNameDomainChainListHash[ligandName].Add(chainDomain + "_" + fileDnaRnaChains[i]);
                        }
                        else
                        {
                            List<string> ligandChainList = new List<string> ();
                            ligandChainList.Add(chainDomain + "_" + fileDnaRnaChains[i]);
                            ligandNameDomainChainListHash.Add(ligandName, ligandChainList);
                        }
                    }
                }
            }
            Dictionary<string, string[]> ligandNameDomainChainHash = new Dictionary<string, string[]>();
            foreach (string lsLigandName in ligandNameDomainChainListHash.Keys)
            {
                ligandNameDomainChainHash.Add(lsLigandName, ligandNameDomainChainListHash[lsLigandName].ToArray()); 
            }
            Dictionary<string, string[]>[] interactingNoProtChainHashes = new Dictionary<string, string[]>[3];
            interactingNoProtChainHashes[0] = domainInteractingLigandsHash;
            interactingNoProtChainHashes[1] = domainInteractingDnaRnaHash; // key: chain domain, values: 
            interactingNoProtChainHashes[2] = ligandNameDomainChainHash;  // key: ligand name, values: ligand chain ids in a file
            return interactingNoProtChainHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandClusterTable"></param>
        /// <param name="chainDomains"></param>
        /// <param name="domainFileChainMapHash"></param>
        /// <returns></returns>
        public Dictionary<int, string[]> GetClusterLigandsHash(DataTable ligandClusterTable, string[] chainDomains, Dictionary<string, string[]> domainFileChainMapHash)
        {
            Dictionary<int, List<string>> clusterLigandListHash = new Dictionary<int,List<string>> ();
            string pdbId = "";
            string chainDomainId = "";
            string ligandDomainCluster = "";
            string ligandFileChain = "";
            string ligandAsymChain = "";
            int clusterId = 0;
            string[] asymChains = null;
            string[] fileChains = null;
            foreach (string chainDomain in chainDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = chainDomain.Substring(4, chainDomain.Length - 4);
                DataRow[] domainClusterLigandRows = ligandClusterTable.Select(string.Format ("PdbID = '{0}' AND ChainDomainId = '{1}'", pdbId, chainDomainId));                
                string[] fileChainMap = (string[])domainFileChainMapHash[chainDomain];
                asymChains = fileChainMap[0].Split(',');
                fileChains = fileChainMap[1].Split(',');
                foreach (DataRow ligandRow in domainClusterLigandRows)
                {
                    clusterId = Convert.ToInt32(ligandRow["ClusterID"].ToString ());
                    ligandAsymChain = ligandRow["LigandChain"].ToString().TrimEnd();
                    if (! IsDomainLigandInteractionIncluded (chainDomain, ligandAsymChain))
                    {
                        continue;
                    }
                    ligandFileChain = MapAsymChainToFileChain(ligandAsymChain, asymChains, fileChains);
                    ligandDomainCluster = chainDomain + "_" + ligandFileChain;
                    if (clusterLigandListHash.ContainsKey(clusterId))
                    {
                        clusterLigandListHash[clusterId].Add(ligandDomainCluster);
                    }
                    else
                    {
                        List<string> domainLigandList = new List<string> ();
                        domainLigandList.Add(ligandDomainCluster);
                        clusterLigandListHash.Add(clusterId, domainLigandList);
                    }
                }
            }
            Dictionary<int, string[]> clusterLigandsDict = new Dictionary<int, string[]>();
            foreach (int lsCluster in clusterLigandListHash.Keys)
            {
                clusterLigandsDict.Add(lsCluster, clusterLigandListHash[lsCluster].ToArray ());
            }
            return clusterLigandsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="pdbLigandTable"></param>
        /// <returns></returns>
        private string GetLigandName(string pdbId, string asymChain, DataTable pdbLigandTable)
        {
            string ligandName = "";
            if (pdbLigandTable != null)
            {
                DataRow[] ligandRows = pdbLigandTable.Select(string.Format ("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain));           
                if (ligandRows.Length > 0)
                {
                    ligandName = ligandRows[0]["Ligand"].ToString().TrimEnd();
                }
            }
            else
            {
                string queryString = string.Format ("Select * From PdbLigands Where PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain);
                DataTable ligandInfoTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
                if (ligandInfoTable.Rows.Count > 0)
                {
                    ligandName = ligandInfoTable.Rows[0]["Ligand"].ToString ().TrimEnd ();
                }
            }
            return ligandName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interactingLigands"></param>
        /// <returns></returns>
        public string[] GetInteractingDnaRnaChains(string pdbId, string[] interactingNoProtLigands)
        {
            string queryString = string.Format("Select AsymID, PolymerType From AsymUnit Where PdbID = '{0}' AND " +
                " (PolymerType = 'polydeoxyribonucleotide' OR PolymerType = 'polyribonucleotide');", pdbId);
            DataTable entryAsuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            List<string> dnaRnaChainList = new List<string>();
            string dnaRnaChain = "";
            foreach (DataRow asuRow in entryAsuTable.Rows)
            {
                dnaRnaChain = asuRow["AsymID"].ToString().TrimEnd();
                foreach (string noProtLigand in interactingNoProtLigands)
                {
                    string[] fields = noProtLigand.Split('_');
                    if (fields[0] == dnaRnaChain)
                    {
                        if (!dnaRnaChainList.Contains(noProtLigand))
                        {
                            dnaRnaChainList.Add(noProtLigand);
                        }
                    }
                }
            /*    if (interactingNoProtLigands.Contains(dnaRnaChain))
                {
                    dnaRnaChainList.Add(dnaRnaChain);
                }*/
            }
            string[] dnaRnaChains = new string[dnaRnaChainList.Count];
            dnaRnaChainList.CopyTo(dnaRnaChains);
            return dnaRnaChains;
        }

        /// <summary>
        /// map the asymmtric chains to the chain ids in the file, since the chain ids in the file are renamed by alphabet order
        /// </summary>
        /// <param name="asymChainsToBeMapped"></param>
        /// <param name="chainMap"></param>
        /// <returns></returns>
        public string[] MapAsymmetricChainsToFileChains(string[] asymChainsToBeMapped, string[] chainMap)
        {
            string[] fileAsymChains = chainMap[0].Split(',');
            string[] fileChains = chainMap[1].Split(',');
            string[] fileChainsForInputAsymChains = new string[asymChainsToBeMapped.Length];
            for (int i = 0; i < asymChainsToBeMapped.Length; i++)
            {
                fileChainsForInputAsymChains[i] = MapAsymChainToFileChain (asymChainsToBeMapped[i], fileAsymChains, fileChains);
            }
            return fileChainsForInputAsymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputAsymChain"></param>
        /// <param name="asymChainsInFile"></param>
        /// <param name="fileChainsInFile"></param>
        /// <returns></returns>
        private string MapAsymChainToFileChain(string inputAsymChain, string[] asymChainsInFile, string[] fileChainsInFile)
        {
            string fileChain = "";
            for (int i = 0; i < asymChainsInFile.Length; i++)
            {
                if (asymChainsInFile[i] == inputAsymChain)
                {
                    fileChain = fileChainsInFile[i];
                    break;
                }
            }
            return fileChain;
        }

        /// <summary>
        /// this function can adjust to select those ligands with more than one contacts.
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private string[][] GetDomainInteractingLigands(string pdbId, int chainDomainId)
        {          
            string queryString = string.Format("Select Distinct LigandChain From PfamLigands Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable interactingLigandsTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select Distinct DnaRnaChain As LigandChain From PfamDnaRnas Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable interactingDnaRnaTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] notDnaRnaLigandChains = new string[interactingLigandsTable.Rows.Count];
            string[] dnaRnaChains = new string[interactingDnaRnaTable.Rows.Count];
            int count = 0;
            string ligandChain = "";
            foreach (DataRow interactingLigandRow in interactingLigandsTable.Rows)
            {
                ligandChain = interactingLigandRow["LigandChain"].ToString ().TrimEnd ();
                notDnaRnaLigandChains [count] = ligandChain;
                count++;
            }
            count = 0;
            foreach (DataRow interactingDnaRnaRow in interactingDnaRnaTable.Rows)
            {
                ligandChain = interactingDnaRnaRow["LigandChain"].ToString().TrimEnd();
                dnaRnaChains[count] = ligandChain;
                count++;
            }
            string[][] ligandChains = new string[2][];
            ligandChains[0] = notDnaRnaLigandChains;
            ligandChains[1] = dnaRnaChains;
            return ligandChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private string[] GetDomainInteractingDnaRnaChains (string pdbId, int chainDomainId)
        {            
            string queryString = string.Format("Select Distinct DnaRnaChain As LigandChain From PfamDnaRnas Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable interactingDnaRnaTable = ProtCidSettings.protcidQuery.Query(queryString);
            
            string[] dnaRnaChains = new string[interactingDnaRnaTable.Rows.Count];
            int count = 0;
            string ligandChain = "";
            foreach (DataRow interactingDnaRnaRow in interactingDnaRnaTable.Rows)
            {
                ligandChain = interactingDnaRnaRow["LigandChain"].ToString().TrimEnd();
                dnaRnaChains[count] = ligandChain;
                count++;
            }
            return dnaRnaChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <returns></returns>
        private string[][] GetDomainInteractingLigandChainIds (string entryDomain)
        {
            List<string> ligandChainIdList = new List<string>();
            List<string> dnaRnaChainIdList = new List<string>();
            if (domainLigandChainListDict.ContainsKey (entryDomain))
            {
                foreach (string ligandAndChainId in domainLigandChainListDict[entryDomain])
                {
                    string[] fields = ligandAndChainId.Split (';');
                    if (fields.Length == 2)
                    {
                        if (fields[0] == "DNA" || fields[0] == "RNA")
                        {
                            dnaRnaChainIdList.Add(fields[1]);
                        }
                        else
                        {
                            ligandChainIdList.Add(fields[1]);
                        }
                    }
                }
            }
            string[][] interactingLigandChains = new string[2][];
            interactingLigandChains[0] = ligandChainIdList.ToArray();
            interactingLigandChains[1] = dnaRnaChainIdList.ToArray();
            return interactingLigandChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="ligandName"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <returns></returns>
        private string GetLigandChainId(string entryDomain, string ligandName, DataTable pfamLigandsTable)
        {
            string ligandChainId = "-";
            DataRow[] domainLigandRows = pfamLigandsTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}' AND Ligand = '{2}'",
                entryDomain.Substring(0, 4), entryDomain.Substring(4, entryDomain.Length - 4), ligandName), "ContactCount DESC");
            if (domainLigandRows.Length > 0)
            {
                ligandChainId = domainLigandRows[0]["LigandChain"].ToString().TrimEnd();
            }
            return ligandChainId;
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomainRows"></param>
        /// <returns></returns>
        private Dictionary<string, Range[]> GetChainDomainRangeHash(DataRow[] chainDomainRows)
        {
            string asymChain = "";
            Dictionary<string, List<Range>> chainDomainRangeListHash = new Dictionary<string, List<Range>>();
            foreach (DataRow chainDomainRow in chainDomainRows)
            {
                asymChain = chainDomainRow["AsymChain"].ToString().TrimEnd();
                Range range = new Range();
                range.startPos = Convert.ToInt32(chainDomainRow["SeqStart"].ToString ());
                range.endPos = Convert.ToInt32(chainDomainRow["SeqENd"].ToString ());
                if (chainDomainRangeListHash.ContainsKey(asymChain))
                {
                    chainDomainRangeListHash[asymChain].Add(range);
                }
                else
                {
                    List<Range> domainRangeList = new List<Range> ();
                    domainRangeList.Add(range);
                    chainDomainRangeListHash.Add(asymChain, domainRangeList);
                }
            }
            Dictionary<string, Range[]> chainDomainRangeHash = new Dictionary<string, Range[]>();
            foreach (string lsChain in chainDomainRangeListHash.Keys)
            {
                chainDomainRangeHash.Add(lsChain, chainDomainRangeListHash[lsChain].ToArray());
            }
            return chainDomainRangeHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <param name="ligandContactRows"></param>
        /// <returns></returns>
        private bool IsLigandDomainInteracting(Range[] domainRanges, DataRow[] ligandContactRows)
        {
            int ligandSeqId = 0;
            foreach (DataRow ligandContactRow in ligandContactRows)
            {
                ligandSeqId = Convert.ToInt32(ligandContactRow["SeqID"].ToString ());
                foreach (Range domainRange in domainRanges)
                {
                    if (ligandSeqId <= domainRange.endPos && ligandSeqId >= domainRange.startPos)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region compile domain files from the pdb group
        public void CompressCrystFormDomainFiles(string groupFileName, string centerDomain, Dictionary<string, string[]> crystBestStructChainDomainDict, DataTable ligandClusterTable, string fileDataDir)
        {
            string pfamAssingmentInfoFile = groupFileName + "_PdbfamAssignments.txt";
            string pdbLigandClusterInfoFileName = groupFileName + "-ligands_clusters_pdb.txt";
            string[] pfamDomainFiles = Directory.GetFiles(fileDataDir, "*.pfam");
            string[] coordDomains = new string[pfamDomainFiles.Length];
            int count = 0;
            foreach (string domainFile in pfamDomainFiles)
            {
                FileInfo fileInfo = new FileInfo(domainFile);
                coordDomains[count] = fileInfo.Name.Replace(".pfam", "");
                count++;
            }
            Dictionary<string, string> domainUnpPfamPmlNameDict = pmlScriptUpdate.GetDomainNewPyMolNameByUnpPfams(coordDomains);
            string[] pdbPymolScriptFiles = new string[4];
            pdbPymolScriptFiles[0] = groupFileName + "_byChain.pml";
            pdbPymolScriptFiles[1] = groupFileName + "_pairFit.pml";
            pdbPymolScriptFiles[2] = groupFileName + "_byDomain.pml";
            pdbPymolScriptFiles[3] = groupFileName + "_pairFitDomain.pml";       
            
            DataTable ligandClusterNeedTable = GetPfamClusterLigandsNeedAlign(ligandClusterTable, coordDomains);

            string[] crystCoordDomains = GetPfamEntryChainDomains(crystBestStructChainDomainDict, coordDomains);
            if (!crystCoordDomains.Contains(centerDomain)) // if the cryst list doesnot contain the center domain, add it to the list
            {
                string[] crystCoordDomainsWithBest = new string[crystCoordDomains.Length + 1];
                crystCoordDomainsWithBest[0] = centerDomain;
                Array.Copy(crystCoordDomains, 0, crystCoordDomainsWithBest, 1, crystCoordDomains.Length);
                crystCoordDomains = crystCoordDomainsWithBest;
            }
            string newPymolScriptFileName = "";
            string[] crystPymolScriptFiles = new string[pdbPymolScriptFiles.Length];
            List<string> crystTextFileList = new List<string>();
            count = 0;
            foreach (string pymolScriptFile in pdbPymolScriptFiles)
            {
                newPymolScriptFileName = pmlScriptUpdate.ReWritePymolScriptFile(Path.Combine(fileDataDir, pymolScriptFile), crystCoordDomains, "cryst");
                crystPymolScriptFiles[count] = newPymolScriptFileName;
                count++;
            }
            crystTextFileList.AddRange(crystPymolScriptFiles);
    //        string[] unpPfamsCrystPmlScriptFiles = pmlScriptUpdate.RewritePymolScriptFilesByUnpPfams(crystPymolScriptFiles, domainUnpPfamPmlNameDict, fileDataDir);
            string unpPfamRenameFile = Path.Combine (fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
            string unpPfamRenameFileName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects (crystCoordDomains, unpPfamRenameFile, domainUnpPfamPmlNameDict, ".pfam");
            crystTextFileList.Add (unpPfamRenameFileName);
            crystTextFileList.Add(pfamAssingmentInfoFile);

            if (ligandClusterNeedTable.Rows.Count > 0)
            {
                string newCrystPfamLigandClusterFileName = ReWritePfamLigandClusterInfoFile(Path.Combine(fileDataDir, pdbLigandClusterInfoFileName), crystCoordDomains, "cryst");
                crystTextFileList.Add(newCrystPfamLigandClusterFileName);
            }
            string newCrystInstructFile = Path.Combine(fileDataDir, "HowToUsePfamLigandsData_cryst.txt");
            ReWriteInstructFile(groupFileName, "cryst", generalInstructFile, newCrystInstructFile);
            crystTextFileList.Add("HowToUsePfamLigandsData_cryst.txt");
            CompressPfamDomainFiles(crystCoordDomains, crystTextFileList.ToArray(), groupFileName, fileDataDir, "cryst");
        }
        /// <summary>
        /// coordinate files are already unzipped and in the destinate file folder
        /// </summary>
        /// <param name="groupFileName"></param>
        /// <param name="coordFiles"></param>
        /// <param name="centerCoordFile"></param>
        /// <param name="fileDataDir"></param>
//        public void CompressDomainCoordFiles(string groupFileName, string[] coordDomains, string centerDomain, Dictionary<string, string>[] pfamBestStructChainDomainHashes,
        public void CompressDomainCoordFiles(string groupFileName, string[] coordDomains, string centerDomain, Dictionary<string, string[]>[] pfamBestStructChainDomainHashes,
                                        DataTable ligandClusterNeedTable, string fileDataDir, Dictionary<string, Range[]> domainRangesHash, DataTable pfamDomainTable, 
                                        Dictionary<string, int[]> domainCoordSeqIdsHash, Dictionary<string, string[]> domainFileChainMapHash, bool fileToBeDeleted)
        {
            if (!coordDomains.Contains(centerDomain)) // somehow, the domain file for center domain not exist, then use the first available domain file
            {
                centerDomain = coordDomains[0];
            }

            //all chains are chain ids in coordinate files
            Dictionary<string, string[]>[] domainInteractingLigandsHashes = GetChainDomainInteractingLigandsHash(coordDomains, domainFileChainMapHash);
            Dictionary<string, string[]> domainInteractingLigandsHash = domainInteractingLigandsHashes[0];
            Dictionary<string, string[]> domainInteractingDnaRnaHash = domainInteractingLigandsHashes[1];
            Dictionary<string, string[]> ligandNameDomainChainHash = domainInteractingLigandsHashes[2];

            Dictionary<string, string> domainUnpPfamPmlNameDict = pmlScriptUpdate.GetDomainNewPyMolNameByUnpPfams(coordDomains);

            // add ligand cluster info
            // get the ligands of selected domains in clusters
            // the chain ids are maped to file chain ids
            /*******************************************************/
            // pdb
            string[] pdbCoordDomains = GetPfamEntryChainDomains(pfamBestStructChainDomainHashes[0], coordDomains);
            Dictionary<int, string[]> pdbClusterLigandsHash = GetClusterLigandsHash(ligandClusterNeedTable, pdbCoordDomains, domainFileChainMapHash);

            string[] pdbPymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, pdbCoordDomains, centerDomain, fileDataDir,
                                domainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainInteractingLigandsHash, domainInteractingDnaRnaHash,
                                ligandNameDomainChainHash, pdbClusterLigandsHash, "pdb");
            List<string> pdbTextFileList = new List<string>(pdbPymolScriptFiles);
            string renameUnpPfamsPmlScript = Path.Combine(fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
   //         string[] unpPfamsPmlScriptFiles = pmlScriptUpdate.RewritePymolScriptFilesByUnpPfams(pdbPymolScriptFiles, domainUnpPfamPmlNameDict, fileDataDir);
            string renameUnpPfamsPmlScriptName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects(coordDomains, renameUnpPfamsPmlScript, domainUnpPfamPmlNameDict, ".pfam");
            pdbTextFileList.Add(renameUnpPfamsPmlScriptName);

            string pfamAssingmentInfoFile = groupFileName + "_PdbfamAssignments.txt";
            WritePfamDomainAssignmentsToTextFile(coordDomains, Path.Combine(fileDataDir, pfamAssingmentInfoFile));
            pdbTextFileList.Add(pfamAssingmentInfoFile);

            // add pfam-ligands clusters info
            string pdbLigandClusterInfoFileName = groupFileName + "-ligands_clusters_pdb.txt";
            if (ligandClusterNeedTable.Rows.Count > 0)
            {                
                string pdbClusterInfoFile = Path.Combine(fileDataDir, pdbLigandClusterInfoFileName);
                WritePfamLigandClusterInfoTable(pdbClusterInfoFile, ligandClusterNeedTable, pdbCoordDomains, domainFileChainMapHash, domainUnpPfamPmlNameDict);
                pdbTextFileList.Add(pdbLigandClusterInfoFileName);
            }
            string pfamInstructFile = Path.Combine(fileDataDir, "HowToUsePfamLigandsData_pdb.txt");
            ReWriteInstructFile(groupFileName, "pdb", generalInstructFile, pfamInstructFile);
            pdbTextFileList.Add("HowToUsePfamLigandsData_pdb.txt");
            CompressPfamDomainFiles(coordDomains, pdbTextFileList.ToArray (), groupFileName, fileDataDir, "pdb");
            
            /******************************************************/
            // unp
            string[] unpCoordDomains = GetPfamEntryChainDomains(pfamBestStructChainDomainHashes[1], coordDomains);
         /*    if (!unpCoordDomains.Contains(centerDomain))  // if the unp list doesnot contain the center domain, add it to the list
            {
                string[] unpCoordDomainsWithBest = new string[unpCoordDomains.Length + 1];
                unpCoordDomainsWithBest[0] = centerDomain;
                Array.Copy(unpCoordDomains, 0, unpCoordDomainsWithBest, 1, unpCoordDomains.Length);
                unpCoordDomains = unpCoordDomainsWithBest;
            }
            string newPymolScriptFileName = "";
           string[] unpPymolScriptFiles = new string[pdbPymolScriptFiles.Length];           
            int count = 0;
            foreach (string pymolScriptFile in pdbPymolScriptFiles)
            {
                newPymolScriptFileName = ReWritePymolScriptFile(Path.Combine(fileDataDir, pymolScriptFile), unpCoordDomains, "unp");
                unpPymolScriptFiles[count] = newPymolScriptFileName;
                count++;
            }*/
            Dictionary<int, string[]> unpClusterLigandsHash = GetClusterLigandsHash(ligandClusterNeedTable, unpCoordDomains, domainFileChainMapHash);
            string[] unpPymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, unpCoordDomains, centerDomain, fileDataDir,
                                domainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainInteractingLigandsHash, domainInteractingDnaRnaHash,
                                ligandNameDomainChainHash, unpClusterLigandsHash, "unp");
            List<string> unpTextFileList = new List<string>();
            unpTextFileList.AddRange(unpPymolScriptFiles);
            string renameScriptFile = Path.Combine(fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
            string renameScriptFileName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects(coordDomains, renameScriptFile, domainUnpPfamPmlNameDict, ".pfam");
//            string[] unpPfamsUnpPmlScriptFiles = pmlScriptUpdate.RewritePymolScriptFilesByUnpPfams(unpPymolScriptFiles, domainUnpPfamPmlNameDict, fileDataDir);
            unpTextFileList.Add(renameScriptFileName);
            unpTextFileList.Add(pfamAssingmentInfoFile);
 /*           if (ligandClusterNeedTable.Rows.Count > 0)
            {
                string newUnpPfamLigandClusterFileName = ReWritePfamLigandClusterInfoFile(Path.Combine(fileDataDir, pfamLigandClusterInfoFileName), unpCoordDomains, "unp");
                unpTextFileList.Add(newUnpPfamLigandClusterFileName);
            }*/
            string unpLigandClusterInfoFileName = groupFileName + "-ligands_clusters_unp.txt";
            if (ligandClusterNeedTable.Rows.Count > 0)
            {
                string unpClusterInfoFile = Path.Combine(fileDataDir, unpLigandClusterInfoFileName);
                WritePfamLigandClusterInfoTable(unpClusterInfoFile, ligandClusterNeedTable, unpCoordDomains, domainFileChainMapHash, domainUnpPfamPmlNameDict);
                unpTextFileList.Add(unpLigandClusterInfoFileName);
            }
            string newUnpInstructFile = Path.Combine(fileDataDir, "HowToUsePfamLigandsData_unp.txt");
            ReWriteInstructFile(groupFileName, "unp", generalInstructFile, newUnpInstructFile);
            unpTextFileList.Add("HowToUsePfamLigandsData_unp.txt");
            CompressPfamDomainFiles(unpCoordDomains, unpTextFileList.ToArray(), groupFileName, fileDataDir, "unp");

            /************************************************/
            // cryst form, based on PDB selection
            string[] crystCoordDomains = GetPfamEntryChainDomains(pfamBestStructChainDomainHashes[2], coordDomains);
            if (!crystCoordDomains.Contains(centerDomain)) // if the cryst list doesnot contain the center domain, add it to the list
            {
                string[] crystCoordDomainsWithBest = new string[crystCoordDomains.Length + 1];
                crystCoordDomainsWithBest[0] = centerDomain;
                Array.Copy(crystCoordDomains, 0, crystCoordDomainsWithBest, 1, crystCoordDomains.Length);
                crystCoordDomains = crystCoordDomainsWithBest;
            }
            string newPymolScriptFileName = "";
            string[] crystPymolScriptFiles = new string[pdbPymolScriptFiles.Length];
            List<string> crystTextFileList = new List<string>();
            int count = 0;
            foreach (string pymolScriptFile in pdbPymolScriptFiles)
            {
                newPymolScriptFileName = pmlScriptUpdate.ReWritePymolScriptFile(Path.Combine(fileDataDir, pymolScriptFile), crystCoordDomains, "cryst");
                crystPymolScriptFiles[count] = newPymolScriptFileName;
                count++;
            }
            crystTextFileList.AddRange(crystPymolScriptFiles);
            string crystRenameScriptFile = Path.Combine(fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
            string crystRenameScriptFileName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects(crystCoordDomains, crystRenameScriptFile, domainUnpPfamPmlNameDict, ".pfam");
//            string[] unpPfamsCrystPmlScriptFiles = pmlScriptUpdate.RewritePymolScriptFilesByUnpPfams(crystPymolScriptFiles, domainUnpPfamPmlNameDict, fileDataDir);
            crystTextFileList.Add(crystRenameScriptFileName);
            crystTextFileList.Add (pfamAssingmentInfoFile);

            if (ligandClusterNeedTable.Rows.Count > 0)
            {
                string newCrystPfamLigandClusterFileName = ReWritePfamLigandClusterInfoFile(Path.Combine(fileDataDir, pdbLigandClusterInfoFileName), crystCoordDomains, "cryst");
                crystTextFileList.Add (newCrystPfamLigandClusterFileName);
            }            
            string newCrystInstructFile = Path.Combine(fileDataDir, "HowToUsePfamLigandsData_cryst.txt");
            ReWriteInstructFile(groupFileName, "cryst", generalInstructFile, newCrystInstructFile);           
            crystTextFileList.Add("HowToUsePfamLigandsData_cryst.txt");           
            CompressPfamDomainFiles(crystCoordDomains, crystTextFileList.ToArray (), groupFileName, fileDataDir, "cryst");

            string[] dataFiles = Directory.GetFiles(fileDataDir, "*.pfam");
            foreach (string dataFile in dataFiles)
            {
                File.Delete(dataFile);
            }
            string[] pymolScriptFiles = Directory.GetFiles(fileDataDir, "*.pml");
            foreach (string scriptFile in pymolScriptFiles)
            {
                File.Delete(scriptFile);
            }

            string[] txtFiles = Directory.GetFiles(fileDataDir, "*.txt");
            foreach (string txtFile in txtFiles)
            {
                if (txtFile == generalInstructFile)
                {
                    continue;
                }
                File.Delete(txtFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFileName"></param>
        /// <param name="coordDomains"></param>
        /// <param name="centerDomain"></param>
        /// <param name="ligandClusterNeedTable"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="domainRangesHash"></param>
        /// <param name="domainCoordSeqIdsHash"></param>
        /// <param name="domainFileChainMapHash"></param>
        /// <param name="domainInteractingLigandsHash"></param>
        /// <param name="domainInteractingDnaRnaHash"></param>
        /// <param name="ligandNameDomainChainHash"></param>
        /// <param name="clusterLigandsHash"></param>
        /// <param name="domainUnpPfamPmlNameDict"></param>
        /// <param name="pdbOrUnp"></param>
        private void CompressPfamDomainCoordFile(string groupFileName, string[] coordDomains, string centerDomain, DataTable ligandClusterNeedTable, DataTable pfamDomainTable,
           string fileDataDir, Dictionary<string, Range[]> domainRangesHash, Dictionary<string, int[]> domainCoordSeqIdsHash, Dictionary<string, string[]> domainFileChainMapHash,
            Dictionary<string, string[]> domainInteractingLigandsHash, Dictionary<string, string[]> domainInteractingDnaRnaHash, Dictionary<string, string[]> ligandNameDomainChainHash,
            Dictionary<int, string[]> clusterLigandsHash, Dictionary<string, string> domainUnpPfamPmlNameDict, string pdbOrUnp)
        {
            string[] pdbPymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, coordDomains, centerDomain, fileDataDir,
                                domainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainInteractingLigandsHash, domainInteractingDnaRnaHash,
                                ligandNameDomainChainHash, clusterLigandsHash, "pdb");
            List<string> compressTextFileList = new List<string>(pdbPymolScriptFiles);

            string renameScriptFile = Path.Combine(fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
            string renameScriptFileName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects(coordDomains, renameScriptFile, domainUnpPfamPmlNameDict, ".pfam");
            compressTextFileList.Add(renameScriptFileName);

            string pfamAssingmentInfoFile = groupFileName + "_PdbfamAssignments.txt";
            WritePfamDomainAssignmentsToTextFile(coordDomains, Path.Combine(fileDataDir, pfamAssingmentInfoFile));
            compressTextFileList.Add(pfamAssingmentInfoFile);

            // add pfam-ligands clusters info
            string pfamLigandClusterInfoFileName = groupFileName + "-ligands_clusters_"+ pdbOrUnp + ".txt";
            if (ligandClusterNeedTable.Rows.Count > 0)
            {
                string pfamId = ligandClusterNeedTable.Rows[0]["PfamID"].ToString().TrimEnd();
                string clusterInfoFile = Path.Combine(fileDataDir, pfamLigandClusterInfoFileName);
                WritePfamLigandClusterInfoTable(clusterInfoFile, ligandClusterNeedTable, coordDomains, domainFileChainMapHash, domainUnpPfamPmlNameDict);
                compressTextFileList.Add(pfamLigandClusterInfoFileName);
            }
            string pfamInstructFile = Path.Combine(fileDataDir, "HowToUsePfamLigandsData_" + pdbOrUnp + ".txt");
            ReWriteInstructFile(groupFileName, pdbOrUnp, generalInstructFile, pfamInstructFile);
            compressTextFileList.Add("HowToUsePfamLigandsData_" + pdbOrUnp + ".txt");
            string[] compressTextFiles = new string[compressTextFileList.Count];
            compressTextFileList.CopyTo(compressTextFiles);
            CompressPfamDomainFiles(coordDomains, compressTextFiles, groupFileName, fileDataDir, pdbOrUnp);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFileName"></param>
        /// <param name="coordDomains"></param>
        /// <param name="centerDomain"></param>
        /// <param name="pfamBestStructChainDomainHash"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="domainRangesHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="domainCoordSeqIdsHash"></param>
        /// <param name="domainFileChainMapHash"></param>
        /// <param name="fileToBeDeleted"></param>
        public void CompressDomainCoordFiles(string groupFileName, string[] coordDomains, string centerDomain, 
                                       string fileDataDir, Dictionary<string, Range[]> domainRangesHash, DataTable pfamDomainTable,  
                                       Dictionary<string, int[]> domainCoordSeqIdsHash, Dictionary<string, string[]> domainFileChainMapHash, string dataType)
        {
            if (!coordDomains.Contains(centerDomain)) // somehow, the domain file for center domain not exist, then use the first available domain file
            {
                centerDomain = coordDomains[0];
            }

            Dictionary<string, string[]>[] domainInteractingLigandsHashes = GetChainDomainInteractingLigandsHash(coordDomains, domainFileChainMapHash);
            Dictionary<string, string[]> domainInteractingLigandsHash = domainInteractingLigandsHashes[0];
            Dictionary<string, string[]> domainInteractingDnaRnaHash = domainInteractingLigandsHashes[1];
            Dictionary<string, string[]> ligandNameDomainChainHash = domainInteractingLigandsHashes[2];

            Dictionary<int, string[]> ligandClusterHash = null;

            string[] pdbPymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, coordDomains, centerDomain, fileDataDir,
                domainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainInteractingLigandsHash, domainInteractingDnaRnaHash, ligandNameDomainChainHash, ligandClusterHash, dataType);
            CompressPfamDomainFiles(coordDomains, pdbPymolScriptFiles, groupFileName, fileDataDir, dataType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="groupFileName"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="dataType"></param>
        private void CompressPfamDomainFiles(string[] coordDomains, string[] pymolScriptFiles, string groupFileName, string fileDataDir, string dataType)
        {
            string destDataDir = Path.Combine(fileDataDir, dataType);
            CopyDomainFilesToFolder(coordDomains, fileDataDir, destDataDir);
            CopyPymolScriptFiles(pymolScriptFiles, fileDataDir, destDataDir);
            string tarGroupFileName = groupFileName + "_" + dataType + ".tar.gz";

            CompressGroupPfamDomainFiles (coordDomains, pymolScriptFiles, tarGroupFileName, fileDataDir, destDataDir);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="groupFileName"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="dataType"></param>
        public void CompressGroupPfamDomainFiles(string[] coordDomains, string[] pymolScriptFiles, string groupFileName, string srcDataDir, string destDataDir)
        {
       //     MoveDomainFilesToDest (coordDomains, srcDataDir, destDataDir);
       //     MovePymolScriptFiles (pymolScriptFiles, srcDataDir, destDataDir);
            string tarGroupFileName = groupFileName;
            if (groupFileName.IndexOf("tar.gz")< 0)
            {
                tarGroupFileName = groupFileName + ".tar.gz";
            }

            string[] domainCoordFiles = new string[coordDomains.Length];
            for (int i = 0; i < coordDomains.Length; i++)
            {
                domainCoordFiles[i] = coordDomains[i] + ".pfam";
            }
            string[] filesToBeCompressed = new string[domainCoordFiles.Length + pymolScriptFiles.Length];
            Array.Copy(domainCoordFiles, 0, filesToBeCompressed, 0, domainCoordFiles.Length);
            Array.Copy(pymolScriptFiles, 0, filesToBeCompressed, domainCoordFiles.Length, pymolScriptFiles.Length);

            string tarFileName = fileCompress.RunTar(tarGroupFileName, filesToBeCompressed, destDataDir, true);

            if (filesToBeCompressed.Length > fileCompress.maxNumOfFiles)
            {
                string fileFolder = Path.Combine(destDataDir, tarGroupFileName.Replace (".tar.gz", ""));
                Directory.Delete(fileFolder, true);
            }
            else
            {
                foreach (string domainFile in filesToBeCompressed)
                {
                    File.Delete(Path.Combine(destDataDir, domainFile));
                }
            }
        }

        #region copy/move files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="srcDataDir"></param>
        /// <param name="destDataDir"></param>
        public void CopyDomainFilesToFolder(string[] coordDomains, string srcDataDir, string destDataDir)
        {
            string domainFile = "";
            string destDomainFile = "";
            foreach (string coordDomain in coordDomains)
            {
                domainFile = Path.Combine(srcDataDir, coordDomain + ".pfam");
                destDomainFile = Path.Combine(destDataDir, coordDomain + ".pfam");
                if (File.Exists(domainFile))
                {
                    File.Copy(domainFile, destDomainFile, true);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="srcDataDir"></param>
        /// <param name="destDataDir"></param>
        public void CopyPymolScriptFiles (string[] pymolScriptFiles, string srcDataDir, string destDataDir)
        {
            string srcScriptFile = "";
            string destScriptFile = "";
            foreach (string scriptFile in pymolScriptFiles)
            {
                srcScriptFile = Path.Combine(srcDataDir, scriptFile);
                destScriptFile = Path.Combine(destDataDir, scriptFile);
                if (File.Exists(srcScriptFile))
                {
                    File.Copy(srcScriptFile, destScriptFile, true);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="srcDataDir"></param>
        /// <param name="destDataDir"></param>
        public void MovePymolScriptFiles(string[] pymolScriptFiles, string srcDataDir, string destDataDir)
        {
            string srcScriptFile = "";
            string destScriptFile = "";
            foreach (string scriptFile in pymolScriptFiles)
            {
                srcScriptFile = Path.Combine(srcDataDir, scriptFile);
                destScriptFile = Path.Combine(destDataDir, scriptFile);
                if (File.Exists(srcScriptFile))
                {
                    if (File.Exists(destScriptFile))
                    {
                        File.Delete(destScriptFile);
                    }
                    File.Move (srcScriptFile, destScriptFile);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="destDir"></param>
        public void MoveFilesToDest(string srcDir, string destDir)
        {
            string[] srcFiles = Directory.GetFiles(srcDir);
            string destFile = "";
            foreach (string srcFile in srcFiles)
            {
                FileInfo fileInfo = new FileInfo(srcFile);
                destFile = Path.Combine(destDir, fileInfo.Name);
                if (File.Exists(srcFile))
                {
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    File.Move(srcFile, destFile);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="destDir"></param>
        /// <param name="coordDomains"></param>
        public void MoveDomainFilesToDest(string[] coordDomains, string srcDir, string destDir)
        {
            string srcFile = "";
            string destFile = "";
            foreach (string coordDomain in coordDomains)
            {
                srcFile = Path.Combine(srcDir, coordDomain + ".pfam");
                destFile = Path.Combine(destDir, coordDomain + ".pfam");
                if (File.Exists(srcFile))
                {
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    File.Move(srcFile, destFile);
                }
            }
        }
        #endregion

        #region Pfam assignment text file   
        private string[] pfamColumns = {"PdbID", "DomainID", "AsymChain", "AuthChain", "EntityID", 
                                    "Pfam_Acc", "Pfam_ID", "Description", 
                                    "SeqStart", "SeqEnd", "AlignStart" , "AlignEnd", "HmmStart", "HmmEnd", 
                                    "PdbSeqStart", "PdbSeqEnd", "PdbAlignStart", "PdbAlignEnd",
                                    "BitScore", "Evalue", "SeqAlignment", "HmmAlignment", 
                                    "Clan_ID", "Clan_Acc", "UniprotID", "UniprotCode", "ChainPfamArch"};
        private string annotation = "#PdbID: PDB code \r\n" +
                                "#DomainID: the integer ID for each domain\r\n" +
                                "#AsymChain: the asymmetric chain ID\r\n" +
                                "#AuthChain: the author chain ID\r\n" +
                                "#EntityID: the integer ID for each sequence\r\n" +
                                "#Pfam_ACC: Pfam accession code\r\n" +
                                "#Pfam_ID: Pfam ID code\r\n" +
                                "#Description: Pfam description\r\n" +
                                "#SeqStart: the begin position of domain in PDB sequence. This is generally the envelope begin position in HMMER search. The envelope alignment is generally wider than the actual alignment region to Pfam HMM.\r\n" +
                                "#SeqEnd: the end position of domain in PDB sequence. This is generally the envelope end position in HMMER search.\r\n" +
                                "#AlignStart: the begin position of domain in the alignment to Pfam HMM. This matches the position in Pfam HMM.\r\n" +
                                "#AlignEnd: the end position of domain in the alignment to Pfam HMM. This matches the position in Pfam HMM.\r\n" +
                                "#HmmStart: the begin position of Pfam HMM\r\n" +
                                "#HmmEnd: the end position of Pfam HMM\r\n" +
                                "#PdbSeqStart: the begin position of PDB sequence in PDB sequence numbering\r\n" +
                                "#PdbSeqEnd: the end position of PDB sequence in PDB sequence numbering\r\n" +
                                "#PdbAlignStart: the begin position of domain in the alignment to Pfam HMM\r\n" +
                                "#PdbAlignEnd: the end position of domain in the alignment to Pfam HMM\r\n" +
                                "#BitScore: the bit score\r\n" +
                                "#Evalue: E-value\r\n" +
                                "#SeqAlignment: the PDB sequence aligned to Pfam HMM\r\n" +
                                "#HmmAlignment: the Pfam HMM sequence in the alignment\r\n" +
                                "#Clan_ID: Pfam clan ID\r\n" +
                                "#Clan_Acc: Pfam clan accession\r\n" +
                                "#UniProtID: UniProt accession code\r\n" +
                                "#UniProtCode: UniProt ID code\r\n" +
                                "#ChainPfamArch: the Pfam architecture of the chain containing this domain";
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public void WritePfamDomainAssignmentsToTextFile(string[] coordDomains, string domainInfoFile)
        {
            StreamWriter dataWriter = new StreamWriter(domainInfoFile);            
            dataWriter.WriteLine(annotation);
            dataWriter.WriteLine();
            string headerLine = "";
            foreach (string col in pfamColumns)
            {
                headerLine += (col + "\t");
            }
            
            dataWriter.WriteLine(headerLine.TrimEnd ('\t'));
            string pdbId = "";
            int chainDomainId = 0;
            string chainPfamInfoLine = "";
            foreach (string coordDomain in coordDomains)
            {
                pdbId = coordDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(coordDomain.Substring(4, coordDomain.Length - 4));
                chainPfamInfoLine = pfamOut.GetEntryChainPfamAssignments(pdbId, chainDomainId, pfamColumns);
                dataWriter.WriteLine(chainPfamInfoLine);
            }
            dataWriter.Close();
        }
        #endregion
        #endregion

        #region add cluster info to text files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamLigandClusterFile"></param>
        /// <param name="ligandClusterTable"></param>
        /// <param name="coordDomains"></param>
        /// <param name="asymFileChainMapHash"></param>
        /// <param name="?"></param>
        private void WritePfamLigandClusterInfoTable(string pfamLigandClusterFile, DataTable ligandClusterTable, string[] coordDomains,
            Dictionary<string, string[]> asymFileChainMapHash, Dictionary<string, string> domainUnpPfamPmlNameDict)
        {
            StreamWriter dataWriter = new StreamWriter(pfamLigandClusterFile);
            dataWriter.WriteLine("PfamID\tClusterID\tPdbDomain\tNewPmlObjName\tLigandFileChain\tLigandName\tAsymChain\tSeqID\tAuthorChain\tAuthorSeqID\tFullName");
            dataWriter.WriteLine("#NewPmlObjName: the object name with UniProt code and Pfam architecture for the domain.");
            dataWriter.WriteLine("#LigandFileChain: the chain ID of a ligand in the provided coordinate file, which may not be same as the chain IDs in the corresponding PDB file.");
            List<int> clusterIdList = new List<int>();
            int clusterId = 0;
            foreach (DataRow ligandRow in ligandClusterTable.Rows)
            {
                clusterId = Convert.ToInt32(ligandRow["ClusterID"].ToString());
                if (!clusterIdList.Contains(clusterId))
                {
                    clusterIdList.Add(clusterId);
                }
            }
            clusterIdList.Sort();
            string dataLine = "";
            string pdbId = "";
            string pfamId = "";
            string chainDomain = "";
            string ligandAsymChain = "";
            string ligandFileChain = "";
            string ligandPdbInfo = "";
            string newPmlObjName = "";
            foreach (int lsClusterId in clusterIdList)
            {
                DataRow[] clusterRows = ligandClusterTable.Select(string.Format("ClusterID = {0}", lsClusterId));
                dataLine = "";
                foreach (DataRow ligandRow in clusterRows)
                {
                    pdbId = ligandRow["PdbID"].ToString();
                    pfamId = ligandRow["PfamID"].ToString().TrimEnd();
                    chainDomain = pdbId + ligandRow["ChainDomainID"].ToString();
                    string[] chainMap = (string[])asymFileChainMapHash[chainDomain];
                    string[] asymChains = chainMap[0].Split(',');
                    string[] fileChains = chainMap[1].Split(',');
                    ligandAsymChain = ligandRow["LigandChain"].ToString().TrimEnd();
                    if (!IsDomainLigandInteractionIncluded(chainDomain, ligandAsymChain))
                    {
                        continue;
                    }
                    ligandFileChain = MapAsymChainToFileChain(ligandAsymChain, asymChains, fileChains);
                    newPmlObjName = chainDomain;
                    if (domainUnpPfamPmlNameDict != null && domainUnpPfamPmlNameDict.ContainsKey(chainDomain))
                    {
                        newPmlObjName = domainUnpPfamPmlNameDict[chainDomain];
                    }
                    if (coordDomains.Contains(chainDomain))
                    {
                        ligandPdbInfo = GetLigandPdbInfo(pdbId, ligandAsymChain, pdbLigandTable);
                        dataLine += (pfamId + "\t" + lsClusterId.ToString() + "\t" + chainDomain + "\t" + newPmlObjName + "\t" +
                            ligandFileChain + "\t" + ligandPdbInfo + "\n");
                    }
                }
                if (dataLine != "")
                {
                    dataWriter.Write(dataLine);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomain"></param>
        /// <param name="ligandChainId"></param>
        /// <returns></returns>
        private bool IsDomainLigandInteractionIncluded(string entryDomain, string ligandChainId)
        {
            if (domainLigandChainListDict.ContainsKey(entryDomain))
            {
                foreach (string ligand in domainLigandChainListDict[entryDomain])
                {
                    string[] fields = ligand.Split(';');
                    if (fields[1] == ligandChainId)
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
        /// <param name="pfamLigandClusterFile"></param>
        /// <param name="ligandClusterTable"></param>
        /// <param name="coordDomains"></param>
        /// <param name="asymFileChainMapHash"></param>
        /// <param name="?"></param>
        private string ReWritePfamLigandClusterInfoFile(string pfamLigandClusterFile, string[] coordDomains, string dataType)
        {
            StreamReader dataReader = new StreamReader(pfamLigandClusterFile);
            string newPfamLigandClusterFile = pfamLigandClusterFile.Replace("_pdb", "_" + dataType);
            StreamWriter dataWriter = new StreamWriter(newPfamLigandClusterFile);
            string line = "";
            string headerLine = dataReader.ReadLine();
            dataWriter.WriteLine(headerLine);
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    dataWriter.WriteLine(line);
                    continue;
                }
                if (line.Substring(0, 1) == "#")
                {
                    dataWriter.WriteLine(line);
                    continue;
                }
                string[] fields = line.Split('\t');
                if (coordDomains.Contains(fields[2]))
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            dataWriter.Close();
            FileInfo fileInfo = new FileInfo(newPfamLigandClusterFile);
            return fileInfo.Name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="pdbLigandTable"></param>
        /// <returns></returns>
        private string GetLigandPdbInfo(string pdbId, string asymChain, DataTable pdbLigandTable)
        {
            string ligandPdbInfo = "";
            if (pdbLigandTable != null)
            {
                DataRow[] ligandInfoRows = pdbLigandTable.Select(string.Format("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain));
                if (ligandInfoRows.Length > 0)
                {
                    ligandPdbInfo = ligandInfoRows[0]["Ligand"].ToString() + "\t" + asymChain + "\t" +
                        ligandInfoRows[0]["SeqID"].ToString() + "\t" +
                        ligandInfoRows[0]["AuthorChain"].ToString().TrimEnd() + "\t" + ligandInfoRows[0]["AuthSeqID"].ToString() + "\t" +
                        ligandInfoRows[0]["Name"].ToString();
                }
            }
            else
            {
                string queryString = string.Format("Select * From PdbLigands Where PdbID = '{0}' AND AsymChain = '{1}';", pdbId, asymChain);
                DataTable ligandInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
                if (ligandInfoTable.Rows.Count > 0)
                {
                    ligandPdbInfo = ligandInfoTable.Rows[0]["Ligand"].ToString() + "\t" + asymChain + "\t" +
                        ligandInfoTable.Rows[0]["SeqID"].ToString() + "\t" +
                        ligandInfoTable.Rows[0]["AuthorChain"].ToString().TrimEnd() + "\t" + ligandInfoTable.Rows[0]["AuthSeqID"].ToString() + "\t" +
                        ligandInfoTable.Rows[0]["Name"].ToString();
                }
            }
            return ligandPdbInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="dataType"></param>
        /// <param name="pfamInstructFile"></param>
        private void ReWriteInstructFile(string pfamId, string dataType, string generalInstructFile, string pfamInstructFile)
        {
            string addedDataLine = "*************************************\n" +
                                    "Pfam-ligands file name: " + pfamId + "_" + dataType + "\n" +
                                    "*************************************\n";
            if (dataType == "pdb")
            {
                addedDataLine += "For one PBD structure, only one domain with most of coordinates is used. So missing ligands may be possible " +
                " if the ligands do not interact with this selected domain, but interact with other domains. \n";
            }
            else if (dataType == "unp")
            {
                addedDataLine += "For one sequence (based on UniProt code), only one domain with most of coordinates is used. So missing ligands may be possible " +
                " if the ligands do not interact with this selected domain, but interact with other domains. \n";
            }
            else if (dataType == "cryst")
            {
                addedDataLine += "For one crystal form, only one domain with most of coordinates is used. So missing ligands may be possible " +
                " if the ligands do not interact with this selected domain, but interact with other domains. \n";
            }
            addedDataLine += "Please search by PfamID/Pfam Accession code http://dunbrack2.fccc.edu/ProtCiD/Search/pfamId.aspx, then download from Pfam web page.\n";
            StreamReader dataReader = new StreamReader(generalInstructFile);
            string instructDataStream = dataReader.ReadToEnd();
            // add pfam name  _unpPfams_byChain  _unpPfams1-cysPrx_C_byChain
            instructDataStream = instructDataStream.Replace("_byChain.pml", pfamId + "_byChain.pml");
            instructDataStream = instructDataStream.Replace("_byDomain.pml", pfamId + "_byDomain.pml");
            instructDataStream = instructDataStream.Replace("_pairFit.pml", pfamId + "_pairFit.pml");
            instructDataStream = instructDataStream.Replace("_pairFitDomain.pml", pfamId + "_pairFitDomain.pml");

            instructDataStream = instructDataStream.Replace("_RenameByUnpPfams.pml", pfamId + "_RenameByUnpPfams.pml");
            instructDataStream = instructDataStream.Replace("_RenumberResiduesByUnp.pml", pfamId + "_RenumberResiduesByUnp.pml");
           

            instructDataStream = instructDataStream.Replace("Pfam_PdbfamAssignments.txt", pfamId + "_PdbfamAssignments.txt");
            dataReader.Close();
            StreamWriter dataWriter = new StreamWriter(pfamInstructFile);
            dataWriter.Write(addedDataLine);
            dataWriter.Write(instructDataStream);
            dataWriter.Close();
        }
        #endregion

        #region update compiling domain files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        public void UpdatePfamDomainFilesInPfams (string[] pfamIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress domain align in pymol");

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentOperationLabel = pfamId;

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                ProtCidSettings.logWriter.WriteLine(pfamId);

                try
                {
                    CompilePfamDomainWithLigandFiles(pfamId, null);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamDomainFiles(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress domain align in pymol");
            ProtCidSettings.logWriter.WriteLine("Compress domain align in pymol");

            string queryString = "Select * From PdbLigands;";
            pdbLigandTable = ProtCidSettings.protcidQuery.Query( queryString);

            Dictionary<string, string[]> updatePfamEntryHash = GetUpdatePfamIds(updateEntries);
            List<string> updatePfamIdList = new List<string>(updatePfamEntryHash.Keys);
            updatePfamIdList.Sort();
            ProtCidSettings.progressInfo.totalOperationNum = updatePfamIdList.Count;
            ProtCidSettings.progressInfo.totalStepNum = updatePfamIdList.Count;

            foreach (string pfamId in updatePfamIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (string.Compare(pfamId, "Ribosomal_L5_C") <= 0)
                {
                    continue;
                }

                string[] pfamUpdateEntries = (string[])updatePfamEntryHash[pfamId];

                try
                {
                    UpdateCompilingPfamDomainFiles(pfamId, pfamUpdateEntries);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Updating domain alignment pymol session error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Updating domain alignment pymol session error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Updating domain alignment pymol sessions Done!");
            ProtCidSettings.logWriter.Flush();
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="bestStructTypes"></param>
        private void UpdateCompilingPfamDomainFiles(string pfamId, string[] updateEntries)
        {
            string[] pfamEntries = GetPfamEntries(pfamId);
            DataTable asuTable = GetEntryAsuTable(pfamEntries);
            UpdateCompilingPfamDomainFiles(pfamId, asuTable, updateEntries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        private void UpdateCompilingPfamDomainFiles(string pfamId, DataTable asuTable, string[] updateEntries)
        {
  //          string pfamAcc = GetPfamAccFromPfamId(pfamId);
            string groupFileName = pfamId;
            string domainAlignFile = Path.Combine(pfamDomainPymolFileDir, "pdb\\" + groupFileName + "_pdb.tar.gz");
            if (File.Exists(domainAlignFile))
            {
                return;
                // retrieve the exist domain files from pdb folder since it contains more domain files than unp and cryst
 //               tarOperator.UnTar(domainAlignFile, pfamDomainPymolFileDir);
                // delete the domain files associated with the update entries
//                DeleteUpdateDomainFiles(updateEntries);
            }
            DeleteObsGroupAlignFiles(pfamId);

            CompressPfamDomainFiles(pfamId, asuTable, groupFileName);          
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteUpdateDomainFiles(string[] updateEntries)
        {
            foreach (string pdbId in updateEntries)
            {
                string[] domainFiles = Directory.GetFiles (pfamDomainPymolFileDir, pdbId + "*.pfam");
                foreach (string domainFile in domainFiles)
                {
                    File.Delete(domainFile);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        private void DeleteObsGroupAlignFiles(string pfamId)
        {
            string groupFileName = "";
            foreach (string bestStructType in bestStructTypes)
            {
                groupFileName = Path.Combine(pfamDomainPymolFileDir + "\\" + bestStructType, pfamId + "_" + bestStructType + ".tar.gz");
                File.Delete(groupFileName);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable GetEntryAsuTable(string[] entries)
        {
            DataTable asuTable = null;
            string queryString = "";
            for (int i = 0; i < entries.Length; i += 200 )
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 200);
                queryString = string.Format("Select PdbId, AsymID, AuthorChain, EntityID, SequenceInCoord, PolymerStatus, PolymerType From AsymUnit " +
                    " Where PdbID IN  ({0}) AND PolymerType = 'polypeptide';", ParseHelper.FormatSqlListString (subEntries));
                DataTable subAsuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subAsuTable, ref asuTable);
            }               
            return asuTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryAsuTable(string pdbId)
        {
            string queryString = string.Format ("Select PdbId, AsymID, AuthorChain, EntityID, SequenceInCoord, PolymerStatus, PolymerType From AsymUnit " +
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return asuTable;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamEntries(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pfamEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pfamEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetUpdatePfamIds(string[] updateEntries)
        {
            Dictionary<string, List<string>> pfamUpdateEntryListHash = new Dictionary<string, List<string>>();
            foreach (string pdbId in updateEntries)
            {
                string[] pfamIds = GetEntryPfamIds(pdbId);
                foreach (string pfamId in pfamIds)
                {
                    if (pfamUpdateEntryListHash.ContainsKey(pfamId))
                    {
                        if (!pfamUpdateEntryListHash[pfamId].Contains(pdbId))
                        {
                            pfamUpdateEntryListHash[pfamId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        pfamUpdateEntryListHash.Add(pfamId, entryList);
                    }
                }
            }
            Dictionary<string, string[]> pfamUpdateEntryDict = new Dictionary<string, string[]>();
            foreach (string pfamId in pfamUpdateEntryListHash.Keys)
            {
                pfamUpdateEntryDict.Add(pfamId, pfamUpdateEntryListHash[pfamId].ToArray ());
            }
            return pfamUpdateEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPfamIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamIds[count] = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }
        #endregion

        #region db info for pfam
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntryDomainHash">key: pdbid, value: pdbid + chain domain id</param>
        /// <returns></returns>
        public string[] GetPfamEntryChainDomains(Dictionary<string, string> pfamEntryDomainDict)
        {
            List<string> entryDomainList = new List<string> ();
            foreach (string pdbId in pfamEntryDomainDict.Keys)
            {
                entryDomainList.Add(pfamEntryDomainDict[pdbId]);
            }
            entryDomainList.Sort();
            return entryDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntryDomainHash">key: pdbid, value: pdbid + chain domain id</param>
        /// <returns></returns>
        public string[] GetPfamEntryChainDomains(Dictionary<string, string[]> pfamEntryDomainDict, string[] coordDomains)
        {
            List<string> entryDomainList = new List<string>();
            foreach (string pdbId in pfamEntryDomainDict.Keys)
            {
                foreach (string chainDomain in pfamEntryDomainDict[pdbId])
                {
                    if (Array.IndexOf(coordDomains, chainDomain) > -1)
                    {
                        entryDomainList.Add(chainDomain);
                    }
                }                
            }
            entryDomainList.Sort();
            return entryDomainList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntryDomainHash">key: pdbid, value: pdbid + chain domain id</param>
        /// <returns></returns>
        public string[] GetPfamEntryChainDomains(Dictionary<string, string[]> pfamEntryDomainDict)
        {
            List<string> entryDomainList = new List<string>();
            foreach (string pdbId in pfamEntryDomainDict.Keys)
            {
                entryDomainList.AddRange(pfamEntryDomainDict[pdbId]);
            }
            entryDomainList.Sort();
            return entryDomainList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntryDomainHash">key: pdbid, value: pdbid + chain domain id</param>
        /// <returns></returns>
        public string[] AddPfamEntryChainDomains(Dictionary<string, string[]> pfamEntryDomainDict, string[] existDomains)
        {
            List<string> entryDomainList = new List<string>(existDomains);
            foreach (string entry in pfamEntryDomainDict.Keys)
            {
                foreach (string domain in pfamEntryDomainDict[entry])
                {
                    if (! entryDomainList.Contains (domain))
                    {
                        entryDomainList.Add(domain);
                    }                   
                }
            }
            entryDomainList.Sort();
            return entryDomainList.ToArray();
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string GetPfamAccFromPfamId(string pfamId)
        {
            string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamAcc = "";
            if (pfamAccTable.Rows.Count > 0)
            {
                pfamAcc = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
            }
            return pfamAcc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private int[] GetChainDomainIds(DataTable chainPfamTable)
        {
            List<int> chainDomainIdList = new List<int>();
            int chainDomainId = 0;
            foreach (DataRow chainPfamRow in chainPfamTable.Rows)
            {
                chainDomainId = Convert.ToInt32(chainPfamRow["ChainDomainID"].ToString());
                if (!chainDomainIdList.Contains(chainDomainId))
                {
                    chainDomainIdList.Add(chainDomainId);
                }
            }
            return chainDomainIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainDomainIds"></param>
        /// <param name="ligandPfamChainDomains">must be sorted</param>
        /// <returns></returns>
        private string[][] SplitEntryChainDomainIdsWithLigands(string[] entryChainDomainIds, DataTable pfamLigandsTable)
        {
            string pdbId = "";
            int chainDomainId = 0;
            List<string> ligandChainDomainIdList = new List<string>();
            List<string> noLigandChainDomainIdList = new List<string>();
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring (4, entryChainDomainId.Length - 4));
                if (IsDomainInteractingWithLigands(pdbId, chainDomainId, pfamLigandsTable))
                {
                    ligandChainDomainIdList.Add(entryChainDomainId);
                }
                else
                {
                    noLigandChainDomainIdList.Add(entryChainDomainId);
                }
            }
            string[][] splitLigandChainDomainIds = new string[2][];
            splitLigandChainDomainIds[0] = new string[ligandChainDomainIdList.Count];
            ligandChainDomainIdList.CopyTo(splitLigandChainDomainIds[0]);
            splitLigandChainDomainIds[1] = new string[noLigandChainDomainIdList.Count];
            noLigandChainDomainIdList.CopyTo(splitLigandChainDomainIds[1]);
            return splitLigandChainDomainIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashtable1"></param>
        /// <param name="hashtable2"></param>
        private void MergeSecondDictToFirstDict (Dictionary<string, string> hashtable1, Dictionary<string, string> hashtable2)
        {
            foreach (string hashkey2 in hashtable2.Keys)
            {
                if (!hashtable1.ContainsKey(hashkey2))
                {
                    hashtable1.Add (hashkey2, hashtable2[hashkey2]);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashtable1"></param>
        /// <param name="hashtable2"></param>
        private void MergeSecondDictToFirstDict (Dictionary<string, string[]> dict1, Dictionary<string, string> dict2)
        {
            foreach (string hashkey2 in dict2.Keys)
            {
                if (! dict1.ContainsKey(hashkey2))
                {
                    string[] items = new string[1];
                    items[0] = dict2[hashkey2];
                    dict1.Add(hashkey2, items);
                }
            }
        }
        #endregion

        #region best chain domains of entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPfamBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable pfamLigandsTable, DataTable asuTable, out string entryDomainBestCoordInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pdbPfamChainTable);
            string[][] splitLigandEntryChainDomainIds = SplitEntryChainDomainIdsWithLigands(entryChainDomainIds, pfamLigandsTable);
            string[] ligandEntryDomains = splitLigandEntryChainDomainIds[0];
            string[] noLigandEntryDomains = splitLigandEntryChainDomainIds[1];
            string bestLigandChain = "";
            if (ligandEntryDomains.Length > 0)
            {
                bestLigandChain = ligandEntryDomains[0];
            }
            string bestNoLigandChain = "";
            if (noLigandEntryDomains.Length > 0)
            {
                bestNoLigandChain = noLigandEntryDomains[0];
            }
            Dictionary<string, string> entryBestStructChainDomainHash = GetPfamBestStructChainDomainIdHash(ligandEntryDomains, pdbPfamChainTable, asuTable, out bestLigandChain);
            Dictionary<string, string> entryBestNoLogandChainDomainHash = GetPfamBestStructChainDomainIdHash(noLigandEntryDomains, pdbPfamChainTable, asuTable, out bestNoLigandChain);
            MergeSecondDictToFirstDict (entryBestStructChainDomainHash, entryBestNoLogandChainDomainHash);
            entryDomainBestCoordInPfam = bestLigandChain;
            if (bestLigandChain == "")
            {
                entryDomainBestCoordInPfam = bestNoLigandChain;
            }
            return entryBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPfamBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, out string entryDomainBestCoordInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pdbPfamChainTable);

            Dictionary<string, string> entryBestStructChainDomainHash = GetPfamBestStructChainDomainIdHash(entryChainDomainIds, pdbPfamChainTable, asuTable, out entryDomainBestCoordInPfam);
                      
            return entryBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="noLigandDomains"></param>
        /// <param name="entryDomainBestCoordInPfam"></param>
        /// <param name="entryDomainsLigandsOrNot"></param>
        /// <returns>0: the representative domains of entries with ligands interacting; 1: the representative domains of entries without ligands interacting</returns>
        public Dictionary<string, string[]> GetPfamBestStructChainDomainsDict (DataTable pdbPfamChainTable, DataTable pfamLigandsTable, DataTable asuTable, string[] noLigandDomains,
            out string entryDomainBestCoordInPfam, out string[][] entryDomainsLigandsOrNot)
        {
            entryDomainsLigandsOrNot = new string[2][];
            entryDomainBestCoordInPfam = "";
            List<string> domainWithLigandList = new List<string>();
            Dictionary<string, string[]> entryBestDomainWithLigandDict = GetLigandBestDomainOnContactsAndCoordsDict(pfamLigandsTable, pdbPfamChainTable, asuTable, out entryDomainBestCoordInPfam, "pdb");
            foreach (string lsPdb in entryBestDomainWithLigandDict.Keys)
            {
                domainWithLigandList.AddRange(entryBestDomainWithLigandDict[lsPdb]);
            }
            entryDomainsLigandsOrNot[0] = domainWithLigandList.ToArray ();
            string entryDomainNoLigandCoordInPfam = "";
            Dictionary<string, string> entryBestDomainNoLigandDict = GetPfamBestStructChainDomainIdHash(noLigandDomains, pdbPfamChainTable, asuTable, out entryDomainNoLigandCoordInPfam);
            List<string> domainNoLigandList = new List<string>();
            foreach (string lsPdb in entryBestDomainNoLigandDict.Keys)
            {
                if (entryBestDomainWithLigandDict.ContainsKey (lsPdb))
                {
                    continue;
                }
                domainNoLigandList.Add(entryBestDomainNoLigandDict[lsPdb]);
            }
            entryDomainsLigandsOrNot[1] = domainNoLigandList.ToArray();

            MergeSecondDictToFirstDict(entryBestDomainWithLigandDict, entryBestDomainNoLigandDict);
            
            if (entryDomainBestCoordInPfam == "")
            {
                entryDomainBestCoordInPfam = entryDomainNoLigandCoordInPfam;
            }            
            return entryBestDomainWithLigandDict;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandDomainListDict"></param>
        /// <param name="pfamLigandsTable"> select pfamligands.pdbid, chainDomainId, ligandchain, ligand, count(*) as contactcount 
        /// From pdbligands, pfamligands where pfamId = '1-cysPrx_C' and pfamligands.pdbid = pdbligands.pdbid and pfamligands.ligandchain = pdbligands.asymchain 
        /// and pfamligands.ligandseqid = pdbligands.seqid group by pfamligands.pdbid, chaindomainid, ligandchain, ligand;</param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetLigandBestDomainOnContactsAndCoordsDict (DataTable pfamLigandsTable, DataTable pdbPfamTable, DataTable asuTable, out string pfamBestDomain, string pdbOrUnp)
        {
            Dictionary<string, List<string>> entryLigandDict = new Dictionary<string, List<string>>();
    //        List<string> ligandList = new List<string>();
            string ligand = "";
            string entryCode = "";
            foreach (DataRow ligandRow in pfamLigandsTable.Rows)
            {
                if (pdbOrUnp == "unp")
                {
                    entryCode = ligandRow["UnpCode"].ToString().TrimEnd();
                }
                else
                {
                    entryCode = ligandRow["PdbID"].ToString();
                }
                if (entryCode == "" || entryCode == null)
                {
                    continue;
                }
                ligand = ligandRow["Ligand"].ToString().TrimEnd();
                if (entryLigandDict.ContainsKey(entryCode))
                {
                    if (!entryLigandDict[entryCode].Contains(ligand))
                    {
                        entryLigandDict[entryCode].Add(ligand);
                    }
                }
                else
                {
                    List<string> ligandList = new List<string>();
                    ligandList.Add(ligand);
                    entryLigandDict.Add(entryCode, ligandList);
                }
            }
            Dictionary<string, string[]> entrySelectDomainDict = new Dictionary<string, string[]>();
            int pfamMaxNumCoord = 0;
            pfamBestDomain = "";
            int entryMaxNumCoord = 0;
            string entryDomainMaxCoord = "";
            foreach (string lsEntry in entryLigandDict.Keys)
            {
                string[] selectedDomains = SelectLigandDomainsList(lsEntry, entryLigandDict[lsEntry], pfamLigandsTable, pdbPfamTable, asuTable, out entryMaxNumCoord, out entryDomainMaxCoord, pdbOrUnp);
                if (selectedDomains.Length > 0)
                {
                    entrySelectDomainDict.Add(lsEntry, selectedDomains);
                    if (pfamMaxNumCoord < entryMaxNumCoord)
                    {
                        pfamMaxNumCoord = entryMaxNumCoord;
                        pfamBestDomain = entryDomainMaxCoord;
                    }
                }
            }
            return entrySelectDomainDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandList">the list of ligand names, not the chain IDs</param>
        /// <param name="pfamLigandsTable"></param>
        /// <param name="pdbPfamTable"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private string[] SelectLigandDomainsList(string inputCode, List<string> ligandList, DataTable pfamLigandsTable, DataTable pdbPfamTable, DataTable asuTable, out int maxNumCoord, out string domainMaxCoord, string isPdbOrUnp)
        {
            int maxContacts = 0;
            Dictionary<string, List<string>> ligandMaxContactDomainsDict = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> domainLigandsDict = new Dictionary<string, List<string>>();
            int numContacts = 0;
            string entryDomain = "";
            DataRow[] ligandRows = null;
            foreach (string lsLigand in ligandList)
            {
                List<string> ligandDomainList = new List<string>();
                if (isPdbOrUnp == "unp")
                {
                    ligandRows = pfamLigandsTable.Select(string.Format("UnpCode = '{0}' AND Ligand = '{1}'", inputCode, lsLigand), "ContactCount DESC");
                }
                else
                {
                    ligandRows = pfamLigandsTable.Select(string.Format("PdbID = '{0}' AND Ligand = '{1}'", inputCode, lsLigand), "ContactCount DESC");
                }
                maxContacts = Convert.ToInt32(ligandRows[0]["ContactCount"].ToString());
                foreach (DataRow ligandRow in ligandRows)
                {
                    numContacts = Convert.ToInt32(ligandRow["ContactCount"].ToString());
                    if (numContacts == maxContacts)
                    {
                        entryDomain = ligandRow["PdbID"].ToString() + ligandRow["ChainDomainID"].ToString();
                        if (!ligandDomainList.Contains(entryDomain))
                        {
                            ligandDomainList.Add(entryDomain);
                        }
                        if (domainLigandsDict.ContainsKey(entryDomain))
                        {
                            if (!domainLigandsDict[entryDomain].Contains(lsLigand))
                            {
                                domainLigandsDict[entryDomain].Add(lsLigand);
                            }
                        }
                        else
                        {
                            List<string> domainLigandList = new List<string>();
                            domainLigandList.Add(lsLigand);
                            domainLigandsDict.Add(entryDomain, domainLigandList);
                        }
                    }
                }
                ligandMaxContactDomainsDict.Add(lsLigand, ligandDomainList);
            }
            List<string> entryDomainList = new List<string>(domainLigandsDict.Keys);
            Dictionary<string, int> domainCoordNumDict = GetDomainNumCoordDict(entryDomainList, pdbPfamTable, asuTable);
            
            List<string> sortedEntryDomainList = SortEntryDomainsByContactingLigandsAndCoordinates(domainLigandsDict, domainCoordNumDict);
            List<string> selectedDomainList = new List<string>();
            int minDomainIndex = sortedEntryDomainList.Count;
            int domainIndex = -1;
            string bestDomain = "";
            foreach (string lsLigand in ligandMaxContactDomainsDict.Keys)
            {
                minDomainIndex = sortedEntryDomainList.Count;
                foreach (string lsDomain in ligandMaxContactDomainsDict[lsLigand])
                {
                    domainIndex = sortedEntryDomainList.IndexOf(lsDomain);
                    if (domainIndex > -1 && domainIndex < minDomainIndex)
                    {
                        minDomainIndex = domainIndex;
                    }
                }
                if (minDomainIndex == sortedEntryDomainList.Count )
                {
                    continue;
                }
                bestDomain = sortedEntryDomainList[minDomainIndex];
                if (!selectedDomainList.Contains(bestDomain))
                {
                    selectedDomainList.Add(bestDomain);
                }                
                if (domainLigandChainListDict.ContainsKey(bestDomain))
                {
                    if (!domainLigandChainListDict[bestDomain].Contains(lsLigand))
                    {
                        domainLigandChainListDict[bestDomain].Add(lsLigand);
                    }
                }
                else
                {
                    List<string> domainLigandList = new List<string>();  // the list of ligands interacting with this domain
                    domainLigandList.Add(lsLigand);
                    domainLigandChainListDict.Add(bestDomain, domainLigandList);
                }
            }

            maxNumCoord = 0;
            domainMaxCoord = "";
            foreach (string domain in selectedDomainList)
            {
                if (domainCoordNumDict[domain] > maxNumCoord)
                {
                    maxNumCoord = domainCoordNumDict[domain];
                    domainMaxCoord = domain;
                }
            }
            return selectedDomainList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryDomainList"></param>
        /// <param name="pdbPfamChainTable"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private Dictionary<string, int> GetDomainNumCoordDict(List<string> entryDomainList, DataTable pdbPfamChainTable, DataTable asuTable)
        {
            Dictionary<string, int> entryDomainNumCoordDict = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            int numOfCoord = 0;
            foreach (string entryChainDomainId in entryDomainList)
            {
                numOfCoord = -1;
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                try
                {
                    numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chainDomainId.ToString() + ": seq start end position error:" + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + chainDomainId.ToString() + ": seq start end position error:" + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                if (numOfCoord >= 0)  // remove those domains with no coordinates
                {
                    entryDomainNumCoordDict.Add(entryChainDomainId, numOfCoord);
                }
            }
            return entryDomainNumCoordDict;
        }

        /// <summary>
        /// sort domains in numbers of contact ligands, the numbers of coordinates
        /// </summary>
        /// <param name="domainLigandsDict"></param>
        /// <param name="domainCoordNumDict"></param>
        /// <returns></returns>
        private List<string> SortEntryDomainsByContactingLigandsAndCoordinates (Dictionary<string, List<string>> domainLigandsDict, Dictionary<string, int> domainCoordNumDict)
        {
            List<string> domainList = new List<string>();
            domainList.Sort();   // in alphabet order
            List<int> ligandNumList = new List<int>();
            List<int> coordNumList = new List<int>();
            foreach (string domain in domainLigandsDict.Keys) // in case data inconsistent
            {
                if (domainLigandsDict.ContainsKey(domain) && domainCoordNumDict.ContainsKey(domain))
                {
                    ligandNumList.Add(domainLigandsDict[domain].Count);
                    coordNumList.Add(domainCoordNumDict[domain]);
                    domainList.Add(domain);
                }
            }
            for (int i = 0; i < domainList.Count; i ++)
            {
                for (int j = i + 1; j < domainList.Count; j ++)
                {
                    if (ligandNumList[i] < ligandNumList[j])
                    {
                        int temp = ligandNumList[j];
                        ligandNumList[j] = ligandNumList[i];
                        ligandNumList[i] = temp;
                        string domainTemp = domainList[j];
                        domainList[j] = domainList[i];
                        domainList[i] = domainTemp;
                        temp = coordNumList[j];
                        coordNumList[j] = coordNumList[i];
                        coordNumList[i] = temp;
                    }
                    else if (ligandNumList[i] == ligandNumList[j])
                    {
                        if (coordNumList[i] < coordNumList[j])
                        {
                            int temp = ligandNumList[j];
                            ligandNumList[j] = ligandNumList[i];
                            ligandNumList[i] = temp;
                            string domainTemp = domainList[j];
                            domainList[j] = domainList[i];
                            domainList[i] = domainTemp;
                            temp = coordNumList[j];
                            coordNumList[j] = coordNumList[i];
                            coordNumList[i] = temp;
                        }
                    }
                }
            }
            return domainList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPfamUniqueChainDomainLigandHash(DataTable pdbPfamChainTable, DataTable pfamLigandsTable, DataTable asuTable, out string entryDomainBestCoordInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pdbPfamChainTable);
            string[][] splitLigandEntryChainDomainIds = SplitEntryChainDomainIdsWithLigands(entryChainDomainIds, pfamLigandsTable);
            string[] ligandEntryDomains = splitLigandEntryChainDomainIds[0];
            string[] noLigandEntryDomains = splitLigandEntryChainDomainIds[1];
            string bestLigandChain = "";
            if (ligandEntryDomains.Length > 0)
            {
                bestLigandChain = ligandEntryDomains[0];
            }
            string bestNoLigandChain = "";
            if (noLigandEntryDomains.Length > 0)
            {
                bestNoLigandChain = noLigandEntryDomains[0];
            }
            Dictionary<string, string> entryBestStructChainDomainHash = GetPfamBestStructChainDomainIdHash(ligandEntryDomains, pdbPfamChainTable, asuTable, out bestLigandChain);
            Dictionary<string, string> entryBestNoLogandChainDomainHash = GetPfamBestStructChainDomainIdHash(noLigandEntryDomains, pdbPfamChainTable, asuTable, out bestNoLigandChain);
            MergeSecondDictToFirstDict (entryBestNoLogandChainDomainHash, entryBestStructChainDomainHash);
            entryDomainBestCoordInPfam = bestLigandChain;
            if (bestLigandChain == "")
            {
                entryDomainBestCoordInPfam = bestNoLigandChain;
            }
            return entryBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainDomainIds"></param>
        /// <param name="pdbpfamChainTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="entryDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamBestStructChainDomainIdHash(string[] entryChainDomainIds, DataTable pdbPfamChainTable, DataTable asuTable, out string entryDomainBestCoordInPfam)
        {
            Dictionary<string, string> entryBestStructChainDomainHash = new Dictionary<string, string>();
            Dictionary<string, int> entryBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            int maxNumOfCoordInPfam = 0;
            int numOfCoord = 0;
            entryDomainBestCoordInPfam = "";
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                try
                {
                    numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chainDomainId.ToString () + ": seq start end position error:" + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + chainDomainId.ToString() + ": seq start end position error:" + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                if (maxNumOfCoordInPfam < numOfCoord)
                {
                    maxNumOfCoordInPfam = numOfCoord;
                    entryDomainBestCoordInPfam = entryChainDomainId;                   
                }
                if (entryBestStructChainDomainHash.ContainsKey(pdbId))
                {
                    int maxNumOfCoord = (int)entryBestStructNumCoordHash[pdbId];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        entryBestStructNumCoordHash[pdbId] = numOfCoord;
                        entryBestStructChainDomainHash[pdbId] = entryChainDomainId;
                    }
                }
                else
                {
                    entryBestStructChainDomainHash.Add(pdbId, entryChainDomainId);
                    entryBestStructNumCoordHash.Add(pdbId, numOfCoord);
                }
            }
            return entryBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <returns></returns>
        private bool IsDomainInteractingWithLigands(string pdbId, int chainDomainId, string[] ligandPfamChainDomains)
        {
            int domainIndex = Array.BinarySearch(ligandPfamChainDomains, pdbId + chainDomainId.ToString());
            if (domainIndex > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="pfamLigandsTable"></param>
        /// <returns></returns>
        private bool IsDomainInteractingWithLigands(string pdbId, int chainDomainId, DataTable pfamLigandsTable)
        {
            DataRow[] dataRows = pfamLigandsTable.Select(string.Format ("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
            if (dataRows.Length  > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomainRows"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private int GetNumOfCoordinates(DataRow[] chainDomainRows, DataTable asuTable)
        {
            if (chainDomainRows.Length == 0)
            {
                return -1;
            }
            string pdbId = chainDomainRows[0]["PdbID"].ToString();
            string asymChain = "";
            string sequenceInCoord = "";
            string domainSeqInCoord = "";
            int alignStart = 0;
            int alignEnd = 0;
            int numOfDomainCoord = 0;
            DataRow asuRow = null;
            foreach (DataRow chainRow in chainDomainRows)
            {
                asymChain = chainRow["AsymChain"].ToString().TrimEnd();
                if (asuTable != null)
                {
                    DataRow[] asuRows = asuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
                    if (asuRows.Length > 0)
                    {
                        asuRow = asuRows[0];
                    }
                }
                else
                {
                    asuRow = GetAsuInfoRow(pdbId, asymChain);
                }
                if (asuRow != null)
                {
                    //      sequence = asuRows[0]["Sequence"].ToString().TrimEnd();
                    sequenceInCoord = asuRow["SequenceInCoord"].ToString().TrimEnd();
                    alignStart = Convert.ToInt32(chainRow["AlignStart"].ToString());
                    alignEnd = Convert.ToInt32(chainRow["AlignEnd"].ToString());
                    domainSeqInCoord = sequenceInCoord.Substring(alignStart - 1, alignEnd - alignStart + 1);
                    numOfDomainCoord += GetNumOfCoordinates(domainSeqInCoord);
                }
            }
            return numOfDomainCoord;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymId"></param>
        /// <returns></returns>
        private DataRow GetAsuInfoRow (string pdbId, string asymId)
        {
            string queryString = string.Format("Select PdbId, AsymID, AuthorChain, EntityID, SequenceInCoord, PolymerStatus, PolymerType From AsymUnit " +
                        " Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymId);
            DataTable chainAsumInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (chainAsumInfoTable.Rows.Count > 0)
            {
                return chainAsumInfoTable.Rows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private int GetNumOfCoordinates(string sequence)
        {
            int numOfCoord = 0;
            foreach (char ch in sequence)
            {
                if (ch != '-' && ch != '.')
                {
                    numOfCoord++;
                }
            }
            return numOfCoord;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbpfamChainTable"></param>
        /// <returns></returns>
        public string[] GetEntryChainDomainIds(DataTable pdbpfamChainTable)
        {
            List<string> chainDomainIdList = new List<string>();
            string entryChainDomainId = "";
            foreach (DataRow chainDomainRow in pdbpfamChainTable.Rows)
            {
                entryChainDomainId = chainDomainRow["PdbID"].ToString() + chainDomainRow["ChainDomainID"].ToString();
                if (!chainDomainIdList.Contains(entryChainDomainId))
                {
                    chainDomainIdList.Add(entryChainDomainId);
                }
            }
            string[] entryChainDomainIds = new string[chainDomainIdList.Count];
            chainDomainIdList.CopyTo(entryChainDomainIds);
            return entryChainDomainIds;
        }
        #endregion

        #region uniprot and chain domains             
        /// <summary>
        /// get best structure for each uniprot sequences based on the best structure of each pdb entry
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="entryChainDomainIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamUnpBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, string[] entryChainDomainIds, string[] unpcodesWithLigands)
        {
            string[] entryEntities = GetPfamEntryEntities(pdbPfamChainTable, entryChainDomainIds);
            DataTable entryEntityUnpTable = GetEntryEntityUnpTable(entryEntities);

            Dictionary<string, string> unpBestStructChainDomainHash = new Dictionary<string, string>();
            Dictionary<string, int> unpBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            string unpCode = "";
            int numOfCoord = 0;
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                unpCode = GetUnpCode(chainDomainRows, entryEntityUnpTable);
                if (Array.IndexOf(unpcodesWithLigands, unpCode) > -1)   //  if this uniprot already has structures with ligands, no needs to select structures without ligands
                {
                    continue;
                }
                numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                if (numOfCoord == 0)  // exclude those with no coordinates
                {
                    continue;
                }
               
                if (unpBestStructChainDomainHash.ContainsKey(unpCode))
                {
                    int maxNumOfCoord = (int)unpBestStructNumCoordHash[unpCode];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        unpBestStructNumCoordHash[unpCode] = numOfCoord;
                        unpBestStructChainDomainHash[unpCode] = entryChainDomainId;
                    }
                }
                else
                {
                    unpBestStructChainDomainHash.Add(unpCode, entryChainDomainId);
                    unpBestStructNumCoordHash.Add(unpCode, numOfCoord);
                }
            }
            return unpBestStructChainDomainHash;
        }

        /// <summary>
        /// get best structure for each uniprot sequences based on the best structure of each pdb entry
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <param name="asuTable"></param>
        /// <param name="entryChainDomainIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamUnpBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, string[] entryChainDomainIds)
        {
            string[] entryEntities = GetPfamEntryEntities(pdbPfamChainTable, entryChainDomainIds);
            DataTable entryEntityUnpTable = GetEntryEntityUnpTable(entryEntities);

            Dictionary<string, string> unpBestStructChainDomainHash = new Dictionary<string, string>();
            Dictionary<string, int> unpBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            string unpCode = "";
            int numOfCoord = 0;
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                unpCode = GetUnpCode(chainDomainRows, entryEntityUnpTable);             
                numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                if (numOfCoord == 0)  // exclude those with no coordinates
                {
                    continue;
                }

                if (unpBestStructChainDomainHash.ContainsKey(unpCode))
                {
                    int maxNumOfCoord = (int)unpBestStructNumCoordHash[unpCode];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        unpBestStructNumCoordHash[unpCode] = numOfCoord;
                        unpBestStructChainDomainHash[unpCode] = entryChainDomainId;
                    }
                }
                else
                {
                    unpBestStructChainDomainHash.Add(unpCode, entryChainDomainId);
                    unpBestStructNumCoordHash.Add(unpCode, numOfCoord);
                }
            }
            return unpBestStructChainDomainHash;
        }
        /// <summary>
        /// the best structure chain id for each pfam and uniprot code
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamUnpBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, out string entryDomainBestCoordInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pdbPfamChainTable);
            string[] entryEntities = GetPfamEntryEntities(pdbPfamChainTable);
            DataTable entryEntityUnpTable = GetEntryEntityUnpTable(entryEntities);
 
            Dictionary<string, string> unpBestStructChainDomainHash = new Dictionary<string,string> ();
            Dictionary<string, int> unpBestStructNumCoordHash = new Dictionary<string,int> ();
            string pdbId = "";
            int chainDomainId = 0;
            string unpCode = "";
            int maxNumOfCoordInPfam = 0;
            int numOfCoord = 0;
            entryDomainBestCoordInPfam = entryChainDomainIds[0];
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                if (numOfCoord == 0)  // exclude those with no coordinates
                {
                    continue;
                }
                unpCode = GetUnpCode(chainDomainRows, entryEntityUnpTable);

                if (maxNumOfCoordInPfam < numOfCoord)
                {
                    maxNumOfCoordInPfam = numOfCoord;
                    entryDomainBestCoordInPfam = entryChainDomainId;
                }
                if (unpBestStructChainDomainHash.ContainsKey(unpCode))
                {
                    int maxNumOfCoord = (int)unpBestStructNumCoordHash[unpCode];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        unpBestStructNumCoordHash[unpCode] = numOfCoord;
                        unpBestStructChainDomainHash[unpCode] = entryChainDomainId;
                    }
                }
                else
                {
                    unpBestStructChainDomainHash.Add(unpCode, entryChainDomainId);
                    unpBestStructNumCoordHash.Add(unpCode, numOfCoord);
                }
            }
            return unpBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        private string[] GetPfamEntryEntities(DataTable pdbPfamChainTable)
        {
            List<string> entryEntityList = new List<string>();
            string entryEntity = "";
            foreach (DataRow domainChainRow in pdbPfamChainTable.Rows)
            {
                entryEntity = domainChainRow["PdbID"].ToString() + domainChainRow["EntityID"].ToString();
                if (!entryEntityList.Contains(entryEntity))
                {
                    entryEntityList.Add(entryEntity);
                }
            }
            string[] entryEntities = new string[entryEntityList.Count];
            entryEntityList.CopyTo(entryEntities);
            return entryEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbPfamChainTable"></param>
        /// <returns></returns>
        private string[] GetPfamEntryEntities(DataTable pdbPfamChainTable, string[] entryChainDomainIds)
        {
            List<string> entryEntityList = new List<string>();
            string entryEntity = "";
            string entryChainDomainId = "";
            foreach (DataRow domainChainRow in pdbPfamChainTable.Rows)
            {
                entryChainDomainId = domainChainRow["PdbID"].ToString() + domainChainRow["ChainDomainID"].ToString();
                if (entryChainDomainIds.Contains(entryChainDomainId))
                {
                    entryEntity = domainChainRow["PdbID"].ToString() + domainChainRow["EntityID"].ToString();
                    if (!entryEntityList.Contains(entryEntity))
                    {
                        entryEntityList.Add(entryEntity);
                    }
                }
            }
            string[] entryEntities = new string[entryEntityList.Count];
            entryEntityList.CopyTo(entryEntities);
            return entryEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryEntities"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityUnpTable(string[] entryEntities)
        {
            DataTable entityUnpTable = null;
            string pdbId = "";
            int entityId = 0;
            foreach (string entryEntity in entryEntities)
            {
                pdbId = entryEntity.Substring(0, 4);
                entityId = Convert.ToInt32(entryEntity.Substring (4, entryEntity.Length - 4));
                DataTable unpCodeTable = GetUnpCodeInfoTable(pdbId, entityId);
                if (entityUnpTable == null)
                {
                    entityUnpTable = unpCodeTable.Copy();
                }
                else
                {
                    ParseHelper.AddNewTableToExistTable(unpCodeTable, ref entityUnpTable);
                }
            }
            return entityUnpTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private DataTable GetUnpCodeInfoTable (string pdbId, int entityId)
        {
            string queryString = string.Format("SELECT PdbDbRefSeqSifts.PdbId,  AlignId, PdbDbRefSeqSifts.RefId,   " + 
                " EntityId, DbCode As UnpID, DbAlignBeg, DbAlignEnd, AsymID, SeqAlignBeg, SeqAlignEnd " +
                " FROM PdbDbRefSifts, PdbDbRefSeqSifts WHERE PdbDbRefSifts.PdbID = '{0}'  AND EntityID = {1} AND " +
                " PdbDbRefSifts.PdbId = PdbDbRefSeqSifts.PdbId AND PdbDbRefSifts.RefId = PdbDbRefSeqSifts.RefId " + 
                " AND DbName = 'UNP';", pdbId, entityId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return unpCodeTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomainRows"></param>
        /// <param name="unpCodeInfoTable"></param>
        /// <returns></returns>
        private string GetUnpCode(DataRow[] chainDomainRows, DataTable unpCodeInfoTable)
        {
            string pdbId = "";
            string asymChain = "";
            string unpCode = "";
            Range domainRange = new Range ();
            foreach (DataRow chainDomainRow in chainDomainRows)
            {
                pdbId = chainDomainRow["PdbID"].ToString();
                asymChain = chainDomainRow["AsymChain"].ToString().TrimEnd();
                domainRange.startPos = Convert.ToInt32 (chainDomainRow["SeqStart"].ToString ());
                domainRange.endPos = Convert.ToInt32 (chainDomainRow["SeqEnd"].ToString ());
                unpCode = GetUnpCode(pdbId, asymChain, domainRange, unpCodeInfoTable);
                if (unpCode != "")
                {
                   break;
                }
            }
            if (unpCode == "")
            {
                unpCode = pdbId;
            }
            return unpCode;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="domainRanges"></param>
        /// <param name="unpCodeInfoTable"></param>
        /// <returns></returns>
        private string GetUnpCode(string pdbId, string asymChain, Range domainRange, DataTable unpCodeInfoTable)
        {
            DataRow[] chainDbRefRows = unpCodeInfoTable.Select(string.Format ("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            string unpCode = "";
            foreach (DataRow chainDbRefRow in chainDbRefRows)
            {
                Range seqDbRange = new Range();
                seqDbRange.startPos = Convert.ToInt32(chainDbRefRow["SeqAlignBeg"].ToString());
                seqDbRange.endPos = Convert.ToInt32(chainDbRefRow["SeqAlignEnd"].ToString());
                if (IsDbRangeOverlapDomainRange(seqDbRange, domainRange))
                {
                    unpCode = chainDbRefRow["UnpID"].ToString ();
                    break;
                }
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbRange"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private bool IsDbRangeOverlapDomainRange(Range seqDbRange, Range domainRange)
        {
            if ((seqDbRange.startPos >= domainRange.startPos && seqDbRange.startPos <= domainRange.endPos) ||
                (seqDbRange.endPos >= domainRange.startPos && seqDbRange.endPos <= domainRange.endPos) ||
                (domainRange.startPos >= seqDbRange.startPos && domainRange.startPos <= seqDbRange.endPos) ||
                domainRange.endPos >= seqDbRange.startPos && domainRange.endPos <= seqDbRange.endPos)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region best structures in crystal forms
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <param name="?"></param>
        /// <param name="?"></param>
        /// <param name="crystDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamCrystBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, string[] entryChainDomainIds)
        {
            string[] pfamEntries = GetPfamEntries(pdbPfamChainTable);
            Dictionary <string, string> entryCrystFormHash = GetEntryCrystFormHash(pfamEntries);

            Dictionary<string, string> crystBestStructChainDomainHash = new Dictionary<string,string> ();
            Dictionary<string, int> crystBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            string crystForm = "";
            int numOfCoord = 0;
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                if (numOfCoord == 0)  // exclude those with no coordinates
                {
                    continue;
                }
                crystForm = "";
                if (entryCrystFormHash.ContainsKey (pdbId))
                {
                    crystForm = (string)entryCrystFormHash[pdbId];
                }
                
                if (crystForm == "")
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + " no crystal form");
                    crystForm = pdbId;
                }
                if (crystBestStructChainDomainHash.ContainsKey(crystForm))
                {
                    int maxNumOfCoord = (int)crystBestStructNumCoordHash[crystForm];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        crystBestStructNumCoordHash[crystForm] = numOfCoord;
                        crystBestStructChainDomainHash[crystForm] = entryChainDomainId;
                    }
                }
                else
                {
                    crystBestStructChainDomainHash.Add(crystForm, entryChainDomainId);
                    crystBestStructNumCoordHash.Add(crystForm, numOfCoord);
                }
            }
            return crystBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <param name="?"></param>
        /// <param name="?"></param>
        /// <param name="crystDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetPfamCrystBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable pfamLigandsTable, DataTable asuTable, string[] entryChainDomainIds)
        {
            string[] pfamEntries = GetPfamEntries(pdbPfamChainTable);
            Dictionary<string, string> entryCrystFormHash = GetEntryCrystFormHash(pfamEntries);

            Dictionary<string, string[]> crystBestStructChainDomainDict = new Dictionary<string, string[]>();
   //         Dictionary<string, int> crystBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            string crystForm = "";
            int numOfCoord = 0;
            string ligand = "";
            Dictionary<string, Dictionary<string, List<string>>> cfLigandsDomainsDict = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] ligandRows = pfamLigandsTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                crystForm = "";
                if (entryCrystFormHash.ContainsKey (pdbId))
                {
                    crystForm = entryCrystFormHash[pdbId];
                }
                if (crystForm == "")
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + " not in pfam, so no crystal form");
                    ProtCidSettings.logWriter.Flush();
                    crystForm = pdbId;
                }
                foreach (DataRow ligandRow in ligandRows)
                {
                    ligand = ligandRow["Ligand"].ToString().TrimEnd();
                    if (cfLigandsDomainsDict.ContainsKey(crystForm))
                    {
                        if (cfLigandsDomainsDict[crystForm].ContainsKey (ligand))
                        {
                            cfLigandsDomainsDict[crystForm][ligand].Add(entryChainDomainId);
                        }
                        else
                        {
                            List<string> domainList = new List<string>();
                            domainList.Add(entryChainDomainId);
                            cfLigandsDomainsDict[crystForm].Add(ligand, domainList);
                        }
                    }
                    else
                    {
                        Dictionary<string, List<string>> ligandDomainListDict = new Dictionary<string, List<string>>();
                        List<string> domainList = new List<string>();
                        domainList.Add(entryChainDomainId);
                        ligandDomainListDict.Add(ligand, domainList);
                        cfLigandsDomainsDict.Add(crystForm, ligandDomainListDict);
                    }
                }
            }
            int maxNumCoord = 0;
            string ligandBestDomain = "";
            foreach (string lsCf in cfLigandsDomainsDict.Keys)
            {
                List<string> cfBestDomainList = new List<string>();
                foreach (string lsLigand in cfLigandsDomainsDict[lsCf].Keys)
                {
                    maxNumCoord = 0;
                    numOfCoord = 0;
                    ligandBestDomain = "";
                    foreach (string lsDomain in cfLigandsDomainsDict[lsCf][lsLigand])
                    {
                        pdbId = lsDomain.Substring(0, 4);
                        chainDomainId = Convert.ToInt32(lsDomain.Substring(4, lsDomain.Length - 4));
                        DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                        numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                        if (numOfCoord <= 0)  // exclude those with no coordinates
                        {
                            continue;
                        }

                        if (maxNumCoord < numOfCoord)
                        {
                            maxNumCoord = numOfCoord;
                            ligandBestDomain = lsDomain;
                        }
                    }
                    if (!cfBestDomainList.Contains(ligandBestDomain))
                    {
                        cfBestDomainList.Add(ligandBestDomain);
                    }
                }
                crystBestStructChainDomainDict.Add(lsCf, cfBestDomainList.ToArray());
            }
            return crystBestStructChainDomainDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <param name="?"></param>
        /// <param name="?"></param>
        /// <param name="crystDomainBestCoordInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamCrystBestStructChainDomainIdHash(DataTable pdbPfamChainTable, DataTable asuTable, out string bestStructDomainInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(pdbPfamChainTable);
            string[] pfamEntries = GetPfamEntries(pdbPfamChainTable);
            Dictionary<string, string> entryCrystFormHash = GetEntryCrystFormHash(pfamEntries);

            Dictionary<string, string> crystBestStructChainDomainHash = new Dictionary<string, string>();
            Dictionary<string, int> crystBestStructNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            string crystForm = "";
            int maxNumOfCoordInPfam = 0;
            int numOfCoord = 0;
            bestStructDomainInPfam = entryChainDomainIds[0];
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                DataRow[] chainDomainRows = pdbPfamChainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                numOfCoord = GetNumOfCoordinates(chainDomainRows, asuTable);
                if (numOfCoord == 0)  // exclude those with no coordinates
                {
                    continue;
                }
                crystForm = "";
                if (entryCrystFormHash.ContainsKey(pdbId))
                {
                    crystForm = entryCrystFormHash[pdbId];
                }
                if (crystForm == "")
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + " not in pfam, no crystal form");
                    crystForm = pdbId;
                }

                if (maxNumOfCoordInPfam < numOfCoord)
                {
                    maxNumOfCoordInPfam = numOfCoord;
                    bestStructDomainInPfam = entryChainDomainId;
                }
                if (crystBestStructChainDomainHash.ContainsKey(crystForm))
                {
                    int maxNumOfCoord = (int) crystBestStructNumCoordHash[crystForm];
                    if (maxNumOfCoord < numOfCoord)
                    {
                        crystBestStructNumCoordHash[crystForm] = numOfCoord;
                        crystBestStructChainDomainHash[crystForm] = entryChainDomainId;
                    }
                }
                else
                {
                    crystBestStructChainDomainHash.Add(crystForm, entryChainDomainId);
                    crystBestStructNumCoordHash.Add(crystForm, numOfCoord);
                }
            }
            return crystBestStructChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryCrystFormHash(string[] entries)
        {
            Dictionary<string, string> entryCrystFormDict = new Dictionary<string,string> ();
            string crystForm = "";
            foreach (string pdbId in entries)
            {
                crystForm = GetEntryCrystForm(pdbId);
                entryCrystFormDict.Add(pdbId, crystForm);
            }
            return entryCrystFormDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        private string[] GetPfamEntries(DataTable pfamDomainTable)
        {
            List<string> pfamEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow pfamDomainRow in pfamDomainTable.Rows)
            {
                pdbId = pfamDomainRow["PdbID"].ToString();
                if (!pfamEntryList.Contains(pdbId))
                {
                    pfamEntryList.Add(pdbId);
                }
            }
            string[] pfamEntries = new string[pfamEntryList.Count];
            pfamEntryList.CopyTo(pfamEntries);
            return pfamEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryCrystForm(string pdbId)
        {
            string queryString = string.Format("Select GroupSeqId, CfGroupId From PfamNonRedundantCfGroups Where PdbID = '{0}';", pdbId);
            DataTable crystFormTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (crystFormTable.Rows.Count == 0)
            {
                queryString = string.Format("Select PdbID1 From PfamHomoRepEntryAlign Where PdbID2 = '{0}';", pdbId);
                DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (repEntryTable.Rows.Count > 0)
                {
                    string repEntry = repEntryTable.Rows[0]["PdbID1"].ToString().TrimEnd();
                    queryString = string.Format("Select GroupSeqId, CfGroupId From PfamNonRedundantCfGroups Where PdbID = '{0}';", repEntry);
                    crystFormTable = ProtCidSettings.protcidQuery.Query( queryString);
                }
            }
            string crystForm = "";
            if (crystFormTable.Rows.Count > 0)
            {
                crystForm = crystFormTable.Rows[0]["GroupSeqID"].ToString() + "_" + crystFormTable.Rows[0]["CfGroupID"].ToString();
            }
            return crystForm;
        }
        #endregion

        #region write a domain file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="domainDefRows"></param>
        public string WriteDomainChainWithLigandFile(DataRow[] chainDomainDefRows, string domainPymolFileDir, ref Dictionary<string, int[]> domainCoordSeqIdsHash, ref Dictionary<string, string[]> domainFileChainMapHash)
        {
            string pdbId = chainDomainDefRows[0]["PdbID"].ToString();
            int chainDomainId = Convert.ToInt32(chainDomainDefRows[0]["ChainDomainID"].ToString ());
            string chainDomain = pdbId + chainDomainDefRows[0]["ChainDomainID"].ToString();
            string chainDomainFile = chainDomain + ".pfam";
            string fullDomainFile = Path.Combine(domainPymolFileDir, chainDomainFile);
            string[] chainMap = null;
            if (File.Exists(fullDomainFile))
            {
                int[] seqInCoord = ReadCoordSeqIdsFromDomainFile(fullDomainFile, out chainMap);
                domainCoordSeqIdsHash.Add(chainDomain, seqInCoord);
                domainFileChainMapHash.Add(chainDomain, chainMap); // map the asymmetric chain to file chain in the domain file with ligands
                if (domainAlignPymolScript.IsDomainMultiChain(chainDomainDefRows))
                {
                   domainAlignPymolScript.UpdateMultiChainDomainDefRowsByFileSeqIds(chainDomainDefRows);
                }
                return chainDomain;
            }
            
            // for multi-chain domain, use the domain file generated
            if (domainAlignPymolScript.IsDomainMultiChain(chainDomainDefRows))
            {
                string multiChainDomainFile = Path.Combine(pfamDomainFileDir, pdbId.Substring(1, 2) + "\\" + pdbId + chainDomainDefRows[0]["ChainDomainID"].ToString() + ".pfam.gz");
                string ungzDomainFile = ParseHelper.UnZipFile(multiChainDomainFile, domainPymolFileDir);
                int[] coordSeqIdsInFile =  ReadCoordSeqIdsFromDomainFile(ungzDomainFile);
                domainCoordSeqIdsHash.Add(chainDomain, coordSeqIdsInFile);

                AddLigandsToDomainFile(ungzDomainFile, out chainMap);  // add possible ligands to file

                domainFileChainMapHash.Add(chainDomain, chainMap);

                domainAlignPymolScript.UpdateMultiChainDomainDefRowsByFileSeqIds(chainDomainDefRows);
            }
            else
            {
                string asymChain = chainDomainDefRows[0]["AsymChain"].ToString().TrimEnd();
                // add ligands
                string[] ligandChains = GetInteractingLigands(pdbId, asymChain);
                string[] asymChains = new string[ligandChains.Length + 1];
                asymChains[0] = asymChain;
                Array.Copy(ligandChains, 0, asymChains, 1, ligandChains.Length);

                int[] seqIdsInCoord = null;
                string[] fileChains = null;
                string domainFile = Path.Combine(domainPymolFileDir, chainDomainFile);
                string gzXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
                string xmlFile = ParseHelper.UnZipFile(gzXmlFile, ProtCidSettings.tempDir);  
                WriteChainDomainLigandFile (pdbId, asymChains, xmlFile, out fileChains, out seqIdsInCoord, domainFile);

                domainCoordSeqIdsHash.Add(chainDomain, seqIdsInCoord);
                chainMap = new string[2];
                chainMap[0] = FormatChainString(asymChains);
                chainMap[1] = FormatChainString (fileChains);
                domainFileChainMapHash.Add(chainDomain, chainMap);

                File.Delete(xmlFile);
            }
            return chainDomain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string[] GetInteractingLigands(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Distinct AsymID from ChainLigands " + 
                " Where PdbID = '{0}' AND ChainAsymID = '{1}';", pdbId, asymChain);
            DataTable interactingLigandsTable = ProtCidSettings.buCompQuery.Query(queryString);
            queryString = string.Format("Select Distinct AsymID From ChainDnaRnas " +
                " Where PdbID = '{0}' AND ChainAsymID = '{1}';", pdbId, asymChain);
            DataTable dnarnaLigandTable = ProtCidSettings.buCompQuery.Query(queryString);
            ParseHelper.AddNewTableToExistTable(dnarnaLigandTable, ref interactingLigandsTable);
            string[] interactingLigands = GetInteractingLigands(interactingLigandsTable);
            return interactingLigands;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        private string[] GetInteractingLigands(string pdbId, string[] asymChains)
        {
            string queryString = string.Format("Select Distinct AsymID from ChainLigands " +
              " Where PdbID = '{0}' AND ChainAsymID IN ({1}) Order By AsymID;", pdbId, ParseHelper.FormatSqlListString (asymChains));
            DataTable interactLigandsTable = ProtCidSettings.buCompQuery.Query(queryString);
            queryString = string.Format("Select Distinct AsymID from ChainDnaRnas " +
              " Where PdbID = '{0}' AND ChainAsymID IN ({1}) Order By AsymID;", pdbId, ParseHelper.FormatSqlListString(asymChains));
            DataTable dnarnaLigandTable = ProtCidSettings.buCompQuery.Query(queryString);
            ParseHelper.AddNewTableToExistTable(dnarnaLigandTable, ref interactLigandsTable);
            string[] interactingLigands = GetInteractingLigands(interactLigandsTable);
            return interactingLigands;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interactLigandTable"></param>
        /// <returns></returns>
        private string[] GetInteractingLigands(DataTable interactLigandTable)
        {
            List<string> ligandChainList = new List<string>();
            string ligandChain = "";
            foreach (DataRow ligandRow in interactLigandTable.Rows)
            {
                ligandChain = ligandRow["AsymID"].ToString().TrimEnd();
                if (AreChainOriginal(ligandChain))
                {
                    if (!ligandChainList.Contains(ligandChain))
                    {
                        ligandChainList.Add(ligandChain);
                    }
                }
            }
            string[] ligandChains = new string[ligandChainList.Count];
            ligandChainList.CopyTo(ligandChains);
            string[] sortedLigandChains = SortAsymChains(ligandChains);
            return sortedLigandChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        private string[] SortAsymChains(string[] asymChains)
        {
            List<string> oneLetterChainList = new List<string>();
            List<string> twoLetterChainList = new List<string>();
            List<string> threeLetterChainList = new List<string>();
            foreach (string asymChain in asymChains)
            {
                if (asymChain.Length == 1)
                {
                    oneLetterChainList.Add(asymChain);
                }
                else if (asymChain.Length == 2)
                {
                    twoLetterChainList.Add(asymChain);
                }
                else if (asymChain.Length == 3)
                {
                    threeLetterChainList.Add(asymChain);
                }
            }
            oneLetterChainList.Sort();
            twoLetterChainList.Sort();
            threeLetterChainList.Sort();
            List<string> sortedChainList = new List<string>();
            sortedChainList.AddRange(oneLetterChainList);
            sortedChainList.AddRange(twoLetterChainList);
            sortedChainList.AddRange(threeLetterChainList);
            string[] sortedChains = new string[sortedChainList.Count];
            sortedChainList.CopyTo(sortedChains);
            return sortedChains;
        }
        /// <summary>
        ///  I only want the original chains in the asymmetric unit, 
        ///  although the real asymmetric unit needs to be built from the NCS operators.
        ///  These chain id with digital numbers are built from NCS operators. 
        ///  But I decided to only use the chains in the PDB ent file
        /// </summary>
        /// <param name="chainId"></param>
        /// <returns></returns>
        private bool AreChainOriginal(string chainId)
        {
            foreach (char ch in chainId)
            {
                if (char.IsDigit(ch))
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// read coord seq ids of a protein chain from a domain file
        /// there is only one protein chain with chain A
        /// multi-chain domain is also transformed into one chain file
        /// </summary>
        /// <param name="domainFile"></param>
        /// <returns></returns>
        public int[] ReadCoordSeqIdsFromDomainFile(string domainFile, out string[] chainMap)
        {
            StreamReader dataReader = new StreamReader(domainFile);
            List<int> coordSeqIdList = new List<int>();
            string line = "";
            int seqId = 0;
            string atomName = "";
            string asymChains = "";
            string fileChains = "";
            chainMap = new string[2];
            bool isBiolAssembly = false;
            string symOpChains = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Chains and Symmetry Operators") > -1)
                {
                    isBiolAssembly = true;
                    line = dataReader.ReadLine();
                    symOpChains = line.Remove(0, "REMARK   2 ".Length).Trim ();
                }
                if (line.IndexOf("AsymChains") > -1 && line.IndexOf("FileChains") > -1)
                {
                    line = dataReader.ReadLine();
                    asymChains = line.Replace("REMARK   2", "").Trim ();
                    line = dataReader.ReadLine();
                    fileChains = line.Replace("REMARK   2", "").Trim ();
                    if (isBiolAssembly)
                    {
                        chainMap[0] = symOpChains;
                    } 
                    else
                    {
                        chainMap[0] = asymChains;
                    }
                    chainMap[1] = fileChains;
                }
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);
                    seqId = Convert.ToInt32(items[6]);
                    atomName = items[2];
                    if (items[5] == "A")
                    {
                        if (atomName == "CA")
                        {
                            if (!coordSeqIdList.Contains(seqId))
                            {
                                coordSeqIdList.Add(seqId);
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            return coordSeqIdList.ToArray ();
        }

        /// <summary>
        /// read coord seq ids of a protein chain from a domain file
        /// there is only one protein chain with chain A
        /// multi-chain domain is also transformed into one chain file
        /// </summary>
        /// <param name="domainFile"></param>
        /// <returns></returns>
        public int[] ReadCoordSeqIdsFromDomainFile(string domainFile)
        {
            StreamReader dataReader = new StreamReader(domainFile);
            List<int> coordSeqIdList = new List<int>();
            string line = "";
            int seqId = 0;
            string atomName = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);
                    seqId = Convert.ToInt32(items[6]);
                    atomName = items[2];
                    if (items[5] == "A")
                    {
                        if (atomName == "CA")
                        {
                            if (!coordSeqIdList.Contains(seqId))
                            {
                                coordSeqIdList.Add(seqId);
                            }
                        }
                    }
                }
            }
            dataReader.Close();

            return coordSeqIdList.ToArray ();
        }

        // for some entries, there are many ligands, the file chain id repeats.
        /// <summary>
        /// atoms with ligands
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="pdbFile"></param>
        /// <returns></returns>
        public string GetChainInfo(string[] asymChains, string pdbFile, out string asymChainsInNewFile, out string fileChainsInNewFile, out int[] seqIdsInCoord)
        {
            StreamReader dataReader = new StreamReader(pdbFile);
            string pdbChainInfo = "";
            string line = "";
            bool atomSelected = false;
            string[] newFileChains = null;
            int[] selectFileChainIndexes = null;
            int selectFileChainIndex = 0;
            int fileChainIndex = -1;
            string chainId = "";
            int seqId = 0;
            int atomId = 1;
            string preChain = "";
            string currentChain = "";
            string preAtomChain = "";
            string currentAtomChain = "";
            string[] asymChainsInFile = null;

            List<int> seqIdInCoordList = new List<int>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("AsymChains") > -1)
                {
                    line = dataReader.ReadLine();  // asymChains
                    string asymChainString = line.Substring("REMARK   2".Length, line.Length - "REMARK   2".Length).Trim();
                    line = dataReader.ReadLine();   // filechains
                    string fileChainString = line.Substring("REMARK   2".Length, line.Length - "REMARK   2".Length).Trim();
                    asymChainsInFile = asymChainString.Split(',');
                    string[] fileChainsInFile = fileChainString.Split(',');
                    // the sequential numbers for the selected asymmetric chains
                    selectFileChainIndexes = GetFileChainIndexes (fileChainsInFile, asymChainsInFile, asymChains);
                    newFileChains = GetNewFileChains(selectFileChainIndexes.Length);
                    continue;
                }
                if (line.IndexOf("SEQRES") > -1)
                {
                    string[] fields = ParseHelper.SplitPlus(line, ' ');
                    currentChain = fields[2];
                    if (preChain == "")
                    {
                        selectFileChainIndex = 0;
                        if (selectFileChainIndexes.Contains(selectFileChainIndex))
                        {
                            fileChainIndex = 0;
                        }
                        else
                        {
                            fileChainIndex = -1;
                        }
                    }
                    else if (preChain != currentChain)
                    {
                        selectFileChainIndex ++;
                        if (selectFileChainIndexes.Contains(selectFileChainIndex))
                        {
                            fileChainIndex++;
                        }
                    }
               //     fileChainIndex = Array.IndexOf(selectFileChains, fields[2]);

                    if (selectFileChainIndexes.Contains(selectFileChainIndex)) // chain id
                    {
                        chainId = newFileChains[fileChainIndex];
                        line = ChangeSeqresLineChainID(line, chainId);
                        pdbChainInfo += (line + "\r\n");
                    }
                    preChain = currentChain;
                }
                if (line.IndexOf("ATOM  ") > -1 || line.IndexOf("HETATM") > -1)
                {
                    string[] fields = ParseHelper.ParsePdbAtomLine(line);
                    currentAtomChain = fields[5];
                    if (preAtomChain == "")
                    {
                        selectFileChainIndex = 0;
                        if (selectFileChainIndexes.Contains(selectFileChainIndex))
                        {
                            fileChainIndex = 0;
                        }
                        else
                        {
                            fileChainIndex = -1;
                        }
                    }
                    else if (preAtomChain != currentAtomChain)
                    {
                        selectFileChainIndex++;
                        if (selectFileChainIndexes.Contains(selectFileChainIndex))
                        {
                            fileChainIndex++;
                        }
                    }
                //    fileChainIndex = Array.IndexOf(selectFileChains, fields[5]);
                    if (selectFileChainIndexes.Contains (selectFileChainIndex))
                    {
                        chainId = newFileChains[fileChainIndex];
                        if (chainId == "A")
                        {
                            if (fields[2] == "CA")
                            {
                                seqId = Convert.ToInt32(fields[6]);
                                if (seqIdInCoordList.Contains(seqId))
                                {
                                    continue;
                                }
                                seqIdInCoordList.Add(seqId);  // seqid
                            }
                        }
                        // remove the original chain Id
                        line = ChangeAtomLineChainID(line, chainId); // use new chain id
                        line = ChangeAtomLineSeqID(line, atomId);
                        pdbChainInfo += (line + "\r\n");
                        atomSelected = true;
                        atomId++;
                    }
                    preAtomChain = currentAtomChain;
                }

                if (line.IndexOf("TER") > -1)
                {
                    if (atomSelected)
                    {
                        line = ChangeAtomLineChainID(line, chainId);
                        line = ChangeAtomLineSeqID(line, atomId);
                        pdbChainInfo += (line + "\r\n");
                        atomSelected = false;
                        atomId++;
                    }
                }
            }
            dataReader.Close();

            asymChainsInNewFile = FormatChainString(asymChainsInFile, selectFileChainIndexes);
            fileChainsInNewFile = FormatChainString(newFileChains);

            seqIdsInCoord = new int[seqIdInCoordList.Count];
            seqIdInCoordList.CopyTo(seqIdsInCoord);

            pdbChainInfo = pdbChainInfo.TrimEnd("\r\n".ToCharArray());
            return pdbChainInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <param name="xmlFile"></param>
        /// <param name="fileChains"></param>
        /// <param name="seqIdsInCoord"></param>
        /// <param name="domainFile"></param>
        public void WriteChainDomainLigandFile (string pdbId, string[] asymChains, string xmlFile, out string[] fileChains, out int[] seqIdsInCoord, string domainFile)
        {
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();
            
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();
            Dictionary<string, string> chainMatchHash = new Dictionary<string,string> ();
            List<string> fileChainsInOrderList = new List<string>();
            string fileChain = "";
            int chainIndex = 0;
            seqIdsInCoord = new int[0];

            foreach (string asymChain in asymChains)
            {
                foreach (ChainAtoms chain in chains)
                {
                    if (asymChain == chain.AsymChain)
                    {
                        if (chainIndex == ParseHelper.chainLetters.Length)
                        {
                            chainIndex = 0;
                        }
                        fileChain = ParseHelper.chainLetters[chainIndex].ToString();
                        asuChainHash.Add(chain.AsymChain, chain.CartnAtoms);
                        chainMatchHash.Add(chain.AsymChain, fileChain);
                        fileChainsInOrderList.Add(fileChain);
                        chainIndex++;

                        if (chain.AsymChain == asymChains[0])
                        {
                            seqIdsInCoord = GetResiduesInCoordinates(chain);
                        }
                    }
                }
            }

            fileChains = new string[fileChainsInOrderList.Count];
            fileChainsInOrderList.CopyTo(fileChains);
            string[] asymChainsInFile = asymChains;

            string resrecordLines = GetSeqResRecords(thisEntryCrystal, asymChains, fileChains);
            string[] hetatmChains = GetNonpolymerAsymChains(thisEntryCrystal.entityCat.EntityInfoList, asymChains);

            string remark = "HEADER    " + pdbId + " " + DateTime.Today.ToShortDateString();
            remark = remark + "REMARK   2 AsymChains    FileChains \r\n";
            remark = remark + "REMARK   2 " + FormatChainString (asymChains) + "\r\n";
            remark = remark + "REMARK   2 " + FormatChainString (fileChains) + "\r\n";
            remark = remark + resrecordLines + "\r\n";
            buWriter.WriteAsymUnitFile(domainFile, asuChainHash, asymChainsInFile, fileChains, hetatmChains, remark);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityInfos"></param>
        /// <returns></returns>
        public string[] GetNonpolymerAsymChains(EntityInfo[] entityInfos, string[] asymChains)
        {
            List<string> nonpolymerAsymChainList = new List<string>();
            foreach (EntityInfo entityInfo in entityInfos)
            {
                if (entityInfo.type != "polydeoxyribonucleotide" && entityInfo.type != "polypeptide" && entityInfo.type != "polyribonucleotide")
                {
                    string asymChainField = entityInfo.asymChains;
                    string[] entityAsymChains = asymChainField.Split(',');
                    foreach (string entityAsymChain in entityAsymChains )
                    {
                        if (asymChains.Contains(entityAsymChain))
                        {
                            nonpolymerAsymChainList.Add(entityAsymChain);
                        }
                    }
                }
            }
            string[] nonpolymerAsymChains = new string[nonpolymerAsymChainList.Count];
            nonpolymerAsymChainList.CopyTo(nonpolymerAsymChains);
            return nonpolymerAsymChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        private int[] GetResiduesInCoordinates(ChainAtoms chain)
        {
            List<int> seqIdsInCoordList = new List<int>();
            foreach (AtomInfo atom in chain.CalphaAtoms ())
            {
                seqIdsInCoordList.Add(Convert.ToInt32 (atom.seqId));
            }
            return seqIdsInCoordList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        public string GetSeqResRecords(EntryCrystal thisEntryCrystal, string[] asymChains, string[] fileChains)
        {
            Dictionary<string, string> chainResidueHash = new Dictionary<string,string> ();
            foreach (EntityInfo entityInfo in thisEntryCrystal.entityCat.EntityInfoList)
            {
                string[] entityAsymChains = entityInfo.asymChains.Split(',');
                foreach (string entityAsymChain in entityAsymChains)
                {
                    if (asymChains.Contains(entityAsymChain))
                    {
                        chainResidueHash.Add(entityAsymChain, entityInfo.threeLetterSeq);
                    }
                }
            }

            string seqresRecords = "";

            int chainCount = 0;
            foreach (string asymChain in asymChains)
            {
                if (chainResidueHash.ContainsKey(asymChain))
                {
                    string[] residues = chainResidueHash[asymChain].Split(' ');
                    string seqresLine = ParseHelper.FormatChainSeqResRecords(fileChains[chainCount], residues);
                    seqresRecords += (seqresLine + "\r\n");
                }
                chainCount++;
            }
            return seqresRecords;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChainsInFile"></param>
        /// <param name="selectChainIndexes"></param>
        /// <returns></returns>
        private string FormatChainString(string[] asymChainsInFile, int[] selectChainIndexes)
        {
            string asymChainsInNewFile = "";
            foreach (int chainIndex in selectChainIndexes)
            {
                asymChainsInNewFile += (asymChainsInFile[chainIndex] + ",");
            }
            asymChainsInNewFile = asymChainsInNewFile.TrimEnd(',');
            return asymChainsInNewFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainIndexes"></param>
        private void SortChainIndexes(int[] chainIndexes)
        {
            for (int i = 0; i < chainIndexes.Length; i++)
            {
                for (int j = i + 1; j < chainIndexes.Length; j++)
                {
                    if (chainIndexes[i] > chainIndexes[j])
                    {
                        int temp = chainIndexes[i];
                        chainIndexes[i] = chainIndexes[j];
                        chainIndexes[j] = temp;
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileChainsInFile"></param>
        /// <param name="asymChainsInFile"></param>
        /// <param name="selectAsymChains"></param>
        /// <returns></returns>
        private string[] GetFileChains(string[] fileChainsInFile, string[] asymChainsInFile, string[] selectAsymChains)
        {
            string[] selectFileChains = new string[selectAsymChains.Length];
            int asymChainIndex = 0;
            for (int i = 0; i < selectAsymChains.Length; i++)
            {
                asymChainIndex = Array.IndexOf(asymChainsInFile, selectAsymChains[i]);
                selectFileChains[i] = fileChainsInFile[asymChainIndex];
            }
            return selectFileChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileChainsInFile"></param>
        /// <param name="asymChainsInFile"></param>
        /// <param name="selectedAsymChains"></param>
        /// <returns></returns>
        private int[] GetFileChainIndexes(string[] fileChainsInFile, string[] asymChainsInFile, string[] selectedAsymChains)
        {
            int[] selectFileChainIndexes = new int[selectedAsymChains.Length];
            int asymChainIndex = 0;
            for (int i = 0; i < selectedAsymChains.Length; i++)
            {
                asymChainIndex = Array.IndexOf(asymChainsInFile, selectedAsymChains[i]);
                selectFileChainIndexes[i] = asymChainIndex;
            }
            SortChainIndexes(selectFileChainIndexes);  // sort in the order 
            return selectFileChainIndexes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileChainsInFile"></param>
        /// <returns></returns>
        private string[] GetNewFileChains(string[] fileChainsInFile)
        {
            string[] newFileChains = new string[fileChainsInFile.Length];
            for (int i = 0; i < fileChainsInFile.Length; i++)
            {
                newFileChains[i] = GetChainId(i);
            }
            return newFileChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numOfChains"></param>
        /// <returns></returns>
        private string[] GetNewFileChains(int numOfChains)
        {
            string[] newFileChains = new string[numOfChains];
            for (int i = 0; i < numOfChains; i++)
            {
                newFileChains[i] = GetChainId(i);
            }
            return newFileChains;
        }
        /// <summary>
        /// /
        /// </summary>
        /// <param name="chainList"></param>
        /// <returns></returns>
        public string FormatChainString(string[] chainList)
        {
            string chainString = "";
            foreach (string chain in chainList)
            {
                chainString += (chain + ",");
            }
            return chainString.TrimEnd(',');
        }
        /// <summary>
        /// /
        /// </summary>
        /// <param name="chainCount"></param>
        /// <returns></returns>
        private string GetChainId(int chainCount)
        {
            int chainIndex = chainCount % ParseHelper.chainLetters.Length;
            string chainId = ParseHelper.chainLetters[chainIndex].ToString();
            return chainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="newChainId"></param>
        /// <returns></returns>
        private string ChangeAtomLineChainID(string atomLine, string newChainId)
        {
            atomLine = atomLine.Remove(21, 1);  // remove the original chain id
            atomLine = atomLine.Insert(21, newChainId);  // use the new one
            return atomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="newAtomId"></param>
        /// <returns></returns>
        private string ChangeAtomLineSeqID(string atomLine, int newAtomId)
        {
            string newAtomLine = atomLine.Remove(6, 5);
            if (newAtomId > 99999)
            {
                newAtomId = 1;
            }
            newAtomLine = newAtomLine.Insert(6, newAtomId.ToString().PadLeft(5));
            return newAtomLine;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqresLine"></param>
        /// <param name="newChainId"></param>
        /// <returns></returns>
        private string ChangeSeqresLineChainID(string seqresLine, string newChainId)
        {
            seqresLine = seqresLine.Remove(11, 1);
            seqresLine = seqresLine.Insert(11, newChainId);
            return seqresLine;
        }
        #endregion

        #region add ligand coordinates to domain file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="multiChainDomainFile"></param>
        public void AddLigandsToDomainFile(string domainFile, out string[] chainMap)
        {
            FileInfo fileInfo = new FileInfo(domainFile);
            string pdbId = fileInfo.Name.Substring(0, 4);
            int chainDomainId = Convert.ToInt32 (fileInfo.Name.Substring(4, fileInfo.Name.IndexOf(".pfam") - 4));
            string gzPdbFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath.Replace ("XML", "regular"), pdbId.Substring (1,2 ) + "\\" + pdbId + ".ent.gz");
            string pdbFile = ParseHelper.UnZipFile(gzPdbFile, ProtCidSettings.tempDir);

            string newDomainFile = domainFile.Replace(".pfam", "_new.pfam");
            string[] domainAsymChains = GetDomainAsymChains(pdbId, chainDomainId);
            string[] interactingLigands = GetInteractingLigands(pdbId, domainAsymChains);

     /*       if (interactingLigands.Length == 0)
            {
                return;
            }
            */
            AddLigandsToDomainFile(domainFile, newDomainFile, pdbFile, interactingLigands, out chainMap);

            File.Delete(domainFile);
            File.Move(newDomainFile, domainFile);

            File.Delete(pdbFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFile"></param>
        /// <param name="newDomainFile"></param>
        /// <param name="pdbFile"></param>
        /// <param name="ligandChains"></param>
        private void AddLigandsToDomainFile(string domainFile, string newDomainFile, string pdbFile, string[] ligandChains, out string[] chainMap)
        {
            Dictionary<string, List<string>>[] ligandAtomInfoHashes = ReadLigandAtomSeqResLines (pdbFile, ligandChains);
            int lastAtomId = 0;
            string[] domainInfo = ReadDomainFile(domainFile, out lastAtomId);
            string[] asymChains = new string[ligandChains.Length + 1];
            asymChains[0] = "A";  // for the domain which is always to be A in the domain file
            Array.Copy(ligandChains, 0, asymChains, 1, ligandChains.Length);
            string[] fileChains = GetNewFileChains(asymChains);

            StreamWriter dataWriter = new StreamWriter(newDomainFile);
            string remark = domainInfo[0];
            string asymChainLine = ParseHelper.FormatStringFieldsToString(asymChains);
            string fileChainLine = ParseHelper.FormatStringFieldsToString(fileChains);

            remark = remark + "\r\n" + "REMARK   2 AsymChains    FileChains\r\n";
            remark = remark + "REMARK   2   " + asymChainLine + "\r\n";
            remark = remark + "REMARK   2   " + fileChainLine;
            dataWriter.WriteLine(remark);
            chainMap = new string[2];
            chainMap[0] = asymChainLine;
            chainMap[1] = fileChainLine;

            FileInfo fileInfo = new FileInfo(newDomainFile);
            string chainDomain = fileInfo.Name.Substring(0, fileInfo.Name.IndexOf(".pfam"));

            string seqres = domainInfo[1];
            int ligandIndex = 1;
            string ligandFileChain = "";
            string newSeqResLine = "";
            foreach (string ligandChain in ligandChains)
            {
                ligandFileChain = fileChains[ligandIndex];
                foreach (string seqresLine in ligandAtomInfoHashes[0][ligandChain])
                {
                    newSeqResLine = ChangeSeqresLineChainID(seqresLine, ligandFileChain);
                    seqres += ("\r\n" + newSeqResLine);
                }
                ligandIndex ++;
            }
            dataWriter.WriteLine(seqres);

            string atomLines = domainInfo[2];
            dataWriter.WriteLine(atomLines);
            ligandIndex = 1;
            string newAtomLine = "";
            int atomId = lastAtomId + 1;
            foreach (string ligandChain in ligandChains)
            {
                ligandFileChain = fileChains[ligandIndex];
                foreach (string atomLine in ligandAtomInfoHashes[1][ligandChain])
                {
                    newAtomLine = ChangeAtomLineChainID(atomLine, ligandFileChain);
                    newAtomLine = ChangeAtomLineSeqID(atomLine, atomId);
                    atomId++;
                    dataWriter.WriteLine(newAtomLine);
                }
            }
            dataWriter.WriteLine("END");
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFile"></param>
        /// <returns></returns>
        private string[] ReadDomainFile(string domainFile, out int lastAtomId)
        {
            StreamReader domainReader = new StreamReader(domainFile);
            string line = "";
            string remark = "";
            string seqRes = "";
            string atomLines = "";
            lastAtomId = 1;
            while ((line = domainReader.ReadLine()) != null)
            {
                if (line.IndexOf("HEADER") > -1 || line.IndexOf("REMARK") > -1)
                {
                    remark += (line + "\r\n");
                }
                else if (line.IndexOf("SEQRES") > -1)
                {
                    seqRes += (line + "\r\n");
                }
                else if (line.IndexOf("END") > -1)
                {
                    continue;
                }
                else if (line.IndexOf("TER   ") > -1)
                {
                    string[] terFields = ParseHelper.ParsePdbTerLine(line);
                    lastAtomId = Convert.ToInt32(terFields[1]);
                    atomLines += (line + "\r\n");
                }
                else
                {
                    atomLines += (line + "\r\n");
                }
            }
            domainReader.Close();
            string[] domainInfo = new string[3];
            domainInfo[0] = remark.TrimEnd("\r\n".ToCharArray ());
            domainInfo[1] = seqRes.TrimEnd("\r\n".ToCharArray ());
            domainInfo[2] = atomLines.TrimEnd("\r\n".ToCharArray ());
            return domainInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbFile"></param>
        /// <param name="ligandChains"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>>[] ReadLigandAtomSeqResLines(string pdbFile, string[] ligandChains)
        {
            StreamReader dataReader = new StreamReader(pdbFile);
            string line = "";
            Dictionary<string, List<string>> chainAtomLinesHash = new Dictionary<string,List<string>> ();
            Dictionary<string, List<string>>  chainSeqResLinesHash = new Dictionary<string,List<string>> ();
            bool atomSelected = false;
            // may have different chain ids in the file
            string[] ligandFileChains = null;
            string selectAsymChain = "";
            int chainIndex = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("AsymChains") > -1 && line.IndexOf("FileChains") > -1)
                {
                    string asymChainLine = dataReader.ReadLine();
                    asymChainLine = asymChainLine.Replace("REMARK   2", "").Trim();
                    string fileChainLine = dataReader.ReadLine();
                    fileChainLine = fileChainLine.Replace("REMARK   2", "").Trim();
                    string[] asymChains = asymChainLine.Split(',');
                    string[] fileChains = fileChainLine.Split(',');
                    ligandFileChains = GetSelectFileChains(ligandChains, asymChains, fileChains);
                }
                if (line.IndexOf("SEQRES") > -1)
                {
                    string[] fields = ParseHelper.SplitPlus(line, ' ');
                    chainIndex = Array.IndexOf(ligandFileChains, fields[2]);
                    if (chainIndex > -1)
                    {
                        selectAsymChain = ligandChains[chainIndex];  // use asymmetric chain as chain id
                        if (chainSeqResLinesHash.ContainsKey(selectAsymChain))
                        {
                            List<string> seqResLineList = (List<string>)chainSeqResLinesHash[selectAsymChain];
                            seqResLineList.Add(line);
                        }
                        else
                        {
                            List<string> seqResLineList = new List<string>();
                            seqResLineList.Add(line);
                            chainSeqResLinesHash.Add(selectAsymChain, seqResLineList);
                        }
                    }
                }
                if (line.IndexOf("ATOM  ") > -1 || line.IndexOf("HETATM") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    chainIndex = Array.IndexOf(ligandFileChains, atomFields[5]);
                    if (chainIndex > -1)
                    {
                        selectAsymChain = ligandChains[chainIndex];
                        if (chainAtomLinesHash.ContainsKey(selectAsymChain))
                        {
                            List<string> atomLineList = (List<string>)chainAtomLinesHash[selectAsymChain];
                            atomLineList.Add(line);
                        }
                        else
                        {
                            List<string> atomLineList = new List<string>();
                            atomLineList.Add(line);
                            chainAtomLinesHash.Add(selectAsymChain, atomLineList);
                        }
                        atomSelected = true;
                    }
                }
                else if (line.IndexOf("TER   ") > -1)
                {
                    if (atomSelected)
                    {
                        string[] terFields = ParseHelper.ParsePdbTerLine(line);
                        chainIndex = Array.IndexOf(ligandFileChains, terFields[5]);
                        if (chainIndex > -1)
                        {
                            selectAsymChain = ligandChains[chainIndex];
                            if (chainAtomLinesHash.ContainsKey(selectAsymChain))
                            {
                                List<string> atomLineList = (List<string>)chainAtomLinesHash[selectAsymChain];
                                atomLineList.Add(line);
                            }
                        }
                        atomSelected = false;
                    }
                }
            }
            dataReader.Close();
            Dictionary<string, List<string>>[] chainAtomInfoHashes = new Dictionary<string, List<string>>[2];
            chainAtomInfoHashes[0] = chainSeqResLinesHash;
            chainAtomInfoHashes[1] = chainAtomLinesHash;
            return chainAtomInfoHashes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private string[] GetDomainAsymChains(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select AsymChain From PdbPfamChain Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable asymChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] domainAsymChains = new string[asymChainTable.Rows.Count];
            int count = 0;
            foreach (DataRow asymChainRow in asymChainTable.Rows)
            {
                domainAsymChains[count] = asymChainRow["AsymChain"].ToString().TrimEnd();
                count++;
            }
            return domainAsymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectAsymChains"></param>
        /// <param name="asymChains"></param>
        /// <param name="fileChains"></param>
        /// <returns></returns>
        private string[] GetSelectFileChains(string[] selectAsymChains, string[] asymChains, string[] fileChains)
        {
            string[] selectFileChains = new string[selectAsymChains.Length];
            int chainIndex = -1;
            for(int selectChainIndex = 0; selectChainIndex < selectAsymChains.Length; selectChainIndex ++)
            {
                chainIndex = Array.IndexOf (asymChains, selectAsymChains[selectChainIndex]);
                if (chainIndex > -1)
                {
                    selectFileChains[selectChainIndex] = fileChains[chainIndex];
                }
            }
            return selectFileChains;
        }
        #endregion

        #region asu info table
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public DataTable GetAsuSeqInfoTable()
        {
            string asuInfoFile = "asuSeqInfo.txt";
            DataTable asuTable = null;
            if (File.Exists(asuInfoFile))
            {
                asuTable = ReadTableFromFile(asuInfoFile);
            }
            else
            {

                string queryString = "Select PdbId, AsymID, AuthorChain, EntityID, SequenceInCoord, PolymerStatus, PolymerType From AsymUnit " +
                        " Where PolymerType = 'polypeptide';";
                // " Where PolymerStatus <> 'water';";
                asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                WriteTableToFile(asuTable, asuInfoFile);
            }
            return asuTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="tableFile"></param>
        private void WriteTableToFile(DataTable dataTable, string tableFile)
        {
            StreamWriter dataWriter = new StreamWriter(tableFile);
            string headerLine = "";
            foreach (DataColumn dCol in dataTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            headerLine = headerLine.TrimEnd('\t');
            dataWriter.WriteLine(headerLine);
            foreach (DataRow dataRow in dataTable.Rows)
            {
                dataWriter.WriteLine(ParseHelper.FormatDataRow (dataRow));
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableFile"></param>
        /// <returns></returns>
        private DataTable ReadTableFromFile(string tableFile)
        {
            DataTable fileTable = new DataTable();
            string line = "";
            StreamReader dataReader = new StreamReader(tableFile);
            string headerLine = dataReader.ReadLine();  // first line is the hearder line
            string[] headerColumns = headerLine.Split('\t');
            foreach (string col in headerColumns)
            {
                fileTable.Columns.Add(new DataColumn (col));
            }
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] dataItems = line.Split('\t');
                DataRow fileRow = fileTable.NewRow();
                fileRow.ItemArray = dataItems;
                fileTable.Rows.Add(fileRow);
            }
            dataReader.Close();
            return fileTable;
        }

        public void FindWrongEntFiles()
        {
            string entFileDir = @"D:\DbProjectData\PDB\regular";
            string[] hashFolders = Directory.GetDirectories(entFileDir);
            StreamWriter dataWriter = new StreamWriter("WrongEntEntries.txt");
            foreach (string hashFolder in hashFolders)
            {
                string[] entFiles = Directory.GetFiles(hashFolder);
                foreach (string entFile in entFiles)
                {
                    FileInfo fileInfo = new FileInfo(entFile);
                    if (fileInfo.Length < 1000)
                    {
                        dataWriter.WriteLine(fileInfo.Name);
                    }
                }
            }
            dataWriter.Close();
        }
        #endregion

        #region add ligand clusters info to pfam domain alignment file
        /// <summary>
        /// 
        /// </summary>
        public void AddLigandClusterInfoToPfamDomainCompressFiles ()
        {
            ProtCidSettings.tempDir = @"X:\pfamdomain_temp";
            string alignFileDir = @"D:\protcid_update31Fromv30\pfam\DomainAlign";
            string dataType = "pdb";
            pfamDomainFileDir = Path.Combine(ProtCidSettings.tempDir, dataType);
            if (! Directory.Exists (pfamDomainFileDir))
            {
                Directory.CreateDirectory(pfamDomainFileDir);
            }
            string domainAlignFileDir = Path.Combine (alignFileDir, dataType);
            string[] pdbTarGzFiles = Directory.GetFiles(domainAlignFileDir);

            string pfamId = "";
            string pfamFileDataDir = "";
            string tarGzGroupFile = "";
            foreach (string pdbTarGzFile in pdbTarGzFiles )
            {
                FileInfo fileInfo = new FileInfo(pdbTarGzFile);
                pfamId = fileInfo.Name.Replace("_" + dataType + ".tar.gz", "");
   //             pfamId = GetPfamIdFromPfamAcc(pfamAcc);

                if (pfamId != "1-cysPrx_C")
                {
                    continue;
                }

    /*            if (File.Exists(Path.Combine(domainAlignFileDir, pfamId + "_" + dataType + ".tar.gz")))
                {
                    continue;
                }*/

                pfamFileDataDir = Path.Combine(pfamDomainFileDir, pfamId);
                if (!Directory.Exists(pfamFileDataDir))
                {
                    Directory.CreateDirectory(pfamFileDataDir);
                    tarOperator.UnTar(pdbTarGzFile, pfamFileDataDir);
                    if (Directory.Exists(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType)))
                    {
                        string[] subDirFiles = Directory.GetFiles(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType));
                        foreach (string subDirFile in subDirFiles )
                        {
                            FileInfo subFileInfo = new FileInfo (subDirFile);
                            File.Move(subDirFile, Path.Combine(pfamFileDataDir, subFileInfo.Name));
                        }
                        Directory.Delete(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType));
                    }
                }

                try
                {
                    tarGzGroupFile = CompressPfamDomainFilesByAddingClusterInfo(pfamId, pfamFileDataDir, dataType);

                    try
                    {
                        File.Move(tarGzGroupFile, Path.Combine(domainAlignFileDir, pfamId + "_" + dataType + ".tar.gz"));
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete pfam file " + pfamId + " " + pfamId + " error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine("Delete pfam file " + pfamId + " " + pfamId + " error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }              
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine("Add cluster info to pfam-ligand domain alignment file error: " + pfamId + " " + pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();                    
                }                               
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        /// <param name="groupFileName"></param>
        public string CompressPfamDomainFilesByAddingClusterInfo(string pfamId, string fileDataDir, string dataType)
        {
            string groupFileName = pfamId;
            string queryString = string.Format("Select PfamID, ClusterID, PdbID, ChainDomainID, LigandChain From PfamLigandClustersHmm Where PfamID = '{0}';", pfamId);
            DataTable ligandClusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] pfamDomainFiles = Directory.GetFiles(fileDataDir, "*.pfam");
            if (pfamDomainFiles.Length == 0)
            {
                fileDataDir = Path.Combine(pfamDomainFileDir, pfamId);
                pfamDomainFiles = Directory.GetFiles(fileDataDir, "*.pfam");
            }
            Dictionary<string, string[]> domainFileChainMapHash = GetAsymFileChainMapFromFiles(pfamDomainFiles); // the map between chain Ids and asymmtric chain ids in the domain file

            List<string> coordDomainList = new List<string>(domainFileChainMapHash.Keys);
            coordDomainList.Sort();
            string[] coordDomains = new string[coordDomainList.Count];
            coordDomainList.CopyTo(coordDomains);

            // remove those domains which are not in the clusters
            DataTable ligandClusterNeedTable = GetPfamClusterLigandsNeedAlign(ligandClusterTable, coordDomains);
            Dictionary<int, string[]> clusterLigandHash = GetClusterLigandsHash(ligandClusterNeedTable, coordDomains, domainFileChainMapHash);
            string tarGroupFileName = "";
            string destDataDir = pfamDomainFileDir;
  /*          string destDataDir = Path.Combine(pfamDomainFileDir, dataType);
            if (!Directory.Exists(destDataDir))
            {
                Directory.CreateDirectory(destDataDir);
            }*/
            try
            {
                string[] pmlScriptFiles = Directory.GetFiles(fileDataDir, "*.pml");
                string[] pmlScriptFileNames = new string[pmlScriptFiles.Length];
                int i = 0;
                string scriptFileName = "";                
                foreach (string pmlScriptFile in pmlScriptFiles )
                {
                    // also change pml script file name from Pfam accession code to ID code
                    scriptFileName = AddClusterInfoToPymolScriptFile(pmlScriptFile, clusterLigandHash, pfamId, coordDomains);
                    pmlScriptFileNames[i] = scriptFileName;
                    i++;
                }
                List<string> compressTextFileList = new List<string>(pmlScriptFileNames);
                if (ligandClusterNeedTable.Rows.Count > 0)
                {
                    string ligandClusterFile = Path.Combine(fileDataDir, pfamId + "_clusterInfo_" + dataType + ".txt");
                    WritePfamLigandClusterInfoTable(ligandClusterFile, ligandClusterNeedTable, coordDomains, domainFileChainMapHash, null);
                    compressTextFileList.Add(pfamId + "_clusterInfo_" + dataType + ".txt");
                }
                FileInfo howUseFileInfo = new FileInfo (generalInstructFile);
                string pfamInstructFile = Path.Combine(fileDataDir, howUseFileInfo.Name.Replace(".txt", "_" + dataType + ".txt"));
                ReWriteInstructFile(pfamId, dataType, generalInstructFile, pfamInstructFile);
                compressTextFileList.Add(howUseFileInfo.Name.Replace(".txt", "_" + dataType + ".txt"));
                string[] compressTextFiles = new string[compressTextFileList.Count];
                compressTextFileList.CopyTo(compressTextFiles);
                tarGroupFileName = groupFileName + "_" + dataType + ".tar.gz";
                CompressGroupPfamDomainFiles(coordDomains, compressTextFiles, tarGroupFileName, fileDataDir, fileDataDir);
                File.Move(Path.Combine(fileDataDir, tarGroupFileName), Path.Combine(destDataDir, tarGroupFileName));
                Directory.Delete(fileDataDir);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            return Path.Combine (destDataDir, tarGroupFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="groupFileName"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="dataType"></param>
        public void CompressGroupPfamDomainFiles(string[] coordDomains, string[] pymolScriptFiles, string groupFileName, string fileDataDir)
        {
            string tarGroupFileName = groupFileName;
            if (groupFileName.IndexOf("tar.gz") < 0)
            {
                tarGroupFileName = groupFileName + ".tar.gz";
            }

            string[] domainCoordFiles = new string[coordDomains.Length];
            for (int i = 0; i < coordDomains.Length; i++)
            {
                domainCoordFiles[i] = coordDomains[i] + ".pfam";
            }
            string[] filesToBeCompressed = new string[domainCoordFiles.Length + pymolScriptFiles.Length];
            Array.Copy(domainCoordFiles, 0, filesToBeCompressed, 0, domainCoordFiles.Length);
            Array.Copy(pymolScriptFiles, 0, filesToBeCompressed, domainCoordFiles.Length, pymolScriptFiles.Length);

            string tarFileName = fileCompress.RunTar(tarGroupFileName, filesToBeCompressed, fileDataDir, true);

            if (filesToBeCompressed.Length > fileCompress.maxNumOfFiles)
            {
                string fileFolder = Path.Combine(fileDataDir, tarGroupFileName.Replace(".tar.gz", ""));
                Directory.Delete(fileFolder, true);
            }
            else
            {
                foreach (string domainFile in filesToBeCompressed)
                {
                    File.Delete(Path.Combine(fileDataDir, domainFile));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public string AddClusterInfoToPymolScriptFile(string pymolScriptFile, Dictionary<int, string[]> clusterLigandsHash, string pfamId, string[] coordDomains)
        {
            string newPymolScriptFile = pymolScriptFile.Replace(pfamId + "_", pfamId + "_new_");
            // for ligand clusters
            string seleClusterString = "";
            if (clusterLigandsHash != null)
            {
                seleClusterString = domainAlignPymolScript.FormatClusterLigandString(clusterLigandsHash, coordDomains);
            }
            
            StreamWriter dataWriter = new StreamWriter(newPymolScriptFile);
            StreamReader dataReader = new StreamReader(pymolScriptFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                dataWriter.WriteLine(line);
                if (line.IndexOf("sele selectLigands,") > -1 )
                {
                    line = dataReader.ReadLine();
                    dataWriter.WriteLine(line);
                    if (seleClusterString != "")
                    {
                        dataWriter.Write(seleClusterString + "\n");
                    }                       
                }               
            }
            dataReader.Close();
            dataWriter.Close();
            File.Delete(pymolScriptFile);           
            File.Move(newPymolScriptFile, pymolScriptFile);

            FileInfo fileInfo = new FileInfo(pymolScriptFile);
            return fileInfo.Name;
                     
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomainFiles"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetAsymFileChainMapFromFiles (string[] pfamDomainFiles)
        {
            Dictionary<string, string[]> domainFileMapHash = new Dictionary<string, string[]>();
            string chainDomain = "";
            foreach (string domainFile in pfamDomainFiles)
            {
                FileInfo fileInfo = new FileInfo(domainFile);
                chainDomain = fileInfo.Name.Replace(".pfam", "");

                string[] chainMap = ReadChainMapFromFile(domainFile);
                domainFileMapHash.Add(chainDomain, chainMap);
            }
            return domainFileMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFile"></param>
        /// <returns></returns>
        private string[] ReadChainMapFromFile (string domainFile)
        {
            string[] chainMap = new string[2];
            StreamReader dataReader = new StreamReader (domainFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("REMARK   2 AsymChains    FileChains") > -1)
                {
                    line = dataReader.ReadLine();
                    chainMap[0] = line.Remove(0, "REMARK   2 ".Length);
                    line = dataReader.ReadLine();
                    chainMap[1] = line.Remove(0, "REMARK   2 ".Length);
                    break;
                }
            }
            dataReader.Close();
            return chainMap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private string GetPfamIdFromPfamAcc (string pfamAcc)
        {
            string queryString = string.Format("Select Pfam_ID From PfamHmm Where Pfam_Acc = '{0}';", pfamAcc);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
        }
        #endregion

        #region specific cases - CBS
        public void UpdateCBSPymolScriptFile()
        {
            string pfamId = "CBS";
            string pymolScriptFile = @"M:\Qifang\DbProjectData\pfam\DomainAlign\unp\PF00571\PF00571_byDomain.pml";
            string newPymolScriptFile = @"M:\Qifang\DbProjectData\pfam\DomainAlign\unp\PF00571\PF00571_byDomain_pair.pml";
            DataTable pfamChainDomainTable = domainAlignPymolScript.GetPfamChainDomainTable(pfamId);
            StreamReader dataReader = new StreamReader(pymolScriptFile);
            StreamWriter dataWriter = new StreamWriter(newPymolScriptFile);
            string line = "";
            string newLine = "";
            string newDataLine = "";
            string pdbId = "";
            int chainDomainId = 0;
            string domainFileName = "";
            bool canBeAdded = false;
            string pairRangeString = "";
            string centerDomainRange = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    if (canBeAdded)
                    {
                        dataWriter.WriteLine(newDataLine);
                    }
                    newDataLine = "";
                    canBeAdded = true;
                    string[] lineFields = line.Split(' ');
                    domainFileName = lineFields[1];
                    pdbId = domainFileName.Substring(0, 4);
                    chainDomainId = Convert.ToInt32(domainFileName.Substring(4, domainFileName.IndexOf(".pfam") - 4));
                    pairRangeString = GetCBSPairDomainRange(pdbId, chainDomainId, pfamChainDomainTable);
                    if (centerDomainRange == "")
                    {
                        centerDomainRange = pairRangeString;
                    }
                    if (pairRangeString == "")
                    {
                        canBeAdded = false;
                    }
                }
                if (line.IndexOf("show spheres") > -1)
                {
                    newLine = line.Replace("show spheres", "show sticks");
                    newDataLine += (newLine + "\r\n");
                    continue;
                }
                if (line.IndexOf("hide cartoon") > -1)
                {
                    newLine = line.Replace("not  and chain A and ", "not ");
                    newLine = ReplaceByNewDomainRange(newLine, pairRangeString);
                    //   newDataLine += (newLine + "+" + pairRangeString + "\r\n");
                    newDataLine += (newLine + "\r\n");
                    continue;
                }
                if (line.IndexOf("spectrum count") > -1)
                {
                    newLine = ReplaceByNewDomainRange(line, pairRangeString);
                    newDataLine += (newLine + "\r\n");
                    continue;
                }
                if (line.IndexOf("align ") > -1)
                {
                    newLine = ReplaceByNewDomainRange(line, pairRangeString, centerDomainRange);
                    newDataLine += (newLine + "\r\n");
                    continue;
                }
                if (line.IndexOf("center") > -1)
                {
                    dataWriter.WriteLine(line);
                }
                newDataLine += (line + "\r\n");
            }
            dataWriter.Close();
            dataReader.Close();
        }

        private string ReplaceByNewDomainRange(string dataLine, string newDomainRange)
        {
            int resiIndex = dataLine.IndexOf("resi ");
            resiIndex = resiIndex + "resi ".Length;
            int commonIndex = dataLine.IndexOf(",");
            string newDataLine = "";
            if (commonIndex < resiIndex)
            {
                newDataLine = dataLine.Remove(resiIndex, dataLine.Length - resiIndex);
                newDataLine = newDataLine + newDomainRange;
            }
            else if (commonIndex > resiIndex)
            {
                newDataLine = dataLine.Remove(resiIndex, commonIndex - resiIndex);
                newDataLine = newDataLine.Insert(resiIndex, newDomainRange);
            }
            return newDataLine;
        }

        private string ReplaceByNewDomainRange(string dataLine, string newDomainRange, string centerDomainRange)
        {
            string newDataLine = ReplaceByNewDomainRange(dataLine, newDomainRange);
            int centerResiIndex = newDataLine.LastIndexOf("resi ");
            centerResiIndex = centerResiIndex + "resi ".Length;
            newDataLine = newDataLine.Substring(0, centerResiIndex);
            newDataLine = newDataLine + centerDomainRange;
            return newDataLine;
        }

        private string GetCBSPairDomainRange(string pdbId, int chainDomainId, DataTable pfamChainDomainTable)
        {
            DataRow[] chainDomainRows = pfamChainDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
            string asymChain = chainDomainRows[0]["AsymChain"].ToString().TrimEnd();
            Range domainRange = new Range();
            domainRange.startPos = Convert.ToInt32(chainDomainRows[0]["SeqStart"].ToString());
            domainRange.endPos = Convert.ToInt32(chainDomainRows[0]["SeqEnd"].ToString());
            DataRow[] otherDomainRows = pfamChainDomainTable.Select(string.Format("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain));
            Range pairRange = GetTheCBSPair(otherDomainRows, domainRange, chainDomainId);
            if (pairRange == null)
            {
                return "";
            }
            string pairRangeString = "";
            /*    string pairRangeString = pairRange.startPos.ToString() + "-" + pairRange.endPos.ToString();*/
            if (pairRange.startPos < domainRange.startPos)
            {
                pairRangeString = pairRange.startPos.ToString() + "-" + pairRange.endPos.ToString() + "+"
                    + domainRange.startPos.ToString() + "-" + domainRange.endPos.ToString();
            }
            else
            {
                pairRangeString = domainRange.startPos.ToString() + "-" + domainRange.endPos.ToString() + "+" +
                    pairRange.startPos.ToString() + "-" + pairRange.endPos.ToString();
            }
            return pairRangeString;
        }

        private Range GetTheCBSPair(DataRow[] otherDomainRows, Range domainRange, int chainDomainId)
        {
            Range pairRange = new Range();
            int seqStart = 0;
            int seqEnd = 0;
            int seqDif = 0;
            int otherChainDomainId = 0;
            int minSeqDif = 99999;
            foreach (DataRow domainRow in otherDomainRows)
            {
                otherChainDomainId = Convert.ToInt32(domainRow["ChainDomainID"].ToString());
                if (chainDomainId == otherChainDomainId)
                {
                    continue;
                }
                seqStart = Convert.ToInt32(domainRow["SeqStart"].ToString());
                seqEnd = Convert.ToInt32(domainRow["SeqENd"].ToString());
                if (seqStart > domainRange.startPos)
                {
                    seqDif = seqStart - domainRange.endPos;
                    if (seqDif < 0)
                    {
                        seqDif = 0;
                    }
                }
                else if (seqEnd < domainRange.endPos)
                {
                    seqDif = domainRange.startPos - seqEnd;
                    if (seqDif < 0)
                    {
                        seqDif = 0;
                    }
                }
                if (minSeqDif > seqDif)
                {
                    minSeqDif = seqDif;
                    pairRange.startPos = seqStart;
                    pairRange.endPos = seqEnd;
                }
            }
            //     string pairRangeString = pairRange.startPos.ToString() + "-" + pairRange.endPos.ToString();
            //     return pairRangeString;
            if (minSeqDif == 99999)
            {
                return null;
            }
            return pairRange;
        }
        #endregion

        #region debug
        public void AddUnpPfamsNamesScriptFilesToPfamDomainCompressFiles ()
        {            
            ProtCidSettings.tempDir = @"X:\pfamdomain_temp";  
            if (! Directory.Exists (ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            string alignFileDir = @"D:\protcid_update31Fromv30\pfam\DomainAlign";
            string savedFileDir = @"X:\Qifang\ProjectData\Pfam\DomainAlign"; ;
            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string generalInstructFile = Path.Combine(alignFileDir, "HowToUsePfamLigandsData.txt");
            string pfamId = "";
            string[] dataTypes = {"pdb", "unp", "cryst"};
            foreach (string dataType in dataTypes)
            {
                pfamDomainFileDir = Path.Combine(ProtCidSettings.tempDir, dataType);
                if (!Directory.Exists(pfamDomainFileDir))
                {
                    Directory.CreateDirectory(pfamDomainFileDir);
                }
            }
            Dictionary<string, string> domainUnPfamsPmlNameDict = new Dictionary<string,string> ();
            string domainAlignFileDir = "";
            string pfamFileDataDir = "";
            string newTarGzFile = "";
            string tarGzFile = "";
            string destFileDir = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString();
                domainUnPfamsPmlNameDict.Clear();             
                foreach (string dataType in dataTypes)
                {
                    destFileDir = Path.Combine(savedFileDir, dataType);
                    domainAlignFileDir = Path.Combine(alignFileDir, dataType);
                    pfamDomainFileDir = Path.Combine(ProtCidSettings.tempDir, dataType);
                    tarGzFile = Path.Combine(domainAlignFileDir, pfamId + "_" + dataType + ".tar.gz");
                    if (! File.Exists(tarGzFile))
                    {
                        continue;
                    }
                    
                    pfamFileDataDir = Path.Combine(pfamDomainFileDir, pfamId);
                    if (!Directory.Exists(pfamFileDataDir))
                    {
                        Directory.CreateDirectory(pfamFileDataDir);
                       
                        tarOperator.UnTar(tarGzFile, pfamFileDataDir);
                        if (Directory.Exists(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType)))
                        {
                            string[] subDirFiles = Directory.GetFiles(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType));
                            foreach (string subDirFile in subDirFiles)
                            {
                                FileInfo subFileInfo = new FileInfo(subDirFile);
                                File.Move(subDirFile, Path.Combine(pfamFileDataDir, subFileInfo.Name));
                            }
                            Directory.Delete(Path.Combine(pfamFileDataDir, pfamId + "_" + dataType));
                        }
                    }

                    try
                    {
                        if (dataType == "pdb" && domainUnPfamsPmlNameDict.Count == 0)
                        {
                            string[] coordDomains = GetCoordDomainsFromPdbTarGzFile(pfamFileDataDir);
                            domainUnPfamsPmlNameDict = pmlScriptUpdate.GetDomainNewPyMolNameByUnpPfams(coordDomains);
                        }

                        newTarGzFile = CompressPfamDomainFilesByAddingUnpPfamsScriptFiles(pfamId, pfamFileDataDir, destFileDir, dataType, domainUnPfamsPmlNameDict, generalInstructFile);                      
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine("Add cluster info to pfam-ligand domain alignment file error: " + pfamId + " " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    } 
                }
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbFileDir"></param>
        /// <returns></returns>
        private string[] GetCoordDomainsFromPdbTarGzFile (string pdbFileDir)
        {
            List<string> coordDomainList = new List<string>();
            string[] pfamFiles = Directory.GetFiles(pdbFileDir, "*.pfam");
            foreach (string pfamFile in pfamFiles)
            {
                FileInfo fileInfo = new FileInfo(pfamFile);
                coordDomainList.Add(fileInfo.Name.Replace(".pfam", ""));
            }
            return coordDomainList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        private string CompressPfamDomainFilesByAddingUnpPfamsScriptFiles(string pfamId, string fileDataDir, string destDataDir, string dataType, 
            Dictionary<string, string> domainUnPfamsPmlNameDict, string generalInstructFile)
        {
            string groupFileName = pfamId;
            string tarGroupFileName = "";

            string[] pfamDomainFiles = Directory.GetFiles(fileDataDir, "*.pfam");

            string[] coordDomains = new string[pfamDomainFiles.Length];
            int count = 0;
            foreach (string domainFile in pfamDomainFiles)
            {
                FileInfo fileInfo = new FileInfo(domainFile);
                coordDomains[count] = fileInfo.Name.Replace(fileInfo.Extension, "");
                count++;
            }

            string pfamAssingmentInfoFile = pfamId + "_PdbfamAssignments.txt";
            WritePfamDomainAssignmentsToTextFile(coordDomains, Path.Combine (fileDataDir, pfamAssingmentInfoFile));
            
            try
            {
                string[] pmlScriptFiles = Directory.GetFiles(fileDataDir, "*.pml");
                string[] pmlScriptFileNames = new string[pmlScriptFiles.Length];
                string scriptFileName = "";
                int i = 0;
                foreach (string pmlScriptFile in pmlScriptFiles)
                {
                    FileInfo fileInfo = new FileInfo(pmlScriptFile);
                    scriptFileName = fileInfo.Name;
                    pmlScriptFileNames[i] = scriptFileName;

                    i++;
                }
                string renameScriptFile = Path.Combine(fileDataDir, groupFileName + "_RenameByUnpPfams.pml");
                string renameScriptFileName = pmlScriptUpdate.WritePmlScriptRenamePmlObjects(coordDomains, renameScriptFile, domainUnPfamsPmlNameDict, ".pfam"); 
                List<string> compressTextFileList = new List<string> (pmlScriptFileNames);
                compressTextFileList.Add (renameScriptFileName);                

                string[] textFiles = Directory.GetFiles(fileDataDir, "*.txt");
                foreach (string txtFile in textFiles)
                {
                    FileInfo fileInfo = new FileInfo(txtFile);
                    if (fileInfo.Name.IndexOf("HowToUsePfamLigandsData") > -1)
                    {                       
                        ReWriteInstructFile(pfamId, dataType, generalInstructFile, txtFile);
                    }
                    compressTextFileList.Add(fileInfo.Name);
                }
                tarGroupFileName = groupFileName + "_" + dataType + ".tar.gz";
                CompressGroupPfamDomainFiles(coordDomains, compressTextFileList.ToArray(), tarGroupFileName, fileDataDir, fileDataDir);
                File.Move(Path.Combine(fileDataDir, tarGroupFileName), Path.Combine(destDataDir, tarGroupFileName));
                Directory.Delete(fileDataDir);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            return Path.Combine(destDataDir, tarGroupFileName);
        }          

        public void RenamePfamDomainAlignFiles ()
        {
            string dataType = "pdb";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Rename Pfam domain alignment files from PfamACC to PfamID");

            string queryString = "Select Distinct Pfam_ID, Pfam_ACC From PdbPfam;";
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = pfamTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamTable.Rows.Count;

            string pfamId = "";
            string pfamAcc = "";
            List<string> pfamIdList = new List<string>();
            foreach (DataRow pfamIdRow in pfamTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                pfamAcc = pfamIdRow["Pfam_Acc"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (IsPfamDomainAlignFileCorrect (pfamId, pfamAcc, pfamDomainPymolFileDir, dataType))
                {
                    pfamIdList.Add(pfamId);
                }
    //            CompressPfamDomainsFromPdb(pfamId, asuTable);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="domainAlignDir"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        private bool IsPfamDomainAlignFileCorrect (string pfamId, string pfamAcc, string domainAlignDir, string dataType)
        {
            string domainAlignFileDir = Path.Combine(domainAlignDir, dataType);
            string pfamIdFile = Path.Combine(domainAlignFileDir, pfamId + "_" + dataType + ".tar.gz");
            string pfamAccFile = Path.Combine(domainAlignFileDir, pfamAcc + "_" + dataType + ".tar.gz");

            if (File.Exists(pfamAccFile) && File.Exists(pfamIdFile))
            {
                FileInfo pfamIdFileInfo = new FileInfo(pfamIdFile);
                FileInfo pfamAccFileInfo = new FileInfo(pfamAccFile);

                if (pfamIdFileInfo.Length < pfamAccFileInfo.Length && pfamIdFileInfo.Length < 10000)
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// 
        /// </summary>
        public void CompressPfamDomainsFromPdb()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress domain align from pdb");

            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            DataTable asuTable = GetAsuSeqInfoTable();

            ProtCidSettings.progressInfo.totalOperationNum = pfamTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamTable.Rows.Count;

            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_ID"].ToString().TrimEnd ();

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                CompressPfamDomainsFromPdb(pfamId, asuTable);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        private void CompressPfamDomainsFromPdb(string pfamId, DataTable asuTable)
        {
  //          string pfamAcc = GetPfamAccFromPfamId(pfamId);
            string groupFileName = pfamId;
            string domainAlignFile = Path.Combine(pfamDomainPymolFileDir, "pdb\\" + groupFileName + "_pdb.tar.gz");
            if (! File.Exists(domainAlignFile))
            {
                return;
            }

            if (File.Exists(Path.Combine(pfamDomainPymolFileDir, "unp\\" + groupFileName + "_unp.tar.gz")) &&
                File.Exists (Path.Combine (pfamDomainPymolFileDir, "cryst\\" + groupFileName + "_cryst.tar.gz")))
            {
                return;
            }

            string queryString = string.Format("Select PdbPfam.PdbID, PdbPfam.EntityID, PdbPfam.DomainID, SeqStart, SeqEnd, AlignStart, AlignEnd, " +
              " HmmStart, HmmEnd, QueryAlignment, HmmAlignment, AsymChain, AuthChain, ChainDomainID " +
              " From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND PdbPfam.DomainId = PdbPfamChain.DomainID " +
              " AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            if (pfamDomainTable.Rows.Count == 0)
            {
                return;
            }

            queryString = string.Format("Select Distinct PdbID, ChainDomainID From PfamLigands WHere PfamID = '{0}';", pfamId);
            DataTable pfamLigandsTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select Distinct PdbID, ChainDomainID From PfamDnaRnas WHere PfamID = '{0}';", pfamId);
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query( queryString);
            ParseHelper.AddNewTableToExistTable(pfamDnaRnaTable, ref pfamLigandsTable);

            ProtCidSettings.logWriter.WriteLine(pfamId);
            try
            {
                // retrieve the exist domain files from pdb folder since it contains more domain files than unp and cryst
                tarOperator.UnTar(domainAlignFile, pfamDomainPymolFileDir);

                string entryDomainBestCoordInPfam = "";
                //      Hashtable pfamBestStructChainDomainHash = GetPfamBestStructChainDomainHash(pfamDomainTable, asuTable, out entryDomainBestCoordInPfam, bestStructType);
                Dictionary<string, string>[] pfamBestStructDomainDicts = GetPfamBestStructChainDomainHash(pfamDomainTable, asuTable, pfamLigandsTable, out entryDomainBestCoordInPfam);
                Dictionary<string, string> pdbPfamBestStructDomainDict = pfamBestStructDomainDicts[0];
                Dictionary<string, string> unpPfamBestStructDomainDict = pfamBestStructDomainDicts[1];
                Dictionary<string, string> crystPfamBestStructDomainDict = pfamBestStructDomainDicts[2];

                string[] pdbPymolScriptFiles = Directory.GetFiles(pfamDomainPymolFileDir, "*.pml");
                if (pdbPymolScriptFiles.Length == 0)
                {
                    string pdbDomainFileDir = Path.Combine(pfamDomainPymolFileDir, pfamId);
                    if (! Directory.Exists(pdbDomainFileDir))
                    {
                        pdbDomainFileDir = Path.Combine(pfamDomainPymolFileDir, pfamId + "_pdb");
                    }
                    MoveFilesToDest(pdbDomainFileDir, pfamDomainPymolFileDir);
                }
                pdbPymolScriptFiles = Directory.GetFiles(pfamDomainPymolFileDir, "*.pml");

                string[] unpCoordDomains = GetPfamEntryChainDomains(unpPfamBestStructDomainDict);
                if (!unpCoordDomains.Contains(entryDomainBestCoordInPfam))
                {
                    string[] unpCoordDomainsWithBest = new string[unpCoordDomains.Length + 1];
                    unpCoordDomainsWithBest[0] = entryDomainBestCoordInPfam;
                    Array.Copy(unpCoordDomains, 0, unpCoordDomainsWithBest, 1, unpCoordDomains.Length);
                    unpCoordDomains = unpCoordDomainsWithBest;
                }
                string[] crystCoordDomains = GetPfamEntryChainDomains(crystPfamBestStructDomainDict);
                if (!crystCoordDomains.Contains(entryDomainBestCoordInPfam))
                {
                    string[] crystCoordDomainsWithBest = new string[crystCoordDomains.Length + 1];
                    crystCoordDomainsWithBest[0] = entryDomainBestCoordInPfam;
                    Array.Copy(crystCoordDomains, 0, crystCoordDomainsWithBest, 1, crystCoordDomains.Length);
                    crystCoordDomains = crystCoordDomainsWithBest;
                }
                string newPymolScriptFileName = "";
                string[] unpPymolScriptFiles = new string[pdbPymolScriptFiles.Length];
                string[] crystPymolScriptFiles = new string[pdbPymolScriptFiles.Length];
                int count = 0;
                foreach (string pymolScriptFile in pdbPymolScriptFiles)
                {
                    newPymolScriptFileName = pmlScriptUpdate.ReWritePymolScriptFile(pymolScriptFile, unpCoordDomains, "unp");
                    unpPymolScriptFiles[count] = newPymolScriptFileName;
                    newPymolScriptFileName = pmlScriptUpdate.ReWritePymolScriptFile(pymolScriptFile, crystCoordDomains, "cryst");
                    crystPymolScriptFiles[count] = newPymolScriptFileName;
                    count++;
                }
                try
                {
                    CompressPfamDomainFiles(unpCoordDomains, unpPymolScriptFiles, groupFileName, pfamDomainPymolFileDir, "unp");
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("compress UNP " + groupFileName + " errors: " + ex.Message);
                }
                try
                {
                    CompressPfamDomainFiles(crystCoordDomains, crystPymolScriptFiles, groupFileName, pfamDomainPymolFileDir, "cryst");
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("compress cryst " + groupFileName + " errors: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress " + pfamId + " domain align files errors: " + ex.Message);
            }
            finally
            {
                string[] dataFiles = Directory.GetFiles(pfamDomainPymolFileDir, "*.pfam");
                foreach (string dataFile in dataFiles)
                {
                    File.Delete(dataFile);
                }
                string[] pymolScriptFiles = Directory.GetFiles(pfamDomainPymolFileDir, "*.pml");
                foreach (string scriptFile in pymolScriptFiles)
                {
                    File.Delete(scriptFile);
                }
            }
        }

        public void TestDomainFileWriter()
        {
            string pdbId = "3uom";

            string asymChain = "A";
            // add ligands
            string[] ligandChains = GetInteractingLigands(pdbId, asymChain);
            string[] asymChains = new string[ligandChains.Length + 1];
            asymChains[0] = asymChain;
            Array.Copy(ligandChains, 0, asymChains, 1, ligandChains.Length);

            int[] seqIdsInCoord = null;
            string[] fileChains = null;
            string domainFile = Path.Combine(pfamDomainPymolFileDir, "1tsr1.pfam");
            string gzXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string xmlFile = ParseHelper.UnZipFile(gzXmlFile, ProtCidSettings.tempDir);
            WriteChainDomainLigandFile(pdbId, asymChains, xmlFile, out fileChains, out seqIdsInCoord, domainFile);


            File.Delete(xmlFile);
        }
        #endregion

    }
}
