using TimeForYou.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkerModule(builder.Configuration);

var host = builder.Build();
host.Run();