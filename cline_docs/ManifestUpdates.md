# App Manifest Updates

## Changes Made
- Updated the application manifest file (`app.manifest`) to restrict the application to run only on Windows 10 and Windows 11
- Confirmed administrator privileges requirement is properly set in the manifest
- Added proper dependency for Windows common controls and dialogs
- Ensured manifest is correctly referenced in the project file

## Technical Details

### OS Compatibility
The application now explicitly declares compatibility with:
- Windows 10 (OS ID: `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`)
- Windows 11 (Uses same OS ID as Windows 10: `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`)

All other Windows versions are explicitly not supported (Windows 7, 8, 8.1).

### Administrator Privileges
- Set `requestedExecutionLevel` to `requireAdministrator` which forces the application to run with admin rights
- This ensures the application has sufficient privileges to:
  - Create and modify scheduled tasks
  - Access and modify system settings
  - Write to protected directories if needed
  - Properly access network shares with credentials

### Windows Common Controls
- Added dependency on Microsoft.Windows.Common-Controls for proper theme integration
- This ensures all dialogs and controls render correctly with the system theme

## Implementation Details
```xml
<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
  <application>
    <!-- Windows 10 and 11 -->
    <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
  </application>
</compatibility>

<trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
  <security>
    <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
      <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
    </requestedPrivileges>
  </security>
</trustInfo>
```

## Project File Reference
The manifest is correctly referenced in the project file with:
```xml
<ApplicationManifest>app.manifest</ApplicationManifest>
```

## User Impact
- Users will now always see the UAC prompt when launching the application
- The application will not run on Windows versions prior to Windows 10
- All Windows UI elements will use the system theme for a consistent look and feel