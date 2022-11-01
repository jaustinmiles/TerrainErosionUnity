using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public struct SnowBall
{
    public float sediment;
    public float xp;
    public float yp;
    public float vx;
    public float vy;
    public float x;
    public float y;
    public float decay;
    public float ox;
    public float oy;
    public bool active;
}
public class Snowball : MonoBehaviour
{
    public Terrain terrain;
    public int samples;
    public float radius;

    public float erosionRate;
    public float depositionRate;
    public float friction;

    public float decayScale;
    public float speed;
    
    public int maxIterations;
    public int numErodes;
    public bool doBlur;
    public int kernelSize;

    public float velocityThreshold;

    public bool coroutine;
    public int coroutineBreakInterval;

    public bool show;

    private LocalMinMaxViz _mExtrema;

    private float _scale;
    private SnowBall[] _balls;
    private int _res;
    private int _iteration;
    private int _currErodes;

    // Start is called before the first frame update
    void Start()
    {
        // GetComponent<TerrainGenerator>().CreateTerrain();
        _mExtrema = GetComponent<LocalMinMaxViz>();
        Initialize();
        _currErodes = 0;
        StartCoroutine(Trace());
    }

    SnowBall DefaultBall()
    {
        int x = Random.Range(0, _res);
        int y = Random.Range(0, _res);
        float ox = Random.Range(-1, 1) * radius;
        float oy = Random.Range(-1, 1) * radius;
        return new SnowBall
        {
            sediment = 0,
            x = x,
            y = y,
            xp = x,
            yp = y,
            vx = 0,
            vy = 0,
            decay = 1f,
            ox = ox,
            oy = oy,
            active = true,
        };
    }

    void CreateSnowBalls()
    {
        _balls = new SnowBall[samples];
        for (int i = 0; i < samples; i++)
        {
            _balls[i] = DefaultBall();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_currErodes < numErodes && _iteration >= maxIterations)
        {
            Debug.Log("restarting");
            Initialize();
            _iteration = 0;
            _currErodes++;
            if (doBlur)
            {
                float[,] heightMap = terrain.terrainData.GetHeights(0, 0, _res, _res);
                GaussianBlur blur = new GaussianBlur(kernelSize);
                Debug.Log("blurred");
                blur.Apply(ref heightMap);   
                terrain.terrainData.SetHeights(0, 0, heightMap);
                RectInt region = new RectInt(0, 0, _res, _res);
                terrain.terrainData.DirtyHeightmapRegion(region, TerrainHeightmapSyncControl.HeightOnly);
                // doBlur = false;
            }
            StartCoroutine(Trace());
        }
        else if (_iteration >= maxIterations)
        {
            if (doBlur)
            {
                float[,] heightMap = terrain.terrainData.GetHeights(0, 0, _res, _res);
                GaussianBlur blur = new GaussianBlur(kernelSize);
                Debug.Log("blurred");
                blur.Apply(ref heightMap);   
                terrain.terrainData.SetHeights(0, 0, heightMap);
                RectInt region = new RectInt(0, 0, _res, _res);
                terrain.terrainData.DirtyHeightmapRegion(region, TerrainHeightmapSyncControl.HeightOnly);
                doBlur = false;
            }
        }
        // iteration++;
    }
    

    void VisualizeGizmos()
    {
        if (_balls == null)
            return;
        foreach (SnowBall b in _balls)
        {
            if (!b.active)
            {
                continue;
            }
            Gizmos.color = Color.blue;
            var terrainData = terrain.terrainData;
            var position = this.transform.position;
            float ballX = (b.xp / _res) * terrainData.size.x + position.x;
            float ballY = (b.yp / _res) * terrainData.size.z + position.z;
            float ballZ = terrainData.GetHeight((int)b.x, (int)b.y) + position.y;
         
            Gizmos.DrawSphere(new Vector3(ballX, ballZ, ballY), terrainData.size.x / 140f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (show)
            VisualizeGizmos();
    }

    void Initialize()
    {
        _res = terrain.terrainData.heightmapResolution;
        _scale = 1f;
        _iteration = 0;
        CreateSnowBalls();
    }

    IEnumerator Trace()
    {
        int currSubroutineCount = 0;
        for (int j = 0; j < maxIterations; j++)
        {
            float[,] heightMap = terrain.terrainData.GetHeights(0, 0, _res, _res);
            for (int i = 0; i < samples; i++)
            {
                RunBall(heightMap, i);
                if (coroutine)
                {
                    currSubroutineCount++;
                    if (currSubroutineCount % coroutineBreakInterval == 0)
                    {
                        yield return null;
                        SetHeightsAndMarkDirty(ref heightMap);
                    }
                }
            }
            _iteration++;
            yield return null;
        }
    }

    void SetHeightsAndMarkDirty(ref float[,] heights)
    {
        terrain.terrainData.SetHeights(0, 0, heights);
        RectInt region = new RectInt(0, 0, _res, _res);
        terrain.terrainData.DirtyHeightmapRegion(region, TerrainHeightmapSyncControl.HeightOnly);
    }

    void RunBall(float[,] heightMap, int i)
    {
        SnowBall ball = _balls[i];
        float x = ball.x;
        float y = ball.y;
        // ball is out of bounds
        if (x < 0 || x >= _res || y < 0 || y >= _res)
        {
            _balls[i].active = false;
            return;
        }
        
        // ball is in local minimum
        if (_mExtrema.IsLocalExtrema((int)x, (int)y, heightMap, false))
        {
            _balls[i].active = false;
            return;
        }
        
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(x / _res, y / _res);
        Vector2 previousVelocity = new Vector2(ball.vx, ball.vy);
        
        // normal += RandomVector(0.05f);
        ball.vx = friction * ball.vx + normal.x * speed;
        ball.vy = friction * ball.vy + normal.z * speed;
        
        // if going slow and at a flat surface
        // Debug.Log(Math.Abs(Math.Sqrt(ball.vx * ball.vx + ball.vy * ball.vy)));
        if (NormalYNearOne(normal) && Math.Abs(Math.Sqrt(ball.vx * ball.vx + ball.vy * ball.vy)) < velocityThreshold)
        {
            _balls[i].active = false;
            return;
        }

        Vector2 newVelocity = new Vector2(ball.vx, ball.vy);
        //TODO: turn this back on
        // if (Vector2.Dot(previousVelocity, newVelocity) < 0)
        // {
        //     Debug.Log("Disabled ball for switching direction");
        //     balls[i].active = false;
        //     return;
        // }

        float deposit = ball.sediment * depositionRate * normal.y;
        float erosion = erosionRate * (1f - normal.y);

        // TODO: maybe add back random sampling of offset for heightmap manipulation

        DecreaseHeightsBilinear(ball, ref heightMap, deposit, erosion);
        ball.sediment = ball.sediment + erosion - deposit;
        ball.xp = x;
        ball.yp = y;
        ball.x += ball.vx;
        ball.y += ball.vy;
        ball.decay *= decayScale;
        _balls[i] = ball;
    }

    void DecreaseHeightsBilinear(SnowBall ball, ref float[,] heightMap, float deposit, float erosion)
    {
        // reverse because of weird coordinate system issue
        float x = ball.yp;
        float y = ball.xp;
        float x1 = Mathf.Floor(x);
        float x2 = x1 + 1;
        float y1 = Mathf.Floor(y);
        float y2 = y1 + 1;

        float tx = (x - x1) / (x2 - x1);
        float ty = (y - y1) / (y2 - y1);

        float change = deposit - erosion;

        Vector2 start = new Vector2(tx, ty);
        //top left
        float max = new Vector2(1, 1).magnitude;
        float tl = max - ((start - new Vector2(0, 0)).magnitude);
        float tr = max - ((start - new Vector2(1, 0)).magnitude);
        float bl = max - ((start - new Vector2(0, 1)).magnitude);
        float br = max - ((start - new Vector2(1, 1)).magnitude);

        float norm = tl + tr + bl + br;
        tl /= norm;
        tr /= norm;
        bl /= norm;
        br /= norm;

        int x1I = (int)x1;
        int x2I = (int)x2;
        int y1I = (int)y1;
        int y2I = (int)y2;
        
        heightMap[x1I, y1I] += change * tl;
        if (x2I < _res)
            heightMap[x2I, y1I] += change * tr;
        if (y2 < _res)
            heightMap[x1I, y2I] += change * bl;
        if (x2 < _res && y2 < _res)
            heightMap[x2I, y2I] += change * br;
    }

    Vector3 RandomVector(float magnitude)
    {
        float x = Random.Range(-magnitude, magnitude);
        float y = Random.Range(-magnitude, magnitude);
        float z = Random.Range(-magnitude, magnitude);
        return new Vector3(x, y, z);
    }

    bool NormalYNearOne(Vector3 normal)
    {
        float epsilon = 0.05f;
        float low = 1f - epsilon;
        float hi = 1f + epsilon;
        if (normal.y > low && normal.y < hi)
        {
            return true;
        }
        return false;
    }
}
