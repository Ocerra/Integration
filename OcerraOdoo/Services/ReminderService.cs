﻿using Microsoft.OData.Client;
using OcerraOdoo.OcerraOData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OcerraOdoo.Services
{
    public class ReminderService
    {
        private readonly OcerraClient ocerraClient;
        private OdataProxy odata;
        private readonly EmailSender emailSender;

        public bool Initialized { get; set; }

        public ReminderService(OcerraClient ocerraClient, OdataProxy odata, EmailSender emailSender)
        {
            this.ocerraClient = ocerraClient;
            this.odata = odata;
            this.emailSender = emailSender;
        }

        public async Task<int> RemindPurchasersByIds(Guid[] voucherHeaderIds) {
            int result = 0;
            try
            {
                foreach (var voucherHeaderId in voucherHeaderIds)
                {
                    var query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)odata.VoucherHeader.Expand("Vendor,PurchaseOrderHeader,Document");
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.VoucherHeaderId == voucherHeaderId);
                    
                    /*query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query
                        .AddQueryOption("$filter", $"VoucherHeaderId eq {voucherHeaderId}"); //.Where(vh => vh.VoucherHeaderId == voucherHeaderId);*/
                    
                    //Export invoices one by one
                    var ocerraInvoice = query.Take(1).FirstOrDefault();

                    if (ocerraInvoice != null)
                    {
                        var reminded = await SendEmail(ocerraInvoice);
                        if (reminded) {
                            result++;
                            await ocerraClient.ApiVoucherHeaderByIdPatchAsync(ocerraInvoice.VoucherHeaderId.Value,new List<Operation> { new Operation
                                {
                                    Path = "/Extra5",
                                    Op = "replace",
                                    Value = "Yes"
                                } 
                            });
                        }
                            
                    }
                    else
                    {
                        throw new Exception($"This invoice {voucherHeaderId} is not found");
                    }
                }
            }
            catch (Exception ex)
            {
                ex.LogError("On remind PO");
            }
            return result;
        }

        private async Task<bool> SendEmail(ODataClient.Proxies.VoucherHeader voucherHeader) {

            if (voucherHeader?.PurchaseOrderHeader?.PurchaserEmail == null || voucherHeader.PurchaseOrderHeader.PurchaserEmail.Split('@').Length > 2) return false;

            string filePath = null;

            var document = voucherHeader.Document; //odata.Document.Where(d => d.DocumentId == voucherHeader.DocumentId).FirstOrDefault(); //.Expand(d => d.StoredFile) - add original file name

            var response = await ocerraClient.ApiFilesDownloadGetAsync(document.StoredFileId);

            var folderPath = @"C:\Applications\OceaniaOdoo\FileAttachments";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{voucherHeader.Vendor?.Name ?? "unknown" }-{voucherHeader.Number ?? "unknown"}.pdf";
            var regex = new Regex(@"[^0-9A-Za-z\.\-_]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            fileName = regex.Replace(fileName, " ");
            filePath = Path.Combine(folderPath, fileName);

            using (var fileStream = File.Create(filePath))
            {
                response.Stream.CopyTo(fileStream);
            }
            

            var emailContent = $@"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Strict//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
 <head>
   <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
   
   
         <style type=""text/css"">
          .ExternalClass{{width:100%}}.ExternalClass,.ExternalClass p,.ExternalClass span,.ExternalClass font,.ExternalClass td,.ExternalClass div{{line-height:150%}}a{{text-decoration:none}}@media screen and (max-width: 600px){{table.row th.col-lg-1,table.row th.col-lg-2,table.row th.col-lg-3,table.row th.col-lg-4,table.row th.col-lg-5,table.row th.col-lg-6,table.row th.col-lg-7,table.row th.col-lg-8,table.row th.col-lg-9,table.row th.col-lg-10,table.row th.col-lg-11,table.row th.col-lg-12{{display:block;width:100% !important}}.d-mobile{{display:block !important}}.d-desktop{{display:none !important}}.w-lg-25{{width:auto !important}}.w-lg-25>tbody>tr>td{{width:auto !important}}.w-lg-50{{width:auto !important}}.w-lg-50>tbody>tr>td{{width:auto !important}}.w-lg-75{{width:auto !important}}.w-lg-75>tbody>tr>td{{width:auto !important}}.w-lg-100{{width:auto !important}}.w-lg-100>tbody>tr>td{{width:auto !important}}.w-lg-auto{{width:auto !important}}.w-lg-auto>tbody>tr>td{{width:auto !important}}.w-25{{width:25% !important}}.w-25>tbody>tr>td{{width:25% !important}}.w-50{{width:50% !important}}.w-50>tbody>tr>td{{width:50% !important}}.w-75{{width:75% !important}}.w-75>tbody>tr>td{{width:75% !important}}.w-100{{width:100% !important}}.w-100>tbody>tr>td{{width:100% !important}}.w-auto{{width:auto !important}}.w-auto>tbody>tr>td{{width:auto !important}}.p-lg-0>tbody>tr>td{{padding:0 !important}}.pt-lg-0>tbody>tr>td,.py-lg-0>tbody>tr>td{{padding-top:0 !important}}.pr-lg-0>tbody>tr>td,.px-lg-0>tbody>tr>td{{padding-right:0 !important}}.pb-lg-0>tbody>tr>td,.py-lg-0>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-0>tbody>tr>td,.px-lg-0>tbody>tr>td{{padding-left:0 !important}}.p-lg-1>tbody>tr>td{{padding:0 !important}}.pt-lg-1>tbody>tr>td,.py-lg-1>tbody>tr>td{{padding-top:0 !important}}.pr-lg-1>tbody>tr>td,.px-lg-1>tbody>tr>td{{padding-right:0 !important}}.pb-lg-1>tbody>tr>td,.py-lg-1>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-1>tbody>tr>td,.px-lg-1>tbody>tr>td{{padding-left:0 !important}}.p-lg-2>tbody>tr>td{{padding:0 !important}}.pt-lg-2>tbody>tr>td,.py-lg-2>tbody>tr>td{{padding-top:0 !important}}.pr-lg-2>tbody>tr>td,.px-lg-2>tbody>tr>td{{padding-right:0 !important}}.pb-lg-2>tbody>tr>td,.py-lg-2>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-2>tbody>tr>td,.px-lg-2>tbody>tr>td{{padding-left:0 !important}}.p-lg-3>tbody>tr>td{{padding:0 !important}}.pt-lg-3>tbody>tr>td,.py-lg-3>tbody>tr>td{{padding-top:0 !important}}.pr-lg-3>tbody>tr>td,.px-lg-3>tbody>tr>td{{padding-right:0 !important}}.pb-lg-3>tbody>tr>td,.py-lg-3>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-3>tbody>tr>td,.px-lg-3>tbody>tr>td{{padding-left:0 !important}}.p-lg-4>tbody>tr>td{{padding:0 !important}}.pt-lg-4>tbody>tr>td,.py-lg-4>tbody>tr>td{{padding-top:0 !important}}.pr-lg-4>tbody>tr>td,.px-lg-4>tbody>tr>td{{padding-right:0 !important}}.pb-lg-4>tbody>tr>td,.py-lg-4>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-4>tbody>tr>td,.px-lg-4>tbody>tr>td{{padding-left:0 !important}}.p-lg-5>tbody>tr>td{{padding:0 !important}}.pt-lg-5>tbody>tr>td,.py-lg-5>tbody>tr>td{{padding-top:0 !important}}.pr-lg-5>tbody>tr>td,.px-lg-5>tbody>tr>td{{padding-right:0 !important}}.pb-lg-5>tbody>tr>td,.py-lg-5>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-5>tbody>tr>td,.px-lg-5>tbody>tr>td{{padding-left:0 !important}}.p-0>tbody>tr>td{{padding:0 !important}}.pt-0>tbody>tr>td,.py-0>tbody>tr>td{{padding-top:0 !important}}.pr-0>tbody>tr>td,.px-0>tbody>tr>td{{padding-right:0 !important}}.pb-0>tbody>tr>td,.py-0>tbody>tr>td{{padding-bottom:0 !important}}.pl-0>tbody>tr>td,.px-0>tbody>tr>td{{padding-left:0 !important}}.p-1>tbody>tr>td{{padding:4px !important}}.pt-1>tbody>tr>td,.py-1>tbody>tr>td{{padding-top:4px !important}}.pr-1>tbody>tr>td,.px-1>tbody>tr>td{{padding-right:4px !important}}.pb-1>tbody>tr>td,.py-1>tbody>tr>td{{padding-bottom:4px !important}}.pl-1>tbody>tr>td,.px-1>tbody>tr>td{{padding-left:4px !important}}.p-2>tbody>tr>td{{padding:8px !important}}.pt-2>tbody>tr>td,.py-2>tbody>tr>td{{padding-top:8px !important}}.pr-2>tbody>tr>td,.px-2>tbody>tr>td{{padding-right:8px !important}}.pb-2>tbody>tr>td,.py-2>tbody>tr>td{{padding-bottom:8px !important}}.pl-2>tbody>tr>td,.px-2>tbody>tr>td{{padding-left:8px !important}}.p-3>tbody>tr>td{{padding:16px !important}}.pt-3>tbody>tr>td,.py-3>tbody>tr>td{{padding-top:16px !important}}.pr-3>tbody>tr>td,.px-3>tbody>tr>td{{padding-right:16px !important}}.pb-3>tbody>tr>td,.py-3>tbody>tr>td{{padding-bottom:16px !important}}.pl-3>tbody>tr>td,.px-3>tbody>tr>td{{padding-left:16px !important}}.p-4>tbody>tr>td{{padding:24px !important}}.pt-4>tbody>tr>td,.py-4>tbody>tr>td{{padding-top:24px !important}}.pr-4>tbody>tr>td,.px-4>tbody>tr>td{{padding-right:24px !important}}.pb-4>tbody>tr>td,.py-4>tbody>tr>td{{padding-bottom:24px !important}}.pl-4>tbody>tr>td,.px-4>tbody>tr>td{{padding-left:24px !important}}.p-5>tbody>tr>td{{padding:48px !important}}.pt-5>tbody>tr>td,.py-5>tbody>tr>td{{padding-top:48px !important}}.pr-5>tbody>tr>td,.px-5>tbody>tr>td{{padding-right:48px !important}}.pb-5>tbody>tr>td,.py-5>tbody>tr>td{{padding-bottom:48px !important}}.pl-5>tbody>tr>td,.px-5>tbody>tr>td{{padding-left:48px !important}}.s-lg-1>tbody>tr>td,.s-lg-2>tbody>tr>td,.s-lg-3>tbody>tr>td,.s-lg-4>tbody>tr>td,.s-lg-5>tbody>tr>td{{font-size:0 !important;line-height:0 !important;height:0 !important}}.s-0>tbody>tr>td{{font-size:0 !important;line-height:0 !important;height:0 !important}}.s-1>tbody>tr>td{{font-size:4px !important;line-height:4px !important;height:4px !important}}.s-2>tbody>tr>td{{font-size:8px !important;line-height:8px !important;height:8px !important}}.s-3>tbody>tr>td{{font-size:16px !important;line-height:16px !important;height:16px !important}}.s-4>tbody>tr>td{{font-size:24px !important;line-height:24px !important;height:24px !important}}.s-5>tbody>tr>td{{font-size:48px !important;line-height:48px !important;height:48px !important}}}}@media yahoo{{.d-mobile{{display:none !important}}.d-desktop{{display:block !important}}.w-lg-25{{width:25% !important}}.w-lg-25>tbody>tr>td{{width:25% !important}}.w-lg-50{{width:50% !important}}.w-lg-50>tbody>tr>td{{width:50% !important}}.w-lg-75{{width:75% !important}}.w-lg-75>tbody>tr>td{{width:75% !important}}.w-lg-100{{width:100% !important}}.w-lg-100>tbody>tr>td{{width:100% !important}}.w-lg-auto{{width:auto !important}}.w-lg-auto>tbody>tr>td{{width:auto !important}}.p-lg-0>tbody>tr>td{{padding:0 !important}}.pt-lg-0>tbody>tr>td,.py-lg-0>tbody>tr>td{{padding-top:0 !important}}.pr-lg-0>tbody>tr>td,.px-lg-0>tbody>tr>td{{padding-right:0 !important}}.pb-lg-0>tbody>tr>td,.py-lg-0>tbody>tr>td{{padding-bottom:0 !important}}.pl-lg-0>tbody>tr>td,.px-lg-0>tbody>tr>td{{padding-left:0 !important}}.p-lg-1>tbody>tr>td{{padding:4px !important}}.pt-lg-1>tbody>tr>td,.py-lg-1>tbody>tr>td{{padding-top:4px !important}}.pr-lg-1>tbody>tr>td,.px-lg-1>tbody>tr>td{{padding-right:4px !important}}.pb-lg-1>tbody>tr>td,.py-lg-1>tbody>tr>td{{padding-bottom:4px !important}}.pl-lg-1>tbody>tr>td,.px-lg-1>tbody>tr>td{{padding-left:4px !important}}.p-lg-2>tbody>tr>td{{padding:8px !important}}.pt-lg-2>tbody>tr>td,.py-lg-2>tbody>tr>td{{padding-top:8px !important}}.pr-lg-2>tbody>tr>td,.px-lg-2>tbody>tr>td{{padding-right:8px !important}}.pb-lg-2>tbody>tr>td,.py-lg-2>tbody>tr>td{{padding-bottom:8px !important}}.pl-lg-2>tbody>tr>td,.px-lg-2>tbody>tr>td{{padding-left:8px !important}}.p-lg-3>tbody>tr>td{{padding:16px !important}}.pt-lg-3>tbody>tr>td,.py-lg-3>tbody>tr>td{{padding-top:16px !important}}.pr-lg-3>tbody>tr>td,.px-lg-3>tbody>tr>td{{padding-right:16px !important}}.pb-lg-3>tbody>tr>td,.py-lg-3>tbody>tr>td{{padding-bottom:16px !important}}.pl-lg-3>tbody>tr>td,.px-lg-3>tbody>tr>td{{padding-left:16px !important}}.p-lg-4>tbody>tr>td{{padding:24px !important}}.pt-lg-4>tbody>tr>td,.py-lg-4>tbody>tr>td{{padding-top:24px !important}}.pr-lg-4>tbody>tr>td,.px-lg-4>tbody>tr>td{{padding-right:24px !important}}.pb-lg-4>tbody>tr>td,.py-lg-4>tbody>tr>td{{padding-bottom:24px !important}}.pl-lg-4>tbody>tr>td,.px-lg-4>tbody>tr>td{{padding-left:24px !important}}.p-lg-5>tbody>tr>td{{padding:48px !important}}.pt-lg-5>tbody>tr>td,.py-lg-5>tbody>tr>td{{padding-top:48px !important}}.pr-lg-5>tbody>tr>td,.px-lg-5>tbody>tr>td{{padding-right:48px !important}}.pb-lg-5>tbody>tr>td,.py-lg-5>tbody>tr>td{{padding-bottom:48px !important}}.pl-lg-5>tbody>tr>td,.px-lg-5>tbody>tr>td{{padding-left:48px !important}}.s-lg-0>tbody>tr>td{{font-size:0 !important;line-height:0 !important;height:0 !important}}.s-lg-1>tbody>tr>td{{font-size:4px !important;line-height:4px !important;height:4px !important}}.s-lg-2>tbody>tr>td{{font-size:8px !important;line-height:8px !important;height:8px !important}}.s-lg-3>tbody>tr>td{{font-size:16px !important;line-height:16px !important;height:16px !important}}.s-lg-4>tbody>tr>td{{font-size:24px !important;line-height:24px !important;height:24px !important}}.s-lg-5>tbody>tr>td{{font-size:48px !important;line-height:48px !important;height:48px !important}}}}

        </style>
</head>
 <!-- Edit the code below this line -->
 <body style=""outline: 0; width: 100%; min-width: 100%; height: 100%; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; font-family: Helvetica, Arial, sans-serif; line-height: 24px; font-weight: normal; font-size: 16px; -moz-box-sizing: border-box; -webkit-box-sizing: border-box; box-sizing: border-box; margin: 0; padding: 0; border: 0;"">
<div class=""preview"" style=""display: none; max-height: 0px; overflow: hidden;"">
  Please, update Purchase Order status                                                                
</div>
<table valign=""top"" class=""bg-light body"" style=""outline: 0; width: 100%; min-width: 100%; height: 100%; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; font-family: Helvetica, Arial, sans-serif; line-height: 24px; font-weight: normal; font-size: 16px; -moz-box-sizing: border-box; -webkit-box-sizing: border-box; box-sizing: border-box; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; margin: 0; padding: 0; border: 0;"" bgcolor=""#f8f9fa"">
  <tbody>
    <tr>
      <td valign=""top"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; margin: 0;"" align=""left"" bgcolor=""#f8f9fa"">
        

<table class=""container"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%;"">
  <tbody>
    <tr>
      <td align=""center"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; margin: 0; padding: 0 16px;"">
        <!--[if (gte mso 9)|(IE)]>
          <table align=""center"">
            <tbody>
              <tr>
                <td width=""600"">
        <![endif]-->
        <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%; max-width: 600px; margin: 0 auto;"">
          <tbody>
            <tr>
              <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; margin: 0;"" align=""left"">
                
  <table class=""mx-auto"" align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; margin: 0 auto;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; margin: 0;"" align=""left"">
        <table class=""s-4 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""24"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 24px; width: 100%; height: 24px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>

<img class=""  "" width=""42"" height=""30"" src=""https://app.ocerra.com/assets/images/OcerraLogoLight.png"" style=""height: auto; line-height: 100%; outline: none; text-decoration: none; border: 0 none;""><table class=""s-3 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""16"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 16px; font-size: 16px; width: 100%; height: 16px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>


      </td>
    </tr>
  </tbody>
</table>


  <table class=""card "" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: separate !important; border-radius: 4px; width: 100%; overflow: hidden; border: 1px solid #dee2e6;"" bgcolor=""#ffffff"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; width: 100%; margin: 0;"" align=""left"">
        <div style=""border-top-width: 5px; border-top-color: #2fb87d; border-top-style: solid;"">
    <table class=""card-body"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; width: 100%; margin: 0; padding: 20px;"" align=""left"">
        <div>
      <h4 class=""text-center"" style=""margin-top: 0; margin-bottom: 0; font-weight: 500; color: inherit; vertical-align: baseline; font-size: 24px; line-height: 28.8px;"" align=""center"">Please, update purchase order status</h4>
      <h5 class=""text-muted text-center"" style=""margin-top: 0; margin-bottom: 0; font-weight: 500; color: #636c72; vertical-align: baseline; font-size: 20px; line-height: 24px;"" align=""center"">{DateTime.Now.ToString("D")}</h5>

      <div class=""hr "" style=""width: 100%; margin: 20px 0; border: 0;"">
  <table border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%;"">
    <tbody>
      <tr>
        <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-width: 1px; border-top-color: #dddddd; border-top-style: solid; height: 1px; width: 100%; margin: 0;"" align=""left""></td>
      </tr>
    </tbody>
  </table>
</div>


      <h5 class=""text-center"" style=""margin-top: 0; margin-bottom: 0; font-weight: 500; color: inherit; vertical-align: baseline; font-size: 20px; line-height: 24px;"" align=""center""><strong>Purchase Orders</strong></h5>
      <table class=""table"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%; max-width: 100%;"" bgcolor=""#ffffff"">
        <tbody>
          <tr>
            <td style=""border-top-width: 0; border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-color: #e9ecef; border-top-style: solid; margin: 0; padding: 12px;"" align=""left"" valign=""top"">Number: {voucherHeader?.PurchaseOrderHeader?.Number}</td>
            <td style=""border-top-width: 0; border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-color: #e9ecef; border-top-style: solid; margin: 0; padding: 12px;"" class=""text-right"" align=""right"" valign=""top"">{voucherHeader?.PurchaseOrderHeader?.Total}</td>
          </tr>
          <tr>
            <td style=""border-top-width: 0; border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-color: #e9ecef; border-top-style: solid; margin: 0; padding: 12px;"" align=""left"" valign=""top"">Supplier: {voucherHeader?.Vendor?.Name}</td>
            <td style=""border-top-width: 0; border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-color: #e9ecef; border-top-style: solid; margin: 0; padding: 12px;"" class=""text-right"" align=""right"" valign=""top""></td>
          </tr>
        </tbody>
      </table>
      <table class=""s-2 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""8"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 8px; font-size: 8px; width: 100%; height: 8px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>

<table class=""btn btn-primary btn-lg mx-auto "" align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: separate !important; border-radius: 4px; margin: 0 auto;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-radius: 4px; margin: 0;"" align=""center"" bgcolor=""#007bff"">
        <a href=""https://erp.ohl.co.nz/web#id={voucherHeader.PurchaseOrderHeader?.ExternalId}&amp;view_type=form&amp;model=purchase.order&amp;menu_id=1564&amp;action=870"" target=""_blank"" style=""font-size: 20px; font-family: Helvetica, Arial, sans-serif; text-decoration: none; border-radius: 4.8px; line-height: 30px; display: inline-block; font-weight: normal; white-space: nowrap; background-color: #007bff; color: #ffffff; padding: 8px 16px; border: 1px solid #007bff;"">See Purchase Order</a>
      </td>
    </tr>
  </tbody>
</table>
      
	</div>
      </td>
    </tr>
  </tbody>
</table>

  </div>
      </td>
    </tr>
  </tbody>
</table>
<table class=""s-4 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""24"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 24px; width: 100%; height: 24px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>




  <!--table class=""card w-100 "" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: separate !important; border-radius: 4px; width: 100%; overflow: hidden; border: 1px solid #dee2e6;"" bgcolor=""#ffffff"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; width: 100%; margin: 0;"" align=""left"">
        <div>
    <table class=""card-body"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; width: 100%; margin: 0; padding: 20px;"" align=""left"">
        <div>
      <table class=""mx-auto"" align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; margin: 0 auto;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; margin: 0;"" align=""left"">
        <img width=""50"" height=""50"" class="""" src=""https://s3.amazonaws.com/lyft.zimride.com/images/emails/enterprise/briefcase_dark_large.png"" style=""height: auto; line-height: 100%; outline: none; text-decoration: none; border: 0 none;"">
      </td>
    </tr>
  </tbody>
</table>

      <h4 class=""text-center"" style=""margin-top: 0; margin-bottom: 0; font-weight: 500; color: inherit; vertical-align: baseline; font-size: 24px; line-height: 28.8px;"" align=""center"">Thanks for processing invoices with Ocerra</h4>
      <p class=""text-center"" style=""line-height: 24px; font-size: 16px; margin: 0;"" align=""center"">Please, record your invoices here.</p>
      <table class=""s-2 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""8"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 8px; font-size: 8px; width: 100%; height: 8px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table-->

<!--table class=""btn btn-light btn-sm mx-auto "" align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: separate !important; border-radius: 4px; margin: 0 auto;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-radius: 4px; margin: 0;"" align=""center"" bgcolor=""#007bff"">
        <a href=""https://app.ocerra.com/#/v/{voucherHeader.DocumentId}"" style=""font-size: 20px; font-family: Helvetica, Arial, sans-serif; text-decoration: none; border-radius: 4.8px; line-height: 30px; display: inline-block; font-weight: normal; white-space: nowrap; background-color: #007bff; color: #ffffff; padding: 8px 16px; border: 1px solid #007bff;"">See Invoice No: {voucherHeader.Number ?? "Unknown"}</a>
      </td>
    </tr>
  </tbody>
</table-->

    </div>
      </td>
    </tr>
  </tbody>
</table>

  </div>
      </td>
    </tr>
  </tbody>
</table>
<table class=""s-4 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""24"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 24px; width: 100%; height: 24px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>




  <table class=""table-unstyled text-muted "" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse; width: 100%; max-width: 100%; color: #636c72;"" bgcolor=""transparent"">
    <tbody>
      <tr>
        <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-width: 0; border-bottom-width: 0; margin: 0;"" align=""left"">© Ocerra {DateTime.Now.Year}</td>
        <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-width: 0; border-bottom-width: 0; margin: 0;"" align=""left"">
          <a href=""https://www.facebook.com/OcerraAP/"" target=""_blank""> 
          	</a><table class=""float-right"" align=""right"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-width: 0; border-bottom-width: 0; margin: 0;"" align=""left"">
        <table class=""pl-2"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Helvetica, Arial, sans-serif; mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-spacing: 0px; border-collapse: collapse;"">
  <tbody>
    <tr>
      <td style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 16px; border-top-width: 0; border-bottom-width: 0; padding-left: 8px; margin: 0;"" align=""left"">
        <img class="" "" width=""20"" height=""20"" src=""https://s3.amazonaws.com/lyft.zimride.com/images/emails/social/v2/facebook@2x.png"" style=""height: auto; line-height: 100%; outline: none; text-decoration: none; border: 0 none;"">
      </td>
    </tr>
  </tbody>
</table>

      </td>
    </tr>
  </tbody>
</table>

          
        </td>
      </tr>      
    </tbody>
  </table>
<table class=""s-4 w-100"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%;"">
  <tbody>
    <tr>
      <td height=""24"" style=""border-spacing: 0px; border-collapse: collapse; line-height: 24px; font-size: 24px; width: 100%; height: 24px; margin: 0;"" align=""left"">
         
      </td>
    </tr>
  </tbody>
</table>




              </td>
            </tr>
          </tbody>
        </table>
        <!--[if (gte mso 9)|(IE)]>
                </td>
              </tr>
            </tbody>
          </table>
        <![endif]-->
      </td>
    </tr>
  </tbody>
</table>


 
      </td>
    </tr>
  </tbody>
</table>
</body>
</html>";

            var result = await emailSender.SendEmail(new EmailOptions()
            {
                Body = emailContent,
                BodyHtml = true,
                From = new EmailAddress("Ocerra", "noreply@ocerra.com"),
                Subject = "Purchase Order status reminder for " + voucherHeader.PurchaseOrderHeader?.Number,
                To = new ListEmailAddress(new EmailAddress(voucherHeader.PurchaseOrderHeader.PurchaserName, voucherHeader.PurchaseOrderHeader.PurchaserEmail)),
                FilePath = filePath
            });

            return result?.EmailId != null;

        }
    }
}
