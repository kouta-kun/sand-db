using System.Net.WebSockets;
using System.Text;
using LiteDB;

namespace SandDb;

public class DatabaseHolder
{
    public DatabaseHolder(LiteDatabase db)
    {
        _db = db;
    }

    private readonly LiteDatabase _db;

    private IEnumerable<BsonDocument> Query(string name, BsonExpression predicate)
    {
        var liteCollection = GetCollection(name);

        return liteCollection.Find(predicate).ToList();
    }

    private BsonDocument Upsert(string name, ObjectId id, BsonDocument document)
    {
        var liteCollection = GetCollection(name);
        liteCollection.Upsert(new BsonValue(id), document);
        return liteCollection.FindById(new BsonValue(id));
    }

    private ILiteCollection<BsonDocument> GetCollection(string name)
    {
        return _db.GetCollection(name, BsonAutoId.Guid);
    }

    public async Task HandleSocket(WebSocket socket)
    {
        var buffer = new byte[1024 * 4];
        var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        while (!receiveResult.CloseStatus.HasValue)
        {
            var command = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, 0, receiveResult.Count));

            await ProcessCommand(socket, command, buffer);

            receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await socket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None
        );
    }

    private delegate T MainTryLambda<out T>();

    private delegate T ExceptionTryLambda<out T>(Exception exc);

    private static T Try<T>(MainTryLambda<T> mainTryLambda, ExceptionTryLambda<T> exceptionTryLambda)
    {
        try
        {
            return mainTryLambda();
        }
        catch (Exception e)
        {
            return exceptionTryLambda(e);
        }
    }

    private async Task ProcessCommand(WebSocket socket, string command, byte[] buffer)
    {
        var commandValue = JsonSerializer.Deserialize(command).AsDocument!;

        var response = Try(() => commandValue["cmd"].AsString switch
        {
            "query" =>
                HandleQuery(commandValue),
            "update" =>
                HandleUpdate(commandValue),
            "force-index" =>
                HandleForceIndex(commandValue),
            _ => throw new ArgumentOutOfRangeException()
        }, (e) => new BsonDocument(new Dictionary<string, BsonValue>
        {
            ["Error"] = e.Message
        }));

        await ReplyMessage(socket, response, buffer);
    }

    private BsonDocument HandleForceIndex(BsonDocument commandValue)
    {
        var collectionName = commandValue["collection"].AsString!;
        var key = commandValue["key"].AsString!;
        var unique = commandValue["unique"].IsBoolean && commandValue["unique"].AsBoolean;

        EnforceIndex(collectionName, key, unique);

        return new BsonDocument(new Dictionary<string, BsonValue>()
        {
            ["Ok"] = true
        });
    }

    private void EnforceIndex(string collectionName, string key, bool unique)
    {
        GetCollection(collectionName).EnsureIndex(BsonExpression.Create("$." + key), unique);
    }

    private static async Task ReplyMessage(WebSocket socket, BsonDocument response, byte[] buffer)
    {
        var responseString = JsonSerializer.Serialize(response);

        while (responseString.Length > 0)
        {
            var send = responseString[..Math.Min(responseString.Length, 1024 * 2)];
            var bytes = Encoding.UTF8.GetBytes(send, 0, send.Length, buffer, 0);

            await socket.SendAsync(
                new ReadOnlyMemory<byte>(buffer, 0, bytes),
                WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None
            );
            responseString = responseString[send.Length..];
        }
    }

    private BsonDocument HandleUpdate(BsonDocument commandValue)
    {
        var idString = commandValue["id"].IsString ? new ObjectId(commandValue["id"].AsString) : ObjectId.NewObjectId();

        var collectionName = commandValue["collection"].AsString!;

        var objectContent = commandValue["object"].AsDocument!;

        return Upsert(collectionName, idString, objectContent);
    }

    private IEnumerable<BsonDocument> List(string name)
    {
        var liteCollection = GetCollection(name);
        return liteCollection.FindAll();
    }

    private BsonDocument HandleQuery(BsonDocument queryCommand)
    {
        var queryParam = queryCommand["query"].AsString;

        var collectionName = queryCommand["collection"].AsString;
        var result = new BsonArray(
            queryParam != null ? Query(collectionName, BsonExpression.Create(queryParam)) : List(collectionName)
        );
        return new BsonDocument(new Dictionary<string, BsonValue>
        {
            ["result"] =
                result
        });
    }
}