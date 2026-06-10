using ImageBoardCTF.Data;
using ImageBoardCTF.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".code.rain.session";
    options.IdleTimeout = TimeSpan.FromHours(6);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

Directory.SetCurrentDirectory(app.Environment.ContentRootPath);
app.Services.GetRequiredService<Database>().Initialize(app.Environment.ContentRootPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllers();

app.MapControllerRoute(
    name: "post",
    pattern: "post/{id:int}",
    defaults: new { controller = "Home", action = "Post" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
