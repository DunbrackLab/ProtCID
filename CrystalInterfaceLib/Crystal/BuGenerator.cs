using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Data;
using DbLib;

namespace CrystalInterfaceLib.Crystal
{
    public class BuGenerator
    {
        private DbQuery dbQuery = new DbQuery ();

        #region generate PDB BUs from DB data and XML files

        #region build BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <param name="chainMatrixHash"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildOneBuAssembly(EntryCrystal thisEntryCrystal, Dictionary<string, List<SymOpMatrix>> chainMatrixHash, string type)
        {
            Dictionary<string, AtomInfo[]> chainCoordHash = new Dictionary<string,AtomInfo[]> ();

            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            ChainAtoms thisChain = null;

            foreach (string asymChain in chainMatrixHash.Keys)
            {
                thisChain = null; // fixed on June 30, 2010
                foreach (ChainAtoms chain in chainAtomsList)
                {
                    if (chain.AsymChain == asymChain)
                    {
                        thisChain = chain;
                        break;
                    }
                }
                if (thisChain == null)
                {
                    continue;
                }

                string chainSymOpString = "";
                foreach (SymOpMatrix symMatrix in chainMatrixHash[asymChain])
                {
                    if (symMatrix.symmetryString == "-" || symMatrix.symmetryString == "")
                    {
                        chainSymOpString = asymChain + "_" + symMatrix.symmetryOpNum + "_-";
                    }
                    else
                    {
                        chainSymOpString = asymChain + "_" + symMatrix.symmetryString;
                        if (type == "pisa")
                        {
                            if (symMatrix.symmetryOpNum != "" && symMatrix.symmetryOpNum != "0")
                            {
                                chainSymOpString += ("(" + symMatrix.symmetryOpNum + ")"); // for those non-NCS operator (same symmetry string)
                            }
                        }
                    }
                    if (!chainCoordHash.ContainsKey(chainSymOpString))
                    {
                        AtomInfo[] atoms = BuildOneChain(thisChain, symMatrix);
                        chainCoordHash.Add(chainSymOpString, atoms);
                    }
                }
            }
            return chainCoordHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <param name="chainMatrixHash"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildOneBuAssembly(string xmlFile, Dictionary<string, List<SymOpMatrix>> chainMatrixHash, string type)
        {
            Dictionary<string, AtomInfo[]> chainCoordHash = new Dictionary<string, AtomInfo[]>();

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            ChainAtoms thisChain = null;

            foreach (string asymChain in chainMatrixHash.Keys)
            {
                thisChain = null; // fixed on June 30, 2010
                foreach (ChainAtoms chain in chainAtomsList)
                {
                    if (chain.AsymChain == asymChain)
                    {
                        thisChain = chain;
                        break;
                    }
                }
                if (thisChain == null)
                {
                    continue;
                }
    
                string chainSymOpString = "";
                foreach (SymOpMatrix symMatrix in chainMatrixHash[asymChain])
                {
                    if (symMatrix.symmetryString == "-" || symMatrix.symmetryString == "")
                    {
                        chainSymOpString = asymChain + "_" + symMatrix.symmetryOpNum + "_-";
                    }
                    else
                    {
                        chainSymOpString = asymChain + "_" + symMatrix.symmetryString;

                        if (type == "pisa")
                        {
                            if (symMatrix.symmetryOpNum != "" && symMatrix.symmetryOpNum != "0")
                            {
                                chainSymOpString += ("(" + symMatrix.symmetryOpNum + ")"); // those non-ncs operator (same symmetry string)
                            }
                        }
                    }
                    if (! chainCoordHash.ContainsKey(chainSymOpString))
                    {
                        AtomInfo[] atoms = BuildOneChain(thisChain, symMatrix);
                        chainCoordHash.Add(chainSymOpString, atoms);
                    }
                }
            }
            return chainCoordHash;
        }

        /// <summary>
        /// transform one chain 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="symMatrix"></param>
        /// <returns></returns>
        public AtomInfo[] BuildOneChain(ChainAtoms chain, SymOpMatrix symMatrix)
        {
            if (symMatrix.symmetryString == "1_555")
            {
                return chain.CartnAtoms;
            }
            AtomInfo[] transformedAtoms = new AtomInfo[chain.CartnAtoms.Length];
            int i = 0;
            foreach (AtomInfo atom in chain.CartnAtoms)
            {
                AtomInfo transformedAtom = atom.TransformAtom(symMatrix);
                transformedAtoms[i] = transformedAtom;
                i++;
            }
            return transformedAtoms;
        }
        #endregion

        /// <summary>
        /// the symmetry operator string
        /// </summary>
        /// <param name="infoNode"></param>
        /// <returns></returns>
        public SymOpMatrix GetCoordSymOpMatrix(string matrixString)
        {
            string[] fields = matrixString.Split(',');
            SymOpMatrix symOpMatrix = new SymOpMatrix();
            List<double> itemList = new List<double> ();
            itemList.Add(Convert.ToDouble(fields[0]));
            itemList.Add(Convert.ToDouble(fields[1]));
            itemList.Add(Convert.ToDouble(fields[2]));
            itemList.Add(Convert.ToDouble(fields[3]));
            symOpMatrix.Add(0, itemList.ToArray ());
            itemList.Clear();
            itemList.Add(Convert.ToDouble(fields[4]));
            itemList.Add(Convert.ToDouble(fields[5]));
            itemList.Add(Convert.ToDouble(fields[6]));
            itemList.Add(Convert.ToDouble(fields[7]));
            symOpMatrix.Add(1, itemList.ToArray ());
            itemList.Clear();
            itemList.Add(Convert.ToDouble(fields[8]));
            itemList.Add(Convert.ToDouble(fields[9]));
            itemList.Add(Convert.ToDouble(fields[10]));
            itemList.Add(Convert.ToDouble(fields[11]));
            symOpMatrix.Add(2, itemList.ToArray ());
            return symOpMatrix;
        }

        /// <summary>
        /// the symmetry operator string
        /// </summary>
        /// <param name="infoNode"></param>
        /// <returns></returns>
        public SymOpMatrix GetOrigCoordSymOpMatrix()
        {
            SymOpMatrix symOpMatrix = new SymOpMatrix();
            List<double> itemList = new List<double> ();
            itemList.Add(1.0);
            itemList.Add(0.0);
            itemList.Add(0.0);
            itemList.Add(0.0);
            symOpMatrix.Add(0, itemList.ToArray ());
            itemList.Clear();
            itemList.Add(0.0);
            itemList.Add(1.0);
            itemList.Add(0.0);
            itemList.Add(0.0);
            symOpMatrix.Add(1, itemList.ToArray ());
            itemList.Clear();
            itemList.Add(0.0);
            itemList.Add(0.0);
            itemList.Add(1.0);
            itemList.Add(0.0);
            symOpMatrix.Add(2, itemList.ToArray ());
            return symOpMatrix;
        }
        #endregion
    }
}
