using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class ListModel : BaseModel
    {
        public List<InvoiceModel> Invoices { get; set; }
    }

    public class InvoiceModel 
    {
        public string Id { get; set; }
        public string Number { get; set; }

        public string Vendor { get; set; }
        public string Date { get; set; }
        public string DueDate { get; set; }
        public string Amount { get; set; }
        public string Status { get; set; }
        public string Exported { get; set; }

        public string CanExport { get; set; }
        public string CanExportMessage { get; set; }

        public string PurchaseOrder { get; set; }
    }
}
