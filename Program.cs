// Note: Tools / Options / Azure Service Authentication is what is used when F5-ing into to app

using Azure;
using Azure.Core;
using Azure.Identity;

using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;

const string DEMO_RG = "demo-appservice-metadata";
const string DEMO_PLAN = "demoappplancw1234567890";
const string DEMO_SITE = "demositecw1234567890";
AzureLocation DEMO_LOCATION = AzureLocation.WestEurope;

// https://learn.microsoft.com/en-us/dotnet/azure/sdk/resource-management
ArmClient client = new ArmClient(new VisualStudioCredential());

SubscriptionResource subscription = client.GetDefaultSubscription();
Console.WriteLine("Subscription Id: " + subscription.Data.SubscriptionId);
Console.WriteLine("Name: " + subscription.Data.DisplayName);
Console.WriteLine("AAD Id: " + subscription.Data.TenantId);

ResourceGroupResource resourceGroup = client.GetDefaultSubscription().GetResourceGroup(DEMO_RG);

AppServicePlanData planCreationData = new AppServicePlanData(DEMO_LOCATION)
{
    Kind = "windows",
    Sku = new AppServiceSkuDescription()
    {
        Name = "B1",
        Tier = "Basic",
        Size = "B1",
        Family = "B"
    }
};

Console.WriteLine("... creating AppService plan");
var planOp = await resourceGroup.GetAppServicePlans().CreateOrUpdateAsync(WaitUntil.Started, DEMO_PLAN, planCreationData);
AppServicePlanResource appServicePlan = await planOp.WaitForCompletionAsync();

WebSiteData siteCreationData = new WebSiteData(DEMO_LOCATION)
{
    Location = DEMO_LOCATION,
    Kind = "windows",
    IsEnabled = true,
    AppServicePlanId = appServicePlan.Id,
    IsHttpsOnly = true,
    SiteConfig = new SiteConfigProperties()
    {
        NetFrameworkVersion = "v8.0",
        MinTlsVersion = AppServiceSupportedTlsVersion.Tls1_2,
        Use32BitWorkerProcess = false,
        IsAlwaysOn = false,
        IsHttp20Enabled = true,
        AppSettings = new List<AppServiceNameValuePair>(),
        IsWebSocketsEnabled = true,
    },
    Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned),
};

Console.WriteLine("... creating WebSite");
var wsOp = await resourceGroup.GetWebSites().CreateOrUpdateAsync(WaitUntil.Started, DEMO_SITE, siteCreationData);
WebSiteResource webSite = await wsOp.WaitForCompletionAsync();

var metadataResult = await webSite.GetMetadataAsync();
var metadata = metadataResult.Value; // will be empty by default

metadata.Properties.Add("CURRENT_STACK", "dotnet");

await webSite.UpdateMetadataAsync(metadata);

Console.WriteLine("end of line");