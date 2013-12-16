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
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Toolbar;

[KSPAddonFixed(KSPAddon.Startup.MainMenu, true, typeof(KSPIRC))]
class KSPIRC : MonoBehaviour {
	private const string NOTICE_CHANNEL_HANDLE = "(Notice)";
	private const int VERSION = 8;
	private const string FULL_VERSION = "0.6.2";

	// debugging
	private const string DEBUG_CHANNEL_HANDLE = "(Debug)";
	private const bool IRC_WINDOW_HIDDEN = true;

	private delegate void IRCCommandHandler(IRCCommand cmd);
	private delegate void UserCommandHandler(UserCommand cmd);

	private Dictionary<string, IRCCommandHandler> serverCommandHandlers = new Dictionary<string, IRCCommandHandler>();
	private Dictionary<string, UserCommandHandler> userCommandHandlers = new Dictionary<string, UserCommandHandler>();
	private IRCClient client;
	private IRCWindow ircWindow;
	private WWW versionWWW;
	private bool debug;
	private IButton windowButton;
	private bool showNewVersionOnButton;

	KSPIRC() {
		GameObject.DontDestroyOnLoad(this);

		string settingsFile = KSPUtil.ApplicationRootPath + "GameData/blizzy/KSPIRC/irc.cfg";
		ConfigNode settings = ConfigNode.Load(settingsFile) ?? new ConfigNode();
		string host = settings.HasValue("host") ? settings.GetValue("host") : null;
		int port = settings.HasValue("port") ? int.Parse(settings.GetValue("port")) : -1;
		string user = settings.HasValue("user") ? settings.GetValue("user") : null;
		string serverPassword = settings.HasValue("serverPassword") ? settings.GetValue("serverPassword") : null;
		string nick = settings.HasValue("nick") ? settings.GetValue("nick") : "";
		debug = settings.HasValue("debug") ? bool.Parse(settings.GetValue("debug")) : false;

		ircWindow = new IRCWindow(nick);
		ircWindow.channelClosedEvent += channelClosed;
		ircWindow.onUserCommandEntered += (e) => handleUserCommand(e.command);
		ircWindow.hidden = IRC_WINDOW_HIDDEN;

		initCommandHandlers();
		initUserCommandHandlers();

		client = new IRCClient();
		client.onCommandReceived += (e) => handleServerCommand(e.command);
		client.onCommandSent += (e) => logSendCommand(e.command);
		client.onConnect += () => ircWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Connecting to server " + host + ":" + port + "...");
		client.onConnected += () => ircWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Server connection established.");
		client.onDisconnected += () => ircWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Disconnected from server.");

		if ((host != null) && (port > 0) && (nick != "")) {
			client.connect(host, port, user, serverPassword, nick);
		} else {
			ircWindow.addToChannel("IRC Plugin", "*", "IRC plugin not configured, not connecting to IRC server.");
			ircWindow.addToChannel("IRC Plugin", "*", "Edit irc.cfg and restart KSP.");
		}

		windowButton = ToolbarManager.Instance.add("irc", "irc");
		windowButton.TexturePath = "blizzy/KSPIRC/button-regular";
		windowButton.ToolTip = "IRC";
		windowButton.Visibility = new GameScenesVisibility(GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPH, GameScenes.TRACKSTATION);
		windowButton.OnClick += (e) => toggleIRCWindow();

		versionWWW = new WWW("http://blizzy.de/kspirc/version.txt");
	}

	public void OnDestroy() {
		windowButton.Destroy();
	}

	#region gui

	public void OnGUI() {
		// auto-hide window
		if (!windowButton.Visibility.Visible) {
			ircWindow.hidden = true;
		}

		if (ircWindow.hidden && (ircWindow.anyChannelsHighlightedPrivateMessage || showNewVersionOnButton)) {
			windowButton.TexturePath = "blizzy/KSPIRC/button-pm";
			windowButton.Important = true;
		} else {
			if (ircWindow.hidden && ircWindow.anyChannelsHighlightedMessage) {
				windowButton.TexturePath = "blizzy/KSPIRC/button-message";
			} else if (ircWindow.hidden && ircWindow.anyChannelsHighlightedJoin) {
				windowButton.TexturePath = "blizzy/KSPIRC/button-join";
			} else {
				windowButton.TexturePath = "blizzy/KSPIRC/button-regular";
			}
			windowButton.Important = false;
		}

		ircWindow.draw();
	}

	private void toggleIRCWindow() {
		ircWindow.hidden = !ircWindow.hidden;

		// reset this as soon as the window is opened
		if (!ircWindow.hidden) {
			showNewVersionOnButton = false;
		}
	}

	#endregion


	public void Update() {
		client.update();
		checkForNewVersion();
	}

	private void logSendCommand(IRCCommand cmd) {
		if (debug) {
			ircWindow.addToChannel(DEBUG_CHANNEL_HANDLE, "CLIENT", cmd.ToString());
		}
	}

	private void channelClosed(ChannelEvent e) {
		if (e.handle.StartsWith("#")) {
			client.send(new IRCCommand(null, "PART", e.handle));
		}
	}

	private void checkForNewVersion() {
		if ((ircWindow.newVersionAvailable == null) && String.IsNullOrEmpty(versionWWW.error) && versionWWW.isDone) {
			try {
				Debug.Log("version found: " + versionWWW.text);
				long ver = long.Parse(versionWWW.text);
				ircWindow.newVersionAvailable = ver > VERSION;
				if (ircWindow.newVersionAvailable == true) {
					Debug.Log("new version found");
					showNewVersionOnButton = true;
				}
			} catch (Exception) {
				// ignore
			}
		}
	}


	#region server commands

	private void initCommandHandlers() {
		IRCCommandHandler ignoreServerCommand = (cmd) => {};

		serverCommandHandlers.Add("JOIN", serverCommandJOIN);
		serverCommandHandlers.Add("KICK", serverCommandKICK);
		serverCommandHandlers.Add("MODE", serverCommandMODE);
		serverCommandHandlers.Add("NICK", serverCommandNICK);
		serverCommandHandlers.Add("NOTICE", serverCommandNOTICE);
		serverCommandHandlers.Add("PART", serverCommandPART);
		serverCommandHandlers.Add("PING", serverCommandPING);
		serverCommandHandlers.Add("PONG", ignoreServerCommand);
		serverCommandHandlers.Add("PRIVMSG", serverCommandPRIVMSG);
		serverCommandHandlers.Add("QUIT", serverCommandQUIT);
		serverCommandHandlers.Add("TOPIC", serverCommandTOPIC);
		serverCommandHandlers.Add("332", serverCommandTopic);
		serverCommandHandlers.Add("353", serverCommandNameReply);
		serverCommandHandlers.Add("366", serverCommandEndOfNames);
	}

	private void handleServerCommand(IRCCommand cmd) {
		bool unknown = !serverCommandHandlers.ContainsKey(cmd.command);

		if (debug) {
			ircWindow.addToChannel(DEBUG_CHANNEL_HANDLE, "SERVER", (unknown ? "(unknown) " : "") + cmd);
		}

		if (!unknown) {
			serverCommandHandlers[cmd.command](cmd);
		}
	}

	private void serverCommandPING(IRCCommand cmd) {
		client.send(new IRCCommand(null, "PONG", cmd.parameters[0]));
	}

	private void serverCommandNOTICE(IRCCommand cmd) {
		ircWindow.addToChannel(NOTICE_CHANNEL_HANDLE, cmd.shortPrefix ?? "SERVER", cmd.parameters.Last());
	}

	private void serverCommandPRIVMSG(IRCCommand cmd) {
		string handle = cmd.parameters[0].StartsWith("#") ? cmd.parameters[0] : cmd.shortPrefix;
		if (cmd is CTCPCommand) {
			CTCPCommand c = (CTCPCommand) cmd;
			if (c.ctcpCommand == "ACTION") {
				ircWindow.addToChannel(handle, "*", cmd.shortPrefix + " " + c.ctcpParameters);
			} else if ((c.ctcpCommand == "VERSION") && !handle.StartsWith("#")) {
				if (c.ctcpParameters == null) {
					ircWindow.addToChannel(handle, "*", "VERSION");
					client.send(new CTCPCommand(null, handle, "VERSION", "Internet Relay Chat Plugin " + FULL_VERSION + " for Kerbal Space Program"));
				} else {
					ircWindow.addToChannel(handle, "*", handle + " uses client: " + c.ctcpParameters);
				}
			}
		} else {
			ircWindow.addToChannel(handle, cmd.shortPrefix, cmd.parameters.Last());
		}
	}

	private void serverCommandNameReply(IRCCommand cmd) {
		string[] names = cmd.parameters.Last().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		ircWindow.addChannelNames(cmd.parameters[cmd.parameters.Length - 2], names);
	}

	private void serverCommandEndOfNames(IRCCommand cmd) {
		ircWindow.endOfChannelNames(cmd.parameters[cmd.parameters.Length - 2]);
	}

	private void serverCommandTopic(IRCCommand cmd) {
		ircWindow.setChannelTopic(cmd.parameters[cmd.parameters.Length - 2], cmd.parameters.Last());
		ircWindow.addToChannel(cmd.parameters[cmd.parameters.Length - 2], "*", "Channel topic is: " + cmd.parameters.Last());
	}

	private void serverCommandJOIN(IRCCommand cmd) {
		ircWindow.addToChannel(cmd.parameters[0], "*", cmd.shortPrefix + " has joined " + cmd.parameters[0], cmd);
		ircWindow.addSingleChannelName(cmd.parameters[0], cmd.shortPrefix);
	}

	private void serverCommandKICK(IRCCommand cmd) {
		string text = cmd.shortPrefix + " kicked " + cmd.parameters[1] + " from " + cmd.parameters[0];
		if (cmd.parameters.Length > 2) {
			text += " (" + cmd.parameters[2] + ")";
		}
		ircWindow.addToChannel(cmd.parameters[0], "*", text);
		ircWindow.removeChannelName(cmd.parameters[0], cmd.parameters[1]);
	}

	private void serverCommandPART(IRCCommand cmd) {
		if (cmd.shortPrefix != ircWindow.name) {
			string text = cmd.shortPrefix + " has left " + cmd.parameters[0];
			if (cmd.parameters.Length > 1) {
				text += " (" + cmd.parameters[1] + ")";
			}
			ircWindow.addToChannel(cmd.parameters[0], "*", text, cmd);
			ircWindow.removeChannelName(cmd.parameters[0], cmd.shortPrefix);
		}
	}

	private void serverCommandQUIT(IRCCommand cmd) {
		string text = cmd.shortPrefix + " has quit";
		if (cmd.parameters.Length > 0) {
			text += " (" + cmd.parameters[0] + ")";
		}
		foreach (string handle in ircWindow.getChannelsContainingName(cmd.shortPrefix)) {
			ircWindow.addToChannel(handle, "*", text, cmd);
			ircWindow.removeChannelName(handle, cmd.shortPrefix);
		}
	}

	private void serverCommandNICK(IRCCommand cmd) {
		string oldName = cmd.shortPrefix;
		string newName = cmd.parameters.Last();
		foreach (string handle in ircWindow.getChannelsContainingName(oldName)) {
			ircWindow.renameInChannel(handle, oldName, newName);
			ircWindow.addToChannel(handle, "*", oldName + " is now known as " + newName);
		}
	}

	private void serverCommandMODE(IRCCommand cmd) {
		// channel mode
		if (cmd.parameters[0].StartsWith("#")) {
			string channel = cmd.parameters[0];
			string mode = cmd.parameters[1];
			if ((mode == "+o") || (mode == "-o") || (mode == "+v") || (mode == "-v")) {
				string name = cmd.parameters[2];
				ircWindow.changeUserModeInChannel(channel, name, mode);
				ircWindow.addToChannel(channel, "*", cmd.shortPrefix + " sets mode " + mode + " on " + name);
			}
		}
	}

	private void serverCommandTOPIC(IRCCommand cmd) {
		string topic = null;
		if (cmd.parameters.Length > 1) {
			topic = cmd.parameters.Last();
		}
		ircWindow.setChannelTopic(cmd.parameters[0], topic);
		ircWindow.addToChannel(cmd.parameters[0], "*", cmd.shortPrefix + " sets channel topic to: " + (topic ?? ""));
	}


	#endregion


	#region user commands

	private void initUserCommandHandlers() {
		userCommandHandlers.Add("DEOP", userCommandDEOP);
		userCommandHandlers.Add("DEVOICE", userCommandDEVOICE);
		userCommandHandlers.Add("J", userCommandJ);
		userCommandHandlers.Add("KICK", userCommandKICK);
		userCommandHandlers.Add("ME", userCommandME);
		userCommandHandlers.Add("MSG", userCommandMSG);
		userCommandHandlers.Add("OP", userCommandOP);
		userCommandHandlers.Add("TOPIC", userCommandTOPIC);
		userCommandHandlers.Add("VOICE", userCommandVOICE);
	}

	private void handleUserCommand(UserCommand cmd) {
		if (userCommandHandlers.ContainsKey(cmd.command)) {
			userCommandHandlers[cmd.command](cmd);
		} else {
			client.send(cmd.command + " " + cmd.parameters);
		}
	}

	private void userCommandME(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if (cmd.parameters.Length > 0) {
			client.send(new CTCPCommand(null, handle, "ACTION", cmd.parameters));
			ircWindow.addToChannel(handle, "*", ircWindow.name + " " + cmd.parameters);
		}
	}

	private void userCommandMSG(UserCommand cmd) {
		string[] parts = cmd.parameters.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 2) {
			client.send(new IRCCommand(null, "PRIVMSG", parts[0], parts[1]));
			ircWindow.addToChannel(parts[0], ircWindow.name, parts[1]);
		}
	}

	private void userCommandJ(UserCommand cmd) {
		handleUserCommand(new UserCommand("JOIN", cmd.parameters));
	}

	private void userCommandTOPIC(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#")) {
			client.send(new IRCCommand(null, "TOPIC", handle, cmd.parameters));
		}
	}

	private void userCommandOP(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#")) {
			sendUserChannelMode(handle, cmd.parameters, "+o");
		}
	}

	private void userCommandDEOP(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#")) {
			sendUserChannelMode(handle, cmd.parameters, "-o");
		}
	}

	private void userCommandVOICE(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#")) {
			sendUserChannelMode(handle, cmd.parameters, "+v");
		}
	}

	private void userCommandDEVOICE(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#")) {
			sendUserChannelMode(handle, cmd.parameters, "-v");
		}
	}

	private void sendUserChannelMode(string handle, string name, string mode) {
		client.send(new IRCCommand(null, "MODE", handle, mode, name));
	}

	private void userCommandKICK(UserCommand cmd) {
		string handle = ircWindow.getCurrentChannelName();
		string[] parts = cmd.parameters.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length > 0) {
			if (parts.Length == 1) {
				client.send(new IRCCommand(null, "KICK", handle, parts[0]));
			} else {
				client.send(new IRCCommand(null, "KICK", handle, parts[0], parts.Last()));
			}
		}
	}

	#endregion
}
