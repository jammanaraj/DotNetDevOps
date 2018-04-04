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
    $workingDir
  )
 
  #################################
  # Elevated custom scripts go here 
  #################################
  Write-Verbose -Verbose "Entering Elevated Custom Script Commands..."

stop-service docker
& "C:/Program Files/Docker/dockerd.exe" --unregister-service
stop-process -name dockerd -Force  -ErrorAction SilentlyContinue
rm "C:/Program Files/Docker/docker.exe" -Force
rm "C:/Program Files/Docker/dockerd.exe" -Force
rm "C:/Program Files/Docker/metadata.json" -Force
[Environment]::SetEnvironmentVariable("LCOW_SUPPORTED", "1", "Machine")
Install-Module DockerProvider -Force
Install-Package Docker -ProviderName DockerProvider -RequiredVersion preview -Force
mkdir d:/docker
$Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $False
[System.IO.File]::WriteAllLines("$env:programdata/docker/config/daemon.json", '{"graph":"d:/docker"}', $Utf8NoBomEncoding)
restart-service docker

}

