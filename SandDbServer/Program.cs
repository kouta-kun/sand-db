using LiteDB;
using SandDb;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5198");
var app = builder.Build();

app.UseWebSockets();

var db = new DatabaseHolder(new LiteDatabase("file.db"));

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await db.HandleSocket(socket);
    }
    else
    {
        await next(context);
    }
});

app.Run();