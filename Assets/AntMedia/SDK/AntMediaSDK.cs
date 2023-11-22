using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NativeWebSocket;
using SimpleJSON;
using System;
using Unity.WebRTC;


namespace Unity.WebRTC.AntMedia.SDK
{
   
    public class WebRTCClient
    {
        string streamId;
        string tokenId;
        string websocketUrl;
        WebSocket websocket;
        private RTCPeerConnection localPC;
        MediaStream localStream;
        RTCSessionDescription remoteSDP, localSDP;
        MonoBehaviour mb;
        bool ready = false;


        public WebRTCClient(string streamId, MonoBehaviour mb, string websocketUrl, string tokenId = null) {
            this.mb = mb;
            this.streamId = streamId;
            this.tokenId = tokenId;
            this.websocketUrl = websocketUrl;

            ConnectToWebSocket();
        }

        

/*************************************************************************************************************/

        public void setDelegateOnTrack(DelegateOnTrack onTrackCallBack) {
            localPC.OnTrack = onTrackCallBack;
        }

        private void CreatePeerConnection() {
            var configuration = GetSelectedSdpSemantics();
            localPC = new RTCPeerConnection(ref configuration);
            localPC.OnIceCandidate = candidate => { 
                Debug.Log("ICE candidate created:"+ candidate.Candidate);
                SendCandidateMessage(candidate.SdpMid, (long)candidate.SdpMLineIndex, candidate.Candidate); 
            };
            localPC.OnIceConnectionChange = state => { 
                switch (state)
                {
                    case RTCIceConnectionState.Connected:
                        //PeerConnected();
                        //break;
                    case RTCIceConnectionState.New:
                    case RTCIceConnectionState.Checking:
                    case RTCIceConnectionState.Closed:
                    case RTCIceConnectionState.Completed:
                    case RTCIceConnectionState.Disconnected:
                    case RTCIceConnectionState.Failed:
                    case RTCIceConnectionState.Max:
                        Debug.Log("IceConnectionState: "+state);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, null);
                };
            };
        }

        private static RTCConfiguration GetSelectedSdpSemantics()
        {
            RTCConfiguration config = default;
            config.iceServers = new[] {new RTCIceServer {urls = new[] {"stun:stun.l.google.com:19302"}}};

            return config;
        }
      

        public void SetLocalStream(MediaStream localStream)
        {
            this.localStream = localStream;
            AddTracks();
        }

        public void AddTracks() {
            foreach (var track in localStream.GetTracks())
            {
                Debug.Log("---- Tracks "+track);
                localPC.AddTrack(track, localStream);
            }  
        }

        public void Join()
        {
            CreatePeerConnection();
            SendJoinMessage();
        }

        public void Publish()
        {
            CreatePeerConnection();
            SendPublishMessage();
        }

        public void Play()
        {
            CreatePeerConnection();
            SendPlayMessage();
        }

        public void Leave() {
            SendLeaveMessage();
            localPC.Dispose();
        }

        public bool IsReady() {
            return ready;
        }
    



/*************************************************************************************************************/

    //Signalling if second peer in P2P
    private IEnumerator TakeConfigurationMessageReceived(RTCSessionDescription sdp) {
        remoteSDP = sdp;
        if(sdp.type == RTCSdpType.Offer) {
            var op1 = localPC.SetRemoteDescription(ref remoteSDP);
            yield return op1;

            if(op1.IsError) {
                Debug.Log("Remote description is not set");
                yield break;
            }

            var op2 = localPC.CreateAnswer();
            yield return op2;

            if(op2.IsError) {
                Debug.Log("Local description (Answer) is not created");
                yield break;
            }
            else {
                localSDP = op2.Desc;
            }

            var op3 = localPC.SetLocalDescription(ref localSDP);
            yield return op3;

            if(op3.IsError) {
                Debug.Log("Local description is not set");
                yield break;
            }
   
            SendDescriptionMessage("answer", localSDP.sdp);
        }
        else {
            var op1 = localPC.SetRemoteDescription(ref remoteSDP);
            yield return op1;      
        }
    }

    //Signalling if first peer in P2P
    private IEnumerator StartMessageReceived() {
        var op1 = localPC.CreateOffer();
        yield return op1;
         
        if(op1.IsError) {
            Debug.Log("Local description (Offer) is not created");
            yield break;
        }
        else {
            localSDP = op1.Desc;
        }

        var op2 = localPC.SetLocalDescription(ref localSDP);
        yield return op2;

        if(op2.IsError) {
            Debug.Log("Local description is not set");
            yield break;
        }

        SendDescriptionMessage("offer", localSDP.sdp);
    }


/*************************************************************************************************************/

        public void ConnectToWebSocket() {
            websocket = new WebSocket(websocketUrl);
            
            websocket.OnOpen += () =>
            {
                Debug.Log("Connection open!");
                ready = true;
            };

            websocket.OnError += (e) =>
            {
                Debug.Log("Error! " + e);
            };

            websocket.OnClose += (e) =>
            {
                Debug.Log("Connection closed!");
            };

            websocket.OnMessage += (bytes) =>
            {
                // Reading a plain text message
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log("Received OnMessage! (" + bytes.Length + " bytes) " + message);
                
                MessageReceived(message);
            };

            websocket.Connect();
        }

        public void Update()
        {
            #if !UNITY_WEBGL || UNITY_EDITOR
                websocket.DispatchMessageQueue();
            #endif
        }

        private void MessageReceived(string msg) 
        {
            JSONObject jsonObject = (JSONObject)JSON.Parse(msg);

            string command = jsonObject["command"];

            if(String.Equals(command, "start")) {
                mb.StartCoroutine(StartMessageReceived()); 
            }
            else if(String.Equals(command, "takeConfiguration")) {
                string sdpTypeStr = jsonObject["type"];
                string sdp = jsonObject["sdp"];

                RTCSdpType sdpType = String.Equals(sdpTypeStr, "offer") ? RTCSdpType.Offer : RTCSdpType.Answer;
                RTCSessionDescription receivedSdp = new RTCSessionDescription {type = sdpType, sdp = sdp};

                mb.StartCoroutine(TakeConfigurationMessageReceived(receivedSdp));    
            }
            else if(String.Equals(command, "takeCandidate")) {
			    string candidate = jsonObject["candidate"];
                int label = (int)jsonObject["label"];

                RTCIceCandidateInit iceCandidateInit = new RTCIceCandidateInit();

                iceCandidateInit.candidate = candidate;
                iceCandidateInit.sdpMLineIndex = label;

                RTCIceCandidate iceCandidate = new RTCIceCandidate(iceCandidateInit);
                localPC.AddIceCandidate(iceCandidate);

		    }
            else if(String.Equals(command, "notification")) {
                string definition = jsonObject["definition"];

                if(String.Equals(definition, "joined")) {
                    Debug.Log("joined to "+streamId);
                }   
            }
        }

        private JSONObject CreateBaseMessageObject(string command, bool setToken)
        {
            JSONObject messageObj = new JSONObject();
            messageObj.Add("command", command);
            messageObj.Add("streamId", streamId);

            if (setToken && !string.IsNullOrEmpty(tokenId))
            {
                messageObj.Add("token", tokenId);
            }

            return messageObj;
        }

        public void SendWebSocketMessage(string msg)
        {
            if (websocket.State == WebSocketState.Open)
            {
                Debug.Log("Sending Message:"+msg);
                websocket.SendText(msg);
            }
        }

        public void SendJoinMessage()
        {
            JSONObject msg = CreateBaseMessageObject("join", true);
            SendWebSocketMessage(msg.ToString());
        }

        public void SendPublishMessage()
        {
            JSONObject msg = CreateBaseMessageObject("publish", true);
            SendWebSocketMessage(msg.ToString());
        }

        public void SendPlayMessage()
        {
            JSONObject msg = CreateBaseMessageObject("play", true);
            SendWebSocketMessage(msg.ToString());
        }

        public void SendLeaveMessage()
        {
            JSONObject msg = CreateBaseMessageObject("leave", false);
            SendWebSocketMessage(msg.ToString());
        }

        public void SendDescriptionMessage(string type, string sdp)
        {
            JSONObject msg = CreateBaseMessageObject("takeConfiguration", false);
            msg.Add("type", type);
            msg.Add("sdp", sdp);

            SendWebSocketMessage(msg.ToString());
        }

        public void SendCandidateMessage(string sdpMid, long mlineindex, string candidate)
        {
            JSONObject msg = CreateBaseMessageObject("takeCandidate", false);
            msg.Add("candidate", candidate);
            msg.Add("label", mlineindex);
            msg.Add("id", sdpMid);

            SendWebSocketMessage(msg.ToString());
        }
    }
}
