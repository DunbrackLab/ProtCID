using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using ProtCidSettingsLib;
using System.Data;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PfamPeptideInterfaceBuilder
    {
        #region build
        /// <summary>
        /// 
        /// </summary>
        public void BuildPfamPeptideInterfaces()
        {
            BuildPfamPeptideInterfaceClusters();
           
            ComparePeptideDomainInterfaces();

            ComparePeptideChainInterfaces();
        }

        /// <summary>
        /// 
        /// </summary>
        public void BuildPfamPeptideInterfaceClusters()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Pfam-Peptide interfaces and cluster interfaces");
            ProtCidSettings.logWriter.WriteLine("Retrieve Pfam-Peptide interfaces and cluster interfaces");
          
            // retreive peptide interfaces
            PfamPeptideInterfaces pfamPepInterfaces = new PfamPeptideInterfaces();
            pfamPepInterfaces.RetrievePfamPeptideInterfaces();
   //         pfamPepInterfaces.GetPfamPeptideInPdbMetaData();

            // write peptide interfaces into PDB format files
            PfamPepInterfaceWriter pepInterfaceWriter = new PfamPepInterfaceWriter();
            pepInterfaceWriter.GeneratePfamPeptideInterfaceFiles();

            // calculate the SA by NACCESS
            DomainInterfaceSA pepInterfaceSa = new DomainInterfaceSA();
            pepInterfaceSa.domainInterfaceTableName = "PfamPeptideInterfaces";
            pepInterfaceSa.CalculateDomainInterfaceSAs();

            // common hmm positions
            PfamHmmSites commonHmmSites = new PfamHmmSites();
            // common hmm positions between peptide interfaces
            commonHmmSites.CountPeptideInterfaceHmmSites();
            // common hmmm positions between domain and peptide interfaces
            //   commonHmmSites.CountPfamCommonHmmSites();

            // Peptide-peptide RMSD
            PeptideInterfaceRmsd pepInterfaceRmsd = new PeptideInterfaceRmsd();
            pepInterfaceRmsd.CalculateDomainInterfacePeptideRmsd();
            pepInterfaceRmsd.CalculateMissingPfamDomainInterfacePeptideRmsd();

            // cluster peptide interfaces, them compress the peptide interfaces into tar files
            PeptideInterfaceCluster pepCluster = new PeptideInterfaceCluster();
            pepCluster.ClusterPeptideInterfaces();

            PeptideInterfaceClusterStat pepClusterStat = new PeptideInterfaceClusterStat();
            pepClusterStat.GetPepInterfaceClusterStat();

            PepClusterInterfaceCompress pepClusterFileCompress = new PepClusterInterfaceCompress();
            pepClusterFileCompress.CompressClusterPeptideInterfaceFiles();

            PfamPepSeqFasta pepSeqFasta = new PfamPepSeqFasta();
            pepSeqFasta.PrintPfamPeptideClusterSequences();

            PfamPepInterfaceFileCompress fileCompress = new PfamPepInterfaceFileCompress();
            fileCompress.CompressPfamPeptideInterfaces();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering peptide interfaces done!");
            ProtCidSettings.logWriter.WriteLine("Clustering peptide interfaces done! ");
        }

        /// <summary>
        /// superpose domains, then calculate RMSD between domain and peptide
        /// </summary>
        public void ComparePeptideDomainInterfaces()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Superpose domains, then calculate peptide and domain RMSD");
            ProtCidSettings.logWriter.WriteLine("Superpose domains, then calculate peptide and domain RMSD");
            // peptide-domain RMSD
            PfamInterfaceRmsd pepDomainRmsd = new PfamInterfaceRmsd();
            pepDomainRmsd.CalculateInterfaceDomainPeptideRmsd();
            //   pepDomainRmsd.ImportDomainPepitdeRmsdIntoDb ();  // should insert the RMSD into db after calculation, will add later
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("RMSD between peptide and domains done!");
            ProtCidSettings.logWriter.WriteLine("RMSD between peptide and domains done!");
        }

        /// <summary>
        /// superpose one chain, then calculate RMSD between chain and peptide
        /// </summary>
        public void ComparePeptideChainInterfaces()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Superpose chains, then calculate peptide and chain RMSD");
            ProtCidSettings.logWriter.WriteLine("Superpose chains, then calculate peptide and chain RMSD");
            PfamChainInterfaceHmmSites pepChainHmmSiteComp = new PfamChainInterfaceHmmSites();
            // common hmm positions between peptide and chain interfaces
            pepChainHmmSiteComp.CountPfamPepChainInterfaceHmmSites();
            // pepChainHmmSiteComp.CountPfamMissingChainInterfaceHmmSites("Pkinase", 18047);
            //   pepChainHmmSiteComp.CountPfamMissingChainInterfaceHmmSites();
            //   pepChainHmmSiteComp.RemovePeptideChainHmmSitesComp();


            // peptide-chain RMSD
            PeptideChainInterfaceRmsd pepChainRmsd = new PeptideChainInterfaceRmsd();
            //        pepChainRmsd.CalculateClusterPeptideChainInterfaceRmsd();
            pepChainRmsd.CalculatePeptideChainRmsd();
            //      pepChainRmsd.ImportPepChainRmsdIntoDb();

            //          PepClusterInterfaceCompress pepInterfaceCompress = new PepClusterInterfaceCompress ();
            //          pepInterfaceCompress.CompressClusterPeptidChainInterfacesFiles();
            //    pepCluster.FindChainInterfacesNotIn();
            //       pepCluster.CompressPepChainInterfaces();*/
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("RMSD between peptide and chain done!");
            ProtCidSettings.logWriter.WriteLine("RMSD between peptide and chain done!");
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamPeptideInteractions(string[] updateEntries)
        {
            // retreive peptide interfaces
            PfamPeptideInterfaces pfamPepInterfaces = new PfamPeptideInterfaces();
            ProtCidSettings.logWriter.WriteLine("Update Pfam-peptide interfaces");
            ProtCidSettings.logWriter.WriteLine("#entries=" + updateEntries.Length);
            ProtCidSettings.logWriter.Flush();

            Dictionary<string, string[]> updatePfamEntryDict = pfamPepInterfaces.UpdatePfamPeptideInterfaces(updateEntries);
   //       Dictionary<string, string[]> updatePfamEntryDict = ReadUpdatePfamPepEntryHash();
            List<string> updatePfamIdList = new List<string>(updatePfamEntryDict.Keys);
            updatePfamIdList.Sort();
            string[] updatePfamIds = new string[updatePfamIdList.Count];
            updatePfamIdList.CopyTo(updatePfamIds);
            string[] updatePepEntries = GetUpdateEntriesFromHash(updatePfamEntryDict);
          
            ProtCidSettings.logWriter.WriteLine("Update Pfam-peptide in pdb meta data.");
            pfamPepInterfaces.UpdatePfamPeptideInPdbMetaData(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush ();
        
            ProtCidSettings.logWriter.WriteLine("Update Pfam-peptide coordinate interface files.");           
            // write peptide interfaces into PDB format files
            PfamPepInterfaceWriter pepInterfaceWriter = new PfamPepInterfaceWriter();  
            pepInterfaceWriter.UpdatePfamPeptideInterfaceFiles(updatePepEntries);
 //           pepInterfaceWriter.WriteDomainPeptideInterfaceFiles();
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            // calculate the SA by NACCESS
            ProtCidSettings.logWriter.WriteLine("Update surface areas of pfam-peptide interfaces.");
            DomainInterfaceSA pepInterfaceSa = new DomainInterfaceSA();
            pepInterfaceSa.domainInterfaceTableName = "PfamPeptideInterfaces";
            pepInterfaceSa.UpdateDomainInterfaceSAs(updatePepEntries);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Update peptide interface hmm sites");
            // common hmm positions
            PfamHmmSites commonHmmSites = new PfamHmmSites();
            // common hmm positions between peptide interfaces
            commonHmmSites.UpdatePeptideInterfaceHmmSites(updatePepEntries);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
            
            // Peptide-peptide RMSD
            ProtCidSettings.logWriter.WriteLine("Update peptide RMSDs");
            PeptideInterfaceRmsd pepInterfaceRmsd = new PeptideInterfaceRmsd();
            pepInterfaceRmsd.UpdateDomainInterfacePeptideRmsd(updatePfamEntryDict);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Update peptide interface clusters");
            // cluster peptide interfaces, 
            PeptideInterfaceCluster pepCluster = new PeptideInterfaceCluster();
            pepCluster.UpdatePeptideInterfaceClusters(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            // the pymol sessions for each pfam-peptide interface clusters
            ProtCidSettings.logWriter.WriteLine("Compress peptide interface files of clusters");
            PepClusterInterfaceCompress pepClusterFileCompress = new PepClusterInterfaceCompress();
            pepClusterFileCompress.UpdateClusterPeptideInterfaceFiles(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Sequences for peptide interface clusters");
            // the sequences for both pfam domains and peptides
            PfamPepSeqFasta pepSeqFasta = new PfamPepSeqFasta();
            pepSeqFasta.UpdatePfamPeptideClusterSequences(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Update peptide interface clusters");
            // the summary info for the updated pfams
            PeptideInterfaceClusterStat pepClusterStat = new PeptideInterfaceClusterStat();
            pepClusterStat.UpdatePepInterfaceClusterStat(updatePfamIds);            
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            // update pfam-peptide interfaces, align pfam domains, peptides are not aligned
            ProtCidSettings.logWriter.WriteLine("Compress all Pfam-peptide interfaces");
            PfamPepInterfaceFileCompress fileCompress = new PfamPepInterfaceFileCompress();
            fileCompress.UpdatePfamPeptideInterfaces(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Update pfam-peptide data done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingInterfaceFilesPepEntries ()
        {          
            string logFile = @"X:\Qifang\Projects\ProtCidDbProjects\pfampepErrorlog.txt";
            StreamReader dataReader = new StreamReader(logFile);
            List<string> entryList = new List<string> ();
            string line = "";
            int fileIndex = 0;
            int entryIndex = 0;
            string pdbId = "";
            while ((line = dataReader.ReadLine() ) != null)
            {
                if (line.IndexOf ("Could not find file") > -1)
                {
                    fileIndex = line.LastIndexOf("\\") + 1;
                    entryIndex = line.IndexOf("_d");
                    pdbId = line.Substring(fileIndex, entryIndex - fileIndex);
                    if (! entryList.Contains (pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            dataReader.Close();
            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamPepChainComparison(string[] updateEntries)
        {
            PfamChainInterfaceHmmSites pepChainHmmSites = new PfamChainInterfaceHmmSites();
            pepChainHmmSites.UpdateCountPfamChainInterfaceHmmSites(updateEntries);

            PeptideChainInterfaceRmsd pepChainRmsd = new PeptideChainInterfaceRmsd();
            pepChainRmsd.UpdateCalculatePeptideChainRmsd(updateEntries);
            //  pepChainRmsd.CalculatePeptideChainRmsd ();

            PepClusterInterfaceCompress pepInterfaceCompress = new PepClusterInterfaceCompress();
            pepInterfaceCompress.CompressClusterPeptidChainInterfacesFiles();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relUpdateEntryHash"></param>
        /// <returns></returns>
        private string[] GetUpdateEntriesFromHash(Dictionary<string, string[]> relUpdateEntryHash)
        {
            List<string> entryList = new List<string>();
            foreach (string pfamId in relUpdateEntryHash.Keys)
            {
                string[] updateEntries = relUpdateEntryHash[pfamId];
                foreach (string pdbId in updateEntries)
                {
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            return entryList.ToArray ();
        }

        private Dictionary<string, string[]> ReadUpdatePfamPepEntryHash ()
        {
            Dictionary<string, string[]> updatePfamEntryHash = new Dictionary<string,string[]> ();
            StreamReader dataReader = new StreamReader("UpdatePepInteractPfamEntries.txt");
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split();
                string[] entries = fields[1].Split(',');
                updatePfamEntryHash.Add(fields[0], entries);
            }
            dataReader.Close();
            return updatePfamEntryHash;
        }

        public void UpdateSomePfamPeptideInteractionsDebug ()
        {
            string[] updateEntries = GetMissingPfamPepInterfacesEntries ();
       //     string[] updateEntries = {"4tq1" };
            PfamPeptideInterfaces pfamPepInterfaces = new PfamPeptideInterfaces();
            Dictionary<string, string[]> updatePfamEntryDict = pfamPepInterfaces.UpdatePfamPeptideInterfaces(updateEntries);

    //        PeptideInterfaceClusterStat pepClusterStat1 = new PeptideInterfaceClusterStat();
    //        pepClusterStat1.AddNumCFs();


            string[] updatePfamIds = { "MHC_II_alpha"};

            ProtCidSettings.logWriter.WriteLine("Update peptide interface clusters");
            // cluster peptide interfaces, 
            PeptideInterfaceCluster pepCluster = new PeptideInterfaceCluster();
            pepCluster.UpdatePeptideInterfaceClusters(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            // the pymol sessions for each pfam-peptide interface clusters
            ProtCidSettings.logWriter.WriteLine("Compress peptide interface files of clusters");
            PepClusterInterfaceCompress pepClusterFileCompress = new PepClusterInterfaceCompress();
            pepClusterFileCompress.UpdateClusterPeptideInterfaceFiles(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Sequences for peptide interface clusters");
            // the sequences for both pfam domains and peptides
            PfamPepSeqFasta pepSeqFasta = new PfamPepSeqFasta();
            pepSeqFasta.UpdatePfamPeptideClusterSequences(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Update peptide interface clusters");
            // the summary info for the updated pfams
            PeptideInterfaceClusterStat pepClusterStat = new PeptideInterfaceClusterStat();
            pepClusterStat.UpdatePepInterfaceClusterStat(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            // update pfam-peptide interfaces, align pfam domains, peptides are not aligned
            ProtCidSettings.logWriter.WriteLine("Compress all Pfam-peptide interfaces");
            PfamPepInterfaceFileCompress fileCompress = new PfamPepInterfaceFileCompress();
            fileCompress.UpdatePfamPeptideInterfaces(updatePfamIds);
            ProtCidSettings.logWriter.WriteLine("Update pfam-peptide data done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingPfamPepInterfacesEntries ()
        {
            StreamWriter entryWriter = new StreamWriter("NoPeptideInterfaceEntryList.txt");
            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable chainEntryTable = ProtCidSettings.buCompQuery.Query(queryString);

            queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> pepEntryList = new List<string>();
            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pepEntryList.Add(entryRow["PdbID"].ToString());
            }
            pepEntryList.Sort();
            List<string> noPepEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in chainEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (pepEntryList.BinarySearch (pdbId) > -1)
                {
                    continue;
                }
                noPepEntryList.Add(pdbId);
                entryWriter.WriteLine(pdbId);
            }
            entryWriter.Close();
            return noPepEntryList.ToArray();
        }
        #endregion
    }
}
