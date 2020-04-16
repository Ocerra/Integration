using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class MainModel : BaseModel
    {
        public string LastVendorSyncDate { get; set; }
        public string LastPurchaseSyncDate { get; set; }
        public string LastInvoiceSyncDate { get; set; }
    }
}
