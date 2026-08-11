using SOACS.OfflineUpdateBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOACS.OfflineUpdateBuilder.Services
{
    public class SourceScanner
    {
        public List<DetectedContent> Scan(string sourceRoot, IEnumerable<CategoryDefinition> categories)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException("Select a valid source folder before scanning.");
            }

            string normalizedRoot = NormalizeFullPath(sourceRoot);
            var results = new List<DetectedContent>();
            var matchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in categories.Where(c => c.Enabled).OrderBy(c => c.Order))
            {
                foreach (var alias in category.SourceAliases.Where(a => !string.IsNullOrWhiteSpace(a.Path)))
                {
                    string candidate = NormalizeFullPath(Path.Combine(normalizedRoot, NormalizeRelativePath(alias.Path)));
                    if (!Directory.Exists(candidate) || matchedPaths.Contains(candidate))
                    {
                        continue;
                    }

                    results.Add(CreateDetectedItem(normalizedRoot, candidate, category.Id, category.DisplayName, true, "Ready"));
                    matchedPaths.Add(candidate);
                    break;
                }
            }

            foreach (string directory in Directory.GetDirectories(normalizedRoot))
            {
                string normalizedDirectory = NormalizeFullPath(directory);
                bool containsKnownContent = matchedPaths.Any(path =>
                    path.Equals(normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                if (containsKnownContent)
                {
                    continue;
                }

                results.Add(CreateDetectedItem(
                    normalizedRoot,
                    normalizedDirectory,
                    "Unassigned",
                    "Unassigned",
                    false,
                    "Needs category"));
            }

            string[] rootFiles = Directory.GetFiles(normalizedRoot);
            if (rootFiles.Length > 0)
            {
                long bytes = rootFiles.Sum(path => new FileInfo(path).Length);
                results.Add(new DetectedContent
                {
                    Include = false,
                    CategoryId = "Unassigned",
                    DisplayName = "Root-level files",
                    SourcePath = normalizedRoot,
                    SourceRelativePath = ".",
                    TopLevelFilesOnly = true,
                    FileCount = rootFiles.Length,
                    SizeBytes = bytes,
                    Status = "Needs category"
                });
            }

            return results;
        }

        private static DetectedContent CreateDetectedItem(
            string sourceRoot,
            string sourcePath,
            string categoryId,
            string displayName,
            bool include,
            string status)
        {
            var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            long size = files.Sum(path => new FileInfo(path).Length);

            return new DetectedContent
            {
                Include = include && files.Length > 0,
                CategoryId = categoryId,
                DisplayName = displayName,
                SourcePath = sourcePath,
                SourceRelativePath = GetRelativePath(sourceRoot, sourcePath),
                FileCount = files.Length,
                SizeBytes = size,
                Status = files.Length == 0 ? "Empty" : status
            };
        }

        private static string GetRelativePath(string root, string path)
        {
            var rootUri = new Uri(AppendDirectorySeparator(root));
            var pathUri = new Uri(AppendDirectorySeparator(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        }

        private static string NormalizeFullPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
