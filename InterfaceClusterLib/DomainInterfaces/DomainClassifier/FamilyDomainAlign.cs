using System;
using System.Data;
using System.Collections.Generic;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Representative domain alignments by Mammoth
	/// </summary>
	public class FamilyDomainAlign
	{
		#region member variables
		private DbQuery dbQuery = new DbQuery ();
		private DbInsert dbInsert = new DbInsert ();
        private DomainAlignment domainAlignInfo = new DomainAlignment();
		#endregion

		public FamilyDomainAlign()
		{
		}
		#region retrieve domain alignments for each rel group
		/// <summary>
		/// the domain alignments for representative domain interfaces
		/// </summary>
		public void GetDomainAlignments ()
		{
			string queryString = string.Format ("SELECT DISTINCT RelSeqID FROM {0}DomainInterfaces;", 
                ProtCidSettings.dataType);
            DataTable seqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
			int relSeqId = -1;
			string familyCode1 = "";
			string familyCode2 = "";
            DataTable alignInfoTable = null;
			foreach (DataRow seqIdRow in seqIdTable.Rows)
			{
				relSeqId = Convert.ToInt32 (seqIdRow["RelSeqID"].ToString ());
				familyCode1 = seqIdRow["Family1"].ToString ().Trim ();
				familyCode2 = seqIdRow["Family2"].ToString ().Trim ();
				if (familyCode1 == familyCode2)
				{
					alignInfoTable = GetSameFamilyDomainAlignments (relSeqId, familyCode1);
				}
				else
				{
					alignInfoTable = GetDifFamilyDomainAlignments (relSeqId, familyCode1, familyCode2);
				}
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, alignInfoTable);
			}
		}

		/// <summary>
		/// same family group
		/// </summary>
		/// <param name="relSeqId"></param>
		/// <param name="familyCode"></param>
		private DataTable GetSameFamilyDomainAlignments (int relSeqId, string familyCode)
		{
		//	int[] domainList = GetDomainList (familyCode);
		//	Array.Sort (domainList);

			string queryString = string.Format 
				("SELECT DISTINCT DomainID1, DomainID2 FROM {0}DomainInterfaces Where RelSeqID = {1};", 
                ProtCidSettings.dataType, relSeqId);
            DataTable domainPairTable = ProtCidSettings.protcidQuery.Query( queryString);
			long DomainID1 = -1;
			long DomainID2 = -1;
			List<long> repDomainList = new List<long> ();
			foreach (DataRow dRow in domainPairTable.Rows)
			{
				DomainID1 = Convert.ToInt64 (dRow["DomainID1"].ToString ());
				DomainID2 = Convert.ToInt64 (dRow["DomainID2"].ToString ());
				// group representative domains based on family codes
				if (! repDomainList.Contains (DomainID1))
				{
					repDomainList.Add (DomainID1);
				}
				if (! repDomainList.Contains (DomainID2))
				{
					repDomainList.Add (DomainID2);
				}
			}
			// alignments within a scop family
            DataTable domainAlignInfoTable = domainAlignInfo.GetDomainAlignments (repDomainList.ToArray ());
            domainAlignInfoTable.TableName = ProtCidSettings.dataType + "DomainRepAlign";
            return domainAlignInfoTable;
		}

		/// <summary>
		/// different family code combination
		/// </summary>
		/// <param name="relSeqId"></param>
		/// <param name="familyCode1"></param>
		/// <param name="familyCode2"></param>
		private DataTable GetDifFamilyDomainAlignments (int relSeqId, string familyCode1, string familyCode2)
		{
			long[] domainList1 = GetDomainList (familyCode1);
			Array.Sort (domainList1);
			long[] domainList2 = GetDomainList (familyCode2);
			Array.Sort (domainList2);

			string queryString = string.Format 
				("SELECT DISTINCT DomainID1, DomainID2 FROM {0}DomainInterfaces Where RelSeqID = {1};", 
                ProtCidSettings.dataType, relSeqId);
            DataTable domainPairTable = ProtCidSettings.protcidQuery.Query( queryString);
			long DomainID1 = -1;
            long DomainID2 = -1;
			List<long> repDomainList1 = new List<long> ();
			List<long> repDomainList2 = new List<long> ();
			foreach (DataRow dRow in domainPairTable.Rows)
			{
				DomainID1 = Convert.ToInt64 (dRow["DomainID1"].ToString ());
				DomainID2 = Convert.ToInt64 (dRow["DomainID2"].ToString ());
				// group representative domains based on family codes
				GetFamilyNum (domainList1, domainList2, DomainID1, ref repDomainList1, ref repDomainList2);
				GetFamilyNum (domainList1, domainList2, DomainID2, ref repDomainList1, ref repDomainList2);	
			}
			// alignments within a scop family
            long[] repDomains1 = new long[repDomainList1.Count];
            repDomainList1.CopyTo(repDomains1);
            DataTable alignInfoTable = domainAlignInfo.GetDomainAlignments(repDomains1);
            long[] repDomains2 = new long[repDomainList2.Count];
            repDomainList2.CopyTo(repDomains2);
            DataTable alignInfoTable2 = domainAlignInfo.GetDomainAlignments(repDomains2);
            foreach (DataRow alignInfoRow in alignInfoTable2.Rows)
            {
                DataRow dataRow = alignInfoTable.NewRow();
                dataRow.ItemArray = alignInfoRow.ItemArray;
                alignInfoTable.Rows.Add(dataRow);
            }
            alignInfoTable.TableName = ProtCidSettings.dataType + "DomainRepAlign";
            return alignInfoTable;
		}
		/// <summary>
		/// add DomainID to its corresponding family domain list
		/// </summary>
		/// <param name="domainList1"></param>
		/// <param name="domainList2"></param>
		/// <param name="DomainID"></param>
		/// <param name="repDomainList1"></param>
		/// <param name="repDomainList2"></param>
		private void GetFamilyNum (long[] domainList1, long[] domainList2, long DomainID, ref List<long> repDomainList1, ref List<long> repDomainList2)
		{
			if (Array.BinarySearch (domainList1, DomainID) > -1)
			{
				if (! repDomainList1.Contains (DomainID))
				{
					repDomainList1.Add (DomainID);
				}
			}
			else if (Array.BinarySearch (domainList2, DomainID) > -1)
			{
				if (! repDomainList2.Contains (DomainID))
				{
					repDomainList2.Add (DomainID);
				}
			}
		}

		/// <summary>
		/// the domain list for this family
		/// </summary>
		/// <param name="familyCode"></param>
		/// <returns></returns>
		private long[] GetDomainList (string familyCode)
		{
			string[] fields = familyCode.Split ('.');
			string queryString = string.Format ("SELECT DomainID FROM ScopDomain " + 
				" WHERE Class = '{0}' AND Fold = {1} AND Superfamily = {2} AND Family = {3};", 
				fields[0], fields[1], fields[2], fields[3]);
			DataTable domainTable = dbQuery.Query (ProtCidSettings.pdbfamDbConnection, queryString);
			queryString = string.Format ("SELECT Distinct DomainID FROM NonScopDomainDef " + 
				" WHERE Class = '{0}' AND Fold = {1} AND Superfamily = {2} AND Family = {3};", 
				fields[0], fields[1], fields[2], fields[3]);
            DataTable nonScopDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
			long[] domainList = new long [domainTable.Rows.Count + nonScopDomainTable.Rows.Count];
			int count = 0;
			foreach (DataRow dRow in domainTable.Rows)
			{
				domainList[count] = Convert.ToInt32 (dRow["DomainID"].ToString ());
				count ++;
			}
			foreach (DataRow dRow in nonScopDomainTable.Rows)
			{
				domainList[count] = Convert.ToInt64 (dRow["DomainID"].ToString ());
				count ++;
			}
			return domainList;
		}
		#endregion
	}
}
