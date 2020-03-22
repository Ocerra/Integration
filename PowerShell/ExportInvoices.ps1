<#
.SYNOPSIS
    Export invoices from Ocerra to XML on the local disk
.DESCRIPTION
    When you execute this script it will prompt for password and retrieve the list of invoice from Ocerra using OData protocol. 
    You can hardcode password using:
    $user = "OcerraLogin"
    $pass = "super-strong-ocerra-password"
    $pair = "${user}:${pass}"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $base64 = [System.Convert]::ToBase64String($bytes)
    $basicAuthValue = "Basic $base64"
    $headers = @{ Authorization = $basicAuthValue }

    And use $headers instead of -Credential $cred for Invoke-RestMethod and Invoke-WebRequest

.INPUTS
    Logn name and password
.OUTPUTS
    The script writes invoice files to the sub-folder including the cusotm JSON format
.EXAMPLE
    .\ExportInvoices.ps1
.LINK
#>


function Get-ScriptDirectory {
    Split-Path $script:MyInvocation.MyCommand.Path
}

function GetTimestamp{
    $timeStampPath = $(Get-ScriptDirectory) + "\ExportTimestamp.txt"
    $timeStamp = if(!$(Test-Path $timeStampPath)) { $(Get-Date).AddMonths(-3).ToString($isoDate) } Else { (Get-Content $timeStampPath | select -First 1) }
    $timeStamp = [datetime]$timeStamp
    Set-Content $timeStampPath $(Get-Date).ToString($isoDate)
	return $timeStamp;
}

function ExportFiles {

    #Authenticate using creadentials
    #Get-Credential –Credential (Get-Credential) | Export-Clixml "SecureCredentials.xml"
	#$Credentials = Import-Clixml "SecureCredentials.xml"
	#$user = $Credentials.UserName
	#$pass = $Credentials.GetNetworkCredential().Password
	
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

    $folder = Get-ScriptDirectory

    $timestamp = GetTimestamp
    
    $folderMonth = $folder + "\" + $timestamp.ToString("yyyy-MM") +"\"
    
    $dest = New-Item -ItemType Directory -Force -Path $folderMonth
	
    $exportMessage = "Invoices in folder $($folderMonth)"

    Write-Host $exportMessage

    $odataRequest = 'https://app.ocerra.com/odata/VoucherHeader?' + 
		'$skip=0&' + 
		'$top=500&' + 
		'$orderby=CreatedDate%20desc&' + 
		'$count=true&' + 
		'$filter=IsActive%20eq%20true&UpdatedDate%20ge%20' + $timeStamp.ToString("yyyy-MM-ddTHH:mm:ss") + '&' + 
		'$expand=Vendor,VoucherLines,Workflow($expand=WorkflowState),Document($expand=DocumentType,StoredFile)'

    #retrieve invoices using OData qery
    $allInvoices = Invoke-RestMethod -Method Get -Uri $odataRequest -Headers $headers        

    [System.Collections.ArrayList]$invoices = @();

    #convert invoices to a custom format
    ForEach($invoice In $allInvoices.value){
        $invoiceNumber = If($invoice.number) { $invoice.number } Else { 'Unknown' }
        $fileName = $invoiceNumber + " - " + $invoice.document.storedFile.originalName;
        $fileId = $invoice.document.storedFile.storedFileId;
        
        [System.Collections.ArrayList]$invoiceLines = @();
        ForEach($invoiceLine in $invoice.voucherLines){
            $invoiceLineOrder = $invoiceLines.Add(@{
                "Description" = $invoiceLine.description;
                "Net" = $invoiceLine.net;
            })
        }
        
        $invoiceOrder = $invoices.Add(@{
            "Number" = $invoice.number;
            "Supplier" = $invoice.vendor.name;
            "Total" = $invoice.gross;
            "Net" = $invoice.net;
            "Tax" = $invoice.tax;
            "DueDate" = $invoice.dueDate;
            "Lines" = $invoiceLines;
        });

        $url = "https://app.ocerra.com/api/Files/Download?storedFileId=$($fileId)"
        $fileHandler = Invoke-WebRequest `
            -Uri $url `
            -OutFile "$($folderMonth)\$($fileName)" `
            -Headers $headers

        Write-Host "Invoice file was saved $($fileName)"
    }

	$invoicesFileName = "$($folderMonth)\Invoices at " + $timeStamp.ToString("yyyy-MM-dd HHmmss") + ".json"
    $invoices | ConvertTo-Json -Depth 5 | Out-File $invoicesFileName -Encoding ASCII

    Write-Host "Export complete"
}

ExportFiles