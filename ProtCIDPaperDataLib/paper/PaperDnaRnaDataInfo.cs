using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    /*
     * a.	Pfamid
b.	Clan_id (if any)
c.	#PDB entries with DNA/RNA 
d.	#PDB entries total (with or without DNA/RNA)
e.	#Uniprots of domain in PDB with DNA/RNA 
f.	#Uniprots of domain in PDB total 
g.	#unique DNA/RNA
h.	#human domains in PDB with DNA/RNA
i.	#human domains in PDB total
j.	#human domains in proteome

     * */
    public class PaperDnaRnaDataInfo : PaperDataInfo
    {
        private string dnaRnaDataDir = "";
        public PaperDnaRnaDataInfo ()
        {
            dnaRnaDataDir = Path.Combine(dataDir, "PfamDnaRna");
        }

        #region Pfam-DNA/RNA
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamDnaRnaInfo ()
        {
            string pfamDnaRnaInfoFile = Path.Combine(dnaRnaDataDir, "DnaRnaPfamsInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pfamDnaRnaInfoFile);
            dataWriter.WriteLine("PfamID\tClanID\t#PDB_DnaRna\t#PDB\t#UNP_DnaRna\t#UNP_PDB\t#HumanUNP_DnaRna\t#HumanUNP_PDB\t#HumanUNP\t#Dna\t#Rna");
            string queryString = "Select Distinct PfamID From PfamDnaRnas;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            string clanId = "";
            string dataLine = "";
            List<string>[] unpDnaRnaOrNotLists = null;
            int numHumanUnp = 0;
            int[] dnaRnaSeqNumbers = null;
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                clanId = GetPfamClanID(pfamId);
                int[] pfamDnaRnaNumbers = GetPfamDnaRnaNumbers(pfamId, out unpDnaRnaOrNotLists);
                dnaRnaSeqNumbers = GetPfamDnaRnaSequences(pfamId);
                numHumanUnp = GetPfamHumanSeqs (pfamId).Length;
                dataLine = pfamId + "\t" + clanId + "\t" + pfamDnaRnaNumbers[2] + "\t" + pfamDnaRnaNumbers[0] + "\t" +
                    pfamDnaRnaNumbers[3] + "\t" + pfamDnaRnaNumbers[1] + "\t" + pfamDnaRnaNumbers[4] + "\t" + pfamDnaRnaNumbers[5] + "\t" +
                    numHumanUnp + "\t" + dnaRnaSeqNumbers[0] + "\t" + dnaRnaSeqNumbers[1];
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// polydeoxyribonucleotide (DNA)  polyribonucleotide (RNA)
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamDnaRnaSequences (string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID, DnaRnaChain From PfamDnaRnas Where PfamID = '{0}';", pfamId);
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> dnaSeqList = new List<string>();
            List<string> rnaSeqList = new List<string>();
            string pdbId = "";
            string drChain = "";
            string sequence = "";
            string polymerType = "";
            foreach (DataRow drChainRow in pfamDnaRnaTable.Rows)
            {
                pdbId = drChainRow["PdbID"].ToString();
                drChain = drChainRow["DnaRnaChain"].ToString().TrimEnd();
                sequence = GetChainSequence(pdbId, drChain, out polymerType);
                if (polymerType == "polydeoxyribonucleotide")
                {
                    if (! dnaSeqList.Contains (sequence))
                    {
                        dnaSeqList.Add(sequence);
                    }
                }
                else if (polymerType == "polyribonucleotide")
                {
                    if (!rnaSeqList.Contains(sequence))
                    {
                        rnaSeqList.Add(sequence);
                    }
                }
            }
            int[] pfamDnaRnaSeqNumbers = new int[2];
            pfamDnaRnaSeqNumbers[0] = dnaSeqList.Count;
            pfamDnaRnaSeqNumbers[1] = rnaSeqList.Count;
            return pfamDnaRnaSeqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="unpDnaRnaOrNotLists"></param>
        /// <returns></returns>
        private int[] GetPfamDnaRnaNumbers(string pfamId, out List<string>[] unpDnaRnaOrNotLists)
        {
            List<string> unpList = null;
            List<string> dnaRnaUnpList = null;

            Dictionary<string, string> entryEntityUnpDict = GetPfamPdbUnp(pfamId, out unpList);
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] pdbUnpNumbers = GetDNARNAInteractingPdbUnps(pfamId, entryEntityUnpDict, out dnaRnaUnpList);
            List<string> leftUnpList = GetDifUnpList(unpList, dnaRnaUnpList);
            List<string> dnaRnaHumanUnpList = GetHumanUniProts(dnaRnaUnpList);
            List<string> leftHumanUnpList = GetHumanUniProts(leftUnpList);

            unpDnaRnaOrNotLists = new List<string>[2];
            unpDnaRnaOrNotLists[0] = dnaRnaUnpList;
            unpDnaRnaOrNotLists[1] = leftUnpList;

            int[] dnaRnaPfamNumbers = new int[6];
            dnaRnaPfamNumbers[0] = entryTable.Rows.Count; // #PDB in PDB
            dnaRnaPfamNumbers[1] = unpList.Count;  // #UNP in PDB
            dnaRnaPfamNumbers[2] = pdbUnpNumbers[0];  // #PDB-DNA/RNA in Pfam
            dnaRnaPfamNumbers[3] = pdbUnpNumbers[1];  // #UNP-DNA/RNA in Pfam
            dnaRnaPfamNumbers[4] = dnaRnaHumanUnpList.Count;  // #Human-DNA/RNA 
            dnaRnaPfamNumbers[5] = leftHumanUnpList.Count + dnaRnaHumanUnpList.Count;    // #Human no DNA/RNA
            return dnaRnaPfamNumbers;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="entityUnpDict"></param>
        /// <param name="dnaRnaUnpList"></param>
        /// <returns></returns>
        private int[] GetDNARNAInteractingPdbUnps(string pfamId, Dictionary<string, string> entityUnpDict, out List<string> dnaRnaUnpList)
        {
            dnaRnaUnpList = new List<string>();
            string queryString = string.Format("Select Distinct PdbID From PfamDNaRnas Where PfamID = '{0}';", pfamId);
            DataTable dnaEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] entryUnpNumbers = new int[2];
            entryUnpNumbers[0] = dnaEntryTable.Rows.Count;
            //        List<string> unpList = new List<string>();
            foreach (DataRow entryRow in dnaEntryTable.Rows)
            {
                foreach (string entity in entityUnpDict.Keys)
                {
                    if (entity.Substring(0, 4) == entryRow["PdbID"].ToString())
                    {
                        if (!dnaRnaUnpList.Contains(entityUnpDict[entity]))
                        {
                            dnaRnaUnpList.Add(entityUnpDict[entity]);
                        }
                    }
                }
            }
            entryUnpNumbers[1] = dnaRnaUnpList.Count;
            return entryUnpNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="unpList"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetPfamPdbUnp(string pfamId, out List<string> unpList)
        {
            //       string querystring = string.Format("Select PdbID, DomainID, EntityID, SeqStart, SeqEnd From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            //        DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query(querystring);
            unpList = new List<string>();
            string querystring = string.Format("Select Distinct PdbDbRefSifts.PdbID, DomainID, PdbDbRefSifts.EntityID, DbCode, SeqStart, SeqEnd, SeqAlignBeg, SeqAlignEnd From PdbDbRefSifts, PdbDbRefSeqSifts, PdbPfam " +
                 " Where Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbDbRefSifts.PdbID AND PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID AND + " +
                 " PdbPfam.EntityID= PdbDbRefSifts.EntityID AND " +
                 " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID AND DbName = 'UNP';", pfamId);
            Dictionary<string, string> entryUnpDict = new Dictionary<string, string>();
            DataTable pfamDomainUnpTable = ProtCidSettings.pdbfamQuery.Query(querystring);
            string pdbEntity = "";
            string unpCode = "";
            foreach (DataRow domainRow in pfamDomainUnpTable.Rows)
            {
                pdbEntity = domainRow["PdbID"].ToString() + domainRow["EntityID"].ToString();
                unpCode = domainRow["DbCode"].ToString().TrimEnd();
                if (IsOverlap(Convert.ToInt32(domainRow["SeqAlignBeg"].ToString()), Convert.ToInt32(domainRow["SeqAlignEnd"].ToString()),
                    Convert.ToInt32(domainRow["SeqStart"].ToString()), Convert.ToInt32(domainRow["SeqEnd"].ToString())))
                {
                    if (!entryUnpDict.ContainsKey(pdbEntity))
                    {
                        entryUnpDict.Add(pdbEntity, unpCode);
                    }
                    if (!unpList.Contains(unpCode))
                    {
                        unpList.Add(unpCode);
                    }
                }
            }
            return entryUnpDict;
        }
        #endregion

        #region clan Pfam - DNA/RNA
        /*
         * a.	Clan_id
b.	#Pfams in clan in Pfam
c.	#Pfams with DNA/RNA 
d.	#Pfams in PDB total (with or without DNA/RNA)
e.	#Uniprots of domain in clan in PDB with DNA/RNA 
f.	#Uniprots of domain in clan in PDB total 
g.	#unique DNA/RNA
h.	#human Pfams in PDB with DNA/RNA
i.	#human Pfams in PDB total
j.	#human Pfams in proteome
        */
        public void PrintClanPfamDnaRnaInfo ()
        {
            string dnaRnaPfamInfoFile = Path.Combine(dnaRnaDataDir, "DnaRnaPfamsInfo.txt");
            string dnaRnaClanInfoFile = Path.Combine(dnaRnaDataDir, "DnaRnaClansInfo.txt");
            StreamWriter dataWriter = new StreamWriter(dnaRnaClanInfoFile);
            dataWriter.WriteLine("ClanID\t#Pfams_PDB");
    /*        dataWriter.WriteLine("ClanID\t#Pfams\t#Pfams_DNA/RNA\t#Pfams_PDB\t#UNP_DNA/RNA\t#UNP_PDB\t#HUMAN_DNA/RNA\t#HUMAN_PDB\t" +
                "#HumanPfams_DNA/RNA\t#HumanPfams_PDB\t#HumanPfams\t#DNA\t#RNA");*/
            string dataLine = "";
            Dictionary<string, List<string>> clanDnaRnaPfamDict = GetClanDnaRnaPfamListDict(dnaRnaPfamInfoFile);
            foreach (string clanId in clanDnaRnaPfamDict.Keys)
            {
                List<string> clanDrPfamList = clanDnaRnaPfamDict[clanId];
                string[] clanPfams = GetClanPfams (clanId);
                string[] clanPfamsPdb = GetClanPfamsInPdb(clanPfams);

                List<string>[] clanUnpLists = GetClanDnaRnaUnpInfo(clanId); // UNP-DNA/RNA, HUMAN-DNA/RNA, UNP-PDB, HUMAN-PDB

                string[] clanHumanPfams = GetClanHumanPfams(clanId);
                string[] clanHumanPfamsPdb = GetClanHumanPfamsInPdb(clanId);
                string[] clanHumanPfamsPdbDr = GetDnaRnaPfams (clanHumanPfamsPdb);
                string[] clanHumanPfamsDr = GetHumanDnaRnaPfams (clanHumanPfamsPdbDr);
                int[] clanDnaRnaNumbers = GetClanDnaRnaSequences(clanPfamsPdb);
                dataLine = clanId + "\t" + clanPfams.Length + "\t" + clanDrPfamList.Count + "\t" + clanPfamsPdb.Length + "\t" + 
                    clanUnpLists[0].Count + "\t" + clanUnpLists[2].Count + "\t" + clanUnpLists[1].Count + "\t" + clanUnpLists[3].Count + "\t" +
                    clanHumanPfamsDr.Length + "\t" + clanHumanPfamsPdb.Length + "\t" + clanHumanPfams.Length + "\t" +
                    clanDnaRnaNumbers[0] + "\t" + clanDnaRnaNumbers[1];
                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfams"></param>
        /// <returns></returns>
        private string[] GetHumanDnaRnaPfams(string[] pfams)
        {
            List<string> humanDrPfamList = new List<string>();
            if (pfams.Length == 0)
            {
                return humanDrPfamList.ToArray();
            }
            string queryString = string.Format ("Select Distinct PdbId, ChainDomainId, PfamID From PfamDnaRnas Where PfamID IN ({0});", ParseHelper.FormatSqlListString (pfams));
            DataTable drDomainTable = ProtCidSettings.protcidQuery.Query (queryString);
            Dictionary<string, List<string>> pfamDomainListDict = new Dictionary<string,List<string>> ();
            string pfamId = "";
            string chainDomain = "";
            foreach (DataRow domainRow in drDomainTable.Rows)
            {
                pfamId = domainRow["PfamID"].ToString ().TrimEnd ();
                chainDomain = domainRow["PdbID"].ToString () + domainRow["ChainDomainId"].ToString ();
                if (pfamDomainListDict.ContainsKey (pfamId))
                {
                    pfamDomainListDict[pfamId].Add (chainDomain);
                }
                else
                {
                    List<string> domainList = new List<string> ();
                    domainList.Add (chainDomain);
                    pfamDomainListDict.Add (pfamId, domainList);
                }
            }
            
            string pdbId = "";
            int chainDomainId = 0;
            foreach (string keyPfam in pfamDomainListDict.Keys)
            {
                foreach (string lsChainDomain in pfamDomainListDict[keyPfam])
                {
                    pdbId = lsChainDomain.Substring (0, 4);
                    chainDomainId = Convert.ToInt32 (lsChainDomain.Substring (4, lsChainDomain.Length - 4));
                    if (IsChainDomainHuman (pdbId, chainDomainId))
                    {
                        humanDrPfamList.Add (keyPfam);
                        break;
                    }
                }
            }
            return humanDrPfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private bool IsChainDomainHuman (string pdbId, int chainDomainId)
        {
            string queryString = string.Format ("Select UnpID From PdbPfamChain, PdbPfam, UnpPdbfam " + 
                " Where PdbPfamChain.PdbID = '{0}' AND pdbPfamChain.ChainDomainID = {1} AND " +
                " PdbPfamChain.PdbID = PdbPfam.PdbID AND PdbPfamChain.DomainID = PdbPfam.DomainID AND "  +
                " PdbPfam.PdbID = UnpPdbfam.PdbID AND PdbPfam.DomainID = UnpPdbfam.DomainID AND UnpID Like '%_HUMAN';", pdbId, chainDomainId);
            DataTable humanUnpTable = ProtCidSettings.pdbfamQuery.Query (queryString);
            if (humanUnpTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanPfams"></param>
        /// <returns></returns>
        private string[] GetDnaRnaPfams (string[] clanPfams)
        {
            string[] clanDrPfams = new string[0];
            if (clanPfams.Length == 0)
            {
                return clanDrPfams;
            }
            string queryString = string.Format ("Select Distinct PfamID From PfamDnaRnas Where PfamID IN ({0});", ParseHelper.FormatSqlListString (clanPfams));
            DataTable drPfamTable = ProtCidSettings.protcidQuery.Query (queryString);
            clanDrPfams = new string[drPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in drPfamTable.Rows)
            {
                clanDrPfams[count] = pfamRow["PfamID"].ToString ().TrimEnd ();
                count ++;
            }
            return clanDrPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dnaRnaPfamInfoFile"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetClanDnaRnaPfamListDict(string dnaRnaPfamInfoFile)
        {
            Dictionary<string, List<string>> clanDrPfamListDict = new Dictionary<string, List<string>>();
            string line = "";
            StreamReader dataReader = new StreamReader(dnaRnaPfamInfoFile);
            line = dataReader.ReadLine();  // header
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields[1] == "-")
                {
                    continue;
                }
                if (clanDrPfamListDict.ContainsKey (fields[1]))
                {
                    clanDrPfamListDict[fields[1]].Add(fields[0]);
                }
                else
                {
                    List<string> pfamList = new List<string>();
                    pfamList.Add(fields[0]);
                    clanDrPfamListDict.Add(fields[1], pfamList);
                }
            }
            dataReader.Close();
            return clanDrPfamListDict;
        }

        /// <summary>
        /// polydeoxyribonucleotide (DNA)  polyribonucleotide (RNA)
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetClanDnaRnaSequences(string[] clanPfams)
        {
            int[] pfamDnaRnaSeqNumbers = new int[2];
            if (clanPfams.Length == 0)
            {
                pfamDnaRnaSeqNumbers[0] = 0;
                pfamDnaRnaSeqNumbers[1] = 0;
                return pfamDnaRnaSeqNumbers;
            }
            string queryString = string.Format("Select Distinct PdbID, DnaRnaChain From PfamDnaRnas Where PfamID IN ({0});", ParseHelper.FormatSqlListString (clanPfams));
            DataTable pfamDnaRnaTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> dnaSeqList = new List<string>();
            List<string> rnaSeqList = new List<string>();
            string pdbId = "";
            string drChain = "";
            string sequence = "";
            string polymerType = "";
            foreach (DataRow drChainRow in pfamDnaRnaTable.Rows)
            {
                pdbId = drChainRow["PdbID"].ToString();
                drChain = drChainRow["DnaRnaChain"].ToString().TrimEnd();
                sequence = GetChainSequence(pdbId, drChain, out polymerType);
                if (polymerType == "polydeoxyribonucleotide")
                {
                    if (!dnaSeqList.Contains(sequence))
                    {
                        dnaSeqList.Add(sequence);
                    }
                }
                else if (polymerType == "polyribonucleotide")
                {
                    if (!rnaSeqList.Contains(sequence))
                    {
                        rnaSeqList.Add(sequence);
                    }
                }
            }            
            pfamDnaRnaSeqNumbers[0] = dnaSeqList.Count;
            pfamDnaRnaSeqNumbers[1] = rnaSeqList.Count;
            return pfamDnaRnaSeqNumbers;
        }

        #endregion

        #region clan - old version
        /// <summary>
        /// 
        /// </summary>
        public void PrintClanPfamPdbUnp ()
        {
            string clanId = "CL0196";
            string queryString = string.Format("Select Pfam_ID From PfamClanFamily, PfamHmm Where Clan_Acc = '{0}' AND PfamClanFamily.Pfam_Acc = PfamHmm.Pfam_Acc;", clanId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dnaRnaDataDir, clanId + "PfamsPdbUnpInfo.txt"));
            dataWriter.WriteLine("PfamID\t#Entries\t#UniProts\t#Entries-DNA\t#UniProts-DNA\tDNA-UniProts\tNoDNA-UniProts\t#HumanUniProts-DNA\t#HumanUniProts");
            string dataLine = "";
           
            List<string>[] unpDnaRnaOrNotLists = null;
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                int[] pfamDnaRnaNumbers = GetPfamDnaRnaNumbers(pfamId, out unpDnaRnaOrNotLists);
                dataLine = pfamId + "\t" + pfamDnaRnaNumbers[0] + "\t" + pfamDnaRnaNumbers[1] + "\t" +
                    pfamDnaRnaNumbers[2] + "\t" + pfamDnaRnaNumbers[3] + "\t" + 
                    FormatArrayString(unpDnaRnaOrNotLists[0].ToArray()) + "\t" + FormatArrayString(unpDnaRnaOrNotLists[1].ToArray()) + "\t" + 
                    pfamDnaRnaNumbers[4] + "\t" + pfamDnaRnaNumbers[5];
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintDnaRnaPfamsSameClanWithOrNot()
        {
            string queryString = "Select Distinct PfamID From PfamDnaRnas;";
            DataTable dnaRnaPfamsTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            string clanId = "";
            Dictionary<string, List<string>> clanPfamListDict = new Dictionary<string, List<string>>();
            foreach (DataRow pfamRow in dnaRnaPfamsTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                clanId = GetPfamClanID (pfamId);
                if (clanId != "")
                {
                    if (clanPfamListDict.ContainsKey(clanId))
                    {
                        clanPfamListDict[clanId].Add(pfamId);
                    }
                    else
                    {
                        List<string> pfamList = new List<string>();
                        pfamList.Add(pfamId);
                        clanPfamListDict.Add(clanId, pfamList);
                    }
                }
            }
            string dataLine = "";
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dnaRnaDataDir, "SameClanPfamsWithOrNoDnaRnas.txt"));
            dataWriter.WriteLine("ClanID\tDescription\tComment\t#DnaRnaPfams\t#PfamsInPdb\t#Pfams\t#DnaRnaUnps(Human)\t#Unps(Human)\t#HumanUnps\t" +
                "DnaRnaPfams\tClanPfams\tDnaRnaUnps\tDnaRnaHumanUnps\tUnps\tHumanUnps");
            string clanDescript = "";
            foreach (string lsClanId in clanPfamListDict.Keys)
            {
                clanDescript = GetClanDescription(lsClanId);
                string[] clanPfams = GetClanPfams(lsClanId);
                string[] clanPfamsInPdb = GetClanPfamsInPdb(clanPfams);
                List<string>[] clanUnpLists = GetClanDnaRnaUnpInfo(lsClanId); // UNP-DNA/RNA, HUMAN-DNA/RNA, UNP-PDB, HUMAN-PDB
                string[] humanUnps = GetClanHumanUnps(lsClanId);
                dataLine = lsClanId + "\t" + clanDescript + "\t" + clanPfamListDict[lsClanId].Count + "\t" + clanPfamsInPdb.Length + "\t" +
                    clanPfams.Length + "\t" + clanUnpLists[0].Count + "(" + clanUnpLists[1].Count + ")" + "\t" +
                    clanUnpLists[2].Count + "(" + clanUnpLists[3].Count + ")" + "\t" + humanUnps.Length + "\t" +
                    FormatArrayString(clanPfamListDict[lsClanId].ToArray()) + "\t" + FormatArrayString(clanPfams) + "\t" +
                    FormatArrayString(clanUnpLists[0]) + "\t" + FormatArrayString(clanUnpLists[1]) + "\t" +
                    FormatArrayString(clanUnpLists[2]) + "\t" + FormatArrayString(clanUnpLists[3]) + "\t" + FormatArrayString(humanUnps);
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpList"></param>
        /// <returns></returns>
        private List<string> GetHumanUniProts(List<string> unpList)
        {
            List<string> humanUnpList = new List<string>();
            foreach (string unpCode in unpList)
            {
                if (IsUnpHuman (unpCode))
                {
                    humanUnpList.Add(unpCode);
                }
            }
            return humanUnpList;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="allUnpList"></param>
        /// <param name="dnaUnpList"></param>
        /// <returns></returns>
        private List<string> GetDifUnpList(List<string> allUnpList, List<string> dnaUnpList)
        {
            List<string> leftUnpList = new List<string>();
            foreach (string unpCode in allUnpList)
            {
                if (!dnaUnpList.Contains(unpCode))
                {
                    leftUnpList.Add(unpCode);
                }
            }
            return leftUnpList;
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private string GetClanDescription(string clanId)
        {
            string queryString = string.Format("Select Description, Comment From PfamClans Where Clan_ID = '{0}';", clanId);
            DataTable clanIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return clanIdTable.Rows[0]["Description"].ToString().TrimEnd() + "\t" + clanIdTable.Rows[0]["Comment"].ToString();
        }     

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamPdbLigandsInfo(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamLigands Where PfamID = '{0}';", pfamId);
            DataTable pdbligandTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbId From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pdbfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] pfamUnpNumbers = new int[2];
            pfamUnpNumbers[0] = pdbligandTable.Rows.Count;
            pfamUnpNumbers[1] = pdbfamTable.Rows.Count;
            return pfamUnpNumbers;
        }
        #endregion

        #region clan query functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private string[] GetClanHumanUnps(string clanId)
        {
            string queryString = string.Format("Select Distinct UnpCode From HumanPfam, PfamClanFamily, PfamClans " + 
                "Where Clan_ID = '{0}' AND PfamClans.Clan_Acc = PfamClanFamily.Clan_Acc AND PfamClanFamily.Pfam_Acc = HumanPfam.Pfam_Acc;", clanId);
            DataTable unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] clanHumanUnps = new string[unpTable.Rows.Count];
            int count = 0;
            foreach (DataRow unpRow in unpTable.Rows)
            {
                clanHumanUnps[count] = unpRow["UnpCode"].ToString().TrimEnd();
                count++;
            }
            return clanHumanUnps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private List<string>[] GetClanDnaRnaUnpInfo(string clanId)
        {
            string queryString = string.Format("Select Pfam_ID From PfamClanFamily, PfamHmm, PfamClans " +
                " Where Clan_ID = '{0}' AND PfamClans.Clan_Acc = PfamClanFamily.Clan_Acc AND  PfamClanFamily.Pfam_Acc = PfamHmm.Pfam_Acc;", clanId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            List<string> pfamUnpList = null;
            List<string> pfamDnaRnaUnpList = null;
            List<string> clanUnpList = new List<string>();
            List<string> clanHumanUnpList = new List<string>();
            List<string> clanDnaRnaUnpList = new List<string>();
            List<string> clanDnaRnaHumanUnpList = new List<string>();
            //       List<string> leftUnpList = null;
            foreach (DataRow pfamRow in pfamIdTable.Rows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                Dictionary<string, string> entryEntityUnpDict = GetPfamPdbUnp(pfamId, out pfamUnpList);
                queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
                DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                int[] pdbUnpNumbers = GetDNARNAInteractingPdbUnps(pfamId, entryEntityUnpDict, out pfamDnaRnaUnpList);
                foreach (string unp in pfamUnpList)
                {
                    if (!clanUnpList.Contains(unp))
                    {
                        clanUnpList.Add(unp);
                    }
                }
                foreach (string unp in pfamDnaRnaUnpList)
                {
                    if (!clanDnaRnaUnpList.Contains(unp))
                    {
                        clanDnaRnaUnpList.Add(unp);
                    }
                }
            }
            List<string>[] clanUnpLists = new List<string>[4];
            clanUnpLists[0] = clanDnaRnaUnpList;
            clanUnpLists[1] = GetHumanUniProts(clanDnaRnaUnpList);
            clanUnpLists[2] = clanUnpList;
            clanUnpLists[3] = GetHumanUniProts(clanUnpList);
            return clanUnpLists;
        }
        #endregion
    }
}
