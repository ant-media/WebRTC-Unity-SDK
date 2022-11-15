using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;



namespace Unity.WebRTC.AntMedia.SDK
{
    class AMSPeer2Peer : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Camera cam;
        [SerializeField] private RawImage sourceImage;
        [SerializeField] private AudioSource sourceAudio;
        [SerializeField] private RawImage receiveImage;
        [SerializeField] private AudioSource receiveAudio;
        [SerializeField] private Transform rotateObject;
#pragma warning restore 0649

        private VideoStreamTrack videoStreamTrack;
        private AudioStreamTrack audioStreamTrack;
        private WebCamTexture webCamTexture;
        private MediaStream localStream;

        WebRTCClient webRTClient;

        private void Awake()
        {
            WebRTC.Initialize(WebRTCSettings.LimitTextureSize);
            joinButton.onClick.AddListener(Join);
            leaveButton.onClick.AddListener(Leave);
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
            webRTClient = new WebRTCClient("stream1", this);
            joinButton.interactable = true;
            leaveButton.interactable = false;
            localStream = new MediaStream();



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

            CaptureAudioStart();
            StartCoroutine(CaptureVideoStart());
            StartCoroutine(WebRTC.Update());
        }

        private void Update()
        {
            if (rotateObject != null)
            {
                rotateObject.Rotate(1, 2, 3);
            }

            webRTClient.Update();
        }

        private void Join()
        {
            webRTClient.SendJoinMessage();
        }

        private void Leave()
        {
            webRTClient.SendJoinMessage();
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
            webCamTexture = new WebCamTexture(userCameraDevice.name, WebRTCSettings.StreamSize.x, WebRTCSettings.StreamSize.y, 30);
            webCamTexture.Play();
            yield return new WaitUntil(() => webCamTexture.didUpdateThisFrame);          

            videoStreamTrack = new VideoStreamTrack(webCamTexture);
            sourceImage.texture = webCamTexture;
            localStream.AddTrack(videoStreamTrack);

            webRTClient.SetLocalStream(localStream);
        }
    }

}
