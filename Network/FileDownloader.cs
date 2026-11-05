using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Elysium.Core.Utils;

namespace PLDUOBAO.Network
{
    internal class FileDownloader
    {
        public static async Task DownloadFileAsync(string url, string outputPath)
        {
            string FileName = Path.GetFileName(url);
            using (HttpClient client = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null,
            }))
            {
                using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0L;
                        int read;
                        int previousProgress = 0;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (canReportProgress)
                            {
                                int progress = (int)(totalRead * 100 / totalBytes);
                                if (progress != previousProgress)
                                {
                                    LogSys.Log($"\r{FileName} [{new string('#', progress / 2)}{new string('-', 50 - progress / 2)}] {progress}%");
                                    previousProgress = progress;
                                }
                            }
                        }
                    }
                }
            }
            LogSys.Log($"{url}下载完成！");
        }
    }
}
