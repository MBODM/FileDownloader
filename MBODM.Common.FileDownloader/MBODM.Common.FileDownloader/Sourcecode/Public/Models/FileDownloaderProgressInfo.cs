using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBODM.Common
{
    public sealed class FileDownloaderProgressInfo
    {
        public FileDownloaderProgressInfo() : this(null, null, 0, 0, null)
        {
        }

        public FileDownloaderProgressInfo(Uri url, string file, int chunkSize, int totalSize, object tag)
        {
            Url = url;
            File = file;
            ChunkSize = chunkSize;
            TotalSize = totalSize;
            Tag = tag;
        }

        public Uri Url { get; }
        public string File { get; }
        public int ChunkSize { get; }
        public int TotalSize { get; }
        public object Tag { get; }
    }
}
