using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(SolverSite.LOCAL_URL);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<SbcStore>();
builder.Services.AddSingleton<MySqlFulfilmentStore>();
builder.Services.AddSingleton<DraftService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
