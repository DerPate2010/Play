using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;
using System.Collections;

public class RecordingOptionsMicVisualizer : MonoBehaviour
{
    public Text currentNoteLabel;
    public MicrophonePitchTracker microphonePitchTracker;

    private IDisposable pitchEventStreamDisposable;

    private AudioWaveFormVisualizer audioWaveFormVisualizer;

    private float micAmplifyMultiplier = 1;

    private IDisposable disposable;

    void Awake()
    {
        audioWaveFormVisualizer = GetComponentInChildren<AudioWaveFormVisualizer>();
    }

    void Update()
    {
        UpdateWaveForm();
    }

    private void UpdateWaveForm()
    {
        MicProfile micProfile = microphonePitchTracker.MicProfile;
        if (micProfile == null)
        {
            return;
        }

        float[] micData = microphonePitchTracker.MicData;

        // Apply noise suppression and amplification to the buffer
        float[] displayData = new float[micData.Length];
        float noiseThreshold = micProfile.NoiseSuppression / 100f;
        if (micData.AnyMatch(sample => sample >= noiseThreshold))
        {
            for (int i = 0; i < micData.Length; i++)
            {
                displayData[i] = NumberUtils.Limit(micData[i] * micAmplifyMultiplier, -1, 1);
            }
        }

        audioWaveFormVisualizer.DrawWaveFormValues(displayData, micData.Length - 4048, 4048);
    }

    public void SetMicProfile(MicProfile micProfile)
    {
        microphonePitchTracker.MicProfile = micProfile;
        if (!string.IsNullOrEmpty(micProfile.Name))
        {
            microphonePitchTracker.StartPitchDetection();
        }
        micAmplifyMultiplier = micProfile.AmplificationMultiplier();

        if (disposable != null)
        {
            disposable.Dispose();
        }
        disposable = micProfile.ObserveEveryValueChanged(it => it.Amplification)
            .Subscribe(newAmplification => micAmplifyMultiplier = micProfile.AmplificationMultiplier());
    }

    void OnEnable()
    {
        pitchEventStreamDisposable = microphonePitchTracker.PitchEventStream.Subscribe(OnPitchDetected);
    }

    void OnDisable()
    {
        pitchEventStreamDisposable?.Dispose();
    }

    private void OnPitchDetected(PitchEvent pitchEvent)
    {
        // Show the note that has been detected
        if (pitchEvent != null && pitchEvent.MidiNote > 0)
        {
            currentNoteLabel.text = "Note: " + MidiUtils.GetAbsoluteName(pitchEvent.MidiNote);
        }
        else
        {
            currentNoteLabel.text = "Note: ?";
        }
    }
}