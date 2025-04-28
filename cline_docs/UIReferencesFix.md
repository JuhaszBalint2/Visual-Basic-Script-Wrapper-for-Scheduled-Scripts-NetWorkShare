# UI References Fix

## Issue Description
Several UI element reference errors were occurring in the application:

1. `The name 'InstancePolicyComboBox' does not exist in the current context` - In MainWindow.xaml.cs
2. `The name 'RunLevelHighestCheckBox' does not exist in the current context` - In MainWindow.xaml.cs
3. `The name "EqualityToVisibilityConverter" does not exist in the namespace "clr-namespace:ScriptSchedulerApp"` - In MainWindow.xaml

## Solution Implemented

### 1. InstancePolicyComboBox Fix
The ComboBox in the Advanced Settings section was missing its Name attribute in the XAML.

```xml
<!-- Before -->
<ComboBox Grid.Column="1" 
          Style="{StaticResource FluentComboBoxStyle}"
          SelectedIndex="0">
```

```xml
<!-- After -->
<ComboBox Grid.Column="1"
          Name="InstancePolicyComboBox" 
          Style="{StaticResource FluentComboBoxStyle}"
          SelectedIndex="0">
```

### 2. RunLevelHighestCheckBox Fix
The code in MainWindow.xaml.cs was looking for a control named `RunLevelHighestCheckBox`, but the actual control in the XAML was named `RunWithHighestPrivilegesCheckBox`. Rather than renaming in the XAML (which might cause other issues), we modified the code to use the correct name.

```csharp
// Before
summaryBuilder.AppendLine($"Highest Privileges: {RunLevelHighestCheckBox.IsChecked}");
bool highestPrivileges = RunLevelHighestCheckBox.IsChecked ?? false;
```

```csharp
// After
summaryBuilder.AppendLine($"Highest Privileges: {RunWithHighestPrivilegesCheckBox.IsChecked}");
bool highestPrivileges = RunWithHighestPrivilegesCheckBox.IsChecked ?? false;
```

### 3. EqualityToVisibilityConverter Fix
The converter class was properly defined in the `ScriptSchedulerApp` namespace, but we needed to ensure it was correctly referenced in the XAML. We added a separate namespace alias to clearly reference it.

```xml
<!-- Before -->
<Window x:Class="ScriptSchedulerApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:ScriptSchedulerApp"
        mc:Ignorable="d"
        ...
```

```xml
<!-- After -->
<Window x:Class="ScriptSchedulerApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:ScriptSchedulerApp"
        xmlns:converters="clr-namespace:ScriptSchedulerApp"
        mc:Ignorable="d"
        ...
```

And updated the converter reference:

```xml
<!-- Before -->
<local:EqualityToVisibilityConverter x:Key="EqualToVisibilityConverter"/>
```

```xml
<!-- After -->
<converters:EqualityToVisibilityConverter x:Key="EqualToVisibilityConverter"/>
```

## Verification
After these changes, all reference errors should be resolved. The application should now compile and run correctly without these specific UI reference errors.
