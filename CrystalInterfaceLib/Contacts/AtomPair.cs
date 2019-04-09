using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrystalInterfaceLib.Crystal;

namespace CrystalInterfaceLib.Contacts
{
    /// <summary>
    /// a pair of atoms and their distance
    /// </summary>
    public class AtomPair
    {
        public AtomInfo firstAtom;
        public AtomInfo secondAtom;
        public double distance;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extAtomPair"></param>
        public void DeepCopy (AtomPair extAtomPair)
        {
            this.firstAtom = (AtomInfo)extAtomPair.firstAtom.Clone(); // deep copy the atom info
            this.secondAtom = (AtomInfo)extAtomPair.secondAtom.Clone();
            this.distance = extAtomPair.distance;
        }
    }
}
