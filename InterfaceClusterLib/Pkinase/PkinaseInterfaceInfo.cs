using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Data;
using System.Linq;
using System.Text;
using System.Net;
using ProtCidSettingsLib;
using DbLib;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;
using PfamLib.PfamArch;
using PfamLib.Settings;
using BuCompLib.BuInterfaces;
using InterfaceClusterLib.InterfaceProcess;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.Pkinase
{
    public class PkinaseInterfaceInfo
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private InterfaceReader interfaceReader = new InterfaceReader();
        private InterfaceWriter interfaceWriter = new InterfaceWriter();
        private CrystalInterfaceLib.ProtInterfaces.InterfaceFileWriter interfaceFileWriter =
            new CrystalInterfaceLib.ProtInterfaces.InterfaceFileWriter();
        private string pfamInterfaceFileDir = "";
        private string dataDir = @"D:\Qifang\ProjectData\Pkinase";
        private CrystInterfaceProcessor interfaceFileGen = new CrystInterfaceProcessor();
        private PfamArchitecture pfamArch = new PfamArchitecture();
        private AsuInterfaces asuInterfaces = new AsuInterfaces();
        private string homoDimerInterfaceFileDir = "";
        private string heteroDimerInterfaceFileDir = "";
        private string[] pfamIds = null;
        private string dataType = "";
        private WebClient webClient = new WebClient();
        private string localXmlDir = @"D:\Qifang\ProjectData\Pkinase\pdb";
        #endregion

        #region Pkinase
        /// <summary>
        /// generate 
        /// </summary>
        public void GeneratePkinaseInterfaceFilesInUnpSeq()
        {
            pfamIds = new string[2];
            pfamIds[0] = "Pkinase";
            pfamIds[1] = "Pkinase_Tyr";

            dataType = "Pkinase";

            Initialize(false);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pkinase entries");
            string[] pkinaseEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds); // GetPdbEntriesWithPkinaseDomains();

            /*       ProtCidSettings.progressInfo.progStrQueue.Enqueue("Pkinase entries with interface files generated");
                   string[] noInterfaceFileEntries = GetEntriesWithoutInterfaceFiles(pkinaseEntries);
       */
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate pkinase interface files.");
            ProtCidSettings.progressInfo.totalStepNum = pkinaseEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pkinaseEntries.Length;

            foreach (string pdbId in pkinaseEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                interfaceFileGen.GenerateEntryInterfaceFiles(pdbId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate interface files done!");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate entry interface files uniprot seq ids.");
            UpdatePkinaseEntryInterfaceFilesInUnpSeqIds(pkinaseEntries);

            PrintEntryPkinaseInterfaceInfoInUnpSeq(pkinaseEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void GenerateInterfaceFilesInXmlSeq()
        {
            pfamIds = new string[2];
            pfamIds[0] = "Pkinase";
            pfamIds[1] = "Pkinase_Tyr";

            dataType = "Pkinase";

            bool isUpdate = true;
            Initialize(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pkinase entries");
            //    string[] pkinaseEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds); 
            string listFile = @"D:\Qifang\ProjectData\Pkinase\uniprotall.dat";
            Hashtable[] entityInfoHashes = ReadKinaseEntityInfoHash(listFile);
            ArrayList kinaseEntryList = new ArrayList(entityInfoHashes[0].Keys);
            string[] pkinaseEntries = new string[kinaseEntryList.Count];
            kinaseEntryList.CopyTo(pkinaseEntries);

     /*       string homoDimerListFile = Path.Combine(dataDir, "pkinaseHomodimerList.txt");
            string heteroDimerListFile = Path.Combine(dataDir, "pkinaseHeteroDimerList.txt");
            CopyEntryInterfaceFiles(pkinaseEntries, homoDimerListFile, heteroDimerListFile, entityInfoHashes[0]);
            */
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Pkinase entries with interface files generated");
       /*     string[] noInterfaceFileEntries = GetEntriesWithoutInterfaceFiles(pkinaseEntries);
*/
                   string noInterfaceLsFile = @"D:\Qifang\ProjectData\Pkinase\newlist.txt";
    //               DonwloadNewKinaseEntries(noInterfaceLsFile);
                   StreamReader entryWriter = new StreamReader(noInterfaceLsFile);
                   ArrayList noInterfaceFileEntryList = new ArrayList();
                   string line = "";
                   while ((line = entryWriter.ReadLine()) != null)
                   {
                       noInterfaceFileEntryList.Add(line);
                   }
                   string[] noInterfaceFileEntries = new string[noInterfaceFileEntryList.Count];
                   noInterfaceFileEntryList.CopyTo(noInterfaceFileEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate pkinase interface files.");
            ProtCidSettings.progressInfo.totalStepNum = pkinaseEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pkinaseEntries.Length;

            foreach (string pdbId in noInterfaceFileEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                ArrayList pkinaseEntityList = (ArrayList)entityInfoHashes[0][pdbId];
                int[] pkinaseEntities = new int[pkinaseEntityList.Count];
                pkinaseEntityList.CopyTo(pkinaseEntities);

                interfaceFileGen.GenerateCrystAndAsuInterfaceFiles(pdbId, pkinaseEntities, true, pfamInterfaceFileDir);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate interface files done!");

       //           PrintEntryPkinaseInterfaceInfo(pkinaseEntries);

            string updatehomoDimerListFile = Path.Combine(dataDir, "pkinaseHomodimerList_update.txt");
            string updateheteroDimerListFile = Path.Combine(dataDir, "pkinaseHeteroDimerList_update.txt");
            CopyEntryInterfaceFiles(noInterfaceFileEntries, updatehomoDimerListFile, updateheteroDimerListFile, entityInfoHashes[0]);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void ReadKinaseKinds()
        {
            StreamReader dataReader = new StreamReader(@"C:\Paper\Kinase\dataset\kinaselist.txt");
            Hashtable uniprotEntryHash = new Hashtable();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split();
                if (uniprotEntryHash.Contains(fields[0]))
                {
                    ArrayList entryList = (ArrayList)uniprotEntryHash[fields[0]];
                    if (!entryList.Contains(fields[1]))
                    {
                        entryList.Add(fields[1]);
                    }
                }
                else
                {
                    ArrayList entryList = new ArrayList();
                    entryList.Add(fields[1]);
                    uniprotEntryHash.Add(fields[0], entryList);
                }
            }
            dataReader.Close();
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Kinase\dataset\kinaselistSumInfo.txt");
            foreach (string unpCode in uniprotEntryHash.Keys)
            {
                dataWriter.WriteLine(unpCode + "\t" + ((ArrayList)uniprotEntryHash[unpCode]).Count.ToString());
            }
            dataWriter.Close();
        }

        /// <summary>
        /// uniprotall.dat
        /// A0A0H3JME9_STAAN  4EQMA  ALK K   39   LRL M   73   RRE E   58   
        /// HRD D  133   DFG F  152   APE E  179   DLW D  191   ISR R  247  
        /// seq M    1 V  291  kin V   13 H  272  coor M    1 E  282  missing    22    13
        /// </summary>
        /// <returns></returns>
        private Hashtable[] ReadKinaseEntityInfoHash(string listFile)
        {
            Hashtable entryEntityHash = new Hashtable();
            Hashtable entryEntityUnpHash = new Hashtable();

            StreamReader entryReader = new StreamReader(listFile);
            string line = "";
            string pdbId = "";
            string authChain = "";
            int entityId = 0;
            string unpCode = "";
            while ((line = entryReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                pdbId = fields[1].ToLower().Substring(0, 4);
                authChain = fields[1].Substring(4, fields[1].Length - 4);
                entityId = GetAuthChainEntityId(pdbId, authChain);
                if (entryEntityHash.Contains(pdbId))
                {
                    ArrayList entityIdList = (ArrayList)entryEntityHash[pdbId];
                    if (!entityIdList.Contains(entityId))
                    {
                        entityIdList.Add(entityId);
                    }
                }
                else
                {
                    ArrayList entityIdList = new ArrayList();
                    entityIdList.Add(entityId);
                    entryEntityHash.Add(pdbId, entityIdList);
                }
                if (fields.Length >= 2)
                {
                    unpCode = fields[0];
                }
                else
                {
                    string[] unpCodes = GetEntityUnpCodes(pdbId, entityId);
                    if (unpCodes.Length >= 1)
                    {
                        unpCode = unpCodes[0];
                    }
                    else
                    {
                        unpCode = "-";
                    }
                }
                if (!entryEntityUnpHash.Contains(pdbId + entityId.ToString()))
                {
                    entryEntityUnpHash.Add(pdbId + entityId.ToString(), unpCode);
                }
            }
            entryReader.Close();

            Hashtable[] entityInfoHashes = new Hashtable[2];
            entityInfoHashes[0] = entryEntityHash;
            entityInfoHashes[1] = entryEntityUnpHash;
            return entityInfoHashes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private int GetAuthChainEntityId(string pdbId, string authChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit WHere PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, authChain);
            DataTable entityIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int entityId = -1;
            if (entityIdTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityIdTable.Rows[0]["EntityID"].ToString());
            }
            return entityId;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void PrintEntryPkinaseInterfaceInfo(string[] pdbList)
        {
            StreamWriter logWriter = new StreamWriter("PkinaseInterfaceFileGenLog.txt");

            StreamWriter homoDimerWriter = new StreamWriter(@"C:\Qifang\Pkinase\pkinaseHomodimerList.txt");
            homoDimerWriter.WriteLine("UniProt\tPfamArch\tPDBID\tEntities\tAuthorChains\tInterfaceList");
            StreamWriter heteroDimerWriter = new StreamWriter(@"C:\Qifang\Pkinase\pkinaseHeteroDimerList.txt");
            heteroDimerWriter.WriteLine("UniProt\tPfamArch\tUniProt\tPfamArch\tPDBID\tKinase Sequence\tNonkinase Sequence\tNonkinase Length\tInterfaceList");
            Hashtable unpHomoDimerHash = new Hashtable();
            Hashtable unpHeteroDimerHash = new Hashtable();
            Hashtable interfacePfamArchHash = new Hashtable();
            Hashtable interfaceNonKinaseLengthHash = new Hashtable();
            Hashtable interfaceNonKinaseSequencesHash = new Hashtable();
            Hashtable interfaceKinaseSequencesHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files For Phosphorylation";
            ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;

            ArrayList interfaceIdList = new ArrayList();
            int entityId1 = -1;
            int entityId2 = -1;
            string interfaceDefQueryString = "";
            bool isHomoDimer = true;
            string unpCode = "";
            string heteroUnpCodes = "";
            string heteroPfamArchs = "";
            ArrayList entityWithInterfaceList = new ArrayList();
            foreach (string pdbId in pdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                unpHomoDimerHash.Clear();
                unpHeteroDimerHash.Clear();
                interfacePfamArchHash.Clear();
                interfaceNonKinaseLengthHash.Clear();
                interfaceNonKinaseSequencesHash.Clear();
                entityWithInterfaceList.Clear();

                interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceIdList.Clear();

                int[] pkinaseEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds); // GetEntitiesWithPkinaseDomains(pdbId);
                //       string[] pkinaseAuthorChains = GetEntityAuthorChains(pdbId, pkinaseEntities);

                int[] entryEntities = null;
                Hashtable entityUnpCodesHash = GetEntryEntityUnpCodeHash(pdbId, out entryEntities);
                Hashtable entityPfamArchHash = GetEntityPfamArchHash(pdbId, entryEntities);
                Hashtable entityLengthHash = GetEntityLengthHash(pdbId, entryEntities);
                Hashtable entitySequenceHash = GetEntitySequenceHash(pdbId, entryEntities);

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    isHomoDimer = false;
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString());

                    if (!entityWithInterfaceList.Contains(entityId1))
                    {
                        entityWithInterfaceList.Add(entityId1);
                    }
                    if (!entityWithInterfaceList.Contains(entityId2))
                    {
                        entityWithInterfaceList.Add(entityId2);
                    }

                    if (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                        Array.IndexOf(pkinaseEntities, entityId2) < 0)
                    {
                        continue;
                    }

                    if (entityId1 == entityId2)
                    {
                        isHomoDimer = true;
                    }

                    try
                    {
                        if (isHomoDimer)
                        {
                            if (entityUnpCodesHash.ContainsKey(entityId1))
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    unpCode = (string)entityUnpCodesHash[entityId1];
                                }
                                else
                                {
                                    unpCode = "pdb";
                                }
                                if (unpHomoDimerHash.ContainsKey(unpCode))
                                {
                                    ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[unpCode];
                                    homoDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                }
                                else
                                {
                                    ArrayList homoDimerList = new ArrayList();
                                    homoDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                    unpHomoDimerHash.Add(unpCode, homoDimerList);
                                }
                            }
                            if (entityPfamArchHash.ContainsKey(entityId1))
                            {
                                interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), entityPfamArchHash[entityId1]);
                            }
                            else
                            {
                                interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), "-");
                            }
                        }
                        else
                        {
                            heteroUnpCodes = "";
                            heteroPfamArchs = "";
                            if (Array.IndexOf(pkinaseEntities, entityId1) > -1)
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    heteroUnpCodes = (string)entityUnpCodesHash[entityId1];
                                }
                                else
                                {
                                    heteroUnpCodes = "pdb";
                                }
                                if (entityUnpCodesHash.ContainsKey(entityId2))
                                {
                                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[entityId2]);
                                }
                                else
                                {
                                    heteroUnpCodes += ";pdb";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId1))
                                {
                                    heteroPfamArchs = (string)entityPfamArchHash[entityId1];
                                }
                                else
                                {
                                    heteroPfamArchs = "-";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId2))
                                {
                                    heteroPfamArchs += (";" + (string)entityPfamArchHash[entityId2]);
                                }
                                else
                                {
                                    heteroPfamArchs += ";-";
                                }
                                interfaceNonKinaseLengthHash.Add(pdbId + "_" + interfaceId.ToString(),
                                       (int)entityLengthHash[entityId2]);
                                interfaceNonKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId2]);
                                interfaceKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId1]);
                            }
                            else
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId2))
                                {
                                    heteroUnpCodes = (string)entityUnpCodesHash[entityId2];
                                }
                                else
                                {
                                    heteroUnpCodes = "pdb";
                                }
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[entityId1]);
                                }
                                else
                                {
                                    heteroUnpCodes += ";pdb";
                                }

                                if (entityPfamArchHash.ContainsKey(entityId2))
                                {
                                    heteroPfamArchs = (string)entityPfamArchHash[entityId2];
                                }
                                else
                                {
                                    heteroPfamArchs = "-";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId1))
                                {
                                    heteroPfamArchs += (";" + (string)entityPfamArchHash[entityId1]);
                                }
                                else
                                {
                                    heteroPfamArchs += ";-";
                                }
                                interfaceNonKinaseLengthHash.Add(pdbId + "_" + interfaceId.ToString(),
                                                (int)entityLengthHash[entityId1]);
                                interfaceNonKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId1]);
                                interfaceKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                   (string[])entitySequenceHash[entityId2]);
                            }
                            if (unpHeteroDimerHash.ContainsKey(heteroUnpCodes))
                            {
                                ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroUnpCodes];
                                heteroDimerList.Add(pdbId + "_" + interfaceId.ToString());
                            }
                            else
                            {
                                ArrayList heteroDimerList = new ArrayList();
                                heteroDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                unpHeteroDimerHash.Add(heteroUnpCodes, heteroDimerList);
                            }

                            interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), heteroPfamArchs);
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                        logWriter.WriteLine(ex.Message);
                        logWriter.Flush();
                    }
                }
                string dataLine = "";
                if (unpHomoDimerHash.Count > 0)
                {
                    foreach (string homoDimerUnpCode in unpHomoDimerHash.Keys)
                    {
                        ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[homoDimerUnpCode];
                        int[] interfaceEntities = GetHomoDimerEntities(homoDimerList);
                        string[] interfaceAuthorChains = GetEntityAuthorChains(pdbId, interfaceEntities);

                        dataLine = homoDimerUnpCode + "\t" +
                            (string)interfacePfamArchHash[(string)homoDimerList[0]] + "\t" + pdbId + "\t";
                        foreach (int entityId in interfaceEntities)
                        {
                            dataLine += (entityId.ToString() + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        dataLine += "\t";
                        foreach (string authChain in interfaceAuthorChains)
                        {
                            dataLine += (authChain + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        dataLine += "\t";
                        foreach (string homoDimer in homoDimerList)
                        {
                            dataLine += (homoDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        homoDimerWriter.WriteLine(dataLine);
                    }
                }
                if (unpHeteroDimerHash.Count > 0)
                {
                    foreach (string heteroDimerUnpCode in unpHeteroDimerHash.Keys)
                    {
                        ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroDimerUnpCode];
                        string[] heteroDimerUnpCodeFields = heteroDimerUnpCode.Split(';');
                        string[] heteroPfamArchFields = ((string)interfacePfamArchHash[(string)heteroDimerList[0]]).Split(';');
                        string[] nonKinaseSequences = (string[])interfaceNonKinaseSequencesHash[(string)heteroDimerList[0]];
                        string[] kinaseSequences = (string[])interfaceKinaseSequencesHash[(string)heteroDimerList[0]];
                        dataLine = heteroDimerUnpCodeFields[0] + "\t" + heteroPfamArchFields[0] + "\t" +
                            heteroDimerUnpCodeFields[1] + "\t" + heteroPfamArchFields[1] + "\t" +
                            pdbId + "\t" +
                            kinaseSequences[1] + "\t" + nonKinaseSequences[1] + "\t" +
                            interfaceNonKinaseLengthHash[(string)heteroDimerList[0]].ToString() + "\t";
                        foreach (string heteroDimer in heteroDimerList)
                        {
                            dataLine += (heteroDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        heteroDimerWriter.WriteLine(dataLine);
                    }
                }
            }
            logWriter.Close();
            homoDimerWriter.Close();
            heteroDimerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void CopyEntryInterfaceFiles(string[] pdbList, string homoDimerListFile, string heteroDimerListFile, Hashtable entryEntityHash)
        {
            StreamWriter logWriter = new StreamWriter("PkinaseInterfaceFileGenLog.txt");

            StreamWriter homoDimerWriter = new StreamWriter(homoDimerListFile);
            homoDimerWriter.WriteLine("UniProt\tPfamArch\tPDBID\tEntities\tAuthorChains\tInterfaceList");
            StreamWriter heteroDimerWriter = new StreamWriter(heteroDimerListFile);
            heteroDimerWriter.WriteLine("UniProt\tPfamArch\tUniProt\tPfamArch\tPDBID\t" +
                "Nonkinase_Length\tKinase_Sequence\tNonkinase_Sequence\tInterfaceList");
            Hashtable unpHomoDimerHash = new Hashtable();
            Hashtable unpHeteroDimerHash = new Hashtable();
            Hashtable interfacePfamArchHash = new Hashtable();
            Hashtable interfaceNonKinaseLengthHash = new Hashtable();
            Hashtable interfaceNonKinaseSequencesHash = new Hashtable();
            Hashtable interfaceKinaseSequencesHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files For Phosphorylation";
            ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;

            ArrayList interfaceIdList = new ArrayList();
            int entityId1 = -1;
            int entityId2 = -1;
            string interfaceFileName = "";
            string interfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            string pkinaseInterfaceFileName = "";
            string[] interfaceFileInfoStrings = null;
            string interfaceDefQueryString = "";
            bool isHomoDimer = true;
            string interfaceName = "";
            ArrayList entityWithInterfaceList = new ArrayList();
            foreach (string pdbId in pdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                unpHomoDimerHash.Clear();
                unpHeteroDimerHash.Clear();
                interfacePfamArchHash.Clear();
                interfaceNonKinaseLengthHash.Clear();
                entityWithInterfaceList.Clear();
                interfaceNonKinaseSequencesHash.Clear();

                interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceIdList.Clear();

                //      int[] pkinaseEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds); 
                ArrayList entryEntityList = (ArrayList)entryEntityHash[pdbId];
                int[] pkinaseEntities = new int[entryEntityList.Count];
                entryEntityList.CopyTo(pkinaseEntities);

                int[] entryEntities = null;
                Hashtable entityUnpCodesHash = GetEntryEntityUnpCodeHash(pdbId, out entryEntities);
                Hashtable entityPfamArchHash = GetEntityPfamArchHash(pdbId, entryEntities);
                Hashtable entityLengthHash = GetEntityLengthHash(pdbId, entryEntities);
                Hashtable entitySequencesHash = GetEntitySequenceHash(pdbId, entryEntities);

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    isHomoDimer = false;
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString());

                    interfaceName = pdbId + "_" + interfaceId.ToString();

                    if (!entityWithInterfaceList.Contains(entityId1))
                    {
                        entityWithInterfaceList.Add(entityId1);
                    }
                    if (!entityWithInterfaceList.Contains(entityId2))
                    {
                        entityWithInterfaceList.Add(entityId2);
                    }

                    if (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                        Array.IndexOf(pkinaseEntities, entityId2) < 0)
                    {
                        continue;
                    }

                    if (entityId1 == entityId2)
                    {
                        isHomoDimer = true;
                    }
                  
                    interfaceFileName = Path.Combine(pfamInterfaceFileDir, interfaceName + ".cryst.gz");
                    if (! File.Exists (interfaceFileName ))
                    {
                        string hashDir = Path.Combine(interfaceFileDir, pdbId.Substring(1, 2));
                        string crystInterfaceFileName = Path.Combine(hashDir, interfaceName + ".cryst.gz");
                        File.Copy(crystInterfaceFileName, interfaceFileName);
                    }

                    if (File.Exists(interfaceFileName))
                    {
                        interfaceFileName = ParseHelper.UnZipFile(interfaceFileName, ProtCidSettings.tempDir);
                        try
                        {
                            InterfaceChains chainInterface = new InterfaceChains();
                            interfaceFileInfoStrings = interfaceReader.ReadInterfaceFromFile(interfaceFileName, ref chainInterface);

                            if (isHomoDimer)
                            {
                                pkinaseInterfaceFileName = PrintHomodimerAndInfo(interfaceName, pkinaseEntities, chainInterface,
                                       entityUnpCodesHash, entityPfamArchHash, ref unpHomoDimerHash, ref interfacePfamArchHash);
                            }
                            else
                            {
                                pkinaseInterfaceFileName = PrintHeteroDimerAndInfo(interfaceName, pkinaseEntities, chainInterface, interfaceFileInfoStrings,
                                    entityUnpCodesHash, entityPfamArchHash, entityLengthHash, entitySequencesHash, ref unpHeteroDimerHash,
                                    ref interfacePfamArchHash, ref interfaceNonKinaseLengthHash, ref interfaceNonKinaseSequencesHash,
                                    ref interfaceKinaseSequencesHash);
                            }
                            if (pkinaseInterfaceFileName.IndexOf(".gz") < 0)
                            {
                                ParseHelper.ZipPdbFile(pkinaseInterfaceFileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                            logWriter.WriteLine(ex.Message);
                            logWriter.Flush();
                        }
                        File.Delete(interfaceFileName);
                    }
                }
                string[] heteroAsuInterfaces = Directory.GetFiles(pfamInterfaceFileDir, pdbId + "_0*");
                foreach (string heteroAsuInterface in heteroAsuInterfaces)
                {
                    InterfaceChains chainInterface = new InterfaceChains();
                    interfaceFileName = ParseHelper.UnZipFile(heteroAsuInterface, ProtCidSettings.tempDir);
                    interfaceFileInfoStrings = interfaceReader.ReadInterfaceFromFile(interfaceFileName, ref chainInterface);

                    FileInfo fileInfo = new FileInfo(heteroAsuInterface);
                    interfaceName = fileInfo.Name.Replace(".cryst.gz", "");
                    pkinaseInterfaceFileName = PrintHeteroDimerAndInfo(interfaceName, pkinaseEntities, chainInterface, interfaceFileInfoStrings,
                                    entityUnpCodesHash, entityPfamArchHash, entityLengthHash, entitySequencesHash, ref unpHeteroDimerHash,
                                    ref interfacePfamArchHash, ref interfaceNonKinaseLengthHash, ref interfaceNonKinaseSequencesHash,
                                    ref interfaceKinaseSequencesHash);
                }
                string dataLine = "";
                if (unpHomoDimerHash.Count > 0)
                {
                    foreach (string homoDimerUnpCode in unpHomoDimerHash.Keys)
                    {
                        ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[homoDimerUnpCode];
                        dataLine = homoDimerUnpCode + "\t" +
                            (string)interfacePfamArchHash[(string)homoDimerList[0]] + "\t" + pdbId + "\t";
                        int[] interfaceEntities = GetHomoDimerEntities(homoDimerList);
                        string[] authorChains = GetEntityAuthorChains(pdbId, interfaceEntities);
                        foreach (int entityId in interfaceEntities)
                        {
                            dataLine += (entityId.ToString() + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        dataLine += "\t";
                        foreach (string authorChain in authorChains)
                        {
                            dataLine += (authorChain + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        dataLine += "\t";
                        foreach (string homoDimer in homoDimerList)
                        {
                            dataLine += (homoDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        homoDimerWriter.WriteLine(dataLine);
                    }
                }
                if (unpHeteroDimerHash.Count > 0)
                {
                    foreach (string heteroDimerUnpCode in unpHeteroDimerHash.Keys)
                    {
                        ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroDimerUnpCode];
                        string[] heteroDimerUnpCodeFields = heteroDimerUnpCode.Split(';');
                        string[] heteroPfamArchFields = ((string)interfacePfamArchHash[(string)heteroDimerList[0]]).Split(';');
                        string[] nonKinaseSequences = (string[])interfaceNonKinaseSequencesHash[(string)heteroDimerList[0]];
                        string[] kinaseSequences = (string[])interfaceKinaseSequencesHash[(string)heteroDimerList[0]];
                        dataLine = heteroDimerUnpCodeFields[0] + "\t" + heteroPfamArchFields[0] + "\t" +
                            heteroDimerUnpCodeFields[1] + "\t" + heteroPfamArchFields[1] + "\t" +
                            pdbId + "\t" +
                            interfaceNonKinaseLengthHash[(string)heteroDimerList[0]].ToString() + "\t" +
                            kinaseSequences[1] + "\t" + nonKinaseSequences[1] + "\t";
                        foreach (string heteroDimer in heteroDimerList)
                        {
                            dataLine += (heteroDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        heteroDimerWriter.WriteLine(dataLine);
                    }
                }
            }
            logWriter.Close();
            homoDimerWriter.Close();
            heteroDimerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceName"></param>
        /// <param name="pkinaseEntities"></param>
        /// <param name="entityUnpCodesHash"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <param name="unpHomoDimerHash"></param>
        /// <param name="interfacePfamArchHash"></param>
        /// <returns></returns>
        private string PrintHomodimerAndInfo(string interfaceName, int[] pkinaseEntities, InterfaceChains chainInterface,
                        Hashtable entityUnpCodesHash, Hashtable entityPfamArchHash,
            ref Hashtable unpHomoDimerHash, ref Hashtable interfacePfamArchHash)
        {
            string unpCode = "";
            string pkinaseInterfaceFileName = Path.Combine(homoDimerInterfaceFileDir, interfaceName + ".cryst.gz");
            string orgInterfaceFileName = Path.Combine(pfamInterfaceFileDir, interfaceName + ".cryst.gz");
            File.Copy(orgInterfaceFileName, pkinaseInterfaceFileName, true);

            if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
            {
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    unpCode = (string)entityUnpCodesHash[chainInterface.entityId1];
                }
                else
                {
                    unpCode = "pdb";
                }
                if (unpHomoDimerHash.ContainsKey(unpCode))
                {
                    ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[unpCode];
                    homoDimerList.Add(interfaceName);
                }
                else
                {
                    ArrayList homoDimerList = new ArrayList();
                    homoDimerList.Add(interfaceName);
                    unpHomoDimerHash.Add(unpCode, homoDimerList);
                }
            }
            if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
            {
                interfacePfamArchHash.Add(interfaceName, entityPfamArchHash[chainInterface.entityId1]);
            }
            else
            {
                interfacePfamArchHash.Add(interfaceName, "-");
            }
            return pkinaseInterfaceFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainInterface"></param>
        /// <param name="interfaceRemarkString"></param>
        /// <param name="entityUnpCodesHash"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <param name="entityLengthHash"></param>
        /// <param name="unpHeteroDimerHash"></param>
        /// <param name="interfacePfamArchHash"></param>
        /// <param name="interfaceNonKinaseLengthHash"></param>
        private string PrintHeteroDimerAndInfo(string interfaceName, int[] pkinaseEntities, InterfaceChains chainInterface,
            string[] interfaceFileInfoStrings, Hashtable entityUnpCodesHash, Hashtable entityPfamArchHash, Hashtable entityLengthHash,
            Hashtable entitySequencesHash, ref Hashtable unpHeteroDimerHash, ref Hashtable interfacePfamArchHash,
            ref Hashtable interfaceNonKinaseLengthHash, ref Hashtable interfaceNonKinaseSequencesHash,
            ref Hashtable interfaceKinaseSqeuencesHash)
        {
            string pkinaseInterfaceFileName = Path.Combine(heteroDimerInterfaceFileDir, interfaceName + ".cryst");
            string orgPkinaseInterfaceFileName = Path.Combine(pfamInterfaceFileDir, interfaceName + ".cryst.gz");
            string heteroUnpCodes = "";
            string heteroPfamArchs = "";
            if (Array.IndexOf(pkinaseEntities, chainInterface.entityId1) > -1)
            {
                File.Copy(orgPkinaseInterfaceFileName, pkinaseInterfaceFileName + ".gz", true);

                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroUnpCodes = (string)entityUnpCodesHash[chainInterface.entityId1];
                }
                else
                {
                    heteroUnpCodes = "pdb";
                }
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[chainInterface.entityId2]);
                }
                else
                {
                    heteroUnpCodes += ";pdb";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroPfamArchs = (string)entityPfamArchHash[chainInterface.entityId1];
                }
                else
                {
                    heteroPfamArchs = "-";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroPfamArchs += (";" + (string)entityPfamArchHash[chainInterface.entityId2]);
                }
                else
                {
                    heteroPfamArchs += ";-";
                }
                interfaceNonKinaseLengthHash.Add(interfaceName,
                       (int)entityLengthHash[chainInterface.entityId2]);
                interfaceNonKinaseSequencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId2]);
                interfaceKinaseSqeuencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId1]);
            }
            else
            {
                string[] reverseFileInfos = ReverseRemarkLigandStrings(interfaceFileInfoStrings);
                interfaceWriter.WriteInterfaceToFile(pkinaseInterfaceFileName, reverseFileInfos[0],
                                    chainInterface.chain2, chainInterface.chain1, reverseFileInfos[1]);
                ParseHelper.ZipPdbFile(pkinaseInterfaceFileName);

                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroUnpCodes = (string)entityUnpCodesHash[chainInterface.entityId2];
                }
                else
                {
                    heteroUnpCodes = "pdb";
                }
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[chainInterface.entityId1]);
                }
                else
                {
                    heteroUnpCodes += ";pdb";
                }

                if (entityPfamArchHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroPfamArchs = (string)entityPfamArchHash[chainInterface.entityId2];
                }
                else
                {
                    heteroPfamArchs = "-";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroPfamArchs += (";" + (string)entityPfamArchHash[chainInterface.entityId1]);
                }
                else
                {
                    heteroPfamArchs += ";-";
                }
                interfaceNonKinaseLengthHash.Add(interfaceName,
                                (int)entityLengthHash[chainInterface.entityId1]);
                interfaceNonKinaseSequencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId1]);
                interfaceKinaseSqeuencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId2]);
            }
            if (unpHeteroDimerHash.ContainsKey(heteroUnpCodes))
            {
                ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroUnpCodes];
                heteroDimerList.Add(interfaceName);
            }
            else
            {
                ArrayList heteroDimerList = new ArrayList();
                heteroDimerList.Add(interfaceName);
                unpHeteroDimerHash.Add(heteroUnpCodes, heteroDimerList);
            }
            interfacePfamArchHash.Add(interfaceName, heteroPfamArchs);
            return pkinaseInterfaceFileName + ".gz";
        }

        public void ZipHeteroDimerFiles()
        {
            string[] asuHeteroFiles = Directory.GetFiles(@"D:\Pkinase\interfaceFiles\heterodimers", "*.cryst");
            foreach (string asuHeteroFile in asuHeteroFiles)
            {
                ParseHelper.ZipPdbFile(asuHeteroFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFileInfoStrings"></param>
        /// <returns></returns>
        private string[] ReverseRemarkLigandStrings(string[] interfaceFileInfoStrings)
        {
            string[] reverseFileInfoStrings = new string[interfaceFileInfoStrings.Length];
            string reverseRemarkString = ReverseRemarkLigandInfoString(interfaceFileInfoStrings[0]);
            reverseFileInfoStrings[0] = reverseRemarkString;
            string reverseHeteroInfo = ReverseHeteroAtomLines(interfaceFileInfoStrings[1]);
            reverseFileInfoStrings[1] = reverseHeteroInfo;
            return reverseFileInfoStrings;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remarkString"></param>
        /// <returns></returns>
        private string ReverseRemarkLigandInfoString(string remarkString)
        {
            string[] remarkFields = remarkString.Split("\r\n".ToCharArray());
            string newRemarkString = "";
            int oldChainAIndex = -1;
            int oldChainBIndex = -1;
            int oldLigandAIndex = -1;
            int oldLigandBIndex = -1;
            bool ligandStart = false;
            for (int i = 0; i < remarkFields.Length; i++)
            {
                if (remarkFields[i].IndexOf("Ligand Info") > -1)
                {
                    ligandStart = true;
                    continue;
                }
                if (!ligandStart)
                {
                    if (remarkFields[i].IndexOf("Interface Chain A") > -1)
                    {
                        oldChainAIndex = i;
                    }
                    else if (remarkFields[i].IndexOf("Interface Chain B") > -1)
                    {
                        oldChainBIndex = i;
                    }
                }
                else
                {
                    if (remarkFields[i].IndexOf("Interface Chain A") > -1)
                    {
                        oldLigandAIndex = i;
                    }
                    else if (remarkFields[i].IndexOf("Interface Chain B") > -1)
                    {
                        oldLigandBIndex = i;
                    }
                }
            }
            string temp = remarkFields[oldChainAIndex];
            remarkFields[oldChainAIndex] = remarkFields[oldChainBIndex];
            remarkFields[oldChainBIndex] = temp;

            if (oldLigandAIndex > -1 && oldLigandBIndex > -1)
            {
                temp = remarkFields[oldLigandAIndex];
                remarkFields[oldLigandAIndex] = remarkFields[oldLigandBIndex];
                remarkFields[oldLigandBIndex] = temp;

                temp = remarkFields[oldLigandAIndex + 1]; // the name, sequence line for the ligand
                remarkFields[oldLigandAIndex + 1] = remarkFields[oldLigandBIndex + 1];
                remarkFields[oldLigandBIndex + 1] = temp;
            }

            foreach (string remarkField in remarkFields)
            {
                if (remarkField == "")
                {
                    continue;
                }
                if (remarkField.IndexOf("Interface Chain A") > -1)
                {
                    newRemarkString += (remarkField.Replace("Interface Chain A", "Interface Chain B") + "\r\n");
                }
                else if (remarkField.IndexOf("Interface Chain B") > -1)
                {
                    newRemarkString += (remarkField.Replace("Interface Chain B", "Interface Chain A") + "\r\n");
                }
                else
                {
                    newRemarkString += (remarkField + "\r\n");
                }
            }
            return newRemarkString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="heteroAtomLines"></param>
        /// <returns></returns>
        private string ReverseHeteroAtomLines(string heteroAtomLines)
        {
            string reverseChainBLines = "";
            string reverseChainALines = "";
            string[] heteroAtomLineFields = heteroAtomLines.Split("\r\n".ToCharArray());
            string chainId = "";
            string reverseHeteroLine = "";
            foreach (string heteroAtomLine in heteroAtomLineFields)
            {
                if (heteroAtomLine == "")
                {
                    continue;
                }
                chainId = heteroAtomLine.Substring(21, 1);
                if (chainId == "A")
                {
                    reverseHeteroLine = heteroAtomLine.Remove(21, 1);
                    reverseHeteroLine = reverseHeteroLine.Insert(21, "B");
                    reverseChainBLines += (reverseHeteroLine + "\r\n");
                }
                else if (chainId == "B")
                {
                    reverseHeteroLine = heteroAtomLine.Remove(21, 1);
                    reverseHeteroLine = reverseHeteroLine.Insert(21, "A");
                    reverseChainALines += (reverseHeteroLine + "\r\n");
                }
            }
            string reverseHeteroInfo = reverseChainALines + "\r\n" + reverseChainBLines;
            return reverseHeteroInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="homoDimerList"></param>
        /// <returns></returns>
        private int[] GetHomoDimerEntities(ArrayList homoDimerList)
        {
            ArrayList entityList = new ArrayList();
            string pdbId = "";
            int interfaceId = 0;
            foreach (string homoDimer in homoDimerList)
            {
                string[] dimerFields = homoDimer.Split('_');
                pdbId = dimerFields[0];
                interfaceId = Convert.ToInt32(dimerFields[1]);
                int[] interfaceEntities = GetInterfaceEntities(pdbId, interfaceId);
                foreach (int entityId in interfaceEntities)
                {
                    if (!entityList.Contains(entityId))
                    {
                        entityList.Add(entityId);
                    }
                }
            }
            int[] entities = new int[entityList.Count];
            entityList.CopyTo(entities);
            return entities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int[] GetInterfaceEntities(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable entityTable = ProtCidSettings.protcidQuery.Query( queryString);
            ArrayList entityList = new ArrayList();
            int entityId1 = Convert.ToInt32(entityTable.Rows[0]["EntityID1"].ToString());
            int entityId2 = Convert.ToInt32(entityTable.Rows[0]["EntityID2"].ToString());
            entityList.Add(entityId1);
            if (!entityList.Contains(entityId2))
            {
                entityList.Add(entityId2);
            }
            int[] entities = new int[entityList.Count];
            entityList.CopyTo(entities);
            return entities;
        }
        #endregion

        #region for update
        /// <summary>
        /// 
        /// </summary>
        public void UpdateInterfaceFilesInXmlSeq()
        {
            pfamIds = new string[2];
            pfamIds[0] = "Pkinase";
            pfamIds[1] = "Pkinase_Tyr";

            dataType = "Pkinase";
            bool isUpdate = true;
            Initialize(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Pkinase entries with interface files generated");
            string updateLsFile = @"E:\Qifang\DbProjectData\PDB\XML-noatom\newls-pdb.txt";
            string[] updatePfamEntries = GetUpdatePdbEntriesWithSpecificPfams(pfamIds, updateLsFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate pkinase interface files.");
            ProtCidSettings.progressInfo.totalStepNum = updatePfamEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updatePfamEntries.Length;

            foreach (string pdbId in updatePfamEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                int[] pkinaseEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds);

                interfaceFileGen.GenerateCrystAndAsuInterfaceFiles(pdbId, pkinaseEntities, true, pfamInterfaceFileDir);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate interface files done!");

            string homoDimerListFile = Path.Combine(dataDir, "pkinaseHomodimerList_update.txt");
            string heteroDimerListFile = Path.Combine(dataDir, "pkinaseHeteroDimerList_update.txt");
            //      CopyEntryInterfaceFiles(updatePfamEntries, homoDimerListFile, heteroDimerListFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <param name="updateLsFile"></param>
        /// <returns></returns>
        public string[] GetUpdatePdbEntriesWithSpecificPfams(string[] pfamIds, string updateLsFile)
        {
            string[] pfamEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds);
            string[] updateEntries = ReadUpdateEntries(updateLsFile);
            ArrayList newPfamEntryList = new ArrayList();
            Array.Sort(pfamEntries);
            foreach (string pdbId in updateEntries)
            {
                if (Array.BinarySearch(pfamEntries, pdbId) > -1)
                {
                    newPfamEntryList.Add(pdbId);
                }
            }
            string[] newPfamEntries = new string[newPfamEntryList.Count];
            newPfamEntryList.CopyTo(newPfamEntries);
            return newPfamEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLsFile"></param>
        /// <returns></returns>
        public string[] ReadUpdateEntries(string newLsFile)
        {
            StreamReader dataReader = new StreamReader(newLsFile);
            ArrayList entryList = new ArrayList();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (!entryList.Contains(line))
                {
                    entryList.Add(line.Substring(0, 4));
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintNewlyAddedEntries()
        {
            string dataDir = @"C:\Qifang\Pkinase\interfaceFiles";
            string heterDimerDir = "heterodimers";
            string homoDimerDir = "homodimers";
            ArrayList newlyAddedEntryList = new ArrayList();

            string[] heterInterfaceEntries = GetEntriesWithCoordInterfaceFiles(Path.Combine(dataDir, heterDimerDir));
            string[] updateHeterInterfaceEntries = GetEntriesWithCoordInterfaceFiles(Path.Combine(dataDir, heterDimerDir + "_update"));
            string[] newHeterEntries = GetStructuresNotInSecondArray(updateHeterInterfaceEntries, heterInterfaceEntries);

            string[] homoInterfaceEntries = GetEntriesWithCoordInterfaceFiles(Path.Combine(dataDir, homoDimerDir));
            string[] updateHomoInterfaceEntries = GetEntriesWithCoordInterfaceFiles(Path.Combine(dataDir, homoDimerDir + "_update"));
            string[] newHomoEntries = GetStructuresNotInSecondArray(updateHomoInterfaceEntries, homoInterfaceEntries);

            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "newlyAddedEntries.txt"));
            foreach (string newPdbId in newHeterEntries)
            {
                dataWriter.WriteLine(newPdbId);
            }
            foreach (string newPdbId in newHomoEntries)
            {
                if (!newHeterEntries.Contains(newPdbId))
                {
                    dataWriter.WriteLine(newPdbId);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateInterfaceEntries"></param>
        /// <param name="orgInterfaceEntries"></param>
        /// <returns></returns>
        private string[] GetStructuresNotInSecondArray(string[] updateInterfaceEntries, string[] orgInterfaceEntries)
        {
            ArrayList newEntryList = new ArrayList();
            foreach (string pdbId in updateInterfaceEntries)
            {
                if (Array.IndexOf(orgInterfaceEntries, pdbId) > -1)
                {
                    continue;
                }
                newEntryList.Add(pdbId);
            }
            string[] newEntries = new string[newEntryList.Count];
            newEntryList.CopyTo(newEntries);
            return newEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFileDir"></param>
        /// <returns></returns>
        private string[] GetEntriesWithCoordInterfaceFiles(string interfaceFileDir)
        {
            string[] interfaceFiles = Directory.GetFiles(interfaceFileDir);
            ArrayList entryList = new ArrayList();
            string pdbId = "";
            foreach (string interfaceFile in interfaceFiles)
            {
                FileInfo fileInfo = new FileInfo(interfaceFile);
                pdbId = fileInfo.Name.Substring(0, 4);
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            string[] interfaceEntries = new string[entryList.Count];
            entryList.CopyTo(interfaceEntries);
            return interfaceEntries;
        }
        #endregion

        #region interface file in uniprot sequence numbers
        /// <summary>
        /// 
        /// </summary>
        public void GenerateInterfaceFilesInUnpSeq()
        {
            pfamIds = new string[4];
            pfamIds[0] = "Integrase_Zn";
            pfamIds[1] = "rve";
            pfamIds[2] = "IN_DBD_C";
            pfamIds[3] = "zf-H2C2";

            dataType = "HivIntegrase";

            Initialize(false);


            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get " + dataType + " entries");
            string[] pfamEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds); // GetPdbEntriesWithPkinaseDomains();

            /*       ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate " + dataType + " interface files.");
                   ProtCidSettings.progressInfo.totalStepNum = pfamEntries.Length;
                   ProtCidSettings.progressInfo.totalOperationNum = pfamEntries.Length;

                   foreach (string pdbId in pfamEntries)
                   {
                       ProtCidSettings.progressInfo.currentFileName = pdbId;
                       ProtCidSettings.progressInfo.currentOperationNum++;
                       ProtCidSettings.progressInfo.currentStepNum++;

                       interfaceFileGen.GenerateEntryInterfaceFiles(pdbId);
                   }
                   ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate interface files done!");
                   */
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate entry interface files uniprot seq ids.");
            UpdateEntryInterfaceFilesInUnpSeqIds(pfamEntries);

            PrintUniprotCodesForSpecificPfams(pfamIds, dataType);
            PrintSpecificPfamEntitySequences(pfamIds, dataType);


            //    PrintEntryPkinaseInterfaceInfoInUnpSeq(pfamEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntries"></param>
        public void UpdateEntryInterfaceFilesInUnpSeqIds(string[] entries)
        {
            StreamWriter logWriter = new StreamWriter(dataType + "InterfaceFileGenLog.txt");

            string dimerListFile = Path.Combine(dataDir, dataType + "DimerList.txt");
            StreamWriter dimerInfoWriter = new StreamWriter(dimerListFile);
            dimerInfoWriter.WriteLine("UniProt\tPfamArch\tUniProt\tPfamArch\tPDBID\tInterfaceID");
            string infoLine = "";

            Hashtable interfacePfamArchHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files For " + dataType;
            ProtCidSettings.progressInfo.totalStepNum = entries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entries.Length;

            ArrayList interfaceIdList = new ArrayList();
            int entityId1 = -1;
            int entityId2 = -1;
            string interfaceFileName = "";
            string pfamInterfaceFileName = "";
            string interfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            string hashDir = "";
            string interfaceRemarkString = "";
            string interfaceDefQueryString = "";
            string interfaceName = "";
            ArrayList entityWithInterfaceList = new ArrayList();
            foreach (string pdbId in entries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                interfacePfamArchHash.Clear();
                entityWithInterfaceList.Clear();

                interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceIdList.Clear();

                int[] pfamEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds); // GetEntitiesWithPkinaseDomains(pdbId);
                int[] entryEntities = null;
                Hashtable entityUnpCodesHash = GetEntryEntityUnpCodeHash(pdbId, out entryEntities);
                Hashtable entityPfamArchHash = GetEntityPfamArchHash(pdbId, entryEntities);
                Hashtable entityPdbUnpMapHash = GetEntityPdbUnpAlignments(pdbId, pfamEntities);

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString());

                    interfaceName = pdbId + "_" + interfaceId.ToString();

                    if (!entityWithInterfaceList.Contains(entityId1))
                    {
                        entityWithInterfaceList.Add(entityId1);
                    }
                    if (!entityWithInterfaceList.Contains(entityId2))
                    {
                        entityWithInterfaceList.Add(entityId2);
                    }

                    if (Array.IndexOf(pfamEntities, entityId1) < 0 ||
                        Array.IndexOf(pfamEntities, entityId2) < 0)
                    {
                        continue;
                    }

                    hashDir = Path.Combine(interfaceFileDir, pdbId.Substring(1, 2));
                    interfaceFileName = Path.Combine(hashDir, interfaceName + ".cryst.gz");

                    if (File.Exists(interfaceFileName))
                    {
                        interfaceFileName = ParseHelper.UnZipFile(interfaceFileName, ProtCidSettings.tempDir);
                        try
                        {
                            InterfaceChains chainInterface = new InterfaceChains();
                            interfaceRemarkString = interfaceReader.ReadInterfaceFromFile(interfaceFileName, ref chainInterface, "all");

                            ChangeInterfaceSeqIdToUnpSeqId(ref chainInterface, entityPdbUnpMapHash);

                            pfamInterfaceFileName = Path.Combine(pfamInterfaceFileDir, interfaceName + ".cryst");
                            if (IsInterfaceReversed(chainInterface, entityPfamArchHash))
                            {
                                string newInterfaceRamarkString = ReverseRemarkString(interfaceRemarkString);
                                interfaceWriter.WriteInterfaceToFile(pfamInterfaceFileName, newInterfaceRamarkString,
                                                    chainInterface.chain2, chainInterface.chain1);
                                infoLine = FormatInfoLine(chainInterface.entityId2, chainInterface.entityId1, entityPfamArchHash, entityUnpCodesHash);
                            }
                            else
                            {
                                interfaceWriter.WriteInterfaceToFile(pfamInterfaceFileName, interfaceRemarkString,
                                                     chainInterface.chain1, chainInterface.chain2);
                                infoLine = FormatInfoLine(chainInterface.entityId1, chainInterface.entityId2, entityPfamArchHash, entityUnpCodesHash);
                            }
                            infoLine = infoLine + "\t" + pdbId + "\t" + interfaceId.ToString();
                            dimerInfoWriter.WriteLine(infoLine);
                            ParseHelper.ZipPdbFile(pfamInterfaceFileName);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                        }
                    }
                    dimerInfoWriter.Flush();
                }
            }
            logWriter.Close();
            dimerInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <param name="entityUnpCodeHash"></param>
        /// <returns></returns>
        private string FormatInfoLine(int entityId1, int entityId2, Hashtable entityPfamArchHash, Hashtable entityUnpCodeHash)
        {
            string infoLine = "";
            if (entityUnpCodeHash.ContainsKey(entityId1))
            {
                infoLine += "\t" + (string)entityUnpCodeHash[entityId1];
            }
            else
            {
                infoLine += "\t";
            }
            if (entityPfamArchHash.ContainsKey(entityId1))
            {
                infoLine += "\t" + (string)entityPfamArchHash[entityId1];
            }
            else
            {
                infoLine += "\t";
            }

            if (entityUnpCodeHash.ContainsKey(entityId2))
            {
                infoLine += "\t" + (string)entityUnpCodeHash[entityId2];
            }
            else
            {
                infoLine += "\t";
            }
            if (entityPfamArchHash.ContainsKey(entityId2))
            {
                infoLine += "\t" + (string)entityPfamArchHash[entityId2];
            }
            else
            {
                infoLine += "\t";
            }
            return infoLine;
        }
        /// <summary>
        /// change interface in alphabet order of the Pfam architectures of the interface chains
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        private bool IsInterfaceReversed(InterfaceChains chainInterface, Hashtable entityPfamArchHash)
        {
            string pfamArch1 = "";
            string pfamArch2 = "";
            if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
            {
                pfamArch1 = (string)entityPfamArchHash[chainInterface.entityId1];
            }
            if (entityPfamArchHash.ContainsKey(chainInterface.entityId2))
            {
                pfamArch2 = (string)entityPfamArchHash[chainInterface.entityId2];
            }
            if (string.Compare(pfamArch1, pfamArch2) > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region generage interface files for Pkinase
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void UpdatePkinaseEntryInterfaceFilesInUnpSeqIds(string[] pdbList)
        {
            StreamWriter logWriter = new StreamWriter(dataType + "InterfaceFileGenLog.txt");

            homoDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "homodimers");
            heteroDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "heterodimers");
            string homoDimerListFile = "D:\\" + dataType + "\\" + dataType + "HomodimerList.txt";
            StreamWriter homoDimerWriter = new StreamWriter(homoDimerListFile);
            homoDimerWriter.WriteLine("UniProt\tPfamArch\tPDBID\tInterfaceList");
            string heteroDimerListFile = "D:\\" + dataType + "\\" + dataType + "HeterodimerList.txt";
            StreamWriter heteroDimerWriter = new StreamWriter(heteroDimerListFile);
            heteroDimerWriter.WriteLine("UniProt\tPfamArch\tUniProt\tPfamArch\tPDBID\tNonkinase Length\tNonkinase Sequence\tInterfaceList");
            Hashtable unpHomoDimerHash = new Hashtable();
            Hashtable unpHeteroDimerHash = new Hashtable();
            Hashtable interfacePfamArchHash = new Hashtable();
            Hashtable interfaceNonKinaseLengthHash = new Hashtable();
            Hashtable interfaceNonKinaseSequencesHash = new Hashtable();
            Hashtable interfaceKinaseSequencesHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files For Phosphorylation";
            ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;

            ArrayList interfaceIdList = new ArrayList();
            int entityId1 = -1;
            int entityId2 = -1;
            string interfaceFileName = "";
            string interfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            string hashDir = "";
            string pkinaseInterfaceFileName = "";
            string interfaceRemarkString = "";
            string interfaceDefQueryString = "";
            bool isHomoDimer = true;
            string interfaceName = "";
            ArrayList entityWithInterfaceList = new ArrayList();
            foreach (string pdbId in pdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                unpHomoDimerHash.Clear();
                unpHeteroDimerHash.Clear();
                interfacePfamArchHash.Clear();
                interfaceNonKinaseLengthHash.Clear();
                entityWithInterfaceList.Clear();
                interfaceNonKinaseSequencesHash.Clear();

                interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceIdList.Clear();

                int[] pkinaseEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds); // GetEntitiesWithPkinaseDomains(pdbId);
                int[] entryEntities = null;
                Hashtable entityUnpCodesHash = GetEntryEntityUnpCodeHash(pdbId, out entryEntities);
                Hashtable entityPfamArchHash = GetEntityPfamArchHash(pdbId, entryEntities);
                Hashtable entityLengthHash = GetEntityLengthHash(pdbId, entryEntities);
                Hashtable entitySequencesHash = GetEntitySequenceHash(pdbId, entryEntities);
                Hashtable entityPdbUnpMapHash = GetEntityPdbUnpAlignments(pdbId, pkinaseEntities);

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    isHomoDimer = false;
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString());

                    interfaceName = pdbId + "_" + interfaceId.ToString();

                    if (!entityWithInterfaceList.Contains(entityId1))
                    {
                        entityWithInterfaceList.Add(entityId1);
                    }
                    if (!entityWithInterfaceList.Contains(entityId2))
                    {
                        entityWithInterfaceList.Add(entityId2);
                    }

                    if (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                        Array.IndexOf(pkinaseEntities, entityId2) < 0)
                    {
                        continue;
                    }

                    if (entityId1 == entityId2)
                    {
                        isHomoDimer = true;
                    }

                    hashDir = Path.Combine(interfaceFileDir, pdbId.Substring(1, 2));
                    interfaceFileName = Path.Combine(hashDir, interfaceName + ".cryst.gz");

                    if (File.Exists(interfaceFileName))
                    {
                        interfaceFileName = ParseHelper.UnZipFile(interfaceFileName, ProtCidSettings.tempDir);
                        try
                        {
                            InterfaceChains chainInterface = new InterfaceChains();
                            interfaceRemarkString = interfaceReader.ReadInterfaceFromFile(interfaceFileName, ref chainInterface, "all");

                            ChangeInterfaceSeqIdToUnpSeqId(ref chainInterface, entityPdbUnpMapHash);

                            if (isHomoDimer)
                            {
                                pkinaseInterfaceFileName = PrintHomodimerAndInfo(interfaceName, pkinaseEntities, chainInterface, interfaceRemarkString,
                                       entityUnpCodesHash, entityPfamArchHash, ref unpHomoDimerHash, ref interfacePfamArchHash);
                            }
                            else
                            {
                                pkinaseInterfaceFileName = PrintHeteroDimerAndInfo(interfaceName, pkinaseEntities, chainInterface, interfaceRemarkString,
                                    entityUnpCodesHash, entityPfamArchHash, entityLengthHash, entitySequencesHash, ref unpHeteroDimerHash,
                                    ref interfacePfamArchHash, ref interfaceNonKinaseLengthHash, ref interfaceNonKinaseSequencesHash,
                                    ref interfaceKinaseSequencesHash);
                            }
                            ParseHelper.ZipPdbFile(pkinaseInterfaceFileName);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                            logWriter.WriteLine(ex.Message);
                            logWriter.Flush();
                        }
                        File.Delete(interfaceFileName);
                    }
                }
                // some peptides left out, print the interactions in the asymmetric units
                if (entityWithInterfaceList.Count < entryEntities.Length)
                {
                    int asuInteractionId = 1;
                    int[] existEntities = new int[entityWithInterfaceList.Count];
                    entityWithInterfaceList.CopyTo(existEntities);
                    InterfaceChains[] leftAsuHeteroInteractions = GetLeftHeteroInteractionsInAsu(pdbId, existEntities, pkinaseEntities);
                    for (int i = 0; i < leftAsuHeteroInteractions.Length; i++)
                    {
                        interfaceName = pdbId + "_0" + asuInteractionId.ToString();
                        ChangeInterfaceSeqIdToUnpSeqId(ref leftAsuHeteroInteractions[i], entityPdbUnpMapHash);
                        interfaceRemarkString = GetAsuInteractionRemarkString(pdbId, asuInteractionId, leftAsuHeteroInteractions[i]);
                        pkinaseInterfaceFileName = PrintHeteroDimerAndInfo(interfaceName, pkinaseEntities, leftAsuHeteroInteractions[i], interfaceRemarkString,
                                        entityUnpCodesHash, entityPfamArchHash, entityLengthHash, entitySequencesHash, ref unpHeteroDimerHash,
                                        ref interfacePfamArchHash, ref interfaceNonKinaseLengthHash,
                                        ref interfaceNonKinaseSequencesHash, ref interfaceKinaseSequencesHash);
                        asuInteractionId++;
                        ParseHelper.ZipPdbFile(pkinaseInterfaceFileName);
                    }
                }
                string dataLine = "";
                if (unpHomoDimerHash.Count > 0)
                {
                    foreach (string homoDimerUnpCode in unpHomoDimerHash.Keys)
                    {
                        ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[homoDimerUnpCode];
                        dataLine = homoDimerUnpCode + "\t" +
                            (string)interfacePfamArchHash[(string)homoDimerList[0]] + "\t" + pdbId + "\t";
                        foreach (string homoDimer in homoDimerList)
                        {
                            dataLine += (homoDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        homoDimerWriter.WriteLine(dataLine);
                    }
                }
                if (unpHeteroDimerHash.Count > 0)
                {
                    foreach (string heteroDimerUnpCode in unpHeteroDimerHash.Keys)
                    {
                        ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroDimerUnpCode];
                        string[] heteroDimerUnpCodeFields = heteroDimerUnpCode.Split(';');
                        string[] heteroPfamArchFields = ((string)interfacePfamArchHash[(string)heteroDimerList[0]]).Split(';');
                        string[] nonKinaseSequences = (string[])interfaceNonKinaseSequencesHash[(string)heteroDimerList[0]];
                        string[] kinaseSequences = (string[])interfaceKinaseSequencesHash[(string)heteroDimerList[0]];
                        dataLine = heteroDimerUnpCodeFields[0] + "\t" + heteroPfamArchFields[0] + "\t" +
                            heteroDimerUnpCodeFields[1] + "\t" + heteroPfamArchFields[1] + "\t" +
                            pdbId + "\t" +
                            interfaceNonKinaseLengthHash[(string)heteroDimerList[0]].ToString() + "\t" +
                            kinaseSequences[1] + "\t" + nonKinaseSequences[1] + "\t";
                        foreach (string heteroDimer in heteroDimerList)
                        {
                            dataLine += (heteroDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        heteroDimerWriter.WriteLine(dataLine);
                    }
                }
            }
            logWriter.Close();
            homoDimerWriter.Close();
            heteroDimerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainInterface"></param>
        /// <param name="interfaceRemarkString"></param>
        /// <param name="entityUnpCodesHash"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <param name="unpHomoDimerHash"></param>
        /// <param name="interfacePfamArchHash"></param>
        private string PrintHomodimerAndInfo(string interfaceName, int[] pkinaseEntities, InterfaceChains chainInterface, string interfaceRemarkString,
            Hashtable entityUnpCodesHash, Hashtable entityPfamArchHash, ref Hashtable unpHomoDimerHash, ref Hashtable interfacePfamArchHash)
        {
            string unpCode = "";
            string pkinaseInterfaceFileName = Path.Combine(homoDimerInterfaceFileDir, interfaceName + ".cryst");
            interfaceWriter.WriteInterfaceToFile(pkinaseInterfaceFileName, interfaceRemarkString,
            chainInterface.chain1, chainInterface.chain2);
            if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
            {
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    unpCode = (string)entityUnpCodesHash[chainInterface.entityId1];
                }
                else
                {
                    unpCode = "pdb";
                }
                if (unpHomoDimerHash.ContainsKey(unpCode))
                {
                    ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[unpCode];
                    homoDimerList.Add(interfaceName);
                }
                else
                {
                    ArrayList homoDimerList = new ArrayList();
                    homoDimerList.Add(interfaceName);
                    unpHomoDimerHash.Add(unpCode, homoDimerList);
                }
            }
            if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
            {
                interfacePfamArchHash.Add(interfaceName, entityPfamArchHash[chainInterface.entityId1]);
            }
            else
            {
                interfacePfamArchHash.Add(interfaceName, "-");
            }
            return pkinaseInterfaceFileName;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainInterface"></param>
        /// <param name="interfaceRemarkString"></param>
        /// <param name="entityUnpCodesHash"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <param name="entityLengthHash"></param>
        /// <param name="unpHeteroDimerHash"></param>
        /// <param name="interfacePfamArchHash"></param>
        /// <param name="interfaceNonKinaseLengthHash"></param>
        private string PrintHeteroDimerAndInfo(string interfaceName, int[] pkinaseEntities, InterfaceChains chainInterface,
            string interfaceRemarkString, Hashtable entityUnpCodesHash, Hashtable entityPfamArchHash, Hashtable entityLengthHash,
            Hashtable entitySequencesHash, ref Hashtable unpHeteroDimerHash, ref Hashtable interfacePfamArchHash,
            ref Hashtable interfaceNonKinaseLengthHash, ref Hashtable interfaceNonKinaseSequencesHash,
            ref Hashtable interfaceKinaseSqeuencesHash)
        {
            string pkinaseInterfaceFileName = Path.Combine(heteroDimerInterfaceFileDir, interfaceName + ".cryst");
            string heteroUnpCodes = "";
            string heteroPfamArchs = "";
            if (Array.IndexOf(pkinaseEntities, chainInterface.entityId1) > -1)
            {
                interfaceWriter.WriteInterfaceToFile(pkinaseInterfaceFileName, interfaceRemarkString,
                                    chainInterface.chain1, chainInterface.chain2);
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroUnpCodes = (string)entityUnpCodesHash[chainInterface.entityId1];
                }
                else
                {
                    heteroUnpCodes = "pdb";
                }
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[chainInterface.entityId2]);
                }
                else
                {
                    heteroUnpCodes += ";pdb";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroPfamArchs = (string)entityPfamArchHash[chainInterface.entityId1];
                }
                else
                {
                    heteroPfamArchs = "-";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroPfamArchs += (";" + (string)entityPfamArchHash[chainInterface.entityId2]);
                }
                else
                {
                    heteroPfamArchs += ";-";
                }
                interfaceNonKinaseLengthHash.Add(interfaceName,
                       (int)entityLengthHash[chainInterface.entityId2]);
                interfaceNonKinaseSequencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId2]);
                interfaceKinaseSqeuencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId1]);
            }
            else
            {
                string newInterfaceRamarkString = ReverseRemarkString(interfaceRemarkString);
                interfaceWriter.WriteInterfaceToFile(pkinaseInterfaceFileName, newInterfaceRamarkString,
                                    chainInterface.chain2, chainInterface.chain1);
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroUnpCodes = (string)entityUnpCodesHash[chainInterface.entityId2];
                }
                else
                {
                    heteroUnpCodes = "pdb";
                }
                if (entityUnpCodesHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[chainInterface.entityId1]);
                }
                else
                {
                    heteroUnpCodes += ";pdb";
                }

                if (entityPfamArchHash.ContainsKey(chainInterface.entityId2))
                {
                    heteroPfamArchs = (string)entityPfamArchHash[chainInterface.entityId2];
                }
                else
                {
                    heteroPfamArchs = "-";
                }
                if (entityPfamArchHash.ContainsKey(chainInterface.entityId1))
                {
                    heteroPfamArchs += (";" + (string)entityPfamArchHash[chainInterface.entityId1]);
                }
                else
                {
                    heteroPfamArchs += ";-";
                }
                interfaceNonKinaseLengthHash.Add(interfaceName,
                                (int)entityLengthHash[chainInterface.entityId1]);
                interfaceNonKinaseSequencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId1]);
                interfaceKinaseSqeuencesHash.Add(interfaceName, (string[])entitySequencesHash[chainInterface.entityId2]);
            }
            if (unpHeteroDimerHash.ContainsKey(heteroUnpCodes))
            {
                ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroUnpCodes];
                heteroDimerList.Add(interfaceName);
            }
            else
            {
                ArrayList heteroDimerList = new ArrayList();
                heteroDimerList.Add(interfaceName);
                unpHeteroDimerHash.Add(heteroUnpCodes, heteroDimerList);
            }
            interfacePfamArchHash.Add(interfaceName, heteroPfamArchs);
            return pkinaseInterfaceFileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void PrintEntryPkinaseInterfaceInfoInUnpSeq(string[] pdbList)
        {
            StreamWriter logWriter = new StreamWriter(dataType + "InterfaceFileGenLog.txt");

            string homoDimerFile = "D:\\" + dataType + "\\" + dataType + "HomodimerList.txt";
            StreamWriter homoDimerWriter = new StreamWriter(homoDimerFile);
            homoDimerWriter.WriteLine("UniProt\tPfamArch\tPDBID\tInterfaceList");
            string heteroDimerFile = "D:\\" + dataType + "\\" + dataType + "HomoDimerList.txt";
            StreamWriter heteroDimerWriter = new StreamWriter(heteroDimerFile);
            heteroDimerWriter.WriteLine("UniProt\tPfamArch\tUniProt\tPfamArch\tPDBID\tKinase Sequence\tNonkinase Sequence\tNonkinase Length\tInterfaceList");
            Hashtable unpHomoDimerHash = new Hashtable();
            Hashtable unpHeteroDimerHash = new Hashtable();
            Hashtable interfacePfamArchHash = new Hashtable();
            Hashtable interfaceNonKinaseLengthHash = new Hashtable();
            Hashtable interfaceNonKinaseSequencesHash = new Hashtable();
            Hashtable interfaceKinaseSequencesHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files For Phosphorylation";
            ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;

            ArrayList interfaceIdList = new ArrayList();
            int entityId1 = -1;
            int entityId2 = -1;
            string interfaceDefQueryString = "";
            bool isHomoDimer = true;
            string unpCode = "";
            string heteroUnpCodes = "";
            string heteroPfamArchs = "";
            string interfaceName = "";
            ArrayList entityWithInterfaceList = new ArrayList();
            foreach (string pdbId in pdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                unpHomoDimerHash.Clear();
                unpHeteroDimerHash.Clear();
                interfacePfamArchHash.Clear();
                interfaceNonKinaseLengthHash.Clear();
                interfaceNonKinaseSequencesHash.Clear();
                entityWithInterfaceList.Clear();

                interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceIdList.Clear();

                int[] pkinaseEntities = GetEntitiesWithSpecificPfams(pdbId, pfamIds); //GetEntitiesWithPkinaseDomains(pdbId);
                int[] entryEntities = null;
                Hashtable entityUnpCodesHash = GetEntryEntityUnpCodeHash(pdbId, out entryEntities);
                Hashtable entityPfamArchHash = GetEntityPfamArchHash(pdbId, entryEntities);
                Hashtable entityLengthHash = GetEntityLengthHash(pdbId, entryEntities);
                Hashtable entitySequenceHash = GetEntitySequenceHash(pdbId, entryEntities);
                Hashtable entityPdbUnpMapHash = GetEntityPdbUnpAlignments(pdbId, pkinaseEntities);

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    isHomoDimer = false;
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString());

                    if (!entityWithInterfaceList.Contains(entityId1))
                    {
                        entityWithInterfaceList.Add(entityId1);
                    }
                    if (!entityWithInterfaceList.Contains(entityId2))
                    {
                        entityWithInterfaceList.Add(entityId2);
                    }

                    if (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                        Array.IndexOf(pkinaseEntities, entityId2) < 0)
                    {
                        continue;
                    }

                    if (entityId1 == entityId2)
                    {
                        isHomoDimer = true;
                    }

                    try
                    {
                        if (isHomoDimer)
                        {
                            if (entityUnpCodesHash.ContainsKey(entityId1))
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    unpCode = (string)entityUnpCodesHash[entityId1];
                                }
                                else
                                {
                                    unpCode = "pdb";
                                }
                                if (unpHomoDimerHash.ContainsKey(unpCode))
                                {
                                    ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[unpCode];
                                    homoDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                }
                                else
                                {
                                    ArrayList homoDimerList = new ArrayList();
                                    homoDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                    unpHomoDimerHash.Add(unpCode, homoDimerList);
                                }
                            }
                            if (entityPfamArchHash.ContainsKey(entityId1))
                            {
                                interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), entityPfamArchHash[entityId1]);
                            }
                            else
                            {
                                interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), "-");
                            }
                        }
                        else
                        {
                            heteroUnpCodes = "";
                            heteroPfamArchs = "";
                            if (Array.IndexOf(pkinaseEntities, entityId1) > -1)
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    heteroUnpCodes = (string)entityUnpCodesHash[entityId1];
                                }
                                else
                                {
                                    heteroUnpCodes = "pdb";
                                }
                                if (entityUnpCodesHash.ContainsKey(entityId2))
                                {
                                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[entityId2]);
                                }
                                else
                                {
                                    heteroUnpCodes += ";pdb";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId1))
                                {
                                    heteroPfamArchs = (string)entityPfamArchHash[entityId1];
                                }
                                else
                                {
                                    heteroPfamArchs = "-";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId2))
                                {
                                    heteroPfamArchs += (";" + (string)entityPfamArchHash[entityId2]);
                                }
                                else
                                {
                                    heteroPfamArchs += ";-";
                                }
                                interfaceNonKinaseLengthHash.Add(pdbId + "_" + interfaceId.ToString(),
                                       (int)entityLengthHash[entityId2]);
                                interfaceNonKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId2]);
                                interfaceKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId1]);
                            }
                            else
                            {
                                if (entityUnpCodesHash.ContainsKey(entityId2))
                                {
                                    heteroUnpCodes = (string)entityUnpCodesHash[entityId2];
                                }
                                else
                                {
                                    heteroUnpCodes = "pdb";
                                }
                                if (entityUnpCodesHash.ContainsKey(entityId1))
                                {
                                    heteroUnpCodes += (";" + (string)entityUnpCodesHash[entityId1]);
                                }
                                else
                                {
                                    heteroUnpCodes += ";pdb";
                                }

                                if (entityPfamArchHash.ContainsKey(entityId2))
                                {
                                    heteroPfamArchs = (string)entityPfamArchHash[entityId2];
                                }
                                else
                                {
                                    heteroPfamArchs = "-";
                                }
                                if (entityPfamArchHash.ContainsKey(entityId1))
                                {
                                    heteroPfamArchs += (";" + (string)entityPfamArchHash[entityId1]);
                                }
                                else
                                {
                                    heteroPfamArchs += ";-";
                                }
                                interfaceNonKinaseLengthHash.Add(pdbId + "_" + interfaceId.ToString(),
                                                (int)entityLengthHash[entityId1]);
                                interfaceNonKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                    (string[])entitySequenceHash[entityId1]);
                                interfaceKinaseSequencesHash.Add(pdbId + "_" + interfaceId.ToString(),
                                   (string[])entitySequenceHash[entityId2]);
                            }
                            if (unpHeteroDimerHash.ContainsKey(heteroUnpCodes))
                            {
                                ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroUnpCodes];
                                heteroDimerList.Add(pdbId + "_" + interfaceId.ToString());
                            }
                            else
                            {
                                ArrayList heteroDimerList = new ArrayList();
                                heteroDimerList.Add(pdbId + "_" + interfaceId.ToString());
                                unpHeteroDimerHash.Add(heteroUnpCodes, heteroDimerList);
                            }

                            interfacePfamArchHash.Add(pdbId + "_" + interfaceId.ToString(), heteroPfamArchs);
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                        logWriter.WriteLine(ex.Message);
                        logWriter.Flush();
                    }
                }
                // some peptides left out, print the interactions in the asymmetric units
                if (entityWithInterfaceList.Count < entryEntities.Length)
                {
                    int asuInteractionId = 1;
                    string interfaceRemarkString = "";
                    string pkinaseInterfaceFileName = "";
                    int[] existEntities = new int[entityWithInterfaceList.Count];
                    entityWithInterfaceList.CopyTo(existEntities);
                    InterfaceChains[] leftAsuHeteroInteractions = GetLeftHeteroInteractionsInAsu(pdbId, existEntities, pkinaseEntities);
                    for (int i = 0; i < leftAsuHeteroInteractions.Length; i++)
                    {
                        interfaceName = pdbId + "_0" + asuInteractionId.ToString();
                        ChangeInterfaceSeqIdToUnpSeqId(ref leftAsuHeteroInteractions[i], entityPdbUnpMapHash);
                        interfaceRemarkString = GetAsuInteractionRemarkString(pdbId, asuInteractionId, leftAsuHeteroInteractions[i]);
                        pkinaseInterfaceFileName = PrintHeteroDimerAndInfo(interfaceName, pkinaseEntities, leftAsuHeteroInteractions[i], interfaceRemarkString,
                                        entityUnpCodesHash, entityPfamArchHash, entityLengthHash, entitySequenceHash,
                                        ref unpHeteroDimerHash, ref interfacePfamArchHash,
                                        ref interfaceNonKinaseLengthHash, ref interfaceNonKinaseSequencesHash,
                                        ref interfaceKinaseSequencesHash);
                        asuInteractionId++;
                        ParseHelper.ZipPdbFile(pkinaseInterfaceFileName);
                    }
                }
                string dataLine = "";
                if (unpHomoDimerHash.Count > 0)
                {
                    foreach (string homoDimerUnpCode in unpHomoDimerHash.Keys)
                    {
                        ArrayList homoDimerList = (ArrayList)unpHomoDimerHash[homoDimerUnpCode];
                        dataLine = homoDimerUnpCode + "\t" +
                            (string)interfacePfamArchHash[(string)homoDimerList[0]] + "\t" + pdbId + "\t";
                        foreach (string homoDimer in homoDimerList)
                        {
                            dataLine += (homoDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        homoDimerWriter.WriteLine(dataLine);
                    }
                }
                if (unpHeteroDimerHash.Count > 0)
                {
                    foreach (string heteroDimerUnpCode in unpHeteroDimerHash.Keys)
                    {
                        ArrayList heteroDimerList = (ArrayList)unpHeteroDimerHash[heteroDimerUnpCode];
                        string[] heteroDimerUnpCodeFields = heteroDimerUnpCode.Split(';');
                        string[] heteroPfamArchFields = ((string)interfacePfamArchHash[(string)heteroDimerList[0]]).Split(';');
                        string[] nonKinaseSequences = (string[])interfaceNonKinaseSequencesHash[(string)heteroDimerList[0]];
                        string[] kinaseSequences = (string[])interfaceKinaseSequencesHash[(string)heteroDimerList[0]];
                        dataLine = heteroDimerUnpCodeFields[0] + "\t" + heteroPfamArchFields[0] + "\t" +
                            heteroDimerUnpCodeFields[1] + "\t" + heteroPfamArchFields[1] + "\t" +
                            pdbId + "\t" +
                            kinaseSequences[1] + "\t" + nonKinaseSequences[1] + "\t" +
                            interfaceNonKinaseLengthHash[(string)heteroDimerList[0]].ToString() + "\t";
                        foreach (string heteroDimer in heteroDimerList)
                        {
                            dataLine += (heteroDimer + ",");
                        }
                        dataLine = dataLine.TrimEnd(',');
                        heteroDimerWriter.WriteLine(dataLine);
                    }
                }
            }
            logWriter.Close();
            homoDimerWriter.Close();
            heteroDimerWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Hashtable GetEntryEntityUnpCodeHash(string pdbId, out int[] entryEntities)
        {
            string queryString = string.Format("Select Distinct EntityID From AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';",
                pdbId);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Hashtable entityUnpCodeHash = new Hashtable();
            int entityId = 0;
            string entityUnpCodes = "";
            entryEntities = new int[entityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                entryEntities[count] = entityId;
                count++;

                string[] unpCodes = GetEntityUnpCodes(pdbId, entityId);
                entityUnpCodes = FormatUnpCodes(unpCodes);

                entityUnpCodeHash.Add(entityId, entityUnpCodes);
            }
            return entityUnpCodeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpCodes"></param>
        /// <returns></returns>
        private string FormatUnpCodes(string[] unpCodes)
        {
            string entityUnpCodes = "";

            if (unpCodes.Length == 1)
            {
                entityUnpCodes = unpCodes[0];
            }
            else
            {
                foreach (string unpCode in unpCodes)
                {
                    entityUnpCodes += ("(" + unpCode + ")_");
                }
                entityUnpCodes = entityUnpCodes.TrimEnd('_');
            }
            return entityUnpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remarkString"></param>
        /// <returns></returns>
        private string ReverseRemarkString(string remarkString)
        {
            string[] remarkFields = remarkString.Split("\r\n".ToCharArray());
            string newRemarkString = "";
            int oldChainAIndex = -1;
            int oldChainBIndex = -1;
            for (int i = 0; i < remarkFields.Length; i++)
            {
                if (remarkFields[i].IndexOf("Interface Chain A") > -1)
                {
                    oldChainAIndex = i;
                }
                else if (remarkFields[i].IndexOf("Interface Chain B") > -1)
                {
                    oldChainBIndex = i;
                }
            }
            string temp = remarkFields[oldChainAIndex];
            remarkFields[oldChainAIndex] = remarkFields[oldChainBIndex];
            remarkFields[oldChainBIndex] = temp;

            foreach (string remarkField in remarkFields)
            {
                if (remarkField == "")
                {
                    continue;
                }
                if (remarkField.IndexOf("Interface Chain A") > -1)
                {
                    newRemarkString += (remarkField.Replace("Interface Chain A", "Interface Chain B") + "\r\n");
                }
                else if (remarkField.IndexOf("Interface Chain B") > -1)
                {
                    newRemarkString += (remarkField.Replace("Interface Chain B", "Interface Chain A") + "\r\n");
                }
                else
                {
                    newRemarkString += (remarkField + "\r\n");
                }
            }
            return newRemarkString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        private string[] GetEntityAuthorChains(string pdbId, int[] entities)
        {
            string queryString = string.Format("Select Distinct AuthorChain From AsymUnit " +
                " Where PdbID = '{0}' AND EntityID IN ({1});", pdbId, ParseHelper.FormatSqlListString(entities));
            DataTable authChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] authChains = new string[authChainTable.Rows.Count];
            int count = 0;
            foreach (DataRow authChainRow in authChainTable.Rows)
            {
                authChains[count] = authChainRow["AuthorChain"].ToString().TrimEnd();
                count++;
            }
            return authChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryEntities"></param>
        /// <returns></returns>
        private Hashtable GetEntityPfamArchHash(string pdbId, int[] entryEntities)
        {
            Hashtable entityPfamArchHash = new Hashtable();
            string entityPfamArch = "";
            foreach (int entityId in entryEntities)
            {
                entityPfamArch = pfamArch.GetEntityPfamArch(pdbId, entityId);
                entityPfamArchHash.Add(entityId, entityPfamArch);
            }
            return entityPfamArchHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        private Hashtable GetEntityLengthHash(string pdbId, int[] entities)
        {
            Hashtable entityLengthHash = new Hashtable();
            int entityLength = 0;
            foreach (int entityId in entities)
            {
                entityLength = GetEntityLength(pdbId, entityId);
                entityLengthHash.Add(entityId, entityLength);
            }
            return entityLengthHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private int GetEntityLength(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable entitySequenceTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (entitySequenceTable.Rows.Count > 0)
            {
                return entitySequenceTable.Rows[0]["Sequence"].ToString().TrimEnd().Length;
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        private Hashtable GetEntitySequenceHash(string pdbId, int[] entities)
        {
            Hashtable entitySequenceHash = new Hashtable();
            string[] sequences = null;
            foreach (int entityId in entities)
            {
                sequences = GetEntitySequenceNstd(pdbId, entityId);
                entitySequenceHash.Add(entityId, sequences);
            }
            return entitySequenceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntitySequenceNstd(string pdbId, int entityId)
        {
            string queryString = string.Format("Select SequenceNstd, Sequence From EntityInfo Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable seqNstdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (seqNstdTable.Rows.Count > 0)
            {
                string[] sequences = new string[2];
                sequences[0] = seqNstdTable.Rows[0]["Sequence"].ToString().TrimEnd();
                sequences[0] = sequences[0].Replace("\n", "");
                sequences[1] = seqNstdTable.Rows[0]["SequenceNstd"].ToString().TrimEnd();
                sequences[1] = sequences[1].Replace("\n", "");
                return sequences;
            }
            return null;
        }
        #endregion

        #region change seq id to uniprot seq id
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="entityPdbUnpMapHash"></param>
        private void ChangeInterfaceSeqIdToUnpSeqId(ref InterfaceChains chainInterface, Hashtable entityPdbUnpMapHash)
        {
            int entityId1 = chainInterface.entityId1;
            int entityId2 = chainInterface.entityId2;
            try
            {
                if (entityPdbUnpMapHash.ContainsKey(entityId1))
                {
                    ChangeSeqIdOfAtoms(chainInterface.chain1, (Hashtable)entityPdbUnpMapHash[entityId1]);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(chainInterface.pdbId + " " + chainInterface.interfaceId.ToString() + " " +
                    chainInterface.entityId1.ToString() + " errors:  " + ex.Message);
            }
            try
            {
                if (entityPdbUnpMapHash.ContainsKey(entityId2))
                {
                    ChangeSeqIdOfAtoms(chainInterface.chain2, (Hashtable)entityPdbUnpMapHash[entityId2]);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(chainInterface.pdbId + " " + chainInterface.interfaceId.ToString() + " " +
                    chainInterface.entityId2.ToString() + " errors:  " + ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        /// <param name="pdbUnpMapHash"></param>
        private void ChangeSeqIdOfAtoms(AtomInfo[] atoms, Hashtable pdbUnpMapHash)
        {
            string seqId = "";
            string unpSeqId = "";
            for (int i = 0; i < atoms.Length; i++)
            {
                seqId = atoms[i].seqId;
                if (pdbUnpMapHash.ContainsKey(seqId))
                {
                    unpSeqId = (string)pdbUnpMapHash[seqId];
                }
                else
                {
                    unpSeqId = "n9999"; // for those with no uniprot matching
                }
                atoms[i].seqId = unpSeqId;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        private Hashtable GetEntityPdbUnpAlignments(string pdbId, int[] entities)
        {
            Hashtable entityPdbUnpMapHash = new Hashtable();
            foreach (int entityId in entities)
            {
                Hashtable pdbUnpMapHash = GetEntityPdbUnpAlignment(pdbId, entityId);
                entityPdbUnpMapHash.Add(entityId, pdbUnpMapHash);
            }
            return entityPdbUnpMapHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private Hashtable GetEntityPdbUnpAlignment(string pdbId, int entityId)
        {
            string asymChain = GetAsymChainForEntityId(pdbId, entityId);
            string queryString = string.Format("Select PdbDbRefSeqAlignSifts.PdbID, " +
                " DbCode, AsymID, AuthorChain, SeqNumbers, DbSeqNumbers" +
                " From PdbDbRefSeqAlignSifts, PdbDbRefSifts " +
                " Where PdbDbRefSeqAlignSifts.PdbID = '{0}' AND AsymID = '{1}' AND" +
                " PdbDbRefSeqAlignSifts.PdbID = PdbDbRefSifts.PdbID AND " +
                " PdbDbRefSeqAlignSifts.RefID = PdbDbRefSifts.RefID ;", pdbId, asymChain);
            DataTable alignmentTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Hashtable pdbUnpMapHash = new Hashtable();
            string seqNumbers = "";
            string dbSeqNumbers = "";
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                seqNumbers = alignmentRow["SeqNumbers"].ToString();
                string[] seqNumberFields = seqNumbers.Split(',');
                dbSeqNumbers = alignmentRow["DbSeqNumbers"].ToString();
                string[] dbSeqNumberFields = dbSeqNumbers.Split(',');
                // the length of seqNumbers and dbSeqNumbers should be same
                for (int i = 0; i < seqNumberFields.Length; i++)
                {
                    if (seqNumberFields[i] != "-" && dbSeqNumberFields[i] != "-")
                    {
                        pdbUnpMapHash.Add(seqNumberFields[i], dbSeqNumberFields[i]);
                    }
                }
            }
            return pdbUnpMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetAsymChainForEntityId(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit WHERE PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable asymChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (asymChainTable.Rows.Count > 0)
            {
                return asymChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion

        #region pdb structures for specific PFAMs
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetPdbEntriesWithSpecificPFAMs(string[] pfamIds)
        {
            string queryString = string.Format("Select Distinct PdbId From PdbPfam Where Pfam_ID IN ({0});",
                ParseHelper.FormatSqlListString(pfamIds));
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                entries[count] = pdbId;
                count++;
            }
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetPdbEntitiesWithSpecificPfams(string[] pfamIds)
        {
            string queryString = string.Format("Select Distinct PdbId, EntityID From PdbPfam Where Pfam_ID IN ({0});",
                ParseHelper.FormatSqlListString(pfamIds));
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] entryEntities = new string[entityTable.Rows.Count];
            int count = 0;
            string entryEntity = "";
            foreach (DataRow entryRow in entityTable.Rows)
            {
                entryEntity = entryRow["PdbID"].ToString() + entryRow["EntityID"].ToString();
                entryEntities[count] = entryEntity;
                count++;
            }
            return entryEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntitiesWithSpecificPfams(string pdbId, string[] pfamIds)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbPfam " +
                " Where PdbID = '{0}' AND Pfam_ID IN ({1});", pdbId, ParseHelper.FormatSqlListString(pfamIds));
            DataTable pfamEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int[] pfamEntities = new int[pfamEntityTable.Rows.Count];
            int entityId = 0;
            int count = 0;
            foreach (DataRow entityRow in pfamEntityTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                pfamEntities[count] = entityId;
                count++;
            }
            return pfamEntities;
        }


        /// <summary>
        /// 
        /// </summary>
        public void PrintUniprotCodesForSpecificPfams(string[] pfamIds, string dataName)
        {
            Initialize(false);
            string unpCodeFile = Path.Combine(dataDir, dataName + "UnpPdbList.txt");
            StreamWriter dataWriter = new StreamWriter(unpCodeFile);
            string unpSequenceFile = Path.Combine(dataDir, dataName + "UnpSequences.txt");
            StreamWriter seqWriter = new StreamWriter(unpSequenceFile);

            string queryString = string.Format("Select Distinct PdbId, EntityID From PdbPfam Where Pfam_ID IN ({0});",
                ParseHelper.FormatSqlListString(pfamIds));
            DataTable pfamEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            int entityId = 0;
            string dataLine = "";
            Hashtable unpCodePdbCodeHash = new Hashtable();
            Hashtable unpCodeAccessionHash = new Hashtable();
            foreach (DataRow entityRow in pfamEntityTable.Rows)
            {
                pdbId = entityRow["pdbID"].ToString();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                string[] unpCodes = GetEntityUnpCodes(pdbId, entityId, ref unpCodeAccessionHash);
                foreach (string unpCode in unpCodes)
                {
                    if (unpCodePdbCodeHash.ContainsKey(unpCode))
                    {
                        ArrayList entryList = (ArrayList)unpCodePdbCodeHash[unpCode];
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                        }
                    }
                    else
                    {
                        ArrayList entryList = new ArrayList();
                        entryList.Add(pdbId);
                        unpCodePdbCodeHash.Add(unpCode, entryList);
                    }
                }
            }
            string unpAccession = "";
            string entryLine = "";
            foreach (string unpCode in unpCodePdbCodeHash.Keys)
            {
                dataLine = unpCode;
                entryLine = "";
                ArrayList entryList = (ArrayList)unpCodePdbCodeHash[unpCode];
                foreach (string entry in entryList)
                {
                    entryLine += (" " + entry);
                }
                dataLine = dataLine + " " + entryLine;
                dataWriter.WriteLine(dataLine);

                unpAccession = (string)unpCodeAccessionHash[unpCode];
                string[] seqInfo = GetUnpSequence(unpAccession);
                dataLine = seqInfo[0];
                dataLine += " | " + entryLine;
                seqWriter.WriteLine(dataLine);
                seqWriter.WriteLine(seqInfo[1]);
            }
            dataWriter.Close();
            seqWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpAccessionCode"></param>
        /// <returns></returns>
        private string[] GetUnpSequence(string unpAccessionCode)
        {
            string unpSeqFile = DownloadUnpSeqFile(unpAccessionCode);
            StreamReader dataReader = new StreamReader(unpSeqFile);
            string headerLine = dataReader.ReadLine();
            string sequence = "";
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                sequence += line;
            }
            dataReader.Close();

            string[] seqInfo = new string[2];
            seqInfo[0] = headerLine;
            seqInfo[1] = sequence;
            return seqInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpAccessionCode"></param>
        /// <returns></returns>
        public string DownloadUnpSeqFile(string unpAccessionCode)
        {
            string unpSeqFile = Path.Combine(dataDir, unpAccessionCode + ".fasta");
            string httpAddress = "http://www.uniprot.org/uniprot/";
            webClient.DownloadFile(httpAddress + unpAccessionCode + ".fasta", unpSeqFile);
            return unpSeqFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntityUnpCodes(string pdbId, int entityId, ref Hashtable unpCodeAccessionHash)
        {
            string queryString = string.Format("Select Distinct DbCode, DbAccession From PdbDbRefSifts " +
                " WHERE PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (dbCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode, DbAccession From PdbDbRefXml " +
                         " WHERE PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            }
            string[] unpCodes = new string[dbCodeTable.Rows.Count];
            int count = 0;
            string unpCode = "";
            string unpAccession = "";
            foreach (DataRow unpRow in dbCodeTable.Rows)
            {
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                unpAccession = unpRow["DbAccession"].ToString().TrimEnd();
                unpCodes[count] = unpCode;
                if (!unpCodeAccessionHash.ContainsKey(unpCode))
                {
                    unpCodeAccessionHash.Add(unpCode, unpAccession);
                }
                count++;
            }
            return unpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntityUnpCodes(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct DbCode, DbAccession From PdbDbRefSifts " +
                " WHERE PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (dbCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbCode, DbAccession From PdbDbRefXml " +
                         " WHERE PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            }
            string[] unpCodes = new string[dbCodeTable.Rows.Count];
            int count = 0;
            foreach (DataRow unpRow in dbCodeTable.Rows)
            {
                unpCodes[count] = unpRow["DbCode"].ToString().TrimEnd();
                count++;
            }
            return unpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamEntries"></param>
        /// <returns></returns>
        private string[] GetEntriesWithoutInterfaceFiles(string[] pfamEntries)
        {
            ArrayList noInterfaceFileEntryList = new ArrayList();
            //       string interfaceFilePath = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
      //      string interfaceFilePath = dataDir;
            StreamWriter dataWriter = new StreamWriter(dataDir + "\\newlist.txt");
            foreach (string pdbId in pfamEntries)
            {
                //     hashDir = Path.Combine(interfaceFilePath, pdbId.Substring(1, 2));
                string[] interfaceFiles = Directory.GetFiles(pfamInterfaceFileDir, pdbId + "*");
                if (interfaceFiles.Length == 0)
                {
                    noInterfaceFileEntryList.Add(pdbId);
                    dataWriter.WriteLine(pdbId);
                }
            }
            dataWriter.Close();
            string[] noInterfaceFileEntries = new string[noInterfaceFileEntryList.Count];
            noInterfaceFileEntryList.CopyTo(noInterfaceFileEntries);
            return noInterfaceFileEntries;
        }
        #endregion

        #region the interactions in ASU between pkinase and small peptides
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryEntities"></param>
        /// <param name="existEntities"></param>
        private InterfaceChains[] GetLeftHeteroInteractionsInAsu(string pdbId, int[] existEntities,
            int[] pkinaseEntities)
        {
            string asuXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string asuFile = ParseHelper.UnZipFile(asuXmlFile, ProtCidSettings.tempDir);
            Dictionary<string, AtomInfo[]> asuHash = asuInterfaces.GetAsuFromXml(asuFile);
            InterfaceChains[] asuInteractions = asuInterfaces.GetAllInteractionsInAsu(pdbId, asuHash);
            Hashtable asymChainEntityHash = GetAsymChainEntityHash(pdbId);
            string asymChain1 = "";
            string asymChain2 = "";
            int entityId1 = 0;
            int entityId2 = 0;
            ArrayList leftInteractionList = new ArrayList();
            foreach (InterfaceChains asuInteraction in asuInteractions)
            {
                asymChain1 = GetAsymChainFromSymOpString(asuInteraction.firstSymOpString);
                asymChain2 = GetAsymChainFromSymOpString(asuInteraction.secondSymOpString);
                if (asymChain1 == asymChain2)
                {
                    continue;
                }
                entityId1 = (int)asymChainEntityHash[asymChain1];
                entityId2 = (int)asymChainEntityHash[asymChain2];
                if (entityId1 == entityId2)
                {
                    continue;
                }
                if (Array.IndexOf(existEntities, entityId1) > -1 &&
                    Array.IndexOf(existEntities, entityId2) > -1)
                {
                    continue;
                }
                if ((Array.IndexOf(pkinaseEntities, entityId1) > -1 &&
                    Array.IndexOf(pkinaseEntities, entityId2) < 0) ||
                    (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                    Array.IndexOf(pkinaseEntities, entityId2) > -1))
                {
                    asuInteraction.entityId1 = entityId1;
                    asuInteraction.entityId2 = entityId2;
                    leftInteractionList.Add(asuInteraction);
                }
            }
            InterfaceChains[] leftAsuInteractions = new InterfaceChains[leftInteractionList.Count];
            leftInteractionList.CopyTo(leftAsuInteractions);
            return leftAsuInteractions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symOpString"></param>
        /// <returns></returns>
        private string GetAsymChainFromSymOpString(string symOpString)
        {
            string[] fields = symOpString.Split('_');
            return fields[0];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Hashtable GetAsymChainEntityHash(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Hashtable asymChainEntityHash = new Hashtable();
            int entityId = 0;
            string asymChain = "";
            foreach (DataRow chainRow in entityChainTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString());
                asymChain = chainRow["AsymID"].ToString().TrimEnd();
                asymChainEntityHash.Add(asymChain, entityId);
            }
            return asymChainEntityHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="asymLigandAtomInfoHash"></param>
        /// <returns></returns>
        private Hashtable[] GetInterfaceLigandAtomInfoHashes(InterfaceChains chainInterface, Hashtable asymLigandAtomInfoHash)
        {
            Hashtable[] ligandAtomInfoHashes = null;
            string asymId1 = GetAsymChainId(chainInterface.firstSymOpString);
            string asymId2 = GetAsymChainId(chainInterface.secondSymOpString);
            if (asymId1 == asymId2)
            {
                ligandAtomInfoHashes = new Hashtable[1];
                ligandAtomInfoHashes[0] = (Hashtable)asymLigandAtomInfoHash[asymId1];
            }
            else
            {
                ligandAtomInfoHashes = new Hashtable[2];
                ligandAtomInfoHashes[0] = (Hashtable)asymLigandAtomInfoHash[asymId1];
                ligandAtomInfoHashes[1] = (Hashtable)asymLigandAtomInfoHash[asymId2];
            }
            return ligandAtomInfoHashes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="symOpString"></param>
        /// <returns></returns>
        private string GetAsymChainId(string symOpString)
        {
            string[] fields = symOpString.Split('_');
            return fields[0];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        private string GetAsuInteractionRemarkString(string pdbId, int interfaceId, InterfaceChains chainInterface)
        {
            string remarkString = "HEADER    Entry: " + pdbId + "  InterfaceID: " +
                interfaceId.ToString() + "   " + DateTime.Today.ToShortDateString() + "\r\n";
            string[] symOpFields = chainInterface.firstSymOpString.Split('_');
            remarkString += "Remark 300 Interface Chain A For Asymmetric Chain " + symOpFields[0] + " Entity " +
                chainInterface.entityId1.ToString() + " Symmetry Operator     " + symOpFields[1] + "_" + symOpFields[2] + "\r\n";
            symOpFields = chainInterface.secondSymOpString.Split('_');
            remarkString += "Remark 300 Interface Chain B For Asymmetric Chain " + symOpFields[0] + " Entity " +
                chainInterface.entityId2.ToString() + " Symmetry Operator     " + symOpFields[1] + "_" + symOpFields[2] + "\r\n";
            return remarkString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetAsuTable(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit WHere PdbID = '{0}';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return asuTable;
        }
        #endregion

        #region entry, space groups and symmetry operators
        public void PrintSpaceGroupSymmetryOperators()
        {
            DbBuilderHelper.Initialize();

            string[] entries = ReadSymOpEntries();
            StreamWriter dataWriter = new StreamWriter("SymOperators.txt");
            string spaceGroup = "";
            foreach (string entry in entries)
            {
                spaceGroup = GetEntrySpaceGroup(entry);
                SymOpMatrix[] symOpMatrices = AppSettings.symOps.FindSpaceGroup(spaceGroup);
                dataWriter.WriteLine(entry + "\t" + spaceGroup);
                foreach (SymOpMatrix symOpMatrix in symOpMatrices)
                {
                    dataWriter.WriteLine(symOpMatrix.symmetryString + "\t" + symOpMatrix.fullSymmetryString);
                }
                dataWriter.WriteLine();
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadSymOpEntries()
        {
            StreamReader dataReader = new StreamReader("symmetryEntryList.txt");
            string line = "";
            ArrayList entryList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line.TrimEnd());
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        private string GetEntrySpaceGroup(string pdbId)
        {
            string queryString = string.Format("Select SpaceGroup From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable sgTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (sgTable.Rows.Count > 0)
            {
                return sgTable.Rows[0]["SpaceGroup"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion

        #region pkinase sum info
        public void PrintPkinaseSumInfo()
        {
            Initialize(false);
            string queryString = "Select Distinct PdbID, EntityID, Pfam_ID From PdbPfam Where Pfam_ID like 'Pkinase%';";
            DataTable pkinaseEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            int entityId = 0;
            string pfamId = "";
            StreamWriter dataWriter = new StreamWriter("PkinaseEntries.txt");
            foreach (DataRow pkinaseEntityRow in pkinaseEntryTable.Rows)
            {
                pfamId = pkinaseEntityRow["Pfam_ID"].ToString().TrimEnd();
                if (pfamId == "Pkinase_C")
                {
                    continue;
                }
                pdbId = pkinaseEntityRow["PdbID"].ToString();
                entityId = Convert.ToInt32(pkinaseEntityRow["EntityID"].ToString());
                string[] authorChains = GetAuthorChains(pdbId, entityId);
                string unpCode = GetUnpCode(pdbId, entityId);

                dataWriter.WriteLine(pdbId + "\t" + entityId.ToString() + "\t" +
                    FormatChains(authorChains) + "\t" + unpCode + "\t" + pfamId);
            }
            dataWriter.Close();
        }

        private string[] GetAuthorChains(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct AuthorChain From AsymUnit Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable authChainsTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] authorChains = new string[authChainsTable.Rows.Count];
            int count = 0;
            foreach (DataRow authChainRow in authChainsTable.Rows)
            {
                authorChains[count] = authChainRow["AuthorChain"].ToString().TrimEnd();
                count++;
            }
            return authorChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetUnpCode(string pdbId, int entityId)
        {
            string queryString = string.Format("Select DbCode From PdbDbRefSifts " +
                " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';",
                pdbId, entityId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (unpCodeTable.Rows.Count > 0)
            {
                if (unpCodeTable.Rows.Count > 1)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("More UNP codes in the entity: " + pdbId + entityId.ToString());
                }
                return unpCodeTable.Rows[0]["DbCode"].ToString().TrimEnd();
            }
            return "-";
        }

        private string FormatChains(string[] chainIds)
        {
            string chainString = "";
            foreach (string chainId in chainIds)
            {
                chainString += (chainId + ",");
            }
            return chainString.TrimEnd(',');
        }
        #endregion

        #region sequences
        /// <summary>
        /// 
        /// </summary>
        public void PrintSpecificPfamEntitySequences(string[] pfamIds, string dataType)
        {
            string[] pkinaseEntities = GetPdbEntitiesWithSpecificPfams(pfamIds); // GetPdbEntitiesWithPkinaseDomains();
            StreamWriter dataWriter = new StreamWriter(Path.Combine(pfamInterfaceFileDir, dataType + "PdbSequences.txt"));
            string pdbId = "";
            int entityId = 0;
            string entityPfamArch = "";
            string entityUnpCodeString = "";
            foreach (string entryEntity in pkinaseEntities)
            {
                pdbId = entryEntity.Substring(0, 4);
                entityId = Convert.ToInt32(entryEntity.Substring(4, entryEntity.Length - 4));
                string[] entityInfoStrings = GetEntityInfo(pdbId, entityId);
                entityPfamArch = GetEntityPfamArch(pdbId, entityId);
                string[] unpCodes = GetEntityUnpCodes(pdbId, entityId);
                entityUnpCodeString = FormatUnpCodes(unpCodes);
                dataWriter.WriteLine(">" + pdbId + entityId + " | " +
                    entityInfoStrings[1] + " " + entityInfoStrings[2] + " " +
                    entityPfamArch + " " + entityUnpCodeString);
                dataWriter.WriteLine(entityInfoStrings[0]);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntityInfo(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID, AuthorChain, Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string sequence = "";
            string asymIds = "";
            string authorChains = "";
            if (seqTable.Rows.Count > 0)
            {
                sequence = seqTable.Rows[0]["Sequence"].ToString().TrimEnd();
                foreach (DataRow seqRow in seqTable.Rows)
                {
                    asymIds += (seqRow["AsymID"].ToString().TrimEnd() + ",");
                    authorChains += (seqRow["AuthorChain"].ToString().TrimEnd() + ",");
                }
            }
            string[] entityInfoStrings = new string[3];
            entityInfoStrings[0] = sequence;
            entityInfoStrings[1] = asymIds.TrimEnd(',');
            entityInfoStrings[2] = authorChains.TrimEnd(',');
            return entityInfoStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntityPfamArch(string pdbId, int entityId)
        {
            string entityPfamArch = pfamArch.GetEntityPfamArch(pdbId, entityId);
            return entityPfamArch;
        }
        #endregion

        #region structure alignments
        public void PrintStructSequenceAlignments()
        {
            pfamIds = new string[4];
            pfamIds[0] = "Integrase_Zn";
            pfamIds[1] = "rve";
            pfamIds[2] = "IN_DBD_C";
            pfamIds[3] = "zf-H2C2";

            dataType = "HivIntegrase";

            Initialize(false);

            ProtCidSettings.alignmentDbConnection = new DbConnect();
            ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath;

            string[] pfamEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds);
            string[] foamEntries = {"2x6n", "2x6s", "2x74", "2x78", "3dlr", "3l2q", "3l2r", 
                                 "3l2u", "3l2v", "3l2w", "3os0", "3os1", "3os2", "3oy9", "3oya", "3oyb", "3oyc",
                                 "3oyd", "3oye", "3oyf", "3oyg", "3oyh", "3oyi", "3oyj", "3oyk", "3oyl", "3oym", "3oyn", 
                                 "3s3m", "3s3n", "3s3o", "4e7h", "4e7i", "4e7j", "4e7k", "4e7l"};

            Hashtable repFoamEntryHash = GetFoamRepEntries(foamEntries);
            StreamWriter foamReduntEntryWriter = new StreamWriter(@"D:\HivIntegrase\FoamSameSeqEntries.txt");
            string dataLine = "";
            foreach (string repEntry in repFoamEntryHash.Keys)
            {
                dataLine = repEntry + " ";
                ArrayList homoEntryList = (ArrayList)repFoamEntryHash[repEntry];
                foreach (string homoEntry in homoEntryList)
                {
                    if (repEntry == homoEntry)
                    {
                        continue;
                    }
                    dataLine += (homoEntry + " ");
                }
                foamReduntEntryWriter.WriteLine(dataLine.TrimEnd(' '));
            }
            foamReduntEntryWriter.Close();


            ArrayList leftFoamEntryList = new ArrayList();
            ArrayList alignFoamEntryList = new ArrayList();
            bool alignExist = false;

            StreamWriter fatcatAlignWriter = new StreamWriter(@"D:\HivIntegrase\fatcatAlign.txt");
            /*          for (int i = 0; i < pfamEntries.Length; i ++ )
                       {
                           for (int j = i + 1; j < pfamEntries.Length; j++)
                           {*/
            foreach (string foamEntry in foamEntries)
            {
                alignExist = false;
                foreach (string pfamEntry in pfamEntries)
                {
                    if (foamEntry == pfamEntry)
                    {
                        continue;
                    }
                    DataTable fatcatAlignTable = GetFatcatStructSeqAlignment(foamEntry, pfamEntry);
                    if (fatcatAlignTable.Rows.Count > 0)
                    {
                        fatcatAlignWriter.WriteLine(ParseHelper.FormatDataRows(fatcatAlignTable.Select()));
                        alignExist = true;
                    }
                }
                if (!alignExist)
                {
                    leftFoamEntryList.Add(foamEntry);
                }
                else
                {
                    alignFoamEntryList.Add(foamEntry);
                }
            }
            fatcatAlignWriter.Close();


            ProtCidSettings.alignmentDbConnection.DisconnectFromDatabase();


        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="foamEntries"></param>
        /// <returns></returns>
        private Hashtable GetFoamRepEntries(string[] foamEntries)
        {
            string queryString = string.Format("Select Distinct Crc From PDBCRCMap Where PdbId IN {0};", ParseHelper.FormatSqlListString(foamEntries));
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string crc = "";
            Hashtable repFoamEntryHash = new Hashtable();
            string repEntry = "";
            string homoEntry = "";
            foreach (DataRow crcRow in crcTable.Rows)
            {
                crc = crcRow["Crc"].ToString().TrimEnd();
                queryString = string.Format("Select Distinct PdbID, IsRep From PDBCRCMap Where crc = '{0}';", crc);
                DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                DataRow[] repRows = entryTable.Select("IsRep = '{1}'");
                if (repRows.Length > 0)
                {
                    repEntry = repRows[0]["PdbID"].ToString();
                    entryTable.Rows.Remove(repRows[0]);
                    foreach (DataRow entryRow in entryTable.Rows)
                    {
                        homoEntry = entryRow["PdbID"].ToString();
                        if (repFoamEntryHash.ContainsKey(repEntry))
                        {
                            ArrayList homoEntryList = (ArrayList)repFoamEntryHash[repEntry];
                            if (!homoEntryList.Contains(homoEntry))
                            {
                                homoEntryList.Add(homoEntry);
                            }
                        }
                        else
                        {
                            ArrayList homoEntryList = new ArrayList();
                            homoEntryList.Add(homoEntry);
                            repFoamEntryHash.Add(repEntry, homoEntryList);
                        }
                    }
                }
            }
            return repFoamEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private DataTable GetFatcatStructSeqAlignment(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select QueryEntry, QueryAsymChain, HitEntry, HitAsymChain, QueryStart, QueryEnd, " +
                " HitStart, HitEnd, E_value, QuerySequence, HitSequence From FatcatAlignments " +
                " Where (QueryEntry = '{0}' AND HitEntry = '{1}') Or (QueryEntry = '{1}' AND HitEntry = '{0}');", pdbId1, pdbId2);
            DataTable fatcatAlignTable = ProtCidSettings.alignmentQuery.Query( queryString);
            return fatcatAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintEntryStructPairs()
        {
            pfamIds = new string[4];
            pfamIds[0] = "Integrase_Zn";
            pfamIds[1] = "rve";
            pfamIds[2] = "IN_DBD_C";
            pfamIds[3] = "zf-H2C2";

            dataType = "HivIntegrase";

            Initialize(false);

            string[] pfamEntries = GetPdbEntriesWithSpecificPFAMs(pfamIds);

            ProtCidSettings.alignmentDbConnection = new DbConnect();
            ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath;

            Hashtable entryAlignChainHash = GetPfamChains(pfamEntries, pfamIds);

            string chainPairFile = @"D:\HivIntegrase\HivStructChainPairs.txt";
            StreamWriter chainPairWriter = new StreamWriter(chainPairFile);
            for (int i = 0; i < pfamEntries.Length; i++)
            {
                string[] alignChainsI = (string[])entryAlignChainHash[pfamEntries[i]];
                for (int j = i + 1; j < pfamEntries.Length; j++)
                {
                    string[] alignChainsJ = (string[])entryAlignChainHash[pfamEntries[j]];
                    foreach (string alignChainI in alignChainsI)
                    {
                        foreach (string alignChainJ in alignChainsJ)
                        {
                            if (IsFatcatAlignmentExist(pfamEntries[i], alignChainI, pfamEntries[j], alignChainJ))
                            {
                                continue;
                            }
                            chainPairWriter.WriteLine(pfamEntries[i] + alignChainI + "    " + pfamEntries[j] + alignChainJ);
                        }
                    }
                }
                chainPairWriter.Flush();
            }
            chainPairWriter.Close();
            ProtCidSettings.alignmentDbConnection.DisconnectFromDatabase();

            // divid the chain pairs list, to run on cluster
            InterfaceClusterLib.Alignments.GroupEntryAlignments entryAlign = new InterfaceClusterLib.Alignments.GroupEntryAlignments();
            entryAlign.DivideNonAlignedChainPairs(chainPairFile, 1000);

            // parse the fatcat structure alignments, and insert into the database
            // parse the alignment file, insert data into alignments.fdb
            // should wait for the structure alignments done
            DataCollectorLib.FatcatAlignment.FatcatAlignmentParser alnFileParser = new DataCollectorLib.FatcatAlignment.FatcatAlignmentParser();

            string[] alnFiles = Directory.GetFiles(@"D:\DbProjectData\Fatcat\ChainAlignments", "*.aln");
            try
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing the alignment file.");
                //    alnFileParser.ParseFatcatAlignments(alnFile, true);
                alnFileParser.ParseFatcatAlignments(alnFiles, true);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing FATCAT alignment files errors: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private Hashtable GetPfamChains(string[] entries, string[] pfamIds)
        {
            Hashtable entryAlignChainsHash = new Hashtable();
            foreach (string pdbId in entries)
            {
                string[] alignChains = GetPfamChains(pdbId, pfamIds);
                entryAlignChainsHash.Add(pdbId, alignChains);
            }
            return entryAlignChainsHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private string[] GetPfamChains(string pdbId, string[] pfamIds)
        {
            string queryString = string.Format("Select EntityID From PdbPfam" +
                " Where PdbID = '{0}' AND Pfam_ID IN ({1});", pdbId, ParseHelper.FormatSqlListString(pfamIds));
            DataTable pfamEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            ArrayList alignChainList = new ArrayList();
            int entityId = 0;
            string asymChain = "";
            foreach (DataRow entityRow in pfamEntityTable.Rows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                asymChain = GetTheBestChainsForSequence(pdbId, entityId);
                alignChainList.Add(asymChain);
            }
            string[] alignChains = new string[alignChainList.Count];
            alignChainList.CopyTo(alignChains);
            return alignChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetTheBestChainsForSequence(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID, SequenceInCoord From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable chainCoordSeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            int numOfCoord = 0;
            int bestNumOfCoord = 0;
            string bestAsymChain = "";
            string seqInCoord = "";
            foreach (DataRow coordSeqRow in chainCoordSeqTable.Rows)
            {
                seqInCoord = coordSeqRow["SequenceInCoord"].ToString().TrimEnd();
                numOfCoord = GetNumOfCoordinates(seqInCoord);
                if (numOfCoord > bestNumOfCoord)
                {
                    bestNumOfCoord = numOfCoord;
                    bestAsymChain = coordSeqRow["AsymID"].ToString().TrimEnd();
                }
            }
            return bestAsymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequenceInCoord"></param>
        /// <returns></returns>
        private int GetNumOfCoordinates(string sequenceInCoord)
        {
            int numOfCoordinates = 0;
            foreach (char ch in sequenceInCoord)
            {
                if (ch != '-')
                {
                    numOfCoordinates++;
                }
            }
            return numOfCoordinates;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="asymChain1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="asymChain2"></param>
        /// <returns></returns>
        private bool IsFatcatAlignmentExist(string pdbId1, string asymChain1, string pdbId2, string asymChain2)
        {
            string queryString = string.Format("Select * From FatcatAlignments Where " +
                " (QueryEntry = '{0}' AND HitEntry = '{1}' AND QueryAsymChain = '{2}' AND HitAsymChain = '{3}') OR " +
                " (QueryEntry = '{1}' AND HitEntry = '{0}' AND QueryAsymChain = '{3}' AND HitAsymChain = '{2}');",
                pdbId1, pdbId2, asymChain1, asymChain2);
            DataTable alignTable = ProtCidSettings.alignmentQuery.Query( queryString);
            if (alignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region surface area of interfaces
        public void CalculteSurfaceAreas()
        {
            dataDir = @"C:\Paper\Kinase\KinaseInterfaces";
            string asaFileDir = Path.Combine(dataDir, "surfaceAreaFiles");
            if (!Directory.Exists(asaFileDir))
            {
                Directory.CreateDirectory(asaFileDir);
            }

            int distance = 15;
            string complexPeptideRegionFile = Path.Combine(dataDir, "complexAutophosPositions.txt");
            Hashtable complexPepRegionHash = SetUpPeptideRegions(complexPeptideRegionFile, distance);

            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "kinaseComplexNonPhosphSAs.txt"), true);
            dataWriter.WriteLine("Autophos_Complex\tSA\tpeptideSA\tDifference");
            dataWriter.WriteLine("Distance = " + distance.ToString());
            string[] complexFiles = Directory.GetFiles(dataDir, "*.pdb");
            string[] symComplexFileNames = {/*"IGF1R_3D94", "LCK_2PL0", */"CLK2_3NR9_1", "CLK2_3NR9_2" };
            string interfaceFileName = "";
            CrystalInterfaceLib.ProtInterfaces.AsaCalculator asaCal = new CrystalInterfaceLib.ProtInterfaces.AsaCalculator();
            double notPepSurfaceArea = 0;
            foreach (string complexFile in complexFiles)
            {
                FileInfo fileInfo = new FileInfo(complexFile);
                interfaceFileName = fileInfo.Name.Replace(".pdb", "").ToUpper();
                int[] peptideRegion = (int[])complexPepRegionHash[interfaceFileName];
                if (Array.IndexOf(symComplexFileNames, interfaceFileName) > -1)
                {
                    string[] newChainFiles = WriteChainFilesRemoveBoth(complexFile, peptideRegion);
                    notPepSurfaceArea = asaCal.ComputeInterfaceSurfaceArea(newChainFiles[2], newChainFiles[0], newChainFiles[1]);
                    dataWriter.WriteLine(interfaceFileName + "\t" + notPepSurfaceArea.ToString());
                    dataWriter.Flush();
                }
                else
                {
                    /*     string[] chainFiles = WriteChainFile(complexFile, peptideRegion);
                         interfaceSA = asaCal.ComputeInterfaceSurfaceArea(complexFile, chainFiles[0], chainFiles[1]);
                         newInterfaceSA = asaCal.ComputeInterfaceSurfaceArea(chainFiles[2], chainFiles[0], chainFiles[3]);

                         notPepSurfaceArea = interfaceSA - newInterfaceSA;
                         dataWriter.WriteLine(interfaceFileName + "\t" + interfaceSA.ToString() + "\t" + newInterfaceSA.ToString() + "\t" + notPepSurfaceArea.ToString());
                         dataWriter.Flush();
                            string[] newChainFiles = WriteChainFilesRemoveSubstrate (complexFile, peptideRegion);
                            notPepSurfaceArea = asaCal.ComputeInterfaceSurfaceArea(newChainFiles[2], newChainFiles[0], newChainFiles[1]);
                            dataWriter.WriteLine(interfaceFileName + "\t" + notPepSurfaceArea.ToString());
                            dataWriter.Flush();*/
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Hashtable SetUpPeptideRegions(string complexAutoPhosphPositionFile, int distance)
        {
            StreamReader dataReader = new StreamReader(complexAutoPhosphPositionFile);
            Hashtable complexPeptideRegionHash = new Hashtable();
            string line = "";
            string autoPhosPosString = "";
            int autoPhosPosition = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                autoPhosPosString = "";
                string[] lineFields = line.Split('\t');
                foreach (char ch in lineFields[2])
                {
                    if (char.IsDigit(ch))
                    {
                        autoPhosPosString += ch.ToString();
                    }
                }
                autoPhosPosition = Convert.ToInt32(autoPhosPosString);
                int[] peptideRegion = new int[2];
                peptideRegion[0] = autoPhosPosition - distance;
                peptideRegion[1] = autoPhosPosition + distance;
                complexPeptideRegionHash.Add(lineFields[0], peptideRegion);
            }
            dataReader.Close();
            return complexPeptideRegionHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <returns></returns>
        private string[] WriteChainFile(string complexFile, int[] substratePeptideRegion)
        {
            FileInfo fileInfo = new FileInfo(complexFile);
            StreamReader dataReader = new StreamReader(complexFile);
            string line = "";
            string chainAFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "A.pdb"));
            string chainBFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "B.pdb"));
            string peptideFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "pep.pdb"));
            string newComplexFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "new.pdb"));
            StreamWriter chainAWriter = new StreamWriter(chainAFile);
            StreamWriter chainBWriter = new StreamWriter(chainBFile);
            StreamWriter peptideWriter = new StreamWriter(peptideFile);
            StreamWriter newComplexWriter = new StreamWriter(newComplexFile);
            string chainId = "";
            int pepSeqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] lineFields = ParseHelper.ParsePdbAtomLine(line);
                    chainId = lineFields[5];
                    if (chainId == "A")
                    {
                        chainAWriter.WriteLine(line);
                        newComplexWriter.WriteLine(line);
                    }
                    else if (chainId == "B")
                    {
                        pepSeqId = Convert.ToInt32(lineFields[6]);
                        if (pepSeqId <= substratePeptideRegion[1] && pepSeqId >= substratePeptideRegion[0])
                        {
                            peptideWriter.WriteLine(line);
                            newComplexWriter.WriteLine(line);
                        }

                        chainBWriter.WriteLine(line);
                    }
                }
            }
            dataReader.Close();
            chainAWriter.Close();
            chainBWriter.Close();
            peptideWriter.Close();
            newComplexWriter.Close();

            string[] chainFiles = new string[4];
            chainFiles[0] = chainAFile;
            chainFiles[1] = chainBFile;
            chainFiles[2] = newComplexFile;
            chainFiles[3] = peptideFile;
            return chainFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <returns></returns>
        private string[] WriteChainFilesRemoveBoth(string complexFile, int[] substratePeptideRegion)
        {
            FileInfo fileInfo = new FileInfo(complexFile);
            StreamReader dataReader = new StreamReader(complexFile);
            string line = "";
            string newChainAFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "A.pdb"));
            string newChainBFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "B.pdb"));
            string newComplexFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "new.pdb"));
            StreamWriter chainAWriter = new StreamWriter(newChainAFile);
            StreamWriter chainBWriter = new StreamWriter(newChainBFile);
            StreamWriter newComplexWriter = new StreamWriter(newComplexFile);
            string chainId = "";
            int seqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] lineFields = ParseHelper.ParsePdbAtomLine(line);
                    chainId = lineFields[5];
                    seqId = Convert.ToInt32(lineFields[6]);
                    if (chainId == "A")
                    {
                        if (seqId > substratePeptideRegion[1] || seqId < substratePeptideRegion[0])
                        {
                            chainAWriter.WriteLine(line);
                            newComplexWriter.WriteLine(line);
                        }
                    }
                    else if (chainId == "B")
                    {
                        if (seqId > substratePeptideRegion[1] || seqId < substratePeptideRegion[0])
                        {
                            chainBWriter.WriteLine(line);
                            newComplexWriter.WriteLine(line);
                        }
                    }
                }
            }
            dataReader.Close();
            chainAWriter.Close();
            chainBWriter.Close();
            newComplexWriter.Close();

            string[] chainFiles = new string[3];
            chainFiles[0] = newChainAFile;
            chainFiles[1] = newChainBFile;
            chainFiles[2] = newComplexFile;
            return chainFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <returns></returns>
        private string[] WriteChainFilesRemoveSubstrate(string complexFile, int[] substratePeptideRegion)
        {
            FileInfo fileInfo = new FileInfo(complexFile);
            StreamReader dataReader = new StreamReader(complexFile);
            string line = "";
            string newChainAFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "A.pdb"));
            string newChainBFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "B.pdb"));
            string newComplexFile = Path.Combine(fileInfo.DirectoryName, "saTempFiles\\" + fileInfo.Name.Replace(".pdb", "new.pdb"));
            StreamWriter chainAWriter = new StreamWriter(newChainAFile);
            StreamWriter chainBWriter = new StreamWriter(newChainBFile);
            StreamWriter newComplexWriter = new StreamWriter(newComplexFile);
            string chainId = "";
            int seqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] lineFields = ParseHelper.ParsePdbAtomLine(line);
                    chainId = lineFields[5];
                    seqId = Convert.ToInt32(lineFields[6]);
                    if (chainId == "A")
                    {
                        chainAWriter.WriteLine(line);
                        newComplexWriter.WriteLine(line);
                    }
                    else if (chainId == "B")
                    {
                        if (seqId > substratePeptideRegion[1] || seqId < substratePeptideRegion[0])
                        {
                            chainBWriter.WriteLine(line);
                            newComplexWriter.WriteLine(line);
                        }
                    }
                }
            }
            dataReader.Close();
            chainAWriter.Close();
            chainBWriter.Close();
            newComplexWriter.Close();

            string[] chainFiles = new string[3];
            chainFiles[0] = newChainAFile;
            chainFiles[1] = newChainBFile;
            chainFiles[2] = newComplexFile;
            return chainFiles;
        }
        #endregion

        #region substrate specificity -- contacts
        private const double distCutoff = 8.0;
        public struct PhosphoSite
        {
            public string name;
            public int seqId;
        }

        public void CalculateContacts()
        {
            dataDir = @"C:\Paper\Kinase\KinaseInterfaces\newInterfaces";
 //           string peptideDataDir = @"C:\Paper\Kinase\KinaseInterfaces\KinasePeptides";
            string contactFileDir = @"C:\Paper\Kinase\KinaseInterfaces\KinasePeptideEnzymeContacts";
            string distFile = Path.Combine(dataDir, "SubPeptideDistances.txt");
            int pepSubDistance = 10;
            string complexPeptideRegionFile = Path.Combine(dataDir, "complexAutophosPositions.txt");
            Hashtable[] pepSubInfoHashs = SetUpPeptideMotifRegions(complexPeptideRegionFile, pepSubDistance);
            StreamWriter sumContactWriter = new StreamWriter(Path.Combine(contactFileDir, "pepContactsSummary.txt"), true);

            string[] protComplexFiles = Directory.GetFiles(dataDir, "*.pdb");
            /*        string[] pepComplexFiles = Directory.GetFiles(peptideDataDir, "*.cryst");
                    string[] complexFiles = new string[protComplexFiles.Length + pepComplexFiles.Length];
                    Array.Copy(protComplexFiles, complexFiles, protComplexFiles.Length);
                    Array.Copy(pepComplexFiles, 0, complexFiles, protComplexFiles.Length, pepComplexFiles.Length);*/
            string contactFile = "";
            string complexName = "";
            string creixellAnnotLines = "";
            //      foreach (string complexFile in complexFiles)
            foreach (string complexFile in protComplexFiles)
            {
                FileInfo fileInfo = new FileInfo(complexFile);
                contactFile = Path.Combine(contactFileDir, fileInfo.Name.Replace(fileInfo.Extension, ".txt"));
                complexName = fileInfo.Name.Replace(fileInfo.Extension, "").ToUpper();
                PhosphoSite phosphoSite = (PhosphoSite)pepSubInfoHashs[0][complexName];
                Hashtable motifNumHash = (Hashtable)pepSubInfoHashs[1][complexName];
                /*   if (pepSubRegion == null)
                   {
                       CalculatePepSubstrateMotifDistances(complexFile, contactFile, motifNumHash);
                   }
                   else
                   {    */
                creixellAnnotLines = CalculatePepSubstrateMotifDistances(complexFile, contactFile, phosphoSite, pepSubDistance, motifNumHash);
                sumContactWriter.WriteLine(creixellAnnotLines);
            }
            sumContactWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void FormatSummaryContacts()
        {
            double distCutoff = 5.0;
            dataDir = @"C:\Paper\Kinase\KinaseInterfaces\KinasePeptideEnzymeContacts";
            string orgSummaryFile = Path.Combine(dataDir, "protSubstrateContactsSummary0.txt");
            string newSummaryFile = Path.Combine(dataDir, "motifProtPepContactsSummary_dist5.txt");
            GetSummaryContacts(orgSummaryFile, newSummaryFile, distCutoff);

            orgSummaryFile = Path.Combine(dataDir, "pepSubstrateContactsSummary0.txt");
            newSummaryFile = Path.Combine(dataDir, "motifPepContactsSummary_dist5.txt");
            GetSummaryContacts(orgSummaryFile, newSummaryFile, distCutoff);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orgSummaryFile"></param>
        /// <param name="newSummaryFile"></param>
        private void GetSummaryContacts(string orgSummaryFile, string newSummaryFile, double distCutoff)
        {
            StreamReader dataReader = new StreamReader(orgSummaryFile);
            string line = "";
            Hashtable motifContactsHash = new Hashtable();
            Hashtable motifResiduePosHash = new Hashtable();
            string motifName = "";
            string pepResidueName = "";
            string pepResidueAnnotId = "";
            double dist = 0;
            ArrayList complexStructList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                string[] lineFields = line.Split('\t');
                if (lineFields[0] == "IGF1R_3d94" || lineFields[0] == "LCK_2pl0")
                {
                    continue;
                }
                string[] structFields = lineFields[0].Split('_');
                if (structFields.Length == 3 && structFields[2] == "2")
                {
                    continue;
                }

                dist = Convert.ToDouble(lineFields[9]);
                if (dist > distCutoff)
                {
                    continue;
                }

                if (!complexStructList.Contains(structFields[0] + "_" + structFields[1]))
                {
                    complexStructList.Add(structFields[0] + "_" + structFields[1]);
                }

                motifName = lineFields[11];
                pepResidueName = lineFields[10];
                pepResidueAnnotId = lineFields[10].Substring(1, lineFields[10].Length - 1);
                if (motifContactsHash.ContainsKey(motifName))
                {
                    Hashtable pepContactHash = (Hashtable)motifContactsHash[motifName];
                    if (pepContactHash.ContainsKey(pepResidueName))
                    {
                        ArrayList structList = (ArrayList)pepContactHash[pepResidueName];
                        if (!structList.Contains(lineFields[0]))
                        {
                            structList.Add(lineFields[0]);
                        }
                    }
                    else
                    {
                        ArrayList structList = new ArrayList();
                        structList.Add(lineFields[0]);
                        pepContactHash.Add(pepResidueName, structList);
                    }
                }
                else
                {
                    Hashtable pepContactHash = new Hashtable();
                    ArrayList structList = new ArrayList();
                    structList.Add(lineFields[0]);
                    pepContactHash.Add(pepResidueName, structList);
                    motifContactsHash.Add(motifName, pepContactHash);
                }

                if (motifResiduePosHash.ContainsKey(motifName))
                {
                    Hashtable pepContactHash = (Hashtable)motifResiduePosHash[motifName];
                    if (pepContactHash.ContainsKey(pepResidueAnnotId))
                    {
                        ArrayList structList = (ArrayList)pepContactHash[pepResidueAnnotId];
                        if (!structList.Contains(lineFields[0]))
                        {
                            structList.Add(lineFields[0]);
                        }
                    }
                    else
                    {
                        ArrayList structList = new ArrayList();
                        structList.Add(lineFields[0]);
                        pepContactHash.Add(pepResidueAnnotId, structList);
                    }
                }
                else
                {
                    Hashtable pepContactHash = new Hashtable();
                    ArrayList structList = new ArrayList();
                    structList.Add(lineFields[0]);
                    pepContactHash.Add(pepResidueAnnotId, structList);
                    motifResiduePosHash.Add(motifName, pepContactHash);
                }
            }
            dataReader.Close();
            StreamWriter dataWriter = new StreamWriter(newSummaryFile);
            ArrayList motifList = new ArrayList(motifContactsHash.Keys);
            motifList.Sort();
            string dataLine = "";
            foreach (string motif in motifList)
            {
                Hashtable contactHash = (Hashtable)motifContactsHash[motif];
                ArrayList residueList = new ArrayList(contactHash.Keys);
                foreach (string residueAnnot in residueList)
                {
                    ArrayList structList = (ArrayList)contactHash[residueAnnot];
                    string[] structs = new string[structList.Count];
                    structList.CopyTo(structs);
                    dataLine = motif + "\t" + residueAnnot + "\t" + structList.Count.ToString() + "\t" + ParseHelper.FormatStringFieldsToString(structs);
                    dataWriter.WriteLine(dataLine);
                }
            }
            foreach (string motif in motifList)
            {
                Hashtable contactHash = (Hashtable)motifResiduePosHash[motif];
                ArrayList residueList = new ArrayList(contactHash.Keys);
                foreach (string residueAnnot in residueList)
                {
                    ArrayList structList = (ArrayList)contactHash[residueAnnot];
                    string[] structs = new string[structList.Count];
                    structList.CopyTo(structs);
                    dataLine = motif + "\t" + residueAnnot + "\t" + structList.Count.ToString() + "\t" + ParseHelper.FormatStringFieldsToString(structs);
                    dataWriter.WriteLine(dataLine);
                }
            }

            ArrayList contactStructList = new ArrayList();
            int numOfContacts = 0;
            ArrayList posContactStructList = new ArrayList();
            int numOfContactsPos = 0;
            foreach (string motif in motifList)
            {
                contactStructList.Clear();
                posContactStructList.Clear();
                numOfContacts = 0;
                numOfContactsPos = 0;

                Hashtable contactHash = (Hashtable)motifContactsHash[motif];
                foreach (string contact in contactHash.Keys)
                {
                    ArrayList structList = (ArrayList)contactHash[contact];
                    foreach (string complexStruct in structList)
                    {
                        if (!contactStructList.Contains(complexStruct))
                        {
                            contactStructList.Add(complexStruct);
                        }
                    }
                    numOfContacts += structList.Count;
                }
                Hashtable contactPosHash = (Hashtable)motifResiduePosHash[motif];
                foreach (string contact in contactPosHash.Keys)
                {
                    ArrayList structList = (ArrayList)contactPosHash[contact];
                    foreach (string complexStruct in structList)
                    {
                        if (!posContactStructList.Contains(complexStruct))
                        {
                            posContactStructList.Add(complexStruct);
                        }
                    }
                    numOfContactsPos += structList.Count;
                }
                dataLine = motif + "\t" + contactHash.Count.ToString() + "\t" + contactStructList.Count.ToString() + "\t" + numOfContacts.ToString() + "\t" +
                    contactPosHash.Count.ToString() + "\t" + posContactStructList.Count.ToString() + "\t" + numOfContactsPos.ToString();
                dataWriter.WriteLine(dataLine);
            }

            dataWriter.WriteLine("# of complexes structures used for analyzing: " + complexStructList.Count.ToString());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="pepRegion"></param>
        /// <param name="motifNumHash"></param>
        public void CalculatePepSubstrateMotifDistances(string complexFile, string contactFile, int[] pepRegion, Hashtable motifNumHash)
        {
            InterfaceChains interfaceChains = new InterfaceChains();
            ReadInterfaceChainsFromFile(complexFile, ref interfaceChains);
            int peptideSeqId = 0;
            double distance = 0;
            string creixellAnnot = "";
            StreamWriter contactWriter = new StreamWriter(contactFile);
            foreach (AtomInfo pepAtom in interfaceChains.chain2)
            {
                peptideSeqId = Convert.ToInt32(pepAtom.seqId);
                if (peptideSeqId >= pepRegion[0] && peptideSeqId <= pepRegion[1])
                {
                    foreach (AtomInfo enzymeAtom in interfaceChains.chain1)
                    {
                        distance = CalculateDistance(pepAtom.xyz, enzymeAtom.xyz);
                        if (distance < distCutoff)
                        {
                            creixellAnnot = AddCreixellAnnotations(Convert.ToInt32(enzymeAtom.seqId), motifNumHash);
                            contactWriter.WriteLine(pepAtom.seqId + "\t" + pepAtom.residue + "\t" + pepAtom.atomName + "\t" + pepAtom.atomId + "\t" +
                                enzymeAtom.seqId + "\t" + enzymeAtom.residue + "\t" + enzymeAtom.atomName + "\t" + enzymeAtom.atomId + "\t" +
                                distance.ToString() + "\t" + creixellAnnot);
                        }
                    }
                }
            }
            contactWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="pepRegion"></param>
        /// <param name="motifNumHash"></param>
        public void CalculatePepSubstrateMotifDistances(string complexFile, string contactFile, Hashtable motifNumHash)
        {
            InterfaceChains interfaceChains = new InterfaceChains();
            ReadInterfaceChainsFromFile(complexFile, ref interfaceChains);
            double distance = 0;
            string creixellAnnot = "";
            StreamWriter contactWriter = new StreamWriter(contactFile);
            foreach (AtomInfo pepAtom in interfaceChains.chain2)
            {
                foreach (AtomInfo enzymeAtom in interfaceChains.chain1)
                {
                    distance = CalculateDistance(pepAtom.xyz, enzymeAtom.xyz);
                    if (distance < distCutoff)
                    {
                        creixellAnnot = AddCreixellAnnotations(Convert.ToInt32(enzymeAtom.seqId), motifNumHash);
                        contactWriter.WriteLine(pepAtom.seqId + "\t" + pepAtom.residue + "\t" + pepAtom.atomName + "\t" + pepAtom.atomId + "\t" +
                            enzymeAtom.seqId + "\t" + enzymeAtom.residue + "\t" + enzymeAtom.atomName + "\t" + enzymeAtom.atomId + "\t" +
                            distance.ToString() + "\t" + creixellAnnot);
                    }
                }

            }
            contactWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="pepRegion"></param>
        /// <param name="motifNumHash"></param>
        public string CalculatePepSubstrateMotifDistances(string complexFile, string contactFile, PhosphoSite phosphoSite, int pepSubDistance, Hashtable motifNumHash)
        {
            InterfaceChains interfaceChains = new InterfaceChains();
            ReadInterfaceChainsFromFile(complexFile, ref interfaceChains);
            FileInfo fileInfo = new FileInfo(complexFile);
            string complexName = fileInfo.Name.Replace(fileInfo.Extension, "");
            double distance = 0;
            string creixellAnnot = "";
            int pepSeqId = 0;
            int pepSeqDist = 0;
            string pepAnnot = "";
            string creixellAnnotLines = "";
            string dataLine = "";
            StreamWriter contactWriter = new StreamWriter(contactFile);
            foreach (AtomInfo pepAtom in interfaceChains.chain2)
            {
                pepSeqId = Convert.ToInt32(pepAtom.seqId);
                if (pepSeqId <= phosphoSite.seqId + pepSubDistance && pepSeqId >= phosphoSite.seqId - pepSubDistance)
                {
                    foreach (AtomInfo enzymeAtom in interfaceChains.chain1)
                    {
                        distance = CalculateDistance(pepAtom.xyz, enzymeAtom.xyz);
                        if (distance < distCutoff)
                        {
                            pepSeqDist = pepSeqId - phosphoSite.seqId;
                            if (pepSeqDist == 0)
                            {
                                pepAnnot = phosphoSite.name + " 0";
                            }
                            else if (pepSeqDist > 0)
                            {
                                pepAnnot = phosphoSite.name + "+" + pepSeqDist.ToString();
                            }
                            else
                            {
                                pepAnnot = phosphoSite.name + pepSeqDist.ToString();
                            }
                            creixellAnnot = AddCreixellAnnotations(Convert.ToInt32(enzymeAtom.seqId), motifNumHash);
                            dataLine = pepAtom.seqId + "\t" + pepAtom.residue + "\t" + pepAtom.atomName + "\t" + pepAtom.atomId + "\t" +
                                enzymeAtom.seqId + "\t" + enzymeAtom.residue + "\t" + enzymeAtom.atomName + "\t" + enzymeAtom.atomId + "\t" +
                                distance.ToString() + "\t" + pepAnnot + "\t" + creixellAnnot;
                            contactWriter.WriteLine(dataLine);
                            if (creixellAnnot != "")
                            {
                                creixellAnnotLines += (complexName + "\t" + dataLine + "\n");
                            }
                        }
                    }
                }
            }
            contactWriter.Close();
            return creixellAnnotLines.TrimEnd('\n');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="motifAnnotationHash"></param>
        /// <returns></returns>
        private string AddCreixellAnnotations(int seqId, Hashtable motifAnnotationHash)
        {
            int motifCutoff = 0;
            foreach (string motif in motifAnnotationHash.Keys)
            {
                if (motif == "HRD" || motif == "DFG")
                {
                    motifCutoff = 5;
                }
                else if (motif == "APE")
                {
                    motifCutoff = 15;
                }
                int[] motifNumbers = (int[])motifAnnotationHash[motif];
                if (Array.IndexOf(motifNumbers, seqId) > -1)
                {
                    return motif + " 0";
                }
                else
                {
                    if (seqId >= motifNumbers[0] - motifCutoff && seqId < motifNumbers[0])
                    {
                        return motif + "-" + (motifNumbers[0] - seqId).ToString();
                    }
                    else if (seqId > motifNumbers[2] && seqId <= motifNumbers[2] + motifCutoff)
                    {
                        return motif + "+" + (seqId - motifNumbers[2]).ToString();
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom1"></param>
        /// <param name="atom2"></param>
        /// <returns></returns>
        private double CalculateDistance(Coordinate atom1, Coordinate atom2)
        {
            double distance = Math.Sqrt(Math.Pow(atom1.X - atom2.X, 2) + Math.Pow(atom1.Y - atom2.Y, 2) + Math.Pow(atom1.Z - atom2.Z, 2));
            return distance;
        }

        /// <summary>
        /// read the interface file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="theInterface"></param>
        /// <param name="atomType"></param>
        public string ReadInterfaceChainsFromFile(string fileName, ref InterfaceChains theInterface)
        {
            ArrayList chainAtomList = new ArrayList();
            if (!File.Exists(fileName))
            {
                return "";
            }
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
            string currentChain = "";
            string preChain = "";
            string remarkString = "";
            bool atomStart = false;

            try
            {
                while ((line = interfaceReader.ReadLine()) != null)
                {
                    // only read protein chains
                    if (line.Length > 6 && (line.ToUpper().Substring(0, 6) == "ATOM  "))
                    {
                        atomStart = true;
                        AtomInfo atom = new AtomInfo();
                        string[] fields = ParseHelper.ParsePdbAtomLine(line);
                        if (fields[4] == "HOH")
                        {
                            continue;
                        }
                        currentChain = fields[5];
                        // the end of one chain
                        if (currentChain != preChain && preChain != "" && preChain != " ")
                        {
                            // ignore the latter chain id with same chain names
                            AtomInfo[] chain1 = new AtomInfo[chainAtomList.Count];
                            chainAtomList.CopyTo(chain1);
                            theInterface.chain1 = chain1;
                            chainAtomList = new ArrayList();
                            preChain = currentChain;
                        }
                        atom.atomName = fields[2];
                        atom.atomId = Convert.ToInt32(fields[1]);
                        atom.residue = fields[4];
                        if (fields[7] != " ")
                        {
                            atom.seqId = fields[6] + fields[7];
                        }
                        else
                        {
                            atom.seqId = fields[6];
                        }
                        try
                        {
                            atom.xyz = new Coordinate(Convert.ToDouble(fields[8]), Convert.ToDouble(fields[9]),
                                Convert.ToDouble(fields[10]));
                            chainAtomList.Add(atom);
                        }
                        catch { }
                    }
                    if (!atomStart)
                    {
                        remarkString += (line + "\r\n");
                    }
                    preChain = currentChain;
                }
                AtomInfo[] chain2 = new AtomInfo[chainAtomList.Count];
                chainAtomList.CopyTo(chain2);
                theInterface.chain2 = chain2;
            }
            catch (Exception ex)
            {
                throw new Exception("Parsing " + fileName + " Errors: " + ex.Message);
            }
            finally
            {
                interfaceReader.Close();
            }
            remarkString = remarkString.TrimEnd("\r\n".ToCharArray());
            return remarkString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Hashtable[] SetUpPeptideMotifRegions(string complexAutoPhosphPositionFile, int distance)
        {
            StreamReader dataReader = new StreamReader(complexAutoPhosphPositionFile);
            Hashtable complexPeptideRegionHash = new Hashtable();
            Hashtable complexPhosphoSiteHash = new Hashtable();
            Hashtable creixellNumHash = new Hashtable();
            string line = "";
            //       string autoPhosPosString = "";
            //        int autoPhosPosition = 0;
            int HRD_D = 0;
            int HRD_R = 0;
            int HRD_H = 0;
            int APE_E = 0;
            int APE_P = 0;
            int APE_A = 0;
            int DFG_G = 0;
            int DFG_F = 0;
            int DFG_D = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                //       autoPhosPosString = "";
                string[] lineFields = line.Split('\t');
                PhosphoSite phosSite = new PhosphoSite();
                phosSite.name = lineFields[2].Substring(0, 1);
                phosSite.seqId = Convert.ToInt32(lineFields[2].Substring(1, lineFields[2].Length - 1));
                complexPeptideRegionHash.Add(lineFields[0], phosSite);
                /*     foreach (char ch in lineFields[2])
                     {
                         if (char.IsDigit(ch))
                         {
                             autoPhosPosString += ch.ToString();
                         }
                     }
                     if (autoPhosPosString == "")
                     {
                         complexPeptideRegionHash.Add(lineFields[0], null);
                     }
                     else
                     {
                         autoPhosPosition = Convert.ToInt32(autoPhosPosString);
                         int[] peptideRegion = new int[2];
                         peptideRegion[0] = autoPhosPosition - distance;
                         peptideRegion[1] = autoPhosPosition + distance;
                         complexPeptideRegionHash.Add(lineFields[0], peptideRegion);
                     }
                     */

                Hashtable motifNumMap = new Hashtable();
                HRD_D = Convert.ToInt32(lineFields[1].Substring(1, lineFields[1].Length - 1));
                HRD_R = HRD_D - 1;
                HRD_H = HRD_D - 2;
                int[] hrdNumbers = new int[3];
                hrdNumbers[0] = HRD_H;
                hrdNumbers[1] = HRD_R;
                hrdNumbers[2] = HRD_D;
                motifNumMap.Add("HRD", hrdNumbers);

                APE_E = Convert.ToInt32(lineFields[4].Substring(1, lineFields[4].Length - 1));
                APE_P = APE_E - 1;
                APE_A = APE_E - 2;
                int[] apeNumbers = new int[3];
                apeNumbers[0] = APE_A;
                apeNumbers[1] = APE_P;
                apeNumbers[2] = APE_E;
                motifNumMap.Add("APE", apeNumbers);

                DFG_G = Convert.ToInt32(lineFields[5].Substring(1, lineFields[5].Length - 1));
                DFG_F = DFG_G - 1;
                DFG_D = DFG_G - 2;
                int[] dfgNumbers = new int[3];
                dfgNumbers[0] = DFG_D;
                dfgNumbers[1] = DFG_F;
                dfgNumbers[2] = DFG_G;
                motifNumMap.Add("DFG", dfgNumbers);

                creixellNumHash.Add(lineFields[0], motifNumMap);
            }
            dataReader.Close();
            Hashtable[] complexInfoHashs = new Hashtable[2];
            complexInfoHashs[0] = complexPeptideRegionHash;
            complexInfoHashs[1] = creixellNumHash;
            return complexInfoHashs;
        }
        #endregion

        #region autophos pdb and unp alignments
        public void PrintKinasePdbUnpSeqAlignments()
        {
            Initialize(true);
            StreamReader dataReader = new StreamReader(@"C:\Paper\Kinase\dataset\kinaseautotable.txt");
            StreamWriter dataWriter = new StreamWriter(@"C:\Paper\Kinase\dataset\autoPdbUnpAlignments.txt");
            string line = "";
            string pdbId = "";
            string authorChain = "";
            string unpCode = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                unpCode = fields[0];
                pdbId = fields[1].Substring(0, 4).ToLower();
                authorChain = fields[1].Substring(4, fields[1].Length - 4);
                GetPdbUnpAlignments(pdbId, authorChain, unpCode, dataWriter);
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <param name="unpCode"></param>
        /// <param name="dataWriter"></param>
        private void GetPdbUnpAlignments(string pdbId, string authorChain, string unpCode, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select PdbDbRefSeqAlignSifts.PdbID, AuthorChain, Sequence, DbSequence, AuthorSeqNumbers, DbSeqNumbers From PdbDbRefSeqAlignSifts, PdbDbRefSifts " +
                " Where PdbDbRefSeqAlignSifts.PdbID = '{0}' AND AuthorChain = '{1}' AND DbCode = '{2}' AND " +
                " PdbDbRefSeqAlignSifts.PdbID = PdbDbRefSifts.PdbID AND PdbDbRefSeqAlignSifts.RefID = PdbDbRefSifts.RefID;", pdbId, authorChain, unpCode);
            DataTable pdbUnpAlignTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string headerLine = "";
            string pdbSeqStartEnd = "";
            string unpSeqStartEnd = "";
            string difResiduePairs = "";
            foreach (DataRow alignRow in pdbUnpAlignTable.Rows)
            {
                pdbSeqStartEnd = GetStartEndPositions(alignRow["AuthorSeqNumbers"].ToString());
                unpSeqStartEnd = GetStartEndPositions(alignRow["DbSeqNumbers"].ToString());
                difResiduePairs = GetDifResiduePairs(alignRow["Sequence"].ToString(), alignRow["DbSequence"].ToString(),
                    alignRow["AuthorSeqNumbers"].ToString(), alignRow["DbSeqNumbers"].ToString());
                headerLine = pdbId + authorChain + " | " + unpCode + " PDB:" + pdbSeqStartEnd + " UNP:" + unpSeqStartEnd + " " + difResiduePairs;
                dataWriter.WriteLine(">" + headerLine);
                dataWriter.WriteLine(alignRow["Sequence"].ToString());
                dataWriter.WriteLine(alignRow["DbSequence"].ToString());
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="dbSequence"></param>
        /// <param name="seqNumberString"></param>
        /// <param name="dbNumberString"></param>
        /// <returns></returns>
        private string GetDifResiduePairs(string sequence, string dbSequence, string seqNumberString, string dbNumberString)
        {
            string[] seqNumbers = seqNumberString.Split(',');
            string[] dbSeqNumbers = seqNumberString.Split(',');
            string difResiduePairs = "";
            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] != dbSequence[i])
                {
                    difResiduePairs += sequence[i].ToString() + "(" + seqNumbers[i] + "):" + dbSequence[i].ToString() + "(" + dbSeqNumbers[i] + ") ";
                }
            }
            return difResiduePairs.TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqNumberString"></param>
        /// <returns></returns>
        private string GetStartEndPositions(string seqNumberString)
        {
            string[] seqNumbers = seqNumberString.Split(',');
            string seqStartEnd = "[" + seqNumbers[0] + "-" + seqNumbers[seqNumbers.Length - 1] + "]";
            return seqStartEnd;
        }
        #endregion

        #region download kinase entries
        public const string PdbWebServer = "http://www.rcsb.org/pdb/files/";
        public void DonwloadNewKinaseEntries(string newKinaseListFile)
        {
            WebClient fileDownloadClient = new WebClient();

            StreamReader dataReader = new StreamReader(newKinaseListFile);
            string line = "";
            string pdbId = "";
            string webXmlFile = "";
           
            string localXmlFile = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                pdbId = line;
                webXmlFile = PdbWebServer + pdbId + ".xml.gz";
                localXmlFile = Path.Combine(localXmlDir, pdbId + ".xml.gz");

                fileDownloadClient.DownloadFile(webXmlFile, localXmlFile);
            }
            dataReader.Close();
        }

        #endregion

        #region initialize
        /// <summary>
        /// initialize dbconnect
        /// </summary>
        private void Initialize(bool isUpdate)
        {
            ProtCidSettings.LoadDirSettings();
            AppSettings.LoadParameters();
            AppSettings.LoadSymOps();

            ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=X:\\Firebird\\Pfam31\\PDBFAM.FDB");
            ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=X:\\Firebird\\Pfam31\\ProtCid.FDB");
            PfamLibSettings.pdbfamConnection = ProtCidSettings.pdbfamDbConnection;
            ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);

            dataDir = @"D:\Qifang\ProjectData\Pkinase";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            pfamInterfaceFileDir = Path.Combine(dataDir, "interfaceFiles");
            if (!Directory.Exists(pfamInterfaceFileDir))
            {
                Directory.CreateDirectory(pfamInterfaceFileDir);
            }

            homoDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "homodimers");
            if (isUpdate)
            {
                homoDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "homodimers_update");
            }
            if (!Directory.Exists(homoDimerInterfaceFileDir))
            {
                Directory.CreateDirectory(homoDimerInterfaceFileDir);
            }
            heteroDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "heterodimers");
            if (isUpdate)
            {
                heteroDimerInterfaceFileDir = Path.Combine(pfamInterfaceFileDir, "heterodimers_update");
            }
            if (!Directory.Exists(heteroDimerInterfaceFileDir))
            {
                Directory.CreateDirectory(heteroDimerInterfaceFileDir);
            }

            localXmlDir = Path.Combine(dataDir, "pdb");
            if (! Directory.Exists (localXmlDir))
            {
                Directory.CreateDirectory(localXmlDir);
            }

            ProtCidSettings.tempDir = "C:\\temp_pkinase";
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
        }
        #endregion
    }
}
