using FileSearchEngine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var elasticUsername = app.Configuration.GetValue<string>("ElasticUsername");
var elasticPassword = app.Configuration.GetValue<string>("ElasticPassword");
var elasticUrl = app.Configuration.GetValue<string>("ElasticUrl");
var elasticFingerprint = app.Configuration.GetValue<string>("ElasticFingerprint");
if(elasticUsername != null && elasticPassword != null && elasticUrl != null && elasticFingerprint != null)
{
    ElasticDatabase.Initialize(elasticUsername, elasticPassword, elasticUrl, elasticFingerprint);
}

await Model.Initialize();

app.Run();