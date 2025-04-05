using System.Collections.Concurrent;
using System.Text.Json;
using ct.console.common;
using ct.console.model;

namespace ct.console.services;

public class CtDownloader
{
    private static readonly ConcurrentDictionary<string, double> ProgressMap = new(); // Shared progress tracker

    private static readonly ConcurrentDictionary<string, string>
        DownloadStatus = new(); // To display status (e.g., Done, Error)

    public async Task Run(string[] args)
    {
        var serverUrl = args.GetServerUrl();
        var chunkMap = await args.LoadChunkMap();
        var outputFolder = args.GetOutput();

        using var httpClient = new HttpClient();

        using var semaphore = new SemaphoreSlim(args.GetThreadsCount());

        var tasks = chunkMap.Select(chunk => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                var chunkValue = chunk.Value;
                var partName =
                    $"{Path.GetFileName(chunkValue.FilePath)}.part{chunkValue.Index}_of_{chunkValue.Total}.enc";

                try
                {
                    if (File.Exists(partName)) // Skip if already downloaded
                    {
                        ProgressMap[chunk.Key] = 100; // Prevent stuck progress display
                        return;
                    }

                    var response = await httpClient.GetAsync($"{serverUrl}/download/{chunk.Key}");
                    if (response.IsSuccessStatusCode)
                    {
                        long? contentLength = response.Content.Headers.ContentLength;
                        if (contentLength == null)
                        {
                            DownloadStatus[chunk.Key] = "Error (No Content Length)";
                            return;
                        }

                        await using var responseStream = await response.Content.ReadAsStreamAsync();

                        string outputPath = Path.Combine(outputFolder, partName);

                        var bufferSize = 8192 * 1024;

                        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                            FileShare.None, bufferSize, true);

                        byte[] buffer = new byte[bufferSize];
                        long totalRead = 0;
                        int bytesRead;

                        ProgressMap[chunk.Key] = 0; // Initialize chunk progress
                        DownloadStatus[chunk.Key] = "Downloading"; // Update the status

                        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            // Update progress
                            ProgressMap[chunk.Key] = (double)totalRead / contentLength.Value * 100;
                            await ProgressMap.ReportProgress();
                        }

                        ProgressMap[chunk.Key] = 100; // Mark as complete
                        DownloadStatus[chunk.Key] = "Done"; // Update status
                        await ProgressMap.ReportProgress();
                    }
                    else
                    {
                        ProgressMap[chunk.Key] = 100; // Prevent stuck progress display
                        DownloadStatus[chunk.Key] = $"Error ({response.StatusCode})";
                        await ProgressMap.ReportProgress();
                    }
                }
                catch (Exception ex)
                {
                    ProgressMap[chunk.Key] = 100;
                    DownloadStatus[chunk.Key] = $"Error ({ex.Message})";
                    await ProgressMap.ReportProgress();
                }
                finally
                {
                    semaphore.Release();
                }
            }))
            .ToList();

        await Task.WhenAll(tasks);
        await Task.Delay(200);

        Console.Clear();
        Console.WriteLine("All chunks downloaded successfully.");
        DownloadStatus.Dump("downloadStatus.json");
    }
}