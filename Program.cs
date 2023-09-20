// Note: Tools / Options / Azure Service Authentication is what is used when F5-ing into to app

using Azure;
using Azure.Core;
using Azure.Identity;

using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;

using JsonObject = System.Collections.Generic.Dictionary<string, object>;

const string DEMO_RG = "demo-appservice-metadata";
const string DEMO_PLAN = "demoappplancw123456789";
const string DEMO_SITE = "demositecw123456789";
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
        NetFrameworkVersion = "v4.0",
        // WindowsFxVersion = "DOTNETCORE|6.0", // No, doesn't work. // https://stackoverflow.com/questions/69897663/setting-azure-app-service-server-stack-on-a-bicep-template?noredirect=1#comment123566981_69897663
        MinTlsVersion = AppServiceSupportedTlsVersion.Tls1_2,
        Use32BitWorkerProcess = true,
        IsAlwaysOn = false,
        IsHttp20Enabled = true,
        AppSettings = new List<AppServiceNameValuePair>(),
        IsWebSocketsEnabled = true
    },
    Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned),
};

Console.WriteLine("... creating WebSite");
var wsOp = await resourceGroup.GetWebSites().CreateOrUpdateAsync(WaitUntil.Started, DEMO_SITE, siteCreationData);
WebSiteResource webSite = await wsOp.WaitForCompletionAsync();

// Screenshot: when-no-metadata-is-provided.png => Website without Runtime set properly
// The problem: https://cloudstep.io/2020/11/18/undocumented-arm-oddities-net-core-app-services/
// Bicep solution: https://roanpaes.wordpress.com/2021/10/06/set-azure-app-service-stack-as-netcore-with-bicep/
// Adding metadata via PS: https://developercommunity.visualstudio.com/t/not-able-to-change-windows-based-app-service-runti/1477982
// C# GenericResourceData inspired by: https://github.com/Azure/azure-sdk-for-net/issues/28993

GenericResourceData metadataCreationData = new GenericResourceData(DEMO_LOCATION) // the internal ctor looks interesting though
{
    Kind = "web",
    // Properties = BinaryData.FromString("{ \"CURRENT_STACK\":\"dotnet\" }"),
    Properties = BinaryData.FromObjectAsJson(
        new JsonObject() {
           { "CURRENT_STACK", "dotnet" }
        })
};

// ... /resourceGroups/demo-appservice-metadata/providers/Microsoft.Web/sites/demositecw123456789/config/metadata
var x = webSite.Id.AppendProviderResource("Microsoft.Web", "config", "metadata");
ResourceIdentifier metadataId = webSite.Id.AppendChildResource("config", "metadata");
/*System.InvalidOperationException
  HResult=0x80131509
  Message=Invalid resource type Microsoft.Web/sites/config
  Source=Azure.ResourceManager
*/
var lroMetadataResource = await client.GetGenericResources().CreateOrUpdateAsync(WaitUntil.Completed, metadataId, metadataCreationData);
GenericResource genericResource = lroMetadataResource.Value;

//foreach (WebSiteResource ws in resourceGroup.GetWebSites())
//{
//    var settings = ws.GetApplicationSettings();
//}

Console.WriteLine("end of line");