using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using PfamLib.PfamArch;
using PfamLib.Settings;

namespace InterfaceClusterLib.AuxFuncs
{
    public class UpdateEntriesDifVersionPfams
    {
        private DbQuery dbQuery = new DbQuery();
        private DbConnect oldDbConnect = null;
        private string oldPdbfamDb = @"X:\Firebird\Pfam30\pdbfam.fdb";
        private PfamArchitecture pfamArch = new PfamArchitecture();
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetUpdateEntriesFromPfamUpdate()
        {
            DbQuery dbQuery = new DbQuery();
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb_pfamupdate.txt"));
            string queryString = "Select Distinct PdbID From PdbPfam Where IsSame = -1;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] updateEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                dataWriter.WriteLine(entryRow["PdbID"].ToString());
                updateEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            dataWriter.Close();
            return updateEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetUpdateEntriesDifEntryPfamArch()
        {
            string updatePfamArchEntryFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb_entryPfamArchUpdate.txt");
            List<string> updatePdbList = new List<string>();
            if (File.Exists(updatePfamArchEntryFile))
            {
                StreamReader dataReader = new StreamReader(updatePfamArchEntryFile);
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    updatePdbList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                oldDbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + oldPdbfamDb);
                string[] pdbIds = GetUpdateEntriesFromPfamUpdate();

                DbConnect currentDbConnect = PfamLibSettings.pdbfamConnection;

                Dictionary<string, string> entryOldPfamArchDict = GetEntryPfamArch(pdbIds, oldDbConnect);
                oldDbConnect.DisconnectFromDatabase();
                Dictionary<string, string> entryNewPfamArchDict = GetEntryPfamArch(pdbIds, currentDbConnect);

                updatePdbList = new List<string>(pdbIds);

                foreach (string pdbId in pdbIds)
                {
                    if (entryNewPfamArchDict.ContainsKey(pdbId) && entryOldPfamArchDict.ContainsKey(pdbId))
                    {
                        if (entryNewPfamArchDict[pdbId] == entryOldPfamArchDict[pdbId])
                        {
                            updatePdbList.Remove(pdbId);
                        }
                    }
                }
                StreamWriter dataWriter = new StreamWriter(updatePfamArchEntryFile);
                foreach (string pdbId in updatePdbList)
                {
                    dataWriter.WriteLine(pdbId);
                }
                dataWriter.Close();
            }
            return updatePdbList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntryPfamArch (string[] pdbIds, DbConnect dbConnect)
        {
            PfamLibSettings.pdbfamConnection = dbConnect;
            string entryPfamArch = "";
            Dictionary<string, string> entryPfamArchDict = new Dictionary<string, string>();
            foreach (string pdbId in pdbIds)
            {
                entryPfamArch = pfamArch.GetEntryGroupPfamArch(pdbId);
                entryPfamArchDict.Add(pdbId, entryPfamArch);
            }
            return entryPfamArchDict;
        }
    }
}
