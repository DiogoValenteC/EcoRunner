using System.Collections.Generic;
using UnityEngine;

public class Ground : MonoBehaviour
{
    static readonly List<Ground> activeGrounds = new List<Ground>();
    static Player player;
    static bool isMaintainingQueue;
    static bool isCreatingClone;
    static int lastQueueFrame = -1;

    [Header("Runtime Info")]
    public float groundHeight;
    public float groundRight;
    public float screenRight;
    public bool hasSpawnedNext;

    [Header("Generation")]
    public float spawnOffset = 2f;
    public int targetGroundCount = 4;
    public float destroyBehindCamera = 30f;
    public float minGroundY = -7f;
    public float maxGroundY = 3f;
    public float maxStepDown = 5f;
    public float heightSafety = 0.65f;
    public float distanceSafety = 0.75f;

    [Header("Obstacles")]
    public Obstacle obstacleTemplate;
    public float obstacleSpawnChance = 0.55f;
    public int maxObstacleChainLength = 4;
    public float earlyObstacleChainDistance = 500f;
    public int earlyMaxObstacleChainLength = 2;
    public float obstacleEdgePadding = 1f;

    BoxCollider2D collider2d;

    struct ObstacleSize
    {
        public readonly float width;
        public readonly float height;
        public readonly float offsetX;
        public readonly float offsetY;

        public ObstacleSize(float width, float height, float offsetX, float offsetY)
        {
            this.width = Mathf.Max(0.01f, width);
            this.height = Mathf.Max(0.01f, height);
            this.offsetX = offsetX;
            this.offsetY = offsetY;
        }
    }

    public float LeftEdge
    {
        get { return collider2d.bounds.min.x; }
    }

    public float RightEdge
    {
        get { return collider2d.bounds.max.x; }
    }

    public float Width
    {
        get { return collider2d.bounds.size.x; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activeGrounds.Clear();
        player = null;
        isMaintainingQueue = false;
        isCreatingClone = false;
        lastQueueFrame = -1;
    }

    private void Awake()
    {
        CachePlayer();
        collider2d = GetComponent<BoxCollider2D>();
        RefreshBounds();

        if (!isCreatingClone)
        {
            RegisterGround(this);
        }
    }

    private void Start()
    {
        RemoveNullGrounds();
        SortGroundsByPosition();
    }

    private void OnDestroy()
    {
        activeGrounds.Remove(this);
    }

    private void FixedUpdate()
    {
        if (player == null || player.isDead)
        {
            return;
        }

        MoveWithWorld();
        RefreshBounds();
        MaintainGroundQueue();
    }

    void CachePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.Find("Player");

        if (playerObject != null)
        {
            player = playerObject.GetComponent<Player>();
        }
    }

    void MoveWithWorld()
    {
        Vector2 pos = transform.position;
        pos.x -= player.velocity.x * player.runDirection * Time.fixedDeltaTime;
        transform.position = pos;
    }

    void RefreshBounds()
    {
        if (collider2d == null)
        {
            return;
        }

        groundHeight = collider2d.bounds.max.y;
        groundRight = collider2d.bounds.max.x;
        screenRight = Camera.main != null ? Camera.main.transform.position.x * 2f : 0f;
    }

    void MaintainGroundQueue()
    {
        if (isMaintainingQueue || lastQueueFrame == Time.frameCount || player == null || collider2d == null)
        {
            return;
        }

        isMaintainingQueue = true;

        RemoveNullGrounds();
        SortGroundsByPosition();

        Ground groundToExtend = GetFirstGroundReadyToSpawnNext();

        if (groundToExtend != null)
        {
            ReplaceGroundAtFront(groundToExtend);
            lastQueueFrame = Time.frameCount;
        }

        isMaintainingQueue = false;
    }

    bool IsBehindCamera()
    {
        return RightEdge < GetCameraLeft();
    }

    void ReplaceGroundAtFront(Ground groundToReplace)
    {
        if (groundToReplace.hasSpawnedNext)
        {
            return;
        }

        SortGroundsByPosition();

        Ground previousGround = GetRightmostGround();

        if (previousGround == null || previousGround == groundToReplace)
        {
            return;
        }

        groundToReplace.hasSpawnedNext = true;

        Ground newGround = CreateGroundClone(groundToReplace);

        if (newGround == null)
        {
            groundToReplace.hasSpawnedNext = false;
            return;
        }

        float nextTop = ChooseNextGroundHeight(previousGround.groundHeight);
        float nextGap = ChooseNextGap(nextTop);
        PositionAfter(newGround, previousGround, nextGap, nextTop);

        newGround.GenerateObstacles();
        newGround.RefreshBounds();
        newGround.hasSpawnedNext = false;
        RegisterGround(newGround);
    }

    Ground CreateGroundClone(Ground source)
    {
        isCreatingClone = true;
        GameObject cloneObject = Instantiate(source.gameObject);
        isCreatingClone = false;

        Ground clone = cloneObject.GetComponent<Ground>();

        if (clone == null)
        {
            Destroy(cloneObject);
            return null;
        }

        clone.collider2d = clone.GetComponent<BoxCollider2D>();
        clone.RefreshBounds();
        clone.ClearGeneratedObstacles();
        clone.ClearBaseChildren();
        clone.hasSpawnedNext = false;
        return clone;
    }

    void PositionAfter(Ground clone, Ground previousGround, float gap, float targetTop)
    {
        Vector2 pos = clone.transform.position;
        pos.x += (previousGround.RightEdge + gap) - clone.LeftEdge;
        pos.y += targetTop - clone.groundHeight;
        clone.transform.position = pos;
        clone.RefreshBounds();
    }

    float GetCameraLeft()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
        {
            float cameraX = mainCamera != null ? mainCamera.transform.position.x : 0f;
            return cameraX - destroyBehindCamera;
        }

        return mainCamera.transform.position.x - (mainCamera.orthographicSize * mainCamera.aspect);
    }

    float GetCameraRight()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
        {
            float cameraX = mainCamera != null ? mainCamera.transform.position.x : 0f;
            return cameraX + destroyBehindCamera;
        }

        return mainCamera.transform.position.x + (mainCamera.orthographicSize * mainCamera.aspect);
    }

    float ChooseNextGroundHeight(float previousHeight)
    {
        float maxReachableHeight = previousHeight + (player.MaxJumpHeight * heightSafety);
        float minY = Mathf.Max(minGroundY, previousHeight - maxStepDown);
        float maxY = Mathf.Min(maxGroundY, maxReachableHeight);

        if (maxY <= minY)
        {
            maxY = minY + 0.5f;
        }

        return Random.Range(minY, maxY);
    }

    float ChooseNextGap(float nextTop)
    {
        float maxGap = Mathf.Max(0.5f, player.GetReachableDistance(nextTop) * distanceSafety);
        float minGap = Mathf.Min(Mathf.Max(0.5f, spawnOffset), maxGap);

        if (maxGap <= minGap)
        {
            return maxGap;
        }

        return Random.Range(minGap, maxGap);
    }

    void GenerateObstacles()
    {
        if (obstacleTemplate == null || collider2d == null || maxObstacleChainLength <= 0)
        {
            return;
        }

        if (Random.value > obstacleSpawnChance)
        {
            return;
        }

        ObstacleSize obstacleSize = GetObstacleSize();
        int maxChainThatFits = Mathf.FloorToInt(
            (collider2d.size.x - (obstacleEdgePadding * 2f)) / obstacleSize.width
        );

        if (maxChainThatFits <= 0)
        {
            return;
        }

        int activeMaxChainLength = GetActiveMaxObstacleChainLength();

        int obstacleCount = Random.Range(
            1,
            Mathf.Min(activeMaxChainLength, maxChainThatFits) + 1
        );

        float chainWidth = obstacleSize.width * obstacleCount;
        float groundLeft = collider2d.offset.x - (collider2d.size.x * 0.5f);
        float groundRight = collider2d.offset.x + (collider2d.size.x * 0.5f);
        float minStartX = groundLeft + obstacleEdgePadding;
        float maxStartX = groundRight - obstacleEdgePadding - chainWidth;

        if (maxStartX < minStartX)
        {
            return;
        }

        float startX = Random.Range(minStartX, maxStartX);
        float groundTop = collider2d.offset.y + (collider2d.size.y * 0.5f);

        for (int i = 0; i < obstacleCount; i++)
        {
            GameObject obstacle = Instantiate(obstacleTemplate.gameObject, transform);
            obstacle.name = "GeneratedObstacle";
            obstacle.transform.localRotation = Quaternion.identity;
            obstacle.transform.localScale = GetObstacleLocalScale();
            obstacle.transform.localPosition = new Vector3(
                startX + (obstacleSize.width * i) + (obstacleSize.width * 0.5f) - obstacleSize.offsetX,
                groundTop - obstacleSize.offsetY + (obstacleSize.height * 0.5f),
                0f
            );
        }
    }

    int GetActiveMaxObstacleChainLength()
    {
        if (player != null && player.maxRunDistance < earlyObstacleChainDistance)
        {
            return Mathf.Max(1, earlyMaxObstacleChainLength);
        }

        return maxObstacleChainLength;
    }

    ObstacleSize GetObstacleSize()
    {
        Vector3 parentScale = GetSafeScale(transform.localScale);
        Vector3 templateScale = obstacleTemplate.transform.localScale;
        BoxCollider2D obstacleCollider = obstacleTemplate.GetComponent<BoxCollider2D>();

        if (obstacleCollider != null)
        {
            return new ObstacleSize(
                Mathf.Abs((obstacleCollider.size.x * templateScale.x) / parentScale.x),
                Mathf.Abs((obstacleCollider.size.y * templateScale.y) / parentScale.y),
                (obstacleCollider.offset.x * templateScale.x) / parentScale.x,
                (obstacleCollider.offset.y * templateScale.y) / parentScale.y
            );
        }

        SpriteRenderer spriteRenderer = obstacleTemplate.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            Bounds localBounds = spriteRenderer.localBounds;

            return new ObstacleSize(
                Mathf.Abs((localBounds.size.x * templateScale.x) / parentScale.x),
                Mathf.Abs((localBounds.size.y * templateScale.y) / parentScale.y),
                (localBounds.center.x * templateScale.x) / parentScale.x,
                (localBounds.center.y * templateScale.y) / parentScale.y
            );
        }

        return new ObstacleSize(1f, 1f, 0f, 0f);
    }

    Vector3 GetObstacleLocalScale()
    {
        Vector3 parentScale = GetSafeScale(transform.localScale);
        Vector3 templateScale = obstacleTemplate.transform.localScale;

        return new Vector3(
            templateScale.x / parentScale.x,
            templateScale.y / parentScale.y,
            templateScale.z
        );
    }

    Vector3 GetSafeScale(Vector3 scale)
    {
        if (Mathf.Abs(scale.x) < 0.001f)
        {
            scale.x = 1f;
        }

        if (Mathf.Abs(scale.y) < 0.001f)
        {
            scale.y = 1f;
        }

        return scale;
    }

    void ClearGeneratedObstacles()
    {
        Obstacle[] obstacles = GetComponentsInChildren<Obstacle>();

        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i].gameObject != gameObject)
            {
                Destroy(obstacles[i].gameObject);
            }
        }
    }

    void ClearBaseChildren()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = children.Length - 1; i >= 0; i--)
        {
            if (children[i] != transform && children[i].name == "Base")
            {
                Destroy(children[i].gameObject);
            }
        }
    }

    static void RegisterGround(Ground ground)
    {
        RemoveNullGrounds();

        if (!activeGrounds.Contains(ground))
        {
            activeGrounds.Add(ground);
        }

        SortGroundsByPosition();
    }

    static void RemoveNullGrounds()
    {
        for (int i = activeGrounds.Count - 1; i >= 0; i--)
        {
            if (activeGrounds[i] == null)
            {
                activeGrounds.RemoveAt(i);
            }
        }
    }

    static void SortGroundsByPosition()
    {
        activeGrounds.Sort(
            delegate (Ground a, Ground b)
            {
                return a.LeftEdge.CompareTo(b.LeftEdge);
            }
        );
    }

    static Ground GetLeftmostGround()
    {
        RemoveNullGrounds();

        if (activeGrounds.Count == 0)
        {
            return null;
        }

        SortGroundsByPosition();
        return activeGrounds[0];
    }

    static Ground GetFirstGroundReadyToSpawnNext()
    {
        RemoveNullGrounds();
        SortGroundsByPosition();

        for (int i = 0; i < activeGrounds.Count; i++)
        {
            if (!activeGrounds[i].hasSpawnedNext && activeGrounds[i].IsBehindCamera())
            {
                return activeGrounds[i];
            }
        }

        return null;
    }

    static Ground GetRightmostGround()
    {
        RemoveNullGrounds();

        if (activeGrounds.Count == 0)
        {
            return null;
        }

        SortGroundsByPosition();
        return activeGrounds[activeGrounds.Count - 1];
    }
}
