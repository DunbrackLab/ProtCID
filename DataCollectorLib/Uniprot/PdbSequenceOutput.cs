using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using XtalLib.Settings;
using AuxFuncLib;

namespace DataCollectorLib.Uniprot
{
    public class PdbSequenceOutput
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        #endregion

        #region output pdb unique sequences
        public void PrintNonredundantPdbChainSequences()
        {
            Initialize();

            StreamWriter dataWriter = new StreamWriter("NonredundantPdbEntitySequences.txt");
            string queryString = "Select Distinct PdbID, EntityID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable protSequenceTable = dbQuery.Query(queryString);
            string pdbId = "";
            int entityId = -1;
            string entryEntity = "";
            string repEntryEntity = "";
            string sequence = "";
            string dataLine = "";
            ArrayList parsedEntryEntityList = new ArrayList();
            foreach (DataRow protSeqRow in protSequenceTable.Rows)
            {
                pdbId = protSeqRow["PdbID"].ToString();
                entityId = Convert.ToInt32(protSeqRow["EntityID"].ToString());
                entryEntity = pdbId + entityId.ToString();
                if (parsedEntryEntityList.Contains(entryEntity))
                {
                    continue;
                }
                string[] redundantEntryEntities = GetRedudantPdbEntities(pdbId, entityId);
                parsedEntryEntityList.AddRange(redundantEntryEntities);
                repEntryEntity = redundantEntryEntities[0];
                sequence = GetEntryEntitySequence(repEntryEntity.Substring(0, 4), Convert.ToInt32(repEntryEntity.Substring(4, repEntryEntity.Length - 4)));
                dataLine = ">" + entryEntity;
                if (redundantEntryEntities.Length > 1)
                {
                    dataLine += " ||";
                }
                foreach (string entity in redundantEntryEntities)
                {
                    if (entity != entryEntity)
                    {
                        dataLine += (" " + entity);
                    }
                }
                dataWriter.WriteLine(dataLine);
                dataWriter.WriteLine(sequence);
                dataWriter.Flush();
            }
            AppSettings.alignmentDbConnection.DisconnectFromDatabase();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void SeparateSequenceFile()
        {
            StreamReader dataReader = new StreamReader("NonredundantPdbEntitySequences.txt");
            string line = "";
            int numOfLinesInFile = 10000;
            int numOfLines = 0;
            int fileNum = 0;
            StreamWriter dataWriter = new StreamWriter("PdbEntitySequences" + fileNum.ToString() + ".txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                if (numOfLines == numOfLinesInFile)
                {
                    dataWriter.Close();
                    numOfLines = 0;
                    fileNum++;
                    dataWriter = new StreamWriter("PdbEntitySequences" + fileNum.ToString() + ".txt");
                }
                dataWriter.WriteLine(line);
                numOfLines++;
            }
            dataReader.Close();
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetRedudantPdbEntities(string pdbId, int entityId)
        {
            ArrayList redundantSeqList = new ArrayList();
            string repEntity = GetRepresentativeEntity(pdbId, entityId);
            if (repEntity == "")
            {
                redundantSeqList.Add(pdbId + entityId.ToString());
            }
            else
            {
                string queryString = string.Format("Select * From RedundantPdbChains " +
                    " WHere PdbID1 = '{0}' AND EntityID1 = {1};", repEntity.Substring(0, 4), repEntity.Substring(4, repEntity.Length - 4));
                DataTable redundantSeqTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);

                redundantSeqList.Add(repEntity);
                string entryEntity = "";
                foreach (DataRow redundantSeqRow in redundantSeqTable.Rows)
                {
                    if (redundantSeqRow["PdbID2"].ToString().TrimEnd() == "-")
                    {
                        continue;
                    }
                    if (redundantSeqRow["PdbID1"].ToString() == redundantSeqRow["PdbID2"].ToString()) // must belong to the same entity
                    {
                        continue;
                    }
                    entryEntity = redundantSeqRow["PdbID2"].ToString() + redundantSeqRow["EntityID2"].ToString();
                    if (!redundantSeqList.Contains(entryEntity))
                    {
                        redundantSeqList.Add(entryEntity);
                    }
                }
            }
            string[] redundantEntryEntities = new string[redundantSeqList.Count];
            redundantSeqList.CopyTo(redundantEntryEntities);
            return redundantEntryEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetRepresentativeEntity(string pdbId, int entityId)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " +
                " WHere (PdbID1 = '{0}' AND EntityID1 = {1}) OR (PdbID2 = '{0}' AND EntityID2 = {1});", pdbId, entityId);
            DataTable repEntityTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (repEntityTable.Rows.Count > 0)
            {
                return repEntityTable.Rows[0]["PdbID1"].ToString() + repEntityTable.Rows[0]["EntityID1"].ToString();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntryEntitySequence(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable sequenceTable = dbQuery.Query(queryString);
            return sequenceTable.Rows[0]["Sequence"].ToString().TrimEnd();
        }
        #endregion

        #region output PDB sequences
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryEntities"></param>
        public void OutputPdbEntryEntitySequences(string[] entryEntities)
        {
            StreamWriter dataWriter = new StreamWriter("newPdbSequences.txt");
            string sequence = "";
            foreach (string entryEntity in entryEntities)
            {
                sequence = GetEntryEntitySequence(entryEntity.Substring(0, 4),
                    Convert.ToInt32(entryEntity.Substring(4, entryEntity.Length - 4)));
                if (ParseHelper.IsSequenceValid (sequence))
                {
                    dataWriter.WriteLine(">" + entryEntity);
                    dataWriter.WriteLine(sequence);
                }
            }
            dataWriter.Close();
        }
        #endregion
        /// <summary>
        /// 
        /// </summary>
        private void Initialize()
        {
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
                DbBuilder.dbConnect.ConnectString =
                    "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath; 
            }

            if (AppSettings.alignmentDbConnection == null)
            {
                AppSettings.alignmentDbConnection = new DbConnect();
            }
            AppSettings.alignmentDbConnection.ConnectString =
                 "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.alignmentDbPath;

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.currentOperationLabel = "Retrieve PDB sequences";
        }
    }
}
