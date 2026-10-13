using UnityEngine;

public class RocketController : MonoBehaviour
{
    const string RocketLevelKey = "EcoRunnerRocketLevelV1";
    const string RocketResetKey = "EcoRunnerRocketResetForTestV1";
    const int MaxRocketLevel = 4;

    public int rocketLevel;
    public bool isLaunching;
    public bool launchResolved;
    public float launchTimer;
    public float launchSpeed = 1.35f;
    public float nextPropulsionDelay = 5f;
    public float minOuterCircle = 38f;
    public float maxOuterCircle = 150f;
    public float lastLaunchScore;
    public float lastLaunchPower;
    public string lastLaunchRating = "";
    public float perfectShakeDuration = 0.18f;
    public float perfectShakeStrength = 0.35f;

    Transform pit;
    Transform rocket;
    Transform[] rocketModels = new Transform[MaxRocketLevel];
    RocketFlight activeFlight;
    int propulsionsUsed;
    float nextPropulsionTimer;
    bool flightInProgress;
    Camera mainCamera;
    Vector3 currentShakeOffset;
    float shakeTimer;
    bool isShakingCamera;

    public int RocketLevel
    {
        get { return rocketLevel; }
    }

    public bool CanBuild
    {
        get { return rocketLevel < MaxRocketLevel && !flightInProgress; }
    }

    public bool CanLaunch
    {
        get { return rocketLevel > 0 && !isLaunching && !flightInProgress; }
    }

    public bool HasEscapedPlanet
    {
        get { return activeFlight != null && activeFlight.escapedPlanet; }
    }

    public float CurrentAltitude
    {
        get
        {
            RocketFlight flight = activeFlight;

            if (flight == null)
            {
                Transform activeModel = GetActiveRocketModel();
                flight = activeModel != null ? activeModel.GetComponent<RocketFlight>() : null;
            }

            return flight != null ? flight.Altitude : 0f;
        }
    }

    public Transform CameraTarget
    {
        get
        {
            if (rocket == null || pit == null)
            {
                FindRocketObjects();
            }

            Transform activeModel = GetActiveRocketModel();

            if (activeModel != null)
            {
                return activeModel;
            }

            if (rocket != null)
            {
                return rocket;
            }

            return pit;
        }
    }

    public Transform ActiveRocketModel
    {
        get { return GetActiveRocketModel(); }
    }

    public float CurrentOuterRadius
    {
        get
        {
            float wave = Mathf.PingPong(launchTimer * launchSpeed, 1f);
            return Mathf.Lerp(maxOuterCircle, minOuterCircle, wave);
        }
    }

    public float TargetRadius
    {
        get { return minOuterCircle; }
    }

    private void Awake()
    {
        FindRocketObjects();
        mainCamera = Camera.main;
        ResetSavedRocketLevelOnceForTesting();
        Load();
        ApplyRocketLevel();
    }

    private void Update()
    {
        if (flightInProgress && !isLaunching && propulsionsUsed < rocketLevel)
        {
            nextPropulsionTimer += Time.deltaTime;

            if (nextPropulsionTimer >= nextPropulsionDelay)
            {
                StartPropulsionTiming();
            }
        }

        if (!isLaunching)
        {
            return;
        }

        launchTimer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResolveLaunch();
        }
    }

    private void LateUpdate()
    {
        UpdateCameraShake();
    }

    public void BuildNextPart()
    {
        if (!CanBuild)
        {
            return;
        }

        rocketLevel++;
        Save();
        ApplyRocketLevel();
    }

    public void StartLaunch()
    {
        if (!CanLaunch)
        {
            return;
        }

        isLaunching = true;
        launchResolved = false;
        launchTimer = 0f;
        lastLaunchScore = 0f;
        lastLaunchPower = 0f;
        lastLaunchRating = "";
        propulsionsUsed = 0;
        nextPropulsionTimer = 0f;
        flightInProgress = false;
    }

    public void CancelLaunch()
    {
        isLaunching = false;
        launchResolved = false;
    }

    public void ResetRocketForView()
    {
        isLaunching = false;
        launchResolved = false;
        launchTimer = 0f;
        nextPropulsionTimer = 0f;
        propulsionsUsed = 0;
        flightInProgress = false;

        for (int i = 0; i < rocketModels.Length; i++)
        {
            if (rocketModels[i] == null)
            {
                continue;
            }

            RocketFlight flight = rocketModels[i].GetComponent<RocketFlight>();

            if (flight != null)
            {
                flight.ResetFlight();
            }
        }

        ApplyRocketLevel();
    }

    public void ResetSavedRocketLevelForTesting()
    {
        rocketLevel = 0;
        PlayerPrefs.SetInt(RocketLevelKey, 0);
        PlayerPrefs.Save();
        ResetRocketForView();
    }

    void ResetSavedRocketLevelOnceForTesting()
    {
        if (PlayerPrefs.GetInt(RocketResetKey, 0) == 1)
        {
            return;
        }

        PlayerPrefs.SetInt(RocketLevelKey, 0);
        PlayerPrefs.SetInt(RocketResetKey, 1);
        PlayerPrefs.Save();
    }

    void ResolveLaunch()
    {
        float miss = Mathf.Abs(CurrentOuterRadius - TargetRadius);
        float score = Mathf.Clamp01(1f - (miss / (maxOuterCircle - minOuterCircle)));

        lastLaunchScore = score * rocketLevel;
        lastLaunchPower = GetPowerMultiplier(score);
        lastLaunchRating = GetRating(score);
        isLaunching = false;
        launchResolved = true;

        if (score >= 0.94f)
        {
            StartPerfectCameraShake();
        }

        TriggerNextRocketPropulsion();
    }

    void StartPropulsionTiming()
    {
        isLaunching = true;
        launchResolved = false;
        launchTimer = 0f;
        nextPropulsionTimer = 0f;
    }

    float GetPowerMultiplier(float score)
    {
        if (score >= 0.94f)
        {
            return 1.3f;
        }

        if (score >= 0.78f)
        {
            return 1.1f;
        }

        if (score >= 0.55f)
        {
            return 0.85f;
        }

        return 0.55f;
    }

    string GetRating(float score)
    {
        if (score >= 0.94f)
        {
            return "Perfeito";
        }

        if (score >= 0.78f)
        {
            return "Otimo";
        }

        if (score >= 0.55f)
        {
            return "Bom";
        }

        return "Pessimo";
    }

    void Load()
    {
        rocketLevel = Mathf.Clamp(PlayerPrefs.GetInt(RocketLevelKey, 0), 0, MaxRocketLevel);
    }

    void Save()
    {
        PlayerPrefs.SetInt(RocketLevelKey, rocketLevel);
        PlayerPrefs.Save();
    }

    void FindRocketObjects()
    {
        pit = FindTransformByName("pitDecolagem");

        if (pit == null)
        {
            pit = FindTransformByName("pit decolagem");
        }

        rocket = FindChildByNameFromRoot(pit, "Foguet");

        if (rocket == null)
        {
            rocket = FindChildByNameFromRoot(pit, "foguete");
        }

        if (rocket == null)
        {
            rocket = FindTransformByName("Foguet");
        }

        if (rocket == null)
        {
            rocket = FindTransformByName("foguete");
        }

        rocketModels[0] = FindChildByName("melhoria1");
        rocketModels[1] = FindChildByName("melhoria2");
        rocketModels[2] = FindChildByName("melhoria3");
        rocketModels[3] = FindChildByName("melhoria4");
    }

    Transform FindChildByName(string objectName)
    {
        Transform root = rocket != null ? rocket : pit;

        Transform child = FindChildByNameFromRoot(root, objectName);

        return child != null ? child : FindTransformByName(objectName);
    }

    Transform FindChildByNameFromRoot(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (NamesMatch(children[i].name, objectName))
            {
                return children[i];
            }
        }

        return null;
    }

    Transform FindTransformByName(string objectName)
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

    void ApplyRocketLevel()
    {
        if (rocket == null)
        {
            FindRocketObjects();
        }

        if (rocket != null)
        {
            rocket.gameObject.SetActive(rocketLevel > 0);
        }

        for (int i = 0; i < rocketModels.Length; i++)
        {
            if (rocketModels[i] != null)
            {
                rocketModels[i].gameObject.SetActive(i == rocketLevel - 1);

                RocketFlight flight = rocketModels[i].GetComponent<RocketFlight>();

                if (flight != null && i != rocketLevel - 1)
                {
                    flight.ResetFlight();
                }
            }
        }
    }

    void TriggerNextRocketPropulsion()
    {
        Transform activeModel = GetActiveRocketModel();

        if (activeModel == null)
        {
            return;
        }

        RocketFlight flight = activeModel.GetComponent<RocketFlight>();

        if (flight == null)
        {
            flight = activeModel.gameObject.AddComponent<RocketFlight>();
        }

        flight.ConfigureForLevel(rocketLevel);

        if (!flightInProgress)
        {
            flight.BeginFlight();
            activeFlight = flight;
            flightInProgress = true;
        }

        activeFlight.TriggerPropulsion(lastLaunchPower);
        propulsionsUsed++;
        nextPropulsionTimer = 0f;
    }

    Transform GetActiveRocketModel()
    {
        int index = rocketLevel - 1;

        if (index < 0 || index >= rocketModels.Length)
        {
            return null;
        }

        if (rocketModels[index] == null)
        {
            FindRocketObjects();
        }

        return rocketModels[index];
    }

    void StartPerfectCameraShake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        currentShakeOffset = Vector3.zero;
        shakeTimer = perfectShakeDuration;
        isShakingCamera = true;
    }

    void UpdateCameraShake()
    {
        if (!isShakingCamera)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            isShakingCamera = false;
            return;
        }

        mainCamera.transform.position -= currentShakeOffset;
        currentShakeOffset = Vector3.zero;
        shakeTimer -= Time.deltaTime;

        if (shakeTimer <= 0f)
        {
            isShakingCamera = false;
            return;
        }

        float fade = shakeTimer / Mathf.Max(0.001f, perfectShakeDuration);
        Vector2 offset = Random.insideUnitCircle * perfectShakeStrength * fade;
        currentShakeOffset = new Vector3(offset.x, offset.y, 0f);
        mainCamera.transform.position += currentShakeOffset;
    }
}
