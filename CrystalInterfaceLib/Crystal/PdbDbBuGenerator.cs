using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.Crystal
{
    public class PdbDbBuGenerator : BuGenerator
    {
        private DbQuery dbQuery = new DbQuery();
 
        #region build PDB BUs from DB
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBUsFromDb(string pdbId, EntryCrystal thisEntryCrystal, bool needLigands)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> buComponentInfoHash = GetPdbBuComponentsFromDb(pdbId, needLigands);
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuHash = BuildPdbBUs(pdbId, buComponentInfoHash, thisEntryCrystal, needLigands);
            return entryBuHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBUsFromDb(string pdbId, string[] buIDs, EntryCrystal thisEntryCrystal, bool needLigands)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> buComponentInfoHash = GetPdbBuComponentsFromDb(pdbId, buIDs, needLigands);
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuHash = BuildPdbBUs(pdbId, buComponentInfoHash, thisEntryCrystal, needLigands);
            return entryBuHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buComponentInfoHash"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBUs(string pdbId, Dictionary<string, Dictionary<string, List<SymOpMatrix>>> buComponentInfoHash, EntryCrystal thisEntryCrystal, bool needLigands)
        {
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuChainAtomsHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
            foreach (string buId in buComponentInfoHash.Keys)
            {
                Dictionary<string, List<SymOpMatrix>> buChainSymMatrixHash = buComponentInfoHash[buId];
                Dictionary<string, AtomInfo[]> buChainAtomsHash = BuildOneBuAssembly(thisEntryCrystal, buChainSymMatrixHash, "pdb");
                entryBuChainAtomsHash.Add(buId, buChainAtomsHash);
            }
            return entryBuChainAtomsHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetPdbBuComponentsFromDb(string pdbId, bool needLigands)
        {
            string queryString = "";
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> buComponentInfoHash = null;
            if (needLigands)
            {
                queryString = string.Format("Select * From PdbBuGen Where PdbID = '{0}';", pdbId);
            }
            else
            {
                queryString = string.Format("Select PdbBuGen.* From PdbBuGen, AsymUnit" +
                    " Where PdbBuGen.PdbID = '{0}' AND AsymUnit.PdbID = '{0}' AND " +
                    " PdbBuGen.AsymID = AsymUnit.AsymID AND PolymerType = 'polypeptide';", pdbId);
            }
            DataTable buGenTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (buGenTable.Rows.Count > 0)
            {
                buComponentInfoHash = GetEntryBuComponentInfoHash(buGenTable);
            }
            else
            {
                buComponentInfoHash = GetPdbAsuComponentsFromDb(pdbId, needLigands);
            }
            return buComponentInfoHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetPdbAsuComponentsFromDb(string pdbId, bool needLigands)
        {
            string queryString = "";
            if (needLigands)
            {
                queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' and PolymerType <> 'water';", pdbId);
            }
            else
            {
                queryString = string.Format("Select AsymID From AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            }
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> asuInfoHash = GetEntryAsuComponentInfoHash(asuTable);
            return asuInfoHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIDs"></param>
        /// <param name="needLigands"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetPdbBuComponentsFromDb(string pdbId, string[] buIDs, bool needLigands)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> buComponentInfoHash = null;

             if (buIDs.Length == 0)
             {
                 buComponentInfoHash = GetPdbAsuComponentsFromDb(pdbId, needLigands);
             }
             else
             {
                 string queryString = "";
                 if (needLigands)
                 {
                     queryString = string.Format("Select * From PdbBuGen Where PdbID = '{0}' AND BiolUnitID IN ({1});",
                         pdbId, ParseHelper.FormatSqlListString(buIDs));
                 }
                 else
                 {
                     queryString = string.Format("Select PdbBuGen.* From PdbBuGen, AsymUnit" +
                         " Where PdbBuGen.PdbID = '{0}' AND AsymUnit.PdbID = '{0}' AND " +
                         " PdbBuGen.BiolUnitID IN ({1}) AND " +
                         " PdbBuGen.AsymID = AsymUnit.AsymID AND PolymerType = 'polypeptide';",
                         pdbId, ParseHelper.FormatSqlListString(buIDs));
                 }
                 DataTable buGenTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                 if (buGenTable.Rows.Count > 0)
                 {
                     buComponentInfoHash = GetEntryBuComponentInfoHash(buGenTable);
                 }
                 else
                 {
                     buComponentInfoHash = GetPdbAsuComponentsFromDb(pdbId, needLigands);
                 }
             }
            return buComponentInfoHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buGenTable"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetEntryBuComponentInfoHash(DataTable buGenTable)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> pdbBuComponentInfoHash = new Dictionary<string, Dictionary<string, List<SymOpMatrix>>>();
            string buId = "";
            string asymChain = "";
            string matrixString = "";
            SymOpMatrix symOpMatrix = null;
            foreach (DataRow buGenRow in buGenTable.Rows)
            {
                buId = buGenRow["BiolUnitID"].ToString().TrimEnd();
                asymChain = buGenRow["AsymID"].ToString().Trim();
                matrixString = buGenRow["SymmetryMatrix"].ToString().Trim();
                symOpMatrix = GetCoordSymOpMatrix(matrixString);
                symOpMatrix.symmetryString = buGenRow["SymmetryString"].ToString().TrimEnd();
                symOpMatrix.symmetryOpNum = buGenRow["SymOpNum"].ToString().TrimEnd ();

                if (pdbBuComponentInfoHash.ContainsKey(buId))
                {
                    Dictionary<string, List<SymOpMatrix>> chainMatrixHash = pdbBuComponentInfoHash[buId];
                    if (chainMatrixHash.ContainsKey(asymChain))
                    {
                        chainMatrixHash[asymChain].Add(symOpMatrix);
                    }
                    else
                    {
                        List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
                        matrixList.Add(symOpMatrix);
                        chainMatrixHash.Add(asymChain, matrixList);
                    }
                }
                else
                {
                    Dictionary<string, List<SymOpMatrix>> chainMatrixHash = new Dictionary<string, List<SymOpMatrix>>();
                    List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
                    matrixList.Add(symOpMatrix);
                    chainMatrixHash.Add(asymChain, matrixList);
                    pdbBuComponentInfoHash.Add(buId, chainMatrixHash);
                }
            }
            return pdbBuComponentInfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetEntryAsuComponentInfoHash(DataTable asuTable)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> asuInfoHash = new Dictionary<string,Dictionary<string,List<SymOpMatrix>>> ();
            Dictionary<string, List<SymOpMatrix>> chainMatrixHash = new Dictionary<string,List<SymOpMatrix>> ();
            string buId = "1";
            string asymChain = "";
            SymOpMatrix symOpMatrix = null;
            foreach (DataRow asuRow in asuTable.Rows)
            {
                asymChain = asuRow["AsymID"].ToString().TrimEnd();
                symOpMatrix = GetOrigCoordSymOpMatrix();
                symOpMatrix.symmetryString ="1_555";
                symOpMatrix.symmetryOpNum = "1";

                if (chainMatrixHash.ContainsKey(asymChain))
                {
                    chainMatrixHash[asymChain].Add(symOpMatrix);
                }
                else
                {
                    List<SymOpMatrix> matrixList =new List<SymOpMatrix> ();
                    matrixList.Add(symOpMatrix);
                    chainMatrixHash.Add(asymChain, matrixList);
                }
            }
            asuInfoHash.Add(buId, chainMatrixHash);
            return asuInfoHash;
        }
        #endregion
    }
}
