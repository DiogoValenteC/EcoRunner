using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Player : MonoBehaviour
{
    const string DashLevelKey = "EcoRunnerUpgradeV2_Dash";
    const string DoubleJumpLevelKey = "EcoRunnerUpgradeV2_DoubleJump";
    const string SpeedLevelKey = "EcoRunnerUpgradeV2_Speed";
    const string AccelerationLevelKey = "EcoRunnerUpgradeV2_Acceleration";
    const string JumpLevelKey = "EcoRunnerUpgradeV2_Jump";
    const string ResistanceLevelKey = "EcoRunnerUpgradeV2_Resistance";

    [Header("Movement")]
    public float gravity = -150f;
    public Vector2 velocity;

    public float baseMaxVelocity = 36f;
    public float upgradedMaxVelocity = 120f;
    public float maxVelocity = 36f;
    public int speedLevel = 0;

    public float baseMaxAcceleration = 3f;
    public float accelerationBonusPerLevel = 0.1f;
    public float maxAcceleration = 3f;
    public int accelerationLevel = 0;
    public float acceleration = 3f;

    public float distance = 0f;
    public float maxRunDistance = 0f;
    public float totalRunDistance = 0f;

    [Header("Direction")]
    public int runDirection = 1;
    public float directionChangeSpeedLoss = 0.1f;
    public float cameraSideOffset = 6f;
    public float horizontalLaneSmoothTime = 0.08f;
    public float laneVelocity = 0f;

    [Header("Toxic Timer")]
    public float toxicRunTime = 120f;
    public float baseToxicRunTime = 120f;
    public float toxicRunTimeBonusPerResistanceLevel = 15f;
    public float toxicSlowdownDuration = 7f;
    public float toxicTimer = 0f;
    public float toxicSlowdownTimer = 0f;
    public bool isToxicSlowingDown = false;
    public float toxicSlowdownStartSpeed = 0f;

    [Header("Dash")]
    public float dashVelocity = 130f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.75f;
    public bool canDash = false;
    public int dashLevel = 0;
    public int maxDashCharges = 0;
    public int dashCharges = 0;
    public float dashTimer = 0f;
    public float dashCooldownTimer = 0f;
    public float dashInvincibilityDuration = 0.25f;
    public float dashInvincibilityTimer = 0f;

    [Header("Jump")]
    public float baseJumpVelocity = 25f;
    public float maxJumpVelocityMultiplier = 1.5f;
    public float jumpVelocity = 25f;
    public float doubleJumpHeightMultiplier = 1f;
    public float groundHeight = -3f;

    public bool isGrounded = false;

    public bool isHoldingJump = false;

    public float maxHoldJumpTime = 0.3f;
    public float maxMaxHoldJumpTime = 0.3f;

    public float holdJumpTimer = 0f;
    public float activeHoldJumpTimeLimit = 0f;
    public bool hasPendingDoubleJump = false;

    public float jumpGroundThreshold = 1f;
    public float bodyHalfWidth = 0.7f;
    public float groundSnapDistance = 0.25f;
    public bool canDoubleJump = false;
    public int doubleJumpLevel = 0;
    public int jumpLevel = 0;
    public int maxAirJumps = 0;
    public int airJumpsRemaining = 0;

    [Header("Collision")]
    public Vector2 defaultColliderSize = new Vector2(1f, 1f);
    public Vector2 defaultColliderOffset = Vector2.zero;
    public float obstacleSpeedLoss = 0.3f;
    public float resistanceLossReductionPerLevel = 0.15f;
    public int resistanceLevel = 0;

    [Header("State")]
    public bool hasStarted = false;
    public bool isDead = false;
    public bool reachedBase = false;
    public float minimumDistanceToReachBase = 5f;

    BoxCollider2D playerCollider;
    Collider2D baseCollider;
    Transform baseTransform;
    Camera playerCamera;
    Vector3 cameraOffset;
    PlayerSpriteAnimator spriteAnimator;

    public float GravityStrength
    {
        get { return Mathf.Abs(gravity); }
    }

    public float MaxJumpHeight
    {
        get
        {
            if (GravityStrength <= 0f)
            {
                return jumpVelocity * maxHoldJumpTime;
            }

            float holdHeight = jumpVelocity * maxHoldJumpTime;
            float gravityHeight = (jumpVelocity * jumpVelocity) / (2f * GravityStrength);
            return holdHeight + gravityHeight;
        }
    }

    public float CurrentRunSpeed
    {
        get { return Mathf.Min(Mathf.Max(velocity.x, maxVelocity * 0.35f), maxVelocity); }
    }

    public float ToxicTimer01
    {
        get
        {
            if (toxicRunTime <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - (toxicTimer / toxicRunTime));
        }
    }

    private void Awake()
    {
        playerCollider = GetComponent<BoxCollider2D>();

        if (playerCollider == null)
        {
            playerCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        if (playerCollider.size.sqrMagnitude <= 0.001f)
        {
            playerCollider.size = defaultColliderSize;
            playerCollider.offset = defaultColliderOffset;
        }

        SetupSpriteAnimator();
        MatchColliderToVisualSize();
        bodyHalfWidth = Mathf.Max(bodyHalfWidth, playerCollider.bounds.extents.x);
        ApplySavedUpgrades();
        FindBase();
        SetupCameraFollow();
    }

    private void Start()
    {
        TrySnapToGroundBelow();
    }

    void Update()
    {
        if (!hasStarted || isDead)
        {
            return;
        }

        if (CanJump() && Input.GetKeyDown(KeyCode.Space))
        {
            StartJump();
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            TryDash();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            isHoldingJump = false;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            ChangeRunDirection(-1);
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            ChangeRunDirection(1);
        }
    }

    private void FixedUpdate()
    {
        if (!hasStarted || isDead)
        {
            return;
        }

        Vector2 pos = transform.position;

        if (pos.y < -20f)
        {
            Die();
            return;
        }

        float previousY = pos.y;

        UpdateToxicTimer();
        UpdateHorizontalSpeed();
        MoveBaseWithWorld();

        if (!isGrounded)
        {
            ApplyPendingDoubleJump();
            UpdateJumpHold();
            MoveVertically(ref pos);
            ApplyGravity();
            TryLand(ref pos, previousY);
            CheckWall(ref pos);
        }
        else
        {
            StickToGround(ref pos);
            CheckGroundAhead(pos);
        }

        CheckObstacles(ref pos);
        CheckBaseReached(pos);
        MoveToCameraSide(ref pos);

        UpdateRunDistance();
        transform.position = pos;
        UpdateCameraFollow();
    }

    public void BeginRun()
    {
        ApplySavedUpgrades();
        hasStarted = true;
        isDead = false;
        reachedBase = false;
        runDirection = 1;
        toxicTimer = 0f;
        toxicSlowdownTimer = 0f;
        isToxicSlowingDown = false;
        toxicSlowdownStartSpeed = 0f;
        dashInvincibilityTimer = 0f;
        distance = 0f;
        maxRunDistance = 0f;
        totalRunDistance = 0f;
        RecoverDashCharges();
        RecoverAirJumps();
        FindBase();
    }

    void UpdateRunDistance()
    {
        float distanceDelta = velocity.x * runDirection * Time.fixedDeltaTime;
        distance += distanceDelta;
        distance = Mathf.Max(0f, distance);
        maxRunDistance = Mathf.Max(maxRunDistance, distance);
        totalRunDistance += Mathf.Abs(distanceDelta);
    }

    void SetupCameraFollow()
    {
        playerCamera = Camera.main;

        if (playerCamera == null)
        {
            return;
        }

        cameraOffset = playerCamera.transform.position - transform.position;

        if (playerCamera.transform.parent == transform)
        {
            playerCamera.transform.SetParent(null, true);
        }
    }

    void UpdateCameraFollow()
    {
        if (playerCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = playerCamera.transform.position;
        cameraPosition.y = transform.position.y + cameraOffset.y;
        playerCamera.transform.position = cameraPosition;
    }

    void SetupSpriteAnimator()
    {
        spriteAnimator = GetComponent<PlayerSpriteAnimator>();

        if (spriteAnimator == null)
        {
            spriteAnimator = gameObject.AddComponent<PlayerSpriteAnimator>();
        }
    }

    void MatchColliderToVisualSize()
    {
        if (playerCollider == null || spriteAnimator == null)
        {
            return;
        }

        float scale = Mathf.Max(0.01f, spriteAnimator.visualScale);
        playerCollider.size = defaultColliderSize * scale;
        playerCollider.offset = defaultColliderOffset * scale;
    }

    void FindBase()
    {
        GameObject baseObject = GameObject.Find("Base");

        if (baseObject == null)
        {
            baseTransform = null;
            baseCollider = null;
            return;
        }

        baseTransform = baseObject.transform;
        baseCollider = baseObject.GetComponent<Collider2D>();

        if (baseCollider == null)
        {
            baseCollider = baseObject.AddComponent<BoxCollider2D>();
        }
    }

    void MoveBaseWithWorld()
    {
        if (baseTransform == null)
        {
            FindBase();
        }

        if (baseTransform == null)
        {
            return;
        }

        if (baseTransform.parent != null && baseTransform.parent.GetComponentInParent<Ground>() != null)
        {
            return;
        }

        Vector2 pos = baseTransform.position;
        pos.x -= velocity.x * runDirection * Time.fixedDeltaTime;
        baseTransform.position = pos;
    }

    void ChangeRunDirection(int newDirection)
    {
        if (newDirection == runDirection)
        {
            return;
        }

        runDirection = newDirection;
        velocity.x *= 1f - directionChangeSpeedLoss;
        dashTimer = 0f;
    }

    void MoveToCameraSide(ref Vector2 pos)
    {
        if (playerCamera == null)
        {
            return;
        }

        float targetX = playerCamera.transform.position.x - (cameraSideOffset * runDirection);
        pos.x = Mathf.SmoothDamp(pos.x, targetX, ref laneVelocity, horizontalLaneSmoothTime);
    }

    public void ApplySavedUpgrades()
    {
        speedLevel = Mathf.Clamp(PlayerPrefs.GetInt(SpeedLevelKey, 0), 0, 7);
        accelerationLevel = Mathf.Clamp(PlayerPrefs.GetInt(AccelerationLevelKey, 0), 0, 7);
        jumpLevel = Mathf.Clamp(PlayerPrefs.GetInt(JumpLevelKey, 0), 0, 7);
        resistanceLevel = Mathf.Clamp(PlayerPrefs.GetInt(ResistanceLevelKey, 0), 0, 3);
        toxicRunTime = baseToxicRunTime + (resistanceLevel * toxicRunTimeBonusPerResistanceLevel);
        maxVelocity = Mathf.Lerp(baseMaxVelocity, upgradedMaxVelocity, speedLevel / 7f);
        maxAcceleration = baseMaxAcceleration * (1f + (accelerationLevel * accelerationBonusPerLevel));
        jumpVelocity = baseJumpVelocity * Mathf.Lerp(1f, maxJumpVelocityMultiplier, jumpLevel / 7f);

        dashLevel = Mathf.Clamp(PlayerPrefs.GetInt(DashLevelKey, 0), 0, 1);
        maxDashCharges = dashLevel;
        canDash = maxDashCharges > 0;
        doubleJumpLevel = Mathf.Clamp(PlayerPrefs.GetInt(DoubleJumpLevelKey, 0), 0, 1);
        maxAirJumps = doubleJumpLevel;
        canDoubleJump = maxAirJumps > 0;

        if (!canDash)
        {
            dashCharges = 0;
            dashTimer = 0f;
            dashCooldownTimer = 0f;
            dashInvincibilityTimer = 0f;
            return;
        }

        dashCharges = Mathf.Clamp(dashCharges, 0, maxDashCharges);
        airJumpsRemaining = Mathf.Clamp(airJumpsRemaining, 0, maxAirJumps);
    }

    void TryDash()
    {
        if (isToxicSlowingDown || !canDash || dashCharges <= 0 || dashCooldownTimer > 0f || dashTimer > 0f)
        {
            return;
        }

        dashCharges--;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        dashInvincibilityTimer = dashInvincibilityDuration;
        velocity.x = dashVelocity;
        velocity.y = 0f;
        isHoldingJump = false;
        PlayDashAnimation();
    }

    void RecoverDashCharges()
    {
        if (!canDash)
        {
            return;
        }

        dashCharges = maxDashCharges;
    }

    public float GetReachableDistance(float targetGroundHeight)
    {
        if (GravityStrength <= 0f)
        {
            return CurrentRunSpeed * maxHoldJumpTime;
        }

        float heightDelta = targetGroundHeight - groundHeight;
        float maxHeight = MaxJumpHeight;

        if (heightDelta > maxHeight)
        {
            return 0f;
        }

        float holdTime = maxHoldJumpTime;
        float riseTimeAfterHold = jumpVelocity / GravityStrength;
        float fallHeight = Mathf.Max(0f, maxHeight - heightDelta);
        float fallTime = Mathf.Sqrt((2f * fallHeight) / GravityStrength);
        float totalAirTime = holdTime + riseTimeAfterHold + fallTime;

        return CurrentRunSpeed * totalAirTime;
    }

    bool CanJump()
    {
        float groundDistance = Mathf.Abs(GetFeetY(transform.position) - groundHeight);
        return isGrounded || groundDistance <= jumpGroundThreshold || CanAirJump();
    }

    void StartJump()
    {
        bool isAirJump = !isGrounded && CanAirJump();

        if (isAirJump)
        {
            airJumpsRemaining--;
            velocity.y = 0f;
            holdJumpTimer = 0f;
            isHoldingJump = false;
            hasPendingDoubleJump = true;
            PlayAirJumpAnimation();
            return;
        }

        isGrounded = false;
        velocity.y = jumpVelocity;
        activeHoldJumpTimeLimit = maxHoldJumpTime;
        isHoldingJump = true;
        holdJumpTimer = 0f;
        PlayJumpAnimation();
    }

    void ApplyPendingDoubleJump()
    {
        if (!hasPendingDoubleJump)
        {
            return;
        }

        hasPendingDoubleJump = false;
        isGrounded = false;
        velocity.y = jumpVelocity * Mathf.Sqrt(doubleJumpHeightMultiplier);
        activeHoldJumpTimeLimit = maxHoldJumpTime * doubleJumpHeightMultiplier;
        isHoldingJump = true;
        holdJumpTimer = 0f;
    }

    bool CanAirJump()
    {
        return canDoubleJump && airJumpsRemaining > 0;
    }

    void RecoverAirJumps()
    {
        if (!canDoubleJump)
        {
            airJumpsRemaining = 0;
            return;
        }

        airJumpsRemaining = maxAirJumps;
    }

    void UpdateHorizontalSpeed()
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.fixedDeltaTime;
        }

        if (dashInvincibilityTimer > 0f)
        {
            dashInvincibilityTimer -= Time.fixedDeltaTime;
        }

        if (isToxicSlowingDown)
        {
            UpdateToxicSlowdown();
            return;
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.fixedDeltaTime;
            velocity.x = dashVelocity;

            if (dashTimer <= 0f)
            {
                velocity.x = Mathf.Min(velocity.x, maxVelocity);
            }

            return;
        }

        float velocityRatio = velocity.x / maxVelocity;
        acceleration = maxAcceleration * (1f - velocityRatio);

        velocity.x += acceleration * Time.fixedDeltaTime;
        velocity.x = Mathf.Clamp(velocity.x, 0f, maxVelocity);

        maxHoldJumpTime = Mathf.Max(
            0.1f,
            maxMaxHoldJumpTime * Mathf.Clamp01(velocityRatio)
        );
    }

    void UpdateToxicTimer()
    {
        if (isToxicSlowingDown)
        {
            return;
        }

        toxicTimer += Time.fixedDeltaTime;

        if (toxicTimer < toxicRunTime)
        {
            return;
        }

        isToxicSlowingDown = true;
        toxicSlowdownTimer = 0f;
        toxicSlowdownStartSpeed = Mathf.Max(velocity.x, 0f);
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        isHoldingJump = false;
    }

    void UpdateToxicSlowdown()
    {
        toxicSlowdownTimer += Time.fixedDeltaTime;

        float t = toxicSlowdownDuration <= 0f ? 1f : Mathf.Clamp01(toxicSlowdownTimer / toxicSlowdownDuration);
        velocity.x = Mathf.Lerp(toxicSlowdownStartSpeed, 0f, t);

        if (toxicSlowdownTimer >= toxicSlowdownDuration)
        {
            velocity.x = 0f;
            Die();
        }
    }

    void UpdateJumpHold()
    {
        if (!isHoldingJump)
        {
            return;
        }

        holdJumpTimer += Time.fixedDeltaTime;

        if (holdJumpTimer >= activeHoldJumpTimeLimit)
        {
            isHoldingJump = false;
        }
    }

    void MoveVertically(ref Vector2 pos)
    {
        pos.y += velocity.y * Time.fixedDeltaTime;
    }

    void ApplyGravity()
    {
        if (!isHoldingJump)
        {
            velocity.y += gravity * Time.fixedDeltaTime;
        }
    }

    void TryLand(ref Vector2 pos, float previousY)
    {
        if (velocity.y > 0f)
        {
            return;
        }

        Bounds previousBounds = GetBoundsAtPosition(new Vector2(pos.x, previousY));
        Bounds currentBounds = GetBoundsAtPosition(pos);

        float previousFeetY = previousBounds.min.y;
        float currentFeetY = currentBounds.min.y;
        float castDistance = Mathf.Abs(previousFeetY - currentFeetY) + groundSnapDistance;
        Vector2 castOrigin = previousBounds.center;
        Vector2 castSize = previousBounds.size;

        RaycastHit2D groundHit = FindGroundBoxHit(
            castOrigin,
            castSize,
            Vector2.down,
            castDistance
        );

        Debug.DrawRay(
            new Vector2(previousBounds.min.x, previousFeetY),
            Vector2.down * castDistance,
            Color.red
        );

        Debug.DrawRay(
            new Vector2(previousBounds.max.x, previousFeetY),
            Vector2.down * castDistance,
            Color.red
        );

        if (groundHit.collider == null)
        {
            return;
        }

        Ground ground = groundHit.collider.GetComponentInParent<Ground>();

        if (ground == null)
        {
            return;
        }

        Bounds hitBounds = groundHit.collider.bounds;
        float hitTop = hitBounds.max.y;

        bool crossedTop =
            previousFeetY >= hitTop &&
            currentFeetY <= hitTop;

        bool isTouchingTop =
            currentFeetY <= hitTop &&
            currentFeetY >= hitTop - groundSnapDistance;

        bool boxIsOverGround =
            currentBounds.max.x >= hitBounds.min.x + 0.05f &&
            currentBounds.min.x <= hitBounds.max.x - 0.05f;

        if ((!crossedTop && !isTouchingTop) || !boxIsOverGround)
        {
            return;
        }

        groundHeight = hitTop;
        pos.y += groundHeight - currentFeetY;
        velocity.y = 0f;
        isGrounded = true;
        isHoldingJump = false;
        RecoverDashCharges();
        RecoverAirJumps();
    }

    void StickToGround(ref Vector2 pos)
    {
        float feetY = GetFeetY(pos);
        pos.y += groundHeight - feetY;
        velocity.y = 0f;
    }

    void CheckGroundAhead(Vector2 pos)
    {
        Bounds bounds = GetBoundsAtPosition(pos);
        float sideInset = Mathf.Min(0.05f, bounds.extents.x * 0.25f);
        float[] rayXs =
        {
            bounds.min.x + sideInset,
            bounds.center.x,
            bounds.max.x - sideInset
        };

        for (int i = 0; i < rayXs.Length; i++)
        {
            Vector2 rayOrigin = new Vector2(rayXs[i], bounds.min.y + 0.05f);
            RaycastHit2D hit = FindGroundOrObstacleHit(rayOrigin, Vector2.down, 0.25f);

            Debug.DrawRay(rayOrigin, Vector2.down * 0.25f, Color.blue);

            if (hit.collider != null)
            {
                return;
            }
        }

        isGrounded = false;
    }

    void CheckWall(ref Vector2 pos)
    {
        Bounds bounds = GetBoundsAtPosition(pos);
        Vector2 rayOrigin = new Vector2(bounds.max.x, bounds.center.y);
        float rayDistance = Mathf.Max(0.1f, velocity.x * Time.fixedDeltaTime);

        RaycastHit2D hit = FindGroundHit(
            rayOrigin,
            Vector2.right,
            rayDistance
        );

        Debug.DrawRay(rayOrigin, Vector2.right * rayDistance, Color.yellow);

        if (hit.collider == null)
        {
            return;
        }

        Ground ground = hit.collider.GetComponentInParent<Ground>();

        if (ground != null && bounds.min.y < hit.collider.bounds.max.y)
        {
            velocity.x = 0f;
        }
    }

    void CheckObstacles(ref Vector2 pos)
    {
        if (dashInvincibilityTimer > 0f)
        {
            return;
        }

        Bounds bounds = GetBoundsAtPosition(pos);
        float forwardDistance = Mathf.Max(0.1f, velocity.x * Time.fixedDeltaTime);
        float verticalDistance = Mathf.Max(0.1f, Mathf.Abs(velocity.y * Time.fixedDeltaTime));

        Obstacle overlappingObstacle = FindOverlappingObstacle(bounds);

        if (overlappingObstacle != null)
        {
            HitObstacle(overlappingObstacle);
            return;
        }

        RaycastHit2D forwardHit = FindObstacleBoxHit(bounds.center, bounds.size, Vector2.right, forwardDistance);
        Vector2 verticalDirection = velocity.y >= 0f ? Vector2.up : Vector2.down;
        RaycastHit2D verticalHit = FindObstacleBoxHit(bounds.center, bounds.size, verticalDirection, verticalDistance);

        TryHitObstacle(forwardHit);
        TryHitObstacle(verticalHit);

        if (forwardHit.collider != null || verticalHit.collider != null)
        {
            isGrounded = false;
            isHoldingJump = false;
        }
    }

    void CheckBaseReached(Vector2 pos)
    {
        if (baseCollider == null || reachedBase)
        {
            return;
        }

        if (maxRunDistance < minimumDistanceToReachBase && runDirection > 0)
        {
            return;
        }

        Bounds playerBounds = GetBoundsAtPosition(pos);

        if (playerBounds.Intersects(baseCollider.bounds))
        {
            ReachBase();
        }
    }

    Bounds GetBoundsAtPosition(Vector2 pos)
    {
        Bounds bounds = playerCollider.bounds;
        Vector3 delta = new Vector3(
            pos.x - transform.position.x,
            pos.y - transform.position.y,
            0f
        );

        bounds.center += delta;
        return bounds;
    }

    float GetFeetY(Vector2 pos)
    {
        return GetBoundsAtPosition(pos).min.y;
    }

    RaycastHit2D FindGroundHit(Vector2 origin, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider == playerCollider)
            {
                continue;
            }

            if (hits[i].collider.GetComponentInParent<Ground>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    RaycastHit2D FindGroundOrObstacleHit(Vector2 origin, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider == playerCollider)
            {
                continue;
            }

            if (hits[i].collider.GetComponentInParent<Ground>() != null || hits[i].collider.GetComponent<Obstacle>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    RaycastHit2D FindGroundBoxHit(Vector2 origin, Vector2 size, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            origin,
            size,
            0f,
            direction,
            distance
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider == playerCollider)
            {
                continue;
            }

            if (hits[i].collider.GetComponentInParent<Ground>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    void TrySnapToGroundBelow()
    {
        Bounds bounds = GetBoundsAtPosition(transform.position);
        Vector2 origin = new Vector2(bounds.center.x, bounds.center.y);
        RaycastHit2D hit = FindGroundHit(origin, Vector2.down, 50f);

        if (hit.collider == null)
        {
            return;
        }

        Ground ground = hit.collider.GetComponentInParent<Ground>();

        if (ground == null)
        {
            return;
        }

        Vector2 pos = transform.position;
        float feetY = GetFeetY(pos);

        groundHeight = hit.collider.bounds.max.y;
        pos.y += groundHeight - feetY;
        transform.position = pos;
        velocity.y = 0f;
        isGrounded = true;
        isHoldingJump = false;
        RecoverDashCharges();
        RecoverAirJumps();
    }

    RaycastHit2D FindObstacleHit(Vector2 origin, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider == playerCollider)
            {
                continue;
            }

            if (hits[i].collider.GetComponent<Obstacle>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    Obstacle FindOverlappingObstacle(Bounds bounds)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null || hits[i] == playerCollider)
            {
                continue;
            }

            Obstacle obstacle = hits[i].GetComponent<Obstacle>();

            if (obstacle != null)
            {
                return obstacle;
            }
        }

        return null;
    }

    RaycastHit2D FindObstacleBoxHit(Vector2 origin, Vector2 size, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            origin,
            size,
            0f,
            direction,
            distance
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider == playerCollider)
            {
                continue;
            }

            if (hits[i].collider.GetComponent<Obstacle>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    void TryHitObstacle(RaycastHit2D hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        Obstacle obstacle = hit.collider.GetComponent<Obstacle>();

        if (obstacle != null)
        {
            HitObstacle(obstacle);
        }
    }

    void HitObstacle(Obstacle obstacle)
    {
        if (!obstacle.PlayHitAnimationAndDestroy())
        {
            return;
        }

        float reducedLoss = obstacleSpeedLoss * (1f - (resistanceLevel * resistanceLossReductionPerLevel));
        velocity.x *= 1f - Mathf.Clamp01(reducedLoss);
        PlayHitAnimation();
    }

    void Die()
    {
        isDead = true;
        isHoldingJump = false;
        isGrounded = false;
        PlayDeathAnimation();
    }

    void ReachBase()
    {
        reachedBase = true;
        isDead = true;
        isHoldingJump = false;
        isGrounded = false;
    }

    void PlayJumpAnimation()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.PlayJump();
        }
    }

    void PlayAirJumpAnimation()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.PlayAirJump();
        }
    }

    void PlayDashAnimation()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.PlayDash();
        }
    }

    void PlayHitAnimation()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.PlayHit();
        }
    }

    void PlayDeathAnimation()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.PlayDeath();
        }
    }
}
