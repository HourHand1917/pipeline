using Godot;
using System;

public partial class MenuContainer : VBoxContainer
{

	private void EnableButtons(bool enable)
	{
		foreach (var button in GetChildren())
		{
			if (button is Button btn)
			{
				btn.Disabled = !enable;
			}
		}
	}

}