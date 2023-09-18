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
            //await ProcessImdbDataAsync();
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
                    .Where(columns => columns[1] == "movie" && columns[5] != "\\N")
                    .Select(columns => new
                    {
                        Title = columns[3],
                        StartYear = columns[5],
                        //Type = columns[1]
                    })
                    .OrderBy(entry => entry.Title)
                    .ToList();

                string csvFilePath = @"Imdb_Dataset\sorted_filtered_imdb_data.csv";
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

                    Dictionary<string, string> cleanedOriginalNameMap = GetCleanedFileNames(directoryPath, fileExtension);

                    if (cleanedOriginalNameMap.Count == 0)
                    {
                        Console.WriteLine("No valid files found in the directory. Please try again.");
                        continue; // Skip the rest of the loop and start over
                    }

                    Dictionary<string, string> selectedMatches = SearchAndDisplayMatches(cleanedOriginalNameMap);

                    if (selectedMatches.Count == 0)
                    {
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.Write("\nDo you want to rename the files to the selected name (y/n)?: ");
                        string renameChoice = Console.ReadLine().ToLower();

                        if (renameChoice == "y")
                        {
                            Console.WriteLine();
                            RenameFiles(directoryPath, selectedMatches, fileExtension);
                            Console.WriteLine("\nFile renaming completed.");
                        }
                        else if (renameChoice == "n")
                        {
                            Console.WriteLine("Rename aborted.");
                        }
                        else
                        {
                            Console.WriteLine("Invalid choice. Please enter 'y' or 'n'.");
                        }
                    }

                    Console.Write("\nDo you want to perform another operation? (y/n): ");
                    string continueChoice = Console.ReadLine().ToLower();

                    if (continueChoice != "y")
                    {
                        Console.Clear();
                        break;
                        
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
            }
        }

        static void RenameFiles(string directoryPath, Dictionary<string, string> selectedMatches, string fileExtension)
        {
            try
            {
                foreach (var selectedMatch in selectedMatches)
                {
                    string originalFileName = selectedMatch.Key;
                    string newFileName = selectedMatch.Value;
                    string sourceFilePath = Path.Combine(directoryPath, originalFileName + fileExtension);
                    string destinationFilePath = Path.Combine(directoryPath, newFileName + fileExtension);

                    File.Move(sourceFilePath, destinationFilePath);
                    Console.WriteLine($"{originalFileName,-80} =>\t {newFileName}{fileExtension}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while renaming files: " + ex.Message);
            }
        }

        static List<string> ReadCSVFile()
        {
            string csvFilePath = @"Imdb_Dataset\sorted_filtered_imdb_data.csv";
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

        static Dictionary<string, string> SearchAndDisplayMatches(Dictionary<string, string> cleanedOriginalNameMap)
        {
            Dictionary<string, string> selectedMatches = new Dictionary<string, string>();

            try
            {
                List<string> csvLines = ReadCSVFile();
                Console.WriteLine("Matching results:\n");
                List<string> noMatchLines = new List<string>();

                foreach (var cleanedOriginalPair in cleanedOriginalNameMap)
                {
                    string cleanedName = cleanedOriginalPair.Key;
                    string originalFileName = cleanedOriginalPair.Value;

                    var matchingEntries = csvLines
                        .Select(line => line.Split(','))
                        .Where(columns => columns.Length == 2 && columns[0].Trim() == cleanedName)
                        .ToList();

                    if (matchingEntries.Count > 0)
                    {
                        matchingEntries = matchingEntries
                            .OrderBy(entry => int.TryParse(entry[1].Trim(), out int year) ? year : int.MaxValue)
                            .ToList();

                        Console.WriteLine($"Matches found for '{originalFileName}':");
                        for (int i = 0; i < matchingEntries.Count; i++)
                        {
                            Console.WriteLine($"{i}: {matchingEntries[i][0].Trim()} {matchingEntries[i][1].Trim()}");
                        }

                        if (matchingEntries.Count == 1)
                        {
                            var selectedMatch = matchingEntries[0];
                            Console.WriteLine($"Selected match for {originalFileName,-80} =>\t {selectedMatch[0].Trim()} ({selectedMatch[1].Trim()})");
                            Console.WriteLine(new String('-', 130));

                            selectedMatches.Add(originalFileName, $"{selectedMatch[0].Trim()} ({selectedMatch[1].Trim()})");
                        }
                        else
                        {
                            while (true)
                            {
                                Console.Write("Please enter the number of the match you want (0, 1, ...): ");
                                string selectedMatchIndexStr = Console.ReadLine();

                                if (int.TryParse(selectedMatchIndexStr, out int selectedMatchIndex) &&
                                    selectedMatchIndex >= 0 && selectedMatchIndex < matchingEntries.Count)
                                {
                                    var selectedMatch = matchingEntries[selectedMatchIndex];
                                    Console.WriteLine($"Selected match for {originalFileName,-80} =>\t {selectedMatch[0].Trim()} ({selectedMatch[1].Trim()})");
                                    Console.WriteLine(new String('-', 130));

                                    selectedMatches.Add(originalFileName, $"{selectedMatch[0].Trim()} ({selectedMatch[1].Trim()})");
                                    break; 
                                }
                                else
                                {
                                    Console.WriteLine("Invalid match selection. Please enter a valid digit.");
                                }
                            }
                        }
                    }
                    else
                    {
                        noMatchLines.Add(originalFileName);
                    }
                }

                Console.WriteLine("\nNo match found for:");
                foreach (var line in noMatchLines)
                {
                    Match match = Regex.Match(line, @"\(\d{4}\)");
                    if (!match.Success)
                    {
                        Console.WriteLine(line);
                    }
                }

                return selectedMatches;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return selectedMatches;
            }
        }

        static string GetValidFileExtension()
        {
            while (true)
            {
                Console.Write("Enter a file extension ('e.g. .avi/mp4/mkv...'): ");
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

        static Dictionary<string, string> GetCleanedFileNames(string path, string extension)
        {
            Dictionary<string, string> cleanedOriginalNameMap = new Dictionary<string, string>();

            if (Directory.Exists(path))
            {
                List<string> files = Directory.GetFiles(path, $"*{extension}").ToList();

                foreach (string file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    Match match = Regex.Match(fileName, @"^(.*)(?=\.\d{4}\.)(\.\d{4})");
                    if (match.Success)
                    {
                        string namePart = match.Groups[1].Value.Trim().Replace('.', ' ');
                        cleanedOriginalNameMap[namePart] = fileName;
                    }
                    else
                    {
                        cleanedOriginalNameMap[fileName] = fileName;
                    }
                }
            }
            else
            {
                Console.WriteLine("Invalid path.");
            }

            return cleanedOriginalNameMap;
        }

    }
}
