/*
The MIT License (MIT)
Copyright (c) 2018 Terry Hardie
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public class VehicleFilterMenuPanel: UIPanel {
		private static UICheckBox _goToNoFilterCheckbox = null;
		private static UICheckBox _goToFilterHearseCheckbox = null;
		private static UICheckBox _goToFilterGarbageTruckCheckbox = null;

		private static List<UICheckBox> _allFilterCheckboxes;

		private static UITextField _goToField = null;

		public static UILabel title;

		public override void Start() {
			autoLayout = true;
			autoLayoutDirection = LayoutDirection.Vertical;
			autoLayoutPadding.top = 5;
			autoLayoutPadding.left = 10;
			autoLayoutPadding.right = 10;
			_allFilterCheckboxes = new List<UICheckBox>();

			UIHelper panelHelper = new UIHelper(this);
			isVisible = false;

			backgroundSprite = "GenericPanel";
			color = new Color32(75, 75, 135, 255);
			width = Translation.getMenuWidth();
			height = 30;

			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			relativePosition = new Vector3(resolution.x - Translation.getMenuWidth() - 250f, 65f);

			title = AddUIComponent<UILabel>();
			title.text = "Path Vis Filter";
			title.relativePosition = new Vector3(50.0f, 5.0f);

			_goToNoFilterCheckbox = panelHelper.AddCheckbox("No Filter", true, onNoFilterCheckbox) as UICheckBox;
			_goToNoFilterCheckbox.Disable();

			height += 40;
			_goToFilterHearseCheckbox = panelHelper.AddCheckbox("Hearse", false, onFilterHearseCheckbox) as UICheckBox;
			height += 40;
			_allFilterCheckboxes.Add(_goToFilterHearseCheckbox);
			_goToFilterGarbageTruckCheckbox = panelHelper.AddCheckbox("Garbage Truck", false, onFilterGarbageTruckCheckbox) as UICheckBox;
			height += 40;
			_allFilterCheckboxes.Add(_goToFilterGarbageTruckCheckbox);
		}

		private static void checkIfNoFilterNeedsToBeChecked() {
			bool fOneChecked = false;
			foreach (UICheckBox thisCheckBox in _allFilterCheckboxes) {
				if (thisCheckBox.isChecked) {
					fOneChecked = true;
					break;
				}
			}
			if (!fOneChecked) {
				_goToNoFilterCheckbox.isChecked = true;
				_goToNoFilterCheckbox.Disable();
			}
		}

		private static void onNoFilterCheckbox(bool val) {
			if (val) {
				CustomPathVisualizer.Instance.addFilter(CustomPathVisualizer.eVehicleTypeFilter.eNoFilter);
				foreach (UICheckBox thisCheckBox in _allFilterCheckboxes) {
					thisCheckBox.isChecked = false;
				}
				_goToNoFilterCheckbox.Disable();
			}
		}
		private static void onFilterHearseCheckbox(bool val) {
			if (val) {
				CustomPathVisualizer.Instance.addFilter(CustomPathVisualizer.eVehicleTypeFilter.eFilterHearse);
				_goToNoFilterCheckbox.isChecked = false;
				_goToNoFilterCheckbox.Enable();
			} else {
				CustomPathVisualizer.Instance.removeFilter(CustomPathVisualizer.eVehicleTypeFilter.eFilterHearse);
				checkIfNoFilterNeedsToBeChecked();
			}
		}
		private static void onFilterGarbageTruckCheckbox(bool val) {
			if (val) {
				CustomPathVisualizer.Instance.addFilter(CustomPathVisualizer.eVehicleTypeFilter.eFilterGarbageTruck);
				_goToNoFilterCheckbox.isChecked = false;
				_goToNoFilterCheckbox.Enable();
			} else {
				CustomPathVisualizer.Instance.removeFilter(CustomPathVisualizer.eVehicleTypeFilter.eFilterGarbageTruck);
				checkIfNoFilterNeedsToBeChecked();
			}
		}


		private UITextField CreateTextField(string str, int y) {
			UITextField textfield = AddUIComponent<UITextField>();
			textfield.relativePosition = new Vector3(15f, y);
			textfield.horizontalAlignment = UIHorizontalAlignment.Left;
			textfield.text = str;
			textfield.textScale = 0.8f;
			textfield.color = Color.black;
			textfield.cursorBlinkTime = 0.45f;
			textfield.cursorWidth = 1;
			textfield.selectionBackgroundColor = new Color(233, 201, 148, 255);
			textfield.selectionSprite = "EmptySprite";
			textfield.verticalAlignment = UIVerticalAlignment.Middle;
			textfield.padding = new RectOffset(5, 0, 5, 0);
			textfield.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
			textfield.normalBgSprite = "TextFieldPanel";
			textfield.hoveredBgSprite = "TextFieldPanelHovered";
			textfield.focusedBgSprite = "TextFieldPanel";
			textfield.size = new Vector3(190, 30);
			textfield.isInteractive = true;
			textfield.enabled = true;
			textfield.readOnly = false;
			textfield.builtinKeyNavigation = true;
			textfield.width = Translation.getMenuWidth() - 30;
			return textfield;
		}

/*
		private UIButton _createCheckbox(string text, int y, MouseEventHandler eventClick) {
			var checkbox = AddUIComponent<UICheckBox>();
			checkbox.
			checkbox.textScale = 0.8f;
			checkbox.width = Translation.getMenuWidth() - 30;
			checkbox.height = 30;
			checkbox.normalBgSprite = "ButtonMenu";
			checkbox.disabledBgSprite = "ButtonMenuDisabled";
			checkbox.hoveredBgSprite = "ButtonMenuHovered";
			checkbox.focusedBgSprite = "ButtonMenu";
			checkbox.pressedBgSprite = "ButtonMenuPressed";
			checkbox.textColor = new Color32(255, 255, 255, 255);
			checkbox.playAudioEvents = true;
			checkbox.text = text;
			checkbox.relativePosition = new Vector3(15f, y);
			checkbox.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
				eventClick(component, eventParam);
				checkbox.Invalidate();
			};

			return checkbox;
		}
		*/


	}
}
