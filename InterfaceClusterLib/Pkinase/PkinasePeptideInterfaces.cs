using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.Pkinase
{
    public class PkinasePeptideInterfaces
    {
        private DbQuery dbQuery = new DbQuery();

        /// <summary>
        /// 
        /// </summary>
        public void AddPeptideSequencesInfoToClusterFile()
        {
            ProtCidSettings.LoadDirSettings();
            AppSettings.LoadParameters();
            AppSettings.LoadSymOps();

            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath;
            }

            string clusterFile = @"D:\DbProjectData\pfam\PfamPeptide\Pkinase\cutoff3\PfamPepInterfaceClusters3.txt";
            string clusterSequenceFile = @"D:\DbProjectData\pfam\PfamPeptide\Pkinase\cutoff3\PkinaseCluster2PeptideSeq.txt";
            StreamReader dataReader = new StreamReader(clusterFile);
            StreamWriter dataWriter = new StreamWriter(clusterSequenceFile);
            string line = dataReader.ReadLine(); // header line
            int selectedClusterId = 2;
            int clusterId = 0;
            string pdbId = "";
            int domainInterfaceId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                clusterId = Convert.ToInt32(fields[1]);
                if (clusterId == selectedClusterId)
                {
                    pdbId = fields[2];
                    domainInterfaceId = Convert.ToInt32(fields[3]);

                    dataWriter.WriteLine(line);
                    string[] pdbDbSeqInfos = GetPeptideSeqInfo(pdbId, domainInterfaceId);
                    if (pdbDbSeqInfos == null)
                    {
                        dataWriter.WriteLine();
                    }
                    else
                    {
                        foreach (string seq in pdbDbSeqInfos)
                        {
                            dataWriter.WriteLine(seq);
                        }
                        dataWriter.WriteLine();
                    }
                }
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string[] GetPeptideSeqInfo(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pepAsymChain = peptideInterfaceTable.Rows[0]["PepAsymChain"].ToString().TrimEnd();
            string[] dbSeqInfos = GetDbSequenceInfo(pdbId, pepAsymChain);
            if (dbSeqInfos == null)
            {
                return null;
            }
            DataTable residueChangeSiftsTable = GetResidueChangeSiftsTable(pdbId, pepAsymChain);
            DataTable residueChangeXmlTable = GetResidueChangeXmlTable(pdbId, pepAsymChain);

            string[] pdbDbSequences = new string[6]; // 0: pdb, 1: db sifts, 2: db xml
            string[] pdbSeqNumbers = dbSeqInfos[2].Split(',');
            string[] unpSeqNumbers = dbSeqInfos[3].Split(',');
            string siftsMarkSeq = "";
            string xmlMarkSeq = "";
            string xmlMarkList = "";
            string siftMarkList = "";
            int pdbSeqId = 0;
            string residueDifSifts = "";
            string residueDifXml = "";
            for (int i = 0; i < pdbSeqNumbers.Length; i++)
            {
                pdbSeqId = Convert.ToInt32(pdbSeqNumbers[i]);
                residueDifSifts = GetResidueDifDetails(pdbSeqId, residueChangeSiftsTable);
                residueDifXml = GetResidueDifDetails(pdbSeqId, residueChangeXmlTable);
                if (residueDifSifts != "")
                {
                    siftsMarkSeq += residueDifSifts[0].ToString ();
                    siftMarkList += ("(" + residueDifSifts + ")");
                }
                else
                {
                    siftsMarkSeq += " ";
                }
                if (residueDifXml != "")
                {
                    xmlMarkSeq += "*";
                    xmlMarkList += ("(" + residueDifXml + ")");
                }
                else
                {
                    xmlMarkSeq += " ";
                }
            }
            pdbDbSequences[0] = dbSeqInfos[0]; // pdb sequence
            pdbDbSequences[1] = dbSeqInfos[1]; // unp sequence
            pdbDbSequences[2] = siftsMarkSeq;  // sifts mark 
            pdbDbSequences[3] = siftMarkList; // sifts details
            pdbDbSequences[4] = xmlMarkSeq; // xml marks
            pdbDbSequences[5] = xmlMarkList;  // change details from XML
            return pdbDbSequences;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbSeqId"></param>
        /// <param name="residueChangeTable"></param>
        /// <returns></returns>
        private string GetResidueDifDetails(int pdbSeqId, DataTable residueChangeTable)
        {
            DataRow[] residueChangeRows = residueChangeTable.Select(string.Format ("SeqNum = '{0}'", pdbSeqId ));
            if (residueChangeRows.Length > 0)
            {
                return residueChangeRows[0]["Details"].ToString().TrimEnd() + 
                    "(" + residueChangeRows[0]["DbResidue"].ToString ().TrimEnd () + " -> " + residueChangeRows[0]["Residue"].ToString ().TrimEnd () + ")";
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private DataTable GetResidueChangeSiftsTable (string pdbId, string asymChain)
        {
            
            string queryString = string.Format("Select * From PdbDbRefSeqDifSifts Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqDifTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return seqDifTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private DataTable GetResidueChangeXmlTable(string pdbId, string asymChain)
        {
            string authorChain = GetAuthorChain(pdbId, asymChain);
            string queryString = string.Format("Select * From PdbDbRefSeqDifXml Where PdbID = '{0}' AND AuthorChain = '{1}';", pdbId, authorChain);
            DataTable seqDifTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return seqDifTable;
        }

        private string GetAuthorChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select AuthorChain From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable authChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (authChainTable.Rows.Count > 0)
            {
                return authChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string[] GetDbSequenceInfo(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Sequence, DbSequence, SeqNumbers, DbSeqNumbers From PdbDbRefSeqAlignSifts Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] seqInfos = null;
            if (seqInfoTable.Rows.Count > 0)
            {
                seqInfos = new string[4];
                seqInfos[0] = seqInfoTable.Rows[0]["Sequence"].ToString().TrimEnd();
                seqInfos[1] = seqInfoTable.Rows[0]["DbSequence"].ToString().TrimEnd();
                seqInfos[2] = seqInfoTable.Rows[0]["SeqNumbers"].ToString().TrimEnd();
                seqInfos[3] = seqInfoTable.Rows[0]["DbSeqNumbers"].ToString().TrimEnd();
            }
            return seqInfos;
        }
    }
}
