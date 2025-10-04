using System;
using System.Diagnostics;
using System.Windows;

namespace SrtExtractor.Views
{
    /// <summary>
    /// Interaction logic for UserGuideWindow.xaml
    /// </summary>
    public partial class UserGuideWindow : Window
    {
        public UserGuideWindow()
        {
            InitializeComponent();
        }

        #region Navigation Methods

        private void NavigateToGettingStarted(object sender, RoutedEventArgs e)
        {
            ShowSection("GettingStartedSection");
        }

        private void NavigateToSingleFile(object sender, RoutedEventArgs e)
        {
            ShowSection("SingleFileSection");
        }

        private void NavigateToBatchMode(object sender, RoutedEventArgs e)
        {
            ShowSection("BatchModeSection");
        }

        private void NavigateToSettings(object sender, RoutedEventArgs e)
        {
            ShowSection("SettingsSection");
        }

        private void NavigateToSrtCorrection(object sender, RoutedEventArgs e)
        {
            ShowSection("SrtCorrectionSection");
        }

        private void NavigateToShortcuts(object sender, RoutedEventArgs e)
        {
            ShowSection("ShortcutsSection");
        }

        private void NavigateToTroubleshooting(object sender, RoutedEventArgs e)
        {
            ShowSection("TroubleshootingSection");
        }

        private void NavigateToFormats(object sender, RoutedEventArgs e)
        {
            ShowSection("FormatsSection");
        }

        private void ShowSection(string sectionName)
        {
            // Hide all sections
            GettingStartedSection.Visibility = Visibility.Collapsed;
            SingleFileSection.Visibility = Visibility.Collapsed;
            BatchModeSection.Visibility = Visibility.Collapsed;
            SettingsSection.Visibility = Visibility.Collapsed;
            SrtCorrectionSection.Visibility = Visibility.Collapsed;
            ShortcutsSection.Visibility = Visibility.Collapsed;
            TroubleshootingSection.Visibility = Visibility.Collapsed;
            FormatsSection.Visibility = Visibility.Collapsed;

            // Show the selected section
            switch (sectionName)
            {
                case "GettingStartedSection":
                    GettingStartedSection.Visibility = Visibility.Visible;
                    break;
                case "SingleFileSection":
                    SingleFileSection.Visibility = Visibility.Visible;
                    break;
                case "BatchModeSection":
                    BatchModeSection.Visibility = Visibility.Visible;
                    break;
                case "SettingsSection":
                    SettingsSection.Visibility = Visibility.Visible;
                    break;
                case "SrtCorrectionSection":
                    SrtCorrectionSection.Visibility = Visibility.Visible;
                    break;
                case "ShortcutsSection":
                    ShortcutsSection.Visibility = Visibility.Visible;
                    break;
                case "TroubleshootingSection":
                    TroubleshootingSection.Visibility = Visibility.Visible;
                    break;
                case "FormatsSection":
                    FormatsSection.Visibility = Visibility.Visible;
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void OpenOnlineDocs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the project repository in the default web browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ZentrixLabs/SrtExtractor",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening online documentation:\n{ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
