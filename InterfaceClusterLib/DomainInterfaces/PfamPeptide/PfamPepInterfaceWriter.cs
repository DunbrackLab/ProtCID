using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using DbLib;
using BuCompLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPepInterfaceWriter : DomainInterfaceWriter
    {
        private BiolUnitRetriever buReader = new BiolUnitRetriever();

        /// <summary>
        /// write domain-peptide interfaces to PDB files
        /// </summary>
        public void GeneratePfamPeptideInterfaceFiles()
        {
         //   string queryString = "Select Distinct PdbID From PfamPeptideInterfaces Where PepDomainID > -1;";
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate Domain-Peptide Interface Files");
            ProtCidSettings.progressInfo.totalOperationNum = pepEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pepEntryTable.Rows.Count;

            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    WriteDomainPeptideInterfaceFiles(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " generate domain-peptide interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamPeptideInterfaceFiles(string[] updateEntries)
        {
            if (updateEntries == null)
            {
                updateEntries = GetPepEntries();
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate Domain-Peptide Interface Files");
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    /* already deleted the previous ones when deleting the domain interface files
                     * */
                    WriteDomainPeptideInterfaceFiles(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " generate domain-peptide interface files error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        private string[] GetPepEntries ()
        {
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pdbIds = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbIds[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pdbIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void WriteDomainPeptideInterfaceFiles(string pdbId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable peptideInterfacesTable = ProtCidSettings.protcidQuery.Query( queryString);
            long[] multiChainDomainIds = GetEntryMultiChainDomainIds(pdbId);

            Dictionary<string, AtomInfo[]> asuChainHash = buReader.GetAsymUnitChainHash(pdbId);

            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            DataTable chainDomainTable = GetEntryChainDomainDefTable(pdbId);
            Dictionary<long, Range[]> domainRangeHash = GetDomainRangeHash (domainDefTable);

            int[] domainInterfaceIds = GetEntryDomainInterfaceIds(peptideInterfacesTable);
            long domainId = 0;
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                DataRow[] domainInterfaceRows = peptideInterfacesTable.Select(string.Format ("DomainInterfaceID = '{0}'", domainInterfaceId));
                domainId = Convert.ToInt64 (domainInterfaceRows[0]["DomainID"].ToString());

                if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
                {
                    try
                    {
                        WriteMultiChainDomainPeptideInterfaceFile(domainInterfaceRows[0], asuChainHash);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString () + " multi-chain domain peptide interface file gen error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " multi-chain domain peptide interface file gen error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
                else
                {
                    try
                    {
                        WriteSingleDomainPeptideInterfaceFile(domainInterfaceRows[0], asuChainHash, domainRangeHash);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString () + " single chain domain interface file gen error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " single chain domain interface file gen error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceRow"></param>
        /// <param name="asuChainHash"></param>
        private void WriteSingleDomainPeptideInterfaceFile(DataRow peptideInterfaceRow, Dictionary<string, AtomInfo[]> asuChainHash, Dictionary<long, Range[]> domainRangeHash)
        {
            string pdbId = peptideInterfaceRow["PdbID"].ToString();
            int domainInterfaceId = Convert.ToInt32(peptideInterfaceRow["DomainInterfaceID"].ToString ());
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashFolder))
            {
                Directory.CreateDirectory(hashFolder);
            }
            string domainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
            if (File.Exists (domainInterfaceFile) || File.Exists (domainInterfaceFile + ".gz"))
            {
                return;
            }
            int chainDomainId = Convert.ToInt32(peptideInterfaceRow["ChainDomainID"].ToString ());
            string domainAsymChain = peptideInterfaceRow["AsymChain"].ToString().TrimEnd ();
            long domainId = Convert.ToInt64(peptideInterfaceRow["DomainID"].ToString ());
            string peptideAsymChain = peptideInterfaceRow["PepAsymChain"].ToString().TrimEnd();

            Range[] domainRanges = domainRangeHash[domainId];
            AtomInfo[] domainAtoms = GetRegionAtomInfos(asuChainHash[domainAsymChain], domainRanges);

            string remarkString = FormatDomainPeptideInterfaceFileInfo(peptideInterfaceRow);
            
            WriteInterfaceToFile(domainInterfaceFile, remarkString, domainAtoms, asuChainHash[peptideAsymChain]);
            ParseHelper.ZipPdbFile(domainInterfaceFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainAtoms"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private AtomInfo[] GetRegionAtomInfos(AtomInfo[] chainAtoms, Range[] domainRanges)
        {
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = 0;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsResidueInDomainRanges(seqId, domainRanges))
                {
                    atomList.Add(atom);
                }
            }
           AtomInfo[] domainAtoms = new AtomInfo[atomList.Count];
           atomList.CopyTo(domainAtoms);
           return domainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        private bool IsResidueInDomainRanges(int seqId, Range[] domainRanges)
        {
            foreach (Range domainRange in domainRanges)
            {
                if (seqId >= domainRange.startPos && seqId <= domainRange.endPos)
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
        /// <param name="chainDomainId"></param>
        /// <param name="pepAsymChain"></param>
        /// <param name="asuChainHash"></param>
        private void WriteMultiChainDomainPeptideInterfaceFile(DataRow peptideInterfaceRow, Dictionary<string, AtomInfo[]> asuChainHash)
        {
            string pdbId = peptideInterfaceRow["PdbID"].ToString();
            int chainDomainId = Convert.ToInt32(peptideInterfaceRow["ChainDomainID"].ToString ());
            int domainInterfaceId = Convert.ToInt32(peptideInterfaceRow["DomainInterfaceID"].ToString ());
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashFolder))
            {
                Directory.CreateDirectory(hashFolder);
            }
            string domainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
            if (File.Exists (domainInterfaceFile) || File.Exists (domainInterfaceFile + ".gz"))
            {
                return;
            }
            string pepAsymChain = peptideInterfaceRow["PepAsymChain"].ToString ().TrimEnd ();

            string gzDomainFile = Path.Combine (pfamDomainFileDir, pdbId.Substring (1, 2) + "\\" + pdbId + chainDomainId.ToString () + ".pfam.gz");
            string domainFile = ParseHelper.UnZipFile (gzDomainFile);
            string domainRemarkString = "";
            AtomInfo[] domainAtoms = atomReader.ReadChainCoordFile(domainFile, out domainRemarkString);
            ParseHelper.ZipPdbFile(domainFile);

            string remarkString = FormatDomainPeptideInterfaceFileInfo(peptideInterfaceRow);
           
            WriteInterfaceToFile(domainInterfaceFile, remarkString, domainAtoms, asuChainHash[pepAsymChain]);
            ParseHelper.ZipPdbFile(domainInterfaceFile);
        }

        /// <summary>
        /// v
        /// </summary>
        /// <param name="domainPepInterfaceRow"></param>
        /// <returns></returns>
        private string FormatDomainPeptideInterfaceFileInfo(DataRow domainPepInterfaceRow)
        {
            string pdbId = domainPepInterfaceRow["PdbID"].ToString();
            int chainDomainId = Convert.ToInt32(domainPepInterfaceRow["ChainDomainId"].ToString ());
            long domainId = Convert.ToInt64(domainPepInterfaceRow["DomainID"].ToString ());
            string remarkString = "HEADER " + pdbId + " " + DateTime.Today.ToShortDateString();
            remarkString = remarkString + "\r\n" + "REMARK 1 Domain Interface ID: " + domainPepInterfaceRow["DomainInterfaceID"].ToString();
            remarkString = remarkString + "\r\n" + FormatDomainFileInfoRemark(pdbId, domainId, chainDomainId);
            remarkString = remarkString + "\r\n" +
                "REMARK 3  Peptide Chain: " + domainPepInterfaceRow["PepAsymChain"].ToString().TrimEnd();
            string pepDomainId = domainPepInterfaceRow["PepDomainID"].ToString();
            if (Convert.ToInt64 (pepDomainId) > 0)
            {
                remarkString = remarkString + " Peptide DomainID: " + pepDomainId + "  " + domainPepInterfaceRow["PepChainDomainID"].ToString();
            }
            remarkString = remarkString + "\r\n";
            remarkString = remarkString +
                "REMARK 4   #AtomPairs: " + domainPepInterfaceRow["NumOfAtomPairs"].ToString() + " " +
                "#ResiduePairs: " + domainPepInterfaceRow["NumOfResiduePairs"].ToString();
            return remarkString;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIds(DataTable peptideInterfaceTable)
        {
            List<int> domainInterfaceIdList = new List<int> ();
            int domainInterfaceId = 0;
            foreach (DataRow interfaceRow in peptideInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceId"].ToString ());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }

            return domainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefTable"></param>
        /// <returns></returns>
        private Dictionary<long, Range[]> GetDomainRangeHash(DataTable domainDefTable)
        {
            Dictionary<long, List<Range>> domainRangeListHash = new Dictionary<long,List<Range>> ();
            long domainId = 0;
            foreach (DataRow domainRow in domainDefTable.Rows)
            {
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                Range range = new Range();
                range.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                range.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());

                if (domainRangeListHash.ContainsKey(domainId))
                {
                    domainRangeListHash[domainId].Add(range);
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(range);
                    domainRangeListHash.Add(domainId, rangeList);

                }
            }
            Dictionary<long, Range[]> domainRangeHash = new Dictionary<long, Range[]>();
            foreach (long lsDomainId in domainRangeListHash.Keys)
            {
                domainRangeHash.Add (lsDomainId, domainRangeListHash[lsDomainId].ToArray ());
            }
            return domainRangeHash;
        }

        #region for debug
        public void CopyPeptideInterfaces()
        {
            string destFileDir = @"D:\DbProjectData\pfam\peptideInterfaces";
            string srcFileDir = @"D:\DbProjectData\InterfaceFiles_update\pfamDomain";
            string srcPepInterfaceFile = "";
            string destPepInterfaceFile = "";
            string queryString = "Select PdbID, DomainInterfaceID From PfamPeptideInterfaces;";
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string peptideInterfaceFileName = "";
            string hashFolder = "";
            string destHashFolder = "";
            foreach (DataRow pepInterfaceRow in peptideInterfaceTable.Rows)
            {
                peptideInterfaceFileName = pepInterfaceRow["PdbID"].ToString() + "_d" + pepInterfaceRow["DomainInterfaceID"].ToString() + ".cryst.gz";
                hashFolder = pepInterfaceRow["PdbID"].ToString().Substring(1, 2);
                srcPepInterfaceFile = Path.Combine(srcFileDir, hashFolder + "\\" + peptideInterfaceFileName);
                destPepInterfaceFile = Path.Combine(destFileDir, hashFolder + "\\" + peptideInterfaceFileName);
                destHashFolder = Path.Combine(destFileDir, hashFolder);
                if (!Directory.Exists(destHashFolder))
                {
                    Directory.CreateDirectory(destHashFolder);
                }
                File.Copy(srcPepInterfaceFile, destPepInterfaceFile, true);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void WriteDomainPeptideInterfaceFiles()
        {
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (string.Compare (pdbId, "1ahg") != 0)
                {
                    continue;
                }

                queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
                DataTable peptideInterfacesTable = ProtCidSettings.protcidQuery.Query( queryString);
                long[] multiChainDomainIds = GetEntryMultiChainDomainIds(pdbId);

                Dictionary<string, AtomInfo[]> asuChainHash = buReader.GetAsymUnitChainHash(pdbId);

                DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
                DataTable chainDomainTable = GetEntryChainDomainDefTable(pdbId);
                Dictionary<long, Range[]> domainRangeHash = GetDomainRangeHash(domainDefTable);

                int[] domainInterfaceIds = GetEntryDomainInterfaceIds(peptideInterfacesTable);
                long domainId = 0;
                foreach (int domainInterfaceId in domainInterfaceIds)
                {
                    DataRow[] domainInterfaceRows = peptideInterfacesTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
                    domainId = Convert.ToInt64 (domainInterfaceRows[0]["DomainID"].ToString());

                    string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
                    if (!Directory.Exists(hashFolder))
                    {
                        Directory.CreateDirectory(hashFolder);
                    }
                    string domainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
                    if (File.Exists(domainInterfaceFile) || File.Exists(domainInterfaceFile + ".gz"))
                    {
                        continue;
                    }

                    if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
                    {
                        try
                        {
                            WriteMultiChainDomainPeptideInterfaceFile(domainInterfaceRows[0], asuChainHash);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString() + " multi-chain domain peptide interface file gen error: " + ex.Message);
                            ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " multi-chain domain peptide interface file gen error: " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                    else
                    {
                        try
                        {
                            WriteSingleDomainPeptideInterfaceFile(domainInterfaceRows[0], asuChainHash, domainRangeHash);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString() + " single chain domain interface file gen error: " + ex.Message);
                            ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " single chain domain interface file gen error: " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                }
            }
        }
        #endregion
    }   
}
