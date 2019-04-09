using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace InterfaceClusterLib.AuxFuncs
{
    public class PfamFileName
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        public bool IsPfamIdValidFileName (string pfamId)
        {
            if (pfamId.IndexOfAny (System.IO.Path.GetInvalidFileNameChars ()) <  0)
            {
                return true;
            }
            return false;
        }
    }
}
