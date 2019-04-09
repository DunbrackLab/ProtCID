using System;
using System.Data;
using System.Collections.Generic;

namespace InterfaceClusterLib.AuxFuncs
{
	/// <summary>
	/// Summary description for RowSorter.
	/// </summary>
	public class RowSorter
	{
		public RowSorter()
		{
		}

		/// <summary>
		/// sort domain rows by entityid, chain and domain start position
		/// if a domain with two fragments, deal it with two domains with same sunid
		/// and same family codes
		/// </summary>
		/// <param name="domainRows"></param>
		public static void SortDomainRowsByEntityChainStartPos (ref DataRow[] domainRows)
		{
			Dictionary<string, DataRow> entityChainStartPosHash = new Dictionary<string,DataRow> ();
			string entityChainStartPosString = "";
			foreach (DataRow dRow in domainRows)
			{
				entityChainStartPosString = dRow["EntityID"].ToString ().PadLeft (3, '0') + "_" + 
					dRow["Chain"].ToString ().Trim ().PadLeft (2, '0') + "_" + 
					dRow["StartPos"].ToString ().Trim ().PadLeft (5, '0');
				entityChainStartPosHash.Add (entityChainStartPosString, dRow);
			}
			List<string> keyStringList = new List<string> (entityChainStartPosHash.Keys);
			keyStringList.Sort ();
			DataRow[] sortedDomainRows = new DataRow [domainRows.Length];
			int i = 0;
			foreach (string keyString in keyStringList)
			{
				sortedDomainRows[i] = (DataRow)entityChainStartPosHash[keyString];
				i ++;
			}
			domainRows = sortedDomainRows;
		}

		/// <summary>
		/// sort domain rows by entityid, chain and domain start position
		/// if a domain with two fragments, deal it with two domains with same sunid
		/// and same family codes
		/// </summary>
		/// <param name="domainRows"></param>
		public static void SortDataRows (ref DataRow[] dataRows, string[] colNames, int[] colSize)
		{
			Dictionary<string, DataRow> sortStringHash = new Dictionary<string,DataRow>  ();
			string sortString = "";
			foreach (DataRow dRow in dataRows)
			{
				sortString = "";
				for (int i = 0; i < colNames.Length; i ++)
				{
					sortString += dRow[colNames[i]].ToString ().Trim ().PadLeft (colSize[i], '0');
					sortString += "_";
				}
				sortString = sortString.TrimEnd ('_');
				if (! sortStringHash.ContainsKey (sortString))
				{
					sortStringHash.Add (sortString, dRow);
				}
			}
			List<string> keyStringList = new List<string> (sortStringHash.Keys);
			keyStringList.Sort ();
			DataRow[] sortedDataRows = new DataRow [sortStringHash.Count];
			int count = 0;
			foreach (string keyString in keyStringList)
			{
				sortedDataRows[count] = (DataRow)sortStringHash[keyString];
				count ++;
			}
			dataRows = sortedDataRows;
		}
	}
}
