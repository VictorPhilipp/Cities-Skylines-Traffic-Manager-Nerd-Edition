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
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using UnityEngine;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	class CustomHearseAI: CarAI {
		public void CustomStartTransfer(ushort vehicleID, ref Vehicle data, TransferManager.TransferReason material, TransferManager.TransferOffer offer) {
			Log._Debug($"CustomHearseAI.CustomStartTransfer called for vehicle {vehicleID} to building {offer.Building}");
			if (material == (TransferManager.TransferReason)data.m_transferType) {
				if (material == TransferManager.TransferReason.DeadMove) {
					if ((data.m_flags & Vehicle.Flags.WaitingTarget) != 0) {
						((VehicleAI)this).SetTarget(vehicleID, ref data, offer.Building);
					}
				} else if ((data.m_flags & Vehicle.Flags.WaitingTarget) != 0) {
					uint citizen = offer.Citizen;
					ushort building = offer.Building;
					if (citizen != 0) {
						ushort buildingByLocation = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizen].GetBuildingByLocation();
						if (Options.advancedHearseAI) {
							DispatchManager.Instance.AddBuildingWithDead(new DispatchManager.DispatchBufferItem(this, vehicleID, buildingByLocation, offer.Amount, offer.Priority));
						} else {
							((VehicleAI)this).SetTarget(vehicleID, ref data, buildingByLocation);
						}
						Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizen].SetVehicle(citizen, vehicleID, 0u);
						data.m_transferSize++;
					} else if (building != 0) {
						if (Options.advancedHearseAI) {
							DispatchManager.Instance.AddBuildingWithDead(new DispatchManager.DispatchBufferItem(this, vehicleID, building, offer.Amount, offer.Priority));
						} else {
							((VehicleAI)this).SetTarget(vehicleID, ref data, building);
						}
					}
				}
			} else {
				base.StartTransfer(vehicleID, ref data, material, offer);
			}
		}

		private void RemoveOffers(ushort vehicleID, ref Vehicle data) {
			if ((data.m_flags & Vehicle.Flags.WaitingTarget) != 0) {
				TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
				offer.Vehicle = vehicleID;
				if ((data.m_flags & Vehicle.Flags.TransferToSource) != 0) {
					Singleton<TransferManager>.instance.RemoveIncomingOffer((TransferManager.TransferReason)data.m_transferType, offer);
				} else if ((data.m_flags & Vehicle.Flags.TransferToTarget) != 0) {
					Singleton<TransferManager>.instance.RemoveOutgoingOffer((TransferManager.TransferReason)data.m_transferType, offer);
				}
			}
		}

		public void CustomSimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos) {
			if ((data.m_flags & Vehicle.Flags.WaitingTarget) != 0 && !DispatchManager.Instance.CheckIfVehicleQueued(vehicleID) && ++data.m_waitCounter > 20) {
				this.RemoveOffers(vehicleID, ref data);
				data.m_flags &= ~Vehicle.Flags.WaitingTarget;
				data.m_flags |= Vehicle.Flags.GoingBack;
				data.m_waitCounter = 0;
				if (!(this.StartPathFind(vehicleID, ref data))) {
					data.Unspawn(vehicleID);
				}
			}
			base.SimulationStep(vehicleID, ref data, physicsLodRefPos);
		}

		private void RemoveSource(ushort vehicleID, ref Vehicle data) {
			if (data.m_sourceBuilding != 0) {
				Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].RemoveOwnVehicle(vehicleID, ref data);
				data.m_sourceBuilding = 0;
			}
		}

		private void RemoveTarget(ushort vehicleID, ref Vehicle data) {
			if (data.m_targetBuilding != 0) {
				Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_targetBuilding].RemoveGuestVehicle(vehicleID, ref data);
				data.m_targetBuilding = 0;
			}
		}

		public void CustomReleaseVehicle(ushort vehicleID, ref Vehicle data) {
			if (data.m_transferType == 42) {
				if (data.m_sourceBuilding != 0 && data.m_transferSize != 0) {
					int transferSize = data.m_transferSize;
					BuildingInfo info = Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].Info;
					if ((object)info != null) {
						info.m_buildingAI.ModifyMaterialBuffer(data.m_sourceBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding], (TransferManager.TransferReason)data.m_transferType, ref transferSize);
						data.m_transferSize = (ushort)Mathf.Clamp(data.m_transferSize - transferSize, 0, data.m_transferSize);
					}
				}
			} else if (data.m_sourceBuilding != 0) {
				ushort buildingID = 0;
				BuildingInfo info2 = Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].Info;
				if ((object)info2 != null) {
					buildingID = data.m_sourceBuilding;
				}
				CitizenManager instance = Singleton<CitizenManager>.instance;
				uint num = data.m_citizenUnits;
				int num2 = 0;
				while (num != 0) {
					uint nextUnit = instance.m_units.m_buffer[num].m_nextUnit;
					for (int i = 0; i < 5; i++) {
						uint citizen = instance.m_units.m_buffer[num].GetCitizen(i);
						if (citizen != 0) {
							instance.m_citizens.m_buffer[citizen].SetVehicle(citizen, 0, 0u);
							instance.m_citizens.m_buffer[citizen].SetVisitplace(citizen, buildingID, 0u);
							instance.m_citizens.m_buffer[citizen].CurrentLocation = Citizen.Location.Visit;
						}
					}
					num = nextUnit;
					if (++num2 > 524288) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			this.RemoveOffers(vehicleID, ref data);
			this.RemoveSource(vehicleID, ref data);
			this.RemoveTarget(vehicleID, ref data);
			base.ReleaseVehicle(vehicleID, ref data);
		}

		public bool CustomArriveAtDestination(ushort vehicleID, ref Vehicle vehicleData) {
			DispatchManager.Instance.VehicleArrivedAtDestination(vehicleID);
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingTarget) != 0) {
				return false;
			}
			if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != 0) {
				return this.ArriveAtSource(vehicleID, ref vehicleData);
			}
			return this.ArriveAtTarget(vehicleID, ref vehicleData);
		}


		private bool ArriveAtSource(ushort vehicleID, ref Vehicle data) {
			if (data.m_sourceBuilding == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
				return true;
			}
			if (data.m_transferType == 42) {
				int num = 0;
				if ((data.m_flags & Vehicle.Flags.TransferToSource) != 0) {
					num = data.m_transferSize;
					BuildingInfo info = Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].Info;
					info.m_buildingAI.ModifyMaterialBuffer(data.m_sourceBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding], (TransferManager.TransferReason)data.m_transferType, ref num);
					data.m_transferSize = (ushort)Mathf.Clamp(data.m_transferSize - num, 0, data.m_transferSize);
				}
			} else {
				CitizenManager instance = Singleton<CitizenManager>.instance;
				uint num2 = data.m_citizenUnits;
				int num3 = 0;
				while (num2 != 0) {
					uint nextUnit = instance.m_units.m_buffer[num2].m_nextUnit;
					for (int i = 0; i < 5; i++) {
						uint citizen = instance.m_units.m_buffer[num2].GetCitizen(i);
						if (citizen != 0 && instance.m_citizens.m_buffer[citizen].Dead) {
							instance.m_citizens.m_buffer[citizen].SetVehicle(citizen, 0, 0u);
							instance.m_citizens.m_buffer[citizen].SetVisitplace(citizen, data.m_sourceBuilding, 0u);
							instance.m_citizens.m_buffer[citizen].CurrentLocation = Citizen.Location.Visit;
						}
					}
					num2 = nextUnit;
					if (++num3 > 524288) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			this.RemoveSource(vehicleID, ref data);
			Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
			return true;
		}

		private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data) {
			if (data.m_targetBuilding == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
				return true;
			}
			if (data.m_transferType == 42) {
				int num = 0;
				if ((data.m_flags & Vehicle.Flags.TransferToTarget) != 0) {
					num = data.m_transferSize;
				}
				if ((data.m_flags & Vehicle.Flags.TransferToSource) != 0) {
					FieldInfo fi_corpseCapacity = this.GetType().GetField("m_corpseCapacity", BindingFlags.Public | BindingFlags.Instance);
					int corpseCapacity = (int)fi_corpseCapacity.GetValue(this);
					num = Mathf.Min(0, data.m_transferSize - corpseCapacity);
				}
				BuildingManager instance = Singleton<BuildingManager>.instance;
				BuildingInfo info = instance.m_buildings.m_buffer[data.m_targetBuilding].Info;
				info.m_buildingAI.ModifyMaterialBuffer(data.m_targetBuilding, ref instance.m_buildings.m_buffer[data.m_targetBuilding], (TransferManager.TransferReason)data.m_transferType, ref num);
				if ((data.m_flags & Vehicle.Flags.TransferToTarget) != 0) {
					data.m_transferSize = (ushort)Mathf.Clamp(data.m_transferSize - num, 0, data.m_transferSize);
				}
				if ((data.m_flags & Vehicle.Flags.TransferToSource) != 0) {
					data.m_transferSize += (ushort)Mathf.Max(0, -num);
				}
				if (data.m_sourceBuilding != 0 && (instance.m_buildings.m_buffer[data.m_sourceBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.Outgoing) {
					BuildingInfo info2 = instance.m_buildings.m_buffer[data.m_sourceBuilding].Info;
					ushort num2 = instance.FindBuilding(instance.m_buildings.m_buffer[data.m_sourceBuilding].m_position, 200f, info2.m_class.m_service, info2.m_class.m_subService, Building.Flags.Incoming, Building.Flags.Outgoing);
					if (num2 != 0) {
						instance.m_buildings.m_buffer[data.m_sourceBuilding].RemoveOwnVehicle(vehicleID, ref data);
						data.m_sourceBuilding = num2;
						instance.m_buildings.m_buffer[data.m_sourceBuilding].AddOwnVehicle(vehicleID, ref data);
					}
				}
			} else {
				this.LoadDeadCitizens(vehicleID, ref data, data.m_targetBuilding);
				FieldInfo fi_driverCount = this.GetType().GetField("m_driverCount", BindingFlags.Public | BindingFlags.Instance);
				for (int i = 0; i < (int)fi_driverCount.GetValue(this); i++) {
					this.CreateDriver(vehicleID, ref data, Citizen.AgePhase.Senior0);
				}
				data.m_flags |= Vehicle.Flags.Stopped;
			}
			((VehicleAI)this).SetTarget(vehicleID, ref data, (ushort)0);
			return false;
		}

		private void LoadDeadCitizens(ushort vehicleID, ref Vehicle data, ushort buildingID) {
			BuildingManager instance = Singleton<BuildingManager>.instance;
			CitizenManager instance2 = Singleton<CitizenManager>.instance;
			uint num = data.m_citizenUnits;
			int num2 = 0;
			while (num != 0) {
				uint nextUnit = instance2.m_units.m_buffer[num].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizen = instance2.m_units.m_buffer[num].GetCitizen(i);
					if (citizen != 0 && instance2.m_citizens.m_buffer[citizen].Dead && instance2.m_citizens.m_buffer[citizen].CurrentLocation != Citizen.Location.Moving) {
						ushort instance3 = instance2.m_citizens.m_buffer[citizen].m_instance;
						if (instance3 != 0) {
							instance2.m_citizens.m_buffer[citizen].m_instance = 0;
							instance2.m_instances.m_buffer[instance3].m_citizen = 0u;
							instance2.ReleaseCitizenInstance(instance3);
						}
						instance2.m_citizens.m_buffer[citizen].CurrentLocation = Citizen.Location.Moving;
					}
				}
				num = nextUnit;
				if (++num2 > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			FieldInfo fi_corpseCapacity = this.GetType().GetField("m_corpseCapacity", BindingFlags.Public | BindingFlags.Instance);
			int corpseCapacity = (int) fi_corpseCapacity.GetValue(this);
			if (data.m_sourceBuilding == 0) {
				corpseCapacity = data.m_transferSize;
			} else if ((instance.m_buildings.m_buffer[data.m_sourceBuilding].m_flags & Building.Flags.Active) == Building.Flags.None) {
				corpseCapacity = data.m_transferSize;
			} else {
				BuildingInfo info = instance.m_buildings.m_buffer[data.m_sourceBuilding].Info;
				int num3 = default(int);
				int num4 = default(int);
				info.m_buildingAI.GetMaterialAmount(data.m_sourceBuilding, ref instance.m_buildings.m_buffer[data.m_sourceBuilding], TransferManager.TransferReason.Dead, out num3, out num4);
				corpseCapacity = Mathf.Min(corpseCapacity, num4 - num3);
			}
			if (data.m_transferSize < corpseCapacity) {
				num = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingID].m_citizenUnits;
				num2 = 0;
				do {
					if (num == 0) {
						return;
					}
					uint nextUnit2 = instance2.m_units.m_buffer[num].m_nextUnit;
					for (int j = 0; j < 5; j++) {
						uint citizen2 = instance2.m_units.m_buffer[num].GetCitizen(j);
						if (citizen2 != 0 && instance2.m_citizens.m_buffer[citizen2].Dead && instance2.m_citizens.m_buffer[citizen2].GetBuildingByLocation() == buildingID) {
							ushort instance4 = instance2.m_citizens.m_buffer[citizen2].m_instance;
							if (instance4 != 0) {
								instance2.ReleaseCitizenInstance(instance4);
							}
							instance2.m_citizens.m_buffer[citizen2].SetVehicle(citizen2, vehicleID, 0u);
							instance2.m_citizens.m_buffer[citizen2].CurrentLocation = Citizen.Location.Moving;
							if (++data.m_transferSize >= corpseCapacity) {
								return;
							}
						}
					}
					num = nextUnit2;
				}
				while (++num2 <= 524288);
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
			}
		}

		private void CreateDriver(ushort vehicleID, ref Vehicle data, Citizen.AgePhase agePhase) {
			SimulationManager instance = Singleton<SimulationManager>.instance;
			CitizenManager instance2 = Singleton<CitizenManager>.instance;
			CitizenInfo groupCitizenInfo = instance2.GetGroupCitizenInfo(ref instance.m_randomizer, base.m_info.m_class.m_service, Citizen.Gender.Male, Citizen.SubCulture.Generic, agePhase);
			if ((object)groupCitizenInfo != null) {
				int family = instance.m_randomizer.Int32(256u);
				uint num = 0u;
				if (instance2.CreateCitizen(out num, 90, family, ref instance.m_randomizer, groupCitizenInfo.m_gender)) {
					ushort num2 = default(ushort);
					if (instance2.CreateCitizenInstance(out num2, ref instance.m_randomizer, groupCitizenInfo, num)) {
						Vector3 randomDoorPosition = data.GetRandomDoorPosition(ref instance.m_randomizer, VehicleInfo.DoorType.Exit);
						groupCitizenInfo.m_citizenAI.SetCurrentVehicle(num2, ref instance2.m_instances.m_buffer[num2], (ushort)0, 0u, randomDoorPosition);
						groupCitizenInfo.m_citizenAI.SetTarget(num2, ref instance2.m_instances.m_buffer[num2], data.m_targetBuilding);
						instance2.m_citizens.m_buffer[num].SetVehicle(num, vehicleID, 0u);
					} else {
						instance2.ReleaseCitizen(num);
					}
				}
			}
		}

	}
}
