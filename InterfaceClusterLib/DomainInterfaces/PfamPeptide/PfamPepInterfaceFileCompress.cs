using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using InterfaceClusterLib.DomainInterfaces;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPepInterfaceFileCompress : PfamDomainFileCompress
    {
        private double simHmmSitesPercent = 0.80;
        private string pfamPepAlignFileDir = "";
        private string domainInterfaceFileDir = "";

        /// <summary>
        /// pymol sessions for Pfam-peptide interfaces, 
        /// aligned by pfam domains, and peptides are not aligned
        /// </summary>
        public void CompressPfamPeptideInterfaces()
        {
            domainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
            pfamPepAlignFileDir = Path.Combine(pfamDomainPymolFileDir, "peptide");
            if (!Directory.Exists(pfamPepAlignFileDir))
            {
                Directory.CreateDirectory(pfamPepAlignFileDir);
            }

            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Align pfam domains which are interacting with peptides");
            ProtCidSettings.progressInfo.totalOperationNum = pfamIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamIdTable.Rows.Count;

            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    CompressPfamPepInterfaces(pfamId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + ": compile pfam-peptide pymol sessions error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + ": compile pfam-peptide pymol sessions error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// pymol sessions for Pfam-peptide interfaces, 
        /// aligned by pfam domains, and peptides are not aligned
        /// </summary>
        /// <param name="updatePfams"></param>
        public void UpdatePfamPeptideInterfaces(string[] updatePfams)
        {
            domainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
            pfamPepAlignFileDir = Path.Combine(pfamDomainPymolFileDir, "peptide");
            if (! Directory.Exists (pfamPepAlignFileDir))
            {
                Directory.CreateDirectory(pfamPepAlignFileDir);
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Align pfam domains which are interacting with peptides");
            ProtCidSettings.progressInfo.totalOperationNum = updatePfams.Length;
            ProtCidSettings.progressInfo.totalStepNum = updatePfams.Length;

            foreach (string pfamId in updatePfams)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    string pfamPepAlignFile = Path.Combine(pfamPepAlignFileDir, pfamId + "_pep.tar.gz");
                    if (File.Exists(pfamPepAlignFile))
                    {
                        File.Delete (pfamPepAlignFile);
                    }

                    CompressPfamPepInterfaces(pfamId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + ": compile pfam-peptide pymol sessions error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + ": compile pfam-peptide pymol sessions error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void CompressPfamPepInterfaces(string pfamId)
        {
            string[] pfamPepInterfaces = GetPfamPepInterfaces(pfamId);
            if (pfamPepInterfaces.Length == 0)
            {
                ProtCidSettings.logWriter.WriteLine(pfamId + " no peptide interfaces.");
                ProtCidSettings.logWriter.Flush();
                return;
            }
            string pfamPepAlignFileName =  pfamId;
            string pfamPepAlignFile = Path.Combine(pfamPepAlignFileDir, pfamPepAlignFileName + "_pep.tar.gz");
            if (File.Exists(pfamPepAlignFile))
            {
                return;
            }
            CompressPfamPeptideFiles(pfamId, pfamPepInterfaces, pfamPepAlignFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="asuTable"></param>
        /// <param name="groupFileName"></param>
        private void CompressPfamPeptideFiles(string pfamId, string[] pfamPepInterfaces, string groupFileName)
        {
            string queryString = string.Format("Select PdbPfam.PdbID, PdbPfam.EntityID, PdbPfam.DomainID, SeqStart, SeqEnd, AlignStart, AlignEnd, " +
               " HmmStart, HmmEnd, QueryAlignment, HmmAlignment, AsymChain, AuthChain, ChainDomainID " +
               " From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND PdbPfam.DomainId = PdbPfamChain.DomainID " +
               " AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            if (pfamDomainTable.Rows.Count == 0)
            {
                return;
            }
            string[] coordPfamPepInterfaces = CopyPfamPepInterfaces(pfamPepInterfaces, domainInterfaceFileDir, pfamPepAlignFileDir);
            pfamPepInterfaces = coordPfamPepInterfaces;   // only use those with coordinates

            string centerInterface = pfamPepInterfaces[0];
            Dictionary<string, int[]> domainCoordSeqIdsHash = ReadPepInterfaceDomainCoordInfo (pfamPepInterfaces);

            DataTable pfamPepInterfaceTable = GetSubPfamPepInterfaceTable (pfamId, pfamPepInterfaces);

            Dictionary<string, Range[]> interfaceDomainRangesHash = GetPepInterfaceDomainRangesHash(pfamPepInterfaces, pfamPepInterfaceTable, pfamDomainTable);

            Dictionary<string, Dictionary<int, int>> interfaceDomainHmmSeqIdHash = GetDomainHmmSeqIdsHash(pfamPepInterfaces, pfamDomainTable, pfamPepInterfaceTable, domainCoordSeqIdsHash);

       //     Hashtable domainFileChainMapHash = GetPepInterfaceChainMap(pfamPepInterfaces, pfamPepInterfaceTable); // the map between chain Ids and asymmtric chain ids in the domain file
            try
            {
                CompressDomainCoordFiles(groupFileName, pfamPepInterfaces, centerInterface, interfaceDomainRangesHash, interfaceDomainHmmSeqIdHash);
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
        /// <param name="pfamPepInterfaces"></param>
        /// <param name="srcFileDir"></param>
        /// <param name="destFileDir"></param>
        private string[] CopyPfamPepInterfaces(string[] pfamPepInterfaces, string srcFileDir, string destFileDir)
        {
            string srcFile = "";
            string destFile = "";
            string pepInterfaceFile = "";
            List<string> coordPepInterfaceFileList = new List<string> ();
            foreach (string pepInterface in pfamPepInterfaces)
            {
                pepInterfaceFile = Path.Combine (destFileDir, pepInterface + ".cryst");
                if (File.Exists(pepInterfaceFile))
                {
                    coordPepInterfaceFileList.Add(pepInterface);
                    continue;
                }
                srcFile = Path.Combine(srcFileDir, pepInterface.Substring (1, 2) + "\\" + pepInterface + ".cryst.gz");
                destFile = Path.Combine(destFileDir, pepInterface + ".cryst.gz");

                if (File.Exists(srcFile))
                {
                    File.Copy(srcFile, destFile, true);
                    ParseHelper.UnZipFile(destFile);
                    coordPepInterfaceFileList.Add(pepInterface);
                }
            }
            return coordPepInterfaceFileList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFileName"></param>
        /// <param name="pepInterfaces"></param>
        /// <param name="centerPepInterface"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="domainRangesHash"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="domainCoordSeqIdsHash"></param>
        /// <param name="domainFileChainMapHash"></param>
        /// <param name="dataType"></param>
        public void CompressDomainCoordFiles(string groupFileName, string[] pepInterfaces, string centerPepInterface, Dictionary<string, Range[]> domainRangesHash, Dictionary<string, Dictionary<int, int>> domainHmmSeqIdHash)
        {
            if (!pepInterfaces.Contains(centerPepInterface)) 
            {
                centerPepInterface = pepInterfaces[0];
            }

            string[] pymolScriptFiles = domainAlignPymolScript.FormatPymolScriptFile(groupFileName, pepInterfaces, centerPepInterface, pfamPepAlignFileDir,
                          domainRangesHash, domainHmmSeqIdHash, "cryst");

            string tarGroupFileName = groupFileName + "_pep";

            CompressGroupPfamDomainPeptideFiles(pepInterfaces, pymolScriptFiles, tarGroupFileName, pfamPepAlignFileDir);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <param name="pymolScriptFiles"></param>
        /// <param name="groupFileName"></param>
        /// <param name="fileDataDir"></param>
        /// <param name="dataType"></param>
        public void CompressGroupPfamDomainPeptideFiles(string[] coordDomains, string[] pymolScriptFiles, string groupFileName, string srcDataDir)
        {
            string tarGroupFileName = groupFileName + ".tar.gz";

            string[] domainCoordFiles = new string[coordDomains.Length];
            for (int i = 0; i < coordDomains.Length; i++)
            {
                domainCoordFiles[i] = coordDomains[i] + ".cryst";
            }
            string[] filesToBeCompressed = new string[domainCoordFiles.Length + pymolScriptFiles.Length];
            Array.Copy(domainCoordFiles, 0, filesToBeCompressed, 0, domainCoordFiles.Length);
            Array.Copy(pymolScriptFiles, 0, filesToBeCompressed, domainCoordFiles.Length, pymolScriptFiles.Length);

            string tarFileName = fileCompress.RunTar(tarGroupFileName, filesToBeCompressed, pfamPepAlignFileDir, true);

            if (filesToBeCompressed.Length > fileCompress.maxNumOfFiles)
            {
                string fileFolder = Path.Combine(pfamPepAlignFileDir, groupFileName);
                Directory.Delete(fileFolder, true);
            }
            else
            {
                foreach (string domainFile in filesToBeCompressed)
                {
                    File.Delete(Path.Combine(pfamPepAlignFileDir, domainFile));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterfaces"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        private Dictionary<string, Range[]> GetPepInterfaceDomainRangesHash (string[] pepInterfaces, DataTable pfamPepInterfaceTable, DataTable pfamDomainTable)
        {
            Dictionary<string, Range[]> pepInterfaceDomainRangeHash = new Dictionary<string, Range[]>();
            string pdbId = "";
            int pepInterfaceId = 0;
            int chainDomainId = 0;
            foreach (string pepInterface in pepInterfaces)
            {
                string[] pepInterfaceFields = pepInterface.Split('_');
                pdbId = pepInterfaceFields[0];
                pepInterfaceId = Convert.ToInt32(pepInterfaceFields[1].Remove (0, 1));
                DataRow[] pepInterfaceRows = pfamPepInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, pepInterfaceId));
                chainDomainId = Convert.ToInt32(pepInterfaceRows[0]["ChainDomainId"].ToString ());
                Range[] domainRanges = domainAlignPymolScript.GetDomainRanges(pdbId, chainDomainId, pfamDomainTable);
                pepInterfaceDomainRangeHash.Add(pepInterface, domainRanges);
            }
            return pepInterfaceDomainRangeHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="domainDefRows"></param>
        private Dictionary<string, int[]> ReadPepInterfaceDomainCoordInfo(string[] pepInterfaces)
        {
            string peptideInterfaceFile = "";
            Dictionary<string, int[]> pepInterfaceCoordHash = new Dictionary<string,int[]>();
            foreach (string pepInterface in pepInterfaces)
            {
                peptideInterfaceFile = Path.Combine(pfamPepAlignFileDir, pepInterface + ".cryst");
                if (File.Exists(peptideInterfaceFile))
                {
                    int[] seqInCoord = ReadCoordSeqIdsFromDomainFile(peptideInterfaceFile);
                    pepInterfaceCoordHash.Add(pepInterface, seqInCoord);
                }
            }
            return pepInterfaceCoordHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamPepInterfaces"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <param name="domainCoordSeqIdsHash"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, int>> GetDomainHmmSeqIdsHash(string[] pfamPepInterfaces, DataTable pfamDomainTable, DataTable pfamPepInterfaceTable, Dictionary<string, int[]> domainCoordSeqIdsHash)
        {
            Dictionary<string, Dictionary<int, int>> domainHmmSeqIdsHash = new Dictionary<string, Dictionary<int, int>>();
            string interfaceProtChainDomain = "";
            foreach (string pepInterface in pfamPepInterfaces)
            {
                interfaceProtChainDomain = GetPepInterfaceProtChainDomainId(pepInterface, pfamDomainTable, pfamPepInterfaceTable);
                int[] coordSeqIds = domainCoordSeqIdsHash[pepInterface];
                Dictionary<int, int> pepDomainHmmSeqIdHash = domainAlignPymolScript.GetSequenceHmmSeqIdHash (interfaceProtChainDomain, pfamDomainTable, coordSeqIds);
                domainHmmSeqIdsHash.Add(pepInterface, pepDomainHmmSeqIdHash);
            }
            return domainHmmSeqIdsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterface"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <returns></returns>
        private string GetPepInterfaceProtChainDomainId(string pepInterface, DataTable pfamDomainTable, DataTable pfamPepInterfaceTable)
        {
            string[] interfaceFields = pepInterface.Split('_');
            string pdbId = interfaceFields[0];
            int pepInterfaceId = Convert.ToInt32(interfaceFields[1].Remove (0, 1));

            DataRow[] pepInterfaceRows = pfamPepInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, pepInterfaceId));
            string interfaceProtChainDomainId = pdbId + pepInterfaceRows[0]["ChainDomainID"].ToString();
            return interfaceProtChainDomainId;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterfaces"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetPepInterfaceChainMap(string[] pepInterfaces, DataTable pfamPepInterfaceTable)
        {
            string pdbId = "";
            int pepInterfaceId = 0;
            string protChain = "";
            string pepChain = "";
            Dictionary<string, string[]> pepInterfaceChainMap = new Dictionary<string, string[]>();
            foreach (string pepInterface in pepInterfaces)
            {
                string[] pepInterfaceFields = pepInterface.Split('_');
                pdbId = pepInterfaceFields[0];
                pepInterfaceId = Convert.ToInt32 (pepInterfaceFields[1].Remove(0, 1));
                DataRow[] pepInterfaceRows = pfamPepInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceId = '{1}'", pdbId, pepInterfaceId));
                if (pepInterfaceRows.Length > 0)
                {
                    protChain = pepInterfaceRows[0]["AsymChain"].ToString().TrimEnd();
                    pepChain = pepInterfaceRows[0]["PepAsymChain"].ToString().TrimEnd();
                    string[] chainMap = new string[2];
                    chainMap[0] = protChain;
                    chainMap[1] = "A";
                    pepInterfaceChainMap.Add(pepInterface, chainMap);
                }
            }
            return pepInterfaceChainMap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamPepInterfaces"></param>
        /// <returns></returns>
        private DataTable GetSubPfamPepInterfaceTable(string pfamId, string[] pfamPepInterfaces)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable subPfamPepInterfaceTable = pfamPepInterfaceTable.Clone();
            string pdbId = "";
            int pepInterfaceId = 0;
            foreach (string pepInterface in pfamPepInterfaces)
            {
                string[] pepInterfaceFields = pepInterface.Split('_');
                pdbId = pepInterfaceFields[0];
                pepInterfaceId = Convert.ToInt32(pepInterfaceFields[1].Remove(0, 1));

                DataRow[] pepInterfaceRows = pfamPepInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, pepInterfaceId));
                foreach (DataRow pepInterfaceRow in pepInterfaceRows)
                {
                    DataRow newDataRow = subPfamPepInterfaceTable.NewRow();
                    newDataRow.ItemArray = pepInterfaceRow.ItemArray;
                    subPfamPepInterfaceTable.Rows.Add(newDataRow);
                }
            }
            return subPfamPepInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamPepInterfaces(string pfamId)
        {
            string queryString = string.Format("Select distinct pdbId From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> pfamPepInterfaceList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                int[] pepInterfaceIds = GetNonRedundantPeptideInterfaceIds(pfamId, pdbId);
                foreach (int pepInterfaceId in pepInterfaceIds)
                {
                    pfamPepInterfaceList.Add(pdbId + "_d" + pepInterfaceId.ToString ());
                }
            }
            pfamPepInterfaceList.Sort();
            return pfamPepInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetNonRedundantPeptideInterfaceIds(string pfamId, string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID From PfamPeptideHmmSites " +
                " Where PfamID = '{0}' AND PdbID = '{1}' Order By DomainInterfaceID;", pfamId, pdbId);
            DataTable pepDomainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * from PfamPeptideHmmSites Where PfamID = '{0}' AND PdbID = '{1}';", pfamId, pdbId);
            DataTable hmmSiteTable = ProtCidSettings.protcidQuery.Query( queryString);

            int pepDomainInterfaceId = 0;
            int[] pepDomainInterfaceIds = new int[pepDomainInterfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceIdRow in pepDomainInterfaceIdTable.Rows)
            {
                pepDomainInterfaceId = Convert.ToInt32(interfaceIdRow["DomainInterfaceID"].ToString());
                pepDomainInterfaceIds[count] = pepDomainInterfaceId;
                count++;
            }
            List<int> nonReduntDomainInterfaceIdList = new List<int> (pepDomainInterfaceIds);
            for (int i = 0; i < pepDomainInterfaceIds.Length; i++)
            {
                if (!nonReduntDomainInterfaceIdList.Contains(pepDomainInterfaceIds[i]))
                {
                    continue;
                }
                int[] hmmSitesI = GetInteractingHmmSites(hmmSiteTable, pepDomainInterfaceIds[i]);
                for (int j = i + 1; j < pepDomainInterfaceIds.Length; j++)
                {
                    if (!nonReduntDomainInterfaceIdList.Contains(pepDomainInterfaceIds[j]))
                    {
                        continue;
                    }
                    int[] hmmSitesJ = GetInteractingHmmSites(hmmSiteTable, pepDomainInterfaceIds[j]);

                    int removeIndex = GetDomainInterfaceToBeRemoved(hmmSitesI, hmmSitesJ);

                    if (removeIndex == 1)
                    {
                        nonReduntDomainInterfaceIdList.Remove(pepDomainInterfaceIds[i]);
                        break;
                    }
                    else if (removeIndex == 2)
                    {
                        nonReduntDomainInterfaceIdList.Remove(pepDomainInterfaceIds[j]);
                    }
                }
            }
        
            return nonReduntDomainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSitesI"></param>
        /// <param name="hmmSitesJ"></param>
        /// <returns></returns>
        private int GetDomainInterfaceToBeRemoved (int[] hmmSitesI, int[] hmmSitesJ)
        {
            List<int> sameHmmSiteList = new List<int> ();
            foreach (int hmmSiteI in hmmSitesI)
            {
                if (hmmSitesJ.Contains(hmmSiteI))
                {
                    sameHmmSiteList.Add(hmmSiteI);
                }
            }

            double samePercentI = (double)sameHmmSiteList.Count / (double)hmmSitesI.Length;
            double samePercentJ = (double)sameHmmSiteList.Count / (double)hmmSitesJ.Length;

            if (samePercentI > simHmmSitesPercent && samePercentJ > simHmmSitesPercent)
            {
                return 2;
            }
            else if (samePercentI > simHmmSitesPercent)
            {
                return 1;
            }
            else if (samePercentJ > simHmmSitesPercent)
            {
                return 2;
            }
            else
            {
                return 0;   // no remove
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryHmmSiteTable"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private int[] GetInteractingHmmSites(DataTable entryHmmSiteTable, int domainInterfaceId)
        {
            DataRow[] hmmSiteRows = entryHmmSiteTable.Select(string.Format ("DomainInterfaceID = '{0}'", domainInterfaceId));
            List<int> hmmSiteList = new List<int> ();
            int hmmSeqId = 0;
            foreach (DataRow hmmSiteRow in hmmSiteRows)
            {
                hmmSeqId = Convert.ToInt32(hmmSiteRow["HmmSeqId"].ToString ());
                if (!hmmSiteList.Contains(hmmSeqId))
                {
                    hmmSiteList.Add(hmmSeqId);
                }
            }

            return hmmSiteList.ToArray ();
        }
    }
}
