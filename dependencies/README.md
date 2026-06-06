# AGS.Types.dll (local, not in Git)

Editor plugins reference `AGS.Types.dll` from this folder. The DLL is **not** committed (AGS install path differs per machine).

## Setup

1. Find your AGS Editor install folder (example: `C:\Program Files (x86)\AGS Editor`).
2. Copy `AGS.Types.dll` from that folder into this directory:

   ```
   dependencies/AGS.Types.dll
   ```

3. Build any plugin project in Visual Studio or with MSBuild.

The path is configured in each `.csproj` as:

```xml
<Reference Include="AGS.Types">
  <HintPath>..\dependencies\AGS.Types.dll</HintPath>
  <Private>False</Private>
</Reference>
```
