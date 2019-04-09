using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace AuxFuncLib
{
    public class HmmInfoFinder
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public int GetPfamHmmLength(string pfamName)
        {
            string queryString = "";
            if (IsInputPfamAccessionCode(pfamName))
            {
                queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_Acc = '{0}';",
                     pfamName);
            }
            else
            {
                queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_ID = '{0}';",
                     pfamName);
            }
            DataTable modelLengthTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            int modelLength = -1;
            if (modelLengthTable.Rows.Count > 0)
            {
                modelLength = Convert.ToInt32(modelLengthTable.Rows[0]["ModelLength"].ToString ());
            }
            return modelLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public int GetPfamHmmLengthFromID(string pfamId)
        {
            string queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_ID = '{0}';",
                     pfamId);
            DataTable modelLengthTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            int modelLength = -1;
            if (modelLengthTable.Rows.Count > 0)
            {
                modelLength = Convert.ToInt32(modelLengthTable.Rows[0]["ModelLength"].ToString());
            }
            return modelLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        public int GetPfamHmmLengthFromAccessionCode (string pfamAcc)
        {
            string queryString = string.Format("Select ModelLength From PfamHmm Where Pfam_Acc = '{0}';",
                     pfamAcc);
            DataTable modelLengthTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            int modelLength = -1;
            if (modelLengthTable.Rows.Count > 0)
            {
                modelLength = Convert.ToInt32(modelLengthTable.Rows[0]["ModelLength"].ToString());
            }
            return modelLength;
        }

        /// <summary>
        /// the current PFAM A: PF00000 7-character long
        /// PFAM B: PB000000 8-character long
        /// </summary>
        /// <param name="pfamName"></param>
        /// <returns></returns>
        public bool IsInputPfamAccessionCode(string pfamName)
        {
            if (pfamName.Length < 7)
            {
                return false;
            }
            string begName = pfamName.Substring(0, 2).ToUpper ();
            if (begName != "PF" && begName != "PB")
            {
                return false;
            }
            for (int i = 2; i < pfamName.Length; i++)
            {
                if (! char.IsDigit(pfamName[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
