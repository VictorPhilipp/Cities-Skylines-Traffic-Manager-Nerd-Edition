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
using CSUtil.Commons;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	public class DispatchManager: AbstractCustomManager, IDispatchManager {
		public class DispatchBufferItem  {
			public VehicleAI m_vehicleAI;
			public ushort m_vehicleId;
			public ushort m_building;
			public int m_amount;
			public int m_priority;

			public DispatchBufferItem(VehicleAI vehicleAI, ushort vehicleId, ushort building, int amount, int priority) {
				m_vehicleAI = vehicleAI;
				m_vehicleId = vehicleId;
				m_building = building;
				m_amount = amount;
				m_priority = priority;
			}
		}
		public static DispatchManager Instance { get; private set; } = null;

		static DispatchManager() {
			Instance = new DispatchManager();
		}

		//On waking up, replace the stock pathfinders with the custom one
		private Dictionary<ushort, DispatchBufferItem> m_incomingRequests;
		private FastList<ushort> m_incomingVehicleReleaseRequests;
		private FastList<ushort> m_incomingBuildingReleaseRequests;
		private FastList<ushort> m_incomingVehicleArrivalRequests;
		private Dictionary<ushort, DispatchBufferItem> m_VehicleToBuildingAssignment;
		private Dictionary<ushort, LinkedList<DispatchBufferItem>> m_VehiclePickupQueue;
		private FastList<DispatchBufferItem> m_unhandledBuildingRequests;
		private Dictionary<ushort, ushort> m_BuildingsQueuedToVehicle; // Building goes in this list when it's in a vehicle's pickup queue wihtout a vehicle currently en-route to it
		private Dictionary<ushort, ushort> m_BuildingsToVehicleAssignment; // Buildings go in here when there is a vehicle currently en-route to it
		private List<ushort> m_VehiclesNotInUse; // Vehicles get put in here when they try to go to a building in someone else's queue
		private object QueueLock;
		private bool Terminated;
		private Thread CustomDispatcherThread;
		private uint QueuedItems;

		private void DispatcherThread() {
			while (true) {
				Dictionary<ushort, DispatchBufferItem> l_items;
				FastList<ushort> l_incomingVehicleReleaseRequests;
				FastList<ushort> l_incomingBuildingReleaseRequests;
				FastList<ushort> l_incomingVehicleArrivalRequests;
				Log._Debug($"Dispatcher Thread #{Thread.CurrentThread.ManagedThreadId} iteration!");
				try {
					Monitor.Enter(QueueLock);
					while (QueuedItems == 0u && !Terminated) {
						Monitor.Wait(QueueLock);
					}
					l_items = m_incomingRequests;
					l_incomingVehicleReleaseRequests = m_incomingVehicleReleaseRequests;
					l_incomingBuildingReleaseRequests = m_incomingBuildingReleaseRequests;
					l_incomingVehicleArrivalRequests = m_incomingVehicleArrivalRequests;
					m_incomingRequests = new Dictionary<ushort, DispatchBufferItem>();
					m_incomingBuildingReleaseRequests = new FastList<ushort>();
					m_incomingVehicleReleaseRequests = new FastList<ushort>();
					m_incomingVehicleArrivalRequests = new FastList<ushort>();
					Log._Debug($"Dispatcher Thread #{Thread.CurrentThread.ManagedThreadId} Got {QueuedItems}");
					QueuedItems = 0;
				} finally {
					Monitor.Exit(QueueLock);
				}
				if (Terminated) {
					return;
				}
				foreach (ushort l_vehicle in l_incomingVehicleReleaseRequests) {
					if (m_VehicleToBuildingAssignment.ContainsKey(l_vehicle)) {
						if (m_BuildingsQueuedToVehicle.ContainsKey(m_VehicleToBuildingAssignment[l_vehicle].m_building)) {
							if (m_BuildingsQueuedToVehicle[m_VehicleToBuildingAssignment[l_vehicle].m_building] == l_vehicle) {
								Log._Debug($"DispatchManager.DispatchThread: Releasing building {m_VehicleToBuildingAssignment[l_vehicle].m_building} - Buildings Queued");
								m_BuildingsQueuedToVehicle.Remove(m_VehicleToBuildingAssignment[l_vehicle].m_building);
							}
						}
						if (m_BuildingsToVehicleAssignment.ContainsKey(m_VehicleToBuildingAssignment[l_vehicle].m_building)) {
							Log._Debug($"DispatchManager.DispatchThread: Releasing building {m_VehicleToBuildingAssignment[l_vehicle].m_building} - Buildings assigned");
							m_BuildingsToVehicleAssignment.Remove(m_VehicleToBuildingAssignment[l_vehicle].m_building);
						}
						Log._Debug($"DispatchManager.DispatchThread: Releasing Vehicle {l_vehicle} - Building assigmnent");
						m_VehicleToBuildingAssignment.Remove(l_vehicle);
					}
					if (m_VehiclePickupQueue.ContainsKey(l_vehicle)) {
						foreach (DispatchBufferItem l_dispatchBufferItem in m_VehiclePickupQueue[l_vehicle]) {
							if (m_BuildingsQueuedToVehicle.ContainsKey(l_dispatchBufferItem.m_building)) {
								Log._Debug($"DispatchManager.DispatchThread: Releasing building {l_dispatchBufferItem.m_building} - Buildings Queued from vehicle pickup queue");
								m_BuildingsQueuedToVehicle.Remove(l_dispatchBufferItem.m_building);
							}
						}
						Log._Debug($"DispatchManager.DispatchThread: Releasing Vehicle {l_vehicle} - Queue");
						m_VehiclePickupQueue.Remove(l_vehicle);
					}
					if (m_VehiclesNotInUse.Contains(l_vehicle)) {
						Log._Debug($"DispatchManager.DispatchThread: Releasing Vehicle {l_vehicle} - Not in use");
						m_VehiclesNotInUse.Remove(l_vehicle);
					}
				}
				foreach (ushort l_building in l_incomingBuildingReleaseRequests) {
					if (m_BuildingsToVehicleAssignment.ContainsKey(l_building)) {
						ushort l_vehicle = m_BuildingsToVehicleAssignment[l_building];
						if (m_VehiclePickupQueue.ContainsKey(l_vehicle)) {
							DispatchBufferItem l_itemToRemove = null;
							foreach (DispatchBufferItem l_queuedBuilding in m_VehiclePickupQueue[l_vehicle]) {
								if (l_queuedBuilding.m_building == l_building) {
									l_itemToRemove = l_queuedBuilding;
									break;
								}
							}
							if (l_itemToRemove != null) {
								Log._Debug($"DispatchManager.DispatchThread: Removing building {l_building} from vehicle {l_vehicle} - Building removed");
								m_VehiclePickupQueue[l_vehicle].Remove(l_itemToRemove);
							}
						}
						if (m_VehicleToBuildingAssignment.ContainsKey(l_vehicle) && m_VehicleToBuildingAssignment[l_vehicle].m_building == l_building) {
							Log._Debug($"DispatchManager.DispatchThread: Removing vehicle {l_vehicle} - Building {l_building} removed");
							m_VehicleToBuildingAssignment.Remove(l_vehicle);
						}
						Log._Debug($"DispatchManager.DispatchThread: Removing building {l_building} from building->vehicle");
						m_BuildingsToVehicleAssignment.Remove(l_building);
					}
				}
				foreach (ushort l_vehicle in l_incomingVehicleArrivalRequests) {
					if (m_VehicleToBuildingAssignment.ContainsKey(l_vehicle)) {
						ushort l_building = m_VehicleToBuildingAssignment[l_vehicle].m_building;
						if (m_BuildingsToVehicleAssignment.ContainsKey(l_building)) {
							Log._Debug($"DispatchManager.DispatchThread: Removing building {l_building} from building->vehicle - Vehicle arrived");
							m_BuildingsToVehicleAssignment.Remove(l_building);
						}
					}
				}

				VehicleManager l_vehManager = Singleton<VehicleManager>.instance;

				foreach (KeyValuePair<ushort, DispatchBufferItem> l_KVbufferItem in l_items) {
					DispatchBufferItem l_bufferItem = l_KVbufferItem.Value;
					Log._Debug($"DispatchManager.DispatchThread: Processing item for Vehicle {l_bufferItem.m_vehicleId} for building {l_bufferItem.m_building} for amount {l_bufferItem.m_amount}");
					// TODO: Check our idle vehicle and see if there a closer one than the game requests
					bool l_defaultDispatch = true;
					bool l_thisRequestVehicleAlreadyDispatched = false; // If this is true, cannot use original vehicle.

					Building l_building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[l_bufferItem.m_building];
					int l_buildingProblematicLevel = 0;
					if ((l_building.m_problems & Notification.Problem.Death) != Notification.Problem.None) {
						if ((l_building.m_problems & Notification.Problem.MajorProblem) != Notification.Problem.None) {
							l_buildingProblematicLevel = 2;
						} else {
							l_buildingProblematicLevel = 1;
						}
					}

					if (m_VehiclePickupQueue.ContainsKey(l_bufferItem.m_vehicleId) && m_VehiclePickupQueue[l_bufferItem.m_vehicleId].Count() > 0) {
						Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_bufferItem.m_vehicleId} contains queued entries.");
						DispatchBufferItem l_queuedItem = m_VehiclePickupQueue[l_bufferItem.m_vehicleId].First();
						m_VehiclePickupQueue[l_bufferItem.m_vehicleId].RemoveFirst();

						ref Vehicle l_vehicle = ref l_vehManager.m_vehicles.m_buffer[l_queuedItem.m_vehicleId];
						HearseAI l_AI;
						try {
							l_AI = (HearseAI)l_vehicle.Info.m_vehicleAI;
						} catch (System.InvalidCastException ex) {
							Log.Error($"DispatchManager.DispatchThread: Vehicle {l_queuedItem.m_vehicleId} is not a hearse! {l_vehicle.Info.m_vehicleAI}");
							continue;
						}

						if (l_vehicle.m_transferSize >= l_AI.m_corpseCapacity) {
							Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_queuedItem.m_vehicleId} Out of capacity unexpectedly. Abandoning rest of queue.");
							// We're full with vehicles in our queue. Abandom them
							if (m_BuildingsQueuedToVehicle.ContainsKey(l_queuedItem.m_building)) {
								Log._Debug($"DispatchManager.DispatchThread: Removed building {l_queuedItem.m_building} from queue");
								m_BuildingsQueuedToVehicle.Remove(l_queuedItem.m_building);
							}
							foreach (DispatchBufferItem l_Item in m_VehiclePickupQueue[l_bufferItem.m_vehicleId]) {
								if (m_BuildingsQueuedToVehicle.ContainsKey(l_Item.m_building)) {
									Log._Debug($"DispatchManager.DispatchThread: Removed building {l_Item.m_building} from queue");
									m_BuildingsQueuedToVehicle.Remove(l_Item.m_building);
								}
							}
							m_VehiclePickupQueue.Remove(l_bufferItem.m_vehicleId);
							continue;
						}

						//Log._Debug($"DispatchManager.DispatchThread: Adding entry for building {l_bufferItem.m_building} into unhandled requests");
						//m_unhandledBuildingRequests.Add(l_bufferItem);

						m_BuildingsQueuedToVehicle.Remove(l_queuedItem.m_building);
						Log._Debug($"DispatchManager.DispatchThread: Added vehicle {l_queuedItem.m_vehicleId} to Vehicle building assigment");
						m_VehicleToBuildingAssignment[l_queuedItem.m_vehicleId] = l_queuedItem;
						Log._Debug("DispatchManager.DispatchThread: Dispatching for queued entry");
						DispatchVehicle(l_queuedItem.m_vehicleId, ref l_vehManager.m_vehicles.m_buffer[l_queuedItem.m_vehicleId], l_queuedItem.m_building, l_bufferItem.m_vehicleAI);
						if (l_bufferItem.m_building == l_queuedItem.m_building) {
							continue;
						}
						// We still need to handle this building. This vehicle has been re-queued
						l_thisRequestVehicleAlreadyDispatched = true;
						Log._Debug($"DispatchManager.DispatchThread: Trying to find alternate vehicle for building {l_bufferItem.m_building}");
					}
					if (l_defaultDispatch && l_buildingProblematicLevel < 2) {
						// If building had a problematic level of 2, we let the default dispatcher or unused vehicle handler take it.
						// This check must be done after the queued items for this vehicle, since the queued vehicle gets called back here with a different building and we
						// intercept it above
						if (m_BuildingsQueuedToVehicle.ContainsKey(l_bufferItem.m_building)) {
							Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} Already in queue for another vehicle.");
							AddVehicleToUnusedList(l_bufferItem.m_vehicleId);
							continue;
						}
						if (m_BuildingsToVehicleAssignment.ContainsKey(l_bufferItem.m_building)) {
							ushort l_vehicle = m_BuildingsToVehicleAssignment[l_bufferItem.m_building];
							Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} Already has vehicle {l_vehicle} on the way");
							if (l_vehicle != l_bufferItem.m_vehicleId) {
								AddVehicleToUnusedList(l_bufferItem.m_vehicleId);
							}
							continue;
						}
						if (!l_thisRequestVehicleAlreadyDispatched && !CheckVehicleReadyToDispatch(l_bufferItem.m_vehicleId)) {
							// Can't do anything with a vehicle not created.
							Log._Debug($"DispatchManager.DispatchThread: Adding building {l_bufferItem.m_building} to unhandled queue.");
							m_unhandledBuildingRequests.Add(l_bufferItem);
							continue;
						}
						
						// Look at other en-route vehicles to see if they will go past

						ushort l_buildingSegment = FindBuildingRoad(l_bufferItem.m_building);
						if (l_buildingSegment != 0) {
							NetSegment l_nsBuildingSegment = Singleton<NetManager>.instance.m_segments.m_buffer[l_buildingSegment];
							Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} has parent node {l_buildingSegment}");
							Vector3 l_buildingLanePos;
							uint l_buildingLaneID;
							int l_buildingLaneIndex;
							float l_buildingLaneOffset;
							if (l_nsBuildingSegment.GetClosestLanePosition(l_building.m_position, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, out l_buildingLanePos, out l_buildingLaneID, out l_buildingLaneIndex, out l_buildingLaneOffset)) {
								foreach (KeyValuePair<ushort, DispatchBufferItem> l_thisVehicle in m_VehicleToBuildingAssignment) {
									ushort uCurrentDestinationBuilding = m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId].m_building;
									Building l_currentBuilding = Singleton<BuildingManager>.instance.m_buildings.m_buffer[uCurrentDestinationBuilding];
									// Don't check ourselves
									if (l_thisVehicle.Value.m_vehicleId == l_bufferItem.m_vehicleId) {
										continue;
									}
									if (l_thisVehicle.Value.m_vehicleId != l_thisVehicle.Key) {
										Log.Error($"DispatchManager.DispatchThread: Key ({l_thisVehicle.Key})/Entry ({l_thisVehicle.Value.m_vehicleId}) mismatch on vehicle ID");
										continue;
									}
									if (!CheckVehicleReadyToDispatch(l_thisVehicle.Key)) {
										continue;
									}
									int l_currentBuildingProblematicLevel = 0;
									if ((l_currentBuilding.m_problems & Notification.Problem.Death) != Notification.Problem.None) {
										if ((l_currentBuilding.m_problems & Notification.Problem.MajorProblem) != Notification.Problem.None) {
											l_currentBuildingProblematicLevel = 2;
										} else {
											l_currentBuildingProblematicLevel = 1;
										}
									}
									if (l_currentBuildingProblematicLevel == 2) {
										// Don't slow down vehicle on way to high problem building
										continue;
									}

									ref Vehicle l_vehicle = ref l_vehManager.m_vehicles.m_buffer[l_thisVehicle.Value.m_vehicleId];
									uint l_searchPathId = l_vehicle.m_path;
									if (l_searchPathId == 0) {
										continue;
									}
									bool l_fPathDebug = false;
									PathUnit[] paths = Singleton<PathManager>.instance.m_pathUnits.m_buffer;
									bool l_abortSearch = false;
									int l_pathPosStart = (l_vehicle.m_pathPositionIndex != 255) ? (l_vehicle.m_pathPositionIndex >> 1) : 0;
									int l_pathPosTotalStart = l_pathPosStart;
									int l_pathPosTotal = l_pathPosStart - 1;
									while (l_searchPathId != 0) {
										for (int i = l_pathPosStart; i < paths[l_searchPathId].m_positionCount; i++) {
											l_pathPosTotal++;
											PathUnit.Position p = paths[l_searchPathId].GetPosition(i);
											if (l_fPathDebug) {
												Log._Debug($"DispatchManager.DispatchThread: PATHDEBUG: Checking position {i} {p.m_segment}");
											}
											if (p.m_segment != l_buildingSegment) {
												continue;
											}
											// We found one!
											// Need to check for space and queue it up (Not loose vehicle original destination)
											HearseAI l_AI;
											try {
												l_AI = (HearseAI)l_vehicle.Info.m_vehicleAI;
											} catch (System.InvalidCastException ex) {
												Log.Error($"DispatchManager.DispatchThread: Vehicle {l_thisVehicle.Value.m_vehicleId} is not a hearse! {l_vehicle.Info.m_vehicleAI}");
												l_abortSearch = true;
												break;
											}
											// Check if the path is on the right side
											if (l_nsBuildingSegment.Info.m_lanes[l_buildingLaneIndex].m_direction != l_nsBuildingSegment.Info.m_lanes[p.m_lane].m_direction) {
												if (l_nsBuildingSegment.Info.m_canCrossLanes) {
													Log._Debug($"DispatchManager.DispatchThread: Segment {l_buildingSegment} direction different, but can cross lanes. Continuing.");
												} else {
													if (l_fPathDebug) {
														Log._Debug($"DispatchManager.DispatchThread: PATHDEBUG: Going past, in wrong direction");
													}
													l_abortSearch = true;
													break;
												}
											}
											Vehicle.Frame lastFrameData = l_vehicle.GetLastFrameData();
											if (l_pathPosTotal == l_pathPosTotalStart) {
												// Check vehicle hasn't just passed building -- Must be on same segment
												// If it just passed, but is on the next segment, the test above would've skipped (segment path counter)
												Vector3 l_roadDirection;
												if (l_nsBuildingSegment.Info.m_lanes[p.m_lane].m_direction == NetInfo.Direction.Forward) {
													l_roadDirection = l_nsBuildingSegment.m_startDirection;
												} else {
													l_roadDirection = l_nsBuildingSegment.m_endDirection;
												}
												Vector3 l_buildingToVehicleDirection = l_buildingLanePos - l_vehicle.GetLastFramePosition();
												l_buildingToVehicleDirection /= l_buildingToVehicleDirection.magnitude;
												float l_dotDir = Vector3.Dot(l_buildingToVehicleDirection, l_roadDirection);
												Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_thisVehicle.Value.m_vehicleId} direction: {l_nsBuildingSegment.Info.m_lanes[p.m_lane].m_direction} segment start dir: {l_nsBuildingSegment.m_startDirection} segment end dir: {l_nsBuildingSegment.m_endDirection} buildingLanePos: {l_buildingLanePos} Vehicle pos: {lastFrameData.m_position} roadDot: {l_roadDirection} l_vehicleToBuildingDirection: {l_buildingToVehicleDirection} l_dotDir: {l_dotDir}");
												if (l_dotDir < 0.0f) {
													// Vehicle just passed
													Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_thisVehicle.Value.m_vehicleId} just passed building {l_bufferItem.m_building} on same segment. Continuing search.");
													l_abortSearch = true;
													break;
												}
											}
											// Check if vehicle is too close (may be going to fast to stop at this point

											float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;
											float breakingDist = 0.5f * sqrVelocity / l_AI.m_info.m_braking;
											float l_distanceToBuilding = Vector3.Distance(l_vehicle.GetLastFramePosition(), l_buildingLanePos);
											if (l_distanceToBuilding < breakingDist) {
												Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_thisVehicle.Value.m_vehicleId} is too close to building {l_bufferItem.m_building}. Continuing search.");
												l_abortSearch = true;
												break;
											}
											// Check how many queue requests this vehicle may have
											int l_queueAmount = 0;
											if (m_VehiclePickupQueue.ContainsKey(l_thisVehicle.Value.m_vehicleId)) {
												foreach (DispatchBufferItem l_queueCheck in m_VehiclePickupQueue[l_thisVehicle.Value.m_vehicleId]) {
													l_queueAmount += l_queueCheck.m_amount;
												}
											}
											if (l_vehicle.m_transferSize + m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId].m_amount + l_bufferItem.m_amount + l_queueAmount > l_AI.m_corpseCapacity) {
												if (l_fPathDebug) {
													Log._Debug($"DispatchManager.DispatchThread: PATHDEBUG: Not enough capacity");
												}
												l_abortSearch = true;
												break;
											}
											// Need to check if actually after our current destination
											if (i == paths[l_searchPathId].m_positionCount - 1 && paths[l_searchPathId].m_nextPathUnit == 0) {
												// Last segment
												// Check vehicle hasn't just passed building -- Must be on same segment
												// If it just passed, but is on the next segment, the test above would've skipped (segment path counter)
												Vector3 l_roadDirection;
												Vector3 l_currentBuildingLanePos;
												uint l_currentBuildingLaneID;
												int l_currentBuildingLaneIndex;
												float l_currentBuildingLaneOffset;
												if (l_nsBuildingSegment.GetClosestLanePosition(l_currentBuilding.m_position, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, out l_currentBuildingLanePos, out l_currentBuildingLaneID, out l_currentBuildingLaneIndex, out l_currentBuildingLaneOffset)) {
													if (l_nsBuildingSegment.Info.m_lanes[p.m_lane].m_direction == NetInfo.Direction.Forward) {
														l_roadDirection = l_nsBuildingSegment.m_startDirection;
													} else {
														l_roadDirection = l_nsBuildingSegment.m_endDirection;
													}
													Vector3 l_currentEndPointToNewEndPointDirection = l_currentBuildingLanePos - l_buildingLanePos;
													l_currentEndPointToNewEndPointDirection /= l_currentEndPointToNewEndPointDirection.magnitude;
													float l_dotDir = Vector3.Dot(l_currentEndPointToNewEndPointDirection, l_roadDirection);
													Log._Debug($"DispatchManager.DispatchThread: Current Building {uCurrentDestinationBuilding} segment direction: {l_nsBuildingSegment.Info.m_lanes[p.m_lane].m_direction} segment start dir: {l_nsBuildingSegment.m_startDirection} segment end dir: {l_nsBuildingSegment.m_endDirection} Current buildingLanePos: {l_currentBuildingLanePos} New BuildingLanepos: {l_buildingLanePos} roadDot: {l_roadDirection} l_currentEndPointToNewEndPointDirection: {l_currentEndPointToNewEndPointDirection} l_dotDir: {l_dotDir}");
													if (l_dotDir < 0.0f) {
														// new building is past current one
														Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} is after current end building {uCurrentDestinationBuilding}");
														l_abortSearch = true;
														break;
													}
												} else {
													Log._Debug($"DispatchManager.DispatchThread: Can't get existing destination building {uCurrentDestinationBuilding} road location. Not risking this one.");
													l_abortSearch = true;
													break;
												}
											}

											Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} is on path of vehicle {l_thisVehicle.Value.m_vehicleId} and has enough space");
											LinkedList<DispatchBufferItem> l_thisVehicleQueue;
											// Check if this vehicle has a pick up queue, if not, create one
											if (!m_VehiclePickupQueue.ContainsKey(l_thisVehicle.Value.m_vehicleId)) {
												l_thisVehicleQueue = new LinkedList<DispatchBufferItem>();
												m_VehiclePickupQueue.Add(l_thisVehicle.Value.m_vehicleId, l_thisVehicleQueue);
											} else {
												l_thisVehicleQueue = m_VehiclePickupQueue[l_thisVehicle.Value.m_vehicleId];
											}
											// Move this vehicle's current pickup destination into the queue, and take the new one.
											// TODO: Add the vehicle in this request to a free queue
											Log._Debug($"DispatchManager.DispatchThread: Putting vehicle {l_thisVehicle.Value.m_vehicleId} current destination of {m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId].m_building} into queue. Vehicle now has {l_thisVehicleQueue.Count() + 1} building in queue");
											l_thisVehicleQueue.AddFirst(m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId]);
											m_BuildingsQueuedToVehicle.Add(m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId].m_building, l_thisVehicle.Value.m_vehicleId);
											m_BuildingsToVehicleAssignment.Remove(m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId].m_building);

											Log._Debug($"DispatchManager.DispatchThread: Updating Vehicle {l_thisVehicle.Value.m_vehicleId} -> Building with new building {l_bufferItem.m_building}");
											m_VehicleToBuildingAssignment.Remove(l_thisVehicle.Value.m_vehicleId);
											l_bufferItem.m_vehicleId = l_thisVehicle.Value.m_vehicleId;
											m_VehicleToBuildingAssignment[l_thisVehicle.Value.m_vehicleId] = l_bufferItem;
											if (!l_thisRequestVehicleAlreadyDispatched) {
												AddVehicleToUnusedList(l_bufferItem.m_vehicleId);
											}
											if (m_VehiclesNotInUse.Contains(l_thisVehicle.Value.m_vehicleId)) {
												m_VehiclesNotInUse.Remove(l_thisVehicle.Value.m_vehicleId);
											}
											Log._Debug("DispatchManager.DispatchThread: Dispatching for building on path of other vehicle");
											DispatchVehicle(l_thisVehicle.Value.m_vehicleId, ref l_vehManager.m_vehicles.m_buffer[l_thisVehicle.Value.m_vehicleId], l_bufferItem.m_building, l_bufferItem.m_vehicleAI);
											l_defaultDispatch = false;
											l_abortSearch = true;
											break;
										}
										if (l_abortSearch) {
											break;
										}
										l_pathPosStart = 0;
										l_searchPathId = paths[l_searchPathId].m_nextPathUnit;
									}
									if (!l_defaultDispatch) {
										break;
									}
								}
							}
						}
					}
					if (l_defaultDispatch && !l_thisRequestVehicleAlreadyDispatched) {
						// Check to see if there is already a vehicle en-route
						if (m_BuildingsToVehicleAssignment.ContainsKey(l_bufferItem.m_building)) {
							ushort l_assignedVehicle = m_BuildingsToVehicleAssignment[l_bufferItem.m_building];
							if (m_VehicleToBuildingAssignment.ContainsKey(l_assignedVehicle)) {
								if (m_VehicleToBuildingAssignment[l_assignedVehicle].m_building == l_bufferItem.m_building) {
									Log._Debug($"DispatchManager.DispatchThread: Keeping vehicle {l_assignedVehicle} assigned to high problem building {l_bufferItem.m_building}");
									continue;
								}
							}
						}
						// Check to see if we have a closer one in our backlog.
						PruneUnusedVehicleList();
						if (l_buildingProblematicLevel == 2) {
							// Dispatch from closest vehicle or default requested
							Log._Debug($"DispatchManager.DispatchThread: Building {l_bufferItem.m_building} has high problem. Looking for nearest vehicle or default");
							RemoveBuildingFromOtherVehiclesQueue(l_bufferItem.m_building);
						}
						if (m_VehiclesNotInUse.Count > 0) {
							float l_distance;
							ushort l_ClosestVehicle = FindClosestUnusedVehicle(l_building.m_position, out l_distance);
							Vector3 l_assignedVehiclePos = l_vehManager.m_vehicles.m_buffer[l_bufferItem.m_vehicleId].GetLastFramePosition();
							Log._Debug($"DispatchManager.DispatchThread: Assigned vehicle pos {l_assignedVehiclePos}");
							Vector3 l_assignedVehicleDistance = l_assignedVehiclePos - l_building.m_position;
							if (l_ClosestVehicle != 0 && l_assignedVehicleDistance.sqrMagnitude > l_distance) {
								Log._Debug($"DispatchManager.DispatchThread: Choosing unused vehicle {l_ClosestVehicle} over {l_bufferItem.m_vehicleId} as it's closer.");
								m_VehicleToBuildingAssignment.Remove(l_bufferItem.m_vehicleId);
								AddVehicleToUnusedList(l_bufferItem.m_vehicleId);
								m_VehiclesNotInUse.Remove(l_ClosestVehicle);
								Log._Debug("DispatchManager.DispatchThread: Dispatching for building - unused vehicle closer");
								DispatchVehicle(l_ClosestVehicle, ref l_vehManager.m_vehicles.m_buffer[l_ClosestVehicle], l_bufferItem.m_building, l_bufferItem.m_vehicleAI);
								l_bufferItem.m_vehicleId = l_ClosestVehicle;
								m_VehicleToBuildingAssignment[l_ClosestVehicle] = l_bufferItem;
								l_defaultDispatch = false;
							}
						}
					}
					if (l_defaultDispatch && !l_thisRequestVehicleAlreadyDispatched) {
						if (m_VehiclesNotInUse.Contains(l_bufferItem.m_vehicleId)) {
							m_VehiclesNotInUse.Remove(l_bufferItem.m_vehicleId);
						}
						m_VehicleToBuildingAssignment[l_bufferItem.m_vehicleId] = l_bufferItem;
						Log._Debug("DispatchManager.DispatchThread: Dispatching for default");
						DispatchVehicle(l_bufferItem.m_vehicleId, ref l_vehManager.m_vehicles.m_buffer[l_bufferItem.m_vehicleId], l_bufferItem.m_building, l_bufferItem.m_vehicleAI);
					}
				}
				// For now, take the vehicles not in use and send them to any unhandled buildings.
				// TODO: Choose vehicles closest
				Log._Debug($"DispatchManager.DispatchThread: m_VehiclesNotInUse: {m_VehiclesNotInUse.Count()} m_unhandledBuildingRequests: {m_unhandledBuildingRequests.m_size}");
				PruneUnusedVehicleList();
				FastList<DispatchBufferItem> l_handledBuildingRequests = new FastList<DispatchBufferItem>();
				foreach (DispatchBufferItem l_bufferItem in m_unhandledBuildingRequests) {
					Building l_building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[l_bufferItem.m_building];
					if (m_VehiclesNotInUse.Count() == 0) {
						break;
					}
					ushort l_selectedvehicle = FindClosestUnusedVehicle(l_building.m_position, out _);
					l_handledBuildingRequests.Add(l_bufferItem);
					m_BuildingsQueuedToVehicle.Remove(l_bufferItem.m_building);

					m_VehiclesNotInUse.Remove(l_selectedvehicle);

					l_bufferItem.m_vehicleId = l_selectedvehicle;
					m_VehicleToBuildingAssignment[l_bufferItem.m_vehicleId] = l_bufferItem;
					Log._Debug("DispatchManager.DispatchThread: Dispatching for unused vehicle");
					DispatchVehicle(l_bufferItem.m_vehicleId, ref l_vehManager.m_vehicles.m_buffer[l_bufferItem.m_vehicleId], l_bufferItem.m_building, l_bufferItem.m_vehicleAI);
				}
				foreach (DispatchBufferItem l_buildingHandled in l_handledBuildingRequests) {
					m_unhandledBuildingRequests.Remove(l_buildingHandled);
				}
				/*
				foreach (ushort l_vehicleToDispatch in m_VehiclesNotInUse) {
					ref Vehicle l_vehicle = ref l_vehManager.m_vehicles.m_buffer[l_vehicleToDispatch];
					Log._Debug($"DispatchManager.DispatchThread Unused Vehicle {l_vehicleToDispatch} data: m_flags: {l_vehicle.m_flags} m_transferSize: {l_vehicle.m_transferSize} type: {l_vehicle.Info.m_vehicleType}");
				}*/
			}
		}

		private void RemoveBuildingFromOtherVehiclesQueue(ushort buildingId) {
			if (m_BuildingsQueuedToVehicle.ContainsKey(buildingId)) {
				DispatchBufferItem l_itemToRemove = null;
				if (m_VehiclePickupQueue.ContainsKey(m_BuildingsQueuedToVehicle[buildingId])) {
					foreach (DispatchBufferItem l_bufferItem in m_VehiclePickupQueue[m_BuildingsQueuedToVehicle[buildingId]]) {
						if (l_bufferItem.m_building == buildingId) {
							l_itemToRemove = l_bufferItem;
							break;
						}
					}
				}
				if (l_itemToRemove == null) {
					Log.Warning($"DispatchManager::RemoveBuildingFromOtherVehiclesQueue: Building {buildingId} referenced vehicle {m_BuildingsQueuedToVehicle[buildingId]} but not found in vehicle's queue");
				} else {
					Log._Debug($"DispatchManager::RemoveBuildingFromOtherVehiclesQueue: Removed building {buildingId} from vehicle {m_BuildingsQueuedToVehicle[buildingId]} queue");
					m_VehiclePickupQueue[m_BuildingsQueuedToVehicle[buildingId]].Remove(l_itemToRemove);
				}
			}
		}

		private void PruneUnusedVehicleList() {
			FastList<ushort> l_vehiclesNowInUse = new FastList<ushort>();
			foreach (ushort l_vehicleToDispatch in m_VehiclesNotInUse) {
				if (!CheckVehicleReadyToDispatch(l_vehicleToDispatch)) {
					l_vehiclesNowInUse.Add(l_vehicleToDispatch);
					continue;
				}
			}
			foreach (ushort l_vehicleNowInUse in l_vehiclesNowInUse) {
				m_VehiclesNotInUse.Remove(l_vehicleNowInUse);
			}
		}

		private void AddVehicleToUnusedList(ushort vehicleId) {
			if (!m_VehicleToBuildingAssignment.ContainsKey(vehicleId)) {
				bool l_fAddToVehicleNotInUse = true;
				if (m_VehiclePickupQueue.ContainsKey(vehicleId) && m_VehiclePickupQueue[vehicleId].Count > 0) {
					l_fAddToVehicleNotInUse = false;
				}
				if (l_fAddToVehicleNotInUse) {
					// This vehicle is already in use going to another building.
					Log._Debug($"DispatchManager.DispatchThread: Adding vehicle {vehicleId} to unused vehicles");
					if (!m_VehiclesNotInUse.Contains(vehicleId)) {
						m_VehiclesNotInUse.Add(vehicleId);
					}
				}
			}
		}

		private ushort FindClosestUnusedVehicle(Vector3 pos , out float selectVehicleDistanceSqrMag) {
			VehicleManager l_vehManager = Singleton<VehicleManager>.instance;
			ushort l_selectedvehicle = 0;
			Vector3 l_shortestDistance = new Vector3(0, 0, 0);
			foreach (ushort l_vehicleToDispatch in m_VehiclesNotInUse) {
				ref Vehicle l_vehicle = ref l_vehManager.m_vehicles.m_buffer[l_vehicleToDispatch];
				Vector3 l_thisDistance = l_vehicle.GetLastFramePosition() - pos;
				if (l_selectedvehicle == 0) {
					l_selectedvehicle = l_vehicleToDispatch;
					l_shortestDistance = l_thisDistance;
				}
				if (l_thisDistance.sqrMagnitude < l_shortestDistance.sqrMagnitude) {
					l_shortestDistance = l_thisDistance;
					l_selectedvehicle = l_vehicleToDispatch;
				}
			}
			selectVehicleDistanceSqrMag = l_shortestDistance.sqrMagnitude;
			return l_selectedvehicle;
		}


		private DispatchManager() {
			m_incomingRequests = new Dictionary<ushort, DispatchBufferItem>();
			m_incomingVehicleReleaseRequests = new FastList<ushort>();
			m_VehicleToBuildingAssignment = new Dictionary<ushort, DispatchBufferItem>();
			m_VehiclePickupQueue = new Dictionary<ushort, LinkedList<DispatchBufferItem>>();
			m_unhandledBuildingRequests = new FastList<DispatchBufferItem>();
			m_BuildingsQueuedToVehicle = new Dictionary<ushort, ushort>();
			m_VehiclesNotInUse = new List<ushort>();
			m_BuildingsToVehicleAssignment = new Dictionary<ushort, ushort>();
			m_incomingBuildingReleaseRequests = new FastList<ushort>();
			m_incomingVehicleArrivalRequests = new FastList<ushort>();
			QueueLock = new object();
			Terminated = false;
			QueuedItems = 0;
			//base.OnCreated(threading);
			CustomDispatcherThread = new Thread(DispatcherThread) { Name = "Dispatcher" };
			CustomDispatcherThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			CustomDispatcherThread.Start();
			if (!CustomDispatcherThread.IsAlive) {
				//CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
				Log.Error($"Dispatcher T#{Thread.CurrentThread.ManagedThreadId}, Dispatcher thread failed to start!");
			}

		}
		protected virtual void OnDestroy() {
#if DEBUGLOCKS
			uint lockIter = 0;
#endif
			try {
				Monitor.Enter(QueueLock);
				Terminated = true;
				Monitor.PulseAll(QueueLock);
			} catch (Exception e) {
				Log.Error("Dispatcher.OnDestroy Error: " + e.ToString());
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		public void AddBuildingWithDead(DispatchBufferItem itemToAdd) {
			VehicleManager l_vehManager = Singleton<VehicleManager>.instance;
			ref Vehicle l_vehicle = ref l_vehManager.m_vehicles.m_buffer[itemToAdd.m_vehicleId];
			Log._Debug($"DispatchManager.AddBuildingWithDead Vehicle {itemToAdd.m_vehicleId} data: m_flags: {l_vehicle.m_flags} m_transferSize: {l_vehicle.m_transferSize} type: {l_vehicle.Info.m_vehicleType}");
			try {
				Monitor.Enter(QueueLock);
				m_incomingRequests.Add(itemToAdd.m_vehicleId, itemToAdd);
				++QueuedItems;
				Monitor.PulseAll(QueueLock);
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		public bool CheckIfVehicleQueued(ushort vehicleId) {
			bool l_vehicleQueued;
			Monitor.Enter(QueueLock);
			l_vehicleQueued = m_incomingRequests.ContainsKey(vehicleId);
			Monitor.Exit(QueueLock);
			return l_vehicleQueued;
		}

		public void ReleaseVehicle(ushort vehicleId) {
			while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				m_incomingVehicleReleaseRequests.Add(vehicleId);
				Monitor.PulseAll(QueueLock);
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		public void VehicleArrivedAtDestination(ushort vehicleId) {
			while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				m_incomingVehicleArrivalRequests.Add(vehicleId);
				Monitor.PulseAll(QueueLock);
			} finally {
				Monitor.Exit(QueueLock);
			}

		}

		public void ReleaseBuilding(ushort buildingId) {
			while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
			try {
				m_incomingBuildingReleaseRequests.Add(buildingId);
				Monitor.PulseAll(QueueLock);
			} finally {
				Monitor.Exit(QueueLock);
			}

		}
		private ushort FindBuildingRoad(ushort buildingId) {
			Building building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId];
			BuildingAI buildingAI = building.Info.m_buildingAI;
			Vector3 position = (buildingAI.m_info.m_zoningMode != BuildingInfo.ZoningMode.CornerLeft) ? ((buildingAI.m_info.m_zoningMode != BuildingInfo.ZoningMode.CornerRight) ? building.CalculateSidewalkPosition(0f, 4f) : building.CalculateSidewalkPosition((float)building.Width * -4f, 4f)) : building.CalculateSidewalkPosition((float)building.Width * 4f, 4f);
			Bounds bounds = new Bounds(position, new Vector3(40f, 40f, 40f));
			Vector3 min = bounds.min;
			int num = Mathf.Max((int)((min.x - 64f) / 64f + 135f), 0);
			Vector3 min2 = bounds.min;
			int num2 = Mathf.Max((int)((min2.z - 64f) / 64f + 135f), 0);
			Vector3 max = bounds.max;
			int num3 = Mathf.Min((int)((max.x + 64f) / 64f + 135f), 269);
			Vector3 max2 = bounds.max;
			int num4 = Mathf.Min((int)((max2.z + 64f) / 64f + 135f), 269);
			NetManager instance = Singleton<NetManager>.instance;
			for (int i = num2; i <= num4; i++) {
				for (int j = num; j <= num3; j++) {
					ushort num5 = instance.m_segmentGrid[i * 270 + j];
					int num6 = 0;
					while (num5 != 0) {
						NetInfo info = instance.m_segments.m_buffer[num5].Info;
						if (info.m_class.m_service == ItemClass.Service.Road && !info.m_netAI.IsUnderground() && info.m_netAI is RoadBaseAI && info.m_hasPedestrianLanes && (info.m_hasForwardVehicleLanes || info.m_hasBackwardVehicleLanes)) {
							ushort startNode = instance.m_segments.m_buffer[num5].m_startNode;
							ushort endNode = instance.m_segments.m_buffer[num5].m_endNode;
							Vector3 position2 = instance.m_nodes.m_buffer[startNode].m_position;
							Vector3 position3 = instance.m_nodes.m_buffer[endNode].m_position;
							Vector3 min3 = bounds.min;
							float a = min3.x - 64f - position2.x;
							Vector3 min4 = bounds.min;
							float a2 = Mathf.Max(a, min4.z - 64f - position2.z);
							float x = position2.x;
							Vector3 max3 = bounds.max;
							float a3 = x - max3.x - 64f;
							float z = position2.z;
							Vector3 max4 = bounds.max;
							float num7 = Mathf.Max(a2, Mathf.Max(a3, z - max4.z - 64f));
							Vector3 min5 = bounds.min;
							float a4 = min5.x - 64f - position3.x;
							Vector3 min6 = bounds.min;
							float a5 = Mathf.Max(a4, min6.z - 64f - position3.z);
							float x2 = position3.x;
							Vector3 max5 = bounds.max;
							float a6 = x2 - max5.x - 64f;
							float z2 = position3.z;
							Vector3 max6 = bounds.max;
							float num8 = Mathf.Max(a5, Mathf.Max(a6, z2 - max6.z - 64f));
							Vector3 b = default(Vector3);
							int num9 = default(int);
							float num10 = default(float);
							Vector3 vector = default(Vector3);
							int num11 = default(int);
							float num12 = default(float);
							if ((num7 < 0f || num8 < 0f) && instance.m_segments.m_buffer[num5].m_bounds.Intersects(bounds) && instance.m_segments.m_buffer[num5].GetClosestLanePosition(position, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, VehicleInfo.VehicleType.None, false, out b, out num9, out num10, out vector, out num11, out num12)) {
								float num13 = Vector3.SqrMagnitude(position - b);
								if (num13 < 400f) {
									return num5;
								}
							}
						}
						num5 = instance.m_segments.m_buffer[num5].m_nextGridSegment;
						if (++num6 >= 36864) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
			return 0;
		}
		private bool CheckVehicleReadyToDispatch(ushort l_vehicleId) {
			VehicleManager l_vehManager = Singleton<VehicleManager>.instance;
			//Log._Debug($"DispatchManager.DispatchThread Vehicle data: Vehicle {l_vehicleId} m_flags: {l_vehicles.m_buffer[l_vehicleId].m_flags} m_transferSize: {l_vehicles.m_buffer[l_vehicleId].m_transferSize} type: {l_vehicles.m_buffer[l_vehicleId].Info.m_vehicleType}");
			/*
			if ((l_vehManager.m_vehicles.m_buffer[l_vehicleId].Info.m_vehicleType & VehicleStateManager.VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
				// Can't do anything with a vehicle not created.
				Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_vehicleId} does not have a valid vehicle type! Type = {l_vehManager.m_vehicles.m_buffer[l_vehicleId].Info.m_vehicleType}");
				return false;
			}
			if ((l_vehManager.m_vehicles.m_buffer[l_vehicleId].m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
				// Can't do anything with a vehicle not created.
				Log._Debug($"DispatchManager.DispatchThread: Vehicle {l_vehicleId} not created.");
				return false;
			}
			*/
			if ((l_vehManager.m_vehicles.m_buffer[l_vehicleId].m_flags & Vehicle.Flags.GoingBack) != 0) {
				// Can't re-assign going back
				//Log._Debug($"DispatchManager.DispatchThread Vehicle {l_vehicleId} marked for going back. Can't use");
				return false;
			}
			return true;
		}
		private void DispatchVehicle(ushort vehicleId, ref Vehicle vehicleInfo, ushort buildingId, VehicleAI vehicleAI) {
			VehicleManager l_vehManager = Singleton<VehicleManager>.instance;
			Log._Debug($"DispatchManager.DispatchVehicle: Adding building {buildingId} to vehicle {vehicleId} mapping");
			if (m_BuildingsToVehicleAssignment.ContainsKey(buildingId)) {
				// Shouldn't happen, but does for some reason
				m_BuildingsToVehicleAssignment.Remove(buildingId);
			}
			m_BuildingsToVehicleAssignment.Add(buildingId, vehicleId);
			Log._Debug($"DispatchManager.DispatchVehicle dispatching {vehicleId} to building {buildingId}");
			Log._Debug($"DispatchManager.DispatchVehicle Vehicle {vehicleId} data: m_flags: {vehicleInfo.m_flags} m_transferSize: {vehicleInfo.m_transferSize} type: {vehicleInfo.Info.m_vehicleType}");
			(vehicleAI).SetTarget(vehicleId, ref vehicleInfo, buildingId);
		}

	}
}
