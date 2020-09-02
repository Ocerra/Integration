using Microsoft.OData.Client;
using OcerraOdoo.ODataClient.Default;
using OcerraOdoo.ODataClient.Proxies;
using OcerraOdoo.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.OcerraOData
{
    public class OdataProxy : Container
    {
        public OdataProxy() : 
            base(new Uri(Settings.Default.OcerraUrl + "odata"))
        {
            this.MergeOption = MergeOption.NoTracking;

            this.Configurations.RequestPipeline.OnMessageCreating = (a) =>
            {
                
                var request = new HttpWebRequestMessage(a);

                //Setting the values in the Authorization header
                request.SetHeader("Authorization", "Basic " + Settings.Default.OcerraApiKey);

                return request;
            };
        }
    }
}
