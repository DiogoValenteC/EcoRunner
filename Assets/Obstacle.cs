using UnityEngine;
using System.Collections.Generic;

public class Obstacle : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Sprite[] hitFrames;
    public float animationFps = 12f;

    Player player;
    Ground parentGround;
    Collider2D obstacleCollider;
    bool isAnimatingHit;
    float frameTimer;
    int frameIndex;

    private void Awake()
    {
        GameObject playerObject = GameObject.Find("Player");

        if (playerObject != null)
        {
            player = playerObject.GetComponent<Player>();
        }

        parentGround = GetComponentInParent<Ground>();
        obstacleCollider = GetComponent<Collider2D>();
        SetupRenderer();
        LoadEditorSpritesIfNeeded();
        ShowFirstFrame();
        MatchColliderToCurrentSprite();
    }

    void Update()
    {
        if (!isAnimatingHit)
        {
            return;
        }

        AdvanceHitAnimation();
    }

    private void FixedUpdate()
    {
        if (parentGround == null)
        {
            parentGround = GetComponentInParent<Ground>();
        }

        if (parentGround != null || player == null)
        {
            return;
        }

        Vector2 pos = transform.position;
        pos.x -= player.velocity.x * Time.fixedDeltaTime;

        if (pos.x < -100)
        {
            Destroy(gameObject);
        }

        transform.position = pos;
    }

    public bool PlayHitAnimationAndDestroy()
    {
        if (isAnimatingHit)
        {
            return false;
        }

        if (hitFrames == null || hitFrames.Length == 0)
        {
            Destroy(gameObject);
            return true;
        }

        isAnimatingHit = true;
        frameTimer = 0f;
        frameIndex = 0;

        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = false;
        }

        SetCurrentSprite();
        return true;
    }

    void AdvanceHitAnimation()
    {
        if (hitFrames == null || hitFrames.Length == 0 || animationFps <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / animationFps;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;

            if (frameIndex >= hitFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
        }

        SetCurrentSprite();
    }

    void SetupRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void ShowFirstFrame()
    {
        if (hitFrames == null || hitFrames.Length == 0)
        {
            return;
        }

        frameIndex = 0;
        SetCurrentSprite();
        MatchColliderToCurrentSprite();
    }

    void SetCurrentSprite()
    {
        if (spriteRenderer == null || hitFrames == null || hitFrames.Length == 0)
        {
            return;
        }

        frameIndex = Mathf.Clamp(frameIndex, 0, hitFrames.Length - 1);
        spriteRenderer.sprite = hitFrames[frameIndex];
    }

    void MatchColliderToCurrentSprite()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || obstacleCollider == null)
        {
            return;
        }

        BoxCollider2D boxCollider = obstacleCollider as BoxCollider2D;

        if (boxCollider == null)
        {
            return;
        }

        Bounds bounds = spriteRenderer.sprite.bounds;
        boxCollider.size = bounds.size;
        boxCollider.offset = bounds.center;
    }

    void LoadEditorSpritesIfNeeded()
    {
        if (hitFrames != null && hitFrames.Length > 0)
        {
            return;
        }

        hitFrames = LoadSprites("Obstacle/ChatGPT Image 24 de mai. de 2026, 02_24_06 (1)");
    }

    Sprite[] LoadSprites(string resourcePath)
    {
        Sprite[] assets = Resources.LoadAll<Sprite>(resourcePath);
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i];

            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort(
            delegate (Sprite a, Sprite b)
            {
                return ExtractFrameIndex(a.name).CompareTo(ExtractFrameIndex(b.name));
            }
        );

        return sprites.ToArray();
    }

    int ExtractFrameIndex(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');

        if (underscore < 0 || underscore >= spriteName.Length - 1)
        {
            return 0;
        }

        int value;
        return int.TryParse(spriteName.Substring(underscore + 1), out value) ? value : 0;
    }
}
