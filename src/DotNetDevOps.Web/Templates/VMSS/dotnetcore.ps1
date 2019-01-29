#
# MyCustomScriptExtension.ps1
#
param (
  $vmAdminUsername,
  $vmAdminPassword
)
 
$password =  ConvertTo-SecureString $vmAdminPassword -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential("$env:USERDOMAIN\$vmAdminUsername", $password)
 
Write-Verbose -Verbose "Entering Custom Script Extension..."
 
Invoke-Command -Credential $credential -ComputerName $env:COMPUTERNAME -ArgumentList $PSScriptRoot -ScriptBlock {
  param 
  (
    $workingDir,
	[string]$dotnetInstallDir = 'C:\dotnet',
    [string]$dotnetVersion = 'Latest',
    [string]$dotnetChannel = '2.2'
  )
 
  #################################
  # Elevated custom scripts go here 
  #################################
  Write-Verbose -Verbose "Entering Elevated Custom Script Commands..."

# Set system path to dotnet installation
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";$dotnetInstallDir", [EnvironmentVariableTarget]::Machine);

# Force use of TLS12 to download script
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;

# Download and run dotnet-install.ps1
&([scriptblock]::Create((Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -UseBasicParsing))) -Channel $dotnetChannel -Version $dotnetVersion -InstallDir $dotnetInstallDir -NoPath;

}

