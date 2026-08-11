using SOACS.OfflineUpdateBuilder.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace SOACS.OfflineUpdateBuilder.Services
{
    public class PackageBuilder
    {
        public BuildResult Build(BuildRequest request, IProgress<BuildProgress> progress, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            var stopwatch = Stopwatch.StartNew();
            string packageId = Guid.NewGuid().ToString("N").ToUpperInvariant();
            string safeName = SanitizeFileName(request.PackageName);
            string safeVersion = SanitizeFileName(request.PackageVersion);
            string buildStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderName = string.Format("{0}_v{1}_{2}", safeName, safeVersion, buildStamp);
            Directory.CreateDirectory(request.OutputRoot);

            string finalFolder = GetUniqueDirectory(Path.Combine(request.OutputRoot, folderName));
            string temporaryFolder = Path.Combine(request.OutputRoot, ".building_" + Guid.NewGuid().ToString("N"));
            string zipPath = finalFolder + ".zip";
            var selectedItems = request.ContentItems.Where(i => i.Include).ToList();
            int totalFiles = Math.Max(1, selectedItems.Sum(i => i.FileCount));
            int processedFiles = 0;
            int lastReportedPercent = -1;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, 1, "Preparing package", temporaryFolder);
                Directory.CreateDirectory(temporaryFolder);
                Directory.CreateDirectory(Path.Combine(temporaryFolder, "Data"));
                Directory.CreateDirectory(Path.Combine(temporaryFolder, "Manifest"));
                Directory.CreateDirectory(Path.Combine(temporaryFolder, "Config"));
                Directory.CreateDirectory(Path.Combine(temporaryFolder, "Docs"));

                var manifest = new PackageManifest
                {
                    PackageId = packageId,
                    Name = request.PackageName.Trim(),
                    Version = request.PackageVersion.Trim(),
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    BuilderVersion = "0.1.0",
                    DeploymentProfileId = request.Profile.Id,
                    DeploymentProfileName = request.Profile.Name
                };

                var usedPackagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var content in selectedItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = request.Profile.Targets.First(t =>
                        string.Equals(t.CategoryId, content.CategoryId, StringComparison.OrdinalIgnoreCase));

                    string itemDataRelativePath = Path.Combine("Data", SanitizePathSegment(content.CategoryId));
                    string itemDataRoot = Path.Combine(temporaryFolder, itemDataRelativePath);
                    Directory.CreateDirectory(itemDataRoot);

                    var manifestItem = new ManifestContentItem
                    {
                        CategoryId = content.CategoryId,
                        DisplayName = content.DisplayName,
                        SourceRelativePath = content.SourceRelativePath,
                        DataRelativePath = itemDataRelativePath,
                        DestinationPath = target.DestinationPath,
                        CopyMode = target.CopyMode,
                        BackupExisting = target.BackupExisting,
                        RequiresProcessesStopped = target.RequiresProcessesStopped ?? string.Empty,
                        FileCount = content.FileCount,
                        SizeBytes = content.SizeBytes
                    };

                    SearchOption searchOption = content.TopLevelFilesOnly
                        ? SearchOption.TopDirectoryOnly
                        : SearchOption.AllDirectories;

                    foreach (string sourceFile in Directory.GetFiles(content.SourcePath, "*", searchOption))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string relativePath = content.TopLevelFilesOnly
                            ? Path.GetFileName(sourceFile)
                            : GetRelativePath(content.SourcePath, sourceFile);
                        string packageRelativeFile = Path.Combine(itemDataRelativePath, relativePath);

                        if (!usedPackagePaths.Add(packageRelativeFile))
                        {
                            throw new InvalidDataException("Two source files map to the same package path: " + packageRelativeFile);
                        }

                        string destinationFile = Path.Combine(temporaryFolder, packageRelativeFile);
                        string destinationDirectory = Path.GetDirectoryName(destinationFile);
                        if (!string.IsNullOrWhiteSpace(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        File.Copy(sourceFile, destinationFile, false);
                        string sha256 = ComputeSha256(destinationFile);
                        var fileInfo = new FileInfo(destinationFile);
                        manifestItem.Files.Add(new ManifestFile
                        {
                            RelativePath = relativePath,
                            SizeBytes = fileInfo.Length,
                            Sha256 = sha256
                        });

                        processedFiles++;
                        int percent = 5 + (int)(processedFiles * 70L / totalFiles);
                        if (percent != lastReportedPercent || processedFiles == totalFiles)
                        {
                            Report(progress, percent, "Copying and hashing data", packageRelativeFile);
                            lastReportedPercent = percent;
                        }
                    }

                    manifest.ContentItems.Add(manifestItem);
                }

                cancellationToken.ThrowIfCancellationRequested();
                string manifestPath = Path.Combine(temporaryFolder, "Manifest", "PackageManifest.xml");
                SerializeXml(manifestPath, manifest);
                SerializeXml(Path.Combine(temporaryFolder, "Config", "DeploymentProfile.xml"), request.Profile);

                File.WriteAllText(
                    Path.Combine(temporaryFolder, "Deploy-OfflineUpdates.ps1"),
                    PowerShellGenerator.CreateDeploymentScript(),
                    new UTF8Encoding(true));
                File.WriteAllText(
                    Path.Combine(temporaryFolder, "Verify-Package.ps1"),
                    PowerShellGenerator.CreateVerificationScript(),
                    new UTF8Encoding(true));
                File.WriteAllText(
                    Path.Combine(temporaryFolder, "Rollback-OfflineUpdates.ps1"),
                    PowerShellGenerator.CreateRollbackScript(),
                    new UTF8Encoding(true));
                File.WriteAllText(
                    Path.Combine(temporaryFolder, "Docs", "DEPLOYMENT_README.txt"),
                    CreateDeploymentReadme(request, packageId),
                    new UTF8Encoding(false));

                Report(progress, 80, "Writing package manifest and scripts", manifestPath);
                ValidateManifest(manifestPath, manifest.ContentItems.Sum(i => i.Files.Count));
                WritePackageHashList(temporaryFolder, manifest);

                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(temporaryFolder, finalFolder);
                Report(progress, 90, "Deployment folder completed", finalFolder);

                if (request.CreateZip)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Report(progress, 92, "Creating ZIP package", zipPath);
                    ZipFile.CreateFromDirectory(finalFolder, zipPath, CompressionLevel.Optimal, false);
                }
                else
                {
                    zipPath = null;
                }

                stopwatch.Stop();
                Report(progress, 100, "Package build complete", finalFolder);
                return new BuildResult
                {
                    PackageFolder = finalFolder,
                    ZipPath = zipPath,
                    PackageId = packageId,
                    FileCount = processedFiles,
                    SizeBytes = selectedItems.Sum(i => i.SizeBytes),
                    Duration = stopwatch.Elapsed
                };
            }
            catch
            {
                if (Directory.Exists(temporaryFolder))
                {
                    try { Directory.Delete(temporaryFolder, true); }
                    catch { }
                }
                throw;
            }
        }

        private static void ValidateRequest(BuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.SourceRoot) || !Directory.Exists(request.SourceRoot))
                throw new DirectoryNotFoundException("The selected source folder does not exist.");
            if (string.IsNullOrWhiteSpace(request.OutputRoot))
                throw new InvalidDataException("Select an output folder.");
            if (string.IsNullOrWhiteSpace(request.PackageName))
                throw new InvalidDataException("Enter a package name.");
            if (string.IsNullOrWhiteSpace(request.PackageVersion))
                throw new InvalidDataException("Enter a package version.");
            if (request.Profile == null)
                throw new InvalidDataException("Select a deployment profile.");
            if (request.ContentItems == null || !request.ContentItems.Any(i => i.Include))
                throw new InvalidDataException("Select at least one source-content item.");

            string source = NormalizeDirectory(request.SourceRoot);
            string output = NormalizeDirectory(request.OutputRoot);
            if (output.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                output.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The output folder cannot be inside the source-data folder.");
            }

            foreach (var item in request.ContentItems.Where(i => i.Include))
            {
                if (string.Equals(item.CategoryId, "Unassigned", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Assign every included item to a known category before building.");

                var target = request.Profile.Targets.FirstOrDefault(t =>
                    string.Equals(t.CategoryId, item.CategoryId, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    throw new InvalidDataException("The selected profile has no target for category: " + item.CategoryId);
                if (string.IsNullOrWhiteSpace(target.DestinationPath) ||
                    target.DestinationPath.StartsWith("REQUIRED:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Set a destination path for category: " + item.DisplayName);
                if (!string.Equals(target.CopyMode, "Merge", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Version 0.1 supports Merge copy mode only. Correct: " + item.DisplayName);
            }
        }

        private static void SerializeXml<T>(string path, T value)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false),
                NewLineChars = Environment.NewLine
            };
            var serializer = new XmlSerializer(typeof(T));
            using (var writer = XmlWriter.Create(path, settings))
            {
                serializer.Serialize(writer, value);
            }
        }

        private static void ValidateManifest(string path, int expectedFileCount)
        {
            var serializer = new XmlSerializer(typeof(PackageManifest));
            using (var stream = File.OpenRead(path))
            {
                var manifest = serializer.Deserialize(stream) as PackageManifest;
                if (manifest == null || manifest.ContentItems.Sum(i => i.Files.Count) != expectedFileCount)
                {
                    throw new InvalidDataException("The generated package manifest failed validation.");
                }
            }
        }

        private static void WritePackageHashList(string packageRoot, PackageManifest manifest)
        {
            var lines = new List<string>();
            var dataPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in manifest.ContentItems)
            {
                foreach (var file in item.Files)
                {
                    string relative = Path.Combine(item.DataRelativePath, file.RelativePath).Replace('\\', '/');
                    dataPaths.Add(relative);
                    lines.Add(file.Sha256 + "  " + relative);
                }
            }

            foreach (string path in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(packageRoot, path).Replace('\\', '/');
                if (relative.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) || dataPaths.Contains(relative))
                    continue;
                lines.Add(ComputeSha256(path) + "  " + relative);
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            File.WriteAllLines(Path.Combine(packageRoot, "Manifest", "SHA256SUMS.txt"), lines, new UTF8Encoding(false));
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string CreateDeploymentReadme(BuildRequest request, string packageId)
        {
            var builder = new StringBuilder();
            builder.AppendLine("SOACS OFFLINE UPDATE DEPLOYMENT PACKAGE");
            builder.AppendLine("=======================================");
            builder.AppendLine("Package: " + request.PackageName);
            builder.AppendLine("Version: " + request.PackageVersion);
            builder.AppendLine("Package ID: " + packageId);
            builder.AppendLine("Profile: " + request.Profile.Name);
            builder.AppendLine();
            builder.AppendLine("1. Extract the entire ZIP before deployment.");
            builder.AppendLine("2. Run Verify-Package.ps1 to validate every data file.");
            builder.AppendLine("3. Close the applications named in the deployment summary.");
            builder.AppendLine("4. Run Deploy-OfflineUpdates.ps1. The script self-elevates.");
            builder.AppendLine("5. Review the summary and type DEPLOY when prompted.");
            builder.AppendLine();
            builder.AppendLine("Preview only:");
            builder.AppendLine("  powershell.exe -ExecutionPolicy Bypass -File .\Deploy-OfflineUpdates.ps1 -PreviewOnly");
            builder.AppendLine();
            builder.AppendLine("Rollback:");
            builder.AppendLine("  powershell.exe -ExecutionPolicy Bypass -File .\Rollback-OfflineUpdates.ps1");
            builder.AppendLine();
            builder.AppendLine("Deployment logs, receipts, and overwritten-file backups are stored under:");
            builder.AppendLine("  %ProgramData%\SOACS\OfflineUpdateBuilder");
            builder.AppendLine();
            builder.AppendLine("Copy mode: Merge. Existing files are backed up before overwrite; unrelated target files are retained.");
            return builder.ToString();
        }

        private static string GetRelativePath(string root, string path)
        {
            var rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string SanitizeFileName(string value)
        {
            string result = value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(result) ? "OfflineUpdate" : result;
        }

        private static string SanitizePathSegment(string value)
        {
            return SanitizeFileName(value).Replace(' ', '_');
        }

        private static string GetUniqueDirectory(string preferredPath)
        {
            if (!Directory.Exists(preferredPath) && !File.Exists(preferredPath))
                return preferredPath;

            for (int index = 2; index < 1000; index++)
            {
                string candidate = preferredPath + "_" + index;
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                    return candidate;
            }
            throw new IOException("Could not create a unique output package name.");
        }

        private static void Report(IProgress<BuildProgress> progress, int percent, string message, string detail)
        {
            progress?.Report(new BuildProgress
            {
                Percent = Math.Max(0, Math.Min(100, percent)),
                Message = message,
                Detail = detail
            });
        }
    }
}
