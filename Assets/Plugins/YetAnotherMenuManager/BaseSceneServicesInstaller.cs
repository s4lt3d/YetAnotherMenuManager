using Core;
using Core.Interfaces;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class BaseSceneServicesInstaller : MonoBehaviour
{
    private string _sceneContextId;
    protected string SceneContextId => _sceneContextId;

    protected virtual void Awake()
    {
        var scene = gameObject.scene;
        _sceneContextId = $"Scene:{scene.handle}";
        InstallServices();
    }

    protected virtual void InstallServices()
    {
        // empty
    }

    protected void RegisterSceneService<T>(ref T service) where T : MonoBehaviour, IService
    {
        if (Services.Has<T>())
        {
            if (Services.TryGet<T>(out var existingService))
                service = existingService;
            return;
        }

        if (service == null)
            service = GetComponent<T>();

        if (service == null)
            service = GetComponentInChildren<T>(true);

        if (service == null)
            service = FindFirstObjectByType<T>(FindObjectsInactive.Include);

        if (service == null)
        {
            service = Services.AddComponent<T>(SceneContextId);
            return;
        }

        Services.TryAdd<T>(service, SceneContextId);
    }

    protected virtual void OnDestroy()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(_sceneContextId))
            return;

        Services.RemoveContext(_sceneContextId);
    }
}