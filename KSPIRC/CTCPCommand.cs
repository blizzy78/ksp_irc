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

class CTCPCommand : IRCCommand {
	public readonly string ctcpCommand;
	public readonly string ctcpParameters;

	public CTCPCommand(string prefix, string handle, string ctcpCommand, string ctcpParameters) :
		base(prefix, "PRIVMSG", handle, "\u0001" + ctcpCommand + ((ctcpParameters != null) ? " " + ctcpParameters : "") + "\u0001") {

		this.ctcpCommand = ctcpCommand;
		this.ctcpParameters = ctcpParameters;
	}
}
