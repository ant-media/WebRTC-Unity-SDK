using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;



namespace Unity.WebRTC.AntMedia.SDK
{
    class AMSStreamingSample : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Dropdown modeDropdown;
        [SerializeField] private Camera cam;
        [SerializeField] private RawImage sourceImage;
        [SerializeField] private AudioSource sourceAudio;
        [SerializeField] private RawImage receiveImage;
        [SerializeField] private AudioSource receiveAudio;
#pragma warning restore 0649

        public const int MODE_P2P = 0;
        public const int MODE_PUBLISH = 1;
        public const int MODE_PLAY = 2;
        private VideoStreamTrack videoStreamTrack;
        private AudioStreamTrack audioStreamTrack;
        private WebCamTexture webCamTexture;
        private MediaStream localStream;
        private int mode = MODE_P2P;

        WebRTCClient webRTClient;

        private void Awake()
        {
            WebRTC.Initialize(true);
            startButton.onClick.AddListener(StartButtonPressed);
            stopButton.onClick.AddListener(StopButtonPressed);
        }

        private void OnDestroy()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                webCamTexture = null;
            }

            WebRTC.Dispose();
        }

        private void Start()
        {  
            StartCoroutine(WebRTC.Update());
            startButton.interactable = true;
            stopButton.interactable = false;
        }
        
        private IEnumerator StartStreaming()
        {           
            string websocketUrl = "ws://localhost:5080/LiveApp/websocket";
            //string websocketUrl = "wss://meet.antmedia.io:5443/LiveApp/websocket";
            webRTClient = new WebRTCClient("stream1", this, websocketUrl);
            localStream = new MediaStream();

            if(mode != MODE_PLAY) {
                CaptureAudioStart();
                StartCoroutine(CaptureVideoStart());
            }

            Debug.Log("Waiting for websocket connection...");
            yield return new WaitUntil(() => webRTClient.IsReady());

            if(mode == MODE_P2P) {
                webRTClient.Join();
                webRTClient.SetLocalStream(localStream);
            }
            else if(mode == MODE_PUBLISH) {
                webRTClient.Publish();
                webRTClient.SetLocalStream(localStream);
            }
            else if(mode == MODE_PLAY) {
                webRTClient.Play();
            }
            
            webRTClient.setDelegateOnTrack(e =>
            {

                if (e.Track is VideoStreamTrack video)
                {
                    video.OnVideoReceived += tex =>
                    {
                        receiveImage.texture = tex;
                    };
                }

                if (e.Track is AudioStreamTrack audioTrack)
                {
                    receiveAudio.SetTrack(audioTrack);
                    receiveAudio.loop = true;
                    receiveAudio.Play();
                }
            });
        }

        private void Update()
        {
            if(webRTClient != null) {
                webRTClient.Update();
            }
        }

        private void StartButtonPressed()
        {
            string selectedMode = modeDropdown.options[modeDropdown.value].text;

            if(String.Equals(selectedMode, "Publish")) {
                mode = MODE_PUBLISH; 
            }
            else if(String.Equals(selectedMode, "Play"))  {
                mode = MODE_PLAY;     
            }
            else if(String.Equals(selectedMode, "P2P"))  {
                mode = MODE_P2P;     
            }
            else {
                Debug.Log("Undefined Streaming Mode:"+mode);
            }

            Debug.Log("Streaming Mode:"+mode);

            startButton.interactable = false;
            stopButton.interactable = true;
            StartCoroutine(StartStreaming());
        }

        private void StopButtonPressed()
        {
            webRTClient.Leave();
            startButton.interactable = true;
            stopButton.interactable = false;
        }
      

        private void CaptureAudioStart()
        {
            var deviceName = Microphone.devices[0];
            Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
            var micClip = Microphone.Start(deviceName, true, 1, 48000);

            // set the latency to “0” samples before the audio starts to play.
            while (!(Microphone.GetPosition(deviceName) > 0)) {}

            sourceAudio.clip = micClip;
            sourceAudio.loop = true;
            sourceAudio.Play();
            audioStreamTrack = new AudioStreamTrack(sourceAudio);
            
            localStream.AddTrack(audioStreamTrack);
        }


        private IEnumerator CaptureVideoStart()
        {
            WebCamDevice userCameraDevice = WebCamTexture.devices[0];
            webCamTexture = new WebCamTexture(userCameraDevice.name, 1280, 720, 30);
            webCamTexture.Play();
            yield return new WaitUntil(() => webCamTexture.didUpdateThisFrame);          

            videoStreamTrack = new VideoStreamTrack(webCamTexture);
            sourceImage.texture = webCamTexture;
            localStream.AddTrack(videoStreamTrack);

        }
    }

}
