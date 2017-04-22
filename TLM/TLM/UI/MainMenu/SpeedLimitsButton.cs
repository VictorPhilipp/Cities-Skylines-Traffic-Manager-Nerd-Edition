﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;

namespace TrafficManager.UI.MainMenu {
	public class SpeedLimitsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.SpeedLimits;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.SpeedLimits;
			}
		}

		public override string Tooltip {
			get {
				return "Speed_limits";
			}
		}
	}
}
