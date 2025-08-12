using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;

namespace Unity.WebRTC.AntMedia.SDK
{
    class PublishSample : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Camera cam;
        [SerializeField] private RawImage sourceImage;
        [SerializeField] private AudioSource sourceAudio;
        [SerializeField] private InputField streamIdField;


#pragma warning restore 0649

        private VideoStreamTrack videoStreamTrack;
        private AudioStreamTrack audioStreamTrack;
        private WebCamTexture webCamTexture;
        private MediaStream localStream;
        WebRTCClient webRTClient;

        private void Awake()
        {
            Application.runInBackground = true;
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
            //string websocketUrl = "ws://localhost:5080/LiveApp/websocket";
            string websocketUrl = "wss://test.antmedia.io:5443/unitydemo/websocket";
            webRTClient = new WebRTCClient(streamIdField.text, this, websocketUrl);
            localStream = new MediaStream();

            //CaptureAudioStart();
            CaptureAudioFake();
            StartCoroutine(CaptureVideoStart());

            Debug.Log("Waiting for websocket connection...");
            yield return new WaitUntil(() => webRTClient.IsReady());

            webRTClient.Publish();
            webRTClient.SetLocalStream(localStream);
        }
        
        private void Update()
        {
            if (webRTClient != null)
            {
                webRTClient.Update();
            }
        }

        private void StartButtonPressed()
        {
            startButton.interactable = false;
            stopButton.interactable = true;
            StartCoroutine(StartStreaming());
        }

        private void StopButtonPressed()
        {
            webRTClient.Stop();
            startButton.interactable = true;
            stopButton.interactable = false;
        }

        private void CaptureAudioFake()
        {
            int sampleRate = 48000;  
            int lengthSec = 10;      

            AudioClip fakeClip = AudioClip.Create(
                "FakeAudio",
                sampleRate * lengthSec,
                1, // mono
                sampleRate,
                false
            );

            float[] samples = new float[sampleRate * lengthSec];
            System.Random rnd = new System.Random();
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)(rnd.NextDouble() * 2 - 1); 
            }
            fakeClip.SetData(samples, 0);

            sourceAudio.clip = fakeClip;
            sourceAudio.loop = true;
            sourceAudio.Play();

            audioStreamTrack = new AudioStreamTrack(sourceAudio);
            localStream.AddTrack(audioStreamTrack);

            Debug.Log("Fake audio stream has stated.");
        }

        private void CaptureAudioStart()
        {
            var deviceName = Microphone.devices[0];
            Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
            var micClip = Microphone.Start(deviceName, true, 1, 48000);

            // set the latency to “0” samples before the audio starts to play.
            //while (!(Microphone.GetPosition(deviceName) > 0)) {}

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
