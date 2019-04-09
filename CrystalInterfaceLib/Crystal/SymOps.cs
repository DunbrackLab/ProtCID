using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using AuxFuncLib;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// a list of symmetry operators in a space group
	/// temporarily retrieved from PDB ent files
	/// </summary
	[XmlRoot("Symmetry")]
	public class SymOps
	{
		private List<SpaceGroupSymmetryMatrices> symOpsInSpaceGroupList = new List<SpaceGroupSymmetryMatrices> ();

		public SymOps()
		{
		}
		[XmlElement("SymmetryMatrices")]
		public SpaceGroupSymmetryMatrices[] SymOpsInSpaceGroupList
		{
			get
			{
				SpaceGroupSymmetryMatrices[] matrixArray = new SpaceGroupSymmetryMatrices [symOpsInSpaceGroupList.Count];
				symOpsInSpaceGroupList.CopyTo (matrixArray);
				return matrixArray;
			}
			set
			{
				if (value == null) return;
				symOpsInSpaceGroupList.Clear ();
				symOpsInSpaceGroupList.AddRange (value);
			}
		}

		/// <summary>
		/// add symmetry operators of a space group
		/// </summary>
		/// <param name="symMatricesInSg"></param>
		public void Add (SpaceGroupSymmetryMatrices symMatricesInSg)
		{
			symOpsInSpaceGroupList.Add (symMatricesInSg);
		}

		/// <summary>
		/// find symmetry operators for a specific spacegroup
		/// in predefined symmetry operators of all possible space groups
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		public SymOpMatrix[] FindSpaceGroup (string spaceGroup)
		{
		//	string sgNoSpaceString = RemoveSpaceInSg(spaceGroup);
			if (symOpsInSpaceGroupList.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < symOpsInSpaceGroupList.Count; i ++)
			{
				if (((SpaceGroupSymmetryMatrices)symOpsInSpaceGroupList[i]).spaceGroup.ToLower () == spaceGroup.ToLower ())
				{
					return ((SpaceGroupSymmetryMatrices)symOpsInSpaceGroupList[i]).SymmetryOpList;
				}
			}
			return null;
		}

		#region compare two space groups
		/// <summary>
		/// is two space groups same or not
		/// </summary>
		/// <param name="spaceGroup1"></param>
		/// <param name="spaceGroup2"></param>
		/// <returns></returns>
		public int CompareTwoSpaceGroups (string spaceGroup1, string spaceGroup2)
		{
			SpaceGroupSymmetryMatrices sgMatrices1 = FindSpaceGroupMatrices (spaceGroup1);
			SpaceGroupSymmetryMatrices sgMatrices2 = FindSpaceGroupMatrices (spaceGroup2);
			return CompareTwoSpaceGroups (sgMatrices1, sgMatrices2);		
		}

		/// <summary>
		/// find symmetry operators for a specific spacegroup
		/// in predefined symmetry operators of all possible space groups
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private SpaceGroupSymmetryMatrices FindSpaceGroupMatrices (string spaceGroup)
		{
			//	string sgNoSpaceString = RemoveSpaceInSg(spaceGroup);
			if (symOpsInSpaceGroupList.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < symOpsInSpaceGroupList.Count; i ++)
			{
				if (((SpaceGroupSymmetryMatrices)symOpsInSpaceGroupList[i]).spaceGroup.ToLower () == spaceGroup.ToLower ())
				{
					return (SpaceGroupSymmetryMatrices)symOpsInSpaceGroupList[i];
				}
			}
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="spaceGroup1"></param>
		/// <param name="spaceGroup2"></param>
		/// <returns></returns>
		private int CompareTwoSpaceGroups (SpaceGroupSymmetryMatrices spaceGroup1, 
			SpaceGroupSymmetryMatrices spaceGroup2)
		{
			if (spaceGroup1 == null || spaceGroup2 == null)
			{
				return -1;
			}
			SymOpMatrix[] symOpList1 = spaceGroup1.SymmetryOpList;
			SymOpMatrix[] symOpList2 = spaceGroup2.SymmetryOpList;
			int sameSymOpsCount = 0;

			foreach (SymOpMatrix symOp1 in symOpList1)
			{
				foreach (SymOpMatrix symOp2 in symOpList2)
				{
					if (AreSymOpMatricesSame (symOp1, symOp2))
					{
						sameSymOpsCount ++;
						break;
					}
				}
			}
			if (symOpList1.Length == symOpList2.Length)
			{
				if (sameSymOpsCount == symOpList1.Length )
				{
					// same space groups
					return 0;
				}
				else
				{
					// different space groups
					return -1;
				}
			}
			else
			{
				// space group 1 is a subset
				if (sameSymOpsCount == symOpList1.Length)
				{
					return 1;
				}
					// space group 1 is a superset
				else if (sameSymOpsCount == symOpList2.Length)
				{
					return 2;
				}
				else
				{
					// different
					return -1;
				}
			}
		}

		/// <summary>
		/// are these two symmetry operator matrices same
		/// </summary>
		/// <param name="symOp1"></param>
		/// <param name="symOp2"></param>
		/// <returns></returns>
		private bool AreSymOpMatricesSame (SymOpMatrix symOp1, SymOpMatrix symOp2)
		{
			int compResult = string.Compare (symOp1.fullSymmetryString, symOp2.fullSymmetryString, true); 
			if (compResult == 0)
			{
				return true;
			}
			return false;
		}
		#endregion

		#region format space group names
		/// <summary>
		/// remove possible space in space group from PDB XML files
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private string RemoveSpaceInSg(string spaceGroup)
		{
			string sgNoSpace = "";
			foreach (char ch in spaceGroup)
			{
				if (ch == ' ')
				{
					continue;
				}
				sgNoSpace += ch;
			}
			return sgNoSpace;
		}

		/// <summary>
		/// format space group name
		/// by removing space, and additional 1 
		/// e.g. A 1 2 1 is same as A 2
		/// and A 2 is same as A2
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private string FormatSpaceGroupName (string spaceGroup)
		{
			string formattedSgString = "";
			string[] sgFields = ParseHelper.SplitPlus (spaceGroup, ' ');
			foreach (string sgField in sgFields)
			{
				if (sgField == "1")
				{
					continue;
				}
				formattedSgString += sgField;
			}
			return formattedSgString;
		}
		#endregion
	}
	/// <summary>
	/// Category for all Symmetry operations in the space group
	/// data are parsed from spacegroup.cif from Zukang (PDB)
	/// </summary>
	public class SpaceGroupSymmetryMatrices
	{
		[XmlAttribute("SpaceGroup")] public string spaceGroup = "";
		[XmlElement("SameSpaceGroups")] public string sameSgListString = "";
		private List<SymOpMatrix> symmetryOpList = new List<SymOpMatrix> ();

		public SpaceGroupSymmetryMatrices()
		{
		}

//		[XmlElement("SymmetryMatrix")] 
		public SymOpMatrix[] SymmetryOpList
		{
			get
			{
				SymOpMatrix[] itemList = new SymOpMatrix [symmetryOpList.Count];
				symmetryOpList.CopyTo(itemList);
				return itemList;
			}
			set
			{
				if (value == null) return;
				SymOpMatrix[] itemList = (SymOpMatrix[]) value;
				symmetryOpList.Clear();
				symmetryOpList.AddRange(itemList);
			}
		}

		public void Add (SymOpMatrix symOpMatrix)
		{
			symmetryOpList.Add (symOpMatrix);
		}
	}
}
