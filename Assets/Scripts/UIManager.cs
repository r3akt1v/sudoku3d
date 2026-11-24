using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Builds all UI at runtime: level select, HUD, pause menu, win screen.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private Canvas canvas;
    private Font font;
    private GameObject levelPanel, hudPanel, pausePanel, winPanel;
    private Text hudInfoText, hintButtonText;
    private Button hintButton;
    private Slider musicSlider, sfxSlider;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildCanvas();
        BuildLevelSelect();
        BuildHUD();
        BuildPauseMenu();
        BuildWinPanel();

        ShowLevelSelect();
    }

    // ---------- helpers ----------

    private void BuildCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private GameObject MakePanel(string name, Transform parent, Color bg, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = bg;
        return go;
    }

    private Text MakeText(string name, Transform parent, string content, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bg * 1.25f;
        colors.pressedColor = bg * 0.8f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var t = MakeText("Label", go.transform, label, 24, TextAnchor.MiddleCenter, Color.white);
        return btn;
    }

    private Slider MakeSlider(string name, Transform parent, Vector2 pos, float value, UnityEngine.Events.UnityAction<float> onChange)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(260, 30);

        var bg = MakePanel("BG", go.transform, new Color(0.12f, 0.12f, 0.14f, 0.9f),
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 10));
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = new Vector2(0, 0.5f);
        bgRt.anchorMax = new Vector2(1, 0.5f);
        bgRt.sizeDelta = new Vector2(0, 10);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = new Vector2(0, 0.5f);
        faRt.anchorMax = new Vector2(1, 0.5f);
        faRt.offsetMin = new Vector2(5, -5);
        faRt.offsetMax = new Vector2(-25, 5);
        var fill = MakePanel("Fill", fillArea.transform, new Color(0.45f, 0.75f, 0.65f, 0.95f),
            Vector2.zero, Vector2.one, Vector2.zero);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = (RectTransform)handleArea.transform;
        haRt.anchorMin = new Vector2(0, 0.5f);
        haRt.anchorMax = new Vector2(1, 0.5f);
        haRt.offsetMin = new Vector2(10, 0);
        haRt.offsetMax = new Vector2(-10, 0);
        var handle = MakePanel("Handle", handleArea.transform, new Color(0.9f, 0.9f, 0.88f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18, 24));

        var slider = go.AddComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    // ---------- panels ----------

    private void BuildLevelSelect()
    {
        levelPanel = MakePanel("LevelSelect", canvas.transform,
            new Color(0f, 0f, 0f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero);
        var title = MakeText("Title", levelPanel.transform, "3D СУДОКУ", 52, TextAnchor.MiddleCenter, new Color(0.92f, 0.88f, 0.80f));
        var tRt = (RectTransform)title.transform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.62f);
        tRt.sizeDelta = new Vector2(600, 70);

        var sub = MakeText("Sub", levelPanel.transform, "Выбери размер поля", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.68f, 0.62f));
        var sRt = (RectTransform)sub.transform;
        sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.52f);
        sRt.sizeDelta = new Vector2(600, 40);

        MakeButton("Btn3", levelPanel.transform, "3 × 3   —   Лёгкий", new Vector2(0, -60), new Vector2(340, 64),
            new Color(0.28f, 0.42f, 0.38f), () => GameManager.Instance.SelectLevel(3));
        MakeButton("Btn6", levelPanel.transform, "6 × 6   —   Средний", new Vector2(0, -140), new Vector2(340, 64),
            new Color(0.38f, 0.36f, 0.28f), () => GameManager.Instance.SelectLevel(6));
        MakeButton("Btn9", levelPanel.transform, "9 × 9   —   Сложный", new Vector2(0, -220), new Vector2(340, 64),
            new Color(0.40f, 0.30f, 0.26f), () => GameManager.Instance.SelectLevel(9));
    }

    private void BuildHUD()
    {
        hudPanel = MakePanel("HUD", canvas.transform, new Color(0, 0, 0, 0),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 70));
        var hudRt = (RectTransform)hudPanel.transform;
        hudRt.offsetMin = new Vector2(0, -70);
        hudRt.offsetMax = new Vector2(0, 0);
        hudPanel.GetComponent<Image>().raycastTarget = false;

        hudInfoText = MakeText("Info", hudPanel.transform, "", 24, TextAnchor.MiddleLeft, new Color(0.9f, 0.87f, 0.8f));
        var iRt = (RectTransform)hudInfoText.transform;
        iRt.anchorMin = new Vector2(0, 0);
        iRt.anchorMax = new Vector2(0.6f, 1);
        iRt.offsetMin = new Vector2(20, 0);
        iRt.offsetMax = Vector2.zero;

        hintButton = MakeButton("HintBtn", hudPanel.transform, "Подсказка (3)", new Vector2(0, 0), new Vector2(190, 48),
            new Color(0.30f, 0.38f, 0.34f), () => GameManager.Instance.UseHint());
        var hbRt = (RectTransform)hintButton.transform;
        hbRt.anchorMin = hbRt.anchorMax = new Vector2(1, 0.5f);
        hbRt.anchoredPosition = new Vector2(-265, 0);
        hintButtonText = hintButton.GetComponentInChildren<Text>();

        var pauseBtn = MakeButton("PauseBtn", hudPanel.transform, "Пауза", new Vector2(0, 0), new Vector2(140, 48),
            new Color(0.30f, 0.30f, 0.34f), () => GameManager.Instance.TogglePause());
        var pbRt = (RectTransform)pauseBtn.transform;
        pbRt.anchorMin = pbRt.anchorMax = new Vector2(1, 0.5f);
        pbRt.anchoredPosition = new Vector2(-80, 0);
    }

    private void BuildPauseMenu()
    {
        pausePanel = MakePanel("PauseMenu", canvas.transform,
            new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero);

        var box = MakePanel("Box", pausePanel.transform, new Color(0.13f, 0.14f, 0.16f, 0.97f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460, 430));

        var title = MakeText("Title", box.transform, "ПАУЗА", 36, TextAnchor.MiddleCenter, new Color(0.92f, 0.88f, 0.80f));
        var tRt = (RectTransform)title.transform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.86f);
        tRt.sizeDelta = new Vector2(400, 50);

        MakeButton("Resume", box.transform, "Продолжить", new Vector2(0, 110), new Vector2(280, 52),
            new Color(0.30f, 0.44f, 0.40f), () => GameManager.Instance.TogglePause());
        MakeButton("Restart", box.transform, "Рестарт уровня", new Vector2(0, 40), new Vector2(280, 52),
            new Color(0.38f, 0.36f, 0.28f), () => GameManager.Instance.RestartLevel());
        MakeButton("Menu", box.transform, "В меню", new Vector2(0, -30), new Vector2(280, 52),
            new Color(0.36f, 0.30f, 0.28f), () => GameManager.Instance.QuitToMenu());

        var musLabel = MakeText("MusLabel", box.transform, "Музыка", 20, TextAnchor.MiddleLeft, new Color(0.75f, 0.72f, 0.66f));
        var mlRt = (RectTransform)musLabel.transform;
        mlRt.anchorMin = mlRt.anchorMax = new Vector2(0.5f, 0.5f);
        mlRt.anchoredPosition = new Vector2(-95, -95);
        mlRt.sizeDelta = new Vector2(80, 30);
        float mv = AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0.45f;
        float sv = AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.85f;
        musicSlider = MakeSlider("MusicSlider", box.transform, new Vector2(45, -95), mv,
            v => AudioManager.Instance.SetMusicVolume(v));

        var sfxLabel = MakeText("SfxLabel", box.transform, "Звуки", 20, TextAnchor.MiddleLeft, new Color(0.75f, 0.72f, 0.66f));
        var slRt = (RectTransform)sfxLabel.transform;
        slRt.anchorMin = slRt.anchorMax = new Vector2(0.5f, 0.5f);
        slRt.anchoredPosition = new Vector2(-95, -145);
        slRt.sizeDelta = new Vector2(80, 30);
        sfxSlider = MakeSlider("SfxSlider", box.transform, new Vector2(45, -145), sv,
            v => AudioManager.Instance.SetSfxVolume(v));
    }

    private void BuildWinPanel()
    {
        winPanel = MakePanel("WinPanel", canvas.transform,
            new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero);

        var box = MakePanel("Box", winPanel.transform, new Color(0.13f, 0.14f, 0.16f, 0.97f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420, 300));

        var title = MakeText("Title", box.transform, "СУДОКУ РЕШЕНО!", 36, TextAnchor.MiddleCenter, new Color(0.55f, 0.85f, 0.70f));
        var tRt = (RectTransform)title.transform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.78f);
        tRt.sizeDelta = new Vector2(400, 50);

        winMovesText = MakeText("Moves", box.transform, "", 24, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.75f));
        var mRt = (RectTransform)winMovesText.transform;
        mRt.anchorMin = mRt.anchorMax = new Vector2(0.5f, 0.58f);
        mRt.sizeDelta = new Vector2(400, 40);

        MakeButton("Again", box.transform, "Играть снова", new Vector2(0, -60), new Vector2(240, 52),
            new Color(0.30f, 0.44f, 0.40f), () => GameManager.Instance.RestartLevel());
        MakeButton("Menu", box.transform, "В меню", new Vector2(0, -125), new Vector2(240, 52),
            new Color(0.36f, 0.30f, 0.28f), () => GameManager.Instance.QuitToMenu());
    }

    private Text winMovesText;

    // ---------- public API ----------

    public void ShowLevelSelect()
    {
        levelPanel.SetActive(true);
        hudPanel.SetActive(false);
        pausePanel.SetActive(false);
        winPanel.SetActive(false);
    }

    public void ShowHUD()
    {
        levelPanel.SetActive(false);
        hudPanel.SetActive(true);
        pausePanel.SetActive(false);
        winPanel.SetActive(false);
        RefreshHUD();
    }

    public void ShowPause(bool show)
    {
        pausePanel.SetActive(show);
        hudPanel.SetActive(!show);
    }

    public void ShowWin(int moves)
    {
        winMovesText.text = $"Ходов: {moves}";
        hudPanel.SetActive(false);
        winPanel.SetActive(true);
    }

    public void HideWin()
    {
        winPanel.SetActive(false);
        hudPanel.SetActive(true);
    }

    public void RefreshHUD()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        hudInfoText.text = $"{gm.LevelLabel}    Ходов: {gm.Moves}";
        hintButtonText.text = $"Подсказка ({gm.HintsLeft})";
        hintButton.interactable = gm.HintsLeft > 0 && !gm.IsGameOver;
    }
}
