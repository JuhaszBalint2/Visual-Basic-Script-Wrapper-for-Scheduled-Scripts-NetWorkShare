# C# Code Structure Fixes Applied

## Issue Description

Multiple C# files contained serious structural issues that caused compilation errors:

1. **Incorrect Code Structure in MainWindow.xaml.cs**:
   - Method definitions appeared before namespace and class declarations
   - `using` statements appeared after code had already begun
   - Several `private` modifiers were applied incorrectly

2. **Incorrect Code Structure in TaskSchedulerExtensions.cs**:
   - The AddEmailAction method was defined before the namespace and class declarations
   - `using` statements appeared after code had already begun
   - A `public` modifier was applied incorrectly outside of a class context

3. **Specific Error Messages**:
   - "A using clause must precede all other elements defined in the namespace except extern alias declarations"
   - "The modifier 'private' is not valid for this item"
   - "The modifier 'public' is not valid for this item"

## Fixes Applied

### MainWindow.xaml.cs Fixes
1. **Reorganized File Structure**:
   - Moved all `using` statements to the top of the file
   - Ensured namespace and class declarations appeared before any method definitions
   - Placed all methods within the MainWindow class

2. **Fixed Methods**:
   - Relocated ActionTypeComboBox_SelectionChanged, AddActionButton_Click, AddRunProgramAction and AddSendEmailAction methods to their proper place within the MainWindow class
   - Ensured proper visibility modifiers

### TaskSchedulerExtensions.cs Fixes
1. **Reorganized File Structure**:
   - Moved all `using` statements to the top of the file
   - Ensured namespace and class declarations appeared before any method definitions

2. **Fixed Method Placement**:
   - Relocated AddEmailAction method from the beginning of the file to its proper place within the TaskSchedulerExtensions class
   - Fixed improper `public` modifier that was outside of any class context
   - Maintained all method parameters and functionality while fixing structure

## Results

The application now compiles properly without structure errors. The code follows proper C# organization with:
- `using` statements at the top
- Namespace and class declarations in the correct order
- Methods properly placed within their class context
- Appropriate visibility modifiers applied

## Best Practices Reinforced

1. Always maintain proper C# file structure:
   - `using` statements at the top
   - Namespace declaration
   - Class declaration
   - Fields and properties
   - Constructor
   - Methods

2. Ensure proper encapsulation:
   - Use appropriate access modifiers (public, private, etc.)
   - Keep method definitions within their proper class context

3. Use consistent code organization:
   - Group related methods together
   - Maintain logical ordering of code elements
