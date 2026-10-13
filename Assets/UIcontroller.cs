using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UIcontroller : MonoBehaviour
{
    const string TitleScreenPath = "TitleScreen";
    const string CoinsKey = "EcoRunnerCoinsV2";
    const string StartingCoinsGrantedKey = "EcoRunnerStartingCoinsGrantedV1";
    const string MaterialsKey = "EcoRunnerConstructionMaterialsV1";
    const string BestRoundTripDistanceKey = "EcoRunnerBestRoundTripDistanceV1";
    const string UpgradePrefix = "EcoRunnerUpgradeV2_";
    static readonly bool InfiniteResources = false;
    const float RocketDisplayedAltitudeMultiplier = 15f;
    static readonly int[] SevenLevelCosts = { 100, 300, 700, 1250, 2000, 3000, 5000 };
    static readonly int[] ResistanceCosts = { 500, 1000, 2000 };
    static readonly int[] ResourceBonusCosts = { 300, 700, 1500, 3000 };
    static readonly int[] ResourceBonusCoinSteps = { 0, 8, 6, 4, 2 };
    static readonly int[] ResourceBonusMaterialSteps = { 0, 4, 3, 2, 1 };
    static readonly int[] RocketCoinCosts = { 800, 1600, 2080, 3900 };
    static readonly int[] RocketMaterialCosts = { 6, 16, 21, 39 };
    static readonly string[] EndingMessages =
    {
        "Consegui escapar.",
        "La embaixo ficou o planeta que nos sustentou por toda a vida.",
        "Nos o devastamos. Consumimos suas florestas, seus rios, seu ar.",
        "Destruimos a nossa propria casa.",
        "E, em vez de aprender a conserta-la, escolhemos fugir.",
        "Agora procuramos outro mundo para chamar de futuro.",
        "Talvez para consumir tudo de novo. Talvez para repetir o fim.",
        "Pouso na Lua em algumas horas..."
    };
    const float EndingFadeDuration = 3f;
    const float EndingLineInterval = 3.1f;
    const float EndingReturnDelay = 4f;

    static bool startImmediatelyAfterReload;
    static bool showTitleScreen = true;

    Player player;
    RocketController rocket;
    Text distanceText;
    Camera mainCamera;
    Vector3 menuCameraPosition;
    float menuCameraSize;
    bool savedMenuCamera;
    bool deathPanelShown;
    bool showingRocketPanel;
    bool showingHowToPlay;
    bool showingQuitConfirm;
    bool escapeEndingStarted;
    bool escapeEndingResetDone;
    float escapeEndingStartTime;
    int finalDistance;
    int finalRoundTripDistance;
    int finalCoins;
    int finalMaterials;
    int totalCoins;
    int totalMaterials;
    int bestRoundTripDistance;
    Texture2D titleScreenTexture;
    UpgradeInfo[] upgrades;

    GUIStyle titleStyle;
    GUIStyle textStyle;
    GUIStyle smallTextStyle;
    GUIStyle upgradeNameStyle;
    GUIStyle upgradeLevelStyle;
    GUIStyle upgradeDescriptionStyle;
    GUIStyle resourceStyle;
    GUIStyle costStyle;
    GUIStyle hudDistanceStyle;
    GUIStyle hudLabelStyle;
    GUIStyle neonButtonStyle;
    GUIStyle rocketButtonStyle;
    GUIStyle iconStyle;
    GUIStyle panelStyle;
    GUIStyle upgradePanelStyle;
    GUIStyle buttonStyle;
    GUIStyle timerBackStyle;
    GUIStyle timerFillStyle;
    GUIStyle timerDangerFillStyle;
    GUIStyle endingStyle;
    Texture2D darkTexture;
    Texture2D panelTexture;
    Texture2D cardTexture;
    Texture2D purpleTexture;
    Texture2D purpleHoverTexture;
    Texture2D orangeTexture;
    Texture2D orangeHoverTexture;
    Texture2D lineTexture;

    private void Awake()
    {
        GameObject playerObject = GameObject.Find("Player");

        if (playerObject != null)
        {
            player = playerObject.GetComponent<Player>();
        }

        rocket = FindAnyObjectByType<RocketController>();

        if (rocket == null)
        {
            GameObject rocketControllerObject = new GameObject("RocketController");
            rocket = rocketControllerObject.AddComponent<RocketController>();
        }

        mainCamera = Camera.main;

        GameObject distanceObject = GameObject.Find("DistanceText");

        if (distanceObject != null)
        {
            distanceText = distanceObject.GetComponent<Text>();
        }

        GrantStartingCoinsOnce();
        totalCoins = PlayerPrefs.GetInt(CoinsKey, 0);
        totalMaterials = PlayerPrefs.GetInt(MaterialsKey, 0);
        bestRoundTripDistance = PlayerPrefs.GetInt(BestRoundTripDistanceKey, 0);
        titleScreenTexture = LoadTitleScreenTexture();
        CreateUpgradeList();
    }

    void Start()
    {
        if (startImmediatelyAfterReload)
        {
            startImmediatelyAfterReload = false;
            StartRun();
            return;
        }

        SetDistanceVisible(false);
    }

    void Update()
    {
        if (showTitleScreen)
        {
            if (Input.anyKeyDown)
            {
                showTitleScreen = false;
            }

            return;
        }

        if (player == null)
        {
            return;
        }

        if (rocket != null && rocket.HasEscapedPlanet)
        {
            UpdateEscapeEnding();
            return;
        }

        if (showingRocketPanel)
        {
            MoveCameraToRocket();
        }

        int distance = Mathf.FloorToInt(player.distance);

        if (player.isDead && !deathPanelShown)
        {
            finalDistance = Mathf.FloorToInt(player.maxRunDistance);
            finalRoundTripDistance = Mathf.FloorToInt(player.totalRunDistance);
            UpdateBestRoundTripDistance(finalRoundTripDistance);
            finalCoins = player.reachedBase ? CalculateRunCoins(finalRoundTripDistance) : 0;
            finalMaterials = player.reachedBase ? CalculateRunMaterials(finalDistance) : 0;
            StoreRunResources(finalCoins, finalMaterials);
            deathPanelShown = true;
            SetDistanceVisible(false);
        }
    }

    void OnGUI()
    {
        BuildStyles();

        if (showTitleScreen)
        {
            DrawTitleScreen();
            return;
        }

        if (rocket != null && rocket.HasEscapedPlanet)
        {
            BeginEscapeEnding();
            DrawEscapeEndingSequence();
            return;
        }

        if (player == null)
        {
            return;
        }

        if (!player.hasStarted && !player.isDead)
        {
            if (showingRocketPanel)
            {
                DrawRocketPanel();
            }
            else
            {
                DrawMenu();
            }

            return;
        }

        if (player.hasStarted && !player.isDead)
        {
            DrawRunHud();
        }

        if (player.isDead)
        {
            DrawDeathPanel();
        }
    }

    public void StartRun()
    {
        if (player == null)
        {
            return;
        }

        deathPanelShown = false;
        showTitleScreen = false;
        showingRocketPanel = false;
        RestoreMenuCamera();
        SetDistanceVisible(true);
        player.BeginRun();
    }

    public void RetryRun()
    {
        startImmediatelyAfterReload = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToShop()
    {
        startImmediatelyAfterReload = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void DrawTitleScreen()
    {
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);

        if (titleScreenTexture != null)
        {
            GUI.DrawTexture(screenRect, titleScreenTexture, ScaleMode.ScaleAndCrop);
        }
        else
        {
            GUI.color = Color.black;
            GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        GUI.Label(new Rect(0f, Screen.height - 86f, Screen.width, 34f), "Pressione qualquer botao", textStyle);
    }

    void DrawMenu()
    {
        float width = Mathf.Min(920f, Screen.width - 24f);
        float height = Mathf.Min(690f, Screen.height - 24f);
        Rect panelRect = GetCenteredRect(width, height);

        DrawNeonPanel(panelRect, new Color(0.7f, 0.18f, 1f, 0.85f));
        DrawCornerCaps(panelRect);

        GUI.Label(new Rect(panelRect.x, panelRect.y + 14f, panelRect.width, 58f), "Eco Runner", titleStyle);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 72f, panelRect.width, 24f), "- Loja de melhorias -", textStyle);

        if (GUI.Button(new Rect(panelRect.x + 34f, panelRect.y + 28f, 118f, 32f), "Sair", neonButtonStyle))
        {
            showingQuitConfirm = true;
        }

        Rect resourcesRect = new Rect(panelRect.x + panelRect.width - 210f, panelRect.y + 22f, 180f, 76f);
        DrawInsetPanel(resourcesRect, new Color(0.7f, 0.18f, 1f, 0.45f));
        GUI.Label(new Rect(resourcesRect.x + 16f, resourcesRect.y + 10f, resourcesRect.width - 24f, 24f), "$  Moedas: " + GetCoinsText(), resourceStyle);
        GUI.Label(new Rect(resourcesRect.x + 16f, resourcesRect.y + 40f, resourcesRect.width - 24f, 24f), "*  Materiais: " + GetMaterialsText(), resourceStyle);

        GUI.Label(new Rect(panelRect.x + 34f, panelRect.y + 104f, 300f, 28f), "Recorde ida e volta: " + bestRoundTripDistance + " m", resourceStyle);

        if (GUI.Button(new Rect(panelRect.xMax - 210f, panelRect.y + 104f, 180f, 32f), "Como jogar", neonButtonStyle))
        {
            showingHowToPlay = true;
        }

        DrawUpgradeList(new Rect(panelRect.x + 34f, panelRect.y + 146f, panelRect.width - 68f, panelRect.height - 258f));

        Rect runRect = new Rect(panelRect.x + 56f, panelRect.yMax - 90f, 350f, 58f);
        Rect rocketRect = new Rect(panelRect.xMax - 406f, panelRect.yMax - 90f, 350f, 58f);

        if (GUI.Button(runRect, ">>  Correr", neonButtonStyle))
        {
            StartRun();
        }

        if (GUI.Button(rocketRect, "/\\  Construir foguete", rocketButtonStyle))
        {
            showingRocketPanel = true;
            rocket.ResetRocketForView();
            MoveCameraToRocket();
        }

        if (showingHowToPlay)
        {
            DrawHowToPlayPanel(panelRect);
        }

        if (showingQuitConfirm)
        {
            DrawQuitConfirmPanel();
        }
    }

    void DrawHowToPlayPanel(Rect menuRect)
    {
        Rect panelRect = GetCenteredRect(Mathf.Min(680f, Screen.width - 36f), 470f);
        DrawNeonPanel(panelRect, new Color(0.7f, 0.18f, 1f, 0.95f));
        DrawCornerCaps(panelRect);

        GUI.Label(new Rect(panelRect.x, panelRect.y + 24f, panelRect.width, 48f), "Como jogar", titleStyle);

        string text =
            "Objetivo: construir o foguete e escapar do planeta.\n\n" +
            "Para zerar, voce precisa completar o foguete e acertar pelo menos 3 propulsoes Perfeitas e 1 Otima no lancamento.\n\n" +
            "Para conseguir recursos, corra o mais longe que puder e volte para a van. Se nao voltar, os recursos da corrida sao perdidos.\n\n" +
            "Todos os recursos sao adquiridos de acordo com a distancia alcancada. Materiais de melhoria so comecam a aparecer depois dos 800m corridos.";

        GUI.Label(new Rect(panelRect.x + 48f, panelRect.y + 88f, panelRect.width - 96f, 172f), text, upgradeDescriptionStyle);

        Rect commandsRect = new Rect(panelRect.x + 56f, panelRect.y + 278f, panelRect.width - 112f, 92f);
        DrawInsetPanel(commandsRect, new Color(0.7f, 0.18f, 1f, 0.45f));
        GUI.Label(new Rect(commandsRect.x + 18f, commandsRect.y + 10f, commandsRect.width - 36f, 20f), "Comandos", resourceStyle);
        GUI.Label(new Rect(commandsRect.x + 18f, commandsRect.y + 36f, 160f, 20f), "A / D: direcao", upgradeDescriptionStyle);
        GUI.Label(new Rect(commandsRect.x + 196f, commandsRect.y + 36f, 160f, 20f), "Shift: dash", upgradeDescriptionStyle);
        GUI.Label(new Rect(commandsRect.x + 360f, commandsRect.y + 36f, 190f, 20f), "Space: pular", upgradeDescriptionStyle);
        GUI.Label(new Rect(commandsRect.x + 18f, commandsRect.y + 62f, commandsRect.width - 36f, 20f), "Space tambem ativa a propulsao do foguete no minigame.", upgradeDescriptionStyle);

        if (GUI.Button(new Rect(panelRect.center.x - 90f, panelRect.yMax - 68f, 180f, 42f), "Entendi", neonButtonStyle))
        {
            showingHowToPlay = false;
        }
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DrawQuitConfirmPanel()
    {
        Rect panelRect = GetCenteredRect(Mathf.Min(460f, Screen.width - 36f), 230f);
        DrawNeonPanel(panelRect, new Color(0.7f, 0.18f, 1f, 0.95f));

        GUI.Label(new Rect(panelRect.x, panelRect.y + 30f, panelRect.width, 42f), "Fechar jogo?", textStyle);
        GUI.Label(new Rect(panelRect.x + 42f, panelRect.y + 82f, panelRect.width - 84f, 34f), "Tem certeza que deseja sair?", upgradeDescriptionStyle);

        if (GUI.Button(new Rect(panelRect.x + 48f, panelRect.y + 148f, 150f, 42f), "Cancelar", neonButtonStyle))
        {
            showingQuitConfirm = false;
        }

        if (GUI.Button(new Rect(panelRect.xMax - 198f, panelRect.y + 148f, 150f, 42f), "Sair", rocketButtonStyle))
        {
            QuitGame();
        }
    }

    void DrawRocketPanel()
    {
        Rect panelRect = new Rect(Screen.width - 380f, Screen.height - 342f, 342f, 292f);
        DrawNeonPanel(panelRect, new Color(1f, 0.5f, 0.08f, 0.85f));

        if (rocket == null)
        {
            GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 30f, panelRect.width - 48f, 60f), "Controlador do foguete nao encontrado.", textStyle);
        }
        else
        {
            int displayedAltitude = Mathf.FloorToInt(rocket.CurrentAltitude * RocketDisplayedAltitudeMultiplier);
            GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 18f, panelRect.width - 48f, 28f), "Altura: " + displayedAltitude + " m", resourceStyle);
            DrawRocketCostInfo(new Rect(panelRect.x + 24f, panelRect.y + 50f, panelRect.width - 48f, 30f));

            Rect buildRect = new Rect(panelRect.x + 34f, panelRect.y + 88f, panelRect.width - 68f, 52f);
            Rect launchRect = new Rect(panelRect.x + 34f, panelRect.y + 152f, panelRect.width - 68f, 52f);
            Rect backRect = new Rect(panelRect.x + 34f, panelRect.y + 216f, panelRect.width - 68f, 52f);

            GUI.enabled = rocket.CanBuild && !rocket.isLaunching && CanAffordNextRocketPart();

            if (GUI.Button(buildRect, "/\\  Construir foguete", rocketButtonStyle))
            {
                TryBuildRocket();
            }

            GUI.enabled = rocket.CanLaunch;

            if (GUI.Button(launchRect, ">>  Lancamento", neonButtonStyle))
            {
                rocket.StartLaunch();
            }

            GUI.enabled = true;

            if (rocket.isLaunching)
            {
                DrawLaunchMinigame(new Rect(0f, 0f, Screen.width, Screen.height));
            }

            if (rocket.launchResolved)
            {
                GUI.Label(new Rect(panelRect.x + 24f, panelRect.y - 40f, panelRect.width - 48f, 30f), rocket.lastLaunchRating + " | Forca: " + rocket.lastLaunchPower.ToString("0.00") + "x", resourceStyle);
            }

            if (GUI.Button(backRect, "<  Voltar", neonButtonStyle))
            {
                if (rocket.isLaunching)
                {
                    rocket.CancelLaunch();
                }

                showingRocketPanel = false;
                rocket.ResetRocketForView();
                RestoreMenuCamera();
            }
        }
    }

    void DrawRocketCostInfo(Rect rect)
    {
        if (rocket == null || rocket.RocketLevel >= RocketCoinCosts.Length)
        {
            GUI.Label(rect, "Foguete completo", costStyle);
            return;
        }

        int index = rocket.RocketLevel;
        string text = "Custo: " + RocketCoinCosts[index] + " moedas | " + RocketMaterialCosts[index] + " materiais";
        GUI.Label(rect, text, costStyle);
    }

    void TryBuildRocket()
    {
        if (rocket == null || !rocket.CanBuild)
        {
            return;
        }

        int index = rocket.RocketLevel;

        if (index < 0 || index >= RocketCoinCosts.Length)
        {
            return;
        }

        int coinCost = RocketCoinCosts[index];
        int materialCost = RocketMaterialCosts[index];

        if (!InfiniteResources && (totalCoins < coinCost || totalMaterials < materialCost))
        {
            return;
        }

        if (!InfiniteResources)
        {
            totalCoins -= coinCost;
            totalMaterials -= materialCost;
        }

        PlayerPrefs.SetInt(CoinsKey, totalCoins);
        PlayerPrefs.SetInt(MaterialsKey, totalMaterials);
        rocket.BuildNextPart();
        PlayerPrefs.Save();
    }

    bool CanAffordNextRocketPart()
    {
        if (rocket == null)
        {
            return false;
        }

        int index = rocket.RocketLevel;

        if (index < 0 || index >= RocketCoinCosts.Length)
        {
            return false;
        }

        return InfiniteResources || (totalCoins >= RocketCoinCosts[index] && totalMaterials >= RocketMaterialCosts[index]);
    }

    void DrawEscapeEnding()
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string message = "consegui escapar desse planeta, o planeta que nós devastamos... destruimos nossa propria casa, e ao inves de tentar concertala, estamos fugindo, procurando outro planeta para consumir tudo e destruir no final... Pouso na lua em algumas horas...";
        GUI.Label(GetCenteredRect(Mathf.Min(900f, Screen.width - 80f), 260f), message, endingStyle);
    }

    void DrawEscapeEndingSequence()
    {
        float elapsed = Time.time - escapeEndingStartTime;
        float fade = Mathf.Clamp01(elapsed / EndingFadeDuration);
        int visibleLines = GetVisibleEndingLines(elapsed);

        GUI.color = new Color(0f, 0f, 0f, fade);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (fade < 1f || visibleLines <= 0)
        {
            return;
        }

        string message = "";

        for (int i = 0; i < visibleLines; i++)
        {
            if (i > 0)
            {
                message += "\n\n";
            }

            message += EndingMessages[i];
        }

        GUI.Label(GetCenteredRect(Mathf.Min(980f, Screen.width - 80f), 520f), message, endingStyle);
    }

    void BeginEscapeEnding()
    {
        if (escapeEndingStarted)
        {
            return;
        }

        escapeEndingStarted = true;
        escapeEndingStartTime = Time.time;
        showingRocketPanel = false;
        SetDistanceVisible(false);
    }

    void UpdateEscapeEnding()
    {
        BeginEscapeEnding();

        float elapsed = Time.time - escapeEndingStartTime;
        float fullMessageTime = EndingFadeDuration + (EndingMessages.Length * EndingLineInterval);

        if (elapsed < fullMessageTime + EndingReturnDelay || escapeEndingResetDone)
        {
            return;
        }

        escapeEndingResetDone = true;
        ResetAllProgress();
        startImmediatelyAfterReload = false;
        showTitleScreen = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    int GetVisibleEndingLines(float elapsed)
    {
        float textElapsed = elapsed - EndingFadeDuration;

        if (textElapsed < 0f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.FloorToInt(textElapsed / EndingLineInterval) + 1, 0, EndingMessages.Length);
    }

    void ResetAllProgress()
    {
        PlayerPrefs.SetInt(CoinsKey, 0);
        PlayerPrefs.SetInt(MaterialsKey, 0);
        PlayerPrefs.SetInt(StartingCoinsGrantedKey, 0);

        for (int i = 0; i < upgrades.Length; i++)
        {
            PlayerPrefs.SetInt(upgrades[i].GetPrefsKey(), 0);
        }

        if (rocket != null)
        {
            rocket.ResetSavedRocketLevelForTesting();
        }

        totalCoins = 0;
        totalMaterials = 0;
        GrantStartingCoinsOnce();
        totalCoins = PlayerPrefs.GetInt(CoinsKey, 0);
        CreateUpgradeList();
        PlayerPrefs.Save();
    }

    void MoveCameraToRocket()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        if (!savedMenuCamera)
        {
            menuCameraPosition = mainCamera.transform.position;
            menuCameraSize = mainCamera.orthographicSize;
            savedMenuCamera = true;
        }

        Transform target = rocket != null ? rocket.CameraTarget : null;

        if (target == null)
        {
            target = FindSceneTransform("Foguet");
        }

        if (target == null)
        {
            target = FindSceneTransform("foguete");
        }

        if (target == null)
        {
            target = FindSceneTransform("pitDecolagem");
        }

        if (target == null)
        {
            target = FindSceneTransform("pit decolagem");
        }

        if (target == null)
        {
            return;
        }

        Vector3 cameraPosition = mainCamera.transform.position;
        cameraPosition.x = target.position.x;
        cameraPosition.y = target.position.y;
        mainCamera.transform.position = cameraPosition;

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = 38f;
        }
    }

    void RestoreMenuCamera()
    {
        if (!savedMenuCamera)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        mainCamera.transform.position = menuCameraPosition;

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = menuCameraSize;
        }
    }

    Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

        for (int i = 0; i < transforms.Length; i++)
        {
            if (NamesMatch(transforms[i].name, objectName) && transforms[i].gameObject.scene.IsValid())
            {
                return transforms[i];
            }
        }

        return null;
    }

    bool NamesMatch(string sceneName, string expectedName)
    {
        return NormalizeName(sceneName) == NormalizeName(expectedName);
    }

    string NormalizeName(string value)
    {
        return value.Replace(" ", "").ToLowerInvariant();
    }

    string GetRocketDescription()
    {
        if (rocket == null || rocket.RocketLevel <= 0)
        {
            return "Nenhuma parte construida. O pit fica vazio ate voce iniciar a construcao.";
        }

        if (rocket.RocketLevel >= 4)
        {
            return "Foguete completo. Cada parte adiciona propulsao ao lancamento.";
        }

        return "Cada melhoria libera mais uma parte visual e adiciona propulsao ao foguete.";
    }

    void DrawLaunchMinigame(Rect area)
    {
        if (rocket == null)
        {
            return;
        }

        Vector2 center = GetRocketMinigameCenter(area);
        GUI.Label(new Rect(center.x - 260f, center.y - rocket.maxOuterCircle - 42f, 520f, 26f), rocket.isLaunching ? "Aperte Space quando os circulos alinharem" : "O lancamento usa Space para definir a propulsao.", textStyle);

        DrawCircle(center, rocket.TargetRadius, new Color(0.2f, 0.85f, 0.45f, 1f), 4f);

        if (rocket.isLaunching)
        {
            DrawCircle(center, rocket.CurrentOuterRadius, new Color(1f, 1f, 1f, 1f), 4f);
        }
        else
        {
            DrawCircle(center, rocket.maxOuterCircle, new Color(1f, 1f, 1f, 0.35f), 3f);
        }
    }

    Vector2 GetRocketMinigameCenter(Rect area)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Transform target = rocket != null ? rocket.CameraTarget : null;

        if (mainCamera == null || target == null)
        {
            return area.center;
        }

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(target.position);
        return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
    }

    void DrawCircle(Vector2 center, float radius, Color color, float thickness)
    {
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        Texture2D texture = Texture2D.whiteTexture;
        int segments = 80;

        GUI.color = color;

        for (int i = 0; i < segments; i++)
        {
            float angleA = (Mathf.PI * 2f * i) / segments;
            float angleB = (Mathf.PI * 2f * (i + 1)) / segments;
            Vector2 a = center + new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA)) * radius;
            Vector2 b = center + new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB)) * radius;
            DrawLine(a, b, texture, thickness);
        }

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    void DrawLine(Vector2 from, Vector2 to, Texture2D texture, float thickness)
    {
        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 previousMatrix = GUI.matrix;

        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - (thickness * 0.5f), delta.magnitude, thickness), texture);
        GUI.matrix = previousMatrix;
    }

    void DrawUpgradeList(Rect area)
    {
        float cardHeight = 55f;
        float gap = 8f;

        for (int i = 0; i < upgrades.Length; i++)
        {
            Rect cardRect = new Rect(
                area.x,
                area.y + (cardHeight + gap) * i,
                area.width,
                cardHeight
            );

            DrawUpgradeCard(cardRect, i);
        }
    }

    void DrawUpgradeCard(Rect rect, int index)
    {
        UpgradeInfo upgrade = upgrades[index];
        int nextCost = upgrade.GetNextCost();
        bool isMaxed = upgrade.currentLevel >= upgrade.maxLevel;
        bool canBuy = !isMaxed && (InfiniteResources || totalCoins >= nextCost);

        DrawInsetPanel(rect, new Color(0.7f, 0.18f, 1f, 0.38f));

        Rect iconRect = new Rect(rect.x + 10f, rect.y + 7f, 42f, 42f);
        DrawInsetPanel(iconRect, new Color(0.86f, 0.25f, 1f, 0.65f));
        GUI.Label(iconRect, GetUpgradeIcon(upgrade.id), iconStyle);

        DrawVerticalLine(rect.x + 66f, rect.y + 8f, rect.height - 16f, new Color(0.8f, 0.35f, 1f, 0.35f));
        DrawVerticalLine(rect.x + rect.width - 292f, rect.y + 8f, rect.height - 16f, new Color(0.8f, 0.35f, 1f, 0.35f));
        DrawVerticalLine(rect.x + rect.width - 140f, rect.y + 8f, rect.height - 16f, new Color(0.8f, 0.35f, 1f, 0.35f));

        GUI.Label(new Rect(rect.x + 78f, rect.y + 6f, 170f, 24f), upgrade.name, upgradeNameStyle);
        GUI.Label(new Rect(rect.x + 78f, rect.y + 29f, 170f, 18f), "Nivel " + upgrade.currentLevel + "/" + upgrade.maxLevel, upgradeLevelStyle);
        GUI.Label(new Rect(rect.x + 270f, rect.y + 8f, rect.width - 580f, 40f), upgrade.description, upgradeDescriptionStyle);
        GUI.Label(new Rect(rect.x + rect.width - 274f, rect.y + 16f, 126f, 24f), isMaxed ? "Maximo" : "Custo: " + nextCost, costStyle);

        GUI.enabled = canBuy;

        if (GUI.Button(new Rect(rect.x + rect.width - 126f, rect.y + 10f, 108f, 34f), isMaxed ? "Comprado" : "Comprar", neonButtonStyle))
        {
            BuyUpgrade(index);
        }

        GUI.enabled = true;
    }

    void DrawDeathPanel()
    {
        Rect panelRect = GetCenteredRect(Mathf.Min(680f, Screen.width - 28f), 430f);
        DrawNeonPanel(panelRect, new Color(0.7f, 0.18f, 1f, 0.85f));
        DrawCornerCaps(panelRect);

        string endTitle = player.reachedBase ? "Voltou a base" : "Fim da corrida";
        GUI.Label(new Rect(panelRect.x, panelRect.y + 28f, panelRect.width, 66f), endTitle, titleStyle);

        Rect statsRect = new Rect(panelRect.x + 64f, panelRect.y + 112f, panelRect.width - 128f, 172f);
        DrawInsetPanel(statsRect, new Color(0.7f, 0.18f, 1f, 0.38f));
        GUI.Label(new Rect(statsRect.x + 28f, statsRect.y + 18f, statsRect.width - 56f, 28f), "Maior distancia: " + finalDistance + " m", textStyle);
        GUI.Label(new Rect(statsRect.x + 28f, statsRect.y + 54f, statsRect.width - 56f, 28f), "Ida e volta: " + finalRoundTripDistance + " m", textStyle);
        GUI.Label(new Rect(statsRect.x + 28f, statsRect.y + 90f, statsRect.width - 56f, 28f), "Moedas ganhas: " + finalCoins, textStyle);
        GUI.Label(new Rect(statsRect.x + 28f, statsRect.y + 126f, statsRect.width - 56f, 28f), "Materiais ganhos: " + finalMaterials, textStyle);

        GUI.Label(new Rect(panelRect.x + 46f, panelRect.y + 290f, panelRect.width - 92f, 22f), "Recorde ida e volta: " + bestRoundTripDistance + " m", upgradeDescriptionStyle);
        GUI.Label(new Rect(panelRect.x + 46f, panelRect.y + 312f, panelRect.width - 92f, 22f), player.reachedBase ? "Recursos armazenados na base." : "Recursos perdidos fora da base.", upgradeDescriptionStyle);

        if (GUI.Button(new Rect(panelRect.x + 58f, panelRect.y + 344f, 230f, 54f), "<  Loja", neonButtonStyle))
        {
            ReturnToShop();
        }

        if (GUI.Button(new Rect(panelRect.xMax - 288f, panelRect.y + 344f, 230f, 54f), ">>  Correr novamente", neonButtonStyle))
        {
            RetryRun();
        }
    }

    void DrawRunHud()
    {
        Rect panelRect = new Rect(22f, 22f, 330f, 94f);
        DrawInsetPanel(panelRect, new Color(0.7f, 0.18f, 1f, 0.48f));

        int distance = Mathf.FloorToInt(player.distance);
        GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 10f, panelRect.width - 36f, 30f), distance + " m", hudDistanceStyle);

        Rect backRect = new Rect(panelRect.x + 18f, panelRect.y + 52f, panelRect.width - 36f, 16f);
        Rect fillRect = new Rect(backRect.x + 2f, backRect.y + 2f, (backRect.width - 4f) * player.ToxicTimer01, backRect.height - 4f);

        DrawBorder(backRect, new Color(0.95f, 0.35f, 1f, 0.75f), 1f);
        GUI.Box(new Rect(backRect.x + 1f, backRect.y + 1f, backRect.width - 2f, backRect.height - 2f), GUIContent.none, timerBackStyle);
        GUI.Box(fillRect, GUIContent.none, player.isToxicSlowingDown ? timerDangerFillStyle : timerFillStyle);

        GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 68f, panelRect.width - 36f, 20f), "CORROSAO", hudLabelStyle);
    }

    void StoreRunResources(int coins, int materials)
    {
        if (coins <= 0 && materials <= 0)
        {
            return;
        }

        totalCoins += Mathf.Max(0, coins);
        totalMaterials += Mathf.Max(0, materials);
        PlayerPrefs.SetInt(CoinsKey, totalCoins);
        PlayerPrefs.SetInt(MaterialsKey, totalMaterials);
        PlayerPrefs.Save();
    }

    void GrantStartingCoinsOnce()
    {
        if (PlayerPrefs.GetInt(StartingCoinsGrantedKey, 0) == 1)
        {
            return;
        }

        if (PlayerPrefs.GetInt(CoinsKey, 0) <= 0)
        {
            PlayerPrefs.SetInt(CoinsKey, 400);
        }

        PlayerPrefs.SetInt(StartingCoinsGrantedKey, 1);
        PlayerPrefs.Save();
    }

    void UpdateBestRoundTripDistance(int distance)
    {
        if (distance <= bestRoundTripDistance)
        {
            return;
        }

        bestRoundTripDistance = distance;
        PlayerPrefs.SetInt(BestRoundTripDistanceKey, bestRoundTripDistance);
        PlayerPrefs.Save();
    }

    int CalculateRunCoins(int distance)
    {
        int baseCoins = CountProgressiveRewards(distance, 10f, 0.92f, 5f);
        return baseCoins + CalculateResourceBonus(baseCoins, GetResourceBonusLevel(), ResourceBonusCoinSteps);
    }

    int CalculateRunMaterials(int distance)
    {
        int baseMaterials = CountProgressiveRewards(distance, 800f, 0.875f, 450f);
        return baseMaterials + CalculateResourceBonus(baseMaterials, GetResourceBonusLevel(), ResourceBonusMaterialSteps);
    }

    int CountProgressiveRewards(int distance, float firstInterval, float intervalMultiplier, float minimumInterval)
    {
        if (distance <= 0)
        {
            return 0;
        }

        int rewards = 0;
        float nextRewardDistance = firstInterval;
        float interval = firstInterval;

        while (distance >= nextRewardDistance)
        {
            rewards++;
            interval = Mathf.Max(minimumInterval, interval * intervalMultiplier);
            nextRewardDistance += interval;
        }

        return rewards;
    }

    int GetResourceBonusLevel()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(UpgradePrefix + "ResourceBonus", 0), 0, 4);
    }

    int CalculateResourceBonus(int amount, int level, int[] steps)
    {
        if (amount <= 0 || level <= 0 || steps == null || level >= steps.Length)
        {
            return 0;
        }

        int step = Mathf.Max(1, steps[level]);
        return amount / step;
    }

    void BuyUpgrade(int index)
    {
        UpgradeInfo upgrade = upgrades[index];

        if (upgrade.currentLevel >= upgrade.maxLevel)
        {
            return;
        }

        int cost = upgrade.GetNextCost();

        if (!InfiniteResources && totalCoins < cost)
        {
            return;
        }

        if (!InfiniteResources)
        {
            totalCoins -= cost;
        }

        upgrade.currentLevel++;
        upgrades[index] = upgrade;

        PlayerPrefs.SetInt(CoinsKey, totalCoins);
        PlayerPrefs.SetInt(upgrade.GetPrefsKey(), upgrade.currentLevel);
        PlayerPrefs.Save();

        if (player != null)
        {
            player.ApplySavedUpgrades();
        }
    }

    Rect GetCenteredRect(float width, float height)
    {
        return new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );
    }

    void DrawNeonPanel(Rect rect, Color glowColor)
    {
        GUI.DrawTexture(rect, panelTexture);
        DrawBorder(rect, glowColor, 3f);
        DrawBorder(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), new Color(0f, 0f, 0f, 0.75f), 2f);
        DrawBorder(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f), new Color(0.35f, 0.32f, 0.36f, 0.8f), 1f);
    }

    void DrawInsetPanel(Rect rect, Color borderColor)
    {
        GUI.DrawTexture(rect, cardTexture);
        DrawBorder(rect, new Color(0.02f, 0.018f, 0.024f, 1f), 2f);
        DrawBorder(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), borderColor, 1f);
        DrawHorizontalLine(rect.x + 6f, rect.y + 3f, rect.width - 12f, new Color(1f, 1f, 1f, 0.08f));
    }

    void DrawCornerCaps(Rect rect)
    {
        Color purple = new Color(0.9f, 0.28f, 1f, 0.85f);
        float length = 46f;
        float thickness = 4f;

        DrawRect(new Rect(rect.x + 14f, rect.y + 14f, length, thickness), purple);
        DrawRect(new Rect(rect.x + 14f, rect.y + 14f, thickness, length), purple);
        DrawRect(new Rect(rect.xMax - 14f - length, rect.y + 14f, length, thickness), purple);
        DrawRect(new Rect(rect.xMax - 18f, rect.y + 14f, thickness, length), purple);
        DrawRect(new Rect(rect.x + 14f, rect.yMax - 18f, length, thickness), purple);
        DrawRect(new Rect(rect.x + 14f, rect.yMax - 14f - length, thickness, length), purple);
        DrawRect(new Rect(rect.xMax - 14f - length, rect.yMax - 18f, length, thickness), purple);
        DrawRect(new Rect(rect.xMax - 18f, rect.yMax - 14f - length, thickness, length), purple);
    }

    void DrawBorder(Rect rect, Color color, float thickness)
    {
        DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    void DrawVerticalLine(float x, float y, float height, Color color)
    {
        DrawRect(new Rect(x, y, 1f, height), color);
    }

    void DrawHorizontalLine(float x, float y, float width, Color color)
    {
        DrawRect(new Rect(x, y, width, 1f), color);
    }

    void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, lineTexture);
        GUI.color = previousColor;
    }

    Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    Texture2D LoadTitleScreenTexture()
    {
        return Resources.Load<Texture2D>(TitleScreenPath);
    }

    string GetUpgradeIcon(string id)
    {
        switch (id)
        {
            case "Dash":
                return ">>";
            case "DoubleJump":
                return "^^";
            case "Speed":
                return "O>";
            case "Acceleration":
                return ">>>";
            case "Jump":
                return "^";
            case "Resistance":
                return "[]";
            case "ResourceBonus":
                return "$";
            default:
                return "*";
        }
    }

    void SetDistanceVisible(bool visible)
    {
        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(visible);
        }
    }

    void BuildStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        darkTexture = MakeTexture(new Color(0.03f, 0.025f, 0.035f, 0.94f));
        panelTexture = MakeTexture(new Color(0.055f, 0.045f, 0.06f, 0.92f));
        cardTexture = MakeTexture(new Color(0.08f, 0.075f, 0.09f, 0.93f));
        purpleTexture = MakeTexture(new Color(0.42f, 0.08f, 0.55f, 0.94f));
        purpleHoverTexture = MakeTexture(new Color(0.63f, 0.15f, 0.82f, 0.96f));
        orangeTexture = MakeTexture(new Color(0.62f, 0.26f, 0.03f, 0.96f));
        orangeHoverTexture = MakeTexture(new Color(0.88f, 0.42f, 0.08f, 0.98f));
        lineTexture = MakeTexture(Color.white);

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = 52;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1f, 0.66f, 1f, 1f);

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.fontSize = 18;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.wordWrap = true;
        textStyle.normal.textColor = new Color(0.88f, 0.58f, 1f, 1f);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 22;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        smallTextStyle = new GUIStyle(GUI.skin.label);
        smallTextStyle.alignment = TextAnchor.MiddleLeft;
        smallTextStyle.fontSize = 15;
        smallTextStyle.wordWrap = true;
        smallTextStyle.normal.textColor = Color.white;

        upgradeNameStyle = new GUIStyle(GUI.skin.label);
        upgradeNameStyle.alignment = TextAnchor.MiddleLeft;
        upgradeNameStyle.fontSize = 19;
        upgradeNameStyle.fontStyle = FontStyle.Bold;
        upgradeNameStyle.normal.textColor = Color.white;

        upgradeLevelStyle = new GUIStyle(GUI.skin.label);
        upgradeLevelStyle.alignment = TextAnchor.MiddleLeft;
        upgradeLevelStyle.fontSize = 13;
        upgradeLevelStyle.fontStyle = FontStyle.Bold;
        upgradeLevelStyle.normal.textColor = new Color(0.92f, 0.46f, 1f, 1f);

        upgradeDescriptionStyle = new GUIStyle(GUI.skin.label);
        upgradeDescriptionStyle.alignment = TextAnchor.MiddleLeft;
        upgradeDescriptionStyle.fontSize = 13;
        upgradeDescriptionStyle.wordWrap = true;
        upgradeDescriptionStyle.normal.textColor = new Color(0.84f, 0.82f, 0.86f, 1f);

        resourceStyle = new GUIStyle(GUI.skin.label);
        resourceStyle.alignment = TextAnchor.MiddleLeft;
        resourceStyle.fontSize = 14;
        resourceStyle.fontStyle = FontStyle.Bold;
        resourceStyle.normal.textColor = new Color(1f, 0.86f, 0.42f, 1f);

        costStyle = new GUIStyle(GUI.skin.label);
        costStyle.alignment = TextAnchor.MiddleLeft;
        costStyle.fontSize = 13;
        costStyle.fontStyle = FontStyle.Bold;
        costStyle.normal.textColor = new Color(1f, 0.78f, 0.34f, 1f);

        hudDistanceStyle = new GUIStyle(GUI.skin.label);
        hudDistanceStyle.alignment = TextAnchor.MiddleLeft;
        hudDistanceStyle.fontSize = 26;
        hudDistanceStyle.fontStyle = FontStyle.Bold;
        hudDistanceStyle.normal.textColor = new Color(1f, 0.7f, 1f, 1f);

        hudLabelStyle = new GUIStyle(GUI.skin.label);
        hudLabelStyle.alignment = TextAnchor.MiddleCenter;
        hudLabelStyle.fontSize = 13;
        hudLabelStyle.fontStyle = FontStyle.Bold;
        hudLabelStyle.normal.textColor = new Color(0.9f, 0.55f, 1f, 1f);

        iconStyle = new GUIStyle(GUI.skin.label);
        iconStyle.alignment = TextAnchor.MiddleCenter;
        iconStyle.fontSize = 24;
        iconStyle.fontStyle = FontStyle.Bold;
        iconStyle.normal.textColor = new Color(1f, 0.54f, 1f, 1f);

        neonButtonStyle = new GUIStyle(GUI.skin.button);
        neonButtonStyle.fontSize = 16;
        neonButtonStyle.fontStyle = FontStyle.Bold;
        neonButtonStyle.alignment = TextAnchor.MiddleCenter;
        neonButtonStyle.normal.background = purpleTexture;
        neonButtonStyle.hover.background = purpleHoverTexture;
        neonButtonStyle.active.background = purpleHoverTexture;
        neonButtonStyle.normal.textColor = new Color(1f, 0.78f, 1f, 1f);
        neonButtonStyle.hover.textColor = Color.white;
        neonButtonStyle.active.textColor = Color.white;

        rocketButtonStyle = new GUIStyle(neonButtonStyle);
        rocketButtonStyle.fontSize = 21;
        rocketButtonStyle.normal.background = orangeTexture;
        rocketButtonStyle.hover.background = orangeHoverTexture;
        rocketButtonStyle.active.background = orangeHoverTexture;
        rocketButtonStyle.normal.textColor = new Color(1f, 0.78f, 0.38f, 1f);

        upgradePanelStyle = new GUIStyle(GUI.skin.box);
        upgradePanelStyle.normal.background = cardTexture;

        Texture2D timerBackTexture = new Texture2D(1, 1);
        timerBackTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        timerBackTexture.Apply();

        Texture2D timerFillTexture = new Texture2D(1, 1);
        timerFillTexture.SetPixel(0, 0, new Color(0.2f, 0.85f, 0.45f, 0.95f));
        timerFillTexture.Apply();

        Texture2D timerDangerTexture = new Texture2D(1, 1);
        timerDangerTexture.SetPixel(0, 0, new Color(0.9f, 0.15f, 0.1f, 0.95f));
        timerDangerTexture.Apply();

        timerBackStyle = new GUIStyle(GUI.skin.box);
        timerBackStyle.normal.background = timerBackTexture;

        timerFillStyle = new GUIStyle(GUI.skin.box);
        timerFillStyle.normal.background = timerFillTexture;

        timerDangerFillStyle = new GUIStyle(GUI.skin.box);
        timerDangerFillStyle.normal.background = timerDangerTexture;

        endingStyle = new GUIStyle(GUI.skin.label);
        endingStyle.alignment = TextAnchor.MiddleCenter;
        endingStyle.fontSize = 28;
        endingStyle.wordWrap = true;
        endingStyle.normal.textColor = Color.white;
    }

    void CreateUpgradeList()
    {
        upgrades = new UpgradeInfo[]
        {
            new UpgradeInfo("Dash", "Dash", 1, "Permite usar um avanco.", new int[] { 1000 }),
            new UpgradeInfo("Salto duplo", "DoubleJump", 1, "Permite um segundo salto.", new int[] { 1000 }),
            new UpgradeInfo("Velocidade", "Speed", 7, "Aumenta o limite de velocidade alcancavel.", SevenLevelCosts),
            new UpgradeInfo("Aceleracao", "Acceleration", 7, "Atinge a velocidade limite mais rapido.", SevenLevelCosts),
            new UpgradeInfo("Salto", "Jump", 7, "Aumenta a altura do salto.", SevenLevelCosts),
            new UpgradeInfo("Resistencia", "Resistance", 3, "Perde menos velocidade ao atingir obstaculos e aumenta em 15s a resistencia a corrosao.", ResistanceCosts),
            new UpgradeInfo("Recursos", "ResourceBonus", 4, "Gera recursos extras: quanto maior o nivel, mais moedas e materiais bonus voce recebe por corrida.", ResourceBonusCosts)
        };
    }

    string GetCoinsText()
    {
        return InfiniteResources ? "Infinito" : totalCoins.ToString();
    }

    string GetMaterialsText()
    {
        return InfiniteResources ? "Infinito" : totalMaterials.ToString();
    }

    struct UpgradeInfo
    {
        public readonly string name;
        public readonly string id;
        public int currentLevel;
        public readonly int maxLevel;
        public readonly string description;
        readonly int[] costs;

        public UpgradeInfo(string name, string id, int maxLevel, string description, int[] costs)
        {
            this.name = name;
            this.id = id;
            this.maxLevel = maxLevel;
            this.currentLevel = Mathf.Clamp(PlayerPrefs.GetInt(UpgradePrefix + id, 0), 0, maxLevel);
            this.description = description;
            this.costs = costs;
        }

        public int GetNextCost()
        {
            if (currentLevel >= maxLevel)
            {
                return 0;
            }

            if (costs == null || currentLevel >= costs.Length)
            {
                return 0;
            }

            return costs[currentLevel];
        }

        public string GetPrefsKey()
        {
            return UpgradePrefix + id;
        }
    }
}
