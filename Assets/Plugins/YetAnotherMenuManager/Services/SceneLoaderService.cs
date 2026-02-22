using Core.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderService : MonoBehaviour, IService
{
    public void LoadScene(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogError("SceneLoader: scene name is empty.");
            return;
        }

        SceneManager.LoadScene(name);
    }

    public void InitializeService()
    {
        // empty
    }

    public void StartService()
    {
        // empty
    }

    public void CleanupService()
    {
        // empty
    }
}