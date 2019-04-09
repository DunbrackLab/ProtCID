using System;
using CrystalInterfaceLib.Settings;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for SymOperator.
	/// </summary>
	public class SymOperator
	{
		public SymOperator()
		{
		}

		/// <summary>
		/// convert a symmetry operator to a full symmetry string in the space group
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <param name="symOpString"></param>
		/// <returns></returns>
		public string ConvertSymOpStringToFull (string spaceGroup, string symOpString)
		{
			if (AppSettings.symOps == null)
			{
				AppSettings.LoadSymOps ();
			}
			SymOpMatrix[] symOpMatrices = AppSettings.symOps.FindSpaceGroup(spaceGroup);
			return ConvertSymOpStringToFull (symOpMatrices, symOpString);
		}
		/// <summary>
		/// convert a symmetry operator to a full symmetry string
		/// 1_656 to 1+X,Y,1+Z based on the symmetry matrices
		/// </summary>
		/// <param name="symOpMatrices"></param>
		/// <param name="symOpString"></param>
		/// <returns></returns>
		public string ConvertSymOpStringToFull (SymOpMatrix[] symOpMatrices, string symOpString)
		{
			SymOpMatrix fullSymOpMatrix = GetSymmetryMatrixFromSymmetryString  (symOpMatrices, symOpString);
			string fullSymString =  fullSymOpMatrix.ToFullSymString ();
			return FormatFullSymString (fullSymString);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullSymOpString">X-1,Y,Z+1</param>
        /// <param name="spaceGroup"></param>
        /// <returns>e.g. 1_456</returns>
        public string ConvertFullSymOpStringToShort (string fullSymOpString, string spaceGroup)
        {            
            SymOpMatrix symOpMatrix = new SymOpMatrix ();
            symOpMatrix.fullSymmetryString = fullSymOpString;
            SetSymOpMatrix (fullSymOpString, ref symOpMatrix);
            string symString = ConvertMatrixToSymString (symOpMatrix, 1);
            return symString;
        }

		#region format full symmetry string
		/// <summary>
		/// format full symmetry string to PDB full symmetry string
		///e.g.  X+0.25000, -Z-1/4,Y+0.75000
		///To PDB: 1/4+X,-1/4-Z,3/4+Y
		/// </summary>
		/// <param name="fullSymString"></param>
		/// <returns></returns>
		private string FormatFullSymString (string fullSymString)
		{
			fullSymString = fullSymString.TrimEnd ("+-".ToCharArray ()).ToUpper ();
			bool needFormat = false;
			foreach (char ch in fullSymString)
			{
				if (Char.IsDigit (ch))
				{
					needFormat = true;
				}
			}
			if (! needFormat)
			{
				return fullSymString;
			}
			string formattedSymString = "";
			string[] symStrFields = fullSymString.Split (',');
			string vectString = "";
			string axisString = "";
			foreach (string symStr in symStrFields)
			{
				if (symStr == "")
				{
					formattedSymString += ",";
					continue;
				}
				vectString = "";
				axisString = "";
				// skip the last character: common ,
				for (int i = 0; i < symStr.Length; i ++)
				{
					if (Char.IsDigit (symStr[i]) || symStr[i] == '.' || symStr[i] == '/')
					{
						vectString += symStr[i];
					}
					else
					{
						axisString += symStr[i];
					}
				}
				if (axisString[axisString.Length - 1] == '-')
				{
					vectString = vectString.Insert (0, "-");
				}
				//  remove the last character: the sign for the vector
				if (axisString[axisString.Length - 1] == '+' || axisString[axisString.Length - 1] == '-')
				{
					axisString = axisString.Remove (axisString.Length - 1, 1);
				}

				if (vectString != "")
				{
					vectString = FormatVectString (vectString);
					formattedSymString += vectString;
				}
				// add + 
				if (vectString != "" && axisString[0] != '-')
				{
					formattedSymString += "+";
				}
				formattedSymString += axisString;
				formattedSymString += ",";			
			}
			return formattedSymString.TrimEnd (',');
		}
		/// <summary>
		/// try to convert 0.3333 to 1/3
		/// </summary>
		/// <param name="fract"></param>
		/// <returns></returns>
		private string FormatVectString (string vectString)
		{
			if (vectString == "")
			{
				return "";
			}
			string formattedVectString = "";
			int dotIndex = vectString.IndexOf (".");
			if (dotIndex < 0)
			{
				return vectString;
			}
			char signChar = '+';
			if (vectString[0] == '-')
			{
				signChar = '-';
			}
			int intVal = Convert.ToInt32 (vectString.Substring (0, dotIndex));
			// something wrong, keep the original one
			if (vectString.Length <= dotIndex + 1)
			{
				return vectString;
			}
			int firstFractDigit = Convert.ToInt32 (vectString.Substring (dotIndex + 1, 1));
			int numVal = 0;
			int demVal = 1;
			switch (firstFractDigit)
			{
				case 1:
					numVal = 1;
					demVal = 6;
					break;
				case 2:
					numVal = 1;
					demVal = 4;
					break;
				case 3:
					numVal = 1;
					demVal = 3;
					break;
				case 5:
					numVal = 1;
					demVal = 2;
					break;
				case 6:
					numVal = 2;
					demVal = 3;
					break;
				case 7:
					numVal = 3;
					demVal = 4;
					break;
				case 8: 
					numVal = 5;
					demVal = 6;
					break;
				default:
					numVal = 0;
					demVal = 1;
					break;
			}
			if (intVal > 0 || intVal < 0)
			{
				numVal = numVal + demVal * Math.Abs (intVal);
				if (intVal < 0)
				{
					formattedVectString += "-";
				}
			}
			else
			{
				if (signChar == '-')
				{
					formattedVectString += "-";
				}
			}
			if (numVal > 0)
			{
				formattedVectString += numVal.ToString ();
			}
			if (demVal > 1)
			{
				formattedVectString += "/";
				formattedVectString += demVal.ToString ();
			}
			
			return formattedVectString;
		}
		#endregion

		#region Retrieve symmetry matrix from a space group 
		/// <summary>
		/// get symmetry matrix for a specific symmetry operation string
		/// may need translation
		/// linear search
		/// </summary>
		/// <param name="symOpMatrices"></param>
		/// <param name="symOpString"></param>
		/// <returns></returns>
		public SymOpMatrix GetSymmetryMatrixFromSymmetryString (SymOpMatrix[] symOpMatrices, string symOpString)
		{
			int index1 = symOpString.IndexOf ("_");
			int index2 = symOpString.LastIndexOf ("_");
			string symOpNum = "";
			string symString = "";
			if (index1 == index2)
			{
				symOpNum = symOpString.Substring (0, index1);
				symString = symOpString.Substring (index1 + 1, symOpString.Length - index1 - 1);
			}
			else
			{
				symOpNum = symOpString.Substring (index1 + 1, index2 - index1 - 1);
				symString = symOpString.Substring (index2 + 1, symOpString.Length - index2 - 1);
			}
			SymOpMatrix convertedSymOpMatrix = null;
			foreach (SymOpMatrix symOpMatrix in symOpMatrices)
			{
				if (symOpMatrix.symmetryOpNum == symOpNum )
				{
					convertedSymOpMatrix = (SymOpMatrix)symOpMatrix.Clone ();
					break;
				}
			}
			if (convertedSymOpMatrix != null)
			{
				if (symString.Length == 3)
				{
					for (int i = 0; i < 3; i ++)
					{
						if (symString[i] == '5')
						{
							continue;
						}
						convertedSymOpMatrix.Add(i, 3, convertedSymOpMatrix.Value (i, 3) + (double)(Convert.ToInt32(symString[i].ToString ()) - 5));
					}
				}
					/* should deal with a symmetry string with translate vectors > 5
					 * format of this type of symmetry string: 1_+10-1+6
					 * translate vector (+5, -6, +1)
					 */
				else if (symString.Length > 3)
				{
					string[] vectStrings = SeparateSymmetryString (symString);
					convertedSymOpMatrix.Add(0, 3, convertedSymOpMatrix.Value (0, 3) + 
						(double) (Convert.ToInt32 (vectStrings[0]) - 5));
					convertedSymOpMatrix.Add(1, 3, convertedSymOpMatrix.Value (1, 3) + 
						(double) (Convert.ToInt32 (vectStrings[1]) - 5));
					convertedSymOpMatrix.Add(2, 3, convertedSymOpMatrix.Value (2, 3) + 
						(double) (Convert.ToInt32 (vectStrings[2]) - 5));
				}
			}
			return convertedSymOpMatrix;
		}

		/// <summary>
		/// for symmetry string with large vectors 
		/// e.g. 1_+10-1+6
		/// </summary>
		/// <param name="symOpString"></param>
		private string[] SeparateSymmetryString (string symOpString)
		{
			string[] vectStrings = new string [3];
			int count = -1;
			foreach (char ch in symOpString)
			{
				if (ch == '+' || ch == '-')
				{
					count ++;
				}
				vectStrings[count] += ch.ToString ();
			}
			return vectStrings;
		}
		/// <summary>
		/// get symmetry matrix for a specific symmetry operation string
		/// may need translation
		/// linear search
		/// </summary>
		/// <param name="symOpMatrices"></param>
		/// <param name="symOpString"></param>
		/// <returns></returns>
		public SymOpMatrix GetSymmetryMatrixFromSymmetryOperId (SymOpMatrix[] symOpMatrices, string symOpNum)
		{
			foreach (SymOpMatrix symOpMatrix in symOpMatrices)
			{
				if (symOpMatrix.symmetryOpNum == symOpNum)
				{
					return symOpMatrix;
				}
			}
			return null;
		}
		#endregion

		#region Retrieve symmetry matrix from a symmetry string
		/// <summary>
		/// get the symmetry matrix from full symmetry string
		/// </summary>
		/// <param name="fullSymOpString"></param>
		/// <returns></returns>
		public SymOpMatrix GetSymMatrix(string fullSymOpString, string symOpString)
		{
			SymOpMatrix symOpMatrix = new SymOpMatrix ();
			symOpMatrix.fullSymmetryString = fullSymOpString;
			symOpMatrix.symmetryOpNum = symOpString.Substring 
				(symOpString.IndexOf ("_") + 1, symOpString.LastIndexOf ("_") - symOpString.IndexOf ("_") - 1);
			symOpMatrix.symmetryString = symOpString;			
			//SetSymOpMatrix (fullSymOpString, symOpString.Substring (symOpString.Length - 3, 3), ref symOpMatrix);
			SetSymOpMatrix (fullSymOpString, ref symOpMatrix);
			return symOpMatrix;
		}

		/// <summary>
		/// get matrix from a symmetry operation string
		/// </summary>
		/// <param name="symOpStr"></param>
		/// <param name="symOpMatrix"></param>
		public void SetSymOpMatrix(string fullSymOpStr, /*string symOpString,*/ ref SymOpMatrix symOpMatrix)
		{
			double[] indexValues = new double [3];
			string [] symOpRows = fullSymOpStr.ToUpper ().Split (',');
			for (int dimNo = 0; dimNo < 3; dimNo ++)
			{
				string symOpRow = symOpRows[dimNo];				
				symOpMatrix.Add (dimNo, 0, GetDimIndex (symOpRow, "X"));				
				symOpMatrix.Add (dimNo, 1, GetDimIndex (symOpRow, "Y"));
				symOpMatrix.Add (dimNo, 2, GetDimIndex (symOpRow, "Z"));
				//int transVector = Convert.ToInt32 (symOpString[dimNo].ToString ()) - 5;
				//symOpMatrix.Add (dimNo, 3, GetVectorValue(symOpRow) - transVector);
				symOpMatrix.Add (dimNo, 3, GetVectorValue(symOpRow));
			}
		}

		/// <summary>
		/// value for the point
		/// </summary>
		/// <param name="rowString"></param>
		/// <param name="dimName"></param>
		/// <returns></returns>
		private double GetDimIndex (string rowString, string dimName)
		{
			int dimIndex = rowString.IndexOf (dimName);
			if (dimIndex == 0)
			{
				return 1.0;
			}
			else if (dimIndex == -1)
			{
				return 0.0;
			}
			else
			{
				char dimSign = rowString[dimIndex - 1];
				if (dimSign == '-')
					return -1.0;
				else
					return 1.0;
			}
			//return 0.0;
		}
		/// <summary>
		/// get the vector value from a symmetry string
		/// e.g. 1/2 + X -> 0.5
		/// assume: char '/' is only in the vector
		/// and vector is positive
		/// </summary>
		/// <param name="symStr"></param>
		/// <returns></returns>
		private double GetVectorValue(string symStr)
		{
			string vectorStr = "";
			for ( int i = 0; i < symStr.Length; i ++ )
			{
				if (Char.IsDigit (symStr[i]) )
				{
					if (i > 0 && symStr[i - 1] == '-')
					{
						vectorStr += symStr[i - 1];
					}
					vectorStr += symStr[i]; 
				}
				if (symStr[i] == '/' && i > 0)
				{
					vectorStr += symStr[i];
				}
			}
			if (vectorStr != "")
			{
				string [] vectorSubStr = vectorStr.Split ('/');
				if (vectorSubStr.Length == 2)
				{
					return System.Convert.ToDouble (vectorSubStr[0]) / System.Convert.ToDouble (vectorSubStr[1]);
				}
				else if (vectorSubStr.Length == 1)
				{
					return System.Convert.ToDouble (vectorSubStr[0]);
				}
			}
			else
			{
				return 0.0;
			}
			return -1.0;
		}
		#endregion

		#region Convert a symmetry matrix to a symmetry string
		/// <summary>
		/// convert a symmetry matrix into a full symmetry string
		/// </summary>
		/// <param name="symOpMatrix">symmetry matrix</param>
		/// <returns>full symmetry string</returns>
		public string ConvertMatrixToSymString (SymOpMatrix symOpMatrix, int precision)
		{
			string symOpString = "";
			string thisDimSymString = "";
			for (int dimNo = 0; dimNo < 3; dimNo ++)
			{	
				thisDimSymString = "";
				// X value
				int elemVal = (int)symOpMatrix.Value (dimNo, 0);
				if (elemVal > 0)
				{
					if (thisDimSymString != "")
					{
						thisDimSymString += "+X"; 
					}
					else
					{
						thisDimSymString += "X";
					}
				}
				else if (elemVal < 0)
				{
					thisDimSymString += "-X";
				}

				// Y value
				elemVal = (int)symOpMatrix.Value (dimNo, 1);
				if (elemVal > 0)
				{
					if (thisDimSymString != "")
					{
						thisDimSymString += "+Y"; 
					}
					else
					{
						thisDimSymString += "Y";
					}
				}
				else if (elemVal < 0)
				{
					thisDimSymString += "-Y";
				}
				// Z value
				elemVal = (int)symOpMatrix.Value (dimNo, 2);
				if (elemVal > 0)
				{
					if (thisDimSymString != "")
					{
						thisDimSymString += "+Z"; 
					}
					else
					{
						thisDimSymString += "Z";
					}
				}
				else if (elemVal < 0)
				{
					thisDimSymString += "-Z";
				}
				// for the translation vector
				double vectVal = symOpMatrix.Value (dimNo, 3);
				if (vectVal != 0.0)
				{
					int i = 0;
					string stringFormat = "0.";
					while (i < precision)
					{
						stringFormat += "0";
						i ++;
					}
					thisDimSymString += vectVal.ToString (stringFormat);
				}
				symOpString += thisDimSymString;
				symOpString += ",";
			}
			return symOpString.TrimEnd (',');
		}
		#endregion
	}
}
