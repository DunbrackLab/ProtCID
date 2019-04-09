using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.AuxFuncs;
using AuxFuncLib;
using CrystalInterfaceLib.DomainInterfaces;

namespace ProtCIDPaperDataLib
{
    public class BiogenDataGen 
    {
        string biogenDataDir = @"X:\Qifang\Projects\BioGen_Bioplex";
        double simQscoreCutoff = 0.2;
        private string[] repeatPfams = null;
        private DbQuery dbQuery = new DbQuery();
        // PdbID + DomaininterfaceID + InPdbba + ASA + Species + UnpCode + Pfam1 + Pfam2 intra/inter

        public BiogenDataGen ()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();

                ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetRepeatPfams()
        {
            if (repeatPfams == null)
            {
                string queryString = "Select Pfam_ID From PfamHmm Where Type = 'Repeat' Order by Pfam_ID;";
                DataTable repeatPfamTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                repeatPfams = new string[repeatPfamTable.Rows.Count];
                int count = 0;
                foreach (DataRow pfamRow in repeatPfamTable.Rows)
                {
                    repeatPfams[count] = pfamRow["Pfam_ID"].ToString().TrimEnd();
                    count++;
                }
            }
            return repeatPfams;
        }

        #region domain interfaces
        string[] columnsNeeded = { "PdbID", "InPdbBA", "ASA", "Species", "UnpCode", "Pfam-Pfam", "Intra", "ClusterID", "#CFs/Cluster", "#CFs/Group" };
        private double domainInterfaceAsaCutoff = 100.0;
        private double rangeCoverage = 0.50;
        private double simInterfaceQcutoff = 0.5;
        private Dictionary<int, string> relationPfamsHash = new Dictionary<int,string> ();

        /// <summary>
        /// 
        /// </summary>
        public void GenerateDomainInterfaceInfoFile()
        {
            StreamWriter logWriter = new StreamWriter(Path.Combine(biogenDataDir, "domainInterfaceInfoLog.txt"), true);
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            StreamWriter dataWriter = new StreamWriter(Path.Combine(biogenDataDir, "KrishnaBiogen_DomainInterfacesInfo.txt"), true);
     //       dataWriter.WriteLine("PdbID\tDomainInterfaceID\tInPdbBA\tASA\tSpecies\tUnpCode\tintra\tPfam-Pfam\tClusterID\t#CFs/Cluster\t#CFs/Group");
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                try
                {
                    PrintEntryBiogenInfo(pdbId, dataWriter);
                    dataWriter.Flush();
                    logWriter.WriteLine(pdbId);
                }
                catch (Exception ex)
                {
                    string message = "Retrieve " + pdbId + " domain interface info error: " + ex.Message;
                    logWriter.WriteLine(message);
                    logWriter.Flush();
                }
            }
            dataWriter.Close();
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void FillInClusterInfo()
        {
            string domainInterfaceFile = Path.Combine(biogenDataDir, "KrishnaBiogen_DomainInterfacesInfo.txt");
            string domainInterfaceClusterFile = Path.Combine(biogenDataDir, "KrishnaBiogen_DomainInterfacesClusterInfo_1.txt");
            string headerLine = "";
            DataTable domainInterfaceTable = ReadDomainInterfaceTableFromFile(domainInterfaceFile, out headerLine);
            List<string> pfamRelList = new List<string>();
            string pfamRel = "";
            DataTable clusterInfoTable = GetEntryDomainInterfaceClusterInfo();

            foreach (DataRow dataRow in domainInterfaceTable.Rows)
            {
                pfamRel = dataRow["Pfams"].ToString();
                if (!pfamRelList.Contains(pfamRel))
                {
                    pfamRelList.Add(pfamRel);
                }
            }
   //         string clusterInfo = "";
            string domainLine = "";
            string clusterId = "-1";
            string numCfCluster = "-1";
            string numCfRelation = "-1";
            StreamWriter dataWriter = new StreamWriter(domainInterfaceClusterFile);
            dataWriter.WriteLine(headerLine);
            foreach (string pfamPair in pfamRelList)
            {
                if (pfamPair == "-1" || pfamPair == "")
                {
                    continue;
                }
                DataTable relClusterTable = GetPfamRelClusterInfo(pfamPair, clusterInfoTable);
                DataRow[] relDomainInterfaceRows = domainInterfaceTable.Select(string.Format("Pfams = '{0}'", pfamPair));
                foreach (DataRow domainInterfaceRow in relDomainInterfaceRows)
                {
                    clusterId = "-1";
                    numCfCluster = "-1";
                    numCfRelation = "-1";
                    DataRow[] clusterInfoRows = relClusterTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'",
                        domainInterfaceRow["PdbID"].ToString(), domainInterfaceRow["DomainInterfaceID"].ToString()));

                    if (clusterInfoRows.Length > 0)
                    {
                        numCfCluster = clusterInfoRows[0]["NumOfCfgCluster"].ToString();
                        numCfRelation = clusterInfoRows[0]["NumOfCfgRelation"].ToString();
                        clusterId = clusterInfoRows[0]["ClusterID"].ToString();
                    }
                    //               clusterInfo = clusterId + "\t" + numCfCluster + "\t" + numCfRelation;
                    domainInterfaceRow["ClusterID"] = clusterId;
                    domainInterfaceRow["NumCFsCluster"] = numCfCluster;
                    domainInterfaceRow["NumCFsGroup"] = numCfRelation;
                    domainLine = ParseHelper.FormatDataRow(domainInterfaceRow);
                    dataWriter.WriteLine(domainLine);
                }
                dataWriter.Flush();
            }
            dataWriter.Close();
        }
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaceClusterInfo()
        {
            string queryString = "Select PfamDomainClusterInterfaces.RelSeqID, PfamDomainClusterInterfaces.ClusterID, PdbID, DomainInterfaceID, NumOfCfgCluster, NumOfCfgRelation" +
            " From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo Where PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID " + 
            " AND PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamPair"></param>
        /// <param name="clusterTable"></param>
        /// <returns></returns>
        private DataTable GetPfamRelClusterInfo (string pfamPair, DataTable clusterTable)
        {
            int relSeqId = GetRelSeqId(pfamPair);
            DataRow[] relClusterRows = clusterTable.Select(string.Format ("RelSeqID = '{0}'", relSeqId));
            DataTable relClusterTable = clusterTable.Clone();
            foreach (DataRow relClusterRow in relClusterRows)
            {
                DataRow newRow = relClusterTable.NewRow();
                newRow.ItemArray = relClusterRow.ItemArray;
                relClusterTable.Rows.Add(newRow);
            }
            return relClusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamPair"></param>
        /// <returns></returns>
        private int GetRelSeqId (string pfamPair)
        {
            string[] pfams = pfamPair.Split(';');
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' AND FamilyCode2 = '{1}';", pfams[0], pfams[1]);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }

        private DataTable ReadDomainInterfaceTableFromFile (string domainInterfaceFile, out string headerLine)
        {
            DataTable domainInterfaceTable = new DataTable("DomainInterfaces");
            StreamReader dataReader = new StreamReader(domainInterfaceFile);
            headerLine = dataReader.ReadLine();
            string[] headerColumns = headerLine.Split('\t');
            foreach (string col in headerColumns)
            {
                domainInterfaceTable.Columns.Add(new DataColumn (col));
            }            
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                List<object> itemList = new List<object>(fields);
                itemList.Add(-1);
                itemList.Add(-1);
                itemList.Add(-1);
                DataRow dataRow = domainInterfaceTable.NewRow();
                dataRow.ItemArray = itemList.ToArray ();
                domainInterfaceTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            return domainInterfaceTable;
        }

        public void FillInRelationPfams()
        {
            StreamReader dataReader = new StreamReader(@"C:\Projects\BioGen_Bioplex\KrishnaBiogen_DomainInterfacesInfo.txt");
            StreamWriter dataWriter = new StreamWriter(@"C:\Projects\BioGen_Bioplex\KrishnaBiogen_DomainInterfacesInfo_filled.txt");
            string line = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            int relSeqId = 0;
            string relPfams = "";
            string dataLine = "";
            int i = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] lineFields = line.Split('\t');
                if (lineFields.Length <= 6)
                {
                    continue;
                }
                if (lineFields[6] == "-")
                {
                    pdbId = lineFields[0];
                    domainInterfaceId = Convert.ToInt32(lineFields[1]);
                    relSeqId = GetDomainRelSeqId(pdbId, domainInterfaceId);
                    relPfams = GetRelationPfams(relSeqId);
                    dataLine = "";
                    i = 0;
                    foreach (string lineField in lineFields)
                    {
                        if (i != 6)
                        {
                            dataLine += (lineField + "\t");
                        }
                        else
                        {
                            dataLine += (relPfams + "\t");
                        }
                        i++;
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd('\t'));
                }
                else
                {
                    dataWriter.WriteLine(line);
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
        private int GetDomainRelSeqId(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dataWriter"></param>
        public void PrintEntryBiogenInfo(string pdbId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}' AND SurfaceArea > {1};", pdbId, domainInterfaceAsaCutoff);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            DataTable clusterInfoTable = GetEntryDomainInterfaceClusterInfo(pdbId);
            DataTable domainSpeciesUnpInfoTable = GetDomainInterfaceSourceInfo(domainInterfaceTable);
            int domainInterfaceId = 0;
            double surfaceArea = 0;
            long[] interfaceDomainIds = null;
            bool isInPdbBa = false;
            string[] entryBAs = null;
            string species = "";
            string name = "";
            string unpCode = "";
            string dataLine = "";
            string intra = "0";
            int relSeqId = 0;
            string relPfams = "";
            string clusterInfo = "";
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                isInPdbBa = false;
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, domainInterfaceTable);
                if (surfaceArea < domainInterfaceAsaCutoff)
                {
                    continue;
                }
                intra = "0";
                if (domainInterfaceRow["InterfaceID"].ToString() == "0")
                {
                    intra = "1";
                }

                entryBAs = IsDomainInterfaceInBU(pdbId, domainInterfaceId, "pdb");
                if (entryBAs.Length > 0)
                {
                    isInPdbBa = true;
                }
                interfaceDomainIds = GetDomainInterfaceDomainIds(pdbId, domainInterfaceId, domainInterfaceTable);
                string[] speciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(pdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);
                species = speciesNameUnp[0];
                name = speciesNameUnp[1];
                unpCode = speciesNameUnp[2];

                if (isInPdbBa)
                {
                    dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t1";
                }
                else
                {
                    dataLine = pdbId + "\t" + domainInterfaceId.ToString() + "\t0";
                }
                clusterInfo = GetDomainInterfaceClusterInfo(pdbId, domainInterfaceId, clusterInfoTable, domainInterfaceTable);
                relSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString ());
                relPfams = GetRelationPfams(relSeqId);
                dataLine = dataLine + "\t" + surfaceArea.ToString() + "\t" + species + "\t" + unpCode + "\t" + intra + "\t" + relPfams
                    /*+ "\t" + clusterInfo*/;

                dataWriter.WriteLine(dataLine);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        public double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId, DataTable clusterInterfaceTable)
        {
            DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            double surfaceArea = -1;
            if (interfaceRows.Length > 0)
            {
                surfaceArea = Convert.ToDouble(interfaceRows[0]["SurfaceArea"].ToString());
            }
            return surfaceArea;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="clusterInfoTable"></param>
        /// <returns></returns>
        private string GetDomainInterfaceClusterInfo(string pdbId, int domainInterfaceId, DataTable clusterInfoTable, DataTable domainInterfaceTable)
        {
            DataRow[] clusterInfoRows = clusterInfoTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            string clusterInfo = "";
            int relSeqId = 0;
            string relPfams = "-";
            string clusterId = "-1";
            string numCfCluster = "-1";
            string numCfRelation = "-1";
            if (clusterInfoRows.Length > 0)
            {
                relSeqId = Convert.ToInt32(clusterInfoRows[0]["RelSeqID"]);
                relPfams = GetRelationPfams(relSeqId);
                numCfCluster = clusterInfoRows[0]["NumOfCfgCluster"].ToString();
                numCfRelation = clusterInfoRows[0]["NumOfCfgRelation"].ToString();
                clusterId = clusterInfoRows[0]["ClusterID"].ToString();
            }
            else
            {
                DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
                relSeqId = Convert.ToInt32(domainInterfaceRows[0]["RelSeqID"].ToString());
                relPfams = GetRelationPfams(relSeqId);
            }
            clusterInfo = relPfams + "\t" + clusterId + "\t" + numCfCluster + "\t" + numCfRelation;
            return clusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaceClusterInfo(string pdbId)
        {
            string queryString = string.Format("Select PfamDomainClusterInterfaces.RelSeqID, PfamDomainClusterInterfaces.ClusterID, PdbID, DomainInterfaceID, NumOfCfgCluster, NumOfCfgRelation" +
            " From PfamDomainClusterInterfaces, PfamDomainClusterSumInfo Where PdbID = '{0}' AND " +
            " PfamDomainClusterInterfaces.RelSeqID = PfamDomainClusterSumInfo.RelSeqID AND PfamDomainClusterInterfaces.ClusterID = PfamDomainClusterSumInfo.ClusterID;", pdbId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetRelationPfams(int relSeqId)
        {
            string relPfams = "";
            if (relationPfamsHash.ContainsKey(relSeqId))
            {
                relPfams = (string)relationPfamsHash[relSeqId];
            }
            else
            {
                string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable relPfamsTable = ProtCidSettings.protcidQuery.Query( queryString);
                relPfams = relPfamsTable.Rows[0]["FamilyCode1"].ToString().TrimEnd() + ";" + relPfamsTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                relationPfamsHash.Add(relSeqId, relPfams);
            }
            return relPfams;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private long[] GetDomainInterfaceDomainIds(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            long[] domainIds = new long[2];
            domainIds[0] = Convert.ToInt64(domainInterfaceRows[0]["DomainID1"].ToString());
            domainIds[1] = Convert.ToInt64(domainInterfaceRows[0]["DomainID2"].ToString());
            return domainIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        public DataTable GetDomainInterfaceSourceInfo(DataTable domainInterfaceTable)
        {
            DataTable domainSourceInfoTable = new DataTable();
            string[] sourceInfoColumns = { "PdbID", "DomainID", "EntityID", "Name", "Species", "UnpCode" };
            foreach (string srcInfoCol in sourceInfoColumns)
            {
                domainSourceInfoTable.Columns.Add(new DataColumn(srcInfoCol));
            }
            string[] entryDomains = GetEntryDomains(domainInterfaceTable);
            string pdbId = "";
            long domainId = 0;
            int entityId = 0;
            string unpCode = "";
            foreach (string entryDomain in entryDomains)
            {
                pdbId = entryDomain.Substring(0, 4);

                domainId = Convert.ToInt64(entryDomain.Substring(4, entryDomain.Length - 4));
                DataTable domainInfoTable = GetDomainInfo(pdbId, domainId);
                entityId = -1;
                if (domainInfoTable.Rows.Count > 0)
                {
                    entityId = Convert.ToInt32(domainInfoTable.Rows[0]["EntityID"].ToString());
                }
                Range[] domainRanges = FormatDomainRanges(domainInfoTable);
                DataRow dataRow = domainSourceInfoTable.NewRow();
                dataRow["PdbID"] = pdbId;
                dataRow["DomainID"] = domainId;
                dataRow["EntityID"] = entityId;
                string[] speciesName = GetSpeciesNameInfo(pdbId, entityId);
                dataRow["Species"] = speciesName[0];
                dataRow["Name"] = speciesName[1];
                unpCode = GetDomainUniprotCode(pdbId, entityId, domainRanges);
                dataRow["UnpCode"] = unpCode;
                domainSourceInfoTable.Rows.Add(dataRow);
            }
            return domainSourceInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationDomainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetEntryDomains(DataTable domainInterfaceTable)
        {
            List<string> entryDomainList = new List<string>();
            string entryDomain = "";
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                entryDomain = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID1"].ToString();
                if (!entryDomainList.Contains(entryDomain))
                {
                    entryDomainList.Add(entryDomain);
                }
                entryDomain = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID2"].ToString();
                if (!entryDomainList.Contains(entryDomain))
                {
                    entryDomainList.Add(entryDomain);
                }
            }
            string[] entryDomains = new string[entryDomainList.Count];
            entryDomainList.CopyTo(entryDomains);
            return entryDomains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainInfo(string pdbId, long domainId)
        {
            string queryString = string.Format("Select EntityID, SeqStart, SeqEnd From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable domainInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return domainInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInfoTable"></param>
        /// <returns></returns>
        private Range[] FormatDomainRanges(DataTable domainInfoTable)
        {
            Range[] domainRanges = new Range[domainInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainRow in domainInfoTable.Rows)
            {
                domainRanges[count] = new Range();
                domainRanges[count].startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRanges[count].endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                count++;
            }
            return domainRanges;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="domainRange"></param>
        /// <returns></returns>
        private string GetDomainUniprotCode(string pdbId, int entityId, Range[] domainRanges)
        {
            string queryString = string.Format("Select PdbDbRefSifts.RefID, AlignID, SeqAlignBeg, SeqAlignEnd, DbCode " +
                " From PdbDbRefSifts, PdbDbRefSeqSifts Where PdbDbRefSifts.PdbID = '{0}' AND PdbDbRefSifts.EntityID = {1} AND " +
                " PdbDbRefSifts.DbName = 'UNP' AND " +
                " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID AND " +
                " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID Order By AlignID;", pdbId, entityId);
            DataTable dbRefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (dbRefTable.Rows.Count == 0)
            {
                queryString = string.Format("Select PdbDbRefXml.RefID, AlignID, SeqAlignBeg, SeqAlignEnd, DbCode " +
                       " From PdbDbRefXml, PdbDbRefSeqXml Where PdbDbRefXml.PdbID = '{0}' AND PdbDbRefXml.EntityID = {1} AND " +
                       " PdbDbRefXml.DbName = 'UNP' AND " +
                       " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID AND " +
                       " PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID Order By AlignID;", pdbId, entityId);
                dbRefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            }
            Dictionary<int, Range[]> refUnpRangeHash = GetUnpRanges(dbRefTable);
            string unpCode = "";
            foreach (int refId in refUnpRangeHash.Keys)
            {
                if (IsDomainUnpSeqOverlap(domainRanges, refUnpRangeHash[refId]))
                {
                    unpCode = GetRefUnpCode(refId, dbRefTable);
                }
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="dbRefTable"></param>
        /// <returns></returns>
        private string GetRefUnpCode(int refId, DataTable dbRefTable)
        {
            DataRow[] refRows = dbRefTable.Select(string.Format("RefID = '{0}'", refId));
            string unpCode = "";
            if (refRows.Length > 0)
            {
                unpCode = refRows[0]["DbCode"].ToString().TrimEnd();
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbRefTable"></param>
        /// <returns></returns>
        private Dictionary<int, Range[]> GetUnpRanges(DataTable dbRefTable)
        {
            List<int> refIdList = new List<int> ();
            int refId = 0;
            foreach (DataRow dbRefRow in dbRefTable.Rows)
            {
                refId = Convert.ToInt32(dbRefRow["RefID"].ToString());
                if (!refIdList.Contains(refId))
                {
                    refIdList.Add(refId);
                }
            }
            Dictionary<int, Range[]> entityUnpRangeHash = new Dictionary<int,Range[]> ();
            foreach (int lsRefId in refIdList)
            {
                DataRow[] refIdRows = dbRefTable.Select(string.Format("RefID = '{0}'", lsRefId));
                int alignId = Convert.ToInt32(refIdRows[0]["AlignID"].ToString());
                DataRow[] dbRefRows = dbRefTable.Select(string.Format("AlignID = '{0}'", alignId));
                Range[] unpRanges = new Range[dbRefRows.Length];
                for (int i = 0; i < dbRefRows.Length; i++)
                {
                    unpRanges[i] = new Range();
                    unpRanges[i].startPos = Convert.ToInt32(dbRefRows[i]["SeqAlignBeg"].ToString());
                    unpRanges[i].endPos = Convert.ToInt32(dbRefRows[i]["SeqAlignEnd"].ToString());
                }
                entityUnpRangeHash.Add(lsRefId, unpRanges);
            }
            return entityUnpRangeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <param name="unpRanges"></param>
        /// <returns></returns>
        private bool IsDomainUnpSeqOverlap(Range[] domainRanges, Range[] unpRanges)
        {
            int overlap_all = 0;
            int rangeOverlap = 0;
            foreach (Range domainRange in domainRanges)
            {
                foreach (Range unpRange in unpRanges)
                {
                    rangeOverlap = GetRangeOverlap(domainRange, unpRange);
                    overlap_all += rangeOverlap;
                }
            }
            int domainLength = GetRangeLength(domainRanges);
            int unpLength = GetRangeLength(unpRanges);
            double domainCoverage = (double)overlap_all / (double)(domainLength);
            double unpCoverage = (double)overlap_all / (double)(unpLength);
            if (domainCoverage >= rangeCoverage || unpCoverage >= rangeCoverage)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private int GetRangeLength(Range[] ranges)
        {
            int rangeLength = 0;
            foreach (Range range in ranges)
            {
                rangeLength += (range.endPos - range.startPos + 1);
            }
            return rangeLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="range1"></param>
        /// <param name="range2"></param>
        /// <returns></returns>
        private int GetRangeOverlap(Range range1, Range range2)
        {
            int maxStart = Math.Max(range1.startPos, range2.startPos);
            int minEnd = Math.Min(range1.endPos, range2.endPos);
            int overlap = minEnd - maxStart + 1;
            if (overlap < 0)
            {
                overlap = 0;
            }
            return overlap;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetSpeciesNameInfo(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Species, Name From AsymUnit WHERE PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable speciesNameTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] speciesName = new string[2];
            if (speciesNameTable.Rows.Count > 0)
            {
                speciesName[0] = speciesNameTable.Rows[0]["Species"].ToString().TrimEnd();
                speciesName[1] = speciesNameTable.Rows[0]["Name"].ToString().TrimEnd();
            }
            else
            {
                speciesName[0] = "-";
                speciesName[1] = "-";
            }
            return speciesName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainIds"></param>
        /// <param name="domainSpeciesUnpInfoTable"></param>
        /// <returns></returns>
        private string[] GetDomainInterfaceSpeciesNameUnpCode(string pdbId, long[] domainIds, DataTable domainSpeciesUnpInfoTable)
        {
            string species = "";
            string name = "";
            string unpCode = "";
            DataRow[] domainRows = domainSpeciesUnpInfoTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainIds[0]));
            if (domainRows.Length > 0)
            {
                species = domainRows[0]["Species"].ToString().TrimEnd();
                name = domainRows[0]["Name"].ToString().TrimEnd();
                unpCode = domainRows[0]["UnpCode"].ToString().TrimEnd();
            }
            if (domainIds[1] != domainIds[0])
            {
                domainRows = domainSpeciesUnpInfoTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainIds[1]));
                if (domainRows.Length > 0)
                {
                    species = species + ";" + domainRows[0]["Species"].ToString().TrimEnd();
                    name = name + ";" + domainRows[0]["Name"].ToString().TrimEnd();
                    unpCode = unpCode + ";" + domainRows[0]["UnpCode"].ToString().TrimEnd();
                }
            }
            if (species == "" || species == ";")
            {
                species = "-";
            }
            if (name == "" || species == ";")
            {
                name = "-";
            }
            if (unpCode == "" || unpCode == ";")
            {
                unpCode = "-";
            }
            string[] spNameUnp = new string[3];
            spNameUnp[0] = species.Replace("\'", "\'\'");  // Skipped single Quot 
            spNameUnp[1] = name.Replace("\'", "\'\'");
            spNameUnp[2] = unpCode;
            return spNameUnp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] IsDomainInterfaceInBU(string pdbId, int domainInterfaceId, string buType)
        {
            DataTable crystBuCompTable = GetCrystBuDomainInterfaceCompTable(pdbId, domainInterfaceId, buType);
            double qScore = 0.0;
            List<string> buList = new List<string>();
            string buId = "";
            foreach (DataRow compRow in crystBuCompTable.Rows)
            {
                qScore = Convert.ToDouble(compRow["QScore"].ToString());
                if (qScore >= simInterfaceQcutoff)
                {
                    buId = compRow["BuID"].ToString();
                    if (!buList.Contains(buId))
                    {
                        buList.Add(buId);
                    }
                }
            }

            return buList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private DataTable GetCrystBuDomainInterfaceCompTable(string pdbId, int domainInterfaceId, string buType)
        {
            string queryString = string.Format("Select * From Cryst" + buType + "BuDomainInterfaceComp " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1} ORDER BY BuID;", pdbId, domainInterfaceId);
            DataTable crystBuCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return crystBuCompTable;
        }
        #endregion

        #region pfam-peptide clusters
        public void PrintPfamPeptideSumInfo ()
        {
            DbConnect dbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=C:\\Firebird\\Pfam30_Update\\ProtCID.fdb");
            string pepSumInfoFile = Path.Combine(biogenDataDir, "pfamPepClusterSum.txt");
            StreamWriter pepClusterSumWriter = new StreamWriter(pepSumInfoFile);
            pepClusterSumWriter.WriteLine("#PFAM_ID\n" + 
                            "#ClusterID \n" + 
                            "#NumEntries: the number of PDB entries in a cluster\n" + 
                            "#NumPfams_peptide: the number of Pfams in peptides identified by PDBfam\n" + 
                            "#NumUniProt_Prot: the number of uniprot codes (sequences) of proteins\n" + 
                            "#NumUniProt_peptide: the number of uniprot codes (sequences) of peptides. Some peptides don't have uniprot codes.\n" + 
                            "#SurfaceArea\n" + 
                            "#MinSeqIdentity: the minimum sequence identity\n" + 
                            "#ClusterInterface: the representative pfam-peptide interface\n" + 
                            "#NumSeqs_peptide: the number of unique peptide sequences. This is based on the sequences.\n");
            pepClusterSumWriter.WriteLine ();
            pepClusterSumWriter.WriteLine ("PFAM_ID\tClusterID\tNumEntries\tNumPfams_peptide\tNumUniProt_prot\t" +
                                    "NumUniProt_peptide\tNumSeqs_peptide\tSurfaceArea\tMinSeqIdentity\tClusterInterface\n");
            string queryString = "Select * From PfamPepClusterSumInfo;";
            DataTable pepClusterSumTable = dbQuery.Query(dbConnect, queryString);
            foreach (DataRow pepClusterRow in pepClusterSumTable.Rows)
            {
                pepClusterSumWriter.WriteLine (ParseHelper.FormatDataRow (pepClusterRow));
            }
            pepClusterSumWriter.Close ();
            dbConnect.DisconnectFromDatabase();
        }
        #endregion

        #region pfam-pfam relations
        /// <summary>
        /// 
        /// </summary>
        public void WritePfamPfamInteractionsFromProtCid()
        {
            string queryString = "Select RelSeqId, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable pfamRelationTable = ProtCidSettings.protcidQuery.Query( queryString);

            string pfamStructRelFile = Path.Combine(biogenDataDir, "PfamStructRelationsInPdb.txt");
            StreamWriter pfamStructRelWriter = new StreamWriter(pfamStructRelFile);
            string headerLine = "Pfam-Pfam\tClusterID\t#CfCluster\t#EntryCluster\t#Cf/Relation\t#Entry/Relation\tInPdb\tInPISA\t" +
                            "SurfaceArea\t#EntryIntra\t#EntryHetero\t#EntryHomo\tMinSeqIdentity";
            pfamStructRelWriter.WriteLine(headerLine);

            string pfam1 = "";
            string pfam2 = "";
            int relSeqId = 0;
            string[] clusterInfoLines = null;
            string relInterfaceInfoLine = ""; // for no clusters
            List<int> relSeqIdListNoCluster = new List<int> ();
            List<string> relationListNoCluster = new List<string>();
            foreach (DataRow relRow in pfamRelationTable.Rows)
            {
                pfam1 = relRow["FamilyCode1"].ToString().TrimEnd();
                pfam2 = relRow["FamilyCode2"].ToString().TrimEnd();
                // skip pfam-peptide info
                if (pfam1 == "peptide" || pfam2 == "peptide")
                {
                    continue;
                }
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                clusterInfoLines = GetPfamRelationClusterInfo(relSeqId);
                if (clusterInfoLines.Length == 0)
                {
                    relSeqIdListNoCluster.Add(relSeqId);
                    relationListNoCluster.Add(pfam1 + ":" + pfam2);
                    continue;
                }
                foreach (string clusterInfoLine in clusterInfoLines)
                {
                    pfamStructRelWriter.WriteLine(pfam1 + ":" + pfam2 + "\t" + clusterInfoLine);
                }
            }
            pfamStructRelWriter.WriteLine("\n");
            pfamStructRelWriter.WriteLine("Pfam-Pfam with no clusters");
            int i = 0;
            foreach (int noClusterRelSeqId in relSeqIdListNoCluster)
            {
                relInterfaceInfoLine = GetPfamRelationInfoWithNoCluster(noClusterRelSeqId);
                if (relInterfaceInfoLine != "")
                {
                    pfamStructRelWriter.WriteLine(relationListNoCluster[i] + "\t" + relInterfaceInfoLine);
                }
                i++;
            }
            pfamStructRelWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private string[] GetPfamRelationClusterInfo(int relSeqId)
        {
            string queryString = string.Format("Select ClusterID, NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, InPdb, InPISA, " +
                " SurfaceArea, NumOfEntryIntra, NumOfEntryHetero, NumOfEntryHomo, MinSeqIdentity From PfamDomainClusterSumInfo Where RelSeqId = {0};", relSeqId);
            DataTable pfamRelClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> pfamRelClusterLineList = new List<string>();
            string clusterLine = "";
            foreach (DataRow clusterRow in pfamRelClusterTable.Rows)
            {
                clusterLine = "";
                foreach (object item in clusterRow.ItemArray)
                {
                    clusterLine += (item.ToString() + "\t");
                }
                pfamRelClusterLineList.Add(clusterLine.TrimEnd('\t'));
            }
            return pfamRelClusterLineList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetPfamRelationInfoWithNoCluster(int relSeqId)
        {
            int numOfEntries = 0;
            int numOfEntriesInPdb = 0;
            int numOfEntriesInPisa = 0;
            string queryString = string.Format("Select PdbId, DomainInterfaceID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            List<string> entryInPdbList = new List<string>();
            List<string> entryInPisaList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
                if (IsDomainInterfaceInBA(pdbId, domainInterfaceId, "pdb"))
                {
                    if (!entryInPdbList.Contains(pdbId))
                    {
                        entryInPdbList.Add(pdbId);
                    }
                }
                if (IsDomainInterfaceInBA(pdbId, domainInterfaceId, "pisa"))
                {
                    if (!entryInPisaList.Contains(pdbId))
                    {
                        entryInPisaList.Add(pdbId);
                    }
                }
            }
            numOfEntries = entryList.Count;
            numOfEntriesInPdb = entryInPdbList.Count;
            numOfEntriesInPisa = entryInPisaList.Count;
            if (numOfEntries > 0)
            {
                return (numOfEntries.ToString() + "\t" + numOfEntriesInPdb + "\t" + numOfEntriesInPisa.ToString());
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceInBA(string pdbId, int domainInterfaceId, string buType)
        {
            string queryString = string.Format("Select QScore From Cryst{0}BuDomainInterfaceComp Where PdbID = '{1}' AND DomainInterfaceID = {2};", buType, pdbId, domainInterfaceId);
            DataTable interfaceInBuTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceInBuTable.Rows.Count > 0)
            {
                double qscore = Convert.ToDouble(interfaceInBuTable.Rows[0]["QScore"].ToString());
                if (qscore >= simQscoreCutoff)
                {
                    return true;
                }
            }
            return false;
        }      
        #endregion

        #region chain interfaces
        double inBAQcutoff = 0.45;
        string[] chainColumnsNeeded = { "PdbID", "InterfaceID", "InPdbBA",  "InPisa", "ASA", "Species", "UnpCode", 
                                          "GroupName", "ClusterID", "#CFs/Cluster", "#CFs/Group", "#Entry/Cluster", "#Entry/Group", "MinSeqIdentity"};
        public void OutputChainInterfacesAndClusterInfo ()
        {
            string chainInterfaceFile = Path.Combine(biogenDataDir, "KrishnaBiogen_ChainInterfaces_v30_April242017.txt");
            StreamWriter dataWriter = new StreamWriter(chainInterfaceFile, true);
            string headerLine = "";
            foreach (string col in chainColumnsNeeded)
            {
                headerLine += (col + "\t");
            }
            dataWriter.WriteLine(headerLine.TrimEnd('\t'));

            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = "Select PdbID, InterfaceID, EntityID1, EntityID2, SurfaceArea From CrystEntryInterfaces";
            DataTable crystInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = "Select SuperGroupSeqID, ClusterID, PdbID, InterfaceID, InterfaceUnit, InPDB, InPISA, Name, Species, UnpCode From PfamSuperClusterEntryInterfaces;";
            DataTable interfaceClusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = "Select SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups;";
            DataTable chainGroupNameTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = "Select SuperGroupSeqID, ClusterID, NumOfCfgCluster, NumOfCfgFamily, NumOfEntryCluster, NumOfEntryFamily, MinSeqIdentity From PfamSuperClusterSumInfo;";
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);

            
            string dataLine = "";
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                try
                {
                    dataLine = GetEntryInterfacesAndClustersInfo(pdbId, crystInterfaceTable, interfaceClusterTable, chainGroupNameTable, clusterSumInfoTable);
                    if (dataLine == "")
                    {
                        continue;
                    }
                    dataWriter.Write(dataLine);
                    dataWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Chain Interface clusters info for Biogen :" + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Chain Interface clusters info for Biogen :" + ex.Message);
                    dataWriter.Flush();
                }
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Biogen Chain file done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceTable"></param>
        /// <param name="chainClusterTable"></param>
        /// <returns></returns>
        public string GetEntryInterfacesAndClustersInfo (string pdbId, DataTable crystInterfaceTable, DataTable interfaceClusterTable, 
            DataTable groupNameTable, DataTable clusterSumInfoTable)
        {
            string queryString = string.Format ("Select * From CrystPdbBuInterfaceComp Where PdbID = '{0}';", pdbId);
            DataTable pdbInterfaceCompTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
            queryString = string.Format("Select * From CrystPisaBuInterfaceComp Where PdbID = '{0}';", pdbId);
            DataTable pisaInterfaceCompTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
            queryString = string.Format("Select PdbID, EntityID, Species From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable speciesTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            queryString = string.Format("Select PdbID, EntityID, DBCode From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP'", pdbId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string entryInterfaceClusterLines = "";
            string entryInterfaceClusterLine = "";
            DataRow[] entryInterfaceRows = crystInterfaceTable.Select(string.Format ("PdbID= '{0}'", pdbId));
            if (entryInterfaceRows.Length > 3000)  // skip virus structures, not useful
            {
                return "";
            }
            string interfaceId = "";
            int superGroupId = 0;
            int clusterId = 0;
            string groupName = "";
            bool isInPdbBa = true;
            bool isInPisaBa = true;
            string species = "";
            string species2 = "";
            string unpCode = "";
            string unpCode2 = "";
            foreach (DataRow interfaceRow in entryInterfaceRows)
            {                 
                interfaceId = interfaceRow["InterfaceID"].ToString();
                isInPdbBa = IsInterfaceInBA (pdbId, interfaceId, pdbInterfaceCompTable);
                isInPisaBa = IsInterfaceInBA(pdbId, interfaceId, pisaInterfaceCompTable);

                entryInterfaceClusterLine = pdbId + "\t" + interfaceId + "\t";
                if (isInPdbBa)
                {
                    entryInterfaceClusterLine += "1\t";
                }
                else
                {
                    entryInterfaceClusterLine += "0\t";
                }
                if (isInPisaBa)
                {
                    entryInterfaceClusterLine += "1\t";
                }
                else
                {
                    entryInterfaceClusterLine += "0\t";
                }
                entryInterfaceClusterLine += (interfaceRow["SurfaceArea"].ToString() + "\t");
                species = GetEntitySpecies(pdbId, interfaceRow["EntityID1"].ToString(), speciesTable);
                unpCode = GetEntityUnpCode(pdbId, interfaceRow["EntityID1"].ToString(), unpCodeTable);
                if (interfaceRow["EntityID1"].ToString () != interfaceRow["EntityID2"].ToString ())
                {
                    species2 = GetEntitySpecies(pdbId, interfaceRow["EntityID2"].ToString(), speciesTable);
                    if (species != species2 )
                    {
                        species += (";" + species2);
                    }
                    unpCode2 = GetEntityUnpCode(pdbId, interfaceRow["EntityID2"].ToString(), unpCodeTable);
                    if (unpCode != unpCode2 )
                    {
                        unpCode += (";" + unpCode2);
                    }
                }
                entryInterfaceClusterLine += (species + "\t" + unpCode + "\t");


                DataRow interfaceClusterInfoRow = GetInterfaceClusterInfo(pdbId, interfaceId, interfaceClusterTable);
                groupName = "";
                if (interfaceClusterInfoRow != null)
                {
                    superGroupId = Convert.ToInt32(interfaceClusterInfoRow["SuperGroupSeqID"].ToString ());
                    clusterId = Convert.ToInt32 (interfaceClusterInfoRow["ClusterID"].ToString ());
                    DataRow clusterSumRow = GetChainClusterSumInfoRow (superGroupId, clusterId, clusterSumInfoTable);
                    groupName = GetGroupString(superGroupId, groupNameTable);
                    entryInterfaceClusterLine += groupName + "\t" + clusterSumRow["NumOfCfgCluster"].ToString() + "\t" +
                        clusterSumRow["NumOfCfgFamily"].ToString() + "\t" + clusterSumRow["NumOfEntryCluster"].ToString() + "\t" +
                        clusterSumRow["NUmOfEntryFamily"].ToString() + "\t" + clusterSumRow["MinSeqIdentity"].ToString();
                }
                entryInterfaceClusterLines += (entryInterfaceClusterLine + "\n");
            }

            return entryInterfaceClusterLines;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainClusterTable"></param>
        /// <returns></returns>
        private DataRow GetInterfaceClusterInfo(string pdbId, string interfaceId, DataTable interfaceClusterTable)
        {
            DataRow[] interfaceClusterInfoRows = interfaceClusterTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            if (interfaceClusterInfoRows.Length > 0)
            {
                return interfaceClusterInfoRows[0];
            }
            return null;
        }

        private DataRow GetChainClusterSumInfoRow (int superGroupId, int clusterId, DataTable clusterSumInfoTable)
        {
            DataRow[] clusterRows = clusterSumInfoTable.Select(string.Format("SuperGroupSeqID = '{0}' AND ClusterID = '{1}'",
                    superGroupId, clusterId));
            if (clusterRows.Length > 0)
            {
                return clusterRows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="groupStringTable"></param>
        /// <returns></returns>
        private string GetGroupString (int superGroupId, DataTable groupStringTable)
        {
            DataRow[] groupRows = groupStringTable.Select(string.Format (" SuperGroupSeqID = '{0}'", superGroupId));
            if (groupRows.Length > 0)
            {
                return groupRows[0]["ChainRelPfamArch"].ToString ().TrimEnd ();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="buInterfaceCompTable"></param>
        /// <returns></returns>
        private bool IsInterfaceInBA (string pdbId, string interfaceId, DataTable buInterfaceCompTable)
        {
            DataRow[] compRows = buInterfaceCompTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            if (compRows.Length > 0)
            {
                double qscore = Convert.ToDouble(compRows[0]["Qscore"].ToString());
                if (qscore > inBAQcutoff)
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
        /// <param name="entityId"></param>
        /// <param name="unpTable"></param>
        /// <returns></returns>
        private string GetEntityUnpCode (string pdbId, string entityId, DataTable unpTable)
        {
            DataRow[] unpRows = unpTable.Select (string.Format ("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
            if (unpRows.Length > 0)
            {
                return unpRows[0]["DbCode"].ToString().TrimEnd();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="speciesTable"></param>
        /// <returns></returns>
        private string GetEntitySpecies (string pdbId, string entityId, DataTable speciesTable)
        {
            DataRow[] speciesRows = speciesTable.Select (string.Format ("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
            if (speciesRows.Length > 0)
            {
                return speciesRows[0]["Species"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion

        #region human pfam assignments
        /// <summary>
        /// 
        /// </summary>
        public void PrintHumanPfamAssignments()
        {

            string queryString = "Select unpAccession, Isoform, UnpCode, Pfam_acc, Pfam_id, SeqStart, SeqEnd,  HmmStart, HmmEnd, Evalue From HumanPfam Where Pfam_id Not Like 'Pfam-B%' Order By UnpAccession, isoform;";
            DataTable humanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            string humanPfamFile = Path.Combine(biogenDataDir, "HumanPfamAssignments.txt");
            StreamWriter humanPfamWriter = new StreamWriter(humanPfamFile);
            string headerLine = "";
            foreach (DataColumn dCol in humanPfamTable.Columns)
            {
                if (dCol.ColumnName == "Isoform")
                {
                    continue;
                }
                headerLine += (dCol.ColumnName + "\t");
            }
            humanPfamWriter.WriteLine(headerLine.TrimEnd('\t'));

            string dataLine = "";
            int isoform = 0;
            foreach (DataRow dataRow in humanPfamTable.Rows)
            {
                isoform = Convert.ToInt32(dataRow["Isoform"].ToString());
                if (isoform == 0)
                {
                    dataLine = dataRow["unpAccession"].ToString().TrimEnd();
                }
                else
                {
                    dataLine = dataRow["unpAccession"].ToString().TrimEnd() + "-" + isoform.ToString();
                }
                dataLine += ("\t" + dataRow["UnpCode"].ToString() + "\t" + dataRow["Pfam_acc"].ToString() + "\t" +
                        dataRow["Pfam_id"].ToString() + "\t" + dataRow["SeqStart"].ToString() + "\t" +
                        dataRow["SeqEnd"].ToString() + "\t" + dataRow["HmmStart"].ToString() + "\t" +
                        dataRow["HmmEnd"].ToString() + "\t" + dataRow["Evalue"].ToString());

                humanPfamWriter.WriteLine(dataLine);
            }
            humanPfamWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetHumanDisorderPredictions()
        {
            string queryString = "Select UnpAccession, Isoform, UnpCode, Sequence, Disorder_VSL, Disorder_IUPRED, Disorder_ESPRITZ, Disorder_Cons From HumanSeqInfo Order By UnpAccession, isoform;";
            DataTable humanDisPredTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string humanDisPredFile = Path.Combine(biogenDataDir, "HumanDisorderPred.txt");
            StreamWriter dispredWriter = new StreamWriter(humanDisPredFile);
            string headerLine = "#File Format\n";
            headerLine += "#>Uniprot accession code -isoform Uniprot code\n";
            headerLine += "#Sequence\n";
            headerLine += "#VSL disorder prediction\n";
            headerLine += "#IUPRED disorder prediction\n";
            headerLine += "#ESPRITZ disorder prediction\n";
            headerLine += "#Consensus disorder prediction\n";
            dispredWriter.WriteLine(headerLine);

            string unpAccIsoform = "";
            int isoform = 0;
            foreach (DataRow dispredRow in humanDisPredTable.Rows)
            {
                isoform = Convert.ToInt32(dispredRow["Isoform"].ToString());
                if (isoform == 0)
                {
                    unpAccIsoform = dispredRow["UnpAccession"].ToString().TrimEnd();
                }
                else
                {
                    unpAccIsoform = dispredRow["UnpAccession"].ToString().TrimEnd() + "-" + isoform.ToString();
                }
                dispredWriter.WriteLine(">" + unpAccIsoform + " " + dispredRow["UnpCode"].ToString());
                dispredWriter.WriteLine(dispredRow["Sequence"]);
                dispredWriter.WriteLine(dispredRow["Disorder_VSL"].ToString());
                dispredWriter.WriteLine(dispredRow["Disorder_IUPRED"].ToString());
                dispredWriter.WriteLine(dispredRow["Disorder_ESPRITZ"].ToString());
                dispredWriter.WriteLine(dispredRow["Disorder_Cons"].ToString());
            }
            dispredWriter.Close();
        }
        #endregion

        #region compare Pfam assignments between FCCC and Pfam itself
        private string pfamAssignDir = @"D:\Pfam\ProtPfam\humanPfam";
        private string dbPfamAssignTableName = "";
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqType"></param>
        public void CompareOurPfamAssignmentsToPfam(string seqType)
        {
            repeatPfams = GetRepeatPfams();
            dbPfamAssignTableName = seqType + "Pfam";
            string pfamAssignFile = Path.Combine(pfamAssignDir, seqType + "_9606.tsv");
            DataTable pfamAssignTable = ReadProteomePfamAssignmentsFromPfamFile(pfamAssignFile);
            List<string> unpAccListFromPfam = GetUniqueSequences(pfamAssignTable);
            List<string> pfamIdListFromPfam = GetUniquePfamIds(pfamAssignTable);

            List<string> commonUnpAccList = new List<string>();
            List<string> dbPfamIdList = new List<string>();
            List<string> pfamPfamIdList = new List<string>();
            int[] dbResidueNumbers = new int[7];
            int[] pfamResidueNumbers = new int[7];
            int numOfRepeatsDb = 0;
            int numOfRepeatsPfam = 0;
            int numOfSplitDomains = 0;
            foreach (string unpAcc in unpAccListFromPfam)
            {
                DataTable dbPfamAssignTable = GetOurPfamAssignments(unpAcc);
                if (dbPfamAssignTable.Rows.Count == 0)
                {
                    continue;
                }
                commonUnpAccList.Add(unpAcc);
                DataRow[] pfamAssignRows = pfamAssignTable.Select(string.Format("UnpAccession = '{0}'", unpAcc), "AlignStart ASC");

                int[] unpDbResidueNumbers = GetPfamsOfUnpSeq(dbPfamAssignTable, dbPfamIdList);
                dbResidueNumbers[0] += unpDbResidueNumbers[0];
                dbResidueNumbers[1] += unpDbResidueNumbers[1];
                dbResidueNumbers[2] += unpDbResidueNumbers[2];
                dbResidueNumbers[3] += unpDbResidueNumbers[3];
                dbResidueNumbers[4] += unpDbResidueNumbers[4];
                dbResidueNumbers[5] += unpDbResidueNumbers[5];
                dbResidueNumbers[6] += unpDbResidueNumbers[6];
                numOfRepeatsDb += GetNumOfRepeats(dbPfamAssignTable.Select());
                numOfSplitDomains += GetNumOfSplits(dbPfamAssignTable.Select());

                int[] unpPfamResidueNumbers = GetPfamsOfUnpSeq(pfamAssignRows, pfamPfamIdList);
                pfamResidueNumbers[0] += unpPfamResidueNumbers[0];
                pfamResidueNumbers[1] += unpPfamResidueNumbers[1];
                pfamResidueNumbers[2] += unpPfamResidueNumbers[2];
                pfamResidueNumbers[3] += unpPfamResidueNumbers[3];
                pfamResidueNumbers[4] += unpPfamResidueNumbers[4];
                pfamResidueNumbers[5] += unpPfamResidueNumbers[5];
                pfamResidueNumbers[6] += unpPfamResidueNumbers[6];
                numOfRepeatsPfam += GetNumOfRepeats(pfamAssignRows);
            }

            string pfamAssignCompFile = Path.Combine(pfamAssignDir, seqType + "_pfamAssignComp_Pfamv30.txt");
            StreamWriter dataWriter = new StreamWriter(pfamAssignCompFile);
            dataWriter.WriteLine("Data from protein Pfam assignments from Pfam");
            dataWriter.WriteLine("#Sequences = " + unpAccListFromPfam.Count.ToString());
            dataWriter.WriteLine("#Pfams = " + pfamIdListFromPfam.Count.ToString());
            dataWriter.WriteLine();
            int[] totalNumbers = GetTotalDbPfamAssignments();
            dataWriter.WriteLine("Data from protein Pfam assignments from FCCC");
            dataWriter.WriteLine("#Sequences = " + totalNumbers[0]);
            dataWriter.WriteLine("#Pfams = " + totalNumbers[1]);
            dataWriter.WriteLine();
            dataWriter.WriteLine("#Sequence common = " + commonUnpAccList.Count.ToString());
            dataWriter.WriteLine("#Pfams from Pfam for common sequences = " + pfamPfamIdList.Count.ToString());
            dataWriter.WriteLine("#Pfams from FCCC for common sequences = " + dbPfamIdList.Count.ToString());
            dataWriter.WriteLine();
            dataWriter.WriteLine("#HMM residues (Pfam) = " + pfamResidueNumbers[0]);
            dataWriter.WriteLine("#Seq residues (Pfam) = " + pfamResidueNumbers[1]);
            dataWriter.WriteLine("#Aligned Seq residues (Pfam) = " + pfamResidueNumbers[2]);
            dataWriter.WriteLine("#Unique Seq residues (Pfam) = " + pfamResidueNumbers[3]);
            dataWriter.WriteLine("#Aligned Unique Seq residues (Pfam) = " + pfamResidueNumbers[4]);
            dataWriter.WriteLine("#Overlap Seq residues (Pfam) = " + pfamResidueNumbers[5]);
            dataWriter.WriteLine("#Overlap Aligned Seq residues (Pfam) = " + pfamResidueNumbers[6]);
            dataWriter.WriteLine("#Repeats (Pfam) = " + numOfRepeatsPfam);
            dataWriter.WriteLine();
            dataWriter.WriteLine("#HMM residues (FCCC) = " + dbResidueNumbers[0]);
            dataWriter.WriteLine("#Seq residues (FCCC) = " + dbResidueNumbers[1]);
            dataWriter.WriteLine("#Aligned Seq residues (FCCC) = " + dbResidueNumbers[2]);
            dataWriter.WriteLine("#Unique Seq residues (FCCC) = " + dbResidueNumbers[3]);
            dataWriter.WriteLine("#Aligned Unique Seq residues (FCCC) = " + dbResidueNumbers[4]);
            dataWriter.WriteLine("#Overlap Seq residues (FCCC) = " + dbResidueNumbers[5]);
            dataWriter.WriteLine("#Overlap Aligned Seq residues (FCCC) = " + dbResidueNumbers[6]);
            dataWriter.WriteLine("#Repeats (FCCC) = " + numOfRepeatsDb);
            dataWriter.WriteLine("#Splits (FCCC) = " + numOfSplitDomains);

            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetTotalDbPfamAssignments()
        {
            int[] statNumbers = new int[2];
            string queryString = "Select Distinct UnpAccession From " + dbPfamAssignTableName + ";";
            DataTable protSeqTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            statNumbers[0] = protSeqTable.Rows.Count;
            queryString = "Select Distinct Pfam_ID From " + dbPfamAssignTableName + ";";
            DataTable pfamIdTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            statNumbers[1] = pfamIdTable.Rows.Count;
            return statNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignTable"></param>
        /// <param name="unpAcc"></param>
        /// <returns></returns>
        private DataTable GetSubDataTable(DataTable pfamAssignTable, string unpAcc)
        {
            DataRow[] unpPfamAssignRows = pfamAssignTable.Select(string.Format("UnpAccession = '{0}'", unpAcc));
            DataTable subPfamAssignTable = pfamAssignTable.Clone();
            foreach (DataRow pfamAssignRow in unpPfamAssignRows)
            {
                DataRow newRow = subPfamAssignTable.NewRow();
                newRow.ItemArray = pfamAssignRow.ItemArray;
                subPfamAssignTable.Rows.Add(newRow);
            }
            return subPfamAssignTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpAcc"></param>
        /// <returns></returns>
        private DataTable GetOurPfamAssignments(string unpAcc)
        {
            string queryString = string.Format("Select UnpAccession, DomainID, Pfam_Acc,  Pfam_ID, AlignStart, AlignEnd, SeqStart, SeqEnd, HmmStart, HmmEnd, Evalue, BitScore, DomainType " +
                " From {0} Where UnpAccession = '{1}' AND Isoform = 0 Order By AlignStart ASC;", dbPfamAssignTableName, unpAcc);
            DataTable unpPfamDbAssignTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            return unpPfamDbAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpAcc"></param>
        /// <returns></returns>
        private DataTable GetOurSplitPfamAssignments(string unpAcc)
        {
            string queryString = string.Format("Select UnpAccession, DomainID, Pfam_Acc,  Pfam_ID, AlignStart, AlignEnd, SeqStart, SeqEnd, HmmStart, HmmEnd, Evalue, BitScore, DomainType " +
                " From {0} Where UnpAccession = '{1}' AND Isoform = 0 AND DomainType IN ('c', 's') Order By AlignStart ASC;", dbPfamAssignTableName, unpAcc);
            DataTable unpPfamDbAssignTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            return unpPfamDbAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignTable"></param>
        /// <param name="pfamIdList"></param>
        /// <returns></returns>
        private int[] GetPfamsOfUnpSeq(DataTable pfamAssignTable, List<string> pfamIdList)
        {
            DataRow[] pfamAssignRows = pfamAssignTable.Select();
            int[] residuesNumbers = GetPfamsOfUnpSeq(pfamAssignRows, pfamIdList);

            return residuesNumbers;
        }

        private int GetNumOfRepeats(DataRow[] pfamAssignRows, List<string> repeatPfamList)
        {
            repeatPfamList.Clear();
            int numOfRepeats = 0;
            string pfamId = "";
            foreach (DataRow assignRow in pfamAssignRows)
            {
                pfamId = assignRow["Pfam_ID"].ToString().TrimEnd();
                if (Array.BinarySearch(repeatPfams, pfamId) > -1)
                {
                    numOfRepeats++;

                    if (!repeatPfamList.Contains(pfamId))
                    {
                        repeatPfamList.Add(pfamId);
                    }
                }
            }
            return numOfRepeats;
        }

        private int GetNumOfRepeats(DataRow[] pfamAssignRows)
        {
            int numOfRepeats = 0;
            string pfamId = "";
            foreach (DataRow assignRow in pfamAssignRows)
            {
                pfamId = assignRow["Pfam_ID"].ToString().TrimEnd();
                if (Array.BinarySearch(repeatPfams, pfamId) > -1)
                {
                    numOfRepeats++;
                }
            }
            return numOfRepeats;
        }

        private int GetNumOfSplits(DataRow[] pfamAssignRows)
        {
            List<long> splitDomainList = new List<long>();
            long domainId = 0;
            string domainType = "";
            foreach (DataRow assignRow in pfamAssignRows)
            {
                domainType = assignRow["DomainType"].ToString().TrimEnd();
                if (domainType == "s" || domainType == "c")
                {
                    domainId = Convert.ToInt64(assignRow["DomainID"].ToString());
                    if (!splitDomainList.Contains(domainId))
                    {
                        splitDomainList.Add(domainId);
                    }
                }
            }
            return splitDomainList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignTable"></param>
        /// <param name="pfamIdList"></param>
        /// <returns></returns>
        private int[] GetPfamsOfUnpSeq(DataRow[] pfamAssignRows, List<string> pfamIdList)
        {
            string pfamId = "";
            int numOfHmmResidues = 0;
            int numOfSeqResidues = 0;
            int numOfAlignSeqResidues = 0;
            int seqStart = 0;
            int seqEnd = 0;
            int alignSeqStart = 0;
            int alignSeqEnd = 0;
            List<int> uniqueSeqResidueList = new List<int>();
            List<int> uniqueAlignSeqResidueList = new List<int>();
            List<int> overlapResidueList = new List<int>();
            int numOfOverlapResidues = 0;
            int numOfOverlapAlignResidues = 0;
            foreach (DataRow assignRow in pfamAssignRows)
            {
                pfamId = assignRow["Pfam_ID"].ToString().TrimEnd();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
                numOfHmmResidues += (Convert.ToInt32(assignRow["HmmEnd"].ToString()) - Convert.ToInt32(assignRow["HmmStart"].ToString()) + 1);
                seqEnd = Convert.ToInt32(assignRow["SeqEnd"].ToString());
                seqStart = Convert.ToInt32(assignRow["SeqStart"].ToString());
                numOfSeqResidues += (seqEnd - seqStart + 1);
                for (int i = seqStart; i <= seqEnd; i++)
                {
                    if (!uniqueSeqResidueList.Contains(i))
                    {
                        uniqueSeqResidueList.Add(i);
                    }
                    else
                    {
                        numOfOverlapResidues++;
                    }
                }
                alignSeqEnd = Convert.ToInt32(assignRow["AlignEnd"].ToString());
                alignSeqStart = Convert.ToInt32(assignRow["AlignStart"].ToString());
                numOfAlignSeqResidues += (alignSeqEnd - alignSeqStart + 1);
                for (int i = alignSeqStart; i <= alignSeqEnd; i++)
                {
                    if (!uniqueAlignSeqResidueList.Contains(i))
                    {
                        uniqueAlignSeqResidueList.Add(i);
                    }
                    else
                    {
                        numOfOverlapAlignResidues++;
                    }
                }
            }

            int[] residuesNumbers = new int[7];
            residuesNumbers[0] = numOfHmmResidues;
            residuesNumbers[1] = numOfSeqResidues;
            residuesNumbers[2] = numOfAlignSeqResidues;
            residuesNumbers[3] = uniqueSeqResidueList.Count;
            residuesNumbers[4] = uniqueAlignSeqResidueList.Count;
            residuesNumbers[5] = numOfOverlapResidues;
            residuesNumbers[6] = numOfOverlapAlignResidues;
            return residuesNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignTable"></param>
        /// <returns></returns>
        private List<string> GetUniqueSequences(DataTable pfamAssignTable)
        {
            List<string> unpAccList = new List<string>();
            string unpAcc = "";
            foreach (DataRow assignRow in pfamAssignTable.Rows)
            {
                unpAcc = assignRow["UnpAccession"].ToString();
                if (!unpAccList.Contains(unpAcc))
                {
                    unpAccList.Add(unpAcc);
                }
            }
            return unpAccList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignTable"></param>
        /// <returns></returns>
        private List<string> GetUniquePfamIds(DataTable pfamAssignTable)
        {
            List<string> pfamIdList = new List<string>();
            string pfamId = "";
            foreach (DataRow assignRow in pfamAssignTable.Rows)
            {
                pfamId = assignRow["Pfam_ID"].ToString();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
            }
            return pfamIdList;
        }

        /// <summary>
        /// read pfam assignments by parsing a text file downloaded from pfam ftp site
        /// </summary>
        /// <param name="pfamAssignFile"></param>
        /// <returns></returns>
        public DataTable ReadProteomePfamAssignmentsFromPfamFile(string pfamAssignFile)
        {
            StreamReader dataReader = new StreamReader(pfamAssignFile);
            string line = "";
            line = dataReader.ReadLine();
            line = dataReader.ReadLine();
            string headerLine = dataReader.ReadLine(); // table columns
            DataTable pfamAssignTable = InitializePfamAssignTable();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                DataRow assignRow = pfamAssignTable.NewRow();
                assignRow.ItemArray = fields;
                pfamAssignTable.Rows.Add(assignRow);
            }
            dataReader.Close();
            return pfamAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        private DataTable InitializePfamAssignTable(string headerLine)
        {
            DataTable pfamAssignTable = new DataTable("PfamAssign");
            headerLine = headerLine.TrimStart('#');
            string[] headerFields = headerLine.Split('>');
            string col = "";
            foreach (string field in headerFields)
            {
                col = field.TrimStart("< ".ToCharArray());
                if (col == "")
                {
                    continue;
                }
                col = col.Replace(" ", "_");
                pfamAssignTable.Columns.Add(new DataColumn(col));
            }
            return pfamAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        private DataTable InitializePfamAssignTable()
        {
            DataTable pfamAssignTable = new DataTable("PfamAssign");
            string[] pfamColumns = { "UnpAccession", "AlignStart", "AlignEnd", "SeqStart", "SeqEnd", "Pfam_Acc", "Pfam_ID", "Type", "HmmStart", "HmmEnd", "HmmLength", "BitScore", "Evalue", "Clan" };
            foreach (string pfamCol in pfamColumns)
            {
                pfamAssignTable.Columns.Add(new DataColumn(pfamCol));
            }
            return pfamAssignTable;
        }
        #endregion

        #region structure examples of Pfam assignments
        #region splits
        public void GetSplitDomainsStructures()
        {
            dbPfamAssignTableName = "HumanPfam";
            string pfamAssignFile = Path.Combine(pfamAssignDir, "human_9606.tsv");
            DataTable pfamAssignTable = ReadProteomePfamAssignmentsFromPfamFile(pfamAssignFile);
            List<string> unpAccListFromPfam = GetUniqueSequences(pfamAssignTable);
            List<string> pfamIdListFromPfam = GetUniquePfamIds(pfamAssignTable);

            string queryString = string.Format("Select Distinct UnpAccession, UnpCode From HumanPfam Where Isoform = 0 AND DomainType in ('c', 's');");
            DataTable splitsUnpTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string unpAcc = "";

            string pfamAssignCompFile = Path.Combine(pfamAssignDir, "human_pfamAssignComp_splits_StructOnly.txt");
            StreamWriter dataWriter = new StreamWriter(pfamAssignCompFile);
            dataWriter.WriteLine("Unp\tUnpCode\t#Splits(FCCC)\t#Splits(Pfam)\tPfams(FCCC)\tPfams(Pfam)\tStrucuturesAndSplits");

            List<string> dbSplitPfamIdList = new List<string>();
            List<string> pfamSplitPfamIdList = new List<string>();

            string dataLine = "";
            foreach (DataRow unpRow in splitsUnpTable.Rows)
            {
                unpAcc = unpRow["UnpAccession"].ToString().TrimEnd();
                DataTable dbPfamAssignTable = GetOurSplitPfamAssignments(unpAcc);
                if (dbPfamAssignTable.Rows.Count == 0)
                {
                    continue;
                }
                if (unpRow["UnpCode"].ToString().TrimEnd() == "")
                {
                    continue;
                }
                Dictionary<long, List<string>> domainRangeDict = GetDomainRangesDict(dbPfamAssignTable.Select(), dbSplitPfamIdList);

                DataRow[] pfamAssignRows = pfamAssignTable.Select(string.Format("UnpAccession = '{0}'", unpAcc), "AlignStart ASC");
                Dictionary<long, List<DataRow>> pfamDomainRangeDict = GetPossiblePfamSplitDomains(pfamAssignRows, pfamSplitPfamIdList);

                DataTable unpPdbMapTable = GetUnpPdbStructuresTable(unpAcc);

                Dictionary<string, List<string>> structSplitsDict = GetOverlapStructuralSplits(dbPfamAssignTable, domainRangeDict, unpPdbMapTable);
                if (structSplitsDict.Count > 0)
                {
                    dataLine = "FCCC\n" + ParseHelper.FormatDataRows(dbPfamAssignTable.Select()) + "\n";
                    dataLine += "Pfam\n";
                    foreach (long pfamDomainId in pfamDomainRangeDict.Keys)
                    {
                        dataLine += (ParseHelper.FormatDataRows(pfamDomainRangeDict[pfamDomainId].ToArray()) + "\n");
                    }
                    dataLine += "PDB\n";
                    foreach (string pdbId in structSplitsDict.Keys)
                    {
                        dataLine += (pdbId + "\t" + ParseHelper.FormatStringFieldsToString(structSplitsDict[pdbId].ToArray()) + "\n");
                    }
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        private Dictionary<long, List<string>> GetDomainRangesDict(DataRow[] pfamAssignRows, List<string> pfamIdList)
        {
            pfamIdList.Clear();
            Dictionary<long, List<string>> domainRangesDict = new Dictionary<long, List<string>>();
            long domainId = 0;
            string range = "";
            string pfamId = "";
            foreach (DataRow pfamRow in pfamAssignRows)
            {
                domainId = Convert.ToInt64(pfamRow["DomainID"].ToString());
                range = pfamRow["SeqStart"].ToString() + "-" + pfamRow["SeqEnd"].ToString();
                if (domainRangesDict.ContainsKey(domainId))
                {
                    domainRangesDict[domainId].Add(range);
                }
                else
                {
                    List<string> rangeList = new List<string>();
                    rangeList.Add(range);
                    domainRangesDict.Add(domainId, rangeList);
                }
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                if (!pfamIdList.Contains(pfamId))
                {
                    pfamIdList.Add(pfamId);
                }
            }
            return domainRangesDict;
        }

        private Dictionary<long, List<DataRow>> GetPossiblePfamSplitDomains(DataRow[] pfamAssignRows, List<string> pfamIdList)
        {
            pfamIdList.Clear();
            long domainNo = 1;
            Dictionary<string, List<DataRow>> pfamRowsDict = new Dictionary<string, List<DataRow>>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamAssignRows)
            {
                pfamId = pfamRow["Pfam_ID"].ToString().TrimEnd();
                if (pfamRowsDict.ContainsKey(pfamId))
                {
                    pfamRowsDict[pfamId].Add(pfamRow);
                }
                else
                {
                    List<DataRow> rowList = new List<DataRow>();
                    rowList.Add(pfamRow);
                    pfamRowsDict.Add(pfamId, rowList);
                }
            }
            Dictionary<long, List<DataRow>> domainRangesDict = new Dictionary<long, List<DataRow>>();
            foreach (string lsPfam in pfamRowsDict.Keys)
            {
                if (pfamRowsDict[lsPfam].Count > 1)
                {
                    Dictionary<long, List<DataRow>> combineDomainRangeDict = GetPfamSplitDomainsFromPfam(pfamRowsDict[lsPfam].ToArray());
                    foreach (long domainId in combineDomainRangeDict.Keys)
                    {
                        domainRangesDict.Add(domainNo, combineDomainRangeDict[domainId]);
                        domainNo++;
                    }
                }
            }
            return domainRangesDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignRows"></param>
        /// <returns></returns>
        private Dictionary<long, List<DataRow>> GetPfamSplitDomainsFromPfam(DataRow[] pfamAssignRows)
        {
            int startI = 0;
            int endI = 0;
            int startJ = 0;
            int endJ = 0;
            long combineDomainId = 1;
            List<int> combineIndexList = new List<int>();
            Dictionary<long, List<DataRow>> domainRangesDict = new Dictionary<long, List<DataRow>>();
            for (int i = 0; i < pfamAssignRows.Length; i++)
            {
                if (combineIndexList.Contains(i))
                {
                    continue;
                }
                List<DataRow> combineRowList = new List<DataRow>();
                startI = Convert.ToInt32(pfamAssignRows[i]["HmmStart"].ToString());
                endI = Convert.ToInt32(pfamAssignRows[i]["HmmEnd"].ToString());
                for (int j = i + 1; j < pfamAssignRows.Length; j++)
                {
                    if (combineIndexList.Contains(j))
                    {
                        continue;
                    }
                    startJ = Convert.ToInt32(pfamAssignRows[j]["HmmStart"].ToString());
                    endJ = Convert.ToInt32(pfamAssignRows[j]["HmmEnd"].ToString());
                    if (!IsOverlaped(startI, endI, startJ, endJ, 0.25))
                    {
                        if (!combineIndexList.Contains(i))
                        {
                            combineIndexList.Add(i);
                            combineRowList.Add(pfamAssignRows[i]);
                        }
                        if (!combineIndexList.Contains(j))
                        {
                            combineIndexList.Add(j);
                            combineRowList.Add(pfamAssignRows[j]);
                        }
                    }
                }
                if (combineRowList.Count > 1)
                {
                    /*         List<string> rangeList = new List<string>();
                             foreach (DataRow dataRow in combineRowList)
                             {
                                 rangeList.Add(dataRow["SeqStart"].ToString() + "-" + dataRow["SeqEnd"].ToString());
                             }*/
                    domainRangesDict.Add(combineDomainId, combineRowList);
                    combineDomainId++;
                }
            }
            return domainRangesDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbPfamAssignTable"></param>
        /// <param name="unpPdbMapTable"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetOverlapStructuralSplits(DataTable dbPfamAssignTable, Dictionary<long, List<string>> domainRangesDict, DataTable unpPdbMapTable)
        {
            Dictionary<string, List<string>> entryDomainRangesDict = new Dictionary<string, List<string>>();
            List<string> unpEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in unpPdbMapTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!unpEntryList.Contains(pdbId))
                {
                    unpEntryList.Add(pdbId);
                }
            }
            int entryDomainId = 1;
            foreach (long domainId in domainRangesDict.Keys)
            {
                if (domainRangesDict.Count > 1)
                {
                    foreach (string lsPdb in unpEntryList)
                    {
                        DataRow[] entryUnpMapRows = unpPdbMapTable.Select(string.Format("PDBID = '{0}'", lsPdb));
                        List<string> pdbRangeList = GetEntryRanges(domainRangesDict[domainId], entryUnpMapRows);
                        if (pdbRangeList.Count > 0)
                        {
                            entryDomainRangesDict.Add(lsPdb + "_" + entryDomainId.ToString(), pdbRangeList);
                        }
                    }
                }
                entryDomainId++;
            }
            return entryDomainRangesDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpRanges"></param>
        /// <param name="entryUnpMapRows"></param>
        /// <returns></returns>
        private List<string> GetEntryRanges(List<string> unpRanges, DataRow[] entryUnpMapRows)
        {
            int unpStart = 0;
            int unpEnd = 0;
            int dbStart = 0;
            int dbEnd = 0;
            int pdbStart = 0;
            int pdbEnd = 0;
            string pdbRange = "";
            List<string> pdbRangeList = new List<string>();
            foreach (string unpRange in unpRanges)
            {
                string[] fields = unpRange.Split('-');
                unpStart = Convert.ToInt32(fields[0]);
                unpEnd = Convert.ToInt32(fields[1]);
                foreach (DataRow mapRow in entryUnpMapRows)
                {
                    dbStart = Convert.ToInt32(mapRow["DbAlignBeg"].ToString());
                    dbEnd = Convert.ToInt32(mapRow["DbAlignEnd"].ToString());
                    pdbStart = Convert.ToInt32(mapRow["SeqAlignBeg"].ToString());
                    pdbEnd = Convert.ToInt32(mapRow["SeqAlignEnd"].ToString());
                    if (IsOverlaped(unpStart, unpEnd, dbStart, dbEnd, 0.8))
                    {
                        pdbRange = MapUnpRangeToPdbRange(unpStart, unpEnd, dbStart, dbEnd, pdbStart, pdbEnd);
                        if (pdbRange != "")
                        {
                            pdbRangeList.Add(pdbRange);
                        }
                    }
                }
            }
            return pdbRangeList;
        }

        private string MapUnpRangeToPdbRange(int unpStart, int unpEnd, int dbStart, int dbEnd, int pdbStart, int pdbEnd)
        {
            int rangePdbStart = 0;
            int rangePdbEnd = 0;
            if (unpStart >= dbStart && unpStart < dbEnd)
            {
                rangePdbStart = pdbStart + (unpStart - dbStart);
            }
            else if (unpStart < dbStart && unpEnd > dbStart)
            {
                rangePdbStart = pdbStart;
            }
            if (unpEnd < dbEnd && unpEnd > dbStart)
            {
                rangePdbEnd = pdbEnd - (dbEnd - unpEnd);
            }
            else if (unpEnd > dbEnd && unpStart < dbEnd)
            {
                rangePdbEnd = pdbEnd;
            }
            if (rangePdbStart > 0 && rangePdbEnd > 0)
            {
                return rangePdbStart.ToString() + "-" + rangePdbEnd.ToString();
            }
            return "";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqStart1"></param>
        /// <param name="seqEnd1"></param>
        /// <param name="seqStart2"></param>
        /// <param name="seqEnd2"></param>
        /// <returns></returns>
        private bool IsOverlaped(double seqStart1, double seqEnd1, double seqStart2, double seqEnd2)
        {
            double overlap = Math.Min(seqEnd1, seqEnd2) - Math.Max(seqStart1, seqStart2);
            double coverage1 = overlap / (seqEnd1 - seqStart1);
            double coverage2 = overlap / (seqEnd2 - seqStart2);
            if (coverage1 >= 0.80 || coverage2 >= 0.80)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqStart1"></param>
        /// <param name="seqEnd1"></param>
        /// <param name="seqStart2"></param>
        /// <param name="seqEnd2"></param>
        /// <returns></returns>
        private bool IsOverlaped(int seqStart1, int seqEnd1, int seqStart2, int seqEnd2, double coverageCutoff)
        {
            double overlap = (double)Math.Min(seqEnd1, seqEnd2) - (double)Math.Max(seqStart1, seqStart2);
            double coverage1 = overlap / (double)(seqEnd1 - seqStart1);
            double coverage2 = overlap / (double)(seqEnd2 - seqStart2);
            if (coverage1 >= coverageCutoff || coverage2 >= coverageCutoff)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region repeats
        public void CompareRepeatDomains()
        {
            repeatPfams = GetRepeatPfams();
            dbPfamAssignTableName = "HumanPfam";
            string pfamAssignFile = Path.Combine(pfamAssignDir, "human_9606.tsv");
            DataTable pfamAssignTable = ReadProteomePfamAssignmentsFromPfamFile(pfamAssignFile);
            List<string> unpAccListFromPfam = GetUniqueSequences(pfamAssignTable);
            List<string> pfamIdListFromPfam = GetUniquePfamIds(pfamAssignTable);

            string queryString = string.Format("Select Distinct UnpAccession, UnpCode From HumanPfam, PfamHmm Where HumanPfam.Pfam_ID = PfamHmm.Pfam_ID AND Type = 'Repeat' AND Isoform = 0;");
            DataTable repeatUnpTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string unpAcc = "";

            string pfamAssignCompFile = Path.Combine(pfamAssignDir, "human_pfamAssignComp_repeats.txt");
            StreamWriter dataWriter = new StreamWriter(pfamAssignCompFile);
            dataWriter.WriteLine("Unp\tUnpCode\t#Repeats(FCCC)\t#Repeats(Pfam)\tPfams(FCCC)\tPfams(Pfam)\tStrucuturesAndRepeats");

            List<string> dbRepeatPfamIdList = new List<string>();
            List<string> pfamRepeatPfamIdList = new List<string>();
            int numOfRepeatsDb = 0;
            int numOfRepeatsPfam = 0;
            int numOfSplitDomains = 0;
            string dataLine = "";
            foreach (DataRow unpRow in repeatUnpTable.Rows)
            {
                unpAcc = unpRow["UnpAccession"].ToString().TrimEnd();
                DataTable dbPfamAssignTable = GetOurPfamAssignments(unpAcc);
                if (dbPfamAssignTable.Rows.Count == 0)
                {
                    continue;
                }
                if (unpRow["UnpCode"].ToString().TrimEnd() == "")
                {
                    continue;
                }
                DataRow[] pfamAssignRows = pfamAssignTable.Select(string.Format("UnpAccession = '{0}'", unpAcc), "AlignStart ASC");
                numOfRepeatsDb = GetNumOfRepeats(dbPfamAssignTable.Select(), dbRepeatPfamIdList);
                numOfSplitDomains = GetNumOfSplits(dbPfamAssignTable.Select());

                numOfRepeatsPfam = GetNumOfRepeats(pfamAssignRows, pfamRepeatPfamIdList);

                if (numOfRepeatsDb == 0 && numOfRepeatsPfam == 0)
                {
                    continue;
                }
                dataLine = unpAcc + "\t" + unpRow["UnpCode"].ToString().TrimEnd() + "\t" + numOfRepeatsDb + "\t" + numOfRepeatsPfam + "\t" +
                    ParseHelper.FormatStringFieldsToString(dbRepeatPfamIdList.ToArray()) + "\t" +
                    ParseHelper.FormatStringFieldsToString(pfamRepeatPfamIdList.ToArray()) + "\t";


                queryString = string.Format("Select Distinct UnpAccession, HumanPfam.Pfam_ID, DomainID, SeqStart, SeqEnd, HmmStart, HmmEnd From HumanPfam, PfamHmm " +
               " Where unpAccession = '{0}' AND Isoform = 0 AND HumanPfam.Pfam_ID = PfamHmm.Pfam_ID AND Type = 'Repeat';", unpAcc);
                DataTable repeatTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

                DataTable unpPdbMapTable = GetUnpPdbStructuresTable(unpAcc);

                Dictionary<string, int> structRepeatsDict = GetOverlapStructuralRepeats(repeatTable, unpPdbMapTable);
                foreach (string pdbId in structRepeatsDict.Keys)
                {
                    dataLine += (pdbId + "(" + structRepeatsDict[pdbId] + ");");
                }
                dataWriter.WriteLine(dataLine.TrimEnd(';'));
            }
            dataWriter.Close();
        }

        private Dictionary<string, int> GetOverlapStructuralRepeats(DataTable unpRepeatTable, DataTable unpPdbMapTable)
        {
            Dictionary<string, int> pdbStructRepeatsDict = new Dictionary<string, int>();
            string pdbId = "";
            foreach (DataRow repeatRow in unpRepeatTable.Rows)
            {
                DataRow[] pdbStructRows = GetMatchedPdbStructs(repeatRow, unpPdbMapTable);
                foreach (DataRow pdbRow in pdbStructRows)
                {
                    pdbId = pdbRow["PdbID"].ToString();
                    if (pdbStructRepeatsDict.ContainsKey(pdbId))
                    {
                        pdbStructRepeatsDict[pdbId]++;
                    }
                    else
                    {
                        pdbStructRepeatsDict.Add(pdbId, 1);
                    }
                }
            }
            return pdbStructRepeatsDict;
        }

        public void GetRepeatStructurs()
        {
            string humanRepeatStructFile = Path.Combine(pfamAssignDir, "HumanRepeatPdbStructures.txt");
            StreamWriter dataWriter = new StreamWriter(humanRepeatStructFile);
            string queryString = string.Format("Select Distinct UnpAccession From HumanPfam, PfamHmm Where HumanPfam.Pfam_ID = PfamHmm.Pfam_ID AND Type = 'Repeat' AND Isoform = 0;");
            DataTable repeatUnpTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string unpAcc = "";
            foreach (DataRow unpRow in repeatUnpTable.Rows)
            {
                unpAcc = unpRow["UnpAccession"].ToString().TrimEnd();

                queryString = string.Format("Select Distinct UnpAccession, HumanPfam.Pfam_ID, DomainID, SeqStart, SeqEnd, HmmStart, HmmEnd From HumanPfam, PfamHmm " +
                " Where unpAccession = '{0}' AND Isoform = 0 AND HumanPfam.Pfam_ID = PfamHmm.Pfam_ID AND Type = 'Repeat';", unpAcc);
                DataTable repeatTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

                DataTable unpPdbMapTable = GetUnpPdbStructuresTable(unpAcc);

                foreach (DataRow repeatRow in repeatTable.Rows)
                {
                    DataRow[] pdbStructRows = GetMatchedPdbStructs(repeatRow, unpPdbMapTable);
                    dataWriter.WriteLine(ParseHelper.FormatDataRow(repeatRow));
                    if (pdbStructRows.Length > 0)
                    {
                        dataWriter.WriteLine(ParseHelper.FormatDataRows(pdbStructRows));
                        dataWriter.WriteLine();
                    }
                }
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCode"></param>
        /// <returns></returns>
        private DataTable GetUnpPdbStructuresTable(string unpAccession)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbID, EntityID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd From PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where DbAccession = '{0}' AND PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID AND PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID;", unpAccession);
            DataTable pdbUnpSeqTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            return pdbUnpSeqTable;
        }

        private DataRow[] GetMatchedPdbStructs(DataRow repeatDomainRow, DataTable pdbUnpSeqTable)
        {
            int domainSeqStart = Convert.ToInt32(repeatDomainRow["SeqStart"].ToString());
            int domainSeqEnd = Convert.ToInt32(repeatDomainRow["SeqEnd"].ToString());
            int dbSeqStart = 0;
            int dbSeqEnd = 0;
            List<DataRow> matchedDataRowList = new List<DataRow>();
            foreach (DataRow pdbRow in pdbUnpSeqTable.Rows)
            {
                dbSeqStart = Convert.ToInt32(pdbRow["DbAlignBeg"].ToString());
                dbSeqEnd = Convert.ToInt32(pdbRow["DbAlignEnd"].ToString());
                if (IsOverlaped(domainSeqStart, domainSeqEnd, dbSeqStart, dbSeqEnd))
                {
                    matchedDataRowList.Add(pdbRow);
                }
            }
            return matchedDataRowList.ToArray();
        }
        #endregion
        #endregion
    }
}
