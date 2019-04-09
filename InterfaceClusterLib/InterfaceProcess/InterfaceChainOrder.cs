using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using PfamLib.PfamArch;

namespace InterfaceClusterLib.InterfaceProcess
{
    /// <summary>
    /// the symmetry of the homo-interfaces or 
    /// the chain-order of hetero-interfaces
    /// </summary>
    public class InterfaceChainOrder
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private InterfaceRetriever interfaceReader = new InterfaceRetriever();
        private PfamArchitecture pfamArch = new PfamArchitecture();
        #endregion

        public void CheckInterfaceChainOrder()
        {
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int[] samePfamArchInterfaces = null;
            List<DataRow> difPfamArchInterfaceRowList = null;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                GetEntryInterfacesPfamArch(pdbId, out samePfamArchInterfaces, out difPfamArchInterfaceRowList);
                if (samePfamArchInterfaces.Length > 0)
                {
                    SetNumOfCommonContactsInInterfaces(pdbId, samePfamArchInterfaces);
                }
                if (difPfamArchInterfaceRowList.Count > 0)
                {
                    FindEntryInterfacesInDifPfamOrder(pdbId, difPfamArchInterfaceRowList);
                }
            }
        }

        #region interface symmetry
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private void SetNumOfCommonContactsInInterfaces(string pdbId, int[] samePfamArchInterfaces)
        {
            List<int> contactNotInDbInterfaceList = new List<int> ();
            foreach (int interfaceId in samePfamArchInterfaces)
            {
                int[] numOfContacts = GetNumOfCommonContactsFromDb(pdbId, interfaceId);
                if (numOfContacts == null)
                {
                    contactNotInDbInterfaceList.Add(interfaceId);
                }
                else
                {
                    UpdateInterfaceInDb(pdbId, interfaceId, numOfContacts[0], numOfContacts[1]);
                }
            }

            int[] interfaceIdsContactsNotInDb =  contactNotInDbInterfaceList.ToArray ();
            SetNumOfContactsFromFile(pdbId, interfaceIdsContactsNotInDb);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <returns></returns>
        private void SetNumOfContactsFromFile(string pdbId, int[] interfaceIds)
        {
            InterfaceChains[] interfaceChains = interfaceReader.GetCrystInterfaces(pdbId, interfaceIds, "cryst");

            List<string> chainAContactList = new List<string>();
            List<string> chainBContactList = new List<string>();
            int numOfCommonContacts = -1;
            int totalContacts = -1;
            foreach (InterfaceChains chainInterface in interfaceChains)
            {
                chainAContactList.Clear();
                chainBContactList.Clear();
                foreach (string seqPair in interfaceChains[0].seqDistHash.Keys)
                {
                    string[] seqIds = seqPair.Split('_');
                    chainAContactList.Add(seqIds[0] + "_" + seqIds[1]);
                    chainBContactList.Add(seqIds[1] + "_" + seqIds[0]);
                }
                numOfCommonContacts = GetNumOfCommonContacts(chainAContactList, chainBContactList);
                totalContacts = interfaceChains[0].seqDistHash.Count;

                UpdateInterfaceInDb(pdbId, chainInterface.interfaceId, numOfCommonContacts, totalContacts);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int[] GetNumOfCommonContactsFromDb(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select SeqID1, SeqID2 From SgInterfaceResidues " +
               " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable contactsTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (contactsTable.Rows.Count == 0)
            {
                return null;
            }
            List<string> chainAContactList = new List<string>();
            List<string> chainBContactList = new List<string>();
            string seqA = "";
            string seqB = "";
            foreach (DataRow contactRow in contactsTable.Rows)
            {
                seqA = contactRow["SeqID1"].ToString().TrimEnd();
                seqB = contactRow["SeqID2"].ToString().TrimEnd();
                chainAContactList.Add(seqA + "_" + seqB);
                chainBContactList.Add(seqB + "_" + seqA);
            }
            int numOfCommonContacts = GetNumOfCommonContacts(chainAContactList, chainBContactList);
            int[] numOfContacts = new int[2];
            numOfContacts[0] = numOfCommonContacts;
            numOfContacts[1] = chainAContactList.Count;
            return numOfContacts;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainAContactList"></param>
        /// <param name="chainBContactList"></param>
        /// <returns></returns>
        private int GetNumOfCommonContacts(List<string> chainAContactList, List<string> chainBContactList)
        {
            int numOfCommonContacts = 0;
            foreach (string seqPairA in chainAContactList)
            {
                if (chainBContactList.Contains(seqPairA))
                {
                    numOfCommonContacts++;
                }
            }
            return numOfCommonContacts;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="numOfCommonContacts"></param>
        /// <param name="numOfContacts"></param>
        private void UpdateInterfaceInDb(string pdbId, int interfaceId, int numOfCommonContacts, int numOfContacts)
        {
            string updateString = string.Format("Update CrystEntryInterfaces " + 
                " Set NumCommonContacts = {0}, NumContacts = {1} Where PdbID = '{2}' AND InterfaceID = {3};",
                numOfCommonContacts, numOfContacts, pdbId, interfaceId);
            ProtCidSettings.protcidQuery.Query( updateString);
        }
        #endregion

        #region chain order of hetero-interfaces
        /// <summary>
        /// 
        /// </summary>
        private void FindEntryInterfacesInDifPfamOrder(string pdbId, List<DataRow> difPfamArchInterfaceRowList)
        {
            foreach (DataRow reversedInterfaceRow in difPfamArchInterfaceRowList)
            {
                ReverseInterfaceRowInDb(reversedInterfaceRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceRow"></param>
        private void ReverseInterfaceRowInDb(DataRow interfaceRow)
        {
            object tempObj = interfaceRow["AsymChain1"];
            interfaceRow["AsymChain1"] = interfaceRow["AsymChain2"];
            interfaceRow["AsymChain2"] = tempObj;

            tempObj = interfaceRow["AuthChain1"];
            interfaceRow["AuthChain1"] = interfaceRow["AuthChain2"];
            interfaceRow["AuthChain2"] = tempObj;

            tempObj = interfaceRow["EntityID1"];
            interfaceRow["EntityID1"] = interfaceRow["EntityID2"];
            interfaceRow["EntityID2"] = tempObj;

            tempObj = interfaceRow["SymmetryString1"];
            interfaceRow["SymmetryString1"] = interfaceRow["SymmetryString2"];
            interfaceRow["SymmetryString2"] = tempObj;

            tempObj = interfaceRow["FullSymmetryString1"];
            interfaceRow["FullSymmetryString1"] = interfaceRow["FullSymmetryString2"];
            interfaceRow["FullSymmetryString2"] = tempObj;

            DeleteEntryInterface(interfaceRow["PdbID"].ToString(), Convert.ToInt32(interfaceRow["InterfaceID"].ToString()));
            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, interfaceRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        private void DeleteEntryInterface(string pdbId, int interfaceId)
        {
            string deleteString = string.Format("Delete From CrystEntryInterfaces Where PdbId = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        
        #endregion

        #region interface pfam arch
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="samePfamArchInterfaces"></param>
        /// <param name="difPfamArchInterfaces"></param>
        private void GetEntryInterfacesPfamArch(string pdbId, out int[] samePfamArchInterfaces, out List<DataRow> difPfamArchInterfaceList)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceTable.TableName = "CrystEntryInterfaces";
            Dictionary<int, string> chainPfamArchHash = null;
            List<int> samePfamArchInterfaceList = new List<int> ();
            difPfamArchInterfaceList = new List<DataRow> ();
            int interfaceId = -1;
            string pfamArch1 = "";
            string pfamArch2 = "";
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                if (interfaceRow["EntityID1"].ToString() == interfaceRow["EntityID2"].ToString())
                {
                    samePfamArchInterfaceList.Add(interfaceId);
                }
                else
                {
                    if (chainPfamArchHash == null)
                    {
                        chainPfamArchHash = pfamArch.GetEntryEntityPfamArchHash (pdbId);
                    }
                    pfamArch1 = chainPfamArchHash[Convert.ToInt32 (interfaceRow["EntityId1"].ToString ())];
                    pfamArch2 = chainPfamArchHash[Convert.ToInt32 (interfaceRow["EntityId2"].ToString ())];
                    int pfamArchComp = string.Compare(pfamArch1, pfamArch2);
                    if (pfamArchComp == 0)
                    {
                        samePfamArchInterfaceList.Add(interfaceId);
                    }
                    else if (pfamArchComp > 1)
                    {
                        difPfamArchInterfaceList.Add(interfaceRow);
                    }
                }
            }
            samePfamArchInterfaces = samePfamArchInterfaceList.ToArray ();
        }
        #endregion
    }
}
