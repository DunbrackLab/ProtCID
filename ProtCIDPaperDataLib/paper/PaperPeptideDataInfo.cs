using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using CrystalInterfaceLib.Crystal;
using InterfaceClusterLib.DomainInterfaces;
using ProtCidSettingsLib;
using AuxFuncLib;
using DbLib;

namespace ProtCIDPaperDataLib.paper
{
    public class PaperPeptideDataInfo : PaperDataInfo
    {
        private string pepDataDir = @"X:\Qifang\Paper\protcid_update\data_v31\PfamPeptide";

        public PaperPeptideDataInfo ()
        {
            pepDataDir = Path.Combine(dataDir, "PfamPeptide");
        }

        #region pfam-peptide -- paper table    
        public void PrintPeptideClustersSumInfo()
        {
            string pfamPepSumInfoFile = Path.Combine(pepDataDir, "PfamPepClusterSumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pfamPepSumInfoFile, true);
            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("ALL");
            dataWriter.WriteLine("#Pfams=" + pepPfamTable.Rows.Count);
            dataWriter.WriteLine("#Entries=" + pepEntryTable.Rows.Count);
            string[] pepUnpList = GetPepUniProts();
            dataWriter.WriteLine("#UNPs=" + pepUnpList.Length);
            dataWriter.WriteLine();
            int[] numOfUnpSeqs = { 2, 5, 10, 20 };
            foreach (int numOfUnpSeq in numOfUnpSeqs)
            {
                //           dataWriter.WriteLine("#Entries = " + numOfUnpSeq.ToString () + ", minimum sequence identity < 90%");
                dataWriter.WriteLine("#UnpSeqs >= " + numOfUnpSeq.ToString());
                PrintPeptideClustersSumInfo(numOfUnpSeq, dataWriter);
                dataWriter.WriteLine();
            }

            dataWriter.Close();
        }

        /// <summary>
        /// the uniprots interacting with peptide
        /// </summary>
        /// <returns></returns>
        public string[] GetPepUniProts ()
        {
            string queryString = "Select Distinct PdbID, DomainID From PfamPeptideInterfaces;";
            DataTable pepDomainTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct PdbID, UnpCode From PfamPepInterfaceClusters;";
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> clusterEntryList = new List<string>();
            List<string> unpList = new List<string>();
            string pdbId = "";
            string unpId = "";
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                unpId = entryRow["UnpCode"].ToString().TrimEnd();
                if (! clusterEntryList.Contains (pdbId))
                {
                    clusterEntryList.Add(pdbId);
                }
                if (! unpList.Contains (unpId))
                {
                    unpList.Add(unpId);
                }
            }
            clusterEntryList.Sort();
            long domainId = 0;
            foreach (DataRow pepEntryRow in pepDomainTable.Rows)
            {
                pdbId = pepEntryRow["PdbID"].ToString();
                if (clusterEntryList.BinarySearch (pdbId) > -1)
                {
                    continue;
                }
                domainId = Convert.ToInt64(pepEntryRow["DOmainID"].ToString ());
                unpId = GetDomainUnpCode(pdbId, domainId);
                if (! unpList.Contains (unpId))
                {
                    unpList.Add(unpId);
                }
            }
            return unpList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string GetDomainUnpCode (string pdbId, long domainId)
        {
            string queryString = string.Format("Select Distinct UnpID From UnpPdbfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable domainUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (domainUnpTable.Rows.Count > 0)
            {
                return domainUnpTable.Rows[0]["UnpID"].ToString().TrimEnd();
            }
            return "-";
        }

        public void PrintPfamPeptideRelationsSumInfo()
        {
            string pfamId = "Trypsin";
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, pfamId + "pepRelationSumInfo.txt"));
            string queryString = string.Format("Select PDbID, DomainInterfaceID, AsymChain, PepAsymChain From PfamPeptideInterfaces " +
                "Where PfamID = '{0}' AND CrystalPack = '0'", pfamId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> protUnpList = new List<string>();
            List<string> entryList = new List<string>();
            List<string> peptideList = new List<string>();
            Dictionary<string, string> protChainUnpDict = new Dictionary<string, string>();
            Dictionary<string, string> pepChainSeqDict = new Dictionary<string, string>();
            string pdbId = "";
            string protAsymChain = "";
            string pepAsymChain = "";
            string protUnp = "";
            string pepSeq = "";
            foreach (DataRow dataRow in pepInterfaceTable.Rows)
            {
                pdbId = dataRow["PdbID"].ToString();
                protAsymChain = dataRow["AsymChain"].ToString().TrimEnd();
                pepAsymChain = dataRow["PepAsymChain"].ToString().TrimEnd();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
                if (!protChainUnpDict.ContainsKey(pdbId + protAsymChain))
                {
                    protUnp = GetEntryAsymChainUnp(pdbId, protAsymChain);
                    protChainUnpDict.Add(pdbId + protAsymChain, protUnp);
                    if (!protUnpList.Contains(protUnp))
                    {
                        protUnpList.Add(protUnp);
                    }
                }
                if (!pepChainSeqDict.ContainsKey(pdbId + pepAsymChain))
                {
                    pepSeq = GetEntryAsymChainSequence(pdbId, pepAsymChain);
                    pepChainSeqDict.Add(pdbId + pepAsymChain, pepSeq);
                    if (!peptideList.Contains(pepSeq))
                    {
                        peptideList.Add(pepSeq);
                    }
                }
            }
            queryString = string.Format("Select Distinct DBCode From PdbDbRefSifts, PdbPfam Where Pfam_ID = '{0}' AND " +
                "PdbPfam.PdbID = PdbDbRefSifts.PdbID AND PdbPfam.EntityID = PdbDbRefSifts.EntityID AND DbName = 'UNP';", pfamId);
            DataTable pfamUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            dataWriter.WriteLine(pfamId + "-peptide interfaces");
            dataWriter.WriteLine("#Protein UNPs = " + protUnpList.Count);
            dataWriter.WriteLine("#peptides = " + peptideList.Count);
            dataWriter.WriteLine("#entries = " + entryList.Count);
            dataWriter.WriteLine(pfamId + " Pfam " + " #Proteins = " + pfamUnpTable.Rows.Count);
            dataWriter.Close();

        }

        private string GetEntryAsymChainUnp(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select DbCode From PdbDbRefSifts, AsymUnit Where AsymUnit.PdbID = '{0}' AND AsymID = '{1}' AND " +
                "AsymUnit.PdbID = PdbDbRefSifts.PdbID AND AsymUnit.EntityID = PdbDbRefSifts.EntityID AND DbName = 'UNP';", pdbId, asymChain);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (dbCodeTable.Rows.Count > 0)
            {
                return dbCodeTable.Rows[0]["DbCode"].ToString().TrimEnd();
            }
            return "-";
        }

        private string GetEntryAsymChainSequence(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (seqTable.Rows.Count > 0)
            {
                return seqTable.Rows[0]["Sequence"].ToString();
            }
            return "-";
        }

        public void PrintNumsOfProteinsPeptides()
        {
            string protPepClusterNumsFile = Path.Combine(pepDataDir, "PfamPepClustersProtPeptideNums.txt");
            StreamWriter protPepClusterNumsWriter = new StreamWriter(protPepClusterNumsFile);
            string protPepNumsFile = Path.Combine(pepDataDir, "DistinctProtPeptideNums.txt");
            StreamWriter protPepNumsWriter = new StreamWriter(protPepNumsFile);
            string queryString = "Select PfamID, ClusterID, NumUnpSeqs, NumPepSeqs From PfamPepClusterSumInfo;";
            DataTable numTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, int> protPepNumClusterNumDict = new Dictionary<string, int>();
            Dictionary<string, string> protPepNumClusterListDict = new Dictionary<string, string>();
            string numPair = "";
            foreach (DataRow clusterRow in numTable.Rows)
            {
                protPepClusterNumsWriter.WriteLine(ParseHelper.FormatDataRow(clusterRow));
                numPair = clusterRow["NumUnpSeqs"].ToString() + "\t" + clusterRow["NumPepSeqs"].ToString();

                if (protPepNumClusterNumDict.ContainsKey(numPair))
                {
                    protPepNumClusterNumDict[numPair] = protPepNumClusterNumDict[numPair] + 1;
                    protPepNumClusterListDict[numPair] = protPepNumClusterListDict[numPair] + "," + clusterRow["PfamID"].ToString() + "_" + clusterRow["ClusterID"].ToString();
                }
                else
                {
                    protPepNumClusterNumDict.Add(numPair, 1);
                    protPepNumClusterListDict.Add(numPair, clusterRow["PfamID"].ToString() + "_" + clusterRow["ClusterID"].ToString());
                }
            }
            foreach (string keyNumPair in protPepNumClusterNumDict.Keys)
            {
                protPepNumsWriter.WriteLine(keyNumPair + "\t" + protPepNumClusterNumDict[keyNumPair].ToString() +
                    "\t" + protPepNumClusterListDict[keyNumPair].TrimEnd(','));
            }
            protPepClusterNumsWriter.Close();
            protPepNumsWriter.Close();
        }

        private void PrintPeptideClustersSumInfo(int numOfUnpSeqCutoff, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select Distinct PfamID, ClusterID From PfamPepClusterSumInfo " +
                //         " where NumEntries >= {0} AND MinSeqIdentity < 90", numOfUnpSeqCutoff);
                     " where NumUnpSeqs >= {0};", numOfUnpSeqCutoff);
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            List<string> unpList = new List<string>();
            List<string> peptideList = new List<string>();
            List<string> pfamList = new List<string>();
            string pfamId = "";
            string prePfamId = "";
            DataTable pfamPepInterfaceTable = null;
            string pdbId = "";
            string unpId = "";
            string domainInterfaceId = "";
            string pepAsymChain = "";
            string pepSequence = "";
            foreach (DataRow clusterRow in pepClusterTable.Rows)
            {
                pfamId = clusterRow["PfamID"].ToString();
                if (!pfamList.Contains(pfamId))
                {
                    pfamList.Add(pfamId);
                }
                if (prePfamId != pfamId)
                {
                    queryString = string.Format("Select PdbID, DomainInterfaceID, AsymChain, PepAsymChain From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
                    pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
                }
                queryString = string.Format("Select Distinct UnpCode, PdbID, DomainInterfaceID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};",
                   pfamId, clusterRow["ClusterID"]);
                DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                {
                    pdbId = interfaceRow["PdbID"].ToString();
                    unpId = interfaceRow["UnpCode"].ToString().TrimEnd();
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                    if (! unpList.Contains (unpId))
                    {
                        unpList.Add(unpId);
                    }
                    domainInterfaceId = interfaceRow["DomainInterfaceID"].ToString();
                    pepAsymChain = GetPeptideAsymChain(pdbId, domainInterfaceId, pfamPepInterfaceTable);
                    pepSequence = GetChainSequence (pdbId, pepAsymChain);
                    if (!peptideList.Contains(pepSequence))
                    {
                        peptideList.Add(pepSequence);
                    }
                }

            }
            dataWriter.WriteLine("#Clusters=" + pepClusterTable.Rows.Count);
            dataWriter.WriteLine("#Pfams=" + pfamList.Count);
            dataWriter.WriteLine("#Entries=" + entryList.Count);
            dataWriter.WriteLine("#UNPs=" + unpList.Count);
            dataWriter.WriteLine("#peptides=" + peptideList.Count);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="pepInterfaceTable"></param>
        /// <returns></returns>
        private string GetPeptideAsymChain(string pdbId, string domainInterfaceId, DataTable pepInterfaceTable)
        {
            DataRow[] pepInterfaceRows = pepInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (pepInterfaceRows.Length > 0)
            {
                return pepInterfaceRows[0]["PepAsymChain"].ToString().TrimEnd();
            }
            return "";
        }

        #region check no interacting peptides
        /// <summary>
        /// 
        /// </summary>
        public void CheckAllPeptidesWithInteractions()
        {
            string[] pepChains = GetPeptideSeqChains();
            string pdbId = "";
            string pepAsymChain = "";
            bool isMonomer = false;
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "NoInteractingPeptides.txt"));
            foreach (string pepChain in pepChains)
            {
                pdbId = pepChain.Substring(0, 4);
                pepAsymChain = pepChain.Substring(4, pepChain.Length - 4);
                isMonomer = false;
                if (!IsPeptideInteracting(pdbId, pepAsymChain, false))
                {
                    if (IsEntryMonomer(pdbId))
                    {
                        isMonomer = true;
                    }
                    if (isMonomer)
                    {
                        if (IsEntryNmr(pdbId))
                        {
                            dataWriter.WriteLine(pepChain + "\t1\t1");
                        }
                        else
                        {
                            dataWriter.WriteLine(pepChain + "\t1\t0");
                        }

                    }
                    else
                    {
                        dataWriter.WriteLine(pepChain + "\t0\t0");
                    }
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepAsymChain"></param>
        /// <returns></returns>
        private bool IsPeptideInteracting(string pdbId, string pepAsymChain, bool includeCP)
        {
            string queryString = "";
            if (includeCP)
            {
                queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND " +
                    "(AsymChain = '{1}' OR PepAsymChain = '{1}');", pdbId, pepAsymChain);
            }
            else
            {
                queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID = '{0}' AND " +
                     "(AsymChain = '{1}' OR PepAsymChain = '{1}') AND CrystalPack = '0';", pdbId, pepAsymChain);
            }
            DataTable pepInterfacesTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (pepInterfacesTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPeptideSeqChains()
        {
            string pepSeqFile = Path.Combine(pepDataDir, "PepSeqInfo.txt");
            List<string> pepSeqList = new List<string>();
            if (File.Exists(pepSeqFile))
            {
                StreamReader dataReader = new StreamReader(pepSeqFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(' ');
                    pepSeqList.Add(fields[0]);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter(pepSeqFile);
                string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
                DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                string sequence = "";
                foreach (DataRow seqRow in chainSeqTable.Rows)
                {
                    sequence = seqRow["Sequence"].ToString();
                    if (sequence.Length < 30)
                    {
                        dataWriter.WriteLine(seqRow["PdbID"].ToString() + seqRow["AsymID"].ToString().TrimEnd() + " " +
                            sequence + " " + sequence.Length.ToString());
                        pepSeqList.Add(seqRow["PdbID"].ToString() + seqRow["AsymID"].ToString().TrimEnd());
                    }
                }
                dataWriter.Close();
            }
            string[] pepChains = new string[pepSeqList.Count];
            pepSeqList.CopyTo(pepChains);
            return pepChains;
        }
        #endregion

        #region for pfam-peptide: Asp
        public void PrintPeptideSequences()
        {
            string interfaceFileDir = Path.Combine(pepDataDir, "Asp_pep\\Asp_2");
            string[] interfaceFiles = Directory.GetFiles(interfaceFileDir, "*.cryst");
            StreamWriter seqWriter = new StreamWriter(Path.Combine(pepDataDir, "Asp_pep\\Asp_pepseq.txt"));
            foreach (string interfaceFile in interfaceFiles)
            {
                FileInfo fileInfo = new FileInfo(interfaceFile);
                string peptideResidues = ReadPeptideResiduesFromFile(interfaceFile);
                seqWriter.WriteLine(fileInfo.Name.Replace(".cryst", "").PadRight(50) + peptideResidues);
            }
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        private string ReadPeptideResiduesFromFile(string interfaceFile)
        {
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            int seqId = 0;
            List<int> seqIdList = new List<int>();
            string residues = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] atomFields = ParseHelper.ParsePdbAtomLine(line);
                    if (atomFields[5] == "B")
                    {
                        seqId = Convert.ToInt32(atomFields[6]);
                        if (seqIdList.Contains(seqId))
                        {
                            continue;
                        }
                        seqIdList.Add(seqId);
                        //    residueList.Add(atomFields[4]);
                        residues += (atomFields[4] + "-");
                    }
                }
            }
            dataReader.Close();
            residues = residues.TrimEnd('-');
            return residues;
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamPeptideAspInteractingResidues()
        {
            if (ProtCidSettings.buCompConnection == null)
            {
                ProtCidSettings.buCompConnection = new DbConnect();
                ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.baInterfaceDbPath;
            }

            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "Asp_pep\\Asp_PepInteractions.txt"));
            string queryString = "Select * From PfamPepInterfaceClusters WHere PfamID = 'Asp' AND ClusterID = 2 Order By PdbID, DomainInterfaceId;";
            DataTable clusterPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> clusterEntryList = new List<string>();
            //      string protResidue = "ASP";
            string[] motifs = { "DTG", "DSG" };
            string pdbId = "";
            int domainInterfaceId = 0;
            int relSeqId = 0;
            int chainInterfaceId = 0;
            long domainId = -1;
            foreach (DataRow interfaceRow in clusterPepInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                if (clusterEntryList.Contains(pdbId))
                {
                    continue;
                }
                clusterEntryList.Add(pdbId);
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                relSeqId = Convert.ToInt32(interfaceRow["RelSeqId"].ToString());
                chainInterfaceId = GetChainInterfaceId(relSeqId, pdbId, domainInterfaceId, out domainId);
                DataTable atomPairTable = GetChainInterfaceAtomPairsTable(pdbId, chainInterfaceId);
                int[] aspPositions = GetResiduePositions(pdbId, domainId, motifs);
                GetAspInteractingAtomPairs(pdbId, aspPositions, atomPairTable, dataWriter);
            }
            dataWriter.Close();
        }

        private void GetAspInteractingAtomPairs(string pdbId, int[] aspPositions, DataTable atomPairTable, StreamWriter dataWriter)
        {
            foreach (int aspPosition in aspPositions)
            {
                DataRow[] atomPairRows = atomPairTable.Select(string.Format("SeqID = '{0}'", aspPosition));
                dataWriter.WriteLine(ParseHelper.FormatDataRows(atomPairRows));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <returns></returns>
        private int[] GetResiduePositions(string pdbId, long domainId, string[] motifs)
        {
            string queryString = string.Format("Select EntityID, SeqStart, SeqEnd From PdbPfam Where PdbID ='{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable entityIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int entityId = -1;
            string sequence = "";
            int seqStart = 0;
            int seqEnd = 0;
            string domainSequence = "";
            List<int> motifIndexList = new List<int>();
            if (entityIdTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityIdTable.Rows[0]["EntityID"].ToString());
                sequence = GetEntitySequence(pdbId, entityId);
                seqStart = Convert.ToInt32(entityIdTable.Rows[0]["SeqStart"].ToString());
                seqEnd = Convert.ToInt32(entityIdTable.Rows[0]["SeqEnd"].ToString());
                domainSequence = sequence.Substring(seqStart - 1, seqEnd - seqStart + 1);
                foreach (string motif in motifs)
                {
                    int[] motifIndexes = FindMotifIndexes(domainSequence, motif);
                    foreach (int motifIndex in motifIndexes)
                    {
                        motifIndexList.Add(motifIndex + seqStart);
                    }
                }
            }
            motifIndexList.Sort();
            int[] allMotifIndexes = new int[motifIndexList.Count];
            motifIndexList.CopyTo(allMotifIndexes);
            return allMotifIndexes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSequence"></param>
        /// <param name="motif"></param>
        /// <returns></returns>
        private int[] FindMotifIndexes(string domainSequence, string motif)
        {
            List<int> motifIndexList = new List<int>();
            string subDomainSequence = domainSequence;
            int motifIndex = -1;
            int subSeqStartPos = 0;
            while (true)
            {
                motifIndex = subDomainSequence.IndexOf(motif);
                if (motifIndex < 0)
                {
                    break;
                }
                motifIndexList.Add(motifIndex + subSeqStartPos);
                if (motifIndex + motif.Length >= subDomainSequence.Length)
                {
                    break;
                }
                subSeqStartPos += (motifIndex + motif.Length);
                subDomainSequence = subDomainSequence.Substring(motifIndex + motif.Length, subDomainSequence.Length - motifIndex - motif.Length);
            }
            int[] motifIndexes = new int[motifIndexList.Count];
            motifIndexList.CopyTo(motifIndexes);
            return motifIndexes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="protResidue"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceAtomPairsTable(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From ChainPeptideAtomPairs Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable atomPairTable = ProtCidSettings.buCompQuery.Query(queryString);
            return atomPairTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private int GetChainInterfaceId(int relSeqId, string pdbId, int domainInterfaceId, out long domainId)
        {
            string queryString = string.Format("Select InterfaceID, DomainID From PfamPeptideInterfaces " +
                " Where RelSeqId = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2};", relSeqId, pdbId, domainInterfaceId);
            DataTable interfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int interfaceId = -1;
            domainId = -1;
            if (interfaceIdTable.Rows.Count > 0)
            {
                interfaceId = Convert.ToInt32(interfaceIdTable.Rows[0]["InterfaceID"].ToString());
                domainId = Convert.ToInt64(interfaceIdTable.Rows[0]["DomainID"].ToString());
            }
            return interfaceId;
        }
        #endregion

        #region for pfam-peptide: SH2
        public void GetSH2Sequences()
        {
            StreamReader dataReader = new StreamReader(Path.Combine(pepDataDir, "SeqSH2\\ClusterSH2A_1.fasta"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "SeqSH2\\ClusterSH2A_1_new.fasta"));
            string line = "";
            string pdbId = "";
            int entityId = 0;
            string unpCode = "";
            string entryDomain = "";
            string sequence = "";
            Dictionary<string, string> unpSequenceHash = new Dictionary<string, string>();
            Dictionary<string, List<string>> unpPdbHash = new Dictionary<string, List<string>>();
            Dictionary<string, string> unpLongestSeqPdbHash = new Dictionary<string, string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line[0] == '>')
                {
                    string[] fields = line.Split(' ');
                    pdbId = fields[0].Substring(1, 4);
                    entryDomain = fields[0].Substring(1, fields[0].Length - 1);
                    entityId = Convert.ToInt32(fields[2]);
                    unpCode = GetEntityUnpCode(pdbId, entityId);
                    sequence = dataReader.ReadLine();
                    if (unpSequenceHash.ContainsKey(unpCode))
                    {
                        string existSequence = (string)unpSequenceHash[unpCode];
                        if (sequence.Length > existSequence.Length)
                        {
                            unpSequenceHash[unpCode] = sequence;
                            unpLongestSeqPdbHash[unpCode] = entryDomain;
                        }
                        unpPdbHash[unpCode].Add(entryDomain);
                    }
                    else
                    {
                        unpSequenceHash.Add(unpCode, sequence);
                        unpLongestSeqPdbHash.Add(unpCode, entryDomain);
                        List<string> domainList = new List<string>();
                        domainList.Add(entryDomain);
                        unpPdbHash.Add(unpCode, domainList);
                    }
                }
            }
            dataReader.Close();
            foreach (string lsUnpCode in unpSequenceHash.Keys)
            {
                dataWriter.WriteLine(">" + lsUnpCode + " | " + (string)unpLongestSeqPdbHash[lsUnpCode] + " " + FormatArrayString(unpPdbHash[lsUnpCode].ToArray()));
                dataWriter.WriteLine((string)unpSequenceHash[lsUnpCode]);
            }
            dataWriter.Close();
        }

        public void AlignPdbSequencesByHmm()
        {
            StreamReader dataReader = new StreamReader(Path.Combine(pepDataDir, "SeqSH2\\ClusterSH2A_1_new.fasta"));
            StreamWriter dataAlignWriter = new StreamWriter(Path.Combine(pepDataDir, "SeqSH2\\ClusterSH2A_1_Align.fasta"));
            string line = "";
            string repDomain = "";
            string pdbId = "";
            long domainId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.Substring(0, 1) == ">")
                {
                    string[] fields = line.Split(' ');
                    repDomain = fields[2];
                    pdbId = repDomain.Substring(0, 4);
                    domainId = Convert.ToInt64(repDomain.Substring(4, repDomain.Length - 4));
                }
            }
            dataReader.Close();
            dataAlignWriter.Close();
        }

        private string[] GetHMMAlignSequence(string pdbId, long domainId)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintPepInteractingHmmSites()
        {
            DomainInterfaceStatInfo statInfo = new DomainInterfaceStatInfo();
            statInfo.PrintPepInteractingHmmSites();
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetPepClusterSumInfo()
        {
            string pfamId = "SH2";
            int clusterId = 1;
            string pfamPepSumFile = Path.Combine(pepDataDir, pfamId + "_pep1sumInfo.txt");
            string queryString = string.Format("Select PdbID, DomainInterfaceID, UnpCode, PfamArch, PepPfamID, PepUnpCode, SeqLength, PepSeqLength, SurfaceArea " +
                " From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter(pfamPepSumFile);
            dataWriter.WriteLine("PdbID\tDomainInterfaceID\tUnpCode\tPfamArch\tPepPfamID\tPepUnpCode\tLength\tPepLength\tSurfaceArea");
            foreach (DataRow pepInterfaceRow in clusterPepInterfaceTable.Rows)
            {
                dataWriter.WriteLine(ParseHelper.FormatDataRow(pepInterfaceRow));
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        public void CombinePfamPepSeqInfo()
        {
            string pfamPepSumFile = Path.Combine(pepDataDir, "SH2_pep1sumInfo.txt");
            string pfamPepSeqFile = Path.Combine(pepDataDir, "SH21_PepHmmMapping_Arg.txt");
            DataTable pepHmmMapTable = ReadDataFileToTable(pfamPepSeqFile);
            DataTable pepSumInfoTable = ReadDataFileToTable(pfamPepSumFile);
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "SH2_pep1sumInfo_new.txt"));
            string headerLine = "";
            foreach (DataColumn col in pepSumInfoTable.Columns)
            {
                headerLine += (col.ColumnName + "\t");
            }
            headerLine = headerLine + "PepResidue\tResidue\tSeqIDs\tHmmSites\tUnpSeqIDs";
            dataWriter.WriteLine(headerLine);
            string pdbId = "";
            string domainInterfaceId = "";
            string dataLine = "";
            foreach (DataRow sumInfoRow in pepSumInfoTable.Rows)
            {
                pdbId = sumInfoRow["PdbID"].ToString();
                domainInterfaceId = sumInfoRow["DomainInterfaceID"].ToString();
                DataRow[] pepHmmMapRows = pepHmmMapTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
                if (pepHmmMapRows.Length > 0)
                {
                    dataLine = ParseHelper.FormatDataRow(sumInfoRow) + "\t" + pepHmmMapRows[0]["PepResidue"].ToString() + "\t" +
                        pepHmmMapRows[0]["Residues"].ToString() + "\t" + pepHmmMapRows[0]["SeqIDs"].ToString() + "\t" +
                        pepHmmMapRows[0]["HmmSites"].ToString() + "\t" + pepHmmMapRows[0]["UnpSeqIDs"].ToString();
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        public void FormatPfamPepHmmMappingTable()
        {
            string dataFile = Path.Combine(pepDataDir, "SH21_PepHmmMapping.txt");
            DataTable pepHmmMapTable = ReadDataFileToTable(dataFile);
            string residueSumInfoFile = Path.Combine(pepDataDir, "SH21_PResiduesInfo_dist4.txt");
            StreamWriter dataWriter = new StreamWriter(residueSumInfoFile);
            List<string> pResidueList = new List<string>();
            foreach (DataRow residueRow in pepHmmMapTable.Rows)
            {
                if (!pResidueList.Contains(residueRow["PepResidue"].ToString()))
                {
                    pResidueList.Add(residueRow["PepResidue"].ToString());
                }
            }
            List<string> entryList = new List<string>();
            List<int> hmmSiteList = new List<int>();
            List<string> distanceList = new List<string>();
            List<string> residueList = new List<string>();
            double avgDistance = 0;
            foreach (string pResidue in pResidueList)
            {
                entryList.Clear();
                hmmSiteList.Clear();
                distanceList.Clear();
                residueList.Clear();
                DataRow[] residueInfoRows = pepHmmMapTable.Select(string.Format("PepResidue = '{0}'", pResidue));
                foreach (DataRow residueInfoRow in residueInfoRows)
                {
                    if (!entryList.Contains(residueInfoRow["PdbID"].ToString()))
                    {
                        entryList.Add(residueInfoRow["PdbID"].ToString());
                    }
                    string[] hmmSites = residueInfoRow["HmmSites"].ToString().Split(',');
                    foreach (string hmmSite in hmmSites)
                    {
                        if (!hmmSiteList.Contains(Convert.ToInt32(hmmSite)))
                        {
                            hmmSiteList.Add(Convert.ToInt32(hmmSite));
                        }
                    }
                    string[] distances = residueInfoRow["Distances"].ToString().Split(',');
                    foreach (string distance in distances)
                    {
                        if (!distanceList.Contains(distance))
                        {
                            distanceList.Add(distance);
                        }
                    }
                    string[] residues = residueInfoRow["Residues"].ToString().Split(',');
                    foreach (string residue in residues)
                    {
                        if (!residueList.Contains(residue))
                        {
                            residueList.Add(residue);
                        }
                    }
                }
                hmmSiteList.Sort();
                residueList.Sort();
                avgDistance = GetAverageDistance(distanceList);
                dataWriter.WriteLine(pResidue + "\t" + FormatArrayString(hmmSiteList) + "\t" + avgDistance + "\t" + entryList.Count.ToString() + "\t" + FormatArrayString(residueList));
            }

            dataWriter.Close();
        }
        /*
                public void FormatPfamPepHmmMappingTable()
                {
                    string dataFile = @"C:\Paper\protcid_update\data\SH21_PepHmmMapping.txt";
                    DataTable pepHmmMapTable = ReadDataFileToTable(dataFile);
                    string residueSumInfoFile = @"C:\Paper\protcid_update\data\SH21_PResiduesInfo_dist4.txt";
                    StreamWriter dataWriter = new StreamWriter(residueSumInfoFile);
                    ArrayList pResidueList = new ArrayList();
                    foreach (DataRow residueRow in pepHmmMapTable.Rows)
                    {
                        if (!pResidueList.Contains(residueRow["PepResidue"].ToString()))
                        {
                            pResidueList.Add(residueRow["PepResidue"].ToString());
                        }
                    }
                    Hashtable residuePairEntryHash = new Hashtable();
                    Hashtable residuePairHmmSiteHash = new Hashtable();
                    Hashtable residuePairDistanceHash = new Hashtable();
                    double avgDistance = 0;
                    string residuePair = "";
                    string pdbId = "";
                    ArrayList residuePairList = new ArrayList();
                    foreach (string pResidue in pResidueList)
                    {
                        residuePairEntryHash.Clear ();
                        residuePairHmmSiteHash.Clear();
                        residuePairDistanceHash.Clear();
                        residuePairList.Clear();
                        DataRow[] residueInfoRows = pepHmmMapTable.Select(string.Format("PepResidue = '{0}'", pResidue));
                        foreach (DataRow residueInfoRow in residueInfoRows)
                        {
                            pdbId = residueInfoRow["PdbID"].ToString ();
                            string[] residues = residueInfoRow["Residues"].ToString().Split(',');
                            string[] hmmSites = residueInfoRow["HmmSites"].ToString().Split(',');
                            string[] distances = residueInfoRow["Distances"].ToString().Split(',');
                            for (int i = 0; i < hmmSites.Length; i++)
                            {
                                residuePair = pResidue + "\t" + hmmSites[i];
                                if (!residuePairList.Contains(residuePair))
                                {
                                    residuePairList.Add(residuePair);
                                }
                                if (residuePairEntryHash.ContainsKey(residuePair))
                                {
                                    ArrayList entryList = (ArrayList)residuePairEntryHash[residuePair];
                                    if (!entryList.Contains(pdbId))
                                    {
                                        entryList.Add(pdbId);
                                    }
                                }
                                else
                                {
                                    ArrayList entryList = new ArrayList();
                                    entryList.Add(pdbId);
                                    residuePairEntryHash.Add(residuePair, entryList);
                                }
                                if (residuePairHmmSiteHash.ContainsKey(residuePair))
                                {
                                    ArrayList hmmSiteList = (ArrayList)residuePairHmmSiteHash[residuePair];
                                    if (!hmmSiteList.Contains(residues[i]))
                                    {
                                        hmmSiteList.Add(residues[i]);
                                    } 
                                }
                                else
                                {
                                    ArrayList hmmSiteList = new ArrayList();
                                    hmmSiteList.Add(residues[i]);
                                    residuePairHmmSiteHash.Add(residuePair, hmmSiteList);
                                }
                                if (residuePairDistanceHash.ContainsKey (residuePair))
                                {
                                    ArrayList distanceList = (ArrayList)residuePairDistanceHash[residuePair];
                                    distanceList.Add(distances[i]);
                                }
                                else
                                {
                                     ArrayList distanceList = new ArrayList();
                                    distanceList.Add(distances[i]);
                                    residuePairDistanceHash.Add(residuePair, distanceList);
                                }
                            }
                        }
                        foreach (string lsResiduePair in residuePairList )
                        {
                            ArrayList entryList = (ArrayList)residuePairEntryHash[lsResiduePair];
                            ArrayList hmmSiteList = (ArrayList)residuePairHmmSiteHash[lsResiduePair];
                       //     hmmSiteList.Sort();
                            ArrayList distanceList = (ArrayList)residuePairDistanceHash[lsResiduePair];
                            avgDistance = GetAverageDistance(distanceList);
                            dataWriter.WriteLine(lsResiduePair + "\t" + FormatArrayString(hmmSiteList) + "\t" + avgDistance + "\t" + entryList.Count.ToString());
                        }
                    }

                    dataWriter.Close();
                }
                */
        private double GetAverageDistance(List<string> distanceList)
        {
            double distSum = 0;
            foreach (string distance in distanceList)
            {
                distSum += Convert.ToDouble(distance);
            }
            double avgDistance = distSum / (double)distanceList.Count;
            return avgDistance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns></returns>
        private DataTable ReadDataFileToTable(string dataFile)
        {
            DataTable dataTable = new DataTable();
            string line = "";
            StreamReader dataReader = new StreamReader(dataFile);
            string headerLine = dataReader.ReadLine();
            string[] cols = headerLine.Split('\t');
            foreach (string col in cols)
            {
                dataTable.Columns.Add(new DataColumn(col));
            }
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length == cols.Length)
                {
                    DataRow dataRow = dataTable.NewRow();
                    dataRow.ItemArray = fields;
                    dataTable.Rows.Add(dataRow);
                }
            }
            dataReader.Close();
            return dataTable;
        }
        public void WritePfamPepClusterInterfacesInfo()
        {
            string pfamId = "SH2";
            int clusterId = 1;
            string dataFile = Path.Combine(pepDataDir, pfamId + "_pep" + clusterId.ToString() + "sumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(dataFile);
            dataWriter.WriteLine("PDB ID\tDomainInterfaceID\tUNP Code\tPfam Arch\tPep Pfam ID\tPep UNP Code\tLength\tPep Length\tSurfaceArea");
            string queryString = string.Format("Select PdbID, DomainInterfaceID, UnpCode, PfamArch, PepPfamId, PepUnpCode, seqLength, PepSeqLength, SurfaceArea " +
                " From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow pepInterfaceRow in clusterPepInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                if (addedEntryList.Contains(pdbId))
                {
                    continue;
                }
                addedEntryList.Add(pdbId);
                dataWriter.WriteLine(ParseHelper.FormatDataRow(pepInterfaceRow));
            }
            dataWriter.Close();
        }


        public void GetPfamPepInterfaceClusterInfo()
        {
            DataTable pepSeqTable = GetPeptideSequenceTable();
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "PepPfamInfo.txt"));
            string queryString = "Select Distinct PfamId From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("#Pfams in Pfam-Peptide: " + pepPfamTable.Rows.Count.ToString());
            queryString = "Select Distinct PfamId, ClusterId From PfamPepInterfaceClusters;";
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("#Clusters: " + pepClusterTable.Rows.Count.ToString());
            queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepPfamEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("#Entry: " + pepPfamEntryTable.Rows.Count.ToString());
            int numOfPeptides = GetNumOfPeptides(pepSeqTable);
            dataWriter.WriteLine("#Peptides: " + numOfPeptides.ToString());

            queryString = "Select PfamId, ClusterId, NumEntries, MinSeqIdentity From PfamPepClusterSumInfo Where NumEntries >= 2;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> pfamId2List = new List<string>();
            List<string> clusterId2List = new List<string>();
            List<string> pepSeq2List = new List<string>();
            List<string> entry2List = new List<string>();
            List<string> pfamId2Seq90List = new List<string>();
            List<string> clusterId2Seq90List = new List<string>();
            List<string> pepSeq2Seq90List = new List<string>();
            List<string> entry2Seq90List = new List<string>();
            string pfamId = "";
            string cluster = "";
            int clusterId = 0;
            int numOfEntry = 0;
            double minSeqId = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                numOfEntry = Convert.ToInt32(clusterRow["NumEntries"].ToString());
                minSeqId = Convert.ToDouble(clusterRow["MinSeqIdentity"].ToString());
                if (numOfEntry >= 2)
                {
                    pfamId = clusterRow["PfamID"].ToString().TrimEnd();

                    cluster = pfamId + "_" + clusterRow["ClusterID"].ToString();

                    clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                    string[] pepSequences = GetPeptideSequences(pfamId, clusterId, pepSeqTable);

                    string[] clusterEntries = GetClusterEntries(pfamId, clusterId);

                    if (!pfamId2List.Contains(pfamId))
                    {
                        pfamId2List.Add(pfamId);
                    }
                    if (!clusterId2List.Contains(cluster))
                    {
                        clusterId2List.Add(cluster);
                    }
                    foreach (string pepSeq in pepSequences)
                    {
                        if (!pepSeq2List.Contains(pepSeq))
                        {
                            pepSeq2List.Add(pepSeq);
                        }
                    }
                    foreach (string pdbId in clusterEntries)
                    {
                        if (!entry2List.Contains(pdbId))
                        {
                            entry2List.Add(pdbId);
                        }
                    }

                    if (minSeqId <= 90)
                    {
                        if (!pfamId2Seq90List.Contains(pfamId))
                        {
                            pfamId2Seq90List.Add(pfamId);
                        }
                        if (!clusterId2Seq90List.Contains(cluster))
                        {
                            clusterId2Seq90List.Add(cluster);
                        }
                        foreach (string pepSeq in pepSequences)
                        {
                            if (!pepSeq2Seq90List.Contains(pepSeq))
                            {
                                pepSeq2Seq90List.Add(pepSeq);
                            }
                        }
                        foreach (string pdbId in clusterEntries)
                        {
                            if (!entry2Seq90List.Contains(pdbId))
                            {
                                entry2Seq90List.Add(pdbId);
                            }
                        }
                    }
                }
            }
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Pfams(#Entry >= 2): " + pfamId2List.Count.ToString());
            dataWriter.WriteLine("#Clusters (#Entry >= 2): " + clusterId2List.Count.ToString());
            dataWriter.WriteLine("#Entry(#Entry >= 2): " + entry2List.Count.ToString());
            dataWriter.WriteLine("#Peptides(#Entry >= 2): " + pepSeq2List.Count.ToString());
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Pfams(#Entry >= 2, MinSeq <= 90): " + pfamId2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Clusters (#Entry >= 2, MinSeq <= 90): " + clusterId2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Entry(#Entry >= 2, MinSeq <= 90): " + entry2Seq90List.Count.ToString());
            dataWriter.WriteLine("#Peptides(#Entry >= 2, MinSeq <= 90): " + pepSeq2Seq90List.Count.ToString());

            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string[] GetClusterEntries(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterEntries = new string[entryTable.Rows.Count];
            int count = 0;
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                clusterEntries[count] = pdbId;
                count++;
            }
            return clusterEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetPeptideSequences(string pfamId, int clusterId, DataTable pepSeqTable)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pepSeqList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            string pepChain = "";
            string pepSeq = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());

                pepChain = GetPeptideAsymChain(pdbId, domainInterfaceId);
                DataRow[] pepSeqRows = pepSeqTable.Select(string.Format("PdbID = '{0}' AND PepAsymChain = '{1}'", pdbId, pepChain));
                if (pepSeqRows.Length > 0)
                {
                    pepSeq = pepSeqRows[0]["Sequence"].ToString();
                    if (!pepSeqList.Contains(pepSeq))
                    {
                        pepSeqList.Add(pepSeq);
                    }
                }
            }
            string[] pepSequences = new string[pepSeqList.Count];
            pepSeqList.CopyTo(pepSequences);
            return pepSequences;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetPeptideAsymChain(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select PepAsymChain From PfamPeptideInterfaces Where PdbID = '{0}' AND DOmainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (pepChainTable.Rows.Count > 0)
            {
                return pepChainTable.Rows[0]["PepAsymChain"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepSeqTable"></param>
        /// <returns></returns>
        private int GetNumOfPeptides(DataTable pepSeqTable)
        {
            List<string> pepSeqList = new List<string>();
            string sequence = "";
            foreach (DataRow pepSeqRow in pepSeqTable.Rows)
            {
                sequence = pepSeqRow["Sequence"].ToString();
                if (!pepSeqList.Contains(sequence))
                {
                    pepSeqList.Add(sequence);
                }
            }
            return pepSeqList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetPeptideSequenceTable()
        {
            string queryString = "Select Distinct PdbID, PepAsymChain From PfamPeptideInterfaces;";
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query(queryString);
            DataTable pepChainSeqTable = pepChainTable.Clone();
            pepChainSeqTable.Columns.Add(new DataColumn("Sequence"));
            string pdbId = "";
            string pepAsymChain = "";
            string sequence = "";
            foreach (DataRow pepChainRow in pepChainTable.Rows)
            {
                pdbId = pepChainRow["PdbID"].ToString();
                pepAsymChain = pepChainRow["PepAsymChain"].ToString().TrimEnd();
                sequence = GetChainSequence(pdbId, pepAsymChain);
                DataRow pepSeqRow = pepChainSeqTable.NewRow();
                pepSeqRow["PdbID"] = pdbId;
                pepSeqRow["PepAsymChain"] = pepAsymChain;
                pepSeqRow["Sequence"] = sequence;
                pepChainSeqTable.Rows.Add(pepSeqRow);
            }
            return pepChainSeqTable;
        }      
        #endregion

        #region pfam-peptide: MHC
        public void PrintMHCSequences()
        {
            string MHCSeqFile = Path.Combine(pepDataDir, "MHCsequences_MHC_I.fasta");
            StreamWriter seqWriter = new StreamWriter(MHCSeqFile);
            string queryString = "Select Distinct Pfam_ID, DbCode, DbAccession, PdbDbRefSifts.PdbID, PdbDbRefSifts.EntityID " +
                //         " From PdbDbRefSifts, PdbPfam Where Pfam_ID IN ('MHC_I', 'MHC_I_2', 'MHC_I_3') AND " +
                " From PdbDbRefSifts, PdbPfam Where Pfam_ID IN ('MHC_I', 'MHC_I_2', 'MHC_I_3', 'MHC_II_alpha', 'MHC_II_beta') AND " +
                "PdbDbRefSifts.PdbID = PdbPfam.PdbID AND PdbDbRefSifts.EntityID = PdbPfam.EntityID AND DbName = 'UNP';";
            DataTable mhcProteinEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            List<string> unpCodeList = new List<string>();
            foreach (DataRow dataRow in mhcProteinEntryTable.Rows)
            {
                unpCode = dataRow["DbCode"].ToString().TrimEnd().ToUpper();
                if (unpCode.IndexOf("_HUMAN") > -1)
                {
                    if (!unpCodeList.Contains(unpCode))
                    {
                        unpCodeList.Add(unpCode);
                    }
                }
            }
            string fastaHeaderLine = "";
            string[] sequenceGn = null;
            foreach (string lsUnpCode in unpCodeList)
            {
                fastaHeaderLine = ">" + lsUnpCode + " | ";
                DataRow[] unpRows = mhcProteinEntryTable.Select(string.Format("DbCode = '{0}'", lsUnpCode));
                fastaHeaderLine += unpRows[0]["DbAccession"].ToString().TrimEnd() + " " + unpRows[0]["Pfam_ID"].ToString() + " ";

                sequenceGn = GetUniprotSequence(lsUnpCode);
                if (sequenceGn != null)
                {
                    fastaHeaderLine += sequenceGn[1] + " ";
                    foreach (DataRow unpRow in unpRows)
                    {
                        fastaHeaderLine += (unpRow["PdbID"].ToString() + unpRow["EntityID"].ToString() + ",");
                    }
                    seqWriter.WriteLine(fastaHeaderLine.TrimEnd(','));
                    seqWriter.WriteLine(sequenceGn[0]);
                }
            }
            seqWriter.Close();
        }

        private string[] GetUniprotSequence(string unpCode)
        {
            string queryString = "";
            DataTable unpSeqTable = null;
            if (unpCode.ToUpper().IndexOf("_HUMAN") > -1)
            {
                queryString = string.Format("Select Sequence, GN From HumanSeqInfo Where UnpCode = '{0}' AND Isoform = 0;", unpCode);
                unpSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            else
            {
                queryString = string.Format("Select Sequence, GN From UnpSeqInfo Where UnpCode = '{0}';", unpCode);
                unpSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string[] gnSequence = new string[2];
            if (unpSeqTable.Rows.Count > 0)
            {
                gnSequence[0] = unpSeqTable.Rows[0]["Sequence"].ToString();
                gnSequence[1] = unpSeqTable.Rows[0]["GN"].ToString();
            }
            else
            {
                try
                {
                    string localFile = DownloadUniprotFromWeb(unpCode);
                    gnSequence = ParseUniprotFile(localFile);
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.Message;
                }
            }

            return gnSequence;
        }

        WebClient webClient = new WebClient();
        private string DownloadUniprotFromWeb(string unpCode)
        {
            string webFile = "http://www.uniprot.org/uniprot/" + unpCode + ".fasta";
            string localFile = Path.Combine(pepDataDir, unpCode + ".fasta");
            if (!File.Exists(localFile))
            {
                webClient.DownloadFile(webFile, localFile);
            }
            return localFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpFile"></param>
        /// <returns></returns>
        private string[] ParseUniprotFile(string unpFile)
        {
            StreamReader dataReader = new StreamReader(unpFile);
            string line = "";
            string[] gnSequence = new string[2];
            string gn = "";
            string sequence = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.Substring(0, 1) == ">")
                {
                    int gnIndex = line.IndexOf("GN=") + "GN=".Length;
                    int gnEndIndex = line.IndexOf("PE=");
                    gn = line.Substring(gnIndex, gnEndIndex - gnIndex).Trim();
                }
                else
                {
                    sequence += line;
                }
            }
            dataReader.Close();
            gnSequence[0] = sequence;
            gnSequence[1] = gn;
            return gnSequence;
        }
        #endregion
        #endregion

        #region peptide distance and length
        struct PepDistInfo
        {
            public double dist;
            public int seqStart;
            public int seqEnd;
            public string resStart;
            public string resEnd;
        }

        public void PrintPeptideLengthDistances()
        {
            if (!Directory.Exists(temp_dir))
            {
                Directory.CreateDirectory(temp_dir);
            }
            DataTable entitySeqTable = GetAsuPeptideSequenceTable();
            string sequence = "";
            Dictionary<string, List<string>> entryPepChainHash = new Dictionary<string, List<string>>();
            string pdbId = "";
            string asymChain = "";
            foreach (DataRow entitySeqRow in entitySeqTable.Rows)
            {
                sequence = entitySeqRow["Sequence"].ToString();
                pdbId = entitySeqRow["PdbID"].ToString();
                asymChain = entitySeqRow["AsymID"].ToString().TrimEnd();
                if (entryPepChainHash.ContainsKey(pdbId))
                {
                    entryPepChainHash[pdbId].Add(asymChain);
                }
                else
                {
                    List<string> pepChainList = new List<string>();
                    pepChainList.Add(asymChain);
                    entryPepChainHash.Add(pdbId, pepChainList);
                }
            }
            StreamWriter pepDistWriter = new StreamWriter(@"C:\Paper\Metallophos_TempleGrant\PepDistInfo.txt");
            // read data from crystal xml file
            foreach (string lsPdb in entryPepChainHash.Keys)
            {
                string[] pepChains = entryPepChainHash[lsPdb].ToArray();

                Dictionary<string, AtomInfo[]> pepChainAtomsHash = GetPeptideAtomCoordinates(lsPdb, pepChains, temp_dir);

                foreach (string chain in pepChainAtomsHash.Keys)
                {
                    PepDistInfo distInfo = CalculateDistanceInPeptideChain((AtomInfo[])pepChainAtomsHash[chain]);
                    pepDistWriter.WriteLine(lsPdb + "\t" + chain + "\t" + distInfo.seqStart.ToString() + "\t" + distInfo.seqEnd.ToString() + "\t" +
                        distInfo.resStart + "\t" + distInfo.resEnd + "\t" + distInfo.dist.ToString());
                }
            }
            pepDistWriter.Close();

            try
            {
                Directory.Delete(temp_dir, true);
            }
            catch { }
        }

        private DataTable GetAsuPeptideSequenceTable()
        {
            DataTable pepSeqTable = null;
            string pepSeqFile = @"C:\Paper\Metallophos_TempleGrant\PeptideSequences.txt";
            if (File.Exists(pepSeqFile))
            {
                pepSeqTable = new DataTable();
                pepSeqTable.Columns.Add(new DataColumn("PdbID"));
                pepSeqTable.Columns.Add(new DataColumn("AsymID"));
                pepSeqTable.Columns.Add(new DataColumn("EntityID"));
                pepSeqTable.Columns.Add(new DataColumn("AuthorChain"));
                pepSeqTable.Columns.Add(new DataColumn("Sequence"));
                StreamReader dataReader = new StreamReader(pepSeqFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    DataRow dataRow = pepSeqTable.NewRow();
                    dataRow.ItemArray = fields;
                    pepSeqTable.Rows.Add(dataRow);
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "Select Distinct PdbID, AsymID, EntityID, AuthorCHain, Sequence From AsymUnit WHere PolymerType = 'polypeptide';";
                DataTable asuEntitySeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                pepSeqTable = asuEntitySeqTable.Clone();

                StreamWriter dataWriter = new StreamWriter(pepSeqFile);
                string sequence = "";
                foreach (DataRow entitySeqRow in asuEntitySeqTable.Rows)
                {
                    sequence = entitySeqRow["Sequence"].ToString().TrimEnd();
                    if (sequence.Length < 30)
                    {
                        dataWriter.WriteLine(ParseHelper.FormatDataRow(entitySeqRow));
                        DataRow dataRow = pepSeqTable.NewRow();
                        dataRow.ItemArray = entitySeqRow.ItemArray;
                        pepSeqTable.Rows.Add(dataRow);
                    }
                }
                dataWriter.Close();
            }
            return pepSeqTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private PepDistInfo CalculateDistanceInPeptideChain(AtomInfo[] atoms)
        {
            AtomInfo[] firstResAtoms = null;
            AtomInfo[] lastResAtoms = null;
            Dictionary<int, List<AtomInfo>> residueAtomsHash = new Dictionary<int, List<AtomInfo>>();

            foreach (AtomInfo atom in atoms)
            {
                if (residueAtomsHash.ContainsKey(Convert.ToInt32(atom.seqId)))
                {
                    residueAtomsHash[Convert.ToInt32(atom.seqId)].Add(atom);
                }
                else
                {
                    List<AtomInfo> atomList = new List<AtomInfo>();
                    atomList.Add(atom);
                    residueAtomsHash.Add(Convert.ToInt32(atom.seqId), atomList);
                }
            }
            List<int> seqIdList = new List<int>(residueAtomsHash.Keys);
            seqIdList.Sort();
            List<AtomInfo> firstResAtomList = residueAtomsHash[seqIdList[0]];
            firstResAtoms = new AtomInfo[firstResAtomList.Count];
            firstResAtomList.CopyTo(firstResAtoms);

            List<AtomInfo> lastResAtomList = residueAtomsHash[seqIdList[seqIdList.Count - 1]];
            lastResAtoms = new AtomInfo[lastResAtomList.Count];
            lastResAtomList.CopyTo(lastResAtoms);

            double distance = GetMinimumDistance(firstResAtoms, lastResAtoms);
            PepDistInfo distInfo = new PepDistInfo();
            distInfo.dist = distance;
            distInfo.resStart = firstResAtoms[0].residue;
            distInfo.resEnd = lastResAtoms[0].residue;
            distInfo.seqStart = (int)seqIdList[0];
            distInfo.seqEnd = (int)seqIdList[seqIdList.Count - 1];
            return distInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms1"></param>
        /// <param name="atoms2"></param>
        /// <returns></returns>
        private double GetMinimumDistance(AtomInfo[] atoms1, AtomInfo[] atoms2)
        {
            double minDist = 10000.0;
            foreach (AtomInfo atom1 in atoms1)
            {
                foreach (AtomInfo atom2 in atoms2)
                {
                    double dist = atom1 - atom2;
                    if (minDist > dist)
                    {
                        minDist = dist;
                    }
                }
            }
            return minDist;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="temp_dir"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> GetPeptideAtomCoordinates(string pdbId, string[] pepAsymChains, string temp_dir)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, temp_dir);
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, AtomInfo[]> pepChainAtomsHash = new Dictionary<string, AtomInfo[]>();
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            foreach (ChainAtoms chain in chains)
            {
                if (pepAsymChains.Contains(chain.AsymChain))
                {
                    pepChainAtomsHash.Add(chain.AsymChain, chain.CartnAtoms);
                }
            }
            return pepChainAtomsHash;
        }
        #endregion

        #region check entry has literature
        System.Net.WebClient webDownload = new System.Net.WebClient();
        public const string PdbWebServer = "https://files.rcsb.org/download/";
        public void CheckEntryWithLiteratures()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pepDataDir, "pepEntryPublicationInfo.txt"), true);
            string fileName = "";
            string xmlFileFolder = @"D:\Qifang\ProjectData\DbProjectData\PDB\XML-noatom";
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string citationInfo = "";
            string pdbId = "";
            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                fileName = pdbId + "-noatom.xml";
                try
                {
                    webDownload.DownloadFile(PdbWebServer + fileName, Path.Combine(xmlFileFolder, fileName));
                    citationInfo = ParseXmlFile(Path.Combine(xmlFileFolder, fileName));
                    dataWriter.WriteLine(entryRow["PdbID"].ToString() + "\t" + citationInfo);
                }
                catch (Exception ex)
                {
                    string errorMsg = pdbId + " " + ex.Message;
                }
            }
            dataWriter.Close();
        }

        private string ParseXmlFile(string xmlFile)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlFile);

            XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            string xmlNameSpace = xmlDoc.DocumentElement.Attributes["xmlns:PDBx"].InnerText;
            nsManager.AddNamespace("PDBx", xmlNameSpace);

            XmlNode citationNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:citationCategory", nsManager);

            if (citationNode == null)
            {
                return "";
            }
            XmlNode citationIdNode = citationNode.ChildNodes[0];
            string nodeName = "";
            string nodeText = "";
            string publicationInfo = "";
            bool notPublished = true;
            foreach (XmlNode citationFieldNode in citationIdNode.ChildNodes)
            {
                nodeName = citationFieldNode.Name.ToLower();
                nodeText = citationFieldNode.InnerText;
                if (nodeName.IndexOf("journal_abbrev") > -1)
                {
                    publicationInfo += (nodeText + "\t");
                }
                else if (nodeName.IndexOf("pdbx_database_id_pubmed") > -1)
                {
                    publicationInfo += (nodeText + "\t");
                    if (nodeText != "")
                    {
                        notPublished = false;
                    }
                }
                else if (nodeName.IndexOf("title") > -1)
                {
                    publicationInfo += (nodeText + "\t");
                }
            }
            if (notPublished)
            {
                publicationInfo += "0";
            }
            else
            {
                publicationInfo += "1";
            }
            return publicationInfo;
        }

        #region peptide associated entries with no literatures
        public void CheckNotPublishedEntries()
        {/*
                string queryString = "select Distinct PdbID from pdbpfam where hmmend - hmmstart < 5 and domainType = 'r';";
                DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                StreamWriter missPfamEntryWriter = new StreamWriter(@"E:\Qifang\DbProjectData\PDB\WrongPfamEntries_hmm5.txt");
                foreach (DataRow entryRow in entryTable.Rows)
                {
                    missPfamEntryWriter.WriteLine(entryRow["PdbID"]);
                }
                missPfamEntryWriter.Close();*/
            string dataFile = Path.Combine(pepDataDir, "pepEntryPublicationInfo.txt");
            string[] entriesNoLiteratures = ReadEntryWithNoLiterature(dataFile);

            string pfamPepSumFile = Path.Combine(pepDataDir, "pep1LiteratureInfo_2.txt");
            StreamWriter dataWriter = new StreamWriter(pfamPepSumFile);
            dataWriter.WriteLine("#Entries with no literatures: " + entriesNoLiteratures.Length);

            string queryString = "Select * From PfamPepClusterSumInfo WHere NumEntries > 1 and MinSeqIdentity < 90;";
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            string headerLine = "";
            foreach (DataColumn dCol in pepClusterTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            headerLine += "NumOfHumanUnps\t#EntriesNoLiteratures\tEntriesNoLiteratures";
            dataWriter.WriteLine(headerLine);
            string pfamId = "";
            int clusterId = 0;
            List<string> pepClusterEntryNoLiteratureList = new List<string>();
            List<string> clusterEntryNoListeratureList = new List<string>();
            List<string> pfamIdList = new List<string>();
            int numOfHumanUnps = 0;
            foreach (DataRow pepClusterRow in pepClusterTable.Rows)
            {
                pepClusterEntryNoLiteratureList.Clear();

                pfamId = pepClusterRow["PfamID"].ToString().TrimEnd();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
                clusterId = Convert.ToInt32(pepClusterRow["ClusterID"].ToString());
                string[] pepClusterEntries = GetPepClusterEntries(pfamId, clusterId);
                numOfHumanUnps = GetNumOfHumanProteins(pfamId, clusterId);
                foreach (string pdbId in pepClusterEntries)
                {
                    if (entriesNoLiteratures.Contains(pdbId))
                    {
                        pepClusterEntryNoLiteratureList.Add(pdbId);
                        if (!clusterEntryNoListeratureList.Contains(pdbId))
                        {
                            clusterEntryNoListeratureList.Add(pdbId);
                        }
                    }
                }
                dataWriter.WriteLine(ParseHelper.FormatDataRow(pepClusterRow) + "\t" + numOfHumanUnps.ToString() + "\t" +
                    pepClusterEntryNoLiteratureList.Count.ToString() + "\t" +
                    FormatArrayString(pepClusterEntryNoLiteratureList));
            }
            dataWriter.WriteLine("#Pfams with clusters with >= 2 entries and minseqidentity < 90%: " + pfamIdList.Count.ToString());
            dataWriter.WriteLine("#Entries in clusters with >= 2 entries and minseqidentity < 90%, but no literatures: " + clusterEntryNoListeratureList.Count.ToString());
            dataWriter.Close();
        }

        private int GetNumOfHumanProteins(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamPepInterfaceClusters " +
                " Where PfamID = '{0}' AND ClusterID = {1} AnD UnpCode Like '%_HUMAN';", pfamId, clusterId);
            DataTable clusterHumanUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            return clusterHumanUnpTable.Rows.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckSingleNotPublishedEntries()
        {/*
                string queryString = "select Distinct PdbID from pdbpfam where hmmend - hmmstart < 5 and domainType = 'r';";
                DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                StreamWriter missPfamEntryWriter = new StreamWriter(@"E:\Qifang\DbProjectData\PDB\WrongPfamEntries_hmm5.txt");
                foreach (DataRow entryRow in entryTable.Rows)
                {
                    missPfamEntryWriter.WriteLine(entryRow["PdbID"]);
                }
                missPfamEntryWriter.Close();*/
            string pfamId = "MHC_I";
            string dataFile = Path.Combine(pepDataDir, "pepEntryPublicationInfo.txt");
            string[] entriesNoLiteratures = ReadEntryWithNoLiterature(dataFile);

            string pfamPepSumFile = Path.Combine(pepDataDir, pfamId + "_LiteratureInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pfamPepSumFile);
            dataWriter.WriteLine("#Entries with no literatures: " + entriesNoLiteratures.Length);

            string queryString = string.Format("Select * From PfamPepClusterSumInfo WHere pfamId = '{0}' and ClusterID = 1;", pfamId);
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int clusterId = 0;
            List<string> pepClusterEntryNoLiteratureList = new List<string>();
            List<string> clusterEntryNoListeratureList = new List<string>();

            foreach (DataRow pepClusterRow in pepClusterTable.Rows)
            {
                pepClusterEntryNoLiteratureList.Clear();
                pfamId = pepClusterRow["PfamID"].ToString().TrimEnd();
                clusterId = Convert.ToInt32(pepClusterRow["ClusterID"].ToString());
                string[] pepClusterEntries = GetPepClusterEntries(pfamId, clusterId);
                foreach (string pdbId in pepClusterEntries)
                {
                    if (entriesNoLiteratures.Contains(pdbId))
                    {
                        pepClusterEntryNoLiteratureList.Add(pdbId);
                        if (!clusterEntryNoListeratureList.Contains(pdbId))
                        {
                            clusterEntryNoListeratureList.Add(pdbId);
                        }
                    }
                }
                dataWriter.WriteLine(ParseHelper.FormatDataRow(pepClusterRow) + "\t" + pepClusterEntryNoLiteratureList.Count.ToString() + "\t" +
                    FormatArrayString(pepClusterEntryNoLiteratureList));
            }
            dataWriter.WriteLine("#Entries with no literature in clusters with >= 2 entries and minseqidentity < 90%: " + clusterEntryNoListeratureList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetPepClusterEntries(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable pepClusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pepClusterEntries = new string[pepClusterEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in pepClusterEntryTable.Rows)
            {
                pepClusterEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pepClusterEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadEntryWithNoLiterature(string dataFile)
        {
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            List<string> entryNoLiteratureList = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 2)
                {
                    entryNoLiteratureList.Add(fields[0]);
                }
                else if (fields[1].ToLower() == "to be published")
                {
                    entryNoLiteratureList.Add(fields[0]);
                }
            }
            dataReader.Close();
            string[] entriesNoLiteratures = new string[entryNoLiteratureList.Count];
            entryNoLiteratureList.CopyTo(entriesNoLiteratures);
            return entriesNoLiteratures;
        }
        #endregion
        #endregion

        #region peptide binding pfams
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamPeptideHumanInfo()
        {
            string pfamPepProtInfoFile = Path.Combine(pepDataDir, "PepBindingPfamUnpInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pfamPepProtInfoFile);
            dataWriter.WriteLine("Pfam\tClusterID\t#Entries\t#Pfams_peptide\t#UnpSeqs\t#UnpSeqs_peptide\t#Seqs_peptide\tSurfaceArea\tMinSeqIdentity\tRepClusterInterface");

            string queryString = "Select * From PfamPepClusterSumInfo Where NumUnpSeqs >= 5;";
            DataTable goodPepClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            int clusterId = 0;
            foreach (DataRow pepClusterRow in goodPepClusterTable.Rows)
            {
                pfamId = pepClusterRow["PfamID"].ToString();
                clusterId = Convert.ToInt32(pepClusterRow["ClusterID"].ToString());
                Dictionary<string, List<string>> clusterUnpEntryHash = GetPfamPeptideBindingUnpEntryHash(pfamId, clusterId);
                dataWriter.WriteLine(ParseHelper.FormatDataRow(pepClusterRow));
                dataWriter.WriteLine(FormatUnpEntryHash(clusterUnpEntryHash));
                dataWriter.WriteLine();
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetPfamPeptideBindingUnpEntryHash(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbId, UnpCode From PfamPepInterfaceClusters Where PfamId = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterUnpEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> unpEntryHash = new Dictionary<string,List<string>> ();
            string unpCode = "";
            string pdbId = "";
            foreach (DataRow unpEntryRow in clusterUnpEntryTable.Rows)
            {
                unpCode = unpEntryRow["UnpCode"].ToString().TrimEnd();
                pdbId = unpEntryRow["PdbID"].ToString();
                if (unpEntryHash.ContainsKey(unpCode))
                {
                    if (!unpEntryHash[unpCode].Contains(pdbId))
                    {
                        unpEntryHash[unpCode].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    unpEntryHash.Add(unpCode, entryList);
                }
            }
            return unpEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntryHash"></param>
        /// <returns></returns>
        private string FormatUnpEntryHash(Dictionary<string, List<string>> unpEntryHash)
        {
            string unpEntryLine = "";
            List<string> unpList = new List<string> (unpEntryHash.Keys);
            unpList.Sort();
            foreach (string unp in unpList)
            {
                unpEntryLine += (unp + " " + unpEntryHash[unp].Count.ToString() + " " + ParseHelper.FormatStringFieldsToString(unpEntryHash[unp].ToArray ()) + "\n");
            }
            return unpEntryLine.TrimEnd('\n');
        }
        #endregion

        #region peptide binding human sequences
        public void PrintHumanRelatedPeptideBindingPfams()
        {
            string pfamUnpInfoFile = Path.Combine(pepDataDir, "HumanSeqsWithPepPfam_all.txt");
            StreamWriter dataWriter = new StreamWriter(pfamUnpInfoFile);
            //    string[] pepPfams = GetStrongPeptideBindingPfams ();
            string[] pepPfams = GetPeptideBindingPfams();
            GetPfamHumanSequences(pepPfams, dataWriter);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPeptideBindingPfams()
        {
            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pepPfamIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pepPfams = new string[pepPfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow pepPfamRow in pepPfamIdTable.Rows)
            {
                pepPfams[count] = pepPfamRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pepPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetStrongPeptideBindingPfams()
        {
            string queryString = "Select Distinct PfamID From PfamPepClusterSumInfo Where NumUnpSeqs >= 5;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pepPfams = new string[pfamIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow dataRow in pfamIdTable.Rows)
            {
                pepPfams[count] = dataRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pepPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfams"></param>
        /// <returns></returns>
        private void GetPfamHumanSequences(string[] pepPfams, StreamWriter dataWriter)
        {
            string queryString = "";
            string unpCode = "";
            int isoform = 0;
            string unpIsoCode = "";
            string sumInfoLine = "";
            string dataLine = "";
            string[] unpStructures = null;
            int numOfSeqs_isoform = 0;
            string seqPfamArch = "";
            List<string> seqList = new List<string> ();
            foreach (string pfamId in pepPfams)
            {
                sumInfoLine = pfamId;
                seqList.Clear();
                dataLine = "";

                queryString = string.Format("Select Distinct UnpCode, IsoForm From HumanPfam Where Pfam_ID = '{0}' AND Isoform = 0 Order By UnpCode, Isoform;", pfamId);
                DataTable pfamHumanSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                numOfSeqs_isoform = pfamHumanSeqTable.Rows.Count;

                foreach (DataRow seqRow in pfamHumanSeqTable.Rows)
                {
                    unpCode = seqRow["UnpCode"].ToString().TrimEnd();
                    isoform = Convert.ToInt32(seqRow["IsoForm"].ToString());
                    seqPfamArch = GetHumanPfamArch(unpCode, isoform);
                    if (isoform == 0)
                    {
                        unpIsoCode = unpCode;
                    }
                    else
                    {
                        unpIsoCode = unpCode + "-" + seqRow["Isoform"].ToString();
                    }
                    if (!seqList.Contains(unpCode))
                    {
                        seqList.Add(unpCode);
                    }
                    unpStructures = GetUnpSeqStructures(unpCode);
                    dataLine += (unpIsoCode + "\t\t" + seqPfamArch + "\t\t" + unpStructures.Length.ToString() + "\t" + ParseHelper.FormatStringFieldsToString(unpStructures) + "\n");
                }
                //             sumInfoLine += " " + seqList.Count.ToString() + " " + numOfSeqs_isoform.ToString();
                sumInfoLine += " " + seqList.Count.ToString();
                dataWriter.WriteLine(sumInfoLine);
                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <param name="isoform"></param>
        /// <returns></returns>
        private string GetHumanPfamArch(string unpCode, int isoform)
        {
            string queryString = string.Format("Select UnpCode, Isoform As EntityID, DomainID, Pfam_ID, SeqStart, SeqEnd, AlignStart, AlignEnd, HmmStart, HmmEnd, DomainType " +
                "From HumanPfam Where UnpCode = '{0}' AND IsoForm = {1} AND Pfam_ID Not Like 'Pfam-B%';", unpCode, isoform);
            DataTable humanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string seqPfamArch = pfamArch.GetNewEntityPfamArch(humanPfamTable.Select());
            return seqPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private string[] GetUnpSeqStructures(string unpCode)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbDbRefSifts Where DbCode = '{0}' AND DbName = 'UNP';", unpCode);
            DataTable unpStructTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpStructList = new List<string> ();
            foreach (DataRow structRow in unpStructTable.Rows)
            {
                unpStructList.Add(structRow["PdbID"].ToString());
            }
            queryString = string.Format("Select Distinct PdbID From PdbDbRefXml Where DbCode = '{0}' AND DbName = 'UNP';", unpCode);
            unpStructTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow structRow in unpStructTable.Rows)
            {
                if (!unpStructList.Contains(structRow["PdbID"].ToString()))
                {
                    unpStructList.Add(structRow["PdbID"].ToString());
                }
            }
            return unpStructList.ToArray ();
        }
        #endregion

        #region peptide binding pfams -- summary info
        /// <summary>
        /// 
        /// </summary>
        public void PrintPeptideBindingPfamsInfo() // modified on Sept. 6, 2018
        {
            string pepPfamsInfoFile = Path.Combine(pepDataDir, "PepBindingPfamsInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pepPfamsInfoFile);
//            dataWriter.WriteLine("Pfam\t#UNP_PDB\t#UNP_HUMAN_PDB\t#UNP_HUMAN\t#UNP_cluster\t#Entries_cluster\tSurfaceArea\tMinSeqIdentity");
            dataWriter.WriteLine("Pfam\tClanID\tClusterID\t#Entries_cluster\t#UNP_cluster\t#HUMAN_UNP_cluster\t#Peptides_cluster\tSurfaceArea\tMinSeqIdentity\t" +
                "#Entries\t#Peptides\t#UNP_PDB_pep\t#UNP_PDB\t#HUMAN_UNP_PDB_Pep\t#HUMAN_UNP_PDB\t#HUMAN_UNP");            
            string[] pepPfams = GetPeptideBindingPfams();
            string dataLine = "";
            string clanId = "";
            int numEntryPfam = 0;
     //       int numUnpPepPfam = 0;
            string[] nonHumanUnpCodesPfam = null;
            int numHumanUnpCluster = 0;
   //         string clusterSumInfoLine = "";
            int[] numUnpAndHumanCodesPep = null;
            int clusterId = 0;
            int numPepSeqListPfam = 0;
            foreach (string pepPfam in pepPfams)
            {
                DataTable pepClusterTable = GetPfamPeptideClusterTable(pepPfam);
                clanId = GetPfamClanID(pepPfam);
                nonHumanUnpCodesPfam = GetPfamUnpCodes(pepPfam);
        //        clusterSumInfoLine = GetPfamPeptideClusterInfo(pepPfam);
                int[] unpCodesNumbers = GetNumOfUnpInPdb(pepPfam);
                numEntryPfam = GetNumEntryInPfam(pepPfam);
                numUnpAndHumanCodesPep = GetNumUnpWithPeptides(pepPfam); // unp with peptides, human unp with peptides for this pfam
                numPepSeqListPfam = GetPfamInteractingPeptides(pepPfam);
                foreach (DataRow clusterRow in pepClusterTable.Rows)
                {
                    clusterId = Convert.ToInt32 (clusterRow["ClusterID"].ToString ());
                    numHumanUnpCluster = GetNumHumanUnpInCluster(pepPfam, clusterId);
                    dataLine = pepPfam + "\t" + clanId + "\t" + clusterRow["ClusterID"].ToString () + "\t" + 
                        clusterRow["NumEntries"].ToString () + "\t" + clusterRow["NumUnpSeqs"].ToString() + "\t" + numHumanUnpCluster.ToString () + "\t" +
                        clusterRow["NumPepSeqs"].ToString() + "\t" + clusterRow["SurfaceArea"].ToString() + "\t" + clusterRow["MinSeqIdentity"].ToString() + "\t" +
                        numEntryPfam + "\t" + numPepSeqListPfam.ToString () + "\t" + 
                        numUnpAndHumanCodesPep[0].ToString() + "\t" + unpCodesNumbers[0].ToString() + "\t" +
                        numUnpAndHumanCodesPep[1].ToString () + "\t" + unpCodesNumbers[1].ToString() + "\t" + unpCodesNumbers[2].ToString();
                    dataWriter.WriteLine(dataLine);
                }                
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamId"></param>
        /// <returns></returns>
        public int[] GetNumOfUnpInPdb(string pepPfamId)
        {
            string[] humanUnpCodes = GetPfamHumanSeqs(pepPfamId);
            string queryString = string.Format("Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = '{0}';", pepPfamId);
            DataTable pfamEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpCodeList = new List<string> ();
            List<string> humanUnpCodeList = new List<string> ();
            string pdbId = "";
            int entityId = 0;
            foreach (DataRow entityRow in pfamEntityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                string[] entityUnpCodes = GetEntityUnpCodes(pdbId, entityId);
                foreach (string unpCode in entityUnpCodes)
                {
                    if (!unpCodeList.Contains(unpCode))
                    {
                        unpCodeList.Add(unpCode);
                    }
                    if (humanUnpCodes.Contains(unpCode))
                    {
                        if (!humanUnpCodeList.Contains(unpCode))
                        {
                            humanUnpCodeList.Add(unpCode);
                        }
                    }
                }
            }
            int[] unpCodesNumbers = new int[3];
            unpCodesNumbers[0] = unpCodeList.Count;  // unp in pdb
            unpCodesNumbers[1] = humanUnpCodeList.Count; // human unp in pdb
            unpCodesNumbers[2] = humanUnpCodes.Length; // human in pfam
            return unpCodesNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamId"></param>
        /// <returns></returns>
        private int GetNumEntryInPfam (string pepPfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pepPfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return pfamEntryTable.Rows.Count;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntityUnpCodes(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct DbCode From PdbDbRefSifts Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode From PdbDbRefXml Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string[] unpCodes = new string[unpCodeTable.Rows.Count];
            int count = 0;
            foreach (DataRow dbCodeRow in unpCodeTable.Rows)
            {
                unpCodes[count] = dbCodeRow["DbCode"].ToString().TrimEnd();
                count++;
            }
            return unpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetNumUnpWithPeptides (string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamPepInterfaceClusters Where PfamId = '{0}';", pfamId);
            DataTable clusterUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> humanUnpList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in clusterUnpTable.Rows)
            {
                unpCode = unpRow["UnpCode"].ToString().TrimEnd().ToUpper ();
                if (unpCode.IndexOf ("_HUMAN") > -1)
                {
                    humanUnpList.Add(unpCode);
                }
            }
            int[] unpHumanCodesPep = new int[2];
            unpHumanCodesPep[0] = clusterUnpTable.Rows.Count;
            unpHumanCodesPep[1] = humanUnpList.Count;
            return unpHumanCodesPep;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public string GetPfamPeptideClusterInfo(string pfamId)
        {
            DataRow largestClusterRow = GetLargestClusterRow (pfamId);
            string largestClusterLine = "-1\t-1\t-1\t-1";
            if (largestClusterRow != null)
            {
                largestClusterLine = largestClusterRow["NumUnpSeqs"].ToString() + "\t" + largestClusterRow["NumEntries"].ToString() + "\t" +
                    largestClusterRow["SurfaceArea"].ToString() + "\t" + largestClusterRow["MinSeqIdentity"].ToString();
            }
            return largestClusterLine;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataRow GetLargestClusterRow (string pfamId)
        {
            DataTable pepClusterInfoTable = GetPfamPeptideClusterTable(pfamId);
            if (pepClusterInfoTable.Rows.Count > 0)
            {
                return pepClusterInfoTable.Rows[0];
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamPeptideClusterTable (string pfamId)
        {
            string queryString = string.Format("Select * From PfamPepClusterSumInfo Where PfamID = '{0}' AND NumEntries >= 2 Order By NumUnpSeqs DESC;", pfamId);
            DataTable pepClusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            return pepClusterInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private int GetNumHumanUnpInCluster (string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamPepInterfaceClusters Where PfamId = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterUnpEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> clusterHumanList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in clusterUnpEntryTable.Rows)
            {
                unpCode = unpRow["UnpCode"].ToString().TrimEnd().ToUpper();
                if (unpCode.IndexOf ("_HUMAN") > -1)
                {
                    clusterHumanList.Add(unpCode);
                }
            }
            return clusterHumanList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamId"></param>
        /// <returns></returns>
        private int GetPfamInteractingPeptides (string pepPfamId)
        {
            string queryString = string.Format("Select Distinct PdbID, PepAsymChain From PfamPeptideInterfaces Where PfamID = '{0}';", pepPfamId);
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pepSeqList = new List<string>();
            Dictionary<string, List<string>> entryPepChainListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string pepAsymId = "";
            foreach (DataRow pepRow in pepChainTable.Rows)
            {
                pdbId = pepRow["PdbID"].ToString();
                pepAsymId = pepRow["PepAsymChain"].ToString().TrimEnd();
                if (entryPepChainListDict.ContainsKey (pdbId))
                {
                    entryPepChainListDict[pdbId].Add(pepAsymId);
                }
                else
                {
                    List<string> pepChainList = new List<string>();
                    pepChainList.Add(pepAsymId);
                    entryPepChainListDict.Add(pdbId, pepChainList);
                }
            }
            foreach (string keyPdbId in entryPepChainListDict.Keys)
            {
                string[] entryPepSequences = GetEntryPeptideSequences(keyPdbId, entryPepChainListDict[keyPdbId].ToArray());
                foreach (string pepSeq in entryPepSequences)
                {
                    if (!pepSeqList.Contains(pepSeq))
                    {
                        pepSeqList.Add(pepSeq);
                    }
                }
            }
            return pepSeqList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepAsymChains"></param>
        /// <returns></returns>
        private string[] GetEntryPeptideSequences (string pdbId, string[] pepAsymChains)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID= '{0}' AND AsymID IN ({1});", 
                pdbId, ParseHelper.FormatSqlListString (pepAsymChains));
            DataTable pepSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pepSequenceList = new List<string>();
            string pepSequence = "";
            foreach (DataRow seqRow in pepSeqTable.Rows)
            {
                pepSequence = seqRow["Sequence"].ToString();
                if (! pepSequenceList.Contains (pepSequence))
                {
                    pepSequenceList.Add(pepSequence);
                }
            }
            return pepSequenceList.ToArray();
        }        
        #endregion

        #region peptide binding clans -- summary info
        /*
         * a.	Clan_id
b.	#Pfams in clan in Pfam
c.	#Pfams with peptides in main cluster 
d.	#Pfams in PDB total (with or without peptides)
e.	#Uniprots of domain in clan in PDB with peptides 
f.	#Uniprots of domain in clan in PDB total 
    #Huamn Uniprots of domain in clan in PDB with peptides 
    #Human Uniprots of domain in clan in PDB total 
g.	#unique peptides
h.	#human Pfams in PDB with peptide
i.	#human Pfams in PDB total
j.	#human Pfams in proteome

         * */
        public void PrintPeptideClansInfo ()
        {
            int numUnpCutoff = 5;
            string pepBindPfamInfoFile = Path.Combine(pepDataDir, "PepBindingPfamsInfo.txt");
            string[] pepClanList = ReadPeptideClans(pepBindPfamInfoFile);
            string pepBindClanFile = Path.Combine(pepDataDir, "PepBindingClansInfo_" + numUnpCutoff.ToString () + ".txt");
            StreamWriter dataWriter = new StreamWriter(pepBindClanFile);
            dataWriter.WriteLine("ClanID\t#Pfams\t#Pfams_cluster\t#Pfams_PDB\t#UNP_PDB_pep\t#UNP_PDB\t#HumanUNP_PDB_pep\t#HumanUNP_PDB\t" +
                "#Peptides\t#HumanPfams_PDB_pep\t#HumanPfams_PDB\t#HumanPfams\tMaxNumUnps_cluster");           
            string dataLine = "";
            int maxNumUnpCluster = 0;
            foreach (string clanId in pepClanList)
            {
                string[] clanPfams = GetClanPfams(clanId);
                string[] clanClusterPfams = GetClanPeptidePfams(clanPfams, numUnpCutoff, out maxNumUnpCluster);
                if (clanClusterPfams.Length == 0)
                {
                    continue;
                }
                string[] clanPdbPfams = GetClanPfamsInPdb(clanPfams);

                string[][] unpHumanUnpLists = GetClanUnpHumanUnpsInPdb(clanPfams);  // clan unps and human unps
                Dictionary<string, List<string>> unpDomainListDict = GetUnpDomainListDict(unpHumanUnpLists[0]);
                string[][] pepUnpHumanUnpLists = GetClanUnpHumanUnpsPeptideBinding(unpDomainListDict);  // peptide binding unps and human unps

                Dictionary<string, List<string>> unpPepDomainListDict = GetUnpPeptideDomainListDict(unpDomainListDict);
                Dictionary<string, List<string>> unpPepPfamListDict = GetUnpPfamListDict(unpPepDomainListDict, clanPfams);

                int numHumanPfams_pdb_pep = GetHumanPfams(unpPepPfamListDict);
                string[] clanHumanPfams = GetClanHumanPfams(clanId);
                string[] clanHumanPfamsPdb = GetClanHumanPfamsInPdb(clanId);

                int numPeptides = GetPfamInteractingPeptides(clanPdbPfams);

                dataLine = clanId + "\t" + clanPfams.Length + "\t" + clanClusterPfams.Length + "\t" + clanPdbPfams.Length + "\t" +
                    pepUnpHumanUnpLists[0].Length + "\t" + unpHumanUnpLists[0].Length + "\t" + pepUnpHumanUnpLists[1].Length + "\t" + unpHumanUnpLists[1].Length + "\t" +
                    numPeptides + "\t" + numHumanPfams_pdb_pep + "\t" + clanHumanPfamsPdb.Length + "\t" + clanHumanPfams.Length + "\t" + maxNumUnpCluster;
                dataWriter.WriteLine (dataLine);
            }
            dataWriter.Close();
        }

        #region clan pfams       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanPfams"></param>
        /// <param name="numUnpCutoff"></param>
        /// <returns></returns>
        private string[] GetClanPeptidePfams (string[] clanPfams, int numUnpCutoff, out int maxNumUnpCluster)
        {
            string queryString = string.Format("Select Distinct PfamID, NumUnpSeqs From PfamPepClusterSumInfo " + 
                " Where PfamID IN ({0}) AND NumUnpSeqs >= {1} AND MinSeqIdentity <= 90;", ParseHelper.FormatSqlListString (clanPfams), numUnpCutoff);
            DataTable pepClusterPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pepClusterPfamList =new List<string> ();
            maxNumUnpCluster = 0;
            int numUnps = 0;
            foreach (DataRow pfamRow in pepClusterPfamTable.Rows)
            {
                pepClusterPfamList.Add (pfamRow["PfamID"].ToString().TrimEnd());
                numUnps = Convert.ToInt32(pfamRow["NumUnpSeqs"].ToString ());
                if (maxNumUnpCluster < numUnps)
                {
                    maxNumUnpCluster = numUnps;
                }
            }
            return pepClusterPfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamInfoFile"></param>
        /// <returns></returns>
        private string[] ReadPeptideClans (string pepPfamInfoFile)
        {
            List<string> clanList = new List<string>();
            StreamReader dataReader = new StreamReader(pepPfamInfoFile);
            string line = dataReader.ReadLine(); // header line
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields[1] != "-")
                {
                    if (! clanList.Contains (fields[1]))
                    {
                        clanList.Add(fields[1]);
                    }
                }
            }
            dataReader.Close();
            return clanList.ToArray();
        }
        #endregion

        #region clan uniprot, human uniprot    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanUnps"></param>
        /// <returns></returns>
        public string[][] GetClanUnpHumanUnpsPeptideBinding (Dictionary<string, List<string>> unpDomainDict)
        {
            List<string> pepClanUnpList = new List<string>();
            List<string> pepClanHumanUnpList = new List<string>();
            foreach (string unpId in unpDomainDict.Keys)
            {
                List<string> domainList = unpDomainDict[unpId];
                if (IsPeptideBindingDomainExist(domainList.ToArray ()))
                {
                    pepClanUnpList.Add(unpId);
                    if (IsUnpHuman (unpId))
                    {
                        pepClanHumanUnpList.Add(unpId);
                    }
                }
            }

            string[][] pepClanUnpLists = new string[2][];
            pepClanUnpLists[0] = pepClanUnpList.ToArray();  // peptide binding #unp in clan
            pepClanUnpLists[1] = pepClanHumanUnpList.ToArray();  // peptide binding #human unp in clan

            return pepClanUnpLists;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpDomainListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetUnpPeptideDomainListDict (Dictionary<string, List<string>> unpDomainListDict)
        {
            Dictionary<string, List<string>> unpPepDomainListDict = new Dictionary<string, List<string>>();
            foreach (string unpId in unpDomainListDict.Keys)
            {
                List<string> pepDomainList = GetPeptideBindingDomains(unpDomainListDict[unpId]);
                if (pepDomainList.Count > 0)
                {
                    unpPepDomainListDict.Add(unpId, pepDomainList);
                }
            }
            return unpPepDomainListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        private bool IsPeptideBindingDomainExist (string[] domains)
        {
            foreach (string domain in domains)
            {
                if (IsPeptideBindingDomain (domain))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        private bool IsPeptideBindingDomain(string domain)
        {
            string pdbId = domain.Substring(0, 4);
            long domainId = Convert.ToInt64(domain.Substring(4, domain.Length - 4));
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where PdbID= '{0}' AND DomainId = {1};", pdbId, domainId);
            DataTable pepDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (pepDomainInterfaceTable.Rows.Count > 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        private List<string> GetPeptideBindingDomains (List<string> domainList)
        {
            List<string> pepBindingDomainList = new List<string>();
            foreach (string domain in domainList)
            {
                if (IsPeptideBindingDomain (domain))
                {
                    pepBindingDomainList.Add(domain);
                }
            }
            return pepBindingDomainList;
        }
        #endregion

        #region clan human pfams
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPfamDict"></param>
        /// <returns></returns>
        public int GetHumanPfams (Dictionary<string, List<string>> unpPfamListDict)
        {
            List<string> pfamIdList = new List<string>();
            foreach (string unpId in unpPfamListDict.Keys)
            {
                if (IsUnpHuman (unpId))
                {
                    foreach (string pfamId in unpPfamListDict[unpId])
                    {
                        if (! pfamIdList.Contains (pfamId))
                        {
                            pfamIdList.Add(pfamId);
                        }
                    }
                }
            }
            return pfamIdList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetDomainPfamDict(string[] domains)
        {
            string queryString = "";
            Dictionary<string, string> domainPfamDict = new Dictionary<string, string>();
            string pfamId = "";
            foreach (string domain in domains)
            {
                queryString = string.Format("Select Pfam_ID From Pdbfam Where PdbId = '{0}' AND DomainID = {1};", domain.Substring(0, 4), domain.Substring(4, domain.Length - 4));
                DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (pfamTable.Rows.Count > 0)
                {
                    pfamId = pfamTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
                    if (!domainPfamDict.ContainsKey(domain))
                    {
                        domainPfamDict.Add(domain, pfamId);
                    }
                }
            }
            return domainPfamDict;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        private List<string> GetDomainPfamList (List<string> domainList, string[] clanPfams)
        {
            string queryString = "";
            List<string> pfamIdList = new List<string>();
            string pfamId = "";
            foreach (string domain in domainList)
            {
                queryString = string.Format("Select Pfam_ID From PdbPfam Where PdbId = '{0}' AND DomainID = {1};", domain.Substring(0, 4), domain.Substring(4, domain.Length - 4));
                DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (pfamTable.Rows.Count > 0)
                {
                    pfamId = pfamTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
                    if (clanPfams.Contains(pfamId))
                    {
                        if (!pfamIdList.Contains(pfamId))
                        {
                            pfamIdList.Add(pfamId);
                        }
                    }
                }
            }
            return pfamIdList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpDomainListDict"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetUnpPfamListDict (Dictionary<string, List<string>> unpDomainListDict, string[] clanPfams)
        {
            Dictionary<string, List<string>> unpPfamListDict = new Dictionary<string, List<string>>();
            foreach (string unpId in unpDomainListDict.Keys)
            {
                List<string> pfamIdList = GetDomainPfamList(unpDomainListDict[unpId], clanPfams);
                unpPfamListDict.Add(unpId, pfamIdList);
            }
            return unpPfamListDict;
        }    
        #endregion

        #region number of unique peptides
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepPfamIds"></param>
        /// <returns></returns>
        private int GetPfamInteractingPeptides(string[] pepPfamIds)
        {
            string queryString = string.Format("Select Distinct PdbID, PepAsymChain From PfamPeptideInterfaces Where PfamID In ({0});", ParseHelper.FormatSqlListString (pepPfamIds));
            DataTable pepChainTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pepSeqList = new List<string>();
            Dictionary<string, List<string>> entryPepChainListDict = new Dictionary<string, List<string>>();
            string pdbId = "";
            string pepAsymId = "";
            foreach (DataRow pepRow in pepChainTable.Rows)
            {
                pdbId = pepRow["PdbID"].ToString();
                pepAsymId = pepRow["PepAsymChain"].ToString().TrimEnd();
                if (entryPepChainListDict.ContainsKey(pdbId))
                {
                    entryPepChainListDict[pdbId].Add(pepAsymId);
                }
                else
                {
                    List<string> pepChainList = new List<string>();
                    pepChainList.Add(pepAsymId);
                    entryPepChainListDict.Add(pdbId, pepChainList);
                }
            }
            foreach (string keyPdbId in entryPepChainListDict.Keys)
            {
                string[] entryPepSequences = GetEntryPeptideSequences(keyPdbId, entryPepChainListDict[keyPdbId].ToArray());
                foreach (string pepSeq in entryPepSequences)
                {
                    if (!pepSeqList.Contains(pepSeq))
                    {
                        pepSeqList.Add(pepSeq);
                    }
                }
            }
            return pepSeqList.Count;
        }

        #endregion
        #endregion

        public void GetNumOfHumanProteinsInPPBDs ()
        {
            string[] PPBDs = { "14-3-3", "Adap_comp_sub", "Atg8", "Bcl-2", "BET", "BIR", "BRCT", "BRO1", "Bromodomain", "CAP_GLY", 
                                        "Cbl_N3", "Chromo", "CTD_bind", "Dynein_light", "EF-hand_1", "EF-hand_7", "EF-hand_8", "FERM_C", 
                                        "FHA", "HORMA", "HRM", "HSP70", "IRS", "MATH", "MBT", "PDZ", "PDZ_2", "PHD", "PID", "PTCB-BRCT", "PWWP", 
                                        "RTT107_BRCT_5", "SH2", "SH3_1", "SH3_9", "Spin-Ssty", "SWIB", "WH1", "WW", "YEATS", "zf-CW", "zf-UBR" };
            List<string> humanProtList = new List<string>();
            foreach (string ppbd in PPBDs)
            {
                string[] humanUnpList = GetHumanUnps(ppbd);
                foreach (string unp in humanUnpList)
                {
                    if (!humanProtList.Contains(unp))
                    {
                        humanProtList.Add(unp);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetHumanUnps (string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpAccession From HumanPfam Where Pfam_ID = '{0}';", pfamId );
            DataTable unpAccTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpList = new List<string>();
            foreach (DataRow accRow in unpAccTable.Rows)
            {
                unpList.Add(accRow["UnpAccession"].ToString().TrimEnd());
            }
            return unpList.ToArray();
        }
    }
}
