using System;
using System.Net.NetworkInformation;
using System.IO;
using System.Security.Principal;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;


namespace Check_Version_GPOSESCALE
{
    class Program
    {
        static IPStatus pingable(string host)
        {
            Ping p = new Ping();
            PingReply pr = p.Send(host, 500);
            return pr.Status;
        }
        static IConfiguration _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        static Dictionary<string, List<string>> versions = new Dictionary<string, List<string>>();
        static ConsoleKeyInfo copyNewVersions;
        static string tempFullPath = Path.GetTempPath() + "archivotemp.txt";
        static string conectedTPVs = "Caixes conectades: {0}\t| Caixes desconectades: {1}";
        static string logReportError = $"log_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt";
        static void WriteDateVersions(string filePath, string letterVersion)
        {
            DateTime fileDate = File.GetLastWriteTime(filePath);
            Console.WriteLine($"Versió {letterVersion.ToUpper()}: {fileDate.ToString("dd/MM/yyyy HH:mm:ss")}");
        }
        static void StartMessage()
        {
            try
            {
                Console.WriteLine("Programa de comprobació de versions del GPOSESCALE.EXE i ALTRES");
                Console.WriteLine("---------------------------------------------------------------");
                Console.WriteLine();
                Console.WriteLine("Iniciant la comprobació de versions del GPOSESCALE.EXE i carpeta {0}", _config["PATHS:TOCOPY"]);
                Console.WriteLine();
                Console.WriteLine("Les dades de les versions del GPOSESCALE son:");
                Console.WriteLine();
                WriteDateVersions(Path.Combine(_config["PATHS:REPONERPOS"], _config["FILES:A"]), "A");
                WriteDateVersions(Path.Combine(_config["PATHS:REPONERPOS"], _config["FILES:M"]), "M");
                WriteDateVersions(Path.Combine(_config["PATHS:REPONERPOS"], _config["FILES:S"]), "S");
                WriteDateVersions(Path.Combine(_config["PATHS:REPONERPOS"], _config["FILES:T"]), "T");
                Console.WriteLine();
                Console.WriteLine("Les dades dels arxius trobats dins de {0} son:", _config["PATHS:TOCOPY"]);
                Console.WriteLine();
                var files = Directory.GetFiles(_config["PATHS:TOCOPY"]);
                foreach (var file in files)
                {
                    WriteDateVersions(file, Path.GetFileName(file));
                }
                Console.WriteLine();
                Console.WriteLine("Es mostrará la llista de caixes amb la versió desactualitzada.");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Voldràs copiar la versió a les caixes que estiguin desactualitzades? S/N");
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al iniciar el programa: {ex.Message}");
            }
        }
        static void Executing()
        {
            try
            {
                File.Copy(Path.Combine(_config["PATHS:GEINSA"], _config["FILES:TERMINAL"]), tempFullPath, true);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Report dia {DateTime.Now}");
                sb.AppendLine(new string('_', 20));

                using (StreamReader sr = new StreamReader(tempFullPath))
                {
                    string line;
                    int on = 0;
                    int off = 0;
                    versions = GetVersionSection();
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] lineParts = line.Split('\t');
                        int.TryParse(lineParts[2], out int value);
                        if (value <= 20)
                        {
                            string ip = lineParts[3].Trim();
                            if (string.IsNullOrWhiteSpace(ip))
                            {
                                continue;
                            }
                            bool pingSuccess = pingable(ip) == IPStatus.Success;
                            if (pingSuccess)
                                on++;
                            else
                                off++;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"\r{conectedTPVs}", on, off);

                            if (pingSuccess)
                            {
                                string tpvNum = lineParts[1];
                                if (int.Parse(tpvNum) >= 8001 & int.Parse(tpvNum) <= 8023) continue;
                                GetLastWriteTime(tpvNum, ip);
                                Console.WriteLine();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"Error al ejecutar el programa: {ex.Message}");
            }
        }
        static void GetLastWriteTime(string tpvNum, string ip)
        {

            var findVersion = versions.FirstOrDefault(v => v.Value.Contains(tpvNum.ToString()));

            string fileFind = _config[$"FILES:{findVersion.Key}"];
            string posPath = Path.Combine("\\\\", ip, _config["PATHS:POS"], findVersion.Key == "S" ? fileFind : _config["FILES:APPPOSNAME"]);
            string toNewPath = Path.Combine("\\\\", ip, _config["PATHS:NEWVER"], findVersion.Key == "S" ? fileFind : _config["FILES:APPPOSNAME"]);
            string fromCopyPath = Path.Combine(_config["PATHS:REPONERPOS"], fileFind);

            DateTime currentVersionDate = File.GetLastWriteTime(posPath);
            DateTime newVersionDate = File.GetLastWriteTime(fromCopyPath);

            AppNewVersionCopy(currentVersionDate, newVersionDate, posPath, toNewPath, fromCopyPath, int.Parse(tpvNum));
            OtherFilesCopy(Path.Combine("\\\\", ip, _config["PATHS:POS"]), int.Parse(tpvNum));

        }
        static void OtherFilesCopy(string posPath, int tpvNum)
        {
            string[] otherFiles = Directory.GetFiles(_config["PATHS:TOCOPY"]);
            foreach (var file in otherFiles)
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CHECK_VERSION_POS", logReportError), true))
                {

                    DateTime newFileDate = File.GetLastWriteTime(file);
                    string currentFile = Path.Combine(posPath, Path.GetFileName(file));
                    DateTime currentFileDate = File.GetLastWriteTime(currentFile);
                    if (newFileDate <= currentFileDate)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine();
                        Console.WriteLine($"El fitxer {Path.GetFileName(file)} a la caixa {tpvNum} està actualitzat.");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        if (copyNewVersions.Key != ConsoleKey.S)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine();
                            Console.WriteLine($"El fitxer {Path.GetFileName(file)} a la caixa {tpvNum} està desactualitzat.");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            continue;
                        }
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine();
                        Console.WriteLine($"El fitxer {Path.GetFileName(file)} a la caixa {tpvNum} està desactualitzat. Copiant nova versió...");
                        File.Copy(file, currentFile.Replace($"{_config["PATHS:POS"]}", $"{_config["PATHS:NEWVER"]}"), true);

                        sw.WriteLine($"Report dia {DateTime.Now}");
                        sw.WriteLine(new string('_', 20));
                        sw.WriteLine($"Versió actual de {Path.GetFileName(currentFile)} a la caixa {tpvNum}: {currentFileDate.ToString("dd/MM/yyyy HH:mm:ss")}");
                        sw.WriteLine($"Versió nova de {Path.GetFileName(currentFile)} a la caixa {tpvNum}: {newFileDate.ToString("dd/MM/yyyy HH:mm:ss")}");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Nova versió de {Path.GetFileName(file)} copiada correctament a la caixa {tpvNum}.");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                }
            }
        }
        static Dictionary<string, List<string>> GetVersionSection()
        {
            Dictionary<string, List<string>> versions = new Dictionary<string, List<string>>();
            var versionSection = _config.GetSection("VERSIONS").GetChildren();
            foreach (var item in versionSection)
            {
                if (!versions.ContainsKey(item.Value))
                {
                    versions[item.Value] = new List<string>();
                }
                versions[item.Value].Add(item.Key);
            }
            return versions;
        }
        static void Main(string[] args)
        {
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CHECK_VERSION_POS");
            Directory.CreateDirectory(logDirectory);
            Console.Title = "Comprovació de les Versions";

            StartMessage();

            copyNewVersions = Console.ReadKey(true);
            while (copyNewVersions.Key != ConsoleKey.S & copyNewVersions.Key != ConsoleKey.N)
            {
                copyNewVersions = Console.ReadKey(true);
            }
            Console.Clear();
            Console.WriteLine(copyNewVersions.Key == ConsoleKey.S ? "Copiant versions noves detactades" : "No es copiaran les versions");
            Console.WriteLine();

            Executing();
            string logFilePath = Path.Combine(logDirectory, logReportError);
            if (copyNewVersions.Key == ConsoleKey.S)
            {
                Process.Start(logFilePath);
            }
            else
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
            }
            //Process.Start(temppath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Programa finalitzat. Premi la tecla enter per tancar o tanqui el programa.");
            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadLine();


            File.Delete(tempFullPath);
        }
        public static void AppNewVersionCopy(DateTime appPosDate, DateTime newVerDate, string posPath, string toNewPath, string fromCopyPath, int tpvNum)
        {
            using (StreamWriter sw = new StreamWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CHECK_VERSION_POS", logReportError), true))
            {
                Console.WriteLine();
                if (newVerDate <= appPosDate)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"La versió de {Path.GetFileName(posPath)} a la caixa {tpvNum} està actualitzada.");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                else
                {
                    if (copyNewVersions.Key != ConsoleKey.S)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                        Console.WriteLine($"El fitxer {Path.GetFileName(posPath)} a la caixa {tpvNum} està desactualitzat.");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        return;
                    }

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"La versió de {Path.GetFileName(posPath)} a la caixa {tpvNum} està desactualitzada. Copiant nova versió...");
                    File.Copy(fromCopyPath, toNewPath, true);

                    sw.WriteLine($"Report dia {DateTime.Now}");
                    sw.WriteLine(new string('_', 20));
                    sw.WriteLine($"Versió actual de {Path.GetFileName(posPath)} a la caixa {tpvNum}: {appPosDate.ToString("dd/MM/yyyy HH:mm:ss")}");
                    sw.WriteLine($"Versió nova de {Path.GetFileName(posPath)} a la caixa {tpvNum}: {newVerDate.ToString("dd/MM/yyyy HH:mm:ss")}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Nova versió de {Path.GetFileName(posPath)} copiada correctament a la caixa {tpvNum}.");
                    Console.ForegroundColor = ConsoleColor.Yellow;


                }
            }
        }
    }
}