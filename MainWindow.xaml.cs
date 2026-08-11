using SOACS.OfflineUpdateBuilder.Models;
using SOACS.OfflineUpdateBuilder.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace SOACS.OfflineUpdateBuilder
{
    public partial class MainWindow : Window
    {
        private readonly SourceScanner _sourceScanner = new SourceScanner();
        private readonly PackageBuilder _packageBuilder = new PackageBuilder();
        private ConfigurationService _configurationService;
        private AppConfiguration _configuration;
        private CancellationTokenSource _buildCancellation;
        private bool _isBusy;

        public ObservableCollection<DetectedContent> DetectedItems { get; } = new ObservableCollection<DetectedContent>();
        public ObservableCollection<TargetDefinition> ActiveTargets { get; } = new ObservableCollection<TargetDefinition>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "DeploymentProfiles.xml");
                _configurationService = new ConfigurationService(configPath);
                _configuration = _configurationService.Load();

                CategoryComboBox.ItemsSource = _configuration.Categories.Where(c => c.Enabled).OrderBy(c => c.Order).ToList();
                CategoryComboBox.SelectedIndex = 0;
                ProfileComboBox.ItemsSource = _configuration.DeploymentProfiles;
                ProfileComboBox.SelectedIndex = _configuration.DeploymentProfiles.Count > 1 ? 1 : 0;

                OutputFolderTextBox.Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "SOACS Offline Update Packages");
                AppendLog("Configuration loaded: " + configPath);
            }
            catch (Exception ex)
            {
                SetStatus("CONFIGURATION ERROR", true);
                MessageBox.Show(
                    "The application could not load DeploymentProfiles.xml.\n\n" + ex.Message,
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isBusy)
                return;

            var answer = MessageBox.Show(
                "A package build is still running. Cancel the build and close?",
                "Build In Progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _buildCancellation?.Cancel();
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string selected = BrowseForFolder(SourceFolderTextBox.Text, "Select the root folder containing offline update data");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SourceFolderTextBox.Text = selected;
            }
        }

        private async void ScanSource_Click(object sender, RoutedEventArgs e)
        {
            if (_configuration == null)
                return;

            string root = SourceFolderTextBox.Text.Trim();
            if (!Directory.Exists(root))
            {
                MessageBox.Show("Select a valid source folder.", "Source Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetBusy(true, "SCANNING");
                BuildProgressBar.IsIndeterminate = true;
                ProgressText.Text = "Scanning source folder...";
                FooterStatusText.Text = "Reading folder structure and calculating content sizes.";
                AppendLog("Scanning: " + root);

                var results = await Task.Run(() => _sourceScanner.Scan(root, _configuration.Categories));
                DetectedItems.Clear();
                foreach (var item in results)
                    DetectedItems.Add(item);

                int ready = results.Count(i => i.Include && i.FileCount > 0);
                int fileCount = results.Sum(i => i.FileCount);
                long bytes = results.Sum(i => i.SizeBytes);
                ScanSummaryText.Text = string.Format("{0} categories • {1:N0} files • {2}", ready, fileCount, FormatBytes(bytes));
                FooterStatusText.Text = ready > 0
                    ? "Review detected content and deployment destinations."
                    : "No recognized data folders were found. Assign unrecognized folders manually.";
                AppendLog(string.Format("Scan complete: {0} items, {1:N0} files, {2}", results.Count, fileCount, FormatBytes(bytes)));
            }
            catch (Exception ex)
            {
                AppendLog("SCAN ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Scan Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BuildProgressBar.IsIndeterminate = false;
                BuildProgressBar.Value = 0;
                ProgressText.Text = "Waiting to build";
                SetBusy(false, "READY");
            }
        }

        private void ApplyCategory_Click(object sender, RoutedEventArgs e)
        {
            var item = ContentGrid.SelectedItem as DetectedContent;
            var category = CategoryComboBox.SelectedItem as CategoryDefinition;
            if (item == null || category == null)
            {
                MessageBox.Show("Select a source item and a category.", "Assign Category", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            item.CategoryId = category.Id;
            item.DisplayName = category.DisplayName;
            item.Include = item.FileCount > 0;
            item.Status = item.FileCount > 0 ? "Ready" : "Empty";
            AppendLog(string.Format("Assigned '{0}' to {1}.", item.SourcePath, category.DisplayName));
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActiveTargets.Clear();
            var profile = ProfileComboBox.SelectedItem as DeploymentProfile;
            if (profile == null)
                return;

            foreach (var target in profile.Targets)
                ActiveTargets.Add(target);

            FooterStatusText.Text = profile.Description;
            AppendLog("Selected deployment profile: " + profile.Name);
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_configurationService == null || _configuration == null)
                return;

            try
            {
                TargetsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                TargetsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                _configurationService.Save(_configuration);
                AppendLog("Deployment profile paths saved. Backup: DeploymentProfiles.xml.bak");
                MessageBox.Show("Deployment profile saved.", "Profile Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string selected = BrowseForFolder(OutputFolderTextBox.Text, "Select the package output folder");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                OutputFolderTextBox.Text = selected;
            }
        }

        private async void BuildPackage_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            TargetsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TargetsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var request = new BuildRequest
            {
                SourceRoot = SourceFolderTextBox.Text.Trim(),
                OutputRoot = OutputFolderTextBox.Text.Trim(),
                PackageName = PackageNameTextBox.Text.Trim(),
                PackageVersion = PackageVersionTextBox.Text.Trim(),
                CreateZip = CreateZipCheckBox.IsChecked == true,
                Profile = ProfileComboBox.SelectedItem as DeploymentProfile,
                ContentItems = DetectedItems.ToList()
            };

            _buildCancellation = new CancellationTokenSource();
            var progress = new Progress<BuildProgress>(update =>
            {
                BuildProgressBar.Value = update.Percent;
                ProgressText.Text = string.Format("{0}% • {1}", update.Percent, update.Message);
                FooterStatusText.Text = update.Detail;
                if (!string.IsNullOrWhiteSpace(update.Detail))
                    AppendLog(update.Message + ": " + update.Detail);
            });

            try
            {
                SetBusy(true, "BUILDING PACKAGE");
                ActivityLogTextBox.Clear();
                AppendLog("Starting deployment package build.");
                BuildProgressBar.Value = 0;

                BuildResult result = await Task.Run(() =>
                    _packageBuilder.Build(request, progress, _buildCancellation.Token));

                AppendLog(string.Format(
                    "BUILD COMPLETE: {0:N0} files, {1}, {2:mm\\:ss}",
                    result.FileCount,
                    FormatBytes(result.SizeBytes),
                    result.Duration));
                FooterStatusText.Text = "Package ready: " + (result.ZipPath ?? result.PackageFolder);
                SetStatus("PACKAGE READY", false);

                var answer = MessageBox.Show(
                    "Deployment package created successfully.\n\n" +
                    (result.ZipPath ?? result.PackageFolder) +
                    "\n\nOpen the output folder?",
                    "Build Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                {
                    Process.Start("explorer.exe", "/select,\"" + (result.ZipPath ?? result.PackageFolder) + "\"");
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("Build cancelled. Partial staging data was removed.");
                FooterStatusText.Text = "Package build cancelled.";
                SetStatus("CANCELLED", true);
            }
            catch (Exception ex)
            {
                AppendLog("BUILD ERROR: " + ex.Message);
                FooterStatusText.Text = ex.Message;
                SetStatus("BUILD FAILED", true);
                MessageBox.Show(ex.Message, "Package Build Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _buildCancellation.Dispose();
                _buildCancellation = null;
                SetBusy(false, HeaderStatusText.Text);
            }
        }

        private void CancelBuild_Click(object sender, RoutedEventArgs e)
        {
            _buildCancellation?.Cancel();
            CancelButton.IsEnabled = false;
            FooterStatusText.Text = "Cancelling after the current file...";
            AppendLog("Cancellation requested.");
        }

        private void SetBusy(bool busy, string status)
        {
            _isBusy = busy;
            BuildButton.IsEnabled = !busy;
            CancelButton.IsEnabled = busy;
            SourceFolderTextBox.IsEnabled = !busy;
            ProfileComboBox.IsEnabled = !busy;
            SetStatus(status, false);
        }

        private void SetStatus(string status, bool error)
        {
            HeaderStatusText.Text = status;
            HeaderStatusText.Foreground = error
                ? (System.Windows.Media.Brush)FindResource("DangerBrush")
                : (System.Windows.Media.Brush)FindResource("AccentBrush");
        }

        private void AppendLog(string message)
        {
            string line = string.Format("{0:HH:mm:ss}  {1}", DateTime.Now, message);
            ActivityLogTextBox.AppendText(line + Environment.NewLine);
            ActivityLogTextBox.ScrollToEnd();
        }

        private static string BrowseForFolder(string currentPath, string description)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
                    dialog.SelectedPath = currentPath;
                return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;
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
}
