using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using LogViewerAppWPF;

using Microsoft.Win32;

namespace LogViewerApp {

    public partial class MainWindow : Window {
        private List<LogEntry> logs = new List<LogEntry>();
        private List<LogEntry> filteredLogs = new List<LogEntry>();
        private List<LogEntry> paginatedLogs = new();
        private bool isExpanded = true;
        private int logCount;
        private int warningCount;
        private int errorCount;
        private int currentPage = 1;
        private int totalPages;

        public event PropertyChangedEventHandler PropertyChanged;

        //public string PageInfo;//=> $"Page {currentPage} of {totalPages}";
        private int PageSize = 10; // Number of items per page

        public MainWindow() {
            InitializeComponent();
        }

        private void LoadFileButton_Click(object sender, RoutedEventArgs e) {
            PageSizeComboBox.SelectedIndex = 0;

            OpenFileDialog fileDialog = new() {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };
            if (fileDialog.ShowDialog() != true)
                return;

            logs = ParseLogs(File.ReadAllText(fileDialog.FileName));

            logs.ForEach(log => {
                if (log.LogLevel != "Exception") {
                    log.StackTraceFull = Utilites.GetTruncatedStackTrace(log.StackTraceFull);
                    log.StackTrace = log.StackTrace1stLine = log.StackTraceFull.Split('\n')[0];
                }
                (log.ClassTraceFull, log.ClassTrace1st) = Utilites.ExtractClassNamesAndLineNumbers(log.StackTraceFull);

                log.ClassTrace = log.ClassTrace1st;
                //DateTime myDate = DateTime.Parse(log.Timestamp, System.Globalization.CultureInfo.InvariantCulture);
                //log.Timestamp = myDate.ToString("dd/MM/yyyy HH:mm:ss");
            });

            if (PageSizeComboBox.Items.Count <= 0) {
                for (int i = 0; i < pagesizeArray.Length; i++) {
                    PageSizeComboBox.Items.Insert(i, pagesizeArray[i]);
                }
            }

            UpdateDisplay();
        }

        private List<LogEntry> ParseLogs(string logText) {
            if ((bool)Encrypted.IsChecked) {
                logText = EncryptDecrypt.Decrypt(logText);
            }

            Regex logEntryPattern = new(
                @"(?<TimeStamp>\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}) - (?<LogLevel>\w+)\r?\n(?<Message>.*?)\r?\n(?<StackTrace>(?:.|\r?\n)+?)(?=\r?\n\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2} - \w+|$)",
                RegexOptions.Singleline);

            return logEntryPattern.Matches(logText).Cast<Match>().Select(match => new LogEntry {
                Timestamp = match.Groups["TimeStamp"].Value,
                LogLevel = match.Groups["LogLevel"].Value,
                MassageFull = match.Groups["Message"].Value.Trim(),
                StackTraceFull = match.Groups["StackTrace"].Value.Trim(),
                Message = match.Groups["Message"].Value.Split('\n')[0],
                Massage1stLine = match.Groups["Message"].Value.Split('\n')[0],
                StackTrace = match.Groups["StackTrace"].Value.Split('\n')[0]
            }).ToList();
        }

        private void UpdateDisplay() {
            filteredLogs = logs.Where(FilterLog).ToList();
            UpdatePagination();

            if (CollapseFilter.IsChecked == true) {
                paginatedLogs = paginatedLogs
                    .GroupBy(log => new { log.Message, log.LogLevel })
                    .Select(group => group.First())
                    .ToList();
            }

            logCount = 0;
            warningCount = 0;
            errorCount = 0;
            foreach (var item in paginatedLogs) {
                if (item.LogLevel == "Log") {
                    logCount += 1;
                } else if (item.LogLevel == "Warning") {
                    warningCount += 1;
                } else {
                    errorCount += 1;
                }
            }

            LogFilter.Content = $"Log ({logCount})";
            WarningFilter.Content = $"Warning ({warningCount})";
            ErrorFilter.Content = $"Error ({errorCount})";
        }

        private bool FilterLog(LogEntry log) {
            // Combine search, log level, and date range filters
            var searchTerm = SearchBox.Text.ToLower();
            if (!string.IsNullOrEmpty(searchTerm) &&
                !log.Message.Contains(searchTerm) &&
                !log.StackTrace.Contains(searchTerm)) {
                return false;
            }

            return FilterByLogLevel(log) && FilterByDate(log);
        }

        private bool FilterByLogLevel(LogEntry log) {
            return (LogFilter.IsChecked == true || log.LogLevel != "Log") &&
               (WarningFilter.IsChecked == true || log.LogLevel != "Warning") &&
               (ErrorFilter.IsChecked == true || log.LogLevel != "Error");
        }

        private bool FilterByDate(LogEntry log) {
            DateTime logDate = DateTime.Parse(log.Timestamp);

            // Check Start Date
            if (StartDatePicker.SelectedDate.HasValue && logDate < StartDatePicker.SelectedDate.Value)
                return false;

            // Check End Date
            if (EndDatePicker.SelectedDate.HasValue && logDate > EndDatePicker.SelectedDate.Value)
                return false;

            return true;
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e) {
            isExpanded = !isExpanded;
            paginatedLogs.ForEach(log => log.IsExpanded = isExpanded);
            foreach (var item in paginatedLogs) {
                logListView.SelectedItem = item;
                LogListView_MouseDoubleClick(default, default);
            }

            if (!isExpanded) {
                ExpandAllButton.Content = "Colleps All";
            } else {
                ExpandAllButton.Content = "Expand All";
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => UpdateDisplay();

        private void FilterButton_Click(object sender, RoutedEventArgs e) => UpdateDisplay();

        private void LogListView_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (logListView.SelectedItem is not LogEntry log)
                return;

            log.IsExpanded = !log.IsExpanded;
            log.StackTrace = log.IsExpanded ? log.StackTraceFull : log.StackTrace1stLine;
            log.Message = log.IsExpanded ? log.MassageFull : log.Massage1stLine;
            log.ClassTrace = log.IsExpanded ? log.ClassTraceFull : log.ClassTrace1st;

            logListView.Items.Refresh();
        }

        private void UpdatePagination() {
            totalPages = (int)Math.Ceiling((double)filteredLogs.Count / PageSize);

            // Get the items for the current page
            paginatedLogs = filteredLogs.Skip((currentPage - 1) * PageSize).Take(PageSize).ToList();

            // Bind the current page's items to the ListView
            logListView.ItemsSource = paginatedLogs;

            PageInfo.Text = $"Page {currentPage} of {totalPages}";
            // Notify the UI of property changes
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e) {
            if (currentPage > 1) {
                currentPage--;
                UpdatePagination();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e) {
            if (currentPage < totalPages) {
                currentPage++;
                UpdatePagination();
            }
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            PageSize = pagesizeArray[PageSizeComboBox.SelectedIndex];
            UpdateDisplay();
        }

        //private void ThemeToggleButton_Checked(object sender, RoutedEventArgs e) {
        //    SetNightMode();
        //    ThemeToggleButton.Content = "☀️ Day Mode";
        //}

        //private void ThemeToggleButton_Unchecked(object sender, RoutedEventArgs e) {
        //    SetDayMode();
        //    ThemeToggleButton.Content = "🌙 Night Mode";
        //}

        private void SetNightMode() {
            this.Background = Brushes.Black;
            this.Foreground = Brushes.White;
        }

        private void SetDayMode() {
            this.Background = Brushes.White;
            this.Foreground = Brushes.Black;
        }

        private int[] pagesizeArray = new int[] {
            10,25,50,100
        };
    }

    public class LogEntry {

        public string Timestamp {
            get; set;
        }

        public string LogLevel {
            get; set;
        }

        public string Message {
            get; set;
        }

        public string Massage1stLine {
            get; set;
        }

        public string MassageFull {
            get; set;
        }

        public string StackTrace {
            get; set;
        }

        public string StackTrace1stLine {
            get; set;
        }

        public string StackTraceFull {
            get; set;
        }

        public string ClassTrace {
            get; set;
        }

        public string ClassTrace1st {
            get; set;
        }

        public string ClassTraceFull {
            get; set;
        }

        public bool IsExpanded {
            get; set;
        }

        public string MethodTrace {
            get; set;
        }
    }
}