using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace ProtCIDPaperDataLib.paper
{
    public class PdbBaDataInfo : PaperDataInfo
    {
        private string rcsbBaDataDir = @"X:\Qifang\Paper\BiolAssem_CurrentOpinioReview\rcsbData";

        /*PDB code
ASU stoichiometry
ASU symmetry (if available - it may not be which is OK)
first BA stoichiometry
first BA symmetry
second BA stoichiometry (if there is a second BA)
second BA symmetry (if there is one)
method (XRAY, EM, etc)
resolution
date of release or deposit (if you have this already; otherwise skip)
number of unique protein entitites
list of uniprots

        * */
        public void GeneratePdbBAInfoFile ()
        {
            string logFile = Path.Combine(rcsbBaDataDir, "BaProtSymStoichLog.txt");
            StreamWriter logWriter = new StreamWriter(logFile);
            string pdbBaSymStoichFile = Path.Combine (rcsbBaDataDir, "PdbAsuBAProtSymStoichInfo_pdbls.txt");
            StreamWriter dataWriter = new StreamWriter (pdbBaSymStoichFile);
            dataWriter.WriteLine("PdbID\tASU Stoichiometry\tASU Symmetry\tBA_1 Stoichiometry\tBA_1 Symmetry\tBA_2 Stoichiometry\tBA_2 Symmetry\t" + 
                "Method\tResolution\tDepositFileDate\tReleaseFileDate\t#Entities\tASU UniProts\tBA_1 UniProts\tBA_2 UniProts");

            string queryString = "Select PdbID, Method, Resolution, DepositFileDate, ReleaseFileDate From PdbEntry Order By PdbID;";
            DataTable protEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            Dictionary<string, int> entryNumEntityDict = GetEntryNumProtEntities();
            string entryProtSymStoichFile = Path.Combine(rcsbBaDataDir, "PdbProtSymStoichiometryDataFile_more.txt");
    //        Dictionary<string, Dictionary<string, string>> entryDateMethodResDict = null;
            Dictionary<string, Dictionary<string, string[]>> entryProtSymStoichDict = ReadRcsbEntryProtStoichiometry(entryProtSymStoichFile);

 /*           string rcsbProtStoiFile = Path.Combine(rcsbBaDataDir, "RcsbProtStoichiometryList.txt");
            Dictionary<string, string> protStoiNameDict = ReadProtStoichiometryNameMap(rcsbProtStoiFile);
            string rcsbProtSymFile = Path.Combine(rcsbBaDataDir, "RcsbProtSymmetryList.txt");
            Dictionary<string, string> protSymNameDict = ReadProtSymmetryNameMap(rcsbProtSymFile);*/
            Dictionary<string, int> symAsuCountDict = new Dictionary<string, int>();
            Dictionary<string, int> symBaCountDict = new Dictionary<string, int>();
            Dictionary<string, int> symBa2CountDict = new Dictionary<string, int>();
            Dictionary<string, int> stoiAsuCountDict = new Dictionary<string, int>();
            Dictionary<string, int> stoiBaCountDict = new Dictionary<string, int>();
            Dictionary<string, int> stoiBa2CountDict = new Dictionary<string, int>();
            
            string dataLine = "";
  //          string pdbId = "";
            string asuSym = "";
            string asuStoi = "";
            string baSym = "";
            string baStoi = "";
            string baUniprots = "";
            string ba2Sym = "";
            string ba2Stoi = "";
            string ba2Uniprots = "";
            int baId = 0;
            List<string> entryList = new List<string>(entryProtSymStoichDict.Keys);
            entryList.Sort();
     //      foreach (DataRow entryRow in protEntryTable.Rows)
            foreach (string pdbId in entryList)
            {
                try
                {
                    string[] entryUniprots = GetEntryUniprots(pdbId);
                    Dictionary<int, List<string>> entryBaUniprotsDict = GetEntryBaUniprots(pdbId);
                    if (entryProtSymStoichDict[pdbId].ContainsKey("NMR"))
                    {
                        asuSym = entryProtSymStoichDict[pdbId]["NMR"][0];
                        asuStoi = entryProtSymStoichDict[pdbId]["NMR"][1];
                    }
                    else
                    {
                        asuSym = entryProtSymStoichDict[pdbId]["ASU"][0];
                        asuStoi = entryProtSymStoichDict[pdbId]["ASU"][1];
                    }
                    dataLine = pdbId + "\t" + asuSym + "\t" + asuStoi + "\t";

                    if (entryProtSymStoichDict[pdbId].ContainsKey("BA1"))
                    {
                        baSym = entryProtSymStoichDict[pdbId]["BA1"][0];
                        baStoi = entryProtSymStoichDict[pdbId]["BA1"][1];
                    }
                    else
                    {
                        baSym = "-";
                        baStoi = "-";
                    }
                    dataLine += (baSym + "\t" + baStoi + "\t");
                    if (entryProtSymStoichDict[pdbId].ContainsKey("BA2"))
                    {
                        ba2Sym = entryProtSymStoichDict[pdbId]["BA2"][0];
                        ba2Stoi = entryProtSymStoichDict[pdbId]["BA2"][1];
                    }
                    else
                    {
                        ba2Sym = "-";
                        ba2Stoi = "-";
                    }
                    dataLine += (ba2Sym + "\t" + ba2Stoi + "\t");
                    baId = 1;
                    baUniprots = GetEntryBaUniprots(baId, entryBaUniprotsDict);
                    baId = 2;
                    ba2Uniprots = GetEntryBaUniprots(baId, entryBaUniprotsDict);
                    if (entryProtSymStoichDict[pdbId].ContainsKey("Method"))
                    {
                        dataLine += (entryProtSymStoichDict[pdbId]["Method"][0] + "\t");
                    }
                    else
                    {
                        dataLine += "-\t";
                    }
                    if (entryProtSymStoichDict[pdbId].ContainsKey("Resolution"))
                    {
                        dataLine += (entryProtSymStoichDict[pdbId]["Resolution"][0] + "\t");
                    }
                    else
                    {
                        dataLine += "-\t";
                    }
                    if (entryProtSymStoichDict[pdbId].ContainsKey("Deposit"))
                    {
                        dataLine += (entryProtSymStoichDict[pdbId]["Deposit"][0] + "\t");
                    }
                    else
                    {
                        dataLine += "-\t";
                    }
                    if (entryProtSymStoichDict[pdbId].ContainsKey("Release"))
                    {
                        dataLine += (entryProtSymStoichDict[pdbId]["Release"][0] + "\t");
                    }
                    else
                    {
                        dataLine += "-\t";
                    }
                    if (entryNumEntityDict.ContainsKey(pdbId))
                    {
                        dataLine += (entryNumEntityDict[pdbId] + "\t");                             
                    }
                    else
                    {
                        dataLine += "-\t";
                    }
                    dataLine += (FormatArrayString(entryUniprots, ';') + "\t" + baUniprots + "\t" + ba2Uniprots);
                    dataWriter.WriteLine(dataLine);

                    AddSymStoiTypeToDict(asuSym, symAsuCountDict);
                    AddSymStoiTypeToDict(asuStoi, stoiAsuCountDict);
                    AddSymStoiTypeToDict(baSym, symBaCountDict);
                    AddSymStoiTypeToDict(baStoi, stoiBaCountDict);
                    AddSymStoiTypeToDict(ba2Sym, symBa2CountDict);
                    AddSymStoiTypeToDict(ba2Stoi, stoiBa2CountDict);

                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(pdbId + ": " + ex.Message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close();
            logWriter.Close();

            string pdbSymStoiSumFile = Path.Combine(rcsbBaDataDir, "PdbAsuBAProtSymStoichSumInfo.txt");
            StreamWriter sumWriter = new StreamWriter(pdbSymStoiSumFile);
            sumWriter.Write("Protein Symmetry of ASU\n");
            sumWriter.Write(FormatSymStoiCountDict (symAsuCountDict));
            sumWriter.Write("Protein Stoichiometry of ASU\n");
            sumWriter.Write(FormatSymStoiCountDict (stoiAsuCountDict));
            sumWriter.Write("Protein Symmetry of BA 1\n");
            sumWriter.Write(FormatSymStoiCountDict(symBaCountDict));
            sumWriter.Write("Protein Stoichiometry of BA 1\n");
            sumWriter.Write(FormatSymStoiCountDict(stoiBaCountDict));
            sumWriter.Write("Protein Symmetry of BA 2\n");
            sumWriter.Write(FormatSymStoiCountDict(symBa2CountDict));
            sumWriter.Write("Protein Stoichiometry of BA 2\n");
            sumWriter.Write(FormatSymStoiCountDict(stoiBa2CountDict));
            sumWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symStoiType"></param>
        /// <param name="symStoiCountDict"></param>
        private void AddSymStoiTypeToDict(string symStoiType, Dictionary<string, int> symStoiCountDict)
        {
            if (symStoiType != "-")
            {
                if (symStoiCountDict.ContainsKey(symStoiType))
                {
                    symStoiCountDict[symStoiType]++;
                }
                else
                {
                    symStoiCountDict.Add(symStoiType, 1);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baId"></param>
        /// <param name="baUniprotsDict"></param>
        /// <returns></returns>
        private string GetEntryBaUniprots (int baId, Dictionary<int, List<string>> baUniprotsDict)
        {
            string baUniprots = "-";
            if (baUniprotsDict.ContainsKey (baId))
            {
                baUniprots = FormatArrayString(baUniprotsDict[baId].ToArray(), ';');
            }
            return baUniprots;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symStoiCountDict"></param>
        /// <returns></returns>
        private string FormatSymStoiCountDict (Dictionary<string, int> symStoiCountDict)
        {
            string symStoiSumLine = "";
            List<string> typeList = new List<string>(symStoiCountDict.Keys);
            typeList.Sort();
            foreach (string symStoiType in typeList)
            {
                symStoiSumLine += (symStoiType + "    " + symStoiCountDict[symStoiType].ToString() + "\n");
            }
            return symStoiSumLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, int> GetEntryNumProtEntities ()
        {
            string queryString = "Select PdbID, Count(Distinct EntityID) As entityCount From AsymUnit WHere PolymerType = 'polypeptide' Group By PdbID;";
            DataTable entryEntityCountTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, int> entryEntityNumDict = new Dictionary<string, int>();
            string pdbId = "";
            int numEntity = 0;
            foreach (DataRow entryRow in entryEntityCountTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                numEntity = Convert.ToInt32(entryRow["EntityCOunt"].ToString ());
                entryEntityNumDict.Add(pdbId, numEntity);
            }
            return entryEntityNumDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryUniprots (string pdbId)
        {
            string queryString = string.Format("Select Distinct DbCode From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode From PdbDbRefXml Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
                unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            string[] entryUniprots = new string[unpCodeTable.Rows.Count];
            int count = 0;
            foreach (DataRow unpRow in unpCodeTable.Rows)
            {
                entryUniprots[count] = unpRow["DbCode"].ToString().TrimEnd();
                count++;
            }
            return entryUniprots;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetEntryBaUniprots (string pdbId)
        {
            string queryString = string.Format("Select BiolUnit.PdbID, BiolUnitID, PdbDbRefSifts.EntityID, DbCode From BiolUnit, AsymUnit, PdbDbRefSifts " + 
                " Where BiolUnit.PdbID = '{0}' AND DbName = 'UNP' AND " + 
                " BiolUnit.PdbID = AsymUnit.PdbID AND BiolUnit.AsymID = AsymUnit.AsymID AND " +
                " AsymUnit.PdbID = PdbDbRefSifts.PdbID AND AsymUnit.EntityID = PdbDbRefSifts.EntityID;", pdbId);
            DataTable baUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (baUnpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select BiolUnit.PdbID, BiolUnitID, PdbDbRefXml.EntityID, DbCode From BiolUnit, AsymUnit, PdbDbRefXml " +
                        " Where BiolUnit.PdbID = '{0}' AND DbName = 'UNP' AND " +
                        " BiolUnit.PdbID = AsymUnit.PdbID AND BiolUnit.AsymID = AsymUnit.AsymID AND " +
                        " AsymUnit.PdbID = PdbDbRefXml.PdbID AND AsymUnit.EntityID = PdbDbRefXml.EntityID;", pdbId);
                baUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            int baId = 0;
            string unpCode = "";
            Dictionary<int, List<string>> baUniprotsDict = new Dictionary<int, List<string>> ();
            foreach (DataRow baRow in baUnpTable.Rows)
            {
                baId = Convert.ToInt32 (baRow["BiolUnitID"].ToString());
                unpCode = baRow["DbCode"].ToString().TrimEnd();
                if (baUniprotsDict.ContainsKey (baId))
                {
                    if (! baUniprotsDict[baId].Contains (unpCode))
                    {
                        baUniprotsDict[baId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> unpCodeList = new List<string>();
                    unpCodeList.Add(unpCode);
                    baUniprotsDict.Add(baId, unpCodeList);
                }
            }
            return baUniprotsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rcsbProtStoichFile"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, string[]>> ReadRcsbEntryProtStoichiometry (string rcsbProtStoichFile)
        {
            Dictionary<string, Dictionary<string, string[]>> entrySymStoichDict = new Dictionary<string, Dictionary<string, string[]>> ();
            StreamReader dataReader = new StreamReader(rcsbProtStoichFile);
            string line = "";
            string pdbId = "";
            string asuBa = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                pdbId = fields[0].ToLower ();
                Dictionary<string, string[]> asuBaSymStoichDict = new Dictionary<string, string[]>();
                for (int i = 1; i < fields.Length; i ++)
                {
                    string[] asuBaFields = fields[i].Split(":;".ToCharArray ());
                    asuBa = asuBaFields[0];
                    if (asuBaFields.Length >= 5)
                    {
                        string[] symStoichFields = new string[4];
                        Array.Copy(asuBaFields, 1, symStoichFields, 0, 4);
                        asuBaSymStoichDict.Add(asuBa, symStoichFields);
                    }
                    else
                    {
                        string[] symStoichFields = new string[1];
                        Array.Copy(asuBaFields, 1, symStoichFields, 0, 1);
                        asuBaSymStoichDict.Add(asuBa, symStoichFields);
                    }                    
                }
                entrySymStoichDict.Add(pdbId, asuBaSymStoichDict);
            }
            dataReader.Close();
            return entrySymStoichDict;
        }     

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rcsbProtSymFile"></param>
        /// <returns></returns>
        private Dictionary<string, string> ReadRcsbEntryProtSymmetry (string rcsbProtSymFile)
        {
            Dictionary<string, string> entryProtSymDict = new Dictionary<string, string>();
            string line = "";
            StreamReader dataReader = new StreamReader(rcsbProtSymFile);
            string symmetry = "";
            string pdbId = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split(": ,".ToCharArray ());
                symmetry = fields[0];
                for (int i = 1; i < fields.Length; i ++)
                {
                    if (fields[i] == "")
                    {
                        continue;
                    }
                    pdbId = fields[i].ToLower();
                    if (!entryProtSymDict.ContainsKey(pdbId))
                    {
                        entryProtSymDict.Add(pdbId, symmetry);
                    }
                }
            }
            dataReader.Close();

            return entryProtSymDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protStoichFile"></param>
        /// <returns></returns>
        private Dictionary<string, string> ReadProtStoichiometryNameMap (string protStoichFile)
        {
            Dictionary<string, string> stoichNameDict = new Dictionary<string, string>();
            StreamReader dataReader = new StreamReader(protStoichFile);
            string line = dataReader.ReadLine ();
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("Distinct Protein Sequences") > -1)
                {
                    continue;
                }
                string[] fields = line.Split (' ');
                if (! stoichNameDict.ContainsKey(fields[3]))
                {
                    stoichNameDict.Add(fields[3], fields[0] + " " + fields[1]);
                }
            }
            dataReader.Close();
            return stoichNameDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protStoichFile"></param>
        /// <returns></returns>
        private Dictionary<string, string> ReadProtSymmetryNameMap(string protSymmetryFile)
        {
            Dictionary<string, string> symmetryNameDict = new Dictionary<string, string>();
            StreamReader dataReader = new StreamReader(protSymmetryFile);
            string line = dataReader.ReadLine();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                if (! symmetryNameDict.ContainsKey(fields[2]))
                {
                    symmetryNameDict.Add(fields[2], fields[0]);
                }
            }
            dataReader.Close();
            return symmetryNameDict;
        }
    }
}
