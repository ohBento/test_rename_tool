using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace test_rename_tool
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await ProcessImdbDataAsync();
            SearchAndGetFiles();
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

        static async Task ProcessImdbDataAsync()
        {
            await DownloadDatasetAsync();

            string filePath = @"Imdb_Dataset\title.basics.tsv";

            try
            {

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                string[] lines = File.ReadAllLines(filePath);

                var sortedData = lines
                    .Skip(1)
                    .Select(line => line.Split('\t'))
                    .Where(columns => columns[1] == "movie")
                    .Select(columns => new
                    {
                        Title = columns[3],
                        StartYear = columns[5],
                        //Type = columns[1]
                    })
                    .OrderBy(entry => entry.Title)
                    .ToList();

                string csvFilePath = @"Imdb_Dataset\sorted_filtered_data.csv";
                var csvContent = sortedData
                                 .Select(entry => $"{entry.Title},{entry.StartYear}");

                File.WriteAllLines(csvFilePath, csvContent);
                stopwatch.Stop();
                TimeSpan elapsedTime = stopwatch.Elapsed;

                Console.WriteLine($"File has been sorted and updated. (took {elapsedTime.TotalSeconds:F2}s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static void SearchAndGetFiles()
        {
            while (true)
            {
                try
                {
                    Console.Write("Enter a directory path: ");
                    string directoryPath = Console.ReadLine();

                    if (!Directory.Exists(directoryPath))
                    {
                        Console.WriteLine("Directory path not found. Please try again.");
                        continue;
                    }

                    string fileExtension = GetValidFileExtension();

                    List<string> cleanedNameList = GetCleanedFileNames(directoryPath, fileExtension);
                    List<string> pathFileNameList = GetFilesFromPath(directoryPath, fileExtension);

                    Console.WriteLine("Files with the extension " + fileExtension + ":\n");

                    foreach (string cleanedName in cleanedNameList)
                    {
                        Console.WriteLine($"cleaned Name: {cleanedName}");
                    }

                    SearchAndDisplayMatches(cleanedNameList);

                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
            }
        }

        static List<string> ReadCSVFile()
        {
            string csvFilePath = @"Imdb_Dataset\sorted_filtered_data.csv";
            List<string> csvLines = new List<string>();

            try
            {
                using (StreamReader reader = new StreamReader(csvFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        csvLines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }

            return csvLines;
        }

        static void SearchAndDisplayMatches(List<string> cleanedNameList)
        {
            try
            {
                List<string> csvLines = ReadCSVFile();
                Console.WriteLine("Matching results:");

                foreach (string cleanedName in cleanedNameList)
                {
                    var matchingEntries = csvLines
                        .Select(line => line.Split(','))
                        .Where(columns => columns.Length == 2 && columns[0].Trim() == cleanedName)
                        .ToList();

                    if (matchingEntries.Count > 0)
                    {
                        Console.WriteLine($"Matches found for '{cleanedName}':");
                        for (int i = 0; i < matchingEntries.Count; i++)
                        {
                            Console.WriteLine($"{i}: {matchingEntries[i][0].Trim()} {matchingEntries[i][1].Trim()}");
                        }

                        if (matchingEntries.Count > 1)
                        {
                            Console.Write("Multiple matches found. Please enter the number of the match you want (0, 1, ...): ");
                            int selectedMatchIndex = int.Parse(Console.ReadLine());

                            if (selectedMatchIndex >= 0 && selectedMatchIndex < matchingEntries.Count)
                            {
                                var selectedMatch = matchingEntries[selectedMatchIndex];
                                Console.WriteLine($"Selected match for '{cleanedName}': {selectedMatch[0].Trim()} {selectedMatch[1].Trim()}");
                            }
                            else
                            {
                                Console.WriteLine("Invalid match selection.");
                            }
                        }
                        else
                        {
                            var singleMatch = matchingEntries.First();
                            Console.WriteLine($"Selected match for '{cleanedName}': {singleMatch[0].Trim()} {singleMatch[1].Trim()}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No matches found for '{cleanedName}'.");
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static string GetValidFileExtension()
        {
            while (true)
            {
                Console.Write("Enter a file extension (e.g., 'avi,mp4,mkv...'): ");
                string fileExtension = Console.ReadLine();

                if (fileExtension.StartsWith(".") && fileExtension.Length > 1 && fileExtension.Length < 255)
                {
                    return fileExtension;
                }
                else
                {
                    Console.WriteLine("Invalid file extension format. Please try again.");
                }
            }
        }

        static List<string> GetCleanedFileNames(string path, string extension)
        {
            List<string> cleanedFileNames = new List<string>();

            if (Directory.Exists(path))
            {
                List<string> files = Directory.GetFiles(path, $"*{extension}").ToList();

                foreach (string file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    Match match = Regex.Match(fileName, @"^(.*)(?=\.\d{4}\.)(\.\d{4})");
                    if (match.Success)
                    {

                        string namePart = match.Groups[1].Value.Trim();
                        cleanedFileNames.Add(namePart.Replace('.', ' '));
                    }
                    else
                    {
                        cleanedFileNames.Add(fileName);
                    }
                }
            }
            else
            {
                Console.WriteLine("Invalid path.");
            }

            return cleanedFileNames;
        }

        static List<string> GetFilesFromPath(string directoryPath, string fileExtension)
        {
            List<string> fileNamesFromPath = new List<string>();

            var fileNames = Directory.GetFiles(directoryPath, "*" + fileExtension);
            foreach (string fileName in fileNames)
            {
                if (File.Exists(fileName))
                {
                    fileNamesFromPath.Add(fileName);
                }
            }
            return fileNamesFromPath;
        }
    }
}





