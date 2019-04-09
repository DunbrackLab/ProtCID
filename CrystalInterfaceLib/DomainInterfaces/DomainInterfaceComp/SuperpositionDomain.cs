using System;
using System.Data;
using System.Collections.Generic;
using CrystalInterfaceLib.StructureComp.HomoEntryComp;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// Summary description for SuperpositionDomain.
	/// </summary>
	public class SuperpositionDomainInterfaces : SuperpositionInterfaces
	{
		public SuperpositionDomainInterfaces()
		{
		}

		#region superposition
		/// <summary>
		/// superpose domain interfaces
		/// </summary>
		/// <param name="domainInterface1"></param>
		/// <param name="domainInterface2"></param>
		/// <param name="alignInfoTable"></param>
		public double SuperposeDomainInterfaces (DomainInterface domainInterface1, DomainInterface domainInterface2, DataTable alignInfoTable)
		{
			long domainID11 = domainInterface1.domainId1;
			long domainID12 = domainInterface1.domainId2;
			long domainID21 = domainInterface2.domainId1;
			long domainID22 = domainInterface2.domainId2;
			double identity1 = 100;
			double identity2 = 100;

            DataRow alignRow1 = null;
            DataRow alignRow2 = null;
            if (domainID11 != domainID21)
            {
                alignRow1 = GetAlignRow(domainID11, domainID21, alignInfoTable);
                if (alignRow1 != null)
                {
                    SuperposeChain(domainInterface2.chain1, alignRow1);
                    identity1 = Convert.ToDouble(alignRow1["Identity"].ToString());
                }
            }

            if (domainID12 != domainID22)
            {
                alignRow2 = GetAlignRow(domainID12, domainID22, alignInfoTable);
                if (alignRow2 != null)
                {
                    SuperposeChain(domainInterface2.chain2, alignRow2);
                    identity2 = Convert.ToDouble(alignRow2["Identity"].ToString());
                }
            }
            if (alignRow1 != null && alignRow2 != null)
            {
                domainInterface2.seqDistHash = SuperposeInterface(domainInterface2.seqDistHash, alignRow1, alignRow2);
                domainInterface2.ResetSeqResidueHash();
            }

            if (identity1 < 0)
            {
                identity1 = 0;
            }
            if (identity2 < 0)
            {
                identity2 = 0;
            }
            if (identity1 > 0 && identity2 > 0)
            {
                return Math.Sqrt(identity1 * identity2);
            }
            else if (identity1 > 0)
            {
                return identity1;
            }
            else if (identity2 > 0)
            {
                return identity2;
            }
            return -1.0;
		}
		#endregion

		#region reverse superposition
		/// <summary>
		/// reverse the superposed interfaces
		/// </summary>
		/// <param name="domainInterface1"></param>
		/// <param name="domainInterface2"></param>
		/// <param name="alignInfoTable"></param>
		public void ReverseSupDomainInterfaces (DomainInterface domainInterface1, 
			DomainInterface domainInterface2, DataTable alignInfoTable)
		{
			long domainID11 = domainInterface1.domainId1;
			long domainID12 = domainInterface1.domainId2;
			long domainID21 = domainInterface2.domainId1;
			long domainID22 = domainInterface2.domainId2;

			DataRow alignRow1 = GetAlignRow (domainID11, domainID21, alignInfoTable);
			ReverseSupChain ( domainInterface2.chain1, alignRow1);

			DataRow alignRow2 = GetAlignRow (domainID12, domainID22, alignInfoTable);
			ReverseSupChain ( domainInterface2.chain2, alignRow2);

            domainInterface2.seqDistHash = ReverseSupInterface(domainInterface2.seqDistHash, alignRow1, alignRow2);
			domainInterface2.ResetSeqResidueHash ();
		}
		#endregion

		#region align row
		/// <summary>
		/// alignment info 
		/// </summary>
		/// <param name="DomainID1"></param>
		/// <param name="DomainID2"></param>
		/// <param name="alignInfoTable"></param>
		/// <returns></returns>
		public DataRow GetAlignRow (long domainID1, long domainID2, DataTable alignInfoTable)
		{
			DataRow[] alignRows = alignInfoTable.Select (string.Format ("DomainID1 = '{0}' AND DomainID2 = '{1}'", domainID1, domainID2));
			if (alignRows.Length > 0)
			{
				return alignRows[0];
			}
			else
			{
				alignRows = alignInfoTable.Select (string.Format ("DomainID1 = '{0}' AND DomainID2 = '{1}'", domainID2, domainID1));
				if (alignRows.Length > 0)
				{
					// reverse it
					ReverseDataRow (alignRows[0]);
					return alignRows[0];
				}
				else
				{
					return null;
				}
			}
		}
	 
		private void ReverseDataRow (DataRow alignInfoRow)
		{
			object temp = alignInfoRow["DomainID1"];
			alignInfoRow["DomainID1"] = alignInfoRow["DomainID2"];
			alignInfoRow["DomainID2"] = temp;
			temp = alignInfoRow["QueryStart"];
			alignInfoRow["QueryStart"] = alignInfoRow["HitStart"];
			alignInfoRow["HitStart"] = temp;
			temp = alignInfoRow["QueryEnd"];
			alignInfoRow["QueryEnd"] = alignInfoRow["hitEnd"];
			alignInfoRow["HitEnd"] = temp;
			temp = alignInfoRow["QuerySequence"];
			alignInfoRow["QuerySequence"] = alignInfoRow["hitSequence"];
			alignInfoRow["HitSequence"] = temp;

            if (alignInfoRow.Table.Columns.Contains("QuerySeqNumbers"))
            {
                temp = alignInfoRow["QuerySeqNumbers"];
                alignInfoRow["QuerySeqNumbers"] = alignInfoRow["HitSeqNumbers"];
                alignInfoRow["HitSeqNumbers"] = temp;
            }
		}
		#endregion
	}
}
