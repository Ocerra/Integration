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

function ExportFiles {

    #Authenticate using creadentials
    #$cred = Get-Credential

    #authenticate using hardcoded Login/Pwd
    #we do not reccomend storing open login or password, use encoded values instead.
    $user = ""
    $pass = ""
    $pair = "${user}:${pass}"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $base64 = [System.Convert]::ToBase64String($bytes)
    $basicAuthValue = "Basic $base64"
    $headers = @{ Authorization = $basicAuthValue }

    $folder = Get-ScriptDirectory

    $date = Get-Date
    
    $folderMonth = $folder + "\" + $date.ToString("yyyy-MM") +"\"
    
    $dest = New-Item -ItemType Directory -Force -Path $folderMonth
	
    $exportMessage = "Invoices in folder $($folderMonth)"

    Write-Host $exportMessage

    #retrieve invoices using OData qery
    $allInvoices = Invoke-RestMethod `
        -Method Get `
        -Uri 'https://app.ocerra.com/odata/VoucherHeader?$skip=0&$top=3&$orderby=CreatedDate%20desc&$count=true&$filter=IsActive%20eq%20true&$expand=Vendor,VoucherLines,Workflow($expand=WorkflowState),Document($expand=DocumentType,StoredFile($expand=InverseParentStoredFile))' `
        -Headers $headers
        #-Credential $cred

    [System.Collections.ArrayList]$invoices = @();

    #convert invoices to a custom format
    ForEach($invoice In $allInvoices.value){
        $fileName = $invoice.Document.StoredFile.OriginalName;
        $fileId = $invoice.Document.StoredFile.StoredFileId;
        
        [System.Collections.ArrayList]$invoiceLines = @();
        ForEach($invoiceLine in $invoice.VoucherLines){
            $invoiceLineOrder = $invoiceLines.Add(@{
                "Description" = $invoiceLine.Description;
                "Net" = $invoiceLine.Net;
            })
        }
        
        $invoiceOrder = $invoices.Add(@{
            "Number" = $invoice.Number;
            "Supplier" = $invoice.Vendor.Name;
            "Total" = $invoice.Gross;
            "Net" = $invoice.Net;
            "Tax" = $invoice.Tax;
            "DueDate" = $invoice.DueDate;
            "Lines" = $invoiceLines;
        });

        $url = "https://app.ocerra.com/api/Files/Download?storedFileId=$($fileId)"
        $fileHandler = Invoke-WebRequest `
            -Uri $url `
            -OutFile "$($folderMonth)\$($fileName)" `
            -Headers $headers
            #-Credential $cred

        Write-Host "Invoice file was saved $($fileName)"
    }

    $invoices | ConvertTo-Json -Depth 5 | Out-File "$($folderMonth)\Top 10 Invoices.json" -Encoding ASCII

    Write-Host "Export complete"
}

ExportFiles