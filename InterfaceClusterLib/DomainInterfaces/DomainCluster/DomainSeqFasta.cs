using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.ChainInterfaces;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainSeqFasta : SeqFastaGenerator
    {

        /// <summary>
        /// 
        /// </summary>
        public void PrintClusterDomainSequences()
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\DomainFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\DomainFasta", "\\WebDomainFasta");
            if (!Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }

            StreamWriter lsFileWriter = new StreamWriter(Path.Combine(webFastaFileDir, "relSeq-ls.txt"), true);
            string relationSeqFile = "";

            string queryString = "Select Distinct RelSeqID From PfamDomainInterfaceCluster;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            int relSeqId = 0;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;

            queryString = "Select PdbID, EntityID, AsymID, Sequence From AsymUnit WHere PolymerType = 'polypeptide';";
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, Pfam_ID, Pfam_Acc From PdbPfam;";
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string srcRelSeqFile = "";
            string destRelSeqFile = "";
            string relationName = "";
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                relationName = DownloadableFileName.GetDomainRelationName(relSeqId);

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    relationSeqFile = "Seq_" + relationName + ".tar.gz";
                    if (!File.Exists(relationSeqFile))
                    {

                        string[] fastaSeqFiles = PrintRelationClusterSeqFasta(relSeqId, entitySeqTable, domainTable);

                        relationSeqFile = fileCompress.RunTar(relationSeqFile, fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, true);
                    }
                    lsFileWriter.WriteLine(relationSeqFile);
                    lsFileWriter.Flush();

                    // move the tar file to the web folder
                    srcRelSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, relationSeqFile);
                    destRelSeqFile = Path.Combine(webFastaFileDir, relationSeqFile);
                    if (File.Exists(destRelSeqFile))
                    {
                        File.Delete(destRelSeqFile);
                    }
                    File.Move(srcRelSeqFile, destRelSeqFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            lsFileWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateClusterDomainSequences(int[] relSeqIds)
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\DomainFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\DomainFasta", "\\WebDomainFasta");
            if (Directory.Exists(webFastaFileDir))
            {
                Directory.Delete(webFastaFileDir, true);
            }
            Directory.CreateDirectory(webFastaFileDir);

            StreamWriter lsFileWriter = new StreamWriter(Path.Combine(webFastaFileDir, "relSeq-newls.txt"), true);
            string relationSeqFile = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            string queryString = "Select PdbID, EntityID, AsymID, Sequence From AsymUnit WHere PolymerType = 'polypeptide';";
            DataTable entitySeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, DomainID, EntityID, SeqStart, SeqEnd, Pfam_ID, Pfam_Acc From PdbPfam;";
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string srcRelSeqFile = "";
            string destRelSeqFile = "";
            string relationName = "";
            foreach (int relSeqId in relSeqIds )
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteRelationSeqFiles(relSeqId);  // delete the old files
                    string[] fastaSeqFiles = PrintRelationClusterSeqFasta(relSeqId, entitySeqTable, domainTable);
                    // the length of parameters of Cmd.exe cannot be longer than 8191
                    relationName = DownloadableFileName.GetDomainRelationName(relSeqId);
                    relationSeqFile = "Seq_" + relationName + ".tar.gz";
                    fileCompress.RunTar(relationSeqFile, fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, true);
                       

                    lsFileWriter.WriteLine(relationSeqFile);
                    lsFileWriter.Flush();

                    // move the tar file to the web folder
                    srcRelSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, relationSeqFile);
                    destRelSeqFile = Path.Combine(webFastaFileDir, relationSeqFile);
                    if (File.Exists(destRelSeqFile))
                    {
                        File.Delete(destRelSeqFile);
                    }
                    File.Move(srcRelSeqFile, destRelSeqFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
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
        private void DeleteRelationSeqFiles(int relSeqId)
        {
            string searchPattern = "Cluster" + relSeqId.ToString() + "A*";
            string[] seqFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, searchPattern);
            foreach (string seqFile in seqFiles)
            {
                File.Delete(seqFile);
            }
            searchPattern = "Cluster" + relSeqId.ToString () + "B*";
            seqFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, searchPattern);
            foreach (string seqFile in seqFiles)
            {
                File.Delete(seqFile);
            }
            string groupSeqFile = Path.Combine (ProtCidSettings.dirSettings.seqFastaPath, "Group" + relSeqId.ToString () + "A.fasta");
            File.Delete (groupSeqFile);
            groupSeqFile = Path.Combine (ProtCidSettings.dirSettings.seqFastaPath, "Group" + relSeqId.ToString () + "B.fasta");  // in case heterodimer
            File.Delete (groupSeqFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entitySeqTable"></param>
        public string[] PrintRelationClusterSeqFasta(int relSeqId, DataTable entitySeqTable, DataTable domainTable)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable familyCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamCode1 = familyCodeTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            string pfamCode2 = familyCodeTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            List<string> relationDomainListA = new List<string>();
            List<string> relationDomainListB = null;
            if (pfamCode1 != pfamCode2)
            {
                relationDomainListB = new List<string>();
            }

            queryString = string.Format("Select Distinct ClusterID From PfamDomainClusterInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int clusterId = 0;
            string entryDomainA = "";
            string entryDomainB = "";
            List<string> clusterDomainListA = new List<string>();
            List<string> clusterDomainListB = null;
            string seqFileName = "";
            string seqFile = "";
            if (pfamCode1 != pfamCode2)
            {
                clusterDomainListB = new List<string>();
            }
            DataTable relDomainInterfaceTable = GetRelationDomainInterfaces(relSeqId);
            List<string> seqFileList = new List<string>();
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterDomainListA.Clear();
                if (clusterDomainListB != null)
                {
                    clusterDomainListB.Clear();
                }
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString());
                DataTable domainInterfaceTable = GetClusterDomainInterfaces(relSeqId, clusterId, relDomainInterfaceTable);
                foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
                {
                    entryDomainA = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID1"].ToString();
                    entryDomainB = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID2"].ToString();
                    if (domainInterfaceRow["IsReversed"].ToString() == "1")
                    {
                        string temp = entryDomainA;
                        entryDomainA = entryDomainB;
                        entryDomainB = temp;
                    }
                    if (!clusterDomainListA.Contains(entryDomainA))
                    {
                        clusterDomainListA.Add(entryDomainA);
                    }
                    if (!relationDomainListA.Contains(entryDomainA))
                    {
                        relationDomainListA.Add(entryDomainA);
                    }

                    if (clusterDomainListB == null)
                    {
                        if (!clusterDomainListA.Contains(entryDomainB))
                        {
                            clusterDomainListA.Add(entryDomainB);
                        }
                        if (!relationDomainListA.Contains(entryDomainB))
                        {
                            relationDomainListA.Add(entryDomainB);
                        }
                    }
                    else
                    {
                        if (!clusterDomainListB.Contains(entryDomainB))
                        {
                            clusterDomainListB.Add(entryDomainB);
                        }
                        if (!relationDomainListB.Contains(entryDomainB))
                        {
                            relationDomainListB.Add(entryDomainB);
                        }
                    }
                }
                string[] clusterDomainsA = new string[clusterDomainListA.Count];
                clusterDomainListA.CopyTo(clusterDomainsA);
                seqFileName = "Cluster" + relSeqId.ToString() + "A_" + clusterId.ToString() + ".fasta";
                seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
                WriteDomainSequenceToFile(clusterDomainsA, seqFile, entitySeqTable, domainTable);
                seqFileList.Add(seqFileName);

                if (clusterDomainListB != null && clusterDomainListB.Count > 0)
                {
                    string[] clusterDomainsB = new string[clusterDomainListB.Count];
                    clusterDomainListB.CopyTo(clusterDomainsB);
                    seqFileName = "Cluster" + relSeqId.ToString() + "B_" + clusterId.ToString() + ".fasta";
                    seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
                    WriteDomainSequenceToFile(clusterDomainsB, seqFile, entitySeqTable, domainTable);
                    seqFileList.Add(seqFileName);
                }
            }
            string[] relationDomainsA = new string[relationDomainListA.Count];
            relationDomainListA.CopyTo(relationDomainsA);
            seqFileName = "Group" + relSeqId.ToString() + "A.fasta";
            seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
            WriteDomainSequenceToFile(relationDomainsA, seqFile, entitySeqTable, domainTable);
            seqFileList.Add(seqFileName);

            if (relationDomainListB != null && relationDomainListB.Count > 0)
            {
                string[] relationDomainsB = new string[relationDomainListB.Count];
                relationDomainListB.CopyTo(relationDomainsB);
                seqFileName = "Group" + relSeqId.ToString() + "B.fasta";
                seqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, seqFileName);
                WriteDomainSequenceToFile(relationDomainsB, seqFile, entitySeqTable, domainTable);
                seqFileList.Add(seqFileName);
            }

            string[] fastaSeqFiles = new string[seqFileList.Count];
            seqFileList.CopyTo(fastaSeqFiles);
            return fastaSeqFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <param name="seqFileName"></param>
        /// <param name="entitySeqTable"></param>
        /// <param name="domainTable"></param>
        public void WriteDomainSequenceToFile(string[] domainList, string seqFileName, DataTable entitySeqTable, DataTable domainTable)
        {
            StreamWriter seqWriter = new StreamWriter(seqFileName);
            string pdbId = "";
            long domainId = 0;
            int entityId = 0;
            string domainSequence = "";
            string entitySequence = "";
            int domainStart = 0;
            int domainEnd = 0;
            string headerLine = "";
            string pfamId = "";
            string pfamAcc = "";
            foreach (string entryDomain in domainList)
            {
                try
                {
                    pdbId = entryDomain.Substring(0, 4);
                    domainId = Convert.ToInt64(entryDomain.Substring(4, entryDomain.Length - 4));
                    headerLine = ">" + pdbId + domainId.ToString() + " | ";
                    domainSequence = "";
                    DataRow[] domainRows = domainTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainId), "SeqStart ASC");
                    pfamId = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
                    pfamAcc = domainRows[0]["Pfam_Acc"].ToString().TrimEnd();
                    foreach (DataRow domainRow in domainRows)
                    {
                        entityId = Convert.ToInt32(domainRow["EntityID"].ToString());
                        DataRow[] entitySeqRows = entitySeqTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                        entitySequence = entitySeqRows[0]["Sequence"].ToString().TrimEnd();
                        domainStart = Convert.ToInt32(domainRow["SeqStart"].ToString());
                        domainEnd = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                        domainSequence += entitySequence.Substring(domainStart - 1, domainEnd - domainStart + 1);
                        headerLine += (entityId.ToString() + " [" + domainStart.ToString() + "-" + domainEnd.ToString() + "] ");
                    }
                    headerLine += (pfamId + " " + pfamAcc);
                    seqWriter.WriteLine(headerLine);
                    seqWriter.WriteLine(domainSequence);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(seqFileName + " " + entryDomain + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(seqFileName + " " + entryDomain + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            seqWriter.Close();
        }

       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIed"></param>
        /// <returns></returns>
        public DataTable GetRelationDomainInterfaces(int relSeqIed)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0};", relSeqIed);
            DataTable relDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return relDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public DataTable GetClusterDomainInterfaces(int relSeqId, int clusterId, DataTable relDomainInterfaceTable)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainClusterInterfaces WHere RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            DataTable domainInterfaceTable = relDomainInterfaceTable.Clone();
            foreach (DataRow interfaceRow in domainInterfaceIdTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                DataRow[] domainInterfaceRows = relDomainInterfaceTable.Select(
                    string.Format("RelSeqID = '{0}' AND PdbID = '{1}' AND DomainInterfaceID = '{2}'", relSeqId, pdbId, domainInterfaceId));

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
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private DataTable GetRelationEntryEntitySeqTable(string[] pdbIds)
        {
            DataTable entitySeqTable = null;
            string queryString = "";
            foreach (string pdbId in pdbIds)
            {
                queryString = string.Format("Select PdbID, EntityID, AsymID, Sequence From AsymUnit Where PdbID = '{0}' And PolymerType = 'polypeptide';", pdbId);
                DataTable seqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                if (entitySeqTable == null)
                {
                    entitySeqTable = seqTable;
                }
                else
                {
                    foreach (DataRow seqRow in seqTable.Rows)
                    {
                        DataRow dataRow = entitySeqTable.NewRow();
                        dataRow.ItemArray = seqRow.ItemArray;
                        entitySeqTable.Rows.Add(dataRow);
                    }
                }
            }
            return entitySeqTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private DataTable GetRelationEntryEntitySeqTable(string[] pdbIds, DataTable seqTable)
        {
            DataTable entitySeqTable = seqTable.Clone();
            foreach (string pdbId in pdbIds)
            {
                DataRow[] entitySeqRows = seqTable.Select(string.Format("PdbID = '{0}'", pdbId));

                foreach (DataRow seqRow in entitySeqRows)
                {
                    DataRow dataRow = entitySeqTable.NewRow();
                    dataRow.ItemArray = seqRow.ItemArray;
                    entitySeqTable.Rows.Add(dataRow);
                }
            }
            return entitySeqTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationEntries(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] relEntries = new string[relEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                relEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return relEntries;
        }

        #region for debug
        public void CompressSeqFastaFilesForDebug()
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\DomainFasta");

            string seqFileDir = @"M:\Qifang\DbProjectData\PhylogeneticTree\DomainFasta";
            string[] fastaSeqFiles = Directory.GetFiles(seqFileDir, "*.fasta");
            string[] fastaSeqFileNames = new string[fastaSeqFiles.Length];
            int count = 0;
            foreach (string seqFile in fastaSeqFiles)
            {
                FileInfo fileInfo = new FileInfo(seqFile);
                fastaSeqFileNames[count] = fileInfo.Name;
                count++;
            }

            // the length of parameters of Cmd.exe cannot be longer than 8191
            /*     if (fastaSeqFiles.Length > 100)
                 {
                     relationFolder = MoveSeqFastaFilesToGroupFolder (fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, relSeqId);

                     relationSeqFile = TarFastaFilesOnFolder(relSeqId, relationFolder);
                 }
                 else
                 {
                     relationSeqFile = TarFastaFiles(relSeqId, fastaSeqFiles);
                 }
                 */
            string relationSeqFile = "Seq1.tar.gz";
            fileCompress.RunTar(relationSeqFile, fastaSeqFileNames, ProtCidSettings.dirSettings.seqFastaPath, true);
        }
        /// <summary>
        /// 
        /// </summary>
        public void UpdateClusterDomainSequencesForDebug (int[] relSeqIds)
        {
            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\DomainFasta");
            if (!Directory.Exists(ProtCidSettings.dirSettings.seqFastaPath))
            {
                Directory.CreateDirectory(ProtCidSettings.dirSettings.seqFastaPath);
            }

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\DomainFasta", "\\WebDomainFasta");
            if (!Directory.Exists(webFastaFileDir))
            {
                Directory.CreateDirectory(webFastaFileDir);
            }

            StreamWriter lsFileWriter = new StreamWriter(Path.Combine(webFastaFileDir, "relSeq-newls.txt"), true);
            string relationSeqFile = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            string srcRelSeqFile = "";
            string destRelSeqFile = "";
            string relationName = "";
            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    string[] fastaSeqFiles = GetRelationClusterSeqFastaFiles(relSeqId);

                    relationName = DownloadableFileName.GetDomainRelationName(relSeqId);
                    relationSeqFile = "Seq_" + relationName + ".tar.gz";
                    fileCompress.RunTar(relationSeqFile, fastaSeqFiles, ProtCidSettings.dirSettings.seqFastaPath, true);


                    lsFileWriter.WriteLine(relationSeqFile);
                    lsFileWriter.Flush();

                    // move the tar file to the web folder
                    srcRelSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, relationSeqFile);
                    destRelSeqFile = Path.Combine(webFastaFileDir, relationSeqFile);
                    if (File.Exists(destRelSeqFile))
                    {
                        File.Delete(destRelSeqFile);
                    }
                    File.Move(srcRelSeqFile, destRelSeqFile);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " Writing sequences to fasta files errors: " + ex.Message);
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
        public string[] GetRelationClusterSeqFastaFiles(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable familyCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamCode1 = familyCodeTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            string pfamCode2 = familyCodeTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            bool isSeqBExist = false;
            if (pfamCode1 != pfamCode2)
            {
                isSeqBExist = true;
            }

            queryString = string.Format("Select Distinct ClusterID From PfamDomainClusterInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> fastaSeqFileList = new List<string>();
            int clusterId = 0;
            string seqFileName = "";
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                seqFileName = "Cluster" + relSeqId.ToString() + "A_" + clusterId.ToString() + ".fasta";
                fastaSeqFileList.Add(seqFileName);
                if (isSeqBExist)
                {
                    seqFileName = "Cluster" + relSeqId.ToString() + "B_" + clusterId.ToString() + ".fasta";
                    fastaSeqFileList.Add(seqFileName);
                }
            }
            seqFileName = "Group" + relSeqId.ToString() + "A.fasta";
            fastaSeqFileList.Add(seqFileName);
            if (isSeqBExist)
            {
                seqFileName = "Group" + relSeqId.ToString() + "B.fasta";
                fastaSeqFileList.Add(seqFileName);
            }

            string[] fastaSeqFiles = new string[fastaSeqFileList.Count];
            fastaSeqFileList.CopyTo(fastaSeqFiles);
            return fastaSeqFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompressBigRelations()
        {
            int[] relSeqIds = { 2055, 2124, 2178, 11619, 11642, 14647, 14811 };


            ProtCidSettings.dirSettings.seqFastaPath = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\fasta", "\\DomainFasta");

            webFastaFileDir = ProtCidSettings.dirSettings.seqFastaPath.Replace("\\DomainFasta", "\\WebDomainFasta");

            List<string> seqFastaFileList = new List<string>();
            string relationSeqFile = "";
            string srcRelSeqFile = "";
            string destRelSeqFile = "";
      //      string relationFolder = "";
            string relationName = "";
            foreach (int relSeqId in relSeqIds)
            {
                seqFastaFileList.Clear();
                string[] relFastaFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + relSeqId.ToString () + "A_*.fasta");
                foreach (string fastaFile in relFastaFiles)
                {
                    FileInfo fileInfo = new FileInfo(fastaFile);
                    seqFastaFileList.Add(fileInfo.Name);
                }
                relFastaFiles = Directory.GetFiles(ProtCidSettings.dirSettings.seqFastaPath, "Cluster" + relSeqId.ToString() + "B_*.fasta");
                foreach (string fastaFile in relFastaFiles)
                {
                    FileInfo fileInfo = new FileInfo(fastaFile);
                    seqFastaFileList.Add(fileInfo.Name);
                }
                seqFastaFileList.Add ("Group" + relSeqId.ToString () + "A.fasta");
                if (File.Exists(Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, "Group" + relSeqId.ToString() + "B.fasta")))
                {
                    seqFastaFileList.Add("Group" + relSeqId.ToString() + "B.fasta");
                }
                string[] seqFastaFiles = new string[seqFastaFileList.Count];
                seqFastaFileList.CopyTo(seqFastaFiles);

     //           relationFolder = MoveSeqFastaFilesToGroupFolder (seqFastaFiles, ProtCidSettings.dirSettings.seqFastaPath, relSeqId);

             //   relationSeqFile = TarFastaFilesOnFolder (relSeqId, relationFolder);
                relationName = DownloadableFileName.GetDomainRelationName(relSeqId);
                string seqTarFile = "Seq_" + relationName + ".tar.gz";
                fileCompress.RunTar(seqTarFile, seqFastaFiles, ProtCidSettings.dirSettings.seqFastaPath, true);

                // move the tar file to the web folder
                srcRelSeqFile = Path.Combine(ProtCidSettings.dirSettings.seqFastaPath, relationSeqFile);
                destRelSeqFile = Path.Combine(webFastaFileDir, relationSeqFile);
                if (File.Exists(destRelSeqFile))
                {
                    File.Delete(destRelSeqFile);
                }
                File.Move(srcRelSeqFile, destRelSeqFile);
            }
        }

        

        public void GetWrongRelSeqIDs()
        {
            StreamReader dataReader = new StreamReader("WrongDomainInterfaces.txt");
            string line = "";
            int origRelSeqId = 0;
            int relSeqId = 0;
            long domainId1 = 0;
            long domainId2 = 0;
            string pfamId1 = "";
            string pfamId2 = "";
            string updateString = "";
            DbUpdate dbUpdate = new DbUpdate ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                origRelSeqId = Convert.ToInt32(fields[0]);
                domainId1 = Convert.ToInt64(fields[2]);
                domainId2 = Convert.ToInt64(fields[3]);
                pfamId1 = GetDomainPfam(domainId1);
                if (domainId1 != domainId2)
                {
                    pfamId2 = GetDomainPfam(domainId2);
                }
                else
                {
                    pfamId2 = pfamId1;
                }
                relSeqId = GetRelSeqID(pfamId1, pfamId2);
                updateString = string.Format("Update PfamDomainInterfaces Set RelSeqID = {0} " +
                    " Where RelSeqID = {1} AND PdbID = '{2}' AND DOmainID1 = {3} AND DomainID2 = {4};", 
                    relSeqId, origRelSeqId, fields[1], domainId1, domainId2);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
            }
            dataReader.Close();
    /*        StreamWriter dataWriter = new StreamWriter("WrongDomainInterfaces.txt");
            ArrayList entryList = new ArrayList();
            string queryString = "Select Distinct RelSeqID, PdbID, DomainID1, DomainID2 From PfamDomainInterfaces;";
            DataTable relDomainPairTable = dbQuery.Query(queryString);
            int relSeqId = 0;
            long domainId1 = 0;
            long domainId2 = 0;
            string pfamId1 = "";
            string pfamId2 = "";
            string pdbId = "";
            queryString = "Select DomainID, Pfam_ID From PdbPfam;";
            DataTable domainTable = dbQuery.Query(queryString);
            foreach (DataRow domainPairRow in relDomainPairTable.Rows)
            {
                try
                {
                    relSeqId = Convert.ToInt32(domainPairRow["RelSeqID"].ToString());
                    domainId1 = Convert.ToInt64(domainPairRow["DomainID1"].ToString());
                    domainId2 = Convert.ToInt64(domainPairRow["DomainID2"].ToString());
                    pdbId = domainPairRow["PdbID"].ToString();
                    string[] pfamCodes = GetRelationString(relSeqId);
                    pfamId1 = GetDomainPfam(domainId1, domainTable);
                    if (domainId1 != domainId2)
                    {
                        pfamId2 = GetDomainPfam(domainId2, domainTable);
                    }
                    else
                    {
                        pfamId2 = pfamId1;
                    }
                    if ((pfamId1 == pfamCodes[0] && pfamId2 == pfamCodes[1]) ||
                        (pfamId1 == pfamCodes[1] && pfamId2 == pfamCodes[0]))
                    {
                        continue;
                    }
                    if (!entryList.Contains(relSeqId.ToString() + "_" + pdbId))
                    {
                        entryList.Add(relSeqId.ToString() + "_" + pdbId);
                    }
                    dataWriter.WriteLine(relSeqId.ToString() + "\t" + pdbId + "\t" + domainId1.ToString() + "\t" + domainId2.ToString());
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + "\t" + pdbId + "\t" + 
                        domainId1.ToString() + "\t" + domainId2.ToString() + " errors: " + ex.Message);
                }
            }
            dataWriter.Close();

            dataWriter = new StreamWriter("WrongRelEntries.txt");
            foreach (string relEntry in entryList)
            {
                dataWriter.WriteLine(relEntry);
            }
            dataWriter.Close();*/
        }

        private string[] GetRelationString(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable familyCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] familyCodes = new string[2];
            familyCodes[0] = familyCodeTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            familyCodes[1] = familyCodeTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return familyCodes;
        }

        private int GetRelSeqID(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation " + 
                " WHere (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " +
                " (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }

        private string GetDomainPfam(long domainId, DataTable pfamTable)
        {
            DataRow[] domainRows = pfamTable.Select(string.Format("DomainID = '{0}'", domainId));
            if (domainRows.Length > 0)
            {
                return domainRows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return "";
        }

        private string GetDomainPfam(long domainId)
        {
            string querystring = string.Format("Select Pfam_ID From PdbPfam Where DomainID = {0};", domainId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( querystring);
            if (pfamDomainTable.Rows.Count > 0)
            {
                return pfamDomainTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion
    }
}
