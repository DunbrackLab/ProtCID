using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using InterfaceClusterLib.DomainInterfaces;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamLigandFileCompress : PfamDomainFileCompress
    {
        #region member variables
        private string tarTempDir = "";
        public PfamLigandFileCompress ()
        {
            if (!Directory.Exists(Path.Combine(pfamDomainPymolFileDir, "ligand")))
            {
                Directory.CreateDirectory(Path.Combine(pfamDomainPymolFileDir, "ligand"));
            }
            tarTempDir = Path.Combine(pfamDomainPymolFileDir, "temp");
            if (!Directory.Exists(tarTempDir))
            {
                Directory.CreateDirectory(tarTempDir);
            }       
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void CompressLigandPfamDomainFilesFromPdb ()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress domain align from pdb");

            string queryString = "Select Distinct Ligand From PdbLigands;";
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable asuTable = GetAsuSeqInfoTable();

            ProtCidSettings.progressInfo.totalOperationNum = ligandTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = ligandTable.Rows.Count;

            ProtCidSettings.logWriter.WriteLine("Compress Pfam-ligands files");

            List<string> untarPfamIdList = new List<string>();

            string ligandId = "";
            foreach (DataRow ligandIdRow in ligandTable.Rows)
            {
                ligandId = ligandIdRow["Ligand"].ToString().TrimEnd();
     /*           if (ligandId == "DNA" || ligandId == "RNA")
                {
                    continue;
                }*/

                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    if (ligandId == "DNA" || ligandId == "RNA")
                    {
                        CompressDomainDnaRnaFiles(ligandId);
                    }
                    else
                    {
                        CompressLigandDomainsFromPdb(ligandId, asuTable, ref untarPfamIdList);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " compress pfam-ligand domain files error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " compress pfam-ligand domain files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            try
            {
                Directory.Delete(tarTempDir, true);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Compress Pfam-ligands files done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// compress the coordinate domain files and pymil script files for each pfam-ligand interactions.
        /// </summary>
        /// <param name="ligandId"></param>
        /// <param name="asuTable"></param>
        /// <param name="untarPfamList"></param>
        private void CompressLigandDomainsFromPdb (string ligandId, DataTable asuTable, ref List<string> untarPfamList)
        {
            if (File.Exists(Path.Combine(pfamDomainPymolFileDir, "ligand\\" + ligandId + ".tar.gz")))
            {
                return;
            }

            string[] pfamIds = GetLigandInteractingPfams(ligandId);
            List<string> coordDomainList = new List<string> ();
            List<string> pymolScriptFileList = new List<string> ();

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
               
                // for pymol script files and coordinate domain files
                CompileLigandPfamDomainsAlignPymolScript(ligandId, pfamId, asuTable, ref untarPfamList, ref coordDomainList, ref pymolScriptFileList);
            }
            if (coordDomainList.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " : no coordinate domains.");
                ProtCidSettings.logWriter.WriteLine(ligandId + " : no coordinate domains.");
                ProtCidSettings.logWriter.Flush();
                return;
            }
            string[] coordDomains = new string[coordDomainList.Count];
            coordDomainList.CopyTo(coordDomains);
            string[] pymolScriptFiles = new string[pymolScriptFileList.Count];
            pymolScriptFileList.CopyTo(pymolScriptFiles);
            string groupFileName = ligandId;
            // tar and compress all the pymol script files and domain files, tar file name is the 2,3-letter code for ligand in the pdb
            MoveDomainFilesToDest(coordDomains, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
            MovePymolScriptFiles(pymolScriptFiles, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
            CompressGroupPfamDomainFiles(coordDomains, pymolScriptFiles, groupFileName, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
        }

        #region update ligand-pfam pymol script files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbEntries"></param>
        public void UpdateLigandPfamDomainFilesFromPdb(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update ligand-Pfam alignments");
            ProtCidSettings.logWriter.WriteLine("Update ligand-Pfam alignments");

            string[] updateLigands = GetUpdateLigands(updateEntries);

            DataTable asuTable = GetAsuSeqInfoTable();          

            ProtCidSettings.progressInfo.totalOperationNum = updateLigands.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateLigands.Length;

            List<string> untarPfamIdList = new List<string>();
            string ligandAlignFile = "";

            foreach (string ligandId in updateLigands)
            {
                
                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
          
                try
                {
                    ligandAlignFile = Path.Combine(pfamDomainPymolFileDir, "ligand\\" + ligandId + ".tar.gz");
              //      File.Delete(ligandAlignFile);

                    if (ligandId == "DNA" || ligandId == "RNA")
                    {
                        CompressDomainDnaRnaFiles(ligandId);
                    }
                    else
                    {
                        CompressLigandDomainsFromPdb(ligandId, asuTable, ref untarPfamIdList);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " compress pfam-ligand domain files error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " compress pfam-ligand domain files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            try
            {
                Directory.Delete(tarTempDir, true);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Update ligand-Pfam alignments done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetUpdateLigands(string[] updateEntries)
        {
            List<string> ligandList = new List<string>();
            foreach (string pdbId in updateEntries)
            {
                string[] entryLigands = GetEntryLigands(pdbId);
                foreach (string ligand in entryLigands)
                {
                    if (!ligandList.Contains(ligand))
                    {
                        ligandList.Add(ligand);
                    }
                }
            }
            return ligandList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryLigands(string pdbId)
        {
            string queryString = string.Format("Select Distinct Ligand From PdbLigands Where PdbID = '{0}';", pdbId);
            DataTable ligandTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] entryLigands = new string[ligandTable.Rows.Count];
            int count = 0;
            foreach (DataRow ligandRow in ligandTable.Rows)
            {
                entryLigands[count] = ligandRow["Ligand"].ToString().TrimEnd();
                count++;
            }
            return entryLigands;
        }
        #endregion

        #region compile pymol session scripts
        /// <summary>
        /// write pymol script files either from pdb pymol script if the center domain is not changed
        /// otherwise, select new center domain, write new pymol script files
        /// <param name="ligandId"></param>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        /// <param name="untarpfamList">the pfam id list which are already untar</param>
        /// <param name="coordDomainList">the entire list of domain files</param>
        /// <param name="pymolScriptFileList">the entire list of pymol script files for the ligand</param>
        private void CompileLigandPfamDomainsAlignPymolScript (string ligandId, string pfamId, DataTable asuTable, ref List<string> untarpfamList, 
            ref List<string> coordDomainList, ref List<string> pymolScriptFileList)
        {
    //        string pfamAcc = GetPfamAccFromPfamId(pfamId);
            string groupFileName = pfamId;
            string domainAlignFile = Path.Combine(pfamDomainPymolFileDir, "pdb\\" + groupFileName + "_pdb.tar.gz");
            if (!File.Exists(domainAlignFile))
            {
                return;
            }
            // pfam-ligand interactions
            DataTable ligandPfamInteractionTable = GetLigandPfamInteractionTable(ligandId, pfamId);
            // the pfam domains interacting with this ligand 
            DataTable ligandInterPfamDomainTable = SelectLigandInteractingDomainTable(pfamId, ligandPfamInteractionTable);

            // key: coordDomain, value: the list of the ligand chains and their seqid
            Dictionary<string, string[]> domainLigandChainDict = GetDomainLigandChainHash(ligandPfamInteractionTable);

            if (ligandInterPfamDomainTable.Rows.Count == 0)
            {
                return;
            }

            try
            {
                // retrieve the exist domain files from pdb folder
                if (!untarpfamList.Contains(pfamId))
                {
                    tarOperator.UnTar(domainAlignFile, tarTempDir);
                    untarpfamList.Add(pfamId);
                }
                string[] pdbPymolScriptFiles = Directory.GetFiles(tarTempDir, pfamId + "*.pml");
                if (pdbPymolScriptFiles.Length == 0)
                {
                    string pdbDomainFileDir = Path.Combine(tarTempDir, pfamId);
                    if (!Directory.Exists(pdbDomainFileDir))
                    {
                        pdbDomainFileDir = Path.Combine(tarTempDir, pfamId + "_pdb");
                    }
                    MoveFilesToDest(pdbDomainFileDir, tarTempDir);
                }

                string entryDomainBestCoordInPfam = "";
                Dictionary<string, string> pfamBestStructChainDomainDict = GetPfamBestStructChainDomainIdHash(ligandInterPfamDomainTable, asuTable, out entryDomainBestCoordInPfam);
                string[] coordDomains = GetPfamEntryChainDomains(pfamBestStructChainDomainDict);
                foreach (string coordDomain in coordDomains)
                {
                    if (!coordDomainList.Contains(coordDomain))
                    {
                        coordDomainList.Add(coordDomain);
                    }
                }

                string[] pfamLigandPymolScriptFiles = CompileLigandDomainAlignPymolScriptFiles(pfamId, ligandId, pfamBestStructChainDomainDict,
                    ligandInterPfamDomainTable, entryDomainBestCoordInPfam, domainLigandChainDict, tarTempDir, false); // ligands not for DNA/RNA
                if (pfamLigandPymolScriptFiles != null)
                {
                    pymolScriptFileList.AddRange(pfamLigandPymolScriptFiles);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress " + pfamId + "  " + ligandId + " domain align files errors: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamBestStructDomainHash"></param>
        /// <param name="pfamDomainTable"></param>
        public string[] CompileLigandDomainAlignPymolScriptFiles(string pfamId, string ligandId, Dictionary<string, string> pfamBestStructDomainDict, 
            DataTable pfamDomainTable, string entryDomainBestCoordInPfam, Dictionary<string, string[]> domainLigandChainDict, string domainFileDir, bool isDnaRna)
        {
            Dictionary<string, int[]> domainCoordSeqIdsHash = new Dictionary<string,int[]> ();
            Dictionary <string, string[]> domainFileChainMapHash = new Dictionary<string,string[]> (); // the map between chain Ids and asymmtric chain ids in the domain file
            string[] pfamEntryChainDomains = GetPfamEntryChainDomains(pfamBestStructDomainDict);
            Dictionary<string, Range[]> chainDomainRangesHash = domainAlignPymolScript.GetDomainRangesHash(pfamEntryChainDomains, pfamDomainTable);

            string[] coordDomains = null;
            string groupFileName = pfamId + "_" + ligandId;

            try
            {
                coordDomains = ReadDomainCoordChainInfo(pfamBestStructDomainDict, pfamDomainTable, ref domainCoordSeqIdsHash, ref domainFileChainMapHash, domainFileDir);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  " + ligandId + " write domain coordinate files errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + "  " + ligandId + " write domain coordinate files errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            string[] pymolScriptFiles = null;
            if (coordDomains == null || coordDomains.Length == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  " + ligandId + " no domain files. something wrong.");
                ProtCidSettings.logWriter.WriteLine(pfamId + "  " + ligandId + " no domain files. something wrong.");
                return null;
            }

            try
            {
                CopyDomainFilesToFolder(coordDomains, tarTempDir, pfamDomainPymolFileDir);
                pymolScriptFiles = WritePfamLigandAlignPymolScriptFiles(groupFileName, coordDomains, entryDomainBestCoordInPfam, pfamBestStructDomainDict,
                                            pfamDomainPymolFileDir, chainDomainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainFileChainMapHash,
                                            domainLigandChainDict, isDnaRna);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(pfamId + "  Compress domain files in a PFAM error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="domainSeleLigandsHash">the ligand chain (not the asymmetric chain in ASU) and its seq id in a domain file</param>
        /// <param name="pymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <returns></returns>
        public string ReWritePymolScriptFile(string[] coordDomains, Dictionary<string, string[]> domainSeleLigandsHash, string pymolScriptFile, string newPymolScriptFile)
        {
            StreamWriter dataWriter = new StreamWriter(newPymolScriptFile);
            StreamReader dataReader = new StreamReader(pymolScriptFile);
            string line = "";
            string coordDomain = "";
            bool isDomainNeeded = true;
            bool isCenterDomain = false;
            string pymolSelectString = "";
            List<string> coordDomainListInScript = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    isDomainNeeded = false;
                    string[] fields = line.Split(' ');
                    coordDomain = fields[1].Replace(".pfam", "").Trim();
                    if (!isCenterDomain)
                    {
                        isCenterDomain = true;
                        isDomainNeeded = true;
                        coordDomainListInScript.Add(coordDomain);
                    }
                    else
                    {
                        if (coordDomains.Contains(coordDomain))
                        {
                            isDomainNeeded = true;
                            coordDomainListInScript.Add(coordDomain);
                        }
                    }
                    if (isDomainNeeded)
                    {
                        if (domainSeleLigandsHash.ContainsKey(coordDomain))
                        {
                            string[] ligands = (string[])domainSeleLigandsHash[coordDomain];
                            foreach (string ligand in ligands)
                            {
                                string[] chainSeqFields = ligand.Split('_');
                                pymolSelectString += (coordDomain + " and chain " + chainSeqFields[0] + " and resi " + chainSeqFields[1] + " + ");
                            }
                        }
                    }
                }
                if (line.IndexOf("center") > -1)
                {
                    isDomainNeeded = true;
                }
                if (isDomainNeeded)
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataWriter.WriteLine("hide spheres, allhet");
            dataWriter.WriteLine("sele seleLigands, " + pymolSelectString);
            dataWriter.WriteLine("show spheres, seleLigands");
            dataReader.Close();
            dataWriter.Close();
            FileInfo fileInfo = new FileInfo(newPymolScriptFile);
            return fileInfo.Name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="thisPymolScriptFileName"></param>
        /// <param name="pfamAcc"></param>
        /// <param name="pfamId"></param>
        /// <param name="ligandId"></param>
        /// <returns></returns>
        private string GetNewPymolScriptFileName(string thisPymolScriptFileName, string pfamAcc, string pfamId, string ligandId)
        {
            string newPymolScriptFileName = thisPymolScriptFileName.Replace(pfamAcc, pfamId + "_" + ligandId);
            return newPymolScriptFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="bestDomain"></param>
        /// <returns></returns>
        private bool IsPdbPymolCenterWithLigand(string pymolScriptFile, string bestDomain)
        {
            StreamReader dataReader = new StreamReader (pymolScriptFile);
            string line = "";
            bool isFirst = false;
            bool centerExist = false;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (isFirst && line.IndexOf("load") > -1)
                {
                    isFirst = false;
                    if (line.IndexOf (bestDomain) > -1)
                    {
                        centerExist = true;
                        break;
                    }
                }
            }
            dataReader.Close();
            return centerExist;
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
        /// <param name="domainLigandChainHash">the coord domain, value: the list of ligand chains and their seq Ids</param>
        /// <returns></returns>
        public string[] WritePfamLigandAlignPymolScriptFiles(string groupFileName, string[] coordDomains, string centerDomain, Dictionary<string, string> pfamBestStructChainDomainHash,
                                       string fileDataDir, Dictionary<string, Range[]> domainRangesHash, DataTable pfamDomainTable,
                                       Dictionary<string, int[]> domainCoordSeqIdsHash, Dictionary<string, string[]> domainFileChainMapHash, Dictionary<string, string[]> domainLigandChainDict, bool isDnaRna)
        {
            if (!coordDomains.Contains(centerDomain)) // somehow, the domain file for center domain not exist, then use the first available domain file
            {
                centerDomain = coordDomains[0];
            }
            Dictionary<string, string[]>[] domainInteractingLigandsHashes = null;
            if (isDnaRna)
            {
                domainInteractingLigandsHashes = GetChainDomainInteractingDnaRnasHash(coordDomains, domainLigandChainDict, domainFileChainMapHash);
            }
            else
            {
                domainInteractingLigandsHashes = GetChainDomainInteractingLigandsHash(coordDomains, domainLigandChainDict, domainFileChainMapHash);
            }
            Dictionary<string, string[]> domainInteractingLigandsHash = domainInteractingLigandsHashes[0];
            Dictionary<string, string[]> domainInteractingDnaRnaHash = domainInteractingLigandsHashes[1];

            string[] ligandPymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, coordDomains, centerDomain, fileDataDir,
                domainRangesHash, pfamDomainTable, domainCoordSeqIdsHash, domainInteractingLigandsHash, domainInteractingDnaRnaHash, null);
            return ligandPymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomains"></param>
        /// <param name="domainLigandChainHash">the domain and its interacting ligand chains and seq ids</param>
        /// <param name="domainFileChainMapHash"></param>
        /// <returns></returns>
        public Dictionary<string, string[]>[] GetChainDomainInteractingLigandsHash(string[] chainDomains, Dictionary<string, string[]> domainLigandChainDict, Dictionary<string, string[]> domainFileChainMapHash)
        {
            Dictionary<string, string[]> domainInteractingLigandsHash = new Dictionary<string, string[]>();
            Dictionary<string, string[]> domainInteractingDnaRnaHash = new Dictionary<string, string[]>();
            string pdbId = "";
            int chainDomainId = 0;
            foreach (string chainDomain in chainDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                string[] interactingNoProtChains = domainLigandChainDict[chainDomain];
                string[] interactingDnaRnaChains = GetInteractingDnaRnaChains(pdbId, interactingNoProtChains);
                List<string> interactingLigandList = new List<string>();
                foreach (string noprotChain in interactingNoProtChains)
                {
                    if (interactingDnaRnaChains.Contains(noprotChain))
                    {
                        continue;
                    }
                    interactingLigandList.Add(noprotChain);
                }
                string[] interactingLigands = new string[interactingLigandList.Count];
                interactingLigandList.CopyTo(interactingLigands);

                string[] fileChainMap = domainFileChainMapHash[chainDomain];
                // match the ligand chains to the chain ids in the file
                // use file chain ids for the pymol sessions.
                if (interactingLigands.Length > 0)
                {
                    string[] ligandFileChains = MapAsymmetricChainsToFileChains(interactingLigands, fileChainMap);
                    domainInteractingLigandsHash.Add(chainDomain, ligandFileChains);
                }

                if (interactingDnaRnaChains.Length > 0)
                {
                    string[] fileDnaRnaChains = MapAsymmetricChainsToFileChains(interactingDnaRnaChains, fileChainMap);
                    domainInteractingDnaRnaHash.Add(chainDomain, fileDnaRnaChains);
                }
            }
            Dictionary<string, string[]>[] interactingNoProtChainHashes = new Dictionary<string, string[]>[2];
            interactingNoProtChainHashes[0] = domainInteractingLigandsHash;
            interactingNoProtChainHashes[1] = domainInteractingDnaRnaHash;
            return interactingNoProtChainHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomains"></param>
        /// <param name="domainLigandChainHash">the domain and its interacting ligand chains and seq ids</param>
        /// <param name="domainFileChainMapHash">domains map to chains in the file, this is critical when display in PyMOL</param>
        /// <returns></returns>
        public Dictionary<string, string[]>[] GetChainDomainInteractingDnaRnasHash(string[] chainDomains, Dictionary<string, string[]> domainLigandChainDict, Dictionary<string, string[]> domainFileChainMapHash)
        {
            Dictionary<string, string[]> domainInteractingLigandsHash = new Dictionary<string,string[]> ();
            Dictionary<string, string[]> domainInteractingDnaRnaHash = new Dictionary<string,string[]> ();
            string pdbId = "";
            int chainDomainId = 0;
            string domainSymmetryString = "";
            foreach (string chainDomain in chainDomains)
            {
                pdbId = chainDomain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                domainSymmetryString = GetDomainSymmetryString(chainDomain, domainFileChainMapHash);

                try
                {
                    string[] interactingNoProtChains = domainLigandChainDict[chainDomain + "_" + domainSymmetryString]; // also with symmetry string
                    string[] interactingDnaRnaChains = GetInteractingDnaRnaChains(pdbId, interactingNoProtChains);
                    List<string> interactingLigandList = new List<string>();
                    foreach (string noprotChain in interactingNoProtChains)
                    {
                        if (interactingDnaRnaChains.Contains(noprotChain))
                        {
                            continue;
                        }
                        interactingLigandList.Add(noprotChain);
                    }
                    string[] interactingLigands = new string[interactingLigandList.Count];
                    interactingLigandList.CopyTo(interactingLigands);

                    string[] fileChainMap = (string[])domainFileChainMapHash[chainDomain];
                    // match the ligand chains to the chain ids in the file
                    // use file chain ids for the pymol sessions.
                    if (interactingLigands.Length > 0)
                    {
                        string[] ligandFileChains = MapAsymmetricChainsToFileChains(interactingLigands, fileChainMap);
                        domainInteractingLigandsHash.Add(chainDomain, ligandFileChains);
                    }

                    if (interactingDnaRnaChains.Length > 0)
                    {
                        string[] fileDnaRnaChains = MapAsymmetricChainsToFileChains(interactingDnaRnaChains, fileChainMap);
                        domainInteractingDnaRnaHash.Add(chainDomain, fileDnaRnaChains);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(chainDomain + " Get Interacting DNA/RNA error: " + ex.Message);
                }
            }
            Dictionary<string, string[]>[] interactingNoProtChainHashes = new Dictionary<string, string[]>[2];
            interactingNoProtChainHashes[0] = domainInteractingLigandsHash;
            interactingNoProtChainHashes[1] = domainInteractingDnaRnaHash;
            return interactingNoProtChainHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomain"></param>
        /// <param name="domainLigandChainHash"></param>
        /// <param name="domainFileChainMapHash"></param>
        /// <returns></returns>
        private string GetDomainSymmetryString(string chainDomain, Dictionary<string, string[]> domainFileChainMapHash)
        {

            string[] fileChainMap = domainFileChainMapHash[chainDomain];
            string[] asymChains = fileChainMap[0].Split(',');  // since the protein domain is always as "A", the first one in the file
            int symOpIndex = asymChains[0].IndexOf("_");
            string symOpString = "";
            if (symOpIndex > -1)
            {
                symOpString = asymChains[0].Substring(symOpIndex + 1, asymChains[0].Length - symOpIndex - 1);
            }
            return symOpString;
        }
        #endregion

        #region ligand-pfam db info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandId"></param>
        /// <returns></returns>
        public string[] GetLigandInteractingPfams(string ligandId)
        {
            string queryString = "";
            if (ligandId == "DNA" || ligandId == "RNA")
            {
                queryString = string.Format("Select Distinct PfamID From PfamDnaRnas, PdbLigands " +
                    " Where PdbLigands.Ligand = '{0}' AND PdbLigands.PdbID = PfamDnaRnas.PdbID AND " +
                    " PdbLigands.AsymChain = PfamDnaRnas.DnaRnaChain;", ligandId);
            }
            else
            {
                queryString = string.Format("Select Distinct PfamID From PfamLigands, PdbLigands " +
                     " Where PdbLigands.Ligand = '{0}' AND PdbLigands.PdbID = PfamLigands.PdbID AND " +
                     " PdbLigands.AsymChain = PfamLigands.LigandChain AND PdbLigands.SeqID = PfamLigands.LigandSeqID;", ligandId);
            }
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamIds[count] = pfamIdRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandId"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        public DataTable SelectLigandInteractingDomainTable(string pfamId, DataTable ligandPfamInteractionTable)
        {
            string queryString = string.Format("Select PdbPfam.PdbID, PdbPfam.EntityID, PdbPfam.DomainID, SeqStart, SeqEnd, " +
            " AlignStart, AlignEnd, HmmStart, HmmEnd, QueryAlignment, HmmAlignment, AsymChain, AuthChain, ChainDomainID " +
            " From PdbPfam, PdbPfamChain " + 
            " Where Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID " +
            " AND PdbPfam.DomainId = PdbPfamChain.DomainID " +
            " AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string pdbId = "";
         //   string protChain = "";
            int chainDomainId = 0;
            string coordDomain = "";
            DataTable ligandPfamDomainTable = pfamDomainTable.Clone();
            List<string> entryChainList = new List<string>();
            Dictionary<string, string[]> domainInterLigandHash = new Dictionary<string,string[]> ();
            foreach (DataRow chainRow in ligandPfamInteractionTable.Rows)
            {
                pdbId = chainRow["PdbID"].ToString();
         //       protChain = chainRow["AsymChain"].ToString().TrimEnd();
                chainDomainId = Convert.ToInt32(chainRow["ChainDomainID"].ToString ());
                coordDomain = pdbId + chainDomainId.ToString();
                if (entryChainList.Contains(pdbId + chainDomainId.ToString () ))
                {
                    continue;
                }
                entryChainList.Add(pdbId + chainDomainId.ToString());
                DataRow[] pfamChainRows = pfamDomainTable.Select(string.Format("PdbID = '{0}' AND ChainDomainID = '{1}'", pdbId, chainDomainId));
                foreach (DataRow pfamChainRow in pfamChainRows)
                {
                    DataRow newPfamChainRow = ligandPfamDomainTable.NewRow();
                    newPfamChainRow.ItemArray = pfamChainRow.ItemArray;
                    ligandPfamDomainTable.Rows.Add(newPfamChainRow);
                }
            }
            return ligandPfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandPfamInterTable"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetDomainLigandChainHash(DataTable ligandPfamInterTable)
        {
            Dictionary<string, string[]> domainLigandChainDict = new Dictionary<string, string[]>();
            Dictionary<string, List<string>> domainLigandChainHash = new Dictionary<string, List<string>>();
            string coordDomain = "";
            string ligandChain = "";
            foreach (DataRow lpInterRow in ligandPfamInterTable.Rows)
            {
                coordDomain = lpInterRow["PdbID"].ToString() + lpInterRow["ChainDomainId"].ToString();
                ligandChain = lpInterRow["LigandChain"].ToString ().TrimEnd ();
                if (domainLigandChainHash.ContainsKey(coordDomain))
                {
                    domainLigandChainHash[coordDomain].Add(ligandChain);
                }
                else
                {
                    List<string> ligandChainSeqList = new List<string> ();
                    ligandChainSeqList.Add(ligandChain);
                    domainLigandChainHash.Add(coordDomain, ligandChainSeqList);
                }
            }
            foreach (string lsCoordDomain in domainLigandChainHash.Keys)
            {
                domainLigandChainDict[lsCoordDomain] = domainLigandChainHash[lsCoordDomain].ToArray ();
            }
            return domainLigandChainDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandPfamInterTable"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetDomainDnaRnaChainHash(DataTable dnaRnaPfamInterTable)
        {
            Dictionary<string, List<string>> domainLigandChainHash = new Dictionary<string, List<string>>();
            string coordDomain = "";
            string ligandChain = "";
            foreach (DataRow lpInterRow in dnaRnaPfamInterTable.Rows)
            {
                coordDomain = lpInterRow["PdbID"].ToString() + lpInterRow["ChainDomainId"].ToString() + "_" + lpInterRow["SymmetryString"].ToString ().TrimEnd ();
                ligandChain = lpInterRow["LigandChain"].ToString().TrimEnd() + "_" + lpInterRow["LigandSymmetryString"].ToString ().TrimEnd ();
                if (domainLigandChainHash.ContainsKey(coordDomain))
                {
                    if (!domainLigandChainHash[coordDomain].Contains(ligandChain))
                    {
                        domainLigandChainHash[coordDomain].Add(ligandChain);
                    }
                }
                else
                {
                    List<string> ligandChainSeqList = new List<string> ();
                    ligandChainSeqList.Add(ligandChain);
                    domainLigandChainHash.Add(coordDomain, ligandChainSeqList);
                }
            }
            Dictionary<string, string[]> domainLigandChainDict = new Dictionary<string, string[]>();
            foreach (string lsCoordDomain in domainLigandChainHash.Keys)
            {
                domainLigandChainDict[lsCoordDomain] = domainLigandChainHash[lsCoordDomain].ToArray ();
            }
            return domainLigandChainDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandId"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public DataTable GetLigandPfamInteractionTable(string ligandId, string pfamId)
        {
            string queryString = "";
            if (ligandId == "DNA" || ligandId == "RNA")
            {
                queryString = string.Format("Select Distinct PfamDnaRnas.PdbID, PfamDnaRnas.AsymChain, PfamDnaRnas.ChainDomainID, SymmetryString, " +
                    " PfamDnaRnas.DnaRnaChain As LigandChain, PfamDnaRnas.DnaRnaSeqID As LigandSeqID, DnaRnaSymmetryString As LigandSymmetryString " +
                    " From PfamDnaRnas, PdbLigands " +
                    " WHere PfamDnaRnas.PfamID = '{0}' AND PdbLigands.Ligand = '{1}' AND " +
                    " PfamDnaRnas.PdbID = PdbLigands.PdbID AND PfamDnaRnas.DnaRnaChain = PdbLigands.AsymChain;", pfamId, ligandId);
            }
            else
            {
                queryString = string.Format("Select Distinct PfamLigands.PdbID, PfamLigands.AsymChain, PfamLigands.ChainDomainID, " + 
                    " PfamLigands.LigandChain, PfamLigands.LigandSeqID " +
                    " From PfamLigands, PdbLigands " +
                    " WHere PfamLigands.PfamID = '{0}' AND PdbLigands.Ligand = '{1}' AND " +
                    " PfamLigands.PdbID = PdbLigands.PdbID AND PfamLigands.LigandChain = PdbLigands.AsymChain AND " +
                    " PfamLigands.LigandSeqID = PdbLigands.SeqID;", pfamId, ligandId);
            }
            DataTable ligandPfamInteractionTable = ProtCidSettings.protcidQuery.Query( queryString);
            return ligandPfamInteractionTable;
        }
        #endregion

        #region read domain coord info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamBestStructChainDomainHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        private string[] ReadDomainCoordChainInfo(Dictionary<string, string> pfamBestStructChainDomainDict, DataTable pfamDomainTable,
            ref Dictionary<string, int[]> fileCoordSeqIdHash, ref Dictionary<string, string[]> chainDomainChainMapHash, string domainFileDir)
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
                    chainDomainFile = WriteDomainChainWithLigandFile(chainDomainRows, domainFileDir, ref fileCoordSeqIdHash, ref chainDomainChainMapHash);
                    if (chainDomainFile != "")
                    {
                        chainDomainFileList.Add(chainDomainFile);
                    }
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
        #endregion

        #region compress domain-DNA/RNA files
        /// <summary>
        /// 
        /// </summary>
        public void CompressPfamDnaRnaFiles()
        {
            string[] ligandIds = {"DNA" , "RNA"};
       //     DataTable asuTable = GetAsuSeqInfoTable();

            foreach (string ligandId in ligandIds)
            {
                CompressDomainDnaRnaFiles(ligandId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dnaRna"></param>
        /// <param name="asuTable"></param>
        public void CompressDomainDnaRnaFiles(string dnaRna)
        {
            if (File.Exists(Path.Combine(pfamDomainPymolFileDir, "ligand\\" + dnaRna + ".tar.gz")))
            {
                return;
            }

            string[] pfamIds = GetLigandInteractingPfams(dnaRna);
          //  string[] pfamIds = {"GATA", "HTH_38", "ParB", "Pfam-B_7018"};
            List<string> coordDomainList = new List<string>();
            List<string> pymolScriptFileList = new List<string>();

            foreach (string pfamId in pfamIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;               
                // for pymol script files and coordinate domain files
                CompressDnaRnaPfamDomainAlignPymolScript(dnaRna, pfamId, ref coordDomainList, ref pymolScriptFileList);
            }
            string[] coordDomains = new string[coordDomainList.Count];
            coordDomainList.CopyTo(coordDomains);
            string[] pymolScriptFiles = new string[pymolScriptFileList.Count];
            pymolScriptFileList.CopyTo(pymolScriptFiles);
            string groupFileName = dnaRna;
            // tar and compress all the pymol script files and domain files, tar file name is the 2,3-letter code for ligand in the pdb
            MoveDomainFilesToDest(coordDomains, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
            MovePymolScriptFiles(pymolScriptFiles, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
            CompressGroupPfamDomainFiles(coordDomains, pymolScriptFiles, groupFileName, pfamDomainPymolFileDir, Path.Combine(pfamDomainPymolFileDir, "ligand"));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dnaRna"></param>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        /// <param name="coordDomainList"></param>
        /// <param name="pymolScriptFileList"></param>
        public void CompressDnaRnaPfamDomainAlignPymolScript(string dnaRna, string pfamId, ref List<string> coordDomainList, ref List<string> pymolScriptFileList)
        {
            DataTable dnaRnaPfamInteractionTable = GetLigandPfamInteractionTable(dnaRna, pfamId);
            // the pfam domain assignments for those chains interacting with ligand 
            DataTable dnaRnaInterPfamDomainTable = SelectLigandInteractingDomainTable(pfamId, dnaRnaPfamInteractionTable);

            // key: coordDomain, value: the list of the ligand chains and their seqid
            Dictionary<string, string[]> domainDnaRnaChainDict = GetDomainDnaRnaChainHash (dnaRnaPfamInteractionTable);  // with symmetry String
            string srcPfamDnaRnaFile = "";
            string destPfamDnaRnaFile = "";

            if (dnaRnaInterPfamDomainTable.Rows.Count == 0)
            {
                return;
            }

            try
            {
                string entryDomainBestCoordInPfam = "";
                Dictionary<string, string> pfamBestStructChainDomainDict = GetPfamBestHmmChainDomainIdHash (dnaRnaInterPfamDomainTable, out entryDomainBestCoordInPfam);
                string[] coordDomains = GetPfamEntryChainDomains(pfamBestStructChainDomainDict);
                foreach (string coordDomain in coordDomains)
                {
                    if (!coordDomainList.Contains(coordDomain))
                    {
                        coordDomainList.Add(coordDomain);
                        srcPfamDnaRnaFile = Path.Combine(pfamDnaRnaDir, coordDomain + ".pfam");
                        destPfamDnaRnaFile = Path.Combine(pfamDomainPymolFileDir, coordDomain + ".pfam");
                        File.Copy(srcPfamDnaRnaFile, destPfamDnaRnaFile, true);
                    }
                }

                string[] pfamLigandPymolScriptFiles = CompileLigandDomainAlignPymolScriptFiles(pfamId, dnaRna, pfamBestStructChainDomainDict,
                    dnaRnaInterPfamDomainTable, entryDomainBestCoordInPfam, domainDnaRnaChainDict, pfamDomainPymolFileDir, true);  // true for DNA/RNA
                if (pfamLigandPymolScriptFiles != null)
                {
                    pymolScriptFileList.AddRange(pfamLigandPymolScriptFiles);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress " + pfamId + "  " + dnaRna + " domain align files errors: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandInterPfamDomainTable"></param>
        /// <param name="entryDomainBestHmmInPfam"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamBestHmmChainDomainIdHash(DataTable ligandInterPfamDomainTable, out string entryDomainBestHmmInPfam)
        {
            string[] entryChainDomainIds = GetEntryChainDomainIds(ligandInterPfamDomainTable);
            Dictionary<string, string> entryBestHmmChainDomainHash = new Dictionary<string, string>();
            Dictionary<string, int> entryBestHmmNumCoordHash = new Dictionary<string, int>();
            string pdbId = "";
            int chainDomainId = 0;
            int maxNumOfHmmInPfam = 0;
            int numOfHmm = 0;
            entryDomainBestHmmInPfam = entryChainDomainIds[0];
            foreach (string entryChainDomainId in entryChainDomainIds)
            {
                pdbId = entryChainDomainId.Substring(0, 4);
                chainDomainId = Convert.ToInt32(entryChainDomainId.Substring(4, entryChainDomainId.Length - 4));
                numOfHmm = GetNumOfInteractingHmmSites(pdbId, chainDomainId);
                if (maxNumOfHmmInPfam < numOfHmm)
                {
                    maxNumOfHmmInPfam = numOfHmm;
                    entryDomainBestHmmInPfam = entryChainDomainId;
                }
                if (entryBestHmmChainDomainHash.ContainsKey(pdbId))
                {
                    int maxNumOfHmm = (int)entryBestHmmNumCoordHash[pdbId];
                    if (maxNumOfHmm < numOfHmm)
                    {
                        entryBestHmmNumCoordHash[pdbId] = numOfHmm;
                        entryBestHmmChainDomainHash[pdbId] = entryChainDomainId;
                    }
                }
                else
                {
                    entryBestHmmChainDomainHash.Add(pdbId, entryChainDomainId);
                    entryBestHmmNumCoordHash.Add(pdbId, numOfHmm);
                }
            }
            return entryBestHmmChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int GetNumOfInteractingHmmSites(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select * From PfamDnaRnas Where PdbID = '{0}' AND ChainDomainId = {1};", pdbId, chainDomainId);
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> buChainDomainList = new List<string>();
            string buId = "";
            string symOp = "";
            string buChainDomainSymOp = "";
            foreach (DataRow pfamDnaRnaRow in pfamDnaRnaTable.Rows)
            {
                buId = pfamDnaRnaRow["BuID"].ToString();
                symOp = pfamDnaRnaRow["SymmetryString"].ToString().TrimEnd();
                buChainDomainSymOp = buId + "_" + chainDomainId + "_" + symOp;
                if (!buChainDomainList.Contains(buChainDomainSymOp))
                {
                    buChainDomainList.Add(buChainDomainSymOp);
                }
            }
            int numOfHmmSites = 0;
            int maxNumOfHmmSites = 0;
            foreach (string buChainDomain in buChainDomainList)
            {
                string[] chainDomainFields = buChainDomain.Split('_');
                DataRow[] chainDomainRows = pfamDnaRnaTable.Select(string.Format ("BuID = '{0}' AND SymmetryString = '{1}'", buId, symOp));
                numOfHmmSites = GetInteractingHmmSites(chainDomainRows);
                if (maxNumOfHmmSites < numOfHmmSites)
                {
                    maxNumOfHmmSites = numOfHmmSites;
                }
            }
            return maxNumOfHmmSites;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainHmmRows"></param>
        /// <returns></returns>
        private int GetInteractingHmmSites(DataRow[] domainHmmRows)
        {
            int hmmSeqId = 0;
            List<int> hmmSiteList = new List<int>();
            foreach (DataRow domainHmmRow in domainHmmRows)
            {
                hmmSeqId = Convert.ToInt32 (domainHmmRow["HmmSeqID"].ToString());
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }
            return hmmSiteList.Count;
        }
        #endregion
        
    }
}
