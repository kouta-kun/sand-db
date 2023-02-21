using Sandbox.UI;

namespace Sandbox;

public class Hud : HudEntity<RootPanel>
{
	public ConnectionPanel ConnectionPanel;

	public Hud()
	{
		if ( !Game.IsClient )
		{
			return;
		}

		ConnectionPanel = RootPanel.AddChild<ConnectionPanel>();
	}
}
