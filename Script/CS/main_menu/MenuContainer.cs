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
				bool disabled = !enable;
				// 「加载游戏」在没有存档时保持置灰，不能被 show_menu 动画重新点亮
				if (enable && btn.Name == "Load" && (SaveManager.Instance == null || !SaveManager.Instance.HasSave()))
					disabled = true;
				btn.Disabled = disabled;
			}
		}
	}

}