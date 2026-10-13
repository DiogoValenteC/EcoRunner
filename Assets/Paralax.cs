using UnityEngine;

public class Paralax : MonoBehaviour
{

    public float depth = 1;

    Player player;

    private void Awake()
    {
        player = GameObject.Find("Player").GetComponent<Player>();
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (player == null || !player.hasStarted || player.isDead)
        {
            return;
        }

        float realVelocity = (player.velocity.x * player.runDirection) / depth;
        Vector2 pos = transform.position;

        pos.x -= realVelocity * Time.fixedDeltaTime;

        if (pos.x <= -200)
        {
            pos.x = 200;
        }
        else if (pos.x >= 200)
        {
            pos.x = -200;
        }

        transform.position = pos;
    }
}
