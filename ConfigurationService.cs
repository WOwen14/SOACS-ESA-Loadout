using SOACS.OfflineUpdateBuilder.Models;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SOACS.OfflineUpdateBuilder.Services
{
    public class ConfigurationService
    {
        private readonly string _configurationPath;

        public ConfigurationService(string configurationPath)
        {
            _configurationPath = configurationPath;
        }

        public string ConfigurationPath => _configurationPath;

        public AppConfiguration Load()
        {
            if (!File.Exists(_configurationPath))
            {
                throw new FileNotFoundException("Deployment profile configuration was not found.", _configurationPath);
            }

            var serializer = new XmlSerializer(typeof(AppConfiguration));
            using (var stream = File.OpenRead(_configurationPath))
            {
                var configuration = serializer.Deserialize(stream) as AppConfiguration;
                if (configuration == null)
                {
                    throw new InvalidDataException("DeploymentProfiles.xml did not contain a valid configuration.");
                }

                return configuration;
            }
        }

        public void Save(AppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var directory = Path.GetDirectoryName(_configurationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_configurationPath))
            {
                File.Copy(_configurationPath, _configurationPath + ".bak", true);
            }

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false),
                NewLineChars = Environment.NewLine
            };

            var serializer = new XmlSerializer(typeof(AppConfiguration));
            using (var writer = XmlWriter.Create(_configurationPath, settings))
            {
                serializer.Serialize(writer, configuration);
            }
        }
    }
}
