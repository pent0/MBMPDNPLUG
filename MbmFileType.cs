using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using PaintDotNet;
using PaintDotNet.Data;

namespace PaintDotNet.Data.MbmFileType
{
    public class MbmFileTypeFactory: IFileTypeFactory
    {
        public FileType[] GetFileTypeInstances()
        {
            return new FileType[] { new MbmFileType() };
        }
    }

    class MbmFileType: FileType
    {
        public MbmFileType()
            : base("Multi-bitmap",
                  new FileTypeOptions
                  {
                      SaveExtensions = new[] { ".mbm" },
                      LoadExtensions = new[] { ".mbm" },
                      SupportsLayers = false
                  })
        {
        }

        protected override Document OnLoad(Stream fileStream)
        {
            MbmFile file = new MbmFile(fileStream);
            return MbmLoad.Load(file, fileStream);
        }
    }
}
