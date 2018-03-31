using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ColossalFramework;
using ColossalFramework.IO;
using CSUtil.Commons;
using UnityEngine;

namespace TrafficManager.Custom.Manager {
	class CustomTransferManager : SimulationManagerBase<TransferManager, TransferProperties>, ISimulationManager {

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void OriginalMatchOffers(TransferManager.TransferReason material) {
			Log.Error("CustomTransferManager.OriginalMatchOffers called.");
		}

		private void CustomMatchOffers(TransferManager.TransferReason material) {
			if (material != TransferManager.TransferReason.Dead) {
				this.OriginalMatchOffers(material);
			}
			return;
		}

	}
}
