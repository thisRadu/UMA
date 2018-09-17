﻿using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;

[TrackColor(0.2f,0.4f,0.2f)]
[TrackClipType(typeof(UmaColorClip))]
[TrackBindingType(typeof(DynamicCharacterAvatar))]
public class UmaColorTrack : TrackAsset
{
    [Tooltip("Time between rebuilding the UMA texture so we aren't rebuilding it every frame")]
    public float timeStep = 0.2f;

	public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
	{
		var mixer = ScriptPlayable<UmaColorMixerBehaviour>.Create (graph, inputCount);
        mixer.GetBehaviour().timeStep = timeStep;
        return mixer;
	}
}
