/*
KSPIRC - Internet Relay Chat plugin for Kerbal Space Program.
Copyright (C) 2013 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

class IRCWindow : AbstractWindow {
	private const string FORUM_THREAD_URL = "http://forum.kerbalspaceprogram.com/threads/59150";

	private string name_;
	public string name {
		get {
			return name_;
		}

		set {
			name_ = value;
			foreach (ChannelGUI channelGUI in channelGUIs.Values) {
				channelGUI.name = value;
			}
		}
	}

	public bool anyChannelsHighlightedPrivateMessage {
		get {
			return channelGUIs.Values.Any(gui => gui.channelHighlightedPrivateMessage);
		}
	}
	public bool anyChannelsHighlightedMessage {
		get {
			return channelGUIs.Values.Any(gui => gui.channelHighlightedMessage);
		}
	}
	public bool anyChannelsHighlightedJoin {
		get {
			return channelGUIs.Values.Any(gui => gui.channelHighlightedJoin);
		}
	}

	public event ChannelClosedHandler channelClosedEvent;
	public event UserCommandHandler onUserCommandEntered;

	public bool? newVersionAvailable;

	private bool namesHidden_;
	private bool namesHidden {
		get {
			return namesHidden_;
		}

		set {
			namesHidden_ = value;
			foreach (ChannelGUI channelGUI in channelGUIs.Values) {
				channelGUI.namesHidden = value;
			}
		}
	}

	private Dictionary<string, ChannelGUI> channelGUIs = new Dictionary<string, ChannelGUI>();
	private List<string> handles = new List<string>();
	private ChannelGUI currentChannelGUI;
	private bool stylesInitialized;
	private GUIStyle buttonActiveStyle;
	private GUIStyle buttonHighlightedNicknameStyle;
	private GUIStyle buttonHighlightedStyle;

	public IRCWindow(string name) {
		this.name = name;

		hidden = true;
		title = "IRC";
		rect = new Rect(Screen.width / 6, Screen.height / 6, Screen.width * 2 / 3, Screen.height * 2 / 3);

		onResized += windowResized;
		onVisibleToggled += (e) => windowVisibleToggled(e.visible);
	}

	protected override void drawContents() {
		initStyles();

		GUILayout.BeginVertical();
			drawChannelButtons();

			if (currentChannelGUI != null) {
				currentChannelGUI.draw();
			}

			drawNewVersion();
		GUILayout.EndVertical();

		if (GUI.Button(new Rect(rect.width - 18, 2, 16, 16), "")) {
			hidden = true;
		}
	}

	private void initStyles() {
		if (!stylesInitialized) {
			buttonActiveStyle = new GUIStyle(GUI.skin.button);
			buttonActiveStyle.fontStyle = FontStyle.Bold;

			buttonHighlightedNicknameStyle = new GUIStyle(GUI.skin.button);
			buttonHighlightedNicknameStyle.normal.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.onHover.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.hover.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.onActive.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.active.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.onFocused.textColor = Color.yellow;
			buttonHighlightedNicknameStyle.focused.textColor = Color.yellow;

			buttonHighlightedStyle = new GUIStyle(GUI.skin.button);
			buttonHighlightedStyle.normal.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.onHover.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.hover.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.onActive.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.active.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.onFocused.textColor = XKCDColors.BlueGrey;
			buttonHighlightedStyle.focused.textColor = XKCDColors.BlueGrey;

			stylesInitialized = true;
		}
	}

	private void windowResized() {
		foreach (ChannelGUI channelGUI in channelGUIs.Values) {
			channelGUI.windowResized();
		}
	}

	private void windowVisibleToggled(bool visible) {
		if (visible) {
			if (currentChannelGUI != null) {
				currentChannelGUI.hidden = false;
			}
		} else {
			foreach (ChannelGUI channelGUI in channelGUIs.Values) {
				channelGUI.hidden = true;
			}
		}
	}

	private void drawNewVersion() {
		if (newVersionAvailable == true) {
			GUILayout.BeginVertical();
				GUILayout.Space(10);
				GUILayout.BeginHorizontal();
					Color oldColor = GUI.color;
					GUI.color = Color.yellow;
					GUILayout.Label("A newer version of this plugin is available.");
					GUI.color = oldColor;

					GUILayout.Space(10);

					if (GUILayout.Button("Download Page")) {
						Application.OpenURL(FORUM_THREAD_URL);
					}

					GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
	}

	private void drawChannelButtons() {
		GUILayout.BeginHorizontal();
			foreach (string handle in handles) {
				ChannelGUI channelGUI = getChannelGUI(handle);
				GUIStyle buttonStyle;
				if (channelGUI.Equals(currentChannelGUI)) {
					buttonStyle = buttonActiveStyle;
				} else if (channelGUI.channelHighlightedPrivateMessage) {
					buttonStyle = buttonHighlightedNicknameStyle;
				} else if (channelGUI.channelHighlightedMessage) {
					buttonStyle = buttonHighlightedStyle;
				} else {
					buttonStyle = GUI.skin.button;
				}

				if (GUILayout.Button(handle, buttonStyle)) {
					currentChannelGUI = channelGUI;
					currentChannelGUI.hidden = false;
					foreach (ChannelGUI gui in channelGUIs.Values.Where(gui => !gui.Equals(currentChannelGUI))) {
						gui.hidden = true;
					}
				}
			}

			GUILayout.FlexibleSpace();

			if ((currentChannelGUI != null) && (channelGUIs.Count > 1)) {
				if (currentChannelGUI.handle.StartsWith("#")) {
					if (GUILayout.Button(namesHidden ? "<" : ">")) {
						namesHidden = !namesHidden;
					}
				}
				if (GUILayout.Button("X")) {
					closeChannel(currentChannelGUI.handle);
				}
			}
		GUILayout.EndHorizontal();
	}

	private void closeChannel(string handle) {
		if (channelClosedEvent != null) {
			channelClosedEvent(new ChannelEvent(handle));
		}
		channelGUIs.Remove(handle);
		handles.Remove(handle);
		currentChannelGUI = channelGUIs.Values.FirstOrDefault();
	}

	public void addToChannel(string handle, string sender, string text, IRCCommand cmd = null) {
		ChannelGUI channelGUI = getChannelGUI(handle);
		channelGUI.addToBuffer(sender, text, cmd);

		// show this channel if no channel is visible yet
		if (currentChannelGUI == null) {
			currentChannelGUI = channelGUI;
			currentChannelGUI.hidden = false;
		}
	}

	public void addChannelNames(string handle, string[] names) {
		getChannelGUI(handle).addNames(names);
	}

	public void addSingleChannelName(string handle, string name) {
		getChannelGUI(handle).addSingleName(name);
	}

	public void endOfChannelNames(string handle) {
		getChannelGUI(handle).endOfNames();
	}

	public void removeChannelName(string handle, string name) {
		getChannelGUI(handle).removeName(name);
	}

	public void renameInChannel(string handle, string oldName, string newName) {
		getChannelGUI(handle).rename(oldName, newName);
	}

	public void changeUserModeInChannel(string handle, string name, string mode) {
		getChannelGUI(handle).changeUserMode(name, mode);
	}

	public string[] getChannelsContainingName(string name) {
		List<string> handles = new List<string>();
		foreach (string handle in channelGUIs.Keys) {
			if (getChannelGUI(handle).containsName(name)) {
				handles.Add(handle);
			}
		}
		return handles.ToArray();
	}

	public void setChannelTopic(string handle, string topic) {
		getChannelGUI(handle).topic = topic;
	}

	public string getCurrentChannelName() {
		return (currentChannelGUI != null) ? currentChannelGUI.handle : null;
	}

	private ChannelGUI getChannelGUI(string handle) {
		ChannelGUI channelGUI;
		if (!channelGUIs.ContainsKey(handle)) {
			channelGUI = new ChannelGUI(handle, name);
			channelGUI.hidden = true;
			channelGUI.onUserCommandEntered += (e) => userCommandEntered(e.command);
			channelGUIs.Add(handle, channelGUI);
			handles.Add(handle);
			handles.Sort(StringComparer.CurrentCultureIgnoreCase);
		} else {
			channelGUI = channelGUIs[handle];
		}
		return channelGUI;
	}

	private void userCommandEntered(UserCommand cmd) {
		if (onUserCommandEntered != null) {
			onUserCommandEntered(new UserCommandEvent(cmd));
		}
	}
}

delegate void ChannelClosedHandler(ChannelEvent e);

class ChannelEvent : EventArgs {
	public string handle {
		get;
		private set;
	}

	public ChannelEvent(string handle) {
		this.handle = handle;
	}
}
