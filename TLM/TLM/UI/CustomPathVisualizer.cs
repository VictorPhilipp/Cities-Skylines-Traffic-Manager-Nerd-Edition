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

using ColossalFramework;
using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using TrafficManager.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrafficManager.UI {
	public class CustomPathVisualizer: PathVisualizer {
		public enum eVehicleTypeFilter {
			eNoFilter,
			eFilterHearse,
			eFilterGarbageTruck,
		}
		public void addFilter(eVehicleTypeFilter filter) {
			bool l_pathsHidden;
			while (!Monitor.TryEnter(m_eFiltersList, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				if (filter == eVehicleTypeFilter.eNoFilter) {
					m_eFiltersList = new List<eVehicleTypeFilter>();
				} else {
					if (m_eFiltersList.Contains(eVehicleTypeFilter.eNoFilter)) {
						m_eFiltersList.Remove(eVehicleTypeFilter.eNoFilter);
					}
				}
				if (!m_eFiltersList.Contains(filter)) {
					m_eFiltersList.Add(filter);
				}
				l_pathsHidden = m_fPathsHidden;
			} finally {
				Monitor.Exit(m_eFiltersList);
			}
			if (!l_pathsHidden) {
				HideAllPaths();
				ShowAllPaths();
			}
		}
		public void removeFilter(eVehicleTypeFilter filter) {
			bool l_pathsHidden;
			if (filter == eVehicleTypeFilter.eNoFilter) {
				return;
			}
			while (!Monitor.TryEnter(m_eFiltersList, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				if (m_eFiltersList.Contains(filter)) {
					m_eFiltersList.Remove(filter);
				}
				if (m_eFiltersList.Count == 0) {
					m_eFiltersList.Add(eVehicleTypeFilter.eNoFilter);
				}
				l_pathsHidden = m_fPathsHidden;
			} finally {
				Monitor.Exit(m_eFiltersList);
			}
			if (!l_pathsHidden) {
				HideAllPaths();
				ShowAllPaths();
			}
		}
		public void CustomAddInstance(ushort vehicleId) {
			InstanceID l_instanceID = new InstanceID();
			FastList<InstanceID> l_instanceIDs = new FastList<InstanceID>();
			bool l_pathsHidden;
			l_instanceID.Vehicle = vehicleId;
			while (!Monitor.TryEnter(m_paths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				if (!m_paths.ContainsKey(vehicleId)) {
					m_paths.Add(vehicleId, l_instanceID);
				}
				l_pathsHidden = m_fPathsHidden;
			} finally {
				Monitor.Exit(m_paths);
			}
			if (!l_pathsHidden) {
				if (checkIfFiltered(vehicleId)) {
					l_instanceIDs.Add(l_instanceID);
					AddPathsToPathVisualizer(l_instanceIDs);
				}
			}
		}
		private bool checkIfFiltered(ushort vehicleId) {
			VehicleManager vehManager = Singleton<VehicleManager>.instance;
			string l_ai = vehManager.m_vehicles.m_buffer[vehicleId].Info.m_vehicleAI.GetType().ToString();
			bool add = true;
			if (!m_eFiltersList.Contains(eVehicleTypeFilter.eNoFilter)) {
				add = false;
				switch (l_ai) {
					case "HearseAI":
						if (m_eFiltersList.Contains(eVehicleTypeFilter.eFilterHearse)) {
							add = true;
						}
						break;
					case "GarbageTruckAI":
						if (m_eFiltersList.Contains(eVehicleTypeFilter.eFilterGarbageTruck)) {
							add = true;
						}
						break;
				}
			}
			return add;
		}
		private void AddPathsToPathVisualizer(FastList<InstanceID> instanceIDs) {

			MethodInfo dynMethod = m_pathVisualizer.GetType().GetMethod("AddInstance", BindingFlags.NonPublic | BindingFlags.Instance);
			foreach (InstanceID instanceID in instanceIDs) {
				dynMethod.Invoke(m_pathVisualizer, new object[] { instanceID });
				Log._Debug($"CustomPathVisualizer::CustomAddInstance: InstanceID {instanceID} added");
			}
		}

		public void RemoveInstance(ushort vehicleId) {
			FastList<InstanceID> l_instanceID = new FastList<InstanceID>();
			while (!Monitor.TryEnter(m_paths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				if (!m_paths.ContainsKey(vehicleId)) {
					return;
				}
				l_instanceID.Add(m_paths[vehicleId]);
				m_paths.Remove(vehicleId);
			} finally {
				Monitor.Exit(m_paths);
			}
			RemovePathsFromPathVisualizer(l_instanceID);
		}

		private void RemovePathsFromPathVisualizer(FastList<InstanceID> instanceIDs) {
			FieldInfo fi_paths = m_pathVisualizer.GetType().GetField("m_paths", BindingFlags.NonPublic|BindingFlags.Instance);
			FieldInfo fi_removePaths = m_pathVisualizer.GetType().GetField("m_removePaths", BindingFlags.NonPublic | BindingFlags.Instance);
			Dictionary<InstanceID, Path> l_paths = (Dictionary < InstanceID, Path > ) fi_paths.GetValue(m_pathVisualizer);
			FastList<Path> l_removePaths = (FastList<Path>) fi_removePaths.GetValue(m_pathVisualizer);
			while (!Monitor.TryEnter(l_paths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				foreach (InstanceID eachInstance in instanceIDs) {
					if (l_paths.ContainsKey(eachInstance)) {
						l_removePaths.Add(l_paths[eachInstance]);
						Log._Debug($"CustomPathVisualizer::RemovePathForPathVisualizer: InstanceID {eachInstance} queued for removal");
					}
				}
			} finally {
				Monitor.Exit(l_paths);
			}
		}

		public void HideAllPaths() {
			while (!Monitor.TryEnter(m_paths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				m_fPathsHidden = true;
			} finally {
				Monitor.Exit(m_paths);
			}

			m_pathVisualizer.DestroyPaths();
			Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
			Singleton<GuideManager>.instance.InfoViewUsed();
		}
		public void ShowAllPaths() {
			FastList<InstanceID> l_instanceIDs = new FastList<InstanceID>();
			while (!Monitor.TryEnter(m_paths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				m_fPathsHidden = false;
				foreach (KeyValuePair<ushort, InstanceID> l_path in m_paths) {
					if (checkIfFiltered(l_path.Key)) {
						l_instanceIDs.Add(l_path.Value);
					}
				}
			} finally {
				Monitor.Exit(m_paths);
			}
			Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.TrafficRoutes, InfoManager.SubInfoMode.Default);
			Singleton<GuideManager>.instance.InfoViewUsed();
			AddPathsToPathVisualizer(l_instanceIDs);
		}

		public static CustomPathVisualizer Instance { get; private set; } = null;

		static CustomPathVisualizer() {
			Instance = new CustomPathVisualizer();
		}

		private CustomPathVisualizer() {
			m_pathVisualizer = Singleton<NetManager>.instance.PathVisualizer;
			m_paths = new Dictionary<ushort, InstanceID>();
			m_eFiltersList = new List<eVehicleTypeFilter>();
			m_eFiltersList.Add(eVehicleTypeFilter.eNoFilter);
			fInitialized = true;
		}

		public bool isInitialized() {
			return fInitialized;
		}

		PathVisualizer m_pathVisualizer;
		bool fInitialized = false;
		Dictionary<ushort, InstanceID> m_paths;
		bool m_fPathsHidden = true;
		List<eVehicleTypeFilter> m_eFiltersList;
	}
}
