using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MBODM.Common
{
    public sealed class FileDownloader : IFileDownloader
    {
        public Task DownloadFileAsync(Uri url, string file)
        {
            return DownloadFileAsync(url, file, CancellationToken.None, null, null);
        }

        public Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken)
        {
            return DownloadFileAsync(url, file, cancellationToken, null, null);
        }

        public Task DownloadFileAsync(Uri url, string file, IProgress<FileDownloaderProgressInfo> progress)
        {
            return DownloadFileAsync(url, file, CancellationToken.None, progress, null);
        }

        public Task DownloadFileAsync(Uri url, string file, IProgress<FileDownloaderProgressInfo> progress, object tag)
        {
            return DownloadFileAsync(url, file, CancellationToken.None, progress, tag);
        }

        public Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken, IProgress<FileDownloaderProgressInfo> progress)
        {
            return DownloadFileAsync(url, file, cancellationToken, progress, null);
        }

        public async Task DownloadFileAsync(Uri url, string file, CancellationToken cancellationToken, IProgress<FileDownloaderProgressInfo> progress, object tag)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentException("Argument is null or empty.", nameof(file));
            }

            file = Path.GetFullPath(file.Trim()).Trim();

            if (string.IsNullOrEmpty(Path.GetFileName(file).Trim()))
            {
                throw new FileDownloaderException("No file name in file path.");
            }

            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength > int.MaxValue)
                {
                    var gbSize = (double)int.MaxValue / (1024 * 1024 * 1024);
                    var gbText = $"{(Math.Truncate(gbSize * 100) / 100):0.00}";

                    throw new FileDownloaderException($"A file download with more than {gbText} gigabytes is not supported.");
                }

                var totalSize = (int)response.Content.Headers.ContentLength;

                if (totalSize <= 0)
                {
                    throw new FileDownloaderException("The Content-Length header field in the server response was 0 or less.");
                }

                // The concept for downloading a file is a little bit special here. The first concept ensured we
                // always got exact 100 steps of progress (1% steps) for every file, regardless how big the file
                // was. For big files this was great. Because we reduced unnecessary updates, since 1% steps are
                // always fine granulated enough. More than 100 1% steps is a waste of resources and performance.
                // But for small files that approach was not good. Since we calculated mathematically the buffer
                // size for 1 chunk (1%), the buffer became that small, that the application was completely busy,
                // for a short amount of time, while handling all that fast incoming tiny chunks. Short 100% cpu
                // peeks on all cores (for that amount of time) occurred. As a result our test GUI was unable to
                // react, for that amount of time, and some progress bars acted very ugly. We also realized that
                // it is impossible to even show the progress of tiny files, because i.e. a download with 1 MBit
                // produce 100 progress updates which are way faster than the human eye. For that reasons things
                // were changed slightly. First we calculated a chunk size for a frequency the human eye can see.
                // A chunk size of 4096 bytes at 1 MBit/s download speed produce ~30 changes per second = ~30 Hz.
                // So all tiny files have just 1, or 2, or 3 (and so on) progress updates. And thats fine, since
                // our eyes can not see more updates in that amount of time. On the other side, this size is big
                // enough to prevent 100% cpu peeks on an actual standard PC. As we said above, for big files it
                // is fine to not produce more than 100 1% steps. So for all files bigger than 100 chunks of our
                // 4096 chunk size, we increase that chunk size (buffer size calculation), to stick with exactly
                // 100 chunks. After some testing, 4096 seems to be a good value for us. Epilog: Lessons learned.

                var readBytes = 0;
                var streamBuffer = new byte[4096];

                if (totalSize > streamBuffer.Length * 100)
                {
                    if (totalSize % 100 == 0)
                    {
                        streamBuffer = new byte[totalSize / 100];
                    }
                    else
                    {
                        streamBuffer = new byte[(totalSize / 100) + 1];
                    }
                }

                using (var httpStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(file).Trim()))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(file).Trim());
                    }

                    using (var fileStream = File.Create(file))
                    {
                        try
                        {
                            while (true)
                            {
                                // The ReadChunk() method ensures we get only 1 of the following 3 results:
                                // 1) A byte count == size of the buffer -> We read a normal buffer sized chunk.
                                // 2) A byte count > 0 but < size of buffer -> We read the last chunk of all chunks.
                                // 3) A byte count == 0 -> We already finished because of "total % x == 0" sized chunks.

                                readBytes = await ReadChunk(streamBuffer, httpStream, cancellationToken).ConfigureAwait(false);

                                if (readBytes <= 0)
                                {
                                    break;
                                }

                                await fileStream.WriteAsync(streamBuffer, 0, readBytes, cancellationToken).ConfigureAwait(false);

                                if (progress != null)
                                {
                                    progress.Report(new FileDownloaderProgressInfo(url, file, readBytes, totalSize, tag));
                                }
                            }
                        }
                        finally
                        {
                            httpStream.Close();
                            fileStream.Close();
                        }
                    }
                }
            }
        }

        private async Task<int> ReadChunk(byte[] streamBuffer, Stream httpStream, CancellationToken cancellationToken)
        {
            // This method make sure we always read a complete chunk. A complete chunk is a part of the
            // whole file and must have exactly the same size as the buffer. Only the last chunk of all
            // chunks is allowed to be smaller than the buffer size. We rely on this behaviour, because
            // our concept works as described above. The response of some servers, including the one we
            // use, sometimes contains a smaller amount of bytes than we requested, via a Stream.Read().
            // So this method ensures we fill our buffer completely, or in other words: Receive 1 chunk.

            var readBytes = 0;
            var allReadBytes = 0;
            var remainingBytes = streamBuffer.Length;

            while (true)
            {
                readBytes = await httpStream.ReadAsync(streamBuffer, allReadBytes, remainingBytes, cancellationToken).ConfigureAwait(false);

                if (readBytes <= 0)
                {
                    return allReadBytes;
                }

                allReadBytes += readBytes;

                if (allReadBytes == streamBuffer.Length)
                {
                    return allReadBytes;
                }

                remainingBytes -= readBytes;
            }
        }
    }
}
