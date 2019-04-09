using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamLigandInteractionBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        public void BuildPfamLigandInteractions()
        {
            DomainInterfaceBuilder.InitializeThread();

            PfamDnaRnaInteractions pfamDnaInteract = new PfamDnaRnaInteractions();
            pfamDnaInteract.RetrievePfamDnaRnaInteractions();

            PfamLigandInteractions pfamLigandInteract = new PfamLigandInteractions();
            pfamLigandInteract.GetLigandsInPdb();
            pfamLigandInteract.RetrievePfamLigandInteractions();
            pfamLigandInteract.GetPfamLigandInteractionSumInfo();
            pfamLigandInteract.PrintLigandInteractingPfamsSumInfo();

            PfamDnaRnaFileCompress dnaRnaFileCompress = new PfamDnaRnaFileCompress();
            dnaRnaFileCompress.WriteDomainDnaRnaFiles();

            //   LigandComAtomsPfam ligandComAtomCal = new LigandComAtomsPfam();
            //   ligandComAtomCal.CalculateOverlapLigandAtomsInPfam();
            LigandComPfamHmm comPfamHmm = new LigandComPfamHmm();
            comPfamHmm.CalculateLigandsComPfamHmmPos();

            PfamLigandClusterHmm ligandClusters = new PfamLigandClusterHmm();
            ligandClusters.ClusterPfamLigandsByPfamHmm();

            // need add ligand clusters to the domain tar.gz files
            // move the operation here on April 28, 2017
            PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
            domainFileCompress.CompressPfamDomainFiles();

            PfamLigandFileCompress ligandFileCompress = new PfamLigandFileCompress();
            ligandFileCompress.CompressLigandPfamDomainFilesFromPdb();
        }

        public void DebugPfamLigandInteractions()
        {
            DomainInterfaceBuilder.InitializeThread();
 /*           LigandComPfamHmm comPfamHmm = new LigandComPfamHmm();
            string pfamId = "T-box";
            comPfamHmm.CalculatePfamLigandComHmmPos (pfamId);

            PfamLigandClusterHmm ligandClusters = new PfamLigandClusterHmm();
 //           pfamId = "1-cysPrx_C";
            ligandClusters.ClusterPfamLigandsByPfamHmm(pfamId, false);
  //          ligandClusters.ClusterPfamLigandsByPfamHmm();
            
            PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
 //           domainFileCompress.CompressPfamDomainFiles();
//            pfamId = "40S_S4_C"; // 40S_S4_C
            domainFileCompress.CompilePfamDomainWithLigandFiles(pfamId, null);*/

            PfamLigandFileCompress ligandFileCompress = new PfamLigandFileCompress();
            string ligandId = "DNA";
            ligandFileCompress.CompressDomainDnaRnaFiles(ligandId);
            ligandFileCompress.CompressLigandPfamDomainFilesFromPdb();  
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamLigandInteractions(string[] updateEntries)
        {
            DomainInterfaceBuilder.InitializeThread();
/*
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-DNA/RNA interactions info");
            ProtCidSettings.logWriter.WriteLine("Update Pfam-DNA/RNA interactions info");
            PfamDnaRnaInteractions pfamDnaInteract = new PfamDnaRnaInteractions();
            string[] updateDnaRnaEntries = null;
            updateDnaRnaEntries = pfamDnaInteract.GetUpdateEntriesWithDnaRna(updateEntries);
            pfamDnaInteract.UpdatePfamDnaRnaInteractions(updateDnaRnaEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-ligands interactions info");
            ProtCidSettings.logWriter.WriteLine("Update Pfam-ligands interactions info");
            PfamLigandInteractions pfamLigands = new PfamLigandInteractions();
            pfamLigands.UpdateLigandsInPdb(updateEntries);
            pfamLigands.UpdatePfamLigandInteractions(updateEntries);
            pfamLigands.UpdatePfamLigandInteractionSumInfo(updateEntries);
            pfamLigands.UpdateLigandInteractingPfamsSumInfo(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Update Pfam-DNA/RNA interface files");
            PfamDnaRnaFileCompress dnaRnaFileCompress = new PfamDnaRnaFileCompress();
            dnaRnaFileCompress.UpdateDomainDnaRnaFiles(updateDnaRnaEntries);

            ProtCidSettings.logWriter.WriteLine("Update Pfam-ligands the number of common Pfam HMM positions");
            LigandComPfamHmm ligandComHmmCal = new LigandComPfamHmm();
            Dictionary<string, List<string>> updatePfamEntryListDict = ligandComHmmCal.UpdateLigandsComPfamHmmPos(updateEntries);
   //         Dictionary<string, List<string>> updatePfamEntryListDict = ligandComHmmCal.GetUpdatePfams(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Update Pfam-ligands clusters");
            PfamLigandClusterHmm ligandClusters = new PfamLigandClusterHmm();
            ligandClusters.UpdatePfamLigandsByPfamHmm(updatePfamEntryListDict);
            */
            ProtCidSettings.logWriter.WriteLine("Update compressing Pfam-ligands domain interface files. Pfam_ID.tar.gz: Pfam domain aligned (one Pfam - ligands)");
            PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
            domainFileCompress.UpdatePfamDomainFiles(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Update ligand-Pfam domain interface files. Ligand.tar.gz (one ligand - Pfams) ");
            PfamLigandFileCompress ligandFileCompress = new PfamLigandFileCompress();
            ligandFileCompress.UpdateLigandPfamDomainFilesFromPdb(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Pfam-ligand update is done!");
            ProtCidSettings.logWriter.Flush();
        }
     
        /// <summary>
        /// 
        /// </summary>
        public void AddClusterInfoToPfamDomainAlign ()
        {
            PfamLigandFileCompress ligandFileCompress = new PfamLigandFileCompress();
            ligandFileCompress.AddLigandClusterInfoToPfamDomainCompressFiles();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadUpdatePfamEntryListDict ()
        {
            Dictionary<string, List<string>> updatePfamEntryListDict = new Dictionary<string, List<string>>();
            StreamReader dataReader = new StreamReader("UpdatePfamEntries.txt");
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split();
                List<string> entryList = new List<string> ();
                entryList.AddRange (fields);
                entryList.RemoveAt (0);
                updatePfamEntryListDict.Add(fields[0], entryList);
            }
            dataReader.Close();
            return updatePfamEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamLigandInteractions(string[] updateDnaRnaEntries, string[] updateEntries)
        {
            DomainInterfaceBuilder.InitializeThread();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-DNA/RNA interactions info");
            PfamDnaRnaInteractions pfamDnaInteract = new PfamDnaRnaInteractions();
            
   //         pfamDnaInteract.UpdatePfamDnaRnaInteractions(updateDnaRnaEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam-ligands interactions info");
            PfamLigandInteractions pfamLigands = new PfamLigandInteractions();
   //         pfamLigands.UpdateLigandsInPdb(updateEntries);
            pfamLigands.UpdatePfamLigandInteractions(updateEntries);
            pfamLigands.UpdatePfamLigandInteractionSumInfo(updateEntries);
            pfamLigands.UpdateLigandInteractingPfamsSumInfo(updateEntries);

            PfamDnaRnaFileCompress dnaRnaFileCompress = new PfamDnaRnaFileCompress();
            dnaRnaFileCompress.UpdateDomainDnaRnaFiles(updateDnaRnaEntries);

            LigandComPfamHmm ligandComHmmCal = new LigandComPfamHmm();
            Dictionary<string, List<string>> updatePfamEntryListDict = ligandComHmmCal.UpdateLigandsComPfamHmmPos(updateEntries);

            PfamLigandClusterHmm ligandClusters = new PfamLigandClusterHmm();
            ligandClusters.UpdatePfamLigandsByPfamHmm(updatePfamEntryListDict);

            PfamDomainFileCompress domainFileCompress = new PfamDomainFileCompress();
            domainFileCompress.UpdatePfamDomainFiles(updateEntries);

            PfamLigandFileCompress ligandFileCompress = new PfamLigandFileCompress();
            ligandFileCompress.UpdateLigandPfamDomainFilesFromPdb(updateEntries);
        }
    }
}
