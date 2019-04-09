using System;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using System.Collections.Generic;
using DbLib;
using AuxFuncLib;
using PfamLib.Settings;
using InterfaceClusterLib.DomainInterfaces.PfamPeptide;
using InterfaceClusterLib.DomainInterfaces.PfamLigand;
using InterfaceClusterLib.DomainInterfaces.PfamNetwork;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Summary description for DomainInterfaceBuilder.
	/// </summary>
	public class DomainInterfaceBuilder
    {
        #region member variables
        public static StreamWriter nonAlignDomainsWriter = null;
        private PfamPeptideInterfaceBuilder pepInterfaceBuilder = new PfamPeptideInterfaceBuilder();
        private PfamLigandInteractionBuilder ligandInteractBuilder = new PfamLigandInteractionBuilder();
        #endregion

        public DomainInterfaceBuilder()
		{
		}

        #region build
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stepNum"></param>
        public void BuildDomainInterfaces(int stepNum)
        {
            InitializeThread();

            DomainInterfaceTables.InitializeTables();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain-domain interactions.");

            /*    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Creating database tables.");
                DomainInterfaceTables.InitializeDbTables();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done.");
                */
            switch (stepNum)
            {
                case 1:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Detecting domain-domain interactions from cryst chain interfaces.");
                    DomainClassifier domainClassifier = new DomainClassifier();
                    domainClassifier.RetrieveDomainInterfaces();
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    //	goto case 1;
                    break;

                case 2:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files.");
                    DomainInterfaceWriter domainInterfaceWriter = new DomainInterfaceWriter();
                    domainInterfaceWriter.WriteDomainInterfaceFiles ();
                    //      domainInterfaceWriter.UpdateDomainInterfaceFiles();
                    //        domainInterfaceWriter.WriteMultiChainDomainInterfaces();
                    ProtCidSettings.progressInfo.currentOperationIndex++;

                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate SAs of domain interface files.");
                    DomainInterfaceSA interfaceSa = new DomainInterfaceSA();
                    //   interfaceSa.UpdateDomainInterfaceSAs();
                    interfaceSa.CalculateDomainInterfaceSAs();
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    //   goto case 2;
                    break;

                case 3:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing domain interfaces.");
                    PfamDomainInterfaceComp domainComp = new PfamDomainInterfaceComp();
                    domainComp.CompareDomainInterfaces();
                    /*   domainComp.SynchronizeDomainChainInterfaceComp();           
                        *  domainComp.CompareSpecificDomainInterfaces ();
                        *  domainComp.UpdateMultiChainDomainInterfaces();
                             */
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;

                case 4:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing entry domain interfaces.");
                    DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                        new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
                    //    int[] relSeqIds = {10515 };
                    //     domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(relSeqIds);
                    domainInterfaceComp.CompareCrystBuDomainInterfaces();
                    ProtCidSettings.progressInfo.currentOperationIndex++;
                    break;


                case 5:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster domain interfaces.");
                    DomainInterfaceCluster interfaceCluster = new DomainInterfaceCluster();
                    int[] relSeqIds = { 2 };
                    interfaceCluster.UpdateDomainInterfaceClusters(relSeqIds);
//                    interfaceCluster.ClusterDomainInterfaces();
                    // interfaceCluster.ClusterLeftRelations();
                    break;
                //    goto case 4;

                case 6:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Print Domain Interface Cluster Info.");
                    DomainClusterStat clusterStat = new DomainClusterStat();
                    int[] updateRelSeqIds = { 2 };
                    clusterStat.UpdateDomainClusterInfo(updateRelSeqIds);
 //                   clusterStat.PrintDomainClusterInfo();
        //            clusterStat.PrintDomainDbSumInfo("PfamDomain");
         //           clusterStat.AddNumCFsToIPfamInPdbMetaData();
                    

                    
                    //      clusterStat.PrintPartialDomainClusterInfo();
                    //      clusterStat.GetPfamDomainSumInfo();

                    /*                  DomainInterfaceStatInfo statInfo = new DomainInterfaceStatInfo();
                                //      statInfo.PrintPepInteractingHmmSites();
                                      //     statInfo.GetPfamPepInterfaceClusterInfo();
                                      //    statInfo.PrintPepLigandHmmSites ();
                                          statInfo.PrintPfamDomainRelationInfo();
                                      //    statInfo.GetGenAsymChainDomainInterfaces();*/
                    break;
                //    goto case 7;

                case 7:
                    PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
                    clusterFileCompress.CompressPfamClusterChainInterfaceFiles();
                    clusterFileCompress.RetrieveCrystInterfaceFilesNotInClusters(true);

                    DomainSeqFasta seqFasta = new DomainSeqFasta();
                    seqFasta.PrintClusterDomainSequences();

                    DomainInterfaceImageGen imageGen = new DomainInterfaceImageGen();
                    imageGen.GenerateDomainInterfaceImages();                   
                    
                    PfamRelNetwork pfamNetWriter = new PfamRelNetwork();
                    pfamNetWriter.GeneratePfamNetworkGraphmlFiles();
                    
                    // build the unp-unp interaction table based on pdb domain interfaces
                    UnpInteractionStr unpInteract = new UnpInteractionStr ();
                    unpInteract.BuildUnpInteractionNetTable();
                    break;

                case 8:  // about peptide interfaces
                    PfamPeptideInterfaces pepInterfaces = new PfamPeptideInterfaces();
                    pepInterfaceBuilder.BuildPfamPeptideInterfaces();
                    break;

                case 9:
          //          ligandInteractBuilder.AddClusterInfoToPfamDomainAlign();

                    ligandInteractBuilder.BuildPfamLigandInteractions();
                    break;

                default:
                    break;
            }
            ProtCidSettings.logWriter.Close();

            ProtCidSettings.progressInfo.threadFinished = true;
        }
        #endregion

        #region update
        /// <summary>
        /// update domain interfaces for same version of Pfam
        /// </summary>
        public void UpdateDomainInterfaces()
        {
            InitializeThread();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Updating Pfam domain interface clusters.");

            DomainInterfaceTables.InitializeTables();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam domain-domain interface info.");

            string[] updateEntries = GetUpdateEntries();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interface database");
            UpdateDomainInterfacesDb(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-Peptide interactions");
            PfamPepInterfaceWriter pepInterfaceWriter = new PfamPepInterfaceWriter();            
            pepInterfaceBuilder.UpdatePfamPeptideInteractions(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-ligand interactions");
            ligandInteractBuilder.UpdatePfamLigandInteractions(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update peptide-chain comparison");
            pepInterfaceBuilder.UpdatePfamPepChainComparison(updateEntries);

            ProtCidSettings.logWriter.Close();

            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfaces(int step)
        {
            InitializeThread();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Updating Pfam domain interface clusters.");

            DomainInterfaceTables.InitializeTables();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam domain-domain interface info.");

     //       string[] updateEntries = GetUpdateEntries();
            // for update peptide/ligands
    //        string[] updateEntries = GetOtherUpdateEntries(@"D:\Qifang\ProjectData\DbProjectData\PDB\updateEntries_ligandsDif.txt");
   //         string[] updateEntries = GetOtherUpdateEntries(@"D:\Qifang\ProjectData\DbProjectData\PDB\MissingChainPepEntries.txt");
   //         string[] updateEntries = GetMissingDomainInterfaceEntries();
            string[] updateEntries = {"2gs0" };

            switch (step)
            {
                case 1:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interface database");
                    UpdateDomainInterfacesDb(updateEntries);
                    /*               int[] updateRelSeqIds = { 10348 }; 
                                    PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
                                   clusterFileCompress.UpdateRelationClusterChainInterfaceFiles(updateRelSeqIds);
                                   string updateRelEntryFile = "UpdateRelationEntries.txt";
                                   UpdateDomainInterfacesDb(updateRelEntryFile);
                      //             
                      //             updateEntries = GetOtherUpdateEntries();
                                   updateEntries = new string[1];
                                   updateEntries[0] = "3mtj"; 
                                   DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                                           new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
                                   domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(updateEntries);
                                   DomainClusterStat clusterStat = new DomainClusterStat();
                                   clusterStat.UpdateBACompInfo();
                                   DomainSeqFasta seqFasta = new DomainSeqFasta();
                                   int[] updateRelSeqIds = { 2986};
                                   seqFasta.UpdateClusterDomainSequences(updateRelSeqIds);*/
                    break;

                case 2:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-Peptide interactions");
                 //   pepInterfaceBuilder.UpdatePfamPeptideInteractions(updateEntries);
                    pepInterfaceBuilder.UpdateSomePfamPeptideInteractionsDebug();
                    break;

                case 3:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-ligand interactions");
                    ligandInteractBuilder.UpdatePfamLigandInteractions(updateEntries);
                    /*   ligandInteractBuilder.DebugPfamLigandInteractions();
                         ligandInteractBuilder.UpdatePfamLigandInteractions(updateDnaEntries, updateEntries);
                         PfamDomainFileCompress domainCompress = new PfamDomainFileCompress();
                         domainCompress.CompressPfamDomainFiles ();*/
                    ProtCidSettings.logWriter.Flush();

                    break;

                case 4:
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update peptide-chain comparison");
                    pepInterfaceBuilder.UpdatePfamPepChainComparison(updateEntries);
                    break;

                default:
                    break;
            }
            ProtCidSettings.logWriter.Close();

            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfacesDb(string[] updateEntries)
        {
            Dictionary<int, string[]> updateRelEntryDict = new Dictionary<int, string[]>();
            string updateRelEntryFile = "UpdateRelationEntries_missing.txt";
            //  string updateRelEntryFile = "NoCompRepHomoEntriesRel1.txt";
            string line = "";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Detecting domain-domain interactions from cryst chain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Detecting domain-domain interactions from cryst chain interfaces.");
            DomainClassifier domainClassifier = new DomainClassifier();
            updateRelEntryDict = domainClassifier.UpdateDomainInterfaces(updateEntries);
            ProtCidSettings.progressInfo.currentOperationIndex++;

            if (updateRelEntryDict.Count == 0)
            {
                if (File.Exists(updateRelEntryFile))
                {
                    List<string> updateEntryList = new List<string>();

                    StreamReader dataReader = new StreamReader(updateRelEntryFile);
                    while ((line = dataReader.ReadLine()) != null)
                    {
                        string[] fields = line.Split(' ');
                        string[] entries = new string[fields.Length - 1];
                        Array.Copy(fields, 1, entries, 0, entries.Length);
                        updateRelEntryDict.Add(Convert.ToInt32(fields[0]), entries);

                        for (int i = 1; i < fields.Length; i++)
                        {
                            if (!updateEntryList.Contains(fields[i]))
                            {
                                updateEntryList.Add(fields[i]);
                            }
                        }
                    }
                    dataReader.Close();
                    if (updateEntries == null)
                    {
                        updateEntries = updateEntryList.ToArray();
                    }
                }
            }

            if (updateRelEntryDict.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No relations need to be updated, program terminated, and return.");
                return;
            }
            List<int> updateRelationList = new List<int>(updateRelEntryDict.Keys);
            updateRelationList.Sort();
            int[] updateRelSeqIds = updateRelationList.ToArray();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating domain interface files.");
            ProtCidSettings.logWriter.WriteLine("Updating domain interface files.");
            DomainInterfaceWriter domainInterfaceFileGen = new DomainInterfaceWriter();
            domainInterfaceFileGen.UpdateDomainInterfaceFiles(updateRelEntryDict);
            //            domainInterfaceFileGen.WriteMissingDomainInterfaceFiles();
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update the surface areas for the domain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Update the surfacea areas for the domain interfaces");
            DomainInterfaceSA interfaceSa = new DomainInterfaceSA();
            interfaceSa.UpdateDomainInterfaceSAs(updateEntries);
            //            interfaceSa.CalculateDomainInterfaceSAs ();
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the comparison of domain interfaces in each relation.");
            ProtCidSettings.logWriter.WriteLine("Updating the comparison of domain interfaces in each relation.");
            PfamDomainInterfaceComp domainComp = new PfamDomainInterfaceComp();
            domainComp.UpdateEntryDomainInterfaceComp(updateRelEntryDict);
            //            domainComp.SynchronizeDomainChainInterfaceComp(updateEntries);
            //       domainComp.CompareRepHomoEntryDomainInterfaces();
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the comparison between cryst and BU entry domain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Updating the comparison between cryst and BU entry domain interfaces.");
            DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
            domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(updateEntries);

            //           string[] buUpdateEntries = GetOtherUpdateEntries(@"D:\Qifang\ProjectData\DbProjectData\PDB\newls-pdb_bucomp.txt");
            //           domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(buUpdateEntries);

            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interface clusters.");
            ProtCidSettings.logWriter.WriteLine("Update domain interface clusters.");
            DomainInterfaceCluster domainInterfaceCluster = new DomainInterfaceCluster();
            domainInterfaceCluster.UpdateDomainInterfaceClusters(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Domain Interface Cluster Info.");
            ProtCidSettings.logWriter.WriteLine("Update Domain Interface Cluster Info.");
            DomainClusterStat clusterStat = new DomainClusterStat();
            clusterStat.UpdateDomainClusterInfo(updateRelEntryDict);      
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update cluster file compressing.");
            ProtCidSettings.logWriter.WriteLine("Update compressing cluster interface files.");
            PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
            clusterFileCompress.UpdateRelationClusterChainInterfaceFiles(updateRelSeqIds);
            ProtCidSettings.logWriter.WriteLine("Copy interfaces not in any clusters");
            clusterFileCompress.UpdateCrystInterfaceFilesNotInClusters(updateRelSeqIds, true);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain cluster interface images.");
            ProtCidSettings.logWriter.WriteLine("Update domain cluster interface images");
            DomainInterfaceImageGen imageGen = new DomainInterfaceImageGen();
            imageGen.UpdateDomainInterfaceImages(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain cluster sequence files.");
            ProtCidSettings.logWriter.WriteLine("Update domain cluster sequence files.");
            DomainSeqFasta seqFasta = new DomainSeqFasta();
            seqFasta.UpdateClusterDomainSequences(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-Pfam network files.");
            ProtCidSettings.logWriter.WriteLine("Update Pfam-Pfam network files.");
            PfamRelNetwork pfamNetWriter = new PfamRelNetwork();
            pfamNetWriter.UpdatePfamNetworkGraphmlFiles(updateEntries);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Uniprot-uniprot domain interactions.");
            ProtCidSettings.logWriter.WriteLine("Update Uniprot-uniprot domain interactions.");
            // build the unp-unp interaction table based on pdb domain interfaces
            UnpInteractionStr unpInteract = new UnpInteractionStr();
            unpInteract.UpdateUnpInteractionNetTable(updateEntries);
            ProtCidSettings.logWriter.Flush();

            // update the domain alignment pymol session files
            // move the code after clustering ligands on May 2, 2017, so the cluster info can be included in the Pfam-ligands file
            /*            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update compiling domain alignment pymol sessions");
                        PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
                        domainFileCompress.UpdatePfamDomainFiles(updateEntries);*/
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfacesDb(string updateRelEntryFile)
        {
            Dictionary<int, string[]> updateRelEntryDict = new Dictionary<int,string[]> ();
            string line = "";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Detecting domain-domain interactions from cryst chain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Detecting domain-domain interactions from cryst chain interfaces.");
            ProtCidSettings.progressInfo.currentOperationIndex++;
            List<string> updateEntryList = new List<string>();
            List<int> updateRelationList = new List<int>();
            int relSeqId = 0;
            if (File.Exists(updateRelEntryFile))
            {
                StreamReader dataReader = new StreamReader(updateRelEntryFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(' ');
                    relSeqId = Convert.ToInt32(fields[0]);
                    string[] entries = new string[fields.Length - 1];
                    Array.Copy(fields, 1, entries, 0, entries.Length);
                    updateRelEntryDict.Add(relSeqId, entries);
                    updateRelationList.Add(relSeqId);

                    for (int i = 1; i < fields.Length; i++)
                    {
                        if (!updateEntryList.Contains(fields[i]))
                        {
                            updateEntryList.Add(fields[i]);
                        }
                    }
                }
                dataReader.Close();                
            }
            string[] updateEntries = updateEntryList.ToArray();

            if (updateRelEntryDict.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No relations need to be updated, program terminated, and return.");
                return;
            }
            int[] updateRelSeqIds = updateRelationList.ToArray();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating domain interface files.");
            ProtCidSettings.logWriter.WriteLine("Updating domain interface files.");
            DomainInterfaceWriter domainInterfaceFileGen = new DomainInterfaceWriter();
            domainInterfaceFileGen.UpdateDomainInterfaceFiles(updateRelEntryDict);
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update the surface areas for the domain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Update the surfacea areas for the domain interfaces");
            DomainInterfaceSA interfaceSa = new DomainInterfaceSA();
            interfaceSa.UpdateDomainInterfaceSAs(updateEntries);
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the comparison of domain interfaces in each relation.");
            ProtCidSettings.logWriter.WriteLine("Updating the comparison of domain interfaces in each relation.");
            PfamDomainInterfaceComp domainComp = new PfamDomainInterfaceComp();
            domainComp.UpdateEntryDomainInterfaceComp(updateRelEntryDict);
            //       domainComp.CompareRepHomoEntryDomainInterfaces();
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the comparison between cryst and BU entry domain interfaces.");
            ProtCidSettings.logWriter.WriteLine("Updating the comparison between cryst and BU entry domain interfaces.");
            DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
            domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(updateEntries);
            ProtCidSettings.progressInfo.currentOperationIndex++;
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interface clusters.");
            ProtCidSettings.logWriter.WriteLine("Update domain interface clusters.");
            DomainInterfaceCluster domainInterfaceCluster = new DomainInterfaceCluster();
            domainInterfaceCluster.UpdateDomainInterfaceClusters(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Domain Interface Cluster Info.");
            ProtCidSettings.logWriter.WriteLine("Update Domain Interface Cluster Info.");
            DomainClusterStat clusterStat = new DomainClusterStat();
            clusterStat.UpdateDomainClusterInfo(updateRelEntryDict);
            //    clusterStat.UpdateIPfamInPdbMetaData(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update cluster file compressing.");
            ProtCidSettings.logWriter.WriteLine("Update compressing cluster interface files.");
            PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
            clusterFileCompress.UpdateRelationClusterChainInterfaceFiles(updateRelSeqIds);
            ProtCidSettings.logWriter.WriteLine("Copy interfaces not in any clusters");
            clusterFileCompress.UpdateCrystInterfaceFilesNotInClusters(updateRelSeqIds, true);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain cluster interface images.");
            ProtCidSettings.logWriter.WriteLine("Update domain cluster interface images");
            DomainInterfaceImageGen imageGen = new DomainInterfaceImageGen();
            imageGen.UpdateDomainInterfaceImages(updateRelSeqIds);
            ProtCidSettings.logWriter.Flush();

            DomainSeqFasta seqFasta = new DomainSeqFasta();
            seqFasta.UpdateClusterDomainSequences(updateRelSeqIds);

            PfamRelNetwork pfamNetWriter = new PfamRelNetwork();
            pfamNetWriter.UpdatePfamNetworkGraphmlFiles(updateEntries);

            // build the unp-unp interaction table based on pdb domain interfaces
            UnpInteractionStr unpInteract = new UnpInteractionStr();
            unpInteract.UpdateUnpInteractionNetTable(updateEntries);

            // update the domain alignment pymol session files
            // move the code after clustering ligands on May 2, 2017, so the cluster info can be included in the Pfam-ligands file
            /*            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update compiling domain alignment pymol sessions");
                        PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
                        domainFileCompress.UpdatePfamDomainFiles(updateEntries);*/
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }          
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadRelUpdateEntryHash()
        {
            string relUpdateEntryFile = "UpdatePepInteractPfamEntries.txt";
            Dictionary<string, List<string>> relUpdateEntryHash = new Dictionary<string,List<string>> ();
            ReadRelUpdateEntryHash(relUpdateEntryFile, ref relUpdateEntryHash);

            relUpdateEntryFile = "UpdatePepInteractPfamEntries_missing.txt";
            ReadRelUpdateEntryHash(relUpdateEntryFile, ref relUpdateEntryHash);

            relUpdateEntryFile = "UpdatePepInteractPfamEntries0.txt";
            ReadRelUpdateEntryHash(relUpdateEntryFile, ref relUpdateEntryHash);

            List<string> pfamIdList = new List<string> (relUpdateEntryHash.Keys);
            Dictionary<string, string[]> relUpdateEntriesHash = new Dictionary<string, string[]>();
            foreach (string pfamId in pfamIdList)
            {
                relUpdateEntriesHash.Add (pfamId, relUpdateEntryHash[pfamId].ToArray ());
            }
            return relUpdateEntriesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relUpdateEntryFile"></param>
        /// <param name="relUpdateEntryHash"></param>
        private void ReadRelUpdateEntryHash(string relUpdateEntryFile, ref Dictionary<string, List<string>> relUpdateEntryHash)
        {
            StreamReader dataReader = new StreamReader(relUpdateEntryFile);
            string line = "";
            string pfamId = "";
            string[] entries = null;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                pfamId = fields[0];
                entries = fields[1].Split(',');
                if (relUpdateEntryHash.ContainsKey(pfamId))
                {
                    foreach (string pdbId in entries)
                    {
                        if (!relUpdateEntryHash[pfamId].Contains(pdbId))
                        {
                            relUpdateEntryHash[pfamId].Add(pdbId);
                        }
                    }
                }
                else
                {
                    List<string> entryList = new List<string> (entries);
                    relUpdateEntryHash.Add(pfamId, entryList);
                }
            }
            dataReader.Close();

        }
       
        /// <summary>
        /// 
        /// </summary>
        public static void InitializeThread()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }

            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect();
                ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath;
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath;
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            }

            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath;
                ProtCidSettings.alignmentQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);
            }

            if (ProtCidSettings.buCompConnection == null)
            {
                ProtCidSettings.buCompConnection = new DbConnect();
                ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.baInterfaceDbPath;
                ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            }
            ProtCidSettings.tempDir = "X:\\xtal_temp";
            if (! Directory.Exists (ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }

            PfamLibSettings.pdbfamConnection = ProtCidSettings.pdbfamDbConnection;
            PfamLibSettings.pdbfamDbQuery = new DbQuery(PfamLibSettings.pdbfamConnection);
            PfamLibSettings.alignmentDbConnection = ProtCidSettings.alignmentDbConnection;
        }

        /// <summary>
        /// the list of entries needed to be updated
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()        
        {
            string newLsFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt");
         //    string newLsFile = "MessyDomainInterfaces_more.txt";
            StreamReader entryReader = new StreamReader(newLsFile);
            string line = "";
            string pdbId = "";
            List<string> updateEntryList = new List<string> ();
            while ((line = entryReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                pdbId = line.Substring(0, 4);
                if (!updateEntryList.Contains(pdbId))
                {
                    updateEntryList.Add(pdbId);
                }
            }
            entryReader.Close();

            return updateEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetOtherUpdateEntries(string listFile)
        {
            StreamReader dataReader = new StreamReader(listFile);
            string line = "";
            List<string> entryList = new List<string>();
            string pdbId = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                pdbId = fields[0].Substring(0, 4);
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            dataReader.Close();
            return entryList.ToArray();
        }

        private bool IsEntryDomainExist (string pdbId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable dinterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (dinterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingDomainInterfaceEntries  ()
        {
            List<string> domainMissingEntryList = new List<string>();
            string domainMissingEntryLsFile = "DomainMissingEntryList.txt";
            if (File.Exists(domainMissingEntryLsFile))
            {
                StreamReader entryReader = new StreamReader(domainMissingEntryLsFile);
                string line = "";
                while ((line = entryReader.ReadLine ()) != null)
                {
                    domainMissingEntryList.Add(line);
                }
                entryReader.Close();
            }
            else
            {
                StreamWriter entryWriter = new StreamWriter(domainMissingEntryLsFile);
                string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
                DataTable crystEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
                DataTable domainEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

                List<string> domainEntryList = new List<string>();
                string pdbId = "";
                foreach (DataRow entryRow in domainEntryTable.Rows)
                {
                    domainEntryList.Add(entryRow["PdbID"].ToString());
                }
                domainEntryList.Sort();

                foreach (DataRow entryRow in crystEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    if (domainEntryList.BinarySearch(pdbId) < 0)
                    {
                        domainMissingEntryList.Add(pdbId);
                        entryWriter.WriteLine(pdbId);
                    }
                }
                entryWriter.Close();
            }
            return domainMissingEntryList.ToArray();
        }
        #endregion

        #region partial update for debug
        public void UpdatePfamDomainInterfaceClusters()
        {
            InitializeThread();
            DomainInterfaceTables.InitializeTables();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing entry domain interfaces.");
       //     string[] updateEntries = {/* "4yc3",*/ "4y72"};
            string[] updateEntries = GetReversedOrderDomainInterfaces();
            DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
            domainInterfaceComp.UpdateCrystBuDomainInterfaceComp(updateEntries);                     

 /*           UnpInteractionStr unpInterInPdb = new UnpInteractionStr();
            unpInterInPdb.UpdateUnpInteractionNetTableFromXml ();
            string[] updateEntries = {"4xr8"};
            unpInterInPdb.UpdateUnpInteractionNetTable(updateEntries);
            PfamDomainInterfaceComp domainComp = new PfamDomainInterfaceComp();
            string[] entries = {"5vam", "6cad", "6b8u"};
            domainComp.SynchronizeDomainChainInterfaceComp(entries);
*/
 /*           DomainClusterStat clusterStat = new DomainClusterStat();
            int[] relSeqIds = { 14511 };
            clusterStat.PrintDomainClusteStatInfo(relSeqIds);
  //          clusterStat.AddMissingRedundantInterfacesToClusterTable ();

            int[] updateRelSeqIds = GetUpdateRelSeqIds();
            PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
            clusterFileCompress.UpdateRelationClusterChainInterfaceFiles(updateRelSeqIds);

            string[] updateEntries = GetOtherUpdateEntries();

            UpdateDomainInterfacesDb(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing entry domain interfaces.");
            DomainInterfaceBuComp.CrystBuDomainInterfaceComp domainInterfaceComp =
                new InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp.CrystBuDomainInterfaceComp();
            domainInterfaceComp.UpdateCrystBuDomainInterfaceComp (updateEntries);                     

            int[] updateRelSeqIds = GetUpdateRelSeqIds ();
            Dictionary<int, string[]> updateRelEntryDict = ReadUpdateRelEntries();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating the comparison of domain interfaces in each relation.");
            ProtCidSettings.logWriter.WriteLine("Updating the comparison of domain interfaces in each relation.");
            PfamDomainInterfaceComp domainComp = new PfamDomainInterfaceComp();
   //         domainComp.UpdateEntryDomainInterfaceComp(updateEntries);
            domainComp.UpdateEntryDomainInterfaceComp(updateRelEntryDict);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interface clusters.");
            ProtCidSettings.logWriter.WriteLine("Update domain interface clusters.");
            DomainInterfaceCluster domainInterfaceCluster = new DomainInterfaceCluster();
            domainInterfaceCluster.ClusterDomainInterfaces();
            //     int[] updateRelSeqIds = domainInterfaceCluster.GetUpdateRelSeqIds();
            //   domainInterfaceCluster.UpdateDomainInterfaceClusters(updateRelSeqIds);
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Domain Interface Cluster Info.");
            ProtCidSettings.logWriter.WriteLine("Update Domain Interface Cluster Info.");
            DomainClusterStat clusterStat = new DomainClusterStat();
        //    clusterStat.PrintDomainDbSumInfo("PfamDomain");
            clusterStat.PrintDomainClusterInfo();
            
            //    clusterStat.UpdateDomainClusterInfo(updateRelSeqIds);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update cluster file compressing.");
            PfamClusterFilesCompress clusterFileCompress = new PfamClusterFilesCompress();
            clusterFileCompress.UpdateRelationClusterChainInterfaceFiles(updateRelSeqIds);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain cluster interface images.");
            ProtCidSettings.logWriter.WriteLine("Update domain cluster interface images");
            DomainInterfaceImageGen imageGen = new DomainInterfaceImageGen();
            //     imageGen.GenerateDomainInterfaceImages();
            imageGen.UpdateDomainInterfaceImages(updateRelSeqIds);
            
            DomainSeqFasta seqFasta = new DomainSeqFasta();
            seqFasta.PrintClusterDomainSequences();
      //      seqFasta.UpdateClusterDomainSequences(updateRelSeqIds); */
        }

        private Dictionary<int, string[]> ReadUpdateRelEntries()
        {
            Dictionary<int, string[]> updateRelEntryDict = new Dictionary<int,string[]> ();
            string updateRelEntryFile = "DifRelSeqIdEntries.txt";
            StreamReader dataReader = new StreamReader(updateRelEntryFile);
            string line = "";
            int relSeqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                string[] entries = new string[fields.Length - 1];
                Array.Copy(fields, 1, entries, 0, entries.Length);
                relSeqId = Convert.ToInt32(fields[0]);
                updateRelEntryDict.Add(relSeqId, entries);
            }
            dataReader.Close();
            return updateRelEntryDict;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetUpdateRelSeqIds()
        {
            string relTarDataDir = @"D:\protcid_update31Fromv30\UpdateDomainClusterInterfaces";
            string[] tarFiles = Directory.GetFiles(relTarDataDir, "*.tar");
            List<int> updateRelList = new List<int>();
            DateTime dt = new DateTime (2018,1, 1);
            int relSeqId = 0;
            foreach (string tarFile in tarFiles)
            {
                FileInfo fileInfo = new FileInfo(tarFile);
                if (DateTime.Compare (fileInfo.LastWriteTime, dt) < 0)
                {
                    relSeqId = Convert.ToInt32(fileInfo.Name.Replace (".tar", ""));
                    if (! updateRelList.Contains (relSeqId))
                    {
                        updateRelList.Add(relSeqId);
                    }
                }
            }
            return updateRelList.ToArray();
/*
            string updateRelSeqIdFile = "UpdateRelSeqIds_100.txt";
            ArrayList updateRelSeqIdList = new ArrayList();
            int relSeqId = 0;

            StreamReader dataReader = new StreamReader(updateRelSeqIdFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                relSeqId = Convert.ToInt32(line);
                updateRelSeqIdList.Add(relSeqId);
            }
            dataReader.Close();

            string difRelSeqIdsFile = "DifRelSeqIDs.txt";
            dataReader = new StreamReader(difRelSeqIdsFile);
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split();
                relSeqId = Convert.ToInt32(fields[0]);
                if (! updateRelSeqIdList.Contains(relSeqId))
                {
                    updateRelSeqIdList.Add(relSeqId);
                }
                relSeqId = Convert.ToInt32(fields[1]);
                if (!updateRelSeqIdList.Contains(relSeqId))
                {
                    updateRelSeqIdList.Add(relSeqId);
                }
            }
            dataReader.Close();

            int[] updateRelSeqIds = new int[updateRelSeqIdList.Count];
            updateRelSeqIdList.CopyTo(updateRelSeqIds);
            return updateRelSeqIds;*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetReversedOrderDomainInterfaces ()
        {
            string queryString = "Select Distinct PfamDomainInterfaces.PdbID As PdbID " + 
                " From PfamDomainInterfaces, CrystPdbBuInterfaceComp " + 
                " Where PfamDomainInterfaces.PdbID = CrystPdbBuInterfaceComp.PdbID AND PfamDomainInterfaces.InterfaceID = CrystPdbBuInterfaceComp.InterfaceID AND " + 
                " IsReversed = '1' AND Qscore > 0.5;";
            DataTable entryReversedChainsTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            foreach (DataRow entryRow in entryReversedChainsTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString());
            }
            return entryList.ToArray();
        }
        #endregion
    }
}
