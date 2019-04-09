using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using BuCompLib.BuInterfaces;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.Contacts;
using AuxFuncLib;

namespace BuCompLib.PfamInteract
{
    public class PfamInteraction
    {
        #region member variables
        private EntryBuInterfaces buInterfaces = new EntryBuInterfaces();
        private BuDomainInterfaces domainInterfaces = new BuDomainInterfaces();
        private AsuIntraChainDomainInterfaces intraChainInterfaces = new AsuIntraChainDomainInterfaces();
        private DbQuery dbQuery = new DbQuery();
        #endregion

        #region pfam relation in the pdb
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamRelationshipInPdbAsuAndBUs()
        {
            BuCompBuilder.BuType = "asu";
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }

            string[] entries = GetEntries();
       //     string[] entries = GetMissingEntries();

            StreamWriter dataWriter = new StreamWriter("PfamDomain\\PfamDomainInteractionsInAsu_left.txt", true);
            StreamWriter monomerWriter = new StreamWriter("PfamDomain\\Monomers_asu_left.txt", true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            if (BuCompBuilder.BuType == "pdb")
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain-domain relations in PDB biological units.");
            }
            else if (BuCompBuilder.BuType == "asu")
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain-domain relations in ASU.");
            }
            ProtCidSettings.progressInfo.totalStepNum = entries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entries.Length;

            Dictionary<string, int> interPfamRelEntryHash = new Dictionary<string, int> ();
            Dictionary<string, int> interPfamRelBuHash = new Dictionary<string,int> ();
            Dictionary<string, int> intraPfamRelEntryHash = new Dictionary<string,int> ();
            Dictionary<string, int> intraPfamrelChainHash = new Dictionary<string,int> ();
            List<string> pfamRelationList = new List<string>  ();
            string[] multimerBuIds = null;
       //     foreach (DataRow entryRow in entryTable.Rows)
            foreach (string pdbId in entries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    if (BuCompLib.BuCompBuilder.BuType != "asu")
                    {
                        multimerBuIds = buInterfaces.GetAllPdbDefinedMultimerBiolUnits(pdbId);
                    }

                    if (multimerBuIds != null &&  multimerBuIds.Length == 0)
                    {
                        monomerWriter.WriteLine(pdbId);
                        monomerWriter.Flush();
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " no multimer bu");
                    }
                    else
                    {
                        Dictionary<string, InterfaceChains[]> buInterfacesHash = buInterfaces.GetAllBuInterfacesInEntry(pdbId, multimerBuIds);

                        Dictionary<string, List<string>> pfamRelBuHash =
                            domainInterfaces.GetEntryBuDomainInterfaces(pdbId, buInterfacesHash, dataWriter);
                        AddPfamRelHashToTotal(pfamRelBuHash, interPfamRelEntryHash, interPfamRelBuHash, ref pfamRelationList);
                    }
                    Dictionary<string, List<string>> pfamRelChainHash =
                        intraChainInterfaces.GetIntraChainDomainInterfaces(pdbId, dataWriter);
                    AddPfamRelHashToTotal(pfamRelChainHash, intraPfamRelEntryHash, intraPfamrelChainHash, ref pfamRelationList);
                }
                catch (Exception ex)
                {
         //           WritePfamRelSumInfoToFile(pfamRelationList, interPfamRelEntryHash, interPfamRelBuHash,
        //                    intraPfamRelEntryHash, intraPfamrelChainHash, sumInfoFileName);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " " + ex.Message);
                }
            }
            dataWriter.Close();
            monomerWriter.Close();
           
        //    WritePfamRelSumInfoToFile(pfamRelationList, interPfamRelEntryHash, interPfamRelBuHash,
        //        intraPfamRelEntryHash, intraPfamrelChainHash, sumInfoFileName);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// /
        /// </summary>
        /// <returns></returns>
        private string[] GetEntries()
        {
            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable entryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }

        private string[] GetMissingEntries()
        {
            StreamReader entryReader = new StreamReader("PfamDomain\\PfamInteractions\\EntriesIndidNotAsu.txt");
            List<string> entryList = new List<string> ();
            string line = "";
            while ((line = entryReader.ReadLine()) != null)
            {
                entryList.Add(line);
            }
            entryReader.Close();
            string[] entries = new  string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamRelCountHash"></param>
        /// <param name="totalPfamRelCountHash"></param>
        private void AddPfamRelHashToTotal(Dictionary<string, List<string>> pfamRelCountHash, Dictionary<string, int> totalPfamRelEntryHash, 
            Dictionary<string, int> totalPfamRelBuHash, ref List<string> pfamRelationList)
        {
            foreach (string pfamRel in pfamRelCountHash.Keys)
            {
                if (totalPfamRelEntryHash.ContainsKey(pfamRel))
                {
                    int count = totalPfamRelEntryHash[pfamRel];
                    count++;
                    totalPfamRelEntryHash[pfamRel] = count;
                }
                else
                {
                    totalPfamRelEntryHash.Add(pfamRel, 1);
                }
                int buCount = pfamRelCountHash[pfamRel].Count;
                if (totalPfamRelBuHash.ContainsKey(pfamRel))
                {
                    int count = (int)totalPfamRelBuHash[pfamRel];
                    count = count + buCount;
                    totalPfamRelBuHash[pfamRel] = count;
                }
                else
                {
                    totalPfamRelBuHash.Add(pfamRel, buCount);
                }
                if (! pfamRelationList.Contains(pfamRel))
                {
                    pfamRelationList.Add(pfamRel);
                }
            }
        }     
        #endregion

    }
}
