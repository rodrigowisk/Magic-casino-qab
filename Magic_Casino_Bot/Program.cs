using Magic_Casino_Bot;

var builder = Host.CreateApplicationBuilder(args);

// 👇 ESTA LINHA É A MÁGICA QUE PERMITE O BOT FAZER REQUISIÇÕES 👇
builder.Services.AddHttpClient();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();