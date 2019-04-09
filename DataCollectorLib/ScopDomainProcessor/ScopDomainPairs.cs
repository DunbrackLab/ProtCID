using System;
using System.Data;
using System.IO;
using System.Collections;
using DbLib;

namespace DataCollectorLib.ScopDomainProcessor
{
	/// <summary>
	/// Summary description for ScopDomainPairs.
	/// </summary>
	public class ScopDomainPairs
	{
		private DbQuery dbQuery = new DbQuery ();

		public ScopDomainPairs()
		{
		}

		#region read chain pairs to be aligned in SCOP
		/// <summary>
		/// 
		/// </summary>
		public void ReadScopDomainPairsToAligned ()
		{
			string queryString = "Select Distinct PdbID1, ChainID1 From RedundantPdbChains;";
			DataTable repChainTable = dbQuery.Query (queryString);
			queryString = "Select Distinct Class, Fold, Superfamily, Family From ScopDomain;";
			DataTable scopFamilyTable = dbQuery.Query (queryString);
			StreamWriter dataWriter = new StreamWriter ("ScopDomainPairs.txt");
			foreach (DataRow familyRow in scopFamilyTable.Rows)
			{
				GetFamilyDomainPairs (familyRow["Class"].ToString ().TrimEnd (), 
					Convert.ToInt32 (familyRow["Fold"].ToString ()), 
					Convert.ToInt32 (familyRow["Superfamily"].ToString ()),
					Convert.ToInt32 (familyRow["Family"].ToString ()),
					repChainTable,  dataWriter);
			}
			dataWriter.Close ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scopClass"></param>
		/// <param name="fold"></param>
		/// <param name="superFamily"></param>
		/// <param name="family"></param>
		/// <param name="repChainTable"></param>
		/// <param name="dataWriter"></param>
		private void GetFamilyDomainPairs (string scopClass, int fold, 
			int superFamily, int family, DataTable repChainTable, StreamWriter dataWriter)
		{
			string queryString = string.Format ("Select PdbID, SunID From ScopDomain " + 
				" Where Class = '{0}' AND Fold = {1} AND Superfamily = {2} AND Family = {3};", 
				scopClass, fold, superFamily, family);
			DataTable domainTable = dbQuery.Query (queryString);
			string pdbId = "";
			int sunId = -1;
	//		Hashtable domainSingleChainHash = new Hashtable ();
			ArrayList entryDomainList = new ArrayList ();
			foreach (DataRow domainRow in domainTable.Rows)
			{
				pdbId = domainRow["PdbID"].ToString ();
				sunId = Convert.ToInt32 (domainRow["SunID"].ToString ());
		//		bool isDomainSingleChain = IsDomainSingleChain (pdbId, sunId);
		//		domainSingleChainHash.Add (pdbId + "_" + sunId.ToString (), isDomainSingleChain);
				entryDomainList.Add (pdbId + "_" + sunId.ToString ());
			}
	//		ArrayList entryDomainList = new ArrayList (domainSingleChainHash.Keys);
	//		entryDomainList.Sort ();
	//		bool iSingleDomainChain = false;
	//		bool jSingleDomainChain = false;
			for (int i = 0; i < entryDomainList.Count; i ++)
			{
	//			iSingleDomainChain = (bool)domainSingleChainHash[entryDomainList[i]];
				for (int j = i + 1; j < entryDomainList.Count; j ++)
				{
	/*				jSingleDomainChain = (bool)domainSingleChainHash[entryDomainList[j]];
					if (iSingleDomainChain && jSingleDomainChain)
					{
						continue;
					}*/
					dataWriter.WriteLine (entryDomainList[i] + "   " + entryDomainList[j]);
				}

			}
		}

		/// <summary>
		/// check if the domain is the chain
		/// that is, the domain is located in one chain, 
		/// and the chain contains only one domain
		/// </summary>
		/// <param name="sunId"></param>
		/// <returns></returns>
		private bool IsDomainSingleChain (string pdbId, int sunId)
		{
			string queryString = string.Format ("Select Distinct Chain From ScopDomainPos " + 
				"Where SunID = {0};", sunId);
			DataTable domainChainTable = dbQuery.Query (queryString);
			if (domainChainTable.Rows.Count == 1) // domain located in one chain
			{
				string chain = domainChainTable.Rows[0]["Chain"].ToString ().TrimEnd ();
				if (IsChainSingleDomain (pdbId, chain)) // chain contains only one domain
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// check if the chain only contains one domain
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chain"></param>
		/// <returns></returns>
		private bool IsChainSingleDomain (string pdbId, string chain)
		{
			/*	string queryString = string.Format ("Select distinct SunID From ScopDomain Where PdbID = '{0}';", pdbId);
				DataTable domainTable = dbQuery.Query (queryString);
				ArrayList chainDomainList = new ArrayList ();
				int sunId = -1;
				foreach (DataRow domainRow in domainTable.Rows)
				{
					sunId = Convert.ToInt32 (domainRow["SunID"].ToString ());
					queryString = string.Format ("Select * from ScopDomainPos " + 
						" Where SunID = {0} AND Chain = '{1}';", sunId, chain);
					DataTable chainDomainTable = dbQuery.Query (queryString);
				}*/
			string queryString = string.Format ("Select Distinct ScopDomain.SunID From ScopDomain, ScopDomainPos " + 
				" Where PdbID = '{0}' AND Chain = '{1}' " + 
				" AND ScopDomain.SunID = ScopDomainPos.SunID;", pdbId, chain);
			DataTable chainDomainTable = dbQuery.Query (queryString);
			if (chainDomainTable.Rows.Count == 1)
			{
				return true;
			}
			return false;
		}
		#endregion
	}
}
