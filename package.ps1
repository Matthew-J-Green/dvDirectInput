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
	"ManagerVersion": "0.27.3",
	"HomePage": "https://www.nexusmods.com/derailvalley/mods/687",
	"Repository": "https://raw.githubusercontent.com/Matthew-J-Green/dvDirectInput/main/repostiory.json"
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

$repositoryPath = "$PSScriptRoot/repository.json"
$latestVersion = (Select-String -Pattern '"Version": "([0-9]+\.[0-9]+\.[0-9]+)"' -Path $repositoryPath).Matches.Groups[1]
if ($latestVersion.Value -ne $VERSION.Value)
{
	Write-Host "New version detected ($VERSION is not $latestVersion). Updating $repositoryPath"
	$repository = Get-Content $repositoryPath
	$updatedRepository = $repository[0..2] + "        {`"Id`": `"${modname}`", `"Version`": `"${VERSION}`", `"DownloadUrl`": `"https://github.com/Matthew-J-Green/dvDirectInput/releases/download/v${VERSION}/${modName}_v${VERSION}.zip`"},"+ $repository[3..($repository.Count - 1)]
	Set-Content $repositoryPath -Value $updatedRepository
}
