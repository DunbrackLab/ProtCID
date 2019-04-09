using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuxFuncLib
{
    public class SeqCrc
    {
        /*
         *  # https://github.com/pontikos/UniProt/blob/master/crc64.py
            # 32 first bits of generator polynomial for CRC64
            # the 32 lower bits are assumed to be zero

            POLY64REVh = 0xd8000000L
            CRCTableh = [0] * 256
            CRCTablel = [0] * 256
            isInitialized = False

            def CRC64(aString):
                global isInitialized
                crcl = 0
                crch = 0
                if (isInitialized is not True): isInitialized = True
                for i in xrange(256): 
                    partl = i
            parth = 0L
            for j in xrange(8):
                rflag = partl & 1L                
                partl >>= 1L               
                if (parth & 1): partl |= (1L << 31L)
                parth >>= 1L
                if rflag: parth ^= POLY64REVh
            CRCTableh[i] = parth;
            CRCTablel[i] = partl;
            for item in aString:
                shr = 0L
            shr = (crch & 0xFF) << 24
            temp1h = crch >> 8L
            temp1l = (crcl >> 8L) | shr                        
            tableindex = (crcl ^ ord(item)) & 0xFF
            crch = temp1h ^ CRCTableh[tableindex]
            crcl = temp1l ^ CRCTablel[tableindex]
            return (crch, crcl)

            def CRC64digest(aString): return "%08X%08X" % (CRC64(aString))

            if __name__ == '__main__':
                assert CRC64("IHATEMATH") == (3822890454, 2600578513)
                assert CRC64digest("IHATEMATH") == "E3DCADD69B01ADD1"
                print 'CRC64: dumb test successful'

         * */
        /// <summary>
        /// convert from crc64.py
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public string GetCrc64 (string sequence)
        {
            ulong poly64Revh = 0xd8000000;
            
            bool initialized = false;

            ulong[] crcTableh = new ulong[256];
            ulong[] crcTablel = new ulong[256];

            ulong crch = 0;
            ulong crcl = 0;
            ulong partl = 0;
            ulong parth = 0;
            ulong lflag = 0;
            ulong hflag = 0;
            long Bit_toggle = 1 << 31;
            if (! initialized)
            {
                initialized = true;
                for (uint i = 0;  i < 256; i ++)
                {
                    partl = i;
                    parth = 0;
                    for (int j = 0; j < 8; j ++)
                    {
                        lflag = partl & 1;
                        partl >>= 1;
                        hflag = parth & 1;
                        if (hflag == 1)
                        {
                            partl |= (ulong)Bit_toggle;
                        }
                        parth >>= 1;
                        if (lflag == 1)
                        {
                            parth ^= poly64Revh;
                        }
                    }
                    crcTableh[i] = parth;
                    crcTablel[i] = partl;
                }
            }

            ulong shr = 0;
            ulong temph = 0;
            ulong templ = 0;
            ulong tableIndex = 0;
            foreach (char ch in sequence)
            {
                shr = (crch & 0xFF) << 24;
                temph = crch >> 8;
                templ = (crcl >> 8) | shr;
                tableIndex = (crcl ^ ((uint)Convert.ToInt64(ch))) & 0xFF;
                crch = temph ^ crcTableh[tableIndex];
                crcl = templ ^ crcTablel[tableIndex];
            }
            return crch.ToString("X8") + crcl.ToString("X8");
        }
    }
}
