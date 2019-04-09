using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using InterfaceClusterLib.DomainInterfaces;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPepSeqFasta : DomainSeqFasta
    {
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamPeptideClusterSequences()
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\PepFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\PepFasta", "\\WebPepFasta");
            if (!Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }

            StreamWriter lsFileWriter = new StreamWriter(Path.Combine(webFastaFileDir, "relSeq-ls.txt"), true);
            string pepSeqFile = "";

            string queryString = "Select Distinct PfamID From PfamPepInterfaceClusters;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            string pfamId = "";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = pfamIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamIdTable.Rows.Count;

            queryString = "Select PdbID, EntityID, AsymID, Sequence From AsymUnit WHere PolymerType = 'polypeptide';";
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, Pfam_ID, Pfam_Acc From PdbPfam;";
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string srcPepSeqFile = "";
            string destPepSeqFile = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    pepSeqFile = "Seq" + pfamId + ".tar.gz";
                    if (File.Exists(Path.Combine(webFastaFileDir, pepSeqFile)))
                    {
                        continue;
                    }

                    string[] fastaSeqFiles = PrintPfamPeptideClusterSeqFasta(pfamId, entitySeqTable, domainTable);

                    pepSeqFile = fileCompress.RunTar(pepSeqFile, fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, true);

                    lsFileWriter.WriteLine(pepSeqFile);
                    lsFileWriter.Flush();

                    // move the tar file to the web folder
                    srcPepSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, pepSeqFile);
                    destPepSeqFile = Path.Combine(webFastaFileDir, pepSeqFile);
                    if (File.Exists(destPepSeqFile))
                    {
                        File.Delete(destPepSeqFile);
                    }
                    File.Move(srcPepSeqFile, destPepSeqFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            lsFileWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatePfams"></param>
        public void UpdatePfamPeptideClusterSequences(string[] updatePfams)
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\PepFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\PepFasta", "\\WebPepFasta");
            if (!Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }

            StreamWriter lsFileWriter = new StreamWriter(Path.Combine(webFastaFileDir, "relSeq-ls.txt"), true);
            string pepSeqFile = "";

            string queryString = "Select Distinct PfamID From PfamPepInterfaceClusters;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updatePfams.Length ;
            ProtCidSettings.progressInfo.totalStepNum = updatePfams.Length;

            queryString = "Select PdbID, EntityID, AsymID, Sequence From AsymUnit WHere PolymerType = 'polypeptide';";
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, Pfam_ID, Pfam_Acc From PdbPfam;";
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string srcPepSeqFile = "";
            string destPepSeqFile = "";
            foreach (string pfamId in updatePfams)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    pepSeqFile = "Seq" + pfamId + ".tar.gz";

                    File.Delete(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, pepSeqFile));

                    string[] fastaSeqFiles = PrintPfamPeptideClusterSeqFasta(pfamId, entitySeqTable, domainTable);

                    pepSeqFile = fileCompress.RunTar(pepSeqFile, fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, true);

                    lsFileWriter.WriteLine(pepSeqFile);
                    lsFileWriter.Flush();

                    // move the tar file to the web folder
                    srcPepSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, pepSeqFile);
                    destPepSeqFile = Path.Combine(webFastaFileDir, pepSeqFile);
                    if (File.Exists(destPepSeqFile))
                    {
                        File.Delete(destPepSeqFile);
                    }
                    File.Move(srcPepSeqFile, destPepSeqFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            lsFileWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entitySeqTable"></param>
        public string[] PrintPfamPeptideClusterSeqFasta(string pfamId, DataTable entitySeqTable, DataTable domainTable)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;
            string entryDomain = "";
            string entryPeptide = "";
            List<string> clusterDomainList = new List<string>();
            List<string> clusterPeptideList = new List<string>();
            List<string> allClusterDomainList = new List<string>();
            List<string> allClusterPeptideList = new List<string>();
            string seqFileName = "";
            string seqFile = "";

            DataTable pfamPeptideInterfaceTable = GetPfamPeptideInterfaces(pfamId);
            List<string> seqFileList = new List<string>();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                clusterDomainList.Clear();
                clusterPeptideList.Clear();
                DataTable clusterPeptideInterfaceTable = GetClusterPeptideInterfaces(pfamId, clusterId, pfamPeptideInterfaceTable);
                foreach (DataRow domainInterfaceRow in clusterPeptideInterfaceTable.Rows)
                {
                    entryDomain = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID"].ToString();
                    entryPeptide = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["PepAsymChain"].ToString();
                    if (!clusterDomainList.Contains(entryDomain))
                    {
                        clusterDomainList.Add(entryDomain);
                    }
                    if (!allClusterDomainList.Contains(entryDomain))
                    {
                        allClusterDomainList.Add(entryDomain);
                    }

                    if (!clusterPeptideList.Contains(entryPeptide))
                    {
                        clusterPeptideList.Add(entryPeptide);
                    }
                    if (!allClusterPeptideList.Contains(entryPeptide))
                    {
                        allClusterPeptideList.Add(entryPeptide);
                    }
                }
                clusterDomainList.Sort();
                string[] clusterDomains = new string[clusterDomainList.Count];
                clusterDomainList.CopyTo(clusterDomains);
                seqFileName = "Cluster" + pfamId + "A_" + clusterId.ToString() + ".fasta";
                seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
                WriteDomainSequenceToFile(clusterDomains, seqFile, entitySeqTable, domainTable);
                seqFileList.Add(seqFileName);

                clusterPeptideList.Sort();
                string[] clusterPeptides = new string[clusterPeptideList.Count];
                clusterPeptideList.CopyTo(clusterPeptides);
                seqFileName = "Cluster" + pfamId + "B_" + clusterId.ToString() + ".fasta";
                seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
                WriteDomainSequenceToFile(clusterPeptides, seqFile, entitySeqTable);
                seqFileList.Add(seqFileName);
            }
            allClusterDomainList.Sort();
            string[] allClusterDomains = new string[allClusterDomainList.Count];
            allClusterDomainList.CopyTo(allClusterDomains);
            seqFileName = "Group" + pfamId + "A.fasta";
            seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
            WriteDomainSequenceToFile(allClusterDomains, seqFile, entitySeqTable, domainTable);
            seqFileList.Add(seqFileName);

            allClusterPeptideList.Sort();
            string[] allClusterPeptides = new string[allClusterPeptideList.Count];
            allClusterPeptideList.CopyTo(allClusterPeptides);
            seqFileName = "Group" + pfamId + "B.fasta";
            seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
            WriteDomainSequenceToFile(allClusterPeptides, seqFile, entitySeqTable);
            seqFileList.Add(seqFileName);

            string[] fastaSeqFiles = new string[seqFileList.Count];
            seqFileList.CopyTo(fastaSeqFiles);
            return fastaSeqFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamPeptideInterfaces(string pfamId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return pfamPepInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterPeptideInterfaces(string pfamId, int clusterId, DataTable pfamPeptideInterfaceTable)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamPepInterfaceClusters WHere PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            DataTable domainInterfaceTable = pfamPeptideInterfaceTable.Clone();
            foreach (DataRow interfaceRow in domainInterfaceIdTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                DataRow[] domainInterfaceRows = pfamPeptideInterfaceTable.Select(
                    string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));

                foreach (DataRow domainInterfaceRow in domainInterfaceRows)
                {
                    DataRow dataRow = domainInterfaceTable.NewRow();
                    dataRow.ItemArray = domainInterfaceRow.ItemArray;
                    domainInterfaceTable.Rows.Add(dataRow);
                }
            }
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <param name="seqFileName"></param>
        /// <param name="entitySeqTable"></param>
        /// <param name="domainTable"></param>
        public void WriteDomainSequenceToFile(string[] pepChains, string seqFileName, DataTable entitySeqTable)
        {
            StreamWriter seqWriter = new StreamWriter(seqFileName);
            string pdbId = "";
            string chainId = "";
            string peptideSequence = "";
            string headerLine = "";
            foreach (string pepChain in pepChains)
            {
                pdbId = pepChain.Substring(0, 4);
                chainId = pepChain.Substring(4, pepChain.Length - 4);
                headerLine = ">" + pdbId + chainId;
                peptideSequence = GetChainSequence(pdbId, chainId, entitySeqTable);
                if (peptideSequence != "")
                {
                    seqWriter.WriteLine(headerLine);
                    seqWriter.WriteLine(peptideSequence);
                }
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="entitySeqTable"></param>
        /// <returns></returns>
        private string GetChainSequence(string pdbId, string asymChain, DataTable entitySeqTable)
        {
            DataRow[] seqRows = entitySeqTable.Select(string.Format ("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            if (seqRows.Length > 0)
            {
                return seqRows[0]["Sequence"].ToString().TrimEnd();
            }
            return "";
        }
    }
}
