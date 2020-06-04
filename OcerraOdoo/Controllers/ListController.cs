﻿using Nancy;
using Nancy.Security;
using OcerraOdoo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Extensions;
using OcerraOdoo.Services;
using OcerraOdoo.Properties;
using OcerraOdoo.OcerraOData;
using Microsoft.OData.Client;

namespace OcerraOdoo.Controllers
{
    public class ListController : Controller<ListModel>
    {
        public ListController(OdataProxy odata, ExportService exportService)
        {
            Get("/Invoices", args => {
                int page = int.Parse((string)Request.Query.page ?? "1");
                page = page < 1 ? 1 : page;

                string search = Request.Query.search;
                string exportState = Request.Query.exportState;

                var workflowStates = odata.WorkflowState.ToList();

                var query = odata.VoucherHeader
                    .Expand("vendor,workflow($expand=workflowState),voucherValidation");

                query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.IsActive && !vh.IsArchived);

                if (!string.IsNullOrEmpty(search))
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Number.Contains(search) || vh.Vendor.Name.Contains(search));

                if (exportState == "True")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.ExternalId != null);

                if (exportState == "False")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.ExternalId == null);


                query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.OrderByDescending(vh => vh.CreatedDate).Skip((page - 1) * 20).Take(20);

                Model.Invoices = query
                .Execute()
                .Select(vh => new InvoiceModel
                {
                    Id = vh.VoucherHeaderId.ToString(),
                    DocumentId = vh.DocumentId.ToString(),
                    Vendor = vh.Vendor != null ? vh.Vendor.Name : "Unknown",
                    Status = vh.Workflow?.WorkflowState?.Name ?? "",
                    Number = vh.Number,
                    Date = (vh.Date ?? vh.CreatedDate).ToString("dd-MMM-yy"),
                    DueDate = vh.DueDate != null ? vh.DueDate.Value.ToString("dd-MMM-yy") : "",
                    Amount = vh.FcGross != null ? vh.FcGross.Value.ToString("C") : "$0.00",
                    Exported = vh.ExternalId != null ? "Yes" : "",
                    CanExport = vh.Vendor != null && vh.FcGross != null && vh.FcNet != null 
                        && vh.VoucherValidation.HasTotalMatches != "Fail" ? "" : "disabled='disabled'",
                    CanExportMessage =
                        vh.Vendor == null ? "You cannot export Invoice without vendor" :
                        vh.FcGross == null ? "You cannot export Invoice without amount" :
                        vh.VoucherValidation.HasTotalMatches == "Fail" ? "You cannot export Invoice without matching amounts" :
                        null,
                    PoMatches = 
                        vh.VoucherValidation.HasPoMatches == "Ignore" ? "" :
                        vh.VoucherValidation.HasPoMatches == "Success" ? "Yes" 
                            : "<b class='red'>No</b>",
                    TotalMatches =
                        vh.VoucherValidation.HasTotalMatches == "Ignore" ? "" :
                        vh.VoucherValidation.HasTotalMatches == "Success" ? "Yes"
                            : "<b class='red'>No</b>",
                    //PurchaseOrder = vh.VoucherPurchaseOrders.FirstOrDefault()?.PurchaseOrderHeader?.Number 
                }).ToList();

                var totalCount = query.Count();

                Model.Page = page;
                Model.Count = totalCount;
                Model.SearchStr = !string.IsNullOrEmpty(search) ? search.Replace("\"", "\\\"") : null;
                Model.ExportState = exportState;

                return View["List.html", Model];
            });

            Post("/ExportInvoicesByIds", async args => {
                var voucherHeaderIdsStr = (string)Request.Form["voucherHeaderIds[]"];
                if (!string.IsNullOrEmpty(voucherHeaderIdsStr))
                {
                    var voucherHeaderIds = voucherHeaderIdsStr.Split(',').Select(s => Guid.Parse(s)).ToArray();

                    var result = await exportService.ExportInvoicesByIds(voucherHeaderIds);

                    return Response.AsJson(new { message = $"Export complete: " + result.Message });
                }
                else 
                {
                    
                    return Response.AsJson(new { message = "You have not selected any invoices for export." });
                }
                
            });
        }

        public override ListModel Init()
        {
            return new ListModel();
        }
    }
}
