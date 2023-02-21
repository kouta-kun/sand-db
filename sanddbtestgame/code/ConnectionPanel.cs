using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

public class ConnectionPanel : Panel
{
	private const float FontSizeVh = 15f;
	private Label CountLabel { get; }
	public long ConnectionCount { get; set; }

	public ConnectionPanel()
	{
		CountLabel = Add.Label();
		CountLabel.Style.Width = Length.ViewWidth( 100.0f );
		CountLabel.Style.Height = Length.ViewWidth( 100.0f );
		CountLabel.Style.BackgroundColor = Color.White.WithAlpha( 0.75f );
		this.ConnectionCount = 0;
		CountLabel.Text = $"Connection count: {ConnectionCount}";
		CountLabel.Style.TextAlign = TextAlign.Center;
		CountLabel.Style.FontSize = Length.ViewHeight( FontSizeVh );
		CountLabel.Style.PaddingTop = Length.ViewHeight( 50f-FontSizeVh );
	}

	public override void Tick()
	{
		base.Tick();

		ConnectionCount = ((Pawn)Game.LocalPawn).ConnectionCount;

		CountLabel.Text = $"Connection count: {ConnectionCount}";
	}
}
