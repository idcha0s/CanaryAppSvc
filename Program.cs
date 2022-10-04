using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;

var builder = WebApplication.CreateBuilder(args);
ConfigurationManager configuration = builder.Configuration; // allows both to access and to set up the config
IWebHostEnvironment environment = builder.Environment;
builder.Services.AddHealthChecks();
// https://stackoverflow.com/questions/55850179/how-can-we-use-httpclient-in-asp-net-core
// Investigate using HttpClient to get healthstatus...
builder.Services.AddHttpClient();

var app = builder.Build();

// TODO: Can we use config builder to get the healthstatus of the vaults we connect to?
app.UseHealthChecks("/healthstatus");
app.UseRequestLocalization();

// localize our datetime - why doesn't this localize automatically w/UseRequestLocalization? 
var PST = new TimeSpan (-7, 0, 0);

// pull the name of the secret we're fetching from the vault from the appsettings.json
var secretName = configuration["Secret"];

app.MapGet("/", async context =>
    {
        // TODO: Get the /healthstatus of each vault: https://docs.microsoft.com/en-us/azure/key-vault/general/private-link-diagnostics
        // pull the x-ms-keyvault-network-info header from the vault
        
        // render the page and pass the current datetime and HttpContext at page load
        await context.Response.WriteAsync(PageRender(DateTimeOffset.Now.ToOffset(PST), context));
    });
app.MapGet("/cus", async context =>
    {
        // get the Uri from appsettings.json based on the current path
        Uri vault = new System.Uri(configuration.GetSection("RegionalVaults")[context.Request.Path]);
    
        // render the page
        await context.Response.WriteAsync(PageRenderVault(vault, secretName, DateTimeOffset.Now.ToOffset(PST), context));
    });
app.MapGet("/eus", async context =>
    {
        // get the Uri from appsettings.json based on the current path
        Uri vault = new System.Uri(configuration.GetSection("RegionalVaults")[context.Request.Path]);
    
        // render the page
        await context.Response.WriteAsync(PageRenderVault(vault, secretName, DateTimeOffset.Now.ToOffset(PST), context));
    });
app.MapGet("/scus", async context =>
    {
        // get the Uri from appsettings.json based on the current path
        Uri vault = new System.Uri(configuration.GetSection("RegionalVaults")[context.Request.Path]);

        // render the page
        await context.Response.WriteAsync(PageRenderVault(vault, secretName, DateTimeOffset.Now.ToOffset(PST), context));
    });
app.MapGet("/all", async context =>
    {
        // render the pages - get the list of vaults as a list of Uri
        var vaults = configuration.GetSection("Vaults").Get<List<Uri>>();

        // render each page based on the vault uri and secretName entries in appsettings.json Vaults and Secret sections respectively
        foreach (var vault in vaults)
            {
                await context.Response.WriteAsync(PageRenderVault(vault, secretName, DateTimeOffset.Now.ToOffset(PST), context));
            }
    });


string PageRender(DateTimeOffset StartDate, HttpContext context)
    {
        // just return text so we get a clean page load when we don't want the HealthChecks hitting live code
        //string AppName = environment.ApplicationName;

        // TODO: Add healthcheck and return header response to page
        //var httpRequestMessage = new HttpRequestMessage(
        //    HttpMethod.Get,
        //    "https://brturner-canary-kv.vault.azure.net/healthcheck");
        //};

        try{

            // This isn't working...
            //var httpClient = HttpClientFactoryExtensions.CreateClient;
            //var httpResponseMessage = await httpClient.Invoke(IHttpClientFactory httpRequestMessage);
            //Console.WriteLine(httpResponseMessage);
            return $"[{StartDate}] HealthCheck\nApplication: {builder.Environment.ApplicationName}\nCallerIP: {context.Connection.RemoteIpAddress}\nUserAgent: {context.Request.Headers.UserAgent}\n";
       }
        catch (Exception e){
            string myDate = DateTimeOffset.Now.ToOffset(PST).ToString();
            return $"[{myDate}] Error: {e.Message}\n";
        }
    }

string PageRenderVault(Uri vault, string secretName, DateTimeOffset myStartDate, HttpContext context)
    {
        // Render the page on page load from the direct vault
        SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay= TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 3,
                    Mode = RetryMode.Exponential
                }
            };
        // Read the secrets
        try{
            // New client using MSI to the vault
            var client = new SecretClient(vault, new DefaultAzureCredential(),options);

            KeyVaultSecret secret = client.GetSecret(secretName);
            string secretValue = secret.Value;
            string headers = string.Empty;

            // Read the vault URI
            string vaultUri = client.VaultUri.ToString();
            DateTimeOffset myEndDate = DateTimeOffset.Now.ToOffset(PST);
            string myDate = DateTimeOffset.Now.ToOffset(PST).ToString();
            string myLatency = (myEndDate - myStartDate).TotalMilliseconds.ToString();
            return $"[{myDate}]\nUri: {vaultUri}\nSecret: {secretName} = {secretValue}\nLatency: {myLatency} ms\nCallerIP: {context.Connection.RemoteIpAddress}\nUserAgent: {context.Request.Headers.UserAgent}\n\n";
        }
        catch (Exception e){
            string myDate = DateTimeOffset.Now.ToOffset(PST).ToString();
            return $"[{myDate}] Error: {e.Message}\n";
        }
    }

app.Run();
