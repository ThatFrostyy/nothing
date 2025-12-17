using System;
using FF;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CursorFollowUI : MonoBehaviour
{
    [SerializeField] private bool hideOutsideGameplay = true;
    [SerializeField] private string fallbackGameplaySceneName = "Main";
    [SerializeField] private string fallbackMenuSceneName = "MainMenu";

    RectTransform rect;
    Graphic graphic;
    bool isVisible = true;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        graphic = GetComponent<Graphic>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplySceneVisibility(SceneManager.GetActiveScene());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Update()
    {
        if (!isVisible || Mouse.current == null) return;

        Vector2 pos = Mouse.current.position.ReadValue();
        rect.position = pos;   // no camera involvement ? no shake
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneVisibility(scene);
    }

    void ApplySceneVisibility(Scene scene)
    {
        bool shouldShow = !hideOutsideGameplay || IsGameplayScene(scene);
        SetVisible(shouldShow);
    }

    bool IsGameplayScene(Scene scene)
    {
        string gameplayName = SceneFlowController.Instance ? SceneFlowController.Instance.GameplaySceneName : fallbackGameplaySceneName;
        string menuName = SceneFlowController.Instance ? SceneFlowController.Instance.MainMenuSceneName : fallbackMenuSceneName;

        if (!string.IsNullOrEmpty(gameplayName) && scene.name.Equals(gameplayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(menuName) && scene.name.Equals(menuName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return scene.name.Equals(fallbackGameplaySceneName, StringComparison.OrdinalIgnoreCase);
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;
        if (graphic)
        {
            graphic.enabled = visible;
        }
    }

    public void Show()
    {
        SetVisible(true);
    }
}
