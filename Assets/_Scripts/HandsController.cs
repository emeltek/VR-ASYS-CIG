using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HandsController : MonoBehaviour
{
    public SceneManager sceneManager;
    private bool leftHandInside = false;
    private bool rightHandInside = false;
    private bool bothHandsInside = false;
    public AudioSource audioSource;
    private bool isVoiceOver = false;
    public bool isExamScene = false;

    /// <summary>İki el de baş bölgesinde mi (korunma pozisyonu). GNN puanlayıcı çığ anında okur.</summary>
    public static bool BothHandsAtHead { get; internal set; }

    private void OnTriggerEnter(Collider other)
    {
        // Eller sığınak dışında da izlenir: kayaya eller zaten baştayken girilirse de sayılsın
        if (other.CompareTag("LeftHand")) leftHandInside = true;
        if (other.CompareTag("RightHand")) rightHandInside = true;
        BothHandsAtHead = leftHandInside && rightHandInside;
        CheckHands();
    }

    private void Update()
    {
        if (BothHandsAtHead && !isVoiceOver) CheckHands();
    }

    public void CheckHands()
    {
        if (leftHandInside && rightHandInside && ParticleController.IsAducationSceneFinished == false && isVoiceOver == false && sceneManager.isSceneStart == true && SafeAreaController.isPlayerInside == true)
        {
            // GNN puanlayıcı etkinse pozisyon, çığ anında puanlanır
            if (!GnnRiskScorer.IsScoring) ScoreManager.Instance.AddScore(30);
            if (isExamScene == false)
            {
                audioSource.Play();

            }
            isVoiceOver = true;
         bothHandsInside = true;   
         Debug.Log("[System] İki elide girdi");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("LeftHand"))
        {
            leftHandInside = false;
        }

        if (other.CompareTag("RightHand"))
        {
            rightHandInside = false;
        }
        BothHandsAtHead = leftHandInside && rightHandInside;
    }
}
