using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;


namespace BuCompLib.PfamInteract
{
    public class ChainLigands
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public DbInsert dbInsert = new DbInsert();
        public DbUpdate dbUpdate = new DbUpdate();
        public double contactCutoff = 4.5;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveChainLigandInteractions ()
        {
            bool isUpdate = false;
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            DataTable chainLigandTable = InitializeTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Chain Ligands";

      //      string queryString = "Select PdbID From PdbEntry Where NumOfLigandAtoms > 0;";  missing some DNA/RNA entries, like 4e68
            string queryString = "Select PdbID From PdbEntry;";
            DataTable ligandEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            ProtCidSettings.progressInfo.totalOperationNum = ligandEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = ligandEntryTable.Rows.Count;

            string pdbId = ""; // pdbid = 2qa4, 26613
            foreach (DataRow entryRow in ligandEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    RetrieveLigandChainInteractions (pdbId, chainLigandTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " retrieving ligand and chain interactions error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " retrieving ligand and chain interactions error: " + ex.Message);
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateChainLigandInteractions(string[] updateEntries)
        {
            bool isUpdate = true;
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            DataTable chainLigandTable = InitializeTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Chain Ligands";

            BuCompBuilder.logWriter.WriteLine("Update chain-ligand interactions.");
            BuCompBuilder.logWriter.WriteLine("#entries=" + updateEntries.Length);
            BuCompBuilder.logWriter.Flush();

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    DeleteChainLigand(pdbId);

                    RetrieveLigandChainInteractions(pdbId, chainLigandTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " retrieving ligand and chain interactions error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " retrieving ligand and chain interactions error: " + ex.Message);
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            BuCompBuilder.logWriter.WriteLine("Done!");
            BuCompBuilder.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteChainLigand(string pdbId)
        {
            string deleteString = string.Format("Delete From ChainLigands Where PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.buCompConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamLigandTable"></param>
        private void RetrieveLigandChainInteractions(string pdbId, DataTable chainLigandTable)
        {
            string gzCoordXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            if (! File.Exists(gzCoordXmlFile))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ".xml.gz file not exist" );
                return;
            }
            string coordXmlFile = ParseHelper.UnZipFile(gzCoordXmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            EntryCrystal thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();
            File.Delete(coordXmlFile);

            // no coordinates for waters
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            string[][] chainLigandNames = GetChainLigandsNames(chains);
            string[] protChainNames = chainLigandNames[0]; // the list of protein chains
            string[] ligandChainNames = chainLigandNames[1];  // the list of ligands, no DNA/RNA
            if (ligandChainNames.Length == 0)
            {
                return;
            }
            Dictionary<string, double> seqPairDistDict = new Dictionary<string, double>();
            foreach (string ligandChainName in ligandChainNames)
            {
                AtomInfo[] ligandAtoms = GetChainAtoms(chains, ligandChainName);
                foreach (string protChainName in protChainNames)
                {
                    AtomInfo[] protChainAtoms = GetChainAtoms(chains, protChainName);

                    Dictionary<string, AtomInfo[]> seqPairContactHash = CalculateAtomDistances(ligandAtoms, protChainAtoms, out seqPairDistDict);
                    foreach (string seqPair in seqPairDistDict.Keys)
                    {
                        string[] seqIds = seqPair.Split('_');
                        double distance = seqPairDistDict[seqPair];
                        AtomInfo[] atomPair = seqPairContactHash[seqPair];
                        DataRow chainLigandRow = chainLigandTable.NewRow();
                        chainLigandRow["PdbID"] = pdbId;
                        chainLigandRow["AsymID"] = ligandChainName;
                    //    chainLigandRow["Ligand"] = ligandChainName;
                        chainLigandRow["ChainAsymID"] = protChainName;
                        chainLigandRow["SeqID"] = seqIds[0];
                        chainLigandRow["ChainSeqID"] = seqIds[1];
                        chainLigandRow["Distance"] = distance;
                        chainLigandRow["Atom"] = atomPair[0].atomName;
                        chainLigandRow["ChainAtom"] = atomPair[1].atomName;
                        chainLigandRow["Residue"] = atomPair[0].residue;
                        chainLigandRow["ChainResidue"] = atomPair[1].residue;
                        chainLigandTable.Rows.Add(chainLigandRow);
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, chainLigandTable);
            chainLigandTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protChainName"></param>
        /// <param name="ligandName"></param>
        /// <param name="protChainAtoms"></param>
        /// <param name="ligandAtoms"></param>
        public Dictionary<string, AtomInfo[]> CalculateAtomDistances(AtomInfo[] ligandAtoms, AtomInfo[] protChainAtoms, out Dictionary<string, double> seqPairDistanceHash)
        {
            seqPairDistanceHash = new Dictionary<string,double> ();
            Dictionary<string, AtomInfo[]> seqPairAtomInfoHash = new Dictionary<string, AtomInfo[]>();
            string seqPair = "";
            double distance = 0;
            foreach (AtomInfo ligandAtom in ligandAtoms)
            {
                foreach (AtomInfo protAtom in protChainAtoms)
                {
                    seqPair = ligandAtom.seqId + "_" + protAtom.seqId  ;
                    distance = GetDistance(ligandAtom.xyz, protAtom.xyz);
                    if (distance > contactCutoff)
                    {
                        continue;
                    }
                    
                    if (seqPairDistanceHash.ContainsKey(seqPair))
                    {
                        double lsDistance = (double)seqPairDistanceHash[seqPair];
                        if (distance < lsDistance)
                        {
                            seqPairDistanceHash[seqPair] = distance;
                            AtomInfo[] atomPairs = new AtomInfo[2];
                            atomPairs[0] = ligandAtom;
                            atomPairs[1] = protAtom;
                            seqPairAtomInfoHash[seqPair] = atomPairs;
                        }
                    }
                    else
                    {
                        AtomInfo[] atomPairs = new AtomInfo[2];
                        atomPairs[0] = ligandAtom;
                        atomPairs[1] = protAtom;
                        seqPairAtomInfoHash.Add(seqPair, atomPairs);
                        seqPairDistanceHash.Add(seqPair, distance);
                    }
                }
            }
            return seqPairAtomInfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coord1"></param>
        /// <param name="coord2"></param>
        /// <returns></returns>
        private double GetDistance(Coordinate coord1, Coordinate coord2)
        {
            double squareSum = Math.Pow(coord1.X - coord2.X, 2) + Math.Pow(coord1.Y - coord2.Y, 2) +
                Math.Pow(coord1.Z - coord2.Z, 2);
            double distance = Math.Sqrt(squareSum);
            return distance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public AtomInfo[] GetChainAtoms (ChainAtoms[] chains, string asymChain)
        {
            AtomInfo[] chainAtoms = null;
            foreach (ChainAtoms chain in chains)
            {
                if (chain.AsymChain == asymChain)
                {
                    chainAtoms = chain.CartnAtoms;
                }
            }
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <returns></returns>
        private string[][] GetChainLigandsNames(ChainAtoms[] chains)
        {
            List<string> protChainNameList = new List<string> ();
            List<string> ligandNameList = new List<string> ();
            List<string> dnaRnaNameList = new List<string> ();
            foreach (ChainAtoms chain in chains)
            {
                if (chain.PolymerType == "polypeptide")
                {
                    protChainNameList.Add(chain.AsymChain);
                }
                else if (chain.PolymerType == "polydeoxyribonucleotide" || chain.PolymerType == "polyribonucleotide")
                {
                    dnaRnaNameList.Add(chain.AsymChain);
                }
                else   
                {
                    ligandNameList.Add(chain.AsymChain);
                }
            }
            string[][] chainLigandNames = new string[2][];
            protChainNameList.Sort();
            chainLigandNames[0] = new string[protChainNameList.Count];
            protChainNameList.CopyTo(chainLigandNames[0]);

            chainLigandNames[1] = new string[ligandNameList.Count];
            ligandNameList.Sort();
            ligandNameList.CopyTo(chainLigandNames[1]);

            return chainLigandNames;
        }
        
        /// <summary>
        /// 
        /// </summary>
        private DataTable InitializeTable(bool isUpdate)
        {
            string[] pfamLigandColumns = {"PdbID", "AsymID", "ChainAsymID", "SeqID", "ChainSeqID", "Distance", 
                                             "Atom", "ChainAtom", "Residue", "ChainResidue"};
            DataTable pfamLigandTable = new DataTable("ChainLigands");
            foreach (string ligandCol in pfamLigandColumns)
            {
                pfamLigandTable.Columns.Add(new DataColumn (ligandCol));
            }
            if (! isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pfamLigandTable.TableName + " (" +
                    " PdbID CHAR(4) NOT NULL, " +
                    " AsymID CHAR(3) NOT NULL, " +
                //    " Ligand CHAR(5) NOT NULL, " +
                    " ChainAsymID CHAR(3), " +
                    " SeqID INTEGER NOT NULL, " +
                    " ChainSeqID INTEGER NOT NULL, " +
                    " ATOM CHAR(4) NOT NULL, " +
                    " ChainATom CHAR(4), " + 
                    " Residue CHAR(3), " +
                    " ChainResidue CHAR(3), " +
                    " Distance FLOAT" +
                " );";
                dbCreate.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, pfamLigandTable.TableName);
                string createIndexString = "CREATE INDEX " + pfamLigandTable.TableName + "_idx1 ON " + pfamLigandTable.TableName + "(PdbID, AsymID)";
                dbCreate.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, pfamLigandTable.TableName);
            }
            return pfamLigandTable;
        }

     
    }
}