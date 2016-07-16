using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MBODM.Common
{
    public interface IFileDownloader
    {
        Task DownloadFileAsync(Uri url, string file);
        Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken);
        Task DownloadFileAsync(Uri url, string file, IProgress<FileDownloaderProgressInfo> progress);
        Task DownloadFileAsync(Uri url, string file, IProgress<FileDownloaderProgressInfo> progress, object tag);
        Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken, IProgress<FileDownloaderProgressInfo> progress);
        Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken, IProgress<FileDownloaderProgressInfo> progress, object tag);
    }
}
