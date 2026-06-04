using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public AudioSource sceneFinish;
    public static bool IsAducationSceneFinished = false;
    public AudioSource avalancheVoice;
    private void OnParticleSystemStopped()
    {
        Debug.Log("Çığ partikülü bitti! Metot tetikleniyor...");
        avalancheVoice.Stop();

        sceneFinish.Play();
    }
}
