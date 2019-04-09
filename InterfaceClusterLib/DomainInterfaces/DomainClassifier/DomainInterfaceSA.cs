using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Xml.Serialization;
using DbLib;
using CrystalInterfaceLib.DomainInterfaces;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.ProtInterfaces;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.BuIO;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainInterfaceSA
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private InterfaceAsa interfaceAsa = new InterfaceAsa();
        private InterfaceReader interfaceReader = new InterfaceReader();
        private InterfaceWriter interfaceWriter = new InterfaceWriter();
        private string saFileDir = ProtCidSettings.dirSettings.interfaceFilePath + "\\SaFiles"; // hash by the middle two letters in the PDB ID
  //      private string saFileHashFolder = "";
        private string domainInterfaceFileDir = ProtCidSettings.dirSettings.interfaceFilePath + "\\pfamDomain"; //hash
        private string domainFileDir = ProtCidSettings.dirSettings.pfamPath + "\\domainFiles"; // hash
        private string chainInterfaceFileDir = ProtCidSettings.dirSettings.interfaceFilePath + "\\cryst"; // hash
        public string domainInterfaceTableName = "PfamDomainInterfaces";
        #endregion

        #region calculate SAs from domains
        /// <summary>
        /// calculate surface area from domain interface files
        /// which are already generated
        /// </summary>
        public void CalculateDomainInterfaceSAs()
        {
            if (! Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface SA";

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString ());

            string queryString = string.Format("Select Distinct PdbID From {0} Where SurfaceArea < 0;", domainInterfaceTableName);
            DataTable noSaEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating surface area of domain interfaces");
            ProtCidSettings.progressInfo.totalOperationNum = noSaEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = noSaEntryTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in noSaEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    CalculateEntryDomainInterfaceSAs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateDomainInterfaceSAs(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface SA";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating surface area of domain interfaces from chain interfaces");
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    CalculateEntryDomainInterfaceSAs (pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        #endregion

        #region entry ASA
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void CalculateEntryDomainInterfaceSAs (string pdbId)
        {
            Dictionary<string, double> domainChainAsaHash = new Dictionary<string,double> ();
            string queryString = string.Format("Select DomainInterfaceID From {0} " + 
                " Where PdbID = '{1}' AND SurfaceArea = -1.0;", domainInterfaceTableName, pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int domainInterfaceId = 0;
            string gzDomainInterfaceFile = "";
            string domainInterfaceFile = "";
            string hashFolder = Path.Combine(domainInterfaceFileDir, pdbId.Substring(1, 2));
            double interfaceBsa = 0;
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString ());
                gzDomainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
                if (!File.Exists(gzDomainInterfaceFile))
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + ": domain interface file not exit." );
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                try
                {
                    domainInterfaceFile = ParseHelper.UnZipFile(gzDomainInterfaceFile, ProtCidSettings.tempDir);
                    interfaceBsa = CalculateDomainInterfaceSurfaceArea(domainInterfaceFile, ref domainChainAsaHash);
                    UpdateDomainInterfaceSaInDb(pdbId, domainInterfaceId, interfaceBsa);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString() + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                finally
                {
                    File.Delete(domainInterfaceFile);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceFile"></param>
        /// <returns></returns>
        public double CalculateDomainInterfaceSurfaceArea(string domainInterfaceFile, ref Dictionary<string, double> domainChainAsaHash)
        {
            FileInfo fileInfo = new FileInfo (domainInterfaceFile);
            InterfaceChains domainInterface = new InterfaceChains ();
            string remark = interfaceReader.ReadInterfaceChainsFromFile(domainInterfaceFile, ref domainInterface);
            if (domainInterface.chain1.Length == 0 || domainInterface.chain2.Length == 0)
            {
                throw new Exception("no interface atoms.");
            }
            string[] domainChainStrings = interfaceReader.GetDomainChainStrings(remark);

            double chainASA = 0;
            double chainBSA = 0;
            if (domainChainAsaHash.ContainsKey(domainChainStrings[0]))
            {
                chainASA = (double)domainChainAsaHash[domainChainStrings[0]];
            }
            else
            {
                string domainChainAFile = domainInterfaceFile.Replace(".cryst", "A.cryst");
                interfaceWriter.WriterOneInterfaceChainToFile(domainChainAFile, domainInterface.chain1, "A");
                chainASA = interfaceAsa.ComputeInterfaceSurfaceArea(domainChainAFile);
                domainChainAsaHash.Add(domainChainStrings[0], chainASA);
            }
            if (domainChainAsaHash.ContainsKey(domainChainStrings[1]))
            {
                chainBSA = (double)domainChainAsaHash[domainChainStrings[1]];
            }
            else
            {
                string domainChainBFile = domainInterfaceFile.Replace(".cryst", "B.cryst");
                interfaceWriter.WriterOneInterfaceChainToFile(domainChainBFile, domainInterface.chain2, "B");
                chainBSA = interfaceAsa.ComputeInterfaceSurfaceArea(domainChainBFile);
                domainChainAsaHash.Add(domainChainStrings[1], chainBSA);
            }
            double interfaceSA = interfaceAsa.ComputeInterfaceSurfaceArea(domainInterfaceFile);

            double interfaceBSA = -1;
            if (chainASA > 0 && chainBSA > 0 && interfaceSA > 0)
            {
                interfaceBSA = (chainASA + chainBSA - interfaceSA) / 2;
            }
            return interfaceBSA;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void CalculateEntrySAFiles(string pdbId)
        {
            string saOutputFileDir = ProtCidSettings.tempDir;

            long[] domainIds = null;
            string[] domainFiles = GetEntryDomainFiles(pdbId, out domainIds);
            string[] domainSaFiles = GenerateSaFiles (domainFiles);
            Dictionary<long, double> domainAsaHash = GetDomainAsaHash(pdbId, domainIds, saOutputFileDir);

            string[] domainInterfaceFiles = GetEntryDomainInterfaceFiles(pdbId);
            string[] interfaceSaFiles = GenerateSaFiles(domainInterfaceFiles);

            int domainInterfaceId = 0;
            string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", domainInterfaceTableName, pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            double interfaceAsa = 0;
            double interfaceBsa = 0;
            string rsaFile = "";
            double domainAsa1 = 0;
            double domainAsa2 = 0;
            foreach (string domainInterfaceFile in domainInterfaceFiles)
            {
                domainInterfaceId = GetDomainInterfaceIdFromFileName(domainInterfaceFile);
                rsaFile = Path.Combine(saOutputFileDir, pdbId + "_d" + domainInterfaceId.ToString() + ".rsa");
                interfaceAsa = GetTotalAbsoluteAsa(rsaFile);
                long[] domainPair = GetInterfaceDomainPair(domainInterfaceId, domainInterfaceTable);
                domainAsa1 = -1;
                domainAsa2 = -1;
                if (domainAsaHash.ContainsKey(domainPair[0]))
                {
                    domainAsa1 = (double)domainAsaHash[domainPair[0]];
                }
                if (domainPair[0] == domainPair[1])
                {
                    domainAsa2 = domainAsa1;
                }
                else
                {
                    if (domainAsaHash.ContainsKey(domainPair[1]))
                    {
                        domainAsa2 = (double)domainAsaHash[domainPair[1]];
                    }
                }
                if (domainAsa1 > -1 && domainAsa2 > -1 && interfaceAsa > -1)
                {
                    interfaceBsa = (domainAsa1 + domainAsa2 - interfaceAsa) / 2;
                }
                else
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString () + " ASA: " + domainAsa1.ToString () + 
                        "   " + domainAsa2.ToString () + " " + interfaceAsa.ToString ());
                    ProtCidSettings.logWriter.Flush();
                    interfaceBsa = -1;
                }
                if (interfaceBsa > -1)
                {
                    UpdateDomainInterfaceSaInDb(pdbId, domainInterfaceId, interfaceBsa);
                }
            }
            // delete the NACCESS output files
            DeleteSaOutputFiles(domainSaFiles);
            DeleteSaOutputFiles(interfaceSaFiles);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceId"></param>
        /// <param name="entryDomainInterfaceTable"></param>
        /// <returns></returns>
        private long[] GetInterfaceDomainPair(int domainInterfaceId, DataTable entryDomainInterfaceTable)
        {
            DataRow[] domainPairRows = entryDomainInterfaceTable.Select(string.Format ("DomainInterfaceID = '{0}'", domainInterfaceId));
            long[] domainPair = new long[2];
            domainPair[0] = Convert.ToInt64(domainPairRows[0]["DomainID1"].ToString());
            domainPair[1] = Convert.ToInt64(domainPairRows[1]["DomainID2"].ToString ());
            return domainPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFileName"></param>
        /// <returns></returns>
        private int GetDomainInterfaceIdFromFileName(string interfaceFileName)
        {
            int exeIndex = interfaceFileName.IndexOf(".");
            int dashIndex = interfaceFileName.IndexOf("_d");
            string domainInterfaceIdString = interfaceFileName.Substring(dashIndex + "_d".Length, exeIndex - dashIndex - "_d".Length);
            int domainInterfaceId = Convert.ToInt32(domainInterfaceIdString);
            return domainInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFiles"></param>
        /// <returns></returns>
        private string[] GenerateSaFiles(string[] coordFiles)
        {
            List<string> saFileList = new List<string> ();
            foreach (string coordFile in coordFiles)
            {
                string[] saFiles = interfaceAsa.GenerateSurfaceAreaFile(coordFile);
                saFileList.AddRange(saFiles);
            }

            return saFileList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="saOutputFiles"></param>
        private void DeleteSaOutputFiles(string[] saOutputFiles)
        {
            foreach (string outputFile in saOutputFiles)
            {
                File.Delete(outputFile);
            }
        }
        #endregion

        #region  ASA files
        /// <summary>
        /// the domain coordinate files are generated in PDBfam, for the structure alignments
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryDomainFiles(string pdbId, out long[] domainIds)
        {
            string domainFileHashFolder = Path.Combine(domainFileDir, pdbId.Substring(1, 2));
            string queryString = string.Format("Select Distinct DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] domainFiles = new string[domainTable.Rows.Count];
            int count = 0;
            domainIds = new long[domainTable.Rows.Count];
            foreach (DataRow domainRow in domainTable.Rows)
            {
                domainIds[count] = Convert.ToInt64(domainRow["DomainID"].ToString());
                domainFiles[count] = Path.Combine (domainFileHashFolder, pdbId + domainRow["DomainID"].ToString() + ".pfam.gz");
                count++;
            }
            return domainFiles;
        }
     
        /// <summary>
        /// the domain interface files are generate in DomainInterfaceWriter
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryDomainInterfaceFiles(string pdbId)
        {
            string hashFolder = Path.Combine(domainInterfaceFileDir, pdbId.Substring (1, 2));
            string[] domainInterfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
            return domainInterfaceFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainIds"></param>
        /// <param name="saOutputFileDir"></param>
        /// <returns></returns>
        private Dictionary<long, double> GetDomainAsaHash(string pdbId, long[] domainIds, string saOutputFileDir)
        {
            Dictionary<long, double> domainAsaHash = new Dictionary<long,double> ();
            string rsaFile = "";
            double domainAsa = 0;
            foreach (long domainId in domainIds)
            {
                rsaFile = Path.Combine(saOutputFileDir, pdbId + domainId.ToString() + ".rsa");
                if (File.Exists(rsaFile))
                {
                    domainAsa = GetTotalAbsoluteAsa(rsaFile);
                }
                else
                {
                    domainAsa = -1;
                }
                domainAsaHash.Add(domainId, domainAsa);
            }
            return domainAsaHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rsaFile"></param>
        /// <param name="ranges"></param>
        /// <returns></returns>
        public double GetTotalAbsoluteAsa (string rsaFile)
        {
            StreamReader dataReader = new StreamReader(rsaFile);
            string line = "";
            double absSaSum = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("TOTAL") > -1)
                {
                    string[] fields = ParseHelper.SplitPlus(line, ' ');
                    absSaSum = Convert.ToDouble(fields[1]);
                }
            }
            dataReader.Close();
            return absSaSum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="surfaceArea"></param>
        private void UpdateDomainInterfaceSaInDb(string pdbId, int domainInterfaceId, double surfaceArea)
        {
            string updateString = string.Format("Update {0} Set SurfaceArea = {1} Where PdbID = '{2}' AND DomainInterfaceID = {3};", 
                                domainInterfaceTableName,  surfaceArea, pdbId, domainInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion

        #region for debug
        public void UpdateDomainInterfaceSAs()
        {
            Dictionary<string, int[]> updateEntryDomainInterfaceHash = GetUpdateDomainInterfaces();
        /*    Hashtable updateEntryDomainInterfaceHash = new Hashtable();
            int[] entryDomainInterfaceIds = {17, 19, 20 };
            updateEntryDomainInterfaceHash.Add("1wzx", entryDomainInterfaceIds);*/
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = updateEntryDomainInterfaceHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntryDomainInterfaceHash.Count;

            foreach (string pdbId in updateEntryDomainInterfaceHash.Keys)
            {
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                int[] domainInterfaceIds = (int[])updateEntryDomainInterfaceHash[pdbId];
                CalculateEntryDomainInterfaceSAs(pdbId, domainInterfaceIds);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, int[]> GetUpdateDomainInterfaces()
        {
            string queryString = "Select Distinct PdbID, DomainInterfaceID From PfamDomainInterfaces WHere SurfaceArea < 0 Or SurfaceArea >= 5000.0;";
            DataTable updateDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, List<int>> updateEntryDomainInterfaceListHash = new Dictionary<string,List<int>> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in updateDomainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                if (updateEntryDomainInterfaceListHash.ContainsKey(pdbId))
                {
                    updateEntryDomainInterfaceListHash[pdbId].Add(domainInterfaceId);
                }
                else
                {
                    List<int> domainInterfaceIdList = new List<int> ();
                    domainInterfaceIdList.Add(domainInterfaceId);
                    updateEntryDomainInterfaceListHash.Add(pdbId, domainInterfaceIdList);
                }
            }
            Dictionary<string, int[]> updateEntryDomainInterfaceHash = new Dictionary<string, int[]>();

            foreach (string entry in updateEntryDomainInterfaceListHash.Keys)
            {
                updateEntryDomainInterfaceHash.Add (entry, updateEntryDomainInterfaceListHash[entry].ToArray ());
            }
            return updateEntryDomainInterfaceHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void CalculateEntryDomainInterfaceSAs(string pdbId, int[] domainInterfaceIds)
        {
            Dictionary<string, double> domainChainAsaHash = new Dictionary<string, double> ();
            string gzDomainInterfaceFile = "";
            string domainInterfaceFile = "";
            string hashFolder = Path.Combine(domainInterfaceFileDir, pdbId.Substring(1, 2));
            double interfaceBsa = 0;
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                gzDomainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
                if (!File.Exists(gzDomainInterfaceFile))
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + ": domain interface file not exit.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                try
                {
                    domainInterfaceFile = ParseHelper.UnZipFile(gzDomainInterfaceFile, ProtCidSettings.tempDir);
                    interfaceBsa = CalculateDomainInterfaceSurfaceArea(domainInterfaceFile, ref domainChainAsaHash);
                    UpdateDomainInterfaceSaInDb(pdbId, domainInterfaceId, interfaceBsa);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + domainInterfaceId.ToString() + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + domainInterfaceId.ToString() + " calculate SA error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                finally
                {
                    File.Delete(domainInterfaceFile);
                }
            }
        }
        #endregion
    }
}
