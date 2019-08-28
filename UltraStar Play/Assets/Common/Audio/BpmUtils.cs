﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BpmUtils
{
    public static float BeatToSecondsInSong(SongMeta songMeta, double beat) {
        // Ultrastar BPM is not "beats per minute" but "bars per minute" in four-four-time.
        // To get the common "beats per minute", one has to multiply with 4.
        var beatsPerMinute = songMeta.Bpm * 4.0;
        var secondsPerBeat = 60.0 / beatsPerMinute;
        var secondsInSong = beat * secondsPerBeat;
        return (float)secondsInSong;
    }

    public static double MillisecondInSongToBeat(SongMeta songMeta, double millisInSong) {
        var millisInSongAfterGap = millisInSong - songMeta.Gap;
        // Ultrastar BPM is not "beats per minute" but "bars per minute" in four-four-time.
        // To get the common "beats per minute", one has to multiply with 4.
        var beatsPerMinute = songMeta.Bpm * 4;
        var result = beatsPerMinute * millisInSongAfterGap / 1000.0 / 60.0;
        return result;
    }
}
