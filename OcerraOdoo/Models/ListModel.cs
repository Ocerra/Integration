﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace OcerraOdoo.Models
{
    public class ListModel : BaseModel
    {
        public List<InvoiceModel> Invoices { get; set; }

        public int Page { get; set; }
        public int Count { get; set; }

        public int PrevPage => Page > 1 ? Page - 1 : 1;

        public int NextPage => Page > 1 ? Page + 1 : 2;

        public string SearchStr { get; set; }
        public string ExportState { get; set; }
    }

    public class InvoiceModel 
    {
        public string Id { get; set; }

        public string DocumentId { get; set; }

        public string Number { get; set; }

        public string Vendor { get; set; }
        public string Date { get; set; }
        public string DueDate { get; set; }
        public string Amount { get; set; }
        public string Status { get; set; }
        public string Exported { get; set; }

        public string PoMatches { get; set; }
        public string TotalMatches { get; set; }

        public string CanExport { get; set; }
        public string CanExportMessage { get; set; }

        public string PurchaseOrder { get; set; }
    }
}
