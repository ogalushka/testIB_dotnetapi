using tracker.Extensions;
using tracker.Repository;
using tracker.Viber; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<TrackRepository>();
builder.Services.AddTransient<ViberWebHook>();
builder.Services.AddHttpClient<ViberHttpClient>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = config.GetRequiredValue<string>("ViberHookEndpoint");
    var viberSecret = config.GetRequiredValue<string>("ViberSecret");
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Viber-Auth-Token", viberSecret);
});

var app = builder.Build();

app.UseMiddleware<ViberSignatureValidationMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await app.Services.GetRequiredService<ViberWebHook>().Setup();
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
        app.Lifetime.StopApplication();
    }
});
app.Lifetime.ApplicationStopping.Register(async () =>
{
    try
    {
        await app.Services.GetRequiredService<ViberWebHook>().Clear();
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
    }
});

app.Run();

