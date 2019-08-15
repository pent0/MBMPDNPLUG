using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaintDotNet.Data.MbmFileType
{
    [Serializable]
    class MbmException: Exception
    {
        public MbmException(String message)
            : base(message)
        {

        }
    }
}
