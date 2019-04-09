using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuxFuncLib
{
    public class StringArrayEqualityComparer : IEqualityComparer <string[]>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        public bool Equals(string[] array1, string[] array2)
        {
            if (array1.Length != array2.Length)
            {
                return false;
            }
            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="intArray"></param>
        /// <returns></returns>
        public int GetHashCode(string[] stringArray)
        {
            int result = 17;
            for (int i = 0; i < stringArray.Length; i++)
            {
                unchecked
                {
                    result = result * 23 + stringArray[i].GetHashCode ();
                }
            }
            return result;
        }
    }
}
