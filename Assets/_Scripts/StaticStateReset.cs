using UnityEngine;

/// <summary>
/// Script'lerdeki statik durumlar sahne değişince kendiliğinden sıfırlanmaz: eğitim sahnesinde çığ geldikten
/// sonra sınav sahnesine geçilince "çığ yaklaştı" bilgisi true kalıyordu. Her sahne yüklendiğinde
/// (sahne objelerinin Start'ından önce) hepsi burada sıfırlanır.
/// </summary>
public static class StaticStateReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => ResetAll();
    }

    public static void ResetAll()
    {
        AvanlancheTrigger.isAvalancheNear = false;
        SafeAreaController.isPlayerInside = false;
        CharacterColliderTrigger.isTreeNear = false;
        ParticleController.IsAducationSceneFinished = false;
        HandsController.BothHandsAtHead = false;
    }
}
