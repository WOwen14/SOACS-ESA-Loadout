using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SOACS.OfflineUpdateBuilder.Models
{
    [XmlRoot("OfflineUpdatePackage")]
    public class PackageManifest
    {
        [XmlAttribute]
        public string PackageId { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Version { get; set; }

        [XmlAttribute]
        public string CreatedUtc { get; set; }

        [XmlAttribute]
        public string BuilderVersion { get; set; }

        [XmlAttribute]
        public string DeploymentProfileId { get; set; }

        [XmlAttribute]
        public string DeploymentProfileName { get; set; }

        [XmlArray("ContentItems")]
        [XmlArrayItem("Item")]
        public List<ManifestContentItem> ContentItems { get; set; } = new List<ManifestContentItem>();
    }

    public class ManifestContentItem
    {
        [XmlAttribute]
        public string CategoryId { get; set; }

        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlAttribute]
        public string SourceRelativePath { get; set; }

        [XmlAttribute]
        public string DataRelativePath { get; set; }

        [XmlAttribute]
        public string DestinationPath { get; set; }

        [XmlAttribute]
        public string CopyMode { get; set; }

        [XmlAttribute]
        public bool BackupExisting { get; set; }

        [XmlAttribute]
        public string RequiresProcessesStopped { get; set; }

        [XmlAttribute]
        public int FileCount { get; set; }

        [XmlAttribute]
        public long SizeBytes { get; set; }

        [XmlArray("Files")]
        [XmlArrayItem("File")]
        public List<ManifestFile> Files { get; set; } = new List<ManifestFile>();
    }

    public class ManifestFile
    {
        [XmlAttribute]
        public string RelativePath { get; set; }

        [XmlAttribute]
        public long SizeBytes { get; set; }

        [XmlAttribute]
        public string Sha256 { get; set; }
    }

    public class BuildProgress
    {
        public int Percent { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
    }

    public class BuildRequest
    {
        public string SourceRoot { get; set; }
        public string OutputRoot { get; set; }
        public string PackageName { get; set; }
        public string PackageVersion { get; set; }
        public bool CreateZip { get; set; }
        public DeploymentProfile Profile { get; set; }
        public List<DetectedContent> ContentItems { get; set; }
    }

    public class BuildResult
    {
        public string PackageFolder { get; set; }
        public string ZipPath { get; set; }
        public string PackageId { get; set; }
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
