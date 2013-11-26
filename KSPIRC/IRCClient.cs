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
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using UnityEngine;

class IRCClient {
	private const long SERVER_PING_INTERVAL = 30000;

	public event IRCCommandHandler onCommandReceived;
	public event IRCCommandHandler onCommandSent;
	public event Callback onConnect;
	public event Callback onConnected;
	public event Callback onDisconnected;

	private string hostname;
	private int port;
	private string user;
	private string serverPassword;
	private string nickname;
	private TcpClient client;
	private NetworkStream stream;
	private byte[] buffer = new byte[10240];
	private StringBuilder textBuffer = new StringBuilder();
	private bool tryReconnect = true;
	private bool connected;
	private long lastServerPing = DateTime.UtcNow.Ticks / 10000;

	public void connect(string hostname, int port, string user, string serverPassword, string nickname) {
		this.hostname = hostname;
		this.port = port;
		this.user = user;
		this.serverPassword = serverPassword;
		this.nickname = nickname;
		connect();
	}

	private void connect() {
		doDisconnect();

		if (onConnect != null) {
			onConnect();
		}

		try {
			client = new TcpClient();
			client.Connect(hostname, port);
			stream = client.GetStream();

			if ((serverPassword != null) && (serverPassword != "")) {
				send(new IRCCommand("PASS", serverPassword));
			}
			send(new IRCCommand("NICK", nickname));
			send(new IRCCommand("USER", user ?? nickname, "8", "*", nickname));

			connected = true;

			if (onConnected != null) {
				onConnected();
			}
		} catch (Exception e) {
			Debug.LogException(e);
		}
	}

	public void disconnect() {
		tryReconnect = false;
		doDisconnect();
	}

	private void doDisconnect() {
		bool wasConnected = connected;

		if (stream != null) {
			try {
				send(new IRCCommand("QUIT", "Build. Fly. Dream."));
			} catch {
				// ignore
			}
		}

		if (stream != null) {
			stream.Close();
			stream = null;
		}
		if (client != null) {
			client.Close();
			client = null;
		}

		connected = false;
		textBuffer.Clear();

		if (wasConnected && (onDisconnected != null)) {
			onDisconnected();
		}
	}

	private void reconnect() {
		if (tryReconnect && connected) {
			try {
				tryReconnect = false;
				doDisconnect();
				connect();
			} finally {
				tryReconnect = true;
			}
		}
	}

	public void update() {
		if (connected) {
			try {
				if (stream.CanRead) {
					while (stream.DataAvailable) {
						int numBytes = stream.Read(buffer, 0, buffer.Length);
						string text = Encoding.UTF8.GetString(buffer, 0, numBytes);
						textBuffer.Append(text);
					}
				}
			} catch (SocketException) {
				reconnect();
			}

			if (textBuffer.Length > 0) {
				for (;;) {
					int pos = textBuffer.ToString().IndexOf("\r\n");
					if (pos >= 0) {
						string line = textBuffer.ToString().Substring(0, pos);
						textBuffer.Remove(0, pos + 2);

						if (onCommandReceived != null) {
							try {
								IRCCommand cmd = IRCCommand.fromLine(line);
								onCommandReceived(new IRCCommandEvent(cmd));
							} catch (ArgumentException e) {
								Debug.LogException(e);
							}
						}
					} else {
						break;
					}
				}
			}

			// send something to socket to potentially trigger SocketException elsewhere when reading
			// off the socket
			long now = DateTime.UtcNow.Ticks / 10000;
			if ((now - lastServerPing) >= SERVER_PING_INTERVAL) {
				lastServerPing = now;
				send("PING :" + now);
			}
		}
	}

	public void send(IRCCommand cmd) {
		if (onCommandSent != null) {
			onCommandSent(new IRCCommandEvent(cmd));
		}
		byte[] data = Encoding.UTF8.GetBytes(cmd.ToString() + "\r\n");
		try {
			stream.Write(data, 0, data.Length);
		} catch (SocketException) {
			reconnect();
		} catch (IOException) {
			reconnect();
		}
	}

	public void send(string cmdAndParams) {
		if (onCommandSent != null) {
			onCommandSent(new IRCCommandEvent(IRCCommand.fromLine(cmdAndParams)));
		}
		byte[] data = Encoding.UTF8.GetBytes(cmdAndParams + "\r\n");
		try {
			stream.Write(data, 0, data.Length);
		} catch (SocketException) {
			reconnect();
		} catch (IOException) {
			reconnect();
		}
	}
}
