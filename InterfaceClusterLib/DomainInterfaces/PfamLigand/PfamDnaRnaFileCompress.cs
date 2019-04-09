using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamDnaRnaFileCompress  : PfamDomainFileCompress 
    {
        #region member variables
        private PdbBuGenerator buGenerator = new PdbBuGenerator();
        #endregion

        #region Domain-DNA/RNA files
        /// <summary>
        /// 
        /// </summary>
        public void WriteDomainDnaRnaFiles()
        {
            if (! Directory.Exists(pfamDnaRnaDir))
            {
                Directory.CreateDirectory(pfamDnaRnaDir);
            }

            string queryString = "Select Distinct PdbID From PfamDnaRnas;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Write Domain-DnaRna files";
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;

            ProtCidSettings.logWriter.WriteLine("Write Pfam-DNA/RNA coord files.");

            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    WriteDomainDnaRnaInterfaceFiles(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " writing Pfam-DNA/RNA interaction files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " writing Pfam-DNA/RNA interaction files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-DNA/RNA coord files done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainDnaRnaFiles(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Write Domain-DnaRna files";
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            ProtCidSettings.logWriter.WriteLine("Update Pfam-DNA/RNA coord files");
  //          DateTime dt = new DateTime (2017, 9, 26);
            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    string[] entryDnaRnaFiles = Directory.GetFiles(pfamDnaRnaDir, pdbId + "*");
   /*                 if (entryDnaRnaFiles.Length > 0)
                    {
                        FileInfo fileInfo = new FileInfo(entryDnaRnaFiles[0]);
                        if (DateTime.Compare (fileInfo.LastWriteTime, dt) >= 0)
                        {
                            continue;
                        }
                    }*/
                    foreach (string dnaRnaFile in entryDnaRnaFiles)
                    {
                        File.Delete(dnaRnaFile);
                    }

                    WriteDomainDnaRnaInterfaceFiles(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " writing Pfam-DNA/RNA interaction files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " writing Pfam-DNA/RNA interaction files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Update Pfam-DNA/RNA coord files done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void WriteDomainDnaRnaInterfaceFiles(string pdbId)
        {
            string queryString = string.Format("Select * From PfamDnaRnas Where PdbID = '{0}';", pdbId);
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] chainDomainIds = GetEntryChainDomainIds(pfamDnaRnaTable);

            queryString = string.Format("Select PdbID, AsymID, PolymerStatus, PolymerType From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable entryAsuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            Dictionary<string, List<string>> buDomainWithMaxHmmHash = new Dictionary<string,List<string>> ();
            foreach (int chainDomainId in chainDomainIds)
            {
                SelectDomainDnaRnaInterfaces(chainDomainId, pfamDnaRnaTable, ref buDomainWithMaxHmmHash);
            }

            List<string> buIdList = new List<string> (buDomainWithMaxHmmHash.Keys);
            string[] buIds = new  string[buIdList.Count];
            buIdList.CopyTo(buIds);
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuHash = buGenerator.BuildPdbBus(pdbId, buIds, true);

            EntryCrystal thisEntryCrystal = buGenerator.ReadEntryCrystalFromXml(pdbId);
            string domainFile = "";
            string chainId = "";
            string chainSymOp = "";
            foreach (string buId in buDomainWithMaxHmmHash.Keys)
            {
                List<string> domainSymOpList = buDomainWithMaxHmmHash[buId];
                string[][] nonProtChainSymOps = GetAllNonProtChainSymOps(pdbId, buId, entryAsuTable);
                string[] dnaRnaChainSymOps = nonProtChainSymOps[0];
                string[] ligandChainSymOps = nonProtChainSymOps[1];

                if(dnaRnaChainSymOps.Length == 0)
                {
                    continue;
                }

                foreach (string domainSymOp in domainSymOpList)
                {
             //       string[] dnaRnaChains = GetInteractingDnaRnaChainSymOps(pdbId, buId, domainSymOp, pfamDnaRnaTable);
                    string[] domainSymOpFields = SeparateDomainFields(domainSymOp);
                    chainId = GetDomainChainId(pdbId, buId, Convert.ToInt32(domainSymOpFields[0]), domainSymOpFields[1], pfamDnaRnaTable);
                    chainSymOp = chainId + "_" + domainSymOpFields[1];
                    domainFile = Path.Combine(pfamDnaRnaDir, pdbId + domainSymOpFields[0] + ".pfam");
            //        WriteDomainDnaRnaInterfaceFiles(pdbId, buId, chainSymOp, dnaRnaChains, buHash, thisEntryCrystal, domainFile);
                    try
                    {
                        WriteDomainDnaRnaInterfaceFiles(pdbId, buId, chainSymOp, dnaRnaChainSymOps, ligandChainSymOps, entryBuHash[buId], thisEntryCrystal, domainFile);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(pdbId + " " + domainSymOp + " domain-DNA/RNA interface error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainSymOps"></param>
        /// <param name="buHash"></param>
        /// <param name="domainFile"></param>
        public void WriteDomainDnaRnaInterfaceFiles (string pdbId, string buId, string protChainSymOp, string[] dnaRnaChainSymOps, string[] ligandChainSymOps, 
                                    Dictionary<string, AtomInfo[]> buHash, EntryCrystal thisEntryCrystal, string domainFile)
        {
            Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();
            Dictionary<string, string> chainMatchHash = new Dictionary<string,string> ();
            List<string> asymChainList = new List<string>();
            List<string> chainSymOpList = new List<string>();
            List<string> fileChainsInOrderList = new List<string> ();
            string fileChain = "";
            string asymChain = "";
            int chainIndex = 0;          
            string[] chainSymOps = new string[dnaRnaChainSymOps.Length + 1 + ligandChainSymOps.Length];
            chainSymOps[0] = protChainSymOp;
            Array.Copy(dnaRnaChainSymOps, 0, chainSymOps, 1, dnaRnaChainSymOps.Length);
            Array.Copy(ligandChainSymOps, 0, chainSymOps, dnaRnaChainSymOps.Length + 1, ligandChainSymOps.Length);

            foreach (string chainSymOp in chainSymOps)
            {
                if (buHash.ContainsKey(chainSymOp))
                {
                    AtomInfo[] chainDomains = (AtomInfo[])buHash[chainSymOp];
                    if (chainIndex == ParseHelper.chainLetters.Length)
                    {
                        chainIndex = 0;
                    }
                    fileChain = ParseHelper.chainLetters[chainIndex].ToString();
                    asuChainHash.Add(chainSymOp, chainDomains);
                    chainMatchHash.Add(chainSymOp, fileChain);
                    fileChainsInOrderList.Add(fileChain);
                    chainIndex++;

                    asymChain = GetAsymChain(chainSymOp);
                    asymChainList.Add(asymChain);
                    chainSymOpList.Add(chainSymOp);
                }
            }
            string[] asymChains = asymChainList.ToArray ();
            string[] fileChains = fileChainsInOrderList.ToArray();
            
            string resrecordLines = GetSeqResRecords(thisEntryCrystal, asymChains, fileChains);

            string remark = "HEADER    " + pdbId + " " + DateTime.Today.ToShortDateString() +"\r\n";
            remark = remark + "REMARK   2 Biological Assembly " + buId + "\r\n";
            remark = remark + "REMARK   2 Chains and Symmetry Operators in the file \r\n";
            remark = remark + "REMARK   2 " + FormatChainString(chainSymOps) + "\r\n"; 
            remark = remark + "REMARK   2 AsymChains    FileChains \r\n";
            remark = remark + "REMARK   2 " + FormatChainString(asymChains) + "\r\n";
            remark = remark + "REMARK   2 " + FormatChainString(fileChains) + "\r\n";
            remark = remark + resrecordLines + "\r\n";
            buWriter.WriteAsymUnitFile(domainFile, asuChainHash, chainSymOpList.ToArray (), fileChains, ligandChainSymOps, remark);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="pfamDnaRnaTable"></param>
        /// <returns></returns>
        private string[] GetInteractingDnaRnaChainSymOps(string pdbId, string buId, string domainSymOp, DataTable pfamDnaRnaTable)
        {
            string[] domainSymOpFields = SeparateDomainFields(domainSymOp);

            DataRow[] interactingDnaRnaRows = pfamDnaRnaTable.Select(string.Format("PdbID = '{0}' AND BuID = '{1}' AND " +
                " ChainDomainId = '{2}' AND SymmetryString = '{3}'", pdbId, buId, domainSymOpFields[0], domainSymOpFields[1]));

            List<string> dnaRnaChainList = new List<string> ();
            string dnaRnaChainSymOp = "";
            foreach (DataRow dnaRnaRow in interactingDnaRnaRows)
            {
                dnaRnaChainSymOp = dnaRnaRow["DnaRnaChain"].ToString().TrimEnd() + "_" + dnaRnaRow["DnaRnaSymmetryString"].ToString().TrimEnd();
                if (!dnaRnaChainList.Contains(dnaRnaChainSymOp))
                {
                    dnaRnaChainList.Add(dnaRnaChainSymOp);
                }
            }
            return dnaRnaChainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="entryAsuTable"></param>
        /// <returns></returns>
        private string[][] GetAllNonProtChainSymOps(string pdbId, string buId, DataTable entryAsuTable)
        {
            string queryString = string.Format("Select * From PdbBuGen Where PdbID = '{0}' AND BiolUnitID = '{1}';", pdbId, buId);
            DataTable buChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            List<string> dnaRnaChainSymOpList = new List<string>();
            List<string> ligandChainSymOpList = new List<string>();
            string chainId = "";
            string chainSymOp = "";
            string symmetryString = "";
            foreach (DataRow buChainRow in buChainTable.Rows)
            {
                chainId = buChainRow["AsymID"].ToString().TrimEnd();
                if (IsChainPeptide(pdbId, chainId, entryAsuTable))
                {
                    continue;
                }
                if (IsChainWater(pdbId, chainId, entryAsuTable))
                {
                    continue;
                }
               symmetryString = buChainRow["SymmetryString"].ToString().TrimEnd();
               if (symmetryString == "-")
               {
                   chainSymOp = chainId + "_" + buChainRow["SymOpNum"].ToString().TrimEnd ();
               }
               else
               {
                   chainSymOp = chainId + "_" + symmetryString;
               }
                if (IsChainDnaRna(pdbId, chainId, entryAsuTable))
                {
                    if (! dnaRnaChainSymOpList.Contains(chainSymOp))
                    {
                        dnaRnaChainSymOpList.Add(chainSymOp);
                    }
                }
                else 
                {
                    if (!ligandChainSymOpList.Contains(chainSymOp))
                    {
                        ligandChainSymOpList.Add(chainSymOp);
                    }
                }
            }
            string[][] nonProtChainSymOps = new string[2][];
            nonProtChainSymOps[0] = dnaRnaChainSymOpList.ToArray ();
            nonProtChainSymOps[1] = ligandChainSymOpList.ToArray ();
            return nonProtChainSymOps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <param name="entryAsuTable"></param>
        /// <returns></returns>
        private bool IsChainPeptide(string pdbId, string chainId, DataTable entryAsuTable)
        {
            DataRow[] chainRows = entryAsuTable.Select(string.Format ("PdbID = '{0}' AND AsymID = '{1}'", pdbId, chainId));
            if (chainRows.Length > 0)
            {
                string polymerType = chainRows[0]["PolymerType"].ToString().TrimEnd();
                if (polymerType == "polypeptide")
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
        /// <param name="chainId"></param>
        /// <param name="entryAsuTable"></param>
        /// <returns></returns>
        private bool IsChainWater(string pdbId, string chainId, DataTable entryAsuTable)
        {
            DataRow[] chainRows = entryAsuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, chainId));
            if (chainRows.Length > 0)
            {
                string polymerStatus = chainRows[0]["PolymerStatus"].ToString().TrimEnd();
                if (polymerStatus == "water")
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
        /// <param name="chainId"></param>
        /// <param name="entryAsuTable"></param>
        /// <returns></returns>
        private bool IsChainDnaRna (string pdbId, string chainId, DataTable entryAsuTable)
        {
            DataRow[] chainRows = entryAsuTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, chainId));
            if (chainRows.Length > 0)
            {
                string polymerType = chainRows[0]["PolymerType"].ToString().TrimEnd();
                if (polymerType == "polydeoxyribonucleotide" || polymerType == "polyribonucleotide")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSymOp"></param>
        /// <returns></returns>
        private string[] SeparateDomainFields(string domainSymOp)
        {
            string[] fields = domainSymOp.Split('_');
            string[] chainSymOpFields = new string[2];
            chainSymOpFields[0] = fields[0];
            if (fields.Length == 3)
            {
                chainSymOpFields[1] = fields[1] + "_" + fields[2];
            }
            else if (fields.Length == 2)
            {
                chainSymOpFields[1] = fields[1];
            }
            else
            {
                chainSymOpFields[1] = "-";
            }
            return chainSymOpFields;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOp"></param>
        /// <returns></returns>
        private string GetAsymChain(string chainSymOp)
        {
            string[] fields = chainSymOp.Split('_');
            return fields[0];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="symmetryString"></param>
        /// <param name="pfamDnaRnaTable"></param>
        /// <returns></returns>
        private string GetDomainChainId(string pdbId, string buId, int chainDomainId, string symmetryString, DataTable pfamDnaRnaTable)
        {
            DataRow[] domainRows = pfamDnaRnaTable.Select(string.Format ("PdbID = '{0}' AND BuID = '{1}' AND ChainDomainID= '{2}' AND SymmetryString = '{3}'",
                pdbId, buId, chainDomainId, symmetryString));
            if (domainRows.Length > 0)
            {
                return domainRows[0]["AsymChain"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomainId"></param>
        /// <param name="pfamDnaRnaTable"></param>
        private void SelectDomainDnaRnaInterfaces(int chainDomainId, DataTable pfamDnaRnaTable, ref Dictionary<string, List<string>> buDomainWithMaxHmmHash)
        {
            DataRow[] domainDnaRnaRows = pfamDnaRnaTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId));
            string[] domainBuIds = GetChainDomainBiolUnitIds(domainDnaRnaRows);

            int numOfHmmPoses = 0;
            int maxNumOfHmmPoses = -1;
            string buWithMaxNumHmm = "";
            string domainWithMaxNumHmm = "";
            foreach (string buId in domainBuIds)
            {
                DataRow[] buDomainDnaRnaRows = pfamDnaRnaTable.Select(string.Format ("BuID = '{0}' AND ChainDomainID = '{1}'", buId, chainDomainId));
                string[] domainSymOps = GetBuDomainSymOpChains(buDomainDnaRnaRows);
                foreach (string domainSymOp in domainSymOps)
                {
                    string[] domainSymOpFields = SeparateDomainFields(domainSymOp);
                    numOfHmmPoses = GetNumOfInteractingHmmPoses(buId, Convert.ToInt32(domainSymOpFields[0]), domainSymOpFields[1], pfamDnaRnaTable);
                    if (maxNumOfHmmPoses < numOfHmmPoses)
                    {
                        maxNumOfHmmPoses = numOfHmmPoses;
                        domainWithMaxNumHmm =  domainSymOp;
                        buWithMaxNumHmm = buId;
                    }
                }
            }
            if (buDomainWithMaxHmmHash.ContainsKey(buWithMaxNumHmm))
            {
                buDomainWithMaxHmmHash[buWithMaxNumHmm].Add(domainWithMaxNumHmm);
            }
            else
            {
                List<string> domainSymOpList = new List<string> ();
                domainSymOpList.Add(domainWithMaxNumHmm);
                buDomainWithMaxHmmHash.Add(buWithMaxNumHmm, domainSymOpList);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="symOp"></param>
        /// <param name="pfamDnaRnaTable"></param>
        /// <returns></returns>
        private int GetNumOfInteractingHmmPoses(string buId, int chainDomainId, string symOp, DataTable pfamDnaRnaTable)
        {
            DataRow[] domainDnaRnaRows = pfamDnaRnaTable.Select(string.Format ("BuID = '{0}' AND ChainDOmainID = '{1}' AND SymmetryString = '{2}'", 
                buId, chainDomainId, symOp));
            List<int> hmmSeqIdList = new List<int> ();
            int hmmSeqId = 0;
            foreach (DataRow domainDnaRnaRow in domainDnaRnaRows)
            {
                hmmSeqId = Convert.ToInt32(domainDnaRnaRow["HmmSeqID"].ToString ());
                if (hmmSeqId > 0)
                {
                    if (!hmmSeqIdList.Contains(hmmSeqId))
                    {
                        hmmSeqIdList.Add(hmmSeqId);
                    }
                }
            }
            return hmmSeqIdList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDnaRnaRows"></param>
        /// <returns></returns>
        private string[] GetChainDomainBiolUnitIds(DataRow[] domainDnaRnaRows)
        {
            List<string> buIdList = new List<string> ();
            string buId = "";
            foreach (DataRow domainDnaRnaRow in domainDnaRnaRows)
            {
                buId = domainDnaRnaRow["BuID"].ToString().TrimEnd();
                if (!buIdList.Contains(buId))
                {
                    buIdList.Add(buId);
                }
            }
            return buIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buDomainDnaRnaRows"></param>
        /// <returns></returns>
        private string[] GetBuDomainSymOpChains(DataRow[] buDomainDnaRnaRows)
        {
            List<string> domainSymOpList = new List<string> ();
            string domainSymOp = "";
            foreach (DataRow domainDnaRnaRow in buDomainDnaRnaRows)
            {
                domainSymOp = domainDnaRnaRow["ChainDomainID"].ToString() + "_" +
                    domainDnaRnaRow["SymmetryString"].ToString().TrimEnd();
                if (!domainSymOpList.Contains(domainSymOp))
                {
                    domainSymOpList.Add(domainSymOp);
                }
            }
            return domainSymOpList.ToArray ();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetBuChainDomainIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct BuID, ChainDomainID, SymmetryString From PfamDnaRnas WHere PdbID = '{0}';", pdbId);
            DataTable buChainDomainIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, List<string>> buChainDomainIdHash = new Dictionary<string,List<string>> ();
            string buId = "";
            string chainDomainIdSymOp = "";
            foreach (DataRow buChainDomainRow in buChainDomainIdTable.Rows)
            {
                buId = buChainDomainRow["BuID"].ToString().TrimEnd();
                chainDomainIdSymOp = buChainDomainRow["ChainDomainID"].ToString() + "_" + 
                    buChainDomainRow["SymmetryString"].ToString ().TrimEnd ();
                if (buChainDomainIdHash.ContainsKey(buId))
                {
                    if (!buChainDomainIdHash[buId].Contains(chainDomainIdSymOp))
                    {
                        buChainDomainIdHash[buId].Add(chainDomainIdSymOp);
                    }
                }
                else
                {
                    List<string> chainDomainIdSymOpList = new List<string> ();
                    chainDomainIdSymOpList.Add(chainDomainIdSymOp);
                    buChainDomainIdHash.Add(buId, chainDomainIdSymOpList);
                }
            }
            return buChainDomainIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDnaRnaTable"></param>
        /// <returns></returns>
        private new int[] GetEntryChainDomainIds(DataTable pfamDnaRnaTable)
        {
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            foreach (DataRow pfamDnaRnaRow in pfamDnaRnaTable.Rows)
            {
                chainDomainId = Convert.ToInt32(pfamDnaRnaRow["ChainDomainID"].ToString());
                if (!chainDomainIdList.Contains(chainDomainId))
                {
                    chainDomainIdList.Add(chainDomainId);
                }
            }
            return chainDomainIdList.ToArray ();
        }
        #endregion
    }
}
