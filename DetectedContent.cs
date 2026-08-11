using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SOACS.OfflineUpdateBuilder.Models
{
    public class DetectedContent : INotifyPropertyChanged
    {
        private bool _include;
        private string _categoryId;
        private string _displayName;
        private string _status;

        public bool Include
        {
            get { return _include; }
            set { _include = value; OnPropertyChanged(); }
        }

        public string CategoryId
        {
            get { return _categoryId; }
            set { _categoryId = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string SourcePath { get; set; }
        public string SourceRelativePath { get; set; }
        public bool TopLevelFilesOnly { get; set; }
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }

        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(); }
        }

        public string SizeDisplay
        {
            get
            {
                double value = SizeBytes;
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int unit = 0;
                while (value >= 1024 && unit < units.Length - 1)
                {
                    value /= 1024;
                    unit++;
                }

                return string.Format("{0:0.##} {1}", value, units[unit]);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
