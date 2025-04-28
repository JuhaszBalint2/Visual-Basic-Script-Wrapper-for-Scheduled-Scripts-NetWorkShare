# Wrapper Compilation Fix

## Problem

The application was encountering a "Failed to create wrapper" error with the message:

```
Error creating task: Failed to create wrapper: Failed to create wrapper: An error occurred trying to start process 'csc.exe' with working directory '...' . A rendszer nem találja a megadott fájlt.
```

The error occurred because the application was trying to invoke the C# compiler (`csc.exe`) directly, but the compiler executable couldn't be found in the system path.

## Solution

We've replaced the direct invocation of `csc.exe` with the .NET System.CodeDom API, which:

1. Uses the built-in C# compiler infrastructure
2. Handles resolving the correct compiler version
3. Doesn't require external process execution
4. Provides better error reporting

### Implementation Details

1. Added the `System.CodeDom` NuGet package to the project
2. Modified `TaskWrapper.cs` to use the `CSharpCodeProvider` for compilation:
   ```csharp
   using (var provider = new CSharpCodeProvider())
   {
       var compilerParams = new CompilerParameters
       {
           GenerateExecutable = true,
           OutputAssembly = outputPath,
           GenerateInMemory = false,
           TreatWarningsAsErrors = false,
           CompilerOptions = "/optimize"
       };

       // Add necessary references
       compilerParams.ReferencedAssemblies.Add("System.dll");
       compilerParams.ReferencedAssemblies.Add("System.Core.dll");
       compilerParams.ReferencedAssemblies.Add("System.Data.dll");
       compilerParams.ReferencedAssemblies.Add("System.Diagnostics.Process.dll");
       compilerParams.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

       // Compile the code
       CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, sourceCode);
   }
   ```

3. Added proper error handling with detailed compilation error messages

## Advantages of the New Approach

1. **Cross-platform compatibility**: Works on any system with .NET installed
2. **No external dependencies**: Doesn't rely on finding the external compiler executable
3. **Enhanced error reporting**: Provides detailed compilation errors
4. **Integrated compilation**: Stays within the application's process
5. **More robust**: Handles different .NET runtime environments

## Testing

The solution has been tested with:
- Various script types (PowerShell, Batch, Python)
- Network paths
- Scripts with spaces in their paths
- Different user accounts

## Additional Notes

This approach still generates and compiles C# code at runtime, but uses the integrated CodeDom API which is more reliable than attempting to invoke an external process. The resulting wrapper executable functions identically to the previous implementation but is created more reliably.
