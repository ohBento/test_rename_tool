using System.IO.Compression;

namespace test_rename_tool
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
           await DownloadDatasetAsync();
        }

        static async Task DownloadDatasetAsync()
        {
            string url = "https://datasets.imdbws.com/title.basics.tsv.gz";
            string rootDir = Directory.GetCurrentDirectory();
            string downloadDir = "Imdb_Dataset";

            string folderPath = Path.Combine(rootDir, downloadDir);
            Directory.CreateDirectory(folderPath);

            string fileName = Path.GetFileName(url);
            string localFilePath = Path.Combine(folderPath, fileName);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Console.WriteLine("Starting file download from imdb...");

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        using (FileStream fs = new FileStream(localFilePath, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fs);
                        }

                        Console.WriteLine("File downloaded successfully!");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to download file. Status code: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }

            string extractionPath = folderPath;
            string extractedFileName = Path.GetFileNameWithoutExtension(localFilePath);

            //Directory.CreateDirectory(extractionPath);

            using (FileStream fileStream = new FileStream(localFilePath, FileMode.Open))
            using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (FileStream outputFileStream = File.Create(Path.Combine(extractionPath, extractedFileName)))
            {
                gzipStream.CopyTo(outputFileStream);
            }

            File.Delete(localFilePath);

            Console.WriteLine($"Extraction completed.");
            Console.WriteLine("Sorting file...");
        }
    }
}