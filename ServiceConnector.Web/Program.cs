using ServiceConnector.Web.Registrars;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceConnector();

var app = builder.Build();

app.UseServiceConnector();

app.Run();