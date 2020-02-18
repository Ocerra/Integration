using Microsoft.OData.Client;
using OcerraConsoleApp.ODataClient.Proxies;
using OcerraConsoleApp.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OcerraConsoleApp
{
    class Program
    {
        /// <summary>
        /// In ocerra, we expose two connection types: OData - for all query business and OpenAPI for all CRUD operations. 
        /// In this demo I want to show how to use both connections.
        /// Ocerra client was generated from OcerraNSwag file using NSwagStudio https://github.com/RicoSuter/NSwag/wiki/NSwagStudio 
        /// OpenAPI metadata can be found here: https://app.ocerra.com/swagger/
        /// The OcerraOData was generated from https://app.ocerra.com/odata using Visual Studio 2019.
        /// Please, configure you connection details in App.Config file before trying this demo. 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var httpClient = new HttpClient() { 
                BaseAddress = new Uri("https://app.ocerra.com/")
            };

            var token = Settings.Default.OcerraLogin + ":" + Settings.Default.OcerraPassword;
            token = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
            
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);


            var client = new OcerraClient(httpClient);

            var taxAccounts = Task.Run(async () => await client.ApiTaxAccountGetAsync(0, 5)).GetAwaiter().GetResult();

            foreach (var taxAccount in taxAccounts) {
                Console.WriteLine($"Tax Account Name: {taxAccount.Name}");
            }

            var container = new ContainerProxy(new Uri("https://app.ocerra.com/odata"));
            container.Configurations.RequestPipeline.OnMessageCreating = (a) =>
            {
                var request = new HttpWebRequestMessage(a);

                //Setting the values in the Authorization header
                request.SetHeader("Authorization", "Basic " + token);

                return request;
            };

            var taxRates = container.TaxRateProxy.Take(10).ToList();

            foreach (var taxRate in taxRates)
            {
                Console.WriteLine($"Tax Rates: {taxRate.CodeProxy}");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }
    }
}
