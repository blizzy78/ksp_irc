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

class ChannelGUI {
	private struct BufferEntry {
		public readonly string sender;
		public readonly string text;
		public readonly bool link;

		public BufferEntry(string sender, string text) {
			this.sender = sender;
			this.text = text;

			string textLower = text.ToLower();
			link = textLower.Contains("http://") || textLower.Contains("https://");
		}
	}

	private const int MAX_BACK_BUFFER_LINES = 250;
	private const float MAX_NAME_WIDTH = 150;

	public string handle {
		get;
		private set;
	}
	public bool channelHighlightedPrivateMessage {
		get;
		private set;
	}
	public bool channelHighlightedMessage {
		get;
		private set;
	}
	public bool channelHighlightedJoin {
		get;
		private set;
	}
	public string topic;
	public bool namesHidden;

	private string name_;
	private string nameLower;
	public string name {
		get {
			return name_;
		}

		set {
			name_ = value;
			nameLower = value.ToLower();
		}
	}

	private bool hidden_;
	public bool hidden {
		get {
			return hidden_;
		}

		set {
			if (value != hidden_) {
				if (value) {
					lastSeenLineNeedsReset = true;

					// reset all styles
					stylesInitialized = false;
				} else {
					lastSeenLineNeedsReset = false;
				}

				hidden_ = value;
			}
		}
	}

	public event UserCommandHandler onUserCommandEntered;

	private bool highlightName;
	private string inputText = "";
	private List<BufferEntry> backBuffer = new List<BufferEntry>();
	private Vector2 backBufferScrollPosition;
	private Vector2 namesScrollPosition;
	private List<User> users = new List<User>();
	private bool gotAllNames = true;
	private float bufferWidth = -1;
	private float nicknameWidth = -1;
	private int unseenIdx = -1;
	private bool stylesInitialized;
	private GUIStyle nameStyle;
	private GUIStyle senderStyle;
	private GUIStyle textStyle;
	private GUIStyle textHighlightedStyle;
	private GUIStyle lastSeenLineStyle;
	private GUIStyle userCountStyle;
	private List<User> usersForTabCompletion = new List<User>();
	private User lastTabCompletionUser;
	private string inputTextBeforeTabCompletion;
	private string inputTextAfterTabCompletion;
	private bool keyDown;
	private bool lastSeenLineNeedsReset;

	public ChannelGUI(string handle, string name) {
		this.handle = handle;
		this.name = name;

		// prevent highlighting in "(Debug)" or "(Notice)" channels
		highlightName = handle.StartsWith("#");
	}

	public void draw() {
		initStyles();

		// reset highlights as soon as we draw anything
		channelHighlightedPrivateMessage = false;
		channelHighlightedMessage = false;
		channelHighlightedJoin = false;

		GUILayout.BeginHorizontal();
			// TODO: get rid of weird margin/padding around drawTextArea() when drawNames() is called
			//       (the margin/padding is not there if it isn't called)
			drawTextArea();

			if (!namesHidden && handle.StartsWith("#")) {
				drawNames();
			}
		GUILayout.EndHorizontal();

		// user has typed, reset tab completion
		if ((inputTextAfterTabCompletion != null) && (inputText != inputTextAfterTabCompletion)) {
			inputTextBeforeTabCompletion = null;
			inputTextAfterTabCompletion = null;
			lastTabCompletionUser = null;
		}

		if (!keyDown && (Event.current.type == EventType.KeyDown)) {
			if (GUI.GetNameOfFocusedControl() == "input") {
				string input = inputText.Trim();
				if ((Event.current.keyCode == KeyCode.Return) || (Event.current.keyCode == KeyCode.KeypadEnter) ||
					(Event.current.character == '\r') || (Event.current.character == '\n')) {

					if (input.Length > 0) {
						handleInput(input);
					}
					inputText = "";
					inputTextBeforeTabCompletion = null;
					inputTextAfterTabCompletion = null;
					lastTabCompletionUser = null;
				} else if ((Event.current.keyCode == KeyCode.Tab) || (Event.current.character == '\t')) {
					if (input.Length > 0) {
						handleTabCompletion();
					}
				}
			}

			keyDown = true;
		} else if (keyDown && (Event.current.type == EventType.KeyUp)) {
			keyDown = false;
		}

		if (Event.current.isKey &&
			((Event.current.keyCode == KeyCode.Tab) || (Event.current.character == '\t')) &&
			(GUI.GetNameOfFocusedControl() == "input")) {

			TextEditor editor = (TextEditor) GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
			editor.MoveTextEnd();

			// prevent tab cycling
			Event.current.Use();
		}
	}

	private void handleTabCompletion() {
		string input = (inputTextBeforeTabCompletion ?? inputText).Trim();
		string prefix = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last().ToLower();
		List<User> users = new List<User>((lastTabCompletionUser != null) ? usersForTabCompletion.Skip(usersForTabCompletion.IndexOf(lastTabCompletionUser) + 1) : usersForTabCompletion);
		// add list again for wrapping around
		users.AddRange(usersForTabCompletion);
		lastTabCompletionUser = users.FirstOrDefault(u => u.name.ToLower().StartsWith(prefix));
		if (lastTabCompletionUser != null) {
			inputTextBeforeTabCompletion = input;
			inputText = input.Substring(0, input.Length - prefix.Length) + lastTabCompletionUser.name + ", ";
			inputTextAfterTabCompletion = inputText;
		}
	}

	private void initStyles() {
		if (!stylesInitialized) {
			nameStyle = new GUIStyle(GUI.skin.label);
			nameStyle.wordWrap = false;
			nameStyle.margin = new RectOffset(0, 0, 0, 0);
			nameStyle.padding = new RectOffset(0, 0, 0, 0);

			senderStyle = new GUIStyle(nameStyle);
			senderStyle.normal.textColor = XKCDColors.BlueGrey;
			senderStyle.alignment = TextAnchor.UpperRight;
			senderStyle.margin = new RectOffset(0, 10, 1, 0);

			textStyle = new GUIStyle(GUI.skin.label);
			textStyle.alignment = TextAnchor.UpperLeft;
			textStyle.margin = new RectOffset(0, 0, 0, 0);
			textStyle.padding = new RectOffset(0, 0, 1, 0);

			textHighlightedStyle = new GUIStyle(textStyle);
			textHighlightedStyle.normal.textColor = Color.yellow;

			Texture2D lineTex = new Texture2D(1, 1);
			lineTex.SetPixel(0, 0, XKCDColors.BlueGrey);
			lineTex.Apply();
			lastSeenLineStyle = new GUIStyle(GUI.skin.label);
			lastSeenLineStyle.normal.background = lineTex;

			userCountStyle = new GUIStyle(GUI.skin.label);
			userCountStyle.alignment = TextAnchor.MiddleCenter;
			userCountStyle.wordWrap = false;

			stylesInitialized = true;
		}
	}

	private void drawTextArea() {
		GUILayout.BeginVertical();
			GUILayout.TextField(topic ?? "",
				(bufferWidth > 0) ?
					new GUILayoutOption[] {
						GUILayout.ExpandWidth(false),
						GUILayout.Width(bufferWidth),
						GUILayout.MaxWidth(bufferWidth)
					} :
					new GUILayoutOption[] {
						GUILayout.ExpandWidth(false),
						GUILayout.Width(10),
						GUILayout.MaxWidth(10)
					});

			drawBuffer();

			GUILayout.BeginHorizontal();
				GUILayout.Label(name, GUILayout.ExpandWidth(false));
				if (Event.current.type == EventType.Repaint) {
					nicknameWidth = GUILayoutUtility.GetLastRect().width;
				}

				GUI.SetNextControlName("input");
				inputText = GUILayout.TextField(inputText,
					((bufferWidth > 0) && (nicknameWidth > 0)) ?
						new GUILayoutOption[] {
							GUILayout.ExpandWidth(false),
							GUILayout.Width(bufferWidth - nicknameWidth - GUI.skin.label.margin.right),
							GUILayout.MaxWidth(bufferWidth - nicknameWidth - GUI.skin.label.margin.right)
						} :
						new GUILayoutOption[] {
							GUILayout.ExpandWidth(false),
							GUILayout.Width(10),
							GUILayout.MaxWidth(10)
						});
			GUILayout.EndHorizontal();
		GUILayout.EndVertical();
	}

	private void drawBuffer() {
		backBufferScrollPosition = GUILayout.BeginScrollView(backBufferScrollPosition, false, true,
			GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.textArea);

			float maxNameWidth = -1;
			foreach (BufferEntry entry in backBuffer) {
				float width = senderStyle.CalcSize(new GUIContent(entry.sender)).x;
				maxNameWidth = Mathf.Min(Mathf.Max(width, maxNameWidth), MAX_NAME_WIDTH);
			}

			bool lastSeenLineDrawn = false;
			int idx = 0;
			foreach (BufferEntry entry in backBuffer) {
				// draw "last seen" indicator
				if (!lastSeenLineDrawn && (idx == unseenIdx)) {
					if (idx > 0) {
						GUILayout.Label("", lastSeenLineStyle, GUILayout.Height(1), GUILayout.MaxHeight(1));
					}
					lastSeenLineDrawn = true;
				}

				GUILayout.BeginHorizontal();
					GUILayout.Label(entry.sender, senderStyle, GUILayout.Width(maxNameWidth), GUILayout.MaxWidth(maxNameWidth));
					GUILayout.Label(entry.text, highlightNickname(entry.sender, entry.text) ? textHighlightedStyle : textStyle);

					// handle clicking on links
					if (entry.link && Input.GetMouseButtonUp(0) &&
						(Event.current.type == EventType.Repaint) &&
						GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) {

						handleClick(entry);
					}
				GUILayout.EndHorizontal();

				idx++;
			}
		GUILayout.EndScrollView();
		if (Event.current.type == EventType.Repaint) {
			bufferWidth = GUILayoutUtility.GetLastRect().width;
		}
	}

	private void handleClick(BufferEntry entry) {
		string textLower = entry.text.ToLower();
		int pos = textLower.IndexOf("http://");
		if (pos < 0) {
			pos = textLower.IndexOf("https://");
		}
		if (pos >= 0) {
			int endPos = textLower.IndexOf(' ', pos);
			if (endPos < 0) {
				endPos = textLower.Length;
			}

			string link = entry.text.Substring(pos, endPos - pos);
			Application.OpenURL(link);
		}
	}

	private bool highlightNickname(string sender, string text) {
		return highlightName && (sender != "*") && text.ToLower().Contains(nameLower);
	}

	private void drawNames() {
		GUILayout.BeginVertical(GUILayout.Width(150), GUILayout.MaxWidth(150));
			GUILayout.Label(users.Count() + " users, " + users.Count(u => u.op) + " ops", userCountStyle);

			namesScrollPosition = GUILayout.BeginScrollView(namesScrollPosition, false, true,
				GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.textArea);

				foreach (User user in users) {
					GUILayout.Label(user.ToString(), nameStyle);
				}
			GUILayout.EndScrollView();
		GUILayout.EndVertical();
	}

	private void handleInput(string input) {
		if (onUserCommandEntered != null) {
			if (input.StartsWith("/")) {
				onUserCommandEntered(new UserCommandEvent(UserCommand.fromInput(input.Substring(1))));
			} else {
				onUserCommandEntered(new UserCommandEvent(new UserCommand("MSG", handle + " " + input)));
			}
		}
	}

	public void addToBuffer(string sender, string text, IRCCommand cmd = null) {
		User user = usersForTabCompletion.SingleOrDefault(u => u.name == sender);
		if (user != null) {
			usersForTabCompletion.Remove(user);
			usersForTabCompletion.Insert(0, user);
		}

		if (lastSeenLineNeedsReset) {
			unseenIdx = backBuffer.Count();
			lastSeenLineNeedsReset = false;
		}

		BufferEntry entry = new BufferEntry(sender, text);
		backBuffer.Add(entry);
		if (backBuffer.Count() > MAX_BACK_BUFFER_LINES) {
			backBuffer.RemoveRange(0, backBuffer.Count() - MAX_BACK_BUFFER_LINES);
			if (unseenIdx > -1) {
				unseenIdx--;
			}
		}

		if ((!handle.StartsWith("#") && !handle.StartsWith("(") && !handle.EndsWith(")")) || highlightNickname(sender, text)) {
			channelHighlightedPrivateMessage = true;
		}
		if ((cmd != null) &&
			((cmd.command == "JOIN") || (cmd.command == "PART") || (cmd.command == "QUIT"))) {

			channelHighlightedJoin = true;
		} else {
			channelHighlightedMessage = true;
		}

		backBufferScrollPosition = new Vector2(0, float.MaxValue);
	}

	public void addNames(string[] names) {
		if (gotAllNames) {
			users.Clear();
			usersForTabCompletion.Clear();
			gotAllNames = false;
		}

		IEnumerable<User> newUsers = names.ToList().ConvertAll(n => User.fromNameWithModes(n));
		users.AddRange(newUsers);
		usersForTabCompletion.AddRange(newUsers);
		sortNames(true);
	}

	public void endOfNames() {
		gotAllNames = true;
	}

	public void addSingleName(string name) {
		User newUser = User.fromNameWithModes(name);
		users.Add(newUser);
		usersForTabCompletion.Add(newUser);
		sortNames(true);
	}

	public void removeName(string name) {
		users.RemoveAll(u => u.name == name);
		usersForTabCompletion.RemoveAll(u => u.name == name);
	}

	public void rename(string oldName, string newName) {
		foreach (User user in users.Where(u => u.name == oldName)) {
			user.name = newName;
		}
		sortNames(true);
	}

	public bool containsName(string name) {
		return users.Any(u => u.name == name);
	}

	public void changeUserMode(string name, string mode) {
		foreach (User user in users.Where(u => u.name == name)) {
			if (mode == "+o") {
				user.op = true;
			} else if (mode == "-o") {
				user.op = false;
			} else if (mode == "+v") {
				user.voice = true;
			} else if (mode == "-v") {
				user.voice = false;
			}
		}
		sortNames(false);
	}

	private void sortNames(bool tabCompletion) {
		users.Sort(User.compareUsers);
		if (tabCompletion) {
			usersForTabCompletion.Sort(User.compareUsers);
			lastTabCompletionUser = null;
		}
	}

	public void windowResized() {
		nicknameWidth = -1;
		bufferWidth = -1;
	}
}

class User {
	public string name;
	public bool op;
	public bool voice;

	public User(string name, bool op, bool voice) {
		this.name = name;
		this.op = op;
		this.voice = voice;
	}

	public override string ToString() {
		return (op ? "@" : "") + (voice ? "+" : "") + name;
	}

	public static User fromNameWithModes(string name) {
		bool op = false;
		if (name.StartsWith("@")) {
			op = true;
			name = name.Substring(1);
		}
		bool voice = false;
		if (name.StartsWith("+")) {
			voice = true;
			name = name.Substring(1);
		}
		return new User(name, op, voice);
	}

	public static int compareUsers(User u1, User u2) {
		if (u1.op && !u2.op) {
			return -1;
		}
		if (!u1.op && u2.op) {
			return 1;
		}
		if (u1.voice && !u2.voice) {
			return -1;
		}
		if (!u1.voice && u2.voice) {
			return 1;
		}
		return StringComparer.CurrentCultureIgnoreCase.Compare(u1.name, u2.name);
	}
}
