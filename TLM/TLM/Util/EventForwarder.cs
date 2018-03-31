using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Util {
	/**
 * Utility component to add to game objects whose events you want forwarded from Unity's message
 * system to standard C# events. Handles all events as of Unity 4.6.1.
 * @author Jackson Dunstan - http://jacksondunstan.com/articles/2922
 */
	public class EventForwarder : MonoBehaviour {
		public delegate void EventHandler0();
		public delegate void EventHandler1<TParam>(TParam param);
		public delegate void EventHandler2<TParam1, TParam2>(TParam1 param1, TParam2 param2);

		public event EventHandler0 AwakeEvent = () => { };
		public event EventHandler0 FixedUpdateEvent = () => { };
		public event EventHandler0 LateUpdateEvent = () => { };
		public event EventHandler1<int> OnAnimatorIKEvent = layerIndex => { };
		public event EventHandler0 OnAnimatorMoveEvent = () => { };
		public event EventHandler1<bool> OnApplicationFocusEvent = focusStatus => { };
		public event EventHandler1<bool> OnApplicationPauseEvent = pauseStatus => { };
		public event EventHandler0 OnApplicationQuitEvent = () => { };
		public event EventHandler2<float[], int> OnAudioFilterReadEvent = (data, channels) => { };
		public event EventHandler0 OnBecameInvisibleEvent = () => { };
		public event EventHandler0 OnBecameVisibleEvent = () => { };
		public event EventHandler1<Collision> OnCollisionEnterEvent = collision => { };
		public event EventHandler1<Collision2D> OnCollisionEnter2DEvent = collision => { };
		public event EventHandler1<Collision> OnCollisionExitEvent = collision => { };
		public event EventHandler1<Collision2D> OnCollisionExit2DEvent = collision => { };
		public event EventHandler1<Collision> OnCollisionStayEvent = collision => { };
		public event EventHandler1<Collision2D> OnCollisionStay2DEvent = collision => { };
		public event EventHandler0 OnConnectedToServerEvent = () => { };
		public event EventHandler1<ControllerColliderHit> OnControllerColliderHitEvent = hit => { };
		public event EventHandler0 OnDestroyEvent = () => { };
		public event EventHandler0 OnDisableEvent = () => { };
		public event EventHandler1<NetworkDisconnection> OnDisconnectedFromServerEvent = info => { };
		public event EventHandler0 OnDrawGizmosEvent = () => { };
		public event EventHandler0 OnDrawGizmosSelectedEvent = () => { };
		public event EventHandler0 OnEnableEvent = () => { };
		public event EventHandler1<NetworkConnectionError> OnFailedToConnectEvent = error => { };
		public event EventHandler1<NetworkConnectionError> OnFailedToConnectToMasterServerEvent = error => { };
		public event EventHandler0 OnGUIEvent = () => { };
		public event EventHandler1<float> OnJointBreakEvent = breakForce => { };
		public event EventHandler1<int> OnLevelWasLoadedEvent = level => { };
		public event EventHandler1<MasterServerEvent> OnMasterServerEventEvent = msEvent => { };
		public event EventHandler0 OnMouseDownEvent = () => { };
		public event EventHandler0 OnMouseDragEvent = () => { };
		public event EventHandler0 OnMouseEnterEvent = () => { };
		public event EventHandler0 OnMouseExitEvent = () => { };
		public event EventHandler0 OnMouseOverEvent = () => { };
		public event EventHandler0 OnMouseUpEvent = () => { };
		public event EventHandler0 OnMouseUpAsButtonEvent = () => { };
		public event EventHandler1<NetworkMessageInfo> OnNetworkInstantiateEvent = info => { };
		public event EventHandler1<GameObject> OnParticleCollisionEvent = other => { };
		public event EventHandler1<NetworkPlayer> OnPlayerConnectedEvent = player => { };
		public event EventHandler1<NetworkPlayer> OnPlayerDisconnectedEvent = player => { };
		public event EventHandler0 OnPostRenderEvent = () => { };
		public event EventHandler0 OnPreCullEvent = () => { };
		public event EventHandler0 OnPreRenderEvent = () => { };
		public event EventHandler2<RenderTexture, RenderTexture> OnRenderImageEvent = (src, dest) => { };
		public event EventHandler0 OnRenderObjectEvent = () => { };
		public event EventHandler2<BitStream, NetworkMessageInfo> OnSerializeNetworkViewEvent = (stream, info) => { };
		public event EventHandler0 OnServerInitializedEvent = () => { };
		public event EventHandler1<Collider> OnTriggerEnterEvent = other => { };
		public event EventHandler1<Collider2D> OnTriggerEnter2DEvent = other => { };
		public event EventHandler1<Collider> OnTriggerExitEvent = other => { };
		public event EventHandler1<Collider2D> OnTriggerExit2DEvent = other => { };
		public event EventHandler1<Collider> OnTriggerStayEvent = other => { };
		public event EventHandler1<Collider2D> OnTriggerStay2DEvent = other => { };
		public event EventHandler0 OnValidateEvent = () => { };
		public event EventHandler0 OnWillRenderObjectEvent = () => { };
		public event EventHandler0 ResetEvent = () => { };
		public event EventHandler0 StartEvent = () => { };
		public event EventHandler0 UpdateEvent = () => { };

		public void Awake() {
			AwakeEvent();
		}

		public void FixedUpdate() {
			FixedUpdateEvent();
		}

		public void LateUpdate() {
			LateUpdateEvent();
		}

		public void OnAnimatorIK(int layerIndex) {
			OnAnimatorIKEvent(layerIndex);
		}

		public void OnAnimatorMove() {
			OnAnimatorMoveEvent();
		}

		public void OnApplicationFocus(bool focusStatus) {
			OnApplicationFocusEvent(focusStatus);
		}

		public void OnApplicationPause(bool pauseStatus) {
			OnApplicationPauseEvent(pauseStatus);
		}

		public void OnApplicationQuit() {
			OnApplicationQuitEvent();
		}

		public void OnAudioFilterRead(float[] data, int channels) {
			OnAudioFilterReadEvent(data, channels);
		}

		public void OnBecameInvisible() {
			OnBecameInvisibleEvent();
		}

		public void OnBecameVisible() {
			OnBecameVisibleEvent();
		}

		public void OnCollisionEnter(Collision collision) {
			OnCollisionEnterEvent(collision);
		}

		public void OnCollisionEnter2D(Collision2D collision) {
			OnCollisionEnter2DEvent(collision);
		}

		public void OnCollisionExit(Collision collision) {
			OnCollisionExitEvent(collision);
		}

		public void OnCollisionExit2D(Collision2D collision) {
			OnCollisionExit2DEvent(collision);
		}

		public void OnCollisionStay(Collision collision) {
			OnCollisionStayEvent(collision);
		}

		public void OnCollisionStay2D(Collision2D collision) {
			OnCollisionStay2DEvent(collision);
		}

		public void OnConnectedToServer() {
			OnConnectedToServerEvent();
		}

		public void OnControllerColliderHit(ControllerColliderHit hit) {
			OnControllerColliderHitEvent(hit);
		}

		public void OnDestroy() {
			OnDestroyEvent();
		}

		public void OnDisable() {
			OnDisableEvent();
		}

		public void OnDisconnectedFromServer(NetworkDisconnection info) {
			OnDisconnectedFromServerEvent(info);
		}

		public void OnDrawGizmos() {
			OnDrawGizmosEvent();
		}

		public void OnDrawGizmosSelected() {
			OnDrawGizmosSelectedEvent();
		}

		public void OnEnable() {
			OnEnableEvent();
		}

		public void OnFailedToConnect(NetworkConnectionError error) {
			OnFailedToConnectEvent(error);
		}

		public void OnFailedToConnectToMasterServer(NetworkConnectionError error) {
			OnFailedToConnectToMasterServerEvent(error);
		}

		public void OnGUI() {
			OnGUIEvent();
		}

		public void OnJointBreak(float breakForce) {
			OnJointBreakEvent(breakForce);
		}

		public void OnLevelWasLoaded(int level) {
			OnLevelWasLoadedEvent(level);
		}

		public void OnMasterServerEvent(MasterServerEvent msEvent) {
			OnMasterServerEventEvent(msEvent);
		}

		public void OnMouseDown() {
			OnMouseDownEvent();
		}

		public void OnMouseDrag() {
			OnMouseDragEvent();
		}

		public void OnMouseEnter() {
			OnMouseEnterEvent();
		}

		public void OnMouseExit() {
			OnMouseExitEvent();
		}

		public void OnMouseOver() {
			OnMouseOverEvent();
		}

		public void OnMouseUp() {
			OnMouseUpEvent();
		}

		public void OnMouseUpAsButton() {
			OnMouseUpAsButtonEvent();
		}

		public void OnNetworkInstantiate(NetworkMessageInfo info) {
			OnNetworkInstantiateEvent(info);
		}

		public void OnParticleCollision(GameObject other) {
			OnParticleCollisionEvent(other);
		}

		public void OnPlayerConnected(NetworkPlayer player) {
			OnPlayerConnectedEvent(player);
		}

		public void OnPlayerDisconnected(NetworkPlayer player) {
			OnPlayerDisconnectedEvent(player);
		}

		public void OnPostRender() {
			OnPostRenderEvent();
		}

		public void OnPreCull() {
			OnPreCullEvent();
		}

		public void OnPreRender() {
			OnPreRenderEvent();
		}

		public void OnRenderImage(RenderTexture src, RenderTexture dest) {
			OnRenderImageEvent(src, dest);
		}

		public void OnRenderObject() {
			OnRenderObjectEvent();
		}

		public void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
			OnSerializeNetworkViewEvent(stream, info);
		}

		public void OnServerInitialized() {
			OnServerInitializedEvent();
		}

		public void OnTriggerEnter(Collider other) {
			OnTriggerEnterEvent(other);
		}

		public void OnTriggerEnter2D(Collider2D other) {
			OnTriggerEnter2DEvent(other);
		}

		public void OnTriggerExit(Collider other) {
			OnTriggerExitEvent(other);
		}

		public void OnTriggerExit2D(Collider2D other) {
			OnTriggerExit2DEvent(other);
		}

		public void OnTriggerStay(Collider other) {
			OnTriggerStayEvent(other);
		}

		public void OnTriggerStay2D(Collider2D other) {
			OnTriggerStay2DEvent(other);
		}

		public void OnValidate() {
			OnValidateEvent();
		}

		public void OnWillRenderObject() {
			OnWillRenderObjectEvent();
		}

		public void Reset() {
			ResetEvent();
		}

		public void Start() {
			StartEvent();
		}

		public void Update() {
			UpdateEvent();
		}
	}
}
