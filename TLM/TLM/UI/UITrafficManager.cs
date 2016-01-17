using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI {
	public class UITrafficManager : UIPanel {
		//private static UIState _uiState = UIState.None;

		private static UIButton _buttonSwitchTraffic;
		private static UIButton _buttonPrioritySigns;
		private static UIButton _buttonManualControl;
		private static UIButton _buttonTimedMain;
		private static UIButton _buttonLaneChange;
		//private static UIButton _buttonLaneRestrictions;
		private static UIButton _buttonClearTraffic;
		private static UIButton _buttonToggleDespawn;

		public static TrafficLightTool TrafficLightTool;

		public override void Start() {
			if (LoadingExtension.Instance == null) {
				Log.Error("UITrafficManager.Start(): LoadingExtension is null.");
				return;
			}
			TrafficLightTool = LoadingExtension.Instance.TrafficLightTool;

			backgroundSprite = "GenericPanel";
			color = new Color32(75, 75, 135, 255);
			width = 250;
			height = LoadingExtension.IsPathManagerCompatible ? 310 : 230;
			relativePosition = new Vector3(85f, 80f);

			UILabel title = AddUIComponent<UILabel>();
			title.text = "Version 1.4.2";
			title.relativePosition = new Vector3(65.0f, 5.0f);

			int y = 30;
			_buttonSwitchTraffic = _createButton(Translation.GetString("Switch_traffic_lights"), new Vector3(35f, y), clickSwitchTraffic);
			y += 40;
			_buttonPrioritySigns = _createButton(Translation.GetString("Add_priority_signs"), new Vector3(35f, y), clickAddPrioritySigns);
			y += 40;
			_buttonManualControl = _createButton(Translation.GetString("Manual_traffic_lights"), new Vector3(35f, y), clickManualControl);
			y += 40;
			_buttonTimedMain = _createButton(Translation.GetString("Timed_traffic_lights"), new Vector3(35f, y), clickTimedAdd);
			y += 40;

			if (LoadingExtension.IsPathManagerCompatible) {
				_buttonLaneChange = _createButton(Translation.GetString("Change_lane_arrows"), new Vector3(35f, y), clickChangeLanes);
				y += 40;
				//buttonLaneRestrictions = _createButton("Road Restrictions", new Vector3(35f, 230f), clickLaneRestrictions);
			}

			_buttonClearTraffic = _createButton(Translation.GetString("Clear_Traffic"), new Vector3(35f, y), clickClearTraffic);
			y += 40;

			if (LoadingExtension.IsPathManagerCompatible) {
				_buttonToggleDespawn = _createButton(LoadingExtension.Instance.DespawnEnabled ? Translation.GetString("Disable_despawning") : Translation.GetString("Enable_despawning"), new Vector3(35f, y), ClickToggleDespawn);
				y += 40;
			}
		}

		private UIButton _createButton(string text, Vector3 pos, MouseEventHandler eventClick) {
			var button = AddUIComponent<UIButton>();
			button.width = 190;
			button.height = 30;
			button.normalBgSprite = "ButtonMenu";
			button.disabledBgSprite = "ButtonMenuDisabled";
			button.hoveredBgSprite = "ButtonMenuHovered";
			button.focusedBgSprite = "ButtonMenu";
			button.pressedBgSprite = "ButtonMenuPressed";
			button.textColor = new Color32(255, 255, 255, 255);
			button.playAudioEvents = true;
			button.text = text;
			button.relativePosition = pos;
			button.eventClick += eventClick;

			return button;
		}

		private void clickSwitchTraffic(UIComponent component, UIMouseEventParameter eventParam) {
			if (TrafficLightTool.getToolMode() != ToolMode.SwitchTrafficLight) {
				_buttonSwitchTraffic.focusedBgSprite = "ButtonMenuFocused";
				TrafficLightTool.SetToolMode(ToolMode.SwitchTrafficLight);
			} else {
				_buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}

		private void clickAddPrioritySigns(UIComponent component, UIMouseEventParameter eventParam) {
			Log.Message("Priority Sign Clicked.");
			if (TrafficLightTool.getToolMode() != ToolMode.AddPrioritySigns) {
				_buttonPrioritySigns.focusedBgSprite = "ButtonMenuFocused";
				TrafficLightTool.SetToolMode(ToolMode.AddPrioritySigns);
			} else {
				_buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}

		private void clickManualControl(UIComponent component, UIMouseEventParameter eventParam) {
			if (TrafficLightTool.getToolMode() != ToolMode.ManualSwitch) {
				_buttonManualControl.focusedBgSprite = "ButtonMenuFocused";
				TrafficLightTool.SetToolMode(ToolMode.ManualSwitch);
			} else {
				_buttonManualControl.focusedBgSprite = "ButtonMenu";
				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}

		private void clickTimedAdd(UIComponent component, UIMouseEventParameter eventParam) {
			if (TrafficLightTool.getToolMode() != ToolMode.TimedLightsSelectNode && TrafficLightTool.getToolMode() != ToolMode.TimedLightsShowLights) {
				_buttonTimedMain.focusedBgSprite = "ButtonMenuFocused";
				TrafficLightTool.SetToolMode(ToolMode.TimedLightsSelectNode);
			} else {
				_buttonTimedMain.focusedBgSprite = "ButtonMenu";
				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}

		/// <summary>
		/// Removes the focused sprite from all menu buttons
		/// </summary>
		public static void deactivateButtons() {
			if (_buttonSwitchTraffic != null)
				_buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
			if (_buttonPrioritySigns != null)
				_buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
			if (_buttonManualControl != null)
				_buttonManualControl.focusedBgSprite = "ButtonMenu";
			if (_buttonTimedMain != null)
				_buttonTimedMain.focusedBgSprite = "ButtonMenu";
			if (_buttonLaneChange != null)
				_buttonLaneChange.focusedBgSprite = "ButtonMenu";
			//_buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";
			if (_buttonClearTraffic != null)
				_buttonClearTraffic.focusedBgSprite = "ButtonMenu";
			if (_buttonToggleDespawn != null)
				_buttonToggleDespawn.focusedBgSprite = "ButtonMenu";
		}

		private void clickClearTraffic(UIComponent component, UIMouseEventParameter eventParam) {
			TrafficLightTool.SetToolMode(ToolMode.None);

			TrafficPriority.ClearTraffic();
		}

		private static void ClickToggleDespawn(UIComponent component, UIMouseEventParameter eventParam) {
			TrafficLightTool.SetToolMode(ToolMode.None);

			LoadingExtension.Instance.DespawnEnabled = !LoadingExtension.Instance.DespawnEnabled;

			if (LoadingExtension.IsPathManagerCompatible) {
				_buttonToggleDespawn.text = LoadingExtension.Instance.DespawnEnabled
					? Translation.GetString("Disable_despawning")
					: Translation.GetString("Enable_despawning");
			}
		}

		private void clickChangeLanes(UIComponent component, UIMouseEventParameter eventParam) {
			if (TrafficLightTool.getToolMode() != ToolMode.LaneChange) {
				if (LoadingExtension.IsPathManagerCompatible) {
					_buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";
				}

				TrafficLightTool.SetToolMode(ToolMode.LaneChange);
			} else {
				if (LoadingExtension.IsPathManagerCompatible) {
					_buttonLaneChange.focusedBgSprite = "ButtonMenu";
				}

				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}

		/*protected virtual void ClickLaneRestrictions(UIComponent component, UIMouseEventParameter eventParam) {
			if (TrafficLightTool.getToolMode() != ToolMode.LaneRestrictions) {
				_buttonLaneRestrictions.focusedBgSprite = "ButtonMenuFocused";
				TrafficLightTool.SetToolMode(ToolMode.LaneRestrictions);
			} else {
				_buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";
				TrafficLightTool.SetToolMode(ToolMode.None);
			}
		}*/

		public override void Update() {
			switch (TrafficLightTool.getToolMode()) {
				case ToolMode.None: _basePanel(); break;
				case ToolMode.SwitchTrafficLight: _switchTrafficPanel(); break;
				case ToolMode.AddPrioritySigns: _addStopSignPanel(); break;
				case ToolMode.ManualSwitch: _manualSwitchPanel(); break;
				case ToolMode.TimedLightsSelectNode: _timedControlNodesPanel(); break;
				case ToolMode.TimedLightsShowLights: _timedControlLightsPanel(); break;
				case ToolMode.LaneChange: _laneChangePanel(); break;
			}
		}

		private void _basePanel() {

		}

		private void _switchTrafficPanel() {

		}

		private void _addStopSignPanel() {

		}

		private void _manualSwitchPanel() {

		}

		private void _timedControlNodesPanel() {
		}

		private void _timedControlLightsPanel() {
		}

		private void _laneChangePanel() {
			try {
				if (TrafficLightTool.SelectedSegment == 0) return;
				var instance = Singleton<NetManager>.instance;

				var segment = instance.m_segments.m_buffer[TrafficLightTool.SelectedSegment];

				var info = segment.Info;

				var num2 = segment.m_lanes;
				var num3 = 0;

				var dir = NetInfo.Direction.Forward;
				if (segment.m_startNode == TrafficLightTool.SelectedNode)
					dir = NetInfo.Direction.Backward;
				var dir3 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

				while (num3 < info.m_lanes.Length && num2 != 0u) {
					if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian && info.m_lanes[num3].m_direction == dir3) {
						//segmentLights[num3].Show();
						//segmentLights[num3].relativePosition = new Vector3(35f, (float)(xPos + (offsetIdx * 40f)));
						//segmentLights[num3].text = ((NetLane.Flags)instance.m_lanes.m_buffer[num2].m_flags & ~NetLane.Flags.Created).ToString();

						//if (segmentLights[num3].containsMouse)
						//{
						//    if (Input.GetMouseButton(0) && !segmentMouseDown)
						//    {
						//        switchLane(num2);
						//        segmentMouseDown = true;

						//        if (
						//            !TrafficPriority.isPrioritySegment(TrafficLightTool.SelectedNode,
						//                TrafficLightTool.SelectedSegment))
						//        {
						//            TrafficPriority.addPrioritySegment(TrafficLightTool.SelectedNode, TrafficLightTool.SelectedSegment, PrioritySegment.PriorityType.None);
						//        }
						//    }
						//}
					}

					num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
					num3++;
				}
			} catch (Exception e) {
				Log.Error("Exception in _laneChangePanel: " + e.ToString());
			}
		}
	}
}
