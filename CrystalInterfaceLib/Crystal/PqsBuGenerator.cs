using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Xml.Serialization;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.BuIO;
using AuxFuncLib;
using DbLib;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for PqsBuGenerator.
	/// </summary>
	public class PqsBuGenerator : CrystalBuilder
	{
		public PqsBuGenerator()
		{
		}


		#region format full symmetry string
		/// <summary>
		/// convert a full symmetry string into a regular symmetry string 
		/// e.g. -X+1, -Y+X+1, -Z+2/3 to 3_665
		/// here, 3 is the symmetry operator number in the space group
		/// 665 is the translation vector in 3D
		/// </summary>
		/// <param name="fullSymString"></param>
		/// <param name="sgSymOpMatrices"></param>
		/// <returns></returns>
		private string ConvertFullToSymString (string fullSymString, SymOpMatrix[] sgSymOpMatrices)
		{
			SymOperator symOp = new SymOperator ();
			string symOpString = "";
			string translateVector = "";
			string formattedFullSymString = RetrieveSymStringNoTranslation (fullSymString, ref translateVector);

			foreach (SymOpMatrix symOpMatrix in sgSymOpMatrices)
			{
				symOpString = symOp.ConvertMatrixToSymString (symOpMatrix, 1);
				int strCompIndex = string.Compare (formattedFullSymString, symOpString);
				if (strCompIndex == 0)
				{
					return symOpMatrix.symmetryOpNum.ToString () + "_" + translateVector;
				}
			}
			return "";
		}

		/// <summary>
		///  convert all symmetry string to PQS formatted
		///  the format of full symmetry string: e.g. 1/2+X, -9/4-Y+Z, -Z
		///  Get the original symmetry string without translation
		/// </summary>
		/// <param name="symString">PDB formatted full symmetry string</param>
		/// <returns>the translation vector</returns>
		private string RetrieveSymStringNoTranslation (string fullSymString, ref string translateVector)
		{
			translateVector = "";
			fullSymString = fullSymString.ToUpper ();
			string formattedSymString = "";
			string[] xyzStrings = fullSymString.Split (',');
			int i = 0;
			double  translateVal = 0;
			foreach (string coordString in xyzStrings)
			{
				string doubleString = "";
				string axesString = "";
				string tempString = "";
				foreach (char ch in coordString)
				{
					tempString += ch;
					if (Char.IsDigit (ch))
					{
						doubleString += tempString;
						tempString = "";
					}
					if (ch == 'X' || ch == 'Y' || ch == 'Z')
					{
						axesString += tempString;
						tempString = "";
					}
				}
				string vectorString = "";
				if (doubleString.IndexOf ("/") > -1)
				{
					char signChar = '+';
					if (doubleString[0] == '-' || doubleString[0] == '+')
					{
						signChar = doubleString[0];
						doubleString = doubleString.Substring (1, doubleString.Length - 1);
					}
					
					double vectVal = ConvertToDouble (doubleString);
					if (signChar == '-')
					{
						vectVal = vectVal * (-1);
					}
					if (vectVal > 1.0 || vectVal < 0.0)
					{
						translateVal = Math.Floor (vectVal);
						vectVal -= translateVal;
					}
					vectorString = vectVal.ToString ("0.0");	
					if (signChar == '+')
					{
						vectorString = "+" + vectorString;
					}
				}
				else
				{
					// no translation in the symmetry operation in the space group
					double vectVal = Convert.ToInt32 (vectorString);
					translateVal = vectVal;
					vectorString = "";
				}
				axesString = axesString.TrimStart ('+');
				formattedSymString += (axesString + vectorString);
				formattedSymString += ",";
				translateVector += Convert.ToString ((int)translateVal + 5);
				i ++;
			}
			return formattedSymString.TrimEnd (',');
		}

		/// <summary>
		/// convert a/b to a double
		/// </summary>
		/// <param name="doubleString"></param>
		/// <returns></returns>
		private double ConvertToDouble (string doubleString)
		{
			string [] vectorSubStr = doubleString.Split ('/');			
			return System.Convert.ToDouble (vectorSubStr[0]) / System.Convert.ToDouble (vectorSubStr[1]);
		}
		#endregion
	}
}
