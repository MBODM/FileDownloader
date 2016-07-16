using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MBODM.Common
{
    [Serializable]
    public class FileDownloaderException : Exception
    {
        public FileDownloaderException() { }
        public FileDownloaderException(string message) : base(message) { }
        public FileDownloaderException(string message, Exception inner) : base(message, inner) { }
        protected FileDownloaderException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
