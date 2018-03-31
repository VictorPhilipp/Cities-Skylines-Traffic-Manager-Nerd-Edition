﻿using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Util;

namespace TrafficManager.Manager.Impl {
	public class JunctionRestrictionsManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.SegmentNodeConf>>, IJunctionRestrictionsManager {
		public static JunctionRestrictionsManager Instance { get; private set; } = new JunctionRestrictionsManager();

		protected class JunctionRestrictionsNodeWatcher : AbstractNodeGeometryObservingManager {
			private JunctionRestrictionsManager junctionRestrictionsManager;

			private SegmentFlags[] invalidSegmentFlags = null;

			public JunctionRestrictionsNodeWatcher(JunctionRestrictionsManager junctionRestrictionsManager) {
				this.junctionRestrictionsManager = junctionRestrictionsManager;
				invalidSegmentFlags = new Traffic.Data.SegmentFlags[NetManager.MAX_SEGMENT_COUNT];
			}

			public void WatchNode(ushort nodeId) {
				SubscribeToNodeGeometry(nodeId);
			}

			public void Reset() {
				for (int i = 0; i < invalidSegmentFlags.Length; ++i) {
					invalidSegmentFlags[i].Reset();
				}
				UnsubscribeFromAllNodeGeometries();
			}

			public void AddInvalidSegmentEndFlags(ushort segmentId, bool startNode, ref SegmentEndFlags endFlags) {
				if (startNode) {
					invalidSegmentFlags[segmentId].startNodeFlags = endFlags;
				} else {
					invalidSegmentFlags[segmentId].endNodeFlags = endFlags;
				}
			}

			protected override void HandleInvalidNode(NodeGeometry geometry) {
				
			}

			protected override void HandleValidNode(NodeGeometry geometry) {
				if (geometry.CurrentSegmentReplacement.oldSegmentEndId != null && geometry.CurrentSegmentReplacement.newSegmentEndId != null) {
					ISegmentEndId oldSegmentEndId = geometry.CurrentSegmentReplacement.oldSegmentEndId;
					ISegmentEndId newSegmentEndId = geometry.CurrentSegmentReplacement.newSegmentEndId;

					SegmentEndFlags flags;
					if (oldSegmentEndId.StartNode) {
						flags = invalidSegmentFlags[oldSegmentEndId.SegmentId].startNodeFlags;
					} else {
						flags = invalidSegmentFlags[oldSegmentEndId.SegmentId].endNodeFlags;
					}

					if (flags.IsDefault()) {
						return;
					}

					Log._Debug($"JunctionRestrictionsManager.NodeWatcher.HandleValidNode({geometry.NodeId}): Segment replacement detected: {oldSegmentEndId.SegmentId} -> {newSegmentEndId.SegmentId}\n" +
						$"Moving segmend end flags {flags} to new segment end."
					);

					junctionRestrictionsManager.SetSegmentEndFlags(newSegmentEndId.SegmentId, newSegmentEndId.StartNode, flags);
				}
			}
		}

		private JunctionRestrictionsNodeWatcher nodeWatcher = null;

		/// <summary>
		/// Holds junction restrictions for each segment end
		/// </summary>
		private SegmentFlags[] SegmentFlags = null;

		private JunctionRestrictionsManager() {
			SegmentFlags = new Traffic.Data.SegmentFlags[NetManager.MAX_SEGMENT_COUNT];
			nodeWatcher = new JunctionRestrictionsNodeWatcher(this);
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();

			Log._Debug($"Junction restrictions:");
			for (int i = 0; i < SegmentFlags.Length; ++i) {
				if (SegmentFlags[i].IsDefault()) {
					continue;
				}
				Log._Debug($"Segment {i}: {SegmentFlags[i]}");
			}
		}

		public bool MayHaveJunctionRestrictions(ushort nodeId) {
			NetNode.Flags flags = NetNode.Flags.None;
			Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				flags = node.m_flags;
				return true;
			});

			Log._Debug($"JunctionRestrictionsManager.MayHaveJunctionRestrictions({nodeId}): flags={(NetNode.Flags)flags}");

			if (! LogicUtil.CheckFlags((uint)flags, (uint)(NetNode.Flags.Created | NetNode.Flags.Deleted), (uint)NetNode.Flags.Created)) {
				return false;
			}

			return LogicUtil.CheckFlags((uint)flags, (uint)(NetNode.Flags.Junction | NetNode.Flags.Bend));
		}

		public bool HasJunctionRestrictions(ushort nodeId) {
			if (! Services.NetService.IsNodeValid(nodeId)) {
				return false;
			}

			bool ret = false;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (segmentId == 0) {
					return true;
				}

				bool startNode = segment.m_startNode == nodeId;
				bool isDefault = startNode
					? SegmentFlags[segmentId].startNodeFlags.IsDefault()
					: SegmentFlags[segmentId].endNodeFlags.IsDefault();

				if (! isDefault) {
					ret = true;
					return false;
				}

				return true;
			});

			return ret;
		}

		public void RemoveJunctionRestrictions(ushort nodeId) {
			Log._Debug($"JunctionRestrictionsManager.RemoveJunctionRestrictions({nodeId}) called.");
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (segmentId == 0) {
					return true;
				}

				if (segment.m_startNode == nodeId) {
					SegmentFlags[segmentId].startNodeFlags.Reset();
				} else {
					SegmentFlags[segmentId].endNodeFlags.Reset();
				}
				
				return true;
			});
		}

		public void RemoveJunctionRestrictionsIfNecessary() {
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				RemoveJunctionRestrictionsIfNecessary((ushort)nodeId);
			}
		}

		public void RemoveJunctionRestrictionsIfNecessary(ushort nodeId) {
			if (!MayHaveJunctionRestrictions(nodeId)) {
				RemoveJunctionRestrictions(nodeId);
			}
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			foreach (bool startNode in Constants.ALL_BOOL) {
				SegmentEndFlags flags = startNode
						? SegmentFlags[geometry.SegmentId].startNodeFlags
						: SegmentFlags[geometry.SegmentId].endNodeFlags;

				if (! flags.IsDefault()) {
					nodeWatcher.AddInvalidSegmentEndFlags(geometry.SegmentId, startNode, ref flags);
				}

				SegmentFlags[geometry.SegmentId].Reset(startNode);
			}
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			
		}

		public bool IsUturnAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].IsUturnAllowed(startNode);
		}

		public bool IsLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].IsLaneChangingAllowedWhenGoingStraight(startNode);
		}

		public bool IsEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].IsEnteringBlockedJunctionAllowed(startNode);
		}

		public bool IsPedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].IsPedestrianCrossingAllowed(startNode);
		}

		public bool IsRightOnRedAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].IsRightOnRedAllowed(startNode);
		}

		public TernaryBool GetUturnAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].GetUturnAllowed(startNode);
		}

		public TernaryBool GetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].GetLaneChangingAllowedWhenGoingStraight(startNode);
		}

		public TernaryBool GetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].GetEnteringBlockedJunctionAllowed(startNode);
		}

		public TernaryBool GetPedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].GetPedestrianCrossingAllowed(startNode);
		}

		public TernaryBool GetRightOnRedAllowed(ushort segmentId, bool startNode) {
			return SegmentFlags[segmentId].GetRightOnRedAllowed(startNode);
		}

		public bool ToggleUturnAllowed(ushort segmentId, bool startNode) {
			return SetUturnAllowed(segmentId, startNode, !IsUturnAllowed(segmentId, startNode));
		}

		public bool ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			return SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, !IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode));
		}

		public bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return SetEnteringBlockedJunctionAllowed(segmentId, startNode, !IsEnteringBlockedJunctionAllowed(segmentId, startNode));
		}

		public bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return SetPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
		}

		public bool ToggleRightOnRedAllowed(ushort segmentId, bool startNode) {
			return SetRightOnRedAllowed(segmentId, startNode, !IsRightOnRedAllowed(segmentId, startNode));
		}

		private void SetSegmentEndFlags(ushort segmentId, bool startNode, SegmentEndFlags flags) {
			if (flags.uturnAllowed != TernaryBool.Undefined) {
				SetUturnAllowed(segmentId, startNode, flags.IsUturnAllowed());
			}

			if (flags.straightLaneChangingAllowed != TernaryBool.Undefined) {
				SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, flags.IsLaneChangingAllowedWhenGoingStraight());
			}

			if (flags.enterWhenBlockedAllowed != TernaryBool.Undefined) {
				SetEnteringBlockedJunctionAllowed(segmentId, startNode, flags.IsEnteringBlockedJunctionAllowed());
			}

			if (flags.pedestrianCrossingAllowed != TernaryBool.Undefined) {
				SetPedestrianCrossingAllowed(segmentId, startNode, flags.IsPedestrianCrossingAllowed());
			}

			if (flags.rightOnRedAllowed != TernaryBool.Undefined) {
				SetRightOnRedAllowed(segmentId, startNode, flags.IsRightOnRedAllowed());
			}
		}

		public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				return false;
			}
			if (!value && LaneConnectionManager.Instance.HasUturnConnections(segmentId, startNode)) {
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"JunctionRestrictionsManager.SetUturnAllowed: No geometry information available for segment {segmentId}");
				return false;
			}

			ushort nodeId = segGeo.GetNodeId(startNode);

			SegmentFlags[segmentId].SetUturnAllowed(startNode, value);
			nodeWatcher.WatchNode(nodeId);

			OnSegmentChange(segmentId);
			return true;
		}

		public bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"JunctionRestrictionsManager.SetUturnAllowed: No geometry information available for segment {segmentId}");
				return false;
			}

			ushort nodeId = segGeo.GetNodeId(startNode);

			SegmentFlags[segmentId].SetLaneChangingAllowedWhenGoingStraight(startNode, value);
			nodeWatcher.WatchNode(nodeId);

			OnSegmentChange(segmentId);
			return true;
		}
		
		public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"JunctionRestrictionsManager.SetUturnAllowed: No geometry information available for segment {segmentId}");
				return false;
			}

			ushort nodeId = segGeo.GetNodeId(startNode);

			SegmentFlags[segmentId].SetEnteringBlockedJunctionAllowed(startNode, value);
			nodeWatcher.WatchNode(nodeId);

			return true;
		}

		public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"JunctionRestrictionsManager.SetUturnAllowed: No geometry information available for segment {segmentId}");
				return false;
			}

			ushort nodeId = segGeo.GetNodeId(startNode);

			SegmentFlags[segmentId].SetPedestrianCrossingAllowed(startNode, value);
			nodeWatcher.WatchNode(nodeId);

			OnSegmentChange(segmentId);
			return true;
		}

		public bool SetRightOnRedAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"JunctionRestrictionsManager.SetRightOnRedAllowed: No geometry information available for segment {segmentId}");
				return false;
			}

			ushort nodeId = segGeo.GetNodeId(startNode);

			SegmentFlags[segmentId].SetRightOnRedAllowed(startNode, value);
			nodeWatcher.WatchNode(nodeId);

			OnSegmentChange(segmentId);
			return true;
		}

		protected void OnSegmentChange(ushort segmentId) {
			RoutingManager.Instance.RequestRecalculation(segmentId);
			SubscribeToSegmentGeometry(segmentId);
			if (OptionsManager.Instance.MayPublishSegmentChanges()) {
				Services.NetService.PublishSegmentChanges(segmentId);
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < SegmentFlags.Length; ++i) {
				SegmentFlags[i].Reset();
			}
			nodeWatcher.Reset();
		}

		public bool LoadData(List<Configuration.SegmentNodeConf> data) {
			bool success = true;
			Log.Info($"Loading junction restrictions. {data.Count} elements");
			foreach (Configuration.SegmentNodeConf segNodeConf in data) {
				try {
					if (!Services.NetService.IsSegmentValid(segNodeConf.segmentId)) {
						continue;
					}

					if (segNodeConf.startNodeFlags != null) {
						Configuration.SegmentNodeFlags flags = segNodeConf.startNodeFlags;
						if (flags.uturnAllowed != null) {
							SetUturnAllowed(segNodeConf.segmentId, true, (bool)flags.uturnAllowed);
						}

						if (flags.straightLaneChangingAllowed != null) {
							SetLaneChangingAllowedWhenGoingStraight(segNodeConf.segmentId, true, (bool)flags.straightLaneChangingAllowed);
						}

						if (flags.enterWhenBlockedAllowed != null) {
							SetEnteringBlockedJunctionAllowed(segNodeConf.segmentId, true, (bool)flags.enterWhenBlockedAllowed);
						}

						if (flags.pedestrianCrossingAllowed != null) {
							SetPedestrianCrossingAllowed(segNodeConf.segmentId, true, (bool)flags.pedestrianCrossingAllowed);
						}

						if (flags.rightOnRedAllowed != null) {
							SetRightOnRedAllowed(segNodeConf.segmentId, true, (bool)flags.rightOnRedAllowed);
						}
					}

					if (segNodeConf.endNodeFlags != null) {
						Configuration.SegmentNodeFlags flags = segNodeConf.endNodeFlags;
						if (flags.uturnAllowed != null) {
							SetUturnAllowed(segNodeConf.segmentId, false, (bool)flags.uturnAllowed);
						}

						if (flags.straightLaneChangingAllowed != null) {
							SetLaneChangingAllowedWhenGoingStraight(segNodeConf.segmentId, false, (bool)flags.straightLaneChangingAllowed);
						}

						if (flags.enterWhenBlockedAllowed != null) {
							SetEnteringBlockedJunctionAllowed(segNodeConf.segmentId, false, (bool)flags.enterWhenBlockedAllowed);
						}

						if (flags.pedestrianCrossingAllowed != null) {
							SetPedestrianCrossingAllowed(segNodeConf.segmentId, false, (bool)flags.pedestrianCrossingAllowed);
						}

						if (flags.rightOnRedAllowed != null) {
							SetRightOnRedAllowed(segNodeConf.segmentId, false, (bool)flags.rightOnRedAllowed);
						}
					}
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning($"Error loading junction restrictions @ segment {segNodeConf.segmentId}: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		public List<Configuration.SegmentNodeConf> SaveData(ref bool success) {
			List<Configuration.SegmentNodeConf> ret = new List<Configuration.SegmentNodeConf>();

			NetManager netManager = Singleton<NetManager>.instance;
			for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
				try {
					if (!Services.NetService.IsSegmentValid((ushort)segmentId)) {
						continue;
					}

					Configuration.SegmentNodeFlags startNodeFlags = null;
					Configuration.SegmentNodeFlags endNodeFlags = null;

					ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
					if (Services.NetService.IsNodeValid(startNodeId)) {
						SegmentEndFlags endFlags = SegmentFlags[segmentId].startNodeFlags;

						if (!endFlags.IsDefault()) {
							startNodeFlags = new Configuration.SegmentNodeFlags();

							startNodeFlags.uturnAllowed = TernaryBoolUtil.ToOptBool(GetUturnAllowed((ushort)segmentId, true));
							startNodeFlags.straightLaneChangingAllowed = TernaryBoolUtil.ToOptBool(GetLaneChangingAllowedWhenGoingStraight((ushort)segmentId, true));
							startNodeFlags.enterWhenBlockedAllowed = TernaryBoolUtil.ToOptBool(GetEnteringBlockedJunctionAllowed((ushort)segmentId, true));
							startNodeFlags.pedestrianCrossingAllowed = TernaryBoolUtil.ToOptBool(GetPedestrianCrossingAllowed((ushort)segmentId, true));
							startNodeFlags.rightOnRedAllowed = TernaryBoolUtil.ToOptBool(GetRightOnRedAllowed((ushort)segmentId, true));
						}
					}

					ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;
					if (Services.NetService.IsNodeValid(endNodeId)) {
						SegmentEndFlags endFlags = SegmentFlags[segmentId].endNodeFlags;

						if (!endFlags.IsDefault()) {
							endNodeFlags = new Configuration.SegmentNodeFlags();

							endNodeFlags.uturnAllowed = TernaryBoolUtil.ToOptBool(GetUturnAllowed((ushort)segmentId, false));
							endNodeFlags.straightLaneChangingAllowed = TernaryBoolUtil.ToOptBool(GetLaneChangingAllowedWhenGoingStraight((ushort)segmentId, false));
							endNodeFlags.enterWhenBlockedAllowed = TernaryBoolUtil.ToOptBool(GetEnteringBlockedJunctionAllowed((ushort)segmentId, false));
							endNodeFlags.pedestrianCrossingAllowed = TernaryBoolUtil.ToOptBool(GetPedestrianCrossingAllowed((ushort)segmentId, false));
							endNodeFlags.rightOnRedAllowed = TernaryBoolUtil.ToOptBool(GetRightOnRedAllowed((ushort)segmentId, false));
						}
					}

					if (startNodeFlags == null && endNodeFlags == null) {
						continue;
					}

					Configuration.SegmentNodeConf conf = new Configuration.SegmentNodeConf((ushort)segmentId);

					conf.startNodeFlags = startNodeFlags;
					conf.endNodeFlags = endNodeFlags;

					Log._Debug($"Saving segment-at-node flags for seg. {segmentId}");
					ret.Add(conf);
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving segment node flags @ {segmentId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
