using VantaiViet.CoreApi.Hosting;
//hello
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiFoundation(builder.Configuration);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var app = builder.Build();
app.UseApiFoundation();
app.Run();
