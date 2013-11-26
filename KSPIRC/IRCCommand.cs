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
using System.Text.RegularExpressions;
using UnityEngine;

class IRCCommand {
	private const string PREFIX = "[^ ]+";
	private const string SPACE = "[ ]+";
	private const string COMMAND = "[a-z]+|[0-9]{3}";
	private const string PARAM = "[^: ][^ ]*";
	private const string TRAILING = @"\:.*";
	private const string PATTERN = @"(?:(?<prefix>\:" + PREFIX + ")" + SPACE + ")?" + "(?<command>" + COMMAND + ")(?:" + SPACE + "(?<param>" + PARAM + "))*(?:" + SPACE + "(?<trailing>" + TRAILING + "))?";
	private static readonly Regex REGEX = new Regex("^" + PATTERN + "$", RegexOptions.IgnoreCase);

	public string prefix {
		get;
		private set;
	}
	public string shortPrefix {
		get {
			if (prefix != null) {
				int pos = prefix.IndexOf('!');
				if (pos >= 0) {
					return prefix.Substring(0, pos);
				} else {
					return prefix;
				}
			} else {
				return null;
			}
		}
	}
	public string command {
		get;
		private set;
	}
	public string[] parameters {
		get;
		private set;
	}

	public IRCCommand(string prefix, string command, params string[] parameters) {
		this.prefix = prefix;
		this.command = command;
		this.parameters = parameters.Where(p => p != null).ToArray();
	}

	public static IRCCommand fromLine(string line) {
		MatchCollection matches = REGEX.Matches(line);
		if (matches.Count > 0) {
			Match match = matches[0];
			GroupCollection groups = match.Groups;
			string prefix = groups["prefix"].Value;
			if (prefix == "") {
				prefix = null;
			} else {
				prefix = prefix.Substring(1);
			}
			string command = groups["command"].Value.ToUpper();
			List<string> parameters = new List<string>();
			foreach (Capture capture in groups["param"].Captures) {
				string parameter = capture.Value;
				if (parameter != "") {
					parameters.Add(parameter);
				}
			}
			string trailing = groups["trailing"].Value;
			if (trailing != "") {
				parameters.Add(trailing.Substring(1));
			}

			if ((command == "PRIVMSG") && (parameters.Count() >= 2)) {
				string lastParam = parameters.Last();
				if (lastParam.StartsWith("\u0001") && lastParam.EndsWith("\u0001")) {
					lastParam = lastParam.Substring(1, lastParam.Length - 2);
					int pos = lastParam.IndexOf(' ');
					if (pos >= 0) {
						return new CTCPCommand(prefix, parameters[0], lastParam.Substring(0, pos), lastParam.Substring(pos + 1));
					}
				}
			}

			return new IRCCommand(prefix, command, parameters.ToArray());
		} else {
			throw new ArgumentException("could not parse line: " + line);
		}
	}

	public override string ToString() {
		StringBuilder buf = new StringBuilder();
		if (prefix != null) {
			buf.Append(":").Append(prefix).Append(" ");
		}
		buf.Append(command);
		for (int i = 0; i < parameters.Length; i++) {
			string param = parameters[i];
			buf.Append(" ");
			// always send last parameter as trailing
			if (i == (parameters.Length - 1)) {
				buf.Append(":");
			}
			buf.Append(param);
		}
		return buf.ToString();
	}
}
