using UnityEngine;

public class RocketFlight : MonoBehaviour
{
    const string PropulsionSpritePath = "Rocket/ChatGPT Image 24 de mai. de 2026, 18_21_32 (1)";

    public float gravity = -18f;
    public float accelerationLimit = 55f;
    public float accelerationGrowth = 1.6f;
    public float maxUpwardVelocity = 90f;
    public float propulsionDuration = 6f;
    public Vector2 velocity;
    public bool isFlying;
    public bool escapedPlanet;
    public float escapeAltitude = 3650f;
    public float escapePowerRequired = 5f;
    public float accumulatedLaunchPower;
    public float launchPower = 1f;
    public int rocketLevel = 1;
    public float skyDarkenStartAltitude = 200f;
    public float skyDarkenEndAltitude = 2400f;
    public Color darkSkyColor = new Color(0.02f, 0.025f, 0.08f, 1f);
    public bool hasLandedOnPlatform;

    [Header("Level Tuning")]
    public float level1AccelerationLimit = 82f;
    public float level1AccelerationGrowth = 2.8f;
    public float level1MaxUpwardVelocity = 55f;
    public float level1Gravity = -6.9f;
    public float level4AccelerationLimit = 54f;
    public float level4AccelerationGrowth = 0.9f;
    public float level4MaxUpwardVelocity = 175f;
    public float level4Gravity = -22f;

    readonly float[] propulsionTimers = new float[4];
    readonly bool[] activePropulsions = new bool[4];
    Vector3 startPosition;
    RocketPropulsionVisual propulsionVisual;
    SpriteRenderer skyRenderer;
    SpriteRenderer baseTriangleRenderer;
    SpriteRenderer landingPlatformRenderer;
    Color skyOriginalColor;
    bool hasSkyOriginalColor;

    public float Altitude
    {
        get { return Mathf.Max(0f, transform.position.y - startPosition.y); }
    }

    public bool HasActivePropulsion
    {
        get
        {
            for (int i = 0; i < activePropulsions.Length; i++)
            {
                if (activePropulsions[i])
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (propulsionVisual != null)
        {
            propulsionVisual.SetActive(HasActivePropulsion);
        }

        UpdateSkyColor();
    }

    private void FixedUpdate()
    {
        if (!isFlying)
        {
            return;
        }

        float acceleration = gravity + GetActivePropulsionAcceleration();
        float previousBaseBottom = GetBaseBottomY();

        velocity.y += acceleration * Time.fixedDeltaTime;
        velocity.y = Mathf.Clamp(velocity.y, -Mathf.Infinity, maxUpwardVelocity);
        transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.fixedDeltaTime;
        CheckPlatformLanding(previousBaseBottom);
        CheckEscapeAltitude();
    }

    void CheckEscapeAltitude()
    {
        if (escapedPlanet || rocketLevel < 4 || accumulatedLaunchPower < escapePowerRequired)
        {
            return;
        }

        if (transform.position.y - startPosition.y >= escapeAltitude)
        {
            escapedPlanet = true;
            isFlying = false;
            ClearPropulsions();
        }
    }

    float GetActivePropulsionAcceleration()
    {
        float acceleration = 0f;

        for (int i = 0; i < activePropulsions.Length; i++)
        {
            if (!activePropulsions[i])
            {
                continue;
            }

            propulsionTimers[i] += Time.fixedDeltaTime;
            float thrust01 = 1f - Mathf.Exp(-accelerationGrowth * propulsionTimers[i]);
            acceleration += accelerationLimit * launchPower * thrust01;

            if (propulsionTimers[i] >= propulsionDuration)
            {
                activePropulsions[i] = false;
                propulsionTimers[i] = 0f;
            }
        }

        return acceleration;
    }

    public void BeginFlight()
    {
        velocity = Vector2.zero;
        ClearPropulsions();
        EnsurePropulsionVisual();
        EnsureSkyRenderer();
        EnsureLandingReferences();
        escapedPlanet = false;
        hasLandedOnPlatform = false;
        accumulatedLaunchPower = 0f;
        isFlying = true;
    }

    public void ConfigureForLevel(int level)
    {
        rocketLevel = Mathf.Clamp(level, 1, 4);
        float t = (rocketLevel - 1f) / 3f;

        accelerationLimit = Mathf.Lerp(level1AccelerationLimit, level4AccelerationLimit, t);
        accelerationGrowth = Mathf.Lerp(level1AccelerationGrowth, level4AccelerationGrowth, t);
        maxUpwardVelocity = Mathf.Lerp(level1MaxUpwardVelocity, level4MaxUpwardVelocity, t);
        gravity = Mathf.Lerp(level1Gravity, level4Gravity, t);
    }

    public void TriggerPropulsion(float powerMultiplier)
    {
        launchPower = Mathf.Max(0.1f, powerMultiplier);

        if (!isFlying)
        {
            BeginFlight();
        }

        EnsurePropulsionVisual();
        accumulatedLaunchPower += launchPower;

        for (int i = 0; i < activePropulsions.Length; i++)
        {
            if (activePropulsions[i])
            {
                continue;
            }

            activePropulsions[i] = true;
            propulsionTimers[i] = 0f;
            return;
        }
    }

    public void ResetFlight()
    {
        isFlying = false;
        escapedPlanet = false;
        hasLandedOnPlatform = false;
        accumulatedLaunchPower = 0f;
        velocity = Vector2.zero;
        ClearPropulsions();
        transform.position = startPosition;
        RestoreSkyColor();
    }

    void ClearPropulsions()
    {
        for (int i = 0; i < activePropulsions.Length; i++)
        {
            activePropulsions[i] = false;
            propulsionTimers[i] = 0f;
        }

        if (propulsionVisual != null)
        {
            propulsionVisual.SetActive(false);
        }
    }

    void EnsurePropulsionVisual()
    {
        if (propulsionVisual == null)
        {
            propulsionVisual = GetComponentInChildren<RocketPropulsionVisual>(true);
        }

        if (propulsionVisual == null)
        {
            GameObject visualObject = new GameObject("Propulsao");
            visualObject.transform.SetParent(transform, false);
            propulsionVisual = visualObject.AddComponent<RocketPropulsionVisual>();
        }

        propulsionVisual.Setup(transform, LoadPropulsionFrames());
    }

    Sprite[] LoadPropulsionFrames()
    {
        Sprite[] assets = Resources.LoadAll<Sprite>(PropulsionSpritePath);
        Sprite[] sprites = new Sprite[assets.Length];
        int count = 0;

        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i];

            if (sprite == null || sprite.rect.width < 100f || sprite.rect.height < 200f)
            {
                continue;
            }

            sprites[count] = sprite;
            count++;
        }

        Sprite[] frames = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            frames[i] = sprites[i];
        }

        return frames;
    }

    void UpdateSkyColor()
    {
        EnsureSkyRenderer();

        if (skyRenderer == null)
        {
            return;
        }

        float range = Mathf.Max(1f, skyDarkenEndAltitude - skyDarkenStartAltitude);
        float altitude01 = Mathf.Clamp01((Altitude - skyDarkenStartAltitude) / range);
        skyRenderer.color = Color.Lerp(skyOriginalColor, darkSkyColor, altitude01);
    }

    void RestoreSkyColor()
    {
        EnsureSkyRenderer();

        if (skyRenderer != null && hasSkyOriginalColor)
        {
            skyRenderer.color = skyOriginalColor;
        }
    }

    void EnsureSkyRenderer()
    {
        if (skyRenderer != null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        float largestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null || !renderer.name.Replace(" ", "").ToLowerInvariant().StartsWith("square"))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            float area = bounds.size.x * bounds.size.y;

            if (area <= largestArea)
            {
                continue;
            }

            largestArea = area;
            skyRenderer = renderer;
        }

        if (skyRenderer == null)
        {
            return;
        }

        skyOriginalColor = skyRenderer.color;
        hasSkyOriginalColor = true;
    }

    void CheckPlatformLanding(float previousBaseBottom)
    {
        if (velocity.y > 0f || escapedPlanet || HasActivePropulsion)
        {
            return;
        }

        EnsureLandingReferences();

        if (baseTriangleRenderer == null || landingPlatformRenderer == null)
        {
            return;
        }

        Bounds baseBounds = baseTriangleRenderer.bounds;
        Bounds platformBounds = landingPlatformRenderer.bounds;
        float platformTop = platformBounds.max.y;
        float currentBaseBottom = baseBounds.min.y;
        bool crossedPlatformTop = previousBaseBottom >= platformTop && currentBaseBottom <= platformTop;
        bool overlapsPlatformX = baseBounds.max.x >= platformBounds.min.x && baseBounds.min.x <= platformBounds.max.x;

        if (!crossedPlatformTop || !overlapsPlatformX)
        {
            return;
        }

        Vector3 position = transform.position;
        position.y += platformTop - currentBaseBottom;
        transform.position = position;
        velocity = Vector2.zero;
        isFlying = false;
        hasLandedOnPlatform = true;
        ClearPropulsions();
    }

    float GetBaseBottomY()
    {
        EnsureLandingReferences();

        if (baseTriangleRenderer == null)
        {
            return transform.position.y;
        }

        return baseTriangleRenderer.bounds.min.y;
    }

    void EnsureLandingReferences()
    {
        if (baseTriangleRenderer == null)
        {
            Transform triangle = FindChildByName(transform, "Triangle");
            baseTriangleRenderer = triangle != null ? triangle.GetComponent<SpriteRenderer>() : null;
        }

        if (landingPlatformRenderer != null)
        {
            return;
        }

        Transform platform = FindSceneTransform("Plataforma");

        if (platform == null)
        {
            return;
        }

        landingPlatformRenderer = FindLandingPlatformRenderer(platform);
    }

    SpriteRenderer FindLandingPlatformRenderer(Transform platform)
    {
        SpriteRenderer[] renderers = platform.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer bestRenderer = null;
        float bestScore = Mathf.Infinity;
        float baseBottom = baseTriangleRenderer != null ? baseTriangleRenderer.bounds.min.y : transform.position.y;
        float baseX = baseTriangleRenderer != null ? baseTriangleRenderer.bounds.center.x : transform.position.x;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            bool overlapsBaseX = baseX >= bounds.min.x && baseX <= bounds.max.x;
            float topDistance = Mathf.Abs(baseBottom - bounds.max.y);
            float score = topDistance + (overlapsBaseX ? 0f : 1000f);

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestRenderer = renderer;
        }

        return bestRenderer;
    }

    Transform FindChildByName(Transform root, string objectName)
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
}

public class RocketPropulsionVisual : MonoBehaviour
{
    public float framesPerSecond = 12f;
    public float heightMultiplier = 1.6f;
    public float verticalOffsetMultiplier = 0.5f;

    SpriteRenderer spriteRenderer;
    Sprite[] frames = new Sprite[0];
    float timer;
    int frameIndex;

    public void Setup(Transform rocketModel, Sprite[] animationFrames)
    {
        EnsureRenderer();

        if (animationFrames != null && animationFrames.Length > 0)
        {
            frames = animationFrames;
            spriteRenderer.sprite = frames[0];
        }

        PositionFromTriangle(rocketModel);
        SetActive(false);
    }

    public void SetActive(bool active)
    {
        EnsureRenderer();

        if (spriteRenderer.enabled == active)
        {
            return;
        }

        spriteRenderer.enabled = active;
        timer = 0f;
        frameIndex = 0;

        if (active && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (!spriteRenderer.enabled || frames.Length <= 1)
        {
            return;
        }

        timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        if (timer < frameDuration)
        {
            return;
        }

        timer -= frameDuration;
        frameIndex = (frameIndex + 1) % frames.Length;
        spriteRenderer.sprite = frames[frameIndex];
    }

    void EnsureRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingOrder = 1000;
    }

    void PositionFromTriangle(Transform rocketModel)
    {
        if (frames.Length == 0 || frames[0] == null)
        {
            return;
        }

        Transform triangle = FindChildByName(rocketModel, "Triangle");
        SpriteRenderer triangleRenderer = triangle != null ? triangle.GetComponent<SpriteRenderer>() : null;

        if (triangleRenderer == null)
        {
            transform.localPosition = new Vector3(0f, -1f, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            transform.localScale = Vector3.one;
            return;
        }

        Bounds triangleBounds = triangleRenderer.bounds;
        float desiredWidth = triangleBounds.size.x;
        float desiredHeight = triangleBounds.size.y * heightMultiplier;
        Vector3 worldPosition = new Vector3(
            triangleBounds.center.x,
            triangleBounds.min.y - desiredHeight * verticalOffsetMultiplier,
            triangleBounds.center.z);

        transform.position = worldPosition;
        transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        Vector2 spriteSize = frames[0].bounds.size;
        Vector3 parentScale = rocketModel.lossyScale;
        float scaleX = desiredWidth / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x));
        float scaleY = desiredHeight / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y));
        transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.Replace(" ", "").ToLowerInvariant() == childName.ToLowerInvariant())
            {
                return children[i];
            }
        }

        return null;
    }
}
