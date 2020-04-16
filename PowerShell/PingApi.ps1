<#
.SYNOPSIS
    Ping API Interface using the PowerShell 
.DESCRIPTION    
	Quick ping API powershell script for Authentication and Authorization tests
.INPUTS
    Logn name and password
.OUTPUTS
    Console text
.EXAMPLE
    .\PingApi.ps1
.LINK
#>


function Get-ScriptDirectory {
    Split-Path $script:MyInvocation.MyCommand.Path
}

function PingOcerraApi {

    $isoDate = "yyyy-MM-dd HH:mm:ss"
	$settingsPath = $(Get-ScriptDirectory) + "\OcerraSettings.txt"
	$settings = (Get-Content $settingsPath) -Join "`n" | ConvertFrom-Json
	$user = $settings.username
    $pass = $settings.password
    $pair = "${user}:${pass}"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $base64 = [System.Convert]::ToBase64String($bytes)
    $basicAuthValue = "Basic $base64"
    $headers = @{ Authorization = $basicAuthValue }

    $request = 'https://app.ocerra.com/api/Client/Current'

    #retrieve invoices using OData qery
    $currentClient = Invoke-RestMethod -Method Get -Uri $request -Headers $headers        

    Write-Host "Current Client $($currentClient.Name)"    
}

PingOcerraApi