using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Windows Forms for FolderBrowserDialog
using Microsoft.Win32.TaskScheduler; // Added Task Scheduler Library using
using MessageBox = System.Windows.MessageBox; // Explicitly using WPF MessageBox
using System.Net; // For NetworkCredential (potentially useful later)
using System.Security; // For SecureString (potentially useful later)

namespace ScriptSchedulerApp
{
    public partial class MainWindow : Window
    {
        // Script state variables
        private string _selectedScriptPath = string.Empty; // Renamed for clarity
        private string _selectedScriptType = string.Empty; // Renamed for clarity
        private string _networkSharePath = @"\\192.168.1.238\System Administration Folder\Scheduled Scripts"; // TODO: Make configurable
        private string _wrapperDir = @"\\192.168.1.238\System Administration Folder\Scheduled Scripts\VBS Wrapper Scripts"; // Updated to VBS wrapper scripts
        private string _logDir = @"\\192.168.1.238\System Administration Folder\Scheduled Scripts\TaskSchedulerLogs"; // TODO: Make configurable

        // Task actions collection
        private List<TaskAction> _taskActions = new List<TaskAction>();

        // Observable collections for UI
        private ObservableCollection<ScriptItem> _scripts;

        // Flag to prevent updates during initialization
        private bool _isWindowInitialized = false;

        // Helper class to represent different action types
        internal abstract class TaskAction
        {
            public abstract string ActionType { get; }
            public abstract string GetSummary();
        }

        private class RunProgramAction : TaskAction
        {
            public override string ActionType => "Run Program";
            public string ProgramPath { get; set; }
            public string Arguments { get; set; }
            public string WorkingDirectory { get; set; }
            public bool CreateWrapper { get; set; }
            public string WrapperLocation { get; set; }

            public override string GetSummary()
            {
                return $"Run Program: {ProgramPath}\nArguments: {Arguments}\nWorking Dir: {WorkingDirectory}";
            }
        }

        private class SendEmailAction : TaskAction
        {
            public override string ActionType => "Send Email";
            public string From { get; set; }
            public string To { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public string SmtpServer { get; set; }
            public int SmtpPort { get; set; }
            public bool UseSSL { get; set; }

            public override string GetSummary()
            {
                return $"Send Email\nFrom: {From}\nTo: {To}\nSubject: {Subject}\nSMTP: {SmtpServer}:{SmtpPort}";
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Initialize scripts collection
            _scripts = new ObservableCollection<ScriptItem>();
            ScriptsListView.ItemsSource = _scripts;

            // Initialize time pickers
            PopulateTimePickers();

            // Initialize comboboxes (other than time)
            PopulateComboBoxes();

            // Set default date in date pickers
            OneTimeStartDatePicker.SelectedDate = DateTime.Today;
            DailyStartDatePicker.SelectedDate = DateTime.Today;
            WeeklyStartDatePicker.SelectedDate = DateTime.Today;

            // Set default working directory and wrapper location
            WorkingDirTextBox.Text = _networkSharePath; // Default to script share
            WrapperLocationTextBox.Text = _wrapperDir;

            // Setup initial UI state
            DisableTabsExcept(ScriptSelectionTab);
            NextButton.IsEnabled = false;
            CreateTaskButton.Visibility = Visibility.Collapsed; // Hide Create Task initially
            BackButton.IsEnabled = false; // Back button disabled on first tab

            // Initial wrapper location visibility based on checkbox
            ToggleWrapperLocationControls(CreateWrapperCheckBox.IsChecked ?? false);

            // Load scripts from network share
            System.Threading.Tasks.Task.Run(() => LoadNetworkScripts());

            // Add Loaded event to ensure UI is fully initialized
            this.Loaded += MainWindow_Loaded;

            // Set initialization flag to false until fully loaded
            _isWindowInitialized = false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initialization flag to true only after everything is loaded
            _isWindowInitialized = true;
            
            // Make sure initial panel visibility is set correctly
            if (ActionTypeComboBox != null && ActionTypeComboBox.SelectedItem != null)
            {
                SetActionPanelVisibility();
            }
        }

        // Helper to set visibility without triggering errors
        private void SetActionPanelVisibility()
        {
            if (!_isWindowInitialized || RunProgramPanel == null || EmailActionPanel == null || DisplayMessagePanel == null)
            {
                return;
            }
            
            // Hide all action panels first
            RunProgramPanel.Visibility = Visibility.Collapsed;
            EmailActionPanel.Visibility = Visibility.Collapsed;
            DisplayMessagePanel.Visibility = Visibility.Collapsed;
            
            // Show the appropriate panel based on selection
            if (ActionTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Run Program":
                        RunProgramPanel.Visibility = Visibility.Visible;
                        break;
                    case "Send Email":
                        EmailActionPanel.Visibility = Visibility.Visible;
                        break;
                    case "Display Message":
                        DisplayMessagePanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void PopulateTimePickers()
        {
            // Populate hours comboboxes (0-23)
            for (int i = 0; i < 24; i++)
            {
                string hourString = i.ToString("00");
                OneTimeHoursComboBox.Items.Add(hourString);
                DailyHoursComboBox.Items.Add(hourString);
                WeeklyHoursComboBox.Items.Add(hourString);
                MonthlyHoursComboBox.Items.Add(hourString);
            }
            int currentHour = DateTime.Now.Hour;
            OneTimeHoursComboBox.SelectedIndex = currentHour;
            DailyHoursComboBox.SelectedIndex = currentHour;
            WeeklyHoursComboBox.SelectedIndex = currentHour;
            MonthlyHoursComboBox.SelectedIndex = currentHour;

            // Populate minutes comboboxes (0-59)
            for (int i = 0; i < 60; i++)
            {
                string minuteString = i.ToString("00");
                OneTimeMinutesComboBox.Items.Add(minuteString);
                DailyMinutesComboBox.Items.Add(minuteString);
                WeeklyMinutesComboBox.Items.Add(minuteString);
                MonthlyMinutesComboBox.Items.Add(minuteString);
            }
            int currentMinute = DateTime.Now.Minute;
            OneTimeMinutesComboBox.SelectedIndex = currentMinute;
            DailyMinutesComboBox.SelectedIndex = currentMinute;
            WeeklyMinutesComboBox.SelectedIndex = currentMinute;
            MonthlyMinutesComboBox.SelectedIndex = currentMinute;
        }

        private void PopulateComboBoxes()
        {
            // Populate daily interval combobox (1-31)
            for (int i = 1; i <= 31; i++) DailyIntervalComboBox.Items.Add(i.ToString());
            DailyIntervalComboBox.SelectedIndex = 0; // Default to 1

            // Populate weekly interval combobox (1-52)
            for (int i = 1; i <= 52; i++) WeeklyIntervalComboBox.Items.Add(i.ToString());
            WeeklyIntervalComboBox.SelectedIndex = 0; // Default to 1
            
            // Populate monthly day combobox (1-31)
            for (int i = 1; i <= 31; i++) MonthlyDayComboBox.Items.Add(i.ToString());
            MonthlyDayComboBox.SelectedIndex = 0; // Default to 1
            
            // Populate monthly interval comboboxes (1-12)
            for (int i = 1; i <= 12; i++)
            {
                MonthlyIntervalComboBox.Items.Add(i.ToString());
                MonthlyDOWIntervalComboBox.Items.Add(i.ToString());
            }
            MonthlyIntervalComboBox.SelectedIndex = 0; // Default to 1
            MonthlyDOWIntervalComboBox.SelectedIndex = 0; // Default to 1
            
            // Initialize monthly week dropdown
            MonthlyWeekComboBox.SelectedIndex = 0;
            
            // Initialize monthly day of week dropdown
            MonthlyDayOfWeekComboBox.SelectedIndex = 1; // Default to Monday

            // Populate startup and logon delay comboboxes (0-60 minutes)
            for (int i = 0; i <= 60; i++)
            {
                StartupDelayComboBox.Items.Add(i.ToString());
                LogonDelayComboBox.Items.Add(i.ToString());
            }
            StartupDelayComboBox.SelectedIndex = 0; // Default to 0
            LogonDelayComboBox.SelectedIndex = 0; // Default to 0

            // Populate restart interval combobox (1-60 minutes)
            for (int i = 1; i <= 60; i++) RestartIntervalComboBox.Items.Add(i.ToString());
            RestartIntervalComboBox.SelectedIndex = 14; // Default to 15

            // Populate restart count combobox (1-10 times)
            for (int i = 1; i <= 10; i++) RestartCountComboBox.Items.Add(i.ToString());
            RestartCountComboBox.SelectedIndex = 2; // Default to 3

            // Populate execution hours combobox (1-72 hours)
            for (int i = 1; i <= 72; i++) ExecutionHoursComboBox.Items.Add(i.ToString());
            ExecutionHoursComboBox.SelectedIndex = 2; // Default to 3

            // Populate Logon User ComboBox (used when Specific User is selected for Logon trigger)
            LogonUserComboBox.Items.Add(Environment.UserName); // Add current user as an option
            // Add other common users/groups if needed, or allow typing
            LogonUserComboBox.SelectedIndex = 0; // Default to current user if list is populated

            // Instance Policy ComboBox
            // Items added in XAML, set default index
            InstancePolicyComboBox.SelectedIndex = 0;
        }

        private async System.Threading.Tasks.Task LoadNetworkScripts() // Qualified Task
        {
            try
            {
                Dispatcher.Invoke(() => StatusTextBlock.Text = "Searching for scripts...");
                Dispatcher.Invoke(() => _scripts.Clear()); // Clear list before loading

                // Get script type filter
                string scriptTypeFilter = "All";
                Dispatcher.Invoke(() =>
                {
                    if (ScriptTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                    {
                        scriptTypeFilter = selectedItem.Content.ToString();
                    }
                });

                // Get name filter
                string nameFilter = "";
                Dispatcher.Invoke(() => nameFilter = NameFilterTextBox.Text.Trim());

                // Use the ScriptManager to get scripts
                var scripts = ScriptManager.Instance.GetScripts(_networkSharePath, scriptTypeFilter, nameFilter);

                // Add scripts to UI
                Dispatcher.Invoke(() =>
                {
                    foreach (var script in scripts)
                    {
                        _scripts.Add(script);
                    }
                    StatusTextBlock.Text = $"Found {scripts.Count} scripts.";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                    Debug.WriteLine($"LoadNetworkScripts exception: {ex}");
                });
            }
        }

        private string GetScriptTypeFromExtension(string extension)
        {
            return ScriptManager.Instance.GetScriptTypeFromExtension(extension);
        }

        private void DisableTabsExcept(TabItem activeTab)
        {
            foreach (TabItem tab in MainTabControl.Items)
            {
                tab.IsEnabled = (tab == activeTab);
            }
            MainTabControl.SelectedItem = activeTab;
        }

        private void EnableTabsUpTo(TabItem targetTab)
        {
            bool foundTarget = false;
            foreach (TabItem tab in MainTabControl.Items)
            {
                if (!foundTarget)
                {
                    tab.IsEnabled = true;
                    if (tab == targetTab) foundTarget = true;
                }
                else tab.IsEnabled = false;
            }
            MainTabControl.SelectedItem = targetTab;
        }

        private void UpdateTaskSummary()
        {
            // Don't run if the window isn't fully initialized yet
            if (!_isWindowInitialized) return;

            try
            {
                // Verify needed controls are available
                if (TaskSummaryTextBox == null)
                {
                    // TaskSummaryTextBox not initialized yet, skip the update
                    return;
                }

                var summaryBuilder = new StringBuilder();
                summaryBuilder.AppendLine($"Task Summary");
                summaryBuilder.AppendLine($"{new string('=', 50)}");
                summaryBuilder.AppendLine();
                
                // Check each control before accessing it
                if (TaskNameTextBox != null)
                    summaryBuilder.AppendLine($"Task Name: {TaskNameTextBox.Text}");
                    
                if (TaskDescriptionTextBox != null)
                    summaryBuilder.AppendLine($"Description: {TaskDescriptionTextBox.Text}");
                    
                summaryBuilder.AppendLine($"Script: {_selectedScriptPath ?? "None"}");
                
                if (ScriptArgumentsTextBox != null)
                    summaryBuilder.AppendLine($"Arguments: {ScriptArgumentsTextBox.Text}");
                    
                if (WorkingDirTextBox != null)
                    summaryBuilder.AppendLine($"Working Directory: {WorkingDirTextBox.Text}");
                    
                // Use safe properties access
                string userPrincipal = "Current User"; // Default value
                try {
                    userPrincipal = GetUserPrincipal(); 
                } catch (Exception) { /* Ignore exceptions during UI init */ }
                
                summaryBuilder.AppendLine($"Run As: {userPrincipal}");
                
                if (RunWithHighestPrivilegesCheckBox != null)
                    summaryBuilder.AppendLine($"Highest Privileges: {RunWithHighestPrivilegesCheckBox.IsChecked}");
                    
                if (CreateWrapperCheckBox != null)
                {
                    summaryBuilder.AppendLine($"Create VBS Wrapper: {CreateWrapperCheckBox.IsChecked}");
                    if (CreateWrapperCheckBox.IsChecked == true && WrapperLocationTextBox != null)
                    {
                        summaryBuilder.AppendLine($"VBS Wrapper Location: {WrapperLocationTextBox.Text}");
                    }
                }

                summaryBuilder.AppendLine("\r\nTrigger:");
                ComboBoxItem selectedTrigger = TriggerTypeComboBox.SelectedItem as ComboBoxItem;
                if (selectedTrigger != null)
                {
                    summaryBuilder.AppendLine($"  Type: {selectedTrigger.Content}");
                    switch (selectedTrigger.Content.ToString())
                    {
                        case "On a schedule":
                            // Add checks for panel and control nulls before accessing properties
                            if (OneTimeSchedulePanel != null && OneTimeSchedulePanel.Visibility == Visibility.Visible)
                            {
                                if (OneTimeStartDatePicker != null && OneTimeHoursComboBox != null && OneTimeMinutesComboBox != null)
                                {
                                    string oneTimeDate = OneTimeStartDatePicker.SelectedDate.HasValue ? OneTimeStartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : "Not set";
                                    summaryBuilder.AppendLine($"  Start Date: {oneTimeDate}");
                                    summaryBuilder.AppendLine($"  Start Time: {OneTimeHoursComboBox.Text ?? "--"}:{OneTimeMinutesComboBox.Text ?? "--"}");
                                } else { summaryBuilder.AppendLine("  One Time controls not fully initialized."); }
                            }
                            else if (DailySchedulePanel != null && DailySchedulePanel.Visibility == Visibility.Visible)
                            {
                                if (DailyStartDatePicker != null && DailyHoursComboBox != null && DailyMinutesComboBox != null && DailyIntervalComboBox != null)
                                {
                                    string dailyDate = DailyStartDatePicker.SelectedDate.HasValue ? DailyStartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : "Not set";
                                    summaryBuilder.AppendLine($"  Start Date: {dailyDate}");
                                    summaryBuilder.AppendLine($"  Start Time: {DailyHoursComboBox.Text ?? "--"}:{DailyMinutesComboBox.Text ?? "--"}");
                                    summaryBuilder.AppendLine($"  Recur Every: {DailyIntervalComboBox.Text ?? "1"} days");
                                } else { summaryBuilder.AppendLine("  Daily controls not fully initialized."); }
                            }
                            else if (WeeklySchedulePanel != null && WeeklySchedulePanel.Visibility == Visibility.Visible)
                            {
                                if (WeeklyStartDatePicker != null && WeeklyHoursComboBox != null && WeeklyMinutesComboBox != null && WeeklyIntervalComboBox != null &&
                                    MondayCheckBox != null && TuesdayCheckBox != null && WednesdayCheckBox != null && ThursdayCheckBox != null &&
                                    FridayCheckBox != null && SaturdayCheckBox != null && SundayCheckBox != null) // Check checkboxes too
                                {
                                    string weeklyDate = WeeklyStartDatePicker.SelectedDate.HasValue ? WeeklyStartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : "Not set";
                                    summaryBuilder.AppendLine($"  Start Date: {weeklyDate}");
                                    summaryBuilder.AppendLine($"  Start Time: {WeeklyHoursComboBox.Text ?? "--"}:{WeeklyMinutesComboBox.Text ?? "--"}");
                                    summaryBuilder.AppendLine($"  Recur Every: {WeeklyIntervalComboBox.Text ?? "1"} weeks");
                                    List<string> days = new List<string>();
                                    if (MondayCheckBox.IsChecked == true) days.Add("Mon");
                                    if (TuesdayCheckBox.IsChecked == true) days.Add("Tue");
                                    if (WednesdayCheckBox.IsChecked == true) days.Add("Wed");
                                    if (ThursdayCheckBox.IsChecked == true) days.Add("Thu");
                                    if (FridayCheckBox.IsChecked == true) days.Add("Fri");
                                    if (SaturdayCheckBox.IsChecked == true) days.Add("Sat");
                                    if (SundayCheckBox.IsChecked == true) days.Add("Sun");
                                    summaryBuilder.AppendLine($"  Days: {(days.Any() ? string.Join(", ", days) : "None")}");
                                } else { summaryBuilder.AppendLine("  Weekly controls not fully initialized."); }
                            }
                            else if (MonthlySchedulePanel != null && MonthlySchedulePanel.Visibility == Visibility.Visible)
                            {
                                if (MonthlyStartDatePicker != null && MonthlyHoursComboBox != null && MonthlyMinutesComboBox != null)
                                {
                                    string monthlyDate = MonthlyStartDatePicker.SelectedDate.HasValue ? MonthlyStartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : "Not set";
                                    summaryBuilder.AppendLine($"  Start Date: {monthlyDate}");
                                    summaryBuilder.AppendLine($"  Start Time: {MonthlyHoursComboBox.Text ?? "--"}:{MonthlyMinutesComboBox.Text ?? "--"}");
                                    
                                    if (MonthlyDayOfMonthRadio.IsChecked == true)
                                    {
                                        // Day of month option
                                        summaryBuilder.AppendLine($"  Monthly Type: Day of month");
                                        summaryBuilder.AppendLine($"  Day: {MonthlyDayComboBox.Text ?? "1"}");
                                        summaryBuilder.AppendLine($"  Recur Every: {MonthlyIntervalComboBox.Text ?? "1"} month(s)");
                                    }
                                    else
                                    {
                                        // Day of week in month option
                                        summaryBuilder.AppendLine($"  Monthly Type: Day of week in month");
                                        
                                        string weekNumber = "First";
                                        if (MonthlyWeekComboBox.SelectedItem is ComboBoxItem selectedWeek)
                                        {
                                            weekNumber = selectedWeek.Content.ToString();
                                        }
                                        
                                        string dayOfWeek = "Monday";
                                        if (MonthlyDayOfWeekComboBox.SelectedItem is ComboBoxItem selectedDay)
                                        {
                                            dayOfWeek = selectedDay.Content.ToString();
                                        }
                                        
                                        summaryBuilder.AppendLine($"  Occurs: {weekNumber} {dayOfWeek}");
                                        summaryBuilder.AppendLine($"  Recur Every: {MonthlyDOWIntervalComboBox.Text ?? "1"} month(s)");
                                    }
                                } else { summaryBuilder.AppendLine("  Monthly controls not fully initialized."); }
                            }
                            break; // End "On a schedule"

                        case "At system startup":
                            if (StartupTriggerPanel != null && StartupDelayComboBox != null)
                            {
                                summaryBuilder.AppendLine($"  Delay: {StartupDelayComboBox.Text ?? "0"} minutes");
                            } else { summaryBuilder.AppendLine("  Startup controls not fully initialized."); }
                            break;

                        case "At log on":
                            if (LogonTriggerPanel != null && AnyUserRadioButton != null && SpecificUserRadioButton != null && LogonUserComboBox != null && LogonDelayComboBox != null)
                            {
                                string logonUser = AnyUserRadioButton.IsChecked == true ? "Any User" : $"Specific User ({LogonUserComboBox.Text ?? "N/A"})";
                                summaryBuilder.AppendLine($"  User: {logonUser}");
                                summaryBuilder.AppendLine($"  Delay: {LogonDelayComboBox.Text ?? "0"} minutes");
                            } else { summaryBuilder.AppendLine("  Logon controls not fully initialized."); }
                            break;
                    }
                }

                summaryBuilder.AppendLine("\r\nSettings:");
                // Add null checks for settings controls as well
                summaryBuilder.AppendLine($"  Allow task to be run on demand: {AllowDemandCheckBox?.IsChecked ?? false}");
                summaryBuilder.AppendLine($"  Run task if missed: {RunIfMissedCheckBox?.IsChecked ?? false}");
                summaryBuilder.AppendLine($"  If task fails, restart every: {RestartIntervalComboBox?.Text ?? "15"} minutes");
                summaryBuilder.AppendLine($"  Attempt restart up to: {RestartCountComboBox?.Text ?? "3"} times");
                summaryBuilder.AppendLine($"  Stop task if runs longer than: {(NoTimeLimitCheckBox?.IsChecked == true ? "No Limit" : $"{ExecutionHoursComboBox?.Text ?? "3"} hours")}");
                summaryBuilder.AppendLine($"  Instance Policy: {(InstancePolicyComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default"}");
                
                // Add power settings
                if (StopOnBatteryCheckBox != null || StartOnBatteryCheckBox != null || WakeToRunCheckBox != null)
                {
                    summaryBuilder.AppendLine("\r\nPower Settings:");
                    if (StopOnBatteryCheckBox != null)
                        summaryBuilder.AppendLine($"  Stop if computer switches to battery power: {StopOnBatteryCheckBox.IsChecked ?? true}");
                    if (StartOnBatteryCheckBox != null)
                        summaryBuilder.AppendLine($"  Start only if AC power is available: {!(StartOnBatteryCheckBox.IsChecked ?? false)}");
                    if (WakeToRunCheckBox != null)
                        summaryBuilder.AppendLine($"  Wake computer to run this task: {WakeToRunCheckBox.IsChecked ?? false}");
                }
                
                // Add network conditions
                if (NetworkConditionCheckBox != null)
                {
                    summaryBuilder.AppendLine("\r\nNetwork Conditions:");
                    summaryBuilder.AppendLine($"  Run only when network connection is available: {NetworkConditionCheckBox.IsChecked ?? false}");
                }
                
                // Add idle settings if applicable
                if (IdleSettingsCheckBox != null && IdleSettingsCheckBox.IsChecked == true)
                {
                    summaryBuilder.AppendLine("\r\nIdle Settings:");
                    summaryBuilder.AppendLine($"  Start only if computer is idle for: {IdleMinutesComboBox?.Text ?? "10"} minutes");
                    summaryBuilder.AppendLine($"  Wait for idle for: {WaitTimeoutComboBox?.Text ?? "1"} hour");
                    if (StopOnIdleEndCheckBox != null)
                        summaryBuilder.AppendLine($"  Stop if computer ceases to be idle: {StopOnIdleEndCheckBox.IsChecked ?? false}");
                    if (RestartOnIdleCheckBox != null)
                        summaryBuilder.AppendLine($"  Restart if idle state resumes: {RestartOnIdleCheckBox.IsChecked ?? false}");
                }
                
                // Add additional task conditions
                summaryBuilder.AppendLine("\r\nSecurity Options:");
                summaryBuilder.AppendLine($"  Run with highest privileges: {RunWithHighestPrivilegesCheckBox?.IsChecked ?? false}");
                if (ForceStopCheckBox != null)
                    summaryBuilder.AppendLine($"  Force stop task if it does not end when requested: {ForceStopCheckBox.IsChecked ?? true}");

                TaskSummaryTextBox.Text = summaryBuilder.ToString();
            }
            catch (Exception ex)
            {
                // Handle quietly during init to avoid error dialogs
                if (TaskSummaryTextBox != null)
                    TaskSummaryTextBox.Text = $"Error generating summary: {ex.Message}";
                
                System.Diagnostics.Debug.WriteLine($"Summary Error: {ex}");
            }
        }

        // --- Event Handlers ---

        private void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            System.Threading.Tasks.Task.Run(() => LoadNetworkScripts()); // Qualified Task.Run
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ScriptTypeComboBox.SelectedIndex = 0;
            NameFilterTextBox.Text = "";
            System.Threading.Tasks.Task.Run(() => LoadNetworkScripts()); // Qualified Task.Run
        }

        private void ScriptsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if not fully initialized
            if (!_isWindowInitialized) return;

            if (ScriptsListView.SelectedItem is ScriptItem selectedItem)
            {
                _selectedScriptPath = selectedItem.Path;
                _selectedScriptType = selectedItem.Type;

                // Update UI elements on the Script Selection Tab
                SelectedScriptTextBox.Text = selectedItem.Name; // Use Name for display
                ScriptTypeTextBox.Text = selectedItem.Type;
                DescriptionTextBox.Text = selectedItem.Description;

                // Update UI elements on the Schedule Tab
                TaskNameTextBox.Text = Path.GetFileNameWithoutExtension(selectedItem.Name); // Auto-populate Task Name
                ConfigScriptPathTextBox.Text = selectedItem.Path; // Update path on config tab
                ConfigScriptTypeTextBox.Text = selectedItem.Type; // Update type on config tab

                // Enable navigation button, but don't navigate automatically
                NextButton.IsEnabled = true;
                // EnableTabsUpTo(ScheduleTab); // REMOVED: Navigation happens on Next button click
            }
            else
            {
                // No script selected, clear details and disable navigation
                _selectedScriptPath = string.Empty;
                _selectedScriptType = string.Empty;
                SelectedScriptTextBox.Text = "None";
                ScriptTypeTextBox.Text = "";
                DescriptionTextBox.Text = "";

                // Clear relevant fields on Schedule Tab
                TaskNameTextBox.Text = "";
                ConfigScriptPathTextBox.Text = "";
                ConfigScriptTypeTextBox.Text = "";

                NextButton.IsEnabled = false;
                DisableTabsExcept(ScriptSelectionTab); // Disable subsequent tabs
            }

            // Update the summary whenever selection changes
            UpdateTaskSummary();
        }

        private void UserAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if not fully initialized
            if (!_isWindowInitialized) return;
            
            // This combo box in the XAML is for selecting SYSTEM/Current/Specific
            // The GetUserPrincipal method handles retrieving the correct value based on this.
            
            // Show or hide the SpecifiedUserPanel based on selection
            if (SpecifiedUserPanel != null)
            {
                if (UserAccountComboBox.SelectedItem is ComboBoxItem selectedItem && 
                    selectedItem.Content.ToString() == "Specified User")
                {
                    SpecifiedUserPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    SpecifiedUserPanel.Visibility = Visibility.Collapsed;
                }
            }
            
            // We might need to update the summary if the selection changes.
            UpdateTaskSummary();
        }

        private void TriggerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if not fully initialized
            if (!_isWindowInitialized) return;

            // Hide all schedule panels initially, checking for null in case handler runs before elements are ready
            if (OneTimeSchedulePanel != null) OneTimeSchedulePanel.Visibility = Visibility.Collapsed;
            if (DailySchedulePanel != null) DailySchedulePanel.Visibility = Visibility.Collapsed;
            if (WeeklySchedulePanel != null) WeeklySchedulePanel.Visibility = Visibility.Collapsed;
            if (MonthlySchedulePanel != null) MonthlySchedulePanel.Visibility = Visibility.Collapsed;
            if (StartupTriggerPanel != null) StartupTriggerPanel.Visibility = Visibility.Collapsed;
            if (LogonTriggerPanel != null) LogonTriggerPanel.Visibility = Visibility.Collapsed;
            if (IdleTriggerPanel != null) IdleTriggerPanel.Visibility = Visibility.Collapsed;
            if (EventTriggerPanel != null) EventTriggerPanel.Visibility = Visibility.Collapsed;
            if (RegistrationTriggerPanel != null) RegistrationTriggerPanel.Visibility = Visibility.Collapsed;
            if (SessionStateTriggerPanel != null) SessionStateTriggerPanel.Visibility = Visibility.Collapsed;
            
            // Hide schedule type container for non-schedule triggers
            if (ScheduleTypeContainer != null)
                ScheduleTypeContainer.Visibility = TriggerTypeComboBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // Show the appropriate panel based on selection
            if (TriggerTypeComboBox?.SelectedItem is ComboBoxItem selectedItem) // Added null-conditional operator for safety
            {
                switch (selectedItem.Content.ToString())
                {
                    case "On a schedule":
                        // For now, simply show the OneTimeSchedulePanel
                        // We'll add ScheduleTypeComboBox in the future for more options
                        if (ScheduleTypeComboBox != null)
                            ScheduleTypeComboBox_SelectionChanged(ScheduleTypeComboBox, null);
                        break;
                    case "At system startup":
                        if (StartupTriggerPanel != null) StartupTriggerPanel.Visibility = Visibility.Visible;
                        break;
                    case "At log on":
                        if (LogonTriggerPanel != null) LogonTriggerPanel.Visibility = Visibility.Visible;
                        break;
                    case "On idle":
                        if (IdleTriggerPanel != null) IdleTriggerPanel.Visibility = Visibility.Visible;
                        break;
                    case "On an event":
                        if (EventTriggerPanel != null) EventTriggerPanel.Visibility = Visibility.Visible;
                        break;
                    case "At task creation/modification":
                        if (RegistrationTriggerPanel != null) RegistrationTriggerPanel.Visibility = Visibility.Visible;
                        break;
                    case "On session change":
                        if (SessionStateTriggerPanel != null) SessionStateTriggerPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
            UpdateTaskSummary();
        }
        
        private void ScheduleTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if not fully initialized
            if (!_isWindowInitialized) return;

            // Only active if TriggerTypeComboBox is set to "On a schedule"
            if (TriggerTypeComboBox?.SelectedItem is ComboBoxItem selectedTrigger && 
                selectedTrigger.Content.ToString() == "On a schedule")
            {
                // Hide all schedule panels first
                if (OneTimeSchedulePanel != null) OneTimeSchedulePanel.Visibility = Visibility.Collapsed;
                if (DailySchedulePanel != null) DailySchedulePanel.Visibility = Visibility.Collapsed;
                if (WeeklySchedulePanel != null) WeeklySchedulePanel.Visibility = Visibility.Collapsed;
                if (MonthlySchedulePanel != null) MonthlySchedulePanel.Visibility = Visibility.Collapsed;

                // Show the appropriate panel based on selection
                if (ScheduleTypeComboBox?.SelectedItem is ComboBoxItem scheduleType)
                {
                    switch (scheduleType.Content.ToString())
                    {
                        case "One time":
                            if (OneTimeSchedulePanel != null) OneTimeSchedulePanel.Visibility = Visibility.Visible;
                            break;
                        case "Daily":
                            if (DailySchedulePanel != null) DailySchedulePanel.Visibility = Visibility.Visible;
                            break;
                        case "Weekly":
                            if (WeeklySchedulePanel != null) WeeklySchedulePanel.Visibility = Visibility.Visible;
                            break;
                        case "Monthly":
                            if (MonthlySchedulePanel != null) MonthlySchedulePanel.Visibility = Visibility.Visible;
                            break;
                    }
                }
                UpdateTaskSummary();
            }
        }

        private void BrowseWorkingDirButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Working Directory";
                if (Directory.Exists(WorkingDirTextBox.Text)) dialog.SelectedPath = WorkingDirTextBox.Text;
                else if (Directory.Exists(_networkSharePath)) dialog.SelectedPath = _networkSharePath;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WorkingDirTextBox.Text = dialog.SelectedPath;
                    UpdateTaskSummary();
                }
            }
        }

        private void BrowseWrapperLocationButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select VBS Wrapper Location";
                dialog.SelectedPath = WrapperLocationTextBox.Text;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WrapperLocationTextBox.Text = dialog.SelectedPath;
                    UpdateTaskSummary();
                }
            }
        }

        private void ToggleWrapperLocationControls(bool isEnabled)
        {
            // Make sure we have valid references to the controls
            bool hasWrapperControls = WrapperLocationLabel != null && WrapperLocationTextBox != null && BrowseWrapperLocationButton != null;
            bool hasExplanationTextBlock = HiddenExplanationTextBlock != null;
            
            if (!hasWrapperControls)
            {
                System.Diagnostics.Debug.WriteLine("One or more wrapper location controls not found. Check XAML structure.");
                // Continue with what we can, rather than returning early
            }
            
            // Enable/disable controls if they exist
            if (hasWrapperControls)
            {
                WrapperLocationLabel.IsEnabled = isEnabled;
                WrapperLocationTextBox.IsEnabled = isEnabled;
                BrowseWrapperLocationButton.IsEnabled = isEnabled;
            }
            
            if (!isEnabled)
            {
                // If wrapper is disabled, hide the controls to simplify UI
                if (hasWrapperControls)
                {
                    WrapperLocationLabel.Visibility = Visibility.Collapsed;
                    WrapperLocationTextBox.Visibility = Visibility.Collapsed;
                    BrowseWrapperLocationButton.Visibility = Visibility.Collapsed;
                }
                
                if (hasExplanationTextBlock)
                {
                    HiddenExplanationTextBlock.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Make sure controls are visible when enabled
                if (hasWrapperControls)
                {
                    WrapperLocationLabel.Visibility = Visibility.Visible;
                    WrapperLocationTextBox.Visibility = Visibility.Visible;
                    BrowseWrapperLocationButton.Visibility = Visibility.Visible;
                    
                    // Ensure the wrapper location textbox has a value
                    if (string.IsNullOrWhiteSpace(WrapperLocationTextBox.Text))
                    {
                        WrapperLocationTextBox.Text = _wrapperDir; // Use the default directory
                    }
                }
                
                if (hasExplanationTextBlock)
                {
                    HiddenExplanationTextBlock.Visibility = Visibility.Visible;
                }
            }
        }

        private void CreateWrapperCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Skip if not fully initialized
            if (!_isWindowInitialized) return;
            
            ToggleWrapperLocationControls(CreateWrapperCheckBox.IsChecked ?? false);
            UpdateTaskSummary();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = MainTabControl.SelectedIndex;
            if (currentIndex > 0)
            {
                EnableTabsUpTo(MainTabControl.Items[currentIndex - 1] as TabItem);
                NextButton.Visibility = Visibility.Visible; // Show Next button
                CreateTaskButton.Visibility = Visibility.Collapsed; // Hide Create Task button
                BackButton.IsEnabled = (currentIndex - 1 > 0); // Disable back if going to first tab
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = MainTabControl.SelectedIndex;
            int nextIndex = currentIndex + 1;

            // Validation
            if (currentIndex == 0 && string.IsNullOrEmpty(_selectedScriptPath))
            {
                MessageBox.Show("Please select a script first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            if (currentIndex == 1) // Schedule Tab
            {
                 if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
                 { MessageBox.Show("Please enter a Task Name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                 if (TriggerTypeComboBox.SelectedItem == null)
                 { MessageBox.Show("Please select a Trigger Type.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                 // Add more trigger-specific validation if needed
            }
            // Add validation for Settings Tab if necessary

            if (nextIndex < MainTabControl.Items.Count)
            {
                bool isMovingToSummary = (MainTabControl.Items[nextIndex] as TabItem) == SummaryTab;

                if (isMovingToSummary)
                {
                    UpdateTaskSummary();
                    NextButton.Visibility = Visibility.Collapsed; // Hide Next on Summary
                    CreateTaskButton.Visibility = Visibility.Visible; // Show Create Task on Summary
                }
                else
                {
                     NextButton.Visibility = Visibility.Visible;
                     CreateTaskButton.Visibility = Visibility.Collapsed;
                }

                EnableTabsUpTo(MainTabControl.Items[nextIndex] as TabItem);
                BackButton.IsEnabled = true; // Enable back button when moving off the first tab
            }
        }

        private async void CreateTaskButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Creating scheduled task...";
            CreateTaskButton.IsEnabled = false;
            
            try
            {
                string scriptPathToUse = _selectedScriptPath; // Default to direct script execution

                // If user chose to create a wrapper script, generate it first
                if (CreateWrapperCheckBox.IsChecked == true)
                {
                    StatusTextBlock.Text = "Creating VBS wrapper script...";
                    scriptPathToUse = await CreateVBSWrapper(); // Use the renamed method
                }

                // Create the scheduled task with the script or wrapper
                StatusTextBlock.Text = "Registering task with Task Scheduler...";
                bool success = await CreateScheduledTask(scriptPathToUse);

                if (success)
                {
                    StatusTextBlock.Text = "Task created successfully.";
                    MessageBox.Show("Scheduled task created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = "Failed to create task.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error: " + ex.Message;
                MessageBox.Show($"Error creating task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CreateTaskButton.IsEnabled = true;
            }
        }

        // --- Core Logic ---

        private async Task<string> CreateVBSWrapper()
        {
            string taskName = TaskNameTextBox.Text.Trim();
            string wrapperDir = WrapperLocationTextBox.Text.Trim();
            string workingDir = WorkingDirTextBox.Text.Trim();
            string scriptArgs = ScriptArgumentsTextBox.Text.Trim();

            try
            {
                // Use the TaskWrapper class to create a VBS wrapper
                string wrapperPath = TaskWrapper.CreateVBSWrapper(
                    taskName,
                    _selectedScriptPath,
                    _selectedScriptType,
                    wrapperDir,
                    workingDir,
                    scriptArgs,
                    _logDir);
                
                return wrapperPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create wrapper: {ex.Message}");
            }
        }

        // Refactored to use ScriptManager and Task Scheduler Library
        private async Task<bool> CreateScheduledTask(string scriptPathOrWrapper)
        {
            try
            {
                // Gather Task Parameters from UI
                string taskName = TaskNameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(taskName))
                {
                    MessageBox.Show("Task Name cannot be empty.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                string taskDescription = TaskDescriptionTextBox.Text.Trim();
                string workingDir = WorkingDirTextBox.Text.Trim();
                string principal = GetUserPrincipal(); // e.g., "SYSTEM" or "DOMAIN\User"
                string password = null; // Initialize password
                bool highestPrivileges = RunWithHighestPrivilegesCheckBox.IsChecked ?? false;
                string scriptArgs = ScriptArgumentsTextBox.Text.Trim(); // Arguments for the target script

                // --- Credential Handling ---
                string currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
                if (!principal.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) &&
                    !principal.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                {
                    // Need credentials for a specific user
                    string targetServer = GetTargetServerFromPaths(); // Helper to find server name
                    if (!string.IsNullOrEmpty(targetServer))
                    {
                        System.Diagnostics.Debug.WriteLine($"Attempting to retrieve credentials for target '{targetServer}' from Credential Manager.");
                        if (CredentialManagerHelper.GetCredential(targetServer, out string storedUser, out string storedPassword))
                        {
                            System.Diagnostics.Debug.WriteLine($"Found credential for user '{storedUser}' on target '{targetServer}'.");
                            // IMPORTANT: Check if the stored username matches the principal we intend to run as.
                            // Credential Manager stores ONE credential per target.
                            if (principal.Equals(storedUser, StringComparison.OrdinalIgnoreCase))
                            {
                                password = storedPassword; // Use the retrieved password
                                System.Diagnostics.Debug.WriteLine($"Using password from Credential Manager for user '{principal}'.");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Credential Manager user '{storedUser}' does not match target principal '{principal}'. Password not used.");
                                MessageBox.Show($"The credential stored for '{targetServer}' is for user '{storedUser}', but the task is set to run as '{principal}'.\n\nPlease update the stored credential or manually provide the password if prompted (prompt not implemented).", "Credential Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                                // Password remains null for now, task creation will likely fail.
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Credential not found for target '{targetServer}' in Credential Manager.");
                            MessageBox.Show($"No credential found for target '{targetServer}' in Windows Credential Manager.\n\nPlease add the credential or provide it if prompted (prompt not implemented).", "Credential Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                            // Password remains null for now, task creation will likely fail.
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Could not determine target server from network paths. Cannot retrieve credentials.");
                        MessageBox.Show($"Could not determine the network target server from paths like '{_networkSharePath}'. Cannot automatically retrieve credentials.\n\nPlease provide the password if prompted (prompt not implemented).", "Target Server Unknown", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // Password remains null for now, task creation will likely fail.
                    }
                }
                // --- End Credential Handling ---

                // Get trigger type and parameters
                string triggerType = GetSelectedTriggerType();
                DateTime startTime = GetStartTimeFromUI(triggerType);
                Dictionary<string, object> triggerParams = GetTriggerParameters(triggerType);
                
                // Check if we need to add any display message actions
                if (_taskActions.Count > 0 && _taskActions.Any(a => a is DisplayMessageAction))
                {
                    Dictionary<string, object> additionalActions = new Dictionary<string, object>();
                    var displayMessageActions = _taskActions.OfType<DisplayMessageAction>().ToList();
                    
                    if (displayMessageActions.Any())
                    {
                        additionalActions["DisplayMessageActions"] = displayMessageActions;
                        triggerParams["AdditionalActions"] = additionalActions;
                    }
                }

                // Use ScriptManager to create the task
                bool result = ScriptManager.Instance.CreateScheduledTask(
                    taskName, 
                    taskDescription, 
                    scriptPathOrWrapper, 
                    _selectedScriptType, 
                    scriptArgs, 
                    workingDir,
                    principal, 
                    password, 
                    highestPrivileges, 
                    triggerType, 
                    startTime, 
                    triggerParams);

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error preparing scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"CreateScheduledTask exception: {ex}");
                return false;
            }
        }

        // Helper to get the selected trigger type
        private string GetSelectedTriggerType()
        {
            string triggerType = "ONCE"; // Default
            
            if (TriggerTypeComboBox?.SelectedItem is ComboBoxItem selectedTrigger)
            {
                string content = selectedTrigger.Content.ToString();
                
                switch (content)
                {
                    case "On a schedule":
                        // Check which schedule type is selected
                        if (ScheduleTypeComboBox?.SelectedItem is ComboBoxItem scheduleType)
                        {
                            switch (scheduleType.Content.ToString())
                            {
                                case "One time":
                                    triggerType = "ONCE";
                                    break;
                                case "Daily":
                                    triggerType = "DAILY";
                                    break;
                                case "Weekly":
                                    triggerType = "WEEKLY";
                                    break;
                                case "Monthly":
                                    triggerType = "MONTHLY";
                                    break;
                                default:
                                    triggerType = "ONCE";
                                    break;
                            }
                        }
                        break;
                    case "At system startup":
                        triggerType = "STARTUP";
                        break;
                    case "At log on":
                        triggerType = "LOGON";
                        break;
                    case "On idle":
                        triggerType = "IDLE";
                        break;
                    case "On an event":
                        triggerType = "EVENT";
                        break;
                    case "At task creation/modification":
                        triggerType = "REGISTRATION";
                        break;
                    case "On session change":
                        triggerType = "SESSION_STATE_CHANGE";
                        break;
                    default:
                        triggerType = "ONCE";
                        break;
                }
            }
            
            return triggerType;
        }

        // Helper to get start time from UI based on trigger type
        private DateTime GetStartTimeFromUI(string triggerType)
        {
            DateTime startTime = DateTime.Now.AddMinutes(5); // Default is 5 minutes from now
            
            try
            {
                switch (triggerType)
                {
                    case "ONCE":
                        if (OneTimeHoursComboBox.SelectedItem != null && OneTimeMinutesComboBox.SelectedItem != null)
                        {
                            int oneTimeHour = int.Parse(OneTimeHoursComboBox.SelectedItem.ToString() ?? "0");
                            int oneTimeMinute = int.Parse(OneTimeMinutesComboBox.SelectedItem.ToString() ?? "0");
                            DateTime oneTimeDate = OneTimeStartDatePicker.SelectedDate ?? DateTime.Today;
                            startTime = new DateTime(oneTimeDate.Year, oneTimeDate.Month, oneTimeDate.Day, oneTimeHour, oneTimeMinute, 0);
                        }
                        break;
                        
                    case "DAILY":
                        if (DailyHoursComboBox.SelectedItem != null && DailyMinutesComboBox.SelectedItem != null)
                        {
                            int dailyHour = int.Parse(DailyHoursComboBox.SelectedItem.ToString() ?? "0");
                            int dailyMinute = int.Parse(DailyMinutesComboBox.SelectedItem.ToString() ?? "0");
                            DateTime dailyDate = DailyStartDatePicker.SelectedDate ?? DateTime.Today;
                            startTime = new DateTime(dailyDate.Year, dailyDate.Month, dailyDate.Day, dailyHour, dailyMinute, 0);
                        }
                        break;
                        
                    case "WEEKLY":
                        if (WeeklyHoursComboBox.SelectedItem != null && WeeklyMinutesComboBox.SelectedItem != null)
                        {
                            int weeklyHour = int.Parse(WeeklyHoursComboBox.SelectedItem.ToString() ?? "0");
                            int weeklyMinute = int.Parse(WeeklyMinutesComboBox.SelectedItem.ToString() ?? "0");
                            DateTime weeklyDate = WeeklyStartDatePicker.SelectedDate ?? DateTime.Today;
                            startTime = new DateTime(weeklyDate.Year, weeklyDate.Month, weeklyDate.Day, weeklyHour, weeklyMinute, 0);
                        }
                        break;

                    case "MONTHLY":
                        if (MonthlyHoursComboBox.SelectedItem != null && MonthlyMinutesComboBox.SelectedItem != null)
                        {
                            int monthlyHour = int.Parse(MonthlyHoursComboBox.SelectedItem.ToString() ?? "0");
                            int monthlyMinute = int.Parse(MonthlyMinutesComboBox.SelectedItem.ToString() ?? "0");
                            DateTime monthlyDate = MonthlyStartDatePicker.SelectedDate ?? DateTime.Today;
                            startTime = new DateTime(monthlyDate.Year, monthlyDate.Month, monthlyDate.Day, monthlyHour, monthlyMinute, 0);
                        }
                        break;
                        
                    // For STARTUP and LOGON, just return current time plus 5 minutes
                    default:
                        startTime = DateTime.Now.AddMinutes(5);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting start time from UI: {ex.Message}");
                // Return default if any error occurs
                startTime = DateTime.Now.AddMinutes(5);
            }
            
            return startTime;
        }

        // Helper to get additional trigger parameters from UI
        private Dictionary<string, object> GetTriggerParameters(string triggerType)
        {
            var parameters = new Dictionary<string, object>();
            
            try
            {
                switch (triggerType)
                {
                    case "DAILY":
                        if (DailyIntervalComboBox.SelectedItem != null)
                        {
                            if (int.TryParse(DailyIntervalComboBox.SelectedItem.ToString(), out int dailyInterval))
                            {
                                parameters["DailyInterval"] = dailyInterval;
                            }
                        }
                        break;
                        
                    case "WEEKLY":
                        if (WeeklyIntervalComboBox.SelectedItem != null)
                        {
                            if (int.TryParse(WeeklyIntervalComboBox.SelectedItem.ToString(), out int weeklyInterval))
                            {
                                parameters["WeeklyInterval"] = weeklyInterval;
                            }
                        }
                        
                        // Get days of week
                        DaysOfTheWeek daysOfWeek = 0; // Use 0 instead of DaysOfTheWeek.None
                        if (MondayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Monday;
                        if (TuesdayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Tuesday;
                        if (WednesdayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Wednesday;
                        if (ThursdayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Thursday;
                        if (FridayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Friday;
                        if (SaturdayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Saturday;
                        if (SundayCheckBox.IsChecked == true) daysOfWeek |= DaysOfTheWeek.Sunday;
                        
                        // Make sure at least one day is selected
                        if (daysOfWeek == 0) // Compare with 0 instead of DaysOfTheWeek.None
                        {
                            daysOfWeek = DaysOfTheWeek.Monday; // Default to Monday if none selected
                        }
                        
                        parameters["DaysOfWeek"] = daysOfWeek;
                        break;
                    
                    case "MONTHLY":
                        // Handle the two types of monthly triggers
                        if (MonthlyDayOfMonthRadio.IsChecked == true)
                        {
                            // Day of month option
                            parameters["MonthlyTriggerType"] = "DayOfMonth";
                            
                            // Get day of month value
                            if (MonthlyDayComboBox.SelectedItem != null &&
                                int.TryParse(MonthlyDayComboBox.SelectedItem.ToString(), out int monthlyDay))
                            {
                                parameters["MonthlyDay"] = monthlyDay;
                            }
                            else
                            {
                                parameters["MonthlyDay"] = 1; // Default to 1st day
                            }
                            
                            // Get months interval value
                            if (MonthlyIntervalComboBox.SelectedItem != null &&
                                int.TryParse(MonthlyIntervalComboBox.SelectedItem.ToString(), out int monthlyInterval))
                            {
                                parameters["MonthlyInterval"] = monthlyInterval;
                            }
                            else
                            {
                                parameters["MonthlyInterval"] = 1; // Default to every month
                            }
                        }
                        else
                        {
                            // Day of Week in Month option
                            parameters["MonthlyTriggerType"] = "DayOfWeekInMonth";
                            
                            // Get the week of month
                            if (MonthlyWeekComboBox.SelectedItem is ComboBoxItem selectedWeek)
                            {
                                string weekText = selectedWeek.Content.ToString();
                                int weekNum;
                                
                                switch (weekText)
                                {
                                    case "First":
                                        weekNum = 1;
                                        break;
                                    case "Second":
                                        weekNum = 2;
                                        break;
                                    case "Third":
                                        weekNum = 3;
                                        break;
                                    case "Fourth":
                                        weekNum = 4;
                                        break;
                                    case "Last":
                                        weekNum = 5; // Task Scheduler uses 5 for Last
                                        break;
                                    default:
                                        weekNum = 1;
                                        break;
                                }
                                
                                parameters["MonthlyWeekNumber"] = weekNum;
                            }
                            
                            // Get the day of week 
                            if (MonthlyDayOfWeekComboBox.SelectedItem is ComboBoxItem selectedDay)
                            {
                                string dayText = selectedDay.Content.ToString();
                                DaysOfTheWeek dayOfWeek;
                                
                                switch (dayText)
                                {
                                    case "Sunday":
                                        dayOfWeek = DaysOfTheWeek.Sunday;
                                        break;
                                    case "Monday":
                                        dayOfWeek = DaysOfTheWeek.Monday;
                                        break;
                                    case "Tuesday":
                                        dayOfWeek = DaysOfTheWeek.Tuesday;
                                        break;
                                    case "Wednesday":
                                        dayOfWeek = DaysOfTheWeek.Wednesday;
                                        break;
                                    case "Thursday":
                                        dayOfWeek = DaysOfTheWeek.Thursday;
                                        break;
                                    case "Friday":
                                        dayOfWeek = DaysOfTheWeek.Friday;
                                        break;
                                    case "Saturday":
                                        dayOfWeek = DaysOfTheWeek.Saturday;
                                        break;
                                    case "Day":
                                        dayOfWeek = DaysOfTheWeek.AllDays;
                                        break;
                                    case "Weekday":
                                        dayOfWeek = DaysOfTheWeek.Monday | DaysOfTheWeek.Tuesday | 
                                                   DaysOfTheWeek.Wednesday | DaysOfTheWeek.Thursday | 
                                                   DaysOfTheWeek.Friday;
                                        break;
                                    case "Weekend day":
                                        dayOfWeek = DaysOfTheWeek.Saturday | DaysOfTheWeek.Sunday;
                                        break;
                                    default:
                                        dayOfWeek = DaysOfTheWeek.Monday;
                                        break;
                                }
                                
                                parameters["MonthlyDayOfWeek"] = dayOfWeek;
                            }
                            
                            // Get months interval for DOW option
                            if (MonthlyDOWIntervalComboBox.SelectedItem != null &&
                                int.TryParse(MonthlyDOWIntervalComboBox.SelectedItem.ToString(), out int monthlyDOWInterval))
                            {
                                parameters["MonthlyDOWInterval"] = monthlyDOWInterval;
                            }
                            else
                            {
                                parameters["MonthlyDOWInterval"] = 1; // Default to every month
                            }
                        }
                        break;
                        
                    case "STARTUP":
                        if (StartupDelayComboBox.SelectedItem != null)
                        {
                            if (int.TryParse(StartupDelayComboBox.SelectedItem.ToString(), out int startupDelay))
                            {
                                parameters["StartupDelay"] = startupDelay;
                            }
                        }
                        break;
                        
                    case "LOGON":
                        if (LogonDelayComboBox.SelectedItem != null)
                        {
                            if (int.TryParse(LogonDelayComboBox.SelectedItem.ToString(), out int logonDelay))
                            {
                                parameters["LogonDelay"] = logonDelay;
                            }
                        }
                        
                        // Check if LogonUserComboBox has a selected item
                        if (LogonUserComboBox.SelectedItem != null)
                        {
                            string logonUser = LogonUserComboBox.SelectedItem.ToString();
                            if (!string.IsNullOrEmpty(logonUser))
                            {
                                parameters["LogonUser"] = logonUser;
                            }
                        }
                        break;
                        
                    case "IDLE":
                        // For idle trigger, the idle time is set in the TaskDefinition settings
                        // So we don't need to add specific parameters here
                        if (IdleTriggerMinutesComboBox.SelectedItem != null &&
                            int.TryParse(IdleTriggerMinutesComboBox.SelectedItem.ToString(), out int idleMinutes))
                        {
                            parameters["IdleMinutes"] = idleMinutes;
                        }
                        else
                        {
                            parameters["IdleMinutes"] = 10; // Default to 10 minutes
                        }
                        break;
                        
                    case "EVENT":
                        // Get the event log parameters
                        if (EventLogComboBox.SelectedItem is ComboBoxItem selectedLog)
                        {
                            parameters["EventLogName"] = selectedLog.Content.ToString();
                        }
                        else
                        {
                            parameters["EventLogName"] = "Application"; // Default
                        }
                        
                        parameters["EventSource"] = EventSourceTextBox.Text.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(EventIdTextBox.Text) &&
                            int.TryParse(EventIdTextBox.Text, out int eventId))
                        {
                            parameters["EventId"] = eventId;
                        }
                        break;
                        
                    case "REGISTRATION":
                        // For registration trigger, the only parameter is delay
                        if (RegistrationDelayComboBox.SelectedItem != null &&
                            int.TryParse(RegistrationDelayComboBox.SelectedItem.ToString(), out int registrationDelay))
                        {
                            parameters["RegistrationDelay"] = registrationDelay;
                        }
                        else
                        {
                            parameters["RegistrationDelay"] = 0; // Default to no delay
                        }
                        break;
                        
                    case "SESSION_STATE_CHANGE":
                        // Get the session state change type
                        if (SessionStateComboBox.SelectedItem is ComboBoxItem selectedState && 
                            selectedState.Tag != null &&
                            int.TryParse(selectedState.Tag.ToString(), out int stateChange))
                        {
                            parameters["SessionStateChange"] = stateChange;
                        }
                        else
                        {
                            parameters["SessionStateChange"] = 1; // Default to ConsoleConnect
                        }
                        
                        // Check if specific user is selected
                        parameters["SessionAnyUser"] = SessionAnyUserRadioButton.IsChecked ?? true;
                        
                        if (SessionSpecificUserRadioButton.IsChecked == true && 
                            SessionUserComboBox.SelectedItem != null)
                        {
                            parameters["SessionUser"] = SessionUserComboBox.SelectedItem.ToString();
                        }
                        
                        // Get delay value
                        if (SessionDelayComboBox.SelectedItem != null &&
                            int.TryParse(SessionDelayComboBox.SelectedItem.ToString(), out int sessionDelay))
                        {
                            parameters["SessionDelay"] = sessionDelay;
                        }
                        else
                        {
                            parameters["SessionDelay"] = 0; // Default to no delay
                        }
                        break;
                }
                
                // Get advanced settings common to all trigger types
                // Allow on-demand execution
                if (AllowDemandCheckBox != null)
                {
                    parameters["AllowDemandStart"] = AllowDemandCheckBox.IsChecked ?? true;
                }
                
                // Run task if missed
                if (RunIfMissedCheckBox != null)
                {
                    parameters["RunIfMissed"] = RunIfMissedCheckBox.IsChecked ?? false;
                }
                
                // Restart on failure
                if (RestartCountComboBox?.SelectedItem != null && RestartIntervalComboBox?.SelectedItem != null)
                {
                    parameters["RestartOnFailure"] = true;
                    
                    if (int.TryParse(RestartCountComboBox.SelectedItem.ToString(), out int restartCount))
                    {
                        parameters["RestartCount"] = restartCount;
                    }
                    
                    if (int.TryParse(RestartIntervalComboBox.SelectedItem.ToString(), out int restartInterval))
                    {
                        parameters["RestartInterval"] = restartInterval;
                    }
                }
                
                // Execution time limit
                if (NoTimeLimitCheckBox != null && NoTimeLimitCheckBox.IsChecked == true)
                {
                    parameters["ExecutionTimeLimit"] = 0; // No time limit
                }
                else if (ExecutionHoursComboBox?.SelectedItem != null)
                {
                    if (int.TryParse(ExecutionHoursComboBox.SelectedItem.ToString(), out int executionHours))
                    {
                        parameters["ExecutionTimeLimit"] = executionHours;
                    }
                }
                
                // Multiple instances policy
                if (InstancePolicyComboBox?.SelectedItem is ComboBoxItem selectedPolicy)
                {
                    string policyText = selectedPolicy.Content.ToString();
                    int policyValue = 0; // Default is IgnoreNew
                    
                    switch (policyText)
                    {
                        case "Don't start a new instance":
                            policyValue = 0; // IgnoreNew
                            break;
                        case "Run a new instance in parallel":
                            policyValue = 1; // Parallel
                            break;
                        case "Queue a new instance":
                            policyValue = 2; // Queue 
                            break;
                        case "Stop the existing instance":
                            policyValue = 3; // StopExisting
                            break;
                    }
                    
                    parameters["MultipleInstances"] = policyValue;
                }
                
                // Run level settings
                if (RunWithHighestPrivilegesCheckBox != null)
                {
                    parameters["RunLevel"] = RunWithHighestPrivilegesCheckBox.IsChecked == true ? 1 : 0; // 1 = Highest, 0 = LUA
                }
                
                // Force stop if task doesn't stop on request
                if (ForceStopCheckBox != null)
                {
                    parameters["ForceStop"] = ForceStopCheckBox.IsChecked ?? true;
                }
                
                // Network condition settings
                if (NetworkConditionCheckBox != null)
                {
                    parameters["RunOnlyIfNetworkAvailable"] = NetworkConditionCheckBox.IsChecked ?? false;
                }
                
                // Battery settings
                if (StopOnBatteryCheckBox != null)
                {
                    parameters["StopIfGoingOnBatteries"] = StopOnBatteryCheckBox.IsChecked ?? true;
                }
                
                if (StartOnBatteryCheckBox != null)
                {
                    parameters["DisallowStartIfOnBatteries"] = !(StartOnBatteryCheckBox.IsChecked ?? false); // Invert check - UI is opposite of API
                }
                
                // Wake settings
                if (WakeToRunCheckBox != null)
                {
                    parameters["WakeToRun"] = WakeToRunCheckBox.IsChecked ?? false;
                }
                
                // Idle settings
                if (IdleSettingsCheckBox != null && IdleSettingsCheckBox.IsChecked == true)
                {
                    parameters["RunOnlyIfIdle"] = true;
                    
                    // Get idle duration from ComboBox
                    if (IdleMinutesComboBox?.SelectedItem != null)
                    {
                        if (int.TryParse(IdleMinutesComboBox.SelectedItem.ToString(), out int idleMinutes))
                        {
                            parameters["IdleMinutes"] = idleMinutes;
                        }
                    }
                    
                    // Get idle wait timeout from ComboBox
                    if (WaitTimeoutComboBox?.SelectedItem != null)
                    {
                        if (int.TryParse(WaitTimeoutComboBox.SelectedItem.ToString(), out int idleWaitHours))
                        {
                            parameters["IdleWaitTimeout"] = idleWaitHours * 60; // Convert hours to minutes
                        }
                    }
                    
                    if (StopOnIdleEndCheckBox != null)
                    {
                        parameters["IdleStopOnEnd"] = StopOnIdleEndCheckBox.IsChecked ?? false;
                    }
                    
                    if (RestartOnIdleCheckBox != null)
                    {
                        parameters["IdleRestartOnIdle"] = RestartOnIdleCheckBox.IsChecked ?? false;
                    }
                }
                
                // Delete when expired setting
                if (DeleteWhenExpiredCheckBox != null)
                {
                    parameters["DeleteWhenExpired"] = DeleteWhenExpiredCheckBox.IsChecked ?? false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting trigger parameters from UI: {ex.Message}");
                // Continue with defaults if any error occurs
            }
            
            return parameters;
        }

        // Helper method to extract server name/IP from a UNC path
        private string GetServerFromUncPath(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.IsUnc)
                {
                    return uri.Host; // Gets the server name or IP
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing UNC path '{path}': {ex.Message}");
            }
            return null;
        }

        // Helper method to determine the target server for credential lookup
        private string GetTargetServerFromPaths()
        {
            // Prioritize paths that are likely network paths
            string server = GetServerFromUncPath(_selectedScriptPath);
            if (!string.IsNullOrEmpty(server)) return server;

            server = GetServerFromUncPath(WorkingDirTextBox.Text);
            if (!string.IsNullOrEmpty(server)) return server;

            if (CreateWrapperCheckBox.IsChecked == true)
            {
                server = GetServerFromUncPath(WrapperLocationTextBox.Text);
                if (!string.IsNullOrEmpty(server)) return server;

                server = GetServerFromUncPath(_logDir); // Check log dir too
                if (!string.IsNullOrEmpty(server)) return server;
            }

            // Add other relevant paths if necessary

            return null; // No network path found or couldn't parse
        }

        private string GetUserPrincipal()
        {
            // Use the UserAccountComboBox defined in XAML
            if (UserAccountComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "SYSTEM":
                        return "SYSTEM";
                    case "Current User":
                        return Environment.UserDomainName + "\\" + Environment.UserName;
                    case "Specified User":
                        // *** UI CHANGE NEEDED ***
                        // Assumes you have added a TextBox named 'SpecifiedUserTextBox' in your XAML
                        // associated with the "Specified User" option.
                        // Ensure SpecifiedUserTextBox is checked for null if it's dynamically added/removed
                        if (SpecifiedUserTextBox == null)
                        {
                             MessageBox.Show("UI Error: SpecifiedUserTextBox not found. Please ensure it exists in the XAML.", "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                             return Environment.UserDomainName + "\\" + Environment.UserName; // Fallback
                        }

                        string specifiedUser = SpecifiedUserTextBox.Text.Trim(); // Read from the new TextBox
                        if (!string.IsNullOrWhiteSpace(specifiedUser))
                        {
                            // Basic validation: check if it contains a backslash (domain\user or computer\user)
                            if (!specifiedUser.Contains("\\"))
                            {
                                MessageBox.Show("Specified user must be in the format DOMAIN\\User or COMPUTER\\User.", "Invalid User Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                                // Return current user as a safe fallback, or handle error differently
                                return Environment.UserDomainName + "\\" + Environment.UserName;
                            }
                            return specifiedUser;
                        }
                        else
                        {
                            MessageBox.Show("Please enter the specific username when 'Specified User' is selected.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                            // Return current user as a safe fallback
                            return Environment.UserDomainName + "\\" + Environment.UserName;
                        }
                    default:
                        return Environment.UserDomainName + "\\" + Environment.UserName; // Default
                }
            }
            return Environment.UserDomainName + "\\" + Environment.UserName; // Default if nothing selected
        }

        private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only process this event if window is fully initialized
            if (!_isWindowInitialized)
            {
                return;
            }
            
            // Use the helper method to set panel visibility
            SetActionPanelVisibility();
        }
        
        private void AddActionButton_Click(object sender, RoutedEventArgs e)
        {
            // Only process this event if window is fully initialized
            if (!_isWindowInitialized)
            {
                return;
            }
            
            if (ActionTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                // Check if panels have been initialized
                if (RunProgramPanel == null || EmailActionPanel == null)
                {
                    MessageBox.Show("UI components are not fully initialized. Please try again.", "Component Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                switch (selectedItem.Content.ToString())
                {
                    case "Run Program":
                        AddRunProgramAction();
                        break;
                    case "Send Email":
                        AddSendEmailAction();
                        break;
                    case "Display Message":
                        AddDisplayMessageAction();
                        break;
                }
                
                // Update summary to show added action
                UpdateTaskSummary();
            }
        }
        
        private void AddRunProgramAction()
        {
            try
            {
                // Create a new RunProgramAction from form data
                var action = new RunProgramAction
                {
                    ProgramPath = _selectedScriptPath,
                    Arguments = ScriptArgumentsTextBox.Text.Trim(),
                    WorkingDirectory = WorkingDirTextBox.Text.Trim(),
                    CreateWrapper = CreateWrapperCheckBox.IsChecked ?? false,
                    WrapperLocation = WrapperLocationTextBox.Text.Trim()
                };
                
                // Add to the actions list
                _taskActions.Add(action);
                
                MessageBox.Show($"Added action: {action.GetSummary()}", "Action Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding program action: {ex.Message}", "Action Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void AddSendEmailAction()
        {
            try
            {
                // Validate email fields
                if (string.IsNullOrWhiteSpace(EmailFromTextBox.Text) ||
                    string.IsNullOrWhiteSpace(EmailToTextBox.Text) ||
                    string.IsNullOrWhiteSpace(SmtpServerTextBox.Text))
                {
                    MessageBox.Show("From email, To email, and SMTP server are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Attempt to parse port
                if (!int.TryParse(SmtpPortTextBox.Text, out int smtpPort))
                {
                    MessageBox.Show("SMTP port must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create a new SendEmailAction from form data
                var action = new SendEmailAction
                {
                    From = EmailFromTextBox.Text.Trim(),
                    To = EmailToTextBox.Text.Trim(),
                    Subject = EmailSubjectTextBox.Text.Trim(),
                    Body = EmailMessageTextBox.Text.Trim(),
                    SmtpServer = SmtpServerTextBox.Text.Trim(),
                    SmtpPort = smtpPort,
                    UseSSL = SmtpSecureCheckBox.IsChecked ?? true
                };
                
                // Add to the actions list
                _taskActions.Add(action);
                
                MessageBox.Show($"Added action: {action.GetSummary()}", "Action Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding email action: {ex.Message}", "Action Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AddDisplayMessageAction()
        {
            try
            {
                // Validate message fields
                if (string.IsNullOrWhiteSpace(MessageTitleTextBox.Text) ||
                    string.IsNullOrWhiteSpace(MessageTextTextBox.Text))
                {
                    MessageBox.Show("Message title and message text are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create a display message action class
                // We'll need to add this class first
                var action = new DisplayMessageAction
                {
                    Title = MessageTitleTextBox.Text.Trim(),
                    Message = MessageTextTextBox.Text.Trim()
                };
                
                // Add to actions list
                _taskActions.Add(action);
                
                MessageBox.Show($"Added action: {action.GetSummary()}", "Action Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding display message action: {ex.Message}", "Action Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    } // End partial class MainWindow

    // Display message action class
    internal class DisplayMessageAction : MainWindow.TaskAction
    {
        public override string ActionType => "Display Message";
        public string Title { get; set; }
        public string Message { get; set; }

        public override string GetSummary()
        {
            return $"Display Message: {Title}";
        }
    }

    // Simple class to hold script details for the ListView
    public class ScriptItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public double SizeKB { get; set; }
    }
} // End namespace