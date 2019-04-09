using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using InterfaceClusterLib.ChainInterfaces;
using InterfaceClusterLib.EntryInterfaces;
using InterfaceClusterLib.DomainInterfaces;
using InterfaceClusterLib.DomainInterfaces.PfamNetwork;
using InterfaceClusterLib.Pkinase;
using BuCompLib.BuInterfaces;
using CrystalInterfaceLib.FileParser;
using InterfaceClusterLib.stat;
using ProtCidSettingsLib;
using InterfaceClusterLib.DomainInterfaces.PfamLigand;
using BugFixingLib;
using ProtCIDWebDataLib;
using ProtCIDPaperDataLib;
using ProtCIDPaperDataLib.paper;
using InterfaceClusterLib.DomainInterfaces.PfamPeptide;

namespace ProtCidLibraries
{
    public partial class LibTest : Form
    {
        public LibTest()
        {
            InitializeComponent();
            ProtCidSettings.applicationStartPath = Application.StartupPath;
        }

        private void entryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //        CrystalInterfaceLib.FileParser.CrystalXmlBuilder xmlGen = new CrystalInterfaceLib.FileParser.CrystalXmlBuilder();
            //       xmlGen.CreateCrystalXmlFiles();
           EntryInterfaceBuilder interfaceBuilder = new EntryInterfaceBuilder();
       //      interfaceBuilder.UpdateEntryInterfaceData(null);
      //      interfaceBuilder.BuildEntryInterfaceGroups(3);
           interfaceBuilder.AddSymmetryIndexes ();
        }

        private void chainClusteringMenuItem_Click(object sender, EventArgs e)
        {
    //        PaperDataInfo paperDataInfo = new PaperDataInfo();
     //       paperDataInfo.OutputMultiDomainChainInterfacesDomainClusters();

            ChainInterfaceBuilder interfaceBuilder = new ChainInterfaceBuilder();
            interfaceBuilder.UpdatePfamChainClusterFiles();
  //          interfaceBuilder.UpdateChainInterfaceClusters();
     //        interfaceBuilder.UpdateChainInterfaceQscores();
    //        interfaceBuilder.BuildChainInterfaceClusters(2);
        }

        private void domainClusteringMenuItem_Click(object sender, EventArgs e)
        {
            /*        DomainSeqIdentity interfaceCompIdentity = new DomainSeqIdentity();
                      interfaceCompIdentity.FillDomainInterfaceCompSeqIdentities();
            */
            DomainInterfaceBuilder domainInterfaceBuilder = new DomainInterfaceBuilder();
            //        domainInterfaceBuilder.UpdatePfamDomainInterfaceClusters();
            //        domainInterfaceBuilder.BuildDomainInterfaces(5);
         //   domainInterfaceBuilder.BuildDomainInterfaces(7);
          //         domainInterfaceBuilder.UpdateDomainInterfaces (1);
            domainInterfaceBuilder.UpdatePfamDomainInterfaceClusters ();
        }

        private void peptideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DomainInterfaceBuilder domainInterfaceBuilder = new DomainInterfaceBuilder();
            domainInterfaceBuilder.UpdateDomainInterfaces (2);    
        }

        private void ligandsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DomainInterfaceBuilder domainInterfaceBuilder = new DomainInterfaceBuilder();
            domainInterfaceBuilder.UpdateDomainInterfaces (3);
       /*     PfamLigandInteractionBuilder ligandInteractBuilder = new PfamLigandInteractionBuilder();
            ligandInteractBuilder.DebugPfamLigandInteractions();
           string[] updateEntries = ReadUpdateEntries(entryFile);
            ligandInteractBuilder.UpdatePfamLigandInteractions(updateEntries);*/
        }

        private void pkinaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PkinaseInterfaceInfo interfaceInfo = new PkinaseInterfaceInfo();
            interfaceInfo.GenerateInterfaceFilesInXmlSeq();
        }

        #region update BuComp database
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateBAInterfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //        bool isUpdate = true;
            BuCompLib.BuCompBuilder buCompBuilder = new BuCompLib.BuCompBuilder();
            //        buCompBuilder.FindInterfacesInBUs(isUpdate);
  //          buCompBuilder.UpdateComparingEntryBiolAssembliess();
            buCompBuilder.UpdateMissingEntryBiolAssemblies();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updaTeBADomainInterfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuCompLib.BuCompBuilder buCompBuilder = new BuCompLib.BuCompBuilder();
            buCompBuilder.UpdateChainPeptideInterfacesInBAs();
            //          buCompBuilder.CombineUpdateEntries();
            buCompBuilder.FindDomainInterfacesInBAs(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateChainLigandInterfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuCompLib.BuCompBuilder buComp = new BuCompLib.BuCompBuilder();
            buComp.UpdateChainPeptideInterfacesInBAs();
        }
        #endregion

        #region update coordinate xml files
        private void updateXMLCoordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CrystalXmlBuilder xmlCoordGen = new CrystalXmlBuilder ();
            xmlCoordGen.modifyType = "update";
            xmlCoordGen.CreateCrystalXmlFiles();
        }
        #endregion

        #region external data
        private void bioGenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BiogenDataGen biogenData = new BiogenDataGen();
            biogenData.PrintHumanPfamAssignments();
    //        biogenData.OutputChainInterfacesAndClusterInfo ();
     //       biogenData.FillInClusterInfo();
     //       biogenData.GenerateDomainInterfaceInfoFile();
        }

        private void liXueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExternalClusterData externalData = new ExternalClusterData();
            externalData.CompileDifPfamClusterCoordinatesFiles();
        }
        #endregion

        #region data analysis
        private void dataAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InterfaceSymmetryDataInfo symDataInfo = new InterfaceSymmetryDataInfo();
            symDataInfo.PrintHomoInterfaceSymmetryIndexes();

        /*         PaperDataInfo paperData = new PaperDataInfo();
                
                   paperData.PrintClanPfamPdbUnp();                 
                  paperData.GetAllPfamLigandsSumInfo();
                   paperData.PrintLysineDistances();
                 paperData.PrintUniqueSequences();
                  paperData.PrintPfamPeptideRelationsSumInfo();
                  paperData.RewritePymolScriptFileByAlign ();
                  paperData.CheckNotPublishedEntries();
                  paperData.PrintCommonEntriesInClusters();
    //              paperData.PrintMHCSequences ();
  //                paperData.CheckNotPublishedEntries();
  //                paperData.RewritePymolScriptFileByAlign();
                  paperData.RewritePymolScriptFileByRenameUnp();
         //        paperData.AddLigandsToClusterTextFile();
            PaperDataInfo paperData = new PaperDataInfo();
            paperData.PrintMNPdbPisaUnderAnnotation();
            paperData.PrintMNAndPdbPisaCoverages();
            paperData.FormatPdbPisaNumbersBarplot();*/

            /*   
                 paperData.PrintOnePfamUnpSequences();
                 paperData.PrintDomainInterfaceClusterSumInfo ();
                 paperData.PrintPfamLigandPepSumInfo ();
                 paperData.PrintPfamLigandClusterSumInfo ();
                 paperData.PrintPfamLigandClusterSumInfo();
             * 
                 paperData.PrintMNPdbPisaUnderAnnotation();
                 paperData.PrintMNAndPdbPisaCoverages ();
             * 
                 paperData.CheckSingleNotPublishedEntries();
                 paperData.CheckNotPublishedEntries();
                 paperData.CheckAllPeptidesWithInteractions();                
                 paperData.CheckEntryWithLiteratures();
                 paperData.PrintPeptideClustersSumInfo();
             * 
                 paperData.CheckChainInterfaces();            
                 paperData.PrintDomainInterfacesWithBioClustersChainNo();
                                        
                 paperData.PrintPfamsWithInteraction();
                 paperData.PrintPfamsWithNoInteracting();*/


            /*           ClusterInfo clusterInfo = new ClusterInfo();
              //         clusterInfo.PrintDomainClustersChainGroupsSumInfo();
             //          clusterInfo.SelectMultiChainInterfaces();
             //          clusterInfo.OutputMultiDomainChainInterfacesDomainClusters();
                       clusterInfo.PrintChainDomainClusters();
                       clusterInfo.OutputChainDomainCompInfo();
                       clusterInfo.FormatChainDomainClusterCompInfo();
                  //              clusterInfo.PrintDomainClusterInfo ();*/         
        }      
        #endregion        

        #region unp-pfam
        private void uniProtToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DomainInterfaceBuilder.InitializeThread();
            UnpInteractionStr unpDomainInter = new UnpInteractionStr();
            unpDomainInter.BuildUnpInteractionNetTable();
        }
        #endregion

        private string[] ReadUpdateEntries (string entryFile)
        {
            List<string> entryList = new List<string>();
            StreamReader dataReader = new StreamReader(entryFile);
            string line = "";
            string pdbId = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("Write domain file error:") > -1)
                {
                    pdbId = line.Substring(0, 4);
                    if (! entryList.Contains (pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            dataReader.Close ();
            dataReader = new StreamReader(@"X:\Qifang\Projects\ProtCidDbProjects\DeletedEntriesInPfamLigands.txt");
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split();
                if (!entryList.Contains(fields[1]))
                {
                    entryList.Add(fields[1]);
                }
            }
            dataReader.Close();
            return entryList.ToArray();
        }

        #region kinase interfaces
        private void kinaseInterfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PkinaseInterfaceInfo kinaseInterfaceGen = new PkinaseInterfaceInfo();
            kinaseInterfaceGen.GenerateInterfaceFilesInXmlSeq();
        }
        #endregion

        #region bug fixing
        private void bugFixToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MissingData messyData = new MissingData();
            messyData.GetDomainInterfacesEntriesMessyDomainIds();
      /*      BugFixing bugFix = new BugFixing();
            bugFix.DeleteDuplicateRowsInBioAssemDB();
                    bugFix.GetMissingHHCrcPairsInFbDb ();
                    bugFix.GetEntriesWithMismatchDomainIDs ();
                    bugFix.UpdateAsuString();
                    bugFix.ReadMissingDomainFileList ();
                    bugFix.FixDomainClusterInterfaceCfGroups ();
                    bugFix.DeletePfamLigandsData ();
                    bugFix.PrintEntriesMissingStrongHits();
                     bugFix.GetEntriesWithMismatchDomainIDs();
                  bugFix.GetEntriesMissingBAchainNowAdded ();
                    bugFix.CheckDomainInterfacesInPdbPisa();

                   bugFix.GetEntriesWithInconsistentDomainAndFileInfo();
                   bugFix.InsertDomainsFromCopy();
                   bugFix.GetEntryList();
                   bugFix.SaveUpdateGroupIds();*/
        }
        #endregion

        #region paper data 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void paperInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DomainSuperpositionRmsd domainRmsd = new DomainSuperpositionRmsd();
            domainRmsd.CalculateDomainRegionRmsd();

            // BiolAssembly Data
            /*        BioAssemblyDataInfo baDataInfo = new BioAssemblyDataInfo();
                   baDataInfo.ReformatClustalOmegaPimMatrix();
                 baDataInfo.RetrieveHumanProteins();
                 baDataInfo.PrintCommonEntriesUniProts();
                  baDataInfo.PrintLinkedHumanProteins();
        //           baDataInfo.PrintClustersDifPfamRelations();
                 baDataInfo.PrintAllUniprotsInAGroup(737);
                  baDataInfo.PrintAllUniprotsInAGroup(737, 1);
                  baDataInfo.PrintCommonEntriesUniProts();
                  baDataInfo.PrintSameGroupBiolClusters();*/

            // Benchmark Data
            /*         BenchmarkDataInfo benchDataInfo = new BenchmarkDataInfo();
                     benchDataInfo.FormatClusterSumDataToRFiles ();
           //          benchDataInfo.GetClusterInfoSummaryData();
           //          benchDataInfo.RetrieveClusterInfoEppicInterfaces();
           //          benchDataInfo.RetrieveClusterInfoEppicBenchmarkInterfaces();
           //          benchDataInfo.CheckProtCidClustersInBenchmark();*/

            // Interface Symmetry
            /*        InterfaceSymmetryDataInfo symDataInfo = new InterfaceSymmetryDataInfo();
                   symDataInfo.RetrieveRasAlpha34Dimer();*/

            // ClusterInfo
            /*         ClusterInfo clusterInfo = new ClusterInfo();
                     clusterInfo.PrintInterfaceClustersForPaper();
                     clusterInfo.PrintRasErbbAsymDimerSumInfo();
                      clusterInfo.RetreiveRasFromPfamDomainAlignFile();
                    clusterInfo.PrintBromodomainChainClustersSumInfo();
             //        clusterInfo.PrintRasAlphaDimerCluster ();
            //          clusterInfo.PrintRasAlphaDimerCluster();
                       clusterInfo.PrintRasErbbAsymDimerSumInfo (); */

            // PymolScript rewrite
            /*        PymolScriptRewriter scriptRewriter = new PymolScriptRewriter();
                     scriptRewriter.DividPymolScriptFileToDifChainPfamArch();v
                     scriptRewriter.RewritePymolScriptFileByRenameUnp();
                     ClusterInfo clusterInfo = new ClusterInfo();
                     clusterInfo.PrintDifACTPfamClusters(); */

            // peptide data
            /*          PaperPeptideDataInfo peptideData = new PaperPeptideDataInfo();
                      peptideData.GetNumOfHumanProteinsInPPBDs ();
                //        peptideData.PrintPeptideBindingPfamsInfo();
               //        peptideData.PrintPeptideClansInfo();
                       peptideData.PrintPeptideClustersSumInfo();
           */
            // DnaRna data
            /*          PaperDnaRnaDataInfo dnaRnaInfo = new PaperDnaRnaDataInfo();
                      dnaRnaInfo.PrintClanPfamPdbUnp();
               //       dnaRnaInfo.PrintPfamDnaRnaInfo();
                //      dnaRnaInfo.PrintClanPfamDnaRnaInfo();*/

            // ligand data
            /*          PaperLigandDataInfo ligandDataInfo = new PaperLigandDataInfo();
                      ligandDataInfo.PrintPfamLigandClusterSumInfoUnp ();
             //         ligandDataInfo.GetAllPfamLigandsSumInfo();*/

            // unp interaction data
            /*        HumanUnpDataInfo unpDataInfo = new HumanUnpDataInfo();
            //        unpDataInfo.PrintUnpProteinPfamInteractions();
                    unpDataInfo.PrintUnpInteractionSumInfoDifMs();
                    unpDataInfo.FillOutUniprotIDs();
                   unpDataInfo.PrintUnpInteractionSumInfo();*/
        }
        #endregion

        #region ProtCID web data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pfamClanDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PfamData webPfamData = new PfamData();
            webPfamData.GenerateClansInPdbTable();
        }

        private void interfaceSumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DownloaderFileRename fileRename = new DownloaderFileRename();
            fileRename.RenameGroupFiles();

            PfamDomainInterfaceSumTables domainSumTables = new PfamDomainInterfaceSumTables();
            // domainSumTables.GetEntriesWithInConsistentPfams();
            domainSumTables.AddMissingPfamPfamEntryChainArchs();
            // domainSumTables.BuildPfamPfamEntryChainArchs();
            // domainSumTables.ReformatEntityUnpCodesToTable ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bioAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PdbBaDataInfo baDataInfo = new PdbBaDataInfo();
            baDataInfo.GeneratePdbBAInfoFile();
            // BioAssemblyDataInfo bioAssemInfo = new BioAssemblyDataInfo();
            // bioAssemInfo.CountNumClustersWithCommonEntriesInPdbPisa ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void complexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EMStructuresInfo emStruct = new EMStructuresInfo();
  //          emStruct.CheckEMcomplexesToBeXtalStructModeled();
 //           emStruct.RetrieveEmStructuresData();
            emStruct.RetrieveEMcomplexesToBeModeled ();
   //         emStruct.RetrieveEmStructuresData();
  //          emStruct.AddStructuresPfamsClustersInfo();
     //       emStruct.AddMissingEMresolution();
     //       emStruct.PrintEMStructuresComplexInfo();

            ComplexStructures complexStruct = new ComplexStructures();
     //       complexStruct.RetrieveComplexPortalStructuresData();
    //        complexStruct.ReadComplexesFromOutputComplexStructFile();
            complexStruct.RetrieveComplexPortalPdbProtCidData();
            //         complexStruct.PrintLinkedHumanProteins();
 //          complexStruct.FindComplexStructureSamples();
            //          complexStruct.AddPdbStructuresToComplexes();
            //       complexStruct.ParsePossibleTetramerComplexes();
            //       complexStruct.ParsePossiblePentamerComplexes();
            //       complexStruct.ParsePossibleTrimerComplexes();
        }
        #endregion


        
    }
}
