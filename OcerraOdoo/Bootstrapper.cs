using Microsoft.OData.Client;
using Nancy;
using Nancy.Authentication.Basic;
using Nancy.Bootstrapper;
using Nancy.Configuration;
using Nancy.TinyIoc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OcerraOdoo.Models;
using OcerraOdoo.OcerraOData;
using OcerraOdoo.Properties;
using OdooRpc.CoreCLR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        public static OcerraModel OcerraModel = null;
        public static OdooModel OdooModel = null;
        public static TinyIoCContainer Container = null;

        protected override IRootPathProvider RootPathProvider
        {
            get { return new Nancy.Hosting.Aspnet.AspNetRootPathProvider(); }
        }

        public override void Configure(INancyEnvironment environment)
        {
            base.Configure(environment);
            environment.Tracing(enabled: false, displayErrorTraces: true);
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            try
            {
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
                    StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    Converters = new List<JsonConverter> {
                    new OdooKeyValueJsonConverter(),
                    new OdooStringJsonConverter(),
                    new OdooDateJsonConverter(),
                    new OdooDecimalJsonConverter(),
                    new OdooObjectArrayJsonConverter()
                },
                    MaxDepth = 10,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                base.ApplicationStartup(container, pipelines);

                pipelines.EnableBasicAuthentication(new BasicAuthenticationConfiguration(
                    container.Resolve<IUserValidator>(),
                    "MyRealm"));

                container.Register((c, p) =>
                {
                    var httpClient = new HttpClient()
                    {
                        BaseAddress = new Uri(Settings.Default.OcerraUrl),
                    };

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Settings.Default.OcerraApiKey);

                    return new OcerraClient(httpClient);
                });

                container.Register((c, p) =>
                {
                    return new OdooRpcClient(new OdooRpc.CoreCLR.Client.Models.OdooConnectionInfo
                    {
                        Database = Settings.Default.OdooDatabase,
                        Host = Settings.Default.OdooUrl,
                        Username = Settings.Default.OdooLogin,
                        Password = Settings.Default.OdooBase64Pwd.FromBase64(),
                        Port = Settings.Default.OdooPort,
                        IsSSL = Settings.Default.OdooSsl
                    });
                });

                container.Register<OdataProxy>();

                var initTask = Task.Run(async () => await InitModels(container));
                initTask.Wait(TimeSpan.FromSeconds(15));

                if (!initTask.IsCompleted)
                    throw new Exception("Init task has not been complete");
            }
            catch (Exception ex) {
                ex.LogError("There was an error in App Startup function");
            }
            

        }

        public async Task InitModels(TinyIoCContainer container) {
            try
            {
                var connectToOcerra = await container.Resolve<OcerraClient>().ApiClientCurrentGetAsync();
                var connectToOdoo = await container.Resolve<OdooRpcClient>().GetOdooVersion();

                OcerraModel = new OcerraModel
                {
                    ClientId = connectToOcerra.ClientId,
                    ClientName = connectToOcerra.Name,
                    CountryCode = connectToOcerra.CurrencyCodes?.FirstOrDefault(cc => cc.IsDefault)?.CountryCode,
                    CurrencyCode = connectToOcerra.CurrencyCodes?.FirstOrDefault(cc => cc.IsDefault)?.Code,
                    CurrencyCodes = connectToOcerra.CurrencyCodes?.ToList(),
                    Connected = true,
                    LastHeartBeat = Settings.Default.OcerraLastHeartBeat.ToDate(new DateTime(2020, 1, 1)),
                };

                OdooModel = new OdooModel
                {
                    Connected = true,
                    VersionNumber = connectToOdoo.ServerVersion
                };
            }
            catch (Exception ex)
            {
                ex.LogError("The was an error when application starts");
            }
        }

        public class UserValidator : IUserValidator
        {
            public ClaimsPrincipal Validate(string username, string password)
            {
                if (username == Settings.Default.ManagementLogin && password == Helpers.FromBase64(Settings.Default.ManagementPassword))
                {
                    return new ClaimsPrincipal(new GenericIdentity(username));
                }

                // Not recognised => anonymous.
                return null;
            }
        }

        
    }
}
