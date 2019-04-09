using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;

namespace CrystalInterfaceLib.FileParser
{
	/// <summary>
	/// Parse PDB NDB_Space_Group.cif file
	/// convert it to a XML file
	/// </summary>
	public class SymOpFileParser
	{
		public SymOpFileParser()
		{
		}

		public void ParseSymOpsFile (string fileName)
		{
			SymOps symOpList = new SymOps ();

			string line = "";
			int symOpNum = 0;
			int dimNum = 3;
			int vectNum = 2;

			SpaceGroupSymmetryMatrices sgSymMatrices = null;

			StreamReader fileReader = new StreamReader (fileName);

			string preSg = "";
			string currentSg = "";
			while ((line = fileReader.ReadLine ()) != null)
			{
				// skip notation
				line = line.Trim ();
				if (line == "" || 
					line.ToLower ().IndexOf ("space_group") > -1 ||
					line.ToLower ().IndexOf ("loop") > -1)
				{
					continue;
				}
				// skip comments
				if (line[0] == '#')
				{
					continue;
				}
				// remove space
				currentSg = line.Substring (line.IndexOf ("'") + 1, line.LastIndexOf ("'") - line.IndexOf ("'") - 1);
				line = line.Substring (line.LastIndexOf ("'") + 1, line.Length - line.LastIndexOf ("'") - 1).Trim ();
				// start new space group,
				// add previous one
				if (currentSg != preSg)
				{
					// add symmetry operations of a space group
					if (sgSymMatrices != null)
					{
						symOpList.Add (sgSymMatrices);
					}
					sgSymMatrices = new SpaceGroupSymmetryMatrices ();
					sgSymMatrices.spaceGroup = currentSg;
				}
				
				SymOpMatrix symOpMatrix = new SymOpMatrix ();
				string[] fields = ParseHelper.SplitPlus (line, ' ');
				symOpMatrix.symmetryOpNum = fields[0];
										
				symOpNum = Convert.ToInt32 (fields[0]);
				symOpMatrix.symmetryString = symOpNum.ToString () + "_555";
				// set 3*3 matrix
				int i = 1;
				for (; i < 10; i ++ )
				{
					symOpMatrix.Add ( (int) Math.Floor ((double)(i - 1) / (double)dimNum), (i - 1) % dimNum, Convert.ToDouble (fields[i]));
				}
				// set matrix vector
				string denominator = "";
				string nominator = "";
				int dim = 0;
				for (; i < fields.Length; i += vectNum )
				{		
					denominator = fields[i];
					nominator = fields[i + 1];
					if (nominator == "0")
					{
						symOpMatrix.Add (dim, 3, 0);
					}
					else
					{
						symOpMatrix.Add ( dim, 3, Convert.ToDouble (denominator) / Convert.ToDouble (nominator));
					}
					dim ++;
				}
				// format full symmetry string from symmetry matrix 
				symOpMatrix.fullSymmetryString = symOpMatrix.ToFullSymString ();
				sgSymMatrices.Add (symOpMatrix);
				symOpMatrix = new SymOpMatrix ();
				preSg = currentSg;
			}
			if (sgSymMatrices != null)
			{
				// add the last space group
				symOpList.Add (sgSymMatrices);
			}
			FindDuplicateSpaceGroups (ref symOpList);
			XmlSerializer xmlSerializer = new XmlSerializer (symOpList.GetType ());
			TextWriter symOpWriter = new StreamWriter ("symOps.xml", false);
			xmlSerializer.Serialize (symOpWriter, symOpList);
			symOpWriter.Close ();
		}

		private void FindDuplicateSpaceGroups (ref SymOps symOpList)
		{
			Dictionary<string, List<string>> sameSgHash = new Dictionary<string,List<string>> ();
			string spaceGroup1 = "";
			string spaceGroup2 = "";
			for (int i = 0; i < symOpList.SymOpsInSpaceGroupList.Length - 1; i ++)
			{
				spaceGroup1 = symOpList.SymOpsInSpaceGroupList[i].spaceGroup;
				for (int j = i + 1; j < symOpList.SymOpsInSpaceGroupList.Length; j ++)
				{
					spaceGroup2 = symOpList.SymOpsInSpaceGroupList[j].spaceGroup;
					if (AreTwoSgStringsSame (symOpList.SymOpsInSpaceGroupList[i], symOpList.SymOpsInSpaceGroupList[j]))
					{
						// add spacegroup2 to the list of duplicate space group of sgMatrices1
						if (sameSgHash.ContainsKey (spaceGroup1))
						{
                            sameSgHash[spaceGroup1].Add(spaceGroup2);
						}
						else
						{
							List<string> sgList = new List<string> ();
							sgList.Add (spaceGroup2);
							sameSgHash.Add (spaceGroup1, sgList);
						}

						// add spacegroup1 to duplicate space group list of sg2
						if (sameSgHash.ContainsKey (spaceGroup2))
						{
                            sameSgHash[spaceGroup2].Add(spaceGroup1);
						}
						else
						{
							List<string> sgList = new List<string> ();
							sgList.Add (spaceGroup1);
							sameSgHash.Add (spaceGroup2, sgList);
						}
					}
				}
			}
			foreach (SpaceGroupSymmetryMatrices sgMatrices in symOpList.SymOpsInSpaceGroupList)
			{
				if (sameSgHash.ContainsKey (sgMatrices.spaceGroup))
				{
					foreach (string sgString in sameSgHash[sgMatrices.spaceGroup])
					{
						sgMatrices.sameSgListString += (sgString + ";");
					}
					sgMatrices.sameSgListString = sgMatrices.sameSgListString.TrimEnd (';');
				}
			}
		}

		/// <summary>
		/// two space group sym OPs are same
		/// </summary>
		/// <param name="sgMatrices1"></param>
		/// <param name="sgMatrices2"></param>
		/// <returns></returns>
		private bool AreTwoSgStringsSame (SpaceGroupSymmetryMatrices sgMatrices1, SpaceGroupSymmetryMatrices sgMatrices2)
		{
			string fullSymOpString1 = GetFullSymOpStringForAllSymOps (sgMatrices1);
			string fullSymOpString2 = GetFullSymOpStringForAllSymOps (sgMatrices2);
			if (string.Compare (fullSymOpString1, fullSymOpString2) == 0)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sgOpMatrices"></param>
		/// <returns></returns>
		private string GetFullSymOpStringForAllSymOps (SpaceGroupSymmetryMatrices sgOpMatrices)
		{
			string[] fullSymOpListStrings = new string [sgOpMatrices.SymmetryOpList.Length];
			for (int i = 0; i < fullSymOpListStrings.Length; i ++)
			{
				fullSymOpListStrings[i] = sgOpMatrices.SymmetryOpList[i].fullSymmetryString;
			}
			Array.Sort (fullSymOpListStrings);
		
			string fullSymOpString = "";
			foreach (string symOpString in fullSymOpListStrings)
			{
				fullSymOpString += symOpString;
			}
			return fullSymOpString;
		}
	}
}
