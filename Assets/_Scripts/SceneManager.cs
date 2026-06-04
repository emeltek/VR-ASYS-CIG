using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    public bool isSceneStart = false;

    public void StartScene()
    {
        isSceneStart = true;
    }
    
    public void RestartScene()
    {
        // Başına tam yolunu yazarak çakışmayı önlüyoruz:
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
    }

    // ❌ UYGULAMADAN ÇIKMA METODU
    public void QuitGame()
    {
        Debug.Log("Uygulamadan çıkış yapılıyor...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}