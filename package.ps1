param (
	[switch]$NoArchive,
	[string]$OutputDirectory = $PSScriptRoot
)

Set-Location "$PSScriptRoot"
$VERSION = (Select-String -Pattern '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>' -Path $PSScriptRoot/*/*.csproj).Matches.Groups[1]
$modName = (Select-String -Pattern '<AssemblyName>(.*)</AssemblyName>' -Path $PSScriptRoot/*/*.csproj).Matches.Groups[1]

Write-Host "Packaging $modName version $VERSION"

$DistDir = "$OutputDirectory/dist"
if ($NoArchive)
{
	$ZipWorkDir = "$OutputDirectory"
}
else
{
	$ZipWorkDir = "$DistDir/tmp"
}

$ZipRootDir = "$ZipWorkDir/$modName"
$LicenseFile = "LICENSE"
$AssemblyFiles = "build/*.*"

$null = New-Item "$ZipRootDir" -ItemType Directory -Force
Copy-Item -Force -Path "$LicenseFile", "$AssemblyFiles" -Destination "$ZipRootDir"
$null = New-Item "${ZipRootDir}\info.json" -Force -ItemType File -Value (@"
{
	"Id": "${modname}",
	"DisplayName": "${modname}",
	"Author": "Greeny",
	"Version": "${VERSION}",
	"AssemblyName": "${modname}.dll",
	"EntryMethod": "${modname}.Main.Load",
	"HomePage": "https://www.nexusmods.com/derailvalley/mods/687",
	"Repository": "https://raw.githubusercontent.com/Matthew-J-Green/dvDirectInput/master/repository.json"
}	
"@)

if (!$NoArchive)
{
	$archivePath = "${DistDir}/${modname}_v$VERSION.zip"
	Compress-Archive -Force -Path "$ZipRootDir" -DestinationPath "$archivePath"
	Remove-Item -LiteralPath "$ZipWorkDir" -Force -Recurse

	Write-Host "Archived at $archivePath"
}
else
{
	Write-Host "Copied Assemblies, License and created Info file to ${ZipRootDir}"
}
