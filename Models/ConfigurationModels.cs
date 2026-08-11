using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace SOACS.OfflineUpdateBuilder.Models
{
    [XmlRoot("OfflineUpdateBuilderConfiguration")]
    public class AppConfiguration
    {
        [XmlAttribute]
        public string Version { get; set; }

        [XmlArray("Categories")]
        [XmlArrayItem("Category")]
        public List<CategoryDefinition> Categories { get; set; } = new List<CategoryDefinition>();

        [XmlArray("DeploymentProfiles")]
        [XmlArrayItem("Profile")]
        public List<DeploymentProfile> DeploymentProfiles { get; set; } = new List<DeploymentProfile>();
    }

    public class CategoryDefinition
    {
        [XmlAttribute]
        public string Id { get; set; }

        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlAttribute]
        public int Order { get; set; }

        [XmlAttribute]
        public bool Enabled { get; set; } = true;

        [XmlArray("SourceAliases")]
        [XmlArrayItem("Alias")]
        public List<SourceAlias> SourceAliases { get; set; } = new List<SourceAlias>();

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class SourceAlias
    {
        [XmlAttribute]
        public string Path { get; set; }
    }

    public class DeploymentProfile
    {
        [XmlAttribute]
        public string Id { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Description { get; set; }

        [XmlArray("Targets")]
        [XmlArrayItem("Target")]
        public List<TargetDefinition> Targets { get; set; } = new List<TargetDefinition>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class TargetDefinition : INotifyPropertyChanged
    {
        private string _destinationPath;
        private string _copyMode;

        [XmlAttribute]
        public string CategoryId { get; set; }

        [XmlAttribute]
        public string DestinationPath
        {
            get { return _destinationPath; }
            set { _destinationPath = value; OnPropertyChanged(); }
        }

        [XmlAttribute]
        public string CopyMode
        {
            get { return string.IsNullOrWhiteSpace(_copyMode) ? "Merge" : _copyMode; }
            set { _copyMode = value; OnPropertyChanged(); }
        }

        [XmlAttribute]
        public bool BackupExisting { get; set; } = true;

        [XmlAttribute]
        public string RequiresProcessesStopped { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
