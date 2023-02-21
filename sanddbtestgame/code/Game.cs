using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.SandDbAccess;

//
// You don't need to put things in a namespace, but it doesn't hurt.
//
namespace Sandbox;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class MyGame : GameManager
{
	private RemoteDb _remoteDb;

	public MyGame()
	{
		_remoteDb = new RemoteDb( "ws://localhost:5198/ws", null );
	}

	// Need to use NumberLong because Json won't handle it correctly and so LiteDB on the
	// otherside of the connection turns it into a NumberLong by itself, and it's better to be explicit anyway.
	private class ConnectionCount : RemoteDb.DbObject
	{
		public NumberLong Count { get; set; }
		public NumberLong SteamId { get; set; }
	}

	/// <summary>
	/// A client has joined the server. Make them a pawn to play with
	/// </summary>
	public override void ClientJoined( IClient client )
	{
		base.ClientJoined( client );

		// Create a pawn for this client to play with
		var pawn = new Pawn();
		client.Pawn = pawn;

		var connCount = UpsertAndFeConnectionCount(client);

		pawn.ConnectionCount = connCount.Count;


		// Get all of the spawnpoints
		var spawnpoints = Entity.All.OfType<SpawnPoint>();

		// chose a random one
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
			pawn.Transform = tx;
		}
	}

	private ConnectionCount UpsertAndFeConnectionCount(IClient client)
	{
		// fetch ConnectionCounts on the remote side with the corresponding steamId
		var connCount = _remoteDb.Query<ConnectionCount>($"SteamId = {client.SteamId}").Result
			.FirstOrDefault(null as ConnectionCount);
		// if none exists, create with count = 0
		if ( connCount == null )
		{
			connCount = _remoteDb.Upsert( new ConnectionCount { Count = 0, SteamId = client.SteamId } ).Result;
		}

		// augment by 1 and update
		connCount.Count += 1L;
		connCount = _remoteDb.Upsert(connCount).Result; // Result is necessary to ensure it's been updated server-side
		
		// Upsert creates if object has no _id set, else it updates

		return connCount;
	}
}
